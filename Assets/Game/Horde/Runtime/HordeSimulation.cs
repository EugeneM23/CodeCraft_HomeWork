using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.Horde
{
    /// <summary>
    /// The crowd: a fixed number of monster slots in <see cref="NativeArray{T}"/>s (position, velocity,
    /// heading, health, state, animation time, speed jitter) with a free list, and the frame step as a
    /// chain of Burst jobs: spatial hash, steering over the <see cref="HordeFlowField"/>, contacts
    /// with the tank and blasts, totals, and render prep (frustum cull into <see cref="Visible"/>).
    /// <para>
    /// Frame contract: <see cref="Schedule"/> in the frame, <see cref="Complete"/> at the start of the
    /// next (it frees the dead into <see cref="Deaths"/>). Everything else (spawn, kill, reads) only
    /// while no step runs. The grid heights and the flow field's readable buffer are read by the step,
    /// so the owner completes the step before it updates the grid or swaps the flow field.
    /// </para>
    /// No managed allocations after construction.
    /// </summary>
    public sealed class HordeSimulation : IDisposable
    {
        /// <summary>A step never integrates more than this (a hitch must not teleport the crowd through walls).</summary>
        public const float MAX_DELTA_TIME = 0.05f;
        /// <summary>Spatial hash cell, metres.</summary>
        public const float HASH_CELL = 1f;

        private const int BATCH = 64;
        private const int MAX_AREAS = 64;

        private static readonly ProfilerMarker SimulateMarker = new("Horde.Simulate");
        private static readonly ProfilerMarker CompleteMarker = new("Horde.Complete");
        private static readonly ProfilerMarker HashMarker = new("Horde.Job.Hash");
        private static readonly ProfilerMarker SteerMarker = new("Horde.Job.Steer");
        private static readonly ProfilerMarker ContactMarker = new("Horde.Job.Contact");
        private static readonly ProfilerMarker RenderMarker = new("Horde.Job.Render");

        private readonly HordeNavGrid _grid;
        private readonly HordeFlowField _flow;
        private readonly int2 _hashSize;
        private readonly float2 _hashOrigin;

        private NativeArray<float3> _position;
        private NativeArray<float3> _nextPosition;
        private NativeArray<float2> _velocity;
        private NativeArray<float> _heading;
        private NativeArray<float> _health;
        private NativeArray<HordeState> _state;
        private NativeArray<float> _animTime;
        private NativeArray<float> _speedScale;
        private NativeArray<float> _attackDamage;
        private NativeArray<int> _free;
        private NativeArray<int> _cellStart;
        private NativeArray<int> _cursor;
        private NativeArray<int> _cellIndex;
        private NativeArray<int> _sorted;
        private NativeArray<float> _totals;
        private NativeArray<float4> _planes;
        private NativeList<HordeAreaDamage> _areas;
        private NativeList<HordeAreaDamage> _areasRunning;
        private NativeQueue<HordeDeathEvent> _deathQueue;
        private NativeList<HordeDeathEvent> _deaths;
        private NativeList<HordeInstance> _visible;

        private Unity.Mathematics.Random _random;
        private JobHandle _handle;
        private bool _running;
        private bool _cull;
        private int _freeCount;
        private int _alive;
        private float _stepDamage;
        private int _near;
        private HordeClip _runClip;
        private HordeClip _attackClip;
        private HordeClip _idleClip;

        public HordeSimulation(int capacity, HordeNavGrid grid, HordeFlowField flow, in HordeSettings settings, uint seed = 1)
        {
            if (capacity <= 0)
                throw new ArgumentException("HordeSimulation needs a positive capacity.");
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            Settings = settings;
            Capacity = capacity;
            _random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);

            _hashOrigin = grid.Settings.Origin;
            _hashSize = math.max((int2)math.ceil((float2)grid.Size * grid.CellSize / HASH_CELL), 1);
            int hashCells = _hashSize.x * _hashSize.y;

            _position = new NativeArray<float3>(capacity, Allocator.Persistent);
            _nextPosition = new NativeArray<float3>(capacity, Allocator.Persistent);
            _velocity = new NativeArray<float2>(capacity, Allocator.Persistent);
            _heading = new NativeArray<float>(capacity, Allocator.Persistent);
            _health = new NativeArray<float>(capacity, Allocator.Persistent);
            _state = new NativeArray<HordeState>(capacity, Allocator.Persistent);
            _animTime = new NativeArray<float>(capacity, Allocator.Persistent);
            _speedScale = new NativeArray<float>(capacity, Allocator.Persistent);
            _attackDamage = new NativeArray<float>(capacity, Allocator.Persistent);
            _free = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _cellStart = new NativeArray<int>(hashCells + 1, Allocator.Persistent);
            _cursor = new NativeArray<int>(hashCells, Allocator.Persistent);
            _cellIndex = new NativeArray<int>(capacity, Allocator.Persistent);
            _sorted = new NativeArray<int>(capacity, Allocator.Persistent);
            _totals = new NativeArray<float>(2, Allocator.Persistent);
            _planes = new NativeArray<float4>(6, Allocator.Persistent);
            _areas = new NativeList<HordeAreaDamage>(8, Allocator.Persistent);
            _areasRunning = new NativeList<HordeAreaDamage>(8, Allocator.Persistent);
            _deathQueue = new NativeQueue<HordeDeathEvent>(Allocator.Persistent);
            _deaths = new NativeList<HordeDeathEvent>(64, Allocator.Persistent);
            _visible = new NativeList<HordeInstance>(capacity, Allocator.Persistent);

            // Slot 0 comes out first.
            for (int i = 0; i < capacity; i++)
                _free[i] = capacity - 1 - i;
            _freeCount = capacity;

            var still = new HordeClip { start = 0, count = 1, frameRate = 30f, loop = true };
            _runClip = _attackClip = _idleClip = still;
        }

        public HordeSettings Settings { get; set; }

        public int Capacity { get; }

        /// <summary>Monsters walking or attacking (the dying of a running step included).</summary>
        public int AliveCount => _alive;

        /// <summary>A step is on the workers.</summary>
        public bool IsRunning => _running;

        /// <summary>Damage the attacking monsters dealt in the last completed step.</summary>
        public float StepDamage => _stepDamage;

        /// <summary>Live monsters within <see cref="HordeSettings.NearRadius"/> of the tank in the last completed step.</summary>
        public int NearCount => _near;

        /// <summary>
        /// Deaths of the completed steps and of <see cref="Kill"/>, oldest first. The owner reads them
        /// and calls <see cref="ClearDeaths"/>; their slots are already free.
        /// </summary>
        public NativeArray<HordeDeathEvent> Deaths => _deaths.AsArray();

        /// <summary>Instances of the monsters the last completed step saw in the frustum (until the next <see cref="Schedule"/>).</summary>
        public NativeArray<HordeInstance> Visible => _visible.AsArray();

        public void ClearDeaths() => _deaths.Clear();

        /// <summary>Animation clips for the render prep (rows of the VAT texture).</summary>
        public void SetClips(HordeClip run, HordeClip attack, HordeClip idle)
        {
            _runClip = run;
            _attackClip = attack;
            _idleClip = idle;
        }

        /// <summary>Frustum planes (Unity's <c>GeometryUtility.CalculateFrustumPlanes</c> order and sign) for the cull; null turns culling off.</summary>
        public void SetCullingPlanes(Plane[] planes)
        {
            ThrowIfRunning();
            _cull = planes != null && planes.Length >= 6;
            if (!_cull)
                return;
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = planes[i].normal;
                _planes[i] = new float4(n.x, n.y, n.z, planes[i].distance);
            }
        }

        // ------------------------------------------------------------------ slots

        public HordeState GetState(int index) => _state[index];

        public float3 GetPosition(int index) => _position[index];

        public float GetHealth(int index) => _health[index];

        public float GetHeading(int index) => _heading[index];

        /// <summary>
        /// A new monster at <paramref name="position"/> (the feet), facing <paramref name="heading"/>
        /// (radians, 0 faces +Z). Returns its slot, or -1 when the horde is full.
        /// </summary>
        public int Spawn(float3 position, float heading)
        {
            ThrowIfRunning();
            if (_freeCount == 0)
                return -1;

            int i = _free[--_freeCount];
            HordeSettings s = Settings;
            _position[i] = position;
            _nextPosition[i] = position;
            _velocity[i] = float2.zero;
            _heading[i] = heading;
            _health[i] = s.Health;
            _state[i] = HordeState.Walk;
            _animTime[i] = _random.NextFloat(0f, 10f);
            _speedScale[i] = 1f + _random.NextFloat(-s.SpeedJitter, s.SpeedJitter);
            _attackDamage[i] = 0f;
            _alive++;
            return i;
        }

        /// <summary>Kills a live monster now: its slot is freed and a death (<see cref="HordeDeathCause.Killed"/>) is added.</summary>
        public bool Kill(int index)
        {
            ThrowIfRunning();
            HordeState s = _state[index];
            if (s != HordeState.Walk && s != HordeState.Attack)
                return false;
            _deaths.Add(new HordeDeathEvent
            {
                Index = index, Position = _position[index], Heading = _heading[index], Cause = HordeDeathCause.Killed
            });
            Release(index);
            return true;
        }

        /// <summary>Frees every slot without death events.</summary>
        public void Clear()
        {
            ThrowIfRunning();
            for (int i = 0; i < Capacity; i++)
                _state[i] = HordeState.Free;
            for (int i = 0; i < Capacity; i++)
                _free[i] = Capacity - 1 - i;
            _freeCount = Capacity;
            _alive = 0;
            _visible.Clear();
        }

        /// <summary>A blast for the next step: live monsters in the sphere take damage (full at the centre, half at the edge).</summary>
        public void AddAreaDamage(float3 center, float radius, float damage)
        {
            if (_areas.Length >= MAX_AREAS || radius <= 0f || damage <= 0f)
                return;
            _areas.Add(new HordeAreaDamage { Center = center, Radius = radius, Damage = damage });
        }

        // ------------------------------------------------------------------ step

        /// <summary>
        /// Schedules one step of <paramref name="deltaTime"/> (clamped to <see cref="MAX_DELTA_TIME"/>)
        /// against <paramref name="tank"/>. Call <see cref="Complete"/> at the start of the next frame.
        /// </summary>
        public JobHandle Schedule(float deltaTime, in HordeTank tank, JobHandle dependsOn = default)
        {
            ThrowIfRunning();
            using (SimulateMarker.Auto())
            {
                float dt = math.clamp(deltaTime, 0f, MAX_DELTA_TIME);
                HordeSettings settings = Settings;
                _visible.Clear();
                (_areas, _areasRunning) = (_areasRunning, _areas);
                _areas.Clear();

                var grid = new HordeGridView
                {
                    Heights = _grid.Heights,
                    Flow = _flow.Directions,
                    Size = _grid.Size,
                    Origin = _grid.Settings.Origin,
                    CellSize = _grid.CellSize,
                    MaxStep = _grid.MaxStep
                };

                JobHandle hash = new HordeHashJob
                {
                    Position = _position,
                    State = _state,
                    CellStart = _cellStart,
                    Cursor = _cursor,
                    CellIndex = _cellIndex,
                    Sorted = _sorted,
                    HashSize = _hashSize,
                    HashOrigin = _hashOrigin,
                    Marker = HashMarker
                }.Schedule(dependsOn);

                JobHandle steer = new HordeSteerJob
                {
                    Position = _position,
                    State = _state,
                    SpeedScale = _speedScale,
                    CellStart = _cellStart,
                    Sorted = _sorted,
                    NextPosition = _nextPosition,
                    Velocity = _velocity,
                    Heading = _heading,
                    AnimTime = _animTime,
                    Grid = grid,
                    HashSize = _hashSize,
                    HashOrigin = _hashOrigin,
                    Settings = settings,
                    Tank = tank,
                    DeltaTime = dt,
                    Marker = SteerMarker
                }.ScheduleBatch(Capacity, BATCH, hash);

                JobHandle contact = new HordeContactJob
                {
                    Position = _nextPosition,
                    State = _state,
                    Health = _health,
                    AttackDamage = _attackDamage,
                    Heading = _heading,
                    Areas = _areasRunning.AsArray(),
                    Deaths = _deathQueue.AsParallelWriter(),
                    Settings = settings,
                    Tank = tank,
                    DeltaTime = dt,
                    Marker = ContactMarker
                }.ScheduleBatch(Capacity, BATCH, steer);

                JobHandle totals = new HordeTotalsJob
                {
                    AttackDamage = _attackDamage,
                    Position = _nextPosition,
                    State = _state,
                    Totals = _totals,
                    Tank = tank,
                    NearRadius = settings.NearRadius
                }.Schedule(contact);

                JobHandle render = new HordeRenderJob
                {
                    Position = _nextPosition,
                    State = _state,
                    Heading = _heading,
                    AnimTime = _animTime,
                    Velocity = _velocity,
                    Planes = _planes,
                    Instances = _visible.AsParallelWriter(),
                    Run = _runClip,
                    Attack = _attackClip,
                    Idle = _idleClip,
                    Cull = _cull,
                    CullSphere = new float2(1f, 1.6f),
                    Marker = RenderMarker
                }.ScheduleBatch(Capacity, BATCH, contact);

                _handle = JobHandle.CombineDependencies(totals, render);
                JobHandle.ScheduleBatchedJobs();
                _running = true;
                return _handle;
            }
        }

        /// <summary>Waits for the running step (if any), takes its results and frees the dead. True if a step finished.</summary>
        public bool Complete()
        {
            if (!_running)
                return false;

            using (CompleteMarker.Auto())
            {
                _handle.Complete();
                _running = false;
                (_position, _nextPosition) = (_nextPosition, _position);
                _stepDamage = _totals[0];
                _near = (int)_totals[1];
                _areasRunning.Clear();

                while (_deathQueue.TryDequeue(out HordeDeathEvent death))
                {
                    _deaths.Add(death);
                    Release(death.Index);
                }

                return true;
            }
        }

        public void Dispose()
        {
            _handle.Complete();
            _running = false;
            if (_position.IsCreated) _position.Dispose();
            if (_nextPosition.IsCreated) _nextPosition.Dispose();
            if (_velocity.IsCreated) _velocity.Dispose();
            if (_heading.IsCreated) _heading.Dispose();
            if (_health.IsCreated) _health.Dispose();
            if (_state.IsCreated) _state.Dispose();
            if (_animTime.IsCreated) _animTime.Dispose();
            if (_speedScale.IsCreated) _speedScale.Dispose();
            if (_attackDamage.IsCreated) _attackDamage.Dispose();
            if (_free.IsCreated) _free.Dispose();
            if (_cellStart.IsCreated) _cellStart.Dispose();
            if (_cursor.IsCreated) _cursor.Dispose();
            if (_cellIndex.IsCreated) _cellIndex.Dispose();
            if (_sorted.IsCreated) _sorted.Dispose();
            if (_totals.IsCreated) _totals.Dispose();
            if (_planes.IsCreated) _planes.Dispose();
            if (_areas.IsCreated) _areas.Dispose();
            if (_areasRunning.IsCreated) _areasRunning.Dispose();
            if (_deathQueue.IsCreated) _deathQueue.Dispose();
            if (_deaths.IsCreated) _deaths.Dispose();
            if (_visible.IsCreated) _visible.Dispose();
        }

        private void Release(int index)
        {
            _state[index] = HordeState.Free;
            _velocity[index] = float2.zero;
            _free[_freeCount++] = index;
            _alive--;
        }

        private void ThrowIfRunning()
        {
            if (_running)
                throw new InvalidOperationException("HordeSimulation: a step is running; Complete() it first.");
        }
    }
}

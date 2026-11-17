using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.Horde
{
    /// <summary>
    /// Height map of the walkable surface on an XZ grid of square cells, read by downward rays.
    /// <para>
    /// A cell keeps the height of whatever the ray hit first (ground, a crater floor, a roof) or
    /// <see cref="NO_GROUND"/> when it hit nothing. Two neighbour cells connect when their heights
    /// differ by no more than <see cref="HordeNavGridSettings.MaxStep"/>, so a house is a tall block
    /// nobody climbs and a gentle crater rim is a slope. A diagonal move also needs both cells it
    /// squeezes past to connect (no corner cutting).
    /// </para>
    /// <para>
    /// Heights belong to the main thread: rays run on workers into a staging buffer, and
    /// <see cref="CompleteUpdate"/> copies them in. Flow fields and paths read their own snapshot,
    /// so the grid can be queried at any time. No managed allocations after construction.
    /// </para>
    /// </summary>
    public sealed class HordeNavGrid : IDisposable
    {
        /// <summary>Height of a cell whose ray hit nothing. Every comparison with it is false.</summary>
        public const float NO_GROUND = float.NaN;

        private const int INITIAL_RAY_CAPACITY = 1024;
        private const int RAYS_PER_JOB = 32;

        private static readonly ProfilerMarker NavGridMarker = new("Horde.NavGrid");

        private readonly HordeNavGridSettings _settings;
        private readonly PhysicsScene _scene;
        private NativeArray<float> _heights;
        private NativeList<int4> _dirty;
        private NativeList<int4> _pending;
        private NativeArray<int> _rayCells;
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;
        private NativeArray<float> _results;
        private NativeArray<int> _changed;
        private JobHandle _handle;
        private int _pendingCount;
        private bool _running;
        private int _version;

        public HordeNavGrid(in HordeNavGridSettings settings) : this(settings, Physics.defaultPhysicsScene) { }

        public HordeNavGrid(in HordeNavGridSettings settings, PhysicsScene scene)
        {
            if (settings.Size.x <= 0 || settings.Size.y <= 0 || settings.CellSize <= 0f)
                throw new ArgumentException("HordeNavGrid needs a positive size and cell size.");

            _settings = settings;
            _scene = scene;
            int cells = settings.Size.x * settings.Size.y;
            _heights = new NativeArray<float>(cells, Allocator.Persistent);
            for (int i = 0; i < cells; i++)
                _heights[i] = NO_GROUND;

            _dirty = new NativeList<int4>(16, Allocator.Persistent);
            _pending = new NativeList<int4>(16, Allocator.Persistent);
            _changed = new NativeArray<int>(1, Allocator.Persistent);
            AllocateRays(math.min(cells, INITIAL_RAY_CAPACITY));
        }

        public HordeNavGridSettings Settings => _settings;
        public int2 Size => _settings.Size;
        public float CellSize => _settings.CellSize;
        public float MaxStep => _settings.MaxStep;
        public int CellCount => _heights.Length;

        /// <summary>Bumps whenever any height changes.</summary>
        public int Version => _version;

        /// <summary>Rays are on the workers; <see cref="CompleteUpdate"/> will bring them in.</summary>
        public bool IsUpdating => _running;

        /// <summary>Dirty cell rects waiting for the next <see cref="ScheduleUpdate"/>.</summary>
        public int DirtyRectCount => _dirty.Length;

        /// <summary>Raw heights, for snapshots. Main thread only; never written by a job.</summary>
        internal NativeArray<float> Heights => _heights;

        // ------------------------------------------------------------------ building

        /// <summary>Reads every cell at once and waits for it. For load time.</summary>
        public void BuildAll()
        {
            using (NavGridMarker.Auto())
            {
                CompleteUpdate();
                _dirty.Clear();
                MarkDirtyCells(int2.zero, _settings.Size - 1);
            }

            ScheduleUpdate().Complete();
            CompleteUpdate();
            _version++;
        }

        /// <summary>
        /// Queues the cells under <paramref name="bounds"/> (padded by one cell) for a new read.
        /// Overlapping or touching rects merge into one.
        /// </summary>
        public void MarkDirty(Bounds bounds)
        {
            float2 cellSize = _settings.CellSize;
            float2 min = ((float3)bounds.min).xz;
            float2 max = ((float3)bounds.max).xz;
            int2 cellMin = (int2)math.floor((min - _settings.Origin) / cellSize) - 1;
            int2 cellMax = (int2)math.floor((max - _settings.Origin) / cellSize) + 1;
            MarkDirtyCells(cellMin, cellMax);
        }

        /// <summary>Queues the cell rect <paramref name="min"/>..<paramref name="max"/> (inclusive).</summary>
        public void MarkDirtyCells(int2 min, int2 max)
        {
            min = math.max(min, 0);
            max = math.min(max, _settings.Size - 1);
            if (math.any(max < min))
                return;

            var rect = new int4(min, max);
            for (int i = 0; i < _dirty.Length; i++)
            {
                int4 other = _dirty[i];
                bool touch = rect.x <= other.z + 1 && other.x <= rect.z + 1 && rect.y <= other.w + 1 && other.y <= rect.w + 1;
                if (!touch)
                    continue;

                rect = new int4(math.min(rect.xy, other.xy), math.max(rect.zw, other.zw));
                _dirty.RemoveAtSwapBack(i);
                i = -1;
            }

            _dirty.Add(rect);
        }

        /// <summary>Dirty rect <paramref name="index"/> as (minX, minZ, maxX, maxZ) cells, inclusive.</summary>
        public int4 GetDirtyRect(int index) => _dirty[index];

        /// <summary>
        /// Schedules rays over every dirty cell. Call <see cref="CompleteUpdate"/> at the start of
        /// the next frame. Does nothing (returns the running handle) while an update is in flight.
        /// </summary>
        public JobHandle ScheduleUpdate(JobHandle dependsOn = default)
        {
            if (_running)
                return _handle;
            if (_dirty.Length == 0)
                return dependsOn;

            using (NavGridMarker.Auto())
            {
                int count = 0;
                for (int i = 0; i < _dirty.Length; i++)
                {
                    int4 r = _dirty[i];
                    count += (r.z - r.x + 1) * (r.w - r.y + 1);
                }

                if (count > _commands.Length)
                {
                    DisposeRays();
                    AllocateRays(math.min(_heights.Length, math.max(count, _commands.Length * 2)));
                }

                _pending.Clear();
                _pending.AddRange(_dirty.AsArray());
                _dirty.Clear();
                _pendingCount = count;

                JobHandle handle = new HordeBuildRaysJob
                {
                    Rects = _pending.AsArray(),
                    Cells = _rayCells,
                    Commands = _commands,
                    Scene = _scene,
                    Query = new QueryParameters(_settings.LayerMask, false, QueryTriggerInteraction.Ignore, false),
                    Origin = _settings.Origin,
                    Width = _settings.Size.x,
                    CellSize = _settings.CellSize,
                    RayTop = _settings.RayTop,
                    RayLength = _settings.RayLength
                }.Schedule(dependsOn);

                handle = RaycastCommand.ScheduleBatch(_commands.GetSubArray(0, count), _hits.GetSubArray(0, count),
                    RAYS_PER_JOB, 1, handle);

                handle = new HordeReadHitsJob
                {
                    Hits = _hits,
                    Results = _results
                }.Schedule(count, 128, handle);

                JobHandle.ScheduleBatchedJobs();
                _handle = handle;
                _running = true;
                return handle;
            }
        }

        /// <summary>Waits for the rays (if any) and writes their heights. True if a height changed.</summary>
        public bool CompleteUpdate()
        {
            if (!_running)
                return false;

            using (NavGridMarker.Auto())
            {
                _handle.Complete();
                _running = false;
                _changed[0] = 0;
                new HordeApplyHeightsJob
                {
                    Cells = _rayCells,
                    Results = _results,
                    Heights = _heights,
                    Changed = _changed,
                    Count = _pendingCount
                }.Run();

                if (_changed[0] == 0)
                    return false;

                _version++;
                return true;
            }
        }

        /// <summary>Overwrites every height without physics (tests, tools). Use <see cref="NO_GROUND"/> for holes.</summary>
        public void SetHeights(NativeArray<float> heights)
        {
            CompleteUpdate();
            _heights.CopyFrom(heights);
            _version++;
        }

        /// <summary>Overwrites one height without physics (tests, tools).</summary>
        public void SetHeight(int2 cell, float height)
        {
            CompleteUpdate();
            _heights[Index(cell)] = height;
            _version++;
        }

        // ------------------------------------------------------------------ queries

        public bool InBounds(int2 cell) => math.all(cell >= 0) && math.all(cell < _settings.Size);

        public int Index(int2 cell) => cell.y * _settings.Size.x + cell.x;

        public bool TryCell(float3 position, out int2 cell)
        {
            cell = (int2)math.floor((position.xz - _settings.Origin) / _settings.CellSize);
            return InBounds(cell);
        }

        /// <summary>Clamps <paramref name="position"/> to the nearest cell of the grid.</summary>
        public int2 ClampCell(float3 position)
        {
            int2 cell = (int2)math.floor((position.xz - _settings.Origin) / _settings.CellSize);
            return math.clamp(cell, 0, _settings.Size - 1);
        }

        public float HeightOf(int2 cell) => _heights[Index(cell)];

        public bool HasGround(int2 cell) => InBounds(cell) && !float.IsNaN(_heights[Index(cell)]);

        /// <summary>Centre of a cell at its surface height (or 0 where there is no ground).</summary>
        public float3 CellCenter(int2 cell)
        {
            float2 xz = _settings.Origin + ((float2)cell + 0.5f) * _settings.CellSize;
            float y = InBounds(cell) ? _heights[Index(cell)] : 0f;
            return new float3(xz.x, float.IsNaN(y) ? 0f : y, xz.y);
        }

        /// <summary>
        /// Surface height under <paramref name="position"/>: bilinear over the cell centres around it,
        /// skipping neighbours a walker could not step to (a wall does not lift the floor next to it).
        /// <see cref="NO_GROUND"/> outside the grid or over a hole.
        /// </summary>
        public float HeightAt(float3 position)
        {
            if (!TryCell(position, out int2 own))
                return NO_GROUND;

            float h0 = _heights[Index(own)];
            if (float.IsNaN(h0))
                return NO_GROUND;

            float2 p = (position.xz - _settings.Origin) / _settings.CellSize - 0.5f;
            int2 c0 = (int2)math.floor(p);
            float2 f = p - c0;
            float h00 = Sample(c0, h0);
            float h10 = Sample(c0 + new int2(1, 0), h0);
            float h01 = Sample(c0 + new int2(0, 1), h0);
            float h11 = Sample(c0 + new int2(1, 1), h0);
            return math.lerp(math.lerp(h00, h10, f.x), math.lerp(h01, h11, f.x), f.y);
        }

        /// <summary>Whether a walker moves from <paramref name="a"/> to the neighbour <paramref name="b"/> (8-connected).</summary>
        public bool CanStep(int2 a, int2 b)
        {
            int2 d = b - a;
            if (!InBounds(a) || math.any(math.abs(d) > 1) || math.all(d == 0))
                return false;
            return HordeNavRules.CanMove(_heights, _settings.Size, _settings.MaxStep, a, d);
        }

        public void Dispose()
        {
            _handle.Complete();
            _running = false;
            if (_heights.IsCreated) _heights.Dispose();
            if (_dirty.IsCreated) _dirty.Dispose();
            if (_pending.IsCreated) _pending.Dispose();
            if (_changed.IsCreated) _changed.Dispose();
            DisposeRays();
        }

        private float Sample(int2 cell, float h0)
        {
            if (!InBounds(cell))
                return h0;
            float h = _heights[Index(cell)];
            return HordeNavRules.Steppable(h, h0, _settings.MaxStep) ? h : h0;
        }

        private void AllocateRays(int capacity)
        {
            _rayCells = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _commands = new NativeArray<RaycastCommand>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _hits = new NativeArray<RaycastHit>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _results = new NativeArray<float>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private void DisposeRays()
        {
            if (_rayCells.IsCreated) _rayCells.Dispose();
            if (_commands.IsCreated) _commands.Dispose();
            if (_hits.IsCreated) _hits.Dispose();
            if (_results.IsCreated) _results.Dispose();
        }
    }
}

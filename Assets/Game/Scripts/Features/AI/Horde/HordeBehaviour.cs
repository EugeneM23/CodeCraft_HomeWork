using Atomic.Elements;
using Atomic.Entities;
using Game.Horde;
using Game.Voxels;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// The horde as one Atomic entity: owns the flow field to the tank, the crowd simulation, its
    /// renderer and the corpses; walks the level's shared navigation grid (<see cref="LevelNav"/>,
    /// owned by the level AI entity, which keeps it up to date with the digging). Frame:
    /// <see cref="CompleteFrame"/> (start of the frame, <see cref="LevelAIEarlyUpdate"/>, before the
    /// grid takes in its new heights) takes the step of the last frame; <see cref="Tick"/> reads the
    /// tank, hands out the deaths (corpses, budgeted blood, kills, sound), damages the tank, keeps the
    /// population, draws, and schedules the field and the next step.
    /// Listens to <see cref="ExplosionEvents.Exploded"/> (area damage). No managed allocations per frame.
    /// </summary>
    public sealed class HordeBehaviour : IEntityInit, IEntityDispose, IEntityTick
    {
        private const float FLOW_INTERVAL = 0.2f;
        private const float AUDIO_INTERVAL = 0.25f;
        private const int SPAWN_TRIES = 8;
        /// <summary>Spawn cells higher than this over the lowest ground are roofs and rubble, not ground.</summary>
        private const float SPAWN_MAX_HEIGHT = 1f;

        private static readonly ProfilerMarker TickMarker = new("Horde.Tick");
        private static readonly ProfilerMarker DeathsMarker = new("Horde.Deaths");

        private readonly HordeArtConfig _art;
        private readonly HordeOptions _options;
        private readonly Plane[] _planes = new Plane[6];

        private HordeNavGrid _grid;
        private HordeFlowField _flow;
        private HordeSimulation _simulation;
        private HordeRenderer _renderer;
        private HordeCorpses _corpses;

        private IEntity _entity;
        private IReactiveVariable<int> _alive;
        private IReactiveVariable<int> _kills;
        private IReactiveVariable<int> _population;
        private IEvent<Vector3> _killed;
        private IHordeAudio _audio;
        private IReactiveVariable<int> _damage;
        private TakeDamageArgs _damageArgs;
        private Color32[] _blood;
        private float _bloodSize;

        private IEntity _tank;
        private Transform _tankTransform;
        private Rigidbody _tankBody;
        private Vector3 _hullCenter;
        private Vector3 _hullHalf;
        private Camera _camera;

        private Unity.Mathematics.Random _random;
        private Bounds _worldBounds;
        private float _damageOwed;
        private float _flowTimer;
        private int2 _flowCell = new(int.MinValue, int.MinValue);
        private float _audioTimer;
        private float _groundHeight;

        public HordeBehaviour(HordeArtConfig art, HordeOptions options)
        {
            _art = art;
            _options = options ?? new HordeOptions();
        }

        public HordeSimulation Simulation => _simulation;
        public HordeNavGrid Grid => _grid;
        public HordeFlowField FlowField => _flow;
        public HordeCorpses Corpses => _corpses;

        /// <summary>Crowd shadows (measured separately: off by default).</summary>
        public bool CastShadows
        {
            get => _renderer != null && _renderer.CastShadows;
            set
            {
                _options.castShadows = value;
                if (_renderer != null)
                    _renderer.CastShadows = value;
            }
        }

        /// <summary>Damage per second of one attacking monster (0: the tank is immune).</summary>
        public float AttackDps
        {
            get => _options.attackDps;
            set
            {
                _options.attackDps = value;
                if (_simulation != null)
                    _simulation.Settings = _options.ToSettings();
            }
        }

        public void Init(IEntity entity)
        {
            _entity = entity;
            _alive = entity.GetHordeAlive();
            _kills = entity.GetHordeKills();
            _population = entity.GetHordePopulation();
            _killed = entity.GetHordeMonsterKilled();
            _audio = entity.GetHordeAudio() ?? HordeAudioNull.Instance;
            _damage = entity.GetDamage();
            _damageArgs = new TakeDamageArgs(entity);
            var look = new BloodLook();
            _blood = look.ToVoxels();
            _bloodSize = look.dropSize;
            _random = new Unity.Mathematics.Random(0x5EED1234u);

            // The level's one grid: made here (from the horde's map) only if the level has no AI entity yet.
            var navOptions = new LevelNavOptions
            {
                mapMin = _options.mapMin,
                mapMax = _options.mapMax,
                rayTop = _options.rayTop,
                rayLength = _options.rayLength
            };
            _grid = LevelAI.Ensure(navOptions).GetLevelNavigation().Grid;
            _groundHeight = LowestGround();
            _flow = new HordeFlowField(_grid);

            _simulation = new HordeSimulation(_options.capacity, _grid, _flow, _options.ToSettings());
            _simulation.SetClips(_art.run, _art.attack, _art.idle);
            _renderer = new HordeRenderer(_art.mesh, _art.material, _options.capacity) { CastShadows = _options.castShadows };
            _corpses = new HordeCorpses(_art.corpseMesh, _art.material, _options.corpseCapacity, _options.corpseLie, _options.corpseSink);

            float2 min = _options.mapMin;
            float2 max = _options.mapMax;
            _worldBounds = new Bounds(new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f),
                new Vector3(max.x - min.x + 4f, 2f * _options.rayTop, max.y - min.y + 4f));

            ExplosionEvents.Exploded += OnExploded;
            LevelAIEarlyUpdate.Add(this);
        }

        public void Dispose(IEntity entity)
        {
            ExplosionEvents.Exploded -= OnExploded;
            LevelAIEarlyUpdate.Remove(this);
            _simulation?.Dispose();
            _flow?.Dispose();
            _renderer?.Dispose();
            _corpses?.Dispose();
            _simulation = null;
            _flow = null;
            _grid = null;
            _renderer = null;
            _corpses = null;
        }

        /// <summary>
        /// Start of the frame: the jobs of the last frame are taken in (step, flow field). The level's grid takes in its
        /// rays after this (<see cref="LevelAIEarlyUpdate"/>): the step reads the heights on a worker.
        /// </summary>
        public void CompleteFrame()
        {
            if (_simulation == null)
                return;
            _simulation.Complete();
            _flow.Complete();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_simulation == null)
                return;

            using (TickMarker.Auto())
            {
                CompleteFrame(); // no-op when the early hook already ran
                bool hasTank = ReadTank(out HordeTank tank);
                HandleDeaths();
                DamageTank();
                KeepPopulation(tank);

                if (_alive.Value != _simulation.AliveCount)
                    _alive.Value = _simulation.AliveCount;

                _audioTimer += deltaTime;
                if (_audioTimer >= AUDIO_INTERVAL)
                {
                    _audioTimer = 0f;
                    _audio.SetDensity(_simulation.AliveCount > 0 ? (float)_simulation.NearCount / _simulation.AliveCount : 0f);
                }

                float time = Time.time;
                _corpses.Update(time);
                _corpses.Draw(_worldBounds, time);
                NativeArray<HordeInstance> visible = _simulation.Visible;
                _renderer.Upload(visible, 0, 0, visible.Length);
                _renderer.Draw(visible.Length, _worldBounds);

                _flowTimer += deltaTime;
                if (hasTank && _flowTimer >= FLOW_INTERVAL)
                {
                    int2 cell = _grid.ClampCell(tank.Center);
                    if (!_flow.HasField || !math.all(cell == _flowCell) || _flow.GridVersion != _grid.Version)
                    {
                        _flow.Schedule(tank.Center);
                        _flowCell = cell;
                        _flowTimer = 0f;
                    }
                }

                if (_camera == null)
                    _camera = Camera.main;
                if (_camera != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(_camera, _planes);
                    _simulation.SetCullingPlanes(_planes);
                }
                else
                {
                    _simulation.SetCullingPlanes(null);
                }

                _simulation.Schedule(deltaTime, tank);
            }
        }

        /// <summary>
        /// Releases <paramref name="count"/> monsters on walkable ground at least
        /// <see cref="HordeOptions.spawnDistance"/> from the tank (as a wave: the audio hears it).
        /// Returns how many found room.
        /// </summary>
        public int SpawnWave(int count)
        {
            if (_simulation == null || _simulation.IsRunning)
                return 0;
            ReadTank(out HordeTank tank);
            int spawned = SpawnFar(count, tank);
            if (spawned > 0)
                _audio.WaveStarted();
            return spawned;
        }

        /// <summary>Kills every live monster (deaths go through the usual corpses and events next tick).</summary>
        public void KillAll()
        {
            if (_simulation == null)
                return;
            CompleteFrame();
            for (int i = 0; i < _simulation.Capacity; i++)
                _simulation.Kill(i);
        }

        // ------------------------------------------------------------------ steps

        private bool ReadTank(out HordeTank tank)
        {
            tank = default;
            tank.Rotation = quaternion.identity;
            GameContext context = GameContext.Instance;
            IEntity current = context != null && context.TryGetPlayerContext(out PlayerContext player)
                && player.TryGetCharacter(out IReactiveVariable<IEntity> character)
                ? character.Value
                : null;
            if (!ReferenceEquals(current, _tank))
                BindTank(current);
            if (_tankTransform == null)
                return false;

            tank.Present = true;
            tank.Center = _tankTransform.TransformPoint(_hullCenter);
            tank.Rotation = _tankTransform.rotation;
            tank.HalfExtents = _hullHalf;
            tank.Velocity = _tankBody != null ? _tankBody.linearVelocity : Vector3.zero;
            return true;
        }

        /// <summary>The player's vehicle changed: find its transform, body and hull box (an event, may allocate).</summary>
        private void BindTank(IEntity tank)
        {
            _tank = tank;
            _tankTransform = null;
            _tankBody = null;
            if (tank == null)
                return;

            _tankTransform = tank is Component component ? component.transform : tank.TryGetTransform(out Transform t) ? t : null;
            if (_tankTransform == null)
                return;
            _tankBody = _tankTransform.GetComponentInParent<Rigidbody>();

            // Hull box: the hull's renderers (turret and gun left out), else every solid collider.
            var local = new Bounds();
            bool any = false;
            if (tank.TryGetHullVisual(out Transform hull) && hull != null)
            {
                foreach (Renderer r in hull.GetComponentsInChildren<Renderer>())
                    any |= Encapsulate(ref local, any, r.transform, r.localBounds);
            }

            if (!any)
            {
                foreach (Collider c in _tankTransform.GetComponentsInChildren<Collider>())
                {
                    if (c.isTrigger || !c.enabled)
                        continue;
                    if (c is BoxCollider box)
                        any |= Encapsulate(ref local, any, box.transform, new Bounds(box.center, box.size));
                    else if (c is MeshCollider mc && mc.sharedMesh != null)
                        any |= Encapsulate(ref local, any, mc.transform, mc.sharedMesh.bounds);
                }
            }

            if (!any)
                local = new Bounds(Vector3.up, new Vector3(3.5f, 2f, 6.5f));
            _hullCenter = local.center;
            _hullHalf = local.extents;
        }

        private bool Encapsulate(ref Bounds local, bool any, Transform from, Bounds bounds)
        {
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            for (int k = 0; k < 8; k++)
            {
                var corner = new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                Vector3 p = _tankTransform.InverseTransformPoint(from.TransformPoint(c + corner));
                if (!any && k == 0)
                    local = new Bounds(p, Vector3.zero);
                else
                    local.Encapsulate(p);
            }

            return true;
        }

        private void HandleDeaths()
        {
            NativeArray<HordeDeathEvent> deaths = _simulation.Deaths;
            if (deaths.Length == 0)
                return;

            using (DeathsMarker.Auto())
            {
                float time = Time.time;
                int sprays = 0;
                for (int i = 0; i < deaths.Length; i++)
                {
                    HordeDeathEvent death = deaths[i];
                    _corpses.Add(death.Position, death.Heading, time);
                    Vector3 at = (Vector3)death.Position + Vector3.up * 0.6f;
                    if (sprays < _options.bloodPerFrame)
                    {
                        VoxelSpray.Burst(at, Vector3.up, _options.bloodDrops, 2f, 5f, 0.6f, _blood, _bloodSize);
                        sprays++;
                    }

                    _killed.Invoke(at);
                }

                _kills.Value += deaths.Length;
                _simulation.ClearDeaths();
            }
        }

        /// <summary>The attack damage of the step goes into the tank's health by whole points, one hit a frame at most.</summary>
        private void DamageTank()
        {
            _damageOwed += _simulation.StepDamage;
            if (_damageOwed < 1f || _tank == null)
            {
                if (_tank == null)
                    _damageOwed = 0f;
                return;
            }

            int whole = (int)_damageOwed;
            _damageOwed -= whole;
            _damage.Value = whole;
            DamageUseCase.TakeDamage(_tank, _damageArgs);
        }

        private void KeepPopulation(in HordeTank tank)
        {
            int target = math.min(_population.Value, _simulation.Capacity);
            int missing = target - _simulation.AliveCount;
            if (missing > 0 && tank.Present)
                SpawnFar(math.min(missing, _options.spawnsPerFrame), tank);
        }

        private int SpawnFar(int count, in HordeTank tank)
        {
            if (!_flow.HasField && tank.Present)
                return 0; // first field still running: no idea yet where the reachable ground is
            float minSq = _options.spawnDistance * _options.spawnDistance;
            int2 size = _grid.Size;
            int spawned = 0;
            for (int n = 0; n < count; n++)
            {
                for (int attempt = 0; attempt < SPAWN_TRIES; attempt++)
                {
                    var cell = new int2(_random.NextInt(0, size.x), _random.NextInt(0, size.y));
                    if (!_grid.HasGround(cell) || _grid.HeightOf(cell) > _groundHeight + SPAWN_MAX_HEIGHT)
                        continue;
                    if (_flow.HasField && _flow.IsUnreachable(cell))
                        continue;
                    float3 p = _grid.CellCenter(cell);
                    p.xz += _random.NextFloat2(-0.2f, 0.2f) * _grid.CellSize;
                    if (tank.Present && math.distancesq(p.xz, tank.Center.xz) < minSq)
                        continue;
                    float2 toTank = tank.Present ? tank.Center.xz - p.xz : new float2(0f, 1f);
                    if (_simulation.Spawn(p, math.atan2(toTank.x, toTank.y)) < 0)
                        return spawned;
                    spawned++;
                    break;
                }
            }

            return spawned;
        }

        private float LowestGround()
        {
            float lowest = float.MaxValue;
            int2 size = _grid.Size;
            for (int z = 0; z < size.y; z += 4)
            for (int x = 0; x < size.x; x += 4)
            {
                var cell = new int2(x, z);
                if (_grid.HasGround(cell))
                    lowest = math.min(lowest, _grid.HeightOf(cell));
            }

            return lowest == float.MaxValue ? 0f : lowest;
        }

        private void OnExploded(Vector3 point, float radius) =>
            _simulation?.AddAreaDamage(point, radius, _options.explosionDamage);
    }
}

using Atomic.Elements;
using Atomic.Entities;
using Game.Horde;
using Game.Voxels;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The horde as one Atomic entity: owns the flow field to the tank, the crowd simulation with its kinds (Crawler,
    /// Spitter: <see cref="HordeKinds"/>), one renderer and one corpse ring per kind, and the acid in flight; walks the
    /// level's shared navigation grid (<see cref="LevelNav"/>, owned by the level AI entity, which keeps it up to date
    /// with the digging). Frame: <see cref="CompleteFrame"/> (start of the frame, <see cref="LevelAIEarlyUpdate"/>, before
    /// the grid takes in its new heights) takes the step of the last frame; <see cref="Tick"/> reads the tank, hands out
    /// the deaths (corpses, budgeted blood, kills, <see cref="IEnemyDeaths"/>, sound), the shots (sound, splashes),
    /// damages the tank, spawns what was asked, draws, and schedules the field and the next step.
    /// <para>
    /// For the arena director it is the level's <see cref="IArenaHorde"/> (<see cref="HordeAPI.LevelHorde"/> on the level
    /// AI entity); for combat an <see cref="IEnemyTargetSource"/> (<see cref="Targets"/>, registered in the level's
    /// <see cref="IEnemyTargets"/> once there is one). Blasts (<see cref="ExplosionEvents.Exploded"/>) only shove: blast
    /// damage comes from the weapons through the target source. No managed allocations per frame.
    /// </para>
    /// </summary>
    public sealed class HordeBehaviour : IEntityInit, IEntityDispose, IEntityTick, IArenaHorde
    {
        private const float FLOW_INTERVAL = 0.2f;
        private const float AUDIO_INTERVAL = 0.25f;
        private const int SPAWN_TRIES = 8;
        private const int MAX_PENDING = 256;
        private const float ELITE_REPORT_INTERVAL = 0.1f;
        /// <summary>Spawn cells higher than this over the lowest ground are roofs and rubble, not ground.</summary>
        private const float SPAWN_MAX_HEIGHT = 1f;

        private static readonly ProfilerMarker TickMarker = new("Horde.Tick");
        private static readonly ProfilerMarker DeathsMarker = new("Horde.Deaths");

        private readonly HordeArtConfig _art;
        private readonly HordeOptions _options;
        private readonly Plane[] _planes = new Plane[6];
        private readonly HordeRenderer[] _renderers = new HordeRenderer[HordeKinds.COUNT];
        private readonly HordeCorpses[] _corpses = new HordeCorpses[HordeKinds.COUNT];
        private readonly int[] _drawnCounts = new int[HordeKinds.COUNT];
        private readonly Pending[] _pending = new Pending[MAX_PENDING];
        private readonly HordeTargetSource _targets = new();

        private LevelNav _nav;
        private int _gridGeneration;
        private HordeNavGrid _grid;
        private HordeFlowField _flow;
        private HordeSimulation _simulation;
        private HordeRenderer _acid;

        private IEntity _entity;
        private IEntity _level;
        private IReactiveVariable<int> _alive;
        private IReactiveVariable<int> _kills;
        private IReactiveVariable<int> _population;
        private IEvent<Vector3> _killed;
        private IHordeAudio _audio;
        private IReactiveVariable<int> _damage;
        private TakeDamageArgs _damageArgs;
        private Color32[] _blood;
        private float _bloodSize;
        private Color32[] _acidDrops;
        private IEnemyTargets _registeredTargets;
        private int _targetsId = -1;

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
        private float _biteTimer;
        private float _acidHitTimer;
        private float _aliveTimer;
        private int _aliveHeard = -1;
        private int _acidDrawn;
        private float _groundHeight;
        private int _queuedWave;
        private int _pendingCount;
        private int _eliteSlot = -1;
        private int _eliteVersion;
        private float _eliteShare;
        private float _eliteTimer;

        private struct Pending
        {
            public Vector3 Position;
            public int Row;
            public float HealthScale;
            public float DamageScale;
        }

        public HordeBehaviour(HordeArtConfig art, HordeOptions options)
        {
            _art = art;
            _options = options ?? new HordeOptions();
        }

        public HordeSimulation Simulation => _simulation;
        public HordeNavGrid Grid => _grid;
        public HordeFlowField FlowField => _flow;

        /// <summary>Corpses of the kind row <paramref name="row"/>.</summary>
        public HordeCorpses CorpsesOf(int row) => _corpses[row];

        /// <summary>The horde for combat's targeting (valid before Init too: an empty horde).</summary>
        public HordeTargetSource Targets => _targets;

        /// <summary>Crowd shadows (measured separately: off by default).</summary>
        public bool CastShadows
        {
            get => _renderers[0] != null && _renderers[0].CastShadows;
            set
            {
                _options.castShadows = value;
                for (int k = 0; k < _renderers.Length; k++)
                    if (_renderers[k] != null)
                        _renderers[k].CastShadows = value;
            }
        }

        /// <summary>Scale of the monsters' damage to the tank (0: immune).</summary>
        public float TankDamageScale
        {
            get => _options.tankDamageScale;
            set => _options.tankDamageScale = value;
        }

        public int AliveCount => (_simulation?.AliveCount ?? 0) + _pendingCount;

        public float EmergeSeconds => _options.emergeTime;

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
            _acidDrops = new Color32[] { _art.acidColor, Color.Lerp(_art.acidColor, Color.black, 0.4f) };
            _random = new Unity.Mathematics.Random(0x5EED1234u);

            // The level's one grid: made here (from the horde's map) only if the level has no AI entity yet.
            var navOptions = new LevelNavOptions
            {
                mapMin = _options.mapMin,
                mapMax = _options.mapMax,
                rayTop = _options.rayTop,
                rayLength = _options.rayLength
            };
            _level = LevelAI.Ensure(navOptions);
            _nav = _level.GetLevelNavigation();
            BindGrid();

            for (int row = 0; row < HordeKinds.COUNT; row++)
            {
                float scale = _options.Kind(row).RenderScale;
                _renderers[row] = new HordeRenderer(_art.mesh, _art.material, _options.capacity) { CastShadows = _options.castShadows };
                _renderers[row].SetLook(scale, _art.TintOf(row), _art.EmissionOf(row));
                _corpses[row] = new HordeCorpses(_art.corpseMesh, _art.material, _options.corpseCapacity, _options.corpseLie,
                    _options.corpseSink, 1.2f * scale);
                _corpses[row].SetLook(scale, _art.TintOf(row), _art.EmissionOf(row));
            }

            // Acid: the corpse path of the same shader (a static mesh at a position), a sphere tinted green.
            Mesh acidMesh = _art.acidMesh != null ? _art.acidMesh : Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            _acid = new HordeRenderer(acidMesh, _art.material, HordeSimulation.MAX_PROJECTILES, true);
            _acid.SetLook(_art.acidSize, _art.acidColor, _art.acidEmission);

            _level.SetLevelHorde(this);
            ExplosionEvents.Exploded += OnExploded;
            LevelAIEarlyUpdate.Add(this);
        }

        public void Dispose(IEntity entity)
        {
            ExplosionEvents.Exploded -= OnExploded;
            LevelAIEarlyUpdate.Remove(this);
            if (_registeredTargets != null)
                _registeredTargets.Unregister(_targetsId);
            _registeredTargets = null;
            _targetsId = -1;
            if (_level != null && _level.TryGetLevelHorde(out IArenaHorde current) && ReferenceEquals(current, this))
                _level.DelLevelHorde();
            _level = null;
            _targets.Simulation = null;
            _simulation?.Dispose();
            _flow?.Dispose();
            for (int k = 0; k < _renderers.Length; k++)
            {
                _renderers[k]?.Dispose();
                _corpses[k]?.Dispose();
                _renderers[k] = null;
                _corpses[k] = null;
            }

            _acid?.Dispose();
            _acid = null;
            _simulation = null;
            _flow = null;
            _grid = null;
        }

        /// <summary>
        /// The game is paused: the loop does not tick the horde, so nothing would draw it. Draws the crowd as it stood
        /// (the instance buffers keep the last upload), the corpses and the acid, without moving anything.
        /// </summary>
        public void DrawPaused()
        {
            if (_simulation == null)
                return;
            float time = GameTicks.Time;
            for (int k = 0; k < HordeKinds.COUNT; k++)
            {
                _corpses[k].Draw(_worldBounds, time);
                _renderers[k].Draw(_drawnCounts[k], _worldBounds);
            }

            _acid.Draw(_acidDrawn, _worldBounds);
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
                if (_nav.Generation != _gridGeneration || !ReferenceEquals(_nav.Grid, _grid))
                    ResetForArena(); // the level grid was rebuilt for a new arena
                RegisterTargets();
                bool hasTank = ReadTank(out HordeTank tank);
                HandleDeaths();
                ReportElite(deltaTime);
                HandleShots(deltaTime);
                DamageTank();
                FlushPending(tank);
                if (_queuedWave > 0)
                {
                    int wave = _queuedWave;
                    _queuedWave = 0;
                    SpawnWave(wave);
                }

                KeepPopulation(tank);

                int alive = _simulation.AliveCount;
                if (_alive.Value != alive)
                    _alive.Value = alive;

                _audioTimer += deltaTime;
                if (_audioTimer >= AUDIO_INTERVAL)
                {
                    _audioTimer = 0f;
                    _audio.SetDensity(alive > 0 ? (float)_simulation.NearCount / alive : 0f);
                }

                _aliveTimer += deltaTime;
                if (alive != _aliveHeard && _aliveTimer >= AUDIO_INTERVAL)
                {
                    _aliveTimer = 0f;
                    _aliveHeard = alive;
                    _audio.SetAlive(alive);
                }

                _biteTimer += deltaTime;
                if (_simulation.BiterCount > 0 && _biteTimer >= AUDIO_INTERVAL)
                {
                    _biteTimer = 0f;
                    _audio.CrawlerBite(_simulation.BiterPosition);
                }

                Draw();

                _flowTimer += deltaTime;
                if (hasTank && _flowTimer >= FLOW_INTERVAL)
                {
                    int2 cell = _grid.ClampCell(tank.Center);
                    if (!_flow.HasField || !math.all(cell == _flowCell) || _flow.GridVersion != _grid.Version)
                    {
                        // Seeded from the ground under the hull: the centre cell alone may be a hole or a rubble top.
                        _flow.Schedule(tank.Center, math.cmin(tank.HalfExtents.xz));
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

        // ------------------------------------------------------------------ arena

        public bool Spawn(EnemyKind kind, Vector3 at, float healthScale, float damageScale)
        {
            if (_simulation == null || !HordeKinds.TryGetRow(kind, out int row) || _pendingCount >= MAX_PENDING)
                return false;
            if (AliveCount >= _simulation.Capacity)
                return false;
            _pending[_pendingCount++] = new Pending { Position = at, Row = row, HealthScale = healthScale, DamageScale = damageScale };
            if (!_simulation.IsRunning)
            {
                ReadTank(out HordeTank tank);
                FlushPending(tank);
            }

            return true;
        }

        void IArenaHorde.WaveStarted() => _audio.WaveStarted();

        /// <summary>A burrow opened at <paramref name="at"/> (its sound).</summary>
        public void BurrowOpened(Vector3 at) => _audio.BurrowOpened(at);

        public void ResetForArena()
        {
            if (_simulation == null)
                return;
            CompleteFrame();
            if (_nav != null && (!ReferenceEquals(_nav.Grid, _grid) || _nav.Generation != _gridGeneration))
                BindGrid(); // a new grid (a new arena elsewhere): a fresh flow field and simulation
            else
                _simulation.Clear();
            for (int k = 0; k < HordeKinds.COUNT; k++)
            {
                _corpses[k].Clear();
                _drawnCounts[k] = 0;
            }

            _acidDrawn = 0;
            if (_eliteSlot >= 0)
            {
                _eliteSlot = -1;
                EliteStatus.ReportDied(); // gone with the old arena: the HUD drops its bar
            }
            _pendingCount = 0;
            _queuedWave = 0;
            _damageOwed = 0f;
            _flowCell = new int2(int.MinValue, int.MinValue);
            _groundHeight = LowestGround();
            if (_alive.Value != 0)
                _alive.Value = 0;
        }

        /// <summary>
        /// Releases <paramref name="count"/> crawlers on walkable ground at least
        /// <see cref="HordeOptions.spawnDistance"/> from the tank (as a wave: the audio hears it).
        /// Returns how many found room. While the step runs on the workers the wave is queued for the next tick (and 0
        /// is returned).
        /// </summary>
        public int SpawnWave(int count)
        {
            if (_simulation == null || count <= 0)
                return 0;
            if (_simulation.IsRunning)
            {
                _queuedWave += count;
                return 0;
            }

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

        /// <summary>
        /// (Re)makes what walks the level's grid: the flow field and the simulation (its hash covers the grid), and the
        /// draw bounds. At Init, and when the grid was replaced for a new arena (behind the curtain; it allocates).
        /// </summary>
        private void BindGrid()
        {
            _simulation?.Dispose();
            _flow?.Dispose();
            _grid = _nav.Grid;
            _gridGeneration = _nav.Generation;
            _groundHeight = LowestGround();
            _flow = new HordeFlowField(_grid);
            _simulation = new HordeSimulation(_options.capacity, _grid, _flow, _options.ToSettings());
            for (int row = 0; row < HordeKinds.COUNT; row++)
                _simulation.SetKind(row, _options.Kind(row));
            _simulation.SetClips(_art.run, _art.attack, _art.idle);
            _targets.Simulation = _simulation;

            HordeNavGridSettings g = _grid.Settings;
            float2 min = g.Origin;
            float2 max = g.Origin + (float2)g.Size * g.CellSize;
            _worldBounds = new Bounds(new Vector3((min.x + max.x) * 0.5f, g.RayTop - g.RayLength * 0.5f, (min.y + max.y) * 0.5f),
                new Vector3(max.x - min.x + 4f, g.RayLength + 4f, max.y - min.y + 4f));
        }

        private void RegisterTargets()
        {
            if (_registeredTargets != null)
                return;
            if (GameContext.TryGetInstance(out GameContext context) && context.TryGetEnemyTargets(out IEnemyTargets targets) && targets != null)
            {
                _registeredTargets = targets;
                _targetsId = targets.Register(_targets);
            }
        }

        private void FlushPending(in HordeTank tank)
        {
            for (int n = 0; n < _pendingCount; n++)
            {
                Pending p = _pending[n];
                float3 at = p.Position;
                float h = _grid.HeightAt(at);
                if (!float.IsNaN(h))
                    at.y = h;
                float2 toTank = tank.Present ? tank.Center.xz - at.xz : new float2(0f, 1f);
                int slot = _simulation.Spawn(at, math.atan2(toTank.x, toTank.y), p.Row, p.HealthScale, p.DamageScale, true);
                if (slot >= 0 && HordeKinds.IsElite(p.Row))
                {
                    _eliteSlot = slot;
                    _eliteVersion = _simulation.Version(slot);
                    _eliteShare = 1f;
                    EliteStatus.ReportAppeared(1f);
                }
            }

            _pendingCount = 0;
        }

        private void Draw()
        {
            float time = GameTicks.Time;
            NativeArray<HordeInstance> visible = _simulation.Visible;
            for (int k = 0; k < HordeKinds.COUNT; k++)
            {
                _corpses[k].Update(time);
                _corpses[k].Draw(_worldBounds, time);
                _simulation.GetVisibleRange(k, out int start, out int count);
                _renderers[k].Upload(visible, start, 0, count);
                _drawnCounts[k] = count;
                _renderers[k].Draw(count, _worldBounds);
            }

            NativeArray<HordeInstance> shots = _simulation.ShotInstances;
            _acid.Upload(shots, 0, 0, shots.Length);
            _acidDrawn = shots.Length;
            _acid.Draw(_acidDrawn, _worldBounds);
        }

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

        /// <summary>The big one's health for the HUD (<see cref="EliteStatus"/>), at most 10 times a second, on a change.</summary>
        private void ReportElite(float deltaTime)
        {
            if (_eliteSlot < 0)
                return;
            _eliteTimer += deltaTime;
            if (_eliteTimer < ELITE_REPORT_INTERVAL || !_simulation.IsTargetAlive(_eliteSlot, _eliteVersion))
                return;
            _eliteTimer = 0f;
            float max = _simulation.GetMaxHealth(_eliteSlot);
            float share = max > 0f ? Mathf.Clamp01(_simulation.GetHealth(_eliteSlot) / max) : 0f;
            if (Mathf.Abs(share - _eliteShare) < 0.001f)
                return;
            _eliteShare = share;
            EliteStatus.ReportHealth(share);
        }

        private void HandleDeaths()
        {
            NativeArray<HordeDeathEvent> deaths = _simulation.Deaths;
            if (deaths.Length == 0)
                return;

            using (DeathsMarker.Auto())
            {
                GameContext.TryGetInstance(out GameContext context);
                IEnemyDeaths feed = null;
                if (context != null)
                    context.TryGetEnemyDeaths(out feed);
                float time = GameTicks.Time;
                int sprays = 0;
                for (int i = 0; i < deaths.Length; i++)
                {
                    HordeDeathEvent death = deaths[i];
                    int row = math.clamp(death.Kind, 0, HordeKinds.COUNT - 1);
                    _corpses[row].Add(death.Position, death.Heading, time);
                    Vector3 at = (Vector3)death.Position + Vector3.up * 0.6f;
                    if (sprays < _options.bloodPerFrame)
                    {
                        VoxelSpray.Burst(at, Vector3.up, _options.bloodDrops, 2f, 5f, 0.6f, _blood, _bloodSize);
                        sprays++;
                    }

                    _killed.Invoke(at);
                    feed?.Report(new EnemyDeath(HordeKinds.ToEnemyKind(row), death.Position,
                        death.Cause == HordeDeathCause.Crushed, HordeKinds.IsElite(row)));
                    if (death.Index == _eliteSlot && HordeKinds.IsElite(row))
                    {
                        _eliteSlot = -1;
                        EliteStatus.ReportDied();
                    }
                }

                _kills.Value += deaths.Length;
                _simulation.ClearDeaths();
            }
        }

        /// <summary>Shots fired and ended in the last step: spit and hit sounds (hits throttled), splashes on the ground (budgeted).</summary>
        private void HandleShots(float deltaTime)
        {
            _acidHitTimer += deltaTime;
            NativeArray<HordeShotEvent> events = _simulation.ShotEvents;
            int splashes = 0;
            for (int i = 0; i < events.Length; i++)
            {
                HordeShotEvent e = events[i];
                if (e.Type == HordeShotEventType.Fired)
                {
                    _audio.Spit(e.Position);
                    continue;
                }

                if (_acidHitTimer >= AUDIO_INTERVAL)
                {
                    _acidHitTimer = 0f;
                    _audio.AcidHit(e.Position);
                }

                if (e.Type == HordeShotEventType.HitGround && splashes < _options.acidSplashPerFrame)
                {
                    VoxelSpray.Burst((Vector3)e.Position + Vector3.up * 0.1f, Vector3.up, _options.acidSplashDrops, 1.5f, 3f, 0.8f,
                        _acidDrops, _bloodSize);
                    splashes++;
                }
            }
        }

        /// <summary>The bite and acid damage of the step goes into the tank's health by whole points, one hit a frame at most.</summary>
        private void DamageTank()
        {
            _damageOwed += _simulation.StepDamage * _options.tankDamageScale;
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
                    if (_simulation.Spawn(p, math.atan2(toTank.x, toTank.y), HordeKinds.CRAWLER) < 0)
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
            _simulation?.AddKnockback(point, radius, _options.explosionPush);
    }
}

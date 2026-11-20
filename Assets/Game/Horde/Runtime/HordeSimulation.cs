using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.Horde
{
    /// <summary>
    /// The crowd: a fixed number of monster slots in <see cref="NativeArray{T}"/>s (position, velocity, heading, health,
    /// state, kind, animation time, speed jitter, damage scale, emerge and shot timers) with a free list, a kind table
    /// (<see cref="HordeKind"/>, up to <see cref="MAX_KINDS"/> rows), the ranged monsters' shots in flight, and the frame
    /// step as a chain of Burst jobs: spatial hash, steering over the <see cref="HordeFlowField"/>, contacts with the
    /// tank, blasts and hits, shots, totals, and render prep (frustum cull, sorted by kind into <see cref="Visible"/>).
    /// <para>
    /// Frame contract: <see cref="Schedule"/> in the frame, <see cref="Complete"/> at the start of the next (it frees the
    /// dead into <see cref="Deaths"/>). Spawn, kill and clear only while no step runs. Damage (<see cref="Damage"/>,
    /// <see cref="AddAreaDamage"/>) may be queued at any time: it lands in the next step. The query side
    /// (<see cref="IsTargetAlive"/>, <see cref="GetTargetPosition"/>, <see cref="Version"/>...) reads a snapshot taken at
    /// <see cref="Complete"/>, valid while a step runs. The grid heights and the flow field's readable buffer are read by
    /// the step, so the owner completes the step before it updates the grid or swaps the flow field.
    /// </para>
    /// No managed allocations after construction.
    /// </summary>
    public sealed partial class HordeSimulation : IDisposable
    {
        /// <summary>A step never integrates more than this (a hitch must not teleport the crowd through walls).</summary>
        public const float MAX_DELTA_TIME = 0.05f;
        /// <summary>Spatial hash cell, metres: not below <see cref="HordeSettings.SeparationRadius"/>, so one ring of cells holds every neighbour (a bigger radius scans more rings).</summary>
        public const float HASH_CELL = 1.5f;
        /// <summary>Rows of the kind table.</summary>
        public const int MAX_KINDS = 4;
        /// <summary>Shots in flight at most (more are dropped).</summary>
        public const int MAX_PROJECTILES = 256;
        /// <summary>A shot hits the hull box padded by this, m.</summary>
        public const float SHOT_HIT_PAD = 0.15f;

        private const int BATCH = 128;
        /// <summary>Blasts with damage queued for one step at most (more are dropped and counted in <see cref="DroppedAreas"/>).</summary>
        private const int MAX_AREAS = 64;
        /// <summary>Shoves queued for one step at most: a budget of their own, so they never crowd out blast damage.</summary>
        private const int MAX_KNOCKBACKS = 64;
        private const int MAX_HITS = 512;
        /// <summary>Height of the cull sphere's centre over the feet and its radius at render scale 1 (the Regax model).</summary>
        private static readonly float2 CullSphere = new(1.3f, 1.8f);

        private static readonly ProfilerMarker SimulateMarker = new("Horde.Simulate");
        private static readonly ProfilerMarker CompleteMarker = new("Horde.Complete");
        private static readonly ProfilerMarker HashMarker = new("Horde.Job.Hash");
        private static readonly ProfilerMarker SteerMarker = new("Horde.Job.Steer");
        private static readonly ProfilerMarker SlotsMarker = new("Horde.Job.Slots");
        private static readonly ProfilerMarker ContactMarker = new("Horde.Job.Contact");
        private static readonly ProfilerMarker ShotsMarker = new("Horde.Job.Shots");
        private static readonly ProfilerMarker RenderMarker = new("Horde.Job.Render");

        private readonly HordeNavGrid _grid;
        private readonly HordeFlowField _flow;
        private readonly int2 _hashSize;
        private readonly float2 _hashOrigin;
        private readonly HordeKind[] _kindTable = new HordeKind[MAX_KINDS];
        private readonly bool[] _kindSet = new bool[MAX_KINDS];
        private readonly int[] _visibleStart = new int[MAX_KINDS];
        private readonly int[] _visibleCount = new int[MAX_KINDS];

        private NativeArray<float3> _position;
        private NativeArray<float3> _nextPosition;
        private NativeArray<float2> _velocity;
        private NativeArray<float> _heading;
        private NativeArray<float> _health;
        /// <summary>Per slot: 1 when a shell's direct hit marked it (<see cref="Tear"/>): its death tears it apart.</summary>
        private NativeArray<byte> _torn;

        /// <summary>Which way the shell that tore a slot's monster flew (zero: no way known).</summary>
        private NativeArray<float3> _tornDirection;

        /// <summary>How hard the shell that tore a slot's monster throws its pieces and goo (1: as without the HE skill).</summary>
        private NativeArray<float> _tornPower;
        private NativeArray<HordeState> _state;
        private NativeArray<float> _animTime;
        private NativeArray<float> _speedScale;
        private NativeArray<float> _attackDamage;
        private NativeArray<byte> _kind;
        private NativeArray<float> _damageScale;
        private NativeArray<float> _stateTime;
        private NativeArray<float> _cooldown;
        private NativeArray<float> _maxHealth;
        /// <summary>Per monster: its place on the hull ring (<see cref="HordeHullRing"/>), -1 none.</summary>
        private NativeArray<int> _slot;
        /// <summary>Per place of the ring: the monster on it, -1 free, -2 taken by a body outside the crowd.</summary>
        private NativeArray<int> _slotOwner;
        /// <summary>This step's copy of <see cref="HordeObstacles"/>.</summary>
        private NativeArray<float4> _obstacles;
        private int _ringSlots;
        private NativeArray<HordeKind> _kinds;
        private NativeArray<int> _free;
        private NativeArray<int> _cellStart;
        private NativeArray<int> _cursor;
        private NativeArray<int> _cellIndex;
        private NativeArray<int> _sorted;
        private NativeArray<float> _totals;
        private NativeArray<float4> _planes;
        private NativeList<HordeAreaDamage> _areas;
        private NativeList<HordeAreaDamage> _areasRunning;
        private NativeList<HordeHit> _hits;
        private NativeQueue<HordeDeathEvent> _deathQueue;
        private NativeList<HordeDeathEvent> _deaths;
        private NativeList<HordeInstance> _visibleAll;
        private NativeList<HordeInstance> _visible;
        private NativeArray<int> _kindStarts;
        private NativeArray<int> _kindCounts;
        private NativeQueue<HordeShot> _shotQueue;
        private NativeArray<HordeProjectile> _projectiles;
        private NativeArray<int> _projectileCount;
        private NativeList<HordeInstance> _shotInstances;
        private NativeList<HordeShotEvent> _shotEvents;

        // Snapshot for queries while a step runs (main thread only).
        private NativeArray<float3> _snapPosition;
        private NativeArray<bool> _snapLive;
        private NativeArray<int> _version;
        private int _highWater;
        private int _areaDamageCount;
        private int _knockbackCount;

        private Unity.Mathematics.Random _random;
        private JobHandle _handle;
        private bool _running;
        private bool _cull;
        private int _freeCount;
        private int _alive;
        private float _stepDamage;
        private float _stepShotDamage;
        private int _near;
        private int _biters;
        private float3 _biterPosition;
        private int _visibleTotal;
        private HordeClip _runClip;
        private HordeClip _attackClip;
        private HordeClip _idleClip;
        private HordeClip _crawlClip;
        private HordeClip _fallClip;

        /// <summary>Per slot: 1 — this monster crawls on the ground instead of running (<see cref="HordeSettings.CrawlShare"/>).</summary>
        private NativeArray<byte> _crawl;

        /// <param name="versionBase">
        /// First generation of every slot. A simulation that replaces another under the same target source starts above
        /// the old one's <see cref="MaxVersion"/>, so a handle from the old one never matches a monster of the new one.
        /// </param>
        public HordeSimulation(int capacity, HordeNavGrid grid, HordeFlowField flow, in HordeSettings settings, uint seed = 1,
            int versionBase = 0)
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
            _torn = new NativeArray<byte>(capacity, Allocator.Persistent);
            _crawl = new NativeArray<byte>(capacity, Allocator.Persistent);
            _tornDirection = new NativeArray<float3>(capacity, Allocator.Persistent);
            _tornPower = new NativeArray<float>(capacity, Allocator.Persistent);
            _state = new NativeArray<HordeState>(capacity, Allocator.Persistent);
            _animTime = new NativeArray<float>(capacity, Allocator.Persistent);
            _speedScale = new NativeArray<float>(capacity, Allocator.Persistent);
            _attackDamage = new NativeArray<float>(capacity, Allocator.Persistent);
            _kind = new NativeArray<byte>(capacity, Allocator.Persistent);
            _damageScale = new NativeArray<float>(capacity, Allocator.Persistent);
            _stateTime = new NativeArray<float>(capacity, Allocator.Persistent);
            _cooldown = new NativeArray<float>(capacity, Allocator.Persistent);
            _maxHealth = new NativeArray<float>(capacity, Allocator.Persistent);
            _slot = new NativeArray<int>(capacity, Allocator.Persistent);
            for (int i = 0; i < capacity; i++)
                _slot[i] = -1;
            _slotOwner = new NativeArray<int>(HordeHullRing.MAX_SLOTS, Allocator.Persistent);
            _obstacles = new NativeArray<float4>(HordeObstacles.CAPACITY, Allocator.Persistent);
            _kinds = new NativeArray<HordeKind>(MAX_KINDS, Allocator.Persistent);
            _free = new NativeArray<int>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _cellStart = new NativeArray<int>(hashCells + 1, Allocator.Persistent);
            _cursor = new NativeArray<int>(hashCells, Allocator.Persistent);
            _cellIndex = new NativeArray<int>(capacity, Allocator.Persistent);
            _sorted = new NativeArray<int>(capacity, Allocator.Persistent);
            _totals = new NativeArray<float>(8, Allocator.Persistent);
            _planes = new NativeArray<float4>(6, Allocator.Persistent);
            _areas = new NativeList<HordeAreaDamage>(8, Allocator.Persistent);
            _areasRunning = new NativeList<HordeAreaDamage>(8, Allocator.Persistent);
            _hits = new NativeList<HordeHit>(64, Allocator.Persistent);
            _deathQueue = new NativeQueue<HordeDeathEvent>(Allocator.Persistent);
            _deaths = new NativeList<HordeDeathEvent>(64, Allocator.Persistent);
            _visibleAll = new NativeList<HordeInstance>(capacity, Allocator.Persistent);
            _visible = new NativeList<HordeInstance>(capacity, Allocator.Persistent);
            _kindStarts = new NativeArray<int>(MAX_KINDS, Allocator.Persistent);
            _kindCounts = new NativeArray<int>(MAX_KINDS, Allocator.Persistent);
            _shotQueue = new NativeQueue<HordeShot>(Allocator.Persistent);
            _projectiles = new NativeArray<HordeProjectile>(MAX_PROJECTILES, Allocator.Persistent);
            _projectileCount = new NativeArray<int>(1, Allocator.Persistent);
            _shotInstances = new NativeList<HordeInstance>(MAX_PROJECTILES, Allocator.Persistent);
            _shotEvents = new NativeList<HordeShotEvent>(MAX_PROJECTILES * 2, Allocator.Persistent);
            _snapPosition = new NativeArray<float3>(capacity, Allocator.Persistent);
            _snapLive = new NativeArray<bool>(capacity, Allocator.Persistent);
            _version = new NativeArray<int>(capacity, Allocator.Persistent);
            for (int i = 0; i < capacity; i++)
                _version[i] = versionBase;

            // Slot 0 comes out first.
            for (int i = 0; i < capacity; i++)
                _free[i] = capacity - 1 - i;
            _freeCount = capacity;

            var still = new HordeClip { start = 0, count = 1, frameRate = 30f, loop = true };
            _runClip = _attackClip = _idleClip = _crawlClip = _fallClip = still;
        }

        public HordeSettings Settings { get; set; }

        public int Capacity { get; }

        /// <summary>Monsters walking, attacking or emerging (the dying of a running step included).</summary>
        public int AliveCount => _alive;

        /// <summary>
        /// <see cref="AliveCount"/> plus the deaths freed but not yet taken by the owner (<see cref="Deaths"/>): a monster
        /// leaves this count only once its death has been handed out.
        /// </summary>
        public int AliveOrUnreported => _alive + _deaths.Length;

        /// <summary>Blasts and shoves dropped because a step's budget was full (a debug counter, never reset).</summary>
        public int DroppedAreas { get; private set; }

        /// <summary>A step is on the workers.</summary>
        public bool IsRunning => _running;

        /// <summary>Damage the monsters dealt to the tank in the last completed step: bites and shots.</summary>
        public float StepDamage => _stepDamage;

        /// <summary>Of <see cref="StepDamage"/>, what the shots dealt.</summary>
        public float StepShotDamage => _stepShotDamage;

        /// <summary>Live monsters within <see cref="HordeSettings.NearRadius"/> of the tank in the last completed step.</summary>
        public int NearCount => _near;

        /// <summary>Monsters that bit the hull in the last completed step, and where the one nearest the hull's centre stands.</summary>
        public int BiterCount => _biters;

        public float3 BiterPosition => _biterPosition;

        /// <summary>
        /// Deaths of the completed steps and of <see cref="Kill"/>, oldest first. The owner reads them
        /// and calls <see cref="ClearDeaths"/>; their slots are already free.
        /// </summary>
        public NativeArray<HordeDeathEvent> Deaths => _deaths.AsArray();

        /// <summary>
        /// Instances of the monsters the last completed step saw in the frustum, sorted by kind (see
        /// <see cref="GetVisibleRange"/>), until the next <see cref="Schedule"/>.
        /// </summary>
        public NativeArray<HordeInstance> Visible => _visible.AsArray();

        /// <summary>Length of <see cref="Visible"/> after the last completed step (readable while a step runs).</summary>
        public int VisibleCount => _visibleTotal;

        /// <summary>Shots in flight after the last completed step (instances: position and yaw), until the next <see cref="Schedule"/>.</summary>
        public NativeArray<HordeInstance> ShotInstances => _shotInstances.AsArray();

        /// <summary>Shots fired and ended in the last completed step.</summary>
        public NativeArray<HordeShotEvent> ShotEvents => _shotEvents.AsArray();

        public int ProjectileCount => _shotInstances.Length;

        /// <summary>The slice of <see cref="Visible"/> holding kind <paramref name="kind"/>.</summary>
        public void GetVisibleRange(int kind, out int start, out int count)
        {
            start = _visibleStart[kind];
            count = _visibleCount[kind];
        }

        public void ClearDeaths() => _deaths.Clear();

        /// <summary>Animation clips for the render prep (rows of the VAT texture).</summary>
        public void SetClips(HordeClip run, HordeClip attack, HordeClip idle)
        {
            _runClip = run;
            _attackClip = attack;
            _idleClip = idle;
        }

        /// <summary>The crawl clip (a VAT row): what a crawling monster (<see cref="IsCrawling"/>) plays instead of the run.</summary>
        public void SetCrawlClip(HordeClip crawl) => _crawlClip = crawl;

        /// <summary>The fall (a VAT row, not looped): what a knocked-down monster (<see cref="HordeState.Knocked"/>) plays.</summary>
        public void SetFallClip(HordeClip fall) => _fallClip = fall;

        /// <summary>The monster in this slot crawls on the ground (slower, low): chosen at its spawn.</summary>
        public bool IsCrawling(int index) => index >= 0 && index < Capacity && _crawl[index] != 0;

        /// <summary>
        /// A monster that jumps down out of a house (HordeState.Dropping): born at <paramref name="at"/> (its height kept: the
        /// hole's storey), flying on at <paramref name="outward"/> (horizontal, m/s) and <paramref name="up"/> (m/s) under
        /// gravity; it runs (no crawl) and walks on landing. The slot, or -1 when full. Between steps.
        /// </summary>
        public int SpawnDropping(float3 at, float2 outward, float up, int kind = 0)
        {
            int slot = Spawn(at, math.atan2(outward.x, outward.y), kind);
            if (slot < 0)
                return -1;
            _position[slot] = at;
            _nextPosition[slot] = at;
            _snapPosition[slot] = at;
            _state[slot] = HordeState.Dropping;
            _velocity[slot] = outward;
            _stateTime[slot] = up;
            _crawl[slot] = 0;
            return slot;
        }

        /// <summary>Makes the monster in this slot run, not crawl (a house's stream: runners only). Between steps.</summary>
        public void StopCrawling(int index)
        {
            ThrowIfRunning();
            if (index >= 0 && index < Capacity)
                _crawl[index] = 0;
        }

        /// <summary>Sets row <paramref name="index"/> of the kind table. A row never set follows <see cref="Settings"/> (<see cref="HordeKind.FromSettings"/>).</summary>
        public void SetKind(int index, in HordeKind kind)
        {
            _kindTable[index] = kind;
            _kindSet[index] = true;
        }

        public HordeKind GetKind(int index) => _kindSet[index] ? _kindTable[index] : HordeKind.FromSettings(Settings);

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

        /// <summary>Health the monster had when it came (its kind × the spawn's health scale).</summary>
        public float GetMaxHealth(int index) => _maxHealth[index];

        public float GetHeading(int index) => _heading[index];

        public int GetKindOf(int index) => _kind[index];

        /// <summary>
        /// A new monster of row <paramref name="kind"/> at <paramref name="position"/> (the feet), facing
        /// <paramref name="heading"/> (radians, 0 faces +Z), with its kind's health × <paramref name="healthScale"/> and
        /// damage × <paramref name="damageScale"/>. <paramref name="emerge"/>: it rises out of the ground for
        /// <see cref="HordeSettings.EmergeTime"/> s first. Returns its slot, or -1 when the horde is full.
        /// </summary>
        public int Spawn(float3 position, float heading, int kind = 0, float healthScale = 1f, float damageScale = 1f, bool emerge = false)
        {
            ThrowIfRunning();
            if (_freeCount == 0)
                return -1;

            kind = math.clamp(kind, 0, MAX_KINDS - 1);
            HordeKind row = GetKind(kind);
            int i = _free[--_freeCount];
            HordeSettings s = Settings;
            _position[i] = position;
            _nextPosition[i] = position;
            _velocity[i] = float2.zero;
            _heading[i] = heading;
            _health[i] = row.Health * healthScale;
            _torn[i] = 0;
            _tornDirection[i] = float3.zero;
            _tornPower[i] = 1f;
            _maxHealth[i] = _health[i];
            _state[i] = emerge ? HordeState.Emerging : HordeState.Walk;
            _stateTime[i] = emerge ? s.EmergeTime : 0f;
            _kind[i] = (byte)kind;
            _damageScale[i] = damageScale;
            _cooldown[i] = row.ShotInterval * _random.NextFloat(0.3f, 1f);
            _animTime[i] = _random.NextFloat(0f, 10f);
            _speedScale[i] = 1f + _random.NextFloat(-s.SpeedJitter, s.SpeedJitter);
            // A share of the crawlers (kind 0) crawls instead of running; the other kinds always walk upright.
            _crawl[i] = (byte)(kind == 0 && s.CrawlShare > 0f && _random.NextFloat() < s.CrawlShare ? 1 : 0);
            _attackDamage[i] = 0f;
            _slot[i] = -1;
            _snapPosition[i] = position;
            _snapLive[i] = true;
            _highWater = math.max(_highWater, i + 1);
            _alive++;
            return i;
        }

        /// <summary>Kills a live monster now: its slot is freed and a death (<see cref="HordeDeathCause.Killed"/>) is added.</summary>
        public bool Kill(int index)
        {
            ThrowIfRunning();
            if (!HordeStates.IsLive(_state[index]))
                return false;
            _deaths.Add(new HordeDeathEvent
            {
                Index = index, Position = _position[index], Heading = _heading[index], Cause = HordeDeathCause.Killed,
                Kind = _kind[index]
            });
            Release(index);
            return true;
        }

        /// <summary>Frees every slot and every shot in flight, without death events.</summary>
        public void Clear()
        {
            ThrowIfRunning();
            for (int i = 0; i < Capacity; i++)
            {
                if (_state[i] != HordeState.Free)
                    _version[i]++;
                _state[i] = HordeState.Free;
                _snapLive[i] = false;
                _slot[i] = -1;
            }

            for (int i = 0; i < Capacity; i++)
                _free[i] = Capacity - 1 - i;
            _freeCount = Capacity;
            _alive = 0;
            _highWater = 0;
            _visible.Clear();
            _visibleAll.Clear();
            _visibleTotal = 0;
            for (int k = 0; k < MAX_KINDS; k++)
                _visibleStart[k] = _visibleCount[k] = 0;
            _projectileCount[0] = 0;
            _shotInstances.Clear();
            _shotEvents.Clear();
            _shotQueue.Clear();
            _hits.Clear();
            _areas.Clear();
            _areaDamageCount = 0;
            _knockbackCount = 0;
            _deaths.Clear();
        }

        /// <summary>
        /// A blast for the next step: live monsters whose body reaches into the sphere take
        /// <paramref name="damage"/> at the centre, falling to damage × <paramref name="minFactor"/> at the edge.
        /// </summary>
        public void AddAreaDamage(float3 center, float radius, float damage, float minFactor = 0.5f)
        {
            if (radius <= 0f || damage <= 0f)
                return;
            if (_areaDamageCount >= MAX_AREAS)
            {
                DroppedAreas++;
                return;
            }

            _areaDamageCount++;
            _areas.Add(new HordeAreaDamage { Center = center, Radius = radius, Damage = damage, MinFactor = minFactor });
        }

        /// <summary>A shove for the next step: live monsters in the sphere move up to <paramref name="push"/> m out from its centre (no damage).</summary>
        public void AddKnockback(float3 center, float radius, float push)
        {
            if (radius <= 0f || push <= 0f)
                return;
            if (_knockbackCount >= MAX_KNOCKBACKS)
            {
                DroppedAreas++;
                return;
            }

            _knockbackCount++;
            _areas.Add(new HordeAreaDamage { Center = center, Radius = radius, MinFactor = 1f, Push = push });
        }

        // ------------------------------------------------------------------ queries (snapshot)

        /// <summary>Highest slot ever used + 1: the queries scan slots below it.</summary>
        public int HighWater => _highWater;

        /// <summary>Generation of a slot: bumped whenever its monster dies or is cleared, so an old (index, version) goes stale.</summary>
        public int Version(int index) => _version[index];

        /// <summary>Highest generation of any slot (a replacing simulation starts above it: see the constructor).</summary>
        public int MaxVersion()
        {
            int max = 0;
            for (int i = 0; i < Capacity; i++)
                max = math.max(max, _version[i]);
            return max;
        }

        /// <summary>The slot holds a live monster of this generation (as of the last completed step).</summary>
        public bool IsTargetAlive(int index, int version) =>
            index >= 0 && index < Capacity && _snapLive[index] && _version[index] == version;

        /// <summary>Feet position of a slot as of the last completed step.</summary>
        public float3 GetTargetPosition(int index) => _snapPosition[index];

        /// <summary>Live as of the last completed step (no generation check).</summary>
        public bool IsSlotLive(int index) => _snapLive[index];

        /// <summary>
        /// Direct damage to one monster, landing in the next step (a death of <see cref="HordeDeathCause.Damaged"/>
        /// when its health runs out). Ignored when the slot's generation is no longer <paramref name="version"/>.
        /// </summary>
        /// <summary>
        /// The monster, if the damage queued with it kills it, is torn apart (a shell's direct hit): its death reads
        /// <see cref="IsTorn"/>. A slot given to a new monster starts whole.
        /// </summary>
        public void Tear(int index, int version) => Tear(index, version, float3.zero);

        /// <param name="direction">Which way the shell flew: the body's goo and pieces are thrown on that way (<see cref="TornDirection"/>).</param>
        public void Tear(int index, int version, float3 direction) => Tear(index, version, direction, 1f);

        /// <param name="power">How hard the shell throws the torn body's goo and pieces (<see cref="TornPower"/>); 1 — as before.</param>
        public void Tear(int index, int version, float3 direction, float power)
        {
            if (!IsTargetAlive(index, version))
                return;
            _torn[index] = 1;
            _tornDirection[index] = direction;
            _tornPower[index] = power;
        }

        /// <summary>The throw factor of the shell that tore the monster in this slot (1: not torn, or no HE skill).</summary>
        public float TornPower(int index) =>
            index >= 0 && index < _tornPower.Length && _torn[index] != 0 ? _tornPower[index] : 1f;

        /// <summary>Which way the shell that tore the monster in this slot flew (zero: none known, or not torn).</summary>
        public float3 TornDirection(int index) =>
            index >= 0 && index < _tornDirection.Length && _torn[index] != 0 ? _tornDirection[index] : float3.zero;

        /// <summary>Whether the monster that died in this slot was torn apart (read with its death).</summary>
        public bool IsTorn(int index) => index >= 0 && index < _torn.Length && _torn[index] != 0;

        public void Damage(int index, int version, float damage)
        {
            if (damage <= 0f || !IsTargetAlive(index, version) || _hits.Length >= MAX_HITS)
                return;
            _hits.Add(new HordeHit { Index = index, Version = version, Damage = damage });
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
                _visibleAll.Clear();
                (_areas, _areasRunning) = (_areasRunning, _areas);
                _areas.Clear();
                _areaDamageCount = 0;
                _knockbackCount = 0;
                for (int k = 0; k < MAX_KINDS; k++)
                    _kinds[k] = GetKind(k);
                for (int h = 0; h < _hits.Length; h++)
                {
                    HordeHit hit = _hits[h];
                    // The slot may have been freed and given to a new monster since the hit was queued.
                    if (_version[hit.Index] == hit.Version && HordeStates.IsLive(_state[hit.Index]))
                        _health[hit.Index] -= hit.Damage;
                }

                _hits.Clear();

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
                    HashCell = HASH_CELL,
                    Marker = HashMarker
                }.Schedule(dependsOn);

                float touchRange = 0f;
                for (int k = 0; k < MAX_KINDS; k++)
                    if (_kindSet[k])
                        touchRange = math.max(touchRange, 2f * _kinds[k].Radius);
                touchRange = math.max(touchRange, 2f * settings.Radius);

                HordeRing ring = BuildRing(settings, tank);
                _ringSlots = ring.Active ? ring.Count : 0;
                int obstacleCount = math.min(HordeObstacles.HighWater, _obstacles.Length);
                for (int o = 0; o < obstacleCount; o++)
                    _obstacles[o] = HordeObstacles.Get(o);

                JobHandle slots = new HordeSlotJob
                {
                    Position = _position,
                    State = _state,
                    Kind = _kind,
                    Kinds = _kinds,
                    Obstacles = _obstacles,
                    Slot = _slot,
                    Owner = _slotOwner,
                    Ring = ring,
                    Tank = tank,
                    Settings = settings,
                    Count = _highWater,
                    ObstacleCount = obstacleCount,
                    Marker = SlotsMarker
                }.Schedule(dependsOn);

                JobHandle steer = new HordeSteerJob
                {
                    Position = _position,
                    State = _state,
                    SpeedScale = _speedScale,
                    Crawling = _crawl,
                    Kind = _kind,
                    Kinds = _kinds,
                    CellStart = _cellStart,
                    Sorted = _sorted,
                    Slot = _slot,
                    Obstacles = _obstacles,
                    Ring = ring,
                    TouchRange = touchRange,
                    ObstacleCount = obstacleCount,
                    NextPosition = _nextPosition,
                    Velocity = _velocity,
                    Heading = _heading,
                    AnimTime = _animTime,
                    Grid = grid,
                    HashSize = _hashSize,
                    HashOrigin = _hashOrigin,
                    HashCell = HASH_CELL,
                    Settings = settings,
                    Tank = tank,
                    DeltaTime = dt,
                    Marker = SteerMarker
                }.ScheduleBatch(Capacity, BATCH, JobHandle.CombineDependencies(hash, slots));

                JobHandle contact = new HordeContactJob
                {
                    Position = _nextPosition,
                    State = _state,
                    Health = _health,
                    StateTime = _stateTime,
                    AnimTime = _animTime,
                    Velocity = _velocity,
                    Cooldown = _cooldown,
                    MaxHealth = _maxHealth,
                    AttackDamage = _attackDamage,
                    Heading = _heading,
                    Kind = _kind,
                    DamageScale = _damageScale,
                    Kinds = _kinds,
                    Areas = _areasRunning.AsArray(),
                    Deaths = _deathQueue.AsParallelWriter(),
                    Shots = _shotQueue.AsParallelWriter(),
                    Grid = grid,
                    Settings = settings,
                    Tank = tank,
                    DeltaTime = dt,
                    Marker = ContactMarker
                }.ScheduleBatch(Capacity, BATCH, steer);

                JobHandle shots = new HordeProjectileJob
                {
                    Projectiles = _projectiles,
                    Count = _projectileCount,
                    Fired = _shotQueue,
                    Instances = _shotInstances,
                    Events = _shotEvents,
                    Totals = _totals,
                    Grid = grid,
                    Tank = tank,
                    HitPad = SHOT_HIT_PAD,
                    DeltaTime = dt,
                    Marker = ShotsMarker
                }.Schedule(contact);

                JobHandle totals = new HordeTotalsJob
                {
                    AttackDamage = _attackDamage,
                    Position = _nextPosition,
                    State = _state,
                    Totals = _totals,
                    Tank = tank,
                    NearRadius = settings.NearRadius
                }.Schedule(shots);

                JobHandle render = new HordeRenderJob
                {
                    Position = _nextPosition,
                    State = _state,
                    Heading = _heading,
                    AnimTime = _animTime,
                    Velocity = _velocity,
                    StateTime = _stateTime,
                    Kind = _kind,
                    Kinds = _kinds,
                    Planes = _planes,
                    Instances = _visibleAll.AsParallelWriter(),
                    Run = _runClip,
                    Attack = _attackClip,
                    Idle = _idleClip,
                    Crawl = _crawlClip,
                    Crawling = _crawl,
                    Fall = _fallClip,
                    Cull = _cull,
                    EmergeTime = settings.EmergeTime,
                    CullSphere = CullSphere,
                    BodyScale = settings.BodyScale,
                    Marker = RenderMarker
                }.ScheduleBatch(Capacity, BATCH, contact);

                JobHandle partition = new HordePartitionJob
                {
                    Unsorted = _visibleAll,
                    Sorted = _visible,
                    Starts = _kindStarts,
                    Counts = _kindCounts
                }.Schedule(render);

                _handle = JobHandle.CombineDependencies(totals, partition);
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
                _stepShotDamage = _totals[2];
                _stepDamage = _totals[0] + _stepShotDamage;
                _near = (int)_totals[1];
                _biters = (int)_totals[3];
                _biterPosition = new float3(_totals[4], _totals[5], _totals[6]);
                _visibleTotal = _visible.Length;
                for (int k = 0; k < MAX_KINDS; k++)
                {
                    _visibleStart[k] = _kindStarts[k];
                    _visibleCount[k] = _kindCounts[k];
                }

                _areasRunning.Clear();

                while (_deathQueue.TryDequeue(out HordeDeathEvent death))
                {
                    _deaths.Add(death);
                    Release(death.Index);
                }

                if (_highWater > 0)
                    NativeArray<float3>.Copy(_position, _snapPosition, _highWater);
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
            if (_torn.IsCreated) _torn.Dispose();
            if (_crawl.IsCreated) _crawl.Dispose();
            if (_tornDirection.IsCreated) _tornDirection.Dispose();
            if (_tornPower.IsCreated) _tornPower.Dispose();
            if (_state.IsCreated) _state.Dispose();
            if (_animTime.IsCreated) _animTime.Dispose();
            if (_speedScale.IsCreated) _speedScale.Dispose();
            if (_attackDamage.IsCreated) _attackDamage.Dispose();
            if (_kind.IsCreated) _kind.Dispose();
            if (_damageScale.IsCreated) _damageScale.Dispose();
            if (_stateTime.IsCreated) _stateTime.Dispose();
            if (_cooldown.IsCreated) _cooldown.Dispose();
            if (_maxHealth.IsCreated) _maxHealth.Dispose();
            if (_slot.IsCreated) _slot.Dispose();
            if (_slotOwner.IsCreated) _slotOwner.Dispose();
            if (_obstacles.IsCreated) _obstacles.Dispose();
            if (_kinds.IsCreated) _kinds.Dispose();
            if (_free.IsCreated) _free.Dispose();
            if (_cellStart.IsCreated) _cellStart.Dispose();
            if (_cursor.IsCreated) _cursor.Dispose();
            if (_cellIndex.IsCreated) _cellIndex.Dispose();
            if (_sorted.IsCreated) _sorted.Dispose();
            if (_totals.IsCreated) _totals.Dispose();
            if (_planes.IsCreated) _planes.Dispose();
            if (_areas.IsCreated) _areas.Dispose();
            if (_areasRunning.IsCreated) _areasRunning.Dispose();
            if (_hits.IsCreated) _hits.Dispose();
            if (_deathQueue.IsCreated) _deathQueue.Dispose();
            if (_deaths.IsCreated) _deaths.Dispose();
            if (_visibleAll.IsCreated) _visibleAll.Dispose();
            if (_visible.IsCreated) _visible.Dispose();
            if (_kindStarts.IsCreated) _kindStarts.Dispose();
            if (_kindCounts.IsCreated) _kindCounts.Dispose();
            if (_shotQueue.IsCreated) _shotQueue.Dispose();
            if (_projectiles.IsCreated) _projectiles.Dispose();
            if (_projectileCount.IsCreated) _projectileCount.Dispose();
            if (_shotInstances.IsCreated) _shotInstances.Dispose();
            if (_shotEvents.IsCreated) _shotEvents.Dispose();
            if (_snapPosition.IsCreated) _snapPosition.Dispose();
            if (_snapLive.IsCreated) _snapLive.Dispose();
            if (_version.IsCreated) _version.Dispose();
        }

        /// <summary>
        /// The attack ring of a step against <paramref name="tank"/>: the crowd's body radius plus half the bite's reach
        /// off the hull, places <see cref="HordeSettings.SlotSpacing"/> bodies wide, the waiting row one place further out.
        /// </summary>
        internal static HordeRing BuildRing(in HordeSettings settings, in HordeTank tank)
        {
            if (!settings.HullSlots || !tank.Present || settings.Radius <= 0f)
                return default;
            float spacing = 2f * settings.Radius * math.max(settings.SlotSpacing, 1f);
            float standoff = settings.Radius + settings.AttackReach * 0.5f;
            float2 half = math.max(tank.HalfExtents.xz, 0.01f);
            return new HordeRing
            {
                Active = true,
                Half = half,
                Standoff = standoff,
                WaitStandoff = standoff + spacing,
                Length = HordeHullRing.Length(half, standoff),
                Count = HordeHullRing.SlotCount(half, standoff, spacing),
                MaxRadius = settings.Radius * 1.5f
            };
        }

        /// <summary>Places on the hull ring in the last step (0: no ring).</summary>
        public int RingSlots => _ringSlots;

        /// <summary>The place on the hull ring the monster in this slot holds after the last step, -1 none (read between steps).</summary>
        public int GetRingSlot(int index) => index >= 0 && index < _slot.Length ? _slot[index] : -1;

        private void Release(int index)
        {
            _state[index] = HordeState.Free;
            _velocity[index] = float2.zero;
            _snapLive[index] = false;
            _version[index]++;
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

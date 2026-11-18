using System;
using System.Collections.Generic;
using Atomic.Entities;
using Unity.Profiling;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The arena director (GDD §13, AI milestone-1 spec §3): a behaviour of the level's AI entity, ticked by its world
    /// (the GameLoop: pause and the skill choice stop it). Sends an arena's <see cref="ArenaWaves"/> out of its burrows:
    /// <list type="bullet">
    /// <item>a wave joins the queue when its delay (from the end of the lull) is up — <see cref="AIEvents.WaveStarted"/>;</item>
    /// <item>the queue empties while live enemies are under 70 % of the live limit, then refills up to the limit
    /// (live: the horde's, the big one included);</item>
    /// <item>each monster comes out of the burrow farthest from the tank of those at least 15 m from it, never two
    /// emerging from one burrow at a time; the first monster of a burrow opens it (its crater, its sound);</item>
    /// <item>health and damage × <see cref="Strength"/>: the run's EnemyStrength, or the chapter's multiplier
    /// × (1 + 0.06·(arena − 1)) without a run;</item>
    /// <item>EliteRegax entries are the horde's big kind (<see cref="HordeKinds.BRUTE"/>): a monster like the others;</item>
    /// <item>every wave out and nothing alive: the arena is cleared once — <c>IRunService.MarkArenaCleared</c>
    /// with a run, <see cref="AIEvents.ArenaCleared"/> without.</item>
    /// </list>
    /// While an arena runs it is the level's <see cref="IArenaReinforcements"/> (<see cref="AIAPI.ArenaReinforcements"/>):
    /// summoned monsters come out of the burrows nearest the given point, over the limit if need be.
    /// <para>
    /// With a run in the GameContext it follows it: ArenaStarted clears the horde (and rebuilds the level grid when no
    /// arena service will), the arena service's Built rebuilds the grid over the new arena and clears the horde,
    /// FightStarted starts the waves of that arena from <see cref="Table"/> (the run's lull is the lull). Without a run
    /// (sandbox, tests, bench) <see cref="Begin"/> does all of it with a lull of its own.
    /// </para>
    /// No managed allocations per frame (an arena's start allocates its queue).
    /// </summary>
    public sealed class ArenaDirector : IEntityInit, IEntityDispose, IEntityTick, IArenaReinforcements
    {
        public const float LULL = 2f;
        public const float MIN_BURROW_DISTANCE = 15f;
        public const float REFILL_SHARE = 0.7f;
        public const float ARENA_STRENGTH_STEP = 0.06f;
        public const int DEFAULT_LIVE_LIMIT = 40;
        /// <summary>Seconds between two summoned monsters.</summary>
        public const float SUMMON_INTERVAL = 0.15f;
        private const int MAX_SUMMONS = 128;
        /// <summary>Burrows of the stand-in arena made when a run fights without an arena service.</summary>
        private const int FALLBACK_BURROWS = 12;
        private const float FALLBACK_SIZE = 40f;
        private const float BIND_INTERVAL = 1f;

        private static readonly ProfilerMarker TickMarker = new("Horde.Director");

        private readonly SummonRequest[] _summons = new SummonRequest[MAX_SUMMONS];

        private IEntity _entity;
        private IRunService _run;
        private IArenaService _arenaService;
        private ArenaServiceBurrows _serviceBurrows;
        private IArenaHorde _horde;

        private ArenaWaves _waves;
        private IArenaBurrows _burrows;
        private EnemyKind[] _queue = new EnemyKind[64];
        private bool[] _opened = Array.Empty<bool>();
        private float[] _busyUntil = Array.Empty<float>();
        private int _queueHead;
        private int _queueTail;
        private int _summonHead;
        private int _summonCount;
        private int _nextWave;
        private float _clock;
        private float _lull;
        private float _interval;
        private float _releaseTimer;
        private float _summonTimer;
        private bool _refilling;
        private bool _running;
        private bool _cleared;
        private bool _warnedNoWaves;
        private float _bindTimer;
        private bool _arenaApplied;

        private struct SummonRequest
        {
            public EnemyKind Kind;
            public Vector3 Near;
        }

        public ArenaDirector(ArenaWavesTable table = null) => Table = table;

        /// <summary>The waves of a run's arenas by number (used when the run starts a fight).</summary>
        public ArenaWavesTable Table { get; set; }

        /// <summary>The horde to spawn into; null: the level's (<see cref="HordeAPI.LevelHorde"/>).</summary>
        public IArenaHorde Horde
        {
            get => _horde ?? (_entity != null && _entity.TryGetLevelHorde(out IArenaHorde h) ? h : null);
            set => _horde = value;
        }

        /// <summary>Follows the run and the arena service of the GameContext (off in tests without a scene).</summary>
        public bool UseGameContext { get; set; } = true;

        public bool IsRunning => _running;

        /// <summary>The arena running (1-based), 0 before the first.</summary>
        public int ArenaNumber { get; private set; }

        /// <summary>Health and damage multiplier of this arena's enemies.</summary>
        public float Strength { get; private set; } = 1f;

        public int LiveLimit => _waves != null && _waves.liveLimit > 0 ? _waves.liveLimit : DEFAULT_LIVE_LIMIT;

        /// <summary>Waves started so far.</summary>
        public int WavesStarted => _nextWave;

        /// <summary>Enemies of started waves still waiting to come out.</summary>
        public int Queued => _queueTail - _queueHead;

        /// <summary>Summoned monsters still waiting to come out.</summary>
        public int SummonsQueued => _summonCount;

        /// <summary>Live enemies as the limit counts them.</summary>
        public int Live => Horde?.AliveCount ?? 0;

        public bool IsCleared => _cleared;

        public IArenaBurrows Burrows => _burrows;

        public void Init(IEntity entity)
        {
            _entity = entity;
            BindServices();
        }

        public void Dispose(IEntity entity)
        {
            if (_run != null)
            {
                _run.ArenaStarted -= OnRunArenaStarted;
                _run.FightStarted -= OnRunFightStarted;
            }

            if (_arenaService != null)
                _arenaService.Built -= OnArenaBuilt;
            _run = null;
            _arenaService = null;
            End();
            _entity = null;
        }

        // ------------------------------------------------------------------ arena

        /// <summary>
        /// Starts arena <paramref name="arenaIndex"/> (1-based) without a run: the level grid is rebuilt over the arena,
        /// the horde cleared, then after a <see cref="LULL"/> the waves come; strength = the waves' chapter multiplier
        /// × (1 + 0.06·(arena − 1)).
        /// </summary>
        public void Begin(int arenaIndex, ArenaWaves waves, IArenaBurrows layout)
        {
            ResetForArena(layout?.Bounds);
            float chapter = waves != null && waves.chapterMultiplier > 0f ? waves.chapterMultiplier : 1f;
            StartArena(arenaIndex, waves, layout, LULL, chapter * (1f + ARENA_STRENGTH_STEP * (Mathf.Max(1, arenaIndex) - 1)));
        }

        /// <summary>
        /// Stops the arena and clears the stage: the level grid is read again (moved over <paramref name="area"/> when
        /// given), the horde loses every monster, shot and corpse.
        /// </summary>
        public void ResetForArena(Bounds? area = null)
        {
            End();
            if (_entity != null && _entity.TryGetLevelNavigation(out LevelNav nav) && nav != null)
                nav.Rebuild(area);
            Horde?.ResetForArena();
        }

        /// <summary>Stops sending monsters (those out stay) and takes the reinforcements door off the level.</summary>
        public void End()
        {
            _running = false;
            _queueHead = _queueTail = 0;
            _summonHead = _summonCount = 0;
            if (_entity != null && _entity.TryGetArenaReinforcements(out IArenaReinforcements door) && ReferenceEquals(door, this))
                _entity.DelArenaReinforcements();
        }

        /// <summary>Monsters outside the waves (the elite's roar): out of the burrows nearest <paramref name="near"/>, over the limit if need be.</summary>
        public void Summon(EnemyKind kind, int count, Vector3 near)
        {
            if (!_running || kind == EnemyKind.EliteRegax || kind == EnemyKind.Boss)
                return;
            for (int i = 0; i < count && _summonCount < MAX_SUMMONS; i++)
            {
                _summons[(_summonHead + _summonCount) % MAX_SUMMONS] = new SummonRequest { Kind = kind, Near = near };
                _summonCount++;
            }
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            using (TickMarker.Auto())
            {
                _bindTimer -= deltaTime;
                BindServices();
                if (!_running)
                    return;

                _clock += deltaTime;
                if (_clock < _lull)
                    return;

                float fight = _clock - _lull;
                ArenaWave[] waves = _waves != null ? _waves.waves : null;
                int waveCount = waves?.Length ?? 0;
                while (_nextWave < waveCount && fight >= (waves[_nextWave]?.delay ?? 0f))
                    StartWave(waves[_nextWave], _nextWave++);

                IArenaHorde horde = Horde;
                bool hasTank = TryGetTank(out Vector3 tank);
                ReleaseSummons(horde, deltaTime);
                ReleaseQueue(horde, hasTank, tank, deltaTime);

                if (!_cleared && _nextWave >= waveCount && Queued == 0 && _summonCount == 0
                    && (horde?.AliveCount ?? 0) == 0)
                {
                    _cleared = true;
                    End();
                    if (_run != null)
                        _run.MarkArenaCleared();
                    else
                        AIEvents.RaiseArenaCleared();
                }
            }
        }

        // ------------------------------------------------------------------ steps

        private void StartArena(int arenaIndex, ArenaWaves waves, IArenaBurrows burrows, float lull, float strength)
        {
            End();
            ArenaNumber = arenaIndex;
            Strength = strength;
            _waves = waves;
            _burrows = burrows;
            _lull = lull;
            _clock = 0f;
            _nextWave = 0;
            _releaseTimer = 0f;
            _summonTimer = 0f;
            _interval = 0.35f;
            _refilling = true;
            _cleared = false;

            int total = waves != null ? waves.TotalCount : 0;
            if (_queue.Length < total)
                _queue = new EnemyKind[Mathf.NextPowerOfTwo(total)];
            int burrowCount = burrows?.BurrowPoints?.Count ?? 0;
            if (_opened.Length != burrowCount)
            {
                _opened = new bool[burrowCount];
                _busyUntil = new float[burrowCount];
            }

            for (int i = 0; i < burrowCount; i++)
            {
                _opened[i] = false;
                _busyUntil[i] = float.MinValue;
            }

            if (waves == null && !_warnedNoWaves)
            {
                _warnedNoWaves = true;
                Debug.LogWarning("ArenaDirector: no waves for arena " + arenaIndex + "; it clears once nothing is alive.");
            }

            if (_entity != null)
                _entity.SetArenaReinforcements(this);
            _running = true;
        }

        private void StartWave(ArenaWave wave, int index)
        {
            if (wave != null)
            {
                _interval = Mathf.Max(0f, wave.spawnInterval);
                ArenaWaveEntry[] entries = wave.entries;
                // Interleaved: a wave of crawlers and spitters comes out mixed, not kind after kind.
                for (int round = 0; entries != null; round++)
                {
                    bool any = false;
                    for (int e = 0; e < entries.Length; e++)
                    {
                        if (round >= entries[e].count)
                            continue;
                        any = true;
                        if (_queueTail < _queue.Length)
                            _queue[_queueTail++] = entries[e].kind;
                    }

                    if (!any)
                        break;
                }
            }

            AIEvents.RaiseWaveStarted(index);
            Horde?.WaveStarted();
        }

        private void ReleaseQueue(IArenaHorde horde, bool hasTank, Vector3 tank, float deltaTime)
        {
            int live = Live;
            int limit = LiveLimit;
            if (live < limit * REFILL_SHARE)
                _refilling = true;
            if (live >= limit)
                _refilling = false;

            _releaseTimer -= deltaTime;
            while (_queueHead < _queueTail && _refilling && _releaseTimer <= 0f)
            {
                EnemyKind kind = _queue[_queueHead];
                int burrow = PickFar(hasTank, tank);
                if (burrow < 0)
                    break;
                if (!Spawn(horde, kind, burrow, out bool unsupported) && !unsupported)
                    break; // the horde is full: try again next frame
                _queueHead++;
                if (unsupported)
                    continue;
                _releaseTimer += _interval;
                if (++live >= limit)
                    _refilling = false;
            }

            if (_releaseTimer < 0f)
                _releaseTimer = 0f; // waiting (limit, burrows) does not save up a burst
        }

        private void ReleaseSummons(IArenaHorde horde, float deltaTime)
        {
            _summonTimer -= deltaTime;
            while (_summonCount > 0 && _summonTimer <= 0f)
            {
                SummonRequest s = _summons[_summonHead];
                int burrow = PickNear(s.Near);
                if (burrow < 0)
                    break;
                if (!Spawn(horde, s.Kind, burrow, out bool unsupported) && !unsupported)
                    break;
                _summonHead = (_summonHead + 1) % MAX_SUMMONS;
                _summonCount--;
                _summonTimer += SUMMON_INTERVAL;
            }

            if (_summonTimer < 0f)
                _summonTimer = 0f;
        }

        private bool Spawn(IArenaHorde horde, EnemyKind kind, int burrow, out bool unsupported)
        {
            unsupported = false;
            if (horde == null)
                return false;
            if (!HordeKinds.TryGetRow(kind, out _))
            {
                unsupported = true; // a kind the horde does not have (yet): dropped
                return false;
            }

            Vector3 at = Open(horde, burrow);
            bool ok = horde.Spawn(kind, at, Strength, Strength);
            if (ok)
                _busyUntil[burrow] = _clock + horde.EmergeSeconds;
            return ok;
        }

        /// <summary>The burrow's point; the first time, it is opened (crater) and heard.</summary>
        private Vector3 Open(IArenaHorde horde, int burrow)
        {
            Vector3 at = _burrows.OpenBurrow(burrow);
            if (!_opened[burrow])
            {
                _opened[burrow] = true;
                horde?.BurrowOpened(at);
            }

            return at;
        }

        /// <summary>The free burrow farthest from the tank among those at least <see cref="MIN_BURROW_DISTANCE"/> from it; -1 if none.</summary>
        private int PickFar(bool hasTank, Vector3 tank)
        {
            IReadOnlyList<Vector3> points = _burrows?.BurrowPoints;
            if (points == null)
                return -1;
            int best = -1;
            float bestSq = MIN_BURROW_DISTANCE * MIN_BURROW_DISTANCE;
            for (int i = 0; i < points.Count && i < _busyUntil.Length; i++)
            {
                if (_busyUntil[i] > _clock)
                    continue;
                if (!hasTank)
                    return i;
                Vector3 d = points[i] - tank;
                d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < bestSq)
                    continue;
                bestSq = sq;
                best = i;
            }

            return best;
        }

        /// <summary>The free burrow nearest <paramref name="near"/>; -1 if all are busy.</summary>
        private int PickNear(Vector3 near)
        {
            IReadOnlyList<Vector3> points = _burrows?.BurrowPoints;
            if (points == null)
                return -1;
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < points.Count && i < _busyUntil.Length; i++)
            {
                if (_busyUntil[i] > _clock)
                    continue;
                Vector3 d = points[i] - near;
                d.y = 0f;
                if (d.sqrMagnitude >= bestSq)
                    continue;
                bestSq = d.sqrMagnitude;
                best = i;
            }

            return best;
        }

        private bool TryGetTank(out Vector3 position)
        {
            position = default;
            if (_entity == null || !_entity.TryGetPlayerVehicle(out PlayerTank tank) || tank?.Transform == null)
                return false;
            position = tank.Transform.position;
            return true;
        }

        // ------------------------------------------------------------------ the run, the arena, the deaths

        /// <summary>Hooks up to the run and the arena service as soon as the GameContext has them.</summary>
        private void BindServices()
        {
            if (!UseGameContext || (_run != null && _arenaService != null))
                return;
            // Looked for at most once a second until found (a scene search while the context is missing).
            if (_bindTimer > 0f)
                return;
            _bindTimer = BIND_INTERVAL;
            if (!GameContext.TryGetInstance(out GameContext context) || context == null)
                return;

            if (_run == null && context.TryGetRun(out IRunService run) && run != null)
            {
                _run = run;
                _run.ArenaStarted += OnRunArenaStarted;
                _run.FightStarted += OnRunFightStarted;
            }

            if (_arenaService == null && context.TryGetArena(out IArenaService arena) && arena != null)
            {
                _arenaService = arena;
                _serviceBurrows = new ArenaServiceBurrows(arena);
                _arenaService.Built += OnArenaBuilt;
            }

            // Bound late, in the middle of a fight: join it.
            if (_run != null && !_running && _run.State == RunState.Fight && _run.ArenaNumber > 0 && ArenaNumber != _run.ArenaNumber)
                OnRunFightStarted(_run.ArenaNumber);
        }

        private void OnRunArenaStarted(int number)
        {
            _arenaApplied = false;
            if (_arenaService != null)
            {
                End();
                Horde?.ResetForArena();
            }
            else
            {
                ResetForArena();
            }
        }

        private void OnArenaBuilt(ArenaPlan plan)
        {
            ResetForArena(_arenaService.Bounds);
            _arenaApplied = true;
        }

        private void OnRunFightStarted(int number)
        {
            // The arena was built before this director heard of the service (its first arena): the grid moves over it now.
            if (_arenaService != null && _arenaService.IsBuilt && !_arenaApplied)
            {
                ResetForArena(_arenaService.Bounds);
                _arenaApplied = true;
            }

            IArenaBurrows burrows = _serviceBurrows;
            if (burrows == null)
            {
                TryGetTank(out Vector3 tank);
                GroundOf(out Func<Vector3, float> ground);
                burrows = EdgeBurrowsLayout.Around(tank, FALLBACK_SIZE, FALLBACK_BURROWS, ground);
            }

            StartArena(number, Table != null ? Table.For(number) : null, burrows, 0f, _run.EnemyStrength);
        }

        private void GroundOf(out Func<Vector3, float> ground)
        {
            ground = null;
            if (_entity != null && _entity.TryGetLevelNavigation(out LevelNav nav) && nav != null)
            {
                Game.Horde.HordeNavGrid grid = nav.Grid;
                ground = p => grid.HeightAt(p);
            }
        }
    }
}

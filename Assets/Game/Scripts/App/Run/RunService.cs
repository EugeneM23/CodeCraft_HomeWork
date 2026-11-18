using System;
using System.Collections.Generic;
using Ticking;

namespace Game
{
    /// <summary>
    /// The run (see <see cref="IRunService"/>, its comments are the spec): plain C#, ticked by its owner from the game
    /// loop (<see cref="Tick"/>), so every timer and <see cref="TimeSec"/> stand on pause.
    /// <para>Decisions beyond the contract:</para>
    /// <list type="bullet">
    /// <item>Timings: <see cref="RunTimings"/>. MarkArenaReady only marks the arena ready; the shutter opens on the next
    /// Tick (so a Build that is ready at once still sees ArenaStarted before ShutterChanged(false)). Without an
    /// <see cref="IArenaBuilder"/> an arena is ready at once.</item>
    /// <item>Marks and samples: base units (<see cref="RunRewards"/>) are summed and the sum is multiplied by the
    /// chapter's RewardMultiplier and rounded half away from zero — no drift per kill. The first-completion reward is in
    /// chapter units already and is added unmultiplied at Victory.</item>
    /// <item>Elites and bosses: a Boss kind counts as a boss (RunResult.bossesKilled), an EliteRegax kind or a death
    /// with the Elite flag as an elite (<see cref="ElitesKilled"/>).</item>
    /// <item>EndRun(Defeat/Abandoned) after the last arena is cleared (the victory delay) ends the run as Victory at once.</item>
    /// <item>A skill offer pauses the game loop given (source: this run) until the last pending level-up is chosen;
    /// a run's end or <see cref="Dispose"/> lets the pause go.</item>
    /// <item>The seed of a run comes from the injected Random, which also picks the offers.</item>
    /// </list>
    /// The tick, deaths and AddExperience allocate nothing; StartRun and offers reuse their lists.
    /// </summary>
    public sealed class RunService : IRunService, IDisposable
    {
        public const int OFFER_SIZE = 3;
        public const int DEFAULT_WEAPON_SLOTS = 4;
        public const int DEFAULT_UPGRADE_SLOTS = 6;
        public const float STRENGTH_PER_ARENA = 1.06f;

        private readonly GameCatalog _catalog;
        private readonly IEnemyDeaths _deaths;
        private readonly Random _random;
        private readonly RunTimings _timings;
        private readonly IArenaBuilder _arenas;
        private readonly IGameLoop _loop;
        private readonly Func<ChapterId, bool> _isChapterCompleted;
        private readonly Func<ChapterId, int> _bestArenaOf;
        private readonly Action _leaveToHangar;
        private readonly Action<EnemyDeath> _onDied;

        private readonly List<SkillId> _offer = new(OFFER_SIZE);
        private readonly List<SkillId> _skills = new();
        private readonly Dictionary<SkillId, int> _skillLevels = new();
        private readonly List<SkillId> _candidates = new();

        private ChapterConfig _chapter;
        private RunState _state;
        private int _arenaNumber;
        private ArenaConfig _arena;
        private float _enemyStrength = 1f;

        // Phase timers inside a state: Preparing (wait ready, then the shutter opening), Leaving (orb pull, then closing)
        private float _timer;
        private bool _ready;
        private bool _shutterMoving;
        private bool _victoryPending;

        private int _weaponSlots;
        private int _upgradeSlots;
        private int _weaponsTaken;
        private int _upgradesTaken;

        private int _baseMarks;
        private int _baseSamples;
        private int _bonusMarks;
        private int _bonusSamples;
        private int _bosses;
        private float _sinceCrush;
        private bool _pausing;
        private bool _disposed;

        /// <param name="catalog">Skills, tanks and chapters.</param>
        /// <param name="deaths">The level's deaths; heard while the run is running.</param>
        /// <param name="random">The seeds and the offers; null — a new one.</param>
        /// <param name="timings">Null — <see cref="RunTimings.Default"/>.</param>
        /// <param name="arenas">Builds every arena behind the shutter; null — none, an arena is ready at once.</param>
        /// <param name="loop">Paused while a skill offer is shown; null — no pause.</param>
        /// <param name="isChapterCompleted">Whether the chapter was completed before (the first-completion reward);
        /// null — no chapter was, the reward comes with every victory.</param>
        /// <param name="bestArenaOf">The chapter's best arena before this run (RunResult.newRecord); null — 0.</param>
        /// <param name="leaveToHangar">What <see cref="LeaveToHangar"/> does after ending the run; null — nothing.</param>
        public RunService(GameCatalog catalog, IEnemyDeaths deaths, Random random = null, RunTimings? timings = null,
            IArenaBuilder arenas = null, IGameLoop loop = null, Func<ChapterId, bool> isChapterCompleted = null,
            Func<ChapterId, int> bestArenaOf = null, Action leaveToHangar = null)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _deaths = deaths;
            _random = random ?? new Random();
            _timings = timings ?? RunTimings.Default;
            _arenas = arenas;
            _loop = loop;
            _isChapterCompleted = isChapterCompleted;
            _bestArenaOf = bestArenaOf;
            _leaveToHangar = leaveToHangar;
            _onDied = OnDied;

            if (_deaths != null)
                _deaths.Died += _onDied;
        }

        // ---------- Events ----------

        public event Action RunStarted;
        public event Action<int> ArenaStarted;
        public event Action<int> FightStarted;
        public event Action<int> ArenaCleared;
        public event Action<int> GateOpened;
        public event Action<int> ArenaLeft;
        public event Action<bool> ShutterChanged;
        public event Action<int> LevelUp;
        public event Action<IReadOnlyList<SkillId>> SkillOffered;
        public event Action<SkillId, int> SkillChanged;
        public event Action LootChanged;
        public event Action<RunState> StateChanged;
        public event Action<RunResult> RunEnded;

        // ---------- State ----------

        public RunState State => _state;
        public bool IsRunning => _state != RunState.None && _state != RunState.Ended;
        public ChapterId Chapter { get; private set; }
        public int Seed { get; private set; }
        public int ArenaNumber => _arenaNumber;
        public int ArenaCount => _chapter != null ? _chapter.ArenaCount : 0;
        public ArenaId Arena => _arena != null ? _arena.Id : default;
        public bool IsEliteArena => _arena != null && _arena.Kind != ArenaKind.Horde;
        public float EnemyStrength => _enemyStrength;
        public float TimeSec { get; private set; }
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int ExperienceToNextLevel => ExperienceForLevel(Level);
        public int PendingLevelUps { get; private set; }
        public IReadOnlyList<SkillId> SkillOffer => _offer;
        public IReadOnlyList<SkillId> Skills => _skills;
        public int Marks { get; private set; }
        public int Samples { get; private set; }
        public int Kills { get; private set; }
        public int Crushes { get; private set; }
        public int CrushStreak { get; private set; }
        public int ElitesKilled { get; private set; }
        public int BossesKilled => _bosses;
        public RunResult Result { get; private set; }

        /// <summary>Experience level n needs: 10 + 12·(n − 1) (GDD §3).</summary>
        public static int ExperienceForLevel(int level) => 10 + 12 * (level - 1);

        public int GetSkillLevel(SkillId skill) => _skillLevels.TryGetValue(skill, out int level) ? level : 0;

        // ---------- Commands ----------

        public bool StartRun(ChapterId chapter, TankId tank)
        {
            if (_disposed || IsRunning)
                return false;

            ChapterConfig config = _catalog.FindChapter(chapter);
            if (config == null || config.ArenaCount == 0)
                return false;

            TankConfig tankConfig = _catalog.FindTank(tank);
            _weaponSlots = tankConfig != null ? tankConfig.WeaponSlots : DEFAULT_WEAPON_SLOTS;
            _upgradeSlots = tankConfig != null ? tankConfig.UpgradeSlots : DEFAULT_UPGRADE_SLOTS;

            _chapter = config;
            Chapter = chapter;
            Seed = _random.Next(1, int.MaxValue / 2);
            Reset();
            SetArena(1);

            RunStarted?.Invoke();
            if (_state != RunState.Preparing || _arenaNumber != 1)
                return true; // a listener ended the run

            ShutterChanged?.Invoke(true);
            EnterArena();
            return true;
        }

        public bool MarkArenaReady()
        {
            if (_state != RunState.Preparing || _ready)
                return false;

            _ready = true;
            _timer = 0f;
            return true;
        }

        public bool MarkArenaCleared()
        {
            if (_state != RunState.Fight)
                return false;

            int number = _arenaNumber;
            _state = RunState.Cleared;
            _timer = 0f;
            _victoryPending = number >= ArenaCount;
            _baseMarks += RunRewards.ForArenaClear(number);

            StateChanged?.Invoke(RunState.Cleared);
            ArenaCleared?.Invoke(number);
            UpdateLoot();
            return true;
        }

        public bool MarkGateOpened()
        {
            if (_state != RunState.Cleared || _victoryPending)
                return false;

            _state = RunState.GateOpen;
            StateChanged?.Invoke(RunState.GateOpen);
            GateOpened?.Invoke(_arenaNumber);
            return true;
        }

        public bool MarkArenaLeft()
        {
            if (_state != RunState.GateOpen)
                return false;

            _state = RunState.Leaving;
            _timer = 0f;
            _shutterMoving = false;
            StateChanged?.Invoke(RunState.Leaving);
            ArenaLeft?.Invoke(_arenaNumber);
            return true;
        }

        public void AddExperience(int amount)
        {
            if (!IsRunning || amount <= 0)
                return;

            Experience += amount;
            int need = ExperienceForLevel(Level);
            while (Experience >= need)
            {
                Experience -= need;
                Level++;
                PendingLevelUps++;
                need = ExperienceForLevel(Level);
            }

            if (_offer.Count == 0 && PendingLevelUps > 0)
                OfferNext();
        }

        public bool ChooseSkill(SkillId skill)
        {
            if (!IsRunning || !_offer.Contains(skill))
                return false;

            int level = GetSkillLevel(skill) + 1;
            if (level == 1)
            {
                _skills.Add(skill);
                SkillConfig config = _catalog.FindSkill(skill);
                if (config != null && config.Group == SkillGroup.Weapon)
                    _weaponsTaken++;
                else
                    _upgradesTaken++;
            }

            _skillLevels[skill] = level;
            _offer.Clear();
            PendingLevelUps--;

            SkillChanged?.Invoke(skill, level);

            if (_offer.Count == 0) // a listener may have offered already
            {
                if (PendingLevelUps > 0 && IsRunning)
                    OfferNext();
                else
                    LetPauseGo();
            }

            return true;
        }

        public bool EndRun(RunOutcome outcome)
        {
            if (!IsRunning || outcome == RunOutcome.Victory)
                return false;

            Finish(_victoryPending ? RunOutcome.Victory : outcome);
            return true;
        }

        public bool LeaveToHangar()
        {
            if (!EndRun(RunOutcome.Abandoned))
                return false;

            _leaveToHangar?.Invoke();
            return true;
        }

        /// <summary>Advances the run's timers by game seconds; call it from the game loop (it stands on pause).</summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning || deltaTime <= 0f)
                return;

            TimeSec += deltaTime;

            if (CrushStreak > 0)
            {
                _sinceCrush += deltaTime;
                if (_sinceCrush > _timings.crushStreakSec)
                    CrushStreak = 0;
            }

            _timer += deltaTime;
            switch (_state)
            {
                case RunState.Preparing:
                    TickPreparing();
                    break;
                case RunState.Lull:
                    if (_timer >= _timings.lullSec)
                        EnterFight();
                    break;
                case RunState.Cleared:
                    if (_victoryPending && _timer >= _timings.victoryDelaySec)
                        Finish(RunOutcome.Victory);
                    break;
                case RunState.Leaving:
                    TickLeaving();
                    break;
            }
        }

        /// <summary>Stops hearing deaths and lets a held pause go. The run is no longer usable.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_deaths != null)
                _deaths.Died -= _onDied;
            LetPauseGo();
        }

        // ---------- Arena flow ----------

        private void TickPreparing()
        {
            if (!_shutterMoving)
            {
                if (!_ready && _timer < _timings.readyTimeoutSec)
                    return;

                _ready = true;
                _shutterMoving = true;
                _timer = 0f;
                ShutterChanged?.Invoke(false);
                return;
            }

            if (_timer < _timings.shutterSec)
                return;

            _state = RunState.Lull;
            _timer = 0f;
            StateChanged?.Invoke(RunState.Lull);
        }

        private void TickLeaving()
        {
            if (!_shutterMoving)
            {
                if (_timer < _timings.orbPullSec)
                    return;

                _shutterMoving = true;
                _timer = 0f;
                ShutterChanged?.Invoke(true);
                return;
            }

            if (_timer < _timings.shutterSec)
                return;

            SetArena(_arenaNumber + 1);
            EnterArena();
        }

        private void EnterFight()
        {
            _state = RunState.Fight;
            _timer = 0f;
            StateChanged?.Invoke(RunState.Fight);
            FightStarted?.Invoke(_arenaNumber);
        }

        // The fields of arena n, Preparing, no events
        private void SetArena(int number)
        {
            _arenaNumber = number;
            _arena = _chapter.ArenaAt(number);
            _enemyStrength = (float)(_chapter.EnemyMultiplier * Math.Pow(STRENGTH_PER_ARENA, number - 1));
            _state = RunState.Preparing;
            _timer = 0f;
            _ready = _arenas == null;
            _shutterMoving = false;
            _victoryPending = false;
        }

        // Behind the closed shutter: build, then tell
        private void EnterArena()
        {
            int number = _arenaNumber;
            if (_arenas != null && _arenas.Build(Arena, Seed + number))
                _ready = true;
            if (_state != RunState.Preparing || _arenaNumber != number)
                return;

            StateChanged?.Invoke(RunState.Preparing);
            ArenaStarted?.Invoke(number);
        }

        private void Finish(RunOutcome outcome)
        {
            bool first = false;
            if (outcome == RunOutcome.Victory)
            {
                first = _isChapterCompleted == null || !_isChapterCompleted(Chapter);
                if (first)
                {
                    _bonusMarks = _chapter.FirstCompletionMarks;
                    _bonusSamples = _chapter.FirstCompletionSamples;
                    UpdateLoot();
                }
            }

            int previousBest = _bestArenaOf != null ? _bestArenaOf(Chapter) : 0;

            _state = RunState.Ended;
            _timer = 0f;
            _victoryPending = false;
            _offer.Clear();
            PendingLevelUps = 0;
            LetPauseGo();

            Result = new RunResult
            {
                outcome = outcome,
                chapter = Chapter,
                arenaReached = _arenaNumber,
                timeSec = TimeSec,
                kills = Kills,
                crushes = Crushes,
                elitesKilled = ElitesKilled,
                bossesKilled = _bosses,
                marks = Marks,
                samples = Samples,
                firstCompletion = first,
                levelReached = Level,
                previousBestArena = previousBest,
                newRecord = _arenaNumber > previousBest
            };

            StateChanged?.Invoke(RunState.Ended);
            RunEnded?.Invoke(Result);
        }

        private void Reset()
        {
            TimeSec = 0f;
            Level = 1;
            Experience = 0;
            PendingLevelUps = 0;
            _offer.Clear();
            _skills.Clear();
            _skillLevels.Clear();
            _weaponsTaken = 0;
            _upgradesTaken = 0;
            _baseMarks = 0;
            _baseSamples = 0;
            _bonusMarks = 0;
            _bonusSamples = 0;
            Marks = 0;
            Samples = 0;
            Kills = 0;
            Crushes = 0;
            CrushStreak = 0;
            _sinceCrush = 0f;
            ElitesKilled = 0;
            _bosses = 0;
            Result = default;
        }

        // ---------- Loot ----------

        private void OnDied(EnemyDeath death)
        {
            if (!IsRunning)
                return;

            Kills++;
            if (death.Kind == EnemyKind.Boss)
                _bosses++;
            else if (death.Kind == EnemyKind.EliteRegax || death.Elite)
                ElitesKilled++;

            if (death.Crushed)
            {
                Crushes++;
                CrushStreak = CrushStreak > 0 && _sinceCrush <= _timings.crushStreakSec ? CrushStreak + 1 : 1;
                _sinceCrush = 0f;
            }

            RunRewards.ForDeath(death, out int marks, out int samples);
            _baseMarks += marks;
            _baseSamples += samples;
            UpdateLoot();
        }

        private void UpdateLoot()
        {
            float multiplier = _chapter != null ? _chapter.RewardMultiplier : 1f;
            int marks = Scale(_baseMarks, multiplier) + _bonusMarks;
            int samples = Scale(_baseSamples, multiplier) + _bonusSamples;
            if (marks == Marks && samples == Samples)
                return;

            Marks = marks;
            Samples = samples;
            LootChanged?.Invoke();
        }

        private static int Scale(int baseAmount, float multiplier) =>
            (int)Math.Round(baseAmount * (double)multiplier, MidpointRounding.AwayFromZero);

        // ---------- Skills ----------

        // The next pending level-up with something to offer. A skill screen listening: the pause, LevelUp, the offer, and
        // the wait for its ChooseSkill. Nobody listening (the sandbox has no skill screen): LevelUp, then the first
        // card of the shuffled offer is taken at once — the game must not stand waiting for a choice nobody can make.
        // Nothing to offer — the level-up is used up silently.
        private void OfferNext()
        {
            while (PendingLevelUps > 0 && IsRunning)
            {
                BuildOffer();
                if (_offer.Count == 0)
                {
                    PendingLevelUps--;
                    continue;
                }

                int level = Level - PendingLevelUps + 1;
                if (SkillOffered == null)
                {
                    LevelUp?.Invoke(level);
                    // ChooseSkill offers the next pending level-up itself
                    if (_offer.Count > 0)
                        ChooseSkill(_offer[0]);
                    return;
                }

                HoldPause();
                LevelUp?.Invoke(level);
                SkillOffered?.Invoke(_offer);
                return;
            }

            LetPauseGo();
        }

        private void BuildOffer()
        {
            _offer.Clear();
            _candidates.Clear();

            IReadOnlyList<SkillConfig> skills = _catalog.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillConfig skill = skills[i];
                if (skill == null || !skill.InPool || skill.Id.IsNone || _candidates.Contains(skill.Id))
                    continue;

                int level = GetSkillLevel(skill.Id);
                if (level >= SkillConfig.MAX_LEVEL)
                    continue;

                if (level == 0 && !HasRoom(skill.Group))
                    continue;

                _candidates.Add(skill.Id);
            }

            // Partial Fisher–Yates: the first OFFER_SIZE of a shuffle
            int count = Math.Min(OFFER_SIZE, _candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int j = _random.Next(i, _candidates.Count);
                (_candidates[i], _candidates[j]) = (_candidates[j], _candidates[i]);
                _offer.Add(_candidates[i]);
            }
        }

        private bool HasRoom(SkillGroup group) =>
            group == SkillGroup.Weapon ? _weaponsTaken < _weaponSlots : _upgradesTaken < _upgradeSlots;

        private void HoldPause()
        {
            if (_loop == null || _pausing)
                return;

            _pausing = true;
            _loop.Pause(this);
        }

        private void LetPauseGo()
        {
            if (!_pausing)
                return;

            _pausing = false;
            _loop?.Resume(this);
        }
    }
}

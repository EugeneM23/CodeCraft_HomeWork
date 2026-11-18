using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game
{
    /// <summary>
    /// A stand-in for the core's RunService until it is in the level: installed by <see cref="ProgressionSetup"/> only
    /// when the GameContext has no IRunService (the sandbox, this branch, tests). It keeps what combat needs to be
    /// played: experience and levels (10 + 12·(n − 1)), a queue of level-ups, offers of 3 skills from the milestone's
    /// six (max level 5, 4 weapon slots, 6 upgrade slots), the game loop paused while an offer waits, ChooseSkill →
    /// SkillChanged, EndRun → RunEnded, and kills counted from IEnemyDeaths. No arenas, no shutter, no loot: those
    /// calls answer false and those numbers stay 0. MarkArenaLeft only raises ArenaLeft (to try the orb pull).
    /// </summary>
    public sealed class LocalRunService : IRunService, IDisposable
    {
        public const int WEAPON_SLOTS = 4;
        public const int UPGRADE_SLOTS = 6;
        public const int OFFER_SIZE = 3;

        // Events the stand-in has no reason to raise
#pragma warning disable CS0067
        public event Action RunStarted;
        public event Action<int> ArenaStarted;
        public event Action<int> FightStarted;
        public event Action<int> ArenaCleared;
        public event Action<int> GateOpened;
        public event Action<bool> ShutterChanged;
        public event Action LootChanged;
#pragma warning restore CS0067

        public event Action<int> ArenaLeft;
        public event Action<int> LevelUp;
        public event Action<SkillId, int> SkillChanged;
        public event Action<RunState> StateChanged;
        public event Action<RunResult> RunEnded;

        private readonly SkillId[] _pool;
        private readonly SkillGroup[] _groups;
        private readonly System.Random _random;
        private readonly IEnemyDeaths _deaths;

        private readonly List<SkillId> _offer = new(OFFER_SIZE);
        private readonly List<SkillId> _skills = new();
        private readonly List<int> _skillLevels = new();
        private readonly List<SkillId> _candidates = new();

        private float _startTime;
        private bool _pausing;
        private SkillOfferDebugView _view;
        private bool _disposed;

        /// <summary>The milestone's six skills.</summary>
        public LocalRunService(IEnemyDeaths deaths = null, int seed = 0)
            : this(SkillEffectTable.MilestoneSkills, GroupsOf(SkillEffectTable.MilestoneSkills), deaths, seed)
        {
        }

        /// <param name="pool">Skills that can be offered.</param>
        /// <param name="groups">The group of each skill of the pool, same order.</param>
        public LocalRunService(SkillId[] pool, SkillGroup[] groups, IEnemyDeaths deaths = null, int seed = 0)
        {
            if (pool == null || groups == null || pool.Length != groups.Length)
                throw new ArgumentException("A group for every skill of the pool");

            _pool = pool;
            _groups = groups;
            Seed = seed != 0 ? seed : Environment.TickCount;
            _random = new System.Random(Seed);
            _deaths = deaths;
            if (_deaths != null)
                _deaths.Died += OnDied;

            Begin();
        }

        // ---------- State ----------

        public RunState State { get; private set; }
        public bool IsRunning => State != RunState.None && State != RunState.Ended;
        public ChapterId Chapter { get; private set; }
        public int Seed { get; }
        public int ArenaNumber => 1;
        public int ArenaCount => 1;
        public ArenaId Arena => default;
        public bool IsEliteArena => false;
        public float EnemyStrength => 1f;
        public float TimeSec => (State == RunState.Ended ? Result.timeSec : GameTicks.Time - _startTime);
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int ExperienceToNextLevel => ExperienceFor(Level);
        public int PendingLevelUps { get; private set; }
        public IReadOnlyList<SkillId> SkillOffer => _offer;
        public IReadOnlyList<SkillId> Skills => _skills;
        public int Marks => 0;
        public int Samples => 0;
        public int Kills { get; private set; }
        public int Crushes { get; private set; }
        public int CrushStreak => 0;
        public int ElitesKilled { get; private set; }
        public RunResult Result { get; private set; }

        /// <summary>Bumped on every change a view shows (experience, level, offer, skills, state).</summary>
        public int Revision { get; private set; }

        /// <summary>Experience level <paramref name="level"/> needs to go on: 10 + 12·(n − 1).</summary>
        public static int ExperienceFor(int level) => 10 + 12 * (Mathf.Max(1, level) - 1);

        public int GetSkillLevel(SkillId skill)
        {
            int index = _skills.IndexOf(skill);
            return index >= 0 ? _skillLevels[index] : 0;
        }

        // ---------- Commands ----------

        public bool StartRun(ChapterId chapter, TankId tank)
        {
            Chapter = chapter;
            Begin();
            RunStarted?.Invoke();
            StateChanged?.Invoke(State);
            return true;
        }

        public bool MarkArenaReady() => false;

        public bool MarkArenaCleared() => false;

        public bool MarkGateOpened() => false;

        public bool MarkArenaLeft()
        {
            if (!IsRunning)
                return false;

            ArenaLeft?.Invoke(ArenaNumber);
            return true;
        }

        public void AddExperience(int amount)
        {
            if (!IsRunning || amount <= 0)
                return;

            Experience += amount;
            int gained = 0;
            while (Experience >= ExperienceToNextLevel)
            {
                Experience -= ExperienceToNextLevel;
                Level++;
                gained++;
            }

            Revision++;
            if (gained == 0)
                return;

            bool offerShown = _offer.Count > 0;
            PendingLevelUps += gained;
            if (!offerShown)
                OfferNext();
        }

        public bool ChooseSkill(SkillId skill)
        {
            if (!IsRunning || _offer.Count == 0 || !_offer.Contains(skill))
                return false;

            int index = _skills.IndexOf(skill);
            int level;
            if (index < 0)
            {
                _skills.Add(skill);
                _skillLevels.Add(1);
                level = 1;
            }
            else
            {
                level = Mathf.Min(_skillLevels[index] + 1, SkillConfig.MAX_LEVEL);
                _skillLevels[index] = level;
            }

            PendingLevelUps--;
            _offer.Clear();
            Revision++;
            SkillChanged?.Invoke(skill, level);

            OfferNext();
            return true;
        }

        public bool EndRun(RunOutcome outcome)
        {
            if (!IsRunning)
                return false;

            Result = new RunResult
            {
                outcome = outcome,
                chapter = Chapter,
                arenaReached = ArenaNumber,
                timeSec = GameTicks.Time - _startTime,
                kills = Kills,
                crushes = Crushes,
                elitesKilled = ElitesKilled,
                levelReached = Level
            };

            State = RunState.Ended;
            _offer.Clear();
            PendingLevelUps = 0;
            ReleasePause();
            Revision++;

            StateChanged?.Invoke(State);
            RunEnded?.Invoke(Result);
            return true;
        }

        public bool LeaveToHangar() => EndRun(RunOutcome.Abandoned);

        // ---------- Debug view ----------

        /// <summary>The IMGUI skill cards and XP line, until the menu's skill screen. Play mode only.</summary>
        public void ShowDebugView()
        {
            if (_view != null || _disposed || !Application.isPlaying)
                return;

            var host = new GameObject(nameof(SkillOfferDebugView));
            _view = host.AddComponent<SkillOfferDebugView>();
            _view.Bind(this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_deaths != null)
                _deaths.Died -= OnDied;
            ReleasePause();

            if (_view != null)
                Object.Destroy(_view.gameObject);
            _view = null;
        }

        // ---------- Inside ----------

        private void Begin()
        {
            State = RunState.Fight;
            Level = 1;
            Experience = 0;
            PendingLevelUps = 0;
            Kills = Crushes = ElitesKilled = 0;
            Result = default;
            _offer.Clear();
            _skills.Clear();
            _skillLevels.Clear();
            _startTime = GameTicks.Time;
            ReleasePause();
            Revision++;
        }

        /// <summary>Shows the next pending level-up's cards, or resumes the game when none is left.</summary>
        private void OfferNext()
        {
            while (PendingLevelUps > 0)
            {
                BuildOffer();
                if (_offer.Count > 0)
                {
                    HoldPause();
                    Revision++;
                    LevelUp?.Invoke(Level - PendingLevelUps + 1);
                    return;
                }

                // Every skill is maxed or no slot is left: nothing to choose, the level-up passes
                PendingLevelUps--;
            }

            ReleasePause();
            Revision++;
        }

        private void BuildOffer()
        {
            _offer.Clear();
            _candidates.Clear();

            int weapons = 0;
            int upgrades = 0;
            for (int i = 0; i < _skills.Count; i++)
            {
                if (GroupOf(_skills[i]) == SkillGroup.Weapon)
                    weapons++;
                else
                    upgrades++;
            }

            for (int i = 0; i < _pool.Length; i++)
            {
                SkillId skill = _pool[i];
                int level = GetSkillLevel(skill);
                if (level >= SkillConfig.MAX_LEVEL)
                    continue;

                bool slotFree = _groups[i] == SkillGroup.Weapon ? weapons < WEAPON_SLOTS : upgrades < UPGRADE_SLOTS;
                if (level > 0 || slotFree)
                    _candidates.Add(skill);
            }

            // A partial Fisher–Yates: the first OFFER_SIZE of the shuffle
            int count = Mathf.Min(OFFER_SIZE, _candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = _random.Next(i, _candidates.Count);
                (_candidates[i], _candidates[pick]) = (_candidates[pick], _candidates[i]);
                _offer.Add(_candidates[i]);
            }
        }

        private SkillGroup GroupOf(SkillId skill)
        {
            int index = Array.IndexOf(_pool, skill);
            return index >= 0 ? _groups[index] : DefaultGroupOf(skill);
        }

        private void HoldPause()
        {
            if (_pausing)
                return;

            _pausing = true;
            GameTicks.Loop?.Pause(this);
        }

        private void ReleasePause()
        {
            if (!_pausing)
                return;

            _pausing = false;
            GameTicks.Loop?.Resume(this);
        }

        private void OnDied(EnemyDeath death)
        {
            if (!IsRunning)
                return;

            Kills++;
            if (death.Crushed)
                Crushes++;
            if (death.Elite)
                ElitesKilled++;
        }

        private static SkillGroup[] GroupsOf(SkillId[] skills)
        {
            var groups = new SkillGroup[skills.Length];
            for (int i = 0; i < skills.Length; i++)
                groups[i] = DefaultGroupOf(skills[i]);
            return groups;
        }

        /// <summary>The group of a skill of GDD §3 (SkillIds), as the catalog has it.</summary>
        public static SkillGroup DefaultGroupOf(SkillId skill)
        {
            if (skill == SkillIds.Turret || skill == SkillIds.SideFlamers || skill == SkillIds.Mortar
                || skill == SkillIds.Rockets || skill == SkillIds.RearMines || skill == SkillIds.AirStrike)
                return SkillGroup.Weapon;

            if (skill == SkillIds.Ricochet || skill == SkillIds.DoubleShot || skill == SkillIds.Piercing
                || skill == SkillIds.BigCaliber || skill == SkillIds.RateOfFire || skill == SkillIds.Shrapnel)
                return SkillGroup.Gun;

            return SkillGroup.Tank;
        }
    }
}

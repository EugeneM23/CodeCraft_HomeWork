using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// IRunHudSource over the real run: the core's IRunService (the level's GameContext), the arena service
    /// (gate, hints), the AI's EliteStatus (elite health), the combat team's ISpecialModule (E) on the tank and
    /// the core's GameCatalog (weapon or upgrade). Made by AppServices.RunHud for the run of the open level and
    /// released by AppServices.ReleaseRunHud when the level's battle UI goes.
    /// Every event is forwarded once; the lists are rebuilt only when a skill changes or an offer comes.
    /// Skill offers come from the core's IRunService.SkillOffered, and the adapter listens to it only while
    /// someone (the battle UI's skill choice) listens to its own SkillOffered: the core pauses a level-up and
    /// waits for ChooseSkill only while it has a listener. With no skill screen (the sandbox) the run takes a
    /// skill by itself and nothing stands.
    /// The arena service is taken from the GameContext on each use (a key lookup, it may be installed after
    /// this adapter is made); the special module is taken again whenever the player's character changes.
    /// </summary>
    public sealed class RunServiceHudSource : IRunHudSource, IDisposable
    {
        public event Action Changed;
        public event Action<RunSummary> RunEnded;

        /// <summary>
        /// The first listener makes the adapter listen to the core's SkillOffered (a level-up then waits for
        /// ChooseSkill, the game paused); with the last one gone the run takes its skills by itself again.
        /// </summary>
        public event Action<IReadOnlyList<SkillOffer>> SkillOffered
        {
            add
            {
                if (value == null || _disposed)
                    return;
                bool first = _skillOffered == null;
                _skillOffered += value;
                if (first)
                    ListenToOffers(true);
            }
            remove
            {
                if (value == null || _skillOffered == null)
                    return;
                _skillOffered -= value;
                if (_skillOffered == null)
                    ListenToOffers(false);
            }
        }

        private readonly IRunService _run;
        private readonly GameCatalog _catalog;
        private readonly List<RunSkill> _skills = new(10);
        private readonly List<SkillOffer> _offers = new(3);
        private readonly Action<int> _onArenaEvent;
        private readonly Action<int> _onLevelUp;
        private readonly Action<IReadOnlyList<SkillId>> _onCoreOffer;
        private readonly Action<SkillId, int> _onSkillChanged;
        private readonly Action _onChanged;
        private readonly Action<RunResult> _onRunEnded;
        private readonly Action<float> _onEliteHealth;
        private readonly Action<IEntity> _onCharacterChanged;

        private IGameContext _context;
        private Subscription<IEntity> _characterSubscription;
        private ISpecialModule _special;
        private Action<IReadOnlyList<SkillOffer>> _skillOffered;
        private bool _listeningToOffers;
        private bool _disposed;

        public IRunService Run => _run;

        public RunServiceHudSource(IRunService run, GameCatalog catalog, IGameContext context)
        {
            _run = run;
            _catalog = catalog;
            _context = context;

            _onArenaEvent = _ => RaiseChanged();
            _onLevelUp = _ => RaiseChanged(); // the level number; the offer comes with SkillOffered
            _onCoreOffer = HandleCoreOffer;
            _onSkillChanged = (_, _) => RebuildSkills();
            _onChanged = RaiseChanged;
            _onRunEnded = HandleRunEnded;
            _onEliteHealth = _ => RaiseChanged();
            _onCharacterChanged = HandleCharacterChanged;

            _run.ArenaStarted += _onArenaEvent;
            _run.ArenaCleared += _onArenaEvent;
            _run.GateOpened += _onArenaEvent;
            _run.LevelUp += _onLevelUp;
            _run.SkillChanged += _onSkillChanged;
            _run.LootChanged += _onChanged;
            _run.RunEnded += _onRunEnded;
            EliteStatus.Appeared += _onChanged;
            EliteStatus.HealthChanged += _onEliteHealth;
            EliteStatus.Died += _onChanged;

            // Observe gives the current character at once, then each change (the tank, on foot, a new tank).
            if (_context != null && _context.TryGetPlayerContext(out PlayerContext player)
                && player.TryGetCharacter(out IReactiveVariable<IEntity> character))
                _characterSubscription = character.Observe(_onCharacterChanged);

            RebuildSkills();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ListenToOffers(false);
            _skillOffered = null;
            _run.ArenaStarted -= _onArenaEvent;
            _run.ArenaCleared -= _onArenaEvent;
            _run.GateOpened -= _onArenaEvent;
            _run.LevelUp -= _onLevelUp;
            _run.SkillChanged -= _onSkillChanged;
            _run.LootChanged -= _onChanged;
            _run.RunEnded -= _onRunEnded;
            EliteStatus.Appeared -= _onChanged;
            EliteStatus.HealthChanged -= _onEliteHealth;
            EliteStatus.Died -= _onChanged;
            _characterSubscription.Dispose();
            _characterSubscription = default;
            _context = null;
            _special = null;
        }

        public int ArenaIndex => _run.ArenaNumber;
        public int ArenaCount => _run.ArenaCount;
        public ArenaHint ArenaHints => TryGetArena(out IArenaService arena) && arena.Definition != null ? arena.Definition.Hints : 0;

        public int Level => _run.Level;
        public int Xp => _run.Experience;
        public int XpToNextLevel => _run.ExperienceToNextLevel;

        public int Marks => _run.Marks;
        public int Samples => _run.Samples;

        public int Kills => _run.Kills;
        public int Crushes => _run.Crushes;
        public int CrushStreak => _run.CrushStreak;
        public int ElitesKilled => _run.ElitesKilled;
        public float ElapsedTime => _run.TimeSec;

        public bool EliteAlive => EliteStatus.IsAlive;
        public float EliteHealth01 => EliteStatus.Health01;

        public bool GateOpen => TryGetArena(out IArenaService arena) && arena.IsGateOpen;
        public Vector3 GatePosition => TryGetArena(out IArenaService arena) ? arena.GatePosition : Vector3.zero;

        public IReadOnlyList<RunSkill> Skills => _skills;

        /// <summary>
        /// The offer of the pending level-up (the core keeps it until ChooseSkill); empty when none. Read from the
        /// core each time, so an offer made before the skill screen subscribed is shown as well.
        /// </summary>
        public IReadOnlyList<SkillOffer> PendingOffer
        {
            get
            {
                if (_run.PendingLevelUps <= 0 || _run.SkillOffer.Count == 0)
                    return Array.Empty<SkillOffer>();

                BuildOffers(_run.SkillOffer);
                return _offers;
            }
        }

        public float Special01
        {
            get
            {
                if (_special == null)
                    return -1f;
                return _special.IsReady ? 1f : Mathf.Clamp01(_special.Charge01);
            }
        }

        public void ChooseSkill(int index)
        {
            if (index < 0 || index >= _offers.Count || _run.SkillOffer.Count == 0)
                return;

            // The core raises SkillOffered again from inside ChooseSkill when another level-up is pending; the
            // battle UI holds that offer until the current choice has left the screen.
            _run.ChooseSkill(new SkillId(_offers[index].Id));
        }

        /// <summary>The core's LeaveToHangar ends the run as Abandoned and goes to the hangar itself.</summary>
        public void ExitToHangar() => _run.LeaveToHangar();

        private void ListenToOffers(bool listen)
        {
            if (listen == _listeningToOffers)
                return;

            _listeningToOffers = listen;
            if (listen)
                _run.SkillOffered += _onCoreOffer;
            else
                _run.SkillOffered -= _onCoreOffer;
        }

        // The core has paused the game for this level-up and waits for ChooseSkill.
        private void HandleCoreOffer(IReadOnlyList<SkillId> offer)
        {
            BuildOffers(offer);
            if (_offers.Count > 0)
                _skillOffered?.Invoke(_offers);
            RaiseChanged();
        }

        private void BuildOffers(IReadOnlyList<SkillId> offer)
        {
            _offers.Clear();
            for (int i = 0; i < offer.Count; i++)
            {
                int level = _run.GetSkillLevel(offer[i]);
                _offers.Add(new SkillOffer(offer[i].Value, level, level + 1, IsWeapon(offer[i])));
            }
        }

        // Weapons first, then the upgrades, each in the order taken
        private void RebuildSkills()
        {
            _skills.Clear();
            IReadOnlyList<SkillId> taken = _run.Skills;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < taken.Count; i++)
                {
                    bool weapon = IsWeapon(taken[i]);
                    if (weapon == (pass == 0))
                        _skills.Add(new RunSkill(taken[i].Value, _run.GetSkillLevel(taken[i]), weapon));
                }
            }
            RaiseChanged();
        }

        private bool IsWeapon(SkillId id)
        {
            SkillConfig config = _catalog != null ? _catalog.FindSkill(id) : null;
            return config != null && config.Group == SkillGroup.Weapon;
        }

        // A key lookup on the GameContext, no allocation: the arena team may install its service after this adapter.
        private bool TryGetArena(out IArenaService arena)
        {
            arena = null;
            return _context != null && _context.TryGetArena(out arena) && arena != null;
        }

        // The module of the current character; none on foot or without a character (Special01 hides it).
        private void HandleCharacterChanged(IEntity character)
        {
            _special = null;
            if (character != null)
                character.TryGetSpecialModule(out _special);
            RaiseChanged();
        }

        private void HandleRunEnded(RunResult result)
        {
            RunEnded?.Invoke(new RunSummary
            {
                Victory = result.outcome == RunOutcome.Victory,
                Abandoned = result.outcome == RunOutcome.Abandoned,
                ChapterId = result.chapter.ToString(),
                ArenaReached = result.arenaReached,
                ArenaCount = _run.ArenaCount,
                Time = result.timeSec,
                Kills = result.kills,
                Crushes = result.crushes,
                ElitesKilled = result.elitesKilled,
                Marks = result.marks,
                Samples = result.samples,
                NewRecord = result.newRecord
            });
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}

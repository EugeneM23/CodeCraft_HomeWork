using System;
using System.Collections.Generic;
using Atomic.Entities;
using Game.Arenas;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// IRunHudSource over the real run: the core's IRunService (the level's GameContext), the arena service
    /// (gate, hints), the AI's EliteStatus (elite health), the combat team's ISpecialModule (E) on the tank and
    /// the core's GameCatalog (weapon or upgrade). Made by AppServices.RunHud for the run of the open level.
    /// Every event is forwarded once; the lists are rebuilt only when a skill changes or a level-up comes.
    /// </summary>
    public sealed class RunServiceHudSource : IRunHudSource, IDisposable
    {
        public event Action Changed;
        public event Action<IReadOnlyList<SkillOffer>> SkillOffered;
        public event Action<RunSummary> RunEnded;

        private readonly IRunService _run;
        private readonly IArenaService _arena;
        private readonly GameCatalog _catalog;
        private readonly List<RunSkill> _skills = new(10);
        private readonly List<SkillOffer> _offers = new(3);
        private readonly Action<int> _onArenaEvent;
        private readonly Action<int> _onLevelUp;
        private readonly Action<SkillId, int> _onSkillChanged;
        private readonly Action _onChanged;
        private readonly Action<RunResult> _onRunEnded;
        private readonly Action<float> _onEliteHealth;

        private IGameContext _context;
        private ISpecialModule _special;

        public IRunService Run => _run;

        public RunServiceHudSource(IRunService run, IArenaService arena, GameCatalog catalog, IGameContext context)
        {
            _run = run;
            _arena = arena;
            _catalog = catalog;
            _context = context;

            _onArenaEvent = _ => RaiseChanged();
            _onLevelUp = _ => OfferSkills();
            _onSkillChanged = (_, _) => RebuildSkills();
            _onChanged = RaiseChanged;
            _onRunEnded = HandleRunEnded;
            _onEliteHealth = _ => RaiseChanged();

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

            RebuildSkills();
            if (_run.PendingLevelUps > 0)
                OfferSkills();
        }

        public void Dispose()
        {
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
            _context = null;
            _special = null;
        }

        public int ArenaIndex => _run.ArenaNumber;
        public int ArenaCount => _run.ArenaCount;
        public ArenaHint ArenaHints => _arena != null && _arena.Definition != null ? _arena.Definition.Hints : 0;

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

        public bool GateOpen => _arena != null && _arena.IsGateOpen;
        public Vector3 GatePosition => _arena != null ? _arena.GatePosition : Vector3.zero;

        public IReadOnlyList<RunSkill> Skills => _skills;

        public float Special01
        {
            get
            {
                ISpecialModule special = Special();
                if (special == null)
                    return -1f;
                return special.IsReady ? 1f : Mathf.Clamp01(special.Charge01);
            }
        }

        public void ChooseSkill(int index)
        {
            if (index < 0 || index >= _offers.Count)
                return;

            _run.ChooseSkill((SkillId)_offers[index].Id);
            // The next pending level-up, if any, offers again right away (the core keeps the queue).
            if (_run.PendingLevelUps > 0 && _run.SkillOffer.Count > 0)
                OfferSkills();
        }

        public void ExitToHangar() => _run.LeaveToHangar();

        private void OfferSkills()
        {
            _offers.Clear();
            IReadOnlyList<SkillId> offer = _run.SkillOffer;
            for (int i = 0; i < offer.Count; i++)
            {
                int level = _run.GetSkillLevel(offer[i]);
                _offers.Add(new SkillOffer(offer[i].Value, level, level + 1, IsWeapon(offer[i])));
            }

            if (_offers.Count > 0)
                SkillOffered?.Invoke(_offers);
            RaiseChanged();
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

        private ISpecialModule Special()
        {
            if (_special != null || _context == null)
                return _special;

            if (_context is GameContext game && game.TryGetPlayerContext(out PlayerContext player)
                && player.TryGetCharacter(out var character) && character.Value is IEntity tank)
                tank.TryGetSpecialModule(out _special);
            return _special;
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

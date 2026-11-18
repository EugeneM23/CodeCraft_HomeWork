using System;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// On the player's tank: turns the run's skills into its <see cref="CombatModifiers"/>. At init applies the levels
    /// the run already has (a tank attached mid-run), then follows IRunService.SkillChanged. A new run
    /// (IRunService.RunStarted) clears the skills without announcing level 0 for each: the modifiers are reset and the
    /// run's current levels applied again. Added and removed by <see cref="ProgressionSetup"/>.
    /// </summary>
    public sealed class SkillEffectsBehaviour : IEntityInit, IEntityDispose
    {
        private readonly IRunService _run;
        private readonly SkillEffectTable _table;
        private CombatModifiers _modifiers;
        private Action _onRunStarted;
        private bool _subscribed;

        public SkillEffectsBehaviour(IRunService run, SkillEffectTable table)
        {
            _run = run;
            _table = table;
        }

        public void Init(IEntity entity)
        {
            if (_run == null || !entity.TryGetCombatModifiers(out _modifiers) || _modifiers == null)
                return;

            ApplyAll();

            _onRunStarted = OnRunStarted;
            _run.SkillChanged += OnSkillChanged;
            _run.RunStarted += _onRunStarted;
            _subscribed = true;
        }

        public void Dispose(IEntity entity)
        {
            if (_subscribed)
            {
                _run.SkillChanged -= OnSkillChanged;
                _run.RunStarted -= _onRunStarted;
            }

            _subscribed = false;
        }

        private void OnSkillChanged(SkillId skill, int level)
        {
            if (SkillEffectsUseCase.Apply(_modifiers, skill, level, _table))
                _modifiers.RaiseChanged();
        }

        private void OnRunStarted()
        {
            _modifiers.Reset();
            ApplyAll();
        }

        /// <summary>Every milestone skill at the run's current level, then one Changed.</summary>
        private void ApplyAll()
        {
            SkillId[] skills = SkillEffectTable.MilestoneSkills;
            for (int i = 0; i < skills.Length; i++)
                SkillEffectsUseCase.Apply(_modifiers, skills[i], _run.GetSkillLevel(skills[i]), _table);
            _modifiers.RaiseChanged();
        }
    }
}

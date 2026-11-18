using System;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Keeps the player's tank on its loadout stats with the run skills over them: applies them now and again
    /// whenever <see cref="CombatModifiers.Changed"/> fires (LoadoutApplyUseCase). Made by <see cref="TankModulesSetup"/>.
    /// </summary>
    public sealed class TankStatsApplier : IDisposable
    {
        private readonly IEntity _tank;
        private readonly CombatModifiers _modifiers;
        private TankCombatStats _stats;

        public TankStatsApplier(IEntity tank, CombatModifiers modifiers, TankCombatStats stats)
        {
            _tank = tank;
            _modifiers = modifiers;
            _stats = stats;
            if (_modifiers != null)
                _modifiers.Changed += Apply;
            Apply();
        }

        public TankCombatStats Stats => _stats;

        /// <summary>A new loadout (the run started with another one): applied at once.</summary>
        public void SetStats(TankCombatStats stats)
        {
            _stats = stats;
            Apply();
        }

        public void Apply() => LoadoutApplyUseCase.Apply(_tank, _stats, _modifiers);

        public void Dispose()
        {
            if (_modifiers != null)
                _modifiers.Changed -= Apply;
        }
    }
}

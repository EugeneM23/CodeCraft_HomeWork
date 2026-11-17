using System;
using Atomic.Elements;

namespace Game
{
    public sealed class RestoreManaBehaviour : IPlayerContextInit, IPlayerContextFixedTick
    {
        private readonly CooldownTimer _period;
        private readonly IValue<int> _restore;

        private IValue<int> _maxMana;
        private IVariable<int> _currentMana;

        public RestoreManaBehaviour(CooldownTimer period, IValue<int> restore)
        {
            _period = period;
            _restore = restore;
        }

        public void Init(IPlayerContext context)
        {
            _currentMana = context.GetCurrentMana();
            _maxMana = context.GetMaxMana();
        }

        public void FixedTick(IPlayerContext context, float deltaTime)
        {
            int maxMana = _maxMana.Value;

            // >=, not ==: a restore amount that does not divide the missing mana exactly used to
            // step straight past the maximum, after which the equality never held again and mana
            // grew without bound.
            if (_currentMana.Value >= maxMana)
                return;

            _period.Tick(deltaTime);
            if (!_period.IsExpired())
                return;

            _currentMana.Value = Math.Min(_currentMana.Value + _restore.Value, maxMana);
            _period.Reset();
        }
    }
}
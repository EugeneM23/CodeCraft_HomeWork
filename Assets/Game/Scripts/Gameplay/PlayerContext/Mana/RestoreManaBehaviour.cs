using System;
using Atomic.Elements;
using Modules.Gameplay;
using Cooldown = Modules.Gameplay.Cooldown;

namespace Game
{
    public sealed class RestoreManaBehaviour : IPlayerContextInit, IPlayerContextFixedTick
    {
        private readonly Cooldown _period;
        private readonly IValue<int> _restore;

        private IValue<int> _maxMana;
        private IVariable<int> _currentMana;

        public RestoreManaBehaviour(Cooldown period, IValue<int> restore)
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
            if (_currentMana.Value == _maxMana.Value)
                return;
            
            _period.Tick(deltaTime);
            if (!_period.IsExpired())
                return;

            _currentMana.Value += _restore.Value;
            _period.Reset();
        }
    }
}
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class MeleeAttackAction : IAction
    {
        private readonly IEntity _character;
        private IReactiveVariable<IEntity> _target;

        public MeleeAttackAction(IEntity character)
        {
            _character = character;
            _target = character.GetTarget();
        }

        public void Invoke()
        {
            if (_target.Value == null) return;

            _target.Value.GetHealth().Reduce(_character.GetDamage().Value);
        }
    }
}
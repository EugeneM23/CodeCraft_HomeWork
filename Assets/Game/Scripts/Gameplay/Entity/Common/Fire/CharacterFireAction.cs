using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterFireAction : IAction
    {
        private readonly int _attack = Animator.StringToHash("Attack");
        private readonly IEntity _character;

        public CharacterFireAction(IEntity character)
        {
            _character = character;
        }

        public void Invoke()
        {
            IEntity weapon = _character.GetWeapon().Value;

            if (!weapon.GetFireCondition().Invoke()) return;

            weapon.GetFireAction().Invoke();
            _character.GetAnimator().SetTrigger(_attack);
        }
    }
}
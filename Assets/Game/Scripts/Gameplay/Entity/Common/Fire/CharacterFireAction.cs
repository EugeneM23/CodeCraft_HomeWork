using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterFireAction : IAction
    {
        private readonly int _fire = Animator.StringToHash("Fire");
        private readonly int _melee = Animator.StringToHash("Melee");
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

            if (weapon.HasRangeWeaponTag())
            {
                Debug.Log("asdsad");
                _character.GetAnimator().Play(_fire, 1);
            }

            if (weapon.HasMeleeWeaponTag())
                _character.GetAnimator().Play(_melee, 2);
        }
    }
}
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
        private readonly IReactiveVariable<IEntity> _weapon;

        public CharacterFireAction(IEntity character)
        {
            _character = character;
            _weapon = _character.GetWeapon();
        }

        public void Invoke()
        {
            IEntity weapon = _character.GetWeapon().Value;

            if (!_weapon.Value.GetFireCondition().Invoke()) return;

            weapon.GetFireAction().Invoke();

            if (_weapon.Value.HasRangeWeaponTag()) 
                _character.GetAnimator().Play(_fire, 1);

            if (_weapon.Value.HasMeleeWeaponTag())
                _character.GetAnimator().Play(_melee, 2);
        }
    }
}
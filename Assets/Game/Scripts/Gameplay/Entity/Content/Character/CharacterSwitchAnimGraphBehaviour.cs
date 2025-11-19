using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterSwitchAnimGraphBehaviour : IEntityInit
    {
        private IReactiveVariable<IEntity> _weapom;

        private Animator _animator;
        private readonly RuntimeAnimatorController _fistController;
        private readonly RuntimeAnimatorController _weaponController;

        public CharacterSwitchAnimGraphBehaviour(RuntimeAnimatorController fistController,
            RuntimeAnimatorController weaponController)
        {
            _fistController = fistController;
            _weaponController = weaponController;
        }

        public void Init(in IEntity entity)
        {
            _weapom = entity.GetWeapon();
            _animator = entity.GetAnimator();
            _weapom.Observe(OnWeaponChanged);
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            if (weapon == null)
            {
                _animator.runtimeAnimatorController = _fistController;
            }
            else
            {
                _animator.runtimeAnimatorController = _weaponController;
            }
        }
    }
}
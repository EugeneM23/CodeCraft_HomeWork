using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class WeaponAnimBehaviour : IEntityInit, IEntityDispose, IEntityUpdate
    {
        private Animator _animator;

        private RuntimeAnimatorController _weaponAnimator;
        private RuntimeAnimatorController _fistAnimator;

        private IEntity _character;

        public void Init(in IEntity entity)
        {
            _character = entity;

            _animator = entity.GetAnimator();
            _weaponAnimator = entity.GetWeaponAnimator();
            _fistAnimator = entity.GetFistAnimator();

            entity.GetWeapon().Observe(OnWeaponChanged);
        }

        public void Dispose(in IEntity entity)
        {
            entity.GetWeapon().Unsubscribe(OnWeaponChanged);
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            _animator.runtimeAnimatorController = _character.GetWeapon() == null ? _fistAnimator : _weaponAnimator;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            _animator.runtimeAnimatorController = _character.GetWeapon() == null ? _fistAnimator : _weaponAnimator;
        }
    }
}
using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class SwitchAnimatorBehaviour : IEntityInit, IEntityDispose
    {
        private Animator _animator;
        private IReactiveVariable<IEntity> _weapon;

        public void Init(in IEntity entity)
        {
            _animator = entity.GetAnimator();
            _weapon = entity.GetWeapon();
            _weapon.Observe(OnWeaponChanged);
        }

        public void Dispose(in IEntity entity) => _weapon.Unsubscribe(OnWeaponChanged);

        private void OnWeaponChanged(IEntity weapon) => SwitchAnimatorUseCase.Switch(_animator, weapon);
    }
}
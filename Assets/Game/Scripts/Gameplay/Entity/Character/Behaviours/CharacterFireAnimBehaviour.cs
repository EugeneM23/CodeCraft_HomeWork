using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterFireAnimBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int _fire = Animator.StringToHash("Attack");

        private Animator _animator;
        private BaseEvent _fireEvent;
        private IReactiveVariable<IEntity> _weapon;

        public void Init(in IEntity entity)
        {
            _weapon = entity.GetWeapon();
            _animator = entity.GetAnimator();
            _fireEvent = _weapon.Value.GetFireEvent();
        }

        private void OnWeaponChanged(IEntity weapon)
        {
            
        }

        private void OnFire()
        {
            _animator.SetTrigger(_fire);
        }

        public void Dispose(in IEntity entity)
        {
            _fireEvent.Unsubscribe(OnFire);
        }
    }
}
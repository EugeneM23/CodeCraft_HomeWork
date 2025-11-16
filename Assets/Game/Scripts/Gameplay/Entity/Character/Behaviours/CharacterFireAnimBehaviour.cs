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

        public void Init(in IEntity entity)
        {
            _animator = entity.GetAnimator();
            _fireEvent = entity.GetWeapon().Value.GetFireEvent();
            _fireEvent.Subscribe(OnFire);
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
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class MoveAnimBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int _ismoving = Animator.StringToHash("IsMoving");

        private Animator _animator;
        private IReactiveValue<Vector3> _moveDirection;

        public void Init(in IEntity entity)
        {
            _animator = entity.GetAnimator();
            _moveDirection = entity.GetMoveDirection();
            _moveDirection.Observe(OnMove);
        }

        private void OnMove(Vector3 direction)
        {
            _animator.SetBool(_ismoving, direction != Vector3.zero);
        }

        public void Dispose(in IEntity entity)
        {
        }
    }
}
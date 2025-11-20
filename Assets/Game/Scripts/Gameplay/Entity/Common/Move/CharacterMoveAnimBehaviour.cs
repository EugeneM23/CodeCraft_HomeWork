using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterMoveAnimBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int IS_MOVING = Animator.StringToHash("IsMoving");
        private Animator _animator;
        private IReactiveVariable<Vector3> _moveDirection;
        private IEntity _character;

        public void Init(in IEntity entity)
        {
            _character = entity;
            _animator = entity.GetAnimator();
            _moveDirection = entity.GetMoveDirection();
            _moveDirection.Observe(OnMoveDirectionChanged);
        }

        public void Dispose(in IEntity entity)
        {
            _moveDirection.Unsubscribe(OnMoveDirectionChanged);
        }

        private void OnMoveDirectionChanged(Vector3 direction)
        {
            if (direction == Vector3.zero || !_character.GetMoveCondition().Invoke())
                _animator.SetBool(IS_MOVING, false);
            else
                _animator.SetBool(IS_MOVING, true);
        }
    }
}
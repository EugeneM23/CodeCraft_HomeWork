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
        private IReactiveVariable<Vector3> _rotateDirection;
        private IEntity _character;
        private Transform _characterTransform;

        public void Init(in IEntity entity)
        {
            _rotateDirection = entity.GetRotateDirection();
            _characterTransform = entity.GetTransform();
            _character = entity;
            _animator = entity.GetAnimator();
            _moveDirection = entity.GetMoveDirection();

            _moveDirection.Observe(OnDirectionChanged);
            _rotateDirection.Observe(OnDirectionChanged);
        }

        public void Dispose(in IEntity entity)
        {
            _moveDirection.Unsubscribe(OnDirectionChanged);
            _rotateDirection.Unsubscribe(OnDirectionChanged);
        }

        private void OnDirectionChanged(Vector3 _)
        {
            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            Vector3 direction = _moveDirection.Value;

            if (direction == Vector3.zero)
            {
                _animator.SetBool(IS_MOVING, false);
                return;
            }

            if (_character.GetMoveCondition().Invoke())
            {
                Vector3 rotateDir = _rotateDirection.Value;

                if (rotateDir != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(rotateDir);
                    Vector3 localDirection = Quaternion.Inverse(targetRotation) * direction;

                    _animator.SetBool(IS_MOVING, true);
                    _animator.SetFloat("MoveX", localDirection.x);
                    _animator.SetFloat("MoveY", localDirection.z);
                }
                else
                {
                    Vector3 localDirection = _characterTransform.InverseTransformDirection(direction);

                    _animator.SetBool(IS_MOVING, true);
                    _animator.SetFloat("MoveX", localDirection.x);
                    _animator.SetFloat("MoveY", localDirection.z);
                }
            }
        }
    }
    
}
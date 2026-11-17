using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <remarks>
    /// Runs on every change of either direction, which for a moving character is every frame and for
    /// the player up to twice a frame. The parameters are set by hashed id: by name, every call
    /// hashed and marshalled the string again. The move condition is looked up once.
    /// </remarks>
    public class CharacterMoveAnimBehaviour : IEntityInit, IEntityDispose
    {
        private static readonly int IS_MOVING = Animator.StringToHash("IsMoving");
        private static readonly int MOVE_X = Animator.StringToHash("MoveX");
        private static readonly int MOVE_Y = Animator.StringToHash("MoveY");

        private Animator _animator;
        private IReactiveVariable<Vector3> _moveDirection;
        private IReactiveVariable<Vector3> _rotateDirection;
        private IEntity _character;
        private Transform _characterTransform;
        private IExpression<bool> _moveCondition;

        public void Init(IEntity entity)
        {
            _rotateDirection = entity.GetRotateDirection();
            _characterTransform = entity.GetTransform();
            _character = entity;
            _animator = entity.GetAnimator();
            _moveDirection = entity.GetMoveDirection();

            // Before the subscriptions: Observe calls back at once. Left null when the entity has no
            // condition, so UpdateAnimation still asks for it, and fails, exactly where it used to.
            entity.TryGetMoveCondition(out _moveCondition);

            _moveDirection.Observe(OnDirectionChanged);
            _rotateDirection.Observe(OnDirectionChanged);
        }

        public void Dispose(IEntity entity)
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

            if ((_moveCondition ?? _character.GetMoveCondition()).Invoke())
            {
                Vector3 rotateDir = _rotateDirection.Value;

                if (rotateDir != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(rotateDir);
                    Vector3 localDirection = Quaternion.Inverse(targetRotation) * direction;

                    _animator.SetBool(IS_MOVING, true);
                    _animator.SetFloat(MOVE_X, localDirection.x);
                    _animator.SetFloat(MOVE_Y, localDirection.z);
                }
                else
                {
                    Vector3 localDirection = _characterTransform.InverseTransformDirection(direction);

                    _animator.SetBool(IS_MOVING, true);
                    _animator.SetFloat(MOVE_X, localDirection.x);
                    _animator.SetFloat(MOVE_Y, localDirection.z);
                }
            }
        }
    }
    
}
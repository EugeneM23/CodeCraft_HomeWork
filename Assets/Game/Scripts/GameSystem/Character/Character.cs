using System;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class Character : IInitializeble
    {
        public event Action OnAttack;
        public event Action OnCollisionHit;
        public event Action<Vector2> OnGrounded;
        public event Action<StateType> OnDash;
        public event Action<StateType> OnItemThrow;
        public event Action<StateType> OnJump;
        public event Action<StateType> OnSmash;

        private CharacterController2D _characterController;

        private AttackComponent _attackComponent;
        private ThrowItemComponent _throwItemComponent;
        public Transform Transfrom => _characterController.transform;
        public int LookDirection => _characterController.LookDirection;
        public bool IsGrounded => _characterController.IsGrounded;
        public bool IsWallSliding => _characterController.IsWallSliding;
        public Vector2 Velocity => _characterController.Velocity;
        public Vector2 MoveDirection => _characterController.MoveDirection;

        [Inject]
        public void Construct(CharacterController2D character, AttackComponent attackComponent,
            ThrowItemComponent throwItemComponent)
        {
            _throwItemComponent = throwItemComponent;
            _characterController = character;
            _attackComponent = attackComponent;
        }

        public virtual void Initialize()
        {
            _characterController.OnGrounded += Grounded;
            _characterController.OnCollisionHit += ColisionHit;

            _characterController.AddMoveCondition(_attackComponent.IsAttacking);
            _characterController.AddMoveCondition(_throwItemComponent.IsThrowing);
        }

        private void ColisionHit() => OnCollisionHit?.Invoke();

        private void Grounded(Vector2 point) => OnGrounded?.Invoke(point);

        public void Attack()
        {
            if (_attackComponent.Attack())
                OnAttack?.Invoke();
        }

        public void ThrowItem()
        {
            if (_throwItemComponent.ThrowItem()) 
                OnItemThrow?.Invoke(StateType.ThrowItem);
        }

        public void Jump(Vector2 power)
        {
            _characterController.AddImpulse(power);
            OnJump?.Invoke(StateType.Jump);
        }

        public void Smash(Vector2 power)
        {
            _characterController.AddImpulse(power);
            OnSmash?.Invoke(StateType.Smash);
        }

        public void Dash(Vector2 power)
        {
            _characterController.AddImpulse(power);
            OnDash?.Invoke(StateType.Dash);
        }

        public void SetMoveDirection(Vector2 direction)
        {
            _characterController.SetMoveDirection(direction);
        }
    }
}
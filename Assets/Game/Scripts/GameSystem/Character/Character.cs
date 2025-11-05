using System;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class Character : IInitializeble
    {
        public event Action OnDash;
        public event Action OnItemThrow;
        public event Action OnJump;
        public event Action OnSmash;

        private CharacterController2D _characterController;
        private AttackComponent _attackComponent;
        private ThrowItemComponent _throwItemComponent;
        public int LookDirection => _characterController.LookDirection;

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
            _characterController.AddMoveCondition(_attackComponent.IsAttacking);
            _characterController.AddMoveCondition(_throwItemComponent.IsThrowing);
        }

        public void Attack() => _attackComponent.Attack();

        public void ThrowItem()
        {
            OnItemThrow?.Invoke();
            _throwItemComponent.ThrowItem();
        }

        public void Jump(Vector2 power)
        {
            _characterController.AddImpulse(power);
            OnJump?.Invoke();
        }

        public void Smash(Vector2 power)
        {
            _characterController.AddImpulse(power);
            OnSmash?.Invoke();
        }

        public void Dash(Vector2 power)
        {
            _characterController.AddImpulse(power);
            OnDash?.Invoke();
        }
    }
}
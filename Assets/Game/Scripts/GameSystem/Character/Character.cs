using System;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay
{
    public class Character : IInitializeble
    {
        public event Action OnDash;
        public event Action OnJump;
        public event Action OnSmash;

        private CharacterController2D _charactreController;
        private AttackComponent _attackComponent;

        [Inject]
        public void Construct(CharacterController2D character, AttackComponent attackComponent)
        {
            _charactreController = character;
            _attackComponent = attackComponent;
        }

        public virtual void Initialize()
        {
            _charactreController.AddMoveCondition(_attackComponent.IsAttacking);
        }

        public void Attack()
        {
            _attackComponent.Attack();
        }

        public void Jump(Vector2 power)
        {
            _charactreController.AddImpulse(power);
            OnJump?.Invoke();
        }

        public void Smash(Vector2 power)
        {
            _charactreController.AddImpulse(power);
            OnSmash?.Invoke();
        }

        public void Dash(Vector2 power)
        {
            _charactreController.AddImpulse(power);
            OnDash?.Invoke();
        }
    }
}
using System.Collections.Generic;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class AttackComponent : ITickable
    {
        [Inject] private readonly StateMachine _stateMachine;
        [Inject] private readonly CharacterController2D _character;
        [Inject] private readonly List<IAction> _actions;

        private readonly float _attackTime = 0.2f;
        private float _attackTimer;
        private bool _isAttacking;

        public interface IAction
        {
            void Invoke();
        }

        public void Attack()
        {
            if (_isAttacking)
                return;

            _isAttacking = true;
            _attackTimer = 0f;

            _stateMachine.SetState<AttackState>();

            foreach (var item in _actions)
                item.Invoke();
        }

        public void Tick()
        {
            if (_isAttacking)
            {
                _attackTimer += Time.deltaTime;
                if (_attackTimer >= _attackTime)
                {
                    _isAttacking = false;
                }
            }
        }

        public bool IsAttacking() => _isAttacking && _character.IsGrounded;
    }
}
using System.Collections.Generic;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class AttackComponent : ITickable, IInitializeble, IDisposable
    {
        [Inject] private readonly StateMachine _stateMachine;
        [Inject] private readonly CharacterController2D _character;
        [Inject] private readonly List<IEntityHitAction> _entityHitActions;

        [Inject] private readonly TargetSensor _targetSensor;

        private readonly float _attackTime = 0.2f;
        private float _attackTimer;
        private bool _isAttacking;

        public interface IEntityHitAction
        {
            void Invoke(RaycastHit2D hit, Entity entity);
        }

        public interface IEnviromentHitAction
        {
            void Invoke(RaycastHit2D hit);
        }

        public void Initialize() => _targetSensor.OnHitTarget += HitTarget;

        public void Dispose() => _targetSensor.OnHitTarget -= HitTarget;

        private void HitTarget(RaycastHit2D hit)
        {
            if (hit.transform.TryGetComponent<Entity>(out var entity))
            {
                foreach (var item in _entityHitActions)
                {
                    item.Invoke(hit, entity);
                }
            }
        }

        public void Attack()
        {
            if (_isAttacking)
                return;

            _isAttacking = true;
            _attackTimer = 0f;

            _stateMachine.SetState<AttackState>();
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
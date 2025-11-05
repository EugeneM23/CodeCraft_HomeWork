using System.Collections.Generic;
using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class AttackComponent : ITickable, IInitializeble, IDisposable
    {
        [Inject] private readonly AnimationFSM _animationFsm;
        [Inject] private readonly CharacterController2D _character;
        [Inject] private readonly List<IAttackAction> _attackActions;
        [Inject] private readonly List<IHitAction> _hitActions;
        [Inject] private readonly TargetSensor _targetSensor;

        private readonly float _attackTime = 0.2f;
        private float _attackTimer;
        private bool _isAttacking;

        public interface IAttackAction
        {
            void Invoke();
        }

        public interface IHitAction
        {
            void Invoke(RaycastHit2D hit, Entity entity);
        }

        public void Initialize() => _targetSensor.OnHitTarget += HitTarget;

        public void Dispose() => _targetSensor.OnHitTarget -= HitTarget;

        private void HitTarget(RaycastHit2D hit)
        {
            if (hit.transform.TryGetComponent<Entity>(out var entity))
                foreach (var item in _hitActions)
                    item.Invoke(hit, entity);
        }

        public bool Attack()
        {
            if (_isAttacking)
                return false;

            _isAttacking = true;
            _attackTimer = 0f;

            foreach (var item in _attackActions)
                item.Invoke();

            _animationFsm.SetState<AttackState>();
            return true;
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
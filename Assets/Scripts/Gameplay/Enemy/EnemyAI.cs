using System;
using UnityEngine;

namespace Gameplay
{
    public sealed class EnemyAI : MonoBehaviour
    {
        private LevePointsManager _attackPoints;
        private Vector3 _moveTarget;
        private Character _enemy;
        private bool _isFirePosition;
        private Transform _attackTarget;

        private void OnEnable()
        {
            _enemy = GetComponent<Character>();
            _isFirePosition = false;
        }

        private void Update()
        {
            if (_isFirePosition)
                Shoot();
            else
                MoveToAttackPosition();
        }

        private void Shoot()
        {
            var direction = _attackTarget.position - transform.position;
            _enemy.Shoot(direction);
        }

        private void MoveToAttackPosition()
        {
            if (_moveTarget == Vector3.zero)
                _moveTarget = _attackPoints.GetPoint();

            if (Vector3.Distance(_moveTarget, transform.position) < 0.5f)
            {
                _isFirePosition = true;
                return;
            }

            _enemy.Move(_moveTarget - transform.position);
        }

        public void SetAttackTarget(Transform target)
        {
            _attackTarget = target;
        }

        public void SetAttackPointmanager(LevePointsManager levePointsManager)
        {
            _attackPoints = levePointsManager;
        }
    }
}
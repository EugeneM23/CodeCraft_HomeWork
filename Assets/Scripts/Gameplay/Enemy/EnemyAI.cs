using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay
{
    public sealed class EnemyAI : MonoBehaviour
    {
        public event Action OnShoot;

        private Vector3 _moveTarget;

        private Character _enemy;
        private bool _isFirePosition;

        private void OnEnable()
        {
            _enemy = GetComponent<Character>();
            _isFirePosition = false;
            _moveTarget = GetMoveTarget();
        }

        private void Update()
        {
            if (_isFirePosition)
                Shoot();
            else
                MoveToAttackPosition();
        }

        private void Shoot() => OnShoot?.Invoke();

        private void MoveToAttackPosition()
        {
            if (Vector3.Distance(_moveTarget, transform.position) < 0.1f)
            {
                _isFirePosition = true;
                return;
            }

            _enemy.Move(_moveTarget - transform.position);
        }

        private Vector3 GetMoveTarget()
        {
            LevelAttackPoints attckPoints = LevelAttackPoints.Instance;

            int rand = Random.Range(0, LevelAttackPoints.Instance.Count);
            Vector3 moveTarget = attckPoints.GetAttackPoint(rand).transform.position;

            return moveTarget;
        }
    }
}
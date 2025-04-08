using UnityEngine;

namespace Gameplay
{
    public sealed class EnemyAI : MonoBehaviour
    {
        private const float STOPING_DISTANCE = 0.5f;
        private Vector3 _destination;
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
                MoveToPosition();
        }

        private void Shoot()
        {
            var direction = _attackTarget.position - transform.position;
            _enemy.Shoot(direction);
        }

        private void MoveToPosition()
        {
            if (Vector3.Distance(_destination, transform.position) < STOPING_DISTANCE)
            {
                _isFirePosition = true;
                return;
            }

            _enemy.Move(_destination - transform.position);
        }

        public void SetAttackTarget(Transform target) => _attackTarget = target;

        public void SetDestination(Vector3 destination) => _destination = destination;
    }
}
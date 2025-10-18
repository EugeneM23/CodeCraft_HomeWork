using UnityEngine;

namespace Gameplay
{
    public class PatrolComponent : IInitializeble, ITickable
    {
        [Inject] private Transform _cameraTransform;
        [Inject] private EnemyMoveController _moveController;

        private readonly Transform[] _patrolPoints;

        private int _currentPointIndex;
        private readonly float _reachThreshold = 0.2f;

        public PatrolComponent(Transform[] patrolPoints)
        {
            _patrolPoints = patrolPoints;
        }

        public void Initialize()
        {
            if (_patrolPoints == null || _patrolPoints.Length == 0)
            {
                Debug.LogError("PatrolComponent: Patrol points not assigned!");
                return;
            }

            _currentPointIndex = 0;
        }

        public void Tick()
        {
            if (_patrolPoints == null || _patrolPoints.Length == 0)
                return;

            Transform targetPoint = _patrolPoints[_currentPointIndex];
            MoveTo(targetPoint);
        }

        private void MoveTo(Transform patrolPoint)
        {
            Vector3 direction = (patrolPoint.position - _cameraTransform.position);
            direction.y = 0;
            float distance = direction.magnitude;

            if (distance < _reachThreshold)
            {
                NextPoint();
                return;
            }

            direction.Normalize();
            _moveController.Move(direction);
        }

        private void NextPoint()
        {
            _currentPointIndex = (_currentPointIndex + 1) % _patrolPoints.Length;
        }
    }
}
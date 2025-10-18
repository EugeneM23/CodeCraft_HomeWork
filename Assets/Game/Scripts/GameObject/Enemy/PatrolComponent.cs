using UnityEngine;

namespace Gameplay
{
    public class PatrolComponent : IInitializeble, ITickable
    {
        [Inject] private Transform _characterTransform;
        [Inject] private EnemyMoveController _moveController;

        
        private readonly Transform[] _patrolPoints;
        private int _currentPointIndex;
        private readonly float _reachThreshold = 0.2f;

        public bool IsActive { get; set; } = true; // Управление активностью извне

        public PatrolComponent(Transform[] patrolPoints)
        {
            _patrolPoints = patrolPoints;
        }

        public void Initialize()
        {
            _currentPointIndex = 0;
        }

        public void Tick()
        {
            if (!IsActive || _patrolPoints.Length == 0)
                return;

            Transform target = _patrolPoints[_currentPointIndex];
            MoveTo(target);
        }

        private void MoveTo(Transform target)
        {
            Vector3 direction = target.position - _characterTransform.position;
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
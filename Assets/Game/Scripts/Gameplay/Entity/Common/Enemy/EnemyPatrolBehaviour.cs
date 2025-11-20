using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyPatrolBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<Vector3> _moveDirection;
        private IReactiveVariable<IEntity> _target;
        private int _currentPointIndex;
        private Transform[] _patrolPoints;

        public void Init(in IEntity entity)
        {
            _moveDirection = entity.GetMoveDirection();
            _currentPointIndex = 0;
            _target = entity.GetTarget();
            _patrolPoints = entity.GetPatrolPoints();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_patrolPoints.Length == 0) return;
            if (_target.Value != null) return;

            Vector3 direction = PatrolUseCase.GetDirectionToPatrolPoint(entity, _patrolPoints, ref _currentPointIndex);

            _moveDirection.Value = direction;
        }
    }
}
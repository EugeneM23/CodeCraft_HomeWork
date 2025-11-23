using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class EnemyPatrolBehaviour : IEntityInit, IEntityUpdate
    {
        private IReactiveVariable<Vector3> _moveDirection;
        private IReactiveVariable<Vector3> _rotateDirection;

        private IReactiveVariable<IEntity> _target;
        private int _currentPointIndex;
        private Transform[] _patrolTransfroms;
        private readonly List<Vector3> _patrolPoints = new();

        public void Init(in IEntity entity)
        {
            _moveDirection = entity.GetMoveDirection();
            _rotateDirection = entity.GetRotateDirection();
            _currentPointIndex = 0;
            _target = entity.GetTarget();
            _patrolTransfroms = entity.GetPatrolPoints();

            foreach (var item in _patrolTransfroms) 
                _patrolPoints.Add(item.position);
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            if (_patrolTransfroms.Length == 0) return;
            if (_target.Value != null) return;

            Vector3 direction =
                PatrolUseCase.GetDirectionToPatrolPoint(entity, _patrolPoints, ref _currentPointIndex);

            _moveDirection.Value = direction.normalized;
            _rotateDirection.Value = direction.normalized;
        }
    }
}
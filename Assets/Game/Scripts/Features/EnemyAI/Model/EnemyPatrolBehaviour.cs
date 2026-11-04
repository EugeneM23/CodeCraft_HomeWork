using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class EnemyPatrolBehaviour : IEntityInit, IEntityTick
    {
        private IReactiveVariable<Vector3> _moveDirection;
        private IReactiveVariable<Vector3> _rotateDirection;

        private IReactiveVariable<IEntity> _target;
        private int _currentPointIndex;
        private Transform[] _patrolTransfroms;
        private readonly List<Vector3> _patrolPoints = new();

        public void Init(IEntity entity)
        {
            _moveDirection = entity.GetMoveDirection();
            _rotateDirection = entity.GetRotateDirection();
            _currentPointIndex = 0;
            _target = entity.GetTarget();
            _patrolTransfroms = entity.GetPatrolPoints();

            // Init runs again whenever the behaviour is re-added to a live entity, which is exactly
            // what StunBuff does when it removes and restores the patrol behaviour. Without this
            // Clear the point list grew by one lap per stun.
            _patrolPoints.Clear();

            // Enemy.prefab ships two unassigned patrol points; dereferencing them threw and
            // aborted Init() for every behaviour after this one. Pre-existing, not a migration
            // regression, but it makes the M-spawned enemy unusable.
            foreach (var item in _patrolTransfroms)
                if (item != null)
                    _patrolPoints.Add(item.position);
        }

        public void Tick(IEntity entity, float deltaTime)
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
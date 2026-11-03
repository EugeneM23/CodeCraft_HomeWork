using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public static class PatrolUseCase
    {
        public static Vector3 GetDirectionToPatrolPoint(in IEntity entity, in List<Vector3> patrolPoints,
            ref int currentPointIndex,
            float reachDistance = 0.5f)
        {
            if (patrolPoints == null || patrolPoints.Count == 0)
                return Vector3.zero;

            Transform entityTransform = entity.GetTransform();
            Vector3 currentPosition = entityTransform.position;
            Vector3 targetPosition = patrolPoints[currentPointIndex];

            float distanceToTarget = Vector3.Distance(currentPosition, targetPosition);

            if (distanceToTarget <= reachDistance)
            {
                currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
                targetPosition = patrolPoints[currentPointIndex];
            }

            return (targetPosition - currentPosition).normalized;
        }
    }
}
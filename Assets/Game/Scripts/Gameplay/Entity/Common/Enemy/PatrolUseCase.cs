using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public static class PatrolUseCase
    {
        public static Vector3 GetDirectionToPatrolPoint(in IEntity entity, in Transform[] patrolPoints,
            ref int currentPointIndex,
            float reachDistance = 0.5f)
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return Vector3.zero;

            Transform entityTransform = entity.GetTransform();
            Vector3 currentPosition = entityTransform.position;
            Vector3 targetPosition = patrolPoints[currentPointIndex].position;

            float distanceToTarget = Vector3.Distance(currentPosition, targetPosition);

            if (distanceToTarget <= reachDistance)
            {
                currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
                targetPosition = patrolPoints[currentPointIndex].position;
            }

            return (targetPosition - currentPosition).normalized;
        }
    }
}
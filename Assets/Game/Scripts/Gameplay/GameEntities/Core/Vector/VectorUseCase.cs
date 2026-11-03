using Atomic.Entities;
using UnityEngine;

namespace Game.Content
{
    public static class VectorUseCase
    {
        public static bool LessOrEqualsDistance(in IEntity entity, in Vector3 point, float distance) => 
            GetDistance(entity, point) <= distance;

        public static float GetDistance(in IEntity entity, Vector3 position)
        {
            Vector3 currentPosition = entity.GetTransform().position;
            Vector3 distance = position - currentPosition;
            return distance.magnitude;
        } 
    }
}
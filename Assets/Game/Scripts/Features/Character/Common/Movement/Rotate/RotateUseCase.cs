using Atomic.Elements;
using Atomic.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game
{
    public static class RotateUseCase
    {
        public static void Rotate(this IEntity entity, in Vector3 direction, in float deltaTime)
        {
            if (direction == Vector3.zero) return;

            Quaternion targetRotaion = Quaternion.LookRotation(direction, Vector3.up);
            Rotate(entity, targetRotaion, deltaTime);
        }

        private static void Rotate(in IEntity entity, in Quaternion targetRotation, in float deltaTime)
        {
            float speed = entity.GetRotationSpeed().Value;
            Transform transform = entity.GetTransform();

            Rotate(transform, targetRotation, speed, deltaTime);
        }

        public static void Rotate(this Transform transform, in Quaternion targetRotation, float rotationSpeed,
            in float deltaTime)
        {
            // Frame-rate independent smoothing. The old factor was `rotationSpeed * deltaTime`,
            // which made turn speed depend on frame rate (0.25 at 60 fps vs 0.10 at 144 fps) and
            // passed 1.0 on any frame longer than 1/rotationSpeed seconds, snapping the facing.
            float t = 1f - Mathf.Exp(-rotationSpeed * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }

        public static void Rotate(this Transform transform, in Quaternion targetRotation)
        {
            transform.rotation = targetRotation;
        }
    }
}
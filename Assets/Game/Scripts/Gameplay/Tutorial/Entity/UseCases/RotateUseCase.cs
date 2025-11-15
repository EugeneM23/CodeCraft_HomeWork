using Atomic.Elements;
using Atomic.Entities;
using SampleGame;
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
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * deltaTime);
        }
    }
}
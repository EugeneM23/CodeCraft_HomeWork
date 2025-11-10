using Atomic.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Scripts.Gameplay
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
            if (targetRotation == quaternion.identity) return;


            float speed = entity.GetValue<float>("RotationSpeed");
            Transform transform = entity.GetValue<Transform>("Transform");

            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, speed * deltaTime);
        }
    }
}
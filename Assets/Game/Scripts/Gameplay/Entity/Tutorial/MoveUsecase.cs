using System;
using Atomic.Entities;
using UnityEngine;

namespace Game.Scripts.Gameplay
{
    public static class MoveUseCase
    {
        public static void Move(this IEntity entity, in Vector3 direction, in float deltaTime)
        {
            Transform transform = entity.GetValue<Transform>("Transform");
            float speed = entity.GetValue<float>("MoveSpeed");

            transform.position += direction * speed * deltaTime;
        }
    }
}
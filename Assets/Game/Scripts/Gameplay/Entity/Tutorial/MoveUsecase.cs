using System;
using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game.Scripts.Gameplay
{
    public static class MoveUseCase
    {
        public static void Move(this IEntity entity, in Vector3 direction, in float deltaTime)
        {
            Transform transform = entity.GetTransform();
            float speed = entity.GetMoveSpeed();

            transform.position += direction * speed * deltaTime;
        }
    }
}
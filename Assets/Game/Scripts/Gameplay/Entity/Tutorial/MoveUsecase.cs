using System;
using Atomic.Elements;
using Atomic.Entities;
using SampleGame;
using UnityEngine;

namespace Game.Scripts.Gameplay
{
    public static class MoveUseCase
    {
        public static void Move(this IEntity entity, in Vector3 direction, in float deltaTime) 
        {
            if (entity.TryGetMoveCondition(out var condition) && !condition.Value) return;

            Transform transform = entity.GetTransform();
            float speed = entity.GetMoveSpeed().Value;

            transform.position += direction * speed * deltaTime;
        }
    }
}
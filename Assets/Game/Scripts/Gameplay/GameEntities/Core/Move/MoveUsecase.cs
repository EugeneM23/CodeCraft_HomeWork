using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public static class MoveUseCase
    {
        public static void MoveSelf(this IEntity entity, float deltaTime)
        {
            IReactiveValue<Vector3> direction = entity.GetMoveDirection();
            entity.Move(direction.Value, deltaTime);
        }

        public static void Move(this IEntity entity, in Vector3 direction, in float deltaTime)
        {
            if (entity.TryGetMoveCondition(out var condition) && !condition.Value) return;

            Transform transform = entity.GetTransform();
            float speed = entity.GetMoveSpeed().Value;

            transform.position += direction * speed * deltaTime;
        }
    }
}
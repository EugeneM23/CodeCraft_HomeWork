using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class RotationBehaviour : IEntityUpdate
    {
        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            IReactiveVariable<Vector3> moveDirection = entity.GetMoveDirection();
            RotateUseCase.Rotate(entity, moveDirection.Value, deltaTime);
        }
    }
}
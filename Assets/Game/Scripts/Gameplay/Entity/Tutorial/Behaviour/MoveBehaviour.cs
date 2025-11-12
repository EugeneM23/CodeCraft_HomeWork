using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class MoveBehaviour : IEntityUpdate
    {

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            IReactiveVariable<Vector3> moveDirection = entity.GetMoveDirection();
            MoveUseCase.Move(entity, moveDirection.Value, deltaTime);
        }
    }
}
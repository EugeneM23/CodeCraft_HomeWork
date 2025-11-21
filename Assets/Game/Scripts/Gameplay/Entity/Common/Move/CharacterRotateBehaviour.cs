using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterRotateBehaviour : IEntityInit, IEntityUpdate
    {
        private IValue<Vector3> _moveDirection;

        public void Init(in IEntity entity)
        {
            _moveDirection = entity.GetMoveDirection();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            Vector3 direction = _moveDirection.Value;
            RotateUseCase.Rotate(entity, direction, deltaTime);
        }
    }
}
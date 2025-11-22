using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterRotateBehaviour : IEntityInit, IEntityUpdate
    {
        private IValue<Vector3> _rotateDirection;

        public void Init(in IEntity entity)
        {
            _rotateDirection = entity.GetRotateDirection();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            Vector3 direction = _rotateDirection.Value;
            RotateUseCase.Rotate(entity, direction, deltaTime);
        }
    }
}
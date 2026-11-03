using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterRotateBehaviour : IEntityInit, IEntityTick
    {
        private IValue<Vector3> _rotateDirection;

        public void Init(IEntity entity)
        {
            _rotateDirection = entity.GetRotateDirection();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            Vector3 direction = _rotateDirection.Value;
            RotateUseCase.Rotate(entity, direction, deltaTime);
        }
    }
}
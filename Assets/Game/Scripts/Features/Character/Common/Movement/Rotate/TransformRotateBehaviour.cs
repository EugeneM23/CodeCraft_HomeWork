using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <remarks>
    /// The transform and the turn speed are looked up once: RotateUseCase.Rotate(IEntity) looked
    /// both up again every frame for every enemy. Same turn, same early out for no direction.
    /// </remarks>
    public class TransformRotateBehaviour : IEntityInit, IEntityTick
    {
        private IValue<Vector3> _rotateDirection;
        private IValue<float> _rotationSpeed;
        private Transform _transform;

        public void Init(IEntity entity)
        {
            _rotateDirection = entity.GetRotateDirection();
            _rotationSpeed = entity.GetRotationSpeed();
            _transform = entity.GetTransform();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            Vector3 direction = _rotateDirection.Value;
            if (direction == Vector3.zero)
                return;

            RotateUseCase.Rotate(_transform, Quaternion.LookRotation(direction, Vector3.up), _rotationSpeed.Value,
                deltaTime);
        }
    }
}
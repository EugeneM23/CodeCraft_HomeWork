using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class CharacterVelocityBehaviour : IEntityTick, IEntityInit
    {
        private Transform _characterTransform;
        private Vector3 _lastFramePosition;
        private IReactiveVariable<float> _velocity;

        public void Init(IEntity entity)
        {
            _characterTransform = entity.GetTransform();
            _velocity = entity.GetVelocity();
            _lastFramePosition = _characterTransform.position;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            // Read once: each read is a native call that rebuilds the world position from the hierarchy.
            Vector3 position = _characterTransform.position;
            float distance = Vector3.Distance(position, _lastFramePosition);
            _velocity.Value = deltaTime > 0 ? distance / deltaTime : 0f;
            _lastFramePosition = position;
        }
    }
}
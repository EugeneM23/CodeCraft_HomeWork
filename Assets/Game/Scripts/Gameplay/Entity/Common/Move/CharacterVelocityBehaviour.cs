using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
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
            float distance = Vector3.Distance(_characterTransform.position, _lastFramePosition);
            _velocity.Value = deltaTime > 0 ? distance / deltaTime : 0f;
            _lastFramePosition = _characterTransform.position;
        }
    }
}
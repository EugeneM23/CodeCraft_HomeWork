using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class CharacterVelocityBehaviour : IEntityUpdate, IEntityInit
    {
        private Transform _characterTransform;
        private Vector3 _lastFramePosition;
        private IReactiveVariable<float> _velocity;

        public void Init(in IEntity entity)
        {
            _characterTransform = entity.GetTransform();
            _velocity = entity.GetVelocity();
            _lastFramePosition = _characterTransform.position;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            float distance = Vector3.Distance(_characterTransform.position, _lastFramePosition);
            _velocity.Value = deltaTime > 0 ? distance / deltaTime : 0f;
            _lastFramePosition = _characterTransform.position;
        }
    }
}
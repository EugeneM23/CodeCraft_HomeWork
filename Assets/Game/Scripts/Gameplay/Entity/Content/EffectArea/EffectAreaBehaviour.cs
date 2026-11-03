using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class EffectAreaBehaviour : IEntityInit, IEntityDispose
    {
        private readonly TriggerEventReceiver _trigger;
        private readonly IEntityAspect _entityAspect;

        public EffectAreaBehaviour(TriggerEventReceiver trigger, IEntityAspect entityAspect)
        {
            _trigger = trigger;
            _entityAspect = entityAspect;
        }

        public void Init(IEntity entity)
        {
            _trigger.OnEntered += OnTriggerEnter;
            _trigger.OnExited += OnTriggerExit;
        }

        public void Dispose(IEntity entity)
        { 
            _trigger.OnEntered -= OnTriggerEnter;
            _trigger.OnExited -= OnTriggerExit;
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (collider.TryGetEntity(out var entity))
                _entityAspect.Apply(entity);
        }

        private void OnTriggerExit(Collider collider)
        {
            if (collider.TryGetEntity(out var entity))
                _entityAspect.Discard(entity);
        }
    }
}

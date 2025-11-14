using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class InteractBehaviour : IEntityInit, IEntityDispose
    {
        private TriggerEventReceiver _triggerReceiver;
        private IEntity _entity;

        public void Init(in IEntity entity)
        {
            _entity = entity;
            _triggerReceiver = entity.GetTriggerEventReceiver();
            _triggerReceiver.OnEntered += Interact;
        }

        private void Interact(Collider collider)
        {
            if (collider.TryGetComponent<IEntity>(out var other))
            {
                InteractUseCase.Interact(_entity, other);
            }
        }

        public void Dispose(in IEntity entity) => _triggerReceiver.OnEntered -= Interact;
    }
}
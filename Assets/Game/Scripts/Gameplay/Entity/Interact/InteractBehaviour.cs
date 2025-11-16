using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class InteractBehaviour : IEntityInit, IEntityDispose
    {
        private TriggerEventReceiver _triggerReceiver;
        private IEntity _character;

        public void Init(in IEntity entity)
        {
            _character = entity;
            _triggerReceiver = entity.GetTriggerEventReceiver();
            _triggerReceiver.OnEntered += Interact;
        }

        private void Interact(Collider collider)
        {
            if (collider.TryGetComponent<IEntity>(out var target))
            {
                InteractUseCase.Interact(_character, target);
            }
        }

        public void Dispose(in IEntity entity) => _triggerReceiver.OnEntered -= Interact;
    }
}
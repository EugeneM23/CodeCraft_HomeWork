using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class AmmoCollisionBehaviour : IEntityInit, IEntityDispose
    {
        private TriggerEventReceiver _collisionEventReceiver;
        private IEntity _self;

        public void Init(in IEntity entity)
        {
            _self = entity;
            _collisionEventReceiver = entity.GetTriggerEventReceiver();
            _collisionEventReceiver.OnEntered += PickUp;
        }

        private void PickUp(Collider character)
        {
            if (!character.gameObject.TryGetComponent<SceneEntity>(out var entity)) return;
            _self.GetInteractAction()?.Invoke(entity);
        }

        public void Dispose(in IEntity entity)
        {
            _collisionEventReceiver.OnEntered -= PickUp;
        }
    }
}
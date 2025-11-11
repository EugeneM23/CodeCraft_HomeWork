using System;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using Unity.VisualScripting;
using UnityEngine;

namespace Game
{
     public class BulletCollisionBehaviour : IEntityInit, IEntityDispose, IEntityUpdate
    {
        private CollisionEventReceiver _collisionEventReceiver;
        private IValue<int> _damage;
        private IEntity _entity;
        private float _lifeTime = 1f;

        public void Init(in IEntity entity)
        {
            _collisionEventReceiver = entity.GetCollisionReceiver();
            _damage = entity.GetDamage();
            _entity = entity;

            _collisionEventReceiver.OnEntered += Destroy;
        }

        private void Destroy(Collision obj)
        {
            if (obj.gameObject.TryGetComponent(out IEntity target) && target.HasDamageableTag())
            {
                target.TakeDamage(_damage.Value);
            }


            Dispose(_entity);
        }

        public void Dispose(in IEntity entity)
        {
            GameObject gameObject = entity.GetGameObject();
            GameObject.Destroy(gameObject);

            _collisionEventReceiver.OnEntered -= Destroy;
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            _lifeTime -= deltaTime;

            if (_lifeTime < 0)
                Dispose(entity);
        }
    }
}
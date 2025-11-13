using System;
using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class BulletCollisionBehaviour : IEntityInit, IEntityDispose, IEntityUpdate
    {
        private readonly GameContext _gameContext;
        private CollisionEventReceiver _collisionEventReceiver;
        private IEntity _bullet;
        private float _lifeTime = 1f;

        public  BulletCollisionBehaviour(GameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Init(in IEntity entity)
        {
            _lifeTime = entity.GetLifeTime().Value;
            _collisionEventReceiver = entity.GetCollisionReceiver();
            _bullet = entity;

            _collisionEventReceiver.OnEntered += Destroy;
        }

        private void Destroy(Collision obj)
        {
            if (obj.gameObject.TryGetComponent(out IEntity target) && target.HasDamageableTag())
                target.TakeDamage(_bullet.GetDamage().Value);

            Dispose(_bullet);
        }

        public void Dispose(in IEntity entity)
        {
            SpawnBulletUseCase.UnSpawnBullet(_gameContext, entity);
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
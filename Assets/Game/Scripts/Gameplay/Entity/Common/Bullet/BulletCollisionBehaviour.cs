using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class BulletCollisionBehaviour : IEntityInit, IEntityDispose
    {
        private CollisionEventReceiver _collisionEventReceiver;
        private IEntity _bullet;
        private GameFactory _gameFactory;

        public void Init(in IEntity entity)
        {
            _gameFactory = GameContext.Instance.GetGameFactory();
            _collisionEventReceiver = entity.GetCollisionReceiver();
            _bullet = entity;
            _collisionEventReceiver.OnEntered += Destroy;
        }

        public void Dispose(in IEntity entity) => _collisionEventReceiver.OnEntered -= Destroy;

        private void Destroy(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out IEntity target) && target.HasDamageableTag())
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));
            else
                SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);

            _gameFactory.Destroy(_bullet);
        }
    }
}
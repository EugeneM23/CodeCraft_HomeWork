using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class BulletCollisionBehaviour : IEntityInit, IEntityDispose
    {
        private readonly GameContext _gameContext;
        private CollisionEventReceiver _collisionEventReceiver;
        private IEntity _bullet;
        private IEntityPool _hitEffectPool;

        public BulletCollisionBehaviour(GameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Init(in IEntity entity)
        {
            _collisionEventReceiver = entity.GetCollisionReceiver();
            _bullet = entity;
            _collisionEventReceiver.OnEntered += Destroy;
            _hitEffectPool = GameContext.Instance.GetHitEffectPool();
        }

        public void Dispose(in IEntity entity) => _collisionEventReceiver.OnEntered -= Destroy;

        private void Destroy(Collision collision)
        {
            if (collision.gameObject.TryGetComponent(out IEntity target) && target.HasDamageableTag())
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));
            else
                SpawnParticleUseCase.SpawnEnviromentHit(collision, _hitEffectPool);

            FireBulletUseCase.UnSpawnBullet(_gameContext, _bullet);
        }
    }
}
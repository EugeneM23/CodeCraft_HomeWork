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

        public void Dispose(in IEntity entity)
        {
            _collisionEventReceiver.OnEntered -= Destroy;
        }

        private void Destroy(Collision obj)
        {
            if (obj.gameObject.TryGetComponent(out IEntity target) && target.HasDamageableTag())
            {
                target.GetHealth().Reduce(_bullet.GetDamage().Value);
                target.GetDamageTakenEvent().Invoke(new TakeDamageArgs(_bullet));
            }
            else
            {
                ContactPoint contact = obj.GetContact(0);
                Vector3 position = contact.point;
                Vector3 normal = contact.normal;
        
                IEntity go = _hitEffectPool.Rent();
                Transform effectTransform = go.GetTransform();
                effectTransform.position = position;
                effectTransform.rotation = Quaternion.LookRotation(normal);
            }

            UnSpawn(_bullet);
        }

        private void UnSpawn(IEntity bullet)
        {
            FireBulletUseCase.UnSpawnBullet(_gameContext, bullet);
        }
    }
}
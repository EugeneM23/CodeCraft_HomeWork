using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class BulletInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _damage;
        [SerializeField] private float _moveSpeed = 10f;
        [SerializeField] private float _lifeTime = 1f;
        [SerializeField] private SceneEntity _hitPrefab;
        [SerializeField] private CollisionEventReceiver _collisionEventReceiver;
        private GameFactory _factory;

        public override void Install(IEntity entity)
        {
            _factory = GameContext.Instance.GetGameFactory();

            //Core
            entity.AddRangeWeaponTag();
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddBulletHitPrefab(_hitPrefab);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);

            //LifeTime
            entity.AddLifeTime(new Cooldown(_lifeTime));
            entity.AddBehaviour(new LifeTimeBehaviour());
            entity.AddDestroyAction(new BaseAction(() => _factory.Destroy(entity)));

            //Movement
            entity.AddMoveSpeed(new ReactiveFloat(_moveSpeed));
            entity.AddMoveDirection(new ReactiveVector3(transform.forward));

            //Collision
            entity.AddCollisionReceiver(_collisionEventReceiver);
            entity.AddBehaviour<BulletCollisionBehaviour>();

            entity.WhenFixedUpdate(entity.MoveSelf);
        }
    }
}
using Atomic.Contexts;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public class BulletInstaller : SceneEntityInstaller
    {
        [SerializeField] private CollisionEventReceiver _collisionEventReceiver;
        [SerializeField] private int _damage;
        [SerializeField] private float _lifeTime = 1f;

        public override void Install(IEntity entity)
        {
            entity.AddLifeTime(new BaseVariable<float>(_lifeTime));
            entity.AddMoveSpeed(new Const<float>(10f));
            entity.AddGameObject(transform.gameObject);
            entity.AddCollisionReceiver(_collisionEventReceiver);
            entity.AddTransform(transform);
            entity.AddMoveDirection(new ReactiveVector3(transform.forward));

            entity.WhenFixedUpdate(entity.MoveSelf);

            entity.AddBehaviour(new BulletCollisionBehaviour(GameContext.Instance));
        }
    }
}
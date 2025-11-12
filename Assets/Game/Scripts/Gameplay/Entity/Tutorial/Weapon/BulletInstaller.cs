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

        public override void Install(IEntity entity)
        {
            entity.AddMoveSpeed(new Const<float>(10f));
            entity.AddGameObject(transform.gameObject);

            entity.AddCollisionReceiver(_collisionEventReceiver);
            entity.AddTransform(transform);
            entity.AddMoveDirection(new ReactiveVector3(transform.forward));

            entity.WhenFixedUpdate(entity.MoveSelf);

            entity.AddBehaviour<BulletCollisionBehaviour>();
        }
    }
}
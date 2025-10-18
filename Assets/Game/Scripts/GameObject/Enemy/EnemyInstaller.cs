using Game.Scripts.GameObject.Player;
using Gameplay.Ability;
using UnityEngine;

namespace Gameplay
{
    public class EnemyInstaller : Installer
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private int _health = 100;
        [SerializeField] private int _gravityScale = 20;
        [SerializeField] private Transform _transform;
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private LayerMask _targetLayer;
        [SerializeField] private Transform[] _patrolPoints;

        [SerializeField] private CircleCollider2D _collider;

        public override void Install(DiContainer container)
        {
            container.BindSingle(_groundLayer);
            container.BindSingle(_rigidbody);
            container.BindSingle(_transform);

            container.BindSingle(new GravityScaleComponent(_rigidbody, _gravityScale));
            container.BindSingle(new Enemy());
            var collisionComponent = new CollisionComponent(_collider, _groundLayer);
            var impulseComponent = new ImpulseComponent(_rigidbody);

            container.BindSingle(impulseComponent);
            container.BindSingle(collisionComponent);

            var moveComponent = new MoveComponent(_rigidbody, _moveSpeed);
            var rotationComponent = new RotationComponent();

            container.BindSingle(new GroundSyncComponent(collisionComponent, moveComponent));
            container.BindSingle(moveComponent);
            container.BindSingle(rotationComponent);
            container.BindSingle(new EnemyMoveController(moveComponent, rotationComponent));

            var healthComponent = new HealthComponent(_health);
            container.BindSingle(healthComponent);
            container.BindSingle(new DeathObserver(healthComponent, _transform));

         
        }
    }
}
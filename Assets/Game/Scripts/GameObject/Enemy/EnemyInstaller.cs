using Game.Scripts.GameObject.Player;
using Gameplay.Ability;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class EnemyInstaller : Installer
    {
        [Header("Enemy Settings")] [SerializeField]
        private float _moveSpeed = 5f;

        [SerializeField] private int _health = 100;
        [SerializeField] private int _gravityScale = 20;

        [Header("Components")] [SerializeField]
        private Transform _transform;

        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CircleCollider2D _collider;

        [Header("Environment Settings")] 
        [SerializeField] private LayerMask _collisionLayer;
        [SerializeField] private Transform[] _patrolPoints;

        public override void Install(DiContainer container)
        {
            // --- Base bindings ---
            container.BindSingle(_collisionLayer);
            container.BindSingle(_rigidbody);
            container.BindSingle(_transform);

            // --- Core enemy setup ---
            container.BindSingle(new GravityScaleComponent(_rigidbody, _gravityScale));
            container.BindSingle(new Enemy());

            // --- Movement & physics ---
            var collisionComponent = new CollisionComponent(_collider, _collisionLayer);
            var impulseComponent = new ImpulseComponent(_rigidbody);
            var moveComponent = new MoveComponent(_rigidbody, _moveSpeed);
            var rotationComponent = new RotationComponent();

            container.BindSingle(impulseComponent);
            container.BindSingle(collisionComponent);
            container.BindSingle(moveComponent);
            container.BindSingle(rotationComponent);

            container.BindSingle(new GroundSyncComponent(collisionComponent, moveComponent));
            container.BindSingle(new EnemyMoveController(moveComponent, rotationComponent));

            // --- Health & death handling ---
            var healthComponent = new HealthComponent(_health);
            container.BindSingle(healthComponent);
            container.BindSingle(new DeathObserver(healthComponent, _transform));

            // --- Optional AI / behavior ---
            container.BindSingle(_patrolPoints);
        }
    }
}
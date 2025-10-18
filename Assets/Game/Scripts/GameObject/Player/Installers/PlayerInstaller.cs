using Game.Scripts.GameObject.Player;
using UnityEngine;

namespace Gameplay
{
    public class PlayerInstaller : Installer
    {
        [Header("Character Settings")] [SerializeField]
        private float _moveSpeed = 5f;

        [SerializeField] private float _jumpForce = 5f;
        [SerializeField] private int _health = 100;
        [SerializeField] private int _gravityScale = 20;
        [SerializeField] private float _attackSpeed = 0.3f;

        [Header("Components")] [SerializeField]
        private Transform _transform;

        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CircleCollider2D _collider;

        [Header("Environment Settings")] [SerializeField]
        private LayerMask _groundLayer;

        public override void Install(DiContainer container)
        {
            // --- Base bindings ---
            container.BindSingle(_groundLayer);
            container.BindSingle(_collider);
            container.BindSingle(_rigidbody);
            container.BindSingle(_transform);

            // --- Core player setup ---
            container.BindSingle(new Player());
            container.BindSingle(new GravityScaleComponent(_rigidbody, _gravityScale));

            // --- Movement & physics ---
            var collisionComponent = new CollisionComponent(_collider, _groundLayer);
            var impulseComponent = new ImpulseComponent(_rigidbody);
            var jumpComponent = new JumpComponent();
            var moveComponent = new MoveComponent(_rigidbody, _moveSpeed);
            var rotationComponent = new RotationComponent();

            container.BindSingle(collisionComponent);
            container.BindSingle(impulseComponent);
            container.BindSingle(jumpComponent);
            container.BindSingle(moveComponent);
            container.BindSingle(rotationComponent);

            container.BindSingle(new JumpController(collisionComponent, impulseComponent, jumpComponent));
            container.BindSingle(new MoveController(moveComponent, rotationComponent));
            container.BindSingle(new GroundSyncComponent(collisionComponent, moveComponent));

            // --- Health & death handling ---
            var healthComponent = new HealthComponent(_health);
            container.BindSingle(healthComponent);
            container.BindSingle(new DeathObserver(healthComponent, _transform));
        }
    }
}
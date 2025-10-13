using Gameplay;
using UnityEngine;

namespace Game.Scripts.Player
{
    public class PlayerInstaller : Installer
    {
        [Header("Character Settings")] [SerializeField]
        private float _moveSpeed = 5f;

        [SerializeField] private float _jumpForce = 5f;
        [SerializeField] private int _health = 100;
        [SerializeField] private int _gravityScale = 20;

        [Header("Components")] [SerializeField]
        private Transform _transform;

        [SerializeField] private TakeDamageProxy _damageProxy;
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CircleCollider2D _collider;

        [Header("Environment Settings")] [SerializeField]
        private LayerMask _groundLayer;

        public override void Install(diContainer container)
        {
            Debug.Log("PlayerInstaller installer");
            container.Add(new CollisionComponent(_collider, _groundLayer));
            container.Add(new GravityScaleComponent(_rigidbody, _gravityScale));
            container.Add(new JumpController());
            container.Add(_collider);
            container.Add(new JumpComponent(_jumpForce, _rigidbody));

            container.Add(new MoveController());
            container.Add(new RotationComponent(_transform));
            container.Add(new InputReader());
            container.Add(new MoveComponent(_rigidbody, _moveSpeed));
            container.Add(new DeathObserver());
            container.Add(new HealthComponent(_health));
            container.Add(_transform);
        }
    }
}
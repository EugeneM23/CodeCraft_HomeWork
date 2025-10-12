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

        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CircleCollider2D _collider;

        [Header("Environment Settings")] [SerializeField]
        private LayerMask _groundLayer;

        public override void Install(DiContainer container)
        {
            
            container.Add(PlayerId.MoveController, new MoveController());
            container.Add(PlayerId.JumpController, new JumpController());
            container.Add(PlayerId.FireController, new FireController());

            container.Add(PlayerId.Condition, new CompositCondition());

            container.Add(PlayerId.Character, new Gameplay.Player());
            container.Add(PlayerId.Transfrom, _transform);
            container.Add(PlayerId.DeathObserver, new DeathObserver());

            container.Add(PlayerId.HealthComponent, new HealthComponent(_health));
            container.Add(PlayerId.GravityScale, new GravityScaleComponent(_rigidbody, _gravityScale));
            container.Add(PlayerId.MoveComponent, new MoveComponent(_rigidbody, _moveSpeed));
            container.Add(PlayerId.RotationComponent, new RotationComponent(_transform));
            container.Add(PlayerId.JumpComponent, new JumpComponent(_jumpForce, _rigidbody));
            container.Add(PlayerId.CollisionComponent, new CollisionComponent(_collider, _groundLayer));
            container.Add(PlayerId.GroundSyncComponent, new GroundSyncComponent());
            container.Add(PlayerId.Rigidbody2D, _rigidbody);
        }
    }
}
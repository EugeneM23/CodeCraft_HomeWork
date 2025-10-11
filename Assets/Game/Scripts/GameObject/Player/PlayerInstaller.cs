using Gameplay;
using UnityEngine;

namespace Game.Scripts.Player
{
    [DefaultExecutionOrder(-999)]
    public class PlayerInstaller : MonoBehaviour
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

        private void Awake()
        {
            ServiceLocator.Add(PlayerId.MoveController, new MoveController());
            ServiceLocator.Add(PlayerId.JumpController, new JumpController());
            ServiceLocator.Add(PlayerId.FireController, new FireController());

            ServiceLocator.Add(PlayerId.Condition, new CompositCondition());

            ServiceLocator.Add(PlayerId.Character, new Gameplay.Player());
            ServiceLocator.Add(PlayerId.DeathObserver, new DeathObserver(_transform));

            ServiceLocator.Add(PlayerId.HealthComponent, new HealthComponent(_health));
            ServiceLocator.Add(PlayerId.GravityScale, new GravityScaleComponent(_rigidbody, _gravityScale));
            ServiceLocator.Add(PlayerId.MoveComponent, new MoveComponent(_rigidbody, _moveSpeed));
            ServiceLocator.Add(PlayerId.RotationComponent, new RotationComponent(_transform));
            ServiceLocator.Add(PlayerId.JumpComponent, new JumpComponent(_jumpForce, _rigidbody));
            ServiceLocator.Add(PlayerId.CollisionComponent, new CollisionComponent(_collider, _groundLayer));
            ServiceLocator.Add(PlayerId.GroundSyncComponent, new GroundSyncComponent());
        }
    }
}
using System;
using System.Collections.Generic;
using Game.Scripts.Modules.PlayerController.Data;
using UnityEngine;

namespace Modules.PlayerController
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private CapsuleCollider2D _capsuleCollider;

        private IReadOnlyCollection<ITickable> _tickables;
        private IReadOnlyCollection<IVelocity> _velocities;

        private CollisionComponent _collisionComponent;
        private SlopeSlideComponent _slopeSlideComponent;
        private WallSlidingComponent _wallSlidingComponent;
        private MoveController _moveController;
        private IMoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        private ImpulseComponent _impulseComponent;

        public ServiceLocator ServiceLocator { get; private set; }
        public PlayerStats Stats { get; private set; }
        public event Action OnCollisionHit;
        public event Action OnJump;
        public bool IsOnStairs { get; set; }
        public CapsuleCollider2D Collider => _capsuleCollider;
        public Vector2 Velocity => _rigidbody2D.linearVelocity;
        public bool IsGrounded => _collisionComponent.IsGrounded;
        public bool IsCeilingHit => _collisionComponent.IsCeilingHit;
        public Vector2 SurfaceNormal => _collisionComponent.SurfaceNormal;
        public bool IsOnSlope => _collisionComponent.IsOnSlope;
        public int WallDirection => _collisionComponent.WallDirection;
        public bool IsOnWall => _collisionComponent.IsOnWall;
        public bool IsSlidingOnSlope => _slopeSlideComponent.IsSlidingOnslope;
        public bool IsWallSliding => _wallSlidingComponent.IsWallSliding;
        public Vector2 MoveDirection => _moveController.CurrentDirection;
        public float DistanceToGround => _collisionComponent.DistanceToGround;
        
        private void Awake()
        {
            _rigidbody2D.gravityScale = 0;
            _rigidbody2D.interpolation = RigidbodyInterpolation2D.Extrapolate;

            ServiceLocator = new ServiceLocator(this);

            Stats = ServiceLocator.Get<PlayerStats>();
            _tickables = ServiceLocator.GetAll<ITickable>();
            _velocities = ServiceLocator.GetAll<IVelocity>();

            _collisionComponent = ServiceLocator.Get<CollisionComponent>();
            _wallSlidingComponent = ServiceLocator.Get<WallSlidingComponent>();
            _moveController = ServiceLocator.Get<MoveController>();
            _moveComponent = ServiceLocator.Get<MoveComponent>();
            _jumpComponent = ServiceLocator.Get<JumpComponent>();
            _impulseComponent = ServiceLocator.Get<ImpulseComponent>();
            //_slopeSlideComponent = ServiceLocator.Get<SlopeSlideComponent>();
        }

        private void Update()
        {
            foreach (var tickable in _tickables)
                tickable.Tick();
        }

        private void FixedUpdate()
        {
            Vector2 velocity = Vector2.zero;

            foreach (var v in _velocities)
                velocity += v.GetVelocity();

            _rigidbody2D.linearVelocity = velocity;
        }

        public void Move(Vector2 direction) => _moveComponent.Move(direction);

        public void Jump() => _jumpComponent.Jump();

        public void AddImpulse(Vector2 impulse)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _impulseComponent.AddImpulse(impulse);
        }

        public void OnCollisionEnter2D(Collision2D other) => OnCollisionHit?.Invoke();

        public void ResetVelocity() => _rigidbody2D.linearVelocity = Vector2.zero;

        public void SetComponent<T>() where T : class
        {
            T component = ServiceLocator.Get<T>();

            switch (component)
            {
                case IMoveComponent move:
                    _moveComponent = move;
                    break;
            }
        }
    }
}
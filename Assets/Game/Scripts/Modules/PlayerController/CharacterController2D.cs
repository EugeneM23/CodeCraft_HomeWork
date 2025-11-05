using System;
using System.Collections.Generic;
using Game.Scripts.Modules.PlayerController.Data;
using Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace Modules.PlayerController
{
    public class CharacterController2D : MonoBehaviour
    {
        public event Action<Vector2> OnGrounded;
        public event Action OnCollisionHit;

        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private CapsuleCollider2D _capsuleCollider;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private PlayerStats _playerStats;

        private List<ITickable> _tickables;
        private IReadOnlyCollection<IVelocity> _velocities;

        private CollisionComponent _collisionComponent;
        private SlopeSlideComponent _slopeSlideComponent;
        private WallSlidingComponent _wallSlidingComponent;
        private ImpulseComponent _impulseComponent;
        private Vector2 _lastFrameVelocity;
        private CharacterFacingComponent _facingComponent;

        private readonly List<Func<bool>> MoveCondition = new();

        public Vector2 LastFrameVelocity => _lastFrameVelocity;
        private ServiceLocator ServiceLocator { get; set; }
        public PlayerStats Stats => _playerStats;

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
        public float DistanceToGround => _collisionComponent.DistanceToGround;
        public SpriteRenderer SpriteRenderer => _spriteRenderer;
        public Vector2 MoveDirection { get; private set; }
        public bool CanMove { get; private set; }

        public int LookDirection => _facingComponent.FacingDirection;

        private void Awake()
        {
            _rigidbody2D.gravityScale = 0;
            _rigidbody2D.interpolation = RigidbodyInterpolation2D.Extrapolate;

            ServiceLocator = new ServiceLocator(this);

            _tickables = ServiceLocator.GetAll<ITickable>();
            _velocities = ServiceLocator.GetAll<IVelocity>();

            _collisionComponent = ServiceLocator.Get<CollisionComponent>();
            _wallSlidingComponent = ServiceLocator.Get<WallSlidingComponent>();
            _impulseComponent = ServiceLocator.Get<ImpulseComponent>();
            _facingComponent = ServiceLocator.Get<CharacterFacingComponent>();
        }

        private void Update()
        {
            foreach (var tickable in _tickables)
                tickable.Tick();

            CanMove = CheckMoveCondition();
        }

        private void FixedUpdate()
        {
            _lastFrameVelocity = _rigidbody2D.linearVelocity;
            Vector2 velocity = Vector2.zero;

            foreach (var v in _velocities)
                velocity += v.GetVelocity();

            _rigidbody2D.linearVelocity = velocity;
        }

        public void SetMoveDirection(Vector2 direction) => MoveDirection = direction;

        public void AddImpulse(Vector2 impulse)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _impulseComponent.AddImpulse(impulse);
        }

        public void TriggerGroundedEvent() => OnGrounded?.Invoke(_lastFrameVelocity);

        public void OnCollisionEnter2D(Collision2D other) => OnCollisionHit?.Invoke();

        public void ResetVelocity() => _rigidbody2D.linearVelocity = Vector2.zero;

        private bool CheckMoveCondition()
        {
            foreach (var item in MoveCondition)
                if (item.Invoke())
                    return false;

            return true;
        }

        public void AddMoveCondition(Func<bool> condition) => MoveCondition.Add(condition);

    }
}
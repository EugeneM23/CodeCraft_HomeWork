using System;
using System.Collections.Generic;
using Codice.Client.BaseCommands.Filters;
using Game.Scripts.Modules.PlayerController.Data;
using Gameplay;
using UnityEngine;

namespace Modules.PlayerController
{
    public class CharacterController2D : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody2D;
        [SerializeField] private CapsuleCollider2D _capsuleCollider;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private LayerMask _layerMask;

        private List<ITickable> _tickables;
        private IReadOnlyCollection<IVelocity> _velocities;

        private CollisionComponent _collisionComponent;
        private SlopeSlideComponent _slopeSlideComponent;
        private WallSlidingComponent _wallSlidingComponent;
        private IMoveComponent _moveComponent;
        private JumpComponent _jumpComponent;
        private ImpulseComponent _impulseComponent;
        private SpriteFlipComponent _spriteFLip;
        private Vector2 _lastFrameVelocity;

        public List<Func<bool>> MoveCondition = new();

        public Vector2 LastFrameVelocity => _lastFrameVelocity;
        public ServiceLocator ServiceLocator { get; private set; }
        public PlayerStats Stats { get; private set; }
        public event Action OnDash;
        public event Action OnSmash;
        public event Action OnJump;
        public event Action<Vector2> OnGrounded;
        public event Action OnCollisionHit;
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
        public float MoveDirection => Input.GetAxisRaw("Horizontal");

        private void Awake()
        {
            _rigidbody2D.gravityScale = 0;
            _rigidbody2D.interpolation = RigidbodyInterpolation2D.Extrapolate;

            ServiceLocator = new ServiceLocator(this);

            Stats = ServiceLocator.Get<PlayerStats>();
            Stats.LayerMask = _layerMask;
            
            _tickables = ServiceLocator.GetAll<ITickable>();
            _velocities = ServiceLocator.GetAll<IVelocity>();

            _collisionComponent = ServiceLocator.Get<CollisionComponent>();
            _wallSlidingComponent = ServiceLocator.Get<WallSlidingComponent>();
            _moveComponent = ServiceLocator.Get<MoveComponent>();
            _jumpComponent = ServiceLocator.Get<JumpComponent>();
            _impulseComponent = ServiceLocator.Get<ImpulseComponent>();
            _spriteFLip = ServiceLocator.Get<SpriteFlipComponent>();
        }

        private void Start()
        {
            _impulseComponent.OnImpulseEnd += _moveComponent.InheritVelocity;
        }

        private void Update()
        {
            foreach (var tickable in _tickables)
                tickable.Tick();
        }

        private void FixedUpdate()
        {
            _lastFrameVelocity = _rigidbody2D.linearVelocity;
            Vector2 velocity = Vector2.zero;

            foreach (var v in _velocities)
                velocity += v.GetVelocity();

            _rigidbody2D.linearVelocity = velocity;
        }

        public void Move(Vector2 direction)
        {
            _moveComponent.Move(direction);
        }

        public void Jump()
        {
            OnJump?.Invoke();
            _jumpComponent.Jump();
        }

        public void TriggerGroundedEvent()
        {
            OnGrounded?.Invoke(_lastFrameVelocity);
        }

        public void Dash(Vector2 impulse)
        {
            OnDash?.Invoke();
            _impulseComponent.AddImpulse(impulse);
        }

        public void Smash(Vector2 impulse)
        {
            OnSmash?.Invoke();
            _impulseComponent.AddImpulse(impulse);
        }

        public void AddImpulse(Vector2 impulse)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _impulseComponent.AddImpulse(impulse);
        }

        public void OnCollisionEnter2D(Collision2D other) => OnCollisionHit?.Invoke();

        public void ResetVelocity() => _rigidbody2D.linearVelocity = Vector2.zero;

        public void AddMoveCondition(Func<bool> condition) => MoveCondition.Add(condition);
    }
}
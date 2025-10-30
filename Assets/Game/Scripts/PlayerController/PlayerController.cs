using System;
using System.Collections.Generic;
using System.IO;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private CapsuleCollider2D _collider;

        private IReadOnlyCollection<ITickable> _tickables;
        private IReadOnlyCollection<IVelocity> _velocities;

        private ServiceLocator _locator;
        private PlayerStats _stats;
        public event Action OnHit;
        public event Action OnJump;
        public bool IsOnStairs { get; set; }

        public bool IsGrounded => _locator.Get<CollisionComponent>().IsGrounded;
        public bool IsCeilingHit => _locator.Get<CollisionComponent>().IsCeilingHit;
        public Vector2 SurfaceNormal => _locator.Get<CollisionComponent>().SurfaceNormal;
        public Vector2 Velocity => _rigidbody.linearVelocity;

        public bool IsOnSlope =>
            Vector2.Angle(Vector2.right, _locator.Get<CollisionComponent>().SurfaceNormal) > 91;

        public bool IsOnWall => _locator.Get<CollisionComponent>().IsOnWall;
        public bool IsOnWallSliding => _locator.Get<CollisionComponent>().IsOnWallSliding;

        public bool IsGrabbingLedge => _locator.Get<LedgeGrabComponent>().IsGrabbing;
        public CapsuleCollider2D Collider => _collider;
        public PlayerStats Stats => _stats;
        public Vector2 MoveDirection => _locator.Get<MoveController>().CurrentDirection;
        public int WallDirection => _locator.Get<CollisionComponent>().WallDirection;
        public float DistanceToGround => _locator.Get<CollisionComponent>().DistanceToGround;

        private void Awake()
        {
            _rigidbody.gravityScale = 0;

            _locator = new ServiceLocator(this);

            _stats = _locator.Get<PlayerStats>();
            _tickables = _locator.GetAll<ITickable>();
            _velocities = _locator.GetAll<IVelocity>();
        }

        private void Update()
        {
            foreach (var t in _tickables)
                t.Tick();
        }

        private void FixedUpdate()
        {
            Vector2 velocity = Vector2.zero;

            foreach (var v in _velocities)
                velocity += v.GetVelocity();

            _rigidbody.linearVelocity = velocity;
        }

        public void Move(Vector2 direction) => _locator.Get<MoveComponent>().Move(direction);

        public void Jump()
        {
            _locator.Get<JumpComponent>().Jump();
            OnJump?.Invoke();
        }

        public void OnCollisionEnter2D(Collision2D other) => Hit();
        public void Hit() => OnHit?.Invoke();

        public void AddImpulse(Vector2 impulse)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _locator.Get<ImpulseComponent>().AddImpulse(impulse);
        }

        public void Restvelocity() => _rigidbody.linearVelocity = Vector2.zero;
    }
}
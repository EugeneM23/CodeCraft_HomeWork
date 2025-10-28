using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private ScriptableStats _stats;
        [SerializeField] private CapsuleCollider2D _collider;

        private IReadOnlyCollection<ITickable> _tickables;
        private IReadOnlyCollection<IVelocity> _velocities;

        private ServiceLocator _locator;
        public event Action OnHit;
        public bool IsOnStairs { get; set; }

        public bool IsGrounded => _locator.Get<CollisionComponent>().IsGrounded;
        public bool IsCeilingHit => _locator.Get<CollisionComponent>().IsCeilingHit;
        public Vector2 SurfaceNormal => _locator.Get<CollisionComponent>().SurfaceNormal;
        public Vector2 Velocity => _rigidbody.linearVelocity;
        public bool IsOnSlope => Vector2.Angle(Vector2.right, _locator.Get<CollisionComponent>().SurfaceNormal) > 89;
        public bool IsOnWall => _locator.Get<WallSlidingComponent>().IsOnWall;
        public bool IsGrabbingLedge => _locator.Get<LedgeGrabComponent>().IsGrabbing;
        public CapsuleCollider2D Collider => _collider;
        public ScriptableStats Stats => _stats;
        public Vector2 MoveDirection => _locator.Get<MoveController>().CurrentDirection;

        private void Start()
        {
            _rigidbody.gravityScale = 0;

            _locator = new ServiceLocator(this);
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

        public void Jump() => _locator.Get<JumpComponent>().Jump();

        public void OnCollisionEnter2D(Collision2D other) => Hit();
        public void Hit() => OnHit?.Invoke();
        public void AddImpulse(Vector2 impulse) => _locator.Get<GravityComponent>().AddImpulse(impulse);
        public void Restvelocity() => _rigidbody.linearVelocity = Vector2.zero;
    }
}
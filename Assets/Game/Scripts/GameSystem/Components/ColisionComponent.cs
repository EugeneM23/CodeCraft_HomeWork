using System;
using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class CollisionComponent : IFixedTickable
    {
        public event Action OnGrounded;
        public event Action OnFlying;

        public bool IsGrounded { get; private set; }
        public RaycastHit2D LastHit { get; private set; }

        private readonly CircleCollider2D _collider;
        private readonly LayerMask _groundLayer;

        private bool _frameState;

        public CollisionComponent(CircleCollider2D collider, LayerMask groundLayer)
        {
            _collider = collider;
            _groundLayer = groundLayer;
        }
        public void FixedTick()
        {
            _frameState = IsGrounded;
            LastHit = Raycast();
            IsGrounded = LastHit.collider != null;

            if (!_frameState && IsGrounded) OnGrounded?.Invoke();
            else if (_frameState && !IsGrounded) OnFlying?.Invoke();
        }

        private RaycastHit2D Raycast()
        {
            Vector2 position = _collider.bounds.center;
            Vector2 groundCheckPos = new Vector2(position.x, _collider.bounds.min.y);
            return Physics2D.CircleCast(groundCheckPos, _collider.radius, Vector2.down, 0.1f, _groundLayer);
        }

        public bool IsGround() => IsGrounded;

        public Vector2 GetHitNormal() => LastHit.normal;
    }
}
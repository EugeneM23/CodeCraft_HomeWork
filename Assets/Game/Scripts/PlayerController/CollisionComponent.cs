using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class CollisionComponent
    {
        private readonly CapsuleCollider2D _collider;
        private readonly ScriptableStats _stats;

        public CollisionComponent(CapsuleCollider2D collider, ScriptableStats stats)
        {
            _collider = collider;
            _stats = stats;
        }

        public bool IsGrounded { get; private set; }
        public bool IsCeilingHit { get; private set; }

        public void DetectCollisions()
        {
            bool groundHit = Physics2D.CapsuleCast(
                _collider.bounds.center,
                _collider.size,
                _collider.direction,
                0,
                Vector2.down,
                _stats.GrounderDistance,
                _stats.PlayerLayer
            );

            bool ceilingHit = Physics2D.CapsuleCast(
                _collider.bounds.center,
                _collider.size,
                _collider.direction,
                0,
                Vector2.up,
                _stats.GrounderDistance,
                _stats.PlayerLayer
            );

            IsCeilingHit = ceilingHit;

            if (!IsGrounded && groundHit)
                IsGrounded = true;
            else if (IsGrounded && !groundHit)
                IsGrounded = false;
        }

        private void DrawDebug(Vector2 origin, bool groundHit, bool ceilingHit)
        {
            float halfHeight = _collider.size.y / 2f;
            Vector2 bottomPoint = origin + Vector2.down * halfHeight;
            Vector2 topPoint = origin + Vector2.up * halfHeight;

            Debug.DrawRay(bottomPoint, Vector2.down * _stats.GrounderDistance, groundHit ? Color.green : Color.red);
            Debug.DrawRay(topPoint, Vector2.up * _stats.GrounderDistance, ceilingHit ? Color.green : Color.red);
        }
    }
}
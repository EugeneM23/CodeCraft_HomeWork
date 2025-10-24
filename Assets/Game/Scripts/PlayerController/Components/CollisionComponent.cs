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

            Physics2D.queriesStartInColliders = false;
        }

        public bool IsGrounded { get; private set; }
        public bool IsCeilingHit { get; private set; }

        public Vector2 SurfaceNormal { get; private set; }

        public void DetectCollisions()
        {
            GetGround();
            GetGroundNormal();
        }

        private void GetGround()
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

        private void GetGroundNormal()
        {
            Vector2 origin = _collider.bounds.center;
            float rayLength = _collider.bounds.extents.y + 1;

            VectorDebug.DrawArrow(1, origin, Vector2.down * rayLength, Color.yellow);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, rayLength, _stats.PlayerLayer);

            if (hit.collider != null)
            {
                Vector2 normal = hit.normal.normalized;
                Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

                VectorDebug.DrawArrow(2, origin, normal * 1, Color.blue);
                VectorDebug.DrawArrow(3, origin, tangent * 1, Color.green);
                VectorDebug.DrawArrow(4, origin, -tangent * 1, Color.red);

                SurfaceNormal = hit.normal;
            }
            else
            {
                SurfaceNormal = Vector2.up;
            }
        }
    }
}
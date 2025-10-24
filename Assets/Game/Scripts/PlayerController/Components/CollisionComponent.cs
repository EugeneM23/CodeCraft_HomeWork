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

        private Vector2 lastSurfaceNormal = Vector2.up;

        private void GetGroundNormal()
        {
            if (!IsGrounded) return;
            
            Vector2 origin = _collider.bounds.center;
            float rayLength = _collider.bounds.extents.y + 5f;

            // Направление второго луча
            Vector2 directionToSurface = -lastSurfaceNormal;

            Debug.DrawLine(origin, origin + directionToSurface * rayLength, Color.green);
            RaycastHit2D hit = Physics2D.Raycast(origin, directionToSurface, _collider.bounds.extents.y + 0.2f,
                _stats.PlayerLayer);

            if (hit.collider != null)
            {
                SurfaceNormal = hit.normal.normalized;
                lastSurfaceNormal = hit.normal.normalized;
                Debug.Log("Green Hit Normal: " + hit.normal);
            }
            else
            {
                // Если зеленый не попал — запускаем красный вниз
                Debug.DrawLine(origin, origin + Vector2.down * rayLength, Color.red);
                RaycastHit2D hitToSurface = Physics2D.Raycast(origin, Vector2.down, rayLength, _stats.PlayerLayer);

                if (hitToSurface.collider != null)
                {
                    SurfaceNormal = hitToSurface.normal.normalized;
                    lastSurfaceNormal = hitToSurface.normal.normalized;
                    Debug.Log("Red Hit Normal: " + hitToSurface.normal);
                }
                else
                {
                    SurfaceNormal = Vector2.zero;
                }
            }
        }
    }
}
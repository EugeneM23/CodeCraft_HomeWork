using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class CollisionComponentSave
    {
        
        private readonly CapsuleCollider2D _collider;
        private readonly ScriptableStats _stats;
        private readonly PlayerController _player;
        public bool IsGrounded { get; private set; }
        public bool IsCeilingHit { get; private set; }

        public Vector2 SurfaceNormal { get; private set; }

        public CollisionComponentSave(CapsuleCollider2D collider, ScriptableStats stats, PlayerController player)
        {
            _collider = collider;
            _stats = stats;
            _player = player;
        }

        public void DetectCollisions()
        {
            IsCeilingHit = CheckCeilingCollision();
            IsGrounded = CheckGroundCollision();
            GetGroundNormal();
        }

        private bool CheckGroundCollision()
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

            return groundHit;
        }

        public bool CheckCeilingCollision()
        {
            bool ceilingHit = Physics2D.CapsuleCast(
                _collider.bounds.center,
                _collider.size,
                _collider.direction,
                0,
                Vector2.up,
                _stats.GrounderDistance,
                _stats.PlayerLayer
            );

            if (ceilingHit && _player._frameVelocity.y > 0)
                return true;

            return false;
        }

        private Vector2 lastSurfaceNormal = Vector2.up;

        private void GetGroundNormal()
        {
            if (!IsGrounded)
            {
                SurfaceNormal = Vector2.zero;
                return;
            }

            Vector2 origin = _collider.bounds.center;
            Vector2 directionToSurface = -lastSurfaceNormal;
            float shortRayLength = _collider.bounds.extents.y + 5;
            float longRayLength = _collider.bounds.extents.y + 5f;

            // Первый луч (зеленый) - по направлению к последней нормали
            Debug.DrawLine(origin, origin + directionToSurface * shortRayLength, Color.green);
            RaycastHit2D hit = Physics2D.Raycast(origin, directionToSurface, shortRayLength, _stats.PlayerLayer);

            if (hit.collider != null)
            {
                SurfaceNormal = hit.normal.normalized;
                lastSurfaceNormal = hit.normal.normalized;
                return;
            }

            // Второй луч (красный) - строго вниз
            Debug.DrawLine(origin, origin + Vector2.down * longRayLength, Color.red);
            RaycastHit2D hitDown = Physics2D.Raycast(origin, Vector2.down, longRayLength, _stats.PlayerLayer);

            if (hitDown.collider != null)
            {
                SurfaceNormal = hitDown.normal.normalized;
                lastSurfaceNormal = hitDown.normal.normalized;
            }
            else
            {
                SurfaceNormal = Vector2.zero;
            }
        }
    }
}
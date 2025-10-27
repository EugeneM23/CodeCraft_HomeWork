using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class CollisionComponent
    {
        private readonly CapsuleCollider2D _collider;
        private readonly ScriptableStats _stats;
        private readonly PlayerController _player;

        public CollisionComponent(CapsuleCollider2D collider, ScriptableStats stats, PlayerController player)
        {
            _collider = collider;
            _stats = stats;
            _player = player;

            Physics2D.queriesStartInColliders = false;
        }

        public bool IsGrounded { get; private set; }
        public bool IsCeilingHit { get; private set; }

        public Vector2 SurfaceNormal { get; private set; }

        public void DetectCollisions()
        {
            IsCeilingHit = CheckCeilingCollision();
            IsGrounded = CheckGroundCollision() && SurfaceNormal == Vector2.zero;
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

        private bool CheckCeilingCollision()
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

        private void GetGroundNormal()
        {
            if (!IsGrounded)
            {
                SurfaceNormal = Vector2.up;
                return;
            }

            Vector2 origin = _collider.bounds.center;
            float longRayLength = _collider.bounds.extents.y + 1f;

            /*if (ScanLowerSurface())
                return;*/

            Debug.DrawLine(origin, origin + Vector2.down * longRayLength, Color.red);
            RaycastHit2D hitDown = Physics2D.Raycast(origin, Vector2.down, longRayLength, _stats.PlayerLayer);

            if (hitDown.collider != null)
            {
                SurfaceNormal = hitDown.normal.normalized;
                return;
            }


            SurfaceNormal = Vector2.zero;
        }

        private bool ScanLowerSurface()
        {
            float shortRayLength = 2f;

            Vector2 origin2 = new Vector2(_collider.bounds.center.x, _collider.bounds.min.y);
            Vector2 direction = new Vector2(_player.FrameInput.Move.x, 0);

            Debug.DrawLine(origin2, origin2 + direction, Color.green);
            RaycastHit2D hit = Physics2D.Raycast(origin2, direction, shortRayLength, _stats.PlayerLayer);
            if (hit.collider != null)
            {
                SurfaceNormal = hit.normal.normalized;
                return true;
            }

            return false;
        }
    }
}
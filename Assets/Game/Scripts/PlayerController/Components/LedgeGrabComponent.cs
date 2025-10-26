using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class LedgeGrabComponent
    {
        private readonly CapsuleCollider2D _collider;
        private readonly PlayerController _player;
        private readonly LayerMask _ledgeLayer;

        private const float CheckDistance = 0.5f;
        private const float MaxNormalDeviation = 80f;

        private bool _isGrabbing;
        private Vector2 _leftCornerPoint;
        private Vector2 _rightCornerPoint;

        public bool IsGrabbing => _isGrabbing;

        public LedgeGrabComponent(CapsuleCollider2D collider, PlayerController player, LayerMask ledgeLayer)
        {
            _collider = collider;
            _player = player;
            _ledgeLayer = ledgeLayer;
        }

        public void CheckLedges()
        {
            if (_player.Velocity.y > 0) return;

            Vector2 center = (Vector2)_player.transform.position + _collider.offset;
            float halfHeight = _collider.size.y / 2f;
            float halfWidth = _collider.size.x / 2f;

            Vector2 leftPos = new Vector2(center.x - halfWidth, center.y + halfHeight);
            Vector2 rightPos = new Vector2(center.x + halfWidth, center.y + halfHeight);

            bool left = CheckCorner(leftPos, -1, out _leftCornerPoint);
            bool right = CheckCorner(rightPos, 1, out _rightCornerPoint);

            _isGrabbing = left || right;
        }

        private bool CheckCorner(Vector2 startPos, float direction, out Vector2 corner)
        {
            corner = Vector2.zero;

            // 1. WallRay
            float dynamicDistance = CheckDistance + Mathf.Min(_player.Velocity.magnitude * Time.fixedDeltaTime, 0.3f);
            Vector2 rayDir = Vector2.right * direction;
            RaycastHit2D wallHit = Physics2D.Raycast(startPos, rayDir, dynamicDistance, _ledgeLayer);
            Debug.DrawLine(startPos, startPos + rayDir * dynamicDistance, wallHit ? Color.red : Color.gray);
            if (!wallHit) return false;

            // 2. TopCheckPos — чуть выше wallHit и немного вперед, чтобы попасть на платформу
            Vector2 topCheckPos = wallHit.point + new Vector2(0.1f * direction, 0.5f);

            // 3. Raycast вниз, чтобы найти кромку
            RaycastHit2D topHit = Physics2D.Raycast(topCheckPos, Vector2.down, 0.4f, _ledgeLayer);
            Debug.DrawLine(topCheckPos, topCheckPos + Vector2.down * 0.4f, topHit ? Color.blue : Color.gray);
            if (!topHit) return false;

            // 4. Проверяем нормали
            Vector2 idealWallNormal = direction == 1 ? Vector2.left : Vector2.right;
            Vector2 idealTopNormal = Vector2.up;

            float wallAngle = Vector2.Angle(wallHit.normal, idealWallNormal);
            float topAngle = Vector2.Angle(topHit.normal, idealTopNormal);

            if (wallAngle > MaxNormalDeviation || topAngle > MaxNormalDeviation)
                return false;

            // 5. Угол найден
            corner = topHit.point;
            return true;
        }



        public void ReleaseGrab() => _isGrabbing = false;

        public void DrawDebug()
        {
            if (!_isGrabbing) return;

            Debug.DrawLine(_player.transform.position, _leftCornerPoint, Color.green);
            Debug.DrawLine(_player.transform.position, _rightCornerPoint, Color.green);
        }
    }
}

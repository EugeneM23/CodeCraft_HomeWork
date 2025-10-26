using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class LedgeGrabComponent
    {
        private readonly CapsuleCollider2D _collider;
        private readonly PlayerController _player;
        private readonly LayerMask _ledgeLayer;

        private const float CheckDistance = 0.5f;
        private const float OffsetFromTop = 0.2f;
        private const float MaxNormalDeviation = 15f;

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
            float halfHeight = _collider.size.y / 2;
            float halfWidth = _collider.size.x / 2;
            float y = center.y + halfHeight - OffsetFromTop;

            Vector2 leftPos = new(center.x - halfWidth, y);
            Vector2 rightPos = new(center.x + halfWidth, y);

            bool left = CheckCorner(leftPos, -1, out _leftCornerPoint);
            bool right = CheckCorner(rightPos, 1, out _rightCornerPoint);

            _isGrabbing = left || right;
        }

        private bool CheckCorner(Vector2 startPos, float direction, out Vector2 corner)
        {
            corner = Vector2.zero;

            // 1. Луч к стене
            RaycastHit2D wallHit = Physics2D.Raycast(startPos, Vector2.right * direction, CheckDistance, _ledgeLayer);
            Debug.DrawLine(startPos, startPos + Vector2.right * direction * CheckDistance, wallHit ? Color.red : Color.gray);

            if (!wallHit) return false;

            // 2. Точка, где рука должна цепляться (чуть выше стены)
            Vector2 grabPoint = wallHit.point + new Vector2(0, 0.5f);

            // 3. Проверяем: над этой точкой есть земля (платформа)
            RaycastHit2D topHit = Physics2D.Raycast(grabPoint, Vector2.down, 0.3f, _ledgeLayer);
            Debug.DrawLine(grabPoint, grabPoint + Vector2.down * 1f, topHit ? Color.blue : Color.gray);

            if (!topHit) return false;

            // 4. Проверяем, что прямо перед grabPoint нет стены (то есть зацеп возможен)
            Vector2 forwardCheck = grabPoint + Vector2.right * direction * 0.2f;
            bool blocked = Physics2D.OverlapCircle(forwardCheck, 0.05f, _ledgeLayer);
            Debug.DrawLine(grabPoint, forwardCheck, blocked ? Color.yellow : Color.green);

            if (blocked) return false;

            // Всё ок — угол найден
            corner = topHit.point;
            return true;        }


        public void ReleaseGrab() => _isGrabbing = false;

        public void DrawDebug()
        {
            if (!_isGrabbing) return;

            Debug.DrawLine(_player.transform.position, _leftCornerPoint, Color.green);
            Debug.DrawLine(_player.transform.position, _rightCornerPoint, Color.green);
        }
    }
}
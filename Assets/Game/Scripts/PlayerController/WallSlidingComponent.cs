using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class WallSlidingComponent
    {
        private readonly CollisionComponent _collision;
        private readonly PlayerController _player;
        private readonly CapsuleCollider2D _collider;
        private readonly ScriptableStats _stats;

        public bool IsOnWall { get; private set; }
        public Vector2 WallNormal { get; private set; }

        public WallSlidingComponent(CapsuleCollider2D collider, PlayerController player, ScriptableStats stats,
            CollisionComponent collision)
        {
            _collider = collider;
            _collision = collision;
            _player = player;
            _stats = stats;
        }

        public Vector2 ScanWall()
        {
            // Сканируем слева и справа
            RaycastHit2D leftHit = Scan(Vector2.left);
            RaycastHit2D rightHit = Scan(Vector2.right);

            float moveX = _player.FrameInput.Move.x;

            // Проверяем левую стену
            if (leftHit.collider != null && IsWallAngle(leftHit))
            {
                WallNormal = leftHit.normal;
                IsOnWall = true; // Активный слайд только при нажатии влево

                if (IsOnWall&& _player.FrameInput.Move.x < 0)
                    return new Vector2(0, _player.Velocity.y / 1f * -1);
            }
            // Проверяем правую стену
            else if (rightHit.collider != null && IsWallAngle(rightHit))
            {
                WallNormal = rightHit.normal;
                IsOnWall = true;

                if (IsOnWall && _player.FrameInput.Move.x > 0)
                    return new Vector2(0, _player.Velocity.y / 1f * -1);
            }
            else
            {
                IsOnWall = false;
                WallNormal = Vector2.zero;
            }
            IsOnWall = false;

            return Vector2.zero;
        }

        private RaycastHit2D Scan(Vector2 direction)
        {
            Vector2 origin = _collider.bounds.center;
            float length = 1f;

            Debug.DrawLine(origin, origin + direction * length, Color.cyan);

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, length, _stats.PlayerLayer);

            if (hit.collider == _collider)
                return default;

            return hit;
        }

        private bool IsWallAngle(RaycastHit2D hit)
        {
            float angle = Vector2.Angle(Vector2.up, hit.normal);
            return Mathf.Abs(angle - 90f) < 10f;
        }
    }
}
using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class WallSlidingComponent : IVelocity
    {
        private readonly PlayerController _player;

        public bool IsOnWall { get; private set; }
        public Vector2 WallNormal { get; private set; }

        public WallSlidingComponent(PlayerController player) => _player = player;

        public Vector2 GetVelocity()
        {
            RaycastHit2D leftHit = Scan(Vector2.left);
            RaycastHit2D rightHit = Scan(Vector2.right);


            if (leftHit.collider != null && IsWallAngle(leftHit))
            {
                WallNormal = leftHit.normal;
                IsOnWall = true; // Активный слайд только при нажатии влево

                if (IsOnWall && _player.FrameInput.Move.x < 0 && _player.Velocity.y < 0)
                    return new Vector2(0, _player.Velocity.y / 1f * -1);
            }
            else if (rightHit.collider != null && IsWallAngle(rightHit))
            {
                WallNormal = rightHit.normal;
                IsOnWall = true;

                if (IsOnWall && _player.FrameInput.Move.x > 0 && _player.Velocity.y < 0)
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
            Vector2 origin = _player.Collider.bounds.center;
            float length = 0.7f;

            Debug.DrawLine(origin, origin + direction * length, Color.cyan);

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, length, _player.Stats.PlayerLayer);

            if (hit.collider == _player.Collider)
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
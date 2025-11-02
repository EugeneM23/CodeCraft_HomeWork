using UnityEngine;

namespace Modules.PlayerController
{
    internal class CollisionComponent : ITickable
    {
        private readonly CharacterController2D _character;
        private bool _wasGroundedLastFrame;

        public Vector2 SurfaceNormal { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool IsCeilingHit { get; private set; }
        public bool IsOnWall { get; private set; }
        public int WallDirection { get; private set; }
        public float DistanceToGround { get; private set; }
        public bool IsOnSlope => Vector2.Angle(Vector2.right, SurfaceNormal) > 91;

        public CollisionComponent(CharacterController2D character)
        {
            _character = character;
            _wasGroundedLastFrame = false;
            Physics2D.queriesStartInColliders = false;
        }

        public void Tick()
        {
            CheckGround();
            CheckCeiling();
            CheckWall();
            GetGroundDistance();
        }

        private void GetGroundDistance()
        {
            RaycastHit2D hit = Physics2D.Raycast(
                _character.Collider.bounds.min,
                Vector2.down,
                float.MaxValue,
                _character.Stats.LayerMask
            );

            if (hit.collider != null)
                DistanceToGround = hit.distance;
            else
                DistanceToGround = float.MaxValue;
        }

        private void CheckGround()
        {
            RaycastHit2D hit = Physics2D.Raycast(
                _character.Collider.bounds.center,
                Vector2.down,
                2,
                _character.Stats.LayerMask
            );

            bool isGroundedNow = hit.collider != null;

            if (isGroundedNow && !_wasGroundedLastFrame)
                _character.TriggerGroundedEvent();

            IsGrounded = isGroundedNow;
            _wasGroundedLastFrame = isGroundedNow;

            SurfaceNormal = hit.collider != null ? hit.normal : Vector2.zero;
        }

        private void CheckCeiling()
        {
            if (_character.Velocity.y <= 0)
            {
                IsCeilingHit = false;
                return;
            }

            RaycastHit2D hit = Physics2D.Raycast(
                _character.Collider.bounds.center,
                Vector2.up,
                0.2f,
                _character.Stats.LayerMask
            );

            IsCeilingHit = hit.collider != null;
        }

        private void CheckWall()
        {
            RaycastHit2D leftHit = Physics2D.Raycast(
                _character.Collider.bounds.center,
                Vector2.left,
                0.7f,
                _character.Stats.LayerMask
            );

            RaycastHit2D rightHit = Physics2D.Raycast(
                _character.Collider.bounds.center,
                Vector2.right,
                0.7f,
                _character.Stats.LayerMask
            );

            if (leftHit.collider != null && IsWall(leftHit))
            {
                IsOnWall = true;
                WallDirection = -1;
            }
            else if (rightHit.collider != null && IsWall(rightHit))
            {
                IsOnWall = true;
                WallDirection = 1;
            }
            else
            {
                IsOnWall = false;
                WallDirection = 0;
            }
        }

        private bool IsWall(RaycastHit2D hit)
        {
            float angle = Vector2.Angle(Vector2.up, hit.normal);
            return Mathf.Abs(angle - 90f) < 10f;
        }
    }
}
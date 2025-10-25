using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class SlopeSlideComponent
    {
        private readonly PlayerController _player;
        private readonly CollisionComponent _collision;
        private readonly float _maxAngle;
        private readonly float _slideForce;

        public SlopeSlideComponent(CollisionComponent collision, PlayerController player, float maxAngle = 45f, float slideForce = 50f)
        {
            _collision = collision;
            _player = player;
            _maxAngle = maxAngle;
            _slideForce = slideForce;
        }

        public Vector2 GetSlideVelocity()
        {
            if (!_collision.IsGrounded) return Vector2.zero;

            Vector2 normal = _collision.SurfaceNormal;
            if (normal == Vector2.zero) return Vector2.zero;

            float angle = Vector2.Angle(normal, Vector2.up);
            if (angle > _maxAngle) return Vector2.zero;
            if (_player.FrameInput.Move )
            {
                
            }

            Vector2 slideDir = Vector3.Cross(Vector3.forward, normal);
            slideDir = new Vector2(slideDir.x, slideDir.y).normalized;

            if (slideDir.y > 0) slideDir = -slideDir;

            float multiplier = angle / _maxAngle;

            return slideDir * (_slideForce * multiplier);
        }
    }
}
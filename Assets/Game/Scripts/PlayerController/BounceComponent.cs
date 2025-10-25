using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class BounceComponent
    {
        private readonly CollisionComponent _collision;
        private readonly PlayerController _player;
        private readonly float _flatSurfaceAngle;
        private readonly float _bounceMultiplier;

        private bool _wasGrounded;
        private bool _canBounce;
        private float _landingVelocity;

        public BounceComponent(CollisionComponent collision, PlayerController player, float flatSurfaceAngle = 5f,
            float bounceMultiplier = 0.5f)
        {
            _collision = collision;
            _player = player;
            _flatSurfaceAngle = flatSurfaceAngle;
            _bounceMultiplier = bounceMultiplier;
            _canBounce = true;
        }

        public float GetBounceImpulse()
        {
            bool isGroundedNow = _collision.IsGrounded;

            if (!_wasGrounded && isGroundedNow)
            {
                Debug.Log("Grounded ---------------------------");
                Debug.Log("Landed! Velocity: " + _player.Velocity.y);
                Vector2 normal = _collision.SurfaceNormal;
                float angle = Vector2.Angle(normal, Vector2.up);
                Debug.Log("Surface angle: " + angle + " CanBounce: " + _canBounce);


                _landingVelocity = _player.Velocity.y;
                _canBounce = false;
                _wasGrounded = true;

                float bounce = Mathf.Abs(_landingVelocity) * _bounceMultiplier;
                Debug.Log("BOUNCE! Impulse: " + bounce);
                return _bounceMultiplier;
            }

            if (!isGroundedNow && _wasGrounded)
            {
                _canBounce = true;
                Debug.Log("Left ground - can bounce again");
            }

            _wasGrounded = isGroundedNow;
            return 0f;
        }
    }
}
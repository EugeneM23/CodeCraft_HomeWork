using UnityEngine;

namespace PlayerController
{
    public class SlopeSlideComponent : IVelocity
    {
        private readonly PlayerController _player;

        private const float MaxAngle = 91f;
        private const float Acceleration = 50f;
        private const float Damping = 5f;
        private const float MinVelocityThreshold = 0.01f;

        private Vector2 _slideVelocity;
        private bool _wasInAir;

        public SlopeSlideComponent(PlayerController player) => _player = player;

        public Vector2 GetVelocity()
        {
            
            if (!_player.IsGrounded)
                return Vector2.zero;

            float slopeAngle = GetSlopeAngle();

            if (slopeAngle < 1f)
                return ApplyDamping();

            return HandleSlopeSlide(slopeAngle);
        }

        private float GetSlopeAngle()
        {
            return Vector2.Angle(_player.SurfaceNormal, Vector2.up);
        }


        private Vector2 HandleSlopeSlide(float slopeAngle)
        {
            Vector2 slideDirection = CalculateSlideDirection();

            if (IsPlayerMovingAgainstSlope(slideDirection))
            {
                ResetSlide();
                return Vector2.zero;
            }

            if (_wasInAir)
                InitializeSlideFromAir(slopeAngle);

            AccelerateSlide(slideDirection, slopeAngle);
            return _slideVelocity;
        }

        private Vector2 CalculateSlideDirection()
        {
            Vector2 direction = Vector3.Cross(Vector3.forward, _player.SurfaceNormal).normalized;
            return direction.y > 0 ? -direction : direction;
        }

        private bool IsPlayerMovingAgainstSlope(Vector2 slideDirection)
        {
            Vector2 input = _player.MoveDirection;

            if (Mathf.Abs(input.x) < MinVelocityThreshold)
                return false;

            bool slopeGoesRight = slideDirection.x > 0;
            bool playerGoesRight = input.x > 0;

            return slopeGoesRight != playerGoesRight;
        }

        private void InitializeSlideFromAir(float slopeAngle)
        {
            float factor = Mathf.Clamp01(slopeAngle / MaxAngle);
            _slideVelocity = _player.Velocity * factor;
            _wasInAir = false;
        }

        private void AccelerateSlide(Vector2 slideDirection, float slopeAngle)
        {
            float factor = Mathf.Clamp01(slopeAngle / MaxAngle);
            _slideVelocity += slideDirection * (Acceleration * factor * Time.deltaTime);
        }

        private Vector2 ApplyDamping()
        {
            _slideVelocity *= Mathf.Exp(-Damping * Time.deltaTime);

            if (_slideVelocity.magnitude < MinVelocityThreshold)
                _slideVelocity = Vector2.zero;

            return _slideVelocity;
        }

        private void ResetSlide()
        {
            _slideVelocity = Vector2.zero;
            _wasInAir = false;
        }
    }
}
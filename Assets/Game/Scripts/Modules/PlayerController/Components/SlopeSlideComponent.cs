using UnityEngine;

namespace Modules.PlayerController
{
    internal class SlopeSlideComponent : IVelocity
    {
        private const float MAX_SLOPE_ANGLE = 91f;
        private const float SLIDE_ACCELERATION = 50f;
        private const float DAMPING = 4f;
        private const float MIN_VELOCITY = 0.01f;
        private const float MIN_ANGLE = 1f;

        private readonly CharacterController2D _character;
        private Vector2 _slideVelocity;
        private bool _wasGroundedLastFrame;

        public SlopeSlideComponent(CharacterController2D character)
        {
            _character = character;
            _character.OnJump += () => _slideVelocity = Vector2.zero;
        }

        public Vector2 GetVelocity()
        {
            bool isGrounded = _character.IsGrounded;
            bool justLeftGround = _wasGroundedLastFrame && !isGrounded;

            // Запоминаем состояние для следующего кадра
            _wasGroundedLastFrame = isGrounded;

            if (!isGrounded || !_character.IsOnSlope)
                IsSlidingOnslope = false;

            // Если только что покинули землю - сохраняем скорость скольжения
            if (justLeftGround)
                return _slideVelocity;

            if (!isGrounded)
                return _slideVelocity;

            // В воздухе - возвращаем сохраненную скорость без изменений

            float angle = Vector2.Angle(_character.SurfaceNormal, Vector2.up);

            // Плоская поверхность - применяем только затухание
            if (angle < MIN_ANGLE)
                return ApplyDamping();

            // Игрок двигается против склона - тормозим скольжение
            if (IsMovingAgainstSlope())
                return ApplyBraking();

            // Вычисляем направление и ускорение скольжения
            Vector2 slideDirection = GetSlideDirection();
            float slideStrength = Mathf.Clamp01(angle / MAX_SLOPE_ANGLE);

            _slideVelocity += slideDirection * (SLIDE_ACCELERATION * slideStrength * Time.deltaTime);

            return _slideVelocity;
        }

        private Vector2 GetSlideDirection()
        {
            // Перпендикуляр к нормали поверхности
            Vector2 perpendicular = Vector3.Cross(Vector3.forward, _character.SurfaceNormal).normalized;

            // Направляем вниз по склону
            return perpendicular.y > 0 ? -perpendicular : perpendicular;
        }

        private bool IsMovingAgainstSlope()
        {
            float input = _character.Velocity.x;

            if (Mathf.Abs(input) < MIN_VELOCITY)
            {
                IsSlidingOnslope = false;
                return false;
            }

            Vector2 slideDirection = GetSlideDirection();

            // Проверяем, двигается ли игрок в противоположную сторону от склона
            bool isMovingAgainstSlope = Mathf.Sign(input) != Mathf.Sign(slideDirection.x);

            if (isMovingAgainstSlope && Mathf.Abs(_slideVelocity.x) > 1f)
                IsSlidingOnslope = true;

            return isMovingAgainstSlope;
        }

        public bool IsSlidingOnslope { get; set; }

        private Vector2 ApplyDamping()
        {
            if (Mathf.Abs(_character.Velocity.x) > 0)
            {
                _slideVelocity = Vector2.zero;
                return _slideVelocity;
            }

            _slideVelocity *= Mathf.Exp(-DAMPING * Time.deltaTime);

            if (_slideVelocity.magnitude < MIN_VELOCITY)
                _slideVelocity = Vector2.zero;

            return _slideVelocity;
        }

        private Vector2 ApplyBraking()
        {
            float inputStrength = Mathf.Abs(_character.Velocity.x);
            float brakingForce = DAMPING * 2f * inputStrength; // Торможение сильнее затухания

            _slideVelocity *= Mathf.Exp(-brakingForce * Time.deltaTime);

            if (_slideVelocity.magnitude < MIN_VELOCITY)
                _slideVelocity = Vector2.zero;

            return _slideVelocity;
        }
    }
}
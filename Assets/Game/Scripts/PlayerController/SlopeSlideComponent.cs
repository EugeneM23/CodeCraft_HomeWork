using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class SlopeSlideComponent
    {
        private readonly PlayerController _player;
        private readonly CollisionComponent _collision;
        private readonly float _maxAngle = 89f;
        private readonly float _acceleration = 30f;
        private readonly float _damping = 5f;
        private Vector2 _currentSlideVelocity;
        private bool _wasInAir;

        public SlopeSlideComponent(CollisionComponent collision, PlayerController player)
        {
            _collision = collision;
            _player = player;
        }

        public Vector2 GetSlideVelocity()
        {
            Vector2 normal = _collision.SurfaceNormal;
            bool grounded = _collision.IsGrounded;

            if (grounded)
            {
                float angle = Vector2.Angle(normal, Vector2.up);

                if (angle >= 1f)
                {
                    Vector2 slideDir = Vector3.Cross(Vector3.forward, normal).normalized;
                    if (slideDir.y > 0)
                        slideDir = -slideDir;

                    Vector2 input = _player.FrameInput.Move;
                    bool slopeRight = slideDir.x > 0;
                    bool playerRight = input.x > 0;

                    // Если игрок идет против склона — обнуляем скорость
                    if (Mathf.Abs(input.x) > 0.01f && slopeRight != playerRight)
                    {
                        _currentSlideVelocity = Vector2.zero;
                        _wasInAir = false;
                        return Vector2.zero;
                    }

                    if (_wasInAir)
                    {
                        float slopeFactor = Mathf.Clamp01(angle / _maxAngle);
                        _currentSlideVelocity = new Vector2(
                            _player.Velocity.x * slopeFactor,
                            _player.Velocity.y * slopeFactor
                        );
                        _wasInAir = false;
                    }

                    float slopeFactorAcceleration = Mathf.Clamp01(angle / _maxAngle);
                    _currentSlideVelocity += slideDir * (_acceleration * slopeFactorAcceleration * Time.deltaTime);
                    return _currentSlideVelocity;
                }
                else
                {
                    _currentSlideVelocity *= Mathf.Exp(-_damping * Time.deltaTime);
                    if (_currentSlideVelocity.magnitude < 0.01f)
                        _currentSlideVelocity = Vector2.zero;
                    return _currentSlideVelocity;
                }
            }
            else
            {
                _wasInAir = true;
                _currentSlideVelocity *= Mathf.Exp(-_damping * Time.deltaTime);
                if (_currentSlideVelocity.magnitude < 0.01f)
                    _currentSlideVelocity = Vector2.zero;
                return _currentSlideVelocity;
            }
        }
    }
}

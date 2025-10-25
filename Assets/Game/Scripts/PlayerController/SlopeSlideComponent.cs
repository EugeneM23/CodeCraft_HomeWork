using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class SlopeSlideComponent
    {
        private readonly PlayerController _player;
        private readonly CollisionComponent _collision;
        private readonly float _maxAngle;
        private readonly float _slideForce;

        public SlopeSlideComponent(CollisionComponent collision, PlayerController player, float maxAngle = 45f,
            float slideForce = 50f)
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
            if (angle > _maxAngle || angle < 1f) return Vector2.zero;

            // Вычисляем направление ската (вниз по склону)
            Vector2 slideDir = Vector3.Cross(Vector3.forward, normal);
            slideDir = new Vector2(slideDir.x, slideDir.y).normalized;

            // Убеждаемся что направление идет вниз
            if (slideDir.y > 0) slideDir = -slideDir;

            Vector2 inputDirection = _player.FrameInput.Move;

            // Случай 1: Нет ввода - скатываемся
            if (Mathf.Abs(inputDirection.x) < 0.01f)
            {
                float multiplier = angle / _maxAngle;
                return slideDir * (_slideForce * multiplier);
            }

            // Случай 2: Определяем склон влево или вправо
            bool slopeGoesRight = slideDir.x > 0; // true = склон идет вправо-вниз
            bool playerGoesRight = inputDirection.x > 0; // true = игрок нажимает вправо

            // Случай 3: Игрок движется вниз по склону
            if (slopeGoesRight == playerGoesRight)
            {
                float multiplier = angle / _maxAngle;
                return slideDir * (_slideForce * multiplier);
            }

            // Случай 4: Игрок движется вверх по склону - не добавляем скат
            return Vector2.zero;
        }
    }
}
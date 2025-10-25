using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class MoveComponent : IMoveComponent
    {
        private readonly CollisionComponent _collision;
        private readonly ScriptableStats _stats;
        private readonly PlayerController _player;

        private float _currentHorizontalSpeed;

        public MoveComponent(ScriptableStats stats, CollisionComponent collision, PlayerController player)
        {
            _stats = stats;
            _collision = collision;
            _player = player;
        }

        public Vector2 Move(Vector2 direction)
        {
            Vector2 normal = _collision.SurfaceNormal;

            if (_collision.IsGrounded)
                return OnGround(direction, normal);

            return InAir(direction);
        }

        private Vector2 InAir(Vector2 direction)
        {
            // Берём текущую горизонтальную скорость из предыдущего кадра
            _currentHorizontalSpeed = _player.Velocity.x;
            float targetSpeed = direction.x * _stats.MaxSpeed;

            float accel = _stats.AirAcceleration;
            float decel = _stats.AirDeceleration;

            if (Mathf.Abs(direction.x) > 0.01f)
                _currentHorizontalSpeed =
                    Mathf.MoveTowards(_currentHorizontalSpeed, targetSpeed, accel * Time.fixedDeltaTime);
            else
                _currentHorizontalSpeed = Mathf.MoveTowards(_currentHorizontalSpeed, 0, decel * Time.fixedDeltaTime);

            // Возвращаем только горизонтальный вектор (вертикальное движение добавят другие компоненты)
            return new Vector2(_currentHorizontalSpeed, 0);
        }

        private Vector2 OnGround(Vector2 direction, Vector2 normal)
        {
            Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

            // Текущая скорость проецируем на касательную
            float currentSpeed = Vector2.Dot(_player.Velocity, tangent);

            // Задаём целевую скорость вдоль касательной
            float targetSpeed = direction.x * _stats.MaxSpeed;

            // Ускорение или замедление
            if (Mathf.Abs(direction.x) > 0.01f)
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, _stats.Acceleration * Time.fixedDeltaTime);
            else
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0, _stats.GroundDeceleration * Time.fixedDeltaTime);

            // Итоговое движение вдоль поверхности
            Vector2 movement = tangent * currentSpeed;

            return movement;
        }
    }
}
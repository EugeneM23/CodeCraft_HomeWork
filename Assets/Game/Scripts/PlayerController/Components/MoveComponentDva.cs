using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class MoveComponentDva : IMoveComponent
    {
        private readonly CollisionComponent _collision;
        private readonly ScriptableStats _stats;
        private readonly PlayerController _player;

        public MoveComponentDva(ScriptableStats stats, CollisionComponent collision, PlayerController player)
        {
            _stats = stats;
            _collision = collision;
            _player = player;
        }

        public Vector2 Move(Vector2 direction)
        {
            Vector2 normal = _collision.SurfaceNormal;

            // Если персонаж на земле и есть нормаль поверхности
            if (_collision.IsGrounded)
            {
                Debug.Log("move on ground");
                // Касательная (направление вдоль поверхности)
                Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

                // Текущая скорость проецируем на касательную
                float currentSpeed = Vector2.Dot(_player.Velocity, tangent);

                // Задаём целевую скорость вдоль касательной
                float targetSpeed = direction.x * _stats.MaxSpeed;

                // Выбираем ускорение
                float accel = _stats.Acceleration;
                float decel = _stats.GroundDeceleration;

                // Ускорение или замедление
                if (Mathf.Abs(direction.x) > 0.01f)
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
                else
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, decel * Time.fixedDeltaTime);

                // Итоговое движение вдоль поверхности
                Vector2 movement = tangent * currentSpeed;


                return movement;
            }
            else
            {
                // В воздухе используем стандартное горизонтальное движение
                float currentSpeed = _player.Velocity.x;
                float targetSpeed = direction.x * _stats.MaxSpeed;

                float accel = _stats.AirAcceleration;
                float decel = _stats.AirDeceleration;

                if (Mathf.Abs(direction.x) > 0.01f)
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
                else
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, decel * Time.fixedDeltaTime);

                // Сохраняем вертикальную скорость
                return new Vector2(currentSpeed, _player.Velocity.y);
            }
        }
    }
}
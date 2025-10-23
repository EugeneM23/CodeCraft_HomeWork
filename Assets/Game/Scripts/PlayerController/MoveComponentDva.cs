using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class MoveComponentDva : IMoveComponent
    {
        private SurfaceTangentDebugger _debugger;
        private PlayerController _character;
        private ScriptableStats _stats;

        public MoveComponentDva(ScriptableStats stats, PlayerController character, SurfaceTangentDebugger debugger)
        {
            _stats = stats;
            _character = character;
            _debugger = debugger;
        }

        public Vector2 Move(Vector2 direction)
        {
            Vector2 normal = _debugger.SurfaceNormal.normalized;
            
            // Если персонаж на земле и есть нормаль поверхности
            if (_character.IsGrounded && normal != Vector2.zero)
            {
                // Касательная (направление вдоль поверхности)
                Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

                // Текущая скорость проецируем на касательную
                float currentSpeed = Vector2.Dot(_character.Velocity, tangent);

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
                
                // Добавляем небольшую силу прижатия к поверхности
                movement += normal * _stats.GroundingForce * Time.fixedDeltaTime;

                return movement;
            }
            else
            {
                // В воздухе используем стандартное горизонтальное движение
                float currentSpeed = _character.Velocity.x;
                float targetSpeed = direction.x * _stats.MaxSpeed;
                
                float accel = _stats.AirAcceleration;
                float decel = _stats.AirDeceleration;
                
                if (Mathf.Abs(direction.x) > 0.01f)
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
                else
                    currentSpeed = Mathf.MoveTowards(currentSpeed, 0, decel * Time.fixedDeltaTime);
                
                // Сохраняем вертикальную скорость
                return new Vector2(currentSpeed, _character.Velocity.y);
            }
        }
    }
}
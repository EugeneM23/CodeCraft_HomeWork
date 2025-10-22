using UnityEngine;

public class SlopeSliding
{
    private readonly GravityComponent _gravityComponent;
    private readonly PlayerController _controller;

    private float slideMultiplier;
    private float slideSpeedAccum;

    public SlopeSliding(PlayerController controller, GravityComponent gravityComponent)
    {
        _controller = controller;
        _gravityComponent = gravityComponent;
    }

    public Vector3 Slide()
    {
        if (DetectObstacle(_controller.SurfaceNormal, out var stop))
            return stop;

        if (!_controller.IsGrounded)
        {
            slideSpeedAccum = 0f;
            return Vector3.zero;
        }

        float angle = Vector2.Angle(_controller.SurfaceNormal, Vector2.up);
        if (angle <= 4f)
        {
            slideSpeedAccum = 0f;
            return Vector3.zero;
        }

        // === Направление скольжения (касательное к поверхности) ===
        Vector2 tangent = new Vector2(_controller.SurfaceNormal.y, -_controller.SurfaceNormal.x);

        // Обеспечиваем направление вниз по склону
        if (Vector2.Dot(tangent, Vector2.down) < 0f)
            tangent = -tangent;

        // === Если есть Input — проверяем, помогает он или мешает ===
        if (_controller.InputDir != Vector2.zero)
        {
            float dot = Vector2.Dot(_controller.InputDir.normalized, tangent.normalized);

            if (dot < 0f)
            {
                // Игрок жмёт ПРОТИВ наклона — остановить скользение
                slideSpeedAccum = 0f;
                return Vector3.zero;
            }
            // Если dot >= 0 → игрок либо помогает скользить, либо жмёт перпендикулярно — позволяем скользить
        }

        // === Увеличиваем скорость скольжения ===
        slideSpeedAccum += _controller.SlideMultiplier * (angle / 90f) * Time.deltaTime;

        // === Добавляем ускорение в гравитацию ===
        _gravityComponent.AddSlopeGravity(tangent.normalized * slideSpeedAccum);

        // === Возвращаем итоговое движение по Time.deltaTime ===
        return tangent.normalized * slideSpeedAccum * Time.deltaTime;    }

    private bool DetectObstacle(Vector2 normal, out Vector3 result)
    {
        // Центр коллайдера
        Vector2 origin = _controller.Collider.bounds.center;

        // Касательная вдоль склона (направление скольжения)
        Vector2 tangent = new Vector2(normal.y, -normal.x);

        // Направление должно быть вниз по склону
        if (Vector2.Dot(tangent, Vector2.down) < 0f)
            tangent = -tangent;

        // Длина луча (можно регулировать)
        float rayDistance = 1.2f;

        // Посылаем луч вперед по направлению скольжения
        RaycastHit2D hit = Physics2D.Raycast(origin, tangent, rayDistance, _controller.GroundLayer);

        Debug.DrawRay(origin, tangent * rayDistance, Color.red);

        if (hit.collider != null)
        {
            // Препятствие найдено — останавливаем скольжение
            result = Vector3.zero;
            return true;
        }

        result = Vector3.zero;
        return false;
    }
}
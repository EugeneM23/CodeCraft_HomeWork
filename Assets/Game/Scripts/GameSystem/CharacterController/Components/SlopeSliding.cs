using UnityEngine;

public class SlopeSliding
{
    private readonly GravityComponent gravityComponent;
    private readonly Collider2D collider;
    private float slideMultiplier;
    private float slideSpeedAccum;
    private int groundLayer;

    public SlopeSliding(float slideMultiplier, GravityComponent gravityComponent, int groundLayer, Collider2D collider)
    {
        this.slideMultiplier = slideMultiplier;
        this.gravityComponent = gravityComponent;
        this.groundLayer = groundLayer;
        this.collider = collider;
    }

    public Vector3 Slide(bool grounded, Vector2 input, Vector2 normal)
    {
        if (DetectObstacle(normal, out var stop))
            return stop;


        if (!grounded || input != Vector2.zero)
        {
            slideSpeedAccum = 0f;
            return Vector3.zero;
        }

        float angle = Vector2.Angle(normal, Vector2.up);
        if (angle <= 4f)
        {
            slideSpeedAccum = 0f;
            return Vector3.zero;
        }

        // Увеличиваем скорость скольжения
        slideSpeedAccum += slideMultiplier * (angle / 90f) * Time.deltaTime;

        // Направление вдоль поверхности (касательная)
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        if (Vector2.Dot(tangent, Vector2.down) < 0f) tangent = -tangent;

        // Добавляем вектор скорости к gravityVector (скольжение = падение по наклонной)
        gravityComponent.AddSlopeGravity(tangent.normalized * slideSpeedAccum);

        // Возвращаем движение по склону
        return tangent.normalized * slideSpeedAccum * Time.deltaTime;
    }

    private bool DetectObstacle(Vector2 normal, out Vector3 result)
    {
        // Центр коллайдера
        Vector2 origin = collider.bounds.center;

        // Касательная вдоль склона (направление скольжения)
        Vector2 tangent = new Vector2(normal.y, -normal.x);

        // Направление должно быть вниз по склону
        if (Vector2.Dot(tangent, Vector2.down) < 0f)
            tangent = -tangent;

        // Длина луча (можно регулировать)
        float rayDistance = 1f;

        // Посылаем луч вперед по направлению скольжения
        RaycastHit2D hit = Physics2D.Raycast(origin, tangent, rayDistance, groundLayer);

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
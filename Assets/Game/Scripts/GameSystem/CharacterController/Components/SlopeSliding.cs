using UnityEngine;

public class SlopeSliding 
{
    private readonly GravityComponent gravityComponent;
    private float slideMultiplier;
    private float slideSpeedAccum;

    public SlopeSliding(float slideMultiplier, GravityComponent gravityComponent)
    {
        this.slideMultiplier = slideMultiplier;
        this.gravityComponent = gravityComponent;
    }

    public Vector3 Slide(bool grounded, Vector2 input, Vector2 normal)
    {
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
}
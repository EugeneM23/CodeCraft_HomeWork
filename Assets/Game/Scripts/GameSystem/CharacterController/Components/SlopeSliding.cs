using UnityEngine;

public class SlopeSliding 
{
    private float slideMultiplier;
    private float slideSpeedAccum;

    public SlopeSliding(float slideMultiplier)
    {
        this.slideMultiplier = slideMultiplier;
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

        slideSpeedAccum += slideMultiplier * (angle / 90f) * Time.deltaTime;

        Vector2 tangent = new Vector2(normal.y, -normal.x);
        if (Vector2.Dot(tangent, Vector2.down) < 0f) tangent = -tangent;

        return tangent.normalized * slideSpeedAccum * Time.deltaTime;
    }
}
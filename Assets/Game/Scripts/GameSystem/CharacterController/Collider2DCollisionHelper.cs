using System.Data;
using UnityEngine;

public class Collider2DCollisionHelper
{
    private Collider2D source;

    public Collider2DCollisionHelper(Collider2D sourceCollider)
    {
        source = sourceCollider;
    }

    public Vector3 GetPenetrationLayer(LayerMask layer)
    {
        Vector3 correction = Vector2.zero;

        if (source == null)
            throw new InvalidExpressionException();

        Collider2D[] overlapCache = new Collider2D[32];

        int count = Physics2D.OverlapBoxNonAlloc(
            source.bounds.center,
            source.bounds.size,
            source.transform.eulerAngles.z,
            overlapCache,
            layer
        );

        bool collided = false;

        Vector2 sumDir = Vector2.zero;

        float totalDist = 0f;

        for (int i = 0; i < count; i++)
        {
            Collider2D target = overlapCache[i];
            if (target == source) continue;

            if (ComputePenetration2D(source, target, out Vector2 dir, out float dist))
            {
                sumDir += dir * dist;
                totalDist += dist;
            }
        }

        correction = sumDir;
        var delta = Vector3.Lerp(Vector3.zero, correction, 0.1f);
        return delta;
    }

    private bool ComputePenetration2D(Collider2D source, Collider2D target, out Vector2 direction, out float distance)
    {
        direction = Vector2.zero;
        distance = 0f;

        if (source == null || target == null)
            return false;

        ColliderDistance2D info = source.Distance(target);
        if (info.isOverlapped)
        {
            direction = info.normal;
            distance = info.distance;
            return true;
        }

        return false;
    }
}
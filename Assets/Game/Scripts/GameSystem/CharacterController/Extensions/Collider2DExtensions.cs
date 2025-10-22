using UnityEngine;

public static class Collider2DExtensions
{
    public static bool GetPenetrationLayer(this Collider2D source, LayerMask layer, out Vector2 correction)
    {
        Collider2D[] overlapCache = new Collider2D[32];
        correction = Vector2.zero;
        if (source == null) return false;

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

            if (source.ComputePenetration2D(target, out Vector2 dir, out float dist))
            {
                collided = true;
                sumDir += dir * dist;
                totalDist += dist;
            }
        }

        correction = sumDir;

        return collided;
    }

    public static bool ComputePenetration2D(this Collider2D source, Collider2D target, out Vector2 direction,
        out float distance)
    {
        direction = Vector2.zero;
        distance = 0f;
        if (source == null || target == null) return false;
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
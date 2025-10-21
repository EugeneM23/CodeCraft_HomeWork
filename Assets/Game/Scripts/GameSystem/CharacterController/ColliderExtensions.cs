using UnityEngine;

public static class ColliderExtensions
{
    static readonly Collider[] overlapCache = new Collider[32];

    public static bool GetPenitrationLayer(this Collider source, LayerMask layer, out Vector3 correction)
    {
        correction = Vector3.zero;
        if (source == null) return false;

        int count = Physics.OverlapBoxNonAlloc(
            source.bounds.center,
            source.bounds.extents,
            overlapCache,
            source.transform.rotation,
            layer);

        bool colided = false;

        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapCache[i];
            if (collider == source) continue;

            if (source.ComputePenitration(collider, out Vector3 dir, out float distance))
            {
                colided = true;
                correction += dir * distance;
            }
        }

        return colided;
    }

    public static bool ComputePenitration(this Collider source, Collider target, out Vector3 direction, out float dist)
    {
        direction = Vector3.zero;
        dist = 0;

        if (source == null || target == null) return false;

        return Physics.ComputePenetration(
            source, source.transform.position, source.transform.rotation,
            target, target.transform.position, target.transform.rotation,
            out direction, out dist
        );
    }
}
using System.Data;
using UnityEngine;

public class Collider2DCollisionHelper
{
    private readonly PlayerController _controller;

    public Collider2DCollisionHelper(PlayerController controller)
    {
        _controller = controller;
    }

    public Vector3 GetPenetrationLayer()
    {
        Vector3 correction = Vector2.zero;

        if (_controller == null)
            throw new InvalidExpressionException();

        Collider2D[] overlapCache = new Collider2D[32];

        int count = Physics2D.OverlapBoxNonAlloc(
            _controller.Collider.bounds.center,
            _controller.Collider.bounds.size,
            _controller.transform.eulerAngles.z,
            overlapCache,
            _controller.GroundLayer
        );

        bool collided = false;

        Vector2 sumDir = Vector2.zero;

        float totalDist = 0f;

        for (int i = 0; i < count; i++)
        {
            Collider2D target = overlapCache[i];
            if (target == _controller) continue;

            if (ComputePenetration2D(_controller.Collider, target, out Vector2 dir, out float dist))
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
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// "Give me a place somewhere on this object": what the Patrol Area node asks. How an object turns into an area is
    /// not the node's business (invariant 7): by default it is the box around everything the object draws; a game with
    /// its own zones (a spline, a room graph) gives the agent its own sampler (AgentContext.Areas).
    /// </summary>
    public interface IAreaSampler
    {
        /// <summary>
        /// A random place on the area (random01 — the agent's own random, two numbers per call). Without an area — a
        /// place within the radius of the centre. The point may be off the walkable ground: the navigator snaps it.
        /// </summary>
        Vector3 RandomPoint(Transform area, Vector3 center, float radius, float randomX, float randomZ);

        /// <summary>The point stands on the area (seen from above), or no further than margin outside its edge.</summary>
        bool Contains(Transform area, Vector3 point, float margin);
    }

    /// <summary>
    /// The sampler every agent has by default: the area of an object is the box around its renderers (its own and its
    /// children's), seen from above; an object that draws nothing is a circle of the given radius around its position.
    /// </summary>
    public sealed class BoundsAreaSampler : IAreaSampler
    {
        public static readonly BoundsAreaSampler Shared = new();

        public Vector3 RandomPoint(Transform area, Vector3 center, float radius, float randomX, float randomZ)
        {
            if (area != null && TryBounds(area, out Bounds bounds))
            {
                return new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, randomX), bounds.center.y,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, randomZ));
            }

            if (area != null)
                center = area.position;

            // A disc: the root keeps the points even over its area instead of crowding the middle
            float angle = randomX * Mathf.PI * 2f;
            float distance = Mathf.Sqrt(randomZ) * Mathf.Max(0f, radius);
            return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        }

        public bool Contains(Transform area, Vector3 point, float margin)
        {
            if (area == null || !TryBounds(area, out Bounds bounds))
                return false;                           // an object that draws nothing has no area to stand on

            return point.x >= bounds.min.x - margin && point.x <= bounds.max.x + margin &&
                   point.z >= bounds.min.z - margin && point.z <= bounds.max.z + margin;
        }

        // The renderers of the object asked about (filled anew each ask: no array a pick)
        private static readonly System.Collections.Generic.List<Renderer> Renderers = new();

        /// <summary>The box around everything the object draws; false — it draws nothing.</summary>
        public static bool TryBounds(Transform area, out Bounds bounds)
        {
            area.GetComponentsInChildren(Renderers);
            bounds = default;
            bool any = false;
            for (int i = 0; i < Renderers.Count; i++)
            {
                if (!any)
                {
                    bounds = Renderers[i].bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(Renderers[i].bounds);
                }
            }

            Renderers.Clear();
            return any;
        }
    }
}

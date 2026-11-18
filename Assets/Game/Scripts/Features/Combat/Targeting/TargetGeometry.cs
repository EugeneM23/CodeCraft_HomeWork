using UnityEngine;

namespace Game
{
    /// <summary>Small geometry the target sources and the auto guns share. Pure, no allocations.</summary>
    public static class TargetGeometry
    {
        /// <summary>
        /// Where a ball moving from <paramref name="from"/> to <paramref name="to"/> first meets a sphere: the share
        /// <paramref name="t"/> (0..1) of the segment. <paramref name="radius"/> is the sum of both radii. A start inside
        /// the sphere is a hit at 0.
        /// </summary>
        public static bool SegmentSphere(Vector3 from, Vector3 to, Vector3 center, float radius, out float t)
        {
            t = 0f;
            Vector3 d = to - from;
            Vector3 m = from - center;
            float c = Vector3.Dot(m, m) - radius * radius;
            if (c <= 0f)
                return true;

            float a = Vector3.Dot(d, d);
            if (a < 1e-10f)
                return false;

            float b = Vector3.Dot(m, d);
            if (b > 0f)
                return false; // starts outside and moves away

            float discriminant = b * b - a * c;
            if (discriminant < 0f)
                return false;

            t = (-b - Mathf.Sqrt(discriminant)) / a;
            return t <= 1f;
        }

        /// <summary>
        /// A rotation looking from <paramref name="from"/> at <paramref name="to"/>: yaw free, pitch clamped to
        /// ±<paramref name="maxPitch"/> degrees. False when the points are on top of each other.
        /// </summary>
        public static bool TryAim(Vector3 from, Vector3 to, float maxPitch, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            Vector3 direction = to - from;
            float flat = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
            if (flat < 0.001f)
                return false;

            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, flat) * Mathf.Rad2Deg; // Unity: +x pitch looks down
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            rotation = Quaternion.Euler(pitch, yaw, 0f);
            return true;
        }

        /// <summary>Linear falloff of area damage: 1 at the centre, <paramref name="minFactor"/> at the edge.</summary>
        public static float Falloff(float distance, float radius, float minFactor) =>
            radius > 0f ? Mathf.Lerp(1f, minFactor, Mathf.Clamp01(distance / radius)) : 1f;
    }
}

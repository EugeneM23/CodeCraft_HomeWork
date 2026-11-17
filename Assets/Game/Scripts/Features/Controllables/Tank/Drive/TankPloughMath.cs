using UnityEngine;

namespace Game
{
    /// <summary>
    /// The numbers of a tank's nose ploughing into the ground: how hard a hit is, whether it is the
    /// nose that hit, and what the hull and the earth do after it. See TankPloughBehaviour.
    /// </summary>
    public static class TankPloughMath
    {
        /// <summary>The slope faces the nose when its normal · the hull's forward is below this.</summary>
        public const float NOSE_FACING = -0.2f;

        /// <summary>0 below <paramref name="minClosing"/> m/s into the ground, 1 from <paramref name="fullClosing"/>.</summary>
        public static float Strength(float closing, float minClosing, float fullClosing)
        {
            if (closing < minClosing)
                return 0f;
            if (fullClosing <= minClosing)
                return 1f;
            return Mathf.Clamp01((closing - minClosing) / (fullClosing - minClosing));
        }

        /// <summary>
        /// The front half of the hull, below its middle, met ground that faces the nose, coming into it
        /// at least <paramref name="minClosing"/> m/s. A flat landing on the belly is the springs' business.
        /// </summary>
        /// <param name="localFromHullCentre">The hit point in the hull's space, from the hull box's centre.</param>
        public static bool IsNoseHit(Vector3 localFromHullCentre, Vector3 normal, Vector3 forward, float closing,
            float minClosing) =>
            localFromHullCentre.z > 0f && localFromHullCentre.y < 0f &&
            Vector3.Dot(normal, forward) < NOSE_FACING && closing >= minClosing;

        /// <summary>Speed after a hit: even a light one costs half the loss, a full one all of it.</summary>
        public static float SpeedAfter(float speed, float strength, float speedLoss) =>
            speed * (1f - speedLoss * Mathf.Max(strength, 0.5f));

        /// <summary>
        /// Velocity after the hit: along the slope, turned up off it by <paramref name="deflect"/> × strength,
        /// at <see cref="SpeedAfter"/>, plus <paramref name="hop"/> × strength m/s straight up.
        /// </summary>
        public static Vector3 Deflect(Vector3 velocity, Vector3 normal, float strength, float speedLoss, float deflect,
            float hop)
        {
            Vector3 along = Vector3.ProjectOnPlane(velocity, normal);
            Vector3 heading = along.sqrMagnitude > 1e-6f ? along.normalized : normal;
            heading = (heading + normal * (deflect * strength)).normalized;
            return heading * SpeedAfter(velocity.magnitude, strength, speedLoss) + Vector3.up * (hop * strength);
        }

        /// <summary>
        /// The slope the hull leaves a hit along: <paramref name="normal"/>, but tipped back toward up so the
        /// ground it describes is no steeper than <paramref name="maxDegrees"/>. A voxel step's vertical face is
        /// no wall to the nose that digs it out: the hull rides up the ramp the bite cuts, not up the face.
        /// </summary>
        public static Vector3 Ramp(Vector3 normal, float maxDegrees)
        {
            Vector3 flat = new Vector3(normal.x, 0f, normal.z);
            if (flat.sqrMagnitude < 1e-8f || Vector3.Angle(normal, Vector3.up) <= maxDegrees)
                return normal;
            float rad = maxDegrees * Mathf.Deg2Rad;
            return flat.normalized * Mathf.Sin(rad) + Vector3.up * Mathf.Cos(rad);
        }

        /// <summary>Mean velocity of the earth the nose throws: forward along the slope and up off it.</summary>
        public static Vector3 Throw(Vector3 velocity, Vector3 normal, float lift, float scale)
        {
            Vector3 along = Vector3.ProjectOnPlane(velocity, normal);
            Vector3 heading = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.zero;
            return (heading + normal * lift) * (velocity.magnitude * scale);
        }
    }
}

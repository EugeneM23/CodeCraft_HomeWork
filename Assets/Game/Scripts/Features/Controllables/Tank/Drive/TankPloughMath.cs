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

        /// <summary>
        /// Speed (m/s) into the ground, against its real face, at or below which a hit makes no impact (sound, shake).
        /// Kept equal to the presets' MinClosing (2): a nose hit needs at least MinClosing into the real face, so with
        /// a preset MinClosing above this every hit has an impact; below it, the slowest hits would turn the hull
        /// silently (TankPloughBehaviour raises nothing for impact 0).
        /// </summary>
        public const float IMPACT_MIN_CLOSING = 2f;

        /// <summary>
        /// …and at which the impact is as hard as it gets, for every preset alike. Above a flat-out tank's
        /// speed (~13 m/s): straight into a crater's step it is still not the hardest hit there is.
        /// </summary>
        public const float IMPACT_FULL_CLOSING = 16f;

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
        /// How hard a hit felt, 0..1, for the sound and the camera: <paramref name="closing"/> m/s into the
        /// ground's real face on the scale every preset shares (IMPACT_MIN_CLOSING..IMPACT_FULL_CLOSING),
        /// times the preset's <paramref name="scale"/>. Not the response strength (Strength): that one is
        /// against the ramp the preset cuts and drives the bite and the hull.
        /// </summary>
        public static float ImpactStrength(float closing, float scale) =>
            Mathf.Clamp01(Strength(closing, IMPACT_MIN_CLOSING, IMPACT_FULL_CLOSING) * scale);

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
        /// at <see cref="SpeedAfter"/>. The hop is not here: it is an impulse at the bow (TankPloughBehaviour).
        /// </summary>
        public static Vector3 Deflect(Vector3 velocity, Vector3 normal, float strength, float speedLoss, float deflect)
        {
            Vector3 along = Vector3.ProjectOnPlane(velocity, normal);
            Vector3 heading = along.sqrMagnitude > 1e-6f ? along.normalized : normal;
            heading = (heading + normal * (deflect * strength)).normalized;
            return heading * SpeedAfter(velocity.magnitude, strength, speedLoss);
        }

        /// <summary>
        /// The slope the hull leaves a hit along: <paramref name="normal"/>, but tipped back toward up so the
        /// ground it describes is no steeper than <paramref name="maxDegrees"/>. A voxel step's vertical face is
        /// no wall to the nose that digs it out: the hull rides up the ramp the bite cuts, not up the face.
        /// </summary>
        /// <remarks>An overhang's underside (normal pointing down) is left as it is: there is no ramp to cut up it.</remarks>
        public static Vector3 Ramp(Vector3 normal, float maxDegrees)
        {
            Vector3 flat = new Vector3(normal.x, 0f, normal.z);
            if (normal.y < 0f || flat.sqrMagnitude < 1e-8f || Vector3.Angle(normal, Vector3.up) <= maxDegrees)
                return normal;
            float rad = maxDegrees * Mathf.Deg2Rad;
            return flat.normalized * Mathf.Sin(rad) + Vector3.up * Mathf.Cos(rad);
        }

        /// <summary>
        /// <paramref name="angular"/> with its nose-up spin (about -<paramref name="right"/>) at least
        /// <paramref name="minRate"/> rad/s: a nose-down spin is overridden, a faster nose-up one kept.
        /// The other axes are left as they are.
        /// </summary>
        public static Vector3 NoseUpSpin(Vector3 angular, Vector3 right, float minRate)
        {
            float noseUp = -Vector3.Dot(angular, right);
            return noseUp >= minRate ? angular : angular - right * (minRate - noseUp);
        }

        /// <summary><paramref name="angular"/> without any nose-up spin (about -<paramref name="right"/>); a nose-down one is kept.</summary>
        public static Vector3 NoNoseUpSpin(Vector3 angular, Vector3 right)
        {
            float noseUp = -Vector3.Dot(angular, right);
            return noseUp > 0f ? angular + right * noseUp : angular;
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

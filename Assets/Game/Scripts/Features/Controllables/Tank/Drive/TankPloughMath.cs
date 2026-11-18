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
        /// Kept equal to the presets' MinClosing (2). The impact goes by ImpactClosing (against a slope of
        /// IMPACT_RAMP), which can be less than the closing into the real face: the slowest such hits bounce
        /// the hull silently (TankPloughBehaviour raises nothing for impact 0).
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
        /// The slope (°) a hit's impact is measured against, the same for every preset: a voxel step's riser
        /// and tread are the one slope they are steps of, so the same entry feels the same whichever facet the
        /// nose meets first.
        /// </summary>
        public const float IMPACT_RAMP = 30f;

        /// <summary>
        /// Speed (m/s) into the ground a hit's impact goes by: against a slope of IMPACT_RAMP rising straight
        /// ahead (RampAhead), the same for every preset and whatever facet was met.
        /// </summary>
        public static float ImpactClosing(Vector3 velocity) => -Vector3.Dot(velocity, RampAhead(velocity, IMPACT_RAMP));

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
        /// The slope the hull leaves a hit along: the normal of a ramp of exactly <paramref name="degrees"/>
        /// rising straight ahead the way the hull goes (<paramref name="velocity"/>, level). The nose rips that
        /// ramp out of whatever it hit: a voxel step's vertical face is no wall to it, and which facet of a
        /// voxel staircase the hull's box met first (a riser, a tread, one turned aside) does not change the
        /// bounce. Straight up for a hull going straight up or down.
        /// </summary>
        public static Vector3 RampAhead(Vector3 velocity, float degrees)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude < 1e-6f)
                return Vector3.up;
            float rad = degrees * Mathf.Deg2Rad;
            return -flat.normalized * Mathf.Sin(rad) + Vector3.up * Mathf.Cos(rad);
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

        /// <summary>
        /// Mean velocity of the chunk the nose rips out: ahead the way the hull goes (level, whatever its
        /// pitch) and <paramref name="lift"/> of that up (1 is 45°), at the hull's speed × <paramref name="scale"/>.
        /// Straight up when the hull goes straight up or down.
        /// </summary>
        public static Vector3 ChunkThrow(Vector3 velocity, float lift, float scale)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 ahead = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.zero;
            return (ahead + Vector3.up * lift) * (velocity.magnitude * scale);
        }

        /// <summary>
        /// m³ of earth the chunk throws: <paramref name="voxels"/> dug out of ground whose voxels are
        /// <paramref name="voxelSize"/> m across in the world, times <paramref name="share"/>. 0 when that is not
        /// a finite number above 0.
        /// </summary>
        public static float ChunkVolume(int voxels, float voxelSize, float share)
        {
            float volume = voxels * voxelSize * voxelSize * voxelSize * share;
            return float.IsFinite(volume) && volume > 0f ? volume : 0f;
        }
    }
}

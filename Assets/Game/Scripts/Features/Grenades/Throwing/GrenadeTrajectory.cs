using UnityEngine;

namespace Game
{
    /// <summary>
    /// Ballistics of a thrown grenade: the launch velocity that lands it exactly on a point.
    ///
    /// The flight time grows with the distance (a constant horizontal speed), so a long throw is a
    /// high lob and a short one a low toss. A minimum flight time keeps a throw at one's own feet an
    /// arc rather than a straight line into the floor. Unlike a fixed launch angle this always has a
    /// solution, whatever the height of the target.
    /// </summary>
    public static class GrenadeTrajectory
    {
        private const float MIN_POSITIVE = 0.01f;

        /// <summary>
        /// Pulls the target in to <paramref name="maxRange"/> of <paramref name="from"/>, measured on
        /// the ground plane. The target keeps its height.
        /// </summary>
        public static Vector3 ClampToRange(Vector3 from, Vector3 to, float maxRange)
        {
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);

            if (flat.sqrMagnitude <= maxRange * maxRange)
                return to;

            flat = flat.normalized * maxRange;
            return new Vector3(from.x + flat.x, to.y, from.z + flat.z);
        }

        /// <summary>
        /// Velocity that carries a body without drag from <paramref name="from"/> to
        /// <paramref name="to"/> under <paramref name="gravity"/>, flying along the ground at
        /// <paramref name="throwSpeed"/> but for no less than <paramref name="minFlightTime"/>.
        /// </summary>
        public static Vector3 GetLaunchVelocity(Vector3 from, Vector3 to, float throwSpeed, float minFlightTime,
            Vector3 gravity)
        {
            Vector3 delta = to - from;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);

            float time = Mathf.Max(
                flat.magnitude / Mathf.Max(throwSpeed, MIN_POSITIVE),
                Mathf.Max(minFlightTime, MIN_POSITIVE));

            // y(t) = vy·t + ½·g·t² must equal delta.y at the end of the flight.
            Vector3 velocity = flat / time;
            velocity.y = delta.y / time - 0.5f * gravity.y * time;
            return velocity;
        }
    }
}

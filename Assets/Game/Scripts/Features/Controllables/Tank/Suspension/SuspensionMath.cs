using UnityEngine;

namespace Game
{
    /// <summary>
    /// Spring arithmetic of a raycast suspension. Lengths run from the wheel's mount on the hull to
    /// the wheel's centre: the longest is the spring hanging free, the shortest is the bump stop.
    /// </summary>
    public static class SuspensionMath
    {
        /// <summary>How much stiffer than the spring the bump stop is.</summary>
        private const float BUMP_STOP_FACTOR = 10f;

        /// <summary>The spring rate that leaves the body resting <paramref name="sag"/> below free length.</summary>
        public static float Stiffness(float mass, float gravity, int wheels, float sag) =>
            mass * gravity / Mathf.Max(1, wheels) / Mathf.Max(sag, 1e-4f);

        /// <summary>
        /// Damping coefficient for a damping ratio, taking each wheel to carry an equal share of the
        /// mass. 1 settles without overshoot; below that the hull rocks a little before it settles.
        /// </summary>
        public static float Damping(float stiffness, float mass, int wheels, float ratio) =>
            2f * ratio * Mathf.Sqrt(stiffness * mass / Mathf.Max(1, wheels));

        /// <summary>
        /// Push of one spring along the suspension axis. Zero with the wheel off the ground (at or
        /// past free length). Never negative: a spring holds the hull up, it cannot pull it down
        /// onto the ground, even while extending fast.
        /// </summary>
        public static float Force(float length, float previousLength, float deltaTime, float maxLength,
            float minLength, float stiffness, float damping)
        {
            if (length >= maxLength)
                return 0f;

            float compression = maxLength - length;
            float compressionSpeed = deltaTime > 0f ? (previousLength - length) / deltaTime : 0f;

            float force = stiffness * compression + damping * compressionSpeed;

            if (length < minLength)
                force += stiffness * BUMP_STOP_FACTOR * (minLength - length);

            return Mathf.Max(0f, force);
        }
    }
}

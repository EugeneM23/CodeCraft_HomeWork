using UnityEngine;

namespace Game
{
    /// <summary>
    /// The numbers behind the helicopter's flight, apart from the body so they can be tested.
    /// </summary>
    public static class HelicopterFlightMath
    {
        /// <summary>
        /// Moves the horizontal velocity towards the wanted one: at <paramref name="acceleration"/>
        /// while it gets faster, at <paramref name="braking"/> while it gets slower. Braking is the
        /// slower of the two, so the helicopter drifts on for a while after the stick is let go.
        /// </summary>
        public static Vector3 ApproachPlanar(Vector3 current, Vector3 target, float acceleration, float braking,
            float deltaTime)
        {
            current.y = 0f;
            target.y = 0f;

            float rate = target.sqrMagnitude >= current.sqrMagnitude ? acceleration : braking;
            return Vector3.MoveTowards(current, target, rate * deltaTime);
        }

        /// <summary>Vertical speed that closes the gap to <paramref name="targetY"/>, slowing near it.</summary>
        public static float ClimbSpeed(float y, float targetY, float gain, float maxSpeed) =>
            Mathf.Clamp((targetY - y) * gain, -maxSpeed, maxSpeed);
    }
}

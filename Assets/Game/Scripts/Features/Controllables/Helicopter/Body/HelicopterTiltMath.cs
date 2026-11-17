using UnityEngine;

namespace Game
{
    /// <summary>
    /// How far the visible body leans. Angles are Unity Euler angles on the body: positive pitch
    /// (about X) puts the nose down, positive roll (about Z) lifts the right side.
    /// </summary>
    public static class HelicopterTiltMath
    {
        /// <summary>
        /// Target lean, x pitch and y roll, in degrees.
        /// Speeding up forwards puts the nose down, slowing down lifts it; flying along keeps a
        /// little of the nose down, as a real one leans into the air it pushes through. Speeding up
        /// sideways banks towards the side it goes, and a turn banks into the turn.
        /// </summary>
        /// <param name="localAcceleration">Horizontal acceleration in the body's axes, m/s².</param>
        /// <param name="localVelocity">Horizontal velocity in the body's axes, m/s.</param>
        /// <param name="yawRate">Degrees per second, positive turning right.</param>
        public static Vector2 TargetAngles(Vector3 localAcceleration, Vector3 localVelocity, float yawRate,
            float degreesPerAcceleration, float degreesPerSpeed, float degreesPerYawRate, float maxAngle)
        {
            float pitch = localAcceleration.z * degreesPerAcceleration + localVelocity.z * degreesPerSpeed;
            float roll = -(localAcceleration.x * degreesPerAcceleration + localVelocity.x * degreesPerSpeed)
                         - yawRate * degreesPerYawRate;

            return new Vector2(Mathf.Clamp(pitch, -maxAngle, maxAngle), Mathf.Clamp(roll, -maxAngle, maxAngle));
        }

        /// <summary>One step of a damped spring pulling <paramref name="angle"/> to <paramref name="target"/>.</summary>
        public static void Spring(ref float angle, ref float velocity, float target, float frequency,
            float dampingRatio, float deltaTime)
        {
            float omega = 2f * Mathf.PI * frequency;
            float acceleration = omega * omega * (target - angle) - 2f * dampingRatio * omega * velocity;

            velocity += acceleration * deltaTime;
            angle += velocity * deltaTime;
        }
    }
}

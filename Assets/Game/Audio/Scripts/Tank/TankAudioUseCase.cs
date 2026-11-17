using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The tank's sound parameters worked out from what the drive knows: it has no gearbox and no revs, so the
    /// engine speed is made up from the hull's speed, the throttle and the boost.
    /// </summary>
    public static class TankAudioUseCase
    {
        /// <summary>Revs at idle, running with the stick at rest.</summary>
        public const float IDLE_RPM = 0.2f;

        /// <summary>Highest revs without boost: the top end belongs to Shift.</summary>
        public const float NORMAL_MAX_RPM = 0.8f;

        /// <summary>Share of the load the throttle alone gives, before the hull has picked up speed.</summary>
        public const float THROTTLE_LOAD = 0.4f;

        public const float RPM_RISE = 4f;
        public const float RPM_FALL = 2.5f;

        /// <summary>Hull speed as a share of its normal top speed, 0..1.</summary>
        public static float SpeedShare(float speed, float maxSpeed) =>
            Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, maxSpeed));

        /// <summary>
        /// Revs the engine is heading for, 0..1: idle at rest, rising with speed and throttle, to
        /// <see cref="NORMAL_MAX_RPM"/> at top speed and to 1 with the boost.
        /// </summary>
        /// <param name="forwardSpeed">m/s along the hull, either way.</param>
        /// <param name="maxSpeed">Normal top speed (MoveSpeed), m/s.</param>
        /// <param name="throttle">Stick, -1..1, either way counts.</param>
        public static float TargetRpm(float forwardSpeed, float maxSpeed, float throttle, bool boost)
        {
            float share = SpeedShare(forwardSpeed, maxSpeed);
            float gas = Mathf.Clamp01(Mathf.Abs(throttle));
            float load = Mathf.Max(share, gas * THROTTLE_LOAD);
            float ceiling = boost && gas > 0.1f ? 1f : NORMAL_MAX_RPM;
            return Mathf.Clamp01(IDLE_RPM + (ceiling - IDLE_RPM) * load);
        }

        /// <summary>Moves the revs towards the target: quicker up than down, frame-rate independent.</summary>
        public static float SmoothRpm(float current, float target, float deltaTime)
        {
            float rate = target > current ? RPM_RISE : RPM_FALL;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * deltaTime));
        }
    }
}

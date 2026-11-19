using System;
using Unity.Mathematics;

namespace Game.Horde
{
    /// <summary>
    /// What a monster on the tank is doing (a slot in <see cref="HordeState.Riding"/>): the states the rider animations
    /// hang on. <see cref="None"/>: not a rider (on the ground with the crowd, or dead).
    /// </summary>
    public enum HordeRiderState : byte
    {
        None = 0,
        /// <summary>In the air from the ground to its attach point.</summary>
        Leaping = 1,
        /// <summary>Landed on the hull, getting a grip (a short settle before it tears).</summary>
        Clinging = 2,
        /// <summary>Holding on and tearing at the armour: a strike every interval.</summary>
        Tearing = 3,
        /// <summary>Lost its grip (a sharp turn, speed, a jolt, a hit): falling to the ground, then it walks again.</summary>
        ShakenOff = 4
    }

    /// <summary>Why a rider lost its grip.</summary>
    public enum HordeShakeCause : byte
    {
        None = 0,
        /// <summary>Sharp turns, high speed or a jolt (a ram, a hard stop) wore its grip down.</summary>
        Motion = 1,
        /// <summary>A hit (the tank's own weapons, a blast) knocked it loose.</summary>
        Hit = 2,
        /// <summary>The tank is gone (or changed, or the arena was reset).</summary>
        TankLost = 3
    }

    /// <summary>Tuning of the riders: when they leap, how they hold on and what they do to the tank. Distances in m, times in s.</summary>
    [Serializable]
    public struct HordeRidingSettings
    {
        /// <summary>Riders on one tank at most (one per attach point; never more than the points).</summary>
        public int MaxRiders;
        /// <summary>A monster this close to the hull (gap, m) may leap.</summary>
        public float LeapRange;
        /// <summary>Chance per second that a monster in range leaps while the tank is slow.</summary>
        public float LeapChance;
        /// <summary>The tank is slow below this speed: full chance.</summary>
        public float SlowSpeed;
        /// <summary>Above <see cref="SlowSpeed"/> the chance is scaled by this (0: they leap only onto a slow tank).</summary>
        public float FastChanceScale;
        /// <summary>No leaps at all above this tank speed.</summary>
        public float MaxLeapTankSpeed;
        /// <summary>Seconds between two leaps on the same tank (they come one by one).</summary>
        public float LeapGap;
        /// <summary>Horizontal leap speed: the flight lasts distance / this, clamped to [<see cref="LeapMinTime"/>, <see cref="LeapMaxTime"/>].</summary>
        public float LeapSpeed;
        public float LeapMinTime;
        public float LeapMaxTime;
        /// <summary>Top of the arc over the straight line, m.</summary>
        public float LeapArc;
        /// <summary>Seconds of settling after landing before it tears.</summary>
        public float ClingTime;
        /// <summary>Damage of one strike while tearing (before the monster's damage scale).</summary>
        public float StrikeDamage;
        /// <summary>Seconds between strikes (each rider gets ±25 % of its own).</summary>
        public float StrikeInterval;
        /// <summary>Grip lost per second per rad/s of the tank's (or, on the turret, the turret's) yaw rate above <see cref="TurnThreshold"/>.</summary>
        public float TurnThreshold;
        public float TurnShake;
        /// <summary>Grip lost per second per m/s above <see cref="SpeedThreshold"/>.</summary>
        public float SpeedThreshold;
        public float SpeedShake;
        /// <summary>Grip lost per second per m/s² of the hull's acceleration above <see cref="JoltThreshold"/> (a ram, a hard stop).</summary>
        public float JoltThreshold;
        public float JoltShake;
        /// <summary>Grip lost to a hit that does not kill it.</summary>
        public float HitShake;
        /// <summary>Grip won back per second.</summary>
        public float GripRegain;
        /// <summary>Thrown off at this speed away from the hull and this speed up, m/s, on top of the tank's own.</summary>
        public float ThrowOut;
        public float ThrowUp;
        /// <summary>A rider that fell does not leap again for this long.</summary>
        public float RejumpDelay;

        public static HordeRidingSettings Default => new()
        {
            MaxRiders = 6,
            LeapRange = 2.2f,
            LeapChance = 0.9f,
            SlowSpeed = 3f,
            FastChanceScale = 0.15f,
            MaxLeapTankSpeed = 9f,
            LeapGap = 0.35f,
            LeapSpeed = 6f,
            LeapMinTime = 0.35f,
            LeapMaxTime = 0.7f,
            LeapArc = 1.2f,
            ClingTime = 0.35f,
            StrikeDamage = 5f,
            StrikeInterval = 0.8f,
            TurnThreshold = 0.8f,
            TurnShake = 2f,
            SpeedThreshold = 8f,
            SpeedShake = 0.35f,
            JoltThreshold = 14f,
            JoltShake = 0.25f,
            HitShake = 0.45f,
            GripRegain = 0.25f,
            ThrowOut = 3.5f,
            ThrowUp = 3f,
            RejumpDelay = 3f
        };
    }

    /// <summary>The arithmetic of riding: leap chance, the flight, the grip. Pure functions (tested on their own).</summary>
    public static class HordeRiding
    {
        public const float GRAVITY = 9.81f;

        /// <summary>
        /// Chance per second that one monster in range leaps onto a tank going <paramref name="tankSpeed"/> and turning at
        /// <paramref name="yawRate"/> rad/s: none onto a tank that turns hard enough to throw it off again.
        /// </summary>
        public static float LeapChance(in HordeRidingSettings s, float tankSpeed, float yawRate = 0f)
        {
            if (tankSpeed >= s.MaxLeapTankSpeed || math.abs(yawRate) > s.TurnThreshold)
                return 0f;
            return tankSpeed <= s.SlowSpeed ? s.LeapChance : s.LeapChance * s.FastChanceScale;
        }

        /// <summary>How long a leap over <paramref name="distance"/> m lasts.</summary>
        public static float LeapTime(in HordeRidingSettings s, float distance) =>
            math.clamp(distance / math.max(s.LeapSpeed, 0.1f), s.LeapMinTime, math.max(s.LeapMinTime, s.LeapMaxTime));

        /// <summary>
        /// Feet in flight at <paramref name="t"/> (0..1) from <paramref name="from"/> to <paramref name="to"/> (where the
        /// attach point is now: the arc follows a moving tank), <paramref name="arc"/> m over the straight line at the middle.
        /// </summary>
        public static float3 LeapPoint(float3 from, float3 to, float arc, float t)
        {
            t = math.saturate(t);
            float3 p = math.lerp(from, to, t);
            p.y += arc * 4f * t * (1f - t);
            return p;
        }

        /// <summary>
        /// Grip lost per second to the tank's motion: yaw rate (rad/s; on the turret, the larger of the hull's and the
        /// turret's), speed (m/s) and acceleration (m/s²) above their thresholds.
        /// </summary>
        public static float Shake(in HordeRidingSettings s, float yawRate, float speed, float acceleration)
        {
            float turn = math.max(math.abs(yawRate) - s.TurnThreshold, 0f) * s.TurnShake;
            float fast = math.max(speed - s.SpeedThreshold, 0f) * s.SpeedShake;
            float jolt = math.max(acceleration - s.JoltThreshold, 0f) * s.JoltShake;
            return turn + fast + jolt;
        }

        /// <summary>The grip after <paramref name="deltaTime"/> s of <paramref name="shake"/> (per second) and <paramref name="hits"/> hits; it never exceeds 1.</summary>
        public static float Grip(in HordeRidingSettings s, float grip, float shake, int hits, float deltaTime)
        {
            float regain = shake > 0f ? 0f : s.GripRegain;
            grip += (regain - shake) * deltaTime - hits * s.HitShake;
            return math.min(grip, 1f);
        }

        /// <summary>Seconds until the next strike of the rider in slot <paramref name="slot"/>: the interval ±25 %, its own.</summary>
        public static float StrikeInterval(in HordeRidingSettings s, int slot, int strike)
        {
            uint h = math.hash(new int2(slot, strike));
            float jitter = (h & 0xFFFF) / 65535f * 0.5f - 0.25f;
            return s.StrikeInterval * (1f + jitter);
        }
    }
}

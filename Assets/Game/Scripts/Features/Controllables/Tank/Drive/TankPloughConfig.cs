using UnityEngine;

namespace Game
{
    /// <summary>How hard a tank's nose ploughs into the ground it hits (TankPloughBehaviour). One asset per preset.</summary>
    [CreateAssetMenu(menuName = "Game/Tank/Plough Config", fileName = "TankPloughConfig")]
    public sealed class TankPloughConfig : ScriptableObject
    {
        [Tooltip("Shown in the sandbox HUD.")]
        public string DisplayName = "Punchy";

        [Header("Hit")]
        [Tooltip("Slower than this (m/s) the hull does not look ahead at all.")]
        public float MinSpeed = 3f;
        [Tooltip("Speed (m/s) into the ground below which a hit is no hit.")]
        public float MinClosing = 2f;
        [Tooltip("Speed (m/s) into the ground at which a hit is as hard as it gets.")]
        public float FullClosing = 8f;
        [Tooltip("Seconds of travel the hull looks ahead: the ground's collider is rebuilt by the time it arrives.")]
        public float LookAhead = 0.1f;

        [Header("Bite")]
        [Tooltip("Radius (m) of one of the three bites across the nose, at the lightest hit.")]
        public float BiteRadiusMin = 0.6f;
        [Tooltip("…and at the hardest.")]
        public float BiteRadiusMax = 1.1f;
        [Tooltip("Seconds between bites while the nose keeps ploughing.")]
        public float BiteInterval = 0.05f;
        [Tooltip("Earth thrown off the slope, along it, per m/s of the hull: 1 throws it as fast as the hull goes.")]
        public float ThrowScale = 0.9f;
        [Tooltip("How much up off the slope the earth goes: 0 skims along it, 1 at 45°.")]
        public float ThrowLift = 0.8f;
        [Tooltip("Share of the earth dug out that flies; the rest is pressed down.")]
        public float ThrowVolume = 0.8f;
        [Tooltip("Spread of the throw at the lightest hit: 0 one tight clump, 1 a wide fan.")]
        public float SpreadMin = 0.2f;
        [Tooltip("…and at the hardest.")]
        public float SpreadMax = 0.7f;
        [Tooltip("Most debris cubes one bite may throw while the earth goes out through Dig.")]
        public int BiteDebris = 160;

        [Header("Response")]
        [Tooltip("Share of the speed the hardest hit costs; a light one costs half of it.")]
        public float SpeedLoss = 0.33f;
        [Tooltip("How far the velocity is turned up off the slope at the hardest hit.")]
        public float Deflect = 0.3f;
        [Tooltip("m/s straight up the hardest hit adds: the jump out of the crater.")]
        public float Hop = 1.2f;
        [Tooltip("rad/s nose-up the hardest hit adds.")]
        public float PitchKick = 0.8f;
        [Tooltip("Seconds after a hit before the next can turn the hull again.")]
        public float ImpactCooldown = 0.3f;

        [Header("Feel")]
        public float ShakeStrength = 0.6f;
        public float ShakeDuration = 0.35f;
        [Tooltip("Radius (m) of the dust puff at the hardest hit; 0 — none.")]
        public float DustRadius = 1.5f;
    }
}

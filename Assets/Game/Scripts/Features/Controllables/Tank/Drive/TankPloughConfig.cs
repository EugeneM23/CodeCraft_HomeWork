using UnityEngine;

namespace Game
{
    /// <summary>How a tank's nose rips a chunk out of the ground it hits and bounces off it (TankPloughBehaviour). One asset per preset.</summary>
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

        [Header("Chunk")]
        [Tooltip("Radius (m) of one of the three digs across the nose that rip the chunk out, at the lightest hit.")]
        public float BiteRadiusMin = 0.6f;
        [Tooltip("…and at the hardest.")]
        public float BiteRadiusMax = 1.1f;
        [Tooltip("The digs go this share of their radius down into the slope as well as ahead: how deep the notch is.")]
        public float BiteDepth = 0.8f;
        [Tooltip("Seconds between chunks while the nose keeps meeting ground hard.")]
        public float BiteInterval = 0.05f;
        [Tooltip("The chunk flies ahead at the hull's speed times this.")]
        public float ThrowScale = 0.85f;
        [Tooltip("…and this much of that up: 1 at 45°.")]
        public float ThrowLift = 0.8f;
        [Tooltip("Share of the earth dug out that flies as the chunk (ThrowEarth); the rest is gone.")]
        public float ThrowVolume = 0.2f;
        [Tooltip("Spread of the chunk at the lightest hit: 0 one big clump, 1 a wide fan of small ones.")]
        public float SpreadMin = 0f;
        [Tooltip("…and at the hardest.")]
        public float SpreadMax = 0.2f;

        [Header("Bounce")]
        [Tooltip("Share of the speed the hardest hit costs; a light one costs half of it.")]
        public float SpeedLoss = 0.3f;
        [Tooltip("Slope (°) of the ramp the nose rips out of what it hit and the hull pushes off along, whatever facet it met. Lower jumps less.")]
        public float MaxRamp = 18f;
        [Tooltip("How far the velocity is turned up off that slope at the hardest hit.")]
        public float Deflect = 0f;
        [Tooltip("m/s up the hardest hit adds, as an impulse at the bow (it lifts the nose too).")]
        public float Hop = 1.5f;
        [Tooltip("rad/s nose-up the hull spins at least (times the hit's strength) as it pushes off.")]
        public float PitchKick = 0.5f;
        [Tooltip("Seconds after a bounce the hull climbs no faster than the bounce sent it: the jump is this preset's, not the slope's.")]
        public float ClimbTime = 0.6f;
        [Tooltip("Seconds after a bounce before the next hit can turn the hull again.")]
        public float ImpactCooldown = 0.3f;

        [Header("Feel")]
        [Tooltip("How hard the same hit feels in this preset (sound event, camera shake): the impact on the scale all presets share, times this. Not the chunk or the bounce.")]
        public float ImpactScale = 0.8f;
        public float ShakeStrength = 0.6f;
        public float ShakeDuration = 0.35f;
    }
}

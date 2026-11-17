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
        [Tooltip("Most debris cubes each of the three digs of a bite may throw (times ThrowVolume), while the earth goes out through Dig.")]
        public int BiteDebris = 160;
        [Tooltip("Bites go this share of their radius down into the slope as well as ahead: the cut the nose sinks into.")]
        public float BiteDepth = 0.8f;
        [Tooltip("Seconds the nose keeps cutting under and ahead of itself after a hit: the longer, the deeper the hull sinks into the slope.")]
        public float PloughTime = 0.25f;
        [Tooltip("Share of the springs' stiffness while the nose digs in: lower lets the hull sink deeper into the cut.")]
        public float DigSpring = 0.3f;

        [Header("Response")]
        [Tooltip("Share of the speed the hardest hit costs; a light one costs half of it.")]
        public float SpeedLoss = 0.33f;
        [Tooltip("Steepest slope (°) the hull leaves a hit along: a voxel step's vertical face is dug out, not climbed. Lower flies less.")]
        public float MaxRamp = 15f;
        [Tooltip("How far the velocity is turned up off the slope at the hardest hit.")]
        public float Deflect = 0f;
        [Tooltip("m/s up the hardest hit adds as it rises, as an impulse at the bow (it lifts the nose too).")]
        public float Hop = 0f;
        [Tooltip("rad/s nose-up the hull is held to at least (times the hit's strength) as it rises, until RisePitch.")]
        public float PitchKick = 1.2f;
        [Tooltip("Nose-up (°) the hull is brought to as it comes out of the cut.")]
        public float RisePitch = 14f;
        [Tooltip("Seconds after the rise before the next hit can turn the hull again (at least the 0.5 s climb).")]
        public float ImpactCooldown = 0.3f;

        [Header("Feel")]
        [Tooltip("How hard the same hit feels in this preset (sound event, camera shake): the impact on the scale all presets share, times this. Not the bite or the hull's response.")]
        public float ImpactScale = 0.8f;
        public float ShakeStrength = 0.6f;
        public float ShakeDuration = 0.35f;
    }
}

using System;
using UnityEngine;

namespace Game
{
    /// <summary>Auto machine gun, turrets, ricochet and air strike numbers (GDD §3, §4).</summary>
    [Serializable]
    public sealed class WeaponSettings
    {
        [Header("Auto machine gun")]
        [Tooltip("Range (m) within which the machine gun picks the nearest enemy.")]
        public float autoGunRange = 18f;

        [Tooltip("Seconds between target searches of the machine gun and the turrets.")]
        public float retargetInterval = 0.1f;

        [Tooltip("Steepest pitch (degrees) the machine gun and the turrets aim up or down.")]
        public float maxPitch = 20f;

        [Tooltip("Right mouse button aims the auto guns at the cursor point on the ground. Off: RMB fires the machine gun as before.")]
        public bool focusMode = true;

        [Header("Machine gun look (experiment)")]
        [Tooltip("Builds the big machine gun on the turret roof at attach (MachineGunRig): it turns to its target. Off: the old small gun, only the fire point turns.")]
        public bool mgBigModel = true;

        [Tooltip("Degrees per second the gun's body turns about the turret's up.")]
        public float mgYawSpeed = 360f;

        [Tooltip("Degrees per second the barrel tilts up or down.")]
        public float mgPitchSpeed = 180f;

        [Tooltip("The gun fires only while its body is within this many degrees of the aim direction.")]
        public float mgFireAngle = 6f;

        [Tooltip("Where the gun's turning base sits on the roof, in the turret's space (m).")]
        public Vector3 mgMount = new(0.285f, 0.45f, -0.09f);

        [Tooltip("Size (m) of the gun's body: width, height, length.")]
        public Vector3 mgBodySize = new(0.4f, 0.35f, 0.7f);

        [Tooltip("Size (m) of the barrel's jacket: width, height, length. A thinner bore and a muzzle run on past it.")]
        public Vector3 mgBarrelSize = new(0.25f, 0.25f, 1.2f);

        [Header("Casings (experiment)")]
        [Tooltip("Voxel casings fly out of the machine gun and the turrets and lie on the ground (CasingField).")]
        public bool casingsOn = true;

        [Tooltip("Casings kept at once, in flight and lying; the oldest is reused.")]
        public int casingCapacity = 256;

        [Tooltip("Size (m) of a casing: width, height, length.")]
        public Vector3 casingSize = new(0.03f, 0.03f, 0.09f);

        public Color casingColor = new(0.85f, 0.62f, 0.22f, 1f);

        [Tooltip("m/s a casing leaves at: x to the gun's right, y up, z along the barrel (+ the tank's own velocity).")]
        public Vector3 casingEjectSpeed = new(2.6f, 2.8f, -0.3f);

        [Tooltip("What a casing lands on. Not the tank's layer (9): the casings fall past the hull to the ground.")]
        public LayerMask casingGroundMask = (1 << 0) | (1 << 3) | (1 << 10) | (1 << 14) | (1 << 16);

        [Header("Turrets (skill)")]
        public float turretDamage = 8f;

        [Tooltip("Seconds between shots of one turret.")]
        public float turretInterval = 0.12f;

        public float turretRange = 18f;

        [Tooltip("Spread (degrees to either side) of a turret's bullets.")]
        public float turretSpread = 2f;

        [Tooltip("Size (m) of a turret's box: width, height, length.")]
        public Vector3 turretSize = new(0.35f, 0.25f, 0.5f);

        [Header("Ricochet (skill)")]
        [Tooltip("A cannon shell bounces after its blast to the nearest enemy within this range (m), outside the blast.")]
        public float ricochetRange = 15f;

        [Header("Air strike (skill)")]
        [Tooltip("Seconds between strikes at skill levels 1..5.")]
        public float[] airStrikeIntervals = { 10f, 9f, 8f, 7f, 6f };

        [Tooltip("Skill level from which two bombs fall per strike.")]
        public int airStrikeTwoBombsLevel = 5;

        public float airStrikeDamage = 400f;

        [Tooltip("Damage radius (m) of a skill bomb; the prototype's crater is 4 m too.")]
        public float airStrikeRadius = 4f;

        [Tooltip("Share of the damage dealt at the edge of the radius.")]
        public float airStrikeMinFactor = 0.25f;

        [Tooltip("The bomb falls on the densest crowd within this range (m) of the tank.")]
        public float airStrikeSearchRange = 30f;

        /// <summary>Strike interval at a skill level (1..5; beyond the table the last entry).</summary>
        public float GetAirStrikeInterval(int level)
        {
            if (airStrikeIntervals == null || airStrikeIntervals.Length == 0)
                return 10f;
            int i = Mathf.Clamp(level - 1, 0, airStrikeIntervals.Length - 1);
            return Mathf.Max(0.5f, airStrikeIntervals[i]);
        }
    }
}

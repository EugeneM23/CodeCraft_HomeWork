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

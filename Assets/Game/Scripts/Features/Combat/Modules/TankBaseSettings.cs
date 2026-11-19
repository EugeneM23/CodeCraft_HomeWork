using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A tank's base numbers (GDD §4) with the standard modules at level 1. Health, speed and turret come from the
    /// core's <see cref="TankConfig"/> when the catalog has the tank; the rest (boost, cannon, machine gun, crush) is
    /// only here.
    /// </summary>
    [Serializable]
    public sealed class TankBaseSettings
    {
        [Tooltip("Hit points without a catalog tank.")]
        [Min(1f)] public float Health = 1000f;

        [Tooltip("Top speed (m/s) without a catalog tank.")]
        [Min(0f)] public float Speed = 6f;

        [Tooltip("Top speed (m/s) with Shift held; the ratio to Speed is kept for any tank.")]
        [Min(0f)] public float BoostSpeed = 10.8f;

        [Tooltip("Turret, degrees per second, without a catalog tank.")]
        [Min(0f)] public float TurretSpeed = 90f;

        [Header("Cannon (88-mm HE)")]
        [Min(0f)] public float CannonDamage = 150f;

        [Tooltip("Blast radius (m).")]
        [Min(0f)] public float CannonRadius = 5f;

        [Tooltip("Reload (s).")]
        [Min(0.01f)] public float CannonReload = 2f;

        [Header("Machine gun")]
        [Min(0f)] public float MgDamage = 10f;

        [Tooltip("Seconds between shots.")]
        [Min(0.01f)] public float MgInterval = 0.16f;

        [Tooltip("Degrees to either side.")]
        [Min(0f)] public float MgSpread = 2f;

        [Tooltip("Range (m) of the automatic aim.")]
        [Min(0f)] public float MgRange = 18f;

        [Header("Crush")]
        [Tooltip("Slowest (m/s) the tank still crushes a horde monster.")]
        [Min(0f)] public float CrushSpeed = 1.5f;

        [Tooltip("Multiplier of the crush box's width.")]
        [Min(0f)] public float CrushWidthFactor = 1f;
    }
}

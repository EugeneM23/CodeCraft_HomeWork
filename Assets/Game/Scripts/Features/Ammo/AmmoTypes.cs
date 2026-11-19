using System;
using UnityEngine;

namespace Game
{
    /// <summary>The cannon's shell types.</summary>
    public enum ShellKind
    {
        /// <summary>Осколочно-фугасный: bursts on contact, a big blast and crater, low penetration, never ricochets.</summary>
        HighExplosive = 0,

        /// <summary>Бронебойный: high penetration, ricochets by angle, punches through armour; a small blast.</summary>
        ArmourPiercing = 1
    }

    /// <summary>One shell type's numbers.</summary>
    [Serializable]
    public struct ShellAmmo
    {
        [Tooltip("Short name for the HUD and the lab's readout: «ОФ», «ББ».")]
        public string shortName;

        public string fullName;

        [Tooltip("Penetration, mm of plate at 0°.")]
        public float penetration;

        [Tooltip("Calibre, mm (overmatch).")]
        public float calibre;

        [Tooltip("Times the cannon's damage (its modules included).")]
        public float damageFactor;

        [Tooltip("Times the cannon's blast radius (its modules included).")]
        public float radiusFactor;

        [Tooltip("Flight speed, m/s.")]
        public float speed;

        [Tooltip("Angle from a surface's normal (deg) at or over which the shell glances off; 0 — it never does.")]
        public float ricochetAngle;

        [Tooltip("Most ricochets one shell makes.")]
        public int maxRicochets;

        [Tooltip("Share of its speed a shell keeps after a ricochet.")]
        public float ricochetSpeedKeep;

        [Tooltip("The shell's glow (ShellGlowView) and trail.")]
        public Color glow;

        [Tooltip("Penetration (mm) a metre of a house's wall takes from the shell going through it; 0 — it never goes through a house.")]
        public float wallCost;

        [Tooltip("Share of its speed a shell loses per metre of a house's wall it goes through.")]
        public float wallSlow;

        [Tooltip("Radius (m) of the hole the shell punches in a house's wall, before a building's smaller bite (VoxelBody.BiteRadius).")]
        public float houseHole;

        public bool CanRicochet => ricochetAngle > 0f && maxRicochets > 0;

        /// <summary>
        /// mm of penetration a metre of brick takes: a house's 20 cm outer wall costs 24 mm, so a fresh armour-piercing
        /// shell (125 mm) goes through both outer walls and the inner ones and floors between them.
        /// </summary>
        public const float WALL_COST = 120f;

        /// <summary>
        /// The armour-piercing hole in a house: 0.55 m bitten as 0.5 m (BiteRadius), a quarter wider than the 0.4 m
        /// its small burst dug before (user, 2026-11-19: «дырочку чуть-чуть шире»).
        /// </summary>
        public const float HOUSE_HOLE = 0.55f;

        /// <summary>Whether the shell goes on through a house's walls (over-penetration) instead of bursting on the first.</summary>
        public bool CanPassWalls => wallCost > 0f && houseHole > 0f;

        public static ShellAmmo DefaultHighExplosive => new()
        {
            shortName = "ОФ", fullName = "Осколочно-фугасный", penetration = 35f, calibre = 88f, damageFactor = 1f,
            radiusFactor = 1f, speed = 60f, ricochetAngle = 0f, maxRicochets = 0, ricochetSpeedKeep = 0f,
            glow = new Color(1f, 0.55f, 0.18f)
        };

        public static ShellAmmo DefaultArmourPiercing => new()
        {
            shortName = "ББ", fullName = "Бронебойный", penetration = 125f, calibre = 88f, damageFactor = 1.3f,
            radiusFactor = 0.25f, speed = 95f, ricochetAngle = 70f, maxRicochets = 2, ricochetSpeedKeep = 0.7f,
            glow = new Color(0.6f, 0.85f, 1f), wallCost = WALL_COST, wallSlow = 0.5f, houseHole = HOUSE_HOLE
        };
    }
}

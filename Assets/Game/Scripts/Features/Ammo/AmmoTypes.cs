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

        public bool CanRicochet => ricochetAngle > 0f && maxRicochets > 0;

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
            glow = new Color(0.6f, 0.85f, 1f)
        };
    }
}

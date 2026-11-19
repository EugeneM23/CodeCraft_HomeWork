using System;
using UnityEngine;

namespace Game
{
    /// <summary>What a shell did to one armour zone it struck.</summary>
    public enum ArmourOutcome
    {
        /// <summary>Glanced off the plate: no hole, the shell flies on along the reflected path.</summary>
        Ricochet,

        /// <summary>The plate held: a dent and a scorch, no hole.</summary>
        NoPenetration,

        /// <summary>Through the plate: a hole, the shell goes on inside.</summary>
        Penetration,

        /// <summary>Through spaced armour (tracks, screens): the shell loses some penetration and flies on.</summary>
        SpacedPassed,

        /// <summary>A module or crewman inside was hit.</summary>
        ModuleHit
    }

    /// <summary>How a zone takes a shell.</summary>
    public enum ArmourKind
    {
        /// <summary>A solid plate: ricochet, hold or let the shell in.</summary>
        Plate,

        /// <summary>Spaced armour (tracks, screens): eats penetration, the shell flies on to the plate behind.</summary>
        Spaced,

        /// <summary>A module or crewman inside: no armour of its own, the hit registers.</summary>
        Module
    }

    /// <summary>Which way a plate faces, in its part's local frame (+z is the front). Flags: a zone may cover several.</summary>
    [Flags]
    public enum ArmourFacing
    {
        None = 0,
        Front = 1,
        Rear = 2,
        Left = 4,
        Right = 8,
        Top = 16,
        Bottom = 32,
        Any = Front | Rear | Left | Right | Top | Bottom
    }

    /// <summary>An armour-piercing shell's numbers.</summary>
    [Serializable]
    public struct ArmourShell
    {
        [Tooltip("Penetration, mm of plate at 0°.")]
        public float penetration;

        [Tooltip("Calibre, mm: a shell over the overmatch factor times a plate's thickness does not ricochet off it.")]
        public float calibre;

        [Tooltip("Muzzle speed, m/s (the lab slows the tracer down so it can be followed).")]
        public float speed;

        [Tooltip("How far (±, share) one shot's penetration strays from the nominal.")]
        public float spread;

        public ArmourShell(float penetration, float calibre, float speed = 900f, float spread = 0.08f)
        {
            this.penetration = penetration;
            this.calibre = calibre;
            this.speed = speed;
            this.spread = spread;
        }
    }

    /// <summary>The armour rules shared by every tank (one config can override them per model).</summary>
    [Serializable]
    public struct ArmourRules
    {
        [Tooltip("Angle from the plate's normal (deg) at or over which a shell glances off.")]
        public float ricochetAngle;

        [Tooltip("A calibre over this many times the plate's thickness overmatches it: no ricochet.")]
        public float overmatch;

        [Tooltip("Degrees a shell that bites in turns toward the plate's normal (cuts the effective thickness).")]
        public float normalisation;

        [Tooltip("Share of its penetration a shell keeps after a ricochet.")]
        public float ricochetKeep;

        public static ArmourRules Default => new() { ricochetAngle = 70f, overmatch = 3f, normalisation = 5f, ricochetKeep = 0.6f };
    }

    /// <summary>One shell against one armour zone: the numbers and the outcome.</summary>
    public struct ArmourResult
    {
        public ArmourOutcome Outcome;

        /// <summary>Angle between the shell's path and the plate's normal, degrees (0 — head-on).</summary>
        public float Angle;

        /// <summary>The plate's nominal thickness, mm.</summary>
        public float Nominal;

        /// <summary>Thickness along the shell's path after normalisation, mm.</summary>
        public float Effective;

        /// <summary>The shell's penetration on this hit (with its spread), mm.</summary>
        public float Penetration;

        /// <summary>What penetration the shell keeps after this hit, mm (0 when it stopped).</summary>
        public float Remaining;

        public bool Overmatched;
    }

    /// <summary>One event on a shell's path: where it struck what, and what came of it.</summary>
    public struct ArmourHit
    {
        public Vector3 Point;

        /// <summary>The plate's smoothed outward normal at the hit.</summary>
        public Vector3 Normal;

        /// <summary>The shell's path into the hit.</summary>
        public Vector3 Direction;

        /// <summary>Metres along the shell's whole path (ricochets included) to this hit.</summary>
        public float Distance;

        /// <summary>The zone's name; null when the shell struck something with no armour (the ground, the table).</summary>
        public string Zone;

        public ArmourKind Kind;
        public ArmourResult Result;

        /// <summary>The armoured tank struck; null on the ground.</summary>
        public TankArmour Armour;

        /// <summary>The voxel body struck, if any.</summary>
        public Voxels.VoxelBody Body;

        public Collider Collider;

        /// <summary>The shell struck something with no armour (the ground, the table, a wall) and stopped there.</summary>
        public bool IsTerrain => Armour == null;
    }
}

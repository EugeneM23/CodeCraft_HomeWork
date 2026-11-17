using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Game.Horde
{
    /// <summary>What a monster slot is doing.</summary>
    public enum HordeState : byte
    {
        /// <summary>The slot is empty (on the free list).</summary>
        Free = 0,
        /// <summary>Walking (or standing) toward the tank.</summary>
        Walk = 1,
        /// <summary>At the hull, clawing it.</summary>
        Attack = 2,
        /// <summary>Killed this step; freed when the step is completed.</summary>
        Dying = 3
    }

    /// <summary>Why a monster died.</summary>
    public enum HordeDeathCause : byte
    {
        Crushed = 1,
        Explosion = 2,
        /// <summary>Killed through <see cref="HordeSimulation.Kill"/>.</summary>
        Killed = 3
    }

    /// <summary>Tuning of the horde. Speeds in m/s, angles in radians, damage in health points.</summary>
    public struct HordeSettings
    {
        /// <summary>Walking speed before the per-monster jitter.</summary>
        public float Speed;
        /// <summary>Per-monster speed scale is 1 ± this.</summary>
        public float SpeedJitter;
        /// <summary>Body radius: the hull reach and the crush box are padded by it.</summary>
        public float Radius;
        /// <summary>Neighbours closer than this push each other apart.</summary>
        public float SeparationRadius;
        /// <summary>Push speed at zero distance, m/s.</summary>
        public float SeparationStrength;
        /// <summary>Neighbours read per monster at most (the rest of a dense crowd is ignored).</summary>
        public int MaxNeighbours;
        /// <summary>Max turn rate, rad/s.</summary>
        public float TurnRate;
        /// <summary>How fast the velocity follows the wish, 1/s.</summary>
        public float Acceleration;
        /// <summary>Monsters slow down over this distance from the hull.</summary>
        public float SlowRadius;
        /// <summary>Gap to the hull at which a monster attacks.</summary>
        public float AttackReach;
        /// <summary>Damage per second of one attacking monster.</summary>
        public float AttackDps;
        /// <summary>Tank speed above which a monster inside the hull box is crushed.</summary>
        public float CrushSpeed;
        /// <summary>Health of a fresh monster.</summary>
        public float Health;
        /// <summary>Monsters within this distance of the tank count as "near" (audio density).</summary>
        public float NearRadius;
        /// <summary>Walking speed the run clip was authored for: the clip plays at speed / this.</summary>
        public float RunClipSpeed;

        public static HordeSettings Default => new HordeSettings
        {
            Speed = 4.5f,
            SpeedJitter = 0.2f,
            Radius = 0.6f,
            SeparationRadius = 1.4f,
            SeparationStrength = 3f,
            MaxNeighbours = 10,
            TurnRate = 6f,
            Acceleration = 6f,
            SlowRadius = 3f,
            AttackReach = 0.5f,
            AttackDps = 2f,
            CrushSpeed = 1.5f,
            Health = 100f,
            NearRadius = 15f,
            RunClipSpeed = 5f
        };
    }

    /// <summary>The tank as the horde sees it: an oriented box and its velocity.</summary>
    public struct HordeTank
    {
        public bool Present;
        /// <summary>World centre of the hull box.</summary>
        public float3 Center;
        public quaternion Rotation;
        public float3 HalfExtents;
        public float3 Velocity;
    }

    /// <summary>A blast: monsters within <see cref="Radius"/> take <see cref="Damage"/>, half at the edge.</summary>
    public struct HordeAreaDamage
    {
        public float3 Center;
        public float Radius;
        public float Damage;
    }

    /// <summary>One death, for the game side (corpses, blood, rewards, sound).</summary>
    public struct HordeDeathEvent
    {
        public int Index;
        public float3 Position;
        public float Heading;
        public HordeDeathCause Cause;
    }

    /// <summary>
    /// Per-instance data of the crowd draw (32 bytes; the shader's <c>HordeInstance</c>).
    /// <c>PositionHeading</c>: feet position and yaw (0 faces +Z).
    /// <c>Animation</c>: VAT rows of the two frames to blend, the blend, and a free slot
    /// (the corpse birth time for corpses).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HordeInstance
    {
        public const int STRIDE = 32;

        public float4 PositionHeading;
        public float4 Animation;
    }

    /// <summary>A clip in the VAT texture: <c>count</c> rows from <c>start</c>, played at <c>frameRate</c>.</summary>
    [Serializable]
    public struct HordeClip
    {
        public int start;
        public int count;
        public float frameRate;
        public bool loop;

        /// <summary>The two rows to blend at <paramref name="time"/> seconds, and the blend.</summary>
        public float3 Sample(float time)
        {
            if (count <= 1)
                return new float3(start, start, 0f);
            float f = time * frameRate;
            int last = loop ? count : count - 1;
            f = loop ? f - math.floor(f / last) * last : math.min(f, last);
            int a = (int)math.floor(f);
            int b = a + 1;
            if (b >= count)
                b = loop ? 0 : count - 1;
            if (a >= count)
                a = count - 1;
            return new float3(start + a, start + b, f - math.floor(f));
        }
    }

    /// <summary>
    /// Shape of a <see cref="HordeNavGrid"/>: which world rect it covers, how fine, and how it reads
    /// the ground.
    /// </summary>
    public struct HordeNavGridSettings
    {
        public const float DEFAULT_CELL_SIZE = 0.5f;
        public const float DEFAULT_MAX_STEP = 0.6f;
        public const int LAYER_DEFAULT = 0;
        public const int LAYER_GROUND = 3;

        /// <summary>World XZ of the grid's min corner.</summary>
        public float2 Origin;
        /// <summary>Cells along X and Z.</summary>
        public int2 Size;
        /// <summary>Side of a square cell, metres.</summary>
        public float CellSize;
        /// <summary>World Y the downward rays start from: above the tallest thing to stand on.</summary>
        public float RayTop;
        /// <summary>How far down the rays look.</summary>
        public float RayLength;
        /// <summary>Largest height difference a walker climbs between neighbour cells.</summary>
        public float MaxStep;
        /// <summary>Layers that count as something to stand on (Ground and Default by default).</summary>
        public int LayerMask;

        /// <summary>Settings that cover the world rect <paramref name="min"/>..<paramref name="max"/> (XZ).</summary>
        public static HordeNavGridSettings Cover(float2 min, float2 max, float cellSize = DEFAULT_CELL_SIZE,
            float rayTop = 30f, float rayLength = 60f)
        {
            int2 size = (int2)math.ceil((max - min) / cellSize);
            return new HordeNavGridSettings
            {
                Origin = min,
                Size = math.max(size, 1),
                CellSize = cellSize,
                RayTop = rayTop,
                RayLength = rayLength,
                MaxStep = DEFAULT_MAX_STEP,
                LayerMask = (1 << LAYER_GROUND) | (1 << LAYER_DEFAULT)
            };
        }
    }

    /// <summary>What a <see cref="HordePathfinder"/> search found.</summary>
    public enum HordePathStatus
    {
        /// <summary>No search finished yet.</summary>
        None,
        /// <summary>The path reaches the goal.</summary>
        Found,
        /// <summary>The goal is cut off; the path leads to the reachable cell closest to it.</summary>
        Partial,
        /// <summary>The start is outside the grid or on a cell with no ground.</summary>
        Failed
    }
}

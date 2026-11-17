using Unity.Mathematics;

namespace Game.Horde
{
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

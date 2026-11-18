using UnityEngine;

namespace Game
{
    /// <summary>The gate: a wall across a gap in the far edge, and the way out behind it.</summary>
    public struct GatePlan
    {
        /// <summary>Middle of the wall's footprint, on the ground (world).</summary>
        public Vector3 Position;

        /// <summary>Wall grid size in voxels, along X.</summary>
        public Vector3Int Size;

        /// <summary>Where the tank counts as gone through: a little past the wall, outside the arena.</summary>
        public Vector3 Exit;
    }
}

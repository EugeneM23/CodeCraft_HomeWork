using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>A house to build: a <see cref="VoxelStructure"/> standing on a flat pad.</summary>
    public struct HousePlan
    {
        public VoxelStructure.Shape Shape;

        /// <summary>Grid size in voxels (X width, Y height, Z depth); the front faces -Z, toward the entry.</summary>
        public Vector3Int Size;

        /// <summary>Middle of the footprint, on the ground (world).</summary>
        public Vector3 Position;

        public int Seed;

        /// <summary>The footprint on the ground (world X/Z), for spacing and burrows.</summary>
        public Rect Footprint;
    }
}

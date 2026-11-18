using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>A tree: a <see cref="VoxelStructure"/> of shape Tree.</summary>
    public struct TreePlan
    {
        public Vector3 Position;
        public Vector3Int Size;
        public int Seed;

        /// <summary>Outside the playable square: scenery round the edge.</summary>
        public bool Outside;
    }
}

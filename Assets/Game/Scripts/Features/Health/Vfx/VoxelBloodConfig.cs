using System;
using UnityEngine;

namespace Game
{
    /// <summary>How much a hit on a living body bleeds, in voxel drops.</summary>
    [Serializable]
    public sealed class VoxelBloodConfig
    {
        [Tooltip("Drops from a bullet, min–max.")]
        public Vector2Int bulletDrops = new(30, 60);

        [Tooltip("Drops from a melee hit, min–max; they come out of the chest.")]
        public Vector2Int meleeDrops = new(50, 80);

        [Tooltip("Drops from a blast that hurts but does not kill.")]
        public int blastDrops = 100;

        public float minSpeed = 1.5f;
        public float maxSpeed = 3.5f;

        [Range(0f, 1f)] public float spread = 0.4f;

        public BloodLook blood = new();
    }
}

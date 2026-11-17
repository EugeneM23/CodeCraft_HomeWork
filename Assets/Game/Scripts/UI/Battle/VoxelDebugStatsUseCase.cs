using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Counts the voxel world's numbers for the debug block. Walks every voxel body, so the HUD calls it a few
    /// times a second, not every frame. Index loops over the lists: no allocations.
    /// </summary>
    public static class VoxelDebugStatsUseCase
    {
        public static void Read(out VoxelDebugStats stats)
        {
            stats = default;

            IReadOnlyList<VoxelBody> bodies = VoxelWorld.Bodies;
            stats.Bodies = bodies.Count;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body == null)
                    continue;
                stats.Voxels += body.SolidCount;
                if (body.TryGetComponent(out Rigidbody _))
                    stats.LoosePieces++;
            }

            // VoxelWorld.Instance creates a world when there is none: only ask once bodies say there is one.
            VoxelWorld world = stats.Bodies > 0 ? VoxelWorld.Instance : null;
            if (world != null)
            {
                stats.Debris = world.Debris != null ? world.Debris.Count : 0;
                stats.RemeshChunks = world.LastRebuildChunks;
                stats.RemeshMs = world.LastRebuildMs;
                stats.AsyncInFlight = world.AsyncInFlight;
            }

            IReadOnlyList<VoxelSmoke> smokes = VoxelSmoke.Active;
            stats.SmokeClouds = smokes.Count;
            for (int i = 0; i < smokes.Count; i++)
            {
                if (smokes[i] != null)
                    stats.SmokeCubes += smokes[i].DrawnCubes;
            }
        }
    }
}

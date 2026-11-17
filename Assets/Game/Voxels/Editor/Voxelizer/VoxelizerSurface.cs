using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>A part's surface on the voxel grid: which cells its triangles pass through, and their colour.</summary>
    public static class VoxelizerSurface
    {
        public const int MAX_SIZE = 512;
        public const float INSIDE_SHADE = 0.85f;

        /// <summary>
        /// In voxels: how far a point on a cell boundary is nudged into the body before it is floored, so a face
        /// lying on a boundary (float noise either side of it) always lands in the cell inside, never outside.
        /// </summary>
        public const float BOUNDARY_NUDGE = 1e-3f;

        public static VoxelizerPartGrid Sample(VoxelizerTriangles triangles, float voxelSize)
        {
            float3 origin = math.floor(triangles.Min / voxelSize) * voxelSize - voxelSize;
            int3 size = math.clamp((int3)math.ceil((triangles.Max - origin) / voxelSize) + 2, 1, MAX_SIZE);
            var grid = new VoxelizerPartGrid
            {
                Size = size,
                Origin = origin,
                VoxelSize = voxelSize,
                Cells = new NativeArray<Color32>(size.x * size.y * size.z, Allocator.Persistent)
            };

            var keys = new NativeList<ulong>(triangles.Count * 16, Allocator.TempJob);
            new SampleSurfaceJob
            {
                Corners = triangles.Corners, Uvs = triangles.Uvs, Material = triangles.Material,
                Tints = triangles.Tints, Pixels = triangles.Pixels, Textures = triangles.Textures,
                Origin = origin, VoxelSize = voxelSize, Size = size, Keys = keys
            }.Run();
            keys.Sort();
            new ResolveCellsJob { Keys = keys.AsArray(), Cells = grid.Cells }.Run();
            keys.Dispose();
            return grid;
        }

        public static void Fill(VoxelizerPartGrid grid)
        {
            new FloodOutsideJob { Cells = grid.Cells, Size = grid.Size }.Run();
            new ShadeInteriorJob { Cells = grid.Cells, Size = grid.Size, Darken = INSIDE_SHADE }.Run();
        }
    }
}

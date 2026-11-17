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
    }
}

using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxels.EditorTools
{
    /// <summary>The «auto» straightening: faces near an axis lie on the grid, details under the minimum go.</summary>
    public static class VoxelizerStraighten
    {
        /// <summary>Before Sample: faces within maxAngleDeg of an axis move onto a voxel boundary (at most half a voxel).</summary>
        public static void SnapPlanes(VoxelizerTriangles triangles, float voxelSize, float maxAngleDeg = 10f) =>
            new SnapPlanesJob
            {
                Corners = triangles.Corners,
                VoxelSize = voxelSize,
                CosMaxAngle = math.cos(math.radians(maxAngleDeg))
            }.Run();

        /// <summary>After Fill: opening by a 3×3×3 cube, minDetail − 1 passes; minDetail below 2 does nothing.</summary>
        public static void Open(VoxelizerPartGrid grid, int minDetail = 2)
        {
            if (minDetail < 2)
                return;
            new OpenJob { Cells = grid.Cells, Size = grid.Size, Passes = minDetail - 1 }.Run();
        }
    }
}

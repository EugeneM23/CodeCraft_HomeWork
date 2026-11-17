using Unity.Collections;
using Unity.Mathematics;

namespace Game.Horde.Tests
{
    /// <summary>Grids with hand-set heights for the pure-logic tests: origin 0, 1 m cells.</summary>
    internal static class HordeTestGrids
    {
        public static HordeNavGrid Flat(int width, int depth, float maxStep = HordeNavGridSettings.DEFAULT_MAX_STEP)
        {
            var settings = HordeNavGridSettings.Cover(float2.zero, new float2(width, depth), 1f);
            settings.MaxStep = maxStep;
            var grid = new HordeNavGrid(settings);
            using var heights = new NativeArray<float>(width * depth, Allocator.Temp);
            grid.SetHeights(heights);
            return grid;
        }

        /// <summary>Raises the cell rect <paramref name="min"/>..<paramref name="max"/> (inclusive) to <paramref name="height"/>.</summary>
        public static void Block(HordeNavGrid grid, int2 min, int2 max, float height = 5f)
        {
            for (int z = min.y; z <= max.y; z++)
            for (int x = min.x; x <= max.x; x++)
                grid.SetHeight(new int2(x, z), height);
        }

        public static float3 Center(int2 cell) => new float3(cell.x + 0.5f, 0f, cell.y + 0.5f);
    }
}

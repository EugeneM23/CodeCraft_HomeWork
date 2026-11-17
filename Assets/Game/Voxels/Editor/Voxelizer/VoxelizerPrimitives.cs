using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>
    /// Parts marked «box» or «cylinder»: replaced by a clean primitive over the part's bounds, as TankBuilder builds them.
    /// Every surface cell of the primitive takes the colour of the nearest SURFACE cell of the part's sampled grid
    /// (BFS over 26 neighbours); inside cells take that colour × INSIDE_SHADE. The BFS is managed editor code, not a Burst job.
    /// </summary>
    public static class VoxelizerPrimitives
    {
        public static void Box(VoxelizerPartGrid grid) =>
            Replace(grid, -1);

        public static void Cylinder(VoxelizerPartGrid grid, int axis) =>
            Replace(grid, math.clamp(axis, 0, 2));

        private static void Replace(VoxelizerPartGrid grid, int axis)
        {
            int3 size = grid.Size;
            int3 lo = size, hi = -1;
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                if (grid.Cells[i].a == 0) continue;
                int3 p = new int3(i % size.x, (i / size.x) % size.y, i / (size.x * size.y));
                lo = math.min(lo, p);
                hi = math.max(hi, p);
            }

            if (math.any(hi < lo))
                return;

            Color32[] nearest = NearestSurfaceColours(grid);

            float3 centre = (float3)(lo + hi + 1) * 0.5f;
            float3 half = (float3)(hi - lo + 1) * 0.5f;
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                int3 p = new int3(i % size.x, (i / size.x) % size.y, i / (size.x * size.y));
                bool inBox = math.all(p >= lo) && math.all(p <= hi);
                bool solid = inBox && (axis < 0 || InDisc(p, centre, half, axis));
                grid.Cells[i] = solid ? nearest[i] : default;
            }

            // Cells with all six neighbours solid are inside.
            int sy = size.x, sz = size.x * size.y;
            Color32[] copy = grid.Cells.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].a == 0) continue;
                int x = i % size.x, y = (i / size.x) % size.y, z = i / sz;
                if (x == 0 || y == 0 || z == 0 || x == size.x - 1 || y == size.y - 1 || z == size.z - 1) continue;
                if (copy[i - 1].a != 0 && copy[i + 1].a != 0 && copy[i - sy].a != 0 && copy[i + sy].a != 0 && copy[i - sz].a != 0 && copy[i + sz].a != 0)
                {
                    Color32 c = copy[i];
                    grid.Cells[i] = new Color32((byte)(c.r * VoxelizerSurface.INSIDE_SHADE), (byte)(c.g * VoxelizerSurface.INSIDE_SHADE),
                        (byte)(c.b * VoxelizerSurface.INSIDE_SHADE), VoxelizerPartGrid.INSIDE);
                }
            }
        }

        /// <summary>For every cell of the grid, the SURFACE colour (alpha SURFACE) of the nearest SURFACE cell: multi-source BFS over 26 neighbours.</summary>
        private static Color32[] NearestSurfaceColours(VoxelizerPartGrid grid)
        {
            int3 size = grid.Size;
            int count = grid.Cells.Length;
            var result = new Color32[count];
            var visited = new bool[count];
            var queue = new Queue<int>();
            for (int i = 0; i < count; i++)
            {
                Color32 c = grid.Cells[i];
                if (c.a != VoxelizerPartGrid.SURFACE) continue;
                result[i] = c;
                visited[i] = true;
                queue.Enqueue(i);
            }

            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % size.x, y = (i / size.x) % size.y, z = i / (size.x * size.y);
                for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy, nz = z + dz;
                    if (nx < 0 || ny < 0 || nz < 0 || nx >= size.x || ny >= size.y || nz >= size.z) continue;
                    int n = nx + size.x * (ny + size.y * nz);
                    if (visited[n]) continue;
                    visited[n] = true;
                    result[n] = result[i];
                    queue.Enqueue(n);
                }
            }

            return result;
        }

        private static bool InDisc(int3 p, float3 centre, float3 half, int axis)
        {
            float3 d = ((float3)p + 0.5f - centre) / half;
            float2 q = axis == 0 ? d.yz : axis == 1 ? d.xz : d.xy;
            return math.lengthsq(q) <= 1f;
        }
    }
}

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>Parts marked «box» or «cylinder»: replaced by a clean primitive over the part's bounds, as TankBuilder builds them.</summary>
    public static class VoxelizerPrimitives
    {
        public static void Box(VoxelizerPartGrid grid) =>
            Replace(grid, -1);

        public static void Cylinder(VoxelizerPartGrid grid, int axis) =>
            Replace(grid, math.clamp(axis, 0, 2));

        private static void Replace(VoxelizerPartGrid grid, int axis)
        {
            int3 lo = grid.Size, hi = -1;
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                Color32 c = grid.Cells[i];
                if (c.a == 0) continue;
                int3 p = new int3(i % grid.Size.x, (i / grid.Size.x) % grid.Size.y, i / (grid.Size.x * grid.Size.y));
                lo = math.min(lo, p);
                hi = math.max(hi, p);
                if (c.a == VoxelizerPartGrid.SURFACE)
                {
                    int key = c.r << 16 | c.g << 8 | c.b;
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                }
            }

            if (math.any(hi < lo))
                return;

            int best = 0, bestCount = -1;
            foreach (KeyValuePair<int, int> pair in counts)
                if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
            var surface = new Color32((byte)(best >> 16), (byte)(best >> 8), (byte)best, VoxelizerPartGrid.SURFACE);
            var shaded = new Color32((byte)(surface.r * VoxelizerSurface.INSIDE_SHADE), (byte)(surface.g * VoxelizerSurface.INSIDE_SHADE),
                (byte)(surface.b * VoxelizerSurface.INSIDE_SHADE), VoxelizerPartGrid.INSIDE);

            float3 centre = (float3)(lo + hi + 1) * 0.5f;
            float3 half = (float3)(hi - lo + 1) * 0.5f;
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                int3 p = new int3(i % grid.Size.x, (i / grid.Size.x) % grid.Size.y, i / (grid.Size.x * grid.Size.y));
                bool inBox = math.all(p >= lo) && math.all(p <= hi);
                bool solid = inBox && (axis < 0 || InDisc(p, centre, half, axis));
                grid.Cells[i] = solid ? surface : default;
            }

            // Cells with all six neighbours solid are inside.
            int sy = grid.Size.x, sz = grid.Size.x * grid.Size.y;
            Color32[] copy = grid.Cells.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].a == 0) continue;
                int x = i % grid.Size.x, y = (i / grid.Size.x) % grid.Size.y, z = i / sz;
                if (x == 0 || y == 0 || z == 0 || x == grid.Size.x - 1 || y == grid.Size.y - 1 || z == grid.Size.z - 1) continue;
                if (copy[i - 1].a != 0 && copy[i + 1].a != 0 && copy[i - sy].a != 0 && copy[i + sy].a != 0 && copy[i - sz].a != 0 && copy[i + sz].a != 0)
                    grid.Cells[i] = shaded;
            }
        }

        private static bool InDisc(int3 p, float3 centre, float3 half, int axis)
        {
            float3 d = ((float3)p + 0.5f - centre) / half;
            float2 q = axis == 0 ? d.yz : axis == 1 ? d.xz : d.xy;
            return math.lengthsq(q) <= 1f;
        }
    }
}

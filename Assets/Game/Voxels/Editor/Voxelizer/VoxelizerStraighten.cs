using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxels.EditorTools
{
    /// <summary>The «auto» straightening: faces near an axis lie on the grid, details one voxel thin go.</summary>
    public static class VoxelizerStraighten
    {
        /// <summary>Corners closer than this (model units) count as one vertex when faces are joined; positions up to ±21 km.</summary>
        public const float WELD = 1e-5f;

        /// <summary>
        /// Before Sample. Triangles whose normal lies within maxAngleDeg of the same signed axis and that share
        /// edges form one face; the face moves along that axis onto the centre of the voxel layer holding its
        /// area-weighted mean, so it samples as one even layer, the layer it really lies in (a mean on a layer
        /// boundary picks the layer inside the body, behind the face). A face with any
        /// corner outside that layer and the two next to it is left alone (a slope, not a flat face): its
        /// unmoved neighbours end at those corners, and a gap of a whole layer would let the outside fill in.
        /// Min/Max are updated.
        /// </summary>
        public static void SnapPlanes(VoxelizerTriangles triangles, float voxelSize, float maxAngleDeg = 10f)
        {
            NativeArray<float3> corners = triangles.Corners;
            int count = triangles.Count;
            float cosMax = math.cos(math.radians(maxAngleDeg));

            // Signed axis per triangle: 0..5 = +X −X +Y −Y +Z −Z, −1 = none.
            var axisOf = new int[count];
            var area = new float[count];
            for (int t = 0; t < count; t++)
            {
                axisOf[t] = -1;
                float3 a = corners[t * 3], b = corners[t * 3 + 1], c = corners[t * 3 + 2];
                float3 n = math.cross(b - a, c - a);
                float length = math.length(n);
                if (length < 1e-12f)
                    continue;
                float3 an = math.abs(n / length);
                int axis = an.x >= an.y && an.x >= an.z ? 0 : an.y >= an.z ? 1 : 2;
                if (an[axis] < cosMax)
                    continue;
                axisOf[t] = axis * 2 + (n[axis] < 0f ? 1 : 0);
                area[t] = length * 0.5f;
            }

            // Join triangles of one signed axis that share an edge.
            var parent = new int[count];
            for (int t = 0; t < count; t++)
                parent[t] = t;
            var edges = new Dictionary<(int3, int3, int), int>();
            for (int t = 0; t < count; t++)
            {
                if (axisOf[t] < 0)
                    continue;
                for (int k = 0; k < 3; k++)
                {
                    int3 p = Weld(corners[t * 3 + k]), q = Weld(corners[t * 3 + (k + 1) % 3]);
                    var key = Less(p, q) ? (p, q, axisOf[t]) : (q, p, axisOf[t]);
                    if (edges.TryGetValue(key, out int other))
                        parent[Find(parent, t)] = Find(parent, other);
                    else
                        edges.Add(key, t);
                }
            }

            // Per face: area-weighted mean along the axis and the lowest and highest corner.
            var sum = new Dictionary<int, (double weighted, double area, float min, float max)>();
            for (int t = 0; t < count; t++)
            {
                if (axisOf[t] < 0)
                    continue;
                int axis = axisOf[t] / 2, root = Find(parent, t);
                float a = corners[t * 3][axis], b = corners[t * 3 + 1][axis], c = corners[t * 3 + 2][axis];
                sum.TryGetValue(root, out var s);
                if (s.area == 0)
                    s = (0, 0, float.MaxValue, float.MinValue);
                s.weighted += area[t] * (a + b + c) / 3.0;
                s.area += area[t];
                s.min = math.min(s.min, math.min(a, math.min(b, c)));
                s.max = math.max(s.max, math.max(a, math.max(b, c)));
                sum[root] = s;
            }

            float3 min = new float3(float.MaxValue), max = new float3(float.MinValue);
            for (int t = 0; t < count; t++)
            {
                if (axisOf[t] >= 0)
                {
                    int axis = axisOf[t] / 2;
                    var s = sum[Find(parent, t)];
                    float mean = s.area > 0 ? (float)(s.weighted / s.area) : 0f;
                    // A face on a layer boundary (within float noise) belongs to the layer inside the body:
                    // below the boundary for a +axis face, above it for a −axis face.
                    float inward = (axisOf[t] & 1) == 0 ? -VoxelizerSurface.BOUNDARY_NUDGE : VoxelizerSurface.BOUNDARY_NUDGE;
                    float layer = math.floor(mean / voxelSize + inward);
                    if (s.area > 0 && math.floor(s.min / voxelSize + inward) >= layer - 1f && math.floor(s.max / voxelSize + inward) <= layer + 1f)
                    {
                        float plane = (layer + 0.5f) * voxelSize;
                        for (int k = 0; k < 3; k++)
                        {
                            float3 p = corners[t * 3 + k];
                            p[axis] = plane;
                            corners[t * 3 + k] = p;
                        }
                    }
                }

                for (int k = 0; k < 3; k++)
                {
                    min = math.min(min, corners[t * 3 + k]);
                    max = math.max(max, corners[t * 3 + k]);
                }
            }

            if (count > 0)
            {
                triangles.Min = min;
                triangles.Max = max;
            }
        }

        /// <summary>
        /// After Fill: opening by a 2×2×2 cube, so features one voxel thick go and everything two voxels thick
        /// or more stays with its colours. minDetail below 2 does nothing; 2 and above all act as 2.
        /// </summary>
        public static void Open(VoxelizerPartGrid grid, int minDetail = 2)
        {
            if (minDetail < 2)
                return;
            new OpenJob { Cells = grid.Cells, Size = grid.Size }.Run();
        }

        private static int3 Weld(float3 p) => new int3((int)math.round(p.x / WELD), (int)math.round(p.y / WELD), (int)math.round(p.z / WELD));

        private static bool Less(int3 a, int3 b) => a.x != b.x ? a.x < b.x : a.y != b.y ? a.y < b.y : a.z < b.z;

        private static int Find(int[] parent, int t)
        {
            while (parent[t] != t)
            {
                parent[t] = parent[parent[t]];
                t = parent[t];
            }

            return t;
        }
    }
}

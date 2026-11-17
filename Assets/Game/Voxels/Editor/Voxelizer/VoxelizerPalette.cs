using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>The model's own palette: its surface colours brought down to K by k-means; inside colours are the same × 0.85.</summary>
    public static class VoxelizerPalette
    {
        public static Color32[] Build(IReadOnlyList<VoxelizerPartGrid> parts, int k, out Color32[] centres)
        {
            k = math.clamp(k, 1, 32);
            var weight = new Dictionary<int, int>();
            foreach (VoxelizerPartGrid part in parts)
            foreach (Color32 c in part.Cells)
            {
                if (c.a != VoxelizerPartGrid.SURFACE) continue;
                int key = c.r << 16 | c.g << 8 | c.b;
                weight.TryGetValue(key, out int n);
                weight[key] = n + 1;
            }

            var points = new List<float3>();
            var weights = new List<int>();
            foreach (var pair in weight)
            {
                points.Add(new float3(pair.Key >> 16 & 0xFF, pair.Key >> 8 & 0xFF, pair.Key & 0xFF));
                weights.Add(pair.Value);
            }

            List<float3> means = KMeans(points, weights, k);
            centres = new Color32[means.Count];
            var palette = new Color32[256];
            for (int i = 0; i < means.Count; i++)
            {
                float3 m = means[i];
                centres[i] = new Color32((byte)m.x, (byte)m.y, (byte)m.z, 255);
                palette[1 + i] = MeshVoxelizer.ToVertexColor((Color)centres[i]);
                palette[1 + k + i] = MeshVoxelizer.ToVertexColor((Color)centres[i] * VoxelizerSurface.INSIDE_SHADE);
            }

            return palette;
        }

        public static NativeArray<byte> Map(VoxelizerPartGrid part, Color32[] centres, int k, Allocator allocator)
        {
            var voxels = new NativeArray<byte>(part.Cells.Length, allocator);
            for (int i = 0; i < part.Cells.Length; i++)
            {
                Color32 c = part.Cells[i];
                if (c.a == 0) continue;
                // Inside cells are the surface × 0.85: undo it to find the same centre.
                float3 p = c.a == VoxelizerPartGrid.INSIDE
                    ? new float3(c.r, c.g, c.b) / VoxelizerSurface.INSIDE_SHADE
                    : new float3(c.r, c.g, c.b);
                int nearest = Nearest(centres, p);
                voxels[i] = (byte)(1 + nearest + (c.a == VoxelizerPartGrid.INSIDE ? k : 0));
            }

            return voxels;
        }

        public static void Smooth(NativeArray<byte> voxels, int3 size, int k, int passes = 2) =>
            new MajorityJob { Voxels = voxels, Size = size, LastSurface = k, Passes = passes }.Run();

        private static int Nearest(Color32[] centres, float3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < centres.Length; i++)
            {
                float d = math.distancesq(p, new float3(centres[i].r, centres[i].g, centres[i].b));
                if (d < bestD) { bestD = d; best = i; }
            }

            return best;
        }

        private static List<float3> KMeans(List<float3> points, List<int> weights, int k)
        {
            var means = new List<float3>();
            if (points.Count == 0)
                return means;
            var random = new Unity.Mathematics.Random(5);
            int heaviest = 0;
            for (int i = 1; i < points.Count; i++) if (weights[i] > weights[heaviest]) heaviest = i;
            means.Add(points[heaviest]);
            var d2 = new float[points.Count];
            while (means.Count < math.min(k, points.Count))
            {
                float total = 0f;
                for (int i = 0; i < points.Count; i++)
                {
                    float m = float.MaxValue;
                    foreach (float3 c in means) m = math.min(m, math.distancesq(points[i], c));
                    d2[i] = m * weights[i];
                    total += d2[i];
                }

                if (total <= 0f) break;
                float r = random.NextFloat(total);
                int pick = 0;
                for (; pick < points.Count - 1; pick++) { r -= d2[pick]; if (r <= 0f) break; }
                means.Add(points[pick]);
            }

            var sum = new float3[means.Count];
            var count = new float[means.Count];
            for (int iteration = 0; iteration < 12; iteration++)
            {
                System.Array.Clear(sum, 0, sum.Length);
                System.Array.Clear(count, 0, count.Length);
                for (int i = 0; i < points.Count; i++)
                {
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int c = 0; c < means.Count; c++)
                    {
                        float d = math.distancesq(points[i], means[c]);
                        if (d < bestD) { bestD = d; best = c; }
                    }

                    sum[best] += points[i] * weights[i];
                    count[best] += weights[i];
                }

                for (int c = 0; c < means.Count; c++)
                    if (count[c] > 0f) means[c] = sum[c] / count[c];
            }

            return means;
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>
    /// Points every 0.35 voxel over each triangle, each with its colour (tint × texture), keyed so
    /// that a sort brings each cell's points together, grouped by colour bin (4 bits a channel).
    /// Key: cell (27 bits) | rgb (24 bits) | bin (12 bits).
    /// </summary>
    [BurstCompile]
    public struct SampleSurfaceJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Corners;
        [ReadOnly] public NativeArray<float2> Uvs;
        [ReadOnly] public NativeArray<int> Material;
        [ReadOnly] public NativeArray<Color32> Tints;
        [ReadOnly] public NativeArray<Color32> Pixels;
        [ReadOnly] public NativeArray<int4> Textures;
        public float3 Origin;
        public float VoxelSize;
        public int3 Size;
        public NativeList<ulong> Keys;

        public void Execute()
        {
            float step = VoxelSize * 0.35f;
            for (int t = 0; t < Corners.Length / 3; t++)
            {
                float3 a = Corners[t * 3], b = Corners[t * 3 + 1], c = Corners[t * 3 + 2];
                float edge = math.max(math.distance(a, b), math.max(math.distance(b, c), math.distance(c, a)));
                int n = math.clamp((int)math.ceil(edge / step), 1, 4096);
                for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n - i; j++)
                {
                    float u = i / (float)n, v = j / (float)n, w = 1f - u - v;
                    float3 p = a * w + b * u + c * v;
                    int3 cell = (int3)math.floor((p - Origin) / VoxelSize);
                    if (math.any(cell < 0) || math.any(cell >= Size))
                        continue;
                    float2 uv = Uvs[t * 3] * w + Uvs[t * 3 + 1] * u + Uvs[t * 3 + 2] * v;
                    Color32 colour = Shade(Material[t], uv);
                    ulong index = (ulong)(cell.x + Size.x * (cell.y + Size.y * cell.z));
                    ulong rgb = ((ulong)colour.r << 16) | ((ulong)colour.g << 8) | colour.b;
                    ulong bin = ((ulong)(uint)(colour.r >> 4) << 8) | ((ulong)(uint)(colour.g >> 4) << 4) | (uint)(colour.b >> 4);
                    Keys.Add((index << 36) | (rgb << 12) | bin);
                }
            }
        }

        private Color32 Shade(int material, float2 uv)
        {
            Color32 tint = Tints[material];
            int4 texture = Textures[material];
            if (texture.w == 0)
                return tint;
            int x = (int)math.floor(math.frac(uv.x) * texture.y);
            int y = (int)math.floor(math.frac(uv.y) * texture.z);
            Color32 texel = Pixels[texture.x + math.clamp(y, 0, texture.z - 1) * texture.y + math.clamp(x, 0, texture.y - 1)];
            return new Color32((byte)(tint.r * texel.r / 255), (byte)(tint.g * texel.g / 255), (byte)(tint.b * texel.b / 255), 255);
        }
    }

    /// <summary>Each cell's colour: the colour bin most of its points fell in, its points averaged.</summary>
    [BurstCompile]
    public struct ResolveCellsJob : IJob
    {
        [ReadOnly] public NativeArray<ulong> Keys; // sorted
        public NativeArray<Color32> Cells;

        public void Execute()
        {
            var count = new NativeArray<int>(4096, Allocator.Temp);
            var sum = new NativeArray<int3>(4096, Allocator.Temp);
            var touched = new NativeList<int>(64, Allocator.Temp);
            int i = 0;
            while (i < Keys.Length)
            {
                ulong cell = Keys[i] >> 36;
                int best = -1;
                for (; i < Keys.Length && Keys[i] >> 36 == cell; i++)
                {
                    int bin = (int)(Keys[i] & 0xFFF);
                    int rgb = (int)((Keys[i] >> 12) & 0xFFFFFF);
                    if (count[bin] == 0)
                        touched.Add(bin);
                    count[bin]++;
                    sum[bin] += new int3(rgb >> 16, (rgb >> 8) & 0xFF, rgb & 0xFF);
                    if (best < 0 || count[bin] > count[best])
                        best = bin;
                }

                int3 mean = sum[best] / count[best];
                Cells[(int)cell] = new Color32((byte)mean.x, (byte)mean.y, (byte)mean.z, VoxelizerPartGrid.SURFACE);
                for (int k = 0; k < touched.Length; k++)
                {
                    count[touched[k]] = 0;
                    sum[touched[k]] = int3.zero;
                }

                touched.Clear();
            }
        }
    }
}

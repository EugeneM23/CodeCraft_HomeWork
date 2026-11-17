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

    /// <summary>
    /// Floods the empty space from the grid's border; every empty cell it never reaches is inside
    /// (a = INSIDE). Unlike parity rays it does not care whether the mesh is closed or overlaps itself.
    /// </summary>
    [BurstCompile]
    public struct FloodOutsideJob : IJob
    {
        public NativeArray<Color32> Cells;
        public int3 Size;

        public void Execute()
        {
            var outside = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            var queue = new NativeQueue<int>(Allocator.Temp);
            for (int z = 0; z < Size.z; z++)
            for (int y = 0; y < Size.y; y++)
            for (int x = 0; x < Size.x; x++)
            {
                bool border = x == 0 || y == 0 || z == 0 || x == Size.x - 1 || y == Size.y - 1 || z == Size.z - 1;
                int i = x + Size.x * (y + Size.y * z);
                if (border && Cells[i].a == 0)
                {
                    outside[i] = true;
                    queue.Enqueue(i);
                }
            }

            int sy = Size.x, sz = Size.x * Size.y;
            while (queue.TryDequeue(out int i))
            {
                int x = i % Size.x, y = (i / Size.x) % Size.y, z = i / sz;
                if (x > 0) Visit(i - 1, ref outside, ref queue);
                if (x < Size.x - 1) Visit(i + 1, ref outside, ref queue);
                if (y > 0) Visit(i - sy, ref outside, ref queue);
                if (y < Size.y - 1) Visit(i + sy, ref outside, ref queue);
                if (z > 0) Visit(i - sz, ref outside, ref queue);
                if (z < Size.z - 1) Visit(i + sz, ref outside, ref queue);
            }

            for (int i = 0; i < Cells.Length; i++)
                if (Cells[i].a == 0 && !outside[i])
                    Cells[i] = new Color32(0, 0, 0, VoxelizerPartGrid.INSIDE);

            outside.Dispose();
            queue.Dispose();
        }

        private void Visit(int j, ref NativeArray<bool> outside, ref NativeQueue<int> queue)
        {
            if (outside[j] || Cells[j].a != 0)
                return;
            outside[j] = true;
            queue.Enqueue(j);
        }
    }

    /// <summary>Inside cells take the colour of the nearest surface (breadth-first from it), × Darken.</summary>
    [BurstCompile]
    public struct ShadeInteriorJob : IJob
    {
        public NativeArray<Color32> Cells;
        public int3 Size;
        public float Darken;

        public void Execute()
        {
            var done = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            var queue = new NativeQueue<int>(Allocator.Temp);
            for (int i = 0; i < Cells.Length; i++)
                if (Cells[i].a == VoxelizerPartGrid.SURFACE)
                {
                    done[i] = true;
                    queue.Enqueue(i);
                }

            int sy = Size.x, sz = Size.x * Size.y;
            while (queue.TryDequeue(out int i))
            {
                Color32 from = Cells[i];
                int x = i % Size.x, y = (i / Size.x) % Size.y, z = i / sz;
                if (x > 0) Spread(i - 1, from, ref done, ref queue);
                if (x < Size.x - 1) Spread(i + 1, from, ref done, ref queue);
                if (y > 0) Spread(i - sy, from, ref done, ref queue);
                if (y < Size.y - 1) Spread(i + sy, from, ref done, ref queue);
                if (z > 0) Spread(i - sz, from, ref done, ref queue);
                if (z < Size.z - 1) Spread(i + sz, from, ref done, ref queue);
            }

            done.Dispose();
            queue.Dispose();
        }

        private void Spread(int j, Color32 from, ref NativeArray<bool> done, ref NativeQueue<int> queue)
        {
            if (done[j] || Cells[j].a != VoxelizerPartGrid.INSIDE)
                return;
            done[j] = true;
            bool fromSurface = from.a == VoxelizerPartGrid.SURFACE;
            Cells[j] = fromSurface
                ? new Color32((byte)(from.r * Darken), (byte)(from.g * Darken), (byte)(from.b * Darken), VoxelizerPartGrid.INSIDE)
                : new Color32(from.r, from.g, from.b, VoxelizerPartGrid.INSIDE);
            queue.Enqueue(j);
        }
    }

    /// <summary>
    /// Flattens every triangle whose normal lies within MaxAngle of an axis onto the voxel boundary
    /// nearest its mean, a quarter voxel into the body, so a nearly flat face samples as one even layer.
    /// </summary>
    [BurstCompile]
    public struct SnapPlanesJob : IJob
    {
        public NativeArray<float3> Corners;
        public float VoxelSize;
        public float CosMaxAngle;

        public void Execute()
        {
            for (int t = 0; t < Corners.Length / 3; t++)
            {
                float3 a = Corners[t * 3], b = Corners[t * 3 + 1], c = Corners[t * 3 + 2];
                float3 n = math.cross(b - a, c - a);
                float length = math.length(n);
                if (length < 1e-12f)
                    continue;
                n /= length;
                float3 an = math.abs(n);
                int axis = an.x >= an.y && an.x >= an.z ? 0 : an.y >= an.z ? 1 : 2;
                if (an[axis] < CosMaxAngle)
                    continue;

                float mean = (a[axis] + b[axis] + c[axis]) / 3f;
                float plane = math.round(mean / VoxelSize) * VoxelSize - 0.25f * VoxelSize * math.sign(n[axis]);
                a[axis] = plane; b[axis] = plane; c[axis] = plane;
                Corners[t * 3] = a; Corners[t * 3 + 1] = b; Corners[t * 3 + 2] = c;
            }
        }
    }

    /// <summary>
    /// Morphological opening with a 3×3×3 cube, Passes times each way: erosion keeps a cell only if all
    /// 26 neighbours are solid, dilation gives back cells that were solid before, with their own colour.
    /// One pass removes every part that holds no full 3×3×3 cube, so anything up to 2 voxels thick.
    /// </summary>
    [BurstCompile]
    public struct OpenJob : IJob
    {
        public NativeArray<Color32> Cells;
        public int3 Size;
        public int Passes;

        public void Execute()
        {
            var original = new NativeArray<Color32>(Cells, Allocator.Temp);
            var mask = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            var next = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            for (int i = 0; i < Cells.Length; i++)
                mask[i] = Cells[i].a != 0;

            for (int p = 0; p < Passes; p++)
            {
                for (int i = 0; i < mask.Length; i++)
                    next[i] = mask[i] && AllNeighbours(mask, i);
                (mask, next) = (next, mask);
            }

            for (int p = 0; p < Passes; p++)
            {
                for (int i = 0; i < mask.Length; i++)
                    next[i] = mask[i] || (original[i].a != 0 && AnyNeighbour(mask, i));
                (mask, next) = (next, mask);
            }

            for (int i = 0; i < Cells.Length; i++)
                Cells[i] = mask[i] ? original[i] : default;

            original.Dispose();
            mask.Dispose();
            next.Dispose();
        }

        /// <summary>All 26 neighbours solid; a cell on the grid's edge never is.</summary>
        private bool AllNeighbours(NativeArray<bool> m, int i)
        {
            int3 p = new int3(i % Size.x, (i / Size.x) % Size.y, i / (Size.x * Size.y));
            if (math.any(p == 0) || math.any(p == Size - 1))
                return false;
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (!m[(p.x + dx) + Size.x * ((p.y + dy) + Size.y * (p.z + dz))])
                    return false;
            return true;
        }

        /// <summary>Any of the 26 neighbours solid.</summary>
        private bool AnyNeighbour(NativeArray<bool> m, int i)
        {
            int3 p = new int3(i % Size.x, (i / Size.x) % Size.y, i / (Size.x * Size.y));
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int3 q = p + new int3(dx, dy, dz);
                if (math.all(q >= 0) && math.all(q < Size) && m[q.x + Size.x * (q.y + Size.y * q.z)])
                    return true;
            }

            return false;
        }
    }
}

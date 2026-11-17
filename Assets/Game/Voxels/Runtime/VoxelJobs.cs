using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Greedy meshing of one chunk. For each of the six face directions the chunk is swept slice
    /// by slice; every visible face goes into a 2D mask, and runs of the same material in the mask
    /// are merged into rectangles. A flat wall of one colour becomes one quad instead of hundreds.
    /// Neighbours are read from the whole body grid, so faces on chunk borders are culled too.
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct GreedyMeshJob : IJob
    {
        /// <summary>The whole grid (Offset 0, Dims = Size) or a copy of the box round the chunk (see <see cref="RegionOf"/>).</summary>
        [ReadOnly] public NativeArray<byte> Voxels;
        [ReadOnly] public NativeArray<Color32> Palette;
        public int3 Size;
        /// <summary>Grid coordinates of Voxels[0].</summary>
        public int3 Offset;
        /// <summary>Extent of Voxels.</summary>
        public int3 Dims;
        public int3 Min;
        public int3 Max;
        public float VoxelSize;
        public float3 Origin;
        /// <summary>
        /// Ground: below the grid counts as solid, so the underside of the bottom layer — never
        /// seen, bedrock lies under it — is not meshed (about an eighth of the ground's triangles).
        /// </summary>
        public bool ClosedBelow;

        public NativeList<VoxelVertex> Vertices;
        public NativeList<int> Indices;

        /// <summary>The box a chunk's faces read: the chunk and one voxel round it, cut to the grid.</summary>
        public static void RegionOf(int3 min, int3 max, int3 size, out int3 offset, out int3 dims)
        {
            int3 lo = math.max(0, min - 1);
            int3 hi = math.min(size - 1, max);
            offset = lo;
            dims = hi - lo + 1;
        }

        public void Execute()
        {
            int3 extent = Max - Min;
            int maskSide = math.cmax(extent);
            var mask = new NativeArray<byte>(maskSide * maskSide, Allocator.Temp);

            // Ground is solid below its surface and air above: a layer where every voxel (and its
            // ring) is the same holds no side faces, and two equal layers no top or bottom faces
            // between them. Only the few mixed layers are swept — a 64³ chunk of flat ground has
            // one to three of its 64.
            var layers = new NativeArray<byte>(extent.y + 2, Allocator.Temp);
            for (int ly = 0; ly < layers.Length; ly++)
                layers[ly] = LayerState(Min.y - 1 + ly);
            var mixed = new NativeList<int>(extent.y, Allocator.Temp);
            for (int y = 0; y < extent.y; y++)
                if (layers[y + 1] == 2)
                    mixed.Add(y);

            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;
                int du = extent[u];
                int dv = extent[v];

                for (int side = 0; side < 2; side++)
                {
                    bool positive = side == 1;
                    int3 step = int3.zero;
                    step[d] = positive ? 1 : -1;

                    for (int s = 0; s < extent[d]; s++)
                    {
                        bool any;
                        // The part of the mask that can hold faces: all of it for a horizontal
                        // slice, the rows of the mixed layers for an upright one.
                        int iTo = du;
                        int jFrom = 0;
                        int jTo = dv;
                        int iFrom = 0;
                        if (d == 1)
                        {
                            // A slice between two equal, uniform layers has no faces at all.
                            byte here = layers[s + 1];
                            if (here != 2 && here == layers[s + 1 + step.y])
                                continue;
                            any = FillMask(mask, d, u, v, s, step, 0, du, 0, dv, du);
                        }
                        else
                        {
                            if (mixed.Length == 0)
                                continue;

                            // Side faces only in mixed layers: y runs along u (d = x) or v (d = z).
                            for (int k = 0; k < du * dv; k++)
                                mask[k] = 0;
                            any = false;
                            for (int n = 0; n < mixed.Length; n++)
                            {
                                int y = mixed[n];
                                any |= u == 1
                                    ? FillMask(mask, d, u, v, s, step, y, y + 1, 0, dv, du)
                                    : FillMask(mask, d, u, v, s, step, 0, du, y, y + 1, du);
                            }

                            if (u == 1)
                            {
                                iFrom = mixed[0];
                                iTo = mixed[mixed.Length - 1] + 1;
                            }
                            else
                            {
                                jFrom = mixed[0];
                                jTo = mixed[mixed.Length - 1] + 1;
                            }
                        }

                        if (!any)
                            continue;

                        for (int j = jFrom; j < jTo; j++)
                        {
                            for (int i = iFrom; i < iTo;)
                            {
                                byte m = mask[i + j * du];
                                if (m == 0)
                                {
                                    i++;
                                    continue;
                                }

                                int w = 1;
                                while (i + w < du && mask[i + w + j * du] == m)
                                    w++;

                                int h = 1;
                                while (j + h < dv)
                                {
                                    bool rowMatches = true;
                                    for (int k = 0; k < w; k++)
                                    {
                                        if (mask[i + k + (j + h) * du] != m)
                                        {
                                            rowMatches = false;
                                            break;
                                        }
                                    }

                                    if (!rowMatches)
                                        break;
                                    h++;
                                }

                                int3 corner = Min;
                                corner[d] += s + (positive ? 1 : 0);
                                corner[u] += i;
                                corner[v] += j;
                                EmitQuad(corner, d, u, v, w, h, positive, Palette[m]);

                                for (int y = 0; y < h; y++)
                                for (int x = 0; x < w; x++)
                                    mask[i + x + (j + y) * du] = 0;

                                i += w;
                            }
                        }
                    }
                }
            }
        }

        private byte Get(int3 p) =>
            VoxelMath.Inside(p, Size) ? Voxels[VoxelMath.Index(p - Offset, Dims)] :
            ClosedBelow && p.y < 0 ? (byte)1 : (byte)0;

        /// <summary>Visible faces of slice s into the mask over i ∈ [i0, i1), j ∈ [j0, j1). True if any.</summary>
        private bool FillMask(NativeArray<byte> mask, int d, int u, int v, int s, int3 step, int i0, int i1, int j0,
            int j1, int du)
        {
            bool any = false;
            for (int j = j0; j < j1; j++)
            for (int i = i0; i < i1; i++)
            {
                int3 p = Min;
                p[d] += s;
                p[u] += i;
                p[v] += j;
                byte m = Get(p);
                m = m != 0 && Get(p + step) == 0 ? m : (byte)0;
                mask[i + j * du] = m;
                any |= m != 0;
            }

            return any;
        }

        /// <summary>
        /// 0 — every voxel of layer y over the chunk and its one-voxel ring reads empty, 1 — all
        /// solid, 2 — mixed. What <see cref="Get"/> returns outside the grid counts too.
        /// </summary>
        private byte LayerState(int y)
        {
            if (y < 0)
                return ClosedBelow ? (byte)1 : (byte)0;
            if (y >= Size.y)
                return 0;

            int3 lo = Min - 1;
            int3 hi = Max;
            // Outside the grid sideways reads as empty.
            bool empty = lo.x < 0 || lo.z < 0 || hi.x >= Size.x || hi.z >= Size.z;
            bool solid = false;
            for (int z = math.max(lo.z, 0); z <= math.min(hi.z, Size.z - 1); z++)
            for (int x = math.max(lo.x, 0); x <= math.min(hi.x, Size.x - 1); x++)
            {
                if (Get(new int3(x, y, z)) != 0)
                    solid = true;
                else
                    empty = true;
                if (solid && empty)
                    return 2;
            }

            return solid ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Unity draws the side where cross(b - a, c - a) points, so +d faces go a → a+U → a+U+V
        /// (U × V = +d for u = d+1, v = d+2) and -d faces the other way round.
        /// </summary>
        private void EmitQuad(int3 corner, int d, int u, int v, int w, int h, bool positive, Color32 color)
        {
            float3 a = Origin + (float3)corner * VoxelSize;
            float3 du = float3.zero;
            du[u] = w * VoxelSize;
            float3 dv = float3.zero;
            dv[v] = h * VoxelSize;
            float3 normal = float3.zero;
            normal[d] = positive ? 1f : -1f;

            int b = Vertices.Length;
            Vertices.Add(new VoxelVertex { Position = a, Normal = normal, Color = color });
            Vertices.Add(new VoxelVertex { Position = a + du, Normal = normal, Color = color });
            Vertices.Add(new VoxelVertex { Position = a + du + dv, Normal = normal, Color = color });
            Vertices.Add(new VoxelVertex { Position = a + dv, Normal = normal, Color = color });

            if (positive)
            {
                Indices.Add(b); Indices.Add(b + 1); Indices.Add(b + 2);
                Indices.Add(b); Indices.Add(b + 2); Indices.Add(b + 3);
            }
            else
            {
                Indices.Add(b); Indices.Add(b + 2); Indices.Add(b + 1);
                Indices.Add(b); Indices.Add(b + 3); Indices.Add(b + 2);
            }
        }
    }

    /// <summary>
    /// Covers the solid voxels of one chunk with as few boxes as it can: grow along X, then whole
    /// rows along Y, then whole slabs along Z. A solid chunk is one box collider; a chewed one a few
    /// dozen. PhysX handles boxes far better than a concave mesh, and boxes work on moving bodies.
    /// </summary>
    [BurstCompile]
    public struct BoxDecomposeJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public int3 Size;
        public int3 Min;
        public int3 Max;

        /// <summary>
        /// Edge (voxels) of a collision cell: 1 — every voxel; 3 — 30 cm cells, each solid if a
        /// third of it is. Ragged things (tree crowns) need no more for physics, and get a few
        /// dozen boxes instead of thousands.
        /// </summary>
        public int Cell;

        public NativeList<VoxelBox> Boxes;

        public void Execute()
        {
            int cell = math.max(1, Cell);
            int3 extent = Max - Min;
            int3 e = (extent + cell - 1) / cell;
            var solid = new NativeArray<bool>(e.x * e.y * e.z, Allocator.Temp);
            var visited = new NativeArray<bool>(e.x * e.y * e.z, Allocator.Temp);

            int threshold = math.max(1, cell * cell * cell / 3);
            for (int z = 0; z < e.z; z++)
            for (int y = 0; y < e.y; y++)
            for (int x = 0; x < e.x; x++)
            {
                int3 from = Min + new int3(x, y, z) * cell;
                int3 to = math.min(from + cell, Max);
                int count = 0;
                for (int k = from.z; k < to.z; k++)
                for (int j = from.y; j < to.y; j++)
                for (int i = from.x; i < to.x; i++)
                    if (Voxels[VoxelMath.Index(new int3(i, j, k), Size)] != 0)
                        count++;
                solid[Local(new int3(x, y, z), e)] = count >= math.min(threshold, (to.x - from.x) * (to.y - from.y) * (to.z - from.z));
            }

            for (int z = 0; z < e.z; z++)
            for (int y = 0; y < e.y; y++)
            for (int x = 0; x < e.x; x++)
            {
                int3 q = new int3(x, y, z);
                if (!Free(q, e, solid, visited))
                    continue;

                int3 s = new int3(1, 1, 1);
                while (x + s.x < e.x && Free(new int3(x + s.x, y, z), e, solid, visited))
                    s.x++;

                while (y + s.y < e.y && RowFree(x, s.x, y + s.y, z, e, solid, visited))
                    s.y++;

                while (z + s.z < e.z && SlabFree(x, s.x, y, s.y, z + s.z, e, solid, visited))
                    s.z++;

                for (int k = 0; k < s.z; k++)
                for (int j = 0; j < s.y; j++)
                for (int i = 0; i < s.x; i++)
                    visited[Local(new int3(x + i, y + j, z + k), e)] = true;

                Boxes.Add(new VoxelBox { Min = Min + q * cell, Max = math.min(Min + (q + s) * cell, Max) });
            }
        }

        private static int Local(int3 q, int3 e) => q.x + e.x * (q.y + e.y * q.z);

        private static bool Free(int3 q, int3 e, NativeArray<bool> solid, NativeArray<bool> visited)
        {
            int index = Local(q, e);
            return solid[index] && !visited[index];
        }

        private static bool RowFree(int x, int w, int y, int z, int3 e, NativeArray<bool> solid, NativeArray<bool> visited)
        {
            for (int i = 0; i < w; i++)
                if (!Free(new int3(x + i, y, z), e, solid, visited))
                    return false;
            return true;
        }

        private static bool SlabFree(int x, int w, int y, int h, int z, int3 e, NativeArray<bool> solid, NativeArray<bool> visited)
        {
            for (int j = 0; j < h; j++)
                if (!RowFree(x, w, y + j, z, e, solid, visited))
                    return false;
            return true;
        }
    }

    /// <summary>
    /// Takes a sphere out of the grid. The edge is roughened with a per-voxel hash so holes look
    /// torn rather than drilled. Every voxel taken is reported, for the debris it throws.
    /// </summary>
    [BurstCompile]
    public struct CarveSphereJob : IJob
    {
        public NativeArray<byte> Voxels;
        public int3 Size;

        /// <summary>Centre in grid units: the centre of voxel i is i + 0.5.</summary>
        public float3 Center;

        public float Radius;

        /// <summary>1 / the crater's height share: distances along Y count this many times over.</summary>
        public float Squash;

        public float Roughness;
        public uint Seed;
        public NativeList<RemovedVoxel> Removed;

        public void Execute()
        {
            float3 reach = new float3(Radius, Radius / math.max(1e-3f, Squash), Radius);
            int3 lo = math.max(0, (int3)math.floor(Center - reach));
            int3 hi = math.min(Size - 1, (int3)math.floor(Center + reach));
            float3 scale = new float3(1f, Squash, 1f);

            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                int3 p = new int3(x, y, z);
                int index = VoxelMath.Index(p, Size);
                byte material = Voxels[index];
                if (material == 0)
                    continue;

                float r = Radius * (1f - Roughness * VoxelMath.Hash01(p, Seed));
                if (math.lengthsq(((float3)p + 0.5f - Center) * scale) > r * r)
                    continue;

                Voxels[index] = 0;
                Removed.Add(new RemovedVoxel { Position = p, Material = material });
            }
        }
    }

    /// <summary>
    /// Takes a wide, shallow bowl out of the grid: an air bomb's crater that a vehicle can drive
    /// out of. Each column round the centre loses what lies within <c>half</c> of the centre's
    /// height, half = Depth · (1 + cos(π · d / rim)) / 2 at distance d. That is level in the middle,
    /// steepest halfway out (π/2 · Depth / rim) and level again at the rim, so no wall stands at
    /// the edge, as it does round a squashed sphere. Above the centre the same curve, mirrored,
    /// shaves an uphill rim just as gently.
    ///
    /// The rim wanders with smooth noise, not a per-voxel hash: single voxels left standing on
    /// the slope would be steps for the tracks. The noise is taken on the direction from the
    /// centre only, so along any line out of the crater the profile stays the same cosine and
    /// the slope stays what it was worked out to be.
    /// </summary>
    [BurstCompile]
    public struct CarveBowlJob : IJob
    {
        public NativeArray<byte> Voxels;
        public int3 Size;

        /// <summary>Centre in grid units, on the surface: the centre of voxel i is i + 0.5.</summary>
        public float3 Center;

        /// <summary>Radius of the rim, in voxels.</summary>
        public float Radius;

        /// <summary>Depth below the centre, in voxels.</summary>
        public float Depth;

        /// <summary>How far the rim wanders in and out, as a share of the radius.</summary>
        public float Roughness;

        public float2 NoiseOffset;
        public NativeList<RemovedVoxel> Removed;

        public void Execute()
        {
            float outer = Radius * (1f + Roughness);
            float3 reach = new float3(outer, Depth, outer);
            int3 lo = math.max(0, (int3)math.floor(Center - reach));
            int3 hi = math.min(Size - 1, (int3)math.floor(Center + reach));

            for (int z = lo.z; z <= hi.z; z++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                float2 flat = new float2(x + 0.5f - Center.x, z + 0.5f - Center.z);
                float distance = math.length(flat);
                if (distance >= outer)
                    continue;

                // Noise round a circle of radius 1.5: a few bulges along the rim.
                float2 direction = distance > 1e-4f ? flat / distance : new float2(1f, 0f);
                float rim = Radius * (1f + Roughness * noise.snoise(direction * 1.5f + NoiseOffset));
                if (distance >= rim)
                    continue;

                float half = Depth * 0.5f * (1f + math.cos(math.PI * distance / rim));
                int yLo = math.max(lo.y, (int)math.floor(Center.y - half));
                int yHi = math.min(hi.y, (int)math.floor(Center.y + half));
                for (int y = yLo; y <= yHi; y++)
                {
                    if (math.abs(y + 0.5f - Center.y) > half)
                        continue;

                    int3 p = new int3(x, y, z);
                    int index = VoxelMath.Index(p, Size);
                    byte material = Voxels[index];
                    if (material == 0)
                        continue;

                    Voxels[index] = 0;
                    Removed.Add(new RemovedVoxel { Position = p, Material = material });
                }
            }
        }
    }

    /// <summary>
    /// Flood fill over the whole grid (6-neighbourhood): which voxels hang together, how many
    /// there are in each piece, its bounds, and whether it rests on the bottom layer.
    /// </summary>
    [BurstCompile]
    public struct LabelComponentsJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public int3 Size;

        /// <summary>Zeroed on input. 0 — empty, otherwise the 1-based index into Components.</summary>
        public NativeArray<int> Labels;

        /// <summary>Scratch, as long as the grid: every voxel is queued once at most.</summary>
        public NativeArray<int> Queue;

        public NativeList<VoxelComponent> Components;

        /// <summary>
        /// Optional fracture cells, one per voxel (see VoronoiCellsJob): when <see cref="UseCells"/> is
        /// set, a piece never crosses from one cell into another. Must be assigned either way.
        /// </summary>
        [ReadOnly] public NativeArray<int> Cells;

        public bool UseCells;

        private int _cell;

        public void Execute()
        {
            int count = Voxels.Length;

            for (int start = 0; start < count; start++)
            {
                if (Voxels[start] == 0 || Labels[start] != 0)
                    continue;

                int label = Components.Length + 1;
                _cell = UseCells ? Cells[start] : 0;
                var info = new VoxelComponent { Min = new int3(int.MaxValue), Max = new int3(int.MinValue) };

                int head = 0;
                int tail = 0;
                Queue[tail++] = start;
                Labels[start] = label;

                while (head < tail)
                {
                    int current = Queue[head++];
                    int3 p = VoxelMath.Coord(current, Size);

                    info.Count++;
                    info.Min = math.min(info.Min, p);
                    info.Max = math.max(info.Max, p);
                    if (p.y == 0)
                        info.Grounded = true;

                    Visit(p + new int3(1, 0, 0), label, ref tail);
                    Visit(p + new int3(-1, 0, 0), label, ref tail);
                    Visit(p + new int3(0, 1, 0), label, ref tail);
                    Visit(p + new int3(0, -1, 0), label, ref tail);
                    Visit(p + new int3(0, 0, 1), label, ref tail);
                    Visit(p + new int3(0, 0, -1), label, ref tail);
                }

                Components.Add(info);
            }
        }

        private void Visit(int3 p, int label, ref int tail)
        {
            if (!VoxelMath.Inside(p, Size))
                return;

            int index = VoxelMath.Index(p, Size);
            if (Voxels[index] == 0 || Labels[index] != 0)
                return;
            if (UseCells && Cells[index] != _cell)
                return;

            Labels[index] = label;
            Queue[tail++] = index;
        }
    }

    /// <summary>Moves one labelled piece out of a grid into a new, tightly sized grid.</summary>
    [BurstCompile]
    public struct ExtractComponentJob : IJob
    {
        public NativeArray<byte> Source;
        [ReadOnly] public NativeArray<int> Labels;
        public int3 SourceSize;
        public int Label;
        public int3 Min;

        public NativeArray<byte> Target;
        public int3 TargetSize;

        public void Execute()
        {
            for (int z = 0; z < TargetSize.z; z++)
            for (int y = 0; y < TargetSize.y; y++)
            for (int x = 0; x < TargetSize.x; x++)
            {
                int3 q = new int3(x, y, z);
                int si = VoxelMath.Index(Min + q, SourceSize);
                if (Labels[si] != Label)
                    continue;

                Target[VoxelMath.Index(q, TargetSize)] = Source[si];
                Source[si] = 0;
            }
        }
    }

    [BurstCompile]
    public struct CountSolidJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public NativeReference<int> Count;

        public void Execute()
        {
            int count = 0;
            for (int i = 0; i < Voxels.Length; i++)
                if (Voxels[i] != 0)
                    count++;
            Count.Value = count;
        }
    }

    /// <summary>
    /// Voxel Voronoi, the way RayFire fractures a mesh, but on the grid: every solid voxel joins
    /// the nearest seed. A per-voxel jitter roughens the borders, so pieces come out jagged
    /// rather than as clean polyhedra. Seeds packed round the point of impact give small pieces
    /// there and big ones further off.
    /// </summary>
    [BurstCompile]
    public struct VoronoiCellsJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        [ReadOnly] public NativeArray<float3> Seeds;
        public int3 Size;
        public float Jitter;
        public uint Seed;

        /// <summary>Out: the seed index per voxel, -1 for empty.</summary>
        public NativeArray<int> Cells;

        public void Execute()
        {
            for (int i = 0; i < Voxels.Length; i++)
            {
                if (Voxels[i] == 0)
                {
                    Cells[i] = -1;
                    continue;
                }

                int3 p = VoxelMath.Coord(i, Size);
                float3 at = (float3)p + 0.5f;
                float best = float.MaxValue;
                int cell = 0;

                for (int s = 0; s < Seeds.Length; s++)
                {
                    float d = math.distance(at, Seeds[s]) +
                              Jitter * VoxelMath.Hash01(p, Seed + (uint)s * 7919u);
                    if (d < best)
                    {
                        best = d;
                        cell = s;
                    }
                }

                Cells[i] = cell;
            }
        }
    }
}

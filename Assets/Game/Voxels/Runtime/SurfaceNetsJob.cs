using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A smooth surface over one chunk of a voxel grid — constrained elastic surface nets
    /// (Gibson, 1998), made for exactly this: smoothing a yes/no volume without losing it.
    ///
    /// Every cell between eight voxel centres where solid meets empty gets one vertex; every edge
    /// between a solid and an empty voxel becomes a quad joining the four cells round it. The
    /// vertices are then relaxed toward the average of their neighbours a few times, but never
    /// out of their own cell, so a 10 cm terrace on a slope melts into a ramp while the surface
    /// stays within half a voxel of the voxels — what you dig is still what you see.
    ///
    /// The cells round the chunk are worked out too (<see cref="MARGIN"/> of them), so the
    /// relaxed vertices on a border come out the same in both chunks and the seams close.
    /// Beyond the grid's sides and below it the voxels are taken as continuing as they are (the
    /// ground goes on), above it as empty: the ground gets a top and no walls.
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct SurfaceNetsJob : IJob
    {
        public const int ITERATIONS = 4;
        public const int MARGIN = ITERATIONS + 1;

        /// <summary>
        /// The voxels the job reads: the whole grid (<see cref="Offset"/> 0, <see cref="Dims"/> =
        /// <see cref="Size"/>) or a copy of just the box round the chunk (<see cref="CopyRegion"/>),
        /// so the job can run on while the grid is carved.
        /// </summary>
        [ReadOnly] public NativeArray<byte> Voxels;
        [ReadOnly] public NativeArray<Color32> Palette;
        /// <summary>The body's whole grid: what lies past its sides and top.</summary>
        public int3 Size;
        /// <summary>Grid coordinates of <see cref="Voxels"/>[0].</summary>
        public int3 Offset;
        /// <summary>Extent of <see cref="Voxels"/>.</summary>
        public int3 Dims;
        public int3 Min;
        public int3 Max;
        public float VoxelSize;
        public float3 Origin;

        /// <summary>A/B switch for measuring: false works every layer, as before.</summary>
        public static bool SkipUniformLayers = true;
        public bool SkipUniform;

        public NativeList<VoxelVertex> Vertices;
        public NativeList<int> Indices;

        private int3 _lo;
        private int3 _dims;
        /// <summary>Per sample layer from _lo.y up: 0 — all empty, 1 — all solid, 2 — mixed. A value, not a
        /// container: the job system wants every container field assigned before scheduling.</summary>
        private FixedList128Bytes<byte> _layers;

        /// <summary>
        /// The box of the grid a chunk's surface reads: the chunk, its margin and one more voxel, cut
        /// to the grid (the job clamps to the grid the same way when it samples).
        /// </summary>
        public static void RegionOf(int3 min, int3 max, int3 size, out int3 offset, out int3 dims)
        {
            int3 lo = math.max(0, min - 1 - MARGIN);
            int3 hi = math.min(size - 1, max + MARGIN);
            offset = lo;
            dims = hi - lo + 1;
        }

        public void Execute()
        {
            _lo = Min - 1 - MARGIN;
            _dims = Max + MARGIN - _lo;
            int cells = _dims.x * _dims.y * _dims.z;

            var vertexOf = new NativeArray<int>(cells, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < cells; i++)
                vertexOf[i] = -1;
            var positions = new NativeList<float3>(4096, Allocator.Temp);
            var cellOf = new NativeList<int3>(4096, Allocator.Temp);
            var materials = new NativeList<byte>(4096, Allocator.Temp);

            // Most of a ground chunk's height is solid earth or open air: layers where every
            // voxel is the same hold no surface, and are skipped below.
            _layers = default;
            // (A chunk too tall for the table works every layer.)
            SkipUniform &= _dims.y + 1 <= _layers.Capacity;
            if (SkipUniform)
                for (int ly = 0; ly <= _dims.y; ly++)
                    _layers.Add(LayerState(_lo.y + ly));

            Place(vertexOf, positions, cellOf, materials);
            Relax(vertexOf, positions, cellOf);

            var normals = new NativeArray<float3>(positions.Length, Allocator.Temp);
            var output = new NativeArray<int>(positions.Length, Allocator.Temp);
            for (int i = 0; i < output.Length; i++)
                output[i] = -1;

            // Normals from the quads one cell further out than the chunk's own, so a vertex on
            // the border is lit by its whole ring of faces, as in the chunk next door.
            Quads(Min - 1, Max + 1, vertexOf, positions, normals, output, materials, false);
            Quads(Min, Max, vertexOf, positions, normals, output, materials, true);

            for (int i = 0; i < output.Length; i++)
            {
                int o = output[i];
                if (o < 0)
                    continue;
                VoxelVertex v = Vertices[o];
                float3 n = normals[i];
                v.Normal = math.lengthsq(n) > 1e-12f ? math.normalize(n) : new float3(0f, 1f, 0f);
                Vertices[o] = v;
            }
        }

        private byte Sample(int3 p)
        {
            if (p.y >= Size.y)
                return 0;
            p = math.clamp(p, int3.zero, Size - 1) - Offset;
            return Voxels[p.x + Dims.x * (p.y + Dims.y * p.z)];
        }

        /// <summary>Whether a whole layer of samples across the worked box is empty (0), solid (1) or mixed (2).</summary>
        private byte LayerState(int y)
        {
            if (y >= Size.y)
                return 0;

            int3 lo = math.clamp(_lo, int3.zero, Size - 1);
            int3 hi = math.clamp(_lo + _dims, int3.zero, Size - 1);
            bool solid = false;
            bool empty = false;
            for (int z = lo.z; z <= hi.z; z++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                if (Sample(new int3(x, y, z)) != 0)
                    solid = true;
                else
                    empty = true;
                if (solid && empty)
                    return 2;
            }

            return solid ? (byte)1 : (byte)0;
        }

        /// <summary>True when sample layers y and y + 1 (grid coordinates) are the same all through: no surface between.</summary>
        private bool Uniform(int y, int y1)
        {
            if (!SkipUniform)
                return false;
            int a = y - _lo.y;
            int b = y1 - _lo.y;
            if (a < 0 || b < 0 || a > _dims.y || b > _dims.y)
                return false;
            byte la = _layers[a];
            return la != 2 && la == _layers[b];
        }

        private int CellIndex(int3 c)
        {
            int3 q = c - _lo;
            if (math.any(q < 0) || math.any(q >= _dims))
                return -1;
            return q.x + _dims.x * (q.y + _dims.y * q.z);
        }

        /// <summary>A vertex in every mixed cell, at the average of the crossings on its edges.</summary>
        private void Place(NativeArray<int> vertexOf, NativeList<float3> positions, NativeList<int3> cellOf,
            NativeList<byte> materials)
        {
            for (int z = 0; z < _dims.z; z++)
            for (int y = 0; y < _dims.y; y++)
            {
                if (Uniform(_lo.y + y, _lo.y + y + 1))
                    continue;

                for (int x = 0; x < _dims.x; x++)
                {
                int3 c = _lo + new int3(x, y, z);
                int index = x + _dims.x * (y + _dims.y * z);

                int mask = 0;
                byte top = 0;
                int topY = int.MinValue;
                for (int k = 0; k < 8; k++)
                {
                    int3 corner = c + new int3(k & 1, (k >> 1) & 1, (k >> 2) & 1);
                    byte m = Sample(corner);
                    if (m == 0)
                        continue;
                    mask |= 1 << k;
                    // The surface takes the colour of the highest solid corner: grass over earth.
                    if (corner.y > topY)
                    {
                        topY = corner.y;
                        top = m;
                    }
                }

                if (mask == 0 || mask == 255)
                    continue;

                float3 sum = float3.zero;
                int crossings = 0;
                for (int k = 0; k < 8; k++)
                for (int axis = 0; axis < 3; axis++)
                {
                    int other = k | (1 << axis);
                    if (other == k)
                        continue;
                    bool a = (mask & (1 << k)) != 0;
                    bool b = (mask & (1 << other)) != 0;
                    if (a == b)
                        continue;
                    float3 pa = new float3(k & 1, (k >> 1) & 1, (k >> 2) & 1);
                    float3 pb = new float3(other & 1, (other >> 1) & 1, (other >> 2) & 1);
                    sum += (pa + pb) * 0.5f;
                    crossings++;
                }

                vertexOf[index] = positions.Length;
                // In voxel-centre units: the centre of voxel p sits at p + 0.5.
                positions.Add((float3)c + 0.5f + sum / crossings);
                cellOf.Add(c);
                materials.Add(top);
                }
            }
        }

        /// <summary>
        /// A few rounds of: each vertex to the average of the vertices in the six cells beside
        /// it, kept inside its own cell.
        /// </summary>
        private void Relax(NativeArray<int> vertexOf, NativeList<float3> positions, NativeList<int3> cellOf)
        {
            var next = new NativeArray<float3>(positions.Length, Allocator.Temp);
            for (int round = 0; round < ITERATIONS; round++)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    int3 c = cellOf[i];
                    float3 sum = float3.zero;
                    int count = 0;
                    for (int d = 0; d < 6; d++)
                    {
                        int3 n = c;
                        n[d >> 1] += (d & 1) == 0 ? 1 : -1;
                        int cell = CellIndex(n);
                        if (cell < 0)
                            continue;
                        int j = vertexOf[cell];
                        if (j < 0)
                            continue;
                        sum += positions[j];
                        count++;
                    }

                    float3 lo = (float3)c + 0.5f;
                    next[i] = count > 0 ? math.clamp(sum / count, lo, lo + 1f) : positions[i];
                }

                for (int i = 0; i < positions.Length; i++)
                    positions[i] = next[i];
            }
        }

        /// <summary>
        /// One quad per edge between a solid and an empty voxel, for the edges that start in
        /// [from, to). With <paramref name="emit"/> the quads are written out; without, they only
        /// add their normals to their corners.
        /// </summary>
        private void Quads(int3 from, int3 to, NativeArray<int> vertexOf, NativeList<float3> positions,
            NativeArray<float3> normals, NativeArray<int> output, NativeList<byte> materials, bool emit)
        {
            for (int d = 0; d < 3; d++)
            {
                int u = (d + 1) % 3;
                int v = (d + 2) % 3;
                int3 eu = int3.zero;
                eu[u] = 1;
                int3 ev = int3.zero;
                ev[v] = 1;
                int3 ed = int3.zero;
                ed[d] = 1;

                for (int z = from.z; z < to.z; z++)
                for (int y = from.y; y < to.y; y++)
                {
                // Both ends of every edge in this row lie in layers that are the same all through.
                if (Uniform(y, y + ed.y))
                    continue;

                for (int x = from.x; x < to.x; x++)
                {
                    int3 p = new int3(x, y, z);
                    bool a = Sample(p) != 0;
                    bool b = Sample(p + ed) != 0;
                    if (a == b)
                        continue;

                    int c00 = Vertex(p - eu - ev, vertexOf);
                    int c10 = Vertex(p - ev, vertexOf);
                    int c11 = Vertex(p, vertexOf);
                    int c01 = Vertex(p - eu, vertexOf);
                    if (c00 < 0 || c10 < 0 || c11 < 0 || c01 < 0)
                        continue;

                    // Solid below the edge's end: the face looks along +d; else along -d.
                    if (!a)
                    {
                        (c10, c01) = (c01, c10);
                    }

                    if (!emit)
                    {
                        float3 n0 = math.cross(positions[c10] - positions[c00], positions[c11] - positions[c00]);
                        float3 n1 = math.cross(positions[c11] - positions[c00], positions[c01] - positions[c00]);
                        float3 n = n0 + n1;
                        normals[c00] += n;
                        normals[c10] += n;
                        normals[c11] += n;
                        normals[c01] += n;
                        continue;
                    }

                    int o00 = Emit(c00, positions, output, materials);
                    int o10 = Emit(c10, positions, output, materials);
                    int o11 = Emit(c11, positions, output, materials);
                    int o01 = Emit(c01, positions, output, materials);
                    Indices.Add(o00); Indices.Add(o10); Indices.Add(o11);
                    Indices.Add(o00); Indices.Add(o11); Indices.Add(o01);
                }
                }
            }
        }

        private int Vertex(int3 cell, NativeArray<int> vertexOf)
        {
            int index = CellIndex(cell);
            return index < 0 ? -1 : vertexOf[index];
        }

        private int Emit(int vertex, NativeList<float3> positions, NativeArray<int> output, NativeList<byte> materials)
        {
            if (output[vertex] >= 0)
                return output[vertex];

            output[vertex] = Vertices.Length;
            Vertices.Add(new VoxelVertex
            {
                Position = Origin + positions[vertex] * VoxelSize,
                Normal = new float3(0f, 1f, 0f),
                Color = Palette[materials[vertex]]
            });
            return output[vertex];
        }
    }

    /// <summary>
    /// Copies a box of a grid into a buffer of its own (see <see cref="SurfaceNetsJob.RegionOf"/>):
    /// a remesh that runs over several frames reads the copy while the grid is carved on.
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    public struct CopyRegionJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public int3 Size;
        public int3 Offset;
        public int3 Dims;
        public NativeArray<byte> Region;

        public void Execute()
        {
            for (int z = 0; z < Dims.z; z++)
            for (int y = 0; y < Dims.y; y++)
            {
                int from = Offset.x + Size.x * ((Offset.y + y) + Size.y * (Offset.z + z));
                int to = Dims.x * (y + Dims.y * z);
                NativeArray<byte>.Copy(Voxels, from, Region, to, Dims.x);
            }
        }
    }

    /// <summary>Cooks the physics meshes of smooth chunks on worker threads, before they are handed to their colliders.</summary>
    // Not Burst: Physics.BakeMesh is a managed entry point (thread-safe, so still parallel).
    public struct BakeMeshesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<EntityId> MeshIds;

        public void Execute(int index) => Physics.BakeMesh(MeshIds[index], false);
    }
}

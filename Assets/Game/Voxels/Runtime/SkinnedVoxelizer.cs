using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxels
{
    /// <summary>
    /// Turns a skinned body, in the pose it stands in right now, into voxels: skin outside, meat and
    /// veins of blood inside (FleshPalette), each voxel tagged with the part of the body whose bones
    /// are nearest. The grid lies in the renderer's axes (its position and rotation, no scale).
    /// </summary>
    /// <remarks>
    /// Filling is MeshVoxelizer's (a ray per column along Z, inside between pairs of crossings), but
    /// the triangles are first sorted into the columns their bounds cover: a creature has thousands
    /// of triangles, and testing each against every column would take ~100 ms.
    /// The baked pose is read in place (Mesh.AcquireReadOnlyMeshData) and turned into grid-space
    /// triangles in Burst: copying it out as managed arrays was half a megabyte of garbage a death,
    /// and the copy, element by element, a few milliseconds of the kill frame.
    /// </remarks>
    public static class SkinnedVoxelizer
    {
        public const byte NO_PART = 255;

        /// <summary>
        /// Most voxels along any side: 8 m at 5 cm, far over any body lying down. A pose past it is
        /// broken (a bone flung off to nowhere) and is not voxelized: its grid could run to hundreds
        /// of megabytes.
        /// </summary>
        public const int MAX_SIZE = 160;

        public struct Part
        {
            /// <summary>A bone reaches to each child that is in this list too; a bone with none is a point.</summary>
            public Transform[] Bones;

            /// <summary>0 — the whole part; +1 / −1 — only voxels on that side of the mid plane (a torso cut in halves).</summary>
            public int Side;
        }

        public struct Result : IDisposable
        {
            public int3 Size;
            public float3 Origin;
            public float VoxelSize;
            public Vector3 Position;
            public Quaternion Rotation;

            /// <summary>Palette index per voxel, 0 — empty.</summary>
            public NativeArray<byte> Voxels;

            /// <summary>Part per voxel, NO_PART — empty.</summary>
            public NativeArray<byte> Parts;

            public NativeArray<Color32> Palette;

            public void Dispose()
            {
                if (Voxels.IsCreated) Voxels.Dispose();
                if (Parts.IsCreated) Parts.Dispose();
                if (Palette.IsCreated) Palette.Dispose();
            }
        }

        private struct Segment
        {
            public float3 A;
            public float3 B;
            public int Part;
            public int Side;
        }

        private static Mesh s_baked;

        public static bool Voxelize(SkinnedMeshRenderer skin, IReadOnlyList<Part> parts, Plane midPlane, float voxelSize,
            in FleshPalette palette, uint seed, out Result result)
        {
            result = default;
            if (skin == null || skin.sharedMesh == null || parts == null || parts.Count == 0 ||
                parts.Count >= NO_PART || voxelSize <= 0f)
                return false;

            if (s_baked == null)
                s_baked = new Mesh { name = "SkinnedVoxelizer" };
            // With useScale the vertices carry the renderer's scale: the grid only takes its position and rotation.
            skin.BakeMesh(s_baked, true);

            using Mesh.MeshDataArray meshes = Mesh.AcquireReadOnlyMeshData(s_baked);
            int vertexCount = meshes[0].vertexCount;
            int indexCount = TriangleIndexCount(meshes[0]);
            if (vertexCount == 0 || indexCount < 3)
                return false;

            Transform space = skin.transform;
            Vector3 position = space.position;
            Quaternion rotation = space.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);

            var vertices = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var minMax = new NativeArray<float3>(2, Allocator.TempJob);
            new ReadVerticesJob { Meshes = meshes, Vertices = vertices, MinMax = minMax }.Run();
            float3 min = minMax[0];
            float3 max = minMax[1];
            minMax.Dispose();

            float3 origin = math.floor(min / voxelSize) * voxelSize - voxelSize;
            // Checked as floats: a vertex flung to nowhere would overflow the int.
            float3 span = math.ceil((max - origin) / voxelSize) + 1f;
            if (!math.all(math.isfinite(span)) || math.any(span > MAX_SIZE))
            {
                vertices.Dispose();
                Debug.LogWarning($"[SkinnedVoxelizer] {skin.name}: the pose spans {max - min} m, over " +
                                 $"{MAX_SIZE} voxels of {voxelSize} m a side; not voxelized.");
                return false;
            }

            int3 size = math.max((int3)span, 1);
            int columns = size.x * size.y;
            int cells = columns * size.z;

            float3 ToGrid(Vector3 world) => ((float3)(inverse * (world - position)) - origin) / voxelSize;

            var triangles = new NativeArray<float3>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<Segment> segments = BuildSegments(parts, ToGrid);
            float3 normal = (float3)(inverse * midPlane.normal);
            float3 onPlane = ToGrid(midPlane.ClosestPointOnPlane(position));

            var start = new NativeArray<int>(columns + 1, Allocator.TempJob);
            var items = new NativeList<int>(indexCount, Allocator.TempJob);
            var solid = new NativeArray<byte>(cells, Allocator.TempJob);
            var voxels = new NativeArray<byte>(cells, Allocator.Persistent);
            var partOf = new NativeArray<byte>(cells, Allocator.Persistent);

            JobHandle handle = new ExpandTrianglesJob
            {
                Meshes = meshes, Vertices = vertices, Origin = origin, VoxelSize = voxelSize, Triangles = triangles
            }.Schedule();
            handle = new BinTrianglesJob
            {
                Triangles = triangles, Columns = size.xy, Start = start, Items = items
            }.Schedule(handle);
            handle = new FillColumnsJob
            {
                Triangles = triangles, Start = start, Items = items.AsDeferredJobArray(), Size = size, Solid = solid
            }.Schedule(columns, 16, handle);
            handle = new ClassifyJob
            {
                Solid = solid,
                Segments = segments,
                Size = size,
                PlaneNormal = normal,
                PlaneDistance = -math.dot(normal, onPlane),
                Seed = seed,
                BloodShare = palette.BloodShare,
                Voxels = voxels,
                Parts = partOf
            }.Schedule(cells, 256, handle);
            handle.Complete();

            vertices.Dispose();
            triangles.Dispose();
            segments.Dispose();
            start.Dispose();
            items.Dispose();
            solid.Dispose();

            result = new Result
            {
                Size = size,
                Origin = origin,
                VoxelSize = voxelSize,
                Position = position,
                Rotation = rotation,
                Voxels = voxels,
                Parts = partOf,
                Palette = palette.BuildPalette(Allocator.Persistent)
            };
            return true;
        }

        private static NativeArray<Segment> BuildSegments(IReadOnlyList<Part> parts, Func<Vector3, float3> toGrid)
        {
            var list = new List<Segment>();
            for (int p = 0; p < parts.Count; p++)
            {
                Transform[] bones = parts[p].Bones;
                if (bones == null)
                    continue;

                var own = new HashSet<Transform>(bones);
                foreach (Transform bone in bones)
                {
                    if (bone == null)
                        continue;

                    float3 a = toGrid(bone.position);
                    bool reached = false;
                    foreach (Transform child in bone)
                    {
                        if (!own.Contains(child))
                            continue;
                        list.Add(new Segment { A = a, B = toGrid(child.position), Part = p, Side = parts[p].Side });
                        reached = true;
                    }

                    if (!reached)
                        list.Add(new Segment { A = a, B = a, Part = p, Side = parts[p].Side });
                }
            }

            var array = new NativeArray<Segment>(list.Count, Allocator.TempJob);
            for (int i = 0; i < list.Count; i++)
                array[i] = list[i];
            return array;
        }

        /// <summary>Indices in the mesh's triangle submeshes (a skin has no other kind, but it is not assumed).</summary>
        private static int TriangleIndexCount(Mesh.MeshData mesh)
        {
            int count = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                SubMeshDescriptor sub = mesh.GetSubMesh(s);
                if (sub.topology == MeshTopology.Triangles)
                    count += sub.indexCount;
            }

            return count;
        }

        /// <summary>The baked pose's vertices (the renderer's space), and their bounds: MinMax[0] min, MinMax[1] max.</summary>
        [BurstCompile]
        private struct ReadVerticesJob : IJob
        {
            [ReadOnly] public Mesh.MeshDataArray Meshes;
            public NativeArray<float3> Vertices;
            [WriteOnly] public NativeArray<float3> MinMax;

            public void Execute()
            {
                Meshes[0].GetVertices(Vertices.Reinterpret<Vector3>());

                float3 min = new float3(float.MaxValue);
                float3 max = new float3(float.MinValue);
                for (int i = 0; i < Vertices.Length; i++)
                {
                    min = math.min(min, Vertices[i]);
                    max = math.max(max, Vertices[i]);
                }

                MinMax[0] = min;
                MinMax[1] = max;
            }
        }

        /// <summary>
        /// Every triangle of the baked pose as its three corners in grid units, submesh after
        /// submesh, as Mesh.triangles lists them: 16- or 32-bit indices, each submesh's base vertex added.
        /// </summary>
        [BurstCompile]
        private struct ExpandTrianglesJob : IJob
        {
            [ReadOnly] public Mesh.MeshDataArray Meshes;
            [ReadOnly] public NativeArray<float3> Vertices;
            public float3 Origin;
            public float VoxelSize;
            [WriteOnly] public NativeArray<float3> Triangles;

            public void Execute()
            {
                Mesh.MeshData mesh = Meshes[0];
                bool wide = mesh.indexFormat == IndexFormat.UInt32;
                int written = 0;

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    SubMeshDescriptor sub = mesh.GetSubMesh(s);
                    if (sub.topology != MeshTopology.Triangles)
                        continue;

                    int end = sub.indexStart + sub.indexCount;
                    if (wide)
                    {
                        NativeArray<int> indices = mesh.GetIndexData<int>();
                        for (int i = sub.indexStart; i < end; i++)
                            Triangles[written++] = ToGrid(sub.baseVertex + indices[i]);
                    }
                    else
                    {
                        NativeArray<ushort> indices = mesh.GetIndexData<ushort>();
                        for (int i = sub.indexStart; i < end; i++)
                            Triangles[written++] = ToGrid(sub.baseVertex + indices[i]);
                    }
                }
            }

            private float3 ToGrid(int vertex) => (Vertices[vertex] - Origin) / VoxelSize;
        }

        /// <summary>Sorts each triangle into every column its XY bounds cover (counting pass, then filling pass).</summary>
        [BurstCompile]
        private struct BinTrianglesJob : IJob
        {
            [ReadOnly] public NativeArray<float3> Triangles;
            public int2 Columns;
            public NativeArray<int> Start;
            public NativeList<int> Items;

            public void Execute()
            {
                int columns = Columns.x * Columns.y;
                var counts = new NativeArray<int>(columns, Allocator.Temp);
                int triangles = Triangles.Length / 3;

                for (int t = 0; t < triangles; t++)
                {
                    Bounds(t, out int2 lo, out int2 hi);
                    for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                        counts[x + y * Columns.x]++;
                }

                int sum = 0;
                for (int c = 0; c < columns; c++)
                {
                    Start[c] = sum;
                    sum += counts[c];
                    counts[c] = 0;
                }

                Start[columns] = sum;
                Items.ResizeUninitialized(sum);

                for (int t = 0; t < triangles; t++)
                {
                    Bounds(t, out int2 lo, out int2 hi);
                    for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                    {
                        int c = x + y * Columns.x;
                        Items[Start[c] + counts[c]++] = t;
                    }
                }
            }

            private void Bounds(int t, out int2 lo, out int2 hi)
            {
                float2 a = Triangles[t * 3].xy;
                float2 b = Triangles[t * 3 + 1].xy;
                float2 c = Triangles[t * 3 + 2].xy;
                lo = math.max(0, (int2)math.floor(math.min(math.min(a, b), c)));
                hi = math.min(Columns - 1, (int2)math.floor(math.max(math.max(a, b), c)));
            }
        }

        [BurstCompile]
        private struct FillColumnsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Triangles;
            [ReadOnly] public NativeArray<int> Start;
            [ReadOnly] public NativeArray<int> Items;
            public int3 Size;

            // Each column writes only its own cells.
            [NativeDisableParallelForRestriction] public NativeArray<byte> Solid;

            public void Execute(int column)
            {
                int first = Start[column];
                int last = Start[column + 1];
                if (last - first < 2)
                    return;

                int x = column % Size.x;
                int y = column / Size.x;
                // Off-centre, as in MeshVoxelizer: never exactly along a shared edge.
                float2 p = new float2(x + 0.50031f, y + 0.49973f);
                var crossings = new NativeList<float>(16, Allocator.Temp);

                for (int k = first; k < last; k++)
                {
                    int t = Items[k] * 3;
                    if (MeshVoxelizer.CrossZ(p, Triangles[t], Triangles[t + 1], Triangles[t + 2], out float z))
                        crossings.Add(z);
                }

                crossings.Sort();
                for (int k = 0; k + 1 < crossings.Length; k += 2)
                {
                    int from = (int)math.ceil(crossings[k] - 0.5f);
                    int to = (int)math.floor(crossings[k + 1] - 0.5f);
                    // A claw thinner than a voxel still gets one.
                    if (to < from)
                        from = to = (int)math.floor((crossings[k] + crossings[k + 1]) * 0.5f);

                    from = math.max(from, 0);
                    to = math.min(to, Size.z - 1);
                    for (int z = from; z <= to; z++)
                        Solid[x + Size.x * (y + Size.y * z)] = 1;
                }
            }
        }

        [BurstCompile]
        private struct ClassifyJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Solid;
            [ReadOnly] public NativeArray<Segment> Segments;
            public int3 Size;
            public float3 PlaneNormal;
            public float PlaneDistance;
            public uint Seed;
            public float BloodShare;
            [WriteOnly] public NativeArray<byte> Voxels;
            [WriteOnly] public NativeArray<byte> Parts;

            public void Execute(int index)
            {
                if (Solid[index] == 0)
                {
                    Voxels[index] = 0;
                    Parts[index] = NO_PART;
                    return;
                }

                int3 cell = VoxelMath.Coord(index, Size);
                float3 centre = (float3)cell + 0.5f;
                int side = math.dot(PlaneNormal, centre) + PlaneDistance >= 0f ? 1 : -1;

                int part = 0;
                float best = float.MaxValue;
                for (int s = 0; s < Segments.Length; s++)
                {
                    Segment segment = Segments[s];
                    if (segment.Side != 0 && segment.Side != side)
                        continue;
                    float d = DistanceSq(centre, segment.A, segment.B);
                    if (d < best)
                    {
                        best = d;
                        part = segment.Part;
                    }
                }

                Parts[index] = (byte)part;

                float shade = VoxelMath.Hash01(cell, Seed);
                if (IsSurface(cell))
                    Voxels[index] = (byte)(FleshPalette.FIRST_SKIN + Slot(shade, MeshVoxelizer.VARIANTS));
                else if (VoxelMath.Hash01(cell, Seed + 101u) < BloodShare)
                    Voxels[index] = (byte)(FleshPalette.FIRST_BLOOD + Slot(shade, FleshPalette.BLOOD_SLOTS));
                else
                    Voxels[index] = (byte)(FleshPalette.FIRST_MEAT + Slot(shade, FleshPalette.MEAT_SLOTS));
            }

            /// <summary>One of <paramref name="slots"/> shades by a 0..1 hash (as MeshVoxelizer picks its variants).</summary>
            private static int Slot(float hash, int slots) => (int)(hash * slots) % slots;

            private bool IsSurface(int3 c) =>
                Empty(c + new int3(1, 0, 0)) || Empty(c - new int3(1, 0, 0)) ||
                Empty(c + new int3(0, 1, 0)) || Empty(c - new int3(0, 1, 0)) ||
                Empty(c + new int3(0, 0, 1)) || Empty(c - new int3(0, 0, 1));

            private bool Empty(int3 c) => !VoxelMath.Inside(c, Size) || Solid[VoxelMath.Index(c, Size)] == 0;

            private static float DistanceSq(float3 p, float3 a, float3 b)
            {
                float3 ab = b - a;
                float length = math.lengthsq(ab);
                float t = length > 1e-6f ? math.saturate(math.dot(p - a, ab) / length) : 0f;
                return math.distancesq(p, a + ab * t);
            }
        }
    }
}

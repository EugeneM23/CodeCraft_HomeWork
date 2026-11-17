using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Turns closed meshes into a voxel grid: for every column of voxel centres a ray goes along +Z,
    /// collects where it crosses the mesh's triangles, and fills the voxels between each pair of
    /// crossings (inside — outside — inside...). Each mesh is filled on its own, so overlapping
    /// primitives (a turret sunk into a hull) come out right. Runs in Burst; the tank takes a few
    /// milliseconds.
    /// </summary>
    public static class MeshVoxelizer
    {
        /// <summary>Shades of each colour in the palette, picked per voxel by a hash.</summary>
        public const int VARIANTS = 4;

        public struct Source
        {
            public Mesh Mesh;

            /// <summary>Mesh space to the grid's space.</summary>
            public Matrix4x4 ToSpace;

            public Color Color;
        }

        public struct Result
        {
            public int3 Size;
            public float3 Origin;
            public NativeArray<byte> Voxels;
            public NativeArray<Color32> Palette;
        }

        /// <param name="noise">How far each shade strays from the base colour, 0..0.3.</param>
        public static bool Voxelize(IReadOnlyList<Source> sources, float voxelSize, float noise, uint seed, out Result result)
        {
            result = default;
            if (sources.Count == 0)
                return false;

            // Bounds in grid space, one voxel of padding all round.
            var triangles = new List<Vector3[]>(sources.Count);
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;

            foreach (Source source in sources)
            {
                Vector3[] vertices = source.Mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = source.ToSpace.MultiplyPoint3x4(vertices[i]);
                    min = Vector3.Min(min, vertices[i]);
                    max = Vector3.Max(max, vertices[i]);
                }

                triangles.Add(vertices);
            }

            float3 origin = math.floor((float3)min / voxelSize) * voxelSize - voxelSize;
            int3 size = (int3)math.ceil(((float3)max - origin) / voxelSize) + 1;
            size = math.clamp(size, 1, 512);

            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            var bases = new List<Color32>();
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);

            for (int s = 0; s < sources.Count; s++)
            {
                Color32 color = sources[s].Color;
                int baseIndex = bases.IndexOf(color);
                if (baseIndex < 0)
                {
                    if (bases.Count >= 255 / VARIANTS)
                        baseIndex = 0;
                    else
                    {
                        baseIndex = bases.Count;
                        bases.Add(color);
                        FillShades(palette, 1 + baseIndex * VARIANTS, sources[s].Color, noise);
                    }
                }

                Vector3[] vertices = triangles[s];
                int[] indices = sources[s].Mesh.triangles;
                var tris = new NativeArray<float3>(indices.Length, Allocator.TempJob);
                for (int i = 0; i < indices.Length; i++)
                    tris[i] = ((float3)vertices[indices[i]] - origin) / voxelSize;

                new FillMeshJob
                {
                    Triangles = tris,
                    Voxels = voxels,
                    Size = size,
                    FirstMaterial = (byte)(1 + baseIndex * VARIANTS),
                    Seed = seed + (uint)s
                }.Run();

                tris.Dispose();
            }

            result = new Result { Size = size, Origin = origin, Voxels = voxels, Palette = palette };
            return true;
        }

        /// <summary>
        /// The first palette slot of <paramref name="color"/>'s shades as <see cref="Voxelize"/> laid them
        /// out with <paramref name="noise"/>, or -1 if the palette has no such colour.
        /// </summary>
        public static int FindShades(NativeArray<Color32> palette, Color color, float noise)
        {
            var probe = new NativeArray<Color32>(1 + VARIANTS, Allocator.Temp);
            FillShades(probe, 1, color, noise);
            Color32 first = probe[1];
            Color32 last = probe[VARIANTS];
            probe.Dispose();

            for (int i = 1; i + VARIANTS <= palette.Length; i += VARIANTS)
                if (Same(palette[i], first) && Same(palette[i + VARIANTS - 1], last))
                    return i;

            return -1;
        }

        /// <summary>
        /// Lays <paramref name="color"/>'s shades into the first free run of slots after the
        /// voxelizer's own; returns the run's first slot, or -1 if the palette is full.
        /// </summary>
        public static int AddShades(NativeArray<Color32> palette, Color color, float noise)
        {
            for (int i = 1; i + VARIANTS <= palette.Length; i += VARIANTS)
            {
                if (palette[i].a != 0)
                    continue;

                FillShades(palette, i, color, noise);
                return i;
            }

            return -1;
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        public static void FillShades(NativeArray<Color32> palette, int first, Color color, float noise, float wetness = 0f)
        {
            for (int v = 0; v < VARIANTS; v++)
            {
                float shade = 1f + noise * (v / (float)(VARIANTS - 1) * 2f - 1f);
                palette[first + v] = WithWetness(ToVertexColor(color * shade), wetness);
            }
        }

        /// <summary>
        /// Wetness rides in the colour's alpha, which the Game/Voxel shader reads as dryness:
        /// 255 is dry, lower is wetter (darker and glossy). Never 0 — that marks a free palette slot.
        /// </summary>
        public static Color32 WithWetness(Color32 color, float wetness)
        {
            color.a = (byte)Mathf.Clamp(Mathf.RoundToInt(255f - Mathf.Clamp01(wetness) * 254f), 1, 255);
            return color;
        }

        /// <summary>
        /// Vertex colours reach the shader as they are, without the sRGB → linear conversion
        /// material colours get. In a linear project they are converted here, so a voxel matches
        /// the material it was made from.
        /// </summary>
        public static Color32 ToVertexColor(Color color)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            c.a = 1f;
            return c;
        }

        [BurstCompile]
        private struct FillMeshJob : IJob
        {
            [ReadOnly] public NativeArray<float3> Triangles;
            public NativeArray<byte> Voxels;
            public int3 Size;
            public byte FirstMaterial;
            public uint Seed;

            public void Execute()
            {
                float2 lo = new float2(float.MaxValue);
                float2 hi = new float2(float.MinValue);
                for (int i = 0; i < Triangles.Length; i++)
                {
                    lo = math.min(lo, Triangles[i].xy);
                    hi = math.max(hi, Triangles[i].xy);
                }

                int2 c0 = math.max(0, (int2)math.floor(lo));
                int2 c1 = math.min(Size.xy - 1, (int2)math.floor(hi));
                var crossings = new NativeList<float>(32, Allocator.Temp);

                for (int y = c0.y; y <= c1.y; y++)
                for (int x = c0.x; x <= c1.x; x++)
                {
                    // Off-centre on purpose, so a ray never runs exactly along a shared edge
                    // and counts the same crossing twice.
                    float2 p = new float2(x + 0.50031f, y + 0.49973f);
                    crossings.Clear();

                    for (int t = 0; t + 2 < Triangles.Length; t += 3)
                        if (CrossZ(p, Triangles[t], Triangles[t + 1], Triangles[t + 2], out float z))
                            crossings.Add(z);

                    if (crossings.Length < 2)
                        continue;

                    crossings.Sort();
                    for (int k = 0; k + 1 < crossings.Length; k += 2)
                        FillSpan(x, y, crossings[k], crossings[k + 1]);
                }
            }

            private void FillSpan(int x, int y, float z0, float z1)
            {
                int first = (int)math.ceil(z0 - 0.5f);
                int last = (int)math.floor(z1 - 0.5f);

                // A plate thinner than a voxel still gets one voxel.
                if (last < first)
                    first = last = (int)math.floor((z0 + z1) * 0.5f);

                first = math.max(first, 0);
                last = math.min(last, Size.z - 1);

                for (int z = first; z <= last; z++)
                {
                    int3 cell = new int3(x, y, z);
                    int variant = (int)(VoxelMath.Hash01(cell, Seed) * VARIANTS) % VARIANTS;
                    Voxels[VoxelMath.Index(cell, Size)] = (byte)(FirstMaterial + variant);
                }
            }
        }

        /// <summary>Where the ray along Z through <paramref name="p"/> crosses a triangle, if it does (shared with SkinnedVoxelizer).</summary>
        internal static bool CrossZ(float2 p, float3 a, float3 b, float3 c, out float z)
        {
            z = 0f;
            float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (math.abs(d) < 1e-9f)
                return false;

            float w1 = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / d;
            float w2 = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / d;
            float w3 = 1f - w1 - w2;
            if (w1 < 0f || w2 < 0f || w3 < 0f)
                return false;

            z = w1 * a.z + w2 * b.z + w3 * c.z;
            return true;
        }
    }
}

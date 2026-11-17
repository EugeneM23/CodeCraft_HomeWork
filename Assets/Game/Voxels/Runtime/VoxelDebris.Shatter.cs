using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxels
{
    public sealed partial class VoxelDebris
    {
        /// <summary>
        /// A clump that smacked into the ground breaks the way a voxel body does, by Voronoi: two
        /// or three seed cubes are picked at random, every cube joins the nearest (with a little
        /// jitter, so the break is ragged) and each group flies on as a smaller clump — a hop and
        /// a short slide apart. A clump of three or fewer falls into single crumbs. The smaller
        /// clumps can break again on a hard enough landing.
        /// </summary>
        [BurstCompile]
        private struct ShatterJob : IJob
        {
            private const int MAX_PARTS = 3;

            public NativeList<Cluster> Clusters;
            public NativeList<Cube> Cubes;
            public int Capacity;
            public uint Seed;

            public void Execute()
            {
                int original = Clusters.Length;
                bool any = false;
                for (int i = 0; i < original; i++)
                    any |= Clusters[i].Shatter != 0;
                if (!any)
                    return;

                var random = new Unity.Mathematics.Random(Seed * 747796405u + 1u);
                var parts = new NativeArray<int>(original, Allocator.Temp);
                var firstChild = new NativeArray<int>(original, Allocator.Temp);
                var seen = new NativeArray<int>(original, Allocator.Temp);
                var seeds = new NativeArray<float3>(original * MAX_PARTS, Allocator.Temp);

                // How many parts each breaking clump makes, and their (still empty) clumps.
                for (int i = 0; i < original; i++)
                {
                    Cluster parent = Clusters[i];
                    if (parent.Shatter == 0)
                        continue;

                    int k = parent.Count <= 3 ? parent.Count : parent.Count >= 10 ? MAX_PARTS : 2;
                    if (Clusters.Length + k > Capacity)
                    {
                        parent.Shatter = 0;
                        Clusters[i] = parent;
                        continue;
                    }

                    parts[i] = k;
                    firstChild[i] = Clusters.Length;
                    for (int j = 0; j < k; j++)
                    {
                        Clusters.Add(new Cluster
                        {
                            Rotation = parent.Rotation,
                            Life = parent.Life + random.NextFloat(-0.8f, 0.8f),
                            Flags = parent.Flags & KEEP,
                            // A break does not start the clock over: the parts lie when the clump would have.
                            Flight = parent.Flight
                        });
                    }
                }

                // Seeds: k cubes of each breaking clump, picked at random (reservoir sampling).
                for (int c = 0; c < Cubes.Length; c++)
                {
                    Cube cube = Cubes[c];
                    if (cube.Cluster >= original || Clusters[cube.Cluster].Shatter == 0)
                        continue;

                    Cluster parent = Clusters[cube.Cluster];
                    float3 world = parent.Position + math.mul(parent.Rotation, cube.Offset);
                    int k = parts[cube.Cluster];
                    int n = seen[cube.Cluster]++;
                    int slot = n < k ? n : random.NextInt(0, n + 1);
                    if (slot < k)
                        seeds[cube.Cluster * MAX_PARTS + slot] = world;
                }

                // Every cube to its nearest seed; the cube's offset holds its world position for now.
                for (int c = 0; c < Cubes.Length; c++)
                {
                    Cube cube = Cubes[c];
                    if (cube.Cluster >= original || Clusters[cube.Cluster].Shatter == 0)
                        continue;

                    Cluster parent = Clusters[cube.Cluster];
                    float3 world = parent.Position + math.mul(parent.Rotation, cube.Offset);
                    int k = parts[cube.Cluster];
                    int best = 0;
                    float bestDistance = float.MaxValue;
                    for (int j = 0; j < k; j++)
                    {
                        float d = math.distance(world, seeds[cube.Cluster * MAX_PARTS + j]) +
                                  random.NextFloat(0f, cube.Size * 0.8f);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = j;
                        }
                    }

                    int child = firstChild[cube.Cluster] + best;
                    Cluster part = Clusters[child];
                    part.Position += world;
                    part.Count++;
                    Clusters[child] = part;

                    cube.Cluster = child;
                    cube.Offset = world;
                    Cubes[c] = cube;
                }

                // Each part: its centre, and a hop and a slide away from where the clump hit.
                for (int i = 0; i < original; i++)
                {
                    Cluster parent = Clusters[i];
                    if (parent.Shatter == 0)
                        continue;

                    for (int j = 0; j < parts[i]; j++)
                    {
                        int index = firstChild[i] + j;
                        Cluster part = Clusters[index];
                        if (part.Count == 0)
                        {
                            part.Life = 0f;
                            Clusters[index] = part;
                            continue;
                        }

                        part.Position /= part.Count;
                        float3 away = part.Position - parent.Position;
                        away.y = 0f;
                        float3 slide = math.lengthsq(away) > 1e-6f ? math.normalize(away) : random.NextFloat3Direction() * new float3(1f, 0f, 1f);
                        float hop = part.Count == 1 ? random.NextFloat(0.4f, 1.2f) : random.NextFloat(0.3f, 0.9f);
                        part.Velocity = parent.Velocity * 0.35f + slide * random.NextFloat(0.3f, 1.1f) + new float3(0f, hop, 0f);
                        part.Spin = random.NextFloat3Direction() * random.NextFloat(2f, part.Count == 1 ? 9f : 5f);
                        Clusters[index] = part;
                    }

                    parent.Shatter = 0;
                    parent.Life = 0f;
                    Clusters[i] = parent;
                }

                // Offsets back into each part's own space, and its size for the ground checks.
                for (int c = 0; c < Cubes.Length; c++)
                {
                    Cube cube = Cubes[c];
                    if (cube.Cluster < original)
                        continue;

                    Cluster part = Clusters[cube.Cluster];
                    float3 offset = math.mul(math.inverse(part.Rotation), cube.Offset - part.Position);
                    cube.Offset = offset;
                    Cubes[c] = cube;

                    part.Radius = math.max(part.Radius, math.length(offset) + cube.Size * 0.5f);
                    Clusters[cube.Cluster] = part;
                }
            }
        }
    }
}

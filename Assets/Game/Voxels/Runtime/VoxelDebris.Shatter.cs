using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxels
{
    public sealed partial class VoxelDebris
    {
        /// <summary>Most pieces one clump breaks into on one landing.</summary>
        private const int MAX_PARTS = 8;
        /// <summary>A clump of fewer cubes does not break into pieces: it falls into single crumbs.</summary>
        private const int MIN_PIECE = 6;
        /// <summary>
        /// About this share of a breaking clump, on the side that hit, falls off as single crumbs
        /// (what CRUMB_DEPTH comes to).
        /// </summary>
        private const float CRUMB_SHARE = 0.18f;
        /// <summary>
        /// A cube is a crumb when it lies deeper than this share of the clump's reach (from its centre,
        /// towards the side that hit), less a jitter of up to a cube: the cap past half the radius is
        /// about 16 % of a ball, and the jitter brings it to about CRUMB_SHARE.
        /// </summary>
        private const float CRUMB_DEPTH = 0.5f;
        /// <summary>Impact speed (m/s) at which a clump breaks into as many pieces as its size allows.</summary>
        private const float FULL_IMPACT = 15f;
        /// <summary>A break harder than this (m/s) knocks out a puff of dust.</summary>
        private const float DUST_SPEED = 6f;
        /// <summary>Dust falls at 0.3 g: a share of the debris gravity, which is GRAVITY_SCALE times g.</summary>
        private const float DUST_GRAVITY = 0.3f / GRAVITY_SCALE;
        /// <summary>Most dust cubes one break knocks out.</summary>
        private const int MAX_DUST = 12;

        /// <summary>
        /// Breaks after which a piece no longer breaks: a thrown clump (0) breaks into pieces, a big
        /// piece (1) once more, theirs (2) land whole with the dull bounce. Without it every landing
        /// after a hop breaks again until all is crumbs; with it pieces stay pieces and the crumbs
        /// stay about a quarter of the clump.
        /// </summary>
        private const int MAX_GENERATION = 2;
        /// <summary>Smallest piece of a break (generation 1) that breaks again.</summary>
        private const int MIN_REBREAK = MIN_PIECE * 2;
        /// <summary>Most pieces a piece breaks into on its second landing.</summary>
        private const int MAX_REBREAK_PARTS = 3;

        /// <summary>Whether a clump may break on a hard landing at all (by its generation and size).</summary>
        private static bool CanBreak(in Cluster cluster) =>
            cluster.Generation == 0 || (cluster.Generation < MAX_GENERATION && cluster.Count >= MIN_REBREAK);

        /// <summary>
        /// How many pieces a clump of <paramref name="cubes"/> breaks into when it hits at
        /// <paramref name="impactSpeed"/> m/s: more for big clumps and hard hits, 2 at least, MAX_PARTS at most.
        /// </summary>
        public static int PartsFor(int cubes, float impactSpeed)
        {
            float strength = math.saturate(impactSpeed / FULL_IMPACT);
            return math.clamp((int)math.round(math.sqrt(cubes) * strength * 0.45f), 2, MAX_PARTS);
        }

        /// <summary>
        /// A clump that smacked into the ground breaks in a cascade. The cubes on the side that hit
        /// fall off as single crumbs, fanning out along the ground; the rest breaks by Voronoi into
        /// <see cref="PartsFor"/> pieces (seed cubes picked at random, every cube to the nearest,
        /// with a little jitter so the break is ragged), which hop up and apart so they land again
        /// and break once more. A clump under MIN_PIECE cubes falls into crumbs only. A hard hit
        /// also knocks out a puff of dust: a few new small cubes that drift and shrink away.
        /// New clusters go to the end of the list; the parent is left with no life for the compact pass.
        /// </summary>
        [BurstCompile]
        private struct ShatterJob : IJob
        {
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

                int cubeCount = Cubes.Length;
                var random = new Unity.Mathematics.Random(Seed * 747796405u + 1u);
                var parts = new NativeArray<int>(original, Allocator.Temp);
                var firstChild = new NativeArray<int>(original, Allocator.Temp);
                var seen = new NativeArray<int>(original, Allocator.Temp);
                var crumbs = new NativeArray<int>(original, Allocator.Temp);
                var nextCrumb = new NativeArray<int>(original, Allocator.Temp);
                var centers = new NativeArray<float3>(original, Allocator.Temp);
                var extents = new NativeArray<float>(original, Allocator.Temp);
                var seeds = new NativeArray<float3>(original * MAX_PARTS, Allocator.Temp);
                var dustColors = new NativeArray<float4>(original * MAX_DUST, Allocator.Temp);
                var crumb = new NativeArray<byte>(cubeCount, Allocator.Temp);

                // The clump's real centre (cubes need not sit around its origin), and a few of its
                // colours at random for the dust (reservoir sampling).
                for (int c = 0; c < cubeCount; c++)
                {
                    Cube cube = Cubes[c];
                    if (!Breaking(cube.Cluster, original))
                        continue;

                    Cluster parent = Clusters[cube.Cluster];
                    centers[cube.Cluster] += parent.Position + math.mul(parent.Rotation, cube.Offset);
                    int n = seen[cube.Cluster]++;
                    int slot = n < MAX_DUST ? n : random.NextInt(0, n + 1);
                    if (slot < MAX_DUST)
                        dustColors[cube.Cluster * MAX_DUST + slot] = cube.Color;
                }

                for (int i = 0; i < original; i++)
                {
                    if (seen[i] > 0)
                        centers[i] /= seen[i];
                }

                // How far the clump reaches from that centre.
                for (int c = 0; c < cubeCount; c++)
                {
                    Cube cube = Cubes[c];
                    if (!Breaking(cube.Cluster, original))
                        continue;

                    Cluster parent = Clusters[cube.Cluster];
                    float3 world = parent.Position + math.mul(parent.Rotation, cube.Offset);
                    extents[cube.Cluster] = math.max(extents[cube.Cluster], math.distance(world, centers[cube.Cluster]) + cube.Size * 0.5f);
                }

                // How many pieces; which cubes are crumbs: those deep on the side that hit, the
                // edge torn by a jitter of up to a cube (about CRUMB_SHARE of the clump).
                for (int i = 0; i < original; i++)
                {
                    Cluster parent = Clusters[i];
                    if (parent.Shatter == 0)
                        continue;
                    // A thrown clump under MIN_PIECE falls into crumbs; a piece breaking again makes 2-3.
                    parts[i] = parent.Generation > 0 ? math.min(PartsFor(parent.Count, parent.Impact), MAX_REBREAK_PARTS)
                        : parent.Count < MIN_PIECE ? 0 : PartsFor(parent.Count, parent.Impact);
                }

                for (int c = 0; c < cubeCount; c++)
                {
                    Cube cube = Cubes[c];
                    if (!Breaking(cube.Cluster, original))
                        continue;

                    int p = cube.Cluster;
                    Cluster parent = Clusters[p];
                    float3 world = parent.Position + math.mul(parent.Rotation, cube.Offset);
                    bool isCrumb = parts[p] == 0 ||
                                   math.dot(world - centers[p], -parent.HitNormal) > extents[p] * CRUMB_DEPTH - random.NextFloat(0f, cube.Size);
                    if (isCrumb)
                    {
                        crumb[c] = 1;
                        crumbs[p]++;
                    }
                }

                // The new clusters: k pieces, then a crumb each, then the dust - as far as there is room.
                for (int i = 0; i < original; i++)
                {
                    Cluster parent = Clusters[i];
                    if (parent.Shatter == 0)
                        continue;

                    int k = parts[i];
                    if (Clusters.Length + k + crumbs[i] > Capacity)
                    {
                        // No room for crumbs: the side that hit stays with the pieces.
                        crumbs[i] = 0;
                        if (k == 0 || Clusters.Length + k > Capacity)
                        {
                            parent.Shatter = 0;
                            parts[i] = 0;
                            Clusters[i] = parent;
                            continue;
                        }
                    }

                    firstChild[i] = Clusters.Length;
                    for (int j = 0; j < k; j++)
                    {
                        Clusters.Add(new Cluster
                        {
                            Rotation = parent.Rotation,
                            Life = parent.Life + random.NextFloat(-0.8f, 0.8f),
                            Flags = parent.Flags & KEEP,
                            // A break does not start the clock over: the parts lie when the clump would have.
                            Flight = parent.Flight,
                            Generation = parent.Generation + 1,
                            HitPoint = parent.HitPoint,
                            HitNormal = parent.HitNormal
                        });
                    }

                    nextCrumb[i] = Clusters.Length;
                    for (int j = 0; j < crumbs[i]; j++)
                    {
                        Clusters.Add(new Cluster
                        {
                            Rotation = parent.Rotation,
                            Life = parent.Life,
                            Flags = parent.Flags & KEEP,
                            Flight = parent.Flight,
                            HitPoint = parent.HitPoint,
                            HitNormal = parent.HitNormal
                        });
                    }

                    if (parent.Impact > DUST_SPEED)
                        Dust(parent, i, dustColors, math.min(seen[i], MAX_DUST), ref random);
                }

                // Seeds: k cubes of each breaking clump, picked at random among those not crumbs.
                for (int i = 0; i < original; i++)
                    seen[i] = 0;
                for (int c = 0; c < cubeCount; c++)
                {
                    Cube cube = Cubes[c];
                    int p = cube.Cluster;
                    if (!Breaking(p, original) || parts[p] == 0 || (crumb[c] != 0 && crumbs[p] > 0))
                        continue;

                    Cluster parent = Clusters[p];
                    float3 world = parent.Position + math.mul(parent.Rotation, cube.Offset);
                    int k = parts[p];
                    int n = seen[p]++;
                    int slot = n < k ? n : random.NextInt(0, n + 1);
                    if (slot < k)
                        seeds[p * MAX_PARTS + slot] = world;
                }

                // Every cube to a crumb of its own or to its nearest seed; the cube's offset holds
                // its world position for now.
                for (int c = 0; c < cubeCount; c++)
                {
                    Cube cube = Cubes[c];
                    int p = cube.Cluster;
                    if (!Breaking(p, original))
                        continue;

                    Cluster parent = Clusters[p];
                    float3 world = parent.Position + math.mul(parent.Rotation, cube.Offset);

                    if (crumb[c] != 0 && crumbs[p] > 0)
                    {
                        int index = nextCrumb[p]++;
                        Cluster single = Clusters[index];
                        float3 n = parent.HitNormal;
                        float3 fan = world - parent.HitPoint;
                        fan -= math.dot(fan, n) * n;
                        if (math.lengthsq(fan) < 1e-6f)
                        {
                            fan = random.NextFloat3Direction();
                            fan -= math.dot(fan, n) * n;
                        }
                        fan = math.lengthsq(fan) > 1e-6f ? math.normalize(fan) : new float3(1f, 0f, 0f);

                        single.Position = world;
                        single.Count = 1;
                        single.Velocity = fan * random.NextFloat(1f, 3f) + n * random.NextFloat(0.5f, 1.5f);
                        single.Spin = random.NextFloat3Direction() * random.NextFloat(6f, 12f);
                        Clusters[index] = single;

                        cube.Size *= random.NextFloat(0.75f, 0.95f);
                        cube.Cluster = index;
                        cube.Offset = world;
                        Cubes[c] = cube;
                        continue;
                    }

                    int k = math.min(parts[p], seen[p]);
                    int best = 0;
                    float bestDistance = float.MaxValue;
                    for (int j = 0; j < k; j++)
                    {
                        float d = math.distance(world, seeds[p * MAX_PARTS + j]) +
                                  random.NextFloat(0f, cube.Size * 0.8f);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = j;
                        }
                    }

                    int child = firstChild[p] + best;
                    Cluster part = Clusters[child];
                    part.Position += world;
                    part.Count++;
                    Clusters[child] = part;

                    cube.Cluster = child;
                    cube.Offset = world;
                    Cubes[c] = cube;
                }

                // Each piece: its centre, and a hop up and a slide away from the clump's centre,
                // high enough to land hard again and break once more.
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
                        float3 away = part.Position - centers[i];
                        away.y = 0f;
                        float3 slide = math.lengthsq(away) > 1e-6f ? math.normalize(away) : random.NextFloat3Direction() * new float3(1f, 0f, 1f);
                        part.Velocity = parent.Velocity * 0.35f + slide * random.NextFloat(0.5f, 2f) +
                                        new float3(0f, random.NextFloat(1.5f, 3f), 0f);
                        part.Spin = random.NextFloat3Direction() * random.NextFloat(2f, 5f);
                        Clusters[index] = part;
                    }

                    parent.Shatter = 0;
                    parent.Life = 0f;
                    Clusters[i] = parent;
                }

                // Offsets back into each new cluster's own space, and its size for the ground checks.
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

                // A clump lands with its centre only 0.6 of its radius above the ground, so its
                // lowest cubes are in it. A crumb or piece whose centre starts under the surface it
                // hit would never see that surface again and fall out of the world: lift it out.
                for (int i = original; i < Clusters.Length; i++)
                {
                    Cluster child = Clusters[i];
                    if (child.Count == 0 || math.lengthsq(child.HitNormal) < 0.5f)
                        continue;

                    float above = math.dot(child.Position - child.HitPoint, child.HitNormal);
                    float least = child.Count == 1 ? child.Radius : 0.02f;
                    if (above < least)
                    {
                        child.Position += child.HitNormal * (least - above);
                        Clusters[i] = child;
                    }
                }
            }

            private bool Breaking(int cluster, int original) => cluster < original && Clusters[cluster].Shatter != 0;

            /// <summary>
            /// A puff of dust where the clump hit: 6-12 new small cubes of its colours, a shade
            /// lighter, flying up and out; each its own cluster (offset kept as the world position
            /// until the last pass, like the rest).
            /// </summary>
            private void Dust(in Cluster parent, int p, NativeArray<float4> colors, int colorCount, ref Unity.Mathematics.Random random)
            {
                if (colorCount == 0)
                    return;

                int count = random.NextInt(6, MAX_DUST + 1);
                float3 at = parent.HitPoint + parent.HitNormal * 0.05f;
                for (int j = 0; j < count; j++)
                {
                    if (Clusters.Length >= Capacity || Cubes.Length >= Capacity)
                        return;

                    float3 direction = random.NextFloat3Direction();
                    direction.y = math.abs(direction.y);
                    Clusters.Add(new Cluster
                    {
                        Position = at,
                        Velocity = direction * random.NextFloat(1f, 2.5f),
                        Rotation = random.NextQuaternionRotation(),
                        Spin = random.NextFloat3Direction() * random.NextFloat(2f, 6f),
                        Life = random.NextFloat(0.8f, 1.4f),
                        Count = 1,
                        Flags = DUST
                    });

                    float4 color = colors[p * MAX_DUST + random.NextInt(0, colorCount)];
                    color.xyz = math.lerp(color.xyz, new float3(1f), 0.2f);
                    Cubes.Add(new Cube
                    {
                        Cluster = Clusters.Length - 1,
                        Offset = at,
                        Size = random.NextFloat(0.04f, 0.06f),
                        Color = color
                    });
                }
            }
        }
    }
}

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxels
{
    /// <summary>
    /// The "pure DOTS cubes" tier: loose voxel cubes that are not GameObjects or Rigidbodies.
    /// Cubes fly in clusters — a clump of a few voxels that keeps its shape, turns as one and
    /// smacks into the ground as one, then falls apart into single cubes where it hit. A single
    /// crumb is a cluster of one.
    ///
    /// Each frame: one RaycastCommand per cluster (PhysX on worker threads) finds what it will
    /// hit, a parallel Burst job moves and lands the clusters, another breaks the ones that hit
    /// hard, a last one compacts the dead and writes one matrix per cube; GPU instancing draws
    /// them, 1023 per call.
    ///
    /// Kept clumps and drops never age: once still (or after MAX_KEPT_FLIGHT, wherever they are),
    /// their cubes move to a lying store that is only drawn (no raycasts), up to LYING_CAPACITY.
    /// </summary>
    public sealed class VoxelDebris : IDisposable
    {
        private const int BATCH = 1023;
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");

        /// <summary>Debris falls this many times faster than g: heavy clods, low arcs, a thud.</summary>
        public const float GRAVITY_SCALE = 2.2f;

        /// <summary>Impact speed (m/s) at which a clump breaks into single cubes.</summary>
        private const float SHATTER_SPEED = 2.2f;

        /// <summary>Lying cubes kept at most; past this the oldest go, or the frame would grow all game.</summary>
        public const int LYING_CAPACITY = 60000;

        /// <summary>How long (s) a kept clump lies still before its cubes join the lying ones.</summary>
        private const float SETTLE_TIME = 0.5f;

        /// <summary>A landed drop is a tile: this much wider and this much flatter than its cube.</summary>
        private const float SPLAT_WIDTH = 1.8f;
        private const float SPLAT_HEIGHT = 0.3f;

        /// <summary>Below this a cube has fallen out of the world, kept or not.</summary>
        private const float FELL_OUT_Y = -60f;

        /// <summary>
        /// Longest (s) a kept clump is simulated: one that never comes to rest (sliding down a
        /// steep face, rocking on a moving piece) then lies where it is, or it would ray-cast all game.
        /// </summary>
        public const float MAX_KEPT_FLIGHT = 20f;

        /// <summary>Never ages; once still, its cubes lie for good.</summary>
        private const int KEEP = 1;
        /// <summary>A drop: spreads into a tile where it first touches, and lies at once.</summary>
        private const int SPLAT = 2;
        /// <summary>A drop that has landed: drawn as a tile.</summary>
        private const int LAID = 4;

        /// <summary>Marks a clump in the compact pass whose cubes move to the lying store.</summary>
        private const int SETTLED = -2;

        private struct Cluster
        {
            public float3 Position;
            public float3 Velocity;
            public quaternion Rotation;
            public float3 Spin;
            public float Radius;
            public float Life;
            public int Count;
            public int Resting;
            public int Shatter;
            public int Flags;
            /// <summary>How long a kept clump has rested, s.</summary>
            public float Still;
            /// <summary>How long a kept clump has been simulated, s: past MAX_KEPT_FLIGHT it lies where it is.</summary>
            public float Flight;
        }

        private struct Cube
        {
            public int Cluster;
            public float3 Offset;
            public float Size;
            public float4 Color;
        }

        private readonly int _capacity;
        private readonly Material _material;
        private readonly Mesh _mesh;
        private readonly MaterialPropertyBlock _block = new();
        private readonly Matrix4x4[] _batchMatrices = new Matrix4x4[BATCH];
        private readonly Vector4[] _batchColors = new Vector4[BATCH];

        private NativeList<Cluster> _clusters;
        private NativeList<Cube> _cubes;
        private NativeArray<RaycastCommand> _commands;
        private NativeArray<RaycastHit> _hits;
        private NativeArray<float4x4> _matrices;
        private NativeArray<float4> _colors;
        private NativeReference<int> _alive;
        private NativeArray<float4x4> _lyingMatrices;
        private NativeArray<float4> _lyingColors;
        /// <summary>x - the slot the next lying cube goes to; y - how many slots are filled.</summary>
        private NativeReference<int2> _lying;
        private uint _seed = 1;

        /// <summary>The frame's step, still running on workers (see <see cref="Complete"/>).</summary>
        private JobHandle _step;
        private bool _stepping;

        /// <summary>What debris lands on: everything but bullets, debris and Ignore Raycast.</summary>
        public int CollisionMask { get; set; } = ~((1 << 2) | (1 << 12) | (1 << 15));

        public int Count
        {
            get
            {
                Complete();
                return _cubes.IsCreated ? _cubes.Length : 0;
            }
        }

        /// <summary>Cubes lying for good (kept clumps that came to rest, landed drops).</summary>
        public int LyingCount
        {
            get
            {
                Complete();
                return _lying.IsCreated ? _lying.Value.y : 0;
            }
        }

        /// <summary>One lying cube, by its slot (0 ... LyingCount - 1): for tests and debugging.</summary>
        public bool TryGetLying(int slot, out Matrix4x4 matrix, out Color color)
        {
            Complete();
            matrix = default;
            color = default;
            if (!_lying.IsCreated || slot < 0 || slot >= _lying.Value.y)
                return false;

            matrix = _lyingMatrices[slot];
            float4 c = _lyingColors[slot];
            color = new Color(c.x, c.y, c.z, c.w);
            return true;
        }

        /// <summary>
        /// What a drop lands on: what any debris lands on, but what moves — loose voxel pieces, and
        /// the bodies that walk or drive (<see cref="MovingLayers"/>). A tile laid on any of them
        /// would hang in the air once it moved off. Blood on the tank is put on its voxel skin instead.
        /// </summary>
        public int DropMask => CollisionMask & ~(1 << VoxelWorld.PieceLayer) & ~MovingLayers;

        /// <summary>Layers of bodies that walk or drive about: Enemy (8) and Character (9), the tank among them.</summary>
        public int MovingLayers { get; set; } = (1 << 8) | (1 << 9);

        /// <summary>The clump begun this frame never ages: once it has lain still a moment its cubes lie for good.</summary>
        public void SetKeep(int cluster)
        {
            Complete();
            if (cluster < 0 || cluster >= _clusters.Length)
                return;
            Cluster c = _clusters[cluster];
            c.Flags |= KEEP;
            c.Life = float.PositiveInfinity;
            _clusters[cluster] = c;
        }

        /// <summary>A drop of blood: one cube that flies, spreads into a tile where it lands and lies there for good. -1 when full.</summary>
        public int Drop(Vector3 position, Vector3 velocity, Color32 color, float size)
        {
            int cluster = BeginCluster(position, velocity, 0f);
            if (cluster < 0)
                return -1;

            SetKeep(cluster);
            Cluster c = _clusters[cluster];
            c.Flags |= SPLAT;
            _clusters[cluster] = c;
            AddCube(cluster, position, size, color);
            return cluster;
        }

        /// <summary>
        /// Waits for the step started at the end of the last frame. VoxelWorld calls it at the very
        /// start of the frame, before physics runs; anything that adds debris calls it too.
        /// </summary>
        public void Complete()
        {
            if (!_stepping)
                return;
            _stepping = false;
            _step.Complete();
        }

        public VoxelDebris(Material material, int capacity, int lyingCapacity = LYING_CAPACITY)
        {
            _capacity = capacity;
            _material = material;
            _mesh = BuildCubeMesh();

            _clusters = new NativeList<Cluster>(capacity, Allocator.Persistent);
            _cubes = new NativeList<Cube>(capacity, Allocator.Persistent);
            _commands = new NativeArray<RaycastCommand>(capacity, Allocator.Persistent);
            _hits = new NativeArray<RaycastHit>(capacity, Allocator.Persistent);
            _matrices = new NativeArray<float4x4>(capacity, Allocator.Persistent);
            _colors = new NativeArray<float4>(capacity, Allocator.Persistent);
            _alive = new NativeReference<int>(Allocator.Persistent);

            int lying = math.max(1, lyingCapacity);
            _lyingMatrices = new NativeArray<float4x4>(lying, Allocator.Persistent);
            _lyingColors = new NativeArray<float4>(lying, Allocator.Persistent);
            _lying = new NativeReference<int2>(Allocator.Persistent);
        }

        public bool HasRoom(int cubes)
        {
            Complete();
            return _cubes.IsCreated && _cubes.Length + cubes <= _capacity;
        }

        /// <summary>Starts a clump flying; cubes are added to it with <see cref="AddCube"/>. -1 when full.</summary>
        public int BeginCluster(Vector3 center, Vector3 velocity, float spin = 4f)
        {
            Complete();
            if (!_clusters.IsCreated || _clusters.Length >= _capacity || _cubes.Length >= _capacity)
                return -1;

            _clusters.Add(new Cluster
            {
                Position = center,
                Velocity = velocity,
                Rotation = quaternion.identity,
                Spin = UnityEngine.Random.insideUnitSphere * spin,
                Life = UnityEngine.Random.Range(25f, 40f)
            });
            return _clusters.Length - 1;
        }

        /// <summary>Adds a cube, at its world position, to a clump begun this frame.</summary>
        public void AddCube(int cluster, Vector3 position, float size, Color32 color)
        {
            Complete();
            if (cluster < 0 || _cubes.Length >= _capacity)
                return;

            Cluster owner = _clusters[cluster];
            float3 offset = (float3)position - owner.Position;
            owner.Count++;
            owner.Radius = math.max(owner.Radius, math.length(offset) + size * 0.5f);
            _clusters[cluster] = owner;

            Color c = color;
            _cubes.Add(new Cube { Cluster = cluster, Offset = offset, Size = size, Color = new float4(c.r, c.g, c.b, c.a) });
        }

        /// <summary>A single crumb. Returns its cluster (-1 when full), so the caller can keep it.</summary>
        public int Spawn(Vector3 position, Vector3 velocity, Color32 color, float size)
        {
            int cluster = BeginCluster(position, velocity, 10f);
            ShortenLife(cluster, UnityEngine.Random.Range(8f, 14f));
            AddCube(cluster, position, size * UnityEngine.Random.Range(0.75f, 1f), color);
            return cluster;
        }

        /// <summary>Caps how long a clump begun this frame lies before it goes (an air bomb's clean up sooner).</summary>
        public void SetLife(int cluster, float life) => ShortenLife(cluster, life);

        /// <summary>A crumb on its own lies a shorter while than a clump.</summary>
        private void ShortenLife(int cluster, float life)
        {
            Complete();
            if (cluster < 0)
                return;
            Cluster c = _clusters[cluster];
            c.Life = math.min(c.Life, life);
            _clusters[cluster] = c;
        }

        /// <summary>
        /// Draws the cubes where the last step left them, then starts this frame's step on workers
        /// and returns without waiting: it runs while the frame renders, and is completed at the
        /// start of the next (<see cref="Complete"/>). New debris shows a frame later.
        /// </summary>
        public void Tick(float deltaTime)
        {
            Complete();
            if (!_clusters.IsCreated)
                return;

            Draw(_matrices, _colors, _alive.Value, ShadowCastingMode.On);
            // Lying cubes cast no shadow: tens of thousands of tiles and crumbs, flat on the ground.
            Draw(_lyingMatrices, _lyingColors, _lying.Value.y, ShadowCastingMode.Off);

            int clusters = _clusters.Length;
            if (clusters == 0 || deltaTime <= 0f)
            {
                _alive.Value = 0;
                return;
            }

            float dt = math.min(deltaTime, 1f / 20f);

            JobHandle handle = new BuildRaysJob
            {
                Clusters = _clusters.AsArray(),
                Commands = _commands,
                DeltaTime = dt,
                Query = new QueryParameters(CollisionMask, false, QueryTriggerInteraction.Ignore, false),
                DropQuery = new QueryParameters(DropMask, false, QueryTriggerInteraction.Ignore, false),
                Scene = Physics.defaultPhysicsScene
            }.Schedule(clusters, 128);

            handle = RaycastCommand.ScheduleBatch(_commands.GetSubArray(0, clusters), _hits.GetSubArray(0, clusters), 64, 1, handle);

            handle = new IntegrateJob
            {
                Clusters = _clusters.AsArray(),
                Hits = _hits,
                DeltaTime = dt,
                Gravity = (float3)Physics.gravity * GRAVITY_SCALE,
                Seed = _seed
            }.Schedule(clusters, 128, handle);

            handle = new ShatterJob
            {
                Clusters = _clusters,
                Cubes = _cubes,
                Capacity = _capacity,
                Seed = _seed++
            }.Schedule(handle);

            handle = new CompactJob
            {
                Clusters = _clusters,
                Cubes = _cubes,
                Matrices = _matrices,
                Colors = _colors,
                Alive = _alive,
                Lying = _lyingMatrices,
                LyingColors = _lyingColors,
                LyingState = _lying
            }.Schedule(handle);

            _step = handle;
            _stepping = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private void Draw(NativeArray<float4x4> source, NativeArray<float4> sourceColors, int count, ShadowCastingMode shadows)
        {
            NativeArray<Matrix4x4> matrices = source.Reinterpret<Matrix4x4>();
            NativeArray<Vector4> colors = sourceColors.Reinterpret<Vector4>();

            for (int start = 0; start < count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, count - start);
                NativeArray<Matrix4x4>.Copy(matrices, start, _batchMatrices, 0, n);
                NativeArray<Vector4>.Copy(colors, start, _batchColors, 0, n);
                _block.SetVectorArray(InstanceColorId, _batchColors);
                Graphics.DrawMeshInstanced(_mesh, 0, _material, _batchMatrices, n, _block, shadows, true);
            }
        }

        public void Dispose()
        {
            Complete();
            if (_clusters.IsCreated) _clusters.Dispose();
            if (_cubes.IsCreated) _cubes.Dispose();
            if (_commands.IsCreated) _commands.Dispose();
            if (_hits.IsCreated) _hits.Dispose();
            if (_matrices.IsCreated) _matrices.Dispose();
            if (_colors.IsCreated) _colors.Dispose();
            if (_alive.IsCreated) _alive.Dispose();
            if (_lyingMatrices.IsCreated) _lyingMatrices.Dispose();
            if (_lyingColors.IsCreated) _lyingColors.Dispose();
            if (_lying.IsCreated) _lying.Dispose();
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
        }

        /// <summary>
        /// A flying clump looks along its path as far as it will go this frame plus its own size;
        /// a resting one just under itself, to notice the ground it lay on was blown away.
        /// </summary>
        [BurstCompile]
        private struct BuildRaysJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Cluster> Clusters;
            [WriteOnly] public NativeArray<RaycastCommand> Commands;
            public float DeltaTime;
            public QueryParameters Query;
            public QueryParameters DropQuery;
            public PhysicsScene Scene;

            public void Execute(int i)
            {
                Cluster cluster = Clusters[i];
                float reach = math.max(0.03f, cluster.Radius * 0.6f);
                QueryParameters query = (cluster.Flags & SPLAT) != 0 ? DropQuery : Query;

                if (cluster.Resting != 0)
                {
                    Commands[i] = new RaycastCommand(Scene, cluster.Position, Vector3.down, query, reach * 1.6f);
                    return;
                }

                float speed = math.length(cluster.Velocity);
                float3 direction = speed > 0.0001f ? cluster.Velocity / speed : new float3(0f, -1f, 0f);
                Commands[i] = new RaycastCommand(Scene, cluster.Position, direction, query, speed * DeltaTime + reach);
            }
        }

        /// <summary>
        /// Heavy and dull: a landing keeps almost none of the bounce and loses most of the slide,
        /// so a clod lands with a thud and stays. A clump that hit hard is marked to break up.
        /// </summary>
        [BurstCompile]
        private struct IntegrateJob : IJobParallelFor
        {
            public NativeArray<Cluster> Clusters;
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            public float DeltaTime;
            public float3 Gravity;
            public uint Seed;

            public void Execute(int i)
            {
                Cluster cluster = Clusters[i];
                cluster.Life -= DeltaTime;

                if (cluster.Position.y < FELL_OUT_Y)
                {
                    cluster.Life = 0f;
                    cluster.Flags &= ~KEEP;
                    Clusters[i] = cluster;
                    return;
                }

                if ((cluster.Flags & KEEP) != 0)
                {
                    cluster.Flight += DeltaTime;
                    if (cluster.Flight >= MAX_KEPT_FLIGHT)
                    {
                        // Lies where it is: the compact pass of this step moves it to the lying store.
                        cluster.Velocity = float3.zero;
                        cluster.Spin = float3.zero;
                        cluster.Resting = 1;
                        cluster.Still = SETTLE_TIME;
                        Clusters[i] = cluster;
                        return;
                    }
                }

                RaycastHit hit = Hits[i];
                float3 normal = hit.normal;
                bool touched = math.lengthsq(normal) > 0.5f;

                if (cluster.Resting != 0)
                {
                    if (!touched)
                    {
                        cluster.Resting = 0;
                        cluster.Still = 0f;
                    }
                    else if ((cluster.Flags & KEEP) != 0)
                        cluster.Still += DeltaTime;

                    Clusters[i] = cluster;
                    return;
                }

                if (touched && (cluster.Flags & SPLAT) != 0)
                {
                    Lay(ref cluster, hit.point, normal, i);
                    Clusters[i] = cluster;
                    return;
                }

                if (touched)
                {
                    float vn = math.dot(cluster.Velocity, normal);
                    float3 tangent = cluster.Velocity - vn * normal;
                    float reach = math.max(0.03f, cluster.Radius * 0.6f);
                    cluster.Position = (float3)hit.point + normal * reach;

                    // Small clumps hold together better: three cubes or fewer need a much harder hit.
                    if (cluster.Count > 1 && -vn > SHATTER_SPEED * (cluster.Count <= 3 ? 1.8f : 1f))
                        cluster.Shatter = 1;

                    cluster.Velocity = tangent * 0.25f - normal * (vn * 0.08f);
                    cluster.Spin *= 0.2f;

                    if (normal.y > 0.5f && math.lengthsq(cluster.Velocity) < 0.6f)
                    {
                        cluster.Resting = 1;
                        cluster.Velocity = float3.zero;
                        cluster.Spin = float3.zero;
                    }
                }
                else
                {
                    cluster.Position += cluster.Velocity * DeltaTime;
                }

                if (cluster.Resting == 0)
                {
                    cluster.Velocity += Gravity * DeltaTime;
                    cluster.Rotation = math.normalize(math.mul(cluster.Rotation, quaternion.Euler(cluster.Spin * DeltaTime)));
                }

                Clusters[i] = cluster;
            }

            /// <summary>
            /// A drop spreads where it lands: a thin tile flat on the surface (a wall too), turned at
            /// random, lying at once - the compact pass moves it to the lying store in this step.
            /// </summary>
            private void Lay(ref Cluster cluster, float3 point, float3 normal, int i)
            {
                // Hashed, not xor-ed: a raw small seed gives near-zero first outputs, so the first
                // tiles of a step all lay at about the same angle. (| 1: a seed is never 0.)
                var random = new Unity.Mathematics.Random(math.hash(new uint2(Seed, (uint)i)) | 1u);
                quaternion tilt = FromTo(new float3(0f, 1f, 0f), normal);
                cluster.Rotation = math.mul(tilt, quaternion.AxisAngle(new float3(0f, 1f, 0f), random.NextFloat(0f, 2f * math.PI)));
                cluster.Position = point + normal * (cluster.Radius * SPLAT_HEIGHT + 0.002f);
                cluster.Velocity = float3.zero;
                cluster.Spin = float3.zero;
                cluster.Resting = 1;
                cluster.Still = SETTLE_TIME;
                cluster.Flags |= LAID;
            }

            private static quaternion FromTo(float3 from, float3 to)
            {
                float d = math.dot(from, to);
                if (d < -0.9999f)
                    return quaternion.AxisAngle(new float3(1f, 0f, 0f), math.PI);
                float3 axis = math.cross(from, to);
                return math.normalize(new quaternion(axis.x, axis.y, axis.z, 1f + d));
            }
        }

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

        /// <summary>
        /// Drops dead clumps and their cubes, moves the cubes of kept clumps that came to rest into the
        /// lying store (a ring: past its end the oldest are overwritten), and writes a matrix and a
        /// colour per flying cube.
        /// </summary>
        [BurstCompile]
        private struct CompactJob : IJob
        {
            public NativeList<Cluster> Clusters;
            public NativeList<Cube> Cubes;
            [WriteOnly] public NativeArray<float4x4> Matrices;
            [WriteOnly] public NativeArray<float4> Colors;
            public NativeReference<int> Alive;
            [WriteOnly] public NativeArray<float4x4> Lying;
            [WriteOnly] public NativeArray<float4> LyingColors;
            public NativeReference<int2> LyingState;

            public void Execute()
            {
                int count = Clusters.Length;
                var remap = new NativeArray<int>(count, Allocator.Temp);
                var settled = new NativeArray<Cluster>(count, Allocator.Temp);
                int clusters = 0;
                for (int i = 0; i < count; i++)
                {
                    Cluster cluster = Clusters[i];
                    if ((cluster.Flags & KEEP) != 0 && cluster.Resting != 0 && cluster.Still >= SETTLE_TIME && cluster.Count > 0)
                    {
                        settled[i] = cluster;
                        remap[i] = SETTLED;
                        continue;
                    }

                    bool alive = cluster.Life > 0f && cluster.Count > 0;
                    remap[i] = alive ? clusters : -1;
                    if (alive)
                        Clusters[clusters++] = cluster;
                }

                int2 lying = LyingState.Value;
                int capacity = Lying.Length;
                int cubes = 0;
                for (int i = 0; i < Cubes.Length; i++)
                {
                    Cube cube = Cubes[i];
                    int owner = remap[cube.Cluster];
                    if (owner == SETTLED)
                    {
                        Lying[lying.x] = Matrix(settled[cube.Cluster], cube, 1f);
                        LyingColors[lying.x] = cube.Color;
                        lying.x = (lying.x + 1) % capacity;
                        lying.y = math.min(lying.y + 1, capacity);
                        continue;
                    }

                    if (owner < 0)
                        continue;

                    cube.Cluster = owner;
                    Cubes[cubes] = cube;

                    Cluster cluster = Clusters[owner];
                    Matrices[cubes] = Matrix(cluster, cube, math.saturate(cluster.Life / 0.6f));
                    Colors[cubes] = cube.Color;
                    cubes++;
                }

                LyingState.Value = lying;
                Clusters.ResizeUninitialized(clusters);
                Cubes.ResizeUninitialized(cubes);
                Alive.Value = cubes;
            }

            private static float4x4 Matrix(in Cluster cluster, in Cube cube, float shrink)
            {
                float3 scale = (cluster.Flags & LAID) != 0
                    ? new float3(SPLAT_WIDTH, SPLAT_HEIGHT, SPLAT_WIDTH)
                    : new float3(1f);
                float3 position = cluster.Position + math.mul(cluster.Rotation, cube.Offset);
                return float4x4.TRS(position, cluster.Rotation, scale * (cube.Size * shrink));
            }
        }

        /// <summary>A unit cube with white vertex colours, so the instance colour shows as is.</summary>
        internal static Mesh BuildCubeMesh()
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var colors = new Color32[24];
            var indices = new int[36];
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

            int v = 0;
            int t = 0;
            for (int d = 0; d < 3; d++)
            {
                Vector3 n = axes[d];
                Vector3 u = axes[(d + 1) % 3];
                Vector3 w = axes[(d + 2) % 3];

                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = n * (0.5f * s) - u * 0.5f - w * 0.5f;
                    vertices[v] = a;
                    vertices[v + 1] = a + u;
                    vertices[v + 2] = a + u + w;
                    vertices[v + 3] = a + w;
                    for (int k = 0; k < 4; k++)
                    {
                        normals[v + k] = n * s;
                        colors[v + k] = new Color32(255, 255, 255, 255);
                    }

                    if (s > 0)
                    {
                        indices[t++] = v; indices[t++] = v + 1; indices[t++] = v + 2;
                        indices[t++] = v; indices[t++] = v + 2; indices[t++] = v + 3;
                    }
                    else
                    {
                        indices[t++] = v; indices[t++] = v + 2; indices[t++] = v + 1;
                        indices[t++] = v; indices[t++] = v + 3; indices[t++] = v + 2;
                    }

                    v += 4;
                }
            }

            var mesh = new Mesh { name = "VoxelDebrisCube", vertices = vertices, normals = normals, colors32 = colors, triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

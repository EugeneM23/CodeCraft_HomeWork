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
    /// Every clump that comes to rest (dust aside) lies for good: once it has lain still SETTLE_TIME
    /// (a kept clump after MAX_KEPT_FLIGHT wherever it is, a drop at once as a tile) its cubes move to
    /// a dense lying store that is only drawn (no raycasts), straight from native arrays, with no
    /// shadows. Flying clumps still go at the end of their life. A full store overwrites its slots in turn.
    /// </summary>
    public sealed partial class VoxelDebris : IDisposable
    {
        private const int BATCH = 1023;
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");

        /// <summary>Debris falls this many times faster than g: heavy clods, low arcs, a thud.</summary>
        public const float GRAVITY_SCALE = 2.2f;

        /// <summary>Impact speed (m/s) at which a clump breaks into single cubes.</summary>
        private const float SHATTER_SPEED = 2.2f;

        /// <summary>How long (s) a clump lies still before its cubes join the lying ones.</summary>
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
        /// <summary>A puff of dust: drifts down slowly, stops where it touches, never lies; shrinks and goes.</summary>
        private const int DUST = 8;

        /// <summary>Marks a clump in the compact pass whose cubes move to the lying store.</summary>
        private const int SETTLED = -2;

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
        private NativeArray<LyingCube> _lyingCubes;
        /// <summary>Drawn as is (RenderMeshInstanced takes the native array).</summary>
        private NativeArray<float4x4> _lyingMatrices;
        private NativeArray<float4> _lyingColors;
        /// <summary>Per batch of BATCH lying slots: 1 when its colours changed and its block must be set again.</summary>
        private NativeArray<byte> _dirtyBatches;
        private NativeReference<LyingState> _lyingState;
        /// <summary>One block per batch of lying cubes, its colours set only when the batch is dirty.</summary>
        private readonly MaterialPropertyBlock[] _lyingBlocks;
        /// <summary>
        /// How lying cubes are drawn: lit like the flying ones (DrawMeshInstanced's defaults - layer 0,
        /// light and reflection probes blended), but no shadows. Only matProps changes, per batch.
        /// </summary>
        private RenderParams _lyingParams;
        /// <summary>Debris time (s): the sum of the steps; when a cube lay down.</summary>
        private float _time;
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

        /// <summary>Clusters still simulated (flying or resting, not yet lying).</summary>
        public int ClusterCount
        {
            get
            {
                Complete();
                return _clusters.IsCreated ? _clusters.Length : 0;
            }
        }

        /// <summary>Cube counts of the clusters still simulated: for tests and debugging.</summary>
        public void GetClusterSizes(System.Collections.Generic.List<int> sizes)
        {
            Complete();
            sizes.Clear();
            if (!_clusters.IsCreated)
                return;
            for (int i = 0; i < _clusters.Length; i++)
                sizes.Add(_clusters[i].Count);
        }

        /// <summary>Single flying cubes of about this colour (±0.02 per channel): for tests.</summary>
        public int CountCrumbsOfColour(Color color)
        {
            Complete();
            int n = 0;
            var c = new float4(color.r, color.g, color.b, color.a);
            for (int i = 0; i < _cubes.Length; i++)
            {
                Cube cube = _cubes[i];
                if (_clusters[cube.Cluster].Count == 1 && (_clusters[cube.Cluster].Flags & DUST) == 0 &&
                    math.all(math.abs(cube.Color - c) < 0.02f))
                    n++;
            }
            return n;
        }

        /// <summary>Cubes lying for good (every clump that came to rest, landed drops; not dust).</summary>
        public int LyingCount
        {
            get
            {
                Complete();
                return _lyingState.IsCreated ? _lyingState.Value.Count : 0;
            }
        }

        /// <summary>One lying cube, by its slot (0 ... LyingCount - 1), as drawn (a sinking one shrunk): for tests and debugging.</summary>
        public bool TryGetLying(int slot, out Matrix4x4 matrix, out Color color)
        {
            Complete();
            matrix = default;
            color = default;
            if (!_lyingState.IsCreated || slot < 0 || slot >= _lyingState.Value.Count)
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

        public VoxelDebris(Material material, int capacity, int lyingCapacity = LYING_DEFAULT)
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
            int batches = (lying + BATCH - 1) / BATCH;
            _lyingCubes = new NativeArray<LyingCube>(lying, Allocator.Persistent);
            _lyingMatrices = new NativeArray<float4x4>(lying, Allocator.Persistent);
            _lyingColors = new NativeArray<float4>(lying, Allocator.Persistent);
            _dirtyBatches = new NativeArray<byte>(batches, Allocator.Persistent);
            _lyingState = new NativeReference<LyingState>(Allocator.Persistent);

            // The first SetVectorArray fixes the array's length for good: set it full length once.
            _lyingParams = new RenderParams(material)
            {
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.BlendProbes,
                reflectionProbeUsage = ReflectionProbeUsage.BlendProbes
            };

            _lyingBlocks = new MaterialPropertyBlock[batches];
            for (int i = 0; i < batches; i++)
            {
                _lyingBlocks[i] = new MaterialPropertyBlock();
                _lyingBlocks[i].SetVectorArray(InstanceColorId, _batchColors);
            }
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
            DrawLying();

            if (deltaTime <= 0f)
            {
                _alive.Value = 0;
                return;
            }

            float dt = math.min(deltaTime, 1f / 20f);
            _time += dt;

            int clusters = _clusters.Length;
            if (clusters == 0)
            {
                // Nothing flies, but lying cubes may still be sinking.
                _alive.Value = 0;
                _step = ScheduleLying(dt, default);
                _stepping = true;
                JobHandle.ScheduleBatchedJobs();
                return;
            }

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
                Lying = _lyingCubes,
                LyingMatrices = _lyingMatrices,
                LyingColors = _lyingColors,
                DirtyBatches = _dirtyBatches,
                State = _lyingState,
                Time = _time
            }.Schedule(handle);

            _step = ScheduleLying(dt, handle);
            _stepping = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private JobHandle ScheduleLying(float dt, JobHandle dependsOn) =>
            new LyingJob
            {
                Lying = _lyingCubes,
                Matrices = _lyingMatrices,
                Colors = _lyingColors,
                DirtyBatches = _dirtyBatches,
                State = _lyingState,
                DeltaTime = dt
            }.Schedule(dependsOn);

        /// <summary>
        /// Lying cubes, drawn straight from the native matrices, a batch at a time; a batch's colours
        /// are copied into its own block only when they changed. No shadows: tens of thousands of
        /// tiles and crumbs flat on the ground.
        /// </summary>
        private void DrawLying()
        {
            int count = _lyingState.Value.Count;
            NativeArray<Matrix4x4> matrices = _lyingMatrices.Reinterpret<Matrix4x4>();
            for (int start = 0, batch = 0; start < count; start += BATCH, batch++)
            {
                int n = Mathf.Min(BATCH, count - start);
                if (_dirtyBatches[batch] != 0)
                {
                    NativeArray<Vector4>.Copy(_lyingColors.Reinterpret<Vector4>(), start, _batchColors, 0, n);
                    _lyingBlocks[batch].SetVectorArray(InstanceColorId, _batchColors);
                    _dirtyBatches[batch] = 0;
                }
                _lyingParams.matProps = _lyingBlocks[batch];
                Graphics.RenderMeshInstanced(_lyingParams, _mesh, 0, matrices, n, start);
            }
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
            if (_lyingCubes.IsCreated) _lyingCubes.Dispose();
            if (_lyingMatrices.IsCreated) _lyingMatrices.Dispose();
            if (_lyingColors.IsCreated) _lyingColors.Dispose();
            if (_dirtyBatches.IsCreated) _dirtyBatches.Dispose();
            if (_lyingState.IsCreated) _lyingState.Dispose();
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
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

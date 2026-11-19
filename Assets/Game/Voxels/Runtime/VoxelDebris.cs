using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
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
    /// a dense lying store that is only drawn (no raycasts), baked into one mesh per world cell
    /// (VoxelDebris.Cells.cs), with no shadows. Flying clumps still go at the end of their life. A full
    /// store overwrites its slots in turn.
    /// </summary>
    public sealed partial class VoxelDebris : IDisposable
    {
        private const int BATCH = 1023;
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");

        private static readonly ProfilerMarker s_drawLyingMarker = new("VoxelDebris.DrawLying");
        private static readonly ProfilerMarker s_drawFlyingMarker = new("VoxelDebris.DrawFlying");
        private static readonly ProfilerMarker s_completeMarker = new("VoxelDebris.Complete");
        private static readonly ProfilerMarker s_scheduleMarker = new("VoxelDebris.Schedule");
        private static readonly ProfilerMarker s_cullLyingMarker = new("VoxelDebris.CullLying");

        /// <summary>
        /// Lying cells whose nearest point is farther than this (m) from the view camera are not
        /// drawn: crumbs and tiles a few centimetres across are a pixel or less out there.
        /// </summary>
        public const float LYING_DRAW_DISTANCE = 120f;

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
        /// <summary>
        /// Sticky goo (a creature's death): rises sluggishly (drag, light gravity), hangs at the top, drops heavily,
        /// sticks where it lands (no bounce, no break); a single drop stretches along its flight.
        /// </summary>
        private const int GOO = 16;
        /// <summary>
        /// Gore that lies where it lands as it is, never soaks into the ground (<see cref="GroundStains"/>): a body part
        /// (VoxelSpray.Chunks), or a drop laid back where the ground would not take it.
        /// </summary>
        private const int NOGROUND = 32;

        /// <summary>Most landed drops waiting for the ground (<see cref="TakeGroundStains"/>); past it the newest are lost (a burst far past any fight).</summary>
        private const int MAX_GROUND_READY = 8192;

        /// <summary>
        /// A drop of blood that landed on something facing up (<see cref="GroundStains"/>): gone from the store, it is
        /// for the ground to take as a stain (VoxelWorld paints it into the voxels there).
        /// </summary>
        public struct GroundStain
        {
            public float3 Point;
            public float3 Normal;
            /// <summary>Radius (m) of the splat it makes.</summary>
            public float Radius;
            /// <summary>Which shade of VoxelBlood it soaks in as.</summary>
            public int Shade;
        }

        /// <summary>Goo going up: velocity damped this much per second, gravity times this.</summary>
        private const float GOO_RISE_DRAG = 2.2f;
        private const float GOO_RISE_GRAVITY = 0.45f;
        /// <summary>Goo coming down: sideways damping per second, gravity times this (a heavy drop).</summary>
        private const float GOO_FALL_DRAG = 1.2f;
        private const float GOO_FALL_GRAVITY = 1.15f;
        /// <summary>A flying goo drop is this much longer per m/s of speed (and thinner to keep its volume), up to GOO_MAX_STRETCH.</summary>
        private const float GOO_STRETCH = 0.14f;
        private const float GOO_MAX_STRETCH = 2.6f;

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

        /// <summary>Goo that flew into the stain box (<see cref="SetStainBox"/>): where it hit, read with <see cref="TakeStainHits"/>.</summary>
        private NativeQueue<float3> _stainHits;
        /// <summary>Blood that landed this step (<see cref="GroundStains"/>), handed to <see cref="_groundReady"/> at Complete.</summary>
        private NativeQueue<GroundStain> _groundHits;
        private readonly System.Collections.Generic.List<GroundStain> _groundReady = new(256);
        private readonly System.Collections.Generic.List<Vector3> _stainReady = new(64);
        private bool _stainOn;
        private float3 _stainCenter;
        private quaternion _stainRotation;
        private float3 _stainHalf;
        private NativeArray<LyingCube> _lyingCubes;
        /// <summary>Per slot, as drawn when drawn instanced (a sinking cube shrunk).</summary>
        private NativeArray<float4x4> _lyingMatrices;
        private NativeArray<float4> _lyingColors;
        /// <summary>LyingQueryJob's hits, per worker thread: LyingJob sums them and does nothing when a step's queries hit nothing.</summary>
        private NativeArray<int> _queryHits;
        private readonly Plane[] _frustum = new Plane[6];
        private NativeReference<LyingState> _lyingState;
        /// <summary>
        /// How lying cubes not in a cell mesh are drawn instanced: layer 0 like the flying ones, no
        /// shadows, their box as bounds. No light or reflection probes: sampling them per instance cost
        /// ~1.25 ms per 50k cubes, and the scenes have no baked probes, so the ambient is the same.
        /// </summary>
        private RenderParams _lyingParams;
        /// <summary>Crush / Blow / Loosen asked for this frame (main thread writes it).</summary>
        private NativeList<LyingQuery> _queries;
        /// <summary>The queries the running step reads; swapped with <see cref="_queries"/> when a step is scheduled.</summary>
        private NativeList<LyingQuery> _queriesInFlight;
        /// <summary>Debris time (s): the sum of the steps; when a cube lay down.</summary>
        private float _time;
        /// <summary>When (VoxelDebris time) CrumbAgeJob next runs.</summary>
        private float _nextCrumbAge;
        private uint _seed = 1;

        /// <summary>The frame's step, still running on workers (see <see cref="Complete"/>).</summary>
        private JobHandle _step;
        private bool _stepping;

        /// <summary>
        /// The camera the game is seen through (VoxelWorld sets it each frame): lying cells outside
        /// its frustum or past LYING_DRAW_DISTANCE are not drawn. Null - every cell is drawn. The
        /// Scene view then shows only the lying cubes the game camera sees.
        /// </summary>
        public Camera ViewCamera { get; set; }

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
            int cluster = BeginGoreCluster(position, velocity, 0f);
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
        /// A box (world centre, rotation, half extents) that catches flying goo (<see cref="GooDrop"/>, <see cref="SetGoo"/>):
        /// a goo clump that comes into it is gone, and where it was goes to the stain hits (<see cref="TakeStainHits"/>).
        /// The tank sets its hull here each step (TankBloodBehaviour): goo does not collide with the tank, it stains it.
        /// One box test per flying goo clump in the step's job.
        /// </summary>
        public void SetStainBox(Vector3 center, Quaternion rotation, Vector3 halfExtents)
        {
            _stainOn = true;
            _stainCenter = center;
            _stainRotation = rotation;
            _stainHalf = halfExtents;
        }

        public void ClearStainBox() => _stainOn = false;

        /// <summary>
        /// Where goo came into the stain box in the steps completed since the last call, into <paramref name="into"/>. It never
        /// waits for the running step (that would stall the main thread on the workers): what it caught comes the next time.
        /// </summary>
        public int TakeStainHits(System.Collections.Generic.List<Vector3> into)
        {
            int count = _stainReady.Count;
            into.AddRange(_stainReady);
            _stainReady.Clear();
            return count;
        }

        /// <summary>A drop of goo: rises slowly, falls heavily, stretched in flight, a tile where it lands. -1 when full.</summary>
        public int GooDrop(Vector3 position, Vector3 velocity, Color32 color, float size)
        {
            int cluster = Drop(position, velocity, color, size);
            if (cluster >= 0)
            {
                Cluster c = _clusters[cluster];
                c.Flags |= GOO;
                _clusters[cluster] = c;
            }

            return cluster;
        }

        /// <summary>
        /// Marks a clump begun this frame as goo: it flies like <see cref="GooDrop"/> and sticks whole where it lands
        /// (it does not break or bounce), then lies for good.
        /// </summary>
        /// <param name="flesh">A body part, not blood: it lies whole where it lands and never soaks into the ground.</param>
        public void SetGoo(int cluster, bool flesh = false)
        {
            Complete();
            if (cluster < 0 || cluster >= _clusters.Length)
                return;
            SetKeep(cluster);
            Cluster c = _clusters[cluster];
            c.Flags |= GOO | (flesh ? NOGROUND : 0);
            c.Spin *= 0.25f;
            _clusters[cluster] = c;
        }

        /// <summary>
        /// Blood soaks into the ground: a drop or blob of gore whose colour is blood (VoxelBlood.IsBlood) that lands on
        /// something facing up leaves the store at once and goes to <see cref="TakeGroundStains"/> (VoxelWorld paints it
        /// into the voxels there; a crater then takes it with the dirt, nothing hangs in the air). Off: it lies as a tile.
        /// </summary>
        public bool GroundStains { get; set; }

        /// <summary>Landed drops waiting for the ground (<see cref="GroundStains"/>).</summary>
        public int GroundStainsWaiting => _groundReady.Count;

        /// <summary>
        /// Up to <paramref name="max"/> of the drops that landed (<see cref="GroundStains"/>), oldest first, into
        /// <paramref name="into"/>; the rest wait for the next call. Never waits for the running step.
        /// </summary>
        public int TakeGroundStains(System.Collections.Generic.List<GroundStain> into, int max)
        {
            int count = math.min(max, _groundReady.Count);
            for (int i = 0; i < count; i++)
                into.Add(_groundReady[i]);
            _groundReady.RemoveRange(0, count);
            return count;
        }

        /// <summary>
        /// A stain the ground would not take (no voxels there, or their palette full): laid back as a tile of blood that
        /// lies for good, as it did before <see cref="GroundStains"/>.
        /// </summary>
        public void LayStain(in GroundStain stain)
        {
            float size = math.clamp(stain.Radius / SPLAT_WIDTH * 2f, 0.04f, 0.12f);
            int cluster = Drop((Vector3)(stain.Point + stain.Normal * (size * 0.6f)), (Vector3)(-stain.Normal * 0.5f),
                VoxelBlood.Colour(stain.Shade, 0), size);
            if (cluster < 0)
                return;
            Cluster c = _clusters[cluster];
            c.Flags |= NOGROUND;
            _clusters[cluster] = c;
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
            using (s_completeMarker.Auto())
                _step.Complete();

            // The step is done: the goo it caught is handed over now, so readers never wait on a running step.
            while (_stainHits.IsCreated && _stainHits.TryDequeue(out float3 hit))
                _stainReady.Add(hit);
            while (_groundHits.IsCreated && _groundHits.TryDequeue(out GroundStain stain))
                if (_groundReady.Count < MAX_GROUND_READY)
                    _groundReady.Add(stain);
        }

        public VoxelDebris(Material material, int capacity, int lyingCapacity = LYING_DEFAULT, int cellCapacity = MAX_CELLS)
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
            _stainHits = new NativeQueue<float3>(Allocator.Persistent);
            _groundHits = new NativeQueue<GroundStain>(Allocator.Persistent);

            int lying = math.max(1, lyingCapacity);
            _lyingCubes = new NativeArray<LyingCube>(lying, Allocator.Persistent);
            _lyingMatrices = new NativeArray<float4x4>(lying, Allocator.Persistent);
            _lyingColors = new NativeArray<float4>(lying, Allocator.Persistent);
            _queryHits = new NativeArray<int>(JobsUtility.ThreadIndexCount, Allocator.Persistent);
            _lyingState = new NativeReference<LyingState>(Allocator.Persistent);
            _queries = new NativeList<LyingQuery>(MAX_QUERIES, Allocator.Persistent);
            _queriesInFlight = new NativeList<LyingQuery>(MAX_QUERIES, Allocator.Persistent);

            _lyingParams = new RenderParams(material)
            {
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off
            };
            CreateCells(material, cellCapacity);
        }

        /// <summary>
        /// Share of the flying store that dirt (anything but gore: earth, crumbs, dust, rubble) may fill; gore (blood drops,
        /// goo: <see cref="Drop"/>, <see cref="GooDrop"/>, <see cref="BeginGoreCluster"/>) may fill all of it, so the mud a
        /// tank churns up can never crowd the blood out. 1 (the default) makes no difference between them.
        /// </summary>
        public float DirtShare { get; set; } = 1f;

        /// <summary>Clumps of gore and of dirt refused for want of room since the store was made (benchmarks, the debug panel).</summary>
        public int RefusedGore { get; private set; }
        public int RefusedDirt { get; private set; }

        private int DirtCapacity => (int)(_capacity * math.saturate(DirtShare));

        /// <summary>Room for this many more cubes of dirt (within <see cref="DirtShare"/>).</summary>
        public bool HasRoom(int cubes)
        {
            Complete();
            return _cubes.IsCreated && _cubes.Length + cubes <= DirtCapacity;
        }

        /// <summary>Room for this many more cubes of gore (the whole store).</summary>
        public bool HasGoreRoom(int cubes)
        {
            Complete();
            return _cubes.IsCreated && _cubes.Length + cubes <= _capacity;
        }

        /// <summary>Starts a clump of dirt flying; cubes are added to it with <see cref="AddCube"/>. -1 when full (past <see cref="DirtShare"/>).</summary>
        public int BeginCluster(Vector3 center, Vector3 velocity, float spin = 4f) => Begin(center, velocity, spin, false);

        /// <summary>Starts a clump of gore (goo, blood) flying: it may take the share of the store dirt may not. -1 when full.</summary>
        public int BeginGoreCluster(Vector3 center, Vector3 velocity, float spin = 4f) => Begin(center, velocity, spin, true);

        private int Begin(Vector3 center, Vector3 velocity, float spin, bool gore)
        {
            Complete();
            if (!_clusters.IsCreated || _clusters.Length >= _capacity || _cubes.Length >= (gore ? _capacity : DirtCapacity))
            {
                if (gore)
                    RefusedGore++;
                else
                    RefusedDirt++;
                return -1;
            }

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
            if (owner.Tint.w == 0f)
                owner.Tint = new float4(color.r, color.g, color.b, math.max((int)color.a, 1)) / 255f;
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
            ApplyBuiltCells();

            // The lying state is read only here, with no job running: the numbers deciding whether to evict.
            LyingState seen = _lyingState.Value;

            using (s_drawFlyingMarker.Auto())
                Draw(_matrices, _colors, _alive.Value, ShadowCastingMode.On);
            using (s_drawLyingMarker.Auto())
                DrawLying();

            if (deltaTime <= 0f)
            {
                _alive.Value = 0;
                return;
            }

            float dt = math.min(deltaTime, 1f / 20f);
            _time += dt;

            using (s_scheduleMarker.Auto())
                Schedule(dt, seen);
        }

        /// <summary>Starts the frame's step on workers: the flying clusters' jobs, then the lying store's.</summary>
        private void Schedule(float dt, in LyingState seen)
        {
            // The step done (Complete above), the queries it read are spent: the frame's new ones go
            // to this step, and the main thread writes the next frame's into the emptied list.
            (_queries, _queriesInFlight) = (_queriesInFlight, _queries);
            _queries.Clear();
            // Reads the cells, so before any job of the step is scheduled.
            ChooseCellBuilds();

            int clusters = _clusters.Length;
            if (clusters == 0)
            {
                // Nothing flies, but lying cubes may still be sinking.
                _alive.Value = 0;
                _step = ScheduleLying(dt, default, seen);
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
                Seed = _seed,
                StainOn = _stainOn,
                StainCenter = _stainCenter,
                StainInverse = math.inverse(_stainRotation),
                StainHalf = _stainHalf,
                StainHits = _stainHits.AsParallelWriter(),
                GroundOn = GroundStains,
                GroundHits = _groundHits.AsParallelWriter()
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
                Cells = _cells,
                CellIndex = _cellIndex,
                FreeCells = _freeCells,
                State = _lyingState,
                Time = _time
            }.Schedule(handle);

            _step = ScheduleLying(dt, handle, seen);
            _stepping = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private JobHandle ScheduleLying(float dt, JobHandle dependsOn, in LyingState seen)
        {
            NativeArray<LyingQuery> queries = _queriesInFlight.AsArray();
            JobHandle handle = dependsOn;
            // Over the cubes lying when the frame began ("seen", read after Complete()): those the
            // compact pass lays down in this step meet the queries from the next step on.
            if (queries.Length > 0 && seen.Count > 0)
            {
                handle = new LyingQueryJob
                {
                    Lying = _lyingCubes,
                    Queries = queries,
                    State = _lyingState,
                    Hits = _queryHits
                }.Schedule(math.min(seen.Count, _lyingCubes.Length), 256, handle);
            }

            // The old lying crumbs sink, once a second (after the queries: a cube a query hit is left to LyingJob).
            if (seen.Count > 0 && _time >= _nextCrumbAge)
            {
                _nextCrumbAge = _time + CRUMB_AGE_PERIOD;
                handle = new CrumbAgeJob
                {
                    Lying = _lyingCubes,
                    Cells = _cells,
                    State = _lyingState,
                    Time = _time
                }.Schedule(handle);
            }

            handle = new LyingJob
            {
                Lying = _lyingCubes,
                Matrices = _lyingMatrices,
                Colors = _lyingColors,
                Cells = _cells,
                State = _lyingState,
                Clusters = _clusters,
                Cubes = _cubes,
                Capacity = _capacity,
                Queries = queries,
                QueryHits = _queryHits,
                Seed = _seed ^ math.asuint(_time),
                DeltaTime = dt
            }.Schedule(handle);

            // "seen" was read on the main thread right after Complete(), before any job of this step was scheduled.
            if (seen.Count > EVICT_FROM * _lyingCubes.Length && seen.Evicting == 0)
            {
                handle = new EvictJob
                {
                    Lying = _lyingCubes,
                    Cells = _cells,
                    State = _lyingState,
                    Time = _time,
                    ViewPoint = ViewPoint
                }.Schedule(handle);
            }
            // Last: the cells built see every change of the step.
            return ScheduleCells(handle);
        }

        private static Bounds ToBounds(in float3x2 box) => new((box.c0 + box.c1) * 0.5f, box.c1 - box.c0);

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
            if (_stainHits.IsCreated) _stainHits.Dispose();
            if (_groundHits.IsCreated) _groundHits.Dispose();
            if (_lyingCubes.IsCreated) _lyingCubes.Dispose();
            if (_lyingMatrices.IsCreated) _lyingMatrices.Dispose();
            if (_lyingColors.IsCreated) _lyingColors.Dispose();
            if (_queryHits.IsCreated) _queryHits.Dispose();
            if (_lyingState.IsCreated) _lyingState.Dispose();
            if (_queries.IsCreated) _queries.Dispose();
            if (_queriesInFlight.IsCreated) _queriesInFlight.Dispose();
            DisposeCells();
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

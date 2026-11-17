using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    public sealed partial class VoxelDebris
    {
        /// <summary>
        /// Lying cubes kept by default: past this, new ones overwrite the slots in turn. 30 000
        /// instanced cubes put the air strike past the 5 ms budget (/perf-check, 2026-11-17); baked
        /// into per-cell meshes (VoxelDebris.Cells.cs) they take a few hundred draw calls at most.
        /// </summary>
        public const int LYING_DEFAULT = 30000;

        /// <summary>How long (s) a lying cube takes to sink into the ground and go.</summary>
        private const float SINK_TIME = 1.5f;

        /// <summary>A lying cube marked by a query this step: LyingJob acts on it in the same step.</summary>
        private const int HIT = 16;
        /// <summary>A lying cube sinking because a round of eviction chose it (not a crush or a blow with no room).</summary>
        private const int EVICTED = 32;
        /// <summary>
        /// A lying cube not yet baked into its cell's mesh (or with no cell at all): drawn instanced
        /// until a build of its cell clears the flag.
        /// </summary>
        private const int PENDING = 64;

        /// <summary>How long (s) a crushed cube takes to sink.</summary>
        private const float CRUSH_SINK_TIME = 0.4f;

        private const int QUERY_CRUSH = 1, QUERY_BLOW = 2, QUERY_LOOSEN = 3;
        /// <summary>Queries per frame; past it the rest are dropped.</summary>
        private const int MAX_QUERIES = 32;
        /// <summary>
        /// Query slots a frame that crushes never take: one tank sends a sphere per road wheel (16),
        /// so a few tanks would fill the list and a blast's Blow / Loosen would be dropped.
        /// </summary>
        private const int BLAST_RESERVE = 8;
        /// <summary>Most crumbs one crushed piece throws in a step.</summary>
        private const int MAX_CRUSH_CRUMBS = 6;
        /// <summary>Share of a crushed piece that flies back as crumbs.</summary>
        private const float CRUSH_CRUMB_SHARE = 1f / 3f;
        private const float CRUMB_LIFE = 10f;
        private const float WOKEN_LIFE = 20f;

        /// <summary>A sphere that crushes, blows or loosens what lies in it, queued for the next step.</summary>
        private struct LyingQuery
        {
            public float3 Center;
            public float Radius;
            /// <summary>Crush: the wheel's velocity (crumbs fly back against it).</summary>
            public float3 Velocity;
            /// <summary>Blow: speed (m/s) at the centre, falling to 0 at the radius.</summary>
            public float Speed;
            public int Kind;
        }

        /// <summary>A cube lying for good: only drawn.</summary>
        private struct LyingCube
        {
            public float3 Position;
            public quaternion Rotation;
            /// <summary>Drawn scale: the cube's size, times the splat tile's shape for a laid drop.</summary>
            public float3 Scale;
            public float Size;
            public float4 Color;
            /// <summary>The piece it lay down with: woken cubes of one group fly on as one clump.</summary>
            public int Group;
            /// <summary>When it lay down (VoxelDebris time, s).</summary>
            public float Born;
            /// <summary>0 — lying; > 0 — sinking for this many seconds of SinkTime.</summary>
            public float Sinking;
            public float SinkTime;
            /// <summary>LAID (blood tile), HIT (marked by a query this step), EVICTED (sinking by eviction), PENDING (not baked).</summary>
            public int Flags;
            /// <summary>Which query hit it (index + 1), 0 — none.</summary>
            public int Query;
            /// <summary>
            /// The first cube of its group, where it lay: the group's cubes sit on a grid of their size
            /// around it, turned by Rotation (faces between neighbours on that grid are not baked).
            /// </summary>
            public float3 Anchor;
            /// <summary>Its cell slot (see VoxelDebris.Cells.cs); -1 — no slot was free, drawn instanced.</summary>
            public int Cell;
        }

        private struct LyingState
        {
            public int Count;
            public int NextGroup;
            /// <summary>Where a cube goes when the store is full: walks round, overwriting.</summary>
            public int Overwrite;
            /// <summary>Cubes sinking because of eviction (flag EVICTED): no new round starts while any sink.</summary>
            public int Evicting;
            /// <summary>Cubes sinking for any reason: LyingJob has nothing to do while this is 0.</summary>
            public int Sinking;
            /// <summary>Cell slots taken (they are never given back).</summary>
            public int Cells;
            /// <summary>Cubes flagged PENDING: LyingInstancesJob has nothing to gather while this and Sinking are 0.</summary>
            public int Unbaked;
        }

        /// <summary>The store is nearly full past this share of its capacity: the old and far cubes start to sink.</summary>
        private const float EVICT_FROM = 0.95f;

        /// <summary>How much of the capacity one round of eviction sinks.</summary>
        private const float EVICT_SHARE = 0.02f;

        /// <summary>Age (s) that weighs as much as EVICT_DISTANCE metres when choosing what goes first.</summary>
        private const float EVICT_AGE = 120f;

        /// <summary>Distance (m) from the viewer that weighs as much as EVICT_AGE seconds.</summary>
        private const float EVICT_DISTANCE = 60f;

        /// <summary>Where the viewer is: eviction sinks the cubes farthest from it first (VoxelWorld sets it each frame).</summary>
        public Vector3 ViewPoint { get; set; }

        /// <summary>
        /// Lying cubes in the sphere under a road wheel break: a third fly back as crumbs, the rest sink.
        /// Crush takes at most MAX_QUERIES - BLAST_RESERVE slots a frame; the rest are kept for Blow / Loosen.
        /// </summary>
        public void Crush(Vector3 center, float radius, Vector3 velocity) =>
            Query(new LyingQuery { Center = center, Radius = radius, Velocity = velocity, Kind = QUERY_CRUSH });

        /// <summary>Lying cubes in the sphere wake and fly away from the centre, harder nearer it; blood stays.</summary>
        public void Blow(Vector3 center, float radius, float speed) =>
            Query(new LyingQuery { Center = center, Radius = radius, Speed = speed, Kind = QUERY_BLOW });

        /// <summary>Lying cubes in the sphere wake and fall: the ground under them was dug away.</summary>
        public void Loosen(Vector3 center, float radius) =>
            Query(new LyingQuery { Center = center, Radius = radius, Kind = QUERY_LOOSEN });

        /// <summary>
        /// Queued for the next step; past MAX_QUERIES in a frame the rest are dropped (crushes past
        /// MAX_QUERIES - BLAST_RESERVE). A sphere with a non-finite centre, radius, velocity or speed is ignored.
        /// No Complete(): the running step reads the other list (<see cref="_queriesInFlight"/>);
        /// this one is touched only by the main thread until Tick swaps them, after its own Complete().
        /// </summary>
        private void Query(in LyingQuery query)
        {
            if (!_queries.IsCreated || !(query.Radius > 0f) || !math.isfinite(query.Radius) ||
                !math.all(math.isfinite(query.Center)) || !math.all(math.isfinite(query.Velocity)) ||
                !math.isfinite(query.Speed))
                return;
            int limit = query.Kind == QUERY_CRUSH ? MAX_QUERIES - BLAST_RESERVE : MAX_QUERIES;
            if (_queries.Length < limit)
                _queries.Add(query);
        }

        /// <summary>Lying cubes within <paramref name="radius"/> of a point (sinking ones too): for tests and debugging.</summary>
        public int CountLyingNear(Vector3 point, float radius)
        {
            Complete();
            if (!_lyingState.IsCreated)
                return 0;

            int n = 0;
            int count = _lyingState.Value.Count;
            float3 p = point;
            for (int i = 0; i < count; i++)
                if (math.distance(_lyingCubes[i].Position, p) <= radius)
                    n++;
            return n;
        }

        /// <summary>
        /// A round of eviction: marks the highest scoring cubes (oldest and farthest from the viewer)
        /// to sink, about EVICT_SHARE of the capacity. A 64-bin histogram of scores finds the cut-off
        /// in two passes without sorting. Runs only when the store is nearly full and no round is sinking.
        /// </summary>
        [BurstCompile]
        private struct EvictJob : IJob
        {
            public NativeArray<LyingCube> Lying;
            public NativeArray<LyingCell> Cells;
            public NativeReference<LyingState> State;
            public float Time;
            public float3 ViewPoint;

            public void Execute()
            {
                LyingState state = State.Value;
                int count = state.Count;
                const int BINS = 64;

                float max = 0f;
                for (int i = 0; i < count; i++)
                    if (Lying[i].Sinking == 0f)
                        max = math.max(max, Score(Lying[i]));
                if (max <= 0f)
                    return;

                var bins = new NativeArray<int>(BINS, Allocator.Temp);
                for (int i = 0; i < count; i++)
                    if (Lying[i].Sinking == 0f)
                        bins[math.min(BINS - 1, (int)(Score(Lying[i]) / max * BINS))]++;

                int want = math.max(1, (int)(Lying.Length * EVICT_SHARE));
                int threshold = BINS;
                for (int sum = 0; threshold > 0 && sum < want;)
                    sum += bins[--threshold];

                int marked = 0;
                for (int i = 0; i < count && marked < want; i++)
                {
                    LyingCube cube = Lying[i];
                    if (cube.Sinking != 0f || math.min(BINS - 1, (int)(Score(cube) / max * BINS)) < threshold)
                        continue;
                    cube.Sinking = 1e-4f;
                    // Each cube its own time (0.6-1.4 x SINK_TIME, from its slot): the round does not
                    // finish in one step, which would dirty nearly every colour batch in one frame.
                    uint h = math.hash(new uint2((uint)i, 0x9e3779b9u));
                    cube.SinkTime = SINK_TIME * (0.6f + 0.8f * ((h & 0xffffu) / 65535f));
                    cube.Flags |= EVICTED;
                    Lying[i] = cube;
                    MarkDirty(Cells, cube.Cell);
                    marked++;
                }

                bins.Dispose();
                state.Evicting = marked;
                state.Sinking += marked;
                State.Value = state;
            }

            private float Score(in LyingCube cube) =>
                (Time - cube.Born) / EVICT_AGE + math.distance(cube.Position, ViewPoint) / EVICT_DISTANCE;
        }

        /// <summary>Cube counts of the lying pieces (cubes that lay down together): for tests and debugging.</summary>
        public void GetLyingGroupSizes(System.Collections.Generic.List<int> sizes)
        {
            Complete();
            sizes.Clear();
            if (!_lyingState.IsCreated)
                return;

            var byGroup = new System.Collections.Generic.Dictionary<int, int>();
            int count = _lyingState.Value.Count;
            for (int i = 0; i < count; i++)
            {
                int group = _lyingCubes[i].Group;
                byGroup.TryGetValue(group, out int n);
                byGroup[group] = n + 1;
            }
            sizes.AddRange(byGroup.Values);
        }

        /// <summary>
        /// Marks the lying cubes the step's queries touch: each slot, unless it is sinking already or
        /// is blood, takes the first query whose sphere holds it (HIT, Query = index + 1). Scheduled
        /// over the cubes lying when the frame began, only in steps that have queries; slots past the
        /// count are skipped. Counts its hits per worker thread, so LyingJob can tell a step with none.
        /// </summary>
        [BurstCompile]
        private struct LyingQueryJob : IJobParallelFor
        {
            public NativeArray<LyingCube> Lying;
            [ReadOnly] public NativeArray<LyingQuery> Queries;
            [ReadOnly] public NativeReference<LyingState> State;
            /// <summary>Hits per thread index: each thread adds only to its own.</summary>
            [NativeDisableParallelForRestriction] public NativeArray<int> Hits;
            [NativeSetThreadIndex] private int _threadIndex;

            public void Execute(int i)
            {
                if (i >= State.Value.Count)
                    return;

                LyingCube cube = Lying[i];
                if (cube.Sinking > 0f || (cube.Flags & LAID) != 0)
                    return;

                for (int q = 0; q < Queries.Length; q++)
                {
                    LyingQuery query = Queries[q];
                    // Written so that a NaN distance or radius never counts as inside.
                    if (!(math.distancesq(cube.Position, query.Center) <= query.Radius * query.Radius))
                        continue;

                    cube.Flags |= HIT;
                    cube.Query = q + 1;
                    Lying[i] = cube;
                    Hits[_threadIndex]++;
                    return;
                }
            }
        }

        /// <summary>
        /// The lying store's step, after the compact pass. Cubes a query hit go first: crushed ones
        /// throw a third (at most MAX_CRUSH_CRUMBS a piece) back as crumbs and sink the rest in
        /// CRUSH_SINK_TIME; blown or loosened ones fly again, the cubes of one group as one clump
        /// (sinking instead when the flying lists are full). Then sinking cubes go down and shrink,
        /// and once sunk - like a cube that flew off - their slot is closed by moving the last one
        /// into it (walking from the end, so the moved cube has had its step already). New clumps
        /// go to the end of the flying lists and fly from the next step.
        /// </summary>
        [BurstCompile]
        private struct LyingJob : IJob
        {
            public NativeArray<LyingCube> Lying;
            public NativeArray<float4x4> Matrices;
            public NativeArray<float4> Colors;
            /// <summary>A cell is marked dirty when one of its cubes starts sinking or goes.</summary>
            public NativeArray<LyingCell> Cells;
            /// <summary>LyingQueryJob's hits per thread: summed and cleared here.</summary>
            public NativeArray<int> QueryHits;
            public NativeReference<LyingState> State;
            public NativeList<Cluster> Clusters;
            public NativeList<Cube> Cubes;
            /// <summary>Flying capacity: clusters and cubes each.</summary>
            public int Capacity;
            [ReadOnly] public NativeArray<LyingQuery> Queries;
            public uint Seed;
            public float DeltaTime;

            public void Execute()
            {
                LyingState state = State.Value;
                int hits = 0;
                if (Queries.Length > 0)
                    for (int t = 0; t < QueryHits.Length; t++)
                    {
                        hits += QueryHits[t];
                        QueryHits[t] = 0;
                    }

                // Nothing sinks and no query hit anything this step: the cubes just lie.
                if (state.Sinking == 0 && hits == 0)
                    return;

                bool queried = hits > 0;
                int firstCluster = Clusters.Length;
                int firstCube = Cubes.Length;
                var random = new Unity.Mathematics.Random(math.hash(new uint2(Seed, 0x5bd1e995u)) | 1u);
                var groupCluster = new NativeParallelHashMap<int, int>(queried ? 256 : 1, Allocator.Temp);
                var groupCrumbs = new NativeParallelHashMap<int, int>(queried ? 256 : 1, Allocator.Temp);

                int count = state.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    LyingCube cube = Lying[i];
                    // Most cubes just lie: nothing to write back.
                    if (cube.Sinking <= 0f && (cube.Flags & HIT) == 0)
                        continue;

                    if ((cube.Flags & HIT) != 0)
                    {
                        LyingQuery query = Queries[cube.Query - 1];
                        cube.Flags &= ~HIT;
                        cube.Query = 0;

                        bool gone = query.Kind == QUERY_CRUSH
                            ? Crumble(cube, query, ref groupCrumbs, ref random)
                            : Wake(cube, query, ref groupCluster, ref random);
                        if (gone)
                        {
                            Remove(i, ref count, ref state);
                            continue;
                        }

                        // A crushed cube that stays, or a woken one with no room to fly: it sinks,
                        // drawn instanced, out of its cell's mesh.
                        cube.Sinking = 1e-4f;
                        cube.SinkTime = query.Kind == QUERY_CRUSH ? CRUSH_SINK_TIME : SINK_TIME;
                        state.Sinking++;
                        MarkDirty(Cells, cube.Cell);
                    }

                    cube.Sinking += DeltaTime;
                    float t = cube.Sinking / math.max(cube.SinkTime, 1e-4f);
                    if (t >= 1f)
                    {
                        state.Sinking = math.max(0, state.Sinking - 1);
                        if ((cube.Flags & EVICTED) != 0)
                            state.Evicting = math.max(0, state.Evicting - 1);
                        Remove(i, ref count, ref state);
                        continue;
                    }

                    Matrices[i] = float4x4.TRS(cube.Position - new float3(0f, 1f, 0f) * cube.Size * t, cube.Rotation, cube.Scale * (1f - t));
                    Lying[i] = cube;
                }

                Recentre(firstCluster, firstCube);
                groupCluster.Dispose();
                groupCrumbs.Dispose();

                state.Count = count;
                State.Value = state;
            }

            /// <summary>
            /// Closes slot <paramref name="i"/> by moving the last cube into it. The moved cube keeps its
            /// cell (a build gathers a cell's cubes by scanning the slots); the gone one's cell loses it.
            /// </summary>
            private void Remove(int i, ref int count, ref LyingState state)
            {
                LyingCube gone = Lying[i];
                // A sinking cube left its cell's mesh when it started to sink: no rebuild for it now.
                LeaveCell(Cells, gone.Cell, gone.Sinking <= 0f);
                if ((gone.Flags & PENDING) != 0)
                    state.Unbaked = math.max(0, state.Unbaked - 1);

                int last = count - 1;
                Lying[i] = Lying[last];
                Matrices[i] = Matrices[last];
                Colors[i] = Colors[last];
                count = last;
            }

            /// <summary>A crushed cube flies back from under the track as a crumb (true), or stays to sink (false).</summary>
            private bool Crumble(in LyingCube cube, in LyingQuery query, ref NativeParallelHashMap<int, int> groupCrumbs,
                ref Unity.Mathematics.Random random)
            {
                if (random.NextFloat() >= CRUSH_CRUMB_SHARE)
                    return false;
                groupCrumbs.TryGetValue(cube.Group, out int crumbs);
                if (crumbs >= MAX_CRUSH_CRUMBS || Clusters.Length >= Capacity || Cubes.Length >= Capacity)
                    return false;
                groupCrumbs[cube.Group] = crumbs + 1;

                float size = cube.Size * 0.8f;
                Clusters.Add(new Cluster
                {
                    Position = cube.Position,
                    Velocity = -query.Velocity * random.NextFloat(0.3f, 0.6f) + new float3(0f, random.NextFloat(1f, 3f), 0f),
                    Rotation = cube.Rotation,
                    Spin = random.NextFloat3Direction() * random.NextFloat(6f, 12f),
                    Radius = size * 0.5f,
                    Life = CRUMB_LIFE,
                    Count = 1
                });
                Cubes.Add(new Cube { Cluster = Clusters.Length - 1, Size = size, Color = cube.Color });
                return true;
            }

            /// <summary>
            /// A blown or loosened cube joins its group's clump, begun at the first cube of the group
            /// met (true); false when the flying lists are full.
            /// </summary>
            private bool Wake(in LyingCube cube, in LyingQuery query, ref NativeParallelHashMap<int, int> groupCluster,
                ref Unity.Mathematics.Random random)
            {
                if (Cubes.Length >= Capacity)
                    return false;

                if (!groupCluster.TryGetValue(cube.Group, out int index))
                {
                    if (Clusters.Length >= Capacity)
                        return false;

                    float3 velocity = float3.zero;
                    float3 spin = float3.zero;
                    if (query.Kind == QUERY_BLOW)
                    {
                        float3 away = cube.Position - query.Center;
                        float d = math.length(away);
                        float3 dir = d > 1e-4f ? away / d : new float3(0f, 1f, 0f);
                        dir.y = math.max(dir.y, 0.5f);
                        dir = math.normalize(dir);
                        velocity = dir * (query.Speed * math.saturate(1f - d / query.Radius));
                        spin = random.NextFloat3Direction() * random.NextFloat(2f, 6f);
                    }

                    Clusters.Add(new Cluster
                    {
                        Position = cube.Position,
                        Velocity = velocity,
                        Rotation = cube.Rotation,
                        Spin = spin,
                        Life = WOKEN_LIFE,
                        // A piece of a break: it may break once more where it lands, but not into a storm of crumbs.
                        Generation = 1
                    });
                    index = Clusters.Length - 1;
                    groupCluster.TryAdd(cube.Group, index);
                }

                Cluster cluster = Clusters[index];
                float3 offset = math.mul(math.inverse(cluster.Rotation), cube.Position - cluster.Position);
                cluster.Count++;
                cluster.Radius = math.max(cluster.Radius, math.length(offset) + cube.Size * 0.5f);
                Clusters[index] = cluster;
                Cubes.Add(new Cube { Cluster = index, Offset = offset, Size = cube.Size, Color = cube.Color });
                return true;
            }

            /// <summary>
            /// Moves each new clump's origin to the middle of its cubes: begun at its first cube, a long
            /// piece would reach far past that end and land (and lie) a reach too high.
            /// </summary>
            private void Recentre(int firstCluster, int firstCube)
            {
                int added = Clusters.Length - firstCluster;
                if (added <= 0)
                    return;

                var centres = new NativeArray<float3>(added, Allocator.Temp);
                for (int c = firstCube; c < Cubes.Length; c++)
                    centres[Cubes[c].Cluster - firstCluster] += Cubes[c].Offset;

                for (int j = 0; j < added; j++)
                {
                    Cluster cluster = Clusters[firstCluster + j];
                    centres[j] /= math.max(1, cluster.Count);
                    cluster.Position += math.mul(cluster.Rotation, centres[j]);
                    cluster.Radius = 0f;
                    Clusters[firstCluster + j] = cluster;
                }

                for (int c = firstCube; c < Cubes.Length; c++)
                {
                    Cube cube = Cubes[c];
                    int j = cube.Cluster - firstCluster;
                    cube.Offset -= centres[j];
                    Cubes[c] = cube;

                    Cluster cluster = Clusters[cube.Cluster];
                    cluster.Radius = math.max(cluster.Radius, math.length(cube.Offset) + cube.Size * 0.5f);
                    Clusters[cube.Cluster] = cluster;
                }

                centres.Dispose();
            }
        }
    }
}

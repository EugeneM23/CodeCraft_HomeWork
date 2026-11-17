using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    public sealed partial class VoxelDebris
    {
        /// <summary>Lying cubes kept by default: past this, new ones overwrite the slots in turn.</summary>
        public const int LYING_DEFAULT = 100000;

        /// <summary>How long (s) a lying cube takes to sink into the ground and go.</summary>
        private const float SINK_TIME = 1.5f;

        /// <summary>A lying cube marked by a query this step (Task 5 acts on it; cleared each step).</summary>
        private const int HIT = 16;

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
            /// <summary>LAID (blood tile), HIT (marked by a query this step).</summary>
            public int Flags;
            /// <summary>Which query hit it (index + 1), 0 — none.</summary>
            public int Query;
        }

        private struct LyingState
        {
            public int Count;
            public int NextGroup;
            /// <summary>Where a cube goes when the store is full: walks round, overwriting.</summary>
            public int Overwrite;
            /// <summary>Cubes sinking because of eviction (SinkTime == SINK_TIME): no new round starts while any sink.</summary>
            public int Evicting;
            /// <summary>Cubes sinking for any reason: LyingJob has nothing to do while this is 0.</summary>
            public int Sinking;
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
                    cube.SinkTime = SINK_TIME;
                    Lying[i] = cube;
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
        /// The lying store's step, after the compact pass: sinking cubes go down and shrink, and
        /// once sunk their slot is closed by moving the last one into it (walking from the end, so
        /// the moved cube has had its step already). Query marks are cleared here (Task 5 acts on them).
        /// </summary>
        [BurstCompile]
        private struct LyingJob : IJob
        {
            public NativeArray<LyingCube> Lying;
            public NativeArray<float4x4> Matrices;
            public NativeArray<float4> Colors;
            public NativeArray<byte> DirtyBatches;
            public NativeReference<LyingState> State;
            public float DeltaTime;

            public void Execute()
            {
                LyingState state = State.Value;
                // Nothing sinks (and no query has marked anything): the cubes just lie.
                if (state.Sinking == 0)
                    return;

                int count = state.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    LyingCube cube = Lying[i];
                    // Most cubes just lie: nothing to write back.
                    if (cube.Sinking <= 0f && (cube.Flags & HIT) == 0 && cube.Query == 0)
                        continue;

                    if (cube.Sinking > 0f)
                    {
                        cube.Sinking += DeltaTime;
                        float t = cube.Sinking / math.max(cube.SinkTime, 1e-4f);
                        if (t >= 1f)
                        {
                            state.Sinking = math.max(0, state.Sinking - 1);
                            if (cube.SinkTime == SINK_TIME)
                                state.Evicting = math.max(0, state.Evicting - 1);

                            int last = count - 1;
                            Lying[i] = Lying[last];
                            Matrices[i] = Matrices[last];
                            Colors[i] = Colors[last];
                            count = last;
                            DirtyBatches[i / BATCH] = 1;
                            DirtyBatches[last / BATCH] = 1;
                            continue;
                        }

                        Matrices[i] = float4x4.TRS(cube.Position - new float3(0f, 1f, 0f) * cube.Size * t, cube.Rotation, cube.Scale * (1f - t));
                    }

                    // HIT is acted on in Task 5; here the mark only lives one step.
                    cube.Flags &= ~HIT;
                    cube.Query = 0;
                    Lying[i] = cube;
                }

                state.Count = count;
                State.Value = state;
            }
        }
    }
}

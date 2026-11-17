using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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
            /// <summary>Cubes sinking because of eviction (Task 4).</summary>
            public int Evicting;
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

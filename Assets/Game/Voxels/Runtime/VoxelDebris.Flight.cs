using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    public sealed partial class VoxelDebris
    {
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
            /// <summary>How long the clump has rested, s: past SETTLE_TIME it lies for good.</summary>
            public float Still;
            /// <summary>How long a kept clump has been simulated, s: past MAX_KEPT_FLIGHT it lies where it is.</summary>
            public float Flight;
            /// <summary>Speed (m/s) along the normal of the landing that marked it to break.</summary>
            public float Impact;
            /// <summary>Where that landing touched, and the surface normal there.</summary>
            public float3 HitPoint;
            public float3 HitNormal;
            /// <summary>How many breaks made this clump: 0 for a clump thrown as is, its pieces 1, theirs 2.</summary>
            public int Generation;
        }

        private struct Cube
        {
            public int Cluster;
            public float3 Offset;
            public float Size;
            public float4 Color;
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
                bool dust = (cluster.Flags & DUST) != 0;
                float3 gravity = dust ? Gravity * DUST_GRAVITY : Gravity;

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
                    else
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

                if (touched && dust)
                {
                    // Dust settles where it touches: no bounce, no break, it just shrinks away.
                    cluster.Position = (float3)hit.point + normal * math.max(0.03f, cluster.Radius * 0.6f);
                    cluster.Velocity = float3.zero;
                    cluster.Spin = float3.zero;
                    cluster.Resting = 1;
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
                    // Pieces of a break break once more at most, and only big ones (see MAX_GENERATION).
                    if (cluster.Count > 1 && CanBreak(cluster) && -vn > SHATTER_SPEED * (cluster.Count <= 3 ? 1.8f : 1f))
                    {
                        cluster.Shatter = 1;
                        cluster.Impact = -vn;
                        cluster.HitPoint = hit.point;
                        cluster.HitNormal = normal;
                    }

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
                    cluster.Velocity += gravity * DeltaTime;
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
        /// Drops dead clumps and their cubes, moves the cubes of every clump that has lain still
        /// SETTLE_TIME (dust aside) into the lying store - each such clump a group of its own; a full
        /// store overwrites its slots in turn - and writes a matrix and a colour per flying cube.
        /// </summary>
        [BurstCompile]
        private struct CompactJob : IJob
        {
            public NativeList<Cluster> Clusters;
            public NativeList<Cube> Cubes;
            [WriteOnly] public NativeArray<float4x4> Matrices;
            [WriteOnly] public NativeArray<float4> Colors;
            public NativeReference<int> Alive;
            public NativeArray<LyingCube> Lying;
            [WriteOnly] public NativeArray<float4x4> LyingMatrices;
            [WriteOnly] public NativeArray<float4> LyingColors;
            /// <summary>Cell slots: a new cube's cell and an overwritten cube's cell are marked dirty.</summary>
            public NativeArray<LyingCell> Cells;
            /// <summary>Cell coordinate to its slot.</summary>
            public NativeParallelHashMap<int2, int> CellIndex;
            /// <summary>Slots given back by the main thread: a new cell takes one of these first.</summary>
            public NativeList<int> FreeCells;
            public NativeReference<LyingState> State;
            public float Time;

            public void Execute()
            {
                int count = Clusters.Length;
                var remap = new NativeArray<int>(count, Allocator.Temp);
                var settled = new NativeArray<Cluster>(count, Allocator.Temp);
                var groups = new NativeArray<int>(count, Allocator.Temp);
                // Per settled clump: its first cube's world position, which the grid of its cubes goes through.
                var anchors = new NativeArray<float3>(count, Allocator.Temp);
                var anchored = new NativeArray<bool>(count, Allocator.Temp);
                LyingState state = State.Value;
                int clusters = 0;
                for (int i = 0; i < count; i++)
                {
                    Cluster cluster = Clusters[i];
                    // Dust never lies: it shrinks away in flight.
                    if (cluster.Resting != 0 && cluster.Still >= SETTLE_TIME && cluster.Count > 0 &&
                        (cluster.Flags & DUST) == 0)
                    {
                        settled[i] = cluster;
                        groups[i] = state.NextGroup++;
                        remap[i] = SETTLED;
                        continue;
                    }

                    bool alive = cluster.Life > 0f && cluster.Count > 0;
                    remap[i] = alive ? clusters : -1;
                    if (alive)
                        Clusters[clusters++] = cluster;
                }

                int capacity = Lying.Length;
                int cubes = 0;
                for (int i = 0; i < Cubes.Length; i++)
                {
                    Cube cube = Cubes[i];
                    int owner = remap[cube.Cluster];
                    if (owner == SETTLED)
                    {
                        Cluster from = settled[cube.Cluster];
                        if (!anchored[cube.Cluster])
                        {
                            anchored[cube.Cluster] = true;
                            anchors[cube.Cluster] = from.Position + math.mul(from.Rotation, cube.Offset);
                        }
                        Lie(from, cube, groups[cube.Cluster], anchors[cube.Cluster], capacity, ref state);
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

                State.Value = state;
                Clusters.ResizeUninitialized(clusters);
                Cubes.ResizeUninitialized(cubes);
                Alive.Value = cubes;
            }

            /// <summary>
            /// One cube of a settled clump into the store: a free slot, or when full the next to overwrite.
            /// It joins its cell (marked dirty) and is drawn instanced (PENDING) until the cell is rebuilt.
            /// </summary>
            private void Lie(in Cluster settledCluster, in Cube cube, int group, float3 anchor, int capacity, ref LyingState state)
            {
                int slot;
                if (state.Count < capacity)
                    slot = state.Count++;
                else
                {
                    slot = state.Overwrite;
                    state.Overwrite = (state.Overwrite + 1) % capacity;

                    // The cube overwritten may have been sinking: it is not any more, and no longer counts.
                    LyingCube old = Lying[slot];
                    if (old.Sinking > 0f)
                    {
                        state.Sinking = math.max(0, state.Sinking - 1);
                        if ((old.Flags & EVICTED) != 0)
                            state.Evicting = math.max(0, state.Evicting - 1);
                    }
                    if ((old.Flags & PENDING) != 0)
                        state.Unbaked = math.max(0, state.Unbaked - 1);
                    LeaveCell(Cells, old.Cell, old.Sinking <= 0f);
                }

                float3 position = settledCluster.Position + math.mul(settledCluster.Rotation, cube.Offset);
                int cell = TakeCell(position, ref state);
                float3 scale = (settledCluster.Flags & LAID) != 0 ? new float3(SPLAT_WIDTH, SPLAT_HEIGHT, SPLAT_WIDTH) : new float3(1f);
                var lying = new LyingCube
                {
                    Position = position,
                    Rotation = settledCluster.Rotation,
                    Scale = scale * cube.Size,
                    Size = cube.Size,
                    Color = cube.Color,
                    Group = group,
                    Born = Time,
                    SinkTime = SINK_TIME,
                    Flags = (settledCluster.Flags & LAID) | PENDING,
                    Anchor = anchor,
                    Cell = cell
                };
                Lying[slot] = lying;
                LyingMatrices[slot] = float4x4.TRS(lying.Position, lying.Rotation, lying.Scale);
                LyingColors[slot] = lying.Color;
                state.Unbaked++;
            }

            /// <summary>
            /// The slot of the cell under <paramref name="position"/>, taken now if new - a given-back slot
            /// first, then an unused one; -1 when every slot is taken.
            /// </summary>
            private int TakeCell(float3 position, ref LyingState state)
            {
                int2 coord = CellOf(position);
                if (!CellIndex.TryGetValue(coord, out int cell))
                {
                    if (FreeCells.Length > 0)
                    {
                        cell = FreeCells[FreeCells.Length - 1];
                        FreeCells.RemoveAt(FreeCells.Length - 1);
                    }
                    else if (state.Cells < Cells.Length)
                        cell = state.Cells++;
                    else
                        return -1;
                    CellIndex.TryAdd(coord, cell);
                    Cells[cell] = new LyingCell { Coord = coord };
                }

                LyingCell c = Cells[cell];
                c.Cubes++;
                c.Dirty = 1;
                Cells[cell] = c;
                return cell;
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
    }
}

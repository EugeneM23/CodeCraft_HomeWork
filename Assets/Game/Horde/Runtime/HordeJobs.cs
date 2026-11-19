using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.Horde
{
    // Burst jobs of the horde: navigation (grid rays, flow field, A*) and the crowd step (spatial
    // hash, steering, contacts, totals, render prep).
    // CompileSynchronously: the editor would otherwise run the first calls as plain C# while Burst
    // compiles in the background, and a load-time BuildAll would take ten times longer.

    /// <summary>One downward ray from <c>RayTop</c> over the centre of every cell of the dirty rects.</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeBuildRaysJob : IJob
    {
        [ReadOnly] public NativeArray<int4> Rects;
        [WriteOnly] public NativeArray<int> Cells;
        [WriteOnly] public NativeArray<RaycastCommand> Commands;
        public PhysicsScene Scene;
        public QueryParameters Query;
        public float2 Origin;
        public int Width;
        public float CellSize;
        public float RayTop;
        public float RayLength;

        public void Execute()
        {
            int n = 0;
            for (int r = 0; r < Rects.Length; r++)
            {
                int4 rect = Rects[r];
                for (int z = rect.y; z <= rect.w; z++)
                for (int x = rect.x; x <= rect.z; x++)
                {
                    float2 xz = Origin + (new float2(x, z) + 0.5f) * CellSize;
                    Cells[n] = z * Width + x;
                    Commands[n] = new RaycastCommand(Scene, new Vector3(xz.x, RayTop, xz.y), Vector3.down, Query, RayLength);
                    n++;
                }
            }
        }
    }

    /// <summary>Hit height, or NaN where the ray found nothing.</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeReadHitsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RaycastHit> Hits;
        [WriteOnly] public NativeArray<float> Results;

        public void Execute(int i)
        {
            RaycastHit hit = Hits[i];
            float3 normal = hit.normal;
            Results[i] = math.lengthsq(normal) > 0.5f ? hit.point.y : HordeNavGrid.NO_GROUND;
        }
    }

    /// <summary>Copies the read heights into the grid and counts the ones that moved.</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeApplyHeightsJob : IJob
    {
        private const float EPSILON = 0.001f;

        [ReadOnly] public NativeArray<int> Cells;
        [ReadOnly] public NativeArray<float> Results;
        public NativeArray<float> Heights;
        public NativeArray<int> Changed;
        public int Count;

        public void Execute()
        {
            int changed = 0;
            for (int i = 0; i < Count; i++)
            {
                int cell = Cells[i];
                float before = Heights[cell];
                float after = Results[i];
                bool same = math.isnan(before) ? math.isnan(after) : math.abs(before - after) <= EPSILON;
                if (same)
                    continue;

                Heights[cell] = after;
                changed++;
            }

            Changed[0] = changed;
        }
    }

    /// <summary>
    /// Binary min-heap of cell indices keyed by an int array, with a position index for decrease-key.
    /// <c>Pos</c>: -1 not seen, -2 closed, otherwise the slot in <c>Items</c>.
    /// </summary>
    internal struct HordeCellHeap
    {
        public const int NOT_SEEN = -1;
        public const int CLOSED = -2;

        public NativeArray<int> Items;
        public NativeArray<int> Pos;
        public NativeArray<int> Key;
        public int Count;

        public void Push(int cell)
        {
            Items[Count] = cell;
            Pos[cell] = Count;
            Count++;
            SiftUp(Count - 1);
        }

        /// <summary>The key of a queued cell went down.</summary>
        public void Decrease(int cell) => SiftUp(Pos[cell]);

        public int Pop()
        {
            int top = Items[0];
            Pos[top] = CLOSED;
            Count--;
            if (Count > 0)
            {
                int last = Items[Count];
                Items[0] = last;
                Pos[last] = 0;
                SiftDown(0);
            }

            return top;
        }

        private void SiftUp(int slot)
        {
            int cell = Items[slot];
            int key = Key[cell];
            while (slot > 0)
            {
                int parent = (slot - 1) >> 1;
                int other = Items[parent];
                if (Key[other] <= key)
                    break;
                Items[slot] = other;
                Pos[other] = slot;
                slot = parent;
            }

            Items[slot] = cell;
            Pos[cell] = slot;
        }

        private void SiftDown(int slot)
        {
            int cell = Items[slot];
            int key = Key[cell];
            while (true)
            {
                int child = slot * 2 + 1;
                if (child >= Count)
                    break;
                if (child + 1 < Count && Key[Items[child + 1]] < Key[Items[child]])
                    child++;
                int other = Items[child];
                if (Key[other] >= key)
                    break;
                Items[slot] = other;
                Pos[other] = slot;
                slot = child;
            }

            Items[slot] = cell;
            Pos[cell] = slot;
        }
    }

    /// <summary>
    /// Step rule and 8-neighbour table shared by the grid and the jobs: 4 orthogonal (cost 10), then
    /// 4 diagonal (cost 14). Its own static class, so Burst never runs the grid's static constructor.
    /// </summary>
    internal static class HordeNavRules
    {
        public const int COUNT = 8;
        public const int ORTHOGONAL_COST = 10;
        public const int DIAGONAL_COST = 14;

        /// <summary>Heights close enough to step between. False when either is NaN (no ground).</summary>
        public static bool Steppable(float a, float b, float maxStep) => math.abs(a - b) <= maxStep;

        /// <summary>
        /// The step rule on raw heights: move from <paramref name="a"/> by <paramref name="d"/> (each axis
        /// -1..1). A diagonal also needs both cells it squeezes past to connect to both ends.
        /// </summary>
        public static bool CanMove(NativeArray<float> heights, int2 size, float maxStep, int2 a, int2 d)
        {
            int2 b = a + d;
            if (math.any(b < 0) || math.any(b >= size))
                return false;

            float ha = heights[a.y * size.x + a.x];
            float hb = heights[b.y * size.x + b.x];
            if (!Steppable(ha, hb, maxStep))
                return false;
            if (d.x == 0 || d.y == 0)
                return true;

            float hx = heights[a.y * size.x + b.x];
            float hz = heights[b.y * size.x + a.x];
            return Steppable(ha, hx, maxStep) && Steppable(hx, hb, maxStep)
                && Steppable(ha, hz, maxStep) && Steppable(hz, hb, maxStep);
        }

        public static int2 Offset(int i)
        {
            switch (i)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(-1, 0);
                case 2: return new int2(0, 1);
                case 3: return new int2(0, -1);
                case 4: return new int2(1, 1);
                case 5: return new int2(-1, 1);
                case 6: return new int2(1, -1);
                default: return new int2(-1, -1);
            }
        }

        public static int Cost(int i) => i < 4 ? ORTHOGONAL_COST : DIAGONAL_COST;

        /// <summary>Inverse of <see cref="Offset"/> for a non-zero offset.</summary>
        public static int DirectionIndex(int2 d)
        {
            if (d.y == 0) return d.x > 0 ? 0 : 1;
            if (d.x == 0) return d.y > 0 ? 2 : 3;
            if (d.y > 0) return d.x > 0 ? 4 : 5;
            return d.x > 0 ? 6 : 7;
        }

        /// <summary>Octile distance in the same 10/14 units.</summary>
        public static int Octile(int2 a, int2 b)
        {
            int2 d = math.abs(a - b);
            return ORTHOGONAL_COST * math.cmax(d) + (DIAGONAL_COST - ORTHOGONAL_COST) * math.cmin(d);
        }
    }

    /// <summary>
    /// Per cell, a bit for each of the 8 directions a walker may move in (bit n = <see cref="HordeNavRules.Offset"/>(n)).
    /// The step rule is symmetric, so the same bits say who may come in. Computed once per grid
    /// snapshot; the searches then test bits instead of comparing heights.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeEdgeMaskJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights;
        [WriteOnly] public NativeArray<byte> Edges;
        public int2 Size;
        public float MaxStep;

        public void Execute(int index)
        {
            int2 cell = new int2(index % Size.x, index / Size.x);
            int mask = 0;
            for (int n = 0; n < HordeNavRules.COUNT; n++)
                if (HordeNavRules.CanMove(Heights, Size, MaxStep, cell, HordeNavRules.Offset(n)))
                    mask |= 1 << n;
            Edges[index] = (byte)mask;
        }
    }

    /// <summary>
    /// Dijkstra from the target cell (and the walkable ground round it): cost of every cell to the target. Edge costs are only 10 and 14,
    /// so the queue is Dial's: 15 buckets in a ring, each an intrusive doubly linked list through
    /// <c>Next</c>/<c>Prev</c> (a cell whose cost drops is moved to its new bucket), O(1) per step.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeFlowDistanceJob : IJob
    {
        private const int BUCKETS = HordeNavRules.DIAGONAL_COST + 1;
        private const int NONE = -1;
        private const int OUTSIDE = -2;

        [ReadOnly] public NativeArray<byte> Edges;
        public NativeArray<int> Distance;
        public NativeArray<int> Next;
        public NativeArray<int> Prev;
        public int2 Size;
        public int2 Target;
        /// <summary>Walkable cells within this many cells of the target are targets too (the ground under the hull).</summary>
        public int SeedRadius;
        /// <summary>Nothing walkable was seeded: the nearest walkable cell within this many cells is the target instead.</summary>
        public int FallbackRadius;

        public void Execute()
        {
            for (int i = 0; i < Distance.Length; i++)
            {
                Distance[i] = int.MaxValue;
                Prev[i] = OUTSIDE;
            }

            var heads = new NativeArray<int>(BUCKETS, Allocator.Temp);
            for (int b = 0; b < BUCKETS; b++)
                heads[b] = NONE;

            int target = Target.y * Size.x + Target.x;
            Distance[target] = 0;
            Link(ref heads, target, 0);
            int queued = 1;
            bool seeded = Edges[target] != 0;

            // The target cell may be a hole or a rubble top no one can step onto: seed from the walkable ground round it.
            int radiusSq = SeedRadius * SeedRadius;
            for (int dz = -SeedRadius; dz <= SeedRadius; dz++)
            for (int dx = -SeedRadius; dx <= SeedRadius; dx++)
            {
                if (dx * dx + dz * dz > radiusSq)
                    continue;
                int index = Seedable(Target + new int2(dx, dz));
                if (index < 0)
                    continue;
                Distance[index] = 0;
                Link(ref heads, index, 0);
                queued++;
                seeded = true;
            }

            if (!seeded)
            {
                int best = -1;
                int bestSq = int.MaxValue;
                for (int dz = -FallbackRadius; dz <= FallbackRadius; dz++)
                for (int dx = -FallbackRadius; dx <= FallbackRadius; dx++)
                {
                    int sq = dx * dx + dz * dz;
                    if (sq >= bestSq || sq > FallbackRadius * FallbackRadius)
                        continue;
                    int index = Seedable(Target + new int2(dx, dz));
                    if (index < 0)
                        continue;
                    best = index;
                    bestSq = sq;
                }

                if (best >= 0)
                {
                    Distance[best] = 0;
                    Link(ref heads, best, 0);
                    queued++;
                }
            }

            for (int cost = 0; queued > 0; cost++)
            {
                int bucket = cost % BUCKETS;
                while (heads[bucket] != NONE)
                {
                    int index = heads[bucket];
                    Unlink(ref heads, index, bucket);
                    queued--;

                    int mask = Edges[index];
                    if (mask == 0)
                        continue;

                    int2 cell = new int2(index % Size.x, index / Size.x);
                    for (int n = 0; n < HordeNavRules.COUNT; n++)
                    {
                        if ((mask & (1 << n)) == 0)
                            continue;

                        int2 next = cell + HordeNavRules.Offset(n);
                        int nextIndex = next.y * Size.x + next.x;
                        int nextCost = cost + HordeNavRules.Cost(n);
                        int old = Distance[nextIndex];
                        if (nextCost >= old)
                            continue;

                        if (Prev[nextIndex] != OUTSIDE)
                            Unlink(ref heads, nextIndex, old % BUCKETS);
                        else
                            queued++;
                        Distance[nextIndex] = nextCost;
                        Link(ref heads, nextIndex, nextCost % BUCKETS);
                    }
                }
            }

            heads.Dispose();
        }

        /// <summary>Index of a cell in the grid with a walkable way out that is not a target yet, else -1.</summary>
        private int Seedable(int2 cell)
        {
            if (math.any(cell < 0) || math.any(cell >= Size))
                return -1;
            int index = cell.y * Size.x + cell.x;
            return Edges[index] != 0 && Distance[index] != 0 ? index : -1;
        }

        private void Link(ref NativeArray<int> heads, int index, int bucket)
        {
            int head = heads[bucket];
            Next[index] = head;
            Prev[index] = NONE;
            if (head != NONE)
                Prev[head] = index;
            heads[bucket] = index;
        }

        private void Unlink(ref NativeArray<int> heads, int index, int bucket)
        {
            int prev = Prev[index];
            int next = Next[index];
            if (prev == NONE)
                heads[bucket] = next;
            else
                Next[prev] = next;
            if (next != NONE)
                Prev[next] = prev;
            Prev[index] = OUTSIDE;
        }
    }

    /// <summary>Per cell, the unit direction toward its cheapest walkable neighbour.</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeFlowDirectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<byte> Edges;
        [ReadOnly] public NativeArray<int> Distance;
        [WriteOnly] public NativeArray<float2> Direction;
        public int2 Size;

        public void Execute(int index)
        {
            int best = Distance[index];
            if (best == int.MaxValue || best == 0)
            {
                Direction[index] = float2.zero;
                return;
            }

            int mask = Edges[index];
            int2 cell = new int2(index % Size.x, index / Size.x);
            int2 bestOffset = int2.zero;
            for (int n = 0; n < HordeNavRules.COUNT; n++)
            {
                if ((mask & (1 << n)) == 0)
                    continue;
                int2 next = cell + HordeNavRules.Offset(n);
                int cost = Distance[next.y * Size.x + next.x];
                if (cost < best)
                {
                    best = cost;
                    bestOffset = HordeNavRules.Offset(n);
                }
            }

            Direction[index] = math.normalizesafe((float2)bestOffset);
        }
    }

    /// <summary>
    /// A* from start to goal over the step rule, then the cell path pulled tight by grid line of sight.
    /// Per-cell arrays are valid only where <c>Stamp</c> equals <c>Generation</c>, so nothing is cleared
    /// between searches. When the goal is cut off, the path leads to the reached cell closest to it.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeAStarJob : IJob
    {
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<byte> Edges;
        public NativeArray<int> G;
        public NativeArray<int> F;
        public NativeArray<int> Parent;
        public NativeArray<int> Stamp;
        public NativeArray<int> HeapItems;
        public NativeArray<int> HeapPos;
        public NativeList<int> Cells;
        public NativeList<float3> Path;
        /// <summary>[0] status (<see cref="HordePathStatus"/>), [1] expanded cells.</summary>
        public NativeArray<int> Result;
        public int Generation;
        public int2 Size;
        public float2 Origin;
        public float CellSize;
        public int2 Start;
        public int2 Goal;
        public float3 StartPosition;
        public float3 GoalPosition;

        public void Execute()
        {
            Cells.Clear();
            Path.Clear();
            Result[1] = 0;

            int start = Index(Start);
            int goal = Index(Goal);
            if (math.isnan(Heights[start]))
            {
                Result[0] = (int)HordePathStatus.Failed;
                return;
            }

            var heap = new HordeCellHeap { Items = HeapItems, Pos = HeapPos, Key = F };
            Touch(start);
            G[start] = 0;
            F[start] = HordeNavRules.Octile(Start, Goal);
            Parent[start] = -1;
            heap.Push(start);

            int closest = start;
            int closestH = F[start];
            int expanded = 0;
            bool found = false;

            while (heap.Count > 0)
            {
                int index = heap.Pop();
                expanded++;
                if (index == goal)
                {
                    found = true;
                    break;
                }

                int2 cell = new int2(index % Size.x, index / Size.x);
                int h = HordeNavRules.Octile(cell, Goal);
                if (h < closestH)
                {
                    closestH = h;
                    closest = index;
                }

                int g = G[index];
                int mask = Edges[index];
                for (int n = 0; n < HordeNavRules.COUNT; n++)
                {
                    int2 d = HordeNavRules.Offset(n);
                    if ((mask & (1 << n)) == 0)
                        continue;

                    int2 next = cell + d;
                    int nextIndex = Index(next);
                    Touch(nextIndex);
                    int pos = HeapPos[nextIndex];
                    if (pos == HordeCellHeap.CLOSED)
                        continue;

                    int nextG = g + HordeNavRules.Cost(n);
                    if (nextG >= G[nextIndex])
                        continue;

                    G[nextIndex] = nextG;
                    F[nextIndex] = nextG + HordeNavRules.Octile(next, Goal);
                    Parent[nextIndex] = index;
                    if (pos == HordeCellHeap.NOT_SEEN)
                        heap.Push(nextIndex);
                    else
                        heap.Decrease(nextIndex);
                }
            }

            Result[1] = expanded;
            int end = found ? goal : closest;
            Result[0] = (int)(found ? HordePathStatus.Found : HordePathStatus.Partial);

            for (int c = end; c != -1; c = Parent[c])
                Cells.Add(c);
            Reverse();
            Pull(found);
        }

        private void Touch(int index)
        {
            if (Stamp[index] == Generation)
                return;
            Stamp[index] = Generation;
            G[index] = int.MaxValue;
            HeapPos[index] = HordeCellHeap.NOT_SEEN;
        }

        private void Reverse()
        {
            for (int i = 0, j = Cells.Length - 1; i < j; i++, j--)
            {
                int t = Cells[i];
                Cells[i] = Cells[j];
                Cells[j] = t;
            }
        }

        /// <summary>Greedy string pulling: keep a waypoint only where the straight line breaks.</summary>
        private void Pull(bool found)
        {
            int count = Cells.Length;
            Path.Add(new float3(StartPosition.x, Heights[Cells[0]], StartPosition.z));
            if (count == 1)
            {
                if (found)
                    Path.Add(new float3(GoalPosition.x, Heights[Cells[0]], GoalPosition.z));
                return;
            }

            int anchor = 0;
            for (int j = 2; j < count; j++)
            {
                if (LineWalkable(Cell(Cells[anchor]), Cell(Cells[j])))
                    continue;
                anchor = j - 1;
                Path.Add(Center(Cells[anchor]));
            }

            int last = Cells[count - 1];
            Path.Add(found ? new float3(GoalPosition.x, Heights[last], GoalPosition.z) : Center(last));
        }

        /// <summary>
        /// Walks the cells the segment between two cell centres crosses; every step must obey the step
        /// rule. A segment through a corner exactly counts as a diagonal step.
        /// </summary>
        private bool LineWalkable(int2 a, int2 b)
        {
            int2 delta = b - a;
            int2 sign = (int2)math.sign(delta);
            int2 n = math.abs(delta);
            int2 cur = a;
            int ix = 0;
            int iy = 0;
            while (ix < n.x || iy < n.y)
            {
                long lhs = (long)(1 + 2 * ix) * n.y;
                long rhs = (long)(1 + 2 * iy) * n.x;
                int2 step;
                if (lhs == rhs)
                {
                    step = sign;
                    ix++;
                    iy++;
                }
                else if (lhs < rhs)
                {
                    step = new int2(sign.x, 0);
                    ix++;
                }
                else
                {
                    step = new int2(0, sign.y);
                    iy++;
                }

                if ((Edges[Index(cur)] & (1 << HordeNavRules.DirectionIndex(step))) == 0)
                    return false;
                cur += step;
            }

            return true;
        }

        private int Index(int2 cell) => cell.y * Size.x + cell.x;

        private int2 Cell(int index) => new int2(index % Size.x, index / Size.x);

        private float3 Center(int index)
        {
            float2 xz = Origin + ((float2)Cell(index) + 0.5f) * CellSize;
            return new float3(xz.x, Heights[index], xz.y);
        }
    }

    // ====================================================================== crowd step

    /// <summary>
    /// Read-only view of the nav grid for the crowd jobs: heights (the grid's own array, never
    /// written while a step runs) and the readable flow field directions.
    /// </summary>
    internal struct HordeGridView
    {
        [ReadOnly] public NativeArray<float> Heights;
        [ReadOnly] public NativeArray<float2> Flow;
        public int2 Size;
        public float2 Origin;
        public float CellSize;
        public float MaxStep;

        public int2 CellOf(float2 xz) => (int2)math.floor((xz - Origin) / CellSize);

        public bool InBounds(int2 cell) => math.all(cell >= 0) && math.all(cell < Size);

        public float HeightOf(int2 cell) => Heights[cell.y * Size.x + cell.x];

        public float2 FlowOf(int2 cell) => InBounds(cell) ? Flow[cell.y * Size.x + cell.x] : float2.zero;

        /// <summary>Same as <see cref="HordeNavGrid.HeightAt"/>: bilinear over steppable neighbours; NaN off the grid or over a hole.</summary>
        public float HeightAt(float2 xz)
        {
            int2 own = CellOf(xz);
            if (!InBounds(own))
                return HordeNavGrid.NO_GROUND;
            float h0 = HeightOf(own);
            if (math.isnan(h0))
                return HordeNavGrid.NO_GROUND;

            float2 p = (xz - Origin) / CellSize - 0.5f;
            int2 c0 = (int2)math.floor(p);
            float2 f = p - c0;
            float h00 = Sample(c0, h0);
            float h10 = Sample(c0 + new int2(1, 0), h0);
            float h01 = Sample(c0 + new int2(0, 1), h0);
            float h11 = Sample(c0 + new int2(1, 1), h0);
            return math.lerp(math.lerp(h00, h10, f.x), math.lerp(h01, h11, f.x), f.y);
        }

        /// <summary>
        /// A walker in <paramref name="from"/> may move into the neighbour <paramref name="to"/>: the
        /// grid's step rule, plus a straight drop down a ledge, plus any move out of a cell with no
        /// ground (so nobody stays stuck where the ground vanished). Never into a cell with no ground.
        /// </summary>
        public bool CanEnter(int2 from, int2 to)
        {
            if (!InBounds(to))
                return false;
            float ht = HeightOf(to);
            if (math.isnan(ht))
                return false;
            if (!InBounds(from))
                return true;
            int2 d = to - from;
            if (math.any(math.abs(d) > 1))
                return false;
            float hf = HeightOf(from);
            if (math.isnan(hf))
                return true;
            if (HordeNavRules.CanMove(Heights, Size, MaxStep, from, d))
                return true;
            return ht < hf - MaxStep && (d.x == 0 || d.y == 0);
        }

        /// <summary>
        /// A walker at <paramref name="from"/> may be put at <paramref name="to"/> (a short shove): every cell on the
        /// way is entered by <see cref="CanEnter"/>, so a shove never lands on a roof or in a wall.
        /// </summary>
        public bool CanTravel(float2 from, float2 to)
        {
            int2 cell = CellOf(from);
            int2 end = CellOf(to);
            for (int guard = 0; guard < 8 && !math.all(cell == end); guard++)
            {
                int2 next = cell + math.clamp(end - cell, -1, 1);
                if (!CanEnter(cell, next))
                    return false;
                cell = next;
            }

            return math.all(cell == end);
        }

        /// <summary>
        /// Nothing on the grid stands above the straight line from <paramref name="from"/> to <paramref name="to"/>
        /// (sampled every cell; the end cells are left out: the shooter's own and the target's).
        /// </summary>
        public bool LineOfSight(float3 from, float3 to)
        {
            float2 d = to.xz - from.xz;
            int steps = math.min((int)math.ceil(math.length(d) / CellSize), 256);
            for (int s = 1; s < steps - 1; s++)
            {
                float t = (float)s / steps;
                int2 cell = CellOf(from.xz + d * t);
                if (!InBounds(cell))
                    continue;
                float h = HeightOf(cell);
                if (!math.isnan(h) && h > math.lerp(from.y, to.y, t))
                    return false;
            }

            return true;
        }

        private float Sample(int2 cell, float h0)
        {
            if (!InBounds(cell))
                return h0;
            float h = HeightOf(cell);
            return HordeNavRules.Steppable(h, h0, MaxStep) ? h : h0;
        }
    }

    /// <summary>Where a point is relative to the tank's hull box.</summary>
    internal static class HordeHull
    {
        public static float3 ToLocal(in HordeTank tank, quaternion inverse, float3 p) => math.rotate(inverse, p - tank.Center);

        /// <summary>Signed XZ distance from a local point to the box (negative inside).</summary>
        public static float Distance(in HordeTank tank, float3 local)
        {
            float2 q = math.abs(local.xz) - tank.HalfExtents.xz;
            return math.length(math.max(q, 0f)) + math.min(math.max(q.x, q.y), 0f);
        }
    }

    /// <summary>
    /// Counting sort of the live monsters into <see cref="HordeSimulation.HASH_CELL"/> cells: <c>CellStart[c]..CellStart[c+1]</c> are
    /// the slots of <c>Sorted</c> holding the monsters of cell c. Monsters off the hash area go to its border.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeHashJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Position;
        [ReadOnly] public NativeArray<HordeState> State;
        public NativeArray<int> CellStart;
        public NativeArray<int> Cursor;
        public NativeArray<int> CellIndex;
        public NativeArray<int> Sorted;
        public int2 HashSize;
        public float2 HashOrigin;
        public float HashCell;
        public ProfilerMarker Marker;

        public void Execute()
        {
            Marker.Begin();
            for (int c = 0; c < CellStart.Length; c++)
                CellStart[c] = 0;

            for (int i = 0; i < Position.Length; i++)
            {
                // Riders are up on the tank: the crowd neither pushes them nor is pushed by them.
                if (!HordeStates.IsLive(State[i]) || State[i] == HordeState.Riding)
                {
                    CellIndex[i] = -1;
                    continue;
                }

                int2 cell = math.clamp((int2)math.floor((Position[i].xz - HashOrigin) / HashCell), 0, HashSize - 1);
                int index = cell.y * HashSize.x + cell.x;
                CellIndex[i] = index;
                CellStart[index + 1]++;
            }

            for (int c = 0; c < Cursor.Length; c++)
            {
                CellStart[c + 1] += CellStart[c];
                Cursor[c] = CellStart[c];
            }

            for (int i = 0; i < Position.Length; i++)
            {
                int index = CellIndex[i];
                if (index < 0)
                    continue;
                Sorted[Cursor[index]] = i;
                Cursor[index]++;
            }

            Marker.End();
        }
    }

    /// <summary>
    /// Per monster: the wish plus separation from neighbours in the hash cells within the separation radius; velocity
    /// eases toward it, the heading turns at a bounded rate. The move obeys the grid step rule (sliding along a blocked
    /// axis), and the feet go to the surface height. Writes <c>NextPosition</c>.
    /// Melee wish: the flow field toward the tank, straight at it when close, slower near the hull. Ranged wish: away
    /// from the tank inside <see cref="HordeKind.BackOff"/>; standing within <see cref="HordeKind.StopDistance"/> with the hull
    /// in sight; else in with the crowd over the flow field.
    /// Emerging monsters stand.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeSteerJob : IJobParallelForBatch
    {
        private const float DIRECT_RANGE = 3f;
        private const float GOLDEN_ANGLE = 2.39996323f;
        private const float ANIM_WRAP = 600f;
        /// <summary>Mouth height over the feet at render scale 1 (the line of sight starts there).</summary>
        private const float MOUTH = 2f;

        [ReadOnly] public NativeArray<float3> Position;
        [ReadOnly] public NativeArray<HordeState> State;
        [ReadOnly] public NativeArray<float> SpeedScale;
        [ReadOnly] public NativeArray<byte> Kind;
        [ReadOnly] public NativeArray<HordeKind> Kinds;
        [ReadOnly] public NativeArray<int> CellStart;
        [ReadOnly] public NativeArray<int> Sorted;
        [WriteOnly] public NativeArray<float3> NextPosition;
        public NativeArray<float2> Velocity;
        public NativeArray<float> Heading;
        public NativeArray<float> AnimTime;
        public HordeGridView Grid;
        public int2 HashSize;
        public float2 HashOrigin;
        public float HashCell;
        public HordeSettings Settings;
        public HordeTank Tank;
        public float DeltaTime;
        public ProfilerMarker Marker;

        public void Execute(int startIndex, int count)
        {
            Marker.Begin();
            quaternion inverse = math.inverse(Tank.Rotation);
            float radius = Settings.SeparationRadius;
            float radiusSq = radius * radius;
            // Hash cells round the own one that a neighbour within the radius can be in (1 with the default settings).
            int rings = math.max(1, (int)math.ceil(radius / HashCell));
            float blend = 1f - math.exp(-Settings.Acceleration * DeltaTime);
            float maxTurn = Settings.TurnRate * DeltaTime;

            for (int i = startIndex; i < startIndex + count; i++)
            {
                HordeState state = State[i];
                float3 p = Position[i];
                if (state != HordeState.Walk && state != HordeState.Attack)
                {
                    NextPosition[i] = p;
                    if (state == HordeState.Emerging || state == HordeState.Riding)
                    {
                        Velocity[i] = float2.zero;
                        AnimTime[i] += DeltaTime;
                    }

                    continue;
                }

                HordeKind kind = Kinds[Kind[i]];
                float2 xz = p.xz;
                int2 cell = Grid.CellOf(xz);

                float2 wish = float2.zero;
                float2 toTank = float2.zero;
                if (Tank.Present)
                {
                    toTank = Tank.Center.xz - xz;
                    float speed = kind.Speed * SpeedScale[i];
                    if (kind.Tactic == HordeTactic.Ranged)
                        wish = RangedWish(p, cell, toTank, kind, speed);
                    else
                    {
                        float hull = HordeHull.Distance(Tank, HordeHull.ToLocal(Tank, inverse, p));
                        float2 dir = Grid.FlowOf(cell);
                        if (math.lengthsq(dir) < 0.25f || hull < DIRECT_RANGE)
                            dir = math.normalizesafe(toTank);
                        float slow = math.saturate((hull - Settings.AttackReach) / math.max(Settings.SlowRadius, 0.01f));
                        speed *= math.lerp(0.3f, 1f, slow);
                        if (state == HordeState.Attack)
                            speed = math.min(speed, 0.4f);
                        wish = dir * speed;
                    }
                }

                // Separation.
                float2 push = float2.zero;
                int seen = 0;
                int2 hashCell = math.clamp((int2)math.floor((xz - HashOrigin) / HashCell), 0, HashSize - 1);
                for (int dz = -rings; dz <= rings && seen < Settings.MaxNeighbours; dz++)
                for (int dx = -rings; dx <= rings && seen < Settings.MaxNeighbours; dx++)
                {
                    int2 c = hashCell + new int2(dx, dz);
                    if (math.any(c < 0) || math.any(c >= HashSize))
                        continue;
                    int ci = c.y * HashSize.x + c.x;
                    int end = CellStart[ci + 1];
                    for (int k = CellStart[ci]; k < end && seen < Settings.MaxNeighbours; k++)
                    {
                        int j = Sorted[k];
                        if (j == i)
                            continue;
                        float2 d = xz - Position[j].xz;
                        float d2 = math.lengthsq(d);
                        if (d2 >= radiusSq)
                            continue;
                        if (d2 < 1e-8f)
                        {
                            // Same spot: split by a direction of its own.
                            math.sincos(i * GOLDEN_ANGLE, out float s, out float co);
                            d = new float2(co, s) * 1e-3f;
                            d2 = 1e-6f;
                        }

                        float dist = math.sqrt(d2);
                        push += d / dist * (1f - dist / radius);
                        seen++;
                    }
                }

                wish += push * Settings.SeparationStrength;

                float2 v = math.lerp(Velocity[i], wish, blend);

                // Heading: face the way it goes, or the tank while standing.
                float heading = Heading[i];
                float2 face = math.lengthsq(v) > 0.04f ? v : toTank;
                if (math.lengthsq(face) > 1e-4f)
                {
                    float target = math.atan2(face.x, face.y);
                    float delta = target - heading;
                    delta -= math.floor((delta + math.PI) / (2f * math.PI)) * (2f * math.PI);
                    heading += math.clamp(delta, -maxTurn, maxTurn);
                    heading -= math.floor((heading + math.PI) / (2f * math.PI)) * (2f * math.PI);
                }

                // Move by the grid's step rule.
                float2 next = xz + v * DeltaTime;
                int2 nextCell = Grid.CellOf(next);
                if (!math.all(nextCell == cell) && !Grid.CanEnter(cell, nextCell))
                {
                    var alongX = new float2(next.x, xz.y);
                    var alongZ = new float2(xz.x, next.y);
                    int2 cellX = Grid.CellOf(alongX);
                    int2 cellZ = Grid.CellOf(alongZ);
                    if (math.all(cellX == cell) || Grid.CanEnter(cell, cellX))
                    {
                        next = alongX;
                        v.y = 0f;
                    }
                    else if (math.all(cellZ == cell) || Grid.CanEnter(cell, cellZ))
                    {
                        next = alongZ;
                        v.x = 0f;
                    }
                    else
                    {
                        next = xz;
                        v = float2.zero;
                    }
                }

                float y = Grid.HeightAt(next);
                NextPosition[i] = new float3(next.x, math.isnan(y) ? p.y : y, next.y);
                Velocity[i] = v;
                Heading[i] = heading;

                float rate = state == HordeState.Attack ? 1f : math.length(v) / math.max(Settings.RunClipSpeed * kind.RenderScale, 0.1f);
                float t = AnimTime[i] + DeltaTime * rate;
                AnimTime[i] = t > ANIM_WRAP ? t - ANIM_WRAP : t;
            }

            Marker.End();
        }

        private float2 RangedWish(float3 p, int2 cell, float2 toTank, in HordeKind kind, float speed)
        {
            float d = math.length(toTank);
            if (d < 1e-3f)
                return float2.zero;
            if (d < kind.BackOff)
                return -toTank / d * speed;
            // In range and the hull in sight: it stands and shoots. Otherwise it walks in with the crowd.
            if (d <= kind.StopDistance && Grid.LineOfSight(p + new float3(0f, MOUTH * kind.RenderScale * Settings.BodyScale, 0f), Tank.Center))
                return float2.zero;
            float2 dir = Grid.FlowOf(cell);
            if (math.lengthsq(dir) < 0.25f)
                dir = toTank / d;
            return dir * speed;
        }
    }

    /// <summary>
    /// Per monster after the move: direct hits and blasts of this step, then the tank. Under the hull of a tank at or
    /// above the crush speed (the box swept along this step's move, leading side only): crushed, if its kind is.
    /// Touching the hull: pushed out of it where the grid lets it go, and attacking (bite damage of this step into
    /// <c>AttackDamage</c>). Emerging monsters count down and take damage and crushes, nothing else. Ranged monsters
    /// fire a shot when their interval is up, the hull is in range and in sight. Deaths go to the queue.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeContactJob : IJobParallelForBatch
    {
        /// <summary>Feet may be this far below the hull box and still be under it.</summary>
        private const float BELOW_HULL = 1.2f;
        /// <summary>Blasts measure from this height over the feet.</summary>
        private const float CHEST = 0.8f;
        /// <summary>Mouth height over the feet at render scale 1 (the shot starts there).</summary>
        private const float MOUTH = 2f;


        public NativeArray<float3> Position;
        public NativeArray<HordeState> State;
        public NativeArray<float> Health;
        public NativeArray<float> StateTime;
        public NativeArray<float> Cooldown;
        [ReadOnly] public NativeArray<float> MaxHealth;
        [WriteOnly] public NativeArray<float> AttackDamage;
        [ReadOnly] public NativeArray<float> Heading;
        [ReadOnly] public NativeArray<byte> Kind;
        [ReadOnly] public NativeArray<float> DamageScale;
        [ReadOnly] public NativeArray<HordeKind> Kinds;
        [ReadOnly] public NativeArray<HordeAreaDamage> Areas;
        public NativeQueue<HordeDeathEvent>.ParallelWriter Deaths;
        public NativeQueue<HordeShot>.ParallelWriter Shots;
        public HordeGridView Grid;
        public HordeSettings Settings;
        public HordeTank Tank;
        public float DeltaTime;
        public ProfilerMarker Marker;

        public void Execute(int startIndex, int count)
        {
            Marker.Begin();
            quaternion inverse = math.inverse(Tank.Rotation);
            float2 flatVelocity = Tank.Velocity.xz;
            bool crushing = math.length(flatVelocity) >= Settings.CrushSpeed;
            float2 localVelocity = math.rotate(inverse, new float3(flatVelocity.x, 0f, flatVelocity.y)).xz;
            float2 sweep = localVelocity * DeltaTime;

            for (int i = startIndex; i < startIndex + count; i++)
            {
                AttackDamage[i] = 0f;
                HordeState state = State[i];
                if (!HordeStates.IsLive(state))
                    continue;

                HordeKind kind = Kinds[Kind[i]];
                float r = kind.Radius;
                float3 p = Position[i];
                float hp = Health[i];
                if (hp <= 0f)
                {
                    Die(i, p, HordeDeathCause.Damaged);
                    continue;
                }

                float2 shove = float2.zero;
                for (int a = 0; a < Areas.Length; a++)
                {
                    HordeAreaDamage area = Areas[a];
                    float dist = math.distance(p + new float3(0f, CHEST * kind.RenderScale * Settings.BodyScale, 0f), area.Center);
                    if (dist >= area.Radius + r)
                        continue;
                    float edge = math.saturate(dist / area.Radius);
                    hp -= area.Damage * math.lerp(1f, area.MinFactor, edge);
                    if (area.Push > 0f)
                        shove += math.normalizesafe(p.xz - area.Center.xz, new float2(0f, 1f)) * (area.Push * (1f - edge));
                }

                if (state != HordeState.Riding && math.lengthsq(shove) > 1e-4f)
                {
                    float2 to = p.xz + shove;
                    if (Grid.CanTravel(p.xz, to))
                    {
                        float y = Grid.HeightAt(to);
                        p = new float3(to.x, math.isnan(y) ? p.y : y, to.y);
                        Position[i] = p;
                    }
                }

                Health[i] = hp;
                if (hp <= 0f)
                {
                    Die(i, p, HordeDeathCause.Explosion);
                    continue;
                }

                // A rider is placed from outside (HordeRiders): hits and blasts only, no crush, shove, bite or shot here.
                if (state == HordeState.Riding)
                    continue;

                bool emerging = state == HordeState.Emerging;
                if (emerging)
                {
                    float left = StateTime[i] - DeltaTime;
                    StateTime[i] = math.max(left, 0f);
                    if (left <= 0f)
                    {
                        state = HordeState.Walk;
                        emerging = false;
                    }
                }
                else
                {
                    state = HordeState.Walk;
                }

                if (Tank.Present)
                {
                    float3 local = HordeHull.ToLocal(Tank, inverse, p);
                    float2 q = math.abs(local.xz) - Tank.HalfExtents.xz;
                    bool level = local.y < Tank.HalfExtents.y && local.y > -Tank.HalfExtents.y - BELOW_HULL;
                    bool crushable = kind.Crushable && (kind.CrushBelowHealth <= 0f || hp <= kind.CrushBelowHealth * MaxHealth[i]);
                    if (level && crushing && crushable && InCrushZone(local.xz, q, sweep, localVelocity, r, Tank.HalfExtents.xz))
                    {
                        Die(i, p, HordeDeathCause.Crushed);
                        continue;
                    }

                    if (!emerging)
                    {
                        if (level && q.x < r && q.y < r)
                        {
                            // Overlaps the hull: out along the shallower side, else the other one, if the grid lets it
                            // go there (not up on a roof, not into a wall); else it stays where it is.
                            float penX = r - q.x;
                            float penZ = r - q.y;
                            float3 alongX = local;
                            alongX.x += math.select(-penX, penX, local.x >= 0f);
                            float3 alongZ = local;
                            alongZ.z += math.select(-penZ, penZ, local.z >= 0f);
                            float3 first = penX < penZ ? alongX : alongZ;
                            float3 second = penX < penZ ? alongZ : alongX;
                            float2 to = (math.rotate(Tank.Rotation, first) + Tank.Center).xz;
                            if (!Grid.CanTravel(p.xz, to))
                                to = (math.rotate(Tank.Rotation, second) + Tank.Center).xz;
                            if (Grid.CanTravel(p.xz, to))
                            {
                                float y = Grid.HeightAt(to);
                                p = new float3(to.x, math.isnan(y) ? p.y : y, to.y);
                                Position[i] = p;
                            }

                            state = HordeState.Attack;
                        }
                        else if (level && math.length(math.max(q, 0f)) < r + Settings.AttackReach)
                        {
                            state = HordeState.Attack;
                        }
                    }
                }

                if (!emerging && kind.Tactic == HordeTactic.Ranged && kind.ShotInterval > 0f)
                {
                    float cd = Cooldown[i] - DeltaTime;
                    if (cd <= 0f)
                    {
                        cd = 0f;
                        float3 mouth = p + new float3(0f, MOUTH * kind.RenderScale * Settings.BodyScale, 0f);
                        if (Tank.Present && math.distance(mouth.xz, Tank.Center.xz) <= kind.ShotRange
                            && Grid.LineOfSight(mouth, Tank.Center))
                        {
                            Shots.Enqueue(new HordeShot
                            {
                                Origin = mouth, Target = Tank.Center, Damage = kind.ShotDamage * DamageScale[i], Speed = kind.ShotSpeed
                            });
                            cd = kind.ShotInterval;
                        }
                    }

                    Cooldown[i] = cd;
                }

                State[i] = state;
                float damage = state == HordeState.Attack ? kind.BiteDps * DeltaTime : 0f;
                if (kind.Tactic == HordeTactic.Melee && kind.HitInterval > 0f)
                {
                    // Blows: the first one lands at contact, then one every HitInterval while it stays at the hull.
                    float cd = math.max(Cooldown[i] - DeltaTime, 0f);
                    if (state == HordeState.Attack && cd <= 0f)
                    {
                        damage += kind.HitDamage;
                        cd = kind.HitInterval;
                    }

                    Cooldown[i] = cd;
                }

                AttackDamage[i] = damage * DamageScale[i];
            }

            Marker.End();
        }

        /// <summary>
        /// A monster at <paramref name="local"/> (hull space, <paramref name="q"/> its per-axis gap to the hull box) is
        /// under the hull of a fast tank: inside the box padded by half a body, or in front of a leading face within a
        /// body's radius plus the way the hull goes this step (<paramref name="sweep"/>). The push-out keeps a monster a
        /// body's radius off the hull, so the sweep alone would have to outrun it; with the body's radius in the reach
        /// the crush does not depend on the frame rate. Only the leading side: the hull does not crush backwards.
        /// </summary>
        private static bool InCrushZone(float2 local, float2 q, float2 sweep, float2 velocity, float r, float2 half)
        {
            float pad = r * 0.5f;
            if (q.x < pad && q.y < pad)
                return true;
            if (math.dot(local, velocity) <= 0f)
                return false;

            float2 lead = math.abs(sweep) + pad;
            float2 hi = half + pad + math.select(float2.zero, lead, sweep > 1e-5f);
            float2 lo = -half - pad - math.select(float2.zero, lead, sweep < -1e-5f);
            return math.all(local > lo) && math.all(local < hi);
        }

        private void Die(int i, float3 p, HordeDeathCause cause)
        {
            State[i] = HordeState.Dying;
            Deaths.Enqueue(new HordeDeathEvent { Index = i, Position = p, Heading = Heading[i], Cause = cause, Kind = Kind[i] });
        }
    }

    /// <summary>
    /// The shots in flight: ballistic move, a hit inside the hull box (padded) is damage to the tank (summed into
    /// <c>Totals[2]</c>), a hit on the grid's ground or a wall ends it. Then the shots fired this step join (their arc
    /// reaches the hull's centre where it is now), and every shot goes to the instance list. Events: fired, hit tank,
    /// hit ground.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeProjectileJob : IJob
    {
        public const float GRAVITY = 9.81f;
        /// <summary>A shot is gone after this many seconds (a hole in the ground under it).</summary>
        public const float MAX_AGE = 5f;

        public NativeArray<HordeProjectile> Projectiles;
        public NativeArray<int> Count;
        public NativeQueue<HordeShot> Fired;
        public NativeList<HordeInstance> Instances;
        public NativeList<HordeShotEvent> Events;
        public NativeArray<float> Totals;
        public HordeGridView Grid;
        public HordeTank Tank;
        public float HitPad;
        public float DeltaTime;
        public ProfilerMarker Marker;

        public void Execute()
        {
            Marker.Begin();
            Instances.Clear();
            Events.Clear();
            quaternion inverse = math.inverse(Tank.Rotation);
            float damage = 0f;
            int n = Count[0];
            int i = 0;
            while (i < n)
            {
                HordeProjectile shot = Projectiles[i];
                shot.Velocity.y -= GRAVITY * DeltaTime;
                shot.Position += shot.Velocity * DeltaTime;
                shot.Age += DeltaTime;

                bool gone = false;
                if (Tank.Present)
                {
                    float3 local = math.abs(math.rotate(inverse, shot.Position - Tank.Center));
                    if (math.all(local <= Tank.HalfExtents + HitPad))
                    {
                        damage += shot.Damage;
                        Events.Add(new HordeShotEvent { Position = shot.Position, Type = HordeShotEventType.HitTank });
                        gone = true;
                    }
                }

                if (!gone)
                {
                    float h = Grid.HeightAt(shot.Position.xz);
                    if ((!math.isnan(h) && shot.Position.y <= h) || shot.Age > MAX_AGE)
                    {
                        Events.Add(new HordeShotEvent
                        {
                            Position = new float3(shot.Position.x, math.isnan(h) ? shot.Position.y : h, shot.Position.z),
                            Type = HordeShotEventType.HitGround
                        });
                        gone = true;
                    }
                }

                if (gone)
                {
                    Projectiles[i] = Projectiles[n - 1];
                    n--;
                    continue;
                }

                Projectiles[i] = shot;
                i++;
            }

            while (Fired.TryDequeue(out HordeShot fired))
            {
                if (n >= Projectiles.Length)
                    continue;
                float3 d = fired.Target - fired.Origin;
                float flight = math.max(math.length(d.xz) / math.max(fired.Speed, 0.1f), 0.2f);
                float3 velocity = new float3(d.x / flight, (d.y + 0.5f * GRAVITY * flight * flight) / flight, d.z / flight);
                Projectiles[n++] = new HordeProjectile { Position = fired.Origin, Velocity = velocity, Damage = fired.Damage };
                Events.Add(new HordeShotEvent { Position = fired.Origin, Type = HordeShotEventType.Fired });
            }

            for (int k = 0; k < n; k++)
            {
                HordeProjectile shot = Projectiles[k];
                Instances.Add(new HordeInstance
                {
                    PositionHeading = new float4(shot.Position, math.atan2(shot.Velocity.x, shot.Velocity.z)),
                    Animation = new float4(0f, 0f, 0f, 0f)
                });
            }

            Count[0] = n;
            Totals[2] = damage;
            Marker.End();
        }
    }

    /// <summary>
    /// Totals of the step: [0] bite damage, [1] live monsters within the near radius of the tank, [3] biting monsters,
    /// [4..6] the biter nearest to the hull's centre ([2] is the shots' damage, written by <see cref="HordeProjectileJob"/>).
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeTotalsJob : IJob
    {
        [ReadOnly] public NativeArray<float> AttackDamage;
        [ReadOnly] public NativeArray<float3> Position;
        [ReadOnly] public NativeArray<HordeState> State;
        public NativeArray<float> Totals;
        public HordeTank Tank;
        public float NearRadius;

        public void Execute()
        {
            float damage = 0f;
            int near = 0;
            int biters = 0;
            float nearest = float.MaxValue;
            float3 biter = float3.zero;
            float nearSq = NearRadius * NearRadius;
            for (int i = 0; i < State.Length; i++)
            {
                HordeState s = State[i];
                if (!HordeStates.IsLive(s))
                    continue;
                float bite = AttackDamage[i];
                damage += bite;
                if (!Tank.Present)
                    continue;
                float d2 = math.distancesq(Position[i].xz, Tank.Center.xz);
                if (d2 < nearSq)
                    near++;
                if (bite > 0f)
                {
                    biters++;
                    if (d2 < nearest)
                    {
                        nearest = d2;
                        biter = Position[i];
                    }
                }
            }

            Totals[0] = damage;
            Totals[1] = near;
            Totals[3] = biters;
            Totals[4] = biter.x;
            Totals[5] = biter.y;
            Totals[6] = biter.z;
        }
    }

    /// <summary>
    /// Live monsters inside the camera frustum into the instance list: position, heading, the two VAT rows to blend
    /// (attack clip at the hull, idle when standing or emerging, run otherwise) and the kind in <c>Animation.w</c>.
    /// An emerging monster is drawn sunk into the ground by what is left of its rise.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordeRenderJob : IJobParallelForBatch
    {
        private const float IDLE_SPEED_SQ = 0.3f * 0.3f;

        [ReadOnly] public NativeArray<float3> Position;
        [ReadOnly] public NativeArray<HordeState> State;
        [ReadOnly] public NativeArray<float> Heading;
        [ReadOnly] public NativeArray<float> AnimTime;
        [ReadOnly] public NativeArray<float2> Velocity;
        [ReadOnly] public NativeArray<float> StateTime;
        [ReadOnly] public NativeArray<byte> Kind;
        [ReadOnly] public NativeArray<HordeKind> Kinds;
        [ReadOnly] public NativeArray<float4> Planes;
        public NativeList<HordeInstance>.ParallelWriter Instances;
        public HordeClip Run;
        public HordeClip Attack;
        public HordeClip Idle;
        public bool Cull;
        public float EmergeTime;
        /// <summary>Height of the bounding sphere centre over the feet, and its radius (at render scale 1).</summary>
        public float2 CullSphere;
        /// <summary>Model height over the Regax's at the same render scale (<see cref="HordeSettings.BodyScale"/>).</summary>
        public float BodyScale;
        public ProfilerMarker Marker;

        public void Execute(int startIndex, int count)
        {
            Marker.Begin();
            var batch = new NativeList<HordeInstance>(count, Allocator.Temp);
            for (int i = startIndex; i < startIndex + count; i++)
            {
                HordeState state = State[i];
                // Riders are drawn by their owner at the tank's pose of this frame (HordeRiders), not a step late.
                if (!HordeStates.IsLive(state) || state == HordeState.Riding)
                    continue;

                byte kindIndex = Kind[i];
                HordeKind kind = Kinds[kindIndex];
                float3 p = Position[i];
                if (Cull)
                {
                    float3 c = p + new float3(0f, CullSphere.x * kind.RenderScale * BodyScale, 0f);
                    float radius = CullSphere.y * kind.RenderScale * BodyScale;
                    bool visible = true;
                    for (int k = 0; k < Planes.Length; k++)
                    {
                        float4 plane = Planes[k];
                        if (math.dot(plane.xyz, c) + plane.w < -radius)
                        {
                            visible = false;
                            break;
                        }
                    }

                    if (!visible)
                        continue;
                }

                if (state == HordeState.Emerging)
                    p.y -= kind.EmergeDepth * BodyScale * math.saturate(StateTime[i] / math.max(EmergeTime, 0.01f));

                HordeClip clip = state == HordeState.Attack ? Attack
                    : state == HordeState.Emerging || math.lengthsq(Velocity[i]) < IDLE_SPEED_SQ ? Idle : Run;
                float3 frame = clip.Sample(AnimTime[i]);
                batch.AddNoResize(new HordeInstance
                {
                    PositionHeading = new float4(p, Heading[i]),
                    Animation = new float4(frame, kindIndex)
                });
            }

            if (batch.Length > 0)
                Instances.AddRangeNoResize(batch); // one reservation per batch, not one per monster
            batch.Dispose();
            Marker.End();
        }
    }

    /// <summary>Sorts the visible instances by kind (<c>Animation.w</c>): kind k is <c>Start[k]</c>..+<c>Counts[k]</c> of <c>Sorted</c>.</summary>
    [BurstCompile(CompileSynchronously = true)]
    internal struct HordePartitionJob : IJob
    {
        [ReadOnly] public NativeList<HordeInstance> Unsorted;
        public NativeList<HordeInstance> Sorted;
        public NativeArray<int> Starts;
        public NativeArray<int> Counts;

        public void Execute()
        {
            int kinds = Counts.Length;
            for (int k = 0; k < kinds; k++)
                Counts[k] = 0;
            for (int i = 0; i < Unsorted.Length; i++)
                Counts[math.clamp((int)Unsorted[i].Animation.w, 0, kinds - 1)]++;
            int start = 0;
            for (int k = 0; k < kinds; k++)
            {
                Starts[k] = start;
                start += Counts[k];
            }

            Sorted.ResizeUninitialized(Unsorted.Length);
            for (int k = 0; k < kinds; k++)
                Counts[k] = 0;
            for (int i = 0; i < Unsorted.Length; i++)
            {
                HordeInstance instance = Unsorted[i];
                int k = math.clamp((int)instance.Animation.w, 0, kinds - 1);
                Sorted[Starts[k] + Counts[k]] = instance;
                Counts[k]++;
            }
        }
    }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Horde
{
    // Burst jobs of the horde navigation: grid rays, flow field, A*.
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
    /// Dijkstra from the target cell: cost of every cell to the target. Edge costs are only 10 and 14,
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
}

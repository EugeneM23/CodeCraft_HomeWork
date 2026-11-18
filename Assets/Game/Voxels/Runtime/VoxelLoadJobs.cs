using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxels
{
    /// <summary>
    /// Strength of one palette entry: x — what a pair of voxels across a face bears pressed
    /// together (kg), y — what it bears bent or pulled (kg·m), z — material group (a pair of
    /// different groups is a seam and holds less).
    /// </summary>
    public static class VoxelLoad
    {
        /// <summary>Edge of a load block, in voxels.</summary>
        public const int BLOCK = 4;

        /// <summary>A pair of voxels of different material groups holds this share: floors part from walls.</summary>
        public const float SEAM = 0.7f;

        /// <summary>
        /// ...and bent, this share: a floor rests on its wall but does not hold the wall up from the side,
        /// and a wall knocked from under does not hang off the joists.
        /// </summary>
        public const float SEAM_BEND = 0.15f;

        /// <summary>A building is made to bear its whole self this many times over at every joint (see StructuralLoadJob.Scale).</summary>
        public const float DESIGN_MARGIN = 1.5f;

        /// <summary>Path cost of a step sideways against one up or down: load goes down the walls first.</summary>
        public const int SIDE_COST = 4;

        /// <summary>Path cost of a step down from the ground's side: the block hangs from the one above.</summary>
        public const int HANG_COST = 16;

        /// <summary>A piece hanging from above holds by its bending strength times this (1/m).</summary>
        public const float TENSION_PER_BEND = 2.5f;

        /// <summary>Overloaded joints within this share of the worst one break together in one pass.</summary>
        public const float BREAK_SHARE = 0.75f;

        /// <summary>Joints overloaded this much break together whatever the worst one is.</summary>
        public const float BREAK_ALL = 1.5f;

        /// <summary>Rows of a wall (its own and those over it) that bend together over a gap: a deep beam, not a whole storey.</summary>
        public const int DEEP_ROWS = 3;

        /// <summary>Blocks round the damage (1.2 m) where joints may break; further off damage spreads only by what falls.</summary>
        public const int ZONE_MARGIN = 3;


        /// <summary>Most break-and-resolve rounds in one pass; the rest goes on next frame.</summary>
        public const int ROUNDS = 3;

        /// <summary>A whole face of brick (16 pairs × 1000 kg): the yardstick of a seam fracture's step cost.</summary>
        public const float FULL_FACE = 16000f;

        /// <summary>Extra path cost of a fracture cell crossing a face with no strength at all.</summary>
        public const float SEAM_FRACTURE_COST = 8f;

        public static int3 Blocks(int3 size) => (size + BLOCK - 1) / BLOCK;

        /// <summary>
        /// kg per cubic metre of a material group (the z of a strength entry): a building weighs
        /// what it is made of, and its pieces fall with it — a slate roof heavy, a floor of boards light.
        /// </summary>
        public static float GroupDensity(float group) => (int)group switch
        {
            0 => 2400f, // concrete
            1 => 2500f, // stone
            2 => 1800f, // brick
            3 => 1600f, // plaster, trim, paper
            4 => 600f, // wood
            5 => 2000f, // slate on its battens
            6 => 700f, // furniture, cloth
            7 => 2500f, // glass
            _ => 1800f
        };
    }

    /// <summary>
    /// Sums a body's voxels into load blocks: the mass of each block, the strength of each face
    /// between neighbouring blocks (pairs of solid voxels across it), and which block columns
    /// reach the bottom layer.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct BuildBlocksJob : IJobFor
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public int3 Size;

        /// <summary>Per palette entry, see <see cref="VoxelLoad"/>.</summary>
        [ReadOnly] public NativeArray<float3> Strength;

        /// <summary>Cubic metres of one voxel: its mass is this times its material's density.</summary>
        public float VoxelVolume;

        public int3 Blocks;

        /// <summary>Per block, kg. Zeroed on input.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<float> Mass;

        /// <summary>Per block, the face towards +x / +y / +z: (press, bend). Zeroed on input.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<float2> BondX;
        [NativeDisableParallelForRestriction] public NativeArray<float2> BondY;
        [NativeDisableParallelForRestriction] public NativeArray<float2> BondZ;

        /// <summary>Per block column (x + Blocks.x · z): 1 when it has voxels in the body's bottom layer. Zeroed on input.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<byte> Bottom;

        /// <summary>One slab of blocks (block z = <paramref name="slab"/>): it writes only its own blocks and faces, so slabs run in parallel.</summary>
        public void Execute(int slab)
        {
            int sx = Size.x;
            int sxy = Size.x * Size.y;
            int z1 = math.min(Size.z, (slab + 1) * VoxelLoad.BLOCK);
            for (int z = slab * VoxelLoad.BLOCK; z < z1; z++)
            for (int y = 0; y < Size.y; y++)
            for (int x = 0; x < Size.x; x++)
            {
                int i = x + sx * y + sxy * z;
                byte v = Voxels[i];
                if (v == 0)
                    continue;

                int3 b = new int3(x, y, z) / VoxelLoad.BLOCK;
                int block = b.x + Blocks.x * (b.y + Blocks.y * b.z);
                Mass[block] += VoxelVolume * VoxelLoad.GroupDensity(Strength[v].z);
                if (y == 0)
                    Bottom[b.x + Blocks.x * b.z] = 1;

                // A face between blocks lies after every BLOCK-th voxel.
                if (x + 1 < Size.x && (x + 1) % VoxelLoad.BLOCK == 0 && Voxels[i + 1] != 0)
                    BondX[block] += Pair(v, Voxels[i + 1]);
                if (y + 1 < Size.y && (y + 1) % VoxelLoad.BLOCK == 0 && Voxels[i + sx] != 0)
                    BondY[block] += Pair(v, Voxels[i + sx]);
                if (z + 1 < Size.z && (z + 1) % VoxelLoad.BLOCK == 0 && Voxels[i + sxy] != 0)
                    BondZ[block] += Pair(v, Voxels[i + sxy]);
            }
        }

        private float2 Pair(byte a, byte b)
        {
            float3 sa = Strength[a];
            float3 sb = Strength[b];
            float2 bond = math.min(sa.xy, sb.xy);
            return sa.z == sb.z ? bond : bond * new float2(VoxelLoad.SEAM, VoxelLoad.SEAM_BEND);
        }
    }

    /// <summary>
    /// The load a standing body's blocks put on each joint on their way to the ground, and the
    /// joints that give way. Paths to the supports (Dijkstra; sideways costs more than down)
    /// carry each block's weight and what rests on it down to the ground; a step sideways adds
    /// a moment (weight × lever), a step down hands the moment to the wall. A joint pressed
    /// harder than its pressing strength, or bent harder than its bending strength, breaks;
    /// the worst ones go first and the load is worked out again. Blocks no path reaches any
    /// more are islands that fall.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct StructuralLoadJob : IJob
    {
        public int3 Blocks;
        [ReadOnly] public NativeArray<float> Mass;
        [ReadOnly] public NativeArray<float2> BondX;
        [ReadOnly] public NativeArray<float2> BondY;
        [ReadOnly] public NativeArray<float2> BondZ;

        /// <summary>Per block column: the body's bottom layer stands on something there.</summary>
        [ReadOnly] public NativeArray<byte> Support;

        /// <summary>Per block, kg pressing on it once (a piece that fell on it).</summary>
        [ReadOnly] public NativeArray<float> Extra;

        /// <summary>Edge of a block in metres: the lever of one step sideways.</summary>
        public float Step;

        /// <summary>
        /// Per block face (block · 3 + axis): the joint is cracked. Kept by the body from solve to
        /// solve — a crack stays one — and added to here.
        /// </summary>
        public NativeArray<byte> Broken;

        /// <summary>
        /// Per block face: what the joint bears as a share of its materials' strength. A building is
        /// made to stand: the design pass (<see cref="Design"/>) raises every joint to bear its
        /// whole-house load with <see cref="VoxelLoad.DESIGN_MARGIN"/> to spare, so only damage overloads one.
        /// </summary>
        public NativeArray<float> Scale;

        /// <summary>Work out the whole building's load and set <see cref="Scale"/> from it; nothing breaks.</summary>
        public bool Design;

        /// <summary>Blocks whose joints may break (inclusive): round the damage. Load goes everywhere.</summary>
        public int3 ZoneLo;
        public int3 ZoneHi;

        /// <summary>Per block: 0 — holds; otherwise the 1-based island it falls with, negative when crushed (it crumbles).</summary>
        public NativeArray<int> Island;

        /// <summary>[0] islands, [1] 1 when joints were still overloaded after the last round, [2] the worst overload of the first round × 1000, [3] blocks the supports reach.</summary>
        public NativeArray<int> Result;

        /// <summary>Per block face along x / z: bending strength of the wall standing whole over it (see Deep).</summary>
        /// Made inside Execute (Temp), so not checked when the job is scheduled.
        [NativeDisableContainerSafetyRestriction] private NativeArray<float> _deepX;
        [NativeDisableContainerSafetyRestriction] private NativeArray<float> _deepZ;

        /// <summary>Per block: crushed under a load it could not bear (see Breaks); it holds nothing up any more.</summary>
        [NativeDisableContainerSafetyRestriction] private NativeArray<byte> _crushed;

        /// <summary>Per block face: the last ratio there was a block pressing down (not one hanging).</summary>
        [NativeDisableContainerSafetyRestriction] private NativeArray<byte> _pressed;

        private int _worstSlot;
        private int _firstWorstSlot;

        public void Execute()
        {
            int count = Blocks.x * Blocks.y * Blocks.z;
            var dist = new NativeArray<int>(count, Allocator.Temp);
            var order = new NativeList<int>(count, Allocator.Temp);
            var heap = new NativeList<long>(count, Allocator.Temp);
            var load = new NativeArray<float>(count, Allocator.Temp);
            var moment = new NativeArray<float>(count, Allocator.Temp);
            var ratio = new NativeArray<float>(count * 3, Allocator.Temp);
            _deepX = new NativeArray<float>(count, Allocator.Temp);
            _deepZ = new NativeArray<float>(count, Allocator.Temp);
            _crushed = new NativeArray<byte>(count, Allocator.Temp);
            _pressed = new NativeArray<byte>(count * 3, Allocator.Temp);

            if (Design)
            {
                Paths(dist, order, heap);
                Accumulate(dist, order, load, moment, ratio);
                for (int slot = 0; slot < ratio.Length; slot++)
                    Scale[slot] = math.clamp(ratio[slot] * VoxelLoad.DESIGN_MARGIN, 1f, 1e6f);
                Result[0] = 0;
                Result[1] = 0;
                Result[3] = order.Length;
                return;
            }

            bool unsettled = false;
            float firstWorst = 0f;
            for (int round = 0;; round++)
            {
                Paths(dist, order, heap);
                if (round == 0)
                    Result[3] = order.Length;
                if (round == VoxelLoad.ROUNDS)
                {
                    unsettled = true;
                    break;
                }

                bool broke = Breaks(dist, order, load, moment, ratio, out float worst);
                if (round == 0)
                {
                    firstWorst = worst;
                    _firstWorstSlot = _worstSlot;
                }
                if (!broke)
                    break;
            }

            Result[0] = Islands(dist, order);
            Result[1] = unsettled ? 1 : 0;
            Result[2] = (int)math.min(firstWorst * 1000f, 2e9f);
            Result[4] = _firstWorstSlot;
        }

        /// <summary>Direction k: 0 +x, 1 −x, 2 +y (up), 3 −y (down), 4 +z, 5 −z.</summary>
        private static int3 Dir(int k)
        {
            int3 d = int3.zero;
            d[k >> 1] = (k & 1) == 0 ? 1 : -1;
            return d;
        }

        private int Index(int3 b) => b.x + Blocks.x * (b.y + Blocks.y * b.z);

        /// <summary>The joint between block b and its neighbour in direction d: its strength, or zero when broken or absent.</summary>
        private float2 Joint(int3 b, int d, out int face, out int axis)
        {
            int3 dir = Dir(d);
            int3 low = math.min(b, b + dir);
            face = Index(low);
            axis = d >> 1;
            if (Broken[face * 3 + axis] != 0)
                return float2.zero;
            return axis == 0 ? BondX[face] : axis == 1 ? BondY[face] : BondZ[face];
        }

        private bool Inside(int3 b) => math.all(b >= 0) && math.all(b < Blocks);

        /// <summary>Shortest paths from the supports over whole joints; <paramref name="order"/> gets the blocks reached, nearest first.</summary>
        private void Paths(NativeArray<int> dist, NativeList<int> order, NativeList<long> heap)
        {
            for (int i = 0; i < dist.Length; i++)
                dist[i] = int.MaxValue;
            order.Clear();
            heap.Clear();

            for (int z = 0; z < Blocks.z; z++)
            for (int x = 0; x < Blocks.x; x++)
            {
                int column = x + Blocks.x * z;
                int block = Index(new int3(x, 0, z));
                if (Support[column] == 0 || Mass[block] <= 0f || _crushed[block] != 0)
                    continue;
                dist[block] = 0;
                Push(heap, 0, block);
            }

            while (heap.Length > 0)
            {
                long top = Pop(heap);
                int d = (int)(top >> 32);
                int u = (int)(top & 0xFFFFFFFF);
                if (d != dist[u])
                    continue;
                order.Add(u);

                int3 b = Coord(u);
                for (int k = 0; k < 6; k++)
                {
                    int3 n = b + Dir(k);
                    if (!Inside(n))
                        continue;
                    int v = Index(n);
                    if (Mass[v] <= 0f || _crushed[v] != 0 || Joint(b, k, out _, out _).x <= 0f)
                        continue;
                    // Up from the ground is cheap, sideways dearer, down (hanging) dearest: a block
                    // standing on a supported one is always nearer the ground than it.
                    int nd = d + (k == 2 ? 1 : k == 3 ? VoxelLoad.HANG_COST : VoxelLoad.SIDE_COST);
                    if (nd >= dist[v])
                        continue;
                    dist[v] = nd;
                    Push(heap, nd, v);
                }
            }
        }

        /// <summary>Breaks the joints overloaded the most (see <see cref="Accumulate"/>). True when it broke any.</summary>
        private bool Breaks(NativeArray<int> dist, NativeList<int> order, NativeArray<float> load, NativeArray<float> moment,
            NativeArray<float> ratio, out float worst)
        {
            worst = Accumulate(dist, order, load, moment, ratio);
            if (worst <= 1f)
                return false;

            float limit = math.max(1f, math.min(worst * VoxelLoad.BREAK_SHARE, VoxelLoad.BREAK_ALL));
            for (int slot = 0; slot < ratio.Length; slot++)
            {
                if (ratio[slot] < limit)
                    continue;
                Broken[slot] = 1;

                // Pressed harder than it bears: what is under the joint is crushed down to the ground,
                // or the load would lie on the stump, held there by the colliders.
                if (_pressed[slot] != 0)
                {
                    for (int3 c = Coord(slot / 3); c.y >= 0; c.y--)
                    {
                        int block = Index(c);
                        if (Mass[block] <= 0f)
                            break;
                        _crushed[block] = 1;
                    }

                    continue;
                }

                // A wall bent over a gap cracks through its whole depth at once (the rows that bent
                // with it, see Deep): what hangs comes off there as one piece, rather than the
                // crack running along the wall row by row.
                int axis = slot % 3;
                if (axis == 1)
                    continue;
                int3 b = Coord(slot / 3);
                // The rows over it that bent with it (see Deep): up to the first gap or crack.
                for (int dy = 1; dy < VoxelLoad.DEEP_ROWS; dy++)
                {
                    int3 n = b + new int3(0, dy, 0);
                    if (!Inside(n))
                        break;
                    int above = Index(n) * 3 + axis;
                    float bend = axis == 0 ? BondX[Index(n)].y : BondZ[Index(n)].y;
                    if (Broken[above] != 0 || bend <= 0f)
                        break;
                    Broken[above] = 1;
                }
            }

            return true;
        }

        /// <summary>
        /// Hands every block's load down its paths and fills <paramref name="ratio"/> with what each joint on
        /// them bears as a share of what it can. Returns the worst share.
        /// </summary>
        private float Accumulate(NativeArray<int> dist, NativeList<int> order, NativeArray<float> load, NativeArray<float> moment,
            NativeArray<float> ratio)
        {
            for (int i = 0; i < load.Length; i++)
            {
                load[i] = Mass[i] + Extra[i];
                moment[i] = 0f;
            }

            for (int i = 0; i < ratio.Length; i++)
            {
                ratio[i] = 0f;
                _pressed[i] = 0;
            }

            Deep();

            // Row by row from the top, and in a row from the far end of the paths: a block's load
            // goes to the block under it, or over a gap sideways along the row towards the
            // ground — both handled after it — or, with neither, it hangs from the block above.
            var keys = new NativeArray<long>(order.Length, Allocator.Temp);
            for (int o = 0; o < order.Length; o++)
            {
                int u = order[o];
                keys[o] = ((long)Coord(u).y << 40) | ((long)dist[u] << 20) | (uint)u;
            }

            keys.Sort();

            float worst = 0f;
            for (int o = keys.Length - 1; o >= 0; o--)
            {
                int u = (int)(keys[o] & 0xFFFFF);
                if (dist[u] == 0)
                    continue;

                int3 b = Coord(u);
                if (Holds(b, 3, dist, out float2 down) && dist[Index(b + Dir(3))] < dist[u])
                {
                    Transfer(u, b, 3, 1f, down, load, moment, ratio, ref worst);
                    continue;
                }

                float sum = 0f;
                for (int k = 0; k < 6; k++)
                    if ((k >> 1) != 1 && Holds(b, k, dist, out float2 bond) && dist[Index(b + Dir(k))] < dist[u])
                        sum += bond.x;

                if (sum > 0f)
                {
                    for (int k = 0; k < 6; k++)
                        if ((k >> 1) != 1 && Holds(b, k, dist, out float2 bond) && dist[Index(b + Dir(k))] < dist[u])
                            Transfer(u, b, k, bond.x / sum, bond, load, moment, ratio, ref worst);
                    continue;
                }

                if (Holds(b, 2, dist, out float2 up) && dist[Index(b + Dir(2))] < dist[u])
                    Transfer(u, b, 2, 1f, up, load, moment, ratio, ref worst);
            }

            keys.Dispose();
            return worst;
        }

        /// <summary>Whether the neighbour in direction k reaches the ground and the joint to it is whole.</summary>
        private bool Holds(int3 b, int k, NativeArray<int> dist, out float2 bond)
        {
            bond = default;
            int3 n = b + Dir(k);
            if (!Inside(n) || dist[Index(n)] == int.MaxValue)
                return false;
            bond = Joint(b, k, out _, out _);
            return bond.x > 0f;
        }

        /// <summary>Hands a share of block u's load through the joint in direction k and checks the joint.</summary>
        private void Transfer(int u, int3 b, int k, float share, float2 bond, NativeArray<float> load, NativeArray<float> moment,
            NativeArray<float> ratio, ref float worst)
        {
            float l = load[u] * share;
            float m = moment[u] * share;
            int v = Index(b + Dir(k));
            Joint(b, k, out int face, out int axis);

            float r;
            if (k == 3) // down: pressed; the wall below takes the moment
            {
                r = l / bond.x;
                load[v] += l;
            }
            else if (k == 2) // up: hangs from the block above (handled already: the pull is only checked)
            {
                r = bond.y > 0f ? l / (bond.y * VoxelLoad.TENSION_PER_BEND) : float.MaxValue;
            }
            else // sideways: bent by everything it carries times its lever, as deep as the wall over it
            {
                m += l * Step;
                float deep = axis == 0 ? _deepX[face] : _deepZ[face];
                r = deep > 0f ? m / deep : float.MaxValue;
                moment[v] += m;
                load[v] += l;
            }

            if (math.any(b < ZoneLo) || math.any(b > ZoneHi))
                return;

            int slot = face * 3 + axis;
            r /= Scale[slot];
            if (r >= ratio[slot])
                _pressed[slot] = (byte)(k == 3 ? 1 : 0);
            ratio[slot] = math.max(ratio[slot], r);
            if (r > worst)
            {
                worst = r;
                _worstSlot = slot;
            }
        }

        /// <summary>
        /// A wall over an opening bends as a whole, not row by row: a joint beside bears with the
        /// bending strength of its own row and of every whole row over it, up to <see cref="VoxelLoad.DEEP_ROWS"/> rows and the first gap or crack.
        /// </summary>
        private void Deep()
        {
            for (int z = 0; z < Blocks.z; z++)
            for (int x = 0; x < Blocks.x; x++)
            {
                for (int y = 0; y < Blocks.y; y++)
                {
                    float deepX = 0f;
                    float deepZ = 0f;
                    bool wholeX = true;
                    bool wholeZ = true;
                    for (int row = y; row < math.min(Blocks.y, y + VoxelLoad.DEEP_ROWS); row++)
                    {
                        int above = Index(new int3(x, row, z));
                        float bx = Broken[above * 3] != 0 ? 0f : BondX[above].y;
                        float bz = Broken[above * 3 + 2] != 0 ? 0f : BondZ[above].y;
                        wholeX &= bx > 0f;
                        wholeZ &= bz > 0f;
                        deepX += wholeX ? bx : 0f;
                        deepZ += wholeZ ? bz : 0f;
                    }

                    int face = Index(new int3(x, y, z));
                    _deepX[face] = deepX;
                    _deepZ[face] = deepZ;
                }
            }
        }

        /// <summary>Gives every block no path reaches an island number: blocks joined by whole joints fall together.</summary>
        private int Islands(NativeArray<int> dist, NativeList<int> order)
        {
            int count = dist.Length;
            for (int i = 0; i < count; i++)
                Island[i] = 0;

            int islands = 0;
            var queue = order; // reuse: the reached list is no longer needed
            for (int start = 0; start < count; start++)
            {
                if (dist[start] != int.MaxValue || Mass[start] <= 0f || Island[start] != 0)
                    continue;

                islands++;
                // A crushed island is numbered below zero: it crumbles rather than falls whole.
                byte crushed = _crushed[start];
                int id = crushed != 0 ? -islands : islands;
                queue.Clear();
                queue.Add(start);
                Island[start] = id;
                for (int head = 0; head < queue.Length; head++)
                {
                    int u = queue[head];
                    int3 b = Coord(u);
                    for (int k = 0; k < 6; k++)
                    {
                        int3 n = b + Dir(k);
                        if (!Inside(n))
                            continue;
                        int v = Index(n);
                        if (Island[v] != 0 || dist[v] != int.MaxValue || Mass[v] <= 0f || _crushed[v] != crushed ||
                            Joint(b, k, out _, out _).x <= 0f)
                            continue;
                        Island[v] = id;
                        queue.Add(v);
                    }
                }
            }

            return islands;
        }

        private int3 Coord(int index)
        {
            int x = index % Blocks.x;
            int yz = index / Blocks.x;
            return new int3(x, yz % Blocks.y, yz / Blocks.y);
        }

        private static void Push(NativeList<long> heap, int key, int value)
        {
            heap.Add(((long)key << 32) | (uint)value);
            int i = heap.Length - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (heap[parent] <= heap[i])
                    break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private static long Pop(NativeList<long> heap)
        {
            long top = heap[0];
            int last = heap.Length - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            int n = heap.Length;
            while (true)
            {
                int l = 2 * i + 1;
                if (l >= n)
                    break;
                int r = l + 1;
                int c = r < n && heap[r] < heap[l] ? r : l;
                if (heap[i] <= heap[c])
                    break;
                (heap[i], heap[c]) = (heap[c], heap[i]);
                i = c;
            }

            return top;
        }
    }

    /// <summary>
    /// Takes out a share of the voxels lining the cracks between fracture cells (a voxel with a
    /// neighbour in another cell): they go as crumbs, the pieces part with ragged edges.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct SeamCrumbsJob : IJob
    {
        public NativeArray<byte> Voxels;
        public int3 Size;
        [ReadOnly] public NativeArray<int> Cells;
        public float Share;
        public uint Seed;
        public NativeList<RemovedVoxel> Removed;

        public void Execute()
        {
            int sx = Size.x;
            int sxy = Size.x * Size.y;
            for (int z = 0; z < Size.z; z++)
            for (int y = 0; y < Size.y; y++)
            for (int x = 0; x < Size.x; x++)
            {
                int i = x + sx * y + sxy * z;
                byte v = Voxels[i];
                if (v == 0)
                    continue;

                int cell = Cells[i];
                bool seam = (x + 1 < Size.x && Voxels[i + 1] != 0 && Cells[i + 1] != cell) ||
                            (y + 1 < Size.y && Voxels[i + sx] != 0 && Cells[i + sx] != cell) ||
                            (z + 1 < Size.z && Voxels[i + sxy] != 0 && Cells[i + sxy] != cell);
                if (!seam || VoxelMath.Hash01(new int3(x, y, z), Seed) >= Share)
                    continue;

                Removed.Add(new RemovedVoxel { Position = new int3(x, y, z), Material = v });
                Voxels[i] = 0;
            }
        }
    }

    /// <summary>
    /// Connectivity round a few bites only (VoxelBody.LocalSplit): a flood from every solid voxel in
    /// the bites' boxes, each stopped when it reaches the bottom layer, grows past Budget or runs
    /// into a flood that did — those hold (Grounded). The others closed off on their own: loose scraps,
    /// fully labelled (1-based component index, as LabelComponentsJob).
    /// </summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct LocalSplitJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public int3 Size;
        [ReadOnly] public NativeArray<int3> Lo;
        [ReadOnly] public NativeArray<int3> Hi;
        public int Budget;

        /// <summary>Zeroed on input; per voxel the 1-based component that reached it.</summary>
        public NativeArray<int> Labels;

        /// <summary>The flood's stack.</summary>
        public NativeList<int> Queue;
        public NativeList<VoxelComponent> Components;

        public void Execute()
        {
            for (int r = 0; r < Lo.Length; r++)
            for (int z = Lo[r].z; z <= Hi[r].z; z++)
            for (int y = Lo[r].y; y <= Hi[r].y; y++)
            for (int x = Lo[r].x; x <= Hi[r].x; x++)
            {
                int start = VoxelMath.Index(new int3(x, y, z), Size);
                if (Voxels[start] == 0 || Labels[start] != 0)
                    continue;
                Flood(start);
            }
        }

        /// <summary>
        /// Depth first, down first: through a wall it runs straight to the bottom layer in a few
        /// hundred voxels; a piece cut loose it walks whole.
        /// </summary>
        private void Flood(int start)
        {
            int label = Components.Length + 1;
            var info = new VoxelComponent { Min = new int3(int.MaxValue), Max = new int3(int.MinValue) };
            Queue.Clear();
            Queue.Add(start);
            Labels[start] = label;

            while (Queue.Length > 0)
            {
                int current = Queue[Queue.Length - 1];
                Queue.RemoveAt(Queue.Length - 1);
                int3 p = VoxelMath.Coord(current, Size);
                info.Count++;
                info.Min = math.min(info.Min, p);
                info.Max = math.max(info.Max, p);
                if (p.y == 0 || info.Count > Budget)
                {
                    info.Grounded = true;
                    break;
                }

                // Pushed last, popped first: down (k = 3) leads.
                for (int i = 0; i < 6; i++)
                {
                    int k = i == 5 ? 3 : i == 3 ? 5 : i;
                    int3 n = p;
                    n[k >> 1] += (k & 1) == 0 ? 1 : -1;
                    if (!VoxelMath.Inside(n, Size))
                        continue;
                    int index = VoxelMath.Index(n, Size);
                    if (Voxels[index] == 0)
                        continue;
                    int other = Labels[index];
                    if (other == label)
                        continue;
                    if (other != 0)
                    {
                        // Ran into an earlier flood: only one that held can still be open to it.
                        info.Grounded = true;
                        break;
                    }

                    Labels[index] = label;
                    Queue.Add(index);
                }

                if (info.Grounded)
                    break;
            }

            Components.Add(info);
        }
    }

    /// <summary>
    /// Labels the voxels of the load islands only (VoxelBody.DropIslands): a flood from each island
    /// block's voxels that stays in its island. Costs what falls, not the whole building's grid.
    /// Components as LabelComponentsJob (1-based), Cell = the island.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct IslandLabelJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        public int3 Size;
        public int3 Blocks;
        [ReadOnly] public NativeArray<int> Island;
        public NativeArray<int> Labels;
        public NativeList<int> Queue;
        public NativeList<VoxelComponent> Components;

        private int CellOf(int3 p)
        {
            int3 b = p / VoxelLoad.BLOCK;
            return Island[b.x + Blocks.x * (b.y + Blocks.y * b.z)];
        }

        public void Execute()
        {
            for (int block = 0; block < Island.Length; block++)
            {
                int cell = Island[block];
                if (cell == 0)
                    continue;

                int bx = block % Blocks.x;
                int byz = block / Blocks.x;
                int3 lo = new int3(bx, byz % Blocks.y, byz / Blocks.y) * VoxelLoad.BLOCK;
                int3 hi = math.min(lo + VoxelLoad.BLOCK, Size);
                for (int z = lo.z; z < hi.z; z++)
                for (int y = lo.y; y < hi.y; y++)
                for (int x = lo.x; x < hi.x; x++)
                {
                    int start = VoxelMath.Index(new int3(x, y, z), Size);
                    if (Voxels[start] != 0 && Labels[start] == 0)
                        Flood(start, cell);
                }
            }
        }

        private void Flood(int start, int cell)
        {
            int label = Components.Length + 1;
            var info = new VoxelComponent { Min = new int3(int.MaxValue), Max = new int3(int.MinValue), Cell = cell };
            Queue.Clear();
            Queue.Add(start);
            Labels[start] = label;
            for (int head = 0; head < Queue.Length; head++)
            {
                int3 p = VoxelMath.Coord(Queue[head], Size);
                info.Count++;
                info.Min = math.min(info.Min, p);
                info.Max = math.max(info.Max, p);
                if (p.y == 0)
                    info.Grounded = true;
                for (int k = 0; k < 6; k++)
                {
                    int3 n = p;
                    n[k >> 1] += (k & 1) == 0 ? 1 : -1;
                    if (!VoxelMath.Inside(n, Size))
                        continue;
                    int index = VoxelMath.Index(n, Size);
                    if (Voxels[index] == 0 || Labels[index] != 0 || CellOf(n) != cell)
                        continue;
                    Labels[index] = label;
                    Queue.Add(index);
                }
            }

            Components.Add(info);
        }
    }

    /// <summary>Zeroes a byte array (a recycled body's voxels, VoxelBody.TakeVoxels).</summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct ClearBytesJob : IJob
    {
        public NativeArray<byte> Bytes;

        public void Execute()
        {
            for (int i = 0; i < Bytes.Length; i++)
                Bytes[i] = 0;
        }
    }

    /// <summary>The mass of a body with a strength table: every voxel by its material's density.</summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct BodyMassJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Voxels;
        [ReadOnly] public NativeArray<float3> Strength;
        public float VoxelVolume;
        public NativeReference<float> Mass;

        public void Execute()
        {
            float mass = 0f;
            for (int i = 0; i < Voxels.Length; i++)
            {
                byte v = Voxels[i];
                if (v != 0)
                    mass += VoxelLoad.GroupDensity(Strength[v].z);
            }

            Mass.Value = mass * VoxelVolume;
        }
    }

    /// <summary>Gives every voxel the cell of its block (a load island, or a seam fracture cell).</summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct BlockCellsJob : IJob
    {
        public int3 Size;
        public int3 Blocks;
        [ReadOnly] public NativeArray<int> BlockCell;
        [WriteOnly] public NativeArray<int> Cells;

        public void Execute()
        {
            int i = 0;
            for (int z = 0; z < Size.z; z++)
            for (int y = 0; y < Size.y; y++)
            {
                int row = Blocks.x * (y / VoxelLoad.BLOCK + Blocks.y * (z / VoxelLoad.BLOCK));
                for (int x = 0; x < Size.x; x++)
                    Cells[i++] = BlockCell[row + x / VoxelLoad.BLOCK];
            }
        }
    }

    /// <summary>
    /// Fracture cells that follow a piece's weak joints: from seed blocks, cells grow over the
    /// block graph (Dijkstra), and crossing a face costs more the weaker it is, so cells meet —
    /// and the piece parts — at seams: a floor from its walls, a pane from its frame, a thin
    /// neck rather than a thick wall.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
    public struct SeamCellsJob : IJob
    {
        public int3 Blocks;
        [ReadOnly] public NativeArray<float> Mass;
        [ReadOnly] public NativeArray<float2> BondX;
        [ReadOnly] public NativeArray<float2> BondY;
        [ReadOnly] public NativeArray<float2> BondZ;

        /// <summary>Seed points, in voxels.</summary>
        [ReadOnly] public NativeArray<float3> Seeds;

        /// <summary>Per block: 1-based cell; 0 for blocks no seed reached.</summary>
        public NativeArray<int> BlockCell;

        public void Execute()
        {
            int count = Blocks.x * Blocks.y * Blocks.z;
            var dist = new NativeArray<float>(count, Allocator.Temp);
            var heap = new NativeList<float2>(count, Allocator.Temp);
            for (int i = 0; i < count; i++)
            {
                dist[i] = float.MaxValue;
                BlockCell[i] = 0;
            }

            for (int s = 0; s < Seeds.Length; s++)
            {
                int3 b = math.clamp((int3)math.floor(Seeds[s] / VoxelLoad.BLOCK), 0, Blocks - 1);
                // A seed in the air (a fallen storey is mostly rooms) moves to the nearest solid block.
                int block = NearestSolid(b);
                if (block < 0 || dist[block] == 0f)
                    continue;
                dist[block] = 0f;
                BlockCell[block] = s + 1;
                Push(heap, 0f, block);
            }

            while (heap.Length > 0)
            {
                float2 top = Pop(heap);
                int u = (int)top.y;
                if (top.x > dist[u])
                    continue;

                int x = u % Blocks.x;
                int yz = u / Blocks.x;
                int3 b = new int3(x, yz % Blocks.y, yz / Blocks.y);
                for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int3 n = b;
                    n[axis] += sign;
                    if (math.any(n < 0) || math.any(n >= Blocks))
                        continue;
                    int v = n.x + Blocks.x * (n.y + Blocks.y * n.z);
                    if (Mass[v] <= 0f)
                        continue;

                    int3 low = math.min(b, n);
                    int face = low.x + Blocks.x * (low.y + Blocks.y * low.z);
                    float press = axis == 0 ? BondX[face].x : axis == 1 ? BondY[face].x : BondZ[face].x;
                    if (press <= 0f)
                        continue;

                    float weak = 1f - math.saturate(press / VoxelLoad.FULL_FACE);
                    float nd = top.x + 1f + VoxelLoad.SEAM_FRACTURE_COST * weak;
                    if (nd >= dist[v])
                        continue;
                    dist[v] = nd;
                    BlockCell[v] = BlockCell[u];
                    Push(heap, nd, v);
                }
            }
        }

        /// <summary>The block with mass nearest to b (by Chebyshev rings up to SEED_REACH), or −1.</summary>
        private int NearestSolid(int3 b)
        {
            for (int r = 0; r <= SEED_REACH; r++)
            {
                int3 lo = math.max(b - r, 0);
                int3 hi = math.min(b + r, Blocks - 1);
                for (int z = lo.z; z <= hi.z; z++)
                for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                {
                    int3 c = new int3(x, y, z);
                    if (math.cmax(math.abs(c - b)) != r)
                        continue;
                    int block = x + Blocks.x * (y + Blocks.y * z);
                    if (Mass[block] > 0f)
                        return block;
                }
            }

            return -1;
        }

        private const int SEED_REACH = 4;

        private static void Push(NativeList<float2> heap, float key, int value)
        {
            heap.Add(new float2(key, value));
            int i = heap.Length - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (heap[parent].x <= heap[i].x)
                    break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        private static float2 Pop(NativeList<float2> heap)
        {
            float2 top = heap[0];
            int last = heap.Length - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            int n = heap.Length;
            while (true)
            {
                int l = 2 * i + 1;
                if (l >= n)
                    break;
                int r = l + 1;
                int c = r < n && heap[r].x < heap[l].x ? r : l;
                if (heap[i].x <= heap[c].x)
                    break;
                (heap[i], heap[c]) = (heap[c], heap[i]);
                i = c;
            }

            return top;
        }
    }
}

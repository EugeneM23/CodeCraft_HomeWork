using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A standing body's structural strength (see StructuralLoadJob): the weight of its parts
    /// goes down to the ground it stands on, joints that cannot bear it break, and what no longer
    /// reaches the ground falls in clusters of blocks. Only bodies given a strength table
    /// (SetStrength) — buildings — take part; the rest stand or fall as before.
    /// </summary>
    public sealed partial class VoxelBody
    {
        /// <summary>Per palette entry: press (kg), bend (kg·m), material group. Owned.</summary>
        private NativeArray<float3> _strength;

        private bool _structureDirty;

        /// <summary>Where the damage since the last settled solve is, in voxels (see MarkStructureDirty).</summary>
        private int3 _zoneLo;
        private int3 _zoneHi;
        private bool _hasZone;

        /// <summary>A hit opens the zone this far round its contact (m).</summary>
        private const float IMPACT_REACH = 1f;

        /// <summary>Per block face (block · 3 + axis): cracked by an earlier solve. Made on the first solve.</summary>
        private NativeArray<byte> _cracks;

        /// <summary>Per block face: the design margin (StructuralLoadJob.Scale). Made by DesignStructure, or all 1 on the first solve.</summary>
        private NativeArray<float> _scale;
        private bool _designed;

        /// <summary>Loads from pieces that fell on the body (voxel coordinates, kg), for its next solve.</summary>
        private readonly Vector4[] _impacts = new Vector4[MAX_IMPACTS];
        private int _impactCount;

        private const int MAX_IMPACTS = 16;

        /// <summary>A probe for the ground under the bottom layer looks this far below it (m)...</summary>
        private const float SUPPORT_BELOW = 0.6f;

        /// <summary>...and this far above it: a house set a little into the ground stands on it.</summary>
        private const float SUPPORT_ABOVE = 0.3f;

        /// <summary>A fallen piece's hit counts as its impulse spread over this long (s).</summary>
        public const float IMPACT_TIME = 0.2f;

        /// <summary>Hits lighter than this (kg of load) are not worth a solve.</summary>
        public const float MIN_IMPACT_LOAD = 1000f;

        /// <summary>Whether this body works out its load (it has a strength table).</summary>
        public bool HasStrength => _strength.IsCreated;

        /// <summary>Per palette entry: a bite's radius is divided by this for a voxel of it (1 — an ordinary one). Owned.</summary>
        private NativeArray<float> _hardness;

        /// <summary>
        /// A blast bites a building only this far (m) as it would anything else; past it, by
        /// <see cref="BUILDING_BITE_SHARE"/> of the rest. A shell's 1.6 m crater is 1.19 m in a wall, an air
        /// bomb's 2 m one 1.46 m; a bullet's 0.3 m hole is the same (user, 2026-11-19: a house should not come
        /// down so quickly from a tank's shells; 2026-11-20: about three times harder than before the skeleton,
        /// not fifteen — at 0.3 a shell's bite was 0.76 m and a bomb's was whole).
        /// </summary>
        public const float BUILDING_BITE_KNEE = 0.4f;

        public const float BUILDING_BITE_SHARE = 0.66f;

        /// <summary>The radius a bite of <paramref name="worldRadius"/> takes out of this body (smaller in a building).</summary>
        public float BiteRadius(float worldRadius) =>
            !HasStrength || worldRadius <= BUILDING_BITE_KNEE
                ? worldRadius
                : BUILDING_BITE_KNEE + (worldRadius - BUILDING_BITE_KNEE) * BUILDING_BITE_SHARE;

        /// <summary>
        /// Gives the body its hardness, one entry per palette index: a bite's radius is divided by it
        /// for a voxel of that entry — a building's load-bearing piers and beams take several times
        /// the hits of the walls between them. The body owns and disposes it; pieces get a copy.
        /// </summary>
        public void SetHardness(NativeArray<float> hardness)
        {
            if (_hardness.IsCreated)
                _hardness.Dispose();
            _hardness = hardness;
        }

        /// <summary>Whether the voxel's palette entry is harder than ordinary (a load-bearing one).</summary>
        public bool IsHard(byte material) => _hardness.IsCreated && material != 0 && _hardness[material] > 1f;

        /// <summary>The voxel at a grid point (0 — empty or outside), for tests.</summary>
        public byte VoxelAt(int3 p) => _voxels.IsCreated && VoxelMath.Inside(p, Size) ? _voxels[VoxelMath.Index(p, Size)] : (byte)0;

        private NativeArray<float> CopyHardness() =>
            _hardness.IsCreated ? new NativeArray<float>(_hardness, Allocator.Persistent) : default;

        /// <summary>
        /// Gives the body its strength table, one entry per palette index (see <see cref="VoxelLoad"/>);
        /// the body owns and disposes it, and pieces that break off get a copy.
        /// </summary>
        public void SetStrength(NativeArray<float3> strength)
        {
            if (_strength.IsCreated)
                _strength.Dispose();
            _strength = strength;
        }

        /// <summary>Whether the voxel at a world point is solid.</summary>
        internal bool IsSolidAt(Vector3 worldPoint)
        {
            if (!_voxels.IsCreated)
                return false;
            int3 p = (int3)math.floor(((float3)transform.InverseTransformPoint(worldPoint) - Origin) / VoxelSize);
            return VoxelMath.Inside(p, Size) && _voxels[VoxelMath.Index(p, Size)] != 0;
        }

        /// <summary>Asks for a load solve of the whole body at the end of this frame (VoxelWorld). Buildings only.</summary>
        public void MarkStructureDirty() => MarkStructureDirtyAt(float3.zero, (float3)Size, true);

        /// <summary>
        /// Asks for a load solve at the end of this frame: the body or the ground under it changed
        /// round a point. Joints break only within <see cref="VoxelLoad.ZONE_MARGIN"/> blocks of the
        /// damage: further off it spreads only by what falls (see AddImpactLoad).
        /// </summary>
        public void MarkStructureDirty(Vector3 worldCenter, float worldRadius, float delay = 0f)
        {
            if (!_voxels.IsCreated)
                return;
            float3 center = ((float3)transform.InverseTransformPoint(worldCenter) - Origin) / VoxelSize;
            float radius = worldRadius / (VoxelSize * Mathf.Abs(transform.lossyScale.x));
            // Damage down by its foot may have taken the ground from under it: probe it again.
            if (center.y - radius < SUPPORT_REPROBE / VoxelSize)
                MarkSupportStale();
            MarkStructureDirtyAt(center, new float3(radius), false, delay);
        }

        /// <summary>
        /// A small bite (a machine-gun bullet's hole) waits this long (s) for its solve: every bullet of a burst
        /// meets in one solve (5-9 ms on a house) rather than one a frame. Anything bigger is solved at once.
        /// </summary>
        public const float MINOR_SOLVE_DELAY = 0.5f;

        /// <summary>Not solved before this Time.time (see MINOR_SOLVE_DELAY).</summary>
        private float _solveNotBefore;

        /// <summary>Whether the queued solve may run now (VoxelWorld).</summary>
        internal bool StructureSolveDue => Time.time >= _solveNotBefore;

        /// <param name="center">In voxels.</param>
        /// <param name="reach">Half extent, in voxels; with <paramref name="whole"/> the box from 0 to it.</param>
        /// <param name="delay">Seconds the solve may wait (MINOR_SOLVE_DELAY for a small bite); 0 — at once.</param>
        internal void MarkStructureDirtyAt(float3 center, float3 reach, bool whole = false, float delay = 0f)
        {
            if (!HasStrength || !IsStatic || !_voxels.IsCreated)
                return;

            int3 lo = whole ? int3.zero : (int3)math.floor(center - reach);
            int3 hi = whole ? Size - 1 : (int3)math.ceil(center + reach);
            _zoneLo = _hasZone ? math.min(_zoneLo, lo) : lo;
            _zoneHi = _hasZone ? math.max(_zoneHi, hi) : hi;
            _hasZone = true;

            // The earliest asked for wins: a blast after a burst's bullets is solved at once.
            float due = Time.time + delay;
            _solveNotBefore = _structureDirty ? Mathf.Min(_solveNotBefore, due) : due;
            Queue();
        }

        private void Queue()
        {
            if (_structureDirty)
                return;
            _structureDirty = true;
            VoxelWorld.QueueStructure(this);
        }

        /// <summary>A piece fell on the body: its hit presses there in the next solve.</summary>
        internal void AddImpactLoad(Vector3 worldPoint, float kg)
        {
            if (!HasStrength || !IsStatic || kg <= 0f)
                return;

            float3 p = ((float3)transform.InverseTransformPoint(worldPoint) - Origin) / VoxelSize;
            if (_impactCount < MAX_IMPACTS)
                _impacts[_impactCount++] = new Vector4(p.x, p.y, p.z, kg);
            MarkStructureDirtyAt(p, new float3(IMPACT_REACH / (VoxelSize * Mathf.Abs(transform.lossyScale.x))));
        }

        /// <summary>
        /// Works out the whole, undamaged building's load and makes every joint bear it with a
        /// margin (StructuralLoadJob.Design): a building stands as it was made, whatever its
        /// panes, sills and chimneys weigh; only damage overloads a joint. Called once, built.
        /// </summary>
        public void DesignStructure()
        {
            if (!HasStrength || !_voxels.IsCreated || SolidCount == 0)
                return;
            RunLoad(true, 0);
        }

        /// <summary>
        /// Works out the load on every joint and lets fall what no longer reaches the ground.
        /// Called by VoxelWorld once a frame for a marked body, before the rebuild, so the pieces
        /// get their colliders this frame and go at its end.
        /// </summary>
        internal void SolveStructure()
        {
            _structureDirty = false;
            int impacts = _impactCount;
            _impactCount = 0;
            if (!HasStrength || !IsStatic || !_voxels.IsCreated || SolidCount == 0)
                return;

            bool unsettled;
            using (SolveMarker.Auto())
                unsettled = RunLoad(false, impacts);

            if (SolidCount <= 0)
            {
                Destroy(gameObject);
                return;
            }

            // Joints still overloaded after the last round: go on next frame (the cracks so far
            // are kept, and the zone) — a collapse spreads over a few frames rather than in one.
            if (unsettled)
            {
                _solveNotBefore = 0f;
                Queue();
            }
            else
                _hasZone = false;
        }

        /// <summary>The design pass or a solve; true when the solve ran out of rounds.</summary>
        private bool RunLoad(bool design, int impacts)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            int3 blocks = VoxelLoad.Blocks(Size);
            int count = blocks.x * blocks.y * blocks.z;
            // The load job packs a block index in 20 bits (its row order key): no building is that big.
            if (count >= 1 << 20)
                return false;
            BlocksMarker.Begin();
            int columns = blocks.x * blocks.z;
            float edge = VoxelSize * Mathf.Abs(transform.lossyScale.x);

            var mass = new NativeArray<float>(count, Allocator.TempJob);
            var bondX = new NativeArray<float2>(count, Allocator.TempJob);
            var bondY = new NativeArray<float2>(count, Allocator.TempJob);
            var bondZ = new NativeArray<float2>(count, Allocator.TempJob);
            var bottom = new NativeArray<byte>(columns, Allocator.TempJob);
            new BuildBlocksJob
            {
                Voxels = _voxels, Size = Size, Strength = _strength, VoxelVolume = edge * edge * edge,
                Blocks = blocks, Mass = mass, BondX = bondX, BondY = bondY, BondZ = bondZ, Bottom = bottom
            }.ScheduleParallel(blocks.z, 1, default).Complete();

            var support = new NativeArray<byte>(columns, Allocator.TempJob);
            FindSupport(blocks, bottom, support);
            BlocksMarker.End();
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();

            var extra = new NativeArray<float>(count, Allocator.TempJob);
            for (int i = 0; i < impacts; i++)
            {
                Vector4 hit = _impacts[i];
                int3 b = math.clamp((int3)math.floor(new float3(hit.x, hit.y, hit.z) / VoxelLoad.BLOCK), 0, blocks - 1);
                int at = SolidNear(b, blocks, mass);
                if (at >= 0)
                    extra[at] += hit.w;
            }

            if (!_cracks.IsCreated)
                _cracks = new NativeArray<byte>(count * 3, Allocator.Persistent);
            if (!_scale.IsCreated)
                _scale = new NativeArray<float>(count * 3, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (!_designed || design)
            {
                // The design pass measures against the bare materials.
                for (int i = 0; i < _scale.Length; i++)
                    _scale[i] = 1f;
                _designed = true;
            }

            // Run here, not scheduled: the islands it finds are cut out of the grid this frame, before
            // the rebuild meshes it (VoxelWorld), so the pieces get their colliders in the same pass.
            var island = new NativeArray<int>(count, Allocator.TempJob);
            var result = new NativeArray<int>(5, Allocator.TempJob);
            LoadMarker.Begin();
            new StructuralLoadJob
            {
                Blocks = blocks, Mass = mass, BondX = bondX, BondY = bondY, BondZ = bondZ, Support = support, Extra = extra,
                Step = VoxelLoad.BLOCK * edge, Broken = _cracks, Scale = _scale, Design = design, Island = island,
                ZoneLo = design || !_hasZone ? new int3(-1) : _zoneLo / VoxelLoad.BLOCK - VoxelLoad.ZONE_MARGIN,
                ZoneHi = design || !_hasZone ? blocks : _zoneHi / VoxelLoad.BLOCK + VoxelLoad.ZONE_MARGIN,
                Result = result
            }.Run();
            LoadMarker.End();
            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
            double tick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            LastBlocksMs = (float)((t1 - t0) * tick);
            LastLoadMs = (float)((t2 - t1) * tick);

            int islands = result[0];
            bool unsettled = result[1] != 0;
            if (!design)
            {
                LastSolveIslands = islands;
                LastSolveWorst = result[2] / 1000f;
                LastSolveReached = result[3];
                if (LogStructure)
                {
                    int slot = result[4];
                    int face = slot / 3;
                    int3 fb = new int3(face % blocks.x, face / blocks.x % blocks.y, face / (blocks.x * blocks.y));
                    Debug.Log($"[Solve] {name}: blocks {count} reached {LastSolveReached} worst {LastSolveWorst:F2} at block {fb} " +
                              $"axis {slot % 3} {BlockMaterials(fb)} islands {islands} unsettled {unsettled} impacts {impacts}"); // perf-ok: tuning log, only with LogStructure (tests)
                }

                // Pieces the frame had no room for go next frame: the islands are found again, the same.
                if (islands > 0 && DropIslands(blocks, island))
                    unsettled = true;
            }

            mass.Dispose();
            bondX.Dispose();
            bondY.Dispose();
            bondZ.Dispose();
            bottom.Dispose();
            support.Dispose();
            extra.Dispose();
            island.Dispose();
            result.Dispose();
            return unsettled;
        }

        /// <summary>Islands found by the last solve (tests and the HUD).</summary>
        /// <summary>Log every solve (tests, tuning).</summary>
        public static bool LogStructure;

        /// <summary>The last solve's main-thread ms: blocks and support, then the load job (tuning).</summary>
        public static float LastBlocksMs, LastLoadMs;

        public int LastSolveIslands { get; private set; }

        /// <summary>The worst joint overload (load / strength) the last solve found before breaking anything.</summary>
        public float LastSolveWorst { get; private set; }

        /// <summary>Blocks the supports reached in the last solve.</summary>
        public int LastSolveReached { get; private set; }

        /// <summary>
        /// Which bottom block columns stand on something: on the ground body under the building
        /// when there is one, otherwise on whatever the bottom layer stands on (as before).
        /// </summary>
        private void FindSupport(int3 blocks, NativeArray<byte> bottom, NativeArray<byte> support)
        {
            // The ground under a building changes only when a crater opens near it (MarkSupportStale):
            // the probes are kept till then.
            int columns = blocks.x * blocks.z;
            if (_groundHeld.IsCreated && !_supportStale)
            {
                for (int c = 0; c < columns; c++)
                    support[c] = (byte)(bottom[c] & _groundHeld[c]);
                return;
            }

            if (!_groundHeld.IsCreated)
                _groundHeld = new NativeArray<byte>(columns, Allocator.Persistent);
            _supportStale = false;

            VoxelBody ground = null;
            Bounds bounds = WorldBounds;
            bounds.Expand(new Vector3(0f, SUPPORT_BELOW * 2f, 0f));
            var bodies = VoxelWorld.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body != null && body != this && body.IsTerrain && body.IsCreated && body.WorldBounds.Intersects(bounds))
                {
                    ground = body;
                    break;
                }
            }

            float step = VoxelSize * VoxelLoad.BLOCK;
            for (int bz = 0; bz < blocks.z; bz++)
            for (int bx = 0; bx < blocks.x; bx++)
            {
                int column = bx + blocks.x * bz;
                if (ground == null)
                {
                    _groundHeld[column] = 1;
                    support[column] = bottom[column];
                    continue;
                }

                // Two corners of the column (inset), at a few heights round the bottom.
                bool held = false;
                for (int corner = 0; corner < 2 && !held; corner++)
                {
                    float u = corner == 0 ? 0.15f : 0.85f;
                    float x = math.min((bx + u) * step, Size.x * VoxelSize - 0.01f);
                    float z = math.min((bz + u) * step, Size.z * VoxelSize - 0.01f);
                    for (float y = -SUPPORT_BELOW; y <= SUPPORT_ABOVE + 0.001f && !held; y += 0.15f)
                    {
                        Vector3 world = transform.TransformPoint((Vector3)Origin + new Vector3(x, y, z));
                        held = ground.IsSolidAt(world);
                    }
                }

                _groundHeld[column] = held ? (byte)1 : (byte)0;
                support[column] = (byte)(bottom[column] & _groundHeld[column]);
            }
        }

        /// <summary>Per bottom block column: the ground was under it when last probed (FindSupport).</summary>
        private NativeArray<byte> _groundHeld;
        private bool _supportStale = true;

        /// <summary>Damage reaching this far (m) above the bottom layer has the ground under the building probed again.</summary>
        private const float SUPPORT_REPROBE = 0.5f;

        /// <summary>The ground near the building changed (a crater): probe it again on the next solve.</summary>
        internal void MarkSupportStale() => _supportStale = true;

        /// <summary>
        /// Takes the islands out as pieces (clods when small, crushed ones as clods where they stood),
        /// cut along their blocks, and whatever the islands alone held to the rest. At most
        /// <see cref="DROP_BUDGET"/> voxels a frame (always one island): a falling house makes its
        /// pieces and their colliders over a few frames, not in one. True when some were left for later.
        /// </summary>
        private bool DropIslands(int3 blocks, NativeArray<int> island)
        {
            RebuildNow();
            using var marker = DropMarker.Auto();
            // Only the islands' voxels are labelled (IslandLabelJob): what falls, not the whole grid.
            var labels = new NativeArray<int>(_voxels.Length, Allocator.TempJob);
            var queue = new NativeList<int>(4096, Allocator.TempJob);
            var components = new NativeList<VoxelComponent>(16, Allocator.TempJob);
            new IslandLabelJob
            {
                Voxels = _voxels, Size = Size, Blocks = blocks, Island = island, Labels = labels, Queue = queue,
                Components = components
            }.Run();

            int dropped = 0;
            bool deferred = false;
            for (int i = 0; i < components.Length; i++)
            {
                VoxelComponent piece = components[i];
                if (piece.Cell == 0)
                    continue;
                if (dropped > 0 && dropped + piece.Count > DROP_BUDGET)
                {
                    deferred = true;
                    continue;
                }

                dropped += piece.Count;

                Vector3 middle = transform.TransformPoint(Origin + ((float3)(piece.Min + piece.Max) * 0.5f + 0.5f) * VoxelSize);
                // A crushed piece, or a scrap too small to be rubble of its own, goes to crumbs where it was.
                if (piece.Cell < 0 || piece.Count < RUBBLE_BODY_MIN)
                    Crush(labels, i + 1, piece);
                else
                    Detach(labels, i + 1, piece, middle, 0f);
                MarkDirty(piece.Min, piece.Max);
                // A scrap the island alone held to the rest goes at the end of the frame (LocalSplit round it).
                DeferSplit(middle, 0f, (float3)(piece.Min + piece.Max) * 0.5f, (float3)(piece.Max - piece.Min) * 0.5f + 1f);
            }

            labels.Dispose();
            queue.Dispose();
            components.Dispose();
            return deferred;
        }

        /// <summary>Most voxels a solve takes out of a building in one frame (about 36 t of brick).</summary>
        private const int DROP_BUDGET = 20000;

        private static readonly Unity.Profiling.ProfilerMarker SolveMarker = new("Voxels.Structure.Solve");
        private static readonly Unity.Profiling.ProfilerMarker BlocksMarker = new("Voxels.Structure.Blocks");
        private static readonly Unity.Profiling.ProfilerMarker LoadMarker = new("Voxels.Structure.Load");
        private static readonly Unity.Profiling.ProfilerMarker DropMarker = new("Voxels.Structure.DropIslands");
        private static readonly Unity.Profiling.ProfilerMarker RubbleMarker = new("Voxels.Rubble.BreakUp");

        /// <summary>
        /// The block a hit at block b presses on: b, or the nearest one with mass under or beside it
        /// (a contact on top of a floor lies in the empty block over it). −1 when none is near.
        /// </summary>
        private static int SolidNear(int3 b, int3 blocks, NativeArray<float> mass)
        {
            for (int k = 0; k < 9; k++)
            {
                int3 c = b + (k switch
                {
                    0 => int3.zero, 1 => new int3(0, -1, 0), 2 => new int3(0, -2, 0),
                    3 => new int3(1, 0, 0), 4 => new int3(-1, 0, 0), 5 => new int3(0, 0, 1), 6 => new int3(0, 0, -1),
                    7 => new int3(0, 1, 0), _ => new int3(0, -3, 0)
                });
                if (math.any(c < 0) || math.any(c >= blocks))
                    continue;
                int index = c.x + blocks.x * (c.y + blocks.y * c.z);
                if (mass[index] > 0f)
                    return index;
            }

            return -1;
        }

        /// <summary>Share of the voxels lining a building piece's cracks that go as crumbs when it breaks.</summary>
        private const float SEAM_CRUMBS = 0.6f;

        /// <summary>Most crumbs one break throws (the rest is dust).</summary>
        private const int SEAM_CRUMB_DEBRIS = 500;

        /// <summary>A building's piece breaking: a share of the voxels along its cracks go as crumbs.</summary>
        private void CrumbleSeams(NativeArray<int> cells, Vector3 worldPoint, float spread)
        {
            var removed = new NativeList<RemovedVoxel>(1024, Allocator.TempJob);
            new SeamCrumbsJob
            {
                Voxels = _voxels, Size = Size, Cells = cells, Share = SEAM_CRUMBS, Seed = _seed++, Removed = removed
            }.Run();

            if (removed.Length > 0)
            {
                SolidCount -= removed.Length;
                MarkDirty(int3.zero, Size - 1);
                ThrowDebris(removed, worldPoint, math.max(1f, spread * 2f), Vector3.zero, SEAM_CRUMB_DEBRIS);
            }

            removed.Dispose();
        }

        /// <summary>Most clods one crushed piece goes to; the rest is dust.</summary>
        private const int CRUSH_DEBRIS = 600;

        /// <summary>Speed (m/s) a crushed piece's clods burst out with.</summary>
        private const float CRUSH_SPEED = 2f;

        /// <summary>
        /// A piece crushed under a load (a post, a pier) goes to clods where it stood: no body is
        /// left standing there to hold up what it bore.
        /// </summary>
        private void Crush(NativeArray<int> labels, int label, VoxelComponent piece)
        {
            var removed = new NativeList<RemovedVoxel>(piece.Count, Allocator.TempJob);
            for (int z = piece.Min.z; z <= piece.Max.z; z++)
            for (int y = piece.Min.y; y <= piece.Max.y; y++)
            for (int x = piece.Min.x; x <= piece.Max.x; x++)
            {
                int index = VoxelMath.Index(new int3(x, y, z), Size);
                if (labels[index] != label)
                    continue;
                removed.Add(new RemovedVoxel { Position = new int3(x, y, z), Material = _voxels[index] });
                _voxels[index] = 0;
            }

            SolidCount -= removed.Length;
            Vector3 middle = transform.TransformPoint(Origin + ((float3)(piece.Min + piece.Max) * 0.5f + 0.5f) * VoxelSize);
            ThrowDebris(removed, middle, CRUSH_SPEED, Vector3.zero, CRUSH_DEBRIS);
            removed.Dispose();
        }

        /// <summary>
        /// A building's scrap smaller than this (voxels, about half a tonne of brick) breaking off goes
        /// to crumbs where it was, not as a body: it neither hangs in the air nor lies about as a lump.
        /// </summary>
        internal const int RUBBLE_BODY_MIN = 250;

        /// <summary>Rubble that has come to rest goes to crumbs and dust when it is this small (voxels)...</summary>
        internal const int RUBBLE_DISSOLVE_MAX = 2500;

        /// <summary>...otherwise it breaks up all through first, into pieces of about this many voxels...</summary>
        private const int RUBBLE_REST_PIECE = 600;

        /// <summary>
        /// ...up to this size (voxels, about 22 t of brick). A bigger one is a whole storey or house that came down
        /// (it breaks where it lands hard, VoxelImpactFracture): broken all at once at rest, its dozens of
        /// pieces shove each other about.
        /// </summary>
        internal const int RUBBLE_BREAK_MAX = 12000;

        /// <summary>Most crumbs a dissolving piece throws (they land, lie and sink, VoxelDebris); the rest is dust.</summary>
        private const int RUBBLE_CRUMBS = 260;

        /// <summary>Speed (m/s) its crumbs slump out with.</summary>
        private const float RUBBLE_SLUMP = 1.2f;

        /// <summary>Most voxels of resting rubble breaking up or dissolving in one frame (a collapse settles over a few).</summary>
        private const int RUBBLE_FRAME_VOXELS = 8000;

        private static int s_rubbleFrame = -1;
        private static int s_rubbleVoxels;

        /// <summary>
        /// A building's piece that has come to rest (VoxelImpactFracture): a small one goes to crumbs,
        /// which lie and sink, and dust; a bigger one breaks up all through, and its pieces do the same
        /// when they come to rest. No lumps of house are left lying about. False when it is too big
        /// (RUBBLE_BREAK_MAX) or the frame had no room for it (it tries again later).
        /// </summary>
        internal bool BreakUpRubble()
        {
            if (!HasStrength || !_voxels.IsCreated || !IsLoose || SolidCount == 0 || SolidCount > RUBBLE_BREAK_MAX)
                return false;
            if (Time.frameCount != s_rubbleFrame)
            {
                s_rubbleFrame = Time.frameCount;
                s_rubbleVoxels = 0;
            }

            if (s_rubbleVoxels > 0 && s_rubbleVoxels + SolidCount > RUBBLE_FRAME_VOXELS)
                return false;
            s_rubbleVoxels += SolidCount;
            using var marker = RubbleMarker.Auto();

            Vector3 middle = WorldBounds.center;
            if (SolidCount > RUBBLE_DISSOLVE_MAX)
                return Fracture(middle, math.clamp(SolidCount / RUBBLE_REST_PIECE, 2, 48), 0.2f, true);

            var removed = new NativeList<RemovedVoxel>(SolidCount, Allocator.TempJob);
            new GatherSolidJob { Voxels = _voxels, Size = Size, Removed = removed }.Run();
            SolidCount = 0;
            ThrowDebris(removed, middle + Vector3.up * 0.3f, RUBBLE_SLUMP, Vector3.zero, RUBBLE_CRUMBS);
            removed.Dispose();
            Destroy(gameObject);
            return true;
        }

        /// <summary>
        /// Fracture cells along the body's weak joints (see SeamCellsJob), one per voxel, for a
        /// body with a strength table; the caller disposes them.
        /// </summary>
        private NativeArray<int> SeamCells(NativeArray<float3> seeds)
        {
            int3 blocks = VoxelLoad.Blocks(Size);
            int count = blocks.x * blocks.y * blocks.z;
            float edge = VoxelSize * Mathf.Abs(transform.lossyScale.x);

            var mass = new NativeArray<float>(count, Allocator.TempJob);
            var bondX = new NativeArray<float2>(count, Allocator.TempJob);
            var bondY = new NativeArray<float2>(count, Allocator.TempJob);
            var bondZ = new NativeArray<float2>(count, Allocator.TempJob);
            var bottom = new NativeArray<byte>(blocks.x * blocks.z, Allocator.TempJob);
            new BuildBlocksJob
            {
                Voxels = _voxels, Size = Size, Strength = _strength, VoxelVolume = edge * edge * edge,
                Blocks = blocks, Mass = mass, BondX = bondX, BondY = bondY, BondZ = bondZ, Bottom = bottom
            }.ScheduleParallel(blocks.z, 1, default).Complete();

            var blockCell = new NativeArray<int>(count, Allocator.TempJob);
            new SeamCellsJob
            {
                Blocks = blocks, Mass = mass, BondX = bondX, BondY = bondY, BondZ = bondZ, Seeds = seeds, BlockCell = blockCell
            }.Run();

            var cells = new NativeArray<int>(_voxels.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            new BlockCellsJob { Size = Size, Blocks = blocks, BlockCell = blockCell, Cells = cells }.Run();

            mass.Dispose();
            bondX.Dispose();
            bondY.Dispose();
            bondZ.Dispose();
            bottom.Dispose();
            blockCell.Dispose();
            return cells;
        }

        /// <summary>Debug: the materials (palette index − 1) / 4 in a block, for the solve log.</summary>
        private string BlockMaterials(int3 block)
        {
            var seen = new System.Collections.Generic.SortedSet<int>();
            int3 lo = block * VoxelLoad.BLOCK;
            int3 hi = math.min(lo + VoxelLoad.BLOCK, Size);
            for (int z = lo.z; z < hi.z; z++)
            for (int y = lo.y; y < hi.y; y++)
            for (int x = lo.x; x < hi.x; x++)
            {
                byte v = _voxels[VoxelMath.Index(new int3(x, y, z), Size)];
                if (v != 0)
                    seen.Add((v - 1) / MeshVoxelizer.VARIANTS);
            }

            return "mats[" + string.Join(",", seen) + "]";
        }

        private void ReleaseStructure()
        {
            ReleaseGlass();
            if (_cracks.IsCreated)
                _cracks.Dispose();
            if (_groundHeld.IsCreated)
                _groundHeld.Dispose();
            _supportStale = true;
            if (_scale.IsCreated)
                _scale.Dispose();
            if (_strength.IsCreated)
                _strength.Dispose();
            if (_hardness.IsCreated)
                _hardness.Dispose();
            if (_structureDirty)
                VoxelWorld.UnqueueStructure(this);
            _structureDirty = false;
            _hasZone = false;
            _impactCount = 0;
            _designed = false;
        }

        /// <summary>A copy of the strength table for a piece breaking off, or none.</summary>
        private NativeArray<float3> CopyStrength() =>
            _strength.IsCreated ? new NativeArray<float3>(_strength, Allocator.Persistent) : default;
    }

    /// <summary>Every solid voxel of a grid, taken out and listed (a piece of rubble going to crumbs).</summary>
    [BurstCompile]
    internal struct GatherSolidJob : IJob
    {
        public NativeArray<byte> Voxels;
        public int3 Size;
        public NativeList<RemovedVoxel> Removed;

        public void Execute()
        {
            for (int i = 0; i < Voxels.Length; i++)
            {
                byte material = Voxels[i];
                if (material == 0)
                    continue;
                Voxels[i] = 0;
                Removed.Add(new RemovedVoxel { Position = VoxelMath.Coord(i, Size), Material = material });
            }
        }
    }
}

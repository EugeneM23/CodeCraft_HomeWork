using System.Collections.Generic;
using Game.Voxels;
using Unity.Mathematics;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Works out an arena from its definition and the run's seed: where it stands in the world, the
    /// ground's shape, the houses, trees, fences, gate, burrows and cover. Pure C#: the same
    /// definition and seed always give the same plan, so it is tested without a scene.
    /// </summary>
    public static class ArenaPlanner
    {
        private const float HALF = ArenaPlan.SIZE * 0.5f;
        private const float VOXEL = ArenaPlan.VOXEL;

        // Where the arena stands: the mud and grass are noise in world metres.
        private const int CENTRE_CANDIDATES = 12;
        private const int CENTRE_RANGE = 1500;
        private const float MUD_GRID = 5f;
        private const float NOISE_ORIGIN = 500f;

        private const float FREQUENCY = 0.045f;
        private const float FREQUENCY_SPREAD = 0.005f;

        // The entry in the south, the gate in the north edge.
        private const float ENTRY_INSET = 4f;
        private const float ENTRY_JITTER = 4f;
        private const float GATE_JITTER = 10f;
        private const float EXIT_PAST = 3.5f;
        private const float ENTRY_PAD = 4f;
        private const float GATE_PAD = 4.5f;
        private static readonly Vector3Int GATE_SIZE = new(70, 26, 6);

        /// <summary>Half the width of the straight lane from the entry to the gate kept clear.</summary>
        private const float LANE_HALF = 2.5f;

        // Houses.
        private static readonly Vector3Int VICTORIAN = new(64, 78, 54);
        private static readonly Vector3Int VICTORIAN_TALL = new(64, 106, 54);
        private static readonly Vector3Int HOUSE = new(60, 44, 50);
        private const float HOUSE_EDGE = 3f;
        /// <summary>
        /// Between footprints: a pad blends into the hills for PAD_BLEND (4 m) past its radius, which
        /// is itself 1-2 m past the walls; nearer, one house's blend would tilt the ground at the other.
        /// </summary>
        private const float HOUSE_GAP = 6.5f;
        private const float HOUSE_FROM_ENTRY = 7f;
        private const float HOUSE_FROM_GATE = 6f;
        private const int HOUSE_TRIES = 64;
        private const float COVER_PAST = 1.5f;
        private const float COVER_EDGE = 1f;

        // Trees.
        private const float TREE_EDGE = 2f;
        private const float TREE_FROM_HOUSE = 3f;
        private const float TREE_FROM_ENTRY = 6f;
        private const float TREE_FROM_GATE = 5f;
        private const float TREE_GAP = 3f;
        private const float TREE_FROM_FENCE = 1.5f;
        private const float OUTSIDE_NEAR = 1f;
        private const float OUTSIDE_FAR = 5f;
        private const float OUTSIDE_FROM_GATE = 6f;
        private const int TREE_TRIES = 40;

        // Fences.
        private const float YARD_OUT = 2.5f;
        private const float YARD_OVERHANG = 1f;
        private const float YARD_GAP = 3f;
        private const float FENCE_EDGE = 1f;
        private const float FENCE_FROM_ENTRY = 4f;
        private const float FENCE_FROM_GATE = 4f;
        private const float FENCE_FROM_HOUSE = 0.25f;
        private const float FENCE_GAP = 1f;
        private const float FREE_FENCE_SHORTEST = 4f;
        private const float FREE_FENCE_LONGEST = 9f;
        private const int FENCE_TRIES = 30;

        // Burrows.
        private const float BURROW_INSET = 1.5f;
        private const float BURROW_STEP = 4f;
        private const float BURROW_FROM_GATE = 6f;
        private const float BURROW_FROM_HOUSE = 2.5f;
        private const float BURROW_FROM_TREE = 2f;
        private const float BURROW_FROM_FENCE = 1.5f;
        private const float BURROW_FROM_ENTRY = 10f;

        /// <summary>The arena for this definition and seed.</summary>
        public static ArenaPlan Plan(ArenaDefinition definition, int seed)
        {
            if (definition == null)
                throw new System.ArgumentNullException(nameof(definition));

            var rng = new System.Random(Mix(seed, definition.Number));
            var plan = new ArenaPlan { Definition = definition, Seed = seed };

            // The hills are fixed relative to the arena's middle; the mud fields are not.
            float frequency = FREQUENCY + Range(rng, -FREQUENCY_SPREAD, FREQUENCY_SPREAD);
            var origin = new Vector2(Range(rng, -NOISE_ORIGIN, NOISE_ORIGIN), Range(rng, -NOISE_ORIGIN, NOISE_ORIGIN));
            Vector2 centre = PickCentre(rng, definition, frequency, origin);
            plan.Centre = new Vector3(centre.x, 0f, centre.y);
            plan.Terrain = Shape(definition.Relief, frequency, origin, centre);

            var entry = new Vector2(centre.x + Range(rng, -ENTRY_JITTER, ENTRY_JITTER), centre.y - HALF + ENTRY_INSET);
            var gate = new Vector2(centre.x + Range(rng, -GATE_JITTER, GATE_JITTER), centre.y + HALF);

            PlaceHouses(rng, plan, entry, gate);
            plan.Pads = Pads(plan, entry, gate);

            plan.Entry = plan.OnGround(entry.x, entry.y);
            Vector3 wall = plan.OnGround(gate.x, gate.y);
            plan.Gate = new GatePlan { Position = wall, Size = GATE_SIZE, Exit = wall + new Vector3(0f, 0f, EXIT_PAST) };

            PlaceYardFences(rng, plan, entry, gate);
            PlaceFreeFences(rng, plan, entry, gate);
            PlaceTrees(rng, plan, entry, gate);
            PlaceOutsideTrees(rng, plan, gate);
            PlaceBurrows(plan, entry, gate);
            PlaceCover(plan, entry);
            return plan;
        }

        // ---------------------------------------------------------------- the ground

        /// <summary>
        /// Of a few world points, the one whose square has the mud share nearest the definition's:
        /// the mud fields are noise in world metres, so moving the arena changes them.
        /// </summary>
        private static Vector2 PickCentre(System.Random rng, ArenaDefinition definition, float frequency, Vector2 origin)
        {
            Vector2 best = default;
            float bestMiss = float.MaxValue;
            for (int i = 0; i < CENTRE_CANDIDATES; i++)
            {
                var candidate = new Vector2(rng.Next(-CENTRE_RANGE, CENTRE_RANGE + 1), rng.Next(-CENTRE_RANGE, CENTRE_RANGE + 1));
                TerrainShape shape = Shape(definition.Relief, frequency, origin, candidate);
                float miss = Mathf.Abs(MudShare(candidate, shape) - definition.Mud);
                if (miss < bestMiss)
                {
                    bestMiss = miss;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>The ground's shape with the noise <paramref name="origin"/> at the arena's middle.</summary>
        private static TerrainShape Shape(float relief, float frequency, Vector2 origin, Vector2 centre) => new()
        {
            Base = 0f,
            Amplitude = relief,
            Frequency = frequency,
            Offset = origin - centre * frequency
        };

        /// <summary>Share (0..1) of the square round <paramref name="centre"/> under mud, on a coarse grid, without pads.</summary>
        public static float MudShare(Vector2 centre, in TerrainShape shape)
        {
            int mud = 0, all = 0;
            for (float dz = -HALF; dz <= HALF + 0.01f; dz += MUD_GRID)
            for (float dx = -HALF; dx <= HALF + 0.01f; dx += MUD_GRID)
            {
                var xz = new Vector2(centre.x + dx, centre.y + dz);
                float height = VoxelTerrain.Height(xz, shape, (Vector4[])null);
                float slope = VoxelTerrain.Slope(xz, shape, (Vector4[])null);
                if (VoxelTerrain.MudAmount(new float2(xz.x, xz.y), height, slope, shape) >= 0.5f)
                    mud++;
                all++;
            }
            return (float)mud / all;
        }

        /// <summary>Flat pads under the entry, the gate and every house; houses are lifted onto theirs.</summary>
        private static Vector4[] Pads(ArenaPlan plan, Vector2 entry, Vector2 gate)
        {
            var pads = new Vector4[plan.Houses.Count + 2];
            pads[0] = Pad(entry, ENTRY_PAD, plan.Terrain);
            pads[1] = Pad(gate, GATE_PAD, plan.Terrain);
            for (int i = 0; i < plan.Houses.Count; i++)
            {
                HousePlan house = plan.Houses[i];
                Rect footprint = house.Footprint;
                float radius = footprint.size.magnitude * 0.5f + 1f;
                Vector4 pad = Pad(footprint.center, radius, plan.Terrain);
                pads[i + 2] = pad;
                house.Position.y = pad.w;
                plan.Houses[i] = house;
            }
            return pads;
        }

        private static Vector4 Pad(Vector2 at, float radius, in TerrainShape shape) =>
            new(at.x, at.y, radius, VoxelTerrain.RawHeight(new float2(at.x, at.y), shape));

        // ---------------------------------------------------------------- houses

        private static void PlaceHouses(System.Random rng, ArenaPlan plan, Vector2 entry, Vector2 gate)
        {
            ArenaDefinition definition = plan.Definition;
            Vector2 centre = Flat(plan.Centre);
            int fewest = Mathf.Max(0, definition.Houses.x);
            int count = rng.Next(fewest, Mathf.Max(fewest, definition.Houses.y) + 1);

            for (int i = 0; i < count; i++)
            {
                PickHouse(rng, out VoxelStructure.Shape shape, out Vector3Int size);
                var extent = new Vector2(size.x * VOXEL, size.z * VOXEL);
                int houseSeed = rng.Next();
                bool special = i == 0 && definition.Layout != ArenaLayout.Scattered;

                // Arena 4: the house stands in the middle of the way, the gate behind it.
                if (special && definition.Layout == ArenaLayout.HouseBlocksGate)
                {
                    Vector2 at = Vector2.Lerp(entry, gate, Range(rng, 0.55f, 0.65f));
                    AddHouse(plan, shape, size, at, extent, houseSeed);
                    continue;
                }

                bool placed = false;
                for (int attempt = 0; attempt < HOUSE_TRIES; attempt++)
                {
                    Vector2 at;
                    if (special)
                    {
                        // Arena 3: in the north half, off to one side of the way to the gate.
                        float z = centre.y + Range(rng, 2f, 10f);
                        float lineX = Mathf.Lerp(entry.x, gate.x, (z - entry.y) / (gate.y - entry.y));
                        float side = rng.Next(2) == 0 ? -1f : 1f;
                        at = new Vector2(lineX + side * Range(rng, 4f, 10f), z);
                    }
                    else
                    {
                        float reachX = HALF - HOUSE_EDGE - extent.x * 0.5f;
                        float reachZ = HALF - HOUSE_EDGE - extent.y * 0.5f;
                        at = new Vector2(centre.x + Range(rng, -reachX, reachX), centre.y + Range(rng, -reachZ, reachZ));
                    }

                    if (HouseFits(plan, Footprint(at, extent), entry, gate, !special))
                    {
                        AddHouse(plan, shape, size, at, extent, houseSeed);
                        placed = true;
                        break;
                    }
                }

                // Arena 3 must have its cover house: the first house, so nothing stands in the way yet.
                if (!placed && special)
                {
                    float z = centre.y + 6f;
                    float lineX = Mathf.Lerp(entry.x, gate.x, (z - entry.y) / (gate.y - entry.y));
                    float side = lineX <= centre.x ? 1f : -1f;
                    AddHouse(plan, shape, size, new Vector2(lineX + side * 7f, z), extent, houseSeed);
                }
            }
        }

        /// <summary>Mostly a Victorian house of two storeys, sometimes three; now and then a plain one.</summary>
        private static void PickHouse(System.Random rng, out VoxelStructure.Shape shape, out Vector3Int size)
        {
            if (rng.NextDouble() < 0.25)
            {
                shape = VoxelStructure.Shape.House;
                size = HOUSE;
                return;
            }
            shape = VoxelStructure.Shape.Victorian;
            size = rng.NextDouble() < 0.25 ? VICTORIAN_TALL : VICTORIAN;
        }

        private static bool HouseFits(ArenaPlan plan, Rect footprint, Vector2 entry, Vector2 gate, bool keepLane)
        {
            if (!Inset(Square(plan), HOUSE_EDGE).Contains(footprint.min) || !Inset(Square(plan), HOUSE_EDGE).Contains(footprint.max))
                return false;
            if (PointRectDistance(entry, footprint) < HOUSE_FROM_ENTRY || PointRectDistance(gate, footprint) < HOUSE_FROM_GATE)
                return false;
            if (keepLane && SegmentRectDistance(entry, gate, footprint) < LANE_HALF)
                return false;
            foreach (HousePlan other in plan.Houses)
                if (RectDistance(footprint, other.Footprint) < HOUSE_GAP)
                    return false;
            return true;
        }

        private static void AddHouse(ArenaPlan plan, VoxelStructure.Shape shape, Vector3Int size, Vector2 at, Vector2 extent, int seed) =>
            plan.Houses.Add(new HousePlan
            {
                Shape = shape,
                Size = size,
                Position = new Vector3(at.x, 0f, at.y),
                Seed = seed,
                Footprint = Footprint(at, extent)
            });

        /// <summary>Two spots just past each house as seen from the entry, where a Spitter is out of the tank's sight.</summary>
        private static void PlaceCover(ArenaPlan plan, Vector2 entry)
        {
            Rect inside = Inset(Square(plan), COVER_EDGE);
            foreach (HousePlan house in plan.Houses)
            {
                Rect footprint = house.Footprint;
                Vector2 away = footprint.center - entry;
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 spot;
                    if (Mathf.Abs(away.y) >= Mathf.Abs(away.x))
                        spot = new Vector2(footprint.center.x + k * footprint.width / 3f,
                            away.y >= 0f ? footprint.yMax + COVER_PAST : footprint.yMin - COVER_PAST);
                    else
                        spot = new Vector2(away.x >= 0f ? footprint.xMax + COVER_PAST : footprint.xMin - COVER_PAST,
                            footprint.center.y + k * footprint.height / 3f);
                    if (inside.Contains(spot))
                        plan.Cover.Add(plan.OnGround(spot.x, spot.y));
                }
            }
        }

        // ---------------------------------------------------------------- fences

        /// <summary>Round each house's yard: one or two sides other than the front, sometimes with a gap.</summary>
        private static void PlaceYardFences(System.Random rng, ArenaPlan plan, Vector2 entry, Vector2 gate)
        {
            foreach (HousePlan house in plan.Houses)
            {
                Rect footprint = house.Footprint;
                int first = rng.Next(3);
                int second = rng.Next(2) == 0 ? -1 : (first + 1 + rng.Next(2)) % 3;
                for (int side = 0; side < 3; side++)
                {
                    if (side != first && side != second)
                        continue;

                    // 0 — the back (north), 1 — the west, 2 — the east. The front faces -Z: left open.
                    bool alongZ = side != 0;
                    Vector2 start = side switch
                    {
                        0 => new Vector2(footprint.xMin - YARD_OVERHANG, footprint.yMax + YARD_OUT),
                        1 => new Vector2(footprint.xMin - YARD_OUT, footprint.yMin - YARD_OVERHANG),
                        _ => new Vector2(footprint.xMax + YARD_OUT, footprint.yMin - YARD_OVERHANG)
                    };
                    float length = (alongZ ? footprint.height : footprint.width) + 2f * YARD_OVERHANG;
                    Vector2 axis = alongZ ? Vector2.up : Vector2.right;

                    if (rng.NextDouble() < 0.5)
                    {
                        float at = Range(rng, 1f, length - 1f - YARD_GAP);
                        TryAddFence(plan, start, at, alongZ, rng.Next(), entry, gate);
                        TryAddFence(plan, start + axis * (at + YARD_GAP), length - at - YARD_GAP, alongZ, rng.Next(), entry, gate);
                    }
                    else
                    {
                        TryAddFence(plan, start, length, alongZ, rng.Next(), entry, gate);
                    }
                }
            }
        }

        /// <summary>Runs of fence on their own, somewhere in the square.</summary>
        private static void PlaceFreeFences(System.Random rng, ArenaPlan plan, Vector2 entry, Vector2 gate)
        {
            Vector2Int range = plan.Definition.Fences;
            int fewest = Mathf.Max(0, range.x);
            int count = rng.Next(fewest, Mathf.Max(fewest, range.y) + 1);
            Vector2 centre = Flat(plan.Centre);
            float reach = HALF - FENCE_EDGE;

            for (int i = 0; i < count; i++)
            {
                int fenceSeed = rng.Next();
                for (int attempt = 0; attempt < FENCE_TRIES; attempt++)
                {
                    bool alongZ = rng.Next(2) == 0;
                    float length = Range(rng, FREE_FENCE_SHORTEST, FREE_FENCE_LONGEST);
                    float along = Range(rng, -reach, reach - length);
                    float across = Range(rng, -reach, reach);
                    Vector2 start = centre + (alongZ ? new Vector2(across, along) : new Vector2(along, across));
                    if (TryAddFence(plan, start, length, alongZ, fenceSeed, entry, gate))
                        break;
                }
            }
        }

        private static bool TryAddFence(ArenaPlan plan, Vector2 start, float length, bool alongZ, int seed, Vector2 entry, Vector2 gate)
        {
            Vector2 end = FenceEnd(start, length, alongZ);
            Rect inside = Inset(Square(plan), FENCE_EDGE);
            if (length <= 0f || !inside.Contains(start) || !inside.Contains(end))
                return false;
            if (PointSegmentDistance(entry, start, end) < FENCE_FROM_ENTRY || PointSegmentDistance(gate, start, end) < FENCE_FROM_GATE)
                return false;
            foreach (HousePlan house in plan.Houses)
                if (SegmentRectDistance(start, end, house.Footprint) < FENCE_FROM_HOUSE)
                    return false;
            foreach (FencePlan other in plan.Fences)
                if (SegmentRectDistance(start, end, FenceRect(other)) < FENCE_GAP)
                    return false;

            plan.Fences.Add(new FencePlan { Start = plan.OnGround(start.x, start.y), Length = length, AlongZ = alongZ, Seed = seed });
            return true;
        }

        private static Vector2 FenceEnd(Vector2 start, float length, bool alongZ) =>
            start + (alongZ ? new Vector2(0f, length) : new Vector2(length, 0f));

        /// <summary>A fence run as a rectangle with no width, for distances.</summary>
        private static Rect FenceRect(in FencePlan fence)
        {
            Vector2 start = Flat(fence.Start);
            return Rect.MinMaxRect(start.x, start.y, start.x + (fence.AlongZ ? 0f : fence.Length), start.y + (fence.AlongZ ? fence.Length : 0f));
        }

        // ---------------------------------------------------------------- trees

        /// <summary>Trees inside the square, off the houses, the fences and the way from the entry to the gate.</summary>
        private static void PlaceTrees(System.Random rng, ArenaPlan plan, Vector2 entry, Vector2 gate)
        {
            Vector2Int range = plan.Definition.Trees;
            int fewest = Mathf.Max(0, range.x);
            int count = rng.Next(fewest, Mathf.Max(fewest, range.y) + 1);
            Vector2 centre = Flat(plan.Centre);
            float reach = HALF - TREE_EDGE;

            for (int i = 0; i < count; i++)
            {
                Vector3Int size = TreeSize(rng);
                int treeSeed = rng.Next();
                for (int attempt = 0; attempt < TREE_TRIES; attempt++)
                {
                    var at = new Vector2(centre.x + Range(rng, -reach, reach), centre.y + Range(rng, -reach, reach));
                    if (!TreeFits(plan, at, entry, gate))
                        continue;
                    plan.Trees.Add(new TreePlan { Position = plan.OnGround(at.x, at.y), Size = size, Seed = treeSeed });
                    break;
                }
            }
        }

        private static bool TreeFits(ArenaPlan plan, Vector2 at, Vector2 entry, Vector2 gate)
        {
            if (Vector2.Distance(at, entry) < TREE_FROM_ENTRY || Vector2.Distance(at, gate) < TREE_FROM_GATE)
                return false;
            if (PointSegmentDistance(at, entry, gate) < LANE_HALF)
                return false;
            foreach (HousePlan house in plan.Houses)
                if (PointRectDistance(at, house.Footprint) < TREE_FROM_HOUSE)
                    return false;
            foreach (FencePlan fence in plan.Fences)
                if (PointRectDistance(at, FenceRect(fence)) < TREE_FROM_FENCE)
                    return false;
            foreach (TreePlan tree in plan.Trees)
                if (Vector2.Distance(at, Flat(tree.Position)) < TREE_GAP)
                    return false;
            return true;
        }

        /// <summary>Scenery in the band round the square, leaving the way out past the gate clear.</summary>
        private static void PlaceOutsideTrees(System.Random rng, ArenaPlan plan, Vector2 gate)
        {
            Vector2 centre = Flat(plan.Centre);
            int count = rng.Next(10, 17);
            float far = HALF + OUTSIDE_FAR;
            int placed = 0;

            for (int attempt = 0; attempt < count * TREE_TRIES && placed < count; attempt++)
            {
                var offset = new Vector2(Range(rng, -far, far), Range(rng, -far, far));
                float outside = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)) - HALF;
                if (outside < OUTSIDE_NEAR || outside > OUTSIDE_FAR)
                    continue;
                Vector2 at = centre + offset;
                if (offset.y > HALF && Mathf.Abs(at.x - gate.x) < OUTSIDE_FROM_GATE)
                    continue;

                bool crowded = false;
                foreach (TreePlan tree in plan.Trees)
                    if (tree.Outside && Vector2.Distance(at, Flat(tree.Position)) < TREE_GAP)
                    {
                        crowded = true;
                        break;
                    }
                if (crowded)
                    continue;

                plan.Trees.Add(new TreePlan { Position = plan.OnGround(at.x, at.y), Size = TreeSize(rng), Seed = rng.Next(), Outside = true });
                placed++;
            }
        }

        private static Vector3Int TreeSize(System.Random rng) => new(40, 90 + rng.Next(-20, 21), 40);

        // ---------------------------------------------------------------- burrows

        /// <summary>
        /// Burrows just inside the edge, spread round it: none at the gate, by the entry, or against
        /// a house, tree or fence.
        /// </summary>
        private static void PlaceBurrows(ArenaPlan plan, Vector2 entry, Vector2 gate)
        {
            Vector2 centre = Flat(plan.Centre);
            float inset = HALF - BURROW_INSET;
            float perimeter = 8f * inset;
            int steps = Mathf.Max(4, Mathf.RoundToInt(perimeter / BURROW_STEP));

            var candidates = new List<Vector2>(steps);
            for (int i = 0; i < steps; i++)
            {
                Vector2 at = centre + PerimeterPoint(inset, perimeter * i / steps);
                if (BurrowFits(plan, at, entry, gate))
                    candidates.Add(at);
            }

            // Farthest-point picking: start from the burrow farthest from the entry, then each next
            // as far as it can be from all picked so far.
            int wanted = Mathf.Min(plan.Definition.Burrows, candidates.Count);
            var picked = new List<int>(wanted);
            var nearest = new float[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                nearest[i] = float.MaxValue;

            int next = -1;
            float farthest = -1f;
            for (int i = 0; i < candidates.Count; i++)
            {
                float d = Vector2.Distance(candidates[i], entry);
                if (d > farthest)
                {
                    farthest = d;
                    next = i;
                }
            }

            while (picked.Count < wanted && next >= 0)
            {
                picked.Add(next);
                for (int i = 0; i < candidates.Count; i++)
                    nearest[i] = Mathf.Min(nearest[i], Vector2.Distance(candidates[i], candidates[next]));

                next = -1;
                farthest = -1f;
                for (int i = 0; i < candidates.Count; i++)
                    if (!picked.Contains(i) && nearest[i] > farthest)
                    {
                        farthest = nearest[i];
                        next = i;
                    }
            }

            // Round the edge in order: easier to read in the Inspector and the gizmos.
            picked.Sort();
            foreach (int i in picked)
                plan.Burrows.Add(plan.OnGround(candidates[i].x, candidates[i].y));
        }

        private static bool BurrowFits(ArenaPlan plan, Vector2 at, Vector2 entry, Vector2 gate)
        {
            if (Vector2.Distance(at, gate) < BURROW_FROM_GATE || Vector2.Distance(at, entry) < BURROW_FROM_ENTRY)
                return false;
            foreach (HousePlan house in plan.Houses)
                if (PointRectDistance(at, house.Footprint) < BURROW_FROM_HOUSE)
                    return false;
            foreach (TreePlan tree in plan.Trees)
                if (!tree.Outside && Vector2.Distance(at, Flat(tree.Position)) < BURROW_FROM_TREE)
                    return false;
            foreach (FencePlan fence in plan.Fences)
                if (PointRectDistance(at, FenceRect(fence)) < BURROW_FROM_FENCE)
                    return false;
            return true;
        }

        /// <summary>A point <paramref name="along"/> metres round a square of half-edge <paramref name="half"/>, from its south-west corner anticlockwise.</summary>
        private static Vector2 PerimeterPoint(float half, float along)
        {
            float edge = 2f * half;
            int side = Mathf.Min(3, (int)(along / edge));
            float t = along - side * edge;
            return side switch
            {
                0 => new Vector2(-half + t, -half),
                1 => new Vector2(half, -half + t),
                2 => new Vector2(half - t, half),
                _ => new Vector2(-half, half - t)
            };
        }

        // ---------------------------------------------------------------- geometry

        /// <summary>The playable square (world X/Z).</summary>
        public static Rect Square(ArenaPlan plan) =>
            new(plan.Centre.x - HALF, plan.Centre.z - HALF, ArenaPlan.SIZE, ArenaPlan.SIZE);

        public static Rect Footprint(Vector2 centre, Vector2 size) => new(centre - size * 0.5f, size);

        public static Rect Inset(Rect rect, float by) =>
            Rect.MinMaxRect(rect.xMin + by, rect.yMin + by, rect.xMax - by, rect.yMax - by);

        public static Vector2 Flat(Vector3 point) => new(point.x, point.z);

        /// <summary>Gap between two rectangles; 0 when they touch or overlap.</summary>
        public static float RectDistance(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance from a point to a rectangle; 0 inside or on it.</summary>
        public static float PointRectDistance(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.xMin - p.x, p.x - r.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(r.yMin - p.y, p.y - r.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Distance from a segment to a rectangle; 0 when the segment touches or crosses it.</summary>
        public static float SegmentRectDistance(Vector2 a, Vector2 b, Rect r)
        {
            var c0 = new Vector2(r.xMin, r.yMin);
            var c1 = new Vector2(r.xMax, r.yMin);
            var c2 = new Vector2(r.xMax, r.yMax);
            var c3 = new Vector2(r.xMin, r.yMax);
            if (SegmentsCross(a, b, c0, c1) || SegmentsCross(a, b, c1, c2) || SegmentsCross(a, b, c2, c3) || SegmentsCross(a, b, c3, c0))
                return 0f;

            // Not crossing: the nearest pair is an end of the segment against the rectangle, or a corner against the segment.
            float d = Mathf.Min(PointRectDistance(a, r), PointRectDistance(b, r));
            d = Mathf.Min(d, Mathf.Min(PointSegmentDistance(c0, a, b), PointSegmentDistance(c1, a, b)));
            return Mathf.Min(d, Mathf.Min(PointSegmentDistance(c2, a, b), PointSegmentDistance(c3, a, b)));
        }

        private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c), d2 = Cross(d - c, b - c);
            float d3 = Cross(b - a, c - a), d4 = Cross(b - a, d - a);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        // ---------------------------------------------------------------- randomness

        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>One random stream per (seed, arena number): the same arena of the same run, the same plan.</summary>
        private static int Mix(int seed, int number)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)number * 0x85EBCA77u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return (int)(h & 0x7FFFFFFFu);
            }
        }
    }
}

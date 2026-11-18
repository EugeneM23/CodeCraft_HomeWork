using System.Collections.Generic;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Puts an <see cref="ArenaPlan"/> into the scene under one root: the voxel ground (rolling, with
    /// its mud), houses, trees, fences, the gate, unseen barriers round the edge and a flat backdrop
    /// beyond the ground. Taking the arena down is destroying the root.
    /// </summary>
    public static class ArenaBuilder
    {
        public const int GROUND_LAYER = 3;
        public const int BUILDING_LAYER = 0;

        /// <summary>
        /// The ground is laid in this many tiles a side: their voxel jobs run side by side on the
        /// workers (one tile of 52 × 52 m took about 300 ms on one thread).
        /// </summary>
        private const int GROUND_TILES = 2;

        private static readonly ProfilerMarker VoxelsMarker = new("Arena.BuildVoxels");
        private static readonly ProfilerMarker PropsMarker = new("Arena.BuildProps");

        private static readonly List<VoxelStructure> Structures = new();

        /// <param name="parent">The scene object the root goes under.</param>
        /// <param name="tank">The tank's body: the gate's exit trigger waits for it.</param>
        /// <param name="backdrop">Material of the flat ground beyond the voxel ground; null — none.</param>
        public static GameObject Build(ArenaPlan plan, Transform parent, Rigidbody tank, Material backdrop, out ArenaGate gate)
        {
            var root = new GameObject($"Arena_{plan.Definition.Number:00}_{plan.Seed}");
            root.transform.SetParent(parent, false);
            root.transform.position = Vector3.zero;

            // The ground tiles find their hills on their parent.
            var terrain = root.AddComponent<VoxelTerrainSettings>();
            terrain.Set(plan.Terrain, plan.Pads);

            Structures.Clear();
            AddGround(plan, root.transform);
            foreach (HousePlan house in plan.Houses)
                AddStructure(root.transform, "House", house.Shape, house.Size, house.Seed, house.Position, BUILDING_LAYER);

            // A tree's foot is sunk a little: on a slope its trunk would hang over the low side.
            foreach (TreePlan tree in plan.Trees)
                AddStructure(root.transform, "Tree", VoxelStructure.Shape.Tree, tree.Size, tree.Seed,
                    tree.Position + Vector3.down * 0.2f, BUILDING_LAYER);

            var gateObject = new GameObject("Gate");
            gateObject.transform.SetParent(root.transform, false);
            gateObject.transform.position = plan.Gate.Position;
            gate = gateObject.AddComponent<ArenaGate>();
            gate.Build(plan.Gate, tank, BUILDING_LAYER);
            Structures.Add(gate.Wall);

            using (VoxelsMarker.Auto())
                VoxelStructure.BuildAll(Structures);
            Structures.Clear();

            using (PropsMarker.Auto())
            {
                foreach (FencePlan fence in plan.Fences)
                    AddFence(plan, root.transform, fence);

                AddEdgeFences(plan, root.transform);
                AddBarriers(plan, root.transform);
                if (backdrop != null)
                    AddBackdrop(plan, root.transform, backdrop);
            }

            return root;
        }

        private static void AddGround(ArenaPlan plan, Transform root)
        {
            float side = ArenaPlan.SIZE + ArenaPlan.MARGIN * 2f;
            int cells = Mathf.RoundToInt(side / ArenaPlan.VOXEL / GROUND_TILES);
            float tile = cells * ArenaPlan.VOXEL;
            // Base sits DEPTH above the bottom; hills rise up to the relief over it, plus a voxel spare.
            int height = Mathf.CeilToInt((ArenaPlan.DEPTH + plan.Terrain.Amplitude) / ArenaPlan.VOXEL) + 2;
            for (int z = 0; z < GROUND_TILES; z++)
            for (int x = 0; x < GROUND_TILES; x++)
            {
                var at = new Vector3(plan.Centre.x + (x + 0.5f - GROUND_TILES * 0.5f) * tile, plan.Centre.y - ArenaPlan.DEPTH,
                    plan.Centre.z + (z + 0.5f - GROUND_TILES * 0.5f) * tile);
                AddStructure(root, "Ground", VoxelStructure.Shape.Ground, new Vector3Int(cells, height, cells), plan.Seed, at, GROUND_LAYER);
            }
        }

        /// <summary>A structure to build with the rest (<see cref="VoxelStructure.BuildAll"/>).</summary>
        private static void AddStructure(Transform root, string name, VoxelStructure.Shape shape, Vector3Int size,
            int seed, Vector3 position, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(root, false);
            go.transform.position = position;
            var structure = go.AddComponent<VoxelStructure>();
            structure.Configure(shape, size, seed);
            Structures.Add(structure);
        }

        private static void AddFence(ArenaPlan plan, Transform root, FencePlan run)
        {
            var go = new GameObject("Fence") { layer = BUILDING_LAYER };
            go.transform.SetParent(root, false);
            go.transform.position = run.Start;
            Vector3 start = run.Start;
            bool alongZ = run.AlongZ;
            go.AddComponent<ArenaFence>().Build(run.Length, alongZ, run.Seed, BUILDING_LAYER,
                d => plan.HeightAt(start.x + (alongZ ? 0f : d), start.z + (alongZ ? d : 0f)));
        }

        /// <summary>A fence along the far edge either side of the gate: the gate reads as a way through it.</summary>
        private static void AddEdgeFences(ArenaPlan plan, Transform root)
        {
            float z = plan.Centre.z + ArenaPlan.SIZE * 0.5f;
            float half = plan.Gate.Size.x * ArenaPlan.VOXEL * 0.5f;
            float left = plan.Centre.x - ArenaPlan.SIZE * 0.5f;
            float right = plan.Centre.x + ArenaPlan.SIZE * 0.5f;
            float gateLeft = plan.Gate.Position.x - half;
            float gateRight = plan.Gate.Position.x + half;

            AddEdgeRun(plan, root, left, gateLeft, z, 11);
            AddEdgeRun(plan, root, gateRight, right, z, 13);
        }

        private static void AddEdgeRun(ArenaPlan plan, Transform root, float from, float to, float z, int seed)
        {
            // In pieces of up to 8 m: a long run is one body, and a shell into it would flood-fill all of it.
            const float piece = 8f;
            for (float x = from; x < to - 0.5f; x += piece)
            {
                float length = Mathf.Min(piece, to - x);
                AddFence(plan, root, new FencePlan
                {
                    Start = plan.OnGround(x, z),
                    Length = length,
                    AlongZ = false,
                    Seed = plan.Seed + seed + Mathf.RoundToInt(x)
                });
            }
        }

        /// <summary>
        /// Unseen walls round the playable square, open at the gate: the tank stays in, shells and
        /// debris fly through (<see cref="ArenaGate.PassThrough"/>).
        /// </summary>
        private static void AddBarriers(ArenaPlan plan, Transform root)
        {
            var go = new GameObject("Barriers") { layer = 2 };
            go.transform.SetParent(root, false);
            Vector3 c = plan.Centre;
            float half = ArenaPlan.SIZE * 0.5f;
            const float thick = 1f;
            const float tall = 8f;

            AddBox(go, new Vector3(c.x, c.y + tall * 0.5f - 2f, c.z - half - thick * 0.5f), new Vector3(ArenaPlan.SIZE + thick * 2f, tall, thick));
            AddBox(go, new Vector3(c.x - half - thick * 0.5f, c.y + tall * 0.5f - 2f, c.z), new Vector3(thick, tall, ArenaPlan.SIZE + thick * 2f));
            AddBox(go, new Vector3(c.x + half + thick * 0.5f, c.y + tall * 0.5f - 2f, c.z), new Vector3(thick, tall, ArenaPlan.SIZE + thick * 2f));

            // The far edge, in two pieces round the gate.
            float gateHalf = plan.Gate.Size.x * ArenaPlan.VOXEL * 0.5f;
            float z = c.z + half + thick * 0.5f;
            float leftEnd = plan.Gate.Position.x - gateHalf;
            float rightStart = plan.Gate.Position.x + gateHalf;
            float leftStart = c.x - half - thick;
            float rightEnd = c.x + half + thick;
            AddBox(go, new Vector3((leftStart + leftEnd) * 0.5f, c.y + tall * 0.5f - 2f, z), new Vector3(leftEnd - leftStart, tall, thick));
            AddBox(go, new Vector3((rightStart + rightEnd) * 0.5f, c.y + tall * 0.5f - 2f, z), new Vector3(rightEnd - rightStart, tall, thick));

            // Past the gate the way out is a corridor as wide as the gate, so the tank cannot go round the outside.
            float depth = ArenaPlan.MARGIN;
            float cz = c.z + half + depth * 0.5f;
            AddBox(go, new Vector3(leftEnd - thick * 0.5f, c.y + tall * 0.5f - 2f, cz), new Vector3(thick, tall, depth));
            AddBox(go, new Vector3(rightStart + thick * 0.5f, c.y + tall * 0.5f - 2f, cz), new Vector3(thick, tall, depth));
        }

        private static void AddBox(GameObject host, Vector3 worldCentre, Vector3 size)
        {
            var box = host.AddComponent<BoxCollider>();
            box.center = host.transform.InverseTransformPoint(worldCentre);
            box.size = size;
            box.excludeLayers = ArenaGate.PassThrough;
        }

        /// <summary>
        /// Flat ground to the horizon round the voxel tile, just under its lowest level: a ring of
        /// four quads, so nothing shows through a crater dug in the tile itself.
        /// </summary>
        private static void AddBackdrop(ArenaPlan plan, Transform root, Material material)
        {
            const float far = 400f;
            float inner = ArenaPlan.SIZE * 0.5f + ArenaPlan.MARGIN;
            float band = far - inner;
            Vector3 c = plan.Centre + Vector3.down * 0.05f;

            AddQuad(root, material, c + new Vector3(0f, 0f, inner + band * 0.5f), new Vector2(far * 2f, band));
            AddQuad(root, material, c - new Vector3(0f, 0f, inner + band * 0.5f), new Vector2(far * 2f, band));
            AddQuad(root, material, c + new Vector3(inner + band * 0.5f, 0f, 0f), new Vector2(band, inner * 2f));
            AddQuad(root, material, c - new Vector3(inner + band * 0.5f, 0f, 0f), new Vector2(band, inner * 2f));
        }

        private static void AddQuad(Transform root, Material material, Vector3 centre, Vector2 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Backdrop";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

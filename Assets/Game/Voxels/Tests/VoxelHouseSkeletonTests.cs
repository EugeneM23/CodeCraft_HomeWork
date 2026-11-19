using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A house's skeleton (VoxelStructure: piers, corners, ring beams, chimney stacks): a shell bites a
    /// building less than anything else, the skeleton less than the walls between it, heavy bombing
    /// leaves the skeleton standing, and a building's rubble that comes to rest goes to crumbs.
    /// </summary>
    public sealed class VoxelHouseSkeletonTests
    {
        private readonly List<GameObject> _spawned = new();

        /// <summary>Two storeys: 6.4 × 10.6 × 5.4 m. Its walls run x 3..60, z 6..50 (VoxelStructure.Victorian).</summary>
        private static readonly Vector3Int TwoStoreys = new(64, 106, 54);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (GameObject go in _spawned)
                    if (go != null)
                        Object.Destroy(go);
                foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                    Object.Destroy(body.gameObject);
                yield return null;
            }

            _spawned.Clear();
        }

        [UnityTest]
        public IEnumerator Victorian_CornersPiersBeamsAndChimneysAreHard()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(TwoStoreys);
            yield return null;

            // The front wall's outer layer is z 6; along it u = x − 3, 58 long, windows (first floor) at u 6..12,
            // 26..32, 45..51 from y 40 to 55. The plinth (y < 5) is ordinary stone: a crater under a corner takes it.
            Assert.IsTrue(Hard(house, new int3(4, 17, 6)), "a corner (u 1) bears");
            Assert.IsTrue(Hard(house, new int3(22, 17, 6)), "the pier between the first two windows (u 19) bears");
            Assert.IsTrue(Hard(house, new int3(7, 34, 6)), "the ring beam at the first floor bears");
            Assert.IsFalse(Hard(house, new int3(7, 2, 6)), "the plinth does not");
            Assert.AreNotEqual(0, house.VoxelAt(new int3(7, 45, 6)), "the wall is there");
            Assert.IsFalse(Hard(house, new int3(7, 45, 6)), "the wall between the corner and a window (u 4) does not");
            Assert.IsTrue(Hard(house, new int3(5, 66, 26)), "a chimney stack bears");
        }

        [UnityTest]
        public IEnumerator Shell_BitesABuildingLess_AndItsSkeletonLessStill()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(TwoStoreys);
            yield return null;

            // A tank shell's 1.6 m crater is about 1.15 m in a building; a bullet's hole is as it was.
            Assert.That(house.BiteRadius(1.6f), Is.InRange(1.05f, 1.25f));
            Assert.That(house.BiteRadius(0.3f), Is.EqualTo(0.3f));

            // A 0.5 m bite at the wall beside the corner (first floor, u 4): the wall 30 cm over it goes, the corner
            // 30 cm beside it stands.
            var over = new int3(7, 48, 6);
            var corner = new int3(4, 45, 6);
            Assert.AreNotEqual(0, house.VoxelAt(over), "the wall over it is there");
            Assert.IsFalse(Hard(house, over));
            Assert.IsTrue(Hard(house, corner));
            house.CarveSphere(World(house, new int3(7, 45, 6)), 0.5f, 2f, 0, true);
            yield return null;
            Assert.AreEqual(0, house.VoxelAt(over), "the wall over the bite went");
            Assert.AreNotEqual(0, house.VoxelAt(corner), "the corner beside it stands");
        }

        [UnityTest]
        public IEnumerator HeavyBombing_LeavesTheSkeleton()
        {
            SpawnFlatGround();
            VoxelBody house = SpawnHouse(TwoStoreys);
            yield return null;
            (int hard, int soft) before = Count(house);

            // Ten air bombs' bites (2 m, whole: VoxelDamage.AirBlast) from above over a grid, each where it strikes
            // what is left (the roof, then the floors), a third of a second apart.
            Bounds b = house.WorldBounds;
            Vector3 c = b.center, e = b.extents;
            for (int bomb = 0; bomb < 10; bomb++)
            {
                if (house == null)
                    break;
                var top = new Vector3(c.x + e.x * (bomb % 3 - 1) * 0.55f, b.max.y + 3f, c.z + e.z * (bomb / 3 % 3 - 1) * 0.5f);
                Vector3 at = Physics.Raycast(top, Vector3.down, out RaycastHit hit, 30f) ? hit.point : new Vector3(top.x, 0.5f, top.z);
                house.CarveSphere(at, 2f, 24f, 100, true);
                for (int i = 0; i < 20; i++)
                    yield return null;
            }

            float end = Time.time + 4f;
            while (Time.time < end)
                yield return null;

            Assert.IsNotNull(house, "something of the house stands");
            (int hard, int soft) after = Count(house);
            float hardLeft = after.hard / (float)before.hard, softLeft = after.soft / (float)before.soft;
            Debug.Log($"[Skeleton] hard {before.hard} -> {after.hard} ({hardLeft:P0}), the rest {before.soft} -> {after.soft} ({softLeft:P0})");
            Assert.That(hardLeft, Is.GreaterThan(softLeft + 0.15f), "the skeleton outlasts the walls between it");
            // The four corners stand to the first floor's beam (3.4 m up) at least.
            foreach (int3 corner in new[] { new int3(4, 34, 6), new int3(59, 34, 6), new int3(4, 34, 50), new int3(59, 34, 50) })
                Assert.AreNotEqual(0, house.VoxelAt(corner), $"the corner at {corner} stands");
        }

        [UnityTest]
        public IEnumerator RestingRubble_GoesToCrumbs([Values(480, 6000)] int voxels)
        {
            SpawnFlatGround();
            // A building's piece set down on the ground: it comes to rest and breaks up, nothing of it lies about.
            int3 size = voxels <= 480 ? new int3(10, 6, 8) : new int3(30, 10, 20);
            VoxelBody piece = SpawnRubble(new Vector3(0f, size.y * 0.05f + 0.05f, 0f), size);
            piece.Launch(Vector3.zero, Vector3.zero);

            float end = Time.time + 8f;
            while (Time.time < end && LooseRubble().Count > 0)
                yield return null;

            List<VoxelBody> left = LooseRubble();
            Debug.Log($"[Skeleton] rubble {voxels}: {left.Count} pieces left ({left.Sum(p => p.SolidCount)} voxels)");
            Assert.That(left, Is.Empty, "the rubble went to crumbs");
        }

        private static List<VoxelBody> LooseRubble() =>
            Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).Where(v => v.HasStrength && !v.IsStatic && v.SolidCount > 0).ToList();

        private static bool Hard(VoxelBody body, int3 p) => body.IsHard(body.VoxelAt(p));

        private static Vector3 World(VoxelBody body, int3 p) =>
            body.transform.TransformPoint((Vector3)(body.Origin + ((float3)p + 0.5f) * body.VoxelSize));

        private static (int hard, int soft) Count(VoxelBody body)
        {
            int hard = 0, soft = 0;
            for (int z = 0; z < body.Size.z; z++)
            for (int y = 0; y < body.Size.y; y++)
            for (int x = 0; x < body.Size.x; x++)
            {
                byte v = body.VoxelAt(new int3(x, y, z));
                if (v == 0)
                    continue;
                if (body.IsHard(v))
                    hard++;
                else
                    soft++;
            }

            return (hard, soft);
        }

        private void SpawnFlatGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            _spawned.Add(ground);
        }

        /// <summary>A block of brick with a building's strength table, as a building's piece breaking off is.</summary>
        private VoxelBody SpawnRubble(Vector3 at, int3 size)
        {
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = 1;
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(120, 60, 45, 255);

            var go = new GameObject("Rubble");
            go.transform.position = at;
            _spawned.Add(go);
            VoxelBodySettings settings = VoxelBodySettings.Default(0.1f);
            settings.Origin = -(float3)size * 0.05f;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            var strength = new NativeArray<float3>(256, Allocator.Persistent);
            strength[1] = new float3(2500f, 40f, 2f);
            body.SetStrength(strength);
            return body;
        }

        private VoxelBody SpawnHouse(Vector3Int size)
        {
            var go = new GameObject("House");
            go.SetActive(false);
            VoxelStructure structure = go.AddComponent<VoxelStructure>();
            Set(structure, "_shape", VoxelStructure.Shape.Victorian);
            Set(structure, "_size", size);
            go.SetActive(true);
            _spawned.Add(go);
            structure.Build();
            return go.GetComponentInChildren<VoxelBody>();
        }

        private static void Set<T>(Object target, string field, T value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            info.SetValue(target, value);
        }
    }
}

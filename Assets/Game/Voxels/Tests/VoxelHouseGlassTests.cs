using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A town house's windows (VoxelBody.SetGlass, ShatterGlass): listed once when built, cheaply; a blast breaks
    /// the panes round it and not the frames or the walls; panes far off stay; broken glass stays broken.
    /// </summary>
    public sealed class VoxelHouseGlassTests
    {
        private readonly List<GameObject> _spawned = new();

        // The front wall's inner layer is z 7 (VoxelStructure.Victorian); the middle first-floor window spans
        // x 29..35 (u 26..32) and y 40..54, its glazing bars at x 32 and y 47.
        private static readonly int3 Pane = new(33, 44, 7);
        private static readonly int3 Bar = new(32, 44, 7);
        private static readonly int3 Wall = new(38, 44, 7);
        private static readonly int3 BackPane = new(33, 44, 49);

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
        public IEnumerator Glass_IsListedOnce_Cheaply()
        {
            VoxelBody house = SpawnHouse();
            yield return null;

            Debug.Log($"[Glass] {house.GlassCount} panes' voxels listed in {VoxelBody.LastGlassScanMs:F2} ms");
            Assert.That(house.GlassCount, Is.GreaterThan(500), "a two-storey town house has a lot of glass");
            Assert.AreNotEqual(0, house.VoxelAt(Pane));
            Assert.That(VoxelBody.LastGlassScanMs, Is.LessThan(5f), "listed with one Burst pass");
        }

        [UnityTest]
        public IEnumerator Blast_BreaksThePanesRoundIt_NotTheFramesOrWalls()
        {
            VoxelBody house = SpawnHouse();
            yield return null;
            int glass = house.GlassCount;
            int solid = house.SolidCount;
            byte bar = house.VoxelAt(Bar), wall = house.VoxelAt(Wall);
            Assert.AreNotEqual(0, bar);
            Assert.AreNotEqual(0, wall);

            // A blast 1.5 m in front of the window: every pane within 2.8 m, fewer out to 4.5 m.
            Vector3 blast = World(house, Pane) + Vector3.back * 1.5f;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int broken = VoxelDamage.ShatterGlass(blast, 2.8f, 4.5f);
            float ms = (float)timer.Elapsed.TotalMilliseconds;
            yield return null;

            Debug.Log($"[Glass] blast broke {broken} of {glass} glass voxels in {ms:F2} ms");
            Assert.That(broken, Is.GreaterThan(100));
            Assert.AreEqual(0, house.VoxelAt(Pane), "the pane went");
            Assert.AreEqual(bar, house.VoxelAt(Bar), "its frame stayed");
            Assert.AreEqual(wall, house.VoxelAt(Wall), "the wall stayed");
            Assert.AreNotEqual(0, house.VoxelAt(BackPane), "a pane on the far side (5.7 m off) stayed");
            Assert.AreEqual(solid - broken, house.SolidCount, "only glass went");
            Assert.AreEqual(glass - broken, house.GlassCount);
        }

        [UnityTest]
        public IEnumerator BrokenGlass_StaysBroken()
        {
            VoxelBody house = SpawnHouse();
            yield return null;
            Vector3 blast = World(house, Pane) + Vector3.back * 1.5f;

            int first = VoxelDamage.ShatterGlass(blast, 3f, 3f);
            int second = VoxelDamage.ShatterGlass(blast, 3f, 3f);
            yield return null;

            Assert.That(first, Is.GreaterThan(0));
            Assert.AreEqual(0, second, "nothing more to break there");
        }

        private static Vector3 World(VoxelBody body, int3 p) =>
            body.transform.TransformPoint((Vector3)(body.Origin + ((float3)p + 0.5f) * body.VoxelSize));

        private VoxelBody SpawnHouse()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            _spawned.Add(ground);

            var go = new GameObject("House");
            go.SetActive(false);
            VoxelStructure structure = go.AddComponent<VoxelStructure>();
            Set(structure, "_shape", VoxelStructure.Shape.Victorian);
            Set(structure, "_size", new Vector3Int(64, 106, 54));
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

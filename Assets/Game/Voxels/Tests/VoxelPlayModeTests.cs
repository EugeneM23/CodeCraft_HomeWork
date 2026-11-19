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
    public sealed class VoxelPlayModeTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();

            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        [UnityTest]
        public IEnumerator Structure_IsMeshedWithCollidersAfterOneFrame()
        {
            VoxelBody body = SpawnStructure(VoxelStructure.Shape.Wall, new Vector3Int(40, 20, 3), Vector3.zero);
            yield return null;

            Assert.AreEqual(40 * 20 * 3, body.SolidCount);
            MeshFilter[] filters = body.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Length, Is.GreaterThan(0));
            Assert.That(filters.Sum(f => f.sharedMesh.GetIndexCount(0)), Is.GreaterThan(0));

            // A solid wall of one chunk row is covered by a handful of boxes, not one per voxel.
            BoxCollider[] boxes = body.GetComponentsInChildren<BoxCollider>().Where(b => b.enabled).ToArray();
            Assert.That(boxes.Length, Is.InRange(1, 40));
        }

        [UnityTest]
        public IEnumerator Blood_SoaksIn_NoPrintCoversIt_AndChurningLeavesItRedBrown()
        {
            VoxelBody body = SpawnStructure(VoxelStructure.Shape.Wall, new Vector3Int(40, 20, 3), Vector3.zero);
            yield return null;
            Assert.IsTrue(body.IsStatic);

            Bounds bounds = body.WorldBounds;
            Vector3 top = new Vector3(bounds.center.x, bounds.max.y - body.VoxelSize * 0.5f, bounds.center.z);
            Assert.That(body.PaintBlood(top, 0.15f, 0, 0, 1f), Is.GreaterThan(0), "the blood soaks into the voxels");
            Assert.IsTrue(body.TryGetBlood(top, out int shade, out int level));
            Assert.AreEqual(0, shade);
            Assert.AreEqual(0, level, "fresh");
            Assert.IsTrue(body.TryGetColor(top, out Color32 colour) && VoxelBlood.IsBlood(colour));

            Assert.AreEqual(0, body.Paint(top, 0.15f, new Color(0.5f, 0.45f, 0.4f), 1f, true), "a dusty print does not cover the blood");
            Assert.IsTrue(body.TryGetBlood(top, out _, out _));

            Assert.That(body.Churn(top, 0.15f, 1f, out int fresh), Is.GreaterThan(0));
            Assert.That(fresh, Is.GreaterThan(0), "fresh blood for the track to pick up");
            for (int i = 0; i < 10; i++)
                body.Churn(top, 0.15f, 1f, out fresh);
            Assert.AreEqual(0, fresh, "churned, it is fresh no more");
            Assert.IsTrue(body.TryGetBlood(top, out _, out level));
            Assert.AreEqual(VoxelBlood.LEVELS - 1, level, "churned as far as it goes, and there it stays");
            Assert.IsTrue(body.TryGetColor(top, out colour) && VoxelBlood.IsBlood(colour), "still red-brown");
        }

        [UnityTest]
        public IEnumerator Blast_TakesVoxelsOutAndThrowsDebris()
        {
            VoxelBody body = SpawnStructure(VoxelStructure.Shape.Wall, new Vector3Int(40, 20, 3), Vector3.zero);
            yield return null;

            int before = body.SolidCount;
            int taken = VoxelDamage.Blast(new Vector3(0f, 1f, 0f), 0.5f, 6f);
            yield return null;

            Assert.That(taken, Is.GreaterThan(20));
            // Loose bits the blast cut off leave the wall too (as debris or pieces), never more voxels.
            Assert.That(body.SolidCount, Is.LessThanOrEqualTo(before - taken));
            Assert.That(VoxelWorld.Instance.Debris.Count, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Pillar_CutThrough_TopBreaksOffAndFalls()
        {
            VoxelBody pillar = SpawnPillar(new int3(4, 30, 4), 0.1f);
            yield return null;

            // A blast wider than the pillar at 2 m up cuts it in two.
            VoxelDamage.Blast(new Vector3(0.2f, 2f, 0.2f), 0.45f, 0f);
            yield return null;

            VoxelBody top = Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).FirstOrDefault(b => b != pillar);
            Assert.IsNotNull(top, "the part above the cut became a body of its own");
            Rigidbody rigidbody = top.GetComponent<Rigidbody>();
            Assert.IsNotNull(rigidbody);
            Assert.IsFalse(rigidbody.isKinematic, "it is let go once its colliders exist");
            Assert.That(top.GetComponentsInChildren<BoxCollider>().Any(b => b.enabled));

            float y = rigidbody.position.y;
            for (int i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(rigidbody.position.y, Is.LessThan(y - 0.05f), "and it falls");

            Assert.That(pillar.SolidCount, Is.GreaterThan(0), "the grounded stump stays");
            Assert.IsNull(pillar.GetComponent<Rigidbody>());
        }

        [UnityTest]
        public IEnumerator VoxelizedModel_RebuildsPartsAsVoxelsAndHidesTheMeshes()
        {
            var root = new GameObject("Model");
            _spawned.Add(root);
            var hull = new GameObject("HullVisual").transform;
            hull.SetParent(root.transform, false);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(hull, false);

            VoxelizedModel model = root.AddComponent<VoxelizedModel>();
            yield return null;

            Assert.AreEqual(1, model.Bodies.Count);
            VoxelBody body = model.Bodies[0];
            Assert.IsFalse(cube.GetComponent<MeshRenderer>().enabled);
            // A 1 m cube in 5 cm voxels: 20³.
            Assert.That(body.SolidCount, Is.InRange(7600, 8400));
            Assert.That(body.GetComponentsInChildren<BoxCollider>().Length, Is.Zero, "a skin has no colliders of its own");
        }

        [UnityTest]
        public IEnumerator SkinCarve_HitsBodiesWithoutColliders()
        {
            var root = new GameObject("Model");
            _spawned.Add(root);
            var hull = new GameObject("HullVisual").transform;
            hull.SetParent(root.transform, false);
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(hull, false);

            VoxelizedModel model = root.AddComponent<VoxelizedModel>();
            yield return null;

            int before = model.Bodies[0].SolidCount;
            VoxelDamage.Blast(new Vector3(0.5f, 0.5f, 0.5f), 0.6f, 4f);
            yield return null;

            Assert.That(model.Bodies[0].SolidCount, Is.LessThan(before));
        }

        private VoxelBody SpawnStructure(VoxelStructure.Shape shape, Vector3Int size, Vector3 position)
        {
            var go = new GameObject("Structure");
            go.SetActive(false);
            go.transform.position = position;
            VoxelStructure structure = go.AddComponent<VoxelStructure>();
            Set(structure, "_shape", shape);
            Set(structure, "_size", size);
            go.SetActive(true);
            _spawned.Add(go);
            structure.Build();
            return go.GetComponentInChildren<VoxelBody>();
        }

        /// <summary>A solid column on the ground, its min corner at the origin.</summary>
        private VoxelBody SpawnPillar(int3 size, float voxelSize)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            _spawned.Add(ground);

            var go = new GameObject("Pillar");
            _spawned.Add(go);

            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = 1;
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(200, 120, 90, 255);

            VoxelBodySettings settings = VoxelBodySettings.Default(voxelSize);
            settings.Anchor = VoxelAnchor.Ground;

            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }

        private static void Set<T>(Object target, string field, T value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            info.SetValue(target, value);
        }
    }
}

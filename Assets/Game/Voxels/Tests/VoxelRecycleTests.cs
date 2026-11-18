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
    /// <summary>A body put away (VoxelBody.Recycle) and used again keeps its chunks and arrays.</summary>
    public sealed class VoxelRecycleTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            VoxelBody.PieceFactory = null;
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        [UnityTest]
        public IEnumerator Recycle_ThenInit_TakesTheSameChunksAndArrays()
        {
            var size = new int3(32, 16, 16);
            VoxelBody body = Spawn("Body", size, VoxelAnchor.Largest, _ => true);
            yield return null;

            Transform[] chunks = body.GetComponentsInChildren<Transform>(true).Where(t => t != body.transform).ToArray();
            Assert.That(chunks.Length, Is.GreaterThan(0));
            int boxesBefore = body.GetComponentsInChildren<BoxCollider>(true).Length;

            body.Recycle();
            Assert.IsTrue(body.IsRecycled);
            Assert.IsFalse(VoxelWorld.Bodies.Contains(body), "out of the world");
            Assert.That(chunks.All(t => !t.gameObject.activeSelf), "chunks switched off, kept");

            NativeArray<byte> voxels = body.TakeVoxels(size.x * size.y * size.z);
            NativeArray<Color32> palette = body.TakePalette(256);
            // Half the block this time.
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y / 2; y++)
            for (int x = 0; x < size.x; x++)
                voxels[x + size.x * (y + size.y * z)] = 1;
            palette[1] = new Color32(90, 90, 90, 255);
            body.Init(size, voxels, palette, VoxelBodySettings.Default(0.1f));
            yield return null;

            Assert.IsFalse(body.IsRecycled);
            Assert.AreEqual(size.x * size.y / 2 * size.z, body.SolidCount);
            Transform[] after = body.GetComponentsInChildren<Transform>(true).Where(t => t != body.transform).ToArray();
            CollectionAssert.AreEquivalent(chunks, after, "the same chunk objects, no new ones");
            Assert.That(after.Any(t => t.gameObject.activeSelf), "the chunks with voxels are on again");
            Assert.That(body.GetComponentsInChildren<BoxCollider>(true).Length, Is.LessThanOrEqualTo(boxesBefore),
                "no new box colliders: the old ones are reused");
            Assert.That(body.GetComponentsInChildren<BoxCollider>().Any(b => b.enabled));
        }

        [UnityTest]
        public IEnumerator Detach_TakesItsPieceFromThePieceFactory()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            _spawned.Add(ground);

            // A pooled body, used and put away.
            VoxelBody pooled = Spawn("Pooled", new int3(4, 4, 4), VoxelAnchor.Largest, _ => true);
            yield return null;
            pooled.Recycle();
            pooled.gameObject.SetActive(false);
            int asked = 0;
            VoxelBody.PieceFactory = () =>
            {
                asked++;
                return asked == 1 ? pooled : null;
            };

            VoxelBody pillar = Spawn("Pillar", new int3(4, 30, 4), VoxelAnchor.Ground, _ => true);
            yield return null;
            VoxelDamage.Blast(new Vector3(0.2f, 2f, 0.2f), 0.45f, 0f);
            yield return null;

            Assert.That(asked, Is.GreaterThan(0), "Detach asked the factory");
            Assert.IsTrue(pooled.gameObject.activeSelf);
            Assert.IsFalse(pooled.IsRecycled);
            Assert.That(pooled.SolidCount, Is.GreaterThan(100), "the pooled body is the top of the pillar now");
            Assert.IsNotNull(pooled.GetComponent<Rigidbody>());
        }

        private VoxelBody Spawn(string name, int3 size, VoxelAnchor anchor, System.Func<int3, bool> solid)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                if (solid(new int3(x, y, z)))
                    voxels[x + size.x * (y + size.y * z)] = 1;
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(200, 120, 90, 255);
            VoxelBodySettings settings = VoxelBodySettings.Default(0.1f);
            settings.Anchor = anchor;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }
    }
}

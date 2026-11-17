using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Mud and blood stuck on a moving body (Deposit, Paint) come off again with Wash and
    /// ShakeOff, and take nothing of the body with them: shaken clean, it is voxel for voxel what
    /// it was — a thin gun as much as a thick hull.
    /// </summary>
    public sealed class VoxelBodyShakeTests
    {
        private static readonly FieldInfo VoxelsField =
            typeof(VoxelBody).GetField("_voxels", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Color32 Mud = new(70, 50, 30, 90);
        private static readonly Color32 Blood = new(160, 6, 4, 25);

        [TearDown]
        public void TearDown()
        {
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        /// <summary>A box of plate, one empty voxel of room all round (as MeshVoxelizer leaves), on a part like a tank's.</summary>
        private static VoxelBody Plate(string name, int3 inner, Vector3 position)
        {
            int3 size = inner + 2;
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int z = 1; z <= inner.z; z++)
            for (int y = 1; y <= inner.y; y++)
            for (int x = 1; x <= inner.x; x++)
                voxels[x + size.x * (y + size.y * z)] = (byte)(1 + (x + y + z) % 2);

            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(150, 140, 100, 255);
            palette[2] = new Color32(120, 110, 80, 255);

            VoxelBodySettings settings = VoxelBodySettings.Default(0.05f);
            settings.Anchor = VoxelAnchor.Largest;
            settings.Colliders = false;

            var go = new GameObject(name);
            go.transform.position = position;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }

        private static byte[] Voxels(VoxelBody body) => ((NativeArray<byte>)VoxelsField.GetValue(body)).ToArray();

        /// <summary>Splashes of mud and blood all over the body, as driving and crushing lay them.</summary>
        private static void Dirty(VoxelBody body)
        {
            for (int i = 0; i < 60; i++)
            {
                Assert.IsTrue(body.TryGetSurfacePoint(1f, out Vector3 at));
                Color32 colour = i % 2 == 0 ? Mud : Blood;
                body.Paint(at, 0.12f, colour, 0.6f);
                body.Deposit(at, 0.1f, colour, 0.55f);
                if (i % 3 == 0)
                    body.Paint(at, 0.08f, i % 2 == 0 ? Blood : Mud, 0.5f);
            }
        }

        [UnityTest]
        public IEnumerator ShakenThreeTimes_AThirdAHalfAndTheRest_TheBodyIsAsItWas()
        {
            VoxelBody hull = Plate("Hull", new int3(24, 12, 36), Vector3.zero);
            VoxelBody gun = Plate("Gun", new int3(2, 2, 30), new Vector3(0f, 1f, 1f));
            yield return null;

            var clean = new Dictionary<VoxelBody, byte[]>();
            var solid = new Dictionary<VoxelBody, int>();
            foreach (VoxelBody body in new[] { hull, gun })
            {
                clean[body] = Voxels(body);
                solid[body] = body.SolidCount;
                Dirty(body);
                Assert.That(Voxels(body), Is.Not.EqualTo(clean[body]), $"{body.name} took mud and blood");
            }

            Vector3 centre = new(0f, 0.5f, 0.5f);
            float[] shares = { 1f / 3f, 0.5f, 1f };
            foreach (float share in shares)
            foreach (VoxelBody body in new[] { hull, gun })
            {
                body.Wash(share, centre, 4f, 50, 2f);
                body.ShakeOff(share, centre, 4f, 50, 2f);
            }

            foreach (VoxelBody body in new[] { hull, gun })
            {
                Assert.AreEqual(solid[body], body.SolidCount, $"{body.name}: nothing added is left, nothing of it went");
                Assert.That(Voxels(body), Is.EqualTo(clean[body]), $"{body.name}: every voxel its own colour again");
            }
        }

        [UnityTest]
        public IEnumerator ShakenOnce_AThird_SomeMudIsLeft()
        {
            VoxelBody hull = Plate("Hull", new int3(24, 12, 36), Vector3.zero);
            yield return null;

            byte[] clean = Voxels(hull);
            Dirty(hull);
            int dirty = Differences(Voxels(hull), clean);

            hull.Wash(1f / 3f, Vector3.zero, 4f, 50, 2f);
            hull.ShakeOff(1f / 3f, Vector3.zero, 4f, 50, 2f);

            int left = Differences(Voxels(hull), clean);
            Assert.That(left, Is.GreaterThan(dirty * 0.4f).And.LessThan(dirty * 0.9f), "about two thirds of the dirt stays");
        }

        private static int Differences(byte[] a, byte[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    n++;
            return n;
        }
    }
}

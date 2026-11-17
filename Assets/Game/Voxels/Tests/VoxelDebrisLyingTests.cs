using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>The lying store: every cluster that comes to rest lies for good, ticked by hand over a plane.</summary>
    public sealed class VoxelDebrisLyingTests
    {
        private const float STEP = 0.02f;

        private VoxelDebris _debris;
        private GameObject _floor;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floor.transform.localScale = Vector3.one * 50f;
            Physics.SyncTransforms();
            _material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            _debris = new VoxelDebris(_material, 4096, 2048);
        }

        [TearDown]
        public void TearDown()
        {
            _debris.Dispose();
            Object.Destroy(_floor);
            Object.Destroy(_material);
        }

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds; t += STEP)
                _debris.Tick(STEP);
            _debris.Complete();
        }

        [Test]
        public void Clump_ThatCameToRest_LiesForGood()
        {
            int clump = _debris.BeginCluster(new Vector3(0f, 0.3f, 0f), Vector3.zero, 0f);
            for (int i = 0; i < 8; i++)
                _debris.AddCube(clump, new Vector3(i * 0.2f, 0.3f, 0f), 0.2f, Color.gray);
            _debris.SetLife(clump, 5f);

            Run(3f);
            Assert.AreEqual(8, _debris.LyingCount, "came to rest and lies");
            Assert.AreEqual(0, _debris.Count, "no longer simulated");

            Run(30f);
            Assert.AreEqual(8, _debris.LyingCount, "its life is over, but lying cubes do not age");
        }

        [Test]
        public void Dust_NeverLies()
        {
            // A hard hit makes dust; after 3 s only crumbs and pieces lie, no dust-sized cubes.
            Clump(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 5, Color.gray);
            Run(4f);
            Assert.That(_debris.LyingCount, Is.GreaterThan(0), "the pieces and crumbs lie");
            for (int i = 0; i < _debris.LyingCount; i++)
            {
                Assert.IsTrue(_debris.TryGetLying(i, out Matrix4x4 m, out _));
                Assert.That(m.lossyScale.x, Is.GreaterThan(0.1f), "no dust cube lies");
            }
        }

        [Test]
        public void FullStore_OverwritesInsteadOfGrowing()
        {
            var small = new VoxelDebris(_material, 4096, 16);
            try
            {
                for (int c = 0; c < 4; c++)
                {
                    int clump = small.BeginCluster(new Vector3(c, 0.3f, 0f), Vector3.zero, 0f);
                    for (int i = 0; i < 8; i++)
                        small.AddCube(clump, new Vector3(c + i * 0.1f, 0.3f, 0f), 0.1f, Color.gray);
                }
                for (float t = 0f; t < 1.6f; t += 0.02f) small.Tick(0.02f); // the full store starts to evict, but nothing has sunk away yet
                small.Complete();
                Assert.AreEqual(16, small.LyingCount);
            }
            finally { small.Dispose(); }
        }

        [Test]
        public void NearlyFullStore_SinksTheOldAndFarFirst_Smoothly()
        {
            var store = new VoxelDebris(_material, 8192, 1000);
            try
            {
                store.ViewPoint = Vector3.zero;
                // 980 cubes: 490 near the viewer, 490 far away. All the same age.
                LayRow(store, new Vector3(0f, 0.2f, 0f), 490);
                LayRow(store, new Vector3(0f, 0.2f, 200f), 490);
                Settle(store, 2f);
                Assert.AreEqual(980, store.LyingCount);

                int before = store.LyingCount;
                int lowest = before;
                for (float t = 0f; t < 4f; t += 0.02f)
                {
                    store.Tick(0.02f);
                    store.Complete();
                    Assert.That(before - store.LyingCount, Is.LessThanOrEqualTo(30), "no cliff: a few at a time");
                    before = store.LyingCount;
                    lowest = Mathf.Min(lowest, before);
                }
                Assert.That(lowest, Is.LessThan(980), "something went");
                Assert.AreEqual(490, store.CountLyingNear(Vector3.zero, 50f), "the near ones all stayed");
            }
            finally { store.Dispose(); }
        }

        [Test]
        public void SinkingCube_IsRemovedAfterSinkTime_AndTheRestKeepTheirShapeAndColour()
        {
            var store = new VoxelDebris(_material, 8192, 1000);
            try
            {
                store.ViewPoint = Vector3.zero;
                // 960 cubes (past 95 %), two colours; the second row lies far away, so it is the one that sinks.
                LayRow(store, new Vector3(0f, 0.2f, 0f), 480, Color.red);
                LayRow(store, new Vector3(0f, 0.2f, 200f), 480, Color.blue);
                Settle(store, 12f);

                // One round sinks EVICT_SHARE of the capacity (20), which brings the store under 95 %: it stops there.
                int count = store.LyingCount;
                Assert.That(count, Is.InRange(938, 942), "about EVICT_SHARE * capacity went");
                for (int i = 0; i < count; i++)
                {
                    Assert.IsTrue(store.TryGetLying(i, out Matrix4x4 m, out Color c), "every slot below LyingCount is a cube");
                    Assert.AreEqual(0.1f, m.lossyScale.x, 0.002f, "none left half sunk");
                    Assert.IsTrue(c == Color.red || c == Color.blue, "colours moved with their cubes");
                }
                Assert.AreEqual(480, store.CountLyingNear(Vector3.zero, 50f), "the near row is whole");
            }
            finally { store.Dispose(); }
        }

        [Test]
        public void Crush_BreaksWhatLiesUnderTheWheel()
        {
            LayPiece(_debris, new Vector3(0f, 0.2f, 0f), 12, Color.gray);
            LayPiece(_debris, new Vector3(5f, 0.2f, 0f), 12, Color.gray);
            Run(2f);
            Assert.AreEqual(24, _debris.LyingCount);

            _debris.Crush(new Vector3(0f, 0.2f, 0f), 0.6f, new Vector3(0f, 0f, 3f));
            Run(1f);
            Assert.AreEqual(12, _debris.CountLyingNear(new Vector3(5f, 0.2f, 0f), 1f), "the far piece is untouched");
            Assert.That(_debris.CountLyingNear(Vector3.zero, 0.6f), Is.LessThanOrEqualTo(4 + 0), "crushed: crumbs flew, the rest sank");
            Assert.That(_debris.LyingCount + _debris.Count, Is.LessThanOrEqualTo(12 + 6), "no cube doubled: the far piece and at most 6 crumbs");
        }

        [Test]
        public void Crush_LeavesBlood()
        {
            _debris.Drop(new Vector3(0f, 0.5f, 0f), Vector3.zero, Color.red, 0.05f);
            Run(1f);
            _debris.Crush(Vector3.zero, 0.6f, Vector3.forward);
            Run(1f);
            Assert.AreEqual(1, _debris.LyingCount);
        }

        [Test]
        public void Blow_ThrowsAPieceUpAsOneClump()
        {
            LayPiece(_debris, new Vector3(1f, 0.2f, 0f), 12, Color.gray);
            Run(2f);
            _debris.Blow(Vector3.zero, 4f, 10f);
            _debris.Tick(0.02f); _debris.Tick(0.02f); _debris.Complete();
            var sizes = new List<int>();
            _debris.GetClusterSizes(sizes);
            CollectionAssert.Contains(sizes, 12, "the piece flies on whole");
            Assert.AreEqual(0, _debris.LyingCount);
        }

        [Test]
        public void Loosen_DropsWithoutAPush()
        {
            LayPiece(_debris, new Vector3(0f, 0.2f, 0f), 12, Color.gray);
            Run(2f);
            _debris.Loosen(Vector3.zero, 1f);
            _debris.Tick(0.02f); _debris.Tick(0.02f); _debris.Complete();
            Assert.AreEqual(1, _debris.ClusterCount);
            Run(2f);
            Assert.AreEqual(12, _debris.CountLyingNear(Vector3.zero, 1.5f), "fell back where it was");
        }

        /// <summary>A piece of <paramref name="cubes"/> cubes of 0.1 m in a row along X, centred at <paramref name="at"/>, at rest.</summary>
        private static void LayPiece(VoxelDebris store, Vector3 at, int cubes, Color color)
        {
            int cluster = store.BeginCluster(at, Vector3.zero, 0f);
            for (int i = 0; i < cubes; i++)
                store.AddCube(cluster, at + new Vector3((i - (cubes - 1) * 0.5f) * 0.1f, 0f, 0f), 0.1f, color);
        }

        /// <summary>Clusters of 10 cubes of 0.1 m in a row along X, side by side, <paramref name="cubes"/> in all.</summary>
        private static void LayRow(VoxelDebris store, Vector3 start, int cubes, Color? color = null)
        {
            for (int c = 0; c < cubes / 10; c++)
            {
                int cluster = store.BeginCluster(start + new Vector3(c * 1f + 0.45f, 0f, 0f), Vector3.zero, 0f);
                for (int i = 0; i < 10; i++)
                    store.AddCube(cluster, start + new Vector3(c * 1f + i * 0.1f + 0.05f, 0f, 0f), 0.1f, color ?? Color.gray);
            }
        }

        private static void Settle(VoxelDebris store, float seconds)
        {
            for (float t = 0f; t < seconds; t += STEP)
                store.Tick(STEP);
            store.Complete();
        }

        /// <summary>A side^3 block of 0.2 m cubes centred at <paramref name="center"/>.</summary>
        private int Clump(Vector3 center, Vector3 velocity, int side, Color color)
        {
            int clump = _debris.BeginCluster(center, velocity, 0f);
            const float cube = 0.2f;
            float half = (side - 1) * cube * 0.5f;
            for (int z = 0; z < side; z++)
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
                _debris.AddCube(clump, center + new Vector3(x * cube - half, y * cube - half, z * cube - half), cube, color);
            return clump;
        }
    }
}

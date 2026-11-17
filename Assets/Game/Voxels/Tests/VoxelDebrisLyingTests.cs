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
            _floor.transform.localScale = Vector3.one * 5f;
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
                for (float t = 0f; t < 3f; t += 0.02f) small.Tick(0.02f);
                small.Complete();
                Assert.AreEqual(16, small.LyingCount);
            }
            finally { small.Dispose(); }
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

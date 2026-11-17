using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>Debris clumps over a plane, ticked by hand: breaking and counting.</summary>
    public sealed class VoxelDebrisCrumbleTests
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
            _debris = new VoxelDebris(_material, 4096, 4096);
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
        public void Clump_ThrownStraightDown_Breaks()
        {
            Clump(new Vector3(0f, 0.6f, 0f), new Vector3(0f, -12f, 0f), 5, Color.gray);
            Run(0.2f);
            var sizes = new List<int>();
            _debris.GetClusterSizes(sizes);
            Assert.That(sizes.Count, Is.GreaterThan(1), "it hit at 12 m/s and broke");
            // A hit harder than DUST_SPEED adds one puff of 6-12 dust cubes; none of the clump's own is lost.
            Assert.That(Sum(sizes) + _debris.LyingCount, Is.InRange(125 + 6, 125 + 12), "no cube lost, only a puff of dust added");
        }

        [TestCase(300, 15f, 8)]
        [TestCase(300, 8f, 4)]
        [TestCase(60, 5f, 2)]
        [TestCase(40, 3f, 2)]
        public void PartsFor_FollowsTheTable(int cubes, float speed, int parts) =>
            Assert.AreEqual(parts, VoxelDebris.PartsFor(cubes, speed));

        [Test]
        public void BigClump_HardHit_BreaksIntoManyPartsAndCrumbs()
        {
            Clump(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 7, Color.gray); // 343 cubes
            Run(0.15f);
            var sizes = new List<int>();
            _debris.GetClusterSizes(sizes);
            int crumbs = 0, pieces = 0, cubes = 0;
            foreach (int n in sizes)
            {
                if (n == 1) crumbs++;
                else pieces++;
                cubes += n;
            }
            int dust = cubes + _debris.LyingCount - 343;
            Assert.That(pieces, Is.InRange(6, 8), "pieces");
            Assert.That(crumbs - dust, Is.InRange(343 * 10 / 100, 343 * 30 / 100), "crumbs: 15-20% of the clump, give or take");
            Assert.That(dust, Is.InRange(6, 12), "one puff of dust (dust cubes are extra, not taken from the clump)");
        }

        [Test]
        public void Pieces_HopAndBreakAgainOnTheSecondLanding()
        {
            Clump(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 7, Color.gray);
            Run(0.15f);
            var first = new List<int>();
            _debris.GetClusterSizes(first);
            int biggest = 0;
            foreach (int n in first) biggest = Mathf.Max(biggest, n);

            Run(3f);
            var later = new List<int>();
            _debris.GetClusterSizes(later);
            int biggestLater = 0;
            foreach (int n in later) biggestLater = Mathf.Max(biggestLater, n);
            // Pieces that have lain still have moved to the lying store by now (task 3); until then they stay here.
            Assert.That(biggestLater, Is.LessThan(biggest), "the second landing broke the biggest piece again");
        }

        [Test]
        public void BigClump_AfterSettling_LeavesPiecesAndAQuarterCrumbs()
        {
            Clump(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 7, Color.gray);
            Run(4f);
            // Nothing lies yet (task 3): every cube is still in a flying cluster.
            var sizes = new List<int>();
            _debris.GetClusterSizes(sizes);
            int crumbs = 0, pieces = 0, biggest = 0, cubes = 0;
            foreach (int n in sizes)
            {
                if (n == 1) crumbs++;
                else pieces++;
                biggest = Mathf.Max(biggest, n);
                cubes += n;
            }
            int dust = cubes + _debris.LyingCount - 343;
            Assert.That(pieces, Is.InRange(8, 24), "pieces stay pieces: the cascade stops");
            Assert.That(biggest, Is.LessThanOrEqualTo(60), "the biggest piece");
            Assert.That(crumbs - dust, Is.InRange(343 * 15 / 100, 343 * 40 / 100), "crumbs: about a quarter of the clump");
        }

        [Test]
        public void SmallClump_FallsIntoCrumbs()
        {
            int clump = _debris.BeginCluster(new Vector3(0f, 0.5f, 0f), new Vector3(0f, -8f, 0f), 0f);
            for (int i = 0; i < 5; i++)
                _debris.AddCube(clump, new Vector3(i * 0.2f, 0.5f, 0f), 0.2f, Color.gray);
            Run(0.15f);
            var sizes = new List<int>();
            _debris.GetClusterSizes(sizes);
            foreach (int n in sizes)
                Assert.AreEqual(1, n, "under MIN_PIECE cubes there are no pieces, only crumbs");
        }

        [Test]
        public void Crumbs_KeepTheirColours()
        {
            // Top layer green (turf), the rest brown: crumbs come from the bottom (the side that hit).
            int clump = _debris.BeginCluster(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 0f);
            for (int z = 0; z < 6; z++)
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
                _debris.AddCube(clump, new Vector3(x * 0.2f, 1.2f + y * 0.2f, z * 0.2f), 0.2f, y == 5 ? Color.green : new Color(0.4f, 0.25f, 0.1f));
            Run(0.15f);
            Assert.That(_debris.CountCrumbsOfColour(Color.green), Is.LessThan(_debris.CountCrumbsOfColour(new Color(0.4f, 0.25f, 0.1f))),
                "crumbs come off the side that hit the ground");
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

        private static int Sum(List<int> sizes) { int s = 0; foreach (int n in sizes) s += n; return s; }
    }
}

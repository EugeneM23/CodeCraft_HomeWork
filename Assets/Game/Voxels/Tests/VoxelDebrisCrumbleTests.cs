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
            Assert.AreEqual(125, Sum(sizes) + _debris.LyingCount, "no cube lost or added");
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

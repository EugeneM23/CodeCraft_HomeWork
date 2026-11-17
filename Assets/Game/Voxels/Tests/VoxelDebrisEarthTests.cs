using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>VoxelDebris.ThrowEarth: earth thrown as a few clumps, crumbs and a puff of dust.</summary>
    public sealed class VoxelDebrisEarthTests
    {
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

        [Test]
        public void ThrowEarth_SplitsTheVolumeIntoClumpsAndCrumbs()
        {
            int cubes = _debris.ThrowEarth(new EarthThrow
            {
                Point = new Vector3(0f, 0.5f, 0f), Velocity = new Vector3(0f, 6f, 4f),
                Volume = 0.8f, Spread = 1f, Top = Color.green, Body = new Color(0.4f, 0.25f, 0.1f)
            });
            Assert.That(cubes, Is.InRange(95, 125), "0.8 m³ / 0.008 = 100 cubes, plus a little dust");
            var sizes = new List<int>();
            _debris.GetClusterSizes(sizes);
            int clumps = 0, singles = 0;
            foreach (int n in sizes) { if (n > 1) clumps++; else singles++; }
            Assert.AreEqual(4, clumps, "Spread 1 → 4 clumps");
            Assert.That(singles, Is.GreaterThanOrEqualTo(15), "18 % crumbs (+ dust)");
        }

        [Test]
        public void ThrowEarth_TightThrowIsOneClump_NoDustBelowItsVolume()
        {
            int cubes = _debris.ThrowEarth(new EarthThrow
            {
                Point = new Vector3(0f, 0.5f, 0f), Velocity = Vector3.up * 3f, Volume = 0.04f, Spread = 0f,
                Top = Color.green, Body = Color.gray
            });
            Assert.AreEqual(5, cubes, "0.04 / 0.008: no dust under 0.05 m³");
        }

        [Test]
        public void ThrowEarth_ReturnsZeroWhenFull()
        {
            var full = new VoxelDebris(_material, 8, 8);
            try
            {
                Assert.AreEqual(0, full.ThrowEarth(new EarthThrow { Point = Vector3.up, Volume = 1f, Top = Color.green, Body = Color.gray }));
            }
            finally { full.Dispose(); }
        }
    }
}

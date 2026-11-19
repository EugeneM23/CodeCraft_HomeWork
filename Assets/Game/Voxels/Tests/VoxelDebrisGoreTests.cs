using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Gore's share of the debris store (VoxelDebris.DirtShare) and the stain box that catches goo: a store of its own,
    /// ticked by hand, no frames needed.
    /// </summary>
    public sealed class VoxelDebrisGoreTests
    {
        private const float STEP = 0.02f;

        private VoxelDebris _debris;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            _debris = new VoxelDebris(_material, 100, 16) { DirtShare = 0.6f };
        }

        [TearDown]
        public void TearDown()
        {
            _debris.Dispose();
            Object.Destroy(_material);
        }

        [Test]
        public void Dirt_FillsItsShareOnly_AndGoreTakesTheRest()
        {
            int dirt = 0;
            for (int i = 0; i < 200; i++)
                if (_debris.Spawn(new Vector3(0f, 50f, 0f), Vector3.zero, Color.gray, 0.05f) >= 0)
                    dirt++;
            Assert.AreEqual(60, dirt, "crumbs stop at 60 % of the store");
            Assert.IsFalse(_debris.HasRoom(1));
            Assert.IsTrue(_debris.HasGoreRoom(40));

            int gore = 0;
            for (int i = 0; i < 200; i++)
                if (_debris.GooDrop(new Vector3(0f, 50f, 0f), Vector3.zero, Color.red, 0.05f) >= 0)
                    gore++;
            Assert.AreEqual(40, gore, "blood still finds the 40 % kept for it");
        }

        [Test]
        public void GooFlyingIntoTheStainBox_IsCaught_AndGone()
        {
            _debris.SetStainBox(new Vector3(0f, 10f, 0f), Quaternion.identity, new Vector3(1f, 1f, 1f));
            // Thrown up into the box from below; caught after its first 0.1 s of flight.
            _debris.GooDrop(new Vector3(0f, 8.5f, 0f), Vector3.up * 6f, Color.red, 0.05f);
            _debris.GooDrop(new Vector3(30f, 8.5f, 0f), Vector3.up * 6f, Color.red, 0.05f);

            var hits = new List<Vector3>();
            for (float t = 0f; t < 0.6f; t += STEP)
            {
                _debris.Tick(STEP);
                _debris.Complete();
                _debris.TakeStainHits(hits);
            }

            Assert.AreEqual(1, hits.Count, "the drop that flew into the box, not the one 30 m off");
            Assert.That(Vector3.Distance(hits[0], new Vector3(0f, 10f, 0f)), Is.LessThan(1.8f));
        }
    }
}

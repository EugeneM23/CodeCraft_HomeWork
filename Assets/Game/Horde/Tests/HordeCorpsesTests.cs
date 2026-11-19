using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Horde.Tests
{
    /// <summary>Corpses the tank runs over (<see cref="HordeCorpses.Squash"/>): the ones in the box go, the rest lie on.</summary>
    public class HordeCorpsesTests
    {
        private Mesh _mesh;
        private Material _material;
        private HordeCorpses _corpses;

        [SetUp]
        public void SetUp()
        {
            _mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
            _material = new Material(Shader.Find("Game/HordeVAT"));
            _corpses = new HordeCorpses(_mesh, _material, 8, 6f, 3f, 1.2f);
        }

        [TearDown]
        public void TearDown()
        {
            _corpses.Dispose();
            Object.DestroyImmediate(_mesh);
            Object.DestroyImmediate(_material);
        }

        [Test]
        public void Squash_TakesTheCorpsesInTheBox_AndLeavesTheRest()
        {
            _corpses.Add(new float3(0f, 0f, 0f), 0f, 1f);
            _corpses.Add(new float3(5f, 0f, 0f), 0f, 1f);
            _corpses.Add(new float3(0f, 0f, 1.2f), 0f, 1f);
            _corpses.Update(1f);
            var squashed = new List<Vector3>();

            int count = _corpses.Squash(float3.zero, quaternion.identity, new float3(1.5f, 1f, 1f), 0.4f, 2f, squashed);

            Assert.AreEqual(2, count, "the one at the centre and the one 1.2 m ahead (inside the 1 m box with its 0.4 m margin)");
            Assert.AreEqual(2, squashed.Count);
            Assert.AreEqual(2, _corpses.SquashedTotal);
            Assert.AreEqual(0, _corpses.Squash(float3.zero, quaternion.identity, new float3(1.5f, 1f, 1f), 0.4f, 2.1f, squashed),
                "a corpse run over is gone: it is not run over twice");
        }

        [Test]
        public void Squash_FollowsTheBoxRotation_AndSkipsSinkingCorpses()
        {
            _corpses.Add(new float3(0f, 0f, 3f), 0f, 1f);  // along +Z: inside a box turned 90° only if its long side is X
            _corpses.Add(new float3(3f, 0f, 0f), 0f, 0f);
            _corpses.Update(1f);
            var squashed = new List<Vector3>();
            quaternion turned = quaternion.RotateY(math.PI * 0.5f);

            // Half extents (4, 1, 1) turned 90° about Y reach 4 m along Z and 1 m along X.
            Assert.AreEqual(1, _corpses.Squash(float3.zero, turned, new float3(4f, 1f, 1f), 0f, 2f, squashed));
            Assert.AreEqual(3f, squashed[0].z, 1e-4f);

            // At time 7 the corpse born at 0 has lain 6 s and is sinking: it is not run over any more.
            Assert.AreEqual(0, _corpses.Squash(float3.zero, quaternion.identity, new float3(4f, 1f, 1f), 0f, 7f, squashed));
        }
    }
}

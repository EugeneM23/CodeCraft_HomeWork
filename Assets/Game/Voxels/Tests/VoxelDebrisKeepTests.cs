using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>A debris store of its own, ticked by hand over a plane: no frames needed.</summary>
    public sealed class VoxelDebrisKeepTests
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
            _debris = new VoxelDebris(_material, 256, 4);
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
        public void Drop_LandsAsAFlatTileThatStays()
        {
            _debris.Drop(new Vector3(0f, 1f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(2f);

            Assert.AreEqual(1, _debris.LyingCount);
            Assert.AreEqual(0, _debris.Count, "a lying cube is no longer simulated");
            Assert.IsTrue(_debris.TryGetLying(0, out Matrix4x4 matrix, out _));
            Vector3 scale = matrix.lossyScale;
            Assert.That(scale.y, Is.LessThan(scale.x * 0.5f), "spread flat");
            Assert.That(matrix.GetPosition().y, Is.EqualTo(0f).Within(0.02f), "on the floor");

            Run(60f);
            Assert.AreEqual(1, _debris.LyingCount, "and it never goes");
        }

        [Test]
        public void KeptClump_LiesForGood()
        {
            int clump = _debris.BeginCluster(new Vector3(0f, 0.5f, 0f), Vector3.zero, 0f);
            for (int i = 0; i < 3; i++)
                _debris.AddCube(clump, new Vector3(i * 0.05f, 0.5f, 0f), 0.05f, Color.red);
            _debris.SetKeep(clump);

            Run(65f);

            Assert.AreEqual(3, _debris.LyingCount, "every cube, even if the clump broke on landing");
            Assert.AreEqual(0, _debris.Count);
        }

        [Test]
        public void KeptClump_ThatNeverComesToRest_LiesWhereItIsAfterItsLongestFlight()
        {
            // Nothing to land on, and high enough to be still falling at MAX_KEPT_FLIGHT (~30 s to
            // FELL_OUT_Y): it never comes to rest, and would be simulated for good.
            _debris.CollisionMask = 0;
            const float height = 10000f;
            int clump = _debris.BeginCluster(new Vector3(0f, height, 0f), Vector3.zero, 0f);
            for (int i = 0; i < 3; i++)
                _debris.AddCube(clump, new Vector3(i * 0.05f, height, 0f), 0.05f, Color.red);
            _debris.SetKeep(clump);

            Run(VoxelDebris.MAX_KEPT_FLIGHT - 1f);
            Assert.AreEqual(3, _debris.Count, "still falling");
            Assert.AreEqual(0, _debris.LyingCount);

            Run(2f);
            Assert.AreEqual(0, _debris.Count, "no longer simulated");
            Assert.AreEqual(3, _debris.LyingCount, "it lies where it got to");
            Assert.IsTrue(_debris.TryGetLying(0, out Matrix4x4 matrix, out _));
            Assert.That(matrix.GetPosition().y, Is.InRange(0f, height - 100f), "in mid-air, wherever it was");
        }

        [Test]
        public void UnkeptClump_ThatNeverComesToRest_GoesAtTheEndOfItsLife()
        {
            // Every clump that comes to rest lies for good (VoxelDebrisLyingTests); one still flying
            // when its life runs out goes. Nothing to land on, high up: it is still falling at 3 s.
            _debris.CollisionMask = 0;
            const float height = 1000f;
            int clump = _debris.BeginCluster(new Vector3(0f, height, 0f), Vector3.zero, 0f);
            _debris.AddCube(clump, new Vector3(0f, height, 0f), 0.05f, Color.red);
            _debris.SetLife(clump, 3f);

            Run(2f);
            Assert.AreEqual(1, _debris.Count, "still flying");

            Run(2f);

            Assert.AreEqual(0, _debris.Count);
            Assert.AreEqual(0, _debris.LyingCount);
        }

        [Test]
        public void Lying_OverCapacity_TheOldestGo()
        {
            for (int i = 0; i < 6; i++)
                _debris.Drop(new Vector3(i, 1f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(2f);

            Assert.AreEqual(4, _debris.LyingCount);
            var xs = new System.Collections.Generic.HashSet<int>();
            for (int slot = 0; slot < 4; slot++)
            {
                _debris.TryGetLying(slot, out Matrix4x4 matrix, out _);
                xs.Add(Mathf.RoundToInt(matrix.GetPosition().x));
            }

            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, xs);
        }

        [Test]
        public void Drop_PassesThroughLoosePieces()
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.layer = VoxelWorld.PieceLayer;
            piece.transform.position = new Vector3(0f, 0.5f, 0f);
            Physics.SyncTransforms();

            _debris.Drop(new Vector3(0f, 1.5f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(2f);
            Object.DestroyImmediate(piece);

            Assert.IsTrue(_debris.TryGetLying(0, out Matrix4x4 matrix, out _));
            Assert.That(matrix.GetPosition().y, Is.EqualTo(0f).Within(0.02f), "on the floor, not on the piece");
        }

        [Test]
        public void Drop_PassesThroughCharactersAndTheTank()
        {
            // The tank and the characters are on Character (9): a tile laid on them would hang in
            // the air once they moved off.
            var tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tank.layer = 9;
            tank.transform.position = new Vector3(0f, 0.5f, 0f);
            Physics.SyncTransforms();

            _debris.Drop(new Vector3(0f, 1.5f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(2f);
            Object.DestroyImmediate(tank);

            Assert.IsTrue(_debris.TryGetLying(0, out Matrix4x4 matrix, out _));
            Assert.That(matrix.GetPosition().y, Is.EqualTo(0f).Within(0.02f), "on the floor, not on the tank");
        }

        [Test]
        public void Drop_FallingOffTheWorld_IsDropped()
        {
            Object.DestroyImmediate(_floor);
            _floor = new GameObject("NoFloor");
            Physics.SyncTransforms();

            _debris.Drop(new Vector3(0f, 1f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(6f);

            Assert.AreEqual(0, _debris.Count);
            Assert.AreEqual(0, _debris.LyingCount);
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>Lying cubes baked into one mesh per world cell, ticked by hand over a plane.</summary>
    public sealed class VoxelDebrisCellTests
    {
        private const float STEP = 0.02f;
        private const float CUBE = 0.2f;

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
        public void TwoTouchingCubesOfOnePiece_ShareNoFace_AndShowNoBottom()
        {
            LayPiece(new Vector3(4f, CUBE * 0.5f, 4f), 2);
            Run(2f);

            Assert.AreEqual(2, _debris.LyingCount);
            Assert.AreEqual(1, _debris.LyingCellCount, "both in one cell");
            // 2 x 6 faces, less the 2 they share and the 2 on the ground: 8 faces, 16 triangles.
            Assert.AreEqual(16, _debris.LyingTriangleCount);
            Assert.AreEqual(0, _debris.LyingInstancedCount, "baked: nothing drawn instanced");
        }

        [Test]
        public void PiecesFarApart_AreTwoCells_AndACrushRebuildsOnlyItsCell()
        {
            LayPiece(new Vector3(4f, CUBE * 0.5f, 4f), 3);
            LayPiece(new Vector3(36f, CUBE * 0.5f, 4f), 3);
            Run(2f);

            Assert.AreEqual(2, _debris.LyingCellCount);
            // A row of 3: 18 faces, less 2 x 2 shared and 3 bottoms = 11 faces, 22 triangles each.
            Assert.AreEqual(44, _debris.LyingTriangleCount);

            // Crumbs fly back for longer than this and lie only SETTLE_TIME after landing; the rest
            // sink for CRUSH_SINK_TIME (0.4 s), out of the mesh.
            _debris.Crush(new Vector3(4f, CUBE * 0.5f, 4f), 0.8f, new Vector3(0f, 0f, 3f));
            Run(0.3f);
            Assert.AreEqual(3, _debris.CountLyingNear(new Vector3(36f, CUBE * 0.5f, 4f), 1f), "the far piece is untouched");
            Assert.AreEqual(22, _debris.LyingTriangleCount, "the crushed piece's cell was rebuilt empty");
        }

        [Test]
        public void SinkingCube_IsNotInTheCellMesh_ButDrawnInstanced()
        {
            LayPiece(new Vector3(4f, CUBE * 0.5f, 4f), 12);
            Run(2f);
            Assert.That(_debris.LyingTriangleCount, Is.GreaterThan(0));

            // At most 6 of the 12 fly back as crumbs; the rest sink for CRUSH_SINK_TIME (0.4 s).
            _debris.Crush(new Vector3(5.1f, CUBE * 0.5f, 4f), 2f, new Vector3(0f, 0f, 3f));
            Run(0.12f);

            Assert.That(_debris.LyingCount, Is.GreaterThanOrEqualTo(6), "still sinking");
            Assert.AreEqual(0, _debris.LyingTriangleCount, "sinking cubes left the cell mesh");
            Assert.AreEqual(_debris.LyingCount, _debris.LyingInstancedCount, "each sinking cube drawn instanced");
        }

        [Test]
        public void BloodTile_IsBaked()
        {
            _debris.Drop(new Vector3(4f, 0.5f, 4f), Vector3.zero, Color.red, 0.05f);
            Run(1f);
            Assert.AreEqual(1, _debris.LyingCount);
            // A flat box flat on the ground: 5 faces, no bottom.
            Assert.AreEqual(10, _debris.LyingTriangleCount);
        }

        /// <summary>A piece of <paramref name="cubes"/> cubes of CUBE in a row along X, starting at <paramref name="at"/>, at rest.</summary>
        private void LayPiece(Vector3 at, int cubes)
        {
            int cluster = _debris.BeginCluster(at + new Vector3((cubes - 1) * CUBE * 0.5f, 0f, 0f), Vector3.zero, 0f);
            for (int i = 0; i < cubes; i++)
                _debris.AddCube(cluster, at + new Vector3(i * CUBE, 0f, 0f), CUBE, Color.gray);
        }
    }
}

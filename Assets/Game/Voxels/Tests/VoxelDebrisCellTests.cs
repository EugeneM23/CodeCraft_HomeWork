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

        [Test]
        public void EmptiedCellSlots_AreReused_SoNewCubesAreStillBaked()
        {
            // Two cell slots, two lying cubes: each new cube pushes the one before out (eviction), so
            // its cell empties; six cells in turn need the slots back.
            var store = new VoxelDebris(_material, 256, 2, 2);
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    var at = new Vector3(4f + 16f * i, CUBE * 0.5f, 4f);
                    store.ViewPoint = at;
                    int cluster = store.BeginCluster(at, Vector3.zero, 0f);
                    store.AddCube(cluster, at, CUBE, Color.gray);
                    for (float t = 0f; t < 4f; t += STEP)
                        store.Tick(STEP);
                    store.Complete();
                }

                Assert.AreEqual(1, store.LyingCount, "the last cube lies, the others were evicted");
                Assert.AreEqual(1, store.LyingCellCount);
                Assert.AreEqual(10, store.LyingTriangleCount, "the last cube is baked: 5 faces");
                Assert.AreEqual(0, store.LyingInstancedCount, "no cube left without a cell");
            }
            finally { store.Dispose(); }
        }

        [Test]
        public void CrushedCell_IsRebuiltBeforeCellsThatOnlyGainedCubes()
        {
            var piece = new Vector3(4f, CUBE * 0.5f, 4f);
            LayPiece(piece, 3);
            Run(2f);
            int before = _debris.LyingCellTrianglesAt(piece);
            Assert.AreEqual(22, before);

            // Eight single cubes in eight other cells lie down in one step: more dirty cells than one
            // frame builds.
            for (int i = 0; i < 8; i++)
            {
                var at = new Vector3(4f + 16f * i, CUBE * 0.5f, 20f);
                int cluster = _debris.BeginCluster(at, Vector3.zero, 0f);
                _debris.AddCube(cluster, at, CUBE, Color.gray);
            }
            for (int n = 0; n < 200 && _debris.LyingCount < 11; n++)
            {
                _debris.Tick(STEP);
                _debris.Complete();
            }
            Assert.AreEqual(11, _debris.LyingCount, "the eight lay down together");

            _debris.Crush(piece + new Vector3(CUBE, 0f, 0f), 0.8f, new Vector3(0f, 0f, 3f));
            _debris.Tick(STEP); // the step that crushes
            _debris.Tick(STEP); // the step that builds
            Assert.That(_debris.LyingCellTrianglesAt(piece), Is.LessThan(before), "rebuilt within two steps");
        }

        [Test]
        public void FarCell_DrawsOnlyItsBigCubes()
        {
            LayPiece(new Vector3(4f, CUBE * 0.5f, 4f), 3);
            // Crumbs: three small cubes in a row, in the same cell.
            const float SMALL = 0.08f;
            int cluster = _debris.BeginCluster(new Vector3(6f + SMALL, SMALL * 0.5f, 6f), Vector3.zero, 0f);
            for (int i = 0; i < 3; i++)
                _debris.AddCube(cluster, new Vector3(6f + i * SMALL, SMALL * 0.5f, 6f), SMALL, Color.gray);
            Run(2f);
            Assert.AreEqual(1, _debris.LyingCellCount);

            var camera = new GameObject("ViewCamera").AddComponent<Camera>();
            try
            {
                _debris.ViewCamera = camera;
                var target = new Vector3(5f, 0f, 5f);

                camera.transform.position = new Vector3(5f, 3f, -3f);
                camera.transform.LookAt(target);
                _debris.Tick(0f);
                Assert.AreEqual(44, _debris.LyingTrianglesDrawn, "near: big cubes and crumbs");

                camera.transform.position = new Vector3(5f, 10f, -40f);
                camera.transform.LookAt(target);
                _debris.Tick(0f);
                Assert.AreEqual(22, _debris.LyingTrianglesDrawn, "past LOD_DISTANCE: only the big cubes");
            }
            finally
            {
                _debris.ViewCamera = null;
                Object.Destroy(camera.gameObject);
            }
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

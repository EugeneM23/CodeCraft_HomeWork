using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Horde.Tests
{
    /// <summary>
    /// The grid reading real colliders with RaycastCommand in edit mode, built far from
    /// anything the open scene has (the default physics scene is shared in the editor).
    /// </summary>
    public class HordeNavGridPhysicsTests
    {
        private const int LAYER_GROUND = 3;
        private const int LAYER_ENEMY = 8;
        private static readonly Vector3 Site = new(5000f, 1000f, 5000f);

        private readonly System.Collections.Generic.List<GameObject> _objects = new();
        private Scene _scene;
        private bool _ownScene;
        private GameObject _house;

        [SetUp]
        public void SetUp()
        {
            // The test runner usually works in its own untitled scene, and then no new scene can be added
            // next to it: build in that one. Next to a saved scene, build in a throwaway additive scene so
            // the saved one never gets dirty.
            _scene = SceneManager.GetActiveScene();
            _ownScene = !string.IsNullOrEmpty(_scene.path);
            if (_ownScene)
                _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Box("Ground", Site + new Vector3(0f, -0.1f, 0f), new Vector3(20f, 0.2f, 20f), LAYER_GROUND);
            _house = Box("House", Site + new Vector3(0f, 2f, 0f), new Vector3(2f, 4f, 2f), 0);
            Box("Monster", Site + new Vector3(3f, 1.5f, 3f), new Vector3(1f, 3f, 1f), LAYER_ENEMY);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
                if (go != null)
                    Object.DestroyImmediate(go);
            _objects.Clear();
            Physics.SyncTransforms();
            if (_ownScene && _scene.IsValid())
                EditorSceneManager.CloseScene(_scene, true);
        }

        private GameObject Box(string name, Vector3 center, Vector3 size, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            SceneManager.MoveGameObjectToScene(go, _scene);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            _objects.Add(go);
            return go;
        }

        private static HordeNavGrid MakeGrid()
        {
            var settings = HordeNavGridSettings.Cover(new float2(Site.x - 5f, Site.z - 5f), new float2(Site.x + 5f, Site.z + 5f));
            settings.RayTop = Site.y + 20f;
            settings.RayLength = 40f;
            return new HordeNavGrid(settings);
        }

        private static int2 CellAt(HordeNavGrid grid, float x, float z)
        {
            Assert.IsTrue(grid.TryCell(new float3(Site.x + x, 0f, Site.z + z), out int2 cell));
            return cell;
        }

        [Test]
        public void BuildAll_ReadsHouseAsTall_IgnoresEnemies()
        {
            using HordeNavGrid grid = MakeGrid();
            Assert.AreEqual(new int2(20, 20), grid.Size);
            grid.BuildAll();

            int2 roof = CellAt(grid, 0.75f, 0.25f); // last cell under the roof
            int2 beside = CellAt(grid, 1.25f, 0.25f);
            Assert.AreEqual(Site.y + 4f, grid.HeightOf(roof), 0.01f, "the ray lands on the roof");
            Assert.AreEqual(Site.y, grid.HeightOf(beside), 0.01f);
            Assert.AreEqual(Site.y, grid.HeightOf(CellAt(grid, 3.25f, 3.25f)), 0.01f, "the monster layer is not terrain");
            Assert.IsFalse(grid.CanStep(beside, roof));
            Assert.IsTrue(grid.HasGround(CellAt(grid, -4.75f, 4.75f)));
        }

        [Test]
        public void MarkDirty_AfterHouseMoves_OpensTheWay()
        {
            using HordeNavGrid grid = MakeGrid();
            grid.BuildAll();
            int2 roof = CellAt(grid, 0.75f, 0.25f); // last cell under the roof
            int2 beside = CellAt(grid, 1.25f, 0.25f);
            Assert.IsFalse(grid.CanStep(beside, roof));

            Bounds before = _house.GetComponent<BoxCollider>().bounds;
            _house.transform.position += new Vector3(100f, 0f, 0f);
            Physics.SyncTransforms();
            int version = grid.Version;

            grid.MarkDirty(before);
            Assert.AreEqual(1, grid.DirtyRectCount);
            grid.ScheduleUpdate();
            Assert.IsTrue(grid.IsUpdating);
            Assert.IsTrue(grid.CompleteUpdate(), "heights changed");

            Assert.Greater(grid.Version, version);
            Assert.AreEqual(Site.y, grid.HeightOf(roof), 0.01f);
            Assert.IsTrue(grid.CanStep(beside, roof));
            Assert.AreEqual(0, grid.DirtyRectCount);
            Assert.IsFalse(grid.CompleteUpdate(), "nothing more in flight");
        }

        [Test]
        public void MarkDirty_AfterHouseIsRemoved_OpensTheWay()
        {
            using HordeNavGrid grid = MakeGrid();
            grid.BuildAll();
            Bounds before = _house.GetComponent<BoxCollider>().bounds;
            Object.DestroyImmediate(_house);
            Physics.SyncTransforms();

            grid.MarkDirty(before);
            grid.ScheduleUpdate();
            Assert.IsTrue(grid.CompleteUpdate());
            int2 roof = CellAt(grid, -0.75f, -0.75f);
            Assert.AreEqual(Site.y, grid.HeightOf(roof), 0.01f);

            using var finder = new HordePathfinder(grid);
            Assert.AreEqual(HordePathStatus.Found, finder.FindPath(Site + new Vector3(-4f, 0f, 0.1f), Site + new Vector3(4f, 0f, 0.1f)));
            Assert.AreEqual(2, finder.Path.Length, "straight through where the house stood");
        }
    }
}

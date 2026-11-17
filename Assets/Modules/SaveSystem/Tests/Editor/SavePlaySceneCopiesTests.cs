using System.Linq;
using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Tests
{
    /// <summary>
    /// A scene with unsaved changes at the start of Play gets a copy that loads go to, so a load brings back the scene
    /// that was played and not the older file on disk.
    /// </summary>
    public class SavePlaySceneCopiesTests
    {
        private static readonly string PARENT = TestScenes.MODULE.StartsWith("Packages/")
            ? "Assets"
            : TestScenes.MODULE + "/Tests/Editor";
        private static readonly string FOLDER = PARENT + "/TempScenes";
        private static readonly string SCENE = FOLDER + "/PlayCopyProbe.unity";

        private TempScene _temp;
        private Scene _scene;
        private GameObject _hero;

        [SetUp]
        public void SetUp()
        {
            // Next to a saved scene or in place of an untitled one (the test runner may start from it): see TempScene
            _temp = new TempScene();
            _scene = _temp.Scene;
            AssetDatabase.CreateFolder(PARENT, "TempScenes");
            _hero = new GameObject("Hero");
            _hero.AddComponent<Saveable>();
            EditorSceneManager.SaveScene(_scene, SCENE);
        }

        [TearDown]
        public void TearDown()
        {
            SaveSceneCopies.Clear();
            _temp?.Dispose();
            _temp = null;
            AssetDatabase.DeleteAsset(FOLDER);
            AssetDatabase.DeleteAsset(SavePlaySceneCopies.FOLDER);
            foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                SaveCatalogBuilder.Rebuild(settings);
        }

        [Test]
        public void SavedScene_NeedsNoCopy()
        {
            Assert.IsFalse(SavePlaySceneCopies.Make(_scene));
            Assert.AreEqual(SCENE, SaveSceneCopies.ToLoad(SCENE), "loads take the scene itself");
        }

        [Test]
        public void SceneWithUnsavedChanges_GetsACopyThatLoadsGoTo_AndStaysAsItWas()
        {
            new GameObject("AddedAfterTheLastSave").AddComponent<Saveable>();
            EditorSceneManager.MarkSceneDirty(_scene);

            Assert.IsTrue(SavePlaySceneCopies.Make(_scene));

            string copy = SaveSceneCopies.ToLoad(SCENE);
            Assert.AreNotEqual(SCENE, copy);
            Assert.IsTrue(SavePlaySceneCopies.IsCopy(copy));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(copy));
            StringAssert.EndsWith("/PlayCopyProbe.unity", copy, "the scene keeps its name: a game may look at it");
            Assert.AreEqual(SCENE, SaveSceneCopies.ToSave(copy), "saves know the scene by the original's path");
            StringAssert.Contains("AddedAfterTheLastSave", System.IO.File.ReadAllText(copy));
            StringAssert.DoesNotContain("AddedAfterTheLastSave", System.IO.File.ReadAllText(SCENE), "the scene's own file is not written");
            Assert.AreEqual(SCENE, _scene.path);
            Assert.IsTrue(_scene.isDirty, "and its unsaved changes are still unsaved");
        }

        [Test]
        public void Copies_AreNotScenesOfTheGame_TheCatalogLeavesThemOut()
        {
            EditorSceneManager.MarkSceneDirty(_scene);
            SavePlaySceneCopies.Make(_scene);
            string copy = SaveSceneCopies.ToLoad(SCENE);

            string[] catalog = SaveCatalogBuilder.BuildSceneEntries().Select(entry => entry.Path).ToArray();

            CollectionAssert.Contains(catalog, SCENE);
            CollectionAssert.DoesNotContain(catalog, copy);
        }

        [Test]
        public void SceneWithNothingToSave_NeedsNoCopy()
        {
            Object.DestroyImmediate(_hero);
            new GameObject("JustAnObject");
            EditorSceneManager.MarkSceneDirty(_scene);

            Assert.IsFalse(SavePlaySceneCopies.Make(_scene));
        }
    }
}

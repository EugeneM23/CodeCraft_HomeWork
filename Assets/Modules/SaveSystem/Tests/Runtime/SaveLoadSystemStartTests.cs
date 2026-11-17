using System.Collections;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// A SaveLoadSystem placed in a level with Load On Start: the level that has just started gets its saved state in
    /// place. Reloading it from disk would cost a second load and, in the editor, drop the scene's unsaved edits.
    /// </summary>
    public class SaveLoadSystemStartTests
    {
        private InMemorySaveStorage _storage;
        private SaveSystemSettings _settings;
        private int _loads;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            _settings = TestScenes.CreateSettings();
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= AddSystem;
            SceneManager.sceneLoaded -= CountLoads;
            if (SaveLoadSystem.Current != null)
                Object.DestroyImmediate(SaveLoadSystem.Current.gameObject);
            Object.DestroyImmediate(_settings);
        }

        private SaveLoadSystem NewSystem(bool loadOnStart, Scene? scene = null)
        {
            var go = new GameObject("Level");
            if (scene.HasValue)
                SceneManager.MoveGameObjectToScene(go, scene.Value);

            var system = go.AddComponent<SaveLoadSystem>();
            system.UseStorage(_storage);
            system.UseSettings(_settings);
            system.Configure(loadOnStart: loadOnStart, saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            return system;
        }

        // The system sits in the level as if placed there: made while the scene loads, before anything in it starts
        private void AddSystem(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= AddSystem; // only the level's own load gets one
            NewSystem(loadOnStart: true, scene);
        }

        private void CountLoads(Scene scene, LoadSceneMode mode) => _loads++;

        [UnityTest]
        public IEnumerator LoadOnStart_InTheLevel_AppliesInPlaceWithoutReloading()
        {
#if UNITY_EDITOR
            yield return TestScenes.Load();
            SaveLoadSystem writer = NewSystem(loadOnStart: false);
            TestScenes.Find("Box").transform.position = new Vector3(4f, 5f, 6f);
            writer.Save();
            Object.DestroyImmediate(writer.gameObject);

            SceneManager.sceneLoaded += AddSystem;
            SceneManager.sceneLoaded += CountLoads;
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(TestScenes.SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestScenes.WaitFor(() => scene.isLoaded);
            for (int frame = 0; frame < 5; frame++)
                yield return null;

            Assert.AreEqual(1, _loads, "the level is not loaded a second time");
            Assert.That(Vector3.Distance(TestScenes.Find("Box").transform.position, new Vector3(4f, 5f, 6f)), Is.LessThan(0.001f));
#else
            Assert.Ignore("The save system test scenes load only in the editor.");
            yield break;
#endif
        }
    }
}

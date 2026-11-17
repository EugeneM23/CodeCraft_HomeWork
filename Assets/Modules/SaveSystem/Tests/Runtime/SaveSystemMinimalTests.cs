using System.Collections;
using System.Linq;
using NUnit.Framework;
using Saving.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>The minimal demo: a spinning cube with a Saveable, Save and Load buttons wired straight to the SaveSystem.</summary>
    public class SaveSystemMinimalTests
    {
        private static readonly string SCENE = TestScenes.MODULE + "/Demo/SaveSystemMinimal.unity";

        private InMemorySaveStorage _storage;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _storage = new InMemorySaveStorage();
            SceneManager.sceneLoaded += UseMemory;
            Scene scene = EditorSceneManager.LoadSceneInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestScenes.WaitFor(() => scene.isLoaded);
            SceneManager.sceneLoaded -= UseMemory;
            yield return null;
#else
            Assert.Ignore("The demo scene loads only in the editor.");
            yield break;
#endif
        }

        private void UseMemory(Scene scene, LoadSceneMode mode) => SaveLoadSystem.Current.UseStorage(_storage);

        [TearDown]
        public void TearDown()
        {
            if (SaveLoadSystem.Current != null)
                Object.DestroyImmediate(SaveLoadSystem.Current.gameObject);
        }

        private static Transform Cube => Object.FindFirstObjectByType<DemoSpinner>().transform;

        private static void Press(string label) =>
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(button => button.name == label).onClick.Invoke();

        // The cube keeps spinning after the load: compared the moment the load is over
        private static Quaternion _rotationAtLoad;

        private static IEnumerator LoadAndWait()
        {
            bool done = false;
            void OnLoaded(LoadOutcome _)
            {
                done = true;
                _rotationAtLoad = Cube.rotation;
            }

            SaveLoadSystem.Current.Loaded += OnLoaded;
            Press("Load");
            yield return TestScenes.WaitFor(() => done);
            SaveLoadSystem.Current.Loaded -= OnLoaded;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Minimal_SaveSpinLoad_RotationComesBack()
        {
            yield return null;
            Press("Save");
            Quaternion saved = Cube.rotation;
            Cube.Rotate(0f, 90f, 0f);

            yield return LoadAndWait();

            Assert.Less(Quaternion.Angle(saved, _rotationAtLoad), 5f, "back to the saved angle when the load is over");
        }

        [UnityTest]
        public IEnumerator Minimal_AfterLoad_ButtonsStillWork()
        {
            Press("Save");
            yield return LoadAndWait();
            Cube.rotation = Quaternion.Euler(0f, 123f, 0f);
            Press("Save");
            Quaternion saved = Cube.rotation;
            Cube.Rotate(0f, 90f, 0f);

            yield return LoadAndWait();

            Assert.Less(Quaternion.Angle(saved, Cube.rotation), 5f);
        }

        [Test]
        public void Minimal_NoAutosaveAndNoLoadOnStart()
        {
            Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE), "nothing is saved until Save is pressed");
        }
    }
}

using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace Loading.Tests
{
    /// <summary>
    /// The module brings its own scene: it joins Build Settings before Play Mode starts (a scene added inside Play Mode
    /// is not loadable) and leaves them after the run.
    /// </summary>
    public sealed class LoadingTestSceneSetup : IPrebuildSetup, IPostBuildCleanup
    {
        public const string SCENE_NAME = "LoadingTestScene";

        public void Setup()
        {
#if UNITY_EDITOR
            string path = ScenePath();
            if (EditorBuildSettings.scenes.All(scene => scene.path != path))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(path, true)).ToArray();
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            string path = ScenePath();
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(scene => scene.path != path).ToArray();
#endif
        }

#if UNITY_EDITOR
        private static string ScenePath() =>
            AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"{SCENE_NAME} t:Scene").First());
#endif
    }

    [PrebuildSetup(typeof(LoadingTestSceneSetup))]
    [PostBuildCleanup(typeof(LoadingTestSceneSetup))]
    public class LoadSceneOperationTests
    {
        private const string TEST_SCENE = LoadingTestSceneSetup.SCENE_NAME;
        private const string OUTSIDE_BUILD_SCENE = "LoadingOutsideBuildScene";
        private const int MAX_FRAMES = 600;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (string name in new[] { TEST_SCENE, OUTSIDE_BUILD_SCENE })
            {
                Scene scene = SceneManager.GetSceneByName(name);
                if (scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [Test]
        public void LoadScene_NotInBuildSettings_FailsWithoutLoading()
        {
            var operation = new LoadSceneOperation("NoSuchScene_42", LoadSceneMode.Additive);

            operation.Begin();

            Assert.AreEqual(LoadingStatus.Failed, operation.Status);
            StringAssert.Contains("NoSuchScene_42", operation.Error);
            StringAssert.Contains("Build Settings", operation.Error);
            Assert.IsFalse(SceneManager.GetSceneByName("NoSuchScene_42").IsValid());
        }

        [Test]
        public void LoadScene_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new LoadSceneOperation(""));
        }

        [Test]
        public void LoadScene_DefaultWeight_IsOne()
        {
            Assert.AreEqual(1f, new LoadSceneOperation(TEST_SCENE).Weight);
        }

        [UnityTest]
        public IEnumerator LoadScene_BuildScene_LoadsAndReachesFullProgress()
        {
            var operation = new LoadSceneOperation(TEST_SCENE, LoadSceneMode.Additive);

            operation.Begin();
            Assert.AreEqual(LoadingStatus.Running, operation.Status, operation.Error);

            for (int frame = 0; frame < MAX_FRAMES && !operation.IsDone; frame++)
            {
                yield return null;
                operation.Tick(Time.unscaledDeltaTime);
            }

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status, operation.Error);
            Assert.AreEqual(1f, operation.Progress);
            Assert.IsTrue(SceneManager.GetSceneByName(TEST_SCENE).isLoaded);
        }

        [UnityTest]
        public IEnumerator LoadScene_ByPath_Loads()
        {
            var operation = new LoadSceneOperation(PathOf(TEST_SCENE), LoadSceneMode.Additive);

            yield return Run(operation);

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status, operation.Error);
            Assert.IsTrue(SceneManager.GetSceneByName(TEST_SCENE).isLoaded);
        }

        [UnityTest]
        public IEnumerator LoadScene_PathOutsideBuild_LoadsInEditor()
        {
            var operation = new LoadSceneOperation(PathOf(OUTSIDE_BUILD_SCENE), LoadSceneMode.Additive);

            yield return Run(operation);

            Assert.AreEqual(LoadingStatus.Succeeded, operation.Status, operation.Error);
            Assert.IsTrue(SceneManager.GetSceneByName(OUTSIDE_BUILD_SCENE).isLoaded);
        }

        private static IEnumerator Run(LoadingOperation operation)
        {
            operation.Begin();
            for (int frame = 0; frame < MAX_FRAMES && !operation.IsDone; frame++)
            {
                yield return null;
                operation.Tick(Time.unscaledDeltaTime);
            }
        }

        private static string PathOf(string sceneName)
        {
#if UNITY_EDITOR
            return AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"{sceneName} t:Scene").First());
#else
            Assert.Ignore("Scene paths are found through the AssetDatabase: editor only");
            return null;
#endif
        }
    }
}

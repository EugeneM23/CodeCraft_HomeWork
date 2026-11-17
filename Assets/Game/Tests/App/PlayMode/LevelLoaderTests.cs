using System.Collections;
using Loading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// LevelLoader on real scenes: two test scenes outside Build Settings, loaded by path (the editor allows it).
    /// </summary>
    public class LevelLoaderTests
    {
        private const string SCENE_A = "Assets/Game/Tests/App/PlayMode/Scenes/CoreTestLevel_A.unity";
        private const string SCENE_B = "Assets/Game/Tests/App/PlayMode/Scenes/CoreTestLevel_B.unity";
        private const int MAX_FRAMES = 600;

        private LevelConfig _level;
        private LevelLoader _loader;

        [SetUp]
        public void SetUp()
        {
            _loader = new LevelLoader();
            _level = ScriptableObject.CreateInstance<LevelConfig>();
            _level.Configure("core_test", "", 1, SCENE_A, SCENE_B);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_level);

            // The test scenes may be all that is loaded: Unity keeps the last scene, so give it another one first
            Scene empty = SceneManager.CreateScene("LevelLoaderTests_Empty");
            SceneManager.SetActiveScene(empty);
            foreach (string path in new[] { SCENE_A, SCENE_B })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        [UnityTest]
        public IEnumerator Load_LoadsEveryScene_MakesTheActiveOneActive_AndProgressGoesUpToOne()
        {
            var runner = new LoadingRunner();
            LoadingResult? result = null;
            Assert.IsTrue(runner.Run(_loader.Load(_level), r => result = r));

            float last = runner.Progress;
            Assert.Less(last, 1f, "Scenes do not load within Run");
            for (int frame = 0; frame < MAX_FRAMES && runner.IsRunning; frame++)
            {
                yield return null;
                runner.Tick(Time.unscaledDeltaTime);
                Assert.GreaterOrEqual(runner.Progress, last - 1e-4f, "Progress never goes back");
                last = runner.Progress;
            }

            Assert.IsTrue(result.HasValue, "The load did not finish");
            Assert.IsTrue(result.Value.Succeeded, result.Value.Error);
            Assert.AreEqual(1f, runner.Progress);
            Assert.IsTrue(SceneManager.GetSceneByPath(SCENE_A).isLoaded);
            Assert.IsTrue(SceneManager.GetSceneByPath(SCENE_B).isLoaded);
            Assert.AreEqual(SCENE_B, SceneManager.GetActiveScene().path);
        }

        [UnityTest]
        public IEnumerator Unload_UnloadsTheScenesOfTheLevel_ButTheActiveOne()
        {
            yield return Run(_loader.Load(_level));

            yield return Run(_loader.Unload(_level));

            Assert.IsFalse(SceneManager.GetSceneByPath(SCENE_A).isLoaded);
            Assert.IsTrue(SceneManager.GetSceneByPath(SCENE_B).isLoaded);
        }

        [UnityTest]
        public IEnumerator LoadMissing_LoadsOnlyTheScenesNotOpen()
        {
            yield return Run(_loader.LoadScene(SCENE_A));
            Assert.AreEqual(SCENE_A, SceneManager.GetActiveScene().path);

            yield return Run(_loader.LoadMissing(_level));

            int loadedA = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == SCENE_A)
                    loadedA++;
            }

            Assert.AreEqual(1, loadedA, "The open scene is not loaded twice");
            Assert.IsTrue(SceneManager.GetSceneByPath(SCENE_B).isLoaded);
            Assert.AreEqual(SCENE_B, SceneManager.GetActiveScene().path);
        }

        private static IEnumerator Run(LoadingOperation tree)
        {
            var runner = new LoadingRunner();
            LoadingResult? result = null;
            runner.Run(tree, r => result = r);
            for (int frame = 0; frame < MAX_FRAMES && runner.IsRunning; frame++)
            {
                yield return null;
                runner.Tick(Time.unscaledDeltaTime);
            }

            Assert.IsTrue(result.HasValue, "The load did not finish");
            Assert.IsTrue(result.Value.Succeeded, result.Value.Error);
        }
    }
}

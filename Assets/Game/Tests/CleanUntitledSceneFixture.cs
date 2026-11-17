using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    /// <summary>
    /// Wraps every test under Game.Tests. The test runner runs EditMode tests in its own untitled scene, and these
    /// tests make and destroy objects in it, so it ends up marked dirty. The SaveSystem module's tests, which run
    /// later in a full run, refuse a dirty untitled scene (they cannot tell it from the user's unsaved work). Afterwards
    /// the runner's dirty untitled scene is swapped for a clean empty one; the runner restores the user's scenes at
    /// the end of the run as usual. A saved scene is never touched.
    /// </summary>
    [SetUpFixture]
    public sealed class CleanUntitledSceneFixture
    {
        [OneTimeTearDown]
        public void LeaveCleanUntitledScene()
        {
            if (SceneManager.sceneCount != 1)
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) || !scene.isDirty)
                return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}

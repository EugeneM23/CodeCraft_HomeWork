using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace AIKit.Tests
{
    /// <summary>
    /// An empty scene made active for the test: new objects land in it, and the user's scenes stay clean (no "save
    /// changes?" before the next PlayMode run). Next to a saved scene it is additive and closed afterwards. A fresh
    /// editor has only an untitled scene, which Unity does not allow company: an unchanged one is replaced and a clean
    /// untitled scene is left afterwards; one with unsaved changes is not touched and the test is inconclusive.
    /// </summary>
    internal sealed class TempScene : IDisposable
    {
        private readonly Scene _previous;
        private readonly bool _replacedUntitled;

        public TempScene()
        {
            _previous = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(_previous.path))
            {
                if (_previous.isDirty)
                    Assert.Inconclusive("Save or discard the untitled scene before running these tests.");

                _replacedUntitled = true;
                Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return;
            }

            Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(Scene);
        }

        public Scene Scene { get; }

        public void Dispose()
        {
            if (_replacedUntitled)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return;
            }

            if (_previous.IsValid())
                SceneManager.SetActiveScene(_previous);

            EditorSceneManager.CloseScene(Scene, removeScene: true);
        }
    }
}

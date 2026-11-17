using System;
using Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Unloads a loaded scene by its path through SceneManager.UnloadSceneAsync. A scene that is not loaded is already
    /// where it should be — succeeds at once. Unity refuses to unload the last loaded scene: that fails.
    /// </summary>
    public sealed class UnloadSceneOperation : LoadingOperation
    {
        private readonly string _path;
        private AsyncOperation _operation;

        public UnloadSceneOperation(string path, float weight = 1f) : base(weight)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Scene path is empty", nameof(path));

            _path = path;
        }

        public string ScenePath => _path;

        protected override void OnBegin()
        {
            _operation = null;
            Scene scene = SceneManager.GetSceneByPath(_path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Succeed();
                return;
            }

            _operation = SceneManager.UnloadSceneAsync(scene);
            if (_operation == null)
                Fail($"Scene '{_path}' could not be unloaded (the last loaded scene cannot be)");
        }

        protected override void OnTick(float dt)
        {
            if (_operation.isDone)
                Succeed();
        }

        protected override float MeasureProgress() => _operation == null ? 0f : _operation.progress;
    }
}

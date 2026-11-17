using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Loading
{
    /// <summary>
    /// Loads a scene by name or path through SceneManager.LoadSceneAsync. A scene missing from Build Settings fails
    /// before anything is loaded, so the current scene stays intact — except in the editor, where a path to a scene
    /// asset loads it anyway (sandboxes outside the build). Progress follows the async operation (Unity reports
    /// 0..0.9 for loading, the rest is activation).
    /// </summary>
    public sealed class LoadSceneOperation : LoadingOperation
    {
        private const float LOADED_PROGRESS = 0.9f;

        private readonly string _scene;
        private readonly LoadSceneMode _mode;
        private AsyncOperation _operation;

        public LoadSceneOperation(string scene, LoadSceneMode mode = LoadSceneMode.Single, float weight = 1f)
            : base(weight)
        {
            if (string.IsNullOrEmpty(scene))
                throw new ArgumentException("Scene name is empty", nameof(scene));

            _scene = scene;
            _mode = mode;
        }

        /// <summary>The name or the path the operation was made with.</summary>
        public string SceneName => _scene;

        protected override void OnBegin()
        {
            _operation = null;
            if (Application.CanStreamedLevelBeLoaded(_scene))
                _operation = SceneManager.LoadSceneAsync(_scene, _mode);
#if UNITY_EDITOR
            else if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(_scene) != null)
                _operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(_scene,
                    new LoadSceneParameters(_mode));
#endif
            else
            {
                Fail($"Scene '{_scene}' is not in Build Settings");
                return;
            }

            if (_operation == null)
                Fail($"Scene '{_scene}' could not be loaded");
        }

        protected override void OnTick(float dt)
        {
            if (_operation.isDone)
                Succeed();
        }

        protected override float MeasureProgress() =>
            _operation == null ? 0f : _operation.progress / LOADED_PROGRESS;
    }
}

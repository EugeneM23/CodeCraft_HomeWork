using System;
using Loading;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Makes a loaded scene active (its lighting is used, new objects go there). Weightless. Unity can refuse it in
    /// the frame other scenes finish loading or unloading, so a refusal is tried again on the next ticks, up to
    /// MAX_TRIES. A scene that is not loaded fails.
    /// </summary>
    public sealed class SetActiveSceneOperation : LoadingOperation
    {
        private const int MAX_TRIES = 30;

        private readonly string _path;
        private int _tries;

        public SetActiveSceneOperation(string path) : base(0f)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Scene path is empty", nameof(path));

            _path = path;
        }

        public string ScenePath => _path;

        protected override void OnBegin()
        {
            _tries = 0;
            TryActivate();
        }

        protected override void OnTick(float dt) => TryActivate();

        private void TryActivate()
        {
            Scene scene = SceneManager.GetSceneByPath(_path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Fail($"Scene '{_path}' is not loaded and cannot be made active");
                return;
            }

            if (SceneManager.GetActiveScene() == scene || SceneManager.SetActiveScene(scene))
            {
                Succeed();
                return;
            }

            if (++_tries >= MAX_TRIES)
                Fail($"Scene '{_path}' could not be made active");
        }
    }
}

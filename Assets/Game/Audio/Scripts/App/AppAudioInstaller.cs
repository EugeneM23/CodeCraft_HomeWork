using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Brings up <see cref="AppAudio"/> (menu sounds, music, volumes) with the app: on the App prefab, after the
    /// SaveInstaller (AppAudio reads the settings store). Without the app — tests, a bare scene — there is no AppAudio,
    /// so nothing plays without an AudioListener. Play Mode only: installers also run in Edit Mode.
    /// </summary>
    public sealed class AppAudioInstaller : MonoEntityInstaller<AppContext>
    {
        [Tooltip("Assets/Game/Audio/Prefabs/AppAudio.prefab")]
        [SerializeField] private AppAudio _prefab;

        private AppAudio _instance;

        public override void Install(AppContext context)
        {
            if (!AtomicUtils.IsPlayMode() || AppAudio.Instance != null)
                return;

            if (_prefab == null)
            {
                Debug.LogError($"{nameof(AppAudioInstaller)}: no AppAudio prefab, the game has no music or menu sounds", this);
                return;
            }

            // A root object: AppAudio keeps itself across scenes (DontDestroyOnLoad) like the app does.
            _instance = Instantiate(_prefab);
            _instance.name = _prefab.name;
        }

        public override void Uninstall(AppContext context)
        {
            if (_instance != null)
                Destroy(_instance.gameObject);
            _instance = null;
        }
    }
}

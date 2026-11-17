using UnityEngine;

namespace Game
{
    /// <summary>
    /// On the App prefab: writes the progress (when dirty) and the settings (when changed) as the app loses focus, is
    /// paused by the OS or quits. SaveInstaller binds the services.
    /// </summary>
    public sealed class AppLifecycleSaves : MonoBehaviour
    {
        private SettingsStore _settings;
        private PlayerProgress _progress;

        public void Bind(SettingsStore settings, PlayerProgress progress)
        {
            _settings = settings;
            _progress = progress;
        }

        public void SaveAll()
        {
            if (_progress != null && _progress.IsDirty)
                _progress.Save();

            _settings?.SaveCurrent();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SaveAll();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveAll();
        }

        private void OnApplicationQuit() => SaveAll();
    }
}

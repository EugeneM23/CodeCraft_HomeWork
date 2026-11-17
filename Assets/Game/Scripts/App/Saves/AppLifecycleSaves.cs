using UnityEngine;

namespace Game
{
    /// <summary>
    /// On the App prefab: writes a dirty progress as the app loses focus, is paused by the OS or quits, and the
    /// settings once more on quit. The settings are not checked on focus loss: they change only through
    /// SettingsStore.Save, which writes them at once, and checking means serializing them (kilobytes of garbage on
    /// every switch to another window). SaveInstaller binds the services.
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

        /// <summary>The progress if dirty and the settings if changed.</summary>
        public void SaveAll()
        {
            SaveProgress();
            _settings?.SaveCurrent();
        }

        private void SaveProgress()
        {
            if (_progress != null && _progress.IsDirty)
                _progress.Save();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SaveProgress();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveProgress();
        }

        private void OnApplicationQuit() => SaveAll();
    }
}

using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Settings and progress of the app: loads both from disk, applies the graphics, puts the services into the context
    /// and starts the autosave on the AppTicker. Stands after AppInstaller in the installer list (it needs AppTicker
    /// and, if present, GameFlow).
    /// </summary>
    public sealed class SaveInstaller : MonoEntityInstaller<AppContext>
    {
        [SerializeField, Min(0f), Tooltip("Seconds of real time after the last progress change before it is written.")]
        private float _autosaveDelay = 3f;

        [SerializeField, Tooltip("Writes the saves on focus loss, OS pause and quit. On the App prefab.")]
        private AppLifecycleSaves _lifecycle;

        private SettingsStore _settings;
        private PlayerProgress _progress;
        private ProgressAutosave _autosave;
        private AppTicker _ticker;

        public override void Install(AppContext context)
        {
            if (!AtomicUtils.IsPlayMode())
                return;

            _settings = new SettingsStore(SaveStores.Settings(), SaveStores.GameVersion);
            _settings.Load();
            _settings.Apply();

            _progress = new PlayerProgress(SaveStores.Progress(), SaveStores.GameVersion);
            _progress.Load();

            context.AddSettings(_settings);
            context.AddProgress(_progress);

            context.TryGetGameFlow(out GameFlow flow);
            _autosave = new ProgressAutosave(_progress, flow, _autosaveDelay);
            if (context.TryGetAppTicker(out _ticker) && _ticker != null)
                _ticker.Add(_autosave);
            else
                Debug.LogWarning("SaveInstaller: no AppTicker in the context, the progress is saved only on " +
                                 "transitions and app pause or quit. Put AppInstaller before SaveInstaller.", this);

            if (_lifecycle != null)
                _lifecycle.Bind(_settings, _progress);
            else
                Debug.LogWarning("SaveInstaller: no AppLifecycleSaves, the saves are not written on quit.", this);
        }

        public override void Uninstall(AppContext context)
        {
            if (_autosave == null)
                return;

            if (_ticker != null)
                _ticker.Remove(_autosave);

            _autosave.Dispose();
            _autosave = null;
            _ticker = null;

            if (_progress.IsDirty)
                _progress.Save();

            _settings.SaveCurrent();

            if (_lifecycle != null)
                _lifecycle.Bind(null, null);

            context.DelSettings();
            context.DelProgress();
            _settings = null;
            _progress = null;
        }
    }
}

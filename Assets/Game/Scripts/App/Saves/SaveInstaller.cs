using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Settings and progress of the app: loads both from disk, applies the graphics, puts the services into the context
    /// and starts the autosave on the AppTicker. The progress is one <see cref="MetaProgress"/> under two keys:
    /// MetaProgress (IMetaProgress, the run and the new screens) and Progress (the legacy IPlayerProgress of the old
    /// menu). Stands after AppInstaller in the installer list (it needs the GameCatalog, AppTicker and, if present,
    /// GameFlow).
    /// </summary>
    public sealed class SaveInstaller : MonoEntityInstaller<AppContext>
    {
        [SerializeField, Min(0f), Tooltip("Seconds of real time after the last progress change before it is written.")]
        private float _autosaveDelay = 3f;

        [SerializeField, Tooltip("Writes the saves on focus loss, OS pause and quit. On the App prefab.")]
        private AppLifecycleSaves _lifecycle;

        private SettingsStore _settings;
        private MetaProgress _progress;
        private ProgressAutosave _autosave;
        private AppTicker _ticker;
        private GameCatalog _fallbackCatalog;

        public override void Install(AppContext context)
        {
            if (!AtomicUtils.IsPlayMode())
                return;

            _settings = new SettingsStore(SaveStores.Settings(), SaveStores.GameVersion);
            _settings.Load();
            _settings.Apply();

            if (!context.TryGetGameCatalog(out GameCatalog catalog) || catalog == null)
            {
                Debug.LogError("SaveInstaller: no GameCatalog in the context, the progress has an empty catalog " +
                               "(nothing owned or for sale). Put AppInstaller with its catalog before SaveInstaller.", this);
                _fallbackCatalog = ScriptableObject.CreateInstance<GameCatalog>();
                catalog = _fallbackCatalog;
            }

            _progress = new MetaProgress(SaveStores.Progress(), SaveStores.GameVersion, catalog);
            _progress.Load();

            context.AddSettings(_settings);
            context.AddMetaProgress(_progress);
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
            context.DelMetaProgress();
            context.DelProgress();
            _settings = null;
            _progress = null;

            if (_fallbackCatalog != null)
            {
                Destroy(_fallbackCatalog);
                _fallbackCatalog = null;
            }
        }
    }
}

using UnityEngine;

namespace Game
{
    /// <summary>
    /// Where the UI takes the game-wide services from. With the app running (AppContext: Boot, or the core's
    /// editor cold start) they are the core's — game flow, pause, settings, progress — and the UI's own game
    /// input installed there by GameInputInstaller. Without it (a test, a scene played on its own) each falls
    /// back to a simple stub. Volumes and interface sounds are set by the sound bridge (Game.UI.Adapters).
    /// A value set here (tests) wins over both. Screens read services in one place (Awake/Open).
    /// </summary>
    public static class AppServices
    {
        // Set explicitly (tests, the sound bridge): wins over the app and the stubs
        private static IGameFlow s_gameFlow;
        private static IPauseService s_pause;
        private static ISettingsStore s_settingsStore;
        private static IPlayerProgress s_progress;
        private static IAudioVolumes s_audio;
        private static IUiSounds s_uiSounds;
        private static IGameInput s_input;

        // Made on first use when there is no app
        private static SceneGameFlow s_stubFlow;
        private static TimeScalePauseService s_stubPause;
        private static JsonSettingsStore s_stubSettings;
        private static JsonPlayerProgress s_stubProgress;
        private static GameInput s_stubInput;

        private static ISettingsStore s_appliedStore;

        public static IGameFlow GameFlow
        {
            get
            {
                if (s_gameFlow != null) return s_gameFlow;
                if (AppContext.TryGet(out AppContext app) && app.TryGetGameFlow(out GameFlow flow)) return flow;
                return s_stubFlow ??= new SceneGameFlow();
            }
            set => s_gameFlow = value;
        }

        public static IPauseService Pause
        {
            get
            {
                if (s_pause != null) return s_pause;
                if (AppContext.TryGet(out AppContext app) && app.TryGetPauseService(out PauseService pause)) return pause;
                return s_stubPause ??= new TimeScalePauseService();
            }
            set => s_pause = value;
        }

        public static ISettingsStore SettingsStore
        {
            get
            {
                if (s_settingsStore != null) return s_settingsStore;
                if (AppContext.TryGet(out AppContext app) && app.TryGetSettings(out SettingsStore store)) return store;
                return s_stubSettings ??= new JsonSettingsStore();
            }
            set => s_settingsStore = value;
        }

        public static IPlayerProgress Progress
        {
            get
            {
                if (s_progress != null) return s_progress;
                if (AppContext.TryGet(out AppContext app) && app.TryGetProgress(out PlayerProgress progress)) return progress;
                return s_stubProgress ??= new JsonPlayerProgress();
            }
            set => s_progress = value;
        }

        public static IGameInput Input
        {
            get
            {
                if (s_input != null) return s_input;
                if (AppContext.TryGet(out AppContext app) && app.TryGetGameInput(out IGameInput input)) return input;
                return s_stubInput ??= new GameInput();
            }
            set => s_input = value;
        }

        public static IAudioVolumes Audio { get => s_audio ??= new ListenerAudioVolumes(); set => s_audio = value; }
        public static IUiSounds UiSounds { get => s_uiSounds ??= new SilentUiSounds(); set => s_uiSounds = value; }

        /// <summary>
        /// Applies the saved volumes and keys and keeps applying them whenever the store saves new settings
        /// (graphics the store applies itself). Called by UiInputModuleBinding before the first frame of a
        /// scene with UI; once per store.
        /// </summary>
        public static void EnsureSettingsApplied()
        {
            ISettingsStore store = SettingsStore;
            if (ReferenceEquals(store, s_appliedStore))
                return;

            if (s_appliedStore != null)
                s_appliedStore.Changed -= ApplyAudioAndControls;
            s_appliedStore = store;
            store.Changed += ApplyAudioAndControls;
            ApplyAudioAndControls(store.Current);
        }

        /// <summary>The stub flow maps level ids to scenes with the core catalog of the menu or the battle UI.</summary>
        public static void RegisterLevels(LevelCatalog levels)
        {
            if (GameFlow is SceneGameFlow flow && levels != null)
                flow.Levels = levels;
        }

        private static void ApplyAudioAndControls(GameSettings settings)
        {
            SettingsApplyUseCase.ApplyAudio(settings.audio, Audio);
            SettingsApplyUseCase.ApplyControls(settings.controls, Input);
        }

        // Domain reload may be off in the editor: start every Play from scratch.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            s_stubInput?.Controls.Dispose();
            if (s_appliedStore != null)
                s_appliedStore.Changed -= ApplyAudioAndControls;

            s_gameFlow = null;
            s_pause = null;
            s_settingsStore = null;
            s_progress = null;
            s_audio = null;
            s_uiSounds = null;
            s_input = null;
            s_stubFlow = null;
            s_stubPause = null;
            s_stubSettings = null;
            s_stubProgress = null;
            s_stubInput = null;
            s_appliedStore = null;
        }
    }
}

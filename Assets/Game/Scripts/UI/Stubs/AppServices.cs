using UnityEngine;

namespace Game
{
    /// <summary>
    /// TEMPORARY access to the game-wide services until the core team's AppContext exists. Each service
    /// falls back to a simple stub on first use; the core team (or a test) sets the real one instead.
    /// Screens take services from here in one place (their Awake/Open), so the switch to AppContext
    /// touches only this class and those lines.
    /// </summary>
    public static class AppServices
    {
        private static IGameFlow s_gameFlow;
        private static IPauseService s_pause;
        private static ISettingsStore s_settingsStore;
        private static IPlayerProgress s_progress;
        private static IAudioVolumes s_audio;
        private static IUiSounds s_uiSounds;
        private static IGameInput s_input;
        private static bool s_settingsApplied;

        public static IGameFlow GameFlow { get => s_gameFlow ??= new SceneGameFlow(); set => s_gameFlow = value; }
        public static IPauseService Pause { get => s_pause ??= new TimeScalePauseService(); set => s_pause = value; }
        public static IPlayerProgress Progress { get => s_progress ??= new JsonPlayerProgress(); set => s_progress = value; }
        public static IAudioVolumes Audio { get => s_audio ??= new ListenerAudioVolumes(); set => s_audio = value; }
        public static IUiSounds UiSounds { get => s_uiSounds ??= new SilentUiSounds(); set => s_uiSounds = value; }
        public static IGameInput Input { get => s_input ??= new GameInput(); set => s_input = value; }

        public static ISettingsStore SettingsStore
        {
            get => s_settingsStore ??= new JsonSettingsStore();
            set
            {
                s_settingsStore = value;
                s_settingsApplied = false;
            }
        }

        /// <summary>
        /// Applies the saved volumes and keys once and keeps applying them whenever the store saves new
        /// settings (graphics the store applies itself). Called by UiInputModuleBinding before the first frame.
        /// </summary>
        public static void EnsureSettingsApplied()
        {
            if (s_settingsApplied)
                return;

            s_settingsApplied = true;
            SettingsStore.Changed += ApplyAudioAndControls;
            ApplyAudioAndControls(SettingsStore.Current);
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
            if (s_input is GameInput input)
                input.Controls.Dispose();

            s_gameFlow = null;
            s_pause = null;
            s_settingsStore = null;
            s_progress = null;
            s_audio = null;
            s_uiSounds = null;
            s_input = null;
            s_settingsApplied = false;
        }
    }
}

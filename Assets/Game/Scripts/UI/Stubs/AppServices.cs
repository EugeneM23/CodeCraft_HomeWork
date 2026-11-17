using UnityEngine;

namespace Game
{
    /// <summary>
    /// TEMPORARY access to the game-wide services until the core team's AppContext exists. Each service
    /// falls back to a simple stub on first use; the core team (or a test) sets the real one instead.
    /// Screens take services from here in one place (their Awake/Init), so the switch to AppContext
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
        private static GameSettings s_settings;

        public static IGameFlow GameFlow { get => s_gameFlow ??= new SceneGameFlow(); set => s_gameFlow = value; }
        public static IPauseService Pause { get => s_pause ??= new TimeScalePauseService(); set => s_pause = value; }
        public static ISettingsStore SettingsStore { get => s_settingsStore ??= new JsonSettingsStore(); set => s_settingsStore = value; }
        public static IPlayerProgress Progress { get => s_progress ??= new JsonPlayerProgress(); set => s_progress = value; }
        public static IAudioVolumes Audio { get => s_audio ??= new ListenerAudioVolumes(); set => s_audio = value; }
        public static IUiSounds UiSounds { get => s_uiSounds ??= new SilentUiSounds(); set => s_uiSounds = value; }
        public static IGameInput Input { get => s_input ??= new GameInput(); set => s_input = value; }

        /// <summary>The settings in effect: loaded and applied on first use.</summary>
        public static GameSettings Settings
        {
            get
            {
                if (s_settings == null)
                {
                    s_settings = SettingsStore.Load();
                    SettingsApplyUseCase.ApplyAll(s_settings, Audio, Input);
                }
                return s_settings;
            }
        }

        /// <summary>Applies and saves new settings (the settings screen's "Apply").</summary>
        public static void ApplySettings(GameSettings settings)
        {
            s_settings = settings;
            SettingsApplyUseCase.ApplyAll(settings, Audio, Input);
            SettingsStore.Save(settings);
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
            s_settings = null;
        }
    }
}

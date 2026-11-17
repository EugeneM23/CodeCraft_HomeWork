using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The player's settings as saved: plain data for JSON. Applied by SettingsApplyUseCase.
    /// Format proposed to the core team (spec 2026-11-17-game-ui, section 6).
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CURRENT_VERSION = 1;

        public int version = CURRENT_VERSION;
        public Graphics graphics = new();
        public Audio audio = new();
        public Controls controls = new();

        /// <summary>A deep copy: the settings screen edits a copy and applies it on "Apply".</summary>
        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));

        /// <summary>Settings for this machine before the player changes anything: current screen, quality and keys.</summary>
        public static GameSettings CreateDefault()
        {
            GameSettings settings = new();
            Resolution current = Screen.currentResolution;
            settings.graphics.width = current.width;
            settings.graphics.height = current.height;
            settings.graphics.refreshRateHz = (float)current.refreshRateRatio.value;
            settings.graphics.windowMode = FullScreenMode.FullScreenWindow;
            settings.graphics.qualityLevel = QualitySettings.GetQualityLevel();
            return settings;
        }

        [Serializable]
        public sealed class Graphics
        {
            public int width = 1920;
            public int height = 1080;
            public float refreshRateHz = 60f;
            public FullScreenMode windowMode = FullScreenMode.FullScreenWindow;
            public int qualityLevel = 2;
            public bool vSync = true;
            [Tooltip("0 = no limit")]
            public int frameRateLimit;
        }

        [Serializable]
        public sealed class Audio
        {
            public float master = 1f;
            public float music = 0.8f;
            public float effects = 1f;
            public float ui = 1f;
            public float ambient = 1f;

            public float Get(AudioChannel channel) => channel switch
            {
                AudioChannel.Master => master,
                AudioChannel.Music => music,
                AudioChannel.Effects => effects,
                AudioChannel.Ui => ui,
                AudioChannel.Ambient => ambient,
                _ => 1f
            };

            public void Set(AudioChannel channel, float value)
            {
                value = Mathf.Clamp01(value);
                switch (channel)
                {
                    case AudioChannel.Master: master = value; break;
                    case AudioChannel.Music: music = value; break;
                    case AudioChannel.Effects: effects = value; break;
                    case AudioChannel.Ui: ui = value; break;
                    case AudioChannel.Ambient: ambient = value; break;
                }
            }
        }

        [Serializable]
        public sealed class Controls
        {
            public float mouseSensitivity = 1f;
            [Tooltip("InputActionAsset.SaveBindingOverridesAsJson(); empty = default keys")]
            public string bindingOverrides = "";
        }
    }
}

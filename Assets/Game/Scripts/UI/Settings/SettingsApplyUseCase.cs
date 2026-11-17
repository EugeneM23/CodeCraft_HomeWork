using UnityEngine;

namespace Game
{
    /// <summary>Puts saved settings into effect: screen, quality, volumes, keys.</summary>
    public static class SettingsApplyUseCase
    {
        public static void ApplyAll(GameSettings settings, IAudioVolumes volumes, IGameInput input)
        {
            ApplyGraphics(settings.graphics);
            ApplyAudio(settings.audio, volumes);
            ApplyControls(settings.controls, input);
        }

        public static void ApplyGraphics(GameSettings.Graphics graphics)
        {
            int qualityCount = QualitySettings.names.Length;
            if (qualityCount > 0)
                QualitySettings.SetQualityLevel(Mathf.Clamp(graphics.qualityLevel, 0, qualityCount - 1), true);

            // After the quality level: a quality level carries its own vSync count.
            QualitySettings.vSyncCount = graphics.vSync ? 1 : 0;
            Application.targetFrameRate = graphics.frameRateLimit > 0 ? graphics.frameRateLimit : -1;

            // The editor's game view ignores this; a build switches the resolution and the window mode.
            if (!Application.isEditor && graphics.width > 0 && graphics.height > 0)
                Screen.SetResolution(graphics.width, graphics.height, graphics.windowMode, ToRatio(graphics.refreshRateHz));
        }

        public static void ApplyAudio(GameSettings.Audio audio, IAudioVolumes volumes)
        {
            if (volumes == null)
                return;

            volumes.SetVolume(AudioChannel.Master, audio.master);
            volumes.SetVolume(AudioChannel.Music, audio.music);
            volumes.SetVolume(AudioChannel.Effects, audio.effects);
            volumes.SetVolume(AudioChannel.Ui, audio.ui);
            volumes.SetVolume(AudioChannel.Ambient, audio.ambient);
        }

        public static void ApplyControls(GameSettings.Controls controls, IGameInput input)
        {
            if (input == null)
                return;

            input.LoadOverrides(controls.bindingOverrides);
            input.LookSensitivity = controls.mouseSensitivity;
        }

        private static RefreshRate ToRatio(float hz)
        {
            if (hz <= 0f)
                return Screen.currentResolution.refreshRateRatio;

            // Hz with three decimals, so 59.94 survives the trip through the save file.
            return new RefreshRate { numerator = (uint)Mathf.RoundToInt(hz * 1000f), denominator = 1000 };
        }
    }
}

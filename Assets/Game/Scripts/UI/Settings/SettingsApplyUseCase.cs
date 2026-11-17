using UnityEngine;

namespace Game
{
    /// <summary>
    /// Puts settings into effect. Graphics is applied by the settings store on Save (the core team's
    /// contract); volumes and keys are applied by the UI side when the store reports a change.
    /// </summary>
    public static class SettingsApplyUseCase
    {
        public static void ApplyGraphics(GraphicsSettingsData graphics)
        {
            int qualityCount = QualitySettings.names.Length;
            if (graphics.qualityLevel >= 0 && qualityCount > 0)
                QualitySettings.SetQualityLevel(Mathf.Clamp(graphics.qualityLevel, 0, qualityCount - 1), true);

            // After the quality level: a quality level carries its own vSync count.
            QualitySettings.vSyncCount = graphics.vSync ? 1 : 0;
            Application.targetFrameRate = graphics.frameRateLimit > 0 ? graphics.frameRateLimit : -1;

            // The editor's game view ignores this; a build switches the resolution and the window mode.
            if (!Application.isEditor && graphics.width > 0 && graphics.height > 0)
                Screen.SetResolution(graphics.width, graphics.height, graphics.windowMode, ToRatio(graphics.refreshRateHz));
        }

        public static void ApplyAudio(AudioSettingsData audio, IAudioVolumes volumes)
        {
            if (volumes == null)
                return;

            volumes.SetVolume(AudioChannel.Master, audio.master);
            volumes.SetVolume(AudioChannel.Music, audio.music);
            volumes.SetVolume(AudioChannel.Effects, audio.effects);
            volumes.SetVolume(AudioChannel.Ui, audio.ui);
            volumes.SetVolume(AudioChannel.Ambient, audio.ambient);
        }

        public static void ApplyControls(ControlsSettingsData controls, IGameInput input)
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

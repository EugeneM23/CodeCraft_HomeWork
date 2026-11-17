using UnityEngine;

namespace Game
{
    /// <summary>The settings' volume of a channel by AudioChannel, and putting all of them into effect.</summary>
    public static class AudioSettingsUseCase
    {
        public static float Get(AudioSettingsData audio, AudioChannel channel) => channel switch
        {
            AudioChannel.Master => audio.master,
            AudioChannel.Music => audio.music,
            AudioChannel.Effects => audio.effects,
            AudioChannel.Ui => audio.ui,
            AudioChannel.Ambient => audio.ambient,
            _ => 1f
        };

        public static void Set(AudioSettingsData audio, AudioChannel channel, float volume)
        {
            volume = Mathf.Clamp01(volume);
            switch (channel)
            {
                case AudioChannel.Master: audio.master = volume; break;
                case AudioChannel.Music: audio.music = volume; break;
                case AudioChannel.Effects: audio.effects = volume; break;
                case AudioChannel.Ui: audio.ui = volume; break;
                case AudioChannel.Ambient: audio.ambient = volume; break;
            }
        }

        /// <summary>Sets every channel's volume (the settings screen's preview and its revert).</summary>
        public static void ApplyAll(AudioSettingsData audio, IAudioVolumes volumes)
        {
            if (volumes == null)
                return;

            volumes.SetVolume(AudioChannel.Master, audio.master);
            volumes.SetVolume(AudioChannel.Music, audio.music);
            volumes.SetVolume(AudioChannel.Effects, audio.effects);
            volumes.SetVolume(AudioChannel.Ui, audio.ui);
            volumes.SetVolume(AudioChannel.Ambient, audio.ambient);
        }
    }
}

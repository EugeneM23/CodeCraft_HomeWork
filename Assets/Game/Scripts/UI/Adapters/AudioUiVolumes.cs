using Game.AudioEngine.Adapters;

namespace Game.UI.Adapters
{
    /// <summary>
    /// The settings' channel volumes (Game.IAudioVolumes) put into the sound team's mixer through AppAudio. The
    /// value is also kept here, so a volume set before AppAudio exists is not lost for GetVolume.
    /// </summary>
    public sealed class AudioUiVolumes : Game.IAudioVolumes
    {
        private readonly float[] _volumes = { 1f, 1f, 1f, 1f, 1f };

        public float GetVolume(Game.AudioChannel channel) => _volumes[(int)channel];

        public void SetVolume(Game.AudioChannel channel, float volume)
        {
            _volumes[(int)channel] = UnityEngine.Mathf.Clamp01(volume);

            AppAudio audio = AppAudio.Instance;
            if (audio != null && audio.Volumes != null)
                audio.Volumes.SetVolume(ToMixer(channel), _volumes[(int)channel]);
        }

        private static Game.AudioEngine.Adapters.AudioChannel ToMixer(Game.AudioChannel channel) => channel switch
        {
            Game.AudioChannel.Master => Game.AudioEngine.Adapters.AudioChannel.Master,
            Game.AudioChannel.Music => Game.AudioEngine.Adapters.AudioChannel.Music,
            Game.AudioChannel.Effects => Game.AudioEngine.Adapters.AudioChannel.Effects,
            Game.AudioChannel.Ui => Game.AudioEngine.Adapters.AudioChannel.UI,
            _ => Game.AudioEngine.Adapters.AudioChannel.Ambient
        };
    }
}

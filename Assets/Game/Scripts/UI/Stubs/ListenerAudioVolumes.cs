using UnityEngine;

namespace Game
{
    /// <summary>
    /// STUB volumes until the sound team's mixer: Master drives AudioListener.volume, the other
    /// channels are only remembered.
    /// </summary>
    public sealed class ListenerAudioVolumes : IAudioVolumes
    {
        private readonly float[] _volumes = { 1f, 1f, 1f, 1f, 1f };

        public float GetVolume(AudioChannel channel) => _volumes[(int)channel];

        public void SetVolume(AudioChannel channel, float volume)
        {
            _volumes[(int)channel] = Mathf.Clamp01(volume);
            if (channel == AudioChannel.Master)
                AudioListener.volume = _volumes[(int)channel];
        }
    }
}

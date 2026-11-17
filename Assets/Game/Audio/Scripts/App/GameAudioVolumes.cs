using UnityEngine;
using UnityEngine.Audio;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Channel volumes on the game's AudioMixer. Remembers the last value of each channel (1 until set) and
    /// writes it to the mixer's exposed "&lt;Channel&gt;Volume" in decibels. Saving is the settings owner's job.
    /// </summary>
    public sealed class GameAudioVolumes : IAudioVolumes
    {
        private const int CHANNEL_COUNT = 5;

        private readonly AudioMixer _mixer;
        private readonly float[] _volumes = new float[CHANNEL_COUNT];
        private readonly bool[] _missingWarned = new bool[CHANNEL_COUNT];

        public GameAudioVolumes(AudioMixer mixer)
        {
            _mixer = mixer;
            for (int i = 0; i < CHANNEL_COUNT; i++)
                _volumes[i] = 1f;
        }

        public float GetVolume(AudioChannel channel)
        {
            int index = (int)channel;
            return index >= 0 && index < CHANNEL_COUNT ? _volumes[index] : 0f;
        }

        public void SetVolume(AudioChannel channel, float volume)
        {
            int index = (int)channel;
            if (index < 0 || index >= CHANNEL_COUNT)
                return;

            _volumes[index] = Mathf.Clamp01(volume);
            Apply(channel);
        }

        /// <summary>
        /// Writes every remembered value to the mixer. Called from Start: AudioMixer.SetFloat in Awake is ignored.
        /// </summary>
        public void ApplyAll()
        {
            for (int i = 0; i < CHANNEL_COUNT; i++)
                Apply((AudioChannel)i);
        }

        private void Apply(AudioChannel channel)
        {
            if (_mixer == null)
                return;

            int index = (int)channel;
            string parameter = AudioVolumeUseCase.ParameterOf(channel);
            if (!_mixer.SetFloat(parameter, AudioVolumeUseCase.ToDecibels(_volumes[index])) && !_missingWarned[index])
            {
                _missingWarned[index] = true;
                Debug.LogWarning($"[Audio] The mixer has no exposed parameter {parameter}.", _mixer);
            }
        }
    }
}

using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>Linear 0..1 volume to mixer decibels and back, and the exposed mixer parameter of each channel.</summary>
    public static class AudioVolumeUseCase
    {
        /// <summary>What a volume of 0 sets on the mixer: silence.</summary>
        public const float MIN_DECIBELS = -80f;

        public const string MASTER_PARAMETER = "MasterVolume";
        public const string MUSIC_PARAMETER = "MusicVolume";
        public const string EFFECTS_PARAMETER = "EffectsVolume";
        public const string UI_PARAMETER = "UIVolume";
        public const string AMBIENT_PARAMETER = "AmbientVolume";

        // 20 * log10(1e-4) is exactly MIN_DECIBELS: anything quieter is silence.
        private const float MIN_LINEAR = 0.0001f;

        /// <summary>20·log10(v), clamped to [-80, 0] dB; 0 gives -80 dB.</summary>
        public static float ToDecibels(float linear)
        {
            linear = Mathf.Clamp01(linear);
            if (linear <= MIN_LINEAR)
                return MIN_DECIBELS;

            return Mathf.Max(MIN_DECIBELS, 20f * Mathf.Log10(linear));
        }

        /// <summary>The inverse of <see cref="ToDecibels"/>: -80 dB and below gives 0.</summary>
        public static float ToLinear(float decibels)
        {
            if (decibels <= MIN_DECIBELS)
                return 0f;

            return Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
        }

        /// <summary>Exposed parameter name of the channel's mixer group: "&lt;Channel&gt;Volume".</summary>
        public static string ParameterOf(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => MASTER_PARAMETER,
            AudioChannel.Music => MUSIC_PARAMETER,
            AudioChannel.Effects => EFFECTS_PARAMETER,
            AudioChannel.UI => UI_PARAMETER,
            AudioChannel.Ambient => AMBIENT_PARAMETER,
            _ => MASTER_PARAMETER
        };
    }
}

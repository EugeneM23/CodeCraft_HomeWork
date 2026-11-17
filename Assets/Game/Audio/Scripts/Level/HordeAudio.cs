using AudioEngine;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Horde sounds, called by the AI team through <see cref="GameAudio.Horde"/>: the crowd hum follows
    /// <see cref="SetDensity"/>, a new wave sounds once.
    /// </summary>
    public sealed class HordeAudio
    {
        /// <summary>Density at and below which the crowd loop is stopped.</summary>
        private const float SILENT = 0.01f;

        private const float FADE_OUT = 1f;

        private AudioEventHandle _crowd;
        private float _density;

        public float Density => _density;

        /// <summary>How thick the horde is, 0..1: sets horde.density and starts or stops the crowd loop.</summary>
        public void SetDensity(float density)
        {
            _density = Mathf.Clamp01(density);
            GameAudio.SetParameter(AudioKeys.Horde.DENSITY, _density);

            if (_density > SILENT)
            {
                if (!_crowd.IsValid)
                    GameAudio.StartLoop(AudioKeys.Horde.CROWD_LOOP, out _crowd);
            }
            else if (_crowd.IsValid)
            {
                GameAudio.StopLoop(ref _crowd, FADE_OUT);
            }
        }

        public void WaveStarted() => GameAudio.Play(AudioKeys.Horde.WAVE_START);

        /// <summary>The level is gone: forget the loop, its AudioSystem took it along.</summary>
        internal void Reset()
        {
            _crowd = default;
            _density = 0f;
        }
    }
}

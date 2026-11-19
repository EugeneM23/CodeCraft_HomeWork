using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// How tense the battle is, 0..1, for the layered battle music: the share of live horde monsters against the
    /// arena's limit, smoothed over <see cref="SMOOTHING_TIME"/> seconds, plus <see cref="SURGE"/> right after a
    /// wave that fades out over <see cref="SURGE_TIME"/> seconds. The layer weights turn it into layer volumes:
    /// calm always, horde from 0.15 to 0.5, peak from 0.5 to 0.9. Plain values, no allocation, no Unity objects.
    /// </summary>
    public sealed class MusicIntensity
    {
        /// <summary>Time constant of the smoothing, seconds: after it the value has covered ~63% of the way.</summary>
        public const float SMOOTHING_TIME = 2f;

        /// <summary>What a wave adds to the value at once.</summary>
        public const float SURGE = 0.3f;

        /// <summary>Seconds the surge takes to fade back to nothing.</summary>
        public const float SURGE_TIME = 3f;

        public const float HORDE_FROM = 0.15f;
        public const float HORDE_TO = 0.5f;
        public const float PEAK_FROM = 0.5f;
        public const float PEAK_TO = 0.9f;

        private float _target;
        private float _smoothed;
        private float _surgeLeft;
        private bool _forced;

        /// <summary>The share of live monsters, 0..1, as last set.</summary>
        public float Target => _target;

        /// <summary>The smoothed share without the surge.</summary>
        public float Smoothed => _smoothed;

        /// <summary>What the last wave still adds, from <see cref="SURGE"/> down to 0.</summary>
        public float SurgeLevel => SURGE * _surgeLeft / SURGE_TIME;

        /// <summary>The intensity, 0..1: the smoothed share plus the surge.</summary>
        public float Value => Mathf.Clamp01(_smoothed + SurgeLevel);

        /// <summary>While true the value is smoothed toward 1 whatever the target: an elite without its own music.</summary>
        public bool Forced
        {
            get => _forced;
            set => _forced = value;
        }

        /// <summary>All live horde monsters against the arena's limit, 0..1.</summary>
        public void SetTarget(float alive01) => _target = float.IsNaN(alive01) ? 0f : Mathf.Clamp01(alive01);

        /// <summary>A wave was released: the value jumps by <see cref="SURGE"/> and the jump fades out.</summary>
        public void Surge() => _surgeLeft = SURGE_TIME;

        /// <summary>
        /// Moves the value toward the target. Exponential, so one long step lands where many short ones do and
        /// the value never passes the target.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float target = _forced ? 1f : _target;
            _smoothed += (target - _smoothed) * (1f - Mathf.Exp(-deltaTime / SMOOTHING_TIME));
            _surgeLeft = Mathf.Max(0f, _surgeLeft - deltaTime);
        }

        /// <summary>A new battle: calm, no surge, not forced.</summary>
        public void Reset()
        {
            _target = 0f;
            _smoothed = 0f;
            _surgeLeft = 0f;
            _forced = false;
        }

        /// <summary>The calm layer plays through the whole battle.</summary>
        public static float CalmWeight(float intensity) => 1f;

        /// <summary>0 at <see cref="HORDE_FROM"/> and below, 1 at <see cref="HORDE_TO"/> and above.</summary>
        public static float HordeWeight(float intensity) => Mathf.Clamp01(Mathf.InverseLerp(HORDE_FROM, HORDE_TO, intensity));

        /// <summary>0 at <see cref="PEAK_FROM"/> and below, 1 at <see cref="PEAK_TO"/> and above.</summary>
        public static float PeakWeight(float intensity) => Mathf.Clamp01(Mathf.InverseLerp(PEAK_FROM, PEAK_TO, intensity));
    }
}

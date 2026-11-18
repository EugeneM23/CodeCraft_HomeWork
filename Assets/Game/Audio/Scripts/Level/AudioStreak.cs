using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// A run of events close together in time: each one within <see cref="Window"/> seconds of the last steps the
    /// streak up, up to <see cref="MaxSteps"/>; a longer gap starts it over. Drives the pitch of experience pickups
    /// and of a crush series. Plain values, no allocation.
    /// </summary>
    public sealed class AudioStreak
    {
        private readonly float _window;
        private readonly int _maxSteps;

        private float _lastTime = float.NegativeInfinity;
        private int _steps;

        public AudioStreak(float window, int maxSteps)
        {
            _window = Mathf.Max(0f, window);
            _maxSteps = Mathf.Max(1, maxSteps);
        }

        public float Window => _window;

        public int MaxSteps => _maxSteps;

        /// <summary>Steps above the first event of the streak: 0 for a lone event, then 1, 2… up to MaxSteps.</summary>
        public int Steps => _steps;

        /// <summary><see cref="Steps"/> as 0..1 of <see cref="MaxSteps"/>, for a bank parameter.</summary>
        public float Level => (float)_steps / _maxSteps;

        /// <summary>Counts an event at <paramref name="time"/> (seconds) and returns the streak's steps after it.</summary>
        public int Register(float time)
        {
            _steps = time - _lastTime <= _window ? Mathf.Min(_steps + 1, _maxSteps) : 0;
            _lastTime = time;
            return _steps;
        }

        public void Reset()
        {
            _lastTime = float.NegativeInfinity;
            _steps = 0;
        }
    }
}

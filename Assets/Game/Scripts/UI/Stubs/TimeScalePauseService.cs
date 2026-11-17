using UnityEngine;

namespace Game
{
    /// <summary>STUB pause until the core team's GameLoop: stops scaled time.</summary>
    public sealed class TimeScalePauseService : IPauseService
    {
        private float _scaleBefore = 1f;

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            _scaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            Time.timeScale = _scaleBefore;
        }
    }
}

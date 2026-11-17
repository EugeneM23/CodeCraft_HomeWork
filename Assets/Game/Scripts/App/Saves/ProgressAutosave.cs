using System;

namespace Game
{
    /// <summary>
    /// Writes the progress a delay of real time after its last change (changes in a burst give one write) and at once
    /// when a scene transition starts. Changes that never stop (credits per kill) still get written: at most
    /// MAX_WAIT_FACTOR delays after the first unsaved change. Ticked by AppTicker on unscaled time: the game's pause
    /// does not hold it.
    /// </summary>
    public sealed class ProgressAutosave : IAppTickable, IDisposable
    {
        private const float MAX_WAIT_FACTOR = 10f;

        private readonly PlayerProgress _progress;
        private readonly GameFlow _flow;
        private readonly float _delaySeconds;

        private float _timeLeft;
        private float _maxLeft;
        private bool _pending;

        /// <param name="flow">May be null: then only the delay saves.</param>
        public ProgressAutosave(PlayerProgress progress, GameFlow flow, float delaySeconds)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _flow = flow;
            _delaySeconds = delaySeconds > 0f ? delaySeconds : 0f;

            _progress.Changed += OnProgressChanged;
            if (_flow != null)
                _flow.TransitionStarted += OnTransitionStarted;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!_pending)
                return;

            _timeLeft -= unscaledDeltaTime;
            _maxLeft -= unscaledDeltaTime;
            if (_timeLeft <= 0f || _maxLeft <= 0f)
                SaveNow();
        }

        /// <summary>Writes a dirty progress now and drops the waiting delay.</summary>
        public void SaveNow()
        {
            _pending = false;
            if (_progress.IsDirty)
                _progress.Save();
        }

        public void Dispose()
        {
            _progress.Changed -= OnProgressChanged;
            if (_flow != null)
                _flow.TransitionStarted -= OnTransitionStarted;

            _pending = false;
        }

        private void OnProgressChanged()
        {
            // A load raises Changed too, but leaves the progress clean
            if (!_progress.IsDirty)
                return;

            if (!_pending)
                _maxLeft = _delaySeconds * MAX_WAIT_FACTOR;

            _pending = true;
            _timeLeft = _delaySeconds;
        }

        private void OnTransitionStarted(GameState from, GameState to) => SaveNow();
    }
}

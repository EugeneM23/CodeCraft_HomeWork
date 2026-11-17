using System;
using Ticking;

namespace Game
{
    /// <summary>
    /// The player's pause on top of the game loop: the service itself is the one pause holder, so other holders
    /// (loading, focus loss) keep theirs. IsPaused and PauseChanged are the loop's: they tell whether the game stands
    /// for anyone.
    /// </summary>
    public sealed class PauseService : IPauseService, IDisposable
    {
        private readonly IGameLoop _loop;

        public PauseService(IGameLoop loop)
        {
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
            _loop.PauseChanged += OnLoopPauseChanged;
        }

        public event Action<bool> PauseChanged;

        public bool IsPaused => _loop.IsPaused;

        /// <summary>This service holds the pause (the player paused), whatever the other holders do.</summary>
        public bool IsHeld => _loop.IsPausedBy(this);

        public void Pause() => _loop.Pause(this);

        public void Resume() => _loop.Resume(this);

        public void Dispose()
        {
            _loop.PauseChanged -= OnLoopPauseChanged;
            _loop.Resume(this);
        }

        private void OnLoopPauseChanged(bool paused) => PauseChanged?.Invoke(paused);
    }
}

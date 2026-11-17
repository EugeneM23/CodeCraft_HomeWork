using System;
using Ticking;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// Paused while the core's game loop is paused (by the player, a loading screen, focus loss). Listens to the
    /// loop instead of guessing from Time.timeScale; the loop leaves AudioListener alone, so the level's sounds are
    /// frozen by GameAudio and the menu sounds and music keep playing.
    /// </summary>
    public sealed class GameLoopPauseSource : IAudioPauseSource, IPauseListener, IDisposable
    {
        private readonly IGameLoop _loop;

        public GameLoopPauseSource(IGameLoop loop)
        {
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
            IsPaused = loop.IsPaused;
            _loop.Add(this); // a listener added while paused gets OnPause at once
        }

        public bool IsPaused { get; private set; }

        public void OnPause() => IsPaused = true;

        public void OnResume() => IsPaused = false;

        public void Dispose() => _loop.Remove(this);
    }
}

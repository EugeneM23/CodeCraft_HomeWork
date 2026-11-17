using System;

namespace Game
{
    /// <summary>
    /// The player's pause (pause screen, results). One holder on top of the game loop's pause: other holders
    /// (loading, focus loss) keep their own. Get it with AppContext.Instance.GetPauseService().
    /// </summary>
    public interface IPauseService
    {
        /// <summary>True when the game stands for any holder, not only this service.</summary>
        bool IsPaused { get; }

        event Action<bool> PauseChanged;

        void Pause();

        void Resume();
    }
}

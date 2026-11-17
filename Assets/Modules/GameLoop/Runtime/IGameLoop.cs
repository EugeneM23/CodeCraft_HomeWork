using System;
using System.Collections.Generic;

namespace Ticking
{
    /// <summary>
    /// The game loop as a service: game code joins it instead of using Unity's Update, FixedUpdate and LateUpdate, and
    /// a pause is the loop not ticking. Every source of a pause (a menu, a scene load, focus loss) holds its own; the game
    /// runs when nobody holds one. Time.timeScale is not involved.
    /// </summary>
    public interface IGameLoop
    {
        /// <summary>True — the game has just stopped, false — it went on.</summary>
        event Action<bool> PauseChanged;

        bool IsPaused { get; }

        /// <summary>Game seconds: the sum of Update deltas, standing still while paused.</summary>
        float Time { get; }

        /// <summary>Who holds the pause now, in the order they took it.</summary>
        IReadOnlyList<object> PauseSources { get; }

        /// <summary>
        /// Starts ticking the listener by every listener interface it implements (IUpdateListener,
        /// IFixedUpdateListener, ILateUpdateListener, IPauseListener). The same listener again does nothing; an object
        /// with none of the interfaces throws.
        /// </summary>
        void Add(object listener);

        /// <summary>Stops ticking the listener at once, even in the middle of a pass. Unknown listeners are ignored.</summary>
        void Remove(object listener);

        bool Contains(object listener);

        /// <summary>The source holds a pause. Holding it again does nothing.</summary>
        void Pause(object source);

        /// <summary>The source lets its pause go; the game runs when no source holds one.</summary>
        void Resume(object source);

        bool IsPausedBy(object source);
    }
}

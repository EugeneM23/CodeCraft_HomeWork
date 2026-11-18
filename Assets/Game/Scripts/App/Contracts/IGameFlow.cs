using System;

namespace Game
{
    /// <summary>
    /// The course of the game as the menus see it. Every request runs a transition behind the loading screen and
    /// returns false when it is not taken (another transition runs, or the request makes no sense where the game is).
    /// Implemented by GameFlow; get it with AppContext.Instance.GetGameFlow().
    /// </summary>
    public interface IGameFlow
    {
        /// <summary>The state the game has just entered, at the end of a transition.</summary>
        event Action<GameState> StateChanged;

        GameState State { get; }

        /// <summary>A transition runs: menus do not take input.</summary>
        bool IsBusy { get; }

        /// <summary>Id of the level being played, null outside a level.</summary>
        string CurrentLevelId { get; }

        /// <summary>The screen the main menu should open on after ReturnToMenu(screen); null — its root screen.</summary>
        string RequestedMenuScreen { get; }

        /// <summary>
        /// The chapter being played (GDD §3, §10); none outside a level or in a level opened without a chapter.
        /// Default: none, for flows without chapters (UI stubs).
        /// </summary>
        ChapterId CurrentChapter => default;

        bool StartLevel(string levelId);

        /// <summary>
        /// Plays the chapter: its level from the game catalog, behind the loading screen. False when it is not taken
        /// (unknown chapter, no level, busy). Default: refused, for flows without chapters (UI stubs).
        /// </summary>
        bool StartChapter(ChapterId chapter) => false;

        bool RestartLevel();

        /// <summary>Saves the progress and goes to the main menu, which opens on the given screen (e.g. "Hangar").</summary>
        bool ReturnToMenu(string screen = null);

        void Quit();
    }
}

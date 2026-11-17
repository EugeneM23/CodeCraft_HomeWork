namespace Game
{
    /// <summary>
    /// Moves the game between the menu and the levels. The UI only asks; the core team's flow
    /// (Boot → menu → level) answers. Until it exists, SceneGameFlow loads scenes by name.
    /// </summary>
    public interface IGameFlow
    {
        /// <summary>The level being played, or the last one started. Null in the menu at launch.</summary>
        LevelConfig CurrentLevel { get; }

        /// <summary>The screen the menu opens on after a level, set by ReturnToMenu.</summary>
        MenuEntry PendingMenuEntry { get; }

        void StartLevel(LevelConfig level);
        void RestartLevel();
        void ReturnToMenu(MenuEntry entry);
        void Quit();
    }
}

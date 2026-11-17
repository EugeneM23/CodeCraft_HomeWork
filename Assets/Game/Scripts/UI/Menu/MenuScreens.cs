namespace Game
{
    /// <summary>
    /// Names of the menu screens the game can ask the main menu to open on
    /// (IGameFlow.ReturnToMenu(screen) → IGameFlow.RequestedMenuScreen).
    /// </summary>
    public static class MenuScreens
    {
        public const string HANGAR = "Hangar";
        public const string LEVEL_SELECT = "LevelSelect";
    }
}

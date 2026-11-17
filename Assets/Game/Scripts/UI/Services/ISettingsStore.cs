namespace Game
{
    /// <summary>Loads and saves the player's settings. Stub: a JSON file; later the core team's SaveSystem.</summary>
    public interface ISettingsStore
    {
        /// <summary>The saved settings, or the defaults when nothing is saved or the file is broken.</summary>
        GameSettings Load();

        void Save(GameSettings settings);
    }
}

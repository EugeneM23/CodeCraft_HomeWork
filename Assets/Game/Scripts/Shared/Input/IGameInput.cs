namespace Game
{
    /// <summary>
    /// The one GameControls of the game, with the player's key overrides loaded. Screens switch the
    /// Tank map off while a menu is open. Later lives in AppContext (core team).
    /// </summary>
    public interface IGameInput
    {
        GameControls Controls { get; }

        /// <summary>Multiplier for the Tank/Look mouse delta, from the settings.</summary>
        float LookSensitivity { get; set; }

        /// <summary>Replaces all key overrides with these (SaveBindingOverridesAsJson); empty resets to defaults.</summary>
        void LoadOverrides(string json);

        /// <summary>The current key overrides as JSON, for GameSettings.</summary>
        string SaveOverrides();

        /// <summary>Tank driving and firing on or off (off while a menu covers the battle).</summary>
        void SetTankEnabled(bool enabled);
    }
}

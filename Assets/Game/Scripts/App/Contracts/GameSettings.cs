using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Settings data, agreed with the menu team (spec game-ui, section 6). Plain [Serializable] data with public
    /// fields; saved as a versioned section of the settings file. Add fields with defaults; renames need a migration.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public GraphicsSettingsData graphics = new();
        public AudioSettingsData audio = new();
        public ControlsSettingsData controls = new();

        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));
    }
}

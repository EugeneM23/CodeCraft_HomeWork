using System;

namespace Game
{
    /// <summary>
    /// Settings on disk. Current is loaded at the start of the game and is the object everyone reads; screens edit a
    /// copy and pass it to Save, which writes it, makes it Current, applies graphics and raises Changed.
    /// </summary>
    public interface ISettingsStore
    {
        GameSettings Current { get; }

        event Action<GameSettings> Changed;

        /// <summary>A fresh copy of the settings on disk (defaults when there is no file).</summary>
        GameSettings Load();

        void Save(GameSettings settings);
    }
}

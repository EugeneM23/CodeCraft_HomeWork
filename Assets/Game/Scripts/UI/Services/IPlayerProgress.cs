using System;

namespace Game
{
    /// <summary>
    /// The player's progress: credits, bought and installed tank modules, finished levels.
    /// Screens change Data through ShopUseCase and the like, then call Save, which raises Changed.
    /// Stub: a JSON file; later the core team's SaveSystem.
    /// </summary>
    public interface IPlayerProgress
    {
        event Action Changed;

        PlayerProgressData Data { get; }

        /// <summary>Stores Data and raises Changed.</summary>
        void Save();
    }
}

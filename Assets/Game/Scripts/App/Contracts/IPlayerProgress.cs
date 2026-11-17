using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// The player's progress: credits, bought tank modules and their levels, the module installed in each slot,
    /// unlocked and completed levels. Strings are ids from the game's configs. Saved by the core on its own
    /// (autosave); Changed fires after every change.
    /// </summary>
    public interface IPlayerProgress
    {
        event Action Changed;

        int Credits { get; }

        void AddCredits(int amount);

        /// <summary>False and nothing spent when there are not enough credits.</summary>
        bool TrySpendCredits(int amount);

        bool OwnsModule(string moduleId);

        /// <summary>0 — not bought; bought modules start at level 1.</summary>
        int GetModuleLevel(string moduleId);

        void SetModuleLevel(string moduleId, int level);

        /// <summary>The module installed in the slot, null when none.</summary>
        string GetInstalledModule(string slot);

        void InstallModule(string slot, string moduleId);

        IReadOnlyCollection<string> UnlockedLevels { get; }

        bool IsLevelUnlocked(string levelId);

        void UnlockLevel(string levelId);

        bool IsLevelCompleted(string levelId);

        void MarkLevelCompleted(string levelId);
    }
}

using System.Collections.Generic;

namespace Game
{
    /// <summary>Which levels of the core's catalog the player may start, and the level after a given one.</summary>
    public static class LevelUnlockUseCase
    {
        /// <summary>Open from the start, unlocked in the progress, or its required level completed.</summary>
        public static bool IsUnlocked(LevelConfig level, IPlayerProgress progress) =>
            level != null && (level.RequiredLevel == null || progress.IsLevelUnlocked(level.Id) ||
                              progress.IsLevelCompleted(level.RequiredLevel.Id));

        /// <summary>The level after this one in the catalog, or null.</summary>
        public static LevelConfig Next(LevelCatalog catalog, LevelConfig level)
        {
            if (catalog == null || level == null)
                return null;

            IReadOnlyList<LevelConfig> levels = catalog.Levels;
            for (int i = 0; i < levels.Count - 1; i++)
                if (levels[i] == level)
                    return levels[i + 1];
            return null;
        }
    }
}

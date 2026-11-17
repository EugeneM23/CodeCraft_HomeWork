using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>All levels in the order of the level select.</summary>
    [CreateAssetMenu(menuName = "Game/UI/Level Catalog", fileName = "LevelCatalogConfig")]
    public sealed class LevelCatalogConfig : ScriptableObject
    {
        public List<LevelConfig> levels = new();

        public LevelConfig Find(string id)
        {
            for (int i = 0; i < levels.Count; i++)
                if (levels[i] != null && levels[i].id == id)
                    return levels[i];
            return null;
        }

        /// <summary>The level after this one in the list, or null.</summary>
        public LevelConfig Next(LevelConfig level)
        {
            int index = levels.IndexOf(level);
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
        }

        public static bool IsUnlocked(LevelConfig level, IPlayerProgress progress) =>
            level.requiredLevel == null || progress.IsLevelUnlocked(level.id) || progress.IsLevelCompleted(level.requiredLevel.id);
    }
}

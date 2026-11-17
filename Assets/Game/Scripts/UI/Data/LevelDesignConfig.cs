using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Design fields of the core's LevelConfig (difficulty, kill target, reward) until they move there after the merge.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/UI/Level Design", fileName = "LevelDesignConfig")]
    public sealed class LevelDesignConfig : ScriptableObject
    {
        public const int DEFAULT_DIFFICULTY = 1;
        public const int DEFAULT_KILL_TARGET = 10;
        public const int DEFAULT_REWARD = 0;

        public List<LevelDesignData> levels = new();

        /// <summary>The entry of the level; null when there is none.</summary>
        public LevelDesignData Find(string levelId)
        {
            if (string.IsNullOrEmpty(levelId))
                return null;

            for (int i = 0; i < levels.Count; i++)
                if (levels[i] != null && levels[i].levelId == levelId)
                    return levels[i];
            return null;
        }

        /// <summary>The name the menus show: the design entry's, else the core level's.</summary>
        public string DisplayName(LevelConfig level)
        {
            if (level == null)
                return string.Empty;
            LevelDesignData data = Find(level.Id);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : level.DisplayName;
        }

        public string Description(LevelConfig level)
        {
            if (level == null)
                return string.Empty;
            LevelDesignData data = Find(level.Id);
            return data != null && !string.IsNullOrEmpty(data.description) ? data.description : level.Description;
        }

        public int Difficulty(LevelConfig level)
        {
            LevelDesignData data = level != null ? Find(level.Id) : null;
            return data != null ? data.difficulty : DEFAULT_DIFFICULTY;
        }

        public int KillTarget(LevelConfig level)
        {
            LevelDesignData data = level != null ? Find(level.Id) : null;
            return data != null ? data.killTarget : DEFAULT_KILL_TARGET;
        }

        public int Reward(LevelConfig level)
        {
            LevelDesignData data = level != null ? Find(level.Id) : null;
            return data != null ? data.reward : DEFAULT_REWARD;
        }
    }
}

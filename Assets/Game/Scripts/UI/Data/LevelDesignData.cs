using System;
using UnityEngine;

namespace Game
{
    /// <summary>Design fields of one level, keyed by the core's LevelConfig.Id.</summary>
    [Serializable]
    public sealed class LevelDesignData
    {
        [Tooltip("Id of the core's LevelConfig these fields belong to")]
        public string levelId;
        [Tooltip("Shown in the level select as N/3")]
        [Range(1, 3)] public int difficulty = 1;
        [Tooltip("Regax to kill to win; 0 = no kill goal")]
        [Min(0)] public int killTarget = 10;
        [Tooltip("Credits for a win")]
        [Min(0)] public int reward;
    }
}

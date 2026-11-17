using UnityEngine;

namespace Game
{
    /// <summary>A level in the level select. Placeholder fields until the game design document.</summary>
    [CreateAssetMenu(menuName = "Game/UI/Level", fileName = "LevelConfig")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Tooltip("Stable key in the save file")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Scene loaded to play the level (must be in Build Settings)")]
        public string sceneName;
        [Range(1, 3)] public int difficulty = 1;
        [Tooltip("Regax to kill to win; 0 = no kill goal")]
        public int killTarget = 10;
        [Tooltip("Credits for a win")]
        public int reward = 300;
        [Tooltip("Level that must be won first; empty = open from the start")]
        public LevelConfig requiredLevel;
    }
}

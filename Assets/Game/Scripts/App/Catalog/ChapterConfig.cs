using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>A chapter (GDD §3, §7, §10): its arenas in order, the level it is played on and its multipliers.</summary>
    [CreateAssetMenu(menuName = "Game/Run/Chapter", fileName = "ChapterConfig")]
    public sealed class ChapterConfig : ScriptableObject
    {
        [SerializeField] private ChapterId _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [Tooltip("The scenes the chapter is played on")]
        [SerializeField] private LevelConfig _level;
        [Tooltip("The arenas in order; the last one ends the run with a victory")]
        [SerializeField] private List<ArenaConfig> _arenas = new();
        [Tooltip("Enemy health and damage: x1 / x1.6 / x2.4 (GDD §5)")]
        [SerializeField, Min(0.1f)] private float _enemyMultiplier = 1f;
        [Tooltip("Marks and samples: x1 / x1.5 / x2 (GDD §7)")]
        [SerializeField, Min(0f)] private float _rewardMultiplier = 1f;
        [Tooltip("Reward for the first completion (GDD §7): 1000 / 2000 / 3000 marks")]
        [SerializeField, Min(0)] private int _firstCompletionMarks = 1000;
        [Tooltip("Reward for the first completion: 10 / 15 / 20 samples")]
        [SerializeField, Min(0)] private int _firstCompletionSamples = 10;
        [Tooltip("Opens when this chapter is completed; none: open from the start")]
        [SerializeField] private ChapterConfig _requiredChapter;
        [Tooltip("The tank the chapter select recommends")]
        [SerializeField] private TankId _recommendedTank;

        public ChapterId Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public LevelConfig Level => _level;
        public IReadOnlyList<ArenaConfig> Arenas => _arenas;
        public int ArenaCount => _arenas.Count;
        public float EnemyMultiplier => _enemyMultiplier;
        public float RewardMultiplier => _rewardMultiplier;
        public int FirstCompletionMarks => _firstCompletionMarks;
        public int FirstCompletionSamples => _firstCompletionSamples;
        public ChapterConfig RequiredChapter => _requiredChapter;
        public TankId RecommendedTank => _recommendedTank;

        /// <summary>The arena by its 1-based number; null outside the chapter.</summary>
        public ArenaConfig ArenaAt(int number) =>
            number >= 1 && number <= _arenas.Count ? _arenas[number - 1] : null;

        /// <summary>Setup for tests and the catalog builder.</summary>
        public void Configure(ChapterId id, LevelConfig level, float enemyMultiplier, float rewardMultiplier,
            int firstMarks, int firstSamples, ChapterConfig requiredChapter, params ArenaConfig[] arenas)
        {
            _id = id;
            _level = level;
            _enemyMultiplier = enemyMultiplier;
            _rewardMultiplier = rewardMultiplier;
            _firstCompletionMarks = firstMarks;
            _firstCompletionSamples = firstSamples;
            _requiredChapter = requiredChapter;
            _arenas = new List<ArenaConfig>(arenas);
        }
    }
}

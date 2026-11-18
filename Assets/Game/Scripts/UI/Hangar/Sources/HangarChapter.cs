namespace Game
{
    /// <summary>A chapter as the chapter select shows it.</summary>
    public readonly struct HangarChapter
    {
        public readonly string Id;
        public readonly bool Unlocked;
        public readonly bool Completed;
        /// <summary>Farthest arena reached; 0 when never played.</summary>
        public readonly int BestArena;
        public readonly int ArenaCount;
        /// <summary>Best time of a won run, seconds; 0 when never won.</summary>
        public readonly float BestTime;
        public readonly int FirstRewardMarks;
        public readonly int FirstRewardSamples;
        public readonly string RecommendedTankId;

        public HangarChapter(string id, bool unlocked, bool completed, int bestArena, int arenaCount, float bestTime,
            int firstRewardMarks, int firstRewardSamples, string recommendedTankId)
        {
            Id = id;
            Unlocked = unlocked;
            Completed = completed;
            BestArena = bestArena;
            ArenaCount = arenaCount;
            BestTime = bestTime;
            FirstRewardMarks = firstRewardMarks;
            FirstRewardSamples = firstRewardSamples;
            RecommendedTankId = recommendedTankId;
        }
    }
}

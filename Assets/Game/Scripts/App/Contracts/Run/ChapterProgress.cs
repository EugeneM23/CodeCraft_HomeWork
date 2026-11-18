namespace Game
{
    /// <summary>A chapter in the save (GDD §14): open, completed, the furthest arena and the best time of a completion.</summary>
    public readonly struct ChapterProgress
    {
        public readonly bool Unlocked;
        public readonly bool Completed;

        /// <summary>The furthest arena reached, 0 — never played.</summary>
        public readonly int BestArena;

        /// <summary>The fastest completion, seconds; 0 — never completed.</summary>
        public readonly float BestTimeSec;

        public ChapterProgress(bool unlocked, bool completed, int bestArena, float bestTimeSec)
        {
            Unlocked = unlocked;
            Completed = completed;
            BestArena = bestArena;
            BestTimeSec = bestTimeSec;
        }
    }
}

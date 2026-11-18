namespace Game
{
    /// <summary>How a run ended, for the results screen. Marks and samples are already credited to the progress.</summary>
    public sealed class RunSummary
    {
        public bool Victory;
        /// <summary>The player left to the hangar from the pause (counts as a defeat).</summary>
        public bool Abandoned;
        public string ChapterId;
        public int ArenaReached;
        public int ArenaCount;
        public float Time;
        public int Kills;
        public int Crushes;
        public int ElitesKilled;
        public int Marks;
        public int Samples;
        /// <summary>The farthest arena of the chapter so far was beaten by this run.</summary>
        public bool NewRecord;
    }
}

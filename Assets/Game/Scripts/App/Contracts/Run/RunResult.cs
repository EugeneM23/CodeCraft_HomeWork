using System;

namespace Game
{
    /// <summary>The summary of an ended run, for the results screen and the save. Marks and samples are kept whatever the outcome.</summary>
    [Serializable]
    public struct RunResult
    {
        public RunOutcome outcome;
        public ChapterId chapter;

        /// <summary>The furthest arena reached, 1-based (the one the run ended on).</summary>
        public int arenaReached;

        /// <summary>Seconds of play, pauses not counted.</summary>
        public float timeSec;

        public int kills;
        public int crushes;
        public int elitesKilled;
        public int bossesKilled;

        /// <summary>Marks and samples of the run, chapter multiplier and first-completion reward included.</summary>
        public int marks;
        public int samples;

        /// <summary>The first-completion reward was part of it (the chapter was not completed before).</summary>
        public bool firstCompletion;

        public int levelReached;

        /// <summary>The chapter's best arena before this run (0 — never played).</summary>
        public int previousBestArena;

        /// <summary>This run went further than any before it in the chapter.</summary>
        public bool newRecord;
    }
}

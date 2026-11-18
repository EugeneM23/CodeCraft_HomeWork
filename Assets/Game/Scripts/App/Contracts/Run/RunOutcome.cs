namespace Game
{
    /// <summary>How a run ended (GDD §3 «Победа и поражение»).</summary>
    public enum RunOutcome
    {
        /// <summary>The chapter's last arena is cleared (the boss, in milestone 1 the elite of arena 5).</summary>
        Victory,

        /// <summary>The tank's health reached 0.</summary>
        Defeat,

        /// <summary>Left for the hangar from the pause: counts as a defeat, the loot is kept.</summary>
        Abandoned
    }
}

namespace Game
{
    /// <summary>
    /// Where the run is (GDD §3). An arena goes Preparing → Lull → Fight → Cleared → GateOpen → Leaving, then the next
    /// arena's Preparing; after the last arena or the tank's death — Ended.
    /// </summary>
    public enum RunState
    {
        /// <summary>No run: the menu, the hangar, or a level not started yet.</summary>
        None,

        /// <summary>The shutter is closed; the arena is being built (arena team) and placed.</summary>
        Preparing,

        /// <summary>About 2 s of calm after the tank drives in (the shutter has opened). No waves yet.</summary>
        Lull,

        /// <summary>Waves come out of the burrows (AI director).</summary>
        Fight,

        /// <summary>The last enemy is dead; the gate is about to open (arena team).</summary>
        Cleared,

        /// <summary>The gate is open (or rammed through); the tank may leave.</summary>
        GateOpen,

        /// <summary>The tank is through the gate: experience on the ground flies into it, the shutter closes (~1 s).</summary>
        Leaving,

        /// <summary>Over: won, lost or left from the pause. <see cref="IRunService.Result"/> holds the summary.</summary>
        Ended
    }
}

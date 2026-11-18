namespace Game
{
    /// <summary>
    /// The arena team's side of the run, as the core sees it: build arena n in place of the previous one while the
    /// shutter is closed. Called by <see cref="RunService"/> before ArenaStarted(n) with the seed Seed + n.
    /// </summary>
    public interface IArenaBuilder
    {
        /// <summary>
        /// True — the arena is built and the tank placed (a synchronous build): the shutter opens on the next tick.
        /// False — it takes frames: the builder calls <see cref="IRunService.MarkArenaReady"/> when done (the run waits
        /// up to RunTimings.readyTimeoutSec).
        /// </summary>
        bool Build(ArenaId arena, int seed);
    }
}

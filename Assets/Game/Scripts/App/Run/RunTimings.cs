using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The run's timers (GDD §3), in game seconds: they stand on pause. An arena: Build behind the closed shutter →
    /// MarkArenaReady (or <see cref="readyTimeoutSec"/>) → the shutter opens (<see cref="shutterSec"/>) → the lull
    /// (<see cref="lullSec"/>) → the fight. Leaving: ArenaLeft → <see cref="orbPullSec"/> for the orbs → the shutter
    /// closes (<see cref="shutterSec"/>) → the next arena. The last arena cleared → <see cref="victoryDelaySec"/> → Victory.
    /// </summary>
    [Serializable]
    public struct RunTimings
    {
        [Tooltip("The longest wait for MarkArenaReady after Build; then the shutter opens anyway")]
        public float readyTimeoutSec;
        [Tooltip("The shutter opening or closing, each way")]
        public float shutterSec;
        [Tooltip("Calm after the shutter has opened, before FightStarted")]
        public float lullSec;
        [Tooltip("From ArenaLeft to the shutter starting to close: the orbs fly into the tank")]
        public float orbPullSec;
        [Tooltip("From the last arena cleared to Victory: the kill is seen")]
        public float victoryDelaySec;
        [Tooltip("A crush within this time of the previous one continues the streak; this long without one ends it")]
        public float crushStreakSec;

        /// <summary>The GDD's numbers: 5 / 0.5 / 2 / 0.5 / 1.5 / 2 s.</summary>
        public static RunTimings Default => new()
        {
            readyTimeoutSec = 5f,
            shutterSec = 0.5f,
            lullSec = 2f,
            orbPullSec = 0.5f,
            victoryDelaySec = 1.5f,
            crushStreakSec = 2f
        };
    }
}

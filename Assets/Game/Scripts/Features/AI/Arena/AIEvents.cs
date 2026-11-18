using System;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The arena director's own signals: a wave began, the arena is cleared. With a run the director also tells the
    /// core (IRunService.MarkArenaCleared); without one (sandbox, bench, tests) these are the only signals. Enemy
    /// deaths go through the core's IEnemyDeaths. Main thread; cleared when Play starts.
    /// </summary>
    public static class AIEvents
    {
        /// <summary>A wave of the arena began: its index from 0.</summary>
        public static event Action<int> WaveStarted;

        /// <summary>Every wave is out and nothing is alive.</summary>
        public static event Action ArenaCleared;

        public static void RaiseWaveStarted(int wave) => WaveStarted?.Invoke(wave);

        public static void RaiseArenaCleared() => ArenaCleared?.Invoke();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            WaveStarted = null;
            ArenaCleared = null;
        }
    }
}

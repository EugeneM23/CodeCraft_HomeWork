using System;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// What the AI tells the rest of the game: an enemy died, a wave began, the arena is cleared. A stand-in for the
    /// core's run contracts (milestone 1, App/Contracts: the enemy-killed event and IRunService.ArenaCleared): the
    /// AI raises these until those are published, then raises the core's and this file goes. Main thread only;
    /// listeners must not allocate. Cleared when Play starts.
    /// </summary>
    public static class AIEvents
    {
        /// <summary>An enemy of any kind died (horde monster or elite).</summary>
        public static event Action<EnemyKilledInfo> EnemyKilled;

        /// <summary>A wave of the arena began: its index from 0.</summary>
        public static event Action<int> WaveStarted;

        /// <summary>Every wave is out, nothing is alive and the elite (if any) is dead.</summary>
        public static event Action ArenaCleared;

        public static void RaiseEnemyKilled(in EnemyKilledInfo info) => EnemyKilled?.Invoke(info);

        public static void RaiseWaveStarted(int wave) => WaveStarted?.Invoke(wave);

        public static void RaiseArenaCleared() => ArenaCleared?.Invoke();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            EnemyKilled = null;
            WaveStarted = null;
            ArenaCleared = null;
        }
    }
}

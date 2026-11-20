using System;

namespace Game
{
    /// <summary>
    /// The elite on the arena for the HUD (its health bar): whether one is alive, its health 0..1, when it appears and
    /// when it dies. Set by the horde's big Regax (HordeBehaviour); read by the menu team. One elite at a time. Main thread;
    /// cleared when Play starts. In the Game assembly so the menu reads it without referencing the AI adapters.
    /// </summary>
    public static class EliteStatus
    {
        public static bool IsAlive { get; private set; }

        /// <summary>Health share 0..1 while alive; 0 otherwise.</summary>
        public static float Health01 { get; private set; }

        public static event Action Appeared;

        /// <summary>Health share changed (0..1).</summary>
        public static event Action<float> HealthChanged;

        public static event Action Died;

        public static void ReportAppeared(float health01)
        {
            IsAlive = true;
            Health01 = health01;
            Appeared?.Invoke();
        }

        public static void ReportHealth(float health01)
        {
            if (!IsAlive || health01 == Health01)
                return;
            Health01 = health01;
            HealthChanged?.Invoke(health01);
        }

        public static void ReportDied()
        {
            if (!IsAlive)
                return;
            IsAlive = false;
            Health01 = 0f;
            Died?.Invoke();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsAlive = false;
            Health01 = 0f;
            Appeared = null;
            HealthChanged = null;
            Died = null;
        }
    }
}

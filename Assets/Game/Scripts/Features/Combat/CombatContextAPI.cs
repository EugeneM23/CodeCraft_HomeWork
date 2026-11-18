using Atomic.Entities;

namespace Game
{
    /// <summary>GameContext keys of the combat team.</summary>
    [EntityExtensionsAPI]
    public static partial class CombatContextAPI
    {
        /// <summary>Every enemy the guns can aim at; the AI team registers the horde as a source.</summary>
        public static readonly ValueKey<IGameContext, IEnemyTargets> EnemyTargets = new(nameof(EnemyTargets));

        /// <summary>The tank's smoke screen (special module E).</summary>
        public static readonly ValueKey<IGameContext, ISmokeScreen> SmokeScreen = new(nameof(SmokeScreen));
    }
}

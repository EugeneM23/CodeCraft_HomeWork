using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// The run's services in the level's GameContext: <c>GameContext.Instance.GetRun()</c>,
    /// <c>GameContext.Instance.GetEnemyDeaths()</c> (and TryGet... where the run may be absent, e.g. the sandbox).
    /// </summary>
    [EntityExtensionsAPI]
    public static partial class RunAPI
    {
        public static readonly ValueKey<IGameContext, IRunService> Run = new(nameof(Run));
        public static readonly ValueKey<IGameContext, IEnemyDeaths> EnemyDeaths = new(nameof(EnemyDeaths));
    }
}

using System;

namespace Game
{
    /// <summary>
    /// The level's feed of enemy deaths. One per level, in the GameContext (RunAPI.EnemyDeaths). AI reports a death once,
    /// at the moment the enemy is dead (before its body is baked or pooled); everyone else subscribes.
    /// </summary>
    public interface IEnemyDeaths
    {
        event Action<EnemyDeath> Died;

        void Report(in EnemyDeath death);
    }
}

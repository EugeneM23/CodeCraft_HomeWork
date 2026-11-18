using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Extra monsters called into the arena outside its waves: the elite's roar calls Crawlers. The arena director
    /// sends them out of the burrows nearest <paramref name="near"/>, over its live limit if need be.
    /// </summary>
    public interface IArenaReinforcements
    {
        void Summon(AIEnemyKind kind, int count, Vector3 near);
    }
}

using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Extra monsters called into the arena outside its waves. The arena director sends them out of the burrows
    /// nearest <paramref name="near"/>, over its live limit if need be. Found on the level's AI entity
    /// (<see cref="AIAPI.ArenaReinforcements"/>) while an arena runs.
    /// </summary>
    public interface IArenaReinforcements
    {
        void Summon(EnemyKind kind, int count, Vector3 near);
    }
}

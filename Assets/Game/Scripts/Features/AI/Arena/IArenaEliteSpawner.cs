using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>Puts an elite into the arena for the director (<see cref="ArenaEliteSpawner"/> in the game, a fake in tests).</summary>
    public interface IArenaEliteSpawner
    {
        /// <summary>
        /// An elite at <paramref name="at"/> facing <paramref name="facing"/>; <paramref name="strength"/> is the arena's
        /// health and damage multiplier. False: none could be made.
        /// </summary>
        bool SpawnElite(Vector3 at, Vector3 facing, float strength);
    }
}

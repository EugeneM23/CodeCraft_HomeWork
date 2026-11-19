using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// What the arena director needs of the level's horde (<see cref="HordeBehaviour"/>; a fake in tests): how many
    /// live, monsters out of a burrow, a clean slate for the next arena.
    /// </summary>
    public interface IArenaHorde
    {
        /// <summary>Live monsters, the ones asked for and not yet in the crowd included.</summary>
        int AliveCount { get; }

        /// <summary>Seconds a monster climbs out of its burrow.</summary>
        float EmergeSeconds { get; }

        /// <summary>
        /// A monster of <paramref name="kind"/> (Crawler, Spitter or the big EliteRegax) climbing out at <paramref name="at"/> with its kind's
        /// health × <paramref name="healthScale"/> and damage × <paramref name="damageScale"/>. False: the horde has no
        /// such kind or no room.
        /// </summary>
        bool Spawn(EnemyKind kind, Vector3 at, float healthScale, float damageScale);

        /// <summary>A monster starts climbing out of a burrow opened just now (its sound).</summary>
        void BurrowOpened(Vector3 at);

        /// <summary>A wave came out (its sound).</summary>
        void WaveStarted();

        /// <summary>Every monster, shot in flight and corpse gone, without death events (the arena is rebuilt).</summary>
        void ResetForArena();
    }
}

using System;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>The waves of a run's arenas by number (arena 1 is the first row; past the end, the last row).</summary>
    [CreateAssetMenu(fileName = "ArenaWavesTable", menuName = "Game/AI/Arena Waves Table")]
    public sealed class ArenaWavesTable : ScriptableObject
    {
        public ArenaWaves[] arenas = Array.Empty<ArenaWaves>();

        /// <summary>The waves of arena <paramref name="number"/> (1-based), or null when the table is empty.</summary>
        public ArenaWaves For(int number)
        {
            if (arenas == null || arenas.Length == 0)
                return null;
            return arenas[Mathf.Clamp(number - 1, 0, arenas.Length - 1)];
        }
    }
}

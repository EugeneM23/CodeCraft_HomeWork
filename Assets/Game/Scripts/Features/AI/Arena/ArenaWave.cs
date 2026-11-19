using System;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>One wave of an arena: when it comes, what is in it, how fast it leaves the burrows.</summary>
    [Serializable]
    public sealed class ArenaWave
    {
        [Tooltip("Seconds from the start of the fight (after the lull) to this wave.")]
        public float delay;

        [Tooltip("Seconds between two monsters of this wave leaving the queue.")]
        public float spawnInterval = 0.35f;

        public ArenaWaveEntry[] entries = Array.Empty<ArenaWaveEntry>();

        public int Count
        {
            get
            {
                int n = 0;
                if (entries != null)
                    for (int i = 0; i < entries.Length; i++)
                        n += Mathf.Max(0, entries[i].count);
                return n;
            }
        }
    }
}

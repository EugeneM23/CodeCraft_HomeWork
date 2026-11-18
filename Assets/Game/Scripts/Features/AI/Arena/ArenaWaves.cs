using System;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The waves of one arena (GDD §10, §13): in order, each with its delay from the start of the fight, its enemies and
    /// how fast they leave the burrows; the chapter's live limit and multiplier. Made by <c>Tools/AI/Build Arena Waves</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "Arena_01_Waves", menuName = "Game/AI/Arena Waves")]
    public sealed class ArenaWaves : ScriptableObject
    {
        [Tooltip("Waves in order of their delay.")]
        public ArenaWave[] waves = Array.Empty<ArenaWave>();

        [Tooltip("Live enemies at most (the chapter's; GDD §5: chapter 1 — 40). New ones come while fewer than 70 % of it live.")]
        public int liveLimit = 40;

        [Tooltip("The chapter's multiplier of enemy health and damage (×1 / ×1.6 / ×2.4); the arena adds 6 % per arena after the first.")]
        public float chapterMultiplier = 1f;

        /// <summary>Enemies of every wave.</summary>
        public int TotalCount
        {
            get
            {
                int n = 0;
                if (waves != null)
                    for (int i = 0; i < waves.Length; i++)
                        n += waves[i]?.Count ?? 0;
                return n;
            }
        }

        /// <summary>Enemies of <paramref name="kind"/> over every wave.</summary>
        public int CountOf(EnemyKind kind)
        {
            int n = 0;
            if (waves == null)
                return 0;
            for (int i = 0; i < waves.Length; i++)
            {
                ArenaWaveEntry[] entries = waves[i]?.entries;
                if (entries == null)
                    continue;
                for (int k = 0; k < entries.Length; k++)
                    if (entries[k].kind == kind)
                        n += Mathf.Max(0, entries[k].count);
            }

            return n;
        }
    }
}

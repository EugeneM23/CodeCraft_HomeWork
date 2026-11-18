using System;

namespace Game
{
    /// <summary>The player's statistics in the save (GDD §14).</summary>
    [Serializable]
    public sealed class StatsSaveData
    {
        public int runs;
        public int kills;
        public int crushes;
        public int elitesKilled;
        public int bossesKilled;
        public int housesDestroyed;
        public float playTimeSec;

        public ProgressStats ToStats() =>
            new(runs, kills, crushes, elitesKilled, bossesKilled, housesDestroyed, playTimeSec);
    }
}

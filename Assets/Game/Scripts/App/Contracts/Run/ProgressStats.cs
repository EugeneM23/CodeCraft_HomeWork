namespace Game
{
    /// <summary>The player's statistics (GDD §14).</summary>
    public readonly struct ProgressStats
    {
        public readonly int Runs;
        public readonly int Kills;
        public readonly int Crushes;
        public readonly int ElitesKilled;
        public readonly int BossesKilled;
        public readonly int HousesDestroyed;
        public readonly float PlayTimeSec;

        public ProgressStats(int runs, int kills, int crushes, int elitesKilled, int bossesKilled, int housesDestroyed,
            float playTimeSec)
        {
            Runs = runs;
            Kills = kills;
            Crushes = crushes;
            ElitesKilled = elitesKilled;
            BossesKilled = bossesKilled;
            HousesDestroyed = housesDestroyed;
            PlayTimeSec = playTimeSec;
        }
    }
}

namespace Game
{
    /// <summary>
    /// The run's income (GDD §7) in base units, before the chapter's reward multiplier: per enemy kind, for clearing an
    /// arena. <see cref="RunService"/> sums base units and multiplies the sum once (see there for the rounding).
    /// </summary>
    public static class RunRewards
    {
        public const int CRAWLER_MARKS = 1;
        public const int CRAWLER_CRUSH_BONUS_MARKS = 1;
        public const int SPITTER_MARKS = 2;
        public const int EXPLODER_MARKS = 2;
        public const int ELITE_MARKS = 40;
        public const int ELITE_SAMPLES = 2;
        public const int BOSS_MARKS = 200;
        public const int BOSS_SAMPLES = 5;
        public const int ARENA_CLEAR_BASE_MARKS = 10;
        public const int ARENA_CLEAR_MARKS_PER_ARENA = 2;

        /// <summary>
        /// An enemy's reward. A boss by kind; an elite by kind or by the death's Elite flag (any kind with the flag pays
        /// as an elite); otherwise by kind, a crushed Crawler +1.
        /// </summary>
        public static void ForDeath(in EnemyDeath death, out int marks, out int samples)
        {
            samples = 0;
            if (death.Kind == EnemyKind.Boss)
            {
                marks = BOSS_MARKS;
                samples = BOSS_SAMPLES;
                return;
            }

            if (death.Kind == EnemyKind.EliteRegax || death.Elite)
            {
                marks = ELITE_MARKS;
                samples = ELITE_SAMPLES;
                return;
            }

            switch (death.Kind)
            {
                case EnemyKind.Crawler:
                    marks = CRAWLER_MARKS + (death.Crushed ? CRAWLER_CRUSH_BONUS_MARKS : 0);
                    break;
                case EnemyKind.Spitter:
                    marks = SPITTER_MARKS;
                    break;
                case EnemyKind.Exploder:
                    marks = EXPLODER_MARKS;
                    break;
                default:
                    marks = 0;
                    break;
            }
        }

        /// <summary>Clearing arena n (1-based): 10 + 2·n.</summary>
        public static int ForArenaClear(int arenaNumber) =>
            ARENA_CLEAR_BASE_MARKS + ARENA_CLEAR_MARKS_PER_ARENA * arenaNumber;
    }
}

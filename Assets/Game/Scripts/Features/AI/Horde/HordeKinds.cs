namespace Game.AIKit.Adapters
{
    /// <summary>Rows of the horde's kind table and the game's enemy kinds they are.</summary>
    public static class HordeKinds
    {
        public const int CRAWLER = 0;
        public const int SPITTER = 1;
        /// <summary>The big Regax of arena 5 (the game's EliteRegax): the crowd's logic at full size.</summary>
        public const int BRUTE = 2;
        public const int COUNT = 3;

        public static EnemyKind ToEnemyKind(int row) => row switch
        {
            SPITTER => EnemyKind.Spitter,
            BRUTE => EnemyKind.EliteRegax,
            _ => EnemyKind.Crawler
        };

        /// <summary>The row reports its death as an elite's (rewards, the HUD's elite bar).</summary>
        public static bool IsElite(int row) => row == BRUTE;

        /// <summary>The row of a horde kind; false for kinds the horde does not have (the exploder, the boss).</summary>
        public static bool TryGetRow(EnemyKind kind, out int row)
        {
            switch (kind)
            {
                case EnemyKind.Crawler:
                    row = CRAWLER;
                    return true;
                case EnemyKind.Spitter:
                    row = SPITTER;
                    return true;
                case EnemyKind.EliteRegax:
                    row = BRUTE;
                    return true;
                default:
                    row = -1;
                    return false;
            }
        }
    }
}

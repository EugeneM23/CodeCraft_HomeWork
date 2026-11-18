namespace Game.AIKit.Adapters
{
    /// <summary>Rows of the horde's kind table and the game's enemy kinds they are.</summary>
    public static class HordeKinds
    {
        public const int CRAWLER = 0;
        public const int SPITTER = 1;
        public const int COUNT = 2;

        public static EnemyKind ToEnemyKind(int row) => row == SPITTER ? EnemyKind.Spitter : EnemyKind.Crawler;

        /// <summary>The row of a horde kind; false for kinds the horde does not have (the elite, the boss).</summary>
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
                default:
                    row = -1;
                    return false;
            }
        }
    }
}

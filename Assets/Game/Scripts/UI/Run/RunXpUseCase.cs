namespace Game
{
    /// <summary>
    /// The run's experience curve (GDD §3): the experience from level n to n + 1 is 10 + 12·(n − 1).
    /// Experience counts within the level: it starts again from the remainder after each level up.
    /// </summary>
    public static class RunXpUseCase
    {
        public const int FIRST_LEVEL_XP = 10;
        public const int XP_STEP = 12;

        /// <summary>Experience needed to go from this level (1-based) to the next.</summary>
        public static int ToNextLevel(int level) => FIRST_LEVEL_XP + XP_STEP * (level < 1 ? 0 : level - 1);

        /// <summary>Adds experience; returns how many levels it gave (0 or more). Level and xp are updated.</summary>
        public static int Add(ref int level, ref int xp, int gained)
        {
            if (gained <= 0)
                return 0;

            if (level < 1)
                level = 1;

            xp += gained;
            int levels = 0;
            int need = ToNextLevel(level);
            while (xp >= need)
            {
                xp -= need;
                level++;
                levels++;
                need = ToNextLevel(level);
            }
            return levels;
        }
    }
}

namespace Game
{
    /// <summary>
    /// Whether the tank crushes an enemy (GDD §4, §5): fast enough, and — for an enemy with a crush mark
    /// (CombatAPI.CrushBelowHealth, the elite's 0.25) — only once its health is down to the mark.
    /// </summary>
    public static class CrushRuleUseCase
    {
        /// <summary>No mark: any health is crushable.</summary>
        public const float NO_MARK = 1f;

        /// <param name="speed">How fast (m/s) the tank closes on the enemy.</param>
        /// <param name="minSpeed">The slowest (m/s) that still crushes.</param>
        /// <param name="healthPercent">The enemy's health share, 0..1 (Health.GetPercent).</param>
        /// <param name="crushBelowHealth">The enemy's mark, 0..1; <see cref="NO_MARK"/> without one.</param>
        public static bool CanCrush(float speed, float minSpeed, float healthPercent, float crushBelowHealth = NO_MARK) =>
            speed >= minSpeed && IsCrushable(healthPercent, crushBelowHealth);

        /// <summary>The enemy's health is at or below its mark.</summary>
        public static bool IsCrushable(float healthPercent, float crushBelowHealth) => healthPercent <= crushBelowHealth;
    }
}

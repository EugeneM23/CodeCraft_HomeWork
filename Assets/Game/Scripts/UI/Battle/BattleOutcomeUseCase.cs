namespace Game
{
    /// <summary>The rules of the sandbox battle: kill the target number of enemies, lose the tank and it is over.</summary>
    public static class BattleOutcomeUseCase
    {
        /// <summary>
        /// Defeat when the player's tank is destroyed (it wins a tie: the last kill and the death in one
        /// hit are a loss); victory when the kills reach a positive target; otherwise the battle goes on.
        /// </summary>
        public static BattleOutcome Decide(int kills, int killTarget, bool playerDestroyed)
        {
            if (playerDestroyed)
                return BattleOutcome.Defeat;

            if (killTarget > 0 && kills >= killTarget)
                return BattleOutcome.Victory;

            return BattleOutcome.Ongoing;
        }

        /// <summary>Credits for the battle: the level's reward for a win, nothing otherwise.</summary>
        public static int Reward(BattleOutcome outcome, int levelReward) =>
            outcome == BattleOutcome.Victory && levelReward > 0 ? levelReward : 0;
    }
}

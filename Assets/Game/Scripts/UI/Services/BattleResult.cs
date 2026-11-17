namespace Game
{
    /// <summary>How a battle ended, for the results screen.</summary>
    public readonly struct BattleResult
    {
        public readonly bool Victory;
        public readonly int Kills;
        public readonly float Time;
        public readonly int Reward;

        public BattleResult(bool victory, int kills, float time, int reward)
        {
            Victory = victory;
            Kills = kills;
            Time = time;
            Reward = reward;
        }
    }
}

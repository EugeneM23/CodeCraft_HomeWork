namespace Game
{
    /// <summary>Counters of the battle for the HUD. Read a few times a second, not every frame.</summary>
    public interface IBattleStats
    {
        int Kills { get; }

        /// <summary>Kills needed to win; 0 when the level has no such goal.</summary>
        int KillTarget { get; }

        /// <summary>Seconds of battle, pauses not counted.</summary>
        float ElapsedTime { get; }
    }
}

using System;

namespace Game
{
    /// <summary>Tells the battle UI that the battle is over and how it ended.</summary>
    public interface IBattleResultSource
    {
        event Action<BattleResult> Finished;
    }
}

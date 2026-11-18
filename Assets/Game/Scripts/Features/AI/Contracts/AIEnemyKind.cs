namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Kinds of enemy the AI raises events about (GDD §5). A stand-in for the core's <c>EnemyKind</c>
    /// (milestone 1 contracts, App/Contracts): replaced by it once it is published.
    /// </summary>
    public enum AIEnemyKind
    {
        Crawler = 0,
        Spitter = 1,
        Exploder = 2,
        EliteRegax = 3
    }
}

namespace Game
{
    /// <summary>What kind of enemy it is (GDD §5): the rewards, the counters and the sounds follow from it.</summary>
    public enum EnemyKind
    {
        /// <summary>Ползун: the horde's mass, food for the tracks.</summary>
        Crawler,

        /// <summary>Плевун: ranged acid, hides behind houses.</summary>
        Spitter,

        /// <summary>Взрывун: blows up close to the tank (chapter 2).</summary>
        Exploder,

        /// <summary>The elite Regax of arenas 5, 10, 15.</summary>
        EliteRegax,

        /// <summary>The chapter's boss on arena 20.</summary>
        Boss
    }
}

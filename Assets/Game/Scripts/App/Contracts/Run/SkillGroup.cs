namespace Game
{
    /// <summary>Where a run skill goes (GDD §3 «Прокачка внутри забега»): weapons take up to 4 slots, upgrades up to 6.</summary>
    public enum SkillGroup
    {
        /// <summary>An automatic weapon (turret, mortar, air strike...).</summary>
        Weapon,

        /// <summary>An upgrade of the gun (ricochet, rate of fire...).</summary>
        Gun,

        /// <summary>An upgrade of the tank (armor, magnet...).</summary>
        Tank
    }
}

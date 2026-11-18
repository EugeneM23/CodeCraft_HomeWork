namespace Game
{
    /// <summary>How a module's value meets the stat: every Set of a loadout goes first, then every Multiply.</summary>
    public enum ModuleStatOp
    {
        /// <summary>The stat becomes the value (the armour-piercing gun's 250 damage).</summary>
        Set,

        /// <summary>The stat is multiplied by the value (screens: health ×1.4).</summary>
        Multiply
    }
}

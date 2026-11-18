namespace Game
{
    /// <summary>A module in a loadout slot and its level (1..3).</summary>
    public readonly struct ModuleLevel
    {
        public readonly ModuleId Id;
        public readonly int Level;

        public ModuleLevel(ModuleId id, int level)
        {
            Id = id;
            Level = level;
        }
    }
}

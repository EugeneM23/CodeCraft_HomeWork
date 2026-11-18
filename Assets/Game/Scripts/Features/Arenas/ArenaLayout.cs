namespace Game
{
    /// <summary>How the houses of an arena stand against the line from the entry to the gate.</summary>
    public enum ArenaLayout
    {
        /// <summary>Houses anywhere off the entry and the gate.</summary>
        Scattered,

        /// <summary>A house between the tank and the far burrows: Spitters hide behind it (arena 3).</summary>
        CoverForSpitters,

        /// <summary>A house in the middle, on the line from the entry to the gate (arena 4).</summary>
        HouseBlocksGate
    }
}

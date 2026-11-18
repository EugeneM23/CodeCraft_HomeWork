namespace Game
{
    /// <summary>Why a purchase or an upgrade went through or not: the shop shows it on the button.</summary>
    public enum PurchaseResult
    {
        Done,
        NotEnoughMarks,
        NotEnoughSamples,
        AlreadyOwned,

        /// <summary>Upgrading a module that is not owned.</summary>
        NotOwned,

        /// <summary>The module is at its top level (or at the level cap of this milestone).</summary>
        MaxLevel,

        /// <summary>Not for sale now (not in the shop, or its condition is not met).</summary>
        Locked,

        /// <summary>No such id in the catalog.</summary>
        Unknown
    }
}

namespace Game
{
    /// <summary>A module as the hangar and the shop show it. Prices are 0 when not applicable; -1 level price = at the top.</summary>
    public readonly struct HangarModule
    {
        public readonly string Id;
        public readonly string SlotId;
        public readonly bool Owned;
        /// <summary>Not for sale yet: LockReasonKey (a UiStrings key) says what opens it.</summary>
        public readonly bool Locked;
        public readonly string LockReasonKey;
        public readonly int Level;
        public readonly int MaxLevel;
        public readonly int PriceMarks;
        public readonly int PriceSamples;
        /// <summary>Price of the next level; -1 when not owned or at MaxLevel.</summary>
        public readonly int UpgradeMarks;
        public readonly int UpgradeSamples;

        public HangarModule(string id, string slotId, bool owned, bool locked, string lockReasonKey, int level,
            int maxLevel, int priceMarks, int priceSamples, int upgradeMarks, int upgradeSamples)
        {
            Id = id;
            SlotId = slotId;
            Owned = owned;
            Locked = locked;
            LockReasonKey = lockReasonKey;
            Level = level;
            MaxLevel = maxLevel;
            PriceMarks = priceMarks;
            PriceSamples = priceSamples;
            UpgradeMarks = upgradeMarks;
            UpgradeSamples = upgradeSamples;
        }

        public bool IsValid => Id != null;
    }
}

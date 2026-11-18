using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A tank module (GDD §6): its slot, price and the price of levels 2 and 3. The combat team keeps the stats each
    /// level gives (+15 % to two main parameters) in its own fields; the save keeps the id and the level.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Tank/Module", fileName = "ModuleConfig")]
    public sealed class ModuleConfig : ScriptableObject
    {
        public const int MAX_LEVEL = 3;

        [SerializeField] private ModuleId _id;
        [SerializeField] private ModuleSlot _slot;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [Tooltip("A standard module: owned from the start, the slot's default")]
        [SerializeField] private bool _standard;
        [Tooltip("Price in marks (0 for a standard one)")]
        [SerializeField, Min(0)] private int _priceMarks;
        [SerializeField, Min(0)] private int _priceSamples;
        [Tooltip("Level 2: marks, samples (GDD §6, module levels)")]
        [SerializeField] private Cost _level2 = new() { marks = 300 };
        [Tooltip("Level 3: marks, samples")]
        [SerializeField] private Cost _level3 = new() { marks = 600, samples = 5 };
        [Tooltip("Sold in the shop now (milestone 1 sells two)")]
        [SerializeField] private bool _inShop = true;
        [Tooltip("The highest level reachable now (milestone 1: 2)")]
        [SerializeField, Range(1, MAX_LEVEL)] private int _levelCap = MAX_LEVEL;

        public ModuleId Id => _id;
        public ModuleSlot Slot => _slot;
        public string DisplayName => _displayName;
        public string Description => _description;
        public bool IsStandard => _standard;
        public int PriceMarks => _priceMarks;
        public int PriceSamples => _priceSamples;
        public bool InShop => _inShop;
        public int LevelCap => _levelCap;

        /// <summary>The cost of reaching the level (2 or 3); default for any other.</summary>
        public Cost UpgradeCost(int toLevel) => toLevel == 2 ? _level2 : toLevel == 3 ? _level3 : default;

        [Serializable]
        public struct Cost
        {
            public int marks;
            public int samples;

            public Cost(int marks, int samples)
            {
                this.marks = marks;
                this.samples = samples;
            }
        }

        /// <summary>Setup for tests and the catalog builder.</summary>
        public void Configure(ModuleId id, ModuleSlot slot, bool standard, int priceMarks, int priceSamples,
            Cost level2, Cost level3, bool inShop = true, int levelCap = MAX_LEVEL)
        {
            _id = id;
            _slot = slot;
            _standard = standard;
            _priceMarks = priceMarks;
            _priceSamples = priceSamples;
            _level2 = level2;
            _level3 = level3;
            _inShop = inShop;
            _levelCap = levelCap;
        }
    }
}

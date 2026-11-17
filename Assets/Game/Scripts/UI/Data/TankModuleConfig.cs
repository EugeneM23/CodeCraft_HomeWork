using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A tank module sold in the shop and installed in the hangar. Level 1 comes with the purchase;
    /// each further entry of levels is one upgrade. Placeholder numbers until the game design document.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/UI/Tank Module", fileName = "TankModuleConfig")]
    public sealed class TankModuleConfig : ScriptableObject
    {
        [Tooltip("Stable key in the save file")]
        public string id;
        public TankSlot slot;
        public string displayName;
        [TextArea] public string description;
        public int price = 500;
        [Tooltip("Element 0 is level 1 (its price is ignored: the purchase); each next one is an upgrade")]
        public List<Level> levels = new() { new Level() };

        public int MaxLevel => Mathf.Max(1, levels.Count);

        /// <summary>Stats this module adds at the given level (1-based); levels add up.</summary>
        public TankStats StatsAt(int level)
        {
            TankStats sum = default;
            int count = Mathf.Min(level, levels.Count);
            for (int i = 0; i < count; i++)
                sum += levels[i].bonus;
            return sum;
        }

        /// <summary>Price to go from level to level + 1; -1 at the top level or when not owned.</summary>
        public int UpgradePrice(int level) => level >= 1 && level < levels.Count ? levels[level].price : -1;

        [Serializable]
        public sealed class Level
        {
            public int price;
            public TankStats bonus;
        }
    }
}

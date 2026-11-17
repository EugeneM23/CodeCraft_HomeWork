using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>Everything the shop sells, the tank's base stats and what a new player starts with.</summary>
    [CreateAssetMenu(menuName = "Game/UI/Shop Catalog", fileName = "ShopCatalogConfig")]
    public sealed class ShopCatalogConfig : ScriptableObject
    {
        public TankStats baseStats = new() { health = 300, armor = 10, speed = 8, damage = 60, reload = 3f, fireRate = 6 };
        public int startCredits = 1000;
        [Tooltip("Owned and installed at the start of a new game")]
        public List<TankModuleConfig> startModules = new();
        public List<TankModuleConfig> modules = new();

        public TankModuleConfig Find(string id)
        {
            for (int i = 0; i < modules.Count; i++)
                if (modules[i] != null && modules[i].id == id)
                    return modules[i];
            return null;
        }
    }
}

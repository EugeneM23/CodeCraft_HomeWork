using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// STUB data of StubHangarSource until the core's save and the combat team's stats: which catalog to read,
    /// the start balance, the 88-mm gun's numbers (TankConfig has health and speed only), each module's effect on
    /// the hangar's numbers and the recommended tank per chapter. Loaded from Resources (any scene can open the
    /// hangar source); built by UiDataBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/UI/Hangar Stub", fileName = "HangarStubConfig")]
    public sealed class HangarStubConfig : ScriptableObject
    {
        /// <summary>Resources path of the asset.</summary>
        public const string RESOURCE_PATH = "HangarStubConfig";

        [Tooltip("Tanks, modules and chapters (placeholder catalog until the core ships its own asset)")]
        public GameCatalog catalog;
        [Tooltip("TankIds value of the tank a new game starts with")]
        public string startTank = "tiger";
        [Tooltip("STUB: so the shop can be tried before runs pay marks")]
        [Min(0)] public int startMarks = 1500;
        [Min(0)] public int startSamples = 10;
        [Tooltip("The standard gun's damage and reload, added to TankConfig's health and speed")]
        public float cannonDamage = 150f;
        public float cannonReload = 2f;
        public List<HangarModuleStats> moduleStats = new();
        [Tooltip("TankIds value recommended for each chapter, in the catalog's chapter order")]
        public List<string> recommendedTanks = new();

        public HangarModuleStats FindStats(string moduleId)
        {
            for (int i = 0; i < moduleStats.Count; i++)
                if (moduleStats[i] != null && moduleStats[i].moduleId == moduleId)
                    return moduleStats[i];
            return null;
        }

        public string RecommendedTank(int chapterIndex) =>
            chapterIndex >= 0 && chapterIndex < recommendedTanks.Count ? recommendedTanks[chapterIndex] : startTank;
    }
}

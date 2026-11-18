using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>
    /// A small GameCatalog for the progress tests: Tiger (starting) and Hound (3000 marks + 15 samples), Fortress
    /// (not priced); the six standard modules, the AP gun (1200, capped at level 2), skirts (1200), the howitzer (not in
    /// the shop); Outskirts -> Town -> Quarry. Dispose destroys every asset it made.
    /// </summary>
    public sealed class MetaTestCatalog : IDisposable
    {
        public const int HOUND_MARKS = 3000;
        public const int HOUND_SAMPLES = 15;
        public const int AP_PRICE = 1200;
        public const int SKIRTS_PRICE = 1200;

        private readonly List<Object> _assets = new();

        public MetaTestCatalog()
        {
            var standardLevel2 = new ModuleConfig.Cost(300, 0);
            var standardLevel3 = new ModuleConfig.Cost(600, 5);
            var modules = new List<ModuleConfig>
            {
                Module(ModuleIds.Gun88He, ModuleSlot.Gun, true, 0, standardLevel2, standardLevel3),
                Module(ModuleIds.MachineGun, ModuleSlot.MachineGun, true, 0, standardLevel2, standardLevel3),
                Module(ModuleIds.ArmorStandard, ModuleSlot.Armor, true, 0, standardLevel2, standardLevel3),
                Module(ModuleIds.EngineStandard, ModuleSlot.Engine, true, 0, standardLevel2, standardLevel3),
                Module(ModuleIds.TracksStandard, ModuleSlot.Tracks, true, 0, standardLevel2, standardLevel3),
                Module(ModuleIds.SpecialSmoke, ModuleSlot.Special, true, 0, standardLevel2, standardLevel3),
                Module(ModuleIds.GunArmorPiercing, ModuleSlot.Gun, false, AP_PRICE, new ModuleConfig.Cost(600, 0),
                    new ModuleConfig.Cost(1200, 5), levelCap: 2),
                Module(ModuleIds.ArmorSkirts, ModuleSlot.Armor, false, SKIRTS_PRICE, new ModuleConfig.Cost(600, 0),
                    new ModuleConfig.Cost(1200, 5)),
                Module(ModuleIds.GunHowitzer, ModuleSlot.Gun, false, 2500, new ModuleConfig.Cost(1250, 0),
                    new ModuleConfig.Cost(2500, 10), inShop: false)
            };

            ModuleId[] loadout =
            {
                ModuleIds.Gun88He, ModuleIds.MachineGun, ModuleIds.ArmorStandard, ModuleIds.EngineStandard,
                ModuleIds.TracksStandard, ModuleIds.SpecialSmoke
            };
            var tanks = new List<TankConfig>
            {
                Tank(TankIds.Tiger, true, 0, 0, loadout),
                Tank(TankIds.Hound, false, HOUND_MARKS, HOUND_SAMPLES, loadout),
                Tank(TankIds.Fortress, false, 0, 0, loadout)
            };

            ChapterConfig outskirts = Chapter(ChapterIds.Outskirts, null);
            ChapterConfig town = Chapter(ChapterIds.Town, outskirts);
            ChapterConfig quarry = Chapter(ChapterIds.Quarry, town);

            Catalog = Make<GameCatalog>();
            Catalog.Configure(tanks, modules, Array.Empty<SkillConfig>(), new[] { outskirts, town, quarry });
        }

        public GameCatalog Catalog { get; }

        public void Dispose()
        {
            foreach (Object asset in _assets)
            {
                if (asset != null)
                    Object.DestroyImmediate(asset);
            }

            _assets.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        private ModuleConfig Module(ModuleId id, ModuleSlot slot, bool standard, int price, ModuleConfig.Cost level2,
            ModuleConfig.Cost level3, bool inShop = true, int levelCap = ModuleConfig.MAX_LEVEL)
        {
            var module = Make<ModuleConfig>();
            module.Configure(id, slot, standard, price, 0, level2, level3, inShop, levelCap);
            return module;
        }

        private TankConfig Tank(TankId id, bool starting, int marks, int samples, ModuleId[] loadout)
        {
            var tank = Make<TankConfig>();
            tank.Configure(id, starting, 4, 6, loadout);

            // TankConfig.Configure takes no price: set it the way the inspector does
            var serialized = new SerializedObject(tank);
            serialized.FindProperty("_priceMarks").intValue = marks;
            serialized.FindProperty("_priceSamples").intValue = samples;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tank;
        }

        private ChapterConfig Chapter(ChapterId id, ChapterConfig required)
        {
            var chapter = Make<ChapterConfig>();
            chapter.Configure(id, null, 1f, 1f, 1000, 10, required);
            return chapter;
        }
    }
}

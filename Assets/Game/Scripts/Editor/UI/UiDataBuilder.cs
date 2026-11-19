using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates or updates the placeholder data of the menus: in Assets/Game/Configs/UI nothing of its own any
    /// more (the old level assets are deleted); in Configs/UI/Catalog a placeholder of the core's
    /// catalog (GameCatalog with the 15 modules, the Tiger, 3 chapters and the 5 arenas of chapter 1, GDD §6, §7,
    /// §10) until the core ships its own asset; in Configs/UI/Resources the HangarStubConfig StubHangarSource
    /// reads; in the core's Assets/Game/Configs/Levels a placeholder second level (quarry) added to the core
    /// catalog. Idempotent: existing assets keep their GUIDs and get the values below.
    /// </summary>
    public static class UiDataBuilder
    {
        public const string FOLDER = "Assets/Game/Configs/UI";
        public const string CATALOG_FOLDER = FOLDER + "/Catalog";
        public const string RESOURCES_FOLDER = FOLDER + "/Resources";
        public const string GAME_CATALOG_PATH = CATALOG_FOLDER + "/PlaceholderGameCatalog.asset";
        public const string HANGAR_STUB_PATH = RESOURCES_FOLDER + "/" + HangarStubConfig.RESOURCE_PATH + ".asset";
        public const string LEVEL_FOLDER = "Assets/Game/Configs/Levels";
        public const string LEVEL_CATALOG_PATH = LEVEL_FOLDER + "/LevelCatalogConfig.asset";

        private const string SANDBOX_LEVEL_PATH = LEVEL_FOLDER + "/VoxelSandboxLevelConfig.asset";
        private const string QUARRY_LEVEL_PATH = LEVEL_FOLDER + "/QuarryLevelConfig.asset";
        private const string SANDBOX_LEVEL_ID = "voxel_sandbox";
        private const string QUARRY_LEVEL_ID = "quarry";
        private const string BATTLE_UI_PATH = UiFactory.PREFAB_FOLDER + "/BattleUi.prefab";

        private static readonly string[] OldLevelAssets =
        {
            FOLDER + "/SandboxLevelConfig.asset",
            FOLDER + "/QuarryLevelConfig.asset",
            FOLDER + "/LevelCatalogConfig.asset",
            FOLDER + "/LevelDesignConfig.asset" // design fields of the kill-goal levels, replaced by the run
        };

        // The shop data of the level-based prototype (TankModuleConfig, ShopCatalogConfig: types deleted)
        private static readonly string[] OldShopAssets =
        {
            FOLDER + "/ShopCatalogConfig.asset",
            FOLDER + "/Modules"
        };

        [MenuItem("Tools/UI/Build Placeholder Data")]
        public static void Build()
        {
            UiFactory.EnsureFolder(FOLDER);
            UiFactory.EnsureFolder(CATALOG_FOLDER);
            UiFactory.EnsureFolder(RESOURCES_FOLDER);

            BuildLevels();
            BuildHangar();

            AssetDatabase.SaveAssets();
            Debug.Log($"UI placeholder data built in {FOLDER}");
        }

        public static LevelCatalog LoadLevelCatalog() => AssetDatabase.LoadAssetAtPath<LevelCatalog>(LEVEL_CATALOG_PATH);
        public static HangarStubConfig LoadHangarStub() => AssetDatabase.LoadAssetAtPath<HangarStubConfig>(HANGAR_STUB_PATH);

        /// <summary>
        /// Puts the BattleUi prefab into the core catalog's level layer when it is not there yet. Called after
        /// BattleUiBuilder.Build (UiAllBuilder), since the prefab may not exist when the data is built.
        /// </summary>
        public static void AddBattleUiToLevelLayer()
        {
            GameObject battleUi = AssetDatabase.LoadAssetAtPath<GameObject>(BATTLE_UI_PATH);
            LevelCatalog catalog = LoadLevelCatalog();
            if (battleUi == null || catalog == null)
                return;

            SerializedObject so = new(catalog);
            SerializedProperty layer = so.FindProperty("_levelLayer");
            if (Contains(layer, battleUi))
                return;

            layer.InsertArrayElementAtIndex(layer.arraySize);
            layer.GetArrayElementAtIndex(layer.arraySize - 1).objectReferenceValue = battleUi;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        // ---------- Levels ----------

        private static void BuildLevels()
        {
            // The level assets of the menus before the core owned the level types
            foreach (string oldAsset in OldLevelAssets)
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(oldAsset, AssetPathToGUIDOptions.OnlyExistingAssets)))
                    AssetDatabase.DeleteAsset(oldAsset);

            LevelCatalog catalog = LoadLevelCatalog();
            LevelConfig sandbox = AssetDatabase.LoadAssetAtPath<LevelConfig>(SANDBOX_LEVEL_PATH);
            if (catalog == null || sandbox == null)
            {
                Debug.LogWarning($"{nameof(UiDataBuilder)}: no core {LEVEL_CATALOG_PATH} or {SANDBOX_LEVEL_PATH}; " +
                                 "the placeholder level is not built.");
                return;
            }

            LevelConfig quarry = BuildQuarry(sandbox);
            AddLevelAfter(catalog, sandbox, quarry);
        }

        // A copy of the sandbox's scene set under another id, opened by the sandbox.
        private static LevelConfig BuildQuarry(LevelConfig sandbox)
        {
            LevelConfig quarry = LoadOrCreate<LevelConfig>(QUARRY_LEVEL_PATH);
            SerializedObject source = new(sandbox);
            SerializedObject so = new(quarry);

            so.FindProperty("_id").stringValue = QUARRY_LEVEL_ID;
            so.FindProperty("_displayName").stringValue = "Карьер";
            so.FindProperty("_description").stringValue =
                "Тот же городок, но Regax вдвое больше. Пока использует сцены песочницы.";

            SerializedProperty fromScenes = source.FindProperty("_scenes");
            SerializedProperty scenes = so.FindProperty("_scenes");
            scenes.arraySize = fromScenes.arraySize;
            for (int i = 0; i < fromScenes.arraySize; i++)
            {
                SerializedProperty from = fromScenes.GetArrayElementAtIndex(i);
                SerializedProperty to = scenes.GetArrayElementAtIndex(i);
                to.FindPropertyRelative("_asset").objectReferenceValue = from.FindPropertyRelative("_asset").objectReferenceValue;
                to.FindPropertyRelative("_path").stringValue = from.FindPropertyRelative("_path").stringValue;
            }

            so.FindProperty("_activeSceneIndex").intValue = source.FindProperty("_activeSceneIndex").intValue;
            so.FindProperty("_gameplaySceneIndex").intValue = source.FindProperty("_gameplaySceneIndex").intValue;
            so.FindProperty("_requiredLevel").objectReferenceValue = sandbox;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quarry);
            return quarry;
        }

        private static void AddLevelAfter(LevelCatalog catalog, LevelConfig previous, LevelConfig level)
        {
            SerializedObject so = new(catalog);
            SerializedProperty levels = so.FindProperty("_levels");
            if (Contains(levels, level))
                return;

            int index = levels.arraySize;
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).objectReferenceValue == previous)
                    index = i + 1;

            levels.InsertArrayElementAtIndex(index);
            levels.GetArrayElementAtIndex(index).objectReferenceValue = level;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static bool Contains(SerializedProperty array, Object item)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == item)
                    return true;
            return false;
        }

        // ---------- Hangar (STUB catalog and stub data) ----------

        private static void BuildHangar()
        {
            foreach (string oldAsset in OldShopAssets)
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(oldAsset, AssetPathToGUIDOptions.OnlyExistingAssets)))
                    AssetDatabase.DeleteAsset(oldAsset);

            UiFactory.EnsureFolder(CATALOG_FOLDER + "/Modules");
            UiFactory.EnsureFolder(CATALOG_FOLDER + "/Arenas");

            ModuleConfig.Cost standard2 = new(300, 0), standard3 = new(600, 5);
            ModuleConfig.Cost cheap2 = new(600, 0), cheap3 = new(1200, 5);
            ModuleConfig.Cost dear2 = new(1250, 0), dear3 = new(2500, 10);

            // GDD §6: a standard module per slot, owned from the start; milestone 1 sells gun_ap and the skirts (screens)
            List<ModuleConfig> modules = new()
            {
                Module(ModuleIds.Gun88He, ModuleSlot.Gun, true, 0, standard2, standard3, false),
                Module(ModuleIds.GunArmorPiercing, ModuleSlot.Gun, false, 1200, cheap2, cheap3, true),
                Module(ModuleIds.GunHowitzer, ModuleSlot.Gun, false, 2500, dear2, dear3, false),
                Module(ModuleIds.MachineGun, ModuleSlot.MachineGun, true, 0, standard2, standard3, false),
                Module(ModuleIds.MachineGunTwin, ModuleSlot.MachineGun, false, 1200, cheap2, cheap3, false),
                Module(ModuleIds.ArmorStandard, ModuleSlot.Armor, true, 0, standard2, standard3, false),
                Module(ModuleIds.ArmorSkirts, ModuleSlot.Armor, false, 1200, cheap2, cheap3, true),
                Module(ModuleIds.ArmorDozer, ModuleSlot.Armor, false, 2500, dear2, dear3, false),
                Module(ModuleIds.EngineStandard, ModuleSlot.Engine, true, 0, standard2, standard3, false),
                Module(ModuleIds.EngineBoosted, ModuleSlot.Engine, false, 2500, dear2, dear3, false),
                Module(ModuleIds.TracksStandard, ModuleSlot.Tracks, true, 0, standard2, standard3, false),
                Module(ModuleIds.TracksWide, ModuleSlot.Tracks, false, 1200, cheap2, cheap3, false),
                Module(ModuleIds.SpecialSmoke, ModuleSlot.Special, true, 0, standard2, standard3, false),
                Module(ModuleIds.SpecialRepair, ModuleSlot.Special, false, 1200, cheap2, cheap3, false),
                Module(ModuleIds.SpecialShockwave, ModuleSlot.Special, false, 2500, dear2, dear3, false)
            };

            TankConfig tiger = LoadOrCreate<TankConfig>($"{CATALOG_FOLDER}/TigerTankConfig.asset");
            tiger.Configure(TankIds.Tiger, true, 4, 6, ModuleIds.Gun88He, ModuleIds.MachineGun, ModuleIds.ArmorStandard,
                ModuleIds.EngineStandard, ModuleIds.TracksStandard, ModuleIds.SpecialSmoke);
            EditorUtility.SetDirty(tiger);

            // STUB: every chapter plays the core catalog's first level until the arenas have their own
            LevelCatalog levels = LoadLevelCatalog();
            LevelConfig level = levels != null ? levels.Find(SANDBOX_LEVEL_ID) ?? levels.FirstLevel : null;
            ArenaConfig[] arenas =
            {
                Arena("outskirts_1", ArenaKind.Horde), Arena("outskirts_2", ArenaKind.Horde),
                Arena("outskirts_3", ArenaKind.Horde), Arena("outskirts_4", ArenaKind.Horde),
                Arena("outskirts_5", ArenaKind.Elite)
            };
            ChapterConfig outskirts = Chapter("Outskirts", ChapterIds.Outskirts, level, 1f, 1f, 1000, 10, null, arenas);
            ChapterConfig town = Chapter("Town", ChapterIds.Town, level, 1.6f, 1.5f, 2000, 15, outskirts);
            ChapterConfig quarry = Chapter("Quarry", ChapterIds.Quarry, level, 2.4f, 2f, 3000, 20, town);

            GameCatalog catalog = LoadOrCreate<GameCatalog>(GAME_CATALOG_PATH);
            catalog.Configure(new[] { tiger }, modules, new List<SkillConfig>(), new[] { outskirts, town, quarry });
            EditorUtility.SetDirty(catalog);

            HangarStubConfig stub = LoadOrCreate<HangarStubConfig>(HANGAR_STUB_PATH);
            stub.catalog = catalog;
            stub.startTank = TankIds.Tiger.Value;
            stub.startMarks = 1500;
            stub.startSamples = 10;
            stub.cannonDamage = 150f;
            stub.cannonReload = 2f;
            stub.recommendedTanks = new List<string> { TankIds.Tiger.Value, TankIds.Hound.Value, TankIds.Fortress.Value };
            HangarTankStats gunLevel = new(0, 0, 15, -15);
            HangarTankStats healthLevel = new(15, 0, 0, 0);
            HangarTankStats speedLevel = new(0, 15, 0, 0);
            stub.moduleStats = new List<HangarModuleStats>
            {
                Stats(ModuleIds.Gun88He, default, default, gunLevel),
                Stats(ModuleIds.GunArmorPiercing, new HangarTankStats(0, 0, 100, -0.4f), default, gunLevel),
                Stats(ModuleIds.GunHowitzer, new HangarTankStats(0, 0, 150, 1f), default, gunLevel),
                Stats(ModuleIds.ArmorStandard, default, default, healthLevel),
                Stats(ModuleIds.ArmorSkirts, default, new HangarTankStats(40, -8, 0, 0), healthLevel),
                Stats(ModuleIds.EngineStandard, default, default, speedLevel),
                Stats(ModuleIds.EngineBoosted, default, new HangarTankStats(0, 20, 0, 0), speedLevel),
                Stats(ModuleIds.TracksStandard, default, default, new HangarTankStats(0, 5, 0, 0))
            };
            EditorUtility.SetDirty(stub);
        }

        private static ModuleConfig Module(ModuleId id, ModuleSlot slot, bool standard, int price, ModuleConfig.Cost level2,
            ModuleConfig.Cost level3, bool inShop)
        {
            ModuleConfig module = LoadOrCreate<ModuleConfig>($"{CATALOG_FOLDER}/Modules/{Pascal(id.Value)}ModuleConfig.asset");
            module.Configure(id, slot, standard, price, 0, level2, level3, inShop, ModuleConfig.MAX_LEVEL);
            EditorUtility.SetDirty(module);
            return module;
        }

        private static ArenaConfig Arena(string id, ArenaKind kind)
        {
            ArenaConfig arena = LoadOrCreate<ArenaConfig>($"{CATALOG_FOLDER}/Arenas/{Pascal(id)}ArenaConfig.asset");
            arena.Configure(new ArenaId(id), kind);
            EditorUtility.SetDirty(arena);
            return arena;
        }

        private static ChapterConfig Chapter(string name, ChapterId id, LevelConfig level, float enemies, float rewards,
            int firstMarks, int firstSamples, ChapterConfig required, params ArenaConfig[] arenas)
        {
            ChapterConfig chapter = LoadOrCreate<ChapterConfig>($"{CATALOG_FOLDER}/{name}ChapterConfig.asset");
            chapter.Configure(id, level, enemies, rewards, firstMarks, firstSamples, required, arenas);
            EditorUtility.SetDirty(chapter);
            return chapter;
        }

        private static HangarModuleStats Stats(ModuleId id, HangarTankStats bonus, HangarTankStats percent,
            HangarTankStats levelPercent) =>
            new() { moduleId = id.Value, bonus = bonus, percent = percent, levelPercent = levelPercent };

        // gun_88_he → Gun88He
        private static string Pascal(string id)
        {
            System.Text.StringBuilder name = new();
            bool upper = true;
            foreach (char c in id)
            {
                if (c == '_')
                {
                    upper = true;
                    continue;
                }

                name.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return name.ToString();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}

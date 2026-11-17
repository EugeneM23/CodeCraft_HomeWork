using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates or updates the placeholder data of the menus: in Assets/Game/Configs/UI the design fields of the
    /// levels (LevelDesignConfig), tank modules (2–3 per slot, 3 levels each) and the shop catalog; in the core's
    /// Assets/Game/Configs/Levels a placeholder second level (quarry) added to the core catalog. Idempotent:
    /// existing assets keep their GUIDs and get the values below. Game design replaces the numbers later.
    /// </summary>
    public static class UiDataBuilder
    {
        public const string FOLDER = "Assets/Game/Configs/UI";
        public const string MODULE_FOLDER = FOLDER + "/Modules";
        public const string LEVEL_DESIGN_PATH = FOLDER + "/LevelDesignConfig.asset";
        public const string SHOP_CATALOG_PATH = FOLDER + "/ShopCatalogConfig.asset";
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
            FOLDER + "/LevelCatalogConfig.asset"
        };

        [MenuItem("Tools/UI/Build Placeholder Data")]
        public static void Build()
        {
            UiFactory.EnsureFolder(FOLDER);
            UiFactory.EnsureFolder(MODULE_FOLDER);

            BuildLevels();
            BuildShop();

            AssetDatabase.SaveAssets();
            Debug.Log($"UI placeholder data built in {FOLDER}");
        }

        public static LevelCatalog LoadLevelCatalog() => AssetDatabase.LoadAssetAtPath<LevelCatalog>(LEVEL_CATALOG_PATH);
        public static LevelDesignConfig LoadLevelDesign() => AssetDatabase.LoadAssetAtPath<LevelDesignConfig>(LEVEL_DESIGN_PATH);
        public static ShopCatalogConfig LoadShopCatalog() => AssetDatabase.LoadAssetAtPath<ShopCatalogConfig>(SHOP_CATALOG_PATH);

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

            LevelDesignConfig design = LoadOrCreate<LevelDesignConfig>(LEVEL_DESIGN_PATH);
            design.levels = new List<LevelDesignData>
            {
                new() { levelId = SANDBOX_LEVEL_ID, difficulty = 1, killTarget = 10, reward = 300 },
                new() { levelId = QUARRY_LEVEL_ID, difficulty = 2, killTarget = 20, reward = 600 }
            };
            EditorUtility.SetDirty(design);

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

        // ---------- Shop ----------

        private static void BuildShop()
        {
            List<TankModuleConfig> modules = new()
            {
                // Gun
                Module("CannonT1", "cannon_t1", TankSlot.Gun, "Пушка Т-1", "Штатное орудие: надёжное, без изысков.", 300,
                    Stats(), 400, Stats(damage: 10), 700, Stats(damage: 15, reload: -0.2f)),
                Module("CannonT2", "cannon_t2", TankSlot.Gun, "Пушка Т-2", "Длинный ствол: больше урона, дольше перезарядка.", 1200,
                    Stats(damage: 30, reload: 0.3f), 500, Stats(damage: 10), 900, Stats(damage: 15, reload: -0.2f)),
                Module("CannonT3", "cannon_t3", TankSlot.Gun, "Тяжёлая пушка", "Сносит дом с одного выстрела, но перезаряжается медленно.", 2500,
                    Stats(damage: 70, reload: 0.8f), 800, Stats(damage: 15), 1200, Stats(damage: 20, reload: -0.3f)),

                // Machine gun
                Module("MachineGunT1", "mg_t1", TankSlot.MachineGun, "Пулемёт ПМ-1", "Штатный пулемёт против мелких Regax.", 300,
                    Stats(), 300, Stats(fireRate: 1), 500, Stats(fireRate: 1.5f)),
                Module("MachineGunT2", "mg_t2", TankSlot.MachineGun, "Спаренный пулемёт", "Два ствола — вдвое плотнее огонь.", 1100,
                    Stats(fireRate: 4), 450, Stats(fireRate: 1), 700, Stats(fireRate: 2)),

                // Hull
                Module("HullT1", "hull_t1", TankSlot.Hull, "Лёгкий корпус", "Штатный корпус.", 300,
                    Stats(), 400, Stats(health: 30), 600, Stats(health: 50)),
                Module("HullT2", "hull_t2", TankSlot.Hull, "Средний корпус", "Больше прочности ценой скорости.", 1500,
                    Stats(health: 150, speed: -1), 600, Stats(health: 40), 900, Stats(health: 60)),

                // Engine
                Module("EngineT1", "engine_t1", TankSlot.Engine, "Двигатель Д-1", "Штатный двигатель.", 300,
                    Stats(), 350, Stats(speed: 0.5f), 550, Stats(speed: 1)),
                Module("EngineT2", "engine_t2", TankSlot.Engine, "Двигатель Д-2Ф", "Форсированный двигатель.", 1300,
                    Stats(speed: 3), 500, Stats(speed: 0.5f), 800, Stats(speed: 1)),

                // Tracks
                Module("TracksT1", "tracks_t1", TankSlot.Tracks, "Стальные гусеницы", "Штатные гусеницы.", 300,
                    Stats(), 300, Stats(speed: 0.5f), 500, Stats(health: 20)),
                Module("TracksT2", "tracks_t2", TankSlot.Tracks, "Широкие гусеницы", "Держат завалы и щебень.", 900,
                    Stats(speed: 1, health: 30), 400, Stats(speed: 0.5f), 650, Stats(health: 30)),

                // Armor
                Module("ArmorT1", "armor_t1", TankSlot.Armor, "Катаная броня", "Штатная броня.", 300,
                    Stats(), 400, Stats(armor: 5), 600, Stats(armor: 8)),
                Module("ArmorT2", "armor_t2", TankSlot.Armor, "Композитная броня", "Крепче, но тяжелее.", 1600,
                    Stats(armor: 20, speed: -0.5f), 600, Stats(armor: 6), 900, Stats(armor: 10)),
                Module("ArmorT3", "armor_t3", TankSlot.Armor, "Динамическая защита", "Лучшая броня в магазине.", 2400,
                    Stats(armor: 35, speed: -1), 800, Stats(armor: 8), 1200, Stats(armor: 12))
            };

            ShopCatalogConfig catalog = LoadOrCreate<ShopCatalogConfig>(SHOP_CATALOG_PATH);
            catalog.baseStats = new TankStats { health = 300, armor = 10, speed = 8, damage = 60, reload = 3f, fireRate = 6 };
            catalog.startCredits = 1000;
            catalog.modules = modules;
            catalog.startModules = new List<TankModuleConfig>();
            HashSet<TankSlot> started = new();
            foreach (TankModuleConfig module in modules)
                if (started.Add(module.slot))
                    catalog.startModules.Add(module);
            EditorUtility.SetDirty(catalog);
        }

        private static TankModuleConfig Module(string name, string id, TankSlot slot, string displayName,
            string description, int price, TankStats level1, int price2, TankStats level2, int price3, TankStats level3)
        {
            TankModuleConfig module = LoadOrCreate<TankModuleConfig>($"{MODULE_FOLDER}/{name}TankModuleConfig.asset");
            module.id = id;
            module.slot = slot;
            module.displayName = displayName;
            module.description = description;
            module.price = price;
            module.levels = new List<TankModuleConfig.Level>
            {
                new() { price = 0, bonus = level1 },
                new() { price = price2, bonus = level2 },
                new() { price = price3, bonus = level3 }
            };
            EditorUtility.SetDirty(module);
            return module;
        }

        private static TankStats Stats(float health = 0, float armor = 0, float speed = 0, float damage = 0,
            float reload = 0, float fireRate = 0) =>
            new() { health = health, armor = armor, speed = speed, damage = damage, reload = reload, fireRate = fireRate };

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

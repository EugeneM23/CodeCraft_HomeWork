using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates or updates the placeholder data of the menus in Assets/Game/Configs/UI: two levels, their catalog,
    /// tank modules (2–3 per slot, 3 levels each) and the shop catalog. Idempotent: existing assets keep their
    /// GUIDs and get the values below. Game design replaces the numbers later.
    /// </summary>
    public static class UiDataBuilder
    {
        public const string FOLDER = "Assets/Game/Configs/UI";
        public const string MODULE_FOLDER = FOLDER + "/Modules";
        public const string LEVEL_CATALOG_PATH = FOLDER + "/LevelCatalogConfig.asset";
        public const string SHOP_CATALOG_PATH = FOLDER + "/ShopCatalogConfig.asset";

        private const string SANDBOX_SCENE = "VoxelSandbox";

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

        public static LevelCatalogConfig LoadLevelCatalog() => AssetDatabase.LoadAssetAtPath<LevelCatalogConfig>(LEVEL_CATALOG_PATH);
        public static ShopCatalogConfig LoadShopCatalog() => AssetDatabase.LoadAssetAtPath<ShopCatalogConfig>(SHOP_CATALOG_PATH);

        // ---------- Levels ----------

        private static void BuildLevels()
        {
            LevelConfig sandbox = Level("SandboxLevelConfig", "sandbox", "Песочница",
                "Разрушаемый городок, Regax прячутся в домах. Разнесите укрытия и зачистите улицы.",
                difficulty: 1, kills: 10, reward: 300, required: null);
            LevelConfig quarry = Level("QuarryLevelConfig", "quarry", "Карьер",
                "Тот же городок, но Regax вдвое больше. Пока использует сцену песочницы.",
                difficulty: 2, kills: 20, reward: 600, required: sandbox);

            LevelCatalogConfig catalog = LoadOrCreate<LevelCatalogConfig>(LEVEL_CATALOG_PATH);
            catalog.levels = new List<LevelConfig> { sandbox, quarry };
            EditorUtility.SetDirty(catalog);
        }

        private static LevelConfig Level(string asset, string id, string name, string description, int difficulty,
            int kills, int reward, LevelConfig required)
        {
            LevelConfig level = LoadOrCreate<LevelConfig>($"{FOLDER}/{asset}.asset");
            level.id = id;
            level.displayName = name;
            level.description = description;
            level.sceneName = SANDBOX_SCENE;
            level.difficulty = difficulty;
            level.killTarget = kills;
            level.reward = reward;
            level.requiredLevel = required;
            EditorUtility.SetDirty(level);
            return level;
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

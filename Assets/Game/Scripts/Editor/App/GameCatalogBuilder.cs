using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools → Game → Rebuild Game Catalog: writes the game's configs with the GDD numbers (§3 skills, §4 and §6 tanks
    /// and modules, §7 rewards, §10 chapters) under Assets/Game/Configs/Run and puts the catalog on the App prefab.
    /// Idempotent: an existing asset is loaded by its path and overwritten in place, so its GUID and every reference
    /// to it stay. Batchmode: <c>-executeMethod Game.EditorTools.GameCatalogBuilder.Build</c>.
    /// The numbers live here, not in the inspector: change them here and rebuild, or hand-edited values are lost.
    /// </summary>
    public static class GameCatalogBuilder
    {
        public const string ROOT = "Assets/Game/Configs/Run";
        public const string CATALOG_PATH = ROOT + "/GameCatalogConfig.asset";

        private const string MENU_PATH = "Tools/Game/Rebuild Game Catalog";
        private const string TANKS = ROOT + "/Tanks";
        private const string MODULES = ROOT + "/Modules";
        private const string SKILLS = ROOT + "/Skills";
        private const string CHAPTERS = ROOT + "/Chapters";
        private const string ARENAS = ROOT + "/Arenas";

        private const string APP_PREFAB_PATH = "Assets/Game/Prefabs/App/App.prefab";
        private const string TIGER_PREFAB_PATH = "Assets/Game/Prefabs/Vehicles/Tank_01.prefab";

        // The chapter's own level (arena team: Chapter1_Static + Chapter1_Arena) once it is in the project; until then
        // the sandbox
        private const string OUTSKIRTS_LEVEL_PATH = "Assets/Game/Configs/Levels/Chapter1LevelConfig.asset";
        private const string FALLBACK_LEVEL_PATH = "Assets/Game/Configs/Levels/VoxelSandboxLevelConfig.asset";

        /// <summary>
        /// Milestone 1 (GDD §10 «Веха 1»: «Уровень 2 для штатных модулей»): level 2 is the highest level of every
        /// module, standard or bought. Set it to ModuleConfig.MAX_LEVEL after milestone 1 and rebuild.
        /// </summary>
        private const int MILESTONE_1_MODULE_LEVEL_CAP = 2;

        /// <summary>The bought modules the shop sells in milestone 1 (GDD §10): the AP gun and the skirts.</summary>
        private static readonly ModuleId[] Milestone1Shop = { ModuleIds.GunArmorPiercing, ModuleIds.ArmorSkirts };

        /// <summary>The run skills of milestone 1 (GDD §10); the other 14 are written but out of the pool.</summary>
        private static readonly SkillId[] Milestone1Skills =
        {
            SkillIds.Turret, SkillIds.AirStrike, SkillIds.Ricochet, SkillIds.RateOfFire, SkillIds.Armor, SkillIds.Magnet
        };

        [MenuItem(MENU_PATH, priority = 120)]
        public static void Build()
        {
            foreach (string folder in new[] { TANKS, MODULES, SKILLS, CHAPTERS, ARENAS })
                EnsureFolder(folder);

            List<ModuleConfig> modules = BuildModules();
            List<TankConfig> tanks = BuildTanks();
            List<SkillConfig> skills = BuildSkills();
            List<ChapterConfig> chapters = BuildChapters();

            GameCatalog catalog = LoadOrCreate<GameCatalog>(CATALOG_PATH);
            catalog.Configure(tanks, modules, skills, chapters);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            bool prefabSet = AssignToAppPrefab(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"{nameof(GameCatalogBuilder)}: {CATALOG_PATH} — {tanks.Count} tanks, {modules.Count} modules, " +
                      $"{skills.Count} skills, {chapters.Count} chapters; App prefab {(prefabSet ? "set" : "NOT set")}");
        }

        // ---------- Modules (GDD §6 «Слоты и модули», «Уровни модулей») ----------

        private static List<ModuleConfig> BuildModules() => new()
        {
            Module(ModuleIds.Gun88He, ModuleSlot.Gun, 0, "88-мм фугасная",
                "Штатное орудие, как в прототипе: 150 урона, радиус взрыва 5 м, перезарядка 2 с, воронка."),
            Module(ModuleIds.GunArmorPiercing, ModuleSlot.Gun, 1200, "Бронебойная",
                "250 урона, пробивает 3 цели, взрыв 2 м, перезарядка 1.6 с."),
            Module(ModuleIds.GunHowitzer, ModuleSlot.Gun, 2500, "Гаубица",
                "Стреляет навесом: 300 урона, радиус 8 м, двойная воронка, перезарядка 3 с."),

            Module(ModuleIds.MachineGun, ModuleSlot.MachineGun, 0, "Пулемёт MG",
                "Стартовое автооружие: 10 урона, выстрел раз в 0.08 с, бьёт ближайшего."),
            Module(ModuleIds.MachineGunTwin, ModuleSlot.MachineGun, 1200, "Спаренный пулемёт",
                "2 ствола, разброс 4°."),

            Module(ModuleIds.ArmorStandard, ModuleSlot.Armor, 0, "Штатная броня",
                "Без прибавки к здоровью."),
            Module(ModuleIds.ArmorSkirts, ModuleSlot.Armor, 1200, "Экраны",
                "+40 % здоровья, −8 % скорости."),
            Module(ModuleIds.ArmorDozer, ModuleSlot.Armor, 2500, "Таранный отвал",
                "Спереди −40 % урона, давка с 0.5 м/с, таран ×2."),

            Module(ModuleIds.EngineStandard, ModuleSlot.Engine, 0, "Штатный двигатель",
                "Базовая скорость и форсаж танка."),
            Module(ModuleIds.EngineBoosted, ModuleSlot.Engine, 2500, "Форсированный двигатель",
                "+20 % скорости и форсажа."),

            Module(ModuleIds.TracksStandard, ModuleSlot.Tracks, 0, "Штатная ходовая",
                "Базовая проходимость и давка."),
            Module(ModuleIds.TracksWide, ModuleSlot.Tracks, 1200, "Широкие траки",
                "Грязь не замедляет, давка шире на 30 %."),

            Module(ModuleIds.SpecialSmoke, ModuleSlot.Special, 0, "Дымовые гранатомёты",
                "Облако дыма на 12 с вокруг танка: Плевуны и элита теряют цель. Перезарядка 30 с."),
            Module(ModuleIds.SpecialRepair, ModuleSlot.Special, 1200, "Ремкомплект",
                "+30 % здоровья за 5 с. Перезарядка 60 с."),
            Module(ModuleIds.SpecialShockwave, ModuleSlot.Special, 2500, "Ударная волна",
                "Отброс и 60 урона в 8 м, оглушение элиты 1.5 с. Перезарядка 20 с."),
        };

        /// <param name="price">0: a standard module (owned, the slot's default); 1200 or 2500 marks: a bought one.</param>
        private static ModuleConfig Module(ModuleId id, ModuleSlot slot, int price, string displayName,
            string description)
        {
            // GDD §6 «Уровни модулей»: standard (base 600) 300 / 600 + 5; for 1200: 600 / 1200 + 5; for 2500: 1250 / 2500 + 10
            ModuleConfig.Cost level2;
            ModuleConfig.Cost level3;
            switch (price)
            {
                case 0:
                    level2 = new ModuleConfig.Cost(300, 0);
                    level3 = new ModuleConfig.Cost(600, 5);
                    break;
                case 1200:
                    level2 = new ModuleConfig.Cost(600, 0);
                    level3 = new ModuleConfig.Cost(1200, 5);
                    break;
                case 2500:
                    level2 = new ModuleConfig.Cost(1250, 0);
                    level3 = new ModuleConfig.Cost(2500, 10);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(price), price, $"no level costs for the module '{id}'");
            }

            bool standard = price == 0;
            bool inShop = !standard && Array.IndexOf(Milestone1Shop, id) >= 0;

            ModuleConfig module = LoadOrCreate<ModuleConfig>($"{MODULES}/{Pascal(id.Value)}ModuleConfig.asset");
            module.Configure(id, slot, standard, price, 0, level2, level3, inShop, MILESTONE_1_MODULE_LEVEL_CAP);

            var so = new SerializedObject(module);
            Set(so, "_displayName", displayName);
            Set(so, "_description", description);
            Apply(so);
            return module;
        }

        // ---------- Tanks (GDD §4, §6 «Танки») ----------

        private static List<TankConfig> BuildTanks()
        {
            var tiger = Tank(TankIds.Tiger, true, 1000f, 6f, 90f, 4, 6, 0, 0, null, "Тигр",
                "Стартовый танк. Стандарт: 4 орудия + 6 улучшений в забеге.");
            var hound = Tank(TankIds.Hound, false, 600f, 8f, 150f, 5, 6, 3000, 15, "Пройди элиту главы 1", "Гончая",
                "Лёгкий танк. +1 слот орудия в забеге; занос давит бортами; гаубицу не ставит.");
            var fortress = Tank(TankIds.Fortress, false, 1800f, 4.5f, 60f, 4, 7, 9000, 40, "Пройди босса главы 2",
                "Крепость", "Сверхтяжёлый танк. +1 слот улучшения; таранит дома насквозь; добивание элиты с 40 % здоровья.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TIGER_PREFAB_PATH);
            if (prefab != null)
            {
                var so = new SerializedObject(tiger);
                Set(so, "_prefab", prefab);
                Apply(so);
            }
            else
            {
                Debug.LogWarning($"{nameof(GameCatalogBuilder)}: no tank prefab at {TIGER_PREFAB_PATH}, the Tiger keeps its prefab");
            }

            return new List<TankConfig> { tiger, hound, fortress };
        }

        private static TankConfig Tank(TankId id, bool starting, float health, float speed, float turretSpeed,
            int weaponSlots, int upgradeSlots, int priceMarks, int priceSamples, string unlockHint, string displayName,
            string description)
        {
            TankConfig tank = LoadOrCreate<TankConfig>($"{TANKS}/{Pascal(id.Value)}TankConfig.asset");

            // Every tank starts on the standard modules, in ModuleSlot order
            tank.Configure(id, starting, weaponSlots, upgradeSlots,
                ModuleIds.Gun88He, ModuleIds.MachineGun, ModuleIds.ArmorStandard, ModuleIds.EngineStandard,
                ModuleIds.TracksStandard, ModuleIds.SpecialSmoke);

            var so = new SerializedObject(tank);
            Set(so, "_displayName", displayName);
            Set(so, "_description", description);
            Set(so, "_health", health);
            Set(so, "_speed", speed);
            Set(so, "_turretSpeed", turretSpeed);
            Set(so, "_priceMarks", priceMarks);
            Set(so, "_priceSamples", priceSamples);
            Set(so, "_unlockHint", unlockHint ?? string.Empty);
            Apply(so);
            return tank;
        }

        // ---------- Skills (GDD §3 «Навыки забега, первая версия») ----------

        private static List<SkillConfig> BuildSkills() => new()
        {
            Skill(SkillIds.Turret, SkillGroup.Weapon, "Пулемётная турель",
                "Вторая турель: 8 урона, выстрел раз в 0.12 с.", "+1 турель на 3-м и 5-м уровне."),
            Skill(SkillIds.SideFlamers, SkillGroup.Weapon, "Боковые огнемёты",
                "Конус 8 м по бортам, 20 урона/с, поджигает.", "+20 % урона и дальности."),
            Skill(SkillIds.Mortar, SkillGroup.Weapon, "Миномёт",
                "1 мина раз в 3 с по случайной толпе в 25 м: 80 урона, радиус 3 м.", "+1 мина."),
            Skill(SkillIds.Rockets, SkillGroup.Weapon, "Ракеты",
                "2 ракеты раз в 4 с по ближайшим, 60 урона.", "+1 ракета."),
            Skill(SkillIds.RearMines, SkillGroup.Weapon, "Мины позади",
                "Мина каждые 6 м пути: 100 урона, радиус 3 м.", "Интервал −1 м."),
            Skill(SkillIds.AirStrike, SkillGroup.Weapon, "Авиаудар",
                "Бомба раз в 10 с по самой густой толпе: воронка 4 м, 400 урона.", "−1 с; на 5-м уровне — 2 бомбы."),

            Skill(SkillIds.Ricochet, SkillGroup.Gun, "Рикошет",
                "Снаряд отскакивает 1 раз.", "+1 отскок."),
            Skill(SkillIds.DoubleShot, SkillGroup.Gun, "Двойной выстрел",
                "Второй снаряд через 0.2 с, 60 % урона.", "+15 % урона второму снаряду."),
            Skill(SkillIds.Piercing, SkillGroup.Gun, "Бронебойный",
                "Снаряд пробивает 2 цели до взрыва.", "+1 цель."),
            Skill(SkillIds.BigCaliber, SkillGroup.Gun, "Крупный калибр",
                "Радиус взрыва +25 %.", "+15 %."),
            Skill(SkillIds.RateOfFire, SkillGroup.Gun, "Скорострельность",
                "Перезарядка −12 %.", "−8 %."),
            Skill(SkillIds.Shrapnel, SkillGroup.Gun, "Осколки",
                "Взрыв оставляет 6 осколков по 20 урона.", "+2 осколка."),

            Skill(SkillIds.TrackSpikes, SkillGroup.Tank, "Шипы на гусеницах",
                "30 урона/с всем вплотную к бортам.", "+20 %."),
            Skill(SkillIds.Armor, SkillGroup.Tank, "Броня",
                "+15 % здоровья, лечит на прибавку.", "+15 %."),
            Skill(SkillIds.Regeneration, SkillGroup.Tank, "Регенерация",
                "0.5 % здоровья в секунду.", "+0.3 %."),
            Skill(SkillIds.Magnet, SkillGroup.Tank, "Магнит",
                "Радиус сбора опыта +50 %.", "+30 %."),
            Skill(SkillIds.Boost, SkillGroup.Tank, "Форсаж",
                "Скорость на Shift +15 %.", "+10 %."),
            Skill(SkillIds.Roller, SkillGroup.Tank, "Каток",
                "Давка с 0.8 м/с вместо 1.5.", "−0.15 м/с."),
            Skill(SkillIds.Shake, SkillGroup.Tank, "Встряска",
                "Выстрел пушки отбрасывает врагов в 4 м.", "+1 м, 20 урона."),
            Skill(SkillIds.Luck, SkillGroup.Tank, "Удача",
                "+15 % опыта.", "+10 %."),
        };

        private static SkillConfig Skill(SkillId id, SkillGroup group, string displayName, string description,
            string perLevel)
        {
            bool inPool = Array.IndexOf(Milestone1Skills, id) >= 0;

            SkillConfig skill = LoadOrCreate<SkillConfig>($"{SKILLS}/{Pascal(id.Value)}SkillConfig.asset");
            skill.Configure(id, group, inPool, displayName);

            var so = new SerializedObject(skill);
            Set(so, "_description", description);
            Set(so, "_perLevel", perLevel);
            Apply(so);
            return skill;
        }

        // ---------- Chapters and arenas (GDD §5 multipliers, §7 rewards, §10 chapters) ----------

        private static List<ChapterConfig> BuildChapters()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfig>(OUTSKIRTS_LEVEL_PATH)
                        ?? AssetDatabase.LoadAssetAtPath<LevelConfig>(FALLBACK_LEVEL_PATH);
            if (level == null)
                Debug.LogError($"{nameof(GameCatalogBuilder)}: no level at {OUTSKIRTS_LEVEL_PATH} or {FALLBACK_LEVEL_PATH}, the Outskirts have no level");

            // Milestone 1 (GDD §10 «Веха 1»): the chapter is cut to 5 arenas — 4 hordes of Crawlers and Spitters,
            // the 5th is the elite Regax instead of the boss
            ArenaConfig[] arenas =
            {
                Arena("outskirts_01", ArenaKind.Horde, "Только Ползуны, 25–35. Подсказки управления и давки."),
                Arena("outskirts_02", ArenaKind.Horde, "Только Ползуны, 25–35. Подсказки управления и давки."),
                Arena("outskirts_03", ArenaKind.Horde, "Первые Плевуны за домом. Подсказка «снеси укрытие»."),
                Arena("outskirts_04", ArenaKind.Horde, "Дом посреди арены, ворота за ним."),
                Arena("outskirts_05", ArenaKind.Elite,
                    "Первая элита: элитный Regax и малая орда. В вехе 1 — последняя арена главы вместо босса."),
            };

            ChapterConfig outskirts = Chapter(ChapterIds.Outskirts, level, 1f, 1f, 1000, 10, null, "Окраины",
                "Село начала XX века: редкие дома, дорога, грязь, поля. Учит давку и ворота.", arenas);
            ChapterConfig town = Chapter(ChapterIds.Town, null, 1.6f, 1.5f, 2000, 15, outskirts, "Городок",
                "Плотная застройка в 2–3 этажа, узкие улицы, дождь. Плевуны на крышах, дома — укрытия и препятствия, без сноса не проехать.");
            ChapterConfig quarry = Chapter(ChapterIds.Quarry, null, 2.4f, 2f, 3000, 20, town, "Карьер",
                "Рельеф с перепадами, грязь, воронки, ночь и дым. Мобильность, обрывы, болота.");

            return new List<ChapterConfig> { outskirts, town, quarry };
        }

        private static ChapterConfig Chapter(ChapterId id, LevelConfig level, float enemyMultiplier,
            float rewardMultiplier, int firstMarks, int firstSamples, ChapterConfig required, string displayName,
            string description, params ArenaConfig[] arenas)
        {
            ChapterConfig chapter = LoadOrCreate<ChapterConfig>($"{CHAPTERS}/{Pascal(id.Value)}ChapterConfig.asset");
            chapter.Configure(id, level, enemyMultiplier, rewardMultiplier, firstMarks, firstSamples, required, arenas);

            var so = new SerializedObject(chapter);
            Set(so, "_displayName", displayName);
            Set(so, "_description", description);
            Apply(so);
            return chapter;
        }

        private static ArenaConfig Arena(string id, ArenaKind kind, string note)
        {
            ArenaConfig arena = LoadOrCreate<ArenaConfig>($"{ARENAS}/{Pascal(id)}ArenaConfig.asset");
            arena.Configure(new ArenaId(id), kind);

            var so = new SerializedObject(arena);
            Set(so, "_note", note);
            Apply(so);
            return arena;
        }

        // ---------- The App prefab ----------

        private static bool AssignToAppPrefab(GameCatalog catalog)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(APP_PREFAB_PATH);
            try
            {
                var installer = root.GetComponentInChildren<AppInstaller>(true);
                if (installer == null)
                {
                    Debug.LogError($"{nameof(GameCatalogBuilder)}: no {nameof(AppInstaller)} on {APP_PREFAB_PATH}");
                    return false;
                }

                var so = new SerializedObject(installer);
                SerializedProperty property = Find(so, "_gameCatalog");
                if (property.objectReferenceValue == catalog)
                    return true;

                property.objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, APP_PREFAB_PATH, out bool saved);
                if (!saved)
                    Debug.LogError($"{nameof(GameCatalogBuilder)}: {APP_PREFAB_PATH} was not saved");
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- Helpers ----------

        // Loads the asset by its path, so a rebuild keeps its GUID; creates it when there is none
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException($"{path} is not a {typeof(T).Name}; move it away and rebuild");

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        // "gun_88_he" → "Gun88He"
        private static string Pascal(string id)
        {
            var builder = new StringBuilder(id.Length);
            foreach (string part in id.Split('_'))
            {
                if (part.Length == 0)
                    continue;

                builder.Append(char.ToUpperInvariant(part[0]));
                builder.Append(part, 1, part.Length - 1);
            }

            return builder.ToString();
        }

        private static SerializedProperty Find(SerializedObject so, string name) =>
            so.FindProperty(name) ?? throw new InvalidOperationException(
                $"{so.targetObject.GetType().Name} has no serialized field '{name}'");

        private static void Set(SerializedObject so, string name, string value) => Find(so, name).stringValue = value;

        private static void Set(SerializedObject so, string name, float value) => Find(so, name).floatValue = value;

        private static void Set(SerializedObject so, string name, int value) => Find(so, name).intValue = value;

        private static void Set(SerializedObject so, string name, Object value) =>
            Find(so, name).objectReferenceValue = value;

        private static void Apply(SerializedObject so)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so.targetObject);
        }
    }
}

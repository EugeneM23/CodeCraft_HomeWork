using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class StubHangarSourceTests
    {
        private readonly List<Object> _created = new();
        private HangarStubConfig _config;
        private string _path;

        private static string Gun => ModuleIds.Gun88He.Value;
        private static string Ap => ModuleIds.GunArmorPiercing.Value;
        private static string Howitzer => ModuleIds.GunHowitzer.Value;
        private static string Armor => ModuleIds.ArmorStandard.Value;
        private static string Skirts => ModuleIds.ArmorSkirts.Value;
        private static string GunSlot => HangarSlots.Id(ModuleSlot.Gun);
        private static string ArmorSlot => HangarSlots.Id(ModuleSlot.Armor);

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "StubHangarSourceTests_" + System.Guid.NewGuid().ToString("N") + ".json");

            ModuleConfig.Cost standard2 = new(300, 0), standard3 = new(600, 5);
            ModuleConfig.Cost cheap2 = new(600, 0), cheap3 = new(1200, 5);
            ModuleConfig gun = Module(ModuleIds.Gun88He, ModuleSlot.Gun, true, 0, standard2, standard3, false);
            ModuleConfig ap = Module(ModuleIds.GunArmorPiercing, ModuleSlot.Gun, false, 1200, cheap2, cheap3, true);
            ModuleConfig howitzer = Module(ModuleIds.GunHowitzer, ModuleSlot.Gun, false, 2500, cheap2, cheap3, false);
            ModuleConfig armor = Module(ModuleIds.ArmorStandard, ModuleSlot.Armor, true, 0, standard2, standard3, false);
            ModuleConfig skirts = Module(ModuleIds.ArmorSkirts, ModuleSlot.Armor, false, 1200, cheap2, cheap3, true);

            TankConfig tiger = Create<TankConfig>();
            tiger.Configure(TankIds.Tiger, true, 4, 6, ModuleIds.Gun88He, default, ModuleIds.ArmorStandard);

            ChapterConfig outskirts = Create<ChapterConfig>();
            outskirts.Configure(ChapterIds.Outskirts, null, 1f, 1f, 1000, 10, null,
                Create<ArenaConfig>(), Create<ArenaConfig>(), Create<ArenaConfig>(), Create<ArenaConfig>(), Create<ArenaConfig>());
            ChapterConfig town = Create<ChapterConfig>();
            town.Configure(ChapterIds.Town, null, 1.6f, 1.5f, 2000, 15, outskirts);

            GameCatalog catalog = Create<GameCatalog>();
            catalog.Configure(new[] { tiger }, new[] { gun, ap, howitzer, armor, skirts }, new SkillConfig[0],
                new[] { outskirts, town });

            _config = Create<HangarStubConfig>();
            _config.catalog = catalog;
            _config.startTank = TankIds.Tiger.Value;
            _config.startMarks = 2000;
            _config.startSamples = 4;
            _config.cannonDamage = 150f;
            _config.cannonReload = 2f;
            _config.recommendedTanks = new List<string> { "tiger", "hound" };
            _config.moduleStats = new List<HangarModuleStats>
            {
                new() { moduleId = Gun, levelPercent = new HangarTankStats(0, 0, 10, 0) },
                new() { moduleId = Ap, bonus = new HangarTankStats(0, 0, 100, -0.4f) },
                new() { moduleId = Skirts, percent = new HangarTankStats(40, -8, 0, 0) }
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
                Object.DestroyImmediate(item);
            _created.Clear();
            if (File.Exists(_path))
                File.Delete(_path);
        }

        [Test]
        public void NewSource_OwnsAndInstallsTheStandardModules_WithTheStartBalance_AndNoSave()
        {
            StubHangarSource source = New();

            Assert.AreEqual(2000, source.Marks);
            Assert.AreEqual(4, source.Samples);
            Assert.IsFalse(source.HasSave);
            Assert.AreEqual(Gun, source.InstalledIn(GunSlot));
            Assert.AreEqual(Armor, source.InstalledIn(ArmorSlot));
            Assert.AreEqual(1, source.Module(Gun).Level);
            Assert.IsFalse(source.Module(Ap).Owned);
            Assert.AreEqual(6, source.Slots.Count);
        }

        [Test]
        public void ModulesFor_ListsOwnedForSaleAndLocked_WithTheLockKey()
        {
            StubHangarSource source = New();

            IReadOnlyList<HangarModule> guns = source.ModulesFor(GunSlot);

            Assert.AreEqual(3, guns.Count);
            HangarModule howitzer = source.Module(Howitzer);
            Assert.IsTrue(howitzer.Locked);
            Assert.AreEqual("module.gun_howitzer.lock", howitzer.LockReasonKey);
            Assert.IsFalse(source.Module(Ap).Locked);
        }

        [Test]
        public void TryBuy_PaysMarks_OwnsAtLevel1_KeepsTheInstalledModule_AndSaves()
        {
            StubHangarSource source = New();
            int changes = 0;
            source.Changed += () => changes++;

            Assert.AreEqual(PurchaseResult.Done, source.TryBuy(Ap));

            Assert.AreEqual(800, source.Marks);
            Assert.AreEqual(1, source.Module(Ap).Level);
            Assert.AreEqual(Gun, source.InstalledIn(GunSlot));
            Assert.AreEqual(1, changes);
            Assert.IsTrue(source.HasSave);
            Assert.AreEqual(PurchaseResult.AlreadyOwned, source.TryBuy(Ap));
        }

        [Test]
        public void TryBuy_ShortOfMarks_Or_Locked_Or_Unknown_ChangesNothing()
        {
            _config.startMarks = 1000;
            StubHangarSource source = New();

            Assert.AreEqual(PurchaseResult.NotEnoughMarks, source.TryBuy(Ap));
            Assert.AreEqual(PurchaseResult.Locked, source.TryBuy(Howitzer));
            Assert.AreEqual(PurchaseResult.Unknown, source.TryBuy("no_such_module"));
            Assert.AreEqual(1000, source.Marks);
            Assert.IsFalse(source.HasSave);
        }

        [Test]
        public void TryUpgrade_Level2PaysMarks_Level3PaysMarksAndSamples_ThenMaxLevel()
        {
            _config.startSamples = 5;
            StubHangarSource source = New();

            Assert.AreEqual(300, source.Module(Gun).UpgradeMarks);
            Assert.AreEqual(PurchaseResult.Done, source.TryUpgrade(Gun));
            Assert.AreEqual(1700, source.Marks);
            Assert.AreEqual(600, source.Module(Gun).UpgradeMarks);
            Assert.AreEqual(5, source.Module(Gun).UpgradeSamples);

            Assert.AreEqual(PurchaseResult.Done, source.TryUpgrade(Gun));
            Assert.AreEqual(1100, source.Marks);
            Assert.AreEqual(0, source.Samples);
            Assert.AreEqual(3, source.Module(Gun).Level);
            Assert.AreEqual(-1, source.Module(Gun).UpgradeMarks);
            Assert.AreEqual(PurchaseResult.MaxLevel, source.TryUpgrade(Gun));
        }

        [Test]
        public void TryUpgrade_ShortOfSamples_Or_NotOwned_ChangesNothing()
        {
            StubHangarSource source = New();
            source.TryUpgrade(Gun);
            int marks = source.Marks;

            Assert.AreEqual(PurchaseResult.NotEnoughSamples, source.TryUpgrade(Gun), "level 3 wants 5 samples, 4 owned");
            Assert.AreEqual(PurchaseResult.NotOwned, source.TryUpgrade(Ap));
            Assert.AreEqual(marks, source.Marks);
            Assert.AreEqual(2, source.Module(Gun).Level);
        }

        [Test]
        public void TryInstall_OnlyOwned_ReplacesTheSlotsModule_AndChangesTheStats()
        {
            StubHangarSource source = New();
            Assert.AreEqual(PurchaseResult.NotOwned, source.TryInstall(Skirts));
            source.TryBuy(Skirts);

            Assert.AreEqual(PurchaseResult.Done, source.TryInstall(Skirts));

            Assert.AreEqual(Skirts, source.InstalledIn(ArmorSlot));
            HangarTankStats stats = source.Stats();
            Assert.AreEqual(1400f, stats.health, 0.01f);
            Assert.AreEqual(6f * 0.92f, stats.speed, 0.01f);
        }

        [Test]
        public void Stats_BaseFromTheTankAndTheGun_LevelsAddPercent_ReplacementPreviewsWithoutInstalling()
        {
            StubHangarSource source = New();
            HangarTankStats start = source.Stats();
            Assert.AreEqual(1000f, start.health, 0.01f);
            Assert.AreEqual(6f, start.speed, 0.01f);
            Assert.AreEqual(150f, start.cannonDamage, 0.01f);
            Assert.AreEqual(2f, start.cannonReload, 0.01f);

            HangarTankStats preview = source.Stats(Ap);
            Assert.AreEqual(250f, preview.cannonDamage, 0.01f);
            Assert.AreEqual(1.6f, preview.cannonReload, 0.01f);
            Assert.AreEqual(Gun, source.InstalledIn(GunSlot));

            source.TryUpgrade(Gun);
            Assert.AreEqual(165f, source.Stats().cannonDamage, 0.01f);
        }

        [Test]
        public void Progress_IsReadBackFromTheFile()
        {
            StubHangarSource source = New();
            source.TryBuy(Skirts);
            source.TryInstall(Skirts);

            StubHangarSource again = New();

            Assert.AreEqual(source.Marks, again.Marks);
            Assert.IsTrue(again.Module(Skirts).Owned);
            Assert.AreEqual(Skirts, again.InstalledIn(ArmorSlot));
            Assert.IsTrue(again.HasSave);
        }

        [Test]
        public void NewGame_WipesTheProgress_AndStartsASavedNewGame()
        {
            StubHangarSource source = New();
            source.TryBuy(Ap);

            source.NewGame();

            Assert.AreEqual(2000, source.Marks);
            Assert.IsFalse(source.Module(Ap).Owned);
            Assert.AreEqual(Gun, source.InstalledIn(GunSlot));
            Assert.IsTrue(source.HasSave);
            Assert.IsFalse(New().Module(Ap).Owned);
        }

        [Test]
        public void Chapters_FirstIsOpen_NextWaitsForIt_WithRewardAndRecommendedTank()
        {
            StubHangarSource source = New();

            IReadOnlyList<HangarChapter> chapters = source.Chapters;

            Assert.AreEqual(2, chapters.Count);
            Assert.IsTrue(chapters[0].Unlocked);
            Assert.AreEqual(5, chapters[0].ArenaCount);
            Assert.AreEqual(1000, chapters[0].FirstRewardMarks);
            Assert.AreEqual(10, chapters[0].FirstRewardSamples);
            Assert.AreEqual("tiger", chapters[0].RecommendedTankId);
            Assert.IsFalse(chapters[1].Unlocked);
            Assert.AreEqual("hound", chapters[1].RecommendedTankId);
        }

        private StubHangarSource New() => new(_config, _path, null);

        private ModuleConfig Module(ModuleId id, ModuleSlot slot, bool standard, int price, ModuleConfig.Cost level2,
            ModuleConfig.Cost level3, bool inShop)
        {
            ModuleConfig module = Create<ModuleConfig>();
            module.Configure(id, slot, standard, price, 0, level2, level3, inShop);
            return module;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T item = ScriptableObject.CreateInstance<T>();
            _created.Add(item);
            return item;
        }
    }
}

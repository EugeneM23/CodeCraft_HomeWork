using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class MetaProgressHangarSourceTests
    {
        private readonly List<Object> _created = new();
        private FakeMetaProgress _meta;
        private FakeFlow _flow;
        private GameCatalog _catalog;
        private MetaProgressHangarSource _source;

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
            ModuleConfig.Cost standard2 = new(300, 0), standard3 = new(600, 5);
            ModuleConfig.Cost cheap2 = new(600, 0), cheap3 = new(1200, 5);
            ModuleConfig gun = Module(ModuleIds.Gun88He, ModuleSlot.Gun, true, 0, standard2, standard3, false);
            ModuleConfig ap = Module(ModuleIds.GunArmorPiercing, ModuleSlot.Gun, false, 1200, cheap2, cheap3, true);
            ModuleConfig howitzer = Module(ModuleIds.GunHowitzer, ModuleSlot.Gun, false, 2500, cheap2, cheap3, false);
            ModuleConfig armor = Module(ModuleIds.ArmorStandard, ModuleSlot.Armor, true, 0, standard2, standard3, false);
            ModuleConfig skirts = Module(ModuleIds.ArmorSkirts, ModuleSlot.Armor, false, 1200, cheap2, cheap3, true);

            TankConfig tiger = Create<TankConfig>();
            tiger.Configure(TankIds.Tiger, true, 4, 6, ModuleIds.Gun88He, default, ModuleIds.ArmorStandard);

            LevelConfig level = Create<LevelConfig>();
            level.Configure("level_outskirts", null, 0);
            ChapterConfig outskirts = Create<ChapterConfig>();
            outskirts.Configure(ChapterIds.Outskirts, level, 1f, 1f, 1000, 10, null,
                Create<ArenaConfig>(), Create<ArenaConfig>(), Create<ArenaConfig>(), Create<ArenaConfig>(), Create<ArenaConfig>());
            ChapterConfig town = Create<ChapterConfig>();
            town.Configure(ChapterIds.Town, level, 1.6f, 1.5f, 2000, 15, outskirts, Create<ArenaConfig>());

            _catalog = Create<GameCatalog>();
            _catalog.Configure(new[] { tiger }, new[] { gun, ap, howitzer, armor, skirts }, new SkillConfig[0],
                new[] { outskirts, town });

            _meta = new FakeMetaProgress { Marks = 2000, Samples = 4 };
            _meta.Levels[ModuleIds.Gun88He] = 2;
            _meta.Levels[ModuleIds.ArmorStandard] = 1;
            _meta.Installed[(TankIds.Tiger, ModuleSlot.Gun)] = ModuleIds.Gun88He;
            _meta.Installed[(TankIds.Tiger, ModuleSlot.Armor)] = ModuleIds.ArmorStandard;
            _meta.LockedModules.Add(ModuleIds.GunHowitzer);

            _flow = new FakeFlow();
            _source = new MetaProgressHangarSource(_meta, _catalog, _flow);
        }

        [TearDown]
        public void TearDown()
        {
            _source.Dispose();
            foreach (Object item in _created)
                Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void ReadsMoneySaveTankAndLoadout_FromTheProgress()
        {
            _meta.HasSave = true;

            Assert.AreEqual(2000, _source.Marks);
            Assert.AreEqual(4, _source.Samples);
            Assert.IsTrue(_source.HasSave);
            Assert.AreEqual(TankIds.Tiger.Value, _source.SelectedTankId);
            Assert.AreEqual(Gun, _source.InstalledIn(GunSlot));
            Assert.AreEqual(Armor, _source.InstalledIn(ArmorSlot));
            Assert.IsNull(_source.InstalledIn(HangarSlots.Id(ModuleSlot.Engine)));
            Assert.IsNull(_source.InstalledIn("no_such_slot"));
        }

        [Test]
        public void Slots_AreTheSixModuleSlots_InTheirOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "Gun", "MachineGun", "Armor", "Engine", "Tracks", "Special" }, _source.Slots);
        }

        [Test]
        public void ModulesFor_ListsTheSlotsModules_WithLevelsPricesAndTheLockKey()
        {
            IReadOnlyList<HangarModule> guns = _source.ModulesFor(GunSlot);

            Assert.AreEqual(3, guns.Count);
            Assert.AreEqual(Gun, guns[0].Id);
            Assert.IsTrue(guns[0].Owned);
            Assert.AreEqual(2, guns[0].Level);
            Assert.AreEqual(600, guns[0].UpgradeMarks, "level 3 price");
            Assert.AreEqual(5, guns[0].UpgradeSamples);
            Assert.IsFalse(guns[1].Owned);
            Assert.IsFalse(guns[1].Locked);
            Assert.AreEqual(1200, guns[1].PriceMarks);
            Assert.AreEqual(-1, guns[1].UpgradeMarks);
            Assert.IsTrue(guns[2].Locked);
            Assert.AreEqual("module.gun_howitzer.lock", guns[2].LockReasonKey);
            Assert.AreEqual(0, _source.ModulesFor("no_such_slot").Count);
            Assert.IsFalse(_source.Module("no_such_module").IsValid);
        }

        [Test]
        public void BuyAndUpgrade_PassTheProgressAnswersThrough()
        {
            _meta.BuyResult = PurchaseResult.NotEnoughMarks;
            Assert.AreEqual(PurchaseResult.NotEnoughMarks, _source.CanBuy(Ap));
            Assert.AreEqual(PurchaseResult.NotEnoughMarks, _source.TryBuy(Ap));
            Assert.AreEqual(PurchaseResult.Locked, _source.TryBuy(Howitzer));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, _source.TryBuy(Gun));

            _meta.UpgradeResult = PurchaseResult.NotEnoughSamples;
            Assert.AreEqual(PurchaseResult.NotEnoughSamples, _source.CanUpgrade(Gun));
            Assert.AreEqual(PurchaseResult.NotEnoughSamples, _source.TryUpgrade(Gun));
            Assert.AreEqual(PurchaseResult.NotOwned, _source.TryUpgrade(Ap));
            Assert.AreEqual(2, _meta.UpgradeCalls);
        }

        [Test]
        public void UnknownIds_AreUnknown_WithoutAskingTheProgress()
        {
            Assert.AreEqual(PurchaseResult.Unknown, _source.TryBuy("no_such_module"));
            Assert.AreEqual(PurchaseResult.Unknown, _source.TryUpgrade(null));
            Assert.AreEqual(PurchaseResult.Unknown, _source.TryInstall("no_such_module"));
            Assert.AreEqual(0, _meta.BuyCalls);
            Assert.AreEqual(0, _meta.UpgradeCalls);
            Assert.AreEqual(0, _meta.InstallCalls);
        }

        [Test]
        public void Buy_RaisesChanged_AndTheListsShowTheNewLevel()
        {
            Assert.IsFalse(_source.ModulesFor(GunSlot)[1].Owned);
            int changes = 0;
            _source.Changed += () => changes++;

            Assert.AreEqual(PurchaseResult.Done, _source.TryBuy(Ap));

            Assert.AreEqual(1, changes);
            Assert.IsTrue(_source.ModulesFor(GunSlot)[1].Owned);
            Assert.AreEqual(1, _source.Module(Ap).Level);
        }

        [Test]
        public void TryInstall_OnlyOwned_IntoTheSelectedTanksSlot()
        {
            Assert.AreEqual(PurchaseResult.NotOwned, _source.TryInstall(Skirts));
            Assert.AreEqual(PurchaseResult.Done, _source.TryInstall(Armor), "already installed");
            Assert.AreEqual(0, _meta.InstallCalls);

            _source.TryBuy(Skirts);
            Assert.AreEqual(PurchaseResult.Done, _source.TryInstall(Skirts));

            Assert.AreEqual(Skirts, _source.InstalledIn(ArmorSlot));
            Assert.AreEqual(1, _meta.InstallCalls);
        }

        [Test]
        public void TryInstall_OwnedButRefused_IsUnknown_NotNotOwned()
        {
            _source.TryBuy(Skirts);
            _meta.InstallFails = true;

            Assert.AreEqual(PurchaseResult.Unknown, _source.TryInstall(Skirts));
            Assert.AreEqual(1, _meta.InstallCalls);
        }

        [Test]
        public void TryBuy_DoesNotInstall()
        {
            Assert.AreEqual(PurchaseResult.Done, _source.TryBuy(Skirts));

            Assert.AreEqual(0, _meta.InstallCalls);
        }

        [Test]
        public void Stats_AreTheTanksNumbers_AndTheReplacementTakesItsSlot_AtItsLevelOr1()
        {
            HangarTankStats stats = _source.Stats();
            Assert.AreEqual(1000f, stats.health, 0.01f);
            Assert.AreEqual(6f, stats.speed, 0.01f);

            List<ModuleLevel> loadout = new();
            MetaProgressHangarSource.FillLoadout(_meta, TankIds.Tiger, _catalog.FindModule(ModuleIds.GunArmorPiercing), loadout);
            Assert.AreEqual(2, loadout.Count);
            Assert.AreEqual(ModuleIds.GunArmorPiercing, loadout[0].Id);
            Assert.AreEqual(1, loadout[0].Level, "not owned: level 1");
            Assert.AreEqual(ModuleIds.ArmorStandard, loadout[1].Id);

            MetaProgressHangarSource.FillLoadout(_meta, TankIds.Tiger, null, loadout);
            Assert.AreEqual(ModuleIds.Gun88He, loadout[0].Id);
            Assert.AreEqual(2, loadout[0].Level);

            _source.Stats(Ap);
            Assert.AreEqual(Gun, _source.InstalledIn(GunSlot), "a preview installs nothing");
            Assert.AreEqual(0, _meta.InstallCalls);
        }

        [Test]
        public void Chapters_MapTheCatalogAndTheSave()
        {
            _meta.Chapters[ChapterIds.Outskirts] = new ChapterProgress(true, true, 5, 812.5f);

            IReadOnlyList<HangarChapter> chapters = _source.Chapters;

            Assert.AreEqual(2, chapters.Count);
            Assert.AreEqual(ChapterIds.Outskirts.Value, chapters[0].Id);
            Assert.IsTrue(chapters[0].Unlocked);
            Assert.IsTrue(chapters[0].Completed);
            Assert.AreEqual(5, chapters[0].BestArena);
            Assert.AreEqual(5, chapters[0].ArenaCount);
            Assert.AreEqual(812.5f, chapters[0].BestTime, 0.001f);
            Assert.AreEqual(1000, chapters[0].FirstRewardMarks);
            Assert.AreEqual(10, chapters[0].FirstRewardSamples);
            Assert.IsNull(chapters[0].RecommendedTankId, "none set in the catalog");
            Assert.IsFalse(chapters[1].Unlocked, "the save has not opened it");
            Assert.AreEqual(2000, chapters[1].FirstRewardMarks);
        }

        [Test]
        public void Chapters_AreRebuiltWhenTheProgressChanges()
        {
            Assert.IsFalse(_source.Chapters[1].Unlocked);

            _meta.Chapters[ChapterIds.Town] = new ChapterProgress(true, false, 0, 0f);
            _meta.NewGame(); // any change raises Changed

            Assert.IsTrue(_source.Chapters[1].Unlocked);
        }

        [Test]
        public void StartChapter_SavesAndStartsTheChaptersLevel_OnlyWhenUnlocked()
        {
            _source.StartChapter(ChapterIds.Town.Value);
            Assert.IsNull(_flow.Started);
            Assert.AreEqual(0, _meta.SaveCalls);

            _source.StartChapter(ChapterIds.Outskirts.Value);
            Assert.AreEqual("level_outskirts", _flow.Started);
            Assert.AreEqual(1, _meta.SaveCalls);
        }

        [Test]
        public void StartChapter_AFlowWithChapters_GetsTheChapter_NotAPlainLevel()
        {
            _flow.TakesChapters = true;

            _source.StartChapter(ChapterIds.Outskirts.Value);

            Assert.AreEqual(ChapterIds.Outskirts, _flow.StartedChapter);
            Assert.IsNull(_flow.Started, "the level comes with the chapter");
            Assert.AreEqual(1, _meta.SaveCalls);
        }

        [Test]
        public void NewGame_GoesToTheProgress_AndDisposeStopsChanged()
        {
            int changes = 0;
            _source.Changed += () => changes++;

            _source.NewGame();
            Assert.AreEqual(1, _meta.NewGameCalls);
            Assert.AreEqual(1, changes);

            _source.Dispose();
            _meta.NewGame();
            Assert.AreEqual(1, changes);
        }

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

        private sealed class FakeFlow : IGameFlow
        {
            public string Started;
            public ChapterId StartedChapter;
            /// <summary>Like the core's GameFlow; false — a stand-in that refuses chapters.</summary>
            public bool TakesChapters;

            public event Action<GameState> StateChanged { add { } remove { } }

            public bool StartChapter(ChapterId chapter)
            {
                if (!TakesChapters)
                    return false;
                StartedChapter = chapter;
                return true;
            }

            public GameState State => GameState.MainMenu;
            public bool IsBusy => false;
            public string CurrentLevelId => null;
            public string RequestedMenuScreen => null;

            public bool StartLevel(string levelId)
            {
                Started = levelId;
                return true;
            }

            public bool RestartLevel() => false;
            public bool ReturnToMenu(string screen = null) => false;

            public void Quit()
            {
            }
        }
    }
}

using System.Text.RegularExpressions;
using NUnit.Framework;
using Saving;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class MetaProgressTests
    {
        private MetaTestCatalog _catalog;
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _catalog = new MetaTestCatalog();
            _storage = new InMemorySaveStorage();
        }

        [TearDown]
        public void TearDown() => _catalog.Dispose();

        private MetaProgress Loaded()
        {
            var progress = new MetaProgress(_storage, "1.0", _catalog.Catalog);
            progress.Load();
            return progress;
        }

        private MetaProgress WithMoney(int marks, int samples = 0)
        {
            MetaProgress progress = Loaded();
            progress.ApplyRun(new RunResult { outcome = RunOutcome.Defeat, marks = marks, samples = samples });
            return progress;
        }

        private bool Written => _storage.Has(MetaProgress.SLOT, SaveFiles.SAVE);

        // ---------- New game ----------

        [Test]
        public void NewGame_StartsWithTheTigerStandardModulesAndTheFirstChapter()
        {
            MetaProgress progress = Loaded();

            Assert.IsFalse(progress.HasSave);
            Assert.AreEqual(0, progress.Marks);
            Assert.AreEqual(0, progress.Samples);
            Assert.AreEqual(TankIds.Tiger, progress.SelectedTank);
            Assert.IsTrue(progress.OwnsTank(TankIds.Tiger));
            Assert.IsFalse(progress.OwnsTank(TankIds.Hound));

            Assert.IsTrue(progress.OwnsModule(ModuleIds.Gun88He));
            Assert.AreEqual(1, progress.GetModuleLevel(ModuleIds.SpecialSmoke));
            Assert.IsFalse(progress.OwnsModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(0, progress.GetModuleLevel(ModuleIds.GunArmorPiercing));

            Assert.AreEqual(ModuleIds.Gun88He, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Gun));
            Assert.AreEqual(ModuleIds.TracksStandard, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Tracks));

            Assert.IsTrue(progress.GetChapter(ChapterIds.Outskirts).Unlocked);
            Assert.IsFalse(progress.GetChapter(ChapterIds.Outskirts).Completed);
            Assert.IsFalse(progress.GetChapter(ChapterIds.Town).Unlocked);
            Assert.AreEqual(0, progress.Stats.Runs);
            Assert.IsFalse(Written, "Loading a new game writes nothing.");
        }

        [Test]
        public void NewGame_ResetsEverything_AndSavesAtOnce()
        {
            MetaProgress progress = WithMoney(5000);
            progress.BuyModule(ModuleIds.ArmorSkirts);
            int changes = 0;
            progress.Changed += () => changes++;

            progress.NewGame();

            Assert.AreEqual(1, changes);
            Assert.AreEqual(0, progress.Marks);
            Assert.IsFalse(progress.OwnsModule(ModuleIds.ArmorSkirts));
            Assert.IsTrue(progress.HasSave);
            Assert.AreEqual(0, Loaded().Marks);
        }

        // ---------- Modules ----------

        [Test]
        public void BuyModule_TakesTheMarks_AndSavesAtOnce()
        {
            MetaProgress progress = WithMoney(1500);
            int changes = 0;
            progress.Changed += () => changes++;

            Assert.AreEqual(PurchaseResult.Done, progress.CanBuyModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(PurchaseResult.Done, progress.BuyModule(ModuleIds.GunArmorPiercing));

            Assert.AreEqual(300, progress.Marks);
            Assert.AreEqual(1, progress.GetModuleLevel(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(1, changes);
            Assert.IsFalse(progress.IsDirty);

            MetaProgress read = Loaded();
            Assert.AreEqual(300, read.Marks);
            Assert.IsTrue(read.OwnsModule(ModuleIds.GunArmorPiercing));
        }

        [Test]
        public void BuyModule_RefusedCases_ChangeNothing()
        {
            MetaProgress progress = WithMoney(1000);
            int changes = 0;
            progress.Changed += () => changes++;

            Assert.AreEqual(PurchaseResult.NotEnoughMarks, progress.BuyModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, progress.BuyModule(ModuleIds.Gun88He));
            Assert.AreEqual(PurchaseResult.Locked, progress.BuyModule(ModuleIds.GunHowitzer));
            Assert.AreEqual(PurchaseResult.Unknown, progress.BuyModule(new ModuleId("no_such_module")));
            Assert.AreEqual(PurchaseResult.Unknown, progress.BuyModule(default));

            Assert.AreEqual(1000, progress.Marks);
            Assert.IsFalse(progress.OwnsModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void UpgradeModule_ToLevel2_ThenTheCapStopsIt()
        {
            MetaProgress progress = WithMoney(MetaTestCatalog.AP_PRICE + 3000);
            progress.BuyModule(ModuleIds.GunArmorPiercing);

            Assert.AreEqual(PurchaseResult.Done, progress.UpgradeModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(2, progress.GetModuleLevel(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(3000 - 600, progress.Marks);

            // The AP gun is capped at level 2 in this milestone
            Assert.AreEqual(PurchaseResult.MaxLevel, progress.CanUpgradeModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(PurchaseResult.MaxLevel, progress.UpgradeModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(2, Loaded().GetModuleLevel(ModuleIds.GunArmorPiercing));
        }

        [Test]
        public void UpgradeModule_Level3_NeedsSamples_AndStopsAtThree()
        {
            MetaProgress progress = WithMoney(900);
            Assert.AreEqual(PurchaseResult.Done, progress.UpgradeModule(ModuleIds.Gun88He));
            Assert.AreEqual(600, progress.Marks);

            Assert.AreEqual(PurchaseResult.NotEnoughSamples, progress.UpgradeModule(ModuleIds.Gun88He));
            Assert.AreEqual(2, progress.GetModuleLevel(ModuleIds.Gun88He));

            progress.ApplyRun(new RunResult { samples = 5 });
            Assert.AreEqual(PurchaseResult.Done, progress.UpgradeModule(ModuleIds.Gun88He));
            Assert.AreEqual(3, progress.GetModuleLevel(ModuleIds.Gun88He));
            Assert.AreEqual(0, progress.Marks);
            Assert.AreEqual(0, progress.Samples);
            Assert.AreEqual(PurchaseResult.MaxLevel, progress.CanUpgradeModule(ModuleIds.Gun88He));
        }

        [Test]
        public void UpgradeModule_NotOwnedOrUnknown_IsRefused()
        {
            MetaProgress progress = WithMoney(5000);

            Assert.AreEqual(PurchaseResult.NotOwned, progress.UpgradeModule(ModuleIds.ArmorSkirts));
            Assert.AreEqual(PurchaseResult.Unknown, progress.UpgradeModule(new ModuleId("no_such_module")));
            Assert.AreEqual(5000, progress.Marks);
        }

        // ---------- Tanks ----------

        [Test]
        public void BuyTank_NeedsMarksAndSamples_StartingAndUnpricedAreNotSold()
        {
            MetaProgress progress = WithMoney(MetaTestCatalog.HOUND_MARKS, MetaTestCatalog.HOUND_SAMPLES - 1);

            Assert.AreEqual(PurchaseResult.NotEnoughSamples, progress.CanBuyTank(TankIds.Hound));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, progress.CanBuyTank(TankIds.Tiger));
            Assert.AreEqual(PurchaseResult.Locked, progress.CanBuyTank(TankIds.Fortress));
            Assert.AreEqual(PurchaseResult.Unknown, progress.CanBuyTank(new TankId("no_such_tank")));

            progress.ApplyRun(new RunResult { samples = 1 });
            Assert.AreEqual(PurchaseResult.Done, progress.BuyTank(TankIds.Hound));
            Assert.AreEqual(0, progress.Marks);
            Assert.AreEqual(0, progress.Samples);
            Assert.IsTrue(Loaded().OwnsTank(TankIds.Hound));
            Assert.AreEqual(ModuleIds.Gun88He, progress.GetInstalled(TankIds.Hound, ModuleSlot.Gun));
        }

        [Test]
        public void SelectTank_OnlyAnOwnedOne()
        {
            MetaProgress progress = WithMoney(MetaTestCatalog.HOUND_MARKS, MetaTestCatalog.HOUND_SAMPLES);

            Assert.IsFalse(progress.SelectTank(TankIds.Hound));
            Assert.AreEqual(TankIds.Tiger, progress.SelectedTank);

            progress.BuyTank(TankIds.Hound);
            Assert.IsTrue(progress.SelectTank(TankIds.Hound));
            Assert.AreEqual(TankIds.Hound, progress.SelectedTank);
            Assert.AreEqual(TankIds.Hound, Loaded().SelectedTank);
        }

        // ---------- Install ----------

        [Test]
        public void Install_OnlyAnOwnedModuleOfThatSlot_IntoAnOwnedTank()
        {
            MetaProgress progress = WithMoney(MetaTestCatalog.AP_PRICE);

            Assert.IsFalse(progress.Install(TankIds.Tiger, ModuleSlot.Gun, ModuleIds.GunArmorPiercing), "not owned");
            progress.BuyModule(ModuleIds.GunArmorPiercing);
            Assert.IsFalse(progress.Install(TankIds.Tiger, ModuleSlot.Armor, ModuleIds.GunArmorPiercing), "wrong slot");
            Assert.IsFalse(progress.Install(TankIds.Hound, ModuleSlot.Gun, ModuleIds.GunArmorPiercing), "tank not owned");
            Assert.IsFalse(progress.Install(TankIds.Tiger, ModuleSlot.Gun, new ModuleId("no_such_module")));
            Assert.AreEqual(ModuleIds.Gun88He, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Gun));

            Assert.IsTrue(progress.Install(TankIds.Tiger, ModuleSlot.Gun, ModuleIds.GunArmorPiercing));
            Assert.AreEqual(ModuleIds.GunArmorPiercing, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Gun));
            Assert.AreEqual(ModuleIds.GunArmorPiercing, Loaded().GetInstalled(TankIds.Tiger, ModuleSlot.Gun));
        }

        // ---------- Runs ----------

        [Test]
        public void ApplyRun_Defeat_KeepsTheLoot_AndTheBestArena_WithoutCompletion()
        {
            MetaProgress progress = Loaded();

            progress.ApplyRun(new RunResult
            {
                outcome = RunOutcome.Defeat, chapter = ChapterIds.Outskirts, arenaReached = 3, timeSec = 200f,
                kills = 50, crushes = 4, elitesKilled = 1, marks = 700, samples = 2
            });
            progress.ApplyRun(new RunResult
            {
                outcome = RunOutcome.Abandoned, chapter = ChapterIds.Outskirts, arenaReached = 2, timeSec = 100f,
                kills = 10, marks = 100
            });

            ChapterProgress chapter = progress.GetChapter(ChapterIds.Outskirts);
            Assert.AreEqual(800, progress.Marks);
            Assert.AreEqual(2, progress.Samples);
            Assert.AreEqual(3, chapter.BestArena);
            Assert.IsFalse(chapter.Completed);
            Assert.AreEqual(0f, chapter.BestTimeSec);
            Assert.IsFalse(progress.GetChapter(ChapterIds.Town).Unlocked);

            ProgressStats stats = progress.Stats;
            Assert.AreEqual(2, stats.Runs);
            Assert.AreEqual(60, stats.Kills);
            Assert.AreEqual(4, stats.Crushes);
            Assert.AreEqual(1, stats.ElitesKilled);
            Assert.AreEqual(300f, stats.PlayTimeSec, 1e-3f);

            Assert.AreEqual(800, Loaded().Marks, "ApplyRun writes the save.");
        }

        [Test]
        public void ApplyRun_Victory_CompletesTheChapter_KeepsTheBestTime_AndUnlocksTheNext()
        {
            MetaProgress progress = Loaded();
            int changes = 0;
            progress.Changed += () => changes++;

            progress.ApplyRun(new RunResult
            {
                outcome = RunOutcome.Victory, chapter = ChapterIds.Outskirts, arenaReached = 5, timeSec = 900f,
                bossesKilled = 1, marks = 2400, samples = 11, firstCompletion = true
            });
            progress.ApplyRun(new RunResult
            {
                outcome = RunOutcome.Victory, chapter = ChapterIds.Outskirts, arenaReached = 5, timeSec = 1000f
            });

            Assert.AreEqual(2, changes);
            ChapterProgress chapter = Loaded().GetChapter(ChapterIds.Outskirts);
            Assert.IsTrue(chapter.Completed);
            Assert.AreEqual(5, chapter.BestArena);
            Assert.AreEqual(900f, chapter.BestTimeSec, 1e-3f);
            Assert.IsTrue(progress.GetChapter(ChapterIds.Town).Unlocked);
            Assert.IsFalse(progress.GetChapter(ChapterIds.Quarry).Unlocked);
            Assert.AreEqual(1, progress.Stats.BossesKilled);

            progress.ApplyRun(new RunResult
            {
                outcome = RunOutcome.Victory, chapter = ChapterIds.Outskirts, arenaReached = 5, timeSec = 700f
            });
            Assert.AreEqual(700f, progress.GetChapter(ChapterIds.Outskirts).BestTimeSec, 1e-3f);
        }

        // ---------- Save and load ----------

        [Test]
        public void SaveAndLoad_KeepsEverything()
        {
            MetaProgress progress = WithMoney(5000, 20);
            progress.BuyModule(ModuleIds.ArmorSkirts);
            progress.UpgradeModule(ModuleIds.ArmorSkirts);
            progress.Install(TankIds.Tiger, ModuleSlot.Armor, ModuleIds.ArmorSkirts);
            progress.SetTutorialFlag("first_shop");
            progress.ApplyRun(new RunResult
            {
                outcome = RunOutcome.Victory, chapter = ChapterIds.Outskirts, arenaReached = 5, timeSec = 800f,
                kills = 300
            });

            MetaProgress read = Loaded();

            Assert.IsTrue(read.HasSave);
            Assert.AreEqual(5000 - 1200 - 600, read.Marks);
            Assert.AreEqual(20, read.Samples);
            Assert.AreEqual(2, read.GetModuleLevel(ModuleIds.ArmorSkirts));
            Assert.AreEqual(ModuleIds.ArmorSkirts, read.GetInstalled(TankIds.Tiger, ModuleSlot.Armor));
            Assert.AreEqual(ModuleIds.Gun88He, read.GetInstalled(TankIds.Tiger, ModuleSlot.Gun));
            Assert.IsTrue(read.HasTutorialFlag("first_shop"));
            Assert.IsFalse(read.HasTutorialFlag("other"));
            Assert.IsTrue(read.GetChapter(ChapterIds.Outskirts).Completed);
            Assert.IsTrue(read.GetChapter(ChapterIds.Town).Unlocked);
            Assert.AreEqual(2, read.Stats.Runs);
            Assert.AreEqual(300, read.Stats.Kills);
            Assert.IsFalse(read.IsDirty);
        }

        [Test]
        public void Load_RaisesChanged()
        {
            MetaProgress progress = new MetaProgress(_storage, "1.0", _catalog.Catalog);
            bool changed = false;
            progress.Changed += () => changed = true;

            progress.Load();

            Assert.IsTrue(changed);
        }

        [Test]
        public void Save_IsWrittenAsVersion2_WithStringIds()
        {
            WithMoney(10);

            Assert.IsTrue(_storage.TryRead(MetaProgress.SLOT, SaveFiles.SAVE, out string text));
            StringAssert.Contains("\"v\":" + ProgressSerializer.VERSION, text.Replace(" ", string.Empty));
            StringAssert.Contains("\"selectedTank\":\"tiger\"", text.Replace(" ", string.Empty));
            StringAssert.Contains("\"Gun\":\"gun_88_he\"", text.Replace(" ", string.Empty));
        }

        [Test]
        public void Load_ASectionOfANewerGame_IsNotApplied_AndNotOverwrittenUntilNewGame()
        {
            string newer = "{\"formatVersion\":1,\"gameVersion\":\"9.0\",\"sections\":{\"progress\":" +
                           "{\"v\":" + (ProgressSerializer.VERSION + 1) + ",\"data\":{\"marks\":5}}}}";
            _storage.Put(MetaProgress.SLOT, SaveFiles.SAVE, newer);
            var progress = new MetaProgress(_storage, "1.0", _catalog.Catalog);
            LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));

            Assert.AreEqual(LoadOutcome.NewerVersion, progress.Load());
            Assert.AreEqual(0, progress.Marks);
            Assert.IsTrue(progress.IsReadOnly);
            Assert.IsTrue(progress.HasSave);

            progress.AddCredits(1);
            Assert.AreEqual(SaveOutcome.Blocked, progress.Save());
            Assert.IsTrue(_storage.TryRead(MetaProgress.SLOT, SaveFiles.SAVE, out string text));
            Assert.AreEqual(newer, text);

            progress.NewGame();
            Assert.IsFalse(progress.IsReadOnly);
            Assert.AreEqual(LoadOutcome.Loaded, new MetaProgress(_storage, "1.0", _catalog.Catalog).Load());
        }

        [Test]
        public void Load_ASaveOfASmallerCatalog_GetsTheNewEntries()
        {
            _storage.Put(MetaProgress.SLOT, SaveFiles.SAVE,
                "{\"formatVersion\":1,\"gameVersion\":\"0.1\",\"sections\":{\"progress\":{\"v\":2,\"data\":" +
                "{\"marks\":40,\"selectedTank\":\"tiger\",\"tanks\":[{\"id\":\"tiger\",\"owned\":true}]," +
                "\"modules\":null,\"chapters\":[{\"id\":\"outskirts\",\"unlocked\":true,\"completed\":true}]}}}}");

            MetaProgress progress = Loaded();

            Assert.AreEqual(40, progress.Marks);
            Assert.IsTrue(progress.OwnsModule(ModuleIds.Gun88He));
            Assert.AreEqual(ModuleIds.Gun88He, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Gun));
            Assert.IsTrue(progress.GetChapter(ChapterIds.Town).Unlocked, "Outskirts was completed.");
            Assert.IsFalse(progress.GetChapter(ChapterIds.Quarry).Unlocked);
        }
    }
}

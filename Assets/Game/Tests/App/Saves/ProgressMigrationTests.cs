using System.Collections.Generic;
using NUnit.Framework;
using Saving;

namespace Game.Tests
{
    /// <summary>"progress" version 1 (the old PlayerProgress) read by MetaProgress.</summary>
    public sealed class ProgressMigrationTests
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

        private void WriteVersion1(ProgressDataV1 data)
        {
            var writer = new SaveLoadService(_storage, "0.9");
            writer.Register(new ProgressV1Serializer(data));
            Assert.AreEqual(SaveOutcome.Saved, writer.Save());
        }

        private MetaProgress Loaded(out LoadOutcome outcome)
        {
            var progress = new MetaProgress(_storage, "1.0", _catalog.Catalog);
            outcome = progress.Load();
            return progress;
        }

        [Test]
        public void Version1_Migrates_CreditsModulesAndSlots_AndKeepsLevelIds()
        {
            WriteVersion1(new ProgressDataV1
            {
                currencies = new Dictionary<string, int> { ["credits"] = 1750, ["scrap"] = 3 },
                modules = new Dictionary<string, int>
                {
                    [ModuleIds.ArmorSkirts.Value] = 2,
                    [ModuleIds.Gun88He.Value] = 5,
                    ["cannon_t1"] = 1,
                    [ModuleIds.GunArmorPiercing.Value] = 0
                },
                installedModules = new Dictionary<string, string>
                {
                    ["Armor"] = ModuleIds.ArmorSkirts.Value,
                    ["Gun"] = ModuleIds.GunArmorPiercing.Value, // not owned: kept for the old menu only
                    ["Hull"] = "hull_t1"
                },
                unlockedLevels = new List<string> { ChapterIds.Outskirts.Value, "voxel_sandbox" },
                completedLevels = new List<string> { ChapterIds.Outskirts.Value }
            });

            MetaProgress progress = Loaded(out LoadOutcome outcome);
            IPlayerProgress legacy = progress;

            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.IsTrue(progress.HasSave);
            Assert.AreEqual(1750, progress.Marks);
            Assert.AreEqual(0, progress.Samples);

            Assert.AreEqual(TankIds.Tiger, progress.SelectedTank);
            Assert.AreEqual(2, progress.GetModuleLevel(ModuleIds.ArmorSkirts));
            Assert.AreEqual(3, progress.GetModuleLevel(ModuleIds.Gun88He), "Levels are capped at 3.");
            Assert.IsFalse(progress.OwnsModule(ModuleIds.GunArmorPiercing));
            Assert.AreEqual(1, progress.GetModuleLevel(ModuleIds.MachineGun), "Standard modules are owned.");
            Assert.AreEqual(ModuleIds.ArmorSkirts, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Armor));
            Assert.AreEqual(ModuleIds.Gun88He, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Gun));

            // v1 had no chapters: its level ids stay the old menu's, the chapters start as in a new game
            Assert.IsFalse(progress.GetChapter(ChapterIds.Outskirts).Completed);
            Assert.IsTrue(progress.GetChapter(ChapterIds.Outskirts).Unlocked);
            Assert.IsFalse(progress.GetChapter(ChapterIds.Town).Unlocked);
            Assert.IsTrue(legacy.IsLevelCompleted(ChapterIds.Outskirts.Value));

            // The old menu's own ids survive
            Assert.AreEqual(1, legacy.GetModuleLevel("cannon_t1"));
            Assert.AreEqual("hull_t1", legacy.GetInstalledModule("Hull"));
            Assert.AreEqual(ModuleIds.GunArmorPiercing.Value, legacy.GetInstalledModule("Gun"));
            Assert.IsTrue(legacy.IsLevelUnlocked("voxel_sandbox"));
        }

        [Test]
        public void Version1_IsWrittenBackAsVersion2_OnTheNextSave()
        {
            WriteVersion1(new ProgressDataV1 { currencies = new Dictionary<string, int> { ["credits"] = 40 } });
            MetaProgress progress = Loaded(out _);

            progress.SetTutorialFlag("hangar");

            Assert.IsTrue(_storage.TryRead(MetaProgress.SLOT, SaveFiles.SAVE, out string text));
            StringAssert.Contains("\"v\":" + ProgressSerializer.VERSION, text.Replace(" ", string.Empty));
            MetaProgress read = Loaded(out LoadOutcome outcome);
            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(40, read.Marks);
            Assert.IsTrue(read.HasTutorialFlag("hangar"));
        }

        [Test]
        public void Version1_AsTheOldGameWroteIt_Loads()
        {
            // The shape the old PlayerProgress wrote, nulls and all
            _storage.Put(MetaProgress.SLOT, SaveFiles.SAVE,
                "{\"formatVersion\":1,\"gameVersion\":\"0.1\",\"sections\":{\"progress\":{\"v\":1,\"data\":" +
                "{\"currencies\":{\"credits\":5},\"modules\":null,\"installedModules\":{\"Engine\":\"engine_t1\"}," +
                "\"unlockedLevels\":[\"quarry\"],\"completedLevels\":null,\"extensions\":{\"garage\":{\"paint\":\"olive\"}}}}}}");

            MetaProgress progress = Loaded(out LoadOutcome outcome);
            IPlayerProgress legacy = progress;

            Assert.AreEqual(LoadOutcome.Loaded, outcome);
            Assert.AreEqual(5, progress.Credits);
            Assert.AreEqual("engine_t1", legacy.GetInstalledModule("Engine"));
            Assert.AreEqual(ModuleIds.EngineStandard, progress.GetInstalled(TankIds.Tiger, ModuleSlot.Engine));
            Assert.IsFalse(progress.GetChapter(ChapterIds.Quarry).Unlocked, "The old menu's level id opens no chapter.");
            Assert.IsTrue(legacy.IsLevelUnlocked("quarry"));
        }

        [Test]
        public void Version1_WithoutACatalog_KeepsTheCreditsAndEveryId()
        {
            WriteVersion1(new ProgressDataV1
            {
                currencies = new Dictionary<string, int> { ["credits"] = 99 },
                modules = new Dictionary<string, int> { [ModuleIds.ArmorSkirts.Value] = 1 },
                unlockedLevels = new List<string> { ChapterIds.Outskirts.Value }
            });

            var progress = new MetaProgress(_storage, "1.0", null);
            Assert.AreEqual(LoadOutcome.Loaded, progress.Load());

            Assert.AreEqual(99, progress.Credits);
            Assert.IsTrue(progress.OwnsModule(ModuleIds.ArmorSkirts.Value));
            Assert.IsTrue(progress.IsLevelUnlocked(ChapterIds.Outskirts.Value));
        }
    }
}

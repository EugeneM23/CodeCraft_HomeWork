using NUnit.Framework;
using Saving;

namespace Game.Tests
{
    /// <summary>The old menu's IPlayerProgress calls over MetaProgress.</summary>
    public sealed class MetaProgressLegacyTests
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

        [Test]
        public void Credits_AreTheMarks_AndOnlyMakeItDirty()
        {
            MetaProgress meta = Loaded();
            IPlayerProgress progress = meta;
            int changes = 0;
            progress.Changed += () => changes++;

            progress.AddCredits(500);
            Assert.AreEqual(500, meta.Marks);
            Assert.IsTrue(meta.IsDirty);
            Assert.IsFalse(_storage.Has(MetaProgress.SLOT, SaveFiles.SAVE), "The autosave writes legacy changes.");

            Assert.IsFalse(progress.TrySpendCredits(600));
            Assert.IsTrue(progress.TrySpendCredits(200));
            Assert.AreEqual(300, progress.Credits);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void CatalogModuleIds_AreTheMetaModules()
        {
            MetaProgress meta = Loaded();
            IPlayerProgress progress = meta;

            Assert.IsTrue(progress.OwnsModule(ModuleIds.Gun88He.Value));
            Assert.AreEqual(1, progress.GetModuleLevel(ModuleIds.Gun88He.Value));

            progress.SetModuleLevel(ModuleIds.ArmorSkirts.Value, 2);
            Assert.IsTrue(meta.OwnsModule(ModuleIds.ArmorSkirts));
            Assert.AreEqual(2, meta.GetModuleLevel(ModuleIds.ArmorSkirts));

            progress.SetModuleLevel(ModuleIds.ArmorSkirts.Value, 7);
            Assert.AreEqual(ModuleConfig.MAX_LEVEL, meta.GetModuleLevel(ModuleIds.ArmorSkirts), "Capped at 3.");

            progress.SetModuleLevel(ModuleIds.ArmorSkirts.Value, 0);
            Assert.IsFalse(meta.OwnsModule(ModuleIds.ArmorSkirts));
        }

        [Test]
        public void UnknownModuleIds_AreKept_ForTheOldMenu()
        {
            IPlayerProgress progress = Loaded();

            progress.SetModuleLevel("cannon_t1", 2);
            progress.InstallModule("Hull", "hull_t1");
            ((MetaProgress)progress).Save();

            IPlayerProgress read = Loaded();
            Assert.IsTrue(read.OwnsModule("cannon_t1"));
            Assert.AreEqual(2, read.GetModuleLevel("cannon_t1"));
            Assert.AreEqual("hull_t1", read.GetInstalledModule("Hull"));
            Assert.IsFalse(read.OwnsModule("hull_t1"));
        }

        [Test]
        public void SlotNames_MapOntoTheSelectedTanksLoadout()
        {
            MetaProgress meta = Loaded();
            IPlayerProgress progress = meta;

            Assert.AreEqual(ModuleIds.Gun88He.Value, progress.GetInstalledModule("Gun"));
            Assert.AreEqual(ModuleIds.MachineGun.Value, progress.GetInstalledModule("machine_gun"));
            Assert.AreEqual(ModuleIds.ArmorStandard.Value, progress.GetInstalledModule("Armor"));
            Assert.IsNull(progress.GetInstalledModule("Hull"), "Hull has no ModuleSlot.");
            Assert.IsNull(progress.GetInstalledModule("Turret"));

            progress.SetModuleLevel(ModuleIds.ArmorSkirts.Value, 1);
            progress.InstallModule("Armor", ModuleIds.ArmorSkirts.Value);
            Assert.AreEqual(ModuleIds.ArmorSkirts, meta.GetInstalled(TankIds.Tiger, ModuleSlot.Armor));
            Assert.AreEqual(ModuleIds.ArmorSkirts.Value, progress.GetInstalledModule("Armor"));
        }

        [Test]
        public void OldMenuModules_InAModuleSlot_ShadowTheLoadout_UntilACatalogModuleGoesIn()
        {
            MetaProgress meta = Loaded();
            IPlayerProgress progress = meta;

            progress.InstallModule("Gun", "cannon_t1");
            Assert.AreEqual("cannon_t1", progress.GetInstalledModule("Gun"));
            Assert.AreEqual(ModuleIds.Gun88He, meta.GetInstalled(TankIds.Tiger, ModuleSlot.Gun), "The run's loadout keeps its gun.");

            progress.InstallModule("Gun", ModuleIds.Gun88He.Value);
            Assert.AreEqual(ModuleIds.Gun88He.Value, progress.GetInstalledModule("Gun"));

            progress.InstallModule("Gun", "cannon_t1");
            progress.InstallModule("Gun", null);
            Assert.AreEqual(ModuleIds.Gun88He.Value, progress.GetInstalledModule("Gun"));
        }

        [Test]
        public void LegacyLevelCalls_SeeTheChapters_ButNeverChangeThem()
        {
            MetaProgress meta = Loaded();
            IPlayerProgress progress = meta;

            Assert.IsTrue(progress.IsLevelUnlocked(ChapterIds.Outskirts.Value), "the chapter's real state shows through");
            Assert.IsFalse(progress.IsLevelUnlocked(ChapterIds.Town.Value));

            // The old menu path uses the same ids: it must not open or complete a chapter
            progress.UnlockLevel(ChapterIds.Town.Value);
            progress.MarkLevelCompleted(ChapterIds.Outskirts.Value);
            progress.UnlockLevel("voxel_sandbox");
            progress.MarkLevelCompleted("voxel_sandbox");

            Assert.IsFalse(meta.GetChapter(ChapterIds.Town).Unlocked);
            Assert.IsFalse(meta.GetChapter(ChapterIds.Outskirts).Completed);
            Assert.IsTrue(progress.IsLevelUnlocked(ChapterIds.Town.Value), "the menu still reads what it wrote");
            Assert.IsTrue(progress.IsLevelCompleted("voxel_sandbox"));
            CollectionAssert.AreEquivalent(
                new[] { ChapterIds.Outskirts.Value, ChapterIds.Town.Value, "voxel_sandbox" }, progress.UnlockedLevels);

            meta.Save();
            Assert.IsTrue(Loaded().IsLevelUnlocked("voxel_sandbox"));
        }

        [Test]
        public void WithoutACatalog_TheLegacyCallsStillKeepTheirData()
        {
            var progress = new MetaProgress(_storage, "1.0", null);
            progress.Load();

            progress.AddCredits(10);
            progress.SetModuleLevel("cannon_t1", 1);
            progress.InstallModule("Gun", "cannon_t1");
            progress.UnlockLevel("quarry");

            Assert.AreEqual(10, progress.Credits);
            Assert.IsTrue(progress.OwnsModule("cannon_t1"));
            Assert.AreEqual("cannon_t1", progress.GetInstalledModule("Gun"));
            Assert.IsTrue(progress.IsLevelUnlocked("quarry"));
            Assert.AreEqual(PurchaseResult.Unknown, progress.BuyModule(ModuleIds.ArmorSkirts));
        }
    }
}

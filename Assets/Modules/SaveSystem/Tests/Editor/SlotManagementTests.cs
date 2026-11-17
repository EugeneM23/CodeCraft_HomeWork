using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Saving.Tests
{
    /// <summary>Managing slots: names, creation time, scene, copy, latest and free slot, export and import.</summary>
    public class SlotManagementTests
    {
        private static readonly DateTime Start = new(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStorage _storage;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _storage = new InMemorySaveStorage();
            _now = Start;
        }

        private SaveLoadService CreateService() => new(_storage, "1.0", () => _now);

        private SaveLoadService SaveSlot(int slot, int value, string name = null)
        {
            SaveLoadService service = CreateService();
            service.SelectSlot(slot);
            service.Register(new FlakySerializer("value") { Value = value });
            if (name != null)
                service.SlotName = name;

            service.Save();
            return service;
        }

        private int LoadValue(int slot)
        {
            SaveLoadService reader = CreateService();
            reader.SelectSlot(slot);
            var value = new FlakySerializer("value") { Value = -1 };
            reader.Register(value);
            reader.Load();
            return value.Value;
        }

        [Test]
        public void GetSlot_Empty_ReturnsNull()
        {
            Assert.IsNull(CreateService().GetSlot(3));
            Assert.IsFalse(CreateService().HasSlot(3));
        }

        [Test]
        public void GetSlot_Saved_HasNameTimesAndScene()
        {
            SaveLoadService service = CreateService();
            service.SceneName = () => "Cave";
            service.SlotName = "Hero";
            service.Save();
            _now = Start.AddMinutes(5);
            service.Save();

            SlotInfo info = CreateService().GetSlot(0);

            Assert.IsTrue(CreateService().HasSlot(0));
            Assert.AreEqual("Hero", info.Name);
            Assert.AreEqual("Cave", info.Scene);
            Assert.AreEqual(Start, info.CreatedAtUtc, "the creation time stays from the first save");
            Assert.AreEqual(Start.AddMinutes(5), info.SavedAtUtc);
        }

        [Test]
        public void GetSlot_OldMetaWithoutNewFields_CreatedIsSavedTime()
        {
            _storage.Put(1, SaveFiles.META,
                "{ \"formatVersion\": 1, \"gameVersion\": \"1.0\", \"savedAtUtc\": \"2026-10-23T14:05:00Z\", \"playTimeSeconds\": 3 }");

            SlotInfo info = CreateService().GetSlot(1);

            Assert.AreEqual(string.Empty, info.Name);
            Assert.AreEqual(info.SavedAtUtc, info.CreatedAtUtc);
            Assert.AreEqual(string.Empty, info.Scene);
        }

        [Test]
        public void SlotName_LoadReadsIt_SelectSlotClearsIt()
        {
            SaveSlot(0, 1, "Hero");

            SaveLoadService service = CreateService();
            service.Load();
            Assert.AreEqual("Hero", service.SlotName);

            service.SelectSlot(1);
            Assert.AreEqual(string.Empty, service.SlotName);
        }

        [Test]
        public void SlotName_KeptAfterLoad_CreationTimeToo()
        {
            SaveSlot(0, 1, "Hero");
            _now = Start.AddHours(1);

            SaveLoadService service = CreateService();
            service.Load();
            service.Save();

            SlotInfo info = CreateService().GetSlot(0);
            Assert.AreEqual("Hero", info.Name);
            Assert.AreEqual(Start, info.CreatedAtUtc);
        }

        [Test]
        public void RenameSlot_OtherSlot_ChangesOnlyItsName()
        {
            SaveSlot(2, 7, "Old");
            _now = Start.AddHours(1);
            SaveLoadService service = CreateService();
            int changes = 0;
            service.SlotsChanged += () => changes++;

            Assert.IsTrue(service.RenameSlot(2, "Before the boss"));

            SlotInfo info = service.GetSlot(2);
            Assert.AreEqual("Before the boss", info.Name);
            Assert.AreEqual(Start, info.SavedAtUtc, "renaming is not saving");
            Assert.AreEqual(7, LoadValue(2));
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void RenameSlot_CurrentSlot_NextSaveKeepsName()
        {
            SaveLoadService service = SaveSlot(0, 1, "Old");

            service.RenameSlot(0, "New");
            service.Save();

            Assert.AreEqual("New", service.SlotName);
            Assert.AreEqual("New", CreateService().GetSlot(0).Name);
        }

        [Test]
        public void RenameSlot_EmptySlot_ReturnsFalse()
        {
            Assert.IsFalse(CreateService().RenameSlot(4, "Nothing"));
            Assert.IsFalse(CreateService().HasSlot(4));
        }

        [Test]
        public void RenameSlot_MetaOfNewerGame_LeftAlone()
        {
            const string newer = "{ \"formatVersion\": 99, \"savedAtUtc\": \"2026-10-23T14:05:00Z\", \"futureField\": 1 }";
            _storage.Put(1, SaveFiles.META, newer);

            Assert.IsFalse(CreateService().RenameSlot(1, "Mine"));

            _storage.TryRead(1, SaveFiles.META, out string text);
            Assert.AreEqual(newer, text);
        }

        [Test]
        public void ExportSlot_BrokenMain_TakesTheBackup()
        {
            SaveSlot(1, 5);
            SaveSlot(1, 6);
            _storage.Put(1, SaveFiles.SAVE, "{ broken");

            Assert.IsTrue(CreateService().CopySlot(1, 4));

            Assert.AreEqual(5, LoadValue(4));
        }

        [Test]
        public void ExportSlot_NothingReadable_NullAndCopyFalse()
        {
            _storage.Put(1, SaveFiles.SAVE, "{ broken");

            Assert.IsNull(CreateService().ExportSlot(1));
            Assert.IsFalse(CreateService().CopySlot(1, 4));
        }

        [Test]
        public void CopySlot_CopiesDataAndMeta()
        {
            SaveSlot(1, 5, "Hero");
            SaveLoadService service = CreateService();

            Assert.IsTrue(service.CopySlot(1, 3));

            Assert.AreEqual(5, LoadValue(3));
            Assert.AreEqual("Hero", service.GetSlot(3).Name);
            Assert.AreEqual(5, LoadValue(1), "the source stays");
        }

        [Test]
        public void CopySlot_OverTarget_ReplacesIt()
        {
            SaveSlot(1, 5);
            SaveSlot(2, 9);

            CreateService().CopySlot(1, 2);

            Assert.AreEqual(5, LoadValue(2));
        }

        [Test]
        public void CopySlot_EmptySourceOrSameSlot()
        {
            SaveLoadService service = CreateService();

            Assert.IsFalse(service.CopySlot(4, 5));
            Assert.Throws<ArgumentException>(() => service.CopySlot(1, 1));
        }

        [Test]
        public void CopySlot_IntoCurrentSlot_BlocksSavingUntilLoad()
        {
            SaveSlot(1, 5);
            SaveLoadService service = SaveSlot(0, 1);

            service.CopySlot(1, 0);

            Assert.IsTrue(service.IsReadOnly);
            Assert.AreEqual(SaveOutcome.Blocked, service.Save());
            service.Load();
            Assert.IsFalse(service.IsReadOnly);
            Assert.AreEqual(5, LoadValue(0));
        }

        [Test]
        public void GetLatestSlot_NewestSaveWins()
        {
            Assert.AreEqual(-1, CreateService().GetLatestSlot());

            SaveSlot(4, 1);
            _now = Start.AddMinutes(1);
            SaveSlot(1, 1);
            _now = Start.AddMinutes(2);
            SaveSlot(2, 1);
            _now = Start.AddMinutes(3);
            SaveSlot(1, 2);

            Assert.AreEqual(1, CreateService().GetLatestSlot());
        }

        [Test]
        public void GetFreeSlot_FirstWithoutData()
        {
            SaveSlot(0, 1);
            SaveSlot(1, 1);
            SaveSlot(3, 1);

            Assert.AreEqual(2, CreateService().GetFreeSlot());
            Assert.AreEqual(4, CreateService().GetFreeSlot(3));
        }

        [Test]
        public void DeleteSlot_RaisesSlotsChanged()
        {
            SaveSlot(1, 1);
            SaveLoadService service = CreateService();
            int changes = 0;
            service.SlotsChanged += () => changes++;

            service.DeleteSlot(1);
            service.Save();

            Assert.AreEqual(2, changes, "a delete and a save");
            Assert.IsFalse(service.HasSlot(1));
        }

        [Test]
        public void ExportImport_MovesSlotAsOneString()
        {
            SaveSlot(2, 8, "Hero");
            string package = CreateService().ExportSlot(2);

            var other = new InMemorySaveStorage();
            var service = new SaveLoadService(other, "1.0", () => _now);
            service.ImportSlot(5, package);

            service.SelectSlot(5);
            var value = new FlakySerializer("value");
            service.Register(value);
            Assert.AreEqual(LoadOutcome.Loaded, service.Load());
            Assert.AreEqual(8, value.Value);
            Assert.AreEqual("Hero", service.SlotName);
        }

        [Test]
        public void ExportSlot_EmptySlot_ReturnsNull() => Assert.IsNull(CreateService().ExportSlot(6));

        [Test]
        public void ImportSlot_BrokenPackage_ThrowsAndWritesNothing()
        {
            SaveLoadService service = CreateService();

            Assert.Throws<FormatException>(() => service.ImportSlot(1, "{ broken"));
            Assert.Throws<FormatException>(() => service.ImportSlot(1, "{ \"files\": { } }"));
            Assert.Throws<ArgumentException>(() => service.ImportSlot(1, null));
            Assert.IsFalse(service.HasSlot(1));
        }

        [Test]
        public void ImportSlot_IntoCurrentSlot_BlocksSavingUntilLoad()
        {
            SaveSlot(3, 4);
            string package = CreateService().ExportSlot(3);
            SaveLoadService service = SaveSlot(0, 1);

            service.ImportSlot(0, package);

            Assert.AreEqual(SaveOutcome.Blocked, service.Save());
            Assert.AreEqual(4, LoadValue(0));
        }

        [Test]
        public void SelectSlot_KeepData_KeepsNameStartsCreationOver()
        {
            SaveLoadService service = SaveSlot(0, 1, "Hero");
            _now = Start.AddHours(2);

            service.SelectSlot(4, keepData: true);
            service.Save();

            SlotInfo info = service.GetSlot(4);
            Assert.AreEqual("Hero", info.Name);
            Assert.AreEqual(Start.AddHours(2), info.CreatedAtUtc);
        }

        [Test]
        public void GetSlots_ListsNewFields()
        {
            SaveSlot(0, 1, "A");
            SaveSlot(1, 1, "B");

            IReadOnlyList<SlotInfo> slots = CreateService().GetSlots();

            Assert.AreEqual("A", slots[0].Name);
            Assert.AreEqual("B", slots[1].Name);
        }
    }
}

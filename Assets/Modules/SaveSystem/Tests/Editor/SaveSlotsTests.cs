using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Slots, meta.json, play time and summary.</summary>
    public class SaveSlotsTests
    {
        private static readonly DateTime SavedAt = new(2026, 10, 23, 14, 5, 0, DateTimeKind.Utc);

        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private SaveLoadService CreateService() => new(_storage, "1.0", () => SavedAt);

        [Test]
        public void SaveSlots_DifferentSlots_DoNotShareData()
        {
            SaveLoadService service = CreateService();
            var value = new FlakySerializer("value");
            service.Register(value);

            value.Value = 1;
            service.Save();
            service.SelectSlot(1);
            value.Value = 2;
            service.Save();

            service.SelectSlot(0);
            service.Load();
            Assert.AreEqual(1, value.Value);
            service.SelectSlot(1);
            service.Load();
            Assert.AreEqual(2, value.Value);
        }

        [Test]
        public void SaveSlots_KeepData_CopiesSnapshotIntoOtherSlot()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new FlakySerializer("other") { Value = 5 });
            writer.Register(new FlakySerializer("value") { Value = 1 });
            writer.Save();

            SaveLoadService service = CreateService();
            service.Register(new FlakySerializer("value"));
            service.Load();
            service.SelectSlot(1, keepData: true);
            Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));

            SaveLoadService reader = CreateService();
            reader.SelectSlot(1);
            var other = new FlakySerializer("other");
            reader.Register(other);
            reader.Load();
            Assert.AreEqual(5, other.Value);
        }

        [Test]
        public void SaveSlots_GetSlots_ReadsMetaOnly()
        {
            SaveLoadService service = CreateService();
            service.Summary["level"] = "Cave";
            service.AddPlayTime(12.5);
            service.Save();
            service.SelectSlot(2);
            service.Summary["level"] = "Lava";
            service.Save();
            _storage.Put(2, SaveFiles.SAVE, "{ broken");

            IReadOnlyList<SlotInfo> slots = CreateService().GetSlots();

            Assert.AreEqual(2, slots.Count);
            Assert.AreEqual(0, slots[0].Slot);
            Assert.AreEqual("Cave", slots[0].Summary["level"]);
            Assert.AreEqual(12.5, slots[0].PlayTimeSeconds);
            Assert.AreEqual(SavedAt, slots[0].SavedAtUtc);
            Assert.AreEqual("1.0", slots[0].GameVersion);
            Assert.AreEqual(2, slots[1].Slot);
            Assert.AreEqual("Lava", slots[1].Summary["level"]);
            Assert.AreEqual(0, slots[1].PlayTimeSeconds, "selecting a slot starts its play time over");
        }

        [Test]
        public void SaveSlots_SlotWithoutMeta_NotListed()
        {
            _storage.Put(3, SaveFiles.SAVE, "{}");

            CollectionAssert.IsEmpty(CreateService().GetSlots());
        }

        [Test]
        public void SaveSlots_DeleteCurrent_RemovesFilesAndStartsOver()
        {
            SaveLoadService service = CreateService();
            service.Register(new FlakySerializer("value") { Value = 1 });
            service.Summary["level"] = "Cave";
            service.Save();

            service.DeleteSlot(0);

            Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE));
            Assert.IsFalse(_storage.Has(0, SaveFiles.META));
            CollectionAssert.IsEmpty(service.Summary);
            Assert.AreEqual(LoadOutcome.Empty, service.Load());
        }

        [Test]
        public void SaveSlots_PlayTimeAndSummary_SurviveSaveAndLoad()
        {
            SaveLoadService writer = CreateService();
            writer.AddPlayTime(12.5);
            writer.AddPlayTime(-3);
            writer.Summary["checkpoint"] = "Gate";
            writer.Save();

            SaveLoadService reader = CreateService();
            reader.Load();

            Assert.AreEqual(12.5, reader.PlayTime);
            Assert.AreEqual("Gate", reader.Summary["checkpoint"]);
        }

        [Test]
        public void SaveSlots_UnreadableMeta_DataLoadsTimeFromZero()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new FlakySerializer("value") { Value = 4 });
            writer.AddPlayTime(10);
            writer.Save();
            _storage.Put(0, SaveFiles.META, "{ broken");

            SaveLoadService reader = CreateService();
            var value = new FlakySerializer("value");
            reader.Register(value);
            LogAssert.Expect(LogType.Warning, new Regex("unreadable"));

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.AreEqual(4, value.Value);
            Assert.AreEqual(0, reader.PlayTime);
        }

        [Test]
        public void SaveSlots_SelectSlot_ClearsSummaryAndPlayTime()
        {
            SaveLoadService service = CreateService();
            service.Summary["level"] = "Cave";
            service.AddPlayTime(5);

            service.SelectSlot(1);

            CollectionAssert.IsEmpty(service.Summary);
            Assert.AreEqual(0, service.PlayTime);
        }
    }
}

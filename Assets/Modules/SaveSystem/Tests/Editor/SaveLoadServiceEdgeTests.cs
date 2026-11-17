using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>
    /// The service's edges: argument guards, calls refused while a load is in progress, files of a broken shape,
    /// layers that throw, and storages that fail on one file only.
    /// </summary>
    public class SaveLoadServiceEdgeTests
    {
        /// <summary>A layer that throws on check or on load; otherwise loads at once.</summary>
        private sealed class ThrowingLayer : ISaveExtension
        {
            public bool FailCheck;
            public bool FailBegin;

            public void CaptureInto(IDictionary<string, SaveSection> sections)
            {
            }

            public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
            {
                if (FailCheck)
                    throw new InvalidOperationException("check broke");

                problem = null;
                return LoadOutcome.Loaded;
            }

            public void BeginLoad(Action finished)
            {
                if (FailBegin)
                    throw new InvalidOperationException("begin broke");

                finished();
            }

            public void Dispose()
            {
            }
        }

        /// <summary>In-memory storage that fails reading or writing chosen files only.</summary>
        private sealed class SelectiveStorage : ISaveStorage
        {
            public readonly InMemorySaveStorage Inner;
            public Func<string, bool> FailRead = _ => false;
            public Func<string, bool> FailWrite = _ => false;

            public SelectiveStorage(InMemorySaveStorage inner) => Inner = inner;

            public IReadOnlyList<int> ListSlots() => Inner.ListSlots();

            public bool TryRead(int slot, string file, out string text)
            {
                if (FailRead(file))
                    throw new IOException($"{file} cannot be read");

                return Inner.TryRead(slot, file, out text);
            }

            public bool TryReadBackup(int slot, string file, out string text) => Inner.TryReadBackup(slot, file, out text);

            public void Write(int slot, string file, string text)
            {
                if (FailWrite(file))
                    throw new IOException($"{file} cannot be written");

                Inner.Write(slot, file, text);
            }

            public void Quarantine(int slot, string file, bool backup) => Inner.Quarantine(slot, file, backup);
            public void DeleteSlot(int slot) => Inner.DeleteSlot(slot);
        }

        private const string NEWER_FORMAT = @"{ ""formatVersion"": 2, ""gameVersion"": ""9.0"", ""chunks"": [] }";

        private const string STEPS_V1 = @"{
  ""formatVersion"": 1,
  ""gameVersion"": ""0.9"",
  ""sections"": { ""steps"": { ""v"": 1, ""data"": {} } }
}";

        private const string VALUES_V0 = @"{
  ""formatVersion"": 1,
  ""gameVersion"": ""0.9"",
  ""sections"": { ""$vars"": { ""v"": 0, ""data"": { ""coins"": 3 } } }
}";

        private static readonly DateTime Start = new(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private SaveLoadService CreateService(ISaveStorage storage = null) => new(storage ?? _storage, "1.0", () => Start);

        private void SaveValue(int value)
        {
            SaveLoadService writer = CreateService();
            writer.Register(new FlakySerializer("value") { Value = value });
            writer.Save();
        }

        /// <summary>A reader whose layer holds the load open until <see cref="FakeExtension.Finish"/>.</summary>
        private (SaveLoadService Service, FakeExtension Layer) LoadHeldOpen()
        {
            SaveValue(5);
            SaveLoadService reader = CreateService();
            var layer = new FakeExtension { Service = reader, FinishAtOnce = false };
            reader.Attach(layer);
            reader.Load();
            Assert.IsTrue(reader.IsLoading, "the layer holds the load open");
            return (reader, layer);
        }

        // ---------- Guards ----------

        [Test]
        public void SaveLoadService_RegisterNull_Throws() =>
            Assert.Throws<ArgumentNullException>(() => CreateService().Register(null));

        [Test]
        public void SaveLoadService_RegisterWithoutKey_Throws()
        {
            SaveLoadService service = CreateService();

            Assert.Throws<ArgumentException>(() => service.Register(new FlakySerializer("")));
            Assert.Throws<ArgumentException>(() => service.Register(new FlakySerializer(null)));
        }

        [Test]
        public void SaveLoadService_UnregisterAnotherInstanceOrNull_KeepsTheRegisteredOne()
        {
            SaveLoadService service = CreateService();
            service.Register(new FlakySerializer("value"));

            service.Unregister(new FlakySerializer("value"));
            service.Unregister((IGameSerializer)null);
            service.Unregister((string)null);

            Assert.Throws<ArgumentException>(() => service.Register(new FlakySerializer("value")),
                "the first serializer is still registered under its key");
        }

        // ---------- During a load ----------

        [Test]
        public void SaveLoadService_LoadSelectSlotAttachDuringLoad_Throw()
        {
            (SaveLoadService reader, FakeExtension layer) = LoadHeldOpen();

            Assert.Throws<InvalidOperationException>(() => reader.Load());
            Assert.Throws<InvalidOperationException>(() => reader.SelectSlot(1));
            Assert.Throws<InvalidOperationException>(() => reader.Attach(null));

            layer.Finish();
            Assert.IsFalse(reader.IsLoading);
            Assert.AreEqual(0, reader.CurrentSlot);
        }

        [Test]
        public void SaveLoadService_CaptureDuringLoad_LeavesSnapshotAlone()
        {
            (SaveLoadService reader, FakeExtension layer) = LoadHeldOpen();
            layer.Value = 9;

            reader.Capture();

            Assert.AreEqual(0, layer.Captures);
            Assert.IsFalse(reader.TryGetSection(FakeExtension.KEY, out _));
            layer.Finish();
            reader.Capture();
            Assert.IsTrue(reader.TryGetSection(FakeExtension.KEY, out SaveSection section));
            Assert.AreEqual(9, (int)section.Data);
        }

        [Test]
        public void SaveLoadService_LayerFinishesTwice_LoadedRaisedOnce()
        {
            (SaveLoadService reader, FakeExtension layer) = LoadHeldOpen();
            int loadedEvents = 0;
            reader.Loaded += _ => loadedEvents++;

            layer.Finish();
            layer.Finish();

            Assert.AreEqual(1, loadedEvents);
            Assert.IsFalse(reader.IsLoading);
        }

        // ---------- Layers that throw ----------

        [Test]
        public void SaveLoadService_LayerCheckThrows_FailedNothingAppliedAndReadOnly()
        {
            SaveValue(5);
            SaveLoadService reader = CreateService();
            reader.Attach(new ThrowingLayer { FailCheck = true });
            var value = new FlakySerializer("value");
            reader.Register(value);

            LogAssert.Expect(LogType.Error, new Regex("checking the saved scenes failed: check broke"));
            Assert.AreEqual(LoadOutcome.Failed, reader.Load());

            Assert.AreEqual(0, value.Value);
            Assert.IsTrue(reader.IsReadOnly);
            Assert.IsFalse(reader.IsLoading);
        }

        [Test]
        public void SaveLoadService_LayerBeginLoadThrows_LoggedAndLoadEnds()
        {
            SaveValue(5);
            SaveLoadService reader = CreateService();
            reader.Attach(new ThrowingLayer { FailBegin = true });
            var value = new FlakySerializer("value");
            reader.Register(value);
            var outcomes = new List<LoadOutcome>();
            reader.Loaded += outcomes.Add;

            LogAssert.Expect(LogType.Error, new Regex("Restoring the saved scene of slot 0 failed: begin broke"));
            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            Assert.AreEqual(5, value.Value, "the global sections were applied before the layer");
            Assert.IsFalse(reader.IsLoading);
            CollectionAssert.AreEqual(new[] { LoadOutcome.Loaded }, outcomes);
        }

        // ---------- Broken files ----------

        [TestCase(@"{ ""formatVersion"": 0, ""sections"": {} }", TestName = "SaveLoadService_FormatVersionZero_Corrupted")]
        [TestCase(@"{ ""formatVersion"": 1, ""sections"": 5 }", TestName = "SaveLoadService_SectionsNotAnObject_Corrupted")]
        [TestCase(@"{ ""formatVersion"": 1, ""sections"": null }", TestName = "SaveLoadService_SectionsNull_Corrupted")]
        [TestCase(@"{ ""formatVersion"": 1, ""sections"": { ""value"": null } }", TestName = "SaveLoadService_NullSection_Corrupted")]
        public void SaveLoadService_MalformedMainWithoutBackup_CorruptedAndQuarantined(string text)
        {
            _storage.Put(0, SaveFiles.SAVE, text);
            SaveLoadService reader = CreateService();
            var value = new FlakySerializer("value") { Value = 4 };
            reader.Register(value);

            LogAssert.Expect(LogType.Warning, new Regex("is corrupted"));
            Assert.AreEqual(LoadOutcome.Corrupted, reader.Load());

            Assert.AreEqual(4, value.Value, "nothing was applied");
            Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE));
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX));
            Assert.IsFalse(reader.IsReadOnly);
        }

        [Test]
        public void SaveLoadService_BrokenMainAndNewerBackup_RefusedAndReadOnly()
        {
            _storage.Put(0, SaveFiles.SAVE, "{ broken");
            _storage.Put(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX, NEWER_FORMAT);
            SaveLoadService reader = CreateService();

            LogAssert.Expect(LogType.Error, new Regex("its backup was written by a newer version"));
            Assert.AreEqual(LoadOutcome.NewerVersion, reader.Load());

            Assert.IsTrue(reader.IsReadOnly);
            Assert.AreEqual(SaveOutcome.Blocked, reader.Save());
            Assert.IsTrue(_storage.TryReadBackup(0, SaveFiles.SAVE, out string backup));
            Assert.AreEqual(NEWER_FORMAT, backup, "the newer backup is left as it was");
        }

        [Test]
        public void SaveLoadService_SerializerWithoutSection_KeepsItsValueOthersLoad()
        {
            SaveValue(5);
            SaveLoadService reader = CreateService();
            var value = new FlakySerializer("value");
            var added = new FlakySerializer("added") { Value = 3 };
            reader.Register(value);
            reader.Register(added);

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            Assert.AreEqual(5, value.Value);
            Assert.AreEqual(3, added.Value);
        }

        [Test]
        public void SaveLoadService_ValuesSectionInvalidVersion_MigrationFailed()
        {
            _storage.Put(0, SaveFiles.SAVE, VALUES_V0);
            SaveLoadService reader = CreateService();

            LogAssert.Expect(LogType.Error, new Regex("has an invalid version 0"));
            Assert.AreEqual(LoadOutcome.MigrationFailed, reader.Load());

            Assert.IsTrue(reader.IsReadOnly);
            Assert.IsFalse(reader.Values.Has("coins"));
        }

        // ---------- meta.json ----------

        [Test]
        public void SaveLoadService_MetaReadThrows_DataLoadsTimeFromZero()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new FlakySerializer("value") { Value = 4 });
            writer.AddPlayTime(30);
            writer.Save();
            var storage = new SelectiveStorage(_storage) { FailRead = file => file == SaveFiles.META };
            SaveLoadService reader = CreateService(storage);
            var value = new FlakySerializer("value");
            reader.Register(value);

            LogAssert.Expect(LogType.Warning, new Regex("meta.json of save slot 0 is unreadable: meta.json cannot be read"));
            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            Assert.AreEqual(4, value.Value);
            Assert.AreEqual(0, reader.PlayTime);
        }

        [Test]
        public void SaveLoadService_MetaWithoutSummary_EmptySummaryKeepsPlayTime()
        {
            SaveValue(5);
            _storage.Put(0, SaveFiles.META, SaveJson.Write(new SlotMeta
            {
                FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                SavedAtUtc = SlotMeta.FormatTime(Start),
                PlayTimeSeconds = 12,
                Summary = null
            }));
            SaveLoadService reader = CreateService();
            reader.Summary["stale"] = "x";

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            Assert.AreEqual(12, reader.PlayTime);
            CollectionAssert.IsEmpty(reader.Summary);
        }

        [Test]
        public void SaveLoadService_SceneNameThrows_SavedWithEmptyScene()
        {
            SaveLoadService service = CreateService();
            service.SceneName = () => throw new InvalidOperationException("no scene");

            Assert.AreEqual(SaveOutcome.Saved, service.Save());

            Assert.AreEqual(string.Empty, service.GetSlot(0).Scene);
        }

        [Test]
        public void SaveLoadService_PremigrationCopyCannotBeWritten_WarnsAndLoads()
        {
            _storage.Put(0, SaveFiles.SAVE, STEPS_V1);
            var storage = new SelectiveStorage(_storage) { FailWrite = file => file == SaveFiles.PREMIGRATION };
            SaveLoadService reader = CreateService(storage);
            var steps = new StepSerializer("steps");
            reader.Register(steps);

            LogAssert.Expect(LogType.Warning, new Regex("Could not keep the pre-migration copy of save slot 0"));
            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            CollectionAssert.AreEqual(new[] { 1, 2 }, steps.Steps);
            Assert.IsFalse(_storage.Has(0, SaveFiles.PREMIGRATION));
        }

        // ---------- Slots ----------

        [Test]
        public void SaveLoadService_RenameCurrentSlotWithoutMeta_NameKeptAndSlotsChanged()
        {
            SaveLoadService service = CreateService();
            int changes = 0;
            service.SlotsChanged += () => changes++;

            Assert.IsTrue(service.RenameSlot(0, "Hero"));

            Assert.AreEqual(1, changes);
            Assert.AreEqual("Hero", service.SlotName);
            Assert.IsNull(service.GetSlot(0), "nothing is written before the first save");
            service.Save();
            Assert.AreEqual("Hero", service.GetSlot(0).Name);
        }

        [Test]
        public void SaveLoadService_ImportPackageWithoutMeta_WritesFreshMeta()
        {
            SaveValue(7);
            SaveLoadService service = CreateService();
            Assert.IsTrue(SaveJson.TryRead(service.ExportSlot(0), out SlotPackage package));
            package.Files.Remove(SaveFiles.META);
            service.SelectSlot(5);

            service.ImportSlot(3, SaveJson.Write(package));

            SlotInfo info = service.GetSlot(3);
            Assert.IsNotNull(info);
            Assert.AreEqual(Start, info.SavedAtUtc);
            Assert.AreEqual(string.Empty, info.Name);
            service.SelectSlot(3);
            var value = new FlakySerializer("value");
            service.Register(value);
            Assert.AreEqual(LoadOutcome.Loaded, service.Load());
            Assert.AreEqual(7, value.Value);
        }
    }
}

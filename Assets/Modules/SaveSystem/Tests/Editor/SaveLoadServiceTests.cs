using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Saving, registering and capturing: the snapshot keeps what is not registered right now.</summary>
    public class SaveLoadServiceTests
    {
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private SaveLoadService CreateService() => new(_storage, "1.0");

        private static Progress SampleProgress() =>
            new() { Level = 3, Name = "Cave", Position = new Vector3(1f, 2f, 3f) };

        [Test]
        public void SaveLoadService_SaveThenLoad_RestoresRegisteredData()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new ProgressSerializer(SampleProgress()));
            Assert.AreEqual(SaveOutcome.Saved, writer.Save());

            SaveLoadService reader = CreateService();
            var progress = new Progress();
            reader.Register(new ProgressSerializer(progress));

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.AreEqual(3, progress.Level);
            Assert.AreEqual("Cave", progress.Name);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), progress.Position);
        }

        [Test]
        public void SaveLoadService_NoFile_Empty() => Assert.AreEqual(LoadOutcome.Empty, CreateService().Load());

        [Test]
        public void SaveLoadService_RegisterAfterLoad_AppliesItsSectionAtOnce()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new ProgressSerializer(SampleProgress()));
            writer.Save();

            SaveLoadService reader = CreateService();
            reader.Load();
            var progress = new Progress();
            reader.Register(new ProgressSerializer(progress));

            Assert.AreEqual(3, progress.Level);
        }

        [Test]
        public void SaveLoadService_SectionWithoutSerializer_IsKept()
        {
            SaveLoadService first = CreateService();
            first.Register(new ProgressSerializer(SampleProgress()));
            first.Register(new FlakySerializer("other") { Value = 42 });
            first.Save();

            SaveLoadService second = CreateService();
            second.Register(new ProgressSerializer(new Progress()));
            second.Load();
            second.Save();

            SaveLoadService third = CreateService();
            var other = new FlakySerializer("other");
            third.Register(other);
            third.Load();

            Assert.AreEqual(42, other.Value);
        }

        [Test]
        public void SaveLoadService_SameKeyTwice_Throws()
        {
            SaveLoadService service = CreateService();
            service.Register(new FlakySerializer("same"));

            Assert.Throws<ArgumentException>(() => service.Register(new FlakySerializer("same")));
        }

        [Test]
        public void SaveLoadService_CaptureThrows_KeepsItsPreviousDataAndSavesOthers()
        {
            SaveLoadService service = CreateService();
            var flaky = new FlakySerializer("flaky") { Value = 1 };
            Progress progress = SampleProgress();
            service.Register(flaky);
            service.Register(new ProgressSerializer(progress));
            service.Save();

            flaky.Value = 2;
            flaky.FailCapture = true;
            progress.Level = 9;
            LogAssert.Expect(LogType.Error, new Regex("Saving 'flaky' failed"));
            Assert.AreEqual(SaveOutcome.Saved, service.Save());

            SaveLoadService reader = CreateService();
            var flakyRead = new FlakySerializer("flaky");
            var progressRead = new Progress();
            reader.Register(flakyRead);
            reader.Register(new ProgressSerializer(progressRead));
            reader.Load();

            Assert.AreEqual(1, flakyRead.Value);
            Assert.AreEqual(9, progressRead.Level);
        }

        [Test]
        public void SaveLoadService_RestoreThrows_OthersStillRestored()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new FlakySerializer("flaky") { Value = 5 });
            writer.Register(new ProgressSerializer(SampleProgress()));
            writer.Save();

            SaveLoadService reader = CreateService();
            var progress = new Progress();
            reader.Register(new FlakySerializer("flaky") { FailRestore = true });
            reader.Register(new ProgressSerializer(progress));
            LogAssert.Expect(LogType.Error, new Regex("Loading 'flaky' failed"));

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.AreEqual(3, progress.Level);
        }

        [Test]
        public void SaveLoadService_Load_RestoresInAscendingOrder()
        {
            var orders = new[] { ("late", 5), ("early", -1), ("middle", 0) };
            SaveLoadService writer = CreateService();
            foreach ((string key, int order) in orders)
                writer.Register(new FlakySerializer(key, order));
            writer.Save();

            var restored = new List<string>();
            SaveLoadService reader = CreateService();
            foreach ((string key, int order) in orders)
                reader.Register(new FlakySerializer(key, order) { OnRestore = () => restored.Add(key) });
            reader.Load();

            CollectionAssert.AreEqual(new[] { "early", "middle", "late" }, restored);
        }

        [Test]
        public void SaveLoadService_SaveDuringLoad_Blocked()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new FlakySerializer("value") { Value = 1 });
            writer.Save();

            SaveLoadService reader = CreateService();
            SaveOutcome? during = null;
            reader.Register(new FlakySerializer("value") { OnRestore = () => during = reader.Save() });
            reader.Load();

            Assert.AreEqual(SaveOutcome.Blocked, during);
        }

        [Test]
        public void SaveLoadService_OnlyIfChangedNothingChanged_WritesNothing()
        {
            SaveLoadService service = CreateService();
            var value = new FlakySerializer("value") { Value = 1 };
            service.Register(value);
            service.Save();

            Assert.AreEqual(SaveOutcome.Unchanged, service.Save(onlyIfChanged: true));
            Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX), "a second write would make a backup");

            value.Value = 2;
            Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));
        }

        [Test]
        public void SaveLoadService_OnlyIfChangedEmptySlot_WritesNothing()
        {
            Assert.AreEqual(SaveOutcome.Unchanged, CreateService().Save(onlyIfChanged: true));
            Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE));
        }

        [Test]
        public void SaveLoadService_CaptureThenUnregister_KeepsCapturedDataWithoutWriting()
        {
            SaveLoadService service = CreateService();
            var scene = new FlakySerializer("scene") { Value = 7 };
            service.Register(scene);

            service.Capture();
            service.Unregister(scene);
            Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE), "capture must not write");

            scene.Value = 99; // no longer registered: not saved any more
            service.Save();

            SaveLoadService reader = CreateService();
            var read = new FlakySerializer("scene");
            reader.Register(read);
            reader.Load();
            Assert.AreEqual(7, read.Value);
        }

        [Test]
        public void SaveLoadService_DateLikeString_StaysAsWritten()
        {
            SaveLoadService writer = CreateService();
            writer.Register(new ProgressSerializer(new Progress { Name = "2026-10-23T14:05:00Z" }));
            writer.Save();

            SaveLoadService reader = CreateService();
            var progress = new Progress();
            reader.Register(new ProgressSerializer(progress));
            reader.Load();

            Assert.AreEqual("2026-10-23T14:05:00Z", progress.Name);
        }

        [Test]
        public void SaveLoadService_NegativeSlot_Throws() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateService().SelectSlot(-1));
    }
}

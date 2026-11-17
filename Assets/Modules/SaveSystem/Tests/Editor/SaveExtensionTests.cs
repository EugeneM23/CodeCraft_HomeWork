using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>The layer hook: its sections go through the snapshot, it can refuse a load and finish it later.</summary>
    public class SaveExtensionTests
    {
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private (SaveLoadService Service, FakeExtension Layer) Create()
        {
            var service = new SaveLoadService(_storage, "1.0");
            var layer = new FakeExtension { Service = service };
            service.Attach(layer);
            return (service, layer);
        }

        private void SaveValue(int value, int other = 0)
        {
            (SaveLoadService writer, FakeExtension layer) = Create();
            layer.Value = value;
            writer.Register(new FlakySerializer("other") { Value = other });
            writer.Save();
        }

        [Test]
        public void Extension_SaveThenLoad_RestoresItsSection()
        {
            SaveValue(5);
            (SaveLoadService reader, FakeExtension layer) = Create();

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.AreEqual(5, layer.Restored);
        }

        [Test]
        public void Extension_Capture_WritesSnapshotWithoutDisk()
        {
            (SaveLoadService service, FakeExtension layer) = Create();
            layer.Value = 3;

            service.Capture();

            Assert.IsTrue(service.TryGetSection(FakeExtension.KEY, out SaveSection section));
            Assert.AreEqual(3, (int)section.Data);
            Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE));
        }

        [Test]
        public void Extension_CheckRefuses_NothingAppliedAndReadOnly()
        {
            SaveValue(5, other: 7);
            (SaveLoadService reader, FakeExtension layer) = Create();
            layer.CheckResult = LoadOutcome.SceneNotFound;
            var other = new FlakySerializer("other");
            reader.Register(other);

            LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
            Assert.AreEqual(LoadOutcome.SceneNotFound, reader.Load());

            Assert.AreEqual(0, other.Value);
            Assert.AreEqual(0, layer.Begins);
            Assert.IsTrue(reader.IsReadOnly);
        }

        [Test]
        public void Extension_LoadHeldOpen_LoadingUntilFinished()
        {
            SaveValue(5);
            (SaveLoadService reader, FakeExtension layer) = Create();
            layer.FinishAtOnce = false;
            int loadedEvents = 0;
            reader.Loaded += _ => loadedEvents++;

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.IsTrue(reader.IsLoading);
            Assert.AreEqual(SaveOutcome.Blocked, reader.Save());
            Assert.AreEqual(0, loadedEvents);

            layer.Finish();

            Assert.IsFalse(reader.IsLoading);
            Assert.AreEqual(1, loadedEvents);
        }

        [Test]
        public void Extension_EmptySlot_NotAskedToLoad()
        {
            (SaveLoadService reader, FakeExtension layer) = Create();

            Assert.AreEqual(LoadOutcome.Empty, reader.Load());
            Assert.AreEqual(0, layer.Begins);
        }

        [Test]
        public void Extension_CaptureThrows_SaveGoesOnWithPreviousSection()
        {
            (SaveLoadService service, FakeExtension layer) = Create();
            layer.Value = 1;
            service.Save();
            layer.Value = 2;
            layer.FailCapture = true;

            LogAssert.Expect(LogType.Error, new Regex("scene sections failed"));
            Assert.AreEqual(SaveOutcome.Saved, service.Save());

            Assert.IsTrue(service.TryGetSection(FakeExtension.KEY, out SaveSection section));
            Assert.AreEqual(1, (int)section.Data);
        }
    }
}

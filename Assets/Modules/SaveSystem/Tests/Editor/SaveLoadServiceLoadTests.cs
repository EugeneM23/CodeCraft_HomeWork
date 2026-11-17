using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Loading that never loses data: backups, corrupted files, newer saves, migrations, storage failures.</summary>
    public class SaveLoadServiceLoadTests
    {
        private const string NEWER_PROGRESS = @"{
  ""formatVersion"": 1,
  ""gameVersion"": ""9.0"",
  ""sections"": {
    ""progress"": { ""v"": 7, ""data"": { ""level"": 8 } },
    ""other"": { ""v"": 1, ""data"": 5 }
  }
}";

        private const string NEWER_FORMAT = @"{ ""formatVersion"": 2, ""gameVersion"": ""9.0"", ""chunks"": [] }";

        private const string STEPS_V1 = @"{
  ""formatVersion"": 1,
  ""gameVersion"": ""0.9"",
  ""sections"": {
    ""steps"": { ""v"": 1, ""data"": {} },
    ""other"": { ""v"": 1, ""data"": 5 }
  }
}";

        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private SaveLoadService CreateService() => new(_storage, "1.0");

        private void SaveProgress(int level)
        {
            SaveLoadService writer = CreateService();
            writer.Register(new ProgressSerializer(new Progress { Level = level, Name = "Cave" }));
            writer.Save();
        }

        private (SaveLoadService Service, Progress Progress) CreateReader()
        {
            SaveLoadService reader = CreateService();
            var progress = new Progress();
            reader.Register(new ProgressSerializer(progress));
            return (reader, progress);
        }

        private static string LoadGolden()
        {
            string[] guids = AssetDatabase.FindAssets("SaveSystemGolden_v1");
            Assert.AreEqual(1, guids.Length, "the golden file SaveSystemGolden_v1.json must exist exactly once");
            return AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[0])).text;
        }

        // ---------- Broken files ----------

        [Test]
        public void SaveLoadService_BrokenMain_RecoversFromBackup()
        {
            SaveProgress(1);
            SaveProgress(2);
            _storage.Put(0, SaveFiles.SAVE, "{ broken");
            (SaveLoadService reader, Progress progress) = CreateReader();

            LogAssert.Expect(LogType.Warning, new Regex("restored from its backup"));
            Assert.AreEqual(LoadOutcome.Recovered, reader.Load());

            Assert.AreEqual(1, progress.Level);
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX));
        }

        [Test]
        public void SaveLoadService_AfterRecovery_NextSaveWritesMainFile()
        {
            SaveProgress(1);
            SaveProgress(2);
            _storage.Put(0, SaveFiles.SAVE, "{ broken");
            (SaveLoadService reader, _) = CreateReader();
            LogAssert.Expect(LogType.Warning, new Regex("restored from its backup"));
            reader.Load();

            Assert.AreEqual(SaveOutcome.Saved, reader.Save(onlyIfChanged: true));
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE));
        }

        [Test]
        public void SaveLoadService_BothBroken_CorruptedAndStartsOver()
        {
            _storage.Put(0, SaveFiles.SAVE, "{ broken");
            _storage.Put(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX, "also broken");
            (SaveLoadService reader, Progress progress) = CreateReader();

            LogAssert.Expect(LogType.Warning, new Regex("is corrupted"));
            Assert.AreEqual(LoadOutcome.Corrupted, reader.Load());

            Assert.AreEqual(0, progress.Level);
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX));
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX + SaveFiles.CORRUPT_SUFFIX));
            Assert.AreEqual(SaveOutcome.Saved, reader.Save());
        }

        // ---------- Newer saves ----------

        [Test]
        public void SaveLoadService_NewerFormat_RefusedAndNeverOverwritten()
        {
            _storage.Put(0, SaveFiles.SAVE, NEWER_FORMAT);
            (SaveLoadService reader, _) = CreateReader();

            LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
            Assert.AreEqual(LoadOutcome.NewerVersion, reader.Load());

            Assert.IsTrue(reader.IsReadOnly);
            Assert.AreEqual(SaveOutcome.Blocked, reader.Save());
            _storage.TryRead(0, SaveFiles.SAVE, out string text);
            Assert.AreEqual(NEWER_FORMAT, text);
        }

        [Test]
        public void SaveLoadService_NewerSection_NothingApplied()
        {
            _storage.Put(0, SaveFiles.SAVE, NEWER_PROGRESS);
            (SaveLoadService reader, Progress progress) = CreateReader();
            var other = new FlakySerializer("other");
            reader.Register(other);

            LogAssert.Expect(LogType.Error, new Regex("newer than 2"));
            Assert.AreEqual(LoadOutcome.NewerVersion, reader.Load());

            Assert.AreEqual(0, progress.Level);
            Assert.AreEqual(0, other.Value, "nothing is applied when one section cannot be read");
        }

        [Test]
        public void SaveLoadService_SelectSlotAfterRefusal_ClearsReadOnly()
        {
            _storage.Put(0, SaveFiles.SAVE, NEWER_FORMAT);
            (SaveLoadService reader, _) = CreateReader();
            LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
            reader.Load();

            reader.SelectSlot(0);

            Assert.IsFalse(reader.IsReadOnly);
            Assert.AreEqual(SaveOutcome.Saved, reader.Save());
        }

        [Test]
        public void SaveLoadService_RegisterNewerSection_SlotBecomesReadOnly()
        {
            _storage.Put(0, SaveFiles.SAVE, NEWER_PROGRESS);
            SaveLoadService reader = CreateService();
            Assert.AreEqual(LoadOutcome.Loaded, reader.Load(), "nothing registered yet: nothing to refuse");

            LogAssert.Expect(LogType.Error, new Regex("becomes read-only"));
            reader.Register(new ProgressSerializer(new Progress()));

            Assert.IsTrue(reader.IsReadOnly);
            Assert.AreEqual(SaveOutcome.Blocked, reader.Save());
        }

        // ---------- Migrations ----------

        [Test]
        public void SaveLoadService_OlderSection_MigratesStepByStep()
        {
            _storage.Put(0, SaveFiles.SAVE, STEPS_V1);
            SaveLoadService reader = CreateService();
            var steps = new StepSerializer("steps");
            reader.Register(steps);

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            CollectionAssert.AreEqual(new[] { 1, 2 }, steps.Steps);
            Assert.IsTrue((bool)steps.Restored["step1"] && (bool)steps.Restored["step2"]);
        }

        [Test]
        public void SaveLoadService_LegacyProgress_MigratesToCurrent()
        {
            SaveLoadService legacy = CreateService();
            legacy.Register(new LegacyProgressSerializer(new Progress { Level = 6 }));
            legacy.Save();
            (SaveLoadService reader, Progress progress) = CreateReader();

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.AreEqual(6, progress.Level);
        }

        [Test]
        public void SaveLoadService_MigrationThrows_NothingAppliedAndReadOnly()
        {
            _storage.Put(0, SaveFiles.SAVE, STEPS_V1);
            SaveLoadService reader = CreateService();
            reader.Register(new StepSerializer("steps") { FailUpgrade = true });
            var other = new FlakySerializer("other");
            reader.Register(other);

            LogAssert.Expect(LogType.Error, new Regex("Migrating 'steps' from version 1 failed"));
            Assert.AreEqual(LoadOutcome.MigrationFailed, reader.Load());

            Assert.AreEqual(0, other.Value);
            Assert.IsTrue(reader.IsReadOnly);
        }

        [Test]
        public void SaveLoadService_Migrated_KeepsOriginalCopyOnce()
        {
            SaveLoadService legacy = CreateService();
            legacy.Register(new LegacyProgressSerializer(new Progress { Level = 1 }));
            legacy.Save();
            _storage.TryRead(0, SaveFiles.SAVE, out string original);

            CreateReader().Service.Load();
            SaveLoadService legacyAgain = CreateService();
            legacyAgain.Register(new LegacyProgressSerializer(new Progress { Level = 2 }));
            legacyAgain.Save();
            CreateReader().Service.Load();

            Assert.IsTrue(_storage.TryRead(0, SaveFiles.PREMIGRATION, out string copy));
            Assert.AreEqual(original, copy, "the copy is made before the first migration only");
        }

        // ---------- Storage failures ----------

        [Test]
        public void SaveLoadService_WriteFails_FailedAndRetriedLater()
        {
            var storage = new FailingStorage();
            var service = new SaveLoadService(storage, "1.0");
            var value = new FlakySerializer("value") { Value = 1 };
            service.Register(value);
            service.Save();

            value.Value = 2;
            storage.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Saving slot 0 failed"));
            Assert.AreEqual(SaveOutcome.Failed, service.Save());

            var reader = new SaveLoadService(storage, "1.0");
            var read = new FlakySerializer("value");
            reader.Register(read);
            reader.Load();
            Assert.AreEqual(1, read.Value, "the file on disk is the previous one");

            storage.FailWrites = false;
            Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));
        }

        [Test]
        public void SaveLoadService_ReadFails_FailedAndReadOnly()
        {
            var storage = new FailingStorage { FailReads = true };
            var service = new SaveLoadService(storage, "1.0");

            LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
            Assert.AreEqual(LoadOutcome.Failed, service.Load());

            Assert.IsTrue(service.IsReadOnly);
        }

        // ---------- Golden file ----------

        [Test]
        public void SaveLoadService_GoldenV1_LoadsExpectedValues()
        {
            _storage.Put(0, SaveFiles.SAVE, LoadGolden());
            (SaveLoadService reader, Progress progress) = CreateReader();

            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            Assert.AreEqual(4, progress.Level);
            Assert.AreEqual("Cave", progress.Name);
            Assert.AreEqual(new Vector3(1.5f, -2f, 0f), progress.Position);
        }

        [Test]
        public void SaveLoadService_GoldenV1_UnknownSectionSurvivesSave()
        {
            _storage.Put(0, SaveFiles.SAVE, LoadGolden());
            (SaveLoadService reader, _) = CreateReader();
            reader.Load();

            reader.Save();

            _storage.TryRead(0, SaveFiles.SAVE, out string text);
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            JToken unknown = json["sections"]["unknown-system"];
            Assert.AreEqual(3, (int)unknown["v"]);
            Assert.IsTrue(JToken.DeepEquals(new JArray(1, 2, 3), unknown["data"]["anything"]));
        }
    }
}

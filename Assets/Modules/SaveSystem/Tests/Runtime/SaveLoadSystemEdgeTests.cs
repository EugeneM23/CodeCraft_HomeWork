using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// The component at its edges: calls passed to the service, buttons ignored during a load, server calls without
    /// a server, a failing storage, and storages set up the way only the Inspector can.
    /// </summary>
    public class SaveLoadSystemEdgeTests
    {
        /// <summary>A storage whose every write fails, like a full disk.</summary>
        private sealed class FullDiskStorage : ISaveStorage
        {
            private readonly InMemorySaveStorage _inner = new();

            public IReadOnlyList<int> ListSlots() => _inner.ListSlots();
            public bool TryRead(int slot, string file, out string text) => _inner.TryRead(slot, file, out text);
            public bool TryReadBackup(int slot, string file, out string text) => _inner.TryReadBackup(slot, file, out text);
            public void Write(int slot, string file, string text) => throw new IOException("disk full");
            public void Quarantine(int slot, string file, bool backup) => _inner.Quarantine(slot, file, backup);
            public void DeleteSlot(int slot) => _inner.DeleteSlot(slot);
        }

        private readonly List<Object> _objects = new();
        private readonly List<string> _folders = new();
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _objects)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _objects.Clear();
            foreach (string folder in _folders)
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }

            _folders.Clear();
        }

        private SaveLoadSystem Bare(string name = "SaveSystem")
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<SaveLoadSystem>();
        }

        private SaveLoadSystem Create(string name = "SaveSystem", ISaveStorage storage = null)
        {
            SaveLoadSystem system = Bare(name);
            system.UseStorage(storage ?? _storage);
            system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            return system;
        }

        private FolderRemoteSaveStorage CreateRemote()
        {
            string folder = Path.Combine(Path.GetTempPath(), "SaveEdgeRemote_" + Guid.NewGuid().ToString("N"));
            _folders.Add(folder);
            return new FolderRemoteSaveStorage(folder);
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _objects.Add(asset);
            return asset;
        }

        // ---------- Calls passed to the service ----------

        [Test]
        public void SaveLoadSystem_ServiceCalls_ReachTheService()
        {
            SaveLoadSystem system = Create();
            int coins = 4;
            DelegateSerializer<int> serializer = system.Register("coins", () => coins, value => coins = value);
            system.Summary["chapter"] = "2";
            system.SlotName = "Hero";

            Assert.AreEqual(SaveOutcome.Saved, system.Save());

            Assert.AreEqual("Hero", system.SlotName);
            Assert.IsTrue(system.HasSlot(0));
            Assert.AreEqual("Hero", system.GetSlot(0).Name);
            Assert.AreEqual("2", system.GetSlot(0).Summary["chapter"]);
            Assert.IsTrue(system.CopySlot(0, 1));
            Assert.IsTrue(system.HasSlot(1));
            string package = system.ExportSlot(0);
            Assert.IsNotNull(package);
            system.ImportSlot(2, package);
            Assert.IsTrue(system.HasSlot(2));

            system.Unregister(serializer);
            Assert.DoesNotThrow(() => system.Register("coins", () => coins, value => coins = value));
            system.Unregister("coins");
            Assert.DoesNotThrow(() => system.Register("coins", () => coins, value => coins = value));
        }

        [Test]
        public void SaveLoadSystem_IdleCopy_SharesTheLiveSystemsSync()
        {
            SaveLoadSystem first = Bare();
            first.UseStorage(_storage);
            first.UseRemote(CreateRemote(), uploadAfterSave: false);
            first.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            SaveLoadSystem copy = Create("Copy", new InMemorySaveStorage());

            Assert.IsTrue(copy.IsIdle);
            Assert.IsNotNull(first.Sync);
            Assert.AreSame(first.Sync, copy.Sync);
        }

        // ---------- Buttons ----------

        [Test]
        public void SaveLoadSystem_ContinueWithoutSlots_StartsNewGame()
        {
            SaveLoadSystem system = Create();
            string loadedScene = null;
            system.LoadSceneHandler = scene => loadedScene = scene;
            system.Values.Set("coins", 5);

            system.ContinueGame();

            Assert.IsNotNull(loadedScene);
            Assert.AreEqual(0, system.CurrentSlot);
            Assert.AreEqual(0, system.Values.Count, "a new game starts empty");
        }

        [Test]
        public void SaveLoadSystem_ButtonsDuringLoad_IgnoredWithWarning()
        {
            var settings = Asset<HeldLoadExtensionAsset>();
            SaveLoadSystem system = Create();
            system.UseSettings(settings);
            system.Values.Set("coins", 1);
            system.SaveGame();
            bool sceneLoaded = false;
            system.LoadSceneHandler = _ => sceneLoaded = true;
            system.Load();
            Assert.IsTrue(system.IsLoading, "the layer holds the load open");

            LogAssert.Expect(LogType.Warning, new Regex("SaveToSlot is ignored: a load is in progress"));
            LogAssert.Expect(LogType.Warning, new Regex("LoadFromSlot is ignored: a load is in progress"));
            LogAssert.Expect(LogType.Warning, new Regex("NewGameInSlot is ignored: a load is in progress"));
            system.SaveToSlot(3);
            system.LoadFromSlot(3);
            system.NewGameInSlot(4);

            Assert.AreEqual(0, system.CurrentSlot);
            Assert.IsFalse(system.HasSlot(3));
            Assert.IsFalse(sceneLoaded);
            settings.Created.Finish();
            Assert.IsFalse(system.IsLoading);
            Assert.AreEqual(1, system.Values.Get("coins", -1));
        }

        // ---------- Server ----------

        [Test]
        public void SaveLoadSystem_NoServer_UploadAndDownloadWarn()
        {
            SaveLoadSystem system = Create();

            LogAssert.Expect(LogType.Warning, new Regex("UploadSlot needs a server"));
            LogAssert.Expect(LogType.Warning, new Regex("DownloadAndLoadSlot needs a server"));
            system.UploadSlot(0);
            system.DownloadAndLoadSlot(0);

            Assert.IsNull(system.Sync);
        }

        [UnityTest]
        public IEnumerator SaveLoadSystem_DownloadAndLoadSlot_LoadsTheServerCopy()
        {
            FolderRemoteSaveStorage remote = CreateRemote();
            SaveLoadSystem system = Bare();
            system.UseStorage(_storage);
            system.UseRemote(remote, uploadAfterSave: false);
            system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            system.Values.Set("coins", 5);
            system.SaveGame();
            system.UploadSlot(0);
            yield return TestScenes.WaitFor(() => remote.ListAsync().Result.Count == 1);
            system.Values.Set("coins", 1);
            system.SaveGame();

            system.DownloadAndLoadSlot(0);
            yield return TestScenes.WaitFor(() => system.Values.Get("coins", -1) == 5);

            Assert.AreEqual(0, system.CurrentSlot);
            Assert.IsFalse(system.IsReadOnly, "the downloaded slot was loaded, so saving is allowed again");
        }

        // ---------- Failing storage ----------

        [Test]
        public void SaveLoadSystem_StorageFails_OnFailedTellsWhy()
        {
            SaveLoadSystem system = Create(storage: new FullDiskStorage());
            string reason = null;
            int saved = 0;
            system.OnFailed.AddListener(text => reason = text);
            system.OnSaved.AddListener(() => saved++);

            LogAssert.Expect(LogType.Error, new Regex("Saving slot 0 failed: disk full"));
            system.SaveGame();

            Assert.AreEqual("Saving slot 0 failed.", reason);
            Assert.AreEqual(0, saved);
        }

        // ---------- Configured storages ----------

        [Test]
        public void SaveLoadSystem_CustomStorageWithoutAsset_ErrorAndFiles()
        {
            SaveLoadSystem system = Bare();
            system.ConfigureStorage(SaveStorageKind.Custom);

            LogAssert.Expect(LogType.Error, new Regex("custom storage but has no SaveStorageAsset"));
            ISaveStorage storage = system.CreateConfiguredStorage();

            Assert.IsInstanceOf<TransformSaveStorage>(storage);
        }

#if UNITY_EDITOR
        private static void SetField(SaveLoadSystem system, string field, Action<UnityEditor.SerializedProperty> set)
        {
            var serialized = new UnityEditor.SerializedObject(system);
            set(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void SaveLoadSystem_CustomStorageAsset_SavesThroughIt()
        {
            var asset = Asset<MemoryStorageAsset>();
            SaveLoadSystem system = Bare();
            system.ConfigureStorage(SaveStorageKind.Custom);
            SetField(system, "_customStorage", property => property.objectReferenceValue = asset);
            system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            system.Values.Set("coins", 2);

            system.SaveGame();

            Assert.IsTrue(asset.Storage.TryRead(0, SaveFiles.SAVE, out string text));
            StringAssert.Contains("coins", text, "no steps are on: the text is stored as it is");
        }

        [Test]
        public void SaveLoadSystem_EncryptionOnWithoutKey_ErrorAndPlainText()
        {
            SaveLoadSystem system = Bare();
            SetField(system, "_encrypt", property => property.boolValue = true);
            SetField(system, "_encryptionKey", property => property.stringValue = string.Empty);

            LogAssert.Expect(LogType.Error, new Regex("Encryption is on but the key is empty"));
            var storage = (TransformSaveStorage)system.CreateConfiguredStorage();

            Assert.AreEqual("{ \"coins\": 1 }", storage.Encode("{ \"coins\": 1 }"));
        }

        [Test]
        public void SaveLoadSystem_KeyKeptEncryptionOff_ReadsEncryptedWritesPlain()
        {
            const string text = "{ \"coins\": 1 }";
            SaveLoadSystem writer = Bare("Writer");
            writer.ConfigureStorage(SaveStorageKind.Files, encryptionKey: "key");
            string encrypted = ((TransformSaveStorage)writer.CreateConfiguredStorage()).Encode(text);
            SaveLoadSystem reader = Bare("Reader");
            reader.ConfigureStorage(SaveStorageKind.Files, encryptionKey: "key");
            SetField(reader, "_encrypt", property => property.boolValue = false);

            var storage = (TransformSaveStorage)reader.CreateConfiguredStorage();

            StringAssert.StartsWith(TransformSaveStorage.MARKER + "aes:", encrypted);
            Assert.AreEqual(text, storage.Decode(encrypted));
            Assert.AreEqual(text, storage.Encode(text));
        }
#endif
    }
}

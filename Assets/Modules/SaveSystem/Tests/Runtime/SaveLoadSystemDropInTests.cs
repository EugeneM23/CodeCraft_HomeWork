using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>The save system as a drop-in: Current, one system across scenes, button methods, events, storages.</summary>
    public class SaveLoadSystemDropInTests
    {
        private readonly List<GameObject> _objects = new();
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _objects)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _objects.Clear();
        }

        private SaveLoadSystem Create(string name = "SaveSystem", Transform parent = null, ISaveStorage storage = null)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            if (parent != null)
                go.transform.SetParent(parent);

            var system = go.AddComponent<SaveLoadSystem>();
            if (storage != null || parent == null)
                system.UseStorage(storage ?? _storage);

            system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            return system;
        }

        [Test]
        public void Current_FirstSystem_ClearedOnDestroy()
        {
            SaveLoadSystem system = Create();

            Assert.AreSame(system, SaveLoadSystem.Current);
            Object.DestroyImmediate(system.gameObject);
            Assert.IsNull(SaveLoadSystem.Current);
        }

        [UnityTest]
        public IEnumerator KeepAcrossScenes_RootMovesToDontDestroyOnLoad_CopyStaysIdle()
        {
            SaveLoadSystem first = Create();
            SaveLoadSystem copy = Create("Copy");
            yield return null;

            Assert.AreEqual("DontDestroyOnLoad", first.gameObject.scene.name);
            Assert.IsTrue(copy != null, "the copy stays in its scene: its buttons and events forward to the first one");
            Assert.AreNotEqual("DontDestroyOnLoad", copy.gameObject.scene.name);
            Assert.AreSame(first, SaveLoadSystem.Current);
            Assert.IsFalse(first.IsIdle);
            Assert.IsTrue(copy.IsIdle);
            Assert.IsFalse(copy.enabled, "no autosave, no load on start: it would write its empty slot over the real one");
        }

        [Test]
        public void Copy_ForwardsCallsToTheLiveSystem()
        {
            SaveLoadSystem first = Create();
            SaveLoadSystem copy = Create("Copy", storage: new InMemorySaveStorage());

            copy.Values.Set("coins", 4);
            copy.SaveToSlot(2);

            Assert.AreEqual(4, first.Values.Get("coins", -1));
            Assert.AreEqual(2, first.CurrentSlot);
            Assert.IsTrue(_storage.Has(2, SaveFiles.SAVE), "the live system's storage, not the copy's");
            Assert.AreEqual(first.GetSlots().Count, copy.GetSlots().Count);

            first.Values.Set("coins", 0);
            copy.LoadFromSlot(2);
            Assert.AreEqual(4, first.Values.Get("coins", -1));
        }

        [Test]
        public void Copy_RaisesItsOwnUnityEventsForTheLiveSystem()
        {
            SaveLoadSystem first = Create();
            SaveLoadSystem copy = Create("Copy");
            int saved = 0, loaded = 0, slots = 0;
            copy.OnSaved.AddListener(() => saved++);
            copy.OnLoaded.AddListener(() => loaded++);
            copy.OnSlotsChanged.AddListener(() => slots++);

            first.SaveGame();
            copy.LoadGame();

            Assert.AreEqual(1, saved, "a \"Saved!\" label wired to the scene's copy shows");
            Assert.AreEqual(1, loaded);
            Assert.AreEqual(1, slots);
        }

        [Test]
        public void Copy_NewGameUsesTheLiveSystemsSettings()
        {
            SaveLoadSystem first = Create();
            SaveLoadSystem copy = Create("Copy");
            string loadedScene = null;
            first.LoadSceneHandler = scene => loadedScene = scene;
            first.SaveGame();

            copy.NewGame();
            copy.DeleteAllSlots();

            Assert.IsNotNull(loadedScene);
            Assert.AreEqual(1, first.CurrentSlot);
            CollectionAssert.IsEmpty(first.GetSlots());
        }

        [UnityTest]
        public IEnumerator Child_StaysInPlace_SecondChildIdlesQuietly()
        {
            // A services object the game keeps itself
            var services = new GameObject("Services");
            Object.DontDestroyOnLoad(services);
            _objects.Add(services);
            SaveLoadSystem child = Create(parent: services.transform, storage: _storage);
            SaveLoadSystem second = Create("Second", services.transform, new InMemorySaveStorage());
            yield return null;

            Assert.AreSame(services.transform, child.transform.parent, "a child is moved by its root, not by itself");
            Assert.IsTrue(second != null);
            Assert.AreSame(child, SaveLoadSystem.Current);
            Assert.IsFalse(child.IsIdle);
            Assert.IsTrue(second.IsIdle);
            Assert.IsFalse(second.enabled);
            second.SaveGame();
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE));
        }

        [UnityTest]
        public IEnumerator OnAnObjectOfTheScene_TheObjectStays_ASystemOfItsOwnTakesOver()
        {
            // Dropped on the level itself: the level must not leave its scene, the save system must outlive it
            var level = new GameObject("Level");
            _objects.Add(level);
            new GameObject("Floor").transform.SetParent(level.transform);
            var system = level.AddComponent<SaveLoadSystem>();
            system.UseStorage(_storage);
            system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            yield return null;

            SaveLoadSystem live = SaveLoadSystem.Current;
            _objects.Add(live.gameObject);
            Assert.AreNotSame(system, live);
            Assert.AreEqual("DontDestroyOnLoad", live.gameObject.scene.name);
            Assert.AreNotEqual("DontDestroyOnLoad", level.scene.name, "the level stays where it is");
            Assert.IsTrue(system.IsIdle);
            Assert.AreSame(system, SaveLoadSystem.OfScene(level.scene), "its scope stays the scene's");

            system.SaveGame(); // a button wired to the scene's component
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE), "the storage given to the scene's component is the live one's");
            Assert.AreEqual(SaveOutcome.Saved, system.LastSave.Outcome);
        }

        [UnityTest]
        public IEnumerator OnAChildOfASceneObject_HandsOverOnStart()
        {
            var manager = new GameObject("Manager");
            _objects.Add(manager);
            SaveLoadSystem child = Create(parent: manager.transform, storage: _storage);
            yield return null;

            _objects.Add(SaveLoadSystem.Current.gameObject);
            Assert.AreNotSame(child, SaveLoadSystem.Current, "its root is not kept by the game: the next scene load would take it");
            Assert.IsTrue(child.IsIdle);
            child.SaveGame();
            Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE));
        }

        [Test]
        public void Buttons_SaveToSlotThenLoadFromSlot()
        {
            SaveLoadSystem system = Create();
            system.Values.Set("coins", 3);

            system.SaveToSlot(2);
            system.Values.Set("coins", 0);
            system.LoadFromSlot(2);

            Assert.AreEqual(2, system.CurrentSlot);
            Assert.AreEqual(3, system.Values.Get("coins", -1));
            Assert.IsTrue(_storage.Has(2, SaveFiles.SAVE));
        }

        [Test]
        public void Buttons_ContinueGame_LoadsLatestSlot()
        {
            SaveLoadSystem system = Create();
            system.Values.Set("coins", 1);
            system.SaveToSlot(4);
            system.SelectSlot(0);

            system.ContinueGame();

            Assert.AreEqual(4, system.CurrentSlot);
            Assert.AreEqual(1, system.Values.Get("coins", -1));
        }

        [Test]
        public void Buttons_NewGame_FreeSlotEmptyAndSceneLoaded()
        {
            SaveLoadSystem system = Create();
            system.Values.Set("coins", 1);
            system.SaveGame();
            string loadedScene = null;
            system.LoadSceneHandler = scene => loadedScene = scene;

            system.NewGame();

            Assert.AreEqual(1, system.CurrentSlot, "slot 0 is taken");
            Assert.AreEqual(0, system.Values.Count);
            Assert.IsNotNull(loadedScene);
        }

        [Test]
        public void Buttons_RenameAndDeleteAll()
        {
            SaveLoadSystem system = Create();
            system.SaveToSlot(0);
            system.SaveToSlot(3);

            system.RenameCurrentSlot("Hero");
            Assert.AreEqual("Hero", system.GetSlot(3).Name);

            system.DeleteAllSlots();
            CollectionAssert.IsEmpty(system.GetSlots());
        }

        [Test]
        public void Events_UnityEventsReportSavesLoadsAndSlots()
        {
            SaveLoadSystem system = Create();
            int saved = 0, loaded = 0, slots = 0;
            system.OnSaved.AddListener(() => saved++);
            system.OnLoaded.AddListener(() => loaded++);
            system.OnSlotsChanged.AddListener(() => slots++);

            system.SaveGame();
            system.LoadGame();
            system.DeleteSlot(0);

            Assert.AreEqual(1, saved);
            Assert.AreEqual(1, loaded);
            Assert.AreEqual(2, slots);
        }

        [Test]
        public void Events_RefusedLoadReportsFailure()
        {
            _storage.Put(0, SaveFiles.SAVE, "{ \"formatVersion\": 99 }");
            SaveLoadSystem system = Create();
            string reason = null;
            system.OnFailed.AddListener(text => reason = text);
            LogAssert.Expect(LogType.Error, new Regex("not loaded"));

            system.LoadGame();

            StringAssert.Contains("NewerVersion", reason);
        }

        [Test]
        public void Storage_PlayerPrefsWithCompressionAndKey_RoundTrips()
        {
            string prefix = "SaveDropInTests_" + Guid.NewGuid().ToString("N");
            var go = new GameObject("Prefs");
            _objects.Add(go);
            var system = go.AddComponent<SaveLoadSystem>();
            system.ConfigureStorage(SaveStorageKind.PlayerPrefs, prefix, compress: true, encryptionKey: "key");
            system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            try
            {
                system.Values.Set("coins", 8);
                system.SaveGame();

                string raw = PlayerPrefs.GetString(prefix + "/slot_0/" + SaveFiles.SAVE);
                StringAssert.StartsWith(TransformSaveStorage.MARKER + "gzip+aes:", raw);
                system.Values.Set("coins", 0);
                system.LoadGame();
                Assert.AreEqual(8, system.Values.Get("coins", -1));
            }
            finally
            {
                system.DeleteAllSlots();
                PlayerPrefs.DeleteKey(prefix + "/index");
            }
        }

        [Test]
        public void Storage_CompressionTurnedOff_OldSavesStillLoad()
        {
            string prefix = "SaveDropInTests_" + Guid.NewGuid().ToString("N");
            SaveLoadSystem PrefsSystem(string name, bool compress, string key)
            {
                var go = new GameObject(name);
                _objects.Add(go);
                var created = go.AddComponent<SaveLoadSystem>();
                created.ConfigureStorage(SaveStorageKind.PlayerPrefs, prefix, compress, key);
                created.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
                return created;
            }

            SaveLoadSystem writer = PrefsSystem("Writer", compress: true, key: "key");
            try
            {
                writer.Values.Set("coins", 6);
                writer.SaveGame();
                Object.DestroyImmediate(writer.gameObject);

                SaveLoadSystem reader = PrefsSystem("Reader", compress: false, key: "key");
                Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
                Assert.AreEqual(6, reader.Values.Get("coins", -1));
            }
            finally
            {
                new PlayerPrefsSaveStorage(prefix + "/").DeleteAll();
            }
        }

        [Test]
        public void Remote_UploadAfterSaveThroughSync()
        {
            var remote = new FolderRemoteSaveStorage(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "SaveDropInRemote_" + Guid.NewGuid().ToString("N")));
            var go = new GameObject("Remote");
            _objects.Add(go);
            var system = go.AddComponent<SaveLoadSystem>();
            system.UseStorage(_storage);
            system.UseRemote(remote);
            system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            try
            {
                system.Values.Set("coins", 2);
                system.SaveGame();

                Assert.IsNotNull(system.Sync);
                CollectionAssert.AreEqual(new[] { 0 }, remote.ListAsync().Result);
            }
            finally
            {
                if (System.IO.Directory.Exists(remote.Root))
                    System.IO.Directory.Delete(remote.Root, recursive: true);
            }
        }
    }
}

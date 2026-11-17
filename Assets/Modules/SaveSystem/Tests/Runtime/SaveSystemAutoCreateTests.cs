using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// A project without a SaveSystem in its scenes: the first Saveable makes one from the settings asset and loads its
    /// slot in place. A system in the scene or made earlier wins; without the asset or with the flag off nothing is made.
    /// The settings come from a hook instead of Resources, the storage is in memory.
    /// </summary>
    public class SaveSystemAutoCreateTests
    {
        private static readonly string MINIMAL_DEMO = TestScenes.MODULE + "/Demo/SaveSystemMinimal.unity";

        private InMemorySaveStorage _storage;
        private SaveSystemSettings _settings;

        [SetUp]
        public void SetUp()
        {
            DestroySystems();
            _storage = new InMemorySaveStorage();
            _settings = TestScenes.CreateSettings();
            Hook();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystemAutoCreate.Reset();
            SaveSystemAutoCreate.FindSettings = () => null; // back off for the rest of the assembly (AutoCreateOff)
            DestroySystems();
            Object.DestroyImmediate(_settings);
        }

        private void Hook()
        {
            SaveSystemAutoCreate.Reset();
            SaveSystemAutoCreate.FindSettings = () => _settings;
            SaveSystemAutoCreate.BeforeStart = system =>
            {
                system.UseStorage(_storage);
                system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            };
        }

        private static void DestroySystems()
        {
            foreach (SaveLoadSystem system in AllSystems())
                Object.DestroyImmediate(system.gameObject);
        }

        private static SaveTestCounter Counter(string name) => TestScenes.Find(name).GetComponent<SaveTestCounter>();

        /// <summary>Points the saved active scene somewhere else, as if the last save was made there.</summary>
        private void MoveSavedActiveScene(string path, string guid)
        {
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            var active = (JObject)json["sections"][SceneSaving.SCENES_KEY]["data"]["active"];
            active["path"] = path;
            active["guid"] = guid;
            _storage.Put(0, SaveFiles.SAVE, json.ToString());
        }

        /// <summary>A save of an earlier run in the open scene, written by a system that is gone again.</summary>
        private void SaveBoxAs(int value)
        {
            var writer = new GameObject("Writer").AddComponent<SaveLoadSystem>();
            writer.UseStorage(_storage);
            writer.UseSettings(_settings);
            writer.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            int current = Counter("Box").Value;
            Counter("Box").Value = value;
            writer.Save();
            Counter("Box").Value = current;
            Object.DestroyImmediate(writer.gameObject);
        }

        /// <summary>What happened to scenes while a step ran: scene loads, and whether a load was still running after it.</summary>
        private sealed class SceneWatch
        {
            public int Loads;
            public bool LoadingAfter;
        }

        /// <summary>
        /// Runs the step and notes right after it whether the live system is still loading (an in-place load is over by
        /// then, a load of the saved scene is not), then waits on the real clock: a scene switch arrives later than a frame.
        /// </summary>
        private static IEnumerator WatchScenes(IEnumerator step, SceneWatch watch)
        {
            void Count(Scene scene, LoadSceneMode mode) => watch.Loads++;
            SceneManager.sceneLoaded += Count;
            try
            {
                yield return step;
                watch.LoadingAfter = SaveLoadSystem.Current != null && SaveLoadSystem.Current.IsLoading;
                yield return new WaitForSecondsRealtime(1f);
            }
            finally
            {
                SceneManager.sceneLoaded -= Count;
            }
        }

        [UnityTest]
        public IEnumerator NoSystem_SceneLoads_OneAutoSystemForAllScenes()
        {
            yield return TestScenes.Load();

            SaveLoadSystem[] systems = AllSystems();
            Assert.AreEqual(1, systems.Length, "one system for all the Saveables of the scene");
            Assert.AreSame(systems[0], SaveLoadSystem.Current);
            Assert.AreEqual(SaveSystemAutoCreate.SYSTEM_NAME, systems[0].name);
            Assert.AreEqual("DontDestroyOnLoad", systems[0].gameObject.scene.name);

            yield return TestScenes.Load();

            Assert.AreSame(systems[0], SaveLoadSystem.Current, "the next scene uses the same system");
            Assert.AreEqual(1, AllSystems().Length);
        }

        [UnityTest]
        public IEnumerator SavedThenRestarted_StateBackWithoutASecondSceneLoad()
        {
            yield return TestScenes.Load();
            Counter("Box").Value = 42;
            Counter("Hidden").Value = 5;
            TestScenes.Find("Box").transform.position = new Vector3(4f, 1f, 0f);
            SaveLoadSystem.Current.Save();
            DestroySystems(); // the game is over

            var watch = new SceneWatch();
            yield return WatchScenes(TestScenes.Load(), watch); // the next run

            Assert.IsFalse(watch.LoadingAfter, "the slot loaded in place, no scene load was running");
            Assert.AreEqual(1, watch.Loads, "the slot loads into the scene, nothing else is loaded");
            Assert.AreEqual(42, Counter("Box").Value);
            Assert.AreEqual(5, Counter("Hidden").Value, "a disabled object gets its data too");
            Assert.AreEqual(new Vector3(4f, 1f, 0f), TestScenes.Find("Box").transform.position);
        }

        [UnityTest]
        public IEnumerator SavedInAnotherScene_StaysHereWithThisScenesState()
        {
            yield return TestScenes.Load();
            Counter("Box").Value = 42;
            SaveLoadSystem.Current.Save();
            MoveSavedActiveScene(MINIMAL_DEMO, TestScenes.GuidOf(MINIMAL_DEMO));
            DestroySystems();

            var watch = new SceneWatch();
            yield return WatchScenes(TestScenes.Load(), watch);

            Assert.IsFalse(watch.LoadingAfter, "no load of the saved scene was running");
            Assert.AreEqual(1, watch.Loads, "no switch to the saved scene");
            Assert.AreEqual(TestScenes.SCENE, SceneManager.GetActiveScene().path);
            Assert.AreEqual(42, Counter("Box").Value, "this scene got its saved state");
        }

        [UnityTest]
        public IEnumerator SavedSceneGone_StillLoadsHere()
        {
            yield return TestScenes.Load();
            Counter("Box").Value = 42;
            SaveLoadSystem.Current.Save();
            MoveSavedActiveScene("Assets/Gone/Level.unity", "00000000000000000000000000000000");
            DestroySystems();

            yield return TestScenes.Load();

            Assert.AreEqual(42, Counter("Box").Value);
            Assert.IsFalse(SaveLoadSystem.Current.IsReadOnly, "a missing saved scene does not refuse the slot");
        }

        [UnityTest]
        public IEnumerator FirstSaveableSpawnedMidScene_SceneNotReloadedKeepsItsState()
        {
            SaveSystemAutoCreate.FindSettings = () => null; // the scene starts without a system
            yield return TestScenes.Load();
            Assert.IsNull(SaveLoadSystem.Current);
            SaveBoxAs(42);
            SaveTestCounter box = Counter("Box");
            box.Value = 3;
            Hook();

            IEnumerator Spawn()
            {
                Object.Instantiate(TestScenes.Prefab); // the first Saveable since the scene started
                yield return null;
            }

            var watch = new SceneWatch();
            yield return WatchScenes(Spawn(), watch);

            Assert.IsNotNull(SaveLoadSystem.Current, "the spawned Saveable made the system");
            Assert.IsFalse(watch.LoadingAfter, "no scene load was running");
            Assert.AreEqual(0, watch.Loads, "no scene was loaded");
            Assert.AreSame(box, Counter("Box"));
            Assert.AreEqual(3, box.Value, "a started scene keeps its state");
        }

        [UnityTest]
        public IEnumerator AfterAutoSystem_Load_ReloadsTheSavedSceneAsBefore()
        {
            yield return TestScenes.Load();
            SaveTestCounter before = Counter("Box");
            before.Value = 42;
            SaveLoadSystem.Current.Save();
            before.Value = 3;

            bool done = false;
            void OnLoaded(LoadOutcome _) => done = true;
            SaveLoadSystem.Current.Loaded += OnLoaded;
            Assert.AreEqual(LoadOutcome.Loaded, SaveLoadSystem.Current.Load());
            yield return TestScenes.WaitFor(() => done);
            SaveLoadSystem.Current.Loaded -= OnLoaded;
            yield return null;

            Assert.AreNotSame(before, Counter("Box"), "a button's Load reloads the saved scene");
            Assert.AreEqual(42, Counter("Box").Value);
        }

        [UnityTest]
        public IEnumerator SystemInTheScene_Wins()
        {
#if UNITY_EDITOR
            void UseMemory(Scene scene, LoadSceneMode mode) => SaveLoadSystem.Current.UseStorage(_storage);
            SceneManager.sceneLoaded += UseMemory;
            Scene demo = EditorSceneManager.LoadSceneInPlayMode(MINIMAL_DEMO, new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestScenes.WaitFor(() => demo.isLoaded);
            SceneManager.sceneLoaded -= UseMemory;
            yield return null;

            Assert.AreEqual(1, AllSystems().Length);
            Assert.AreNotEqual(SaveSystemAutoCreate.SYSTEM_NAME, SaveLoadSystem.Current.name, "the scene's own system is live");
#else
            Assert.Ignore("The demo scene loads only in the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SystemMadeEarlier_Wins()
        {
            var services = new GameObject("[Services]");
            Object.DontDestroyOnLoad(services);
            var own = services.AddComponent<SaveLoadSystem>();
            own.UseStorage(_storage);
            own.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);

            yield return TestScenes.Load();

            Assert.AreSame(own, SaveLoadSystem.Current);
            Assert.AreEqual(1, AllSystems().Length);
        }

        [UnityTest]
        public IEnumerator FlagOff_NoSystem()
        {
            _settings.CreateSystemWhenMissing = false;

            yield return TestScenes.Load();

            Assert.IsNull(SaveLoadSystem.Current);
        }

        [UnityTest]
        public IEnumerator NoSettingsAsset_NoSystemAndOneLookup()
        {
            int lookups = 0;
            SaveSystemAutoCreate.FindSettings = () =>
            {
                lookups++;
                return null;
            };

            yield return TestScenes.Load(); // several Saveables wake
            Object.Instantiate(TestScenes.Prefab);
            yield return null;

            Assert.IsNull(SaveLoadSystem.Current);
            Assert.AreEqual(1, lookups, "the answer is remembered: later Saveables do not look for the asset again");
        }

        [UnityTest]
        public IEnumerator SaveSystemAddedLater_IsACopyThatForwards()
        {
            yield return TestScenes.Load();
            SaveLoadSystem auto = SaveLoadSystem.Current;

            var copy = new GameObject("SaveSystem").AddComponent<SaveLoadSystem>(); // the prefab in a later scene
            copy.UseStorage(new InMemorySaveStorage()); // if forwarding ever broke, the copy must not write real saves
            copy.SaveGame(); // a button wired to the scene's copy

            Assert.IsTrue(copy.IsIdle);
            Assert.AreSame(auto, SaveLoadSystem.Current);
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out _), "the live system wrote the slot");
            Object.DestroyImmediate(copy.gameObject);
        }

        [UnityTest]
        public IEnumerator Registered_ReportsDisabledSaveablesOfALoadingScene()
        {
            var registered = new List<string>();
            void Record(Saveable saveable) => registered.Add(saveable.Id);
            SaveRegistry.Registered += Record;

            yield return TestScenes.Load();

            SaveRegistry.Registered -= Record;
            CollectionAssert.Contains(registered, TestScenes.BOX);
            CollectionAssert.Contains(registered, TestScenes.HIDDEN, "the scan reports a disabled Saveable too");
        }

        [UnityTest]
        public IEnumerator NewLookupAfterAnEarlierNo_CreatesTheSystem()
        {
            SaveSystemAutoCreate.FindSettings = () => null;
            yield return TestScenes.Load(); // no system: the "no" is remembered
            Assert.IsNull(SaveLoadSystem.Current);

            SaveSystemAutoCreate.FindSettings = () => _settings; // no Reset: a new lookup forgets the "no"
            Object.Instantiate(TestScenes.Prefab);
            yield return null;

            Assert.IsNotNull(SaveLoadSystem.Current);
        }

        [UnityTest]
        public IEnumerator BrokenLookup_LoggedAndTheSceneScanCompletes()
        {
            SaveSystemAutoCreate.FindSettings = () => throw new InvalidOperationException("broken settings lookup");
            LogAssert.Expect(LogType.Exception, new Regex("broken settings lookup"));

            yield return TestScenes.Load();

            Assert.IsNull(SaveLoadSystem.Current);
            Assert.IsTrue(SaveRegistry.TryGetRecord(SceneManager.GetActiveScene(), out _),
                "the registry finished the scan of the scene");
        }

        [UnityTest]
        public IEnumerator ThrowingSetup_LoggedAndNoHalfBuiltSystemLeft()
        {
            SaveSystemAutoCreate.BeforeStart = _ => throw new InvalidOperationException("broken setup");
            LogAssert.Expect(LogType.Exception, new Regex("broken setup"));

            yield return TestScenes.Load();
            yield return null; // Destroy takes effect at the end of a frame

            Assert.IsNull(SaveLoadSystem.Current, "the half-built system was removed");
            Assert.AreEqual(0, AllSystems(FindObjectsInactive.Include).Length,
                "destroyed, not just switched off");
        }

        // 6000.5 drops the sort mode (the old overloads are obsolete there); 6000.0 has only them
        private static SaveLoadSystem[] AllSystems(FindObjectsInactive inactive = FindObjectsInactive.Exclude)
        {
#if UNITY_6000_5_OR_NEWER
            return Object.FindObjectsByType<SaveLoadSystem>(inactive);
#else
            return Object.FindObjectsByType<SaveLoadSystem>(inactive, FindObjectsSortMode.None);
#endif
        }
    }
}

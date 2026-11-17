using System.Linq;
using System.Collections;
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
    /// Scene objects through a real save system on the test scene: destroyed, spawned, disabled, untouched objects,
    /// the order against Start, the load phases, in-place loads, DontDestroyOnLoad, callbacks, scene changes.
    /// </summary>
    public class SceneSavingTests
    {
        private InMemorySaveStorage _storage;
        private SaveSystemSettings _settings;
        private GameObject _systemObject;
        private SaveLoadSystem _system;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Load();
            _storage = new InMemorySaveStorage();
            _settings = TestScenes.CreateSettings();
            CreateSystem();
        }

        private void CreateSystem()
        {
            _systemObject = new GameObject("SaveLoadSystem");
            Object.DontDestroyOnLoad(_systemObject); // like [Services]: it outlives the scene reload
            _system = _systemObject.AddComponent<SaveLoadSystem>();
            _system.UseStorage(_storage);
            _system.UseSettings(_settings);
            _system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_systemObject);
            Object.DestroyImmediate(_settings);
        }

        private static SaveTestCounter Counter(string name) => TestScenes.Find(name).GetComponent<SaveTestCounter>();

        private static SaveTestCounter CounterOf(string id) =>
            SaveRegistry.TryGet(id, out Saveable saveable) ? saveable.GetComponent<SaveTestCounter>() : null;

        private IEnumerator LoadAndWait(LoadOutcome expected = LoadOutcome.Loaded)
        {
            bool done = false;
            void OnLoaded(LoadOutcome _) => done = true;
            _system.Loaded += OnLoaded;
            Assert.AreEqual(expected, _system.Load());
            yield return TestScenes.WaitFor(() => done);
            _system.Loaded -= OnLoaded;
            yield return null; // Destroy calls of the load take effect
        }

        private string SceneKey => SceneSaving.SCENE_PREFIX + TestScenes.GuidOf(TestScenes.SCENE);

        [UnityTest]
        public IEnumerator Scene_UserStartRunsFirst_LoadedValueWins()
        {
            Counter("Box").Value = 42;
            _system.Save();
            Counter("Box").Value = 3;

            yield return LoadAndWait();

            Assert.AreEqual(42, Counter("Box").Value);
            Assert.AreEqual(1, Counter("Box").Starts, "the reloaded Box ran its own Start once");
        }

        [UnityTest]
        public IEnumerator Scene_DestroyedObject_StaysDestroyed()
        {
            Object.Destroy(TestScenes.Find("Doomed"));
            yield return null;
            _system.Save();

            yield return LoadAndWait();

            Assert.IsNull(TestScenes.Find("Doomed"));
            Assert.IsNotNull(TestScenes.Find("Box"));
        }

        [UnityTest]
        public IEnumerator Scene_SpawnedPrefab_BackWithIdParentNestedAndData()
        {
            GameObject spawned = Object.Instantiate(TestScenes.Prefab, TestScenes.Find("Holder").transform);
            spawned.transform.localPosition = new Vector3(2f, 3f, 0f);
            yield return null; // its Start resets the counters
            spawned.GetComponent<SaveTestCounter>().Value = 7;
            spawned.transform.Find("Inner").GetComponent<SaveTestCounter>().Value = 8;
            string id = spawned.GetComponent<Saveable>().Id;
            string innerId = spawned.transform.Find("Inner").GetComponent<Saveable>().Id;
            _system.Save();

            yield return LoadAndWait();

            Assert.IsTrue(SaveRegistry.TryGet(id, out Saveable restored));
            Assert.AreEqual("Holder", restored.transform.parent.name);
            Assert.AreEqual(new Vector3(2f, 3f, 0f), restored.transform.localPosition);
            Assert.AreEqual(7, CounterOf(id).Value);
            Assert.AreEqual(8, CounterOf(innerId).Value);
            Assert.AreSame(restored.transform, CounterOf(innerId).transform.parent);
        }

        [UnityTest]
        public IEnumerator Scene_SpawnHandler_MakesTheSavedObject()
        {
            GameObject spawned = Object.Instantiate(TestScenes.Prefab, TestScenes.Find("Holder").transform);
            yield return null;
            spawned.GetComponent<SaveTestCounter>().Value = 7;
            string id = spawned.GetComponent<Saveable>().Id;
            _system.Save();

            int calls = 0;
            GameObject made = null;
            GameObject askedFor = null;
            Transform askedParent = null;
            // A pool: it makes the object where it keeps its own, not under the parent the save names
            SaveLoadSystem.SpawnHandler = (prefab, parent) =>
            {
                calls++;
                askedFor = prefab;
                askedParent = parent;
                made = Object.Instantiate(prefab);
                return made;
            };
            try
            {
                yield return LoadAndWait();
            }
            finally
            {
                SaveLoadSystem.SpawnHandler = null;
            }

            Assert.AreEqual(1, calls);
            Assert.AreSame(TestScenes.Prefab, askedFor);
            Assert.AreEqual("Holder", askedParent.name);
            Assert.IsTrue(SaveRegistry.TryGet(id, out Saveable restored));
            Assert.AreSame(made, restored.gameObject, "the game's object, not a second one");
            Assert.AreEqual("Holder", restored.transform.parent.name, "moved to the saved parent");
            Assert.AreEqual(7, CounterOf(id).Value);
        }

        [UnityTest]
        public IEnumerator Scene_SpawnHandlerReturnsNull_ObjectIsInstantiated()
        {
            GameObject spawned = Object.Instantiate(TestScenes.Prefab, TestScenes.Find("Holder").transform);
            yield return null;
            string id = spawned.GetComponent<Saveable>().Id;
            _system.Save();

            SaveLoadSystem.SpawnHandler = (_, _) => null; // not a prefab the game makes itself
            try
            {
                yield return LoadAndWait();
            }
            finally
            {
                SaveLoadSystem.SpawnHandler = null;
            }

            Assert.IsTrue(SaveRegistry.TryGet(id, out Saveable restored));
            Assert.AreEqual("Holder", restored.transform.parent.name);
        }

        [UnityTest]
        public IEnumerator Scene_SpawnHandlerThrows_ObjectIsSkippedAndTheLoadEnds()
        {
            GameObject spawned = Object.Instantiate(TestScenes.Prefab, TestScenes.Find("Holder").transform);
            yield return null;
            string id = spawned.GetComponent<Saveable>().Id;
            Counter("Box").Value = 42;
            _system.Save();

            SaveLoadSystem.SpawnHandler = (_, _) => throw new System.InvalidOperationException("the pool is gone");
            LogAssert.Expect(LogType.Exception, new Regex("the pool is gone"));
            LogAssert.Expect(LogType.Error, new Regex("SpawnHandler failed"));
            try
            {
                yield return LoadAndWait();
            }
            finally
            {
                SaveLoadSystem.SpawnHandler = null;
            }

            Assert.IsFalse(SaveRegistry.TryGet(id, out _), "a half-made object is worse than a missing one");
            Assert.AreEqual(42, Counter("Box").Value, "the rest of the scene is restored");
        }

        [UnityTest]
        public IEnumerator Live_SceneStaysLoaded_ObjectsGoBackToTheSave()
        {
            Transform holder = TestScenes.Find("Holder").transform;
            GameObject kept = Object.Instantiate(TestScenes.Prefab, holder);
            GameObject lost = Object.Instantiate(TestScenes.Prefab, holder);
            yield return null;
            kept.GetComponent<SaveTestCounter>().Value = 7;
            string keptId = kept.GetComponent<Saveable>().Id;
            string lostId = lost.GetComponent<Saveable>().Id;
            GameObject box = TestScenes.Find("Box");
            Counter("Box").Value = 42;
            _system.Save();

            // The game goes on: values change, a saved object dies, a new one appears
            Counter("Box").Value = 3;
            kept.GetComponent<SaveTestCounter>().Value = 1;
            Object.Destroy(lost);
            GameObject late = Object.Instantiate(TestScenes.Prefab);
            yield return null;
            string lateId = late.GetComponent<Saveable>().Id;

            _system.LiveLoad = true;
            yield return LoadAndWait();

            Assert.AreSame(box, TestScenes.Find("Box"), "the scene was not loaded again");
            Assert.AreEqual(1, Counter("Box").Starts);
            Assert.AreEqual(42, Counter("Box").Value);
            Assert.IsTrue(SaveRegistry.TryGet(keptId, out Saveable keptNow));
            Assert.AreSame(kept, keptNow.gameObject, "an object alive in the save and now keeps its instance");
            Assert.AreEqual(7, CounterOf(keptId).Value);
            Assert.IsTrue(SaveRegistry.TryGet(lostId, out Saveable back), "a saved object that died since is made again");
            Assert.AreEqual("Holder", back.transform.parent.name);
            Assert.IsTrue(late == null, "an object made after the save is removed");
            Assert.IsFalse(SaveRegistry.TryGet(lateId, out _));
        }

        [UnityTest]
        public IEnumerator Live_ObjectTheGameMadeAgainItself_TakesTheSavedIdInsteadOfBeingReplaced()
        {
            Transform holder = TestScenes.Find("Holder").transform;
            GameObject first = Object.Instantiate(TestScenes.Prefab, holder);
            yield return null;
            first.GetComponent<SaveTestCounter>().Value = 7;
            string savedId = first.GetComponent<Saveable>().Id;
            _system.Save();

            // The game throws the object away and builds it again in the same place (a weapon given anew): the same
            // thing as far as the game goes, another id as far as the save goes
            Object.Destroy(first);
            GameObject again = Object.Instantiate(TestScenes.Prefab, holder);
            yield return null;
            Assert.AreNotEqual(savedId, again.GetComponent<Saveable>().Id);

            _system.LiveLoad = true;
            yield return LoadAndWait();

            Assert.IsTrue(again != null, "what the game made is kept, not removed as made after the save");
            Assert.IsTrue(SaveRegistry.TryGet(savedId, out Saveable restored));
            Assert.AreSame(again, restored.gameObject, "it stands for the saved object");
            Assert.AreEqual(7, CounterOf(savedId).Value);
            Assert.AreEqual(1, holder.Cast<Transform>().Count(child => child.name.StartsWith(TestScenes.Prefab.name)), "and no second copy is made");
        }

        [UnityTest]
        public IEnumerator Live_SceneObjectDestroyedSinceTheSave_SceneIsLoadedAgain()
        {
            GameObject box = TestScenes.Find("Box");
            Counter("Box").Value = 42;
            _system.Save();
            Object.Destroy(box);
            yield return null;

            _system.LiveLoad = true;
            yield return LoadAndWait();

            Assert.IsNotNull(TestScenes.Find("Box"), "only the scene file can bring it back");
            Assert.AreEqual(42, Counter("Box").Value);
        }

        [UnityTest]
        public IEnumerator Live_DespawnHandler_TakesWhatWasMadeAfterTheSave()
        {
            _system.Save();
            GameObject late = Object.Instantiate(TestScenes.Prefab);
            yield return null;

            GameObject asked = null;
            // A pool: the object is switched off and kept
            SaveLoadSystem.DespawnHandler = target =>
            {
                asked = target;
                target.SetActive(false);
                return true;
            };
            _system.LiveLoad = true;
            try
            {
                yield return LoadAndWait();
            }
            finally
            {
                SaveLoadSystem.DespawnHandler = null;
            }

            Assert.AreSame(late, asked);
            Assert.IsTrue(late != null, "taken by the game: not destroyed");
            Assert.IsFalse(late.activeSelf);
            Object.Destroy(late);
        }

        [UnityTest]
        public IEnumerator Live_Off_LoadReloadsTheSceneAsBefore()
        {
            GameObject box = TestScenes.Find("Box");
            Counter("Box").Value = 42;
            _system.Save();
            Assert.IsFalse(_system.LiveLoad, "off unless asked for");

            yield return LoadAndWait();

            Assert.IsTrue(box == null, "the scene was loaded again");
            Assert.AreEqual(42, Counter("Box").Value);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Scenes_AdditiveSceneNextToTheActiveOne_ComesBackOnLoad()
        {
            Scene additive = EditorSceneManager.LoadSceneInPlayMode(TestScenes.WHOLE_SCENE, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return TestScenes.WaitFor(() => additive.isLoaded);
            yield return null;
            Counter("Box").Value = 42;
            _system.Save();
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string json));

            var saved = (JArray)JObject.Parse(json)["sections"][SceneSaving.SCENES_KEY]["data"]["additive"];
            Assert.AreEqual(1, saved.Count);
            Assert.AreEqual(TestScenes.WHOLE_SCENE, (string)saved[0]["path"]);

            yield return LoadAndWait();

            Assert.AreEqual(TestScenes.SCENE, SceneManager.GetActiveScene().path, "the saved scene is the active one again");
            Assert.AreEqual(2, SceneManager.sceneCount, "and the scene loaded next to it is back, once");
            Assert.IsTrue(SceneManager.GetSceneByPath(TestScenes.WHOLE_SCENE).isLoaded);
            Assert.AreEqual(42, Counter("Box").Value);
        }

        [UnityTest]
        public IEnumerator Scenes_OnlyTheActiveOne_NoAdditiveListInTheSave()
        {
            _system.Save();
            yield return null;

            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string json));
            JToken data = JObject.Parse(json)["sections"][SceneSaving.SCENES_KEY]["data"];
            Assert.IsNull(data["additive"], "saves of one-scene games look as before");
        }
#endif

        [UnityTest]
        public IEnumerator Scene_DisabledObject_GetsItsData()
        {
            Counter("Hidden").Value = 5;
            _system.Save();

            yield return LoadAndWait();

            Assert.AreEqual(5, Counter("Hidden").Value);
            Assert.IsFalse(TestScenes.Find("Hidden").activeSelf);
        }

        [UnityTest]
        public IEnumerator Scene_ObjectWithoutData_StaysAsInScene()
        {
            Vector3 scenePosition = TestScenes.Find("Box").transform.position;
            TestScenes.Find("Box").transform.position = new Vector3(5f, 5f, 0f);
            _system.Save();
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            ((JObject)json["sections"][SceneKey]["data"]["objects"]).Remove(TestScenes.BOX);
            _storage.Put(0, SaveFiles.SAVE, json.ToString());

            yield return LoadAndWait();

            Assert.AreEqual(scenePosition, TestScenes.Find("Box").transform.position);
        }

        [UnityTest]
        public IEnumerator Scene_Loaded_RaisedAfterDataApplied()
        {
            Counter("Box").Value = 42;
            _system.Save();
            int valueOnLoaded = -1;
            bool loadingOnLoaded = true;
            _system.Loaded += _ =>
            {
                valueOnLoaded = Counter("Box").Value;
                loadingOnLoaded = _system.IsLoading;
            };

            yield return LoadAndWait();

            Assert.AreEqual(42, valueOnLoaded);
            Assert.IsFalse(loadingOnLoaded);
        }

        [UnityTest]
        public IEnumerator Scene_SaveDuringReload_Blocked()
        {
            _system.Save();
            bool done = false;
            _system.Loaded += _ => done = true;

            _system.Load();
            SaveOutcome during = _system.Save();
            yield return TestScenes.WaitFor(() => done);

            Assert.AreEqual(SaveOutcome.Blocked, during);
        }

        [UnityTest]
        public IEnumerator Scene_UnknownScene_SceneNotFoundAndNothingReloaded()
        {
            _storage.Put(0, SaveFiles.SAVE, "{ \"formatVersion\": 1, \"gameVersion\": \"t\", \"sections\": { \"$scenes\": " +
                                            "{ \"v\": 1, \"data\": { \"active\": { \"path\": \"Assets/Missing.unity\", " +
                                            "\"guid\": \"00000000000000000000000000000000\" } } } } }");
            GameObject box = TestScenes.Find("Box");

            LogAssert.Expect(LogType.Error, new Regex("is not in the build"));
            Assert.AreEqual(LoadOutcome.SceneNotFound, _system.Load());
            yield return null;

            Assert.IsTrue(_system.IsReadOnly);
            Assert.AreSame(box, TestScenes.Find("Box"), "the scene was not reloaded");
        }

        [UnityTest]
        public IEnumerator Scene_NewerComponentData_NewerVersionAndNothingReloaded()
        {
            _system.Save();
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            json["sections"][SceneKey]["data"]["objects"][TestScenes.BOX]["components"]["Transform"]["v"] = 9;
            _storage.Put(0, SaveFiles.SAVE, json.ToString());
            GameObject box = TestScenes.Find("Box");

            LogAssert.Expect(LogType.Error, new Regex("newer than 1"));
            Assert.AreEqual(LoadOutcome.NewerVersion, _system.Load());
            yield return null;

            Assert.AreSame(box, TestScenes.Find("Box"));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Scene_LoadRightAfterSceneLoad_AppliesInPlace()
        {
            Counter("Box").Value = 42;
            _system.Save();
            // A game that has just started: empty snapshot, the save is only on disk
            Object.DestroyImmediate(_systemObject);
            CreateSystem();
            int loads = 0;
            bool done = false;

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != TestScenes.SCENE)
                    return;

                loads++;
                if (loads > 1)
                    return;

                _system.Loaded += _ => done = true;
                _system.Load();
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                EditorSceneManager.LoadSceneInPlayMode(TestScenes.SCENE, new LoadSceneParameters(LoadSceneMode.Single));
                yield return TestScenes.WaitFor(() => done);
                yield return null;
            }
            finally
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            Assert.AreEqual(1, loads, "the saved scene was applied without loading it again");
            Assert.AreEqual(42, Counter("Box").Value);
        }

        [UnityTest]
        public IEnumerator Scene_SceneChange_RestoredFromSnapshot()
        {
            Counter("Box").Value = 42;
            _system.Capture();

            Scene scene = EditorSceneManager.LoadSceneInPlayMode(TestScenes.SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestScenes.WaitFor(() => scene.isLoaded);
            yield return null;

            Assert.AreEqual(42, Counter("Box").Value);
        }
#endif

        [UnityTest]
        public IEnumerator Scene_DontDestroyOnLoad_AliveObjectAndRespawn()
        {
            GameObject keeper = Object.Instantiate(TestScenes.Prefab);
            Object.DontDestroyOnLoad(keeper);
            yield return null;
            string id = keeper.GetComponent<Saveable>().Id;
            keeper.GetComponent<SaveTestCounter>().Value = 9;
            _system.Save();
            keeper.GetComponent<SaveTestCounter>().Value = 1;

            yield return LoadAndWait();
            Assert.AreEqual(9, keeper.GetComponent<SaveTestCounter>().Value, "the live object got its data");

            Object.Destroy(keeper);
            yield return null;
            yield return LoadAndWait();

            Assert.IsTrue(SaveRegistry.TryGet(id, out Saveable respawned));
            Assert.AreEqual("DontDestroyOnLoad", respawned.gameObject.scene.name);
            Assert.AreEqual(9, respawned.GetComponent<SaveTestCounter>().Value);
            Object.Destroy(respawned.gameObject);
        }

        [UnityTest]
        public IEnumerator Scene_Callbacks_BeforeSaveAndAfterLoad()
        {
            _system.Save();
            Assert.AreEqual(1, TestScenes.Find("Box").GetComponent<SaveTestCallbacks>().BeforeSave);

            yield return LoadAndWait();

            Assert.AreEqual(1, TestScenes.Find("Box").GetComponent<SaveTestCallbacks>().AfterLoad);
        }

        [UnityTest]
        public IEnumerator Scene_Save_WritesScenesAndSceneSection()
        {
            _system.Save();

            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            Assert.AreEqual(TestScenes.SCENE, (string)json["sections"][SceneSaving.SCENES_KEY]["data"]["active"]["path"]);
            Assert.IsNotNull(json["sections"][SceneKey]["data"]["objects"][TestScenes.HIDDEN]);
            yield break;
        }

        [UnityTest]
        public IEnumerator Scene_References_ResolvedAfterReload()
        {
            var links = TestScenes.Find("Linker").GetComponent<SaveTestLinks>();
            links.Target = TestScenes.Find("Hidden").GetComponent<Saveable>();
            links.Counter = Counter("Box");
            links.Handle = TestScenes.Find("Box").transform.Find("Handle");
            links.Prefab = TestScenes.Prefab;
            links.Icon = TestScenes.Icon;
            links.Many.Add(TestScenes.Find("Doomed").GetComponent<Saveable>());
            _system.Save();

            yield return LoadAndWait();

            links = TestScenes.Find("Linker").GetComponent<SaveTestLinks>();
            Assert.AreSame(TestScenes.Find("Hidden").GetComponent<Saveable>(), links.Target);
            Assert.AreSame(Counter("Box"), links.Counter);
            Assert.AreSame(TestScenes.Find("Box").transform.Find("Handle"), links.Handle);
            Assert.AreSame(TestScenes.Prefab, links.Prefab);
            Assert.AreSame(TestScenes.Icon, links.Icon);
            Assert.AreSame(TestScenes.Find("Doomed").GetComponent<Saveable>(), links.Many[0]);
        }

        [UnityTest]
        public IEnumerator Scene_ReferenceToSpawned_ResolvedAfterReload()
        {
            GameObject spawned = Object.Instantiate(TestScenes.Prefab);
            yield return null;
            string id = spawned.GetComponent<Saveable>().Id;
            TestScenes.Find("Linker").GetComponent<SaveTestLinks>().Target = spawned.GetComponent<Saveable>();
            _system.Save();

            yield return LoadAndWait();

            Saveable target = TestScenes.Find("Linker").GetComponent<SaveTestLinks>().Target;
            Assert.IsNotNull(target);
            Assert.AreEqual(id, target.Id);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// The objects layer at its edges on the test scene: a scene or prefab missing from the settings, spawned objects
    /// without a prefab, a parent or a nested Saveable that is gone, scene sections that are newer or unreadable, a file
    /// without the active scene, persistent scene objects and callbacks that throw.
    /// </summary>
    public class SceneSavingEdgeTests
    {
        private const string PLAIN_PREFAB = "0e000000000000000000000000000001";
        private const string UNKNOWN_ID = "0e00000000000000000000000000ffff";

        private readonly List<Object> _objects = new();
        private InMemorySaveStorage _storage;
        private SaveLoadSystem _system;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Load();
            _storage = new InMemorySaveStorage();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _objects)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _objects.Clear();
            _system = null;
        }

        private void StartSystem(SaveSystemSettings settings = null)
        {
            settings ??= TestScenes.CreateSettings();
            _objects.Add(settings);
            var go = new GameObject("SaveLoadSystem");
            Object.DontDestroyOnLoad(go); // like [Services]: it outlives the scene reload
            _system = go.AddComponent<SaveLoadSystem>();
            _system.UseStorage(_storage);
            _system.UseSettings(settings);
            _system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            _objects.Insert(0, go); // destroyed before its settings
        }

        private T Track<T>(T created) where T : Object
        {
            _objects.Add(created);
            return created;
        }

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

        private static string SceneKey => SceneSaving.SCENE_PREFIX + TestScenes.GuidOf(TestScenes.SCENE);

        private JObject ReadSave()
        {
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            return json;
        }

        private void WriteSave(JObject json) => _storage.Put(0, SaveFiles.SAVE, json.ToString());

        private static JObject Sections(JObject json) => (JObject)json["sections"];

        private static JArray SpawnedOf(JObject json) => (JArray)json["sections"][SceneKey]["data"]["spawned"];

        /// <summary>Saves with one spawned prefab under the parent; adds the spawned root's id to the list.</summary>
        private IEnumerator SaveSpawned(Transform parent, List<string> id)
        {
            GameObject spawned = Object.Instantiate(TestScenes.Prefab, parent);
            yield return null; // its Start
            id.Add(spawned.GetComponent<Saveable>().Id);
            Assert.AreEqual(SaveOutcome.Saved, _system.Save());
        }

        // ---------- Save ----------

        [UnityTest]
        public IEnumerator SceneLayer_SceneNotInSettings_WarnsOnceAndSavesNoSceneSection()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            settings.SetCatalogs(Track(TestScenes.CreateSettings()).Prefabs, new SaveSystemSettings.SceneEntry[0]);
            StartSystem(settings);
            int warnings = 0;
            void Count(string message, string stack, LogType type)
            {
                if (type == LogType.Warning && message.Contains("is not in SaveSystemSettings"))
                    warnings++;
            }

            Application.logMessageReceived += Count;
            try
            {
                _system.Save();
                _system.Save();
            }
            finally
            {
                Application.logMessageReceived -= Count;
            }

            Assert.AreEqual(1, warnings);
            JObject sections = Sections(ReadSave());
            Assert.IsFalse(sections.Properties().Any(property => property.Name.StartsWith(SceneSaving.SCENE_PREFIX)));
            Assert.IsNull(sections[SceneSaving.SCENES_KEY], "the active scene cannot be named without its GUID");
            yield break;
        }

        [UnityTest]
        public IEnumerator SceneLayer_CopyOfSceneObject_NotSavedWithWarning()
        {
            StartSystem();
            GameObject copy = Object.Instantiate(TestScenes.Find("Box"));
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex("without a prefab id"));
            Assert.AreEqual(SaveOutcome.Saved, _system.Save());

            CollectionAssert.IsEmpty(SpawnedOf(ReadSave()));
            Assert.IsTrue(copy.GetComponent<Saveable>().IsSpawned);
        }

        [UnityTest]
        public IEnumerator SceneLayer_CallbackThrows_LoggedAndSaveGoesOn()
        {
            StartSystem();
            var thrower = Track(new GameObject("Thrower"));
            thrower.AddComponent<Saveable>();
            thrower.AddComponent<SaveTestThrowingCallbacks>();
            TestScenes.Find("Box").GetComponent<SaveTestCounter>().Value = 6;

            LogAssert.Expect(LogType.Exception, new Regex(SaveTestThrowingCallbacks.MESSAGE));
            LogAssert.Expect(LogType.Warning, new Regex("without a prefab id"));
            Assert.AreEqual(SaveOutcome.Saved, _system.Save());

            Assert.AreEqual(6, (int)ReadSave()["sections"][SceneKey]["data"]["objects"][TestScenes.BOX]
                ["components"]["SaveTestCounter"]["data"]["Value"]);
            yield break;
        }

        // ---------- Spawned objects ----------

        [UnityTest]
        public IEnumerator SceneLayer_SpawnedUnderSceneObject_BackUnderTheSameChild()
        {
            StartSystem();
            var ids = new List<string>();
            yield return SaveSpawned(TestScenes.Find("Box").transform.Find("Handle"), ids);
            JObject saved = (JObject)SpawnedOf(ReadSave())[0];
            Assert.AreEqual(TestScenes.BOX, (string)saved["parent"]);
            Assert.AreEqual("Handle", (string)saved["parentPath"]);

            yield return LoadAndWait();

            Assert.IsTrue(SaveRegistry.TryGet(ids[0], out Saveable restored));
            Assert.AreSame(TestScenes.Find("Box").transform.Find("Handle"), restored.transform.parent);
        }

        [UnityTest]
        public IEnumerator SceneLayer_SpawnedParentGone_RestoredAtSceneRoot()
        {
            StartSystem();
            var ids = new List<string>();
            yield return SaveSpawned(TestScenes.Find("Box").transform.Find("Handle"), ids);
            JObject json = ReadSave();
            SpawnedOf(json)[0]["parent"] = UNKNOWN_ID;
            WriteSave(json);

            yield return LoadAndWait();

            Assert.IsTrue(SaveRegistry.TryGet(ids[0], out Saveable restored));
            Assert.IsNull(restored.transform.parent);
            Assert.AreEqual(TestScenes.SCENE, restored.gameObject.scene.path);
        }

        [UnityTest]
        public IEnumerator SceneLayer_PrefabNotInSettings_SkippedWithWarning()
        {
            StartSystem();
            var ids = new List<string>();
            yield return SaveSpawned(null, ids);
            JObject json = ReadSave();
            SpawnedOf(json)[0]["prefab"] = UNKNOWN_ID;
            WriteSave(json);

            LogAssert.Expect(LogType.Warning, new Regex($"The prefab {UNKNOWN_ID} of the spawned object .* is not in SaveSystemSettings"));
            yield return LoadAndWait();

            Assert.IsFalse(SaveRegistry.TryGet(ids[0], out _));
            Assert.IsNotNull(TestScenes.Find("Box"), "the rest of the scene loads");
        }

        [UnityTest]
        public IEnumerator SceneLayer_PrefabLostItsSaveable_SkippedWithError()
        {
            var template = Track(new GameObject("PlainTemplate"));
            template.SetActive(false);
            Object.DontDestroyOnLoad(template);
            SaveSystemSettings settings = TestScenes.CreateSettings();
            settings.SetCatalogs(settings.Prefabs.Append(new SaveSystemSettings.PrefabEntry { Id = PLAIN_PREFAB, Prefab = template }),
                settings.Scenes);
            StartSystem(settings);
            var ids = new List<string>();
            yield return SaveSpawned(null, ids);
            JObject json = ReadSave();
            SpawnedOf(json)[0]["prefab"] = PLAIN_PREFAB;
            WriteSave(json);

            LogAssert.Expect(LogType.Error, new Regex("has no Saveable on its root any more"));
            yield return LoadAndWait();

            Assert.IsFalse(SaveRegistry.TryGet(ids[0], out _));
            Assert.IsNull(TestScenes.Find("PlainTemplate(Clone)"), "the instance is destroyed");
        }

        [UnityTest]
        public IEnumerator SceneLayer_NestedSaveableGone_SkippedWithWarningRootRestored()
        {
            StartSystem();
            var ids = new List<string>();
            yield return SaveSpawned(null, ids);
            JObject json = ReadSave();
            var nested = (JObject)SpawnedOf(json)[0]["nested"];
            JToken inner = nested["Inner"];
            nested.Remove("Inner");
            nested["Gone"] = inner;
            WriteSave(json);

            LogAssert.Expect(LogType.Warning, new Regex("has no Saveable at Gone"));
            yield return LoadAndWait();

            Assert.IsTrue(SaveRegistry.TryGet(ids[0], out Saveable restored));
            Assert.IsNotNull(restored.transform.Find("Inner"));
        }

        // ---------- Refused loads ----------

        [UnityTest]
        public IEnumerator SceneLayer_NewerSceneSection_RefusedAndNothingReloaded()
        {
            StartSystem();
            _system.Save();
            JObject json = ReadSave();
            Sections(json)[SceneKey]["v"] = 2;
            WriteSave(json);
            GameObject box = TestScenes.Find("Box");

            LogAssert.Expect(LogType.Error, new Regex("was saved with version 2, newer than 1"));
            Assert.AreEqual(LoadOutcome.NewerVersion, _system.Load());
            yield return null;

            Assert.IsTrue(_system.IsReadOnly);
            Assert.AreSame(box, TestScenes.Find("Box"));
        }

        [UnityTest]
        public IEnumerator SceneLayer_UnreadableSceneSection_FailedAndNothingReloaded()
        {
            StartSystem();
            _system.Save();
            JObject json = ReadSave();
            Sections(json)[SceneKey]["data"] = "not a scene";
            WriteSave(json);
            GameObject box = TestScenes.Find("Box");

            LogAssert.Expect(LogType.Error, new Regex("cannot be read"));
            Assert.AreEqual(LoadOutcome.Failed, _system.Load());
            yield return null;

            Assert.IsTrue(_system.IsReadOnly);
            Assert.AreSame(box, TestScenes.Find("Box"));
        }

        [UnityTest]
        public IEnumerator SceneLayer_SavedSceneWithoutPath_SceneNotFound()
        {
            StartSystem();
            _storage.Put(0, SaveFiles.SAVE, "{ \"formatVersion\": 1, \"gameVersion\": \"t\", \"sections\": { \"$scenes\": " +
                                            "{ \"v\": 1, \"data\": { \"active\": { \"path\": \"\", " +
                                            "\"guid\": \"" + UNKNOWN_ID + "\" } } } } }");

            LogAssert.Expect(LogType.Error, new Regex("is not in the build"));
            Assert.AreEqual(LoadOutcome.SceneNotFound, _system.Load());
            yield break;
        }

        // ---------- Loads without a scene change ----------

        [UnityTest]
        public IEnumerator SceneLayer_NoActiveSceneSaved_LoadEndsAtOnceWithoutReload()
        {
            StartSystem();
            var counter = TestScenes.Find("Box").GetComponent<SaveTestCounter>();
            counter.Value = 42;
            _system.Save();
            JObject json = ReadSave();
            Sections(json).Remove(SceneSaving.SCENES_KEY);
            WriteSave(json);
            counter.Value = 3;
            GameObject box = TestScenes.Find("Box");
            int loaded = 0;
            _system.Loaded += _ => loaded++;

            Assert.AreEqual(LoadOutcome.Loaded, _system.Load());

            Assert.IsFalse(_system.IsLoading);
            Assert.AreEqual(1, loaded);
            yield return null;
            Assert.AreSame(box, TestScenes.Find("Box"), "no scene to load: the scene is not reloaded");
            Assert.AreEqual(3, counter.Value, "scene sections wait for their scene to load");
        }

        [UnityTest]
        public IEnumerator SceneLayer_PersistentSceneObject_GetsItsDataBack()
        {
            StartSystem();
            var go = Track(new GameObject("Keeper"));
            go.SetActive(false);
            Object.DontDestroyOnLoad(go);
            var saveable = go.AddComponent<Saveable>();
            var counter = go.AddComponent<SaveTestCounter>();
            saveable.SetId(System.Guid.NewGuid().ToString("N"));
            go.SetActive(true);
            yield return null; // its Start
            Assert.IsFalse(saveable.IsSpawned, "it woke up with its own id, like a scene object");
            counter.Value = 9;
            _system.Save();
            Assert.IsNotNull(ReadSave()["sections"][SceneSaving.PERSISTENT_KEY]["data"]["objects"][saveable.Id]);
            counter.Value = 1;

            yield return LoadAndWait();

            Assert.AreEqual(9, counter.Value);
        }
    }
}

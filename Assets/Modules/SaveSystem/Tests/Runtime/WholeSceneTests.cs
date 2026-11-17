using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>
    /// A scene whose save system is set to Whole Scene, through a real save and a load that reloads it: plain objects
    /// with nothing added to them, objects made in play, an object the game makes again itself, an object set up by
    /// hand with a Saveable.
    /// </summary>
    public class WholeSceneTests
    {
        private InMemorySaveStorage _storage;
        private SaveSystemSettings _settings;
        private GameObject _systemObject;
        private SaveLoadSystem _system;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The test's system first: the one in the scene then stays an idle copy that only brings its scope and list
            _storage = new InMemorySaveStorage();
            _settings = TestScenes.CreateSettings();
            _systemObject = new GameObject("SaveLoadSystem");
            Object.DontDestroyOnLoad(_systemObject);
            _system = _systemObject.AddComponent<SaveLoadSystem>();
            _system.UseStorage(_storage);
            _system.UseSettings(_settings);
            _system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            yield return TestScenes.Load(TestScenes.WHOLE_SCENE);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_systemObject);
            Object.DestroyImmediate(_settings);
        }

        private static GameObject Find(string name) => TestScenes.Find(name);

        private static string IdOf(string name) => Find(name).GetComponent<Saveable>().Id;

        private IEnumerator LoadAndWait()
        {
            bool done = false;
            void OnLoaded(LoadOutcome _) => done = true;
            _system.Loaded += OnLoaded;
            Assert.AreEqual(LoadOutcome.Loaded, _system.Load());
            yield return TestScenes.WaitFor(() => done);
            _system.Loaded -= OnLoaded;
            yield return null; // Destroy calls of the load take effect
        }

        private JObject SavedScene()
        {
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            return (JObject)json["sections"][SceneSaving.SCENE_PREFIX + TestScenes.GuidOf(TestScenes.WHOLE_SCENE)]["data"];
        }

        private static GameObject[] CopiesAtRoot() =>
            SceneManager.GetActiveScene().GetRootGameObjects().Where(root => root.name.StartsWith("SaveTestPlainSpawn")).ToArray();

        // ---------- Objects of the scene ----------

        [UnityTest]
        public IEnumerator Scene_PlainObjects_AreSavedWithNothingAddedToThem()
        {
            Assert.IsNotNull(Find("Plain").GetComponent<Saveable>().Policy, "a hidden Saveable that follows the scope");
            Assert.IsNotNull(Find("Holder").transform.Find("Child").GetComponent<Saveable>(), "a child is an object of the scene too");
            Assert.IsNull(Find("Wall").GetComponent<Saveable>(), "static objects are skipped");
            Assert.IsNull(Find("Marked").GetComponent<Saveable>().Policy, "a Saveable set up by hand stays as it is");
            Assert.IsNull(Find("Marked").transform.Find("Inner").GetComponent<Saveable>(), "and what is under it is its business");
            Assert.IsNull(Find("SaveSystem").GetComponent<Saveable>());
            yield break;
        }

        [UnityTest]
        public IEnumerator Scene_MovedObject_ComesBack_AnUntouchedOneIsNotWritten()
        {
            string plain = IdOf("Plain");
            string still = IdOf("Still");
            Find("Plain").transform.position = new Vector3(1f, 5f, 2f);
            _system.Save();
            JObject objects = (JObject)SavedScene()["objects"];
            Find("Plain").transform.position = Vector3.zero;
            Find("Still").transform.position = Vector3.zero; // after the save: the scene brings it back

            yield return LoadAndWait();

            Assert.IsNotNull(objects[plain]?["components"]?["Transform"]);
            Assert.IsNull(objects[still], "it stands as the scene has it: nothing to write");
            Assert.AreEqual(new Vector3(1f, 5f, 2f), Find("Plain").transform.position);
            Assert.AreEqual(new Vector3(2f, 0f, 0f), Find("Still").transform.position);
        }

        [UnityTest]
        public IEnumerator Scene_ChildMoved_ComesBack_ItsParentIsNotWritten()
        {
            string holder = IdOf("Holder");
            Find("Holder").transform.Find("Child").localPosition = new Vector3(0f, 3f, 0f);
            _system.Save();

            yield return LoadAndWait();

            Assert.IsNull(SavedScene()["objects"][holder]);
            Assert.AreEqual(new Vector3(0f, 3f, 0f), Find("Holder").transform.Find("Child").localPosition);
        }

        [UnityTest]
        public IEnumerator Scene_DestroyedObject_StaysDestroyed_SwitchedOffStaysOff()
        {
            Object.Destroy(Find("Doomed"));
            Find("Sleeper").SetActive(false);
            yield return null;
            _system.Save();

            yield return LoadAndWait();

            Assert.IsNull(Find("Doomed"));
            Assert.IsFalse(Find("Sleeper").activeSelf);
            Assert.IsNotNull(Find("Plain"));
        }

        [UnityTest]
        public IEnumerator Scene_ObjectSwitchedOffInTheSceneFile_TurnedOnInPlay_IsNotTakenForDestroyed()
        {
            // A pause menu: off in the scene file, so its hidden Saveable gets its first Awake when the game opens it
            GameObject hidden = Find("Hidden");
            string id = IdOf("Hidden");
            var probe = Find("Probe").GetComponent<MembersProbe>();
            probe.Target = hidden.transform;

            hidden.SetActive(true);
            yield return null;

            Assert.IsNotNull(hidden.GetComponent<Saveable>(), "it keeps its hidden Saveable");
            _system.Save();
            CollectionAssert.DoesNotContain(SavedScene()["destroyed"].Select(entry => (string)entry).ToArray(), id);

            yield return LoadAndWait();

            Assert.IsNotNull(Find("Hidden"), "the load did not destroy it");
            Assert.IsTrue(Find("Hidden").activeSelf, "and it is on, as it was saved");
            Assert.AreSame(Find("Hidden").transform, Find("Probe").GetComponent<MembersProbe>().Target);
        }

        [UnityTest]
        public IEnumerator Scene_Script_AllFieldsComeBack_WithAReferenceToAPlainObject()
        {
            var probe = Find("Probe").GetComponent<MembersProbe>();
            probe.Health = 3;
            probe.Prop = 6;
            probe.Target = Find("Plain").transform; // not a Saveable anyone set up: the scope gave it an id
            Find("Counter").GetComponent<SaveTestCounter>().Value = 42;
            _system.Save();

            yield return LoadAndWait();

            var loaded = Find("Probe").GetComponent<MembersProbe>();
            Assert.AreNotSame(probe, loaded);
            Assert.AreEqual(3, loaded.Health);
            Assert.AreEqual(6, loaded.Prop);
            Assert.AreSame(Find("Plain").transform, loaded.Target);
            Assert.AreEqual(42, Find("Counter").GetComponent<SaveTestCounter>().Value, "[Save] members as usual");
        }

        [UnityTest]
        public IEnumerator Scene_Script_PrivateFieldComesBack()
        {
            // A look angle a script keeps to itself and writes to a Transform every frame: without it the camera
            // looks straight ahead after a load
            Find("Probe").GetComponent<MembersProbe>().Timer = 5.5f;
            _system.Save();

            yield return LoadAndWait();

            Assert.AreEqual(5.5f, Find("Probe").GetComponent<MembersProbe>().Timer);
        }

        [UnityTest]
        public IEnumerator Scene_Body_VelocityComesBack()
        {
            Find("Body").GetComponent<Rigidbody>().linearVelocity = new Vector3(1f, 2f, 3f);
            _system.Save();
            Vector3 atLoaded = default;
            void Read(LoadOutcome _) => atLoaded = Find("Body").GetComponent<Rigidbody>().linearVelocity;
            _system.Loaded += Read;

            yield return LoadAndWait();
            _system.Loaded -= Read;

            Assert.AreEqual(new Vector3(1f, 2f, 3f), atLoaded);
        }

        [UnityTest]
        public IEnumerator Scene_ObjectWithASaveable_KeepsItsOwnList()
        {
            Find("Marked").GetComponent<SaveTestCounter>().Value = 9;
            Find("Marked").transform.position = new Vector3(0f, 7f, 0f);
            _system.Save();

            yield return LoadAndWait();

            Assert.AreEqual(9, Find("Marked").GetComponent<SaveTestCounter>().Value);
            Assert.AreEqual(new Vector3(11f, 0f, 0f), Find("Marked").transform.position, "its list has the script, not the Transform");
        }

        [UnityTest]
        public IEnumerator Scene_ObjectWithASaveable_WhatItsListLeavesOut_IsSavedByTheScope()
        {
            // An enemy set up by hand: its list has its own scripts, the script of a child (its effects, its Animator)
            // is not in it — it must not be saved worse than a plain object of the scene
            Find("Marked").transform.Find("Inner").GetComponent<MembersProbe>().Health = 77;
            // Made in play under it (a muzzle flash): not the scene's, not there after a load — not written
            var flash = new GameObject("Flash");
            flash.transform.SetParent(Find("Marked").transform, false);
            flash.AddComponent<MembersProbe>();
            _system.Save();
            string[] saved = ((JObject)SavedScene()["objects"][TestScenes.MARKED]["components"]).Properties().Select(entry => entry.Name).ToArray();

            yield return LoadAndWait();

            CollectionAssert.Contains(saved, "Inner/MembersProbe");
            CollectionAssert.DoesNotContain(saved, "Flash/MembersProbe");
            Assert.AreEqual(77, Find("Marked").transform.Find("Inner").GetComponent<MembersProbe>().Health);
        }

        [UnityTest]
        public IEnumerator Scene_ObjectWithASaveable_MembersPickedByHand_AddToWhatTheScopeTakes()
        {
            // Its list has the script with one private member picked (Selected). A plain object next to it would give
            // all the fields of that script: the one set up by hand must not give less
            var probe = Find("Marked").GetComponent<MembersProbe>();
            probe.Timer = 4f;   // picked by hand
            probe.Health = 31;  // not picked: the scope takes it
            _system.Save();

            yield return LoadAndWait();

            var loaded = Find("Marked").GetComponent<MembersProbe>();
            Assert.AreNotSame(probe, loaded);
            Assert.AreEqual(4f, loaded.Timer);
            Assert.AreEqual(31, loaded.Health);
        }

        // ---------- Objects made in play ----------

        [UnityTest]
        public IEnumerator MadeInPlay_ComesBackAsACopyOfThePrefabItIsNamedAfter()
        {
            GameObject made = Object.Instantiate(TestScenes.PlainPrefab);
            yield return null; // its Start resets the counters
            made.transform.position = new Vector3(0f, 4f, 0f);
            made.GetComponent<SaveTestCounter>().Value = 7;
            made.transform.Find("Part").GetComponent<SaveTestCounter>().Value = 8;
            _system.Save();

            yield return LoadAndWait();
            yield return null;

            GameObject[] copies = CopiesAtRoot();
            Assert.AreEqual(1, copies.Length);
            Assert.AreNotSame(made, copies[0]);
            Assert.AreEqual(new Vector3(0f, 4f, 0f), copies[0].transform.position);
            Assert.AreEqual(7, copies[0].GetComponent<SaveTestCounter>().Value);
            Assert.AreEqual(8, copies[0].transform.Find("Part").GetComponent<SaveTestCounter>().Value);

            // And again on what came back: nothing doubles
            _system.Save();
            yield return LoadAndWait();
            yield return null;
            Assert.AreEqual(1, CopiesAtRoot().Length);
            Assert.AreEqual(7, CopiesAtRoot()[0].GetComponent<SaveTestCounter>().Value);
        }

        [UnityTest]
        public IEnumerator MadeInPlay_UnderAnotherObjectMadeInPlay_ComesBackUnderIt()
        {
            // A robot fires a rocket and the game keeps the rocket under the robot: not a part of the robot's prefab
            GameObject robot = Object.Instantiate(TestScenes.PlainPrefab);
            robot.transform.position = new Vector3(0f, 4f, 0f);
            GameObject rocket = Object.Instantiate(TestScenes.PlainPrefab, robot.transform);
            yield return null; // their Starts reset the counters
            robot.GetComponent<SaveTestCounter>().Value = 7;
            robot.transform.Find("Part").GetComponent<SaveTestCounter>().Value = 8;
            rocket.transform.localPosition = new Vector3(1f, 2f, 3f);
            rocket.GetComponent<SaveTestCounter>().Value = 9;
            _system.Save();

            var spawned = (JArray)SavedScene()["spawned"];
            string robotId = robot.GetComponent<Saveable>().Id;
            Assert.AreEqual(1, spawned.Count(entry => (string)entry["id"] == robotId));
            Assert.AreEqual(1, spawned.Count(entry => (string)entry["parent"] == robotId),
                "the rocket is an object of its own in the save, and names the robot as its parent");

            yield return LoadAndWait();
            yield return null;

            GameObject[] copies = CopiesAtRoot();
            Assert.AreEqual(1, copies.Length, "one robot at the root");
            Transform robotNow = copies[0].transform;
            Assert.AreEqual(7, robotNow.GetComponent<SaveTestCounter>().Value);
            Assert.AreEqual(8, robotNow.Find("Part").GetComponent<SaveTestCounter>().Value, "what the prefab brought is still the robot's");
            Transform[] rockets = robotNow.Cast<Transform>().Where(child => child.name.StartsWith(TestScenes.PlainPrefab.name)).ToArray();
            Assert.AreEqual(1, rockets.Length, "the rocket was made again under the robot");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), rockets[0].localPosition);
            Assert.AreEqual(9, rockets[0].GetComponent<SaveTestCounter>().Value);

            // And again on what came back: nothing doubles
            _system.Save();
            yield return LoadAndWait();
            yield return null;
            Assert.AreEqual(1, CopiesAtRoot().Length);
            Assert.AreEqual(1, CopiesAtRoot()[0].transform.Cast<Transform>().Count(child => child.name.StartsWith(TestScenes.PlainPrefab.name)));
        }

        [UnityTest]
        public IEnumerator MadeInPlay_ByTheGameItselfOnEveryStart_IsTakenOverNotDoubled()
        {
            // Giver makes a copy under itself in Start, like a weapon given at the start of a level
            Transform giver = Find("Giver").transform;
            Assert.AreEqual(1, giver.childCount);
            giver.GetChild(0).GetComponent<SaveTestCounter>().Value = 5;
            giver.GetChild(0).localPosition = new Vector3(0f, 1f, 0f);
            _system.Save();

            yield return LoadAndWait();
            yield return null;

            giver = Find("Giver").transform;
            Assert.AreEqual(1, giver.childCount, "the saved copy took over the one the game made, it did not add a second");
            Assert.AreSame(Find("Giver").GetComponent<SaveTestStartSpawner>().Made, giver.GetChild(0).gameObject);
            Assert.AreEqual(5, giver.GetChild(0).GetComponent<SaveTestCounter>().Value);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), giver.GetChild(0).localPosition);
        }

        [UnityTest]
        public IEnumerator MadeInPlay_WithoutAPrefab_IsNotSavedWithAWarning_AnEffectIsLeftAloneQuietly()
        {
            new GameObject("Loose").AddComponent<SaveTestCounter>();
            new GameObject("Spark"); // no script, no body: a one-shot effect
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex("'Loose' was made in play and the scene uses no prefab named 'Loose'"));
            _system.Save();

            Assert.AreEqual(1, ((JArray)SavedScene()["spawned"]).Count, "only the copy the Giver made");
            Assert.IsNull(Find("Spark").GetComponent<Saveable>());
        }

#if SAVE_PARTICLES
        private static GameObject[] EffectsAtRoot() =>
            SceneManager.GetActiveScene().GetRootGameObjects().Where(root => root.name.StartsWith("SaveTestPlainEffect")).ToArray();

        [UnityTest]
        public IEnumerator MadeInPlay_Effect_GoesOnFromItsTime_AndIsDestroyedWhenItEnds()
        {
            // An explosion: a prefab without a script, made in play, destroyed by a timer of the game
            GameObject made = Object.Instantiate(TestScenes.EffectPrefab, new Vector3(3f, 3f, 3f), Quaternion.identity);
            Object.Destroy(made, 30f);
            yield return new WaitForSeconds(0.4f);
            float time = made.GetComponent<ParticleSystem>().time;
            _system.Save();

            yield return LoadAndWait();
            yield return null;

            GameObject[] copies = EffectsAtRoot();
            Assert.AreEqual(1, copies.Length, "the effect is back");
            Assert.AreNotSame(made, copies[0]);
            Assert.AreEqual(new Vector3(3f, 3f, 3f), copies[0].transform.position);
            Assert.GreaterOrEqual(copies[0].GetComponent<ParticleSystem>().time, time - 0.05f, "it goes on, it does not start over");
            Assert.IsTrue(copies[0].GetComponent<ParticleSystem>().IsAlive());

            // The game's timer did not come back with it: the copy goes when it has ended
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(0, EffectsAtRoot().Length);
        }

        [UnityTest]
        public IEnumerator MadeInPlay_Effect_ThatHasEnded_IsNotSaved()
        {
            GameObject made = Object.Instantiate(TestScenes.EffectPrefab);
            yield return null;
            made.GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            yield return null;

            _system.Save();

            Assert.AreEqual(1, ((JArray)SavedScene()["spawned"]).Count, "only the copy the Giver made");
            Assert.IsNull(made.GetComponent<Saveable>());
        }
#endif

        // ---------- The editor: a scene played with unsaved changes ----------

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Editor_SceneWithAPlayCopy_LoadsGoToTheCopy_SavesKnowItByTheOriginal()
        {
            // What the editor does when Play starts with unsaved changes in the scene: a copy that loads go to
            const string FOLDER = "Assets/__SaveSystemPlayCopies";
            string copy = $"{FOLDER}/probe/SaveTestWholeScene.unity";
            UnityEditor.AssetDatabase.DeleteAsset(FOLDER);
            UnityEditor.AssetDatabase.CreateFolder("Assets", "__SaveSystemPlayCopies");
            UnityEditor.AssetDatabase.CreateFolder(FOLDER, "probe");
            Assert.IsTrue(UnityEditor.AssetDatabase.CopyAsset(TestScenes.WHOLE_SCENE, copy));
            SaveSceneCopies.Set(TestScenes.WHOLE_SCENE, copy);
            try
            {
                Find("Plain").transform.position = new Vector3(1f, 5f, 2f);
                _system.Save();

                yield return LoadAndWait();

                Assert.AreEqual(copy, SceneManager.GetActiveScene().path, "the scene as it was played, not the file on disk");
                Assert.AreEqual(new Vector3(1f, 5f, 2f), Find("Plain").transform.position);
                Assert.IsNotNull(SavedScene()["objects"], "its section is the original's");

                // And once more from the copy: it is still the same scene to the save
                Find("Plain").transform.position = new Vector3(3f, 3f, 3f);
                _system.Save();
                yield return LoadAndWait();
                Assert.AreEqual(new Vector3(3f, 3f, 3f), Find("Plain").transform.position);
            }
            finally
            {
                SaveSceneCopies.Clear();
            }

            // Back to a scene of the project before the copy goes away
            yield return TestScenes.Load(TestScenes.WHOLE_SCENE);
            UnityEditor.AssetDatabase.DeleteAsset(FOLDER);
        }
#endif

        // ---------- The long frame of a load ----------

        [UnityTest]
        public IEnumerator Load_ItsLongFrame_IsNotHandedToTheGameAsOneStep()
        {
            // Unity gives the time a scene took to load to the game as deltaTime (up to maximumDeltaTime): a bullet
            // saved in flight would be far away before it is seen. The step is capped while a load is on its way
            float usual = Time.maximumDeltaTime;
            Assert.Greater(usual, Time.fixedDeltaTime, "the test needs a cap to lower");
            _system.Save();
            float longest = 0f;
            bool done = false;
            void OnLoaded(LoadOutcome _) => done = true;
            _system.Loaded += OnLoaded;

            Assert.AreEqual(LoadOutcome.Loaded, _system.Load());
            Assert.AreEqual(Time.fixedDeltaTime, Time.maximumDeltaTime, 0.0001f, "capped from the moment the load is asked for");
            for (int frame = 0; frame < 300 && !done; frame++)
            {
                yield return null;
                longest = Mathf.Max(longest, Time.deltaTime);
            }

            _system.Loaded -= OnLoaded;
            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
                longest = Mathf.Max(longest, Time.deltaTime);
            }

            Assert.LessOrEqual(longest, Time.fixedDeltaTime + 0.0001f, "no frame of the load was a long step");
            Assert.AreEqual(usual, Time.maximumDeltaTime, "and the game's own limit is back");
        }

        // ---------- The report ----------

        [UnityTest]
        public IEnumerator Report_LastSave_SaysWhatWasWritten()
        {
            Assert.IsNull(_system.LastSave);
            Find("Plain").transform.position = Vector3.up;

            _system.Save();

            SaveReport report = _system.LastSave;
            Assert.AreEqual(SaveOutcome.Saved, report.Outcome);
            Assert.AreEqual(0, report.Slot);
            Assert.Greater(report.Objects, 0);
            Assert.Greater(report.Characters, 0);
            Assert.AreEqual(0, report.Problems.Count);
            yield break;
        }
    }
}

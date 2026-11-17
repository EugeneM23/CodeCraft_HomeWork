using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Saving.Editor;
using Saving.Objects;
using UnityEditor;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// The lists of a save system set to Whole Scene (which objects, which ids) and what the scope takes from an
    /// object by kind of component.
    /// </summary>
    public class SaveSceneBakerTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static GameObject Make(string name, Transform parent = null)
        {
            var made = new GameObject(name);
            if (parent != null)
                made.transform.SetParent(parent, false);

            return made;
        }

        private static SaveLoadSystem MakeSystem() => Make("SaveSystem").AddComponent<SaveLoadSystem>();

        private static string[] Listed(SaveLoadSystem system) => system.SceneObjects.Select(entry => entry.Object.name).ToArray();

        private static string IdOf(SaveLoadSystem system, GameObject target) =>
            system.SceneObjects.Single(entry => entry.Object == target).Id;

        // ---------- Which objects ----------

        [Test]
        public void AddedByHand_IsTheLazyWayIn_WholeSceneWithKeys()
        {
            Make("Crate");

            SaveLoadSystem system = MakeSystem();

            Assert.AreEqual(SaveScope.WholeScene, system.Scope);
            Assert.IsTrue(system.Hotkeys);
            CollectionAssert.AreEquivalent(new[] { "Crate" }, Listed(system), "listed as soon as the component is there");
        }

        [Test]
        public void Bake_LeavesOutStaticSaveablesIgnoredAndItself()
        {
            GameObject crate = Make("Crate");
            Make("Lid", crate.transform);
            Make("Floor").isStatic = true;
            GameObject wall = Make("Wall");
            wall.isStatic = true;
            Make("Lamp", wall.transform);
            GameObject hero = Make("Hero");
            hero.AddComponent<Saveable>();
            Make("Hand", hero.transform);
            GameObject junk = Make("Junk");
            Make("Scrap", junk.transform);
            SaveLoadSystem system = MakeSystem();
            var serialized = new SerializedObject(system);
            SerializedProperty ignored = serialized.FindProperty("_ignored");
            ignored.arraySize = 1;
            ignored.GetArrayElementAtIndex(0).objectReferenceValue = junk;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SaveSceneBaker.Bake(system);

            // Lamp: a moving part under a static wall is still saved
            CollectionAssert.AreEquivalent(new[] { "Crate", "Lid", "Lamp" }, Listed(system));
        }

        [Test]
        public void Bake_SkipStaticOff_ListsStaticObjectsToo()
        {
            Make("Floor").isStatic = true;
            SaveLoadSystem system = MakeSystem();
            system.UseScope(SaveScope.WholeScene, skipStatic: false);

            SaveSceneBaker.Bake(system);

            CollectionAssert.AreEquivalent(new[] { "Floor" }, Listed(system));
        }

        [Test]
        public void Bake_SaveablesScope_KeepsNoLists()
        {
            Make("Crate");
            SaveLoadSystem system = MakeSystem();
            system.UseScope(SaveScope.Saveables);

            Assert.IsTrue(SaveSceneBaker.Bake(system));

            Assert.AreEqual(0, system.SceneObjects.Count);
            Assert.IsFalse(SaveSceneBaker.Bake(system), "nothing to change the second time");
        }

        // ---------- Which ids ----------

        [Test]
        public void Ids_KeptForGood_OnceListed()
        {
            GameObject crate = Make("Crate");
            SaveLoadSystem system = MakeSystem();
            string id = IdOf(system, crate);

            GameObject shelf = Make("Shelf");
            crate.name = "Barrel";
            crate.transform.SetParent(shelf.transform, false);
            SaveSceneBaker.Bake(system);

            Assert.AreEqual(id, IdOf(system, crate), "renamed and moved: still the same object in old saves");
            Assert.AreNotEqual(id, IdOf(system, shelf));
        }

        [Test]
        public void Ids_SameSceneGivesSameIds_AndTwinsDiffer()
        {
            GameObject first = Make("Crate");
            GameObject second = Make("Crate");
            SaveLoadSystem system = MakeSystem();
            string firstId = IdOf(system, first);
            string secondId = IdOf(system, second);

            system.SetSceneTable(null, null); // as on another machine, or in a scene that was never saved with the list
            SaveSceneBaker.Bake(system);

            Assert.AreNotEqual(firstId, secondId);
            Assert.AreEqual(firstId, IdOf(system, first));
            Assert.AreEqual(secondId, IdOf(system, second));
        }

        // ---------- What is taken from an object ----------

        [Test]
        public void Policy_KindsOfComponents()
        {
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            GameObject crate = Make("Crate");

            Assert.AreEqual(SaveCoverage.Transforms, SavePolicy.CategoryOf(crate.transform, catalog));
            Assert.AreEqual(SaveCoverage.Physics, SavePolicy.CategoryOf(crate.AddComponent<Rigidbody2D>(), catalog));
            Assert.AreEqual(SaveCoverage.Physics, SavePolicy.CategoryOf(crate.AddComponent<BoxCollider2D>(), catalog));
            Assert.AreEqual(SaveCoverage.Effects, SavePolicy.CategoryOf(crate.AddComponent<AudioSource>(), catalog));
            Assert.AreEqual(SaveCoverage.Scripts, SavePolicy.CategoryOf(crate.AddComponent<MembersProbe>(), catalog));
            Assert.AreEqual(SaveCoverage.Scripts, SavePolicy.CategoryOf(crate.AddComponent<SaveTestCounter>(), catalog));
            Assert.AreEqual(SaveCoverage.None, SavePolicy.CategoryOf(crate.AddComponent<Light>(), catalog), "not a state of play");
            Assert.AreEqual(SaveCoverage.None, SavePolicy.CategoryOf(crate.AddComponent<UnityEngine.UI.Image>(), catalog),
                "Unity's own scripts are not the game's");
        }

        [Test]
        public void Policy_TakesOnlyWhatItsCoverageNames()
        {
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            GameObject crate = Make("Crate");
            var body = crate.AddComponent<Rigidbody2D>();
            var probe = crate.AddComponent<MembersProbe>();
            var scripts = new SavePolicy(SaveCoverage.Scripts);
            Saveable saveable = Saveable.Adopt(crate, "crate-id", scripts, null);

            Assert.IsNull(scripts.SerializerFor(saveable, body, catalog, restoring: false));
            Assert.IsNull(scripts.SerializerFor(saveable, crate.transform, catalog, restoring: false));
            Assert.IsNotNull(scripts.SerializerFor(saveable, probe, catalog, restoring: false));
            CollectionAssert.AreEquivalent(new[] { "MembersProbe" }, ObjectState.Capture(saveable, catalog).Components.Keys.ToArray());
        }

        [Test]
        public void Policy_PrivateFields_SimpleOnes_OnlyWhenAsked()
        {
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            GameObject crate = Make("Crate");
            var probe = crate.AddComponent<PrivateStateProbe>();
            probe.Angle = 25f;
            probe.Armed = true;
            probe.Now = PrivateStateProbe.Phase.Attack;
            probe.Aim = Vector3.up;
            probe.ElapsedTime = 12f;
            var asked = new SavePolicy(SaveCoverage.Scripts, null, privateFields: true);
            Saveable saveable = Saveable.Adopt(crate, "crate-id", asked, null);

            var serializer = (MembersSerializer)asked.SerializerFor(saveable, probe, catalog, restoring: false);
            CollectionAssert.AreEquivalent(
                new[] { "Shown", "_angle", "_armed", "_phase", "_aim", "_lastTimeFired", "_elapsedTime", "_lastPosition" },
                serializer.Members.Select(member => member.Name).ToArray(),
                "no reference, no collection, nothing [NonSerialized] or readonly, nothing that is only what the script saw last");

            ObjectData data = ObjectState.Capture(saveable, catalog);
            probe.Angle = 0f;
            probe.Armed = false;
            probe.Now = PrivateStateProbe.Phase.Idle;
            probe.Aim = Vector3.zero;
            probe.ElapsedTime = 0f;
            ObjectState.RestoreComponents(saveable, data, catalog);

            Assert.AreEqual(25f, probe.Angle);
            Assert.IsTrue(probe.Armed);
            Assert.AreEqual(PrivateStateProbe.Phase.Attack, probe.Now);
            Assert.AreEqual(Vector3.up, probe.Aim);
            Assert.AreEqual(12f, probe.ElapsedTime, "a count of seconds is a plain value");

            // "What I saw last" is not put back: a fresh script (after a load) notices the saved state as a change and
            // brings its looks up to date — the glow of a hot gun, the rate of its steam
            GameObject loadedCrate = Make("Crate after a load");
            var loaded = loadedCrate.AddComponent<PrivateStateProbe>();
            probe.SeesAChange(0.25f);
            data = ObjectState.Capture(saveable, catalog);
            Saveable loadedSaveable = Saveable.Adopt(loadedCrate, "crate-id-2", asked, null);
            ObjectState.RestoreComponents(loadedSaveable, data, catalog);
            Assert.IsTrue(loaded.Armed);
            Assert.IsTrue(loaded.SeesAChange(0.25f));

            var plain = (MembersSerializer)new SavePolicy(SaveCoverage.Scripts).SerializerFor(saveable, probe, catalog, restoring: false);
            CollectionAssert.AreEquivalent(new[] { "Shown" }, plain.Members.Select(member => member.Name).ToArray());
        }

        [Test]
        public void Moment_IsSavedAsHowLongAgo_AndComesBackAgainstTheClockOfTheLoad()
        {
            GameObject crate = Make("Crate");
            var probe = crate.AddComponent<PrivateStateProbe>();
            MembersSerializer serializer = MembersSerializer.AllWithPrivateFor(typeof(PrivateStateProbe));
            SaveMember moment = serializer.Members.Single(member => member.Name == "_lastTimeFired");
            Assert.IsTrue(moment.IsMoment);
            Assert.IsFalse(serializer.Members.Single(member => member.Name == "_elapsedTime").IsMoment);
            Assert.IsFalse(serializer.Members.Single(member => member.Name == "_angle").IsMoment);

            // Fired 3 seconds ago
            probe.LastTimeFired = Time.time - 3f;
            Newtonsoft.Json.Linq.JObject saved = serializer.CaptureObject(probe);
            Assert.IsNull(saved["_lastTimeFired"], "not a reading of the clock");
            Assert.AreEqual(3.0, (double)saved["_lastTimeFired@ago"], 0.001);

            // Loaded in another run of the game, whatever its clock shows: still 3 seconds ago
            probe.LastTimeFired = 12345f;
            serializer.RestoreObject(probe, saved);
            Assert.AreEqual(Time.time - 3f, probe.LastTimeFired, 0.001f);

            // "It has not happened yet" stays that
            probe.LastTimeFired = float.NegativeInfinity;
            saved = serializer.CaptureObject(probe);
            probe.LastTimeFired = 1f;
            serializer.RestoreObject(probe, saved);
            Assert.IsTrue(float.IsNegativeInfinity(probe.LastTimeFired));

            // A save from before moments has the plain value: it is read as it was
            serializer.RestoreObject(probe, new Newtonsoft.Json.Linq.JObject { ["_lastTimeFired"] = 7f });
            Assert.AreEqual(7f, probe.LastTimeFired);
        }

        [Test]
        public void Policy_Transform_OnlyWhenTheObjectMoved_AlwaysOnALoad()
        {
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            GameObject crate = Make("Crate");
            var policy = new SavePolicy(SaveCoverage.Everything);
            Saveable saveable = Saveable.Adopt(crate, "crate-id", policy, new SceneObjectBaseline(crate));

            Assert.IsNull(policy.SerializerFor(saveable, crate.transform, catalog, restoring: false), "as the scene has it");
            Assert.IsNotNull(policy.SerializerFor(saveable, crate.transform, catalog, restoring: true));

            crate.transform.localPosition = Vector3.one;

            Assert.IsNotNull(policy.SerializerFor(saveable, crate.transform, catalog, restoring: false));
        }

        [Test]
        public void Policy_SceneObject_AnswersForItselfOnly_ColliderWrittenOnlyWhenSwitched()
        {
            // A wall left out of the list (static) under a listed parent: the parent must not save it by path
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            GameObject room = Make("Room");
            var door = room.AddComponent<BoxCollider2D>();
            GameObject wall = Make("Wall", room.transform);
            wall.AddComponent<BoxCollider2D>();
            wall.AddComponent<MembersProbe>();
            var policy = new SavePolicy(SaveCoverage.Everything);
            Saveable saveable = Saveable.Adopt(room, "room-id", policy, new SceneObjectBaseline(room));
            policy.RememberStart(room, catalog);

            ObjectData untouched = ObjectState.Capture(saveable, catalog);
            door.enabled = false;
            ObjectData switched = ObjectState.Capture(saveable, catalog);

            CollectionAssert.IsEmpty(untouched.Components.Keys, "as the scene has it: nothing to write");
            CollectionAssert.IsEmpty(untouched.Children.Keys);
            CollectionAssert.AreEquivalent(new[] { "BoxCollider2D" }, switched.Components.Keys.ToArray());
        }

        [Test]
        public void Policy_ObjectMadeInPlay_BringsItsPartsButNotTheirPlaces()
        {
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            GameObject bullet = Make("Bullet(Clone)");
            bullet.AddComponent<SaveTestCounter>();
            Make("Tip", bullet.transform).AddComponent<SaveTestCounter>();
            Saveable saveable = Saveable.Adopt(bullet, "bullet-id", new SavePolicy(SaveCoverage.Everything), null, "prefab-id");

            CollectionAssert.AreEquivalent(new[] { "Transform", "SaveTestCounter", "Tip/SaveTestCounter" },
                ObjectState.Capture(saveable, catalog).Components.Keys.ToArray());
            Assert.AreEqual("prefab-id", saveable.PrefabId);
        }

        [Test]
        public void Policy_PrefabsByName_CloneSuffixStripped_TwinsAreAmbiguous()
        {
            GameObject bullet = Make("Bullet");
            GameObject twinA = Make("Twin");
            GameObject twinB = Make("Twin");
            var policy = new SavePolicy(SaveCoverage.Everything, new List<ScenePrefab>
            {
                new() { Id = "b", Prefab = bullet },
                new() { Id = "t1", Prefab = twinA },
                new() { Id = "t2", Prefab = twinB }
            });

            Assert.AreEqual("Bullet", SavePolicy.BaseName("Bullet(Clone)(Clone)"));
            Assert.IsTrue(policy.TryFindPrefab("Bullet(Clone)", out ScenePrefab found));
            Assert.AreEqual("b", found.Id);
            Assert.IsFalse(policy.TryFindPrefab("Twin(Clone)", out _));
            Assert.IsTrue(policy.IsAmbiguous("Twin(Clone)"));
            Assert.IsFalse(policy.TryFindPrefab("Unknown", out _));
        }
    }
}

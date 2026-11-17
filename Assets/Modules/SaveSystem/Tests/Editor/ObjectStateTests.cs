using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Component keys of a Saveable and the round trip of one object's state.</summary>
    public class ObjectStateTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static Saveable MakeSaveable(string name, out FieldsProbe probe)
        {
            var root = new GameObject(name);
            var saveable = root.AddComponent<Saveable>();
            saveable.SetId(System.Guid.NewGuid().ToString("N"));
            probe = root.AddComponent<FieldsProbe>();
            return saveable;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static ComponentSerializerCatalog Catalog => ComponentSerializerCatalog.Default;

        [Test]
        public void Keys_PathsTypesAndRepeats()
        {
            Saveable saveable = MakeSaveable("Root", out _);
            saveable.gameObject.AddComponent<BoxCollider2D>();
            saveable.gameObject.AddComponent<BoxCollider2D>();
            Child(saveable.gameObject, "Visual").AddComponent<SpriteRenderer>();
            Child(saveable.gameObject, "Visual");
            GameObject nested = Child(saveable.gameObject, "Nested");
            nested.AddComponent<Saveable>();
            Child(nested, "Deep");

            string[] keys = ComponentKeys.Collect(saveable).Select(entry => entry.Key).ToArray();

            CollectionAssert.IsSubsetOf(new[]
            {
                "Transform", "FieldsProbe", "BoxCollider2D", "BoxCollider2D#1", "Visual/Transform",
                "Visual/SpriteRenderer", "Visual#1/Transform"
            }, keys);
            Assert.IsFalse(keys.Any(key => key.StartsWith("Nested")), "a nested Saveable saves itself");
            Assert.IsFalse(keys.Contains("Saveable"));
        }

        [Test]
        public void Keys_FindChild_FollowsRepeatedNames()
        {
            Saveable saveable = MakeSaveable("Root", out _);
            Child(saveable.gameObject, "Visual");
            GameObject second = Child(saveable.gameObject, "Visual");

            Assert.AreSame(second.transform, ComponentKeys.FindChild(saveable.transform, "Visual#1"));
            Assert.AreEqual("Visual#1", ComponentKeys.PathOf(saveable.transform, second.transform));
            Assert.IsNull(ComponentKeys.FindChild(saveable.transform, "Missing"));
        }

        [Test]
        public void Keys_TypeNameOf_DropsPathAndNumber()
        {
            Assert.AreEqual("BoxCollider2D", ComponentKeys.TypeNameOf("Visual/BoxCollider2D#1"));
            Assert.AreEqual("Transform", ComponentKeys.TypeNameOf("Transform"));
        }

        [Test]
        public void Capture_OnlyComponentsWithSerializer()
        {
            Saveable saveable = MakeSaveable("Root", out _);
            saveable.gameObject.AddComponent<PlainProbe>();

            ObjectData data = ObjectState.Capture(saveable, Catalog);

            CollectionAssert.AreEquivalent(new[] { "Transform", "FieldsProbe" }, data.Components.Keys);
        }

        [Test]
        public void Capture_Unchecked_NotSaved()
        {
            Saveable saveable = MakeSaveable("Root", out _);
            saveable.SetSaved("FieldsProbe", false);

            ObjectData data = ObjectState.Capture(saveable, Catalog);

            Assert.IsFalse(data.Components.ContainsKey("FieldsProbe"));
            Assert.IsFalse(saveable.IsSaved("FieldsProbe"));
        }

        [Test]
        public void State_RoundTrip_ActiveEnabledAndData()
        {
            Saveable saveable = MakeSaveable("Root", out FieldsProbe probe);
            GameObject visual = Child(saveable.gameObject, "Visual");
            probe.Hp = 7;
            probe.enabled = false;
            visual.SetActive(false);
            saveable.transform.localPosition = new Vector3(4f, 5f, 0f);
            ObjectData data = ObjectState.Capture(saveable, Catalog);

            probe.Hp = 1;
            probe.enabled = true;
            visual.SetActive(true);
            saveable.transform.localPosition = Vector3.zero;
            ObjectState.RestoreActive(saveable, data);
            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(7, probe.Hp);
            Assert.IsFalse(probe.enabled);
            Assert.IsFalse(visual.activeSelf);
            Assert.AreEqual(new Vector3(4f, 5f, 0f), saveable.transform.localPosition);
            Assert.AreEqual(false, data.Components["FieldsProbe"].Enabled);
            Assert.IsNull(data.Components["Transform"].Enabled, "Transform has no enabled flag");
        }

        [Test]
        public void Restore_MissingComponent_WarnsOnce()
        {
            Saveable saveable = MakeSaveable("Root", out _);
            ObjectData data = ObjectState.Capture(saveable, Catalog);
            data.Components["Ghost"] = new ComponentData { V = 1, Data = new JObject() };

            LogAssert.Expect(LogType.Warning, new Regex("has no component Ghost"));
            ObjectState.RestoreComponents(saveable, data, Catalog);
            ObjectState.RestoreComponents(saveable, data, Catalog);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Restore_OneComponentFails_OthersApplied()
        {
            Saveable saveable = MakeSaveable("Root", out FieldsProbe probe);
            saveable.transform.localPosition = new Vector3(3f, 0f, 0f);
            ObjectData data = ObjectState.Capture(saveable, Catalog);
            data.Components["FieldsProbe"].Data = JObject.Parse("{ \"_hp\": \"not a number\" }");
            saveable.transform.localPosition = Vector3.zero;

            LogAssert.Expect(LogType.Error, new Regex("Loading FieldsProbe of 'Root' failed"));
            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(new Vector3(3f, 0f, 0f), saveable.transform.localPosition);
        }

        [Test]
        public void Restore_NewerComponentVersion_Skipped()
        {
            Saveable saveable = MakeSaveable("Root", out FieldsProbe probe);
            probe.Hp = 5;
            ObjectData data = ObjectState.Capture(saveable, Catalog);
            data.Components["FieldsProbe"].V = 9;
            probe.Hp = 1;

            LogAssert.Expect(LogType.Error, new Regex("newer than 1"));
            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(1, probe.Hp);
        }

        [Test]
        public void Capture_ComponentThrows_KeepsPreviousEntry()
        {
            Saveable saveable = MakeSaveable("Root", out _);
            var failing = new TaggedSerializer(typeof(FieldsProbe), "kept");
            var catalog = new ComponentSerializerCatalog(new IComponentSerializer[] { failing }, new IComponentSerializer[0]);
            ObjectData previous = ObjectState.Capture(saveable, catalog);
            failing.FailCapture = true;

            LogAssert.Expect(LogType.Error, new Regex("Saving FieldsProbe of 'Root' failed"));
            ObjectData data = ObjectState.Capture(saveable, catalog, previous);

            Assert.AreEqual("kept", (string)data.Components["FieldsProbe"].Data);
        }

        [Test]
        public void Restore_ManyComponentsGone_OneWarningForTheObject()
        {
            // A weapon picked up in play and not there after the load: dozens of saved components, one warning
            Saveable saveable = MakeSaveable("Root", out _);
            var data = new ObjectData();
            foreach (string key in new[] { "Gun/Transform", "Gun/AudioSource", "Gun/Barrel/Transform", "Gun/Muzzle/Transform", "Gun/Muzzle/ParticleSystem" })
                data.Components[key] = new ComponentData { V = 1, Data = new JObject() };
            int warnings = 0;
            void Count(string condition, string stackTrace, LogType type) => warnings += type == LogType.Warning ? 1 : 0;

            LogAssert.Expect(LogType.Warning, new Regex("'Root' has 5 saved components that are not on it any more \\(Gun/Transform, .* and 2 more\\)"));
            Application.logMessageReceived += Count;
            ObjectState.RestoreComponents(saveable, data, Catalog);
            ObjectState.RestoreComponents(saveable, data, Catalog);
            Application.logMessageReceived -= Count;

            Assert.AreEqual(1, warnings, "once for the object, and not again on the next load");
        }

        [Test]
        public void Restore_WholeSceneObject_ComponentsGone_NobodyIsTold()
        {
            // The muzzle flash that was under the weapon when the game was saved: nobody picked it by hand
            var weapon = new GameObject("Weapon(Clone)");
            Saveable saveable = Saveable.Adopt(weapon, "weapon-id", new SavePolicy(SaveCoverage.Everything), null, "prefab-id");
            var data = new ObjectData();
            data.Components["Muzzle/Flash(Clone)/ParticleSystem"] = new ComponentData { V = 1, Data = new JObject() };
            int warnings = 0;
            void Count(string condition, string stackTrace, LogType type) => warnings += type == LogType.Warning ? 1 : 0;

            Application.logMessageReceived += Count;
            ObjectState.RestoreComponents(saveable, data, Catalog);
            Application.logMessageReceived -= Count;

            Assert.AreEqual(0, warnings);
        }

        [Test]
        public void Restore_OneComponentGone_NamesIt()
        {
            Saveable saveable = MakeSaveable("Root", out _);
            var data = new ObjectData();
            data.Components["Gone/Transform"] = new ComponentData { V = 1, Data = new JObject() };

            LogAssert.Expect(LogType.Warning, new Regex("'Root' has no component Gone/Transform any more"));
            ObjectState.RestoreComponents(saveable, data, Catalog);
        }
    }
}

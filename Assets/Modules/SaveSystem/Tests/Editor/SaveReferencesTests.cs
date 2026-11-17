using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>References to scene objects, prefabs and assets, and what happens to anything else.</summary>
    public class SaveReferencesTests
    {
        private TempScene _scene;
        private SaveSystemSettings _settings;
        private Saveable _owner;

        [SetUp]
        public void SetUp()
        {
            _scene = new TempScene();
            _settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
            SaveReferences.Settings = _settings;
            _owner = new GameObject("Owner").AddComponent<Saveable>();
            _owner.SetId(System.Guid.NewGuid().ToString("N"));
            SaveRegistry.Register(_owner);
        }

        [TearDown]
        public void TearDown()
        {
            SaveRegistry.Unregister(_owner);
            SaveReferences.Settings = null;
            Object.DestroyImmediate(_settings);
            _scene.Dispose();
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_owner.transform, false);
            return child;
        }

        [Test]
        public void Ref_ComponentOnChild_IdPathAndComponent()
        {
            var renderer = Child("Visual").AddComponent<SpriteRenderer>();

            JToken token = SaveReferences.Write(renderer);

            Assert.AreEqual(_owner.Id, (string)token["id"]);
            Assert.AreEqual("Visual", (string)token["path"]);
            Assert.AreEqual("SpriteRenderer", (string)token["component"]);
            Assert.AreSame(renderer, SaveReferences.Read(token, typeof(SpriteRenderer)));
        }

        [Test]
        public void Ref_GameObjectAndTransform_RoundTrip()
        {
            GameObject child = Child("Hand");

            JToken token = SaveReferences.Write(child);

            Assert.IsNull(token["component"]);
            Assert.AreSame(child, SaveReferences.Read(token, typeof(GameObject)));
            Assert.AreSame(child.transform, SaveReferences.Read(token, typeof(Transform)));
        }

        [Test]
        public void Ref_SecondComponentOfType_Numbered()
        {
            _owner.gameObject.AddComponent<BoxCollider2D>();
            var second = _owner.gameObject.AddComponent<BoxCollider2D>();

            JToken token = SaveReferences.Write(second);

            Assert.AreEqual("BoxCollider2D#1", (string)token["component"]);
            Assert.IsNull(token["path"], "a component of the Saveable's own object has no path");
            Assert.AreSame(second, SaveReferences.Read(token, typeof(BoxCollider2D)));
        }

        [Test]
        public void Ref_Prefab_ByPrefabId()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestScenes.PREFAB);
            string id = prefab.GetComponent<Saveable>().PrefabId;
            _settings.SetCatalogs(new[] { new SaveSystemSettings.PrefabEntry { Id = id, Prefab = prefab } },
                new SaveSystemSettings.SceneEntry[0]);

            JToken token = SaveReferences.Write(prefab);

            Assert.AreEqual(id, (string)token["prefab"]);
            Assert.AreSame(prefab, SaveReferences.Read(token, typeof(GameObject)));
        }

        [Test]
        public void Ref_Asset_ByKey()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(TestScenes.ICON);
            _settings.SetAssetCatalog(TestScenes.IconEntries());

            JToken token = SaveReferences.Write(icon);

            StringAssert.Contains(AssetDatabase.AssetPathToGUID(TestScenes.ICON) + ":", (string)token["asset"]);
            Assert.AreSame(icon, SaveReferences.Read(token, typeof(Sprite)));
        }

        [Test]
        public void Ref_SceneObjectOutsideSaveable_UnresolvedWithWarning()
        {
            GameObject loose = Loose();

            LogAssert.Expect(LogType.Warning, new Regex("outside any Saveable"));
            JToken token = SaveReferences.Write(loose);

            Assert.IsTrue(SaveReferences.IsUnresolved(token), "not null: the object held a reference the save cannot express");
        }

        [Test]
        public void Ref_ContainsUnresolved_FindsItInsideListsAndClasses()
        {
            JToken unresolved = SaveReferences.UnresolvedToken();

            Assert.IsTrue(SaveReferences.ContainsUnresolved(unresolved));
            Assert.IsTrue(SaveReferences.ContainsUnresolved(new JArray(new JObject { ["id"] = "a" }, unresolved)));
            Assert.IsTrue(SaveReferences.ContainsUnresolved(new JObject { ["spot"] = new JObject { ["inner"] = unresolved } }));
            Assert.IsFalse(SaveReferences.ContainsUnresolved(new JArray(new JObject { ["id"] = "a" }, JValue.CreateNull())));
            Assert.IsFalse(SaveReferences.ContainsUnresolved(new JObject { ["unresolved"] = true, ["step"] = 1 }),
                "a class of the game with a field of that name is not the marker");
            Assert.IsFalse(SaveReferences.ContainsUnresolved(null));
        }

        // ---------- All Fields: references are saved when the save can express them ----------

        [Test]
        public void AllFields_ReferenceToSaveable_RoundTrip()
        {
            var probe = _owner.gameObject.AddComponent<MembersProbe>();
            Transform hand = Child("Hand").transform;
            probe.Target = hand;
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(MembersProbe));

            JToken data = serializer.Capture(probe);
            probe.Target = null;
            serializer.Restore(probe, data);

            Assert.AreEqual(_owner.Id, (string)data["Target"]["id"]);
            Assert.AreSame(hand, probe.Target);
        }

        [Test]
        public void AllFields_ReferenceTheSaveCannotExpress_LeftOutQuietly()
        {
            var probe = _owner.gameObject.AddComponent<MembersProbe>();
            probe.Target = Loose().transform;
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(MembersProbe));
            int warnings = 0;
            void Count(string condition, string stackTrace, LogType type) => warnings += type == LogType.Warning ? 1 : 0;

            Application.logMessageReceived += Count;
            var data = (JObject)serializer.Capture(probe);
            Application.logMessageReceived -= Count;
            Transform wired = Child("Wired").transform;
            probe.Target = wired;
            serializer.Restore(probe, data);

            Assert.IsFalse(data.ContainsKey("Target"), "wiring the save cannot point at is not written at all");
            Assert.AreEqual(0, warnings, "nobody picked this member by hand: no warning");
            Assert.AreSame(wired, probe.Target, "a load keeps what the scene has there");
        }

        [Test]
        public void AllFields_NullReference_IsStateAndComesBack()
        {
            var probe = _owner.gameObject.AddComponent<MembersProbe>();
            probe.Target = null;
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(MembersProbe));

            JToken data = serializer.Capture(probe);
            probe.Target = Child("Later").transform;
            serializer.Restore(probe, data);

            Assert.IsNull(probe.Target, "a lost target stays lost");
        }

        [Test]
        public void AllFields_CollectionWithOneInexpressibleReference_WholeMemberLeftAlone()
        {
            var probe = _owner.gameObject.AddComponent<NestedProbe>();
            probe.Map = new Dictionary<string, GameObject> { { "own", _owner.gameObject }, { "loose", Loose() } };
            probe.Waves = new[] { new NestedProbe.Wave { Prefab = _owner.gameObject, Count = 2 } };
            probe.Plain.Kills = 5;
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(NestedProbe));

            var data = (JObject)serializer.Capture(probe);
            Dictionary<string, GameObject> wired = probe.Map;
            probe.Waves = new NestedProbe.Wave[0];
            probe.Plain.Kills = 0;
            serializer.Restore(probe, data);

            Assert.IsFalse(data.ContainsKey("Map"), "a map with a hole in it helps nobody");
            Assert.AreSame(wired, probe.Map);
            Assert.AreEqual(1, probe.Waves.Length);
            Assert.AreSame(_owner.gameObject, probe.Waves[0].Prefab, "a class with a reference inside is saved when it can be expressed");
            Assert.AreEqual(2, probe.Waves[0].Count);
            Assert.AreEqual(5, probe.Plain.Kills);
        }

        [Test]
        public void Selected_CollectionWithOneInexpressibleReference_KeepsTheSceneValue()
        {
            var probe = _owner.gameObject.AddComponent<NestedProbe>();
            GameObject loose = Loose();
            probe.Map = new Dictionary<string, GameObject> { { "own", _owner.gameObject }, { "loose", loose } };
            IComponentSerializer serializer = MembersSerializer.SelectedFor(typeof(NestedProbe), new[] { "Map" });
            LogAssert.Expect(LogType.Warning, new Regex(loose.name + ".*a load keeps what the scene has there"));

            JToken data = serializer.Capture(probe);
            var wired = new Dictionary<string, GameObject> { { "scene", _owner.gameObject } };
            probe.Map = wired;
            serializer.Restore(probe, data);

            Assert.IsTrue(SaveReferences.IsUnresolved(data["Map"]));
            Assert.AreSame(wired, probe.Map);
        }

        [Test]
        public void Fields_ReferenceTheSaveCannotExpress_KeepsTheSceneValue()
        {
            var links = _owner.gameObject.AddComponent<SaveTestLinks>();
            GameObject loose = Loose();
            links.Handle = loose.transform;
            links.Target = _owner;
            IComponentSerializer serializer = ComponentSerializerCatalog.Default.For(typeof(SaveTestLinks));
            LogAssert.Expect(LogType.Warning, new Regex(loose.name + ".*a load keeps what the scene has there"));

            JToken data = serializer.Capture(links);
            Transform wired = Child("Wired").transform;
            links.Handle = wired;
            links.Target = null;
            serializer.Restore(links, data);

            Assert.AreSame(wired, links.Handle);
            Assert.AreSame(_owner, links.Target, "the rest is loaded as usual");
        }

        // ---------- A saved reference that is not found on a load ----------

        [Test]
        public void Load_CollectionWithAReferenceThatIsGone_KeepsTheSceneValue_NoNullInTheGamesList()
        {
            // The game's list of actors was saved while it held an object the loaded scene does not have
            var probe = _owner.gameObject.AddComponent<NestedProbe>();
            probe.Map = new Dictionary<string, GameObject> { { "own", _owner.gameObject }, { "hand", Child("Hand") } };
            probe.Plain.Kills = 5;
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(NestedProbe));
            var data = (JObject)serializer.Capture(probe);
            data["Map"]["hand"]["id"] = "an-id-the-scene-does-not-have";

            var filledByTheScene = new Dictionary<string, GameObject> { { "scene", _owner.gameObject } };
            probe.Map = filledByTheScene;
            probe.Plain.Kills = 0;
            LogAssert.Expect(LogType.Warning, new Regex("was not found"));
            LogAssert.Expect(LogType.Warning, new Regex("NestedProbe.Map holds a saved reference that was not found.*keeps what the scene has"));
            serializer.Restore(probe, data);

            Assert.AreSame(filledByTheScene, probe.Map);
            Assert.AreEqual(5, probe.Plain.Kills, "the rest is loaded as usual");
        }

        [Test]
        public void Load_SingleReferenceThatIsGone_BecomesNull()
        {
            var probe = _owner.gameObject.AddComponent<MembersProbe>();
            probe.Target = Child("Hand").transform;
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(MembersProbe));
            var data = (JObject)serializer.Capture(probe);
            data["Target"]["id"] = "an-id-the-scene-does-not-have";

            LogAssert.Expect(LogType.Warning, new Regex("was not found"));
            serializer.Restore(probe, data);

            Assert.IsNull(probe.Target, "a target that is gone is no target");
        }

        [Test]
        public void Load_SaveFieldListWithAReferenceThatIsGone_KeepsTheSceneValue()
        {
            var links = _owner.gameObject.AddComponent<SaveTestLinks>();
            links.Many.Add(_owner);
            links.Target = _owner;
            IComponentSerializer serializer = ComponentSerializerCatalog.Default.For(typeof(SaveTestLinks));
            var data = (JObject)serializer.Capture(links);
            data["Many"][0]["id"] = "an-id-the-scene-does-not-have";

            links.Target = null;
            LogAssert.Expect(LogType.Warning, new Regex("was not found"));
            LogAssert.Expect(LogType.Warning, new Regex("SaveTestLinks.Many holds a saved reference that was not found"));
            serializer.Restore(links, data);

            Assert.AreEqual(1, links.Many.Count);
            Assert.AreSame(_owner, links.Many[0], "the list the scene has, not one with a null in it");
            Assert.AreSame(_owner, links.Target);
        }

        private static GameObject Loose() => new("Loose" + System.Guid.NewGuid().ToString("N"));

        [Test]
        public void Ref_Missing_NullWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("was not found"));

            Assert.IsNull(SaveReferences.Read(JObject.Parse("{ \"id\": \"no-such-id\" }"), typeof(GameObject)));
        }

        [Test]
        public void Fields_References_RoundTrip()
        {
            var links = _owner.gameObject.AddComponent<SaveTestLinks>();
            GameObject handle = Child("Handle");
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(TestScenes.ICON);
            _settings.SetAssetCatalog(TestScenes.IconEntries());
            links.Target = _owner;
            links.Handle = handle.transform;
            links.Icon = icon;
            links.Many.Add(_owner);
            IComponentSerializer serializer = ComponentSerializerCatalog.Default.For(typeof(SaveTestLinks));
            JToken data = serializer.Capture(links);

            links.Target = null;
            links.Handle = null;
            links.Icon = null;
            links.Many.Clear();
            serializer.Restore(links, data);

            Assert.AreSame(_owner, links.Target);
            Assert.AreSame(handle.transform, links.Handle);
            Assert.AreSame(icon, links.Icon);
            Assert.AreSame(_owner, links.Many[0]);
        }
    }
}

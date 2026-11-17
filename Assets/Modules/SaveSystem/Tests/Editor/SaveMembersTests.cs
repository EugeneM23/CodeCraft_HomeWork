using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>Which members All Fields and Selected see, and how MembersSerializer keeps them.</summary>
    public class SaveMembersTests
    {
        private sealed class PlainData
        {
            public int Coins;
            [SerializeField] private int _kept;
            private int _hidden;
            public Sprite Icon;
            public Action Callback;

            public string Name { get; set; }
            public int Computed => Coins * 2;

            public int Kept
            {
                get => _kept;
                set => _kept = value;
            }

            public int Hidden
            {
                get => _hidden;
                set => _hidden = value;
            }
        }

        private sealed class MarkedData
        {
            [Save] public int Coins;
            public int NotSaved;
            [Save] public string Name { get; set; }
        }

        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static string[] Names(IEnumerable<SaveMember> members) => members.Select(member => member.Name).ToArray();

        // ---------- Which members ----------

        [Test]
        public void All_Script_SerializedValueFieldsAndAutoProperties()
        {
            // Auto-properties hold state Unity does not serialize (CurrentHealth { get; set; }); Speed and Timer have code
            CollectionAssert.AreEquivalent(new[] { "Health", "_speed", "Ids", "Prop", "Unlocked" },
                Names(SaveMembers.All(typeof(MembersProbe))));
        }

        [Test]
        public void All_Script_SkipsValuesHidingReferencesInside()
        {
            CollectionAssert.AreEquivalent(new[] { "Plain" }, Names(SaveMembers.All(typeof(NestedProbe))));
            Assert.IsTrue(SaveMembers.HoldsUnityObjects(typeof(NestedProbe.Wave[])));
            Assert.IsTrue(SaveMembers.HoldsUnityObjects(typeof(Dictionary<string, GameObject>)));
            Assert.IsTrue(SaveMembers.HoldsUnityObjects(typeof(NestedProbe.Deep)));
            Assert.IsFalse(SaveMembers.HoldsUnityObjects(typeof(NestedProbe.Stats)));
        }

        [Test]
        public void AllWithReferences_Script_AddsMembersThatHoldUnityObjects()
        {
            // What the All Fields mode keeps: the values plus references; a delegate and runtime state still stay out
            CollectionAssert.AreEquivalent(new[] { "Health", "_speed", "Ids", "Prop", "Unlocked", "Target", "_body" },
                Names(SaveMembers.AllWithReferences(typeof(MembersProbe))));
            CollectionAssert.AreEquivalent(new[] { "Plain", "Waves", "Map", "Nested" },
                Names(SaveMembers.AllWithReferences(typeof(NestedProbe))));
        }

        [Test]
        public void AllWithReferences_PlainObjectAndUnityComponent_SameAsAll()
        {
            CollectionAssert.AreEqual(Names(SaveMembers.All(typeof(PlainData))), Names(SaveMembers.AllWithReferences(typeof(PlainData))));
            CollectionAssert.AreEqual(Names(SaveMembers.All(typeof(Light))), Names(SaveMembers.AllWithReferences(typeof(Light))));
        }

        private class VirtualBase
        {
            [Save] public virtual int Level { get; set; }
        }

        private sealed class GetterOverride : VirtualBase
        {
            public override int Level => base.Level;
        }

        private sealed class MarkedOverride : VirtualBase
        {
            [Save] public override int Level { get; set; }
        }

        [Test]
        public void Marked_OverriddenProperty_OnceAndQuiet()
        {
            var logs = new List<string>();
            void Collect(string message, string stack, LogType type) => logs.Add(message);
            Application.logMessageReceived += Collect;
            try
            {
                CollectionAssert.AreEqual(new[] { "Level" }, Names(SaveMembers.Marked(typeof(GetterOverride))));
                CollectionAssert.AreEqual(new[] { "Level" }, Names(SaveMembers.Marked(typeof(MarkedOverride))));
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }

            CollectionAssert.IsEmpty(logs);
            var target = new GetterOverride { Level = 4 };
            Assert.AreEqual(4, SaveMembers.Marked(typeof(GetterOverride))[0].GetValue(target));
        }

        [Test]
        public void Candidates_Script_FieldsAndPropertiesIncludingStateAndReferences()
        {
            string[] names = Names(SaveMembers.Candidates(typeof(MembersProbe)));

            CollectionAssert.IsSubsetOf(
                new[] { "Health", "_speed", "Ids", "Skipped", "_timer", "Target", "_body", "Prop", "Speed", "Timer", "Body" }, names);
            CollectionAssert.DoesNotContain(names, "Callback");
            CollectionAssert.DoesNotContain(names, "ReadOnlyProp");
            CollectionAssert.DoesNotContain(names, "enabled");
            Assert.IsFalse(names.Any(name => name.Contains("k__BackingField")), "auto-properties show as properties");
        }

        [Test]
        public void All_PlainClass_PublicAndSerializedFieldsAndProperties()
        {
            CollectionAssert.AreEquivalent(new[] { "Coins", "_kept", "Name", "Kept", "Hidden" },
                Names(SaveMembers.All(typeof(PlainData))));
        }

        [Test]
        public void All_MarkedMembers_OnlyThem()
        {
            CollectionAssert.AreEquivalent(new[] { "Coins", "Name" }, Names(SaveMembers.All(typeof(MarkedData))));
            CollectionAssert.AreEquivalent(new[] { "Coins", "Name" }, Names(SaveMembers.Marked(typeof(MarkedData))));
        }

        [Test]
        public void All_NativeComponent_SimplePropertiesWithoutRiskyOnes()
        {
            string[] names = Names(SaveMembers.All(typeof(Light)));

            CollectionAssert.IsSubsetOf(new[] { "type", "color", "intensity", "range" }, names);
            foreach (string risky in new[] { "enabled", "name", "tag", "hideFlags", "cookie", "flare" })
                CollectionAssert.DoesNotContain(names, risky);
        }

        [Test]
        public void Candidates_NativeComponent_AssetReferencesButNoCopies()
        {
            string[] names = Names(SaveMembers.Candidates(typeof(SpriteRenderer)));

            CollectionAssert.Contains(names, "sprite");
            CollectionAssert.Contains(names, "color");
            CollectionAssert.Contains(names, "sharedMaterial");
            CollectionAssert.DoesNotContain(names, "material");
            CollectionAssert.DoesNotContain(names, "materials");
        }

        [Test]
        public void Marked_PropertyWithoutSetter_SkippedWithWarning()
        {
            SaveMembers.ForgetTypes(); // the warning comes once per type: another test may have seen it first
            LogAssert.Expect(LogType.Warning, new Regex("GetOnly"));

            string[] names = Names(SaveMembers.Marked(typeof(PropertyProbe)));

            CollectionAssert.AreEquivalent(new[] { "Score", "Renamed" }, names);
            Assert.AreEqual("_old", SaveMembers.Marked(typeof(PropertyProbe)).Single(member => member.Name == "Renamed").Key);
        }

        // ---------- Keeping them ----------

        [Test]
        public void AllFields_Script_RoundTrip()
        {
            var probe = new GameObject("Probe").AddComponent<MembersProbe>();
            probe.Health = 3;
            probe.Speed = 7f;
            probe.Ids.Add(4);
            probe.Timer = 9f;
            probe.Prop = 5;
            probe.Unlock();
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(MembersProbe));

            JToken data = serializer.Capture(probe);
            var copy = new GameObject("Copy").AddComponent<MembersProbe>();
            serializer.Restore(copy, data);

            Assert.AreEqual(3, copy.Health);
            Assert.AreEqual(7f, copy.Speed);
            CollectionAssert.AreEqual(new[] { 4 }, copy.Ids);
            Assert.AreEqual(5, copy.Prop, "an auto-property is state");
            Assert.IsTrue(copy.Unlocked, "a private setter too");
            Assert.AreEqual(0f, copy.Timer, "runtime state is not part of All Fields");
        }

        [Test]
        public void Selected_ReferenceTheSaveCannotExpress_KeepsTheSceneValueOnLoad()
        {
            // A manager the script finds in Start: not a Saveable, so the save cannot point at it — and must not clear it
            var probe = new GameObject("Probe").AddComponent<MembersProbe>();
            probe.Target = new GameObject("UnsavedManager").transform;
            IComponentSerializer serializer = MembersSerializer.SelectedFor(typeof(MembersProbe), new[] { "Target", "Health" });
            LogAssert.Expect(LogType.Warning, new Regex("UnsavedManager.*a load keeps what the scene has there"));
            probe.Health = 8;

            JToken data = serializer.Capture(probe);
            var copy = new GameObject("Copy").AddComponent<MembersProbe>();
            Transform own = new GameObject("FoundInStart").transform;
            copy.Target = own;
            serializer.Restore(copy, data);

            Assert.AreSame(own, copy.Target);
            Assert.AreEqual(8, copy.Health, "the rest is loaded as usual");
        }

        [Test]
        public void Selected_OnlyChosenMembers_PrivateStateToo()
        {
            var probe = new GameObject("Probe").AddComponent<MembersProbe>();
            probe.Health = 3;
            probe.Timer = 9f;
            probe.Prop = 5;
            IComponentSerializer serializer = MembersSerializer.SelectedFor(typeof(MembersProbe), new[] { "_timer", "Prop" });

            var data = (JObject)serializer.Capture(probe);
            var copy = new GameObject("Copy").AddComponent<MembersProbe>();
            serializer.Restore(copy, data);

            CollectionAssert.AreEquivalent(new[] { "_timer", "Prop" }, data.Properties().Select(property => property.Name));
            Assert.AreEqual(9f, copy.Timer);
            Assert.AreEqual(5, copy.Prop);
            Assert.AreEqual(10, copy.Health);
        }

        [Test]
        public void Selected_UnknownName_Ignored()
        {
            IComponentSerializer serializer = MembersSerializer.SelectedFor(typeof(MembersProbe), new[] { "Health", "Gone" });
            var probe = new GameObject("Probe").AddComponent<MembersProbe>();

            var data = (JObject)serializer.Capture(probe);

            CollectionAssert.AreEquivalent(new[] { "Health" }, data.Properties().Select(property => property.Name));
        }

        [Test]
        public void Restore_BadMember_OthersApplyWithWarning()
        {
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(MembersProbe));
            var probe = new GameObject("Probe").AddComponent<MembersProbe>();
            LogAssert.Expect(LogType.Warning, new Regex("Health"));

            serializer.Restore(probe, new JObject { ["Health"] = "not a number", ["_speed"] = 5f });

            Assert.AreEqual(10, probe.Health);
            Assert.AreEqual(5f, probe.Speed);
        }

        [Test]
        public void Restore_MissingKeys_LeaveMembersAlone()
        {
            var probe = new GameObject("Probe").AddComponent<MembersProbe>();
            probe.Health = 6;

            MembersSerializer.AllFor(typeof(MembersProbe)).Restore(probe, new JObject { ["_speed"] = 1f });

            Assert.AreEqual(6, probe.Health);
        }

        [Test]
        public void AllFields_Light_RoundTrip()
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Spot;
            light.intensity = 3.5f;
            light.color = Color.red;
            light.range = 12f;
            IComponentSerializer serializer = MembersSerializer.AllFor(typeof(Light));

            JToken data = serializer.Capture(light);
            var copy = new GameObject("Copy").AddComponent<Light>();
            serializer.Restore(copy, data);

            Assert.AreEqual(LightType.Spot, copy.type);
            Assert.AreEqual(3.5f, copy.intensity);
            Assert.AreEqual(Color.red, copy.color);
            Assert.AreEqual(12f, copy.range);
        }

        private static readonly Type[] NativeTypes =
        {
            typeof(Light), typeof(Camera), typeof(LineRenderer), typeof(TrailRenderer), typeof(SpriteRenderer),
            typeof(MeshRenderer), typeof(MeshFilter), typeof(BoxCollider2D), typeof(CircleCollider2D), typeof(Rigidbody2D),
            typeof(BoxCollider), typeof(Rigidbody), typeof(AudioSource), typeof(Animator), typeof(Transform),
            typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image)
        };

        [Test]
        public void All_UnityScript_PublicPropertiesNotFields()
        {
            string[] names = Names(SaveMembers.All(typeof(UnityEngine.UI.Image)));

            CollectionAssert.IsSubsetOf(new[] { "color", "fillAmount", "raycastTarget" }, names);
            Assert.IsFalse(names.Any(name => name.StartsWith("m_")), "uGUI keeps its state behind setters");
        }

        [Test]
        public void AllFields_CommonNativeComponents_RoundTripQuietly([ValueSource(nameof(NativeTypes))] Type type)
        {
            var source = new GameObject("Source");
            Component component = type == typeof(Transform) ? source.transform : source.AddComponent(type);
            IComponentSerializer serializer = MembersSerializer.AllFor(type);
            var logs = new List<string>();
            void Collect(string message, string stack, LogType logType)
            {
                if (logType != LogType.Log)
                    logs.Add($"{logType}: {message}");
            }

            Application.logMessageReceived += Collect;
            JToken data;
            JToken again;
            try
            {
                data = serializer.Capture(component);
                var target = new GameObject("Target");
                Component copy = type == typeof(Transform) ? target.transform : target.AddComponent(type);
                serializer.Restore(copy, data);
                again = serializer.Capture(copy);
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }

            // Any warning or error means a risky member got into All Fields
            CollectionAssert.IsEmpty(logs);
            Assert.AreEqual(data.ToString(), again.ToString());
        }
    }
}

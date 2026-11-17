using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Save modes of a Saveable's components: Auto, All Fields, Selected; and what the object state keeps.</summary>
    public class SaveModeTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static ComponentSerializerCatalog Catalog => ComponentSerializerCatalog.Default;

        private static Saveable MakeSaveable(out MembersProbe probe)
        {
            var root = new GameObject("Root");
            var saveable = root.AddComponent<Saveable>();
            saveable.SetId(System.Guid.NewGuid().ToString("N"));
            probe = root.AddComponent<MembersProbe>();
            return saveable;
        }

        private static ObjectData Capture(Saveable saveable) => ObjectState.Capture(saveable, Catalog);

        [Test]
        public void Mode_DefaultAuto_SetAndClear()
        {
            Saveable saveable = MakeSaveable(out _);
            Assert.AreEqual(SaveMode.Auto, saveable.ModeOf("MembersProbe"));

            saveable.SetMode("MembersProbe", SaveMode.AllFields);
            Assert.AreEqual(SaveMode.AllFields, saveable.ModeOf("MembersProbe"));

            saveable.SetMembers("MembersProbe", new[] { "Health", "_timer" });
            Assert.AreEqual(SaveMode.Selected, saveable.ModeOf("MembersProbe"));
            CollectionAssert.AreEqual(new[] { "Health", "_timer" }, saveable.MembersOf("MembersProbe"));

            saveable.SetMode("MembersProbe", SaveMode.Auto);
            Assert.AreEqual(SaveMode.Auto, saveable.ModeOf("MembersProbe"));
            CollectionAssert.IsEmpty(saveable.MembersOf("MembersProbe"));
        }

        [Test]
        public void Auto_ScriptWithoutSerializer_NotSaved()
        {
            Saveable saveable = MakeSaveable(out _);

            Assert.IsFalse(Capture(saveable).Components.ContainsKey("MembersProbe"));
        }

        [Test]
        public void AllFields_ScriptWithoutSaveFields_Saved()
        {
            Saveable saveable = MakeSaveable(out MembersProbe probe);
            probe.Health = 4;
            saveable.SetMode("MembersProbe", SaveMode.AllFields);

            ComponentData data = Capture(saveable).Components["MembersProbe"];

            Assert.AreEqual(4, (int)data.Data["Health"]);
            Assert.IsNull(data.Data["_timer"]);
            Assert.AreEqual(true, data.Enabled);
        }

        [Test]
        public void Selected_OverridesBuiltIn()
        {
            Saveable saveable = MakeSaveable(out _);
            saveable.transform.localPosition = new Vector3(1f, 2f, 3f);
            saveable.SetMembers("Transform", new[] { "localPosition" });

            var data = (JObject)Capture(saveable).Components["Transform"].Data;

            CollectionAssert.AreEquivalent(new[] { "localPosition" }, data.Properties().Select(property => property.Name));
        }

        [Test]
        public void Unchecked_ModeIgnored()
        {
            Saveable saveable = MakeSaveable(out _);
            saveable.SetMode("MembersProbe", SaveMode.AllFields);
            saveable.SetSaved("MembersProbe", false);

            Assert.IsFalse(Capture(saveable).Components.ContainsKey("MembersProbe"));
            Assert.AreEqual(SaveMode.AllFields, saveable.ModeOf("MembersProbe"), "checking it again brings the mode back");
        }

        [Test]
        public void Modes_RoundTripThroughObjectState()
        {
            Saveable saveable = MakeSaveable(out MembersProbe probe);
            var light = saveable.gameObject.AddComponent<Light>();
            saveable.SetMembers("MembersProbe", new[] { "_timer", "Health" });
            saveable.SetMode("Light", SaveMode.AllFields);
            probe.Timer = 2.5f;
            probe.Health = 1;
            light.intensity = 4f;
            ObjectData data = Capture(saveable);

            probe.Timer = 0f;
            probe.Health = 10;
            light.intensity = 1f;
            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(2.5f, probe.Timer);
            Assert.AreEqual(1, probe.Health);
            Assert.AreEqual(4f, light.intensity);
        }

        [Test]
        public void Modes_LoadedWithAnotherMode_MatchingKeysApply()
        {
            Saveable saveable = MakeSaveable(out MembersProbe probe);
            saveable.SetMode("MembersProbe", SaveMode.AllFields);
            probe.Health = 2;
            probe.Speed = 8f;
            ObjectData data = Capture(saveable);

            probe.Health = 10;
            probe.Speed = 1f;
            saveable.SetMembers("MembersProbe", new[] { "Health" });
            ObjectState.RestoreComponents(saveable, data, Catalog);

            Assert.AreEqual(2, probe.Health);
            Assert.AreEqual(1f, probe.Speed, "a member that is not selected any more stays as it is");
        }

        [Test]
        public void CaptureState_PublicSnapshotUsesModes()
        {
            Saveable saveable = MakeSaveable(out MembersProbe probe);
            saveable.SetMembers("MembersProbe", new[] { "Prop" });
            probe.Prop = 7;

            JToken state = saveable.CaptureState();
            probe.Prop = 0;
            saveable.RestoreState(state);

            Assert.AreEqual(7, probe.Prop);
        }

        [Test]
        public void Rules_SurviveDuplicateAndAreSerialized()
        {
            Saveable saveable = MakeSaveable(out _);
            saveable.SetMembers("MembersProbe", new[] { "Health" });

            Saveable copy = Object.Instantiate(saveable.gameObject).GetComponent<Saveable>();

            Assert.AreEqual(SaveMode.Selected, copy.ModeOf("MembersProbe"));
            CollectionAssert.AreEqual(new List<string> { "Health" }, copy.MembersOf("MembersProbe"));
        }
    }
}

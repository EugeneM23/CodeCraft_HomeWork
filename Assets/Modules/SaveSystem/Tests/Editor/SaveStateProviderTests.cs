using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>
    /// Plain objects a component hands over through ISaveStateProvider are saved with their object: which members,
    /// under which keys, back into the same instances, with the callbacks.
    /// </summary>
    public class SaveStateProviderTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static Saveable Make(out StateProviderProbe probe)
        {
            var root = new GameObject("Unit");
            var saveable = root.AddComponent<Saveable>();
            saveable.SetId(System.Guid.NewGuid().ToString("N"));
            probe = root.AddComponent<StateProviderProbe>();
            return saveable;
        }

        [Test]
        public void State_PlainObjects_SavedUnderProviderAndClass()
        {
            Saveable saveable = Make(out StateProviderProbe probe);
            probe.Life.Current = 40;
            probe.First.Bullets = 3;
            probe.Second.Bullets = 9;

            var state = (JObject)saveable.CaptureState()["state"];

            CollectionAssert.AreEquivalent(
                new[] { "StateProviderProbe:Health", "StateProviderProbe:Clip", "StateProviderProbe:Clip#1" },
                state.Properties().Select(property => property.Name).ToArray(),
                "the null and the Unity object in the list are skipped");
            Assert.AreEqual(40, (int)state["StateProviderProbe:Health"]["_current"]);
            Assert.AreEqual(3, (int)state["StateProviderProbe:Clip"]["_bullets"]);
            Assert.AreEqual(9, (int)state["StateProviderProbe:Clip#1"]["_bullets"]);
        }

        [Test]
        public void State_MarkedClass_OnlyItsMarkedMembers()
        {
            Saveable saveable = Make(out _);

            var health = (JObject)saveable.CaptureState()["state"]["StateProviderProbe:Health"];

            CollectionAssert.AreEqual(new[] { "_current" }, health.Properties().Select(property => property.Name).ToArray());
        }

        [Test]
        public void State_UnmarkedClass_PublicAndSimplePrivateValues()
        {
            Saveable saveable = Make(out _);

            string[] names = ((JObject)saveable.CaptureState()["state"]["StateProviderProbe:Clip"])
                .Properties().Select(property => property.Name).ToArray();

            CollectionAssert.IsSubsetOf(new[] { "Capacity", "_bullets", "Heat" }, names);
            CollectionAssert.DoesNotContain(names, "_given", "what it was given at construction");
            CollectionAssert.DoesNotContain(names, "_history", "a list");
            CollectionAssert.DoesNotContain(names, "_service", "a reference");
            CollectionAssert.DoesNotContain(names, "_scratch", "marked NonSerialized");
        }

        [Test]
        public void State_RoundTrip_SameInstancesGetTheirValues()
        {
            Saveable saveable = Make(out StateProviderProbe probe);
            probe.Life.Current = 40;
            probe.First.Bullets = 3;
            probe.First.Warm(0.5f);
            probe.Second.Bullets = 9;
            JToken state = saveable.CaptureState();
            StateProviderProbe.Clip first = probe.First;
            object service = probe.First.Service;

            probe.Life.Current = 1;
            probe.First.Bullets = 30;
            probe.First.Warm(0f);
            probe.First.SetScratch(5);
            probe.Second.Bullets = 30;
            saveable.RestoreState(state);

            Assert.AreSame(first, probe.First, "filled in place, not replaced");
            Assert.AreEqual(40, probe.Life.Current);
            Assert.AreEqual(3, probe.First.Bullets);
            Assert.AreEqual(0.5f, probe.First.Heat);
            Assert.AreEqual(9, probe.Second.Bullets, "two objects of one class are told apart by their order");
            Assert.AreSame(service, probe.First.Service);
            Assert.AreEqual(7, probe.First.Given);
            Assert.AreEqual(5, probe.First.Scratch, "members that are not saved keep what they have");
        }

        [Test]
        public void State_Callbacks_BeforeSaveAndAfterLoad()
        {
            Saveable saveable = Make(out StateProviderProbe probe);
            probe.Life.Current = 40;

            JToken state = saveable.CaptureState();
            Assert.AreEqual(1, probe.Life.BeforeSaves);
            Assert.AreEqual(100, probe.Life.Shown, "a save tells nobody that the value was loaded");

            saveable.RestoreState(state);

            Assert.AreEqual(40, probe.Life.Shown, "OnAfterLoad is where the object tells its views");
        }

        [Test]
        public void State_NoProvider_NoStateKeyInTheSave()
        {
            var root = new GameObject("Plain");
            var saveable = root.AddComponent<Saveable>();
            saveable.SetId(System.Guid.NewGuid().ToString("N"));

            Assert.IsNull(saveable.CaptureState()["state"], "saves of objects without providers look as before");
        }

        [Test]
        public void State_ProviderThrows_LoggedAndTheRestIsSaved()
        {
            Saveable saveable = Make(out StateProviderProbe probe);
            probe.Throws = true;
            LogAssert.Expect(LogType.Exception, new Regex("the container is gone"));

            JToken state = saveable.CaptureState();

            Assert.IsNull(state["state"]);
            Assert.IsNotNull(state["components"]["Transform"], "the components are saved all the same");
        }

        [Test]
        public void State_SavedObjectMissingNow_SkippedQuietly()
        {
            Saveable saveable = Make(out StateProviderProbe probe);
            probe.First.Bullets = 3;
            JToken state = saveable.CaptureState();
            ((JObject)state["state"]).Add("StateProviderProbe:Gone", new JObject { ["value"] = 1 });

            probe.First.Bullets = 30;
            saveable.RestoreState(state);

            Assert.AreEqual(3, probe.First.Bullets);
        }
    }
}

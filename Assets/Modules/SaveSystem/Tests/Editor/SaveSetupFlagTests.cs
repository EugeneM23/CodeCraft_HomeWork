using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// A private flag that says "I have set myself up" is not saved: on a fresh object it would make the script skip
    /// the setup the flag stands for.
    /// </summary>
    public class SaveSetupFlagTests
    {
        /// <summary>A plain object handed over by an ISaveStateProvider.</summary>
        private sealed class Model
        {
            public bool Setup = true; // public: saved as it is

#pragma warning disable CS0414 // read through reflection only
            private bool _initialized = true;
            private bool _armed = true;
            private int _count = 3;
#pragma warning restore CS0414
        }

        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void Script_PrivateSetupFlags_AreNotAmongItsMembers()
        {
            string[] names = SaveMembers.AllWithPrivate(typeof(SetupFlagProbe)).Select(member => member.Name).ToArray();

            CollectionAssert.DoesNotContain(names, "isInitialized");
            CollectionAssert.DoesNotContain(names, "_isSetup");
            CollectionAssert.DoesNotContain(names, "m_Subscribed");
            CollectionAssert.DoesNotContain(names, "Initialized", "an auto-property only the script sets is the same flag");
            CollectionAssert.IsSubsetOf(new[] { "_armed", "Ready", "_initialSpeed" }, names, "state of the game and plain values stay");
            CollectionAssert.IsSubsetOf(new[] { "PublicInitialized", "_serializedInit" }, names,
                "what Unity serializes is a setting, whatever it is called");
        }

        [Test]
        public void PlainObject_PrivateSetupFlag_IsNotAmongItsState()
        {
            string[] names = SaveMembers.State(typeof(Model)).Select(member => member.Name).ToArray();

            CollectionAssert.DoesNotContain(names, "_initialized");
            CollectionAssert.IsSubsetOf(new[] { "Setup", "_armed", "_count" }, names);
        }

        [Test]
        public void Script_SavedSetUp_RestoredOnAFreshObject_ItStillSetsItselfUp()
        {
            var root = new GameObject("Effect");
            var saveable = root.AddComponent<Saveable>();
            saveable.SetId(System.Guid.NewGuid().ToString("N"));
            SetupFlagProbe probe = root.AddComponent<SetupFlagProbe>();
            saveable.SetMode("SetupFlagProbe", SaveMode.AllFields);
            probe.Set(setUp: true, armed: true);
            JToken state = saveable.CaptureState();

            // As after a load: the object was made again and has not run its setup yet
            probe.Set(setUp: false, armed: false);
            saveable.RestoreState(state);

            Assert.IsTrue(probe.Ready, "the state of the game is back");
            Assert.IsFalse(probe.Initialized, "the script has not set itself up on this object: the flag must say so");
        }
    }
}

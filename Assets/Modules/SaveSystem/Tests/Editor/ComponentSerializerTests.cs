using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>The typed component serializer: round trip, type and version, one-step upgrades.</summary>
    public class ComponentSerializerTests
    {
        private TempScene _scene;

        [SetUp]
        public void SetUp() => _scene = new TempScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void ComponentSerializer_CaptureThenRestore_RoundTrips()
        {
            var source = new GameObject("Source").AddComponent<SerializerProbe>();
            source.Speed = 2.5f;
            source.Mode = "run";
            IComponentSerializer serializer = new ProbeSerializer();

            var copy = new GameObject("Copy").AddComponent<SerializerProbe>();
            serializer.Restore(copy, serializer.Capture(source));

            Assert.AreEqual(2.5f, copy.Speed);
            Assert.AreEqual("run", copy.Mode);
        }

        [Test]
        public void ComponentSerializer_TypeAndVersion()
        {
            IComponentSerializer serializer = new ProbeSerializer();

            Assert.AreEqual(typeof(SerializerProbe), serializer.ComponentType);
            Assert.AreEqual(2, serializer.Version);
        }

        [Test]
        public void ComponentSerializer_Upgrade_OneStep()
        {
            IComponentSerializer serializer = new ProbeSerializer();

            JToken upgraded = serializer.Upgrade(JObject.Parse("{ \"spd\": 3 }"), 1);

            Assert.AreEqual(3, (int)upgraded["speed"]);
        }

        [Test]
        public void ComponentSerializer_NoStep_ThrowsMigrationException() =>
            Assert.Throws<SaveMigrationException>(() => ((IComponentSerializer)new ProbeSerializer()).Upgrade(new JObject(), 5));
    }
}

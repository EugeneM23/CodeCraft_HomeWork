using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>[Save] fields: what is kept, under which key, and that a bad value changes nothing.</summary>
    public class FieldsSerializerTests
    {
        private TempScene _scene;
        private FieldsProbe _probe;

        [SetUp]
        public void SetUp()
        {
            _scene = new TempScene();
            _probe = new GameObject("Probe").AddComponent<FieldsProbe>();
        }

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private static IComponentSerializer Serializer => FieldsSerializer.For(typeof(FieldsProbe));

        [Test]
        public void Fields_PrivatePublicAndInherited_RoundTrip()
        {
            _probe.Hp = 7;
            _probe.Title = "Knight";
            _probe.Level = 3;
            _probe.Items.Add(5);
            _probe.Scores["Boss"] = 2;
            _probe.Data.A = 1;
            _probe.Data.B = 2;

            JToken data = Serializer.Capture(_probe);
            var copy = new GameObject("Copy").AddComponent<FieldsProbe>();
            Serializer.Restore(copy, data);

            Assert.AreEqual(7, copy.Hp);
            Assert.AreEqual("Knight", copy.Title);
            Assert.AreEqual(3, copy.Level);
            CollectionAssert.AreEqual(new[] { 5 }, copy.Items);
            Assert.AreEqual(2, copy.Scores["Boss"]);
            Assert.AreEqual(1, copy.Data.A);
            Assert.AreEqual(2, copy.Data.B);
        }

        [Test]
        public void Fields_Keys_FieldNamesAsIsAndAlias()
        {
            var json = (JObject)Serializer.Capture(_probe);

            Assert.IsNotNull(json["_hp"]);
            Assert.IsNotNull(json["Title"]);
            Assert.IsNotNull(json["_level"]);
            Assert.IsNotNull(json["_start"]);
            Assert.IsNull(json["_startPosition"]);
        }

        [Test]
        public void Fields_UnmarkedAndReadonly_NotSaved()
        {
            var json = (JObject)Serializer.Capture(_probe);

            Assert.IsNull(json["_setting"]);
            Assert.IsNull(json["_fixed"]);
        }

        [Test]
        public void Fields_Alias_ReadsFormerName()
        {
            Serializer.Restore(_probe, JObject.Parse("{ \"_start\": { \"x\": 1, \"y\": 2 } }"));

            Assert.AreEqual(new Vector2(1f, 2f), _probe.StartPosition);
        }

        [Test]
        public void Fields_MissingKey_KeepsValue()
        {
            _probe.Title = "Old";

            Serializer.Restore(_probe, JObject.Parse("{ \"_hp\": 3 }"));

            Assert.AreEqual(3, _probe.Hp);
            Assert.AreEqual("Old", _probe.Title);
        }

        [Test]
        public void Fields_UnknownKey_Ignored() =>
            Assert.DoesNotThrow(() => Serializer.Restore(_probe, JObject.Parse("{ \"gone\": 1 }")));

        [Test]
        public void Fields_WrongType_NothingAssigned()
        {
            _probe.Title = "Old";

            Assert.Catch(() => Serializer.Restore(_probe, JObject.Parse("{ \"Title\": \"New\", \"_hp\": \"not a number\" }")));

            Assert.AreEqual("Old", _probe.Title);
        }

        [Test]
        public void Fields_SerializableClass_FollowsUnityRules()
        {
            JToken inner = Serializer.Capture(_probe)["Data"];

            Assert.IsNotNull(inner["a"]);
            Assert.IsNotNull(inner["_b"]);
            Assert.IsNull(inner["c"], "properties are not saved");
            Assert.IsNull(inner["b"], "properties are not saved");
        }

        [Test]
        public void Fields_TypeWithoutSaveFields_NoSerializer() =>
            Assert.IsNull(FieldsSerializer.For(typeof(PlainProbe)));

        [Test]
        public void Fields_SaveOnProperties_RoundTripUnderTheirKeys()
        {
            var probe = new GameObject("Props").AddComponent<PropertyProbe>();
            probe.Score = 4;
            probe.Renamed = "x";
            IComponentSerializer serializer = FieldsSerializer.For(typeof(PropertyProbe));

            var json = (JObject)serializer.Capture(probe);
            var copy = new GameObject("Copy").AddComponent<PropertyProbe>();
            serializer.Restore(copy, json);

            Assert.AreEqual(4, (int)json["Score"]);
            Assert.AreEqual("x", (string)json["_old"]);
            Assert.IsNull(json["GetOnly"]);
            Assert.AreEqual(4, copy.Score);
            Assert.AreEqual("x", copy.Renamed);
        }
    }
}

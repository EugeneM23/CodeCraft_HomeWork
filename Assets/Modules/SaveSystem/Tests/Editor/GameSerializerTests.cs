using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Saving.Tests
{
    /// <summary>The typed base from the homework: service in the constructor, key rules, JSON round trip.</summary>
    public class GameSerializerTests
    {
        private sealed class Box
        {
            public int Value;
        }

        private sealed class BoxData
        {
            public int Value;
        }

        private sealed class BoxSerializer : GameSerializer<Box, BoxData>
        {
            public BoxSerializer(Box box, string key = "box", int version = 1) : base(box, key, version)
            {
            }

            protected override BoxData Serialize(Box box) => new() { Value = box.Value };
            protected override void Deserialize(Box box, BoxData data) => box.Value = data.Value;
        }

        [Test]
        public void GameSerializer_EmptyKey_Throws() =>
            Assert.Throws<ArgumentException>(() => new BoxSerializer(new Box(), key: ""));

        [Test]
        public void GameSerializer_ReservedKey_Throws() =>
            Assert.Throws<ArgumentException>(() => new BoxSerializer(new Box(), key: "$scenes"));

        [Test]
        public void GameSerializer_VersionBelowOne_Throws() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoxSerializer(new Box(), version: 0));

        [Test]
        public void GameSerializer_NullService_Throws() =>
            Assert.Throws<ArgumentNullException>(() => new BoxSerializer(null));

        [Test]
        public void GameSerializer_CaptureThenRestore_RoundTrips()
        {
            IGameSerializer writer = new BoxSerializer(new Box { Value = 5 });
            JToken data = writer.Capture();

            var box = new Box();
            IGameSerializer reader = new BoxSerializer(box);
            reader.Restore(data);

            Assert.AreEqual(5, (int)data["value"]);
            Assert.AreEqual(5, box.Value);
        }

        [Test]
        public void GameSerializer_NoUpgrade_ThrowsMigrationException()
        {
            IGameSerializer serializer = new BoxSerializer(new Box(), version: 2);

            var exception = Assert.Throws<SaveMigrationException>(() => serializer.Upgrade(new JObject(), 1));
            Assert.AreEqual(1, exception.FromVersion);
        }
    }
}

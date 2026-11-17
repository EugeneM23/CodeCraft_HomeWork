using System;
using System.Collections.Generic;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Register(key, object): a plain object or a ScriptableObject saved as a whole, filled back in place.</summary>
    public class ObjectSerializerTests
    {
        public sealed class Stats
        {
            public int Coins;
            public string Name { get; set; }
            public List<string> Keys = new();
            public Inventory Bag = new();
            private int _secret;

            public int Secret
            {
                get => _secret;
                set => _secret = value;
            }
        }

        public sealed class Inventory
        {
            public int Slots { get; set; }
            public List<int> Items = new();
        }

        public sealed class Picked
        {
            [Save] public int Level;
            public int Other;
        }

        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private SaveLoadService CreateService() => new(_storage, "1.0");

        [Test]
        public void Register_PlainObject_RoundTripIntoSameInstance()
        {
            var stats = new Stats { Coins = 5, Name = "Knight", Secret = 3 };
            stats.Keys.Add("red");
            stats.Bag.Slots = 4;
            stats.Bag.Items.Add(9);
            SaveLoadService writer = CreateService();
            writer.Register("stats", stats);
            writer.Save();

            var loaded = new Stats();
            List<string> keys = loaded.Keys;
            SaveLoadService reader = CreateService();
            reader.Register("stats", loaded);
            reader.Load();

            Assert.AreEqual(5, loaded.Coins);
            Assert.AreEqual("Knight", loaded.Name);
            Assert.AreEqual(3, loaded.Secret, "a public property is saved");
            CollectionAssert.AreEqual(new[] { "red" }, loaded.Keys);
            Assert.AreEqual(4, loaded.Bag.Slots, "nested plain objects keep their properties");
            CollectionAssert.AreEqual(new[] { 9 }, loaded.Bag.Items);
            Assert.AreNotSame(keys, loaded.Keys, "members are replaced, the object itself stays");
        }

        [Test]
        public void Register_SaveMarkedMembers_OnlyThem()
        {
            var picked = new Picked { Level = 3, Other = 8 };
            SaveLoadService writer = CreateService();
            writer.Register("picked", picked);
            writer.Save();

            var loaded = new Picked();
            SaveLoadService reader = CreateService();
            reader.Register("picked", loaded);
            reader.Load();

            Assert.AreEqual(3, loaded.Level);
            Assert.AreEqual(0, loaded.Other);
        }

        [Test]
        public void Register_ScriptableObject_UnityFieldRules()
        {
            var asset = ScriptableObject.CreateInstance<MembersAsset>();
            var loaded = ScriptableObject.CreateInstance<MembersAsset>();
            try
            {
                asset.Coins = 12;
                asset.Title = "Gold";
                asset.Hidden = 4;
                SaveLoadService writer = CreateService();
                writer.Register("asset", asset);
                writer.Save();

                SaveLoadService reader = CreateService();
                reader.Register("asset", loaded);
                reader.Load();

                Assert.AreEqual(12, loaded.Coins);
                Assert.AreEqual("Gold", loaded.Title);
                Assert.AreEqual(0, loaded.Hidden, "a private field Unity does not serialize is not saved");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(loaded);
            }
        }

        [Test]
        public void Register_ReturnsSerializer_UnregisterByKey()
        {
            var stats = new Stats { Coins = 1 };
            SaveLoadService service = CreateService();
            ObjectSerializer serializer = service.Register("stats", stats);

            Assert.AreEqual("stats", serializer.Key);
            Assert.AreSame(stats, serializer.Target);
            service.Unregister("stats");
            Assert.DoesNotThrow(() => service.Register("stats", stats));
        }

        [Test]
        public void Register_BadArguments_Throw()
        {
            SaveLoadService service = CreateService();

            Assert.Throws<ArgumentException>(() => service.Register("$stats", new Stats()));
            Assert.Throws<ArgumentNullException>(() => service.Register("stats", (object)null));
        }
    }
}

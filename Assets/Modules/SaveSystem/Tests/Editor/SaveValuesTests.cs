using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>Values of a slot: Values.Set / Get under a key, saved in the "$vars" section.</summary>
    public class SaveValuesTests
    {
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private SaveLoadService CreateService() => new(_storage, "1.0");

        [Test]
        public void Values_SetThenLoad_ReturnsSavedValues()
        {
            SaveLoadService writer = CreateService();
            writer.Values.Set("coins", 120);
            writer.Values.Set("name", "Knight");
            writer.Values.Set("spawn", new Vector3(1f, 2f, 3f));
            writer.Values.Set("keys", new List<string> { "red", "blue" });
            writer.Save();

            SaveLoadService reader = CreateService();
            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

            Assert.AreEqual(120, reader.Values.Get("coins", 0));
            Assert.AreEqual("Knight", reader.Values.Get<string>("name"));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), reader.Values.Get<Vector3>("spawn"));
            CollectionAssert.AreEqual(new[] { "red", "blue" }, reader.Values.Get<List<string>>("keys"));
        }

        [Test]
        public void Values_Get_MissingOrWrongType_ReturnsDefault()
        {
            SaveLoadService service = CreateService();
            service.Values.Set("name", "Knight");

            Assert.AreEqual(7, service.Values.Get("missing", 7));
            Assert.AreEqual(-1, service.Values.Get("name", -1));
            Assert.IsFalse(service.Values.TryGet("name", out int _));
        }

        [Test]
        public void Values_HasDeleteAndKeys()
        {
            SaveLoadService service = CreateService();
            service.Values.Set("a", 1);
            service.Values.Set("b", 2);

            Assert.IsTrue(service.Values.Has("a"));
            Assert.IsTrue(service.Values.Delete("a"));
            Assert.IsFalse(service.Values.Has("a"));
            Assert.IsFalse(service.Values.Delete("a"));
            CollectionAssert.AreEquivalent(new[] { "b" }, service.Values.Keys);
            Assert.AreEqual(1, service.Values.Count);
        }

        [Test]
        public void Values_StoredValueIsACopy()
        {
            SaveLoadService service = CreateService();
            var keys = new List<string> { "red" };
            service.Values.Set("keys", keys);

            keys.Add("blue");

            CollectionAssert.AreEqual(new[] { "red" }, service.Values.Get<List<string>>("keys"));
        }

        [Test]
        public void Values_EmptyKey_Throws()
        {
            SaveLoadService service = CreateService();

            Assert.Throws<ArgumentException>(() => service.Values.Set("", 1));
            Assert.Throws<ArgumentException>(() => service.Values.Set(null, 1));
            Assert.Throws<ArgumentException>(() => service.Values.Set("$type", 1));
        }

        [Test]
        public void Values_SelectSlot_StartsEmpty()
        {
            SaveLoadService service = CreateService();
            service.Values.Set("coins", 5);

            service.SelectSlot(1);

            Assert.AreEqual(0, service.Values.Count);
        }

        [Test]
        public void Values_LoadFileWithoutValues_ClearsThem()
        {
            CreateService().Save();
            SaveLoadService service = CreateService();
            service.Values.Set("coins", 5);

            service.Load();

            Assert.IsFalse(service.Values.Has("coins"));
        }

        [Test]
        public void Values_EmptySlot_SaveIfChangedWritesNothing()
        {
            SaveLoadService service = CreateService();

            Assert.AreEqual(SaveOutcome.Unchanged, service.Save(onlyIfChanged: true));
        }

        [Test]
        public void Values_Changed_SaveIfChangedWrites()
        {
            SaveLoadService service = CreateService();
            service.Values.Set("coins", 1);

            Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));
            Assert.AreEqual(SaveOutcome.Unchanged, service.Save(onlyIfChanged: true));
            service.Values.Set("coins", 2);
            Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));
        }

        [Test]
        public void Values_SectionFromNewerVersion_RefusesLoad()
        {
            _storage.Put(0, SaveFiles.SAVE,
                "{ \"formatVersion\": 1, \"gameVersion\": \"9\", \"sections\": { \"$vars\": { \"v\": 99, \"data\": {} } } }");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not loaded"));

            Assert.AreEqual(LoadOutcome.NewerVersion, CreateService().Load());
        }
    }
}

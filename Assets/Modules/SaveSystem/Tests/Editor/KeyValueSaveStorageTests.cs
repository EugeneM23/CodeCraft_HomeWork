using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>A key-value store as a database stands in for it: a dictionary behind the three methods.</summary>
    internal sealed class DictionaryStorage : KeyValueSaveStorage
    {
        public readonly Dictionary<string, string> Data = new();
        public int Flushes;

        public DictionaryStorage(string prefix = "saves/") : base(prefix)
        {
        }

        protected override bool TryGet(string key, out string text) => Data.TryGetValue(key, out text);
        protected override void Set(string key, string text) => Data[key] = text;
        protected override void Remove(string key) => Data.Remove(key);
        protected override void Flush() => Flushes++;
    }

    public class KeyValueSaveStorageTests : SaveStorageContractTests
    {
        private DictionaryStorage _store;

        protected override ISaveStorage CreateStorage() => _store = new DictionaryStorage();
        protected override void Plant(int slot, string file, string text) => _store.Data[_store.KeyOf(slot, file)] = text;
        protected override bool Exists(int slot, string file) => _store.Data.ContainsKey(_store.KeyOf(slot, file));

        [Test]
        public void KeyValueStorage_KeysUsePrefixAndSlot()
        {
            Storage.Write(2, SaveFiles.SAVE, "two");

            Assert.AreEqual("saves/slot_2/save.json", _store.KeyOf(2, SaveFiles.SAVE));
            Assert.AreEqual("two", _store.Data["saves/slot_2/save.json"]);
        }

        [Test]
        public void KeyValueStorage_DeleteSlot_RemovesEveryKeyOfIt()
        {
            Storage.Write(1, SaveFiles.SAVE, "a");
            Storage.Write(1, SaveFiles.SAVE, "b");
            Storage.Write(1, SaveFiles.META, "m");

            Storage.DeleteSlot(1);

            foreach (string key in _store.Data.Keys)
                StringAssert.DoesNotStartWith("saves/slot_1/", key);
        }

        [Test]
        public void KeyValueStorage_TwoPrefixes_DoNotMix()
        {
            var other = new DictionaryStorage("other/");
            foreach (KeyValuePair<string, string> pair in _store.Data)
                other.Data[pair.Key] = pair.Value;

            Storage.Write(1, SaveFiles.SAVE, "mine");

            CollectionAssert.AreEqual(new[] { 1 }, Storage.ListSlots());
            CollectionAssert.IsEmpty(other.ListSlots());
        }

        [Test]
        public void KeyValueStorage_EveryChangeIsFlushed()
        {
            Storage.Write(1, SaveFiles.SAVE, "a");
            int afterWrite = _store.Flushes;
            Storage.DeleteSlot(1);

            Assert.Greater(afterWrite, 0);
            Assert.Greater(_store.Flushes, afterWrite);
        }

        [Test]
        public void KeyValueStorage_UnderTheService_RoundTrips()
        {
            var service = new SaveLoadService(Storage, "1.0");
            service.Values.Set("coins", 3);
            service.Save();

            var reader = new SaveLoadService(Storage, "1.0");
            Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
            Assert.AreEqual(3, reader.Values.Get("coins", 0));
            Assert.AreEqual(1, reader.GetSlots().Count);
        }
    }

    public class PlayerPrefsSaveStorageTests : SaveStorageContractTests
    {
        private PlayerPrefsSaveStorage _prefs;

        protected override ISaveStorage CreateStorage() =>
            _prefs = new PlayerPrefsSaveStorage("SaveSystemTests_" + Guid.NewGuid().ToString("N") + "/");

        protected override void Plant(int slot, string file, string text) => PlayerPrefs.SetString(_prefs.KeyOf(slot, file), text);
        protected override bool Exists(int slot, string file) => PlayerPrefs.HasKey(_prefs.KeyOf(slot, file));

        [TearDown]
        public void DeleteKeys()
        {
            _prefs.DeleteAll();
            for (int slot = 0; slot < 8; slot++)
            {
                foreach (string file in new[] { SaveFiles.SAVE, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX })
                    PlayerPrefs.DeleteKey(_prefs.KeyOf(slot, file));
            }

            PlayerPrefs.Save();
        }

        [Test]
        public void PlayerPrefsStorage_DeleteAll_LeavesNoKeys()
        {
            Storage.Write(0, SaveFiles.SAVE, "a");
            Storage.Write(0, SaveFiles.SAVE, "b");
            Storage.Write(5, SaveFiles.META, "m");

            _prefs.DeleteAll();

            Assert.IsFalse(PlayerPrefs.HasKey(_prefs.KeyOf(0, SaveFiles.SAVE)));
            Assert.IsFalse(PlayerPrefs.HasKey(_prefs.KeyOf(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX)));
            Assert.IsFalse(PlayerPrefs.HasKey(_prefs.KeyOf(5, SaveFiles.META)));
            CollectionAssert.IsEmpty(Storage.ListSlots());
        }
    }
}

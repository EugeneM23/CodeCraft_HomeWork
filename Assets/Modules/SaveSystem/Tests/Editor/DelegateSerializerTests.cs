using System;
using NUnit.Framework;

namespace Saving.Tests
{
    /// <summary>Register(key, getter, setter): a value of plain code saved without a serializer class.</summary>
    public class DelegateSerializerTests
    {
        private InMemorySaveStorage _storage;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        private SaveLoadService CreateService() => new(_storage, "1.0");

        [Test]
        public void Register_GetterAndSetter_RoundTrip()
        {
            float volume = 0.25f;
            SaveLoadService writer = CreateService();
            writer.Register("volume", () => volume, value => volume = value);
            writer.Save();

            volume = 1f;
            SaveLoadService reader = CreateService();
            reader.Register("volume", () => volume, value => volume = value);
            reader.Load();

            Assert.AreEqual(0.25f, volume);
        }

        [Test]
        public void Register_AfterLoad_GetsItsDataAtOnce()
        {
            int coins = 9;
            SaveLoadService writer = CreateService();
            writer.Register("coins", () => coins, value => coins = value);
            writer.Save();

            coins = 0;
            SaveLoadService reader = CreateService();
            reader.Load();
            reader.Register("coins", () => coins, value => coins = value);

            Assert.AreEqual(9, coins);
        }

        [Test]
        public void Unregister_ByKey_StopsSaving()
        {
            int coins = 1;
            SaveLoadService service = CreateService();
            service.Register("coins", () => coins, value => coins = value);
            service.Save();

            service.Unregister("coins");
            coins = 2;
            service.Save();

            SaveLoadService reader = CreateService();
            int loaded = 0;
            reader.Register("coins", () => loaded, value => loaded = value);
            reader.Load();
            Assert.AreEqual(1, loaded, "the last captured section stays in the slot");
        }

        [Test]
        public void Register_ReservedOrEmptyKey_Throws()
        {
            SaveLoadService service = CreateService();

            Assert.Throws<ArgumentException>(() => service.Register("$mine", () => 1, _ => { }));
            Assert.Throws<ArgumentException>(() => service.Register("", () => 1, _ => { }));
            Assert.Throws<ArgumentNullException>(() => service.Register<int>("x", null, _ => { }));
        }
    }
}

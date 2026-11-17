using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Saving.Tests
{
    /// <summary>The component: autosave on game time, saves on pause, focus loss and quit, startup load.</summary>
    public class SaveLoadSystemTests
    {
        private sealed class Counter
        {
            public int Value;
        }

        private sealed class CounterData
        {
            public int Value;
        }

        private sealed class CounterSerializer : GameSerializer<Counter, CounterData>
        {
            public CounterSerializer(Counter counter) : base(counter, "counter")
            {
            }

            protected override CounterData Serialize(Counter counter) => new() { Value = counter.Value };
            protected override void Deserialize(Counter counter, CounterData data) => counter.Value = data.Value;
        }

        private InMemorySaveStorage _storage;
        private GameObject _object;

        [SetUp]
        public void SetUp() => _storage = new InMemorySaveStorage();

        [TearDown]
        public void TearDown()
        {
            if (_object != null)
                Object.DestroyImmediate(_object);
        }

        private SaveLoadSystem Create(int startupSlot = 0, bool loadOnStart = false, bool saveOnPause = true,
            float autoSavePeriod = 10f)
        {
            _object = new GameObject("SaveLoadSystem");
            var system = _object.AddComponent<SaveLoadSystem>();
            system.UseStorage(_storage);
            system.Configure(startupSlot, loadOnStart, saveOnQuit: true, saveOnPause, autoSavePeriod);
            return system;
        }

        /// <summary>A system with something to save, so onlyIfChanged has a reason to write.</summary>
        private SaveLoadSystem CreateWithData(bool saveOnPause = true, float autoSavePeriod = 10f)
        {
            SaveLoadSystem system = Create(saveOnPause: saveOnPause, autoSavePeriod: autoSavePeriod);
            system.Register(new CounterSerializer(new Counter { Value = 1 }));
            return system;
        }

        private bool Saved() => _storage.Has(0, SaveFiles.SAVE);

        [Test]
        public void SaveLoadSystem_TickBeforePeriod_DoesNotSave()
        {
            CreateWithData().Tick(9f);

            Assert.IsFalse(Saved());
        }

        [Test]
        public void SaveLoadSystem_TickPastPeriod_Saves()
        {
            SaveLoadSystem system = CreateWithData();

            system.Tick(9f);
            system.Tick(1.5f);

            Assert.IsTrue(Saved());
        }

        [Test]
        public void SaveLoadSystem_ZeroDelta_TimerAndPlayTimeWait()
        {
            SaveLoadSystem system = CreateWithData();

            for (int i = 0; i < 100; i++)
                system.Tick(0f);

            Assert.IsFalse(Saved());
            Assert.AreEqual(0, system.PlayTime);
        }

        [Test]
        public void SaveLoadSystem_Tick_AddsPlayTime()
        {
            SaveLoadSystem system = CreateWithData();

            system.Tick(2.5f);

            Assert.AreEqual(2.5, system.PlayTime, 1e-6);
        }

        [Test]
        public void SaveLoadSystem_AutoSaveOff_NeverSaves()
        {
            CreateWithData(autoSavePeriod: 0f).Tick(1000f);

            Assert.IsFalse(Saved());
        }

        [Test]
        public void SaveLoadSystem_ShortPeriod_ClampedToMinimum()
        {
            SaveLoadSystem system = CreateWithData(autoSavePeriod: 1f);

            system.Tick(SaveLoadSystem.MIN_AUTO_SAVE_PERIOD - 0.5f);

            Assert.IsFalse(Saved());
        }

        [Test]
        public void SaveLoadSystem_ApplicationPause_Saves()
        {
            CreateWithData();

            _object.SendMessage("OnApplicationPause", true);

            Assert.IsTrue(Saved());
        }

        [Test]
        public void SaveLoadSystem_SaveOnPauseOff_PauseDoesNotSave()
        {
            CreateWithData(saveOnPause: false);

            _object.SendMessage("OnApplicationPause", true);

            Assert.IsFalse(Saved());
        }

        [Test]
        public void SaveLoadSystem_FocusLost_Saves()
        {
            CreateWithData();

            _object.SendMessage("OnApplicationFocus", false);

            Assert.IsTrue(Saved());
        }

        [Test]
        public void SaveLoadSystem_Quit_Saves()
        {
            CreateWithData();

            _object.SendMessage("OnApplicationQuit");

            Assert.IsTrue(Saved());
        }

        [UnityTest]
        public IEnumerator SaveLoadSystem_LoadOnStart_LoadsStartupSlot()
        {
            var writer = new SaveLoadService(_storage, "test");
            writer.SelectSlot(2);
            writer.Register(new CounterSerializer(new Counter { Value = 7 }));
            writer.Save();

            SaveLoadSystem system = Create(startupSlot: 2, loadOnStart: true);
            var counter = new Counter();
            system.Register(new CounterSerializer(counter));

            yield return null; // Start

            Assert.AreEqual(2, system.CurrentSlot);
            Assert.AreEqual(7, counter.Value);
        }

        [Test]
        public void SaveLoadSystem_UseStorageAfterStart_Throws()
        {
            SaveLoadSystem system = Create();
            _ = system.CurrentSlot; // starts the service

            Assert.Throws<InvalidOperationException>(() => system.UseStorage(new InMemorySaveStorage()));
        }

        [Test]
        public void SaveLoadSystem_Settings_LayerCapturesOnSave()
        {
            var settings = ScriptableObject.CreateInstance<FakeExtensionAsset>();
            SaveLoadSystem system = Create();
            system.UseSettings(settings);

            system.Save();

            Assert.AreEqual(1, settings.Created.Captures);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void SaveLoadSystem_Destroyed_DisposesLayer()
        {
            var settings = ScriptableObject.CreateInstance<FakeExtensionAsset>();
            SaveLoadSystem system = Create();
            system.UseSettings(settings);
            system.Save();

            Object.DestroyImmediate(_object);

            Assert.IsTrue(settings.Created.Disposed);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void SaveLoadSystem_UseSettingsAfterStart_Throws()
        {
            SaveLoadSystem system = Create();
            _ = system.CurrentSlot; // starts the service

            Assert.Throws<InvalidOperationException>(() => system.UseSettings(null));
        }
    }
}

using System.Text.RegularExpressions;
using NUnit.Framework;
using Saving;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class SettingsStoreTests
    {
        private int _vSyncCount;
        private int _targetFrameRate;

        // Save applies the graphics: keep the editor's own values
        [SetUp]
        public void SetUp()
        {
            _vSyncCount = QualitySettings.vSyncCount;
            _targetFrameRate = Application.targetFrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            QualitySettings.vSyncCount = _vSyncCount;
            Application.targetFrameRate = _targetFrameRate;
        }

        private static GameSettings Edited()
        {
            var settings = new GameSettings();
            settings.audio.music = 0.25f;
            settings.audio.ambient = 0f;
            settings.graphics.width = 1920;
            settings.graphics.height = 1080;
            settings.graphics.refreshRateHz = 59.94f;
            settings.graphics.windowMode = FullScreenMode.ExclusiveFullScreen;
            settings.graphics.vSync = false;
            settings.graphics.frameRateLimit = 120;
            settings.controls.mouseSensitivity = 2.5f;
            settings.controls.bindingOverrides = "{\"bindings\":[]}";
            return settings;
        }

        [Test]
        public void Load_WithoutAFile_GivesTheDefaults()
        {
            var store = new SettingsStore(new InMemorySaveStorage(), "1.0");

            GameSettings loaded = store.Load();

            Assert.AreEqual(LoadOutcome.Empty, store.LastLoadOutcome);
            Assert.AreEqual(-1, loaded.graphics.qualityLevel);
            Assert.AreEqual(0, loaded.graphics.width);
            Assert.AreEqual(1f, loaded.audio.master);
            Assert.AreEqual(1f, store.Current.controls.mouseSensitivity);
        }

        [Test]
        public void Save_ThenLoadInANewStore_GivesTheSameSettings()
        {
            var storage = new InMemorySaveStorage();
            new SettingsStore(storage, "1.0").Save(Edited());

            var store = new SettingsStore(storage, "1.0");
            GameSettings loaded = store.Load();

            Assert.AreEqual(LoadOutcome.Loaded, store.LastLoadOutcome);
            Assert.AreEqual(0.25f, loaded.audio.music);
            Assert.AreEqual(0f, loaded.audio.ambient);
            Assert.AreEqual(1920, loaded.graphics.width);
            Assert.AreEqual(1080, loaded.graphics.height);
            Assert.AreEqual(59.94f, loaded.graphics.refreshRateHz, 0.001f);
            Assert.AreEqual(FullScreenMode.ExclusiveFullScreen, loaded.graphics.windowMode);
            Assert.IsFalse(loaded.graphics.vSync);
            Assert.AreEqual(120, loaded.graphics.frameRateLimit);
            Assert.AreEqual(2.5f, loaded.controls.mouseSensitivity);
            Assert.AreEqual("{\"bindings\":[]}", loaded.controls.bindingOverrides);
        }

        [Test]
        public void Save_KeepsACopy_RaisesChanged_AndAppliesTheFrameRate()
        {
            var store = new SettingsStore(new InMemorySaveStorage(), "1.0");
            GameSettings changed = null;
            store.Changed += settings => changed = settings;
            GameSettings edited = Edited();

            store.Save(edited);
            edited.audio.music = 0.9f;

            Assert.AreSame(store.Current, changed);
            Assert.AreNotSame(edited, store.Current);
            Assert.AreEqual(0.25f, store.Current.audio.music);
            Assert.AreEqual(120, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
        }

        [Test]
        public void Load_ReturnsACopy_EditsDoNotTouchCurrent()
        {
            var store = new SettingsStore(new InMemorySaveStorage(), "1.0");

            GameSettings loaded = store.Load();
            loaded.audio.master = 0.1f;

            Assert.AreEqual(1f, store.Current.audio.master);
        }

        [Test]
        public void Load_AFileWithoutNewFields_KeepsTheirDefaults()
        {
            var storage = new InMemorySaveStorage();
            storage.Put(0, SaveFiles.SAVE, "{\"formatVersion\":1,\"gameVersion\":\"0.1\",\"sections\":{\"settings\":" +
                                           "{\"v\":1,\"data\":{\"audio\":{\"music\":0.5}}}}}");
            var store = new SettingsStore(storage, "1.0");

            GameSettings loaded = store.Load();

            Assert.AreEqual(LoadOutcome.Loaded, store.LastLoadOutcome);
            Assert.AreEqual(0.5f, loaded.audio.music);
            Assert.AreEqual(1f, loaded.audio.effects);
            Assert.IsNotNull(loaded.graphics);
            Assert.AreEqual(-1, loaded.graphics.qualityLevel);
            Assert.AreEqual(string.Empty, loaded.controls.bindingOverrides);
        }

        [Test]
        public void Load_ASectionOfANewerGame_GivesDefaults_AndNeverOverwritesIt()
        {
            var storage = new InMemorySaveStorage();
            string newer = "{\"formatVersion\":1,\"gameVersion\":\"9.0\",\"sections\":{\"settings\":" +
                           "{\"v\":" + (SettingsSerializer.VERSION + 1) + ",\"data\":{\"audio\":{\"music\":0.5}}}}}";
            storage.Put(0, SaveFiles.SAVE, newer);
            var store = new SettingsStore(storage, "1.0");
            LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));

            GameSettings loaded = store.Load();
            store.Save(Edited());

            Assert.AreEqual(LoadOutcome.NewerVersion, store.LastLoadOutcome);
            Assert.AreEqual(1f, loaded.audio.music);
            Assert.IsTrue(store.IsReadOnly);
            Assert.IsTrue(storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.AreEqual(newer, text);
        }

        [Test]
        public void AudioChannels_GetAndSet_ByName_Clamped()
        {
            var audio = new AudioSettingsData();

            AudioChannels.Set(audio, AudioChannels.EFFECTS, 1.5f);
            AudioChannels.Set(audio, AudioChannels.UI, 0.3f);

            Assert.AreEqual(1f, audio.effects);
            Assert.AreEqual(0.3f, AudioChannels.Get(audio, AudioChannels.UI));
            Assert.Throws<System.ArgumentException>(() => AudioChannels.Get(audio, "voice"));
        }
    }
}

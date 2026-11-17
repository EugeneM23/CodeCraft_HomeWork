using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameSettingsTests
    {
        private static GameSettings Changed()
        {
            GameSettings settings = new();
            settings.graphics.width = 2560;
            settings.graphics.height = 1440;
            settings.graphics.refreshRateHz = 143.9f;
            settings.graphics.windowMode = FullScreenMode.Windowed;
            settings.graphics.qualityLevel = 1;
            settings.graphics.vSync = false;
            settings.graphics.frameRateLimit = 120;
            settings.audio.master = 0.5f;
            settings.audio.music = 0.25f;
            settings.audio.effects = 0.75f;
            settings.audio.ui = 0.1f;
            settings.audio.ambient = 0f;
            settings.controls.mouseSensitivity = 2.5f;
            settings.controls.bindingOverrides = "{\"bindings\":[]}";
            return settings;
        }

        [Test]
        public void JsonRoundTrip_KeepsEveryField()
        {
            GameSettings original = Changed();

            GameSettings loaded = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(original));

            Assert.That(JsonUtility.ToJson(loaded), Is.EqualTo(JsonUtility.ToJson(original)));
            Assert.That(loaded.graphics.windowMode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(loaded.graphics.refreshRateHz, Is.EqualTo(143.9f));
            Assert.That(loaded.audio.music, Is.EqualTo(0.25f));
            Assert.That(loaded.controls.bindingOverrides, Is.EqualTo("{\"bindings\":[]}"));
        }

        [Test]
        public void Clone_ChangingTheCopy_LeavesTheOriginal()
        {
            GameSettings original = Changed();

            GameSettings copy = original.Clone();
            copy.graphics.width = 800;
            copy.audio.master = 1f;
            copy.controls.mouseSensitivity = 0.2f;
            copy.controls.bindingOverrides = "";

            Assert.That(copy.graphics, Is.Not.SameAs(original.graphics));
            Assert.That(original.graphics.width, Is.EqualTo(2560));
            Assert.That(original.audio.master, Is.EqualTo(0.5f));
            Assert.That(original.controls.mouseSensitivity, Is.EqualTo(2.5f));
            Assert.That(original.controls.bindingOverrides, Is.EqualTo("{\"bindings\":[]}"));
        }

        [Test]
        public void Defaults_KeepTheCurrentResolutionAndTheProjectQuality()
        {
            GameSettings settings = new();

            Assert.That(settings.graphics.width, Is.Zero);
            Assert.That(settings.graphics.height, Is.Zero);
            Assert.That(settings.graphics.qualityLevel, Is.EqualTo(-1));
            Assert.That(settings.controls.bindingOverrides, Is.Empty);
        }
    }
}

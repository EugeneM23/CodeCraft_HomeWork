using NUnit.Framework;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class AudioSettingsBridgeTests
    {
        [Test]
        public void EveryChannel_MapsToItsSettingsKey()
        {
            Assert.AreEqual(AudioChannels.MASTER, AudioSettingsBridge.KeyOf(AudioChannel.Master));
            Assert.AreEqual(AudioChannels.MUSIC, AudioSettingsBridge.KeyOf(AudioChannel.Music));
            Assert.AreEqual(AudioChannels.EFFECTS, AudioSettingsBridge.KeyOf(AudioChannel.Effects));
            Assert.AreEqual(AudioChannels.UI, AudioSettingsBridge.KeyOf(AudioChannel.UI));
            Assert.AreEqual(AudioChannels.AMBIENT, AudioSettingsBridge.KeyOf(AudioChannel.Ambient));
        }

        [Test]
        public void SettingsKeys_AreAllCovered()
        {
            foreach (string key in AudioChannels.All)
            {
                bool found = false;
                foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
                    found |= AudioSettingsBridge.KeyOf(channel) == key;
                Assert.IsTrue(found, $"settings key '{key}' has no AudioChannel");
            }
        }
    }
}

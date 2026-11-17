using System;
using Game.AudioEngine.Adapters.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Audio;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class AudioMixerTests
    {
        private const string MIXER = "Assets/Game/Audio/AudioMixer.mixer";

        private static AudioMixer LoadMixer()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER);
            Assert.IsNotNull(mixer, MIXER);
            return mixer;
        }

        [TestCase(AudioChannel.Master)]
        [TestCase(AudioChannel.Music)]
        [TestCase(AudioChannel.Effects)]
        [TestCase(AudioChannel.UI)]
        [TestCase(AudioChannel.Ambient)]
        public void EveryChannel_HasItsGroupUnderMaster(AudioChannel channel)
        {
            AudioMixerGroup[] groups = LoadMixer().FindMatchingGroups("Master");
            Assert.IsNotNull(Array.Find(groups, g => g.name == channel.ToString()), $"group {channel}");
        }

        [TestCase(AudioChannel.Master)]
        [TestCase(AudioChannel.Music)]
        [TestCase(AudioChannel.Effects)]
        [TestCase(AudioChannel.UI)]
        [TestCase(AudioChannel.Ambient)]
        public void EveryChannel_HasItsExposedVolume(AudioChannel channel)
        {
            string parameter = AudioVolumeUseCase.ParameterOf(channel);
            Assert.IsTrue(LoadMixer().GetFloat(parameter, out _), parameter);
        }

        [Test]
        public void TheOldSoundGroup_IsRenamedToEffects()
        {
            AudioMixer mixer = LoadMixer();
            Assert.IsNull(Array.Find(mixer.FindMatchingGroups("Master"), g => g.name == "Sound"));
            Assert.IsFalse(mixer.GetFloat("SoundVolume", out _));
        }

        [Test]
        public void MixerGroupLookup_FindsEveryChannel()
        {
            AudioMixer mixer = LoadMixer();
            foreach (AudioChannel channel in (AudioChannel[])Enum.GetValues(typeof(AudioChannel)))
                Assert.IsNotNull(AudioMixerGroups.Find(mixer, channel), channel.ToString());
        }
    }
}

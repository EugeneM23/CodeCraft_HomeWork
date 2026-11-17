using Game.AudioEngine.Adapters.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class AudioImportRulesTests
    {
        [Test]
        public void ShortKeyClip_DecompressesOnLoad_Preloads_AndIsMono()
        {
            AudioImportRule rule = AudioImportRules.RuleFor("Assets/Game/Audio/Clips/tank/hit/Hit_01.wav", 0.8f);
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, rule.LoadType);
            Assert.IsTrue(rule.Preload);
            Assert.IsTrue(rule.LoadInBackground);
            Assert.IsTrue(rule.ForceToMono);
        }

        [Test]
        public void ShortClipOutsideClips_StaysStereo()
        {
            AudioImportRule rule = AudioImportRules.RuleFor("Assets/Game/Audio/Zombie/ZombieAttack1.wav", 1f);
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, rule.LoadType);
            Assert.IsTrue(rule.Preload);
            Assert.IsFalse(rule.ForceToMono);
        }

        [Test]
        public void MediumClip_IsCompressedInMemory_AndPreloads()
        {
            AudioImportRule rule = AudioImportRules.RuleFor("Assets/Game/Audio/Clips/world/wind_loop/Wind.wav", 12f);
            Assert.AreEqual(AudioClipLoadType.CompressedInMemory, rule.LoadType);
            Assert.IsTrue(rule.Preload);
            Assert.IsFalse(rule.ForceToMono);
        }

        [Test]
        public void LongClip_Streams_WithoutPreload()
        {
            AudioImportRule rule = AudioImportRules.RuleFor("Assets/Game/Audio/Clips/world/rain_loop/Rain.wav", 45f);
            Assert.AreEqual(AudioClipLoadType.Streaming, rule.LoadType);
            Assert.IsFalse(rule.Preload);
        }

        [TestCase("Assets/Game/Audio/Music/Music.wav")]
        [TestCase("Assets/Game/Audio/Clips/music/menu/Menu.ogg")]
        [TestCase("Assets/Game/Audio/Clips/music/battle/Battle.ogg")]
        public void Music_Streams_EvenWhenShort(string path)
        {
            AudioImportRule rule = AudioImportRules.RuleFor(path, 3f);
            Assert.AreEqual(AudioClipLoadType.Streaming, rule.LoadType);
            Assert.IsFalse(rule.Preload);
            Assert.IsFalse(rule.ForceToMono);
        }

        [TestCase("Assets/Game/Audio/Clips/tank/hit/Hit.wav", true)]
        [TestCase("Assets/Game/Audio/Clips/tank/hit/Hit.OGG", true)]
        [TestCase("Assets/Game/Audio/Clips/tank/hit/README.txt", false)]
        [TestCase("Assets/Other/Sound.wav", false)]
        public void OnlyAudioFilesUnderGameAudio_AreRuled(string path, bool ruled) =>
            Assert.AreEqual(ruled, AudioImportRules.IsRuledPath(path));
    }
}

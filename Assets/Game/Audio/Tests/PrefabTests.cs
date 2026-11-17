using AudioEngine;
using Game.AudioEngine.Adapters.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.AudioEngine.Adapters.Tests
{
    public sealed class PrefabTests
    {
        [Test]
        public void GameAudio_LoadsFromResources_WithItsAudioSystemAndPreloadList()
        {
            GameAudio prefab = Resources.Load<GameAudio>("GameAudio");
            Assert.IsNotNull(prefab, "Resources/GameAudio");
            Assert.IsNotNull(prefab.GetComponent<AudioSystem>());
            Assert.IsNotEmpty(prefab.PreloadClips, "run Tools/Audio/Sync Banks to fill the preload list");
        }

        [Test]
        public void AppAudio_LoadsFromResources_WithItsPlayers()
        {
            AppAudio prefab = Resources.Load<AppAudio>("AppAudio");
            Assert.IsNotNull(prefab, "Resources/AppAudio");
            Assert.IsNotNull(prefab.GetComponentInChildren<UISoundPlayer>(true));
            Assert.IsNotNull(prefab.GetComponentInChildren<MusicPlayer>(true));
        }

        [TestCase("Assets/Game/Audio/Resources/GameAudio.prefab")]
        [TestCase("Assets/Game/Audio/Resources/AppAudio.prefab")]
        public void Prefab_HasNoMissingReferences(string path) =>
            CollectionAssert.IsEmpty(AudioPrefabCheck.FindProblems(path));
    }
}

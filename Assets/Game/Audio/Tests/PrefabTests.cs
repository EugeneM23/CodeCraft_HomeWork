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
        public void AppAudio_Prefab_HasItsPlayers()
        {
            AppAudio prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<AppAudio>(AudioAssetPaths.APP_AUDIO_PREFAB);
            Assert.IsNotNull(prefab, AudioAssetPaths.APP_AUDIO_PREFAB);
            Assert.IsNotNull(prefab.GetComponentInChildren<UISoundPlayer>(true));
            Assert.IsNotNull(prefab.GetComponentInChildren<MusicPlayer>(true));
        }

        [Test]
        public void AppAudio_IsNotInResources_SoNothingBringsItUpWithoutTheApp()
        {
            Assert.IsNull(Resources.Load<AppAudio>("AppAudio"));
        }

        [Test]
        public void AppPrefab_InstallsAppAudio_AfterItsOtherInstallers()
        {
            GameObject app = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(AppPrefabAudioBuilder.APP_PREFAB_PATH);
            Assert.IsNotNull(app, AppPrefabAudioBuilder.APP_PREFAB_PATH);

            AppAudioInstaller installer = app.GetComponentInChildren<AppAudioInstaller>(true);
            Assert.IsNotNull(installer, "run Tools/Audio/Add Audio Installer To App");

            var so = new UnityEditor.SerializedObject(installer);
            Assert.IsNotNull(so.FindProperty("_prefab").objectReferenceValue, "AppAudioInstaller has no AppAudio prefab");

            AppContext context = app.GetComponentInChildren<AppContext>(true);
            var list = new UnityEditor.SerializedObject(context).FindProperty("sceneInstallers");
            bool listed = false;
            for (int i = 0; i < list.arraySize; i++)
                listed |= list.GetArrayElementAtIndex(i).objectReferenceValue == installer;
            Assert.IsTrue(listed, "AppAudioInstaller is not in the AppContext's scene installers");
        }

        [TestCase("Assets/Game/Audio/Resources/GameAudio.prefab")]
        [TestCase("Assets/Game/Audio/Prefabs/AppAudio.prefab")]
        public void Prefab_HasNoMissingReferences(string path) =>
            CollectionAssert.IsEmpty(AudioPrefabCheck.FindProblems(path));
    }
}

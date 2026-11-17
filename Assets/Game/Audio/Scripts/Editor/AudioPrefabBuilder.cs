using AudioEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Tools/Audio/Build Prefabs: Resources/GameAudio (AudioSystem with the six banks + GameAudio) and
    /// Prefabs/AppAudio (AppAudio with its UI sound player, music player and stinger source). Idempotent: an
    /// existing prefab is opened, completed and rewired, never recreated, so its file ids stay put.
    /// </summary>
    public static class AudioPrefabBuilder
    {
        private const string MENU = "Tools/Audio/Build Prefabs";
        private const string GAME_AUDIO = "GameAudio";
        private const string APP_AUDIO = "AppAudio";
        private const string UI_PLAYER_CHILD = "UISoundPlayer";
        private const string MUSIC_PLAYER_CHILD = "MusicPlayer";
        private const string STINGER_CHILD = "Stinger";

        [MenuItem(MENU, priority = 3)]
        public static void BuildFromMenu() => Build();

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Audio: Build Prefabs skipped in Play mode");
                return;
            }

            AudioClipFolders.EnsureOnDisk(AudioAssetPaths.RESOURCES, null);
            AssetDatabase.Refresh();

            BuildPrefab(AudioAssetPaths.GAME_AUDIO_PREFAB, GAME_AUDIO, FillGameAudio);
            BuildPrefab(AudioAssetPaths.APP_AUDIO_PREFAB, APP_AUDIO, FillAppAudio);
            AssetDatabase.SaveAssets();
            Debug.Log($"Audio: prefabs built: {AudioAssetPaths.GAME_AUDIO_PREFAB}, {AudioAssetPaths.APP_AUDIO_PREFAB}");
        }

        private static void BuildPrefab(string path, string rootName, System.Action<GameObject> fill)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                // Build the first version in a preview scene so the open scene is never touched (a dirty scene
                // blocks the test runner of the shared editor).
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject empty = new GameObject(rootName);
                    SceneManager.MoveGameObjectToScene(empty, preview);
                    PrefabUtility.SaveAsPrefabAsset(empty, path);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                }
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = rootName;
                fill(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FillGameAudio(GameObject root)
        {
            AudioSystem system = Ensure<AudioSystem>(root);
            Ensure<GameAudio>(root);

            SerializedObject serialized = new SerializedObject(system);
            serialized.FindProperty("initOnAwake").boolValue = true;
            SerializedProperty banks = serialized.FindProperty("initialBanks");
            banks.arraySize = AudioLayout.Banks.Length;
            for (int i = 0; i < AudioLayout.Banks.Length; i++)
                banks.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioBank>(AudioAssetPaths.BankAsset(AudioLayout.Banks[i]));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // _preloadClips is filled by Tools/Audio/Sync Banks.
        }

        private static void FillAppAudio(GameObject root)
        {
            AudioMixer mixer = AudioMixerGroups.LoadMixer();
            AudioMixerGroup uiGroup = AudioMixerGroups.Find(mixer, AudioChannel.UI);
            AudioMixerGroup musicGroup = AudioMixerGroups.Find(mixer, AudioChannel.Music);
            UISoundCatalog uiCatalog = AssetDatabase.LoadAssetAtPath<UISoundCatalog>(AudioAssetPaths.UI_CATALOG);

            AppAudio appAudio = Ensure<AppAudio>(root);

            GameObject uiChild = EnsureChild(root, UI_PLAYER_CHILD);
            AudioSource uiSource = Ensure<AudioSource>(uiChild);
            Configure(uiSource, uiGroup);
            UISoundPlayer uiPlayer = Ensure<UISoundPlayer>(uiChild);
            SerializedObject uiSerialized = new SerializedObject(uiPlayer);
            uiSerialized.FindProperty("audioSource").objectReferenceValue = uiSource;
            uiSerialized.FindProperty("catalog").objectReferenceValue = uiCatalog;
            uiSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject musicChild = EnsureChild(root, MUSIC_PLAYER_CHILD);
            AudioSource musicSource = Ensure<AudioSource>(musicChild);
            Configure(musicSource, musicGroup);
            MusicPlayer musicPlayer = Ensure<MusicPlayer>(musicChild);
            SerializedObject musicSerialized = new SerializedObject(musicPlayer);
            musicSerialized.FindProperty("audioSource").objectReferenceValue = musicSource;
            musicSerialized.ApplyModifiedPropertiesWithoutUndo();

            // AppAudio.Awake routes the stinger source to the Music group; the prefab says the same.
            GameObject stingerChild = EnsureChild(root, STINGER_CHILD);
            AudioSource stingerSource = Ensure<AudioSource>(stingerChild);
            Configure(stingerSource, musicGroup);

            SerializedObject serialized = new SerializedObject(appAudio);
            serialized.FindProperty("_mixer").objectReferenceValue = mixer;
            serialized.FindProperty("_uiSoundPlayer").objectReferenceValue = uiPlayer;
            serialized.FindProperty("_uiCatalog").objectReferenceValue = uiCatalog;
            serialized.FindProperty("_uiSource").objectReferenceValue = uiSource;
            serialized.FindProperty("_uiGroup").objectReferenceValue = uiGroup;
            serialized.FindProperty("_musicPlayer").objectReferenceValue = musicPlayer;
            serialized.FindProperty("_musicSource").objectReferenceValue = musicSource;
            serialized.FindProperty("_stingerSource").objectReferenceValue = stingerSource;
            serialized.FindProperty("_musicGroup").objectReferenceValue = musicGroup;
            serialized.FindProperty("_menuMusic").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<MusicCatalog>(AudioAssetPaths.MENU_MUSIC);
            serialized.FindProperty("_battleMusic").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<MusicCatalog>(AudioAssetPaths.BATTLE_MUSIC);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A 2D source that plays only when told to and keeps playing through AudioListener.pause.</summary>
        private static void Configure(AudioSource source, AudioMixerGroup output)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            // Not serialized by Unity: AppAudio.Awake sets it again at runtime; kept here for clarity.
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = output;
        }

        private static GameObject EnsureChild(GameObject root, string name)
        {
            Transform child = root.transform.Find(name);
            if (child != null)
                return child.gameObject;

            GameObject go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}

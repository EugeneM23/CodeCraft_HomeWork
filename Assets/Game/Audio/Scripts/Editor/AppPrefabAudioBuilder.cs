using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Puts the sound team's installer (AppAudioInstaller, with the AppAudio prefab) on the core's App prefab, last
    /// in the AppContext's scene installer list: AppAudio reads the settings store that SaveInstaller installs.
    /// Idempotent.
    /// </summary>
    public static class AppPrefabAudioBuilder
    {
        public const string APP_PREFAB_PATH = "Assets/Game/Prefabs/App/App.prefab";
        private const string INSTALLERS_FIELD = "sceneInstallers";
        private const string PREFAB_FIELD = "_prefab";
        private const string MENU = "Tools/Audio/Add Audio Installer To App";

        [MenuItem(MENU, priority = 40)]
        public static void Build()
        {
            AppAudio appAudio = AssetDatabase.LoadAssetAtPath<AppAudio>(AudioAssetPaths.APP_AUDIO_PREFAB);
            if (appAudio == null)
            {
                Debug.LogWarning($"AppPrefabAudioBuilder: no AppAudio prefab at {AudioAssetPaths.APP_AUDIO_PREFAB} (Tools/Audio/Build Prefabs)");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(APP_PREFAB_PATH);
            try
            {
                AppContext context = root.GetComponentInChildren<AppContext>(true);
                if (context == null)
                {
                    Debug.LogWarning($"AppPrefabAudioBuilder: no AppContext in {APP_PREFAB_PATH}");
                    return;
                }

                bool changed = false;
                AppAudioInstaller installer = context.GetComponent<AppAudioInstaller>();
                if (installer == null)
                {
                    installer = context.gameObject.AddComponent<AppAudioInstaller>();
                    changed = true;
                }

                SerializedObject installerSo = new(installer);
                SerializedProperty prefab = installerSo.FindProperty(PREFAB_FIELD);
                if (prefab.objectReferenceValue != appAudio)
                {
                    prefab.objectReferenceValue = appAudio;
                    installerSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                SerializedObject contextSo = new(context);
                SerializedProperty list = contextSo.FindProperty(INSTALLERS_FIELD);
                bool listed = false;
                for (int i = 0; i < list.arraySize; i++)
                    listed |= list.GetArrayElementAtIndex(i).objectReferenceValue == installer;

                if (!listed)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
                    contextSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (!changed)
                    return;

                PrefabUtility.SaveAsPrefabAsset(root, APP_PREFAB_PATH);
                Debug.Log($"AppAudioInstaller added to {APP_PREFAB_PATH}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

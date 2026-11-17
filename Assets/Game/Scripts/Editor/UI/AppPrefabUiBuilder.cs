using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Puts the menu team's installer (GameInputInstaller) on the core's App prefab, last in the AppContext's
    /// scene installer list: it reads the settings store that SaveInstaller installs. Idempotent.
    /// </summary>
    public static class AppPrefabUiBuilder
    {
        public const string APP_PREFAB_PATH = "Assets/Game/Prefabs/App/App.prefab";
        private const string INSTALLERS_FIELD = "sceneInstallers";

        [MenuItem("Tools/UI/Add UI Installers To App")]
        public static void Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(APP_PREFAB_PATH);
            try
            {
                AppContext context = root.GetComponentInChildren<AppContext>(true);
                if (context == null)
                {
                    Debug.LogWarning($"AppPrefabUiBuilder: no AppContext in {APP_PREFAB_PATH}");
                    return;
                }

                GameInputInstaller installer = context.GetComponent<GameInputInstaller>();
                if (installer == null)
                    installer = context.gameObject.AddComponent<GameInputInstaller>();

                SerializedObject so = new(context);
                SerializedProperty list = so.FindProperty(INSTALLERS_FIELD);
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer)
                        return;
                }

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, APP_PREFAB_PATH);
                Debug.Log($"GameInputInstaller added to {APP_PREFAB_PATH}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

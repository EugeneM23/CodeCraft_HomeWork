using Saving.Objects;
using UnityEditor;
using UnityEngine;

namespace Saving.Editor
{
    /// <summary>
    /// SaveSystemSettings in the Inspector: the auto-create flag, the asset folders, the catalogs (read only) and a
    /// rebuild button.
    /// </summary>
    [CustomEditor(typeof(SaveSystemSettings))]
    public sealed class SaveSystemSettingsEditor : UnityEditor.Editor
    {
        private bool _showThirdParty;

        public override void OnInspectorGUI()
        {
            var settings = (SaveSystemSettings)target;
            EditorGUILayout.HelpBox("The editor keeps these catalogs up to date: after prefabs and scenes are imported, " +
                                    "moved or deleted, and before a build.", MessageType.Info);

            serializedObject.Update();
            SerializedProperty createSystem = serializedObject.FindProperty("_createSystemWhenMissing");
            EditorGUILayout.PropertyField(createSystem);
            if (createSystem.boolValue && !SaveSystemSetup.IsRuntimeSettingsPath(AssetDatabase.GetAssetPath(settings)))
                EditorGUILayout.HelpBox("A scene without a SaveSystem gets none from this asset: in play it is found only as " +
                                        $"{SaveSystemSettings.RESOURCES_NAME} right in a Resources folder.", MessageType.Info);

            EditorGUILayout.Space();
            SerializedProperty folders = serializedObject.FindProperty("_assetFolders");
            EditorGUILayout.LabelField("Asset folders for references", EditorStyles.boldLabel);
            for (int index = 0; index < folders.arraySize; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty folder = folders.GetArrayElementAtIndex(index);
                    folder.stringValue = EditorGUILayout.TextField(folder.stringValue);
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        folders.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
            }

            if (GUILayout.Button("Add folder"))
            {
                folders.InsertArrayElementAtIndex(folders.arraySize);
                folders.GetArrayElementAtIndex(folders.arraySize - 1).stringValue = "Assets/";
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.LabelField($"Assets in the table: {settings.Assets.Count}");
            EditorGUILayout.Space();
            // Read only: the editor rebuilds the list with the catalogs
            _showThirdParty = EditorGUILayout.Foldout(_showThirdParty,
                $"Third Party Assemblies: {settings.ThirdPartyAssemblies.Count} (not saved as the game's scripts)", true);
            if (_showThirdParty)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (string assembly in settings.ThirdPartyAssemblies)
                        EditorGUILayout.LabelField(assembly);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Prefabs with Saveable: {settings.Prefabs.Count}", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                foreach (SaveSystemSettings.PrefabEntry entry in settings.Prefabs)
                    EditorGUILayout.ObjectField(entry.Id, entry.Prefab, typeof(GameObject), false);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Scenes: {settings.Scenes.Count}", EditorStyles.boldLabel);
            foreach (SaveSystemSettings.SceneEntry entry in settings.Scenes)
                EditorGUILayout.LabelField(entry.Path, entry.Guid);

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Catalogs"))
                SaveCatalogBuilder.Rebuild(settings);
        }
    }
}

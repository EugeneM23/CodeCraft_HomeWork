using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Saving.Installation
{
    /// <summary>Tools → Save System → Welcome: what is in place and the first steps. Opens by itself after installing.</summary>
    public sealed class SaveSystemWelcomeWindow : EditorWindow
    {
        private const string TMP_IMPORT_MENU = "Window/TextMeshPro/Import TMP Essential Resources";
        private const string ADD_SYSTEM_MENU = "GameObject/Save System/Save System";
        private const string ADD_MENU_MENU = "GameObject/Save System/Save Slots Menu";

        [MenuItem("Tools/Save System/Welcome", false, 0)]
        public static void Open()
        {
            var window = GetWindow<SaveSystemWelcomeWindow>(true, "Save System");
            window.minSize = new Vector2(420, 380);
            window.Show();
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Save System", EditorStyles.largeLabel);
            EditorGUILayout.LabelField("Add Saveable to an object or prefab and press Play: it saves itself. "
                + "The save system is created on its own; drop the SaveSystem prefab into a scene for your own settings "
                + "and buttons.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            DrawJson();
            DrawSettings();
            DrawTmp();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Start", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!SaveSystemInstaller.ModuleCompiled))
            {
                if (GUILayout.Button("Add Save System to the scene"))
                    EditorApplication.ExecuteMenuItem(ADD_SYSTEM_MENU);
                if (GUILayout.Button("Add Save Slots Menu to the scene"))
                    EditorApplication.ExecuteMenuItem(ADD_MENU_MENU);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open minimal demo"))
                        OpenScene("Demo/SaveSystemMinimal.unity");
                    if (GUILayout.Button("Open full demo"))
                        OpenScene("Demo/SaveSystemDemo.unity");
                }
            }

            if (GUILayout.Button("Open README"))
                OpenModuleAsset("README.md");
        }

        private static void DrawJson()
        {
            switch (SaveSystemInstaller.Json)
            {
                case SaveSystemInstaller.JsonState.Package:
                    Row(true, "Newtonsoft JSON package");
                    break;
                case SaveSystemInstaller.JsonState.Dll:
                    Row(true, $"Newtonsoft JSON from {SaveSystemInstaller.FindJsonDll()}");
                    break;
                default:
                    Row(false, SaveSystemInstaller.IsInstalling ? "Newtonsoft JSON: installing…" : "Newtonsoft JSON is missing");
                    break;
            }

            if (!SaveSystemInstaller.HasUgui)
                Row(false, SaveSystemInstaller.IsInstalling ? "uGUI: installing…" : "uGUI is missing (slot menu, demo)");

            string[] missing = SaveSystemInstaller.MissingPackages;
            if (missing.Length > 0 && !SaveSystemInstaller.IsInstalling && GUILayout.Button("Install " + string.Join(", ", missing)))
            {
                SessionState.EraseBool(SaveSystemInstaller.REQUESTED_KEY);
                SaveSystemInstaller.AddMissingPackages();
            }

            if (SaveSystemInstaller.Json != SaveSystemInstaller.JsonState.Missing && !SaveSystemInstaller.ModuleCompiled)
                Row(false, "The module is compiling…");
        }

        private static void DrawSettings()
        {
            string settings = AssetDatabase.FindAssets("t:SaveSystemSettings")
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (settings == null)
            {
                Row(false, "Settings: created with the first Saveable");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                Row(true, $"Settings: {settings}");
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(settings);
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
            }
        }

        private static void DrawTmp()
        {
            bool tmp = AssetDatabase.FindAssets("t:TMP_Settings").Length > 0;
            using (new EditorGUILayout.HorizontalScope())
            {
                Row(tmp, tmp ? "TextMesh Pro resources (slot menu)" : "TextMesh Pro resources: needed by the slot menu only");
                if (!tmp && GUILayout.Button("Import", GUILayout.Width(60)))
                    EditorApplication.ExecuteMenuItem(TMP_IMPORT_MENU);
            }
        }

        private static void Row(bool ok, string text) =>
            EditorGUILayout.LabelField(new GUIContent((ok ? "✔  " : "•  ") + text), EditorStyles.wordWrappedLabel);

        private static void OpenScene(string relativePath)
        {
            string root = SaveSystemInstaller.ModuleRoot;
            if (root == null || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene($"{root}/{relativePath}");
        }

        private static void OpenModuleAsset(string relativePath)
        {
            string root = SaveSystemInstaller.ModuleRoot;
            Object asset = root == null ? null : AssetDatabase.LoadMainAssetAtPath($"{root}/{relativePath}");
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
        }
    }
}

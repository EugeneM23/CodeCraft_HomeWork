using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rukhanka.Toolbox;

public class AnimationEventEnumGenerator : EditorWindow
{
    private string className = "AnimationEventType";
    private string outputPath = "Assets/Game/Scripts/CodeGen";
    private string namespaceString = "Game.Animation";
    private Vector2 scrollPosition;
    private Dictionary<string, uint> foundEvents = new Dictionary<string, uint>();

    [MenuItem("Tools/Generate Animation Events Enum")]
    public static void ShowWindow()
    {
        GetWindow<AnimationEventEnumGenerator>("Animation Events Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animation Event Enum Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        className = EditorGUILayout.TextField("Class Name:", className);
        outputPath = EditorGUILayout.TextField("Output Path:", outputPath);
        namespaceString = EditorGUILayout.TextField("Namespace:", namespaceString);

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan Animation Events", GUILayout.Height(30)))
        {
            ScanAnimationEvents();
        }

        if (GUILayout.Button("Add Custom Event", GUILayout.Height(25)))
        {
            CustomHashDialog.ShowWindow(this);
        }

        EditorGUILayout.Space();

        if (foundEvents.Count > 0)
        {
            GUILayout.Label($"Found {foundEvents.Count} events:", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            foreach (var kvp in foundEvents.OrderBy(x => x.Key))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{kvp.Key} = {kvp.Value}u");
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    foundEvents.Remove(kvp.Key);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Class", GUILayout.Height(30)))
            {
                GenerateClassFile();
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(30)))
            {
                foundEvents.Clear();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    public void AddCustomHash(string eventName, uint hash)
    {
        foundEvents[eventName] = hash;
        Repaint();
    }

    private void ScanAnimationEvents()
    {
        foundEvents.Clear();
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip != null)
            {
                foreach (var animEvent in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (!string.IsNullOrEmpty(animEvent.functionName))
                    {
                        string eventName = animEvent.functionName;
                        uint hash = eventName.CalculateHash32();
                        
                        if (!foundEvents.ContainsKey(eventName))
                        {
                            foundEvents[eventName] = hash;
                        }
                    }
                }
            }
        }

        Debug.Log($"Found {foundEvents.Count} unique events");
    }

    private void GenerateClassFile()
    {
        if (foundEvents.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No events found", "OK");
            return;
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// Auto-generated");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceString}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {className}");
        sb.AppendLine("    {");
        sb.AppendLine("        public const uint None = 0u;");
        sb.AppendLine();

        foreach (var kvp in foundEvents.OrderBy(x => x.Key))
        {
            string sanitizedName = SanitizeName(kvp.Key);
            sb.AppendLine($"        public const uint {sanitizedName} = {kvp.Value}u;");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        string fullPath = Path.Combine(outputPath, $"{className}.cs");
        File.WriteAllText(fullPath, sb.ToString());
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"Generated: {fullPath}", "OK");
        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, 1);
    }

    private string SanitizeName(string name)
    {
        StringBuilder sb = new StringBuilder();

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
            else
                sb.Append('_');
        }

        string result = sb.ToString();
        if (result.Length > 0 && char.IsDigit(result[0]))
            result = "Event_" + result;

        return result;
    }
}

public class CustomHashDialog : EditorWindow
{
    private string eventName = "";
    private string hashString = "";
    private AnimationEventEnumGenerator parentWindow;

    public static void ShowWindow(AnimationEventEnumGenerator parent)
    {
        CustomHashDialog window = GetWindow<CustomHashDialog>("Add Custom Event");
        window.parentWindow = parent;
        window.minSize = new Vector2(300, 100);
        window.maxSize = new Vector2(300, 100);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();

        eventName = EditorGUILayout.TextField("Event Name:", eventName);
        hashString = EditorGUILayout.TextField("Hash:", hashString);
        
        EditorGUILayout.Space();

        if (GUILayout.Button("Add"))
        {
            if (uint.TryParse(hashString, out uint hash))
            {
                parentWindow.AddCustomHash(eventName, hash);
                Close();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Invalid hash", "OK");
            }
        }
    }
}
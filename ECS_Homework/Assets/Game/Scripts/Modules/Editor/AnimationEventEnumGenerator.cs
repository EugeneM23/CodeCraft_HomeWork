using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rukhanka.Toolbox;
using Unity.Collections;

public class AnimationEventEnumGenerator : EditorWindow
{
    private string enumName = "AnimationEventType";
    private string outputPath = "Assets/Game/Scripts/CodeGen";
    private string namespaceString = "Game.Animation";
    private Vector2 scrollPosition;
    private List<string> foundEvents = new List<string>();

    [MenuItem("Tools/Generate Animation Events Enum")]
    public static void ShowWindow()
    {
        GetWindow<AnimationEventEnumGenerator>("Animation Events Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animation Event Enum Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        enumName = EditorGUILayout.TextField("Enum Name:", enumName);
        outputPath = EditorGUILayout.TextField("Output Path:", outputPath);
        namespaceString = EditorGUILayout.TextField("Namespace:", namespaceString);

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan Animation Events", GUILayout.Height(30)))
        {
            ScanAnimationEvents();
        }

        if (foundEvents.Count > 0)
        {
            GUILayout.Label($"Found {foundEvents.Count} events:", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            foreach (var eventName in foundEvents)
            {
                EditorGUILayout.LabelField("• " + eventName);
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Generate Enum", GUILayout.Height(30)))
            {
                GenerateEnumFile();
            }
        }
    }

    private void ScanAnimationEvents()
    {
        foundEvents.Clear();
        HashSet<string> eventNames = new HashSet<string>();

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip != null)
            {
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);

                foreach (var animEvent in events)
                {
                    if (!string.IsNullOrEmpty(animEvent.functionName))
                    {
                        eventNames.Add(animEvent.functionName);
                    }
                }
            }
        }

        foundEvents = eventNames.OrderBy(x => x).ToList();
        Debug.Log($"Found {foundEvents.Count} events");
    }

    private uint ComputeHash(string name)
    {
        return name.CalculateHash32();
    }

    private void GenerateEnumFile()
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
        sb.AppendLine($"    public enum {enumName} : uint");
        sb.AppendLine("    {");
        sb.AppendLine("        None = 0,");

        foreach (var eventName in foundEvents)
        {
            uint hash = ComputeHash(eventName);
            sb.AppendLine($"        {SanitizeName(eventName)} = {hash}u,");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        string fullPath = Path.Combine(outputPath, $"{enumName}.cs");
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
        if (char.IsDigit(result[0]))
            result = "Event_" + result;

        return result;
    }
}
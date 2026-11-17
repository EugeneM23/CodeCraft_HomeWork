using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>The graph asset's inspector: open it in the graph window, its issues, its records for the curious.</summary>
    [CustomEditor(typeof(BehaviorGraphAsset))]
    public sealed class BehaviorGraphAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (BehaviorGraphAsset)target;
            if (GUILayout.Button("Open in the Graph Window", GUILayout.Height(28f)))
                BehaviorGraphWindow.Open(asset);

            foreach (GraphIssue issue in asset.Issues)
                EditorGUILayout.HelpBox(issue.ToString(), issue.IsError ? MessageType.Error : MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{asset.Records.Count} records, {asset.Variables.Count} variables",
                EditorStyles.miniLabel);
            DrawDefaultInspector();
        }
    }
}

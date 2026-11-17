using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>The AI Squad's inspector: its command graph, its characters, and a button that opens the graph.</summary>
    [CustomEditor(typeof(AISquad))]
    public sealed class AISquadEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var squad = (AISquad)target;
            using (new EditorGUI.DisabledScope(squad.GraphAsset == null))
            {
                if (GUILayout.Button("Open Command Graph"))
                    BehaviorGraphWindow.Open(squad.GraphAsset);
            }

            if (!Application.isPlaying || squad.Squad == null)
                return;

            Squad running = squad.Squad;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("In Play", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Decision", squad.Graph?.Decision?.DisplayName ?? "none");
            EditorGUILayout.LabelField("Doing", squad.Graph?.Current?.DisplayName ?? "nothing");
            EditorGUILayout.LabelField("Target", running.Target != null ? running.Target.name : "nobody");
            EditorGUILayout.LabelField("Members", $"{running.Count}, in place {running.Cohesion:P0}");
            EditorGUILayout.LabelField("Shape", running.Shape.ToString());
            Repaint();
        }
    }
}

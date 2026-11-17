using System;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>The cheat panel (Window → AI Kit → Cheats): AICheats commands typed or clicked, in Play Mode.</summary>
    public sealed class AICheatsWindow : EditorWindow
    {
        private string _line = "ai.trace on";
        private string _answer = "";

        [MenuItem("Window/AI Kit/Cheats")]
        public static void Open() => GetWindow<AICheatsWindow>("AI Cheats");

        private void OnGUI()
        {
            GUI.enabled = Application.isPlaying;
            using (new EditorGUILayout.HorizontalScope())
            {
                _line = EditorGUILayout.TextField(_line);
                if (GUILayout.Button("Run", GUILayout.Width(50f)))
                    _answer = AICheats.Execute(_line);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (string command in new[] { "ai.trace on", "ai.trace off", "ai.freeze on", "ai.freeze off" })
                {
                    if (GUILayout.Button(command))
                        _answer = AICheats.Execute(command);
                }
            }

            foreach (string name in AICheats.SpawnableNames)
            {
                if (GUILayout.Button($"ai.spawn {name} at the scene view's pivot"))
                {
                    SceneView view = SceneView.lastActiveSceneView;
                    Vector3 at = view != null ? view.pivot : Vector3.zero;
                    _answer = AICheats.Execute(FormattableString.Invariant($"ai.spawn {name} {at.x} {at.y} {at.z}"));
                }
            }

            GUI.enabled = true;
            EditorGUILayout.HelpBox(Application.isPlaying ? _answer : "Play Mode only.", MessageType.None);
        }
    }
}

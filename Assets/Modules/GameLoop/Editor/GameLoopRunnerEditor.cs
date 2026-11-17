using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ticking.Editor
{
    /// <summary>
    /// The runner's inspector shows its loop live in Play Mode: running or paused, who holds the pause, game time,
    /// listeners per phase. A button holds and lets go a pause of its own ("Inspector") — to look at a frozen frame.
    /// </summary>
    [CustomEditor(typeof(GameLoopRunner))]
    public sealed class GameLoopRunnerEditor : UnityEditor.Editor
    {
        private static readonly InspectorSource Source = new();
        private bool _showListeners;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("In Play Mode the loop's state shows here.", MessageType.None);
                return;
            }

            // A prefab asset is not the running instance: its loop is a different, idle one
            if (EditorUtility.IsPersistent(target))
            {
                EditorGUILayout.HelpBox("This is the prefab asset. Select the instance in the scene " +
                                        "(DontDestroyOnLoad) to see the running loop.", MessageType.None);
                return;
            }

            GameLoop loop = ((GameLoopRunner)target).Loop;
            EditorGUILayout.LabelField("Game Loop", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State", loop.IsPaused ? "Paused" : "Running");
            EditorGUILayout.LabelField("Game time", $"{loop.Time:F2} s");
            EditorGUILayout.LabelField("Listeners",
                $"{loop.ListenerCount}: Update {loop.UpdateListenerCount}, Fixed {loop.FixedUpdateListenerCount}, " +
                $"Late {loop.LateUpdateListenerCount}, Pause {loop.PauseListenerCount}");

            if (loop.IsPaused)
            {
                EditorGUILayout.LabelField("Paused by");
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (object source in loop.PauseSources)
                        DrawObject(source);
                }
            }

            if (loop.IsPausedBy(Source))
            {
                if (GUILayout.Button("Resume (Inspector)"))
                    loop.Resume(Source);
            }
            else if (GUILayout.Button("Pause (Inspector)"))
            {
                loop.Pause(Source);
            }

            _showListeners = EditorGUILayout.Foldout(_showListeners, "Listeners", true);
            if (!_showListeners)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (object listener in loop.Listeners)
                    DrawObject(listener);
            }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        private static void DrawObject(object value)
        {
            if (value is Object unityObject)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(unityObject, typeof(Object), true);
            }
            else
            {
                EditorGUILayout.LabelField(value.ToString());
            }
        }

        private sealed class InspectorSource
        {
            public override string ToString() => "Inspector";
        }
    }
}

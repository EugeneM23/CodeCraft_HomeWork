using UnityEditor;
using UnityEngine;

namespace AIKit.Mecanim.Editor
{
    /// <summary>
    /// The AI Animator's inspector: the character it shows, the controller's parameters and states it uses (picked from
    /// the controller), the tuning (folded), and what its climbing and jumping clips measure on the character's body.
    /// </summary>
    [CustomEditor(typeof(AIAnimator))]
    public sealed class AIAnimatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var animator = (AIAnimator)target;
            serializedObject.Update();

            AICharacter character = animator.GetComponentInParent<AICharacter>(true);
            if (character == null)
                EditorGUILayout.HelpBox("No AI Character on this object or above it: there is nothing to show.",
                    MessageType.Warning);
            else
                EditorGUILayout.LabelField("Shows", character.name);

            if (TraversalMeasure.ControllerOf(animator.Animator) == null)
                EditorGUILayout.HelpBox("The Animator has no controller: the names below are typed, not picked.",
                    MessageType.Info);

            if (Section("Parameters", true))
                Children(serializedObject.FindProperty("_parameters"));
            if (Section("States", true))
                Children(serializedObject.FindProperty("_states"));
            if (Section("Tuning", false))
                Children(serializedObject.FindProperty("_tuning"));

            serializedObject.ApplyModifiedProperties();
            DrawTraversal(character);
        }

        // The climb and the jump of the character's body, as measured from this animator's clips
        private static void DrawTraversal(AICharacter character)
        {
            AgentBody body = character != null ? character.Body : null;
            if (body == null || !body.Traverses || body.Traversal == null)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(body.Traversal.Describe(), EditorStyles.wordWrappedMiniLabel);
            if (TraversalMeasure.IsStale(character))
                EditorGUILayout.HelpBox("The clips changed since they were measured.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Measure climbing and jumping"))
                {
                    string done = TraversalMeasure.MeasureInto(character, fresh: true);     // asked: measured now
                    if (done != null)
                        Debug.Log($"{character.name}: {done}", character);
                }
            }
        }

        private static void Children(SerializedProperty parent)
        {
            if (parent == null)
                return;

            EditorGUI.indentLevel++;
            SerializedProperty end = parent.GetEndProperty();
            SerializedProperty child = parent.Copy();
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                EditorGUILayout.PropertyField(child, true);
            }

            EditorGUI.indentLevel--;
        }

        private static bool Section(string title, bool openByDefault)
        {
            string key = "AIKit.AIAnimator." + title;
            bool open = SessionState.GetBool(key, openByDefault);
            bool now = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (now != open)
                SessionState.SetBool(key, now);
            return now;
        }
    }
}

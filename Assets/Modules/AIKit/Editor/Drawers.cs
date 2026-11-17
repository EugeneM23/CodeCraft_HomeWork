using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// A node setting in the inspector: one switch between "a number" and "a blackboard variable"; a variable is picked
    /// by name, with its scope.
    /// </summary>
    [CustomPropertyDrawer(typeof(BlackboardValue<>))]
    public sealed class BlackboardValueDrawer : PropertyDrawer
    {
        private const float TOGGLE = 18f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty constant = property.FindPropertyRelative("_constant");
            bool variable = property.FindPropertyRelative("_useVariable").boolValue;
            return variable || constant == null
                ? EditorGUIUtility.singleLineHeight
                : EditorGUI.GetPropertyHeight(constant, GUIContent.none, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty useVariable = property.FindPropertyRelative("_useVariable");
            SerializedProperty name = property.FindPropertyRelative("_name");
            SerializedProperty scope = property.FindPropertyRelative("_scope");
            SerializedProperty constant = property.FindPropertyRelative("_constant");

            EditorGUI.BeginProperty(position, label, property);
            Rect field = EditorGUI.PrefixLabel(position, label);
            var toggle = new Rect(field.x, field.y, TOGGLE, EditorGUIUtility.singleLineHeight);
            var rest = new Rect(field.x + TOGGLE + 2f, field.y, field.width - TOGGLE - 2f, field.height);

            useVariable.boolValue = GUI.Toggle(toggle, useVariable.boolValue,
                new GUIContent("$", "On — the value comes from a blackboard variable"), EditorStyles.miniButton);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            if (useVariable.boolValue)
            {
                var scopeRect = new Rect(rest.x, rest.y, 70f, EditorGUIUtility.singleLineHeight);
                var nameRect = new Rect(rest.x + 72f, rest.y, rest.width - 72f, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(scopeRect, scope, GUIContent.none);
                EditorGUI.PropertyField(nameRect, name, GUIContent.none);
            }
            else if (constant != null)
            {
                EditorGUI.PropertyField(rest, constant, GUIContent.none, true);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>
    /// A text field that names something of the project — an action, a group of attack places, a move, a weapon, an
    /// ability, a type, a tag — in an ordinary inspector (a weapon's, an ability's): a list to pick from, the same the
    /// canvas shows (NodeOptions; step 90, C5). A name the project has none of stays, marked "(not found)".
    /// </summary>
    [CustomPropertyDrawer(typeof(ActionNameAttribute))]
    [CustomPropertyDrawer(typeof(PoolNameAttribute))]
    [CustomPropertyDrawer(typeof(WeaponMoveNameAttribute))]
    [CustomPropertyDrawer(typeof(WeaponNameAttribute))]
    [CustomPropertyDrawer(typeof(TypeNameAttribute))]
    [CustomPropertyDrawer(typeof(TagNameAttribute))]
    public sealed class PickerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            if (attribute is TagNameAttribute)
            {
                string stored = property.stringValue;
                string tag = EditorGUI.TagField(position, label, ShownTag(stored));
                string next = TagToStore(stored, tag);
                if (next != stored)
                    property.stringValue = next;
                EditorGUI.EndProperty();
                return;
            }

            System.Collections.Generic.List<string> options = NodeOptions.For(fieldInfo) ?? new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<string> words = ElementInspector.OptionWords(options, property.stringValue,
                NodeOptions.EmptyMeaning(fieldInfo), null, out int selected, out System.Collections.Generic.List<string> values);
            var shown = new GUIContent[words.Count];
            for (int i = 0; i < words.Count; i++)
                shown[i] = new GUIContent(words[i].Replace('/', '∕'));

            int picked = EditorGUI.Popup(position, label, selected, shown);
            if (picked >= 0 && picked < values.Count && picked != selected)
                property.stringValue = values[picked];
            EditorGUI.EndProperty();
        }

        /// <summary>How the tag field shows a stored tag (an empty one — as "Untagged", the field has no word for none).</summary>
        public static string ShownTag(string stored) => string.IsNullOrEmpty(stored) ? "Untagged" : stored;

        /// <summary>
        /// What a tag field stores after it is drawn: the tag picked in it — or what was stored, when the field only shows
        /// it (an empty tag is shown as "Untagged" and stays empty: an Area Damage with no tag hits nobody, "Untagged" —
        /// every untagged object).
        /// </summary>
        public static string TagToStore(string stored, string picked) => picked == ShownTag(stored) ? stored ?? "" : picked;
    }

    /// <summary>A response curve with its shape drawn next to the numbers.</summary>
    [CustomPropertyDrawer(typeof(ResponseCurve))]
    public sealed class ResponseCurveDrawer : PropertyDrawer
    {
        private const float GRAPH = 48f;
        private const int SAMPLES = 32;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true) + (property.isExpanded ? GRAPH + 4f : 0f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float fields = EditorGUI.GetPropertyHeight(property, label, true);
            EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, fields), property, label, true);
            if (!property.isExpanded)
                return;

            var graph = new Rect(position.x + EditorGUIUtility.labelWidth, position.y + fields + 2f,
                position.width - EditorGUIUtility.labelWidth, GRAPH);
            EditorGUI.DrawRect(graph, new Color(0f, 0f, 0f, 0.2f));
            ResponseCurve curve = ReadCurve(property);
            var points = new Vector3[SAMPLES + 1];
            for (int i = 0; i <= SAMPLES; i++)
            {
                float x = i / (float)SAMPLES;
                float y = Mathf.Clamp01(curve.Evaluate(x));
                points[i] = new Vector3(graph.x + x * graph.width, graph.yMax - y * graph.height, 0f);
            }

            Handles.color = new Color(0.4f, 0.8f, 1f);
            Handles.DrawAAPolyLine(2f, points);
        }

        private static ResponseCurve ReadCurve(SerializedProperty property) => new()
        {
            Type = (CurveType)property.FindPropertyRelative(nameof(ResponseCurve.Type)).enumValueIndex,
            M = property.FindPropertyRelative(nameof(ResponseCurve.M)).floatValue,
            K = property.FindPropertyRelative(nameof(ResponseCurve.K)).floatValue,
            B = property.FindPropertyRelative(nameof(ResponseCurve.B)).floatValue,
            C = property.FindPropertyRelative(nameof(ResponseCurve.C)).floatValue
        };
    }
}

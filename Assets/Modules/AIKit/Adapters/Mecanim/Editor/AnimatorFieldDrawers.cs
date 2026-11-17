using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Mecanim.Editor
{
    /// <summary>
    /// The names of the AI Animator's settings are picked from its controller, never typed (§32.2: keys from lists): a
    /// parameter of the right kind, a state, a layer. A name the controller does not have is shown as missing; with no
    /// controller the plain text field is left.
    /// </summary>
    internal static class AnimatorFieldNames
    {
        public const string NONE = "(none)";

        public static AnimatorController ControllerOf(SerializedProperty property) =>
            property.serializedObject.targetObject is Component component
                ? TraversalMeasure.ControllerOf(component.GetComponent<Animator>())
                : null;

        // A popup of these names with the current one picked (a missing one is shown and kept until another is picked)
        public static void Popup(Rect position, SerializedProperty property, GUIContent label, List<string> names)
        {
            string current = property.stringValue ?? "";
            var options = new List<string> { NONE };
            options.AddRange(names);
            int index = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
            if (index < 0)
            {
                options.Add(current + " (missing)");
                index = options.Count - 1;
            }

            EditorGUI.BeginProperty(position, label, property);
            int picked = EditorGUI.Popup(position, label, index, options.Select(name => new GUIContent(name)).ToArray());
            if (picked != index)
                property.stringValue = picked == 0 ? "" : picked <= names.Count ? names[picked - 1] : current;
            EditorGUI.EndProperty();
        }

        public static List<string> States(AnimatorController controller)
        {
            var names = new List<string>();
            foreach (AnimatorControllerLayer layer in controller.layers)
                Collect(layer.stateMachine, names);
            return names.Distinct().OrderBy(name => name).ToList();
        }

        private static void Collect(AnimatorStateMachine machine, List<string> names)
        {
            if (machine == null)
                return;

            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null)
                    names.Add(child.state.name);
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                Collect(child.stateMachine, names);
        }
    }

    [CustomPropertyDrawer(typeof(AnimatorParameterAttribute))]
    internal sealed class AnimatorParameterDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            AnimatorController controller = AnimatorFieldNames.ControllerOf(property);
            if (controller == null || property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var type = ((AnimatorParameterAttribute)attribute).Type;
            List<string> names = controller.parameters.Where(parameter => parameter.type == type)
                .Select(parameter => parameter.name).ToList();
            AnimatorFieldNames.Popup(position, property, label, names);
        }
    }

    [CustomPropertyDrawer(typeof(AnimatorStateAttribute))]
    internal sealed class AnimatorStateDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            AnimatorController controller = AnimatorFieldNames.ControllerOf(property);
            if (controller == null || property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            AnimatorFieldNames.Popup(position, property, label, AnimatorFieldNames.States(controller));
        }
    }

    [CustomPropertyDrawer(typeof(AnimatorLayerAttribute))]
    internal sealed class AnimatorLayerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            AnimatorController controller = AnimatorFieldNames.ControllerOf(property);
            if (controller == null || property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            AnimatorFieldNames.Popup(position, property, label, controller.layers.Select(layer => layer.name).ToList());
        }
    }
}

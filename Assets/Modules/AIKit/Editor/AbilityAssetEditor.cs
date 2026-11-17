using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// The inspector of an ability made of parts (step 90, C6): its name, then each kind of part with "+ part" — the
    /// conditions of when it may be used, the queue and the timing, one delivery (picked from a list), the effects and the
    /// cues. A part is any non-abstract class of its kind in any assembly, so a game adds its own (a heal, a status) without
    /// touching this.
    /// </summary>
    [CustomEditor(typeof(AbilityAsset), true)]
    public sealed class AbilityAssetEditor : UnityEditor.Editor
    {
        private static readonly string[] Drawn =
        {
            "m_Script", nameof(AbilityAsset.Name), nameof(AbilityAsset.Conditions), nameof(AbilityAsset.Queue),
            nameof(AbilityAsset.Timing), nameof(AbilityAsset.Delivery), nameof(AbilityAsset.Effects),
            nameof(AbilityAsset.Cues)
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(AbilityAsset.Name)));

            Parts<AbilityCondition>(nameof(AbilityAsset.Conditions), "When it may be used", "+ condition");
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(AbilityAsset.Queue)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(AbilityAsset.Timing)), true);
            Delivery();
            Parts<AbilityEffect>(nameof(AbilityAsset.Effects), "What it does where it acts", "+ effect");
            Parts<AbilityCue>(nameof(AbilityAsset.Cues), "What is shown while it flies", "+ cue");

            // Fields of a subclass (a game's own ability script) below the parts
            DrawPropertiesExcluding(serializedObject, Drawn);
            serializedObject.ApplyModifiedProperties();

            if (target is AbilityAsset { ConvertedOnLoad: true } ability)
            {
                EditorGUILayout.HelpBox("Made of the flat numbers of before step 90 and turned into parts when it was read: " +
                                        "save to keep the parts.", MessageType.Info);
                if (GUILayout.Button("Save The Parts"))
                {
                    ability.ConvertedOnLoad = false;
                    EditorUtility.SetDirty(ability);
                    AssetDatabase.SaveAssetIfDirty(ability);
                }
            }
        }

        /// <summary>The non-abstract part classes of a kind, by name (what "+ part" offers).</summary>
        public static List<Type> KindsOf(Type kind)
        {
            var kinds = new List<Type>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom(kind))
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition && type.GetConstructor(Type.EmptyTypes) != null)
                    kinds.Add(type);
            }

            kinds.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return kinds;
        }

        /// <summary>A part's kind in plain words ("Area Damage" for AreaDamageEffect).</summary>
        public static string TitleOf(Type type)
        {
            string name = type.Name;
            foreach (string tail in new[] { "Condition", "Delivery", "Effect", "Cue" })
            {
                if (name.EndsWith(tail, StringComparison.Ordinal) && name.Length > tail.Length)
                {
                    name = name.Substring(0, name.Length - tail.Length);
                    break;
                }
            }

            return ObjectNames.NicifyVariableName(name);
        }

        private void Parts<T>(string field, string title, string add) where T : AbilityPart
        {
            SerializedProperty list = serializedObject.FindProperty(field);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    object part = element.managedReferenceValue;
                    EditorGUILayout.LabelField(part != null ? TitleOf(part.GetType()) : "(missing part)",
                        EditorStyles.miniBoldLabel);
                    if (GUILayout.Button("×", GUILayout.Width(22f)))
                    {
                        list.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                EditorGUI.indentLevel++;
                DrawChildren(element);
                EditorGUI.indentLevel--;
            }

            List<Type> kinds = KindsOf(typeof(T));
            var names = new string[kinds.Count + 1];
            names[0] = add;
            for (int i = 0; i < kinds.Count; i++)
                names[i + 1] = TitleOf(kinds[i]);
            int picked = EditorGUILayout.Popup(0, names);
            if (picked <= 0)
                return;

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue =
                Activator.CreateInstance(kinds[picked - 1]);
        }

        private void Delivery()
        {
            SerializedProperty delivery = serializedObject.FindProperty(nameof(AbilityAsset.Delivery));
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("How it gets where it acts", EditorStyles.boldLabel);
            List<Type> kinds = KindsOf(typeof(AbilityDelivery));
            var names = new string[kinds.Count];
            int current = -1;
            object value = delivery.managedReferenceValue;
            for (int i = 0; i < kinds.Count; i++)
            {
                names[i] = TitleOf(kinds[i]);
                if (value != null && value.GetType() == kinds[i])
                    current = i;
            }

            int picked = EditorGUILayout.Popup("Delivery", current, names);
            if (picked != current && picked >= 0)
                delivery.managedReferenceValue = Activator.CreateInstance(kinds[picked]);

            EditorGUI.indentLevel++;
            DrawChildren(delivery);
            EditorGUI.indentLevel--;
        }

        // The fields of a part, without its own foldout
        private static void DrawChildren(SerializedProperty part)
        {
            SerializedProperty child = part.Copy();
            SerializedProperty end = part.GetEndProperty();
            if (!child.NextVisible(true))
                return;

            while (!SerializedProperty.EqualContents(child, end))
            {
                EditorGUILayout.PropertyField(child, true);
                if (!child.NextVisible(false))
                    break;
            }
        }
    }
}

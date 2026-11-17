using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>A SceneReference as one scene field; the stored path follows the picked asset and is cleared with it.</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty asset = property.FindPropertyRelative("_asset");
            SerializedProperty path = property.FindPropertyRelative("_path");

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            Object picked = EditorGUI.ObjectField(position, label, asset.objectReferenceValue, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                asset.objectReferenceValue = picked;
                path.stringValue = picked != null ? AssetDatabase.GetAssetPath(picked) : string.Empty;
            }

            EditorGUI.EndProperty();
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.AudioEngine.Adapters.EditorTools
{
    /// <summary>
    /// Wiring of the audio prefabs: no missing scripts, no empty object reference in our and the module's
    /// components (AudioSource is left out, its clip is empty by design), every AudioSource routed to a mixer group.
    /// </summary>
    public static class AudioPrefabCheck
    {
        /// <summary>Problems of the prefab at this path, empty when it is wired.</summary>
        public static List<string> FindProblems(string prefabPath)
        {
            List<string> problems = new List<string>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                problems.Add($"{prefabPath} missing (run Tools/Audio/Build Prefabs)");
                return problems;
            }

            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = transform.gameObject;
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                    problems.Add($"{prefabPath}: {go.name} has {missing} missing scripts");

                foreach (AudioSource source in go.GetComponents<AudioSource>())
                    if (source.outputAudioMixerGroup == null)
                        problems.Add($"{prefabPath}: AudioSource on {go.name} has no mixer group");

                foreach (MonoBehaviour behaviour in go.GetComponents<MonoBehaviour>())
                    if (behaviour != null)
                        CollectNullReferences(prefabPath, behaviour, problems);
            }

            return problems;
        }

        private static void CollectNullReferences(string prefabPath, MonoBehaviour behaviour, List<string> problems)
        {
            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = iterator.propertyType == SerializedPropertyType.Generic;
                // m_ fields are Unity's own (m_Script, m_PrefabInstance...), empty by design in a prefab asset.
                if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                    iterator.name.StartsWith("m_", System.StringComparison.Ordinal))
                    continue;

                if (iterator.objectReferenceValue == null)
                    problems.Add($"{prefabPath}: {behaviour.GetType().Name}.{iterator.propertyPath} is empty");
            }
        }
    }
}

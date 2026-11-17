using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// Tools → AI Kit → Validate All: every AICharacter of the prefabs and of the open scenes — the problems its
    /// inspector shows. Each issue is a console line that names the character; clicking it selects it.
    /// </summary>
    public static class ValidateAll
    {
        [MenuItem("Tools/AI Kit/Validate All")]
        public static void Run()
        {
            List<ValidationIssue> issues = Collect();
            foreach (ValidationIssue issue in issues)
            {
                if (issue.IsError)
                    Debug.LogError(issue.Message, issue.Context);
                else
                    Debug.LogWarning(issue.Message, issue.Context);
            }

            Debug.Log(issues.Count == 0 ? "AI Kit: all characters are valid." : $"AI Kit: {issues.Count} issue(s).");
        }

        public static List<ValidationIssue> Collect()
        {
            var issues = new List<ValidationIssue>();

            // The characters: on the prefabs, and in the open scenes (a scene object, not an asset)
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null)
                    continue;

                foreach (AICharacter character in prefab.GetComponentsInChildren<AICharacter>(true))
                    issues.AddRange(CharacterIssues(character));
            }

            foreach (AICharacter character in Object.FindObjectsByType<AICharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!EditorUtility.IsPersistent(character))
                    issues.AddRange(CharacterIssues(character));
            }

            return issues;
        }

        /// <summary>The problems of one character (the ones its inspector shows), each naming the character.</summary>
        public static List<ValidationIssue> CharacterIssues(AICharacter character)
        {
            var issues = new List<ValidationIssue>();
            foreach (string problem in AICharacterEditor.Problems(character))
            {
                issues.Add(new ValidationIssue(ValidationCode.CharacterSetup, false, $"{character.name}: {problem}",
                    character));
            }

            return issues;
        }
    }
}

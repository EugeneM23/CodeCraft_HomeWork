using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Editor
{
    /// <summary>
    /// The inspector of AICharacter, in four sections: Behavior (the graph), Body (what moves the character; picking a
    /// kind adds the components it needs; the climb and the jump as measured from its clips), Combat (the weapons — the
    /// abilities are the graph's) and Advanced. Its picture is an AI Animator's (step 90, C22) and its senses a Senses
    /// sensor's of the graph (C21): neither is a section here. Below them is what is wrong with the setup: the same list
    /// Validate All prints.
    /// </summary>
    [CustomEditor(typeof(AICharacter))]
    public sealed class AICharacterEditor : UnityEditor.Editor
    {
        private const string NONE = "None";

        /// <summary>The problem of a character without a behavior graph.</summary>
        public const string NO_GRAPH = "No behavior graph — the character does nothing.";

        private static readonly string[] AdvancedFields =
        {
            "_simulation", "_thinkInterval", "_seed", "_trace", "_faction", "_drawSensors", "_showState", "_stateHeight",
            "_showName"
        };

        /// <summary>The body kinds a character can be given: every concrete AgentBody of every assembly.</summary>
        public static List<Type> BodyKinds()
        {
            var kinds = new List<Type>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<AgentBody>())
            {
                if (!type.IsAbstract && !type.IsGenericTypeDefinition)
                    kinds.Add(type);
            }

            kinds.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return kinds;
        }

        /// <summary>Adds the components the character's body needs and the character does not have yet.</summary>
        public static void EnsureRequired(AICharacter character)
        {
            if (character == null || character.Body == null)
                return;

            foreach (Type type in character.Body.RequiredComponents)
            {
                if (character.GetComponent(type) == null)
                    Undo.AddComponent(character.gameObject, type);
            }
        }

        /// <summary>What is wrong with the character's setup, in plain words (empty: nothing).</summary>
        public static List<string> Problems(AICharacter character)
        {
            var problems = new List<string>();
            if (character == null)
                return problems;

            AgentBody body = character.Body;
            if (body == null)
                problems.Add("The character has no body: pick one in the Body section.");
            else
            {
                foreach (Type type in body.RequiredComponents)
                {
                    if (character.GetComponent(type) == null)
                        problems.Add($"The body needs a {type.Name} component on the character.");
                }
            }

            // How it is shown is the picture's (the AI Animator's editor says what is wrong there)
            foreach (ICharacterPictureHook hook in CharacterPictureHooks.All)
            {
                string picture = hook.Problem(character);
                if (picture != null)
                    problems.Add(picture);
            }

            if (character.GraphAsset == null)
            {
                problems.Add(NO_GRAPH);
                return problems;
            }

            BehaviorGraph graph = character.GraphAsset.Peek();
            if (graph == null)
                return problems;

            // The graph does not care which weapon is in the hand: a move it asks for is a slot every weapon the
            // character carries must have
            IReadOnlyList<WeaponAsset> carried = character.Carried;
            var seen = new HashSet<string>();
            graph.VisitNodes(node =>
            {
                string problem = null;
                if (node is AttackTask attack)
                {
                    var without = new List<string>();
                    for (int i = 0; i < carried.Count; i++)
                    {
                        if (carried[i].Find(attack.Move) == null)
                            without.Add(carried[i].name);
                    }

                    if (carried.Count == 0)
                        problem = "Attack needs a weapon on the character (Combat section).";
                    else if (without.Count > 0)
                    {
                        problem = string.IsNullOrEmpty(attack.Move)
                            ? $"No moves on: {string.Join(", ", without)}."
                            : $"No move '{attack.Move}' on: {string.Join(", ", without)}.";
                    }
                }
                else if (node is SwitchWeaponTask { Weapon: SwitchWeaponTask.NEXT })
                {
                    if (carried.Count < 2)
                        problem = "Switch Weapon (Next) needs two weapons on the character (Combat section, Weapons).";
                }
                else if (node is SwitchWeaponTask switching && character.Find(switching.Weapon) == null)
                {
                    problem = string.IsNullOrEmpty(switching.Weapon) ? "Switch Weapon names no weapon."
                        : $"The character does not carry '{switching.Weapon}' (Combat section, Weapons).";
                }

                if (problem != null && seen.Add(problem))
                    problems.Add(problem);
            });

            // The abilities are the graph's (step 90): what is wrong with them is the graph's problem
            foreach (GraphIssue issue in character.GraphAsset.Issues)
            {
                if (issue.Code is GraphIssueCode.NoAbility or GraphIssueCode.AbilityNameClash
                        or GraphIssueCode.EmptyAbilitySet && seen.Add(issue.Message))
                    problems.Add(issue.Message);
            }

            return problems;
        }

        public override void OnInspectorGUI()
        {
            var character = (AICharacter)target;
            serializedObject.Update();

            if (Section("Behavior"))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_graph"));
                serializedObject.ApplyModifiedProperties();
                BehaviorSection.Draw(character);
                serializedObject.Update();
            }

            if (Section("Body"))
                DrawBody(character);

            if (Section("Combat"))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_weapon"), new GUIContent("Weapon In Hand"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_weapons"), true);
            }

            if (Section("Advanced"))
            {
                for (int i = 0; i < AdvancedFields.Length; i++)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(AdvancedFields[i]));
            }

            serializedObject.ApplyModifiedProperties();

            foreach (string problem in Problems(character))
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        private void DrawBody(AICharacter character)
        {
            SerializedProperty body = serializedObject.FindProperty("_body");
            List<Type> kinds = BodyKinds();
            var names = new string[kinds.Count + 1];
            names[0] = NONE;
            int current = 0;
            for (int i = 0; i < kinds.Count; i++)
            {
                names[i + 1] = ObjectNames.NicifyVariableName(kinds[i].Name);
                if (character.Body != null && character.Body.GetType() == kinds[i])
                    current = i + 1;
            }

            int picked = EditorGUILayout.Popup("Kind", current, names);
            if (picked != current)
            {
                body.managedReferenceValue = picked == 0 ? null : Activator.CreateInstance(kinds[picked - 1]);
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                EnsureRequired(character);
                body = serializedObject.FindProperty("_body");
            }

            if (body.managedReferenceValue != null)
                EditorGUILayout.PropertyField(body, true);

            DrawTraversal(character);
        }

        // The climb and the jump as the character's clips go (step 90, C20): one line of what was measured, and the
        // button that measures again — never the numbers themselves
        private void DrawTraversal(AICharacter character)
        {
            AgentBody body = character.Body;
            if (body == null || !body.Traverses || body.Traversal == null)
                return;

            ICharacterPictureHook[] hooks = CharacterPictureHooks.All;
            bool stale = false;
            foreach (ICharacterPictureHook hook in hooks)
                stale |= hook.TraversalIsStale(character);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(body.Traversal.Describe(), EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUI.DisabledScope(hooks.Length == 0 || Application.isPlaying))
                {
                    if (GUILayout.Button("Measure again", GUILayout.Width(100f)))
                    {
                        serializedObject.ApplyModifiedProperties();
                        foreach (ICharacterPictureHook hook in hooks)
                        {
                            string done = hook.MeasureTraversal(character);
                            if (done != null)
                                Debug.Log($"{character.name}: {done}", character);
                        }

                        serializedObject.Update();
                    }
                }
            }

            if (stale)
                EditorGUILayout.HelpBox("The climbing or jumping clips changed since they were measured: measure again.",
                    MessageType.Info);
        }

        // A foldout whose state is kept for the editor session; Advanced starts folded
        private static bool Section(string title)
        {
            string key = "AIKit.Character." + title;
            bool open = SessionState.GetBool(key, title != "Advanced");
            bool now = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (now != open)
                SessionState.SetBool(key, now);
            return now;
        }
    }

    /// <summary>
    /// The button of a character's Behavior section. No scene objects are picked here (step 90, C18): the graph finds
    /// the objects of the world itself (Find), and the target comes from its sensors.
    /// </summary>
    internal static class BehaviorSection
    {
        public static void Draw(AICharacter character)
        {
            BehaviorGraphAsset graph = character.GraphAsset;
            if (graph == null)
            {
                if (GUILayout.Button("Create Behavior Graph", GUILayout.Height(30f)))
                    CreateGraph(character);
                return;
            }

            if (GUILayout.Button("Open Behavior Graph", GUILayout.Height(30f)))
                BehaviorGraphWindow.Open(character);

            if (Application.isPlaying)
                EditorGUILayout.LabelField("Running", character.State != null ? character.State.DisplayName : "\u2014");
        }

        private static void CreateGraph(AICharacter character)
        {
            string path = EditorUtility.SaveFilePanelInProject("New Behavior Graph", $"{character.name}Behavior", "asset",
                "Where to keep the graph of this character");
            if (string.IsNullOrEmpty(path))
                return;

            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            // A graph starts with its Brain: the only thing that decides what the character does
            BehaviorGraph graph = new BrainGraphBuilder().Build();
            GraphSerializer.Write(graph, asset);
            AssetDatabase.CreateAsset(asset, path);
            Undo.RecordObject(character, "Create Behavior Graph");
            character.GraphAsset = asset;
            EditorUtility.SetDirty(character);
            BehaviorGraphWindow.Open(character);
        }
    }
}

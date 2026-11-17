using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// One step of setting an agent up, contributed by whoever knows a part: an adapter picks its body when it finds
    /// what the body needs, a game adds its loop driver. Steps are found by type (TypeCache) — an assembly with a step
    /// plugs in by existing. Lower Order runs first; the core's own steps are at 0 (the character) and 900 (a fallback
    /// body).
    /// </summary>
    public interface IAgentSetupStep
    {
        int Order { get; }

        void Apply(AgentSetupContext setup);
    }

    /// <summary>The agent being set up and what was done (the wizard prints it).</summary>
    public sealed class AgentSetupContext
    {
        internal AgentSetupContext(GameObject root) => Root = root;

        public GameObject Root { get; }

        public List<string> Log { get; } = new();

        /// <summary>The component on that object, added (with Undo) only when the entity has none of it anywhere.</summary>
        public T Ensure<T>(Transform where) where T : Component
        {
            T existing = Root.GetComponentInChildren<T>(true);
            if (existing != null)
                return existing;

            T added = Undo.AddComponent<T>(where.gameObject);
            Log.Add($"+ {typeof(T).Name} on {where.name}");
            return added;
        }

        public bool Has<T>() where T : class => Root.GetComponentInChildren<T>(true) != null;
    }

    /// <summary>
    /// The setup wizard (Tools → AI Kit → Setup Agent): the AICharacter on the root (its body moves the root), the steps
    /// the project contributes, and a Simple Body when nobody gave the character a body. The graph goes into the
    /// character's own Graph field. Safe to run again — it only adds what is missing.
    /// </summary>
    public static class AgentSetup
    {
        public const string MENU = "Tools/AI Kit/Setup Agent";

        public static AgentSetupContext Run(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var setup = new AgentSetupContext(root);
            Undo.RegisterFullObjectHierarchyUndo(root, "Setup AI Character");
            foreach (IAgentSetupStep step in Steps())
                step.Apply(setup);

            EditorUtility.SetDirty(root);
            return setup;
        }

        /// <summary>Every step the project knows, in order (the core's own included).</summary>
        public static List<IAgentSetupStep> Steps()
        {
            var steps = new List<IAgentSetupStep> { new CharacterStep(), new FallbackBodyStep() };
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IAgentSetupStep>())
            {
                if (type.IsAbstract || type.IsInterface || type.Assembly == typeof(AgentSetup).Assembly ||
                    type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                steps.Add((IAgentSetupStep)Activator.CreateInstance(type));
            }

            return steps.OrderBy(step => step.Order).ThenBy(step => step.GetType().FullName).ToList();
        }

        [MenuItem(MENU)]
        [MenuItem("GameObject/AI Kit/Setup Agent", false, 20)]
        private static void SetupSelected()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("Setup Agent: select the object that becomes the agent.");
                return;
            }

            AgentSetupContext setup = Run(root);
            Debug.Log($"Setup Agent on {root.name}:\n" + (setup.Log.Count > 0 ? string.Join("\n", setup.Log) : "nothing to add"),
                root);
        }

        [MenuItem(MENU, true)]
        private static bool CanSetupSelected() => Selection.activeGameObject != null;

        private sealed class CharacterStep : IAgentSetupStep
        {
            public int Order => 0;

            public void Apply(AgentSetupContext setup) =>
                setup.Ensure<AICharacter>(setup.Root.transform);     // on the root: its body moves the root
        }

        // Nobody knew the body: the character gets a Simple Body that moves its transform, so it at least does
        // something visible
        private sealed class FallbackBodyStep : IAgentSetupStep
        {
            public int Order => 900;

            public void Apply(AgentSetupContext setup)
            {
                AICharacter character = setup.Root.GetComponentInChildren<AICharacter>(true);
                if (character == null || character.Body != null)
                    return;

                // In Play the character has woken up already, and a body is given only before it does
                if (Application.isPlaying)
                {
                    Debug.LogWarning($"Setup Agent: no body given to {character.name} — it is running (Play Mode), and " +
                                     "a body is given only before the character wakes up. Leave Play Mode and run " +
                                     "Setup Agent again, or pick a body in its Body section.", character);
                    return;
                }

                Undo.RecordObject(character, "Setup AI Character");
                character.Body = new SimpleBody();
                EditorUtility.SetDirty(character);
                setup.Log.Add($"+ Simple Body on {character.name}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace AIKit
{
    /// <summary>
    /// What a node is called. By default — its type ("Wait", "Strike", "Use Ability"): the title in the registry, the last
    /// part of its [AIKitNode] path. The user may give a node a label of its own on the canvas (AgentNode.Label, step 90:
    /// "по умолчанию — тип; пользователь может подписать ноду руками"); builders and code never do — what one node of a
    /// type does differently from another is in its settings, shown on the node. Name is a service field kept equal to
    /// the type title (NameByType: the builders and the graph window run it), so an older graph's names ("Strike" of a
    /// node whose type is called otherwise now) never pass for labels. A C# class name is never shown.
    /// </summary>
    public static class NodeTitles
    {
        private static readonly List<AgentState> States = new();

        // The title of a type, read once (a node's DisplayName runs in Play: no reflection per call)
        private static readonly Dictionary<Type, string> Titles = new();

        /// <summary>The last part of the node type's path in the registry ("Graph/Flow/Wait" — "Wait"); null — not a node type.</summary>
        public static string Of(Type type)
        {
            if (type == null)
                return null;

            lock (Titles)
            {
                if (Titles.TryGetValue(type, out string title))
                    return title;

                var info = (AIKitNodeAttribute)Attribute.GetCustomAttribute(type, typeof(AIKitNodeAttribute));
                title = info != null ? LeafOf(info.Path) : null;
                Titles[type] = title;
                return title;
            }
        }

        /// <summary>The last part of a path of the list ("AI/Decisions/Movement/Chase" — "Chase").</summary>
        public static string LeafOf(string path) =>
            string.IsNullOrEmpty(path) ? path : path.Substring(path.LastIndexOf('/') + 1);

        /// <summary>
        /// The node's type title; a node of no registered type (a test's own block) — its class name in plain words, with
        /// "Task" / "Reasoner" / "Condition" off ("HoldTask" — "Hold").
        /// </summary>
        public static string Of(AgentNode node) => node == null ? "?" : Of(node.GetType()) ?? Plain(node.GetType());

        /// <summary>
        /// A state's title: the type of the block it holds ("node = behavior"). The root and a state with no block keep
        /// their own name (they are not nodes of the canvas).
        /// </summary>
        public static string Of(AgentState state)
        {
            if (state == null)
                return "?";

            foreach (AgentTask task in state.Tasks)
            {
                if (!task.IsBackground)
                    return Of(task);
            }

            // The root (its tasks are sensors that run beside), an empty node: its own name — never the user's label (a
            // label passed off as a type title would be written into Name and outlive the label)
            return !string.IsNullOrEmpty(state.Name) ? state.Name : Of((AgentNode)state);
        }

        /// <summary>What the canvas shows as the node's title: the user's label, or the type title.</summary>
        public static string Shown(AgentNode node)
        {
            if (node == null)
                return "?";

            if (!string.IsNullOrEmpty(node.Label))
                return node.Label;

            return node is AgentState state ? Of(state) : Of(node);
        }

        /// <summary>The node carries a label of its own (the canvas shows its type under the title then).</summary>
        public static bool IsLabelled(AgentNode node) => node != null && !string.IsNullOrEmpty(node.Label);

        /// <summary>
        /// How the decision trace names a node: what it is called and its id, and its type when it is called otherwise
        /// ("Attack #12", "Run Away #12 (Kite)") — a label or a code name never hides what the node is. Built when the trace
        /// is read, never when it is recorded.
        /// </summary>
        public static string Traced(AgentNode node)
        {
            if (node == null)
                return "nowhere";

            string shown = node.DisplayName;
            string type = node is AgentState state ? Of(state) : Of(node);
            return string.IsNullOrEmpty(type) || type == shown ? $"{shown} #{node.Id}" : $"{shown} #{node.Id} ({type})";
        }

        /// <summary>
        /// Every state of the graph that holds a block gets the block's type title as its Name (the service name; a label
        /// the user gave stays as it is). True — a name changed.
        /// </summary>
        public static bool NameByType(BehaviorGraph graph)
        {
            if (graph?.Root == null)
                return false;

            States.Clear();
            graph.CollectStates(States);
            bool changed = false;
            foreach (AgentState state in States)
            {
                // A node of no block (none, or only tasks that run beside) has no type to be called by: left as it is
                if (state.Parent == null || !state.HasForegroundTasks)
                    continue;

                string title = Of(state);
                if (state.Name == title)
                    continue;

                state.Name = title;
                changed = true;
            }

            States.Clear();
            return changed;
        }

        // A class name in words: "KeepFormationTask" — "Keep Formation" (a type outside the registry only)
        private static string Plain(Type type)
        {
            string name = type.Name;
            foreach (string tail in new[] { "SensorTask", "Task", "Reasoner", "Condition", "Value" })
            {
                if (name.Length > tail.Length && name.EndsWith(tail, StringComparison.Ordinal))
                {
                    name = name.Substring(0, name.Length - tail.Length);
                    break;
                }
            }

            var words = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                    words.Append(' ');
                words.Append(name[i]);
            }

            return words.ToString();
        }
    }
}

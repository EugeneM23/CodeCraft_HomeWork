using System.Collections.Generic;
using System.Reflection;

namespace AIKit.Editor
{
    /// <summary>
    /// Step 90 (C18): the character keeps no scene objects any more, so a graph's object variables the character used to
    /// fill — Point A, Point B, Patrol Zone — hold nothing ever. Unless a node of the graph writes one (a sensor, a Find),
    /// such a variable goes from the graph with its nodes on the canvas and their wires: a pin that read it is empty
    /// again (Patrol Area without an area walks around the character's home). Runs when the canvas opens a graph written
    /// before step 90 (and by Tools → AI Kit → Upgrade All Graphs) — once: a graph of the format since (SINCE_FORMAT) has
    /// no such variables of before, and one of those names declared there is the user's own (a Find fills it next). A
    /// squad's command graph keeps them: its AI Squad still binds objects to its variables.
    /// </summary>
    public sealed class SceneObjectVariablesUpgrade : IEditorGraphUpgrade
    {
        /// <summary>The variables the lab's characters filled with their scene objects.</summary>
        public static readonly string[] Names = { "Point A", "Point B", "Patrol Zone" };

        /// <summary>The format a graph is written in since the character lost its scene objects (step 90).</summary>
        public const int SINCE_FORMAT = 1;

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues)
        {
            if (asset == null || graph == null || asset.FormatVersion >= SINCE_FORMAT || IsCommandGraph(graph))
                return false;

            bool changed = false;
            foreach (string name in Names)
            {
                VariableDeclaration declared = graph.Variables.Find(variable => variable.Name == name);
                if (declared == null || declared.Type != VariableType.Transform || IsWritten(graph, name))
                    continue;

                graph.Variables.Remove(declared);
                asset.VariableNodes.RemoveAll(node => node.Variable == name);
                graph.VisitNodes(node => Forget(node, name));
                changed = true;
            }

            return changed;
        }

        // A squad's command graph: run by an AI Squad, whose bindings fill its object variables
        private static bool IsCommandGraph(BehaviorGraph graph)
        {
            bool squad = false;
            graph.VisitNodes(node => squad |= node is SquadBrainTask);
            return squad;
        }

        // A node writes the variable: a sensor, a search (their Store In), a block that sets it
        private static bool IsWritten(BehaviorGraph graph, string name)
        {
            bool written = false;
            graph.VisitNodes(node =>
            {
                FieldInfo store = node.GetType().GetField("StoreIn");
                if (store != null && store.FieldType == typeof(string) && (string)store.GetValue(node) == name)
                    written = true;
            });
            return written;
        }

        // Every name field of the node that names the variable lets go of it
        private static void Forget(AgentNode node, string name)
        {
            foreach (FieldInfo field in node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(string) && field.GetCustomAttribute<VariableNameAttribute>() != null &&
                    (string)field.GetValue(node) == name)
                    field.SetValue(node, "");
            }
        }
    }
}

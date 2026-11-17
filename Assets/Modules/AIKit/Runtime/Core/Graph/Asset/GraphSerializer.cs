using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A BehaviorGraph to a graph asset and back. Every node and arrow becomes a record: its stable type id
    /// ([AIKitNode] — a class rename keeps the asset), its own fields as JSON, its links (GraphFields) as ids. Reading
    /// builds the elements, sets the links, adds the declared variables and links the graph once. What cannot be read
    /// is reported as a GraphIssue that names the record, never thrown. A graph made the old way (reflexes, nested nodes,
    /// conditions on arrows or on entering a node, arrows by a signal or a change) is reported as OldModel: those parts
    /// are not read, and the editor opens such an asset for viewing only.
    /// </summary>
    public static class GraphSerializer
    {
        /// <summary>The type id of an edge's record.</summary>
        public const string TRANSITION = "aikit.transition";

        /// <summary>
        /// Writes the graph into the asset, replacing its records. The look of the records that stay is kept (step 90: a
        /// builder's sub-menu over a user's graph no longer drops it): positions, folds and the bend points of the data
        /// wires into nodes by the node's id; the bend points, links and Brain-frame exits of arrows by what the arrow is
        /// (its node, kind, output and target — an arrow's record id is given anew on every write). A fresh asset gets a
        /// first layout. A node without [AIKitNode] (a code-only lambda node) cannot be saved: InvalidOperationException
        /// names it.
        /// </summary>
        public static void Write(BehaviorGraph graph, BehaviorGraphAsset asset)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            graph.AssignIds();
            var positions = new Dictionary<int, Vector2>();
            var folded = new HashSet<int>();
            var bends = new Dictionary<int, List<Vector2>>();    // the data wires into a node, bent: by the node's id
            foreach (GraphRecord old in asset.Records)
            {
                positions[old.Id] = old.Position;
                if (old.Folded)
                    folded.Add(old.Id);
                if (old.Type != TRANSITION && old.Waypoints is { Count: > 0 })
                    bends[old.Id] = old.Waypoints;
            }

            Dictionary<string, GraphRecord> arrows = ArrowLooks(asset);
            var writer = new Writer(asset, graph);
            asset.Records.Clear();
            asset.Objects.Clear();
            asset.Reflexes.Clear();
            asset.RootId = writer.IdOf(graph.Root);

            writer.Flush();
            foreach (GraphRecord record in asset.Records)
            {
                if (positions.TryGetValue(record.Id, out Vector2 position))
                    record.Position = position;
                record.Folded = folded.Contains(record.Id);
                if (record.Type != TRANSITION && bends.TryGetValue(record.Id, out List<Vector2> points))
                    record.Waypoints = new List<Vector2>(points);
            }

            // A node an upgrade step added stands next to the node it belongs to, never at (0, 0) (step 90)
            graph.VisitNodes(node =>
            {
                if (!positions.ContainsKey(node.Id) && UpgradePlaces.TryPlaceOf(asset, node, out Vector2 placed) &&
                    asset.Find(node.Id) is { } added)
                    added.Position = placed;
            });

            KeepArrowLooks(asset, graph, writer, arrows);
            if (positions.Count == 0)
                Layout(asset, graph);

            asset.Variables.Clear();
            foreach (VariableDeclaration declaration in graph.Variables)
                asset.Variables.Add(JsonUtility.FromJson<VariableDeclaration>(JsonUtility.ToJson(declaration)));

            asset.FormatVersion = GraphUpgrades.Current;
            asset.MarkChanged();
        }

        /// <summary>Reads the asset into a new graph; null when there is no root to start from.</summary>
        public static BehaviorGraph Read(BehaviorGraphAsset asset, List<GraphIssue> issues)
        {
            issues ??= new List<GraphIssue>();
            var elements = new Dictionary<int, object>();
            var owners = new Dictionary<int, GraphRecord>();        // the record each element was made of
            var dropped = new HashSet<int>();
            foreach (GraphRecord record in asset.Records)
            {
                object element = Create(record, issues);
                if (element == null)
                    continue;

                if (element is Transition { IsKnownTrigger: false } old)
                {
                    string what = (int)old.Trigger == 2 ? "a signal" : (int)old.Trigger == 3 ? "a change of a variable" : "something gone";
                    issues.Add(new GraphIssue(GraphIssueCode.OldModel, true, record.Id, record.Type,
                        $"an arrow of the old model (it fired on {what}): not read"));
                    dropped.Add(record.Id);
                    continue;
                }

                if (elements.ContainsKey(record.Id))
                {
                    issues.Add(new GraphIssue(GraphIssueCode.DanglingEdge, true, record.Id, record.Type,
                        $"two records share id {record.Id}"));
                    continue;
                }

                elements[record.Id] = element;
                owners[record.Id] = record;
            }

            // The links of the records read — a second record of an id (not read) puts no links on the first one's element
            foreach (GraphRecord record in asset.Records)
            {
                if (elements.TryGetValue(record.Id, out object element) && owners.TryGetValue(record.Id, out GraphRecord owner) &&
                    ReferenceEquals(owner, record))
                    SetLinks(asset, record, element, elements, dropped, issues);
            }

            // A block's output data pins (step 91) know their block: the links were just set on both
            foreach (object element in elements.Values)
                (element as IHasOutputs)?.BindOutputs();

            // A node that holds nodes is the old model's branch: the graph is flat now — what is under it is not read
            foreach (object element in elements.Values)
            {
                if (element is AgentState branch && branch.Children.Count > 0 && branch.Id != asset.RootId)
                {
                    issues.Add(new GraphIssue(GraphIssueCode.OldModel, true, branch.Id, "aikit.state",
                        $"'{branch.DisplayName}' holds nodes of its own (the old model's nested nodes): they are not read"));
                    branch.Children.Clear();
                }
            }

            if (!elements.TryGetValue(asset.RootId, out object rootElement) || rootElement is not AgentState root)
            {
                issues.Add(new GraphIssue(GraphIssueCode.MissingRoot, true, asset.RootId, null,
                    "the graph has no root state"));
                return null;
            }

            if (asset.Reflexes.Count > 0)
                issues.Add(new GraphIssue(GraphIssueCode.OldModel, true, asset.RootId, null,
                    $"{asset.Reflexes.Count} reflex arrow(s) of the old model (a sensor's Detected / Lost, From anywhere): " +
                    "a sensor only writes its variable now — the Brain decides"));

            var graph = new BehaviorGraph(root);
            foreach (object element in elements.Values)
            {
                if (element is AgentState state)
                {
                    for (int i = 0; i < state.Transitions.Count; i++)
                    {
                        state.Transitions[i].Order = i;
                        state.Transitions[i].TargetId = state.Transitions[i].Target?.Id ?? 0;
                    }
                }
            }

            foreach (VariableDeclaration declaration in asset.Variables)
                graph.Variables.Add(declaration);

            // An older format is brought to the current picture before the graph is linked (step 90)
            asset.UpgradedOnRead = GraphUpgrades.Apply(asset, graph, elements, issues);

            graph.Link();
            return graph;
        }

        private static object Create(GraphRecord record, List<GraphIssue> issues)
        {
            object element;
            if (record.Type == TRANSITION)
            {
                element = new Transition();
            }
            else
            {
                element = AIKitNodeRegistry.Create(record.Type);
                if (element == null)
                {
                    issues.Add(new GraphIssue(GraphIssueCode.UnknownNodeType, true, record.Id, record.Type,
                        $"no node type has the id '{record.Type}' (renamed without keeping its [AIKitNode] id?)"));
                    return null;
                }
            }

            if (!string.IsNullOrEmpty(record.Data))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(GraphFields.RenameOldKeys(element.GetType(), record.Data), element);
                }
                catch (ArgumentException exception)
                {
                    // Not thrown on: the rest of the asset is read, and the record is named so it can be fixed
                    issues.Add(new GraphIssue(GraphIssueCode.BadData, true, record.Id, record.Type,
                        $"the record's data cannot be read ({exception.Message}): it is not read — fix its Data in the " +
                        "asset's inspector"));
                    return null;
                }
            }

            if (element is AgentNode node)
                node.Id = record.Id;

            return element;
        }

        // Link fields of the old model: conditions on entering a node and on an arrow
        private static readonly HashSet<string> OldLinks = new() { "EnterConditions", "Conditions" };

        private static void SetLinks(BehaviorGraphAsset asset, GraphRecord record, object element,
            Dictionary<int, object> elements, HashSet<int> dropped, List<GraphIssue> issues)
        {
            foreach (GraphLink link in record.Links)
            {
                GraphFields.Link field = GraphFields.Find(element.GetType(), link.Field);
                if (field == null && OldLinks.Contains(link.Field) && element is AgentState or Transition)
                {
                    string where = element is Transition ? "on an arrow" : "on entering it";
                    issues.Add(new GraphIssue(GraphIssueCode.OldModel, true, record.Id, record.Type,
                        $"{NameOf(element)} has conditions {where} (the old model): not read"));
                    continue;
                }

                if (field == null)
                {
                    issues.Add(new GraphIssue(GraphIssueCode.UnknownField, true, record.Id, record.Type,
                        $"{NameOf(element)} has no link field '{link.Field}' (renamed without [FormerlySerializedAs]?)"));
                    continue;
                }

                if (field.IsObject)
                {
                    UnityEngine.Object value = link.Object >= 0 && link.Object < asset.Objects.Count
                        ? asset.Objects[link.Object]
                        : null;
                    if (value != null && field.ElementType.IsInstanceOfType(value))
                        field.Field.SetValue(element, value);
                    continue;
                }

                IList list = field.IsList ? (IList)Activator.CreateInstance(field.Field.FieldType) : null;
                foreach (int id in link.Ids)
                {
                    if (dropped.Contains(id))
                        continue;                       // an arrow of the old model: reported where it was read

                    if (!elements.TryGetValue(id, out object target) || !field.ElementType.IsInstanceOfType(target))
                    {
                        issues.Add(new GraphIssue(GraphIssueCode.DanglingEdge, true, record.Id, record.Type,
                            $"{NameOf(element)}.{field.Name} points to {id}, which is not a {field.ElementType.Name}"));
                        continue;
                    }

                    if (list != null)
                        list.Add(target);
                    else
                        field.Field.SetValue(element, target);
                }

                if (list != null)
                    field.Field.SetValue(element, list);
            }
        }

        private static string NameOf(object element) =>
            element is AgentNode node ? $"'{node.DisplayName}'" : element.ToString();

        /// <summary>Type id of a node ([AIKitNode]); null — the node is code-only.</summary>
        public static string TypeIdOf(object element)
        {
            if (element is Transition)
                return TRANSITION;

            var info = (AIKitNodeAttribute)Attribute.GetCustomAttribute(element.GetType(), typeof(AIKitNodeAttribute));
            return info == null || info.Category == AIKitNodeAttribute.CODE ? null : info.Id;   // a lambda is not data
        }

        // A first layout for a graph written from code: the sensors and the Brain on the left, a decision per row, its
        // chain of blocks to its right in the order the arrows lead; what no chain reaches — below
        private static void Layout(BehaviorGraphAsset asset, BehaviorGraph graph)
        {
            if (graph.Root == null)
                return;

            const float COLUMN = 380f;
            const float ROW = 260f;
            var depth = new Dictionary<AgentState, int>();
            var rows = new Dictionary<AgentState, int>();
            int row = 0;
            if (graph.Brain != null)
            {
                foreach (Transition wire in graph.Brain.Transitions)
                {
                    AgentState decision = wire.Target;
                    if (decision == null || rows.ContainsKey(decision))
                        continue;

                    rows[decision] = row++;
                    depth[decision] = 1;
                    var queue = new Queue<AgentState>();
                    queue.Enqueue(decision);
                    while (queue.Count > 0)
                    {
                        AgentState node = queue.Dequeue();
                        bool first = true;
                        foreach (Transition arrow in node.Transitions)
                        {
                            AgentState next = arrow.Target;
                            if (next == null || next == graph.Brain || next.Reasoner != null || depth.ContainsKey(next))
                                continue;

                            // The first block a node leads to goes on in its row; the others (Do and On Tick, Done and
                            // Failed, a Random's outputs) each get a row of their own below — never one place for two
                            depth[next] = depth[node] + 1;
                            rows[next] = first ? rows[node] : row++;
                            first = false;
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            foreach (AgentState state in graph.Root.Children)
            {
                GraphRecord record = asset.Find(state.Id);
                if (record == null)
                    continue;

                if (state == graph.Brain)
                    record.Position = Vector2.zero;
                else if (depth.TryGetValue(state, out int column))
                    record.Position = new Vector2(column * COLUMN, rows[state] * ROW);
                else
                    record.Position = new Vector2(COLUMN, row++ * ROW);
            }

            for (int i = 0; i < graph.Root.Tasks.Count; i++)
            {
                GraphRecord record = asset.Find(graph.Root.Tasks[i].Id);
                if (record != null)
                    record.Position = new Vector2(-COLUMN, i * ROW);
            }

            // An arrow back across the rows (a chain that loops) ends at a link at its pin, not in a long wire
            GraphLinks.LinkLoops(asset, graph);
        }

        // ---------- the look of the arrows, kept across writes ----------

        private const string STATE = "aikit.state";

        // What an arrow is, the same in the asset and in the graph: its node, its kind, its output, its target, and which
        // of the arrows alike it is (two arrows from one pin into one node)
        private static string ArrowKey(int source, TransitionTrigger trigger, int output, int target, int occurrence) =>
            $"{source}/{(int)trigger}/{output}/{target}#{occurrence}";

        // The records of the asset's arrows that carry a look of their own (bend points, a link, a Brain wire's exit), by
        // what the arrow is
        private static Dictionary<string, GraphRecord> ArrowLooks(BehaviorGraphAsset asset)
        {
            var looks = new Dictionary<string, GraphRecord>();
            var seen = new Dictionary<string, int>();
            foreach (GraphRecord node in asset.Records)
            {
                if (node.Type != STATE)
                    continue;

                GraphLink edges = node.Links.Find(link => link.Field == nameof(AgentState.Transitions));
                if (edges == null)
                    continue;

                foreach (int id in edges.Ids)
                {
                    GraphRecord edge = asset.Find(id);
                    if (edge == null || edge.Type != TRANSITION)
                        continue;

                    var arrow = new Transition();
                    if (!string.IsNullOrEmpty(edge.Data))
                        JsonUtility.FromJsonOverwrite(edge.Data, arrow);
                    GraphLink to = edge.Links.Find(link => link.Field == nameof(Transition.Target));
                    int target = to != null && to.Ids.Count > 0 ? to.Ids[0] : arrow.TargetId;
                    string alike = ArrowKey(node.Id, arrow.Trigger, arrow.Output, target, 0);
                    seen.TryGetValue(alike, out int occurrence);
                    seen[alike] = occurrence + 1;

                    if (edge.IsLink || edge.Waypoints is { Count: > 0 } || edge.ExitSide != FrameSide.None)
                        looks[ArrowKey(node.Id, arrow.Trigger, arrow.Output, target, occurrence)] = edge;
                }
            }

            return looks;
        }

        // The arrows just written take the look their records had before
        private static void KeepArrowLooks(BehaviorGraphAsset asset, BehaviorGraph graph, Writer writer,
            Dictionary<string, GraphRecord> looks)
        {
            if (looks.Count == 0)
                return;

            var states = new List<AgentState>();
            graph.CollectStates(states);
            var seen = new Dictionary<string, int>();
            foreach (AgentState state in states)
            {
                foreach (Transition arrow in state.Transitions)
                {
                    int target = arrow.Target?.Id ?? 0;
                    string alike = ArrowKey(state.Id, arrow.Trigger, arrow.Output, target, 0);
                    seen.TryGetValue(alike, out int occurrence);
                    seen[alike] = occurrence + 1;

                    if (!looks.TryGetValue(ArrowKey(state.Id, arrow.Trigger, arrow.Output, target, occurrence),
                            out GraphRecord old))
                        continue;

                    GraphRecord written = asset.Find(writer.IdOf(arrow));
                    if (written == null)
                        continue;

                    written.Waypoints = new List<Vector2>(old.Waypoints ?? new List<Vector2>());
                    written.IsLink = old.IsLink;
                    written.LinkFree = old.LinkFree;
                    written.LinkPosition = old.LinkPosition;
                    written.ExitSide = old.ExitSide;
                    written.ExitAlong = old.ExitAlong;
                }
            }
        }

        private sealed class Writer
        {
            private readonly BehaviorGraphAsset _asset;
            private readonly Dictionary<object, int> _ids = new(ReferenceComparer.Instance);
            private readonly HashSet<int> _taken = new();
            private readonly Queue<object> _pending = new();
            private int _nextEdgeId;

            public Writer(BehaviorGraphAsset asset, BehaviorGraph graph)
            {
                _asset = asset;
                int max = 0;
                graph.VisitNodes(node => max = Math.Max(max, node.Id));
                _nextEdgeId = max + 1000;
            }

            public int IdOf(object element)
            {
                if (element == null)
                    return 0;

                if (_ids.TryGetValue(element, out int id))
                    return id;

                // A node keeps its id; one that collides (an element outside the tree numbered apart) gets a fresh one
                AgentNode node = element as AgentNode;
                id = node != null && node.Id != 0 && !_taken.Contains(node.Id) ? node.Id : _nextEdgeId++;
                while (!_taken.Add(id))
                    id = _nextEdgeId++;
                if (node != null)
                    node.Id = id;
                _ids[element] = id;
                _pending.Enqueue(element);
                return id;
            }

            public void Flush()
            {
                while (_pending.Count > 0)
                {
                    object element = _pending.Dequeue();
                    string type = TypeIdOf(element);
                    if (type == null)
                    {
                        throw new InvalidOperationException(
                            $"'{(element as AgentNode)?.DisplayName}' ({element.GetType().Name}) has no [AIKitNode] " +
                            "id and cannot be saved in a graph asset (a code-only node: write it as a node class)");
                    }

                    var record = new GraphRecord { Id = _ids[element], Type = type, Data = JsonUtility.ToJson(element) };
                    foreach (GraphFields.Link field in GraphFields.LinksOf(element.GetType()))
                    {
                        var link = new GraphLink { Field = field.Name };
                        if (field.IsObject)
                        {
                            var value = field.Field.GetValue(element) as UnityEngine.Object;
                            if (value == null)
                                continue;

                            link.Object = _asset.Objects.IndexOf(value);
                            if (link.Object < 0)
                            {
                                _asset.Objects.Add(value);
                                link.Object = _asset.Objects.Count - 1;
                            }
                        }
                        else
                        {
                            foreach (object value in field.Values(element))
                                link.Ids.Add(IdOf(value));

                            if (link.Ids.Count == 0)
                                continue;
                        }

                        record.Links.Add(link);
                    }

                    _asset.Records.Add(record);
                }
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();

            public new bool Equals(object a, object b) => ReferenceEquals(a, b);

            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// The graph being edited — the layer between the window and the asset (the canvas never touches records): the
    /// asset read into live elements, the positions of states, and every edit as one operation that records Undo and
    /// writes the asset back. A canvas (GraphView today, Graph Toolkit tomorrow) only shows a document and calls these.
    /// </summary>
    public sealed class GraphDocument
    {
        private readonly Dictionary<AgentState, Vector2> _positions = new();

        public GraphDocument(BehaviorGraphAsset asset)
        {
            Asset = asset;
            Reload();
        }

        public BehaviorGraphAsset Asset { get; }

        /// <summary>The live elements being edited (not the compiled graph agents run).</summary>
        public BehaviorGraph Graph { get; private set; }

        public List<GraphIssue> Issues { get; } = new();

        public event Action Changed;

        public void Reload()
        {
            Issues.Clear();
            Graph = GraphSerializer.Read(Asset, Issues);
            if (Graph == null)
            {
                var root = new AgentState("Root");
                Graph = new BehaviorGraph(root);
            }

            _positions.Clear();
            _folded.Clear();
            _waiting.Clear();
            var states = new List<AgentState>();
            Graph.CollectStates(states);
            foreach (AgentState state in states)
            {
                GraphRecord record = Asset.Find(state.Id);
                // A node an upgrade step added has no record yet: it stands beside the node it belongs to (step 90)
                _positions[state] = record != null ? record.Position
                    : UpgradePlaces.TryPlaceOf(Asset, state, out Vector2 placed) ? placed : Vector2.zero;
                if (record is { Folded: true })
                    _folded.Add(state);
            }

            LoadWaypoints();
            GraphValidator.Check(Asset, Graph, Issues);
            CheckKeys();

            // Records this code cannot read (or a part of the old model) would be dropped by the next write: the graph is
            // shown, not edited
            IsReadOnly = Asset.Records.Count > 0 && Issues.Exists(issue =>
                issue.Code == GraphIssueCode.UnknownNodeType || issue.Code == GraphIssueCode.UnknownField ||
                issue.Code == GraphIssueCode.MissingRoot || issue.Code == GraphIssueCode.OldModel ||
                issue.Code == GraphIssueCode.BadData);
            _warned = false;

            // An older format was brought to the current picture when read, or a step that needs the editor (an asset
            // found by name) brings it now: the upgraded graph is saved, once (step 90)
            bool upgraded = Asset.UpgradedOnRead | EditorGraphUpgrades.Apply(Asset, Graph, Issues);

            // What the steps said (a node replaced, a note of the format) is said only by this read — the saved graph is
            // of the current format and no step runs on it again: the notes stay on the graph while it is open, and go to
            // the console once (handbook §29а.3: an explicit migration and a log entry)
            _upgradeNotes.Clear();
            if (upgraded)
            {
                foreach (GraphIssue issue in Issues)
                {
                    if (IsUpgradeNote(issue))
                        _upgradeNotes.Add(issue);
                }
            }

            // A node is called by its type (the project's rule): an older graph's own names go, once, when it is opened
            if ((NodeTitles.NameByType(Graph) | upgraded) && !IsReadOnly && Asset.Records.Count > 0)
            {
                Save(false);
                foreach (GraphIssue note in _upgradeNotes)
                    Debug.LogWarning($"{Asset.name}: upgraded to the current format — {note}", Asset);
            }
        }

        // The notes of the upgrade steps of this document's last read (kept across its saves)
        private readonly List<GraphIssue> _upgradeNotes = new();

        /// <summary>A note an upgrade step leaves on the graph it brought to the current picture.</summary>
        public static bool IsUpgradeNote(GraphIssue issue) =>
            issue.Code == GraphIssueCode.ReplacedNode || issue.Code == GraphIssueCode.OldFormat;

        /// <summary>
        /// The asset has records this code cannot read (a node type that is gone, a field renamed without its old name, a
        /// part of the old model — reflexes, nested nodes, conditions on arrows): any edit would write the graph back without them, so the window shows it and changes nothing. Bring the node
        /// types back (or fix the asset in its inspector), and it opens for editing again.
        /// </summary>
        public bool IsReadOnly { get; private set; }

        private bool _warned;

        private bool RefuseReadOnly(string what)
        {
            if (!IsReadOnly)
                return false;

            if (!_warned)
            {
                _warned = true;
                Debug.LogWarning($"{Asset.name}: the graph has nodes this version of the module cannot read — it is opened read-only, " +
                                 $"so they are not lost ('{what}' was not done). See the graph's problems.", Asset);
            }

            return true;
        }

        // ---------- wire points: where an arrow (or a data wire) is bent on the canvas ----------

        // By the owner of the wire: a Transition for an arrow, the task a data wire comes into
        private readonly Dictionary<object, List<Vector2>> _waypoints = new();

        private static readonly Vector2[] NoWaypoints = new Vector2[0];

        /// <summary>The points the wire is bent through (a Transition, or the task a data wire comes into).</summary>
        public IReadOnlyList<Vector2> WaypointsOf(object wire) =>
            wire != null && _waypoints.TryGetValue(wire, out List<Vector2> points) ? points : NoWaypoints;

        public void AddWaypoint(object wire, int index, Vector2 position)
        {
            if (wire == null)
                return;

            Undo.RecordObject(Asset, "Add Wire Point");
            if (!_waypoints.TryGetValue(wire, out List<Vector2> points))
            {
                points = new List<Vector2>();
                _waypoints[wire] = points;
            }

            points.Insert(Mathf.Clamp(index, 0, points.Count), position);
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        /// <summary>A point dragged on the canvas (no rebuild: the canvas already shows it there).</summary>
        public void MoveWaypoint(object wire, int index, Vector2 position)
        {
            if (wire == null || !_waypoints.TryGetValue(wire, out List<Vector2> points) || index < 0 || index >= points.Count)
                return;

            Undo.RecordObject(Asset, "Move Wire Point");
            points[index] = position;
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
        }

        public void RemoveWaypoint(object wire, int index)
        {
            if (wire == null || !_waypoints.TryGetValue(wire, out List<Vector2> points) || index < 0 || index >= points.Count)
                return;

            Undo.RecordObject(Asset, "Remove Wire Point");
            points.RemoveAt(index);
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        // ---------- links: an arrow that ends at a label with the name of its node, not at the node itself ----------

        // How an arrow drawn as a link stands: docked at its output pin (the default, step 90 — C2), or where it was
        // dragged to
        private struct LinkLook
        {
            public bool Free;
            public Vector2 Position;
        }

        private readonly Dictionary<Transition, LinkLook> _links = new();

        /// <summary>The arrow is drawn as a link: a short wire to a label "→ node" (false — a wire all the way).</summary>
        public bool IsLink(Transition transition) => transition != null && _links.ContainsKey(transition);

        /// <summary>The arrow's link label stands at its output pin, under the other labels of that pin (not dragged away).</summary>
        public bool IsDocked(Transition transition) =>
            transition != null && _links.TryGetValue(transition, out LinkLook look) && !look.Free;

        /// <summary>
        /// The arrow is drawn as a link (false — a wire all the way). position — where a label dragged off its pin stands;
        /// a docked one stands at its pin (IsDocked) and position says nothing.
        /// </summary>
        public bool TryLinkOf(Transition transition, out Vector2 position)
        {
            position = default;
            if (transition == null || !_links.TryGetValue(transition, out LinkLook look))
                return false;

            position = look.Position;
            return true;
        }

        /// <summary>
        /// Draws the arrow as a link docked at its output pin ("Replace With Link"): its bend points go — they led to the
        /// far node.
        /// </summary>
        public void SetLink(Transition transition)
        {
            if (transition == null)
                return;

            Undo.RecordObject(Asset, "Replace Wire With Link");
            Dock(transition);
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        // Docked at its pin, with no bend points
        private void Dock(Transition transition)
        {
            _links[transition] = new LinkLook();
            _waypoints.Remove(transition);
        }

        /// <summary>
        /// A link label dragged on the canvas: it leaves its pin and stands where it was put from now (no rebuild: the
        /// canvas already shows it there). "Dock To Pin" puts it back.
        /// </summary>
        public void MoveLink(Transition transition, Vector2 position)
        {
            if (transition == null || !_links.ContainsKey(transition))
                return;

            Undo.RecordObject(Asset, "Move Link");
            _links[transition] = new LinkLook { Free = true, Position = position };
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
        }

        /// <summary>A link label dragged off its pin goes back to stand at it ("Dock To Pin").</summary>
        public void DockLink(Transition transition)
        {
            if (transition == null || !_links.ContainsKey(transition) || IsDocked(transition))
                return;

            Undo.RecordObject(Asset, "Dock Link");
            _links[transition] = new LinkLook();
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        /// <summary>The arrow is a wire all the way to its node again.</summary>
        public void ClearLink(Transition transition)
        {
            if (transition == null || !_links.Remove(transition))
                return;

            Undo.RecordObject(Asset, "Show Link As Wire");
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        // An arrow wired by hand back across the canvas (to a node left of its node or level with it) is a link at its
        // pin at once — the rule the builders follow (GraphLinks.LinkLoops); the Brain's wires leave its frame instead
        private void LinkIfItRunsBack(AgentState from, Transition arrow)
        {
            if (from == null || arrow?.Target == null || from.IsBrain)
                return;

            if (GraphLinks.RunsBack(PositionOf(from), PositionOf(arrow.Target)))
                Dock(arrow);
        }

        // ---------- the Brain's wires: the point of its frame each one leaves (C10) ----------

        private readonly Dictionary<Transition, (FrameSide Side, float Along)> _exits = new();

        /// <summary>
        /// The point of the Brain's frame the wire was wired from by hand (a side, and how far along it: 0 — its start, 1 —
        /// its end); false — none: the wire leaves the side that faces its decision, spread with the others.
        /// </summary>
        public bool TryExitOf(Transition wire, out FrameSide side, out float along)
        {
            side = FrameSide.None;
            along = 0f;
            if (wire == null || !_exits.TryGetValue(wire, out (FrameSide Side, float Along) exit))
                return false;

            side = exit.Side;
            along = exit.Along;
            return true;
        }

        /// <summary>The Brain's wire leaves its frame at this point from now (where the user let it go; kept with the graph).</summary>
        public void SetExit(Transition wire, FrameSide side, float along)
        {
            if (wire == null)
                return;

            Undo.RecordObject(Asset, "Move Brain Wire");
            if (side == FrameSide.None)
                _exits.Remove(wire);
            else
                _exits[wire] = (side, Mathf.Clamp01(along));
            StoreWaypoints();
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        // Every wire owner of the graph with the record the asset keeps it in. An edge has no id of its own: its record
        // is found through the link of its owner (the n-th edge of a state is the n-th id of the "Transitions" link)
        private IEnumerable<(object Wire, GraphRecord Record)> Wires()
        {
            foreach (AgentState state in States())
            {
                GraphRecord record = Asset.Find(state.Id);
                GraphLink link = record?.Links.Find(candidate => candidate.Field == nameof(AgentState.Transitions));
                for (int i = 0; link != null && i < state.Transitions.Count && i < link.Ids.Count; i++)
                    yield return (state.Transitions[i], Asset.Find(link.Ids[i]));

                foreach (AgentTask task in state.Tasks)
                    yield return (task, Asset.Find(task.Id));
            }

        }

        private void LoadWaypoints()
        {
            _waypoints.Clear();
            _links.Clear();
            _exits.Clear();
            foreach ((object wire, GraphRecord record) in Wires())
            {
                if (record != null && record.Waypoints != null && record.Waypoints.Count > 0)
                    _waypoints[wire] = new List<Vector2>(record.Waypoints);
                if (record is not { } || wire is not Transition arrow)
                    continue;

                // An older graph's link stood where it was put: it docks at its pin now (LinkFree is false), the user's
                // word C2 — only a label dragged off its pin since step 90 is free
                if (record.IsLink)
                    _links[arrow] = new LinkLook { Free = record.LinkFree, Position = record.LinkPosition };
                if (record.ExitSide != FrameSide.None)
                    _exits[arrow] = (record.ExitSide, record.ExitAlong);
            }
        }

        // The records of the asset are written anew on every save: the points are put back onto them
        private void StoreWaypoints()
        {
            foreach ((object wire, GraphRecord record) in Wires())
            {
                if (record == null)
                    continue;

                record.Waypoints = _waypoints.TryGetValue(wire, out List<Vector2> points)
                    ? new List<Vector2>(points)
                    : new List<Vector2>();

                LinkLook look = default;
                record.IsLink = wire is Transition arrow && _links.TryGetValue(arrow, out look);
                record.LinkFree = record.IsLink && look.Free;
                record.LinkPosition = record.LinkFree ? look.Position : default;

                (FrameSide Side, float Along) exit = default;
                bool placed = wire is Transition brainWire && _exits.TryGetValue(brainWire, out exit);
                record.ExitSide = placed ? exit.Side : FrameSide.None;
                record.ExitAlong = placed ? exit.Along : 0f;
            }
        }

        public List<AgentState> States()
        {
            var states = new List<AgentState>();
            Graph.CollectStates(states);
            return states;
        }

        public Vector2 PositionOf(AgentState state) => _positions.TryGetValue(state, out Vector2 p) ? p : Vector2.zero;

        // The states whose output pins that lead nowhere are folded into the card
        private readonly HashSet<AgentState> _folded = new();

        // The nodes made since the canvas last took them to place, in the order they were made
        private readonly List<object> _waiting = new();

        /// <summary>
        /// The node made by the last edit (a state, a variable node, a condition, a sensor) — the canvas finds it a free
        /// place once it is drawn (nodes never land on each other); null — no new node waits for a place.
        /// </summary>
        public object JustAdded => _waiting.Count > 0 ? _waiting[^1] : null;

        /// <summary>
        /// Every node made since the canvas last took them to place, JustAdded the last of them: an edit may make two (the
        /// Branch a condition picked with nothing selected gets, the Detection the first shape gets) — each lands on no
        /// other node.
        /// </summary>
        public IReadOnlyList<object> Waiting => _waiting;

        /// <summary>The canvas has taken the new nodes to place: nothing waits for a place here any more.</summary>
        public void Placed() => _waiting.Clear();

        // A node the edit made waits for the canvas to find it a free place
        private void Wait(object made)
        {
            if (made != null && !_waiting.Contains(made))
                _waiting.Add(made);
        }

        /// <summary>The node's output pins that lead nowhere are folded into its card.</summary>
        public bool IsFolded(AgentState state) => state != null && _folded.Contains(state);

        /// <summary>Folds the node's free output pins into its card, or opens them (no rebuild: the canvas shows it).</summary>
        public void SetFolded(AgentState state, bool folded)
        {
            if (state == null || IsFolded(state) == folded)
                return;

            Undo.RecordObject(Asset, folded ? "Fold Pins" : "Open Pins");
            if (folded)
                _folded.Add(state);
            else
                _folded.Remove(state);

            GraphRecord record = Asset.Find(state.Id);
            if (record != null)
                record.Folded = folded;
            EditorUtility.SetDirty(Asset);
        }

        /// <summary>A state moved on the canvas (no Undo step of its own: positions follow the next save).</summary>
        public void Move(AgentState state, Vector2 position)
        {
            _positions[state] = position;
            GraphRecord record = Asset.Find(state.Id);
            if (record == null)
                return;

            record.Position = position;
            EditorUtility.SetDirty(Asset);
        }

        // ---------- edits ----------

        public AgentState AddState(AgentState parent, string name, Vector2 position)
        {
            AgentState state = null;
            Edit("Add State", () =>
            {
                state = new AgentState(name);
                (parent ?? Graph.Root).AddChild(state);
                _positions[state] = position;
            });
            return state;
        }

        /// <summary>A new state that does one thing: named after the task and holding it ("node = behavior").</summary>
        public AgentState AddStateWithTask(AgentState parent, string typeId, Vector2 position)
        {
            AgentState state = null;
            Edit("Add Node", () =>
            {
                if (AIKitNodeRegistry.Create(typeId) is not AgentTask task)
                    return;

                state = new AgentState(NodeTitles.Of(task));
                state.AddTask(task);
                (parent ?? Graph.Root).AddChild(state);
                _positions[state] = position;
                _folded.Add(state);                     // a new node's free output pins start folded into its card
                Wait(state);                            // the canvas finds it a free place once it is drawn
            });
            JoinActiveSubgraph(state);
            return state;
        }

        public void AddTask(AgentState state, string typeId) => Edit("Add Task", () =>
        {
            if (AIKitNodeRegistry.Create(typeId) is AgentTask task)
                state.AddTask(task);
        });

        /// <summary>
        /// The user's label of a node (step 90, C4: "по умолчанию — тип; пользователь может подписать ноду руками"),
        /// trimmed; empty, or the node's type title, — the node is called by its type again. Shown wherever the node is
        /// named. False — nothing changed.
        /// </summary>
        public bool RenameNode(AgentNode node, string label)
        {
            if (node == null)
                return false;

            label = label?.Trim() ?? "";
            string type = node is AgentState state ? NodeTitles.Of(state) : NodeTitles.Of(node);
            if (label == type)
                label = "";
            if (label == (node.Label ?? ""))
                return false;

            Edit(label.Length == 0 ? "Name By Type" : "Rename Node", () => node.Label = label);
            return true;
        }

        /// <summary>The node's label goes: it is called by its type again ("Name By Type").</summary>
        public bool NameByType(AgentNode node) => RenameNode(node, "");

        /// <summary>
        /// The pin a node takes its condition through, by the field's name: a Branch's question, a decision's extra
        /// requirement (ReasonerTask.Condition, step 90) — any single link field of a condition. Null — the first such
        /// field of the node; none — the node asks nothing.
        /// </summary>
        public static GraphFields.Link ConditionLinkOf(AgentNode asker, string field = null)
        {
            if (asker == null)
                return null;

            foreach (GraphFields.Link link in ValueFields.ConditionLinks(asker))
            {
                if (!link.IsList && (field == null || link.Answers(field)))
                    return link;
            }

            return null;
        }

        /// <summary>
        /// One more condition node for an asker: a node with a Condition pin — a Branch, a decision (its question; a second
        /// one makes an AND of the two, whatever the first is — an OR too: both must hold) — or an AND / OR node (one more
        /// input). field — which Condition pin of the asker (null — its first).
        /// </summary>
        public AgentCondition AddConditionTo(object owner, string typeId, Vector2 position, string target = null,
            string field = null)
        {
            if (AIKitNodeRegistry.Create(typeId) is not AgentCondition condition)
                return null;

            foreach (System.Reflection.FieldInfo objectField in ElementInspector.ObjectFields(condition))
            {
                if (!string.IsNullOrEmpty(target) && string.IsNullOrEmpty((string)objectField.GetValue(condition)))
                    objectField.SetValue(condition, target);
            }

            return JoinCondition(owner, condition, position, field);
        }

        /// <summary>
        /// A condition node made elsewhere (a state of the Character section: an Is) joined to what asks it, the same way
        /// AddConditionTo joins one: into an empty Condition pin, ANDed with the question there, or one more input of an
        /// AND / OR node. Null — the owner asks nothing (nothing changes).
        /// </summary>
        public AgentCondition JoinCondition(object owner, AgentCondition condition, Vector2 position, string field = null)
        {
            if (condition == null || RefuseReadOnly("Add Condition"))
                return null;

            List<AgentCondition> inputs = owner is AgentCondition joining ? InputsOf(joining) : null;
            GraphFields.Link pin = inputs == null ? ConditionLinkOf(owner as AgentNode, field) : null;
            if (inputs == null && pin == null)
                return null;

            AgentCondition wrapper = null;
            Edit("Add Condition", () =>
            {
                if (inputs != null)
                {
                    inputs.Add(condition);              // AND / OR: one more input
                    return;
                }

                switch (pin.Field.GetValue(owner))
                {
                    case null:
                        pin.Field.SetValue(owner, condition);
                        break;
                    case AllCondition all:
                        all.Conditions.Add(condition);
                        break;
                    case AgentCondition asked:
                        // An OR is not widened by one more alternative: what was asked and the new question must both
                        // hold (CharacterSection and the attack-slot upgrade join the same way)
                        wrapper = new AllCondition(asked, condition);
                        pin.Field.SetValue(owner, wrapper);
                        break;
                }
            }, notify: false);

            MoveNode(condition, position);
            if (wrapper != null)
                MoveNode(wrapper, position + new Vector2(170f, 60f));

            Wait(condition);
            Changed?.Invoke();
            return condition;
        }

        /// <summary>A condition node is taken away from wherever it is wired in (with everything wired into it).</summary>
        public void RemoveCondition(AgentCondition condition) => Edit("Remove Condition", () => Unhold(condition));

        // The node is let go by everything that holds it: a field of a node (a Branch's or a decision's Condition, a value
        // pin), a list of one (the inputs of AND / OR, the shapes of Detection), the root's lists (a block, a sensor, a
        // value node nobody reads)
        private void Unhold(AgentNode node)
        {
            if (node == null)
                return;

            var holders = new List<AgentNode>();
            Graph.VisitNodes(holders.Add);
            foreach (AgentNode holder in holders)
            {
                foreach (GraphFields.Link link in GraphFields.LinksOf(holder.GetType()))
                {
                    if (link.IsObject || !link.ElementType.IsInstanceOfType(node) || link.Holds(typeof(AgentState)))
                        continue;

                    if (!link.IsList)
                    {
                        if (link.Field.GetValue(holder) == node)
                            link.Field.SetValue(holder, null);
                    }
                    else if (link.Field.GetValue(holder) is System.Collections.IList list)
                    {
                        list.Remove(node);
                    }
                }
            }

            foreach (AgentState state in States())
                state.Tasks.Remove(node as AgentTask);
            Graph.Root.Values.Remove(node as AgentValue);
        }

        /// <summary>What is wired into an AND / OR node (null — the condition is not one of them).</summary>
        public static List<AgentCondition> InputsOf(AgentCondition condition) => condition switch
        {
            AllCondition all => all.Conditions,
            AnyCondition any => any.Conditions,
            _ => null
        };

        /// <summary>The object the graph's nodes work with most often: what a new question is asked about.</summary>
        public string CommonTarget()
        {
            foreach (string variable in SensorVariables())
                return variable;

            return "";
        }

        /// <summary>
        /// The node of the variable the wire into the task's pin was drawn from; null — no wire was drawn (the field
        /// names its variable, and nothing is shown across the canvas).
        /// </summary>
        public GraphVariableNode WiredNode(AgentNode task, string variable) =>
            Asset.VariableNodes.Find(node => node.Variable == variable && node.Readers.Contains(task.Id));

        /// <summary>
        /// The group of places the graph's Take Attack Slot node asks from for this object (the first such node; any
        /// of them when none names the object); empty — the common one.
        /// </summary>
        public string SlotPoolOf(string target)
        {
            string any = "";
            foreach (AgentState state in Graph.Root.Children)
            {
                foreach (AgentTask task in state.Tasks)
                {
                    if (task is not TakeAttackSlotTask slot)
                        continue;

                    if (slot.Target == target)
                        return slot.Pool ?? "";

                    if (any.Length == 0)
                        any = slot.Pool ?? "";
                }
            }

            return any;
        }

        /// <summary>An arrow from a node to a node: the Brain's wire to a decision, a decision's Do, a block's output.</summary>
        public Transition Connect(AgentState from, AgentState to,
            TransitionTrigger trigger = TransitionTrigger.Always)
        {
            if (from == null || to == null)
                return null;

            Transition edge = null;
            Edit("Add Edge", () =>
            {
                edge = new Transition { Target = to, Trigger = trigger };
                from.AddTransition(edge);
                LinkIfItRunsBack(from, edge);
            });
            return edge;
        }

        // ---------- sensors: background tasks of the root that write what they see into a variable ----------

        public List<SensorTask> Sensors()
        {
            var sensors = new List<SensorTask>();
            foreach (AgentTask task in Graph.Root.Tasks)
            {
                if (task is SensorTask sensor)
                    sensors.Add(sensor);
            }

            return sensors;
        }

        /// <summary>The names of the variables sensors write: a node may take one as its target.</summary>
        public List<string> SensorVariables()
        {
            var names = new List<string>();
            foreach (SensorTask sensor in Sensors())
            {
                if (!string.IsNullOrEmpty(sensor.StoreIn))
                    names.Add(sensor.StoreIn);
            }

            return names;
        }

        /// <summary>The Detection of the graph (null — none yet).</summary>
        public DetectionTask Detection()
        {
            foreach (SensorTask sensor in Sensors())
            {
                if (sensor is DetectionTask detection)
                    return detection;
            }

            return null;
        }

        /// <summary>
        /// One more sensor wired into Detection (the given one, or the graph's own; with none, a Detection is made
        /// next to the sensor). The sensor only says what "sees" means; Detection looks and writes the variable.
        /// </summary>
        public SensorTask AddShape(string typeId, Vector2 position, DetectionTask into = null)
        {
            // A graph opened for viewing takes nothing, not even in memory (the canvas would draw what the asset has not)
            if (AIKitNodeRegistry.Create(typeId) is not SensorTask shape || shape is DetectionTask || RefuseReadOnly("Add Sensor"))
                return null;

            Undo.RecordObject(Asset, "Add Sensor");
            DetectionTask detection = into ?? Detection();
            bool made = detection == null;
            if (made)
            {
                detection = new DetectionTask { StoreIn = "" };     // writes nowhere until given a variable
                Graph.Root.AddTask(detection);
            }

            detection.Sensors.Add(shape);
            Save(notify: false);
            if (made)
            {
                MoveNode(detection, position + new Vector2(280f, 0f));
                Wait(detection);                                    // made with its first shape: it lands on no node either
            }

            MoveNode(shape, position);
            Wait(shape);
            Changed?.Invoke();
            return shape;
        }

        /// <summary>The sensor is taken out of Detection.</summary>
        public void RemoveShape(DetectionTask detection, SensorTask shape) =>
            Edit("Remove Sensor", () => detection?.Sensors.Remove(shape));

        /// <summary>A new sensor of the type saved under this id (a node of the "Sensor" category).</summary>
        public SensorTask AddSensor(string typeId, Vector2 position)
        {
            if (AIKitNodeRegistry.Create(typeId) is not SensorTask sensor || RefuseReadOnly("Add Sensor"))
                return null;

            Undo.RecordObject(Asset, "Add Sensor");
            // A new sensor fills nothing: the variable it writes is given to it from the blackboard, by hand
            sensor.StoreIn = "";
            Graph.Root.AddTask(sensor);
            Save(notify: false);
            MoveNode(sensor, position);
            Wait(sensor);
            Changed?.Invoke();
            return sensor;
        }

        public void RemoveSensor(SensorTask sensor) => Edit("Remove Sensor", () =>
        {
            Graph.Root.Tasks.Remove(sensor);        // its variable stays: variables belong to the blackboard
        });

        /// <summary>
        /// The sensor writes what it sees into this variable of the blackboard from now (empty — into none). The
        /// arrow of the sensor comes into the given node of the variable.
        /// </summary>
        public void SetSensorVariable(SensorTask sensor, string variable, GraphVariableNode into = null)
        {
            if (sensor == null)
                return;

            Edit("Sensor Variable", () =>
            {
                sensor.StoreIn = variable ?? "";
                foreach (GraphVariableNode node in Asset.VariableNodes)
                {
                    if (into != null && node.Variable == sensor.StoreIn)
                        node.Filled = node == into;
                }
            });
        }

        // ---------- variable nodes: a sensor's variable drawn as small nodes next to the nodes that use it ----------

        public IReadOnlyList<GraphVariableNode> VariableNodes => Asset.VariableNodes;

        /// <summary>
        /// Every sensor's variable has at least one node on the canvas (a new one appears next to its sensor), and
        /// nodes of variables that are gone are dropped. Called before the canvas draws.
        /// </summary>
        public void EnsureVariableNodes()
        {
            if (IsReadOnly)
                return;                             // nothing is touched in an asset that did not read whole

            List<string> variables = SensorVariables();
            foreach (VariableDeclaration declared in Graph.Variables)
                variables.Add(declared.Name);           // a declared variable may have nodes on the canvas too
            foreach (VariableUse used in BlackboardUsage.Collect(this))
                variables.Add(used.Name);               // and one a node keeps on the blackboard ("Target Distance")
            foreach (VariableUse own in BlackboardUsage.OfCharacter(ElementInspector.Character))
                variables.Add(own.Name);                // and a number of the character ("Grenade Cooldown"), wired or not

            bool changed = Asset.VariableNodes.RemoveAll(node => !variables.Contains(node.Variable)) > 0;

            // A node made earlier as if a sensor read its own variable: the sensor is not its reader (deleting that
            // node used to take the sensor's variable away; now it is a plain node of the variable, safe to delete). A
            // sensor or a shape of Detection that reads a variable through a pin of its own (a number bound to it) stays
            // that node's reader: the wire the user drew into its pin
            var sensorsById = new Dictionary<int, SensorTask>();
            foreach (SensorTask sensor in Sensors())
            {
                sensorsById[sensor.Id] = sensor;
                if (sensor is DetectionTask detection)
                    detection.Sensors.ForEach(shape => sensorsById[shape.Id] = shape);
            }

            foreach (GraphVariableNode node in Asset.VariableNodes)
            {
                changed |= node.Readers.RemoveAll(id => sensorsById.TryGetValue(id, out SensorTask sensor) &&
                                                        !ReadsThroughPin(sensor, node.Variable)) > 0;
            }
            foreach (SensorTask sensor in Sensors())
            {
                if (string.IsNullOrEmpty(sensor.StoreIn) || Asset.VariableNodes.Exists(node => node.Variable == sensor.StoreIn))
                    continue;

                Asset.VariableNodes.Add(new GraphVariableNode
                {
                    Variable = sensor.StoreIn,
                    Position = PositionOfNode(sensor) + new Vector2(300f, 60f)
                });
                changed = true;
            }

            changed |= WireEveryReader();
            if (changed)
                EditorUtility.SetDirty(Asset);
        }

        /// <summary>
        /// Every variable a node works with comes into it by a wire that can be seen: a pin field that names a variable
        /// but has no wire on record (a graph built in code, an older asset) gets a node of that variable next to it,
        /// wired to the pin. Nothing is chosen or renamed here — only the variable the field already names is shown.
        /// </summary>
        private bool WireEveryReader()
        {
            bool changed = false;
            foreach (AgentState state in States())
            {
                int pin = 0;
                foreach (AgentTask task in state.Tasks)
                {
                    // A sensor of the root is drawn where it was put, not where the root (never drawn) is
                    Vector2 at = state == Graph.Root ? PositionOfNode(task) : PositionOf(state);
                    foreach (System.Reflection.FieldInfo field in ValueFields.Pins(task))
                    {
                        pin++;
                        if (IsWrittenBySensor(task, field))
                            continue;                   // the variable a sensor writes: its arrow, not a reader's wire

                        string variable = ValueFields.VariableOf(task, field);
                        if (string.IsNullOrEmpty(variable) || WiredNode(task, variable) != null)
                            continue;

                        var node = new GraphVariableNode
                        {
                            Variable = variable,
                            Position = at + new Vector2(-190f, 4f + 22f * pin)
                        };
                        node.Readers.Add(task.Id);
                        Asset.VariableNodes.Add(node);
                        changed = true;
                    }

                    // The question of a Branch, of a decision: a fact condition reads its object by a wire too
                    foreach (GraphFields.Link link in ValueFields.ConditionLinks(task))
                    {
                        foreach (object asked in link.Values(task))
                            changed |= WireConditionReaders((AgentCondition)asked);
                    }
                }
            }

            // A value node reads its object by a wire too (Get: "Of")
            foreach (AgentValue value in Values())
                changed |= WireReadersOf(value, PositionOfNode(value), ValueFields.Pins(value));

            // And a shape of Detection what it reads through its own pins (Detection's interval and variable are not its)
            foreach (SensorTask sensor in Sensors())
            {
                if (sensor is not DetectionTask detection)
                    continue;

                foreach (SensorTask shape in detection.Sensors)
                    changed |= WireReadersOf(shape, PositionOfNode(shape), ValueFields.ShapePins(shape));
            }

            return changed;
        }

        // The sensor (or a shape of Detection) reads the variable through a pin of its own: a number of it bound to it
        private static bool ReadsThroughPin(SensorTask sensor, string variable)
        {
            foreach (System.Reflection.FieldInfo field in ValueFields.Pins(sensor))
            {
                if (!IsWrittenBySensor(sensor, field) && ValueFields.VariableOf(sensor, field) == variable)
                    return true;
            }

            return false;
        }

        // A condition node and the conditions joined into it: each object pin that names a variable gets its wire
        private bool WireConditionReaders(AgentCondition condition)
        {
            bool changed = false;
            List<AgentCondition> inputs = InputsOf(condition);
            for (int i = 0; inputs != null && i < inputs.Count; i++)
                changed |= WireConditionReaders(inputs[i]);

            if (condition is CompareCondition)
                return changed;                     // a Compare is wired by its own A / B pins

            return changed | WireReadersOf(condition, PositionOfNode(condition), ValueFields.Pins(condition));
        }

        // A node that is not a block (a condition, a value node, a shape): each of its pins that names a variable gets
        // its wire
        private bool WireReadersOf(AgentNode reader, Vector2 at, IEnumerable<System.Reflection.FieldInfo> pins)
        {
            bool changed = false;
            int pin = 0;
            foreach (System.Reflection.FieldInfo field in pins)
            {
                pin++;
                if (IsWrittenBySensor(reader, field))
                    continue;                           // a shape of Detection writes nothing: no reader's wire

                string variable = ValueFields.VariableOf(reader, field);
                if (string.IsNullOrEmpty(variable) || WiredNode(reader, variable) != null)
                    continue;

                var node = new GraphVariableNode
                {
                    Variable = variable,
                    Position = at + new Vector2(-190f, 4f + 22f * pin)
                };
                node.Readers.Add(reader.Id);
                Asset.VariableNodes.Add(node);
                changed = true;
            }

            return changed;
        }

        // ---------- value nodes: data a reader takes by a wire (Get, What…; step 90) ----------

        /// <summary>Every value node of the graph once: those a node reads, then those nobody reads yet.</summary>
        public List<AgentValue> Values()
        {
            var values = new List<AgentValue>();
            Graph.VisitNodes(node =>
            {
                if (node is AgentValue value && !values.Contains(value))
                    values.Add(value);
            });
            return values;
        }

        /// <summary>The nodes that read the value, each with the pin (its link field) it comes into.</summary>
        public List<(AgentNode Reader, GraphFields.Link Pin)> ReadersOf(AgentValue value)
        {
            var readers = new List<(AgentNode, GraphFields.Link)>();
            if (value == null)
                return readers;

            var nodes = new List<AgentNode>();
            Graph.VisitNodes(nodes.Add);
            foreach (AgentNode node in nodes)
            {
                if (node is AgentState)
                    continue;                           // the root's list of the unread is no pin

                foreach (GraphFields.Link link in ValueFields.ValueLinks(node))
                {
                    foreach (object held in link.Values(node))
                    {
                        if (held == value && !readers.Exists(found => found.Item1 == node && found.Item2 == link))
                            readers.Add((node, link));
                    }
                }
            }

            return readers;
        }

        /// <summary>
        /// A new value node of this type that nothing reads yet (from the list of nodes): kept with the graph, on the canvas
        /// until it is wired into a pin or deleted.
        /// </summary>
        public AgentValue AddValue(string typeId, Vector2 position)
        {
            if (AIKitNodeRegistry.Create(typeId) is not AgentValue value)
                return null;

            Edit("Add Value", () => Graph.Root.Values.Add(value), notify: false);
            if (IsReadOnly)
                return null;

            MoveNode(value, position);
            Wait(value);
            Changed?.Invoke();
            return value;
        }

        /// <summary>
        /// A new value node of this type wired into a pin of the reader (a value pin let go over an empty place): a single
        /// pin takes it instead of what came in (that stays on the canvas, unread), a list pin takes one more.
        /// </summary>
        public AgentValue AddValueTo(AgentNode reader, string pin, string typeId, Vector2 position) =>
            AIKitNodeRegistry.Create(typeId) is AgentValue value ? WireNewValue(reader, pin, value, position) : null;

        /// <summary>
        /// A new value node made elsewhere (a number of the Character section: a Get) wired into a pin of the reader, as
        /// AddValueTo wires one: what came into a single pin stays on the canvas, unread; the new node stands at position
        /// and waits for a free place. Null — the pin does not take it (nothing changes).
        /// </summary>
        public AgentValue WireNewValue(AgentNode reader, string pin, AgentValue value, Vector2 position)
        {
            GraphFields.Link link = reader != null && value != null ? GraphFields.Find(reader.GetType(), pin) : null;
            if (link == null || link.IsObject || !link.ElementType.IsInstanceOfType(value) || RefuseReadOnly("Add Value"))
                return null;

            Edit("Add Value", () => Plug(value, reader, link), notify: false);
            MoveNode(value, position);
            Wait(value);
            Changed?.Invoke();
            return value;
        }

        /// <summary>
        /// A wire from a value node into a pin of a reader: a single pin takes it instead of what came in, a list pin takes
        /// one more. One value node may come into several pins. False — the pin does not take this value.
        /// </summary>
        public bool WireValue(AgentValue value, AgentNode reader, string pin)
        {
            GraphFields.Link link = reader != null && value != null ? GraphFields.Find(reader.GetType(), pin) : null;
            if (link == null || link.IsObject || !link.ElementType.IsInstanceOfType(value))
                return false;

            Edit("Wire Value", () => Plug(value, reader, link));
            return true;
        }

        /// <summary>The value node is deleted, with every wire from it (the pins it fed take nothing).</summary>
        public void RemoveValue(AgentValue value)
        {
            if (value != null)
                Remove(value);
        }

        /// <summary>The wire from the value node into the reader's pin is pulled out; read by nobody, the node stays on the canvas.</summary>
        public void UnwireValue(AgentValue value, AgentNode reader, string pin)
        {
            GraphFields.Link link = reader != null ? GraphFields.Find(reader.GetType(), pin) : null;
            if (link == null || value == null)
                return;

            Edit("Unwire Value", () =>
            {
                if (!link.IsList)
                {
                    if (link.Field.GetValue(reader) == value)
                        link.Field.SetValue(reader, null);
                }
                else if (link.Field.GetValue(reader) is System.Collections.IList list)
                {
                    list.Remove(value);
                }

                KeepIfUnread(value);
            });
        }

        // The value comes into the pin; what came into a single pin before stays on the canvas if nobody else reads it
        private void Plug(AgentValue value, AgentNode reader, GraphFields.Link link)
        {
            Graph.Root.Values.Remove(value);
            if (link.IsList)
            {
                if (link.Field.GetValue(reader) is not System.Collections.IList list)
                {
                    list = (System.Collections.IList)Activator.CreateInstance(link.Field.FieldType);
                    link.Field.SetValue(reader, list);
                }

                if (!list.Contains(value))
                    list.Add(value);
                return;
            }

            var before = link.Field.GetValue(reader) as AgentValue;
            link.Field.SetValue(reader, value);
            if (before != null && before != value)
                KeepIfUnread(before);
            if (reader is CompareCondition compare)
                TakeInput(compare, link.Name, value);
            if (value is ComponentValue && reader is IReadsMembers)
                TakeComponent(reader);
        }

        // A component in As of a Get / Is is what it asks about (step 91): the object variable wired into its own Object is read no
        // more — its wire goes, the field forgets it (a pin that is not drawn must hold no variable)
        private void TakeComponent(AgentNode reader)
        {
            switch (reader)
            {
                case GetValue get:
                    Unread(get, get.Object);
                    get.Object = "";
                    break;
                case IsCondition condition:
                    Unread(condition, condition.Object);
                    condition.Object = "";
                    break;
            }
        }

        // A value node in A of a Compare is what is compared — as a number (CompareCondition.Answer): the variable that came
        // into A before is read no more (its wire goes, the field forgets it) and the Compare is a number comparison, drawn
        // as one — a flag's Compare asked "is it on" would else stay titled by its flag while it compares a number. One
        // in B is what A is compared with: the variable B named goes the same way
        private void TakeInput(CompareCondition compare, string input, AgentValue value)
        {
            if (input == nameof(CompareCondition.A))
            {
                if (compare.Other != compare.Variable)
                    Unread(compare, compare.Variable);
                compare.Variable = "";
                bool flag = value is { Kind: ValueKind.Flag };   // a flag (Found): "is it on", not a number
                compare.Kind = flag ? VariableType.Bool : VariableType.Float;
                if (flag)
                    compare.Expected = true;
            }
            else if (input == nameof(CompareCondition.B))
            {
                if (compare.Other != compare.Variable)
                    Unread(compare, compare.Other);
                compare.Other = "";
            }
        }

        // The nodes of the variable stop feeding the reader (their wire into it is gone)
        private void Unread(AgentNode reader, string variable)
        {
            if (reader == null || string.IsNullOrEmpty(variable))
                return;

            foreach (GraphVariableNode node in Asset.VariableNodes)
            {
                if (node.Variable == variable)
                    node.Readers.Remove(reader.Id);
            }
        }

        // A value node nobody reads any more is kept by the root (it stays on the canvas, the user deletes it)
        private void KeepIfUnread(AgentValue value)
        {
            if (value != null && value is not IBlockOutput && ReadersOf(value).Count == 0 && !Graph.Root.Values.Contains(value))
                Graph.Root.Values.Add(value);
        }

        /// <summary>The variable has a node on the canvas (a sensor fills it), so more nodes of it can be put there.</summary>
        public bool HasVariableNode(string variable) =>
            IsDeclared(variable) || Asset.VariableNodes.Exists(node => node.Variable == variable) ||
            BlackboardUsage.Collect(this).Exists(use => use.Name == variable) ||
            // a number of the character's parts (an ability's Cooldown, the body's Speed): a row of the panel like any other
            BlackboardUsage.OfCharacter(ElementInspector.Character).Exists(use => use.Name == variable);

        /// <summary>One more node of the variable at this place of the canvas (a row of the blackboard dropped there).</summary>
        public GraphVariableNode AddVariableNodeAt(string variable, Vector2 position)
        {
            if (!HasVariableNode(variable) || RefuseReadOnly("Add Variable Node"))
                return null;

            // A number of the character's parts dragged onto the canvas becomes a variable of the graph too, so every
            // character that runs the graph has it: a character whose part keeps it puts its own value there first
            // (a declared variable keeps a present value), the others start with the value it has now
            VariableUse own = IsDeclared(variable)
                ? null
                : BlackboardUsage.OfCharacter(ElementInspector.Character).Find(use => use.Name == variable);
            if (own != null)
                Edit("Declare Variable", () => Graph.Variables.Add(
                    new VariableDeclaration(variable, own.Type) { Float = own.OwnValue }), notify: false);

            Undo.RecordObject(Asset, "Add Variable Node");
            var node = new GraphVariableNode { Variable = variable, Position = position };
            Asset.VariableNodes.Add(node);
            EditorUtility.SetDirty(Asset);
            Wait(node);
            Changed?.Invoke();
            return node;
        }

        /// <summary>One more node of the same variable, next to the given one: drag it to the node that reads it.</summary>
        public GraphVariableNode DuplicateVariableNode(GraphVariableNode node, Vector2? at = null)
        {
            if (node == null || RefuseReadOnly("Duplicate Variable"))
                return null;

            Undo.RecordObject(Asset, "Duplicate Variable");
            // A copy is a new place to wire from: it starts with no wires of its own
            var copy = new GraphVariableNode { Variable = node.Variable, Position = at ?? node.Position + new Vector2(40f, 50f) };
            Asset.VariableNodes.Add(copy);
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
            return copy;
        }

        /// <summary>Removes a node of a variable (the last one of a variable stays: the variable itself is not removed here).</summary>
        public void RemoveVariableNode(GraphVariableNode node)
        {
            // Deleted with its wires: every pin it fed no longer takes the variable (a wire gone — the value gone, as
            // the rule of the canvas says); the variable itself stays on the blackboard. What a sensor fills keeps
            // being filled: another node of it takes that over (the canvas makes one if none is left)
            Edit("Remove Variable Node", () =>
            {
                foreach (int reader in node.Readers)
                    Unwire(reader, node.Variable);

                GraphVariableNode heir = Asset.VariableNodes.Find(other => other != node && other.Variable == node.Variable);
                if (heir != null)
                    heir.Filled |= node.Filled;
                Asset.VariableNodes.Remove(node);
            });
        }

        /// <summary>
        /// A declared variable gets a new name everywhere it is named: its declaration, every field of every node that
        /// names it, its nodes on the canvas, the object the character binds to it. False — the name is empty or
        /// taken (nothing changes).
        /// </summary>
        public bool RenameVariable(string from, string to)
        {
            to = to?.Trim();
            VariableDeclaration declared = Graph.Variables.Find(v => v.Name == from);
            if (declared == null || string.IsNullOrEmpty(to) || to == from || IsDeclared(to) ||
                BlackboardUsage.Collect(this).Exists(use => use.Name == to))
                return false;

            Edit("Rename Variable", () =>
            {
                declared.Name = to;
                Graph.VisitNodes(node => RenameIn(node, from, to));
                foreach (GraphVariableNode node in Asset.VariableNodes)
                {
                    if (node.Variable == from)
                        node.Variable = to;
                }
            });

            return true;
        }

        /// <summary>
        /// The characters of the open scenes that run this graph (and the one the window was opened for): what a rename
        /// or a deletion of a variable has to reach — the objects are bound on each of them.
        /// </summary>
        public List<AICharacter> CharactersOfThisGraph()
        {
            var found = new List<AICharacter>();
            if (ElementInspector.Character != null)
                found.Add(ElementInspector.Character);

            foreach (AICharacter character in UnityEngine.Object.FindObjectsByType<AICharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (character.GraphAsset == Asset && !found.Contains(character))
                    found.Add(character);
            }

            return found;
        }

        // Every field of the node that names the variable: a name field, a "number or variable" field bound to it
        private static void RenameIn(AgentNode node, string from, string to)
        {
            foreach (System.Reflection.FieldInfo field in node.GetType().GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (field.FieldType == typeof(string))
                {
                    bool names = field.GetCustomAttributes(typeof(VariableNameAttribute), true).Length > 0;
                    if (names && (string)field.GetValue(node) == from)
                        field.SetValue(node, to);
                }
                else if (ValueFields.IsValue(field))
                {
                    object boxed = field.GetValue(node);
                    if (boxed != null && ValueFields.Uses(boxed) && ValueFields.NameOf(boxed) == from)
                        field.SetValue(node, ValueFields.ToVariable(boxed, to));
                }
            }
        }

        // A sensor's StoreIn names the variable it writes into — never one it reads: no wire comes into it, and
        // deleting a node of that variable does not take it away (the sensor's own arrow shows where it writes)
        private static bool IsWrittenBySensor(object element, System.Reflection.FieldInfo field) =>
            element is SensorTask && field.Name == nameof(SensorTask.StoreIn);

        // The node with this id stops taking the variable: its pin fields that name it go back to their own value
        private void Unwire(int readerId, string variable)
        {
            AgentNode reader = null;
            Graph.VisitNodes(found =>
            {
                if (found.Id == readerId)
                    reader = found;
            });
            if (reader == null)
                return;

            if (reader is CompareCondition compare)
            {
                if (compare.Variable == variable)
                    compare.Variable = "";
                if (compare.Other == variable)
                    compare.Other = "";
            }

            foreach (System.Reflection.FieldInfo field in ValueFields.Pins(reader))
            {
                if (ValueFields.VariableOf(reader, field) != variable || IsWrittenBySensor(reader, field))
                    continue;                           // a sensor keeps the variable it writes

                if (!ValueFields.IsValue(field))
                    field.SetValue(reader, "");
                else
                {
                    object boxed = field.GetValue(reader);
                    field.SetValue(reader, ValueFields.ToConstant(boxed, DefaultOf(variable)));
                }
            }
        }

        public void MoveVariableNode(GraphVariableNode node, Vector2 position)
        {
            Undo.RecordObject(Asset, "Move Variable");
            node.Position = position;
            EditorUtility.SetDirty(Asset);              // no rebuild: its wires stay its own wherever it is put
        }

        /// <summary>The node of the variable the sensor's arrow comes into (the first one, until another is chosen).</summary>
        public GraphVariableNode SourceNode(string variable)
        {
            GraphVariableNode first = null;
            foreach (GraphVariableNode node in Asset.VariableNodes)
            {
                if (node.Variable != variable)
                    continue;

                if (node.Filled)
                    return node;

                first ??= node;
            }

            if (first != null)
            {
                first.Filled = true;
                EditorUtility.SetDirty(Asset);
            }

            return first;
        }

        /// <summary>The sensor's arrow now comes into this node of its variable.</summary>
        public void SetSource(GraphVariableNode node)
        {
            if (node == null)
                return;

            Undo.RecordObject(Asset, "Rewire Variable");
            foreach (GraphVariableNode other in Asset.VariableNodes)
            {
                if (other.Variable == node.Variable)
                    other.Filled = other == node;
            }

            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        /// <summary>The wire into the task's object pin now comes from this node of the variable (the caller redraws).</summary>
        public void SetReader(GraphVariableNode node, AgentNode task)
        {
            if (node == null || task == null)
                return;

            Undo.RecordObject(Asset, "Wire Variable");
            AssignReader(node, task);
            EditorUtility.SetDirty(Asset);
        }

        private void AssignReader(GraphVariableNode node, AgentNode task)
        {
            // One wire per variable: a task may read several variables, each from a node of its own
            foreach (GraphVariableNode other in Asset.VariableNodes)
            {
                if (other.Variable == node.Variable)
                    other.Readers.Remove(task.Id);
            }

            node.Readers.Add(task.Id);
        }

        // ---------- frames: titled boxes around groups of nodes ----------

        public IReadOnlyList<GraphFrame> Frames => Asset.Frames;

        /// <summary>A new frame around the nodes with these ids (a node is in one frame: it leaves the frame it was in).</summary>
        public GraphFrame AddFrame(string title, IEnumerable<int> members)
        {
            var ids = new List<int>(members);
            if (ids.Count == 0)
                return null;

            Undo.RecordObject(Asset, "Add Frame");
            foreach (GraphFrame other in Asset.Frames)
                other.Members.RemoveAll(ids.Contains);

            var frame = new GraphFrame { Title = title, Members = ids };
            Asset.Frames.Add(frame);
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
            return frame;
        }

        // ---------- subgraphs: parts of the graph collapsed into one node (a look of the editor only) ----------

        /// <summary>The collapsed parts of the graph.</summary>
        public IReadOnlyList<GraphSubgraph> Subgraphs => Asset.Subgraphs;

        /// <summary>
        /// The subgraph the canvas shows now (null — the graph itself): a node made there joins it. Kept by its number, so an
        /// Undo that reads the asset anew still finds it.
        /// </summary>
        public GraphSubgraph ActiveSubgraph
        {
            get => _activeSubgraph == 0 ? null : Asset.Subgraphs.Find(part => part.Id == _activeSubgraph);
            set => _activeSubgraph = value?.Id ?? 0;
        }

        private int _activeSubgraph;

        /// <summary>The subgraph a node is collapsed into; null — it stands on the graph itself.</summary>
        public GraphSubgraph SubgraphOf(int id) => id == 0 ? null : Asset.Subgraphs.Find(part => part.Members.Contains(id));

        /// <summary>
        /// Collapses these nodes into one (blocks and decisions; never the root or the Brain): they leave any other
        /// subgraph and frame, and the new node stands where they stood on average. Null — nothing to collapse.
        /// </summary>
        public GraphSubgraph CollapseToSubgraph(IEnumerable<int> ids, string title = "Subgraph")
        {
            var states = new List<AgentState>();
            foreach (int id in ids)
            {
                AgentState state = States().Find(found => found.Id == id);
                if (state != null && state != Graph.Root && !state.IsBrain && !states.Contains(state))
                    states.Add(state);
            }

            if (states.Count == 0 || RefuseReadOnly("Collapse To Subgraph"))
                return null;

            Undo.RecordObject(Asset, "Collapse To Subgraph");
            var members = states.ConvertAll(state => state.Id);
            foreach (GraphSubgraph other in Asset.Subgraphs)
                other.Members.RemoveAll(members.Contains);
            Asset.Subgraphs.RemoveAll(other => other.Members.Count == 0);
            foreach (GraphFrame frame in Asset.Frames)
                frame.Members.RemoveAll(members.Contains);

            Vector2 middle = Vector2.zero;
            foreach (AgentState state in states)
                middle += PositionOf(state);
            int next = 1;
            foreach (GraphSubgraph other in Asset.Subgraphs)
                next = Mathf.Max(next, other.Id + 1);

            var part = new GraphSubgraph { Id = next, Title = title, Members = members, Position = middle / states.Count };
            Asset.Subgraphs.Add(part);
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
            return part;
        }

        /// <summary>The subgraph's nodes stand on the graph again, where they stood; the subgraph is gone.</summary>
        public void ExpandSubgraph(GraphSubgraph part)
        {
            if (part == null || !Asset.Subgraphs.Contains(part) || RefuseReadOnly("Expand Subgraph"))
                return;

            Undo.RecordObject(Asset, "Expand Subgraph");
            Asset.Subgraphs.Remove(part);
            if (ActiveSubgraph == part)
                ActiveSubgraph = null;
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        public void RenameSubgraph(GraphSubgraph part, string title)
        {
            if (part == null || string.IsNullOrWhiteSpace(title) || part.Title == title.Trim())
                return;

            Undo.RecordObject(Asset, "Rename Subgraph");
            part.Title = title.Trim();
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        /// <summary>The subgraph's node was dragged: it stands there (no redraw).</summary>
        public void MoveSubgraph(GraphSubgraph part, Vector2 position)
        {
            if (part == null)
                return;

            part.Position = position;
            EditorUtility.SetDirty(Asset);
        }

        /// <summary>
        /// Every decision collapsed with its chain into a subgraph named after it, standing where the decision stood
        /// (16.11.2026, the user's word: "collapse everything into subgraphs", "decisions too"): the decision and the blocks its
        /// Do leads to and on, never another decision or the Brain; a block two chains share goes with the more important
        /// decision; a node in a subgraph already stays in it, and a decision whose chain is collapsed already joins it. On
        /// the graph only the Brain and its wires into the subgraphs stay. Returns how many subgraphs were made or joined.
        /// </summary>
        public int CollapseDecisionChains()
        {
            if (IsReadOnly)
                return 0;

            var decisions = States().Where(state => state.Reasoner != null)
                .OrderBy(state => state.Reasoner.Priority).ToList();
            var taken = new HashSet<int>();
            int made = 0;
            foreach (AgentState decision in decisions)
            {
                var chain = new List<int>();
                var queue = new Queue<AgentState>();
                foreach (Transition arrow in decision.Transitions)
                    queue.Enqueue(arrow.Target);
                while (queue.Count > 0)
                {
                    AgentState next = queue.Dequeue();
                    if (next == null || next == Graph.Root || next.IsBrain || next.Reasoner != null ||
                        !taken.Add(next.Id) || SubgraphOf(next.Id) != null)
                        continue;

                    chain.Add(next.Id);
                    foreach (Transition arrow in next.Transitions)
                        queue.Enqueue(arrow.Target);
                }

                if (SubgraphOf(decision.Id) != null)
                    continue;

                GraphSubgraph its = decision.Transitions.Select(arrow => arrow.Target).Where(target => target != null)
                    .Select(target => SubgraphOf(target.Id)).FirstOrDefault(found => found != null);
                if (chain.Count == 0 && its != null)
                {
                    its.Members.Insert(0, decision.Id); // collapsed before without its decision: it joins its chain
                    its.Position = PositionOf(decision);
                    made++;
                    continue;
                }

                chain.Insert(0, decision.Id);           // the decision goes in with its chain (16.11.2026, the user's word)
                GraphSubgraph part = CollapseToSubgraph(chain, decision.DisplayName);
                if (part == null)
                    continue;

                part.Position = PositionOf(decision);
                made++;
            }

            if (made > 0)
            {
                EditorUtility.SetDirty(Asset);
                Changed?.Invoke();
            }

            return made;
        }

        // A node made while the canvas shows a subgraph joins it (it was made inside)
        private void JoinActiveSubgraph(AgentState state)
        {
            if (state == null || state.Id == 0 || ActiveSubgraph == null || !Asset.Subgraphs.Contains(ActiveSubgraph) ||
                ActiveSubgraph.Members.Contains(state.Id))
                return;

            ActiveSubgraph.Members.Add(state.Id);
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        /// <summary>
        /// Removes the frame; the nodes inside stay — the Brain too (step 90: its frame is drawn round it again; the Brain is
        /// deleted only as a node, "Delete Brain").
        /// </summary>
        public void RemoveFrame(GraphFrame frame)
        {
            if (frame == null || !Asset.Frames.Contains(frame))
                return;

            Undo.RecordObject(Asset, "Remove Frame");
            Asset.Frames.Remove(frame);
            EditorUtility.SetDirty(Asset);
            Changed?.Invoke();
        }

        /// <summary>The brain a node belongs to: the id of the brain whose frame holds it (a brain — its own id); 0 — none.</summary>
        public int BrainOf(int id)
        {
            foreach (GraphFrame frame in Asset.Frames)
            {
                if (frame.Brain != 0 && (frame.Brain == id || frame.Members.Contains(id)))
                    return frame.Brain;
            }

            return 0;
        }

        /// <summary>
        /// Every reasoner state (Combat Brain) has a frame of its own — the canvas draws a brain as its frame; a
        /// frame whose brain is gone becomes a plain one. Not an edit of the graph: nothing is announced.
        /// </summary>
        public void EnsureBrainFrames()
        {
            if (IsReadOnly)
                return;

            List<AgentState> states = States();
            bool changed = false;
            foreach (GraphFrame frame in Asset.Frames)
            {
                if (frame.Brain != 0 && !states.Exists(state => state.Id == frame.Brain && state.IsBrain))
                {
                    frame.Brain = 0;
                    changed = true;
                }
            }

            // A sensor is no part of a brain: it works by itself, whatever the brains do, and only fills its variable
            List<SensorTask> sensors = Sensors();
            foreach (GraphFrame frame in Asset.Frames)
            {
                if (frame.Brain != 0 && frame.Members.RemoveAll(id => sensors.Exists(sensor => sensor.Id == id)) > 0)
                    changed = true;
            }

            // Nothing but the Brain is a part of its frame (step 90): what a node dragged in left there goes
            foreach (GraphFrame frame in Asset.Frames)
            {
                if (frame.Brain != 0 && frame.Members.Count > 0)
                {
                    frame.Members.Clear();
                    changed = true;
                }
            }

            foreach (AgentState state in states)
            {
                if (!state.IsBrain || Asset.Frames.Exists(frame => frame.Brain == state.Id))
                    continue;

                Asset.Frames.Add(new GraphFrame { Title = NodeTitles.Of(state), Brain = state.Id });
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(Asset);
        }

        /// <summary>The title typed on the canvas (no rebuild: the canvas already shows it).</summary>
        public void RenameFrame(GraphFrame frame, string title)
        {
            if (frame == null || frame.Title == title)
                return;

            Undo.RecordObject(Asset, "Rename Frame");
            frame.Title = title;
            EditorUtility.SetDirty(Asset);
            // The brain inside keeps its name: a node is called by its type, a frame's title is only the frame's
        }

        /// <summary>A node dragged into the frame or out of it on the canvas (no rebuild).</summary>
        public void SetFrameMember(GraphFrame frame, int id, bool inside)
        {
            if (frame == null || id == 0)
                return;

            Undo.RecordObject(Asset, "Change Frame");
            foreach (GraphFrame other in Asset.Frames)
                other.Members.Remove(id);

            if (inside)
                frame.Members.Add(id);
            EditorUtility.SetDirty(Asset);
        }

        /// <summary>Where the canvas draws a node that is not a state (a sensor).</summary>
        public Vector2 PositionOfNode(AgentNode node) => Asset.Find(node.Id)?.Position ?? Vector2.zero;

        public void MoveNode(AgentNode node, Vector2 position)
        {
            GraphRecord record = Asset.Find(node.Id);
            if (record == null)
                return;

            record.Position = position;
            EditorUtility.SetDirty(Asset);
        }

        /// <summary>Sets one field of an element (a data wire plugged in or pulled out on the canvas).</summary>
        public void SetField(object element, System.Reflection.FieldInfo field, object value) =>
            Edit("Wire", () => field.SetValue(element, value));

        // ---------- the blackboard: numbers of nodes as variables ----------

        /// <summary>The variable is declared in the graph.</summary>
        public bool IsDeclared(string variable) => Graph.Variables.Exists(v => v.Name == variable);

        /// <summary>The kind of a variable: as declared; a name nodes only use — an object if a sensor fills it, else a number.</summary>
        public VariableType TypeOfVariable(string variable)
        {
            VariableDeclaration declared = Graph.Variables.Find(v => v.Name == variable);
            if (declared != null)
                return declared.Type;

            if (SensorVariables().Contains(variable))
                return VariableType.Transform;

            VariableUse used = BlackboardUsage.Collect(this).Find(use => use.Name == variable);
            return used != null ? used.Type : VariableType.Float;
        }

        /// <summary>The value a declared number or flag starts with (null — not such a variable).</summary>
        public object DefaultOf(string variable)
        {
            VariableDeclaration declared = Graph.Variables.Find(v => v.Name == variable);
            return declared?.Type switch
            {
                VariableType.Float => declared.Float,
                VariableType.Int => declared.Int,
                VariableType.Double => declared.Double,
                VariableType.Bool => declared.Bool,
                _ => null
            };
        }

        /// <summary>The value a declared number or flag starts with is changed (the panel of the blackboard).</summary>
        public void SetDefault(string variable, object value)
        {
            VariableDeclaration declared = Graph.Variables.Find(v => v.Name == variable);
            if (declared == null || value == null)
                return;

            // An Undo step of its own: Ctrl+Z takes back the start value, not the edit before it
            Edit("Start Value", () => WriteDefault(declared, value), notify: false);
        }

        private static void WriteDefault(VariableDeclaration declared, object value)
        {
            switch (value)
            {
                case float number:
                    declared.Float = number;
                    break;
                case int whole:
                    declared.Int = whole;
                    break;
                case double wide:
                    declared.Double = wide;
                    break;
                case bool flag:
                    declared.Bool = flag;
                    break;
            }
        }

        /// <summary>
        /// A pin field of a node is bound to a variable (a wire dropped on it) or let go (empty — the wire is deleted):
        /// an object field keeps the name, a "number or variable" field switches between the variable and its number.
        /// </summary>
        public void BindField(object element, System.Reflection.FieldInfo field, string variable) => Edit("Wire", () =>
        {
            if (!ValueFields.IsValue(field))
            {
                field.SetValue(element, variable);
                return;
            }

            object boxed = field.GetValue(element);
            field.SetValue(element, string.IsNullOrEmpty(variable)
                ? ValueFields.ToConstant(boxed, DefaultOf(ValueFields.NameOf(boxed)))
                : ValueFields.ToVariable(boxed, variable));
        });

        // ---------- the numbered outputs of a Random node ----------

        /// <summary>A wire from output number <paramref name="output"/> of a Random node: one wire per output, a new one replaces it.</summary>
        public Transition ConnectOutput(AgentState from, AgentState to, int output)
        {
            Transition edge = null;
            Edit("Add Edge", () =>
            {
                from.Transitions.RemoveAll(old => old.Output == output);
                edge = new Transition { Target = to, Trigger = TransitionTrigger.TasksSucceeded, Output = output };
                from.AddTransition(edge);
                LinkIfItRunsBack(from, edge);
            });
            return edge;
        }

        /// <summary>One more output at the bottom of a Random node (weight 1).</summary>
        public void AddRandomOutput(PickRandomTask random) =>
            Edit("Add Output", () => random.Outputs.Add(new RandomOutput()));

        /// <summary>Takes an output off a Random node with its wire; the outputs below move up a number with theirs.</summary>
        public void RemoveRandomOutput(AgentState state, int output) => Edit("Remove Output", () =>
        {
            PickRandomTask random = state?.Random;
            if (random == null || output < 0 || output >= random.Outputs.Count)
                return;

            random.Outputs.RemoveAt(output);
            state.Transitions.RemoveAll(edge => edge.Output == output);
            foreach (Transition edge in state.Transitions)
            {
                if (edge.Output > output)
                    edge.Output--;
            }
        });

        /// <summary>Points an existing arrow at another node (its trigger and output stay).</summary>
        public void Retarget(Transition transition, AgentState to) => Edit("Retarget Edge", () =>
        {
            transition.Target = to;
            LinkIfItRunsBack(States().Find(state => state.Transitions.Contains(transition)), transition);
        });

        /// <summary>Removes a node (with every arrow into it), an arrow, a task or a condition.</summary>
        public void Remove(object element) => Edit("Remove", () =>
        {
            switch (element)
            {
                case AgentState state when state != Graph.Root:
                    Graph.Root.Children.Remove(state);
                    foreach (AgentState other in States())
                        other.Transitions.RemoveAll(t => t.Target == state);
                    ForgetId(state.Id);
                    foreach (AgentTask task in state.Tasks)
                        ForgetId(task.Id);              // its block is what a variable node feeds (by the block's id)
                    foreach (AgentTask task in state.Tasks)
                    {
                        // The values the block gave out (step 91): the pins they fed take nothing now
                        foreach (GraphFields.Link output in ValueFields.OutputLinks(task))
                            foreach (object given in output.Values(task))
                                Unhold(given as AgentNode);
                    }
                    break;

                case Transition transition:
                    foreach (AgentState other in States())
                        other.Transitions.Remove(transition);
                    break;

                case AgentNode node:
                    RemoveNode(node);
                    break;
            }
        });

        // A node that is gone leaves no trace under its id: no decision is "not from" it, no variable node feeds it, no
        // frame holds it, no breakpoint waits in it — a new node that gets the same id later starts clean
        private void ForgetId(int id)
        {
            if (id == 0)
                return;

            Graph.VisitNodes(node =>
            {
                if (node is ReasonerTask decision)
                    decision.NotFrom?.Remove(id);
            });
            foreach (AgentState state in States())
            {
                if (state.Reasoner is { } decision)
                    decision.NotFrom?.Remove(id);
            }

            foreach (GraphVariableNode variable in Asset.VariableNodes)
                variable.Readers.Remove(id);
            foreach (GraphFrame frame in Asset.Frames)
                frame.Members.Remove(id);
            foreach (GraphSubgraph part in Asset.Subgraphs)
                part.Members.Remove(id);
            Asset.Subgraphs.RemoveAll(part => part.Members.Count == 0 && part != ActiveSubgraph);
            GraphBreakpoints.Set(Asset, id, false);
        }

        private void RemoveNode(AgentNode node)
        {
            ForgetId(node.Id);
            Unhold(node);
        }

        /// <summary>A field of an element changed in the inspector: write it back.</summary>
        public void Touch(string label = "Edit Node") => Edit(label, () => { });

        /// <summary>
        /// A field changed on the canvas itself: written back without the Changed event, so the canvas is not rebuilt
        /// under the field being typed into.
        /// </summary>
        public void TouchQuietly(string label = "Edit Node") => Edit(label, () => { }, notify: false);

        /// <summary>Declares a variable under a free name made from the given one (("Target", "Target 2"…); returns the name.
        /// Not saved by itself: for an edit that is already under way and ends with Touch.</summary>
        public string DeclareUnique(string baseName, VariableType type)
        {
            string name = baseName;
            for (int n = 2; Graph.Variables.Exists(v => v.Name == name); n++)
                name = $"{baseName} {n}";

            Graph.Variables.Add(new VariableDeclaration(name, type));     // the caller saves (Touch)
            return name;
        }

        public void DeclareVariable(string name, VariableType type) => Edit("Declare Variable", () =>
        {
            Graph.Variables.Add(new VariableDeclaration(name, type));
        });

        public void RemoveVariable(VariableDeclaration declaration) =>
            Edit("Remove Variable", () => Graph.Variables.Remove(declaration));

        /// <summary>
        /// A variable of the graph is deleted from the blackboard with everything that names it: its nodes on the
        /// canvas and their wires, every field that reads or writes it (a pin goes back to its own number, a name field —
        /// a sensor's too — to none). Nothing takes its place by itself. False —
        /// not a variable the graph declares (a number of the character's abilities is the ability's).
        /// </summary>
        public bool DeleteVariable(string name)
        {
            if (string.IsNullOrEmpty(name) || !IsDeclared(name))
                return false;

            Edit("Delete Variable", () =>
            {
                Graph.Variables.RemoveAll(declared => declared.Name == name);
                Asset.VariableNodes.RemoveAll(node => node.Variable == name);
                Graph.VisitNodes(node => ForgetIn(node, name));
            });

            return true;
        }

        // Every field of the node that names the variable lets go of it
        private void ForgetIn(AgentNode node, string name)
        {
            foreach (System.Reflection.FieldInfo field in node.GetType().GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (field.FieldType == typeof(string))
                {
                    bool names = field.GetCustomAttributes(typeof(VariableNameAttribute), true).Length > 0;
                    if (names && (string)field.GetValue(node) == name)
                        field.SetValue(node, "");
                }
                else if (ValueFields.IsValue(field))
                {
                    object boxed = field.GetValue(node);
                    if (boxed != null && ValueFields.Uses(boxed) && ValueFields.NameOf(boxed) == name)
                        field.SetValue(node, ValueFields.ToConstant(boxed, DefaultOf(name)));
                }
            }
        }

        private void Edit(string label, Action change, bool notify = true)
        {
            if (RefuseReadOnly(label))
                return;

            Undo.RecordObject(Asset, label);
            change();
            Save(notify);
        }

        public void Save(bool notify = true)
        {
            if (RefuseReadOnly("Save"))
                return;

            Graph.Link();

            // An ability an older graph named by a string no asset has yet: the write makes the record anew from the node
            // (which has no field for the name) — the name is put back, so it is never lost and its node keeps saying it
            List<AbilityNameUpgrade.Named> unresolved = AbilityNameUpgrade.Unresolved(Asset, Graph);
            GraphSerializer.Write(Graph, Asset);
            AbilityNameUpgrade.Keep(Asset, unresolved);
            foreach (KeyValuePair<AgentState, Vector2> pair in _positions)
            {
                GraphRecord record = Asset.Find(pair.Key.Id);
                if (record != null)
                {
                    record.Position = pair.Value;
                    record.Folded = _folded.Contains(pair.Key);
                }
            }

            StoreWaypoints();
            ForgetGoneReaders();
            EditorUtility.SetDirty(Asset);
            Issues.Clear();
            GraphValidator.Check(Asset, Graph, Issues);
            CheckKeys();
            AbilityNameUpgrade.Report(unresolved, Issues);
            Issues.AddRange(_upgradeNotes);                     // what the upgrade said is still true of this graph
            if (notify)
                Changed?.Invoke();
        }

        // A node that is gone feeds no variable node: the ids the graph no longer has leave every wire (a block of a node
        // deleted, what it held) — a node made later with the same id starts with no wire
        private void ForgetGoneReaders()
        {
            var alive = new HashSet<int>();
            Graph.VisitNodes(node => alive.Add(node.Id));
            foreach (GraphVariableNode variable in Asset.VariableNodes)
                variable.Readers.RemoveAll(id => !alive.Contains(id));
        }

        // The keys of the nodes nothing in the project has (a move of no weapon, an action nothing shows, a group of places
        // nobody names): a warning on the node — a typo, or a thing renamed. Only the editor knows the project (step 90)
        private void CheckKeys()
        {
            var nodes = new List<AgentNode>();
            Graph.VisitNodes(nodes.Add);
            foreach (AgentNode node in nodes)
            {
                if (node is AgentState)
                    continue;

                foreach (string unknown in NodeOptions.UnknownKeys(node))
                {
                    Issues.Add(new GraphIssue(GraphIssueCode.UnknownKey, false, node.Id, GraphSerializer.TypeIdOf(node),
                        $"'{node.DisplayName}': {unknown} — pick one from the list"));
                }
            }
        }
    }
}

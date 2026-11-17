using System;
using System.Collections.Generic;
using System.Reflection;

namespace AIKit
{
    /// <summary>What is wrong with a graph asset.</summary>
    public enum GraphIssueCode
    {
        /// <summary>No root state to start from.</summary>
        MissingRoot,

        /// <summary>A record's type id belongs to no node class (renamed without keeping its [AIKitNode] id, or gone).</summary>
        UnknownNodeType,

        /// <summary>A link field the type no longer has (renamed without [FormerlySerializedAs]).</summary>
        UnknownField,

        /// <summary>A link points to an id that is not there, or to the wrong kind of element.</summary>
        DanglingEdge,

        /// <summary>A node record that is not in the graph's list (outside the root).</summary>
        UnreachableState,

        /// <summary>A node with no block: entered, it does nothing.</summary>
        EmptyState,

        /// <summary>A node reads a variable the asset does not declare (a typo would be a silent second variable).</summary>
        UnknownVariable,

        /// <summary>Assets the graph points to lead back to it (invariant 9: data assets are leaves — no cycles).</summary>
        SubgraphCycle,

        /// <summary>A pin a node cannot work without has no wire (a Brain with decisions that need a target and no Target).</summary>
        UnwiredPin,

        /// <summary>Two decisions of one Brain have the same priority: which one wins is only by the order they were made.</summary>
        EqualPriority,

        /// <summary>A decision has nothing behind its Do pin: taken, it does nothing.</summary>
        NoChain,

        /// <summary>A decision's "not from" names something that is not a decision of its Brain.</summary>
        NotFromStranger,

        /// <summary>The graph has more than one Brain: the graph runs the first one — the other is a mistake.</summary>
        SecondBrain,

        /// <summary>
        /// A squad's decision wired to a plain Brain, or a character's decision wired to a Squad Brain: it is never taken.
        /// </summary>
        WrongBrain,

        /// <summary>The graph has no Brain: a character with it does nothing at all (the canvas is the program).</summary>
        NoBrain,

        /// <summary>No decision's chain leads to the node: it never runs.</summary>
        NotInChain,

        /// <summary>A node holds more than one block (a graph from code): a node is one block — a chain shows the order.</summary>
        TwoBlocks,

        /// <summary>
        /// A part of the old model: reflex arrows (a sensor's Detected / Lost), nested nodes, conditions on arrows or on
        /// entering a node, arrows by a signal or a change. Not read; the editor opens the asset for viewing only.
        /// </summary>
        OldModel,

        // ---------- step 90 (reserved here so the parallel lanes add their checks without clashing) ----------

        /// <summary>A node names a key nothing in the project has (a move slot, an action, a pool) — canvas (lane E).</summary>
        UnknownKey,

        /// <summary>A decision reads an attack slot of a group nobody in the graph asks for (lane S).</summary>
        NobodyAsksForSlot,

        /// <summary>An ability decision or block with no ability (lane M).</summary>
        NoAbility,

        /// <summary>Two different ability assets with one name in one graph (lane M).</summary>
        AbilityNameClash,

        /// <summary>A general ability decision with no abilities in its set (lane M).</summary>
        EmptyAbilitySet,

        /// <summary>A reaction is less important than an ordinary decision it has to pre-empt (lane R).</summary>
        ReactionBelowDecision,

        /// <summary>A search reads no What, or a Find / Interact has nothing to work with (lane O).</summary>
        NothingToSearch,

        /// <summary>A node type of the old tactics was removed: the issue names the chain that replaces it (lane O).</summary>
        ReplacedNode,

        /// <summary>A value node (Get, What) a reader needs is not wired in (lanes S, O; the canvas draws it).</summary>
        UnwiredValue,

        /// <summary>
        /// A Try Get Component with no type, or one no loaded code has; a Get / Is reading a component with no member of it,
        /// or a sensor looking for an unknown type (step 91).
        /// </summary>
        BadComponent,

        /// <summary>The asset is of an older format and was not upgraded and saved yet (Tools → AI Kit → Upgrade All Graphs).</summary>
        OldFormat,

        /// <summary>
        /// A record's Data cannot be read (not valid JSON — edited by hand, a bad merge): the record is not read, the
        /// editor opens the asset for viewing only, so the record is kept to be fixed in the asset's inspector.
        /// </summary>
        BadData
    }


    /// <summary>One problem of a graph asset, naming the record (id, type) and the node.</summary>
    public readonly struct GraphIssue
    {
        public GraphIssue(GraphIssueCode code, bool isError, int recordId, string type, string message)
        {
            Code = code;
            IsError = isError;
            RecordId = recordId;
            Type = type;
            Message = message;
        }

        public GraphIssueCode Code { get; }

        public bool IsError { get; }

        public int RecordId { get; }

        public string Type { get; }

        public string Message { get; }

        public override string ToString() => $"[{Code}] record {RecordId}{(Type != null ? $" ({Type})" : "")}: {Message}";
    }

    /// <summary>
    /// Checks a graph asset: what reading finds (no root, unknown types and fields, dangling links, parts of the old
    /// model) and what the read graph shows against the model the canvas draws — one Brain, decisions wired to it, each
    /// with its chain, one block per node, nothing that no chain reaches; variables nobody declared. Every issue names the
    /// record and the node.
    /// </summary>
    public static class GraphValidator
    {
        private const string STATE = "aikit.state";

        public static List<GraphIssue> Validate(BehaviorGraphAsset asset)
        {
            var issues = new List<GraphIssue>();
            if (asset == null)
                return issues;

            BehaviorGraph graph = GraphSerializer.Read(asset, issues);
            if (graph != null)
                Check(asset, graph, issues);

            return issues;
        }

        /// <summary>The checks on a read graph (Compile runs them too).</summary>
        public static void Check(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues)
        {
            var states = new List<AgentState>();
            graph.CollectStates(states);

            // Node records outside the graph's list
            foreach (GraphRecord record in asset.Records)
            {
                if (record.Type != STATE || record.Id == asset.RootId || states.Exists(state => state.Id == record.Id))
                    continue;

                issues.Add(new GraphIssue(GraphIssueCode.UnreachableState, true, record.Id, record.Type,
                    "the node is not in the graph's list (outside the root)"));
            }

            AgentState brain = graph.Brain;
            if (brain == null)
                issues.Add(new GraphIssue(GraphIssueCode.NoBrain, true, asset.RootId, null,
                    "the graph has no Brain: a character with it does nothing — add a Brain and wire decisions to it"));

            bool commandGraph = brain != null && CombatBrainTask.Of(brain) is SquadBrainTask;
            Dictionary<AgentState, AgentState> deadOnTick = DeadOnTick(states);
            foreach (KeyValuePair<AgentState, AgentState> dead in deadOnTick)
            {
                issues.Add(new GraphIssue(GraphIssueCode.DanglingEdge, false, dead.Key.Id, STATE,
                    $"'{dead.Key.DisplayName}' is on the On Tick chain of '{dead.Value.DisplayName}' and takes time: an " +
                    "On Tick chain runs only blocks that take no time — it never runs there (lead to it from Do or a " +
                    "Done pin, or make it wait no time)"));
            }

            foreach (AgentState state in states)
            {
                if (state == graph.Root)
                    continue;

                if (state.Tasks.Count > 1)
                    issues.Add(new GraphIssue(GraphIssueCode.TwoBlocks, true, state.Id, STATE,
                        $"'{state.DisplayName}' holds {state.Tasks.Count} blocks: a node is one block — make them a chain"));

                if (state.Tasks.Count == 0)
                    issues.Add(new GraphIssue(GraphIssueCode.EmptyState, false, state.Id, STATE,
                        $"'{state.DisplayName}' has no block: entered, it does nothing"));

                if (state.IsBrain && state != brain)
                    issues.Add(new GraphIssue(GraphIssueCode.SecondBrain, true, state.Id, STATE,
                        $"'{state.DisplayName}' is a second Brain: the graph runs the first one ('{brain?.DisplayName}')"));

                if (state.Reasoner != null)
                {
                    if (brain == null || !brain.Transitions.Exists(wire => wire.Target == state))
                        issues.Add(new GraphIssue(GraphIssueCode.UnwiredPin, false, state.Id, STATE,
                            $"'{state.DisplayName}' is not wired to the Brain: it is never taken"));
                }
                else if (brain != null && !state.IsBrain && !graph.Runs(state) && !deadOnTick.ContainsKey(state))
                {
                    issues.Add(new GraphIssue(GraphIssueCode.NotInChain, false, state.Id, STATE,
                        $"'{state.DisplayName}': no decision's chain leads to it — it never runs"));
                }

                if (!commandGraph && state.Tasks.Exists(task => task is SquadBlockTask))
                    issues.Add(new GraphIssue(GraphIssueCode.WrongBrain, false, state.Id, STATE,
                        $"'{state.DisplayName}' leads a squad's formation: it belongs in a squad's command graph (a Squad Brain)"));

                foreach (Transition arrow in state.Transitions)
                {
                    // A block's arrow that leads on at once (not by how its work ended): the block is left before it is done
                    if (!state.IsBrain && state.Reasoner == null && arrow.Trigger == TransitionTrigger.Always &&
                        arrow.Target != null)
                        issues.Add(new GraphIssue(GraphIssueCode.DanglingEdge, false, state.Id, STATE,
                            $"'{state.DisplayName}' has an arrow that leads on at once to '{arrow.Target.DisplayName}': " +
                            "the block is left before it is done — lead from its Done or Failed pin"));

                    if (state.IsBrain || arrow.Target == null || !(arrow.Target.IsBrain || arrow.Target.Reasoner != null))
                        continue;

                    issues.Add(new GraphIssue(GraphIssueCode.DanglingEdge, false, state.Id, STATE,
                        $"'{state.DisplayName}' has an arrow into '{arrow.Target.DisplayName}': only the Brain takes " +
                        "decisions — the chain ends there and the Brain chooses"));
                }

                CheckWireOnly(state, issues);
            }

            if (brain != null)
                CheckDecisions(brain, issues);

            ConditionChecks.Check(graph, issues);       // step 90: Attack Slot, Is, Get (lane S)
            AbilityGraphChecks.Check(graph, issues);        // the graph's abilities (step 90, lane M)
            ObjectGraphChecks.Check(graph, issues);     // the searches (step 90, lane O)

            CheckVariables(asset, graph, issues);
            CheckCycles(asset, issues);
        }

        // The blocks an On Tick chain leads to that take time — the chain stops before them (BehaviorGraphInstance runs
        // only instant blocks on it), so the wire into them does nothing: each with the node whose On Tick pin leads there
        private static Dictionary<AgentState, AgentState> DeadOnTick(List<AgentState> states)
        {
            var dead = new Dictionary<AgentState, AgentState>();
            var seen = new HashSet<AgentState>();
            var next = new Queue<AgentState>();
            foreach (AgentState owner in states)
            {
                foreach (Transition pin in owner.Transitions)
                {
                    if (pin.Trigger != TransitionTrigger.Tick || pin.Target == null)
                        continue;

                    seen.Clear();
                    next.Clear();
                    next.Enqueue(pin.Target);
                    while (next.Count > 0)
                    {
                        AgentState node = next.Dequeue();
                        if (!seen.Add(node) || node.IsBrain || node.Reasoner != null)
                            continue;                   // an arrow into a decision is a problem of its own

                        if (!node.IsInstant)
                        {
                            if (node.Tasks.Count > 0 && !dead.ContainsKey(node))
                                dead[node] = owner;
                            continue;
                        }

                        foreach (Transition arrow in node.Transitions)
                        {
                            if (arrow.Target != null && arrow.Trigger != TransitionTrigger.Tick &&
                                arrow.Trigger != TransitionTrigger.Always)
                                next.Enqueue(arrow.Target);
                        }
                    }
                }
            }

            return dead;
        }

        // A pin that takes its value only by a wire, left without one: the node never does its work
        private static void CheckWireOnly(AgentState state, List<GraphIssue> issues)
        {
            foreach (AgentTask task in state.Tasks)
            {
                foreach (FieldInfo field in task.GetType().GetFields())
                {
                    if (field.GetCustomAttribute<WireOnlyAttribute>() is not { Optional: false })
                        continue;

                    object value = field.GetValue(task);
                    bool unwired = value switch
                    {
                        string name => string.IsNullOrEmpty(name),
                        null => true,
                        _ => value.GetType().IsGenericType &&
                             value.GetType().GetGenericTypeDefinition() == typeof(BlackboardValue<>) &&
                             !(bool)value.GetType().GetProperty("UsesVariable").GetValue(value)
                    };

                    if (unwired)
                        issues.Add(new GraphIssue(GraphIssueCode.UnwiredPin, false, state.Id, STATE,
                            $"'{state.DisplayName}' has nothing wired into {field.Name}: it does nothing until a variable is wired in"));
                }
            }
        }

        // The decisions wired to the Brain: each with a chain behind Do, a priority of its own, "not from" among its own;
        // the Brain's Target wired in when a decision needs it
        private static void CheckDecisions(AgentState brain, List<GraphIssue> issues)
        {
            var decisions = new List<AgentState>();
            foreach (Transition wire in brain.Transitions)
            {
                if (wire.Target?.Reasoner != null && !decisions.Contains(wire.Target))
                    decisions.Add(wire.Target);
            }

            CombatBrainTask own = CombatBrainTask.Of(brain);
            bool squadBrain = own is SquadBrainTask;
            AgentState needsTarget = decisions.Find(decision => decision.Reasoner.NeedsTarget);
            if (own != null && string.IsNullOrEmpty(own.Target) && needsTarget != null)
                issues.Add(new GraphIssue(GraphIssueCode.UnwiredPin, true, brain.Id, STATE,
                    $"'{brain.DisplayName}' has no Target wired in, and '{needsTarget.DisplayName}' needs one: it is never taken"));

            foreach (AgentState decision in decisions)
            {
                ReasonerTask task = decision.Reasoner;
                if (task is SquadDecisionTask != squadBrain)
                    issues.Add(new GraphIssue(GraphIssueCode.WrongBrain, false, decision.Id, STATE, squadBrain
                        ? $"'{decision.DisplayName}' is a character's decision: a Squad Brain takes only a squad's decisions"
                        : $"'{decision.DisplayName}' is a squad's decision: wire it to a Squad Brain"));

                // Its Do: an arrow that is not its On Tick (an On Tick chain is no chain of the decision's own)
                if (!decision.Transitions.Exists(arrow => arrow.Target != null && arrow.Trigger != TransitionTrigger.Tick))
                {
                    bool ticks = decision.Transitions.Exists(arrow => arrow.Target != null &&
                                                                      arrow.Trigger == TransitionTrigger.Tick);
                    issues.Add(new GraphIssue(GraphIssueCode.NoChain, !ticks, decision.Id, STATE, ticks
                        ? $"'{decision.DisplayName}' has nothing wired to its Do pin: taken, it stands and only runs its On Tick"
                        : $"'{decision.DisplayName}' has nothing wired to its Do pin: taken, it does nothing"));
                }

                AgentState twin = decisions.Find(other => other != decision && other.Reasoner.Priority == task.Priority);
                if (twin != null)
                    issues.Add(new GraphIssue(GraphIssueCode.EqualPriority, false, decision.Id, STATE,
                        $"'{decision.DisplayName}' and '{twin.DisplayName}' have the same priority {task.Priority}"));

                foreach (int id in task.NotFrom)
                {
                    if (!decisions.Exists(other => other.Id == id))
                        issues.Add(new GraphIssue(GraphIssueCode.NotFromStranger, false, decision.Id, STATE,
                            $"'{decision.DisplayName}': \"not from\" names a node that is not a decision of {brain.DisplayName}"));
                }
            }

            CheckReactions(decisions, issues);
        }

        // A reaction is taken out of turn only over less important decisions: one below a decision whose chain attacks
        // never cuts in on that fight (step 90, lane R — handbook §19.2: the defence answers the attack it is in)
        private static void CheckReactions(List<AgentState> decisions, List<GraphIssue> issues)
        {
            foreach (AgentState reaction in decisions)
            {
                if (reaction.Reasoner is not ReactionDecision own)
                    continue;

                AgentState above = decisions.Find(other => other.Reasoner is not ReactionDecision &&
                                                           other.Reasoner.Priority < own.Priority && ChainAttacks(other));
                if (above != null)
                    issues.Add(new GraphIssue(GraphIssueCode.ReactionBelowDecision, false, reaction.Id, STATE,
                        $"'{reaction.DisplayName}' (priority {own.Priority}) is less important than " +
                        $"'{above.DisplayName}' ({above.Reasoner.Priority}), whose chain attacks: it never cuts in on " +
                        "that fight — give it a more important priority"));
            }
        }

        /// <summary>A block of the decision's chain attacks (the chain as far as it leads, up to the Brain and the decisions).</summary>
        public static bool ChainAttacks(AgentState decision)
        {
            var seen = new HashSet<AgentState> { decision };
            var next = new Queue<AgentState>();
            next.Enqueue(decision);
            while (next.Count > 0)
            {
                AgentState node = next.Dequeue();
                if (node != decision && node.Tasks.Exists(task => task is AttackTask))
                    return true;

                foreach (Transition arrow in node.Transitions)
                {
                    AgentState to = arrow.Target;
                    if (to != null && !to.IsBrain && to.Reasoner == null && seen.Add(to))
                        next.Enqueue(to);
                }
            }

            return false;
        }

        private static void CheckVariables(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues)
        {
            var declared = new HashSet<string>();
            declared.Add(BehaviorGraphInstance.DELTA_TIME);     // the graph writes it itself, for the On Tick chain

            // What the graph declares: the asset's list, and what an upgrade step declared when the graph was read (an old
            // asset compiled before it is saved — an agent's blackboard gets both)
            foreach (VariableDeclaration declaration in asset.Variables)
            {
                if (!string.IsNullOrEmpty(declaration.Name))
                    declared.Add(declaration.Name);
            }

            foreach (VariableDeclaration declaration in graph.Variables)
            {
                if (declaration != null && !string.IsNullOrEmpty(declaration.Name))
                    declared.Add(declaration.Name);
            }

            graph.VisitNodes(node =>
            {
                foreach (string name in VariablesOf(node))
                {
                    if (!declared.Contains(name))
                        issues.Add(new GraphIssue(GraphIssueCode.UnknownVariable, true, node.Id,
                            GraphSerializer.TypeIdOf(node),
                            $"'{node.DisplayName}' reads '{name}', which the asset does not declare"));
                }
            });
        }

        // Assets the graph points to, followed down: reaching the asset again is a cycle
        private static void CheckCycles(BehaviorGraphAsset asset, List<GraphIssue> issues)
        {
            var path = new List<BehaviorGraphAsset> { asset };
            if (Reaches(asset, asset, path, new HashSet<BehaviorGraphAsset>()))
                issues.Add(new GraphIssue(GraphIssueCode.SubgraphCycle, true, asset.RootId, null,
                    $"assets lead back to the graph: {string.Join(" → ", path.ConvertAll(a => a.name))}"));
        }

        private static bool Reaches(BehaviorGraphAsset from, BehaviorGraphAsset target, List<BehaviorGraphAsset> path,
            HashSet<BehaviorGraphAsset> seen)
        {
            foreach (UnityEngine.Object linked in from.Objects)
            {
                if (linked is not BehaviorGraphAsset next)
                    continue;

                path.Add(next);
                if (next == target || (seen.Add(next) && Reaches(next, target, path, seen)))
                    return true;

                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        /// <summary>The variable names an element reads: its blackboard values bound to a variable, its [VariableName] fields.</summary>
        public static IEnumerable<string> VariablesOf(object element)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type type = element.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(FLAGS | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(string) && field.GetCustomAttribute<VariableNameAttribute>() != null)
                    {
                        if (field.GetValue(element) is string name && !string.IsNullOrEmpty(name))
                            yield return name;
                        continue;
                    }

                    if (!field.FieldType.IsGenericType ||
                        field.FieldType.GetGenericTypeDefinition() != typeof(BlackboardValue<>))
                        continue;

                    object value = field.GetValue(element);
                    var uses = (bool)field.FieldType.GetProperty("UsesVariable").GetValue(value);
                    var variable = (string)field.FieldType.GetProperty("Name").GetValue(value);
                    if (uses && !string.IsNullOrEmpty(variable))
                        yield return variable;
                }
            }
        }
    }
}

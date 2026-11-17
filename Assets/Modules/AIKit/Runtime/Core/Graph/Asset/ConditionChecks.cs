using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// The graph checks of step 90's conditions and values (GraphValidator runs them with its own):
    /// - an Attack Slot reads a group of places nothing in the graph asks for (Take Attack Slot, Circle Target with Wait
    ///   For Slot, Square Off — the blocks whose node says "asks for an attack place") — "mine" never comes, "not mine"
    ///   always holds (NobodyAsksForSlot; a named group an Attack may ask for by its weapon's shot is let pass);
    /// - an Is or a Get asks about the Wired Object with nothing wired into Object — it has no data, never a silent "me"
    ///   (UnwiredValue, spec 10.10).
    /// </summary>
    public static class ConditionChecks
    {
        private const string STATE = "aikit.state";

        public static void Check(BehaviorGraph graph, List<GraphIssue> issues)
        {
            if (graph?.Root == null || issues == null)
                return;

            // The blocks that ask for a place count only where they run: one on an On Tick pin that takes time (or that no
            // chain reaches) never asks (BehaviorGraph.Runs)
            var asked = new HashSet<string>();
            bool attacks = false;
            var states = new List<AgentState>();
            graph.CollectStates(states);
            foreach (AgentState state in states)
            {
                if (state == graph.Root || !graph.Runs(state))
                    continue;

                foreach (AgentTask task in state.Tasks)
                {
                    switch (task)
                    {
                        case TakeAttackSlotTask take:
                            asked.Add(AttackSlots.PoolOf(take.Pool));
                            break;
                        case CircleTargetTask { WaitForSlot: true } circle:
                            asked.Add(AttackSlots.PoolOf(circle.Pool));
                            break;
                        case SquareOffTask:
                            asked.Add(AttackSlots.POOL);
                            break;
                        case AttackTask:
                            attacks = true;
                            break;
                    }
                }
            }

            var slots = new List<AttackSlotCondition>();
            graph.VisitNodes(node =>
            {
                switch (node)
                {
                    case AttackSlotCondition slot:
                        slots.Add(slot);
                        break;
                    case TryGetComponentTask look:
                        CheckLook(look, issues);
                        break;
                    case IsCondition { As: { } cast } condition:
                        CheckMember(condition, cast, condition.Member, flags: true, issues);
                        break;
                    case GetValue { As: { } cast } value:
                        CheckMember(value, cast, value.Member, flags: false, issues);
                        break;
                    case IsCondition { Of: Whose.WiredObject } condition when string.IsNullOrEmpty(condition.Object):
                        issues.Add(new GraphIssue(GraphIssueCode.UnwiredValue, false, node.Id,
                            GraphSerializer.TypeIdOf(node),
                            $"'{node.DisplayName}' asks about the wired object, and nothing is wired into Object: it has no data"));
                        break;
                    case GetValue { Of: Whose.WiredObject } value when string.IsNullOrEmpty(value.Object):
                        issues.Add(new GraphIssue(GraphIssueCode.UnwiredValue, false, node.Id,
                            GraphSerializer.TypeIdOf(node),
                            $"'{node.DisplayName}' reads the wired object, and nothing is wired into Object: it has no data"));
                        break;
                }
            });

            CheckSensorTypes(graph, issues);

            foreach (AttackSlotCondition slot in slots)
            {
                string pool = AttackSlots.PoolOf(slot.Pool);
                if (asked.Contains(pool) || attacks && pool != AttackSlots.POOL)
                    continue;

                string group = pool == AttackSlots.POOL ? "the common group" : $"the group '{pool}'";
                issues.Add(new GraphIssue(GraphIssueCode.NobodyAsksForSlot, false, slot.Id,
                    GraphSerializer.TypeIdOf(slot) ?? STATE,
                    $"nobody asks for an attack slot: '{slot.DisplayName}' reads a place of {group}, and nothing in the " +
                    "graph asks for one (Take Attack Slot, Circle Target, Square Off) — it never changes"));
            }
        }

        // A Try Get Component must name a component type the project has, and take its object by a wire (step 91)
        private static void CheckLook(TryGetComponentTask look, List<GraphIssue> issues)
        {
            if (string.IsNullOrEmpty(look.Type))
                Add(issues, look, $"'{look.DisplayName}' has no type of component to look for: it finds nothing");
            else if (look.ComponentType == null)
                Add(issues, look, $"'{look.DisplayName}' looks for {look.Type}, and no code of the project has that type");
            else if (!AIReadables.IsComponentType(look.ComponentType))
                Add(issues, look, $"'{look.DisplayName}' looks for {AIReadables.Words(look.ComponentType)}, which is not " +
                                  "a component: it finds nothing");

            if (string.IsNullOrEmpty(look.Object))
                issues.Add(new GraphIssue(GraphIssueCode.UnwiredValue, false, look.Id, GraphSerializer.TypeIdOf(look),
                    $"'{look.DisplayName}' looks at the wired object, and nothing is wired into Object: it finds nothing"));
        }

        // A Get reading a component reads a number of the type, an Is a flag (or nothing — the cast itself)
        private static void CheckMember(AgentNode reader, ComponentValue cast, string member, bool flags, List<GraphIssue> issues)
        {
            if (cast.Type == null)
                return;                                 // Try Get Component says so

            if (string.IsNullOrEmpty(member))
            {
                if (!flags)
                    Add(issues, reader, $"'{reader.DisplayName}' reads nothing of the component: pick a number of {cast.TypeWords}");
                return;
            }

            ReadableMember read = AIReadables.Find(cast.Type, member);
            if (read == null)
                Add(issues, reader, $"'{reader.DisplayName}' reads {AIReadables.Spaced(member)}, and {cast.TypeWords} has " +
                                    "no such public number, flag or vector");
            else if (read.IsFlag != flags)
                Add(issues, reader, $"'{reader.DisplayName}' reads {read.Label}, which is " +
                                    $"{(read.IsFlag ? "a flag (an Is reads it)" : "a number (a Get reads it)")}");
        }

        // A sensor looking for objects of a type: one the project has and the AI may know
        private static void CheckSensorTypes(BehaviorGraph graph, List<GraphIssue> issues)
        {
            graph.VisitNodes(node =>
            {
                if (node is not SensorTask { OfType: { Length: > 0 } name } sensor)
                    return;

                Type type = AIReadables.TypeNamed(name);
                if (type == null)
                    Add(issues, sensor, $"'{sensor.DisplayName}' looks for objects of {name}, and no code of the project " +
                                        "has that type: it sees nothing");
                else if (!typeof(UnityEngine.Component).IsAssignableFrom(type))
                    Add(issues, sensor, $"'{sensor.DisplayName}' looks for objects of {AIReadables.Words(type)}, which is " +
                                        "not a component: it sees nothing");
            });
        }

        private static void Add(List<GraphIssue> issues, AgentNode node, string message) =>
            issues.Add(new GraphIssue(GraphIssueCode.BadComponent, false, node.Id, GraphSerializer.TypeIdOf(node), message));
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Step 90 (C15, C16): the tactics nodes of step 87 are gone — each found, booked, walked and acted by itself with its
    /// own numbers. A graph that still has them is read into the new picture of the same behaviour:
    /// - the blocks Pick Up, Take Cover, Use Turret, Load Turret, High Ground become a chain What → Find → Move To (Climb
    ///   To for High Ground) → Interact, the found object written into a Transform variable; the block's old Done arrows
    ///   leave from Interact, its old Failed arrows from Find (Nothing Found);
    /// - Throw (block and decision) is ThrowUpgrade's: it becomes Attack in the same node (a rock is a weapon now);
    /// - the decisions Fetch, Take Cover, Use Turret, Reload Turret, High Ground become Search For with their priority and
    ///   "not from", their need wired as an Is condition.
    /// What does not map (the numbers that are the object's now: Pick Time, Behind, Load Time, the actions) is said in a
    /// Replaced Node warning on the node. The graph opens for editing; saved, it is the new picture.
    /// </summary>
    public sealed class TacticsNodesUpgrade : IGraphUpgrade
    {
        public const string PICK_UP = "aikit.task.pick-up";
        public const string TAKE_COVER = "aikit.task.take-cover";
        public const string USE_TURRET = "aikit.task.use-turret";
        public const string LOAD_TURRET = "aikit.task.load-turret";
        public const string HIGH_GROUND = "aikit.task.high-ground";
        public const string FETCH_DECISION = "aikit.reasoner.fetch";
        public const string TAKE_COVER_DECISION = "aikit.reasoner.take-cover";
        public const string USE_TURRET_DECISION = "aikit.reasoner.use-turret";
        public const string RELOAD_TURRET_DECISION = "aikit.reasoner.reload-turret";
        public const string HIGH_GROUND_DECISION = "aikit.reasoner.high-ground";

        private const string STATE = "aikit.state";

        /// <summary>Where a block the step adds after another stands, from it (the next column of the chain).</summary>
        public static readonly Vector2 NEXT_OFFSET = new(380f, 0f);

        private static readonly HashSet<string> Removed = new()
        {
            PICK_UP, TAKE_COVER, USE_TURRET, LOAD_TURRET, HIGH_GROUND, FETCH_DECISION, TAKE_COVER_DECISION,
            USE_TURRET_DECISION, RELOAD_TURRET_DECISION, HIGH_GROUND_DECISION
        };

        public int Version => 1;

        // ---------- the old data (the removed nodes' fields, by their JSON names) ----------

        [Serializable]
        private sealed class OldDecision
        {
            public int Priority = 1;
            public List<int> NotFrom = new();
            public float Every = -1f;
        }

        [Serializable]
        private sealed class OldBlock
        {
            public string Kind = "";
            public string Target = "";
            public string Turret = "";
            public BlackboardValue<float> SearchRadius = 0f;
            public BlackboardValue<float> MinDistance = 0f;
            public BlackboardValue<float> MaxDistance = 0f;
            public BlackboardValue<float> Pace = 1f;
        }

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues)
        {
            if (asset == null || graph?.Root == null)
                return false;

            bool changed = false;
            foreach (GraphRecord record in asset.Records)
            {
                if (record == null || !Removed.Contains(record.Type))
                    continue;

                GraphRecord holderRecord = HolderOf(asset, record.Id);
                AgentState holder = holderRecord != null && elements.TryGetValue(holderRecord.Id, out object found)
                    ? found as AgentState
                    : null;
                if (holder == null || holder.Tasks.Count > 0)
                    continue;                           // not a node's block (or the node holds another): left as read

                issues?.RemoveAll(issue => issue.Code == GraphIssueCode.UnknownNodeType && issue.RecordId == record.Id ||
                                           issue.Code == GraphIssueCode.DanglingEdge && issue.RecordId == holderRecord.Id &&
                                           issue.Message != null && issue.Message.Contains($"points to {record.Id},"));

                string said = Replace(graph, holder, record);
                issues?.Add(new GraphIssue(GraphIssueCode.ReplacedNode, false, holderRecord.Id, record.Type, said));
                changed = true;
            }

            // The new nodes are numbered away from every record (a Throw record, read by the next step, keeps its own id)
            if (changed)
                graph.AssignIds(GraphUpgrades.RecordIds(asset));
            return changed;
        }

        // The node that holds the record as its block (its record's Tasks link names it)
        private static GraphRecord HolderOf(BehaviorGraphAsset asset, int taskId)
        {
            foreach (GraphRecord record in asset.Records)
            {
                if (record?.Type != STATE)
                    continue;

                foreach (GraphLink link in record.Links)
                {
                    if (link.Field == nameof(AgentState.Tasks) && link.Ids.Contains(taskId))
                        return record;
                }
            }

            return null;
        }

        // The node gets the new picture of the old block or decision; what is said about it
        private static string Replace(BehaviorGraph graph, AgentState holder, GraphRecord record)
        {
            switch (record.Type)
            {
                case FETCH_DECISION:
                    return Decision(holder, record, CharacterState.Carrying,
                        "Fetch is Search For now: wire What / Find / Move To / Interact (Take) behind it; its need — " +
                        "empty hands — is its condition");
                case TAKE_COVER_DECISION:
                    return Decision(holder, record, CharacterState.InCover,
                        "Take Cover (the decision) is Search For now: its need — not in cover — is its condition; " +
                        "\"only with a weapon that shoots\" is a condition of your own now (Is Weapon Shoots)");
                case USE_TURRET_DECISION:
                    return Decision(holder, record, CharacterState.Occupying,
                        "Use Turret (the decision) is Search For now: its need — not at a turret — is its condition");
                case RELOAD_TURRET_DECISION:
                    return Decision(holder, record, null,
                        "Reload Turret is Search For now: its chain finds a turret that offers Load, then a box, and loads it");
                case HIGH_GROUND_DECISION:
                    return Decision(holder, record, CharacterState.Occupying,
                        "High Ground (the decision) is Search For now: its need — on no perch — is its condition; its " +
                        "Every is its Rest; Leave After is the band's slack of the What");
                case PICK_UP:
                {
                    var old = Read<OldBlock>(record);
                    var what = new WhatValue
                    {
                        Tag = old.Kind, Offer = Offer.Take, Radius = Constant(old.SearchRadius, 30f)
                    };
                    Chain(graph, holder, what, "Item", old.Pace, climb: false, Offer.Take, threat: "");
                    return "Pick Up is a chain now: What → Find → Move To → Interact (Take); the time and the action of " +
                           "taking are the object's (Pickable Item, Item Source)";
                }
                case TAKE_COVER:
                {
                    var old = Read<OldBlock>(record);
                    var what = new WhatValue
                    {
                        Component = typeof(Cover).FullName, Offer = Offer.Occupy, Radius = Constant(old.SearchRadius, 15f),
                        FromTarget = true, MinDistance = Constant(old.MinDistance, 5f),
                        MaxDistance = Constant(old.MaxDistance, 18f)
                    };
                    Chain(graph, holder, what, "Cover", old.Pace, climb: false, Offer.Occupy, old.Target);
                    return "Take Cover is a chain now: What (Cover) → Find → Move To → Interact (Occupy); Behind is the " +
                           "Cover's own (a crate needs a Cover component)";
                }
                case USE_TURRET:
                {
                    var old = Read<OldBlock>(record);
                    var what = new WhatValue { Component = typeof(Turret).FullName, Offer = Offer.Use, Radius = 0f };
                    Chain(graph, holder, what, Named(old.Turret, "Turret"), old.Pace, climb: false, Offer.Use, old.Target);
                    return "Use Turret is a chain now: What (Turret, Use) → Find → Move To → Interact (Use) — manned, the " +
                           "Attack decision fires its gun";
                }
                case LOAD_TURRET:
                {
                    var old = Read<OldBlock>(record);
                    var what = new WhatValue { Component = typeof(Turret).FullName, Offer = Offer.Load, Radius = 0f };
                    Chain(graph, holder, what, Named(old.Turret, "Turret"), old.Pace, climb: false, Offer.Load, threat: "");
                    return "Load Turret is a chain now: What (Turret, Load) → Find → Move To → Interact (Load); the time " +
                           "and the action of loading are the turret's";
                }
                case HIGH_GROUND:
                {
                    var old = Read<OldBlock>(record);
                    var what = new WhatValue
                    {
                        Component = typeof(Perch).FullName, Offer = Offer.Occupy, Radius = Constant(old.SearchRadius, 6f),
                        FromTarget = true, MinDistance = Constant(old.MinDistance, 3f),
                        MaxDistance = Constant(old.MaxDistance, 14f)
                    };
                    Chain(graph, holder, what, "Perch", default, climb: true, Offer.Occupy, old.Target);
                    return "High Ground is a chain now: What (Perch) → Find → Climb To → Interact (Occupy)";
                }
                default:
                    return "a removed node";
            }
        }

        // A decision becomes Search For: its priority, its "not from", its need as a condition (when there is an Is)
        private static string Decision(AgentState holder, GraphRecord record, CharacterState? need, string said)
        {
            var old = Read<OldDecision>(record);
            var search = new SearchForReasoner();
            CopyDecision(old, search);
            if (old.Every >= 0f)
                search.Rest = old.Every;
            if (need.HasValue)
            {
                search.Condition = ObjectConditions.Is(need.Value, invert: true);
                if (search.Condition == null)
                    said += " (no Is condition in this build: wire one yourself)";
                else
                    UpgradePlaces.PlaceNear(search.Condition, holder, AttackSlotUpgrade.CONDITION_OFFSET);
            }

            holder.AddTask(search);
            return said;
        }

        private static void CopyDecision(OldDecision old, ReasonerTask into)
        {
            into.Priority = old.Priority;
            into.NotFrom = old.NotFrom != null ? new List<int>(old.NotFrom) : new List<int>();
        }

        // The block's node holds the Find (What wired in); Move To (or Climb To) and Interact follow it; the old Done arrows
        // leave from Interact, the old Failed arrows stay on the Find (its Nothing Found)
        private static void Chain(BehaviorGraph graph, AgentState holder, WhatValue what, string variable,
            BlackboardValue<float> pace, bool climb, Offer offer, string threat)
        {
            Declare(graph, variable);
            holder.AddTask(new FindTask { What = what, StoreIn = variable });

            AgentTask walk = climb
                ? new ClimbToTask { Target = variable }
                : new MoveToTask { Target = variable, Pace = pace.UsesVariable ? pace : Constant(pace, 1f) };
            var interact = new InteractTask { Object = variable, Interaction = offer, Target = threat ?? "" };
            AgentState walking = NodeOf(graph, walk);
            AgentState interacting = NodeOf(graph, interact);

            // The new nodes stand where the chain goes on — to the right of the block they follow, the What under the
            // Find it is wired into — never at (0, 0), on top of each other (UpgradePlaces)
            UpgradePlaces.PlaceNear(walking, holder, NEXT_OFFSET);
            UpgradePlaces.PlaceNear(interacting, walking, NEXT_OFFSET);
            UpgradePlaces.PlaceNear(what, holder, AttackSlotUpgrade.CONDITION_OFFSET);

            // The arrows the old block led on by: its Done (or end) now from the end of the new chain
            var moved = new List<Transition>();
            foreach (Transition arrow in holder.Transitions)
            {
                if (arrow.Trigger == TransitionTrigger.TasksSucceeded || arrow.Trigger == TransitionTrigger.TasksFinished)
                    moved.Add(arrow);
            }

            foreach (Transition arrow in moved)
            {
                holder.Transitions.Remove(arrow);
                interacting.AddTransition(arrow);
            }

            holder.AddTransition(new Transition { Target = walking, Trigger = TransitionTrigger.TasksSucceeded });
            walking.AddTransition(new Transition { Target = interacting, Trigger = TransitionTrigger.TasksSucceeded });
        }

        private static AgentState NodeOf(BehaviorGraph graph, AgentTask task)
        {
            var node = new AgentState(NodeTitles.Of(task));
            node.AddTask(task);
            graph.Root.AddChild(node);
            return node;
        }

        // The variable the Find writes and the blocks after it read (an object variable of that name is reused)
        private static void Declare(BehaviorGraph graph, string name)
        {
            if (graph.Variables.Exists(declared => declared.Name == name))
                return;

            graph.Variables.Add(new VariableDeclaration(name, VariableType.Transform));
        }

        private static string Named(string name, string fallback) => string.IsNullOrEmpty(name) ? fallback : name;

        private static float Constant(BlackboardValue<float> value, float fallback)
        {
            float number = value.Get(null);
            return value.UsesVariable || number <= 0f ? fallback : number;
        }

        private static T Read<T>(GraphRecord record) where T : new()
        {
            var old = new T();
            if (!string.IsNullOrEmpty(record.Data))
                JsonUtility.FromJsonOverwrite(record.Data, old);
            return old;
        }

        private static void SetString(object node, string field, string value) =>
            node.GetType().GetField(field)?.SetValue(node, value ?? "");

        private static void SetNumber(object node, string field, float value)
        {
            System.Reflection.FieldInfo info = node.GetType().GetField(field);
            if (info != null && info.FieldType == typeof(float))
                info.SetValue(node, value);
        }
    }
}

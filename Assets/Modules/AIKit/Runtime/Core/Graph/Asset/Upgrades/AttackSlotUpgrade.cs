using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Step 90 (C7, spec 4.8 / 10.3): the checks of a place to attack from that hid in checkboxes become what the canvas
    /// draws. Read from the old records' data (the fields are gone, so JsonUtility drops them — the step reads them
    /// itself; an absent key is the old default, true):
    /// - Fight (Attack) and Chase with Needs Attack Place → an Attack Slot (mine) wired into the decision's Condition pin;
    /// - Flank with Needs Attack Place → an Attack Slot (mine, someone attacks first), or (mine) when Only When Not First
    ///   was off;
    /// - Circle with Ask For Place → a Take Attack Slot with Wait Time 0 (its Pool) on the decision's On Tick pin, and
    ///   an Attack Slot (not mine, of that pool) in its Condition — the built-in "has a place — does not hold".
    /// A condition the decision has already is kept: the two are joined by an AND. A decision that has its Attack Slot
    /// already is left alone (the step changes nothing on a graph that does not need it). The new nodes stand next to
    /// their decision (UpgradePlaces).
    /// </summary>
    public sealed class AttackSlotUpgrade : IGraphUpgrade
    {
        public const string FIGHT = "aikit.reasoner.fight";
        public const string CHASE = "aikit.reasoner.chase";
        public const string FLANK = "aikit.reasoner.flank";
        public const string CIRCLE = "aikit.reasoner.circle";

        /// <summary>Where a condition the step wires in stands, from its decision.</summary>
        public static readonly Vector2 CONDITION_OFFSET = new(-60f, 170f);

        /// <summary>Where an AND that joins it with a condition the decision had stands, from the decision.</summary>
        public static readonly Vector2 AND_OFFSET = new(-60f, 250f);

        /// <summary>Where the ask on Circle's On Tick stands, from the decision.</summary>
        public static readonly Vector2 ASK_OFFSET = new(420f, -170f);

        public int Version => 1;

        // The old fields, read off a record's data; an absent key keeps the old default
        [Serializable]
        private sealed class OldFlags
        {
            public bool NeedsAttackPlace = true;
            public bool OnlyWhenNotFirst = true;
            public bool AskForPlace = true;
            public string Pool = "";
        }

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues)
        {
            if (asset == null || graph?.Root == null)
                return false;

            bool changed = false;
            foreach (GraphRecord record in asset.Records)
            {
                if (record.Type != FIGHT && record.Type != CHASE && record.Type != FLANK && record.Type != CIRCLE)
                    continue;

                if (!elements.TryGetValue(record.Id, out object element) || element is not ReasonerTask decision)
                    continue;

                AgentState node = StateOf(graph, elements, decision);
                var old = new OldFlags();
                if (!string.IsNullOrEmpty(record.Data))
                    JsonUtility.FromJsonOverwrite(record.Data, old);

                switch (record.Type)
                {
                    case FIGHT:
                    case CHASE:
                        if (old.NeedsAttackPlace)
                            changed |= Require(asset, decision, node, AttackSlotIs.Mine, "");
                        break;
                    case FLANK:
                        // Only When Not First without Needs Attack Place ("somebody else attacks first", no place of its
                        // own) has no Attack Slot to say it — no graph of the lab has it; such a Flank holds on sight now
                        if (old.NeedsAttackPlace)
                        {
                            changed |= Require(asset, decision, node,
                                old.OnlyWhenNotFirst ? AttackSlotIs.MineAndSomeoneAttacksFirst : AttackSlotIs.Mine, "");
                        }

                        break;
                    case CIRCLE:
                        if (old.AskForPlace)
                        {
                            changed |= Require(asset, decision, node, AttackSlotIs.NotMine, old.Pool ?? "");
                            if (node != null)
                                changed |= Ask(graph, node, old.Pool ?? "");
                        }

                        break;
                }
            }

            return changed;
        }

        // The node that holds the decision (the step runs before the graph is linked: found among the read elements)
        private static AgentState StateOf(BehaviorGraph graph, IReadOnlyDictionary<int, object> elements, ReasonerTask decision)
        {
            foreach (AgentState state in graph.Root.Children)
            {
                if (state != null && state.Tasks.Contains(decision))
                    return state;
            }

            foreach (object element in elements.Values)
            {
                if (element is AgentState state && state.Tasks.Contains(decision))
                    return state;
            }

            return null;
        }

        // The decision's Condition gets an Attack Slot (joined by an AND with what it had); true — it changed
        private static bool Require(BehaviorGraphAsset asset, ReasonerTask decision, AgentState node, AttackSlotIs @is,
            string pool)
        {
            if (HasSlotCondition(decision.Condition))
                return false;

            var slot = new AttackSlotCondition(@is, pool);
            if (decision.Condition == null)
            {
                decision.Condition = slot;
                UpgradePlaces.PlaceNear(slot, node, CONDITION_OFFSET);
                return true;
            }

            // An AND of its own takes the slot in — but not an inverted one: "not (tired and my place)" is not "not tired,
            // and my place" (the inverted AND is kept whole under a new AND below)
            AgentCondition had = decision.Condition;
            if (had is AllCondition all && !NotUpgrade.WasInverted(asset, all.Id))
            {
                all.Conditions.Add(slot);
                UpgradePlaces.PlaceNear(slot, node, CONDITION_OFFSET);
                return true;
            }

            var both = new AllCondition(had, slot);
            decision.Condition = both;
            UpgradePlaces.PlaceNear(both, node, AND_OFFSET);
            UpgradePlaces.PlaceNear(slot, both, new Vector2(-200f, 60f));
            return true;
        }

        // Circle's On Tick gets the instant ask (Take Attack Slot, Wait Time 0) of the group it asked from; true — added
        private static bool Ask(BehaviorGraph graph, AgentState circle, string pool)
        {
            foreach (Transition arrow in circle.Transitions)
            {
                if (arrow.Trigger == TransitionTrigger.Tick && arrow.Target != null &&
                    arrow.Target.Tasks.Exists(task => task is TakeAttackSlotTask))
                    return false;                       // it asks already
            }

            var ask = new AgentState(NodeTitles.Of(typeof(TakeAttackSlotTask)));
            ask.AddTask(new TakeAttackSlotTask { Pool = pool, WaitTime = 0f });
            graph.Root.AddChild(ask);
            circle.AddTransition(new Transition { Target = ask, Trigger = TransitionTrigger.Tick });
            UpgradePlaces.PlaceNear(ask, circle, ASK_OFFSET);
            return true;
        }

        /// <summary>The condition is an Attack Slot, or an AND / OR that holds one.</summary>
        public static bool HasSlotCondition(AgentCondition condition)
        {
            switch (condition)
            {
                case null:
                    return false;
                case AttackSlotCondition:
                    return true;
                case AllCondition all:
                    return all.Conditions.Exists(HasSlotCondition);
                case AnyCondition any:
                    return any.Conditions.Exists(HasSlotCondition);
                default:
                    return false;
            }
        }
    }
}

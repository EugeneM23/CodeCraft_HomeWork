using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// 15.11.2026 (the user's word: "every decision node has the same build — what a decision waits for is checked by other
    /// nodes and its Condition pin"): the decisions lost the pins of their own that were conditions. What a graph wired into
    /// them becomes a Compare in the decision's Condition pin (joined by an AND with a condition it had):
    /// - Use Ability's Timer (ready when the timer is 0 or less) → "Timer ≤ 0". The ability's own pause (its Cooldown part)
    ///   is asked now too — it was not while the Timer was wired: the graph is told, to take one of the two pauses away;
    /// - Sleep's Night (night while the variable holds an object) → "Night is there";
    /// - Gather's Called (the flag that calls the squad) → "Called is on".
    /// A decision whose pin was not wired is left as it is (it held never before; it holds always now — the graph is told).
    /// </summary>
    public sealed class DecisionPinsUpgrade : IGraphUpgrade
    {
        public const string USE_ABILITY = "aikit.reasoner.use-ability";
        public const string SLEEP = "aikit.reasoner.sleep";
        public const string GATHER = "aikit.reasoner.gather";

        public int Version => 3;

        // The old pins, read off a record's data (the fields are gone: JsonUtility drops them)
        [Serializable]
        private sealed class OldPins
        {
            public BlackboardValue<float> Timer;
            public string Night;
            public BlackboardValue<bool> Called;
        }

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues)
        {
            if (asset == null || graph?.Root == null)
                return false;

            bool changed = false;
            foreach (GraphRecord record in asset.Records)
            {
                if (record.Type != USE_ABILITY && record.Type != SLEEP && record.Type != GATHER)
                    continue;

                if (!elements.TryGetValue(record.Id, out object element) || element is not ReasonerTask decision ||
                    string.IsNullOrEmpty(record.Data))
                    continue;

                var old = new OldPins();
                JsonUtility.FromJsonOverwrite(record.Data, old);
                AgentCondition condition = record.Type switch
                {
                    USE_ABILITY when old.Timer.UsesVariable =>
                        new CompareCondition(old.Timer.Name, NumberComparison.LessOrEqual, 0f),
                    SLEEP when !string.IsNullOrEmpty(old.Night) =>
                        new CompareCondition(old.Night, VariableType.Transform),
                    GATHER when old.Called.UsesVariable =>
                        new CompareCondition(old.Called.Name, VariableType.Bool),
                    _ => null
                };

                if (condition == null)
                {
                    if (record.Type != USE_ABILITY && WasWiredless(record.Data, record.Type))
                    {
                        issues?.Add(new GraphIssue(GraphIssueCode.OldFormat, false, record.Id, record.Type,
                            $"'{decision.DisplayName}' had nothing wired into its own pin and was never taken; it has no such " +
                            "pin now and holds always — wire what it waits for into its Condition pin"));
                    }

                    continue;
                }

                Require(asset, decision, StateOf(graph, elements, decision), condition);
                changed = true;
                if (record.Type == USE_ABILITY)
                {
                    issues?.Add(new GraphIssue(GraphIssueCode.OldFormat, false, record.Id, record.Type,
                        $"'{decision.DisplayName}': its Timer is a Compare in its Condition pin now, and the ability's own " +
                        "pause (its Cooldown part) is asked too — take one of the two away if one pause is meant"));
                }
            }

            return changed;
        }

        // Sleep / Gather of an older graph whose pin was there but not wired
        private static bool WasWiredless(string data, string type) =>
            type == SLEEP ? data.Contains("\"Night\"") : data.Contains("\"Called\"");

        // The decision's Condition gets the condition (joined by an AND with what it had)
        private static void Require(BehaviorGraphAsset asset, ReasonerTask decision, AgentState node, AgentCondition condition)
        {
            if (decision.Condition == null)
            {
                decision.Condition = condition;
                UpgradePlaces.PlaceNear(condition, node, AttackSlotUpgrade.CONDITION_OFFSET);
                return;
            }

            if (decision.Condition is AllCondition all && !NotUpgrade.WasInverted(asset, all.Id))
            {
                all.Conditions.Add(condition);
                UpgradePlaces.PlaceNear(condition, node, AttackSlotUpgrade.CONDITION_OFFSET);
                return;
            }

            var both = new AllCondition(decision.Condition, condition);
            decision.Condition = both;
            UpgradePlaces.PlaceNear(both, node, AttackSlotUpgrade.AND_OFFSET);
            UpgradePlaces.PlaceNear(condition, both, new Vector2(-200f, 60f));
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
    }
}

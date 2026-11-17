using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Step 90 (C14): Throw is an attack — the rock is a weapon of one move, the weapon decides what happens. A graph of
    /// step 87 with the Throw block or the Throw decision (their node types are gone, so the reader could not make them)
    /// gets, in the same node, an Attack block (its Target kept; the move — the weapon's first) or the Attack decision (its
    /// priority and "not from" kept; its distance 0 — from the weapon in hand). The node keeps its place, its wires and its
    /// arrows; the graph names what was replaced (ReplacedNode).
    /// </summary>
    public sealed class ThrowUpgrade : IGraphUpgrade
    {
        public const string THROW_BLOCK = "aikit.task.throw";
        public const string THROW_DECISION = "aikit.reasoner.throw";

        public int Version => 1;

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues)
        {
            bool changed = false;
            foreach (GraphRecord record in asset.Records)
            {
                bool block = record.Type == THROW_BLOCK;
                if (!block && record.Type != THROW_DECISION || elements.ContainsKey(record.Id))
                    continue;

                AgentState holder = HolderOf(asset, elements, record.Id);
                if (holder == null)
                    continue;

                AgentTask replacement = block ? AttackOf(record) : DecisionOf(record);
                replacement.Id = record.Id;
                holder.AddTask(replacement);
                int id = record.Id;
                issues?.RemoveAll(issue =>
                    issue.Code == GraphIssueCode.UnknownNodeType && issue.RecordId == id ||
                    issue.Code == GraphIssueCode.DanglingEdge && issue.RecordId == holder.Id &&
                    issue.Message != null && issue.Message.Contains($"points to {id},"));
                issues?.Add(new GraphIssue(GraphIssueCode.ReplacedNode, false, holder.Id, record.Type, block
                    ? $"'{holder.DisplayName}': Throw is an Attack now — the weapon in the hand (a rock) throws"
                    : $"'{holder.DisplayName}': the Throw decision is the Attack decision now — within the reach of the weapon " +
                      "in the hand"));
                changed = true;
            }

            return changed;
        }

        // The Attack block: the old Throw's object kept, the move — the weapon's first (a rock has one)
        private static AgentTask AttackOf(GraphRecord record)
        {
            var attack = new AttackTask();
            if (!string.IsNullOrEmpty(record.Data))
                JsonUtility.FromJsonOverwrite(record.Data, attack);     // Name, Target; the throw's own numbers are dropped
            attack.Move = "";
            return attack;
        }

        // The Attack decision: the old one's priority and "not from" kept; the distance from the weapon in hand (0) and no
        // attack place asked (the Throw decision asked for none)
        private static AgentTask DecisionOf(GraphRecord record)
        {
            var decision = new FightReasoner();
            if (!string.IsNullOrEmpty(record.Data))
                JsonUtility.FromJsonOverwrite(record.Data, decision);
            JsonUtility.FromJsonOverwrite("{\"Closer\":0,\"LetGoAfter\":0,\"NeedsAttackPlace\":false}", decision);
            return decision;
        }

        // The node whose block the record was (its Tasks link names it)
        private static AgentState HolderOf(BehaviorGraphAsset asset, IReadOnlyDictionary<int, object> elements, int id)
        {
            foreach (GraphRecord record in asset.Records)
            {
                foreach (GraphLink link in record.Links)
                {
                    if (link.Field == nameof(AgentState.Tasks) && link.Ids.Contains(id) &&
                        elements.TryGetValue(record.Id, out object element) && element is AgentState state)
                        return state;
                }
            }

            return null;
        }
    }
}

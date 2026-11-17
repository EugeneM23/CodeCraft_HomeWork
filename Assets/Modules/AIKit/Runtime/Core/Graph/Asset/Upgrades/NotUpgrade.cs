using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// 15.11.2026 (the user's word: "not — let's remove it"): a condition has no "not" any more — the opposite is a setting
    /// of the condition itself. A condition of an older graph that had "not" ("Invert": true in its data) says the same
    /// thing in its own setting now: an Is — Is Not; a Compare of a flag or an object — the other answer (is off, is not
    /// there), of a number — the opposite comparison (&lt; and ≥, ≤ and &gt;); an Attack Slot "mine" — "not mine" and back.
    /// An AND / OR of one condition passes the "not" into it. One that has no opposite of its own (an AND or an OR of
    /// several, an Attack Slot "mine, I attack first"…) is kept as it is and named as a problem of the graph: its meaning
    /// changed — say it again by hand.
    /// </summary>
    public sealed class NotUpgrade : IGraphUpgrade
    {
        private const string INVERTED = "\"Invert\":true";

        public int Version => 2;

        /// <summary>The record of this id kept "not" in its data (the step that reads it runs after the older steps).</summary>
        public static bool WasInverted(BehaviorGraphAsset asset, int id)
        {
            string data = asset?.Find(id)?.Data;
            return data != null && data.Replace(" ", "").Contains(INVERTED);
        }

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues)
        {
            if (asset == null)
                return false;

            bool changed = false;
            foreach (GraphRecord record in asset.Records)
            {
                if (!WasInverted(asset, record.Id) || !elements.TryGetValue(record.Id, out object element))
                    continue;

                changed = true;
                if (!Turn(element))
                {
                    var node = element as AgentNode;
                    issues?.Add(new GraphIssue(GraphIssueCode.OldFormat, false, record.Id, record.Type,
                        $"'{node?.DisplayName ?? record.Type}' had \"not\", and a condition has no \"not\" any more: it " +
                        "answers the other way now — say the opposite with its own settings (or the conditions inside it)"));
                }
            }

            return changed;
        }

        /// <summary>Says the opposite with the condition's own setting; false — it has none.</summary>
        public static bool Turn(object element)
        {
            switch (element)
            {
                case IsCondition condition:
                    condition.Is = condition.Is == IsCheck.Is ? IsCheck.IsNot : IsCheck.Is;
                    return true;

                case CompareCondition compare:
                    bool number = compare.A != null
                        ? compare.A.Kind != ValueKind.Flag
                        : compare.Kind != VariableType.Bool && compare.Kind != VariableType.Transform;
                    if (!number)
                    {
                        compare.Expected = !compare.Expected;
                        return true;
                    }

                    compare.Comparison = compare.Comparison switch
                    {
                        NumberComparison.Less => NumberComparison.GreaterOrEqual,
                        NumberComparison.LessOrEqual => NumberComparison.Greater,
                        NumberComparison.Greater => NumberComparison.LessOrEqual,
                        _ => NumberComparison.Less
                    };
                    return true;

                // An AND / OR of one condition is that condition: the "not" goes into it
                case AllCondition { Conditions: { Count: 1 } } all:
                    return Turn(all.Conditions[0]);
                case AnyCondition { Conditions: { Count: 1 } } any:
                    return Turn(any.Conditions[0]);

                case AttackSlotCondition slot when slot.Is is AttackSlotIs.Mine or AttackSlotIs.NotMine:
                    slot.Is = slot.Is == AttackSlotIs.Mine ? AttackSlotIs.NotMine : AttackSlotIs.Mine;
                    return true;

                default:
                    return false;
            }
        }
    }
}

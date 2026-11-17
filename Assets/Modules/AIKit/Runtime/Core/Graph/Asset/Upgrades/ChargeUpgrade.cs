using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// Step 90 (C14, spec 10.6): the charge is a move — its numbers are the weapon's Rush, as its dashes are. A Charge node
    /// of a graph of before keeps working: its own numbers are used with a weapon that has no Rush. Nothing in the graph
    /// changes; the graph is told once (the note below), and the upgraded format is saved with it.
    /// </summary>
    public sealed class ChargeUpgrade : IGraphUpgrade
    {
        public int Version => 1;

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues)
        {
            bool any = false;
            foreach (object element in elements.Values)
            {
                if (element is not ChargeTask charge)
                    continue;

                any = true;
                issues?.Add(new GraphIssue(GraphIssueCode.OldFormat, false, charge.Id, "aikit.task.charge",
                    "Charge is a move now: its numbers are the weapon's Rush (the weapon's Charging); the node's own " +
                    "numbers are used only with a weapon that has none"));
            }

            return any;
        }
    }
}

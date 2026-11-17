using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// The problems of a graph's searches (step 90), shown on their nodes: a Find with no What wired in finds nothing; a
    /// Find that writes the found object into no variable gives the blocks after it nothing; a Search For whose chain has
    /// no Find searches for nothing. (An Interact with no object wired in is a pin that takes its value only by a wire:
    /// the graph check of such pins names it.) Run by the graph validator.
    /// </summary>
    public static class ObjectGraphChecks
    {
        private const string STATE = "aikit.state";

        public static void Check(BehaviorGraph graph, List<GraphIssue> issues)
        {
            if (graph?.Root == null || issues == null)
                return;

            var states = new List<AgentState>();
            graph.CollectStates(states);
            foreach (AgentState state in states)
            {
                foreach (AgentTask task in state.Tasks)
                {
                    if (task is not FindTask find)
                        continue;

                    if (find.What == null)
                        issues.Add(new GraphIssue(GraphIssueCode.NothingToSearch, false, state.Id, STATE,
                            $"'{state.DisplayName}' has no What wired in: it finds nothing"));
                    if (string.IsNullOrEmpty(find.StoreIn))
                        issues.Add(new GraphIssue(GraphIssueCode.NothingToSearch, false, state.Id, STATE,
                            $"'{state.DisplayName}' writes what it finds into no variable: the blocks after it get nothing"));
                }

                if (state.Reasoner is SearchForReasoner && !ChainFinds(graph, state))
                    issues.Add(new GraphIssue(GraphIssueCode.NothingToSearch, false, state.Id, STATE,
                        $"'{state.DisplayName}': its chain has no Find — it searches for nothing"));
            }
        }

        // A Find somewhere in the decision's chain (its Do and every block after it)
        private static bool ChainFinds(BehaviorGraph graph, AgentState decision)
        {
            var seen = new HashSet<AgentState>();
            var queue = new Queue<AgentState>();
            queue.Enqueue(decision);
            while (queue.Count > 0)
            {
                AgentState node = queue.Dequeue();
                if (!seen.Add(node))
                    continue;

                if (node != decision && node.Tasks.Exists(task => task is FindTask))
                    return true;

                foreach (Transition arrow in node.Transitions)
                {
                    AgentState next = arrow.Target;
                    if (next != null && next != graph.Brain && next.Reasoner == null)
                        queue.Enqueue(next);
                }
            }

            return false;
        }
    }
}

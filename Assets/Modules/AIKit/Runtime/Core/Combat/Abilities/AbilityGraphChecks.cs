using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// What is wrong with the abilities of a graph (step 90: the abilities are the graph's — what the character's inspector
    /// said about its own list is the graph's problem now): a decision with no ability, a Use Ability block with none
    /// outside the chain of an ability decision (no decision hands it one), Use Any Ability with nothing in its set, two
    /// assets of one name (the character prepares the first one only).
    /// </summary>
    public static class AbilityGraphChecks
    {
        private const string STATE = "aikit.state";

        public static void Check(BehaviorGraph graph, List<GraphIssue> issues)
        {
            if (graph?.Root == null)
                return;

            var states = new List<AgentState>();
            graph.CollectStates(states);
            foreach (AgentState state in states)
            {
                foreach (AgentTask task in state.Tasks)
                {
                    switch (task)
                    {
                        case UseAbilityReasoner { Ability: null }:
                            issues.Add(new GraphIssue(GraphIssueCode.NoAbility, false, state.Id, STATE,
                                $"'{state.DisplayName}' names no ability: it is never taken"));
                            break;

                        case UseAnyAbilityReasoner any when !HasAny(any):
                            issues.Add(new GraphIssue(GraphIssueCode.EmptyAbilitySet, false, state.Id, STATE,
                                $"'{state.DisplayName}' has no abilities wired into its set: it is never taken"));
                            break;

                        case UseAbilityTask { Ability: null } when !InAbilityChain(graph, state):
                            issues.Add(new GraphIssue(GraphIssueCode.NoAbility, false, state.Id, STATE,
                                $"'{state.DisplayName}' names no ability, and no ability decision hands it one " +
                                "(it is used in the chain of Use Ability or Use Any Ability only)"));
                            break;
                    }
                }
            }

            // Two different assets of one name: the character prepares the first one, the other is never used
            var byName = new Dictionary<string, AbilityAsset>();
            graph.VisitNodes(node =>
            {
                if (node is not IUsesAbilities uses)
                    return;

                var named = new List<AbilityAsset>();
                uses.CollectAbilities(named);
                foreach (AbilityAsset ability in named)
                {
                    if (ability == null || string.IsNullOrEmpty(ability.Name))
                        continue;

                    if (!byName.TryGetValue(ability.Name, out AbilityAsset first))
                    {
                        byName[ability.Name] = ability;
                        continue;
                    }

                    if (first != ability)
                        issues.Add(new GraphIssue(GraphIssueCode.AbilityNameClash, true, node.Id,
                            GraphSerializer.TypeIdOf(node),
                            $"two abilities are named '{ability.Name}' ('{first.name}' and '{ability.name}'): a character " +
                            "prepares only the first — rename one"));
                }
            });
        }

        private static bool HasAny(UseAnyAbilityReasoner any)
        {
            if (any.Abilities == null)
                return false;

            foreach (AbilityValue value in any.Abilities)
            {
                if (value?.Ability != null)
                    return true;
            }

            return false;
        }

        // The block is in the chain of a decision that hands its chain an ability
        private static bool InAbilityChain(BehaviorGraph graph, AgentState state)
        {
            ReasonerTask owner = graph.ChainOwnerOf(state)?.Reasoner;
            return owner is UseAbilityReasoner or UseAnyAbilityReasoner;
        }
    }
}

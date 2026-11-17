using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// The abilities of the character are its graph's (step 90, C6 / C23 — no list on the character): when it is given a
    /// graph, it prepares its own runner for every ability the graph names (its Use Ability / Use Any Ability nodes and
    /// their Ability nodes), so two characters with one asset keep their own pause and their own run. An id a game's own
    /// runner was registered under (Abilities.Register — a skill of the game, a demo's part) is left to that runner; an
    /// ability the game plays (a Code Runner delivery) is only described, its runner is the game's. A new graph — the
    /// previous graph's runners go, the new graph's come.
    /// </summary>
    public sealed partial class AICharacter
    {
        private readonly List<AbilityRunner> _abilityRunners = new();
        private static readonly List<AbilityAsset> Named = new();
        private static readonly List<AgentNode> Nodes = new();

        /// <summary>The character's own runners, one per ability its graph names (tests, debugging).</summary>
        public IReadOnlyList<AbilityRunner> AbilityRunners => _abilityRunners;

        // The previous graph's runners go (one a game's runner took over is the game's now), the new graph's come
        private void PrepareAbilities(BehaviorGraph graph)
        {
            for (int i = 0; i < _abilityRunners.Count; i++)
            {
                AbilityRunner old = _abilityRunners[i];
                if (ReferenceEquals(Abilities.RunnerOf(old.Id), old))
                    Abilities.Remove(old.Id);
            }

            _abilityRunners.Clear();
            if (graph == null)
                return;

            Named.Clear();
            Nodes.Clear();
            graph.VisitNodes(Nodes.Add);
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i] is IUsesAbilities uses)
                    uses.CollectAbilities(Named);
            }

            Nodes.Clear();
            foreach (AbilityAsset asset in Named)
            {
                if (asset == null || string.IsNullOrEmpty(asset.Name))
                    continue;

                IAbilityRunner present = Abilities.RunnerOf(asset.Name);
                if (present != null && !(present is AbilityRunner mine && _abilityRunners.Contains(mine)))
                    continue;                       // a game's runner plays it (or one asset of this name is prepared)

                if (asset.IsCodeRunner)
                {
                    // The game plays it: its description now, the game's runner when it registers
                    if (!Abilities.Has(asset.Name))
                    {
                        var definition = new AbilityDefinition();
                        asset.Describe(definition);
                        Abilities.Add(definition);
                    }

                    continue;
                }

                if (present != null)
                    continue;                       // two assets of one name: the first is the character's

                var runner = new AbilityRunner(asset, transform);
                _abilityRunners.Add(runner);
                Abilities.Add(runner.Definition);
                Abilities.Register(runner);
            }

            Named.Clear();
        }
    }
}

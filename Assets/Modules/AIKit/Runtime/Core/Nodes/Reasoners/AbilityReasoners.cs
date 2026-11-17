using System;
using System.Collections.Generic;
using System.Text;

namespace AIKit
{
    /// <summary>
    /// Use Ability: one ability (an asset on the node) is ready for the Brain's target — its pause is over, it is within
    /// its range, the line, the ground and the side hold, a place in its pool is to be had and the body can start it — and
    /// the decision keeps holding while the ability plays. Readiness is a hard condition, asked before the decision is
    /// taken (handbook §16: the ability is data; §17: filter first), so a decision that is taken can act. The pause is the
    /// ability's own (its Cooldown part, counted from its end). A pause of the graph's own is a condition like any other —
    /// a Compare of the timer into the Condition pin (15.11.2026, the user's word: every decision node has the same
    /// build; the Timer pin is gone). The decision hands its ability to its chain (AgentContext.ChosenAbility):
    /// a Use Ability block with no ability of its own uses it.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Use Ability", "Task", "aikit.reasoner.use-ability", Simple = true, Endless = true)]
    public sealed class UseAbilityReasoner : ReasonerTask, IUsesAbilities
    {
        /// <summary>The ability (a project asset, kept in the graph asset's table of objects).</summary>
        [NonSerialized] public AbilityAsset Ability;

        /// <summary>Whose pause the decision waits for, on the node.</summary>
        public override string Requirement =>
            Ability == null ? null
            : Ability.Cooldown > 0f ? $"pause: ability's {Ability.Cooldown:0.#} s" : "pause: none";

        public void CollectAbilities(List<AbilityAsset> abilities)
        {
            if (Ability != null)
                abilities.Add(Ability);
        }

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            TargetFacts facts = query.Facts;
            if (!facts.Sees)
            {
                whyNot = "no target";
                return false;
            }

            if (Ability == null)
            {
                whyNot = "no ability chosen";
                return false;
            }

            AgentAbilities abilities = query.Context?.Abilities;
            string id = Ability.Name;
            AbilityDefinition card = abilities?.DefinitionOf(id);
            if (card == null)
            {
                whyNot = query.Explains ? $"{id} is not prepared on the character" : "not prepared";
                return false;
            }

            // The decision stays taken while the ability plays: its chain runs it to the end
            if (query.IsCurrent && abilities.IsRunning(id))
            {
                whyNot = null;
                return true;
            }

            // Everything the ability asks is asked about the Brain's target now, before the choice: a decision that is
            // taken can act
            AbilityBlock block;
            using (query.Context.AimAt(facts.Target))
                block = abilities.BlockOf(id, query.Context, withPlace: true);

            whyNot = block == AbilityBlock.None ? null : AbilityReasons.Why(block, id, card, facts, abilities, query.Explains);
            return block == AbilityBlock.None;
        }

        // The decision hands its ability to its chain
        public override void Taken(AgentContext context, in TargetFacts facts)
        {
            if (context == null)
                return;

            context.ChosenAbility = Ability;
        }

        public override void LeftCharge(AgentContext context)
        {
            if (context == null)
                return;

            context.ChosenAbility = null;
        }
    }

    /// <summary>
    /// Use Any Ability: one of its own set of abilities (the Ability nodes wired into its Abilities pin) is ready for the
    /// Brain's target — the general decision of handbook §17 (filter → score → strategy → place), next to Use Ability,
    /// the decision of one exceptional move (§17 p. 2). Asking it draws nothing (invariant 6: a question makes no state);
    /// when it is taken it picks one of the ready ones — by weight × score × anti-repeat, a roulette among the best two
    /// (§4.6; the agent's seeded random, so a seed replays it) — and keeps that pick while it is in charge, handing it to
    /// its chain (AgentContext.ChosenAbility) and holding while it plays.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Use Any Ability", "Task", "aikit.reasoner.use-any-ability", Simple = true,
        Endless = true)]
    public sealed class UseAnyAbilityReasoner : ReasonerTask
    {
        private sealed class Mem
        {
            public AbilityAsset Pick;
        }

        /// <summary>The decision's set: Ability nodes wired into its Abilities pin.</summary>
        [NonSerialized] public List<AbilityValue> Abilities = new();

        // The ids of the set, made once (the set of a graph does not change in play) — the abilities are asked by id
        [NonSerialized] private List<string> _ids;
        [NonSerialized] private List<AbilityAsset> _assets;

        public UseAnyAbilityReasoner()
        {
        }

        public UseAnyAbilityReasoner(params AbilityAsset[] abilities)
        {
            foreach (AbilityAsset ability in abilities)
                Abilities.Add(new AbilityValue(ability));
        }

        public override string Requirement => "picks one of its ready abilities by weight (of the best two)";

        /// <summary>The ability this agent's decision picked when it was taken; null — none.</summary>
        public AbilityAsset PickOf(AgentContext context) => Memory<Mem>(context).Pick;

        [NonSerialized] private int _idsOf = -1;

        private List<string> Ids()
        {
            int count = Abilities?.Count ?? 0;
            if (_ids != null && _idsOf == count)
                return _ids;

            _ids ??= new List<string>();
            _assets ??= new List<AbilityAsset>();
            _ids.Clear();
            _assets.Clear();
            _idsOf = count;
            if (Abilities == null)
                return _ids;

            for (int i = 0; i < Abilities.Count; i++)
            {
                AbilityAsset ability = Abilities[i]?.Ability;
                if (ability == null || _ids.Contains(ability.Name))
                    continue;

                _ids.Add(ability.Name);
                _assets.Add(ability);
            }

            return _ids;
        }

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            TargetFacts facts = query.Facts;
            if (!facts.Sees)
            {
                whyNot = "no target";
                return false;
            }

            List<string> ids = Ids();
            if (ids.Count == 0)
            {
                whyNot = "no abilities wired in";
                return false;
            }

            AgentContext context = query.Context;
            AgentAbilities abilities = context?.Abilities;
            if (abilities == null)
            {
                whyNot = "no abilities";
                return false;
            }

            // Its pick plays: the decision stays taken
            AbilityAsset pick = query.IsCurrent ? Memory<Mem>(context).Pick : null;
            if (pick != null && abilities.IsRunning(pick.Name))
            {
                whyNot = null;
                return true;
            }

            bool any;
            using (context.AimAt(facts.Target))
                any = abilities.AnyAvailable(context, withPlace: true, among: ids);
            if (any)
            {
                whyNot = null;
                return true;
            }

            whyNot = query.Explains ? WhyNone(context, abilities, ids, facts) : "none ready";
            return false;
        }

        // Taken: one of the ready ones is picked — the only draw of the decision — and handed to its chain
        public override void Taken(AgentContext context, in TargetFacts facts)
        {
            if (context?.Abilities == null)
                return;

            List<string> ids = Ids();
            string id;
            using (context.AimAt(facts.Target))
                id = context.Abilities.Select(context, withPlace: true, among: ids);

            AbilityAsset pick = null;
            for (int i = 0; id != null && i < ids.Count; i++)
            {
                if (ids[i] == id)
                    pick = _assets[i];
            }

            Memory<Mem>(context).Pick = pick;
            context.ChosenAbility = pick;
        }

        public override void LeftCharge(AgentContext context)
        {
            if (context == null)
                return;

            Memory<Mem>(context).Pick = null;
            context.ChosenAbility = null;
        }

        // Why none of the set is ready — each one's reason (only while somebody looks)
        private static string WhyNone(AgentContext context, AgentAbilities abilities, List<string> ids, in TargetFacts facts)
        {
            var why = new StringBuilder("none ready: ");
            for (int i = 0; i < ids.Count; i++)
            {
                AbilityDefinition card = abilities.DefinitionOf(ids[i]);
                AbilityBlock block;
                using (context.AimAt(facts.Target))
                    block = card == null ? AbilityBlock.Unknown : abilities.BlockOf(ids[i], context, withPlace: true);
                if (i > 0)
                    why.Append("; ");
                why.Append(ids[i]).Append(' ').Append(block == AbilityBlock.None
                    ? "scores nothing"
                    : AbilityReasons.Why(block, ids[i], card, facts, abilities, true));
            }

            return why.ToString();
        }
    }

    // Why an ability is not ready, in plain words (the line the canvas shows under a decision in Play)
    internal static class AbilityReasons
    {
        public static string Why(AbilityBlock block, string id, AbilityDefinition card, in TargetFacts facts,
            AgentAbilities abilities, bool explains)
        {
            if (!explains)
                return "not ready";

            return block switch
            {
                AbilityBlock.Unknown => $"{id} is not prepared on the character",
                AbilityBlock.Resting => $"rests {abilities.RestLeft(id):0.0} s",
                AbilityBlock.Playing => "plays",
                AbilityBlock.TooNear or AbilityBlock.TooFar =>
                    $"target {facts.Distance:0.0} m, needs {card.MinRange:0.#}–{card.MaxRange:0.#}",
                AbilityBlock.NoLine => "no clear line to the target",
                AbilityBlock.InAir => "in the air",
                AbilityBlock.WrongSide => "the target is on the wrong side",
                AbilityBlock.NeedsPlace => "no attack place",
                AbilityBlock.TurnTaken => $"no free place to use {id}",
                AbilityBlock.BodyBusy => "the body is busy",
                AbilityBlock.Unaware => "not aware of the target",
                AbilityBlock.NoTarget => "no target",
                _ => "not now"
            };
        }
    }
}

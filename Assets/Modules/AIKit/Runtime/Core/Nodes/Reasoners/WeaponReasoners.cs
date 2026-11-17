using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Switch Weapon (the decision once called Change Weapon — step 90: the decision and its block are of one sense, so
    /// they share a title): once in a while in a fight — every Every … Every Max seconds, counted while the Brain has a
    /// target — the character takes up another weapon (its chain: Switch Weapon, Next). With Back When Calm, a character
    /// with no target and not holding its first weapon takes the first one up again at once (the bow goes after the
    /// fight). The time to the next change starts anew when the decision is taken; taken, it holds while its chain runs
    /// (a Wait before the switch included), and a switch that has started is never cut short (Switch Weapon's
    /// CommitsFor). It needs two weapons carried — part of what it is, said on its node
    /// (Requirement). The fight is counted once per think in Observe (the only place a decision keeps time, step 90);
    /// the question only reads.
    /// Handbook §17 p. 1 says the stance (close ↔ far) should follow the situation — a distance with a margin and a hold
    /// — not chance; the timer is the user's choice for the lab (both stances shown in turn).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Switch Weapon", "Task", "aikit.reasoner.change-weapon", Simple = true, Endless = true)]
    public sealed class ChangeWeaponReasoner : ReasonerTask
    {
        /// <summary>Without a target it holds too: it takes the first weapon back into the hands.</summary>
        public override bool NeedsTarget => false;

        public override string Requirement => "needs 2 weapons";

        private sealed class Mem
        {
            public bool Started;
            public float Last;              // the agent's time at the last look
            public float Fought;            // seconds with a target since the last change
            public float Due;               // seconds with a target the next change waits for
        }

        /// <summary>Seconds with a target between changes, at least.</summary>
        [Min(0f)] public float Every = 15f;

        /// <summary>Seconds with a target between changes, at most (a random time in between each time).</summary>
        [Min(0f)] public float EveryMax = 30f;

        /// <summary>No target and not the first weapon in the hand — take the first one up again at once.</summary>
        public bool BackWhenCalm = true;

        // Once per think, before any decision is asked: the time of the fight goes on while the Brain has a target
        public override void Observe(AgentContext context, in TargetFacts facts)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            float now = context.LocalTime;
            if (!m.Started)
            {
                m.Started = true;
                m.Last = now;
                m.Due = NextDue(context);
            }

            if (facts.Sees)
                m.Fought += Mathf.Max(0f, now - m.Last);
            m.Last = now;
        }

        // Taken: the time to the next change starts anew (the switch it begins runs to its end anyway — Switch Weapon's
        // CommitsFor — then the Brain chooses again, and this decision no longer holds)
        public override void Taken(AgentContext context, in TargetFacts facts)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            m.Started = true;
            m.Last = context.LocalTime;
            m.Fought = 0f;
            m.Due = NextDue(context);
        }

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            whyNot = null;
            AgentContext context = query.Context;
            if (context == null)
                return false;

            if (context.Rack == null || context.Rack.Carried.Count < 2)
            {
                whyNot = "one weapon only";
                return false;
            }

            // Taken, it holds while its chain runs (a Wait or a Step before the Switch Weapon block included): Taken
            // started the time to the next change anew, so it is not asked again until the chain has come to its end
            if (query.IsCurrent && !query.ChainEnded)
                return true;

            if (!query.Facts.Sees)
            {
                WeaponAsset first = context.Rack.Carried[0];
                if (BackWhenCalm && (context.Rack.Own ?? context.Weapon) != first)   // own weapon: a rock is none of them
                    return true;

                whyNot = "no target";
                return false;
            }

            Mem m = Memory<Mem>(context);
            if (m.Started && m.Fought >= m.Due)
                return true;

            whyNot = !query.Explains ? null
                : m.Started ? $"next change in {m.Due - m.Fought:0} s" : "the fight has just begun";
            return false;
        }

        private float NextDue(AgentContext context)
        {
            float low = Mathf.Min(Every, EveryMax);
            float high = Mathf.Max(Every, EveryMax);
            return low + (float)context.NextRandom() * (high - low);
        }
    }

    /// <summary>
    /// Kite: the Brain has a target and the weapon in the hand shoots — keep away from the target and shoot it (its
    /// chain: Kite, then Attack). With a weapon that does not shoot it is never taken, so the same graph fights close
    /// with a blade and from afar with a bow; which weapon is in the hand is Switch Weapon's business. The weapon that
    /// shoots is part of what the decision is (a check of its own, not a wired condition: spec 10.2 — else the
    /// swordsmen of a graph where Kite comes before the attack would start to run from the player), and its node says
    /// so (Requirement).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Kite", "Task", "aikit.reasoner.kite", Simple = true, Endless = true)]
    public sealed class KiteReasoner : ReasonerTask
    {
        /// <summary>
        /// Taken only when the target is this near, metres (0 — at any distance). An archer in a squad's line kites only
        /// when a foe comes close and keeps its slot otherwise (handbook §20a.2: out of the ranks to stay alive, then back).
        /// </summary>
        [Min(0f)] public float Within;

        /// <summary>…and let go when the target is farther than this (with Within set; not below Within).</summary>
        [Min(0f)] public float LetGoAfter;

        public override string Requirement => "only with a weapon that shoots";

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            if (!Reasons.Sees(query.Facts, out whyNot))
                return false;

            if (query.Context?.Weapon is not { Ranged: true })
            {
                whyNot = "my weapon does not shoot";
                return false;
            }

            if (Within <= 0f)
                return true;

            float limit = query.IsCurrent ? Mathf.Max(Within, LetGoAfter) : Within;
            if (query.Facts.Distance > limit)
            {
                whyNot = query.Explains ? $"target {query.Facts.Distance:0.0} m, needs {limit:0.0} or closer" : "too far";
                return false;
            }

            return true;
        }
    }
}

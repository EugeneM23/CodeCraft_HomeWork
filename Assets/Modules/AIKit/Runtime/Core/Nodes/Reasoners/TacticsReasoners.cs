using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Charge: the target has run far — farther than Farther, nearer than Within — and the last charge is Every seconds
    /// behind (handbook §19.1: a strong move is gated, it never comes twice in a row; §16: counted from its end). Its chain
    /// is the Charge node (with whatever comes before it — a Wait, a Turn To Target): taken, the decision holds while its
    /// chain runs, the Charge node holds the agent to the end of the charge; the chain over, the decision lets go, and the
    /// melee decisions take the agent again.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Charge", "Task", "aikit.reasoner.charge", Simple = true, Endless = true)]
    public sealed class ChargeReasoner : ReasonerTask
    {
        private sealed class Mem
        {
            public bool Charged;
            public float RestUntil = float.NegativeInfinity;
        }

        /// <summary>Metres: the target farther than this — charge.</summary>
        [Min(0f)] public float Farther = 8f;

        /// <summary>
        /// Metres: the target nearer than this — a charge can reach it. 0 — as far as the weapon in the hand rushes (its
        /// Rush — step 90: 0 is "from the weapon in hand"); with no Rush on it the decision is not taken.
        /// </summary>
        [Min(0f)] public float Within = 25f;

        /// <summary>Seconds after a charge before the next one.</summary>
        [Min(0f)] public float Every = 6f;

        /// <summary>Within 0 is the weapon's, on the node.</summary>
        public override string Requirement => Within <= 0f ? "within: the weapon's rush" : null;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            TargetFacts facts = query.Facts;
            if (!Reasons.Sees(facts, out whyNot))
                return false;

            AgentContext context = query.Context;
            if (context == null)
                return false;

            float within = Within;
            if (within <= 0f)
            {
                WeaponRush rush = context.Weapon?.RushOrNone;
                if (rush == null)
                {
                    whyNot = "my weapon does not charge";
                    return false;
                }

                within = rush.Distance;
            }

            Mem m = Memory<Mem>(context);
            if (query.IsCurrent)
            {
                // Taken, it holds while its chain runs — a Wait or a Turn To Target before the Charge block included (the
                // Charge block holds the agent by itself only from its first tick); its chain over, it lets go
                if (!query.ChainEnded)
                    return true;

                whyNot = "charged just now";
                return false;
            }

            float rest = m.RestUntil - context.LocalTime;
            if (rest > 0f)
            {
                whyNot = query.Explains ? $"next charge in {rest:0.0} s" : null;
                return false;
            }

            if (facts.Distance < Farther)
            {
                whyNot = query.Explains ? $"target {facts.Distance:0.0} m, needs {Farther:0.#} or farther" : null;
                return false;
            }

            if (facts.Distance > within)
            {
                whyNot = query.Explains ? $"target {facts.Distance:0.0} m, needs {within:0.#} or nearer" : null;
                return false;
            }

            return true;
        }

        public override void InCharge(AgentContext context, in TargetFacts facts)
        {
            if (context != null)
                Memory<Mem>(context).Charged = true;
        }

        // The charge is over (another decision was taken, or none): the rest starts from its end
        public override void LeftCharge(AgentContext context)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            if (!m.Charged)
                return;

            m.Charged = false;
            m.RestUntil = context.LocalTime + Every;
        }
    }
}

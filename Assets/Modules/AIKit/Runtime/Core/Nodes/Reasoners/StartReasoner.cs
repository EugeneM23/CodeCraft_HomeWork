using System;

namespace AIKit
{
    /// <summary>
    /// Start: the first thing the character does — for the simplest behavior, blocks in a line behind its Do (step 92, the
    /// user's word: "a reasoner that works as a start, for a primitive AI of nodes in a line"). It holds until the Brain
    /// lets go of it for the first time — its line came to its end, a more important decision took over, or the graph
    /// stopped (the character was switched off) — and never again in this life of the character: a line runs once, a loop
    /// is an arrow from its last block back to its first. A behavior given anew (the agent's memory starts from nothing)
    /// starts it again. With a more important decision holding at the start it waits its turn, then runs its line once
    /// (handbook §3.1: the root takes the first child whose entry holds; §29а: whether it ran is progress, a save keeps it).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Start", "Task", "aikit.reasoner.start", Simple = true, Endless = true, Order = 0)]
    public sealed class StartReasoner : ReasonerTask
    {
        /// <summary>Why it does not hold once let go (the canvas in Play shows it under the decision).</summary>
        public const string SPENT = "done — runs once";

        public override bool NeedsTarget => false;

        public override string Requirement => "once: never taken again once its line ends or is cut short";

        /// <summary>The Brain has let go of it for this agent: it never holds again in this life of the agent.</summary>
        public bool IsSpentFor(AgentContext context) => context != null && Memory<Mem>(context).Spent;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            // Its line has come to its end: it lets go now (and LeftCharge makes it spent)
            if (query.ChainEnded || IsSpentFor(query.Context))
            {
                whyNot = SPENT;
                return false;
            }

            whyNot = null;
            return true;
        }

        // The Brain let go of it — its line ended, another decision won, the graph stopped: spent for this life (the Brain's
        // own act, as a reaction's; Holds only reads it — invariant 6)
        public override void LeftCharge(AgentContext context)
        {
            if (context != null)
                Memory<Mem>(context).Spent = true;
        }

        private sealed class Mem
        {
            public bool Spent;
        }
    }
}

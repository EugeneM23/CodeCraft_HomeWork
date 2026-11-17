using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>What the Attack Slot condition asks about the agent's place to attack the Brain's target from.</summary>
    public enum AttackSlotIs
    {
        /// <summary>The agent holds a place (of the group) to attack the target from.</summary>
        Mine,

        /// <summary>It holds one, and nobody who holds one asked before it: it attacks first (goes straight in).</summary>
        MineAndIAttackFirst,

        /// <summary>It holds one, and another holder asked before it: somebody else attacks first (it comes round).</summary>
        MineAndSomeoneAttacksFirst,

        /// <summary>It holds none: it waits for one.</summary>
        NotMine
    }

    /// <summary>
    /// Attack Slot (step 90, C7 — the check that hid in the Needs Attack Place / Only When Not First checkboxes of Fight,
    /// Chase and Flank and in the Circle decision, now on the canvas): does the agent hold a place to attack the Brain's
    /// target from — mine / mine, I attack first / mine, someone attacks first / not mine — in a group of places (Pool,
    /// empty — the common one). Wire it into a decision's Condition pin (Attack: mine; Chase: mine; Flank: mine, someone
    /// attacks first; Circle: not mine) or a Branch (handbook §17: token availability is a hard filter before the choice;
    /// §4.3–4.4: a named veto; B.2: "in strike radius AND has a token"). A pure question (invariant 6): it only looks at
    /// the target's engagement (AgentRoster.FindEngagement) — it never makes one, never joins the agent to a group, never
    /// asks for a place; asking is a block (Take Attack Slot, on a decision's On Tick). Outside any group (no roster)
    /// nothing limits the attack: the place counts as the agent's, and nobody attacks before it. No target to ask about —
    /// no data. Which target: the Brain's — the one the decision is asked about, or the one its chain works with.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Attack Slot", "Condition", "aikit.condition.attack-slot", Simple = true)]
    public sealed class AttackSlotCondition : AgentCondition, IExplainedCondition
    {
        /// <summary>What it asks about the agent's place.</summary>
        public AttackSlotIs Is = AttackSlotIs.Mine;

        /// <summary>The group of places it asks about: the one the asking block names (empty — the common one).</summary>
        [PoolName] public string Pool = "";

        public AttackSlotCondition()
        {
        }

        public AttackSlotCondition(AttackSlotIs @is)
        {
            Is = @is;
        }

        public AttackSlotCondition(AttackSlotIs @is, string pool)
        {
            Is = @is;
            Pool = pool ?? "";
        }

        /// <summary>
        /// The mark on the node: the question is about the Brain's target (no pin — the target is the one the decision is
        /// asked about, or the one its chain works with).
        /// </summary>
        public override string Requirement => "target: Brain's";

        protected override bool Evaluate(AgentContext context) => Answer(context) == Truth.Yes;

        protected override Truth Answer(AgentContext context)
        {
            if (!Look(context, out bool mine, out bool first))
                return Truth.NoData;

            bool holds = Is switch
            {
                AttackSlotIs.Mine => mine,
                AttackSlotIs.MineAndIAttackFirst => mine && first,
                AttackSlotIs.MineAndSomeoneAttacksFirst => mine && !first,
                _ => !mine
            };
            return holds ? Truth.Yes : Truth.No;
        }

        // Where the agent stands at the target now: holds a place, is the first of the holders. False — no target to ask
        // about. Looks only: no engagement is made and nobody joins anything
        private bool Look(AgentContext context, out bool mine, out bool first)
        {
            mine = first = false;
            Transform target = context?.BrainsTarget;
            if (target == null)
                return false;

            if (context.Roster == null)
            {
                mine = first = true;                    // no group: nothing limits the attack
                return true;
            }

            string pool = AttackSlots.PoolOf(Pool);
            Engagement engagement = context.Roster.FindEngagement(target);
            mine = engagement != null && engagement.Tokens.Holds(context.Agent, pool);
            first = mine && AttackSlots.Leader(context, target, pool) == null;
            return true;
        }

        public string Phrase(AgentContext context)
        {
            string group = string.IsNullOrEmpty(Pool) ? "" : $" ({Pool})";
            return Is switch
            {
                AttackSlotIs.Mine => $"my attack slot{group}",
                AttackSlotIs.MineAndIAttackFirst => $"my attack slot{group}, I attack first",
                AttackSlotIs.MineAndSomeoneAttacksFirst => $"my attack slot{group}, someone attacks first",
                _ => $"no attack slot of mine{group}"
            };
        }

        public string WhyNoData(AgentContext context) => "no target";
    }
}

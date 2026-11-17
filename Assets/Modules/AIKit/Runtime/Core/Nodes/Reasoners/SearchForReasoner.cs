using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Search For (step 90, C15 — "the brain decides to search"): the decision to go and get something from the world —
    /// a rock to throw, a cover to shoot from, a turret to man, a perch. It holds on need: its Condition pin says why
    /// ("I hold no picked weapon", "I am not in cover"), asked first (a cheap gate before anything else); with nothing
    /// wired it holds whenever it is not resting. It does not look for objects itself — what to look for and how is its
    /// chain's (What → Find → Move To → Interact): it holds while its chain runs. It rests Rest seconds after it lets go
    /// (another decision was taken) and after its chain ended in Nothing Found — no search over and over, think after
    /// think (handbook §18а.3: no relocation again and again). Whatever its chain claimed is let go when it leaves.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Objects/Search For", "Task", "aikit.reasoner.search-for", Simple = true, Endless = true)]
    public sealed class SearchForReasoner : ReasonerTask
    {
        private sealed class Mem
        {
            public bool InCharge;
            public float TakenAt = float.NegativeInfinity;
            public float RestUntil = float.NegativeInfinity;
        }

        /// <summary>Seconds it is not taken after it lets go, or after its chain ended finding nothing.</summary>
        [Min(0f)] public float Rest = 2f;

        /// <summary>Its need is its wired condition (cheap) — asked before anything else of it.</summary>
        public override bool AsksConditionFirst => true;

        /// <summary>A search for a rock holds while patrolling too: a target is the Find's business (a band), not its own.</summary>
        public override bool NeedsTarget => false;

        public override string Requirement => $"rests {Rest:0.#} s after it lets go or finds nothing";

        /// <summary>The agent rests from this search until then (its own clock); below its LocalTime — it does not.</summary>
        public float RestsUntil(AgentContext context) => Memory<Mem>(context).RestUntil;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            whyNot = null;
            AgentContext context = query.Context;
            if (context == null)
                return true;

            Mem m = Memory<Mem>(context);
            if (query.IsCurrent && m.InCharge && ChainRuns(context))
                return true;                            // its chain is at work: it is not cut short by its own rest

            float rest = m.RestUntil - context.LocalTime;
            if (rest <= 0f)
                return true;

            whyNot = query.Explains ? $"rests {rest:0.0} s" : "rests";
            return false;
        }

        /// <summary>Its chain ended finding nothing since it was taken: it rests from that moment.</summary>
        public override void Observe(AgentContext context, in TargetFacts facts)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            if (!m.InCharge || context.NothingFoundAt < m.TakenAt || ChainRuns(context))
                return;

            m.RestUntil = Mathf.Max(m.RestUntil, context.NothingFoundAt + Rest);
            m.TakenAt = float.PositiveInfinity;         // this search's "nothing" is counted once
        }

        public override void Taken(AgentContext context, in TargetFacts facts)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            m.InCharge = true;
            m.TakenAt = context.LocalTime;
        }

        /// <summary>Let go: it rests from now, and whatever its chain claimed is let go.</summary>
        public override void LeftCharge(AgentContext context)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            if (m.InCharge)
                m.RestUntil = Mathf.Max(m.RestUntil, context.LocalTime + Rest);
            m.InCharge = false;
            context.WorldObjects.ReleaseAll(context.Agent);
        }

        // The agent's chain is at work: it stands in a block that has not finished (or in the decision, about to lead on).
        // A finished block it still stands in is the end of the chain — no arrow led on from it
        private static bool ChainRuns(AgentContext context)
        {
            AgentState now = context.Agent != null ? context.Agent.Graph?.Current : null;
            if (now == null || now.IsBrain)
                return false;

            return now.Reasoner != null || !now.HasForegroundTasks || !now.TasksFinishedFor(context) ||
                   now.HasRepeatingTasks && !now.AnyTaskFailedFor(context);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>What the Brain hands a reasoner when it asks "do you hold now?".</summary>
    public readonly struct ReasonerQuery
    {
        public readonly AgentContext Context;
        public readonly TargetFacts Facts;

        /// <summary>The agent acts on this decision now: a threshold with slack lets go only at its far end.</summary>
        public readonly bool IsCurrent;

        /// <summary>
        /// Somebody looks at the verdicts (the canvas in Play): a decision that does not hold writes out why, with its
        /// numbers. Off — it only answers, and a think allocates nothing.
        /// </summary>
        public readonly bool Explains;

        /// <summary>
        /// The decision is the current one and its chain has come to its end — a block done with no arrow leading on, or
        /// an arrow back to the Brain: the Brain chooses again now. A decision that holds only "until its chain ends" (a
        /// reaction, step 90) lets go here; the others are asked as usual.
        /// </summary>
        public readonly bool ChainEnded;

        public ReasonerQuery(AgentContext context, TargetFacts facts, bool isCurrent, bool explains = true,
            bool chainEnded = false)
        {
            Context = context;
            Facts = facts;
            IsCurrent = isCurrent;
            Explains = explains;
            ChainEnded = isCurrent && chainEnded;
        }
    }

    /// <summary>How one decision stood at the Brain's last choice — for the canvas in Play (handbook §33.1).</summary>
    public readonly struct ReasonerVerdict
    {
        public readonly AgentState State;
        public readonly bool Holds;
        public readonly bool Won;

        /// <summary>Why it did not hold ("target 6.2 m, needs 1.9 or closer", "not from Fight"); null — it held.</summary>
        public readonly string WhyNot;

        public ReasonerVerdict(AgentState state, bool holds, bool won, string whyNot)
        {
            State = state;
            Holds = holds;
            Won = won;
            WhyNot = whyNot;
        }
    }

    /// <summary>
    /// A decision the Brain can take — a node wired to the Brain. It says when it holds (its own settings, in plain
    /// words), how important it is (Priority: 1 — the most important) and which decisions it cannot be taken from.
    /// What the agent does once it is taken is the chain of ready blocks behind its Do pin (handbook §0.1: deciding and
    /// acting are layers apart). The task itself only holds the agent while the chain runs.
    /// </summary>
    [Serializable]
    public abstract class ReasonerTask : AgentTask
    {
        /// <summary>
        /// 1 — the most important. Two decisions of one Brain with the same number are a problem the canvas shows;
        /// between them the current one keeps, else the one made earlier.
        /// </summary>
        [HideInInspector] public int Priority = 1;

        /// <summary>The ids of the decisions (reasoner states) this one cannot be taken from. Empty — from any.</summary>
        [HideInInspector] public List<int> NotFrom = new();

        /// <summary>
        /// The decision's extra requirement, wired on the canvas into its Condition pin (an Attack Slot, an Is, a Compare,
        /// AND / OR — step 90): the decision is a candidate only while its own settings hold AND this answers yes. Asked
        /// after the own settings (the cheap gate first, handbook §3.2), with the agent aimed at the Brain's target. Null
        /// — nothing wired: no extra requirement.
        /// </summary>
        [NonSerialized] public AgentCondition Condition;

        /// <summary>The decision holds now; whyNot — the first thing that does not, with its value, for the canvas.</summary>
        public abstract bool Holds(in ReasonerQuery query, out string whyNot);

        /// <summary>
        /// What the Brain asks: the decision's own settings, then its wired condition. whyNot — the first thing that does
        /// not hold ("only if … — no" for the condition).
        /// </summary>
        public bool HoldsWithCondition(in ReasonerQuery query, out string whyNot)
        {
            // A decision whose own question looks into the world (a search) asks the cheap wired condition first
            // (handbook §3.2 p.6, invariant 5)
            if (AsksConditionFirst && !ConditionSays(query, out whyNot))
                return false;

            if (!Holds(query, out whyNot))
                return false;

            return AsksConditionFirst || ConditionSays(query, out whyNot);
        }

        /// <summary>
        /// The wired condition is asked before the decision's own settings: on for a decision whose own question is a
        /// query of the world (Search For), so the cheap requirement gates the expensive one. Off by default.
        /// </summary>
        public virtual bool AsksConditionFirst => false;

        // The wired condition with the agent aimed at the Brain's target: yes — holds; no or no data — does not
        private bool ConditionSays(in ReasonerQuery query, out string whyNot)
        {
            whyNot = null;
            if (Condition == null)
                return true;

            Truth answer;
            AgentContext context = query.Context;
            if (context != null)
            {
                using (context.AimAt(query.Facts.Target))
                {
                    answer = Condition.Ask(context);
                    if (answer != Truth.Yes && query.Explains)
                    {
                        whyNot = Explain(context, answer);
                        return false;
                    }
                }
            }
            else
            {
                answer = Condition.Ask(null);
            }

            if (answer == Truth.Yes)
                return true;

            whyNot = answer == Truth.NoData
                ? query.Explains ? $"only if {NodeTitles.Of(Condition)} — no data" : "condition has no data"
                : query.Explains ? $"only if {NodeTitles.Of(Condition)} — no" : "condition says no";
            return false;
        }

        // The verdict of a wired condition that does not say yes, in plain words (only while somebody looks): what it
        // asks — its own words, when it has them ("only if Target is blocking — no") — and why it has no data
        private string Explain(AgentContext context, Truth answer)
        {
            if (Condition is not IExplainedCondition explained)
                return answer == Truth.NoData
                    ? $"only if {NodeTitles.Of(Condition)} — no data"
                    : $"only if {NodeTitles.Of(Condition)} — no";

            string phrase = explained.Phrase(context);
            if (answer != Truth.NoData)
                return $"only if {phrase} — no";

            string why = explained.WhyNoData(context);
            return string.IsNullOrEmpty(why) ? $"only if {phrase} — no data" : $"only if {phrase} — no data ({why})";
        }

        /// <summary>
        /// Taken out of turn (a reaction, step 90): when it starts to hold it wins at once, without waiting out the
        /// minimum time of the decision in charge — but never in the middle of a blow or an ability (handbook §18.2 p.3,
        /// §20б.4 p.1). Off for ordinary decisions.
        /// </summary>
        public virtual bool OutOfTurn => false;

        /// <summary>
        /// The Brain has just handed the agent to this decision (its chain starts, or starts again): what it hands its
        /// chain is set here (an ability decision picks the ability). Nothing by default.
        /// </summary>
        public virtual void Taken(AgentContext context, in TargetFacts facts)
        {
        }

        /// <summary>
        /// Once per think, for every decision wired to the Brain, before any of them is asked (step 90; handbook §3.2: one
        /// re-check per think, in a fixed order): the only place a decision may keep track of time and events — a
        /// reaction catches an event on its rising edge and rolls its chance here, Change Weapon counts the fight here.
        /// Holds only reads what Observe kept (invariant 6: a question makes no state). Nothing by default.
        /// </summary>
        public virtual void Observe(AgentContext context, in TargetFacts facts)
        {
        }

        /// <summary>
        /// The decision is about the Brain's Target (Fight, Chase…): with no Target wired into the Brain it never holds,
        /// and the graph check names the loose pin. Off — it holds without one (Patrol, Daily Routine, Sleep).
        /// </summary>
        public virtual bool NeedsTarget => true;

        /// <summary>
        /// Once per think while the agent acts on this decision — its chain runs, whatever block of it is on — before the
        /// blocks' own arrows: what the decision keeps up as long as it is in charge. Nothing by default (step 90: what a
        /// decision keeps up is better drawn — a block on its On Tick pin, as Circle asks for a place with Take Attack Slot).
        /// </summary>
        public virtual void InCharge(AgentContext context, in TargetFacts facts)
        {
        }

        /// <summary>The agent acts on this decision no more (another one was taken, none, or the graph stopped).</summary>
        public virtual void LeftCharge(AgentContext context)
        {
        }

        /// <summary>The decision cannot be taken while the agent acts on this one.</summary>
        public bool IsBlockedFrom(AgentState current) => current != null && NotFrom != null && NotFrom.Contains(current.Id);

        protected override void OnEnter(AgentContext context)
        {
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
    }
}

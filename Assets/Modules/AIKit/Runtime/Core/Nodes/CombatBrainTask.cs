using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The Brain: the node a graph starts in and the only thing that decides what the character does. The decisions
    /// (reasoner nodes) wired to it are its choices; once per think it takes the most important one that holds, and that
    /// decision's chain of blocks runs (handbook §0.1: deciding and acting are layers apart; §1.2: transitions by
    /// priority). Its rules, shown on its node: a decision just taken is kept at least
    /// BehaviorGraphInstance.MIN_DECISION_TIME, never switched in the middle of a blow or an ability (§18.2), a chain
    /// that has ended hands the choice back, and one that did nothing rests EMPTY_CHAIN_REST. The Brain thinks about one
    /// object — its Target, the one Detection writes, wired in by a visible wire; the blocks of a decision's chain with
    /// no Target of their own work with it. With no Target wired, the decisions that need one never hold and the others
    /// do. The task itself only waits: with no decision that holds the agent stands.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Brain/Brain", "Task", "aikit.task.combat-brain", Simple = true, Endless = true, Order = 10)]
    public class CombatBrainTask : AgentTask
    {
        /// <summary>
        /// The object the Brain thinks about (the one Detection writes). A data pin on the canvas: it comes in only by
        /// a wire. No wire — nothing to decide about: the Brain stands and the graph shows the problem.
        /// </summary>
        [VariableName(VariableType.Transform)] public string Target;

        [NonSerialized] private BlackboardVariable<Transform> _target;

        private sealed class Mem
        {
            public int Stamp = -1;
            public TargetFacts Facts = TargetFacts.None;
            public Transform SeenTarget;
            public float SeenSince;
            public readonly List<ReasonerVerdict> Verdicts = new();
        }

        /// <summary>
        /// The decisions (reasoners) are wired to this Brain: it chooses among them by priority, their conditions and
        /// their "not from" lists.
        /// </summary>
        public static bool HasReasoners(AgentState brain)
        {
            for (int i = 0; i < brain.Transitions.Count; i++)
            {
                if (brain.Transitions[i].Target?.Reasoner != null)
                    return true;
            }

            return false;
        }

        /// <summary>The facts about the Brain's Target in this think (counted once per think, nothing is written).</summary>
        public static TargetFacts FactsOf(AgentState brain, AgentContext context)
        {
            CombatBrainTask own = Of(brain);
            if (own == null || context == null)
                return TargetFacts.None;

            Mem mem = own.Memory<Mem>(context);
            int stamp = context.ThinkStamp;
            if (mem.Stamp != stamp || stamp == 0)
            {
                mem.Stamp = stamp;
                Transform seen = string.IsNullOrEmpty(own.Target) ? null : own._target.Get(context.Blackboard, own.Target);
                Transform target = own.ThinkAbout(brain, context, seen);
                if (target != mem.SeenTarget)
                {
                    mem.SeenTarget = target;               // a new target (or none): its time starts now
                    mem.SeenSince = context.LocalTime;
                }

                mem.Facts = TargetFacts.Of(context, target, context.LocalTime - mem.SeenSince);
            }

            return mem.Facts;
        }

        /// <summary>
        /// The facts the Brain counted at its last think, as they are — never counted anew (the character's status reads
        /// "in combat" here for whoever asks, between thinks too: a question makes no state). None — it has not thought.
        /// </summary>
        public static TargetFacts LastFactsOf(AgentState brain, AgentContext context)
        {
            CombatBrainTask own = Of(brain);
            return own != null && context != null ? own.Memory<Mem>(context).Facts : TargetFacts.None;
        }

        /// <summary>How every decision stood at this Brain's last choice (empty — it has not chosen yet).</summary>
        public static IReadOnlyList<ReasonerVerdict> VerdictsOf(AgentState brain, AgentContext context) =>
            VerdictListOf(brain, context) ?? (IReadOnlyList<ReasonerVerdict>)Array.Empty<ReasonerVerdict>();

        internal static List<ReasonerVerdict> VerdictListOf(AgentState brain, AgentContext context)
        {
            CombatBrainTask own = Of(brain);
            return own != null && context != null ? own.Memory<Mem>(context).Verdicts : null;
        }

        /// <summary>
        /// Once per think, before any decision is asked: what the Brain thinks about, given what its Target pin brings
        /// (seen). The Brain of a character thinks about what it sees, and hands it to its squad if it marches in one (the
        /// squad goes for what its members see).
        /// </summary>
        protected virtual Transform ThinkAbout(AgentState brain, AgentContext context, Transform seen)
        {
            context.Squad?.Saw(context, seen);
            return seen;
        }

        /// <summary>The Brain task of the state (a Brain or a Squad Brain); null — the state is no Brain.</summary>
        public static CombatBrainTask Of(AgentState brain)
        {
            for (int i = 0; brain != null && i < brain.Tasks.Count; i++)
            {
                if (brain.Tasks[i] is CombatBrainTask own)
                    return own;
            }

            return null;
        }

        protected override void OnEnter(AgentContext context)
        {
            context?.Navigation?.Clear();
            context?.Actuator?.Stop();

            // The character joins the scene's group when its Brain starts (unless the game gave it a world): from then on
            // the questions about attack places (the Attack Slot condition) find the group without joining it themselves
            // — a question makes no state (invariant 6), as a sensor that listens joins the scene's bus on its start
            if (context != null)
                AIWorldRunner.Join(context.Agent);
        }

        protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
    }
}

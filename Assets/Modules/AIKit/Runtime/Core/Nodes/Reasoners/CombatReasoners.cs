using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Attack (the decision once called Fight — step 90: if it is an attack, it is called "Attack"): the target is within
    /// reach. Two thresholds (handbook §17: what depends on distance is decided with slack): taken when the target is
    /// closer than Closer, let go only when it is farther than Let Go After — a target stepping on the line does not make
    /// the agent flicker between attacking and chasing. Closer 0 — within the reach of the weapon in the hand (its
    /// shortest move, handbook §18.3: the distances are the weapon's; no weapon — 1.9 m), and the node says so; Let Go
    /// After 0 (or below Closer) — no slack. Whether the agent must hold a place to attack from is not its own check
    /// any more (step 90, C7): an Attack Slot condition wired into its Condition pin says it, visibly.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Attack", "Task", "aikit.reasoner.fight", Simple = true, Endless = true)]
    public sealed class FightReasoner : ReasonerTask
    {
        /// <summary>Closer with no weapon in the hand (the old default).</summary>
        public const float NO_WEAPON_CLOSER = 1.9f;

        /// <summary>Taken when the target is this near, metres; 0 — within the reach of the weapon in the hand.</summary>
        [Min(0f)] public float Closer = 1.9f;

        /// <summary>Let go when the target is farther than this (not below Closer); 0 — as soon as it is past Closer.</summary>
        [Min(0f)] public float LetGoAfter = 2.2f;

        public override string Requirement =>
            Closer <= 0f ? WeaponDistances.Say("taken within", 1f, NO_WEAPON_CLOSER) : null;

        /// <summary>Closer as it works for this agent now (0 — the reach of the weapon in its hand).</summary>
        public float CloserFor(AgentContext context) =>
            WeaponDistances.Resolve(Closer, context, 1f, NO_WEAPON_CLOSER);

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            TargetFacts facts = query.Facts;
            if (!Reasons.Sees(facts, out whyNot))
                return false;

            float closer = CloserFor(query.Context);
            float limit = query.IsCurrent ? Mathf.Max(closer, LetGoAfter) : closer;
            if (facts.Distance > limit)
            {
                whyNot = !query.Explains ? null
                    : Closer <= 0f && limit <= closer
                        ? $"target {facts.Distance:0.0} m, needs my weapon's reach {limit:0.0} or closer"
                        : $"target {facts.Distance:0.0} m, needs {limit:0.0} or closer";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Chase: the target is seen — the agent goes straight at it (its chain: Chase). Whether it must hold a place to
    /// attack from first is not a check of its own any more (step 90, C7): an Attack Slot condition wired into its
    /// Condition pin says it, visibly. Who goes round instead is Flank's business: give Flank the more important priority
    /// and an Attack Slot "mine, someone attacks first", and it takes those agents (handbook §21.1 — roles at the
    /// target). A decision never depends on whether another one is wired.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Movement/Chase", "Task", "aikit.reasoner.chase", Simple = true, Endless = true)]
    public sealed class ChaseReasoner : ReasonerTask
    {
        public override bool Holds(in ReasonerQuery query, out string whyNot) => Reasons.Sees(query.Facts, out whyNot);
    }

    /// <summary>
    /// Flank: the target is seen — the agent comes around it from its own side (its chain: Flank; handbook §21.1). Who
    /// flanks is said by the wire into its Condition pin: an Attack Slot "mine, someone attacks first" — the agent holds a
    /// place and another one attacks before it (step 90: was the hidden Needs Attack Place / Only When Not First).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Movement/Flank", "Task", "aikit.reasoner.flank", Simple = true, Endless = true)]
    public sealed class FlankReasoner : ReasonerTask
    {
        public override bool Holds(in ReasonerQuery query, out string whyNot) => Reasons.Sees(query.Facts, out whyNot);
    }

    /// <summary>
    /// Circle: the target is seen — the decision of one that waits in front of it for a place to attack from (its chain:
    /// Random → Stand / Step / Dash, a taunt; handbook §20b.4 p. 6: the pauses between the series). What it waits for is
    /// on the canvas (step 90, C7): an Attack Slot "not mine" in its Condition pin, and a Take Attack Slot (Wait Time 0)
    /// on its On Tick pin, which asks the target for a place and keeps the agent's place on the ring once per think while
    /// the decision is in charge, whatever block of its chain is on (handbook §18.1: a place is asked for every step). The
    /// place given, the condition says no, and the Brain takes the attack; left without one, the request is taken back.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Movement/Circle", "Task", "aikit.reasoner.circle", Simple = true, Endless = true)]
    public sealed class CircleReasoner : ReasonerTask
    {
        public override bool Holds(in ReasonerQuery query, out string whyNot) => Reasons.Sees(query.Facts, out whyNot);
    }

    /// <summary>
    /// Notice: the target has just come — the agent takes a moment to react before it acts (handbook §9.1, B.2: Patrol →
    /// Notice → Chase; a reaction of 0.3–1.2 s reads as alive, not as a machine). Holds while the Brain has had its
    /// target for less than React seconds; give it the most important priority and a chain that stands for that long —
    /// the body shows the moment (the sword is drawn: the character's animation is "in combat" from now). A target
    /// gone and seen again is noticed again.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Notice", "Task", "aikit.reasoner.notice", Simple = true, Endless = true)]
    public sealed class NoticeReasoner : ReasonerTask
    {
        /// <summary>Seconds after the target comes during which the agent is noticing it.</summary>
        [Min(0f)] public float React = 1f;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            TargetFacts facts = query.Facts;
            if (!Reasons.Sees(facts, out whyNot))
                return false;

            if (facts.SeenFor >= React)
            {
                whyNot = query.Explains ? $"noticed {facts.SeenFor:0.0} s ago" : null;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Watch: the target is seen — the decision of one that keeps an eye on it without going after it (a guard that holds
    /// its ground: its chain turns to the target and stands). Give it a less important priority than the attack.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Combat/Watch", "Task", "aikit.reasoner.watch", Simple = true, Endless = true)]
    public sealed class WatchReasoner : ReasonerTask
    {
        public override bool Holds(in ReasonerQuery query, out string whyNot) => Reasons.Sees(query.Facts, out whyNot);
    }

    /// <summary>Patrol: always holds — the decision for when nothing more important does.</summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Movement/Patrol", "Task", "aikit.reasoner.patrol", Simple = true, Endless = true)]
    public sealed class PatrolReasoner : ReasonerTask
    {
        public override bool NeedsTarget => false;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            whyNot = null;
            return true;
        }
    }

    // The checks the reasoners share, each with its "why not" in plain words
    internal static class Reasons
    {
        public static bool Sees(in TargetFacts facts, out string whyNot)
        {
            whyNot = facts.Sees ? null : "no target";
            return facts.Sees;
        }
    }
}

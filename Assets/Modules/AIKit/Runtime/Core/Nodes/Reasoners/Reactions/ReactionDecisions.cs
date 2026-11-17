using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Target Attacks: the Brain's target winds up a blow (its own say: Winding Up), within Range and in front of the
    /// agent — the telegraph of handbook §19.1. The answer is the chain's: dodge aside or back (Random → Dash, never
    /// toward the attacker, §38 №37), and when the dodge cannot be made — block (Dash's Blocked → Block: "no dodge is a
    /// block, not nothing", §19.2). The chance grows with the blows that landed on the agent lately (Per Recent Hit for
    /// each, halving every 2 s — §19.2: a flurry of blows meets the guard, not an endless flinch). A target that does not
    /// say what it does (no status) is no event: no data, not "no".
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Reactions/Target Attacks", "Task", "aikit.reasoner.target-attacks", Simple = true,
        Endless = true)]
    public sealed class TargetAttacksReaction : ReactionDecision
    {
        /// <summary>Metres: a target winding up farther than this is not attacking the agent yet (handbook §19.2: 3 m).</summary>
        [Min(0f)] public float Range = 3f;

        /// <summary>Added to Chance for each blow that landed on the agent lately (halving every 2 s).</summary>
        [Range(0f, 1f)] public float PerRecentHit = 0.15f;

        public TargetAttacksReaction()
        {
            Chance = 0.7f;
            Cooldown = 1f;
        }

        protected override bool Happens(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out bool fresh)
        {
            bool known = StatusOf(facts.Target, memory, explain, out ICharacterStatus status, out fresh);
            if (!known)
                return false;

            if (!status.TryGetState(CharacterState.WindingUp, out bool windingUp))
            {
                memory.WhyNot = explain ? $"{facts.Target.name} does not say whether it winds up" : "no data";
                return false;
            }

            if (!windingUp)
            {
                memory.WhyNot = "target not winding up";
                return false;
            }

            if (facts.Distance > Range)
            {
                memory.WhyNot = explain ? $"target {facts.Distance:0.0} m, needs {Range:0.0} or closer" : "too far";
                return false;
            }

            if (!InFront(context, facts.Target.position))
            {
                memory.WhyNot = "target not in front";
                return false;
            }

            return true;
        }

        protected override float ChanceFor(AgentContext context, in TargetFacts facts, ReactionMemory memory) =>
            Chance + PerRecentHit * context.Hits.PressureAt(context.LocalTime);
    }

    /// <summary>
    /// Target Opens Up: the Brain's target is open to a blow — recovering after its own, or staggered — within Range
    /// (handbook §19.1: telegraph → window → punishment; §20б.4 p.5: punish out of turn). The answer is the chain's: a
    /// step in (Dash Forward) and Attack. A target that says neither is no event.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Reactions/Target Opens Up", "Task", "aikit.reasoner.target-opens-up", Simple = true,
        Endless = true)]
    public sealed class TargetOpensUpReaction : ReactionDecision
    {
        /// <summary>Metres: a target open farther than this is out of reach of a punishment.</summary>
        [Min(0f)] public float Range = 3f;

        public TargetOpensUpReaction()
        {
            Chance = 0.6f;
            Cooldown = 3f;
        }

        protected override bool Happens(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out bool fresh)
        {
            if (!StatusOf(facts.Target, memory, explain, out ICharacterStatus status, out fresh))
                return false;

            bool saysRecovering = status.TryGetState(CharacterState.Recovering, out bool recovering);
            bool saysStaggered = status.TryGetState(CharacterState.Staggered, out bool staggered);
            if (!saysRecovering && !saysStaggered)
            {
                memory.WhyNot = explain ? $"{facts.Target.name} does not say whether it recovers" : "no data";
                return false;
            }

            if (!recovering && !staggered)
            {
                memory.WhyNot = "target not open";
                return false;
            }

            if (facts.Distance > Range)
            {
                memory.WhyNot = explain ? $"target {facts.Distance:0.0} m, needs {Range:0.0} or closer" : "too far";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Got Hit: a blow landed on the agent within the last Window seconds (with From Behind Only — a blow from its back
    /// half). Each new hit is a new event. A tactical answer only (step 90: the flinch is the body's, its animator plays
    /// it): turn to the target (Turn To Target), call for help. It is about the Brain's target — the attacker once
    /// Detection sees it (the Hit By shape sees whoever struck); with no target yet it waits for one within the window.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Reactions/Got Hit", "Task", "aikit.reasoner.got-hit", Simple = true, Endless = true)]
    public sealed class GotHitReaction : ReactionDecision
    {
        /// <summary>Seconds after a hit during which the agent still answers it.</summary>
        [Min(0f)] public float Window = 1.5f;

        /// <summary>On — only a blow from behind (its back half) is answered: one from the front is seen coming.</summary>
        public bool FromBehindOnly;

        public GotHitReaction()
        {
            Cooldown = 0.5f;
        }

        protected override bool Happens(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out bool fresh)
        {
            HitMemory hits = context.Hits;
            fresh = hits.Count != memory.SeenHits;
            memory.SeenHits = hits.Count;
            if (!hits.Any)
            {
                memory.WhyNot = "not hit";
                return false;
            }

            float since = hits.Since(context.LocalTime);
            if (since > Window)
            {
                memory.WhyNot = explain ? $"hit {since:0.0} s ago, answers within {Window:0.0}" : "not hit lately";
                return false;
            }

            if (FromBehindOnly && !hits.LastFromBehind)
            {
                memory.WhyNot = "hit from the front";
                return false;
            }

            if (facts.Target == null)
            {
                memory.WhyNot = "no target";
                return false;
            }

            return true;
        }
    }

    /// <summary>Whose health a Health Low decision watches.</summary>
    public enum HealthSubject
    {
        /// <summary>The agent's own.</summary>
        Me,

        /// <summary>The Brain's target's.</summary>
        Target,

        /// <summary>The lowest of the allies within Range: the agents of the world of the agent's faction.</summary>
        Ally
    }

    /// <summary>
    /// Health Low: the health of Whose — mine, the target's, an ally's — goes below Below percent (the user's order of
    /// 13.11: my health, the target's, an ally's). Caught when it goes under the line; taken, it holds while the health
    /// stays under the line with Slack, and lets go above it (handbook §17: what depends on a number is decided with
    /// slack). The health is the object's own say (IHealth, the game's); the allies come from the world's roster (no
    /// scan of the scene, handbook §21.4), of the agent's faction. A Whose with no health to read is no event.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Reactions/Health Low", "Task", "aikit.reasoner.health-low", Simple = true, Endless = true)]
    public sealed class HealthLowReaction : ReactionDecision
    {
        public HealthSubject Whose = HealthSubject.Me;

        /// <summary>Percent of the full health: below it the health is low.</summary>
        [Range(0f, 100f)] public float Below = 30f;

        /// <summary>Percent above Below the health must come back to before it is not low any more.</summary>
        [Range(0f, 100f)] public float Slack = 10f;

        /// <summary>Metres around the agent its allies are watched in (Whose = Ally).</summary>
        [Min(0f)] public float Range = 10f;

        public HealthLowReaction()
        {
            Cooldown = 5f;
        }

        /// <summary>The target's health needs the Brain's target; mine and an ally's do not.</summary>
        public override bool NeedsTarget => Whose == HealthSubject.Target;

        /// <summary>Where the allies come from is said on the node: no wire shows it.</summary>
        public override string Requirement => Whose == HealthSubject.Ally
            ? base.Requirement + "; allies — the world's agents of my faction"
            : base.Requirement;

        /// <summary>Whose = Ally: the ally the last look found lowest (null — none within Range with a health).</summary>
        public AICharacter LowestAllyFor(AgentContext context) =>
            context != null ? Memory<ReactionMemory>(context).Ally : null;

        protected override bool KeepsWhileInCharge(ReactionMemory memory) => memory.Was;

        protected override bool Happens(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out bool fresh)
        {
            fresh = false;
            object before = Whose == HealthSubject.Ally ? memory.Ally : Whose == HealthSubject.Target ? memory.Whose : null;
            if (!HealthNow(context, facts, memory, explain, out float percent))
            {
                memory.Latched = false;
                return false;
            }

            // Another subject than at the last look (the Brain switched straight to another target, the lowest ally is
            // another one): the slack was the last one's — the new one is judged by the line, and if it is low, it is a
            // new event (as a new target is for Target Attacks)
            object now = Whose == HealthSubject.Ally ? memory.Ally : Whose == HealthSubject.Target ? memory.Whose : null;
            bool another = !ReferenceEquals(before, now);
            float line = memory.Latched && !another ? Below + Slack : Below;
            memory.Latched = percent < line;
            if (memory.Latched)
            {
                fresh = another;
                return true;
            }

            memory.WhyNot = explain ? $"health {percent:0}%, low below {line:0}%" : "health not low";
            return false;
        }

        // The health of Whose in percent; false — nothing to read (memory.WhyNot says why)
        private bool HealthNow(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out float percent)
        {
            percent = 0f;
            switch (Whose)
            {
                case HealthSubject.Me:
                    if (!memory.OwnHealthLooked || memory.OwnHealth is UnityEngine.Object { } gone && gone == null)
                    {
                        memory.OwnHealthLooked = true;
                        Transform self = context.Agent != null ? context.Agent.EntityRoot : context.Self;
                        memory.OwnHealth = CharacterStatuses.HealthOf(self);
                    }

                    if (Percent(memory.OwnHealth, out percent))
                        return true;

                    memory.WhyNot = "no health on me";
                    return false;

                case HealthSubject.Target:
                    Transform target = facts.Target;
                    if (target != memory.Whose || memory.Health is UnityEngine.Object { } lost && lost == null)
                    {
                        memory.Whose = target;
                        memory.Status = null;
                        memory.Health = CharacterStatuses.HealthOf(target);
                    }

                    if (target == null)
                    {
                        memory.WhyNot = "no target";
                        return false;
                    }

                    if (Percent(memory.Health, out percent))
                        return true;

                    memory.WhyNot = explain ? $"no health on {target.name}" : "no health on the target";
                    return false;

                default:
                    return LowestAlly(context, memory, explain, out percent);
            }
        }

        // The ally of the agent's faction within Range whose health is lowest (the world's roster, never the scene). Its
        // health is its own say (its status — the character keeps its game's health found once), so the reaction keeps
        // no memory of the allies it met: one that is gone is gone, a new one costs nothing (handbook §8.2: read where the
        // value lives)
        private bool LowestAlly(AgentContext context, ReactionMemory memory, bool explain, out float lowest)
        {
            lowest = float.MaxValue;
            memory.Ally = null;
            AgentRoster roster = context.Roster;
            AICharacter self = context.Agent;
            if (roster == null || self == null)
            {
                memory.WhyNot = "no world: no allies known";
                return false;
            }

            int faction = ((ISignalListener)self).Faction;
            Vector3 at = context.Position;
            float most = Range * Range;
            IReadOnlyList<AICharacter> members = roster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                AICharacter ally = members[i];
                if (ally == null || ally == self || !ally.enabled || !ally.gameObject.activeInHierarchy ||
                    ((ISignalListener)ally).Faction != faction)
                    continue;

                Vector3 where = ally.Context != null ? ally.Context.Position : ally.transform.position;
                if ((where - at).sqrMagnitude > most)
                    continue;

                if (((ICharacterStatus)ally).TryGetStat(CharacterStat.HealthPercent, out float percent) &&
                    Mathf.Max(0f, percent) < lowest)
                {
                    lowest = Mathf.Max(0f, percent);
                    memory.Ally = ally;
                }
            }

            if (memory.Ally != null)
                return true;

            lowest = 0f;
            memory.WhyNot = explain ? $"no ally with health within {Range:0} m" : "no ally with health near";
            return false;
        }
    }
}

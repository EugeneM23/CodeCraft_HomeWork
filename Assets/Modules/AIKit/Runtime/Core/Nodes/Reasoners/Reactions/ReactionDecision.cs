using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>Where a reaction stands for one agent.</summary>
    public enum ReactionPhase
    {
        /// <summary>No event to react to (or the last one was let pass).</summary>
        Idle,

        /// <summary>An event was caught and the roll said yes: the reaction time runs.</summary>
        Waiting,

        /// <summary>The reaction time is up and the event still holds: the decision holds — the Brain takes it out of turn.</summary>
        Ready,

        /// <summary>The Brain took the decision: it holds until its chain ends.</summary>
        InCharge
    }

    /// <summary>Why the last event was let pass (for the canvas).</summary>
    public enum ReactionMiss
    {
        None,

        /// <summary>The roll of the chance said no (once per event).</summary>
        Chance,

        /// <summary>The event came while the reaction was cooling down.</summary>
        Cooldown,

        /// <summary>When the reaction time was up, the event was over (handbook §38 №36: no block into the void).</summary>
        Cancelled,

        /// <summary>The event was over before the Brain could take the reaction (the agent was in the middle of a blow).</summary>
        Passed
    }

    /// <summary>
    /// One agent's reaction (step 90): written only by the decision's Observe — once per think — and by the Brain's own
    /// acts (Taken, LeftCharge); Holds only reads it (invariant 6: a question makes no state). The fields below the line
    /// are the event's own look (a reaction keeps there what it needs: the status of the object it watches…).
    /// </summary>
    public sealed class ReactionMemory
    {
        public ReactionPhase Phase;

        /// <summary>The event held at the last look: a rising edge is a new event (handbook §38 №15).</summary>
        public bool Was;

        /// <summary>When the reaction time is up (the agent's clock).</summary>
        public float FireAt;

        /// <summary>No event is caught before this (the agent's clock).</summary>
        public float CoolUntil = float.NegativeInfinity;

        /// <summary>The Brain let go of the reaction: its cooldown starts at the next look.</summary>
        public bool Over;

        public ReactionMiss Missed;

        /// <summary>The chance the last event was rolled with.</summary>
        public float Chance;

        /// <summary>Why the event does not happen at the last look (the reaction's own words), null — it happens.</summary>
        public string WhyNot;

        // ---------- the event's own look ----------

        /// <summary>The object whose say is kept below (the target watched).</summary>
        public Transform Whose;

        public ICharacterStatus Status;
        public IHealth Health;

        /// <summary>The agent's own health was looked for (found or not).</summary>
        public bool OwnHealthLooked;
        public IHealth OwnHealth;

        /// <summary>Got Hit: the number of the last hit seen.</summary>
        public int SeenHits;

        /// <summary>Health Low: under the line — until above it with the slack.</summary>
        public bool Latched;

        /// <summary>Health Low (Ally): the ally the last look found lowest (its health read from its own status).</summary>
        public AICharacter Ally;
    }

    /// <summary>
    /// A reaction to what the player (or any target) does — a decision of the Brain taken out of turn (step 90; handbook
    /// §19.1–19.4, §20б.4 p.5; step 89: a reaction is a decision with its priority, not a layer over the Brain). Once per
    /// think, also in the middle of the agent's own blow, it looks at its event (Observe): caught on its rising edge
    /// (§38 №15), the chance rolled once for the event with the agent's own random, then a random reaction time
    /// (§19.2: 0.1–0.35 s — a frame-perfect answer reads as cheating, §38 №35) and, when it is up, the event looked at
    /// again (§38 №36: an attack cancelled meanwhile gets no block into the void). Then the decision holds and the Brain
    /// takes it at once, without the minimum time of the decision in charge — but never in the middle of a blow or an
    /// ability (§18.2 p.3, §20б.4 p.1: a started blow is played out). Taken, it holds until its chain ends, then lets go
    /// until a new event, after its Cooldown (§19.2: the rhythm of counters). Give it a priority above the decisions it
    /// must cut in on (Attack): below one whose chain attacks, the graph shows the problem.
    /// </summary>
    [Serializable]
    public abstract class ReactionDecision : ReasonerTask
    {
        /// <summary>
        /// A point is in front of the agent within 60° of where it faces — the cosine of that angle (handbook §19.2: dot
        /// 0.5, only from the front).
        /// </summary>
        public const float FRONT_DOT = 0.5f;

        /// <summary>The chance to react to one event, rolled once when it comes (the agent's own random): 1 — always.</summary>
        [Range(0f, 1f)] public float Chance = 1f;

        /// <summary>
        /// Seconds from the event to the reaction, at least — and ReactionTimeMax at most: each event waits a random
        /// time in between, then the event is looked at again.
        /// </summary>
        [Min(0f)] public float ReactionTime = 0.1f;

        [Min(0f)] public float ReactionTimeMax = 0.35f;

        /// <summary>Seconds after the reaction is over before a new event can be caught.</summary>
        [Min(0f)] public float Cooldown = 1f;

        private const string NOTHING = "nothing happens";
        private const string CHANCE_MISSED = "chance missed";
        private const string COOLING = "cooldown";
        private const string WAITS = "waits for the next one";
        private const string REACTING = "reacts in a moment";

        /// <summary>A reaction wins at once (never mid-blow): the Brain does not wait out the minimum time of the decision in charge.</summary>
        public sealed override bool OutOfTurn => true;

        public override string Requirement => "out of turn: taken at once, never mid-blow; held until its chain ends";

        /// <summary>
        /// The event holds now (once per think, from Observe; the memory is the agent's — keep there what the look needs).
        /// When it does not, say why in memory.WhyNot (the reaction's own words: "target not winding up"; with numbers only
        /// while explain is on — else a constant string). fresh — a new event of the same kind came although the last one
        /// still holds (a second hit): it is caught as a new one.
        /// </summary>
        protected abstract bool Happens(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out bool fresh);

        /// <summary>The chance to react to this event, rolled once when it is caught. Chance by default.</summary>
        protected virtual float ChanceFor(AgentContext context, in TargetFacts facts, ReactionMemory memory) => Chance;

        /// <summary>
        /// Taken, the decision holds until its chain ends — and while this says so: a reaction to a lasting state (health
        /// low) lets go once the state is over. True by default: a dodge runs to its end even if the attack is over.
        /// </summary>
        protected virtual bool KeepsWhileInCharge(ReactionMemory memory) => true;

        /// <summary>Where the reaction stands for this agent (tests, the canvas).</summary>
        public ReactionPhase PhaseFor(AgentContext context) =>
            context != null ? Memory<ReactionMemory>(context).Phase : ReactionPhase.Idle;

        public sealed override void Observe(AgentContext context, in TargetFacts facts)
        {
            if (context == null)
                return;

            ReactionMemory m = Memory<ReactionMemory>(context);
            float now = context.LocalTime;

            // In charge by the memory, but the Brain is on another decision (the graph was stopped and started anew):
            // let go as if the Brain had said so
            BehaviorGraphInstance graph = context.Agent != null ? context.Agent.Graph : null;
            if (m.Phase == ReactionPhase.InCharge && graph != null && graph.Decision?.Reasoner != this)
            {
                m.Phase = ReactionPhase.Idle;
                m.Over = true;
            }

            if (m.Over)
            {
                m.Over = false;
                m.CoolUntil = now + Cooldown;
            }

            m.WhyNot = null;
            bool happens = Happens(context, facts, m, context.ExplainsDecisions, out bool fresh);
            if (!happens && m.WhyNot == null)
                m.WhyNot = NOTHING;

            bool rising = happens && (!m.Was || fresh);
            m.Was = happens;

            if (m.Phase == ReactionPhase.Idle && rising)
                Catch(context, facts, m, now);

            if (m.Phase == ReactionPhase.Waiting)
            {
                if (now + 1e-5f >= m.FireAt)
                {
                    // The reaction time is up: the event is looked at again (handbook §19.2, §38 №36)
                    if (happens)
                    {
                        m.Phase = ReactionPhase.Ready;
                    }
                    else
                    {
                        m.Phase = ReactionPhase.Idle;
                        m.Missed = ReactionMiss.Cancelled;
                    }
                }
            }
            else if (m.Phase == ReactionPhase.Ready && !happens)
            {
                // Over before the Brain could take it (a blow of the agent's own was being played out)
                m.Phase = ReactionPhase.Idle;
                m.Missed = ReactionMiss.Passed;
            }
        }

        // A new event: the cooldown, the one roll of the chance, the reaction time
        private void Catch(AgentContext context, in TargetFacts facts, ReactionMemory m, float now)
        {
            if (now < m.CoolUntil)
            {
                m.Missed = ReactionMiss.Cooldown;
                return;
            }

            float chance = Mathf.Clamp01(ChanceFor(context, facts, m));
            m.Chance = chance;
            if (!context.Chance(chance))
            {
                m.Missed = ReactionMiss.Chance;
                return;
            }

            m.Missed = ReactionMiss.None;
            float low = Mathf.Min(ReactionTime, ReactionTimeMax);
            float high = Mathf.Max(ReactionTime, ReactionTimeMax);
            m.FireAt = now + (high > low ? context.Range(low, high) : low);
            m.Phase = ReactionPhase.Waiting;
        }

        public sealed override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            whyNot = null;
            AgentContext context = query.Context;
            if (context == null)
            {
                whyNot = NOTHING;
                return false;
            }

            ReactionMemory m = Memory<ReactionMemory>(context);
            if (m.Phase == ReactionPhase.InCharge)
            {
                if (!query.IsCurrent)
                {
                    whyNot = "busy";
                    return false;
                }

                if (query.ChainEnded)
                {
                    whyNot = "done";
                    return false;
                }

                if (KeepsWhileInCharge(m))
                    return true;

                whyNot = m.WhyNot ?? NOTHING;
                return false;
            }

            if (m.Phase == ReactionPhase.Ready)
                return true;

            whyNot = WhyNotNow(query, m);
            return false;
        }

        // Why it does not hold, in the reaction's own words while the event is off, else by where it stands
        private static string WhyNotNow(in ReasonerQuery query, ReactionMemory m)
        {
            if (!m.Was)
                return m.WhyNot ?? NOTHING;

            float now = query.Context.LocalTime;
            if (m.Phase == ReactionPhase.Waiting)
                return query.Explains ? $"reacts in {Mathf.Max(0f, m.FireAt - now):0.00} s" : REACTING;

            switch (m.Missed)
            {
                case ReactionMiss.Chance:
                    return query.Explains ? $"chance missed ({m.Chance * 100f:0}%)" : CHANCE_MISSED;
                case ReactionMiss.Cooldown:
                    return m.CoolUntil > now
                        ? query.Explains ? $"cooldown {m.CoolUntil - now:0.0} s" : COOLING
                        : "came in the cooldown — " + WAITS;
                default:
                    return "done — " + WAITS;
            }
        }

        // The Brain hands the agent to the reaction: the event is acted on
        public sealed override void Taken(AgentContext context, in TargetFacts facts)
        {
            if (context == null)
                return;

            ReactionMemory m = Memory<ReactionMemory>(context);
            m.Phase = ReactionPhase.InCharge;
            m.Missed = ReactionMiss.None;
        }

        // The Brain let go of it (its chain ended, or another decision won): the cooldown starts at the next look
        public sealed override void LeftCharge(AgentContext context)
        {
            if (context == null)
                return;

            ReactionMemory m = Memory<ReactionMemory>(context);
            if (m.Phase != ReactionPhase.InCharge)
                return;

            m.Phase = ReactionPhase.Idle;
            m.Over = true;
        }

        // ---------- what the reactions share ----------

        /// <summary>
        /// The status of the object (its own say: ICharacterStatus), kept in the memory per object — asked anew only when
        /// the object changes (handbook §8.2: a node may keep a link to a component). changed — another object than at the
        /// last look. False — no object, or it says nothing about itself (memory.WhyNot says which).
        /// </summary>
        protected static bool StatusOf(Transform target, ReactionMemory memory, bool explain, out ICharacterStatus status,
            out bool changed)
        {
            changed = target != memory.Whose;
            if (changed || memory.Status is UnityEngine.Object { } gone && gone == null)
            {
                memory.Whose = target;
                memory.Status = CharacterStatuses.Of(target);
                memory.Health = null;
            }

            status = memory.Status;
            if (target == null)
            {
                memory.WhyNot = "no target";
                return false;
            }

            if (status == null)
            {
                memory.WhyNot = explain ? $"no status on {target.name}" : "no status on the target";
                return false;
            }

            return true;
        }

        /// <summary>The point is in front of the agent: within 60° of where it faces (FRONT_DOT), on the ground's plane.</summary>
        protected static bool InFront(AgentContext context, Vector3 point)
        {
            Vector3 forward = context.Self != null ? context.Self.forward : Vector3.forward;
            Vector3 to = point - context.Position;
            forward.y = 0f;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
                return true;                            // on top of it: nowhere to turn to

            return Vector3.Dot(forward.normalized, to.normalized) >= FRONT_DOT;
        }

        /// <summary>The health in percent of its Max; false — no health, or no Max to measure by.</summary>
        protected static bool Percent(IHealth health, out float percent)
        {
            percent = 0f;
            if (health == null || health is UnityEngine.Object { } gone && gone == null || health.Max <= 0f)
                return false;

            percent = Mathf.Max(0f, health.Current) / health.Max * 100f;
            return true;
        }
    }
}

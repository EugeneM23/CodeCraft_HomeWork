using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>The attack slots of a target as the group nodes see them: one pool of the roster's attack tokens.</summary>
    public static class AttackSlots
    {
        public const string POOL = "AttackSlot";

        /// <summary>The pool a node asks from: its own name for it, or the common one.</summary>
        public static string PoolOf(string name) => string.IsNullOrEmpty(name) ? POOL : name;

        /// <summary>How much nearer to the target a waiting agent must be to take the place of the one who holds it.</summary>
        public const float SWAP_MARGIN = 1f;

        private static readonly List<AICharacter> Holders = new();

        /// <summary>The engagement around the object (the agent joins the scene's group first); null — no object.</summary>
        public static Engagement EngagementOf(AgentContext context, Transform target)
        {
            if (context == null || target == null)
                return null;

            AIWorldRunner.Join(context.Agent);
            return context.Roster?.EngagementOf(target);
        }

        /// <summary>
        /// The engagement around the object if somebody has asked for places round it — for questions, which must not
        /// make one (AgentRoster.FindEngagement) and must not join the agent anywhere either (invariant 6: a question
        /// makes no state; the agent joins the scene's group when its Brain starts). Null — none yet, no group, or no
        /// object.
        /// </summary>
        public static Engagement FindEngagement(AgentContext context, Transform target)
        {
            if (context == null || target == null)
                return null;

            return context.Roster?.FindEngagement(target);
        }

        /// <summary>
        /// The agent to take the other side from: the first holder of an attack slot on the target that is not this
        /// agent. An agent that holds a slot itself looks only at those who got theirs before it — so of two attackers
        /// the first goes straight in and the second flanks, not both. Null — nobody to flank.
        /// </summary>
        public static AICharacter OtherHolder(AgentContext context, Transform target) => Leader(context, target);

        /// <summary>
        /// The agent that holds a place of the common group and attacks before this one (null — this agent is the
        /// first, or nobody attacks): the first one goes straight at the target, the rest come round to their own places.
        /// </summary>
        public static AICharacter Leader(AgentContext context, Transform target) => Leader(context, target, POOL);

        /// <summary>
        /// The agent that holds a place of the group (pool) and attacks before this one, in the order the places were
        /// asked for (null — this agent is the first, or nobody holds one). Only asks: no engagement is made.
        /// </summary>
        public static AICharacter Leader(AgentContext context, Transform target, string pool)
        {
            Engagement engagement = FindEngagement(context, target);
            if (engagement == null)
                return null;

            engagement.Tokens.HoldersOf(pool, Holders);
            AICharacter other = null;
            for (int i = 0; i < Holders.Count && other == null; i++)
            {
                if (Holders[i] == context.Agent)
                    break;                              // it leads: those after it flank around it

                other = Holders[i];
            }

            Holders.Clear();
            return other;
        }

        /// <summary>
        /// Where this agent should stand around the target: its own place on the target's ring (AttackRing — evenly
        /// spread directions in the world, the free one nearest to where it already stands, kept while it keeps
        /// asking), as an angle in degrees from world forward. False — the target is in no fight (no group).
        /// </summary>
        public static bool PlaceAround(AgentContext context, Transform target, out float angle, bool first = false)
        {
            angle = 0f;
            Engagement engagement = EngagementOf(context, target);
            if (engagement == null)
                return false;

            // Those who hold a place to attack from are on the ring too, whatever they are doing — striking, waiting
            // for their swing — and the first of them anchors it: the others are shared out round the one in the fight
            engagement.Tokens.HoldersOf(POOL, Holders);
            for (int i = 0; i < Holders.Count; i++)
            {
                if (Holders[i] == null || Holders[i] == context.Agent)
                    continue;

                Vector3 held = Holders[i].EntityRoot != null ? Holders[i].EntityRoot.position : Holders[i].transform.position;
                engagement.Ring.AngleFor(Holders[i], held - target.position, i == 0);
            }

            // With nobody holding a place yet, the first to come anchors the ring and keeps it while it asks: another
            // agent that comes "first" too (nobody leads it either) does not take it over at each of its steps
            object asking = (object)context.Agent ?? context;
            bool anchors = first && (Holders.Count == 0
                ? !engagement.Ring.IsAnchoredByAnother(asking)
                : Holders[0] == context.Agent);
            Holders.Clear();

            Vector3 fromTarget = context.Position - target.position;
            angle = engagement.Ring.AngleFor(asking, fromTarget, anchors);
            return true;
        }

        /// <summary>The direction from the target of this agent's place (PlaceAround), as a unit vector.</summary>
        public static Vector3 DirectionOf(float angle) => Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;

        /// <summary>The agent leaves the fight: its place on the ring goes to whoever comes next.</summary>
        public static void LeaveRing(AgentContext context, Transform target)
        {
            Engagement engagement = EngagementOf(context, target);
            engagement?.Ring.Release((object)context.Agent ?? context);
        }
    }

    /// <summary>
    /// Asks the target for a place to attack it from (handbook §18.1). A target lets only so many attack at once (one
    /// per three agents of the group unless the target says otherwise — Attack Slot Capacity); the place belongs to
    /// whoever is nearer to the target — a clearly nearer agent takes it from the farthest holder. Success ("Got") — the
    /// place is this agent's: attack. Failure ("Waiting") — others hold every place: do something else and ask again.
    /// The same graph on every agent of a group sorts them into roles by itself.
    /// With Wait Time 0 it is the instant ask (step 90, spec 10.3 — the visible place of what the Circle decision once
    /// did by itself): it files the request, or keeps it filed, keeps the agent's place on the ring round the target, and
    /// answers at once; the request stays filed for the next ask. Put it on a decision's On Tick pin: the engine runs it
    /// once per think while the decision is in charge, whatever block of its chain is on (a place is asked for every
    /// step, §18.1). A request still waiting when that decision leaves is taken back (AgentContext.DecisionLeft;
    /// handbook §37.3: leaving gives up what was asked); one nobody asks again goes stale anyway (STALE_AFTER).
    /// With Wait Time above 0 it waits that long for the answer and, still waiting, gives the request up.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Take Attack Slot", "Task", "aikit.task.take-attack-slot", Success = "Got", Failure = "Waiting",
        Simple = true)]
    public sealed class TakeAttackSlotTask : AgentTask
    {
        private sealed class Mem
        {
            public float Waited;
            public Engagement Asked;
            public string Pool;

            // The instant ask: the request left filed, taken back when the decision leaves
            public AgentContext Context;
            public Engagement Waiting;
            public string WaitingPool;
            public Action Left;
            public bool Listening;
            public BehaviorGraphInstance Graph;     // the graph it listens for (another, or stopped — it stops)

            // The listener (a method of the memory: no closure is made where the ask waits, think after think)
            public void TakeBack() => TakeAttackSlotTask.TakeBack(this);
        }

        /// <summary>The Transform variable (of the agent) with the object to attack; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>
        /// The group of places asked from: agents with the same name here share one count of places (melee and archers
        /// each wait for their own). Empty — the common one.
        /// </summary>
        [PoolName] public string Pool = "";

        /// <summary>
        /// Seconds to wait for an answer before giving up the request (the group answers once per step). 0 — the instant
        /// ask: it answers at once and the request stays filed (on a decision's On Tick).
        /// </summary>
        public BlackboardValue<float> WaitTime = 0.2f;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>Wait Time is a plain 0: the ask is done at once (it passes in a think, it runs on an On Tick pin).</summary>
        public override bool IsInstant => !WaitTime.UsesVariable && WaitTime.Get(null) <= 0f;

        public override string Requirement => IsInstant
            ? "keeps my place on the ring while it asks; a waiting ask is taken back when the decision leaves"
            : null;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Waited = 0f;
            m.Asked = null;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            Transform target = (string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target));
            Engagement engagement = AttackSlots.EngagementOf(context, target);
            if (engagement == null)
                return target != null ? TaskStatus.Success : TaskStatus.Failure;    // no group: nothing limits the attack

            bool instant = IsInstant;
            if (instant)
                AttackSlots.PlaceAround(context, target, out _);    // its place on the ring is kept while it waits

            float distance = Vector3.Distance(context.Position, target.position);
            string pool = AttackSlots.PoolOf(Pool);
            engagement.Tokens.NearestHolds(pool, AttackSlots.SWAP_MARGIN);
            Mem m = Memory<Mem>(context);
            m.Asked = engagement;
            m.Pool = pool;
            TokenResult result = engagement.Tokens.Request(context.Agent, pool, 1, 0f, distance);
            if (result == TokenResult.Granted)
                return TaskStatus.Success;                      // the Attack Slot condition says "mine" from now on

            if (instant)
            {
                Wait(context, m, engagement, pool);             // filed: asked again on the next think
                return TaskStatus.Failure;
            }

            m.Waited += dt;
            if (result == TokenResult.Pending && m.Waited < WaitTime.Get(context))
                return TaskStatus.Running;

            engagement.Tokens.Release(context.Agent, pool);     // not waiting in the queue: asks again later
            return TaskStatus.Failure;
        }

        // Left while it still waited for an answer: it waits no more (handbook §37.3 — leaving gives up what was asked)
        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            Mem m = Memory<Mem>(context);
            if (reason != ExitReason.Finished && m.Asked != null && context != null)
                m.Asked.Tokens.Withdraw(context.Agent, m.Pool);
            m.Asked = null;
        }

        // The instant ask's request stays filed; the decision whose chain asks it leaves — it is taken back, unless the
        // place came meanwhile (then it is the agent's: Release Attack Slot gives it back). The agent's ask listens from
        // its first wait on, for as long as the graph it waited in runs (once: no allocation at every change of decision);
        // the graph stopped or replaced — its memory is gone — it stops listening, so no graph given leaves one behind
        private static void Wait(AgentContext context, Mem m, Engagement engagement, string pool)
        {
            m.Waiting = engagement;
            m.WaitingPool = pool;
            if (m.Listening)
                return;

            m.Context = context;
            m.Graph = context.Agent != null ? context.Agent.Graph : null;
            m.Left ??= m.TakeBack;
            context.DecisionLeft += m.Left;
            m.Listening = true;
        }

        private static void TakeBack(Mem m)
        {
            AgentContext context = m.Context;
            if (context != null && m.Waiting != null && !m.Waiting.Tokens.Holds(context.Agent, m.WaitingPool))
                m.Waiting.Tokens.Withdraw(context.Agent, m.WaitingPool);
            m.Waiting = null;

            BehaviorGraphInstance graph = context?.Agent != null ? context.Agent.Graph : null;
            if (context == null || !m.Listening || graph != null && graph == m.Graph && graph.IsRunning)
                return;

            context.DecisionLeft -= m.Left;             // its graph stopped (or is another now): it listens no more
            m.Listening = false;
        }
    }

    /// <summary>
    /// Gives the attack place back to the target (Take Attack Slot took it): another agent of the group that asks
    /// next gets it. Put it after the attack to make the group take turns. Finishes at once ("Done"); an agent that
    /// holds no place just goes on.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Release Attack Slot", "Task", "aikit.task.release-attack-slot", Success = "Done",
        Simple = true, NeverFails = true)]
    public sealed class ReleaseAttackSlotTask : AgentTask
    {
        // Every place the agent holds goes back — of whatever group it was taken in (Take Attack Slot's Pool)
        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            context?.ReleasePlaces();
            return TaskStatus.Success;
        }
    }

    /// <summary>
    /// Circles the target sideways, facing it, at Radius — a fighter that sizes its opponent up while it waits. With
    /// WaitForSlot (the default) it keeps asking the target for an attack place all the while and finishes ("Got
    /// Slot") the moment one is free: those who found every place taken wait here for their turn (of several who
    /// wait the nearest gets it). Without WaitForSlot it circles for Duration seconds and finishes. Failure ("Lost")
    /// — no target.
    /// Each run goes round to the left or to the right by the agent's own random.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Circle Target", "Task", "aikit.task.circle-target", Success = "Got Slot", Failure = "Lost",
        Simple = true)]
    public sealed class CircleTargetTask : AgentTask
    {
        private sealed class Mem
        {
            public float Side = 1f;
            public float Time;
            public float Offset;
            public float NextChange;
            public bool Standing;
            public Engagement Asked;
            public string Pool;
            public bool Got;
        }

        /// <summary>The Transform variable (of the agent) with the object to circle.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>
        /// The group of places asked from: agents with the same name here share one count of places (melee and archers
        /// each wait for their own). Empty — the common one.
        /// </summary>
        [PoolName] public string Pool = "";

        /// <summary>How far from the target to walk; 0 — just outside the reach of the weapon in the hand (1.1 × it).</summary>
        public BlackboardValue<float> Radius = 4f;

        /// <summary>The radius with no weapon in the hand (the old default).</summary>
        public const float NO_WEAPON_RADIUS = 4f;

        public override string Requirement => WeaponDistances.Join(
            WaitForSlot ? "asks for an attack place while it waits" : null,
            WeaponDistances.Say("waits at", Radius, WeaponDistances.AROUND, NO_WEAPON_RADIUS));

        /// <summary>How fast, as a part of the body's full speed (a wary sidestep, not a run).</summary>
        public BlackboardValue<float> SpeedScale = 0.35f;

        /// <summary>How far in and out of the radius it steps while looking for a moment to strike.</summary>
        public BlackboardValue<float> Sway = 1.2f;

        /// <summary>Seconds, on average, between changes of side and distance.</summary>
        public BlackboardValue<float> ChangeEvery = 1.5f;

        /// <summary>On — ends when the target gives an attack place; off — ends after Duration seconds.</summary>
        public bool WaitForSlot = true;

        /// <summary>Seconds of circling when WaitForSlot is off.</summary>
        public BlackboardValue<float> Duration = 3f;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Time = 0f;
            m.NextChange = 0f;
            m.Offset = 0f;
            m.Standing = false;
            m.Got = false;
            m.Side = context != null && context.Chance(0.5f) ? 1f : -1f;
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            IAgentActuator actuator = context?.Actuator;
            if (actuator == null)
                return TaskStatus.Failure;

            Transform target = (string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target));
            if (target == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            m.Time += dt;
            Vector3 toTarget = target.position - context.Position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (WaitForSlot)
            {
                Engagement engagement = AttackSlots.EngagementOf(context, target);
                if (engagement == null)
                    return TaskStatus.Success;          // no group: nothing limits the attack
                string pool = AttackSlots.PoolOf(Pool);
                engagement.Tokens.NearestHolds(pool, AttackSlots.SWAP_MARGIN);
                m.Asked = engagement;
                m.Pool = pool;
                if (engagement.Tokens.Request(context.Agent, pool, 1, 0f, distance) == TokenResult.Granted)
                {
                    m.Got = true;
                    return TaskStatus.Success;
                }
            }
            else if (m.Time >= Duration.Get(context))
            {
                return TaskStatus.Success;
            }

            if (distance < 1e-3f)
                return TaskStatus.Running;

            // Now and then: another side, a step closer or back, a short stop — looking for a moment to strike
            if (m.Time >= m.NextChange)
            {
                float every = Mathf.Max(0.2f, ChangeEvery.Get(context));
                float sway = Mathf.Max(0f, Sway.Get(context));
                m.Standing = m.NextChange > 0f && context.Chance(0.2f);   // never a stop the moment it starts
                m.NextChange = m.Time + (m.Standing ? context.Range(0.3f, 0.7f) : context.Range(0.6f, 1.4f) * every);
                if (!m.Standing)
                {
                    if (context.Chance(0.5f))
                        m.Side = -m.Side;
                    m.Offset = context.Range(-sway, sway);
                }
            }

            // Sideways along the circle, with a pull in or out to the wanted distance; the eyes stay on the target
            Vector3 inward = toTarget / distance;
            Vector3 sideways = new Vector3(-inward.z, 0f, inward.x) * m.Side;
            float radius = WeaponDistances.Resolve(Radius.Get(context), context, WeaponDistances.AROUND, NO_WEAPON_RADIUS);
            float wanted = Mathf.Max(0.5f, radius + m.Offset);
            Vector3 direction = sideways + inward * Mathf.Clamp(distance - wanted, -1f, 1f);
            if (Claim(context, BodyAxis.Move, MoveAuthority.Strafe))
            {
                if (m.Standing)
                    actuator.Stop();
                else
                    actuator.Move(direction, SpeedScale.Get(context));
            }
            if (Claim(context, BodyAxis.Face, MoveAuthority.Strafe))
                actuator.Face(inward);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            Mem m = Memory<Mem>(context);
            if (reason != ExitReason.Finished)
                context?.Actuator?.Stop();

            // Left without the place — taken away, or lost its target — it waits for a place no more (handbook §37.3:
            // leaving gives up what was asked); a place it got is its own (Release Attack Slot gives it back)
            if (!m.Got && m.Asked != null && context != null)
                m.Asked.Tokens.Withdraw(context.Agent, m.Pool);

            m.Asked = null;
        }
    }

    /// <summary>
    /// Squares off against the target while it waits for a place to attack from: facing it, the character steps — to
    /// the left, to the right (square to the target the whole way), a step in, a step back — from one step straight into
    /// the next, keeping within a metre of Radius and near its own place on the target's ring (handbook §20b.4: between
    /// the series — a sidestep, a step back, a window for the player). It asks the target for a place all the while and
    /// finishes ("Got Slot") the moment one is free; Failure ("Lost") — no target. Standing still between steps is not
    /// its job: other nodes stop the body.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Square Off", "Task", "aikit.task.square-off", Success = "Got Slot", Failure = "Lost",
        Simple = true)]
    public sealed class SquareOffTask : AgentTask
    {
        private sealed class Mem
        {
            public float Time;
            public float LineUntil;
            public int Step;
            public bool Correcting;
            public float Bearing;
            public float Drift;
            public float Room;
            public bool ToPlace;
            public float Goal;
            public float StepFrom;
            public Vector3 LastAt;
            public float Pace;
            public float SlowFor;
            public float AskedSpeed;
            public Engagement Asked;
            public bool Got;
        }

        /// <summary>
        /// A step is blocked — a wall, the edge of the ground — when the body has gone at less than this part of its own
        /// pace for BLOCKED_FOR seconds.
        /// </summary>
        private const float BLOCKED_BELOW = 0.5f;

        /// <summary>Seconds of going too slow before a step counts as blocked.</summary>
        private const float BLOCKED_FOR = 0.15f;

        /// <summary>Seconds a step from a standstill is given to get up to pace before it can count as blocked.</summary>
        private const float GETTING_GOING = 0.6f;

        /// <summary>Metres per second no body walks: a move this fast is the body put somewhere else.</summary>
        private const float TELEPORT = 30f;

        /// <summary>How far in and out of the radius its steps may take it, metres.</summary>
        private const float BAND = 1f;

        /// <summary>The longest one step may last: a step that never gets where it was headed (a wall) gives up.</summary>
        private const float LONGEST_STEP = 4f;

        /// <summary>
        /// The least room, in degrees either side of its own place on the ring, its steps may take it round the target.
        /// </summary>
        private const float AWAY_FROM_PLACE = 12f;

        /// <summary>Its room either side of its place, as a part of the angle between two places on the ring.</summary>
        private const float ROOM_OF_GAP = 0.25f;

        /// <summary>
        /// How many times its room it has to be off its place before it hurries back: its own steps stay inside the room,
        /// so only a place that moved (somebody came, left, the fighter went round) or a push sends it back briskly.
        /// </summary>
        private const float FAR_OFF = 1.5f;

        /// <summary>Degrees within which a step taken back toward the place counts as arrived.</summary>
        private const float ON_PLACE = 3f;

        /// <summary>How much brisker than its wary step the agent goes while it is on its way back to its place.</summary>
        private const float TAKE_PLACE = 2f;

        /// <summary>The most of the body's speed it uses to get back to its place: a brisk step, never a run.</summary>
        private const float BRISKEST = 0.6f;

        private const int STAND = 0, LEFT = 1, RIGHT = 2, IN = 3, OUT = 4;

        /// <summary>The Transform variable (of the agent) with the object to square off against; empty — the Brain's.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>How far from the target it keeps; 0 — just outside the reach of the weapon in the hand (1.1 × it).</summary>
        public BlackboardValue<float> Radius = 4f;

        /// <summary>The radius with no weapon in the hand (the old default).</summary>
        public const float NO_WEAPON_RADIUS = 4f;

        /// <summary>How fast, as a part of the body's full speed (a wary step, not a run).</summary>
        public BlackboardValue<float> SpeedScale = 0.4f;

        public override string Requirement => WeaponDistances.Join(
            "asks for an attack place while it waits; keeps to its place on the ring",
            WeaponDistances.Say("keeps", Radius, WeaponDistances.AROUND, NO_WEAPON_RADIUS));

        [NonSerialized] private BlackboardVariable<Transform> _target;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Time = 0f;
            m.LineUntil = 0f;
            m.Step = STAND;
            m.Correcting = false;
            m.ToPlace = false;
            m.LastAt = context != null ? context.Position : Vector3.zero;
            m.Pace = 0f;
            m.SlowFor = 0f;
            m.AskedSpeed = 0f;
            m.Got = false;
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            IAgentActuator actuator = context?.Actuator;
            if (actuator == null)
                return TaskStatus.Failure;

            Transform target = string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);
            if (target == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            m.Time += dt;
            Vector3 toTarget = target.position - context.Position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            Engagement engagement = AttackSlots.EngagementOf(context, target);
            if (engagement == null)
                return TaskStatus.Success;              // no group: nothing limits the attack
            string pool = AttackSlots.PoolOf("");
            engagement.Tokens.NearestHolds(pool, AttackSlots.SWAP_MARGIN);
            m.Asked = engagement;
            if (engagement.Tokens.Request(context.Agent, pool, 1, 0f, distance) == TokenResult.Granted)
            {
                m.Got = true;
                return TaskStatus.Success;
            }

            if (distance < 1e-3f)
                return TaskStatus.Running;

            Vector3 inward = toTarget / distance;
            float radius = WeaponDistances.Resolve(Radius.Get(context), context, WeaponDistances.AROUND, NO_WEAPON_RADIUS);

            // How far round the target it has drifted from its own place on the ring (degrees, + to the left), and how
            // much room round its place it has: a part of the angle between two places — three of them have a lot,
            // ten have little — and never less than AWAY_FROM_PLACE
            m.Bearing = Mathf.Atan2(-inward.x, -inward.z) * Mathf.Rad2Deg;
            m.Drift = AttackSlots.PlaceAround(context, target, out float place) ? Mathf.DeltaAngle(place, m.Bearing) : 0f;
            m.Room = Mathf.Max(AWAY_FROM_PLACE, engagement.Ring.Gap * ROOM_OF_GAP);

            // Every step is planned to fit — inside its band and inside its room round its place — and ends where it was
            // headed. A step cut short the moment it touched an edge, and a hurried step back after it, was a new
            // direction every half a second. What still ends a step early: a push out of the band, and its place on the
            // ring moving far off (then it hurries back to it). A sidestep is headed for a spot round the target, not for
            // an angle off its place: the place turns after the fighter, and a goal that moved with it would be reached —
            // or run away from — the moment it turned
            bool outside = distance < radius - BAND || distance > radius + BAND;
            bool arrived = m.Step switch
            {
                LEFT => m.ToPlace ? m.Drift >= m.Goal : Mathf.DeltaAngle(m.Goal, m.Bearing) >= 0f,
                RIGHT => m.ToPlace ? m.Drift <= m.Goal : Mathf.DeltaAngle(m.Goal, m.Bearing) <= 0f,
                IN => distance <= m.Goal,
                OUT => distance >= m.Goal,
                _ => true,                              // it has only just come: the first step
            };
            bool farOff = Mathf.Abs(m.Drift) > m.Room * FAR_OFF;
            bool blocked = Blocked(context, m, dt);
            if (blocked)
                AwayFrom(context, m);
            else if (arrived || m.Time >= m.LineUntil || (outside && !m.Correcting) ||
                     (farOff && !m.ToPlace && !m.Correcting))
                NextStep(context, m, distance, radius, outside, farOff);

            if (Claim(context, BodyAxis.Move, MoveAuthority.Strafe))
            {
                // Every step is taken relative to where the target is now: a sidestep stays square to it the whole way
                // (a fixed line in the world would turn into a step back and leave the band), in and out stay along it
                Vector3 side = new Vector3(-inward.z, 0f, inward.x);
                Vector3 direction = m.Step switch
                {
                    LEFT => side,
                    RIGHT => -side,
                    IN => inward,
                    _ => -inward,
                };

                // Off its place on the ring it goes back at a brisker step than the wary shuffle: the places are only
                // shared evenly if the bodies get to them while the fight is still where it was
                float scale = SpeedScale.Get(context);
                float asked = m.ToPlace ? Mathf.Min(BRISKEST, scale * TAKE_PLACE) : scale;
                actuator.Move(direction, asked);
                m.AskedSpeed = asked * actuator.Body.MaxSpeed;     // the pace its wall check measures the step against
            }

            if (Claim(context, BodyAxis.Face, MoveAuthority.Strafe))
                actuator.Face(inward);
            return TaskStatus.Running;
        }

        // The next step: left or right across the target's direction, in or out along it — out of the band it goes back
        // into it, far off its place it goes back to it. Each step is given where it is headed, so it fits its room and
        // its band and is not cut short
        private static void NextStep(AgentContext context, Mem m, float distance, float radius, bool outside, bool farOff)
        {
            m.StepFrom = m.Time;
            m.LineUntil = m.Time + LONGEST_STEP;
            m.Correcting = outside;
            m.ToPlace = false;
            if (distance < radius - BAND)
            {
                m.Step = OUT;                           // too near: back to the middle of the band — from its edge every
                m.Goal = radius;                        // next step would be a step back again, never aside
            }
            else if (distance > radius + BAND)
            {
                m.Step = IN;                            // too far: in to the middle of the band
                m.Goal = radius;
            }
            else if (farOff)
            {
                // Its place has moved far off: back toward it, briskly. That is what keeps ten of them spread round
                // the target instead of bunched on the side they came from
                m.ToPlace = true;
                m.Step = m.Drift > 0f ? RIGHT : LEFT;
                m.Goal = m.Drift > 0f ? ON_PLACE : -ON_PLACE;
            }
            else if (context.Chance(0.6f))
            {
                // Aside, across its place and over to the other side of it, as a fighter paces in front of a foe: from
                // one side of its room the step goes to the other side, from the middle either way
                float lean = m.Drift / m.Room;
                bool left = lean < -0.3f || (lean <= 0.3f && context.Chance(0.5f));
                m.Step = left ? LEFT : RIGHT;
                float place = m.Bearing - m.Drift;      // the spot round the target, measured from its place now
                m.Goal = place + (left ? 1f : -1f) * m.Room * context.Range(0.4f, 0.8f);
            }
            else
            {
                // In or out, over the radius to the other half of the band
                m.Step = distance < radius ? OUT : IN;
                m.Goal = radius + (m.Step == OUT ? 1f : -1f) * BAND * context.Range(0.4f, 0.8f);
            }
        }

        // Whether the step it is taking has run into something. Only its own going is watched — how fast the body has
        // moved against the pace it walks at — and not the world round it: the body stands against a wall or the edge
        // of its ground, and on an island with obstacles it stood there until the step gave up, the animator showing
        // a stop and the stance in the middle of the node
        private static bool Blocked(AgentContext context, Mem m, float dt)
        {
            Vector3 at = context.Position;
            Vector3 moved = at - m.LastAt;
            m.LastAt = at;
            moved.y = 0f;
            float rate = dt > 0f ? moved.magnitude / dt : 0f;
            if (m.Step == STAND || rate > TELEPORT)
                return false;                           // put somewhere else: not a pace it walks at

            // The pace it walks at — never above the pace its step asks: a body that came in at a run is carried on by
            // the run for a moment, and measured against the run every wary step read as held by a wall
            m.Pace = Mathf.Max(m.Pace, rate);
            if (m.AskedSpeed > 0.05f)
                m.Pace = Mathf.Min(m.Pace, m.AskedSpeed);
            bool slow = rate < m.Pace * BLOCKED_BELOW || m.Pace < 0.05f;
            m.SlowFor = slow ? m.SlowFor + dt : 0f;
            return m.SlowFor >= BLOCKED_FOR && m.Time - m.StepFrom >= (m.Pace < 0.05f ? GETTING_GOING : BLOCKED_FOR);
        }

        // The step that ran into something gives way to one away from it: a sidestep to the other side, a step in or
        // out turns into a sidestep. It is taken to its end (Correcting), so a place or a band does not send it straight
        // back into the wall
        private static void AwayFrom(AgentContext context, Mem m)
        {
            int was = m.Step;
            m.StepFrom = m.Time;
            m.LineUntil = m.Time + LONGEST_STEP;
            m.SlowFor = 0f;
            m.ToPlace = false;
            m.Correcting = true;
            bool left = was == RIGHT || (was != LEFT && context.Chance(0.5f));
            m.Step = left ? LEFT : RIGHT;
            m.Goal = m.Bearing + (left ? 1f : -1f) * m.Room * context.Range(0.4f, 0.8f);
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            Mem m = Memory<Mem>(context);
            if (reason != ExitReason.Finished)
                context?.Actuator?.Stop();

            // Left without the place — taken away, or lost its target — it waits for a place no more (handbook §37.3);
            // a place it got is its own (Release Attack Slot gives it back)
            if (!m.Got && m.Asked != null && context != null)
                m.Asked.Tokens.Withdraw(context.Agent, AttackSlots.PoolOf(""));

            m.Asked = null;
        }
    }
}

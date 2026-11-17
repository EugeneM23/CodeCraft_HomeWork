using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>Why a step ended.</summary>
    public enum StepEnd
    {
        None,
        Arrived,
        TimeUp,
        OutOfBand,
        Blocked
    }

    /// <summary>Which way a Step goes, relative to the target.</summary>
    public enum StepDirection
    {
        /// <summary>Sideways, the side chosen by itself: across its place on the ring to the other side of it.</summary>
        Aside,
        Left,
        Right,

        /// <summary>A step toward the target, into the near half of the band.</summary>
        In,

        /// <summary>A step back from the target, into the far half of the band.</summary>
        Out
    }

    /// <summary>
    /// Wary stepping in front of the target, facing it (handbook §20b.4 p. 6: between the series — a sidestep, a step
    /// in, a step back), at Speed of the body's speed, for Time to Time Max seconds — never less than Time. It walks leg
    /// after leg: the first the way Direction says, each next one back the other way, every leg planned to fit inside
    /// Band of Radius and — with others round the target — inside its room round its own place on the ring (§18.3,
    /// §14.2: the places are shared round the target, a step keeps to its own), so a long step paces to and fro instead
    /// of walking into the neighbours. Out of the band a leg goes back to the middle of it first; its place moved far
    /// off — back to it, briskly; held by a wall — it turns aside at once. Done — its time is over; No Target — there
    /// is none. One of the blocks a Random picks between: Stand, Step, Dash.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Step", "Task", "aikit.task.step", Success = "Done", Failure = "No Target", Simple = true,
        Order = 110)]
    public sealed class StepTask : AgentTask
    {
        // What the block does by itself, said on its node (step 90): it keeps to the agent's own place on the ring
        public override string Requirement => "keeps to its place on the ring";

        private sealed class Mem
        {
            public bool Planned;
            public float Time;
            public float Until;
            public int Kind;
            public bool Correcting;
            public bool ToPlace;
            public bool HasGoal;
            public float Goal;
            public Vector3 LastAt;
            public float Pace;
            public float SlowFor;
            public float StepFrom;
            public bool Turned;
            public StepEnd LastEnd;
            public float LastLasted;
        }

        /// <summary>Why the last step of this agent ended (for the canvas and tests).</summary>
        public StepEnd LastEndFor(AgentContext context) => Memory<Mem>(context).LastEnd;

        /// <summary>How many seconds the last step of this agent lasted.</summary>
        public float LastLastedFor(AgentContext context) => Memory<Mem>(context).LastLasted;

        private static TaskStatus Ended(Mem m, StepEnd why)
        {
            m.LastEnd = why;
            m.LastLasted = m.Time;
            return TaskStatus.Success;
        }

        /// <summary>Slower than this part of the pace it has walked at, the step is held by something.</summary>
        private const float BLOCKED_BELOW = 0.5f;

        /// <summary>Seconds it has to be held before the step counts as blocked.</summary>
        private const float BLOCKED_FOR = 0.15f;

        /// <summary>Seconds a step from a stand is given to get going before standing still counts as blocked.</summary>
        private const float GETTING_GOING = 0.6f;

        /// <summary>Faster than this (m/s) the body was put somewhere, it did not walk: not a pace.</summary>
        private const float TELEPORT = 30f;

        /// <summary>The least room, in degrees either side of its own place on the ring, its steps may take it round the target.</summary>
        private const float AWAY_FROM_PLACE = 12f;

        /// <summary>Its room either side of its place, as a part of the angle between two places on the ring.</summary>
        private const float ROOM_OF_GAP = 0.25f;

        /// <summary>How many times its room it has to be off its place before the step goes back to it.</summary>
        private const float FAR_OFF = 1.5f;

        /// <summary>Degrees within which a step taken back toward the place counts as arrived.</summary>
        private const float ON_PLACE = 3f;

        /// <summary>How much brisker than its wary step the agent goes back to its place.</summary>
        private const float TAKE_PLACE = 2f;

        /// <summary>The most of the body's speed it uses to get back to its place: a brisk step, never a run.</summary>
        private const float BRISKEST = 0.6f;

        private const int LEFT = 1, RIGHT = 2, IN = 3, OUT = 4;

        /// <summary>The Transform variable to step in front of; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        public StepDirection Direction = StepDirection.Aside;

        /// <summary>How fast, as a part of the body's full speed (a wary step, not a run).</summary>
        public BlackboardValue<float> Speed = 0.4f;

        /// <summary>The shortest the step lasts, in seconds: it never ends sooner.</summary>
        public BlackboardValue<float> Time = 0.8f;

        /// <summary>Above Time — the step lasts a random time between the two.</summary>
        public BlackboardValue<float> TimeMax = 1.6f;

        /// <summary>How far from the target it keeps.</summary>
        public BlackboardValue<float> Radius = 4f;

        /// <summary>How far in and out of the radius its steps may take it.</summary>
        public BlackboardValue<float> Band = 1f;

        [NonSerialized] private BlackboardVariable<Transform> _target;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Planned = false;
            m.Time = 0f;
            m.Pace = 0f;
            m.SlowFor = 0f;
            m.StepFrom = 0f;
            m.Turned = false;
            m.LastAt = context != null ? context.Position : Vector3.zero;
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
            if (distance < 1e-3f)
                return TaskStatus.Success;

            Vector3 inward = toTarget / distance;
            float radius = Radius.Get(context);
            float band = Mathf.Max(0f, Band.Get(context));

            // How far round the target it is from its own place on the ring (degrees, + to the left), and how much room
            // round its place it has: a part of the angle between two places, never less than AWAY_FROM_PLACE
            float bearing = Mathf.Atan2(-inward.x, -inward.z) * Mathf.Rad2Deg;
            float drift = 0f, room = float.PositiveInfinity;
            if (AttackSlots.PlaceAround(context, target, out float place))
            {
                drift = Mathf.DeltaAngle(place, bearing);
                Engagement ring = AttackSlots.EngagementOf(context, target);
                room = Mathf.Max(AWAY_FROM_PLACE, (ring != null ? ring.Ring.Gap : 360f) * ROOM_OF_GAP);
            }

            bool outside = distance < radius - band || distance > radius + band;
            if (!m.Planned)
            {
                // How long this step lasts: between Time and Time Max, and never less than Time (the user's word: "with
                // a minimum time of 3 seconds the action never lasts less than 3 seconds")
                float time = Time.Get(context);
                float timeMax = TimeMax.Get(context);
                m.Until = Mathf.Max(0.1f, timeMax > time ? context.Range(time, timeMax) : time);
                Leg(context, m, distance, radius, band, bearing, drift, room, outside, 0);
                m.Planned = true;
            }

            // The step lasts its time, all of it: that is the only way it ends
            if (m.Time >= m.Until)
            {
                actuator.Stop();
                return Ended(m, StepEnd.TimeUp);
            }

            // Within its time it walks leg after leg: one leg goes where it was headed — inside its band and its room
            // round its place on the ring, so the neighbours keep theirs — and the next one turns back across its room
            // (in and out — back over the radius). A sidestep is headed for a spot round the target, not an angle off its
            // place: the place turns after the fighter, and a goal that moved with it would run away from the leg
            bool arrived = m.HasGoal && m.Kind switch
            {
                LEFT => m.ToPlace ? drift >= m.Goal : Mathf.DeltaAngle(m.Goal, bearing) >= 0f,
                RIGHT => m.ToPlace ? drift <= m.Goal : Mathf.DeltaAngle(m.Goal, bearing) <= 0f,
                IN => distance <= m.Goal,
                OUT => distance >= m.Goal,
                _ => true,
            };

            // Held by a wall or the edge of its ground: it turns away at once instead of standing there (the animator
            // showed a stop and the stance in the middle of the node); in a corner it keeps turning, it does not stand
            if (Blocked(context, m, dt))
                TurnAway(context, m, bearing, room);
            else if (arrived || outside && !m.Correcting)
                Leg(context, m, distance, radius, band, bearing, drift, room, outside,
                    // after a leg that only put it right (into the band, to its place): Direction again; a step that
                    // ran into a wall paces aside for the rest of its time — never back into the wall
                    m.Turned ? m.Kind : m.Correcting || m.ToPlace ? 0 : m.Kind);

            if (Claim(context, BodyAxis.Move, MoveAuthority.Strafe))
            {
                // Relative to where the target is now: a sidestep stays square to it the whole way (a fixed line in the
                // world would turn into a step back and leave the band), in and out stay along it
                Vector3 side = new Vector3(-inward.z, 0f, inward.x);
                Vector3 direction = m.Kind switch
                {
                    LEFT => side,
                    RIGHT => -side,
                    IN => inward,
                    _ => -inward,
                };
                float scale = Speed.Get(context);
                actuator.Move(direction, m.ToPlace ? Mathf.Min(BRISKEST, scale * TAKE_PLACE) : scale);
            }

            if (Claim(context, BodyAxis.Face, MoveAuthority.Strafe))
                actuator.Face(inward);
            return TaskStatus.Running;
        }

        // Where the next leg goes: back into the band if it is out of it, back to its place if that moved far off, else
        // the way Direction says — the first leg — or back the other way (after: the kind of the leg just walked) —
        // given where it is headed, so it fits its room and its band
        private void Leg(AgentContext context, Mem m, float distance, float radius, float band, float bearing,
            float drift, float room, bool outside, int after)
        {
            m.StepFrom = m.Time;                        // a new leg gets going anew: the wall check gives it its time
            m.Pace = 0f;
            m.SlowFor = 0f;
            m.Correcting = outside;
            m.ToPlace = false;
            m.HasGoal = true;
            bool farOff = Mathf.Abs(drift) > room * FAR_OFF;

            if (distance < radius - band)
            {
                m.Kind = OUT;                           // too near: back to the middle of the band — from its edge
                m.Goal = radius;                        // the next step would be a step back again, never aside
                return;
            }

            if (distance > radius + band)
            {
                m.Kind = IN;                            // too far: in to the middle of the band
                m.Goal = radius;
                return;
            }

            if (farOff)
            {
                // Its place has moved far off: back toward it, briskly — that keeps ten of them spread round the target
                m.ToPlace = true;
                m.Kind = drift > 0f ? RIGHT : LEFT;
                m.Goal = drift > 0f ? ON_PLACE : -ON_PLACE;
                return;
            }

            // The way this leg goes: the first — Direction's; the next — back the way the last one came
            bool alongTheLine = after == IN || after == OUT ||
                                after == 0 && (Direction == StepDirection.In || Direction == StepDirection.Out);
            switch (alongTheLine)
            {
                case true:
                    // Over the radius to its half of the band
                    m.Kind = after == 0 ? (Direction == StepDirection.In ? IN : OUT) : after == IN ? OUT : IN;
                    m.Goal = radius + (m.Kind == OUT ? 1f : -1f) * band * context.Range(0.4f, 0.8f);
                    return;

                default:
                    // Aside: across its place to the other side of it, as a fighter paces in front of a foe — from one
                    // side of its room to the other, from the middle either way. Left / Right: that side of its place;
                    // a later leg — back to the other side
                    bool left = after == RIGHT || after == 0 && Direction == StepDirection.Left;
                    if (after == 0 && Direction == StepDirection.Aside)
                    {
                        float lean = float.IsInfinity(room) ? 0f : drift / room;
                        left = lean < -0.3f || (lean <= 0.3f && context.Chance(0.5f));
                    }

                    m.Kind = left ? LEFT : RIGHT;
                    m.HasGoal = !float.IsInfinity(room);   // with no place on a ring only its time ends a sidestep
                    m.Goal = bearing - drift + (left ? 1f : -1f) * room * context.Range(0.4f, 0.8f);
                    return;
            }
        }

        // Whether the step has run into something. Only its own going is watched — how fast the body moved against the
        // pace it has walked at in this step — never the world round it (a task does not scan the world)
        private static bool Blocked(AgentContext context, Mem m, float dt)
        {
            Vector3 at = context.Position;
            Vector3 moved = at - m.LastAt;
            m.LastAt = at;
            moved.y = 0f;
            float rate = dt > 0f ? moved.magnitude / dt : 0f;
            if (rate > TELEPORT)
                return false;                           // put somewhere else: not a pace it walks at

            m.Pace = Mathf.Max(m.Pace, rate);
            bool slow = rate < m.Pace * BLOCKED_BELOW || m.Pace < 0.05f;
            m.SlowFor = slow ? m.SlowFor + dt : 0f;
            return m.SlowFor >= BLOCKED_FOR && m.Time - m.StepFrom >= (m.Pace < 0.05f ? GETTING_GOING : BLOCKED_FOR);
        }

        // The held step gives way to one away from it: a sidestep to the other side, a step in or out turns into a
        // sidestep. It is taken to its end (Correcting), so the band does not send it straight back into the wall
        private void TurnAway(AgentContext context, Mem m, float bearing, float room)
        {
            int was = m.Kind;
            m.Turned = true;
            m.StepFrom = m.Time;
            m.SlowFor = 0f;
            m.Pace = 0f;
            m.Correcting = true;
            m.ToPlace = false;
            bool left = was == RIGHT || (was != LEFT && context.Chance(0.5f));
            m.Kind = left ? LEFT : RIGHT;
            m.HasGoal = !float.IsInfinity(room);
            m.Goal = bearing + (left ? 1f : -1f) * (float.IsInfinity(room) ? 0f : room) * context.Range(0.4f, 0.8f);
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (reason != ExitReason.Finished)
                context?.Actuator?.Stop();
        }
    }
}

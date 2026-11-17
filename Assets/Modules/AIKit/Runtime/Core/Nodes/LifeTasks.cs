using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Peaceful life, one step of a day: walks to a place, turns the way the place looks and does something there for
    /// a while — the body shows the action by its name (an animation: digging, hammering, fishing; see
    /// the character's Actions, which its AI Animator shows). Success ("Done") — the action is over (its ending, if it has one,
    /// was shown too).
    /// Failure ("Failed") — no place, or no way to it. No Place at all — the action is done where the agent stands.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Life/Perform", "Task", "aikit.task.perform", Order = 10, Success = "Done", Failure = "Failed", Simple = true)]
    public sealed class PerformTask : AgentTask
    {
        private const int WALK = 0;
        private const int ACT = 1;
        private const int END = 2;

        /// <summary>Seconds next to the place without reaching it (somebody stands on it): the action is done from here.</summary>
        private const float STUCK_TIME = 1.5f;

        /// <summary>Seconds of walking after which the place counts as out of reach.</summary>
        private const float WALK_LIMIT = 60f;

        /// <summary>Seconds an action that should end by itself is waited for.</summary>
        private const float ACTION_LIMIT = 30f;

        /// <summary>Seconds the ending of an action is waited for.</summary>
        private const float END_LIMIT = 4f;

        /// <summary>Seconds after arriving during which the body still turns the way the place looks.</summary>
        private const float TURN_TIME = 1.5f;

        private sealed class Mem
        {
            public int Phase;
            public float Time;
            public float Near;
        }

        /// <summary>The Transform variable (of the agent) with the place: where to stand and which way to look.</summary>
        [VariableName(VariableType.Transform)] public string Place;

        /// <summary>The action the body shows there: the name of an animation of its animator ("Plow", "Hammer Left").</summary>
        [ActionName] public string Animation = "";

        /// <summary>Seconds of the action; 0 — until the animation ends by itself (a single throw, a single pull).</summary>
        public BlackboardValue<float> Duration = 5f;

        /// <summary>How fast it walks there, as a part of the body's full speed (no hurry: a walk, not a run).</summary>
        public BlackboardValue<float> Pace = 0.4f;

        public BlackboardValue<float> ArriveDistance = 0.3f;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _place;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Phase = WALK;
            m.Time = 0f;
            m.Near = 0f;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            bool hasPlace = !string.IsNullOrEmpty(Place);
            Transform place = hasPlace ? _place.Get(context.Blackboard, Place) : null;
            if (hasPlace && place == null)
                return TaskStatus.Failure;              // the variable holds no object: nowhere to go

            Mem m = Memory<Mem>(context);
            ActionState actions = context.Actions;
            m.Time += dt;

            if (m.Phase == WALK)
            {
                if (place != null && !Arrived(context, m, place, dt))
                {
                    INavigator navigation = context.Navigation;
                    if (navigation is INavigationPace pace)
                        pace.Pace = Pace.Get(context);

                    if (navigation == null || !navigation.SetDestination(place.position).CanGo() ||
                        navigation.Status == NavStatus.Unreachable || m.Time > WALK_LIMIT)
                        return TaskStatus.Failure;

                    if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                        navigation.Step(dt);
                    return TaskStatus.Running;
                }

                context.Navigation?.Clear();
                context.Actuator?.Stop();
                actions.Begin(Animation);
                m.Phase = ACT;
                m.Time = 0f;
                return TaskStatus.Running;
            }

            if (m.Phase == ACT)
            {
                if (place != null && m.Time < TURN_TIME && Claim(context, BodyAxis.Face, MoveAuthority.Frozen))
                    context.Actuator?.Face(place.forward);

                float duration = Duration.Get(context);
                bool acting = duration > 0f
                    ? m.Time < duration
                    : m.Time < 0.3f || (actions.IsShown && m.Time < ACTION_LIMIT);
                if (acting)
                    return TaskStatus.Running;

                actions.End(false);
                m.Phase = END;
                m.Time = 0f;
                return TaskStatus.Running;
            }

            // The work is put down before the agent walks on
            return actions.IsShown && m.Time < END_LIMIT ? TaskStatus.Running : TaskStatus.Success;
        }

        // At the place — or next to it for a while with no way onto it (another one stands there)
        private bool Arrived(AgentContext context, Mem m, Transform place, float dt)
        {
            Vector3 to = place.position - context.Position;
            to.y = 0f;
            float distance = to.magnitude;
            float arrive = Mathf.Max(0.05f, ArriveDistance.Get(context));
            m.Near = distance <= arrive * 4f ? m.Near + dt : 0f;
            return distance <= arrive || m.Near >= STUCK_TIME;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context == null)
                return;

            if (context.Navigation is INavigationPace pace)
                pace.Pace = 1f;

            // The walk is called off on every way out, a failure too (handbook §2.1): a NavMesh agent walks on by itself
            context.Navigation?.Clear();
            if (reason == ExitReason.Finished)
                return;

            if (Memory<Mem>(context).Phase != WALK)
                context.Actions.End(true);              // taken away (a more important decision): the work is dropped at once
            context.Actuator?.Stop();
        }
    }

    /// <summary>
    /// A talk: walks to a meeting place and waits there for somebody else who came to talk; when there is one, they
    /// stand around the middle of the place facing it and talk for a while (the body shows the action by its name —
    /// an idle pose will do). The place has Seats places; who came when all are taken waits and leaves.
    /// Success ("Done") — talked. Failure ("Alone") — nobody came in WaitTime (or no place, no way to it).
    /// Who sits where is kept by the group of the scene (the roster's tokens of the place): the agents do not ask
    /// each other.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Life/Chat", "Task", "aikit.task.chat", Order = 20, Success = "Done", Failure = "Alone", Simple = true)]
    public sealed class ChatTask : AgentTask
    {
        /// <summary>The pool of a meeting place's tokens: a token is a seat.</summary>
        public const string POOL = "Chat";

        /// <summary>From this far the agent asks for a seat.</summary>
        private const float NEAR = 3f;

        private const float WALK_LIMIT = 60f;
        private const float SEAT_LIMIT = 5f;
        private const float END_LIMIT = 4f;

        /// <summary>Seconds one keeps talking after the others have left.</summary>
        private const float LAST_WORDS = 1.5f;

        private sealed class Mem
        {
            public float Time;
            public float Waited;
            public float Talked;
            public float Seating;
            public float Ending;
            public bool HasSeat;
            public bool Talking;
            public bool IsEnding;
            public Vector3 Seat;
        }

        /// <summary>The Transform variable (of the agent) with the meeting place.</summary>
        [VariableName(VariableType.Transform)] public string Place;

        /// <summary>The action the body shows while talking (the name of an animation of its animator).</summary>
        [ActionName] public string Animation = "";

        /// <summary>Seconds of the talk.</summary>
        public BlackboardValue<float> Duration = 6f;

        /// <summary>Seconds it waits for somebody to talk to.</summary>
        public BlackboardValue<float> WaitTime = 6f;

        /// <summary>How many can talk at the place at once.</summary>
        public int Seats = 2;

        /// <summary>How far from the middle of the place each one stands.</summary>
        public BlackboardValue<float> Radius = 0.7f;

        /// <summary>How fast it walks there, as a part of the body's full speed.</summary>
        public BlackboardValue<float> Pace = 0.4f;

        // One look at a time (the main thread): who holds a seat at the place being asked
        private static readonly List<AICharacter> Holders = new();

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _place;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Time = 0f;
            m.Waited = 0f;
            m.Talked = 0f;
            m.Seating = 0f;
            m.Ending = 0f;
            m.HasSeat = false;
            m.Talking = false;
            m.IsEnding = false;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            Transform place = string.IsNullOrEmpty(Place) ? null : _place.Get(context.Blackboard, Place);
            if (place == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            ActionState actions = context.Actions;
            if (m.IsEnding)
            {
                m.Ending += dt;
                return actions.IsShown && m.Ending < END_LIMIT
                    ? TaskStatus.Running
                    : TaskStatus.Success;
            }

            m.Time += dt;
            INavigator navigation = context.Navigation;
            if (navigation is INavigationPace pace)
                pace.Pace = Pace.Get(context);

            Vector3 center = place.position;
            Vector3 toCenter = center - context.Position;
            toCenter.y = 0f;
            float distance = toCenter.magnitude;

            // Up to the place
            if (!m.HasSeat && distance > NEAR)
            {
                if (navigation == null || !navigation.SetDestination(center).CanGo() ||
                    navigation.Status == NavStatus.Unreachable || m.Time > WALK_LIMIT)
                    return TaskStatus.Failure;

                if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                    navigation.Step(dt);
                return TaskStatus.Running;
            }

            // A seat: the place's token. Who else holds one is somebody to talk to
            AIWorldRunner.Join(context.Agent);
            Engagement circle = context.Roster?.EngagementOf(place);
            int others = 0;
            bool seated = false;
            if (circle != null)
            {
                circle.Tokens.HoldFirmly(POOL);
                circle.Tokens.SetCapacity(POOL, Mathf.Max(2, Seats));
                seated = circle.Tokens.Request(context.Agent, POOL, 1, 0f, distance) == TokenResult.Granted;
                circle.Tokens.HoldersOf(POOL, Holders);
                if (seated && !m.HasSeat)
                {
                    m.Seat = FreeSeat(context, place);
                    m.HasSeat = true;
                }

                for (int i = 0; i < Holders.Count; i++)
                {
                    if (Holders[i] != context.Agent)
                        others++;
                }

                Holders.Clear();
            }

            if (!seated)
            {
                // Every seat is taken (or there is no group at all): stands by for a while, then goes
                navigation?.Clear();
                m.Waited += dt;
                return m.Waited >= WaitTime.Get(context) ? TaskStatus.Failure : TaskStatus.Running;
            }

            // To the seat; the eyes on the middle of the place
            Vector3 toSeat = m.Seat - context.Position;
            toSeat.y = 0f;
            if (!m.Talking && toSeat.magnitude > 0.35f && m.Seating < SEAT_LIMIT && navigation != null &&
                navigation.SetDestination(m.Seat).CanGo())
            {
                m.Seating += dt;
                if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                    navigation.Step(dt);
                return TaskStatus.Running;
            }

            navigation?.Clear();
            if (toCenter.sqrMagnitude > 0.01f && Claim(context, BodyAxis.Face, MoveAuthority.Frozen))
                context.Actuator?.Face(toCenter);

            if (others == 0 && !m.Talking)
            {
                m.Waited += dt;
                return m.Waited >= WaitTime.Get(context) ? TaskStatus.Failure : TaskStatus.Running;
            }

            if (!m.Talking)
            {
                m.Talking = true;
                actions.Begin(Animation);
            }

            m.Talked += dt;
            if (m.Talked < Duration.Get(context) && (others > 0 || m.Talked < LAST_WORDS))
                return TaskStatus.Running;

            actions.End(false);
            m.IsEnding = true;
            return TaskStatus.Running;
        }

        // The seats lie on a circle around the middle of the place. The free one is the one farthest from everybody
        // who already holds a seat (of equal ones — the nearest to this agent)
        private Vector3 FreeSeat(AgentContext context, Transform place)
        {
            int seats = Mathf.Max(2, Seats);
            float radius = Radius.Get(context);
            Vector3 best = place.position;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < seats; i++)
            {
                Vector3 seat = place.position +
                               Quaternion.AngleAxis(i * 360f / seats, Vector3.up) * place.right * radius;
                float nearestOther = 100f;
                for (int h = 0; h < Holders.Count; h++)
                {
                    AICharacter holder = Holders[h];
                    if (holder == null || holder == context.Agent || holder.Context == null)
                        continue;

                    nearestOther = Mathf.Min(nearestOther, Vector3.Distance(holder.Context.Position, seat));
                }

                float score = nearestOther * 10f - Vector3.Distance(context.Position, seat);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = seat;
                }
            }

            return best;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context == null)
                return;

            context.ReleaseToken(POOL);                 // the seat is free for the next one
            if (context.Navigation is INavigationPace pace)
                pace.Pace = 1f;

            context.Navigation?.Clear();               // the walk is called off on every way out
            if (reason == ExitReason.Finished)
                return;

            Mem m = Memory<Mem>(context);
            if (m.Talking || m.IsEnding)
                context.Actions.End(true);              // torn away from the talk: it stops talking at once
            context.Actuator?.Stop();
        }
    }
}

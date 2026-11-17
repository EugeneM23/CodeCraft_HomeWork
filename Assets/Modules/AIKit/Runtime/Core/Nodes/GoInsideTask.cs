using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Go Inside: walks to a door (Place) and goes in — nothing of the character is drawn and its body is in nobody's
    /// way (AICharacter.IsInside) — for Duration seconds, then comes out where it went in. 0 — it stays inside until it
    /// is taken away (its decision lets go: the morning, a fight, a meeting). Home to sleep, into the tavern, the
    /// bathhouse: the graph says the character went in — no component hides it behind the graph's back (handbook B.6:
    /// fewer hidden states). Success ("Done") — came out after Duration. Failure ("Failed") — no place, or no way to it.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Life/Go Inside", "Task", "aikit.task.go-inside", Success = "Done", Failure = "Failed", Simple = true)]
    public sealed class GoInsideTask : AgentTask
    {
        /// <summary>Seconds next to the door without reaching it (somebody stands on it): it goes in from here.</summary>
        private const float STUCK_TIME = 1.5f;

        /// <summary>Seconds of walking after which the door counts as out of reach.</summary>
        private const float WALK_LIMIT = 60f;

        private sealed class Mem
        {
            public bool In;
            public float Time;
            public float Near;
        }

        /// <summary>The Transform variable (of the agent) with the door: where it goes in and comes out.</summary>
        [VariableName(VariableType.Transform)] public string Place;

        /// <summary>Seconds inside; 0 — until the block is taken away.</summary>
        public BlackboardValue<float> Duration = 0f;

        /// <summary>How fast it walks to the door, as a part of the body's full speed.</summary>
        public BlackboardValue<float> Pace = 0.4f;

        public BlackboardValue<float> ArriveDistance = 0.3f;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _place;

        /// <summary>The agent is inside now (tests and the canvas).</summary>
        public bool IsInsideFor(AgentContext context) => Memory<Mem>(context).In;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.In = false;
            m.Time = 0f;
            m.Near = 0f;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            Transform door = string.IsNullOrEmpty(Place) ? null : _place.Get(context.Blackboard, Place);
            if (door == null)
                return TaskStatus.Failure;              // the variable holds no object: nowhere to go in

            Mem m = Memory<Mem>(context);
            m.Time += dt;
            if (!m.In)
            {
                if (!Arrived(context, m, door, dt))
                {
                    INavigator navigation = context.Navigation;
                    if (navigation is INavigationPace pace)
                        pace.Pace = Pace.Get(context);

                    if (navigation == null || !navigation.SetDestination(door.position).CanGo() ||
                        navigation.Status == NavStatus.Unreachable || m.Time > WALK_LIMIT)
                        return TaskStatus.Failure;

                    if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                        navigation.Step(dt);
                    return TaskStatus.Running;
                }

                context.Navigation?.Clear();
                context.Actuator?.Stop();
                SetInside(context, true);
                m.In = true;
                m.Time = 0f;
                return TaskStatus.Running;
            }

            float duration = Duration.Get(context);
            if (duration <= 0f || m.Time < duration)
            {
                if (Claim(context, BodyAxis.Move, MoveAuthority.Frozen))
                    context.Actuator?.Stop();
                return TaskStatus.Running;
            }

            SetInside(context, false);
            return TaskStatus.Success;
        }

        // At the door — or next to it for a while with no way onto it (another one stands there)
        private bool Arrived(AgentContext context, Mem m, Transform door, float dt)
        {
            Vector3 to = door.position - context.Position;
            to.y = 0f;
            float distance = to.magnitude;
            float arrive = Mathf.Max(0.05f, ArriveDistance.Get(context));
            m.Near = distance <= arrive * 4f ? m.Near + dt : 0f;
            return distance <= arrive || m.Near >= STUCK_TIME;
        }

        private static void SetInside(AgentContext context, bool inside)
        {
            if (context.Agent != null)
                context.Agent.IsInside = inside;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context == null)
                return;

            // Out again on every way out: the morning came, a fight, a meeting — the character steps out of the door
            if (Memory<Mem>(context).In)
                SetInside(context, false);
            Memory<Mem>(context).In = false;

            if (context.Navigation is INavigationPace pace)
                pace.Pace = 1f;
            context.Navigation?.Clear();
        }
    }
}

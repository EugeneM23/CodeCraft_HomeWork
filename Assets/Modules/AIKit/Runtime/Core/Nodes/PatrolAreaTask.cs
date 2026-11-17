using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Patrols an area: picks a random place on an object (Area — a Transform variable, optional: an AI Object with a zone
    /// walks within its zone, another object within its drawn box seen from above) or, with no object, within Radius of
    /// the character's home — where it woke up (handbook §25б.1; step 90: not where the patrol began, which drifted after
    /// every chase) —, walks there, stands for a pause and picks the next. A place with no way to it is dropped and
    /// another is picked. The task never finishes: the Brain takes the agent away from it when a more important decision
    /// holds.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Patrol Area", "Task", "aikit.task.patrol-area", Simple = true, Endless = true)]
    public sealed class PatrolAreaTask : AgentTask
    {
        private sealed class Mem
        {
            public Vector3 Origin;
            public Vector3 Point;
            public bool HasPoint;
            public float PauseLeft;
        }

        /// <summary>The Transform variable (of the agent) with the object to patrol (optional); empty — around the home.</summary>
        [VariableName(VariableType.Transform)] public string Area;

        /// <summary>How far from the home to wander when there is no area object (or it has no area).</summary>
        public BlackboardValue<float> Radius = 5f;

        /// <summary>Seconds of standing at each place.</summary>
        public BlackboardValue<float> Pause = 1f;

        /// <summary>Above Pause — the pause is random between the two.</summary>
        public BlackboardValue<float> PauseMax = 2f;

        public BlackboardValue<float> ArriveDistance = 0.4f;

        /// <summary>
        /// How fast it walks, as a part of the body's full speed: 1 — as fast as the body goes; below — a stroll (the
        /// animator shows a calm walk for a slow body). Full speed again when the patrol is left.
        /// </summary>
        public BlackboardValue<float> Pace = 1f;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _area;

        /// <summary>The place this agent walks to now (debugging and tests); null — it stands or looks for one.</summary>
        public Vector3? CurrentPointFor(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            return m.HasPoint ? m.Point : null;
        }

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.HasPoint = false;
            m.PauseLeft = 0f;
            if (context != null)
            {
                context.WakeUp();                       // a context nobody stepped yet: it wakes up here
                m.Origin = context.Home;
            }
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            INavigator navigation = context?.Navigation;
            if (navigation == null)
                return TaskStatus.Failure;

            if (navigation is INavigationPace pace)
                pace.Pace = Pace.Get(context);

            Mem m = Memory<Mem>(context);
            if (m.PauseLeft > 0f)
            {
                m.PauseLeft -= dt;
                return TaskStatus.Running;
            }

            if (!m.HasPoint)
            {
                // One place per step: a bad one (off the walkable ground) costs a step, not a loop
                Transform area = string.IsNullOrEmpty(Area) ? null : _area.Get(context.Blackboard, Area);
                IAreaSampler sampler = context.Areas ?? BoundsAreaSampler.Shared;
                float randomX = (float)context.NextRandom();
                float randomZ = (float)context.NextRandom();
                Vector3 point = area != null && area.TryGetComponent(out AIObject zone) && zone.HasZone
                    ? zone.RandomPoint(randomX, randomZ)
                    : sampler.RandomPoint(area, m.Origin, Radius.Get(context), randomX, randomZ);
                if (!navigation.SetDestination(point).CanGo())
                    return TaskStatus.Running;

                m.Point = point;
                m.HasPoint = true;
                return TaskStatus.Running;
            }

            if (!navigation.Status.IsMoving())
            {
                m.HasPoint = false;                     // gave up on the way: another place
                return TaskStatus.Running;
            }

            if (navigation.RemainingDistance <= ArriveDistance.Get(context))
            {
                navigation.Clear();
                m.HasPoint = false;
                float pause = Pause.Get(context);
                float pauseMax = PauseMax.Get(context);
                m.PauseLeft = pauseMax > pause ? context.Range(pause, pauseMax) : pause;
                return TaskStatus.Running;
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context?.Navigation is INavigationPace pace)
                pace.Pace = 1f;

            context?.Navigation?.Clear();
            context?.Actuator?.Stop();
        }
    }
}

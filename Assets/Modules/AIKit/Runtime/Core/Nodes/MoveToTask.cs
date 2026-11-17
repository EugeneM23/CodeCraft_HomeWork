using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Goes to an object (Target — a Transform variable) or, with no Target, to a point, and finishes when closer than
    /// StopDistance. An object is followed while it moves. An object of the world (what a Find found: a rock, a cover, a
    /// turret) is walked to the way the object says (step 90): to its place for this character — behind a cover, away
    /// from the Brain's target; at a turret, its seat — and reached within its own reach; the claim on it is kept while
    /// the character walks, and an object there is no way to goes into the character's unreachable journal (it is not
    /// offered to it for a while, handbook §24). Somebody stands on the object's place (the mayor at the well): next to
    /// it — still within what the object's scenario reaches (its reach + LEAVE_SLACK) — and coming no nearer for
    /// Walking.STUCK_TIME, the character counts as there, as Perform and Go Inside do.
    /// Failure — the navigator says there is no way there (at once or later, when it gives up), or the Target variable
    /// holds no object — so the graph can pick another plan instead of standing still.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Move To", "Task", "aikit.task.move-to", Success = "Reach Position", Failure = "No Path",
        Simple = true)]
    public sealed class MoveToTask : AgentTask
    {
        /// <summary>Metres nearer that count as coming nearer to the place (less is standing and pressing).</summary>
        private const float PROGRESS = 0.05f;

        private sealed class Mem
        {
            public Transform Seen;
            public WorldObject Object;
            public float Nearest = float.MaxValue;     // the nearest it came since it last came nearer, next to the place
            public float Stuck;                         // seconds next to the place coming no nearer
        }

        public MoveToTask()
        {
        }

        public MoveToTask(BlackboardValue<Vector3> destination, float stopDistance = 0.3f)
        {
            Destination = destination;
            StopDistance = stopDistance;
        }

        /// <summary>The Transform variable (of the agent) to walk to; empty — Destination is used.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        public BlackboardValue<Vector3> Destination;

        public BlackboardValue<float> StopDistance = 0.3f;

        /// <summary>On — the destination is re-read every step (a moving point). An object is always followed.</summary>
        public bool Follow;

        /// <summary>How fast it goes, as a part of the body's full speed (1 — as fast as the body goes).</summary>
        public BlackboardValue<float> Pace = 1f;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        private bool HasTarget => !string.IsNullOrEmpty(Target);

        public override string Requirement => HasTarget
            ? "to an object of the world: its place for me (against the Brain's target), within its reach — or beside " +
              "it when somebody stands there"
            : null;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Seen = null;
            m.Object = null;
            m.Nearest = float.MaxValue;
            m.Stuck = 0f;
            if (TryGoal(context, m, out Vector3 goal))
                context?.Navigation?.SetDestination(goal);
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            INavigator navigation = context?.Navigation;
            if (navigation == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            if (!TryGoal(context, m, out Vector3 goal))
                return TaskStatus.Failure;              // the variable holds no object: nowhere to go

            if (navigation is INavigationPace pace)
                pace.Pace = Pace.Get(context);

            WorldObject target = m.Object;
            if (target != null)
            {
                context.WorldObjects.Renew(target, context.Agent);     // still going for it: the claim stays
                if (AtPlace(context, target, goal) || Beside(context, m, target, goal, dt))
                {
                    navigation.Clear();
                    context.Actuator?.Stop();
                    return TaskStatus.Success;
                }
            }

            if (Follow || HasTarget)
                navigation.SetDestination(goal);

            if (!navigation.Status.IsMoving())
            {
                if (target != null)
                    context.WorldObjects.ReportUnreachable(context.Agent, target);
                return TaskStatus.Failure;              // no way there (Unreachable) or nothing to follow
            }

            if (target == null && navigation.RemainingDistance <= StopDistance.Get(context))
                return TaskStatus.Success;

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context?.Navigation is INavigationPace pace)
                pace.Pace = 1f;
            context?.Navigation?.Clear();
            if (reason != ExitReason.Finished)
                context?.Actuator?.Stop();
        }

        // Within the object's reach of its place (flat)
        private static bool AtPlace(AgentContext context, WorldObject target, Vector3 place)
        {
            Vector3 to = place - context.Position;
            to.y = 0f;
            return to.sqrMagnitude <= target.Reach * target.Reach;
        }

        // Next to the place (within what the object's scenario reaches) and coming no nearer for STUCK_TIME: somebody
        // stands on it — it is there. A walk that keeps coming nearer, however slowly, is never cut short
        private static bool Beside(AgentContext context, Mem m, WorldObject target, Vector3 place, float dt)
        {
            Vector3 to = place - context.Position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > target.Reach + WorldObject.LEAVE_SLACK)
            {
                m.Nearest = float.MaxValue;
                m.Stuck = 0f;
                return false;
            }

            if (distance < m.Nearest - PROGRESS)
            {
                m.Nearest = distance;
                m.Stuck = 0f;
                return false;
            }

            m.Stuck += dt;
            return m.Stuck >= Walking.STUCK_TIME;
        }

        // Where to go: Destination; the object of the Target variable — an object of the world: its place for this agent
        private bool TryGoal(AgentContext context, Mem m, out Vector3 goal)
        {
            if (!HasTarget)
            {
                goal = Destination.Get(context);
                return true;
            }

            Transform target = _target.Get(context?.Blackboard, Target);
            if (target == null)
            {
                goal = default;
                return false;
            }

            if (target != m.Seen)
            {
                m.Seen = target;
                m.Object = target.GetComponentInParent<WorldObject>();
            }

            goal = m.Object != null ? m.Object.PlaceFor(Offer.Occupy, context, context.BrainTarget) : target.position;
            return true;
        }
    }
}

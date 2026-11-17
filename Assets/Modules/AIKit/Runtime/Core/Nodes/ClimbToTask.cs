using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Goes to the target over the walls: while this node runs, the character's paths may run over the climbing walls
    /// (the body climbs them up or down the way its clip shows — handbook §13.2–13.3: what an edge asks is the body's,
    /// the edge is executed by the body), and it walks the path to the target until no wall is left between them.
    /// Success ("Climbed") — the target can be reached on foot from where the body stands (at once, when there was no
    /// wall to begin with): what comes next walks; Failure ("No Way") — no target, a body that does not climb, or no way
    /// to the target even over the walls. A climb that has started is not cut short (a Brain does not take the
    /// character elsewhere while it is on a wall). The walls are shut again when the node is left. For a Brain's target
    /// up on a wall (the Climb decision), and for the way home: Climb To the patrol zone before patrolling it.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Climb To", "Task", "aikit.task.climb-to", Order = 90, Success = "Climbed", Failure = "No Way",
        Simple = true)]
    public sealed class ClimbToTask : AgentTask
    {
        /// <summary>The Transform variable to go to; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>A body on a wall finishes the climb first.</summary>
        public override bool CommitsFor(AgentContext context) =>
            context?.Navigation is IClimbingNavigator { ClimbDirection: not 0 };

        private Transform TargetOf(AgentContext context) =>
            string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);

        protected override void OnEnter(AgentContext context)
        {
            if (context?.Navigation is IClimbingNavigator climber)
                climber.ClimbAllowed = true;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            INavigator navigation = context?.Navigation;
            if (navigation is not IClimbingNavigator { CanClimb: true } climber)
                return TaskStatus.Failure;

            if (climber.ClimbDirection != 0)
                return TaskStatus.Running;              // on a wall: the body climbs, nothing to ask meanwhile

            Transform target = TargetOf(context);
            if (target == null)
                return TaskStatus.Failure;

            if (climber.WayTo(WorldObject.PlaceOf(target, context)) == ClimbWay.OnFoot)
            {
                navigation.Clear();
                return TaskStatus.Success;              // no wall left between: what comes next walks
            }

            climber.ClimbAllowed = true;                // a block before may have shut them

            navigation.SetDestination(WorldObject.PlaceOf(target, context));   // an object of the world: its place (a perch: its top)
            if (!navigation.Status.IsMoving())
                return TaskStatus.Failure;              // no way, not even over the walls

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context?.Navigation is IClimbingNavigator climber)
                climber.ClimbAllowed = false;           // the walls are this node's: shut once it is left
        }
    }
}

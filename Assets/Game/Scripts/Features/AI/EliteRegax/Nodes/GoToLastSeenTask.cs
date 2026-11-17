using System;
using AIKit;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Go To Last Seen: walks (by the navigator) to where the lost target was seen last. "There" — arrived;
    /// "No Way" — nothing lost any more, or no way to the place.
    /// </summary>
    [Serializable]
    [AIKitNode("Game/Actions/Go To Last Seen", "Task", "game.task.go-to-last-seen", Success = "There", Failure = "No Way",
        Simple = true)]
    public sealed class GoToLastSeenTask : AgentTask
    {
        [Min(0f)] public float Arrive = 1.2f;

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            INavigator navigation = context?.Navigation;
            if (navigation == null || !LastSeenReasoner.LastSeen(context, out Vector3 place))
                return TaskStatus.Failure;

            Vector3 to = place - context.Position;
            to.y = 0f;
            if (to.magnitude <= Arrive)
                return TaskStatus.Success;

            navigation.SetDestination(place);
            if (!navigation.Status.IsMoving())
                return TaskStatus.Failure;
            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            context?.Navigation?.Clear();
            context?.Actuator?.Stop();
        }
    }
}

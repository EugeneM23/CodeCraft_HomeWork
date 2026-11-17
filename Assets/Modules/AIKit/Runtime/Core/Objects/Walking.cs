using UnityEngine;

namespace AIKit
{
    /// <summary>A walk of a block to a point by the path, with the checks every such block makes.</summary>
    internal static class Walking
    {
        public enum Result
        {
            Walking,
            Arrived,
            NoWay
        }

        /// <summary>
        /// One step toward the point. Arrived — within reach (flat), or next to it for a while with no way closer
        /// (somebody stands there); NoWay — the navigator finds no way; Walking — on the way. near — the block's own
        /// seconds spent next to the point (kept by the block between the steps); mayMove — the block holds the legs.
        /// </summary>
        public static Result To(AgentContext context, Vector3 point, float reach, float pace, bool mayMove, float dt,
            ref float near)
        {
            Vector3 to = point - context.Position;
            to.y = 0f;
            float distance = to.magnitude;
            reach = Mathf.Max(0.05f, reach);
            near = distance <= reach * 3f + 0.5f ? near + dt : 0f;
            if (distance <= reach || near >= STUCK_TIME)
            {
                context.Navigation?.Clear();
                context.Actuator?.Stop();
                return Result.Arrived;
            }

            INavigator navigation = context.Navigation;
            if (navigation == null)
                return Result.NoWay;

            if (navigation is INavigationPace paced)
                paced.Pace = pace;
            if (!navigation.SetDestination(point).CanGo() || navigation.Status == NavStatus.Unreachable)
                return Result.NoWay;

            if (mayMove)
                navigation.Step(dt);
            return Result.Walking;
        }

        /// <summary>The pace back to the full one (on the block's way out).</summary>
        public static void FullPace(AgentContext context)
        {
            if (context?.Navigation is INavigationPace paced)
                paced.Pace = 1f;
        }

        /// <summary>Seconds next to the point with no way closer, after which the block counts it as reached.</summary>
        public const float STUCK_TIME = 1.5f;

        /// <summary>The flat direction the body looks (its object's forward); world forward without one.</summary>
        public static Vector3 Forward(AgentContext context)
        {
            Vector3 forward = context?.Self != null ? context.Self.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        /// <summary>The ground under a point near the agent (the ground probe); without one — the agent's own height.</summary>
        public static Vector3 Ground(AgentContext context, Vector3 point)
        {
            if (context?.Ground != null && context.Ground.FindGround(point + Vector3.up, Vector3.up, 3f, out Vector3 ground))
                return ground;

            point.y = context != null ? context.Position.y : point.y;
            return point;
        }

        /// <summary>Who the agent is in the world: its character's object (what a blow says it came from).</summary>
        public static Transform Self(AgentContext context) =>
            context?.Agent != null && context.Agent.EntityRoot != null ? context.Agent.EntityRoot : context?.Self;
    }
}

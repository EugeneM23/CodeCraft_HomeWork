using System;
using AIKit;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Wander: roams near the place it first started (its spawn) — a random point within <see cref="Radius"/>, walked to
    /// by the navigator, a pause there, the next point. A point with no way to it is dropped for another. Never ends.
    /// </summary>
    [Serializable]
    [AIKitNode("Game/Actions/Wander", "Task", "game.task.wander", Simple = true, Endless = true)]
    public sealed class WanderTask : AgentTask
    {
        [Min(0f)] public float Radius = 6f;
        [Min(0f)] public float PauseMin = 1f;
        [Min(0f)] public float PauseMax = 3f;
        [Min(0f)] public float Arrive = 0.6f;

        /// <summary>Seconds a point may take before it is given up (stuck on a rim, a moving obstacle).</summary>
        [Min(0f)] public float GiveUpAfter = 8f;

        private sealed class Mem
        {
            public Vector3 Origin;
            public bool HasOrigin;
            public Vector3 Point;
            public bool HasPoint;
            public float Pause;
            public float Walking;
        }

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.HasPoint = false;
            m.Pause = 0f;
            if (!m.HasOrigin && context != null)
            {
                m.Origin = context.Position;
                m.HasOrigin = true;
            }
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            INavigator navigation = context?.Navigation;
            if (navigation == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            if (m.Pause > 0f)
            {
                m.Pause -= dt;
                if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                    context.Actuator?.Move(Vector3.zero);
                return TaskStatus.Running;
            }

            if (!m.HasPoint)
            {
                float angle = context.Range(0f, Mathf.PI * 2f);
                float distance = Radius * Mathf.Sqrt(context.Range(0.1f, 1f));
                m.Point = m.Origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                m.HasPoint = true;
                m.Walking = 0f;
                navigation.Clear();
            }

            m.Walking += dt;
            if (!navigation.SetDestination(m.Point).CanGo() || navigation.Status == NavStatus.Unreachable ||
                m.Walking > GiveUpAfter)
            {
                Rest(context, navigation, m, 0.5f);
                return TaskStatus.Running;
            }

            Vector3 to = m.Point - context.Position;
            to.y = 0f;
            if (to.magnitude <= Arrive)
            {
                Rest(context, navigation, m, context.Range(PauseMin, Mathf.Max(PauseMin, PauseMax)));
                return TaskStatus.Running;
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            context?.Navigation?.Clear();
            context?.Actuator?.Stop();
        }

        private static void Rest(AgentContext context, INavigator navigation, Mem m, float pause)
        {
            m.HasPoint = false;
            m.Pause = pause;
            navigation.Clear();
            context.Actuator?.Move(Vector3.zero);
        }
    }
}

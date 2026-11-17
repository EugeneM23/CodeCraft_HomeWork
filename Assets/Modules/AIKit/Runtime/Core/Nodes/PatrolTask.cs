using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Walks a route: point by point with a pause at each, looping or ping-ponging. With no points it paces around the
    /// place it started, so an agent dropped into a scene without a route still looks alive.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Patrol", "Task", "aikit.task.patrol")]
    public sealed class PatrolTask : AgentTask
    {
        // Per agent: where it woke up, where on the route it is, and its pause
        private sealed class Mem
        {
            public Vector3 Origin;
            public bool HasOrigin;
            public int Index;
            public int Step = 1;
            public float PauseLeft;
        }

        /// <summary>The route; empty — pacing IdleRange around the place the task first started.</summary>
        public Vector3[] Points = Array.Empty<Vector3>();

        public PatrolTask()
        {
        }

        public PatrolTask(params Vector3[] points) => Points = points;

        /// <summary>Seconds of standing at each point.</summary>
        public BlackboardValue<float> PauseAtPoint = 1f;

        public BlackboardValue<float> PauseAtPointMax;

        /// <summary>On — back and forth along the route; off — around the loop.</summary>
        public bool PingPong = true;

        /// <summary>How far to pace when there is no route at all.</summary>
        public BlackboardValue<float> IdleRange = 3f;

        public BlackboardValue<float> ArriveDistance = 0.3f;

        public int CurrentIndexFor(AgentContext context) => Memory<Mem>(context).Index;

        public void SetRoute(Vector3[] points, AgentContext context = null)
        {
            Points = points;
            Mem m = Memory<Mem>(context);
            m.Index = 0;
            m.Step = 1;
        }

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.PauseLeft = 0f;
            if (m.HasOrigin || context == null)
                return;

            m.Origin = context.Position;
            m.HasOrigin = true;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            Mem m = Memory<Mem>(context);
            INavigator navigation = context?.Navigation;
            if (navigation == null)
                return TaskStatus.Failure;

            if (m.PauseLeft > 0f)
            {
                m.PauseLeft -= dt;
                if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                    context.Actuator?.Move(Vector3.zero);     // one owner per axis (handbook §15)
                return TaskStatus.Running;
            }

            Vector3 destination = CurrentPoint(context);
            if (!navigation.SetDestination(destination).CanGo())
            {
                Advance(context);
                return TaskStatus.Running;
            }

            if (navigation.RemainingDistance <= ArriveDistance.Get(context))
            {
                Advance(context);
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

        private Vector3 CurrentPoint(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            if (Points != null && Points.Length > 0)
                return Points[Mathf.Clamp(m.Index, 0, Points.Length - 1)];

            // No route: pace around the spot the agent woke up at
            float side = m.Index % 2 == 0 ? 1f : -1f;
            return m.Origin + Vector3.right * (side * IdleRange.Get(context) * 0.5f);
        }

        private void Advance(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            float pause = PauseAtPoint.Get(context);
            float pauseMax = PauseAtPointMax.Get(context);
            m.PauseLeft = pauseMax > pause && context != null ? context.Range(pause, pauseMax) : pause;

            int count = Points != null && Points.Length > 0 ? Points.Length : 2;
            if (count <= 1)
                return;

            if (!PingPong)
            {
                m.Index = (m.Index + 1) % count;
                return;
            }

            if (m.Index + m.Step >= count || m.Index + m.Step < 0)
                m.Step = -m.Step;

            m.Index += m.Step;
        }
    }
}

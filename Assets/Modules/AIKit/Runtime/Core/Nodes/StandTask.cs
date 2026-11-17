using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Stands still in its stance, turned to the target, for a while — a pause between the series, a window for the
    /// player (handbook §20b.4 p. 6). With Time Max above Time the pause is random between the two. The body is held
    /// where it is (a hand-held stop, not a path), the animator shows what the body does: standing. Done — the time is
    /// up; with no target it still stands its time. One of the blocks a Random picks between: Stand, Step, Dash.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Stand", "Task", "aikit.task.stand", Success = "Done", Simple = true, NeverFails = true,
        Order = 100)]
    public sealed class StandTask : AgentTask
    {
        private sealed class Mem
        {
            public float Left;
        }

        /// <summary>The Transform variable to look at; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>Seconds it stands.</summary>
        public BlackboardValue<float> Time = 1f;

        /// <summary>Above Time — it stands a random time between the two.</summary>
        public BlackboardValue<float> TimeMax;

        [NonSerialized] private BlackboardVariable<Transform> _target;

        protected override void OnEnter(AgentContext context)
        {
            float time = Time.Get(context);
            float timeMax = TimeMax.Get(context);
            Memory<Mem>(context).Left = timeMax > time && context != null ? context.Range(time, timeMax) : time;
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            IAgentActuator actuator = context?.Actuator;
            if (actuator != null)
            {
                if (Claim(context, BodyAxis.Move, MoveAuthority.Strafe))
                    actuator.Stop();

                Transform target = string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);
                if (target != null && Claim(context, BodyAxis.Face, MoveAuthority.Strafe))
                {
                    Vector3 to = target.position - context.Position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 1e-6f)
                        actuator.Face(to.normalized);
                }
            }

            Mem m = Memory<Mem>(context);
            m.Left -= dt;
            return m.Left <= 1e-5f ? TaskStatus.Success : TaskStatus.Running;
        }
    }
}

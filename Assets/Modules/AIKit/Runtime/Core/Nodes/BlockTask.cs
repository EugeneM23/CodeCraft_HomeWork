using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Holds the guard up for Hold seconds (handbook §19.2: a dodge that cannot be made is a block, not nothing): the
    /// character is blocking all that time (its state Blocking — the game reads it when it works out a blow, another
    /// agent's Is condition sees it), the body stands turned to the target, and the character shows the action Block
    /// (the presentation shows it if it has one; the guard holds by its own number either way). Done — the guard is
    /// lowered. A block is no blow: a more important decision may take the character away meanwhile (the guard drops).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Block", "Task", "aikit.task.block", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class BlockTask : AgentTask
    {
        private sealed class Mem
        {
            public float Left;
            public int Shown = -1;
        }

        /// <summary>The Transform variable to turn the guard to; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>Seconds the guard is held up.</summary>
        public BlackboardValue<float> Hold = 0.8f;

        /// <summary>The action the character shows while it blocks.</summary>
        [ActionName] public string Action = "Block";

        [NonSerialized] private BlackboardVariable<Transform> _target;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Left = Hold.Get(context);
            m.Shown = -1;
            if (context == null)
                return;

            context.Navigation?.Clear();
            context.IsBlocking = true;
            if (!string.IsNullOrEmpty(Action))
            {
                context.Actions.Begin(Action);
                m.Shown = context.Actions.Serial;
            }
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

        // The guard goes down: held to its end — the action's ending is shown; taken away — dropped at once. An action
        // somebody asked for after the guard's is not touched
        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context == null)
                return;

            context.IsBlocking = false;
            Mem m = Memory<Mem>(context);
            if (m.Shown >= 0 && context.Actions.Serial == m.Shown)
                context.Actions.End(atOnce: reason != ExitReason.Finished);
            m.Shown = -1;
        }
    }
}

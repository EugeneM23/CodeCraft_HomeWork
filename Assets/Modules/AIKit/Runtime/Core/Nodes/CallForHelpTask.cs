using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Calls the others for help: every agent within Radius whose sensor listens (Hear Calls) and looks for this kind
    /// of object takes the caller's target as if it saw it itself — its own "Detected" arrow fires and its own graph
    /// decides what to do. Ends at once ("Done"). An agent that came because it was called does not pass the call on
    /// (or one shout would run through the whole map from unit to unit).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Call For Help", "Task", "aikit.task.call-for-help", Success = "Done", Simple = true, Order = 80,
        NeverFails = true)]
    public sealed class CallForHelpTask : AgentTask
    {
        /// <summary>The id of the call on the signal bus.</summary>
        public const string SIGNAL = "Help";

        /// <summary>One action: the call is made once — no On Tick.</summary>
        public override bool IsOneShot => true;

        /// <summary>Seconds after hearing a call during which the agent does not call itself.</summary>
        private const float RELAY_SILENCE = 10f;

        private sealed class Mem
        {
            public int Heard;
        }

        /// <summary>The Transform variable (of the agent) with the object the help is needed against.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>How far the call is heard.</summary>
        public BlackboardValue<float> Radius = 15f;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>How many heard the last call of this agent (debugging and tests).</summary>
        public int HeardBy(AgentContext context) => Memory<Mem>(context).Heard;

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Success;

            Mem m = Memory<Mem>(context);
            m.Heard = 0;
            Transform target = _target.Get(context.Blackboard, Target);
            if (target == null || context.HasSignalSince(SIGNAL, RELAY_SILENCE))
                return TaskStatus.Success;

            AIWorldRunner.Listen(context.Agent);
            if (context.Signals == null)
                return TaskStatus.Success;

            Transform sender = context.Agent != null ? context.Agent.EntityRoot : context.Self;
            m.Heard = context.Signals.Broadcast(new Signal(SIGNAL, context.Position, sender, target),
                SignalArea.Within(Radius.Get(context)));
            return TaskStatus.Success;
        }
    }
}

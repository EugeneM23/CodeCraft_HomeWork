using System;

namespace AIKit
{
    /// <summary>
    /// Stands still for a while (a pause at a patrol point, the recovery after a strike). With SecondsMax above
    /// Seconds the pause is random in that range — a pack stops looking like a clock.
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Flow/Wait", "Task", "aikit.task.wait", Success = "Done", Simple = true, NeverFails = true, Order = 30)]
    public sealed class WaitTask : AgentTask
    {
        private sealed class Mem
        {
            public float Left;
        }

        public WaitTask()
        {
        }

        public WaitTask(float seconds, float secondsMax = 0f)
        {
            Seconds = seconds;
            SecondsMax = secondsMax;
        }

        public BlackboardValue<float> Seconds = 1f;

        /// <summary>Above Seconds — the pause is random between the two.</summary>
        public BlackboardValue<float> SecondsMax;

        public float LeftFor(AgentContext context) => Memory<Mem>(context).Left;

        protected override void OnEnter(AgentContext context)
        {
            float seconds = Seconds.Get(context);
            float secondsMax = SecondsMax.Get(context);
            Memory<Mem>(context).Left =
                secondsMax > seconds && context != null ? context.Range(seconds, secondsMax) : seconds;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            Mem m = Memory<Mem>(context);
            m.Left -= dt;
            return m.Left <= 0f ? TaskStatus.Success : TaskStatus.Running;
        }
    }
}

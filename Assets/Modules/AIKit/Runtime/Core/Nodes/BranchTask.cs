using System;

namespace AIKit
{
    /// <summary>
    /// Splits the flow in two by a condition, at once: "True" when it holds, "False" when it does not — the Branch
    /// of a visual script. The condition comes in by a wire into its Condition pin (a Compare, an AND / OR), so one
    /// question is asked in one place and each answer has its own way on (handbook §2.5: a leaf turns a check into a
    /// status). Nothing wired in — the answer is "False": a broken wire gives no hidden answer.
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Flow/Branch", "Task", "aikit.task.branch", Success = "True", Failure = "False", Simple = true)]
    public sealed class BranchTask : AgentTask
    {
        [NonSerialized] public AgentCondition Condition;

        public BranchTask()
        {
        }

        public BranchTask(AgentCondition condition) => Condition = condition;

        public override bool IsInstant => true;

        /// <summary>It only asks: two Branches with one question in a think get one answer.</summary>
        protected internal override bool OnlyAsks => true;

        /// <summary>The answer to the node's question right now.</summary>
        public bool Ask(AgentContext context) => Condition != null && Condition.Check(context);

        protected override TaskStatus OnTick(AgentContext context, float dt) =>
            Ask(context) ? TaskStatus.Success : TaskStatus.Failure;
    }
}

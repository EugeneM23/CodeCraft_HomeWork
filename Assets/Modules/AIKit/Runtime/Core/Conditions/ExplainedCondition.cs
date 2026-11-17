namespace AIKit
{
    /// <summary>
    /// A condition that says in plain words what it asks — for the verdict the Brain writes under a decision whose
    /// Condition pin it is wired into: "only if Target is blocking — no", "only if my attack slot — no data (Target does
    /// not say…)" (handbook §33.1: the trace names the failing condition). Asked only while somebody looks at the
    /// verdicts (it may allocate), with the agent pointed where the question points.
    /// </summary>
    public interface IExplainedCondition
    {
        /// <summary>What it asks, about whom: "my attack slot is mine", "Target is blocking".</summary>
        string Phrase(AgentContext context);

        /// <summary>Why it has no data now ("Target does not say whether it is blocking"); null — nothing to add.</summary>
        string WhyNoData(AgentContext context);
    }
}

namespace AIKit
{
    /// <summary>
    /// The answer of a condition (step 90): yes, no — or no data (the object does not say, nothing is wired in). No data
    /// is never turned into yes (an Is Not of no data is no data), and it vetoes a decision and sends a Branch to False (handbook §4.3: "false =
    /// no data = veto"; §38 №19: no zero passed off as a value).
    /// </summary>
    public enum Truth
    {
        No,
        Yes,
        NoData
    }

    /// <summary>
    /// A yes/no question — the question of a Branch or of a decision's Condition pin (Compare, AND / OR, Attack Slot, Is);
    /// no arrow has a condition (the canvas is the program). Without side effects (invariant 6) and in two levels
    /// (invariant 5): a cheap Gate first (a flag, a squared distance), the expensive answer only when the gate is open.
    /// Inside a think tick every condition is answered once: asked again (two Branches with one question), it gives the
    /// answer it already has — until a block of the flow does its work (AgentContext.AnswerStamp: a Branch in a loop of
    /// instant blocks sees what the loop changed: a Set / Add of the chain itself, run in the same think, is seen by the
    /// next ask; a variable changed from outside the flow is seen next think — one think, one answer, handbook §3.2).
    /// Outside a think tick (tasks, abilities) every call evaluates.
    /// </summary>
    [System.Serializable]
    public abstract class AgentCondition : AgentNode
    {
        [System.NonSerialized] private AgentContext _answeredFor;
        [System.NonSerialized] private int _answeredThink;
        [System.NonSerialized] private Truth _answer;

        /// <summary>
        /// What the condition reads that no wire shows, written on its node in plain words (as AgentTask.Requirement of a
        /// block): "target: Brain's" (Attack Slot). Null — nothing to say: all it reads comes in by its wires.
        /// </summary>
        public virtual string Requirement => null;

        /// <summary>The answer is yes (no and no data both read as "not yes").</summary>
        public bool Check(AgentContext context) => Ask(context) == Truth.Yes;

        /// <summary>
        /// The three-valued answer; a closed gate counts as "no". There is no "not" on a condition (removed 15.11.2026, the
        /// user's word): the opposite is a setting of the condition itself — Is: Is Not, Compare: is off / &lt;, Attack Slot:
        /// Not Mine.
        /// </summary>
        public Truth Ask(AgentContext context)
        {
            int think = context?.AnswerStamp ?? 0;
            if (think != 0 && think == _answeredThink && ReferenceEquals(context, _answeredFor))
                return _answer;

            Truth answer = Gate(context) ? Answer(context) : Truth.No;
            if (think != 0)
            {
                _answeredFor = context;
                _answeredThink = think;
                _answer = answer;
            }

            return answer;
        }

        /// <summary>The cheap level: false — the answer is "no" and the expensive level is not asked.</summary>
        protected virtual bool Gate(AgentContext context) => true;

        /// <summary>The expensive level, asked only when the gate is open.</summary>
        protected abstract bool Evaluate(AgentContext context);

        /// <summary>
        /// The three-valued expensive level: by default Evaluate's yes or no. A condition that can lack data (a value read
        /// from an object that does not say, a wire that is not there) overrides it and answers NoData.
        /// </summary>
        protected virtual Truth Answer(AgentContext context) => Evaluate(context) ? Truth.Yes : Truth.No;
    }
}

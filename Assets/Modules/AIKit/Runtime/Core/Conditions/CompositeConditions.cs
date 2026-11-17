using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>Every child must be true (and).</summary>
    [Serializable]
    [AIKitNode("Graph/Conditions/AND", "Condition", "aikit.condition.all", Simple = true, Order = 20)]
    public sealed class AllCondition : AgentCondition
    {
        public AllCondition()
        {
        }

        public AllCondition(params AgentCondition[] conditions)
        {
            if (conditions != null)
                Conditions.AddRange(conditions);
        }

        [System.NonSerialized] public List<AgentCondition> Conditions = new();

        protected override bool Evaluate(AgentContext context) => Answer(context) == Truth.Yes;

        // A "no" decides; otherwise any child without data leaves the whole without data (step 90); nothing wired in — no
        // data, never a "yes" that holds for nothing
        protected override Truth Answer(AgentContext context)
        {
            if (Conditions.Count == 0)
                return Truth.NoData;

            bool noData = false;
            for (int i = 0; i < Conditions.Count; i++)
            {
                Truth child = Conditions[i] != null ? Conditions[i].Ask(context) : Truth.NoData;
                if (child == Truth.No)
                    return Truth.No;
                noData |= child == Truth.NoData;
            }

            return noData ? Truth.NoData : Truth.Yes;
        }
    }

    /// <summary>At least one child must be true (or).</summary>
    [Serializable]
    [AIKitNode("Graph/Conditions/OR", "Condition", "aikit.condition.any", Simple = true, Order = 30)]
    public sealed class AnyCondition : AgentCondition
    {
        public AnyCondition()
        {
        }

        public AnyCondition(params AgentCondition[] conditions)
        {
            if (conditions != null)
                Conditions.AddRange(conditions);
        }

        [System.NonSerialized] public List<AgentCondition> Conditions = new();

        protected override bool Evaluate(AgentContext context) => Answer(context) == Truth.Yes;

        // A "yes" decides; otherwise any child without data leaves the whole without data (step 90); nothing wired in — no
        // data, never a "yes" that holds for nothing
        protected override Truth Answer(AgentContext context)
        {
            if (Conditions.Count == 0)
                return Truth.NoData;

            bool noData = false;
            for (int i = 0; i < Conditions.Count; i++)
            {
                Truth child = Conditions[i] != null ? Conditions[i].Ask(context) : Truth.NoData;
                if (child == Truth.Yes)
                    return Truth.Yes;
                noData |= child == Truth.NoData;
            }

            return noData ? Truth.NoData : Truth.No;
        }
    }
}

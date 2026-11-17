using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>How a number is compared.</summary>
    public enum NumberComparison
    {
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    /// <summary>
    /// Compares a variable of the blackboard, the way its kind allows: a number — with another number (less, more…),
    /// a flag — is it on or off, an object — is there one or not. One node for every comparison: put a variable in,
    /// pick how to compare. Kind is what the variable is; the editor sets it when the variable is picked.
    /// A value node may come in instead of a variable (step 90): a Get wired into A — the health of the target, my speed
    /// — is compared as a number (Kind is Float then), and one wired into B is what it is compared with (instead of Value
    /// or Other). A value node that has no data now (the object does not say) leaves the comparison without data — not
    /// "no" (handbook §4.3, §38 №19): a decision it is wired into does not hold, a Branch goes to False. So does nothing
    /// wired into A (a wire taken away): no data, which NOT does not turn into "yes".
    /// With Slack a number comparison that holds keeps holding until the number is off by that much more — two
    /// thresholds instead of one (handbook §17: hysteresis).
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Conditions/Compare", "Condition", "aikit.condition.compare", Simple = true, Order = 10)]
    public sealed class CompareCondition : AgentCondition, IExplainedCondition
    {
        /// <summary>The name of the variable (input A: comes in by a wire from the variable's node).</summary>
        [VariableName] public string Variable;

        /// <summary>What the variable is: a number (Float, Int), a flag (Bool) or an object (Transform).</summary>
        public VariableType Kind = VariableType.Float;

        /// <summary>A number: how it is compared with Value.</summary>
        public NumberComparison Comparison = NumberComparison.GreaterOrEqual;

        /// <summary>A number: compared with Value, or — when a variable is wired in here — with that variable.</summary>
        public float Value;

        /// <summary>The number variable compared with instead of Value (empty — Value).</summary>
        [VariableName(VariableType.Float)] public string Other;

        /// <summary>A flag: on (true) or off; an object: there is one (true) or there is none.</summary>
        public bool Expected = true;

        public float Slack;

        /// <summary>A value node wired into input A (a Get): compared, as a number, instead of Variable.</summary>
        [NonSerialized] public AgentValue A;

        /// <summary>A value node wired into input B (a Get): what A is compared with, instead of Value or Other.</summary>
        [NonSerialized] public AgentValue B;

        private sealed class Held
        {
            public bool Value;
        }

        public CompareCondition()
        {
        }

        /// <summary>A number compared with a value.</summary>
        public CompareCondition(string variable, NumberComparison comparison, float value, float slack = 0f)
        {
            Variable = variable;
            Comparison = comparison;
            Value = value;
            Slack = slack;
        }

        /// <summary>A flag that is on (or off), or an object that is there (or not).</summary>
        public CompareCondition(string variable, VariableType kind, bool expected = true)
        {
            Variable = variable;
            Kind = kind;
            Expected = expected;
        }

        /// <summary>A value node (a Get) compared with a number.</summary>
        public CompareCondition(AgentValue a, NumberComparison comparison, float value, float slack = 0f)
        {
            A = a;
            Comparison = comparison;
            Value = value;
            Slack = slack;
        }

        protected override bool Evaluate(AgentContext context) => Answer(context) == Truth.Yes;

        protected override Truth Answer(AgentContext context)
        {
            if (context == null)
                return Truth.No;

            if (A == null && string.IsNullOrEmpty(Variable))
                return Truth.NoData;                    // nothing wired into A: no data (NOT of it is no "yes" either)

            Blackboard board = context.Blackboard;
            if (A == null)
            {
                switch (Kind)
                {
                    case VariableType.Bool:
                        return board.Get(Variable, false) == Expected ? Truth.Yes : Truth.No;
                    case VariableType.Transform:
                        return (board.Get<Transform>(Variable) != null) == Expected ? Truth.Yes : Truth.No;
                }
            }

            // A flag wired into A (step 91: Found of Try Get Component): is it on (Expected) or off
            if (A != null && A.Kind == ValueKind.Flag)
            {
                if (!A.TryGetFlag(context, out bool on))
                    return Truth.NoData;

                return on == Expected ? Truth.Yes : Truth.No;
            }

            float number;
            if (A != null)
            {
                if (!A.TryGetNumber(context, out number))
                    return Truth.NoData;
            }
            else
            {
                number = Kind == VariableType.Int ? board.Get(Variable, 0) : board.Get(Variable, 0f);
            }

            float limit;
            if (B != null)
            {
                if (!B.TryGetNumber(context, out limit))
                    return Truth.NoData;
            }
            else
            {
                limit = string.IsNullOrEmpty(Other) ? Value : board.Get(Other, 0f);
            }

            Held held = Slack > 0f ? Record<Held>(context) : null;
            if (held != null && held.Value)
                limit += Comparison == NumberComparison.Less || Comparison == NumberComparison.LessOrEqual ? Slack : -Slack;

            bool holds = Compare(number, limit, Comparison);
            if (held != null)
                held.Value = holds;

            return holds ? Truth.Yes : Truth.No;
        }

        internal static bool Compare(float value, float threshold, NumberComparison comparison) =>
            comparison switch
            {
                NumberComparison.Less => value < threshold,
                NumberComparison.LessOrEqual => value <= threshold,
                NumberComparison.Greater => value > threshold,
                _ => value >= threshold
            };

        public string Phrase(AgentContext context)
        {
            string a = A is GetValue get ? get.Phrase : A is FoundOutput found ? found.Phrase : A != null ? NodeTitles.Of(A) : Variable;
            if (A == null && Kind == VariableType.Bool || A is { Kind: ValueKind.Flag })
                return $"{a} is {(Expected ? "on" : "off")}";
            if (A == null && Kind == VariableType.Transform)
                return Expected ? $"{a} is there" : $"{a} is empty";

            string b = B is GetValue other ? other.Phrase
                : B != null ? NodeTitles.Of(B)
                : !string.IsNullOrEmpty(Other) ? Other
                : Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            string sign = Comparison switch
            {
                NumberComparison.Less => "<",
                NumberComparison.LessOrEqual => "≤",
                NumberComparison.Greater => ">",
                _ => "≥"
            };
            return $"{a} {sign} {b}";
        }

        public string WhyNoData(AgentContext context)
        {
            if (A == null && string.IsNullOrEmpty(Variable))
                return "nothing wired into A";

            if (A is { Kind: ValueKind.Flag })
                return A.TryGetFlag(context, out _) ? null : A is FoundOutput found ? $"{found.Phrase}: not looked for yet" : "A has no data";

            if (A != null && !A.TryGetNumber(context, out _))
                return A is GetValue get ? $"{get.Phrase}: no data" : "A has no data";

            return B != null && !B.TryGetNumber(context, out _)
                ? B is GetValue other ? $"{other.Phrase}: no data" : "B has no data"
                : null;
        }
    }
}

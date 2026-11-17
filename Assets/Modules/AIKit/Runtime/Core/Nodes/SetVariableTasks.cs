using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A node that changes a number of the blackboard with Value and is done at once: Set, Add, Subtract, Multiply,
    /// Divide. Value itself may be a number or another variable, so one variable can be copied into another or
    /// added to it. A value node wired into its Number pin (a Get: the health of the target — step 90) is used instead
    /// of Value: what it gives now goes into the variable (a snapshot); when it has no data the variable stays as it is.
    /// An Int variable is changed as a number and kept whole (rounded to the nearest); a variable of another kind (a flag,
    /// an object) is no number and is left as it is. The blackboard is how nodes and brains pass data to each other
    /// (handbook §8).
    /// </summary>
    [Serializable]
    public abstract class NumberTask : AgentTask
    {
        /// <summary>The number variable to change.</summary>
        [VariableName(VariableType.Float)] public string Variable;

        public BlackboardValue<float> Value = 1f;

        /// <summary>A value node wired in (a Get): its number now, instead of Value. Nothing wired — Value.</summary>
        [NonSerialized] public AgentValue Number;

        public override bool IsInstant => true;

        /// <summary>The new number, from what the variable holds now and Value.</summary>
        protected abstract double Apply(double now, double value);

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null || string.IsNullOrEmpty(Variable))
                return TaskStatus.Success;

            float value;
            if (Number != null)
            {
                if (!Number.TryGetNumber(context, out value))
                    return TaskStatus.Success;          // no data: the variable is left as it is (never a 0 passed off)
            }
            else
            {
                value = Value.Get(context);
            }

            // Kept in the variable's own number type (15.11.2026: int, float, double — an int rounds to the nearest); a
            // variable of another kind is no number: left as it is; none yet — a float made by the first write
            Blackboard board = context.Blackboard;
            int index = board.IndexOf(Variable);
            if (index < 0)
            {
                board.Set(Variable, (float)Apply(0d, value));
                return TaskStatus.Success;
            }

            if (!board.IsNumberAt(index))
                return TaskStatus.Success;

            board.TryGetNumberAt(index, out double now);
            board.SetNumberAt(index, Apply(now, value));
            return TaskStatus.Success;
        }
    }

    /// <summary>The variable becomes Value.</summary>
    [Serializable]
    [AIKitNode("Graph/Variables/Set", "Task", "aikit.task.set-number", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class SetNumberTask : NumberTask
    {
        protected override double Apply(double now, double value) => value;
    }

    /// <summary>Value is added to the variable: "Attacks + 1".</summary>
    [Serializable]
    [AIKitNode("Graph/Variables/Add", "Task", "aikit.task.add-number", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class AddNumberTask : NumberTask
    {
        public AddNumberTask()
        {
        }

        public AddNumberTask(string variable, float value = 1f)
        {
            Variable = variable;
            Value = value;
        }

        protected override double Apply(double now, double value) => now + value;
    }

    /// <summary>Value is taken away from the variable.</summary>
    [Serializable]
    [AIKitNode("Graph/Variables/Subtract", "Task", "aikit.task.subtract-number", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class SubtractNumberTask : NumberTask
    {
        protected override double Apply(double now, double value) => now - value;
    }

    /// <summary>The variable is multiplied by Value.</summary>
    [Serializable]
    [AIKitNode("Graph/Variables/Multiply", "Task", "aikit.task.multiply-number", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class MultiplyNumberTask : NumberTask
    {
        protected override double Apply(double now, double value) => now * value;
    }

    /// <summary>The variable is divided by Value; by zero — it stays as it is.</summary>
    [Serializable]
    [AIKitNode("Graph/Variables/Divide", "Task", "aikit.task.divide-number", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class DivideNumberTask : NumberTask
    {
        protected override double Apply(double now, double value) => value != 0d ? now / value : now;
    }

    /// <summary>Writes a flag of the blackboard — on or off — and is done at once.</summary>
    [Serializable]
    [AIKitNode("Graph/Variables/Set Flag", "Task", "aikit.task.set-flag", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class SetFlagTask : AgentTask
    {
        /// <summary>The flag variable to write.</summary>
        [VariableName(VariableType.Bool)] public string Variable;

        public bool Value = true;

        public override bool IsInstant => true;

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context != null && !string.IsNullOrEmpty(Variable))
                context.Blackboard.Set(Variable, Value);

            return TaskStatus.Success;
        }
    }
}

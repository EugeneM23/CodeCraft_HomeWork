using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A value node of the tests (step 90): a number of its own, and an object it reads by a wire — what a Get or a What of
    /// the lanes looks like to the canvas. Its assembly is a test one: the canvas never offers it.
    /// </summary>
    [Serializable]
    [AIKitNode("Tests/Number Value", "Value", "aikit.tests.number-value")]
    public sealed class TestNumberValue : AgentValue
    {
        public float Number = 2f;

        /// <summary>The object it reads (by a wire from a node of the variable).</summary>
        [VariableName(VariableType.Transform)] public string Of;

        public override ValueKind Kind => ValueKind.Number;

        public override bool TryGetNumber(AgentContext context, out float value)
        {
            value = Number;
            return true;
        }
    }

    /// <summary>
    /// A block of the tests that reads values: one into a single pin, any number into a list pin; it says what it requires
    /// on its node (AgentTask.Requirement). Its assembly is a test one: the canvas never offers it.
    /// </summary>
    [Serializable]
    [AIKitNode("Tests/Value Reader", "Task", "aikit.tests.value-reader", Success = "Done")]
    public sealed class TestValueReaderTask : AgentTask
    {
        public const string REQUIRES = "only with a number wired into Amount";

        /// <summary>One value, by a wire into its pin.</summary>
        [NonSerialized] public AgentValue Amount;

        /// <summary>Any number of values, by wires into one pin.</summary>
        [NonSerialized] public List<TestNumberValue> Numbers = new();

        public override string Requirement => REQUIRES;

        protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Success;
    }
}

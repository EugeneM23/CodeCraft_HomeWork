using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Writes a line to the console and finishes at once: a mark on the way through the graph ("got here"). The line
    /// starts with the agent's name; a click on it in the console selects the agent. A variable of any kind wired into
    /// its Value pin is written after the message as text — "Grenade Timer = 2.4", "Target = Player".
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Debug/Debug Log", "Task", "aikit.task.debug-log", Success = "Done", Simple = true, NeverFails = true)]
    public sealed class DebugLogTask : AgentTask
    {
        public override bool IsInstant => true;

        [FreeText] public string Message = "Hello";

        /// <summary>The variable written after the message (any kind; by a wire into the Value pin). Empty — none.</summary>
        [VariableName, HideInInspector] public string Value;

        /// <summary>A value node wired into its Number pin (a Get, step 90): written after the message — "Health of Target = 3".</summary>
        [NonSerialized] public AgentValue Number;

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            string line = string.IsNullOrEmpty(Value) ? Message : $"{Message} {Value} = {TextOf(context, Value)}";
            if (Number != null)
            {
                string what = Number is GetValue get ? get.Phrase : NodeTitles.Of(Number);
                string number = Number.TryGetNumber(context, out float value)
                    ? value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    : "no data";
                line = $"{line} {what} = {number}";
            }

            Transform self = context?.Self;
            Debug.Log(self != null ? $"[{self.name}] {line}" : line, self);
            return TaskStatus.Success;
        }

        // The value of a variable as text, whatever its kind; "(none)" — it is not on the blackboard (yet)
        private static string TextOf(AgentContext context, string variable)
        {
            Blackboard board = context?.Blackboard;
            int index = board != null ? board.IndexOf(variable) : -1;
            if (index < 0 || !board.HasValueAt(index))
                return "(none)";

            return board.BoxedValueOf(index) switch
            {
                null => "(nothing)",
                UnityEngine.Object found when found == null => "(nothing)",
                UnityEngine.Object found => found.name,
                float number => number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                bool flag => flag ? "yes" : "no",
                object other => other.ToString()
            };
        }
    }
}

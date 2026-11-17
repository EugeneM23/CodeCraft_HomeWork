using System;
using System.Globalization;

namespace AIKit.Editor
{
    /// <summary>
    /// What a condition of a Branch says in plain words: "Ammo is at least 3", "Alarm is on", "there is no Player". The
    /// canvas shows a condition as this sentence instead of a form of fields — it answers "which way does the flow go?"
    /// at a glance. A condition without a phrase of its own (AND / OR) reads by its name.
    /// </summary>
    public static class ArrowPhrases
    {
        /// <summary>How a variable with no object picked yet reads in a sentence.</summary>
        public const string NOBODY = "something";

        /// <summary>A number comparison in the words of a count, in the order of NumberComparison.</summary>
        public static readonly string[] CountWords = { "is less than", "is at most", "is more than", "is at least" };

        /// <summary>One condition in plain words: "Ammo is at least 3", "there is no Player".</summary>
        public static string Phrase(AgentCondition condition, string nodeTarget)
        {
            switch (condition)
            {
                case null:
                    return "";
                case CompareCondition compare:
                    if (compare.Kind == VariableType.Bool)
                        return $"{Name(compare.Variable)} is {(compare.Expected ? "on" : "off")}";
                    if (compare.Kind == VariableType.Transform)
                        return $"there is {(compare.Expected ? "" : "no ")}{Name(compare.Variable)}";

                    int how = Math.Clamp((int)compare.Comparison, 0, CountWords.Length - 1);
                    return $"{Name(compare.Variable)} {CountWords[how]} {(string.IsNullOrEmpty(compare.Other) ? Number(compare.Value) : compare.Other)}";
                default:
                    return Title(condition).ToLowerInvariant();
            }
        }

        /// <summary>A number the short way: 5, 2.5 — or the name of the variable it is read from.</summary>
        public static string Number(BlackboardValue<float> value) =>
            value.UsesVariable ? value.Name : Number(value.Get(null));

        public static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>The condition's name as the list of nodes shows it: its type title ("Compare", "AND").</summary>
        public static string Title(AgentNode node) => NodeTitles.Of(node);

        private static string Name(string variable) => string.IsNullOrEmpty(variable) ? NOBODY : variable;
    }
}

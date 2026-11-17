using System;

namespace AIKit
{
    /// <summary>
    /// A utility score as a type, not a bare float (handbook §4.2): never negative and never NaN (clamped in one
    /// place), reads like a number in arithmetic, prints the same way in every trace.
    /// </summary>
    public readonly struct UtilityScore : IComparable<UtilityScore>, IEquatable<UtilityScore>
    {
        public readonly float Value;

        public UtilityScore(float value) => Value = float.IsNaN(value) || value < 0f ? 0f : value;

        public static UtilityScore Zero => default;

        public static UtilityScore One => new(1f);

        public static implicit operator UtilityScore(float value) => new(value);

        public static implicit operator float(UtilityScore score) => score.Value;

        public static UtilityScore operator *(UtilityScore a, UtilityScore b) => new(a.Value * b.Value);

        public static UtilityScore operator +(UtilityScore a, UtilityScore b) => new(a.Value + b.Value);

        public int CompareTo(UtilityScore other) => Value.CompareTo(other.Value);

        public bool Equals(UtilityScore other) => Value.Equals(other.Value);

        public override bool Equals(object obj) => obj is UtilityScore other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString("0.000");
    }

    /// <summary>How the axes of a decision are combined.</summary>
    public enum ScoreReduction
    {
        /// <summary>Every axis matters: the product, compensated for the number of axes (geometric mean).</summary>
        Product,

        /// <summary>Arguments add up: the average of the axes.</summary>
        Average
    }
}

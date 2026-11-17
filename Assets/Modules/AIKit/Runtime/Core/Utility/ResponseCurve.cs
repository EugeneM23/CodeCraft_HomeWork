using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>Shape of a response curve (the families of the Infinite Axis Utility System).</summary>
    public enum CurveType
    {
        Linear,
        Polynomial,
        Logistic,
        Logit,
        Normal,
        Sine,

        /// <summary>m / (1 + k·(x − b)) + c: falls fast, then slowly (threat by distance).</summary>
        Hyperbolic,

        /// <summary>Always c: a plain weight.</summary>
        Const,

        /// <summary>The designer's own shape: an AnimationCurve over 0..1.</summary>
        Custom
    }

    /// <summary>
    /// Turns a normalized input (0..1) into a normalized score (0..1): the knob a designer tunes instead of writing
    /// code. M — slope, K — exponent/steepness/width, B — horizontal shift, C — vertical shift. Every family reads all
    /// four (a knob that does nothing is a bug: K once did nothing for Logit and Sine), and the result is always a number
    /// in 0..1 — never NaN (a Polynomial below B with a fractional K used to give NaN, and the choice silently fell
    /// back to "the first candidate").
    /// </summary>
    [Serializable]
    public struct ResponseCurve
    {
        public CurveType Type;
        public float M;
        public float K;
        public float B;
        public float C;

        [Tooltip("Custom only: the shape over 0..1")]
        public AnimationCurve Shape;

        public float Evaluate(float x)
        {
            x = Mathf.Clamp01(x);
            float y = Type switch
            {
                CurveType.Linear => M * (x - B) + C,
                CurveType.Polynomial => M * Mathf.Pow(Mathf.Max(0f, x - B), K) + C,
                CurveType.Logistic => M / (1f + Mathf.Exp(-K * 10f * (x - 0.5f - B))) + C,
                CurveType.Logit => M * (Logit(x - B) / (10f * NonZero(K)) + 0.5f) + C,
                CurveType.Normal => M * Mathf.Exp(-K * 10f * (x - B) * (x - B)) + C,
                CurveType.Sine => M * Mathf.Sin(Mathf.PI * NonZero(K) * (x - B)) + C,
                CurveType.Hyperbolic => M / (1f + NonZero(K) * 10f * Mathf.Max(0f, x - B)) + C,
                CurveType.Const => C,
                CurveType.Custom => Shape != null ? Shape.Evaluate(x) : x,
                _ => x
            };

            return float.IsNaN(y) ? 0f : Mathf.Clamp01(y);
        }

        /// <summary>ln(p / (1 − p)) with p kept inside (0, 1): the inverse of the logistic switch.</summary>
        private static float Logit(float p)
        {
            p = Mathf.Clamp(p, 1e-4f, 1f - 1e-4f);
            return Mathf.Log(p / (1f - p));
        }

        /// <summary>A K of 0 (a default-constructed curve) reads as 1 where K divides or scales a period.</summary>
        private static float NonZero(float k) => Mathf.Approximately(k, 0f) ? 1f : k;

        /// <summary>Straight up: 0 → 0, 1 → 1.</summary>
        public static ResponseCurve Linear() => new() { Type = CurveType.Linear, M = 1f, K = 1f, B = 0f, C = 0f };

        /// <summary>Straight down: 0 → 1, 1 → 0 (closer is better, lower health is more urgent).</summary>
        public static ResponseCurve Inverse() => new() { Type = CurveType.Linear, M = -1f, K = 1f, B = 0f, C = 1f };

        /// <summary>A soft switch around B (0..1); K — steepness.</summary>
        public static ResponseCurve Logistic(float b = 0f, float k = 1f) =>
            new() { Type = CurveType.Logistic, M = 1f, K = k, B = b, C = 0f };

        /// <summary>A bell around B: "good at this distance, worse on both sides".</summary>
        public static ResponseCurve Band(float b = 0.5f, float k = 1f) =>
            new() { Type = CurveType.Normal, M = 1f, K = k, B = b, C = 0f };

        public static ResponseCurve Hyperbolic(float k = 1f) =>
            new() { Type = CurveType.Hyperbolic, M = 1f, K = k, B = 0f, C = 0f };

        public static ResponseCurve Constant(float value) => new() { Type = CurveType.Const, C = value };

        public static ResponseCurve Custom(AnimationCurve shape) => new() { Type = CurveType.Custom, Shape = shape };

        public override string ToString() => $"{Type}(m={M}, k={K}, b={B}, c={C})";
    }
}

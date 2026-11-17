using UnityEngine;

namespace AIKit
{
    /// <summary>Scoring math shared by ability choice, position choice and role assignment.</summary>
    public static class UtilityMath
    {
        /// <summary>
        /// Multiplying many 0..1 scores drags the result down (ten axes of 0.9 give 0.35), which makes a long list of
        /// considerations useless. The compensation is the geometric mean, product^(1/n) (handbook §4.4): ten axes of
        /// 0.9 give 0.9 back. A zero still vetoes the option.
        /// </summary>
        public static float Compensate(float score, int count)
        {
            if (count <= 1 || score <= 0f)
                return Mathf.Clamp01(score);

            return Mathf.Clamp01(Mathf.Pow(score, 1f / count));
        }

        /// <summary>
        /// Scores a decision (handbook §4.4). A required axis without data vetoes the whole decision (false, and the
        /// axis is reported in veto); an optional axis without data is skipped; a zero from any axis is a score of zero,
        /// not a veto — the two read differently in the trace. Axes combine by the reduction; the weight multiplies.
        /// </summary>
        public static bool Evaluate(Consideration[] axes, AgentContext context, ScoreReduction reduction,
            out UtilityScore score, out Consideration veto)
        {
            veto = null;
            score = UtilityScore.One;
            if (axes == null || axes.Length == 0)
                return true;

            float product = 1f;
            float sum = 0f;
            int counted = 0;
            for (int i = 0; i < axes.Length; i++)
            {
                Consideration axis = axes[i];
                if (axis == null)
                    continue;

                if (!axis.Evaluate(context, out UtilityScore value))
                {
                    if (!axis.Required)
                        continue;           // optional and no data: it simply has no say

                    veto = axis;
                    score = UtilityScore.Zero;
                    return false;
                }

                product *= value;
                sum += value;
                counted++;
            }

            if (counted == 0)
                return true;

            score = reduction == ScoreReduction.Average ? sum / counted : Compensate(product, counted);
            return true;
        }

        /// <summary>The score alone (product, compensated); a veto reads as 0 here — use Evaluate to tell them apart.</summary>
        public static float Score(Consideration[] considerations, AgentContext context) =>
            Evaluate(considerations, context, ScoreReduction.Product, out UtilityScore score, out _) ? score : 0f;

        /// <summary>Maps a raw value into 0..1 between min and max (min above max inverts the axis).</summary>
        public static float Normalize(float value, float min, float max)
        {
            if (Mathf.Approximately(min, max))
                return value >= max ? 1f : 0f;

            return Mathf.Clamp01((value - min) / (max - min));
        }
    }
}

using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>What a consideration looks at.</summary>
    public enum ConsiderationInput
    {
        /// <summary>Distance to the current target.</summary>
        DistanceToTarget,

        /// <summary>A blackboard float (health fraction, ammo, threat — whatever the game writes there).</summary>
        Variable,

        /// <summary>A fixed number: a plain weight on the axis.</summary>
        Constant,

        /// <summary>Fresh random 0..1 each evaluation — a pinch of unpredictability.</summary>
        Random,

        /// <summary>How aware the agent is of its target, 0..1 (perception; without it — 1 with a target).</summary>
        Awareness,

        /// <summary>Seconds since the target was last seen (0 — in sight now).</summary>
        TimeSinceSeen
    }

    /// <summary>
    /// One scoring axis: takes a fact of the world, normalizes it to 0..1 and bends it with a response curve.
    /// An axis that has no data (no target for a distance, a variable that does not exist, RequiresTarget without a
    /// target) says so — Evaluate returns false — and a required axis without data vetoes the decision: "not
    /// considered", which the trace tells apart from "scored zero" (handbook §4.3).
    /// </summary>
    [Serializable]
    public sealed class Consideration
    {
        [Tooltip("For reading in the trace: \"too far\", \"low health\"")]
        public string Name;

        public ConsiderationInput Input = ConsiderationInput.DistanceToTarget;

        [Tooltip("Variable input: where the number lives")]
        public BlackboardScope Scope;

        [Tooltip("Variable input: the name of the float")]
        [VariableName] public string Variable;

        [Tooltip("Constant input: the raw value")]
        public float Constant;

        [Tooltip("Raw value mapped to 0")]
        public float InputMin;

        [Tooltip("Raw value mapped to 1")]
        public float InputMax = 1f;

        public ResponseCurve Curve = ResponseCurve.Linear();

        [Tooltip("On — no data on this axis vetoes the whole decision; off — the axis is simply skipped")]
        public bool Required = true;

        [Tooltip("Decorator: 1 − the curve's output (the same curve read the other way)")]
        public bool InvertOutput;

        [Tooltip("Decorator: the output multiplied by this (then kept within 0..1)")]
        [Min(0f)]
        public float Multiply = 1f;

        [Tooltip("The axis has no data while the agent has no target (whatever its input)")]
        public bool RequiresTarget;

        [NonSerialized] private BlackboardVariable<float> _variable;

        /// <summary>The axis score; false — no data (a required axis then vetoes the decision).</summary>
        public bool Evaluate(AgentContext context, out UtilityScore score)
        {
            score = UtilityScore.Zero;
            if (!TryGetRaw(context, out float raw))
                return false;

            float value = Curve.Evaluate(UtilityMath.Normalize(raw, InputMin, InputMax));
            if (InvertOutput)
                value = 1f - value;
            score = Mathf.Clamp01(value * Multiply);
            return true;
        }

        /// <summary>The axis score, with "no data" read as 0 (tests, quick looks; decisions use the other overload).</summary>
        public float Evaluate(AgentContext context) => Evaluate(context, out UtilityScore score) ? score : 0f;

        private bool TryGetRaw(AgentContext context, out float raw)
        {
            raw = 0f;
            if (RequiresTarget && (context == null || !context.HasTarget))
                return false;

            switch (Input)
            {
                case ConsiderationInput.DistanceToTarget:
                    if (context == null || !context.HasTarget)
                        return false;       // no target, no distance — not "very far"

                    raw = context.DistanceToTarget;
                    return true;

                case ConsiderationInput.Variable:
                    return context != null && _variable.TryGet(context.BlackboardOf(Scope), Variable, out raw);

                case ConsiderationInput.Random:
                    raw = (float)(context?.NextRandom() ?? 0.5);
                    return true;

                case ConsiderationInput.Awareness:
                    if (context == null)
                        return false;

                    raw = context.Track.Awareness;
                    return true;

                case ConsiderationInput.TimeSinceSeen:
                    if (context == null || (!context.HasTarget && context.Track.Awareness <= 0f))
                        return false;           // never seen: no time since — not "a long time"

                    raw = context.HasTarget ? 0f : context.Track.TimeSinceSeen;
                    return true;

                default:
                    raw = Constant;
                    return true;
            }
        }

        public override string ToString() =>
            string.IsNullOrEmpty(Name) ? $"{Input}:{Curve.Type}" : Name;
    }
}

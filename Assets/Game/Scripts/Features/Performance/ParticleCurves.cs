using UnityEngine;

namespace Game
{
    /// <summary>
    /// Upper bounds of particle-system values that may be a constant, a random range or a curve.
    /// </summary>
    /// <remarks>
    /// Callers use these to budget or time particle systems, where under-estimating cuts visible
    /// particles short, so every answer errs on the high side.
    /// </remarks>
    internal static class ParticleCurves
    {
        // Tangents can carry a curve above its highest key; sampling the keyed span catches that.
        private const int SAMPLES = 16;

        /// <summary>The largest value <paramref name="value"/> can produce, whatever its mode.</summary>
        public static float Max(ParticleSystem.MinMaxCurve value)
        {
            switch (value.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return value.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(value.constantMin, value.constantMax);
                case ParticleSystemCurveMode.Curve:
                    return value.curveMultiplier * Peak(value.curve);
                default:
                    return value.curveMultiplier * Mathf.Max(Peak(value.curveMin), Peak(value.curveMax));
            }
        }

        /// <summary>The highest value of <paramref name="curve"/> over its keyed span; 0 for no curve.</summary>
        public static float Peak(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0)
                return 0f;

            float peak = curve[0].value;
            for (int i = 1; i < curve.length; i++)
                peak = Mathf.Max(peak, curve[i].value);

            float start = curve[0].time;
            float end = curve[curve.length - 1].time;
            for (int s = 1; s < SAMPLES; s++)
                peak = Mathf.Max(peak, curve.Evaluate(Mathf.Lerp(start, end, s / (float)SAMPLES)));

            return peak;
        }
    }
}

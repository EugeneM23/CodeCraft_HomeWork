using UnityEngine;

namespace Game
{
    /// <summary>
    /// Light intensities the game's effects were tuned with under the Built-in pipeline, turned into
    /// what URP needs for the same brightness.
    /// </summary>
    /// <remarks>
    /// Built-in here ran in linear colour space without linear light intensity: it lit with
    /// (colour × intensity) taken from gamma to linear, so an intensity of 10 burned about 200 times
    /// as bright as 1. URP always uses linear intensity (colour taken to linear, then × intensity).
    /// Taking the intensity itself from gamma to linear gives back the Built-in brightness at the
    /// light; colour channels below one differ by a few percent. URP's falloff is inverse-square,
    /// Built-in's was its own curve, so the same light still reaches less far from its centre.
    /// The sun (intensity 1) is the same either way.
    /// </remarks>
    public static class LightIntensity
    {
        public static float FromBuiltIn(float builtInIntensity) =>
            builtInIntensity <= 0f ? 0f : Mathf.GammaToLinearSpace(builtInIntensity);
    }
}

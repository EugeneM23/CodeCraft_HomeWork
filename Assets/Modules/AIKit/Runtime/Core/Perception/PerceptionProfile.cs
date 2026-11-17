using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The senses of a type of agent (handbook §9.2), an asset the graph's Senses sensor sees with (step 90, C21: the
    /// senses are the sensor's, not a section of the character): where it looks — a focus cone ahead, a wider
    /// periphery, a short range behind — how fast attention grows in each and how fast it fades, how long a lost target
    /// is remembered, the thresholds of the alert ladder and how often it looks. Angles are from where the body looks in
    /// its own frame (any gravity). Defaults: a fresh target in full view is noticed (0.3) in about 0.15 s and fully
    /// known (0.8) in about 0.4 s; a lost one fades from full to nothing in two seconds. One asset is shared by every
    /// agent that sees with it: it holds no agent's state.
    /// </summary>
    [CreateAssetMenu(fileName = "Perception Profile", menuName = "AI Kit/Perception Profile")]
    public sealed class PerceptionProfile : ScriptableObject
    {
        [Header("Where it looks")]
        [Tooltip("Eye height above the feet, along the body's up")]
        [Min(0f)] public float EyeHeight = 1.5f;

        [Tooltip("Focus: straight ahead, far")]
        [Min(0f)] public float FocusRange = 12f;

        [Tooltip("Half-angle of the focus cone, degrees from the facing (90 — the whole front half)")]
        [Range(0f, 180f)] public float FocusHalfAngle = 45f;

        [Tooltip("Periphery: wider, nearer, slower to notice")]
        [Min(0f)] public float PeripheryRange = 8f;

        [Range(0f, 180f)] public float PeripheryHalfAngle = 100f;

        [Tooltip("Behind: any angle, short")]
        [Min(0f)] public float BackRange = 2.5f;

        [Tooltip("A target already known is kept up to this distance whichever way the agent faces (hysteresis: noticing " +
                 "and losing are different thresholds)")]
        [Min(0f)] public float LoseRange = 16f;

        [Header("How fast")]
        [Tooltip("Awareness gained per second in the focus (the periphery gains half, the back a quarter)")]
        [Min(0f)] public float GainPerSecond = 2f;

        [Tooltip("Awareness lost per second while the target is not seen")]
        [Min(0f)] public float DecayPerSecond = 0.5f;

        [Tooltip("How long the place a target was last seen stays worth searching, seconds")]
        [Min(0f)] public float MemoryTime = 4f;

        [Header("Alert ladder")]
        [Tooltip("From here the agent is suspicious (it noticed something)")]
        [Range(0f, 1f)] public float NoticeThreshold = 0.3f;

        [Tooltip("From here the agent knows: it fights")]
        [Range(0f, 1f)] public float AwareThreshold = 0.8f;

        [Header("How often")]
        [Tooltip("Seconds between looks, picked anew from [min, max] with the agent's random each time (5–10 Hz by " +
                 "default); 0 — every step")]
        [Min(0f)] public float VisionIntervalMin = 0.1f;

        [Min(0f)] public float VisionIntervalMax = 0.2f;

        /// <summary>
        /// How far a target is looked for at all (the scan radius): the largest of the ranges. Pairs, not Mathf.Max of four
        /// (its params array was made at every look).
        /// </summary>
        public float ScanRadius => Mathf.Max(Mathf.Max(FocusRange, PeripheryRange), Mathf.Max(BackRange, LoseRange));

        private static PerceptionProfile _defaults;

        /// <summary>The profile of a Senses sensor that names none: every number at its default (never saved).</summary>
        public static PerceptionProfile Defaults
        {
            get
            {
                if (_defaults == null)
                    _defaults = Create();
                return _defaults;
            }
        }

        /// <summary>A profile made in code (a spawner's own sight, a test), set as it says; never saved.</summary>
        public static PerceptionProfile Create(Action<PerceptionProfile> set = null)
        {
            var profile = CreateInstance<PerceptionProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            set?.Invoke(profile);
            return profile;
        }

        /// <summary>A copy made in code (never saved): the same numbers, changeable without touching this one.</summary>
        public PerceptionProfile Clone()
        {
            PerceptionProfile copy = Instantiate(this);
            copy.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }
    }
}

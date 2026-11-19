using UnityEngine;

namespace Game
{
    /// <summary>
    /// Stand-in sounds for the armour lab, made in code until the sound team gives real ones: a ricochet's rising
    /// ping, a non-penetration's dull clang, a penetration's short crack.
    /// </summary>
    public static class ArmourLabSounds
    {
        private const int RATE = 44100;

        private static AudioClip s_ricochet;
        private static AudioClip s_clang;
        private static AudioClip s_crack;

        public static void Play(ArmourOutcome outcome, Vector3 at)
        {
            AudioClip clip = outcome switch
            {
                ArmourOutcome.Ricochet => s_ricochet ??= Make("ArmourRicochet", 0.45f, 2400f, 4200f, 7f, 0.05f),
                ArmourOutcome.NoPenetration => s_clang ??= Make("ArmourClang", 0.35f, 520f, 380f, 11f, 0.35f),
                _ => s_crack ??= Make("ArmourCrack", 0.25f, 180f, 90f, 18f, 0.8f)
            };
            AudioSource.PlayClipAtPoint(clip, at, 0.8f);
        }

        /// <summary>A tone gliding from one pitch to another, dying away, with some noise mixed in.</summary>
        private static AudioClip Make(string name, float seconds, float from, float to, float decay, float noise)
        {
            int count = (int)(RATE * seconds);
            var data = new float[count];
            var random = new System.Random(name.GetHashCode());
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)RATE;
                float k = t / seconds;
                phase += 2.0 * Mathf.PI * Mathf.Lerp(from, to, k) / RATE;
                float tone = Mathf.Sin((float)phase) + 0.35f * Mathf.Sin((float)(phase * 2.71));
                float hiss = (float)(random.NextDouble() * 2.0 - 1.0);
                float envelope = Mathf.Exp(-decay * t) * Mathf.Min(1f, i / 80f);
                data[i] = envelope * ((1f - noise) * tone * 0.6f + noise * hiss);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

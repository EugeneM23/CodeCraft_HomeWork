using UnityEngine;

namespace AIKit
{
    public sealed partial class AgentContext
    {
        /// <summary>
        /// The blows that landed on this character (step 90, reactions): who struck last, when, from where, and how hard
        /// it has been hit lately. Written by the character's hit receiver only (AICharacter.TakeHit); the reaction
        /// decisions read it (Got Hit, the pressure that raises Target Attacks' chance) and the Hit By shape of
        /// Detection sees the last attacker.
        /// </summary>
        public HitMemory Hits { get; } = new();
    }

    /// <summary>
    /// What a character remembers of the blows that landed on it (handbook §8.2: "who hit me last"; §19.2: recent hits
    /// raise the chance to defend — a counter that halves every HALF_LIFE seconds, so a flurry of blows meets the guard
    /// instead of an endless flinch). Times are the character's own clock (AgentContext.LocalTime).
    /// </summary>
    public sealed class HitMemory
    {
        /// <summary>Seconds in which the pressure of the past hits halves (handbook §19.2: ~2 s).</summary>
        public const float HALF_LIFE = 2f;

        private float _pressure;
        private float _pressureAt;

        /// <summary>How many blows have landed on the character so far: a new hit is a new number (an event caught by its edge).</summary>
        public int Count { get; private set; }

        /// <summary>Who struck last (null — nobody yet, or a blow of nobody: a trap, a fall).</summary>
        public Transform LastAttacker { get; private set; }

        /// <summary>When the last blow landed, on the character's clock; negative infinity — never.</summary>
        public float LastAt { get; private set; } = float.NegativeInfinity;

        /// <summary>Where the last blow came from: the attacker's position when it landed.</summary>
        public Vector3 LastFrom { get; private set; }

        /// <summary>How hard the last blow was.</summary>
        public float LastDamage { get; private set; }

        /// <summary>The last blow came from behind the character: from the back half of where it faced.</summary>
        public bool LastFromBehind { get; private set; }

        /// <summary>Some blow has landed on the character.</summary>
        public bool Any => Count > 0;

        /// <summary>Seconds since the last blow at this moment of the character's clock (infinity — never hit).</summary>
        public float Since(float now) => now - LastAt;

        /// <summary>
        /// How hard the character has been hit lately at this moment of its clock: every blow adds 1, and the sum halves
        /// every HALF_LIFE seconds. 0 — not hit lately.
        /// </summary>
        public float PressureAt(float now)
        {
            if (_pressure <= 0f)
                return 0f;

            float passed = Mathf.Max(0f, now - _pressureAt);
            return _pressure * Mathf.Pow(0.5f, passed / HALF_LIFE);
        }

        /// <summary>A blow landed now (the character's clock): it is the last one, and it adds to the pressure.</summary>
        public void Record(Transform attacker, Vector3 from, float damage, bool fromBehind, float now)
        {
            _pressure = PressureAt(now) + 1f;
            _pressureAt = now;
            Count++;
            LastAttacker = attacker;
            LastAt = now;
            LastFrom = from;
            LastDamage = damage;
            LastFromBehind = fromBehind;
        }

        /// <summary>Forgets every blow (a character that comes back to life, a test).</summary>
        public void Clear()
        {
            _pressure = 0f;
            _pressureAt = 0f;
            Count = 0;
            LastAttacker = null;
            LastAt = float.NegativeInfinity;
            LastFrom = Vector3.zero;
            LastDamage = 0f;
            LastFromBehind = false;
        }
    }
}

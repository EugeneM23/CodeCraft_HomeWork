using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Where the target is around the agent, by the angle from where the agent looks: in front (within 45°), at the side
    /// (45–135°), behind (beyond 135°).
    /// </summary>
    [Flags]
    public enum AbilityAngles
    {
        None = 0,
        Front = 1,
        Side = 2,
        Back = 4,
        All = Front | Side | Back
    }

    /// <summary>A distance band: an ability is used from Min to Max away from the target.</summary>
    [Serializable]
    public struct RangeBand
    {
        [Min(0f)] public float Min;
        [Min(0f)] public float Max;

        public RangeBand(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public bool Contains(float distance) => distance >= Min && distance <= Max;

        public override string ToString() => $"{Min:0.##}..{Max:0.##}";
    }

    /// <summary>
    /// An ability described by data, not by code: when it is allowed, how good it is right now and how it is paced.
    /// The runner (IAbilityRunner) only knows how to play it — the choice is made here, so a new attack is a line in
    /// the inspector, not a new state in the graph.
    /// This is the final shape an ability asset serializes. The distances belong to the role profile
    /// that uses the ability (a brute and a skirmisher swing the same sword from different bands), not to the
    /// character. The phase and token fields are data now; their behavior arrives with stage 8.
    /// </summary>
    [Serializable]
    public sealed class AbilityDefinition
    {
        [Tooltip("Matches the runner and the animation clip id")]
        public string Id;

        [Tooltip("Base weight among the options that pass their conditions")]
        [Min(0f)]
        public float Weight = 1f;

        [Tooltip("Pause after the ability ENDS before it may be used again")]
        [Min(0f)]
        public float Cooldown = 1f;

        [Tooltip("Not available for this long after the agent wakes up: keeps a pack from telegraphing in unison")]
        [Min(0f)]
        public float StartDelay;

        [Tooltip("From how close to how far away the ability is used")]
        public RangeBand Range = new(0f, 2f);

        [Tooltip("Only from the ground")]
        public bool RequiresGround = true;

        [Tooltip("Only with a target")]
        public bool RequiresTarget = true;

        [Tooltip("Only with a clear line to the target (the entity's sight probe; none — always clear)")]
        public bool RequiresLineOfSight;

        [Tooltip("Only when the agent is at least this aware of its target (0 — any awareness)")]
        [Range(0f, 1f)]
        public float MinAwareness;

        [Tooltip("How much the score drops when this ability was the last one used (0 — no anti-repeat)")]
        [Range(0f, 1f)]
        public float RepeatPenalty = 0.5f;

        [Tooltip("Scoring axes; empty — the weight alone decides")]
        public Consideration[] Considerations = Array.Empty<Consideration>();

        [Tooltip("How the axes combine: the compensated product (every axis matters) or the average")]
        public ScoreReduction Reduction = ScoreReduction.Product;

        [Header("Phases and tokens (behavior — stage 8)")]
        [Tooltip("Telegraph before the strike lands, seconds")]
        [Min(0f)]
        public float WindUp;

        [Tooltip("Vulnerable after the strike, seconds")]
        [Min(0f)]
        public float Recovery;

        [Tooltip("The pool of the target's attack tokens the ability needs (\"Melee\", \"Ranged\"); empty — none")]
        public string TokenPool;

        [Tooltip("Attack tokens the ability takes from the target's pool")]
        [Min(0)]
        public int TokenCost = 1;

        [Tooltip("How many tokens the ability's pool has on one target, whatever the target says; 0 — the target's say")]
        [Min(0)]
        public int TokenCapacity;

        [Tooltip("Usable only while the agent holds its place to attack the target — the one the node " +
                 "Take Attack Slot gives, of whatever group of places")]
        public bool RequiresPlace;

        [Tooltip("Where the target may be around the agent: in front, at the side, behind")]
        public AbilityAngles Angles = AbilityAngles.All;

        /// <summary>The near edge of Range (code and object initializers; the data is Range).</summary>
        public float MinRange
        {
            get => Range.Min;
            set => Range.Min = value;
        }

        /// <summary>The far edge of Range.</summary>
        public float MaxRange
        {
            get => Range.Max;
            set => Range.Max = value;
        }

        public bool InRange(float distance) => Range.Contains(distance);

        /// <summary>A copy for one agent (a shared ability asset hands every agent its own): the axes array is copied too.</summary>
        public AbilityDefinition Clone()
        {
            var copy = (AbilityDefinition)MemberwiseClone();
            copy.Considerations = Considerations != null
                ? (Consideration[])Considerations.Clone()
                : Array.Empty<Consideration>();
            return copy;
        }

        public override string ToString() => string.IsNullOrEmpty(Id) ? "<ability>" : Id;
    }
}

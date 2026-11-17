using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Hit By, a sensor of Detection: it "sees" whoever struck this character last — for Remember seconds after the blow,
    /// if the attacker is of Detection's kind (its tag) and no farther than Within (handbook §9.3: damage always wakes —
    /// a blow in the back is not ignored because nobody was seen). So being hit is visible where it is seen: wired into
    /// Detection, it is one of the ways this character sees; without it a blow from somebody unseen does not make him a
    /// target. Seen by another sensor meanwhile, the attacker is held by the usual rule; not seen once the blow is old —
    /// lost. What the blow does to the decisions is the Got Hit decision's.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Hit By", "Sensor", "aikit.sensor.hit-by")]
    public sealed class HitBySensorTask : SensorTask
    {
        /// <summary>Seconds the attacker is seen for after its blow.</summary>
        public BlackboardValue<float> Remember = 5f;

        /// <summary>How far from this character the attacker may be, metres (a shot comes from afar).</summary>
        public BlackboardValue<float> Within = 30f;

        protected internal override float Reach(AgentContext context) => Mathf.Max(0f, Within.Get(context));

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            if (context == null || candidate == null)
                return false;

            HitMemory hits = context.Hits;
            Transform attacker = hits.LastAttacker;
            if (attacker == null || hits.Since(context.LocalTime) > Remember.Get(context))
                return false;

            if (attacker != candidate && !attacker.IsChildOf(candidate))
                return false;

            float within = Reach(context);              // another shape of Detection may gather from farther
            return (candidate.position - context.Position).sqrMagnitude <= within * within;
        }
    }
}

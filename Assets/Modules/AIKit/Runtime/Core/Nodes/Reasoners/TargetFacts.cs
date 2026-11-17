using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What a reasoner may ask about the Brain's Target in this think: is there one, how far it is, how long it has been
    /// seen. Computed by the Brain once per think and handed to every reasoner (handbook §3.2: world queries cached for
    /// one think). Nothing is written to the blackboard. Whether the agent holds a place to attack the target from is not
    /// a fact of the Brain any more (step 90): a decision that needs one says so by a visible Attack Slot condition wired
    /// into its Condition pin.
    /// </summary>
    public readonly struct TargetFacts
    {
        public readonly Transform Target;
        public readonly float Distance;

        /// <summary>Seconds the Brain has had this target without a break (0 — it came this think, or there is none).</summary>
        public readonly float SeenFor;

        public TargetFacts(Transform target, float distance, float seenFor = 0f)
        {
            Target = target;
            Distance = distance;
            SeenFor = target != null ? seenFor : 0f;
        }

        /// <summary>There is a target to think about.</summary>
        public bool Sees => Target != null;

        public static TargetFacts None => new(null, SensorTask.FAR);

        public static TargetFacts Of(AgentContext context, Transform target, float seenFor = 0f)
        {
            if (context == null || target == null)
                return None;

            return new TargetFacts(target, Vector3.Distance(context.Position, target.position), seenFor);
        }
    }
}

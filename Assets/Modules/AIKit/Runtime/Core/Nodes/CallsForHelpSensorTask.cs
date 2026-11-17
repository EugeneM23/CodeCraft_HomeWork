using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Calls For Help, a sensor of Detection: it "sees" what an ally called about — the object a Call For Help of
    /// somebody within its call's radius named — for Remember seconds after the call, if the object is of Detection's
    /// kind (its tag) and no farther than Within. So the call is visible where it is heard: wired into Detection, it is
    /// one of the ways this character sees; without it the character does not hear calls at all. Seen by another sensor
    /// meanwhile, the object is held by the usual rule; not seen once the call is old — lost (handbook §10: Help is a
    /// signal of the bus; §9.4: memory per target).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Calls For Help", "Sensor", "aikit.sensor.calls-for-help", Order = 60)]
    public sealed class CallsForHelpSensorTask : SensorTask
    {
        /// <summary>Seconds a call is heard for: the time to get there.</summary>
        public BlackboardValue<float> Remember = 8f;

        /// <summary>How far from this character the object called about may be, metres.</summary>
        public BlackboardValue<float> Within = 30f;

        protected internal override bool Listens => true;

        protected internal override float Reach(AgentContext context) => Mathf.Max(0f, Within.Get(context));

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            if (context == null || candidate == null)
                return false;

            Transform subject = context.SignalSubject(CallForHelpTask.SIGNAL);
            if (subject != candidate || !context.HasSignalSince(CallForHelpTask.SIGNAL, Remember.Get(context)))
                return false;

            float within = Reach(context);              // another shape of Detection may gather from farther
            return (candidate.position - context.Position).sqrMagnitude <= within * within;
        }
    }
}

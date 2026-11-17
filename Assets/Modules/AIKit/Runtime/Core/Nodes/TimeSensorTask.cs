using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The clock of the world an agent lives by: the hour of the day, 0…24. A game puts it on an object of the scene
    /// (with the tag the time sensors look for); who turns the hours and how long a day is — the game's.
    /// </summary>
    public interface IDayClock
    {
        float Hour { get; }
    }

    /// <summary>
    /// A sensor of time: "sees" the clock of the scene while the hour is inside its period — from From to To, over
    /// midnight when To is less than From (22 → 6 is a night). Like any sensor it only writes what it sees into its
    /// variable — the clock while the period lasts, nothing outside it; wire the variable into a decision that cares
    /// (Sleep: its Night pin). Every agent may have hours of its own.
    /// The clock is the object with the sensor's Tag that carries an IDayClock; no such object — it never sees anything.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Time Of Day", "Sensor", "aikit.sensor.time", Order = 70, WorksAlone = true)]
    public sealed class TimeSensorTask : SensorTask
    {
        public TimeSensorTask()
        {
            Tag = "GameController";
            Interval = 0.5f;
        }

        /// <summary>The hour the period begins at.</summary>
        public BlackboardValue<float> From = 22f;

        /// <summary>The hour it ends at (less than From — the period runs over midnight).</summary>
        public BlackboardValue<float> To = 6f;

        // The clock holds no attack place: losing it gives nothing back (step 90: SensorTask.GivesPlaceBack)
        protected override bool GivesPlaceBack => false;

        protected internal override float Reach(AgentContext context) => float.MaxValue;     // wherever the clock is

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            var clock = candidate.GetComponentInParent<IDayClock>();
            return clock != null && Within(clock.Hour, From.Get(context), To.Get(context));
        }

        /// <summary>The hour lies inside the period (which may run over midnight).</summary>
        public static bool Within(float hour, float from, float to) =>
            from <= to ? hour >= from && hour < to : hour >= from || hour < to;
    }
}

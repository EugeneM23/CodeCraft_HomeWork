using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The sensor of the graph made of sensors: any number of them wired in (a radius, a cone, a zone…), each with
    /// its own settings. An object is noticed when at least one of them sees it, and held while at least one of them
    /// still does. Detection itself does the looking — every LookEvery seconds, with a small random spread so agents
    /// do not all look in the same frame (handbook §9.6) — and writes what it sees into its variable; the sensors
    /// wired in only say what "sees" means (handbook §9.2: two or three shapes instead of one).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Detection", "Sensor", "aikit.sensor.detection", Order = 10)]
    public sealed class DetectionTask : SensorTask
    {
        /// <summary>The sensors wired in: only their shape is used (their own tag, variable and interval are not).</summary>
        [NonSerialized] public List<SensorTask> Sensors = new();

        /// <summary>The look period is LookEvery ± this share of it, at random: 0.2 — from 80% to 120%.</summary>
        private const float SPREAD = 0.2f;

        public DetectionTask()
        {
        }

        public DetectionTask(params SensorTask[] sensors)
        {
            Sensors.AddRange(sensors);
        }

        protected internal override float Reach(AgentContext context)
        {
            float reach = 0f;
            for (int i = 0; i < Sensors.Count; i++)
            {
                if (Sensors[i] != null)
                    reach = Mathf.Max(reach, Sensors[i].Reach(context));
            }

            return reach;
        }

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            for (int i = 0; i < Sensors.Count; i++)
            {
                if (Sensors[i] != null && Sensors[i].Sees(context, candidate, isSeen))
                    return true;
            }

            return false;
        }

        // The shapes wired in never tick themselves: what Detection holds is what they hold (a line's grace per target)
        protected internal override void BeginLook(AgentContext context, Transform held)
        {
            base.BeginLook(context, held);
            for (int i = 0; i < Sensors.Count; i++)
                Sensors[i]?.BeginLook(context, held);
        }

        /// <summary>A sensor wired in hears the signals of the scene (Calls For Help).</summary>
        protected internal override bool Listens
        {
            get
            {
                for (int i = 0; i < Sensors.Count; i++)
                {
                    if (Sensors[i] != null && Sensors[i].Listens)
                        return true;
                }

                return false;
            }
        }

        protected override float NextLook(AgentContext context)
        {
            float period = base.NextLook(context);
            return context == null ? period : period * (1f - SPREAD + 2f * SPREAD * (float)context.NextRandom());
        }

        public override void DrawGizmos(Vector3 origin, Vector3 forward, AgentContext context,
            Func<string, Transform> objectOf)
        {
            for (int i = 0; i < Sensors.Count; i++)
                Sensors[i]?.DrawGizmos(origin, forward, context, objectOf);
        }
    }
}

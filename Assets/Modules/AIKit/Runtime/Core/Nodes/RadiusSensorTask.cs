using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Sees an object anywhere around the agent: noticed within Radius, kept up to LoseRadius. Walls and the way the
    /// agent faces do not matter.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Radius", "Sensor", "aikit.sensor.radius", Order = 20)]
    public sealed class RadiusSensorTask : SensorTask
    {
        public BlackboardValue<float> Radius = 5f;

        /// <summary>An object already seen is kept up to this distance (less than Radius — Radius is used).</summary>
        public BlackboardValue<float> LoseRadius = 7f;

        protected internal override float Reach(AgentContext context) =>
            Mathf.Max(Radius.Get(context), LoseRadius.Get(context));

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            float radius = isSeen ? Reach(context) : Radius.Get(context);
            return (candidate.position - context.Position).sqrMagnitude <= radius * radius;
        }

        public override void DrawGizmos(Vector3 origin, Vector3 forward, AgentContext context,
            Func<string, Transform> objectOf)
        {
            Color color = Gizmos.color;
            DrawCircle(origin, Radius.Get(context));
            Gizmos.color = Faint(color);
            DrawCircle(origin, Reach(context));
            Gizmos.color = color;
        }
    }

    /// <summary>
    /// The radius sensor with eyes: an object within Radius is noticed only when nothing stands between it and the
    /// agent (a ray from the eyes — the body's sight probe, Physics Probes; without one every line is clear).
    /// An object already seen is kept up to LoseRadius, and for LoseSightAfter seconds after it went behind a wall —
    /// so a chase is not dropped at the first corner.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Radius (Line Of Sight)", "Sensor", "aikit.sensor.radius-sight", Order = 30)]
    public sealed class SightRadiusSensorTask : SensorTask
    {
        public BlackboardValue<float> Radius = 5f;

        /// <summary>An object already seen is kept up to this distance (less than Radius — Radius is used).</summary>
        public BlackboardValue<float> LoseRadius = 7f;

        /// <summary>Seconds an object already seen is kept after the line to it got blocked.</summary>
        public BlackboardValue<float> LoseSightAfter = 2f;

        protected internal override float Reach(AgentContext context) =>
            Mathf.Max(Radius.Get(context), LoseRadius.Get(context));

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            float radius = isSeen ? Reach(context) : Radius.Get(context);
            if ((candidate.position - context.Position).sqrMagnitude > radius * radius)
                return false;

            // A level line from the eyes: where the pivot of the object is (feet or centre) does not matter
            Vector3 eye = context.EyePosition;
            Vector3 aim = candidate.position;
            aim.y = eye.y;
            if (context.Sight == null || context.Sight.HasLineOfSight(eye, aim))
            {
                MarkClear(context, candidate);
                return true;
            }

            return isSeen && SinceClear(context) <= LoseSightAfter.Get(context);
        }

        public override void DrawGizmos(Vector3 origin, Vector3 forward, AgentContext context,
            Func<string, Transform> objectOf)
        {
            Color color = Gizmos.color;
            DrawCircle(origin, Radius.Get(context));
            Gizmos.color = Faint(color);
            DrawCircle(origin, Reach(context));
            Gizmos.color = color;
        }
    }

    /// <summary>
    /// Sees an object in front of the agent: inside a cone of HalfAngle degrees to each side of where the body looks,
    /// up to Range — over the ground for a body in 3D, in the plane of the view for a side-view body (as the Senses
    /// measure: AgentPerception.ViewOf). An object already seen is kept up to LoseRange whichever way the agent faces (it
    /// does not lose what it chases by turning). With LineOfSight the look must not be blocked — a level line from the
    /// eyes, as Radius (Line Of Sight) draws it; that needs a sight probe on the character (Physics Probes); without one
    /// every line is clear.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Cone", "Sensor", "aikit.sensor.cone", Order = 40)]
    public sealed class ConeSensorTask : SensorTask
    {
        public BlackboardValue<float> Range = 8f;

        /// <summary>Degrees to each side of the facing: 45 — a quarter of the circle in front, 180 — all around.</summary>
        public BlackboardValue<float> HalfAngle = 45f;

        /// <summary>An object already seen is kept up to this distance, at any angle (less than Range — Range is used).</summary>
        public BlackboardValue<float> LoseRange = 10f;

        /// <summary>On — a blocked look does not see (asks the character's sight probe; none — nothing blocks).</summary>
        public bool LineOfSight;

        protected internal override float Reach(AgentContext context) =>
            Mathf.Max(Range.Get(context), LoseRange.Get(context));

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            // In the plane the body sees in, from where it looks (the Senses' rule: a body in 3D over the ground along
            // its Forward, a side-view body in the plane of the view along its facing)
            Vector3 normal = AgentPerception.ViewOf(context, out Vector3 look);
            Vector3 to = Vector3.ProjectOnPlane(candidate.position - context.Position, normal);
            float range = isSeen ? Reach(context) : Range.Get(context);
            if (to.sqrMagnitude > range * range)
                return false;

            if (!isSeen && to.sqrMagnitude > 1e-6f && look != Vector3.zero && Vector3.Angle(look, to) > HalfAngle.Get(context))
                return false;

            if (!LineOfSight || context.Sight == null)
                return true;

            // A level line from the eyes, as Radius (Line Of Sight) draws it: where the pivot of the object is (its feet,
            // at or a little under the ground) does not matter
            Vector3 eye = context.EyePosition;
            Vector3 aim = candidate.position;
            aim.y = eye.y;
            return context.Sight.HasLineOfSight(eye, aim);
        }

        public override void DrawGizmos(Vector3 origin, Vector3 forward, AgentContext context,
            Func<string, Transform> objectOf)
        {
            Color color = Gizmos.color;
            DrawSector(origin, forward, Range.Get(context), HalfAngle.Get(context));
            Gizmos.color = Faint(color);
            DrawCircle(origin, Reach(context));
            Gizmos.color = color;
        }
    }


    /// <summary>
    /// Sees an object that stands inside a zone — an AI Object of a kind ("Patrol Zone") that is the character's own (it
    /// names the character among its owners; step 90: no scene objects on the character — the zone says whose it is),
    /// found through the registry of the world: its zone (a box or a circle) or, without one, its drawn box seen from
    /// above — the same area the Patrol Area node walks. Where the agent itself is does not matter: the sensor watches
    /// the place, not the surroundings of the agent. No zone of the kind is the character's — it sees nothing.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Zone", "Sensor", "aikit.sensor.zone", Order = 50)]
    public sealed class ZoneSensorTask : SensorTask
    {
        /// <summary>The kind of AI Object that is the zone (one the character owns).</summary>
        [ObjectKind] public string Zone;

        /// <summary>An object already seen is kept this far outside the zone's edge.</summary>
        public BlackboardValue<float> LoseMargin = 1f;

        // One look at a time (the main thread): the registry's candidates
        private static readonly System.Collections.Generic.List<WorldObject> Zones = new();

        protected internal override float Reach(AgentContext context) => float.MaxValue;     // wherever the zone is

        /// <summary>The character's own zone of the kind (the nearest of its own); null — it owns none.</summary>
        public AIObject ZoneOf(AgentContext context)
        {
            if (context == null || string.IsNullOrEmpty(Zone))
                return null;

            context.WorldObjects.Find(new ObjectQuery { Kind = Zone }, Zones);
            AIObject best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Zones.Count; i++)
            {
                if (Zones[i] is not AIObject zone || !zone.IsOwnedBy(context.Agent))
                    continue;

                float distance = (zone.transform.position - context.Position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = zone;
                }
            }

            Zones.Clear();
            return best;
        }

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen)
        {
            AIObject zone = ZoneOf(context);
            if (zone == null)
                return false;

            float margin = isSeen ? LoseMargin.Get(context) : 0f;
            if (zone.HasZone)
                return zone.Contains(candidate.position, margin);

            IAreaSampler areas = context.Areas ?? BoundsAreaSampler.Shared;
            return areas.Contains(zone.transform, candidate.position, margin);
        }

        public override void DrawGizmos(Vector3 origin, Vector3 forward, AgentContext context,
            Func<string, Transform> objectOf)
        {
            AIObject zone = ZoneOf(context);
            if (zone == null)
                return;

            Vector3 center = zone.transform.position;
            if (!zone.HasZone && BoundsAreaSampler.TryBounds(zone.transform, out Bounds bounds))
            {
                Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 0.05f, bounds.size.z));
                center = bounds.center;
            }

            Gizmos.DrawLine(origin, center);
        }
    }
}

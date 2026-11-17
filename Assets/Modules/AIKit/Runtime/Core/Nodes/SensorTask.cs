using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A sensor of the graph: works beside whatever node runs (a background task of the root) and keeps looking for an
    /// object of a kind (a Unity tag). What it sees goes into a Transform variable — the object while it is seen,
    /// nothing once it is lost — and that is all it does: what to do about it is the Brain's (handbook §0.1, §38 №1: no
    /// decision in a sensor). The variable comes into the Brain's Target or a node's pin by a visible wire. A sensor
    /// holds on to the object it already sees (it does not jump to a nearer one) and, of several new ones, takes the
    /// nearest. What "sees" means is the subclass's: a radius, a cone of vision, a zone, a call for help.
    /// </summary>
    [Serializable]
    public abstract class SensorTask : AgentTask
    {
        private sealed class Mem
        {
            public float LookIn;
            public float ClearAt = float.NegativeInfinity;      // the line to Held was clear then
            public Transform Held;
            public Transform Noticed;       // of the objects not held, the nearest whose line was clear on the last look
            public float NoticedAt = float.NegativeInfinity;
            public float NoticedDistance = float.MaxValue;
            public object Own;              // a sensor kind's own memory (OwnMemory)
        }

        /// <summary>
        /// A sensor kind's own memory for this agent, kept beside the sensor's (a node has one memory per agent): made on
        /// the first ask. The Senses sensor keeps the agent's perception here.
        /// </summary>
        protected T OwnMemory<T>(AgentContext context) where T : class, new()
        {
            Mem m = Memory<Mem>(context);
            if (m.Own is not T own)
            {
                own = new T();
                m.Own = own;
            }

            return own;
        }

        /// <summary>
        /// A sensor that looks along a line: the line to this object was clear just now. The stamp is the held object's
        /// own — another object in sight does not keep a held one that went behind a wall (handbook §9.4: a stamp per
        /// target) — and an object taken in its place starts with the moment its own line was clear, not with what is
        /// left of the lost one's. Inside Detection the object held is Detection's (BeginLook passes it on).
        /// </summary>
        protected void MarkClear(AgentContext context, Transform candidate)
        {
            Mem m = Memory<Mem>(context);
            if (candidate == m.Held)
            {
                m.ClearAt = context.LocalTime;
                return;
            }

            // Not held: if the sensor takes it on this look, it is the nearest of those it saw clear (it takes the nearest)
            float distance = (candidate.position - context.Position).sqrMagnitude;
            if (distance < m.NoticedDistance)
            {
                m.Noticed = candidate;
                m.NoticedAt = context.LocalTime;
                m.NoticedDistance = distance;
            }
        }

        /// <summary>
        /// A look begins: the object the sensor holds now (null — none). An object the sensor took on its last look gets
        /// the stamp of its own clear line then (none — it was not seen clear: no grace). Detection passes the object it
        /// holds on to the shapes wired into it, whose own ticks never run.
        /// </summary>
        protected internal virtual void BeginLook(AgentContext context, Transform held)
        {
            Mem m = Memory<Mem>(context);
            if (held != m.Held)
                m.ClearAt = held != null && held == m.Noticed ? m.NoticedAt : float.NegativeInfinity;

            m.Held = held;
            m.Noticed = null;
            m.NoticedAt = float.NegativeInfinity;
            m.NoticedDistance = float.MaxValue;
        }

        /// <summary>Seconds since the line was last clear (infinity — never).</summary>
        protected float SinceClear(AgentContext context) => context.LocalTime - Memory<Mem>(context).ClearAt;

        // One look at a time (the main thread): the candidates of the look in progress
        private static readonly List<Transform> Candidates = new();

        protected SensorTask()
        {
            IsBackground = true;
        }

        /// <summary>The kind of object to look for: a Unity tag (picked from the project's tags).</summary>
        [TagName] public string Tag = "Player";

        /// <summary>
        /// A second kind of object it looks for as well (a Unity tag; empty or Untagged — none): a soldier sees the player
        /// and the soldiers of the other side. Of all it sees of both kinds, the nearest is taken.
        /// </summary>
        [TagName] public string AlsoTag = "";

        /// <summary>
        /// Only objects with a component of this type (step 91: one the game marked [AIReadable] — the player's controller,
        /// an AI Character); with no Tag — every object that has one. Empty — any object of the tags.
        /// </summary>
        [TypeName(typeof(Component), AnyComponent = true, Empty = "(any type)")] public string OfType = "";

        /// <summary>Seconds between looks.</summary>
        public BlackboardValue<float> Interval = 0.2f;

        /// <summary>The Transform variable (of the agent) the seen object is written to.</summary>
        [VariableName(VariableType.Transform)] public string StoreIn;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _store;

        /// <summary>The object this agent's sensor sees now (null — nothing).</summary>
        public Transform SeenBy(AgentContext context) =>
            context != null && !string.IsNullOrEmpty(StoreIn) ? _store.Get(context.Blackboard, StoreIn) : null;

        /// <summary>How far around the agent candidates are gathered at all (the largest distance the sensor may see at).</summary>
        protected internal abstract float Reach(AgentContext context);

        /// <summary>
        /// The sensor sees this object. isSeen — it is the object the sensor already holds: the test to keep it may be
        /// more forgiving than the test to notice it (a gap that keeps the sensor from flickering at the edge).
        /// </summary>
        protected internal abstract bool Sees(AgentContext context, Transform candidate, bool isSeen);

        /// <summary>Seconds until the next look (Interval; Detection spreads it a little at random).</summary>
        protected virtual float NextLook(AgentContext context) => Mathf.Max(0f, Interval.Get(context));

        /// <summary>
        /// Draws the sensor in the Scene view (Gizmos; the colour is set by the caller): its shape around the agent.
        /// context — the running agent's, null in Edit Mode (fields then give their constants); objectOf resolves a
        /// Transform variable to its object (the character's binding in Edit Mode, the blackboard in Play Mode).
        /// </summary>
        public virtual void DrawGizmos(Vector3 origin, Vector3 forward, AgentContext context,
            Func<string, Transform> objectOf)
        {
        }

        protected override void OnEnter(AgentContext context)
        {
            Memory<Mem>(context).LookIn = 0f;
            if (Listens && context != null)
                AIWorldRunner.Listen(context.Agent);        // hears the others of the scene from now on
        }

        /// <summary>
        /// The sensor gives the agent's attack places on an object back when it loses that object (a place to attack
        /// what is no longer seen would be held for nothing): a sensor that writes a variable does — what it does by
        /// itself is said on its node (Requirement, step 90). A sensor that keeps no target (the clock, a squad's eyes)
        /// says no.
        /// </summary>
        protected virtual bool GivesPlaceBack => !string.IsNullOrEmpty(StoreIn);

        public override string Requirement => GivesPlaceBack ? "gives the attack place back when the target is lost" : null;

        /// <summary>The distance written while the sensor sees nothing.</summary>
        public const float FAR = 9999f;

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null || string.IsNullOrEmpty(StoreIn))
                return TaskStatus.Running;

            Mem m = Memory<Mem>(context);
            m.LookIn -= dt;
            if (m.LookIn > 0f)
                return TaskStatus.Running;

            m.LookIn = NextLook(context);
            Transform seen = _store.Get(context.Blackboard, StoreIn);
            BeginLook(context, seen);
            Transform now = Look(context, seen);
            _store.Set(context.Blackboard, StoreIn, now);
            if (seen != null && now != seen && context.Roster != null && GivesPlaceBack)
                context.Roster.FindEngagement(seen)?.Tokens.Forget(context.Agent);  // lost it: its attack slot is free
            return TaskStatus.Running;
        }

        /// <summary>The sensor hears the signals of the scene (Calls For Help): the agent listens from the moment it starts.</summary>
        protected internal virtual bool Listens => false;

        /// <summary>The type named by Of Type; null — none (any object of the tags), or no loaded type has that name.</summary>
        protected Type TypeLookedFor => string.IsNullOrEmpty(OfType) ? null : AIReadables.TypeNamed(OfType);

        /// <summary>The sensor names a tag (not empty, not Untagged).</summary>
        protected bool HasTag => !string.IsNullOrEmpty(Tag) && Tag != "Untagged";

        /// <summary>The object (or one above it) has a component of the type.</summary>
        protected static bool IsOfType(Transform candidate, Type type) =>
            candidate != null && candidate.GetComponentInParent(type, true) != null;

        /// <summary>The sensor looks for a second kind of object too.</summary>
        protected bool HasAlsoTag =>!string.IsNullOrEmpty(AlsoTag) && AlsoTag != "Untagged" && AlsoTag != Tag;

        // One look at a time (the main thread): the candidates of the second kind
        private static readonly List<Transform> AlsoCandidates = new();

        /// <summary>
        /// One look: what the sensor sees now — of the candidates of its kind within Reach, the one it holds while it
        /// still sees it, else the nearest it sees; null — nothing.
        /// </summary>
        protected virtual Transform Look(AgentContext context, Transform seen)
        {
            if (context.Objects == null)
                return null;

            Transform self = context.Agent != null ? context.Agent.EntityRoot : context.Self;
            Vector3 position = context.Position;
            Type type = TypeLookedFor;
            bool byType = type != null && !HasTag;
            if (byType)
                context.Objects.FindAllOfType(type, position, Reach(context), self, Candidates);
            else
                context.Objects.FindAll(Tag, position, Reach(context), self, Candidates);
            if (HasAlsoTag && !byType)
            {
                context.Objects.FindAll(AlsoTag, position, Reach(context), self, AlsoCandidates);
                Candidates.AddRange(AlsoCandidates);
                AlsoCandidates.Clear();
            }

            Transform nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < Candidates.Count; i++)
            {
                Transform candidate = Candidates[i];
                if (type != null && !byType && !IsOfType(candidate, type))
                    continue;

                bool isSeen = candidate == seen;
                if (!Sees(context, candidate, isSeen))
                    continue;

                if (isSeen)
                {
                    nearest = candidate;                // still sees the one it holds: stays with it
                    break;
                }

                float distance = (candidate.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            Candidates.Clear();
            return nearest;
        }

        // ---------- shapes for DrawGizmos ----------

        /// <summary>A flat circle around a point (Gizmos has spheres only).</summary>
        protected static void DrawCircle(Vector3 center, float radius, int segments = 48)
        {
            if (radius <= 0f)
                return;

            Vector3 previous = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        /// <summary>A flat sector: two edges and the arc between them, halfAngle degrees to each side of forward.</summary>
        protected static void DrawSector(Vector3 center, Vector3 forward, float radius, float halfAngle, int segments = 24)
        {
            forward.y = 0f;
            if (radius <= 0f || forward.sqrMagnitude < 1e-6f)
                return;

            forward.Normalize();
            halfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
            Vector3 previous = center + Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward * radius;
            if (halfAngle < 180f)
                Gizmos.DrawLine(center, previous);

            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Vector3 next = center + Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            if (halfAngle < 180f)
                Gizmos.DrawLine(center, previous);
        }

        /// <summary>The same colour, fainter: the "keeps up to here" outline next to the "notices from here" one.</summary>
        protected static Color Faint(Color color) => new(color.r, color.g, color.b, color.a * 0.35f);
    }
}

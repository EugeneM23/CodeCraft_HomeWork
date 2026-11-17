using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Keeps the agent at shooting distance from the target — the kiter's legs. Nearer than MinDistance it backs away
    /// (eyes on the target) to the middle of the band; farther than MaxDistance it comes closer. Success ("In Range")
    /// — it stands inside the band, facing the target: shoot. Also "In Range" when it has backed away for RetreatTime
    /// and still is too near (cornered, or the target is faster): better shoot than run forever.
    /// Failure ("Lost") — no target. Steers straight (no path around obstacles).
    /// With Watch The Way Back (and a body that answers "how far can I go straight": its walk probe) it looks behind
    /// before every step back (handbook §14.1 — a context map: directions it wants, directions it cannot take): the way
    /// straight back is free — it backs straight away; a wall behind — it backs away along the wall, the free way that
    /// leads farthest from the target; cornered (every free way leads toward the target) — it does not rub against the
    /// wall: it runs by the path to another place at shooting distance with room behind it, even past the target
    /// (§23.4: a position query with a fallback, "куда отходить в эту сторону"). A target that keeps up (still too near
    /// after Retreat Time) is run from the same way, once in ESCAPE_REST seconds; in between the agent shoots.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Kite", "Task", "aikit.task.kite", Success = "In Range", Failure = "Lost",
        Simple = true)]
    public sealed class KiteTask : AgentTask
    {
        private sealed class Mem
        {
            public bool Retreating;
            public float Retreated;
            public float Side = 1f;
            public float Blocked;
            public bool HasVantage;
            public Vector3 Vantage;
            public float VantageAge;

            // Watching the way back: the free way chosen along a wall, kept a moment; the run out of a corner
            public Vector3 WayBack;
            public float WayBackAge;
            public bool Escaping;
            public Vector3 Escape;
            public float Escaped;

            // The agent's time the last run to a new place ended (kept between the node's runs: a run is rested from)
            public float LastEscape = float.NegativeInfinity;

            // The agent's time the last run gave up without getting there (too long, no way): a corner is not run out of
            // again before Escape Rest
            public float LastGaveUp = float.NegativeInfinity;
        }

        /// <summary>The Transform variable (of the agent) with the object to keep away from.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>Nearer than this — back away. 0 — from the reach of the weapon in the hand (0.35 × it; step 90).</summary>
        public BlackboardValue<float> MinDistance = 5f;

        /// <summary>Farther than this — come closer. 0 — from the reach of the weapon in the hand (0.65 × it).</summary>
        public BlackboardValue<float> MaxDistance = 9f;

        /// <summary>The band with no weapon in the hand (the old defaults).</summary>
        public const float NO_WEAPON_MIN = 5f, NO_WEAPON_MAX = 9f;

        // The band of a shooter is its weapon's: what a 0 means is said on the node (step 90, spec 10.4)
        public override string Requirement => WeaponDistances.Join(
            WeaponDistances.Say("backs away within", MinDistance, WeaponDistances.KITE_NEAR, NO_WEAPON_MIN),
            WeaponDistances.Say("comes closer beyond", MaxDistance, WeaponDistances.KITE_FAR, NO_WEAPON_MAX));

        /// <summary>How fast, as a part of the body's full speed.</summary>
        public BlackboardValue<float> SpeedScale = 1f;

        /// <summary>Seconds of backing away after which it stops and shoots anyway.</summary>
        public BlackboardValue<float> RetreatTime = 2f;

        /// <summary>
        /// On — "In Range" only with a clear line to the target. A wall in between — the agent picks the nearest
        /// place at shooting distance from which the target is seen and runs there by the path (around the wall).
        /// Needs a sight probe on the body (Physics Probes); without one every line counts as clear.
        /// </summary>
        public bool NeedsSight = true;

        /// <summary>
        /// Degrees of clear view the agent wants on the target: not only the line to it but this wide a fan around
        /// that line must be free. 0 — the bare line is enough (it shoots past the very edge of a wall); more — it
        /// runs out wider from behind the corner before it stops to shoot.
        /// </summary>
        public BlackboardValue<float> MinViewAngle = 15f;

        /// <summary>
        /// Metres of free room the agent wants on each side of its line to the target, all the way along it (handbook
        /// §18а.1: a place to shoot from is checked with several rays, not one): it steps this far out from behind a
        /// corner before it stops to shoot — an arrow is not the line of the eyes, it leaves from the hands. The angle of
        /// Min View Angle gives room only far away, next to the agent it gives centimetres. 0 — no room asked.
        /// </summary>
        public BlackboardValue<float> Clearance = 0.6f;

        /// <summary>
        /// On — before it backs away it looks whether the way back is free (needs the body's walk probe; without one every
        /// way is free): a wall behind — it backs away along the wall; cornered — it runs to another place at shooting
        /// distance, even past the target, instead of rubbing against the wall. Off — it backs straight away whatever is
        /// behind, and stops to shoot after Retreat Time.
        /// </summary>
        public bool WatchTheWayBack = true;

        /// <summary>Metres of free way a step back needs in its direction (beyond the body's radius).</summary>
        public const float LOOK_BACK = 1.5f;

        /// <summary>Directions of the context map round the agent (§14.1: 8–16 in 3D).</summary>
        private const int WAY_DIRECTIONS = 16;

        /// <summary>
        /// The least "away from the target" a free way may be (the cosine to straight back): a little toward the target
        /// still counts (−0.2 ≈ 100°), more — the agent is cornered.
        /// </summary>
        private const float CORNERED = -0.2f;

        /// <summary>Seconds a way back along a wall is kept while it is free (no flicker between two equal ways).</summary>
        private const float WAY_BACK_KEEP = 0.4f;

        /// <summary>Metres of free way behind a new place (away from the target): it must not be another corner.</summary>
        private const float ESCAPE_ROOM = 3f;

        /// <summary>Metres a new place is at least away from where the cornered agent stands.</summary>
        private const float ESCAPE_AWAY = 3f;

        /// <summary>Metres from the new place it counts as there.</summary>
        private const float ESCAPE_ARRIVE = 0.75f;

        /// <summary>Seconds a run to a new place lasts at most.</summary>
        private const float ESCAPE_LIMIT = 6f;

        /// <summary>
        /// Seconds after a run to a new place before a target that keeps up (too near after Retreat Time) is run from
        /// again — and, after a run that gave up (too long, no way), before a corner is run out of again: in between the
        /// agent shoots — better shoot than run forever.
        /// </summary>
        public const float ESCAPE_REST = 4f;

        /// <summary>The agent of this context runs out of a corner to a new place now (tests, the debugger).</summary>
        public bool IsEscaping(AgentContext context) => Memory<Mem>(context).Escaping;

        /// <summary>Seconds of sidestepping one way with no clear line before it tries the other way.</summary>
        private const float SIDE_TIME = 2.5f;

        /// <summary>Seconds a chosen place to shoot from is kept before it is chosen anew (the target moves).</summary>
        private const float VANTAGE_TIME = 1f;

        /// <summary>Places tried on each ring around the target.</summary>
        private const int VANTAGE_ANGLES = 16;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Retreating = false;
            m.Retreated = 0f;
            m.Blocked = 0f;
            m.HasVantage = false;
            m.WayBack = Vector3.zero;
            m.WayBackAge = 0f;
            m.Escaping = false;
            m.Escaped = 0f;
            m.Side = context != null && context.Chance(0.5f) ? 1f : -1f;
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            IAgentActuator actuator = context?.Actuator;
            if (actuator == null)
                return TaskStatus.Failure;

            Transform target = (string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target));
            if (target == null)
                return TaskStatus.Failure;

            Vector3 toTarget = target.position - context.Position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 1e-3f)
                return TaskStatus.Running;

            Mem m = Memory<Mem>(context);
            float min = WeaponDistances.Resolve(MinDistance.Get(context), context, WeaponDistances.KITE_NEAR, NO_WEAPON_MIN);
            float max = Mathf.Max(min, WeaponDistances.Resolve(MaxDistance.Get(context), context, WeaponDistances.KITE_FAR, NO_WEAPON_MAX));
            Vector3 inward = toTarget / distance;

            // Out of a corner to a new place, by the path: the band is asked again once it is there
            if (m.Escaping && Escape(context, m, dt))
                return TaskStatus.Running;

            // Too near: back away until the middle of the band (not just to its edge — or one step of the target
            // would send it running again). A retreat that ended is over: the next one backs away all of Retreat Time
            if (distance < min)
            {
                m.Retreating = true;
            }
            else if (distance >= (min + max) * 0.5f)
            {
                m.Retreating = false;
                m.Retreated = 0f;
            }

            bool sees = Sees(context, target);
            Vector3 direction;
            if (m.Retreating)
            {
                m.Retreated += dt;
                direction = WayBack(context, m, inward, dt);
                if (direction.sqrMagnitude < 1e-6f)
                {
                    // Cornered: not into the wall — out to another place at shooting distance, past the target if need be;
                    // after a run that did not get there (too long, no way) not before Escape Rest: it was run again at
                    // once, for ever, and never shot (better shoot than run for ever)
                    if (context.LocalTime - m.LastGaveUp >= ESCAPE_REST && StartEscape(context, m, target, min, max) &&
                        Escape(context, m, dt))
                        return TaskStatus.Running;

                    direction = -inward;                // nowhere to run to: as before, and it shoots after Retreat Time
                }

                if (sees && m.Retreated >= RetreatTime.Get(context))
                {
                    // The target keeps up: once in a while not a shot from too near but a run to another place at shooting
                    // distance (the run opens the distance the backing away could not); in between — better shoot
                    if (Rested(context, m) && StartEscape(context, m, target, min, max) && Escape(context, m, dt))
                        return TaskStatus.Running;

                    return Arrived(context, actuator, inward);
                }
            }
            else if (!sees && GoToVantage(context, m, target, min, max, dt))
            {
                return TaskStatus.Running;              // on its way around the wall, by the path
            }
            else if (distance > max)
            {
                direction = inward;
            }
            else if (sees)
            {
                return Arrived(context, actuator, inward);
            }
            else
            {
                // The distance is right but something is in the way: around the target, sideways, till the line opens
                m.Blocked += dt;
                if (m.Blocked >= SIDE_TIME)
                {
                    m.Blocked = 0f;
                    m.Side = -m.Side;
                }

                float middle = (min + max) * 0.5f;
                direction = new Vector3(-inward.z, 0f, inward.x) * m.Side + inward * Mathf.Clamp(distance - middle, -1f, 1f);
            }

            if (m.HasVantage)
            {
                m.HasVantage = false;                   // steering by hand again: the path is dropped
                context.Navigation?.Clear();
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Strafe))
                actuator.Move(direction, SpeedScale.Get(context));
            if (Claim(context, BodyAxis.Face, MoveAuthority.Strafe))
                actuator.Face(inward);
            return TaskStatus.Running;
        }

        // The way to back away along. Straight back when it is free; else the context map (§14.1): of the free ways round
        // the agent, the one that leads farthest from the target, a little in favour of the roomier one and of the side
        // taken a moment ago (no flicker between two equal ways along a wall). Zero — cornered: every free way leads
        // toward the target
        private Vector3 WayBack(AgentContext context, Mem m, Vector3 inward, float dt)
        {
            Vector3 away = -inward;
            IWalkProbe walk = context.Walk;
            if (!WatchTheWayBack || walk == null)
                return away;

            Vector3 from = context.Position;
            float look = LOOK_BACK + (context.Actuator != null ? context.Actuator.Body.Radius : 0f);
            if (walk.FreeDistance(from, away, look) >= look)
            {
                m.WayBack = Vector3.zero;
                m.WayBackAge = 0f;
                return away;
            }

            m.WayBackAge += dt;
            bool hadWay = m.WayBack.sqrMagnitude > 0.5f;
            if (hadWay && m.WayBackAge < WAY_BACK_KEEP && Vector3.Dot(m.WayBack, away) >= CORNERED &&
                walk.FreeDistance(from, m.WayBack, look) >= look)
                return m.WayBack;

            Vector3 best = Vector3.zero;
            float bestScore = float.MinValue;
            float far = ESCAPE_ROOM * 2f;
            for (int i = 0; i < WAY_DIRECTIONS; i++)
            {
                Vector3 way = Quaternion.AngleAxis(i * 360f / WAY_DIRECTIONS, Vector3.up) * away;
                float awayness = Vector3.Dot(way, away);
                if (awayness < CORNERED)
                    continue;

                float free = walk.FreeDistance(from, way, far);
                if (free < look)
                    continue;

                float score = awayness + 0.3f * free / far + (hadWay && Vector3.Dot(way, m.WayBack) > 0f ? 0.1f : 0f);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = way;
            }

            m.WayBack = best;
            m.WayBackAge = 0f;
            return best;
        }

        // Cornered: the nearest place on two rings round the target, inside the band, that is in the open (a free straight
        // line from the target to it — the agent runs round, never through a wall), has room behind it (not another
        // corner) and is not where the agent stands. False — no such place: it stays and backs off as before
        private bool StartEscape(AgentContext context, Mem m, Transform target, float min, float max)
        {
            IWalkProbe walk = context.Walk;
            if (!WatchTheWayBack || walk == null || context.Navigation == null)
                return false;

            Vector3 self = context.Position;
            Vector3 center = target.position;
            center.y = self.y;
            float best = float.MaxValue;
            Vector3 place = default;
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? (min + max) * 0.5f : Mathf.Max(min + 0.5f, max - 0.5f);
                for (int i = 0; i < VANTAGE_ANGLES; i++)
                {
                    float angle = i * Mathf.PI * 2f / VANTAGE_ANGLES;
                    var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 candidate = center + outward * radius;
                    Vector3 to = candidate - self;
                    to.y = 0f;
                    float cost = to.sqrMagnitude;
                    if (cost >= best || cost < ESCAPE_AWAY * ESCAPE_AWAY ||
                        walk.FreeDistance(center, outward, radius) < radius - 0.1f ||
                        walk.FreeDistance(candidate, outward, ESCAPE_ROOM) < ESCAPE_ROOM)
                        continue;

                    best = cost;
                    place = candidate;
                }
            }

            if (best == float.MaxValue)
                return false;

            m.Escaping = true;
            m.Escape = place;
            m.Escaped = 0f;
            m.Retreated = 0f;
            m.HasVantage = false;
            m.WayBack = Vector3.zero;
            return true;
        }

        // Escape Rest has passed since the last run to a new place ended (the first run any time)
        private static bool Rested(AgentContext context, Mem m) => context.LocalTime - m.LastEscape >= ESCAPE_REST;

        // One step of the run to the new place. True — on its way; false — there, or the run is over (no way, too long)
        private bool Escape(AgentContext context, Mem m, float dt)
        {
            m.Escaped += dt;
            Vector3 to = m.Escape - context.Position;
            to.y = 0f;
            INavigator navigation = context.Navigation;
            bool there = to.magnitude <= ESCAPE_ARRIVE;
            if (there || m.Escaped >= ESCAPE_LIMIT || navigation == null ||
                !navigation.SetDestination(m.Escape).CanGo() || navigation.Status == NavStatus.Unreachable)
            {
                m.Escaping = false;
                m.Retreated = 0f;
                m.LastEscape = context.LocalTime;
                if (!there)
                    m.LastGaveUp = context.LocalTime;
                navigation?.Clear();
                return false;
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return true;
        }

        // A level line from the eyes to above the target: where the target's pivot is (feet or centre) does not matter
        private bool Sees(AgentContext context, Transform target)
        {
            if (!NeedsSight || context.Sight == null)
                return true;

            Vector3 eye = context.EyePosition;
            Vector3 aim = target.position;
            aim.y = eye.y;
            return context.Sight.HasLineOfSight(eye, aim) && ClearView(context, eye, aim);
        }

        // Room round the line from the eye to the aim: the corridor of Clearance on each side is free (two lines, parallel
        // to it, all the way), and the fan of Min View Angle (its two edges are looked along)
        private bool ClearView(AgentContext context, Vector3 eye, Vector3 aim)
        {
            Vector3 line = aim - eye;
            float side = Mathf.Max(0f, Clearance.Get(context));
            var flat = new Vector3(line.x, 0f, line.z);
            if (side > 0.01f && flat.sqrMagnitude > 1e-6f)
            {
                Vector3 across = new Vector3(-flat.z, 0f, flat.x).normalized * side;
                if (!context.Sight.HasLineOfSight(eye + across, aim + across) ||
                    !context.Sight.HasLineOfSight(eye - across, aim - across))
                    return false;
            }

            float half = Mathf.Clamp(MinViewAngle.Get(context), 0f, 170f) * 0.5f;
            if (half <= 0.01f)
                return true;

            return context.Sight.HasLineOfSight(eye, eye + Quaternion.AngleAxis(half, Vector3.up) * line) &&
                   context.Sight.HasLineOfSight(eye, eye + Quaternion.AngleAxis(-half, Vector3.up) * line);
        }

        // No clear line: walk the path to a place the target is seen from. False — no such place or no way to it
        private bool GoToVantage(AgentContext context, Mem m, Transform target, float min, float max, float dt)
        {
            INavigator navigation = context.Navigation;
            if (navigation == null || context.Sight == null)
                return false;

            m.VantageAge += dt;
            bool there = m.HasVantage && (m.Vantage - context.Position).sqrMagnitude < 0.5f * 0.5f;
            if (!m.HasVantage || there || m.VantageAge >= VANTAGE_TIME)
            {
                m.VantageAge = 0f;
                m.HasVantage = FindVantage(context, target, min, max, out m.Vantage);
            }

            if (!m.HasVantage || !navigation.SetDestination(m.Vantage).CanGo() ||
                navigation.Status == NavStatus.Unreachable)
            {
                m.HasVantage = false;
                return false;
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return true;
        }

        // The nearest place (to the agent) on rings around the target, inside the band, the target is seen from.
        // The line is looked along from the side of the target: a place inside a wall is then behind the wall face
        private bool FindVantage(AgentContext context, Transform target, float min, float max, out Vector3 place)
        {
            place = default;
            Vector3 self = context.Position;
            float eyeHeight = context.EyePosition.y - self.y;
            Vector3 center = target.position;
            center.y = self.y;
            Vector3 aim = center + Vector3.up * eyeHeight;
            float best = float.MaxValue;
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = Mathf.Lerp(min + 0.5f, Mathf.Max(min + 0.5f, max - 0.5f), ring * 0.5f);
                for (int i = 0; i < VANTAGE_ANGLES; i++)
                {
                    float angle = i * Mathf.PI * 2f / VANTAGE_ANGLES;
                    Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    float cost = (candidate - self).sqrMagnitude;
                    Vector3 eye = candidate + Vector3.up * eyeHeight;
                    if (cost >= best || !context.Sight.HasLineOfSight(aim, eye) || !ClearView(context, eye, aim))
                        continue;

                    best = cost;
                    place = candidate;
                }
            }

            return best < float.MaxValue;
        }

        private TaskStatus Arrived(AgentContext context, IAgentActuator actuator, Vector3 inward)
        {
            Memory<Mem>(context).HasVantage = false;
            context.Navigation?.Clear();
            actuator.Stop();
            if (Claim(context, BodyAxis.Face, MoveAuthority.Strafe))
                actuator.Face(inward);
            return TaskStatus.Success;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            context?.Navigation?.Clear();
            if (reason != ExitReason.Finished)
                context?.Actuator?.Stop();
        }
    }
}

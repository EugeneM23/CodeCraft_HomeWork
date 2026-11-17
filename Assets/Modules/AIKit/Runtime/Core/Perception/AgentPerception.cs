using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>The alert ladder (handbook §9б.1): what the agent is up to, about its one interest.</summary>
    public enum AlertLevel
    {
        /// <summary>Nothing noticed.</summary>
        Idle,

        /// <summary>Something noticed (awareness at the notice threshold): it looks, it does not fight yet.</summary>
        Suspicious,

        /// <summary>It knows (awareness at the aware threshold): it fights.</summary>
        Aware,

        /// <summary>It knew and lost sight: it searches the last known place while the memory lasts.</summary>
        Searching
    }

    /// <summary>One thing the agent perceives: how aware of it, where last seen, whether in sight now.</summary>
    public sealed class PerceivedTarget
    {
        public Transform Transform;
        public float Awareness;
        public Vector3 LastKnownPosition;
        public float TimeSinceSeen;
        public bool Visible;
    }

    /// <summary>
    /// The agent's senses (handbook §9.1), opened by a Senses sensor of its graph (step 90, C21: the state is the
    /// sensor's; a character whose graph has none has no senses at all): every look — at a random interval from the
    /// profile, phase-shifted per agent — asks the body's ITargetScanner who is around, keeps the candidates the filter
    /// and the factions allow (the character's own side is no target), and checks each against the cones around where
    /// the body looks (a 3D body's forward, a side-view body's facing: focus, periphery, back; a known target is kept up
    /// to LoseRange whichever way the agent faces) and the line of sight (ISightProbe — the only way to the physics,
    /// invariant 7).
    /// Every step awareness grows by the zone's rate while a target is in sight and fades while it is not. The best
    /// track (highest awareness) is the agent's one interest: it is written into the agent's TargetTrack — the subject
    /// while in sight, only the last known place once out of it — and drives the alert ladder. Perception is the only
    /// writer of the track (invariant 10); unfair inputs come through Alert (a hit wakes always) and Hear (a shout
    /// raises a neighbour to Suspicious, not further).
    /// </summary>
    public sealed class AgentPerception
    {
        private const int MAX_TRACKS = 4;
        private const float BACK_ZONE = 0.25f;
        private const float PERIPHERY_ZONE = 0.5f;

        private readonly AgentContext _context;
        private readonly List<PerceivedTarget> _tracks = new(MAX_TRACKS);
        private readonly List<Transform> _candidates = new(16);
        private float _lookIn;
        private bool _started;

        public AgentPerception(AgentContext context, PerceptionProfile profile, ITargetScanner scanner,
            ITargetFilter filter = null)
        {
            _context = context;
            Profile = profile != null ? profile : PerceptionProfile.Defaults;
            Scanner = scanner;
            Filter = filter;
        }

        private PerceptionProfile _profile;

        /// <summary>What it sees with (the Senses sensor's asset, or one given in code); null — the defaults.</summary>
        public PerceptionProfile Profile
        {
            get => _profile;
            set => _profile = value != null ? value : PerceptionProfile.Defaults;
        }

        public ITargetScanner Scanner { get; set; }

        public ITargetFilter Filter { get; set; }

        public IReadOnlyList<PerceivedTarget> Tracks => _tracks;

        /// <summary>The agent's one interest; null — nothing perceived.</summary>
        public PerceivedTarget Best { get; private set; }

        public AlertLevel Level { get; private set; }

        /// <summary>Awareness of the best track (0 — none).</summary>
        public float Awareness => Best?.Awareness ?? 0f;

        /// <summary>The best track is in sight and noticed.</summary>
        public bool CanSee => Best != null && Best.Visible && Best.Awareness >= Profile.NoticeThreshold;

        /// <summary>A lost target is still worth searching for.</summary>
        public bool HasMemory => Best != null && Best.TimeSinceSeen <= Profile.MemoryTime;

        /// <summary>How many looks were taken (tests read when they happen).</summary>
        public int Looks { get; private set; }

        /// <summary>Seconds until the next look.</summary>
        public float NextLookIn => _lookIn;

        public void Step(float dt)
        {
            if (!_started)
            {
                _started = true;
                _lookIn = Profile.VisionIntervalMax > 0f ? _context.Range(0f, Profile.VisionIntervalMax) : 0f;
            }

            _lookIn -= dt;
            if (_lookIn <= 0f)
            {
                Look();
                _lookIn = Profile.VisionIntervalMax > 0f
                    ? _context.Range(Mathf.Min(Profile.VisionIntervalMin, Profile.VisionIntervalMax), Profile.VisionIntervalMax)
                    : 0f;
            }

            for (int i = _tracks.Count - 1; i >= 0; i--)
            {
                PerceivedTarget track = _tracks[i];
                if (track.Transform == null || !track.Transform.gameObject.activeInHierarchy || !IsPerceivable(track.Transform))
                    track.Visible = false;              // destroyed, pooled or dead: never seen again

                if (track.Visible)
                {
                    track.Awareness = Mathf.Min(1f, track.Awareness + Profile.GainPerSecond * ZoneRate(track) * dt);
                    track.TimeSinceSeen = 0f;
                    track.LastKnownPosition = track.Transform.position;
                }
                else
                {
                    track.Awareness = Mathf.Max(0f, track.Awareness - Profile.DecayPerSecond * dt);
                    track.TimeSinceSeen += dt;
                }

                if (track.Awareness <= 0f && track.TimeSinceSeen > Profile.MemoryTime)
                    _tracks.RemoveAt(i);
            }

            Publish();
        }

        /// <summary>
        /// Something made a target known without seeing it — a hit in the back, a scripted reveal: its awareness is
        /// raised at once (to full by default) and the agent decides on its next step.
        /// </summary>
        public void Alert(Transform source, float awareness = 1f)
        {
            if (source == null || !IsTarget(source))
                return;

            PerceivedTarget track = TrackOf(source, Mathf.Clamp01(awareness));
            if (track == null)
                return;                                 // every place is taken by one known at least as well

            track.Awareness = Mathf.Max(track.Awareness, Mathf.Clamp01(awareness));
            track.Visible = true;
            track.TimeSinceSeen = 0f;
            track.LastKnownPosition = source.position;
            Publish();
            _context.Agent?.RequestThink();
        }

        /// <summary>
        /// A signal heard (an ally's shout): its subject becomes at least suspicious — never more from hearsay alone — and
        /// its position the place to search; a signal without a subject is a noise to look at.
        /// </summary>
        public void Hear(in Signal signal)
        {
            PerceivedTarget track = signal.Subject != null && IsTarget(signal.Subject)
                ? TrackOf(signal.Subject, Profile.NoticeThreshold)
                : null;
            if (track != null)
            {
                if (track.Awareness < Profile.NoticeThreshold)
                    track.Awareness = Profile.NoticeThreshold;
                track.LastKnownPosition = signal.Position;
                track.TimeSinceSeen = 0f;
            }

            Publish();
            _context.Agent?.RequestThink();
        }

        /// <summary>
        /// Up to count points around the last known place within radius (the agent's random: one seed — the same
        /// points), on the ground plane of the body. Empty when there is nothing to search for.
        /// </summary>
        public int SearchPoints(int count, float radius, List<Vector3> results)
        {
            results.Clear();
            if (Best == null)
                return 0;

            Vector3 up = _context.Actuator != null ? _context.Actuator.Body.Up : Vector3.up;
            Vector3 right = new Vector3(up.y, -up.x, 0f);
            if (right.sqrMagnitude < 1e-6f)
                right = Vector3.right;
            right.Normalize();
            Vector3 forward = Vector3.Cross(up, right).normalized;

            for (int i = 0; i < count; i++)
            {
                float angle = _context.Range(0f, Mathf.PI * 2f);
                float distance = radius * Mathf.Sqrt(_context.Range(0f, 1f));
                results.Add(Best.LastKnownPosition + right * (Mathf.Cos(angle) * distance) +
                            forward * (Mathf.Sin(angle) * distance));
            }

            return results.Count;
        }

        public bool IsVisible(Transform target)
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i].Transform == target)
                    return _tracks[i].Visible;
            }

            return false;
        }

        /// <summary>Forgets everything (a respawn, a new level).</summary>
        public void Clear()
        {
            _tracks.Clear();
            Best = null;
            Level = AlertLevel.Idle;
            _context.Track.Clear();
        }

        // ---------- looking ----------

        private void Look()
        {
            Looks++;
            for (int i = 0; i < _tracks.Count; i++)
                _tracks[i].Visible = false;

            if (Scanner == null)
                return;

            Vector3 eye = Eye(out Vector3 up, out Vector3 facing);
            Scanner.Scan(eye, Profile.ScanRadius, _candidates);
            for (int i = 0; i < _candidates.Count; i++)
            {
                Transform candidate = _candidates[i];
                if (!IsTarget(candidate))
                    continue;

                PerceivedTarget known = Find(candidate);
                bool held = known != null && known.Awareness >= Profile.NoticeThreshold;
                Vector3 aim = AimOf(candidate);
                if (!InCones(aim - eye, up, facing, held) || !LineClear(eye, aim))
                    continue;

                PerceivedTarget track = known ?? TrackOf(candidate, 0f);
                if (track != null)
                    track.Visible = true;                   // a newcomer with no room waits for one
            }
        }

        private bool InCones(Vector3 toTarget, Vector3 up, Vector3 facing, bool held)
        {
            float distance = toTarget.magnitude;
            if (held)
                return distance <= Profile.LoseRange;

            if (distance <= Profile.BackRange)
                return true;

            float angle = facing.sqrMagnitude > 1e-6f ? Vector3.Angle(facing, toTarget) : 0f;
            return (distance <= Profile.FocusRange && angle <= Profile.FocusHalfAngle) ||
                   (distance <= Profile.PeripheryRange && angle <= Profile.PeripheryHalfAngle);
        }

        // How fast awareness grows: the focus fully, the periphery half, behind a quarter; a held target fully
        private float ZoneRate(PerceivedTarget track)
        {
            if (track.Awareness >= Profile.NoticeThreshold || track.Transform == null)
                return 1f;

            Vector3 eye = Eye(out _, out Vector3 facing);
            Vector3 toTarget = AimOf(track.Transform) - eye;
            float angle = facing.sqrMagnitude > 1e-6f ? Vector3.Angle(facing, toTarget) : 0f;
            float distance = toTarget.magnitude;
            if (distance <= Profile.FocusRange && angle <= Profile.FocusHalfAngle)
                return 1f;

            return distance <= Profile.PeripheryRange && angle <= Profile.PeripheryHalfAngle ? PERIPHERY_ZONE : BACK_ZONE;
        }

        private bool LineClear(Vector3 eye, Vector3 aim)
        {
            if (_context.Sight == null)
                return true;

            _context.SightQueries++;
            return _context.Sight.HasLineOfSight(eye, aim);
        }

        private Vector3 Eye(out Vector3 up, out Vector3 facing)
        {
            AgentBodyInfo body = _context.Actuator != null ? _context.Actuator.Body : AgentBodyInfo.Default;
            up = body.Up.sqrMagnitude > 1e-6f ? body.Up.normalized : Vector3.up;
            facing = LookOf(body, up);
            if (facing == Vector3.zero && _context.Actuator == null)
                facing = Vector3.right;

            return body.Position + up * Profile.EyeHeight;
        }

        /// <summary>
        /// Where the body looks, across its up: a body in 3D says it (Forward, flattened against up — the cones follow
        /// where it turns); a side-view body looks along the right of its up, Facing one way or the other; zero — no
        /// facing (every angle counts as straight ahead).
        /// </summary>
        public static Vector3 LookOf(in AgentBodyInfo body, Vector3 up)
        {
            Vector3 forward = Vector3.ProjectOnPlane(body.Forward, up);
            if (forward.sqrMagnitude > 1e-6f)
                return forward.normalized;

            Vector3 right = new Vector3(up.y, -up.x, 0f);
            return body.Facing != 0 && right.sqrMagnitude > 1e-6f ? right.normalized * body.Facing : Vector3.zero;
        }

        /// <summary>
        /// The plane a body sees in, as the normal that does not count, and where it looks across it (look; zero — nowhere
        /// in particular): a body in 3D sees over the ground — the plane across its up — and looks along its Forward; a
        /// side-view body sees in the plane of its up and its right — the depth does not count — and looks along its
        /// right, Facing one way or the other. The Cone shape of Detection and Get's angles measure by it, as the cones
        /// of the Senses do (LookOf).
        /// </summary>
        public static Vector3 ViewOf(in AgentBodyInfo body, out Vector3 look)
        {
            Vector3 up = body.Up.sqrMagnitude > 1e-6f ? body.Up.normalized : Vector3.up;
            look = LookOf(body, up);
            if (Vector3.ProjectOnPlane(body.Forward, up).sqrMagnitude > 1e-6f)
                return up;

            Vector3 right = new Vector3(up.y, -up.x, 0f);
            return right.sqrMagnitude > 1e-6f ? Vector3.Cross(right.normalized, up) : up;
        }

        /// <summary>
        /// ViewOf the agent's body; an agent without one sees over the world's ground (Y up) along its transform's
        /// forward.
        /// </summary>
        public static Vector3 ViewOf(AgentContext context, out Vector3 look)
        {
            if (context?.Actuator != null)
                return ViewOf(context.Actuator.Body, out look);

            Transform self = context?.Agent != null ? context.Agent.EntityRoot : context?.Self;
            look = self != null ? Vector3.ProjectOnPlane(self.forward, Vector3.up) : Vector3.zero;
            look = look.sqrMagnitude > 1e-6f ? look.normalized : Vector3.zero;
            return Vector3.up;
        }

        private Vector3 AimOf(Transform target)
        {
            if (target.TryGetComponent(out IPerceivable perceivable))
                return perceivable.AimPoint;

            return Scanner is IAimProbe aim ? aim.AimPoint(target) : target.position;
        }

        // ---------- who counts ----------

        private bool IsTarget(Transform candidate)
        {
            if (candidate == null || (_context.Agent != null && candidate == _context.Agent.EntityRoot))
                return false;

            if (!IsPerceivable(candidate))
                return false;

            int faction = _context.Agent != null ? _context.Agent.Faction : 0;
            if (candidate.TryGetComponent(out IPerceivable perceivable) && perceivable.Faction == faction && faction != 0)
                return false;

            return Filter == null || Filter.Accepts(candidate);
        }

        private bool IsPerceivable(Transform candidate) =>
            (!candidate.TryGetComponent(out IPerceivable perceivable) || perceivable.IsPerceivable) &&
            (Filter == null || Filter.Accepts(candidate));

        private PerceivedTarget Find(Transform target)
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                if (_tracks[i].Transform == target)
                    return _tracks[i];
            }

            return null;
        }

        // The track of a target, made when there is none; null — no room for it. A newcomer takes the place of the weakest
        // only when it comes in stronger (awareness: what it will have): one more face in a crowd matters less than the
        // ones we know, and an equal one waits (handbook §9б.2 p.5 — one interest at a time, an equal one is ignored)
        private PerceivedTarget TrackOf(Transform target, float awareness)
        {
            PerceivedTarget track = Find(target);
            if (track != null)
                return track;

            if (_tracks.Count >= MAX_TRACKS)
            {
                int weakest = 0;
                for (int i = 1; i < _tracks.Count; i++)
                {
                    if (_tracks[i].Awareness < _tracks[weakest].Awareness)
                        weakest = i;
                }

                if (_tracks[weakest].Awareness >= awareness)
                    return null;

                _tracks.RemoveAt(weakest);
            }

            track = new PerceivedTarget
            {
                Transform = target,
                LastKnownPosition = target.position,
                TimeSinceSeen = float.PositiveInfinity
            };
            _tracks.Add(track);
            return track;
        }

        // ---------- the one interest ----------

        private void Publish()
        {
            // The most aware of; a tie goes to the one in sight, then to the nearer
            Best = null;
            Vector3 here = _context.Position;
            for (int i = 0; i < _tracks.Count; i++)
            {
                PerceivedTarget track = _tracks[i];
                if (Best == null || track.Awareness > Best.Awareness + 1e-4f)
                {
                    Best = track;
                    continue;
                }

                if (track.Awareness < Best.Awareness - 1e-4f)
                    continue;

                if (track.Visible != Best.Visible)
                {
                    if (track.Visible)
                        Best = track;
                    continue;
                }

                if ((track.LastKnownPosition - here).sqrMagnitude < (Best.LastKnownPosition - here).sqrMagnitude)
                    Best = track;
            }

            TargetTrack trackOut = _context.Track;
            if (Best == null)
            {
                if (trackOut.Subject.IsSet)
                    trackOut.Drop();
                trackOut.Awareness = 0f;
                Level = AlertLevel.Idle;
                return;
            }

            bool seen = Best.Visible && Best.Awareness >= Profile.NoticeThreshold;
            if (seen)
            {
                if (!trackOut.Subject.Is(Best.Transform))
                    trackOut.Set(Best.Transform, Best.Awareness);
                trackOut.Awareness = Best.Awareness;
            }
            else
            {
                if (trackOut.Subject.IsSet)
                    trackOut.Drop();
                trackOut.Awareness = Best.Awareness;
                trackOut.LastKnownPosition = Best.LastKnownPosition;
            }

            Level = NextLevel(seen);
        }

        // Hysteresis: Aware holds until awareness falls below the notice threshold; lost sight with memory — Searching
        private AlertLevel NextLevel(bool seen)
        {
            float awareness = Best.Awareness;
            if (seen && (awareness >= Profile.AwareThreshold ||
                         (Level == AlertLevel.Aware && awareness >= Profile.NoticeThreshold)))
                return AlertLevel.Aware;

            if (!seen && (Level == AlertLevel.Aware || Level == AlertLevel.Searching) && HasMemory)
                return AlertLevel.Searching;

            if (awareness >= Profile.NoticeThreshold)
                return AlertLevel.Suspicious;

            // Once suspicious (a glimpse, a shout heard) it stays so while it remembers the place: it investigates
            return Level == AlertLevel.Suspicious && HasMemory && awareness > 0f ? AlertLevel.Suspicious : AlertLevel.Idle;
        }
    }
}

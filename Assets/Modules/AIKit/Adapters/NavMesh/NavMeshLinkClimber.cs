using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// A body that climbs walls: its paths may run over the links of the "Climb" area (a NavMeshLink from the foot of a
    /// wall to its top) while the graph allows it, and when the agent comes to such a link the body climbs it — up, or
    /// down when the link is walked from the top — instead of sliding along a straight line (handbook §13.3: an edge is
    /// executed, not driven to). The way the body moves over the wall is the climbing clip's: how high it is at each
    /// moment and how far it has come forward, read from the clip (§20.3), so the hands and the feet stay on the wall.
    /// Bodies climb one wall at once, but Climb Spacing apart at the least (§13.4: two capsules on one narrow edge read
    /// as a fault): one that comes nearer to another on the wall waits at its foot. Unity's own one-agent-per-link is
    /// let go of as the climb starts.
    /// </summary>
    public sealed class NavMeshLinkClimber
    {
        /// <summary>The index of the NavMesh area "Climb" (Project Settings → Navigation → Areas): the links only climbers take.</summary>
        public const int CLIMB_AREA = 3;

        /// <summary>Seconds the body takes to turn its face to the wall when it starts.</summary>
        private const float TURN_TIME = 0.2f;

        // The bodies on walls now: one does not start nearer than Climb Spacing to another
        private static readonly List<NavMeshLinkClimber> OnWalls = new();

        private readonly Transform _owner;
        private readonly NavMeshAgent _agent;
        private readonly NavMeshBody _settings;
        private Vector3 _bottom;
        private Vector3 _top;
        private Vector3 _to;
        private Quaternion _turnFrom;
        private Quaternion _face;
        private Vector3 _wallAt;    // the face of the wall over the ground, y = 0
        private Vector3 _start;     // where the clip starts over the ground (at the foot of the wall), y = 0
        private Vector3 _end;       // where it ends (on the top), y = 0
        private float _stepIn;      // seconds of steps to the wall before the clip
        private float _stepOut;     // seconds of steps off the wall after it
        private float _total;
        private float _duration;    // the clip's seconds; down by a jump — take-off, flight and landing
        private float _flight;      // seconds in the air of a jump down
        private float _overEdge;    // seconds of it the body steps over the edge, before it falls
        private bool _dropping;     // this way down is a jump
        private float _time;
        private bool _allowed;
        private ClimbMotion _climb;  // the climbing clip, as measured (none: the plain way — Reach 0, even curves)
        private float _takeOff;     // seconds of the jump's take-off, as measured (none: 0)
        private float _landing;     // seconds of standing up where it landed, as measured (none: 0)

        public NavMeshLinkClimber(Transform owner, NavMeshAgent agent, NavMeshBody settings)
        {
            _owner = owner;
            _agent = agent;
            _settings = settings;
            _agent.autoTraverseOffMeshLink = false;     // the climb is ours
            Allowed = false;
        }

        /// <summary>1 — on a wall going up, −1 — going down, 0 — not climbing.</summary>
        public int Direction { get; private set; }

        public bool IsClimbing => Direction != 0;

        /// <summary>
        /// The clip plays now (1 up, −1 down, 0 not): on a climb the body first steps from the end of the link to the
        /// wall and at last from the wall to the link's other end — a walk, not the clip.
        /// </summary>
        public int ClipDirection => IsClimbing && !_dropping && _time >= _stepIn && _time < _stepIn + _duration ? Direction : 0;

        /// <summary>The body is on its way down by a jump (Climbs Down By Jumping), from the steps to the edge to the landing.</summary>
        public bool IsDropping => IsClimbing && _dropping;

        /// <summary>Where in the jump down the body is: none while it steps to the edge, then take-off, the air, landing.</summary>
        public JumpPhase JumpPhase
        {
            get
            {
                if (!IsDropping || _time < _stepIn)
                    return JumpPhase.None;

                float since = _time - _stepIn;
                if (since < _takeOff)
                    return JumpPhase.TakeOff;

                return since < _takeOff + _flight ? JumpPhase.InAir : JumpPhase.Landing;
            }
        }

        /// <summary>How many walls the body has climbed (debugging and tests).</summary>
        public int Climbs { get; private set; }

        /// <summary>
        /// On — the climbing walls are open to the paths of this body (the area mask of the agent). Closed by default:
        /// the graph says when the character climbs, and only then do its paths run over walls.
        /// </summary>
        public bool Allowed
        {
            get => _allowed;
            set
            {
                _allowed = value;
                if (_agent == null)
                    return;

                if (value)
                {
                    _agent.areaMask |= 1 << CLIMB_AREA;
                }
                else
                {
                    _agent.areaMask &= ~(1 << CLIMB_AREA);
                    LeaveTheFoot();                     // waiting at the foot of a wall shut now: it does not climb it
                }
            }
        }

        // A body waiting on a climbing link (at the foot of a wall, behind another climber) whose walls are shut steps off
        // the link where it stands: nothing else takes an agent off a link, and the wall would be climbed once free
        private void LeaveTheFoot()
        {
            if (IsClimbing || _agent == null || !_agent.isActiveAndEnabled || !_agent.isOnOffMeshLink || !IsClimbLink())
                return;

            _agent.Warp(_owner.position);
            _waiting = false;
        }

        // The area of the link the agent stands on, read once per link (the property read boxes the number)
        private Object _areaOwner;
        private int _area = -1;

        private int AreaOfLink()
        {
            if (_agent == null || !_agent.isOnOffMeshLink)
                return -1;

            Object owner = _agent.currentOffMeshLinkData.owner;
            if (owner == null)
                return -1;

            if (!ReferenceEquals(owner, _areaOwner))
            {
                _areaOwner = owner;
                _area = AreaOfCurrentLink(_agent);
            }

            return _area;
        }

        /// <summary>The agent stands on a link of the Climb area (or on a link with no area to tell, when the body does not jump).</summary>
        public bool IsClimbLink()
        {
            int area = AreaOfLink();
            return area == CLIMB_AREA || area < 0 && !_settings.JumpsOverLinks;
        }

        public void FrameTick(float dt)
        {
            if (_agent == null)
                return;

            // A link that is no wall (of another area) on a body that does not jump: crossed the plain way, as the agent
            // itself would — the climber took the agent's own crossing over for every link
            if (_crossing || !IsClimbing && _agent.isActiveAndEnabled && _agent.isOnOffMeshLink &&
                _settings.Jumper == null && !IsClimbLink())
            {
                Cross(dt);
                return;
            }

            // On a wall the agent is off (see TryBegin): the climb goes on without it
            if (!IsClimbing && (!_agent.isActiveAndEnabled || !TryBegin()))
                return;

            _time += dt;
            if (!_agent.enabled)                        // back on its feet (a landing): the agent holds the body
                _owner.position = PositionAt(Mathf.Min(_time, _total));
            _owner.rotation = Quaternion.Slerp(_turnFrom, _face, Mathf.Clamp01(_time / TURN_TIME));
            if (JumpPhase == JumpPhase.Landing)
                AgentBack();                            // feet down: the others see it and walk round it while it stands up
            if (_time < _total)
                return;

            Finish();
        }

        /// <summary>
        /// The character is switched off: a climb under way ends at once at the end it went to, so that switching the
        /// character on again does not resume a stale climb.
        /// </summary>
        public void Abort()
        {
            if (_crossing)
            {
                _owner.position = _crossTo;
                EndCrossing();
            }

            if (!IsClimbing)
                return;

            _owner.position = _to;
            Finish();
        }

        // ---------- a link that is no wall, on a body that does not jump: walked over ----------

        private bool _crossing;
        private Vector3 _crossFrom;
        private Vector3 _crossTo;
        private float _crossTime;
        private float _crossDuration;

        // Over the link from where the body stands to its far end at the body's own speed, then the link is done (what the
        // agent's own crossing does)
        private void Cross(float dt)
        {
            if (!_crossing)
            {
                _crossing = true;
                _crossFrom = _owner.position;
                _crossTo = _agent.currentOffMeshLinkData.endPos + Vector3.up * _agent.baseOffset;
                _crossTime = 0f;
                _crossDuration = Vector3.Distance(_crossFrom, _crossTo) / Mathf.Max(0.1f, _agent.speed);
            }

            _crossTime += dt;
            float along = _crossDuration > 1e-4f ? Mathf.Clamp01(_crossTime / _crossDuration) : 1f;
            _owner.position = Vector3.Lerp(_crossFrom, _crossTo, along);
            if (along >= 1f)
                EndCrossing();
        }

        private void EndCrossing()
        {
            _crossing = false;
            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnOffMeshLink)
                _agent.CompleteOffMeshLink();
        }

        // ---------- waiting at the foot ----------

        // What the wall is, worked out once while the body waits at its foot (a ray and a look along the wall a frame
        // were garbage at the frame rate): for this link and this place of the body
        private bool _waiting;
        private Object _waitLink;
        private Vector3 _waitFrom;
        private Vector3 _waitWallAt;
        private Vector3 _waitFlat;

        // On a climbing link: the climb starts from where the body stands
        private bool TryBegin()
        {
            if (!_agent.isOnOffMeshLink || !IsClimbLink())
                return false;

            if (!_allowed)
            {
                LeaveTheFoot();                         // the walls were shut while it waited: not climbed
                return false;
            }

            OffMeshLinkData link = _agent.currentOffMeshLinkData;
            Vector3 from = _owner.position;
            Object owner = link.owner;
            if (!_waiting || !ReferenceEquals(owner, _waitLink) || (from - _waitFrom).sqrMagnitude > 1e-4f)
            {
                // The wall: which way the link crosses it, and its face. Not from where the body stands — it steps onto a
                // link anywhere near its end.
                Vector3 lower = link.startPos.y <= link.endPos.y ? link.startPos : link.endPos;
                Vector3 upper = link.startPos.y <= link.endPos.y ? link.endPos : link.startPos;
                Vector3 flatWay = Flat(upper - lower);
                Vector3 face = WallFace(lower, flatWay, out bool found, out Vector3 square);
                // Straight at the face: the ends Unity gives an agent on a wide link lean toward where it goes, and a
                // climb along them ran slantwise, sliding along the wall
                if (found && Vector3.Dot(flatWay, square) > 0.01f)
                    flatWay = square * Vector3.Dot(flatWay, square);
                // Along the wall — where the body stands: the crowd brings a body onto a link off its middle (round one
                // who waits there), and a climb from the link's own point slid it up to a metre along the wall to grab it
                face += AlongTheWall(face, lower, flatWay, from);
                _waiting = true;
                _waitLink = owner;
                _waitFrom = from;
                _waitWallAt = face;
                _waitFlat = flatWay;
            }

            Vector3 wallAt = _waitWallAt;
            Vector3 flat = _waitFlat;
            if (SomebodyNear(wallAt))
                return false;                           // too near one on the wall: wait at the foot (§13.4)

            _waiting = false;
            _wallAt = wallAt;
            _to = Across(link.endPos, wallAt, flat) + Vector3.up * _agent.baseOffset;
            Direction = _to.y >= from.y ? 1 : -1;
            _bottom = Direction > 0 ? from : _to;
            _top = Direction > 0 ? _to : from;
            if (flat.sqrMagnitude < 1e-4f)
                flat = Flat(_top - _bottom);

            // The way over the wall is the clips' (measured in the editor, step 90, C20); none measured — the plain way
            ITraversalMotion motion = _settings.Motion;
            bool clip = Direction > 0 ? motion.TryClimbUp(out _climb) : motion.TryClimbDown(out _climb) ||
                                                                         motion.TryClimbUp(out _climb);
            if (!clip)
                _climb = default;
            _takeOff = _landing = 0f;
            if (motion.TryJump(out JumpMotion jump))
            {
                _takeOff = Mathf.Max(0f, jump.TakeOff);
                _landing = Mathf.Max(0f, jump.Landing);
            }

            // It comes off the wall beyond the link's far end, not on it: the next one waits right there, at its foot
            // or at the edge, and two agents on one spot are shoved apart by the crowd — up, at least a body's width past
            // the end (and never back from where the clip ends); down, a body's width further from the wall
            if (flat.sqrMagnitude > 1e-4f)
            {
                Vector3 away = flat.normalized;         // from the foot of the wall to its top
                float clear = 2f * _agent.radius;
                if (Direction > 0)
                {
                    float along = Vector3.Dot(Flat(_top) - _wallAt, away);
                    float clipEnds = _climb.Reach > 0.01f ? _climb.Reach - _climb.FromWall : 0f;
                    _to = _top = Arrive(_top, away * (Mathf.Max(along + clear, clipEnds) - along));
                }
                else
                {
                    _to = _bottom = Arrive(_bottom, -away * clear);
                }
            }

            // The body faces the wall both ways: up it looks where it climbs, down it backs over the edge
            _turnFrom = _owner.rotation;
            _face = flat.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : _owner.rotation;

            // The clip's time; none measured — up the wall at the body's own speed
            float speed = Mathf.Max(0.1f, _agent.speed);
            _duration = _climb.Time > 0.01f ? _climb.Time : Mathf.Max(0.2f, Mathf.Abs(_top.y - _bottom.y) / speed);
            // Where the clip starts and ends (at the wall), and the steps to it and from it at the body's own pace
            _start = _end = Vector3.zero;
            _stepIn = _stepOut = 0f;
            if (_climb.Reach > 0.01f && flat.sqrMagnitude > 1e-4f)
            {
                Vector3 direction = flat.normalized;
                _start = _wallAt - direction * _climb.FromWall;           // the clip starts this far short of the wall
                _end = _start + direction * _climb.Reach;
                float below = Vector3.Distance(Flat(_bottom), _start) / speed;
                float above = Vector3.Distance(_end, Flat(_top)) / speed;
                _stepIn = Direction > 0 ? below : above;
                _stepOut = Direction > 0 ? above : below;
            }

            // Down by a jump (handbook §13.3, a fall edge: ballistic, done when landed below): steps to the edge facing
            // out, takes off, falls under gravity from the edge to the link's foot end and lands there (the user's word,
            // C20: the fall is gravity's — nothing speeds it up, nothing throws the body up)
            _dropping = Direction < 0 && _settings.ClimbsDownByJumping && flat.sqrMagnitude > 1e-4f;
            if (_dropping)
            {
                Vector3 direction = flat.normalized;   // from the foot of the wall to its top
                _start = _wallAt + direction * NavMeshBody.JUMP_FROM_EDGE;      // the take-off, at the edge
                _end = Flat(_bottom);                                           // where it lands
                _face = Quaternion.LookRotation(-direction, Vector3.up);
                _stepIn = Vector3.Distance(Flat(_top), _start) / speed;
                _stepOut = 0f;
                // It steps off: over the ground evenly, on the top until its middle is past the edge, then falling
                float fall = Ballistics.FallTime(_top.y - _bottom.y, 0f, NavMeshBody.Gravity);
                float across = Vector3.Distance(_start, _end);
                float onTop = Mathf.Min(NavMeshBody.JUMP_FROM_EDGE, across * 0.5f);
                _overEdge = across > onTop ? fall * onTop / (across - onTop) : 0f;
                _flight = _overEdge + fall;
                _duration = _takeOff + _flight + _landing;
            }

            // It faces the wall before the clip starts: a body at its place already (no steps in) turned on the wall,
            // and the hands, half a metre in front of it, swept along the edge — it turns where it stands first
            if ((_climb.Reach > 0.01f || _dropping) && Quaternion.Angle(_turnFrom, _face) > 1f)
                _stepIn = Mathf.Max(_stepIn, TURN_TIME);

            _total = _stepIn + _duration + _stepOut;
            _time = 0f;
            Climbs++;
            OnWalls.Add(this);

            // The agent is switched off for the climb: Unity lets one agent on a link at a time (the next one waited at
            // its foot), and an agent left at either end of the link stood in somebody's place — the crowd shoved the
            // two apart, a body jumped 0.6–0.9 m in a frame. Off, it is nowhere; it comes back where the climb ends,
            // and the path is planned anew from there.
            Vector3 stays = _owner.position;
            _agent.CompleteOffMeshLink();
            _agent.enabled = false;
            _owner.position = stays;                    // completing the link put the body at its far end
            return true;
        }

        /// <summary>Metres above the foot of the wall its face is looked for at (below any ledge, above the ground's bumps).</summary>
        private const float FACE_HEIGHT = 0.5f;

        // What the ray toward a wall's face hits (made once: no array a look)
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// The face of the wall over the ground (y = 0): a ray from the link's lower end at FACE_HEIGHT toward its upper
        /// end, the first solid face turned to it that is not a character. The link's ends need not lie the same way
        /// off the wall (the foot end lies where the clip starts, further out). None — halfway between the ends. Square —
        /// the way straight into the face over the ground (the ray's own way when there is none).
        /// </summary>
        private Vector3 WallFace(Vector3 lower, Vector3 flat, out bool found, out Vector3 square)
        {
            Vector3 middle = Flat(lower + flat * 0.5f);
            found = false;
            square = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.zero;
            if (flat.sqrMagnitude < 1e-4f)
                return middle;

            Vector3 way = flat.normalized;
            var ray = new Ray(lower + Vector3.up * FACE_HEIGHT, way);
            int count = Physics.RaycastNonAlloc(ray, Hits, flat.magnitude + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 face = middle;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.distance >= nearest || Vector3.Dot(hit.normal, way) > -0.5f ||
                    hit.collider.GetComponentInParent<NavMeshAgent>() != null)
                    continue;

                nearest = hit.distance;
                face = Flat(hit.point);
                found = true;
                Vector3 into = -Flat(hit.normal);
                if (into.sqrMagnitude > 1e-4f)
                    square = into.normalized;
            }

            return face;
        }

        /// <summary>Metres the face found beside the link may lie nearer or further than at the link (the same wall).</summary>
        private const float SAME_FACE = 0.2f;

        /// <summary>
        /// How far along the wall from the link's point the body stands (over the ground): the wall's face is looked
        /// for there too, and the climb goes there only when the same wall is there — none (no wall, a corner, another
        /// face) — the link's point.
        /// </summary>
        private Vector3 AlongTheWall(Vector3 wallAt, Vector3 lower, Vector3 flat, Vector3 standing)
        {
            if (flat.sqrMagnitude < 1e-4f)
                return Vector3.zero;

            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
            Vector3 aside = side * Vector3.Dot(Flat(standing) - wallAt, side);
            if (aside.sqrMagnitude < 1e-4f)
                return Vector3.zero;

            Vector3 there = WallFace(lower + aside, flat, out bool found, out _);
            float depth = Vector3.Dot(there - (wallAt + aside), flat.normalized);
            return found && Mathf.Abs(depth) < SAME_FACE ? aside : Vector3.zero;
        }

        // The link's far end moved along the wall to lie straight across it from where the body climbs, when there is
        // walkable ground there; the end itself when there is not
        private static Vector3 Across(Vector3 end, Vector3 wallAt, Vector3 flat)
        {
            if (flat.sqrMagnitude < 1e-4f)
                return end;

            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
            Vector3 aside = side * Vector3.Dot(wallAt - Flat(end), side);
            if (aside.sqrMagnitude < 1e-4f)
                return end;

            return NavMesh.SamplePosition(end + aside, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) &&
                   Mathf.Abs(hit.position.y - end.y) < 0.3f
                ? hit.position
                : end;
        }

        /// <summary>Metres aside a place to come off the wall is looked for when somebody stands where it would be.</summary>
        private static readonly float[] ASIDE = { 0f, 0.8f, -0.8f, 1.6f, -1.6f };

        // A point this far on from where the link ends, on the walkable ground there and where nobody stands (aside of
        // it when somebody does: the crowd shoves two bodies on one spot apart); the link's end when there is no ground
        private Vector3 Arrive(Vector3 end, Vector3 shift)
        {
            Vector3 ground = end - Vector3.up * _agent.baseOffset;
            Vector3 side = Vector3.Cross(Vector3.up, Flat(shift)).normalized;
            Vector3 onGround = end;
            bool found = false;
            foreach (float aside in ASIDE)
            {
                if (!NavMesh.SamplePosition(ground + shift + side * aside, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) ||
                    Mathf.Abs(hit.position.y - ground.y) >= 0.3f)
                    continue;

                Vector3 place = hit.position + Vector3.up * _agent.baseOffset;
                if (NavMeshMover.IsFree(place, _agent.radius, _agent))
                    return place;

                if (!found)
                    onGround = place;                   // nowhere free: straight on, as before
                found = true;
            }

            return onGround;
        }

        private void Finish()
        {
            Direction = 0;
            OnWalls.Remove(this);
            if (_agent == null || _agent.enabled)
                return;                                 // the agent was back already (a landing): it holds the body where it is

            _owner.position = _to;
            AgentBack();
        }

        // The agent switched on again where the body stands: the climb's end (once)
        private void AgentBack()
        {
            if (_agent.enabled)
                return;

            _agent.enabled = true;
            if (_agent.isActiveAndEnabled)
                _agent.Warp(_to);
        }

        // Somebody climbs or jumps nearer than Climb Spacing to this point of a wall (the bodies gone are forgotten)
        private bool SomebodyNear(Vector3 wallAt)
        {
            const float spacing = NavMeshBody.CLIMB_SPACING;
            OnWalls.RemoveAll(other => other._agent == null || !other.IsClimbing);

            foreach (NavMeshLinkClimber other in OnWalls)
            {
                if (other != this && (other._wallAt - wallAt).sqrMagnitude < spacing * spacing)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Where the body is this many seconds into the climb. With a measured clip (its Reach): the body first steps at
        /// its own speed from its end of the link to where the clip starts (From Wall short of the wall's face) or, going
        /// down, ends; then the clip: the height its Rise of the wall's height, Reach forward as its Advance — played back
        /// going down; then it steps on to the link's other end. Without one: evenly from one end of the link to the
        /// other over the climb's time.
        /// </summary>
        private Vector3 PositionAt(float time)
        {
            if (_dropping)
                return DropAt(time);

            float height = _top.y - _bottom.y;
            Vector3 flat = _top - _bottom;
            flat.y = 0f;
            if (_climb.Reach <= 0.01f || flat.sqrMagnitude < 1e-4f)
            {
                float t = Mathf.Clamp01(time / _duration);
                float along = Direction > 0 ? t : 1f - t;
                return _bottom + flat * _climb.AdvanceAt(along) + Vector3.up * (height * _climb.RiseAt(along));
            }

            bool up = Direction > 0;
            Vector3 from = up ? _bottom : _top;
            Vector3 to = up ? _top : _bottom;
            if (time < _stepIn)                         // steps to the wall, on the ground it stands on
                return At(Vector3.Lerp(Flat(from), up ? _start : _end, time / _stepIn), from.y);

            if (time >= _stepIn + _duration)            // steps off the wall, on the ground it came to
            {
                float off = _stepOut > 0f ? Mathf.Clamp01((time - _stepIn - _duration) / _stepOut) : 1f;
                return At(Vector3.Lerp(up ? _end : _start, Flat(to), off), to.y);
            }

            float clip = Mathf.Clamp01((time - _stepIn) / _duration);
            float at = up ? clip : 1f - clip;           // where on the clip: the way down is the way up played back
            Vector3 over = Vector3.Lerp(_start, _end, _climb.AdvanceAt(at));
            return At(over, _bottom.y + height * _climb.RiseAt(at));
        }

        /// <summary>
        /// Down by a jump, this many seconds in: steps on the top to the edge; stands there taking off; in the air — over
        /// the ground evenly from the edge to the link's foot end, on the top until its middle is past the edge, then
        /// falling under gravity (Physics.gravity: it steps off, nothing throws it up); then stands where it landed.
        /// </summary>
        private Vector3 DropAt(float time)
        {
            if (time < _stepIn)
                return At(Vector3.Lerp(Flat(_top), _start, time / _stepIn), _top.y);

            float flying = time - _stepIn - _takeOff;
            if (flying <= 0f)
                return At(_start, _top.y);
            if (flying >= _flight)
                return At(_end, _bottom.y);

            float y = flying <= _overEdge
                ? _top.y                                // still over the top: it steps off the edge
                : Ballistics.HeightAt(_top.y, 0f, NavMeshBody.Gravity, flying - _overEdge);
            return At(Vector3.Lerp(_start, _end, flying / _flight), Mathf.Max(_bottom.y, y));
        }

        private static Vector3 Flat(Vector3 point) => new(point.x, 0f, point.z);

        private static Vector3 At(Vector3 flat, float y) => new(flat.x, y, flat.z);

        // ---------- which area a link is of: the link's owner is a component of the AI Navigation package ----------

        private static readonly Dictionary<System.Type, PropertyInfo> AreaProperties = new();

        /// <summary>
        /// The area of the link the agent stands on, read from the owner of the link (NavMeshLink.area: the adapter
        /// does not reference the package, so the property is looked up by name once per type); −1 — not known.
        /// </summary>
        internal static int AreaOfCurrentLink(NavMeshAgent agent)
        {
            if (agent == null || !agent.isOnOffMeshLink)
                return -1;

            Object owner = agent.currentOffMeshLinkData.owner;
            if (owner == null)
                return -1;

            System.Type type = owner.GetType();
            if (!AreaProperties.TryGetValue(type, out PropertyInfo property))
            {
                property = type.GetProperty("area", BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.PropertyType != typeof(int))
                    property = null;
                AreaProperties[type] = property;
            }

            return property != null ? (int)property.GetValue(owner) : -1;
        }
    }
}

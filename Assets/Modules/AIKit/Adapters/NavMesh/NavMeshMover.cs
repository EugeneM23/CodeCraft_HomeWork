using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// The "virtual gamepad" of a NavMeshAgent — the 3D counterpart of CharacterBodyActuator.
    /// Paths are the pathing's job (it sets the destination and the agent walks it with its own avoidance); this part
    /// handles what the graph presses by hand: nudging in a direction (keeping distance, strafing), turning, and
    /// stopping. A manual nudge cancels the current path, so the two never fight. Reads its numbers from the
    /// NavMeshBody live.
    /// A hand-held step is given to the agent as a speed to walk at, not as a jump of the transform: the agent then
    /// carries the body itself and its local avoidance still holds — bodies that strafe round one target step aside
    /// instead of standing inside each other and shivering (handbook §14.2).
    /// </summary>
    public sealed class NavMeshMover : IAgentActuator, IAgentNumbers, IDashActuator, IClipAimActuator, IWalkProbe
    {
        private const float DEADZONE = 1e-4f;

        /// <summary>Degrees a hand-held step may be turned aside to let somebody past; never further, never backwards.</summary>
        private const float GIVE_WAY = 60f;

        // The body's numbers (IAgentNumbers): the character's status reads them (Get), Set Parameter changes them (step 90)
        public const string TURN_SPEED = "Turn Speed";
        public const string ACCELERATION = "Acceleration";

        /// <summary>The ramp distance (NavMeshBody.RampDistance) as a number of the body: Set Parameter multiplies it (step 90).</summary>
        public const string RAMP_DISTANCE = "Ramp Distance";

        private readonly Transform _owner;
        private readonly NavMeshAgent _agent;
        private readonly NavMeshBody _settings;
        private Vector3 _move;
        private Vector3 _facing;
        private float _speedScale = 1f;
        private bool _hasFacing;
        private bool _drives;
        private readonly BodyGlide _glide = new();

        // The speed given to the agent by hand the step before (GiveWay measures the crowd's word against it): only when
        // the step before gave one
        private Vector3 _given;
        private bool _gave;

        // The speed the glide speeds up to or brakes from (the step asked, or the path's): Ramp Distance is measured on it
        private float _rampSpeed;

        // Every NavMesh body's mover: a clip's step stops at the others' bodies (see MoveByAnimation)
        private static readonly List<NavMeshMover> All = new();

        public NavMeshMover(Transform owner, NavMeshAgent agent, NavMeshBody settings)
        {
            _owner = owner;
            _agent = agent;
            _settings = settings;
            All.RemoveAll(other => other._owner == null);
            All.Add(this);
        }

        /// <summary>
        /// Nobody else's body (its agent switched on) is nearer than radius + its own radius to this point, flat: a place
        /// to put a body down without the crowd shoving the two apart.
        /// </summary>
        public static bool IsFree(Vector3 at, float radius, NavMeshAgent self)
        {
            for (int i = 0; i < All.Count; i++)
            {
                NavMeshAgent other = All[i]._agent;
                if (other == null || other == self || All[i]._owner == null || !other.isActiveAndEnabled)
                    continue;

                Vector3 apart = Flatten(All[i]._owner.position - at);
                float closest = radius + other.radius;
                if (apart.sqrMagnitude < closest * closest)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// A clip's step stopped at the bodies near it (their agents switched on): agent.Move walks through them, and the
        /// crowd then shoves the two apart in one frame — the soak test saw fighters jump 0.7 m.
        /// </summary>
        private Vector3 KeptOffOthers(Vector3 step, Vector3 from)
        {
            for (int i = 0; i < All.Count; i++)
            {
                NavMeshMover other = All[i];
                if (other == this || other._owner == null || other._agent == null || !other._agent.isActiveAndEnabled)
                    continue;

                Vector3 to = Flatten(other._owner.position - from);
                float closest = _agent.radius + other._agent.radius;
                if (to.sqrMagnitude < (closest + 1f) * (closest + 1f))
                    step = KeptOff(step, to, closest);
            }

            return step;
        }

        public AgentBodyInfo Body
        {
            get
            {
                if (_agent == null)
                    return AgentBodyInfo.Default;

                return new AgentBodyInfo
                {
                    Position = _owner.position,
                    Velocity = _agent.isActiveAndEnabled ? _agent.velocity : Vector3.zero,   // off on a wall
                    Up = Vector3.up,
                    IsGrounded = !_settings.IsTraversing && !(_agent.isActiveAndEnabled && _agent.isOnOffMeshLink),
                    Facing = 1,
                    Forward = Flatten(_owner.forward).normalized,    // where it is turned: the senses' cones open there
                    Radius = _agent.radius,
                    Height = _agent.height,
                    MaxSpeed = _agent.speed,
                    JumpHeight = 0f,
                    JumpReach = 0f,
                    CanJump = false,
                    CanFly = false,
                    CanClimb = _settings.ClimbsWalls
                };
            }
        }

        public void Move(Vector3 direction, float speedScale = 1f)
        {
            _move = Flatten(direction);
            _speedScale = Mathf.Clamp01(speedScale);
        }

        public void Face(Vector3 direction)
        {
            Vector3 flat = Flatten(direction);
            if (flat.sqrMagnitude <= DEADZONE)
                return;

            _facing = flat.normalized;
            _hasFacing = true;
        }

        /// <summary>A NavMesh body does not climb by input; the walls are climbed over their links (NavMeshLinkClimber).</summary>
        public void SetVertical(float axis)
        {
        }

        /// <summary>A NavMesh body does not jump by input; links are traversed by the pathing.</summary>
        public void Jump(float holdTime)
        {
        }

        public float HoldTimeForHeight(float rise) => 0f;

        public void Crouch(bool crouch)
        {
        }

        public void DropThrough()
        {
        }

        /// <summary>
        /// Nothing pressed and no path any more: the body brakes with its acceleration (BodyGlide) instead of standing
        /// still on the spot — a walk's stop clip needs the time to put the feet down.
        /// </summary>
        public void Stop()
        {
            _move = Vector3.zero;
            _speedScale = 1f;
            // A path still being planned is dropped too: asked and left in one step, it was walked once it came
            if (IsUsable && (_agent.hasPath || _agent.pathPending))
                _agent.ResetPath();              // the glide carries the body down from the speed it has
        }

        /// <summary>The body was put somewhere (a jump over a link, a teleport): no speed carries over.</summary>
        public void Halt()
        {
            _glide.Halt();
            _drives = false;
            _gave = false;
            _dashLeft = 0f;                      // a dash does not go on from where the body was put
            _byClip = false;
            EndClipTurn();
        }

        // ---------- root motion of actions: a blow, a turn, a hit moves the body as its clip does ----------

        /// <summary>
        /// The part of its clip's own turn an aimed turn is done within. The blends into the clip and out of it weigh
        /// down its first and last degrees (a 90° step turn turns the swordsman 84°), so the aim is reached a little
        /// before the clip's turn is; what the clip turns after that, the feet settle on the spot.
        /// </summary>
        public const float AIM_WITHIN = 0.9f;

        private bool _byClip;
        private Transform _turnTarget;
        private float _turnClipAngle;
        private float _turnClipDone;
        private Transform _keepOff;
        private bool _aimShown;         // the aimed action has been seen playing: it ends when its clip is over
        private float _aimWaited;       // seconds the aim has waited for its action to show

        /// <summary>
        /// Seconds an aim (a turn aimed at a target, a step kept off one) waits for its action to show: the presentation
        /// shows what the character asks for in its own frame (step 90: it reads the character, nothing is pushed into
        /// it), so a frame or two may pass. An action that never shows lets go of its aim then — it does not carry into
        /// the next one.
        /// </summary>
        public const float AIM_WAIT = 0.2f;

        /// <summary>An action's clip carries the body now (the last frame of the animator was one).</summary>
        public bool CarriedByClip => _byClip;

        public bool ActionsByClip => _settings != null && (_settings.ActionRootMotion || _settings.RootMotion);

        public void TurnByClipTo(Transform target, float clipAngle)
        {
            _turnTarget = target;
            _turnClipAngle = Mathf.Max(0f, clipAngle);
            _turnClipDone = 0f;
            _aimShown = false;
            _aimWaited = 0f;
        }

        public void KeepClipOff(Transform target)
        {
            _keepOff = target;
            _aimShown = false;
            _aimWaited = 0f;
        }

        public void EndClipTurn()
        {
            _turnTarget = null;
            _turnClipAngle = 0f;
            _turnClipDone = 0f;
            _keepOff = null;
        }

        // No action plays this frame: the aimed one is over — or it has not shown yet, and is waited for a moment
        private void LetGoOfAim(float dt)
        {
            if (_turnTarget == null && _keepOff == null)
                return;

            _aimWaited += dt;
            if (_aimShown || _aimWaited > AIM_WAIT)
                EndClipTurn();
        }

        /// <summary>
        /// Metres of the target's body a clip's step stops short of (on top of this body's radius): the same half a body
        /// the reach of a blow is measured to — the blade still lands, the bodies do not go into each other.
        /// </summary>
        public const float TARGET_HALF_BODY = 0.5f;

        /// <summary>
        /// A clip's step cut short so that it ends no closer to the target than closest (flat, centre to centre): only the
        /// part of the step toward the target is cut — a step sideways or away is the clip's own.
        /// </summary>
        public static Vector3 KeptOff(Vector3 step, Vector3 toTarget, float closest)
        {
            float distance = toTarget.magnitude;
            if (distance < 1e-4f)
                return step;

            Vector3 way = toTarget / distance;
            float along = Vector3.Dot(step, way);
            float allowed = Mathf.Max(0f, distance - closest);
            return along > allowed ? step - way * (along - allowed) : step;
        }

        /// <summary>
        /// One frame of the animator (dt seconds). With Root Motion the clips carry the body all the time (the walk too:
        /// CarryByAnimation). With Action Root Motion only while an action plays: the body goes where the root of its
        /// clip goes — kept on the NavMesh by the agent — and turns as the clip turns; a turn aimed at a target
        /// (TurnByClipTo) is stretched to end facing it, a step kept off one (KeepClipOff) stops short of it. Otherwise
        /// (the walk, a dash, root motion off) the agent moves the body as ever. Returns how far the clip moved the body,
        /// flat.
        /// </summary>
        public Vector3 MoveByAnimation(Vector3 deltaPosition, Quaternion deltaRotation, bool inAction, float dt)
        {
            if (CarriedByAnimation)
                return CarryByAnimation(deltaPosition, deltaRotation, inAction, dt);

            _byClip = inAction && ActionsByClip && IsUsable && _dashLeft <= 0f;   // a dash goes its own measured way
            if (!_byClip)
            {
                if (!inAction)
                    LetGoOfAim(dt);              // the turn's clip is over: an aim does not carry into the next action
                return Vector3.zero;
            }

            _aimShown = true;
            Vector3 from = _owner.position;
            Vector3 step = ActionStep(deltaPosition, from);
            if (step.sqrMagnitude > 0f)
                _agent.Move(step);               // the agent keeps the body on the mesh and stands where it stands

            TurnByClip(deltaRotation);
            return Flatten(_owner.position - from);
        }

        // An action's step this frame: the clip's own, kept off the target it was aimed at and off the bodies near it
        private Vector3 ActionStep(Vector3 deltaPosition, Vector3 from)
        {
            Vector3 step = Flatten(deltaPosition);
            if (_keepOff != null)
                step = KeptOff(step, Flatten(_keepOff.position - from), _agent.radius + TARGET_HALF_BODY);
            return KeptOffOthers(step, from);
        }

        // An action's turn this frame: as the clip turns, round the vertical only (no tipping); an aimed one ends facing
        // its target
        private void TurnByClip(Quaternion deltaRotation)
        {
            float raw = Mathf.DeltaAngle(0f, deltaRotation.eulerAngles.y);
            float yaw = raw;
            if (_turnTarget != null && _turnClipAngle > 0f)
            {
                Vector3 to = Flatten(_turnTarget.position - _owner.position);
                float need = to.sqrMagnitude > DEADZONE ? Vector3.SignedAngle(Flatten(_owner.forward), to, Vector3.up) : 0f;
                yaw = AimedTurn(raw, need, _turnClipAngle * AIM_WITHIN - _turnClipDone);
                _turnClipDone += Mathf.Abs(raw);
            }

            if (Mathf.Abs(yaw) > 0.001f)
                _owner.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * _owner.rotation;
        }

        /// <summary>
        /// This frame's share of an aimed turn: the clip turned by raw degrees with clipLeft degrees of its turn still to
        /// come, and the target is need degrees off the nose — the body turns the same part of what it needs as the clip
        /// did of what it had left. So the clip's pace (slow start, fast middle, settling end) is kept, and the turn ends
        /// facing the target however far round it was. The clip's turn done, nothing more.
        /// </summary>
        public static float AimedTurn(float raw, float need, float clipLeft)
        {
            if (clipLeft <= 0.5f)
                return 0f;

            return need * Mathf.Clamp01(Mathf.Abs(raw) / clipLeft);
        }

        // ---------- the dash: exactly this far over exactly this long, past the speed and the speeding up ----------

        private Vector3 _dashVelocity;
        private float _dashLeft;

        public bool IsDashing => _dashLeft > 0f;

        /// <summary>Nothing of the NavMesh's edge in the way, from where the body stands to where the dash ends.</summary>
        public bool CanDash(Vector3 displacement)
        {
            if (!IsUsable)
                return false;

            Vector3 from = _agent.nextPosition;
            return !NavMesh.Raycast(from, from + Flatten(displacement), out _, _agent.areaMask);
        }

        /// <summary>
        /// How far the body can walk straight from a point (IWalkProbe): along the NavMesh it may stand on, up to its edge
        /// (a wall, a drop). A point off the mesh — no way at all.
        /// </summary>
        public float FreeDistance(Vector3 from, Vector3 direction, float maxDistance)
        {
            Vector3 flat = Flatten(direction);
            if (maxDistance <= 0f || flat.sqrMagnitude < 1e-8f)
                return 0f;

            int mask = _agent != null ? _agent.areaMask : NavMesh.AllAreas;
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1f, mask))
                return 0f;

            Vector3 to = start.position + flat.normalized * maxDistance;
            return NavMesh.Raycast(start.position, to, out NavMeshHit hit, mask) ? hit.distance : maxDistance;
        }

        public void Dash(Vector3 displacement, float time)
        {
            if (!IsUsable || time <= 0f)
                return;

            if (_agent.hasPath)
                _agent.ResetPath();
            _dashVelocity = Flatten(displacement) / time;
            _dashLeft = time;
        }

        // The dash carries the body by hand, evenly (the clip's own pace: the feet do not slide); it ends standing —
        // the dodge clip ends in the stance, no speed carries over into a glide
        private void ApplyDash(float dt)
        {
            float step = Mathf.Min(dt, _dashLeft);
            if (!CarriedByAnimation)                // carried by the animation, the dash's way goes in with its frames
                _agent.Move(KeptOffOthers(_dashVelocity * step, _owner.position));
            _agent.velocity = Vector3.zero;
            _dashLeft -= dt;
            _glide.Halt();
            _drives = false;
            _gave = false;
            if (_hasFacing)
            {
                Quaternion wanted = Quaternion.LookRotation(_facing, Vector3.up);
                _owner.rotation = Quaternion.RotateTowards(_owner.rotation, wanted, _settings.TurnSpeed * dt);
                _hasFacing = false;
            }

            _move = Vector3.zero;
        }

        // An action's clip carries the body: the agent does not walk it on top (no glide out of the last step, no path),
        // and it ends standing — the clip ends in the stance. A look asked for still turns it (a blow keeps facing its
        // target); a turn in place asks for none, its clip turns the body
        private void ApplyClip(float dt)
        {
            _agent.velocity = Vector3.zero;
            _glide.Halt();
            _drives = false;
            _gave = false;
            if (_hasFacing)
            {
                Quaternion wanted = Quaternion.LookRotation(_facing, Vector3.up);
                _owner.rotation = Quaternion.RotateTowards(_owner.rotation, wanted, _settings.TurnSpeed * dt);
                _hasFacing = false;
            }

            _move = Vector3.zero;
        }

        public void Apply(float dt)
        {
            if (!IsUsable)
                return;

            if (_settings.IsTraversing)
            {
                // In the air or on a wall the link carries the body and turns it: nothing pressed is taken meanwhile,
                // and it starts from a standstill where the link has put it
                _move = Vector3.zero;
                _hasFacing = false;
                _glide.Halt();
                _drives = false;
                _gave = false;
                return;
            }

            if (_dashLeft > 0f)
            {
                ApplyDash(dt);
                return;
            }

            if (_byClip)
            {
                ApplyClip(dt);
                return;
            }

            // A hand-held direction wins over whatever path was running; either speeds up and brakes with the
            // acceleration (on a path the agent does it itself)
            Vector3? asked = null;
            if (_move.sqrMagnitude > DEADZONE)
            {
                if (_agent.hasPath || _agent.pathPending)
                    _agent.ResetPath();
                float pace = _agent.speed * _speedScale;
                asked = GiveWay(_move.normalized, pace, dt);
                // Up to the step's own pace over Ramp Distance (a wary step starts gently), or down from a faster one
                _rampSpeed = Mathf.Max(pace, _glide.Velocity.magnitude);
            }
            else if (_agent.hasPath)
            {
                _rampSpeed = _agent.speed;           // a path ending brakes from the path's pace
            }

            _glide.Step(asked, _agent.hasPath, _agent.velocity, GlideAcceleration(), dt, out bool handOver);
            bool gave = false;
            if (handOver)
            {
                _agent.velocity = _glide.Velocity;   // a path begins at the speed the body has
                _drives = false;
            }
            else if (!_agent.hasPath && (_glide.Velocity.sqrMagnitude > DEADZONE || _drives))
            {
                // Off a path the agent is given the speed the glide holds and walks it itself, avoidance and all.
                // Once the body has come to a stop the agent is left alone: a body standing on its place still gives
                // way when somebody crowds it, instead of being pinned where it stands
                _agent.velocity = _glide.Velocity;
                _drives = _glide.Velocity.sqrMagnitude > DEADZONE;
                gave = _drives;
            }

            _given = _glide.Velocity;
            _gave = gave;

            if (asked.HasValue)
            {
                if (!_hasFacing)
                    Face(_move);
            }
            else if (!_hasFacing && _agent.hasPath && _agent.velocity.sqrMagnitude > 0.04f)
            {
                Face(_agent.velocity);           // walking a path: look where the feet go
            }

            if (_hasFacing)
            {
                Quaternion wanted = Quaternion.LookRotation(_facing, Vector3.up);
                _owner.rotation = Quaternion.RotateTowards(_owner.rotation, wanted, _settings.TurnSpeed * dt);
                _hasFacing = false;
            }

            _move = Vector3.zero;                // the stick is pressed anew every step
        }

        /// <summary>Degrees per second the crowd's turn of the steps fades once it says nothing more (60° — in 0.7 s).</summary>
        private const float VEER_FADE = 90f;

        /// <summary>Degrees the crowd's word on the step of a frame must turn it by to count (below — the open).</summary>
        private const float VEER_HEARD = 0.5f;

        // How far the crowd has turned the steps asked lately (its word, kept while it lasts), degrees round the vertical
        private float _veer;
        private Vector3 _wantedWas;

        /// <summary>
        /// Which way the body really steps: the way asked now, turned as far as the crowd has turned the steps lately.
        /// The agent's own avoidance has its say about the speed it was given each frame — how far it turned that speed
        /// is where the crowd (a body in the way, a wall) leaves room — and that turn is kept and added to while the crowd
        /// keeps turning, and fades once it says nothing more. Only the crowd's turn is taken, never the old heading
        /// itself: out in the open the step goes exactly the way asked (a body walking east and asked north turns north;
        /// past the one in its way it is back on the way asked). It is never turned further than GIVE_WAY: a body gives
        /// way round the others instead of being pushed backwards by whoever walks into it (handbook §14.2, "the one
        /// holding its place gives way round, not inwards"). A step asked the other way starts with no turn of the old.
        /// </summary>
        private Vector3 GiveWay(Vector3 wanted, float speed, float dt)
        {
            if (!_gave || Vector3.Dot(wanted, _wantedWas) < 0f)
                _veer = 0f;
            _wantedWas = wanted;

            Vector3 room = Flatten(_agent.velocity);
            Vector3 given = Flatten(_given);
            float said = _gave && room.sqrMagnitude > DEADZONE && given.sqrMagnitude > DEADZONE
                ? Vector3.SignedAngle(given, room, Vector3.up)
                : 0f;
            _veer = Mathf.Abs(said) > VEER_HEARD
                ? Mathf.Clamp(_veer + said, -GIVE_WAY, GIVE_WAY)
                : Mathf.MoveTowards(_veer, 0f, VEER_FADE * dt);
            return Quaternion.AngleAxis(_veer, Vector3.up) * wanted * speed;
        }

        // The acceleration the glide speeds up and brakes with: up to (or down from) the speed it ramps on over Ramp
        // Distance (v² / 2s — a wary step starts gently, a run bursts off); no Ramp Distance — the agent's acceleration
        private float GlideAcceleration()
        {
            float ramp = _settings.RampDistance;
            if (ramp <= 0.01f || _rampSpeed <= 0.01f)
                return _agent.acceleration;

            return _rampSpeed * _rampSpeed / (2f * ramp);
        }

        // On a link (waiting at the foot of a wall, or about to jump) the agent is the link's: a reset path or a hand-held
        // step there throws it to the link's far end — the body was seen "teleported onto the wall"
        private bool IsUsable => _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh && !_agent.isOnOffMeshLink;

        // ---------- root motion: the clips carry the body, the agent says where and round whom ----------

        /// <summary>The bodies carried by their animation now: they push each other apart when they stand inside each other.</summary>
        private static readonly List<NavMeshMover> Carried = new();

        /// <summary>Metres per second: asked to go slower than this, a carried body is stopping — the stop clip puts its feet down.</summary>
        private const float STOPPING = 0.3f;

        private Vector3 _carriedVelocity;

        private bool CarriedByAnimation => _settings != null && _settings.RootMotion;

        /// <summary>
        /// The speed the animator is shown with root motion: the agent's own — the path's or the step's, with the crowd's
        /// word on it (its avoidance) — or the dash's.
        /// </summary>
        public Vector3 ShownVelocity
        {
            get
            {
                if (!IsUsable)
                    return Vector3.zero;

                return _dashLeft > 0f ? _dashVelocity : Flatten(_agent.velocity);
            }
        }

        /// <summary>How fast the body really went the last frame it was carried (root motion), metres per second.</summary>
        public Vector3 CarriedVelocity => _carriedVelocity;

        internal void Join()
        {
            Carried.RemoveAll(other => other._owner == null);    // bodies destroyed meanwhile
            if (CarriedByAnimation && !Carried.Contains(this))
                Carried.Add(this);
        }

        internal void Leave() => Carried.Remove(this);

        /// <summary>
        /// One frame of full root motion (handbook §20.2). An action (a blow, a dodge clip) moves the body as its clip does
        /// (kept off its target and the bodies near it, an aimed turn ending on its target); a dash goes its measured way;
        /// the walk goes the way the agent says — its path, its way round the others — at the clip's pace, stretched by no
        /// more than Pace Give. Then bodies standing inside each other are pushed apart, and the agent puts the result on
        /// the NavMesh: the body never leaves it, and the agent's own place is where the body is, so the path and the crowd
        /// go on from there. Returns how far the body went, flat.
        /// </summary>
        private Vector3 CarryByAnimation(Vector3 deltaPosition, Quaternion deltaRotation, bool inAction, float dt)
        {
            _byClip = inAction && _dashLeft <= 0f && IsUsable;
            if (!inAction)
                LetGoOfAim(dt);                             // over — or not shown yet: waited for a moment
            else
                _aimShown = true;
            if (!IsUsable || dt <= 0f)
                return Vector3.zero;

            Vector3 from = _owner.position;
            Vector3 step;
            if (_dashLeft > 0f)
                step = KeptOffOthers(_dashVelocity * dt, from);
            else if (inAction)
                step = ActionStep(deltaPosition, from);     // a blow, a dodge: its own steps, never stretched
            else if (ShownVelocity.magnitude < STOPPING)
                step = Flatten(deltaPosition);              // a stop clip puts its feet down
            else
                step = Steer(Flatten(deltaPosition), ShownVelocity * dt, _settings.PaceGive);

            step += PushApart(dt);
            // The agent keeps the body on the mesh. Its next position is where the body stands — the Ground Offset in it
            // already (Unity's own: transform.position = agent.nextPosition): adding the offset again put a body with a
            // Ground Offset that much off its agent, lower than the same body walked by the agent
            _agent.nextPosition = from + step;
            Vector3 to = _agent.nextPosition;
            _owner.position = to;
            _carriedVelocity = Flatten(to - from) / dt;
            if (inAction)
                TurnByClip(deltaRotation);

            return Flatten(to - from);
        }

        /// <summary>
        /// The walk's step this frame: the way the agent asks (wanted), as far as the clip went (root) — stretched toward
        /// the agent's pace by no more than give. With nothing asked the clip's own way is kept (a stop clip puts its
        /// feet down); with the clip standing the body stands, whatever is asked (the start clip begins it).
        /// </summary>
        public static Vector3 Steer(Vector3 root, Vector3 wanted, float give)
        {
            float clip = root.magnitude;
            float asked = wanted.magnitude;
            if (clip < 1e-6f)
                return Vector3.zero;

            give = Mathf.Clamp01(give);
            float pace = Mathf.Clamp(asked, clip * (1f - give), clip * (1f + give));
            Vector3 way = asked > 1e-5f ? wanted / asked : root / clip;
            return way * pace;
        }

        // Two bodies inside each other (their radii overlap) are pushed apart, each half the overlap, no faster than
        // Push Apart: the agents' avoidance steers them round each other, but its push no longer moves a carried body
        private Vector3 PushApart(float dt)
        {
            float most = _settings.PushApart * dt;
            if (most <= 0f)
                return Vector3.zero;

            Vector3 push = Vector3.zero;
            Vector3 at = _owner.position;
            int mine = Carried.IndexOf(this);
            for (int i = 0; i < Carried.Count; i++)
            {
                NavMeshMover other = Carried[i];
                if (other == this || other._agent == null || !other._agent.isActiveAndEnabled)
                    continue;

                Vector3 apart = Flatten(at - other._owner.position);
                float reach = _agent.radius + other._agent.radius;
                float distance = apart.magnitude;
                if (distance >= reach)
                    continue;

                // One on the other: the one carried first steps one way, the other the other — the same every run (a
                // hash of the object changed from run to run, and two equal ones went the same way)
                Vector3 away = distance > 1e-4f ? apart / distance : mine < i ? Vector3.left : Vector3.right;
                push += away * ((reach - distance) * 0.5f);
            }

            return Vector3.ClampMagnitude(push, most);
        }

        void IAgentNumbers.NamesOf(List<string> names)
        {
            names.Add(TURN_SPEED);
            names.Add(ACCELERATION);
            names.Add(RAMP_DISTANCE);
        }

        float IAgentNumbers.GetNumber(int index)
        {
            if (index == 0)
                return _settings.TurnSpeed;

            if (index == 2)
                return _settings.RampDistance;

            return _agent != null ? _agent.acceleration : 0f;
        }

        void IAgentNumbers.SetNumber(int index, float value)
        {
            value = Mathf.Max(0f, value);
            if (index == 0)
            {
                _settings.TurnSpeed = value;
                if (_agent != null)
                    _agent.angularSpeed = value;
            }
            else if (index == 2)
            {
                // How far the body takes to get up to speed: its acceleration is worked out from it anew (one owner)
                _settings.RampDistance = value;
                _settings.ApplyAcceleration(_agent);
            }
            else if (_agent != null)
            {
                _agent.acceleration = value;
            }
        }

        private static Vector3 Flatten(Vector3 direction) => new(direction.x, 0f, direction.z);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Mecanim
{
    /// <summary>
    /// AI Animator: shows on a Mecanim Animator what an AI Character does (step 90, the user's "very important" C22: the
    /// model knows nothing of its picture — the animation controller is a component of its own that knows the model,
    /// takes from it whatever it needs and works with it; handbook §20.1–20.2). It sits on the Animator's own object
    /// (it takes the root's motion itself), finds its character above it and, every frame, after the character, READS
    /// it — nothing is pushed into it: how fast the body really moves (measured, not asked), whether the brain has a
    /// target (the combat stance), the weapon in the hand and its clip set (swapped in its own override, never the
    /// controller, and before any action is shown — a draw is shown with the new weapon's clips), the body's climb and
    /// jump, the character's actions (AgentContext.Actions: a new request cross-fades to the state of that name, a gentle
    /// end to its ending) and its hurt (a flinch on a layer of its own). Its only ways back are the answers the model
    /// declares (IActionView: "is there such an action", "is the request still on screen"), and the root's motion handed
    /// to a body that takes it (AgentBody.MoveByAnimation). A character without it works the same: nothing is shown.
    /// </summary>
    [AddComponentMenu("AI Kit/AI Animator")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(-140)]
    public sealed class AIAnimator : MonoBehaviour, IActionView
    {
        /// <summary>Metres per second no body walks at: a jump of the position this fast is a teleport, not a run.</summary>
        private const float TELEPORT_SPEED = 30f;

        /// <summary>The slowest a walk is played when the controller cannot shorten its steps (no Stride).</summary>
        private const float SLOWEST_WITHOUT_STRIDE = 0.5f;

        /// <summary>Seconds over which the change of speed is averaged before it counts as braking.</summary>
        private const float BRAKING_SMOOTHING = 0.08f;

        /// <summary>
        /// How far forward (MoveZ) the way must point to show the forward half again, and how far back for the backward
        /// one (0.2 — about 12° past the side): in between, round the side, the half shown stays.
        /// </summary>
        public const float BACKING_SWITCH = 0.2f;

        /// <summary>Seconds Backing takes to go from one half to the other.</summary>
        public const float BACKING_BLEND = 0.25f;

        /// <summary>Seconds of blending into a climbing state put at once.</summary>
        private const float CLIMB_BLEND = 0.05f;

        /// <summary>Seconds of blending into the flinch.</summary>
        private const float HURT_BLEND = 0.05f;

        [SerializeField] private AnimatorParameters _parameters = new();

        [SerializeField] private AnimatorStates _states = new();

        [SerializeField] private AnimatorTuning _tuning = new();

        private Animator _animator;
        private AICharacter _character;
        private AgentContext _context;
        private Transform _owner;

        /// <summary>
        /// The action being shown has changed: its name when one starts (the plain swing of an attack comes as "Attack"),
        /// null when the body is free again. For what goes with an action in the picture: a tool in the hand.
        /// </summary>
        public event Action<string> ActionChanged;

        /// <summary>The parameters of the controller it writes.</summary>
        public AnimatorParameters Parameters => _parameters;

        /// <summary>The states it puts the animator into.</summary>
        public AnimatorStates States => _states;

        /// <summary>How it turns the body's motion into the picture.</summary>
        public AnimatorTuning Tuning => _tuning;

        /// <summary>The Animator it shows the character on (on its own object).</summary>
        public Animator Animator => _animator != null ? _animator : _animator = GetComponent<Animator>();

        private IControllerFace _face;

        /// <summary>
        /// Where its controller plays and is driven: the Animator itself unless something else is put in front of it (a
        /// graph that plays the controller inside it — a bridge sets it). Setting it reads the parameters again.
        /// </summary>
        public IControllerFace Face
        {
            get => _face ??= new AnimatorFace(Animator);
            set
            {
                _face = value ?? new AnimatorFace(Animator);
                _hashedFor = null;
                _motions = null;
                _shownSet = null;
                ReadParameters();
            }
        }

        /// <summary>
        /// Actions (by name; the plain swing — ActionState.SWING) something else shows (a bridge): this animator leaves them
        /// alone and the action it shows ends. Null — it shows every action it has.
        /// </summary>
        public Predicate<string> ShowsActionElsewhere { get; set; }

        /// <summary>
        /// It hands the root's motion to the body (or moves its object) in OnAnimatorMove; off — something else does (a
        /// bridge that takes the motion of a graph in front of the Animator).
        /// </summary>
        public bool ForwardsRootMotion { get; set; } = true;

        /// <summary>
        /// The view the character's actions answer through instead of this animator (a bridge that shows some actions
        /// itself and asks this animator about the rest); null — this animator.
        /// </summary>
        public IActionView ViewInFront { get; set; }

        /// <summary>An action of the controller plays now (its base layer is in an Action-tagged state, or blends into one).</summary>
        public bool ShowsActionNow => InAction(Animator);

        /// <summary>The character it shows (null — none found above it yet).</summary>
        public AICharacter Character => _character;

        /// <summary>It has found its character and reads it.</summary>
        public bool IsAttached => _context != null;

        // ---------- what it shows now (the debugger and the tests read it) ----------

        /// <summary>The action being shown now (null — none).</summary>
        public string CurrentAction => _action;

        /// <summary>The speed the animator is shown now, metres per second.</summary>
        public float ShownSpeed => _shownSpeed;

        /// <summary>The animator is shown "in combat" now: the brain has a target, or had one less than Calm After ago.</summary>
        public bool ShowsCombat => _engaged || _combatLeft > 0f;

        /// <summary>The body is shown braking now: it slows down faster than Braking At.</summary>
        public bool ShowsBraking => _speedChange < -_tuning.BrakingAt;

        /// <summary>Backing as shown now: 0 — the forward half of the circle, 1 — the backward one.</summary>
        public float ShownBacking => _shownBacking;

        /// <summary>How many plain swings were shown.</summary>
        public int Swings { get; private set; }

        /// <summary>The weapon's clip set the animator shows now (null — the controller's own clips).</summary>
        public AnimatorOverrideController ShownMotions => _shownSet;

        /// <summary>The clip set that was shown when the last action began (the draw of a new weapon: that weapon's).</summary>
        public AnimatorOverrideController MotionsAtLastAction { get; private set; }

        /// <summary>The body takes the root's motion from this animator (it uses root motion).</summary>
        public bool DrivesBody => _rootMotionBody != null;

        // ---------- Unity ----------

        private void Awake() => _animator = GetComponent<Animator>();

        private void OnEnable()
        {
            Attach();
            ResetPosition();
        }

        private void OnDisable() => Detach();

        private void Update() => FrameTick(Time.deltaTime);

        // Unity calls it on the Animator's own object only: the root's motion goes to the body that takes it; with no such
        // body the animator moves its object as it would by itself
        private void OnAnimatorMove()
        {
            Animator animator = Animator;
            if (animator == null || !ForwardsRootMotion)
                return;

            if (_rootMotionBody != null)
            {
                // The time the animator was stepped by: the frame the picture was stepped with (FrameTick comes first; a
                // paused frame — none), or, animated with the physics, the physics step
                float dt = animator.updateMode == AnimatorUpdateMode.Fixed ? Time.fixedDeltaTime : _frameDt;
                _movedByClip += _rootMotionBody.MoveByAnimation(animator.deltaPosition, animator.deltaRotation,
                    InAction(animator), dt);
            }
            else if (animator.applyRootMotion)
            {
                animator.ApplyBuiltinRootMotion();
            }
        }

        // ---------- the character ----------

        /// <summary>
        /// Finds the character above (or takes the one given), and becomes the view of its actions; a body that takes
        /// root motion gets the root's motion from now on. Safe to call again (Awake order is not known: whoever comes
        /// later links up). False — no character with a context yet.
        /// </summary>
        public bool Attach(AICharacter character = null)
        {
            if (character != null)
                _character = character;
            if (_character == null)
                _character = GetComponentInParent<AICharacter>(true);
            if (_character == null || _character.Context == null)
                return false;

            if (_context == _character.Context)
            {
                if (!ReferenceEquals(_context.Actions.View, ViewInFront ?? this))
                {
                    // Re-enabled (or attached again after Detach): what was asked and suffered meanwhile is over — it is
                    // not replayed now, as for an AI Animator that comes late
                    _readSerial = _context.Actions.Serial;
                    _hurtSeen = _context.HurtAt;
                    _context.Actions.View = ViewInFront ?? this;       // the view again
                }

                return true;
            }

            _context = _character.Context;
            _owner = _character.transform;
            _context.Actions.View = ViewInFront ?? this;
            _readSerial = _context.Actions.Serial;      // what was asked before it came is not replayed
            _hurtSeen = _context.HurtAt;
            ReadParameters();

            AgentBody body = _character.Body;
            if (body != null && body.UsesRootMotion && Animator != null)
            {
                _rootMotionBody = body;
                Animator.applyRootMotion = true;
            }

            ResetPosition();
            return true;
        }

        /// <summary>No longer the view of the character's actions (switched off): its actions are shown by nothing.</summary>
        public void Detach()
        {
            if (_context != null && ReferenceEquals(_context.Actions.View, ViewInFront ?? this))
                _context.Actions.View = null;
            ResetAttack();                              // a swing waiting to fire is not fired when it is back
        }

        /// <summary>The body was put somewhere (enabled, moved): the next frame measures no speed from the old place.</summary>
        public void ResetPosition()
        {
            if (_owner != null)
                _lastPosition = _owner.position;
            _movedByClip = Vector3.zero;
            _lastSpeed = 0f;
            _speedChange = 0f;
        }

        /// <summary>
        /// The clip of an action moved the body by this much (flat; a body that moves by hand): it is the clip's own step,
        /// not a walk — the speed shown leaves it out.
        /// </summary>
        public void MovedByClip(Vector3 moved) => _movedByClip += moved;

        // ---------- every frame: the character read, the picture written ----------

        private Vector3 _lastPosition;
        private Vector3 _movedByClip;
        private float _frameDt;
        private float _shownSpeed;
        private float _lastSpeed;
        private float _speedChange;
        private float _combatLeft;
        private bool _engaged;
        private Vector2 _shownDirection = new(0f, 1f);
        private bool _backing;
        private float _shownBacking;
        private int _climbWas;
        private JumpPhase _jumpWas;
        private int _shownClimb;    // the body was carried last frame (a climb or a jump): no walk is measured from there
        private AgentBody _rootMotionBody;
        private float _hurtSeen = -1f;

        /// <summary>One frame of the picture (Update calls it; a test steps it by hand): reads the character, writes the Animator.</summary>
        public void FrameTick(float dt)
        {
            // The frame's time, whatever comes of it: the root's motion of a paused frame (no time) is handed on with none
            _frameDt = Mathf.Max(0f, dt);
            if (!IsAttached && !Attach())
                return;

            Animator animator = Animator;
            IControllerFace face = Face;
            if (animator == null || _owner == null || dt <= 0f)
                return;

            // The weapon's clips first: an action asked for in the same step (a draw) is shown with them. A weapon taken
            // from the world (a rock, a turret) shows its own while it is held, if it has any
            WeaponAsset world = _character.WorldWeapon;
            WeaponAsset weapon = world != null && world.Motions != null ? world : _character.MotionsWeapon;
            ShowMotions(weapon != null ? weapon.Motions : null);
            ReadAction();
            ReadHurt();

            _engaged = _context.BrainTarget != null;
            AgentBody body = _character.Body;

            // On a wall the body is carried by the climb, and the clip of the climb shows it: no walk is measured. Off
            // the wall again, the walk is measured from where the climb left the body
            int climbing = Mathf.Clamp(body?.ClimbClipDirection ?? 0, -1, 1);
            JumpPhase jumping = body?.Jump ?? JumpPhase.None;
            if (_hasClimb)
                face.SetFloat(_climbId, climbing);
            if (climbing != 0 && climbing != _climbWas)
                StartState(climbing > 0 ? _states.ClimbUp : _states.ClimbDown, CLIMB_BLEND, 0);
            _climbWas = climbing;
            if (_hasJump)
                face.SetInteger(_jumpId, (int)jumping);
            if (jumping == JumpPhase.Landing && _jumpWas != JumpPhase.Landing)
                StartLanding(body);
            _jumpWas = jumping;
            int carried = climbing != 0 ? climbing : jumping != JumpPhase.None ? 2 : 0;
            if (carried != 0 || _shownClimb != 0)
            {
                _shownClimb = carried;
                ResetPosition();
                _shownSpeed = 0f;
                if (_hasSpeed)
                    face.SetFloat(_speedId, 0f);
                if (_hasBraking)
                    face.SetBool(_brakingId, false);
                _combatLeft = _engaged ? _tuning.CalmAfter : Mathf.Max(0f, _combatLeft - dt);
                if (_hasCombat)
                    face.SetBool(_combatId, ShowsCombat);
                return;
            }

            // The speed is measured, not asked: whatever moves the body (a path, a push), the feet follow — all but the
            // clip of an action, which moves the body itself. A body the clips carry the walk of too is shown the speed it
            // is asked for: the walking clip then moves it that way (shown its own speed, a standing one never starts)
            Vector3 moved = _rootMotionBody is { WalksByAnimation: true }
                ? _rootMotionBody.ShownVelocity * dt
                : _owner.position - _lastPosition - _movedByClip;
            _lastPosition = _owner.position;
            _movedByClip = Vector3.zero;
            moved.y = 0f;
            float speed = moved.magnitude / dt;
            bool teleport = speed > TELEPORT_SPEED;
            if (teleport)
                speed = 0f;                             // moved in one frame further than anything walks: a teleport

            // How fast the speed changes, averaged a little: falling fast — the body brakes (a stop clip may play);
            // a steady walk, however slow, does not
            float change = teleport ? 0f : (speed - _lastSpeed) / dt;
            _lastSpeed = speed;
            _speedChange = Mathf.Lerp(_speedChange, change, 1f - Mathf.Exp(-dt / BRAKING_SMOOTHING));
            if (_hasBraking)
                face.SetBool(_brakingId, ShowsBraking);

            float smoothing = _tuning.Smoothing;
            _shownSpeed = smoothing > 0f ? Mathf.Lerp(_shownSpeed, speed, 1f - Mathf.Exp(-dt / smoothing)) : speed;

            // Which way it goes relative to its nose: a body that backs away or sidesteps shows it
            if (speed > 0.1f)
            {
                Vector3 local = _owner.InverseTransformDirection(moved).normalized;
                var direction = new Vector2(local.x, local.z);
                _shownDirection = smoothing > 0f
                    ? Vector2.Lerp(_shownDirection, direction, 1f - Mathf.Exp(-dt / smoothing))
                    : direction;

                // The half of the circle turns over only past the side by a margin: a body that walks round its target
                // (straight sideways, a little forward one moment and a little back the next) keeps one side clip
                if (_shownDirection.y < -BACKING_SWITCH)
                    _backing = true;
                else if (_shownDirection.y > BACKING_SWITCH)
                    _backing = false;
            }

            _shownBacking = Mathf.MoveTowards(_shownBacking, _backing ? 1f : 0f, dt / BACKING_BLEND);
            if (_hasBacking)
                face.SetFloat(_backingId, _shownBacking);

            if (_hasMoveX)
                face.SetFloat(_moveXId, _shownDirection.x);
            if (_hasMoveZ)
                face.SetFloat(_moveZId, _shownDirection.y);
            if (_hasSpeed)
                face.SetFloat(_speedId, _shownSpeed);
            // The pace of the legs follows the speed down to the slowest a walk looks right at; slower than that, a
            // controller with Stride shortens the steps (the walk blends with the stance) instead of slowing them
            float pace = _shownSpeed / Mathf.Max(0.1f, _tuning.ClipSpeed);
            float slowest = _hasStride ? Mathf.Clamp(_tuning.SlowestRate, 0.1f, 1f) : SLOWEST_WITHOUT_STRIDE;
            if (_hasMoveRate)
                face.SetFloat(_moveRateId, Mathf.Clamp(pace, slowest, 3f));
            if (_hasStride)
                face.SetFloat(_strideId, Mathf.Clamp01(pace / slowest));

            _combatLeft = _engaged ? _tuning.CalmAfter : Mathf.Max(0f, _combatLeft - dt);
            if (_hasCombat)
                face.SetBool(_combatId, ShowsCombat);
            if (_hasWeapon)
                face.SetInteger(_weaponId, _character.WeaponIndex);

            WatchAction(dt);
        }

        // ---------- the weapon's clip set ----------

        // Its own override of the animator: one controller for every weapon, the weapon's Motions put its clips on the
        // controller's states (handbook §20.5)
        private AnimatorOverrideController _motions;
        private AnimatorOverrideController _shownSet;
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _overrides = new();
        private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _setPairs = new();
        private readonly Dictionary<AnimationClip, AnimationClip> _setClips = new();

        // The clips of a character variant (an override controller on the Animator): they stay under every weapon's set
        private readonly Dictionary<AnimationClip, AnimationClip> _ownClips = new();

        /// <summary>
        /// The animator shows this set of clips from now on: the clips of its own override are swapped in place
        /// (ApplyOverrides), never the controller — swapping the controller would start the animator over. A clip the
        /// set leaves alone is the controller's own — or, for a character variant (an override controller on the
        /// Animator), the variant's. Null — every clip the controller's (the variant's) own. Asked every frame with the
        /// weapon the character shows its moves with; nothing happens when the set is the one shown.
        /// </summary>
        public void ShowMotions(AnimatorOverrideController set)
        {
            Animator animator = Animator;
            IControllerFace face = Face;
            if (animator == null || set == _shownSet && (_motions != null || set == null))
                return;

            if (_motions == null)
            {
                if (set == null)
                    return;                             // no set yet and none asked: the controller as it is

                RuntimeAnimatorController controller = face.Controller;
                _ownClips.Clear();
                if (controller is AnimatorOverrideController variant)
                {
                    _setPairs.Clear();
                    variant.GetOverrides(_setPairs);
                    foreach (KeyValuePair<AnimationClip, AnimationClip> pair in _setPairs)
                    {
                        if (pair.Key != null && pair.Value != null)
                            _ownClips[pair.Key] = pair.Value;
                    }

                    controller = variant.runtimeAnimatorController;
                }

                if (controller == null)
                    return;

                _motions = new AnimatorOverrideController(controller) { name = controller.name + " (Motions)" };
                face.Controller = _motions;
                ReadParameters();
            }

            _setClips.Clear();
            if (set != null)
            {
                _setPairs.Clear();
                set.GetOverrides(_setPairs);
                foreach (KeyValuePair<AnimationClip, AnimationClip> pair in _setPairs)
                {
                    if (pair.Key != null && pair.Value != null)
                        _setClips[pair.Key] = pair.Value;
                }
            }

            _overrides.Clear();
            _motions.GetOverrides(_overrides);
            for (int i = 0; i < _overrides.Count; i++)
            {
                AnimationClip original = _overrides[i].Key;
                AnimationClip shown = null;
                if (original != null && !_setClips.TryGetValue(original, out shown))
                    _ownClips.TryGetValue(original, out shown);
                _overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, shown);
            }

            _motions.ApplyOverrides(_overrides);
            _shownSet = set;
        }

        // ---------- actions: the character's state, shown ----------

        private int _readSerial;
        private bool _showing;      // the request read last is on screen (its action, or its ending)
        private string _action;
        private bool _swinging;     // the action shown is the plain swing (the trigger), not a state of its name
        private float _actionTime;
        private bool _fresh;        // the request was read this frame: the animator has had no step to start it yet
        private int _returnTo;      // the state the animator was in when the action began (its full path)
        private bool _hasReturn;

        // A new request of the character since the last frame: shown (only the last one counts — it is a state). Any new
        // request clears a swing the controller has not taken yet: it never fires later on its own
        private void ReadAction()
        {
            ActionState actions = _context.Actions;
            if (actions.Serial == _readSerial)
                return;

            _readSerial = actions.Serial;
            ResetAttack();
            switch (actions.Current)
            {
                // An action something else shows (a bridge) is left to it: the one shown here ends
                case ActionState.Kind.Action when ShowsActionElsewhere != null && ShowsActionElsewhere(actions.Name):
                case ActionState.Kind.Swing when ShowsActionElsewhere != null && ShowsActionElsewhere(ActionState.SWING):
                    Leave();
                    break;
                case ActionState.Kind.Action:
                    Play(actions.Name);
                    break;
                case ActionState.Kind.Swing:
                    Swing();
                    break;
                default:
                    Stop(!actions.EndsGently);
                    break;
            }
        }

        // "X Begin" is entered first if the controller has one (it leads to "X" by itself). A name the controller does
        // not know shows nothing — and the action on screen ends with it: the character asked for something else
        // (ActionState.Begin: "nothing is shown")
        private void Play(string action)
        {
            Animator animator = Animator;
            IControllerFace face = Face;
            _showing = false;
            if (animator == null)
                return;

            int state = StateHash(animator, 0, action, Suffix.Begin);
            if (state == 0)
                state = StateHash(animator, 0, action, Suffix.None);
            if (state == 0)
            {
                Leave();
                return;
            }

            RememberWhereFrom(animator);
            face.CrossFadeInFixedTime(state, _states.Blend, 0);
            Begin(action);
        }

        // The plain swing of an attack, by the Attack trigger; an action of a name on screen gives way to it first (back
        // to the walk, where a swing starts from); no trigger — nothing to show it with, the action on screen ends
        private void Swing()
        {
            Swings++;
            _showing = false;
            Animator animator = Animator;
            IControllerFace face = Face;
            if (animator == null)
                return;

            if (!_hasAttack)
            {
                Leave();
                return;
            }

            if (_action != null && !_swinging)
                BackToTheWalk(animator);
            else
                RememberWhereFrom(animator);
            face.SetTrigger(_attackId);
            Begin("Attack");
            _swinging = true;
        }

        // The action is ended: its ending ("X Stop") if asked gently and the controller has one, else back to the walk
        private void Stop(bool atOnce)
        {
            Animator animator = Animator;
            IControllerFace face = Face;
            _showing = _action != null;                 // the ending (or the blend out) is still on screen
            if (animator == null || _action == null)
                return;

            int stop = atOnce ? 0 : StateHash(animator, 0, _action, Suffix.Stop);
            if (stop != 0)
                face.CrossFadeInFixedTime(stop, _states.Blend, 0);
            else
                BackToTheWalk(animator);

            _actionTime = 0f;                           // the ending is given its time to begin as well
            _fresh = true;
        }

        // A request that cannot be shown: the action on screen ends at once
        private void Leave()
        {
            if (_action == null)
                return;

            BackToTheWalk(Animator);
            _action = null;
            _swinging = false;
            _showing = false;
            ActionChanged?.Invoke(null);
        }

        // Back to the walk: the Locomotion state — or, a controller with no state of that name, the state the animator
        // was in when the action began (a state that is not there is never asked for)
        private void BackToTheWalk(Animator animator)
        {
            IControllerFace face = Face;
            if (animator == null)
                return;

            int walk = StateHash(animator, 0, _states.Locomotion, Suffix.None);
            if (walk == 0 && _hasReturn && face.HasState(0, _returnTo))
                walk = _returnTo;
            if (walk != 0)
                face.CrossFadeInFixedTime(walk, _states.Blend, 0);
        }

        // Where the animator is before an action starts (from another action: where that one came from)
        private void RememberWhereFrom(Animator animator)
        {
            IControllerFace face = Face;
            AnimatorStateInfo now = face.GetCurrentAnimatorStateInfo(0);
            if (!string.IsNullOrEmpty(_states.ActionTag) && now.IsTag(_states.ActionTag))
                return;

            _returnTo = now.fullPathHash;
            _hasReturn = true;
        }

        private void Begin(string action)
        {
            _action = action;
            _swinging = false;
            _actionTime = 0f;
            _fresh = true;
            _showing = true;
            MotionsAtLastAction = _shownSet;
            ActionChanged?.Invoke(action);
        }

        // A swing nobody took is not kept for later: a new request, an end, a switch-off clears it
        private void ResetAttack()
        {
            Animator animator = Animator;
            IControllerFace face = Face;
            if (_hasAttack && animator != null && face.Controller != null)
                face.ResetTrigger(_attackId);
        }

        // An action is over when the animator has left its states (the blend into it is given time to begin first —
        // counted from the first frame after the request: the frame that read it may be long, and the animator steps
        // into the action only after it)
        private void WatchAction(float dt)
        {
            if (_action == null)
                return;

            if (_fresh)
            {
                _fresh = false;
                return;
            }

            _actionTime += dt;
            if (_actionTime <= _states.Blend + 0.1f)
                return;

            if (InAction(Animator))
                return;

            _action = null;
            _swinging = false;
            _showing = false;
            ActionChanged?.Invoke(null);
        }

        /// <summary>An action plays on the Animator's base layer now (or is being blended into).</summary>
        private bool InAction(Animator animator)
        {
            IControllerFace face = Face;
            if (animator == null || string.IsNullOrEmpty(_states.ActionTag) || face.Controller == null)
                return false;

            return face.GetCurrentAnimatorStateInfo(0).IsTag(_states.ActionTag) ||
                   (face.IsInTransition(0) && face.GetNextAnimatorStateInfo(0).IsTag(_states.ActionTag));
        }

        /// <summary>IActionView: the controller has an action of this name (a state "X", or "X Begin").</summary>
        public bool Has(string action)
        {
            Animator animator = Animator;
            IControllerFace face = Face;
            return animator != null && face.Controller != null && !string.IsNullOrEmpty(action) &&
                   (StateHash(animator, 0, action, Suffix.Begin) != 0 || StateHash(animator, 0, action, Suffix.None) != 0);
        }

        /// <summary>
        /// IActionView: the request with this serial is still on screen — a request not read yet counts as on screen; one
        /// a newer request has replaced does not.
        /// </summary>
        public bool IsShowing(int serial) => serial > _readSerial || serial == _readSerial && _showing;

        // ---------- the jump's landing: from where the feet touch ----------

        // The body is down: the landing clip is started where its feet touch the ground (the measured Touch) — started
        // from its own start the feet would still be in the air while the body stands up
        private void StartLanding(AgentBody body)
        {
            MeasuredTraversal traversal = body?.Traversal;
            if (traversal == null || !traversal.TryJump(out JumpMotion jump) || jump.Touch <= 0f)
                return;

            Animator animator = Animator;
            IControllerFace face = Face;
            int land = StateHash(animator, 0, _states.JumpLand, Suffix.None);
            if (land != 0)
                face.CrossFadeInFixedTime(land, CLIMB_BLEND, 0, jump.Touch);
        }

        // ---------- the hurt: a flinch on its own layer (handbook §20.2), nothing in the brain ----------

        private void ReadHurt()
        {
            float hurtAt = _context.HurtAt;
            if (Mathf.Approximately(hurtAt, _hurtSeen))
                return;

            _hurtSeen = hurtAt;
            if (hurtAt < 0f || string.IsNullOrEmpty(_states.Hurt) || string.IsNullOrEmpty(_states.HurtLayer))
                return;

            Animator animator = Animator;
            IControllerFace face = Face;
            int layer = face.GetLayerIndex(_states.HurtLayer);
            if (StartState(_states.Hurt, HURT_BLEND, layer))
                Flinches++;
        }

        /// <summary>How many flinches were shown (the debugger and the tests read it).</summary>
        public int Flinches { get; private set; }

        // A state from its first frame, whatever the animator is doing on that layer; false — it has no such state
        private bool StartState(string state, float blend, int layer)
        {
            Animator animator = Animator;
            IControllerFace face = Face;
            int hash = StateHash(animator, layer, state, Suffix.None);
            if (hash == 0)
                return false;

            face.CrossFadeInFixedTime(hash, blend, layer, 0f);
            return true;
        }

        // ---------- the states' hashes, worked out once a name (asked inside the agent's step: no strings there) ----------

        private enum Suffix
        {
            None,
            Begin,
            Stop
        }

        // The full path's hash and the short name's of the states asked about, by name — a table for each layer and
        // suffix, for the controller they were read for
        private readonly List<Dictionary<string, Vector2Int>> _stateHashes = new();
        private RuntimeAnimatorController _hashedFor;

        // The hash the animator knows the state "name" (+ " Begin" / " Stop") by on the layer — its full path ("Base
        // Layer.Climb Up") or its short name; 0 — the controller has no such state
        private int StateHash(Animator animator, int layer, string name, Suffix suffix)
        {
            IControllerFace face = Face;
            if (animator == null || string.IsNullOrEmpty(name) || layer < 0)
                return 0;

            RuntimeAnimatorController controller = face.Controller;
            if (controller == null || layer >= face.LayerCount)
                return 0;

            if (!ReferenceEquals(controller, _hashedFor))
            {
                for (int i = 0; i < _stateHashes.Count; i++)
                    _stateHashes[i]?.Clear();
                _hashedFor = controller;
            }

            int table = layer * 3 + (int)suffix;
            while (_stateHashes.Count <= table)
                _stateHashes.Add(null);
            Dictionary<string, Vector2Int> hashesOf = _stateHashes[table] ??= new Dictionary<string, Vector2Int>();
            if (!hashesOf.TryGetValue(name, out Vector2Int hashes))
            {
                string state = suffix == Suffix.Begin ? name + " Begin" : suffix == Suffix.Stop ? name + " Stop" : name;
                hashes = new Vector2Int(Animator.StringToHash(face.GetLayerName(layer) + "." + state),
                    Animator.StringToHash(state));
                hashesOf[name] = hashes;
            }

            if (face.HasState(layer, hashes.x))
                return hashes.x;

            return face.HasState(layer, hashes.y) ? hashes.y : 0;
        }


        // ---------- the controller's parameters ----------

        private int _speedId, _moveRateId, _attackId, _moveXId, _moveZId, _combatId, _brakingId, _strideId, _backingId,
            _weaponId, _climbId, _jumpId;

        private bool _hasSpeed, _hasMoveRate, _hasAttack, _hasMoveX, _hasMoveZ, _hasCombat, _hasBraking, _hasStride,
            _hasBacking, _hasWeapon, _hasClimb, _hasJump;

        /// <summary>Looks up the controller's parameters by the names set here (call after changing the controller or a name).</summary>
        public void ReadParameters()
        {
            AnimatorParameters names = _parameters;
            Read(names.Speed, AnimatorControllerParameterType.Float, out _speedId, out _hasSpeed);
            Read(names.MoveRate, AnimatorControllerParameterType.Float, out _moveRateId, out _hasMoveRate);
            Read(names.Attack, AnimatorControllerParameterType.Trigger, out _attackId, out _hasAttack);
            Read(names.MoveX, AnimatorControllerParameterType.Float, out _moveXId, out _hasMoveX);
            Read(names.MoveZ, AnimatorControllerParameterType.Float, out _moveZId, out _hasMoveZ);
            Read(names.Combat, AnimatorControllerParameterType.Bool, out _combatId, out _hasCombat);
            Read(names.Stride, AnimatorControllerParameterType.Float, out _strideId, out _hasStride);
            Read(names.Braking, AnimatorControllerParameterType.Bool, out _brakingId, out _hasBraking);
            Read(names.Backing, AnimatorControllerParameterType.Float, out _backingId, out _hasBacking);
            Read(names.Weapon, AnimatorControllerParameterType.Int, out _weaponId, out _hasWeapon);
            Read(names.Climb, AnimatorControllerParameterType.Float, out _climbId, out _hasClimb);
            Read(names.Jump, AnimatorControllerParameterType.Int, out _jumpId, out _hasJump);
        }

        private void Read(string parameter, AnimatorControllerParameterType type, out int id, out bool has)
        {
            id = Animator.StringToHash(parameter ?? "");
            has = Face.HasParameter(parameter, type);
        }

        /// <summary>The controller of this Animator has a parameter of this name and kind.</summary>
        public static bool HasParameter(Animator animator, string parameter, AnimatorControllerParameterType type)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameter))
                return false;

            foreach (AnimatorControllerParameter candidate in animator.parameters)
            {
                if (candidate.name == parameter && candidate.type == type)
                    return true;
            }

            return false;
        }
    }
}

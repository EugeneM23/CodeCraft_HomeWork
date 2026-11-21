using System.Collections;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// VoxelSandbox's camera works as in tank games. The window holds the cursor, and moving the mouse
    /// swings the game camera round whatever it follows (the tank): left and right all the way
    /// round. Up and down it tilts round the tank, and not evenly. Mouse up, towards where the view
    /// looks, tilts it only a little, <see cref="_tiltAhead"/>, towards the horizon. Mouse down, away
    /// from the view, tilts it far more, <see cref="_tiltBack"/>, up over the tank (short of
    /// straight overhead), and on the way moves it up to <see cref="_backShift"/> back and
    /// <see cref="_backRise"/> up. Over the tank the view looks steeply down, so the tilt alone still
    /// showed only a couple of metres behind the tank; the step back is what brings the ground behind
    /// it on screen, and the rise keeps the tank from sliding to the top edge.
    ///
    /// For now the view does not turn up and down at all (<see cref="_turnView"/> off): mouse down
    /// only moves the camera back and up, mouse up only brings it back, and the view keeps the
    /// scene's angle. Left and right still swing it round the tank. The battle HUD's crosshair in the middle
    /// of the screen aims: while the cursor is locked, the
    /// turret turns to the screen centre (<see cref="CursorUseCase.ScreenPoint"/>); the shells fly
    /// straight out of the barrel.
    ///
    /// The camera starts as the scene shows it. The offset is taken from where [CameraRoot] stands
    /// to the camera point of the tank it follows. The rig's angle and the camera's own turn and shift under the rig are kept as well
    /// (the follow keeps the camera's turn). The swing turns all of it round the vertical and tilts
    /// it; a vehicle's CameraDistance does not push the camera further out here.
    ///
    /// While the cursor is held the camera looks ahead, where the crosshair points: it moves
    /// <see cref="_lookAhead"/> forward, stands <see cref="_pullBack"/> further out along its offset
    /// and turns its view <see cref="_lookTilt"/> up towards the horizon. More ground ahead shows,
    /// and the tank sits lower on the screen.
    ///
    /// The keys and the mouse come from the player context's IPlayerInput. Esc lets the cursor go:
    /// the camera stays where it is and the cursor aims as it did before. A
    /// click in the game takes the cursor back. Double-click the middle button to put the camera back.
    ///
    /// The game's follow is left to do the following: this only turns its offset (the player
    /// context's CameraOffset) and tilts the rig by the same rotation.
    /// </summary>
    [DefaultExecutionOrder(9000)] // LateUpdate after the follow (TickableManager), which sets the camera's turn first
    public sealed class VoxelSandboxOrbit : MonoBehaviour
    {
        [Tooltip("Degrees the camera turns per pixel of mouse movement.")]
        [SerializeField] private float _turnSpeed = 0.45f;
        [Tooltip("How far (degrees) mouse up, towards where the view looks, tilts the camera towards the horizon.")]
        [SerializeField] private float _tiltAhead = 3.4f;
        [Tooltip("How far (degrees) mouse down, away from the view, tilts the camera up over the tank; never " +
                 "past 88° above the horizon.")]
        [SerializeField] private float _tiltBack = 25.5f;
        [Tooltip("Metres the camera moves back, away from the view, at the full tilt back: the ground behind " +
                 "the tank comes on screen.")]
        [SerializeField] private float _backShift = 9f;
        [Tooltip("Metres the camera rises at the full tilt back, on top of the tilt. With the step back it " +
                 "keeps the tank where the step back alone would push it towards the top of the screen.")]
        [SerializeField] private float _backRise = 3f;
        [Tooltip("How quickly the camera catches up with the mouse.")]
        [SerializeField] private float _ease = 12f;
        [Tooltip("Off (for now): mouse up and down only moves the camera, back and up, and the view keeps the " +
                 "scene's angle; mouse up past the start does nothing. On: the camera also tilts round the tank " +
                 "and the view turns with it, and looking ahead turns the view up.")]
        [SerializeField] private bool _turnView = false;

        [Tooltip("Off (the user): the mouse only swings the camera round the tank; its height, distance and angle stay as "
                 + "the scene sets them, and no look ahead. On: mouse up and down tilts it and it looks ahead, as before.")]
        [SerializeField] private bool _tiltWithMouse = false;

        [Tooltip("Metres the view's middle is ahead of the tank, the way the camera faces: more of that side is on " +
                 "screen and the tank sits lower. The camera goes round the tank on a circle and the lead turns with it.")]
        [SerializeField] private float _lookLead = 2f;

        [Tooltip("Metres the mouse can push the view's middle about (off the lead's point) to peek that way; pushed on " +
                 "past the edge sideways, the camera turns. 0 — the mouse only turns it.")]
        [SerializeField] private float _peekRadius = 3f;
        [Tooltip("Metres of peek per degree the mouse would have turned the camera.")]
        [SerializeField] private float _peekPerDegree = 0.08f;
        [Tooltip("Degrees the camera turns down when the peek is pushed to the bottom of the screen (to the disc's edge).")]
        [SerializeField] private float _nodDown = 4f;
        [Tooltip("Degrees the camera turns up when the peek is pushed to the top of the screen; a gradient in between.")]
        [SerializeField] private float _nodUp = 1f;
        private float _debugNod;
        // Off for now (the user): no nod with the peek.
        private const bool NOD_ON = false;

        [Header("Zoom (mouse wheel)")]
        [Tooltip("Share of the distance one wheel notch zooms in or out (0.15 — 15 %).")]
        [SerializeField] private float _zoomPerNotch = 0.15f;
        [Tooltip("Nearest, as a share of the scene's distance.")]
        [SerializeField] private float _zoomMin = 0.4f;
        [Tooltip("Furthest, as a share of the scene's distance.")]
        [SerializeField] private float _zoomMax = 2f;

        private float _zoom = 1f;
        private float _shownZoom = 1f;

        [Header("Look ahead (while the cursor is held)")]
        [Tooltip("Metres the camera moves forward, along where it looks.")]
        [SerializeField] private float _lookAhead = 1f;
        [Tooltip("How much further out along its offset the camera stands: 0.2 is 20 % further. Keeps the " +
                 "tank on the screen while the view looks ahead.")]
        [SerializeField] private float _pullBack = 0.2f;
        [Tooltip("Degrees the view turns up towards the horizon, where it looks.")]
        [SerializeField] private float _lookTilt = 2.55f;

        private IVariable<Vector3> _offset;
        private IReactiveVariable<IEntity> _character;
        private IPlayerInput _input;
        private Transform _rig;
        // The camera under the rig and its spot there from the scene: the wheel slides it in and out.
        private Transform _cameraChild;
        private Vector3 _childRest;
        private Vector3 _childOrbit;
        private Vector3 _childLead;
        private Vector3 _childRight;
        // Where the view's middle is pushed about the tank (m, camera's right and forward on the ground), and shown eased.
        private Vector2 _peek;
        private Vector2 _shownPeek;
        private Vector3 _baseOffset;
        private Quaternion _baseRotation;
        // Where the scene's camera looks, flat on the ground, and its right: the tilt turns about it.
        private Vector3 _baseFacing = Vector3.forward;
        // Furthest the mouse may tilt the camera back: _tiltBack, or less where it would pass over the top.
        private float _maxPitch;
        // Where the rig stood in the scene, before the follow first moved it.
        private Vector3 _sceneRigPosition;
        private Transform _sceneRig;
        private float _yaw;
        private float _pitch;
        private float _shownYaw;
        private float _shownPitch;
        // 0..1: how much the camera looks ahead; eases in while the cursor is held, out when it is let go.
        private float _ahead;
        private float _lastClick = -1f;
        private bool _holding;

        private void Awake()
        {
            // The follow starts moving the rig on the first frame; the scene's spot is taken before.
            Camera main = Camera.main;
            if (main == null)
                return;

            _sceneRig = main.transform.parent != null ? main.transform.parent : main.transform;
            _sceneRigPosition = _sceneRig.position;
        }

        private IEnumerator Start()
        {
            // The player context is installed by the time the scene's first frame is over.
            yield return null;

            if (!GameContext.TryGetInstance(out GameContext game) || !game.TryGetPlayerContext(out PlayerContext player))
            {
                Debug.LogWarning("[VoxelSandboxOrbit] No player context: no orbit.");
                enabled = false;
                yield break;
            }

            _offset = player.GetCameraOffset() as IVariable<Vector3>;
            _rig = player.GetCameraRoot();
            Camera view = Camera.main;
            if (view != null && view.transform != _rig && view.transform.IsChildOf(_rig))
            {
                _cameraChild = view.transform;
                _childRest = _cameraChild.localPosition;
            }
            _character = player.GetCharacter();
            player.TryGetPlayerInput(out _input);
            if (_offset == null || _rig == null || _input == null)
            {
                Debug.LogWarning("[VoxelSandboxOrbit] No camera offset variable, rig or player input: no orbit.");
                enabled = false;
                yield break;
            }

            // As the scene shows it: from the tank's camera point, which the follow keeps the rig
            // round, to where the rig stood. The installer's offset stays the fallback.
            _baseOffset = _offset.Value;
            VoxelSandboxStart start = GetComponent<VoxelSandboxStart>();
            if (_sceneRig == _rig && start != null && start.Vehicle != null
                && start.Vehicle.TryGetCameraPoint(out Transform point) && point != null)
                _baseOffset = _sceneRigPosition - point.position;

            // The rig is not turned by anything else.
            _baseRotation = _rig.rotation;

            // Forward is taken once, flat, from the scene's view (else from the camera towards the
            // tank). Taken from the tilted rig every frame, it turned round once the rig, already
            // 70° down, tilted past straight down.
            Vector3 facing = _baseRotation * Vector3.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f)
                facing = new Vector3(-_baseOffset.x, 0f, -_baseOffset.z);
            if (facing.sqrMagnitude > 1e-6f)
                _baseFacing = facing.normalized;

            // The circle (not tilting): the camera's spot from the tank's camera point, and the step ahead along the
            // view, both in the rig's frame, so turning the rig turns them round the tank.
            if (_cameraChild != null)
            {
                Quaternion toRig = Quaternion.Inverse(_baseRotation);
                // From the tank's middle, where the rig stands now: the scene's pose stays where the user put it.
                _childOrbit = toRig * (_baseOffset - ToTankMiddle()) + _childRest;
                _childLead = toRig * _baseFacing;
                _childRight = toRig * Vector3.Cross(Vector3.up, _baseFacing);
            }

            // Tilting back must not take the camera over the top of the tank.
            float elevation = Mathf.Atan2(_baseOffset.y, new Vector2(_baseOffset.x, _baseOffset.z).magnitude) * Mathf.Rad2Deg;
            _maxPitch = Mathf.Clamp(Mathf.Min(_tiltBack, 88f - elevation), 0f, _tiltBack);

            Hold();
        }

        private void OnDisable() => Release();

        // The peek's nod (the user): pushed towards the bottom of the screen the camera turns down to it, up to
        // _nodDown degrees at the disc's edge; towards the top, up by as much as _nodUp; a gradient between, none in the
        // middle. On top of the turn the follow gave the camera this frame (it writes it whole every frame).
        private void LateUpdate()
        {
            if (!NOD_ON || _offset == null || _tiltWithMouse || _cameraChild == null || _peekRadius <= 0f)
                return;
            float t = Mathf.Clamp(_shownPeek.y / _peekRadius, -1f, 1f);
            float nod = t < 0f ? -t * _nodDown : -t * _nodUp;
            _cameraChild.localRotation *= Quaternion.Euler(nod, 0f, 0f);
            _debugNod = nod;
        }

        private void Update()
        {
            if (_offset == null)
                return;

            if (_input.ResetLookPressed)
            {
                if (Time.unscaledTime - _lastClick < 0.3f)
                {
                    _yaw = _pitch = 0f;
                    _peek = Vector2.zero;
                }
                _lastClick = Time.unscaledTime;
            }

            if (_input.FreeCursorPressed)
                Release();
            else if (Cursor.lockState != CursorLockMode.Locked && Application.isFocused
                     && _input.HoldCursorPressed)
                Hold();

            // Only a held cursor turns the camera: a free one is aiming, or off in another window.
            bool held = Cursor.lockState == CursorLockMode.Locked;
            // Not tilting (the user): the scene's angle and spot stay, only the swing round the tank is the mouse's.
            if (!_tiltWithMouse)
                _pitch = 0f;
            if (held)
            {
                Vector2 look = _input.Look;
                if (_tiltWithMouse || _peekRadius <= 0f)
                    _yaw += look.x * _turnSpeed * 10f;
                else
                {
                    // Peek (the user): the mouse moves the view's middle about the tank within the disc; pushed on past
                    // its edge sideways, it turns the camera round the tank as before.
                    float degrees = _turnSpeed * 10f;
                    Vector2 peek = _peek + look * (degrees * _peekPerDegree);
                    if (peek.magnitude > _peekRadius)
                    {
                        Vector2 inside = peek.normalized * _peekRadius;
                        float overX = peek.x - inside.x;
                        _yaw += overX / _peekPerDegree;
                        peek = inside;
                    }
                    _peek = peek;
                }
                // Mouse up tilts the camera towards the horizon; mouse down, up over the tank. With the
                // view not turning, up has nothing to do past the start.
                float lowest = _turnView ? -_tiltAhead : 0f;
                if (_tiltWithMouse)
                    _pitch = Mathf.Clamp(_pitch - look.y * _turnSpeed * 10f, lowest, _maxPitch);
            }

            float blend = 1f - Mathf.Exp(-_ease * Time.unscaledDeltaTime);
            _shownYaw = Mathf.LerpAngle(_shownYaw, _yaw, blend);
            _shownPitch = Mathf.Lerp(_shownPitch, _pitch, blend);
            // No look ahead while not tilting: it moved the camera off the scene's spot.
            _ahead = Mathf.Lerp(_ahead, held && _tiltWithMouse ? 1f : 0f, blend);

            // Swing round the vertical, then tilt about the camera's own right axis: the offset and
            // the rig turn together, so the view stays on the target.
            Quaternion yaw = Quaternion.Euler(0f, _shownYaw, 0f);
            Vector3 facing = yaw * _baseFacing;
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            // Not turning the view, the camera does not swing up round the tank either: swung up with
            // the view kept, it looked past the tank, which slid to the bottom of the screen.
            Quaternion tilt = _turnView ? Quaternion.AngleAxis(_shownPitch, right) : Quaternion.identity;
            Quaternion orbit = tilt * yaw;
            Quaternion rig = orbit * _baseRotation;

            // Tilted back, the camera also steps back, away from the view, and up, as far as it is tilted.
            float tiltedBack = _maxPitch > 0f ? Mathf.Clamp01(_shownPitch / _maxPitch) : 0f;

            // Looking ahead: forward along the view, flat on the ground, a step further out, and
            // the view turned up about its own right axis.
            Vector3 offset = orbit * _baseOffset * (1f + _pullBack * _ahead)
                             + facing * (_lookAhead * _ahead - _backShift * tiltedBack)
                             + Vector3.up * (_backRise * tiltedBack);

            // The wheel brings the camera in and out along its offset (the user).
            Mouse mouse = Mouse.current;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(wheel) > 0.01f)
                // By the sign only: the Input System gives ±1 or ±120 a notch depending on the platform and version.
                _zoom = Mathf.Clamp(_zoom * (wheel > 0f ? 1f - _zoomPerNotch : 1f / (1f - _zoomPerNotch)), _zoomMin, _zoomMax);
            _shownZoom = Mathf.Lerp(_shownZoom, _zoom, blend);

            if (!_tiltWithMouse && _cameraChild != null)
            {
                // On a circle round the tank (the user): the rig stands on the tank's camera point and only follows it;
                // the camera under it holds the whole distance, so the mouse's swing turns it round the tank at once,
                // not along the follow's straight easing. The view's middle is ahead of the tank by the lead, the way
                // the camera faces: more of the side the player looks to is on screen. The wheel changes the radius.
                // The rig on the tank's middle (the user), not on its camera point a metre ahead of the hull: the circle and
                // the peek's disc go round the tank itself.
                _offset.Value = ToTankMiddle() / CameraDistance();
                _shownPeek = Vector2.Lerp(_shownPeek, _peek, blend);
                _cameraChild.localPosition = _childOrbit * _shownZoom + _childLead * (_lookLead + _shownPeek.y)
                                             + _childRight * _shownPeek.x;
                _rig.rotation = yaw * _baseRotation;
                DrawDebug();
                return;
            }

            // The follow scales the offset by the vehicle's CameraDistance; dividing it out here
            // keeps the camera the scene's offset away.
            _offset.Value = offset * (_cameraChild != null ? 1f : _shownZoom) / CameraDistance();
            if (_cameraChild != null)
                _cameraChild.localPosition = _childRest;
            float lookTilt = _turnView ? _lookTilt : 0f;
            _rig.rotation = Quaternion.AngleAxis(-lookTilt * _ahead, right) * rig;
        }

        // ---- Temporary debug (the user): the camera's circle round the tank on the ground, and a lead slider ----

        private const bool DEBUG_CIRCLE = true;
        private const int CIRCLE_POINTS = 64;
        private LineRenderer _circle;
        private LineRenderer _leadLine;
        private float _debugRadius;
        private float _debugHeight;
        private float _debugAhead;
        private GUIStyle _debugStyle;

        private void DrawDebug()
        {
            if (!DEBUG_CIRCLE || _cameraChild == null)
                return;

            _circle ??= MakeLine("[CameraCircle]", new Color(1f, 0.85f, 0.1f), true);
            _leadLine ??= MakeLine("[CameraLead]", new Color(0.2f, 1f, 0.9f), false);

            // The circle the camera goes round: on the ground under the rig (the tank's camera point), as wide as the
            // camera stands from it flat.
            // The peek shifts the whole circle off the tank; the blue disc is how far it may go, the red ring its middle.
            Vector3 tank = _rig.position;
            Vector3 peekWorld = _rig.rotation * (_childRight * _shownPeek.x + _childLead * _shownPeek.y);
            Vector3 centre = tank + peekWorld;
            Vector3 eye = _cameraChild.position;
            Vector3 flat = eye - centre;
            flat.y = 0f;
            _debugRadius = flat.magnitude;
            _debugHeight = eye.y - centre.y;
            float y = GroundY(centre) + 0.15f;
            _circle.positionCount = CIRCLE_POINTS;
            for (int i = 0; i < CIRCLE_POINTS; i++)
            {
                float a = i * Mathf.PI * 2f / CIRCLE_POINTS;
                _circle.SetPosition(i, new Vector3(centre.x + Mathf.Cos(a) * _debugRadius, y, centre.z + Mathf.Sin(a) * _debugRadius));
            }
            Ring(ref _peekDisc, "[PeekDisc]", new Color(0.3f, 0.6f, 1f), tank, _peekRadius, y);
            Ring(ref _peekMiddle, "[PeekMiddle]", new Color(1f, 0.25f, 0.2f), centre, 0.35f, y);

            // Where the view's middle meets the ground: the lead from the tank to it.
            Vector3 forward = _cameraChild.forward;
            Vector3 middle = centre;
            if (forward.y < -0.01f)
                middle = eye + forward * ((y - eye.y) / forward.y);
            Vector3 ahead = middle - tank;
            ahead.y = 0f;
            _debugAhead = ahead.magnitude;
            _leadLine.positionCount = 2;
            _leadLine.SetPosition(0, new Vector3(tank.x, y, tank.z));
            _leadLine.SetPosition(1, new Vector3(middle.x, y, middle.z));
        }

        private LineRenderer _peekDisc;
        private LineRenderer _peekMiddle;

        private void Ring(ref LineRenderer line, string name, Color color, Vector3 centre, float radius, float y)
        {
            line ??= MakeLine(name, color, true);
            line.positionCount = CIRCLE_POINTS;
            for (int i = 0; i < CIRCLE_POINTS; i++)
            {
                float a = i * Mathf.PI * 2f / CIRCLE_POINTS;
                line.SetPosition(i, new Vector3(centre.x + Mathf.Cos(a) * radius, y, centre.z + Mathf.Sin(a) * radius));
            }
        }

        private static float GroundY(Vector3 above) =>
            Physics.Raycast(above + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : above.y - 1f;

        private LineRenderer MakeLine(string name, Color color, bool loop)
        {
            LineRenderer line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.loop = loop;
            line.widthMultiplier = 0.12f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default");
            line.material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            line.startColor = line.endColor = color;
            return line;
        }

        private void OnGUI()
        {
            if (!DEBUG_CIRCLE || _cameraChild == null || _tiltWithMouse)
                return;
            _debugStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.UpperLeft };
            Rect box = new(Screen.width * 0.5f - 440f, 8f, 880f, 80f);
            GUI.Box(box, $"  Камера: радиус {_debugRadius:F1} м, высота {_debugHeight:F1} м, центр кадра впереди {_debugAhead:F1} м, подсмотр {_peek.magnitude:F1}/{_peekRadius:F1} м, наклон {_debugNod:+0.0;-0.0;0}°", _debugStyle);
            GUI.Label(new Rect(box.x + 10f, box.y + 32f, 140f, 20f), $"Сдвиг вперёд {_lookLead:F1}");
            _lookLead = GUI.HorizontalSlider(new Rect(box.x + 150f, box.y + 38f, 590f, 16f), _lookLead, 0f, 8f);
            GUI.Label(new Rect(box.x + 10f, box.y + 54f, 140f, 20f), $"Радиус подсмотра {_peekRadius:F1}");
            _peekRadius = GUI.HorizontalSlider(new Rect(box.x + 150f, box.y + 60f, 590f, 16f), _peekRadius, 0f, 8f);
        }

        // From the tank's camera point (which the follow keeps the rig on) to the middle of its hull, flat.
        private Vector3 ToTankMiddle()
        {
            IEntity character = _character?.Value;
            if (character == null || !character.TryGetCameraPoint(out Transform point) || point == null
                || !character.TryGetTransform(out Transform body) || body == null)
                return Vector3.zero;
            Vector3 middle = body.position;
            if (body.TryGetComponent(out Rigidbody rb))
                middle = rb.worldCenterOfMass;
            Vector3 to = middle - point.position;
            to.y = 0f;
            return to;
        }

        private float CameraDistance()
        {
            IEntity character = _character?.Value;
            return character != null && character.TryGetCameraDistance(out float distance) && distance > 0.01f
                ? distance
                : 1f;
        }

        private void Hold()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _holding = true;
        }

        private void Release()
        {
            if (!_holding)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _holding = false;
        }
    }
}

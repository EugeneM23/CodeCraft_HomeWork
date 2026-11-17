using System.Collections;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

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
    /// scene's angle. Left and right still swing it round the tank. A crosshair in the middle of the
    /// screen aims: while the cursor is locked, the
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
            // The crosshair is drawn with GUI.DrawTexture only: no layout pass, no garbage for it.
            useGUILayout = false;

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

            if (GameContext.Instance == null)
                yield break;

            IPlayerContext player = GameContext.Instance.GetPlayerContext();
            _offset = player.GetCameraOffset() as IVariable<Vector3>;
            _rig = player.GetCameraRoot();
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

            // Tilting back must not take the camera over the top of the tank.
            float elevation = Mathf.Atan2(_baseOffset.y, new Vector2(_baseOffset.x, _baseOffset.z).magnitude) * Mathf.Rad2Deg;
            _maxPitch = Mathf.Clamp(Mathf.Min(_tiltBack, 88f - elevation), 0f, _tiltBack);

            Hold();
        }

        private void OnDisable() => Release();

        private void Update()
        {
            if (_offset == null)
                return;

            if (_input.ResetLookPressed)
            {
                if (Time.unscaledTime - _lastClick < 0.3f)
                    _yaw = _pitch = 0f;
                _lastClick = Time.unscaledTime;
            }

            if (_input.FreeCursorPressed)
                Release();
            else if (Cursor.lockState != CursorLockMode.Locked && Application.isFocused
                     && _input.HoldCursorPressed)
                Hold();

            // Only a held cursor turns the camera: a free one is aiming, or off in another window.
            bool held = Cursor.lockState == CursorLockMode.Locked;
            if (held)
            {
                Vector2 look = _input.Look;
                _yaw += look.x * _turnSpeed * 10f;
                // Mouse up tilts the camera towards the horizon; mouse down, up over the tank. With the
                // view not turning, up has nothing to do past the start.
                float lowest = _turnView ? -_tiltAhead : 0f;
                _pitch = Mathf.Clamp(_pitch - look.y * _turnSpeed * 10f, lowest, _maxPitch);
            }

            float blend = 1f - Mathf.Exp(-_ease * Time.unscaledDeltaTime);
            _shownYaw = Mathf.LerpAngle(_shownYaw, _yaw, blend);
            _shownPitch = Mathf.Lerp(_shownPitch, _pitch, blend);
            _ahead = Mathf.Lerp(_ahead, held ? 1f : 0f, blend);

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

            // The follow scales the offset by the vehicle's CameraDistance; dividing it out here
            // keeps the camera the scene's offset away.
            _offset.Value = offset / CameraDistance();
            float lookTilt = _turnView ? _lookTilt : 0f;
            _rig.rotation = Quaternion.AngleAxis(-lookTilt * _ahead, right) * rig;
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

        private void OnGUI()
        {
            if (_offset == null || Cursor.lockState != CursorLockMode.Locked
                || UnityEngine.Event.current.type != EventType.Repaint)
                return;

            // A cross with a gap in the middle, white on a dark outline: it shows on sky and on snow.
            Vector2 centre = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Cross(centre, 6f, 9f, 4f, new Color(0f, 0f, 0f, 0.55f));
            Cross(centre, 7f, 7f, 2f, new Color(1f, 1f, 1f, 0.9f));
        }

        private static void Cross(Vector2 centre, float gap, float length, float thickness, Color color)
        {
            Color was = GUI.color;
            GUI.color = color;
            float half = thickness * 0.5f;
            Texture texture = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(centre.x - gap - length, centre.y - half, length, thickness), texture);
            GUI.DrawTexture(new Rect(centre.x + gap, centre.y - half, length, thickness), texture);
            GUI.DrawTexture(new Rect(centre.x - half, centre.y - gap - length, thickness, length), texture);
            GUI.DrawTexture(new Rect(centre.x - half, centre.y + gap, thickness, length), texture);
            GUI.color = was;
        }
    }
}

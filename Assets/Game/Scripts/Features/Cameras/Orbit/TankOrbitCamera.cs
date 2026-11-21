using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// Experiment (the user): a third-person camera like World of Tanks'. It sits on a camera object in the sandbox scene:
    /// the user places that camera where they like it behind the scene's tank, and the Game view shows the shot while
    /// editing. In Play the camera's pose against the tank is the start — its height, distance, angle and framing — and
    /// from there the mouse swings it round the tank (yaw, pitch about the point it looks at over the tank), the wheel
    /// brings it in and out, and the turret turns to where the screen's centre looks. The reticle sits in the middle; a
    /// ring shows where the gun points now. V switches between it and the game's camera from above.
    ///
    /// The scene object's own Camera is only the preview: in Play it is switched off and the game's main camera is moved
    /// (it runs after everything else and writes the rig over the follow and the sandbox's orbit). Switched off, it hands
    /// the rig back as it found it.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(Camera))]
    public sealed class TankOrbitCamera : MonoBehaviour
    {
        [Header("Limits")]
        [Tooltip("Nearest the wheel brings the camera, m (the scene's distance is always allowed).")]
        [SerializeField] private float _minDistance = 5f;
        [Tooltip("Furthest the wheel takes it, m.")]
        [SerializeField] private float _maxDistance = 28f;
        [Tooltip("Most it looks up, degrees (negative: from below the horizon).")]
        [SerializeField] private float _minPitch = -12f;
        [Tooltip("Most it looks down onto the tank, degrees.")]
        [SerializeField] private float _maxPitch = 85f;

        [Header("Feel")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField] private float _sensitivity = 0.12f;
        [Tooltip("Share of the distance one wheel notch zooms in or out (0.15 — 15 %).")]
        [SerializeField] private float _zoomPerNotch = 0.15f;
        [SerializeField] private float _zoomEase = 10f;
        [Tooltip("How fast the orbit's centre catches up with the tank (higher is stiffer).")]
        [SerializeField] private float _follow = 25f;
        [Tooltip("Radius of the camera's ball that walls and ground pull in, m.")]
        [SerializeField] private float _cameraRadius = 0.3f;

        private const float AIM_DISTANCE = 250f;     // where the reticle aims at the sky
        private const float DEFAULT_HEIGHT = 2.5f;   // the orbit's centre over the tank when the camera looks straight down

        private static readonly RaycastHit[] s_hits = new RaycastHit[32];

        /// <summary>Switched off for now (the user): the sandbox plays with the arcade camera, and V does nothing.</summary>
        private const bool AVAILABLE = false;

        /// <summary>The realistic camera is on (V toggles while it is <see cref="AVAILABLE"/>).</summary>
        public static bool On { get; set; } = AVAILABLE;

        private PlayerContext _player;
        private Transform _rig;
        private Transform _camera;
        private Camera _view;
        private Behaviour _orbit;

        // The scene camera against the scene's tank, read once in Awake before anything moves.
        private bool _hasPose;
        private Vector3 _pivotLocal = Vector3.up * DEFAULT_HEIGHT; // the turret's middle, in the hull's frame
        private float _startDistance = 13f;
        private float _startPitch = 14f;
        private float _startYaw;                     // against the tank's heading
        private Quaternion _framing = Quaternion.identity; // the camera's turn off looking straight at the centre
        private float _fieldOfView;

        private Quaternion _savedRigRotation;
        private Quaternion _savedCameraRotation;
        private float _savedFieldOfView;
        private bool _active;

        private float _yaw;
        private float _pitch;
        private float _distance;
        private float _shownDistance;
        private Vector3 _pivot;
        private bool _hasPivot;

        private Vector3 _aimPoint;
        private Vector3 _gunPoint;
        private bool _hasGunPoint;
        private GUIStyle _style;
        private bool _onTarget;
        private Texture2D _ring;
        private Texture2D _dot;

        [Header("Gun laying (realistic)")]
        [Tooltip("Most the gun lifts, degrees (the Tiger: 15–20).")]
        [SerializeField] private float _gunElevation = 20f;
        [Tooltip("Most the gun dips, degrees (the Tiger: 8).")]
        [SerializeField] private float _gunDepression = 8f;
        [Tooltip("Degrees per second the gun is laid up and down.")]
        [SerializeField] private float _gunLaySpeed = 20f;

        private Transform _cannon;
        private Quaternion _cannonRest;
        private TankGunSwayBehaviour _sway;
        private float _elevation;
        private float _wantedElevation;

        private const int START_FRAMES = 3;
        private int _boundFrame;

        private const float RING_SIZE = 44f;       // the gun marker, px
        private const float ON_TARGET_ANGLE = 0.6f; // the barrel this close to the camera's point counts as on it, degrees

        private void Awake()
        {
            Camera preview = GetComponent<Camera>();
            _fieldOfView = preview.fieldOfView;
            // Only the preview: the game's camera is the one that moves.
            preview.enabled = false;
            ReadPose();
        }

        // The scene camera against the scene's tank: the orbit's centre is where its view passes over the tank, and
        // its distance, angle and side, and how it frames that centre, are what the user set.
        private void ReadPose()
        {
            TankInstaller tank = FindAnyObjectByType<TankInstaller>();
            if (tank == null)
                return;

            Transform body = tank.transform;
            Vector3 eye = transform.position;
            // The camera orbits the middle of the turret (the user).
            _pivotLocal = tank.TurretCentreLocal();
            Vector3 pivot = body.TransformPoint(_pivotLocal);
            Vector3 offset = eye - pivot;
            _startDistance = Mathf.Max(offset.magnitude, 1f);
            _startPitch = Mathf.Asin(Mathf.Clamp(offset.y / _startDistance, -1f, 1f)) * Mathf.Rad2Deg;
            float lookYaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
            float tankYaw = Mathf.Atan2(body.forward.x, body.forward.z) * Mathf.Rad2Deg;
            _startYaw = Mathf.DeltaAngle(tankYaw, lookYaw);
            Quaternion straight = Quaternion.Euler(_startPitch, lookYaw, 0f);
            _framing = Quaternion.Inverse(straight) * transform.rotation;
            _hasPose = true;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (AVAILABLE && keyboard != null && keyboard.vKey.wasPressedThisFrame)
                On = !On;
        }

        private void LateUpdate()
        {
            if (!Bind())
                return;

            // A few frames for the arcade camera (VoxelSandboxOrbit) to take the scene's angle first: it does so after its
            // first frame, and taken from a rig this camera had already turned, it never looked down at the tank again.
            if (Time.frameCount - _boundFrame < START_FRAMES)
                return;

            IEntity tank = _player.GetCharacter().Value;
            if (!On || tank == null || !tank.TryGetTransform(out Transform body))
            {
                Deactivate();
                return;
            }

            Activate(body);
            Look();
            Place(body);
            Aim(tank, body);
        }

        private void OnDisable() => Deactivate();

        private bool Bind()
        {
            if (_player != null && _rig != null && _camera != null)
                return true;
            if (!GameContext.TryGetInstance(out GameContext game) || !game.TryGetPlayerContext(out _player))
                return false;
            _rig = _player.GetCameraRoot();
            _view = Camera.main;
            _camera = _view != null ? _view.transform : null;
            _orbit = FindAnyObjectByType<VoxelSandboxOrbit>();
            if (_rig == null || _camera == null)
                return false;
            // The arcade camera's angle as the scene sets it, before anything turns the rig: switched back, it looks
            // down at the tank as it did. Saved on switching instead, it kept this camera's angle.
            _savedRigRotation = _rig.rotation;
            _savedCameraRotation = _camera.localRotation;
            _savedFieldOfView = _view.fieldOfView;
            _boundFrame = Time.frameCount;
            return true;
        }

        private void Activate(Transform body)
        {
            if (_active)
                return;
            _active = true;
            _view.fieldOfView = _fieldOfView;
            // The sandbox's orbit swings the same rig; it rests while this camera runs.
            if (_orbit != null)
                _orbit.enabled = false;

            // From the scene camera's pose, turned with the tank as it faces now.
            float tankYaw = Mathf.Atan2(body.forward.x, body.forward.z) * Mathf.Rad2Deg;
            _yaw = tankYaw + (_hasPose ? _startYaw : 0f);
            _pitch = Mathf.Clamp(_startPitch, _minPitch, _maxPitch);
            _distance = _shownDistance = _startDistance;
            _hasPivot = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Deactivate()
        {
            if (!_active)
                return;
            _active = false;
            ResetElevation();
            if (_rig != null)
                _rig.rotation = _savedRigRotation;
            if (_camera != null)
                _camera.localRotation = _savedCameraRotation;
            if (_view != null)
                _view.fieldOfView = _savedFieldOfView;
            if (_orbit != null)
                _orbit.enabled = true;
            CameraFollowController.RequestSnap();
        }

        private void Look()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            if (Cursor.lockState != CursorLockMode.Locked && Application.isFocused && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            // The wheel zooms with the cursor held or not.
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
                // By the sign only: the Input System gives ±1 or ±120 a notch depending on the platform and version.
                _distance = Mathf.Clamp(_distance * (wheel > 0f ? 1f - _zoomPerNotch : 1f / (1f - _zoomPerNotch)),
                    // A scene camera set nearer than the limit still zooms in, to 60 % of its distance.
                    Mathf.Min(_minDistance, _startDistance * 0.6f), Mathf.Max(_maxDistance, _startDistance));

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * _sensitivity;
            _pitch = Mathf.Clamp(_pitch - delta.y * _sensitivity, _minPitch, _maxPitch);
        }

        private void Place(Transform body)
        {
            // The turret's middle: it turns with the hull's heading but not its rocking, which would shake the view.
            Vector3 flatForward = body.forward;
            flatForward.y = 0f;
            Quaternion heading = flatForward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(flatForward) : Quaternion.identity;
            Vector3 target = body.position + heading * Vector3.Scale(_pivotLocal, body.lossyScale);
            float t = 1f - Mathf.Exp(-_follow * Time.deltaTime);
            _pivot = _hasPivot ? Vector3.Lerp(_pivot, target, t) : target;
            _hasPivot = true;
            _shownDistance = Mathf.Lerp(_shownDistance, _distance, 1f - Mathf.Exp(-_zoomEase * Time.deltaTime));

            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = orbit * Vector3.back;

            // Walls and ground between the tank and the camera pull it in (the tank's own colliders do not).
            float distance = _shownDistance;
            int count = Physics.SphereCastNonAlloc(_pivot, _cameraRadius, back, s_hits, distance, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.transform.IsChildOf(body) || (hit.rigidbody != null && !hit.rigidbody.isKinematic))
                    continue;
                if (hit.distance > 0.01f && hit.distance < distance)
                    distance = hit.distance;
            }

            _rig.SetPositionAndRotation(_pivot + back * Mathf.Max(distance, 1f), orbit * _framing);
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
        }

        private void Aim(IEntity tank, Transform body)
        {
            // What the reticle is on: the first thing the ray from the screen's centre hits past the tank.
            Ray ray = new(_camera.position, _camera.forward);
            _aimPoint = ray.GetPoint(AIM_DISTANCE);
            float best = AIM_DISTANCE;
            int count = Physics.RaycastNonAlloc(ray, s_hits, AIM_DISTANCE, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.transform.IsChildOf(body) || hit.distance >= best)
                    continue;
                best = hit.distance;
                _aimPoint = hit.point;
            }

            Transform pivot = tank.TryGetTurretPivot(out Transform turret) ? turret : body;
            Vector3 direction = _aimPoint - pivot.position;
            if (direction.sqrMagnitude > 0.01f && tank.TryGetRotateDirection(out IReactiveVariable<Vector3> aim))
                aim.Value = direction.normalized;
            // Not CombatFocus: that is the right button's focus fire, and set every frame it kept the machine gun firing.

            // The gun is laid up or down onto the camera's point at its own speed, within its limits (World of Tanks):
            // the cannon with its fire point, so the shell flies that way, and the visible gun with it.
            Transform muzzle = pivot;
            if (tank.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value is Component cannonPart)
            {
                Elevate(tank, pivot, cannonPart.transform);
                if (cannon.Value.TryGetFirePoint(out Transform firePoint) && firePoint != null)
                    muzzle = firePoint;
            }

            // Where the gun points now (the gun marker): what a ray along the barrel from the muzzle hits.
            Vector3 along = muzzle.forward;
            _gunPoint = muzzle.position + along * AIM_DISTANCE;
            best = AIM_DISTANCE;
            count = Physics.RaycastNonAlloc(muzzle.position, along, s_hits, AIM_DISTANCE, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.transform.IsChildOf(body) || hit.distance >= best)
                    continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic && hit.rigidbody.GetComponentInParent<TankInstaller>() == null)
                    continue; // loose debris
                best = hit.distance;
                _gunPoint = hit.point;
            }
            _hasGunPoint = true;
            // On target: the barrel's line passes within a little of the camera's point (or as near as the limits let it).
            Vector3 want = _aimPoint - muzzle.position;
            _onTarget = Vector3.Angle(new Vector3(along.x, 0f, along.z), new Vector3(want.x, 0f, want.z)) < ON_TARGET_ANGLE
                        && (Mathf.Abs(_elevation - _wantedElevation) < ON_TARGET_ANGLE);
        }

        private void Elevate(IEntity tank, Transform pivot, Transform cannon)
        {
            if (_cannon != cannon)
            {
                ResetElevation();
                _cannon = cannon;
                _cannonRest = cannon.localRotation;
                _sway = tank.TryGetBehaviour(out TankGunSwayBehaviour sway) ? sway : null;
            }

            // The pivot only yaws, level with the world: the angle up to the point is the gun's laying, whatever the hull does.
            Vector3 local = pivot.InverseTransformDirection(_aimPoint - cannon.position);
            float wanted = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            _wantedElevation = Mathf.Clamp(wanted, -_gunDepression, _gunElevation);
            _elevation = Mathf.MoveTowards(_elevation, _wantedElevation, _gunLaySpeed * Time.deltaTime);

            cannon.localRotation = _cannonRest * Quaternion.Euler(-_elevation, 0f, 0f);
            if (_sway != null)
                _sway.Elevation = _elevation;
        }

        // Level again (the arcade camera, or another tank): the cannon as it was, the visible gun on its sway alone.
        private void ResetElevation()
        {
            if (_cannon != null)
                _cannon.localRotation = _cannonRest;
            if (_sway != null)
                _sway.Elevation = 0f;
            _cannon = null;
            _sway = null;
            _elevation = _wantedElevation = 0f;
        }

        private void OnGUI()
        {
            if (!AVAILABLE || _view == null || UnityEngine.Event.current.type != EventType.Repaint)
                return;

            GUI.color = Color.white;
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.8f) }
            };
            GUI.Label(new Rect(Screen.width * 0.5f - 300f, 8f, 600f, 24f),
                (_active ? "Прицел: реалистичный (камера за танком)" : "Прицел: аркадный (камера сверху)")
                + "   V — переключить" + (_active ? ", колесо — зум" : ""), _style);

            if (!_active)
                return;

            _ring ??= MakeRing(64, 3f);
            _dot ??= MakeDot(16);

            // The camera's point: a small dot in the middle of the screen, where the turret turns to.
            Vector2 centre = new(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(centre.x - 3f, centre.y - 3f, 6f, 6f), _dot);

            // The gun marker: a ring with a dot where the barrel points, orange while the turret is still turning, green
            // once it is on the camera's point. It is the aim: a shot goes into the dot.
            if (_hasGunPoint)
            {
                Vector3 screen = _view.WorldToScreenPoint(_gunPoint);
                if (screen.z > 0f)
                {
                    Vector2 at = new(screen.x, Screen.height - screen.y);
                    GUI.color = _onTarget ? new Color(0.45f, 1f, 0.45f, 0.95f) : new Color(1f, 0.55f, 0.2f, 0.95f);
                    GUI.DrawTexture(new Rect(at.x - RING_SIZE * 0.5f, at.y - RING_SIZE * 0.5f, RING_SIZE, RING_SIZE), _ring);
                    GUI.DrawTexture(new Rect(at.x - 3f, at.y - 3f, 6f, 6f), _dot);
                }
            }
            GUI.color = Color.white;
        }

        private static Texture2D MakeRing(int size, float width)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float r = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float a = Mathf.Clamp01(width * 0.5f + 0.5f - Mathf.Abs(d - (r - width * 0.5f)));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeDot(int size)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
            }
            texture.Apply();
            return texture;
        }
    }
}

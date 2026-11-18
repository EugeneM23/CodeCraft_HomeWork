using System.Reflection;
using Game.Voxels;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>
    /// The tank destruction lab (prototype): a target tank (TankDummy) on a turntable before a fixed
    /// camera. Right mouse drag turns the table (and tilts the camera), the wheel zooms; left mouse
    /// fires machine-gun bullets from the camera at the point under the cursor, as the game's bullet
    /// does to voxels (bite, bore through up to three walls, chips: BulletCollisionBehaviour's numbers);
    /// Space fires a cannon shell there (ExplosionUseCase's voxel blast); R puts a fresh target up.
    /// No game context: no run, no horde, no AI.
    /// </summary>
    public sealed class TankLab : MonoBehaviour
    {
        [SerializeField] private GameObject _target;
        [SerializeField] private Transform _table;
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _focus;

        [Tooltip("Height of the turntable's top over its pivot, m.")]
        [SerializeField] private float _tableTop = 0.3f;

        [Tooltip("Seconds between machine-gun bullets.")]
        [SerializeField] private float _fireInterval = 0.12f;

        [SerializeField] private float _bulletSpeed = 120f;

        [Tooltip("The cannon shell's blast radius (TankShell_01).")]
        [SerializeField] private float _shellRadius = 5f;

        [Tooltip("The table's yaw at start, degrees: 215 shows the tank three-quarter from the front left.")]
        [SerializeField] private float _startYaw = 215f;

        private GameObject _current;
        private float _nextShot;
        private float _pitch = 18f;
        private float _yaw;
        private float _distance = 8f;

        // The game's bullet and blast numbers (private consts), read once.
        private float _chipRadius = 0.13f;
        private float _holeRadius = 0.3f;
        private float _tunnel = 0.9f;
        private float _chipForce = 9f;
        private int _pierces = 3;
        private int _debris = 40;
        private float _armour = 0.7f;
        private float _craterShare = 0.32f;
        private float _blastSpeed = 9f;
        private float _smokeShare = 0.7f;

        private struct Bullet
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float Life;
            public int Pierced;
            public Collider Last;
            public Transform Tracer;
        }

        private readonly Bullet[] _bullets = new Bullet[64];
        private int _bulletCount;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake()
        {
            ReadNumbers();
            _table.rotation = Quaternion.Euler(0f, _startYaw, 0f);
            Respawn();
            Place();
        }

        private void ReadNumbers()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            T Const<T>(System.Type type, string name, T fallback)
            {
                FieldInfo field = type.GetField(name, flags);
                return field != null && field.IsLiteral ? (T)field.GetRawConstantValue() : fallback;
            }

            System.Type bullet = typeof(BulletCollisionBehaviour);
            _chipRadius = Const(bullet, "VOXEL_CHIP_RADIUS", _chipRadius);
            _holeRadius = Const(bullet, "WALL_HOLE_RADIUS", _holeRadius);
            _tunnel = Const(bullet, "WALL_TUNNEL_DEPTH", _tunnel);
            _chipForce = Const(bullet, "CHIP_FORCE", _chipForce);
            _pierces = Const(bullet, "MAX_WALL_PIERCES", _pierces);
            _debris = Const(bullet, "BULLET_DEBRIS", _debris);
            _armour = Const(bullet, "ARMOUR_TOUGHNESS", _armour);
            System.Type blast = typeof(ExplosionUseCase);
            _craterShare = Const(blast, "VOXEL_CRATER_SHARE", _craterShare);
            _blastSpeed = Const(blast, "VOXEL_BLAST_SPEED", _blastSpeed);
            _smokeShare = Const(blast, "VOXEL_SMOKE_SHARE", _smokeShare);
        }

        /// <summary>A fresh target on the table (the old one and what lies about go).</summary>
        private void Respawn()
        {
            if (_current != null)
                Destroy(_current);
            foreach (VoxelBody body in FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                if (body.GetComponent<Rigidbody>() != null)
                    Destroy(body.gameObject);
            _current = Instantiate(_target, _table, false);
            // The table is an unscaled pivot; its disc top is at _tableTop.
            _current.transform.localPosition = new Vector3(0f, _tableTop, 0f);
            _current.transform.localRotation = Quaternion.identity;
        }

        private void Update() // perf-ok: the lab's own input and bullets, not the game
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null)
                return;

            // The camera flies round the tank instead of the table turning it: knocked-off armour, crumbs and
            // debris lie in the world, so turning only the tank left them hanging in front of it.
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.3f;
                _pitch = Mathf.Clamp(_pitch - delta.y * 0.15f, -5f, 70f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _distance = Mathf.Clamp(_distance - Mathf.Sign(scroll) * 0.8f, 4f, 25f);
            Place();

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            if (mouse.leftButton.isPressed && Time.time >= _nextShot)
            {
                _nextShot = Time.time + _fireInterval;
                Fire(ray);
            }

            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame && Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                    Shell(hit.point);
                if (keyboard.rKey.wasPressedThisFrame)
                    Respawn();
            }

            Fly(Time.deltaTime);
        }

        /// <summary>The camera on its orbit round the focus: fixed yaw, the pitch and distance the mouse set.</summary>
        private void Place()
        {
            Quaternion look = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.position = _focus.position + look * new Vector3(0f, 0f, -_distance);
            _camera.transform.rotation = look;
        }

        // ---------- Machine gun ----------

        private void Fire(Ray ray)
        {
            if (_bulletCount >= _bullets.Length)
                return;
            Vector3 from = _camera.transform.position + _camera.transform.right * 0.3f - _camera.transform.up * 0.3f;
            Vector3 to = Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(60f);
            Vector3 direction = (to - from).normalized;
            var tracer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tracer.GetComponent<Collider>());
            tracer.transform.localScale = new Vector3(0.03f, 0.03f, 0.6f);
            tracer.GetComponent<Renderer>().sharedMaterial = TracerMaterial();
            _bullets[_bulletCount++] = new Bullet { Position = from, Direction = direction, Life = 2f, Tracer = tracer.transform };
        }

        private static Material s_tracer;

        private static Material TracerMaterial()
        {
            if (s_tracer != null)
                return s_tracer;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            s_tracer = new Material(shader) { color = new Color(1f, 0.8f, 0.3f) };
            return s_tracer;
        }

        /// <summary>Bullets fly on; a voxel wall is bored through (three bites along the tunnel), the last one is chipped.</summary>
        private void Fly(float dt)
        {
            for (int i = 0; i < _bulletCount; i++)
            {
                ref Bullet b = ref _bullets[i];
                float step = _bulletSpeed * dt;
                b.Life -= dt;
                bool alive = b.Life > 0f;
                Vector3 start = b.Position;
                while (alive && step > 0f)
                {
                    int count = Physics.RaycastNonAlloc(start, b.Direction, _hits, step, ~0, QueryTriggerInteraction.Ignore);
                    int best = -1;
                    for (int h = 0; h < count; h++)
                        if (_hits[h].collider != b.Last && (best < 0 || _hits[h].distance < _hits[best].distance))
                            best = h;
                    if (best < 0)
                    {
                        start += b.Direction * step;
                        break;
                    }

                    RaycastHit hit = _hits[best];
                    VoxelBody body = hit.collider.GetComponentInParent<VoxelBody>();
                    if (body != null && !body.IsTerrain && body.Toughness >= _armour && b.Pierced < _pierces)
                    {
                        b.Pierced++;
                        VoxelDamage.Punch(hit.point, b.Direction, _holeRadius, _chipForce, _debris);
                        b.Last = hit.collider;
                        step -= hit.distance;
                        start = hit.point + b.Direction * 0.01f;
                        continue;
                    }

                    // Stopped by armour (or the ground): armour still gets a hole, chips flying in through it.
                    if (body != null && !body.IsTerrain)
                        VoxelDamage.Punch(hit.point, b.Direction, _holeRadius, _chipForce, _debris);
                    else
                        VoxelDamage.Impact(hit.point, b.Direction, _chipRadius, _chipForce, _debris);
                    alive = false;
                }

                b.Position = start;
                if (b.Tracer != null)
                {
                    b.Tracer.position = start;
                    b.Tracer.rotation = Quaternion.LookRotation(b.Direction);
                }

                if (alive)
                    continue;
                if (b.Tracer != null)
                    Destroy(b.Tracer.gameObject);
                _bullets[i] = _bullets[--_bulletCount];
                i--;
            }
        }

        // ---------- Cannon ----------

        private void Shell(Vector3 point)
        {
            VoxelDamage.Blast(point, _shellRadius * _craterShare, _blastSpeed);
            VoxelSmoke.Blast(point, _shellRadius * _smokeShare);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(16, 16, 520, 120),
                "ЛКМ — пулемёт   Space — снаряд пушки   R — новая мишень\nПКМ + мышь — облёт камерой вокруг танка   колесо — ближе/дальше");
        }
    }
}

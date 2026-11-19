using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Experiment: the machine gun's and the turrets' spent casings as small brass voxels (a GameContext behaviour,
    /// added by <see cref="TargetingSetup.InstallContext"/>). A casing flies out tumbling, falls under gravity, bounces
    /// once off the ground and lies there until it is the oldest of <c>WeaponSettings.casingCapacity</c> and is
    /// thrown again. No physics after the spawn: two rays down there find where it lands (one under the spawn, one
    /// under the predicted landing point; the tank's layer is not in the mask, so the casings fall past the hull).
    /// </summary>
    /// <remarks>
    /// Every array is made in the constructor — the pool is warm from the level's start — and nothing allocates per
    /// frame. Drawing as <see cref="XpOrbField"/>: one RenderMeshInstanced of a cube per camera from
    /// RenderPipelineManager.beginCameraRendering with the matrices Tick last built, so the casings stay drawn through
    /// the level-up pause. Only flying casings are stepped; lying ones keep their matrix.
    /// </remarks>
    public sealed class CasingField : IGameContextInit, IGameContextTick, IGameContextDispose
    {
        private const int MAX_BATCH = 1023;
        private const float GRAVITY = 9.81f;
        private const float RAY_UP = 0.5f;
        private const float RAY_LENGTH = 30f;
        private const float FALL_WITHOUT_GROUND = 3f;
        private const float BOUNCE_SPEED = 1f;
        private const float BOUNCE_KEEP_UP = 0.3f;
        private const float BOUNCE_KEEP_FLAT = 0.4f;

        private const byte FREE = 0;
        private const byte FLYING = 1;
        private const byte LYING = 2;

        private static readonly ProfilerMarker s_marker = new("Combat.Casings");
        private static readonly int s_baseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int s_color = Shader.PropertyToID("_Color");
        private static readonly int s_smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int s_metallic = Shader.PropertyToID("_Metallic");

        private readonly WeaponSettings _settings;
        private readonly int _capacity;
        private readonly Vector3[] _position;
        private readonly Vector3[] _velocity;
        private readonly Quaternion[] _rotation;
        private readonly Vector3[] _spinAxis;
        private readonly float[] _spinSpeed;
        private readonly float[] _ground;
        private readonly byte[] _state;
        private readonly byte[] _bounces;
        private readonly Matrix4x4[] _matrices;

        private int _next;
        private int _used;
        private int _flying;
        private Vector3 _min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        private Vector3 _max = new(float.MinValue, float.MinValue, float.MinValue);

        private Mesh _mesh;
        private Material _material;
        private RenderParams _renderParams;
        private Bounds _bounds;
        private bool _drawing;

        public CasingField(WeaponSettings settings)
        {
            _settings = settings ?? new WeaponSettings();
            _capacity = Mathf.Max(1, _settings.casingCapacity);
            _position = new Vector3[_capacity];
            _velocity = new Vector3[_capacity];
            _rotation = new Quaternion[_capacity];
            _spinAxis = new Vector3[_capacity];
            _spinSpeed = new float[_capacity];
            _ground = new float[_capacity];
            _state = new byte[_capacity];
            _bounces = new byte[_capacity];
            _matrices = new Matrix4x4[_capacity];
        }

        /// <summary>Casings in the buffer, flying or lying. For tests.</summary>
        public int Count => _used;

        /// <summary>Casings still in the air. For tests.</summary>
        public int Flying => _flying;

        public void Init(IGameContext context) => CreateLook();

        public void Dispose(IGameContext context)
        {
            if (_drawing)
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _drawing = false;

            if (_material != null)
                Object.Destroy(_material);
            _material = null;
            _mesh = null;
            Clear();
        }

        /// <summary>Forgets every casing.</summary>
        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
                _state[i] = FREE;
            _next = 0;
            _used = 0;
            _flying = 0;
            _min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        }

        /// <summary>
        /// Throws a casing from <paramref name="position"/>: <c>casingEjectSpeed</c> in the frame of
        /// <paramref name="frame"/> (x its right, y its up, z its forward), a little random, plus
        /// <paramref name="inherited"/> (the vehicle's velocity). The casing lies along the frame's forward.
        /// </summary>
        public void Eject(Vector3 position, Quaternion frame, Vector3 inherited)
        {
            if (!_settings.casingsOn)
                return;

            Vector3 speed = _settings.casingEjectSpeed;
            Vector3 local = new(speed.x * Random.Range(0.8f, 1.2f), speed.y * Random.Range(0.8f, 1.2f),
                speed.z + Random.Range(-0.4f, 0.4f));
            Spawn(position, frame * local + inherited, frame);
        }

        private void Spawn(Vector3 position, Vector3 velocity, Quaternion rotation)
        {
            int i = _next;
            _next = (_next + 1) % _capacity;
            if (i >= _used)
                _used = i + 1;
            if (_state[i] == FLYING)
                _flying--;

            _position[i] = position;
            _velocity[i] = velocity;
            _rotation[i] = rotation;
            _spinAxis[i] = Random.onUnitSphere;
            _spinSpeed[i] = Random.Range(400f, 900f);
            _ground[i] = LandingHeight(position, velocity);
            _bounces[i] = 0;
            _state[i] = FLYING;
            _flying++;
            Grow(position);
            WriteMatrix(i);
        }

        /// <summary>The ground under where the casing comes down: a ray under the spawn, then one under the landing.</summary>
        private float LandingHeight(Vector3 position, Vector3 velocity)
        {
            float ground = Ground(position, position.y - FALL_WITHOUT_GROUND);
            float drop = Mathf.Max(0f, position.y - ground);
            float vy = velocity.y;
            float time = (vy + Mathf.Sqrt(vy * vy + 2f * GRAVITY * drop)) / GRAVITY;
            Vector3 landing = position + new Vector3(velocity.x, 0f, velocity.z) * time;
            landing.y = position.y;
            return Ground(landing, ground);
        }

        private float Ground(Vector3 point, float fallback)
        {
            Vector3 origin = point + Vector3.up * RAY_UP;
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RAY_LENGTH, _settings.casingGroundMask,
                QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : fallback;
        }

        public void Tick(IGameContext context, float deltaTime)
        {
            if (_flying == 0 || deltaTime <= 0f)
                return;

            using (s_marker.Auto())
            {
                float half = _settings.casingSize.y * 0.5f;
                for (int i = 0; i < _used; i++)
                {
                    if (_state[i] != FLYING)
                        continue;

                    Vector3 v = _velocity[i];
                    v.y -= GRAVITY * deltaTime;
                    Vector3 p = _position[i] + v * deltaTime;
                    _rotation[i] = Quaternion.AngleAxis(_spinSpeed[i] * deltaTime, _spinAxis[i]) * _rotation[i];

                    float floor = _ground[i] + half;
                    if (p.y <= floor && v.y < 0f)
                    {
                        p.y = floor;
                        if (_bounces[i] == 0 && -v.y > BOUNCE_SPEED)
                        {
                            _bounces[i] = 1;
                            v.y = -v.y * BOUNCE_KEEP_UP;
                            v.x *= BOUNCE_KEEP_FLAT;
                            v.z *= BOUNCE_KEEP_FLAT;
                            _spinSpeed[i] *= 0.5f;
                        }
                        else
                        {
                            Lie(i);
                            _position[i] = p;
                            WriteMatrix(i);
                            continue;
                        }
                    }

                    _velocity[i] = v;
                    _position[i] = p;
                    Grow(p);
                    WriteMatrix(i);
                }

                _bounds.SetMinMax(_min - Vector3.one, _max + Vector3.one);
            }
        }

        /// <summary>Flat on the ground, along where it pointed last.</summary>
        private void Lie(int i)
        {
            _state[i] = LYING;
            _flying--;
            Vector3 forward = _rotation[i] * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
                forward = _rotation[i] * Vector3.right;
            forward.y = 0f;
            _rotation[i] = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
            _velocity[i] = Vector3.zero;
        }

        private void WriteMatrix(int i) => _matrices[i] = Matrix4x4.TRS(_position[i], _rotation[i], _settings.casingSize);

        private void Grow(Vector3 p)
        {
            _min = Vector3.Min(_min, p);
            _max = Vector3.Max(_max, p);
            _bounds.SetMinMax(_min - Vector3.one, _max + Vector3.one);
        }

        // ---------- Drawing ----------

        private void CreateLook()
        {
            if (!Application.isPlaying || _drawing)
                return;

            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_mesh == null || shader == null)
            {
                Debug.LogWarning("[CasingField] No cube mesh or URP Lit shader: casings are not drawn.");
                return;
            }

            Color color = _settings.casingColor;
            _material = new Material(shader) { name = "Casing (runtime)", enableInstancing = true };
            _material.SetColor(s_baseColor, color);
            _material.SetColor(s_color, color);
            _material.SetFloat(s_metallic, 0.85f);
            _material.SetFloat(s_smoothness, 0.6f);

            _renderParams = new RenderParams(_material)
            {
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.BlendProbes
            };

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _drawing = true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext renderContext, Camera camera)
        {
            if (_used == 0 || _material == null || _mesh == null || camera == null)
                return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
                return;

            _renderParams.camera = camera;
            _renderParams.worldBounds = _bounds;
            for (int start = 0; start < _used; start += MAX_BATCH)
            {
                int n = Mathf.Min(MAX_BATCH, _used - start);
                Graphics.RenderMeshInstanced(_renderParams, _mesh, 0, _matrices, n, start);
            }
        }
    }
}

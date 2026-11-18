using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// The level's experience orbs (a GameContext behaviour, added by <see cref="ProgressionSetup"/>). Dead enemies
    /// (IEnemyDeaths.Died) drop orbs worth their kind's experience (<see cref="XpSettings"/>); the tank pulls in those
    /// within the pickup radius (× the magnet's PickupRadiusFactor), and ArenaLeft pulls in all of them. A collected
    /// orb goes to IRunService.AddExperience and CombatSignals / GameSignals.XpPicked. The data is
    /// <see cref="XpOrbBuffer"/>; this class drops, ticks and draws.
    /// </summary>
    /// <remarks>
    /// Drawing: one RenderMeshInstanced of a cube per camera. The GameContext ticks from Atomic's tick manager,
    /// which the game loop drives (AtomicTickBridge): a paused loop — the level-up offer — does not tick it, and a
    /// RenderMesh call lasts one frame, so drawing from Tick would make the orbs vanish under the skill cards. The
    /// draw is therefore issued from RenderPipelineManager.beginCameraRendering for that camera (it runs every
    /// rendered frame, paused or not, before the camera culls) with the matrices Tick last built; it allocates
    /// nothing. While paused the orbs stand still, bob and spin included.
    /// </remarks>
    public sealed class XpOrbField : IGameContextInit, IGameContextTick, IGameContextDispose
    {
        private const int MAX_BATCH = 1023;
        private const float TARGET_HEIGHT = 0.8f;
        private const float RAY_UP = 2f;
        private const float RAY_LENGTH = 6f;

        private static readonly ProfilerMarker s_marker = new("Combat.XpOrbs");
        private static readonly int s_baseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int s_emissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int s_color = Shader.PropertyToID("_Color");
        private static readonly int s_smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int s_metallic = Shader.PropertyToID("_Metallic");

        private readonly XpSettings _settings;
        private readonly XpOrbBuffer _buffer;
        private readonly Matrix4x4[] _matrices;

        private IGameContext _context;
        private IRunService _run;
        private IEnemyDeaths _deaths;

        private Transform _tank;
        private CombatModifiers _modifiers;

        private Mesh _mesh;
        private Material _material;
        private RenderParams _renderParams;
        private Bounds _bounds;
        private int _drawCount;
        private bool _drawing;

        public XpOrbField(XpSettings settings)
        {
            _settings = settings ?? new XpSettings();
            _buffer = new XpOrbBuffer(_settings.capacity, _settings.gravity, _settings.collectDistance,
                _settings.flySpeedMin, _settings.flySpeedMax, _settings.flyAcceleration);
            _matrices = new Matrix4x4[_buffer.Capacity];
        }

        /// <summary>The orbs, for tests and debug views.</summary>
        public XpOrbBuffer Buffer => _buffer;

        /// <summary>The tank that pulls the orbs, or null (no tank: the orbs wait).</summary>
        public void SetTank(Transform tank, CombatModifiers modifiers)
        {
            _tank = tank;
            _modifiers = modifiers;
        }

        public void Init(IGameContext context)
        {
            _context = context;
            Bind();
            CreateLook();
        }

        public void Dispose(IGameContext context)
        {
            Unbind();
            SetTank(null, null);

            if (_drawing)
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _drawing = false;

            if (_material != null)
                Object.Destroy(_material);
            _material = null;
            _mesh = null;
            _drawCount = 0;
            _buffer.Clear();
        }

        public void Tick(IGameContext context, float deltaTime)
        {
            using (s_marker.Auto())
            {
                // The run may come after the field (the core's, installed later): looked up until found
                if (_run == null || _deaths == null)
                    Bind();

                bool hasTank = _tank != null;
                Vector3 target = hasTank ? _tank.position + Vector3.up * TARGET_HEIGHT : Vector3.zero;
                float radius = _settings.pickupRadius * (_modifiers != null ? _modifiers.PickupRadiusFactor : 1f);

                _buffer.Step(deltaTime, target, hasTank, radius);
                Deliver();
                BuildMatrices();
            }
        }

        // ---------- Drops ----------

        private void OnDied(EnemyDeath death)
        {
            int xp = _settings.XpFor(death.Kind);
            if (xp <= 0)
                return;

            int orbs = Mathf.Clamp(_settings.OrbsFor(death.Kind), 1, xp);
            int each = xp / orbs;
            int rest = xp - each * orbs;
            float groundY = GroundHeight(death.Point);
            float spread = orbs > 1 ? 3.5f : 1.2f;

            for (int i = 0; i < orbs; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float speed = Random.Range(0.3f, 1f) * spread;
                var velocity = new Vector3(Mathf.Cos(angle) * speed, Random.Range(3.5f, 5f), Mathf.Sin(angle) * speed);
                Vector3 start = death.Point + Vector3.up * 0.4f;
                _buffer.Spawn(start, velocity, groundY, each + (i < rest ? 1 : 0), Random.value * 10f);
            }
        }

        /// <summary>One ray down at the drop: the orbs land on it, no physics after.</summary>
        private float GroundHeight(Vector3 point)
        {
            Vector3 origin = point + Vector3.up * RAY_UP;
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RAY_LENGTH, _settings.groundMask,
                QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : point.y;
        }

        private void OnArenaLeft(int arena) => _buffer.PullAll();

        private void Deliver()
        {
            for (int i = 0; i < _buffer.CollectedCount; i++)
            {
                _buffer.GetCollected(i, out Vector3 point, out int value);
                _run?.AddExperience(value);
                CombatSignals.RaiseXpPicked(point, value);
                GameSignals.RaiseXpPicked(point);
            }
        }

        // ---------- Drawing ----------

        private void CreateLook()
        {
            if (!Application.isPlaying)
                return;

            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_mesh == null || shader == null)
            {
                Debug.LogWarning("[XpOrbField] No cube mesh or URP Lit shader: experience orbs are not drawn.");
                return;
            }

            Color color = _settings.color;
            _material = new Material(shader) { name = "XpOrb (runtime)", enableInstancing = true };
            _material.SetColor(s_baseColor, color);
            _material.SetColor(s_color, color);
            // Matte: URP Lit's default smoothness puts a white highlight on the cubes.
            _material.SetFloat(s_smoothness, 0.1f);
            _material.SetFloat(s_metallic, 0f);
            _material.EnableKeyword("_EMISSION");
            Color glow = _settings.emissionColor * Mathf.Max(0f, _settings.emissionIntensity);
            glow.a = 1f;
            _material.SetColor(s_emissionColor, glow);
            _material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            _renderParams = new RenderParams(_material)
            {
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off
            };

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _drawing = true;
        }

        private void BuildMatrices()
        {
            int count = _buffer.Count;
            _drawCount = count;
            if (count == 0 || _material == null)
                return;

            float time = GameTicks.Time;
            float half = _settings.orbSize * 0.5f;
            var scale = new Vector3(_settings.orbSize, _settings.orbSize, _settings.orbSize);
            Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < count; i++)
            {
                Vector3 position = _buffer.GetPosition(i);
                float phase = _buffer.GetPhase(i);
                if (_buffer.GetState(i) == XpOrbBuffer.OrbState.Lying)
                    position.y += half + _settings.bobHeight * (1f + Mathf.Sin(time * _settings.bobSpeed + phase));
                else
                    position.y += half;

                Quaternion spin = Quaternion.AngleAxis(time * _settings.spinSpeed + phase * 36f, Vector3.up);
                _matrices[i] = Matrix4x4.TRS(position, spin, scale);
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            _bounds.SetMinMax(min - Vector3.one * _settings.orbSize, max + Vector3.one * _settings.orbSize);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext renderContext, Camera camera)
        {
            if (_drawCount == 0 || _material == null || _mesh == null || camera == null)
                return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
                return;

            _renderParams.camera = camera;
            _renderParams.worldBounds = _bounds;
            for (int start = 0; start < _drawCount; start += MAX_BATCH)
            {
                int n = Mathf.Min(MAX_BATCH, _drawCount - start);
                Graphics.RenderMeshInstanced(_renderParams, _mesh, 0, _matrices, n, start);
            }
        }

        // ---------- Run ----------

        private void Bind()
        {
            if (_context == null)
                return;

            if (_run == null && _context.TryGetRun(out IRunService run) && run != null)
            {
                _run = run;
                _run.ArenaLeft += OnArenaLeft;
            }

            if (_deaths == null && _context.TryGetEnemyDeaths(out IEnemyDeaths deaths) && deaths != null)
            {
                _deaths = deaths;
                _deaths.Died += OnDied;
            }
        }

        private void Unbind()
        {
            if (_run != null)
                _run.ArenaLeft -= OnArenaLeft;
            if (_deaths != null)
                _deaths.Died -= OnDied;
            _run = null;
            _deaths = null;
        }
    }
}

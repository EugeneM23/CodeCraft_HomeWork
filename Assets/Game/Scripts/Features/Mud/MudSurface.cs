using System.Collections.Generic;
using UnityEngine;
using Ticking;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// A floor of mud that remembers what drove, walked and shot into it. Sits on the floor's
    /// collider, so whatever stands on the floor or hits it finds it there, and owns the track map
    /// the ground shader (Game/MudGround) reads: R is how deep the mud is pressed (1 = down to the
    /// collider), G the berm pushed up beside it, B the tread the last track pass left (0.5 ridge,
    /// 1 groove). The map covers the collider's top in world XZ.
    ///
    /// Three kinds of stamp, all only ever deepening, never filling anything back in:
    ///   <see cref="Dig"/> adds a strip's depth: each pass over the same ground goes deeper.
    ///   <see cref="Press"/> makes a strip at least so deep: standing on it is enough.
    ///   <see cref="Dent"/> adds an oval bowl with a rim: craters and footprints.
    /// Values may go past 1; the ground shader clamps. The frame's stamps go out in LateUpdate.
    ///
    /// Also throws clumps of mud (<see cref="Throw"/>) for whatever churns it up: tracks, hits.
    /// They land on the mud's surface, not on the collider beneath it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MudSurface : MonoBehaviour, ILateUpdateListener
    {
        private const float BERM_SHARE = 0.3f;
        private const int PASS_DIG = 0;
        private const int PASS_PRESS = 1;
        private const int PASS_DENT = 2;
        private const int PASS_TREAD = 3;

        /// <summary>Seconds after a clump lands that its splat is pressed into the mud.</summary>
        private const float LANDING_LAG = 0.08f;

        /// <summary>Height (m) above the mud's surface a clump starts from at least.</summary>
        private const float SURFACE_CLEARANCE = 0.08f;

        private static readonly int TracksId = Shader.PropertyToID("_MudTracks");
        private static readonly int RectId = Shader.PropertyToID("_MudTracksRect");
        private static readonly int TexelId = Shader.PropertyToID("_MudTexel");
        private static readonly int ThicknessId = Shader.PropertyToID("_MudThickness");
        private static readonly int MudDryId = Shader.PropertyToID("_MudDry");
        private static readonly int MudWetId = Shader.PropertyToID("_MudWet");
        private static readonly int WetnessId = Shader.PropertyToID("_Wetness");

        [Tooltip("The mud's visible mesh (MudGround), drawn with Game/MudGround.")]
        [SerializeField] private Renderer _renderer;
        [Tooltip("Hidden/Game/MudStamp. Referenced so a build keeps it; found by name if left empty.")]
        [SerializeField] private Shader _stampShader;
        [Tooltip("World size (m) of one texel of the track map.")]
        [SerializeField, Min(0.005f)] private float _texelSize = 0.02f;

        [Header("Clumps")]
        [Tooltip("Clumps of mud thrown up by Throw. Emitted by hand, simulated in world space.")]
        [SerializeField] private ParticleSystem _clumps;
        [Tooltip("The clumps' collision plane, moved just under the mud's surface on start.")]
        [SerializeField] private Transform _clumpFloor;
        [Tooltip("Share of the mud's thickness a landed clump sinks below the surface: just bedded in, about as deep as its own splat. Deeper and the mud swallows it whole.")]
        [SerializeField, Range(0f, 1f)] private float _clumpSink = 0.12f;
        [Tooltip("Clump colours if the mud's material gives none; otherwise they are taken from its own wet and dry mud, so a clump is the ground's colour.")]
        [SerializeField] private Color _clumpDark = new(0.15f, 0.095f, 0.05f, 1f);
        [SerializeField] private Color _clumpLight = new(0.29f, 0.2f, 0.115f, 1f);

        private RenderTexture _map;
        private Material _stampMaterial;
        private CommandBuffer _commands;
        private MaterialPropertyBlock _block;
        private Vector2 _min;
        private Vector2 _size;

        private Batch _digs;
        private Batch _presses;
        private Batch _dents;

        // Where thrown clumps will come down, and when: each leaves a splat in the mud on landing.
        private readonly List<Landing> _landings = new();
        private float _floorHeight;

        // Top of the visible mud (m, world): clumps start above it, or they fly hidden inside it.
        private float _surfaceHeight;

        // The ground shader's relief, read off its material, for SinkAt.
        private float _thickness;
        private float _reliefDepth;
        private float _reliefScale;
        private float _reliefContrast;
        private float _puddleLevel;
        private float _seed;

        public RenderTexture Texture => _map;

        private struct Landing
        {
            public float Time;
            public Vector3 Point;
            public Vector3 Heading;
            public float Size;
        }

        /// <summary>One frame's stamps of one kind, drawn as one mesh.</summary>
        private bool _looped;

        private sealed class Batch
        {
            public readonly Mesh Mesh = new() { name = "MudStamps", indexFormat = IndexFormat.UInt32 };
            public readonly List<Vector3> Vertices = new();
            public readonly List<Vector2> Local = new();
            public readonly List<Vector4> Params = new();
            public readonly List<Vector4> Shape = new();
            public readonly List<int> Indices = new();

            public Batch() => Mesh.MarkDynamic();

            public bool Empty => Vertices.Count == 0;

            public void Upload()
            {
                Mesh.Clear();
                Mesh.SetVertices(Vertices);
                Mesh.SetUVs(0, Local);
                Mesh.SetUVs(1, Params);
                if (Shape.Count == Vertices.Count)
                    Mesh.SetUVs(2, Shape);
                Mesh.SetTriangles(Indices, 0, false);
                Mesh.bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), Vector3.one * 2f);

                Vertices.Clear();
                Local.Clear();
                Params.Clear();
                Shape.Clear();
                Indices.Clear();
            }
        }

        private void Awake()
        {
            Bounds bounds = GetComponent<BoxCollider>().bounds;
            _min = new Vector2(bounds.min.x, bounds.min.z);
            _size = new Vector2(bounds.size.x, bounds.size.z);

            Shader stamp = _stampShader != null ? _stampShader : Shader.Find("Hidden/Game/MudStamp");
            _stampMaterial = new Material(stamp) { hideFlags = HideFlags.HideAndDontSave };
            _commands = new CommandBuffer { name = "Mud stamps" };
            _block = new MaterialPropertyBlock();
            _digs = new Batch();
            _presses = new Batch();
            _dents = new Batch();

            if (_clumpFloor != null && _renderer != null && _renderer.sharedMaterial != null)
            {
                Vector3 floor = _clumpFloor.position;
                floor.y = bounds.max.y + _renderer.sharedMaterial.GetFloat(ThicknessId) * (1f - _clumpSink);
                _clumpFloor.position = floor;
            }

            _floorHeight = _clumpFloor != null ? _clumpFloor.position.y : bounds.max.y;
            _surfaceHeight = bounds.max.y +
                             (_renderer != null && _renderer.sharedMaterial != null
                                 ? _renderer.sharedMaterial.GetFloat(ThicknessId)
                                 : 0f);
            TakeGroundColours();
            TakeRelief();
            CreateMap();
        }

        private void TakeRelief()
        {
            Material material = _renderer != null ? _renderer.sharedMaterial : null;
            if (material == null)
                return;

            _thickness = material.GetFloat(ThicknessId);
            _reliefDepth = material.GetFloat("_ReliefDepth");
            _reliefScale = material.GetFloat("_ReliefScale");
            _reliefContrast = material.GetFloat("_ReliefContrast");
            _puddleLevel = material.GetFloat("_PuddleLevel");
            _seed = material.GetFloat("_Seed");
        }

        /// <summary>
        /// How far (m) below the collider's top the mud's bottom lies at <paramref name="point"/>
        /// (world XZ): 0 on firm ground, up to a whole mud thickness under a puddle, where the mud
        /// is twice as deep. What a track stands on sinks this far. The same sum as MudPuddleSink in
        /// Shaders/Mud/MudCommon.hlsl, which draws it; keep the two alike.
        /// </summary>
        public float SinkAt(Vector3 point)
        {
            if (_thickness <= 0f)
                return 0f;

            float relief = Relief(new Vector2(point.x, point.z));
            return Mathf.Clamp01((_puddleLevel - relief) / 0.006f) * _thickness;
        }

        // ---- MudCommon.hlsl's relief, on the CPU ----

        private float Relief(Vector2 xz)
        {
            float n = Fbm(xz * _reliefScale + Vector2.one * (_seed * 7.31f));
            return Mathf.Clamp((n - 0.5f) * 2f * _reliefContrast, -1f, 1f) * _reliefDepth;
        }

        private static float Frac(float v) => v - Mathf.Floor(v);

        private static float Hash(Vector2 p)
        {
            p = new Vector2(Frac(p.x * 123.34f), Frac(p.y * 456.21f));
            float d = Vector2.Dot(p, p + new Vector2(45.32f, 45.32f));
            p += new Vector2(d, d);
            return Frac(p.x * p.y);
        }

        private static float ValueNoise(Vector2 p)
        {
            Vector2 i = new(Mathf.Floor(p.x), Mathf.Floor(p.y));
            Vector2 f = p - i;
            Vector2 u = new(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y));

            float a = Hash(i);
            float b = Hash(i + new Vector2(1f, 0f));
            float c = Hash(i + new Vector2(0f, 1f));
            float d = Hash(i + new Vector2(1f, 1f));
            return Mathf.Lerp(Mathf.Lerp(a, b, u.x), Mathf.Lerp(c, d, u.x), u.y);
        }

        private static float Fbm(Vector2 p)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            for (int octave = 0; octave < 3; octave++)
            {
                sum += ValueNoise(p) * amplitude;
                p = p * 2.03f + new Vector2(17.1f, 17.1f);
                amplitude *= 0.5f;
            }
            return sum / 0.875f;
        }

        /// <summary>
        /// Clumps are the ground's own mud: their colours come from the mud material's wet and dry
        /// colours at its wetness, a little either side, so a landed clump reads as part of the floor.
        /// </summary>
        private void TakeGroundColours()
        {
            Material material = _renderer != null ? _renderer.sharedMaterial : null;
            if (material == null || !material.HasProperty(MudDryId) || !material.HasProperty(MudWetId))
                return;

            Color dry = material.GetColor(MudDryId);
            Color wet = material.GetColor(MudWetId);
            float wetness = material.HasProperty(WetnessId) ? material.GetFloat(WetnessId) : 0.5f;
            Color ground = Color.Lerp(dry, wet, wetness);
            _clumpDark = Color.Lerp(ground, wet, 0.35f);
            _clumpLight = Color.Lerp(ground, dry, 0.35f);
        }

        private void CreateMap()
        {
            int width = Mathf.CeilToInt(_size.x / _texelSize);
            int height = Mathf.CeilToInt(_size.y / _texelSize);

            if (_map != null)
                _map.Release();

            _map = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "MudTracks",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            _map.Create();

            _commands.Clear();
            _commands.SetRenderTarget(_map);
            _commands.ClearRenderTarget(false, true, Color.clear);
            Graphics.ExecuteCommandBuffer(_commands);

            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_block);
                _block.SetTexture(TracksId, _map);
                _block.SetVector(RectId, new Vector4(_min.x, _min.y, 1f / _size.x, 1f / _size.y));
                _block.SetVector(TexelId, new Vector4(_size.x / width, _size.y / height, 0f, 0f));
                _renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// Digs a strip <paramref name="width"/> wide from <paramref name="from"/> to
        /// <paramref name="to"/> (world XZ; height is ignored) <paramref name="amount"/> deeper,
        /// with berms either side.
        /// </summary>
        public void Dig(Vector3 from, Vector3 to, float width, float amount) =>
            AddStrip(_digs, from, to, width, amount, BERM_SHARE);

        /// <summary>
        /// Makes a strip at least <paramref name="depth"/> deep. Ground already deeper is left as
        /// it is; berms are not touched.
        /// </summary>
        public void Press(Vector3 from, Vector3 to, float width, float depth) =>
            AddStrip(_presses, from, to, width, depth, 0f);

        /// <summary>
        /// Presses an oval bowl <paramref name="amount"/> deeper at its middle: half
        /// <paramref name="halfLength"/> along <paramref name="forward"/>, <paramref name="halfWidth"/>
        /// across it. <paramref name="exponent"/> shapes the bowl (2 round like a crater, 4
        /// flat-bottomed like a footprint); <paramref name="rim"/> is the share of the radius the
        /// pushed-out rim reaches past the edge (0 for none). <paramref name="roughness"/> (0..1)
        /// breaks it up — a wandering edge, a lumpy bottom, a patchy rim — never twice the same.
        /// </summary>
        public void Dent(Vector3 centre, Vector3 forward, float halfLength, float halfWidth, float amount,
            float exponent, float rim, float roughness = 0f)
        {
            if (amount <= 0f || halfLength <= 0f || halfWidth <= 0f)
                return;

            Vector2 along = new(forward.x, forward.z);
            along = along.sqrMagnitude > 1e-8f ? along.normalized : Vector2.up;
            Vector2 side = new(-along.y, along.x);
            Vector2 c = new(centre.x, centre.z);
            // A wandering edge reaches out past the oval by up to a third.
            float reach = (1f + 2f * rim) * (1f + 0.35f * roughness);
            float seed = Random.Range(0f, 100f);

            int first = _dents.Vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                float u = (i is 1 or 2 ? 1f : -1f) * halfLength * reach;
                float v = (i >= 2 ? 1f : -1f) * halfWidth * reach;
                Vector2 world = c + along * u + side * v;

                _dents.Vertices.Add(ToMap(world));
                _dents.Local.Add(new Vector2(u, v));
                _dents.Params.Add(new Vector4(halfLength, halfWidth, amount, 0f));
                _dents.Shape.Add(new Vector4(exponent, rim, roughness, seed));
            }

            AddQuad(_dents, first);
        }

        /// <summary>
        /// Throws <paramref name="count"/> clumps from <paramref name="position"/>, each at
        /// <paramref name="velocity"/> plus up to <paramref name="spread"/> m/s in a random direction.
        /// They land, lie where they fell for a while and slump away into the mud.
        /// </summary>
        public void Throw(Vector3 position, Vector3 velocity, float spread, int count, float size = 1f)
        {
            if (_clumps == null || count <= 0)
                return;

            // Callers give points on the collider (a track's contact, a bullet's hit), which lies
            // under the mud. Started there, a clump flies hidden inside the mud, under its own
            // floor, and turns up already lying on top: lift it out first.
            position.y = Mathf.Max(position.y, _surfaceHeight + SURFACE_CLEARANCE);

            ParticleSystem.EmitParams emit = new();
            for (int i = 0; i < count; i++)
            {
                emit.position = position;
                emit.velocity = velocity + Random.insideUnitSphere * spread;
                // Mostly fist-to-boot-sized chunks, now and then a big slab.
                float lump = Random.value;
                float s = Mathf.Lerp(0.18f, 0.32f, lump) * (lump > 0.8f ? 1.5f : 1f) * size;
                // Wet mud lands flattened: wider than it is tall.
                emit.startSize3D = new Vector3(s * Random.Range(0.85f, 1.25f), s * Random.Range(0.5f, 0.8f),
                    s * Random.Range(0.85f, 1.25f));
                emit.rotation3D = new Vector3(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
                emit.startLifetime = Random.Range(7f, 11f);
                emit.startColor = Color.Lerp(_clumpDark, _clumpLight, Random.value);
                _clumps.Emit(emit, 1);

                QueueLanding(emit.position, emit.velocity, s);
            }
        }

        /// <summary>
        /// Works out where and when a clump thrown from <paramref name="from"/> at
        /// <paramref name="velocity"/> comes down on the clumps' floor (it flies a plain arc: no
        /// drag), so the splat is pressed into the mud the moment it lands.
        /// </summary>
        private void QueueLanding(Vector3 from, Vector3 velocity, float size)
        {
            float gravity = -Physics.gravity.y * _clumps.main.gravityModifier.constant;
            if (gravity <= 0f)
                return;

            float drop = from.y - _floorHeight;
            float root = velocity.y * velocity.y + 2f * gravity * drop;
            if (root < 0f)
                return;

            float time = (velocity.y + Mathf.Sqrt(root)) / gravity;
            Vector3 point = from + new Vector3(velocity.x, 0f, velocity.z) * time;
            if (point.x < _min.x || point.x > _min.x + _size.x || point.z < _min.y || point.z > _min.y + _size.y)
                return;

            _landings.Add(new Landing
            {
                // A beat after it lands: the clump has to be seen hitting before the mud gives.
                Time = GameTicks.Time + time + LANDING_LAG,
                Point = point,
                Heading = new Vector3(velocity.x, 0f, velocity.z),
                Size = size
            });
        }

        /// <summary>
        /// Presses the splats of every clump that has landed by now: a shallow, rough dent a little
        /// longer along the way it flew, with a rim of mud pushed up round it.
        /// </summary>
        private void StampLandings()
        {
            float now = GameTicks.Time;
            for (int i = _landings.Count - 1; i >= 0; i--)
            {
                Landing landing = _landings[i];
                if (landing.Time > now)
                    continue;

                _landings.RemoveAt(i);
                // No bigger than the clump itself: it sits in its own splat.
                float s = landing.Size;
                float stretch = Mathf.Lerp(1f, 1.3f, Mathf.Clamp01(landing.Heading.magnitude / 4f));
                Dent(landing.Point, landing.Heading, s * 0.32f * stretch, s * 0.28f, Mathf.Clamp(s * 0.35f, 0.03f, 0.12f),
                    2f, Random.Range(0.25f, 0.45f), 0.8f);
            }
        }

        private void AddStrip(Batch batch, Vector3 from, Vector3 to, float width, float amount, float bermShare)
        {
            if (amount <= 0f || width <= 0f)
                return;

            Vector2 a = new(from.x, from.z);
            Vector2 b = new(to.x, to.z);
            Vector2 span = b - a;
            float length = span.magnitude;
            if (length < 1e-4f)
                return;

            Vector2 along = span / length;
            Vector2 side = new(-along.y, along.x);
            float half = width * 0.5f;
            float outer = half * (1f + bermShare * 2f);
            float startAlong = Vector2.Dot(a, along);

            int first = batch.Vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                float u = i is 1 or 2 ? length : 0f;
                float v = i >= 2 ? outer : -outer;
                Vector2 world = a + along * u + side * v;

                batch.Vertices.Add(ToMap(world));
                batch.Local.Add(new Vector2(u, v));
                batch.Params.Add(new Vector4(half, outer, amount, startAlong + u));
            }

            AddQuad(batch, first);
        }

        private Vector3 ToMap(Vector2 world) =>
            new((world.x - _min.x) / _size.x, (world.y - _min.y) / _size.y, 0f);

        private static void AddQuad(Batch batch, int first)
        {
            batch.Indices.Add(first);
            batch.Indices.Add(first + 1);
            batch.Indices.Add(first + 2);
            batch.Indices.Add(first);
            batch.Indices.Add(first + 2);
            batch.Indices.Add(first + 3);
        }

        // With the app the game loop ticks the surface (it stands on pause); without it (tests) it ticks itself
        private void OnEnable() => _looped = GameTicks.TryAdd(this);

        private void OnDisable()
        {
            if (_looped)
                GameTicks.Remove(this);
            _looped = false;
        }

        private void LateUpdate() // perf-ok: ticks itself without the game loop (tests); one flag check with it
        {
            if (!_looped)
                OnLateUpdate(Time.deltaTime);
        }

        public void OnLateUpdate(float deltaTime)
        {
            if (_landings.Count > 0)
                StampLandings();

            if (_digs.Empty && _presses.Empty && _dents.Empty)
                return;

            if (!_map.IsCreated())
                CreateMap();

            // Map UV straight onto the target. SetViewProjectionMatrices adapts the projection to
            // the platform and the render target itself; converting it here as well flips the map
            // upside down on D3D, and every rut lands mirrored across the floor.
            Matrix4x4 projection = Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f);

            _commands.Clear();
            _commands.SetRenderTarget(_map);
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, projection);
            Draw(_digs, PASS_DIG);
            Draw(_dents, PASS_DENT);
            if (!_presses.Empty)
            {
                Draw(_presses, PASS_PRESS);
                // The same strips again: the tread of whatever the tracks stand on now.
                _commands.DrawMesh(_presses.Mesh, Matrix4x4.identity, _stampMaterial, 0, PASS_TREAD);
            }
            Graphics.ExecuteCommandBuffer(_commands);
        }

        private void Draw(Batch batch, int pass)
        {
            if (batch.Empty)
                return;

            batch.Upload();
            _commands.DrawMesh(batch.Mesh, Matrix4x4.identity, _stampMaterial, 0, pass);
        }

        private void OnDestroy()
        {
            if (_map != null)
                _map.Release();
            if (_stampMaterial != null)
                Destroy(_stampMaterial);
            if (_digs != null)
                Destroy(_digs.Mesh);
            if (_presses != null)
                Destroy(_presses.Mesh);
            if (_dents != null)
                Destroy(_dents.Mesh);
            _commands?.Release();
        }
    }
}

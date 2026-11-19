using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// A plain bullet's hit leaves a hot point for a moment, like the slug cooling where it struck: a small HDR voxel
    /// (Bloom catches it) and a short-range point light, white-orange to dull red to out over <see cref="LIFE"/>
    /// seconds. One shared object, made on the first hit. Spots and lights are fixed rings: under constant fire the
    /// oldest is reused, so nothing is allocated per hit and at most <see cref="LIGHTS"/> lights burn at once. The
    /// machine gun's muzzle flicker (<see cref="Flicker"/>) takes its lights from the same ring.
    /// </summary>
    public sealed class BulletHotSpots : MonoBehaviour
    {
        /// <summary>Seconds a hot spot glows.</summary>
        public const float LIFE = 0.3f;

        /// <summary>Most hot spots alive at once (one instanced draw).</summary>
        public const int MAX = 128;

        /// <summary>Most hot-spot lights burning at once; the oldest is reused.</summary>
        public const int LIGHTS = 12;

        private const float SIZE = 0.07f;
        private const float LIGHT_RANGE = 1.8f;
        private const float LIGHT_INTENSITY = 4f;
        private const float GLOW = 12f;
        private const float FLICKER_LIFE = 0.06f;
        private const float FLICKER_INTENSITY = 6f;

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static readonly Color WhiteHot = new(1f, 0.82f, 0.55f);
        private static readonly Color Orange = new(1f, 0.42f, 0.08f);
        private static readonly Color DullRed = new(0.55f, 0.05f, 0.01f);
        private static BulletHotSpots s_instance;

        private readonly Vector3[] _position = new Vector3[MAX];
        private readonly Quaternion[] _rotation = new Quaternion[MAX];
        private readonly float[] _age = new float[MAX];
        private readonly float[] _scale = new float[MAX];
        private readonly Matrix4x4[] _matrices = new Matrix4x4[MAX];
        private readonly Vector4[] _colors = new Vector4[MAX];
        private readonly Light[] _lights = new Light[LIGHTS];
        private readonly float[] _lightAge = new float[LIGHTS];
        private readonly float[] _lightLife = new float[LIGHTS];
        private readonly float[] _lightPeak = new float[LIGHTS];
        private int _next;
        private int _nextLight;
        private Mesh _mesh;
        private Material _material;
        private MaterialPropertyBlock _block;

        /// <summary>Off: hits leave no hot point (A/B perf runs, the debug panel).</summary>
        public static bool Enabled = true;

        /// <summary>Hot spots alive now (tests, the debug panel).</summary>
        public static int ActiveSpots => s_instance == null ? 0 : s_instance.CountAlive();

        /// <summary>Hot-spot lights burning now (tests, the debug panel).</summary>
        public static int ActiveLights => s_instance == null ? 0 : s_instance.CountLights();

        /// <summary>
        /// A bullet struck at <paramref name="point"/> on a surface facing <paramref name="normal"/>.
        /// <paramref name="power"/> scales the point's size and its light's brightness and reach (an HE shell's hit: 1.6).
        /// </summary>
        public static void Emit(Vector3 point, Vector3 normal, float power = 1f)
        {
            if (!Enabled)
                return;
            Instance.Add(point, normal, Mathf.Max(0.1f, power));
        }

        /// <summary>
        /// A gun's muzzle flickered at <paramref name="point"/> (the machine gun, every round): a brief warm light from
        /// the same ring as the hot points, so all of them together stay under <see cref="LIGHTS"/>.
        /// </summary>
        public static void Flicker(Vector3 point)
        {
            if (!Enabled)
                return;
            Instance.Ignite(point, FLICKER_LIFE, FLICKER_INTENSITY, LIGHT_RANGE);
        }

        private static BulletHotSpots Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("<BulletHotSpots>");
                    s_instance = go.AddComponent<BulletHotSpots>();
                    s_instance.Init();
                }
                return s_instance;
            }
        }

        /// <summary>
        /// The glow at <paramref name="t"/> (0 just hit, 1 cold): white-orange, then orange, then dull red fading out.
        /// The alpha is the brightness (0..1) the light follows.
        /// </summary>
        public static Color Cooling(float t)
        {
            t = Mathf.Clamp01(t);
            Color c = t < 0.35f ? Color.Lerp(WhiteHot, Orange, t / 0.35f) : Color.Lerp(Orange, DullRed, (t - 0.35f) / 0.65f);
            // Smoothstep: holds hot a moment, then cools (half bright at mid-life, out at the end).
            c.a = 1f - t * t * (3f - 2f * t);
            return c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            s_instance = null;
            Enabled = true;
        }

        /// <summary>Called right after AddComponent, not Awake: Awake does not run in edit mode (tests).</summary>
        private void Init()
        {
            for (int i = 0; i < MAX; i++)
                _age[i] = LIFE;

            for (int i = 0; i < LIGHTS; i++)
            {
                var go = new GameObject("HotSpotLight");
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = LIGHT_RANGE;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                l.enabled = false;
                _lights[i] = l;
                _lightAge[i] = LIFE;
                _lightLife[i] = LIFE;
            }
        }

        private void Add(Vector3 point, Vector3 normal, float power)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            // Half sunk into the surface: a glowing voxel in the hole, not a cube stuck on the wall.
            _position[_next] = point + n * (SIZE * power * 0.15f);
            _rotation[_next] = Quaternion.LookRotation(n);
            _age[_next] = 0f;
            _scale[_next] = power;
            _next = (_next + 1) % MAX;

            Ignite(point + n * (0.2f * power), LIFE, LIGHT_INTENSITY * power, LIGHT_RANGE * power);
        }

        /// <summary>The next light of the ring, burning at <paramref name="peak"/> and cooling out over <paramref name="life"/> s.</summary>
        private void Ignite(Vector3 at, float life, float peak, float range)
        {
            Light light = _lights[_nextLight];
            _lightAge[_nextLight] = 0f;
            _lightLife[_nextLight] = life;
            _lightPeak[_nextLight] = peak;
            light.transform.position = at;
            light.range = range;
            light.color = Cooling(0f);
            light.intensity = peak;
            light.enabled = true;
            _nextLight = (_nextLight + 1) % LIGHTS;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            for (int i = 0; i < LIGHTS; i++)
            {
                if (_lightAge[i] >= _lightLife[i])
                    continue;
                _lightAge[i] += dt;
                if (_lightAge[i] >= _lightLife[i])
                {
                    _lights[i].enabled = false;
                    continue;
                }
                Color c = Cooling(_lightAge[i] / _lightLife[i]);
                _lights[i].color = c;
                _lights[i].intensity = _lightPeak[i] * c.a;
            }

            int n = 0;
            for (int i = 0; i < MAX; i++)
            {
                if (_age[i] >= LIFE)
                    continue;
                _age[i] += dt;
                if (_age[i] >= LIFE)
                    continue;
                float t = _age[i] / LIFE;
                Color c = Cooling(t);
                float size = SIZE * _scale[i] * (1f - 0.35f * t);
                _matrices[n] = Matrix4x4.TRS(_position[i], _rotation[i], new Vector3(size, size, size));
                // Bright enough to bloom while hot, a dim red ember at the end.
                float glow = Mathf.Lerp(0.6f, GLOW, c.a);
                _colors[n] = new Vector4(c.r * glow, c.g * glow, c.b * glow, 1f);
                n++;
            }

            if (n > 0)
                Draw(n);
        }

        private void Draw(int count)
        {
            _mesh ??= TankVoxelMuzzleSmokeBehaviour.BuildCubeMesh();
            _material ??= TankVoxelMuzzleSmokeBehaviour.LoadMaterial();
            _block ??= new MaterialPropertyBlock();
            _block.SetVectorArray(InstanceColorId, _colors);
            Graphics.DrawMeshInstanced(_mesh, 0, _material, _matrices, count, _block, ShadowCastingMode.Off, false);
        }

        private int CountAlive()
        {
            int n = 0;
            for (int i = 0; i < MAX; i++)
                if (_age[i] < LIFE)
                    n++;
            return n;
        }

        private int CountLights()
        {
            int n = 0;
            for (int i = 0; i < LIGHTS; i++)
                if (_lights[i] != null && _lights[i].enabled)
                    n++;
            return n;
        }
    }
}

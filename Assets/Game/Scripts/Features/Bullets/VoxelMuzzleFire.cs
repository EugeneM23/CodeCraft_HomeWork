using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Fire and sparks at a gun's muzzle on a shot, as voxels: a tongue of glowing cubes thrown forward in a cone
    /// (white-yellow, orange, red, dark, shrinking as they cool) and thin spark streaks flying on and falling. The
    /// cannon also puffs flame out sideways from its muzzle brake and lights a brief flash; the machine gun's round
    /// is a much smaller copy whose flicker borrows <see cref="BulletHotSpots"/>' light ring. One shared object, made
    /// on the first shot; its arrays are a fixed pool (the oldest cube is reused), drawn in one instanced call.
    /// </summary>
    public sealed class VoxelMuzzleFire : MonoBehaviour
    {
        /// <summary>Most cubes and sparks alive at once (one instanced draw).</summary>
        public const int MAX = 1023;

        /// <summary>Cannon flash lights; the oldest is reused.</summary>
        public const int LIGHTS = 2;

        private const float FLASH_TIME = 0.09f;
        private const float FLASH_INTENSITY = 30f;
        private const float FLASH_RANGE = 9f;
        private const float GRAVITY = 14f;

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static readonly Color WhiteHot = new(1f, 0.92f, 0.62f);
        private static readonly Color Orange = new(1f, 0.45f, 0.08f);
        private static readonly Color Red = new(0.75f, 0.1f, 0.02f);
        private static readonly Color Dark = new(0.12f, 0.06f, 0.05f);
        private static VoxelMuzzleFire s_instance;

        private readonly Vector3[] _position = new Vector3[MAX];
        private readonly Vector3[] _velocity = new Vector3[MAX];
        private readonly float[] _age = new float[MAX];
        private readonly float[] _life = new float[MAX];
        private readonly float[] _size = new float[MAX];
        // 0 flame (brakes hard, rises a little, cools to dark), 1 spark (keeps its speed, falls, a streak).
        private readonly byte[] _kind = new byte[MAX];
        private readonly Matrix4x4[] _matrices = new Matrix4x4[MAX];
        private readonly Vector4[] _colors = new Vector4[MAX];
        private readonly Light[] _lights = new Light[LIGHTS];
        private readonly float[] _lightAge = new float[LIGHTS];
        private int _count;
        private int _nextLight;
        private Mesh _mesh;
        private Material _material;
        private MaterialPropertyBlock _block;

        /// <summary>Off: shots leave no voxel fire (A/B perf runs, the debug panel).</summary>
        public static bool Enabled = true;

        /// <summary>Cubes and sparks alive now (tests, the debug panel).</summary>
        public static int Alive => s_instance == null ? 0 : s_instance._count;

        /// <summary>Cannon flash lights burning now (tests).</summary>
        public static int ActiveLights => s_instance == null ? 0 : s_instance.CountLights();

        /// <summary>
        /// The cannon fired from <paramref name="muzzle"/> (its forward along the barrel): a burst of flame, side
        /// jets from the muzzle brake, a spray of sparks and a flash. <paramref name="carry"/> is the gun's own
        /// velocity, which the fire keeps.
        /// </summary>
        public static void Cannon(Transform muzzle, Vector3 carry)
        {
            if (!Enabled || muzzle == null)
                return;
            Instance.Burst(muzzle.position, muzzle.forward, muzzle.right, carry, true);
        }

        /// <summary>The machine gun fired a round from <paramref name="muzzle"/>: a small tongue, a few sparks.</summary>
        public static void MachineGun(Transform muzzle, Vector3 carry)
        {
            if (!Enabled || muzzle == null)
                return;
            Instance.Burst(muzzle.position, muzzle.forward, muzzle.right, carry, false);
            BulletHotSpots.Flicker(muzzle.position + muzzle.forward * 0.15f);
        }

        /// <summary>
        /// Colour of a flame cube at <paramref name="t"/> (0 just out, 1 gone): white-yellow, orange, red, dark.
        /// The alpha is how bright it glows (0..1).
        /// </summary>
        public static Color FlameColor(float t)
        {
            t = Mathf.Clamp01(t);
            Color c = t < 0.25f ? Color.Lerp(WhiteHot, Orange, t / 0.25f)
                : t < 0.6f ? Color.Lerp(Orange, Red, (t - 0.25f) / 0.35f)
                : Color.Lerp(Red, Dark, (t - 0.6f) / 0.4f);
            c.a = 1f - t * t;
            return c;
        }

        private static VoxelMuzzleFire Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("<VoxelMuzzleFire>");
                    s_instance = go.AddComponent<VoxelMuzzleFire>();
                    s_instance.Init();
                }
                return s_instance;
            }
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
            for (int i = 0; i < LIGHTS; i++)
            {
                var go = new GameObject("MuzzleFlash");
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.7f, 0.35f);
                l.range = FLASH_RANGE;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _lights[i] = l;
                _lightAge[i] = FLASH_TIME;
            }
        }

        private void Burst(Vector3 origin, Vector3 forward, Vector3 right, Vector3 carry, bool cannon)
        {
            if (cannon)
            {
                // The main tongue: a cone out along the barrel, the core fastest.
                for (int k = 0; k < 44; k++)
                    Flame(origin, Cone(forward, 0.28f) * Random.Range(7f, 22f), carry, Random.Range(0.15f, 0.3f),
                        Random.Range(0.09f, 0.2f));
                // The muzzle brake: two short jets out sideways, a little forward.
                for (int k = 0; k < 16; k++)
                {
                    Vector3 side = (k & 1) == 0 ? right : -right;
                    Flame(origin, Cone(side * 0.9f + forward * 0.45f, 0.3f) * Random.Range(4f, 9f), carry,
                        Random.Range(0.12f, 0.22f), Random.Range(0.08f, 0.15f));
                }
                // Short: they stay near the barrel (~2-4 m), so they never read as sparks off what the shell hit.
                for (int k = 0; k < 26; k++)
                    Spark(origin, Cone(forward, 0.4f) * Random.Range(7f, 14f), carry, Random.Range(0.15f, 0.3f),
                        Random.Range(0.025f, 0.045f));

                Light light = _lights[_nextLight];
                _lightAge[_nextLight] = 0f;
                light.transform.position = origin + forward * 1.2f;
                light.intensity = FLASH_INTENSITY;
                light.enabled = true;
                _nextLight = (_nextLight + 1) % LIGHTS;
            }
            else
            {
                int flames = Random.Range(5, 9);
                for (int k = 0; k < flames; k++)
                    Flame(origin, Cone(forward, 0.18f) * Random.Range(4f, 10f), carry, Random.Range(0.07f, 0.13f),
                        Random.Range(0.07f, 0.12f));
                int sparks = Random.Range(2, 5);
                for (int k = 0; k < sparks; k++)
                    Spark(origin, Cone(forward, 0.35f) * Random.Range(10f, 22f), carry, Random.Range(0.12f, 0.3f),
                        Random.Range(0.015f, 0.025f));
            }
        }

        private static Vector3 Cone(Vector3 axis, float spread) =>
            (axis.normalized + Random.insideUnitSphere * spread).normalized;

        private void Flame(Vector3 at, Vector3 velocity, Vector3 carry, float life, float size) =>
            Add(at, velocity + carry, life, size, 0);

        private void Spark(Vector3 at, Vector3 velocity, Vector3 carry, float life, float size) =>
            Add(at, velocity + carry, life, size, 1);

        private void Add(Vector3 at, Vector3 velocity, float life, float size, byte kind)
        {
            // Full: the oldest-looking one goes (any slot; the pool is a swap-remove list).
            int i = _count < MAX ? _count++ : Random.Range(0, MAX);
            _position[i] = at;
            _velocity[i] = velocity;
            _age[i] = 0f;
            _life[i] = life;
            _size[i] = size;
            _kind[i] = kind;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            for (int i = 0; i < LIGHTS; i++)
            {
                if (_lightAge[i] >= FLASH_TIME)
                    continue;
                _lightAge[i] += dt;
                float f = 1f - Mathf.Clamp01(_lightAge[i] / FLASH_TIME);
                _lights[i].intensity = FLASH_INTENSITY * f * f;
                _lights[i].enabled = _lightAge[i] < FLASH_TIME;
            }

            // Flame brakes hard in the air and lifts a little; sparks keep going and fall.
            float flameDamp = Mathf.Exp(-9f * dt);
            float sparkDamp = Mathf.Exp(-1.2f * dt);
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                _age[i] += dt;
                if (_age[i] >= _life[i])
                {
                    _count--;
                    _position[i] = _position[_count];
                    _velocity[i] = _velocity[_count];
                    _age[i] = _age[_count];
                    _life[i] = _life[_count];
                    _size[i] = _size[_count];
                    _kind[i] = _kind[_count];
                    i--;
                    continue;
                }

                Vector3 v = _velocity[i];
                float t = _age[i] / _life[i];
                if (_kind[i] == 0)
                {
                    v *= flameDamp;
                    v.y += 2f * dt;
                    _velocity[i] = v;
                    _position[i] += v * dt;
                    Color c = FlameColor(t);
                    // Swells a moment as it leaves the muzzle, then shrinks away as it cools.
                    float size = _size[i] * (t < 0.2f ? Mathf.Lerp(0.6f, 1.2f, t / 0.2f) : Mathf.Lerp(1.2f, 0.2f, (t - 0.2f) / 0.8f));
                    float glow = Mathf.Lerp(0.3f, 4.5f, c.a);
                    _matrices[n] = Matrix4x4.TRS(_position[i], Quaternion.identity, new Vector3(size, size, size));
                    _colors[n] = new Vector4(c.r * glow, c.g * glow, c.b * glow, Mathf.Clamp01(c.a * 1.5f));
                }
                else
                {
                    v *= sparkDamp;
                    v.y -= GRAVITY * dt;
                    _velocity[i] = v;
                    _position[i] += v * dt;
                    float speed = v.magnitude;
                    float length = _size[i] + speed * 0.014f;
                    Quaternion look = speed > 0.01f ? Quaternion.LookRotation(v / speed) : Quaternion.identity;
                    float width = _size[i] * (1f - 0.5f * t);
                    _matrices[n] = Matrix4x4.TRS(_position[i], look, new Vector3(width, width, length));
                    Color hot = Color.Lerp(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.55f, 0.12f), Mathf.Clamp01(t * 1.6f));
                    Color c = Color.Lerp(hot, new Color(0.9f, 0.18f, 0.04f), Mathf.Clamp01(t * 2f - 1f)) * Mathf.Lerp(10f, 3f, t);
                    _colors[n] = new Vector4(c.r, c.g, c.b, 1f);
                }
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

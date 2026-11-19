using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Machine-gun hits: a spray of glowing voxel sparks (bright HDR cubes, so Bloom catches them) thrown off the
    /// surface round the ricochet direction, falling, stretching along their flight and cooling from white-yellow to
    /// orange-red; and a short flash of light at the hit. One shared object, made on the first hit; its arrays are a
    /// fixed pool (the oldest spark is reused), its lights a small ring.
    /// </summary>
    public sealed class VoxelSparks : MonoBehaviour
    {
        private const int MAX = 1500;
        private const int BATCH = 1023;
        private const int LIGHTS = 6;
        private const float GRAVITY = 14f;
        private const float DRAG = 1.2f;
        private const float FLASH_TIME = 0.07f;

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static VoxelSparks s_instance;

        private readonly Vector3[] _position = new Vector3[MAX];
        private readonly Vector3[] _velocity = new Vector3[MAX];
        private readonly float[] _age = new float[MAX];
        private readonly float[] _life = new float[MAX];
        private readonly float[] _size = new float[MAX];
        private readonly Matrix4x4[] _matrices = new Matrix4x4[BATCH];
        private readonly Vector4[] _colors = new Vector4[BATCH];
        private readonly Light[] _lights = new Light[LIGHTS];
        private readonly float[] _lightAge = new float[LIGHTS];
        private int _count;
        private int _nextLight;
        private Mesh _mesh;
        private Material _material;
        private MaterialPropertyBlock _block;

        /// <summary>
        /// Sparks off a hit at <paramref name="point"/> on a surface facing <paramref name="normal"/>, struck along
        /// <paramref name="direction"/>.
        /// </summary>
        public static void Emit(Vector3 point, Vector3 normal, Vector3 direction, int count = 26)
        {
            if (s_instance == null)
            {
                var go = new GameObject("<VoxelSparks>");
                s_instance = go.AddComponent<VoxelSparks>();
            }
            s_instance.Spray(point, normal, direction, count);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => s_instance = null;

        private void Awake()
        {
            for (int i = 0; i < LIGHTS; i++)
            {
                var go = new GameObject("SparkLight");
                go.transform.SetParent(transform, false);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.72f, 0.35f);
                l.range = 3.5f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _lights[i] = l;
                _lightAge[i] = FLASH_TIME;
            }
        }

        private void Spray(Vector3 point, Vector3 normal, Vector3 direction, int count)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            Vector3 d = direction.sqrMagnitude > 1e-6f ? direction.normalized : -n;
            // Mostly along the ricochet, fanned wide: the glancing hit throws them on, the square one back out.
            Vector3 bounce = Vector3.Reflect(d, n);
            Vector3 at = point + n * 0.03f;
            for (int k = 0; k < count; k++)
            {
                int i = _count < MAX ? _count++ : Random.Range(0, MAX);
                Vector3 heading = (bounce + Random.insideUnitSphere * 0.75f + n * 0.25f).normalized;
                if (Vector3.Dot(heading, n) < 0.05f)
                    heading = (heading + n * 0.6f).normalized;
                _position[i] = at;
                _velocity[i] = heading * Random.Range(5f, 17f);
                _age[i] = 0f;
                _life[i] = Random.Range(0.18f, 0.55f);
                _size[i] = Random.Range(0.025f, 0.05f);
            }

            Light light = _lights[_nextLight];
            _lightAge[_nextLight] = 0f;
            light.transform.position = point + n * 0.25f;
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
                if (_lightAge[i] >= FLASH_TIME)
                    continue;
                _lightAge[i] += dt;
                float f = 1f - Mathf.Clamp01(_lightAge[i] / FLASH_TIME);
                _lights[i].intensity = 6f * f * f;
                _lights[i].enabled = _lightAge[i] < FLASH_TIME;
            }

            float damp = Mathf.Exp(-DRAG * dt);
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
                    i--;
                    continue;
                }
                Vector3 v = _velocity[i] * damp;
                v.y -= GRAVITY * dt;
                _velocity[i] = v;
                _position[i] += v * dt;
            }

            if (_count > 0)
                Draw();
        }

        private void Draw()
        {
            _mesh ??= TankVoxelMuzzleSmokeBehaviour.BuildCubeMesh();
            _material ??= TankVoxelMuzzleSmokeBehaviour.LoadMaterial();
            _block ??= new MaterialPropertyBlock();

            for (int start = 0; start < _count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, _count - start);
                for (int k = 0; k < n; k++)
                {
                    int i = start + k;
                    float t = _age[i] / _life[i];
                    Vector3 v = _velocity[i];
                    float speed = v.magnitude;
                    // Stretched along the flight: a streak, not a dot.
                    float length = _size[i] + speed * 0.012f;
                    Quaternion look = speed > 0.01f ? Quaternion.LookRotation(v / speed) : Quaternion.identity;
                    float width = _size[i] * (1f - 0.5f * t);
                    _matrices[k] = Matrix4x4.TRS(_position[i], look, new Vector3(width, width, length));

                    // White-hot to yellow to orange-red, bright enough to bloom.
                    Color hot = Color.Lerp(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.55f, 0.12f), Mathf.Clamp01(t * 1.6f));
                    Color c = Color.Lerp(hot, new Color(0.9f, 0.18f, 0.04f), Mathf.Clamp01(t * 2f - 1f)) * Mathf.Lerp(9f, 3f, t);
                    _colors[k] = new Vector4(c.r, c.g, c.b, 1f);
                }
                _block.SetVectorArray(InstanceColorId, _colors);
                Graphics.DrawMeshInstanced(_mesh, 0, _material, _matrices, n, _block, ShadowCastingMode.Off, false);
            }
        }
    }
}

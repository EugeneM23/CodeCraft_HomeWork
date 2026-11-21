using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// The air bomb's smoke as voxel cubes (the cannon's VoxelPuff look), shaped as a small mushroom cloud: a cap that
    /// rises and rolls as a ring (up through its middle, out over the top, down its rim), a stem that is drawn up into
    /// it, and a low skirt that runs out over the ground. The cubes come in lumps, so it reads as a billowing cloud.
    /// Each cube follows its place in that shape plus an offset of its own: shots, blasts and tanks push the offset
    /// through <see cref="VoxelPuffField"/> (a shot leaves a hole), and it heals back slowly.
    /// One object per blast, gone when its last cube is.
    /// </summary>
    public sealed class VoxelBlastSmoke : MonoBehaviour
    {
        private const int MAX = 900;
        private const int CAP = 320;
        private const int STEM = 160;
        private const int BATCH = 1023;
        private const float LIFETIME = 6f; // the user: gone twice as fast (was 12)
        private const float CLEARER = 0.75f; // the user: 25 % more see-through
        private const float GRID = 0.1f;

        /// <summary>How fast (1/s) the push of a shot or a tank dies down, and how fast its hole heals.</summary>
        private const float OFFSET_DRAG = 2.5f;
        private const float OFFSET_HEAL = 0.45f;

        private const byte ROLE_CAP = 0;
        private const byte ROLE_STEM = 1;
        private const byte ROLE_SKIRT = 2;

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static Mesh s_mesh;
        private static Material s_material;

        private static readonly Color Dark = new(0.14f, 0.13f, 0.12f, 1f);
        private static readonly Color Light = new(0.58f, 0.56f, 0.53f, 1f);
        private static readonly Color Hot = new(1f, 0.5f, 0.15f, 1f);

        // The shape: per cube its role and where in the shape it sits (fixed), its lump's scatter.
        private readonly byte[] _role = new byte[MAX];
        private readonly float[] _phi = new float[MAX];   // round the vertical axis
        private readonly float[] _theta = new float[MAX]; // round the cap's tube (rolls), or height in the stem
        private readonly float[] _tube = new float[MAX];  // distance from the tube's core, 0..1
        private readonly float[] _spin = new float[MAX];
        private readonly Vector3[] _lump = new Vector3[MAX];

        // What the field moves: the drawn position, the offset from the shape and its speed.
        private readonly Vector3[] _position = new Vector3[MAX];
        private readonly Vector3[] _offset = new Vector3[MAX];
        private readonly Vector3[] _velocity = new Vector3[MAX];
        private readonly Vector3[] _anchor = new Vector3[MAX];
        private readonly float[] _age = new float[MAX];
        private readonly float[] _life = new float[MAX];
        private readonly float[] _size = new float[MAX];
        private readonly float[] _edge = new float[MAX];

        private readonly Matrix4x4[] _matrices = new Matrix4x4[BATCH];
        private readonly Vector4[] _colors = new Vector4[BATCH];
        private MaterialPropertyBlock _block;
        private Vector3 _origin;
        private float _radius;
        private float _time;
        private Vector2 _drift;
        private int _count;

        // Each blast's own cloud: its height, widths, lean and a lopsided outline (two waves round the axis).
        private float _height;
        private float _capWidth;
        private float _stemWidth;
        private float _skirtReach;
        private float _rise;
        private Vector2 _tilt;
        private Vector4 _waves; // cap wave: amplitude, phase; skirt wave: amplitude, phase
        private int _lobes;

        /// <summary>Sets the smoke of a blast of <paramref name="radius"/> metres off at <paramref name="point"/>.</summary>
        public static void Spawn(Vector3 point, float radius)
        {
            var go = new GameObject("<VoxelBlastSmoke>");
            go.transform.position = point;
            go.AddComponent<VoxelBlastSmoke>().Burst(point, Mathf.Max(1f, radius));
        }

        private void Burst(Vector3 point, float radius)
        {
            _origin = point;
            _radius = radius;
            _drift = Random.insideUnitCircle * 0.25f; // a light wind leans the cloud a little
            _height = Random.Range(0.8f, 1.2f);
            _capWidth = Random.Range(0.75f, 1.3f);
            _stemWidth = Random.Range(0.7f, 1.35f);
            _skirtReach = Random.Range(0.8f, 1.2f);
            _rise = Random.Range(0.8f, 1.25f);
            _tilt = Random.insideUnitCircle * Random.Range(0.05f, 0.22f); // the cap sits off the stem's axis
            _lobes = Random.Range(2, 4);
            _waves = new Vector4(Random.Range(0.1f, 0.3f), Random.Range(0f, 6.3f), Random.Range(0.12f, 0.35f), Random.Range(0f, 6.3f));
            _count = MAX;

            // Lumps: cubes share a lump's place and scatter round it, so the cloud is made of billows.
            int i = 0;
            while (i < CAP)
            {
                float phi = Random.Range(0f, Mathf.PI * 2f);
                float theta = Random.Range(0f, Mathf.PI * 2f);
                float tube = Random.Range(0.35f, 1f);
                float spin = Random.Range(0.8f, 1.2f);
                float lump = Random.Range(0.35f, 0.6f) * radius / 3.4f;
                int n = Mathf.Min(Random.Range(8, 16), CAP - i);
                for (int k = 0; k < n; k++, i++)
                    Seed(i, ROLE_CAP, phi, theta, tube, spin, Random.insideUnitSphere * lump);
            }

            while (i < CAP + STEM)
            {
                float phi = Random.Range(0f, Mathf.PI * 2f);
                float height = Random.Range(0.05f, 0.95f);
                float tube = Random.Range(0.2f, 1f);
                float lump = Random.Range(0.25f, 0.45f) * radius / 3.4f;
                int n = Mathf.Min(Random.Range(6, 12), CAP + STEM - i);
                for (int k = 0; k < n; k++, i++)
                    Seed(i, ROLE_STEM, phi, height, tube, Random.Range(0.8f, 1.2f), Random.insideUnitSphere * lump);
            }

            while (i < MAX)
            {
                float phi = Random.Range(0f, Mathf.PI * 2f);
                float reach = Mathf.Sqrt(Random.Range(0.02f, 1f));
                float lump = Random.Range(0.45f, 0.8f) * radius / 3.4f;
                int n = Mathf.Min(Random.Range(6, 12), MAX - i);
                for (int k = 0; k < n; k++, i++)
                    Seed(i, ROLE_SKIRT, phi, Random.Range(0f, 1f), reach, Random.Range(0.8f, 1.2f), Random.insideUnitSphere * lump);
            }

            for (int c = 0; c < _count; c++)
                _anchor[c] = _position[c] = AnchorOf(c, 0f);
        }

        private void Seed(int i, byte role, float phi, float theta, float tube, float spin, Vector3 lump)
        {
            _role[i] = role;
            _phi[i] = phi + Random.Range(-0.12f, 0.12f);
            _theta[i] = theta;
            _tube[i] = tube;
            _spin[i] = spin;
            _lump[i] = lump;
            _offset[i] = Vector3.zero;
            _velocity[i] = Vector3.zero;
            _age[i] = 0f;
            _life[i] = role switch
            {
                ROLE_CAP => LIFETIME * Random.Range(0.75f, 1f),
                ROLE_STEM => LIFETIME * Random.Range(0.6f, 0.9f),
                _ => LIFETIME * Random.Range(0.6f, 0.95f)
            };
            _size[i] = role == ROLE_STEM ? Random.Range(0.25f, 0.45f) : Random.Range(0.35f, 0.6f);
        }

        // Where cube i sits in the shape at time t since the blast.
        private Vector3 AnchorOf(int i, float t)
        {
            float r = _radius;
            float rise = 1f - Mathf.Exp(-t * _rise / 2.4f);     // the cloud shoots up, then hangs
            float spread = 1f - Mathf.Exp(-t / 3.2f);
            float capHeight = 1.2f * _height * (r * (0.25f + 1.25f * rise) + 0.05f * r * t); // keeps creeping up as it cools
            // Lopsided: the cap's ring and the skirt reach further on some sides than on others.
            float capWave = 1f + _waves.x * Mathf.Sin(_phi[i] * _lobes + _waves.y);
            float ring = r * (0.15f + 0.5f * spread) * _capWidth * capWave; // the cap's ring radius
            float tubeR = r * (0.22f + 0.28f * spread) * Mathf.Lerp(1f, _capWidth, 0.5f); // the thickness of the roll
            Vector3 wind = new Vector3(_drift.x, 0f, _drift.y) * (t * r * 0.05f)
                + new Vector3(_tilt.x, 0f, _tilt.y) * capHeight; // the cap leans off the stem
            Vector3 lump = _lump[i] * (0.5f + 0.9f * spread);

            switch (_role[i])
            {
                case ROLE_CAP:
                {
                    // The roll: up through the middle, out over the top, down at the rim (fast first, then slow).
                    float roll = _theta[i] + _spin[i] * 3f * (1f - Mathf.Exp(-t / 5f));
                    float d = tubeR * _tube[i];
                    float across = ring - Mathf.Cos(roll) * d;
                    float up = capHeight + Mathf.Sin(roll) * d * 0.75f;
                    float phi = _phi[i] + 0.05f * t * _spin[i];
                    return _origin + wind + new Vector3(Mathf.Cos(phi) * across, up, Mathf.Sin(phi) * across) + lump;
                }
                case ROLE_STEM:
                {
                    // Drawn up into the cap: each lump climbs its stem and the stem narrows to the top.
                    float h = Mathf.Repeat(_theta[i] + 0.04f * t * _spin[i], 1f);
                    float width = r * (0.25f + 0.1f * spread) * (1.15f - 0.45f * h) * _tube[i] * _stemWidth;
                    float phi = _phi[i] + 0.1f * t;
                    Vector3 lean = wind * h;
                    return _origin + lean + new Vector3(Mathf.Cos(phi) * width, h * capHeight * 0.9f + 0.2f, Mathf.Sin(phi) * width) + lump;
                }
                default:
                {
                    // The skirt: a low ring of dust that runs out fast and rolls up a little at its edge.
                    float run = 1f - Mathf.Exp(-t / 1.1f);
                    float skirtWave = 1f + _waves.z * Mathf.Sin(_phi[i] * (_lobes + 1) + _waves.w);
                    float across = r * (0.35f + 1.65f * run * _tube[i] * _skirtReach * skirtWave);
                    float up = 0.3f + r * 0.4f * run * _theta[i] * (0.5f + 0.5f * _tube[i]);
                    return _origin + wind * 0.5f + new Vector3(Mathf.Cos(_phi[i]) * across, up, Mathf.Sin(_phi[i]) * across) + lump;
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            _time += dt;

            for (int i = 0; i < _count; i++)
            {
                _age[i] += dt;
                if (_age[i] >= _life[i])
                {
                    _count--;
                    MoveCube(_count, i);
                    i--;
                    continue;
                }

                float t = _age[i] / _life[i];
                _edge[i] = _size[i] * Mathf.Lerp(0.7f, 3f, Mathf.Sqrt(t));
                _anchor[i] = AnchorOf(i, _time);
                _position[i] = _anchor[i] + _offset[i];
            }

            if (_count == 0)
            {
                Destroy(gameObject);
                return;
            }

            // Shots, blasts and tanks push the cubes as they are drawn (their swollen size, so a shot's hole shows).
            VoxelPuffField.Apply(_position, _velocity, _edge, _count, dt, false, true);

            float drag = Mathf.Exp(-OFFSET_DRAG * dt);
            float heal = Mathf.Exp(-OFFSET_HEAL * dt);
            for (int i = 0; i < _count; i++)
            {
                Vector3 offset = _position[i] - _anchor[i];
                _velocity[i] *= drag;
                offset = (offset + _velocity[i] * dt) * heal;
                _offset[i] = offset;
                _position[i] = _anchor[i] + offset;
            }

            Draw();
        }

        private void MoveCube(int from, int to)
        {
            _role[to] = _role[from];
            _phi[to] = _phi[from];
            _theta[to] = _theta[from];
            _tube[to] = _tube[from];
            _spin[to] = _spin[from];
            _lump[to] = _lump[from];
            _position[to] = _position[from];
            _offset[to] = _offset[from];
            _velocity[to] = _velocity[from];
            _anchor[to] = _anchor[from];
            _age[to] = _age[from];
            _life[to] = _life[from];
            _size[to] = _size[from];
            _edge[to] = _edge[from];
        }

        private void Draw()
        {
            s_mesh ??= TankVoxelMuzzleSmokeBehaviour.BuildCubeMesh();
            s_material ??= TankVoxelMuzzleSmokeBehaviour.LoadMaterial();
            _block ??= new MaterialPropertyBlock();

            float top = _origin.y + _radius * 2.9f;
            for (int start = 0; start < _count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, _count - start);
                for (int k = 0; k < n; k++)
                {
                    int i = start + k;
                    float t = _age[i] / _life[i];
                    float edge = _edge[i];
                    Vector3 p = _position[i];
                    p = new Vector3(Mathf.Round(p.x / GRID) * GRID, Mathf.Round(p.y / GRID) * GRID, Mathf.Round(p.z / GRID) * GRID);
                    _matrices[k] = new Matrix4x4 { m00 = edge, m11 = edge, m22 = edge, m03 = p.x, m13 = p.y, m23 = p.z, m33 = 1f };

                    Color c;
                    float alpha;
                    if (_age[i] < 0.25f)
                    {
                        // Fire for a moment at the start.
                        c = Color.Lerp(Hot * 3f, Dark, _age[i] / 0.25f);
                        alpha = 0.85f;
                    }
                    else
                    {
                        // Greys as it cools; the higher a billow, the more light it catches.
                        float lit = Mathf.Clamp01((p.y - _origin.y) / (top - _origin.y));
                        c = Color.Lerp(Dark, Light, Mathf.SmoothStep(0f, 1f, t * 0.7f + lit * 0.45f));
                        float body = (_role[i] == ROLE_CAP ? 0.29f : 0.32f) * CLEARER; // 20% clearer than the first pass, then 25 % more
                        alpha = body * (1f - Mathf.SmoothStep(0.45f, 1f, t));
                    }
                    _colors[k] = new Vector4(c.r, c.g, c.b, alpha);
                }
                _block.SetVectorArray(InstanceColorId, _colors);
                Graphics.DrawMeshInstanced(s_mesh, 0, s_material, _matrices, n, _block, ShadowCastingMode.Off, false);
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Experiment: the exhaust's cubes in world space — emitted at the pipes, slowed by drag,
    /// rising a little, torn by turbulence, swelling and thinning away; drawn instanced with
    /// Game/VoxelPuff (see-through).
    /// </summary>
    public sealed class VoxelExhaustCloud
    {
        private const int MAX = 6138;
        private const int BATCH = 1023;

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static Mesh s_mesh;
        private static Material s_material;

        private readonly VoxelExhaustSettings _settings;
        private readonly Vector3[] _position = new Vector3[MAX];
        private readonly Vector3[] _velocity = new Vector3[MAX];
        private readonly float[] _age = new float[MAX];
        private readonly float[] _life = new float[MAX];
        private readonly float[] _size = new float[MAX];
        private readonly float[] _alpha = new float[MAX];
        private readonly float[] _shade = new float[MAX];
        private readonly Matrix4x4[] _matrices = new Matrix4x4[MAX];
        private readonly Vector4[] _colors = new Vector4[MAX];
        private readonly Matrix4x4[] _batchMatrices = new Matrix4x4[BATCH];
        private readonly Vector4[] _batchColors = new Vector4[BATCH];
        private MaterialPropertyBlock _block;
        private int _count;

        public VoxelExhaustCloud(VoxelExhaustSettings settings)
        {
            _settings = settings;
        }

        /// <summary>One cube out of a pipe: <paramref name="speed"/>, <paramref name="size"/> and
        /// <paramref name="alpha"/> as the particle smoke would have them.</summary>
        public void Emit(Vector3 position, Vector3 direction, float speed, float size, float alpha)
        {
            VoxelExhaustSettings s = _settings;
            int i = _count < MAX ? _count++ : Random.Range(0, MAX);

            float cone = Mathf.Tan(s.Cone * Mathf.Deg2Rad);
            Vector3 dir = (direction + Random.insideUnitSphere * cone).normalized;
            _position[i] = position;
            _velocity[i] = dir * (speed * Random.Range(0.7f, 1.2f));
            _age[i] = 0f;
            _life[i] = Mathf.Max(0.05f, Random.Range(s.Lifetime.x, s.Lifetime.y));
            _size[i] = size * Random.Range(0.7f, 1.3f) * s.SizeScale;
            _alpha[i] = alpha;
            _shade[i] = Random.value;
        }

        public void Tick(float deltaTime)
        {
            if (_count == 0 || deltaTime <= 0f)
                return;

            VoxelExhaustSettings s = _settings;
            float damp = Mathf.Exp(-s.Drag * deltaTime);
            float rise = s.Rise * deltaTime;
            float noise = s.Noise * deltaTime;
            float grid = s.Grid;

            for (int i = 0; i < _count; i++)
            {
                _age[i] += deltaTime;
                if (_age[i] >= _life[i])
                {
                    _count--;
                    _position[i] = _position[_count];
                    _velocity[i] = _velocity[_count];
                    _age[i] = _age[_count];
                    _life[i] = _life[_count];
                    _size[i] = _size[_count];
                    _alpha[i] = _alpha[_count];
                    _shade[i] = _shade[_count];
                    i--;
                    continue;
                }

                Vector3 v = _velocity[i] * damp + Random.insideUnitSphere * noise;
                v.y += rise;
                _velocity[i] = v;
                _position[i] += v * deltaTime;

                float t = _age[i] / _life[i];
                // Swells fast at first, like the particles' size curve.
                float swell = 1f + (s.Grow - 1f) * (1f - (1f - t) * (1f - t));
                float edge = _size[i] * swell;
                float alpha = _alpha[i] * s.OpacityScale * (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((t - s.FadeStart) / Mathf.Max(0.01f, 1f - s.FadeStart))));

                Vector3 p = _position[i];
                if (grid > 0.001f)
                    p = new Vector3(Mathf.Round(p.x / grid) * grid, Mathf.Round(p.y / grid) * grid,
                        Mathf.Round(p.z / grid) * grid);
                _matrices[i] = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(edge, edge, edge));

                Color c = Color.Lerp(s.DarkColor, s.LightColor, _shade[i]);
                _colors[i] = new Vector4(c.r, c.g, c.b, Mathf.Clamp01(alpha));
            }

            if (_count == 0)
                return;

            s_mesh ??= TankVoxelMuzzleSmokeBehaviour.BuildCubeMesh();
            s_material ??= TankVoxelMuzzleSmokeBehaviour.LoadMaterial();
            _block ??= new MaterialPropertyBlock();
            for (int start = 0; start < _count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, _count - start);
                System.Array.Copy(_matrices, start, _batchMatrices, 0, n);
                System.Array.Copy(_colors, start, _batchColors, 0, n);
                _block.SetVectorArray(InstanceColorId, _batchColors);
                Graphics.DrawMeshInstanced(s_mesh, 0, s_material, _batchMatrices, n, _block, ShadowCastingMode.Off, false);
            }
        }
    }
}

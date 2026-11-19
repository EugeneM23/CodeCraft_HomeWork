using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// The air bomb's smoke as voxel cubes (the cannon's VoxelPuff look): a dark burst thrown out and up from the
    /// blast, which slows, keeps rising as a column, swells, greys and thins away. Cubes react to walls, tanks and
    /// shots through <see cref="VoxelPuffField"/>. One object per blast, gone when its last cube is.
    /// </summary>
    public sealed class VoxelBlastSmoke : MonoBehaviour
    {
        private const int MAX = 900;
        private const int BATCH = 1023;
        private const float LIFETIME = 9f;
        private const float DRAG = 1.8f;
        private const float RISE = 0.3f;
        private const float GRID = 0.1f;

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static Mesh s_mesh;
        private static Material s_material;

        private static readonly Color Dark = new(0.16f, 0.15f, 0.14f, 1f);
        private static readonly Color Light = new(0.55f, 0.54f, 0.52f, 1f);
        private static readonly Color Hot = new(1f, 0.5f, 0.15f, 1f);

        private readonly Vector3[] _position = new Vector3[MAX];
        private readonly Vector3[] _velocity = new Vector3[MAX];
        private readonly float[] _age = new float[MAX];
        private readonly float[] _life = new float[MAX];
        private readonly float[] _size = new float[MAX];
        private readonly Matrix4x4[] _matrices = new Matrix4x4[BATCH];
        private readonly Vector4[] _colors = new Vector4[BATCH];
        private MaterialPropertyBlock _block;
        private int _count;

        /// <summary>Sets the smoke of a blast of <paramref name="radius"/> metres off at <paramref name="point"/>.</summary>
        public static void Spawn(Vector3 point, float radius)
        {
            var go = new GameObject("<VoxelBlastSmoke>");
            go.transform.position = point;
            go.AddComponent<VoxelBlastSmoke>().Burst(point, Mathf.Max(1f, radius));
        }

        private void Burst(Vector3 point, float radius)
        {
            _count = MAX;
            for (int i = 0; i < _count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                bool column = i % 3 == 0;
                // A third goes up as the column, the rest out low and wide as the skirt.
                Vector3 v = column
                    ? new Vector3(dir.x * 1.5f, Random.Range(2.5f, 5f), dir.z * 1.5f)
                    : new Vector3(dir.x, dir.y * 0.5f, dir.z).normalized * Random.Range(4f, 11f) * radius / 3f;
                _position[i] = point + Random.insideUnitSphere * radius * 0.25f + Vector3.up * 0.3f;
                _velocity[i] = v;
                _age[i] = 0f;
                _life[i] = LIFETIME * Random.Range(0.55f, 1f);
                _size[i] = Random.Range(0.25f, 0.55f) * (column ? 1.2f : 1f);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            VoxelPuffField.Apply(_position, _velocity, _size, _count, dt, true, true);

            float damp = Mathf.Exp(-DRAG * dt);
            Vector3 rise = Vector3.up * (RISE * dt);
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
                _velocity[i] = _velocity[i] * damp + rise;
                _position[i] += _velocity[i] * dt;
            }

            if (_count == 0)
            {
                Destroy(gameObject);
                return;
            }
            Draw();
        }

        private void Draw()
        {
            s_mesh ??= TankVoxelMuzzleSmokeBehaviour.BuildCubeMesh();
            s_material ??= TankVoxelMuzzleSmokeBehaviour.LoadMaterial();
            _block ??= new MaterialPropertyBlock();

            for (int start = 0; start < _count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, _count - start);
                for (int k = 0; k < n; k++)
                {
                    int i = start + k;
                    float t = _age[i] / _life[i];
                    // Swells to three times its size, the column most.
                    float edge = _size[i] * Mathf.Lerp(0.6f, 3f, Mathf.Sqrt(t));
                    Vector3 p = _position[i];
                    p = new Vector3(Mathf.Round(p.x / GRID) * GRID, Mathf.Round(p.y / GRID) * GRID, Mathf.Round(p.z / GRID) * GRID);
                    _matrices[k] = new Matrix4x4 { m00 = edge, m11 = edge, m22 = edge, m03 = p.x, m13 = p.y, m23 = p.z, m33 = 1f };

                    Color c;
                    float alpha;
                    if (_age[i] < 0.18f)
                    {
                        // Fire for a moment at the start.
                        c = Color.Lerp(Hot * 3f, Dark, _age[i] / 0.18f);
                        alpha = 0.85f;
                    }
                    else
                    {
                        c = Color.Lerp(Dark, Light, Mathf.SmoothStep(0f, 1f, t));
                        alpha = 0.32f * (1f - Mathf.SmoothStep(0.35f, 1f, t));
                    }
                    _colors[k] = new Vector4(c.r, c.g, c.b, alpha);
                }
                _block.SetVectorArray(InstanceColorId, _colors);
                Graphics.DrawMeshInstanced(s_mesh, 0, s_material, _matrices, n, _block, ShadowCastingMode.Off, false);
            }
        }
    }
}

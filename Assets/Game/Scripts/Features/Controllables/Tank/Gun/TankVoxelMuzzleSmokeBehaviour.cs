using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Experiment: the cannon's smoke drawn as see-through voxel cubes (Game/VoxelPuff, instanced). A shot
    /// spits a jet along the barrel: thin at the muzzle, fattening towards the far end, where the
    /// biggest, densest blob sits. Every cube flies from the muzzle to its place in that shape over
    /// ExtendTime, then drifts: settling, creeping apart, thinning away. A thin wisp of small cubes
    /// then keeps leaking from the barrel. Cubes stay in world space; the wisp follows the barrel.
    /// </summary>
    public sealed class TankVoxelMuzzleSmokeBehaviour : IEntityInit, IEntityLateTick, IEntityDispose
    {
        private const int MAX = 4092;
        private const int BATCH = 1023;
        private const string MATERIAL = "Voxels/VoxelPuff";

        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");
        private static Mesh s_mesh;
        private static Material s_material;

        private readonly VoxelMuzzleSmokeSettings _settings;

        private readonly Vector3[] _position = new Vector3[MAX];
        private readonly Vector3[] _velocity = new Vector3[MAX];
        private readonly float[] _age = new float[MAX];
        private readonly float[] _life = new float[MAX];
        private readonly float[] _size = new float[MAX];
        // Jet phase: from the muzzle (_start) to its place (_start + _offset) over _extend seconds;
        // _extend is 0 once the cube drifts freely.
        private readonly Vector3[] _start = new Vector3[MAX];
        private readonly Vector3[] _offset = new Vector3[MAX];
        private readonly float[] _extend = new float[MAX];
        // 0...1: how much this cube bursts (weak at the muzzle, full at the far end).
        private readonly float[] _burst = new float[MAX];
        private readonly Matrix4x4[] _matrices = new Matrix4x4[MAX];
        private readonly Vector4[] _colors = new Vector4[MAX];
        private readonly Matrix4x4[] _batchMatrices = new Matrix4x4[BATCH];
        private readonly Vector4[] _batchColors = new Vector4[BATCH];
        private MaterialPropertyBlock _block;
        private int _count;

        private Transform _firePoint;
        private IEvent _fireEvent;
        private float _wispLeft;
        private float _wispCarry;

        public TankVoxelMuzzleSmokeBehaviour(VoxelMuzzleSmokeSettings settings)
        {
            _settings = settings;
        }

        public void Init(IEntity entity)
        {
            if (!entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value == null)
                return;

            weapon.Value.TryGetFirePoint(out _firePoint);
            if (weapon.Value.TryGetFireEvent(out _fireEvent))
                _fireEvent.Subscribe(OnFire);
        }

        public void Dispose(IEntity entity)
        {
            _fireEvent?.Unsubscribe(OnFire);
            _fireEvent = null;
            _count = 0;
        }

        private void OnFire()
        {
            if (!_settings.Voxel || _firePoint == null)
                return;

            Vector3 origin = _firePoint.position;
            Vector3 forward = _firePoint.forward;
            Vector3 right = _firePoint.right;
            Vector3 up = _firePoint.up;
            VoxelMuzzleSmokeSettings s = _settings;

            float length = Mathf.Max(0.1f, s.JetLength);
            float tipRadius = Mathf.Max(0.01f, s.RadiusEnd);
            if (s.ReactToWorld)
            {
                // A wall ahead: the jet ends short of it, the cap bursting against it.
                float reach = VoxelPuffField.Reach(origin, forward, length + tipRadius * 0.6f);
                length = Mathf.Max(0.3f, Mathf.Min(length, reach - tipRadius * 0.6f));
            }
            for (int i = 0; i < s.Cubes; i++)
            {
                float along; // 0 at the muzzle, 1 at the far end.
                Vector3 local;
                Vector3 outward; // Where it bursts to.
                if (Random.value < s.TipShare)
                {
                    // The mushroom's cap: a ball round the far end, flattened along the barrel, its
                    // centre a little short of the end. It bursts out across the axis, the brim
                    // rolling back towards the muzzle.
                    Vector3 ball = Random.insideUnitSphere * tipRadius;
                    float axial = Vector3.Dot(ball, forward);
                    Vector3 across = ball - forward * axial;
                    local = forward * (length - tipRadius * 0.3f + axial * s.CapFlatten) + across;
                    along = 1f;
                    Vector3 side = across.sqrMagnitude > 1e-6f ? across.normalized
                        : (right * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)).normalized;
                    // Out at the rim most (the cap's edge rolls), less in the middle.
                    float rim = Mathf.Clamp01(across.magnitude / tipRadius);
                    outward = (side * (1f - s.CapCurl * rim) - forward * (s.CapCurl * rim)).normalized;
                }
                else
                {
                    // The spindle: more cubes further out, its radius swelling late.
                    along = Mathf.Pow(Random.value, 1f / (1f + Mathf.Max(0f, s.DensityToEnd)));
                    float radius = Mathf.Lerp(s.RadiusMuzzle, tipRadius, Mathf.Pow(along, Mathf.Max(0.1f, s.Taper)));
                    Vector2 disc = Random.insideUnitCircle * radius;
                    local = forward * (length * along) + right * disc.x + up * disc.y;
                    Vector3 off = right * disc.x + up * disc.y;
                    outward = off.sqrMagnitude > 1e-6f ? off.normalized : Random.onUnitSphere;
                }

                float size = Mathf.Lerp(s.SizeMuzzle, s.SizeEnd, along) * Random.Range(0.75f, 1.25f);
                // The far end arrives last: the jet stretches out.
                float extend = Mathf.Max(0.02f, s.ExtendTime * Mathf.Lerp(0.25f, 1f, along) * Random.Range(0.85f, 1.15f));
                // What it keeps afterwards: on along the jet, out from its axis.
                Vector3 radial = local - forward * Vector3.Dot(local, forward);
                Vector3 residual = (forward + radial.normalized * 0.6f).normalized * (s.ResidualSpeed * Random.Range(0.5f, 1.5f));

                // The burst: out from the axis (from the blob's centre at the end) and up, strongest at the end.
                float burst = Mathf.Lerp(s.BurstAtMuzzle, 1f, Mathf.Pow(along, Mathf.Max(0f, s.BurstGrowth)));
                Vector3 burstDir = (outward * (1f - s.BurstUp) + Vector3.up * s.BurstUp).normalized;
                residual += burstDir * (s.Burst * burst * Random.Range(0.5f, 1.3f));

                int c = Spawn(origin, residual, s.Lifetime * Random.Range(0.6f, 1f), size);
                _start[c] = origin;
                _offset[c] = local;
                _extend[c] = extend;
                _burst[c] = burst;
            }

            _wispLeft = s.WispTime;
            _wispCarry = 0f;
        }

        /// <summary>A cube drifting freely from the start (the jet sets its own phase after); its slot.</summary>
        private int Spawn(Vector3 position, Vector3 velocity, float life, float size)
        {
            int i;
            if (_count < MAX)
                i = _count++;
            else
                i = Random.Range(0, MAX); // Full: a random old cube makes room.

            _position[i] = position;
            _velocity[i] = velocity;
            _age[i] = 0f;
            _life[i] = Mathf.Max(0.05f, life);
            _size[i] = size;
            _extend[i] = 0f;
            _burst[i] = 0f;
            return i;
        }

        public void LateTick(IEntity entity, float deltaTime)
        {
            VoxelMuzzleSmokeSettings s = _settings;

            if (_wispLeft > 0f && _firePoint != null && s.WispTime > 0f)
            {
                float share = _wispLeft / s.WispTime;
                _wispCarry += s.WispRate * share * deltaTime;
                while (_wispCarry >= 1f)
                {
                    _wispCarry -= 1f;
                    Vector3 v = _firePoint.forward * Random.Range(0.3f, 0.9f) + Vector3.up * Random.Range(0.2f, 0.6f)
                        + Random.insideUnitSphere * 0.15f;
                    Spawn(_firePoint.position, v, s.Lifetime * Random.Range(0.4f, 0.7f),
                        s.WispSize * Random.Range(0.7f, 1.3f));
                }
                _wispLeft -= deltaTime;
            }

            if (_count == 0)
                return;

            if (s.ReactToWorld || s.ReactToTanks)
                VoxelPuffField.Apply(_position, _velocity, _size, _count, deltaTime, s.ReactToWorld, s.ReactToTanks, _extend);

            float damp = Mathf.Exp(-s.Drag * deltaTime);
            Vector3 rise = Vector3.up * (s.Rise * deltaTime);
            float grid = s.Grid;

            for (int i = 0; i < _count; i++)
            {
                _age[i] += deltaTime;
                if (_age[i] >= _life[i])
                {
                    // Swap the last live cube into this slot and look at the slot again.
                    _count--;
                    _position[i] = _position[_count];
                    _velocity[i] = _velocity[_count];
                    _age[i] = _age[_count];
                    _life[i] = _life[_count];
                    _size[i] = _size[_count];
                    _start[i] = _start[_count];
                    _offset[i] = _offset[_count];
                    _extend[i] = _extend[_count];
                    _burst[i] = _burst[_count];
                    i--;
                    continue;
                }

                if (_extend[i] > 0f)
                {
                    // Jet phase: spat out fast, braking into its place in the jet.
                    float x = _age[i] / _extend[i];
                    if (x < 1f)
                    {
                        float ease = 1f - Mathf.Pow(1f - x, Mathf.Max(1f, s.EjectSharpness));
                        _position[i] = _start[i] + _offset[i] * ease;
                    }
                    else
                    {
                        _position[i] = _start[i] + _offset[i];
                        _extend[i] = 0f;
                    }
                }
                else
                {
                    Vector3 v = _velocity[i];
                    // Keep a slowed cube creeping sideways, along where it was already heading: the cloud spreads.
                    float flat = Mathf.Sqrt(v.x * v.x + v.z * v.z);
                    if (flat > 1e-4f)
                    {
                        float push = s.Creep * (1f + (s.BurstCreep - 1f) * _burst[i]) * deltaTime / flat;
                        v.x += v.x * push;
                        v.z += v.z * push;
                    }
                    _velocity[i] = v * damp + rise;
                    _position[i] += _velocity[i] * deltaTime;
                }

                float t = _age[i] / _life[i];
                // Swells from 40 % to its full edge, holds, then shrinks by half as it thins.
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.25f));
                float shrink = 1f - 0.5f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.6f) / 0.4f));
                // Bursting smoke keeps swelling over its life.
                float swell = 1f + (s.BurstSwell - 1f) * _burst[i] * Mathf.Clamp01(t * 1.5f);
                float edge = Mathf.Lerp(0.4f, 1f, grow) * shrink * swell * _size[i];
                float alpha = s.Opacity * (1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((t - s.FadeStart) / Mathf.Max(0.01f, 1f - s.FadeStart))));

                Vector3 p = _position[i];
                if (grid > 0.001f)
                    p = new Vector3(Mathf.Round(p.x / grid) * grid, Mathf.Round(p.y / grid) * grid,
                        Mathf.Round(p.z / grid) * grid);
                _matrices[i] = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(edge, edge, edge));

                Color c;
                if (_age[i] < s.HotTime)
                {
                    // Fire: bright and nearly solid, cooling to smoke.
                    float k = _age[i] / Mathf.Max(0.001f, s.HotTime);
                    c = Color.Lerp(s.HotColor * s.HotGlow, s.SmokeColor, k);
                    alpha = Mathf.Lerp(Mathf.Max(alpha, 0.85f), alpha, k);
                }
                else
                    c = Color.Lerp(s.SmokeColor, s.FadeColor, t);
                _colors[i] = new Vector4(c.r, c.g, c.b, alpha);
            }

            if (_count == 0)
                return;

            s_mesh ??= BuildCubeMesh();
            s_material ??= LoadMaterial();
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

        internal static Material LoadMaterial()
        {
            Material material = Resources.Load<Material>(MATERIAL);
            if (material == null)
                material = new Material(Shader.Find("Game/VoxelPuff"));
            if (!material.enableInstancing)
                material = new Material(material) { enableInstancing = true };
            return material;
        }

        /// <summary>A unit cube with white vertex colours (the Voxel shader multiplies by them).</summary>
        internal static Mesh BuildCubeMesh()
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var colors = new Color32[24];
            var triangles = new int[36];
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
            {
                Vector3 a = n[f];
                Vector3 u = Mathf.Abs(a.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(a, u);
                Vector3 c = a * 0.5f;
                int v = f * 4;
                vertices[v] = c - u * 0.5f - w * 0.5f;
                vertices[v + 1] = c + u * 0.5f - w * 0.5f;
                vertices[v + 2] = c + u * 0.5f + w * 0.5f;
                vertices[v + 3] = c - u * 0.5f + w * 0.5f;
                for (int k = 0; k < 4; k++)
                {
                    normals[v + k] = a;
                    colors[v + k] = new Color32(255, 255, 255, 255);
                }
                // Winding chosen so the face points along a (clockwise seen from outside).
                int t = f * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "VoxelMuzzleSmokeCube", vertices = vertices, normals = normals, colors32 = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

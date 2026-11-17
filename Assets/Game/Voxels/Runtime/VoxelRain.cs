using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxels
{
    /// <summary>
    /// Voxel rain: thousands of long thin boxes falling slant in the wind in a block of air round
    /// what the camera looks at, each landing in a splash of tiny cubes.
    ///
    /// A drop does not look for the ground every frame: it falls in a straight line, so one ray
    /// when it is born (or carried round to the other side of the block as the camera moves) tells
    /// where it lands. Moving the drops and the splashes is a Burst job; GPU instancing draws them.
    /// </summary>
    public sealed class VoxelRain : MonoBehaviour
    {
        private const int BATCH = 1023;
        private const string MATERIAL = "Voxels/VoxelRain";
        private static readonly int InstanceColorId = Shader.PropertyToID("_InstColor");

        [SerializeField] private int _drops = 2300;
        [Tooltip("Side (m) of the square of rain round what the camera looks at.")]
        [SerializeField] private float _area = 44f;
        [Tooltip("Drops are born this high (m) over the point the camera looks at.")]
        [SerializeField] private float _height = 20f;
        [SerializeField] private float _fallSpeed = 12f;
        [SerializeField] private Vector3 _wind = new(2.2f, 0f, 1.1f);
        [SerializeField] private Vector3 _dropSize = new(0.03f, 0.42f, 0.03f);
        [SerializeField] private Color _dropColor = new(0.72f, 0.78f, 0.88f, 0.38f);
        [Tooltip("Cubes a landing drop throws up.")]
        [SerializeField] private int _splashCubes = 3;
        [SerializeField] private float _splashSize = 0.04f;
        [SerializeField] private Color _splashColor = new(0.8f, 0.85f, 0.95f, 0.6f);
        [Tooltip("Sunlight and ambient light are dimmed to this share while it rains.")]
        [Range(0f, 1f)]
        [SerializeField] private float _gloom = 0.7f;

        private struct Drop
        {
            public float3 Position;
            public float Speed;
            public float LandY;
            public int NeedsRay;
        }

        private struct Splash
        {
            public float3 Position;
            public float3 Velocity;
            public float Life;
        }

        private NativeArray<Drop> _dropsData;
        private NativeList<Splash> _splashes;
        private NativeList<int> _rays;
        private NativeList<float3> _landed;
        private NativeArray<float4x4> _dropMatrices;
        private NativeArray<float4x4> _splashMatrices;
        private NativeReference<int> _splashCount;

        private readonly Matrix4x4[] _batch = new Matrix4x4[BATCH];
        private readonly Vector4[] _colors = new Vector4[BATCH];
        private MaterialPropertyBlock _dropBlock;
        private MaterialPropertyBlock _splashBlock;

        private Mesh _mesh;
        private Material _material;
        private uint _seed = 7;
        private float3 _centre;
        private bool _created;

        private Light _sun;
        private float _sunIntensity;
        private Color _ambient;
        private float _ambientIntensity;

        public int Drops => _drops;
        public int Splashes => _splashes.IsCreated ? _splashes.Length : 0;

        /// <summary>What drops land on: everything but bullets, debris and Ignore Raycast.</summary>
        public int GroundMask { get; set; } = ~((1 << 2) | (1 << 12) | (1 << 15));

        private void OnEnable()
        {
            _dropBlock ??= new MaterialPropertyBlock();
            _splashBlock ??= new MaterialPropertyBlock();
            _mesh = VoxelDebris.BuildCubeMesh();
            _material = LoadMaterial();

            _dropsData = new NativeArray<Drop>(_drops, Allocator.Persistent);
            _splashes = new NativeList<Splash>(_drops, Allocator.Persistent);
            _rays = new NativeList<int>(_drops, Allocator.Persistent);
            _landed = new NativeList<float3>(_drops, Allocator.Persistent);
            _dropMatrices = new NativeArray<float4x4>(_drops, Allocator.Persistent);
            _splashMatrices = new NativeArray<float4x4>(_drops * 2, Allocator.Persistent);
            _splashCount = new NativeReference<int>(Allocator.Persistent);
            _created = true;

            _centre = Focus();
            var random = new Unity.Mathematics.Random(_seed++);
            for (int i = 0; i < _drops; i++)
            {
                float3 p = _centre + new float3(
                    random.NextFloat(-0.5f, 0.5f) * _area,
                    random.NextFloat(-2f, _height),
                    random.NextFloat(-0.5f, 0.5f) * _area);
                _dropsData[i] = new Drop { Position = p, Speed = _fallSpeed * random.NextFloat(0.85f, 1.15f), NeedsRay = 1 };
            }

            Gloom(true);
        }

        private void OnDisable()
        {
            Gloom(false);
            if (!_created)
                return;
            _dropsData.Dispose();
            _splashes.Dispose();
            _rays.Dispose();
            _landed.Dispose();
            _dropMatrices.Dispose();
            _splashMatrices.Dispose();
            _splashCount.Dispose();
            if (_mesh != null)
                Destroy(_mesh);
            _created = false;
        }

        /// <summary>A rainy day is a grey one: the sun and the sky light are turned down while it rains.</summary>
        private void Gloom(bool on)
        {
            if (on)
            {
                _sun = RenderSettings.sun;
                if (_sun == null)
                    foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (light.type == LightType.Directional)
                        {
                            _sun = light;
                            break;
                        }

                if (_sun != null)
                {
                    _sunIntensity = _sun.intensity;
                    _sun.intensity *= _gloom;
                }

                _ambient = RenderSettings.ambientLight;
                _ambientIntensity = RenderSettings.ambientIntensity;
                RenderSettings.ambientLight = _ambient * _gloom;
                RenderSettings.ambientIntensity = _ambientIntensity * _gloom;
            }
            else
            {
                if (_sun != null)
                    _sun.intensity = _sunIntensity;
                RenderSettings.ambientLight = _ambient;
                RenderSettings.ambientIntensity = _ambientIntensity;
            }
        }

        /// <summary>The point the camera looks at, on the level the camera's own ground is on.</summary>
        private static float3 Focus()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return float3.zero;

            Transform t = camera.transform;
            float groundY = 0f;
            if (Physics.Raycast(t.position, t.forward, out RaycastHit hit, 200f, ~((1 << 2) | (1 << 12) | (1 << 15)), QueryTriggerInteraction.Ignore))
                return hit.point;
            if (math.abs(t.forward.y) > 0.05f)
            {
                float distance = math.clamp((groundY - t.position.y) / t.forward.y, 0f, 80f);
                return (float3)(t.position + t.forward * distance);
            }

            return (float3)t.position;
        }

        private void LateUpdate()
        {
            if (!_created)
                return;

            float dt = math.min(Time.deltaTime, 1f / 20f);
            _centre = math.lerp(_centre, Focus(), math.saturate(dt * 4f));
            float3 wind = _wind;

            // Born under the camera: a drop falling past the lens is a white bar across the screen.
            float height = _height;
            Camera camera = Camera.main;
            if (camera != null)
                height = math.clamp(camera.transform.position.y - _centre.y - 3f, 4f, _height);

            _rays.Clear();
            _landed.Clear();
            new FallJob
            {
                Drops = _dropsData,
                Centre = _centre,
                Area = _area,
                Height = height,
                Wind = wind,
                FallSpeed = _fallSpeed,
                DeltaTime = dt,
                Seed = _seed++,
                Rays = _rays.AsParallelWriter(),
                Landed = _landed.AsParallelWriter()
            }.Schedule(_drops, 256).Complete();

            CastRays(wind);

            float3 direction = math.normalize(new float3(wind.x, -_fallSpeed, wind.z));
            quaternion tilt = Quaternion.FromToRotation(Vector3.down, direction);
            new SplashJob
            {
                Splashes = _splashes,
                Landed = _landed,
                Drops = _dropsData,
                DropMatrices = _dropMatrices,
                SplashMatrices = _splashMatrices,
                SplashCount = _splashCount,
                Tilt = tilt,
                DropSize = _dropSize,
                SplashSize = _splashSize,
                PerLanding = _splashCubes,
                Gravity = Physics.gravity.y,
                DeltaTime = dt,
                Seed = _seed++
            }.Schedule().Complete();

            Draw(_dropMatrices, _drops, _dropColor, _dropBlock);
            Draw(_splashMatrices, _splashCount.Value, _splashColor, _splashBlock);
        }

        /// <summary>One ray for each drop just born or carried round: where along its slant it lands.</summary>
        private void CastRays(float3 wind)
        {
            int count = _rays.Length;
            if (count == 0)
                return;

            var commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
            var hits = new NativeArray<RaycastHit>(count, Allocator.TempJob);
            var query = new QueryParameters(GroundMask, false, QueryTriggerInteraction.Ignore, false);
            for (int i = 0; i < count; i++)
            {
                Drop drop = _dropsData[_rays[i]];
                float3 velocity = new float3(wind.x, -drop.Speed, wind.z);
                commands[i] = new RaycastCommand(drop.Position, math.normalize(velocity), query, _height + 60f);
            }

            RaycastCommand.ScheduleBatch(commands, hits, 32, 1).Complete();

            for (int i = 0; i < count; i++)
            {
                int index = _rays[i];
                Drop drop = _dropsData[index];
                RaycastHit hit = hits[i];
                drop.LandY = hit.colliderInstanceID != 0 ? hit.point.y : _centre.y - 40f;
                drop.NeedsRay = 0;
                _dropsData[index] = drop;
            }

            commands.Dispose();
            hits.Dispose();
        }

        private void Draw(NativeArray<float4x4> source, int count, Color color, MaterialPropertyBlock block)
        {
            if (count == 0)
                return;

            var c = new Vector4(color.r, color.g, color.b, color.a);
            for (int i = 0; i < BATCH; i++)
                _colors[i] = c;
            block.SetVectorArray(InstanceColorId, _colors);

            NativeArray<Matrix4x4> matrices = source.Reinterpret<Matrix4x4>();
            for (int start = 0; start < count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, count - start);
                NativeArray<Matrix4x4>.Copy(matrices, start, _batch, 0, n);
                Graphics.DrawMeshInstanced(_mesh, 0, _material, _batch, n, block, ShadowCastingMode.Off, false);
            }
        }

        private static Material LoadMaterial()
        {
            Material material = Resources.Load<Material>(MATERIAL);
            if (material == null)
            {
                Shader shader = Shader.Find("Game/VoxelRain");
                material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            }

            if (!material.enableInstancing)
                material = new Material(material) { enableInstancing = true };
            return material;
        }

        /// <summary>
        /// Drops fall along the wind; one that reached its ground lands (a splash is asked for) and
        /// is born again at the top; one the moving block left behind is carried to its far side.
        /// </summary>
        [BurstCompile]
        private struct FallJob : IJobParallelFor
        {
            public NativeArray<Drop> Drops;
            public float3 Centre;
            public float Area;
            public float Height;
            public float3 Wind;
            public float FallSpeed;
            public float DeltaTime;
            public uint Seed;
            public NativeList<int>.ParallelWriter Rays;
            public NativeList<float3>.ParallelWriter Landed;

            public void Execute(int i)
            {
                Drop drop = Drops[i];
                var random = new Unity.Mathematics.Random(Seed * 1664525u + (uint)i * 22695477u + 1u);

                if (drop.NeedsRay == 0)
                {
                    drop.Position += new float3(Wind.x, -drop.Speed, Wind.z) * DeltaTime;
                    if (drop.Position.y <= drop.LandY)
                    {
                        Landed.AddNoResize(new float3(drop.Position.x, drop.LandY, drop.Position.z));
                        drop.Position = Centre + new float3(
                            random.NextFloat(-0.5f, 0.5f) * Area,
                            Height * random.NextFloat(0.85f, 1.1f),
                            random.NextFloat(-0.5f, 0.5f) * Area);
                        drop.Speed = FallSpeed * random.NextFloat(0.85f, 1.15f);
                        drop.NeedsRay = 1;
                    }
                }

                float half = Area * 0.5f;
                float2 off = drop.Position.xz - Centre.xz;
                if (math.abs(off.x) > half)
                {
                    drop.Position.x -= math.sign(off.x) * Area;
                    drop.NeedsRay = 1;
                }

                if (math.abs(off.y) > half)
                {
                    drop.Position.z -= math.sign(off.y) * Area;
                    drop.NeedsRay = 1;
                }

                if (drop.Position.y > Centre.y + Height * 1.3f || drop.Position.y < Centre.y - 30f)
                {
                    drop.Position.y = Centre.y + Height * random.NextFloat(0.3f, 1f);
                    drop.NeedsRay = 1;
                }

                if (drop.NeedsRay != 0)
                    Rays.AddNoResize(i);
                Drops[i] = drop;
            }
        }

        /// <summary>
        /// Each landing throws a few tiny cubes up and out; they fall back and are gone in a
        /// quarter of a second. Then every drop and every splash cube gets its matrix.
        /// </summary>
        [BurstCompile]
        private struct SplashJob : IJob
        {
            public NativeList<Splash> Splashes;
            [ReadOnly] public NativeList<float3> Landed;
            [ReadOnly] public NativeArray<Drop> Drops;
            [WriteOnly] public NativeArray<float4x4> DropMatrices;
            public NativeArray<float4x4> SplashMatrices;
            public NativeReference<int> SplashCount;
            public quaternion Tilt;
            public float3 DropSize;
            public float SplashSize;
            public int PerLanding;
            public float Gravity;
            public float DeltaTime;
            public uint Seed;

            public void Execute()
            {
                var random = new Unity.Mathematics.Random(Seed * 747796405u + 1u);
                int capacity = SplashMatrices.Length;

                for (int i = 0; i < Landed.Length; i++)
                {
                    int n = random.NextInt(1, PerLanding + 1);
                    for (int k = 0; k < n && Splashes.Length < capacity; k++)
                    {
                        float2 side = random.NextFloat2Direction() * random.NextFloat(0.3f, 0.9f);
                        Splashes.Add(new Splash
                        {
                            Position = Landed[i] + new float3(0f, 0.02f, 0f),
                            Velocity = new float3(side.x, random.NextFloat(1.0f, 2.0f), side.y),
                            Life = random.NextFloat(0.18f, 0.32f)
                        });
                    }
                }

                int alive = 0;
                for (int i = 0; i < Splashes.Length; i++)
                {
                    Splash s = Splashes[i];
                    s.Life -= DeltaTime;
                    if (s.Life <= 0f)
                        continue;
                    s.Velocity.y += Gravity * DeltaTime;
                    s.Position += s.Velocity * DeltaTime;
                    Splashes[alive] = s;
                    float size = SplashSize * math.saturate(s.Life / 0.12f + 0.3f);
                    SplashMatrices[alive] = float4x4.TRS(s.Position, quaternion.identity, new float3(size));
                    alive++;
                }

                Splashes.ResizeUninitialized(alive);
                SplashCount.Value = alive;

                for (int i = 0; i < Drops.Length; i++)
                {
                    Drop drop = Drops[i];
                    // A drop whose landing is not known yet, or that is already under it, is not drawn.
                    float3 scale = drop.NeedsRay != 0 ? float3.zero : DropSize;
                    DropMatrices[i] = float4x4.TRS(drop.Position, Tilt, scale);
                }
            }
        }
    }
}

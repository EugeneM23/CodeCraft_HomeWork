using Game.Voxels;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Draft: a pool of liquid mud in the sandbox. A bowl is dug in the voxel ground and a surface of
    /// voxel columns lies over it; the columns move as a height field (waves that spread, bounce off the
    /// rim and settle). The player's tank sinks into it about half way, pushes the mud aside where its hull
    /// is (waves and a wake), is dragged and throws mud when it moves. Drawn instanced, no GameObject per
    /// cell. Put up by code at the sandbox's start (the scene is not touched).
    /// </summary>
    public sealed class MudPool : MonoBehaviour
    {
        private const int N = 48;
        private const float CELL = 0.16f;
        private const float RADIUS = N * CELL * 0.5f - CELL;
        private const float SLAB = 0.3f;
        private const float STEP = 1f / 60f;
        private const float WAVE = 0.55f;
        private const float DAMPING = 0.985f;
        private const float DRAG = 1.6f;
        private const float SINK_DEPTH = 0.85f;

        private readonly float[] _h = new float[N * N];
        private readonly float[] _v = new float[N * N];
        private readonly bool[] _inside = new bool[N * N];
        private readonly Matrix4x4[] _dark = new Matrix4x4[N * N];
        private readonly Matrix4x4[] _light = new Matrix4x4[N * N];
        private int _darkCount;
        private int _lightCount;

        private float _level;
        private bool _ready;
        private float _carry;
        private float _splashDebt;

        private Rigidbody _tank;
        private Bounds _hullLocal;

        private Mesh _mesh;
        private Material _darkMaterial;
        private Material _lightMaterial;
        private bool _drawing;

        private static readonly Color32 MudColour = new(74, 52, 34, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            SpawnInSandbox();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) =>
            SpawnInSandbox();

        /// <summary>Put aside by the user (2026-11-18): the pool is not spawned until this is turned back on.</summary>
        private const bool ENABLED = false;

        private static void SpawnInSandbox()
        {
            if (!ENABLED)
                return;
            var start = FindFirstObjectByType<VoxelSandboxStart>();
            if (start == null || FindFirstObjectByType<MudPool>() != null)
                return;
            Transform player = start.Vehicle != null ? start.Vehicle.transform : start.transform;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            var go = new GameObject("[MudPool]");
            go.transform.position = player.position + forward * 12f - right * 9f;
            go.AddComponent<MudPool>()._tank = start.Vehicle != null ? start.Vehicle.GetComponent<Rigidbody>() : null;
        }

        private System.Collections.IEnumerator Start()
        {
            // The voxel ground gets its colliders after load: wait for it, then take the first spot round the
            // asked one where the ground is open (no house, wall or tree over it).
            Vector3 asked = transform.position;
            RaycastHit hit = default;
            Vector3 centre = asked;
            for (float t = 0f; t < 15f && hit.collider == null; t += 0.5f)
            {
                for (int k = 0; k < 24 && hit.collider == null; k++)
                {
                    float angle = k * 0.9f;
                    float reach = k == 0 ? 0f : 4f + k * 1.5f;
                    Vector3 c = asked + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                    // Not under the tank: it starts on firm ground and drives in.
                    if (_tank != null && Vector2.Distance(new Vector2(c.x, c.z),
                            new Vector2(_tank.position.x, _tank.position.z)) < RADIUS + 6f)
                        continue;
                    if (OpenGround(c, out RaycastHit h))
                    {
                        hit = h;
                        centre = c;
                    }
                }
                if (hit.collider == null)
                    yield return new WaitForSeconds(0.5f);
            }
            if (hit.collider == null)
                yield break;

            VoxelBody ground = hit.collider.GetComponentInParent<VoxelBody>();
            ground.CarveCrater(hit.point, RADIUS + CELL, 28f, 0f, 0);
            _level = hit.point.y - 0.12f;
            transform.position = new Vector3(centre.x, _level, centre.z);

            // The bowl's colliders are rebuilt at the end of the frame: wait, then keep only the cells over the dug
            // ground (the bowl is shallow at its rim; a cell there would stick out over the grass).
            yield return null;
            yield return null;
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                Vector2 p = CellOffset(x, z);
                int i = x + N * z;
                _inside[i] = false;
                if (p.magnitude > RADIUS)
                    continue;
                Vector3 at = new(centre.x + p.x, _level + 3f, centre.z + p.y);
                if (!TerrainBelow(at, out float groundY))
                    continue;
                _inside[i] = groundY < _level - SURFACE_CLEARANCE;
            }

            if (_tank != null)
                _hullLocal = HullBounds(_tank);
            CreateLook();
            _ready = true;
        }

        /// <summary>How far (m) the dug ground under a cell must lie below the surface for the cell to be mud.</summary>
        private const float SURFACE_CLEARANCE = 0.12f;

        private static bool TerrainBelow(Vector3 from, out float y)
        {
            y = float.MinValue;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, s_hits, 10f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (s_hits[i].collider.GetComponentInParent<VoxelBody>() is { IsTerrain: true } && s_hits[i].point.y > y)
                    y = s_hits[i].point.y;
            return y > float.MinValue;
        }

        private static readonly RaycastHit[] s_hits = new RaycastHit[16];
        private static readonly Collider[] s_near = new Collider[32];

        /// <summary>Voxel ground under <paramref name="c"/> with nothing else (a house, a wall, a tree) in the pool's reach.</summary>
        private static bool OpenGround(Vector3 c, out RaycastHit ground)
        {
            ground = default;
            int n = Physics.RaycastNonAlloc(c + Vector3.up * 30f, Vector3.down, s_hits, 80f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                VoxelBody body = s_hits[i].collider.GetComponentInParent<VoxelBody>();
                if (body == null || !body.IsTerrain || s_hits[i].point.y <= best)
                    continue;
                best = s_hits[i].point.y;
                ground = s_hits[i];
            }
            if (ground.collider == null)
                return false;

            int near = Physics.OverlapSphereNonAlloc(ground.point + Vector3.up * (RADIUS + 0.3f), RADIUS + 0.5f, s_near, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < near; i++)
            {
                VoxelBody body = s_near[i].GetComponentInParent<VoxelBody>();
                if (body == null || !body.IsTerrain)
                    return false;
            }
            return true;
        }

        private static Vector2 CellOffset(int x, int z) => new((x - (N - 1) * 0.5f) * CELL, (z - (N - 1) * 0.5f) * CELL);

        /// <summary>The tank's solid colliders as one box in its own space.</summary>
        private static Bounds HullBounds(Rigidbody body)
        {
            Transform t = body.transform;
            bool any = false;
            var b = new Bounds();
            foreach (Collider c in body.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || c is not BoxCollider box)
                    continue;
                Vector3 e = box.size * 0.5f;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = box.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    Vector3 local = t.InverseTransformPoint(box.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(local, Vector3.zero); any = true; }
                    else b.Encapsulate(local);
                }
            }
            return any ? b : new Bounds(Vector3.up, new Vector3(2.2f, 1.6f, 3.6f));
        }

        private void Update()
        {
            if (!_ready)
                return;
            _carry += Mathf.Min(Time.deltaTime, 0.1f);
            while (_carry >= STEP)
            {
                _carry -= STEP;
                Displace();
                Waves();
            }
            DragTank(Time.deltaTime);
            BuildMatrices();
        }

        // ---------- The mud ----------

        private void Waves()
        {
            for (int z = 1; z < N - 1; z++)
            for (int x = 1; x < N - 1; x++)
            {
                int i = x + N * z;
                if (!_inside[i])
                    continue;
                float sum = 0f;
                int n = 0;
                if (_inside[i - 1]) { sum += _h[i - 1]; n++; }
                if (_inside[i + 1]) { sum += _h[i + 1]; n++; }
                if (_inside[i - N]) { sum += _h[i - N]; n++; }
                if (_inside[i + N]) { sum += _h[i + N]; n++; }
                float avg = n > 0 ? sum / n : _h[i];
                _v[i] = (_v[i] + (avg - _h[i]) * WAVE) * DAMPING;
            }

            for (int i = 0; i < _h.Length; i++)
                if (_inside[i])
                    _h[i] = Mathf.Clamp(_h[i] + _v[i], -SINK_DEPTH, 0.5f);
        }

        /// <summary>Where the hull is under the surface, the mud is pushed out of it and up round it.</summary>
        private void Displace()
        {
            if (_tank == null)
                return;
            Transform t = _tank.transform;
            Vector3 local = t.InverseTransformPoint(transform.position);
            if (new Vector2(local.x, local.z).magnitude > RADIUS + 4f)
                return;

            for (int z = 1; z < N - 1; z++)
            for (int x = 1; x < N - 1; x++)
            {
                int i = x + N * z;
                if (!_inside[i])
                    continue;
                Vector2 o = CellOffset(x, z);
                Vector3 world = new(transform.position.x + o.x, _level + _h[i], transform.position.z + o.y);
                Vector3 p = t.InverseTransformPoint(world);
                if (p.x < _hullLocal.min.x || p.x > _hullLocal.max.x || p.z < _hullLocal.min.z || p.z > _hullLocal.max.z)
                    continue;
                float bottom = t.TransformPoint(new Vector3(p.x, _hullLocal.min.y, p.z)).y - _level;
                if (_h[i] <= bottom)
                    continue;
                float excess = _h[i] - bottom;
                _h[i] = bottom;
                _v[i] = 0f;
                // The mud goes to the sides and swells there.
                float share = excess * 0.25f;
                _v[i - 1] += share; _v[i + 1] += share; _v[i - N] += share; _v[i + N] += share;
            }
        }

        /// <summary>A tank in the mud is held back and throws mud when it moves.</summary>
        private void DragTank(float dt)
        {
            if (_tank == null)
                return;
            Vector3 p = _tank.worldCenterOfMass;
            Vector2 flat = new(p.x - transform.position.x, p.z - transform.position.z);
            if (flat.magnitude > RADIUS || p.y > _level + 0.6f)
                return;

            Vector3 v = _tank.linearVelocity;
            float keep = Mathf.Exp(-DRAG * dt);
            _tank.linearVelocity = new Vector3(v.x * keep, v.y, v.z * keep);

            float speed = new Vector2(v.x, v.z).magnitude;
            VoxelDebris debris = VoxelWorld.Instance != null ? VoxelWorld.Instance.Debris : null;
            if (debris == null || speed < 0.5f)
                return;
            _splashDebt += speed * 40f * dt;
            Transform t = _tank.transform;
            while (_splashDebt >= 1f)
            {
                _splashDebt -= 1f;
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 at = t.TransformPoint(new Vector3(side * _hullLocal.extents.x, _hullLocal.min.y + 0.2f,
                    Random.Range(_hullLocal.min.z, _hullLocal.max.z)));
                at.y = Mathf.Max(at.y, _level + 0.05f);
                Vector3 throwOut = t.right * side * Random.Range(1f, 3f) + Vector3.up * Random.Range(2f, 4.5f) - v * 0.2f;
                if (debris.Spawn(at, throwOut, Shade(), Random.Range(0.05f, 0.1f)) < 0)
                    break;
            }
        }

        private static Color32 Shade()
        {
            float k = Random.Range(0.8f, 1.15f);
            return new Color32((byte)Mathf.Min(255, MudColour.r * k), (byte)Mathf.Min(255, MudColour.g * k),
                (byte)Mathf.Min(255, MudColour.b * k), 255);
        }

        // ---------- Drawing ----------

        private void BuildMatrices()
        {
            _darkCount = 0;
            _lightCount = 0;
            Vector3 size = new(CELL, SLAB, CELL);
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int i = x + N * z;
                if (!_inside[i])
                    continue;
                Vector2 o = CellOffset(x, z);
                // Snapped to a 4 cm step: the surface stays blocky, like the voxels round it.
                float y = Mathf.Round((_level + _h[i]) / 0.04f) * 0.04f - SLAB * 0.5f;
                var m = Matrix4x4.TRS(new Vector3(transform.position.x + o.x, y, transform.position.z + o.y), Quaternion.identity, size);
                if (_h[i] > 0.05f)
                    _light[_lightCount++] = m;
                else
                    _dark[_darkCount++] = m;
            }
        }

        private void CreateLook()
        {
            _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_mesh == null || shader == null)
                return;
            _darkMaterial = Wet(shader, new Color(0.29f, 0.2f, 0.13f));
            _lightMaterial = Wet(shader, new Color(0.42f, 0.31f, 0.2f));
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _drawing = true;
        }

        private static Material Wet(Shader shader, Color colour)
        {
            var m = new Material(shader) { enableInstancing = true, name = "Mud (runtime)" };
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Smoothness", 0.82f);
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView))
                return;
            Draw(camera, _darkMaterial, _dark, _darkCount);
            Draw(camera, _lightMaterial, _light, _lightCount);
        }

        private void Draw(Camera camera, Material material, Matrix4x4[] matrices, int count)
        {
            if (count == 0 || material == null)
                return;
            var rp = new RenderParams(material)
            {
                camera = camera,
                worldBounds = new Bounds(transform.position, new Vector3(N * CELL, 4f, N * CELL)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true
            };
            for (int start = 0; start < count; start += 1023)
                Graphics.RenderMeshInstanced(rp, _mesh, 0, matrices, Mathf.Min(1023, count - start), start);
        }

        private void OnDestroy()
        {
            if (_drawing)
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (_darkMaterial != null) Destroy(_darkMaterial);
            if (_lightMaterial != null) Destroy(_lightMaterial);
        }
    }
}

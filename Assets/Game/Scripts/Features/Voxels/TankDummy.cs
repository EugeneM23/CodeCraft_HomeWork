using System.Collections.Generic;
using Game.Voxels;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A standing voxel target tank (prototype). Outside it is Tank_01 as the game shows it: a copy of
    /// the whole prefab with every behaviour, collider and body taken off before it wakes (no second
    /// tank), voxelized by Tank_01's own VoxelizedModel — the same parts, camouflage, decals and number.
    /// The voxels get box colliders (bullets hit and pierce them), and the hull is hollowed out only
    /// deep inside: armour all round, roof and floor included. In the hollow, cut to fit it: seats,
    /// levers, a lit panel, the engine in the stern, shells along a side, and four seated crewmen
    /// who pop in blood when hit (TankDummyCrew).
    /// </summary>
    public sealed class TankDummy : MonoBehaviour
    {
        [Tooltip("The tank whose look and voxel skin are taken (Tank_01).")]
        [SerializeField] private GameObject _tank;

        [Tooltip("Armour left round the hollow, m.")]
        [SerializeField] private float _armour = 0.06f;

        [SerializeField] private int _crew = 4;

        private const float VOXEL = 0.05f;
        private const string HULL = "HullVisual";

        /// <summary>Two targets in the sandbox at load (off: the tank destruction lab has them now).</summary>
        private static readonly bool SANDBOX_TARGETS = false;

        private bool _built;

        /// <summary>The tank's own voxel size (its VoxelizedModel's): the hull grid's cells.</summary>
        private float _cell = VOXEL;

        private void Start() => Build();

        private void Build()
        {
            if (_built || _tank == null)
                return;
            _built = true;

            // The whole tank, under a switched-off holder: nothing in it wakes.
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            GameObject copy = Instantiate(_tank, holder.transform, false);
            copy.name = "Tank";
            Strip(copy);
            SetLayer(copy.transform, gameObject.layer);
            copy.transform.SetParent(transform, false);
            Destroy(holder);

            // Its own VoxelizedModel voxelizes it as the game does; the bodies are made here (with colliders).
            var model = copy.GetComponent<VoxelizedModel>();
            if (model == null)
                return;
            _cell = (float)(typeof(VoxelizedModel).GetField("_voxelSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(model) ?? VOXEL);
            List<VoxelizedModel.Part> parts = model.Voxelize();
            DestroyImmediate(model);

            foreach (VoxelizedModel.Part part in parts)
            {
                MeshVoxelizer.Result grid = part.Grid;
                bool hull = part.Root.name == HULL;
                // The turret is a shell of armour too (it was solid steel: bullets only chipped it).
                bool turret = part.Root.name == "TurretVisual";
                NativeArray<bool> hollow = hull || turret ? Hollow(grid) : default;
                if (turret)
                    OpenTurretFloor(grid, hollow);

                var go = new GameObject("Voxels") { layer = gameObject.layer };
                go.transform.SetParent(part.Root, false);
                VoxelBodySettings settings = VoxelBodySettings.Default(_cell);
                settings.Origin = grid.Origin;
                settings.Anchor = VoxelAnchor.Largest;
                settings.Colliders = true;
                settings.Layer = gameObject.layer;
                // Armour: a blast bites a little under half as far into it as into brick.
                settings.Toughness = 0.45f;
                settings.Density = 7800f;
                settings.FractureSpeed = 4f;
                go.AddComponent<VoxelBody>().Init(grid.Size, grid.Voxels, grid.Palette, settings);
                foreach (MeshRenderer renderer in part.Renderers)
                    renderer.enabled = false;

                if (hull)
                    Furnish(part.Root, grid, hollow);
                else if (turret)
                    SeatCommander(part.Root, grid, hollow);
                if (hollow.IsCreated)
                    hollow.Dispose();
            }
        }

        /// <summary>Meshes only: every behaviour, collider and body of the copy goes (dependents first).</summary>
        private static void Strip(GameObject root)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                Component[] all = root.GetComponentsInChildren<Component>(true);
                for (int i = all.Length - 1; i >= 0; i--)
                {
                    Component c = all[i];
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is VoxelizedModel)
                        continue;
                    if (c is MonoBehaviour or Collider or Rigidbody or Joint or AudioSource or ParticleSystem or Light or
                        ParticleSystemRenderer or LineRenderer or TrailRenderer or Animator)
                        DestroyImmediate(c);
                }
            }
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t)
                SetLayer(child, layer);
        }

        /// <summary>
        /// Empties the hull only deep inside: a voxel goes when every voxel within the armour's
        /// thickness along each axis is solid too. Returns the hollow (what was taken, plus what was
        /// empty already and shut in), where the inside is put.
        /// </summary>
        private NativeArray<bool> Hollow(MeshVoxelizer.Result grid)
        {
            int3 s = grid.Size;
            int wall = math.max(2, Mathf.RoundToInt(_armour / _cell));
            int count = s.x * s.y * s.z;
            var hollow = new NativeArray<bool>(count, Allocator.Persistent);
            NativeArray<byte> v = grid.Voxels;
            int Index(int x, int y, int z) => x + s.x * (y + s.y * z);
            bool Solid(int x, int y, int z) =>
                x >= 0 && y >= 0 && z >= 0 && x < s.x && y < s.y && z < s.z && v[Index(x, y, z)] != 0;

            for (int z = 0; z < s.z; z++)
            for (int y = 0; y < s.y; y++)
            for (int x = 0; x < s.x; x++)
            {
                if (!Solid(x, y, z))
                    continue;
                bool deep = true;
                for (int d = 1; d <= wall && deep; d++)
                    deep = Solid(x + d, y, z) && Solid(x - d, y, z) && Solid(x, y + d, z) && Solid(x, y - d, z) &&
                           Solid(x, y, z + d) && Solid(x, y, z - d);
                if (deep)
                    hollow[Index(x, y, z)] = true;
            }

            for (int i = 0; i < count; i++)
                if (hollow[i])
                    v[i] = 0;

            // Empty cells shut in by the hull (a voxelizer that leaves meshes hollow) are hollow too.
            var outside = new NativeArray<bool>(count, Allocator.Temp);
            var queue = new Queue<int3>();
            for (int z = 0; z < s.z; z++)
            for (int y = 0; y < s.y; y++)
            for (int x = 0; x < s.x; x++)
            {
                if (x != 0 && y != 0 && z != 0 && x != s.x - 1 && y != s.y - 1 && z != s.z - 1)
                    continue;
                if (Solid(x, y, z))
                    continue;
                outside[Index(x, y, z)] = true;
                queue.Enqueue(new int3(x, y, z));
            }

            while (queue.Count > 0)
            {
                int3 p = queue.Dequeue();
                for (int k = 0; k < 6; k++)
                {
                    int3 n = p;
                    n[k >> 1] += (k & 1) == 0 ? 1 : -1;
                    if (math.any(n < 0) || math.any(n >= s) || Solid(n.x, n.y, n.z) || outside[Index(n.x, n.y, n.z)])
                        continue;
                    outside[Index(n.x, n.y, n.z)] = true;
                    queue.Enqueue(n);
                }
            }

            for (int i = 0; i < count; i++)
                if (v[i] == 0 && !outside[i])
                    hollow[i] = true;
            outside.Dispose();
            return hollow;
        }

        // ---------- The inside ----------

        private static readonly Color32[] Colours =
        {
            default,
            new(96, 92, 58, 255), // 1 khaki
            new(70, 68, 42, 255), // 2 dark khaki
            new(214, 168, 132, 255), // 3 face
            new(48, 52, 40, 255), // 4 helmet
            new(30, 26, 22, 255), // 5 boots, levers
            new(92, 54, 34, 255), // 6 seat leather
            new(70, 74, 78, 255), // 7 panel
            new(170, 210, 120, 255), // 8 dial glow
            new(120, 124, 128, 255), // 9 engine metal
            new(60, 62, 66, 255), // 10 engine dark
            new(196, 160, 72, 255), // 11 brass shell
            new(110, 110, 110, 255), // 12 shell tip
            new(40, 40, 40, 255), // 13 eyes
        };

        /// <summary>The inside, laid out in the hollow's box and cut to the hollow itself.</summary>
        private void Furnish(Transform hull, MeshVoxelizer.Result grid, NativeArray<bool> hollow)
        {
            int3 s = grid.Size;
            int3 lo = new int3(int.MaxValue);
            int3 hi = new int3(int.MinValue);
            for (int z = 0; z < s.z; z++)
            for (int y = 0; y < s.y; y++)
            for (int x = 0; x < s.x; x++)
                if (hollow[x + s.x * (y + s.y * z)])
                {
                    lo = math.min(lo, new int3(x, y, z));
                    hi = math.max(hi, new int3(x, y, z));
                }

            // The inside has its own (coarser) grid over the hollow's box, in the hull's local space.
            float3 min = grid.Origin + (float3)lo * _cell;
            int3 size = (int3)math.floor((float3)(hi - lo + 1) * _cell / VOXEL);
            if (math.any(size < new int3(10, 8, 20)))
                return;

            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            bool Inside(int3 p) =>
                math.all(p >= 0) && math.all(p < size) && InHollow(grid, hollow, min + ((float3)p + 0.5f) * VOXEL);

            void Box(int3 a, int3 b, byte m)
            {
                for (int z = a.z; z <= b.z; z++)
                for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                {
                    var p = new int3(x, y, z);
                    if (Inside(p))
                        voxels[x + size.x * (y + size.y * z)] = m;
                }
            }

            int engine = size.z * 3 / 10;
            // The engine in the stern (−z): a block with a darker head and pipes.
            Box(new int3(2, 0, 0), new int3(size.x - 3, size.y * 6 / 10, engine - 2), 9);
            Box(new int3(4, size.y * 6 / 10, 2), new int3(size.x - 5, size.y * 7 / 10, engine - 4), 10);
            for (int x = 4; x < size.x - 4; x += 4)
                Box(new int3(x, 2, engine - 1), new int3(x + 1, size.y * 5 / 10, engine - 1), 10);

            // Shells upright along the left side, in the middle.
            for (int z = engine + 2; z < engine + 2 + size.z * 3 / 10; z += 3)
            {
                Box(new int3(0, 0, z), new int3(1, 9, z + 1), 11);
                Box(new int3(0, 10, z), new int3(1, 11, z + 1), 12);
            }

            // In front (+z): the panel across the bow, levers before the driver.
            int front = size.z - 1;
            Box(new int3(1, size.y / 2, front - 1), new int3(size.x - 2, size.y * 8 / 10, front), 7);
            for (int x = 3; x < size.x - 3; x += 3)
                Box(new int3(x, size.y * 7 / 10, front - 2), new int3(x, size.y * 7 / 10, front - 2), 8);
            int lx = size.x / 4;
            Box(new int3(lx - 2, 0, front - 6), new int3(lx - 2, size.y / 2, front - 6), 5);
            Box(new int3(lx + 2, 0, front - 6), new int3(lx + 2, size.y / 2, front - 6), 5);

            // Seats: two in front, two under the turret.
            var seats = new List<int3>
            {
                new(size.x / 4, 0, front - 12), new(size.x * 3 / 4, 0, front - 12),
                new(size.x / 4, 0, engine + size.z / 4), new(size.x * 3 / 4, 0, engine + size.z / 4)
            };
            foreach (int3 seat in seats)
            {
                Box(seat + new int3(-3, 0, -3), seat + new int3(3, 3, 3), 6);
                Box(seat + new int3(-3, 4, -4), seat + new int3(3, 12, -3), 6);
            }

            VoxelBody inside = Spawn(hull, "Inside", size, voxels, Palette(), min);
            for (int i = 0; i < seats.Count && i < _crew; i++)
                SpawnCrewman(hull, grid, hollow, min + (float3)(seats[i] + new int3(0, 4, 0)) * VOXEL, inside);
        }

        /// <summary>
        /// The turret: its floor is opened under the hollow (it sits over the hull, nothing between), and the
        /// commander sits in it, in the middle, on that open floor.
        /// </summary>
        private float3 _commanderSeat;
        private bool _hasCommander;

        private void SeatCommander(Transform turret, MeshVoxelizer.Result grid, NativeArray<bool> hollow)
        {
            if (_hasCommander)
                SpawnCrewman(turret, grid, hollow, _commanderSeat, null);
        }

        /// <summary>Opens the turret's floor under its hollow (before its body is made) and finds the commander's seat.</summary>
        private void OpenTurretFloor(MeshVoxelizer.Result grid, NativeArray<bool> hollow)
        {
            _hasCommander = false;
            int3 s = grid.Size;
            int3 lo = new int3(int.MaxValue);
            int3 hi = new int3(int.MinValue);
            for (int z = 0; z < s.z; z++)
            for (int x = 0; x < s.x; x++)
            {
                int bottom = -1;
                for (int y = 0; y < s.y && bottom < 0; y++)
                    if (hollow[x + s.x * (y + s.y * z)])
                        bottom = y;
                if (bottom < 0)
                    continue;
                // Everything under the column's hollow goes: the turret has no floor.
                for (int y = 0; y < bottom; y++)
                {
                    int i = x + s.x * (y + s.y * z);
                    grid.Voxels[i] = 0;
                    hollow[i] = true;
                }
                lo = math.min(lo, new int3(x, 0, z));
                hi = math.max(hi, new int3(x, bottom, z));
            }

            if (lo.x > hi.x)
                return;
            _commanderSeat = grid.Origin + new float3((lo.x + hi.x) * 0.5f, 0f, (lo.z + hi.z) * 0.5f) * _cell;
            _hasCommander = true;
        }

        /// <summary>Whether a point in the hull's local space lies in the hollow.</summary>
        private bool InHollow(MeshVoxelizer.Result grid, NativeArray<bool> hollow, float3 local)
        {
            int3 s = grid.Size;
            var g = (int3)math.floor((local - grid.Origin) / _cell);
            return math.all(g >= 0) && math.all(g < s) && hollow[g.x + s.x * (g.y + s.y * g.z)];
        }

        private static NativeArray<Color32> Palette()
        {
            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            for (int i = 0; i < Colours.Length; i++)
                palette[i] = Colours[i];
            return palette;
        }

        /// <summary>A seated crewman, in khaki with a helmet, cut to the hollow: his own body, so a hit pops him.</summary>
        private void SpawnCrewman(Transform hull, MeshVoxelizer.Result grid, NativeArray<bool> hollow, float3 seat, VoxelBody inside)
        {
            var size = new int3(8, 16, 12);
            float3 at = seat - new float3(size.x / 2, 0, 3) * VOXEL;
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            int solid = 0;
            void Box(int3 a, int3 b, byte m)
            {
                for (int z = a.z; z <= b.z; z++)
                for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                {
                    if (!InHollow(grid, hollow, at + (new float3(x, y, z) + 0.5f) * VOXEL))
                        continue;
                    voxels[x + size.x * (y + size.y * z)] = m;
                    solid++;
                }
            }

            Box(new int3(1, 0, 3), new int3(6, 2, 10), 2); // thighs, forward
            Box(new int3(1, 0, 10), new int3(6, 1, 11), 5); // boots
            Box(new int3(1, 3, 2), new int3(6, 9, 5), 1); // torso
            Box(new int3(0, 6, 3), new int3(0, 9, 7), 1); // arms
            Box(new int3(7, 6, 3), new int3(7, 9, 7), 1);
            Box(new int3(2, 10, 2), new int3(5, 13, 5), 3); // head
            Box(new int3(2, 12, 6), new int3(2, 12, 6), 13); // eyes
            Box(new int3(5, 12, 6), new int3(5, 12, 6), 13);
            Box(new int3(1, 14, 1), new int3(6, 15, 6), 4); // helmet
            if (solid < 60)
            {
                voxels.Dispose();
                return;
            }

            VoxelBody body = Spawn(hull, "Crewman", size, voxels, Palette(), at);
            body.gameObject.AddComponent<TankDummyCrew>().Watch(body, inside);
        }

        private VoxelBody Spawn(Transform parent, string name, int3 size, NativeArray<byte> voxels, NativeArray<Color32> palette,
            float3 origin)
        {
            var go = new GameObject(name) { layer = gameObject.layer };
            go.transform.SetParent(parent, false);
            VoxelBodySettings settings = VoxelBodySettings.Default(VOXEL);
            settings.Origin = origin;
            settings.Anchor = VoxelAnchor.Largest;
            settings.Colliders = true;
            settings.Layer = gameObject.layer;
            settings.Density = 1000f;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }


        // ---------- Placing in the sandbox ----------

        private const string RESOURCE = "TankDummy_01";

        /// <summary>
        /// Two target tanks ahead of the player's tank whenever the sandbox loads (VoxelSandboxStart in
        /// the scene): spawned from code, so the sandbox scene itself is left as it is.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            SpawnInSandbox();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) =>
            SpawnInSandbox();

        private static void SpawnInSandbox()
        {
            if (!SANDBOX_TARGETS)
                return;
            var start = FindFirstObjectByType<VoxelSandboxStart>();
            if (start == null || FindFirstObjectByType<TankDummy>() != null)
                return;
            var prefab = Resources.Load<GameObject>(RESOURCE);
            if (prefab == null)
                return;

            Transform player = start.Vehicle != null ? start.Vehicle.transform : start.transform;
            Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            // The target's root sits where the player's tank root sits over its ground: same model, same pivot. The
            // ground under each is looked up past the tanks (their layer is not in the mask); without ground yet
            // (the voxel terrain is built after load) the player's height is kept.
            const int GROUND = (1 << 0) | (1 << 3) | (1 << 10) | (1 << 14) | (1 << 16);
            float playerGround = Physics.Raycast(player.position + Vector3.up * 5f, Vector3.down, out RaycastHit under,
                50f, GROUND, QueryTriggerInteraction.Ignore) ? under.point.y : float.NaN;
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = player.position + forward * (18f + 6f * i) + right * (i == 0 ? -5f : 5f);
                at.y = player.position.y;
                if (!float.IsNaN(playerGround) && Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit hit,
                        60f, GROUND, QueryTriggerInteraction.Ignore))
                    at.y += hit.point.y - playerGround;
                // Broadside on to the player.
                Instantiate(prefab, at, Quaternion.LookRotation(right), null).name = RESOURCE;
            }
        }
    }
}

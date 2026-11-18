using System.Collections.Generic;
using Game.Voxels;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A standing voxel target tank (prototype): Tank_01's look, voxelized as Tank_01's skin is, but
    /// with colliders and hollowed out, and a crew, seats, levers, a panel, the engine and an ammo rack
    /// inside in their own colours. No control, no AI, no installers: only the hull's meshes are taken.
    /// Shells and machine-gun bullets break the armour (VoxelDamage, the bullet's wall piercing); a
    /// crewman hit pops in blood (TankDummyCrew).
    /// </summary>
    public sealed class TankDummy : MonoBehaviour
    {
        [Tooltip("The tank whose look and voxel skin settings are taken (its HullVisual and VoxelizedModel).")]
        [SerializeField] private GameObject _tank;

        [Tooltip("Armour left round the inside, m.")]
        [SerializeField] private float _armour = 0.15f;

        [SerializeField] private int _crew = 4;

        private const float VOXEL = 0.05f;
        private const string HULL = "HullVisual";

        private void Start() => Build();

        private void Build()
        {
            if (_tank == null || transform.Find(HULL) != null)
                return;

            Transform source = _tank.transform.Find(HULL);
            var template = _tank.GetComponent<VoxelizedModel>();
            if (source == null || template == null)
                return;

            // Under a switched-off holder: the copy's behaviours must not wake (no second tank).
            var holder = new GameObject("Holder");
            holder.SetActive(false);
            GameObject hull = Instantiate(source.gameObject, holder.transform, false);
            hull.name = HULL;
            Strip(hull);
            hull.transform.SetParent(transform, false);
            Destroy(holder);
            SetLayer(hull.transform, gameObject.layer);

            // Tank_01's voxel skin settings, on a throwaway VoxelizedModel that only voxelizes.
            var model = gameObject.AddComponent<VoxelizedModel>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(template), model);
            List<VoxelizedModel.Part> parts = model.Voxelize();
            DestroyImmediate(model);

            foreach (VoxelizedModel.Part part in parts)
            {
                MeshVoxelizer.Result grid = part.Grid;
                bool hullPart = part.Root.name == HULL;
                bool turret = part.Root.name == "TurretVisual";
                if (hullPart || turret)
                    Hollow(grid, turret ? 0.6f : 0.3f);

                var go = new GameObject("Voxels") { layer = gameObject.layer };
                go.transform.SetParent(part.Root, false);
                VoxelBodySettings settings = VoxelBodySettings.Default(VOXEL);
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

                if (hullPart)
                    Furnish(part.Root, grid);
            }
        }

        /// <summary>The hull's meshes only: every behaviour, collider and body of the copy goes.</summary>
        private static void Strip(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component is MonoBehaviour or Collider or Rigidbody or Joint or AudioSource or ParticleSystem or Light)
                    DestroyImmediate(component);
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t)
                SetLayer(child, layer);
        }

        /// <summary>The cabin: the grid's inside, all but the armour, and the hull's bottom share <paramref name="floor"/> of its height.</summary>
        private int3 _cabinLo;
        private int3 _cabinHi;

        /// <summary>Empties the inside of a grid, leaving its armour, from <paramref name="floor"/> of its height up.</summary>
        private void Hollow(MeshVoxelizer.Result grid, float floor)
        {
            int3 s = grid.Size;
            int wall = math.max(2, Mathf.RoundToInt(_armour / VOXEL));
            int3 lo = new int3((int)(s.x * 0.22f), math.max(wall, (int)(s.y * floor * 0.5f)), wall + 2);
            int3 hi = new int3(s.x - 1 - (int)(s.x * 0.22f), s.y - 1 - wall, s.z - 1 - wall - 2);
            _cabinLo = lo;
            _cabinHi = hi;
            for (int z = lo.z; z <= hi.z; z++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
                grid.Voxels[x + s.x * (y + s.y * z)] = 0;
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

        /// <summary>Seats, levers and the panel in front, the engine in the stern, shells along a side, and the crew.</summary>
        private void Furnish(Transform hull, MeshVoxelizer.Result grid)
        {
            int3 lo = _cabinLo;
            int3 hi = _cabinHi;
            int3 size = hi - lo + 1;
            if (math.any(size < new int3(12, 10, 24)))
                return;

            float3 origin = grid.Origin + (float3)lo * VOXEL;
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            void Box(int3 a, int3 b, byte m)
            {
                a = math.clamp(a, 0, size - 1);
                b = math.clamp(b, 0, size - 1);
                for (int z = a.z; z <= b.z; z++)
                for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                    voxels[x + size.x * (y + size.y * z)] = m;
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

            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            for (int i = 0; i < Colours.Length; i++)
                palette[i] = Colours[i];
            VoxelBody inside = Spawn(hull, "Inside", size, voxels, palette, origin);

            for (int i = 0; i < seats.Count && i < _crew; i++)
                SpawnCrewman(hull, origin + ((float3)seats[i] + new float3(0f, 4f, 0f)) * VOXEL, inside);
        }

        /// <summary>A seated crewman, in khaki with a helmet: his own body, so a hit pops him (TankDummyCrew).</summary>
        private void SpawnCrewman(Transform hull, float3 seat, VoxelBody inside)
        {
            var size = new int3(8, 16, 12);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            void Box(int3 a, int3 b, byte m)
            {
                for (int z = a.z; z <= b.z; z++)
                for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                    voxels[x + size.x * (y + size.y * z)] = m;
            }

            Box(new int3(1, 0, 3), new int3(6, 2, 10), 2); // thighs, forward
            Box(new int3(1, 0, 10), new int3(6, 1, 11), 5); // boots
            Box(new int3(1, 3, 2), new int3(6, 9, 5), 1); // torso
            Box(new int3(0, 6, 3), new int3(0, 9, 7), 1); // arms
            Box(new int3(7, 6, 3), new int3(7, 9, 7), 1);
            Box(new int3(2, 10, 2), new int3(5, 13, 5), 3); // head
            Box(new int3(2, 12, 6 - 1), new int3(2, 12, 6 - 1), 13); // eyes
            Box(new int3(5, 12, 6 - 1), new int3(5, 12, 6 - 1), 13);
            Box(new int3(1, 14, 1), new int3(6, 15, 6), 4); // helmet

            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            for (int i = 0; i < Colours.Length; i++)
                palette[i] = Colours[i];
            float3 origin = seat - new float3(size.x * 0.5f, 0f, size.z * 0.3f) * VOXEL;
            VoxelBody body = Spawn(hull, "Crewman", size, voxels, palette, origin);
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

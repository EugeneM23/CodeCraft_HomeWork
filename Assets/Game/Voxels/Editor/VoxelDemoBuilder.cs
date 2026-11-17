using System;
using System.Collections.Generic;
using Game.Voxels.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Voxels.EditorTools
{
    /// <summary>
    /// Builds VoxelSandbox from a copy of Playground: the game's own systems stay — the contexts,
    /// the camera rig, the player's character and the real Tank_01 with its drive, suspension,
    /// camera, smoke and shot — and the arena is replaced by voxels: thick mud ground (the colours
    /// of the main project's MT_MudGround) and a small town, with wooden ramps to jump the tank
    /// into its houses from. The player starts in the tank.
    /// Running it again rebuilds the scene from scratch.
    /// </summary>
    public static class VoxelDemoBuilder
    {
        private const string LOG = "[VoxelDemo]";
        private const string RESOURCES = "Assets/Game/Voxels/Resources/Voxels";
        private const string SHADER = "Game/Voxel";
        private const string PLAYGROUND = "Assets/Game/Scenes/Playground.unity";
        private const string SCENE_FOLDER = "Assets/Game/Voxels/Scenes";
        public const string SANDBOX = SCENE_FOLDER + "/VoxelSandbox.unity";

        private const int LAYER_GROUND = 3;

        /// <summary>
        /// The ground: one body of 800 × 80 × 800 voxels (80 × 8 × 80 m), its bottom at
        /// <see cref="GROUND_BOTTOM"/>. The hills (VoxelTerrain) run from y = 0 to 6 m, so even the
        /// lowest valley has 1.2 m of ground under it. One body, not tiles: a smooth surface over
        /// tiles would crease along every seam.
        /// </summary>
        private const int TILES = 8;
        private const float TILE = 10f;
        private const int GROUND_VOXELS = 80;
        private const float GROUND_BOTTOM = -1.2f;

        /// <summary>Hills (true) or flat ground (false); soils, mud fields and trees either way.</summary>
        private static readonly bool HILLS = false;

        /// <summary>Plant the tree groves (off for now; to come back to).</summary>
        private static readonly bool TREES = false;

        /// <summary>Edge (m) of a tree's voxels.</summary>
        private const float TREE_VOXEL = 0.14f;

        /// <summary>What Playground keeps: the game's systems and the tank. The rest is the old arena.</summary>
        private static readonly HashSet<string> Kept = new() { "[CameraRoot]", "[Character]", "[Context]", "Tank_01", "RayFireMan" };

        /// <summary>Where the tank and the character start (x, z); their height comes from the terrain.</summary>
        private static readonly Vector3 TankStart = new(-34f, 0f, 0f);
        private const float TANK_YAW = 90f;
        private static readonly Vector3 CharacterStart = new(-34f, 0f, -4f);

        private static TerrainShape s_shape;
        private static Vector4[] s_pads;

        private struct Placement
        {
            public string Name;
            public VoxelStructure.Shape Shape;
            public Vector3 Position;
            public float Yaw;
            public Vector3Int Size;
            public int Seed;
        }

        /// <summary>The village's roads: centre lines; the road as wide as a tank, a kerb and a frayed verge either side.</summary>
        private static Vector4[] Streets => new Vector4[]
        {
            // The road through the village, east–west.
            new(-40f, 0f, 40f, 0f),
            // A lane north from the crossroads, with a dog-leg.
            new(0f, 0f, 3f, 18f),
            new(3f, 18f, -2f, 40f),
        };

        /// <summary>From a road's centre line to the middle of a house fronting it (m).</summary>
        private const float FRONTAGE = 1.6f + 0.25f + 1.2f + 0.4f + 2.7f;

        /// <summary>
        /// A small village: a cluster of houses at the crossroads either side of the road, one on
        /// each side of the lane, and a few standing on their own further out — mostly one or two
        /// storeys, one of three — and the two old brick towers behind. Sizes in 10 cm voxels.
        /// </summary>
        // A property, not a static field: the editor reloads changed methods in place, without
        // re-running static initialisers, and a field would keep the old plan.
        private static Placement[] Town => BuildTownPlan();

        private static Placement[] BuildTownPlan()
        {
            var plan = new List<Placement>
            {
                new() { Name = "Tower_A", Shape = VoxelStructure.Shape.Tower, Position = new Vector3(-22f, 0f, 20f), Size = new Vector3Int(28, 90, 28) },
                new() { Name = "Tower_B", Shape = VoxelStructure.Shape.Tower, Position = new Vector3(22f, 0f, 24f), Size = new Vector3Int(24, 70, 24) },
            };

            // (x, z, yaw, floors, seed). Yaw 0 fronts south, 180 north, 90 west, -90 east.
            (float x, float z, float yaw, int floors, int seed)[] houses =
            {
                // Near the crossroads, each on its own plot: two north of the road, two south, apart.
                (-10f, FRONTAGE + 1f, 2f, 2, 12),
                (-24f, FRONTAGE + 3.5f, -4f, 1, 5),
                (-6f, -FRONTAGE - 1.5f, 178f, 1, 22),
                (12f, -FRONTAGE - 2.5f, 184f, 3, 16),
                // Along the lane, one each side, well up it.
                (10f, 12f, 92f, 2, 25),
                (-8f, 30f, -86f, 1, 31),
                // Out on their own: farmsteads off the road.
                (26f, FRONTAGE + 6f, -9f, 1, 18),
                (-30f, -FRONTAGE - 9f, 190f, 2, 27),
                (31f, -24f, 206f, 1, 33),
                (14f, 33f, 76f, 1, 9),
            };

            for (int i = 0; i < houses.Length; i++)
            {
                var (x, z, yaw, floors, seed) = houses[i];
                plan.Add(new Placement
                {
                    Name = $"House_{i:00}",
                    Shape = VoxelStructure.Shape.Victorian,
                    Position = new Vector3(x, 0f, z),
                    Yaw = yaw,
                    Size = new Vector3Int(64, 50 + floors * 28, 54),
                    Seed = seed
                });
            }

            return plan.ToArray();
        }

        /// <summary>
        /// Jump ramps for the tank to fly into houses from: the house, the side of it the tank flies
        /// at (its outward direction in the house's own axes; the front is -Z) and the gap (m) from
        /// the ramp's lip to that wall. Each has a clear run-up over grass behind it. The gap sets
        /// where a boosted tank (9 m/s on grass) strikes: about 3 m up from a 5 m gap — the first
        /// floor of a tall house — and about 2 m up from 7.5–9 m, the windows of a one-storey one.
        /// Without Shift the tank lands short and drives into the wall.
        /// </summary>
        private static (string house, Vector2 side, float gap)[] RampPlan => new (string, Vector2, float)[]
        {
            // From the south, the run-up on the grass short of the Regaxes, the lip on the road's
            // kerb: the tank flies across the road into the front of a one-storey house.
            ("House_01", new Vector2(0f, -1f), 9f),
            // From the east into the side of the three-storey house, at its first floor.
            ("House_03", new Vector2(-1f, 0f), 5f),
            // From the west into the back of a one-storey house up the north-west.
            ("House_05", new Vector2(0f, 1f), 7.5f),
            // From the south into the back of the two-storey house by the start.
            ("House_07", new Vector2(0f, 1f), 5.5f),
        };

        /// <summary>
        /// A ramp is a plain box 3.2 m wide (the tank is 2), tilted up 20° — under the 30° past
        /// which the suspension starts righting the hull — to a lip 1.8 m above the ground.
        /// </summary>
        private const float RAMP_WIDTH = 3.2f;
        private const float RAMP_HEIGHT = 1.8f;
        private const float RAMP_ANGLE = 20f;

        /// <summary>Ground (m) a ramp covers from its foot to its lip.</summary>
        private static float RampRun => RAMP_HEIGHT / Mathf.Tan(RAMP_ANGLE * Mathf.Deg2Rad);

        private struct Ramp
        {
            public string House;
            public Vector3 Foot;
            public Quaternion Rotation;
        }

        /// <summary>Where the ramps of <see cref="RampPlan"/> stand: foot on the ground, lip toward the house.</summary>
        private static List<Ramp> Ramps
        {
            get
            {
                var ramps = new List<Ramp>();
                Placement[] town = Town;
                foreach (var (house, side, gap) in RampPlan)
                {
                    int i = Array.FindIndex(town, p => p.Name == house);
                    if (i < 0)
                    {
                        Debug.LogWarning($"{LOG} No {house} in the town: its ramp is left out.");
                        continue;
                    }

                    Placement placement = town[i];
                    Vector3 outward = Quaternion.Euler(0f, placement.Yaw, 0f) * new Vector3(side.x, 0f, side.y).normalized;
                    float half = Mathf.Abs(side.x) > Mathf.Abs(side.y) ? placement.Size.x * 0.05f : placement.Size.z * 0.05f;
                    Vector3 lip = placement.Position + outward * (half + gap);
                    Vector3 foot = lip + outward * RampRun;
                    foot.y = GroundAt(foot);
                    ramps.Add(new Ramp { House = house, Foot = foot, Rotation = Quaternion.LookRotation(-outward) });
                }

                return ramps;
            }
        }

        /// <summary>A point on or within <paramref name="margin"/> m of a ramp's footprint.</summary>
        private static bool OnRamp(Vector3 at, Ramp ramp, float margin)
        {
            Vector3 local = Quaternion.Inverse(ramp.Rotation) * (at - ramp.Foot);
            return Mathf.Abs(local.x) < RAMP_WIDTH * 0.5f + margin && local.z > -margin && local.z < RampRun + margin;
        }

        [MenuItem("Tools/Voxels/Build Sandbox Scene")]
        public static void BuildSandbox()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} Leave Play Mode first.");
                return;
            }

            EnsureMaterials();
            if (!AssetDatabase.IsValidFolder(SCENE_FOLDER))
                AssetDatabase.CreateFolder("Assets/Game/Voxels", "Scenes");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SANDBOX) != null)
                AssetDatabase.DeleteAsset(SANDBOX);
            if (!AssetDatabase.CopyAsset(PLAYGROUND, SANDBOX))
            {
                Debug.LogError($"{LOG} Could not copy {PLAYGROUND}.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SANDBOX, OpenSceneMode.Single);
            int removed = ClearArena(scene);

            PlanTerrain();
            BuildGround();
            BuildTown();
            BuildRamps(scene);
            if (TREES)
                BuildTrees();
            GameObject tank = PlaceTank(scene);
            PlaceCharacter(scene);
            AddStart(tank);
            PlaceRegaxes(scene);
            new GameObject("<Hud>").AddComponent<VoxelDemoHud>();
            BuildWeather(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings(SANDBOX);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LOG} {SANDBOX} built from {PLAYGROUND}: {removed} arena objects removed, {TILES * TILES} ground tiles, {Town.Length} buildings.");
        }

        /// <summary>Smoke on the map: the spots and the B key (off for now; to come back to).</summary>
        private static readonly bool SMOKE = false;

        /// <summary>Rain on the map and the N key (off for now; to come back to).</summary>
        private static readonly bool RAIN = false;

        /// <summary>Smoke spots (x, z, delay before the first cloud): on the road east of the tank, at the crossroads, up the lane.</summary>
        private static readonly (float x, float z, float delay)[] SmokeSpots =
        {
            (-20f, 0.5f, 0f),
            (1f, 1f, 5f),
            (2.5f, 14f, 10f),
        };

        /// <summary>Puts the rain and the smoke spots into VoxelSandbox as it is, without rebuilding it.</summary>
        [MenuItem("Tools/Voxels/Add Weather To Sandbox")]
        public static void AddWeatherToSandbox()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} Leave Play Mode first.");
                return;
            }

            EnsureMaterials();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SANDBOX)
                scene = EditorSceneManager.OpenScene(SANDBOX, OpenSceneMode.Single);

            PlanTerrain();
            BuildWeather(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// &lt;Weather&gt;: voxel rain round the camera, the sandbox keys for it (B — smoke at the
        /// cursor, N — rain on / off) and the smoke spots, each setting a cloud off again and again.
        /// </summary>
        private static void BuildWeather(Scene scene)
        {
            GameObject old = Find(scene, "<Weather>");
            if (old != null)
                Object.DestroyImmediate(old);

            // Nothing on: no <Weather> at all, and no keys for it.
            if (!RAIN && !SMOKE)
            {
                Debug.Log($"{LOG} <Weather>: rain and smoke are off, none placed.");
                return;
            }

            var weather = new GameObject("<Weather>");
            VoxelRain rain = null;
            if (RAIN)
            {
                rain = new GameObject("Rain").AddComponent<VoxelRain>();
                rain.transform.SetParent(weather.transform, false);
            }

            var keys = weather.AddComponent<VoxelWeatherDemo>();
            var serialized = new SerializedObject(keys);
            serialized.FindProperty("_rain").objectReferenceValue = rain;
            serialized.FindProperty("_smokeKey").boolValue = SMOKE;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; SMOKE && i < SmokeSpots.Length; i++)
            {
                var (x, z, delay) = SmokeSpots[i];
                var at = new Vector3(x, 0f, z);
                at.y = GroundAt(at) + 0.1f;
                var spot = new GameObject($"SmokeSpot_{i}");
                spot.transform.SetParent(weather.transform, false);
                spot.transform.position = at;
                spot.AddComponent<VoxelSmokeSource>().Set(3.2f, 20f, 3f, delay);
            }

            Debug.Log($"{LOG} <Weather>: rain {(RAIN ? "on" : "off")}, {(SMOKE ? SmokeSpots.Length : 0)} smoke spots.");
        }

        /// <summary>Takes the voxel street back out of Playground: the voxels have a scene of their own.</summary>
        [MenuItem("Tools/Voxels/Remove Street From Playground")]
        public static void RemoveStreetFromPlayground()
        {
            Scene scene = EditorSceneManager.OpenScene(PLAYGROUND, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "<Voxels>")
                    continue;

                Object.DestroyImmediate(root);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"{LOG} <Voxels> removed from {PLAYGROUND}.");
                return;
            }
        }

        /// <summary>Everything but the game's systems, the tank and the light goes.</summary>
        private static int ClearArena(Scene scene)
        {
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (Kept.Contains(root.name) || root.GetComponent<Light>() != null)
                    continue;

                Object.DestroyImmediate(root);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// The hills, and a flat pad under every building and where the tank and the character
        /// start, each at the height the hills have there, so nothing floats or sinks.
        /// </summary>
        private static void PlanTerrain()
        {
            s_shape = TerrainShape.Default;
            if (!HILLS)
                s_shape.Amplitude = 0f;
            var pads = new List<Vector4>();

            foreach (Placement placement in Town)
            {
                float halfX = placement.Size.x * 0.05f;
                float halfZ = placement.Size.z * 0.05f;
                float radius = Mathf.Sqrt(halfX * halfX + halfZ * halfZ) + 0.5f;
                pads.Add(Pad(placement.Position, radius));
            }

            pads.Add(Pad(TankStart, 4f));
            pads.Add(Pad(CharacterStart, 1.5f));
            s_pads = pads.ToArray();
        }

        private static Vector4 Pad(Vector3 at, float radius) =>
            new(at.x, at.z, radius, VoxelTerrain.RawHeight(new Vector2(at.x, at.z), s_shape));

        private static float GroundAt(Vector3 at) => VoxelTerrain.Height(new Vector2(at.x, at.z), s_shape, s_pads);

        private static void BuildGround()
        {
            var root = new GameObject("<Ground>") { layer = LAYER_GROUND };
            root.AddComponent<VoxelTerrainSettings>().Set(s_shape, s_pads, Streets);
            float half = TILES * TILE * 0.5f;

            // On the Ground layer, like the floor in the game: the character stands on it.
            var ground = new GameObject("Terrain") { layer = LAYER_GROUND };
            ground.transform.SetParent(root.transform, false);
            ground.transform.position = new Vector3(0f, GROUND_BOTTOM, 0f);
            int side = Mathf.RoundToInt(half * 2f / 0.1f);
            Configure(ground.AddComponent<VoxelStructure>(), VoxelStructure.Shape.Ground,
                new Vector3Int(side, GROUND_VOXELS, side), 7);

            // Below the tiles, so whatever drops through a crater to the bottom lands somewhere.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Bedrock";
            floor.layer = LAYER_GROUND;
            floor.transform.SetParent(root.transform, false);
            floor.transform.position = new Vector3(0f, GROUND_BOTTOM - 0.5f, 0f);
            floor.transform.localScale = new Vector3(TILES * TILE + 2f, 1f, TILES * TILE + 2f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = BedrockMaterial();
        }

        private static void BuildTown()
        {
            var root = new GameObject("<Town>");
            foreach (Placement placement in Town)
            {
                var go = new GameObject(placement.Name);
                go.transform.SetParent(root.transform, false);
                Vector3 at = placement.Position;
                at.y = GroundAt(at);
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, placement.Yaw, 0f));
                Configure(go.AddComponent<VoxelStructure>(), placement.Shape, placement.Size,
                    placement.Seed != 0 ? placement.Seed : placement.Name.GetHashCode() & 0xFFFF);
            }
        }

        /// <summary>
        /// Groves of trees and a few lone ones round the town: leafy trees and pines of different
        /// heights, kept off the buildings, the start and the map's edge. Always the same layout.
        /// </summary>
        private static void BuildTrees()
        {
            var root = new GameObject("<Trees>");
            var random = new System.Random(1234);
            var placed = new List<Vector3>();
            float edge = TILES * TILE * 0.5f - 2.5f;

            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

            bool Free(Vector3 at)
            {
                if (Mathf.Abs(at.x) > edge || Mathf.Abs(at.z) > edge)
                    return false;
                if (Vector3.Distance(Flat(at), Flat(TankStart)) < 8f || Vector3.Distance(Flat(at), Flat(CharacterStart)) < 4f)
                    return false;
                foreach (Placement building in Town)
                {
                    float halfX = building.Size.x * 0.05f;
                    float halfZ = building.Size.z * 0.05f;
                    if (Vector3.Distance(Flat(at), Flat(building.Position)) < Mathf.Sqrt(halfX * halfX + halfZ * halfZ) + 2.5f)
                        return false;
                }

                foreach (Vector3 other in placed)
                    if (Vector3.Distance(Flat(at), Flat(other)) < 3f)
                        return false;
                return true;
            }

            void Plant(Vector3 at)
            {
                at.y = GroundAt(at);
                int seed = random.Next(1, 100000);
                bool pine = (seed & 1) == 1;
                // Trees are built of 14 cm voxels, not 10: the same size of tree in a third of the
                // voxels — half the triangles, and a cheaper break when one comes down.
                int width = Mathf.RoundToInt((pine ? random.Next(26, 32) : random.Next(34, 42)) * 0.1f / TREE_VOXEL);
                int height = Mathf.RoundToInt((pine ? random.Next(80, 104) : random.Next(58, 80)) * 0.1f / TREE_VOXEL);

                var go = new GameObject(pine ? $"Pine_{placed.Count}" : $"Tree_{placed.Count}");
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                VoxelStructure structure = go.AddComponent<VoxelStructure>();
                Configure(structure, VoxelStructure.Shape.Tree, new Vector3Int(width, height, width), seed);
                var serialized = new SerializedObject(structure);
                serialized.FindProperty("_voxelSize").floatValue = TREE_VOXEL;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                placed.Add(at);
            }

            // Groves: a handful of trees round a few spots.
            for (int grove = 0; grove < 7; grove++)
            {
                var centre = new Vector3(Range(-edge, edge), 0f, Range(-edge, edge));
                int count = random.Next(4, 9);
                for (int i = 0, tries = 0; i < count && tries < 40; tries++)
                {
                    Vector3 at = centre + new Vector3(Range(-6f, 6f), 0f, Range(-6f, 6f));
                    if (!Free(at))
                        continue;
                    Plant(at);
                    i++;
                }
            }

            // And some standing on their own.
            for (int i = 0, tries = 0; i < 12 && tries < 200; tries++)
            {
                var at = new Vector3(Range(-edge, edge), 0f, Range(-edge, edge));
                if (!Free(at))
                    continue;
                Plant(at);
                i++;
            }

            Debug.Log($"{LOG} {placed.Count} trees planted.");
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>The real Tank_01 from Playground, at the south edge of town, with voxel ruts.</summary>
        private static GameObject PlaceTank(Scene scene)
        {
            GameObject tank = Find(scene, "Tank_01");
            Vector3 start = TankStart;
            start.y = GroundAt(start) + 0.05f;
            tank.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, TANK_YAW, 0f));
            if (!tank.TryGetComponent(out VoxelTrackRuts _))
                tank.AddComponent<VoxelTrackRuts>();
            // No aim pitch: the shells fly straight out of the barrel, not at the crosshair.
            return tank;
        }

        private static void PlaceCharacter(Scene scene)
        {
            GameObject character = Find(scene, "[Character]");
            if (character == null)
                return;

            // The game works out the camera's offset once, from where the rig and the character
            // stand at the start: the rig moves with the character, or the camera would hang
            // metres off to one side looking at nothing.
            Vector3 start = CharacterStart;
            start.y = GroundAt(start) + 0.05f;
            Vector3 shift = start - character.transform.position;
            character.transform.SetPositionAndRotation(start, Quaternion.identity);

            GameObject rig = Find(scene, "[CameraRoot]");
            if (rig != null)
                rig.transform.position += shift;
        }

        /// <summary>
        /// VoxelSandboxStart lives in the game's assembly (it uses the game's contexts), which this
        /// editor assembly does not reference: it is added by name.
        /// </summary>
        private static void AddStart(GameObject tank)
        {
            Type type = Type.GetType("Game.VoxelSandboxStart, Game");
            if (type == null)
            {
                Debug.LogWarning($"{LOG} Game.VoxelSandboxStart not found: the player starts on foot.");
                return;
            }

            var go = new GameObject("<SandboxStart>");
            Component start = go.AddComponent(type);
            var serialized = new SerializedObject(start);
            serialized.FindProperty("_vehicle").objectReferenceValue = tank.GetComponent("MonoEntity");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Mouse look: the mouse swings the game camera round the tank, a crosshair aims.
            Type orbit = Type.GetType("Game.VoxelSandboxOrbit, Game");
            if (orbit != null)
                go.AddComponent(orbit);

            AddAirStrike(go, tank);
        }

        /// <summary>The tank shell's explosion effect: the air bomb's fireball is a cluster of them.</summary>
        private const string EXPLOSION_VFX_GUID = "b4c963e707aa04a49a4aee66432d1356";

        /// <summary>An air bomb every 5 s round the tank (Game.VoxelAirStrike, added by name too).</summary>
        [MenuItem("Tools/Voxels/Add Air Strike To Sandbox")]
        private static void AddAirStrikeToOpenScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            GameObject start = Find(scene, "<SandboxStart>");
            Component starter = start != null ? start.GetComponent("VoxelSandboxStart") : null;
            Object vehicle = starter != null ? new SerializedObject(starter).FindProperty("_vehicle").objectReferenceValue : null;
            if (start == null || vehicle is not Component tank)
            {
                Debug.LogWarning($"{LOG} No <SandboxStart> with a tank in the open scene.");
                return;
            }

            AddAirStrike(start, tank.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void AddAirStrike(GameObject host, GameObject tank)
        {
            Type type = Type.GetType("Game.VoxelAirStrike, Game");
            if (type == null)
            {
                Debug.LogWarning($"{LOG} Game.VoxelAirStrike not found: no air strikes.");
                return;
            }

            Component strike = host.GetComponent(type) ?? host.AddComponent(type);
            var serialized = new SerializedObject(strike);
            serialized.FindProperty("_target").objectReferenceValue = tank.transform;
            var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(EXPLOSION_VFX_GUID));
            serialized.FindProperty("_explosionVfx").objectReferenceValue = vfx != null ? vfx.GetComponent("MonoEntity") : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private const string REGAX = "Assets/Game/Prefabs/Characters/Regax.prefab";

        /// <summary>Regaxes in the village, to be shot, shelled and run down into voxel flesh.</summary>
        private const int REGAXES = 20;

        /// <summary>Metres between neighbours in the row: about a Regax's width, so the tank meets one after another.</summary>
        private const float REGAX_SPACING = 2.4f;

        /// <summary>Puts the Regaxes into VoxelSandbox as it is, without rebuilding it.</summary>
        [MenuItem("Tools/Voxels/Add Regaxes To Sandbox")]
        public static void AddRegaxesToSandbox()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} Leave Play Mode first.");
                return;
            }

            // The scene is saved at the end: unsaved edits someone else made must not go with it.
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                Debug.LogError($"{LOG} {scene.path} has unsaved changes: save or discard them first.");
                return;
            }

            if (scene.path != SANDBOX)
                scene = EditorSceneManager.OpenScene(SANDBOX, OpenSceneMode.Single);

            PlanTerrain();
            PlaceRegaxes(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// <see cref="REGAXES"/> instances of Regax.prefab (it dies into voxel flesh) standing in one
        /// straight row on the mud, <see cref="REGAX_SPACING"/> apart, for the tank to run down: of
        /// every row that starts on mud (2 m grid, 15° steps) and keeps off the buildings, the
        /// streets' edge and the starts, the one with the most of its spots on mud — on a tie, the
        /// one nearer the tank. They all face the tank's start, stand still, and stand at the scene's
        /// root, as on Playground. Always the same layout; earlier ones are replaced.
        /// </summary>
        private static void PlaceRegaxes(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(REGAX);
            if (prefab == null)
            {
                Debug.LogWarning($"{LOG} No {REGAX}: no Regaxes placed.");
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("Regax_") && PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab)
                    Object.DestroyImmediate(root);

            // The ground's own answer (streets paved over), once BuildGround has put it in the scene.
            VoxelTerrainSettings terrain = Object.FindFirstObjectByType<VoxelTerrainSettings>();
            bool Mud(Vector3 at) => (terrain != null
                ? terrain.SoilAt(at)
                : VoxelTerrain.Soil(new Vector2(at.x, at.z), s_shape, s_pads)) == TerrainSoil.Mud;

            float edge = TILES * TILE * 0.5f - 3f;
            List<Ramp> ramps = Ramps;

            bool Free(Vector3 at)
            {
                if (Mathf.Abs(at.x) > edge || Mathf.Abs(at.z) > edge)
                    return false;
                if (Vector3.Distance(Flat(at), Flat(TankStart)) < 6f || Vector3.Distance(Flat(at), Flat(CharacterStart)) < 4f)
                    return false;
                foreach (Ramp ramp in ramps)
                    if (OnRamp(at, ramp, 1.5f))
                        return false;
                foreach (Placement building in Town)
                {
                    float halfX = building.Size.x * 0.05f;
                    float halfZ = building.Size.z * 0.05f;
                    if (Vector3.Distance(Flat(at), Flat(building.Position)) < Mathf.Sqrt(halfX * halfX + halfZ * halfZ) + 1.5f)
                        return false;
                }

                return true;
            }

            Vector3 bestStart = default;
            Vector3 bestStep = default;
            int bestMud = -1;
            float bestDistance = float.MaxValue;
            for (float x = -edge; x <= edge; x += 2f)
            for (float z = -edge; z <= edge; z += 2f)
            {
                var start = new Vector3(x, 0f, z);
                if (!Mud(start))
                    continue;

                for (int a = 0; a < 24; a++)
                {
                    float angle = a * Mathf.PI / 12f;
                    Vector3 step = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * REGAX_SPACING;
                    int mud = 0;
                    bool free = true;
                    for (int i = 0; i < REGAXES && free; i++)
                    {
                        Vector3 at = start + step * i;
                        free = Free(at);
                        if (free && Mud(at))
                            mud++;
                    }

                    if (!free)
                        continue;

                    float distance = Vector3.Distance(start + step * ((REGAXES - 1) * 0.5f), Flat(TankStart));
                    if (mud > bestMud || (mud == bestMud && distance < bestDistance))
                    {
                        bestMud = mud;
                        bestDistance = distance;
                        bestStart = start;
                        bestStep = step;
                    }
                }
            }

            if (bestMud < 0)
            {
                Debug.LogWarning($"{LOG} No room for a row of {REGAXES} Regaxes: none placed.");
                return;
            }

            for (int i = 0; i < REGAXES; i++)
            {
                Vector3 at = bestStart + bestStep * i;
                at.y = GroundAt(at) + 0.05f;
                Vector3 look = Flat(TankStart) - Flat(at);
                var regax = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                regax.name = $"Regax_{i + 1:00}";
                regax.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
                StandStill(regax);
            }

            Debug.Log($"{LOG} {REGAXES} Regaxes in a row from {bestStart} every {REGAX_SPACING} m along {bestStep.normalized}, {bestMud} on mud; standing still.");
        }

        /// <summary>
        /// The Regax stands where it is placed — no patrol, chase or attack — like the targets on
        /// Playground: the enemy installer's own switch, set as an override on this instance. The
        /// installer lives in the game's assembly, which this one does not reference: found by name.
        /// </summary>
        private static void StandStill(GameObject regax)
        {
            Component core = regax.GetComponent("EnemyCharacterCoreInstaller");
            SerializedProperty flag = core != null ? new SerializedObject(core).FindProperty("_standStill") : null;
            if (flag == null)
            {
                Debug.LogWarning($"{LOG} {regax.name}: no EnemyCharacterCoreInstaller._standStill, it will roam.");
                return;
            }

            flag.boolValue = true;
            flag.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Puts the jump ramps into VoxelSandbox as it is, without rebuilding it.</summary>
        [MenuItem("Tools/Voxels/Add Ramps To Sandbox")]
        public static void AddRampsToSandbox()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} Leave Play Mode first.");
                return;
            }

            // The scene is saved at the end: unsaved edits someone else made must not go with it.
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                Debug.LogError($"{LOG} {scene.path} has unsaved changes: save or discard them first.");
                return;
            }

            if (scene.path != SANDBOX)
                scene = EditorSceneManager.OpenScene(SANDBOX, OpenSceneMode.Single);

            EnsureMaterials();
            PlanTerrain();
            BuildRamps(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// &lt;Ramps&gt;: a box at each spot of <see cref="RampPlan"/>, tilted so its top runs from
        /// the ground at the foot up to the lip; its far end reaches down to the ground, its near
        /// one is sunk in it. On the Ground layer, so the tank's wheels and the character stand on
        /// it. Not voxels: shells and the tank do not break it. Earlier ones are replaced.
        /// </summary>
        private static void BuildRamps(Scene scene)
        {
            GameObject old = Find(scene, "<Ramps>");
            if (old != null)
                Object.DestroyImmediate(old);

            float angle = RAMP_ANGLE * Mathf.Deg2Rad;
            float slope = RAMP_HEIGHT / Mathf.Sin(angle);
            float thickness = RAMP_HEIGHT / Mathf.Cos(angle);
            Material material = RampMaterial();
            List<Ramp> ramps = Ramps;
            var root = new GameObject("<Ramps>") { layer = LAYER_GROUND };
            foreach (Ramp ramp in ramps)
            {
                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Ramp_{ramp.House}";
                box.layer = LAYER_GROUND;
                box.transform.SetParent(root.transform, false);
                Quaternion tilt = ramp.Rotation * Quaternion.Euler(-RAMP_ANGLE, 0f, 0f);
                Vector3 centre = ramp.Foot + tilt * new Vector3(0f, -thickness * 0.5f, slope * 0.5f);
                box.transform.SetPositionAndRotation(centre, tilt);
                box.transform.localScale = new Vector3(RAMP_WIDTH, thickness, slope);
                box.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            Debug.Log($"{LOG} <Ramps>: {ramps.Count} boxes, {RAMP_ANGLE}° up to {RAMP_HEIGHT} m — at {string.Join(", ", ramps.ConvertAll(r => r.House))}.");
        }

        private static Material RampMaterial()
        {
            const string path = RESOURCES + "/Ramp.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.42f, 0.34f, 0.25f) };
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name)
                    return root;
            return null;
        }

        private static void Configure(VoxelStructure structure, VoxelStructure.Shape shape, Vector3Int size, int seed)
        {
            var serialized = new SerializedObject(structure);
            serialized.FindProperty("_shape").enumValueIndex = (int)shape;
            serialized.FindProperty("_size").vector3IntValue = size;
            serialized.FindProperty("_seed").intValue = seed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material BedrockMaterial()
        {
            const string path = RESOURCES + "/Bedrock.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.12f, 0.09f, 0.06f) };
            material.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureMaterials()
        {
            Shader shader = Shader.Find(SHADER);
            if (shader == null)
            {
                Debug.LogError($"{LOG} Shader {SHADER} not found.");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Game/Voxels/Resources"))
                AssetDatabase.CreateFolder("Assets/Game/Voxels", "Resources");
            if (!AssetDatabase.IsValidFolder(RESOURCES))
                AssetDatabase.CreateFolder("Assets/Game/Voxels/Resources", "Voxels");

            CreateMaterial($"{RESOURCES}/VoxelChunk.mat", shader, false);
            CreateMaterial($"{RESOURCES}/VoxelDebris.mat", shader, true);

            // Smoke: the voxel shader, matte. Rain: its own see-through one.
            Material smoke = CreateMaterial($"{RESOURCES}/VoxelSmoke.mat", shader, true);
            smoke.SetFloat("_Glossiness", 0f);
            Shader rain = Shader.Find("Game/VoxelRain");
            if (rain != null)
                CreateMaterial($"{RESOURCES}/VoxelRain.mat", rain, true);
            else
                Debug.LogError($"{LOG} Shader Game/VoxelRain not found.");
        }

        private static Material CreateMaterial(string path, Shader shader, bool instanced)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.enableInstancing = instanced;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
                return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

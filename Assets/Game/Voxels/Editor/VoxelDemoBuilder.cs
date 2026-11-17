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
    /// Edits to VoxelSandbox as it is, made in place: the Regax row, the jump ramps, the air strike
    /// and the weather. The scene itself is the source: it is edited by hand like any other, and
    /// nothing rebuilds it. The plan of the town and the ramps below is the one the scene was laid
    /// out from; the Regaxes and the ramps are placed by it.
    /// </summary>
    public static class VoxelDemoBuilder
    {
        private const string LOG = "[VoxelDemo]";
        private const string RESOURCES = "Assets/Game/Voxels/Resources/Voxels";
        private const string SHADER = "Game/Voxel";
        private const string SCENE_FOLDER = "Assets/Game/Voxels/Scenes";
        public const string SANDBOX = SCENE_FOLDER + "/VoxelSandbox.unity";

        private const int LAYER_GROUND = 3;

        /// <summary>The ground: 8 × 8 tiles of 10 m, centred on the origin.</summary>
        private const int TILES = 8;
        private const float TILE = 10f;

        /// <summary>Where the tank starts (x, z); its height comes from the terrain.</summary>
        private static readonly Vector3 TankStart = new(-34f, 0f, 0f);

        /// <summary>
        /// A flat pad 4 m beside the tank, where the player used to start on foot. The scene's ground
        /// was laid out with it; the plan keeps it so the Regaxes and ramps land where they did.
        /// </summary>
        private static readonly Vector3 SidePad = new(-34f, 0f, -4f);

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

        /// <summary>
        /// The hills (flat in the sandbox), and a flat pad under every building, where the tank starts
        /// and beside it, each at the height the hills have there, so nothing floats or sinks.
        /// </summary>
        private static void PlanTerrain()
        {
            s_shape = TerrainShape.Default;
            // Flat ground, as the scene has it.
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
            pads.Add(Pad(SidePad, 1.5f));
            s_pads = pads.ToArray();
        }

        private static Vector4 Pad(Vector3 at, float radius) =>
            new(at.x, at.z, radius, VoxelTerrain.RawHeight(new Vector2(at.x, at.z), s_shape));

        private static float GroundAt(Vector3 at) => VoxelTerrain.Height(new Vector2(at.x, at.z), s_shape, s_pads);

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

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
        /// root. Always the same layout; earlier ones are replaced.
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
                if (Vector3.Distance(Flat(at), Flat(TankStart)) < 6f || Vector3.Distance(Flat(at), Flat(SidePad)) < 4f)
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
            }

            // A plain Regax has no AI (the elite's is AIKit, Regax_Elite): they stand like targets on a shooting range.
            Debug.Log($"{LOG} {REGAXES} Regaxes in a row from {bestStart} every {REGAX_SPACING} m along {bestStep.normalized}, {bestMud} on mud; standing still.");
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
        /// one is sunk in it. On the Ground layer, so the tank's wheels stand on
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

            material = new Material(Shader.Find("Standard")) { color = new Color(0.42f, 0.34f, 0.25f) };
            material.SetFloat("_Glossiness", 0.1f);
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
    }
}

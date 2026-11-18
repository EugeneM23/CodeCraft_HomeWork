using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Makes chapter 1 «Outskirts» for milestone 1: the five arena definitions (GDD §10), the
    /// chapter asset, the backdrop material, the chapter scene (the sandbox with its town, ramps,
    /// ground and Regaxes taken out, an arena service in their place) and its LevelConfig.
    /// Run again to bring them up to date; definitions are overwritten with the table below.
    /// </summary>
    public static class ArenaChapterBuilder
    {
        public const string CONFIG_FOLDER = "Assets/Game/Configs/Arenas/Chapter1";
        public const string CHAPTER_PATH = CONFIG_FOLDER + "/Chapter1_Arenas.asset";
        public const string MATERIAL_PATH = "Assets/Game/Materials/Arenas/MT_ArenaBackdrop.mat";
        public const string SCENE_PATH = "Assets/Game/Scenes/Levels/Chapter1/Chapter1_Arena.unity";
        public const string LEVEL_PATH = "Assets/Game/Configs/Levels/Chapter1LevelConfig.asset";

        /// <summary>How much further from the tank the camera stands than in the sandbox.</summary>
        private const float CAMERA_WIDEN = 1.7f;

        private const string CATALOG_PATH = "Assets/Game/Configs/Levels/LevelCatalogConfig.asset";
        private const string SANDBOX_SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string LEVEL_LAYER_SCENE = "Assets/Game/Scenes/Levels/VoxelSandbox/VoxelSandbox_Level.unity";

        /// <summary>Roots of the sandbox scene the arena replaces.</summary>
        private static readonly string[] Removed = { "<Town>", "<Ramps>", "<Ground>" };

        [MenuItem("Tools/Arenas/Build Chapter 1 Assets")]
        public static ArenaChapter BuildAssets()
        {
            EnsureFolder(CONFIG_FOLDER);
            var arenas = new List<ArenaDefinition>
            {
                Definition(1, ArenaLayout.Scattered, new(1, 1), new(3, 5), new(1, 2), 0.5f, 0.1f, 10, ArenaHint.Controls, false),
                Definition(2, ArenaLayout.Scattered, new(1, 2), new(4, 6), new(1, 2), 0.8f, 0.3f, 10, ArenaHint.Crush, false),
                Definition(3, ArenaLayout.CoverForSpitters, new(2, 2), new(3, 5), new(1, 2), 0.8f, 0.2f, 12, ArenaHint.DestroyCover, false),
                Definition(4, ArenaLayout.HouseBlocksGate, new(1, 2), new(3, 6), new(1, 3), 1f, 0.25f, 12, ArenaHint.None, false),
                Definition(5, ArenaLayout.Scattered, new(2, 3), new(2, 4), new(0, 1), 0.6f, 0.35f, 14, ArenaHint.None, true)
            };

            var chapter = LoadOrCreate<ArenaChapter>(CHAPTER_PATH);
            chapter.Configure(ChapterIds.Outskirts, arenas);
            EditorUtility.SetDirty(chapter);
            AssetDatabase.SaveAssets();
            return chapter;
        }

        [MenuItem("Tools/Arenas/Build Chapter 1 Scene")]
        public static void BuildScene()
        {
            BuildAssets();
            BackdropMaterial();

            EnsureFolder(Path.GetDirectoryName(SCENE_PATH)!.Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE_PATH) != null)
                AssetDatabase.DeleteAsset(SCENE_PATH);
            AssetDatabase.CopyAsset(SANDBOX_SCENE, SCENE_PATH);

            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            Rigidbody tank = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(Removed, root.name) >= 0 || root.name.StartsWith("Regax_"))
                {
                    Object.DestroyImmediate(root);
                    continue;
                }

                VoxelSandboxStart start = root.GetComponentInChildren<VoxelSandboxStart>(true);
                if (start != null && start.Vehicle != null)
                {
                    tank = start.Vehicle.GetComponentInParent<Rigidbody>();
                    if (tank == null)
                        tank = start.Vehicle.GetComponentInChildren<Rigidbody>();
                }
            }

            // Opening the scene unloads assets nothing refers to: take them again by path.
            var chapter = AssetDatabase.LoadAssetAtPath<ArenaChapter>(CHAPTER_PATH);
            var backdrop = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            var arena = new GameObject("<Arena>");
            SceneManager.MoveGameObjectToScene(arena, scene);
            var service = arena.AddComponent<ArenaService>();
            var so = new SerializedObject(service);
            so.FindProperty("_chapter").objectReferenceValue = chapter;
            so.FindProperty("_tank").objectReferenceValue = tank;
            so.FindProperty("_backdrop").objectReferenceValue = backdrop;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (tank == null)
                Debug.LogWarning("[ArenaChapterBuilder] No tank found (VoxelSandboxStart.Vehicle): set ArenaService._tank by hand.");

            if (tank != null)
                WidenCamera(scene, tank.transform);

            // The sandbox's air bomb every few seconds is the airstrike skill's job in a run.
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "VoxelAirStrike")
                    continue;
                behaviour.enabled = false;
            }

            EditorSceneManager.SaveScene(scene);
            BuildLevelConfig();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArenaChapterBuilder] Built {SCENE_PATH}, tank {(tank != null ? tank.name : "none")}.");
        }

        /// <summary>
        /// The sandbox camera sits close behind the tank; on a 40 m arena it must see most of it
        /// (GDD §3). The follow and the orbit keep the rig where the scene puts it relative to the
        /// tank's camera point, so the rig is moved out along the same line.
        /// </summary>
        private static void WidenCamera(Scene scene, Transform tank)
        {
            Transform point = null;
            foreach (Transform child in tank.GetComponentsInChildren<Transform>(true))
                if (child.name == "CameraPoint")
                    point = child;
            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (camera == null)
                    camera = root.GetComponentInChildren<Camera>(true);
            if (point == null || camera == null)
            {
                Debug.LogWarning("[ArenaChapterBuilder] No CameraPoint or camera: the camera is left as in the sandbox.");
                return;
            }

            Transform rig = camera.transform.parent != null ? camera.transform.parent : camera.transform;
            rig.position = point.position + (rig.position - point.position) * CAMERA_WIDEN;
        }

        private static void BuildLevelConfig()
        {
            var level = LoadOrCreate<LevelConfig>(LEVEL_PATH);
            level.Configure("chapter1_outskirts", "Окраины", 0, SCENE_PATH, LEVEL_LAYER_SCENE);
            level.Configure(1, null);
            EditorUtility.SetDirty(level);

            // In the catalog, so Play from the chapter scene cold-starts the app (EditorColdStart)
            // and the game flow can open it; in the build, so it can be loaded at all.
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CATALOG_PATH);
            if (catalog != null)
            {
                var so = new SerializedObject(catalog);
                SerializedProperty levels = so.FindProperty("_levels");
                bool listed = false;
                for (int i = 0; i < levels.arraySize; i++)
                    listed |= levels.GetArrayElementAtIndex(i).objectReferenceValue == level;
                if (!listed)
                {
                    levels.InsertArrayElementAtIndex(levels.arraySize);
                    levels.GetArrayElementAtIndex(levels.arraySize - 1).objectReferenceValue = level;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == SCENE_PATH))
            {
                scenes.Add(new EditorBuildSettingsScene(SCENE_PATH, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static ArenaDefinition Definition(int number, ArenaLayout layout, Vector2Int houses, Vector2Int trees,
            Vector2Int fences, float relief, float mud, int burrows, ArenaHint hints, bool elite)
        {
            string path = $"{CONFIG_FOLDER}/Arena_{number:00}.asset";
            var definition = LoadOrCreate<ArenaDefinition>(path);
            definition.Configure(new ArenaId($"outskirts_{number:00}"), number, layout, houses, trees, fences, relief, mud, burrows, hints, elite);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Material BackdropMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (material != null)
                return material;

            EnsureFolder(Path.GetDirectoryName(MATERIAL_PATH)!.Replace('\\', '/'));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            // The grass of the voxel ground, a shade darker: it lies beyond the arena's edge.
            material.SetColor("_BaseColor", new Color(0.25f, 0.34f, 0.15f));
            material.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(material, MATERIAL_PATH);
            return material;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

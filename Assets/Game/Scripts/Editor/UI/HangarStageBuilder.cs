using System;
using System.Collections.Generic;
using Game.Voxels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the 3D hangar of the main menu as the HangarStage prefab: a URP camera with the game's post
    /// effects, a key light with soft shadows (only the tank casts them), a rim and a top light without
    /// shadows, a room of plain boxes, a turntable and the Tiger. The Tiger is Tank_01 stripped down to its
    /// transforms, mesh renderers and a disabled VoxelizedModel: no installers, physics or particles; the
    /// stage builds its voxel skin on start, as the battle does, but as static meshes.
    /// </summary>
    public static class HangarStageBuilder
    {
        public const string PREFAB_NAME = "HangarStage";
        public const string MATERIAL_FOLDER = "Assets/Game/Materials/UI";

        private const string TANK_PREFAB = "Assets/Game/Prefabs/Vehicles/Tank_01.prefab";
        private const string SKIN_MATERIAL = "Assets/Game/Voxels/Resources/Voxels/VoxelChunk.mat";
        private const string VOLUME_PROFILE = "Assets/Settings/Camera Profile.asset";
        private const string CAMERA_DATA_TYPE =
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";
        private const string VOLUME_TYPE = "UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime";
        private const string LIT_SHADER = "Universal Render Pipeline/Lit";

        private const float FOV = 35f;
        private const float ASPECT = 16f / 9f;
        private const float TURNTABLE_HEIGHT = 0.16f;
        private const float WALL_HEIGHT = 7f;

        private static readonly Color ClearColor = new(0.03f, 0.035f, 0.04f, 1f);

        private static string PrefabPath => $"{UiFactory.PREFAB_FOLDER}/{PREFAB_NAME}.prefab";

        /// <summary>
        /// Puts the stage into the active scene (the prefab, built first if missing) and sets the scene's
        /// ambient light. The scene's other camera, if any, must go: the stage brings the MainCamera.
        /// </summary>
        public static HangarStage Create()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                prefab = BuildPrefab();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Transform key = instance.transform.Find("Lights/KeyLight");
            ApplyEnvironment(key != null ? key.GetComponent<Light>() : null);
            return instance.GetComponent<HangarStage>();
        }

        /// <summary>Rebuilds the prefab from Tank_01 (in a preview scene: the open scene is not touched).</summary>
        [MenuItem("Tools/UI/Build Hangar Stage")]
        public static GameObject BuildPrefab()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = Build(scene);
                UiFactory.EnsureFolder(UiFactory.PREFAB_FOLDER);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"Hangar stage built: {PrefabPath}");
                return prefab;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // ---------- Stage ----------

        private static GameObject Build(Scene scene)
        {
            var root = new GameObject(PREFAB_NAME);
            SceneManager.MoveGameObjectToScene(root, scene);
            HangarStage stage = root.AddComponent<HangarStage>();

            Material floor = EnsureMaterial("MT_Hangar_Floor", new Color(0.16f, 0.165f, 0.17f), 0.3f, 0f);
            Material wall = EnsureMaterial("MT_Hangar_Wall", new Color(0.1f, 0.11f, 0.12f), 0.1f, 0f);
            Material table = EnsureMaterial("MT_Hangar_Turntable", new Color(0.2f, 0.205f, 0.21f), 0.45f, 0.6f);
            Material accent = EnsureMaterial("MT_Hangar_Accent", UiStyle.Accent, 0.4f, 0f, UiStyle.Accent * 1.5f);

            // The turntable is an unscaled pivot that turns; the disc under it and the tank's mount are its children.
            Transform turntable = new GameObject("Turntable").transform;
            Place(turntable.gameObject, root.transform);
            Transform mount = new GameObject("TankMount").transform;
            Place(mount.gameObject, turntable);
            mount.localPosition = new Vector3(0f, TURNTABLE_HEIGHT, 0f);
            // The camera looks along +Z: the tank (gun along its +Z) starts facing it.
            mount.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Tank first: the disc, the room and the framings are sized by it.
            GameObject tank = CreateTiger(scene, out float voxelSize);
            HangarStage.Seat(tank.transform, mount);
            Bounds bounds = RendererBounds(tank);
            float radius = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);

            float tableRadius = radius + 0.35f;
            Box("Disc", turntable, PrimitiveType.Cylinder, table, new Vector3(0f, TURNTABLE_HEIGHT * 0.5f, 0f),
                new Vector3(tableRadius * 2f, TURNTABLE_HEIGHT * 0.5f, tableRadius * 2f));
            Box("TurntableRing", root.transform, PrimitiveType.Cylinder, accent, new Vector3(0f, 0.01f, 0f),
                new Vector3(tableRadius * 2f + 0.16f, 0.01f, tableRadius * 2f + 0.16f));

            BuildRoom(root.transform, radius, floor, wall, accent);
            BuildLights(root.transform, bounds);

            // Camera and its two framings.
            Transform menuPose = new GameObject("MenuPose").transform;
            Place(menuPose.gameObject, root.transform);
            Transform hangarPose = new GameObject("HangarPose").transform;
            Place(hangarPose.gameObject, root.transform);
            // Main menu: a wider three-quarter view, the tank low and right of the buttons column.
            Frame(menuPose, bounds.center, radius, bounds.extents.y, 22f, 8f, new Vector2(0.64f, 0.38f), new Vector2(0.5f, 0.42f));
            // Hangar: the tank between the slot and stat panels (0.24..0.74 of the width), above the modules strip.
            Frame(hangarPose, bounds.center, radius, bounds.extents.y, -28f, 13f, new Vector2(0.49f, 0.6f), new Vector2(0.44f, 0.5f));
            Camera camera = CreateCamera(root.transform);
            camera.transform.SetPositionAndRotation(menuPose.position, menuPose.rotation);

            UiFactory.SetField(stage, "_camera", camera.transform);
            UiFactory.SetField(stage, "_menuPose", menuPose);
            UiFactory.SetField(stage, "_hangarPose", hangarPose);
            UiFactory.SetField(stage, "_turntable", turntable);
            UiFactory.SetField(stage, "_tankMount", mount);
            UiFactory.SetField(stage, "_tankVisual", tank);
            UiFactory.SetField(stage, "_skinMaterial", AssetDatabase.LoadAssetAtPath<Material>(SKIN_MATERIAL));
            SetFloat(stage, "_skinVoxelSize", voxelSize);
            return root;
        }

        // ---------- Tiger ----------

        /// <summary>
        /// Tank_01 unpacked and stripped to what draws it: transforms, mesh filters and renderers, and its
        /// VoxelizedModel switched off (its own Start would need a VoxelWorld; the stage builds the skin).
        /// </summary>
        private static GameObject CreateTiger(Scene scene, out float voxelSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TANK_PREFAB);
            if (prefab == null)
                throw new InvalidOperationException($"{nameof(HangarStageBuilder)}: no tank prefab at {TANK_PREFAB}");

            var tank = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(tank, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            tank.name = "Tiger";

            foreach (Transform t in tank.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            Strip(tank);

            voxelSize = 0.03f;
            if (tank.TryGetComponent(out VoxelizedModel model))
            {
                model.enabled = false;
                SerializedProperty size = new SerializedObject(model).FindProperty("_voxelSize");
                if (size != null)
                    voxelSize = size.floatValue;
            }
            else
            {
                Debug.LogWarning($"{nameof(HangarStageBuilder)}: Tank_01 has no VoxelizedModel; the hangar shows its meshes.");
            }

            // Small parts: no shadows from the meshes (the voxel skin casts them, the kept track links do not).
            foreach (MeshRenderer renderer in tank.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }

            return tank;
        }

        /// <summary>Destroys every component but Transform, MeshFilter, MeshRenderer and VoxelizedModel.</summary>
        private static void Strip(GameObject tank)
        {
            var doomed = new List<Component>();
            foreach (Component c in tank.GetComponentsInChildren<Component>(true))
                if (c != null && !Keep(c))
                    doomed.Add(c);

            // Dependents first: scripts (RequireComponent), joints, particle systems, colliders, then bodies.
            doomed.Sort((a, b) => Order(a).CompareTo(Order(b)));
            foreach (Component c in doomed)
                if (c != null)
                    Object.DestroyImmediate(c);

            foreach (Component c in tank.GetComponentsInChildren<Component>(true))
                if (c != null && !Keep(c))
                    Debug.LogWarning($"{nameof(HangarStageBuilder)}: could not strip {c.GetType().Name} from {c.name}", c);
        }

        private static bool Keep(Component c) =>
            c is Transform || c is MeshFilter || c is MeshRenderer || c is VoxelizedModel;

        private static int Order(Component c) => c switch
        {
            MonoBehaviour => 0,
            Joint => 1,
            ParticleSystem => 2,
            Collider => 3,
            Rigidbody => 4,
            _ => 5
        };

        // ---------- Room ----------

        private static void BuildRoom(Transform root, float radius, Material floor, Material wall, Material accent)
        {
            // The camera looks along +Z: the back wall is ahead of it, the side walls left and right.
            float half = Mathf.Max(10f, radius * 4f);
            float back = Mathf.Max(8f, radius * 3.5f);
            float depth = back + half * 1.6f;
            Transform room = new GameObject("Room").transform;
            Place(room.gameObject, root);

            Box("Floor", room, PrimitiveType.Cube, floor, new Vector3(0f, -0.1f, back - depth * 0.5f),
                new Vector3(half * 2f, 0.2f, depth));
            Box("BackWall", room, PrimitiveType.Cube, wall, new Vector3(0f, WALL_HEIGHT * 0.5f, back + 0.1f),
                new Vector3(half * 2f, WALL_HEIGHT, 0.2f));
            Box("LeftWall", room, PrimitiveType.Cube, wall, new Vector3(-half - 0.1f, WALL_HEIGHT * 0.5f, back - depth * 0.5f),
                new Vector3(0.2f, WALL_HEIGHT, depth));
            Box("RightWall", room, PrimitiveType.Cube, wall, new Vector3(half + 0.1f, WALL_HEIGHT * 0.5f, back - depth * 0.5f),
                new Vector3(0.2f, WALL_HEIGHT, depth));

            // Pillars and two lamp strips on the back wall: something for the eye behind the tank.
            for (int i = -1; i <= 1; i += 2)
            {
                Box(i < 0 ? "PillarLeft" : "PillarRight", room, PrimitiveType.Cube, wall,
                    new Vector3(i * half * 0.55f, WALL_HEIGHT * 0.5f, back - 0.4f), new Vector3(0.8f, WALL_HEIGHT, 0.6f));
                Box(i < 0 ? "LampLeft" : "LampRight", room, PrimitiveType.Cube, accent,
                    new Vector3(i * half * 0.28f, WALL_HEIGHT * 0.62f, back - 0.02f), new Vector3(half * 0.3f, 0.08f, 0.04f));
            }
        }

        // ---------- Lights ----------

        private static void BuildLights(Transform root, Bounds tank)
        {
            Transform lights = new GameObject("Lights").transform;
            Place(lights.gameObject, root);

            // Key: from the front left, above; the only light with shadows (only the tank casts any).
            Light key = NewLight("KeyLight", lights, LightType.Directional, new Color(1f, 0.95f, 0.87f), 1.6f);
            key.transform.rotation = Quaternion.Euler(48f, 32f, 0f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.85f;

            // Rim: from behind, cool, outlines the turret against the dark wall.
            Light rim = NewLight("RimLight", lights, LightType.Directional, new Color(0.7f, 0.8f, 1f), 0.45f);
            rim.transform.rotation = Quaternion.Euler(24f, -150f, 0f);

            // Top: a pool of light on the turntable, limited range, no shadows.
            Light top = NewLight("TopLight", lights, LightType.Spot, new Color(1f, 0.92f, 0.8f), 14f);
            top.transform.SetPositionAndRotation(new Vector3(0f, tank.max.y + 4.5f, 0f), Quaternion.Euler(90f, 0f, 0f));
            top.range = tank.max.y + 7f;
            top.spotAngle = 70f;
            top.innerSpotAngle = 40f;
        }

        private static Light NewLight(string name, Transform parent, LightType type, Color color, float intensity)
        {
            var go = new GameObject(name, typeof(Light));
            Place(go, parent);
            Light light = go.GetComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return light;
        }

        /// <summary>The scene's ambient: dim trilight as in the sandbox, no skybox, no fog; the key light is the sun.</summary>
        private static void ApplyEnvironment(Light sun)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.215f, 0.245f);
            RenderSettings.ambientEquatorColor = new Color(0.11f, 0.12f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.045f, 0.04f);
            RenderSettings.fog = false;
            if (sun != null && sun.type == LightType.Directional)
                RenderSettings.sun = sun;
        }

        // ---------- Camera ----------

        private static Camera CreateCamera(Transform root)
        {
            var go = new GameObject("HangarCamera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            Place(go, root);
            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ClearColor;
            camera.fieldOfView = FOV;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;

            // URP camera data and the game's post profile, by name: Game.Editor does not reference URP.
            Component data = AddByName(go, CAMERA_DATA_TYPE);
            if (data != null)
            {
                var so = new SerializedObject(data);
                so.FindProperty("m_RenderPostProcessing").boolValue = true;
                so.FindProperty("m_Antialiasing").intValue = 2; // SMAA: crisp voxel edges, no history
                // m_VolumeLayerMask stays Default: the volume below sits on that layer
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var volumeObject = new GameObject("PostVolume");
            Place(volumeObject, root);
            Component volume = AddByName(volumeObject, VOLUME_TYPE);
            Object profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(VOLUME_PROFILE);
            if (volume != null && profile != null)
            {
                var so = new SerializedObject(volume);
                so.FindProperty("m_IsGlobal").boolValue = true;
                so.FindProperty("sharedProfile").objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (volume != null)
            {
                Debug.LogWarning($"{nameof(HangarStageBuilder)}: no volume profile at {VOLUME_PROFILE}");
            }

            return camera;
        }

        /// <summary>
        /// A camera pose that shows a body of horizontal radius <paramref name="radius"/> (any turn of the
        /// turntable) and half height <paramref name="halfHeight"/> inside a part of a 16:9 screen
        /// (<paramref name="at"/> its centre, <paramref name="size"/> its size, both 0..1).
        /// </summary>
        private static void Frame(Transform pose, Vector3 center, float radius, float halfHeight, float yaw, float pitch,
            Vector2 at, Vector2 size)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float tanV = Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * ASPECT;
            float lift = radius * Mathf.Sin(pitch * Mathf.Deg2Rad);
            // the near half of the body is closer than the centre: half a radius more keeps it inside
            float distance = Mathf.Max(radius / (tanH * size.x), (halfHeight + lift) / (tanV * size.y)) + radius * 0.5f;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 aim = center
                          - right * ((at.x - 0.5f) * 2f * tanH * distance)
                          - up * ((at.y - 0.5f) * 2f * tanV * distance);
            pose.SetPositionAndRotation(aim - rotation * Vector3.forward * distance, rotation);
        }

        // ---------- Helpers ----------

        /// <summary>A primitive without collider, casting no shadows (only the tank does).</summary>
        private static GameObject Box(string name, Transform parent, PrimitiveType type, Material material,
            Vector3 position, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Place(go, parent);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        /// <summary>Moves a new root object into the parent's scene (a preview scene) and parents it.</summary>
        private static void Place(GameObject go, Transform parent)
        {
            if (go.transform.parent == null && go.scene != parent.gameObject.scene)
                SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
        }

        private static Bounds RendererBounds(GameObject go)
        {
            bool any = false;
            Bounds bounds = new(go.transform.position, Vector3.zero);
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (!any) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                any = true;
            }

            return bounds;
        }

        private static Material EnsureMaterial(string name, Color color, float smoothness, float metallic,
            Color? emission = null)
        {
            UiFactory.EnsureFolder(MATERIAL_FOLDER);
            string path = $"{MATERIAL_FOLDER}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find(LIT_SHADER);
            if (shader == null)
                throw new InvalidOperationException($"{nameof(HangarStageBuilder)}: shader '{LIT_SHADER}' not found");

            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Component AddByName(GameObject go, string typeName)
        {
            Type type = Type.GetType(typeName);
            if (type == null)
            {
                Debug.LogWarning($"{nameof(HangarStageBuilder)}: type {typeName} not found");
                return null;
            }

            Component existing = go.GetComponent(type);
            return existing != null ? existing : go.AddComponent(type);
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

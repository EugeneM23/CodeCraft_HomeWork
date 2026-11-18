using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the tank destruction lab scene (prototype): a studio — neutral floor and backdrop, key,
    /// fill and rim lights, the game's post-processing volume — with a turntable, a target tank
    /// (TankDummy_01) on it and a fixed camera; TankLab drives it. Rebuilt from scratch each time.
    /// </summary>
    public static class TankLabBuilder
    {
        private const string SCENE = "Assets/Game/Scenes/Labs/TankDestructionLab.unity";

        /// <summary>The sandbox's post-processing profile (the same look as the game).</summary>
        private const string PROFILE_GUID = "6e26b30d444fd41f3a15ad77fe182bf7";

        /// <summary>The layer the game keeps its volumes on, and the camera's volume mask.</summary>
        private const int VOLUME_LAYER = 9;

        [MenuItem("Tools/Voxels/Build Tank Destruction Lab")]
        public static void Build()
        {
            if (!System.IO.Directory.Exists("Assets/Game/Scenes/Labs"))
                AssetDatabase.CreateFolder("Assets/Game/Scenes", "Labs");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.57f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.23f);

            // Studio: a floor and a backdrop in a neutral grey.
            var grey = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.46f, 0.47f, 0.49f) };
            grey.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(grey, "Assets/Game/Scenes/Labs/LabBackdrop.mat");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(8f, 1f, 8f);
            floor.GetComponent<Renderer>().sharedMaterial = grey;
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Backdrop";
            back.transform.position = new Vector3(0f, 10f, 22f);
            back.transform.localScale = new Vector3(80f, 20f, 0.5f);
            back.GetComponent<Renderer>().sharedMaterial = grey;

            // Lights: key (shadows), fill, rim.
            Light("Key", new Vector3(40f, -35f, 0f), new Color(1f, 0.96f, 0.9f), 2.2f, LightShadows.Soft);
            Light("Fill", new Vector3(20f, 50f, 0f), new Color(0.8f, 0.86f, 1f), 0.7f, LightShadows.None);
            Light("Rim", new Vector3(30f, 170f, 0f), new Color(1f, 1f, 1f), 1.4f, LightShadows.None);

            // The game's post-processing.
            var volumeGo = new GameObject("Post") { layer = VOLUME_LAYER };
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(PROFILE_GUID));

            // The turntable: an unscaled pivot with a disc whose top is 0.3 m up.
            var table = new GameObject("Turntable").transform;
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            disc.transform.SetParent(table, false);
            disc.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            disc.transform.localScale = new Vector3(8f, 0.15f, 8f);
            var dark = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.22f, 0.22f, 0.23f) };
            AssetDatabase.CreateAsset(dark, "Assets/Game/Scenes/Labs/LabTable.mat");
            disc.GetComponent<Renderer>().sharedMaterial = dark;

            var focus = new GameObject("Focus").transform;
            focus.position = new Vector3(0f, 1.3f, 0f);

            var cameraGo = new GameObject("Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.4f, 0.41f, 0.43f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            var data = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1 << VOLUME_LAYER;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraGo.AddComponent<AudioListener>();

            var lab = new GameObject("TankLab").AddComponent<TankLab>();
            var so = new SerializedObject(lab);
            so.FindProperty("_target").objectReferenceValue = Resources.Load<GameObject>("TankDummy_01");
            so.FindProperty("_table").objectReferenceValue = table;
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_focus").objectReferenceValue = focus;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SCENE);
            AssetDatabase.SaveAssets();
            Debug.Log("[TankLab] built " + SCENE);
        }

        private static void Light(string name, Vector3 euler, Color color, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
        }
    }
}

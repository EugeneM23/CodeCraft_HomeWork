// Puts the game's scenes on URP: every camera gets its URP data (post-processing on where the
// scene had Post Processing v2, the volume mask on the layer its Volume sits on), the PPv2
// components go, and VoxelSandbox loses the old IMGUI VoxelDemoHud (the menu's HUD replaced it).
// Run in Edit Mode, nothing else open and unsaved:
//   unity cmd --port <port> script eval --file Tools/Urp/setup_scenes.cs
// AppDomain data "urp_scenes_apply" = "1" applies and saves; otherwise it only reports.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

if (EditorApplication.isPlaying) return "abort: in Play";
foreach (var open in Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt))
    if (open.isDirty) return "abort: " + open.name + " has unsaved changes";

bool apply = AppDomain.CurrentDomain.GetData("urp_scenes_apply") as string == "1";
string[] scenes =
{
    "Assets/Game/Voxels/Scenes/VoxelSandbox.unity",
    "Assets/Game/Scenes/Levels/VoxelSandbox/VoxelSandbox_Level.unity",
    "Assets/Game/Scenes/Boot.unity",
    "Assets/Game/Scenes/MainMenu.unity",
};
string active = EditorSceneManager.GetActiveScene().path;
var sb = new System.Text.StringBuilder(apply ? "APPLY\n" : "REPORT\n");

foreach (string path in scenes)
{
    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    sb.AppendLine("== " + scene.name);
    var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
    bool changed = false;

    foreach (var cam in all.Select(g => g.GetComponent<Camera>()).Where(c => c != null))
    {
        // PPv2's components, found by type name: the package goes once no scene uses it.
        var ppv2 = cam.GetComponents<Component>().Where(c => c != null && c.GetType().Namespace == "UnityEngine.Rendering.PostProcessing").ToList();
        var volume = cam.GetComponent<Volume>();
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        sb.AppendLine($"  camera {cam.name} layer={cam.gameObject.layer} clear={cam.clearFlags} target={(cam.targetTexture ? "RT" : "screen")} ppv2=[{string.Join(",", ppv2.Select(c => c.GetType().Name))}] " +
                      $"volume={(volume ? volume.sharedProfile?.name + " w" + volume.weight : "none")} urpData={(data ? "post=" + data.renderPostProcessing + " mask=" + data.volumeLayerMask.value + " type=" + data.renderType : "none")}");
        if (!apply) continue;

        bool hadPost = ppv2.Count > 0 || volume != null;
        if (data == null)
            data = Undo.AddComponent<UniversalAdditionalCameraData>(cam.gameObject);
        if (hadPost)
        {
            data.renderPostProcessing = true;
            // PPv2 read the volumes on its own layer; URP's default mask is Default only.
            if (volume != null)
                data.volumeLayerMask = 1 << volume.gameObject.layer;
            data.antialiasing = AntialiasingMode.None;
        }
        foreach (var c in ppv2.OrderBy(c => c.GetType().Name == "PostProcessLayer" ? 0 : 1))
            UnityEngine.Object.DestroyImmediate(c, true);
        changed = true;
    }

    foreach (var light in all.Select(g => g.GetComponent<Light>()).Where(l => l != null))
    {
        var data = light.GetComponent<UniversalAdditionalLightData>();
        sb.AppendLine($"  light {light.name} {light.type} I={light.intensity} shadows={light.shadows} bias={light.shadowBias}/{light.shadowNormalBias} urpData={(data ? "pipelineBias=" + data.usePipelineSettings : "none")}");
        if (apply && data == null)
        {
            Undo.AddComponent<UniversalAdditionalLightData>(light.gameObject);
            changed = true;
        }
    }

    foreach (var hud in all.SelectMany(g => g.GetComponents<MonoBehaviour>()).Where(m => m != null && m.GetType().Name == "VoxelDemoHud").ToList())
    {
        sb.AppendLine("  VoxelDemoHud on " + hud.name);
        if (apply)
        {
            UnityEngine.Object.DestroyImmediate(hud, true);
            changed = true;
        }
    }

    int missing = all.Sum(g => g.GetComponents<Component>().Count(c => c == null));
    if (missing > 0) sb.AppendLine("  missing scripts: " + missing);

    if (apply && changed)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        sb.AppendLine("  saved");
    }
}

if (!string.IsNullOrEmpty(active)) EditorSceneManager.OpenScene(active, OpenSceneMode.Single);
return sb.ToString();

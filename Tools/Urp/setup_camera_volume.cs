// Rebuilds the URP volume profile the scene cameras already use (Assets/Settings/Camera Profile.asset,
// on the camera's Volume, weight 1) so URP shows what Post Processing v2 showed with
// "[Camera] Profile" at volume weight 0.529. PPv2 blends every overridden value from the effect's
// default towards the profile value by the weight; the numbers below are those blended values.
// Ambient occlusion is not a volume effect in URP: it is the SSAO feature on PC_Renderer
// (Tools/Urp/setup_urp_assets.cs). Vignette was inactive in PPv2 and stays out.
// Run in Edit Mode: unity cmd --port <port> script eval --file Tools/Urp/setup_camera_volume.cs
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

if (EditorApplication.isPlaying) return "abort: in Play";
const string Path = "Assets/Settings/Camera Profile.asset";
const float W = 0.529f; // PPv2 volume weight

var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Path);
if (profile == null) return "no profile at " + Path;

// Start clean: the profile held template leftovers (shadows/midtones/highlights) that Built-in never showed.
// Every sub-asset goes, listed or not (an earlier version left an orphan behind).
profile.components.Clear();
foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(Path))
{
    if (sub != null && sub != profile)
        Object.DestroyImmediate(sub, true);
}

T Add<T>() where T : VolumeComponent
{
    var c = profile.Add<T>(false);
    c.name = typeof(T).Name;
    AssetDatabase.AddObjectToAsset(c, profile);
    return c;
}

// Bloom: PPv2 intensity 1 -> 0.529, threshold 1, soft knee 0.5 -> 0.76, diffusion 7 (URP scatter 0.7).
var bloom = Add<Bloom>();
bloom.intensity.Override(1f * W);
bloom.threshold.Override(1f);
bloom.scatter.Override(0.7f);
bloom.highQualityFiltering.Override(true);

// Chromatic aberration: 0.239 -> 0.126.
var ca = Add<ChromaticAberration>();
ca.intensity.Override(0.239f * W);

// Motion blur: shutter angle 270 (= 0.75 of a frame), 10 samples; camera and object motion as PPv2.
var mb = Add<MotionBlur>();
mb.mode.Override(MotionBlurMode.CameraAndObjects);
mb.quality.Override(MotionBlurQuality.High);
mb.intensity.Override(270f / 360f);
mb.clamp.Override(0.05f);

// No tonemapping and no grading in PPv2 here: the camera output is linear HDR clamped by the display.
var tm = Add<Tonemapping>();
tm.mode.Override(TonemappingMode.None);

EditorUtility.SetDirty(profile);
AssetDatabase.SaveAssets();
return "ok: " + string.Join(", ", profile.components.Select(c => c.GetType().Name));

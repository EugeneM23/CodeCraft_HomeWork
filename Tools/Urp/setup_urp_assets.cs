// Creates (or updates) the URP pipeline and renderer assets and assigns them to the quality levels.
// Run in Edit Mode in the project's editor:
//   unity cmd --port <port> script eval --file Tools/Urp/setup_urp_assets.cs
// Safe to run again: existing assets are updated in place, so their GUIDs stay.
// The reasoning behind every number is in Docs/superpowers/specs/2026-11-17-urp.md.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

if (EditorApplication.isPlaying) return "abort: in Play";

const string Dir = "Assets/Settings/Rendering";
var log = new List<string>();

void Set(SerializedObject so, string name, Action<SerializedProperty> apply)
{
    var p = so.FindProperty(name);
    if (p == null) { log.Add("missing " + so.targetObject.name + "." + name); return; }
    apply(p);
}
void Int(SerializedObject so, string n, int v) => Set(so, n, p => p.intValue = v);
void Flt(SerializedObject so, string n, float v) => Set(so, n, p => p.floatValue = v);
void Bool(SerializedObject so, string n, bool v) => Set(so, n, p => p.boolValue = v);

T LoadOrCreate<T>(string path) where T : ScriptableObject
{
    var a = AssetDatabase.LoadAssetAtPath<T>(path);
    if (a != null) return a;
    a = ScriptableObject.CreateInstance<T>();
    AssetDatabase.CreateAsset(a, path);
    log.Add("created " + path);
    return a;
}

var postData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
if (postData == null) return "abort: no default PostProcessData";

// ---------- Renderers ----------
// PC: Forward+ with SSAO (stands in for PPv2's ambient occlusion). Lite: Forward+ without SSAO.
// Mobile: plain Forward, no SSAO.
UniversalRendererData Renderer(string name, RenderingMode mode, bool ssao)
{
    var r = LoadOrCreate<UniversalRendererData>($"{Dir}/{name}.asset");
    r.postProcessData = postData;
    var so = new SerializedObject(r);
    Int(so, "m_RenderingMode", (int)mode);
    Int(so, "m_DepthPrimingMode", (int)DepthPrimingMode.Disabled);
    Int(so, "m_CopyDepthMode", (int)CopyDepthMode.AfterOpaques);
    Bool(so, "m_UseNativeRenderPass", false);
    so.ApplyModifiedPropertiesWithoutUndo();

    var existing = r.rendererFeatures.Find(f => f is ScreenSpaceAmbientOcclusion) as ScreenSpaceAmbientOcclusion;
    if (ssao && existing == null)
    {
        existing = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
        existing.name = "SSAO";
        AssetDatabase.AddObjectToAsset(existing, r);
        r.rendererFeatures.Add(existing);
        var list = new SerializedObject(r);
        var map = list.FindProperty("m_RendererFeatureMap");
        map.arraySize = r.rendererFeatures.Count;
        for (int i = 0; i < r.rendererFeatures.Count; i++)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(r.rendererFeatures[i], out string _, out long id);
            map.GetArrayElementAtIndex(i).longValue = id;
        }
        list.ApplyModifiedPropertiesWithoutUndo();
        log.Add("added SSAO to " + name);
    }
    if (ssao)
    {
        // PPv2 here: Scalable Ambient Obscurance, Ultra, intensity 2 and radius 0.1 at volume weight
        // 0.529 -> intensity ~1.06, radius ~0.17 m. URP's SSAO is tuned to the same darkening.
        var s = new SerializedObject(existing);
        Int(s, "m_Settings.AOMethod", 0);              // Blue noise
        Bool(s, "m_Settings.Downsample", false);
        Bool(s, "m_Settings.AfterOpaque", false);
        Int(s, "m_Settings.Source", 1);                // Depth normals
        Int(s, "m_Settings.NormalSamples", 1);         // Medium
        Flt(s, "m_Settings.Intensity", 1.1f);
        // PPv2 multiplied its AO over the whole forward image, sunlight included.
        Flt(s, "m_Settings.DirectLightingStrength", 1f);
        Flt(s, "m_Settings.Radius", 0.17f);
        Int(s, "m_Settings.Samples", 0);               // High
        Int(s, "m_Settings.BlurQuality", 0);           // High
        Flt(s, "m_Settings.Falloff", 100f);
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    EditorUtility.SetDirty(r);
    return r;
}

var pcRenderer = Renderer("PC_Renderer", RenderingMode.ForwardPlus, true);
var liteRenderer = Renderer("Lite_Renderer", RenderingMode.ForwardPlus, false);
var mobileRenderer = Renderer("Mobile_Renderer", RenderingMode.Forward, false);

// ---------- Pipeline assets ----------
// Built-in levels this replaces (QualitySettings): shadows off on Very Low/Low; hard 1 cascade 35 m
// on Medium; 2 cascades 40-45 m above; MSAA 2x on Very High/Ultra. Shadow map sizes follow
// Built-in's for a 1080p screen (Medium ~1024, High ~2048).
UniversalRenderPipelineAsset Pipeline(string name, ScriptableRendererData renderer, bool hdr, int msaa, bool shadows,
    int shadowRes, int cascades, float distance, bool additionalShadows)
{
    var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"{Dir}/{name}.asset");
    if (a == null)
    {
        a = UniversalRenderPipelineAsset.Create(renderer);
        AssetDatabase.CreateAsset(a, $"{Dir}/{name}.asset");
        log.Add("created " + name);
    }
    var so = new SerializedObject(a);
    var list = so.FindProperty("m_RendererDataList");
    list.arraySize = 1;
    list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
    Int(so, "m_DefaultRendererIndex", 0);
    Bool(so, "m_SupportsHDR", hdr);
    Int(so, "m_MSAA", msaa);
    Flt(so, "m_RenderScale", 1f);
    Bool(so, "m_RequireDepthTexture", true);        // blood decals read the camera depth
    Bool(so, "m_RequireOpaqueTexture", false);
    Int(so, "m_MainLightRenderingMode", 1);         // per pixel
    Bool(so, "m_MainLightShadowsSupported", shadows);
    Int(so, "m_MainLightShadowmapResolution", shadowRes);
    Int(so, "m_AdditionalLightsRenderingMode", 1);  // per pixel
    Int(so, "m_AdditionalLightsPerObjectLimit", 4);
    Bool(so, "m_AdditionalLightShadowsSupported", additionalShadows);
    Int(so, "m_AdditionalLightsShadowmapResolution", 2048);
    Flt(so, "m_ShadowDistance", distance);
    Int(so, "m_ShadowCascadeCount", cascades);
    Flt(so, "m_Cascade2Split", 1f / 3f);            // Built-in's split
    // Built-in's light defaults, which the sun kept: URP's own 1 / 1 lifts the small shadows the
    // voxel steps cast on themselves, and walls came out flat and brighter.
    Flt(so, "m_ShadowDepthBias", 0.05f);
    Flt(so, "m_ShadowNormalBias", 0.4f);
    Bool(so, "m_SoftShadowsSupported", true);       // the sun is Hard; soft only where a light asks
    Bool(so, "m_UseSRPBatcher", true);
    Int(so, "m_ShEvalMode", 3);                     // per pixel: no SH keyword variants
    Bool(so, "m_SupportsLightCookies", false);
    so.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(a);
    return a;
}

var low = Pipeline("URP_PC_Low", liteRenderer, true, 1, false, 1024, 1, 20f, false);
var medium = Pipeline("URP_PC_Medium", pcRenderer, true, 1, true, 1024, 1, 35f, false);
var high = Pipeline("URP_PC_High", pcRenderer, true, 1, true, 2048, 2, 40f, true);
var ultra = Pipeline("URP_PC_Ultra", pcRenderer, true, 2, true, 2048, 2, 45f, true);
var mobile = Pipeline("URP_Mobile", mobileRenderer, false, 1, true, 1024, 1, 40f, false);

// ---------- Quality levels ----------
var byLevel = new Dictionary<string, UniversalRenderPipelineAsset>
{
    ["Very Low"] = low, ["Low"] = low, ["Medium"] = medium, ["High"] = high,
    ["Very High"] = ultra, ["Ultra"] = ultra, ["Mobile"] = mobile,
};
int current = QualitySettings.GetQualityLevel();
string[] names = QualitySettings.names;
for (int i = 0; i < names.Length; i++)
{
    if (!byLevel.TryGetValue(names[i], out var asset)) { log.Add("no asset for level " + names[i]); continue; }
    QualitySettings.SetQualityLevel(i, false);
    QualitySettings.renderPipeline = asset;
}
QualitySettings.SetQualityLevel(current, false);
GraphicsSettings.defaultRenderPipeline = high;

AssetDatabase.SaveAssets();
return "ok; " + string.Join("; ", log) + "; current level " + names[current] + " -> " + (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "null");

// VoxelSandbox before/after screenshots for the render pipeline switch (Built-in -> URP).
// Run in Play, in VoxelSandbox, with one call:
//   unity cmd --port <port> script eval --file Tools/RenderShots/render_shots.cs
// Before the call set AppDomain data "shots_dir" to the output folder (default below).
// It returns at once and plays a fixed script over the next ~30 s: shots of the start, then the
// tank digs ruts, crushes a Regax, a blast goes off, rain starts. Every camera pose is fixed
// relative to the tank's start pose, so a second run on another pipeline shoots the same points.
// Each shot holds the scene camera at its pose for a few frames (motion blur sees a still
// camera), then renders it into a 1920x1080 texture: post-processing included, IMGUI HUD not.
// Poll AppDomain data "shots_status" until it reads "done ..." or "failed ...".
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

const string DefaultDir = "Screenshots/RenderShots";
const int Width = 1920, Height = 1080;
const int HoldFrames = 8;

var domain = AppDomain.CurrentDomain;
if (!EditorApplication.isPlaying) return "ERROR not in Play";
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "VoxelSandbox") return "ERROR not VoxelSandbox";
if (domain.GetData("shots_status") is string st && st.StartsWith("running")) return "ERROR already running: " + st;
if (domain.GetData("shots_update") is EditorApplication.CallbackFunction oldU) EditorApplication.update -= oldU;
if (domain.GetData("shots_render") is UnityEngine.Events.UnityAction oldR) Application.onBeforeRender -= oldR;

string dir = domain.GetData("shots_dir") as string ?? DefaultDir;
Directory.CreateDirectory(dir);

// ---------- Scene objects (same lookups as Tools/PerfCheck/Bench/perf_bench.cs) ----------
var asm = typeof(Game.EntityAPI).Assembly;
MethodInfo FindExt(string name)
{
    foreach (var t in asm.GetTypes())
    {
        var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        if (m != null) return m;
    }
    return null;
}
object ctx = FindExt("GetPlayerContext").Invoke(null, new object[] { Game.GameContext.Instance });
object charVar = FindExt("GetCharacter").Invoke(null, new object[] { ctx });
object character = charVar.GetType().GetProperty("Value").GetValue(charVar);
var dargs = new object[] { character, null };
if (!(bool)FindExt("TryGetDriveInput").Invoke(null, dargs)) return "ERROR the player is not in the tank";
var driveInput = (Atomic.Elements.IReactiveVariable<Vector2>)dargs[1];

var start = UnityEngine.Object.FindFirstObjectByType<Game.VoxelSandboxStart>();
Transform tank = start != null && start.Vehicle != null ? start.Vehicle.transform : GameObject.Find("Tank_01")?.transform;
if (tank == null) return "ERROR no tank";

Camera cam = Camera.main;
if (cam == null) return "ERROR no main camera";

var strikes = UnityEngine.Object.FindObjectsByType<Game.VoxelAirStrike>(FindObjectsSortMode.None);
FieldInfo onField = typeof(Game.VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
MethodInfo explode = typeof(Game.VoxelAirStrike).GetMethod("Explode", BindingFlags.NonPublic | BindingFlags.Instance);
var strikeWasOn = new bool[strikes.Length];
for (int i = 0; i < strikes.Length; i++) { strikeWasOn[i] = (bool)onField.GetValue(strikes[i]); onField.SetValue(strikes[i], false); }

Vector3 Ground(Vector3 p) =>
    Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;

// Tank frame at the start: the origin of every pose.
Vector3 t0 = tank.position;
Vector3 fwd = Vector3.ProjectOnPlane(tank.forward, Vector3.up).normalized;
Vector3 right = Vector3.Cross(Vector3.up, fwd);
Vector3 P(float r, float u, float f) => t0 + right * r + Vector3.up * u + fwd * f;

// Town: centre of its renderers' bounds.
Vector3 town = t0 + fwd * 30f;
var townRoot = GameObject.Find("<Town>");
if (townRoot != null)
{
    var rs = townRoot.GetComponentsInChildren<Renderer>();
    if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); town = b.center; }
}
Vector3 townEye = town + Vector3.ProjectOnPlane(t0 - town, Vector3.up).normalized * 26f + Vector3.up * 11f;
Vector3 blast = Ground(P(14f, 0f, 10f));
Vector3 blastEye = blast - right * 11f + Vector3.up * 5f - fwd * 9f;
Vector3 crushAt = default, crushFwd = fwd;

// ---------- Rendering a shot ----------
var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
{
    antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
    name = "RenderShots"
};
rt.Create();
var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
var log = new List<string>();
var inv = System.Globalization.CultureInfo.InvariantCulture;

bool posed = false;
Vector3 posePos = default;
Quaternion poseRot = default;
int poseFrame = 0;

void Pose(Vector3 from, Vector3 lookAt)
{
    posePos = from;
    poseRot = Quaternion.LookRotation(lookAt - from, Vector3.up);
    poseFrame = Time.frameCount;
    posed = true;
    cam.transform.SetPositionAndRotation(posePos, poseRot);
}

void Capture(string name)
{
    RenderTexture savedTarget = cam.targetTexture;
    cam.transform.SetPositionAndRotation(posePos, poseRot);
    cam.targetTexture = rt;
    cam.Render();
    cam.targetTexture = savedTarget;
    posed = false;

    var prev = RenderTexture.active;
    RenderTexture.active = rt;
    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
    tex.Apply(false);
    RenderTexture.active = prev;
    File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
    log.Add(name + " t=" + Time.timeSinceLevelLoad.ToString("F1", inv));
}

// ---------- Script ----------
var drive = new float[2];
UnityEngine.Events.UnityAction onRender = () =>
{
    if (drive[0] != 0 || drive[1] != 0) driveInput.Value = new Vector2(drive[0], drive[1]);
    // After the camera rig's LateUpdate: the held pose wins.
    if (posed) cam.transform.SetPositionAndRotation(posePos, poseRot);
};
Application.onBeforeRender += onRender;

GameObject rain = null;
EditorApplication.CallbackFunction onUpdate = null;

void Finish(string status)
{
    EditorApplication.update -= onUpdate;
    Application.onBeforeRender -= onRender;
    drive[0] = drive[1] = 0;
    posed = false;
    for (int i = 0; i < strikes.Length; i++) if (strikes[i] != null) onField.SetValue(strikes[i], strikeWasOn[i]);
    if (rain != null) UnityEngine.Object.Destroy(rain);
    rt.Release();
    UnityEngine.Object.DestroyImmediate(rt);
    UnityEngine.Object.DestroyImmediate(tex);
    var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
    domain.SetData("shots_status", status + " | " + Application.unityVersion + " " + (rp == null ? "Built-in" : rp.name) + " " +
                                   QualitySettings.names[QualitySettings.GetQualityLevel()] + " | " + string.Join("; ", log));
}

// Steps: wait this many seconds after the previous one, then act. A capture (wait -1) runs once
// the pose before it has been held for HoldFrames frames.
var steps = new List<(float wait, Action act)>
{
    (0f, () => Pose(P(-12f, 22f, -8f), P(0f, 0f, 22f))),
    (-1f, () => Capture("01_overview")),
    (0f, () => Pose(P(4.5f, 2.2f, 5.5f), P(0f, 1.0f, 0f))),
    (-1f, () => Capture("02_tank_close")),
    (0f, () => Pose(townEye, town)),
    (-1f, () => Capture("03_town")),
    (0f, () => { drive[0] = -0.6f; drive[1] = 1f; }),             // dig ruts: off the road into the mud, 3 s
    (3f, () => { drive[0] = 0f; drive[1] = 0f; }),
    (2.5f, () => Pose(P(1.5f, 4.5f, -3.5f), P(-3f, 0f, 5f))),
    (-1f, () => Capture("04_mud_ruts")),
    (0f, () =>
    {
        // Crush: the nearest Regax goes 8 m in front of the tank, which drives at it.
        GameObject regax = null; float best = float.MaxValue;
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.name.StartsWith("Regax") || !go.activeInHierarchy) continue;
            float d = (go.transform.position - tank.position).sqrMagnitude;
            if (d < best) { best = d; regax = go; }
        }
        Vector3 f = Vector3.ProjectOnPlane(tank.forward, Vector3.up).normalized;
        crushAt = Ground(tank.position + f * 8f);
        crushFwd = f;
        if (regax != null)
        {
            var rb = regax.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = crushAt + Vector3.up * 0.1f; rb.linearVelocity = Vector3.zero; }
            regax.transform.position = crushAt + Vector3.up * 0.1f;
        }
        else log.Add("no Regax");
        drive[1] = 1f;
    }),
    (2.5f, () => { drive[1] = -1f; }),                            // back off so the blood is in view
    (1.2f, () => { drive[1] = 0f; }),
    (1.5f, () => Pose(crushAt - crushFwd * 6f + Vector3.Cross(Vector3.up, crushFwd) * 5f + Vector3.up * 6f, crushAt)),
    (-1f, () => Capture("05_blood")),
    (0f, () => Pose(blastEye, blast + Vector3.up * 1.5f)),
    (-1f, () => { if (explode != null && strikes.Length > 0) explode.Invoke(strikes[0], new object[] { blast }); else log.Add("no air strike"); posed = true; }),
    (0.15f, () => Capture("06_blast_fire")),
    (0f, () => Pose(blastEye, blast + Vector3.up * 1.5f)),
    (1.5f, () => Capture("07_blast_smoke")),
    (0f, () => { rain = new GameObject("RenderShots Rain"); rain.AddComponent<Game.Voxels.VoxelRain>(); }),
    (3f, () => Pose(P(-3f, 5f, -6f), P(0f, 0.5f, 16f))),
    (-1f, () => Capture("08_rain")),
};

int step = 0;
float stepStart = -1f;
onUpdate = () =>
{
    try
    {
        if (!EditorApplication.isPlaying) { Finish("failed: left Play"); return; }
        if (EditorApplication.isPaused) return;
        // Burst warm-up and settled physics.
        if (Time.timeSinceLevelLoad < 12f) { domain.SetData("shots_status", "running: warm-up " + Time.timeSinceLevelLoad.ToString("F1", inv)); return; }
        if (stepStart < 0f) stepStart = Time.time;
        while (step < steps.Count)
        {
            float wait = steps[step].wait;
            bool ready = wait < 0f ? Time.frameCount - poseFrame >= HoldFrames : Time.time - stepStart >= wait;
            if (!ready) break;
            steps[step].act();
            step++;
            stepStart = Time.time;
            // One action per frame once anything waits, so poses get rendered between steps.
            if (step < steps.Count && steps[step].wait != 0f) break;
        }
        domain.SetData("shots_status", "running: step " + step + "/" + steps.Count);
        if (step >= steps.Count) Finish("done");
    }
    catch (Exception e)
    {
        log.Add("exception: " + e.GetBaseException().Message);
        Finish("failed");
    }
};
EditorApplication.update += onUpdate;
domain.SetData("shots_update", onUpdate);
domain.SetData("shots_render", onRender);
domain.SetData("shots_status", "running: armed");
return "armed: " + steps.Count + " steps into " + Path.GetFullPath(dir) + "; tank " + t0 + " fwd " + fwd + " town " + town + " blast " + blast;

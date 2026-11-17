// VoxelSandbox benchmark (Docs/Rules/Performance.md, sections 3-4).
// Run in Play, in VoxelSandbox, with one call:
//   unity cmd --port <port> script eval --file Tools/PerfCheck/Bench/perf_bench.cs
// It returns at once and plays the scenarios over the next ~40 s from EditorApplication.update and
// Application.onBeforeRender. Read the result with perf_bench_read.cs. Leaving Play aborts the run.
// Optional: set AppDomain data "perfbench_only" to a comma list of scenarios before the call.
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

var domain = AppDomain.CurrentDomain;
if (!EditorApplication.isPlaying) return "ERROR not in Play";
string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
if (sceneName != "VoxelSandbox") return "ERROR active scene is " + sceneName + ", not VoxelSandbox";
if (domain.GetData("perfbench_status") is string st && st.StartsWith("running")) return "ERROR a run is already going: " + st;

// ---------- Cleanup of a previous run ----------
if (domain.GetData("perfbench_update") is EditorApplication.CallbackFunction oldU) EditorApplication.update -= oldU;
if (domain.GetData("perfbench_render") is UnityEngine.Events.UnityAction oldR) Application.onBeforeRender -= oldR;

// ---------- Scene objects ----------
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
if (!(bool)FindExt("TryGetDriveInput").Invoke(null, dargs)) return "ERROR the player is not in the tank (no DriveInput)";
var driveInput = (Atomic.Elements.IReactiveVariable<Vector2>)dargs[1];

var start = UnityEngine.Object.FindFirstObjectByType<Game.VoxelSandboxStart>();
Transform tank = start != null && start.Vehicle != null ? start.Vehicle.transform : GameObject.Find("Tank_01")?.transform;
if (tank == null) return "ERROR no tank";
Rigidbody tankBody = tank.GetComponent<Rigidbody>();

var strikes = UnityEngine.Object.FindObjectsByType<Game.VoxelAirStrike>(FindObjectsSortMode.None);
FieldInfo onField = typeof(Game.VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
MethodInfo explode = typeof(Game.VoxelAirStrike).GetMethod("Explode", BindingFlags.NonPublic | BindingFlags.Instance);
var strikeWasOn = new bool[strikes.Length];
for (int i = 0; i < strikes.Length; i++) { strikeWasOn[i] = (bool)onField.GetValue(strikes[i]); onField.SetValue(strikes[i], false); }

// ---------- Profiler ----------
bool profWasEnabled = ProfilerDriver.enabled;
bool profWasEditor = ProfilerDriver.profileEditor;
ProfilerDriver.profileEditor = false;
ProfilerDriver.enabled = true;
string frameCountNote = "default";
try
{
    var pus = typeof(ProfilerDriver).Assembly.GetType("UnityEditor.Profiling.ProfilerUserSettings");
    var fc = pus?.GetProperty("frameCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    if (fc != null) { int before = (int)fc.GetValue(null); if (before < 2000) fc.SetValue(null, 2000); frameCountNote = before + "->" + fc.GetValue(null); }
}
catch (Exception e) { frameCountNote = "n/a " + e.GetBaseException().Message; }

// ---------- Scenarios ----------
string only = domain.GetData("perfbench_only") as string;
var plan = new List<(string name, float seconds)> { ("idle", 4f), ("drive", 8f), ("airstrike", 6.5f), ("crush", 4f) };
if (!string.IsNullOrEmpty(only)) plan.RemoveAll(p => !("," + only + ",").Contains("," + p.name + ","));

var counterNames = new (ProfilerCategory cat, string name, string key)[]
{
    (ProfilerCategory.Internal, "GPU Frame Time", "gpu"),
    (ProfilerCategory.Render, "Batches Count", "batches"),
    (ProfilerCategory.Render, "SetPass Calls Count", "setpass"),
    (ProfilerCategory.Render, "Triangles Count", "triangles"),
    (ProfilerCategory.Render, "Shadow Casters Count", "shadowCasters"),
};
var recorders = new List<ProfilerRecorder>();

// Per-frame CPU data, from the profiler hierarchy (frames the bench itself spent time in are skipped).
var skipFrames = new HashSet<int>();
var rows = new List<double[]>(); // main, playerLoop, gcBytes, voxels, physics, scripts, render
var worst = new List<(double ms, int frame, string top)>();
int readUpTo = -1;
var children = new List<int>();
var stack = new Stack<(int id, int depth)>();

bool IsVoxel(string n) => n.Contains("Voxel") && !n.Contains("VoxelAirStrike") && !n.Contains("VoxelSandbox") && !n.Contains("VoxelDemo");

void ReadFrames(int upTo)
{
    int first = Math.Max(readUpTo + 1, ProfilerDriver.firstFrameIndex);
    for (int f = first; f <= upTo; f++)
    {
        if (skipFrames.Contains(f)) continue;
        using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
        if (view == null || !view.valid) continue;
        double pl = 0, gc = 0, vox = 0, phys = 0, scripts = 0, scriptVox = 0, render = 0;
        int plId = -1;
        children.Clear();
        view.GetItemChildren(view.GetRootItemID(), children);
        foreach (int c in children) if (view.GetItemName(c) == "PlayerLoop") { plId = c; break; }
        if (plId < 0) continue;
        pl = view.GetItemColumnDataAsDouble(plId, HierarchyFrameDataView.columnTotalTime);
        gc = view.GetItemColumnDataAsDouble(plId, HierarchyFrameDataView.columnGcMemory);
        stack.Clear();
        stack.Push((plId, 0));
        string topName = ""; double topMs = 0;
        while (stack.Count > 0)
        {
            var (id, d) = stack.Pop();
            children.Clear();
            view.GetItemChildren(id, children);
            foreach (int c in children)
            {
                string n = view.GetItemName(c);
                double t = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
                if (d == 0)
                {
                    if (n == "FixedUpdate.PhysicsFixedUpdate") phys += t;
                    else if (n == "PostLateUpdate.FinishFrameRendering") render += t;
                    else if (n.EndsWith("ScriptRunBehaviourUpdate") || n.EndsWith("ScriptRunBehaviourLateUpdate") || n.EndsWith("ScriptRunBehaviourFixedUpdate") || n.EndsWith("ScriptRunDelayedDynamicFrameRate")) scripts += t;
                }
                if (IsVoxel(n))
                {
                    vox += t;
                    // Voxel work that sits inside the script phases is taken out of "scripts" below.
                    scriptVox += d >= 1 ? t : 0;
                    if (t > topMs) { topMs = t; topName = n; }
                    continue;
                }
                if (d <= 4 && t > topMs && d >= 1) { topMs = t; topName = n; }
                stack.Push((c, d + 1));
            }
        }
        double main = view.frameTimeMs;
        rows.Add(new[] { main, pl, gc, vox, phys, Math.Max(0, scripts - scriptVox), render });
        worst.Add((pl, f, topName + " " + topMs.ToString("F2")));
        if (worst.Count > 3) { worst.Sort((a, b) => b.ms.CompareTo(a.ms)); worst.RemoveAt(3); }
    }
    readUpTo = Math.Max(readUpTo, upTo);
}

string Stats(int col, double scale = 1, string fmt = "F2")
{
    if (rows.Count == 0) return "\"n\":0";
    var v = new double[rows.Count];
    for (int i = 0; i < v.Length; i++) v[i] = rows[i][col] * scale;
    Array.Sort(v);
    double sum = 0; foreach (var x in v) sum += x;
    int over = 0; foreach (var x in v) if (x > 16.6) over++;
    string F(double x) => x.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    return $"\"avg\":{F(sum / v.Length)},\"p50\":{F(v[v.Length / 2])},\"p95\":{F(v[(int)(v.Length * 0.95)])},\"max\":{F(v[v.Length - 1])},\"over16\":{over}";
}

string CounterStats(ProfilerRecorder r, bool time)
{
    int n = r.Count;
    if (n == 0) return "null";
    double sum = 0, max = 0;
    for (int k = 0; k < n; k++) { double x = r.GetSample(k).Value; sum += x; if (x > max) max = x; }
    double s = time ? 1e-6 : 1;
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    return $"{{\"avg\":{(sum / n * s).ToString("F2", ci)},\"max\":{(max * s).ToString("F2", ci)}}}";
}

// ---------- Drive and events ----------
var drive = new float[2]; // steer, throttle; 0,0 = leave the input alone
UnityEngine.Events.UnityAction onRender = () =>
{
    if (drive[0] != 0 || drive[1] != 0) driveInput.Value = new Vector2(drive[0], drive[1]);
};
Application.onBeforeRender += onRender;

Vector3 Ground(Vector3 p)
{
    return Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
}

GameObject regax = null;
float regaxMin = float.MaxValue;
Vector3 regaxStart = default;

int phase = -1;
string sub = "warmup";
float phaseStart = 0;
int phaseFirstFrame = 0;
int blasts = 0;
var results = new List<string>();
var sbNotes = new List<string>();
var inv = System.Globalization.CultureInfo.InvariantCulture;
Vector3 tankStart = tank.position;
EditorApplication.CallbackFunction onUpdate = null;

void Finish(string status)
{
    EditorApplication.update -= onUpdate;
    Application.onBeforeRender -= onRender;
    drive[0] = drive[1] = 0;
    foreach (var r in recorders) r.Dispose();
    recorders.Clear();
    for (int i = 0; i < strikes.Length; i++) if (strikes[i] != null) onField.SetValue(strikes[i], strikeWasOn[i]);
    ProfilerDriver.enabled = profWasEnabled;
    ProfilerDriver.profileEditor = profWasEditor;
    string env = $"\"unity\":\"{Application.unityVersion}\",\"pipeline\":\"{(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name)}\"," +
                 $"\"quality\":\"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\"screen\":\"{Screen.width}x{Screen.height}\"," +
                 $"\"sceneViews\":{SceneView.sceneViews.Count},\"maximized\":{(EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.maximized ? "true" : "false")}," +
                 $"\"burst\":{(Unity.Burst.BurstCompiler.IsEnabled ? "true" : "false")},\"gpu\":\"{SystemInfo.graphicsDeviceName}\",\"cpu\":\"{SystemInfo.processorType.Trim()}\",\"profilerFrames\":\"{frameCountNote}\"";
    string notes = string.Join("; ", sbNotes).Replace("\"", "'");
    domain.SetData("perfbench_result", $"{{\"status\":\"{status}\",\"date\":\"{DateTime.Now:yyyy-MM-dd HH:mm}\",{env},\"notes\":\"{notes}\",\"scenarios\":{{{string.Join(",", results)}}}}}");
    domain.SetData("perfbench_status", status);
}

void BeginScenario()
{
    var (name, _) = plan[phase];
    rows.Clear(); worst.Clear();
    foreach (var r in recorders) r.Dispose();
    recorders.Clear();
    foreach (var c in counterNames) recorders.Add(ProfilerRecorder.StartNew(c.cat, c.name, 8000));
    readUpTo = ProfilerDriver.lastFrameIndex;
    phaseStart = Time.time;
    phaseFirstFrame = Time.frameCount;
    blasts = 0;
    drive[0] = drive[1] = 0;
    if (name == "drive") drive[1] = 1;
    if (name == "crush")
    {
        regax = null;
        float best = float.MaxValue;
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!go.name.StartsWith("Regax") || !go.activeInHierarchy) continue;
            float d = (go.transform.position - tank.position).sqrMagnitude;
            if (d < best) { best = d; regax = go; }
        }
        if (regax != null)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(tank.forward, Vector3.up).normalized;
            Vector3 p = Ground(tank.position + fwd * 8f) + Vector3.up * 0.1f;
            var rb = regax.GetComponent<Rigidbody>();
            if (rb != null) { rb.position = p; rb.linearVelocity = Vector3.zero; }
            regax.transform.position = p;
            regaxStart = p;
            regaxMin = float.MaxValue;
        }
        else sbNotes.Add("crush: no Regax in the scene");
        drive[1] = 1;
    }
    sub = "run";
}

void EndScenario()
{
    var (name, _) = plan[phase];
    drive[0] = drive[1] = 0;
    sub = "read";
    ReadFrames(ProfilerDriver.lastFrameIndex);
    var counters = new List<string>();
    for (int i = 0; i < counterNames.Length; i++) counters.Add($"\"{counterNames[i].key}\":{CounterStats(recorders[i], counterNames[i].key == "gpu")}");
    worst.Sort((a, b) => b.ms.CompareTo(a.ms));
    var w = new List<string>();
    foreach (var x in worst) w.Add($"\"{x.ms.ToString("F1", inv)} ms: {x.top.Replace("\"", "'")}\"");
    string extra = "";
    if (name == "crush" && regax != null) extra = $",\"regaxMinDistance\":{regaxMin.ToString("F2", inv)},\"regaxAlive\":{(regax != null && regax.activeInHierarchy ? "true" : "false")}";
    if (name == "airstrike") extra = $",\"blasts\":{blasts}";
    if (name == "drive" || name == "crush") extra += $",\"tankMoved\":{Vector3.Distance(tank.position, tankStart).ToString("F1", inv)}";
    results.Add($"\"{name}\":{{\"frames\":{rows.Count},\"seconds\":{(Time.time - phaseStart).ToString("F1", inv)}," +
                $"\"main\":{{{Stats(0)}}},\"playerLoop\":{{{Stats(1)}}},\"gcBytes\":{{{Stats(2, 1, "F0")}}}," +
                $"\"voxels\":{{{Stats(3)}}},\"physics\":{{{Stats(4)}}},\"scripts\":{{{Stats(5)}}},\"render\":{{{Stats(6)}}}," +
                $"{string.Join(",", counters)},\"worst\":[{string.Join(",", w)}]{extra}}}");
    tankStart = tank.position;
}

onUpdate = () =>
{
    try
    {
        if (!EditorApplication.isPlaying) { Finish("aborted: left Play"); return; }
        if (EditorApplication.isPaused) return;
        if (phase < 0)
        {
            // Burst warm-up: at least 8 s into the scene.
            if (Time.timeSinceLevelLoad < 8f) { domain.SetData("perfbench_status", "running: warm-up " + Time.timeSinceLevelLoad.ToString("F1", inv)); return; }
            phase = 0; sub = "settle"; phaseStart = Time.time;
        }
        if (phase >= plan.Count) { Finish("done"); return; }
        var (name, seconds) = plan[phase];
        domain.SetData("perfbench_status", $"running: {name} {sub} {(Time.time - phaseStart).ToString("F1", inv)}/{seconds}");
        if (sub == "settle") { if (Time.time - phaseStart >= 0.5f) BeginScenario(); return; }
        if (sub == "run")
        {
            float t = Time.time - phaseStart;
            if (name == "drive") drive[1] = ((int)(t / 3f)) % 2 == 0 ? 1f : -1f; // forward first: the map edge is behind
            if (name == "airstrike" && explode != null && strikes.Length > 0 && blasts < 6 && t >= 0.5f + blasts)
            {
                float a = blasts * 60f * Mathf.Deg2Rad;
                Vector3 p = Ground(tank.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (12f + (blasts % 3) * 2f));
                explode.Invoke(strikes[0], new object[] { p });
                blasts++;
            }
            if (name == "crush" && regax != null) regaxMin = Mathf.Min(regaxMin, Vector3.Distance(regax.transform.position, tank.position));
            // Read the profiler in chunks so its buffer never wraps; the frame doing it is skipped.
            if (ProfilerDriver.lastFrameIndex - readUpTo > 200)
            {
                ReadFrames(ProfilerDriver.lastFrameIndex - 2);
                skipFrames.Add(ProfilerDriver.lastFrameIndex + 1);
                skipFrames.Add(ProfilerDriver.lastFrameIndex + 2);
            }
            if (t >= seconds) { drive[0] = drive[1] = 0; sub = "tail"; phaseStart = Time.time; }
            return;
        }
        if (sub == "tail")
        {
            // Let the profiler hand over the last frames of the scenario.
            if (Time.time - phaseStart < 0.1f) return;
            EndScenario();
            phase++; sub = "settle"; phaseStart = Time.time;
        }
    }
    catch (Exception e)
    {
        sbNotes.Add("exception: " + e.GetBaseException().Message);
        Finish("failed");
    }
};
EditorApplication.update += onUpdate;
domain.SetData("perfbench_update", onUpdate);
domain.SetData("perfbench_render", onRender);
domain.SetData("perfbench_status", "running: armed");
domain.SetData("perfbench_result", null);
float total = 8f;
foreach (var p in plan) total += p.seconds + 0.6f;
return $"armed: {string.Join(",", plan.ConvertAll(p => p.name))}, ~{total:F0} s from scene start; strikes={strikes.Length} profilerFrames={frameCountNote}; read with perf_bench_read.cs";

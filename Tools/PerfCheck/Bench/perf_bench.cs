// VoxelSandbox benchmark (Docs/Rules/Performance.md, sections 3-4).
// Run in Play, in VoxelSandbox, with one call:
//   unity cmd --port <port> script eval --file Tools/PerfCheck/Bench/perf_bench.cs
// It returns at once and plays the scenarios over the next ~50 s from EditorApplication.update and
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
// The tank's DriveInput: on the sandbox vehicle itself (the player drives the tank directly since the
// cleanup); before it, on the player context's character.
var start = UnityEngine.Object.FindFirstObjectByType<Game.VoxelSandboxStart>();
object driver = start != null && start.Vehicle != null ? start.Vehicle : null;
if (driver == null) driver = GameObject.Find("Tank_01")?.GetComponent<Atomic.Entities.MonoEntity>();
if (driver == null)
{
    object ctx = FindExt("GetPlayerContext").Invoke(null, new object[] { Game.GameContext.Instance });
    object charVar = FindExt("GetCharacter").Invoke(null, new object[] { ctx });
    driver = charVar.GetType().GetProperty("Value").GetValue(charVar);
}
var dargs = new object[] { driver, null };
if (!(bool)FindExt("TryGetDriveInput").Invoke(null, dargs)) return "ERROR the tank has no DriveInput";
var driveInput = (Atomic.Elements.IReactiveVariable<Vector2>)dargs[1];
// The run (core, milestone 1) also runs in the sandbox: a level-up holds the game loop paused until a skill is
// picked, and the tank stands. The bench picks at once, as combat_bench.cs: lowest level first, ties by name.
MethodInfo getRun = FindExt("GetRun");
var runSvc = getRun != null ? getRun.Invoke(null, new object[] { Game.GameContext.Instance }) as Game.IRunService : null;
var skillPicks = new List<string>();
void TendSkills()
{
    if (runSvc == null || runSvc.PendingLevelUps <= 0 || runSvc.SkillOffer.Count == 0) return;
    Game.SkillId pick = runSvc.SkillOffer[0];
    for (int i = 1; i < runSvc.SkillOffer.Count; i++)
    {
        var s = runSvc.SkillOffer[i];
        int a = runSvc.GetSkillLevel(s), b = runSvc.GetSkillLevel(pick);
        if (a < b || (a == b && string.CompareOrdinal(s.ToString(), pick.ToString()) < 0)) pick = s;
    }
    if (runSvc.ChooseSkill(pick)) skillPicks.Add(pick.ToString());
}
var bargs = new object[] { driver, null };
MethodInfo boostExt = FindExt("TryGetDriveBoost");
var driveBoost = boostExt != null && (bool)boostExt.Invoke(null, bargs) ? (Atomic.Elements.IReactiveVariable<bool>)bargs[1] : null;

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
var plan = new List<(string name, float seconds)> { ("idle", 4f), ("drive", 8f), ("airstrike", 6.5f), ("crush", 4f), ("mg", 7f), ("house", 6.5f), ("crater", 5f) };
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
// Columns: 0 main, 1 playerLoop, 2 gc (game), 3 voxels, 4 physics, 5 scripts, 6 render, 7 gc editor-only (Burst JIT), 8 gc debug HUD (IMGUI),
// 9 plough bite ms, 10 bite calls, 11 bite gc (marker TankPlough.Bite, inclusive).
var skipFrames = new HashSet<int>();
var rows = new List<double[]>();
var worst = new List<(double ms, int frame, string top)>();
int readUpTo = -1;
var children = new List<int>();
var stack = new Stack<(int id, int depth, bool inVox)>();

bool IsVoxel(string n) => n.Contains("Voxel") && !n.Contains("VoxelAirStrike") && !n.Contains("VoxelSandbox") && !n.Contains("VoxelDemo");
bool IsEditorOnly(string n) => n.Contains("Burst.Compiler") || n.Contains("JitCompilerService");
bool IsDebugHud(string n) => n == "GUI.Repaint" || n.Contains("VoxelDemoHud") || n.Contains("VoxelSandboxOrbit.OnGUI");
// Categories are matched by name wherever they sit under PlayerLoop (Unity moves markers between
// versions: 6.0 has them right under PlayerLoop, 6.3 may nest them). The first match on a path wins.
bool IsRender(string n) => n == "Camera.Render" || n.Contains("RenderCameraStack") || n.Contains("DoRenderLoop") || n.Contains("FinishFrameRendering") || n.Contains("UpdateAllRenderers");
bool IsPhysics(string n) => n.Contains("PhysicsFixedUpdate") || n == "Physics.Simulate" || n.StartsWith("Physics.Processing") || n.StartsWith("FixedUpdate.Physics");
bool IsScripts(string n) => n.Contains("ScriptRunBehaviour") || n.Contains("ScriptRunDelayedDynamicFrameRate");
string Short(string n)
{
    int k = n.LastIndexOf("::", StringComparison.Ordinal);
    if (k >= 0) n = n.Substring(k + 2);
    return n.Replace(" [Invoke]", "").Replace("\"", "'");
}

string HotPath(HierarchyFrameDataView view, int id)
{
    var parts = new List<string>();
    double parentT = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnTotalTime);
    var kids = new List<int>();
    for (int depth = 0; depth < 12; depth++)
    {
        kids.Clear();
        view.GetItemChildren(id, kids);
        int best = -1; double bestT = 0;
        foreach (int c in kids) { double t = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime); if (t > bestT) { bestT = t; best = c; } }
        if (best < 0 || bestT < 0.4 * parentT || bestT < 0.3) break;
        parts.Add(Short(view.GetItemName(best)) + " " + bestT.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        id = best; parentT = bestT;
    }
    return string.Join(" > ", parts);
}

// Where the game's garbage comes from: every GC.Alloc under PlayerLoop (editor-only and debug HUD left out),
// keyed by the last 4 markers above it. Per scenario: frames, bytes, worst frame.
var gcSrc = new Dictionary<string, double[]>();
var gcStack = new Stack<(int id, string path)>();
var gcKids = new List<int>();
void AddGcSources(HierarchyFrameDataView view, int plId)
{
    var frame = new Dictionary<string, double>();
    gcStack.Clear();
    gcStack.Push((plId, ""));
    while (gcStack.Count > 0)
    {
        var (id, path) = gcStack.Pop();
        gcKids.Clear();
        view.GetItemChildren(id, gcKids);
        foreach (int c in gcKids)
        {
            if (view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory) <= 0) continue;
            string n = view.GetItemName(c);
            if (IsEditorOnly(n) || IsDebugHud(n)) continue;
            if (n == "GC.Alloc")
            {
                double b = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory);
                frame.TryGetValue(path, out double cur);
                frame[path] = cur + b;
                continue;
            }
            string np = path.Length == 0 ? Short(n) : path + " > " + Short(n);
            int cut = 0;
            for (int q = 0, k = np.Length - 1; k >= 0; k--) if (np[k] == '>' && ++q == 4) { cut = k + 2; break; }
            gcStack.Push((c, cut > 0 ? np.Substring(cut) : np));
        }
    }
    foreach (var kv in frame)
    {
        if (!gcSrc.TryGetValue(kv.Key, out var a)) gcSrc[kv.Key] = a = new double[3];
        a[0]++; a[1] += kv.Value; a[2] = Math.Max(a[2], kv.Value);
    }
}
string GcSourcesJson()
{
    var e = new List<KeyValuePair<string, double[]>>(gcSrc);
    e.Sort((a, b) => b.Value[1].CompareTo(a.Value[1]));
    var l = new List<string>();
    for (int i = 0; i < e.Count && i < 8; i++)
        l.Add($"\"{e[i].Value[0]}f {e[i].Value[1]:F0}B max {e[i].Value[2]:F0}B: {e[i].Key.Replace("\"", "'")}\"");
    return "[" + string.Join(",", l) + "]";
}

var stack3 = new Stack<(int id, int depth, bool inVox, int cat)>();
var catSeen = new int[4]; // frames in which physics(1), render(2), scripts(3) were found
void ReadFrames(int upTo)
{
    int first = Math.Max(readUpTo + 1, ProfilerDriver.firstFrameIndex);
    for (int f = first; f <= upTo; f++)
    {
        if (skipFrames.Contains(f)) continue;
        using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
        if (view == null || !view.valid) continue;
        int plId = -1;
        children.Clear();
        view.GetItemChildren(view.GetRootItemID(), children);
        foreach (int c in children) if (view.GetItemName(c) == "PlayerLoop") { plId = c; break; }
        if (plId < 0) continue;
        double pl = view.GetItemColumnDataAsDouble(plId, HierarchyFrameDataView.columnTotalTime);
        double gc = view.GetItemColumnDataAsDouble(plId, HierarchyFrameDataView.columnGcMemory);
        double vox = 0, phys = 0, scripts = 0, scriptVox = 0, render = 0, gcEditor = 0, gcHud = 0, bite = 0, biteCalls = 0, biteGc = 0;
        bool seenP = false, seenR = false, seenS = false;
        stack3.Clear();
        stack3.Push((plId, 0, false, 0));
        while (stack3.Count > 0)
        {
            var (id, d, inVox, cat) = stack3.Pop();
            children.Clear();
            view.GetItemChildren(id, children);
            foreach (int c in children)
            {
                string n = view.GetItemName(c);
                if (IsEditorOnly(n)) { gcEditor += view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory); continue; }
                if (IsDebugHud(n)) { gcHud += view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory); continue; }
                double t = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
                if (n == "TankPlough.Bite")
                {
                    bite += t;
                    biteCalls += view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnCalls);
                    biteGc += view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory);
                }
                int cc = cat;
                if (cat == 0)
                {
                    if (IsPhysics(n)) { phys += t; cc = 1; seenP = true; }
                    else if (IsRender(n)) { render += t; cc = 2; seenR = true; }
                    else if (IsScripts(n)) { scripts += t; cc = 3; seenS = true; }
                }
                bool vx = inVox;
                if (!inVox && IsVoxel(n))
                {
                    vox += t;
                    // Voxel work inside the script phases is taken out of "scripts".
                    if (cc == 3) scriptVox += t;
                    vx = true;
                }
                stack3.Push((c, d + 1, vx, cc));
            }
        }
        if (seenP) catSeen[1]++;
        if (seenR) catSeen[2]++;
        if (seenS) catSeen[3]++;
        rows.Add(new[] { view.frameTimeMs, pl, Math.Max(0, gc - gcEditor - gcHud), vox, phys, Math.Max(0, scripts - scriptVox), render, gcEditor, gcHud, bite, biteCalls, biteGc });
        if (gc - gcEditor - gcHud > 0) AddGcSources(view, plId);
        if (worst.Count < 3 || pl > worst[worst.Count - 1].ms)
        {
            worst.Add((pl, f, HotPath(view, plId)));
            worst.Sort((a, b) => b.ms.CompareTo(a.ms));
            if (worst.Count > 3) worst.RemoveAt(3);
        }
    }
    readUpTo = Math.Max(readUpTo, upTo);
}

// "of" is a subset of the rows (the crater's bite frames); all rows by default.
string Stats(int col, double scale = 1, string fmt = "F2", bool frames = true, List<double[]> of = null)
{
    of ??= rows;
    if (of.Count == 0) return "\"n\":0";
    var v = new double[of.Count];
    for (int i = 0; i < v.Length; i++) v[i] = of[i][col] * scale;
    Array.Sort(v);
    double sum = 0; foreach (var x in v) sum += x;
    int over = 0; foreach (var x in v) if (x > 16.6) over++;
    string F(double x) => x.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    string s = $"\"avg\":{F(sum / v.Length)},\"p50\":{F(v[v.Length / 2])},\"p95\":{F(v[(int)(v.Length * 0.95)])},\"max\":{F(v[v.Length - 1])}";
    if (frames) s += $",\"over16\":{over}";
    else { int nz = 0; foreach (var x in v) if (x > 0) nz++; s += $",\"framesWithAlloc\":{nz}"; }
    return s;
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
// A blast is queued from EditorApplication.update and set off here, inside the player loop, so its
// main-thread cost lands in PlayerLoop like a real bomb's (from EditorApplication.update it did not).
var pendingBlast = new List<Vector3>();
bool boostOn = false;
// The crater scenario's own crater: the blast of SandboxCraterPloughTest, the hole without a debris storm.
bool craterBlastPending = false;
Vector3 craterAt = default;
// The house scenario: the building nearest the tank, bombed at its four corners, a second apart.
Game.Voxels.VoxelBody houseBody = null;
int houseBlasts = 0;
int houseStart = 0;
// The mg scenario: the roof machine gun fires bursts at the nearest house's wall (CombatFocus, as with the right
// button held), and the cannon fires every 1.5 s, so the bullets fly through its muzzle smoke.
bool mgOn = false;
Vector3 mgAim = default;
bool cannonPending = false;
int cannonShots = 0;
Game.Voxels.VoxelBody mgHouse = null;
int mgHouseStart = 0;
var fargs = new object[] { driver, null };
MethodInfo fireExt = FindExt("TryGetFireAction");
var cannonFire = fireExt != null && (bool)fireExt.Invoke(null, fargs) ? fargs[1] as Atomic.Elements.IAction : null;
var cargs = new object[] { driver, null };
MethodInfo condExt = FindExt("TryGetFireCondition");
var cannonReady = condExt != null && (bool)condExt.Invoke(null, cargs) ? cargs[1] as Atomic.Elements.IExpression<bool> : null;
UnityEngine.Events.UnityAction onRender = () =>
{
    if (drive[0] != 0 || drive[1] != 0) driveInput.Value = new Vector2(drive[0], drive[1]);
    if (mgOn) Game.CombatFocus.Set(mgAim);
    if (cannonPending && cannonFire != null && (cannonReady == null || cannonReady.Invoke())) { cannonPending = false; cannonFire.Invoke(); cannonShots++; }
    if (driveBoost != null && driveBoost.Value != boostOn) driveBoost.Value = boostOn;
    if (craterBlastPending) { craterBlastPending = false; Game.Voxels.VoxelDamage.AirBlast(craterAt, 4f, 32f, 24f, 0, 0.85f, 24f); }
    if (pendingBlast.Count > 0 && explode != null && strikes.Length > 0 && strikes[0] != null)
    {
        foreach (var p in pendingBlast) explode.Invoke(strikes[0], new object[] { p });
        pendingBlast.Clear();
    }
};
Application.onBeforeRender += onRender;

Vector3 Ground(Vector3 p)
{
    return Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
}

GameObject regax = null;
float regaxMin = float.MaxValue;
Vector3 regaxStart = default;

// Crater: a straight run nearest the origin, as SandboxCraterPloughTest.TryFindRunway; the crater 18 m on
// (14 m run-up + 4 m rim); the tank from the start at full throttle with boost.
const int GROUND = 1 << Game.PhysicsLayers.Ground;
bool TryFindRunway(out Vector3 from0, out Vector3 way0)
{
    from0 = default; way0 = Vector3.forward;
    Vector3[] ways = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
    float bestR = float.MaxValue;
    for (float x = -30f; x <= 30f; x += 3f)
    for (float z = -30f; z <= 30f; z += 3f)
    foreach (Vector3 way in ways)
    {
        var from = new Vector3(x, 0f, z);
        if (Physics.CheckBox(from + way * 15f + Vector3.up * 2f, new Vector3(3f, 1.5f, 16f), Quaternion.LookRotation(way), ~GROUND, QueryTriggerInteraction.Ignore)) continue;
        if (!Physics.Raycast(from + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, GROUND)) continue;
        if (!Physics.Raycast(from + way * 28f + Vector3.up * 5f, Vector3.down, out RaycastHit end, 10f, GROUND) || Mathf.Abs(end.point.y - hit.point.y) > 0.5f) continue;
        if (from.sqrMagnitude < bestR) { bestR = from.sqrMagnitude; from0 = hit.point; way0 = way; }
    }
    return bestR < float.MaxValue;
}
var tankInstaller = tank.GetComponent<Game.TankInstaller>();
Vector3 craterStart = default, craterFwd = Vector3.forward;
string craterStep = "";
int ploughEvents = 0, impactsBefore = 0;
float entrySpeed = -1f, minFarSpeed = float.MaxValue, maxAlong = float.MinValue, entryTime = -1f, exitTime = -1f;
Action<Vector3, float> onPloughed = (pt, strength) => ploughEvents++;

int phase = -1;
string sub = "warmup";
float phaseStart = 0;
float runStart = 0;
int phaseFirstFrame = 0;
int blasts = 0;
var results = new List<string>();
var sbNotes = new List<string>();
var inv = System.Globalization.CultureInfo.InvariantCulture;
Vector3 tankStart = tank.position;
Vector3 sceneStart = tank.position;
string mgStep = "";
EditorApplication.CallbackFunction onUpdate = null;

void Finish(string status)
{
    EditorApplication.update -= onUpdate;
    Application.onBeforeRender -= onRender;
    Game.TankPloughEvents.Ploughed -= onPloughed;
    drive[0] = drive[1] = 0;
    if (driveBoost != null) driveBoost.Value = false;
    mgOn = false; cannonPending = false;
    Game.CombatFocus.Clear();
    foreach (var r in recorders) r.Dispose();
    recorders.Clear();
    for (int i = 0; i < strikes.Length; i++) if (strikes[i] != null) onField.SetValue(strikes[i], strikeWasOn[i]);
    ProfilerDriver.enabled = profWasEnabled;
    ProfilerDriver.profileEditor = profWasEditor;
    string env = $"\"unity\":\"{Application.unityVersion}\",\"pipeline\":\"{(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name)}\"," +
                 $"\"quality\":\"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\"screen\":\"{Screen.width}x{Screen.height}\"," +
                 $"\"sceneViews\":{SceneView.sceneViews.Count},\"maximized\":{(EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.maximized ? "true" : "false")}," +
                 $"\"burst\":{(Unity.Burst.BurstCompiler.IsEnabled ? "true" : "false")},\"gpu\":\"{SystemInfo.graphicsDeviceName}\",\"cpu\":\"{SystemInfo.processorType.Trim()}\",\"profilerFrames\":\"{frameCountNote}\"";
    if (skillPicks.Count > 0) sbNotes.Add("skills picked: " + string.Join(",", skillPicks));
    string notes = string.Join("; ", sbNotes).Replace("\"", "'");
    domain.SetData("perfbench_result", $"{{\"status\":\"{status}\",\"date\":\"{DateTime.Now:yyyy-MM-dd HH:mm}\",{env},\"notes\":\"{notes}\",\"scenarios\":{{{string.Join(",", results)}}}}}");
    domain.SetData("perfbench_status", status);
}

void BeginScenario()
{
    var (name, _) = plan[phase];
    rows.Clear(); worst.Clear(); gcSrc.Clear(); Array.Clear(catSeen, 0, catSeen.Length);
    foreach (var r in recorders) r.Dispose();
    recorders.Clear();
    foreach (var c in counterNames) recorders.Add(ProfilerRecorder.StartNew(c.cat, c.name, 8000));
    readUpTo = ProfilerDriver.lastFrameIndex;
    phaseStart = Time.time;
    runStart = Time.time;
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
            // Only the plain Regax that stand still; the elite ones walk and fight.
            if (!go.name.StartsWith("Regax") || go.name.StartsWith("Regax_Elite") || !go.activeInHierarchy) continue;
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
    if (name == "crater")
    {
        ploughEvents = 0;
        impactsBefore = tankInstaller != null && tankInstaller.Plough != null ? tankInstaller.Plough.Impacts : 0;
        entrySpeed = -1f; minFarSpeed = float.MaxValue; maxAlong = float.MinValue; entryTime = exitTime = -1f;
        Game.TankPloughEvents.Ploughed -= onPloughed;
        Game.TankPloughEvents.Ploughed += onPloughed;
        drive[1] = 1;
        boostOn = true;
    }
    sub = "run";
}

void EndScenario()
{
    var (name, _) = plan[phase];
    drive[0] = drive[1] = 0;
    boostOn = false;
    Game.TankPloughEvents.Ploughed -= onPloughed;
    sub = "read";
    ReadFrames(ProfilerDriver.lastFrameIndex);
    string[] catNames = { "", "physics", "render", "scripts" };
    for (int k = 1; k < 4; k++) if (rows.Count > 0 && catSeen[k] == 0) sbNotes.Add(name + ": no " + catNames[k] + " marker found, its column is 0");
    var counters = new List<string>();
    for (int i = 0; i < counterNames.Length; i++) counters.Add($"\"{counterNames[i].key}\":{CounterStats(recorders[i], counterNames[i].key == "gpu")}");
    worst.Sort((a, b) => b.ms.CompareTo(a.ms));
    var w = new List<string>();
    foreach (var x in worst) w.Add($"\"{x.ms.ToString("F1", inv)} ms: {x.top.Replace("\"", "'")}\"");
    string extra = "";
    if (name == "crush" && regax != null) extra = $",\"regaxMinDistance\":{regaxMin.ToString("F2", inv)},\"regaxAlive\":{(regax != null && regax.activeInHierarchy ? "true" : "false")}";
    if (name == "airstrike") extra = $",\"blasts\":{blasts}";
    if (name == "mg") extra = $",\"cannonShots\":{cannonShots},\"houseVoxels\":{mgHouseStart},\"houseLost\":{(mgHouse != null ? mgHouseStart - mgHouse.SolidCount : -1)}";
    if (name == "house") extra =$",\"houseBlasts\":{houseBlasts},\"houseVoxels\":{houseStart},\"houseLeft\":{(houseBody != null ? houseBody.SolidCount : 0)}";
    if (name == "drive" || name == "crush") extra += $",\"tankMoved\":{Vector3.Distance(tank.position, tankStart).ToString("F1", inv)}";
    if (name == "crater")
    {
        // Bite figures over the frames the plough bit in only.
        var biteRows = rows.FindAll(r => r[10] > 0);
        string preset = tankInstaller != null && tankInstaller.PloughPreset != null ? tankInstaller.PloughPreset.DisplayName : "none";
        int impacts = tankInstaller != null && tankInstaller.Plough != null ? tankInstaller.Plough.Impacts - impactsBefore : -1;
        extra = $",\"preset\":\"{preset}\",\"ploughEvents\":{ploughEvents},\"impacts\":{impacts},\"entrySpeed\":{entrySpeed.ToString("F2", inv)}," +
                $"\"minFarSpeed\":{(minFarSpeed == float.MaxValue ? -1f : minFarSpeed).ToString("F2", inv)},\"outIn\":{(exitTime >= 0 ? exitTime - entryTime : -1f).ToString("F2", inv)},\"maxAlong\":{maxAlong.ToString("F1", inv)}," +
                $"\"biteFrames\":{biteRows.Count},\"biteMs\":{{{Stats(9, 1, "F3", true, biteRows)}}},\"biteCalls\":{{{Stats(10, 1, "F0", false, biteRows)}}},\"biteGc\":{{{Stats(11, 1, "F0", false, biteRows)}}}," +
                $"\"biteFramePlayerLoop\":{{{Stats(1, 1, "F2", true, biteRows)}}},\"biteFrameGc\":{{{Stats(2, 1, "F0", false, biteRows)}}}";
        if (biteRows.Count == 0) sbNotes.Add("crater: the plough never bit");
    }
    results.Add($"\"{name}\":{{\"frames\":{rows.Count},\"seconds\":{(Time.time - runStart).ToString("F1", inv)}," +
                $"\"main\":{{{Stats(0)}}},\"playerLoop\":{{{Stats(1)}}},\"gcBytes\":{{{Stats(2, 1, "F0", false)}}},\"gcEditorOnly\":{{{Stats(7, 1, "F0", false)}}},\"gcDebugHud\":{{{Stats(8, 1, "F0", false)}}}," +
                $"\"voxels\":{{{Stats(3)}}},\"physics\":{{{Stats(4)}}},\"scripts\":{{{Stats(5)}}},\"render\":{{{Stats(6)}}}," +
                $"{string.Join(",", counters)},\"worst\":[{string.Join(",", w)}],\"gcSources\":{GcSourcesJson()}{extra}}}");
    tankStart = tank.position;
}

onUpdate = () =>
{
    try
    {
        if (!EditorApplication.isPlaying) { Finish("aborted: left Play"); return; }
        if (EditorApplication.isPaused) return;
        TendSkills();
        if (phase < 0)
        {
            // Burst warm-up: at least 10 s into the scene.
            if (Time.timeSinceLevelLoad < 10f) { domain.SetData("perfbench_status", "running: warm-up " + Time.timeSinceLevelLoad.ToString("F1", inv)); return; }
            phase = 0; sub = "settle"; phaseStart = Time.time;
        }
        if (phase >= plan.Count) { Finish("done"); return; }
        var (name, seconds) = plan[phase];
        domain.SetData("perfbench_status", $"running: {name} {sub} {(Time.time - phaseStart).ToString("F1", inv)}/{seconds}");
        if (sub == "settle" && name == "mg")
        {
            // Not measured: the building nearest the tank's start in the scene, the tank 10 m off its near wall,
            // facing it, so every run shoots the same wall from the same place.
            float s = Time.time - phaseStart;
            if (mgStep == "")
            {
                float best = float.MaxValue;
                foreach (var st2 in UnityEngine.Object.FindObjectsByType<Game.Voxels.VoxelStructure>(FindObjectsSortMode.None))
                {
                    var body = st2.GetComponentInChildren<Game.Voxels.VoxelBody>();
                    if (!st2.IsBuilding || body == null) continue;
                    float dd = Vector3.Distance(body.WorldBounds.center, sceneStart);
                    if (dd < best) { best = dd; mgHouse = body; }
                }
                if (mgHouse != null)
                {
                    Bounds hb = mgHouse.WorldBounds;
                    Vector3 near = hb.ClosestPoint(sceneStart);
                    Vector3 away = sceneStart - near; away.y = 0;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.left;
                    Vector3 at = Ground(near + away * 10f) + Vector3.up * 0.4f;
                    tankBody.linearVelocity = Vector3.zero; tankBody.angularVelocity = Vector3.zero;
                    tankBody.position = at; tankBody.rotation = Quaternion.LookRotation(-away);
                    tank.SetPositionAndRotation(at, tankBody.rotation);
                    tankStart = at;
                }
                mgStep = "place";
                return;
            }
            if (s >= 1.5f) { mgStep = ""; BeginScenario(); }
            return;
        }
        if (sub == "settle" && name == "crater")
        {
            // Not measured: find the run, blast the crater, give its collider a moment, put the tank at the start.
            float s = Time.time - phaseStart;
            if (craterStep == "")
            {
                if (!TryFindRunway(out craterStart, out craterFwd)) { sbNotes.Add("crater: no open run for a crater"); phase++; phaseStart = Time.time; return; }
                if (tankInstaller == null || tankInstaller.Plough == null) sbNotes.Add("crater: the tank has no plough");
                craterAt = craterStart + craterFwd * 18f;
                craterBlastPending = true;
                craterStep = "blast";
                return;
            }
            if (craterStep == "blast" && s >= 0.5f)
            {
                tankBody.linearVelocity = Vector3.zero; tankBody.angularVelocity = Vector3.zero;
                tankBody.position = craterStart + Vector3.up * 0.4f; tankBody.rotation = Quaternion.LookRotation(craterFwd);
                tank.SetPositionAndRotation(tankBody.position, tankBody.rotation);
                tankStart = tankBody.position;
                craterStep = "place";
                return;
            }
            if (craterStep == "place" && s >= 1.5f) { craterStep = ""; BeginScenario(); }
            return;
        }
        if (sub == "settle") { if (Time.time - phaseStart >= 0.5f) BeginScenario(); return; }
        if (sub == "run")
        {
            float t = Time.time - phaseStart;
            if (name == "drive") drive[1] = ((int)(t / 3f)) % 2 == 0 ? 1f : -1f; // forward first: the map edge is behind
            if (name == "airstrike" && explode != null && strikes.Length > 0 && blasts < 6 && t >= 0.5f + blasts)
            {
                float a = blasts * 60f * Mathf.Deg2Rad;
                Vector3 p = Ground(tank.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (12f + (blasts % 3) * 2f));
                pendingBlast.Add(p);
                blasts++;
            }
            if (name == "house" && explode != null && strikes.Length > 0)
            {
                if (houseBody == null && houseBlasts == 0)
                {
                    float best = float.MaxValue;
                    foreach (var s in UnityEngine.Object.FindObjectsByType<Game.Voxels.VoxelStructure>(FindObjectsSortMode.None))
                    {
                        var body = s.GetComponentInChildren<Game.Voxels.VoxelBody>();
                        if (!s.IsBuilding || body == null) continue;
                        float d = Vector3.Distance(s.transform.position, tank.position);
                        if (d < best) { best = d; houseBody = body; }
                    }
                    houseStart = houseBody != null ? houseBody.SolidCount : 0;
                }
                if (houseBody != null && houseBlasts < 4 && t >= 0.5f + houseBlasts)
                {
                    Bounds hb = houseBody.WorldBounds;
                    float hx = houseBlasts % 2 == 0 ? hb.min.x + 0.6f : hb.max.x - 0.6f;
                    float hz = houseBlasts < 2 ? hb.min.z + 0.6f : hb.max.z - 0.6f;
                    // At the foot of the wall: a raycast from above would land on the eaves.
                    pendingBlast.Add(new Vector3(hx, hb.min.y + 0.2f, hz));
                    houseBlasts++;
                }
            }
            if (name == "mg")
            {
                if (!mgOn)
                {
                    // The house and the tank's place were set in the settle (the same house every run).
                    if (mgHouse != null)
                    {
                        // A point just inside the wall facing the tank, 1.5 m up.
                        Bounds hb = mgHouse.WorldBounds;
                        Vector3 eye = tank.position + Vector3.up * 1.5f;
                        Vector3 onWall = hb.ClosestPoint(eye);
                        onWall.y = Mathf.Clamp(eye.y, hb.min.y + 0.5f, hb.max.y - 0.5f);
                        Vector3 inward = hb.center - onWall; inward.y = 0;
                        mgAim = onWall + (inward.sqrMagnitude > 1e-4f ? inward.normalized : Vector3.zero) * 0.3f;
                        mgHouseStart = mgHouse.SolidCount;
                        sbNotes.Add($"mg: house {mgHouse.name} at {Vector3.Distance(tank.position, mgAim):F1} m, gun {(cannonFire != null ? "yes" : "no")}");
                    }
                    else sbNotes.Add("mg: no building");
                    mgOn = true;
                }
                if (t >= 1f + cannonShots * 1.5f && !cannonPending) cannonPending = true;
            }
            if (name == "crush" && regax != null) regaxMin = Mathf.Min(regaxMin, Vector3.Distance(regax.transform.position, tank.position));
            if (name == "crater")
            {
                // In: 4 m before the crater centre (the near rim); out: 5.4 m past it (over the far rim).
                float along = Vector3.Dot(tankBody.position - craterAt, craterFwd);
                float speed = Vector3.Dot(tankBody.linearVelocity, craterFwd);
                maxAlong = Mathf.Max(maxAlong, along);
                if (entryTime < 0 && along >= -4f) { entryTime = Time.time; entrySpeed = speed; }
                if (entryTime >= 0 && exitTime < 0 && along > 0) minFarSpeed = Mathf.Min(minFarSpeed, speed);
                if (entryTime >= 0 && exitTime < 0 && along > 5.4f) exitTime = Time.time;
            }
            // Read the profiler in chunks so its buffer never wraps; the frame doing it is skipped.
            if (ProfilerDriver.lastFrameIndex - readUpTo > 200)
            {
                ReadFrames(ProfilerDriver.lastFrameIndex - 2);
                skipFrames.Add(ProfilerDriver.lastFrameIndex + 1);
                skipFrames.Add(ProfilerDriver.lastFrameIndex + 2);
            }
            if (t >= seconds) { drive[0] = drive[1] = 0; boostOn = false; mgOn = false; cannonPending = false; Game.CombatFocus.Clear(); sub = "tail"; phaseStart = Time.time; }
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
float total = 10f;
foreach (var p in plan) total += p.seconds + (p.name == "crater" || p.name == "mg" ? 1.6f : 0.6f);
return $"armed: {string.Join(",", plan.ConvertAll(p => p.name))}, ~{total:F0} s from scene start; strikes={strikes.Length} profilerFrames={frameCountNote}; read with perf_bench_read.cs";

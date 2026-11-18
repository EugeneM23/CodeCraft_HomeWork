// Arena benchmark (Docs/Rules/Performance.md section 3; AI milestone-1 spec section 6: "arena with horde", 60 live + the big one).
// Run in Play, in VoxelSandbox, right after entering Play (a fresh Play per run), with one call:
//   unity cmd --project-path <worktree> script eval --file Tools/PerfCheck/Bench/arena_bench.cs
// Optional AppDomain data set before the call (e.g. with a one-line script eval):
//   "arenabench_n"       int     live monsters on average (Crawlers + 1 Spitter in 10)             default 60
//   "arenabench_elite"   bool    the big Regax (the horde's EliteRegax kind) among them                 default true
//   "arenabench_waves"   string  asset path of an ArenaWaves to run instead of the synthetic one   default none
//   "arenabench_shadows" bool    crowd casts shadows                                              default false
// It returns at once: builds the horde (HordeSpawn.CreateInScene, no damage to the tank) and, once it is up, the
// arena director on a 40 x 40 m EdgeBurrowsLayout round the tank (16 burrows; Begin rebuilds the level grid over it),
// waits for >= 10 s of Play and the live count near the limit, then drives the tank forward/back (gas 1, flip every
// 3 s) for 8 s while it records. hordeMain = every Horde.* marker on the main thread (Horde.Director included).
// gcPerKill = game GC bytes of the run / kills. Read with arena_bench_read.cs. Leaving Play aborts the run.
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
if (domain.GetData("arenabench_status") is string st && st.StartsWith("running")) return "ERROR a run is already going: " + st;
if (domain.GetData("arenabench_update") is EditorApplication.CallbackFunction oldU) EditorApplication.update -= oldU;
if (domain.GetData("arenabench_render") is UnityEngine.Events.UnityAction oldR) Application.onBeforeRender -= oldR;

int n = domain.GetData("arenabench_n") is int nn ? nn : 60;
bool withElite = !(domain.GetData("arenabench_elite") is bool el) || el;
string wavesPath = domain.GetData("arenabench_waves") as string;
bool shadows = domain.GetData("arenabench_shadows") is bool sh && sh;
const float SECONDS = 8f;
var inv = System.Globalization.CultureInfo.InvariantCulture;

// ---------- Tank ----------
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

var strikes = UnityEngine.Object.FindObjectsByType<Game.VoxelAirStrike>(FindObjectsSortMode.None);
FieldInfo onField = typeof(Game.VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
var strikeWasOn = new bool[strikes.Length];
for (int i = 0; i < strikes.Length; i++) { strikeWasOn[i] = (bool)onField.GetValue(strikes[i]); onField.SetValue(strikes[i], false); }

// ---------- Horde ----------
Game.AIKit.Adapters.HordeBehaviour horde = null;
GameObject hordeObject = null;
var art = AssetDatabase.LoadAssetAtPath<Game.Horde.HordeArtConfig>("Assets/Game/Horde/Art/RegaxHordeArtConfig.asset");
if (art == null) return "ERROR no Assets/Game/Horde/Art/RegaxHordeArtConfig.asset (Tools/Horde/Bake Regax VAT)";
foreach (var old in UnityEngine.Object.FindObjectsByType<Game.AIKit.Adapters.HordeInstaller>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old.gameObject);
{
    var options = new Game.AIKit.Adapters.HordeOptions { capacity = 512, population = 0, tankDamageScale = 0f, castShadows = shadows };
    var installer = Game.AIKit.Adapters.HordeSpawn.CreateInScene(art, options);
    horde = installer.Behaviour;
    hordeObject = installer.gameObject;
}
float hordeMadeAt = Time.time;

// ---------- Arena ----------
Game.AIKit.Adapters.ArenaWaves waves = null;
if (!string.IsNullOrEmpty(wavesPath))
{
    waves = AssetDatabase.LoadAssetAtPath<Game.AIKit.Adapters.ArenaWaves>(wavesPath);
    if (waves == null) return "ERROR no ArenaWaves at " + wavesPath;
}
else
{
    waves = ScriptableObject.CreateInstance<Game.AIKit.Adapters.ArenaWaves>();
    Game.AIKit.Adapters.ArenaWaveEntry C() => new Game.AIKit.Adapters.ArenaWaveEntry(Game.EnemyKind.Crawler, 300);
    // The director refills below 70 % of the limit: a limit of n / 0.85 keeps about n alive on average.
    waves.liveLimit = Mathf.RoundToInt(n / 0.85f) + (withElite ? 1 : 0);
    var main = new Game.AIKit.Adapters.ArenaWave
    {
        delay = 0.5f, spawnInterval = 0.03f,
        // The queue interleaves entries round-robin: nine Crawler entries and one Spitter entry come out 9 : 1.
        entries = new[] { C(), C(), C(), C(), C(), C(), C(), C(), C(), new Game.AIKit.Adapters.ArenaWaveEntry(Game.EnemyKind.Spitter, 300) }
    };
    var elite = new Game.AIKit.Adapters.ArenaWave
    {
        delay = 0f, spawnInterval = 0.5f,
        entries = new[] { new Game.AIKit.Adapters.ArenaWaveEntry(Game.EnemyKind.EliteRegax, 1) }
    };
    waves.waves = withElite ? new[] { elite, main } : new[] { main };
}
Game.AIKit.Adapters.ArenaDirector director = null;
Game.AIKit.Adapters.EdgeBurrowsLayout layout = null;
float begunAt = -1f;
string Begin()
{
    Atomic.Entities.IEntity level = Game.AIKit.Adapters.LevelAI.Ensure();
    if (!Game.AIKit.Adapters.HordeAPI.TryGetLevelArenaDirector(level, out director) || director == null) return "no ArenaDirector on the level AI entity";
    var grid = Game.AIKit.Adapters.AIAPI.GetLevelNavigation(level).Grid;
    Vector3 c = tank.position;
    layout = Game.AIKit.Adapters.EdgeBurrowsLayout.Around(c, 40f, 16, p => grid.HeightAt(p));
    director.Begin(1, waves, layout);
    begunAt = Time.time;
    return null;
}
int EliteAlive() { var s = horde?.Simulation; int c = 0; if (s == null) return 0; for (int i = 0; i < s.HighWater; i++) if (s.IsSlotLive(i) && s.GetKindOf(i) == Game.AIKit.Adapters.HordeKinds.BRUTE) c++; return c; }

// ---------- Profiler ----------
bool profWasEnabled = ProfilerDriver.enabled;
bool profWasEditor = ProfilerDriver.profileEditor;
ProfilerDriver.profileEditor = false;
ProfilerDriver.enabled = true;
try
{
    var pus = typeof(ProfilerDriver).Assembly.GetType("UnityEditor.Profiling.ProfilerUserSettings");
    var fc = pus?.GetProperty("frameCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    if (fc != null && (int)fc.GetValue(null) < 2000) fc.SetValue(null, 2000);
}
catch (Exception) { }

var counterNames = new (ProfilerCategory cat, string name, string key, bool time)[]
{
    (ProfilerCategory.Internal, "GPU Frame Time", "gpu", true),
    (ProfilerCategory.Render, "Batches Count", "batches", false),
    (ProfilerCategory.Render, "SetPass Calls Count", "setpass", false),
    (ProfilerCategory.Render, "Triangles Count", "triangles", false),
    (ProfilerCategory.Render, "Shadow Casters Count", "shadowCasters", false),
    // Horde markers: main thread, then the jobs (summed over all worker threads).
    (ProfilerCategory.Scripts, "Horde.Tick", "hordeTick", true),
    (ProfilerCategory.Scripts, "Horde.Complete", "hordeComplete", true),
    (ProfilerCategory.Scripts, "Horde.Simulate", "hordeSimulate", true),
    (ProfilerCategory.Scripts, "Horde.Render", "hordeRender", true),
    (ProfilerCategory.Scripts, "Horde.Deaths", "hordeDeaths", true),
    (ProfilerCategory.Scripts, "Horde.Director", "hordeDirector", true),
    (ProfilerCategory.Scripts, "Horde.Job.Shots", "jobShots", true),
    (ProfilerCategory.Scripts, "Horde.FlowField", "hordeFlowField", true),
    (ProfilerCategory.Scripts, "Horde.NavGrid", "hordeNavGrid", true),
    (ProfilerCategory.Scripts, "Horde.Job.Hash", "jobHash", true),
    (ProfilerCategory.Scripts, "Horde.Job.Steer", "jobSteer", true),
    (ProfilerCategory.Scripts, "Horde.Job.Contact", "jobContact", true),
    (ProfilerCategory.Scripts, "Horde.Job.Render", "jobRender", true),
};
var recorders = new List<ProfilerRecorder>();

// Per-frame CPU data from the profiler hierarchy, as perf_bench.cs.
// Columns: 0 main, 1 playerLoop, 2 gc (game), 3 horde main thread (Horde.* under PlayerLoop), 4 render, 5 gc editor-only, 6 gc HUD.
var skipFrames = new HashSet<int>();
var rows = new List<double[]>();
var worst = new List<(double ms, int frame, string top)>();
int readUpTo = -1;
var children = new List<int>();
var stack = new Stack<(int id, int depth, string name, bool inHorde)>();
var gcBy = new Dictionary<string, double>();
bool IsEditorOnly(string nm) => nm.Contains("Burst.Compiler") || nm.Contains("JitCompilerService");
bool IsDebugHud(string nm) => nm == "GUI.Repaint" || nm.Contains("VoxelDemoHud") || nm.Contains("VoxelSandboxOrbit.OnGUI");
bool IsRender(string nm) => nm == "Camera.Render" || (nm.StartsWith("PostLateUpdate.") && nm.Contains("Render"));
string Short(string nm)
{
    int k = nm.LastIndexOf("::", StringComparison.Ordinal);
    if (k >= 0) nm = nm.Substring(k + 2);
    return nm.Replace(" [Invoke]", "").Replace("\"", "'");
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
        parts.Add(Short(view.GetItemName(best)) + " " + bestT.ToString("F1", inv));
        id = best; parentT = bestT;
    }
    return string.Join(" > ", parts);
}
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
        double hordeMs = 0, render = 0, gcEditor = 0, gcHud = 0;
        stack.Clear();
        stack.Push((plId, 0, "PlayerLoop", false));
        while (stack.Count > 0)
        {
            var (id, d, parentName, inHorde) = stack.Pop();
            children.Clear();
            view.GetItemChildren(id, children);
            foreach (int c in children)
            {
                string nm = view.GetItemName(c);
                if (IsEditorOnly(nm)) { gcEditor += view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory); continue; }
                if (IsDebugHud(nm)) { gcHud += view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory); continue; }
                double t = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
                if (d <= 1 && IsRender(nm)) render += t;
                if (nm == "GC.Alloc") { gcBy[parentName] = (gcBy.TryGetValue(parentName, out double g0) ? g0 : 0) + view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnGcMemory); continue; }
                bool horde0 = nm.StartsWith("Horde.");
                if (horde0 && !inHorde) hordeMs += t;
                stack.Push((c, d + 1, nm, inHorde || horde0));
            }
        }
        rows.Add(new[] { view.frameTimeMs, pl, Math.Max(0, gc - gcEditor - gcHud), hordeMs, render, gcEditor, gcHud });
        if (worst.Count < 3 || pl > worst[worst.Count - 1].ms)
        {
            worst.Add((pl, f, HotPath(view, plId)));
            worst.Sort((a, b) => b.ms.CompareTo(a.ms));
            if (worst.Count > 3) worst.RemoveAt(3);
        }
    }
    readUpTo = Math.Max(readUpTo, upTo);
}
string Stats(int col, string fmt = "F2", bool frames = true)
{
    if (rows.Count == 0) return "\"n\":0";
    var v = new double[rows.Count];
    for (int i = 0; i < v.Length; i++) v[i] = rows[i][col];
    Array.Sort(v);
    double sum = 0; foreach (var x in v) sum += x;
    int over = 0; foreach (var x in v) if (x > 16.6) over++;
    string F(double x) => x.ToString(fmt, inv);
    string s = $"\"avg\":{F(sum / v.Length)},\"p50\":{F(v[v.Length / 2])},\"p95\":{F(v[(int)(v.Length * 0.95)])},\"max\":{F(v[v.Length - 1])}";
    if (frames) s += $",\"over16\":{over}";
    else { int nz = 0; foreach (var x in v) if (x > 0) nz++; s += $",\"framesWithAlloc\":{nz}"; }
    return s;
}
string CounterStats(ProfilerRecorder r, bool time)
{
    int cnt = r.Count;
    if (cnt == 0) return "null";
    double sum = 0, max = 0;
    for (int k = 0; k < cnt; k++) { double x = r.GetSample(k).Value; sum += x; if (x > max) max = x; }
    double s = time ? 1e-6 : 1;
    return $"{{\"avg\":{(sum / cnt * s).ToString(time ? "F3" : "F0", inv)},\"max\":{(max * s).ToString(time ? "F3" : "F0", inv)}}}";
}

// ---------- Drive ----------
var drive = new float[2];
UnityEngine.Events.UnityAction onRender = () =>
{
    if (drive[0] != 0 || drive[1] != 0) driveInput.Value = new Vector2(drive[0], drive[1]);
};
Application.onBeforeRender += onRender;

string sub = "warmup";
float phaseStart = 0, runStart = 0;
int killsAtStart = 0;
double aliveSum = 0, visibleSum = 0; int samples = 0;
double GcSum() { double s = 0; foreach (var r in rows) s += r[2]; return s; }
Vector3 tankStart = tank.position;
float maxTankSpeed = 0;
var notes = new List<string>();
EditorApplication.CallbackFunction onUpdate = null;

int Kills() => hordeObject != null && hordeObject.TryGetComponent<Atomic.Entities.MonoEntity>(out var me) ? Game.AIKit.Adapters.AIAPI.GetHordeKills(me).Value : 0;

void Finish(string status, string result)
{
    EditorApplication.update -= onUpdate;
    Application.onBeforeRender -= onRender;
    drive[0] = drive[1] = 0;
    foreach (var r in recorders) r.Dispose();
    recorders.Clear();
    for (int i = 0; i < strikes.Length; i++) if (strikes[i] != null) onField.SetValue(strikes[i], strikeWasOn[i]);
    ProfilerDriver.enabled = profWasEnabled;
    ProfilerDriver.profileEditor = profWasEditor;
    domain.SetData("arenabench_result", result);
    domain.SetData("arenabench_status", status);
}

string Env() =>
    $"\"unity\":\"{Application.unityVersion}\",\"pipeline\":\"{(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name)}\"," +
    $"\"quality\":\"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\"screen\":\"{Screen.width}x{Screen.height}\"," +
    $"\"sceneViews\":{SceneView.sceneViews.Count},\"burst\":{(Unity.Burst.BurstCompiler.IsEnabled ? "true" : "false")}," +
    $"\"workers\":{Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount},\"gpuName\":\"{SystemInfo.graphicsDeviceName}\",\"cpu\":\"{SystemInfo.processorType.Trim()}\"";

onUpdate = () =>
{
    try
    {
        if (!EditorApplication.isPlaying) { Finish("aborted: left Play", null); return; }
        if (EditorApplication.isPaused) return;
        int alive = horde?.Simulation?.AliveCount ?? 0;
        if (sub == "warmup")
        {
            if (director == null)
            {
                if (horde?.Simulation == null) return; // the horde initialises on its entity's Start
                string err = Begin();
                if (err != null) { Finish("failed", "{\"status\":\"failed\",\"error\":\"" + err + "\"}"); return; }
            }
            bool full = alive >= waves.liveLimit * 0.9f;
            domain.SetData("arenabench_status", $"running: warm-up t={Time.timeSinceLevelLoad.ToString("F1", inv)} alive={alive}/{waves.liveLimit} elite={EliteAlive()}");
            if (Time.timeSinceLevelLoad < 10f || Time.time - begunAt < 4f || !full) return;
            sub = "settle"; phaseStart = Time.time;
            return;
        }
        if (sub == "settle")
        {
            if (Time.time - phaseStart < 0.5f) return;
            rows.Clear(); worst.Clear(); gcBy.Clear();
            foreach (var c in counterNames) recorders.Add(ProfilerRecorder.StartNew(c.cat, c.name, 8000));
            readUpTo = ProfilerDriver.lastFrameIndex;
            runStart = phaseStart = Time.time;
            killsAtStart = Kills();
            tankStart = tank.position;
            sub = "run";
            return;
        }
        if (sub == "run")
        {
            float t = Time.time - phaseStart;
            drive[1] = ((int)(t / 3f)) % 2 == 0 ? 1f : -1f;
            var rb = tank.GetComponent<Rigidbody>();
            if (rb != null) maxTankSpeed = Mathf.Max(maxTankSpeed, rb.linearVelocity.magnitude);
            aliveSum += alive; visibleSum += horde?.Simulation?.VisibleCount ?? 0; samples++;
            domain.SetData("arenabench_status", $"running: arena live={alive} {t.ToString("F1", inv)}/{SECONDS}");
            if (ProfilerDriver.lastFrameIndex - readUpTo > 200)
            {
                ReadFrames(ProfilerDriver.lastFrameIndex - 2);
                skipFrames.Add(ProfilerDriver.lastFrameIndex + 1);
                skipFrames.Add(ProfilerDriver.lastFrameIndex + 2);
            }
            if (t >= SECONDS) { drive[0] = drive[1] = 0; sub = "tail"; phaseStart = Time.time; }
            return;
        }
        if (sub == "tail")
        {
            if (Time.time - phaseStart < 0.1f) return;
            ReadFrames(ProfilerDriver.lastFrameIndex);
            var counters = new List<string>();
            for (int i = 0; i < counterNames.Length; i++) counters.Add($"\"{counterNames[i].key}\":{CounterStats(recorders[i], counterNames[i].time)}");
            var w = new List<string>();
            foreach (var x in worst) w.Add($"\"{x.ms.ToString("F1", inv)} ms: {x.top.Replace("\"", "'")}\"");
            var gcList = new List<KeyValuePair<string, double>>(gcBy);
            gcList.Sort((a, b) => b.Value.CompareTo(a.Value));
            var gcTop = new List<string>();
            for (int i = 0; i < gcList.Count && i < 8; i++) gcTop.Add($"\"{Short(gcList[i].Key)} {gcList[i].Value.ToString("F0", inv)} B\"");
            float secs = Time.time - runStart;
            int kills = Kills() - killsAtStart;
            string tris = art != null ? art.triangles.ToString() : "0";
            string result = $"{{\"status\":\"done\",\"date\":\"{DateTime.Now:yyyy-MM-dd HH:mm}\",{Env()},\"n\":{n},\"shadows\":{(shadows ? "true" : "false")},\"trisPerMonster\":{tris}," +
                            $"\"frames\":{rows.Count},\"seconds\":{secs.ToString("F1", inv)},\"aliveAvg\":{(samples > 0 ? aliveSum / samples : 0).ToString("F0", inv)},\"visibleAvg\":{(samples > 0 ? visibleSum / samples : 0).ToString("F0", inv)}," +
                            $"\"kills\":{kills},\"gcPerKill\":{(kills > 0 ? GcSum() / kills : 0).ToString("F0", inv)},\"eliteAlive\":{EliteAlive()},\"liveLimit\":{waves.liveLimit},\"burrowsOpened\":{layout?.OpenedCount ?? 0}," +
                            $"\"killsPerSecond\":{(kills / Math.Max(0.1f, secs)).ToString("F1", inv)},\"tankMoved\":{Vector3.Distance(tank.position, tankStart).ToString("F1", inv)},\"tankMaxSpeed\":{maxTankSpeed.ToString("F1", inv)}," +
                            $"\"main\":{{{Stats(0)}}},\"playerLoop\":{{{Stats(1)}}},\"hordeMain\":{{{Stats(3)}}},\"render\":{{{Stats(4)}}}," +
                            $"\"gcBytes\":{{{Stats(2, "F0", false)}}},\"gcEditorOnly\":{{{Stats(5, "F0", false)}}},\"gcDebugHud\":{{{Stats(6, "F0", false)}}}," +
                            $"{string.Join(",", counters)},\"gcTop\":[{string.Join(",", gcTop)}],\"worst\":[{string.Join(",", w)}],\"notes\":\"{string.Join("; ", notes).Replace("\"", "'")}\"}}";
            Finish("done", result);
        }
    }
    catch (Exception e)
    {
        notes.Add("exception: " + e.GetBaseException().Message);
        Finish("failed", "{\"status\":\"failed\",\"error\":\"" + e.GetBaseException().ToString().Replace("\"", "'").Replace("\r", " ").Replace("\n", " ") + "\"}");
    }
};
EditorApplication.update += onUpdate;
domain.SetData("arenabench_update", onUpdate);
domain.SetData("arenabench_render", onRender);
domain.SetData("arenabench_status", "running: armed");
domain.SetData("arenabench_result", null);
return $"armed: arena live limit {waves.liveLimit} elite={withElite} shadows={shadows}; measures 8 s after >= 10 s of Play and a full arena; read with arena_bench_read.cs";

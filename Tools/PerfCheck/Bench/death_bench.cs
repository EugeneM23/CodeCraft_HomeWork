// Death benchmark (Docs/Rules/Performance.md: the event frame of a Regax death; perf debt item 3 of
// Docs/Reports/2026-11-18-perf-fix.md). Run in Play, in VoxelSandbox (fresh Play, >= 10 s in), with one call:
//   unity cmd --project-path <worktree> script eval --file Tools/PerfCheck/Bench/death_bench.cs
// It returns at once and kills plain Regax or zombies (Zombie.prefab; not Regax_Elite*) one at a time, 1.2 s apart, cycling shot / blast / crush:
//   shot  - DamageUseCase.TakeDamage from a source 3 m off towards the tank (Damage 100), as a bullet;
//   blast - the same from a shell (ExplosionRadius 5, Damage 150, factor 0.8), then VoxelDamage.Blast as ExplosionUseCase;
//   crush - TankCrushBehaviour.Crush on the tank (its own killer, blood fans and stains).
// The kill runs inside the player loop (a step at the end of Update, before LateUpdate's voxel rebuild).
// Profiler on, allocation callstacks on. Per kill: frames D..D+5 (D = the kill frame) with PlayerLoop ms, game GC bytes and
// the Death.* / Crush.* / voxel marker times; over the whole window: PlayerLoop avg/p95/max/over16; GC sources by
// managed callstack (top frames) over all death frames. Read the result with death_bench_read.cs.
// AppDomain data (optional): "deathbench_kills" int (default 9), "deathbench_kinds" string "shot,blast,crush",
// "deathbench_stacks" bool (default true; false for timing without the callstacks' cost on every allocation).
using System;
using System.Collections.Generic;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using Game;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.LowLevel;

var domain = AppDomain.CurrentDomain;
if (!EditorApplication.isPlaying) return "ERROR not in Play";
string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
if (sceneName != "VoxelSandbox") return "ERROR active scene is " + sceneName + ", not VoxelSandbox";
if (domain.GetData("deathbench_status") is string st && st.StartsWith("running")) return "ERROR a run is already going: " + st;
if (domain.GetData("deathbench_update") is EditorApplication.CallbackFunction oldU) EditorApplication.update -= oldU;
if (domain.GetData("deathbench_step") is PlayerLoopSystem.UpdateFunction oldStep)
{
    var l0 = PlayerLoop.GetCurrentPlayerLoop();
    for (int i = 0; i < l0.subSystemList.Length; i++)
    {
        var subs = new List<PlayerLoopSystem>(l0.subSystemList[i].subSystemList ?? Array.Empty<PlayerLoopSystem>());
        if (subs.RemoveAll(s => s.updateDelegate == oldStep) > 0) { l0.subSystemList[i].subSystemList = subs.ToArray(); PlayerLoop.SetPlayerLoop(l0); }
    }
}

int kills = domain.GetData("deathbench_kills") is int kk ? kk : 9;
string[] kinds = (domain.GetData("deathbench_kinds") as string ?? "shot,blast,crush").Split(',');
const float GAP = 1.2f;
var inv = System.Globalization.CultureInfo.InvariantCulture;

// ---------- Tank and its crush ----------
var start = UnityEngine.Object.FindFirstObjectByType<Game.VoxelSandboxStart>();
Transform tank = start != null && start.Vehicle != null ? start.Vehicle.transform : GameObject.Find("Tank_01")?.transform;
if (tank == null) return "ERROR no tank";
var tankEntity = tank.GetComponent<MonoEntity>();
object crushBehaviour = null;
if (tankEntity != null)
    for (int i = 0; i < tankEntity.BehaviourCount; i++)
        if (tankEntity.GetBehaviourAt(i) is Game.TankCrushBehaviour cb) crushBehaviour = cb;
MethodInfo crushMethod = typeof(Game.TankCrushBehaviour).GetMethod("Crush", BindingFlags.NonPublic | BindingFlags.Instance);

var strikes = UnityEngine.Object.FindObjectsByType<Game.VoxelAirStrike>(FindObjectsSortMode.None);
FieldInfo onField = typeof(Game.VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
var strikeWasOn = new bool[strikes.Length];
for (int i = 0; i < strikes.Length; i++) { strikeWasOn[i] = (bool)onField.GetValue(strikes[i]); onField.SetValue(strikes[i], false); }

// ---------- Profiler ----------
bool profWasEnabled = ProfilerDriver.enabled;
bool profWasEditor = ProfilerDriver.profileEditor;
bool stacksWere = UnityEngine.Profiling.Profiler.enableAllocationCallstacks;
ProfilerDriver.profileEditor = false;
ProfilerDriver.enabled = true;
// Callstacks make every GC.Alloc slower: "deathbench_stacks" false times the deaths without them (no gcSources then).
bool withStacks = !(domain.GetData("deathbench_stacks") is bool ws) || ws;
UnityEngine.Profiling.Profiler.enableAllocationCallstacks = withStacks;
object memModeWas = null;
PropertyInfo memMode = typeof(ProfilerDriver).GetProperty("memoryRecordMode", BindingFlags.Public | BindingFlags.Static);
try { if (memMode != null) { memModeWas = memMode.GetValue(null); memMode.SetValue(null, Enum.Parse(memMode.PropertyType, withStacks ? "GCAlloc" : "None")); } } catch { memMode = null; }
try
{
    var pus = typeof(ProfilerDriver).Assembly.GetType("UnityEditor.Profiling.ProfilerUserSettings");
    var fc = pus?.GetProperty("frameCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    if (fc != null && (int)fc.GetValue(null) < 2000) fc.SetValue(null, 2000);
}
catch { }

var killMarkers = new Dictionary<string, ProfilerMarker> { ["shot"] = new ProfilerMarker("DeathBench.Kill.shot"), ["blast"] = new ProfilerMarker("DeathBench.Kill.blast"), ["crush"] = new ProfilerMarker("DeathBench.Kill.crush") };

// ---------- Victims ----------
var victims = new List<GameObject>();
foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
    if ((go.name.StartsWith("Regax") || go.name.StartsWith("Zombie")) && !go.name.StartsWith("Regax_Elite") && go.activeInHierarchy && go.GetComponent<MonoEntity>() != null)
        victims.Add(go);
victims.Sort((a, b) => (a.transform.position - tank.position).sqrMagnitude.CompareTo((b.transform.position - tank.position).sqrMagnitude));
if (victims.Count == 0) return "ERROR no Regax or zombie in the scene";
kills = Math.Min(kills, victims.Count);

var killerGo = new GameObject("DeathBenchKiller");
IEntity shot = new Entity();
shot.AddTransform(killerGo.transform);
shot.AddDamage(new ReactiveInt(100));
IEntity shell = new Entity();
shell.AddTransform(killerGo.transform);
shell.AddDamage(new ReactiveInt(150));
shell.AddExplosionRadius(5f);

int done = 0;
string pendingKind = null;
var killLog = new List<string>();
PlayerLoopSystem.UpdateFunction step = () =>
{
    if (pendingKind == null) return;
    string kind = pendingKind;
    pendingKind = null;
    GameObject victim = victims[done];
    var entity = victim.GetComponent<MonoEntity>();
    Vector3 at = victim.transform.position + Vector3.up * 1f;
    Vector3 toward = tank.position - victim.transform.position; toward.y = 0;
    toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
    ProfilerMarker killMarker = killMarkers.TryGetValue(kind, out var km) ? km : killMarkers["shot"];
    killMarker.Begin();
    try
    {
        if (kind == "shot")
        {
            killerGo.transform.position = at + toward * 3f;
            DamageUseCase.TakeDamage(entity, new TakeDamageArgs(shot));
        }
        else if (kind == "blast")
        {
            Vector3 p = victim.transform.position + toward * 1.5f + Vector3.up * 0.3f;
            killerGo.transform.position = p;
            DamageUseCase.TakeDamage(entity, new TakeDamageArgs(shell, 0.8f));
            Game.Voxels.VoxelDamage.Blast(p, 5f * 0.32f, 9f);
        }
        else if (crushBehaviour != null)
            crushMethod.Invoke(crushBehaviour, new object[] { (IEntity)entity, victim.transform.position + toward * 0.6f + Vector3.up * 0.5f, 7f });
    }
    catch (Exception e) { killLog.Add("exception " + e.GetBaseException().Message.Replace("\"", "'")); }
    killMarker.End();
    killLog.Add(kind);
    done++;
};
{
    var loop = PlayerLoop.GetCurrentPlayerLoop();
    for (int i = 0; i < loop.subSystemList.Length; i++)
    {
        if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.Update)) continue;
        var subs = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList ?? Array.Empty<PlayerLoopSystem>());
        subs.Add(new PlayerLoopSystem { type = typeof(DBNull), updateDelegate = step });
        loop.subSystemList[i].subSystemList = subs.ToArray();
    }
    PlayerLoop.SetPlayerLoop(loop);
}

// ---------- Reading ----------
bool IsEditorOnly(string n) => n.Contains("Burst.Compiler") || n.Contains("JitCompilerService");
bool IsDebugHud(string n) => n == "GUI.Repaint" || n.Contains("VoxelDemoHud") || n.Contains("VoxelSandboxOrbit.OnGUI");
bool Interesting(string n) => n.StartsWith("Death.") || n.StartsWith("Crush.") || n == "VoxelWorld.LateUpdate()" || n.StartsWith("VoxelDebris") ||
                              n == "FixedUpdate.PhysicsFixedUpdate" || n == "Physics.Simulate" || n.StartsWith("VoxelDeaths") || n.StartsWith("DeathBench.Kill") ||
                              n.Contains("Combat.XpOrbs") || n == "PostLateUpdate.FinishFrameRendering" || n.StartsWith("Physics.") && n.Contains("Bake");
var frames = new List<(int index, double pl, double gc, bool kill, string kind, Dictionary<string, double> markers)>();
var gcSources = new Dictionary<string, double[]>(); // key -> bytes, count
var skip = new HashSet<int>();
int readUpTo = ProfilerDriver.lastFrameIndex;
int firstFrame = readUpTo + 1;
var callstack = new List<ulong>();
var names = new List<string>();
var ends = new List<int>();
var anc = new List<string>();
var resolved = new Dictionary<ulong, string>();

string Resolve(RawFrameDataView v, ulong a)
{
    if (resolved.TryGetValue(a, out string s)) return s;
    var mi = v.ResolveMethodInfo(a);
    s = mi.methodName ?? "?";
    int paren = s.IndexOf('(');
    if (paren > 0) s = s.Substring(0, paren);
    if (!string.IsNullOrEmpty(mi.sourceFileName) && mi.sourceFileLine > 0)
        s += ":" + System.IO.Path.GetFileName(mi.sourceFileName) + ":" + mi.sourceFileLine;
    resolved[a] = s;
    return s;
}

void ReadFrames(int upTo)
{
    for (int f = Math.Max(readUpTo + 1, ProfilerDriver.firstFrameIndex); f <= upTo; f++)
    {
        if (skip.Contains(f)) continue;
        using var v = ProfilerDriver.GetRawFrameDataView(f, 0);
        if (v == null || !v.valid) continue;
        double pl = 0, gc = 0; bool kill = false; string kindSeen = null;
        var markers = new Dictionary<string, double>();
        var gcLocal = new List<(string key, double bytes)>();
        // Depth-first: a stack of (name, last sample index inside it).
        names.Clear(); ends.Clear();
        int plEnd = -1;
        for (int i = 0; i < v.sampleCount; i++)
        {
            while (ends.Count > 0 && i > ends[ends.Count - 1]) { ends.RemoveAt(ends.Count - 1); names.RemoveAt(names.Count - 1); }
            string n = v.GetSampleName(i);
            int last = i + CountDescendants(v, i);
            bool inPl = plEnd >= i;
            if (n == "PlayerLoop" && !inPl) { pl += v.GetSampleTimeMs(i); plEnd = last; inPl = true; }
            if (n.StartsWith("DeathBench.Kill")) { kill = true; kindSeen = n.Substring(16); }
            // The marked steps at any depth, and the coarse phases of the frame (depth 2-4 under PlayerLoop, 0.3 ms or more).
            bool coarse = inPl && n != "PlayerLoop" && names.Count >= 1 && names.Count <= 4 && v.GetSampleTimeMs(i) >= 0.3;
            if (inPl && (Interesting(n) || coarse) && !names.Contains(n)) { markers.TryGetValue(n, out double t); markers[n] = t + v.GetSampleTimeMs(i); }
            if (inPl && n == "GC.Alloc")
            {
                bool excluded = false;
                foreach (string a in names) if (IsEditorOnly(a) || IsDebugHud(a)) { excluded = true; break; }
                if (!excluded)
                {
                    long bytes = v.GetSampleMetadataCount(i) > 0 ? v.GetSampleMetadataAsLong(i, 0) : 0;
                    gc += bytes;
                    callstack.Clear();
                    v.GetSampleCallstack(i, callstack);
                    var parts = new List<string>();
                    for (int c = 0; c < callstack.Count && parts.Count < 4; c++)
                    {
                        string m = Resolve(v, callstack[c]);
                        if (m == "?" || m.StartsWith("System.Runtime") || m.StartsWith("(wrapper")) continue;
                        parts.Add(m);
                    }
                    string marker = "";
                    for (int k = names.Count - 1; k >= 0; k--) if (Interesting(names[k]) || names[k].Contains(".")) { marker = names[k]; break; }
                    gcLocal.Add(((parts.Count > 0 ? string.Join(" < ", parts) : "(no stack)") + " @ " + marker, bytes));
                }
            }
            names.Add(n); ends.Add(last);
        }
        frames.Add((f, pl, gc, kill, kindSeen, markers));
        foreach (var (key, bytes) in gcLocal)
        {
            if (!gcSources.TryGetValue(key, out var a)) gcSources[key] = a = new double[2];
            a[0] += bytes; a[1]++;
        }
    }
    readUpTo = Math.Max(readUpTo, upTo);
}

int CountDescendants(RawFrameDataView v, int i)
{
    int total = 0, kids = v.GetSampleChildrenCount(i), at = i + 1;
    for (int k = 0; k < kids; k++) { int d = CountDescendants(v, at); total += 1 + d; at += 1 + d; }
    return total;
}

void Finish(string status)
{
    EditorApplication.update -= (EditorApplication.CallbackFunction)domain.GetData("deathbench_update");
    var loop = PlayerLoop.GetCurrentPlayerLoop();
    for (int i = 0; i < loop.subSystemList.Length; i++)
    {
        var subs = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList ?? Array.Empty<PlayerLoopSystem>());
        if (subs.RemoveAll(s => s.updateDelegate == step) > 0) loop.subSystemList[i].subSystemList = subs.ToArray();
    }
    PlayerLoop.SetPlayerLoop(loop);
    for (int i = 0; i < strikes.Length; i++) if (strikes[i] != null) onField.SetValue(strikes[i], strikeWasOn[i]);
    ProfilerDriver.enabled = profWasEnabled;
    ProfilerDriver.profileEditor = profWasEditor;
    // The record mode first: setting it turns the callstacks on or off too (and it outlives Play, an editor setting).
    try { if (memMode != null && memModeWas != null) memMode.SetValue(null, memModeWas); } catch { }
    UnityEngine.Profiling.Profiler.enableAllocationCallstacks = stacksWere;
    UnityEngine.Object.Destroy(killerGo);

    string F(double x, string fmt = "F2") => x.ToString(fmt, inv);
    // Window stats.
    var pls = new List<double>();
    int allocFrames = 0;
    foreach (var fr in frames) { pls.Add(fr.pl); if (fr.gc > 0) allocFrames++; }
    pls.Sort();
    double sum = 0; foreach (var x in pls) sum += x;
    int over = 0; foreach (var x in pls) if (x > 16.6) over++;
    string window = pls.Count == 0 ? "{}" :
        $"{{\"frames\":{pls.Count},\"avg\":{F(sum / pls.Count)},\"p95\":{F(pls[(int)(pls.Count * 0.95)])},\"max\":{F(pls[pls.Count - 1])},\"over16\":{over},\"framesWithAlloc\":{allocFrames}}}";
    // Per kill.
    var perKill = new List<string>();
    int killNo = 0;
    for (int i = 0; i < frames.Count; i++)
    {
        if (!frames[i].kill) continue;
        var rows = new List<string>();
        for (int k = 0; k < 6 && i + k < frames.Count; k++)
        {
            var fr = frames[i + k];
            var ms = new List<string>();
            foreach (var kv in fr.markers) if (kv.Value >= 0.05) ms.Add($"\"{kv.Key}\":{F(kv.Value)}");
            rows.Add($"{{\"d\":{k},\"pl\":{F(fr.pl)},\"gc\":{F(fr.gc, "F0")},\"m\":{{{string.Join(",", ms)}}}}}");
        }
        string kind = frames[i].kind ?? "?";
        perKill.Add($"{{\"kind\":\"{kind}\",\"frames\":[{string.Join(",", rows)}]}}");
        killNo++;
    }
    // The three worst frames of the window, with what took them (0.5 ms or more), and how far each is from the last kill.
    var order = new List<int>();
    for (int i = 0; i < frames.Count; i++) order.Add(i);
    order.Sort((a, b) => frames[b].pl.CompareTo(frames[a].pl));
    var worstOut = new List<string>();
    for (int w = 0; w < 3 && w < order.Count; w++)
    {
        int i = order[w];
        int since = -1;
        for (int k = i; k >= 0; k--) if (frames[k].kill) { since = i - k; break; }
        var ms = new List<string>();
        foreach (var kv in frames[i].markers) if (kv.Value >= 0.5) ms.Add(kv.Key + " " + F(kv.Value));
        worstOut.Add($"\"{F(frames[i].pl)} ms, {since} frames after a kill: {string.Join(", ", ms).Replace("\"", "'")}\"");
    }
    var src = new List<KeyValuePair<string, double[]>>(gcSources);
    src.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
    var srcOut = new List<string>();
    double gcTotal = 0; foreach (var kv in src) gcTotal += kv.Value[0];
    for (int i = 0; i < src.Count && i < 40; i++)
        srcOut.Add($"\"{F(src[i].Value[0], "F0")}B x{src[i].Value[1]}: {src[i].Key.Replace("\"", "'").Replace("\\", "/")}\"");
    domain.SetData("deathbench_result",
        $"{{\"status\":\"{status}\",\"date\":\"{DateTime.Now:yyyy-MM-dd HH:mm}\",\"burst\":{(Unity.Burst.BurstCompiler.IsEnabled ? "true" : "false")},\"kills\":{killLog.Count},\"log\":\"{string.Join(",", killLog)}\"," +
        $"\"window\":{window},\"worst\":[{string.Join(",", worstOut)}],\"gcTotal\":{F(gcTotal, "F0")},\"perKill\":[{string.Join(",", perKill)}],\"gcSources\":[{string.Join(",", srcOut)}]}}");
    domain.SetData("deathbench_status", status);
}

float phaseStart = -1f;
float nextKill = 0f;
EditorApplication.CallbackFunction onUpdate = null;
onUpdate = () =>
{
    try
    {
        if (!EditorApplication.isPlaying) { Finish("aborted: left Play"); return; }
        if (EditorApplication.isPaused) return;
        if (Time.timeSinceLevelLoad < 10f) { domain.SetData("deathbench_status", "running: warm-up " + Time.timeSinceLevelLoad.ToString("F1", inv)); return; }
        if (phaseStart < 0) { phaseStart = Time.time; nextKill = Time.time + 0.5f; readUpTo = ProfilerDriver.lastFrameIndex; return; }
        domain.SetData("deathbench_status", $"running: {done}/{kills} kills");
        if (done < kills && pendingKind == null && Time.time >= nextKill)
        {
            pendingKind = kinds[done % kinds.Length];
            nextKill = Time.time + GAP;
        }
        if (done >= kills && Time.time >= nextKill)
        {
            ReadFrames(ProfilerDriver.lastFrameIndex);
            Finish("done");
            return;
        }
        // Read in chunks so the buffer never wraps; never right after a kill (its frames must be whole).
        if (ProfilerDriver.lastFrameIndex - readUpTo > 300 && Time.time < nextKill - 0.6f)
        {
            ReadFrames(ProfilerDriver.lastFrameIndex - 2);
            skip.Add(ProfilerDriver.lastFrameIndex + 1);
            skip.Add(ProfilerDriver.lastFrameIndex + 2);
        }
    }
    catch (Exception e) { Finish("failed: " + e.GetBaseException().Message.Replace("\"", "'")); }
};
EditorApplication.update += onUpdate;
domain.SetData("deathbench_update", onUpdate);
domain.SetData("deathbench_step", step);
domain.SetData("deathbench_status", "running: armed");
domain.SetData("deathbench_result", null);
return $"armed: {kills} kills ({string.Join(",", kinds)}) of {victims.Count} Regax, crush={(crushBehaviour != null ? "yes" : "no")}; read with death_bench_read.cs";

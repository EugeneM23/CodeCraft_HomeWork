// Arena team scenario for perf-check: Chapter1_Arena, 5 builds then a fight on arena 5.
// Arm with one script eval; read AppDomain "arenabench_status"/"arenabench_result".
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

var domain = AppDomain.CurrentDomain;
if (!EditorApplication.isPlaying) return "ERROR not in Play";
if (domain.GetData("arenabench_status") is string st0 && st0.StartsWith("running")) return "ERROR already running";
if (domain.GetData("arenabench_update") is EditorApplication.CallbackFunction oldU) EditorApplication.update -= oldU;
if (domain.GetData("arenabench_render") is UnityEngine.Events.UnityAction oldR) Application.onBeforeRender -= oldR;

var s = UnityEngine.Object.FindFirstObjectByType<Game.ArenaService>();
if (s == null) return "ERROR no ArenaService";
if (!s.IsBuilt) return "ERROR arena 1 not built yet";

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
catch { }

var ci = System.Globalization.CultureInfo.InvariantCulture;
var skip = new HashSet<int>();
// frame -> (playerLoop ms, gc bytes, hot path or null)
var rows = new SortedDictionary<int, (double pl, double gc, string hot)>();
var marks = new Dictionary<int, string>(); // build frame -> Arena.* markers
var buildFrames = new HashSet<int>();
int readUpTo = ProfilerDriver.lastFrameIndex;
var kids = new List<int>();

string Short(string n) { int k = n.LastIndexOf("::", StringComparison.Ordinal); if (k >= 0) n = n.Substring(k + 2); return n.Replace(" [Invoke]", "").Replace("\"", "'"); }
string HotPath(HierarchyFrameDataView view, int id)
{
    var parts = new List<string>();
    double parentT = view.GetItemColumnDataAsDouble(id, HierarchyFrameDataView.columnTotalTime);
    var k2 = new List<int>();
    for (int depth = 0; depth < 14; depth++)
    {
        k2.Clear(); view.GetItemChildren(id, k2);
        int best = -1; double bestT = 0;
        foreach (int c in k2) { double t = view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime); if (t > bestT) { bestT = t; best = c; } }
        if (best < 0 || bestT < 0.4 * parentT || bestT < 0.3) break;
        parts.Add(Short(view.GetItemName(best)) + " " + bestT.ToString("F1", ci));
        id = best; parentT = bestT;
    }
    return string.Join(" > ", parts);
}
string ArenaMarkers(HierarchyFrameDataView view, int root)
{
    var acc = new SortedDictionary<string, double>();
    var stack = new Stack<int>(); stack.Push(root);
    var k2 = new List<int>();
    while (stack.Count > 0)
    {
        int id = stack.Pop(); k2.Clear(); view.GetItemChildren(id, k2);
        foreach (int c in k2)
        {
            string n = view.GetItemName(c);
            if (n.StartsWith("Arena.") || n == "VoxelBody.Init" || n.StartsWith("JobHandle.Complete") || n.StartsWith("VoxelStructure"))
            {
                acc.TryGetValue(n, out double v);
                acc[n] = v + view.GetItemColumnDataAsDouble(c, HierarchyFrameDataView.columnTotalTime);
            }
            stack.Push(c);
        }
    }
    var parts = new List<string>();
    foreach (var kv in acc) parts.Add(kv.Key + " " + kv.Value.ToString("F1", ci));
    return string.Join(", ", parts);
}
void ReadFrames(int upTo)
{
    int first = Math.Max(readUpTo + 1, ProfilerDriver.firstFrameIndex);
    for (int f = first; f <= upTo; f++)
    {
        if (skip.Contains(f)) continue;
        using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
        if (view == null || !view.valid) continue;
        int plId = -1; kids.Clear(); view.GetItemChildren(view.GetRootItemID(), kids);
        foreach (int c in kids) if (view.GetItemName(c) == "PlayerLoop") { plId = c; break; }
        if (plId < 0) continue;
        double pl = view.GetItemColumnDataAsDouble(plId, HierarchyFrameDataView.columnTotalTime);
        double gc = view.GetItemColumnDataAsDouble(plId, HierarchyFrameDataView.columnGcMemory);
        string hot = pl > 7.0 ? HotPath(view, plId) : null;
        rows[f] = (pl, gc, hot);
        string am = ArenaMarkers(view, plId);
        if (am.Contains("Arena.Build")) { marks[f] = am; buildFrames.Add(f); }
    }
    readUpTo = Math.Max(readUpTo, upTo);
}

// Windows: name -> (from frame inclusive, to frame inclusive)
var windows = new List<(string name, int from, int to, string extra)>();
var notes = new List<string>();
string Stats(string name, int from, int to, string extra)
{
    var v = new List<double>(); double gcMax = 0; int gcFrames = 0; double gcSum = 0;
    var worst = new List<(double ms, int f, string hot)>();
    foreach (var kv in rows)
    {
        if (kv.Key < from || kv.Key > to || buildFrames.Contains(kv.Key)) continue;
        v.Add(kv.Value.pl);
        if (kv.Value.gc > 0) { gcFrames++; gcSum += kv.Value.gc; gcMax = Math.Max(gcMax, kv.Value.gc); }
        worst.Add((kv.Value.pl, kv.Key, kv.Value.hot));
    }
    if (v.Count == 0) return $"\"{name}\":{{\"n\":0{extra}}}";
    v.Sort(); worst.Sort((a, b) => b.ms.CompareTo(a.ms));
    double sum = 0; foreach (var x in v) sum += x;
    int over = 0; foreach (var x in v) if (x > 16.6) over++;
    var w = new List<string>();
    for (int i = 0; i < Math.Min(3, worst.Count); i++) w.Add($"\"{worst[i].ms.ToString("F1", ci)} ms @{worst[i].f - from}: {worst[i].hot}\"");
    return $"\"{name}\":{{\"n\":{v.Count},\"avg\":{(sum / v.Count).ToString("F2", ci)},\"p50\":{v[v.Count / 2].ToString("F2", ci)},\"p95\":{v[(int)(v.Count * 0.95)].ToString("F2", ci)},\"max\":{v[v.Count - 1].ToString("F2", ci)},\"over16\":{over},\"gcFrames\":{gcFrames},\"gcMax\":{gcMax:F0},\"gcSum\":{gcSum:F0}{extra},\"worst\":[{string.Join(",", w)}]}}";
}

// ---------- Timeline ----------
// Builds at 1, 6, 11, 16, 21 s (n = 1..5); after-window 3 s; fight on arena 5 starting at 26 s:
// burrows at 26, 27, 28; blasts at 29, 30, 31, 32; fight window 26..34 s.
float t0 = Time.realtimeSinceStartup;
var pending = new List<Action>();
var buildMs = new double[6];
var buildFrame = new int[6];
var afterEnd = new int[6];
int nextBuild = 1;
int burrows = 0, blasts = 0, fightFrom = -1, fightTo = -1, openOk = 0;
string houseInfo = "";
int stage = 0;
UnityEngine.Events.UnityAction onRender = () =>
{
    try
    {
        foreach (var a in pending) a();
        pending.Clear();
    }
    catch (Exception e) { notes.Add("render exception: " + e.GetBaseException().Message); }
};
EditorApplication.CallbackFunction onUpdate = null;
void Finish(string status)
{
    EditorApplication.update -= onUpdate;
    Application.onBeforeRender -= onRender;
    ProfilerDriver.enabled = profWasEnabled;
    ProfilerDriver.profileEditor = profWasEditor;
    var parts = new List<string>();
    var builds = new List<string>();
    for (int n = 1; n <= 5; n++)
    {
        string m = marks.TryGetValue(buildFrame[n], out var mm) ? mm : (marks.Count > 0 ? "?" : "");
        // the build frame index can be off by one: look at neighbours
        for (int d = -2; d <= 2 && m == "?"; d++) if (marks.TryGetValue(buildFrame[n] + d, out var m2)) m = m2;
        double plBuild = 0; foreach (int bf in buildFrames) if (Math.Abs(bf - buildFrame[n]) <= 2) plBuild = rows[bf].pl;
        builds.Add($"{{\"n\":{n},\"lastBuildMs\":{buildMs[n].ToString("F1", ci)},\"buildFramePlayerLoop\":{plBuild.ToString("F1", ci)},\"markers\":\"{m}\"}}");
        parts.Add(Stats("after" + n, buildFrame[n] + 1, afterEnd[n], ""));
    }
    parts.Add(Stats("fight", fightFrom, fightTo, $",\"burrowsOpened\":{openOk},\"blasts\":{blasts},\"house\":\"{houseInfo}\""));
    domain.SetData("arenabench_result", $"{{\"status\":\"{status}\",\"burst\":{Unity.Burst.BurstCompiler.IsEnabled.ToString().ToLower()},\"quality\":\"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\",\"builds\":[{string.Join(",", builds)}],{string.Join(",", parts)},\"notes\":\"{string.Join("; ", notes).Replace("\"", "'")}\"}}");
    domain.SetData("arenabench_status", status);
}
onUpdate = () =>
{
    try
    {
        if (!EditorApplication.isPlaying) { notes.Add("left Play"); Finish("aborted"); return; }
        float t = Time.realtimeSinceStartup - t0;
        if (nextBuild <= 5 && t >= 1f + (nextBuild - 1) * 5f && pending.Count == 0)
        {
            int n = nextBuild++;
            pending.Add(() =>
            {
                buildFrame[n] = ProfilerDriver.lastFrameIndex + 1;
                s.Build(s.Chapter.At(n), 500 + n);
                buildMs[n] = s.LastBuildMs;
            });
            domain.SetData("arenabench_status", "running: build " + n);
        }
        for (int n = 1; n <= 5; n++)
            if (afterEnd[n] == 0 && buildFrame[n] > 0 && t >= 1f + (n - 1) * 5f + 3f)
            {
                afterEnd[n] = ProfilerDriver.lastFrameIndex;
                ReadFrames(ProfilerDriver.lastFrameIndex);
                skip.Add(ProfilerDriver.lastFrameIndex + 1); skip.Add(ProfilerDriver.lastFrameIndex + 2);
            }
        if (afterEnd[5] > 0 && t >= 26f)
        {
            if (fightFrom < 0) { fightFrom = ProfilerDriver.lastFrameIndex + 1; domain.SetData("arenabench_status", "running: fight"); }
            if (burrows < 3 && t >= 26f + burrows) { burrows++; pending.Add(() => { if (s.TryOpenBurrow(s.Tank.position, out var p)) openOk++; else notes.Add("burrow not opened"); }); }
            if (blasts < 4 && t >= 29f + blasts)
            {
                blasts++;
                pending.Add(() =>
                {
                    var h = s.Plan.Houses[0];
                    if (houseInfo == "") houseInfo = h.Shape + " " + h.Size + " at " + h.Position;
                    Game.Voxels.VoxelDamage.Blast(h.Position + Vector3.up * 2f, 2.2f, 8f);
                });
            }
            if (ProfilerDriver.lastFrameIndex - readUpTo > 300)
            {
                ReadFrames(ProfilerDriver.lastFrameIndex - 2);
                skip.Add(ProfilerDriver.lastFrameIndex + 1); skip.Add(ProfilerDriver.lastFrameIndex + 2);
            }
            if (t >= 34f && fightTo < 0) { fightTo = ProfilerDriver.lastFrameIndex; }
            if (fightTo > 0 && t >= 34.2f) { ReadFrames(fightTo); Finish("done"); }
        }
        else if (ProfilerDriver.lastFrameIndex - readUpTo > 300)
        {
            ReadFrames(ProfilerDriver.lastFrameIndex - 2);
            skip.Add(ProfilerDriver.lastFrameIndex + 1); skip.Add(ProfilerDriver.lastFrameIndex + 2);
        }
    }
    catch (Exception e) { notes.Add("exception: " + e.GetBaseException().Message); Finish("failed"); }
};
EditorApplication.update += onUpdate;
Application.onBeforeRender += onRender;
domain.SetData("arenabench_update", onUpdate);
domain.SetData("arenabench_render", onRender);
domain.SetData("arenabench_status", "running: armed");
domain.SetData("arenabench_result", null);
return "armed: ~35 s; houses on current plan " + s.Plan.Houses.Count;

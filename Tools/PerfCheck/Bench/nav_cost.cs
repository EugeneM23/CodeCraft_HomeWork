// Cost of the horde navigation (Game.Horde) against a NavMesh rebuild, on the real VoxelSandbox ground.
// Docs/superpowers/specs/2026-11-17-ai-and-horde.md, 3.1. Run in Play, in VoxelSandbox, ~10 s after
// entering Play, TWICE:
//   unity cmd --project-path <worktree> script eval --file Tools/PerfCheck/Bench/nav_cost.cs
// Call 1 measures everything (median of 7, ms) and then carves one real crater; the ground collider is
// rebuilt on workers and swapped a frame or two later. Call 2 (any time after) checks that
// VoxelWorld.ChunkCollidersChanged fired, updates the grid from the dirty bounds it gave, and confirms
// the heights in the crater dropped. Call 2 disposes what call 1 left.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Game.Horde;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

var domain = AppDomain.CurrentDomain;
if (!EditorApplication.isPlaying) return "ERROR not in Play";
string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
if (sceneName != "VoxelSandbox") return "ERROR active scene is " + sceneName + ", not VoxelSandbox";

const int REPEATS = 7;
const int LAYER_GROUND = 3;
const int LAYER_DEFAULT = 0;
var sb = new StringBuilder();

// ======================================================================= call 2: crater check
if (domain.GetData("navcost_grid") is HordeNavGrid pendingGrid)
{
    var events = (List<Bounds>)domain.GetData("navcost_events");
    var handler = (Action<Bounds>)domain.GetData("navcost_handler");
    var crater = (Vector3)domain.GetData("navcost_crater");
    var before = (float[])domain.GetData("navcost_before");
    Game.Voxels.VoxelWorld.ChunkCollidersChanged -= handler;
    domain.SetData("navcost_grid", null);

    sb.AppendLine("Crater at " + crater.ToString("F1") + ": ChunkCollidersChanged fired " + events.Count + " time(s)");
    for (int i = 0; i < events.Count; i++)
        sb.AppendLine("  bounds " + events[i].center.ToString("F1") + " size " + events[i].size.ToString("F1"));

    int rects = pendingGrid.DirtyRectCount;
    int cells = 0;
    for (int i = 0; i < rects; i++)
    {
        int4 r = pendingGrid.GetDirtyRect(i);
        cells += (r.z - r.x + 1) * (r.w - r.y + 1);
    }

    int version = pendingGrid.Version;
    var watch = Stopwatch.StartNew();
    JobHandle h = pendingGrid.ScheduleUpdate();
    h.Complete();
    double rayMs = watch.Elapsed.TotalMilliseconds;
    bool changed = pendingGrid.CompleteUpdate();
    double totalMs = watch.Elapsed.TotalMilliseconds;
    sb.AppendLine("Update from the event: " + rects + " rect(s), " + cells + " rays, raycast " + rayMs.ToString("F3") +
                  " ms, total " + totalMs.ToString("F3") + " ms, changed " + changed + ", version " + version + "->" + pendingGrid.Version);

    int k = 0;
    float maxDrop = 0f;
    float sumDrop = 0f;
    for (int dz = -2; dz <= 2; dz++)
    for (int dx = -2; dx <= 2; dx++)
    {
        var p = new float3(crater.x + dx * 0.5f, 0f, crater.z + dz * 0.5f);
        float drop = before[k++] - pendingGrid.HeightAt(p);
        maxDrop = math.max(maxDrop, drop);
        sumDrop += drop;
    }

    sb.AppendLine("Heights in the crater (5x5 cells round the centre): mean drop " + (sumDrop / 25f).ToString("F2") +
                  " m, max drop " + maxDrop.ToString("F2") + " m");
    pendingGrid.Dispose();
    return sb.ToString();
}

// ======================================================================= call 1: measure
double Median(List<double> values)
{
    values.Sort();
    int n = values.Count;
    return n % 2 == 1 ? values[n / 2] : 0.5 * (values[n / 2 - 1] + values[n / 2]);
}

string Row(string what, List<double> ms, string note = "") =>
    "| " + what + " | " + Median(ms).ToString("F3") + " | " + ms[0].ToString("F3") + " .. " + ms[ms.Count - 1].ToString("F3") + " | " + note + " |";

var start = UnityEngine.Object.FindFirstObjectByType<Game.VoxelSandboxStart>();
Transform tank = start != null && start.Vehicle != null ? start.Vehicle.transform : GameObject.Find("Tank_01")?.transform;
if (tank == null) return "ERROR no tank";

Game.Voxels.VoxelBody terrain = null;
foreach (var body in Game.Voxels.VoxelWorld.Bodies)
    if (body != null && body.IsTerrain) { terrain = body; break; }
if (terrain == null) return "ERROR no terrain body";

sb.AppendLine("Burst enabled: " + Unity.Burst.BurstCompiler.IsEnabled + ", job workers: " + Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount);

var settings = HordeNavGridSettings.Cover(new float2(-40f, -40f), new float2(40f, 40f));
settings.RayTop = 20f;
settings.RayLength = 30f;
var grid = new HordeNavGrid(settings);
var rows = new List<string>();

// (a) BuildAll over the map.
grid.BuildAll(); // warm-up: Burst, ray buffers grown to the whole map
var t = new List<double>();
for (int i = 0; i < REPEATS; i++)
{
    var w = Stopwatch.StartNew();
    grid.BuildAll();
    t.Add(w.Elapsed.TotalMilliseconds);
}
rows.Add(Row("a) BuildAll 80x80 m, 0.5 m (" + grid.CellCount + " rays)", t));

// What the rays hit: one main-thread ray per 2x2 cells, by layer and object name.
int ground = 0, blocked = 0;
var hitNames = new Dictionary<string, int>();
for (int z = 0; z < grid.Size.y; z += 2)
for (int x = 0; x < grid.Size.x; x += 2)
{
    var cell = new int2(x, z);
    if (grid.HasGround(cell)) ground++;
    if (grid.HasGround(cell) && grid.HeightOf(cell) > 1.5f) blocked++;
    float3 c = grid.CellCenter(cell);
    if (Physics.Raycast(new Vector3(c.x, settings.RayTop, c.z), Vector3.down, out RaycastHit hit, settings.RayLength, settings.LayerMask, QueryTriggerInteraction.Ignore))
    {
        var owner = hit.collider.GetComponentInParent<Game.Voxels.VoxelBody>();
        string key = LayerMask.LayerToName(hit.collider.gameObject.layer) + ":" + (owner != null ? owner.name : hit.collider.transform.root.name);
        hitNames[key] = hitNames.TryGetValue(key, out int n) ? n + 1 : 1;
    }
}
var hitList = new List<KeyValuePair<string, int>>(hitNames);
hitList.Sort((p, q) => q.Value.CompareTo(p.Value));
var hitText = new StringBuilder();
for (int i = 0; i < hitList.Count && i < 16; i++) hitText.Append(hitList[i].Key).Append(" x").Append(hitList[i].Value).Append("; ");

// (b) One dirty chunk 6.4 x 6.4 m.
var chunkBounds = new Bounds(new Vector3(9.6f, 0f, 9.6f), new Vector3(6.4f, 8f, 6.4f));
var tRay = new List<double>();
var tTotal = new List<double>();
long gcBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < REPEATS + 1; i++)
{
    var w = Stopwatch.StartNew();
    grid.MarkDirty(chunkBounds);
    JobHandle h = grid.ScheduleUpdate();
    h.Complete();
    double ray = w.Elapsed.TotalMilliseconds;
    grid.CompleteUpdate();
    if (i == 0) { gcBefore = GC.GetAllocatedBytesForCurrentThread(); continue; }
    tRay.Add(ray);
    tTotal.Add(w.Elapsed.TotalMilliseconds);
}
long gcGrid = GC.GetAllocatedBytesForCurrentThread() - gcBefore;
grid.MarkDirty(chunkBounds);
int4 rect = grid.GetDirtyRect(0);
int chunkRays = (rect.z - rect.x + 1) * (rect.w - rect.y + 1);
var tSchedule = new List<double>();
for (int i = 0; i < REPEATS; i++)
{
    grid.MarkDirty(chunkBounds);
    var w = Stopwatch.StartNew();
    grid.ScheduleUpdate();
    tSchedule.Add(w.Elapsed.TotalMilliseconds);
    grid.CompleteUpdate();
}
rows.Add(Row("b) chunk update: schedule only (main thread)", tSchedule, chunkRays + " rays"));
rows.Add(Row("b) chunk update: schedule + rays done", tRay, chunkRays + " rays"));
rows.Add(Row("b) chunk update: total incl. apply", tTotal, "GC " + gcGrid + " B over " + REPEATS + " updates"));

// (c) Flow field over the map to the tank.
var field = new HordeFlowField(grid);
float3 target = tank.position;
field.Schedule(target); field.Complete(); // warm-up
var tFlowSched = new List<double>();
var tFlow = new List<double>();
gcBefore = GC.GetAllocatedBytesForCurrentThread();
for (int i = 0; i < REPEATS; i++)
{
    var w = Stopwatch.StartNew();
    field.Schedule(target);
    tFlowSched.Add(w.Elapsed.TotalMilliseconds);
    field.Complete();
    tFlow.Add(w.Elapsed.TotalMilliseconds);
}
long gcFlow = GC.GetAllocatedBytesForCurrentThread() - gcBefore;
int reachable = 0;
for (int i = 0; i < grid.CellCount; i++)
    if (!field.IsUnreachable(new int2(i % grid.Size.x, i / grid.Size.x))) reachable++;
rows.Add(Row("c) flow field 160x160 to tank: schedule (main)", tFlowSched));
rows.Add(Row("c) flow field 160x160 to tank: schedule + complete", tFlow, reachable + " of " + grid.CellCount + " cells reachable; GC " + gcFlow + " B"));

// (e1) A* on the real map, corner to corner.
var finder = new HordePathfinder(grid);
var a0 = new float3(-36f, 0f, -36f);
var a1 = new float3(36f, 0f, 36f);
finder.FindPath(a0, a1);
var tPathReal = new List<double>();
for (int i = 0; i < REPEATS; i++)
{
    var w = Stopwatch.StartNew();
    finder.FindPath(a0, a1);
    tPathReal.Add(w.Elapsed.TotalMilliseconds);
}
rows.Add(Row("e) A* real map (-36,-36)->(36,36), 102 m", tPathReal, finder.Status + ", " + finder.ExpandedCells + " expanded, " + finder.Path.Length + " waypoints"));
finder.Dispose();

// (d) Synthetic 400x400 (200x200 m): 6x6 m "houses" on a 12 m pitch (6 m streets) and three long walls with one gap each.
var bigSettings = HordeNavGridSettings.Cover(new float2(0f, 0f), new float2(200f, 200f));
var big = new HordeNavGrid(bigSettings);
using (var heights = new NativeArray<float>(big.CellCount, Allocator.Temp))
{
    var heightsCopy = heights;
    var rng = new Unity.Mathematics.Random(12345);
    for (int i = 0; i < heightsCopy.Length; i++) heightsCopy[i] = rng.NextFloat(0f, 0.2f);
    for (int hz = 8; hz < 392; hz += 24)
    for (int hx = 8; hx < 392; hx += 24)
        for (int z = hz; z < hz + 12; z++)
        for (int x = hx; x < hx + 12; x++) heightsCopy[z * 400 + x] = 5f;
    for (int wall = 100; wall < 400; wall += 100)
        for (int z = 0; z < 400; z++)
            if (z < 190 || z > 196) heightsCopy[z * 400 + wall + 2] = 4f; // one 3.5 m gap, in a street
    big.SetHeights(heightsCopy);
}
var bigField = new HordeFlowField(big);
var bigTarget = new float3(97f, 0f, 97f); // a street crossing
bigField.Schedule(bigTarget); bigField.Complete();
var tBig = new List<double>();
var tBigSched = new List<double>();
for (int i = 0; i < REPEATS; i++)
{
    var w = Stopwatch.StartNew();
    bigField.Schedule(bigTarget);
    tBigSched.Add(w.Elapsed.TotalMilliseconds);
    bigField.Complete();
    tBig.Add(w.Elapsed.TotalMilliseconds);
}
rows.Add(Row("d) flow field 400x400 synthetic: schedule (main)", tBigSched));
rows.Add(Row("d) flow field 400x400 synthetic: schedule + complete", tBig, "160 000 cells"));

// (e2) A* across ~100 m of the synthetic map, through the walls' gaps.
var bigFinder = new HordePathfinder(big);
var b0 = new float3(13f, 0f, 13f);
var b1 = new float3(109f, 0f, 13f);
bigFinder.FindPath(b0, b1);
var tBigPath = new List<double>();
for (int i = 0; i < REPEATS; i++)
{
    var w = Stopwatch.StartNew();
    bigFinder.FindPath(b0, b1);
    tBigPath.Add(w.Elapsed.TotalMilliseconds);
}
float pathLen = 0f;
for (int i = 1; i < bigFinder.Path.Length; i++) pathLen += math.distance(bigFinder.Path[i - 1], bigFinder.Path[i]);
rows.Add(Row("e) A* synthetic 96 m apart, wall gap 85 m off the line", tBigPath, bigFinder.Status + ", " + bigFinder.ExpandedCells + " expanded, " + bigFinder.Path.Length + " waypoints, " + pathLen.ToString("F0") + " m"));
bigFinder.Dispose();
bigField.Dispose();
big.Dispose();

// (f) NavMesh for comparison.
NavMeshBuildSettings nav = NavMesh.GetSettingsByIndex(0);
nav.agentRadius = 0.5f;
nav.agentHeight = 2f;
nav.agentClimb = 0.6f;
nav.agentSlope = 45f;
int navMask = (1 << LAYER_GROUND) | (1 << LAYER_DEFAULT);
var markups = new List<NavMeshBuildMarkup>();
var sources = new List<NavMeshBuildSource>();

void NavRows(string what, Bounds bounds, int repeats, bool smallTiles)
{
    var b = nav;
    if (smallTiles) { b.overrideTileSize = true; b.tileSize = 64; }
    var tCollect = new List<double>();
    var tBuild = new List<double>();
    int sourceCount = 0;
    for (int i = 0; i < repeats + 1; i++)
    {
        var w = Stopwatch.StartNew();
        NavMeshBuilder.CollectSources(bounds, navMask, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        double collect = w.Elapsed.TotalMilliseconds;
        sourceCount = sources.Count;
        var data = new NavMeshData(b.agentTypeID);
        w.Restart();
        NavMeshBuilder.UpdateNavMeshData(data, b, sources, bounds);
        double build = w.Elapsed.TotalMilliseconds;
        UnityEngine.Object.DestroyImmediate(data);
        if (i == 0) continue;
        tCollect.Add(collect);
        tBuild.Add(build);
    }
    rows.Add(Row("f) NavMesh " + what + ": CollectSources (main)", tCollect, sourceCount + " sources"));
    rows.Add(Row("f) NavMesh " + what + ": UpdateNavMeshData (sync)", tBuild, smallTiles ? "tileSize 64 (10.7 m)" : "default tileSize 256 (42.7 m)"));
}

var navChunk = chunkBounds;
navChunk.Expand(new Vector3(1f, 0f, 1f)); // + agent radius on each side
NavRows("chunk 6.4 m", navChunk, REPEATS, false);
NavRows("chunk 6.4 m", navChunk, REPEATS, true);
NavRows("map 80x80 m", new Bounds(new Vector3(0f, 2.8f, 0f), new Vector3(80f, 8f, 80f)), 5, false);

// ---------- Crater for call 2
field.Dispose();
int2 tankCell = grid.ClampCell(tank.position);
Vector3 craterAt = Vector3.zero;
bool foundSpot = false;
for (int radius = 16; radius < 60 && !foundSpot; radius += 2)
for (int a = 0; a < 16 && !foundSpot; a++)
{
    float ang = a * Mathf.PI / 8f;
    int2 c = tankCell + new int2((int)(math.cos(ang) * radius), (int)(math.sin(ang) * radius));
    if (!grid.InBounds(c - 8) || !grid.InBounds(c + 8)) continue;
    float h0 = grid.HeightOf(c);
    bool flat = !float.IsNaN(h0) && h0 < 1f;
    for (int dz = -8; dz <= 8 && flat; dz++)
    for (int dx = -8; dx <= 8 && flat; dx++)
        flat = math.abs(grid.HeightOf(c + new int2(dx, dz)) - h0) < 0.3f;
    if (!flat) continue;
    craterAt = (Vector3)grid.CellCenter(c);
    foundSpot = true;
}

if (foundSpot)
{
    var before = new float[25];
    int k = 0;
    for (int dz = -2; dz <= 2; dz++)
    for (int dx = -2; dx <= 2; dx++)
        before[k++] = grid.HeightAt(new float3(craterAt.x + dx * 0.5f, 0f, craterAt.z + dz * 0.5f));

    var events = new List<Bounds>();
    HordeNavGrid g = grid;
    Action<Bounds> handler = b => { events.Add(b); g.MarkDirty(b); };
    Game.Voxels.VoxelWorld.ChunkCollidersChanged += handler;
    domain.SetData("navcost_grid", grid);
    domain.SetData("navcost_events", events);
    domain.SetData("navcost_handler", handler);
    domain.SetData("navcost_crater", craterAt);
    domain.SetData("navcost_before", before);
    int carved = terrain.CarveCrater(craterAt, 3f, 35f, 6f, 0);
    sb.AppendLine("Crater carved at " + craterAt.ToString("F1") + " (r 3 m): " + carved + " voxels. Run the script again to check it.");
}
else
{
    sb.AppendLine("No flat spot for a crater found near the tank");
    grid.Dispose();
}

sb.AppendLine("Ray hits by layer:object (1 ray per 2x2 cells): " + hitText);
sb.AppendLine("Cells with ground " + ground + " / " + (grid.CellCount / 4) + " sampled, higher than 1.5 m: " + blocked);
sb.AppendLine();
sb.AppendLine("| step | median ms | min .. max | note |");
sb.AppendLine("|---|---|---|---|");
foreach (string r in rows) sb.AppendLine(r);
return sb.ToString();

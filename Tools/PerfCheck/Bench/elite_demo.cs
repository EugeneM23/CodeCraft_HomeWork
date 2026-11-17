// Elite Regax demo (spec 2026-11-17-ai-and-horde.md section 4). Run in Play, in VoxelSandbox, with one call:
//   unity cmd --project-path <worktree> script eval --file Tools/PerfCheck/Bench/elite_demo.cs
// Optional AppDomain data, set before the call (a one-line script eval):
//   "elitedemo_n"        int    elites to spawn                                  default 3
//   "elitedemo_seconds"  float  how long it watches                              default 20
//   "elitedemo_strikes"  bool   leave the sandbox's air strikes on               default false
// It carves a crater 12 m ahead of the tank (VoxelBody.CarveCrater on the terrain), spawns the elites 20-30 m from the
// tank beyond it (Assets/Game/Prefabs/Characters/Regax_Elite.prefab, EliteRegaxSpawn), and returns at once. While it
// watches, it logs every 0.5 s the tank's health and each elite's state, distance to the hull and position, and the
// cost of the AI step (ProfilerRecorder on "AI.Step", main thread). Read the log with:
//   unity cmd ... script eval --code "return System.AppDomain.CurrentDomain.GetData(\"elitedemo_log\") as string;"
// "elitedemo_status" says running / done. Leaving Play stops it.
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

var domain = AppDomain.CurrentDomain;
if (!EditorApplication.isPlaying) return "ERROR not in Play";
string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
if (sceneName != "VoxelSandbox") return "ERROR active scene is " + sceneName + ", not VoxelSandbox";
if (domain.GetData("elitedemo_update") is EditorApplication.CallbackFunction oldU) EditorApplication.update -= oldU;

int n = domain.GetData("elitedemo_n") is int nn ? nn : 3;
float seconds = domain.GetData("elitedemo_seconds") is float ss ? ss : 20f;
bool strikesOn = domain.GetData("elitedemo_strikes") is bool so && so;
var inv = System.Globalization.CultureInfo.InvariantCulture;

var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Characters/Regax_Elite.prefab");
if (prefab == null) return "ERROR no Regax_Elite.prefab (Tools/AI/Build Regax Elite)";

var start = UnityEngine.Object.FindFirstObjectByType<Game.VoxelSandboxStart>();
Transform tank = start != null && start.Vehicle != null ? start.Vehicle.transform : GameObject.Find("Tank_01")?.transform;
if (tank == null) return "ERROR no tank";
var tankEntity = tank.GetComponent<Atomic.Entities.MonoEntity>();
Game.Health tankHealth = tankEntity != null && Game.EntityAPI.TryGetHealth(tankEntity, out Game.Health th) ? th : null;

if (!strikesOn)
{
    var onField = typeof(Game.VoxelAirStrike).GetField("_on", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    foreach (var strike in UnityEngine.Object.FindObjectsByType<Game.VoxelAirStrike>(FindObjectsSortMode.None)) onField.SetValue(strike, false);
}

// ---------- the crater between the tank and the elites ----------
Vector3 forward = tank.forward; forward.y = 0f; forward.Normalize();
Vector3 craterAt = tank.position + forward * 12f;
string crater = "no terrain under the crater";
if (Physics.Raycast(craterAt + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 60f, 1 << Game.PhysicsLayers.Ground))
{
    var body = ground.collider.GetComponentInParent<Game.Voxels.VoxelBody>();
    if (body != null)
    {
        int carved = body.CarveCrater(ground.point, 5f, 35f, 2f, 100);
        crater = $"crater r5 at {ground.point.ToString("F1")} ({carved} voxels)";
    }
}

// ---------- the elites, 20-30 m out, beyond the crater ----------
var elites = new List<GameObject>();
for (int i = 0; i < n; i++)
{
    float angle = n == 1 ? 0f : -30f + 60f * i / (n - 1);
    float distance = 20f + 10f * (i % 3) / 2f;
    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
    Vector3 at = Game.AI.EliteRegaxSpawn.GroundNear(tank.position, direction, distance);
    GameObject elite = Game.AI.EliteRegaxSpawn.Spawn(prefab, at, -direction);
    elite.name = prefab.name + "_" + i;
    elites.Add(elite);
}

// ---------- watch ----------
var log = new StringBuilder();
log.AppendLine($"{crater}; {n} elites; tank at {tank.position.ToString("F1")} health {(tankHealth != null ? tankHealth.GetCurrent() : -1)}");
var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AI.Step", 4096);
float began = Time.time, nextLine = 0f;
double stepSum = 0; double stepMax = 0; int stepFrames = 0;
EditorApplication.CallbackFunction onUpdate = null;
onUpdate = () =>
{
    try
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= onUpdate; recorder.Dispose(); domain.SetData("elitedemo_status", "aborted: left Play"); return; }
        if (recorder.Valid && recorder.LastValue > 0) { double ms = recorder.LastValue * 1e-6; stepSum += ms; stepMax = Math.Max(stepMax, ms); stepFrames++; }
        float t = Time.time - began;
        if (t < nextLine) return;
        nextLine = t + 0.5f;
        var tankState = Game.AI.LevelAI.Current != null ? Game.AI.AIAPI.GetPlayerVehicle(Game.AI.LevelAI.Current) : null;
        log.Append($"t={t.ToString("F1", inv)} hp={(tankHealth != null ? tankHealth.GetCurrent() : -1)}");
        foreach (var e in elites)
        {
            if (e == null) { log.Append(" | gone"); continue; }
            var ch = e.GetComponent<AIKit.AICharacter>();
            float hull = tankState != null ? tankState.DistanceToHull(e.transform.position) : -1f;
            string state = ch != null && ch.State != null ? ch.State.DisplayName : "-";
            string decision = ch != null && ch.Graph != null && ch.Graph.Decision != null ? ch.Graph.Decision.DisplayName : "-";
            log.Append($" | {decision}/{state} hull={hull.ToString("F1", inv)} y={e.transform.position.y.ToString("F2", inv)} at=({e.transform.position.x.ToString("F1", inv)},{e.transform.position.z.ToString("F1", inv)})");
        }
        log.AppendLine();
        domain.SetData("elitedemo_log", log.ToString());
        if (t >= seconds)
        {
            log.AppendLine($"AI.Step avg {(stepFrames > 0 ? stepSum / stepFrames : 0).ToString("F3", inv)} ms, max {stepMax.ToString("F3", inv)} ms over {stepFrames} frames, {n} elites");
            domain.SetData("elitedemo_log", log.ToString());
            domain.SetData("elitedemo_status", "done");
            EditorApplication.update -= onUpdate;
            recorder.Dispose();
        }
    }
    catch (Exception ex)
    {
        log.AppendLine("exception: " + ex);
        domain.SetData("elitedemo_log", log.ToString());
        domain.SetData("elitedemo_status", "failed");
        EditorApplication.update -= onUpdate;
        recorder.Dispose();
    }
};
EditorApplication.update += onUpdate;
domain.SetData("elitedemo_update", onUpdate);
domain.SetData("elitedemo_status", "running");
domain.SetData("elitedemo_log", log.ToString());
return $"spawned {n} elites; {crater}; watching {seconds} s";

using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Atomic.Entities;
using Game.AIKit.Adapters;
using Game.Horde;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The sandbox's live horde (SandboxHorde): it is there on load, the level grid covers the whole map, a group of
    /// crawlers rises on the grass, comes at the standing tank and leaps on; the kills count in the run (kills and
    /// experience orbs); a wiped-out group is followed by a new one; off clears it. Frames and a report go to
    /// Logs/sandbox-horde/. No WaitForEndOfFrame: runs in -batchmode with a graphics device.
    /// </summary>
    public sealed class SandboxHordeSceneTests
    {
        private const int SHOT_WIDTH = 1280;
        private const int SHOT_HEIGHT = 720;
        private readonly StringBuilder _report = new();
        private Transform _tank;
        private bool _loggedRider;

        [UnityTest, Timeout(240000)]
        public IEnumerator SandboxHasLiveZombies_ThatComeLeapOn_AndRespawn()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Game/Voxels/Scenes/VoxelSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            SandboxHordeBehaviour sandbox = SandboxHorde.Active;
            Assert.That(sandbox, Is.Not.Null, "the sandbox made its live horde");
            GameContext context = GameContext.Instance;
            context.TryGetPlayerContext(out PlayerContext player);
            IEntity tankEntity = player.GetCharacter().Value;
            _tank = tankEntity is Component c ? c.transform : null;
            Assert.IsTrue(context.TryGetRun(out IRunService run) && run != null);
            float respawnDelay = sandbox.Config.respawnDelay;
            s_run = run;
            sandbox.Config.respawnDelay = 2f; // the test does not wait the 12 s of the config (the timer is armed while a group lives)
            try
            {
                // 1. The first group rises and comes.
                float t = 0f;
                while (t < 8f && sandbox.Groups == 0)
                {
                    yield return null;
                    t += Time.deltaTime;
                }

                Assert.AreEqual(1, sandbox.Groups, "the first group came");
                HordeNavGridSettings grid = sandbox.Horde.Grid.Settings;
                Line($"first group after {t:0.0}s: {sandbox.Alive} alive; grid origin {grid.Origin} size {grid.Size}, moved back over the map {sandbox.NavRestores}x");
                Assert.LessOrEqual(grid.Origin.x, -39.5f, "the grid covers the tank's start");
                yield return Wait(1.2f);
                Shot("1-group-rises", Vector3.Lerp(_tank.position, sandbox.Config.spawnCenter, 0.35f) + new Vector3(0f, 6f, -6f),
                    sandbox.Config.spawnCenter);

                int killsBefore = run.Kills;
                int maxRiders = 0;
                float nearest = float.MaxValue;
                t = 0f;
                bool riderShot = false;
                while (t < 25f && sandbox.Alive > 0 && !(maxRiders >= 3 && t > 8f))
                {
                    yield return null;
                    t += Time.deltaTime;
                    int riders = sandbox.Horde.Riders.Count;
                    maxRiders = Mathf.Max(maxRiders, riders);
                    if (sandbox.Horde.Targets.TryFindNearest(_tank.position, 100f, out _, out _, out Vector3 at))
                        nearest = Mathf.Min(nearest, Vector3.Distance(at, _tank.position));
                    if (riders >= 1 && maxRiders == 1 && !_loggedRider)
                    {
                        _loggedRider = true;
                        HordeRiderInfo r0 = sandbox.Horde.Riders.GetRider(0);
                        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
                        var ht = (Transform)typeof(HordeBehaviour).GetField("_tankTransform", bf).GetValue(sandbox.Horde);
                        Line($"  first rider: state {r0.State} point {r0.Point} feet {(Vector3)r0.Feet}; horde tank {(ht != null ? ht.name + " " + ht.position : "none")}; test tank {_tank.name} {_tank.position}; run {run.GetType().Name} state {run.State}");
                    }

                    if (!riderShot && riders >= 2)
                    {
                        riderShot = true;
                        Shot("2-riders", _tank.position + _tank.right * 6f + _tank.forward * 2f + Vector3.up * 3f, _tank.position + Vector3.up);
                    }

                    if (Mathf.FloorToInt(t * 0.5f) != Mathf.FloorToInt((t - Time.deltaTime) * 0.5f))
                        Line($"  t {t:0.0}: alive {sandbox.Alive}, riders {riders}, nearest {nearest:0.0} m, run kills {run.Kills}, orbs {Orbs()}");
                }

                Line($"group 1 over {t:0.0}s: most riders {maxRiders}, nearest {nearest:0.0} m, alive {sandbox.Alive}, run kills +{run.Kills - killsBefore}, orbs {Orbs()}");
                Assert.Less(nearest, 4f, "the group reached the tank");
                Assert.Greater(maxRiders, 0, "some leapt on");

                // 1b. The tank moves off 10 m (its body pushed along after each physics step: the drive input does not
                // turn the tracks in batchmode): the riders ride along, the crowd follows.
                Rigidbody body = _tank.GetComponentInParent<Rigidbody>();
                Vector3 start = _tank.position;
                Vector3 dir = _tank.forward;
                dir.y = 0f;
                dir.Normalize();
                int ridersBefore = sandbox.Horde.Riders.Count;
                float moved = 0f;
                int frame = 0;
                while (moved < 10f)
                {
                    yield return new WaitForFixedUpdate();
                    float step = 3.5f * Time.fixedDeltaTime;
                    moved += step;
                    if (body != null)
                    {
                        body.position += dir * step;
                        body.linearVelocity = dir * 3.5f;
                    }

                    if (frame++ % 15 == 0)
                    {
                        yield return null; // the horde draws in the frame's update: shoot after it
                        Shot($"1b-chase-{frame / 15:00}", _tank.position + _tank.right * 8f - dir * 3f + Vector3.up * 4f, _tank.position + dir * 2f);
                    }
                }

                yield return Wait(0.5f);
                float chaseNearest = sandbox.Horde.Targets.TryFindNearest(_tank.position, 100f, out _, out _, out Vector3 near)
                    ? Vector3.Distance(near, _tank.position) : -1f;
                int farBehind = 0;
                HordeSimulation chaseSim = sandbox.Horde.Simulation;
                for (int i = 0; i < chaseSim.HighWater; i++)
                    if (chaseSim.IsSlotLive(i) && Vector3.Distance(chaseSim.GetTargetPosition(i), _tank.position) > 8f)
                        farBehind++;
                Line($"tank moved {Vector3.Distance(start, _tank.position):0.0} m: riders {ridersBefore} -> {sandbox.Horde.Riders.Count}, nearest {chaseNearest:0.0} m, more than 8 m behind {farBehind} of {sandbox.Alive}");

                // 2. Wipe it out: the kills count in the run, a new group follows.
                sandbox.Horde.KillAll();
                yield return Wait(0.5f);
                int killed = run.Kills - killsBefore;
                Line($"wiped: run kills +{killed}, orbs on the ground {Orbs()}, level {run.Level}, xp {run.Experience}, run {run.State}, tank health {(tankEntity.TryGetHealth(out var hp) ? hp.GetCurrent().ToString() : "?")}");
                Assert.Greater(killed, 0, "the sandbox horde's kills count in the run");
                Shot("3-wiped", _tank.position + _tank.right * 7f + Vector3.up * 4f, _tank.position);

                t = 0f;
                while (t < 6f && sandbox.Groups < 2 && TendAnd(true))
                {
                    yield return null;
                    t += Time.deltaTime;
                    if (Mathf.FloorToInt(t) != Mathf.FloorToInt(t - Time.deltaTime))
                        Line($"  respawn wait {t:0.0}: next in {sandbox.NextGroupIn:0.0}, alive {sandbox.Alive}, pending level-ups {run.PendingLevelUps}, offer {run.SkillOffer.Count}, loop paused {(GameTicks.Loop != null && GameTicks.Loop.IsPaused)}, timeScale {Time.timeScale}, enabled {SandboxHorde.Enabled}, cfg delay {sandbox.Config.respawnDelay}, same {ReferenceEquals(sandbox, SandboxHorde.Active)}, groups {sandbox.Groups}");
                }

                Line($"next group after {t:0.0}s (delay set to 2 s for the test, {respawnDelay} s in the config): {sandbox.Alive} alive");
                Assert.AreEqual(2, sandbox.Groups, "a new group followed the wiped one");

                // 3. Off: cleared at once, no new group.
                SandboxHorde.Enabled = false;
                yield return Wait(0.3f);
                Line($"off: alive {sandbox.Alive}");
                Assert.AreEqual(0, sandbox.Alive);
                yield return Wait(3f);
                Assert.AreEqual(2, sandbox.Groups, "no group while off");
            }
            finally
            {
                sandbox.Config.respawnDelay = respawnDelay;
                SandboxHorde.Enabled = true;
                string dir = Dir();
                File.WriteAllText(Path.Combine(dir, "report.txt"), _report.ToString());
                Debug.Log("[SandboxHordeTest]\n" + _report);
            }
        }

        private static IRunService s_run;

        /// <summary>A level-up pauses the game until a skill is chosen: the test picks the first card.</summary>
        private static void TendSkills()
        {
            if (s_run != null && s_run.PendingLevelUps > 0 && s_run.SkillOffer.Count > 0)
                s_run.ChooseSkill(s_run.SkillOffer[0]);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime, TendSkills())
                yield return null;
        }

        private static bool TendAnd(bool value)
        {
            TendSkills();
            return value;
        }

        private static int Orbs() =>
            GameContext.Instance.TryGetBehaviour(out XpOrbField field) && field != null ? field.Buffer.Count : 0;

        private void Line(string text)
        {
            _report.AppendLine(text);
            Debug.Log("[SandboxHordeTest] " + text);
        }

        private static string Dir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "sandbox-horde"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Shot(string name, Vector3 from, Vector3 at)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Transform ct = cam.transform;
            ct.GetPositionAndRotation(out Vector3 oldPosition, out Quaternion oldRotation);
            var rt = new RenderTexture(SHOT_WIDTH, SHOT_HEIGHT, 24);
            RenderTexture oldTarget = cam.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                ct.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                texture = new Texture2D(SHOT_WIDTH, SHOT_HEIGHT, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, SHOT_WIDTH, SHOT_HEIGHT), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(Dir(), name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                ct.SetPositionAndRotation(oldPosition, oldRotation);
                cam.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                rt.Release();
                Object.Destroy(rt);
                if (texture != null)
                    Object.Destroy(texture);
            }
        }
    }
}

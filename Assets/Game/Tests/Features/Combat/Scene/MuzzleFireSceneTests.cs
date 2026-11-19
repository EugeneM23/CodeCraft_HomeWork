using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using Game.AIKit.Adapters;
using Game.Horde;
using Game.Voxels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Voxel fire and sparks at the muzzle (VoxelMuzzleFire) in VoxelSandbox: a cannon shot throws a burst with a
    /// flash that is gone within half a second, the machine gun's rounds a small tongue each, and an A/B of frame
    /// time with continuous machine-gun fire into a horde of 60 plus a cannon shot a second (nothing, the hot points
    /// only, hot points and muzzle fire). Saves muzzle close-ups to Logs/muzzle-fire/ under the project folder. No
    /// WaitForEndOfFrame: runs in batchmode with graphics.
    /// </summary>
    public sealed class MuzzleFireSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string HORDE_ART = "Assets/Game/Horde/Art/ZombieHordeArtConfig.asset";
        private const int SHOT_WIDTH = 960;
        private const int SHOT_HEIGHT = 540;
        private static readonly float[] Moments = { 0f, 0.03f, 0.06f, 0.1f, 0.15f, 0.22f, 0.3f, 0.4f };
        private static readonly float[] HeMoments = { 0f, 0.04f, 0.08f, 0.15f, 0.25f, 0.4f, 0.6f, 0.9f };

        private Vector3 _aim;
        private bool _firing;
        private TriggerHolder _hold;

        /// <summary>The right button held while _firing; see MgGlowSceneTests.TriggerHolder.</summary>
        [DefaultExecutionOrder(10000)]
        private sealed class TriggerHolder : MonoBehaviour
        {
            public MuzzleFireSceneTests Test;

            private void LateUpdate()
            {
                if (Test != null && Test._firing)
                    CombatFocus.Set(Test._aim);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_hold != null)
                Object.Destroy(_hold.gameObject);
            _hold = null;
            _firing = false;
            CombatFocus.Clear();
            BulletHotSpots.Enabled = true;
            VoxelMuzzleFire.Enabled = true;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CannonShot_ThrowsVoxelFireSparksAndAFlash_GoneInHalfASecond()
        {
            yield return LoadSandbox();
            MonoEntity tank = FindTank();
            Assert.That(tank, Is.Not.Null, "the player's tank");
            Assert.That(tank.TryGetWeapon(out IReactiveVariable<IEntity> cannonSlot) && cannonSlot.Value != null, "the cannon");
            IEntity cannon = cannonSlot.Value;
            Assert.That(cannon.TryGetFirePoint(out Transform muzzle), "the cannon's fire point");

            var log = new List<string>();
            int burst = 0;
            int lights = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool on = pass == 1;
                VoxelMuzzleFire.Enabled = on;
                // From the side and a little behind: the barrel's end, the burst and the smoke out ahead of it.
                Vector3 from = muzzle.position + muzzle.right * 3.2f - muzzle.forward * 0.8f + Vector3.up * 0.9f;
                Vector3 at = muzzle.position + muzzle.forward * 1.6f;

                cannon.GetFireAction().Invoke();
                int moment = 0;
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    yield return null;
                    if (on)
                    {
                        burst = Mathf.Max(burst, VoxelMuzzleFire.Alive);
                        lights = Mathf.Max(lights, VoxelMuzzleFire.ActiveLights);
                    }
                    if (moment < Moments.Length && t >= Moments[moment])
                    {
                        Shot($"cannon_{(on ? "B_on" : "A_off")}_{moment}_t{t:F2}", from, at, log);
                        moment++;
                    }
                }
                Assert.That(VoxelMuzzleFire.Alive, Is.EqualTo(0), "the fire is out 0.7 s after the shot");
                Assert.That(VoxelMuzzleFire.ActiveLights, Is.EqualTo(0), "the flash is out");

                // Let the smoke clear before the next pass.
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                    yield return null;
            }

            File.WriteAllLines(Path.Combine(ShotDir(), "cannon_log.txt"), log);
            Debug.Log($"[MuzzleFireTest] cannon: burst {burst} cubes, {lights} flash light(s)");
            Assert.That(burst, Is.GreaterThan(60), "flame, brake jets and sparks");
            Assert.That(lights, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator MachineGunFire_EveryRoundAtTheMuzzle_StaysUnderTheLightCap()
        {
            yield return LoadSandbox();
            MonoEntity tank = FindTank();
            Assert.That(tank, Is.Not.Null, "the player's tank");
            Assert.That(tank.TryGetSecondaryWeapon(out IReactiveVariable<IEntity> mgSlot) && mgSlot.Value != null, "the machine gun");
            IEntity mg = mgSlot.Value;
            Assert.That(mg.TryGetFirePoint(out Transform muzzle), "the machine gun's fire point");

            _aim = Ground(tank.transform.position + tank.transform.forward * 9f);
            HoldFire();

            // Unrecorded warm-up: the gun turns to the aim point first.
            _firing = true;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                yield return null;

            var log = new List<string>();
            int maxAlive = 0;
            int maxLights = 0;
            int shots = 0;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                yield return null;
                maxAlive = Mathf.Max(maxAlive, VoxelMuzzleFire.Alive);
                maxLights = Mathf.Max(maxLights, BulletHotSpots.ActiveLights);
                if (t >= 1f && shots < 16 && VoxelMuzzleFire.Alive >= 5)
                {
                    Vector3 from = muzzle.position + muzzle.right * 1.8f - muzzle.forward * 1.6f + Vector3.up * 0.9f;
                    Shot($"mg_{shots:00}_t{t:F2}", from, muzzle.position + muzzle.forward * 1.2f, log);
                    shots++;
                }
            }
            _firing = false;
            CombatFocus.Clear();

            File.WriteAllLines(Path.Combine(ShotDir(), "mg_log.txt"), log);
            Debug.Log($"[MuzzleFireTest] machine gun: max {maxAlive} cubes, max {maxLights} lights");
            Assert.That(maxAlive, Is.GreaterThan(0), "the rounds threw fire");
            Assert.That(maxAlive, Is.LessThan(200), "a small tongue per round");
            Assert.That(maxLights, Is.LessThanOrEqualTo(BulletHotSpots.LIGHTS));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator HeShellHit_ThrowsABigSparkBurstAndABrighterHotPoint()
        {
            yield return LoadSandbox();
            MonoEntity tank = FindTank();
            Assert.That(tank, Is.Not.Null, "the player's tank");
            Assert.That(tank.TryGetWeapon(out IReactiveVariable<IEntity> cannonSlot) && cannonSlot.Value != null, "the cannon");
            IEntity cannon = cannonSlot.Value;
            Assert.That(cannon.TryGetFirePoint(out Transform muzzle), "the cannon's fire point");
            Assert.That(AmmoUseCase.GetOrAdd(cannon).Value, Is.EqualTo(ShellKind.HighExplosive), "HE loaded");

            bool blew = false;
            Vector3 blast = default;
            System.Action<Vector3, float> onBlast = (p, r) =>
            {
                if (blew)
                    return;
                blew = true;
                blast = p;
            };
            ExplosionEvents.Exploded += onBlast;
            var log = new List<string>();
            int maxSparks = 0;
            int maxLights = 0;
            try
            {
                // Down at the ground 12 m ahead, for this one shot: the shell has to land.
                Vector3 shotFrom = muzzle.position;
                Vector3 ground = Ground(tank.transform.position + tank.transform.forward * 12f);
                Quaternion aimed = muzzle.localRotation;
                muzzle.rotation = Quaternion.LookRotation(ground - shotFrom);
                cannon.GetFireAction().Invoke();
                muzzle.localRotation = aimed;
                for (float t = 0f; t < 3f && !blew; t += Time.deltaTime)
                    yield return null;
                Assert.That(blew, "the HE shell went off within 3 s");

                // From the shooter's side, 6 m back from the blast and up a little.
                Vector3 back = shotFrom - blast;
                back.y = 0f;
                back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back;
                Vector3 from = blast + back * 6f + Vector3.Cross(Vector3.up, back) * 2f + Vector3.up * 2.2f;
                Vector3 at = blast + Vector3.up * 0.8f;
                int moment = 0;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    maxSparks = Mathf.Max(maxSparks, VoxelSparks.Alive);
                    maxLights = Mathf.Max(maxLights, BulletHotSpots.ActiveLights);
                    yield return null;
                    if (moment < HeMoments.Length && t >= HeMoments[moment])
                    {
                        Shot($"he_{moment}_t{t:F2}", from, at, log);
                        moment++;
                    }
                }
            }
            finally
            {
                ExplosionEvents.Exploded -= onBlast;
            }

            File.WriteAllLines(Path.Combine(ShotDir(), "he_log.txt"), log);
            Debug.Log($"[MuzzleFireTest] HE hit at {blast}: max {maxSparks} sparks, max {maxLights} hot lights");
            Assert.That(maxSparks, Is.GreaterThanOrEqualTo(ShellHitSparksUseCase.SPARKS), "several times a bullet's 26");
            Assert.That(maxLights, Is.InRange(1, BulletHotSpots.LIGHTS));
            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(0), "cooled out");
        }

        /// <summary>
        /// A/B of frame time, in batchmode with graphics (relative numbers only): constant machine-gun fire into a
        /// horde of 60 plus a cannon shot every second, in rounds of (none, hot points, hot points + muzzle fire).
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator PerfAB_ConstantFireIntoHorde_WithAndWithoutTheEffects()
        {
            yield return LoadSandbox();
            MonoEntity tank = FindTank();
            Assert.That(tank, Is.Not.Null, "the player's tank");
            Assert.That(tank.TryGetWeapon(out IReactiveVariable<IEntity> cannonSlot) && cannonSlot.Value != null, "the cannon");
            IEntity cannon = cannonSlot.Value;
            CreateHorde(60);
            for (int i = 0; i < 120; i++)
                yield return new WaitForFixedUpdate();

            HoldFire();
            _firing = true;
            for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
            {
                _aim = AimPoint(tank.transform);
                yield return null;
            }

            string[] names = { "none", "glow", "glow+fire" };
            var sum = new double[3];
            var frames = new int[3];
            var all = new List<float>[] { new(), new(), new() };
            var blocks = new List<string>();
            // ABC, CBA, ABC, CBA: whatever piles up over the run (gibs, blood, dead crawlers) weighs on every config alike.
            for (int round = 0; round < 4; round++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int config = round % 2 == 0 ? k : 2 - k;
                    double blockSum = 0;
                    int blockFrames = 0;
                    BulletHotSpots.Enabled = config >= 1;
                    VoxelMuzzleFire.Enabled = config >= 2;
                    float nextCannon = 0f;
                    for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
                    {
                        _aim = AimPoint(tank.transform);
                        if (t >= nextCannon)
                        {
                            cannon.GetFireAction().Invoke();
                            nextCannon += 1f;
                        }
                        yield return null;
                        if (t < 0.5f)
                            continue; // the last config's effects die out
                        sum[config] += Time.unscaledDeltaTime;
                        frames[config]++;
                        all[config].Add(Time.unscaledDeltaTime * 1000f);
                        blockSum += Time.unscaledDeltaTime;
                        blockFrames++;
                    }
                    blocks.Add($"round {round} {names[config]}: avg {1000.0 * blockSum / Mathf.Max(1, blockFrames):F2} ms");
                }
            }
            _firing = false;
            CombatFocus.Clear();

            var lines = new List<string>();
            for (int config = 0; config < 3; config++)
            {
                all[config].Sort();
                float p95 = all[config].Count > 0 ? all[config][(int)(all[config].Count * 0.95f)] : 0f;
                string line = $"{names[config]}: {frames[config]} frames, avg {1000.0 * sum[config] / Mathf.Max(1, frames[config]):F2} ms, p95 {p95:F2} ms";
                lines.Add(line);
                Debug.Log("[MuzzleFirePerf] " + line);
            }
            lines.AddRange(blocks);
            File.WriteAllLines(Path.Combine(ShotDir(), "perf_ab.txt"), lines);
            Assert.That(frames[2], Is.GreaterThan(0));
        }

        // ---------- Scene ----------

        private static IEnumerator LoadSandbox()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);
            foreach (GameObject placed in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (placed.name.StartsWith("Regax_Elite_"))
                    placed.SetActive(false);
        }

        private void HoldFire()
        {
            _hold = new GameObject("<MuzzleFireTrigger>").AddComponent<TriggerHolder>();
            _hold.Test = this;
        }

        private static MonoEntity FindTank()
        {
            var start = Object.FindFirstObjectByType<VoxelSandboxStart>();
            if (start != null && start.Vehicle != null)
                return start.Vehicle;
            return GameObject.Find("Tank_01")?.GetComponent<MonoEntity>();
        }

        private static Vector3 Ground(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : p;

        /// <summary>The nearest live crawler's body, or the ground 8 m ahead.</summary>
        private static Vector3 AimPoint(Transform tank)
        {
            Vector3 from = tank.position;
            Vector3 best = Ground(from + tank.forward * 8f);
            float bestDistance = 25f;
            foreach (Collider c in Physics.OverlapSphere(from, 25f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c.TryGetComponent(out IEntity e) || !e.HasEnemyTag())
                    continue;
                float d = Vector3.Distance(c.bounds.center, from);
                if (d < bestDistance && d > 3f)
                {
                    bestDistance = d;
                    best = c.bounds.center;
                }
            }
            return best;
        }

        private static void CreateHorde(int population)
        {
            var art = AssetDatabase.LoadAssetAtPath<HordeArtConfig>(HORDE_ART);
            Assert.That(art, Is.Not.Null, HORDE_ART);
            foreach (HordeInstaller old in Object.FindObjectsByType<HordeInstaller>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);
            HordeSpawn.CreateInScene(art, new HordeOptions
            {
                capacity = Mathf.Max(population, 64),
                population = population,
                tankDamageScale = 0f,
                castShadows = false
            });
        }

        // ---------- Screenshots ----------

        private static string ShotDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "muzzle-fire"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Camera.main moved for one render into a PNG at Logs/muzzle-fire/&lt;name&gt;.png, then put back.</summary>
        private static void Shot(string name, Vector3 from, Vector3 at, List<string> log)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            var smokes = VoxelSmoke.Active;
            for (int i = 0; i < smokes.Count; i++)
                smokes[i].VoxelLateUpdate(0f);

            Transform camTransform = cam.transform;
            camTransform.GetPositionAndRotation(out Vector3 oldPosition, out Quaternion oldRotation);
            float oldFov = cam.fieldOfView;
            var rt = new RenderTexture(SHOT_WIDTH, SHOT_HEIGHT, 24);
            RenderTexture oldTarget = cam.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camTransform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                cam.fieldOfView = 50f;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                texture = new Texture2D(SHOT_WIDTH, SHOT_HEIGHT, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, SHOT_WIDTH, SHOT_HEIGHT), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(ShotDir(), name + ".png"), texture.EncodeToPNG());
                log.Add($"{name} cubes={VoxelMuzzleFire.Alive} flash={VoxelMuzzleFire.ActiveLights} hotLights={BulletHotSpots.ActiveLights}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MuzzleFireTest] shot {name} failed: {e.Message}");
            }
            finally
            {
                camTransform.SetPositionAndRotation(oldPosition, oldRotation);
                cam.fieldOfView = oldFov;
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

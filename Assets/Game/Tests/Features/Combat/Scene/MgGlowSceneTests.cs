using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    /// The machine gun's cooling hot points (BulletHotSpots) in VoxelSandbox: a burst into a house wall leaves glowing
    /// points that are out 0.3 s after the last hit, and constant fire into a horde of 60 never burns more than the
    /// light cap. Saves a frame sequence (glow off, then on) to Logs/mg-glow/ under the project folder. No
    /// WaitForEndOfFrame: runs in batchmode with graphics.
    /// </summary>
    public sealed class MgGlowSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string HORDE_ART = "Assets/Game/Horde/Art/ZombieHordeArtConfig.asset";
        private const int SHOT_WIDTH = 960;
        private const int SHOT_HEIGHT = 540;

        private Vector3 _aim;
        private bool _firing;
        private TriggerHolder _hold;

        /// <summary>
        /// Holds the right button for the test: CombatFocus set in a LateUpdate after the fire controller's LateTick
        /// has cleared it, so the auto gun sees it next Update. (Application.onBeforeRender, which perf_bench uses,
        /// never fires in batchmode.)
        /// </summary>
        [DefaultExecutionOrder(10000)]
        private sealed class TriggerHolder : MonoBehaviour
        {
            public MgGlowSceneTests Test;

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
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BurstIntoWall_LeavesHotPoints_ThatCoolOutIn03s()
        {
            yield return LoadSandbox();
            Transform tank = FindTank();
            Assert.That(tank, Is.Not.Null, "the player's tank");

            VoxelBody house = NearestBuilding(tank.position);
            Assert.That(house, Is.Not.Null, "a building in the sandbox");
            Vector3 onWall = PlaceTankBeforeWall(tank, house, out Vector3 away);
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            Vector3 side = Vector3.Cross(Vector3.up, away);
            // In front of the wall, a little to the side: the face the bullets strike.
            Vector3 camFrom = onWall + away * 2.6f + side * 1.1f + Vector3.up * 0.4f;
            HoldFire();

            // Unrecorded warm-up burst: the gun turns to the wall first.
            BulletHotSpots.Enabled = false;
            _firing = true;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                yield return null;
            _firing = false;
            CombatFocus.Clear();
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
                yield return null;

            var log = new List<string>();
            int maxLights = 0;
            int maxSpots = 0;
            float lastSeenAfter = -1f;
            for (int pass = 0; pass < 2; pass++)
            {
                bool glow = pass == 1;
                string tag = glow ? "B_on" : "A_off";
                BulletHotSpots.Enabled = glow;

                _firing = true;
                int frame = 0;
                for (float t = 0f; t < 1f; t += Time.deltaTime)
                {
                    yield return null;
                    if (glow)
                    {
                        maxLights = Mathf.Max(maxLights, BulletHotSpots.ActiveLights);
                        maxSpots = Mathf.Max(maxSpots, BulletHotSpots.ActiveSpots);
                    }
                    if (t >= 0.85f)
                        Shot($"{tag}_fire_{frame++:00}", camFrom, onWall, log);
                }

                _firing = false;
                CombatFocus.Clear();
                frame = 0;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    yield return null;
                    if (glow && BulletHotSpots.ActiveSpots > 0)
                        lastSeenAfter = t;
                    if (glow)
                        maxLights = Mathf.Max(maxLights, BulletHotSpots.ActiveLights);
                    if (t <= 0.45f)
                        Shot($"{tag}_after_{frame++:00}_t{t:F2}", camFrom, onWall, log);
                }

                for (float t = 0f; t < 0.5f; t += Time.deltaTime)
                    yield return null;
            }

            File.WriteAllLines(Path.Combine(ShotDir(), "log.txt"), log);
            Debug.Log($"[MgGlowTest] wall: max lights {maxLights}, max spots {maxSpots}, last glow {lastSeenAfter:F2} s after the burst");

            Assert.That(maxLights, Is.GreaterThan(0), "the burst lit hot points");
            Assert.That(maxLights, Is.LessThanOrEqualTo(BulletHotSpots.LIGHTS));
            Assert.That(maxSpots, Is.GreaterThan(0));
            Assert.That(BulletHotSpots.ActiveSpots, Is.EqualTo(0), "all cooled out");
            Assert.That(BulletHotSpots.ActiveLights, Is.EqualTo(0), "all lights off");
            // The last bullets are still flying when the trigger lets go (~0.15 s), then 0.3 s of glow.
            Assert.That(lastSeenAfter, Is.LessThan(0.55f));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ConstantFireIntoHordeOf60_HitsFlesh_StaysUnderTheLightCap()
        {
            yield return LoadSandbox();
            Transform tank = FindTank();
            Assert.That(tank, Is.Not.Null, "the player's tank");
            CreateHorde(60);
            for (int i = 0; i < 120; i++)
                yield return new WaitForFixedUpdate();

            HoldFire();
            _firing = true;
            int maxLights = 0;
            int maxSpots = 0;
            int frames = 0;
            float maxDt = 0f;
            float sumDt = 0f;
            var log = new List<string>();
            int shot = 0;
            for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
            {
                // The nearest crawler, re-aimed every frame: hits on flesh, the ground behind it on a miss.
                _aim = NearestCrawler(tank.position, out bool found);
                if (!found)
                    _aim = tank.position + tank.forward * 8f;
                yield return null;
                maxLights = Mathf.Max(maxLights, BulletHotSpots.ActiveLights);
                maxSpots = Mathf.Max(maxSpots, BulletHotSpots.ActiveSpots);
                if (t > 1f)
                {
                    frames++;
                    sumDt += Time.unscaledDeltaTime;
                    maxDt = Mathf.Max(maxDt, Time.unscaledDeltaTime);
                }
                if (shot < 2 && t > 2f + shot * 1.5f)
                {
                    shot++;
                    Vector3 from = tank.position - tank.forward * 4f + tank.right * 3f + Vector3.up * 4f;
                    Shot($"C_horde_{shot}", from, _aim, log);
                }
            }

            Debug.Log($"[MgGlowTest] horde 60: max lights {maxLights}, max spots {maxSpots}, frames {frames}, " +
                      $"avg {1000f * sumDt / Mathf.Max(1, frames):F1} ms, worst {1000f * maxDt:F1} ms (batchmode, informative only)");
            Assert.That(maxSpots, Is.GreaterThan(0), "the machine gun hit something");
            Assert.That(maxLights, Is.LessThanOrEqualTo(BulletHotSpots.LIGHTS));
            Assert.That(maxSpots, Is.LessThanOrEqualTo(BulletHotSpots.MAX));
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

        /// <summary>The right button held while _firing (TriggerHolder).</summary>
        private void HoldFire()
        {
            _hold = new GameObject("<MgGlowTrigger>").AddComponent<TriggerHolder>();
            _hold.Test = this;
        }

        private static Transform FindTank()
        {
            var start = Object.FindFirstObjectByType<VoxelSandboxStart>();
            if (start != null && start.Vehicle != null)
                return start.Vehicle.transform;
            return GameObject.Find("Tank_01")?.transform;
        }

        private static VoxelBody NearestBuilding(Vector3 from)
        {
            VoxelBody best = null;
            float bestDistance = float.MaxValue;
            foreach (VoxelStructure s in Object.FindObjectsByType<VoxelStructure>(FindObjectsSortMode.None))
            {
                var body = s.GetComponentInChildren<VoxelBody>();
                if (!s.IsBuilding || body == null)
                    continue;
                float d = Vector3.Distance(body.WorldBounds.center, from);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = body;
                }
            }
            return best;
        }

        /// <summary>The tank 10 m off the building's near wall, facing it; returns the aimed wall point.</summary>
        private Vector3 PlaceTankBeforeWall(Transform tank, VoxelBody house, out Vector3 away)
        {
            Vector3 s0 = tank.position;
            Bounds hb = house.WorldBounds;
            Vector3 near = hb.ClosestPoint(s0);
            away = s0 - near;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.left;
            Vector3 at = Ground(near + away * 10f) + Vector3.up * 0.4f;
            var body = tank.GetComponent<Rigidbody>();
            Quaternion rotation = Quaternion.LookRotation(-away);
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = at;
                body.rotation = rotation;
            }
            tank.SetPositionAndRotation(at, rotation);

            Vector3 eye = at + Vector3.up * 1.5f;
            Vector3 onWall = hb.ClosestPoint(eye);
            onWall.y = Mathf.Clamp(eye.y, hb.min.y + 0.5f, hb.max.y - 0.5f);
            Vector3 inward = hb.center - onWall;
            inward.y = 0f;
            _aim = onWall + (inward.sqrMagnitude > 1e-4f ? inward.normalized : Vector3.zero) * 0.3f;
            return onWall;
        }

        private static Vector3 Ground(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point
                : p;

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

        /// <summary>The nearest live crawler's body (by its collider), 0.8 m up.</summary>
        private static Vector3 NearestCrawler(Vector3 from, out bool found)
        {
            found = false;
            Vector3 best = default;
            float bestDistance = 25f;
            foreach (Collider c in Physics.OverlapSphere(from, 25f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!c.TryGetComponent(out Atomic.Entities.IEntity e) || !e.HasEnemyTag())
                    continue;
                float d = Vector3.Distance(c.bounds.center, from);
                if (d < bestDistance && d > 3f)
                {
                    bestDistance = d;
                    best = c.bounds.center;
                    found = true;
                }
            }
            return best;
        }

        // ---------- Screenshots ----------

        private static string ShotDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "mg-glow"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Camera.main moved for one render into a PNG at Logs/mg-glow/&lt;name&gt;.png, then put back.</summary>
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
                cam.fieldOfView = 55f;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                texture = new Texture2D(SHOT_WIDTH, SHOT_HEIGHT, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, SHOT_WIDTH, SHOT_HEIGHT), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(ShotDir(), name + ".png"), texture.EncodeToPNG());
                log.Add($"{name} spots={BulletHotSpots.ActiveSpots} lights={BulletHotSpots.ActiveLights}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MgGlowTest] shot {name} failed: {e.Message}");
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

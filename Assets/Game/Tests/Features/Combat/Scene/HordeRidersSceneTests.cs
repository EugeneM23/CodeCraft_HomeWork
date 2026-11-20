using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using Atomic.Elements;
using Atomic.Entities;
using Game.AIKit.Adapters;
using Game.Horde;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Zombies leaping onto the player's tank in VoxelSandbox (HordeRiders): a horde of crawlers comes to the standing
    /// tank and leaps on, the riders tear at it, a hard pivot throws them off, the tank's gun-free kill (direct damage)
    /// leaves corpses by the hull. Frame sequences and a report (riders, strikes, falls, Horde.Riders and PlayerLoop
    /// times) go to Logs/riders-shots/. No WaitForEndOfFrame: runs in -batchmode with a graphics device.
    /// </summary>
    public sealed class HordeRidersSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string HORDE_ART = "Assets/Game/Horde/Art/ZombieHordeArtConfig.asset";
        private const int SHOT_WIDTH = 1280;
        private const int SHOT_HEIGHT = 720;

        private IEntity _tank;
        private Transform _tankTransform;
        private Rigidbody _body;
        private IReactiveVariable<Vector2> _driveInput;
        private HordeBehaviour _horde;
        private int _leaps;
        private int _strikes;
        private int _falls;
        private int _died;
        private int _maxRiders;
        private bool _shootLeap;
        private Vector2 _drive;

        private void WriteDrive()
        {
            if (_driveInput != null && _drive != Vector2.zero)
                _driveInput.Value = _drive;
        }
        private readonly StringBuilder _report = new();
        private bool _leapWas;

        // Leaping onto the tank is off in the game for now (HordeRiderOptions.LeapOntoTank); this test keeps the code alive.
        [SetUp]
        public void LeapOn()
        {
            _leapWas = HordeRiderOptions.LeapOntoTank;
            HordeRiderOptions.LeapOntoTank = true;
        }

        [TearDown]
        public void LeapBack() => HordeRiderOptions.LeapOntoTank = _leapWas;

        [UnityTest, Timeout(240000)]
        public IEnumerator Zombies_LeapOn_Tear_AreShakenOff_AndDieOnTheHull()
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

            GameContext context = GameContext.Instance;
            Assert.That(context, Is.Not.Null);
            if (context.TryGetPlayerContext(out PlayerContext player) && player != null)
                _tank = player.GetCharacter().Value;
            Assert.That(_tank, Is.Not.Null, "the player's tank");
            _tankTransform = _tank is Component c ? c.transform : null;
            if (_tankTransform == null)
                _tank.TryGetTransform(out _tankTransform);
            _body = _tankTransform.GetComponentInParent<Rigidbody>();
            Assert.IsTrue(_tank.TryGetDriveInput(out _driveInput));

            var art = AssetDatabase.LoadAssetAtPath<HordeArtConfig>(HORDE_ART);
            Assert.That(art, Is.Not.Null, HORDE_ART);
            var options = new HordeOptions { capacity = 128, population = 0, tankDamageScale = 0.25f };
            options.crawler.health = 1e5f; // the tank's auto gun would mow them down before they reach it
            foreach (HordeInstaller old in Object.FindObjectsByType<HordeInstaller>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject); // the sandbox's own live horde (SandboxHorde)
            yield return null;
            _horde = HordeSpawn.CreateInScene(art, options).Behaviour;
            for (int i = 0; i < 10; i++)
                yield return null; // the horde initialises on Start

            // Twelve crawlers round the standing tank, 3.5..5 m out from its centre (as the debug panel's «рядом», closer).
            Vector3 centre = _tankTransform.position;
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12f;
                Vector3 at = centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (i % 2 == 0 ? 4.5f : 5.5f);
                _horde.Spawn(EnemyKind.Crawler, at, 1f, 1f);
            }

            Application.onBeforeRender += WriteDrive;
            HordeRiderEvents.StateChanged += OnState;
            HordeRiderEvents.Struck += OnStruck;
            HordeRiderEvents.Died += OnDied;
            var riders = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Horde.Riders", 600);
            var playerLoop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop", 600);
            try
            {
                // 1. The tank stands: they come and leap on.
                float t = 0f;
                int leapShots = 0;
                while (t < 14f && (_maxRiders < 4 || t < 8f))
                {
                    yield return null;
                    t += Time.deltaTime;
                    if (Time.frameCount % 30 == 0)
                        Line($"  t {t:0.0}: alive {_horde.AliveCount}, nearest {NearestMonster():0.00} m, riders {_horde.Riders.Count}");
                    int count = _horde.Riders != null ? _horde.Riders.Count : 0;
                    _maxRiders = Mathf.Max(_maxRiders, count);
                    if (_shootLeap && leapShots < 8 && Time.frameCount % 3 == 0)
                        Shot($"1-leap-{leapShots++}", Side(10f, 5f), _tankTransform.position + Vector3.up * 1.2f);
                }

                Line($"stand {t:0.0}s: leaps {_leaps}, riders now {_horde.Riders.Count}, most {_maxRiders}, strikes {_strikes}");
                Line($"horde: alive {_horde.AliveCount}, nearest to the tank {NearestMonster():0.0} m, tank at {_tankTransform.position}, " +
                     $"run paused {Time.timeScale}, loop {GameTicks.HasLoop}");
                Shot("2-riding-side", Side(6f, 3f), _tankTransform.position + Vector3.up * 1.4f);
                Shot("2-riding-top", _tankTransform.position + _tankTransform.forward * -6f + Vector3.up * 7f, _tankTransform.position);
                double riderMs = Average(riders);
                double loopMs = Average(playerLoop);
                Line($"stand: Horde.Riders {riderMs:0.000} ms/frame, PlayerLoop {loopMs:0.00} ms/frame (avg of last {riders.Count} frames)");

                // 2. Two riders killed where they hold on: their corpses go beside the hull, goo from the deck.
                int diedBefore = _died;
                int killed = 0;
                for (int n = 0; n < _horde.Riders.Count && killed < 2; n++)
                {
                    HordeRiderInfo info = _horde.Riders.GetRider(n);
                    if (info.State != HordeRiderState.Tearing && info.State != HordeRiderState.Clinging)
                        continue;
                    _horde.Targets.Damage(info.Slot, info.Version, 1e7f, _tankTransform.position);
                    killed++;
                }

                for (int f = 0; f < 30; f++)
                {
                    yield return null;
                    if (f == 2 || f == 8 || f == 25)
                        Shot($"3-killed-{f:00}", Side(6f, 3f), _tankTransform.position + Vector3.up);
                }

                Line($"kill: shot {killed} riders, died on the hull {_died - diedBefore}, riders left {_horde.Riders.Count}");

                // 3. A hard pivot in place: the rest are thrown off.
                int fallsBefore = _falls;
                float yawMax = 0f;
                float turned = 0f;
                float lastYaw = _tankTransform.eulerAngles.y;
                for (float p = 0f; p < 3f; p += Time.fixedDeltaTime)
                {
                    _drive = new Vector2(1f, 0.5f);
                    _driveInput.Value = _drive;
                    if (_body != null && p > 0.3f && Mathf.Abs(_body.angularVelocity.y) < 2.5f)
                        _body.angularVelocity = new Vector3(0f, 2.5f, 0f); // a hard pivot whatever the drive does in batchmode
                    if (_body != null)
                        yawMax = Mathf.Max(yawMax, Mathf.Abs(_body.angularVelocity.y));
                    if (Time.frameCount % 4 == 0 && p < 2f)
                        Shot($"4-pivot-{Mathf.RoundToInt(p * 10f):00}", Side(9f, 4.5f), _tankTransform.position + Vector3.up);
                    yield return new WaitForFixedUpdate();
                    if (_body != null && p > 0.3f)
                        _body.angularVelocity = new Vector3(0f, 2.5f, 0f); // after the physics step: the frame reads it
                    turned += Mathf.Abs(Mathf.DeltaAngle(lastYaw, _tankTransform.eulerAngles.y));
                    lastYaw = _tankTransform.eulerAngles.y;
                }

                _drive = Vector2.zero;
                _driveInput.Value = Vector2.zero;
                if (_body != null)
                    _body.angularVelocity = Vector3.zero;
                for (float w = 0f; w < 4f && _horde.Riders.Count > 0; w += Time.deltaTime)
                    yield return null; // the last thrown land
                Line($"pivot 3s: peak yaw rate {yawMax:0.00} rad/s, turned {turned:0} deg, thrown off {_falls - fallsBefore}, riders left {_horde.Riders.Count}");
                Line($"totals: leaps {_leaps}, strikes {_strikes}, thrown off {_falls}, died {_died}");

                Assert.Greater(_maxRiders, 0, "zombies leapt onto the tank");
                Assert.Greater(_strikes, 0, "they tore at it");
                Assert.Greater(killed, 0, "some were tearing when shot");
                Assert.AreEqual(killed, _died - diedBefore, "every killed rider died on the hull");
                Assert.Greater(_falls - fallsBefore, 0, "the pivot threw some off");
                Assert.AreEqual(0, _horde.Riders.Count, "all off and landed");
            }
            finally
            {
                riders.Dispose();
                playerLoop.Dispose();
                Application.onBeforeRender -= WriteDrive;
                _drive = Vector2.zero;
                HordeRiderEvents.StateChanged -= OnState;
                HordeRiderEvents.Struck -= OnStruck;
                HordeRiderEvents.Died -= OnDied;
                if (_driveInput != null)
                    _driveInput.Value = Vector2.zero;
                string dir = ShotsDir();
                File.WriteAllText(Path.Combine(dir, "report.txt"), _report.ToString());
                Debug.Log("[RidersTest]\n" + _report);
            }
        }

        private void OnState(HordeRiderInfo info)
        {
            if (info.State == HordeRiderState.Leaping)
            {
                _leaps++;
                _shootLeap = true;
            }
            else if (info.State == HordeRiderState.ShakenOff)
                _falls++;
        }

        private void OnStruck(HordeRiderInfo info) => _strikes++;

        private string States()
        {
            string s = "";
            for (int i = 0; i < _horde.Riders.Count; i++)
                s += (int)_horde.Riders.GetRider(i).State;
            return s;
        }

        private int Tearing()
        {
            int n = 0;
            for (int i = 0; i < _horde.Riders.Count; i++)
                if (_horde.Riders.GetRider(i).State == HordeRiderState.Tearing)
                    n++;
            return n;
        }

        private float NearestMonster()
        {
            HordeTargets targets = _horde.Targets;
            return targets.TryFindNearest(_tankTransform.position, 500f, out _, out _, out Vector3 at)
                ? Vector3.Distance(at, _tankTransform.position)
                : -1f;
        }

        private void OnDied(HordeRiderInfo info) => _died++;

        private Vector3 Side(float distance, float height) =>
            _tankTransform.position + _tankTransform.right * distance + _tankTransform.forward * 2f + Vector3.up * height;

        private void Line(string text)
        {
            _report.AppendLine(text);
            Debug.Log("[RidersTest] " + text);
        }

        private static double Average(ProfilerRecorder recorder)
        {
            int n = recorder.Count;
            if (n == 0)
                return 0;
            double sum = 0;
            for (int i = 0; i < n; i++)
                sum += recorder.GetSample(i).Value;
            return sum / n * 1e-6;
        }

        private static string ShotsDir()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "riders-shots"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Camera.main from <paramref name="from"/> at <paramref name="at"/> into Logs/riders-shots/&lt;name&gt;.png; put back after.</summary>
        private static void Shot(string name, Vector3 from, Vector3 at)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            Transform camTransform = cam.transform;
            camTransform.GetPositionAndRotation(out Vector3 oldPosition, out Quaternion oldRotation);
            var rt = new RenderTexture(SHOT_WIDTH, SHOT_HEIGHT, 24);
            RenderTexture oldTarget = cam.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                camTransform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                texture = new Texture2D(SHOT_WIDTH, SHOT_HEIGHT, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, SHOT_WIDTH, SHOT_HEIGHT), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(ShotsDir(), name + ".png"), texture.EncodeToPNG());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RidersTest] shot {name} failed: {e.Message}");
            }
            finally
            {
                camTransform.SetPositionAndRotation(oldPosition, oldRotation);
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

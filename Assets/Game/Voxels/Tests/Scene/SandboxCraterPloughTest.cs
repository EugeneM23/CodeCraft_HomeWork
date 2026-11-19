using System.Collections;
using System.IO;
using System.Reflection;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// VoxelSandbox, the tank on full gas with Shift along a straight runway.
    ///
    /// With an air bomb's crater ahead its nose rips a chunk out of the far slope and it bounces out:
    /// it keeps at least half its entry speed there, is out over the far rim within 2.5 s of entering,
    /// leaves it nose up and jumps visibly but not high. The chunk is thrown (ThrowEarth cubes) and
    /// leaves a notch in the far slope. That bar is for Punchy; Subtle and Cinema are driven too and logged.
    /// The impact the event reports grows Subtle &lt; Punchy &lt; Cinema on the same entry.
    ///
    /// For every preset the hull never goes into the ground: at every physics step from the near rim to
    /// 1.5 s after the far rim none of its box's four bottom corners is more than 0.15 m below the ground
    /// under it; and the plough never touches the body's excluded layers or the springs.
    /// Screenshots (Punchy) go to Logs/CraterPlough.
    ///
    /// Without a crater the plough never answers: ordinary driving is no hit.
    /// </summary>
    public sealed class SandboxCraterPloughTest
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string OUT = "Logs/CraterPlough";
        private const float RIM = 4f;
        // Start to the near rim. Full gas with Shift takes the tank to ~13 m/s here, the entry speed the
        // presets were tuned and approved at (the drive's acceleration was halved after that: 14 m gave 10.6 m/s).
        private const float RUN_UP = 21f;
        // How far on from the start the runway must be level, and clear of anything but ground.
        private const float LEVEL_TO = RUN_UP + 2f * RIM + 6f;
        private const float CLEAR_TO = RUN_UP + 2f * RIM + 9f;
        private const int GROUND = 1 << PhysicsLayers.Ground;
        // Past the far rim: how far on the nose-up pitch is looked at.
        private const float PAST_RIM = 3f;
        // Seconds after the far rim the hull is still watched for going into the ground.
        private const float WATCH_AFTER_RIM = 1.5f;
        // Deepest (m) a hull corner may be below the ground under it: the solver's give, no more.
        private const float MAX_HULL_DEPTH = 0.15f;
        // Real seconds any one wait may take before the test fails instead of hanging (the [Timeout]s stay).
        private const float REAL_LIMIT = 90f;
        // Spacing (m) of the ground heights the notch is measured against.
        private const float PROFILE_STEP = 0.25f;

        private GameObject _tankObject;
        private IEntity _tank;
        private Rigidbody _body;

        /// <summary>What one crater run measured.</summary>
        private sealed class CraterRun
        {
            public readonly string Preset;
            public float EntrySpeed = -1f, MinFarSpeed = float.MaxValue, Took = float.PositiveInfinity;
            public float MaxNoseUp = float.MinValue, Apex = float.MinValue, Notch;
            // The deepest any bottom corner of the hull went below the ground under it, m (negative: never touched it).
            public float HullDepth = float.MinValue;
            public string HullDepthAt = "";
            public int Ploughed, SolidBefore, SolidAfter, Thrown, Throws, Steps;
            // The hardest strength01 the Ploughed event gave in this run.
            public float MaxStrength;
            public bool Untouched = true;
            public string TouchNote = "";

            public CraterRun(string preset) => Preset = preset;
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Sandbox_TankHitsCraterSlopeAtSpeed_RipsAChunkAndBouncesOut()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                // Punchy is the one held to the bar; Subtle and Cinema are driven too, each in a fresh
                // sandbox, held to the hull-above-ground and hands-off checks and otherwise logged.
                var punchy = new CraterRun("Punchy");
                var subtle = new CraterRun("Subtle");
                var cinema = new CraterRun("Cinema");
                CraterRun[] runs = { punchy, subtle, cinema };
                foreach (CraterRun run in runs)
                    yield return RunCrater(run, run == punchy);

                Debug.Log($"[CraterPlough] impact strength (Ploughed, max per run): Subtle {subtle.MaxStrength:F2}, " +
                          $"Punchy {punchy.MaxStrength:F2}, Cinema {cinema.MaxStrength:F2}");

                foreach (CraterRun run in runs)
                {
                    Assert.That(run.Steps, Is.GreaterThan(50), $"{run.Preset}: the hull was watched");
                    Assert.That(run.HullDepth, Is.LessThanOrEqualTo(MAX_HULL_DEPTH),
                        $"{run.Preset}: hull never below the ground (worst {run.HullDepthAt})");
                    Assert.IsTrue(run.Untouched, $"{run.Preset}: excluded layers and springs never changed ({run.TouchNote})");
                    Assert.That(run.MaxStrength, Is.GreaterThan(0f), $"{run.Preset}: Ploughed came with an impact");
                }

                // The same entry hits harder the stronger the preset: the sound and the shake go by it.
                Assert.That(subtle.MaxStrength, Is.LessThan(punchy.MaxStrength), "impact: Subtle below Punchy");
                Assert.That(punchy.MaxStrength, Is.LessThan(cinema.MaxStrength), "impact: Punchy below Cinema");

                Assert.That(punchy.EntrySpeed, Is.GreaterThan(6f), "the tank came in fast");
                Assert.That(punchy.MinFarSpeed, Is.GreaterThanOrEqualTo(punchy.EntrySpeed * 0.5f), "kept half its speed on the far slope");
                Assert.That(punchy.Took, Is.LessThan(2.5f), "out over the far rim within 2.5 s");
                Assert.That(punchy.MaxNoseUp, Is.GreaterThanOrEqualTo(12f), "nose up over the far rim");
                Assert.That(punchy.Apex, Is.InRange(0.3f, 0.8f), "a visible jump over the rim, not a launch");
                Assert.That(cinema.Apex, Is.LessThanOrEqualTo(1.5f), "Cinema: a bigger jump, still no launch");
                Assert.That(punchy.Notch, Is.GreaterThanOrEqualTo(0.2f), "a notch ripped out of the far slope");
                Assert.That(punchy.SolidAfter, Is.LessThan(punchy.SolidBefore), "earth dug out of the far slope");
                Assert.That(punchy.Thrown, Is.GreaterThan(0), "the chunk was thrown (ThrowEarth)");
                Assert.That(punchy.Ploughed, Is.GreaterThan(0), "the nose hit");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>One run into a fresh crater in a fresh sandbox with <see cref="CraterRun.Preset"/>; logs one [CraterPlough] line.</summary>
        private IEnumerator RunCrater(CraterRun run, bool screenshots)
        {
            yield return LoadSandbox();
            TankInstaller installer = _tankObject.GetComponent<TankInstaller>();
            for (int i = 0; i < 3 && installer.PloughPreset.DisplayName != run.Preset; i++)
                installer.NextPloughPreset();
            Assert.AreEqual(run.Preset, installer.PloughPreset.DisplayName, "preset");

            Assert.IsTrue(TryFindRunway(out Vector3 start, out Vector3 forward), "open ground for a run-up and a crater");
            Vector3 crater = start + forward * (RUN_UP + RIM);
            VoxelDamage.AirBlast(crater, RIM, 32f, 24f, 0, 0.85f, 24f);
            float wait = Deadline();
            for (int i = 0; i < 10; i++)
            {
                Within(wait, "frames after the bomb");
                yield return null; // the crater's collider is rebuilt a frame or two later
            }

            yield return PlaceTank(start, forward);
            // The tank at rest on the runway: the far rim is at this height, give or take a voxel.
            float restY = _body.position.y;

            Assert.IsTrue(Physics.Raycast(crater + forward * (RIM * 0.7f) + Vector3.up * 5f, Vector3.down,
                out RaycastHit farHit, 10f, GROUND), "the far slope");
            VoxelBody farGround = TankPloughBehaviour.FindGround(farHit.point);
            Assert.IsNotNull(farGround, "the ground body under the far slope");
            run.SolidBefore = farGround.SolidCount;
            float[] slope = Profile(crater, forward);
            BoxCollider hull = HullCollider(installer);

            // What the plough must never touch.
            TankPloughBehaviour plough = installer.Plough;
            int thrownBefore = plough.ThrownCubes;
            int throwsBefore = plough.Throws;
            TankSuspension suspension = _tank.GetSuspension();
            int excludeBefore = _body.excludeLayers.value;
            float stiffnessBefore = suspension.Stiffness;
            float dampingBefore = suspension.Damping;

            void OnPloughed(Vector3 point, float strength)
            {
                run.Ploughed++;
                run.MaxStrength = Mathf.Max(run.MaxStrength, strength);
            }

            IReactiveVariable<Vector2> drive = _tank.GetDriveInput();
            _tank.TryGetDriveBoost(out IReactiveVariable<bool> boost);

            // Every physics step: full gas with Shift; from the near rim to WATCH_AFTER_RIM s after the far
            // one the hull is looked at.
            float entryTime = -1f, farRimTime = -1f;
            void OnStep()
            {
                Hold(drive, boost);
                float along = Vector3.Dot(_body.position - crater, forward);
                if (along < -RIM || (farRimTime >= 0f && Time.time > farRimTime + WATCH_AFTER_RIM))
                    return;
                if (farRimTime < 0f && along >= RIM)
                    farRimTime = Time.time;
                run.Steps++;

                if (run.Untouched && (_body.excludeLayers.value != excludeBefore || suspension.Stiffness != stiffnessBefore ||
                                      suspension.Damping != dampingBefore))
                {
                    run.Untouched = false;
                    run.TouchNote = $"at {along:F2} m: exclude {_body.excludeLayers.value}/{excludeBefore}, " +
                                    $"stiffness {suspension.Stiffness}/{stiffnessBefore}, damping {suspension.Damping}/{dampingBefore}";
                }

                Transform box = hull.transform;
                Vector3 half = hull.size * 0.5f;
                for (int c = 0; c < 4; c++)
                {
                    var corner = new Vector3((c & 1) != 0 ? half.x : -half.x, -half.y, (c & 2) != 0 ? half.z : -half.z);
                    Vector3 world = box.TransformPoint(hull.center + corner);
                    if (!Physics.Raycast(world + Vector3.up * 5f, Vector3.down, out RaycastHit ground, 10f, GROUND,
                            QueryTriggerInteraction.Ignore))
                        continue;
                    float depth = ground.point.y - world.y;
                    if (depth > run.HullDepth)
                    {
                        run.HullDepth = depth;
                        run.HullDepthAt = $"{depth:F2} m, {((c & 2) != 0 ? "front" : "rear")} {((c & 1) != 0 ? "right" : "left")} " +
                                          $"corner, {along:F2} m from the crater's centre";
                    }
                }
            }

            var probe = new GameObject("CraterPloughProbe").AddComponent<FixedStepProbe>();
            probe.Step = OnStep;
            TankPloughEvents.Ploughed += OnPloughed;
            try
            {

                Directory.CreateDirectory(OUT);

                float exitTime = -1f;
                float until = Time.time + 8f;
                float deadline = Deadline();
                int shot = 0;
                bool done = false;
                while (Time.time < until && !done)
                {
                    Within(deadline, $"{run.Preset}: the crater run");
                    yield return null;

                    // Distance along the run from the crater's centre: -RIM at the near rim, +RIM at the far one.
                    float along = Vector3.Dot(_body.position - crater, forward);
                    float speed = Vector3.Dot(_body.linearVelocity, forward);
                    if (entryTime < 0f && along >= -RIM)
                    {
                        entryTime = Time.time;
                        run.EntrySpeed = speed;
                    }

                    if (entryTime < 0f)
                        continue;
                    if (along > 0f && exitTime < 0f)
                        run.MinFarSpeed = Mathf.Min(run.MinFarSpeed, speed);
                    if (along > RIM + 1.4f && exitTime < 0f)
                        exitTime = Time.time;
                    if (along > 0f)
                        run.Apex = Mathf.Max(run.Apex, _body.position.y - restY);
                    if (along >= RIM && along <= RIM + PAST_RIM)
                        run.MaxNoseUp = Mathf.Max(run.MaxNoseUp,
                            Mathf.Asin(Mathf.Clamp(_tankObject.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg);
                    done = along > RIM + PAST_RIM && farRimTime >= 0f && Time.time > farRimTime + WATCH_AFTER_RIM;
                    if (screenshots && shot < 12 && Time.frameCount % 4 == 0)
                        Capture($"{shot++:00}_crater");
                }

                if (exitTime >= 0f)
                    run.Took = exitTime - entryTime;
                run.SolidAfter = farGround.SolidCount;
                run.Thrown = plough.ThrownCubes - thrownBefore;
                run.Throws = plough.Throws - throwsBefore;

                // The notch: how far the ground along the run's centre line on the far slope went down.
                float[] after = Profile(crater, forward);
                for (int i = 0; i < slope.Length; i++)
                {
                    float a = -RIM + i * PROFILE_STEP;
                    if (a >= 0f && a <= RIM + 1.5f)
                        run.Notch = Mathf.Max(run.Notch, slope[i] - after[i]);
                }

                Debug.Log($"[CraterPlough] {run.Preset}: entry {run.EntrySpeed:F2} m/s, " +
                          $"min on far slope {run.MinFarSpeed:F2} m/s ({run.MinFarSpeed / Mathf.Max(0.01f, run.EntrySpeed):P0}), " +
                          $"out in {run.Took:F2} s, nose up over the far rim {run.MaxNoseUp:F1}°, " +
                          $"apex {run.Apex:F2} m above the runway rest height (the rim), notch {run.Notch:F2} m deep, " +
                          $"hull worst {run.HullDepth:F2} m below the ground ({run.HullDepthAt}; {run.Steps} steps), " +
                          $"ThrowEarth cubes {run.Thrown} in {run.Throws} calls, impacts {plough.Impacts}, max impact {run.MaxStrength:F2}, " +
                          $"untouched {run.Untouched}");
            }
            finally
            {
                TankPloughEvents.Ploughed -= OnPloughed;
                Object.Destroy(probe.gameObject);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Sandbox_TankDrivesFlatGroundAtSpeed_NeverPloughs()
        {
            LogAssert.ignoreFailingMessages = true;
            FixedStepProbe probe = null;
            try
            {
                yield return LoadSandbox();
                Assert.IsTrue(TryFindRunway(out Vector3 start, out Vector3 forward), "open ground for a run");
                yield return PlaceTank(start, forward);

                TankPloughBehaviour plough = _tankObject.GetComponent<TankInstaller>().Plough;
                Assert.IsNotNull(plough, "the tank has a plough");
                IReactiveVariable<Vector2> drive = _tank.GetDriveInput();
                _tank.TryGetDriveBoost(out IReactiveVariable<bool> boost);

                // The engine cranks first (TankEngine); then the whole runway, flat out.
                float topSpeed = 0f;
                float until = Time.time + 8f;
                float deadline = Deadline();
                probe = new GameObject("FlatDriveProbe").AddComponent<FixedStepProbe>();
                probe.Step = () => Hold(drive, boost);
                while (Time.time < until && Vector3.Dot(_body.position - start, forward) < 26f)
                {
                    Within(deadline, "the flat run");
                    yield return null;
                    topSpeed = Mathf.Max(topSpeed, Vector3.Dot(_body.linearVelocity, forward));
                }

                Debug.Log($"[FlatDrive] top speed {topSpeed:F2} m/s, plough impacts {plough.Impacts}");
                Assert.That(topSpeed, Is.GreaterThan(6f), "the tank got going");
                Assert.That(plough.Impacts, Is.EqualTo(0), "no plough on flat ground");
            }
            finally
            {
                if (probe != null)
                    Object.Destroy(probe.gameObject);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>Full gas with Shift, written at the start of the physics step, over the player's controller.</summary>
        private static void Hold(IReactiveVariable<Vector2> drive, IReactiveVariable<bool> boost)
        {
            drive.Value = new Vector2(0f, 1f);
            if (boost != null)
                boost.Value = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene sandbox = SceneManager.GetSceneByPath(SCENE);
            if (!sandbox.IsValid() || !sandbox.isLoaded)
                yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterCraterPlough"));
            AsyncOperation unload = SceneManager.UnloadSceneAsync(sandbox);
            float deadline = Deadline();
            while (unload != null && !unload.isDone)
            {
                Within(deadline, "unloading the sandbox");
                yield return null;
            }
        }

        /// <summary>When a wait that started now has taken too long in real time.</summary>
        private static float Deadline() => Time.realtimeSinceStartup + REAL_LIMIT;

        /// <summary>
        /// Fails the test, rather than letting it hang, once a wait has gone past <paramref name="deadline"/>
        /// in real time: in batchmode a frame or a physics step that never comes would hang the run.
        /// </summary>
        private static void Within(float deadline, string what)
        {
            if (Time.realtimeSinceStartup > deadline)
                Assert.Fail($"{what}: still waiting after {REAL_LIMIT} s of real time (frames or physics steps stopped coming)");
        }

        private IEnumerator LoadSandbox()
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            float deadline = Deadline();
            while (!load.isDone)
            {
                Within(deadline, "loading the sandbox");
                yield return null;
            }

            deadline = Deadline();
            for (int i = 0; i < 90; i++)
            {
                Within(deadline, "physics steps after loading the sandbox");
                yield return new WaitForFixedUpdate();
            }

            // No bombs but ours.
            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(on, "VoxelAirStrike._on (renamed?)");
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on.SetValue(strike, false);

            _tankObject = GameObject.Find("Tank_01");
            _tank = _tankObject.GetComponent<MonoEntity>();
            _body = _tankObject.GetComponent<Rigidbody>();
        }

        private IEnumerator PlaceTank(Vector3 start, Vector3 forward)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.position = start + Vector3.up * 0.4f;
            _body.rotation = Quaternion.LookRotation(forward);
            _tankObject.transform.SetPositionAndRotation(_body.position, _body.rotation);
            float deadline = Deadline();
            for (int i = 0; i < 40; i++)
            {
                Within(deadline, "the tank settling on the runway");
                yield return new WaitForFixedUpdate();
            }
        }

        /// <summary>Ground height along the run's centre line, every PROFILE_STEP m from the near rim to 2 m past the far one.</summary>
        private static float[] Profile(Vector3 crater, Vector3 forward)
        {
            int count = Mathf.RoundToInt((2f * RIM + 2f) / PROFILE_STEP) + 1;
            var heights = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 at = crater + forward * (-RIM + i * PROFILE_STEP) + Vector3.up * 6f;
                heights[i] = Physics.Raycast(at, Vector3.down, out RaycastHit hit, 12f, GROUND) ? hit.point.y : float.NaN;
            }

            return heights;
        }

        /// <summary>The hull's box the plough sweeps: TankInstaller._hullCollider.</summary>
        private static BoxCollider HullCollider(TankInstaller installer)
        {
            FieldInfo field = typeof(TankInstaller).GetField("_hullCollider", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "TankInstaller._hullCollider (renamed?)");
            var hull = (BoxCollider)field.GetValue(installer);
            Assert.IsNotNull(hull, "the tank's hull collider");
            return hull;
        }

        /// <summary>
        /// A straight run along ±X or ±Z, nearest the origin: level from its start to LEVEL_TO m on, and nothing
        /// but ground in a box 6 m wide and 3 m high from 1 m behind it to CLEAR_TO m on. Room for a run-up,
        /// the crater and the way out.
        /// </summary>
        private static bool TryFindRunway(out Vector3 start, out Vector3 forward)
        {
            start = default;
            forward = Vector3.forward;
            Vector3[] ways = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            float best = float.MaxValue;
            const int notGround = ~GROUND;
            const float halfLength = (CLEAR_TO + 1f) * 0.5f;
            for (float x = -30f; x <= 30f; x += 3f)
            for (float z = -30f; z <= 30f; z += 3f)
            foreach (Vector3 way in ways)
            {
                var from = new Vector3(x, 0f, z);
                Vector3 centre = from + way * (halfLength - 1f) + Vector3.up * 2f;
                if (Physics.CheckBox(centre, new Vector3(3f, 1.5f, halfLength), Quaternion.LookRotation(way), notGround,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (!Physics.Raycast(from + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, GROUND))
                    continue;
                if (!Physics.Raycast(from + way * LEVEL_TO + Vector3.up * 5f, Vector3.down, out RaycastHit end, 10f, GROUND) ||
                    Mathf.Abs(end.point.y - hit.point.y) > 0.5f)
                    continue;
                if (from.sqrMagnitude < best)
                {
                    best = from.sqrMagnitude;
                    start = hit.point;
                    forward = way;
                }
            }

            return best < float.MaxValue;
        }

        private static void Capture(string name)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;
            var target = new RenderTexture(1280, 720, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OUT, name + ".png"), image.EncodeToPNG());
            Object.Destroy(target);
            Object.Destroy(image);
        }
    }
}

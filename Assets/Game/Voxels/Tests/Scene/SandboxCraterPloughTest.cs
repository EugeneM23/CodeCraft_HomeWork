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
    /// With an air bomb's crater ahead it must not stop dead against the far slope: it keeps at least
    /// half its entry speed there, is out over the far rim within 2.5 s of entering and leaves it nose
    /// up. Its nose ploughs on the way (TankPloughEvents.Ploughed) and earth is dug out of the far
    /// slope. Screenshots go to Logs/CraterPlough.
    ///
    /// Without a crater the plough never answers: ordinary driving is no hit.
    /// </summary>
    public sealed class SandboxCraterPloughTest
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string OUT = "Logs/CraterPlough";
        private const float RIM = 4f;
        private const float RUN_UP = 14f;
        private const int GROUND = 1 << 3;
        // Past the far rim: how far on the nose-up pitch is looked at.
        private const float PAST_RIM = 3f;

        private GameObject _tankObject;
        private IEntity _tank;
        private Rigidbody _body;

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Sandbox_TankHitsCraterSlopeAtSpeed_BreaksOutWithoutStopping()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return LoadSandbox();

                Assert.IsTrue(TryFindRunway(out Vector3 start, out Vector3 forward), "open ground for a run-up and a crater");
                Vector3 crater = start + forward * (RUN_UP + RIM);
                VoxelDamage.AirBlast(crater, RIM, 32f, 24f, 0, 0.85f, 24f);
                for (int i = 0; i < 10; i++)
                    yield return null; // the crater's collider is rebuilt a frame or two later

                yield return PlaceTank(start, forward);
                float restY = _body.position.y;

                Assert.IsTrue(Physics.Raycast(crater + forward * (RIM * 0.7f) + Vector3.up * 5f, Vector3.down,
                    out RaycastHit farHit, 10f, GROUND), "the far slope");
                VoxelBody farGround = TankPloughBehaviour.FindGround(farHit.point);
                Assert.IsNotNull(farGround, "the ground body under the far slope");
                int before = farGround.SolidCount;
                int ploughed = 0;
                void OnPloughed(Vector3 point, float strength) => ploughed++;
                TankPloughEvents.Ploughed += OnPloughed;
                try
                {
                    Directory.CreateDirectory(OUT);
                    IReactiveVariable<Vector2> drive = _tank.GetDriveInput();
                    _tank.TryGetDriveBoost(out IReactiveVariable<bool> boost);

                    float entrySpeed = -1f, entryTime = -1f, exitTime = -1f;
                    float minFarSpeed = float.MaxValue;
                    float maxNoseUp = float.MinValue;
                    float apex = float.MinValue;
                    float until = Time.time + 8f;
                    int shot = 0;
                    bool past = false;
                    while (Time.time < until && !past)
                    {
                        drive.Value = new Vector2(0f, 1f);
                        if (boost != null)
                            boost.Value = true;
                        yield return new WaitForEndOfFrame();

                        // Distance along the run from the crater's centre: -RIM at the near rim, +RIM at the far one.
                        float along = Vector3.Dot(_body.position - crater, forward);
                        float speed = Vector3.Dot(_body.linearVelocity, forward);
                        if (entryTime < 0f && along >= -RIM)
                        {
                            entryTime = Time.time;
                            entrySpeed = speed;
                        }

                        if (entryTime < 0f)
                            continue;
                        if (along > 0f && exitTime < 0f)
                            minFarSpeed = Mathf.Min(minFarSpeed, speed);
                        if (along > RIM + 1.4f && exitTime < 0f)
                            exitTime = Time.time;
                        if (along > 0f)
                            apex = Mathf.Max(apex, _body.position.y - restY);
                        if (along >= RIM && along <= RIM + PAST_RIM)
                            maxNoseUp = Mathf.Max(maxNoseUp, Mathf.Asin(Mathf.Clamp(_tankObject.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg);
                        past = along > RIM + PAST_RIM;
                        if (shot < 12 && Time.frameCount % 4 == 0)
                            Capture($"{shot++:00}_crater");
                    }

                    float took = exitTime >= 0f ? exitTime - entryTime : float.PositiveInfinity;
                    TankPloughConfig preset = _tankObject.GetComponent<TankInstaller>().PloughPreset;
                    Debug.Log($"[CraterPlough] {(preset != null ? preset.DisplayName : "-")}: entry {entrySpeed:F2} m/s, " +
                              $"min on far slope {minFarSpeed:F2} m/s ({minFarSpeed / Mathf.Max(0.01f, entrySpeed):P0}), " +
                              $"out in {took:F2} s, nose up over the far rim {maxNoseUp:F1}°, apex {apex:F2} m above the rim");

                    Assert.That(entrySpeed, Is.GreaterThan(6f), "the tank came in fast");
                    Assert.That(minFarSpeed, Is.GreaterThanOrEqualTo(entrySpeed * 0.5f), "kept half its speed on the far slope");
                    Assert.That(took, Is.LessThan(2.5f), "out over the far rim within 2.5 s");
                    Assert.That(maxNoseUp, Is.GreaterThanOrEqualTo(12f), "nose up over the far rim");
                    Assert.That(ploughed, Is.GreaterThan(0), "the nose ploughed");
                    Assert.That(farGround.SolidCount, Is.LessThan(before), "earth dug out of the far slope");
                }
                finally
                {
                    TankPloughEvents.Ploughed -= OnPloughed;
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Sandbox_TankDrivesFlatGroundAtSpeed_NeverPloughs()
        {
            LogAssert.ignoreFailingMessages = true;
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
                while (Time.time < until && Vector3.Dot(_body.position - start, forward) < 26f)
                {
                    drive.Value = new Vector2(0f, 1f);
                    if (boost != null)
                        boost.Value = true;
                    yield return new WaitForEndOfFrame(); // after the player's controller has written the stick
                    topSpeed = Mathf.Max(topSpeed, Vector3.Dot(_body.linearVelocity, forward));
                }

                Debug.Log($"[FlatDrive] top speed {topSpeed:F2} m/s, plough impacts {plough.Impacts}");
                Assert.That(topSpeed, Is.GreaterThan(6f), "the tank got going");
                Assert.That(plough.Impacts, Is.EqualTo(0), "no plough on flat ground");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene sandbox = SceneManager.GetSceneByPath(SCENE);
            if (!sandbox.IsValid() || !sandbox.isLoaded)
                yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterCraterPlough"));
            yield return SceneManager.UnloadSceneAsync(sandbox);
        }

        private IEnumerator LoadSandbox()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 90; i++)
                yield return new WaitForFixedUpdate();

            // No bombs but ours.
            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
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
            for (int i = 0; i < 40; i++)
                yield return new WaitForFixedUpdate();
        }

        /// <summary>
        /// A straight 30 m of ground along ±X or ±Z with nothing on it but the ground, nearest the
        /// origin: room for a run-up, the crater and the way out.
        /// </summary>
        private static bool TryFindRunway(out Vector3 start, out Vector3 forward)
        {
            start = default;
            forward = Vector3.forward;
            Vector3[] ways = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            float best = float.MaxValue;
            const int notGround = ~GROUND;
            for (float x = -30f; x <= 30f; x += 3f)
            for (float z = -30f; z <= 30f; z += 3f)
            foreach (Vector3 way in ways)
            {
                var from = new Vector3(x, 0f, z);
                Vector3 centre = from + way * 15f + Vector3.up * 2f;
                if (Physics.CheckBox(centre, new Vector3(3f, 1.5f, 16f), Quaternion.LookRotation(way), notGround,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (!Physics.Raycast(from + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, GROUND))
                    continue;
                if (!Physics.Raycast(from + way * 28f + Vector3.up * 5f, Vector3.down, out RaycastHit end, 10f, GROUND) ||
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

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// Plays VoxelSandbox with the game's own tank: the player drives it from the start, a tank
    /// shell leaves a bowl in the mud (not a hole to the bedrock), and the wheels press ruts into
    /// mud as the tank drives through it. Screenshots go to Logs/VoxelSmoke.
    /// </summary>
    public sealed class SandboxVoxelSmokeTest
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string SHELL = "Assets/Game/Prefabs/Projectiles/TankShell_01.prefab";
        private const string OUT = "Logs/VoxelSmoke";

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Sandbox_CratersAreBowlsAndTheTankLeavesRuts()
        {
            LogAssert.ignoreFailingMessages = true;
            var errors = new List<string>();
            Application.LogCallback collect = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception)
                    errors.Add(message + "\n" + stack);
            };
            Application.logMessageReceived += collect;

            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
                for (int i = 0; i < 90; i++)
                    yield return new WaitForFixedUpdate();

                GameObject tankObject = GameObject.Find("Tank_01");
                IEntity tank = tankObject.GetComponent<MonoEntity>();
                IEntity character = GameContext.Instance.GetPlayerContext().GetCharacter().Value;
                Assert.AreSame(tank, character, "the player starts in the tank");
                Debug.Log($"[VoxelSmoke] tank at {tankObject.transform.position:F2}, up·Y {Vector3.Dot(tankObject.transform.up, Vector3.up):F3}");

                Directory.CreateDirectory(OUT);
                Capture("30_sandbox_start");

                // A shell into open ground ahead of the tank.
                GameFactory factory = GameContext.Instance.GetGameFactory();
                MonoEntity shellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SHELL).GetComponent<MonoEntity>();
                Vector3 impact = new(6f, 0f, -14f);
                IEntity shell = factory.Create(shellPrefab);
                shell.SetDamage(new Atomic.Elements.ReactiveVariable<int>(150));
                ExplosionUseCase.Explode(shell, impact, null, factory);
                SpawnUseCase.UnSpawnEntity(factory, shell);

                int flying = VoxelWorld.Instance.Debris.Count;
                // The ground remeshes on workers without the frame waiting (VoxelWorld): the crater's
                // collider comes a frame or two after the blast. Wait for it, a few frames at most.
                RaycastHit bottom = default;
                for (int i = 0; i < 10; i++)
                {
                    yield return null;
                    if (Physics.Raycast(impact + Vector3.up * 3f, Vector3.down, out bottom, 10f, 1 << 3) &&
                        bottom.point.y < -0.5f)
                        break;
                }

                Assert.IsTrue(Physics.Raycast(impact + Vector3.up * 3f, Vector3.down, out bottom, 10f, 1 << 3));
                Debug.Log($"[VoxelSmoke] crater bottom at y {bottom.point.y:F2} ({bottom.collider.name}), debris {flying}");
                Assert.That(bottom.point.y, Is.LessThan(-0.5f), "the shell dug a crater");
                Assert.That(bottom.point.y, Is.GreaterThan(-1.55f), "a bowl, not a shaft to the bedrock");
                Assert.That(bottom.collider.GetComponentInParent<VoxelBody>(), Is.Not.Null);

                for (int i = 0; i < 30; i++)
                    yield return null;
                Capture("31_crater");

                // Ruts are pressed into mud only (grass and the road take a print), and the tank starts
                // on the road: it is put on a stretch of mud and driven through it as the player drives.
                Assert.IsTrue(TryFindMud(tankObject.transform.forward, impact, out Vector3 mud), "a stretch of mud to drive through");
                Rigidbody body = tankObject.GetComponent<Rigidbody>();
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = mud + Vector3.up * 0.3f;
                tankObject.transform.position = body.position;
                for (int i = 0; i < 50; i++)
                    yield return new WaitForFixedUpdate();

                VoxelBody ground = Physics.Raycast(tankObject.transform.position + Vector3.up, Vector3.down, out RaycastHit under, 3f, 1 << 3)
                    ? under.collider.GetComponentInParent<VoxelBody>()
                    : null;
                Assert.IsNotNull(ground, "the tank stands on voxel mud");
                int before = ground.SolidCount;
                Vector3 from = tankObject.transform.position;
                // The player's controller writes the keys into the stick every LateUpdate: the throttle
                // is put in at the end of each frame, for the physics steps of the next one.
                var drive = tank.GetDriveInput();
                float until = Time.time + 2f;
                while (Time.time < until)
                {
                    drive.Value = new Vector2(0f, 1f);
                    yield return new WaitForEndOfFrame();
                }

                yield return new WaitForFixedUpdate();
                Debug.Log($"[VoxelSmoke] drove {Vector3.Distance(from, tankObject.transform.position):F1} m from {from:F1}");
                Assert.That(Vector3.Distance(from, tankObject.transform.position), Is.GreaterThan(3f), "the tank drove through the mud");

                Debug.Log($"[VoxelSmoke] ruts: tile {before} → {ground.SolidCount} voxels, debris {VoxelWorld.Instance.Debris.Count}");
                Assert.That(ground.SolidCount, Is.LessThan(before), "the wheels pressed ruts into the mud");
                Capture("32_ruts");

                foreach (string error in errors.Take(10))
                    Debug.Log($"[VoxelSmoke] logged error: {error}");
                Assert.IsEmpty(errors.Where(e => e.Contains("Voxel")).ToList());
            }
            finally
            {
                Application.logMessageReceived -= collect;
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>
        /// The scene goes with the test: an empty scene takes its place, so the next test does not
        /// find its ground, debris or voxel world.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene sandbox = SceneManager.GetSceneByPath(SCENE);
            if (!sandbox.IsValid() || !sandbox.isLoaded)
                yield break;

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterSandboxSmoke"));
            yield return SceneManager.UnloadSceneAsync(sandbox);
        }

        /// <summary>
        /// A spot with 8 m of mud ahead of it along <paramref name="forward"/>, clear of houses,
        /// ramps and Regaxes (nothing but the ground in a box round the way) and of the crater at
        /// <paramref name="crater"/>, nearest the origin.
        /// </summary>
        private static bool TryFindMud(Vector3 forward, Vector3 crater, out Vector3 spot)
        {
            spot = default;
            VoxelTerrainSettings terrain = Object.FindFirstObjectByType<VoxelTerrainSettings>();
            if (terrain == null)
                return false;

            forward.y = 0f;
            forward.Normalize();
            const int notGround = ~(1 << 3);
            float best = float.MaxValue;
            for (float x = -36f; x <= 36f; x += 2f)
            for (float z = -36f; z <= 36f; z += 2f)
            {
                var start = new Vector3(x, 0f, z);
                if (Vector3.Distance(start + forward * 2.5f, new Vector3(crater.x, 0f, crater.z)) < 9f)
                    continue;
                bool allMud = true;
                for (float d = -3f; d <= 8f && allMud; d += 0.5f)
                    allMud = terrain.SoilAt(start + forward * d) == TerrainSoil.Mud;
                if (!allMud)
                    continue;

                Vector3 centre = start + forward * 2.5f + Vector3.up * 1.5f;
                var box = new Vector3(3f, 1.2f, 7f);
                if (Physics.CheckBox(centre, box, Quaternion.LookRotation(forward), notGround, QueryTriggerInteraction.Ignore))
                    continue;

                if (!Physics.Raycast(start + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f, 1 << 3))
                    continue;

                float distance = start.sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    spot = hit.point;
                }
            }

            return best < float.MaxValue;
        }

        /// <summary>What the game camera sees right now.</summary>
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

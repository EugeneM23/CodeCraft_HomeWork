using System.Collections;
using System.IO;
using System.Reflection;
using Atomic.Entities;
using Game.AIKit.Adapters;
using Game.Horde;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The machine-gun look experiment in VoxelSandbox: the big gun turning to a horde, its yellow bullets and the
    /// casings on the ground. Saves close-ups of the gun and one wide shot to Logs/mg-shots/ under the project folder.
    /// The main camera is moved for each shot and put back at once (it keeps URP's post-processing: the bloom).
    /// </summary>
    public sealed class MgLookSceneTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string HORDE_ART = "Assets/Game/Horde/Art/RegaxHordeArtConfig.asset";
        private const int SHOT_WIDTH = 1600;
        private const int SHOT_HEIGHT = 900;

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BigMachineGun_TurnsAndFires_CasingsLieOnTheGround()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            // The sandbox's random air strikes and its elites would get in the way: off for the test.
            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);
            foreach (GameObject placed in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (placed.name.StartsWith("Regax_Elite_"))
                    placed.SetActive(false);

            GameContext context = GameContext.Instance;
            Assert.That(context, Is.Not.Null, "the sandbox has a GameContext");

            Transform tank = FindTank(context);
            Assert.That(tank, Is.Not.Null, "the player's tank");

            Transform rig = FindChild(tank, "MachineGunRig");
            Assert.That(rig, Is.Not.Null, "the big machine gun was built on the turret (WeaponSettings.mgBigModel)");
            context.TryGetBehaviour(out CasingField casings);
            Assert.That(casings, Is.Not.Null, "the level has a CasingField (WeaponSettings.casingsOn)");

            CreateHorde(20);

            // Wait for the first shot (the crawlers come into range), then let it fire for about 4 s.
            float waited = 0f;
            while (casings.Count == 0 && waited < 20f)
            {
                yield return null;
                waited += Time.deltaTime;
            }

            Assert.That(casings.Count, Is.GreaterThan(0), "the machine gun fired within 20 s");

            int shot = 0;
            float firing = 0f;
            float nextShot = 1.2f;
            while (firing < 4f)
            {
                yield return null;
                firing += Time.deltaTime;
                if (shot < 3 && firing >= nextShot)
                {
                    shot++;
                    nextShot += 1.1f;
                    CloseUp(tank, rig, shot.ToString());
                }
            }

            // Casings in the air settle within a second.
            for (float t = 0f; t < 1f; t += Time.deltaTime)
                yield return null;

            Wide(tank, "4-casings-wide");

            Debug.Log($"[MgLookTest] first shot after {waited:F1} s, casings {casings.Count} ({casings.Flying} flying), " +
                      $"gun yaw {rig.localEulerAngles.y:F0}°");
        }

        private static Transform FindTank(GameContext context)
        {
            if (context.TryGetPlayerContext(out PlayerContext player) && player != null)
            {
                IEntity character = player.GetCharacter().Value;
                if (character != null && character.HasVehicleTag())
                {
                    if (character is Component component)
                        return component.transform;
                    if (character.TryGetTransform(out Transform transform))
                        return transform;
                }
            }

            return GameObject.Find("Tank_01")?.transform;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private static void CreateHorde(int population)
        {
            var art = AssetDatabase.LoadAssetAtPath<HordeArtConfig>(HORDE_ART);
            Assert.That(art, Is.Not.Null, HORDE_ART + " (Tools/Horde/Bake Regax VAT)");

            foreach (HordeInstaller old in Object.FindObjectsByType<HordeInstaller>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);

            var options = new HordeOptions
            {
                capacity = Mathf.Max(population, 64),
                population = population,
                tankDamageScale = 0f,
                castShadows = false
            };
            HordeSpawn.CreateInScene(art, options);
        }

        // ---------- Screenshots ----------

        /// <summary>5 m off the tank's right-rear and above, looking at the gun.</summary>
        private static void CloseUp(Transform tank, Transform rig, string name)
        {
            Vector3 gun = rig.position + Vector3.up * 0.35f;
            Vector3 from = gun + (tank.right - tank.forward).normalized * 4.3f + Vector3.up * 2.5f;
            Shot(name, from, gun + tank.forward * 0.6f);
        }

        /// <summary>Higher and further out on the right: the ground the casings fall on.</summary>
        private static void Wide(Transform tank, string name)
        {
            Vector3 from = tank.position + tank.right * 8f - tank.forward * 3f + Vector3.up * 6f;
            Shot(name, from, tank.position + tank.right * 2.5f);
        }

        /// <summary>
        /// Camera.main moved to <paramref name="from"/>, looking at <paramref name="at"/>, into a 1600×900 PNG at
        /// Logs/mg-shots/&lt;name&gt;.png; the camera is put back right after. A failed shot only logs.
        /// </summary>
        private static void Shot(string name, Vector3 from, Vector3 at)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning($"[MgLookTest] no Camera.main for the shot {name}");
                return;
            }

            // The voxel smoke is drawn in LateUpdate, after this coroutine step: issue this frame's draws now.
            var smokes = global::Game.Voxels.VoxelSmoke.Active;
            for (int i = 0; i < smokes.Count; i++)
                smokes[i].VoxelLateUpdate(0f);

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
                byte[] png = texture.EncodeToPNG();

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "mg-shots"));
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, name + ".png");
                File.WriteAllBytes(path, png);
                Debug.Log($"[MgLookTest] shot {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MgLookTest] shot {name} failed: {e.Message}");
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

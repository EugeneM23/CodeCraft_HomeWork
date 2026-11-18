using System.Collections;
using System.IO;
using System.Reflection;
using Atomic.Elements;
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
    /// The combat systems in VoxelSandbox with the game's own tank: the auto gun against a horde of crawlers and their
    /// experience, the run skills at level 3, the smoke launchers and the stun. Screenshots go to
    /// Logs/combat-shots/ under the project folder.
    /// </summary>
    public sealed class CombatSandboxTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string HORDE_ART = "Assets/Game/Horde/Art/RegaxHordeArtConfig.asset";
        private const int SKILL_TARGET = 3;
        private const int MAX_PICKS = 40;
        private const int SHOT_WIDTH = 1600;
        private const int SHOT_HEIGHT = 900;

        private static readonly SkillId[] MilestoneSkills =
        {
            SkillIds.Turret, SkillIds.AirStrike, SkillIds.Ricochet, SkillIds.RateOfFire, SkillIds.Armor, SkillIds.Magnet
        };

        // GDD §3, levels 1..5: checked against what the skills do to the tank.
        private static readonly int[] TurretsByLevel = { 1, 1, 2, 2, 3 };
        private static readonly float[] ReloadByLevel = { 0.88f, 0.80f, 0.72f, 0.64f, 0.56f };
        private static readonly float[] HealthByLevel = { 1.15f, 1.30f, 1.45f, 1.60f, 1.75f };
        private static readonly float[] PickupByLevel = { 1.5f, 1.8f, 2.1f, 2.4f, 2.7f };

        private IEntity _tank;
        private Transform _tankTransform;
        private IRunService _run;
        private IReactiveVariable<Vector2> _driveInput;
        private Vector2 _drive;
        private GameObject _hordeObject;
        private int _picks;

        private int _xpPicked;
        private int _bombsDropped;
        private int _strikeBlasts;
        private bool _bombFalling;
        private int _lastBlastFrame = -1;

        // ---------- Setup ----------

        private IEnumerator LoadSandbox()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            // The sandbox's random air strikes would kill the crawlers and shake the tank: off for the test.
            FieldInfo on = typeof(VoxelAirStrike).GetField("_on", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (VoxelAirStrike strike in Object.FindObjectsByType<VoxelAirStrike>(FindObjectsSortMode.None))
                on?.SetValue(strike, false);

            // The sandbox's own elites would bite and stun the tank.
            foreach (GameObject placed in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (placed.name.StartsWith("Regax_Elite_"))
                    placed.SetActive(false);

            GameContext context = GameContext.Instance;
            Assert.That(context, Is.Not.Null, "the sandbox has a GameContext");

            // The player sits in the tank: that entity is the one combat attached to.
            if (context.TryGetPlayerContext(out PlayerContext player) && player != null)
            {
                IEntity character = player.GetCharacter().Value;
                if (character != null && character.HasVehicleTag())
                    _tank = character;
            }

            if (_tank == null)
                _tank = GameObject.Find("Tank_01")?.GetComponent<MonoEntity>();

            Assert.That(_tank, Is.Not.Null, "the player's tank");
            _tankTransform = _tank is Component component ? component.transform : null;
            if (_tankTransform == null)
                _tank.TryGetTransform(out _tankTransform);
            Assert.That(_tankTransform, Is.Not.Null, "the tank's transform");
            Assert.IsTrue(_tank.TryGetCombatModifiers(out CombatModifiers _), "combat attached to the tank (CombatModifiers)");

            Assert.IsTrue(context.TryGetRun(out _run) && _run != null, "the sandbox has a run (LocalRunService)");
            Assert.IsTrue(_tank.TryGetDriveInput(out _driveInput), "the tank has DriveInput");

            _drive = Vector2.zero;
            _picks = 0;
            _xpPicked = 0;
            _bombsDropped = 0;
            _strikeBlasts = 0;
            _bombFalling = false;
            _lastBlastFrame = -1;

            Application.onBeforeRender += WriteDrive;
            CombatSignals.XpPicked += OnXpPicked;
            GameSignals.AirBombDropped += OnBombDropped;
            ExplosionEvents.Exploded += OnExploded;
        }

        [TearDown]
        public void TearDown()
        {
            Application.onBeforeRender -= WriteDrive;
            CombatSignals.XpPicked -= OnXpPicked;
            GameSignals.AirBombDropped -= OnBombDropped;
            ExplosionEvents.Exploded -= OnExploded;
            _drive = Vector2.zero;
            _tank = null;
            _run = null;
            _driveInput = null;
            _hordeObject = null;
        }

        // The player's controller writes the stick in Update; this lands after it, before the next FixedUpdate.
        private void WriteDrive()
        {
            if (_driveInput != null && _drive != Vector2.zero)
                _driveInput.Value = _drive;
        }

        private void OnXpPicked(Vector3 point, int amount) => _xpPicked += amount;

        private void OnBombDropped(Transform bomb)
        {
            _bombsDropped++;
            _bombFalling = true;
        }

        private void OnExploded(Vector3 point, float radius)
        {
            if (!_bombFalling)
                return;
            _bombFalling = false;
            _strikeBlasts++;
            _lastBlastFrame = Time.frameCount;
        }

        private HordeBehaviour CreateHorde(int population)
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
            HordeInstaller installer = HordeSpawn.CreateInScene(art, options);
            _hordeObject = installer.gameObject;
            return installer.Behaviour;
        }

        private int HordeKills() =>
            _hordeObject != null && _hordeObject.TryGetComponent(out MonoEntity entity) ? entity.GetHordeKills().Value : 0;

        private int OrbsOnTheGround() =>
            GameContext.Instance.TryGetBehaviour(out XpOrbField field) && field != null ? field.Buffer.Count : 0;

        /// <summary>
        /// Picks from a waiting offer so the level-up pause never stalls the test: the lowest skill still under
        /// <see cref="SKILL_TARGET"/> first, otherwise the lowest one.
        /// </summary>
        private void TendSkills()
        {
            if (_run.PendingLevelUps <= 0 || _run.SkillOffer.Count == 0)
                return;

            SkillId pick = _run.SkillOffer[0];
            int best = int.MaxValue;
            for (int i = 0; i < _run.SkillOffer.Count; i++)
            {
                int level = _run.GetSkillLevel(_run.SkillOffer[i]);
                int score = level < SKILL_TARGET ? level : 100 + level;
                if (score < best)
                {
                    best = score;
                    pick = _run.SkillOffer[i];
                }
            }

            if (_run.ChooseSkill(pick))
                _picks++;
        }

        private bool SkillsAtTarget()
        {
            foreach (SkillId skill in MilestoneSkills)
                if (_run.GetSkillLevel(skill) < SKILL_TARGET)
                    return false;
            return true;
        }

        private string SkillLevels()
        {
            string s = "";
            foreach (SkillId skill in MilestoneSkills)
                s += $"{skill.Value}={_run.GetSkillLevel(skill)} ";
            return s.TrimEnd();
        }

        private static float FlatSpeed(Vector3 from, Vector3 to, float deltaTime)
        {
            if (deltaTime <= 0f)
                return 0f;
            Vector3 step = to - from;
            step.y = 0f;
            return step.magnitude / deltaTime;
        }

        private static float ByLevel(float[] table, int level) => table[Mathf.Clamp(level - 1, 0, table.Length - 1)];

        private static int ByLevel(int[] table, int level) => table[Mathf.Clamp(level - 1, 0, table.Length - 1)];

        // ---------- Tests ----------

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AutoGun_KillsTheCrawlers_AndTheTankCollectsTheirExperience()
        {
            yield return LoadSandbox();

            int startLevel = _run.Level;
            int startExperience = _run.Experience;
            CreateHorde(30);

            int stillKills = 0;
            int lastKills = 0;
            int maxOrbs = 0;
            float maxSpeed = 0f;
            bool shot6 = false;
            bool shot12 = false;
            Vector3 last = _tankTransform.position;

            float t = 0f;
            while (t < 20f)
            {
                yield return null;
                t += Time.deltaTime;
                TendSkills();

                Vector3 now = _tankTransform.position;
                float speed = FlatSpeed(last, now, Time.deltaTime);
                last = now;
                maxSpeed = Mathf.Max(maxSpeed, speed);

                int kills = HordeKills();
                if (kills > lastKills && speed < 0.5f)
                    stillKills += kills - lastKills;
                lastKills = kills;
                maxOrbs = Mathf.Max(maxOrbs, OrbsOnTheGround());

                if (!shot6 && t >= 6f)
                {
                    shot6 = true;
                    Shot("1-autogun-6s");
                }

                if (!shot12 && t >= 12f)
                {
                    shot12 = true;
                    Shot("1-autogun-12s");
                }

                bool xpGrew = _run.Level > startLevel || _run.Experience > startExperience || _xpPicked > 0;
                if (shot12 && lastKills > 0 && xpGrew)
                    break;
            }

            Debug.Log($"[CombatTest] autogun: {t:F1} s, horde kills {lastKills} (tank still {stillKills}), run kills {_run.Kills}, " +
                      $"orbs on the ground max {maxOrbs}, xp picked {_xpPicked}, level {startLevel} -> {_run.Level} " +
                      $"(xp {_run.Experience}/{_run.ExperienceToNextLevel}), picks {_picks}, tank max speed {maxSpeed:F2}");

            Assert.That(lastKills, Is.GreaterThan(0), "the auto gun killed crawlers");
            Assert.That(stillKills, Is.GreaterThan(0), "kills came while the tank stood (shots, not crushes)");
            Assert.That(maxOrbs > 0 || _xpPicked > 0, Is.True, "the dead dropped experience orbs");
            Assert.That(_run.Level > startLevel || _run.Experience > startExperience || _xpPicked > 0, Is.True,
                "the tank collected experience");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Skills_AtLevelThree_ChangeTheTank()
        {
            yield return LoadSandbox();

            Assert.IsTrue(_tank.TryGetCombatModifiers(out CombatModifiers modifiers));
            Assert.IsTrue(_tank.TryGetHealth(out Health health), "the tank has health");
            Assert.IsTrue(_tank.TryGetWeapon(out IReactiveVariable<IEntity> cannonVariable) && cannonVariable.Value != null,
                "the tank has a cannon");
            Assert.IsTrue(cannonVariable.Value.TryGetWeaponCooldown(out CooldownTimer cooldown), "the cannon has a cooldown");

            // The loadout without skills: what the skills multiply.
            int baseMaxHealth = health.GetMax();
            float baseReload = cooldown.Duration;
            Assert.That(modifiers.MaxHealthFactor, Is.EqualTo(1f).Within(1e-4f), "no skills before the test's picks");

            while (!SkillsAtTarget() && _picks < MAX_PICKS)
            {
                if (_run.PendingLevelUps <= 0)
                    _run.AddExperience(Mathf.Max(1, _run.ExperienceToNextLevel - _run.Experience));
                TendSkills();
                yield return null;
            }

            // A level-up left over from the last feed must not hold the pause.
            for (int i = 0; i < 10 && _run.PendingLevelUps > 0; i++)
            {
                TendSkills();
                yield return null;
            }

            int turret = _run.GetSkillLevel(SkillIds.Turret);
            int airStrike = _run.GetSkillLevel(SkillIds.AirStrike);
            int ricochet = _run.GetSkillLevel(SkillIds.Ricochet);
            int rateOfFire = _run.GetSkillLevel(SkillIds.RateOfFire);
            int armor = _run.GetSkillLevel(SkillIds.Armor);
            int magnet = _run.GetSkillLevel(SkillIds.Magnet);

            string summary = $"skills {SkillLevels()}, picks {_picks}, run level {_run.Level}; modifiers: turrets {modifiers.TurretCount}, " +
                             $"air strike {modifiers.AirStrikeLevel}, ricochet {modifiers.RicochetBounces}, reload x{modifiers.CannonReloadFactor:F2}, " +
                             $"health x{modifiers.MaxHealthFactor:F2}, pickup x{modifiers.PickupRadiusFactor:F2}; health max {baseMaxHealth} -> {health.GetMax()}, " +
                             $"cannon reload {baseReload:F2} -> {cooldown.Duration:F2} s";
            Debug.Log("[CombatTest] skills: " + summary);

            Assert.IsTrue(SkillsAtTarget(), "all six skills reached level 3 within 40 picks: " + summary);

            // At level 3 these are the brief's numbers (turrets 2, air strike 3, ricochet 3, x0.72, x1.45, x2.1);
            // a skill the offers pushed past 3 is checked at its own level.
            Assert.That(modifiers.TurretCount, Is.EqualTo(ByLevel(TurretsByLevel, turret)), "turrets");
            Assert.That(modifiers.AirStrikeLevel, Is.EqualTo(airStrike), "air strike level");
            Assert.That(modifiers.RicochetBounces, Is.EqualTo(ricochet), "ricochet bounces");
            Assert.That(modifiers.CannonReloadFactor, Is.EqualTo(ByLevel(ReloadByLevel, rateOfFire)).Within(1e-3f), "reload factor");
            Assert.That(modifiers.MaxHealthFactor, Is.EqualTo(ByLevel(HealthByLevel, armor)).Within(1e-3f), "health factor");
            Assert.That(modifiers.PickupRadiusFactor, Is.EqualTo(ByLevel(PickupByLevel, magnet)).Within(1e-3f), "pickup factor");

            // Tiger: 1000 × 1.45 = 1450 and 2 × 0.72 = 1.44 s with the plain loadout.
            Assert.That(health.GetMax(), Is.EqualTo(Mathf.RoundToInt(baseMaxHealth * modifiers.MaxHealthFactor)).Within(2),
                "max health follows armor");
            Assert.That(cooldown.Duration, Is.EqualTo(baseReload * modifiers.CannonReloadFactor).Within(0.01f),
                "cannon reload follows rate of fire");

            // The turrets on the hull.
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                yield return null;
                TendSkills();
            }

            Shot("2-turrets");

            // A horde in reach: the air strike drops on its densest part every 8 s at level 3.
            CreateHorde(40);
            bool blastShot = false;
            float waited = 0f;
            while (waited < 14f)
            {
                yield return null;
                waited += Time.deltaTime;
                TendSkills();

                if (!blastShot && _lastBlastFrame > 0 && Time.frameCount - _lastBlastFrame >= 4)
                {
                    blastShot = true;
                    Shot("2-air-strike-blast");
                }

                if (waited >= 12f && blastShot)
                    break;
            }

            Debug.Log($"[CombatTest] air strike: bombs {_bombsDropped}, blasts {_strikeBlasts} in {waited:F1} s; " +
                      $"horde kills {HordeKills()}, health {health.GetCurrent()}/{health.GetMax()}");

            Assert.That(_bombsDropped, Is.GreaterThan(0), "the air strike dropped a bomb on the horde");
            Assert.That(_strikeBlasts, Is.GreaterThan(0), "the bomb went off");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SmokeLaunchers_HideTheTank_ThenReload()
        {
            yield return LoadSandbox();

            Assert.IsTrue(_tank.TryGetSpecialModule(out ISpecialModule special) && special != null, "the tank has a special module");
            Assert.IsTrue(GameContext.Instance.TryGetSmokeScreen(out ISmokeScreen screen) && screen != null, "the level has a smoke screen");
            Assert.IsTrue(special.IsReady, "ready before use");

            bool fired = special.TryActivate();
            bool inside = screen.Contains(_tankTransform.position);
            bool readyAfter = special.IsReady;
            float charge = special.Charge01;
            bool second = special.TryActivate();

            Debug.Log($"[CombatTest] smoke: fired {fired}, active {screen.IsActive}, tank inside {inside}, ready after {readyAfter}, " +
                      $"charge {charge:F2}, remaining {special.Remaining:F1} s, second {second}");

            Assert.IsTrue(fired, "the smoke launchers fired");
            Assert.IsTrue(inside, "the smoke screen covers the tank");
            Assert.IsFalse(readyAfter, "reloading after the shot");
            Assert.That(charge, Is.LessThan(0.1f), "the charge starts over");
            Assert.IsFalse(second, "no second shot while reloading");

            // A cloud fills out in 1.5 s.
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                yield return null;
                TendSkills();
            }

            Shot("3-smoke");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Stun_StopsTheTank()
        {
            yield return LoadSandbox();

            // The player's drive controller writes the (idle) stick into DriveInput every LateTick, after anything a
            // test writes in Update; Application.onBeforeRender, which would land after it, does not run in batchmode
            // (nothing renders). Without the controller the test's stick holds: it is written every frame below.
            Assert.IsTrue(GameContext.Instance.TryGetPlayerContext(out PlayerContext player) && player != null,
                "the sandbox has a player context");
            player.DelBehaviour<ControllableDriveController>();

            // Full gas forward for 2.5 s.
            _drive = new Vector2(0f, 1f);
            _driveInput.Value = _drive;
            Vector3 last = _tankTransform.position;
            float speed = 0f;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                yield return null;
                _driveInput.Value = _drive;
                TendSkills();
                Vector3 now = _tankTransform.position;
                speed = Mathf.Lerp(speed, FlatSpeed(last, now, Time.deltaTime), 0.3f);
                last = now;
            }

            float speedBefore = speed;
            float stunAt = GameTicks.Time;
            TankStunUseCase.Stun(_tank, 1.5f);
            bool stunned = TankStunUseCase.IsStunned(_tank);

            // Still on the gas: the stun is what stops it.
            float lowest = float.MaxValue;
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                yield return null;
                _driveInput.Value = _drive;
                TendSkills();
                Vector3 now = _tankTransform.position;
                speed = Mathf.Lerp(speed, FlatSpeed(last, now, Time.deltaTime), 0.3f);
                last = now;
                lowest = Mathf.Min(lowest, speed);
            }

            bool stillStunned = TankStunUseCase.IsStunned(_tank);

            float waited = 0f;
            while (GameTicks.Time < stunAt + 2f && waited < 10f)
            {
                yield return null;
                waited += Time.deltaTime;
                TendSkills();
            }

            bool stunnedAfter = TankStunUseCase.IsStunned(_tank);
            _drive = Vector2.zero;
            _driveInput.Value = Vector2.zero;

            Debug.Log($"[CombatTest] stun: speed before {speedBefore:F2} m/s, lowest while stunned {lowest:F2} m/s, " +
                      $"stunned {stunned} / at 1.3 s {stillStunned} / at 2 s {stunnedAfter}");

            Assert.That(speedBefore, Is.GreaterThan(1f), "the tank was driving before the stun");
            Assert.IsTrue(stunned, "stunned at once");
            Assert.IsTrue(stillStunned, "still stunned 1.3 s into a 1.5 s stun");
            Assert.That(lowest, Is.LessThan(0.5f), "the stun stopped the tank (below 0.5 m/s while stunned)");
            Assert.IsFalse(stunnedAfter, "the stun is over after 2 s");
        }

        // ---------- Screenshots ----------

        /// <summary>
        /// Camera.main into a 1600×900 PNG at Logs/combat-shots/&lt;name&gt;.png (IMGUI is not in it). A failed shot only logs.
        /// </summary>
        private static void Shot(string name)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning($"[CombatTest] no Camera.main for the shot {name}");
                return;
            }

            // The voxel smoke is drawn with Graphics.DrawMeshInstanced in LateUpdate, after this coroutine step:
            // its draws of the last frame are spent, so issue this frame's now (dt 0 only draws) for cam.Render.
            var smokes = global::Game.Voxels.VoxelSmoke.Active;
            for (int i = 0; i < smokes.Count; i++)
                smokes[i].VoxelLateUpdate(0f);

            var rt = new RenderTexture(SHOT_WIDTH, SHOT_HEIGHT, 24);
            RenderTexture oldTarget = cam.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                texture = new Texture2D(SHOT_WIDTH, SHOT_HEIGHT, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, SHOT_WIDTH, SHOT_HEIGHT), 0, 0);
                texture.Apply();
                byte[] png = texture.EncodeToPNG();

                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "combat-shots"));
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, name + ".png");
                File.WriteAllBytes(path, png);
                Debug.Log($"[CombatTest] shot {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CombatTest] shot {name} failed: {e.Message}");
            }
            finally
            {
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

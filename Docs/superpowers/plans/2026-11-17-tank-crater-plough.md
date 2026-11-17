# Танк роет склон воронки носом — план реализации

> **Для агентов:** обязательный навык — superpowers:subagent-driven-development (или superpowers:executing-plans). Шаги отмечаются чекбоксами (`- [ ]`).

**Цель:** танк, бьющийся носом о землю на скорости, вырезает грунт, бросает его вперёд-вверх, теряет около трети скорости, задирает нос и выскакивает из воронки.

**Архитектура:** Atomic-поведение `TankPloughBehaviour` в физическом шаге. Оно упреждает удар корпуса BoxCast'ом по земле (слой 3), вынимает грунт `VoxelBody.Dig` и бросает его через `IEarthThrow`, а скорость после удара считает само (`TankPloughMath`). Числа живут в `TankPloughConfig` (ScriptableObject). Три пресета переключаются клавишей M в песочнице.

**Стек:** Unity 6000.3.28f1, URP, Atomic 3.0 (`IEntityInit`, `IEntityFixedTick`), PhysX, NUnit (EditMode — `Game.Tests`, PlayMode — `Game.Voxels.SceneTests`).

**Спецификация:** `Docs/superpowers/specs/2026-11-17-tank-crater-plough.md`.

## Общие ограничения

- Код, имена и комментарии на английском; `namespace Game` в сборке `Game`, `Game.Tests` в тестах, `Game.Voxels.Tests` в сценных тестах.
- Ноль GC в тике: `Physics.BoxCastNonAlloc` с заранее выделенным буфером, без LINQ, без `GetComponent`/`Find`, без строк в тике.
- Поля `_camelCase`, `const` — `UPPER_SNAKE`, `static readonly` — PascalCase, приватные статические — `s_camelCase`. У неочевидных `[SerializeField]` есть `[Tooltip]`.
- Один верхнеуровневый тип на файл, имя файла равно имени типа.
- Файлы чужих команд не меняются: `VoxelBody`, `VoxelDebris`, `VoxelAirStrike`, `VoxelRammer`, `TankDriveBehaviour`. Только вызовы их публичных методов.
- Земля — слой 3 (`1 << 3`), тело земли — `VoxelBody` с `IsTerrain`; найти — по `VoxelWorld.Bodies` и `WorldBounds`.
- Коммиты только своих путей (`git add <пути>`), сообщение `<Область>: <что>` + строка `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Ничего не пушить.
- Тесты в редакторе своей копии (`tank-crater-112bdb`): EditMode через `unity` CLI / MCP `test_run`; PlayMode — `async-tests: true`, результат читается из консоли (`Run finished`). Перед запуском проверить, что открытые сцены не грязные.

## Файлы

| Файл | Что |
|---|---|
| `Assets/Game/Voxels/Tests/Scene/SandboxCraterPloughTest.cs` | PlayMode: воронка перед танком, въезд на газу, замер скорости |
| `Assets/Game/Scripts/Features/Controllables/Tank/Drive/TankPloughMath.cs` | сила удара, «удар носом», скорость и направление после удара, выброс |
| `Assets/Game/Tests/Features/Controllables/Tank/Drive/TankPloughMathTests.cs` | EditMode-тесты математики |
| `…/Tank/Drive/TankPloughConfig.cs` | ScriptableObject с числами пресета |
| `…/Tank/Drive/IEarthThrow.cs`, `DigEarthThrow.cs` | вынуть и бросить грунт; временная реализация на `Dig` |
| `…/Tank/Drive/TankPloughEvents.cs` | `static event Action<Vector3, float> Ploughed` |
| `…/Tank/Drive/TankPloughBehaviour.cs` | упреждение, вырез, отклик, страховка, тряска, событие |
| `…/Tank/TankInstaller.cs` | поля `_hullCollider`, `_ploughPresets`, `_ploughPreset`; подключение поведения; `NextPloughPreset()` |
| `Assets/Game/Configs/Tank/TankPlough{Subtle,Punchy,Cinema}Config.asset` | три пресета |
| `Assets/Game/Prefabs/Vehicles/Tank_01.prefab` | ссылки на коллайдер корпуса и пресеты |
| `Assets/Game/Configs/Input/GameControls.inputactions` (+ генерируемый `Shared/Input/GameControls.cs`) | действие `Sandbox/Plough` на M |
| `Assets/Game/Scripts/Features/Voxels/VoxelSandboxPloughSwitch.cs` + сцена `VoxelSandbox.unity` | клавиша и строка HUD |

---

### Задача 1: красный сценный тест «танк встаёт в воронке»

**Файлы:**
- Создать: `Assets/Game/Voxels/Tests/Scene/SandboxCraterPloughTest.cs`

**Интерфейсы:**
- Использует: `VoxelDamage.AirBlast(point, 4f, 32f, 24f, 0, 0.85f, 24f)` (без глыб, чтобы они не мешали), `GameContext.Instance.GetPlayerContext().GetCharacter()`, `IEntity.GetDriveInput()`, `IEntity.TryGetDriveBoost(out IReactiveVariable<bool>)`.
- Даёт: тест `Sandbox_TankHitsCraterSlopeAtSpeed_BreaksOutWithoutStopping`, лог-строки `[CraterPlough] …` с цифрами.

- [ ] **Шаг 1: написать тест**

```csharp
using System.Collections;
using System.IO;
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
    /// VoxelSandbox: an air bomb's crater on open ground ahead of the tank, and the tank driven into
    /// it on full gas with Shift. It must not stop dead against the far slope: it keeps at least half
    /// its entry speed there and is out over the far rim within 2.5 s of entering.
    /// Screenshots go to Logs/CraterPlough.
    /// </summary>
    public sealed class SandboxCraterPloughTest
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string OUT = "Logs/CraterPlough";
        private const float RIM = 4f;
        private const float RUN_UP = 14f;
        private const int GROUND = 1 << 3;

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Sandbox_TankHitsCraterSlopeAtSpeed_BreaksOutWithoutStopping()
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
                for (int i = 0; i < 90; i++)
                    yield return new WaitForFixedUpdate();

                // No bombs but ours.
                foreach (VoxelAirStrikeSwitch strike in Object.FindObjectsByType<VoxelAirStrikeSwitch>(FindObjectsSortMode.None))
                    strike.SetOn(false);

                GameObject tankObject = GameObject.Find("Tank_01");
                IEntity tank = tankObject.GetComponent<MonoEntity>();
                Rigidbody body = tankObject.GetComponent<Rigidbody>();

                Assert.IsTrue(TryFindRunway(out Vector3 start, out Vector3 forward), "open ground for a run-up and a crater");
                Vector3 crater = start + forward * (RUN_UP + RIM);
                VoxelDamage.AirBlast(crater, RIM, 32f, 24f, 0, 0.85f, 24f);
                for (int i = 0; i < 10; i++)
                    yield return null; // the crater's collider is rebuilt a frame or two later

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = start + Vector3.up * 0.4f;
                body.rotation = Quaternion.LookRotation(forward);
                tankObject.transform.SetPositionAndRotation(body.position, body.rotation);
                for (int i = 0; i < 40; i++)
                    yield return new WaitForFixedUpdate();

                Directory.CreateDirectory(OUT);
                IReactiveVariable<Vector2> drive = tank.GetDriveInput();
                tank.TryGetDriveBoost(out IReactiveVariable<bool> boost);

                float entrySpeed = -1f, entryTime = -1f, exitTime = -1f;
                float minFarSpeed = float.MaxValue;
                float until = Time.time + 8f;
                int shot = 0;
                while (Time.time < until && exitTime < 0f)
                {
                    drive.Value = new Vector2(0f, 1f);
                    if (boost != null)
                        boost.Value = true;
                    yield return new WaitForEndOfFrame();

                    // Distance along the run from the crater's centre: -RIM at the near rim, +RIM at the far one.
                    float along = Vector3.Dot(body.position - crater, forward);
                    float speed = Vector3.Dot(body.linearVelocity, forward);
                    if (entryTime < 0f && along >= -RIM)
                    {
                        entryTime = Time.time;
                        entrySpeed = speed;
                    }

                    if (entryTime >= 0f && along > 0f)
                        minFarSpeed = Mathf.Min(minFarSpeed, speed);
                    if (entryTime >= 0f && along > RIM + 1.4f)
                        exitTime = Time.time;
                    if (entryTime >= 0f && shot < 12 && Time.frameCount % 4 == 0)
                        Capture($"{shot++:00}_crater");
                }

                float took = exitTime >= 0f ? exitTime - entryTime : float.PositiveInfinity;
                Debug.Log($"[CraterPlough] entry {entrySpeed:F2} m/s, min on far slope {minFarSpeed:F2} m/s " +
                          $"({minFarSpeed / Mathf.Max(0.01f, entrySpeed):P0}), out in {took:F2} s");

                Assert.That(entrySpeed, Is.GreaterThan(6f), "the tank came in fast");
                Assert.That(minFarSpeed, Is.GreaterThanOrEqualTo(entrySpeed * 0.5f), "kept half its speed on the far slope");
                Assert.That(took, Is.LessThan(2.5f), "out over the far rim within 2.5 s");
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
```

`VoxelAirStrikeSwitch` в коде нет: исполнитель проверяет, как выключить таймер авиаудара. `VoxelAirStrike._on` — приватное поле, в `perf_bench.cs` его выключают рефлексией. Сделать так же: `typeof(VoxelAirStrike).GetField("_on", NonPublic|Instance).SetValue(strike, false)` для каждого `VoxelAirStrike` в сцене. Тип `VoxelAirStrike` лежит в сборке `Game`, она в ссылках `Game.Voxels.SceneTests` есть.

- [ ] **Шаг 2: прогнать тест — он должен упасть**

Запуск: PlayMode, фильтр `SandboxCraterPloughTest`, `async-tests: true`. Результат — в консоли, строки `[CraterPlough]` и `Run finished`.
Ожидается: FAIL на «kept half its speed on the far slope» или «out over the far rim». Цифры из `[CraterPlough]` записать в отчёт задачи. Если тест прошёл, условия не воспроизводят удар: исполнитель останавливается и сообщает цифры, не подгоняя тест.

- [ ] **Шаг 3: коммит**

```bash
git add Assets/Game/Voxels/Tests/Scene/SandboxCraterPloughTest.cs Assets/Game/Voxels/Tests/Scene/SandboxCraterPloughTest.cs.meta
git commit -m "Tests: tank driven into a crater stops against the far slope (red)"
```

---

### Задача 2: `TankPloughMath` и её тесты

**Файлы:**
- Создать: `Assets/Game/Scripts/Features/Controllables/Tank/Drive/TankPloughMath.cs`
- Тест: `Assets/Game/Tests/Features/Controllables/Tank/Drive/TankPloughMathTests.cs`

**Интерфейсы — даёт:**
```csharp
public static class TankPloughMath
{
    public const float NOSE_FACING = -0.2f;
    public static float Strength(float closing, float minClosing, float fullClosing);
    public static bool IsNoseHit(Vector3 localFromHullCentre, Vector3 normal, Vector3 forward, float closing, float minClosing);
    public static float SpeedAfter(float speed, float strength, float speedLoss);
    public static Vector3 Deflect(Vector3 velocity, Vector3 normal, float strength, float speedLoss, float deflect, float hop);
    public static Vector3 Throw(Vector3 velocity, Vector3 normal, float lift, float scale);
}
```

- [ ] **Шаг 1: тесты**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class TankPloughMathTests
    {
        private static readonly Vector3 Slope32 = new Vector3(0f, Mathf.Cos(32f * Mathf.Deg2Rad), -Mathf.Sin(32f * Mathf.Deg2Rad));

        [Test]
        public void Strength_IsZeroBelowMinClosing_AndOneFromFullClosing()
        {
            Assert.That(TankPloughMath.Strength(1.9f, 2f, 8f), Is.EqualTo(0f));
            Assert.That(TankPloughMath.Strength(5f, 2f, 8f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(TankPloughMath.Strength(9f, 2f, 8f), Is.EqualTo(1f));
        }

        [Test]
        public void HardHit_KeepsTwoThirdsOfTheSpeed()
        {
            Assert.That(TankPloughMath.SpeedAfter(9f, 1f, 0.33f), Is.EqualTo(6.03f).Within(1e-4f));
        }

        [Test]
        public void LightHit_StillCostsHalfTheLoss()
        {
            Assert.That(TankPloughMath.SpeedAfter(6f, 0.1f, 0.33f), Is.EqualTo(6f * (1f - 0.165f)).Within(1e-4f));
        }

        [Test]
        public void LevelHullIntoA32DegreeSlope_IsANoseHit()
        {
            var v = new Vector3(0f, 0f, 8f);
            float closing = -Vector3.Dot(v, Slope32);
            Assert.IsTrue(TankPloughMath.IsNoseHit(new Vector3(0f, -0.15f, 1.3f), Slope32, Vector3.forward, closing, 2f));
        }

        [Test]
        public void FlatBellyLanding_IsNotANoseHit()
        {
            var v = new Vector3(0f, -6f, 4f);
            float closing = -Vector3.Dot(v, Vector3.up);
            Assert.IsFalse(TankPloughMath.IsNoseHit(new Vector3(0f, -0.2f, 0.5f), Vector3.up, Vector3.forward, closing, 2f));
        }

        [Test]
        public void NoseDownLanding_IsANoseHit()
        {
            Vector3 forward = Quaternion.Euler(30f, 0f, 0f) * Vector3.forward; // pitched 30° nose down
            var v = new Vector3(0f, -5f, 6f);
            float closing = -Vector3.Dot(v, Vector3.up);
            Assert.IsTrue(TankPloughMath.IsNoseHit(new Vector3(0f, -0.2f, 1.2f), Vector3.up, forward, closing, 2f));
        }

        [Test]
        public void HitBehindTheMiddle_IsNotANoseHit()
        {
            var v = new Vector3(0f, 0f, 8f);
            float closing = -Vector3.Dot(v, Slope32);
            Assert.IsFalse(TankPloughMath.IsNoseHit(new Vector3(0f, -0.15f, -0.4f), Slope32, Vector3.forward, closing, 2f));
        }

        [Test]
        public void Deflect_TurnsTheVelocityUpTheSlope_AndCostsTheLoss()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 after = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0.3f, 0f);
            Assert.That(after.y, Is.GreaterThan(0f), "goes up");
            Assert.That(after.z, Is.GreaterThan(0f), "still forward");
            Assert.That(Vector3.Dot(after, Slope32), Is.GreaterThanOrEqualTo(0f), "no longer into the slope");
            Assert.That(after.magnitude, Is.EqualTo(8f * 0.67f).Within(1e-3f));
        }

        [Test]
        public void Deflect_HopAddsStraightUp()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 without = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0.3f, 0f);
            Vector3 with = TankPloughMath.Deflect(v, Slope32, 1f, 0.33f, 0.3f, 1.5f);
            Assert.That(with.y - without.y, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void Throw_GoesForwardAndUp_ScaledBySpeed()
        {
            var v = new Vector3(0f, 0f, 8f);
            Vector3 thrown = TankPloughMath.Throw(v, Slope32, 0.8f, 0.9f);
            Assert.That(thrown.y, Is.GreaterThan(0f));
            Assert.That(thrown.z, Is.GreaterThan(0f));
            Vector3 along = Vector3.ProjectOnPlane(v, Slope32).normalized;
            Assert.That(thrown, Is.EqualTo((along + Slope32 * 0.8f) * (8f * 0.9f)).Using(Vector3EqualityComparer.Instance));
        }
    }
}
```

(`UnityEngine.TestTools.Utils.Vector3EqualityComparer` — добавить `using UnityEngine.TestTools.Utils;`.)

- [ ] **Шаг 2: прогнать — FAIL (нет типа `TankPloughMath`, ошибка компиляции)**

- [ ] **Шаг 3: реализация**

```csharp
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The numbers of a tank's nose ploughing into the ground: how hard a hit is, whether it is the
    /// nose that hit, and what the hull and the earth do after it. See TankPloughBehaviour.
    /// </summary>
    public static class TankPloughMath
    {
        /// <summary>The slope faces the nose when its normal · the hull's forward is below this.</summary>
        public const float NOSE_FACING = -0.2f;

        /// <summary>0 below <paramref name="minClosing"/> m/s into the ground, 1 from <paramref name="fullClosing"/>.</summary>
        public static float Strength(float closing, float minClosing, float fullClosing)
        {
            if (closing < minClosing)
                return 0f;
            if (fullClosing <= minClosing)
                return 1f;
            return Mathf.Clamp01((closing - minClosing) / (fullClosing - minClosing));
        }

        /// <summary>
        /// The front half of the hull, below its middle, met ground that faces the nose, coming into it
        /// at least <paramref name="minClosing"/> m/s. A flat landing on the belly is the springs' business.
        /// </summary>
        /// <param name="localFromHullCentre">The hit point in the hull's space, from the hull box's centre.</param>
        public static bool IsNoseHit(Vector3 localFromHullCentre, Vector3 normal, Vector3 forward, float closing,
            float minClosing) =>
            localFromHullCentre.z > 0f && localFromHullCentre.y < 0f &&
            Vector3.Dot(normal, forward) < NOSE_FACING && closing >= minClosing;

        /// <summary>Speed after a hit: even a light one costs half the loss, a full one all of it.</summary>
        public static float SpeedAfter(float speed, float strength, float speedLoss) =>
            speed * (1f - speedLoss * Mathf.Max(strength, 0.5f));

        /// <summary>
        /// Velocity after the hit: along the slope, turned up off it by <paramref name="deflect"/> × strength,
        /// at <see cref="SpeedAfter"/>, plus <paramref name="hop"/> × strength m/s straight up.
        /// </summary>
        public static Vector3 Deflect(Vector3 velocity, Vector3 normal, float strength, float speedLoss, float deflect,
            float hop)
        {
            Vector3 along = Vector3.ProjectOnPlane(velocity, normal);
            Vector3 heading = along.sqrMagnitude > 1e-6f ? along.normalized : normal;
            heading = (heading + normal * (deflect * strength)).normalized;
            return heading * SpeedAfter(velocity.magnitude, strength, speedLoss) + Vector3.up * (hop * strength);
        }

        /// <summary>Mean velocity of the earth the nose throws: forward along the slope and up off it.</summary>
        public static Vector3 Throw(Vector3 velocity, Vector3 normal, float lift, float scale)
        {
            Vector3 along = Vector3.ProjectOnPlane(velocity, normal);
            Vector3 heading = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.zero;
            return (heading + normal * lift) * (velocity.magnitude * scale);
        }
    }
}
```

- [ ] **Шаг 4: прогнать EditMode `TankPloughMathTests` — PASS**

- [ ] **Шаг 5: коммит**

```bash
git add Assets/Game/Scripts/Features/Controllables/Tank/Drive/TankPloughMath.cs* Assets/Game/Tests/Features/Controllables/Tank/Drive/TankPloughMathTests.cs*
git commit -m "Tank: plough math - hit strength, nose hit, speed and heading after it"
```

---

### Задача 3: плуг — конфиг, выброс, поведение, подключение (тест задачи 1 зеленеет)

**Файлы:**
- Создать: `…/Tank/Drive/TankPloughConfig.cs`, `IEarthThrow.cs`, `DigEarthThrow.cs`, `TankPloughEvents.cs`, `TankPloughBehaviour.cs`
- Создать: `Assets/Game/Configs/Tank/TankPloughSubtleConfig.asset`, `TankPloughPunchyConfig.asset`, `TankPloughCinemaConfig.asset` (через редакторский скрипт `script_eval`: `ScriptableObject.CreateInstance` + `AssetDatabase.CreateAsset`, числа из таблицы ниже)
- Изменить: `…/Tank/TankInstaller.cs`, `Assets/Game/Prefabs/Vehicles/Tank_01.prefab`

**Интерфейсы — даёт:**
```csharp
public sealed class TankPloughConfig : ScriptableObject { /* public float fields below */ public string DisplayName; }
public interface IEarthThrow { int Excavate(VoxelBody ground, Vector3 point, float radius, Vector3 velocity, float spread, float volumeShare); }
public static class TankPloughEvents { public static event Action<Vector3, float> Ploughed; public static void RaisePloughed(Vector3 point, float strength); }
public sealed class TankPloughBehaviour : IEntityInit, IEntityFixedTick
{
    public TankPloughBehaviour(BoxCollider hull, TankPloughConfig config, IEarthThrow earth);
    public TankPloughConfig Config { get; set; }
    public int Impacts { get; } // impacts responded to, for tests
}
// TankInstaller
public TankPloughConfig PloughPreset { get; }
public void NextPloughPreset();
```

- [ ] **Шаг 1: `TankPloughConfig`**

```csharp
using UnityEngine;

namespace Game
{
    /// <summary>How hard a tank's nose ploughs into the ground it hits (TankPloughBehaviour). One asset per preset.</summary>
    [CreateAssetMenu(menuName = "Game/Tank/Plough Config", fileName = "TankPloughConfig")]
    public sealed class TankPloughConfig : ScriptableObject
    {
        [Tooltip("Shown in the sandbox HUD.")]
        public string DisplayName = "Punchy";

        [Header("Hit")]
        [Tooltip("Slower than this (m/s) the hull does not look ahead at all.")]
        public float MinSpeed = 3f;
        [Tooltip("Speed (m/s) into the ground below which a hit is no hit.")]
        public float MinClosing = 2f;
        [Tooltip("Speed (m/s) into the ground at which a hit is as hard as it gets.")]
        public float FullClosing = 8f;
        [Tooltip("Seconds of travel the hull looks ahead: the ground's collider is rebuilt by the time it arrives.")]
        public float LookAhead = 0.1f;

        [Header("Bite")]
        [Tooltip("Radius (m) of one of the three bites across the nose, at the lightest hit.")]
        public float BiteRadiusMin = 0.6f;
        [Tooltip("…and at the hardest.")]
        public float BiteRadiusMax = 1.1f;
        [Tooltip("Seconds between bites while the nose keeps ploughing.")]
        public float BiteInterval = 0.05f;
        [Tooltip("Earth thrown off the slope, along it, per m/s of the hull: 1 throws it as fast as the hull goes.")]
        public float ThrowScale = 0.9f;
        [Tooltip("How much up off the slope the earth goes: 0 skims along it, 1 at 45°.")]
        public float ThrowLift = 0.8f;
        [Tooltip("Share of the earth dug out that flies; the rest is pressed down.")]
        public float ThrowVolume = 0.8f;
        [Tooltip("Spread of the throw at the lightest hit: 0 one tight clump, 1 a wide fan.")]
        public float SpreadMin = 0.2f;
        [Tooltip("…and at the hardest.")]
        public float SpreadMax = 0.7f;
        [Tooltip("Most debris cubes one bite may throw while the earth goes out through Dig.")]
        public int BiteDebris = 160;

        [Header("Response")]
        [Tooltip("Share of the speed the hardest hit costs; a light one costs half of it.")]
        public float SpeedLoss = 0.33f;
        [Tooltip("How far the velocity is turned up off the slope at the hardest hit.")]
        public float Deflect = 0.3f;
        [Tooltip("m/s straight up the hardest hit adds: the jump out of the crater.")]
        public float Hop = 1.2f;
        [Tooltip("rad/s nose-up the hardest hit adds.")]
        public float PitchKick = 0.8f;
        [Tooltip("Seconds after a hit before the next can turn the hull again.")]
        public float ImpactCooldown = 0.3f;

        [Header("Feel")]
        public float ShakeStrength = 0.6f;
        public float ShakeDuration = 0.35f;
        [Tooltip("Radius (m) of the dust puff at the hardest hit; 0 — none.")]
        public float DustRadius = 1.5f;
    }
}
```

Пресеты (ассеты):

| Поле | Subtle «Сдержанно» | Punchy «Эффектно» | Cinema «Кино» |
|---|---|---|---|
| `DisplayName` | Subtle | Punchy | Cinema |
| `BiteRadiusMin`/`Max` | 0.45 / 0.8 | 0.6 / 1.1 | 0.8 / 1.4 |
| `ThrowScale` | 0.6 | 0.9 | 1.3 |
| `ThrowVolume` | 0.5 | 0.8 | 1.0 |
| `SpreadMin`/`Max` | 0.1 / 0.4 | 0.2 / 0.7 | 0.3 / 1.0 |
| `BiteDebris` | 80 | 160 | 300 |
| `SpeedLoss` | 0.4 | 0.33 | 0.25 |
| `Hop` / `PitchKick` | 0.5 / 0.4 | 1.2 / 0.8 | 2.2 / 1.3 |
| `ShakeStrength` / `ShakeDuration` | 0.3 / 0.25 | 0.6 / 0.35 | 1.0 / 0.5 |
| `DustRadius` | 0 | 1.5 | 2.5 |

Остальные поля — значения по умолчанию.

- [ ] **Шаг 2: `IEarthThrow`, `DigEarthThrow`, `TankPloughEvents`**

```csharp
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>Digs earth out of the ground and throws it: the tank's nose ploughing (TankPloughBehaviour).</summary>
    public interface IEarthThrow
    {
        /// <param name="velocity">Mean velocity of the thrown earth.</param>
        /// <param name="spread">0 one tight clump .. 1 a wide fan of clumps and crumbs.</param>
        /// <param name="volumeShare">Share of what is dug out that flies.</param>
        /// <returns>Voxels dug out.</returns>
        int Excavate(VoxelBody ground, Vector3 point, float radius, Vector3 velocity, float spread, float volumeShare);
    }
}
```

```csharp
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// For now: VoxelBody.Dig takes the earth out and throws it itself. Goes once the earth team's
    /// VoxelDebris.ThrowEarth is in game/develop (DebrisEarthThrow).
    /// </summary>
    public sealed class DigEarthThrow : IEarthThrow
    {
        private readonly int _debris;

        public DigEarthThrow(int debrisPerBite) => _debris = debrisPerBite;

        public int Excavate(VoxelBody ground, Vector3 point, float radius, Vector3 velocity, float spread,
            float volumeShare) =>
            ground.Dig(point, radius, velocity, Mathf.RoundToInt(_debris * volumeShare));
    }
}
```

```csharp
using System;
using UnityEngine;

namespace Game
{
    /// <summary>A tank's nose ploughed into the ground: point, strength 0..1. For sound.</summary>
    public static class TankPloughEvents
    {
        public static event Action<Vector3, float> Ploughed;

        public static void RaisePloughed(Vector3 point, float strength) => Ploughed?.Invoke(point, strength);
    }
}
```

- [ ] **Шаг 3: `TankPloughBehaviour`**

```csharp
using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The tank's nose ploughs into ground it hits at speed instead of stopping dead against it.
    ///
    /// Every physics step it sweeps the hull's box a moment ahead along its flight. Ground the
    /// nose is about to hit hard enough (TankPloughMath.IsNoseHit) is dug out before it gets there
    /// — three bites across the nose, thrown forward and up — and the hull is given the velocity it
    /// leaves the hit with: a share of its speed gone, turned up along the slope, nose up. Out of a
    /// crater it comes over the far rim in a short jump rather than crawling out.
    ///
    /// A hit the sweep missed (the collider was rebuilt late, a thin ledge) shows as a step that
    /// lost much of the speed: it is answered after the fact, from the speed before it.
    /// </summary>
    public sealed class TankPloughBehaviour : IEntityInit, IEntityFixedTick
    {
        private const int GROUND_MASK = 1 << 3;
        private const float SHRINK = 0.02f;
        private const float SKIN = 0.05f;
        private const float MISSED_LOSS = 0.4f;
        private const float MISSED_REACH = 0.4f;
        private const float BITE_ACROSS = 0.6f;

        private static readonly ProfilerMarker BiteMarker = new("TankPlough.Bite");
        private static readonly RaycastHit[] s_hits = new RaycastHit[8];

        private readonly BoxCollider _hull;
        private readonly IEarthThrow _earth;
        private Rigidbody _body;
        private IValue<IEntity> _driver;

        private float _time;
        private float _nextBite;
        private float _nextResponse;
        private Vector3 _lastVelocity;
        private bool _hasLast;
        private CameraShakeArgs _shake;

        public TankPloughConfig Config { get; set; }

        /// <summary>Hits the hull was turned by. For tests.</summary>
        public int Impacts { get; private set; }

        public TankPloughBehaviour(BoxCollider hull, TankPloughConfig config, IEarthThrow earth)
        {
            _hull = hull;
            Config = config;
            _earth = earth;
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _driver = entity.TryGetDriver(out IReactiveVariable<IEntity> driver) ? driver : null;
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            _time += deltaTime;
            TankPloughConfig config = Config;
            if (config == null || _hull == null || _body.isKinematic)
                return;

            Vector3 velocity = _body.linearVelocity;
            float speed = velocity.magnitude;

            // Missed: the solver stopped the hull against ground the sweep did not see.
            if (_hasLast && _lastVelocity.magnitude >= config.MinSpeed && speed < _lastVelocity.magnitude * (1f - MISSED_LOSS) &&
                Sweep(_lastVelocity, MISSED_REACH, out RaycastHit missed))
                Hit(missed, _lastVelocity, config);
            else if (speed >= config.MinSpeed && Sweep(velocity, speed * config.LookAhead + SKIN, out RaycastHit ahead))
                Hit(ahead, velocity, config);

            _lastVelocity = _body.linearVelocity;
            _hasLast = true;
        }

        /// <summary>The nearest ground the hull's box meets along <paramref name="velocity"/> within <paramref name="reach"/> m.</summary>
        private bool Sweep(Vector3 velocity, float reach, out RaycastHit nearest)
        {
            nearest = default;
            float speed = velocity.magnitude;
            if (speed < 1e-3f)
                return false;

            Transform hull = _hull.transform;
            Vector3 centre = hull.TransformPoint(_hull.center);
            Vector3 half = Vector3.Scale(_hull.size, hull.lossyScale) * 0.5f - Vector3.one * SHRINK;
            int count = Physics.BoxCastNonAlloc(centre, half, velocity / speed, s_hits, hull.rotation, reach, GROUND_MASK,
                QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hits[i];
                if (hit.distance <= 0f && hit.point == Vector3.zero)
                    continue; // already overlapping: no normal to go by
                if (hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                }
            }

            return best < float.MaxValue;
        }

        private void Hit(RaycastHit hit, Vector3 velocity, TankPloughConfig config)
        {
            Transform hull = _hull.transform;
            Vector3 normal = hit.normal;
            float closing = -Vector3.Dot(velocity, normal);
            Vector3 local = hull.InverseTransformPoint(hit.point) - _hull.center;
            if (!TankPloughMath.IsNoseHit(local, normal, hull.forward, closing, config.MinClosing))
                return;

            VoxelBody ground = FindGround(hit.point);
            if (ground == null)
                return;

            float strength = TankPloughMath.Strength(closing, config.MinClosing, config.FullClosing);

            if (_time >= _nextBite)
            {
                _nextBite = _time + config.BiteInterval;
                Bite(ground, hit.point, velocity, normal, strength, config);
            }

            if (_time < _nextResponse)
                return;

            _nextResponse = _time + config.ImpactCooldown;
            Impacts++;
            _body.linearVelocity = TankPloughMath.Deflect(velocity, normal, strength, config.SpeedLoss, config.Deflect,
                config.Hop);
            _body.angularVelocity += -hull.right * (config.PitchKick * strength); // negative about right = nose up

            if (config.DustRadius > 0f)
                VoxelSmoke.Blast(hit.point, config.DustRadius * strength, 0.6f);
            if (_driver != null && _driver.Value != null && config.ShakeStrength > 0f)
                CameraShakeUseCase.ShakePlayerCamera(new CameraShakeArgs(config.ShakeDuration, config.ShakeStrength * strength));
            TankPloughEvents.RaisePloughed(hit.point, strength);
        }

        private void Bite(VoxelBody ground, Vector3 point, Vector3 velocity, Vector3 normal, float strength,
            TankPloughConfig config)
        {
            using (BiteMarker.Auto())
            {
                float radius = Mathf.Lerp(config.BiteRadiusMin, config.BiteRadiusMax, strength);
                Vector3 heading = velocity.normalized;
                Vector3 centre = point + heading * (radius * 0.4f);
                Vector3 across = _hull.transform.right * (Vector3.Scale(_hull.size, _hull.transform.lossyScale).x * 0.5f * BITE_ACROSS);
                Vector3 thrown = TankPloughMath.Throw(velocity, normal, config.ThrowLift, config.ThrowScale);
                float spread = Mathf.Lerp(config.SpreadMin, config.SpreadMax, strength);

                _earth.Excavate(ground, centre, radius, thrown, spread, config.ThrowVolume);
                _earth.Excavate(ground, centre + across, radius * 0.85f, thrown, spread, config.ThrowVolume);
                _earth.Excavate(ground, centre - across, radius * 0.85f, thrown, spread, config.ThrowVolume);
            }
        }

        private static VoxelBody FindGround(Vector3 point)
        {
            var bodies = VoxelWorld.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                VoxelBody body = bodies[i];
                if (body != null && body.IsTerrain && body.IsCreated && body.WorldBounds.SqrDistance(point) < 0.01f)
                    return body;
            }

            return null;
        }
    }
}
```

Знак `PitchKick`: в Unity поворот вокруг `+right` опускает нос (ось X, правая рука → вниз в левосторонней системе). Исполнитель проверяет знак в Play: нос после удара должен идти вверх. Если идёт вниз, поменять на `+hull.right`.

`CameraShakeArgs(float duration, float strength)` — уже есть, им пользуется `VoxelAirStrike`. `new` один раз на удар, не каждый кадр: это кадр события (≤ 4 КБ).

- [ ] **Шаг 4: подключение в `TankInstaller`**

В раздел `[Header("Body")]` после `_rigidbody`:
```csharp
        [Tooltip("The hull's box (not the turret's): swept ahead for ground the nose is about to plough into.")]
        [SerializeField] private BoxCollider _hullCollider;
```
Новый раздел перед `[Header("Crushing")]`:
```csharp
        [Header("Plough")]
        [Tooltip("How hard the nose ploughs into ground it hits at speed; the sandbox switches between them (M).")]
        [SerializeField] private TankPloughConfig[] _ploughPresets;
        [SerializeField, Min(0)] private int _ploughPreset = 1;

        private TankPloughBehaviour _plough;

        /// <summary>The plough preset in use, or null without one.</summary>
        public TankPloughConfig PloughPreset =>
            _ploughPresets != null && _ploughPresets.Length > 0 ? _ploughPresets[_ploughPreset % _ploughPresets.Length] : null;

        /// <summary>The next plough preset, round and round. For the sandbox.</summary>
        public void NextPloughPreset()
        {
            if (_ploughPresets == null || _ploughPresets.Length == 0)
                return;
            _ploughPreset = (_ploughPreset + 1) % _ploughPresets.Length;
            if (_plough != null)
                _plough.Config = PloughPreset;
        }
```
В `Install` сразу после `TankDriveBehaviour`:
```csharp
            // After the drive: the nose ploughs into ground it is about to hit, and the hull's speed
            // after the hit is set here, over the drive's push.
            if (_hullCollider != null && PloughPreset != null)
            {
                _plough = new TankPloughBehaviour(_hullCollider, PloughPreset, new DigEarthThrow(PloughPreset.BiteDebris));
                entity.AddBehaviour(_plough);
            }
```

- [ ] **Шаг 5: ассеты пресетов и префаб**

`script_eval` в своём редакторе: создать папку `Assets/Game/Configs/Tank`, три ассета с числами из таблицы. В `Tank_01.prefab` (через `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`): `_hullCollider` — `BoxCollider` корня размером 2.025 × 0.39 × 2.775 (центр y 0.645); `_ploughPresets` — [Subtle, Punchy, Cinema]; `_ploughPreset` = 1.

- [ ] **Шаг 6: EditMode целиком и PlayMode `SandboxCraterPloughTest` — PASS**

Если тест не проходит, подбирать в пресете Punchy только `LookAhead` (до 0.15), `Deflect`, `Hop` и `SpeedLoss`. Порог теста не трогать. Цифры `[CraterPlough]` до и после записать.

- [ ] **Шаг 7: дописать в тест проверки плуга и прогнать**

Перед въездом: `int before = ground.SolidCount` (тело земли под кратером, `FindGround` как в поведении, через `VoxelWorld.Bodies`), подписка на `TankPloughEvents.Ploughed` со счётчиком. После выезда:
```csharp
Assert.That(ploughed, Is.GreaterThan(0), "the nose ploughed");
Assert.That(farGround.SolidCount, Is.LessThan(before), "earth dug out of the far slope");
```

- [ ] **Шаг 8: коммит**

```bash
git add Assets/Game/Scripts/Features/Controllables/Tank/Drive/TankPlough*.cs* Assets/Game/Scripts/Features/Controllables/Tank/Drive/IEarthThrow.cs* Assets/Game/Scripts/Features/Controllables/Tank/Drive/DigEarthThrow.cs* Assets/Game/Scripts/Features/Controllables/Tank/TankInstaller.cs Assets/Game/Configs/Tank.meta Assets/Game/Configs/Tank Assets/Game/Prefabs/Vehicles/Tank_01.prefab Assets/Game/Voxels/Tests/Scene/SandboxCraterPloughTest.cs
git commit -m "Tank: nose ploughs into ground it hits at speed and breaks out of craters"
```

---

### Задача 4: переключатель пресетов в песочнице (M + HUD)

**Файлы:**
- Изменить: `Assets/Game/Configs/Input/GameControls.inputactions` — в карту `Sandbox` действие `{ "name": "Plough", "type": "Button", "id": "6a1d0000-0000-4000-8000-000000000304", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false }` и привязка `{ "name": "", "id": "6a1d0000-0000-4000-8000-000000003004", "path": "<Keyboard>/m", "interactions": "", "processors": "", "groups": "", "action": "Plough", "isComposite": false, "isPartOfComposite": false }`. Импорт в редакторе перегенерирует `Assets/Game/Scripts/Shared/Input/GameControls.cs`; он коммитится вместе.
- Создать: `Assets/Game/Scripts/Features/Voxels/VoxelSandboxPloughSwitch.cs`; добавить его в сцену `VoxelSandbox.unity` на объект с `VoxelAirStrike`, `_tank` — `TankInstaller` танка.

- [ ] **Шаг 1: компонент**

```csharp
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>VoxelSandbox: the Sandbox/Plough key (M) switches the tank's plough preset; the HUD shows which.</summary>
    public sealed class VoxelSandboxPloughSwitch : MonoBehaviour
    {
        [SerializeField] private TankInstaller _tank;

        private System.Func<string> _hudLine;
        private string _hudText;
        private TankPloughConfig _shown;

        private void OnEnable()
        {
            _hudLine ??= HudLine;
            VoxelHudLines.Add(_hudLine);
        }

        private void OnDisable() => VoxelHudLines.Remove(_hudLine);

        private void Update() // perf-ok: the sandbox key reads input every frame; one flag check
        {
            if (_tank != null && AppServices.Input.Controls.Sandbox.Plough.WasPressedThisFrame())
                _tank.NextPloughPreset();
        }

        // Rebuilt only when the preset changes: no allocation otherwise.
        private string HudLine()
        {
            TankPloughConfig preset = _tank != null ? _tank.PloughPreset : null;
            if (_hudText != null && preset == _shown)
                return _hudText;
            _shown = preset;
            string key = AppServices.Input.Controls.Sandbox.Plough.GetBindingDisplayString();
            _hudText = $"{key} — tank plough preset (now {(preset != null ? preset.DisplayName : "none")})";
            return _hudText;
        }
    }
}
```

- [ ] **Шаг 2: проверка в Play** — нажатие M (MCP `runtime_simulate_key`) меняет строку HUD Subtle → Punchy → Cinema → Subtle, `TankPloughBehaviour.Config` меняется. Сцена сохраняется только своя правка (добавленный компонент).

- [ ] **Шаг 3: коммит**

```bash
git add Assets/Game/Configs/Input/GameControls.inputactions Assets/Game/Scripts/Shared/Input/GameControls.cs Assets/Game/Scripts/Features/Voxels/VoxelSandboxPloughSwitch.cs* Assets/Game/Voxels/Scenes/VoxelSandbox.unity
git commit -m "Sandbox: M switches the tank plough preset, shown in the HUD"
```

---

### Задача 5: прототип пользователю

- [ ] Тест задачи 1 пишет кадры в `Logs/CraterPlough`. Прогнать его по разу с каждым пресетом (через `_ploughPreset` в префабе на время прогона или через `NextPloughPreset` в тесте), собрать по 4–6 кадров каждого в лист (`Tools`-скрипт не нужен: PowerShell + System.Drawing в scratchpad). Отправить `SendUserFile`, подписав, какой пресет где.
- [ ] Записать цифры (скорость входа, минимум, время выезда) по каждому пресету в отчёт.

### Задача 6: выброс через `ThrowEarth` (после вливания `game/debris` в `game/develop`)

- [ ] Влить `game/develop` в ветку, когда в нём появится `VoxelDebris.ThrowEarth(in EarthThrow)`.
- [ ] Создать `DebrisEarthThrow : IEarthThrow`: цвета `Top` — `ground.TryGetColor(point + normal*0.1f)`, `Body` — `ground.TryGetColor(point - Vector3.up * radius)`; `int dug = ground.Dig(point, radius, Vector3.zero, 0)`; `Volume = dug * voxelSize³ * volumeShare`; `VoxelWorld.Instance.Debris.ThrowEarth(new EarthThrow { Point = point, Velocity = velocity, Volume = …, Spread = spread, Top = …, Body = … })`.
- [ ] `TankInstaller` переходит на `DebrisEarthThrow`; `DigEarthThrow` удаляется. Тест задачи 3 — PASS.

### Задача 7: ревью и производительность

- [ ] `superpowers:requesting-code-review` по диффу ветки.
- [ ] `/perf-check` на ветке: idle, drive, airstrike, crush не хуже `game/develop`; плюс ручной сценарий въезда в воронку (как в тесте) с профайлером: `TankPlough.Bite` ≤ 0.5 мс, кадр удара ≤ 16 мс, GC в езде 0.
- [ ] Отчёт тимлиду: что сделано, цифры до/после, выбранный пресет, предложение сценария **crater** для `Tools/PerfCheck/Bench/`.

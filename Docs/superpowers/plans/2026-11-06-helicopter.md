# Вертолёт Heli_01 — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** вертолёт на Playground: E — сел и взлетел на 5 м, WASD — полёт с инерцией и наклонами,
нос за курсором, ЛКМ — пулемёт с гильзами, E в воздухе — сел и вышел, сбитый падает.

**Architecture:** вселение, как у танка (`VehicleUseCase`, `PlayerContext.Character`). Полёт —
`HelicopterFlightBehaviour` (состояния Landed / TakingOff / Flying / Landing, скорость пишется в
тело, инерция — ограниченный разгон и торможение). Курс — `HelicopterYawBehaviour`. Наклоны —
визуальные, `HelicopterTiltBehaviour` на пружинах. Пулемёт наводится на точку курсора на земле
(`AimHeight` / `AimPoint` в `CharacterRotateController`). Префаб и сцену собирает `HelicopterBuilder`.

**Tech Stack:** Unity 6000.0.38f1, Atomic Entities (`EntityAPIGenerator`), legacy `Input`, NUnit EditMode.

Спек: `Docs/superpowers/specs/2026-11-06-helicopter-design.md`.

## Global Constraints
- Высота 5 м, скорость 10 м/с, разгон 7 м/с², торможение 5 м/с², вертикаль ≤ 3 м/с,
  разгон вертикали 6 м/с², курс ≤ 120°/с, раскрутка курса 360°/с², наклон ≤ 15°, HP 250, масса 1500.
- Пулемёт: темп 0.24 с, урон 10, разброс 3°, без патронов; пуля 60 м/с; гильзы — от AR_01.
- `WeaponID.HeliGun` — в конец enum.
- Все коллайдеры — на корне (слой Character, 9). У винтов коллайдеров нет.
- Пивот пулемёта — ребёнок корня, а не наклоняемого `BodyVisual`.
- Код не коммитится, коммитятся только документы в `Docs/`.
- Перекомпиляция — только вне Play: редактор общий для нескольких сессий. `autoRefresh` выключен:
  после записи файлов — `AssetDatabase.Refresh()`.

## Файлы
| Файл | Что |
|---|---|
| `Scripts/Declarations/EntityAPI.cs` | `AimHeight`, `AimPoint`, `FlightState` |
| `Scripts/Features/Weapons/Model/WeaponID.cs` | `HeliGun` |
| `Scripts/Features/Helicopter/Model/HelicopterFlightState.cs` | состояния (новый) |
| `Scripts/Features/Helicopter/Model/HelicopterFlightMath.cs` | `ApproachPlanar`, `ClimbSpeed` (новый) |
| `Scripts/Features/Helicopter/Model/TouchdownDetector.cs` | касание (новый) |
| `Scripts/Features/Helicopter/Model/HelicopterFlightBehaviour.cs` | полёт, взлёт, посадка (новый) |
| `Scripts/Features/Helicopter/Model/HelicopterYawBehaviour.cs` | нос за курсором (новый) |
| `Scripts/Features/Helicopter/Model/HelicopterGunAimBehaviour.cs` | пулемёт к точке курсора (новый) |
| `Scripts/Features/Helicopter/View/HelicopterTiltMath.cs`, `HelicopterTiltBehaviour.cs` | наклоны (новые) |
| `Scripts/Features/Helicopter/View/RotorSpinBehaviour.cs` | винты (новый) |
| `Scripts/Entities/Vehicle/HelicopterInstaller.cs` | сущность (новый) |
| `Scripts/Features/Movement/Input/CharacterRotateController.cs` | `AimHeight` / `AimPoint` |
| `Scripts/Features/Tank/Model/TankDestroyBehaviour.cs` | `fallOnDeath` |
| `Scripts/Editor/TankBuilder.cs` | хелперы → `internal` |
| `Scripts/Editor/HelicopterBuilder.cs` | материалы, пуля, префаб, Playground (новый) |
| `Tests/HelicopterMathTests.cs` | тесты (новый) |

## Task 1: Ключи, математика, тесты
- [x] Ключи в `EntityAPI`, `HeliGun` в `WeaponID`.
- [x] `HelicopterFlightMath`:
  - `ApproachPlanar(current, target, acceleration, braking, dt)` — `MoveTowards` со скоростью
    acceleration, если `|target| ≥ |current|`, иначе braking; Y обнуляется.
  - `ClimbSpeed(y, targetY, gain, max)` = `clamp((targetY − y) × gain, −max, max)`.
- [x] `TouchdownDetector.Step(height, verticalSpeed, dt)`: true при `height ≤ 0.05` или при
  `|vy| < 0.1` в течение 0.3 с подряд.
- [x] `HelicopterTiltMath.TargetAngles(localAccel, localVel, yawRate, k_acc, k_vel, k_yaw, max)`:
  pitch = az·k_acc + vz·k_vel, roll = −(ax·k_acc + vx·k_vel) − yawRate·k_yaw, оба в ±max.
  `Spring(ref angle, ref vel, target, f, ζ, dt)` — полунеявная пружина с демпфером.
- [x] Тесты `HelicopterMathTests` (14):
  - разгон — 0.7 за 0.1 с, торможение — 9.5 с 10, вертикаль игнорируется;
  - climb — ограничение и знак;
  - касание — у земли, остановка 0.3 с, ещё снижается, остановка прервана;
  - наклоны — нос вниз и вверх, крен в сторону движения и в сторону поворота, ограничение;
  - пружина затухает.

## Task 2: Поведения и инсталлер
- [x] `HelicopterFlightBehaviour`, состояния по кругу:
  - Landed: гравитация, удержание по XZ. → TakingOff — новый пилот или ввод пилота, оставшегося
    внутри.
  - TakingOff → Flying — высота ±0.1 м.
  - Flying → Landing — `RequestLanding`.
  - Landing → Landed — касание, затем `onTouchdown` (`VehicleUseCase.TryExit`).
  - Скорость, которой тело не набирает, сбрасывается.
- [x] `HelicopterYawBehaviour`: желаемая угловая скорость = `DeltaAngle × 4`, ≤ `RotationSpeed`,
  раскрутка 360°/с², `MoveRotation`. Обнуляет `angularVelocity`.
- [x] `HelicopterGunAimBehaviour`: `TurretPivot` к `AimPoint`, рыскание ±30°, тангаж 0..70° вниз,
  180°/с; `TurretVisual.localRotation` = пивота.
- [x] `HelicopterTiltBehaviour`: ускорение в FixedTick, пружины 2 Гц / 0.5 в Tick, `dt ≤ 1/30`.
- [x] `RotorSpinBehaviour`: 900°/с и 2000°/с, раскрутка 1.5 с, остановка 3 с.
- [x] `HelicopterInstaller`: ключи как у танка + `AimHeight`, `AimPoint`, `FlightState`;
  `VehicleExitAction`: в воздухе — `RequestLanding`, на земле — `TryExit`.

## Task 3: Изменения существующего кода
- [x] `CharacterRotateController`: плоскость на `AimHeight`, точка в `AimPoint`, если ключи есть.
- [x] `TankDestroyBehaviour(…, fallOnDeath)`: `Drop()` — динамическое тело, гравитация, без
  ограничений вращения, закрутка ≤ 2 рад/с.

## Task 4: Билдер, префаб, сцена
- [x] `HelicopterBuilder` (`Tools/Helicopter/Build`):
  - материалы `MT_Heli_Body`, `MT_Heli_Glass`;
  - `Bullet_Heli` — копия `Bullet_01_AR`, `_moveSpeed` 60;
  - `Heli_01.prefab` по дереву из спеки;
  - в Playground на (3.2, 0, −6).

## Task 5: Проверка
- [x] Refresh → компиляция без ошибок → EditMode 107/107.
- [x] Build → префабы и сцена.
- [x] Play: взлёт, полёт, стрельба и гильзы, посадка с выходом, повторная посадка, уничтожение
  в воздухе (см. «Результат» в спеке).

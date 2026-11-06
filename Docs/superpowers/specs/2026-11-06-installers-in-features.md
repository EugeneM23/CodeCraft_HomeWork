# Инсталлеры в папках фич, техника → Controllables

**Дата:** 2026-11-06
**Статус:** утверждено (вариант 1 из трёх предложенных)
**Продолжает:** [2026-11-06-feature-folders-by-object.md](2026-11-06-feature-folders-by-object.md).
Отменяет правило 2 из [2026-11-04-scripts-by-feature.md](2026-11-04-scripts-by-feature.md)
(«инсталлеры — не члены фичи, живут в `Entities/`»).

## Задача

Открыл папку объекта — видишь, из чего он собран.

## Что было не так

- **Состав объекта лежал далеко от объекта.** `Features/Vehicles/Mount/` — один файл
  `MountDeathBehaviour`, а то, что маунт собран из `VehicleUseCase`, `DamageCastBehaviour`,
  `DeathBehaviour`, `PassThroughLoosePiecesBehaviour` и контроллеров движения, написано в
  `Entities/Vehicle/MountInstaller.cs`.
- **`Model/` и `View/` у танка режут по слоям, а не по смыслу.** 9 файлов в `Model/`, 4 во `View/` —
  не видно, что относится к подвеске, а что к пушке.
- **Правило уже не соблюдалось:** `AbilitySystemInstaller`, `InteractInstaller`, `CameraInstaller`
  жили внутри фич.
- **`Vehicles/` — неверное имя.** Regax — существо, не машина. Общее у танка, вертолёта и Regax одно:
  в них садятся и ими управляют.

## Правила

1. **Инсталлер лежит в корне папки объекта, который он собирает.**
2. **Внутри объекта код делится на подпапки по частям** (`Suspension/`, `Gun/`), если части
   различимы. Если нет — остаются `Model/` `View/` `Input/` или плоская папка. В этой правке по частям
   разложены только `Controllables/` и `Enemies/`; у остальных фич слои остаются.
3. **Общий код группы объектов — в её `Common/`.**
4. **`Entities/` больше нет.**

## Раскладка

### `Features/Controllables/` (было `Features/Vehicles/` + `Entities/Vehicle/`)

```
Controllables/
├── Common/        VehicleUseCase, VehicleDriveController, VehicleFireController,
│                  VehicleHighlight, TankDestroyBehaviour
├── Tank/          TankInstaller
│   ├── Drive/         TankDriveBehaviour, TankDriveMath, TankTrackView, TankRamBehaviour
│   ├── Suspension/    TankSuspension, TankSuspensionBehaviour, SuspensionMath
│   └── Gun/           TurretAimBehaviour, GunRecoilBehaviour, TankRecoilBehaviour,
│                      TankMuzzleSmokeBehaviour, TankShockwaveBehaviour, ShellInstaller
├── Helicopter/    HelicopterInstaller, HelicopterGunAimBehaviour, HelicopterCameraPointBehaviour
│   ├── Flight/        HelicopterFlightBehaviour, HelicopterFlightMath, HelicopterFlightState,
│   │                  HelicopterYawBehaviour, TouchdownDetector
│   └── Body/          HelicopterTiltBehaviour, HelicopterTiltMath, RotorSpinBehaviour
└── Mount/         MountInstaller, MountDeathBehaviour
```

- `ShellInstaller` — снаряд, который есть только у танка, поэтому `Tank/Gun/`, а не `Bullets/`.
- `TankDestroyBehaviour` ставит и вертолёт — поэтому `Common/`.
- `TankRamBehaviour` — танк ломает препятствия ходом, поэтому `Drive/`.

### Остальные инсталлеры из `Entities/`

| Откуда | Куда |
|---|---|
| `Entities/Character/` (7: Anim, Audio, Buff, Core, Flash, Grenade, Vfx) | `Features/Character/` (новая) |
| `Entities/Enemy/` (4) | `Features/Enemies/` (корень) |
| `Entities/Weapon/` (Melee, Range, WeaponVisual) | `Features/Weapons/` (корень) |
| `Entities/Projectile/` Bullet, ProjectileCore, ProjectileVisual, Hit | `Features/Bullets/` |
| `Entities/Projectile/` Explosion | `Features/Explosions/` |
| `Entities/Projectile/` Grenade, Flash | `Features/Grenades/` (корень) |
| `Entities/Projectile/` Shell | `Features/Controllables/Tank/Gun/` |
| `Entities/PickUp/` (8) | `Features/PickUps/` (новая) |
| `Entities/Common/` EffectAreaBehaviour, EffectAreaInstaller | `Shared/Aspects/` — область, которая накладывает аспект |
| `Entities/Common/` AudioSourceInstaller | `Shared/View/` |
| `Entities/Vehicle/` (4) | `Features/Controllables/` (см. выше) |

### `Features/Enemies/`

`Model/` → `AI/` (патруль, погоня, атака, цель, направление), `View/` → `Animation/`
(анимации атаки и получения урона). Инсталлеры — в корне.

- `Character/` — только инсталлеры: персонаж собран из `Movement`, `Health`, `Weapons`, `Buffs`,
  `Grenades`, `Interaction`, своего кода у него нет. `CharacterFlashInstaller` и
  `CharacterGrenadeInstaller` тоже здесь: они собирают персонажа, а не гранату.
- `GrenadePickUpInstaller` — в `PickUps/`: это пикап.
- `HitInstaller` (эффект попадания) — в `Bullets/`, рядом с `BulletHitUseCase`.

## Шаг 2: переименование классов

Отдельно от переносов, чтобы каждый шаг проверялся сам по себе.

| Было | Стало |
|---|---|
| `VehicleUseCase` | `ControllableUseCase` |
| `VehicleDriveController` | `ControllableDriveController` |
| `VehicleFireController` | `ControllableFireController` |
| `VehicleHighlight` | `ControllableHighlight` |

Файлы переименовываются через `AssetDatabase.RenameAsset` — GUID сохраняется. В префабах и сценах
эти имена не встречаются (проверено grep'ом): все четыре — не MonoBehaviour.

### Чего не делаем

- **Ключи `EntityAPI` не трогаем:** тег `Vehicle` и `VehicleExitAction` — это генерируемый API
  (`AddVehicleTag`, `GetVehicleExitAction`...), переименование тянет кодген и все места использования.
- **`TankDestroyBehaviour` не переименовываем**, хотя он общий.
- **`Editor/Vehicles/` не трогаем** — это сборщики префабов, не рантайм.
- **Старые спеки, планы и `ScriptLayoutMigration.cs` не правим.**

## Риски и порядок

- Переносы — `AssetDatabase.MoveAsset` в открытом редакторе, `.meta` едет за файлом.
- Только когда редактор не в Play: редактор общий, перекомпиляция в Play стирает состояние
  сущностей и ломает RayFire.
- Неймспейс у всех файлов `Game`, строковых путей `Entities/`/`Features/` в коде нет.

Порядок: создать папки → `MoveAsset` → удалить пустые папки → `Refresh` → компиляция → проверка →
переименование классов → компиляция → проверка.

## Как проверяем

- Компиляция без ошибок.
- Набор GUID всех `*.cs.meta` в `Assets/Game/Scripts` до и после совпадает.
- Префабы и `Playground.unity` без новых «missing script».
- Нет пустых папок и папок без `.meta`; `Entities/` и `Features/Vehicles/` исчезли.

## Результат

Прогон: 64 переноса, 0 ошибок; папка `Vehicles/` переименована в `Controllables/`, `Enemies/Model` → `AI`,
`Enemies/View` → `Animation` (у папок GUID сохранился); `Entities/` удалена. Четыре класса
переименованы вместе с файлами, плюс тест `VehicleUseCaseTests` → `ControllableUseCaseTests`
(его в списке выше не было). GUID всех 232 скриптов совпали с состоянием до переноса. Компиляция
чистая. В 97 префабах и сценах `Assets/Game` неразрешённые ссылки на скрипты есть только в
`GameScene.unity` и `Playground.unity`, и ни один из этих GUID не принадлежит нашим скриптам —
это компоненты Cinemachine, пакета которого в проекте нет.

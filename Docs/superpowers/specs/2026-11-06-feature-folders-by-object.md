# Папки фич по игровым объектам

**Дата:** 2026-11-06
**Статус:** утверждено (вариант 1 из трёх предложенных)
**Продолжает:** [2026-11-04-scripts-by-feature.md](2026-11-04-scripts-by-feature.md). Правила оттуда
(слои `Model/` `View/` `Input/`, инсталлеры в `Entities/`, один неймспейс `Game`) не меняются,
меняются только имена и состав папок в `Features/`.

## Задача

Назвать папки в `Features/` по тому, что в игре видно глазами: танк, пули, гранаты. Открыл папку —
видишь вещь целиком.

## Что было не так

За два дня в проект пришли танк, гранаты, флешка, рикошет и разрушаемые стены, и папки,
названные по механике, перестали говорить, что в них лежит.

| Папка | Проблема |
|---|---|
| `Combat/` | 25 файлов: оружие, пули с рикошетом, 7 файлов гранат, флешка, взрыв на 338 строк, звуки. По имени не понять, где граната |
| `Vehicles/` | Машина в игре одна: 10 из 16 файлов называются `Tank*`, ещё два — `TurretAim` и `GunRecoil` — его башня и пушка |
| `Vitals/` | Абстрактное имя для здоровья, урона и смерти; туда же попал `FlashScreenBehaviour` — белый экран от флешки |
| `EnemyAI/` | В `View/` лежат анимации атаки и получения урона, к AI они не относятся |
| `Destruction/` | Разрушаются только стены: `WallLoadSolver`, `WallSeams`, 51 упоминание wall на 9 файлов |
| `Combat/Model/CooldownTimer.cs` | Пользуются шесть мест: способности, баффы, спавн, оружие, снаряды, общие сущности. Не боевой код |

## Решение

Вариант 1 — папка на игровой объект.

```
Features/
├── Tank/               Input/ Model/ View/   ← Vehicles/ целиком (16)
├── Weapons/            Input/ Model/ View/   ← из Combat (9)
├── Bullets/            плоско                ← из Combat (3)
├── Grenades/           Input/ Model/ View/   ← из Combat (9) + FlashScreenBehaviour из Vitals
├── Explosions/         плоско                ← из Combat (2)
├── Health/             Model/ View/          ← Vitals/ без FlashScreenBehaviour + HitSoundBehaviour из Combat
├── Enemies/            Model/ View/          ← EnemyAI/ целиком (9)
├── DestructibleWalls/  плоско                ← Destruction/ целиком (9)
├── Abilities/  Buffs/  Cameras/  Interaction/  Movement/  Spawning/   — без изменений
Shared/Timers/CooldownTimer.cs                ← из Combat
```

`Combat/`, `Vehicles/`, `Vitals/`, `EnemyAI/`, `Destruction/` исчезают.

### Куда уходит `Combat/`

| Файл | Куда |
|---|---|
| `Input/CharacterFireController` | `Weapons/Input/` |
| `Model/Ammo`, `CharacterFireAction`, `DamageCastBehaviour`, `RangeWeaponFireAction`, `TickCooldownBehaviour`, `WeaponCatalog`, `WeaponID` | `Weapons/Model/` |
| `View/AttackSoundBehaviour` | `Weapons/View/` |
| `Model/BulletCollisionBehaviour`, `BulletHitUseCase`, `RicochetUseCase` | `Bullets/` |
| `Input/CharacterGrenadeController` | `Grenades/Input/` |
| `Model/FlashUseCase`, `GrenadeFuseBehaviour`, `GrenadeLandingBehaviour`, `GrenadeSwitchAction`, `GrenadeThrowAction`, `GrenadeTrajectory`, `GrenadeType` | `Grenades/Model/` |
| `View/GrenadeHudBehaviour` | `Grenades/View/` |
| `Model/ExplosionUseCase`, `View/ExplosionFlash` | `Explosions/` |
| `View/HitSoundBehaviour` | `Health/View/` |
| `Model/CooldownTimer` | `Shared/Timers/` |

Плюс `Vitals/View/FlashScreenBehaviour` → `Grenades/View/`.

### Решения по спорным файлам

- **`DamageCastBehaviour` → `Weapons/`.** Это удар оружием ближнего боя; висит на каждом персонаже, но
  работает только с melee-оружием в руках.
- **`AttackSoundBehaviour` → `Weapons/`.** Подписан на `FireEvent`, то есть звучит на атаку, а не на
  врага; сейчас его ставит только `EnemyAudioInstaller`, но смысл у него оружейный.
- **`HitSoundBehaviour` → `Health/`.** Подписан на `Health.OnHealthChanged`: это звук получения
  урона, родственник `TakeDamageSoundBehaviour`, а не звук попадания пули.
- **`BlindBuff`, `BlindBuffConfig`, `BlindStumbleBehaviour` остаются в `Buffs/`.** Их вешает флешка, но
  это баффы, как `StunBuff`: всё семейство живёт в одной папке и собирается одним `BuffUseCase`.
- **`Bullets/`, а не `Projectiles/`.** Снаряд танка рикошетит тем же `RicochetUseCase`, но папка о
  пулях, а снаряд — их частный случай.
- **`Explosions/` отдельно от `Grenades/`.** Взрывается и граната, и снаряд танка: взрыв не
  принадлежит ни одной из них.
- **`Shared/Timers/`, а не корень `Shared/`.** В `Shared/` все файлы лежат в подпапках; папка с одним
  файлом здесь оправдана тем же, чем `Buffs/View/` в прошлой спеке — она называет, что внутри.

### Чего не делаем

- **Классы не переименовываются.** `VehicleUseCase`, `VehicleDriveController`,
  `VehicleFireController` остаются как есть: переименование — правка кода и инсталлеров, а здесь
  меняются только папки.
- **`Entities/` не трогаем.** `Entities/Vehicle/`, `Entities/Projectile/` остаются под старыми
  именами: решено ограничиться `Features/`.
- **Подпапки в `DestructibleWalls/` и `Cameras/` не вводим**, хотя файлов в них больше четырёх:
  это отдельная правка, не переименование.
- **Старые спеки и планы не правим.** Пути в них — история, `ScriptLayoutMigration.cs` — одноразовая
  миграция, которая уже отработала.

## Риски и порядок

Главный риск тот же, что и в прошлый раз, — потерять GUID и порвать ссылки из префабов и сцен.

- Переносы через `AssetDatabase.MoveAsset` в открытом редакторе: `.meta` едет за файлом.
- Только когда редактор не в Play: перенос вызывает перекомпиляцию, а перекомпиляция в Play
  стирает состояние сущностей и ломает RayFire. Редактор общий для нескольких сессий.
- Неймспейс у всех файлов `Game`, на путь компилятору всё равно: код не меняется ни в одной строке.
- Строковых путей `Features/...` в коде нет, кроме `ScriptLayoutMigration.cs` (см. выше).

Порядок: создать папки → `MoveAsset` по таблице → удалить опустевшие папки → `Refresh` →
перекомпиляция.

## Как проверяем

- Компиляция без ошибок.
- Набор GUID всех `*.cs.meta` в `Assets/Game/Scripts` до и после совпадает один в один.
- `Playground.unity` открывается без «The referenced script is missing».
- В `Features/` нет пустых папок и папок без `.meta`.

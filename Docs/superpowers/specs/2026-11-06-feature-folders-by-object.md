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

## Результат

Прогон: 30 переносов, 0 ошибок, `Combat/` удалена. GUID всех 215 скриптов совпали с состоянием
до переноса; у переименованных папок GUID тоже сохранился. В 92 префабах `Assets/Game` пропавших
скриптов нет. В сцене пропавшие компоненты есть только на объекте `[CameraRoot]/[Camera]/cm` — это
три компонента Cinemachine, пакета которого в проекте нет; объект был таким и до переноса.

## Дополнение: связи между фичами

После переноса посчитаны прямые обращения к типам одной фичи из другой (без комментариев и без
вызовов через `EntityAPI`). Циклов нет. Два места оказались не там, где их используют, и исправлены
тем же днём.

| Что | Было | Стало | Почему |
|---|---|---|---|
| `FindTargetUseCase.cs` | `Enemies/Model/` | `Weapons/Model/` | Вызывает его только `DamageCastBehaviour` — поиск цели для удара ближнего боя. Враги им не пользуются. Перенос через `MoveAsset`, GUID прежний, код не менялся |
| Пролом стены (`Breach` и всё под ним) | `ExplosionUseCase` | новый `DestructibleWalls/BreachUseCase.cs` | Им пользуется и таран танка, который ничего не взрывает. Пролом — про стены. Код перенесён без изменений; `ExplosionUseCase` и `TankRamBehaviour` теперь зовут `BreachUseCase.Breach` |

Это единственное отступление от «код не меняется»: метод переехал в другой класс, поменялись два
вызова. `ExplosionUseCase` сократился с 338 до 145 строк и больше не знает про `BreakInChunks`,
`DemolitionQueue` и `TemporaryCollisionIgnore`.

Итог по связям: ушла Weapons → Enemies, Tank → Explosions сменилась на Tank → DestructibleWalls,
Explosions → DestructibleWalls сжалась с трёх типов до одного `BreachUseCase`.

Оставлено как есть: `StunBuff` и `BlindBuff` снимают с сущности конкретные поведения врага и
движения по типу (связи Buffs → Enemies, Buffs → Movement). Это не беспорядок в папках, а выбранный
способ оглушения; менять его — отдельная работа.

## Дополнение: общая папка `Vehicles/`

Тем же днём к танку добавились вертолёт и ездовой Регакс. Код «сесть, управлять, выйти» остался
в `Tank/`, хотя пользуются им все трое: при переносе машина была одна, и `Vehicles/` целиком стала
`Tank/`. Теперь общее вынесено обратно, а в папке каждого объекта осталось только его собственное.

| Файл | Было | Стало | Кто пользуется |
|---|---|---|---|
| `VehicleUseCase` | `Tank/Model/` | `Vehicles/Model/` | танк, вертолёт, Регакс, `TankDestroyBehaviour`, `MountDeathBehaviour` |
| `VehicleDriveController` | `Tank/Input/` | `Vehicles/Input/` | танк, вертолёт |
| `VehicleFireController` | `Tank/Input/` | `Vehicles/Input/` | танк, вертолёт, Регакс |
| `MountInstaller` | `Entities/Mount/` | `Entities/Vehicle/` | к установщикам танка и вертолёта |

`Tank/Input/` и `Entities/Mount/` опустели и удалены.

```
Features/
├── Vehicles/    Input/ Model/   ← общее для всего, во что садятся (3)
├── Tank/        Model/ View/    ← только танк
├── Helicopter/  Model/ View/    ← только вертолёт
├── Mount/       Model/          ← только ездовой Регакс
```

Правила те же: `AssetDatabase.MoveAsset` при выключенном Play, код не менялся ни в одной строке.

**Оставлено:** `TankDestroyBehaviour` остаётся в `Tank/`, хотя вертолёт тоже его использует
(с `fallOnDeath: true`). Перенос без переименования оставил бы в общей папке класс с именем `Tank*`,
а переименование — это уже правка кода. Это отдельный шаг.

**Проверка:** GUID всех 232 скриптов в `Assets/Game/Scripts` до и после совпали. Пропавших скриптов
в префабах `Assets/Game` нет, пустых папок в `Features/` нет, компиляция без ошибок.

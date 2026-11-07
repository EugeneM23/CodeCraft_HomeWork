# Character — только тела; игрок — обычные фичи; контексты — тонкие

**Дата:** 2026-11-07
**Статус:** утверждено (рекомендуемый вариант из трёх проектов и двух оценок)
**Продолжает:** [2026-11-06-player-enemy-character.md](2026-11-06-player-enemy-character.md),
[2026-11-06-review-dead-code-and-moves.md](2026-11-06-review-dead-code-and-moves.md).

## Что было не так

- **На одном уровне `Character/` — две оси:** виды тел (`PlayerCharacter`, `EnemyCharacter`), тело
  существа (`Creature`) и механики (`CharacterMovement`, `CharacterAttack`); отсюда повторы
  `Character/CharacterMovement`.
- **`PlayerCharacter/` наполовину не про персонажа:** 22 из 38 файлов на префаб `[Character]` не
  ставятся — это контроллеры ввода, способности, мана, курсор, маркер. Их ставит `PlayerContext`
  (игрок-человек), а не тело.
- **«Игрок» разбросан:** контекст в `Contexts/Player`, его системы — в `PlayerCharacter/`, `Cameras/`,
  `Controllables/Common`. `Contexts/Player` читался как дубль `PlayerCharacter`.

## Как выбирали

Три независимых проекта: «кто это» (`Features/Player` + `Features/Characters`), «что делает»
(механики наверху), «зеркало Atomic» (всё про игрока в `Contexts/Player`). Две оценки: глазами
новичка (победил «кто это», 8 против 7) и по проводке и цене (победил «Atomic», 8 против 7);
«механики наверху» — 4 у обоих. Итог собран из обоих лидеров. Решающий довод за фичи игрока в
`Features/`: так проект уже растёт сам — `FogOfWar` (поле `PlayerContextInstaller` + контроллер
контекста) добавлен параллельной сессией как обычная фича.

## Раскладка

```
Contexts/                         только контейнеры и их сборка
├── Game/                         без изменений
└── Player/                       PlayerContext, IPlayerContext, PlayerContextInstaller

Features/
├── Controls/                     управление игрока: всё, что читает клавиши и мышь и ведёт то, чем он управляет
│   ├── ControlSystemInstaller           ← Contexts/Player/PlayerCharacterSystemInstaller (переименован)
│   ├── CursorUseCase                    ← Character/PlayerCharacter/
│   ├── PlayerMoveController, PlayerAimController       ← PlayerCharacter/Movement/   (тело и техника)
│   ├── PlayerInteractController                        ← PlayerCharacter/Interaction/
│   ├── PlayerCharacterJumpController                   ← PlayerCharacter/Movement/   (только пешком)
│   ├── PlayerCharacterFireController                   ← PlayerCharacter/Attack/
│   ├── PlayerCharacterGrenadeController                ← PlayerCharacter/Grenades/
│   ├── PlayerCharacterDropWeaponController             ← PlayerCharacter/Interaction/Weapons/
│   ├── PlayerCharacterDeathController                  ← Contexts/Player/
│   └── ControllableDriveController, ControllableFireController ← Controllables/Common/ (только техника)
├── Abilities/                    ← Character/PlayerCharacter/Abilities/ (папкой: Dash/ Stun/ Teleport/ Mana/)
├── Cameras/, FogOfWar/           без изменений
└── Character/                    только тела
    ├── Common/                   то, что ставят два вида и больше
    │   ├── Movement/             ← CharacterMovement/ (папкой)
    │   ├── Attack/               ← CharacterAttack/ (папкой, без анимации атаки игрока)
    │   └── Creature/             ← Creature/ (папкой, с Gibs/): Zombie, Regax, Enemy, Regax_Mount
    ├── PlayerCharacter/          префаб [Character]
    │   ├── 6 инсталлеров
    │   ├── Animation/            PlayerCharacterAttackAnimBehaviour ← CharacterAttack/Animation/CharacterAttackAnimBehaviour
    │   ├── Grenades/             GrenadeSwitchAction, GrenadeThrowAction, GrenadeTrajectory, UI/
    │   └── Interaction/          InteractInstaller, InteractUseCase, DetectInteractableBehaviour,
    │                             Markers/ (+ MarkerInstaller ← PickUps/), Weapons/
    └── EnemyCharacter/           без изменений
```

## Правила

1. **Контекст — только контейнер и его сборка.** Всё, что он ставит, — обычная фича в `Features/`:
   поле в `PlayerContextInstaller` = папка фичи (`Controls`, `Cameras`, `Abilities`, `FogOfWar`).
2. **`Character/` — по папке на вид тела** (по core-инсталлеру), общее для двух видов и больше — в
   `Common/<часть>`. Часть или своё решают ключи и ассеты, а не число пользователей сегодня:
   `TransformMoveBehaviour` работает только с ключами ходьбы → `Common/Movement`; анимация на слоях
   `AC_BaseCharacter` (есть только у игрока) → `PlayerCharacter/Animation`. Появился второй вид —
   файл переезжает в `Common/`.
3. **Ввод игрока — в `Controls/`.** Префикс контроллера говорит, что он ведёт: `Player*` — любое
   тело и технику, `PlayerCharacter*` — только пешком, `Controllable*` — только технику.

## Правки кода

- `PlayerCharacterSystemInstaller` → `ControlSystemInstaller` (обычный `[Serializable]` класс);
  поле в `PlayerContextInstaller` → `_controlSystem` с `[FormerlySerializedAs("_characterSystem")]`,
  чтобы сцены не потеряли настройку. Рядом с полями — комментарий «поле → папка фичи».
- `CharacterAttackAnimBehaviour` → `PlayerCharacterAttackAnimBehaviour` (обычный класс): строка в
  `PlayerCharacterAnimInstaller`, комментарии.
- Заголовки «// Combat» → «// Attack» в core-инсталлерах игрока и врага — как папка `Common/Attack`.
- `PlayerUseCase` удаляется: пустая обёртка с одним вызовом (`DashPickUpInstaller`).

## Чего не делаем

- `Character` не переименовываем в `Characters`: имя выбрал пользователь, а переименование тронуло
  бы ещё ~24 пути.
- `DashAbilityConfig` не переименовываем: `DashPickUpInstaller` ищет способность по
  `nameof(DashAbilityConfig)`.
- Ни один MonoBehaviour и ScriptableObject не переименовывается — только переносы (GUID сохраняются).

## Порядок и риски

- Параллельная сессия сейчас правит `Creature/Gibs`, `FogOfWar`, `PlayerContextInstaller`,
  `RegaxPrefabBuilder` — переносы и правки этих файлов только после неё.
- Только вне Play; проверка Play — в том же вызове, что и Refresh.

## Как проверяем

Компиляция; набор GUID `*.cs.meta` до и после совпадает (минус `PlayerUseCase`); у перенесённых
MonoBehaviour `MonoScript.GetClass()` находит класс; в префабах и сценах — только прежние 7 ссылок
Cinemachine; поле `_controlSystem` в обеих сценах заполнено; тесты `Game.Tests`; в Play персонаж
ходит, целится и стреляет, способности работают.

## Дополнение: гранаты и взаимодействие — фичи, а не части тела

Попали в `PlayerCharacter/` по правилу «ставится на один вид — лежит в его папке». Но это способности,
которые телу выдают, а не то, из чего тело сделано: `PlayerCharacterGrenadeInstaller` даёт запас
гранат, выбор, бросок и HUD; `PlayerCharacterFlashInstaller` — ослепление от флешки с белым экраном;
`InteractInstaller` — поиск того, с чем взаимодействовать. Так же уже лежат баффы:
`CharacterBuffInstaller` — в `Features/Buffs`.

**Уточнение правила 2.** `Character/` — тела: как собраны (Core/Anim/Audio/Vfx-инсталлеры) и из чего
сделано любое ходячее тело (`Common/`: движение, атака, облик существа). Способности, которые телу
можно выдать (гранаты, взаимодействие, баффы, способности игрока), — фичи со своим инсталлером в
`Features/`, даже если сейчас их выдают одному телу.

```
Features/Grenades/                граната целиком
├── (снаряд, как был: GrenadeInstaller, FlashInstaller, FlashUseCase, GrenadeFuseBehaviour,
│    GrenadeLandingBehaviour, GrenadeType)
├── Throwing/   GrenadeThrowerInstaller (был PlayerCharacterGrenadeInstaller), GrenadeThrowAction,
│               GrenadeSwitchAction, GrenadeTrajectory, UI/GrenadeHudBehaviour
└── Flashed/    FlashScreenInstaller (был PlayerCharacterFlashInstaller), UI/FlashScreenBehaviour
Features/Interaction/             ← Character/PlayerCharacter/Interaction/ (папкой)
├── InteractInstaller, InteractUseCase, DetectInteractableBehaviour
├── Markers/    MarkerInstaller, InteractMarkerHighlightBehaviour, InteractMarkerUIBehaviour
└── Weapons/    PickUpWeaponUseCase, DropWeaponUseCase
Character/PlayerCharacter/        PlayerCharacterCore/Anim/Audio/VfxInstaller + Animation/
```

Два MonoBehaviour переименовываются вместе с файлами (`RenameAsset`, GUID сохраняется): они стоят на
`[Character].prefab` и в `Playground.unity`. `GrenadeBuilder` и комментарии правятся под новые имена.
`InteractInstaller` — `[Serializable]`, лежит в поле `_interactInstaller` у `PlayerCharacterCoreInstaller`
(и `TankBuilder` пишет `_interactInstaller._layerMask`) — имя поля не меняется.

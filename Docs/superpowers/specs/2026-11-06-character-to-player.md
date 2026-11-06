# Character → Player

**Дата:** 2026-11-06
**Статус:** утверждено
**Продолжает:** [2026-11-06-character-subfeatures.md](2026-11-06-character-subfeatures.md).

## Задача

То, чем управляет игрок, называется `Player`. Папки `Features/Character/` больше нет.

## Что было не так

`Character/` смешивал две вещи: то, что есть только у игрока (ввод, инсталлеры его префаба,
гранаты, способности), и общую механику, которую берут враг и Regax (модель движения, атака). Имя
`Player` для общей механики соврало бы, поэтому она уходит наверх.

## Раскладка

```
Features/Player/               PlayerAnim/Audio/Core/Flash/Grenade/VfxInstaller
├── Movement/      PlayerMoveController, PlayerRotateController, PlayerJumpController
├── Attack/        PlayerFireController
├── Interaction/   без изменений внутри (Input/ Model/ Markers/)
├── Abilities/     без изменений внутри
└── Grenades/      PlayerGrenadeController, GrenadeSwitchAction, GrenadeThrowAction,
                   GrenadeTrajectory + UI/
Features/Movement/   Model/ Animation/ Audio/     ← из Character/Movement без Input/
Features/Attack/     CharacterFireAction, DamageCastBehaviour
Features/Buffs/      + CharacterBuffInstaller (стоит на игроке, Zombie, Regax, Regax_Mount)
```

## Переименования (только классы, которые есть лишь у игрока)

| Было | Стало |
|---|---|
| `CharacterAnimInstaller`, `CharacterAudioInstaller`, `CharacterCoreInstaller`, `CharacterFlashInstaller`, `CharacterGrenadeInstaller`, `CharacterVfxInstaller` | `Player…Installer` |
| `CharacterSystemInstaller`, `CharacterDeathController` (остаются в `Contexts/Player/`) | `PlayerSystemInstaller`, `PlayerDeathController` |
| `CharacterMoveController`, `CharacterRotateController`, `CharacterJumpController`, `CharacterFireController`, `CharacterGrenadeController`, `CharacterInteractController`, `CharacterDropWeaponController` | `Player…Controller` |
| `CharacterShakeController` (остаётся в `Cameras/`) | `PlayerShakeController` |
| тест `CharacterDeathControllerTests` | `PlayerDeathControllerTests` |

`CharacterMoveController` и `CharacterRotateController` ставит и `MountInstaller` — это ввод игрока,
когда он едет на Regax, поэтому `Player` им подходит.

Файлы переименовываются через `AssetDatabase.RenameAsset` — GUID сохраняется, ссылки инсталлеров из
префабов и сцен не рвутся. Имена классов в префабах и сценах строками не встречаются (проверено).
Коллизий с существующими `Player*` нет.

## Чего не делаем

- Общий код не переименовываем: `CharacterMoveBehaviour`, `CharacterVelocityBehaviour`,
  `CharacterRotateBehaviour`, `CharacterFireAction`, `CharacterMoveAnimBehaviour`,
  `CharacterBuffInstaller` остаются.
- Ключи `EntityAPI`/`PlayerContextAPI` (`GetCharacter`, `OnCharacterChanged`...) и префаб
  `[Character].prefab` не трогаем.
- `Sandbox/CharacterDebug`, сборщики в `Editor/` (кроме правки имён классов в коде) не трогаем.

## Как проверяем

Как раньше: `MoveAsset`/`RenameAsset` вне Play, компиляция, GUID `*.cs.meta` до и после совпадают,
нет новых неразрешённых скриптов в префабах и сценах, нет пустых папок, `Features/Character/` нет.

## Результат

Прогон: 6 переносов (`Character` → `Player` и `Movement`/`Movement/Input` — папками, GUID папок
сохранён) и 17 переименований файлов, 0 ошибок. Имена классов заменены в 30 файлах кода, включая
сборщики `Editor/` и тесты. GUID всех скриптов и тестов совпали. Компиляция чистая, у шести
переименованных инсталлеров `MonoScript.GetClass()` находит класс. В префабах и сценах — те же 7
неразрешённых ссылок Cinemachine, новых нет. `Features/Character/` больше нет.

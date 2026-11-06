# PlayerCharacter и EnemyCharacter

**Дата:** 2026-11-06
**Статус:** утверждено
**Продолжает:** [2026-11-06-character-to-player.md](2026-11-06-character-to-player.md).

## Задача

Объекты называются по тому, что это: персонаж игрока и персонаж-враг. Переименовываются папки, классы
и инсталлеры.

## Папки

| Было | Стало |
|---|---|
| `Features/Player/` | `Features/PlayerCharacter/` |
| `Features/Enemies/` | `Features/EnemyCharacter/` |

## Классы (файлы — через `AssetDatabase.RenameAsset`, GUID сохраняется)

**`Player…` → `PlayerCharacter…`:** `AnimInstaller`, `AudioInstaller`, `CoreInstaller`,
`FlashInstaller`, `GrenadeInstaller`, `VfxInstaller`, `SystemInstaller` (в `Contexts/Player/`),
`DeathController` (в `Contexts/Player/`) и тест `DeathControllerTests`, `DropWeaponController`,
`FireController`, `GrenadeController`, `InteractController`, `JumpController`, `MoveController`,
`RotateController`, `ShakeController` (в `Cameras/`).

**`Enemy…` → `EnemyCharacter…`:** `AnimInstaller`, `AudioInstaller`, `CoreInstaller`,
`VfxInstaller`, `AttackBehaviour`, `ChaseBehaviour`, `PatrolBehaviour`, `AttackAnimBehaviour`,
`TakeDamageAnimBehaviour`.

## Чего не делаем

- Контекст игрока — не персонаж: `PlayerContext`, `PlayerContextInstaller`, `PlayerContextAPI`,
  `PlayerUseCase`, `PlayerID`, папка `Contexts/Player/` остаются.
- `EnemyController` (в `GameContextInstaller.cs`) и `Spawning/EnemyTrigger` — спавн, не персонаж:
  остаются.
- Классы без префикса внутри папок (`InteractInstaller`, `AbilitySystemInstaller`,
  `GrenadeSwitchAction`, `PatrolUseCase`...) не трогаем.
- Ключи `EntityAPI`/`PlayerContextAPI`, префабы — не трогаем.

Коллизий с существующими `PlayerCharacter*`/`EnemyCharacter*` нет; строками в префабах и сценах эти
имена не встречаются.

## Как проверяем

Вне Play; компиляция; GUID `*.cs.meta` до и после совпадают; у переименованных инсталлеров
`MonoScript.GetClass()` находит класс; нет новых неразрешённых скриптов в префабах и сценах.

## Результат

2 папки и 26 файлов переименованы, 0 ошибок; имена классов заменены в 47 файлах кода, включая
`Editor/` и тесты. GUID всех скриптов и тестов совпали. Компиляция чистая, у 10 переименованных
инсталлеров `MonoScript.GetClass()` находит класс. В префабах и сценах — те же 7 неразрешённых
ссылок Cinemachine, новых нет.

## Дополнение: общая папка `Character/`

```
Features/Character/
├── CharacterAttack/     ← Features/Attack
├── CharacterMovement/   ← Features/Movement
├── PlayerCharacter/     ← Features/PlayerCharacter
└── EnemyCharacter/      ← Features/EnemyCharacter
```

Три переноса папок через `MoveAsset`, GUID папок и скриптов сохранены, компиляция чистая, новых
неразрешённых скриптов в префабах и сценах нет. Следом `Features/Attack/` перенесена в
`Character/CharacterAttack/` (общая атака игрока, врага и Regax) — тот же результат проверки.

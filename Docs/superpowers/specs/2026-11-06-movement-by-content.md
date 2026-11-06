# Movement: Model/ по содержимому

**Дата:** 2026-11-06
**Статус:** утверждено
**Продолжает:** [2026-11-06-character-to-player.md](2026-11-06-character-to-player.md).

## Что было не так

`Features/Movement/Model/` — 9 файлов трёх разных механик (перемещение, поворот, прыжок) под
абстрактным именем, рядом с уже предметными `Animation/` и `Audio/`. `GravityScaler` — пустой
MonoBehaviour (`Start()` без тела): ни ссылок в коде, ни в префабах/сценах/ассетах.

## Решение

```
Features/Movement/
├── Move/        MoveUseCase, CharacterMoveBehaviour, RigidbodyMoveBehaviour, CharacterVelocityBehaviour
├── Rotate/      RotateUseCase, CharacterRotateBehaviour, RigidbodyRotateBehaviour
├── Jump/        JumpUseCase
├── Animation/   CharacterMoveAnimBehaviour
└── Audio/       MoveStepSoundBehaviour
```

`GravityScaler.cs` удаляется (в истории git остаётся). `Model/` исчезает.

## Чего не делаем

- Общие классы `Character*` не переименовываем — отдельный шаг, если понадобится.
- `Model/` в других фичах не трогаем.

## Как проверяем

`MoveAsset` вне Play, компиляция, GUID `*.cs.meta` до и после совпадают (минус `GravityScaler`), нет
новых неразрешённых скриптов в префабах и сценах.

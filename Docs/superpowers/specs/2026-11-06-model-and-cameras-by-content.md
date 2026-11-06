# Model/ и Cameras/ — по содержимому

**Дата:** 2026-11-06
**Статус:** утверждено
**Продолжает:** [2026-11-06-review-dead-code-and-moves.md](2026-11-06-review-dead-code-and-moves.md).

## Задача

Убрать последние абстрактные папки `Model/` и разложить плоскую `Cameras/` (11 файлов трёх
механизмов). Только переносы через `MoveAsset`, код не меняется.

## Раскладка

```
Buffs/                CharacterBuffInstaller, BuffBase, BuffConfig, TemporaryBuff, BuffUseCase,
                      DiscardAllBuffBehaviour                      — механизм баффов
├── Blind/            BlindBuff, BlindBuffConfig, BlindStumbleBehaviour
├── ExtraDamage/      ExtraDamageBuff, ExtraDamageBuffConfig
├── Jump/             JumpBuff, JumpBuffConfig
├── MoveSpeed/        MoveSpeedBuff, MoveSpeedBuffConfig
├── Stun/             StunBuff, StunBuffConfig
└── Vfx/              BuffVFXBehaviour

Character/PlayerCharacter/Abilities/
                      AbilitySystemInstaller, Ability, AbilityConfig, AbilityRunner, AbilityUseCase,
                      RayCastUseCase                               — механизм способностей
├── Mana/             ManaSystemInstaller, ManaUseCase, RestoreManaBehaviour
├── Dash/             DashAbilityConfig
├── Stun/             StunAbilityConfig
└── Teleport/         TeleportAbilityConfig, VectorUseCase

Character/PlayerCharacter/Interaction/
                      InteractInstaller, InteractUseCase, DetectInteractableBehaviour,
                      PlayerInteractController
├── Weapons/          PickUpWeaponUseCase, DropWeaponUseCase, PlayerCharacterDropWeaponController
└── Markers/          без изменений

Health/               Health, DamageUseCase, TakeDamageArgs, DeathBehaviour   ← Model/ (4 файла, плоско)
├── Animation/ Audio/ Vfx/

Weapons/              инсталлеры + WeaponCatalog, WeaponID, TickCooldownBehaviour,
                      Ammo, RangeWeaponFireAction                  ← Model/ (5 файлов, плоско)
└── Casings/

Cameras/              CameraInstaller, CameraSystemInstaller
├── Follow/           CameraFollowController, CameraFollowUseCase
├── Look/             CameraLookController, CameraLookSettings, CameraLookUseCase
└── Shake/            CameraShakeArgs, CameraShakeBehaviour, ShakeTransformUseCase, PlayerShakeController
```

- Баффы и способности — механизм в корне, каждый вид в своей папке: открыл папку — видишь список
  видов. Папки с одним файлом (Dash, Stun) оправданы тем же: они называют вид способности.
- `RayCastUseCase` нужен Stun и Teleport — остаётся в корне Abilities; `VectorUseCase` нужен
  только Teleport.
- В Health и Weapons после ухода `Model/` по 4–8 файлов одной темы — плоско.
- `Input/` в Interaction исчезает: контроллеры легли к своей механике.

## Проверка

Вне Play; компиляция; GUID `*.cs.meta` до и после совпадают; нет новых неразрешённых скриптов в
префабах и сценах; тесты `Game.Tests`; нет пустых папок.

## Результат

58 переносов, 0 ошибок; папок `Model/` и `Interaction/Input/` в `Features/` не осталось (кроме
`Movement` — там папок `Model/`/`Input/` нет уже давно). GUID всех скриптов совпали, компиляция
чистая, в префабах, сценах и ассетах-конфигах (баффы, способности, аспекты) — те же 7 ссылок
Cinemachine, новых нет. 134 теста `Game.Tests` проходят.

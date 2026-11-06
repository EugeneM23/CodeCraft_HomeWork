# Ревью: мёртвый код и первая волна переносов

**Дата:** 2026-11-06
**Статус:** утверждено
**Продолжает:** [2026-11-06-review-bugs.md](2026-11-06-review-bugs.md).

Параллельно другая сессия правит танк (Drive, Suspension, TankInstaller), `BulletCollisionBehaviour`,
`ExplosionUseCase`, `CharacterMovement/Footprints`, Anim-инсталлеры персонажей и `Features/Mud`.
Эти файлы в волне не участвуют.

## Мёртвый код (удалено)

Проверено двумя способами: имя в `*.cs` и GUID скрипта во всех ассетах.

- Файлы: `ProjectileCoreInstaller`, `ProjectileVisualInstaller`, `TrailView`, `TearDetachedSections`.
  Ссылка `UniTask` убрана из `Game.asmdef` — её держал только `TrailView`.
- Ключи `EntityAPI`, которых никто не читает: `MoveAction`, `DeathAction`, `PickUpPrefab`,
  `ShowUIAction`, `IsInteract`, `UITransform` (без единого обращения) и `ExtraDamage`,
  `DamageCastEnabled`, `DeathEvent`, `TriggerEventReceiver` (только `Add*`) — вместе со строками
  `Add*` и полями `_triggerReceiver` в core-инсталлерах игрока и врага.
- Поля в инспекторе, которые ни на что не влияли: `BulletInstaller._damage` (урон пуле задаёт
  оружие), `EnemyCharacterCoreInstaller._damage` и его `AddDamage` (урон врага — у его оружия),
  `DashAbilityConfig._multiplier`, `_initialCharges` у Stun/Teleport, `StunBuffConfig.Time`.

Отложено: ключ `SecondaryWeapon` (в `TankInstaller`, его сейчас правит другая сессия); выключенная
ударная волна танка — вопрос пользователю.

## Переносы и переименования

`AssetDatabase.MoveAsset`/`RenameAsset` вне Play, GUID сохраняются.

```
Shared/
├── Physics/   PhysicsLayers                         ← Shared/Math
├── Spawning/  GameFactory, GameFactoryID, EntityPrefabPool, LifeTimeBehaviour, SpawnUseCase
│                                                    ← Features/Spawning (это пул, не фича)
├── Vfx/       HitEffectInstaller (файл HitInstaller.cs → HitEffectInstaller.cs), ExplosionFlash,
│              LightAnimation                        ← Bullets/, Explosions/, Shared/View
├── Audio/     AudioSourceInstaller                  ← Shared/View
├── Events/    без изменений
└── Timers/    без изменений
Features/EffectAreas/           EffectAreaInstaller, EffectAreaBehaviour   ← Shared/Aspects
└── Aspects/                    DamageAspect, JumpAspect, MoveSpeedAspect, ScriptableEntityAspectComposite
Features/Weapons/Casings/       CasingInstaller (было ShellInstaller в Controllables/Tank/Gun)
Features/Character/CharacterAttack/  + FindTargetUseCase (из Weapons/Model)
                                Animation/ + SwitchAnimatorBehaviour, SwitchAnimatorUseCase (из Shared/Animations)
Features/Character/CharacterMovement/Move/   CharacterMoveBehaviour → TransformMoveBehaviour
Features/Character/CharacterMovement/Rotate/ CharacterRotateBehaviour → TransformRotateBehaviour
Features/Character/EnemyCharacter/AI/        EnemyAggroZone (было Spawning/EnemyTrigger)
Features/Character/EnemyCharacter/Animation/ + AnimatorTrigger (из Shared/Animations)
Features/Character/EnemyCharacter/Audio/     AttackSoundBehaviour (из Weapons/Audio)
Features/Character/PlayerCharacter/Abilities/Model/ + RayCastUseCase, VectorUseCase (из Shared/Math)
Features/Health/Animation/      + DeathStateBehaviour (из Shared/Animations)
Editor/Controllables/           ← Editor/Vehicles
```

Почему:
- `ShellInstaller` собирает гильзы всего огнестрела (`Shell_01`, `TankCasing_01`), не снаряд танка.
  Ключи `Shell*` в EntityAPI пока остаются.
- `HitEffectInstaller` — общий одноразовый эффект на 10 префабах (флешка, ракета, дым из дула);
  `ExplosionFlash` висит и на дыме из дула. Файл назван не по классу.
- `EnemyTrigger` ничего не спавнит: это зона агрессии, выдаёт врагам цель.
- `CharacterMove/RotateBehaviour` ставит только враг; это пара к `Rigidbody*` — движение через transform.
- Код одного владельца уходит из `Shared/` к владельцу; абстрактные `Math/`, `View/`, `Animations/`
  исчезают.

## Открытые вопросы (к пользователю)

1. Инсталлеры `EnemyCharacterAnim/Audio/VfxInstaller` собирают и ездового Regax — общее «тело
   существа». Переименовать (и как) или оставить.
2. `PlayerCharacterMove/Rotate/Interact/ShakeController` управляют и техникой — имя.
3. Выключенная ударная волна танка — удалить или оставить.
4. Дальше: `Model/` в Buffs/Health/Weapons/Abilities/Interaction, `Cameras/` (11 файлов, три
   механизма), дублирование.

## Результат

- Мёртвый код: 4 файла, 10 ключей EntityAPI, 7 полей инспектора удалены; компиляция чистая.
- Переносы: 25 операций `MoveAsset`/`RenameAsset`, 0 ошибок; `Shared/Math`, `Shared/View`,
  `Shared/Animations`, `Weapons/Audio`, `Features/Spawning` исчезли. Имена классов заменены в 11
  файлах (включая `TankBuilder`, `PlaygroundBuilder`, `StunBuff`).
- GUID всех 254 скриптов и тестов совпали; у переименованных MonoBehaviour (`HitEffectInstaller`,
  `EnemyAggroZone`, `CasingInstaller`) `MonoScript.GetClass()` находит класс. В префабах и сценах —
  те же 7 неразрешённых ссылок Cinemachine, новых нет. 134 теста `Game.Tests` проходят.

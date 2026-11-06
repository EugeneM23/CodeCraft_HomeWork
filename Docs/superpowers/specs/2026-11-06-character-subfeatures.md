# Подфичи персонажа и предметные имена вместо View

**Дата:** 2026-11-06
**Статус:** утверждено
**Продолжает:** [2026-11-06-installers-in-features.md](2026-11-06-installers-in-features.md).

## Что было не так

- **`Character/` — 7 инсталлеров и ничего больше.** Код, который есть только у персонажа (ввод,
  прыжок, взаимодействие, способности, бросок гранат, HUD), лежал в `Movement/`, `Interaction/`,
  `Abilities/`, `Grenades/`, `Weapons/`.
- **`View/` ничего не говорит.** В `Health/View` вперемешку анимация, звуки и кровь.

## Где граница персонажа

По тому, какие инсталлеры используют каждый файл:

- только персонаж — ввод, прыжок, взаимодействие, способности и мана, бросок гранат, HUD гранат,
  белый экран флешки;
- персонаж, враг и Regax — модель движения, `CharacterFireAction`, `DamageCastBehaviour`. Враг и
  Regax собраны как персонаж и берут это из `Character/`;
- все, включая технику и пикапы, — `Health/`, `Buffs/`: остаются на верхнем уровне.

## Раскладка

```
Features/Character/            7 инсталлеров
├── Movement/      ← Features/Movement (Input/ Model/ + Animation/ Audio/ вместо View/)
├── Attack/        CharacterFireController, CharacterFireAction, DamageCastBehaviour   ← Weapons
├── Interaction/   InteractInstaller + Input/ Model/ Markers/   ← Features/Interaction
├── Abilities/     AbilitySystemInstaller, ManaSystemInstaller + Model/   ← Features/Abilities
└── Grenades/      CharacterGrenadeController, GrenadeSwitchAction, GrenadeThrowAction,
                   GrenadeTrajectory + UI/ (GrenadeHudBehaviour, FlashScreenBehaviour)
Features/Grenades/   плоско: GrenadeInstaller, FlashInstaller, FlashUseCase,
                     GrenadeFuseBehaviour, GrenadeLandingBehaviour, GrenadeType
Features/Weapons/    инсталлеры + Model/ + Audio/ (AttackSoundBehaviour)
Features/Health/     Model/ + Animation/ (DeathAnim) + Audio/ (DeathSound, HitSound, PainSound,
                     TakeDamageSound) + Vfx/ (DeathEffect, TakeDamageBlood)
Features/Buffs/      Model/ + Vfx/ (BuffVFXBehaviour)
```

## Правило (дополняет правила прошлой спеки)

**Слой представления называется по содержимому:** `Animation/`, `Audio/`, `Vfx/`, `UI/`,
`Markers/`. Папок `View/` в `Features/` больше нет. `Input/` и `Model/` остаются.

## Чего не делаем

- `Model/` по содержимому не делим — отдельная правка.
- Классы не переименовываем, код не меняется.
- `Health/`, `Buffs/`, `Cameras/` (включая `CharacterShakeController`) не переносим в `Character/`.

## Как проверяем

Как в прошлый раз: перенос через `AssetDatabase.MoveAsset` вне Play, компиляция, набор GUID
`*.cs.meta` до и после совпадает, в префабах и сценах нет новых неразрешённых скриптов, нет пустых
папок и папок без `.meta`.

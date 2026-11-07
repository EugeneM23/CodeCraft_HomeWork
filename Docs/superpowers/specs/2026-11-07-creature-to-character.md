# Creature → Character

**Дата:** 2026-11-07
**Статус:** утверждено (пользователь: «creature давай заменим на character так логичнее»; раскладку
выбрали из трёх проектов и двух оценок)
**Продолжает:** [2026-11-07-character-and-player-layout.md](2026-11-07-character-and-player-layout.md).

## Что было не так

`Character/Common/Creature/` вводил третье слово для тела, хотя рядом уже есть `Character`. Имена
`Creature*Installer` не показывали, что это обычная пара к `PlayerCharacter*Installer`: тот же набор
Anim/Audio/Vfx, только для всех тел, кроме игрока (Enemy, Zombie, Regax, Regax_Mount).

## Раскладка

```
Character/Common/
├── CharacterAnimInstaller        ← Creature/CreatureAnimInstaller   (MonoBehaviour, GUID 3bf3370d)
├── CharacterAudioInstaller       ← Creature/CreatureAudioInstaller  (MonoBehaviour, GUID 9d7c3305)
├── CharacterVfxInstaller         ← Creature/CreatureVfxInstaller    (MonoBehaviour, GUID 30226912)
├── Attack/Animation/CharacterAttackAnimBehaviour ← Creature/Animation/CreatureAttackAnimBehaviour
├── Attack/Audio/AttackSoundBehaviour             ← Creature/Audio/
├── Gibs/                         ← Creature/Gibs/ (папкой)
│   └── CharacterGibsInstaller    ← CreatureGibsInstaller (MonoBehaviour, GUID f93930a8), Gibs, GibsOnDeathBehaviour
└── Movement/                     без изменений
Health/Animation/
├── TakeDamageAnimBehaviour       ← Creature/Animation/CreatureTakeDamageAnimBehaviour
└── AnimatorTrigger               ← Creature/Animation/
```

Почему так:
- Установщики встают в корень `Common/` рядом с `Attack/` и `Movement/`. Читается как пара к
  `PlayerCharacter/`: `PlayerCharacterAnimInstaller` у игрока, `CharacterAnimInstaller` у всех остальных.
- Поведения уходят к той части, на событие которой реагируют, как уже устроен `Movement/`: анимация
  и звук атаки слушают выстрел оружия → `Attack/`; вздрагивание слушает урон → `Health/Animation`,
  рядом с `DeathAnimBehaviour`.
- `AnimatorTrigger` нужен только анимациям урона и атаки и переходам AC_Zombie/AC_Rapax. Он идёт в
  `Health/Animation`, а не в новую `Shared/Animation`: папку `Shared/Animations` 2026-11-06 убрали как
  абстрактную.
- `Gibs/` остаётся в `Common/`: это то, из чего сделано тело (облик существа), а не способность.
  `Health/Gibs` дал бы цикл через `ExplosionUseCase`.

## Имена в коде и комментариях

- Классы и упоминания типов в коде, `cref` и комментариях — по новым именам.
- В комментариях «creature» → «character», где речь о любом теле, кроме игрока. Где речь о Regax,
  пишем «Regax», в коде верхового — «the mount». «The character» в проекте значит и игрока
  (`[Character].prefab`, `GetCharacter()`), поэтому так не пишем там, где можно прочитать как игрока.
- `RegaxGibsBuilder`: `CREATURE_PREFAB` → `REGAX_PREFAB`, `WireCreature` → `WireRegax`,
  `CreatureMaterial` → `RegaxMaterial`.
- Старые спеки — история, их не правим. Имена `CharacterAnimInstaller` и `CharacterAttackAnimBehaviour`
  раньше принадлежали игроку (сейчас у него `PlayerCharacter*`). Теперь это тело не-игрока, у них
  другие GUID.

## Как проверяем

Компиляция. Набор GUID `*.cs.meta` до и после совпадает. `MonoScript.GetClass()` находит четыре
переименованных MonoBehaviour. На Enemy, Zombie, Regax и Regax_Mount нет пропавших скриптов.
`[Character].prefab` и обе сцены ссылаются на те же GUID, что и раньше. `grep -ri creature` по
`Assets/Game` и `Tools/` ничего не находит. Тесты `Game.Tests`.

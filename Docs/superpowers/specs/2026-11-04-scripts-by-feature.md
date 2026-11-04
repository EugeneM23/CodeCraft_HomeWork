# Раскладка скриптов по фичам

**Дата:** 2026-11-04
**Статус:** утверждено (вариант 2 из трёх предложенных)

## Задача

Сделать дерево скриптов понятным: открыл папку фичи — видишь механику целиком.

## Что было не так

| Проблема | Цифра |
|---|---|
| Скрипты в `Assets/Game/Scripts` | 148 файлов в 61 папке |
| Папок с одним файлом | 20 |
| Максимальная вложенность | 8 уровней |
| Неймспейсов, не связанных с папками | `Game` (93), `Game.Gameplay` (45), `Game.Content` (5) |
| Файлов вообще без неймспейса | 4 |

Корневая причина — верхний уровень разрезан по слоям (`GameEntities/{Core,Content,View}`), а не по
фичам. Одна механика жила в 4–6 папках из трёх разных корней:

- урон: `Core/TakeDamage` + `Core/Death` + `View/TakeDamage` + `View/Death` + `View/BodyFall` + `Modules/Health.cs`
- передвижение: `Core/Move` + `Core/Rotate` + `Core/Jump` + `View/Move` + `PlayerContext/Controllers` + `Content/GravityScaler`

Сверху накопилось: два класса `AttackSoundBehaviour`, мёртвый `Joystick`, два живых сборщика
плейграунда, и `PlaygroundBuilder.cs` в рантайм-сборке (билд плеера упал бы на `UnityEditor`).

## Решение

Фича первична, слой вторичен.

```
Assets/Game/Scripts/
├── Contexts/            Game/ Player/           — контейнеры и их инсталлеры
├── Declarations/        EntityAPI, GameContextAPI, PlayerContextAPI, AbilityAPI, PlayerID
├── Features/
│   ├── Movement/        Model/ View/ Input/
│   ├── Combat/          Model/ View/ Input/
│   ├── Vitals/          Model/ View/            — здоровье, урон, смерть
│   ├── Buffs/           Model/ View/
│   ├── Abilities/       Model/                  — вместе с маной: мана существует только ради способностей
│   ├── Interaction/     Model/ View/ Input/
│   ├── EnemyAI/         Model/ View/
│   ├── Cameras/         (плоско, 8 файлов)
│   └── Spawning/        (плоско, 6 файлов)
├── Entities/            Character/ Enemy/ Weapon/ Projectile/ PickUp/ Common/
├── Shared/              Aspects/ Animations/ Events/ Math/ View/
├── Sandbox/             только для тестовых сцен
└── Editor/              своя сборка Game.Editor
```

### Правила

1. **Фича — папка в `Features/`.** Внутри `Model/` (логика, тик, use-cases), `View/` (подписка на
   события, аниматор, звук, VFX), `Input/` (контроллеры контекста игрока). Подпапки появляются
   только если в фиче больше четырёх файлов; мелкие (`Cameras`, `Spawning`) остаются плоскими.
   Если фича крупная, но слой у неё один, подпапка всё равно ставится — `Abilities/Model/` без
   `View/` рядом сообщает, что вью-кода у способностей нет, и каждая крупная фича читается
   одинаково. По той же причине `Buffs/View/` и `Combat/Input/` остаются с одним файлом: они
   отделяют слой, а не просто дробят папку, как прежние `Core/Jump/` и `Core/Vector/`.
2. **Инсталлеры — не члены фичи.** `CharacterCoreInstaller` трогает движение, бой, здоровье и баффы
   одновременно: это точка сборки. Они живут в `Entities/<Префаб>/` и отвечают на вопрос «из чего
   собран персонаж» одной папкой.
3. **`Shared/`** — механизмы, которыми пользуются несколько фич и которые не принадлежат ни одной:
   аспекты, переключение аниматора, приёмники событий Unity, векторная математика.
4. **`Declarations/`** — файлы с `[EntityExtensionsAPI]`, где объявлены ключи. Сами extension-методы
   генератор дописывает в partial-класс при компиляции; эти файлы — объявления, не вывод генератора.
5. **Слой читается по суффиксу имени:** `*UseCase`, `*Behaviour`, `*AnimBehaviour`,
   `*SoundBehaviour`, `*Controller`, `*Installer`, `*Config`.

### Неймспейсы: один `Game` на весь рантайм

Отступление от «неймспейс = папка», которое я предлагал в варианте 1. Причины:

- Неймспейс на фичу означал бы `Game.Camera`, а внутри `Game.*` это сразу ломает каждое обращение к
  типу `UnityEngine.Camera` — компилятор разрешает `Camera` в неймспейс. На этом уже спотыкались в
  `CharacterRotateController`. Та же мина — `Game.Animation` против `UnityEngine.Animation`.
- Понимание даёт дерево файлов, а не неймспейс: в IDE видно папки.
- `WeaponID` остаётся в `Game`, поэтому сериализованная Odin-строка `Game.WeaponID, Game` (6 строк в
  `GameScene.unity` и `Playground.unity`) остаётся верной. Любое переименование потребовало бы правки
  этих строк — как уже приходилось делать в `e4e540dda`.

Итого: `Game` для рантайма, `Game.EditorTools` для редакторного кода, `Game.Sandbox` для кода
тестовых сцен. Удаляются `Game.Gameplay` (45 файлов), `Game.Content` (5), `Modules.Gameplay` (7),
`Modules.Common` (1); четыре файла без неймспейса получают `Game`.

Правка: 50 строк `namespace`, удаление 46 строк `using` (`Game.Gameplay` 14, `Game.Content` 2,
`Modules.Gameplay` 30).

### Сборка `Modules` растворяется

`Assets/Modules` смешивала правила игры с вьюшными хелперами: `Health`, `Ammo`, `Cooldown` рядом с
`TrailView`, `LightAnimation` и мёртвым `Joystick`, а `TriggerEventReceiver` лежал в сборке `Modules`,
но в неймспейсе `Game`. Ничего, кроме `Game`, на эту сборку не ссылается, переносимостью она не
пользуется.

- `Health` → `Features/Vitals/Model/`, `Ammo`, `Cooldown` → `Features/Combat/Model/`
- `AnimationEventReceiver`, `CollisionEventReceiver`, `TriggerEventReceiver` → `Shared/Events/`
- `TrailView`, `LightAnimation` → `Shared/View/`
- `Joystick` → удалить: экранных стиков больше нет, `GetJoystick` никто не читает
- `Game.asmdef` получает `UniTask`, `DOTweenModules`, `UnityEngine.UI` (нужны только `TrailView` и
  `LightAnimation`); `Modules.asmdef` и ссылки на него из `Game.asmdef` и `Game.Tests.asmdef` уходят

### Удаляется

| Что | Почему |
|---|---|
| `Assets/Tools/Playground/` (2 файла + asmdef) | старый сборщик `Tools/Playground/Rebuild Scene` делает сцену, которую просили удалить |
| `Gameplay/Level/PlaygroundOverlay.cs` | не используется ни в одной сцене (проверено по GUID) |
| `Modules/Joystick.cs` | мёртвый |
| `View/MeleeAttack/AttackSoundBehaviour.cs` | закомментирован целиком |
| `View/BodyFall/BodyFallBloodBehaviour.cs` | то же |
| `View/BodyFall/BodyFallSoundBehaviour.cs` | то же |
| `View/TakeDamage/TakeDamageSoundTypeBehaviour.cs` | то же |

Последние четыре — это и есть те «файлы без неймспейса»: они закомментированы от первой строки до
последней, то есть не объявляют ни одного типа, поэтому и неймспейса у них нет. Ни один `.cs` на них
не ссылается, их GUID не встречаются ни в одной сцене и ни в одном префабе. В одном из них был `TODO`
с идеей неповторяющейся случайной дорожки для melee — она остаётся в истории git.

Из-за этого живой `View/Fire/AttackSoundBehaviour.cs` переименовывать не понадобилось: двойника у
него больше нет, а имя точное — его дёргает и зомби на удар, не только стрельба.

### Переименования

| Было | Стало | Почему |
|---|---|---|
| `Modules/Cooldown.cs` → класс `Cooldown` | `CooldownTimer` | см. ниже |
| `Core/Move/MoveUsecase.cs` | `MoveUseCase.cs` | регистр вразрез с остальными use-case; класс внутри уже звался `MoveUseCase` |
| `PlayerContext/Ability/AbillityRunner.cs` | `AbilityRunner.cs` | опечатка в имени файла; класс внутри уже звался `AbilityRunner` |

### Столкновение `Cooldown`

Тринадцать файлов держали строку `using Cooldown = Modules.Gameplay.Cooldown;`. Причина: в
`Atomic.Elements` есть свой `Cooldown` (с `ICooldown`, `RandomCooldown`, `ThreadSafeCooldown`), а в
этих файлах подключены оба неймспейса, так что голое `Cooldown` без алиаса неоднозначно. Схлопывание
неймспейсов вытащило бы конфликт наружу: `Game.Cooldown` оказался бы в том же неймспейсе, что и
обращающийся к нему код, и перебил бы `using Atomic.Elements`.

Поэтому наш тип переименован в `CooldownTimer`, все тринадцать алиасов убраны. Для сериализации это
безопасно: это обычный `[Serializable]`-класс в поле инсталлера, Unity пишет имя поля и его подполя
(`_current`, `_duration`), а не имя типа.

Пять ключей в `EntityAPI` остаются с полным именем — теперь `Game.CooldownTimer`, `Game.Ammo`,
`Game.Health`, `Game.AnimationEventReceiver`. Квалификация там не от неймспейсов, а потому что поле
ключа и тип значения зовутся одинаково: голое `Ammo` в позиции типа компилятор видит как поле.

Отдельно стоит заметить: `Atomic.Elements.Cooldown` функциональнее нашего, и самописный, скорее
всего, лишний. Но замена сбросит выставленные в инспекторе значения, так что это отдельная работа.

### `PlaygroundBuilder` в свою сборку

`Assets/Game/Scripts/Editor/Game.Editor.asmdef` с `includePlatforms: ["Editor"]` и ссылкой на `Game`.
Вложенный asmdef забирает папку себе. Сейчас файл попадает в `Game.csproj`, то есть в рантайм-сборку:
asmdef отменяет соглашение о папке `Editor`, и `UnityEditor` затягивается в игровую сборку.

## Риски и порядок

Главный риск — потерять GUID и тем порвать ссылки из префабов и сцен. Поэтому:

- Переносы только через `AssetDatabase.MoveAsset`, который тащит `.meta` за файлом. Руками по
  файловой системе — нельзя, `.meta` не регенерировать.
- `MoveAsset` требует редактор, а `-executeMethod` работает только с закрытым Unity.
- Проверено, что переносы между сборками безопасны: во всех сценах, префабах и ассетах нет ни одной
  строки с именем типа вида `..., Modules` или `..., Assembly-CSharp`, а `[SerializeReference]` в
  коде проекта не используется вообще. Единственная сериализованная строка типа —
  `Game.WeaponID, Game`, и она не меняется.

Порядок — четыре коммита, каждый со своей компиляцией:

1. Удаления, `Game.Editor.asmdef`, растворение `Modules.asmdef`.
2. Переносы и переименования через `MoveAsset` (на расположение файлов компилятору всё равно, так что
   этот шаг компиляцию не ломает).
3. Неймспейсы и `using`.
4. Компиляция в batchmode, пересборка плейграунда из меню, открытие обеих сцен на проверку ссылок.

Полная карта всех 155 файлов не дублируется в этом документе: она и есть тело скрипта переноса
`Assets/Game/Scripts/Editor/ScriptLayoutMigration.cs`, где её можно и прочитать, и исполнить.

## Результат

| | Было | Стало |
|---|---|---|
| Файлов | 148 + 9 в `Modules` | 152 |
| Папок | 61 | 46 |
| Папок с одним файлом | 20 | 2 (обе отделяют слой) |
| Максимальная вложенность | 8 | 7 |
| Неймспейсов | 6 | 3 |
| Файлов без неймспейса | 4 | 0 |
| Сборок | `Game`, `Modules`, `Game.Tests`, `Game.Playground.Editor` | `Game`, `Game.Editor`, `Game.Tests` |

Прогон миграции: `deleted=7 moved=150 skipped=0 failed=0 prunedFolders=60`, ноль ошибок компиляции.

GUID никто не потерял — проверено сравнением всех `*.cs.meta` с состоянием в `HEAD`: было 161, стало
154, исчезли ровно восемь, и каждый из восьми принадлежит удалённому файлу. Плюс отдельная проверка
со стороны данных: среди GUID, на которые ссылаются обе сцены, префабы и ассеты, нет ни одного
скриптового, которого больше нет в проекте.

## Как проверяем

- `Game.csproj` больше не содержит `PlaygroundBuilder.cs`.
- Компиляция без ошибок и предупреждений о дубликатах типов.
- `GameScene.unity` и `Playground.unity` открываются, в консоли нет `MissingReference` и
  «The referenced script is missing».
- `Tools/Playground/Build` пересобирает сцену и проставляет те же компоненты.
- `git status` не показывает удалённых `.meta` без пары — то есть ни один GUID не потерян.

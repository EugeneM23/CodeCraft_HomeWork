# Правила порядка в проекте

**Дата:** 2026-11-17
**Статус:** действует. Согласовано с пользователем 2026-11-17.
**Владелец:** команда «Производительность и порядок».
**Проверяет:** агент `perf-check` (`/perf-check`, см. `Docs/Rules/README.md`).

Правила собраны из того, что уже принято в коде прототипа (обход 2026-11-17), и из общего плана `Docs/superpowers/specs/2026-11-17-game-production-plan.md`. Новое помечено **(новое)**.

**Старое и новое.** Правила обязательны для новых и изменённых файлов. Старые нарушения агент записывает в отчёт как долг с числом, и долг должен только уменьшаться. Ветку блокируют только нарушения в файлах и строках, которые она добавила или изменила. Переименование старых ассетов и файлов — работа команды чистки или владельца папки, а не попутная правка.

## 1. Папки

```
Assets/
  Modules/<Name>/            модули Platformer как есть, с package.json и asmdef
  Game/
    Scripts/
      App/                   AppContext, поток игры, загрузка уровней, инсталлеры
      Adapters/<Модуль>/     сборки-адаптеры модулей Platformer: Game.<Модуль>.Adapters (ядро — GameLoop, Loading, SaveSystem)
      Contexts/{Game,Player} контексты уровня и игрока (уже есть)
      Declarations/          общий EntityAPI.cs и ключи прототипа (уже есть)
      UI/                    экраны и их логика
      Features/<Фича>/       игровые фичи: танк, камеры, здоровье, грязь...
      Features/AI/           адаптеры AIKit, мозги монстров
      Shared/<Тема>/         общий код без своей фичи: Audio, Events, Physics, Spawning, Timers, Vfx
      Editor/                редакторский код игры (своя сборка)
    Horde/                   подсистема орды: Runtime, Tests, своя сборка
    Voxels/                  воксельный модуль: Runtime, Editor (в нём Voxelizer), Demo, Tests, Shaders, Resources, Scenes
    Tests/                   тесты сборки Game; повторяет дерево Scripts/
    Audio/                   звук: Banks/, Events/, Clips/<банк>/<ключ>/, Resources/, микшер; Scripts/ — адаптер AudioEngine (своя сборка), Tests/
    Scenes/                  Boot, MainMenu, Hangar, Levels/<Уровень>/...
    Animations/ Configs/ Fonts/ Materials/ Models/ Prefabs/ Shaders/ Sprites/ Textures/
                             ассеты по типу, внутри — папки по фиче или объекту
  Plugins/ 3rdParty/ TextMesh Pro/
                             сторонний код. Не правим, кроме исправления ошибок, с пометкой в коммите
Docs/
  Rules/                     эти правила
  Reports/                   отчёты агента perf-check и perf-history.jsonl
  Game/                      геймдизайн-документ
  AtomicGuide/               гайд по Atomic
  superpowers/specs/, plans/ спецификации и планы
Tools/<Инструмент>/          скрипты вне Unity (Maya, PerfCheck)
.claude/agents/, skills/     агенты и команды Claude Code проекта
```

**Внутри фичи** действует `Docs/superpowers/specs/2026-11-06-installers-in-features.md`:
- фича делится по объекту, потом по части: `Features/Controllables/Tank/{Drive,Gun,Suspension}`;
- инсталлер лежит в корне папки того объекта, который он собирает;
- общий для группы код — в `Common/`;
- представление — в подпапках `Animation/`, `Audio/`, `Vfx/`, `UI/`;
- папки `Entities/` нет;
- маленькая фича остаётся плоской: `Features/Mud/`.

**Запрещено:**
- файлы в корне `Assets/` (сейчас там `TestAnim.cs` и `TestAnimIK.cs` — долг);
- в корне репозитория — мусор вроде `mono_crash.*.json`. Папка `.idea/` в git не нужна **(новое)**;
- папки-опечатки и служебные имена: `{Aspects`, `New Folder`, `New Material.mat`, `[Archive]`, `Copy of`, `… 1`, `(1)`;
- пустые папки (Unity держит для них `.meta`);
- `.meta` без ассета и ассет без `.meta`.

## 2. Код

### Сборки (asmdef)

| Сборка | Папка | rootNamespace | Платформы |
|---|---|---|---|
| `Game` | `Game/Scripts` | `Game` | все |
| `Game.Editor` | `Game/Scripts/Editor` | `Game.EditorTools` | Editor |
| `Game.Tests` | `Game/Tests` | `Game.Tests` | Editor (EditMode) |
| `Game.Voxels` | `Game/Voxels/Runtime` | `Game.Voxels` | все |
| `Game.Voxels.Editor` | `Game/Voxels/Editor` | `Game.Voxels.EditorTools` | Editor |
| `Game.Voxels.Demo` | `Game/Voxels/Demo` | `Game.Voxels.Demo` | все |
| `Game.Voxels.Tests` | `Game/Voxels/Tests` | `Game.Voxels.Tests` | все |
| `Game.Voxels.SceneTests` | `Game/Voxels/Tests/Scene` | `Game.Voxels.Tests` | Editor (PlayMode-тесты сцены) |
| `Game.Voxels.VoxelizerTests` | `Game/Voxels/Tests/Voxelizer` | `Game.Voxels.Tests` | Editor (EditMode-тесты вокселизатора) |
| `Game.AudioEngine.Adapters` | `Game/Audio/Scripts` | `Game.AudioEngine.Adapters` | все |
| `Game.AudioEngine.Adapters.Editor` | `Game/Audio/Scripts/Editor` | `Game.AudioEngine.Adapters.EditorTools` | Editor |
| `Game.AudioEngine.Adapters.Tests` | `Game/Audio/Tests` | `Game.AudioEngine.Adapters.Tests` | Editor (EditMode) |
| `Game.GameLoop.Adapters` (и другие модули ядра) | `Game/Scripts/Adapters/<Модуль>` | `Game.<Модуль>.Adapters` | все |
| `Game.App.SceneTests` | `Game/Tests/App/PlayMode` | `Game.Tests` | все (PlayMode-тесты потока игры) |

- **Новая сборка** называется `Game.<Подсистема>`, её части — `Game.<Подсистема>.Editor`, `.Tests`, `.Adapters`. Пример — `Game.Horde`. `rootNamespace` равен имени сборки, а у редакторских — `….EditorTools`, чтобы не спорить с `UnityEditor`.
- **Модуль** в `Assets/Modules/<Name>` сохраняет свои сборки как есть. Всё про нашу игру лежит в отдельной сборке-адаптере `Game.<Name>.Adapters` рядом с кодом игры, а не внутри модуля: `Game/Scripts/Adapters/<Name>/` или, у подсистемы со своей папкой, в ней (`Game/Audio/Scripts`).
- Рантайм-сборка не ссылается на редакторские. `using UnityEditor` в рантайм-коде — только внутри `#if UNITY_EDITOR`.
- Тяжёлая подсистема без зависимостей от `Game` (воксели, орда) — своя сборка. Она ссылается только на Burst, Collections и Mathematics, а `Game` ссылается на неё, но не наоборот.
- Тестовые сборки: `UNITY_INCLUDE_TESTS` в `defineConstraints`, `autoReferenced: false`.

### Пространства имён

- Пространство имён равно `rootNamespace` сборки и **не повторяет папки**: весь код сборки `Game` лежит в `namespace Game`. Так устроены все 455 файлов прототипа.
- Каждый файл объявляет пространство имён. Код без пространства имён (`Assets/TestAnim.cs`) — долг.

### Файлы и типы

- **Один верхнеуровневый тип на файл, имя файла равно имени типа.**
  - Вложенные типы не считаются.
  - Исключение — файлы-наборы Burst-задач и простых структур одной подсистемы: `VoxelJobs.cs`, `VoxelTypes.cs`. Новые такие файлы называются `<Подсистема>Jobs.cs`, `<Подсистема>Types.cs`.
  - Сейчас 12 файлов держат лишние типы, например `GameContextInstaller.cs` + `EnemyController`. Это долг.
- Имена файлов и папок в коде — PascalCase, латиница, без пробелов.

### Имена типов

Принятые суффиксы — использовать их, а не придумывать синонимы:

| Суффикс | Что это | Сейчас |
|---|---|---|
| `…Behaviour` | поведение Atomic-сущности (`IEntityInit`, `IEntityTick`…) | 72 |
| `…Installer` | инсталлер сущности или контекста | 48 |
| `…UseCase` | статический алгоритм без состояния | 36 |
| `…Controller` | контроллер контекста игрока | 14 |
| `…Config` | ScriptableObject с настройками | 14 |
| `…Builder` | редакторский сборщик префаба | 12 |
| `…Context` | Atomic-контекст (`AppContext`, `GameContext`) | 4 |
| `…API` | ключи сущностей, `[EntityExtensionsAPI]` | 4 |
| `…View` | отображение без логики | 2 |
| `…Job` | Burst-задача | |
| `…Pool`, `…Factory`, `…Settings`, `…Buff`, `…Math` | по смыслу | |
| `…Tests` | класс тестов типа `<Тип>` | |

- Предметные типы без суффикса — нормально: `Health`, `TankEngine`, `VoxelWorld`.
- Интерфейсы начинаются с `I`.
- Новых суффиксов `…Manager`, `…Helper`, `…Utils`, `…System` (кроме Atomic `EntitySystem`) не вводим **(новое)**. Вместо них — `UseCase` или предметное имя.

### Поля и константы

| Что | Стиль | Пример |
|---|---|---|
| приватное и `[SerializeField]` поле | `_camelCase` | `_shotsToClean` |
| приватное статическое поле | `s_camelCase` | `s_hits` |
| `static readonly` | PascalCase | `VoxelLayers` |
| `const` | `UPPER_SNAKE` | `FULL_STICK` |
| публичное поле `[Serializable]`-класса данных | camelCase | `minSpeed` |
| свойство, метод, событие | PascalCase | |
| локальная переменная, параметр | camelCase | |

Префикса `m_` нет. У `[SerializeField]` с неочевидным смыслом есть `[Tooltip]`. Рядом в коде такие тултипы уже есть.

### Ключи сущностей

Каждая команда объявляет свои ключи в файле `<Команда>API.cs` с `[EntityExtensionsAPI]` внутри своей папки, по образцу `Declarations/AbilityAPI.cs` и `GameContextAPI.cs`. Общий `Declarations/EntityAPI.cs` правит только команда чистки.

### Комментарии и язык

Код, имена и комментарии — на английском. Документы — на русском.

## 3. Ассеты

### Префиксы (новое)

Для **новых** ассетов. Старые без префикса — долг, переименовывает команда чистки или владелец папки.

| Тип | Префикс | Пример |
|---|---|---|
| материал | `MT_` | `MT_Tank_Body` |
| текстура | `T_` | `T_MudClump`, `T_Tank_Body_N` (нормаль `_N`, маска `_M`) |
| статичный меш | `SM_` | `SM_House_01` |
| скелетный меш | `SK_` | `SK_Regax` |
| Animator Controller и Override | `AC_` | `AC_BaseCharacter` |
| эффект (префаб частиц, VFX) | `FX_` | `FX_RocketExplosion` |
| воксельный префаб | `VX_` | `VX_House_01` |
| материал эффекта | `MT_FX_` | `MT_FX_Smoke` |

**Без префикса** (PascalCase):
- **Префабы** геймплея, вариант через `_NN`: `Tank_01`, `Regax_Gibs`.
- **Конфиги** — `<Имя><Тип>Config.asset`: `DashAbilityConfig`.
- **Сцены**: `VoxelSandbox`, `MainMenu`.
- **Анимации** — `<Субъект>_<Действие>[_NN]`: `Zombie_Attack`.
- **Шейдеры** — `Shaders/<Фича>/<Имя>.shader`, имя шейдера в коде — `Game/<Имя>`.

### Имена

- Латиница, цифры, `_`. **Без пробелов, скобок и точек в середине.** Сейчас таких 25+, например `Gunshot 5-5.wav` и `SM_Prop_Cage_Cart_Door_01 (1).prefab`. Это долг.
- Без опечаток и дублей: `Outiline.mat`, `AudioSurce.prefab`, `1.png` — долг.
- Номер варианта — `_01`, `_02`, а не ` 1` или ` (1)`.

### Импорт

- Текстуры: размер не больше нужного. Для UI и эффектов — до 1024, для атласов — до 2048. 4096 — только с объяснением в спецификации.
- У мешей Read/Write выключен, если меш не читается из кода (вокселизатор читает — для его входных моделей можно).
- Звук: короткие клипы — Decompress On Load, музыка — Streaming. Правила банков пишет команда звука.

### Лишнее

- Ассет без ссылок из сцен в Build Settings, префабов, `Resources/` и кода считается кандидатом на удаление. Агент приводит их в отчёте списком. Удаляет команда чистки после подтверждения.
- Сторонние паки держим только целиком нужные. Демо-сцены и примеры паков (`Demo/`, `Examples/`) из проекта убираются.

## 4. Тесты

- Тесты сборки `Game` лежат в `Game/Tests/` и повторяют дерево `Scripts/`: `Tests/Features/Controllables/Tank/Gun/...`.
- Тесты подсистемы со своей сборкой лежат рядом с ней: `Voxels/Tests`, `Horde/Tests`.
- Класс тестов — `<Тип>Tests.cs`. Сценарные тесты сцены — `<Сцена><Что>Test.cs`, например `SandboxVoxelSmokeTest`.
- Имя теста описывает поведение: `Fast_TheTankCrushesTheEnemy_AndBloodSpraysFromUnderIt`.
- EditMode и PlayMode разделяются **сборками**, а не папками. PlayMode-тесты сцены — в отдельной сборке `….SceneTests`, только Editor.
- Новая фича приходит с тестами. Тесты в общем редакторе не запускаются, пока в нём открыта несохранённая сцена.

## 5. Документы

- Документы на русском, идентификаторы и пути — в обратных кавычках.
- Спецификации: `Docs/superpowers/specs/YYYY-MM-DD-<тема>.md`. Планы: `Docs/superpowers/plans/YYYY-MM-DD-<тема>.md` с той же датой и темой, что у спецификации.
- В шапке спецификации: `**Дата:**`, `**Статус:**`. Дальше разделы по смыслу: задача, что было, решение, правила, риски, как проверяем.
- Правила — `Docs/Rules/<Тема>.md`. Отчёты агента — `Docs/Reports/YYYY-MM-DD-<что>.md`.
- Геймдизайн — `Docs/Game/`.
- Даты абсолютные (`2026-11-17`), не «вчера» и не «на прошлой неделе».

## 6. Git

- Ветка команды — `game/<команда>`, интеграция — `game/develop`.
- Коммитятся только свои пути (`git add <пути>`), без `git add -A`.
- Сообщение коммита: `<Область>: <что сделано>` по-английски, например `Spec: ...`, `Voxels: ...`, `Rules: ...`. В конце — строка `Co-Authored-By`, которую даёт системное напоминание сессии.
- Ничего не пушить без просьбы пользователя.
- Крупные бинарные файлы (больше 50 МБ) в git не кладутся без согласия пользователя.

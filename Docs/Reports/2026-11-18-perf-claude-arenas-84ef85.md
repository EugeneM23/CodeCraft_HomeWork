# Проверка производительности и порядка: claude/arenas-84ef85

**Дата:** 2026-11-18 05:11–05:30 **Коммит:** `7464a40d7` **База:** `dc272cb08` (`game/develop` на старте ветки) **Редактор:** свой, порт 7803 (`projectPath` = эта рабочая копия), Unity 6000.3.28f1, URP, качество **Ultra**. Экран и Scene view не снимались: замер не проводился (раздел «Замер»).

Ветка команды «Арены»: генератор арен главы 1, норы, ворота, пять арен вехи 1. Кроме того, в ветке лежат контракты забега ядра (`2da27bbf8`, взяты из `claude/core-run` без правок), а в `VoxelStructure` добавлены `Configure` и `BuildAll`.

## Итог

**Вливать нельзя: блокирует порядок.** Это 4 подтверждённых новых нарушения правил `Docs/Rules/Project.md`, каждое исправляется за минуты:
1. Материал `M_ArenaBackdrop.mat` назван без префикса `MT_`.
2. В `ArenaDefinition.cs` три верхнеуровневых типа.
3. В `ArenaPlan.cs` пять верхнеуровневых типов.
4. Пространства имён `Game.Arenas`, `Game.EditorTools.Arenas` и `Game.Tests.Arenas` внутри сборок `Game`, `Game.Editor` и `Game.Tests`.

Производительность **не проверена**. Машина 11 минут была загружена на 76–100 % (по правилам нужно ниже 25 %), поэтому бенчмарк песочницы и сценарий арен пропущены. Нужен перезамер на тихой машине. Скрипт для сценария арен готов (раздел «Сценарий команды»).

## Замер

**Пропущен.** Правило — `Performance.md`, раздел 4, п. 3: замер при нагрузке выше 25 % недействителен.

Загрузку CPU я проверял с 05:12:55 по 05:23:45, раз в 30 с. Всё время было 76–100 %, ни одного замера ниже 76 %. Мой редактор (PID 80300) не был в Play и занимал 0.1 %.

Нагрузку давали другие редакторы:

| PID | Рабочая копия | Что делал | CPU машины |
|---|---|---|---|
| 64412 | `core-95555d` | batchmode, PlayMode-тесты | ~13 % |
| 77316 | `ai-c4884e` | batchmode, PlayMode-тесты `Game.AIKit.Adapters.SceneTests`, `Game.Voxels.SceneTests` | ~16 % |
| 31752 | `structure-0ac902` | batchmode, PlayMode-тесты `Game.Voxels.Tests`, `Game.Voxels.SceneTests` | ~15 % |
| 51668 | `tank-crater-112bdb` | редактор (порт 7805) | ~15 % |
| 37616 | `ui-b8b803` | batchmode, PlayMode-тесты; запущен во время ожидания | — |

Что в итоге не делалось:
- Play в редакторе 7803 не запускался.
- Сцена `VoxelSandbox` осталась открытой и не изменённой: не в Play, `dirty=False`.
- Профайлер не включался.

**Чего ждать от песочницы.** В песочнице ветка меняет только `VoxelStructure.Build()`. Раньше задача шла через `BuildVoxelsJob.Run()`, теперь через `Schedule()` и сразу `Complete()`. Это та же синхронная сборка при загрузке, только на рабочем потоке. Остальной код ветки в `VoxelSandbox` не работает. Поэтому изменения 5 стандартных сценариев (idle, drive, airstrike, crush, crater) от ветки я не жду, но **это не проверено**.

**Эталон для будущего сравнения.** Последняя строка `game/develop` в `perf-history.jsonl` — `2faa7b274`, снятая на **Built-in**. Замера `game/develop` на URP после влития debris и structure в истории нет. Перед перезамером веток вехи 1 нужен замер самого `game/develop` (владелец — команда «Производительность и порядок», по просьбе тимлида).

## Сценарий команды: арены главы 1

**Не выполнен:** причина та же, нагрузка машины.

Скрипт сценария готов, перезапуск — одной командой. Он лежит в `C:\Users\MYPC~1\AppData\Local\Temp\claude\4878b0043f9a588e\b94600ce-b271-4660-9c78-fafa95f2ccc6\scratchpad\arena_bench.cs`. Это временная папка сессии; если сценарий нужен надолго, его стоит положить в `Tools/PerfCheck/Bench/arena_bench.cs` (владелец — производительность).

Что делает скрипт (запускать в Play сцены `Chapter1_Arena`, после того как арена 1 построилась сама):
- **Сборки.** Через 1, 6, 11, 16 и 21 с вызывает `Build(Chapter.At(n), 500 + n)` для n = 1..5, внутри PlayerLoop (`Application.onBeforeRender`). Для каждой сборки записывает:
  - `LastBuildMs`;
  - PlayerLoop кадра сборки;
  - маркеры `Arena.Build`, `Arena.BuildVoxels`, `Arena.BuildProps`, `VoxelBody.Init`, `JobHandle.Complete`.
- **После сборки.** Окно 3 с после кадра сборки: avg, p95, худший кадр PlayerLoop и горячие пути трёх худших кадров.
- **Бой на арене 5.** Через 26, 27 и 28 с открывает норы (`TryOpenBurrow`), через 29–32 с делает 4 взрыва у первого дома (`VoxelDamage.Blast(..., 2.2f, 8f)`). Окно 26–34 с: avg, p95, худший кадр, GC по кадрам, горячие пути.
- Результат читается из `AppDomain` по ключу `arenabench_result`.

Бюджеты для перезамера:
- сборка — меньше 300 мс на арену (спецификация `2026-11-18-arenas.md`, раздел про сборку под шторкой);
- кадры после сборки и в бою — обычный бюджет кадра: PlayerLoop avg ≤ 5.0, p95 ≤ 6.5, max ≤ 16 мс, GC события ≤ 4 КБ.

## Статическая проверка

`Tools/PerfCheck/static_check.sh --base dc272cb08`, коммит `7464a40d7`. На момент проверки незакоммиченных изменений не было.

### Новые нарушения

| # | Где | Правило | Что сделать |
|---|---|---|---|
| 1 | `Assets/Game/Materials/Arenas/M_ArenaBackdrop.mat` | `asset-prefix`: префикс материала — `MT_` (Project.md, раздел 3) | Переименовать в `MT_ArenaBackdrop.mat` (вместе с `.meta`) и поправить `ArenaChapterBuilder.MATERIAL_PATH` (`Assets/Game/Scripts/Editor/Arenas/ArenaChapterBuilder.cs:21`) |
| 2 | `Assets/Game/Scripts/Features/Arenas/ArenaDefinition.cs:7,21,34` | Один верхнеуровневый тип на файл (Project.md, раздел 2): `ArenaLayout`, `ArenaHint`, `ArenaDefinition` | Вынести перечисления в `ArenaLayout.cs` и `ArenaHint.cs` |
| 3 | `Assets/Game/Scripts/Features/Arenas/ArenaPlan.cs:8,25,36,49,66` | То же: `HousePlan`, `TreePlan`, `FencePlan`, `GatePlan`, `ArenaPlan` | Простые структуры перенести в `ArenaTypes.cs`: это исключение по правилу `<Подсистема>Types.cs`. `ArenaPlan` оставить в своём файле |
| 4 | 9 файлов `Features/Arenas/*.cs` (все, кроме `ArenaAPI.cs`), `Editor/Arenas/ArenaChapterBuilder.cs:9`, `Tests/Features/Arenas/ArenaPlannerTests.cs:6` | Пространство имён равно `rootNamespace` сборки и не повторяет папки (Project.md, раздел 2): `Game.Arenas` в сборке `Game`, `Game.EditorTools.Arenas` в `Game.Editor`, `Game.Tests.Arenas` в `Game.Tests` | Перейти на `namespace Game`, `Game.EditorTools` и `Game.Tests`. Или получить у пользователя исключение и записать его в правила. Спецификация арен выбрала `Game.Arenas` сама, но спецификация правила не отменяет |

Нарушения 2–4 скрипт не ловит. Я нашёл их при ручном обзоре и проверил по коду.

### Ложные срабатывания

Нет.

### Долг по правилам

Сравнение с прошлой строкой `static` в истории: `game/debris` `f07b35a6f`. Строки `game/develop` на текущей базе нет.

| Правило | Было | Стало | Откуда рост |
|---|---|---|---|
| `camera-main` | 9 | 10 | из базы: NEW ветки debris стал долгом после влития |
| `debug-log` | 0 | 1 | из базы: `VoxelBody.Structure.cs:254` (`[Solve]`, ветка structure) |
| `file-type-name` | 2 | 8 | из базы: `VoxelDebris.Cells.cs`, `VoxelDebris.Earth.cs`, `VoxelBody.Structure.cs` и др. (влитые debris и structure) |
| `asset-prefix` | 29 | 30 | из базы (влития после `f07b35a6f`) |
| `unused-assets` | 12 | 14 | из базы: `MT_Bullet_01.mat`, `MT_Rocket_01.mat` (эксперименты с выстрелом) |
| остальные | = | = | |

Весь рост долга — **ВНИМАНИЕ (база)**, ветка арен его не добавила. Скрипт считает долгом только то, что было до ветки, а файлов из этого списка ветка не трогала. Общий долг — 129.

## Ручной обзор изменений

120 файлов, +5198 / −6. Код ветки работает при сборке арены, под шторкой; в тике ничего нет.

**Тик и аллокации — чисто.**
- `ArenaService`: `Update` нет. `Start` — корутина на 2 кадра. Ворота срабатывают через `OnTriggerEnter`.
- В `TryPickBurrow` и `OpenBurrow` аллокаций нет.
- `ArenaGate` и `ArenaFence` не тикают.
- `ArenaBuilder` аллоцирует (интерполяция имени корня, лямбда с захватом в `AddFence`), но только при сборке.

**Instantiate и Destroy.**
- `ArenaService.TakeDown` вызывает `Destroy` у отколотых тел и `DestroyImmediate(_root)`.
- Это смена арены под шторкой, а не бой, поэтому правило пулов не нарушено.
- `DestroyImmediate` входит в `LastBuildMs`. При перезамере его долю стоит посмотреть отдельно.

**`Complete()` в том же кадре.** `VoxelStructure.BuildAll` ставит все задачи, вызывает `ScheduleBatchedJobs` и ждёт их по очереди. Задачи сборки идут параллельно, и это то, что нужно при загрузке. Комментарий у метода объясняет, почему тело нужно сразу. Нарушения нет.

**Циклы не в Burst.** `ArenaFence.Fill` заполняет воксели забора на главном потоке. Забор 8 м — это примерно 80 × 13 × 3 ≈ 3 тыс. вокселей, а на арене их около десятка и больше. Правило про ~1000 элементов относится к кадру боя. Сборка идёт под шторкой, так что это **ВНИМАНИЕ**, не блок: если `Arena.BuildProps` окажется заметной долей от 300 мс, заполнение заборов нужно перевести в Burst-задачу рядом с `BuildVoxelsJob`.

**Контракты ядра (`2da27bbf8`).** `EnemyDeaths.Report` (`Assets/Game/Scripts/App/Run/EnemyDeaths.cs:24`) вызывает `GetInvocationList()` на каждую смерть врага, а это новый массив. В давке орды смертей много за кадр, так что это GC в кадре события. При 60 смертях и 2–3 слушателях выходит порядка 3 КБ при бюджете 4 КБ. **ВНИМАНИЕ**, владелец — ядро: держать массив слушателей и пересобирать его в `add`/`remove`.

**Два ассета на одну арену.** Ядро завело `ArenaConfig` (`App/Catalog`) с комментарием «команда арен добавляет сюда настройки генератора». Арены вместо этого сделали свой `ArenaDefinition`. Теперь `ArenaId` арены лежит в двух ScriptableObject. **ВНИМАНИЕ:** договориться с ядром, какой из них главный, иначе id разойдутся.

**Имена конфигов.** `Arena_01..05.asset` и `Chapter1_Arenas.asset` не следуют форме `<Имя><Тип>Config.asset`. Это допустимо, только если `ArenaDefinition` и `ArenaChapter` считать предметными типами, а не конфигами. Замечание, не блок; решится вместе с пунктом про два ассета.

**Тесты.** `ArenaPlannerTests` (8 тестов EditMode) покрывает планировщик: детерминизм по seed, дома, раскладки, норы, ворота, высоты. Сборка в сцене не покрыта: `ArenaBuilder`, `ArenaService` (смена арены, `TakeDown`, норы), `ArenaGate` (триггер выхода), а также `VoxelStructure.BuildAll` и `Configure`. **ВНИМАНИЕ:** нужен хотя бы один PlayMode-тест «построить арену 1 → арену 2: старая снесена, танк у входа, ворота закрыты».

**Папки и сборки.**
- Папки — по Project.md, раздел 1: `Features/Arenas`, `Editor/Arenas`, `Tests/Features/Arenas`, `Configs/Arenas/Chapter1`, `Scenes/Levels/Chapter1`.
- Новых asmdef нет. В `Game.Tests` добавлена ссылка на `Game.Voxels`; это согласуется с таблицей.
- `ProjectSettings/EditorBuildSettings.asset` изменён: добавлена сцена `Chapter1_Arena`. Раздел 4 плана разрешает менять `ProjectSettings/` только ядру и рендеру, остальные просят тимлида. **ВНИМАНИЕ:** согласовать с тимлидом.

**Изменения во время проверки.** Пока шла проверка, в рабочей копии появились незакоммиченные правки: `ArenaBuilder.cs`, `ArenaPlanner.cs`, `ArenaService.cs`, `VoxelStructure.cs`, `SaveSystemSettings.asset`. Отчёт о коммите `7464a40d7`; эти правки не проверены.

## Что сделать

1. **Арены.** Переименовать `M_ArenaBackdrop.mat` → `MT_ArenaBackdrop.mat`, поправить `ArenaChapterBuilder.MATERIAL_PATH`. Блок.
2. **Арены.** Разнести типы: `ArenaLayout.cs`, `ArenaHint.cs`, `ArenaTypes.cs` (структуры планов). Блок.
3. **Арены.** Пространства имён `Game` / `Game.EditorTools` / `Game.Tests` вместо `Game.Arenas` / `Game.EditorTools.Arenas` / `Game.Tests.Arenas`, либо исключение от пользователя. Блок.
4. **Арены вместе с производительностью.** Перезамер на тихой машине (фон < 25 %, лучше < 10 %): 5 сценариев песочницы и сценарий арен скриптом выше. Отдельно показать `LastBuildMs` против 300 мс, `Arena.BuildVoxels` / `Arena.BuildProps`, кадры через 3 с после сборки и бой на арене 5.
5. **Производительность.** Замер `game/develop` на URP: без него эталона для веток вехи 1 нет.
6. **Арены.** PlayMode-тест смены арены, а также тест `VoxelStructure.BuildAll`.
7. **Арены вместе с ядром.** Один источник данных арены: `ArenaConfig` ядра или `ArenaDefinition` арен.
8. **Ядро.** `EnemyDeaths.Report` без `GetInvocationList()` на каждую смерть.
9. **Арены.** Согласовать с тимлидом правку `EditorBuildSettings.asset`.
10. **Арены, если замер покажет.** Заполнение заборов (`ArenaFence.Fill`) в Burst-задачу.

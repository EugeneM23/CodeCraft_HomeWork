# Проверка производительности и порядка: game/structure

**Дата:** 2026-11-17 16:28–16:55 **Коммит:** `fd73a65b4` (плюс незакоммиченный сценарий `house` в `Tools/PerfCheck/Bench/perf_bench.cs`) **База:** `game/develop` `d2fbd6a04`, merge-base `619ca415d` **Редактор:** 6000.3.28f1, порт 7801 (рабочая копия ветки), URP, качество **Ultra**, окно Game 1117×628, Scene view открыт (`sceneViews` 1, `maximized: false`), Burst включён. Загрузка CPU без своего редактора в зачётных прогонах — 7–17 %.

## Итог

**Нельзя вливать.** Блокируют:
- худший кадр в `airstrike` (22.2 мс против 12.9 у develop) и в `house` (19.4 против 15.0);
- среднее PlayerLoop в `house`: 5.21 мс при бюджете 5.0 (у develop 4.44);
- рост пика GC в кадре события в `airstrike` и `house` (перф-долг, хуже develop больше чем на 10 %).

Причина у всех одна: решение нагрузки здания (`VoxelWorld.SolveStructures` → `VoxelBody.SolveStructure`) идёт синхронно на главном потоке в `VoxelWorld.LateUpdate`, 10–27 мс в кадре обрушения.

Статика: два подтверждённых новых нарушения, оба исправляются в одну строку: `Debug.Log` без пометки и поле `WorstSlot`.

## Замер

### Как мерили

- Каждый сценарий шёл в **свежем Play**, по одному сценарию на Play (`perfbench_only`).
- **Develop** мерили в этом же редакторе. Рантайм и тесты вокселей временно вернули к merge-base `619ca415d` (`git apply -R` диффа ветки по `Assets/Game/Voxels/Runtime` и `Assets/Game/Voxels/Tests`), перекомпилировали вне Play, прогнали все пять сценариев, затем восстановили через `git checkout HEAD -- …`. После этого `git status` по `Assets/Game/Voxels` чист, сборка ветки загружена: `SetStrength` есть, ошибок компиляции нет.
- Почему merge-base, а не рантайм `game/develop`:
  - в `game/develop` после merge-base в `Voxels/Runtime` пришёл только вокселизатор: `VoxFile`, `VoxelModel*`, `VoxelPaint`, перенос из `VoxelizedModel`, `VoxelBody.ReserveStains`;
  - всё это не на горячем пути песочницы;
  - зато эти файлы ломают компиляцию с `Editor` и `Tests` ветки.
  - Ниже такой замер называется «develop».
- **Повторы.** `airstrike` и `house` прогнаны повторно: develop один раз, ветка дважды.
  - Повторы шли при загрузке 30–65 %: основной редактор 7800 был занят, около 3.7 ядра. Такие замеры недействительны (Performance.md, раздел 4, п. 3), в таблицу они не идут.
  - Разброс PlayerLoop max:
    - ветка: `airstrike` 22.2 / 38.0 / 28.6, `house` 19.4 / 37.1 / 31.0;
    - develop: `airstrike` 12.9 / 20.3, `house` 15.0 / 33.4.
  - Во **всех девяти** худших кадрах ветки в `airstrike` и `house` стоит `VoxelWorld.LateUpdate` на 10–27 мс. У develop его нет ни в одном худшем кадре, поэтому вывод о причине от шума не зависит.
- Сравнение с историей: последняя строка `game/develop` (`2faa7b274`) снята на Built-in, при окне 2294×1291 и Maximize On Play. Ближайшая строка на URP — ветка вокселизатора `7563c1f76`. Обе для сравнения не годятся, поэтому ориентир — свой замер develop. Прошлых строк `game/structure` нет.
- В Main Thread входит Scene view: он открыт, Maximize On Play выключен. На PlayerLoop это не влияет. GPU и счётчики рендера сняты на окне 1117×628, а не на 1080p, поэтому GPU-бюджет здесь не проверяется: везде меньше 1 мс.

### Таблица (ветка / develop, мс; GC — пик за кадр)

| Сценарий | PlayerLoop avg | p95 | max | Main avg* | GC игры | Воксели avg (max) | Физика | Скрипты | Рендер | GPU | Batches | Shadow casters | Вердикт |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Бюджет / ориентир | ≤ 5.0 | ≤ 6.5 | ≤ 16 | — | 0 Б (idle, drive); ≤ 4 КБ (событие) | 1.0 | 0.75 | 0.75 | 2.0 | ≤ 4.0 | 1000 | 500 | |
| idle | 3.37 / 3.48 | 4.31 / 4.23 | 5.09 / 4.62 | 7.70 / 7.73 | 288 Б в 10 кадрах / так же | 0.13 / 0.18 | 0.21 / 0.21 | 0.46 / 0.46 | 1.58 / 1.62 | 0.59 / 0.55 | 682 / 697 | 247 / 254 | ОК; GC — **ДОЛГ** (есть и у develop) |
| drive | 4.25 / 4.42 | 5.74 / 5.83 | 49.99 / 47.01 | 8.59 / 8.87 | 683 КБ, 23 кадра / 670 КБ, 17 кадров | 0.48 (10.5) / 0.48 (9.4) | 0.31 / 0.33 | 0.58 / 0.60 | 1.87 / 1.96 | 0.48 / 0.49 | 837 / 836 | 322 / 321 | **ДОЛГ**: max +6 %, GC как у develop |
| airstrike | 4.01 / 3.96 | 5.57 / 4.68 | **22.24 / 12.94** | 8.43 / 8.45 | **209 КБ / 31 КБ** | 0.38 (14.7) / 0.28 (3.5) | 0.29 / 0.23 | 0.54 / 0.56 | 1.71 / 1.78 | 0.51 / 0.51 | 708 / 730 | 257 / 270 | **БЛОК**: max вышел за 16 мс, GC события хуже долга develop в 6.7 раза. ВНИМАНИЕ: p95 +19 %, воксели +36 % |
| crush | 4.69 / 5.48 | 6.73 / 8.73 | 44.60 / 50.29 | 9.16 / 10.58 | 671 КБ / 682 КБ | 0.67 (10.4) / 0.77 (9.8) | 0.45 / 0.56 | 0.63 / 0.78 | 1.92 / 2.21 | 0.48 / 0.53 | 836 / 847 | 315 / 318 | **ДОЛГ**: p95, max и GC за бюджетом, но лучше develop |
| house (новый) | **5.21 / 4.44** | 6.41 / 5.39 | **19.39 / 15.04** | 10.03 / 9.45 | **96 КБ / 39 КБ** | 0.62 (11.2) / 0.40 (4.6) | 0.52 / 0.28 | 0.58 / 0.60 | 2.30 / 1.99 | 0.60 / 0.59 | 979 / 754 | 388 / 279 | **БЛОК**: avg и max вышли за бюджет, GC события ×2.5. ВНИМАНИЕ: p95 +19 %, рендер за ориентиром 2.0, треугольники 2.02 млн за ориентиром 2 млн, batches +30 %, тени +39 % |

\* Main Thread включает Scene view.

**house, что произошло со зданием.** В здании 48 492 вокселя.
- Ветка: после четырёх бомб осталось 12 587, то есть обрушилось ~74 %. В повторах — 12 253 и 11 864.
- Develop: осталось 39 283, это ~19 % — только воронки.
- Ветка делает намного больше работы: больше обломков, кусков и физики. Это и есть фича. Но обрушение должно уложиться в бюджет кадра.

### Худшие кадры

- **airstrike, ветка** — 22.2, 19.3 и 19.2 мс. Путь: `UpdateScene > PreLateUpdate.ScriptRunBehaviourLateUpdate > GameLoopRunner.LateUpdate() > VoxelTickBridge > VoxelWorld.LateUpdate` 14.0 / 13.3 / 13.0 мс.
  - Ниже по дереву ни один ребёнок не берёт 40 %, потому что у решения нагрузки нет своих маркеров. Но у develop этого пути в худших кадрах нет. Новое в `VoxelWorld.LateUpdate` — только `SolveStructures()`.
  - Авиаудар бьёт в 12–16 м от танка. `VoxelWorld.OnGroundCarved` метит здания в радиусе воронки + 1.5 м, и соседние дома начинают считать нагрузку и сыпаться.
  - Владелец — команда `game/structure`.
- **house, ветка** — 19.4, 16.6 и 15.6 мс. Тот же путь, `VoxelWorld.LateUpdate` 10.2 / 11.1 / 10.4 мс. Владелец — команда `game/structure`.
- **airstrike и house, develop** — 12.9 и 15.0 мс. Путь: `RenderPlayModeViewCameras > Application.InvokeOnBeforeRender()`. Это сам взрыв: стенд вызывает `Explode` из `onBeforeRender`. Как и раньше.
- **drive и crush, обе стороны** — 44–50 мс.
  - Путь: `AtomicTickBridge > Crush.Kill` 26–30 мс, из них `Mono.JIT` 12–14 мс, затем `Crush.SprayAndStain` 9–16 мс.
  - В `drive` танк по дороге давит Regax. Это перф-долг develop, а не ветки. Владелец — команда AI.

### Заметки

- Пик GC в кадре события у develop тоже за бюджетом 4 КБ (31–39 КБ), это перф-долг.
  - У ветки он больше, потому что больше кусков: `Detach` создаёт `GameObject` и `AddComponent` на каждый кусок, пулов нет.
  - Пик GC шумит. В нагруженном повторе develop `house` дал 224 КБ, ветка — 116 КБ. Поэтому блок по GC идёт вслед за главным: он уйдёт вместе с исправлением обрушения или подтвердится следующим замером.
- GC в `idle`: 288 Б в 10 кадрах, одинаково на обеих сторонах. Это долг develop, к ветке не относится.

## Статическая проверка

`Tools/PerfCheck/static_check.sh --repo <ветка> --base game/develop`: новых 3, долг 118.

### Новые нарушения

1. `Assets/Game/Voxels/Runtime/VoxelBody.Structure.cs:241`, `debug-log` — **подтверждено**.
   - `Debug.Log` с интерполяцией строк в рантайме. Выключен флагом `LogStructure` (по умолчанию false), так что в игре не стоит ничего, но пометки нет.
   - Что сделать: `// perf-ok: tuning log, off unless LogStructure` в конце строки или вынести в метод с `[Conditional("VOXEL_LOAD_LOG")]`. Заодно `BlockMaterials` (`SortedSet`, `string.Join`) зовётся только оттуда.
2. `Assets/Game/Voxels/Runtime/VoxelLoadJobs.cs:199`, `field-naming` — **подтверждено**. `private int WorstSlot;` → `_worstSlot`: рядом лежит `_firstWorstSlot`.
3. `Assets/Game/Voxels/Runtime/VoxelBody.Structure.cs`, `file-type-name` — **ложное срабатывание**.
   - Это часть `partial class VoxelBody`, а имя `<Тип>.<Часть>.cs` — обычное соглашение.
   - Project.md про partial молчит. Предлагаю команде «Производительность и порядок» записать исключение и научить ему скрипт.

### Долг по правилам

Прошлых строк `game/structure` нет. Последняя статика в истории develop — ветка вокселизатора `7563c1f76`, долг 118. У этой ветки тоже 118.

| Правило | 7563c1f76 → game/structure |
|---|---|
| unity-update | 2 → 3 |
| find | 5 → 6 |
| camera-main | 8 → 9 |
| instantiate | 6 → 11 |
| old-input | 10 → 0 |
| ongui | 1 → 0 |
| file-type-name | 1 → 2 |
| field-naming | 17 → 21 |
| unused-assets | 14 → 12 |
| остальные | без изменений |

Ни одна строка долга не лежит в файлах, которые трогала ветка: все её файлы в `Assets/Game/Voxels/{Runtime,Tests}`, в долге их нет. Разница идёт от другого дерева (merge-base против базы вокселизатора), а не от ветки. Рост долга за веткой — **нет**.

## Ручной обзор изменений

`git diff 619ca415d..fd73a65b4`: 22 файла, +2326 / −26. Код — `Voxels/Runtime` (`VoxelLoadJobs.cs`, `VoxelBody.Structure.cs`, правки `VoxelBody`, `VoxelWorld`, `VoxelStructure`, `VoxelImpactFracture`, `VoxelJobs`, `VoxelTypes`). Тесты — `VoxelHouseCollapseTests`, `VoxelHouseStructureTests`, `VoxelLoadJobTests`.

1. **БЛОК (по замеру): синхронное решение нагрузки в тике.** Это и есть причина спайков.
   - `VoxelWorld.VoxelLateUpdate → SolveStructures → VoxelBody.SolveStructure → RunLoad`, всё в одном кадре на главном потоке:
     - `BuildBlocksJob.ScheduleParallel(...).Complete()`;
     - `FindSupport` на главном потоке;
     - `StructuralLoadJob.Run()`;
     - `DropIslands`: `BlockCellsJob.Run()` и `LabelComponentsJob.Run()` по всей сетке тела, затем `Crush`, `Crumble` и `Detach` на каждый остров.
   - Комментарий у `SolveStructure` объясняет, зачем в этом кадре: «pieces get their colliders this frame». Но время ничем не ограничено: все помеченные здания решаются в одном кадре, острова тоже.
   - Что сделать, на выбор:
     - ставить `BuildBlocksJob` и `StructuralLoadJob` в кадре и забирать в начале следующего (хук `EarlyUpdate`, как у `VoxelWorld`). Кадр задержки на обрушение не виден;
     - и/или бюджет: не больше N зданий и M островов за кадр, остальное — в следующий (`Queue()` уже умеет «дорешать потом»).
2. **ВНИМАНИЕ: нет `ProfilerMarker`.** Время меряется `Stopwatch` (`LastBlocksMs`, `LastLoadMs`, `LastStructureMs`). Раздел 5 («Замер своими силами») требует маркеры `Voxels.Structure.Solve`, `.Blocks`, `.Load`, `.DropIslands`. Без них агент не видит разбивку под `VoxelWorld.LateUpdate`.
3. **ВНИМАНИЕ: `Complete()` и `Run()` в том же кадре без комментария у вызова.** Сюда входят `RunLoad`, `SeamCells` и `DropIslands`. В `SeamCells` это наследие `Fracture`: `VoronoiCellsJob.Run()` и раньше был синхронным, результат нужен сейчас. Достаточно комментария.
4. **ВНИМАНИЕ: `GetComponentInParent<VoxelBody>()` на каждый `OnCollisionEnter`.** Это `VoxelImpactFracture.PressOnto`, и он стоит до проверки `_broken`, то есть срабатывает на каждый контакт любого падающего куска. В обрушении таких контактов сотни. Нужна дешёвая проверка: сначала `collision.impulse.y` против `MIN_IMPACT_LOAD`, потом поиск тела. Или кэш «коллайдер → тело» в `VoxelWorld`.
5. **ВНИМАНИЕ: `DisableSafetyChecks = true` на всех четырёх новых задачах.** Это `BuildBlocksJob`, `StructuralLoadJob`, `BlockCellsJob` и `SeamCellsJob`, плюс `[NativeDisableContainerSafetyRestriction]` на скретч-массивах. По разделу 5 так можно только на горячих задачах, уже проверенных в Play с проверками безопасности. Команде подтвердить в отчёте, что прогон с проверками был.
6. **ДОЛГ (растёт по объёму): куски без пулов.** `Detach` создаёт `new GameObject` с `AddComponent<VoxelBody>`, `Rigidbody` и `VoxelImpactFracture`. Это старый механизм, но ветка резко увеличивает число кусков: острова и `MAX_STRUCTURE_PIECES` = 12 на этаж. Отсюда пик GC и рост batches и теней в `house`.
7. **Загрузка уровня, не замерено.** `DesignStructure()` (полный `RunLoad` плюс заполнение `_scale` циклом на главном потоке) идёт на каждое здание при постройке `VoxelStructure`. Это стоимость старта сцены, сценарии её не ловят. Стоит разово замерить `LastBlocksMs + LastLoadMs` по всем зданиям.
8. **Порядок.**
   - `VoxelLoadJobs.cs` кроме задач держит `static class VoxelLoad` с константами подсистемы. Под исключение «`<Подсистема>Jobs.cs`» это подходит с натяжкой; допустимо.
   - Папки и сборка верные: `Game.Voxels`, новых asmdef нет.
   - Тесты на новый код есть: три файла, EditMode и PlayMode.
9. **Незакоммиченные чужие файлы в рабочей копии.** `Roboto_*_SDF.asset` и `SaveSystemSettings.asset` к ветке не относятся, их надо не коммитить вместе с фичей. `Docs/superpowers/specs/2026-11-17-structural-destruction.md` правился параллельно во время проверки, не мной.

## Что сделать

1. **Убрать спайк обрушения из кадра.** Это БЛОК: `airstrike` max, `house` avg и max. Перенести решение нагрузки на «поставить в кадре — забрать в следующем» и/или ограничить работу за кадр (здания и острова). Цель — худший кадр `house` и `airstrike` ≤ 16 мс, среднее `house` ≤ 5.0 мс. Владелец — команда `game/structure`; папка `Voxels/` по разделу 4 плана — у команды вокселизатора, согласовать с ней.
2. **Снизить пик GC в кадре обрушения.** Это БЛОК вслед за п. 1: меньше кусков за кадр, при возможности пул тел-кусков. Владелец — `game/structure`.
3. **Две правки статики в одну строку** (БЛОК): `WorstSlot` → `_worstSlot`, пометка `perf-ok` или `[Conditional]` на `Debug.Log` в `VoxelBody.Structure.cs:241`. Владелец — `game/structure`.
4. **Добавить `ProfilerMarker` на шаги решения нагрузки** и комментарии у синхронных `Complete()` и `Run()`. ВНИМАНИЕ. Владелец — `game/structure`.
5. **Дешёвая отсечка в `VoxelImpactFracture.PressOnto` до `GetComponentInParent`.** ВНИМАНИЕ. Владелец — `game/structure`.
6. **Подтвердить прогон с проверками безопасности** для задач с `DisableSafetyChecks`. ВНИМАНИЕ. Владелец — `game/structure`.
7. **Повторить `airstrike` и `house` на тихой машине после исправлений**, с Maximize On Play, рядом с замером develop. Владелец — `game/structure` вместе с агентом perf-check.
8. **Исключение для partial-файлов `<Тип>.<Часть>.cs`** в Project.md и в `static_check.sh`. Владелец — «Производительность и порядок» (`game/perf`).
9. **Долг develop, не ветки.** `Crush.Kill` + `Mono.JIT` 26–30 мс и `Crush.SprayAndStain` 9–16 мс в `drive` и `crush`, GC в `drive` 670 КБ. Владелец — команда AI. GC 288 Б в `idle` — причина не выделена.

Сценарий `house` внесён строкой в таблицу раздела 3 `Docs/Rules/Performance.md`. Сам шаг в `perf_bench.cs` пока не закоммичен.

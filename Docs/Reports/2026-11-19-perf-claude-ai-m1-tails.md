# Проверка производительности и порядка: claude/ai-m1-tails

**Дата:** 2026-11-19 **Коммит:** `53579b4ba` **База:** `game/develop` = `c3f2a7ad5` (совпадает с merge-base; ветка — один коммит поверх). **Редактор:** замер не снимался (`measure: no`, `play: no`): версии, рендера, качества, экрана и Scene view нет.

## Итог

**Можно вливать по статике.** Подтверждённых новых нарушений нет ни у скрипта, ни в ручном обзоре. Ветка не добавила долга и не убрала его. Перф-вердикта нет: замер будет добавлен отдельно. Предупреждений по ветке нет, есть только мелкие советы. Долг `game/develop` вырос на 23 пункта (129 → 152) с прошлой строки `static`. Этот рост пришёл прямыми коммитами в `game/develop`, ветка к нему отношения не имеет. Он вынесен тимлиду.

## Замер

**Замер будет добавлен отдельно.** В этой проверке он не снимался: `measure: no`, `play: no`. Строка `bench` в `perf-history.jsonl` не добавлена. Таблицы сценариев и худших кадров нет.

Что учесть при замере:

- **Стандартные сценарии** `perf_bench.cs` (idle, drive, airstrike, crush, crater, mg, house) код ветки не задевают. В `VoxelSandbox` орды нет: `HordeInstaller` в сцене не стоит, поэтому `HordeBehaviour.Tick` не идёт. От них ожидается то же, что от `game/develop` `c3f2a7ad5`. Сценарий crush — давка Regax-сущности, а не орды.
- **Код ветки работает только с ордой.** Нужны `Tools/PerfCheck/Bench/combat_bench.cs` (бой с ордой, 60 Ползунов) и `horde_arena_bench.cs` (или `arena_bench.cs`). Смотреть:
  - маркер `Horde.Tick`. Новый `SyncCrushSpeed` раз в кадр ищет `TankCrushBehaviour` по списку поведений танка. Это линейный проход без аллокаций, ожидается разница в пределах шума;
  - GC в кадрах боя. Ветка впервые включает пять звуков орды: нора, укус, плевок, кислота и число живых. Пул событий AudioEngine прогревается по одному экземпляру на событие (`AudioSystem._prewarmPerEvent = 1`). Когда звуки накладываются, `AudioEventPool.TryGet` делает `ScriptableObject.Instantiate`. Ждать `AudioEventPool` или `PlayEvent` в `gcSources` в первые секунды боя. Бюджет кадра события — ≤ 4 КБ;
  - число раздавленных. Порог давки теперь берётся у танка (`TankCrushBehaviour.MinSpeed`). Базовое значение то же, 1.5 м/с (`TankCrushConfig.minSpeed`, `TankBaseSettings.CrushSpeed`). Без модуля гусениц давка должна совпасть с `game/develop`.
- **Опорная точка.** Свежей строки `bench` для `game/develop` в истории нет. Последняя — `2faa7b274` от 2026-11-17, ближайший замер кода develop — `claude/perf-fix` `aa2d5d942` от 2026-11-18. С тех пор в develop вошли искры пулемёта (`VoxelSparks`), воксельный дым бомбы (`VoxelBlastSmoke`), `MudPool` и лаборатория. Поэтому `game/develop` `c3f2a7ad5` нужно мерить в той же сессии, что и ветку.

## Статическая проверка

Запуск: `bash Tools/PerfCheck/static_check.sh --repo <worktree> --base game/develop` (`c3f2a7ad5`). Итог: **новых 0, долг 152**. Строка `STATIC_JSON` добавлена в `Docs/Reports/perf-history.jsonl`.

**Новые нарушения:** нет.

**Ложные срабатывания среди новых:** нет, потому что новых нет. В долге два срабатывания по сути ложные:
- `unity-update` `Assets/Game/Horde/Runtime/HordeCorpses.cs:76`. Это `public void Update(float time)` у обычного класса `HordeCorpses : IDisposable`, а не у `MonoBehaviour`.
- `camera-main` `Assets/Game/Scripts/Features/AI/Horde/HordeBehaviour.cs:349`. `Camera.main` вызывается, только пока `_camera == null`, то есть значение кэшируется. Это отмечено ещё в отчёте `game/ai-m1`, но пометки `// perf-ok: cached` на строке до сих пор нет.

**Долг по правилам.** Сравнение с прошлыми строками `static` в истории: `game/ai-m1` `f78d466e1` и `game/audio-m1` `d1f9eb7a5` от 2026-11-18. У обеих одинаковые числа, долг 129. Ветка менялась только в строках, которые ни одно правило не ловит, поэтому её долг по каждому правилу равен долгу `game/develop` `c3f2a7ad5`. Ни одного из перечисленных ниже файлов в `f78d466e1` не было, кроме папки `AI/Combat` (см. её строку).

| Правило | было | стало | что добавилось (всё из `game/develop`) |
|---|---|---|---|
| unity-update | 3 | 6 | `MonoBehaviour.Update` в боевом коде: `Features/Bullets/VoxelSparks.cs:100` (`c3f2a7ad5`, искры пулемёта), `Features/Voxels/VoxelBlastSmoke.cs:66` (`02621813b`, дым бомбы), `Features/Voxels/MudPool.cs:205` (`6dd788e76`, черновик лужи) |
| find | 6 | 11 | `MudPool.cs:67`, `:68`; `TankDummy.cs:468`, `:469` (`eba802373`); `TankLab.cs:105` (`c7baff1df`) |
| instantiate | 11 | 14 | `TankDummy.cs:50`, `:492`; `TankLab.cs:108` |
| ongui | 0 | 1 | `TankLab.cs:256` |
| file-type-name | 8 | 9 | `Voxels/Runtime/VoxelBody.Recycle.cs` (частичный класс, `934d735ec`) |
| field-naming | 21 | 22 | `Tests/App/Saves/MetaProgressTests.cs:38` (`private bool Written`) |
| asset-prefix | 30 | 32 | `Scenes/Labs/LabBackdrop.mat`, `Scenes/Labs/LabTable.mat` без `MT_` (`c7baff1df`) |
| empty-dirs | 1 | 2 | `Scripts/Features/AI/Combat`: в `f78d466e1` её ещё не было; её принесло влитие боя (`fad8a5104`), а файлы удалил `da437cb42` (Integration: drop combat's temporary horde bridge), папка осталась |
| meta | 1 | 2 | `Scripts/Features/AI/Combat.meta` — та же папка |
| unused-assets | 14 | 19 | +5; прошлый список в истории поимённо не хранится. Сейчас: 10 шрифтов `Roboto`, `MT_Bullet_01`, `MT_Rocket_01`, `MT_Tank_Black`, `MT_Tank_Lens`, `pixelLaser_*.mat`, `laserProjectile.obj`, два `Outline (...).shadergraph` |
| остальные | = | = | |

Для ветки это не ВНИМАНИЕ: она ничего из этого не добавила. Для `game/develop` это **ВНИМАНИЕ**: долг должен только уменьшаться, а свежей строки `static` самого `game/develop` в истории нет.

## Ручной обзор изменений

`git diff game/develop --stat`: 7 файлов, +113 −49.

- **`Features/AI/Horde/HordeBehaviour.cs`.** Новый `SyncCrushSpeed()` вызывается в `Tick` каждый кадр, после `ReadTank` и до `_simulation.Schedule`. Порядок верный: шаг симуляции уже забран (`CompleteFrame`), новый ставится в конце тика.
  - Аллокаций нет. `HordeSettings` — структура, `HordeSimulation.Settings` — авто-свойство. Копия пишется обратно только при смене порога.
  - `CrushSpeedOf` вызывает `IEntity.TryGetBehaviour<TankCrushBehaviour>`. Это линейный проход по массиву поведений с проверкой `is T`, без аллокаций (`Entity_Behaviours.cs:211`, `MonoEntity_Behaviours.cs:192`). Тест `ReadingTheThreshold_AllocatesNothing` проверяет это ограничением Unity (`NoAllocation.Constraint`), как требует Performance.md.
  - Поиск поведения в тике — не `GetComponent` и не `Find*`, правило его прямо не запрещает. Но то же можно сделать один раз в `BindTank` (это событие): лоадаут меняет `MinSpeed` того же объекта, так что кэш поведения ничего не сломает. Совет, не нарушение.
  - `NO_CRUSH = float.MaxValue` — конечное число, сравнение `>=` в `HordeJobs.cs:1103` корректно при любом режиме float в Burst. Других мест, где `Settings.CrushSpeed` умножается или возводится в квадрат, нет.
- **`Features/AI/Horde/HordeOptions.cs`.** Поле `crushSpeed` удалено, `ToSettings()` ставит `NO_CRUSH`. Сериализованных ключей `crushSpeed:` в сценах, префабах и ассетах нет (`git grep`). Скрипты стендов в `Tools/PerfCheck/Bench` (`combat_bench.cs`, `horde_*`) поле не трогают, поэтому через `script eval` они соберутся. Сеттер `HordeBehaviour.CrushSpeed` больше нигде не используется.
- **`Features/AI/Horde/GameHordeAudio.cs`.** Пять заглушек TODO заменены прямыми вызовами `GameAudio.Horde`.
  - Частота: `SetAlive` и `CrawlerBite` идут не чаще 4 раз в секунду (`AUDIO_INTERVAL = 0.25`), `AcidHit` тоже. `BurrowOpened` срабатывает раз на нору.
  - `Spit` вызывается на каждый выстрел без ограничения на стороне AI. Лишние вызовы отсекает `AudioSystem.PlayEvent` по `maxFrequency` (0.1 с, `AudioLayout.cs:135`): это поиск в словаре, без аллокаций.
  - `PlayEvent` в установившемся режиме не аллоцирует: делегат `DisposeCallback` закэширован. Пул событий растёт через `ScriptableObject.Instantiate`, это код модуля звука, а не новая строка ветки. См. «Замер».
  - `AppAudio.SetBattleIntensity` — только установка цели.
- **`Horde/Runtime/HordeTypes.cs`.** Только документация.
- **`Horde/Tests/HordeKindTests.cs`.** Убрана проверка `CrushSpeed == 1.5` («GDD §4»). Число из GDD по-прежнему закреплено в `TankStatsUseCaseTests.cs:56`, и `HordeSettings.Default.CrushSpeed` остался 1.5 для тестов симуляции. Покрытие не потеряно.
- **`Tests/AI/HordeCrushSpeedTests.cs` (новый).**
  - Лежит правильно: `Game/Tests/AI`, сборка `Game.AIKit.Adapters.Tests`. Пространство имён `Game.AIKit.Adapters.Tests`, один верхнеуровневый тип, `.meta` в формате соседних файлов.
  - Четыре теста: порог танка, следование лоадауту, `NO_CRUSH` без танка и без поведения, отсутствие аллокаций.
  - Мелочь не по букве Project.md, раздел 4: класс назван по теме, а не по типу (`HordeBehaviour`). В папке так уже есть `ArenaDirectorRunTests`. Не блок.
- **Чего тесты не покрывают.** Сам `SyncCrushSpeed` в тике: порог меняется у живой симуляции и сбрасывается на `NO_CRUSH` после `ResetForArena`. И проброс `GameHordeAudio` → `GameAudio.Horde`. Код простой, не блок.
- **По списку проверки.**
  - `Instantiate` и `Destroy` в бою — не добавлены.
  - `Complete()` в том же кадре — не добавлен.
  - Циклы по вокселям или юнитам вне Burst — нет.
  - Новых asmdef нет.
  - Лишних типов в файлах нет.
  - Файлы лежат по Project.md, раздел 1.
- **Документ.** `Docs/superpowers/specs/2026-11-18-combat.md:16` пишет, что порог орды задан в `HordeSettings.CrushSpeed`. Теперь орда берёт порог танка, строку стоит поправить.

## Что сделать

1. **Тимлид / производительность.** Снять замер ветки и `game/develop` `c3f2a7ad5` в одной сессии: `combat_bench.cs` и `horde_arena_bench.cs` (код ветки работает только с ордой), плюс `perf_bench.cs` для контроля. В кадрах боя проверить GC от `AudioEventPool` и время `Horde.Tick`.
2. **Звук (`game/audio`), если замер покажет GC.** Если в бою найдётся `ScriptableObject.Instantiate` из `AudioEventPool.TryGet`, прогреть события орды (`Crawler.BITE`, `Spitter.SPIT`, `Spitter.ACID_HIT`, `Monsters.BURROW_OPEN`) больше чем на один экземпляр. Сейчас `_prewarmPerEvent = 1`.
3. **Тимлид (`game/develop`).** Долг develop вырос с 129 до 152 прямыми коммитами 2026-11-18:
   - `MonoBehaviour.Update` в боевом коде (`VoxelSparks`, `VoxelBlastSmoke`, `MudPool`): перевести на GameLoop или пометить `// perf-ok: <причина>`;
   - `Find*`, `Instantiate` и `OnGUI` в прототипах `TankLab`, `TankDummy` и `MudPool`: если это демо, место им в демо-сборке, иначе нужны пометки;
   - `LabBackdrop.mat` и `LabTable.mat` без `MT_`;
   - снять свежую строку `static` для самого `game/develop`.
4. **AI (папка `Features/AI`).** Удалить пустую папку `Assets/Game/Scripts/Features/AI/Combat` вместе с `Combat.meta`: это остаток после `da437cb42`, минус по одному пункту в `empty-dirs` и `meta`. На `HordeBehaviour.cs:349` поставить `// perf-ok: cached`: `camera-main` −1.
5. **AI, по желанию.** Кэшировать `TankCrushBehaviour` в `BindTank` и каждый кадр читать только `MinSpeed`. Поведение то же, но без поиска по списку поведений танка в каждом тике.
6. **AI, по желанию.** Добавить тест на `SyncCrushSpeed` в тике (смена порога, сброс после новой арены) и на проброс `GameHordeAudio`. Например, `SetAlive(30)` даёт `GameAudio.Horde.Alive == 0.5`; для этого тестовой сборке нужна ссылка на `Game.AudioEngine.Adapters`.
7. **Бой, документ.** Поправить `Docs/superpowers/specs/2026-11-18-combat.md:16`: порог давки орды теперь у танка (`TankCrushBehaviour.MinSpeed`), а не в `HordeSettings.CrushSpeed`.

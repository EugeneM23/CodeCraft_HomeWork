# Проверка производительности и порядка: claude/combat-326ad2

**Дата:** 2026-11-18 **Коммит:** 161fbef3c **База:** dc272cb08 (merge-base с `game/develop`, сейчас 731f821f5) **Редактор:** не открывался — только статическая проверка

Оцениваются коммиты боевой команды: 07d979a79, 6d152e66c, fad8a5104, 161fbef3c. Чужие коммиты, взятые как есть (контракты ядра cfd7be640 из `claude/core-run`, звук 6f1346f27), не оцениваются.

## Итог

**Можно вливать по статике (ВНИМАНИЕ, не БЛОК).** Подтверждённых новых нарушений нет: три находки скрипта — ложные срабатывания. Замер не делался: он **отложен**, и вливать до него нельзя (бой меняет горячий путь, раздел 5 Performance.md требует замер «до и после»).

## Замер

**Не проводился.** Вызывающий запретил открывать редактор: машина загружена (93 % CPU, открыты редакторы других команд). Замер при такой загрузке недействителен (раздел 4, п. 3, порог 25 %).

- Замер ждёт: прогон `perf_bench.cs` (idle, drive, airstrike, crush, crater) и новый сценарий **`Tools/PerfCheck/Bench/combat_bench.cs`** («бой с ордой»: орда 60, шесть навыков на 3-м уровне, езда 8 с; результат читает `combat_bench_read.cs`).
- Что проверить на замере:
  - 0 Б GC в idle и drive при включённом бою;
  - PlayerLoop в бою с ордой;
  - кадры убийства монстра орды (`HordeTargetSource.Damage` → `_horde.CompleteFrame()`);
  - кадр первого удара навыка авиаудара (бомба строится, пул не прогрет);
  - стоимость `SkillOfferDebugView.OnGUI`, который всегда включён.

## Статическая проверка

Скрипт нашёл 3 новых нарушения. Все три — **ложные срабатывания**:

| Файл:строка | Правило | Почему ложное | Что сделать |
|---|---|---|---|
| `Assets/Game/Scripts/Features/Controls/ControllableFireController.cs:36` | camera-main | вызов в `Init`, а не в тике; камера кэшируется в поле | `// perf-ok: cached in Init` |
| `Assets/Game/Scripts/Features/Controls/ControllableFireController.cs:92` | camera-main | в `LateTick`, но только когда кэшированная камера уничтожена (как в долге `CameraLookController`). Если камеры в сцене нет, а ПКМ зажата, поиск идёт каждый кадр — дёшево, но без кэша | `// perf-ok: re-lookup only after the cached camera is destroyed` |
| `Assets/Game/Scripts/Features/Combat/Targeting/TargetingSetup.cs:88` | find | `FindFirstObjectByType<VoxelAirStrike>` один раз в `AttachTank`, не в тике (так и написано в комментарии) | `// perf-ok: once on tank attach` |

Пометки `perf-ok` нужны, чтобы скрипт перестал считать эти строки новыми нарушениями.

Существующие пометки `perf-ok` у ветки проверены, причины правдоподобны:
- `SkillOfferDebugView.Update` — выбор навыка идёт во время паузы;
- `SkillOfferDebugView.OnGUI` — отладочный UI до экрана меню; см. п. 2 ниже;
- `VoxelAirStrike.Update` — тумблер авиаудара.

**Долг по правилам.** Сравнение с прошлой строкой `static` из истории `game/develop`: `game/debris` f07b35a6f, база 619ca415d. Её база не совпадает с dc272cb08, поэтому сравнение приблизительное.

| Правило | Было | Стало | Откуда |
|---|---|---|---|
| camera-main | 9 (+1 new) | 10 | влитое в `game/develop` из debris |
| debug-log | 0 | 1 | `VoxelBody.Structure.cs:254`, из `game/develop` |
| file-type-name | 2 (+5 new) | 8 | влитое в `game/develop` из debris |
| asset-prefix | 29 | 30 | `game/develop` |
| unused-assets | 12 | 14 | `game/develop` |
| всего | 118 | 129 | |

Весь прирост долга есть в базе dc272cb08, то есть пришёл из `game/develop`, а не из коммитов боя. Ветка долга не добавила. Прирост в `game/develop` — **ВНИМАНИЕ** для тимлида: например, `Debug.Log` в `VoxelBody.Structure.cs:254` — это команда вокселей.

## Ручной обзор изменений

Просмотрены тики: `AutoGunBehaviour`, `TurretsBehaviour`, `AirStrikeSkillBehaviour`, `SpecialModuleInputBehaviour`, `XpOrbField`, `HordeCombatBridge`, `BulletMoveBehaviour`, `BulletCollisionBehaviour`, `ControllableFireController`, `TankDrive`, `TankCrush`, `TurretAim`, `VoxelAirStrike`. Просмотрены также `EnemyTargets`, `HordeTargetSource`, `EntityEnemySource`, `BulletShotUseCase`, `ShellBounceUseCase`.

**Что сделано хорошо:**
- В тиках нет аллокаций, LINQ, `foreach` по интерфейсам, `GetComponent` и строк.
- Поиск цели идёт раз в 0.1 с; `OverlapSphereNonAlloc` со статическим буфером.
- Сгустки опыта рисуются одним `RenderMeshInstanced` без теней, с маркером `Combat.XpOrbs`.
- Есть маркеры `Combat.FindDensest` и `Combat.HordeSnapshot`.
- Запись value-type ключей (`ExplosionRadius`, `RicochetCount`) идёт только при изменении значения.
- `VoxelAirStrike` перешёл с `Destroy` бомб на пул — это улучшение.
- Турели строятся один раз в `Init`.

Подтверждённых новых нарушений нет. Замечания:

1. **ВНИМАНИЕ — чужая папка.** `Features/AI/Combat/HordeCombatBridge.cs` и `HordeTargetSource.cs` лежат в папке и сборке AI (`Game.AIKit.Adapters`, владелец — команда AI). В шапке они помечены как TEMPORARY. Нужно согласие AI или перенос в источник AI.
2. **ВНИМАНИЕ — IMGUI всегда включён.** `SkillOfferDebugView` создаётся в `LocalRunService` в каждом уровне. Его `OnGUI` рисует строку опыта каждый кадр, а IMGUI выделяет GC каждый кадр. Это попадает под исключение «отладочный HUD», но в отчёте замера пойдёт отдельной строкой. Его надо держать за флагом разработки и убрать, когда меню сделает экран навыка.
3. **ВНИМАНИЕ — линейный обход орды.** `HordeTargetSource` обходит всех живых монстров линейно и в управляемом коде:
   - снимок раз в кадр;
   - `Raycast` для каждой пули в каждом `FixedTick`;
   - `TryFindDensest` — около 90 обходов на один удар.

   Спека боя §8 обещала сетку AI (`HASH_CELL`). При орде 30–60 (GDD) это дёшево, правило «>1000 элементов → Burst» не нарушено. Если орда вырастет, нужна сетка. Кроме того, `Damage` при убийстве ждёт шаг орды (`CompleteFrame()`). Это прокомментировано и допустимо, но это точка синхронизации внутри физического шага, и её надо проверить замером.
4. **ВНИМАНИЕ — нет маркера.** Спека §8 обещает маркер `Combat.AutoGun`, а его нет.
5. **ВНИМАНИЕ — пул бомб.** Пул бомб `VoxelAirStrike` (4 штуки) не прогревается при загрузке: первая бомба строится в бою. Сверх четырёх бомба уничтожается через `Destroy`. Нужно прогреть пул в `Start` или в `CombatPoolPrewarm`.
6. **Замечание — материал в коде.** `XpOrbField` создаёт материал в коде (`Shader.Find("Universal Render Pipeline/Lit")` + `new Material`). Это происходит в `Init` и на перф не влияет. Но лучше ассет `MT_XpOrb`: в билде нет риска остаться без шейдера, и материал можно настраивать.
7. **Тесты.** Тестов нет у:
   - `HordeTargetSource` (версии слотов, переиспользование слота);
   - `EntityEnemySource` (спека §9 обещала «поиск ближайшего в источнике сущностей»).

   EditMode-тесты остальных частей есть: модули, прогресс, опыт, `EnemyTargets`.
8. **Неотслеживаемые файлы.** В рабочей копии лежат `Assets/Game/Tests/Features/Combat/Scene/` (`CombatSandboxTests.cs`, новая сборка `Game.Combat.SceneTests.asmdef`, без `.meta`). Это незакоммиченная работа, она не оценивалась. При коммите сборку нужно добавить в таблицу Project.md §2.
9. **Однотипность файлов.** Нарушений «один тип на файл» и неверных папок в остальной части ветки нет.

## Что сделать

1. **Боевая команда** — когда машина освободится (<25 % CPU), сделать замер: `perf_bench.cs` и `combat_bench.cs` в свежем Play VoxelSandbox. Приложить сравнение с `game/develop`. До замера не вливать.
2. **Тимлид** (правила) — добавить в Performance.md §3 строку сценария **combat** («бой с ордой», VoxelSandbox, орда 60, 6 навыков на 3-м уровне, езда вперёд/назад, 8 с, `Tools/PerfCheck/Bench/combat_bench.cs`). Это предложение; правила здесь не правились.
3. **Боевая команда + AI** — договориться о `Features/AI/Combat/*`: либо AI принимает файлы, либо источник орды делает AI, а мост уходит.
4. **Боевая команда** — спрятать `SkillOfferDebugView` за флагом разработки.
5. **Боевая команда** — прогреть пул бомб авиаудара при загрузке.
6. **Боевая команда** — поставить три пометки `perf-ok` (см. таблицу) и добавить маркер `Combat.AutoGun`.
7. **Боевая команда** — тесты на `HordeTargetSource` и `EntityEnemySource`. Сборку `Game.Combat.SceneTests` внести в Project.md §2.
8. **Боевая команда + AI** — когда размер орды вырастет за сотни, перевести запросы к орде на сетку AI (`HASH_CELL`), как в спеке §8.
9. **Тимлид** — следить за ростом долга в `game/develop`: 118 → 129, в том числе `Debug.Log` в `VoxelBody.Structure.cs:254` (команда вокселей).

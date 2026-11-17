# План: структурное разрушение домов

Спецификация: `Docs/superpowers/specs/2026-11-17-structural-destruction.md`. Ветка `game/structure`. Редактор — своя копия, порт 7801.

## Шаги

1. ~~Баг «дом улетает»: тест `VoxelHouseCollapseTests`, `BlastPush` вместо `AddExplosionForce`.~~ Готово, `4b71e4b2a`.
2. **Таблица прочности.** `VoxelStrength` (структура: сжатие и изгиб на 256 индексов, `NativeArray<float2>`), `VoxelBody.SetStrength` (тело владеет массивом, куски получают копию). `VoxelStructure` заполняет таблицу для домов, башен и стен.
3. **Задачи Burst** (`VoxelLoadJobs.cs`):
   - `BuildBlocksJob` — масса блоков и прочности граней;
   - `StructuralLoadJob` — Дейкстра от опор, накопление нагрузки и момента, разрыв, островки;
   - `IslandCellsJob` — ячейка каждого вокселя по островку его блока;
   - `SeamCellsJob` — ячейки раскола по слабым связям (для `Fracture`).
4. **`VoxelBody.Structure.cs`** (partial): опоры из проб рельефа (`IsSolidAt`), пометка «к пересчёту», `AddImpactLoad`, `SolveStructure` (запуск задач, отделение островков через `LabelComponentsJob` с ячейками). `VoxelWorld` вызывает пересчёт перед перестройкой; подписка на `VoxelSignals.Carved/AirBlasted`.
5. **Удары.** `VoxelImpactFracture`: нагрузка на стоящее тело; `Fracture` с таблицей — ячейки по швам.
6. **Тесты** из спецификации, калибровка констант.
7. **Прототип пользователю:** серия скриншотов «выбили первый этаж» в песочнице.
8. Ревью субагентом, полный прогон тестов, `/perf-check`, отчёт тимлиду, заметка в общую папку.

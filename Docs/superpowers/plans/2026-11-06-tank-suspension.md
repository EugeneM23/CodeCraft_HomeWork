# Лучевая подвеска танка — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** физическая лучевая подвеска на 10 катков, привод силой в пятне контакта, стабилизатор,
отдача, подвижные катки и гусеницы.

**Architecture:** `TankSuspensionBehaviour` (лучи, пружины, стабилизатор) пишет итоги в
`TankSuspension`; `TankDriveBehaviour` читает их и толкает тело; `TankRecoilBehaviour` — импульс на
выстрел; `TankTrackView` двигает катки и звенья. Числа — `SuspensionMath`, чистая функция с тестами.

Спек: `Docs/superpowers/specs/2026-11-06-tank-suspension-design.md`.

## Global Constraints
- Перекомпиляция и Refresh — только вне Play Mode (общий редактор, перезагрузка домена в игре
  стирает значения сущностей).
- Сцену с чужими несохранёнными правками билдер не сохраняет.
- Код не коммитится (в тех же файлах незакоммиченные правки пользователя), коммитятся только документы.
- Все длины модели — через `S` в `TankBuilder`.

### Task 1: SuspensionMath и тесты
- [ ] `Tests/SuspensionMathTests.cs`: шесть случаев из спека → FAIL (нет типа).
- [ ] `Features/Vehicles/Model/SuspensionMath.cs` → PASS.

### Task 2: Подвеска, привод, отдача
- [ ] `EntityAPI.Suspension`, `TankSuspension` (+ `TankWheel`).
- [ ] `TankSuspensionBehaviour`, переделка `TankDriveBehaviour`, `TankRecoilBehaviour`.
- [ ] Удалить `HullSwayBehaviour`.

### Task 3: Вид и установщик
- [ ] `TankTrackView`, `TankTrackRig`.
- [ ] `TankInstaller`: катки, пружины, гусеницы, отдача; порядок поведений: подвеска → привод.

### Task 4: Модель
- [ ] `TankBuilder`: тело без заморозки, корпус без роликов, крепления, катки со спицами, звенья,
  колёса гусеницы, крылья. Пересобрать танк.

### Task 5: Проверка
- [ ] Все EditMode-тесты.
- [ ] Play: покой, езда, воронка, выстрел, наклон, посадка/высадка, гибель.

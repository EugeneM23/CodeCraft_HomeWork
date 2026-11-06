# Мокрая грязь, лужи и колея — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Пол Playground становится мокрой грязью с процедурными лужами, а танк продавливает в ней
вечную колею с рисунком траков и валиками.

**Architecture:** `MudSurface` на кубе `Floor` владеет картой следов (RenderTexture, R — колея,
G — валик) и рисует в неё штампы одним аддитивным проходом за кадр. Шейдер `Game/MudGround`
смещает вершины плотной сетки-ребёнка `Floor` по формуле высоты из `MudCommon.hlsl` и красит
грязь и воду. `TankTrackMarksBehaviour` каждый кадр строит отрезки под катками на земле и отдаёт их
поверхности, на которой стоят катки.

**Tech Stack:** Unity 6000.0.38, Built-in RP, HLSL surface shader, Atomic.Entities, NUnit (EditMode).

## Global Constraints
- Спека: `Docs/superpowers/specs/2026-11-06-mud-ground-design.md`.
- Built-in RP, шейдеры пишутся вручную, без Shader Graph и без внешних текстур.
- Коллайдер пола остаётся box на y = 0; физика и подвеска не меняются.
- Коммитятся только документы в `Docs/`; код остаётся незакоммиченным.
- Общий редактор Unity: не перекомпилировать во время Play (проверять `editor_status`).

## Уточнения к спеке (решены при планировании)
- Высота: `h = (T + relief) · (1 − rut) + B · berm · (1 − rut)`. Колея снимает и рельеф, поэтому
  `rut = 1` всегда даёт y = 0, где стоит танк.
- `T` = 0.10 м, `ReliefDepth` = 0.04 м, `B` = 0.04 м: так ноги персонажей утоплены неглубоко.
- Вода двух видов, чтобы не заливать каждую колею после одного проезда:
  - лужа — там, где `relief < PuddleLevel` (−0.015 м). Поверхность в луже плоская на
    `T + PuddleLevel`: вершина рисуется на `max(h, waterSurface)`, колея под водой видна как
    более тёмная (глубокая) вода;
  - вода в колее — где `rut > 0.85`, то есть после второго проезда.
- `MudSurface` стоит на самом `Floor` (там коллайдер, его находит подвеска) и ссылается на дочерний
  рендерер `MudGround`.
- Подвеска записывает в `TankWheel.Ground` коллайдер под катком, поэтому отдельный луч не нужен.
- Точки контакта берутся из нарисованных катков (`Visual.position − up · WheelRadius`), а не из
  физического шага, иначе колея отставала бы от гусеницы на шаг физики.
- Ширина колеи — `localScale.x` звена гусеницы, как в `TankTrackView` (0.5 м).
- Штампы кадра собираются в один динамический меш: вершины уже в UV карты, параметры в UV-каналах.
  Один `DrawMesh` с ортопроекцией через `GL.GetGPUProjectionMatrix(…, true)`.

## Файлы
- Create `Assets/Game/Scripts/Features/Mud/MudSurface.cs` — карта следов, очередь и отрисовка штампов.
- Create `Assets/Game/Scripts/Features/Controllables/Tank/Drive/TrackMarkMath.cs` — чистая математика.
- Create `Assets/Game/Scripts/Features/Controllables/Tank/Drive/TankTrackMarksBehaviour.cs`.
- Modify `Assets/Game/Scripts/Features/Controllables/Tank/Suspension/TankSuspension.cs` — поле `Ground`.
- Modify `Assets/Game/Scripts/Features/Controllables/Tank/Suspension/TankSuspensionBehaviour.cs` — пишет `Ground`.
- Modify `Assets/Game/Scripts/Features/Controllables/Tank/TankInstaller.cs` — добавить behaviour.
- Create `Assets/Game/Shaders/Mud/MudCommon.hlsl`, `MudGround.shader`, `MudStamp.shader`.
- Create `Assets/Game/Scripts/Editor/MudGroundBuilder.cs` — меш, материал, сборка на `Floor`.
- Modify `Assets/Game/Scripts/Editor/PlaygroundBuilder.cs` — вызывать сборку грязи после `BuildBox`.
- Create `Assets/Game/Tests/TrackMarkMathTests.cs`.

### Task 1: TrackMarkMath + тесты
**Produces:** `static float TrackMarkMath.Points(IReadOnlyList<Vector3> contacts, IReadOnlyList<bool> grounded, Vector3 forward, float extend, List<Vector3> points)` — точки ленты спереди назад (первая — первый каток на земле + forward·extend, последняя — последний − forward·extend), возвращает длину пятна; без катков на земле — пустой список и 0.
`static float TrackMarkMath.Amount(float passDepth, float trackSpeed, float dt, float contactLength)` — `passDepth·|trackSpeed|·dt/contactLength`, 0 при `contactLength < 1e-4`.
- [ ] Тесты: все на земле, средний в воздухе (пропускается), ни одного, один каток (два удлинения), длина; `Amount` — ноль на месте, сумма за проход длины пятна равна passDepth при dt 0.01 и 0.033.
- [ ] Реализация, прогон тестов.

### Task 2: MudSurface + шейдер штампа
**Produces:** `MudSurface.Stamp(Vector3 from, Vector3 to, float width, float amount)`, `MudSurface.Texture`, свойства `_MudTracks`, `_MudTracksRect` (xMin, zMin, 1/sizeX, 1/sizeZ), `_MudTexel` (мировой шаг тексела).
- [ ] RGHalf, 2 см/тексель от `BoxCollider.bounds`, очистка в `Awake`, `Release` в `OnDestroy`, пересоздание если `!IsCreated()`.
- [ ] Очередь штампов → меш (pos = UV карты; uv0 = along, across; uv1 = length, halfWidth, amount, worldAlong) → `CommandBuffer` в `LateUpdate`.
- [ ] `Hidden/Game/MudStamp`: `Blend One One`, `ZTest Always`, `Cull Off`; профиль колеи и валика, рисунок траков.

### Task 3: Шейдер земли
- [ ] `MudCommon.hlsl`: hash/value noise/fbm, `MudRelief`, `MudTracks`, `MudHeight`, `MudDisplayHeight`, `MudWater`.
- [ ] `Game/MudGround`: surface Standard, `vertex:vert addshadow fullforwardshadows`, target 3.5; вершина на `MudDisplayHeight`; нормали из разностей; грязь, мокрость, вода с рябью.

### Task 4: Танк ставит колею
- [ ] `TankWheel.Ground` (Collider), подвеска пишет его (null без земли).
- [ ] `TankTrackMarksBehaviour(IEntityInit, IEntityTick)`: катки по бортам спереди назад, точки через `TrackMarkMath.Points`, скорость ленты как в `TankTrackView`, `Amount`, штампы в `MudSurface` с коллайдера первого катка на земле.
- [ ] Регистрация в `TankInstaller` после `TankTrackView`.

### Task 5: Сборка сцены и проверка
- [ ] `MudGroundBuilder`: меш-сетка 300 × 450 ячеек в единичном квадрате (UInt32, касательная (1,0,0,−1)), `Assets/Game/Models/MudGround.asset`; материал `Assets/Game/Materials/MT_MudGround.mat`; меню `Tools/Playground/Mud Ground` для открытой сцены; `Apply(Transform floor)` для `PlaygroundBuilder`.
- [ ] Применить к открытой Playground, сохранить сцену.
- [ ] Тесты EditMode, Play: прямо, разворот, второй проезд, лужа; скриншоты Game View.

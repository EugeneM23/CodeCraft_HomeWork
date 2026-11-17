# Грунт и обломки: план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** комья земли рассыпаются каскадом на куски, крошку и пыль; улёгшиеся обломки лежат, пока есть место
(~100 тыс. кубиков), давятся танком и подбрасываются взрывами; публичный `ThrowEarth` для команды «танк и воронка».

**Architecture:** всё внутри `Game.Voxels.VoxelDebris` (разбит на partial-файлы). Летящие кластеры — как сейчас
(лучи `RaycastCommand`, Burst-задачи шага, который идёт, пока рисуется кадр). Новое «лежачее» хранилище —
плотный `NativeArray<LyingCube>` со swap-remove, рисуется `Graphics.RenderMeshInstanced` прямо из
`NativeArray` матриц. Запросы давки/взрыва/осыпания копятся и обрабатываются задачами в шаге.

**Tech Stack:** Unity 6000.3.28f1, URP, Burst, Jobs, Collections, Mathematics, NUnit (Unity Test Framework).

**Спека:** `Docs/superpowers/specs/2026-11-17-debris-crumble.md` — читать перед любой задачей.

## Global Constraints

- Ветка `game/debris`, рабочая копия `C:\!!!Work\CodeCraft_HomeWork_ATOMIC\.claude\worktrees\debris-230e86`.
- Код и комментарии на английском, документы на русском. Стиль — как в `VoxelDebris.cs`: `/// <summary>`, объясняющие «почему», `_camelCase` поля, `UPPER_CASE` константы.
- В кадре — без выделений управляемой памяти (GC ≈ 0). Внутри задач только `Allocator.Temp`. Массовая работа — `[BurstCompile]` задачи.
- PlayerLoop ≤ 5 мс (`Docs/Rules/Performance.md`), без пиков в кадре взрыва.
- Не трогать `VoxelBody.cs`, `VoxelImpactFracture.cs` (команда структуры), `Packages/`, `ProjectSettings/`.
- `VoxelWorld.cs`, `VoxelTrackRuts.cs` — правки только в местах вызова `VoxelDebris`.
- Коммитить только свои пути: `git add <пути>`; сообщение заканчивается строкой
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Ничего не пушить.
- Редактор Unity открыт на этой копии; рабочий процесс тестов — раздел «Как проверять».

## Как проверять

Редактор Unity этой копии открывает координатор (сессия команды), субагенты им пользуются через `unity` CLI
(`unity --help`; команды `unity cmd ...` с `--project` этой копии или порт из `unity-status`).
- После правки кода: `unity cmd script recompile` (или MCP `script_recompile`), затем консоль на ошибки.
  **Перед перекомпиляцией проверить, что редактор не в Play.**
- Тесты: EditMode-класс по фильтру, например `VoxelDebrisCrumbleTests`; PlayMode — асинхронно, результат
  по строке консоли `[TestResultCollector] Run finished: N total, X passed, Y failed`. Не запускать тесты, если
  открытая сцена грязная. Одна команда тестов за раз, не перезапускать после ответа 400 — читать консоль.
- Тесты `Game.Voxels.Tests` (папка `Assets/Game/Voxels/Tests`) работают без сцены: своя `VoxelDebris`,
  плоскость `GameObject.CreatePrimitive(PrimitiveType.Plane)`, `Physics.SyncTransforms()`, шаги `Tick(0.02f)`
  руками и `Complete()` (образец — `VoxelDebrisKeepTests`).

## Файлы

| Файл | Что в нём |
|---|---|
| `Assets/Game/Voxels/Runtime/VoxelDebris.cs` | публичный API, конструктор, `Tick`, отрисовка, `Dispose` |
| `Assets/Game/Voxels/Runtime/VoxelDebris.Flight.cs` (новый) | `Cluster`, `Cube`, `BuildRaysJob`, `IntegrateJob`, `CompactJob` |
| `Assets/Game/Voxels/Runtime/VoxelDebris.Shatter.cs` (новый) | `ShatterJob`, `PartsFor`, константы каскада |
| `Assets/Game/Voxels/Runtime/VoxelDebris.Lying.cs` (новый) | `LyingCube`, `LyingQuery`, `LyingQueryJob`, `LyingJob`, `EvictJob`, API `Crush/Blow/Loosen` |
| `Assets/Game/Voxels/Runtime/VoxelDebris.Earth.cs` (новый) | `EarthThrow`, `ThrowEarth` |
| `Assets/Game/Voxels/Runtime/VoxelWorld.cs` | ёмкость лежачих; подписка `VoxelSignals.Carved/AirBlasted` → `Blow/Loosen`; позиция камеры |
| `Assets/Game/Voxels/Runtime/VoxelTrackRuts.cs` | `Crush` под катками |
| `Assets/Game/Voxels/Tests/VoxelDebrisCrumbleTests.cs` (новый) | каскад |
| `Assets/Game/Voxels/Tests/VoxelDebrisLyingTests.cs` (новый) | сон, вытеснение, давка, взрыв, осыпание |
| `Assets/Game/Voxels/Tests/VoxelDebrisEarthTests.cs` (новый) | `ThrowEarth` |

---

### Task 1: Разбить `VoxelDebris` на partial-файлы и добавить отладочные счётчики

Без изменения поведения. Класс становится `public sealed partial class VoxelDebris`.

**Files:**
- Modify: `Assets/Game/Voxels/Runtime/VoxelDebris.cs`
- Create: `Assets/Game/Voxels/Runtime/VoxelDebris.Flight.cs`, `Assets/Game/Voxels/Runtime/VoxelDebris.Shatter.cs` (+ `.meta` создаст Unity при импорте; закоммитить их)
- Test: `Assets/Game/Voxels/Tests/VoxelDebrisCrumbleTests.cs`

**Interfaces — Produces:**
- `public int ClusterCount { get; }` — число летящих/лежащих-в-симуляции кластеров (после `Complete()`).
- `public void GetClusterSizes(System.Collections.Generic.List<int> sizes)` — очищает список и кладёт `Count` каждого кластера (для тестов и отладки; аллоцирует только если список растёт).

- [ ] **Step 1:** Перенести `Cluster`, `Cube`, `BuildRaysJob`, `IntegrateJob`, `CompactJob` в `VoxelDebris.Flight.cs`; `ShatterJob` — в `VoxelDebris.Shatter.cs`. В каждом файле `namespace Game.Voxels { public sealed partial class VoxelDebris { ... } }` и нужные `using`.
- [ ] **Step 2:** Добавить в `VoxelDebris.cs`:

```csharp
public int ClusterCount
{
    get
    {
        Complete();
        return _clusters.IsCreated ? _clusters.Length : 0;
    }
}

/// <summary>Cube counts of the clusters still simulated: for tests and debugging.</summary>
public void GetClusterSizes(System.Collections.Generic.List<int> sizes)
{
    Complete();
    sizes.Clear();
    if (!_clusters.IsCreated)
        return;
    for (int i = 0; i < _clusters.Length; i++)
        sizes.Add(_clusters[i].Count);
}
```

- [ ] **Step 3:** Создать `VoxelDebrisCrumbleTests.cs` с проверкой, что разбиение ничего не сломало (тот же каркас, что `VoxelDebrisKeepTests`: `SetUp` с плоскостью 5×, материал URP/Lit с инстансингом, `new VoxelDebris(_material, 4096, 4096)`, `Run(seconds)`):

```csharp
[Test]
public void Clump_ThrownStraightDown_Breaks()
{
    int clump = Clump(new Vector3(0f, 0.6f, 0f), new Vector3(0f, -12f, 0f), 5, Color.gray);
    Run(0.2f);
    var sizes = new List<int>();
    _debris.GetClusterSizes(sizes);
    Assert.That(sizes.Count, Is.GreaterThan(1), "it hit at 12 m/s and broke");
    Assert.AreEqual(125, Sum(sizes) + _debris.LyingCount, "no cube lost or added");
}

/// <summary>A side³ block of 0.2 m cubes centred at <paramref name="center"/>.</summary>
private int Clump(Vector3 center, Vector3 velocity, int side, Color color)
{
    int clump = _debris.BeginCluster(center, velocity, 0f);
    const float cube = 0.2f;
    float half = (side - 1) * cube * 0.5f;
    for (int z = 0; z < side; z++)
    for (int y = 0; y < side; y++)
    for (int x = 0; x < side; x++)
        _debris.AddCube(clump, center + new Vector3(x * cube - half, y * cube - half, z * cube - half), cube, color);
    return clump;
}

private static int Sum(List<int> sizes) { int s = 0; foreach (int n in sizes) s += n; return s; }
```

- [ ] **Step 4:** Перекомпилировать, запустить `VoxelDebrisKeepTests` и `VoxelDebrisCrumbleTests` (EditMode). Ожидается PASS.
- [ ] **Step 5:** Commit `Debris: VoxelDebris split into partial files, cluster counters for tests`.

---

### Task 2: Каскадное рассыпание, крошка и пыль

Спека, раздел 1. Только `VoxelDebris.Flight.cs`, `VoxelDebris.Shatter.cs`, тесты.

**Files:**
- Modify: `VoxelDebris.Flight.cs` (`Cluster` новые поля; `IntegrateJob` пишет удар; `CompactJob` для `DUST`)
- Modify: `VoxelDebris.Shatter.cs`
- Test: `Assets/Game/Voxels/Tests/VoxelDebrisCrumbleTests.cs`

**Interfaces — Produces:**
- `public static int PartsFor(int cubes, float impactSpeed)` в `VoxelDebris`.
- `Cluster` получает поля `float Impact` (скорость удара по нормали, м/с), `float3 HitPoint`, `float3 HitNormal`.
- Флаг `private const int DUST = 8;` (пыль: не ложится, своя гравитация).
- Константы: `MAX_PARTS = 8`, `MIN_PIECE = 6`, `CRUMB_SHARE = 0.18f`, `FULL_IMPACT = 15f`, `DUST_SPEED = 6f`, `DUST_GRAVITY = 0.3f / GRAVITY_SCALE` (доля от гравитации обломков).

- [ ] **Step 1: падающие тесты**

```csharp
[TestCase(300, 15f, 8)]
[TestCase(300, 8f, 4)]
[TestCase(60, 5f, 2)]
[TestCase(40, 3f, 2)]
public void PartsFor_FollowsTheTable(int cubes, float speed, int parts) =>
    Assert.AreEqual(parts, VoxelDebris.PartsFor(cubes, speed));

[Test]
public void BigClump_HardHit_BreaksIntoManyPartsAndCrumbs()
{
    Clump(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 7, Color.gray); // 343 cubes
    Run(0.15f);
    var sizes = new List<int>();
    _debris.GetClusterSizes(sizes);
    int crumbs = 0, pieces = 0, cubes = 0;
    foreach (int n in sizes)
    {
        if (n == 1) crumbs++;
        else pieces++;
        cubes += n;
    }
    int dust = cubes + _debris.LyingCount - 343;
    Assert.That(pieces, Is.InRange(6, 8), "pieces");
    Assert.That(crumbs - dust, Is.InRange(343 * 10 / 100, 343 * 30 / 100), "crumbs: 15-20% of the clump, give or take");
    Assert.That(dust, Is.InRange(6, 12), "one puff of dust (dust cubes are extra, not taken from the clump)");
}

[Test]
public void Pieces_HopAndBreakAgainOnTheSecondLanding()
{
    Clump(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 7, Color.gray);
    Run(0.15f);
    var first = new List<int>();
    _debris.GetClusterSizes(first);
    int biggest = 0;
    foreach (int n in first) biggest = Mathf.Max(biggest, n);

    Run(3f);
    var later = new List<int>();
    _debris.GetClusterSizes(later);
    int biggestLater = 0;
    foreach (int n in later) biggestLater = Mathf.Max(biggestLater, n);
    // Pieces that have lain still have moved to the lying store by now (task 3); until then they stay here.
    Assert.That(biggestLater, Is.LessThan(biggest), "the second landing broke the biggest piece again");
}

[Test]
public void SmallClump_FallsIntoCrumbs()
{
    int clump = _debris.BeginCluster(new Vector3(0f, 0.5f, 0f), new Vector3(0f, -8f, 0f), 0f);
    for (int i = 0; i < 5; i++)
        _debris.AddCube(clump, new Vector3(i * 0.2f, 0.5f, 0f), 0.2f, Color.gray);
    Run(0.15f);
    var sizes = new List<int>();
    _debris.GetClusterSizes(sizes);
    foreach (int n in sizes)
        Assert.AreEqual(1, n, "under MIN_PIECE cubes there are no pieces, only crumbs");
}

[Test]
public void Crumbs_KeepTheirColours()
{
    // Top layer green (turf), the rest brown: crumbs come from the bottom (the side that hit).
    int clump = _debris.BeginCluster(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 0f);
    for (int z = 0; z < 6; z++)
    for (int y = 0; y < 6; y++)
    for (int x = 0; x < 6; x++)
        _debris.AddCube(clump, new Vector3(x * 0.2f, 1.2f + y * 0.2f, z * 0.2f), 0.2f, y == 5 ? Color.green : new Color(0.4f, 0.25f, 0.1f));
    Run(0.15f);
    Assert.That(_debris.CountCrumbsOfColour(Color.green), Is.LessThan(_debris.CountCrumbsOfColour(new Color(0.4f, 0.25f, 0.1f))),
        "crumbs come off the side that hit the ground");
}
```

Для последнего теста добавить в `VoxelDebris.cs` отладочный метод:

```csharp
/// <summary>Single flying cubes of about this colour (±0.02 per channel): for tests.</summary>
public int CountCrumbsOfColour(Color color)
{
    Complete();
    int n = 0;
    var c = new float4(color.r, color.g, color.b, color.a);
    for (int i = 0; i < _cubes.Length; i++)
    {
        Cube cube = _cubes[i];
        if (_clusters[cube.Cluster].Count == 1 && (_clusters[cube.Cluster].Flags & DUST) == 0 &&
            math.all(math.abs(cube.Color - c) < 0.02f))
            n++;
    }
    return n;
}
```

- [ ] **Step 2:** Запустить — FAIL (`PartsFor` не существует; частей ≤ 3).

- [ ] **Step 3: `PartsFor`**

```csharp
/// <summary>
/// How many pieces a clump of <paramref name="cubes"/> breaks into when it hits at
/// <paramref name="impactSpeed"/> m/s: more for big clumps and hard hits, 2 at least, MAX_PARTS at most.
/// </summary>
public static int PartsFor(int cubes, float impactSpeed)
{
    float strength = math.saturate(impactSpeed / FULL_IMPACT);
    return math.clamp((int)math.round(math.sqrt(cubes) * strength * 0.45f), 2, MAX_PARTS);
}
```

Сверка таблицы: 300 / 15 → √300·1·0.45 = 7.8 → 8; 300 / 8 → 17.3·0.533·0.45 = 4.2 → 4; 60 / 5 → 7.7·0.333·0.45 = 1.2 → 2; 40 / 3 → 2.

- [ ] **Step 4: удар в `IntegrateJob`.** Там, где сейчас ставится `cluster.Shatter = 1`, сохранить удар:

```csharp
if (cluster.Count > 1 && -vn > SHATTER_SPEED * (cluster.Count <= 3 ? 1.8f : 1f))
{
    cluster.Shatter = 1;
    cluster.Impact = -vn;
    cluster.HitPoint = hit.point;
    cluster.HitNormal = normal;
}
```

Для `DUST` в начале `Execute`: гравитация `Gravity * DUST_GRAVITY` (флаг читается у кластера), при касании — просто останов (`Velocity = 0`, `Resting = 1`), без раскола.

- [ ] **Step 5: `ShatterJob`.** Переписать по правилам (сохранив устройство: дети добавляются в конец `Clusters`, кубики переназначаются, родитель `Life = 0`):
  1. `k = parent.Count < MIN_PIECE ? 0 : PartsFor(parent.Count, parent.Impact)`; при `k == 0` весь кластер — крошка.
  2. Крошка: кубик идёт в крошку, если `math.dot(world - parent.Position, -parent.HitNormal) > parent.Radius * 0.4f - random.NextFloat(0f, cube.Size)` (сторона удара, ~18 % объёма шара; джиттер рвёт край). При `k == 0` — все кубики крошка.
  3. Ёмкость: нужно `k + crumbs (+ dust)` новых кластеров. Сначала посчитать крошку в отдельном проходе (`NativeArray<int> crumbCount` на родителя). Если `Clusters.Length + k + crumbs > Capacity` — крошки нет (кубики к ближнему семени), если и `k` не влезает — не колоть (`Shatter = 0`, как сейчас).
  4. Семена — до `MAX_PARTS` на родителя (`seeds` размера `original * MAX_PARTS`), как сейчас резервуарной выборкой, но только среди кубиков, не ставших крошкой.
  5. Части: `Velocity = parent.Velocity * 0.35f + slide * random.NextFloat(0.5f, 2f) + up * random.NextFloat(1.5f, 3f)`, где `up = new float3(0,1,0)`, `slide` — горизонтальное направление от центра родителя; `Spin` 2–5 рад/с.
  6. Крошка: свой кластер `Count = 1`; `fan = world - HitPoint` без составляющей по нормали, нормализованный (или случайное направление в плоскости); `Velocity = fan * random.NextFloat(1f, 3f) + HitNormal * random.NextFloat(0.5f, 1.5f)`; `Spin` 6–12; `cube.Size *= random.NextFloat(0.75f, 0.95f)`; `Life = parent.Life`, `Flags = parent.Flags & KEEP`, `Flight = parent.Flight`.
  7. Пыль: если `parent.Impact > DUST_SPEED` и место есть — `random.NextInt(6, 13)` кластеров по одному **новому** кубику (`Cubes.Add`, проверить `Cubes.Length < Capacity`): размер 0.04–0.06, цвет — цвет случайного кубика родителя, `rgb = lerp(rgb, 1, 0.2)`, позиция `HitPoint + HitNormal * 0.05`, скорость `random.NextFloat3Direction()` с `y = abs(y)`, × 1–2.5; `Flags = DUST`, `Life = random.NextFloat(0.8f, 1.4f)`.
  8. Смещения кубиков обратно в пространство детей и `Radius` — как сейчас (последний цикл).
- [ ] **Step 6: `CompactJob`:** кластер с `DUST` никогда не переходит в лежачее хранилище (сейчас это только `KEEP`, но в Task 3 правило расширится — оставить проверку `(Flags & DUST) == 0` уже здесь).
- [ ] **Step 7:** Перекомпилировать, `VoxelDebrisCrumbleTests` + `VoxelDebrisKeepTests` — PASS. Если `Pieces_HopAndBreakAgain…` не проходит — подобрать подскок в пределах 1.5–3 м/с и `SHATTER_SPEED` для частей, не меняя таблицу `PartsFor`.
- [ ] **Step 8:** Commit `Debris: clumps break in a cascade - pieces by size and impact, crumbs off the side that hit, a puff of dust`.

---

### Task 3: Лежачее хранилище для всех улёгшихся обломков

Спека, раздел 2 (без вытеснения по оценке — это Task 4). Заменяет кольцо `_lyingMatrices/_lyingColors`.

**Files:**
- Create: `Assets/Game/Voxels/Runtime/VoxelDebris.Lying.cs`
- Modify: `VoxelDebris.cs` (поля, конструктор, `Tick`, отрисовка, `LyingCount`, `TryGetLying`, `Dispose`), `VoxelDebris.Flight.cs` (`IntegrateJob`: `Still` для всех; `CompactJob`: сон для всех, запись в новое хранилище)
- Modify: `Assets/Game/Voxels/Runtime/VoxelWorld.cs:273` — `new VoxelDebris(material, 24000, LYING_DEFAULT)`
- Test: `Assets/Game/Voxels/Tests/VoxelDebrisLyingTests.cs`

**Interfaces — Produces:**

```csharp
/// <summary>A cube lying for good: only drawn.</summary>
private struct LyingCube
{
    public float3 Position;
    public quaternion Rotation;
    /// <summary>Drawn scale: the cube's size, times the splat tile's shape for a laid drop.</summary>
    public float3 Scale;
    public float Size;
    public float4 Color;
    /// <summary>The piece it lay down with: woken cubes of one group fly on as one clump.</summary>
    public int Group;
    /// <summary>When it lay down (VoxelDebris time, s).</summary>
    public float Born;
    /// <summary>0 — lying; > 0 — sinking for this many seconds of SinkTime.</summary>
    public float Sinking;
    public float SinkTime;
    public int Flags;       // LAID (blood tile), HIT (marked by a query this step)
    public int Query;       // which query hit it (index + 1), 0 — none
}

private struct LyingState
{
    public int Count;
    public int NextGroup;
    /// <summary>Where a cube goes when the store is full: walks round, overwriting.</summary>
    public int Overwrite;
    public int Evicting;   // cubes sinking because of eviction (Task 4)
}

public const int LYING_DEFAULT = 100000;
private const float SINK_TIME = 1.5f;
```

Поля `VoxelDebris`: `NativeArray<LyingCube> _lyingCubes`, `NativeArray<float4x4> _lyingMatrices` (рисуется напрямую), `NativeArray<float4> _lyingColors`, `NativeArray<byte> _dirtyBatches` (на каждую пачку из 1023), `NativeReference<LyingState> _lyingState`, `MaterialPropertyBlock[] _lyingBlocks` (по одному на пачку, создаются в конструкторе), `float _time`.

- [ ] **Step 1: падающие тесты** (`VoxelDebrisLyingTests`, каркас как у `VoxelDebrisKeepTests`, `new VoxelDebris(_material, 4096, 2048)`):

```csharp
[Test]
public void Clump_ThatCameToRest_LiesForGood()
{
    int clump = _debris.BeginCluster(new Vector3(0f, 0.3f, 0f), Vector3.zero, 0f);
    for (int i = 0; i < 8; i++)
        _debris.AddCube(clump, new Vector3(i * 0.2f, 0.3f, 0f), 0.2f, Color.gray);
    _debris.SetLife(clump, 5f);

    Run(3f);
    Assert.AreEqual(8, _debris.LyingCount, "came to rest and lies");
    Assert.AreEqual(0, _debris.Count, "no longer simulated");

    Run(30f);
    Assert.AreEqual(8, _debris.LyingCount, "its life is over, but lying cubes do not age");
}

[Test]
public void Dust_NeverLies()
{
    // A hard hit makes dust; after 3 s only crumbs and pieces lie, no dust-sized cubes.
    Clump(new Vector3(0f, 1.2f, 0f), new Vector3(0f, -15f, 0f), 5, Color.gray);
    Run(4f);
    for (int i = 0; i < _debris.LyingCount; i++)
    {
        Assert.IsTrue(_debris.TryGetLying(i, out Matrix4x4 m, out _));
        Assert.That(m.lossyScale.x, Is.GreaterThan(0.1f), "no dust cube lies");
    }
}

[Test]
public void FullStore_OverwritesInsteadOfGrowing()
{
    var small = new VoxelDebris(_material, 4096, 16);
    try
    {
        for (int c = 0; c < 4; c++)
        {
            int clump = small.BeginCluster(new Vector3(c, 0.3f, 0f), Vector3.zero, 0f);
            for (int i = 0; i < 8; i++)
                small.AddCube(clump, new Vector3(c + i * 0.1f, 0.3f, 0f), 0.1f, Color.gray);
        }
        for (float t = 0f; t < 3f; t += 0.02f) small.Tick(0.02f);
        small.Complete();
        Assert.AreEqual(16, small.LyingCount);
    }
    finally { small.Dispose(); }
}
```

Существующие `VoxelDebrisKeepTests` остаются зелёными (кровь — `LAID`, тоже в хранилище).

- [ ] **Step 2:** Запустить — FAIL (`Clump_ThatCameToRest…`: кубики не ложатся без `KEEP`).

- [ ] **Step 3: сон для всех.** `IntegrateJob`: `cluster.Still += DeltaTime` у любого лежащего кластера (не только `KEEP`). `CompactJob`: условие сна — `cluster.Resting != 0 && cluster.Still >= SETTLE_TIME && cluster.Count > 0 && (cluster.Flags & DUST) == 0`. Тест `KeptClump_ThatNeverComesToRest…` (`MAX_KEPT_FLIGHT`) — оставить как есть.

- [ ] **Step 4: запись в хранилище из `CompactJob`.** `CompactJob` получает `Lying` (`NativeArray<LyingCube>`), `Matrices`/`Colors` хранилища, `DirtyBatches`, `State`, `Time`. Каждому усыплённому кластеру — `group = state.NextGroup++`. Для каждого его кубика:

```csharp
int slot;
if (state.Count < Lying.Length)
    slot = state.Count++;
else
{
    slot = state.Overwrite;
    state.Overwrite = (state.Overwrite + 1) % Lying.Length;
}

float3 scale = (settledCluster.Flags & LAID) != 0 ? new float3(SPLAT_WIDTH, SPLAT_HEIGHT, SPLAT_WIDTH) : new float3(1f);
var lying = new LyingCube
{
    Position = settledCluster.Position + math.mul(settledCluster.Rotation, cube.Offset),
    Rotation = settledCluster.Rotation,
    Scale = scale * cube.Size,
    Size = cube.Size,
    Color = cube.Color,
    Group = group,
    Born = Time,
    Flags = settledCluster.Flags & LAID
};
Lying[slot] = lying;
LyingMatrices[slot] = float4x4.TRS(lying.Position, lying.Rotation, lying.Scale);
LyingColors[slot] = lying.Color;
DirtyBatches[slot / BATCH] = 1;
```

- [ ] **Step 5: `LyingJob`** (`IJob`, `[BurstCompile]`, в `VoxelDebris.Lying.cs`) — после `CompactJob` в цепочке шага. Проходит слоты **с конца к началу** (тогда перенос последнего в текущий слот безопасен):
  - `Sinking > 0`: `Sinking += DeltaTime`; `t = Sinking / SinkTime`; если `t >= 1` — удалить слот (swap-remove: `Lying[i] = Lying[count-1]`, то же для матрицы и цвета, `count--`, пометить пачки `i/BATCH` и `count/BATCH` грязными); иначе матрица `TRS(Position - up * Size * t, Rotation, Scale * (1 - t))`.
  - Обработка `HIT` — в Task 5; здесь только сбросить `Flags &= ~HIT; Query = 0`.
  - В конце `state.Count = count`.
  Удаление через swap-remove — единственный способ освободить слот (вытеснение и давка ставят `Sinking`).

- [ ] **Step 6: отрисовка лежачих** (`VoxelDebris.cs`, `Tick` после `Complete()`, вместо второго `Draw`):

```csharp
private void DrawLying()
{
    int count = _lyingState.Value.Count;
    NativeArray<Matrix4x4> matrices = _lyingMatrices.Reinterpret<Matrix4x4>();
    var rp = new RenderParams(_material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
    for (int start = 0, batch = 0; start < count; start += BATCH, batch++)
    {
        int n = Mathf.Min(BATCH, count - start);
        if (_dirtyBatches[batch] != 0)
        {
            NativeArray<Vector4>.Copy(_lyingColors.Reinterpret<Vector4>(), start, _batchColors, 0, n);
            _lyingBlocks[batch].SetVectorArray(InstanceColorId, _batchColors);
            _dirtyBatches[batch] = 0;
        }
        rp.matProps = _lyingBlocks[batch];
        Graphics.RenderMeshInstanced(rp, _mesh, 0, matrices, n, start);
    }
}
```

`_lyingBlocks = new MaterialPropertyBlock[(lyingCapacity + BATCH - 1) / BATCH]` в конструкторе, каждый с `SetVectorArray(InstanceColorId, new Vector4[BATCH])` (фиксирует длину массива). Матрицы не копируются: `RenderMeshInstanced` берёт `NativeArray`. Проверить в редакторе глазами (координатор), что цвета лежачих верные, — если `RenderMeshInstanced` с `startInstance` в этой версии Unity ведёт себя иначе, откатиться на копию матриц только грязных пачек в постоянные `Matrix4x4[]` на пачку и `DrawMeshInstanced`.

- [ ] **Step 7:** `LyingCount` → `_lyingState.Value.Count`; `TryGetLying(slot, …)` читает `_lyingMatrices[slot]` и `_lyingColors[slot]` (с учётом сжатия тонущих — матрица уже сжата). `Dispose` освобождает новые массивы. `VoxelWorld.Awake`: `new VoxelDebris(LoadMaterial(DEBRIS_MATERIAL, true), 24000, VoxelDebris.LYING_DEFAULT)`. Константу `LYING_CAPACITY` удалить (поиск по проекту — только `VoxelDebris.cs`). XML-комментарий класса обновить: лежачее хранилище теперь для всех улёгшихся.
- [ ] **Step 8:** Перекомпилировать; EditMode `VoxelDebrisLyingTests`, `VoxelDebrisCrumbleTests`, `VoxelDebrisKeepTests` — PASS; затем PlayMode `VoxelBloodPlayModeTests`, `VoxelTankCrushTests` — PASS.
- [ ] **Step 9:** Commit `Debris: everything that comes to rest lies for good in a dense store drawn straight from native arrays`.

---

### Task 4: Вытеснение старых и дальних

Спека, раздел 2 «Вытеснение».

**Files:**
- Modify: `VoxelDebris.Lying.cs` (`EvictJob`), `VoxelDebris.cs` (`ViewPoint`, планирование `EvictJob`)
- Modify: `VoxelWorld.cs` — перед `Debris.Tick` задать `Debris.ViewPoint = cam.position` (`Camera.main`, закешировать в поле, перепроверять раз в секунду, если `null`)
- Test: `VoxelDebrisLyingTests.cs`

**Interfaces — Produces:** `public Vector3 ViewPoint { get; set; }` — откуда считать дальность.
Константы: `EVICT_FROM = 0.95f`, `EVICT_SHARE = 0.02f`, `EVICT_AGE = 120f`, `EVICT_DISTANCE = 60f`.

- [ ] **Step 1: падающий тест**

```csharp
[Test]
public void NearlyFullStore_SinksTheOldAndFarFirst_Smoothly()
{
    var store = new VoxelDebris(_material, 8192, 1000);
    try
    {
        store.ViewPoint = Vector3.zero;
        // 980 cubes: 490 near the viewer, 490 far away. All the same age.
        LayRow(store, new Vector3(0f, 0.2f, 0f), 490);
        LayRow(store, new Vector3(0f, 0.2f, 200f), 490);
        Settle(store, 2f);
        Assert.AreEqual(980, store.LyingCount);

        int before = store.LyingCount;
        int lowest = before;
        for (float t = 0f; t < 4f; t += 0.02f)
        {
            store.Tick(0.02f);
            store.Complete();
            Assert.That(before - store.LyingCount, Is.LessThanOrEqualTo(30), "no cliff: a few at a time");
            before = store.LyingCount;
            lowest = Mathf.Min(lowest, before);
        }
        Assert.That(lowest, Is.LessThan(980), "something went");
        Assert.AreEqual(490, store.CountLyingNear(Vector3.zero, 50f), "the near ones all stayed");
    }
    finally { store.Dispose(); }
}
```

Вспомогательные: `LayRow` — кластеры по 10 кубиков 0.1 м вдоль X в ряд на высоте 0.2 м (плоскость теста 5× = 50 м; для ряда на z=200 увеличить плоскость в `SetUp` до `localScale = Vector3.one * 50f`); `Settle(store, s)` — тики. В `VoxelDebris` добавить отладочный `public int CountLyingNear(Vector3 point, float radius)` (проход по `_lyingCubes`).

- [ ] **Step 2:** FAIL.
- [ ] **Step 3: `EvictJob`** (`IJob`, Burst). Планируется в `Tick` после `LyingJob`, только если `state.Count > EVICT_FROM * capacity && state.Evicting == 0`:

```csharp
public void Execute()
{
    LyingState state = State.Value;
    int count = state.Count;
    const int BINS = 64;
    var bins = new NativeArray<int>(BINS, Allocator.Temp);
    float max = 0f;
    for (int i = 0; i < count; i++)
        if (Lying[i].Sinking == 0f)
            max = math.max(max, Score(Lying[i]));
    if (max <= 0f) return;

    for (int i = 0; i < count; i++)
        if (Lying[i].Sinking == 0f)
            bins[math.min(BINS - 1, (int)(Score(Lying[i]) / max * BINS))]++;

    int want = math.max(1, (int)(Lying.Length * EVICT_SHARE));
    int threshold = BINS;
    for (int sum = 0; threshold > 0 && sum < want; )
        sum += bins[--threshold];

    int marked = 0;
    for (int i = 0; i < count && marked < want; i++)
    {
        LyingCube cube = Lying[i];
        if (cube.Sinking != 0f || math.min(BINS - 1, (int)(Score(cube) / max * BINS)) < threshold)
            continue;
        cube.Sinking = 1e-4f;
        cube.SinkTime = SINK_TIME;
        Lying[i] = cube;
        marked++;
    }
    state.Evicting = marked;
    State.Value = state;
}

private float Score(in LyingCube cube) =>
    (Time - cube.Born) / EVICT_AGE + math.distance(cube.Position, ViewPoint) / EVICT_DISTANCE;
```

`LyingJob` уменьшает `state.Evicting` на каждый удалённый слот, у которого `SinkTime == SINK_TIME` (вытеснение), до нуля.
- [ ] **Step 4:** PASS всех `VoxelDebris*Tests`. Commit `Debris: when the lying store fills, the oldest and farthest cubes sink away a few at a time`.

---

### Task 5: Давка, взрыв, осыпание

Спека, разделы 3 и 4.

**Files:**
- Modify: `VoxelDebris.Lying.cs` (`LyingQuery`, `LyingQueryJob`, обработка `HIT` в `LyingJob`, API)
- Modify: `VoxelDebris.cs` (очередь запросов, планирование)
- Modify: `VoxelWorld.cs` (подписка на `VoxelSignals.Carved`, `VoxelSignals.AirBlasted` в `OnEnable/OnDisable`, методы-обработчики — поля-делегаты, без лямбд в кадре)
- Modify: `VoxelTrackRuts.cs` (`VoxelLateUpdate`: давка под катками)
- Test: `VoxelDebrisLyingTests.cs`

**Interfaces — Produces:**

```csharp
/// <summary>Lying cubes in the sphere under a road wheel break: a third fly back as crumbs, the rest sink.</summary>
public void Crush(Vector3 center, float radius, Vector3 velocity);
/// <summary>Lying cubes in the sphere wake and fly away from the centre, harder nearer it; blood stays.</summary>
public void Blow(Vector3 center, float radius, float speed);
/// <summary>Lying cubes in the sphere wake and fall: the ground under them was dug away.</summary>
public void Loosen(Vector3 center, float radius);

private struct LyingQuery { public float3 Center; public float Radius; public float3 Velocity; public float Speed; public int Kind; }
private const int QUERY_CRUSH = 1, QUERY_BLOW = 2, QUERY_LOOSEN = 3;
private const int MAX_QUERIES = 32;   // per frame; past it the rest are dropped
private const int HIT = 16;
private const float CRUSH_SINK_TIME = 0.4f;
```

Запросы пишутся в `NativeList<LyingQuery> _queries` (ёмкость `MAX_QUERIES`, выделена в конструкторе) на главном потоке после `Complete()`; шаг копирует их в задачи и очищает список, когда шаг завершён (в следующем `Tick`).

- [ ] **Step 1: падающие тесты**

```csharp
[Test]
public void Crush_BreaksWhatLiesUnderTheWheel()
{
    LayPiece(_debris, new Vector3(0f, 0.2f, 0f), 12, Color.gray);
    LayPiece(_debris, new Vector3(5f, 0.2f, 0f), 12, Color.gray);
    Run(2f);
    Assert.AreEqual(24, _debris.LyingCount);

    _debris.Crush(new Vector3(0f, 0.2f, 0f), 0.6f, new Vector3(0f, 0f, 3f));
    Run(1f);
    Assert.AreEqual(12, _debris.CountLyingNear(new Vector3(5f, 0.2f, 0f), 1f), "the far piece is untouched");
    Assert.That(_debris.CountLyingNear(Vector3.zero, 0.6f), Is.LessThanOrEqualTo(4 + 0), "crushed: crumbs flew, the rest sank");
}

[Test]
public void Crush_LeavesBlood()
{
    _debris.Drop(new Vector3(0f, 0.5f, 0f), Vector3.zero, Color.red, 0.05f);
    Run(1f);
    _debris.Crush(Vector3.zero, 0.6f, Vector3.forward);
    Run(1f);
    Assert.AreEqual(1, _debris.LyingCount);
}

[Test]
public void Blow_ThrowsAPieceUpAsOneClump()
{
    LayPiece(_debris, new Vector3(1f, 0.2f, 0f), 12, Color.gray);
    Run(2f);
    _debris.Blow(Vector3.zero, 4f, 10f);
    _debris.Tick(0.02f); _debris.Tick(0.02f); _debris.Complete();
    var sizes = new List<int>();
    _debris.GetClusterSizes(sizes);
    CollectionAssert.Contains(sizes, 12, "the piece flies on whole");
    Assert.AreEqual(0, _debris.LyingCount);
}

[Test]
public void Loosen_DropsWithoutAPush()
{
    LayPiece(_debris, new Vector3(0f, 0.2f, 0f), 12, Color.gray);
    Run(2f);
    _debris.Loosen(Vector3.zero, 1f);
    _debris.Tick(0.02f); _debris.Tick(0.02f); _debris.Complete();
    Assert.AreEqual(1, _debris.ClusterCount);
    Run(2f);
    Assert.AreEqual(12, _debris.CountLyingNear(Vector3.zero, 1.5f), "fell back where it was");
}
```

`LayPiece(store, at, n, colour)` — кластер из n кубиков 0.1 м в ряд по X, `Vector3.zero` скорость.

- [ ] **Step 2:** FAIL.
- [ ] **Step 3: `LyingQueryJob`** (`IJobParallelFor` по слотам, батч 256, Burst, только если есть запросы): для каждого слота без `Sinking` и без `LAID` (для `QUERY_CRUSH`, `QUERY_BLOW`, `QUERY_LOOSEN` кровь всегда пропускается) — первый запрос, в сферу которого попадает `Position`, пишет `Flags |= HIT; Query = q + 1`.
- [ ] **Step 4: обработка `HIT` в `LyingJob`** (до обработки тонущих в том же проходе с конца). `LyingJob` получает `Clusters`, `Cubes`, `Capacity` летящих, `Queries`, `Seed`. Временные `NativeParallelHashMap<int, int> groupCluster` и `NativeParallelHashMap<int, int> groupCrumbs` (`Allocator.Temp`, ёмкость 256).
  - `QUERY_CRUSH`: `random.NextFloat() < 1/3` и `groupCrumbs[group] < 6` и место в летящих → крошка: новый кластер `Count = 1` в позиции кубика, `Velocity = -query.Velocity * random.NextFloat(0.3f, 0.6f) + up * random.NextFloat(1f, 3f)` (назад из-под гусеницы), `Life = 10`, кубик `Size * 0.8`; слот удаляется (swap-remove). Иначе `Sinking = 1e-4f; SinkTime = CRUSH_SINK_TIME`.
  - `QUERY_BLOW` / `QUERY_LOOSEN`: найти/создать кластер группы (`groupCluster`): создаётся в позиции первого кубика, `Rotation = cube.Rotation`, `Life = 20`, `Spin` 2–6 (для `LOOSEN` 0), скорость: `BLOW` — `dir = normalize(Position - Center)`, `dir.y = max(dir.y, 0.5)`, renormalize, `× Speed * (1 - d / Radius)`; `LOOSEN` — 0. Кубик добавляется: `Offset = mul(inverse(cluster.Rotation), cube.Position - cluster.Position)`, `cluster.Count++`, `Radius = max(Radius, length(Offset) + Size * 0.5)`. Если летящих мест нет — `Sinking` как при вытеснении. Слот удаляется.
  - Записи новых кластеров/кубиков — в `Clusters`/`Cubes` (это `NativeList`, задача однопоточная — безопасно). Новые кластеры появляются в следующем шаге.
- [ ] **Step 5: API**: `Crush/Blow/Loosen` — `Complete()`, `if (_queries.Length < MAX_QUERIES) _queries.Add(...)`. В `Tick`: `LyingQueryJob` и `LyingJob` получают `_queries.AsArray()`; после завершения шага (в начале следующего `Tick`, после `Complete()`) — `_queries.Clear()`. Внимание: запросы, добавленные между `Complete()` и `Tick` в одном кадре, должны попасть в шаг этого `Tick` — очищать в `Tick` **после** планирования нельзя (задача читает список); держать два списка и менять местами (`_queries` пишется, `_queriesInFlight` читается шагом).
- [ ] **Step 6: `VoxelWorld`:** 

```csharp
private Action<Vector3, float, int> _onCarved;
private Action<Vector3, float, int> _onAirBlasted;

// Awake: _onCarved = OnCarved; _onAirBlasted = OnAirBlasted;
// OnEnable: VoxelSignals.Carved += _onCarved; VoxelSignals.AirBlasted += _onAirBlasted;
// OnDisable: -= both.

/// <summary>A shell or a blast dug the ground: what lay there flies, what lay round the hole falls in.</summary>
private void OnCarved(Vector3 point, float radius, int voxels)
{
    Debris?.Blow(point, radius * 1.5f, 4f + radius * 3f);
    Debris?.Loosen(point, radius + 0.3f);
}

/// <summary>An air bomb: everything lying within twice the crater flies.</summary>
private void OnAirBlasted(Vector3 point, float radius, int voxels)
{
    Debris?.Blow(point, radius * 2f, 14f);
    Debris?.Loosen(point, radius + 0.3f);
}
```

Внимание: `AirBlast` бросает свои новые комья **до** `RaiseAirBlasted` — они ещё летят (не лежат), запрос их не тронет. Правильно.
- [ ] **Step 7: `VoxelTrackRuts.VoxelLateUpdate`:** после цикла `Bite`, если `speed > 0.5f` и `VoxelWorld.Instance?.Debris` не `null` — для каждого катка `_wheelList[i]`: `debris.Crush(wheel.position - Vector3.up * _wheelRadius * 0.5f, _wheelRadius + 0.1f, velocity)`. Катков 16 → 16 запросов в кадр, `MAX_QUERIES = 32` хватает на танк + 2 взрыва; если танков станет больше — объединять сферы по борту (не сейчас).
- [ ] **Step 8:** PASS всех `VoxelDebris*Tests`, PlayMode `VoxelTankCrushTests`, `VoxelBloodPlayModeTests`. Commit `Debris: the tank crushes lying debris, blasts throw it up again, dug-away ground drops it`.

---

### Task 6: `ThrowEarth`

Спека, раздел 4а. Сигнатура уже передана команде «танк и воронка» — не менять.

**Files:**
- Create: `Assets/Game/Voxels/Runtime/VoxelDebris.Earth.cs`
- Test: `Assets/Game/Voxels/Tests/VoxelDebrisEarthTests.cs`

**Interfaces — Produces:**

```csharp
public struct EarthThrow
{
    public Vector3 Point;     // where the earth leaves the ground, world space
    public Vector3 Velocity;  // mean velocity of the throw, m/s
    public float Volume;      // m³ of earth thrown; ~0.008 m³ per 20 cm cube
    public float Spread;      // 0 — one tight clump, 1 — a wide fan of small clumps and crumbs
    public Color32 Top;       // turf / surface colour (top cubes)
    public Color32 Body;      // earth under it (dry or wet mud)
}

/// <summary>Throws earth as a few clumps, crumbs and a puff of dust. Returns cubes thrown; 0 when full.</summary>
public int ThrowEarth(in EarthThrow earth);
```

- [ ] **Step 1: падающие тесты**

```csharp
[Test]
public void ThrowEarth_SplitsTheVolumeIntoClumpsAndCrumbs()
{
    int cubes = _debris.ThrowEarth(new EarthThrow
    {
        Point = new Vector3(0f, 0.5f, 0f), Velocity = new Vector3(0f, 6f, 4f),
        Volume = 0.8f, Spread = 1f, Top = Color.green, Body = new Color(0.4f, 0.25f, 0.1f)
    });
    Assert.That(cubes, Is.InRange(95, 125), "0.8 m³ / 0.008 = 100 cubes, plus a little dust");
    var sizes = new List<int>();
    _debris.GetClusterSizes(sizes);
    int clumps = 0, singles = 0;
    foreach (int n in sizes) { if (n > 1) clumps++; else singles++; }
    Assert.AreEqual(4, clumps, "Spread 1 → 4 clumps");
    Assert.That(singles, Is.GreaterThanOrEqualTo(15), "18 % crumbs (+ dust)");
}

[Test]
public void ThrowEarth_TightThrowIsOneClump_NoDustBelowItsVolume()
{
    int cubes = _debris.ThrowEarth(new EarthThrow
    {
        Point = new Vector3(0f, 0.5f, 0f), Velocity = Vector3.up * 3f, Volume = 0.04f, Spread = 0f,
        Top = Color.green, Body = Color.gray
    });
    Assert.AreEqual(5, cubes, "0.04 / 0.008: no dust under 0.05 m³");
}

[Test]
public void ThrowEarth_ReturnsZeroWhenFull()
{
    var full = new VoxelDebris(_material, 8, 8);
    try
    {
        Assert.AreEqual(0, full.ThrowEarth(new EarthThrow { Point = Vector3.up, Volume = 1f, Top = Color.green, Body = Color.gray }));
    }
    finally { full.Dispose(); }
}
```

- [ ] **Step 2:** FAIL.
- [ ] **Step 3: реализация** (главный поток, без выделений):
  - `total = max(1, round(Volume / 0.008))`; `crumbs = total < MIN_PIECE ? 0 : round(total * CRUMB_SHARE)`; `inClumps = total - crumbs`; `k = clamp(1 + round(Spread * 3), 1, 4)`, но не больше `inClumps / MIN_PIECE` (не меньше 1); `dust = Volume > 0.05 ? 6 + round(Spread * 6) : 0`.
  - Если `!HasRoom(total + dust)` — вернуть 0.
  - Форма кома: статическая таблица `s_ballOffsets` (`int3[]`, кубики сетки в шаре радиуса 4, отсортированы по расстоянию до центра; строится один раз в статическом конструкторе). Ком из `m` кубиков берёт первые `m` смещений × 0.2 м; кубики верхнего слоя (наибольший `y` среди взятых) — `Top`, остальные — `Body`.
  - Ком `j`: позиция `Point + horizontal jitter * Spread * 0.5`, скорость `Velocity + Random.insideUnitSphere * (Velocity.magnitude * 0.3f * Spread)`, `spin` 2–6.
  - Крошка — `Spawn(Point + jitter, Velocity * U(0.6, 1.1) + Random.insideUnitSphere * 2 * (0.3 + Spread), Body, 0.2 * U(0.75, 0.95))`.
  - Пыль — `AddDust(Point, Body, dust)`: общий помощник с `ShatterJob` по смыслу, но на главном потоке (кластер `DUST`, правила раздела «Пыль» спеки). Вынести константы пыли в одно место.
  - Возврат — `total + dust`.
- [ ] **Step 4:** PASS. Commit `Debris: ThrowEarth - a public way to throw earth as clumps, crumbs and dust`.

---

### Task 7: Замеры, скриншоты, отчёт (координатор, не субагент)

- [ ] **Step 1:** Скриншоты VoxelSandbox до/после: бомба (`VoxelAirStrike.Explode` в 10 м перед танком), кадры 0 / 0.3 / 1 / 5 с, Game View. Отправить пользователю (`SendUserFile`), сохранить в заметки группы.
- [ ] **Step 2:** `/perf-check` сценарий авиаудара до (на `game/develop`) и после, при загрузке CPU < 25 %.
- [ ] **Step 3:** Потолок: в Play заполнить хранилище (бомбы подряд или `ThrowEarth` по сетке) до 60 / 100 / 150 / 200 тыс. лежачих (ёмкость выставить через `VoxelWorld`), снять PlayerLoop, `Camera.Render`, GC. Таблицу и предложение — пользователю, затем выставить `LYING_DEFAULT`.
- [ ] **Step 4:** Ревью кода субагентом-ревьюером всей ветки; исправления.
- [ ] **Step 5:** Обновить спеку (раздел «Проверка» — фактические цифры), отчёт тимлиду.

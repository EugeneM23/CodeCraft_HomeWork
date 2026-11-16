# Воксельная смерть Regax — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Regax при смерти становится вокселями — жёсткими кусками плоти с зелёной кровью-кубиками,
которые лежат до конца игры; попадание по живому брызжет кубиками; кровь из пакета с Regax уходит.

**Architecture:** В `Game.Voxels` (от игры не зависит): `VoxelDebris` учится «вечным» кубикам и
каплям-плашкам, `VoxelBody` — запуску целого тела, засыпанию и пробуждению; новые
`SkinnedVoxelizer` (поза → воксели с частями тела), `VoxelCarcass` (сетка → тела по кластерам),
`VoxelSpray`, `VoxelDrip`. В игре: правила гибов (`GibRules`) выносятся из `Gibs` и общие для
Maya- и воксельных гибов; `VoxelGibsOnDeathBehaviour` и `VoxelBloodBehaviour` встают на Regax
вместо гибов и крови пакета; меню **Tools/Regax/Use Voxel Death** перестраивает префаб.

**Tech Stack:** Unity 6000.0.38, Built-in RP, Burst/Jobs/Collections/Mathematics, Atomic.Entities,
NUnit (EditMode — `Game.Tests`; PlayMode — `Game.Voxels.Tests`, `Game.Voxels.SceneTests`).

## Global Constraints
- Спека: `Docs/superpowers/specs/2026-11-16-regax-voxel-death.md`.
- `Game.Voxels` не ссылается на `Game`. Всё про Regax (имена костей, цвета) — только в игре.
- **Коммитятся только документы в `Docs/`** (папка в git — `Docs/` с большой буквы); код остаётся
  незакоммиченным в рабочем дереве. Поэтому в задачах нет шагов «Commit» для кода.
- Общий редактор Unity (MCP, порт 7800), в нём работают другие сессии и пользователь:
  - перекомпиляция / `AssetDatabase.Refresh` — только вне Play, проверка в том же `script_eval`:
    `if (UnityEditor.EditorApplication.isPlaying) return "abort: Play"; UnityEditor.AssetDatabase.Refresh(); return "ok";`
  - если редактор в Play — спросить пользователя, прежде чем останавливать;
  - тесты (`test_run`) — только если `scene_list_open` не показывает ни одной `isDirty: true`
    сцены, иначе модальное окно сохранения вешает редактор;
  - не сохранять сцены с чужими несохранёнными правками.
- Числа из спеки: воксель 0.05 м; разлом кусков от 7 м/с; пробуждение лежащего куска от
  carve-силы > 5 м/с; уснувший кусок — 1 с покоя; капля 0.025 м, плашка ×1.8 по ширине, ×0.3 по
  высоте; лежащих кубиков ≤ 60 000; капли: шов 40, смертельное попадание 60, пуля 6–12,
  ближний бой 10–16, взрыв 20; кровь с куска 0.6 с × 30/с; швы 50/40/40/35/35/20/20 %.
- Комментарии в коде — на английском, объясняют «почему», как в остальном `Game.Voxels`.

## Уточнения к спеке (решены при планировании)
- Шар взрыва по трупу делает сам `ExplosionUseCase.Explode`: `DamageInRadius` убивает Regax
  (труп появляется внутри события смерти), а `VoxelDamage.Blast` вызывается после — и задевает
  свежие куски. Своего `Blast` в смерти не нужно. Toughness кусков 0.6: снаряд (5 м × 0.32 =
  1.6 м) выгрызает шар ~1 м, а не съедает пол-трупа.
- Островки внутри кластера отделяет готовый `VoxelBody.Split`: ≤ 12 вокселей — комья
  (`CRUMBLE_MAX`), больше — тела. Отдельный порог 60 из спеки не вводится.
- Капля, улетевшая ниже −60 м, удаляется: «вечные» кубики иначе падали бы бесконечно.
- Вместо открытого `MakeDynamic` у `VoxelBody` появляется `Launch(velocity, spin)`: масса
  считается из вокселей, вращение задаётся вместе со скоростью.
- `Tools/Regax/Use Voxel Death` не удаляет `CharacterVfxInstaller` (он общий с зомби), а только
  очищает у Regax ссылки `_bulletBlood` / `_meleeBlood`.

## Файлы

| Файл | Что |
|---|---|
| `Assets/Game/Voxels/Runtime/VoxelDebris.cs` | изменить: `Keep`, капли `Drop`, «лежащие» кубики, `DropMask` |
| `Assets/Game/Voxels/Runtime/VoxelTypes.cs` | изменить: `Settle`, `KeepDebris`, `FractureSpeed` в `VoxelBodySettings` |
| `Assets/Game/Voxels/Runtime/VoxelBody.cs` | изменить: `Launch`, `SplitLoose`, `IsLoose`, `Freeze`, пробуждение, `Keep` обломков |
| `Assets/Game/Voxels/Runtime/VoxelImpactFracture.cs` | изменить: свойство `MinSpeed` |
| `Assets/Game/Voxels/Runtime/VoxelSettle.cs` | создать: снимает `Rigidbody` с уснувшего тела |
| `Assets/Game/Voxels/Runtime/MeshVoxelizer.cs` | изменить: `Cross` → общий `internal static CrossZ` |
| `Assets/Game/Voxels/Runtime/FleshPalette.cs` | создать: палитра кожа / мясо / кровь |
| `Assets/Game/Voxels/Runtime/SkinnedVoxelizer.cs` | создать: поза скина → воксели + части |
| `Assets/Game/Voxels/Runtime/VoxelCarcass.cs` | создать: сетка → тела по кластерам, рваные грани |
| `Assets/Game/Voxels/Runtime/VoxelSpray.cs` | создать: веер капель |
| `Assets/Game/Voxels/Runtime/VoxelDrip.cs` | создать: капли с летящего куска |
| `Assets/Game/Voxels/Tests/SkinnedColumn.cs` | создать: тестовый скин-столбик из двух костей |
| `Assets/Game/Voxels/Tests/VoxelDebrisKeepTests.cs` | создать |
| `Assets/Game/Voxels/Tests/VoxelBodySettleTests.cs` | создать |
| `Assets/Game/Voxels/Tests/SkinnedVoxelizerTests.cs` | создать |
| `Assets/Game/Voxels/Tests/VoxelCarcassTests.cs` | создать |
| `Assets/Game/Voxels/Tests/Scene/Game.Voxels.SceneTests.asmdef` | изменить: ссылка на `Game.Voxels.Tests` |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/GibRules.cs` | создать: швы, кластеры, скорости, разбор удара |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/Gibs.cs` | изменить: через `GibRules` |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/GibsOnDeathBehaviour.cs` | изменить: через `GibRules.ReadHit` |
| `Assets/Game/Tests/Features/Character/Common/Gibs/GibRulesTests.cs` | создать (EditMode) |
| `Assets/Game/Scripts/Features/Health/Vfx/BloodLook.cs` | создать: цвета и размер капель |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsConfig.cs` | создать: части, швы, цвета Regax |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsRig.cs` | создать: кости по именам, плоскость позвоночника |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibs.cs` | создать: смерть в воксели |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsOnDeathBehaviour.cs` | создать |
| `Assets/Game/Scripts/Features/Character/Common/Gibs/CharacterVoxelGibsInstaller.cs` | создать |
| `Assets/Game/Voxels/Tests/Scene/VoxelGibsPlayModeTests.cs` | создать |
| `Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodConfig.cs` | создать |
| `Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodBehaviour.cs` | создать |
| `Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodInstaller.cs` | создать |
| `Assets/Game/Voxels/Tests/Scene/VoxelBloodPlayModeTests.cs` | создать |
| `Assets/Game/Scripts/Editor/RegaxVoxelDeathBuilder.cs` | создать: меню Use Voxel Death |
| `Assets/Game/Scripts/Editor/RegaxGibsBuilder.cs` | изменить: `FindDeep` → `internal` |
| `Assets/Game/Scripts/Editor/RegaxPrefabBuilder.cs` | изменить: Build в конце вызывает Use Voxel Death |
| `Assets/Game/Prefabs/Characters/Regax.prefab`, `SK_Rapax.fbx.meta` | меняет меню (Task 8) |

## Как гонять тесты и компиляцию (во всех задачах)
- Компиляция: `script_eval` с проверкой Play (см. Global Constraints), затем `console_get` —
  ошибок компиляции нет.
- Тесты: `scene_list_open` → ни одной грязной сцены → `test_run` (mode PlayMode или EditMode,
  фильтр по полному имени класса, например `Game.Voxels.Tests.VoxelDebrisKeepTests`) →
  `test_status` до конца. Ожидание «FAIL» на шаге 2 значит: не компилируется или тест падает.

---

### Task 1: `VoxelDebris` — вечные кубики и капли-плашки

**Files:**
- Modify: `Assets/Game/Voxels/Runtime/VoxelDebris.cs`
- Test: `Assets/Game/Voxels/Tests/VoxelDebrisKeepTests.cs`

**Interfaces:**
- Produces:
  - `public const int VoxelDebris.LYING_CAPACITY = 60000;`
  - `public VoxelDebris(Material material, int capacity, int lyingCapacity = LYING_CAPACITY)`
  - `public int LyingCount { get; }` — сколько кубиков лежит навсегда
  - `public bool TryGetLying(int slot, out Matrix4x4 matrix, out Color color)`
  - `public int DropMask { get; }` — `CollisionMask` без слоя `VoxelWorld.PieceLayer`
  - `public void SetKeep(int cluster)` — ком не стареет и, улёгшись, переходит в лежащие
  - `public int Drop(Vector3 position, Vector3 velocity, Color32 color, float size)` — капля, −1 если места нет
  - `public int Spawn(...)` теперь возвращает id кластера (было `void`)

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Game/Voxels/Tests/VoxelDebrisKeepTests.cs
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>A debris store of its own, ticked by hand over a plane: no frames needed.</summary>
    public sealed class VoxelDebrisKeepTests
    {
        private const float STEP = 0.02f;

        private VoxelDebris _debris;
        private GameObject _floor;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floor.transform.localScale = Vector3.one * 5f;
            Physics.SyncTransforms();
            _material = new Material(Shader.Find("Standard")) { enableInstancing = true };
            _debris = new VoxelDebris(_material, 256, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _debris.Dispose();
            Object.Destroy(_floor);
            Object.Destroy(_material);
        }

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds; t += STEP)
                _debris.Tick(STEP);
            _debris.Complete();
        }

        [Test]
        public void Drop_LandsAsAFlatTileThatStays()
        {
            _debris.Drop(new Vector3(0f, 1f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(2f);

            Assert.AreEqual(1, _debris.LyingCount);
            Assert.AreEqual(0, _debris.Count, "a lying cube is no longer simulated");
            Assert.IsTrue(_debris.TryGetLying(0, out Matrix4x4 matrix, out _));
            Vector3 scale = matrix.lossyScale;
            Assert.That(scale.y, Is.LessThan(scale.x * 0.5f), "spread flat");
            Assert.That(matrix.GetPosition().y, Is.EqualTo(0f).Within(0.02f), "on the floor");

            Run(60f);
            Assert.AreEqual(1, _debris.LyingCount, "and it never goes");
        }

        [Test]
        public void KeptClump_LiesForGood()
        {
            int clump = _debris.BeginCluster(new Vector3(0f, 0.5f, 0f), Vector3.zero, 0f);
            for (int i = 0; i < 3; i++)
                _debris.AddCube(clump, new Vector3(i * 0.05f, 0.5f, 0f), 0.05f, Color.red);
            _debris.SetKeep(clump);

            Run(65f);

            Assert.AreEqual(3, _debris.LyingCount, "every cube, even if the clump broke on landing");
            Assert.AreEqual(0, _debris.Count);
        }

        [Test]
        public void UnkeptClump_StillGoes()
        {
            int clump = _debris.BeginCluster(new Vector3(0f, 0.5f, 0f), Vector3.zero, 0f);
            _debris.AddCube(clump, new Vector3(0f, 0.5f, 0f), 0.05f, Color.red);

            Run(45f);

            Assert.AreEqual(0, _debris.Count);
            Assert.AreEqual(0, _debris.LyingCount);
        }

        [Test]
        public void Lying_OverCapacity_TheOldestGo()
        {
            for (int i = 0; i < 6; i++)
                _debris.Drop(new Vector3(i, 1f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(2f);

            Assert.AreEqual(4, _debris.LyingCount);
            var xs = new System.Collections.Generic.HashSet<int>();
            for (int slot = 0; slot < 4; slot++)
            {
                _debris.TryGetLying(slot, out Matrix4x4 matrix, out _);
                xs.Add(Mathf.RoundToInt(matrix.GetPosition().x));
            }

            CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5 }, xs);
        }

        [Test]
        public void Drop_PassesThroughLoosePieces()
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.layer = VoxelWorld.PieceLayer;
            piece.transform.position = new Vector3(0f, 0.5f, 0f);
            Physics.SyncTransforms();

            _debris.Drop(new Vector3(0f, 1.5f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(2f);
            Object.DestroyImmediate(piece);

            Assert.IsTrue(_debris.TryGetLying(0, out Matrix4x4 matrix, out _));
            Assert.That(matrix.GetPosition().y, Is.EqualTo(0f).Within(0.02f), "on the floor, not on the piece");
        }

        [Test]
        public void Drop_FallingOffTheWorld_IsDropped()
        {
            Object.DestroyImmediate(_floor);
            _floor = new GameObject("NoFloor");
            Physics.SyncTransforms();

            _debris.Drop(new Vector3(0f, 1f, 0f), Vector3.zero, Color.green, 0.025f);
            Run(6f);

            Assert.AreEqual(0, _debris.Count);
            Assert.AreEqual(0, _debris.LyingCount);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Компиляция (см. «Как гонять»). Ожидание: ошибки компиляции — нет `Drop`, `SetKeep`, `LyingCount`,
`TryGetLying`, конструктора с тремя аргументами.

- [ ] **Step 3: Write minimal implementation**

В `VoxelDebris.cs`:

1. Константы после `SHATTER_SPEED`:

```csharp
        /// <summary>Lying cubes kept at most; past this the oldest go, or the frame would grow all game.</summary>
        public const int LYING_CAPACITY = 60000;

        /// <summary>How long (s) a kept clump lies still before its cubes join the lying ones.</summary>
        private const float SETTLE_TIME = 0.5f;

        /// <summary>A landed drop is a tile: this much wider and this much flatter than its cube.</summary>
        private const float SPLAT_WIDTH = 1.8f;
        private const float SPLAT_HEIGHT = 0.3f;

        /// <summary>Below this a cube has fallen out of the world, kept or not.</summary>
        private const float FELL_OUT_Y = -60f;

        /// <summary>Never ages; once still, its cubes lie for good.</summary>
        private const int KEEP = 1;
        /// <summary>A drop: spreads into a tile where it first touches, and lies at once.</summary>
        private const int SPLAT = 2;
        /// <summary>A drop that has landed: drawn as a tile.</summary>
        private const int LAID = 4;

        /// <summary>Marks a clump in the compact pass whose cubes move to the lying store.</summary>
        private const int SETTLED = -2;
```

2. В `struct Cluster` добавить поля:

```csharp
            public int Flags;
            /// <summary>How long a kept clump has rested, s.</summary>
            public float Still;
```

3. Поля класса после `_alive`:

```csharp
        private NativeArray<float4x4> _lyingMatrices;
        private NativeArray<float4> _lyingColors;
        /// <summary>x — the slot the next lying cube goes to; y — how many slots are filled.</summary>
        private NativeReference<int2> _lying;
```

4. Конструктор:

```csharp
        public VoxelDebris(Material material, int capacity, int lyingCapacity = LYING_CAPACITY)
        {
            _capacity = capacity;
            _material = material;
            _mesh = BuildCubeMesh();

            _clusters = new NativeList<Cluster>(capacity, Allocator.Persistent);
            _cubes = new NativeList<Cube>(capacity, Allocator.Persistent);
            _commands = new NativeArray<RaycastCommand>(capacity, Allocator.Persistent);
            _hits = new NativeArray<RaycastHit>(capacity, Allocator.Persistent);
            _matrices = new NativeArray<float4x4>(capacity, Allocator.Persistent);
            _colors = new NativeArray<float4>(capacity, Allocator.Persistent);
            _alive = new NativeReference<int>(Allocator.Persistent);

            int lying = math.max(1, lyingCapacity);
            _lyingMatrices = new NativeArray<float4x4>(lying, Allocator.Persistent);
            _lyingColors = new NativeArray<float4>(lying, Allocator.Persistent);
            _lying = new NativeReference<int2>(Allocator.Persistent);
        }
```

5. Публичное API (после `Count`):

```csharp
        /// <summary>Cubes lying for good (kept clumps that came to rest, landed drops).</summary>
        public int LyingCount
        {
            get
            {
                Complete();
                return _lying.IsCreated ? _lying.Value.y : 0;
            }
        }

        /// <summary>One lying cube, by its slot (0 … LyingCount − 1): for tests and debugging.</summary>
        public bool TryGetLying(int slot, out Matrix4x4 matrix, out Color color)
        {
            Complete();
            matrix = default;
            color = default;
            if (!_lying.IsCreated || slot < 0 || slot >= _lying.Value.y)
                return false;

            matrix = _lyingMatrices[slot];
            float4 c = _lyingColors[slot];
            color = new Color(c.x, c.y, c.z, c.w);
            return true;
        }

        /// <summary>
        /// What a drop lands on: what any debris lands on, but loose voxel pieces. A piece may move
        /// off later and leave a tile hanging in the air.
        /// </summary>
        public int DropMask => CollisionMask & ~(1 << VoxelWorld.PieceLayer);

        /// <summary>The clump begun this frame never ages: once it has lain still a moment its cubes lie for good.</summary>
        public void SetKeep(int cluster)
        {
            Complete();
            if (cluster < 0 || cluster >= _clusters.Length)
                return;
            Cluster c = _clusters[cluster];
            c.Flags |= KEEP;
            c.Life = float.PositiveInfinity;
            _clusters[cluster] = c;
        }

        /// <summary>A drop of blood: one cube that flies, spreads into a tile where it lands and lies there for good. -1 when full.</summary>
        public int Drop(Vector3 position, Vector3 velocity, Color32 color, float size)
        {
            int cluster = BeginCluster(position, velocity, 0f);
            if (cluster < 0)
                return -1;

            SetKeep(cluster);
            Cluster c = _clusters[cluster];
            c.Flags |= SPLAT;
            _clusters[cluster] = c;
            AddCube(cluster, position, size, color);
            return cluster;
        }
```

6. `Spawn` возвращает кластер:

```csharp
        /// <summary>A single crumb. Returns its cluster (-1 when full), so the caller can keep it.</summary>
        public int Spawn(Vector3 position, Vector3 velocity, Color32 color, float size)
        {
            int cluster = BeginCluster(position, velocity, 10f);
            ShortenLife(cluster, UnityEngine.Random.Range(8f, 14f));
            AddCube(cluster, position, size * UnityEngine.Random.Range(0.75f, 1f), color);
            return cluster;
        }
```

7. `Tick`: рисование и задачи.

```csharp
        public void Tick(float deltaTime)
        {
            Complete();
            if (!_clusters.IsCreated)
                return;

            Draw(_matrices, _colors, _alive.Value, ShadowCastingMode.On);
            // Lying cubes cast no shadow: tens of thousands of tiles and crumbs, flat on the ground.
            Draw(_lyingMatrices, _lyingColors, _lying.Value.y, ShadowCastingMode.Off);

            int clusters = _clusters.Length;
            if (clusters == 0 || deltaTime <= 0f)
            {
                _alive.Value = 0;
                return;
            }

            float dt = math.min(deltaTime, 1f / 20f);

            JobHandle handle = new BuildRaysJob
            {
                Clusters = _clusters.AsArray(),
                Commands = _commands,
                DeltaTime = dt,
                Query = new QueryParameters(CollisionMask, false, QueryTriggerInteraction.Ignore, false),
                DropQuery = new QueryParameters(DropMask, false, QueryTriggerInteraction.Ignore, false),
                Scene = Physics.defaultPhysicsScene
            }.Schedule(clusters, 128);

            handle = RaycastCommand.ScheduleBatch(_commands.GetSubArray(0, clusters), _hits.GetSubArray(0, clusters), 64, 1, handle);

            handle = new IntegrateJob
            {
                Clusters = _clusters.AsArray(),
                Hits = _hits,
                DeltaTime = dt,
                Gravity = (float3)Physics.gravity * GRAVITY_SCALE,
                Seed = _seed
            }.Schedule(clusters, 128, handle);

            handle = new ShatterJob
            {
                Clusters = _clusters,
                Cubes = _cubes,
                Capacity = _capacity,
                Seed = _seed++
            }.Schedule(handle);

            handle = new CompactJob
            {
                Clusters = _clusters,
                Cubes = _cubes,
                Matrices = _matrices,
                Colors = _colors,
                Alive = _alive,
                Lying = _lyingMatrices,
                LyingColors = _lyingColors,
                LyingState = _lying
            }.Schedule(handle);

            _step = handle;
            _stepping = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private void Draw(NativeArray<float4x4> source, NativeArray<float4> sourceColors, int count, ShadowCastingMode shadows)
        {
            NativeArray<Matrix4x4> matrices = source.Reinterpret<Matrix4x4>();
            NativeArray<Vector4> colors = sourceColors.Reinterpret<Vector4>();

            for (int start = 0; start < count; start += BATCH)
            {
                int n = Mathf.Min(BATCH, count - start);
                NativeArray<Matrix4x4>.Copy(matrices, start, _batchMatrices, 0, n);
                NativeArray<Vector4>.Copy(colors, start, _batchColors, 0, n);
                _block.SetVectorArray(InstanceColorId, _batchColors);
                Graphics.DrawMeshInstanced(_mesh, 0, _material, _batchMatrices, n, _block, shadows, true);
            }
        }
```

8. `Dispose` — добавить:

```csharp
            if (_lyingMatrices.IsCreated) _lyingMatrices.Dispose();
            if (_lyingColors.IsCreated) _lyingColors.Dispose();
            if (_lying.IsCreated) _lying.Dispose();
```

9. `BuildRaysJob`: поле `public QueryParameters DropQuery;` и выбор запроса:

```csharp
            public void Execute(int i)
            {
                Cluster cluster = Clusters[i];
                float reach = math.max(0.03f, cluster.Radius * 0.6f);
                QueryParameters query = (cluster.Flags & SPLAT) != 0 ? DropQuery : Query;

                if (cluster.Resting != 0)
                {
                    Commands[i] = new RaycastCommand(Scene, cluster.Position, Vector3.down, query, reach * 1.6f);
                    return;
                }

                float speed = math.length(cluster.Velocity);
                float3 direction = speed > 0.0001f ? cluster.Velocity / speed : new float3(0f, -1f, 0f);
                Commands[i] = new RaycastCommand(Scene, cluster.Position, direction, query, speed * DeltaTime + reach);
            }
```

10. `IntegrateJob`: поле `public uint Seed;`, начало `Execute` и укладка капли.

```csharp
            public void Execute(int i)
            {
                Cluster cluster = Clusters[i];
                cluster.Life -= DeltaTime;

                if (cluster.Position.y < FELL_OUT_Y)
                {
                    cluster.Life = 0f;
                    cluster.Flags &= ~KEEP;
                    Clusters[i] = cluster;
                    return;
                }

                RaycastHit hit = Hits[i];
                float3 normal = hit.normal;
                bool touched = math.lengthsq(normal) > 0.5f;

                if (cluster.Resting != 0)
                {
                    if (!touched)
                    {
                        cluster.Resting = 0;
                        cluster.Still = 0f;
                    }
                    else if ((cluster.Flags & KEEP) != 0)
                        cluster.Still += DeltaTime;

                    Clusters[i] = cluster;
                    return;
                }

                if (touched && (cluster.Flags & SPLAT) != 0)
                {
                    Lay(ref cluster, hit.point, normal, i);
                    Clusters[i] = cluster;
                    return;
                }

                // ... the rest of Execute as it was: bounce, shatter mark, rest, gravity, spin ...
            }

            /// <summary>
            /// A drop spreads where it lands: a thin tile flat on the surface (a wall too), turned at
            /// random, lying at once — the compact pass moves it to the lying store in this step.
            /// </summary>
            private void Lay(ref Cluster cluster, float3 point, float3 normal, int i)
            {
                var random = new Unity.Mathematics.Random((Seed ^ ((uint)i * 2654435761u)) | 1u);
                quaternion tilt = FromTo(new float3(0f, 1f, 0f), normal);
                cluster.Rotation = math.mul(tilt, quaternion.AxisAngle(new float3(0f, 1f, 0f), random.NextFloat(0f, 2f * math.PI)));
                cluster.Position = point + normal * (cluster.Radius * SPLAT_HEIGHT + 0.002f);
                cluster.Velocity = float3.zero;
                cluster.Spin = float3.zero;
                cluster.Resting = 1;
                cluster.Still = SETTLE_TIME;
                cluster.Flags |= LAID;
            }

            private static quaternion FromTo(float3 from, float3 to)
            {
                float d = math.dot(from, to);
                if (d < -0.9999f)
                    return quaternion.AxisAngle(new float3(1f, 0f, 0f), math.PI);
                float3 axis = math.cross(from, to);
                return math.normalize(new quaternion(axis.x, axis.y, axis.z, 1f + d));
            }
```

11. `ShatterJob`: осколки наследуют `KEEP` — в создании детей:

```csharp
                        Clusters.Add(new Cluster
                        {
                            Rotation = parent.Rotation,
                            Life = parent.Life + random.NextFloat(-0.8f, 0.8f),
                            Flags = parent.Flags & KEEP
                        });
```

12. `CompactJob` целиком:

```csharp
        /// <summary>
        /// Drops dead clumps and their cubes, moves the cubes of kept clumps that came to rest into the
        /// lying store (a ring: past its end the oldest are overwritten), and writes a matrix and a
        /// colour per flying cube.
        /// </summary>
        [BurstCompile]
        private struct CompactJob : IJob
        {
            public NativeList<Cluster> Clusters;
            public NativeList<Cube> Cubes;
            [WriteOnly] public NativeArray<float4x4> Matrices;
            [WriteOnly] public NativeArray<float4> Colors;
            public NativeReference<int> Alive;
            [WriteOnly] public NativeArray<float4x4> Lying;
            [WriteOnly] public NativeArray<float4> LyingColors;
            public NativeReference<int2> LyingState;

            public void Execute()
            {
                int count = Clusters.Length;
                var remap = new NativeArray<int>(count, Allocator.Temp);
                var settled = new NativeArray<Cluster>(count, Allocator.Temp);
                int clusters = 0;
                for (int i = 0; i < count; i++)
                {
                    Cluster cluster = Clusters[i];
                    if ((cluster.Flags & KEEP) != 0 && cluster.Resting != 0 && cluster.Still >= SETTLE_TIME && cluster.Count > 0)
                    {
                        settled[i] = cluster;
                        remap[i] = SETTLED;
                        continue;
                    }

                    bool alive = cluster.Life > 0f && cluster.Count > 0;
                    remap[i] = alive ? clusters : -1;
                    if (alive)
                        Clusters[clusters++] = cluster;
                }

                int2 lying = LyingState.Value;
                int capacity = Lying.Length;
                int cubes = 0;
                for (int i = 0; i < Cubes.Length; i++)
                {
                    Cube cube = Cubes[i];
                    int owner = remap[cube.Cluster];
                    if (owner == SETTLED)
                    {
                        Lying[lying.x] = Matrix(settled[cube.Cluster], cube, 1f);
                        LyingColors[lying.x] = cube.Color;
                        lying.x = (lying.x + 1) % capacity;
                        lying.y = math.min(lying.y + 1, capacity);
                        continue;
                    }

                    if (owner < 0)
                        continue;

                    cube.Cluster = owner;
                    Cubes[cubes] = cube;

                    Cluster cluster = Clusters[owner];
                    Matrices[cubes] = Matrix(cluster, cube, math.saturate(cluster.Life / 0.6f));
                    Colors[cubes] = cube.Color;
                    cubes++;
                }

                LyingState.Value = lying;
                Clusters.ResizeUninitialized(clusters);
                Cubes.ResizeUninitialized(cubes);
                Alive.Value = cubes;
            }

            private static float4x4 Matrix(in Cluster cluster, in Cube cube, float shrink)
            {
                float3 scale = (cluster.Flags & LAID) != 0
                    ? new float3(SPLAT_WIDTH, SPLAT_HEIGHT, SPLAT_WIDTH)
                    : new float3(1f);
                float3 position = cluster.Position + math.mul(cluster.Rotation, cube.Offset);
                return float4x4.TRS(position, cluster.Rotation, scale * (cube.Size * shrink));
            }
        }
```

Комментарий класса `VoxelDebris` дополнить абзацем: «Kept clumps and drops never age: once still,
their cubes move to a lying store that is only drawn (no raycasts), up to LYING_CAPACITY.»

- [ ] **Step 4: Run test to verify it passes**

Компиляция, затем PlayMode-тесты `Game.Voxels.Tests.VoxelDebrisKeepTests` и, для регрессии,
`Game.Voxels.Tests.VoxelPlayModeTests`. Ожидание: все PASS.

- [ ] **Step 5: Без коммита** (код остаётся в рабочем дереве, см. Global Constraints).

---

### Task 2: `VoxelBody` — запуск целого тела, засыпание, пробуждение, вечные обломки

**Files:**
- Modify: `Assets/Game/Voxels/Runtime/VoxelTypes.cs` (struct `VoxelBodySettings`)
- Modify: `Assets/Game/Voxels/Runtime/VoxelBody.cs`
- Modify: `Assets/Game/Voxels/Runtime/VoxelImpactFracture.cs`
- Create: `Assets/Game/Voxels/Runtime/VoxelSettle.cs`
- Test: `Assets/Game/Voxels/Tests/VoxelBodySettleTests.cs`

**Interfaces:**
- Consumes: `VoxelDebris.SetKeep(int)`, `VoxelDebris.Spawn(...) → int`, `VoxelDebris.LyingCount` (Task 1).
- Produces:
  - `VoxelBodySettings.Settle` (bool), `.KeepDebris` (bool), `.FractureSpeed` (float, 0 — 3.5 по умолчанию)
  - `public const float VoxelBody.WAKE_FORCE = 5f;`
  - `public bool VoxelBody.IsLoose { get; }` — есть свой `Rigidbody`
  - `public void VoxelBody.Launch(Vector3 velocity, Vector3 spin)`
  - `public void VoxelBody.SplitLoose()`
  - `internal void VoxelBody.Freeze()`
  - `public float VoxelImpactFracture.MinSpeed { get; set; }`
  - `public sealed class VoxelSettle : MonoBehaviour`

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Game/Voxels/Tests/VoxelBodySettleTests.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Voxels.Tests
{
    public sealed class VoxelBodySettleTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();

            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        private void Floor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4f;
            _spawned.Add(floor);
            Physics.SyncTransforms();
        }

        private static VoxelBodySettings Loose(bool keepDebris = false, float fractureSpeed = 0f)
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(0.1f);
            settings.Anchor = VoxelAnchor.Largest;
            settings.Layer = VoxelWorld.PieceLayer;
            settings.Settle = true;
            settings.KeepDebris = keepDebris;
            settings.FractureSpeed = fractureSpeed;
            return settings;
        }

        private static VoxelBody Body(int3 size, Vector3 position, Func<int3, bool> solid, VoxelBodySettings settings)
        {
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                if (solid(new int3(x, y, z)))
                    voxels[x + size.x * (y + size.y * z)] = 1;

            var palette = new NativeArray<Color32>(256, Allocator.Persistent);
            palette[1] = new Color32(200, 60, 60, 255);

            var go = new GameObject("Flesh") { layer = VoxelWorld.PieceLayer };
            go.transform.position = position;
            VoxelBody body = go.AddComponent<VoxelBody>();
            body.Init(size, voxels, palette, settings);
            return body;
        }

        private static IEnumerator Settle(VoxelBody body)
        {
            float until = Time.time + 6f;
            while (body.IsLoose && Time.time < until)
                yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Launch_LetsTheBodyGoWithItsVelocityAndSpin()
        {
            VoxelBody body = Body(new int3(4, 4, 4), new Vector3(0f, 5f, 0f), _ => true, Loose());
            body.Launch(new Vector3(3f, 0f, 0f), new Vector3(0f, 2f, 0f));
            // Kinematic until its colliders are built at the end of the frame.
            yield return null;
            yield return new WaitForFixedUpdate();

            Rigidbody rigidbody = body.GetComponent<Rigidbody>();
            Assert.IsNotNull(rigidbody);
            Assert.IsFalse(rigidbody.isKinematic);
            Assert.That(rigidbody.linearVelocity.x, Is.GreaterThan(2f));
            Assert.That(rigidbody.angularVelocity.y, Is.GreaterThan(1f));
            Assert.IsNotNull(body.GetComponent<VoxelSettle>());
        }

        [UnityTest]
        public IEnumerator Settled_LosesItsRigidbodyButKeepsItsColliders()
        {
            Floor();
            VoxelBody body = Body(new int3(4, 4, 4), new Vector3(0f, 0.3f, 0f), _ => true, Loose());
            body.Launch(Vector3.zero, Vector3.zero);
            yield return Settle(body);

            Assert.IsFalse(body.IsLoose, "it settled within 6 s");
            Assert.IsNull(body.GetComponent<Rigidbody>());
            Assert.That(body.GetComponentsInChildren<BoxCollider>().Any(b => b.enabled));
        }

        [UnityTest]
        public IEnumerator Settled_ABlastThrowsItAgain_ABulletOnlyChips()
        {
            Floor();
            VoxelBody body = Body(new int3(4, 4, 4), new Vector3(0f, 0.3f, 0f), _ => true, Loose());
            body.Launch(Vector3.zero, Vector3.zero);
            yield return Settle(body);
            Assert.IsFalse(body.IsLoose);

            Vector3 top = body.WorldBounds.center + Vector3.up * 0.15f;
            VoxelDamage.Impact(top, Vector3.down, 0.13f);
            yield return null;
            Assert.IsFalse(body.IsLoose, "a bullet's chip does not wake it");

            VoxelDamage.Blast(top, 0.15f, 9f);
            yield return null;
            Assert.IsTrue(body.IsLoose, "a blast does");
        }

        [UnityTest]
        public IEnumerator SplitLoose_AnIslandFliesOnWithTheBody()
        {
            // A 6×6×6 block and, past a gap, a 3×3×3 one: 27 voxels, more than a clod.
            VoxelBody body = Body(new int3(11, 6, 6), new Vector3(0f, 5f, 0f),
                c => c.x < 6 || (c.x >= 8 && c.y < 3 && c.z < 3), Loose());
            body.Launch(new Vector3(0f, 0f, 4f), Vector3.zero);
            body.SplitLoose();
            yield return null;
            yield return new WaitForFixedUpdate();

            VoxelBody island = Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).Single(b => b != body);
            Assert.AreEqual(27, island.SolidCount);
            Assert.That(island.GetComponent<Rigidbody>().linearVelocity.z, Is.GreaterThan(3f));
        }

        [UnityTest]
        public IEnumerator KeepDebris_WhatACarveThrowsLiesForGood()
        {
            Floor();
            VoxelBody body = Body(new int3(8, 4, 8), new Vector3(-0.4f, 0f, -0.4f), _ => true, Loose(keepDebris: true));
            yield return null;

            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int before = debris.LyingCount;
            VoxelDamage.Blast(body.WorldBounds.center + Vector3.up * 0.2f, 0.25f, 3f);

            float until = Time.time + 8f;
            while (debris.LyingCount == before && Time.time < until)
                yield return null;
            Assert.That(debris.LyingCount, Is.GreaterThan(before));
        }

        [Test]
        public void FractureSpeed_SetsTheBreakThreshold()
        {
            VoxelBody body = Body(new int3(2, 2, 2), Vector3.up * 5f, _ => true, Loose(fractureSpeed: 7f));
            body.Launch(Vector3.zero, Vector3.zero);
            Assert.AreEqual(7f, body.GetComponent<VoxelImpactFracture>().MinSpeed);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Компиляция. Ожидание: нет `Settle`, `KeepDebris`, `FractureSpeed`, `Launch`, `SplitLoose`,
`IsLoose`, `VoxelSettle`, `MinSpeed`.

- [ ] **Step 3: Write minimal implementation**

1. `VoxelTypes.cs`, в `VoxelBodySettings` после `Material`:

```csharp
        /// <summary>
        /// Once loose, the body drops its Rigidbody after lying still a second (VoxelSettle) and
        /// then costs only its drawing. A carve harder than VoxelBody.WAKE_FORCE gives it back.
        /// </summary>
        public bool Settle;

        /// <summary>Debris this body throws (carves, clods, fracture crumbs) lies for good (VoxelDebris.SetKeep).</summary>
        public bool KeepDebris;

        /// <summary>Slowest impact (m/s) that breaks the body once loose; 0 — VoxelImpactFracture's own.</summary>
        public float FractureSpeed;
```

2. `VoxelImpactFracture.cs`, после полей:

```csharp
        /// <summary>Slowest impact (m/s) that breaks this piece; a body's FractureSpeed sets it.</summary>
        public float MinSpeed
        {
            get => _minSpeed;
            set => _minSpeed = value;
        }
```

3. `VoxelBody.cs`:

```csharp
        /// <summary>
        /// Carve speed (m/s) above which a settled body is thrown again: a shell's blast (9) is, a
        /// bullet's chip (2.5) is not.
        /// </summary>
        public const float WAKE_FORCE = 5f;

        // ... next to _pendingBlastForce:
        private Vector3 _pendingSpin;

        /// <summary>Has a Rigidbody of its own: thrown, falling, or not settled yet.</summary>
        public bool IsLoose => _rigidbody != null;

        /// <summary>
        /// Lets a body made whole (a creature's piece) go as a loose one: kinematic until its
        /// colliders exist at the end of this frame, then off with this velocity and spin.
        /// </summary>
        public void Launch(Vector3 velocity, Vector3 spin)
        {
            if (_rigidbody != null || !_voxels.IsCreated)
                return;

            MakeDynamic(MassOf(SolidCount), velocity, transform.position, 0f);
            _pendingSpin = spin;
        }

        /// <summary>
        /// Takes out every piece not joined to the biggest one: big ones as bodies, scraps as clods.
        /// Called right after Launch they fly on with this body (see InheritedVelocity).
        /// </summary>
        public void SplitLoose()
        {
            if (_voxels.IsCreated && _settings.Split && SolidCount > 0)
                Split(transform.position, 0f);
        }

        /// <summary>Drops the Rigidbody of a loose body that came to rest. It stays where it lies, colliders and all.</summary>
        internal void Freeze()
        {
            if (_rigidbody == null)
                return;

            if (TryGetComponent(out VoxelImpactFracture fracture))
                Destroy(fracture);
            Destroy(_rigidbody);
            _rigidbody = null;
            _activatePending = false;
        }

        private float MassOf(int voxels)
        {
            float edge = VoxelSize * Mathf.Abs(transform.lossyScale.x);
            return Mathf.Max(1f, voxels * edge * edge * edge * _settings.Density);
        }
```

`CarveSphere` — тело вместо однострочника:

```csharp
        public int CarveSphere(Vector3 worldCenter, float worldRadius, float force, int debrisBudget)
        {
            int taken = Carve(worldCenter, worldRadius * Toughness, _settings.CraterDepth, 0.25f, true, debrisBudget,
                worldCenter, force, Vector3.zero);

            // A settled piece a blast reaches is thrown again; a bullet only chips it.
            if (taken > 0 && _settings.Settle && _rigidbody == null && force > WAKE_FORCE &&
                _voxels.IsCreated && SolidCount > 0)
                MakeDynamic(MassOf(SolidCount), Vector3.zero, worldCenter, force);

            return taken;
        }
```

`Detach`: две строки массы заменить на `float mass = MassOf(piece.Count);`.
`UpdateMass`: тело — `if (_rigidbody != null) _rigidbody.mass = MassOf(SolidCount);`.

`MakeDynamic`:

```csharp
        private void MakeDynamic(float mass, Vector3 velocity, Vector3 blastCenter, float force)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            _rigidbody.mass = mass;
            _rigidbody.isKinematic = true;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.maxDepenetrationVelocity = 3f;

            if (!TryGetComponent(out VoxelImpactFracture fracture))
                fracture = gameObject.AddComponent<VoxelImpactFracture>();
            if (_settings.FractureSpeed > 0f)
                fracture.MinSpeed = _settings.FractureSpeed;
            if (_settings.Settle && !TryGetComponent(out VoxelSettle _))
                gameObject.AddComponent<VoxelSettle>();

            _activatePending = true;
            _pendingVelocity = velocity;
            _pendingBlastCenter = blastCenter;
            _pendingBlastForce = force;
        }
```

`InheritedVelocity` — первой строкой:

```csharp
            // Launched this frame and still kinematic: its pieces take the speed it is about to get.
            if (_rigidbody != null && _activatePending)
                return _pendingVelocity;
```

`FinishRebuild` — после `_rigidbody.linearVelocity = _pendingVelocity;`:

```csharp
            _rigidbody.angularVelocity = _pendingSpin;
            _pendingSpin = Vector3.zero;
```

Вечные обломки — помощник и три вызова:

```csharp
        private void Keep(VoxelDebris debris, int cluster)
        {
            if (_settings.KeepDebris && debris != null && cluster >= 0)
                debris.SetKeep(cluster);
        }
```

- в `ThrowDebris`: `debris.Spawn(...)` → `Keep(debris, debris.Spawn(...));`; после
  `BeginCluster` и `SetLife` для кома — `Keep(debris, clump);`;
- в `Crumble`: после `BeginCluster` — `Keep(debris, clump);`.
  (`Wash` / `ShakeOff` — грязь танка — не трогать.)

4. `VoxelSettle.cs`:

```csharp
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Takes the Rigidbody off a loose body (VoxelBodySettings.Settle) once it has lain still for
    /// a second: a creature's flesh then lies all game for the price of its meshes, not of physics.
    /// A blast gives the body its Rigidbody back (VoxelBody.CarveSphere), and this starts over.
    /// </summary>
    [RequireComponent(typeof(VoxelBody))]
    public sealed class VoxelSettle : MonoBehaviour
    {
        private const float STILL_TIME = 1f;
        private const float STILL_SPEED = 0.05f;
        private const float STILL_SPIN = 0.1f;

        private VoxelBody _body;
        private Rigidbody _rigidbody;
        private float _still;

        private void Awake() => _body = GetComponent<VoxelBody>();

        private void FixedUpdate()
        {
            if (_body == null || !_body.IsLoose)
            {
                _still = 0f;
                _rigidbody = null;
                return;
            }

            if (_rigidbody == null && !TryGetComponent(out _rigidbody))
                return;
            if (_rigidbody.isKinematic)
            {
                _still = 0f;
                return;
            }

            bool still = _rigidbody.IsSleeping() ||
                         (_rigidbody.linearVelocity.sqrMagnitude < STILL_SPEED * STILL_SPEED &&
                          _rigidbody.angularVelocity.sqrMagnitude < STILL_SPIN * STILL_SPIN);
            _still = still ? _still + Time.fixedDeltaTime : 0f;
            if (_still < STILL_TIME)
                return;

            _still = 0f;
            _rigidbody = null;
            _body.Freeze();
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

PlayMode: `Game.Voxels.Tests.VoxelBodySettleTests`, `Game.Voxels.Tests.VoxelPlayModeTests`,
`Game.Voxels.Tests.VoxelDebrisKeepTests`. Ожидание: все PASS.

- [ ] **Step 5: Без коммита.**

---

### Task 3: `SkinnedVoxelizer` и `FleshPalette`

**Files:**
- Modify: `Assets/Game/Voxels/Runtime/MeshVoxelizer.cs`
- Create: `Assets/Game/Voxels/Runtime/FleshPalette.cs`
- Create: `Assets/Game/Voxels/Runtime/SkinnedVoxelizer.cs`
- Create: `Assets/Game/Voxels/Tests/SkinnedColumn.cs`
- Test: `Assets/Game/Voxels/Tests/SkinnedVoxelizerTests.cs`

**Interfaces:**
- Produces:
  - `internal static bool MeshVoxelizer.CrossZ(float2 p, float3 a, float3 b, float3 c, out float z)`
  - `FleshPalette` (struct): `FIRST_SKIN = 1`, `FIRST_MEAT = 5`, `MEAT_SLOTS = 4`, `FIRST_BLOOD = 9`,
    `BLOOD_SLOTS = 2`; поля `Skin`, `SkinNoise`, `Meat`, `MeatWetness`, `Blood`, `BloodWetness`,
    `BloodShare`; `NativeArray<Color32> BuildPalette(Allocator)`; `Color32 BloodVoxel(int index)`.
  - `SkinnedVoxelizer.NO_PART = 255`; `struct Part { Transform[] Bones; int Side; }`;
    `struct Result : IDisposable { int3 Size; float3 Origin; float VoxelSize; Vector3 Position;
    Quaternion Rotation; NativeArray<byte> Voxels; NativeArray<byte> Parts; NativeArray<Color32> Palette; }`;
    `static bool Voxelize(SkinnedMeshRenderer skin, IReadOnlyList<Part> parts, Plane midPlane,
    float voxelSize, in FleshPalette palette, uint seed, out Result result)`.
  - `public static class SkinnedColumn` (тестовый): `SkinnedMeshRenderer Create(Vector3 at, out Transform root, out Transform lower, out Transform upper)`.

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Game/Voxels/Tests/SkinnedColumn.cs
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>
    /// A 0.4 × 1 × 0.4 m column standing on <c>at</c>, skinned to two bones: Lower (0.25 m up)
    /// holds the bottom half, Upper (0.75 m up, its child) the top half.
    /// </summary>
    public static class SkinnedColumn
    {
        public static SkinnedMeshRenderer Create(Vector3 at, out Transform root, out Transform lower, out Transform upper)
        {
            root = new GameObject("Column").transform;
            root.position = at;
            lower = new GameObject("Lower").transform;
            lower.SetParent(root, false);
            lower.localPosition = new Vector3(0f, 0.25f, 0f);
            upper = new GameObject("Upper").transform;
            upper.SetParent(lower, false);
            upper.localPosition = new Vector3(0f, 0.5f, 0f);

            var skinObject = new GameObject("Skin");
            skinObject.transform.SetParent(root, false);

            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);

            Vector3[] vertices = cube.vertices;
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i], new Vector3(0.4f, 1f, 0.4f)) + new Vector3(0f, 0.5f, 0f);
                weights[i] = new BoneWeight { boneIndex0 = vertices[i].y < 0.5f ? 0 : 1, weight0 = 1f };
            }

            var mesh = new Mesh
            {
                name = "Column",
                vertices = vertices,
                triangles = cube.triangles,
                normals = cube.normals,
                boneWeights = weights
            };
            Matrix4x4 skinToWorld = skinObject.transform.localToWorldMatrix;
            mesh.bindposes = new[] { lower.worldToLocalMatrix * skinToWorld, upper.worldToLocalMatrix * skinToWorld };
            mesh.RecalculateBounds();

            var skin = skinObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { lower, upper };
            skin.rootBone = lower;
            skin.updateWhenOffscreen = true;
            return skin;
        }
    }
}
```

```csharp
// Assets/Game/Voxels/Tests/SkinnedVoxelizerTests.cs
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class SkinnedVoxelizerTests
    {
        private Transform _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }

        public static FleshPalette Palette() => new()
        {
            Skin = Color.green,
            SkinNoise = 0.05f,
            Meat = new[] { Color.red },
            MeatWetness = 0.6f,
            Blood = new[] { Color.yellow },
            BloodWetness = 0.9f,
            BloodShare = 0.15f
        };

        /// <summary>The cell of a grid under a world point; <paramref name="outside"/> off the grid.</summary>
        public static byte At(in SkinnedVoxelizer.Result r, NativeArray<byte> grid, Vector3 world, byte outside)
        {
            float3 local = (float3)(Quaternion.Inverse(r.Rotation) * (world - r.Position));
            int3 cell = (int3)math.floor((local - r.Origin) / r.VoxelSize);
            if (math.any(cell < 0) || math.any(cell >= r.Size))
                return outside;
            return grid[cell.x + r.Size.x * (cell.y + r.Size.y * cell.z)];
        }

        private SkinnedVoxelizer.Result Voxelize(SkinnedVoxelizer.Part[] parts, Plane plane, SkinnedMeshRenderer skin)
        {
            Assert.IsTrue(SkinnedVoxelizer.Voxelize(skin, parts, plane, 0.05f, Palette(), 1, out SkinnedVoxelizer.Result r));
            return r;
        }

        private static SkinnedVoxelizer.Part[] ByBone(Transform lower, Transform upper) => new[]
        {
            new SkinnedVoxelizer.Part { Bones = new[] { lower } },
            new SkinnedVoxelizer.Part { Bones = new[] { upper } }
        };

        [Test]
        public void Column_FillsItsVolume_SkinOutsideMeatInside()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            using SkinnedVoxelizer.Result r = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin);

            Assert.That(r.Voxels.Count(v => v != 0), Is.InRange(1150, 1420), "0.4 × 1 × 0.4 m in 5 cm voxels is 1280");
            Assert.That(At(r, r.Voxels, new Vector3(0.18f, 0.5f, 0f), 0),
                Is.InRange(FleshPalette.FIRST_SKIN, FleshPalette.FIRST_MEAT - 1), "the outer layer is skin");
            Assert.That(At(r, r.Voxels, new Vector3(0f, 0.5f, 0f), 0),
                Is.GreaterThanOrEqualTo(FleshPalette.FIRST_MEAT), "inside is meat or blood");
        }

        [Test]
        public void EachVoxel_GoesToThePartOfItsNearestBone()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            using SkinnedVoxelizer.Result r = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin);

            Assert.AreEqual(0, At(r, r.Parts, new Vector3(0f, 0.2f, 0f), SkinnedVoxelizer.NO_PART));
            Assert.AreEqual(1, At(r, r.Parts, new Vector3(0f, 0.8f, 0f), SkinnedVoxelizer.NO_PART));
            int bottom = r.Parts.Count(p => p == 0);
            int top = r.Parts.Count(p => p == 1);
            Assert.That(Mathf.Abs(bottom - top), Is.LessThan(100), "split at half height");
        }

        [Test]
        public void SidedParts_SplitAtTheMidPlane()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            var halves = new[]
            {
                new SkinnedVoxelizer.Part { Bones = new[] { lower, upper }, Side = 1 },
                new SkinnedVoxelizer.Part { Bones = new[] { lower, upper }, Side = -1 }
            };
            using SkinnedVoxelizer.Result r = Voxelize(halves, new Plane(Vector3.right, Vector3.zero), skin);

            Assert.AreEqual(0, At(r, r.Parts, new Vector3(0.1f, 0.5f, 0f), SkinnedVoxelizer.NO_PART), "+x is the + side");
            Assert.AreEqual(1, At(r, r.Parts, new Vector3(-0.1f, 0.5f, 0f), SkinnedVoxelizer.NO_PART));
        }

        [Test]
        public void TheGridFollowsThePose()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            upper.position += Vector3.right; // the top half moves a metre to the right
            using SkinnedVoxelizer.Result r = Voxelize(ByBone(lower, upper), new Plane(Vector3.right, 0f), skin);

            Assert.AreNotEqual(0, At(r, r.Voxels, new Vector3(1f, 0.75f, 0f), 0));
            Assert.AreEqual(1, At(r, r.Parts, new Vector3(1f, 0.75f, 0f), SkinnedVoxelizer.NO_PART));
            Assert.AreEqual(0, At(r, r.Voxels, new Vector3(0f, 0.75f, 0f), 0), "nothing left where it stood");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Компиляция. Ожидание: нет `SkinnedVoxelizer`, `FleshPalette`.

- [ ] **Step 3: Write minimal implementation**

1. `MeshVoxelizer.cs`: вынести `Cross` из `FillMeshJob` в класс `MeshVoxelizer` как
   `internal static bool CrossZ(float2 p, float3 a, float3 b, float3 c, out float z)` (тело то же),
   в `FillMeshJob.Execute` звать `CrossZ(...)`. Тест танка (`VoxelizedModel_...`) должен остаться
   зелёным.

2. `FleshPalette.cs`:

```csharp
using System;
using Unity.Collections;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Colours of a body that bleeds: skin outside, meat inside, veins of blood in the meat. Meat
    /// and blood are wet — glossy in the Game/Voxel shader (wetness rides in the alpha).
    /// </summary>
    [Serializable]
    public struct FleshPalette
    {
        public const int FIRST_SKIN = 1;
        public const int FIRST_MEAT = FIRST_SKIN + MeshVoxelizer.VARIANTS;
        public const int MEAT_SLOTS = 4;
        public const int FIRST_BLOOD = FIRST_MEAT + MEAT_SLOTS;
        public const int BLOOD_SLOTS = 2;

        public Color Skin;
        [Range(0f, 0.3f)] public float SkinNoise;
        public Color[] Meat;
        [Range(0f, 1f)] public float MeatWetness;
        public Color[] Blood;
        [Range(0f, 1f)] public float BloodWetness;

        /// <summary>Share of the meat voxels that are blood.</summary>
        [Range(0f, 1f)] public float BloodShare;

        public NativeArray<Color32> BuildPalette(Allocator allocator)
        {
            var palette = new NativeArray<Color32>(256, allocator);
            MeshVoxelizer.FillShades(palette, FIRST_SKIN, Skin, SkinNoise);
            for (int i = 0; i < MEAT_SLOTS; i++)
                palette[FIRST_MEAT + i] = MeshVoxelizer.WithWetness(
                    MeshVoxelizer.ToVertexColor(Pick(Meat, i, new Color(0.5f, 0.1f, 0.1f))), MeatWetness);
            for (int i = 0; i < BLOOD_SLOTS; i++)
                palette[FIRST_BLOOD + i] = BloodVoxel(i);
            return palette;
        }

        /// <summary>A blood colour as a voxel or a drop carries it, wetness included.</summary>
        public Color32 BloodVoxel(int index) =>
            MeshVoxelizer.WithWetness(MeshVoxelizer.ToVertexColor(Pick(Blood, index, Color.red)), BloodWetness);

        private static Color Pick(Color[] colors, int index, Color fallback) =>
            colors == null || colors.Length == 0 ? fallback : colors[index % colors.Length];
    }
}
```

3. `SkinnedVoxelizer.cs`:

```csharp
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Turns a skinned body, in the pose it stands in right now, into voxels: skin outside, meat and
    /// veins of blood inside (FleshPalette), each voxel tagged with the part of the body whose bones
    /// are nearest. The grid lies in the renderer's axes (its position and rotation, no scale).
    /// </summary>
    /// <remarks>
    /// Filling is MeshVoxelizer's (a ray per column along Z, inside between pairs of crossings), but
    /// the triangles are first sorted into the columns their bounds cover: a creature has thousands
    /// of triangles, and testing each against every column would take ~100 ms.
    /// </remarks>
    public static class SkinnedVoxelizer
    {
        public const byte NO_PART = 255;
        private const int MAX_SIZE = 512;

        public struct Part
        {
            /// <summary>A bone reaches to each child that is in this list too; a bone with none is a point.</summary>
            public Transform[] Bones;

            /// <summary>0 — the whole part; +1 / −1 — only voxels on that side of the mid plane (a torso cut in halves).</summary>
            public int Side;
        }

        public struct Result : IDisposable
        {
            public int3 Size;
            public float3 Origin;
            public float VoxelSize;
            public Vector3 Position;
            public Quaternion Rotation;

            /// <summary>Palette index per voxel, 0 — empty.</summary>
            public NativeArray<byte> Voxels;

            /// <summary>Part per voxel, NO_PART — empty.</summary>
            public NativeArray<byte> Parts;

            public NativeArray<Color32> Palette;

            public void Dispose()
            {
                if (Voxels.IsCreated) Voxels.Dispose();
                if (Parts.IsCreated) Parts.Dispose();
                if (Palette.IsCreated) Palette.Dispose();
            }
        }

        private struct Segment
        {
            public float3 A;
            public float3 B;
            public int Part;
            public int Side;
        }

        private static Mesh s_baked;

        public static bool Voxelize(SkinnedMeshRenderer skin, IReadOnlyList<Part> parts, Plane midPlane, float voxelSize,
            in FleshPalette palette, uint seed, out Result result)
        {
            result = default;
            if (skin == null || skin.sharedMesh == null || parts == null || parts.Count == 0 ||
                parts.Count >= NO_PART || voxelSize <= 0f)
                return false;

            if (s_baked == null)
                s_baked = new Mesh { name = "SkinnedVoxelizer" };
            // With useScale the vertices carry the renderer's scale: the grid only takes its position and rotation.
            skin.BakeMesh(s_baked, true);
            Vector3[] vertices = s_baked.vertices;
            int[] indices = s_baked.triangles;
            if (vertices.Length == 0 || indices.Length < 3)
                return false;

            Transform space = skin.transform;
            Vector3 position = space.position;
            Quaternion rotation = space.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            foreach (Vector3 v in vertices)
            {
                min = math.min(min, v);
                max = math.max(max, v);
            }

            float3 origin = math.floor(min / voxelSize) * voxelSize - voxelSize;
            int3 size = math.clamp((int3)math.ceil((max - origin) / voxelSize) + 1, 1, MAX_SIZE);
            int columns = size.x * size.y;
            int cells = columns * size.z;

            float3 ToGrid(Vector3 world) => ((float3)(inverse * (world - position)) - origin) / voxelSize;

            var triangles = new NativeArray<float3>(indices.Length, Allocator.TempJob);
            for (int i = 0; i < indices.Length; i++)
                triangles[i] = ((float3)vertices[indices[i]] - origin) / voxelSize;

            NativeArray<Segment> segments = BuildSegments(parts, ToGrid);
            float3 normal = (float3)(inverse * midPlane.normal);
            float3 onPlane = ToGrid(midPlane.ClosestPointOnPlane(position));

            var start = new NativeArray<int>(columns + 1, Allocator.TempJob);
            var items = new NativeList<int>(indices.Length, Allocator.TempJob);
            var solid = new NativeArray<byte>(cells, Allocator.TempJob);
            var voxels = new NativeArray<byte>(cells, Allocator.Persistent);
            var partOf = new NativeArray<byte>(cells, Allocator.Persistent);

            JobHandle handle = new BinTrianglesJob
            {
                Triangles = triangles, Columns = size.xy, Start = start, Items = items
            }.Schedule();
            handle = new FillColumnsJob
            {
                Triangles = triangles, Start = start, Items = items.AsDeferredJobArray(), Size = size, Solid = solid
            }.Schedule(columns, 16, handle);
            handle = new ClassifyJob
            {
                Solid = solid,
                Segments = segments,
                Size = size,
                PlaneNormal = normal,
                PlaneDistance = -math.dot(normal, onPlane),
                Seed = seed,
                BloodShare = palette.BloodShare,
                Voxels = voxels,
                Parts = partOf
            }.Schedule(cells, 256, handle);
            handle.Complete();

            triangles.Dispose();
            segments.Dispose();
            start.Dispose();
            items.Dispose();
            solid.Dispose();

            result = new Result
            {
                Size = size,
                Origin = origin,
                VoxelSize = voxelSize,
                Position = position,
                Rotation = rotation,
                Voxels = voxels,
                Parts = partOf,
                Palette = palette.BuildPalette(Allocator.Persistent)
            };
            return true;
        }

        private static NativeArray<Segment> BuildSegments(IReadOnlyList<Part> parts, Func<Vector3, float3> toGrid)
        {
            var list = new List<Segment>();
            for (int p = 0; p < parts.Count; p++)
            {
                Transform[] bones = parts[p].Bones;
                if (bones == null)
                    continue;

                var own = new HashSet<Transform>(bones);
                foreach (Transform bone in bones)
                {
                    if (bone == null)
                        continue;

                    float3 a = toGrid(bone.position);
                    bool reached = false;
                    foreach (Transform child in bone)
                    {
                        if (!own.Contains(child))
                            continue;
                        list.Add(new Segment { A = a, B = toGrid(child.position), Part = p, Side = parts[p].Side });
                        reached = true;
                    }

                    if (!reached)
                        list.Add(new Segment { A = a, B = a, Part = p, Side = parts[p].Side });
                }
            }

            var array = new NativeArray<Segment>(list.Count, Allocator.TempJob);
            for (int i = 0; i < list.Count; i++)
                array[i] = list[i];
            return array;
        }

        /// <summary>Sorts each triangle into every column its XY bounds cover (counting pass, then filling pass).</summary>
        [BurstCompile]
        private struct BinTrianglesJob : IJob
        {
            [ReadOnly] public NativeArray<float3> Triangles;
            public int2 Columns;
            public NativeArray<int> Start;
            public NativeList<int> Items;

            public void Execute()
            {
                int columns = Columns.x * Columns.y;
                var counts = new NativeArray<int>(columns, Allocator.Temp);
                int triangles = Triangles.Length / 3;

                for (int t = 0; t < triangles; t++)
                {
                    Bounds(t, out int2 lo, out int2 hi);
                    for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                        counts[x + y * Columns.x]++;
                }

                int sum = 0;
                for (int c = 0; c < columns; c++)
                {
                    Start[c] = sum;
                    sum += counts[c];
                    counts[c] = 0;
                }

                Start[columns] = sum;
                Items.ResizeUninitialized(sum);

                for (int t = 0; t < triangles; t++)
                {
                    Bounds(t, out int2 lo, out int2 hi);
                    for (int y = lo.y; y <= hi.y; y++)
                    for (int x = lo.x; x <= hi.x; x++)
                    {
                        int c = x + y * Columns.x;
                        Items[Start[c] + counts[c]++] = t;
                    }
                }
            }

            private void Bounds(int t, out int2 lo, out int2 hi)
            {
                float2 a = Triangles[t * 3].xy;
                float2 b = Triangles[t * 3 + 1].xy;
                float2 c = Triangles[t * 3 + 2].xy;
                lo = math.max(0, (int2)math.floor(math.min(math.min(a, b), c)));
                hi = math.min(Columns - 1, (int2)math.floor(math.max(math.max(a, b), c)));
            }
        }

        [BurstCompile]
        private struct FillColumnsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Triangles;
            [ReadOnly] public NativeArray<int> Start;
            [ReadOnly] public NativeArray<int> Items;
            public int3 Size;

            // Each column writes only its own cells.
            [NativeDisableParallelForRestriction] public NativeArray<byte> Solid;

            public void Execute(int column)
            {
                int first = Start[column];
                int last = Start[column + 1];
                if (last - first < 2)
                    return;

                int x = column % Size.x;
                int y = column / Size.x;
                // Off-centre, as in MeshVoxelizer: never exactly along a shared edge.
                float2 p = new float2(x + 0.50031f, y + 0.49973f);
                var crossings = new NativeList<float>(16, Allocator.Temp);

                for (int k = first; k < last; k++)
                {
                    int t = Items[k] * 3;
                    if (MeshVoxelizer.CrossZ(p, Triangles[t], Triangles[t + 1], Triangles[t + 2], out float z))
                        crossings.Add(z);
                }

                crossings.Sort();
                for (int k = 0; k + 1 < crossings.Length; k += 2)
                {
                    int from = (int)math.ceil(crossings[k] - 0.5f);
                    int to = (int)math.floor(crossings[k + 1] - 0.5f);
                    // A claw thinner than a voxel still gets one.
                    if (to < from)
                        from = to = (int)math.floor((crossings[k] + crossings[k + 1]) * 0.5f);

                    from = math.max(from, 0);
                    to = math.min(to, Size.z - 1);
                    for (int z = from; z <= to; z++)
                        Solid[x + Size.x * (y + Size.y * z)] = 1;
                }
            }
        }

        [BurstCompile]
        private struct ClassifyJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Solid;
            [ReadOnly] public NativeArray<Segment> Segments;
            public int3 Size;
            public float3 PlaneNormal;
            public float PlaneDistance;
            public uint Seed;
            public float BloodShare;
            [WriteOnly] public NativeArray<byte> Voxels;
            [WriteOnly] public NativeArray<byte> Parts;

            public void Execute(int index)
            {
                if (Solid[index] == 0)
                {
                    Voxels[index] = 0;
                    Parts[index] = NO_PART;
                    return;
                }

                int3 cell = VoxelMath.Coord(index, Size);
                float3 centre = (float3)cell + 0.5f;
                int side = math.dot(PlaneNormal, centre) + PlaneDistance >= 0f ? 1 : -1;

                int part = 0;
                float best = float.MaxValue;
                for (int s = 0; s < Segments.Length; s++)
                {
                    Segment segment = Segments[s];
                    if (segment.Side != 0 && segment.Side != side)
                        continue;
                    float d = DistanceSq(centre, segment.A, segment.B);
                    if (d < best)
                    {
                        best = d;
                        part = segment.Part;
                    }
                }

                Parts[index] = (byte)part;

                int variant = (int)(VoxelMath.Hash01(cell, Seed) * 4f) & 3;
                if (IsSurface(cell))
                    Voxels[index] = (byte)(FleshPalette.FIRST_SKIN + variant);
                else if (VoxelMath.Hash01(cell, Seed + 101u) < BloodShare)
                    Voxels[index] = (byte)(FleshPalette.FIRST_BLOOD + (variant & 1));
                else
                    Voxels[index] = (byte)(FleshPalette.FIRST_MEAT + variant);
            }

            private bool IsSurface(int3 c) =>
                Empty(c + new int3(1, 0, 0)) || Empty(c - new int3(1, 0, 0)) ||
                Empty(c + new int3(0, 1, 0)) || Empty(c - new int3(0, 1, 0)) ||
                Empty(c + new int3(0, 0, 1)) || Empty(c - new int3(0, 0, 1));

            private bool Empty(int3 c) => !VoxelMath.Inside(c, Size) || Solid[VoxelMath.Index(c, Size)] == 0;

            private static float DistanceSq(float3 p, float3 a, float3 b)
            {
                float3 ab = b - a;
                float length = math.lengthsq(ab);
                float t = length > 1e-6f ? math.saturate(math.dot(p - a, ab) / length) : 0f;
                return math.distancesq(p, a + ab * t);
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

PlayMode: `Game.Voxels.Tests.SkinnedVoxelizerTests`, `Game.Voxels.Tests.VoxelPlayModeTests`
(вокселизатор танка после выноса `CrossZ`). Ожидание: все PASS.

- [ ] **Step 5: Без коммита.**

---

### Task 4: `VoxelCarcass`, `VoxelSpray`, `VoxelDrip`

**Files:**
- Create: `Assets/Game/Voxels/Runtime/VoxelCarcass.cs`
- Create: `Assets/Game/Voxels/Runtime/VoxelSpray.cs`
- Create: `Assets/Game/Voxels/Runtime/VoxelDrip.cs`
- Test: `Assets/Game/Voxels/Tests/VoxelCarcassTests.cs`

**Interfaces:**
- Consumes: `SkinnedVoxelizer.Result`, `NO_PART` (Task 3); `VoxelBody.Init`; `VoxelDebris.Drop`, `LyingCount` (Task 1).
- Produces:
  - `VoxelCarcass.MAX_TORN = 256`; `struct TornFace { Vector3 Point; Vector3 Normal; }`;
    `sealed class Piece { VoxelBody Body; int Cluster; int Parts; TornFace[] Torn; }`;
    `static List<Piece> Build(in SkinnedVoxelizer.Result grid, IReadOnlyList<int> clusterOfPart, VoxelBodySettings settings, string name)`
    — `Normal` смотрит из куска наружу, в сторону куска, от которого он оторван.
  - `static int VoxelSpray.Burst(Vector3 at, Vector3 direction, int count, float minSpeed, float maxSpeed, float spread, IReadOnlyList<Color32> colors, float size, Vector3 inherit = default)`
  - `VoxelDrip.Begin(IReadOnlyList<Vector3> worldPoints, Color32[] colors, float size, float duration, float rate)`

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Game/Voxels/Tests/VoxelCarcassTests.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelCarcassTests
    {
        private static readonly Color32[] Green = { new(40, 190, 10, 30) };

        private Transform _root;
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
            foreach (GameObject go in _spawned)
                if (go != null)
                    Object.Destroy(go);
            _spawned.Clear();
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        private static VoxelBodySettings Settings()
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(0.05f);
            settings.Layer = VoxelWorld.PieceLayer;
            settings.Settle = true;
            settings.KeepDebris = true;
            return settings;
        }

        private SkinnedVoxelizer.Result Grid()
        {
            SkinnedMeshRenderer skin = SkinnedColumn.Create(Vector3.zero, out _root, out Transform lower, out Transform upper);
            var parts = new[]
            {
                new SkinnedVoxelizer.Part { Bones = new[] { lower } },
                new SkinnedVoxelizer.Part { Bones = new[] { upper } }
            };
            Assert.IsTrue(SkinnedVoxelizer.Voxelize(skin, parts, new Plane(Vector3.right, 0f), 0.05f,
                SkinnedVoxelizerTests.Palette(), 1, out SkinnedVoxelizer.Result grid));
            return grid;
        }

        private void Floor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _spawned.Add(floor);
            Physics.SyncTransforms();
        }

        [Test]
        public void TwoClusters_TwoBodiesTornWhereTheyMet()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            int solid = grid.Voxels.Count(v => v != 0);

            List<VoxelCarcass.Piece> pieces = VoxelCarcass.Build(grid, new[] { 0, 1 }, Settings(), "Flesh");

            Assert.AreEqual(2, pieces.Count);
            Assert.AreEqual(solid, pieces.Sum(p => p.Body.SolidCount));
            VoxelCarcass.Piece bottom = pieces.Single(p => p.Cluster == 0);
            VoxelCarcass.Piece top = pieces.Single(p => p.Cluster == 1);
            Assert.That(bottom.Torn.Length, Is.GreaterThan(0));
            Assert.That(bottom.Torn.All(f => f.Normal.y > 0.9f), "the bottom was torn on its top face");
            Assert.That(top.Torn.All(f => f.Normal.y < -0.9f));
            Assert.That(bottom.Torn.All(f => Mathf.Abs(f.Point.y - 0.5f) < 0.06f), "at half height");
        }

        [Test]
        public void OneCluster_OneWholeBodyWithoutTears()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            List<VoxelCarcass.Piece> pieces = VoxelCarcass.Build(grid, new[] { 0, 0 }, Settings(), "Flesh");

            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(2, pieces[0].Parts);
            Assert.IsEmpty(pieces[0].Torn);
        }

        [Test]
        public void Body_StandsWhereTheColumnStood()
        {
            using SkinnedVoxelizer.Result grid = Grid();
            List<VoxelCarcass.Piece> pieces = VoxelCarcass.Build(grid, new[] { 0, 0 }, Settings(), "Flesh");

            Bounds bounds = pieces[0].Body.WorldBounds;
            Assert.That(bounds.center.y, Is.EqualTo(0.5f).Within(0.08f));
            Assert.That(bounds.size.y, Is.EqualTo(1f).Within(0.12f));
        }

        [UnityTest]
        public IEnumerator Spray_DropsLandAndLieForGood()
        {
            Floor();
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int before = debris.LyingCount;

            Assert.AreEqual(12, VoxelSpray.Burst(new Vector3(0f, 1f, 0f), Vector3.up, 12, 1f, 2f, 0.3f, Green, 0.025f));

            float until = Time.time + 4f;
            while (debris.LyingCount < before + 12 && Time.time < until)
                yield return null;
            Assert.AreEqual(before + 12, debris.LyingCount);
        }

        [UnityTest]
        public IEnumerator Drip_DropsWhileItLastsThenGoes()
        {
            Floor();
            var go = new GameObject("Piece");
            _spawned.Add(go);
            go.transform.position = Vector3.up * 2f;
            VoxelDebris debris = VoxelWorld.Instance.Debris;
            int before = debris.Count + debris.LyingCount;

            VoxelDrip drip = go.AddComponent<VoxelDrip>();
            drip.Begin(new[] { go.transform.position }, Green, 0.025f, 0.3f, 30f);
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(drip == null, "it goes when its time is up");
            Assert.That(debris.Count + debris.LyingCount - before, Is.GreaterThanOrEqualTo(5));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Компиляция. Ожидание: нет `VoxelCarcass`, `VoxelSpray`, `VoxelDrip`.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Assets/Game/Voxels/Runtime/VoxelCarcass.cs
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Cuts a voxelized body (SkinnedVoxelizer) into bodies, one per cluster of parts: a body that
    /// burst into its head, a chest with both arms and a pair of legs comes out as four VoxelBodies,
    /// each standing exactly where its voxels were. Where two clusters met, the faces torn apart are
    /// listed — that is where the blood comes from.
    /// </summary>
    public static class VoxelCarcass
    {
        /// <summary>Torn faces kept per piece: they only tell where blood comes from.</summary>
        public const int MAX_TORN = 256;

        public struct TornFace
        {
            public Vector3 Point;

            /// <summary>Out of the piece, towards the piece it was torn from.</summary>
            public Vector3 Normal;
        }

        public sealed class Piece
        {
            public VoxelBody Body;
            public int Cluster;

            /// <summary>How many parts of the body the piece holds.</summary>
            public int Parts;

            public TornFace[] Torn;
        }

        private struct TornVoxel
        {
            public int3 Position;
            public int3 Direction;
        }

        /// <param name="clusterOfPart">Cluster of each part, by part index; any ids below the part count.</param>
        /// <param name="settings">For every body; its Origin and VoxelSize are set here.</param>
        public static List<Piece> Build(in SkinnedVoxelizer.Result grid, IReadOnlyList<int> clusterOfPart,
            VoxelBodySettings settings, string name)
        {
            var pieces = new List<Piece>();
            if (!grid.Voxels.IsCreated || clusterOfPart == null || clusterOfPart.Count == 0)
                return pieces;

            int clusters = 0;
            foreach (int c in clusterOfPart)
                clusters = math.max(clusters, c + 1);

            var map = new NativeArray<int>(256, Allocator.TempJob);
            for (int i = 0; i < 256; i++)
                map[i] = i < clusterOfPart.Count ? clusterOfPart[i] : -1;

            var lo = new NativeArray<int3>(clusters, Allocator.TempJob);
            var hi = new NativeArray<int3>(clusters, Allocator.TempJob);
            new ClusterBoundsJob { Parts = grid.Parts, Map = map, Size = grid.Size, Min = lo, Max = hi }.Run();

            for (int c = 0; c < clusters; c++)
            {
                if (hi[c].x < lo[c].x)
                    continue;

                int3 size = hi[c] - lo[c] + 1;
                var target = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Persistent);
                var torn = new NativeList<TornVoxel>(MAX_TORN * 4, Allocator.TempJob);
                new ExtractClusterJob
                {
                    Voxels = grid.Voxels, Parts = grid.Parts, Map = map, Size = grid.Size,
                    Cluster = c, Min = lo[c], TargetSize = size, Target = target, Torn = torn
                }.Run();

                var go = new GameObject($"{name}_{c}") { layer = settings.Layer };
                go.transform.SetPositionAndRotation(grid.Position, grid.Rotation);

                VoxelBodySettings own = settings;
                own.VoxelSize = grid.VoxelSize;
                own.Origin = grid.Origin + (float3)lo[c] * grid.VoxelSize;

                VoxelBody body = go.AddComponent<VoxelBody>();
                body.Init(size, target, new NativeArray<Color32>(grid.Palette, Allocator.Persistent), own);

                pieces.Add(new Piece
                {
                    Body = body,
                    Cluster = c,
                    Parts = CountParts(clusterOfPart, c),
                    Torn = Faces(go.transform, grid, torn)
                });
                torn.Dispose();
            }

            map.Dispose();
            lo.Dispose();
            hi.Dispose();
            return pieces;
        }

        private static int CountParts(IReadOnlyList<int> clusterOfPart, int cluster)
        {
            int count = 0;
            foreach (int c in clusterOfPart)
                if (c == cluster)
                    count++;
            return count;
        }

        /// <summary>At most MAX_TORN faces, picked evenly along the list so the whole cut bleeds.</summary>
        private static TornFace[] Faces(Transform space, in SkinnedVoxelizer.Result grid, NativeList<TornVoxel> torn)
        {
            int count = math.min(torn.Length, MAX_TORN);
            var faces = new TornFace[count];
            int stride = torn.Length > MAX_TORN ? torn.Length / MAX_TORN : 1;
            for (int i = 0; i < count; i++)
            {
                TornVoxel voxel = torn[i * stride];
                float3 local = grid.Origin + ((float3)voxel.Position + 0.5f + (float3)voxel.Direction * 0.5f) * grid.VoxelSize;
                faces[i] = new TornFace
                {
                    Point = space.TransformPoint(local),
                    Normal = space.TransformDirection((float3)voxel.Direction)
                };
            }

            return faces;
        }

        [BurstCompile]
        private struct ClusterBoundsJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Parts;
            [ReadOnly] public NativeArray<int> Map;
            public int3 Size;
            public NativeArray<int3> Min;
            public NativeArray<int3> Max;

            public void Execute()
            {
                for (int c = 0; c < Min.Length; c++)
                {
                    Min[c] = new int3(int.MaxValue);
                    Max[c] = new int3(-1);
                }

                for (int i = 0; i < Parts.Length; i++)
                {
                    byte part = Parts[i];
                    if (part == SkinnedVoxelizer.NO_PART)
                        continue;
                    int c = Map[part];
                    if (c < 0)
                        continue;
                    int3 cell = VoxelMath.Coord(i, Size);
                    Min[c] = math.min(Min[c], cell);
                    Max[c] = math.max(Max[c], cell);
                }
            }
        }

        [BurstCompile]
        private struct ExtractClusterJob : IJob
        {
            [ReadOnly] public NativeArray<byte> Voxels;
            [ReadOnly] public NativeArray<byte> Parts;
            [ReadOnly] public NativeArray<int> Map;
            public int3 Size;
            public int Cluster;
            public int3 Min;
            public int3 TargetSize;
            public NativeArray<byte> Target;
            public NativeList<TornVoxel> Torn;

            public void Execute()
            {
                for (int z = 0; z < TargetSize.z; z++)
                for (int y = 0; y < TargetSize.y; y++)
                for (int x = 0; x < TargetSize.x; x++)
                {
                    int3 cell = Min + new int3(x, y, z);
                    int index = VoxelMath.Index(cell, Size);
                    byte part = Parts[index];
                    if (part == SkinnedVoxelizer.NO_PART || Map[part] != Cluster)
                        continue;

                    Target[VoxelMath.Index(new int3(x, y, z), TargetSize)] = Voxels[index];
                    Check(cell, new int3(1, 0, 0));
                    Check(cell, new int3(-1, 0, 0));
                    Check(cell, new int3(0, 1, 0));
                    Check(cell, new int3(0, -1, 0));
                    Check(cell, new int3(0, 0, 1));
                    Check(cell, new int3(0, 0, -1));
                }
            }

            /// <summary>A neighbour of another cluster: this face was torn.</summary>
            private void Check(int3 cell, int3 direction)
            {
                int3 next = cell + direction;
                if (!VoxelMath.Inside(next, Size))
                    return;
                byte part = Parts[VoxelMath.Index(next, Size)];
                if (part == SkinnedVoxelizer.NO_PART)
                    return;
                int other = Map[part];
                if (other >= 0 && other != Cluster)
                    Torn.Add(new TornVoxel { Position = cell, Direction = direction });
            }
        }
    }
}
```

```csharp
// Assets/Game/Voxels/Runtime/VoxelSpray.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>A fan of drops (VoxelDebris.Drop): blood out of a wound or a torn seam.</summary>
    public static class VoxelSpray
    {
        /// <param name="spread">How far a drop strays from <paramref name="direction"/>: 0 — none, 1 — a wide cone.</param>
        /// <param name="inherit">Velocity of what bleeds, added to every drop.</param>
        /// <returns>How many drops flew (fewer when the debris store is full).</returns>
        public static int Burst(Vector3 at, Vector3 direction, int count, float minSpeed, float maxSpeed, float spread,
            IReadOnlyList<Color32> colors, float size, Vector3 inherit = default)
        {
            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null || colors == null || colors.Count == 0 || count <= 0)
                return 0;

            Vector3 aim = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 heading = (aim + Random.insideUnitSphere * spread).normalized;
                Vector3 velocity = inherit + heading * Random.Range(minSpeed, maxSpeed);
                Color32 color = colors[Random.Range(0, colors.Count)];
                if (debris.Drop(at, velocity, color, size * Random.Range(0.8f, 1.2f)) < 0)
                    break;
                spawned++;
            }

            return spawned;
        }
    }
}
```

```csharp
// Assets/Game/Voxels/Runtime/VoxelDrip.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A piece just torn off bleeds as it flies: for a short while it drops blood from its torn
    /// faces, so a trail of drops marks its arc on the ground.
    /// </summary>
    public sealed class VoxelDrip : MonoBehaviour
    {
        private Vector3[] _points;
        private Color32[] _colors;
        private float _size;
        private float _until;
        private float _interval;
        private float _next;
        private Rigidbody _body;

        /// <param name="worldPoints">Where the drops come from, now; they ride with this object from here on.</param>
        public void Begin(IReadOnlyList<Vector3> worldPoints, Color32[] colors, float size, float duration, float rate)
        {
            if (worldPoints == null || worldPoints.Count == 0 || colors == null || colors.Length == 0)
            {
                Destroy(this);
                return;
            }

            _points = new Vector3[worldPoints.Count];
            for (int i = 0; i < _points.Length; i++)
                _points[i] = transform.InverseTransformPoint(worldPoints[i]);
            _colors = colors;
            _size = size;
            _until = Time.time + duration;
            _interval = 1f / Mathf.Max(0.01f, rate);
            _next = Time.time;
        }

        private void Update()
        {
            if (_points == null || Time.time >= _until)
            {
                Destroy(this);
                return;
            }

            VoxelWorld world = VoxelWorld.Instance;
            VoxelDebris debris = world != null ? world.Debris : null;
            if (debris == null)
                return;
            if (_body == null)
                TryGetComponent(out _body);

            while (_next <= Time.time)
            {
                _next += _interval;
                Vector3 point = transform.TransformPoint(_points[Random.Range(0, _points.Length)]);
                Vector3 velocity = _body != null && !_body.isKinematic ? _body.GetPointVelocity(point) : Vector3.zero;
                debris.Drop(point, velocity + Random.insideUnitSphere * 0.4f, _colors[Random.Range(0, _colors.Length)], _size);
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

PlayMode: `Game.Voxels.Tests.VoxelCarcassTests` и все предыдущие классы `Game.Voxels.Tests`.
Ожидание: все PASS.

- [ ] **Step 5: Без коммита.**

---

### Task 5: `GibRules` — общие правила Maya- и воксельных гибов

**Files:**
- Create: `Assets/Game/Scripts/Features/Character/Common/Gibs/GibRules.cs`
- Modify: `Assets/Game/Scripts/Features/Character/Common/Gibs/Gibs.cs`
- Modify: `Assets/Game/Scripts/Features/Character/Common/Gibs/GibsOnDeathBehaviour.cs`
- Test: `Assets/Game/Tests/Features/Character/Common/Gibs/GibRulesTests.cs`

**Interfaces:**
- Produces (namespace `Game`):
  - `readonly struct SeamLink(int a, int b, float hold)` — поля `A`, `B`, `Hold`
  - `readonly struct KillingHit(Vector3 origin, float damage, bool blast, float blastShare)` — `Origin`, `Damage`, `Blast`, `BlastShare`
  - `[Serializable] sealed class GibSpeeds` — `minSpeed 1.5, maxSpeed 4, fullSpeedDamage 100, lift 0.35, spin 3, speedJitter 0.4, scatter 0.4, blastMinSpeed 2, blastMaxSpeed 4, blastLift 0.4, blastSpin 5`
  - `static class GibRules`:
    - `KillingHit ReadHit(TakeDamageArgs hit, Vector3 fallback)`
    - `bool TryGetPosition(IEntity source, out Vector3 position)`
    - `int[] RollClusters(int pieces, IReadOnlyList<SeamLink> seams, Func<float> roll, List<int> held)`
    - `void Throw(GibSpeeds speeds, in KillingHit hit, out float speed, out float lift, out float spin)`
    - `Vector3 ClusterVelocity(Vector3 centre, Vector3 origin, float speed, float lift, GibSpeeds speeds)`
    - `Vector3 ClusterSpin(float spin, float weight)` — weight 1 у одиночной конечности

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Game/Tests/Features/Character/Common/Gibs/GibRulesTests.cs
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GibRulesTests
    {
        private static readonly SeamLink[] Chain = { new(0, 1, 1f), new(1, 2, 1f) };

        [Test]
        public void EverySeamHolds_StillTwoClusters()
        {
            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(3, Chain, () => 0f, held);

            Assert.AreEqual(2, cluster.Distinct().Count(), "a body that stayed whole is no burst");
            Assert.AreEqual(1, held.Count);
        }

        [Test]
        public void NoSeamHolds_EveryPieceAlone()
        {
            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(3, Chain, () => 0.99f, held);

            Assert.AreEqual(3, cluster.Distinct().Count());
            Assert.IsEmpty(held);
        }

        [Test]
        public void AHeldSeam_KeepsItsPiecesTogether()
        {
            var seams = new[] { new SeamLink(0, 1, 1f), new SeamLink(1, 2, 0f) };
            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(3, seams, () => 0.5f, held);

            Assert.AreEqual(cluster[0], cluster[1]);
            Assert.AreNotEqual(cluster[1], cluster[2]);
            CollectionAssert.AreEqual(new[] { 0 }, held);
        }

        [Test]
        public void NoHit_TheBodyItselfIsTheOrigin()
        {
            KillingHit hit = GibRules.ReadHit(null, new Vector3(1f, 2f, 3f));

            Assert.AreEqual(new Vector3(1f, 2f, 3f), hit.Origin);
            Assert.IsFalse(hit.Blast);
            Assert.AreEqual(0f, hit.Damage);
        }

        [Test]
        public void AClusterFliesAwayFromTheOrigin()
        {
            var speeds = new GibSpeeds { scatter = 0f, speedJitter = 0f };
            Vector3 velocity = GibRules.ClusterVelocity(new Vector3(2f, 0f, 0f), Vector3.zero, 3f, 0f, speeds);

            Assert.That(velocity.x, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(velocity.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void ABlastThrowsByHowCloseItWent()
        {
            var speeds = new GibSpeeds();
            GibRules.Throw(speeds, new KillingHit(Vector3.zero, 0f, true, 1f), out float centre, out _, out _);
            GibRules.Throw(speeds, new KillingHit(Vector3.zero, 0f, true, 0f), out float edge, out _, out _);

            Assert.AreEqual(speeds.blastMaxSpeed, centre);
            Assert.AreEqual(speeds.blastMinSpeed, edge);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Компиляция. Ожидание: нет `GibRules`, `SeamLink`, `KillingHit`, `GibSpeeds`.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Assets/Game/Scripts/Features/Character/Common/Gibs/GibRules.cs
using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game
{
    /// <summary>A seam between two pieces of a body, and its chance to hold when the body bursts.</summary>
    public readonly struct SeamLink
    {
        public readonly int A;
        public readonly int B;
        public readonly float Hold;

        public SeamLink(int a, int b, float hold)
        {
            A = a;
            B = b;
            Hold = hold;
        }
    }

    /// <summary>What killed a body, as far as its burst cares.</summary>
    public readonly struct KillingHit
    {
        /// <summary>Where the hit came from: the bullet, the shell where it went off, the attacker.</summary>
        public readonly Vector3 Origin;

        public readonly float Damage;
        public readonly bool Blast;

        /// <summary>1 at a blast's centre, 0 at its edge.</summary>
        public readonly float BlastShare;

        public KillingHit(Vector3 origin, float damage, bool blast, float blastShare)
        {
            Origin = origin;
            Damage = damage;
            Blast = blast;
            BlastShare = blastShare;
        }
    }

    /// <summary>How hard the clusters of a burst body fly. The defaults are Regax_Gibs'.</summary>
    [Serializable]
    public sealed class GibSpeeds
    {
        [Tooltip("Speed (m/s) a cluster leaves at from the weakest hit.")]
        public float minSpeed = 1.5f;

        [Tooltip("Speed (m/s) a cluster leaves at from the strongest hit.")]
        public float maxSpeed = 4f;

        [Tooltip("Damage of a hit that throws at full speed; weaker hits scale down to the minimum.")]
        public float fullSpeedDamage = 100f;

        [Tooltip("Share of the speed added straight up, so pieces arc instead of skimming the floor.")]
        public float lift = 0.35f;

        [Tooltip("Random spin (rad/s) per axis, for a lone limb; bigger clusters turn slower.")]
        public float spin = 3f;

        [Tooltip("Each cluster's speed is scaled by a random factor within 1 ± this.")]
        public float speedJitter = 0.4f;

        [Tooltip("How far (share of the speed) a cluster's direction may stray from straight away.")]
        public float scatter = 0.4f;

        [Tooltip("Speed (m/s) a cluster leaves at from the edge of a blast.")]
        public float blastMinSpeed = 2f;

        [Tooltip("Speed (m/s) a cluster leaves at from the centre of a blast.")]
        public float blastMaxSpeed = 4f;

        public float blastLift = 0.4f;
        public float blastSpin = 5f;
    }

    /// <summary>
    /// The rules every burst body follows, whether its pieces are Maya-cut ragdolls (Gibs) or voxels
    /// (VoxelGibs): what the killing hit was, which seams hold, how fast and which way each
    /// cluster flies.
    /// </summary>
    public static class GibRules
    {
        /// <remarks>
        /// The death event carries the dying entity, not its killer, so the killer is read from the
        /// last damage event. DamageUseCase raises that from inside Health.Reduce, before
        /// OnHealthEmpty, so the last hit is always known by the time the death event arrives.
        /// </remarks>
        public static KillingHit ReadHit(TakeDamageArgs hit, Vector3 fallback)
        {
            Vector3 origin = fallback;
            float damage = 0f;
            bool blast = false;
            float blastShare = 1f;

            if (hit?.source != null)
            {
                if (TryGetPosition(hit.source, out Vector3 from))
                    origin = from;

                if (hit.source.TryGetDamage(out IReactiveVariable<int> sourceDamage))
                    damage = sourceDamage.Value * hit.damageFactor;

                // A tank shell or a rocket: the shell sits where it went off, and the damage factor is
                // how close to the centre the body stood (1 there, the projectile's minimum at the edge).
                blast = ExplosionUseCase.IsExplosive(hit.source);
                if (blast)
                {
                    float minFactor = hit.source.TryGetExplosionMinDamageFactor(out float min) ? min : 0.25f;
                    blastShare = Mathf.InverseLerp(minFactor, 1f, hit.damageFactor);
                }
            }

            return new KillingHit(origin, damage, blast, blastShare);
        }

        public static bool TryGetPosition(IEntity source, out Vector3 position)
        {
            if (source.TryGetTransform(out Transform transform) && transform != null)
            {
                position = transform.position;
                return true;
            }

            if (source.TryGetGameObject(out GameObject gameObject) && gameObject != null)
            {
                position = gameObject.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Rolls every seam and returns each piece's cluster id; <paramref name="held"/> gets the
        /// indices of the seams that held. Two pieces or more always give at least two clusters: a
        /// body that stayed whole is no burst, so random held seams are let go until it splits.
        /// </summary>
        public static int[] RollClusters(int pieces, IReadOnlyList<SeamLink> seams, Func<float> roll, List<int> held)
        {
            held.Clear();
            for (int i = 0; i < seams.Count; i++)
                if (roll() < seams[i].Hold)
                    held.Add(i);

            int[] cluster = Clusters(pieces, seams, held);
            while (held.Count > 0 && Distinct(cluster) < 2)
            {
                held.RemoveAt(Random.Range(0, held.Count));
                cluster = Clusters(pieces, seams, held);
            }

            return cluster;
        }

        public static void Throw(GibSpeeds speeds, in KillingHit hit, out float speed, out float lift, out float spin)
        {
            speed = hit.Blast
                ? Mathf.Lerp(speeds.blastMinSpeed, speeds.blastMaxSpeed, Mathf.Clamp01(hit.BlastShare))
                : Mathf.Lerp(speeds.minSpeed, speeds.maxSpeed, Mathf.Clamp01(hit.Damage / speeds.fullSpeedDamage));
            lift = hit.Blast ? speeds.blastLift : speeds.lift;
            spin = hit.Blast ? speeds.blastSpin : speeds.spin;
        }

        /// <summary>Away from the hit (never down into the floor), lifted, scattered, at a jittered speed.</summary>
        public static Vector3 ClusterVelocity(Vector3 centre, Vector3 origin, float speed, float lift, GibSpeeds speeds)
        {
            Vector3 away = centre - origin;
            away.y = Mathf.Max(away.y, 0f);
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.up;

            float clusterSpeed = speed * Random.Range(1f - speeds.speedJitter, 1f + speeds.speedJitter);
            return (away.normalized + Vector3.up * lift + Random.insideUnitSphere * speeds.scatter) * clusterSpeed;
        }

        /// <param name="weight">How big the cluster is: 1 for a lone limb, more for more.</param>
        /// <remarks>A whole chest with its arms spinning like a lone hand looks weightless.</remarks>
        public static Vector3 ClusterSpin(float spin, float weight) =>
            Random.insideUnitSphere * (spin / Mathf.Sqrt(weight + 0.5f));

        private static int[] Clusters(int count, IReadOnlyList<SeamLink> seams, List<int> held)
        {
            int[] parent = new int[count];
            for (int i = 0; i < count; i++)
                parent[i] = i;

            int Find(int i)
            {
                while (parent[i] != i)
                    i = parent[i] = parent[parent[i]];
                return i;
            }

            foreach (int s in held)
                parent[Find(seams[s].A)] = Find(seams[s].B);

            for (int i = 0; i < count; i++)
                parent[i] = Find(i);
            return parent;
        }

        private static int Distinct(int[] cluster) => new HashSet<int>(cluster).Count;
    }
}
```

`Gibs.cs`:
- добавить свойство

```csharp
        private GibSpeeds Speeds => new()
        {
            minSpeed = _minSpeed, maxSpeed = _maxSpeed, fullSpeedDamage = _fullSpeedDamage, lift = _lift, spin = _spin,
            speedJitter = _speedJitter, scatter = _scatter, blastMinSpeed = _blastMinSpeed,
            blastMaxSpeed = _blastMaxSpeed, blastLift = _blastLift, blastSpin = _blastSpin
        };
```

- в `Burst` заменить расчёт `speed`/`lift`/`spinSpeed` и цикл по кластерам:

```csharp
            GibSpeeds speeds = Speeds;
            GibRules.Throw(speeds, new KillingHit(origin, damage, blast, blastShare),
                out float speed, out float lift, out float spinSpeed);

            // ... members dictionary as before ...

            foreach (List<Rigidbody> bodies in members.Values)
            {
                Vector3 velocity = GibRules.ClusterVelocity(Centre(bodies), origin, speed, lift, speeds);
                // Three bodies make a lone limb.
                Vector3 spin = GibRules.ClusterSpin(spinSpeed, bodies.Count / 3f);

                foreach (Rigidbody body in bodies)
                {
                    body.isKinematic = false;
                    body.linearVelocity = velocity;
                    body.angularVelocity = spin;
                }
            }
```

- `HoldSeams` через правила; удалить `Clusters` и `CountClusters`:

```csharp
        private int[] HoldSeams()
        {
            Seam[] seams = _seams ?? System.Array.Empty<Seam>();
            var links = new SeamLink[seams.Length];
            for (int i = 0; i < seams.Length; i++)
                links[i] = new SeamLink(seams[i].pieceA, seams[i].pieceB, seams[i].holdChance);

            var held = new List<int>();
            int[] cluster = GibRules.RollClusters(_pieces.Length, links, () => Random.value, held);
            foreach (int i in held)
                Join(seams[i]);
            return cluster;
        }
```

`GibsOnDeathBehaviour.cs`: `OnDeath` становится

```csharp
        private void OnDeath(TakeDamageArgs _)
        {
            foreach (Renderer renderer in _body)
                renderer.enabled = false;

            KillingHit hit = GibRules.ReadHit(_lastHit, _skeleton.position);
            Gibs gibs = Object.Instantiate(_prefab, _skeleton.position, _skeleton.rotation);
            gibs.Burst(_skeleton, hit.Origin, hit.Damage, hit.Blast, hit.BlastShare);
        }
```

`TryGetPosition` из него удалить (`using Atomic.Elements;` остаётся — нужен для `IEvent`).
Абзац remarks про «last damage event» переехал в `GibRules.ReadHit` — в классе оставить
ссылку: «See GibRules.ReadHit for how the killing hit is known.»

- [ ] **Step 4: Run test to verify it passes**

EditMode: `Game.Tests.GibRulesTests` и `Game.Tests.GibsOnDeathBehaviourTests` (регрессия
Maya-гибов). Ожидание: все PASS.

- [ ] **Step 5: Без коммита.**

---

### Task 6: Смерть в воксели — `VoxelGibs` и инсталлер

**Files:**
- Create: `Assets/Game/Scripts/Features/Health/Vfx/BloodLook.cs`
- Create: `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsConfig.cs`
- Create: `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsRig.cs`
- Create: `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibs.cs`
- Create: `Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsOnDeathBehaviour.cs`
- Create: `Assets/Game/Scripts/Features/Character/Common/Gibs/CharacterVoxelGibsInstaller.cs`
- Modify: `Assets/Game/Voxels/Tests/Scene/Game.Voxels.SceneTests.asmdef` — в `references` добавить `"Game.Voxels.Tests"`
- Test: `Assets/Game/Voxels/Tests/Scene/VoxelGibsPlayModeTests.cs`

**Interfaces:**
- Consumes: всё из Tasks 1–5.
- Produces (namespace `Game`):
  - `[Serializable] BloodLook { Color[] colors; float wetness = 0.9f; float dropSize = 0.025f; Color32[] ToVoxels(); }`
  - `[Serializable] VoxelGibsConfig` с вложенными `PartSpec { string name; string[] bones; int side; }`,
    `SeamSpec { int a; int b; float hold; }` и полями `voxelSize, toughness, density, fractureSpeed,
    spineFrom, spineTo, leftMarker, parts, seams, speeds, meat, meatWetness, bloodShare, skinNoise,
    blood, seamDrops, hitDrops, dripTime, dripRate`
  - `VoxelGibsRig { Skin, Skeleton, Hide, Parts, SpineFrom, SpineTo, LeftMarker; Color SkinColor;
    static VoxelGibsRig Resolve(SkinnedMeshRenderer, Transform, Renderer[], VoxelGibsConfig); Plane MidPlane(); }`
  - `VoxelGibs.NAME = "Flesh"`; `static List<VoxelCarcass.Piece> VoxelGibs.Burst(VoxelGibsRig rig, VoxelGibsConfig config, in KillingHit hit)`
  - `VoxelGibsOnDeathBehaviour(VoxelGibsRig rig, VoxelGibsConfig config)`
  - `CharacterVoxelGibsInstaller` — сериализуемые `_skin`, `_skeleton`, `_hide`, `_config`

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Game/Voxels/Tests/Scene/VoxelGibsPlayModeTests.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelGibsPlayModeTests
    {
        private readonly List<GameObject> _created = new();
        private SkinnedMeshRenderer _skin;
        private IEntity _character;

        [SetUp]
        public void SetUp()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = Vector3.one * 4f;
            _created.Add(floor);
            Physics.SyncTransforms();

            _skin = SkinnedColumn.Create(new Vector3(0f, 0.05f, 0f), out Transform root, out _, out _);
            _created.Add(root.gameObject);

            var config = new VoxelGibsConfig
            {
                parts = new[]
                {
                    new VoxelGibsConfig.PartSpec { name = "Bottom", bones = new[] { "Lower" } },
                    new VoxelGibsConfig.PartSpec { name = "Top", bones = new[] { "Upper" } }
                },
                seams = new[] { new VoxelGibsConfig.SeamSpec { a = 0, b = 1, hold = 0f } },
                spineFrom = "Lower",
                spineTo = "Upper",
                leftMarker = ""
            };

            _character = new Entity();
            _character.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            _character.AddDeathTakenEvent(new Event<TakeDamageArgs>());
            var rig = VoxelGibsRig.Resolve(_skin, root, new Renderer[] { _skin }, config);
            new VoxelGibsOnDeathBehaviour(rig, config).Init(_character);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                if (go != null)
                    Object.Destroy(go);
            _created.Clear();
            foreach (VoxelBody body in Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None))
                Object.Destroy(body.gameObject);
        }

        private void KilledFrom(Vector3 position)
        {
            var go = new GameObject("Killer");
            _created.Add(go);
            go.transform.position = position;

            IEntity killer = new Entity();
            killer.AddTransform(go.transform);
            killer.AddDamage(new ReactiveInt(50));
            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(killer));
            _character.GetDeathTakenEvent().Invoke(new TakeDamageArgs(_character));
        }

        private static VoxelBody[] Flesh() =>
            Object.FindObjectsByType<VoxelBody>(FindObjectsSortMode.None).Where(b => b.name.Contains(VoxelGibs.NAME)).ToArray();

        private static int Drops() => VoxelWorld.Instance.Debris.Count + VoxelWorld.Instance.Debris.LyingCount;

        [UnityTest]
        public IEnumerator Death_HidesTheSkin_AndThrowsFleshAwayFromTheKiller()
        {
            int before = Drops();
            KilledFrom(new Vector3(0f, 0.5f, -5f));

            Assert.IsFalse(_skin.enabled);
            Assert.AreEqual(2, Flesh().Length, "the seam never holds: two pieces");
            Assert.That(Drops(), Is.GreaterThan(before + 40), "blood from the torn seam and the wound");

            yield return null;
            yield return new WaitForFixedUpdate();
            foreach (VoxelBody piece in Flesh())
            {
                Rigidbody rigidbody = piece.GetComponent<Rigidbody>();
                Assert.IsNotNull(rigidbody);
                Assert.IsFalse(rigidbody.isKinematic);
                Assert.That(rigidbody.linearVelocity.z, Is.GreaterThan(0f), "away from the killer behind it");
            }
        }

        [UnityTest]
        public IEnumerator Flesh_SettlesAndStays()
        {
            KilledFrom(new Vector3(0f, 0.5f, -5f));
            yield return new WaitForSeconds(8f);

            VoxelBody[] flesh = Flesh();
            Assert.That(flesh.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(flesh.Count(b => !b.IsLoose), Is.GreaterThan(0), "pieces at rest drop their Rigidbody");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Компиляция. Ожидание: нет `VoxelGibsConfig`, `VoxelGibsRig`, `VoxelGibsOnDeathBehaviour`, `VoxelGibs`.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Assets/Game/Scripts/Features/Health/Vfx/BloodLook.cs
using System;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>What a body's blood looks like as voxel drops. The defaults are Regax's green.</summary>
    [Serializable]
    public sealed class BloodLook
    {
        [Tooltip("Drop colours (sRGB): the old pack blood's M_RegaxBlood_Fluid and M_RegaxBlood_Decal.")]
        public Color[] colors = { new(0.25f, 0.75f, 0.05f), new(0.13f, 0.44f, 0.02f) };

        [Tooltip("0 dry, 1 soaking: glossier and darker in the Game/Voxel shader.")]
        [Range(0f, 1f)] public float wetness = 0.9f;

        [Tooltip("Edge of one drop, m: half a voxel.")]
        public float dropSize = 0.025f;

        public Color32[] ToVoxels()
        {
            var voxels = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                voxels[i] = MeshVoxelizer.WithWetness(MeshVoxelizer.ToVertexColor(colors[i]), wetness);
            return voxels;
        }
    }
}
```

```csharp
// Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsConfig.cs
using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How a body comes apart in voxels: its parts by bone names, the seams between them, how hard
    /// the pieces fly, what the flesh and blood look like. The defaults are Regax's (the bones and
    /// seams of RegaxGibsBuilder; Docs/superpowers/specs/2026-11-16-regax-voxel-death.md).
    /// </summary>
    [Serializable]
    public sealed class VoxelGibsConfig
    {
        [Serializable]
        public sealed class PartSpec
        {
            public string name;
            public string[] bones;

            [Tooltip("0 — the whole part; +1 — only the body's left of the spine plane, −1 — its right.")]
            public int side;
        }

        [Serializable]
        public sealed class SeamSpec
        {
            public int a;
            public int b;
            [Range(0f, 1f)] public float hold;
        }

        [Tooltip("Edge of one voxel, m.")]
        public float voxelSize = 0.05f;

        [Tooltip("Blast radius multiplier on the pieces: a shell bites about 1 m out of them, not half the body.")]
        [Range(0.1f, 1f)] public float toughness = 0.6f;

        [Tooltip("kg/m³, for the mass of the pieces.")]
        public float density = 1000f;

        [Tooltip("Slowest landing (m/s) that breaks a piece: above a fall from the body's height (~5.4).")]
        public float fractureSpeed = 7f;

        [Tooltip("The spine plane (cuts the torso in halves) runs through these two bones…")]
        public string spineFrom = "Rapax_ Pelvis";
        public string spineTo = "Rapax_ Spine2";

        [Tooltip("…and its + side is the one this bone is on.")]
        public string leftMarker = "Rapax_ L UpperArm";

        public PartSpec[] parts =
        {
            new() { name = "Head", bones = new[] { "Rapax_ Neck", "Rapax_ Head", "Rapax_JawLeft", "Rapax_JawMiddle", "Rapax_JawRight" } },
            new() { name = "Arm_L", bones = new[] { "Rapax_ L UpperArm", "Rapax_ L Forearm", "Rapax_ L Hand", "Rapax_ L Finger0", "Rapax_ L Finger1", "Rapax_ L Finger2" } },
            new() { name = "Arm_R", bones = new[] { "Rapax_ R UpperArm", "Rapax_ R Forearm", "Rapax_ R Hand", "Rapax_ R Finger0", "Rapax_ R Finger1", "Rapax_ R Finger2" } },
            new() { name = "Leg_L", bones = new[] { "Rapax_ L Thigh", "Rapax_ L Calf", "Rapax_ L Foot", "Rapax_ L Toe0", "Rapax_ L Toe1", "Rapax_ L Toe2" } },
            new() { name = "Leg_R", bones = new[] { "Rapax_ R Thigh", "Rapax_ R Calf", "Rapax_ R Foot", "Rapax_ R Toe0", "Rapax_ R Toe1", "Rapax_ R Toe2" } },
            new() { name = "Torso_L", side = 1, bones = Torso },
            new() { name = "Torso_R", side = -1, bones = Torso }
        };

        public SeamSpec[] seams =
        {
            new() { a = 5, b = 6, hold = 0.5f },
            new() { a = 5, b = 1, hold = 0.4f },
            new() { a = 6, b = 2, hold = 0.4f },
            new() { a = 5, b = 3, hold = 0.35f },
            new() { a = 6, b = 4, hold = 0.35f },
            new() { a = 5, b = 0, hold = 0.2f },
            new() { a = 6, b = 0, hold = 0.2f }
        };

        public GibSpeeds speeds = new();

        [Header("Flesh")]
        [Tooltip("Meat inside the skin (sRGB), dark red to pink.")]
        public Color[] meat =
        {
            new(0.35f, 0.04f, 0.05f), new(0.55f, 0.10f, 0.12f), new(0.70f, 0.25f, 0.28f), new(0.45f, 0.15f, 0.10f)
        };

        [Range(0f, 1f)] public float meatWetness = 0.6f;

        [Tooltip("Share of the meat that is veins of blood.")]
        [Range(0f, 1f)] public float bloodShare = 0.15f;

        [Tooltip("How far each skin voxel's shade strays from the skin material's colour.")]
        [Range(0f, 0.3f)] public float skinNoise = 0.08f;

        public BloodLook blood = new();

        [Header("Bleeding")]
        [Tooltip("Drops out of each torn seam.")]
        public int seamDrops = 40;

        [Tooltip("Drops out of the killing wound.")]
        public int hitDrops = 60;

        [Tooltip("How long (s) a torn piece drips as it flies, and how many drops a second.")]
        public float dripTime = 0.6f;
        public float dripRate = 30f;

        private static string[] Torso => new[]
        {
            "Rapax_ Pelvis", "Rapax_ Spine", "Rapax_ Spine1", "Rapax_ Spine2", "Rapax_ L Clavicle",
            "Rapax_ R Clavicle", "Rapax_ Tail", "Rapax_ Tail1", "Rapax_ Tail2"
        };
    }
}
```

```csharp
// Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsRig.cs
using System;
using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>A body's skin and bones, found by the names a VoxelGibsConfig gives: what VoxelGibs cuts.</summary>
    public sealed class VoxelGibsRig
    {
        public SkinnedMeshRenderer Skin;
        public Transform Skeleton;
        public Renderer[] Hide;
        public SkinnedVoxelizer.Part[] Parts;
        public Transform SpineFrom;
        public Transform SpineTo;
        public Transform LeftMarker;

        /// <summary>The skin material's colour: Regax's M_Rapax 1 is a plain Standard with no texture.</summary>
        public Color SkinColor
        {
            get
            {
                Material material = Skin != null ? Skin.sharedMaterial : null;
                return material != null && material.HasProperty("_Color") ? material.color : Color.gray;
            }
        }

        public static VoxelGibsRig Resolve(SkinnedMeshRenderer skin, Transform skeleton, Renderer[] hide, VoxelGibsConfig config)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (Transform bone in skeleton.GetComponentsInChildren<Transform>(true))
                bones.TryAdd(bone.name, bone);

            Transform Find(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;
                if (bones.TryGetValue(name, out Transform bone))
                    return bone;
                Debug.LogWarning($"[VOXEL GIBS] no bone '{name}' under {skeleton.name}");
                return null;
            }

            var parts = new SkinnedVoxelizer.Part[config.parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var found = new List<Transform>();
                foreach (string name in config.parts[i].bones ?? Array.Empty<string>())
                {
                    Transform bone = Find(name);
                    if (bone != null)
                        found.Add(bone);
                }

                parts[i] = new SkinnedVoxelizer.Part { Bones = found.ToArray(), Side = config.parts[i].side };
            }

            return new VoxelGibsRig
            {
                Skin = skin,
                Skeleton = skeleton,
                Hide = hide ?? Array.Empty<Renderer>(),
                Parts = parts,
                SpineFrom = Find(config.spineFrom),
                SpineTo = Find(config.spineTo),
                LeftMarker = Find(config.leftMarker)
            };
        }

        /// <summary>
        /// The plane down the spine that cuts the torso in halves, its normal towards the body's left
        /// (the side LeftMarker is on), whatever pose the body died in.
        /// </summary>
        public Plane MidPlane()
        {
            Vector3 point = SpineFrom != null ? SpineFrom.position : Skeleton.position;
            Vector3 spine = SpineFrom != null && SpineTo != null ? SpineTo.position - SpineFrom.position : Skeleton.up;
            Vector3 normal = Vector3.Cross(spine, Skeleton.forward);
            if (normal.sqrMagnitude < 1e-6f)
                normal = Skeleton.right;
            normal.Normalize();
            if (LeftMarker != null && Vector3.Dot(LeftMarker.position - point, normal) < 0f)
                normal = -normal;
            return new Plane(normal, point);
        }
    }
}
```

```csharp
// Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibs.cs
using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A body dies into voxels: its skin is hidden, the pose it died in becomes voxels (skin, meat,
    /// veins of blood), cut into clusters of parts by the same seam rules the Maya gibs use, each
    /// cluster a rigid VoxelBody thrown away from the killing hit. Torn seams and the wound spray
    /// blood drops, and torn pieces drip as they fly. Pieces, crumbs and drops lie all game.
    /// </summary>
    /// <remarks>
    /// A shell's blast also bites into the fresh pieces: ExplosionUseCase kills (DamageInRadius)
    /// before it carves voxels (VoxelDamage.Blast), so nothing here carves.
    /// </remarks>
    public static class VoxelGibs
    {
        public const string NAME = "Flesh";

        public static List<VoxelCarcass.Piece> Burst(VoxelGibsRig rig, VoxelGibsConfig config, in KillingHit hit)
        {
            Bounds body = rig.Skin.bounds; // before the skin is hidden
            foreach (Renderer renderer in rig.Hide)
                if (renderer != null)
                    renderer.enabled = false;

            var palette = new FleshPalette
            {
                Skin = rig.SkinColor,
                SkinNoise = config.skinNoise,
                Meat = config.meat,
                MeatWetness = config.meatWetness,
                Blood = config.blood.colors,
                BloodWetness = config.blood.wetness,
                BloodShare = config.bloodShare
            };

            if (!SkinnedVoxelizer.Voxelize(rig.Skin, rig.Parts, rig.MidPlane(), config.voxelSize, palette,
                    (uint)Random.Range(1, int.MaxValue), out SkinnedVoxelizer.Result grid))
                return new List<VoxelCarcass.Piece>();

            var links = new SeamLink[config.seams.Length];
            for (int i = 0; i < links.Length; i++)
                links[i] = new SeamLink(config.seams[i].a, config.seams[i].b, config.seams[i].hold);
            int[] cluster = GibRules.RollClusters(config.parts.Length, links, () => Random.value, new List<int>());

            VoxelBodySettings settings = VoxelBodySettings.Default(config.voxelSize);
            settings.Anchor = VoxelAnchor.Largest;
            settings.Colliders = true;
            settings.Layer = VoxelWorld.PieceLayer;
            settings.Toughness = config.toughness;
            settings.Density = config.density;
            settings.ChunkSize = 32;
            settings.Settle = true;
            settings.KeepDebris = true;
            settings.FractureSpeed = config.fractureSpeed;

            List<VoxelCarcass.Piece> pieces;
            try
            {
                pieces = VoxelCarcass.Build(grid, cluster, settings, $"{rig.Skin.name}_{NAME}");
            }
            finally
            {
                grid.Dispose();
            }

            GibRules.Throw(config.speeds, hit, out float speed, out float lift, out float spin);
            Color32[] blood = config.blood.ToVoxels();

            int broken = 0;
            foreach (SeamLink seam in links)
                if (cluster[seam.A] != cluster[seam.B])
                    broken++;
            int torn = 0;
            foreach (VoxelCarcass.Piece piece in pieces)
                torn += piece.Torn.Length;

            foreach (VoxelCarcass.Piece piece in pieces)
            {
                Vector3 velocity = GibRules.ClusterVelocity(piece.Body.WorldBounds.center, hit.Origin, speed, lift, config.speeds);
                piece.Body.Launch(velocity, GibRules.ClusterSpin(spin, piece.Parts));
                piece.Body.SplitLoose();

                if (piece.Torn.Length == 0)
                    continue;

                var points = new Vector3[piece.Torn.Length];
                for (int i = 0; i < points.Length; i++)
                    points[i] = piece.Torn[i].Point;
                piece.Body.gameObject.AddComponent<VoxelDrip>()
                    .Begin(points, blood, config.blood.dropSize, config.dripTime, config.dripRate);

                // The seams' drops are shared out by how much of the cut each piece carries.
                int drops = Mathf.RoundToInt(config.seamDrops * broken * piece.Torn.Length / (float)torn);
                for (int i = 0; i < drops; i++)
                {
                    VoxelCarcass.TornFace face = piece.Torn[Random.Range(0, piece.Torn.Length)];
                    VoxelSpray.Burst(face.Point, face.Normal, 1, 2f, 5f, 0.35f, blood, config.blood.dropSize, velocity);
                }
            }

            Vector3 wound = body.ClosestPoint(hit.Origin);
            Vector3 away = wound - hit.Origin;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.up;
            VoxelSpray.Burst(wound, away + Vector3.up * 0.5f, config.hitDrops, 2f, 5f, 0.35f, blood, config.blood.dropSize);

            return pieces;
        }
    }
}
```

```csharp
// Assets/Game/Scripts/Features/Character/Common/Gibs/VoxelGibsOnDeathBehaviour.cs
using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// On death the body becomes voxel flesh (VoxelGibs), thrown from the killing hit. The killer
    /// is remembered from the last damage event (see GibRules.ReadHit).
    /// </summary>
    public sealed class VoxelGibsOnDeathBehaviour : IEntityInit, IEntityDispose
    {
        private readonly VoxelGibsRig _rig;
        private readonly VoxelGibsConfig _config;

        private IEvent<TakeDamageArgs> _damageTakenEvent;
        private IEvent<TakeDamageArgs> _deathTakenEvent;
        private TakeDamageArgs _lastHit;

        public VoxelGibsOnDeathBehaviour(VoxelGibsRig rig, VoxelGibsConfig config)
        {
            _rig = rig;
            _config = config;
        }

        public void Init(IEntity entity)
        {
            _damageTakenEvent = entity.GetDamageTakenEvent();
            _deathTakenEvent = entity.GetDeathTakenEvent();
            _damageTakenEvent.Subscribe(this.OnDamageTaken);
            _deathTakenEvent.Subscribe(this.OnDeath);
        }

        public void Dispose(IEntity entity)
        {
            _damageTakenEvent.Unsubscribe(this.OnDamageTaken);
            _deathTakenEvent.Unsubscribe(this.OnDeath);
        }

        private void OnDamageTaken(TakeDamageArgs args) => _lastHit = args;

        private void OnDeath(TakeDamageArgs _) =>
            VoxelGibs.Burst(_rig, _config, GibRules.ReadHit(_lastHit, _rig.Skeleton.position));
    }
}
```

```csharp
// Assets/Game/Scripts/Features/Character/Common/Gibs/CharacterVoxelGibsInstaller.cs
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Makes the character die into voxel flesh. Put on Regax.prefab by Tools/Regax/Use Voxel Death
    /// (RegaxVoxelDeathBuilder), in place of CharacterGibsInstaller.
    /// </summary>
    public sealed class CharacterVoxelGibsInstaller : MonoEntityInstaller
    {
        [SerializeField] private SkinnedMeshRenderer _skin;

        [Tooltip("The animated hierarchy holding the bones the parts name.")]
        [SerializeField] private Transform _skeleton;

        [Tooltip("What disappears when the flesh takes the body's place.")]
        [SerializeField] private Renderer[] _hide;

        [SerializeField] private VoxelGibsConfig _config = new();

        public override void Install(IEntity entity)
        {
            if (_skin == null || _skeleton == null)
                return;

            entity.AddBehaviour(new VoxelGibsOnDeathBehaviour(VoxelGibsRig.Resolve(_skin, _skeleton, _hide, _config), _config));
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

PlayMode: `Game.Voxels.Tests.VoxelGibsPlayModeTests` (сборка `Game.Voxels.SceneTests`).
Ожидание: PASS. Если `Flesh_SettlesAndStays` не дожидается засыпания — сначала проверить в логе,
что куски упали на пол (а не сквозь), а не увеличивать ожидание.

- [ ] **Step 5: Без коммита.**

---

### Task 7: Кровь при попадании — `VoxelBloodBehaviour`

**Files:**
- Create: `Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodConfig.cs`
- Create: `Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodBehaviour.cs`
- Create: `Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodInstaller.cs`
- Test: `Assets/Game/Voxels/Tests/Scene/VoxelBloodPlayModeTests.cs`

**Interfaces:**
- Consumes: `BloodLook` (Task 6), `GibRules.TryGetPosition` (Task 5), `VoxelSpray.Burst` (Task 4).
- Produces: `VoxelBloodConfig { Vector2Int bulletDrops = (6, 12); Vector2Int meleeDrops = (10, 16);
  int blastDrops = 20; float minSpeed = 1.5f; float maxSpeed = 3.5f; float spread = 0.4f; BloodLook blood; }`;
  `VoxelBloodBehaviour(Collider body, Transform chest, VoxelBloodConfig config)`;
  `VoxelBloodInstaller` — сериализуемые `_body`, `_chest`, `_config`.

- [ ] **Step 1: Write the failing test**

```csharp
// Assets/Game/Voxels/Tests/Scene/VoxelBloodPlayModeTests.cs
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelBloodPlayModeTests
    {
        private readonly List<GameObject> _created = new();
        private IEntity _character;

        [SetUp]
        public void SetUp()
        {
            var body = new GameObject("Body");
            _created.Add(body);
            body.transform.position = new Vector3(0f, 1f, 0f);
            CapsuleCollider capsule = body.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;

            var chest = new GameObject("Chest");
            _created.Add(chest);
            chest.transform.position = new Vector3(0f, 1.4f, 0f);
            Physics.SyncTransforms();

            _character = new Entity();
            _character.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            new VoxelBloodBehaviour(capsule, chest.transform, new VoxelBloodConfig()).Init(_character);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                if (go != null)
                    Object.Destroy(go);
            _created.Clear();
        }

        private IEntity Source(Vector3 at)
        {
            var go = new GameObject("Source");
            _created.Add(go);
            go.transform.position = at;
            IEntity source = new Entity();
            source.AddTransform(go.transform);
            source.AddDamage(new ReactiveInt(10));
            return source;
        }

        private static int Drops() => VoxelWorld.Instance.Debris.Count + VoxelWorld.Instance.Debris.LyingCount;

        private int SprayFrom(IEntity source)
        {
            int before = Drops();
            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(source));
            return Drops() - before;
        }

        [Test]
        public void ABullet_Sprays6To12Drops()
        {
            IEntity bullet = Source(new Vector3(0f, 1f, -3f));
            bullet.AddRangeWeaponTag();
            Assert.That(SprayFrom(bullet), Is.InRange(6, 12));
        }

        [Test]
        public void AClaw_Sprays10To16Drops()
        {
            IEntity claw = Source(new Vector3(0f, 1f, -1f));
            claw.AddMeleeWeaponTag();
            Assert.That(SprayFrom(claw), Is.InRange(10, 16));
        }

        [Test]
        public void ABlast_Sprays20Drops()
        {
            IEntity shell = Source(new Vector3(0f, 0f, -2f));
            shell.AddExplosionRadius(4f);
            Assert.AreEqual(20, SprayFrom(shell));
        }

        [Test]
        public void NoSource_NoBlood()
        {
            int before = Drops();
            _character.GetDamageTakenEvent().Invoke(new TakeDamageArgs(null));
            Assert.AreEqual(before, Drops());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Компиляция. Ожидание: нет `VoxelBloodBehaviour`, `VoxelBloodConfig`.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodConfig.cs
using System;
using UnityEngine;

namespace Game
{
    /// <summary>How much a hit on a living body bleeds, in voxel drops.</summary>
    [Serializable]
    public sealed class VoxelBloodConfig
    {
        [Tooltip("Drops from a bullet, min–max.")]
        public Vector2Int bulletDrops = new(6, 12);

        [Tooltip("Drops from a melee hit, min–max; they come out of the chest.")]
        public Vector2Int meleeDrops = new(10, 16);

        [Tooltip("Drops from a blast that hurts but does not kill.")]
        public int blastDrops = 20;

        public float minSpeed = 1.5f;
        public float maxSpeed = 3.5f;

        [Range(0f, 1f)] public float spread = 0.4f;

        public BloodLook blood = new();
    }
}
```

```csharp
// Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodBehaviour.cs
using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A living body bleeds voxel drops where it is hit: out of the point of its collider nearest
    /// the bullet or the blast, along the surface's outward normal; a claw's hit bleeds from the
    /// chest. The drops land as tiles and lie all game. Takes the place of TakeDamageBloodBehaviour
    /// (particle blood) on Regax.
    /// </summary>
    public sealed class VoxelBloodBehaviour : IEntityInit, IEntityDispose
    {
        private readonly Collider _body;
        private readonly Transform _chest;
        private readonly VoxelBloodConfig _config;
        private Color32[] _colors;
        private IEvent<TakeDamageArgs> _damageTakenEvent;

        public VoxelBloodBehaviour(Collider body, Transform chest, VoxelBloodConfig config)
        {
            _body = body;
            _chest = chest;
            _config = config;
        }

        public void Init(IEntity entity)
        {
            _colors = _config.blood.ToVoxels();
            _damageTakenEvent = entity.GetDamageTakenEvent();
            _damageTakenEvent.Subscribe(this.OnDamageTaken);
        }

        public void Dispose(IEntity entity) => _damageTakenEvent.Unsubscribe(this.OnDamageTaken);

        private void OnDamageTaken(TakeDamageArgs args)
        {
            IEntity source = args?.source;
            if (source == null || _body == null || !GibRules.TryGetPosition(source, out Vector3 from))
                return;

            int count;
            Vector3 point;
            if (ExplosionUseCase.IsExplosive(source))
            {
                count = _config.blastDrops;
                point = _body.ClosestPoint(from);
            }
            else if (source.HasMeleeWeaponTag() && _chest != null)
            {
                count = Random.Range(_config.meleeDrops.x, _config.meleeDrops.y + 1);
                point = _chest.position;
            }
            else
            {
                count = Random.Range(_config.bulletDrops.x, _config.bulletDrops.y + 1);
                point = _body.ClosestPoint(from);
            }

            Vector3 outward = point - _body.bounds.center;
            if (outward.sqrMagnitude < 1e-4f)
                outward = point - from;

            VoxelSpray.Burst(point, outward, count, _config.minSpeed, _config.maxSpeed, _config.spread, _colors,
                _config.blood.dropSize);
        }
    }
}
```

```csharp
// Assets/Game/Scripts/Features/Health/Vfx/VoxelBloodInstaller.cs
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>Voxel blood on hits. Put on Regax.prefab's &lt;VFX&gt; by Tools/Regax/Use Voxel Death.</summary>
    public sealed class VoxelBloodInstaller : MonoEntityInstaller
    {
        [Tooltip("The body's collider: drops come out of its surface nearest the hit.")]
        [SerializeField] private Collider _body;

        [Tooltip("Where a melee hit bleeds from.")]
        [SerializeField] private Transform _chest;

        [SerializeField] private VoxelBloodConfig _config = new();

        public override void Install(IEntity entity) =>
            entity.AddBehaviour(new VoxelBloodBehaviour(_body, _chest, _config));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

PlayMode: `Game.Voxels.Tests.VoxelBloodPlayModeTests`. Ожидание: PASS.

- [ ] **Step 5: Без коммита.**

---

### Task 8: Префаб — **Tools/Regax/Use Voxel Death**

**Files:**
- Create: `Assets/Game/Scripts/Editor/RegaxVoxelDeathBuilder.cs`
- Modify: `Assets/Game/Scripts/Editor/RegaxGibsBuilder.cs` — `private static Transform FindDeep` → `internal static`
- Modify: `Assets/Game/Scripts/Editor/RegaxPrefabBuilder.cs` — в конце `BuildPrefab`
- Изменятся меню: `Assets/Game/Prefabs/Characters/Regax.prefab`, `Assets/3rdParty/Rapax/FBX Files/SK_Rapax.fbx.meta`

**Interfaces:**
- Consumes: `CharacterVoxelGibsInstaller` (`_skin`, `_skeleton`, `_hide`), `VoxelBloodInstaller` (`_body`, `_chest`), `CharacterGibsInstaller`, `DeathBloodInstaller`, `CharacterVfxInstaller` (`_bulletBlood`, `_meleeBlood`), `MonoEntity.sceneInstallers`.
- Produces: `public static void RegaxVoxelDeathBuilder.Apply()` (меню **Tools/Regax/Use Voxel Death**).

- [ ] **Step 1: Проверка до правки (вместо падающего теста)**

```bash
grep -c "CharacterGibsInstaller\|DeathBloodInstaller" Assets/Game/Prefabs/Characters/Regax.prefab
```

Ожидание: скрипты по GUID, поэтому имена не найдутся — вместо этого взять GUID из
`Assets/Game/Scripts/Features/Character/Common/Gibs/CharacterGibsInstaller.cs.meta` и
`Assets/Game/Scripts/Features/Health/Vfx/DeathBloodInstaller.cs.meta` и посчитать их в префабе:
оба > 0. Посмотреть `git diff --stat Assets/Game/Prefabs/Characters/Regax.prefab`: если там
чужие несохранённые правки — они останутся (меню грузит и сохраняет префаб целиком), но сказать
об этом пользователю.

- [ ] **Step 2: Write the implementation**

```csharp
// Assets/Game/Scripts/Editor/RegaxVoxelDeathBuilder.cs
using Atomic.Entities;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Regax/Use Voxel Death: Regax dies into voxel flesh and bleeds voxel blood in place of
    /// the Maya gibs and the pack's blood (Docs/superpowers/specs/2026-11-16-regax-voxel-death.md).
    /// Tools/Regax/Build calls it last, so a rebuild from the zombie keeps the voxel death.
    /// </summary>
    public static class RegaxVoxelDeathBuilder
    {
        private const string LOG = "[REGAX VOXEL DEATH]";
        private const string REGAX_PREFAB = "Assets/Game/Prefabs/Characters/Regax.prefab";
        private const string MODEL = "Assets/3rdParty/Rapax/FBX Files/SK_Rapax.fbx";
        private const string CHEST_BONE = "Rapax_ Spine2";

        [MenuItem("Tools/Regax/Use Voxel Death")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"{LOG} leave Play first: the prefab and the model's import change");
                return;
            }

            MakeModelReadable();
            WireRegax();
        }

        /// <summary>BakeMesh skins on the CPU: a build keeps a mesh's vertices only when it is readable.</summary>
        private static void MakeModelReadable()
        {
            if (AssetImporter.GetAtPath(MODEL) is not ModelImporter importer)
            {
                Debug.LogError($"{LOG} no model importer at {MODEL}");
                return;
            }

            if (importer.isReadable)
                return;

            importer.isReadable = true;
            importer.SaveAndReimport();
            Debug.Log($"{LOG} {MODEL}: Read/Write on");
        }

        private static void WireRegax()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(REGAX_PREFAB);
            try
            {
                Transform skeleton = root.transform.Find("<Skeleton>");
                Transform skinObject = skeleton != null ? skeleton.Find("<Skin>") : null;
                SkinnedMeshRenderer skin = skinObject != null ? skinObject.GetComponent<SkinnedMeshRenderer>() : null;
                Transform colliderObject = root.transform.Find("<Collider>");
                Collider body = colliderObject != null ? colliderObject.GetComponent<Collider>() : null;
                Transform vfx = root.transform.Find("<VFX>");
                Transform chest = skeleton != null ? RegaxGibsBuilder.FindDeep(skeleton, CHEST_BONE) : null;
                MonoEntity entity = root.GetComponent<MonoEntity>();

                if (skin == null || body == null || vfx == null || chest == null || entity == null)
                {
                    Debug.LogError($"{LOG} {REGAX_PREFAB} lacks <Skeleton>/<Skin>, <Collider>, <VFX>, {CHEST_BONE} or MonoEntity");
                    return;
                }

                // The Maya gibs and the pack's death blood come off.
                foreach (CharacterGibsInstaller old in root.GetComponentsInChildren<CharacterGibsInstaller>(true))
                    Remove(entity, old);
                foreach (DeathBloodInstaller old in root.GetComponentsInChildren<DeathBloodInstaller>(true))
                    Remove(entity, old);

                // The pack's hit blood: the installer is shared with the zombies, so only its effects go.
                CharacterVfxInstaller effects = root.GetComponentInChildren<CharacterVfxInstaller>(true);
                if (effects != null)
                {
                    var serialized = new SerializedObject(effects);
                    serialized.FindProperty("_bulletBlood").objectReferenceValue = null;
                    serialized.FindProperty("_meleeBlood").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                CharacterVoxelGibsInstaller gibs = root.GetComponent<CharacterVoxelGibsInstaller>();
                if (gibs == null)
                    gibs = root.AddComponent<CharacterVoxelGibsInstaller>();
                var gibsSettings = new SerializedObject(gibs);
                gibsSettings.FindProperty("_skin").objectReferenceValue = skin;
                gibsSettings.FindProperty("_skeleton").objectReferenceValue = skeleton;
                SerializedProperty hide = gibsSettings.FindProperty("_hide");
                hide.arraySize = 1;
                hide.GetArrayElementAtIndex(0).objectReferenceValue = skin;
                gibsSettings.ApplyModifiedPropertiesWithoutUndo();

                VoxelBloodInstaller blood = vfx.GetComponent<VoxelBloodInstaller>();
                if (blood == null)
                    blood = vfx.gameObject.AddComponent<VoxelBloodInstaller>();
                var bloodSettings = new SerializedObject(blood);
                bloodSettings.FindProperty("_body").objectReferenceValue = body;
                bloodSettings.FindProperty("_chest").objectReferenceValue = chest;
                bloodSettings.ApplyModifiedPropertiesWithoutUndo();

                List(entity, gibs);
                List(entity, blood);

                PrefabUtility.SaveAsPrefabAsset(root, REGAX_PREFAB);
                Debug.Log($"{LOG} {REGAX_PREFAB} now dies into voxels and bleeds voxel blood");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Remove(MonoEntity entity, Component installer)
        {
            var serialized = new SerializedObject(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue != installer)
                    continue;
                element.objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(installer);
        }

        private static void List(MonoEntity entity, Component installer)
        {
            var serialized = new SerializedObject(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer)
                    return;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
```

`RegaxPrefabBuilder.BuildPrefab`, после `RegaxGibsBuilder.WireExistingGibs();`:

```csharp
            // …and trades them for the voxel death (Docs/superpowers/specs/2026-11-16-regax-voxel-death.md).
            RegaxVoxelDeathBuilder.Apply();
```

- [ ] **Step 3: Компиляция и запуск меню**

Компиляция (вне Play). Затем `editor_status` → не Play → `editor_invoke_menu`
`Tools/Regax/Use Voxel Death` → `console_get`: строка `[REGAX VOXEL DEATH] … now dies into voxels`,
ошибок нет.

- [ ] **Step 4: Проверка результата**

```bash
grep -n "isReadable" "Assets/3rdParty/Rapax/FBX Files/SK_Rapax.fbx.meta" | head -1
```

Ожидание: `isReadable: 1`. В `Regax.prefab` GUID `CharacterGibsInstaller` и `DeathBloodInstaller`
не встречаются, GUID `CharacterVoxelGibsInstaller` и `VoxelBloodInstaller` (из их `.meta`) — по
одному разу, и оба id компонентов есть в `sceneInstallers`. У `CharacterVfxInstaller`
`_bulletBlood` и `_meleeBlood` — `{fileID: 0}`.

- [ ] **Step 5: Без коммита** (префаб и мета — тоже код/ассеты, остаются в рабочем дереве).

---

### Task 9: Проверка в Play на Playground, итог в спеке, коммит спеки

**Files:**
- Modify: `Docs/superpowers/specs/2026-11-16-regax-voxel-death.md` — раздел «Результат»
- Commit: только `Docs/superpowers/specs/2026-11-16-regax-voxel-death.md` и
  `Docs/superpowers/plans/2026-11-16-regax-voxel-death.md`

- [ ] **Step 1: Подготовка редактора**

`editor_status` — не в Play (если в Play — спросить пользователя). `scene_list_open` — открыт ли
Playground и не грязный ли. Если открыта другая сцена с несохранёнными правками — не переключать,
спросить пользователя. Если Playground не открыт и грязных сцен нет — `scene_open`
`Assets/Game/Scenes/Playground.unity`.

- [ ] **Step 2: Убийство «пулей»**

`editor_playmode_start`, подождать ~3 с. В `script_eval` (генерируемые расширения EntityAPI
Roslyn не видит — вызывать их через reflection, как в `voxel-sandbox-benchmark`): найти ближайший
к игроку `MonoEntity` с именем `Regax`; создать `Entity` с `Transform` (объект в 5 м от Regax),
`Damage = ReactiveInt(100)` и `RangeWeaponTag`; вызвать
`DamageUseCase.TakeDamage(regax, new TakeDamageArgs(source))`. Затем `capture_game_view`.
Ожидание: на месте Regax — 2–7 воксельных кусков (тёмно-зелёная кожа, красное мясо на разрывах),
зелёные капли и плашки, без эффекта крови пакета. `console_get` — без ошибок.

- [ ] **Step 3: Убийство «снарядом»**

Тот же путь, но у источника `ExplosionRadius = 5`, `Damage = 150`, источник в 2 м от Regax; сразу
после `TakeDamage` вызвать `VoxelDamage.Blast(точка источника, 5 × 0.32, 9)` — так же, как это
делает `ExplosionUseCase.Explode` после `DamageInRadius`. Ожидание: куски разлетаются сильнее,
у них выгрызен шар, вокруг комья мяса. Скриншот.

- [ ] **Step 4: Попадание по живому**

Тот же источник-пуля с `Damage = 10` по живому Regax: 6–12 капель (`Debris.Count + LyingCount`
выросли на столько), Regax жив.

- [ ] **Step 5: 10 смертей и цифры**

Убить по очереди 10 Regax (или одного, перезапуская, если их меньше). Замерить:
- цену самой смерти: смерть (вокселизация, разбиение, капли) идёт синхронно внутри
  `DamageUseCase.TakeDamage`, поэтому `Stopwatch` вокруг этого вызова в том же `script_eval` и
  есть её время на главном потоке — цель ≤ 5 мс (первый вызов после компиляции не в счёт: Burst
  ещё не прогрет);
- время кадра со смертью (`ProfilerRecorder` «Main Thread»; создавать рекордеры через несколько
  секунд после старта Play — см. память `voxel-sandbox-benchmark`): сюда входит и построение мешей
  кусков в `VoxelWorld.LateUpdate`;
- через 15 с после последней смерти: число `VoxelBody` с `Flesh` в имени, сколько из них
  `IsLoose`, `Debris.LyingCount`, средний кадр за 5 с.
`editor_playmode_stop`.

- [ ] **Step 6: Итог в спеке**

Добавить в спеку раздел «Результат»: что проверено (пуля, снаряд, попадание, 10 смертей), цифры
из шага 5, и уточнения из этого плана («Уточнения к спеке»: шар взрыва делает `ExplosionUseCase`,
toughness 0.6, островки ≤ 12 — комья, капли ниже −60 м удаляются). Если какой-то тест или шаг не
удалось выполнить (грязная сцена, чужой Play) — так и написать.

- [ ] **Step 7: Коммит только документов**

```bash
git add -- Docs/superpowers/specs/2026-11-16-regax-voxel-death.md Docs/superpowers/plans/2026-11-16-regax-voxel-death.md
git commit -m "Spec: Regax voxel death, result" -- Docs/superpowers/specs/2026-11-16-regax-voxel-death.md Docs/superpowers/plans/2026-11-16-regax-voxel-death.md
```

В сообщение коммита добавить строку соавторства из системной инструкции сессии. Код и префаб не
коммитить.

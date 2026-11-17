# Вокселизатор «модель → воксельный префаб»: план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Дата:** 2026-11-17
**Спецификация:** `Docs/superpowers/specs/2026-11-17-voxelizer-rules.md`
**Ветка:** `claude/voxelizer-e3c9e1` (рабочая копия `.claude/worktrees/voxelizer-e3c9e1`), `game/develop` с Unity 6.3 уже влит.

**Goal:** Окно в редакторе. Пользователь кладёт модель (техника или здание), получает превью в стиле нашего танка и запекает воксельный префаб `VX_<Имя>` с `VoxelBody` и разрушением.

**Architecture:** Вокселизация идёт в редакторе, Burst-задачами, по частям модели. Шаги:
1. треугольники части в её пространстве;
2. выпрямление плоскостей у осей;
3. точки поверхности с цветом текстуры;
4. цвет ячейки по большинству;
5. заливка снаружи;
6. срез деталей мельче 2 вокселей;
7. примитивы по пометкам;
8. палитра на модель (k-means);
9. фильтр большинства.

Результат пишется в `VoxelModelAsset` (рантайм, `Game.Voxels`). Компонент `VoxelModel` на префабе на старте отдаёт данные в `VoxelBody`.

**Tech Stack:** Unity 6000.3.28f1, Burst, Collections, Mathematics, NUnit (Unity Test Framework), IMGUI-окно редактора, `PreviewRenderUtility`.

## Global Constraints

- Unity **6000.3.28f1**; свой редактор только на своей рабочей копии (`unity status`: `projectPath` = рабочая копия).
- Код и комментарии — на английском, документы — на русском.
- Воксель: техника **0.03 м**, здания **0.1 м**.
- Разрушение:
  - техника — `Toughness` **0.45**, `Density` **7800**, `Anchor` `Largest`, `Colliders` false;
  - здание — `Toughness` **1**, `Density` **1800**, `Anchor` `Ground`, `Colliders` true, `ColliderCell` **2**.
- Палитра своя на модель: K по умолчанию **12** (допустимо 4–32), без случайных оттенков. Внутренность — цвет поверхности × **0.85**. Альфа палитры **255** (сухо).
- Выпрямление «авто»:
  - треугольник с нормалью не дальше **10°** от оси ставится на границу вокселей;
  - детали тоньше **2** вокселей срезаются.
- Шаг точек по треугольнику — не больше **0.35** вокселя. Сетка — не больше **512** по оси.
- Новые файлы рантайма — **новыми файлами** в `Assets/Game/Voxels/Runtime`. Существующие классы (`VoxelBody`, `VoxelWorld`, `VoxelizedModel`, `MeshVoxelizer`…) **не менять**: там работает команда ядра. Если без этого не обойтись — сначала написать тимлиду.
- Пакеты и `ProjectSettings/` не трогать.
- Один верхнеуровневый тип на файл. Исключение — `VoxelizerJobs.cs` и `VoxelizerTypes.cs`.
- Пространства имён: рантайм — `Game.Voxels`, редактор — `Game.Voxels.EditorTools`, тесты — `Game.Voxels.Tests`.
- Новые ассеты: воксельный префаб `VX_<Имя>.prefab`, данные `VX_<Имя>_Data.asset`, настройки `<Имя>VoxelizerConfig.asset`.
- В рантайм-коде нет LINQ и аллокаций в тиках. У `VoxelModel` тиков нет, только `Start`.
- Правила общего редактора:
  - не делать Refresh и не перекомпилировать во время Play;
  - тесты не запускать, пока открыта несохранённая сцена;
  - чужие сцены не сохранять.
- Коммитить только свои пути. Сообщение коммита заканчивается строкой `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Не пушить.

## Карта файлов

| Файл | Сборка | Отвечает за |
|---|---|---|
| `Assets/Game/Voxels/Runtime/VoxelModelAsset.cs` | `Game.Voxels` | Запечённые данные модели: части (путь, размер, начало, сетка в RLE), палитра, настройки разрушения; кодек RLE |
| `Assets/Game/Voxels/Runtime/VoxelModel.cs` | `Game.Voxels` | Компонент префаба: на `Start` создаёт `VoxelBody` на каждую часть из `VoxelModelAsset` |
| `Assets/Game/Voxels/Runtime/VoxFile.cs` | `Game.Voxels` | Чтение и запись MagicaVoxel `.vox` (одна модель: SIZE, XYZI, RGBA) |
| `Assets/Game/Voxels/Editor/Game.Voxels.Editor.asmdef` | — | + ссылки `Unity.Burst`, `Unity.Collections` |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerConfig.cs` | `Game.Voxels.Editor` | ScriptableObject с настройками вокселизации модели: модель, класс, длина, K, пометки частей |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerTypes.cs` | `Game.Voxels.Editor` | Структуры: `VoxelizerPartGrid`, `VoxelizerResult`, `VoxelizerTriangles` |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerJobs.cs` | `Game.Voxels.Editor` | Burst-задачи: `SnapPlanesJob`, `SampleSurfaceJob`, `ResolveCellsJob`, `FloodOutsideJob`, `ShadeInteriorJob`, `OpenJob`, `MajorityJob` |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerSource.cs` | `Game.Voxels.Editor` | Сбор треугольников, UV, цвета материала и пикселей текстуры части в её пространстве |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerPrimitives.cs` | `Game.Voxels.Editor` | Пометки «коробка» и «цилиндр»: заполнение по габаритам |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerPalette.cs` | `Game.Voxels.Editor` | k-means до K цветов, сетки цветов → байты |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizeUseCase.cs` | `Game.Voxels.Editor` | Весь конвейер: конфиг → `VoxelizerResult` |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelModelBakeUseCase.cs` | `Game.Voxels.Editor` | `VoxelizerResult` → `VX_<Имя>_Data.asset` + `VX_<Имя>.prefab` |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerPreview.cs` | `Game.Voxels.Editor` | Меш по сетке (грани между пустым и полным) и рендер через `PreviewRenderUtility` |
| `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerWindow.cs` | `Game.Voxels.Editor` | Окно `Tools/Voxels/Voxelizer` |
| `Assets/Game/Voxels/Tests/Voxelizer/Game.Voxels.VoxelizerTests.asmdef` | новая, Editor | EditMode-тесты вокселизатора |
| `Assets/Game/Voxels/Tests/Voxelizer/*.cs` | `Game.Voxels.VoxelizerTests` | Тесты по задачам |
| `Assets/Game/Voxels/Tests/VoxelModelTests.cs` | `Game.Voxels.Tests` (PlayMode) | Запечённый префаб строит `VoxelBody` |

Новая сборка `Game.Voxels.VoxelizerTests` попадает в таблицу `Docs/Rules/Project.md` §2. Этот файл ведёт команда производительности: в задаче 1 строку просим через тимлида, сами файл не правим.

---

### Task 0: Редактор на 6.3 и сборки

**Files:**
- Modify: `Assets/Game/Voxels/Editor/Game.Voxels.Editor.asmdef`
- Create: `Assets/Game/Voxels/Tests/Voxelizer/Game.Voxels.VoxelizerTests.asmdef`
- Create: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizerSmokeTests.cs`

**Interfaces:**
- Produces: сборка `Game.Voxels.VoxelizerTests` (Editor, ссылается на `Game.Voxels`, `Game.Voxels.Editor`, Burst, Collections, Mathematics, TestRunner).

- [ ] **Step 1: Library под 6.3.** Нужен `Library` того же Unity, без открытого редактора на источнике.
  - Найти копию, где редактор 6000.3.28f1 закрыт: `unity status`; проверить, держит ли кто-то `Temp/UnityLockfile`.
  - Скопировать её `Library` в свою копию через `robocopy <src>\Library <worktree>\Library /E /MT:16 /XD ScriptAssemblies Bee`.
  - Если такой копии нет, открыть без `Library`: импорт займёт дольше.
- [ ] **Step 2: Открыть свой редактор** (в фоне, до 10 минут):

```bash
unity open "C:/!!!Work/CodeCraft_HomeWork_ATOMIC/.claude/worktrees/voxelizer-e3c9e1" --until-ready --editor-version 6000.3.28f1
```

Ожидание: `unity status` показывает рабочую копию в состоянии `ready`, версия 6000.3.28f1. Порт записать: ниже он `<PORT>`.

- [ ] **Step 3: Ссылки редакторской сборки.** В `Game.Voxels.Editor.asmdef` массив `references` становится таким:

```json
"references": [
    "Game.Voxels",
    "Game.Voxels.Demo",
    "Unity.Burst",
    "Unity.Collections",
    "Unity.Mathematics"
],
```

- [ ] **Step 4: Тестовая сборка** `Assets/Game/Voxels/Tests/Voxelizer/Game.Voxels.VoxelizerTests.asmdef`:

```json
{
    "name": "Game.Voxels.VoxelizerTests",
    "rootNamespace": "Game.Voxels.Tests",
    "references": [
        "Game.Voxels",
        "Game.Voxels.Editor",
        "Unity.Burst",
        "Unity.Collections",
        "Unity.Mathematics",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Папка `Tests/Voxelizer` лежит внутри `Tests`, у которой своя сборка `Game.Voxels.Tests`. Вложенный asmdef забирает файлы своей папки себе — так и нужно.

- [ ] **Step 5: Дымовой тест** `VoxelizerSmokeTests.cs`:

```csharp
using NUnit.Framework;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerSmokeTests
    {
        [Test]
        public void TheVoxelizerTestAssemblyCompilesAndRuns() => Assert.Pass();
    }
}
```

- [ ] **Step 6: Перекомпиляция и тест.** В одном eval проверить, что не Play, и только потом обновить ассеты:

```bash
unity cmd --port <PORT> script eval --code "if (UnityEditor.EditorApplication.isPlaying) return \"abort: playing\"; UnityEditor.AssetDatabase.Refresh(); return \"ok\";"
unity test "C:/!!!Work/CodeCraft_HomeWork_ATOMIC/.claude/worktrees/voxelizer-e3c9e1" --mode EditMode --filter Game.Voxels.Tests.VoxelizerSmokeTests -o Logs/voxelizer-tests.xml
```

Ожидание: 1 тест, PASS, ошибок компиляции в консоли нет.

- [ ] **Step 7: Commit**

```bash
git add Assets/Game/Voxels/Editor/Game.Voxels.Editor.asmdef Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: editor assembly takes Burst and Collections, EditMode test assembly

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 8:** Написать тимлиду: новая сборка `Game.Voxels.VoxelizerTests` (Editor, EditMode-тесты вокселизатора) для таблицы `Docs/Rules/Project.md` §2.

---

### Task 1: `VoxelModelAsset` — запечённые данные и RLE

**Files:**
- Create: `Assets/Game/Voxels/Runtime/VoxelModelAsset.cs`
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelModelAssetTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class VoxelModelAsset : ScriptableObject`. Поля:
    - `public Part[] parts`;
    - `public Color32[] palette` (256);
    - `public float voxelSize`, `public float toughness`, `public float density`;
    - `public VoxelAnchor anchor`;
    - `public bool colliders`;
    - `public int colliderCell`.
  - Вложенный `[Serializable] public sealed class Part`:
    - `public string path` (путь части от корня модели, `""` — корень);
    - `public Vector3Int size`;
    - `public Vector3 origin`;
    - `public byte[] runs`.
  - `public static byte[] Encode(NativeArray<byte> voxels)`.
  - `public static NativeArray<byte> Decode(byte[] runs, int length, Allocator allocator)`.
  - `public VoxelBodySettings Settings(Vector3 origin, int layer)`.

- [ ] **Step 1: Failing test**

```csharp
using Game.Voxels;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelModelAssetTests
    {
        [Test]
        public void Encode_ThenDecode_GivesTheSameVoxels()
        {
            var voxels = new NativeArray<byte>(1000, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++)
                voxels[i] = (byte)(i % 7 == 0 ? 0 : (i / 100) + 1);

            byte[] runs = VoxelModelAsset.Encode(voxels);
            NativeArray<byte> back = VoxelModelAsset.Decode(runs, voxels.Length, Allocator.Temp);

            Assert.AreEqual(voxels.ToArray(), back.ToArray());
            voxels.Dispose();
            back.Dispose();
        }

        [Test]
        public void Encode_AnEmptyGrid_IsTiny()
        {
            var voxels = new NativeArray<byte>(512 * 512, Allocator.Temp);
            Assert.Less(VoxelModelAsset.Encode(voxels).Length, 64);
            voxels.Dispose();
        }

        [Test]
        public void Settings_CarryTheClassAndThePartOrigin()
        {
            var asset = ScriptableObject.CreateInstance<VoxelModelAsset>();
            asset.voxelSize = 0.03f;
            asset.toughness = 0.45f;
            asset.density = 7800f;
            asset.anchor = VoxelAnchor.Largest;
            asset.colliders = false;

            VoxelBodySettings s = asset.Settings(new Vector3(1f, 2f, 3f), 5);

            Assert.AreEqual(0.03f, s.VoxelSize);
            Assert.AreEqual(0.45f, s.Toughness);
            Assert.AreEqual(7800f, s.Density);
            Assert.AreEqual(VoxelAnchor.Largest, s.Anchor);
            Assert.IsFalse(s.Colliders);
            Assert.AreEqual(5, s.Layer);
            Assert.AreEqual(2f, s.Origin.y);
            Object.DestroyImmediate(asset);
        }
    }
}
```

- [ ] **Step 2: Run, expect FAIL** (тип `VoxelModelAsset` не найден):

```bash
unity test "C:/!!!Work/CodeCraft_HomeWork_ATOMIC/.claude/worktrees/voxelizer-e3c9e1" --mode EditMode --filter Game.Voxels.Tests.VoxelModelAssetTests -o Logs/voxelizer-tests.xml
```

- [ ] **Step 3: Implementation** `VoxelModelAsset.cs`:

```csharp
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A model voxelized in the editor (the Voxelizer window): one grid per moving part, one palette
    /// for the whole model, and how it breaks. VoxelModel hands it to VoxelBody on start, so nothing
    /// is voxelized in the game.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Voxels/Voxel Model", fileName = "VX_Model_Data")]
    public sealed class VoxelModelAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            [Tooltip("The part's transform as a path from the model root; empty — the root itself.")]
            public string path = "";
            public Vector3Int size;
            [Tooltip("Part space position of the min corner of voxel (0, 0, 0), m.")]
            public Vector3 origin;
            [Tooltip("The grid, x fastest, as (count, value) byte pairs.")]
            public byte[] runs = Array.Empty<byte>();
        }

        public Part[] parts = Array.Empty<Part>();
        [Tooltip("256 colours; index 0 is empty. Alpha is dryness (255 — dry).")]
        public Color32[] palette = new Color32[256];
        public float voxelSize = 0.03f;
        [Range(0.1f, 1f)] public float toughness = 1f;
        public float density = 1800f;
        public VoxelAnchor anchor = VoxelAnchor.Largest;
        public bool colliders;
        [Tooltip("Edge of a collision cell in voxels; 0 or 1 — every voxel.")]
        public int colliderCell;

        public VoxelBodySettings Settings(Vector3 origin, int layer)
        {
            VoxelBodySettings settings = VoxelBodySettings.Default(voxelSize);
            settings.Origin = (float3)origin;
            settings.Anchor = anchor;
            settings.Colliders = colliders;
            settings.ColliderCell = colliderCell;
            settings.Layer = layer;
            settings.Toughness = toughness;
            settings.Density = density;
            return settings;
        }

        /// <summary>Runs of equal bytes as (count 1..255, value) pairs.</summary>
        public static byte[] Encode(NativeArray<byte> voxels)
        {
            var runs = new List<byte>(1024);
            int i = 0;
            while (i < voxels.Length)
            {
                byte value = voxels[i];
                int count = 1;
                while (i + count < voxels.Length && count < 255 && voxels[i + count] == value)
                    count++;
                runs.Add((byte)count);
                runs.Add(value);
                i += count;
            }

            return runs.ToArray();
        }

        public static NativeArray<byte> Decode(byte[] runs, int length, Allocator allocator)
        {
            var voxels = new NativeArray<byte>(length, allocator);
            int at = 0;
            for (int r = 0; r + 1 < runs.Length && at < length; r += 2)
            {
                int count = math.min(runs[r], length - at);
                byte value = runs[r + 1];
                for (int k = 0; k < count; k++)
                    voxels[at + k] = value;
                at += count;
            }

            return voxels;
        }
    }
}
```

- [ ] **Step 4: Run, expect PASS** (та же команда, 3 теста).
- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Voxels/Runtime/VoxelModelAsset.cs Assets/Game/Voxels/Runtime/VoxelModelAsset.cs.meta Assets/Game/Voxels/Tests/Voxelizer/VoxelModelAssetTests.cs*
git commit -m "Voxels: VoxelModelAsset holds a model voxelized in the editor, grids as byte runs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Точки поверхности и цвет ячейки

**Files:**
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerTypes.cs`
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerJobs.cs` (`SampleSurfaceJob`, `ResolveCellsJob`)
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerSource.cs`
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizerSurfaceTests.cs`, помощник `Assets/Game/Voxels/Tests/Voxelizer/TestMeshes.cs`

**Interfaces:**
- Produces:
  - `public sealed class VoxelizerTriangles : IDisposable` — данные треугольников части в её пространстве:
    - `NativeArray<float3> Corners` (3 на треугольник);
    - `NativeArray<float2> Uvs` (3 на треугольник);
    - `NativeArray<int> Material` (1 на треугольник);
    - `NativeArray<Color32> Tints` (1 на материал);
    - `NativeArray<Color32> Pixels` (текстуры подряд), `NativeArray<int4> Textures` (на материал: offset, width, height, has);
    - `float3 Min, Max`.
  - `public static VoxelizerTriangles VoxelizerSource.Collect(Transform part, IReadOnlyList<Renderer> renderers)`.
  - `public sealed class VoxelizerPartGrid : IDisposable`:
    - `int3 Size`, `float3 Origin`, `float VoxelSize`;
    - `NativeArray<Color32> Cells` — a=0 пусто, a=255 поверхность, a=128 внутренность;
    - `string Path`, `Transform Root`.
  - `public static VoxelizerPartGrid VoxelizerSurface.Sample(VoxelizerTriangles triangles, float voxelSize)` — в файле `VoxelizerSurface.cs`; его тоже добавить в Files.

- [ ] **Step 1: Помощник для тестов** `TestMeshes.cs` (тип `TestMeshes`, один на файл):

```csharp
using UnityEngine;

namespace Game.Voxels.Tests
{
    /// <summary>Meshes and textured objects built in code for the voxelizer tests.</summary>
    public static class TestMeshes
    {
        /// <summary>An axis-aligned box from min to max, 12 triangles, outward winding, UVs 0..1 per face.</summary>
        public static Mesh Box(Vector3 min, Vector3 max)
        {
            Vector3[] c =
            {
                new(min.x, min.y, min.z), new(max.x, min.y, min.z), new(max.x, max.y, min.z), new(min.x, max.y, min.z),
                new(min.x, min.y, max.z), new(max.x, min.y, max.z), new(max.x, max.y, max.z), new(min.x, max.y, max.z)
            };
            int[][] faces =
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 4, 7, 3 },
                new[] { 1, 2, 6, 5 }, new[] { 0, 1, 5, 4 }, new[] { 3, 7, 6, 2 }
            };
            var vertices = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            Vector2[] corner = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };
            for (int f = 0; f < 6; f++)
            {
                for (int k = 0; k < 4; k++)
                {
                    vertices[f * 4 + k] = c[faces[f][k]];
                    uvs[f * 4 + k] = corner[k];
                }

                int b = f * 4;
                int t = f * 6;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 2;
                triangles[t + 3] = b; triangles[t + 4] = b + 2; triangles[t + 5] = b + 3;
            }

            var mesh = new Mesh { vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>A child with this mesh and a Standard material of this colour, optionally textured.</summary>
        public static MeshRenderer Add(Transform parent, string name, Mesh mesh, Color color, Texture2D texture = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Standard")) { color = color, mainTexture = texture };
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>A texture whose left half is one colour and right half another.</summary>
        public static Texture2D TwoColours(Color left, Color right)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                texture.SetPixel(x, y, x < 4 ? left : right);
            texture.Apply();
            return texture;
        }
    }
}
```

Шейдер `Standard` в URP-проекте может не найтись. Тогда брать `Shader.Find("Universal Render Pipeline/Lit")`: `color` и `mainTexture` у него отображаются на `_BaseColor` и `_BaseMap`. `VoxelizerSource` читает оба варианта.

- [ ] **Step 2: Failing tests** `VoxelizerSurfaceTests.cs`:

```csharp
using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerSurfaceTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ABoxOfOneMetre_AtTenCentimetres_HasTheShellOfATenCube()
        {
            MeshRenderer r = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.gray);
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);

            int surface = 0;
            foreach (Color32 c in grid.Cells)
                if (c.a == 255) surface++;

            Assert.AreEqual(10 * 10 * 10 - 8 * 8 * 8, surface);
        }

        [Test]
        public void ATextureInTwoColours_GivesExactlyThoseTwoColours()
        {
            Texture2D texture = TestMeshes.TwoColours(Color.red, Color.blue);
            MeshRenderer r = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.white, texture);
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);

            // Color32 has no value equality: compare colours as rgb ints.
            var colours = new HashSet<int>();
            foreach (Color32 c in grid.Cells)
                if (c.a != 0) colours.Add(c.r << 16 | c.g << 8 | c.b);

            Assert.AreEqual(2, colours.Count);
            Object.DestroyImmediate(texture);
        }
    }
}
```

- [ ] **Step 3: Run, expect FAIL** (типы не найдены).
- [ ] **Step 4: `VoxelizerTypes.cs`**

```csharp
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>A part's triangles in its own space, with what colours them.</summary>
    public sealed class VoxelizerTriangles : IDisposable
    {
        public NativeArray<float3> Corners;   // 3 per triangle
        public NativeArray<float2> Uvs;       // 3 per triangle
        public NativeArray<int> Material;     // 1 per triangle
        public NativeArray<Color32> Tints;    // 1 per material, sRGB as authored
        public NativeArray<Color32> Pixels;   // every material's texture, one after another
        public NativeArray<int4> Textures;    // per material: offset into Pixels, width, height, 1 if textured
        public float3 Min;
        public float3 Max;

        public int Count => Corners.IsCreated ? Corners.Length / 3 : 0;

        public void Dispose()
        {
            if (Corners.IsCreated) Corners.Dispose();
            if (Uvs.IsCreated) Uvs.Dispose();
            if (Material.IsCreated) Material.Dispose();
            if (Tints.IsCreated) Tints.Dispose();
            if (Pixels.IsCreated) Pixels.Dispose();
            if (Textures.IsCreated) Textures.Dispose();
        }
    }

    /// <summary>
    /// One part on the voxel grid before the palette: a = 0 empty, 255 surface, 128 inside.
    /// Colours are sRGB as authored; the palette turns them into vertex colours.
    /// </summary>
    public sealed class VoxelizerPartGrid : IDisposable
    {
        public string Path = "";
        public Transform Root;
        public int3 Size;
        public float3 Origin;
        public float VoxelSize; // model units: the class voxel / Scale
        public NativeArray<Color32> Cells;

        public const byte SURFACE = 255;
        public const byte INSIDE = 128;

        public void Dispose()
        {
            if (Cells.IsCreated) Cells.Dispose();
        }
    }
}
```

- [ ] **Step 5: `VoxelizerSource.cs`.** Треугольники рендереров в пространстве части, цвет и пиксели материалов. Текстуру копировать через `RenderTexture`: так читается любая, даже без Read/Write.

```csharp
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>Collects what the voxelizer needs from a part's renderers, in the part's space.</summary>
    public static class VoxelizerSource
    {
        private static readonly string[] TextureNames = { "_BaseMap", "_MainTex", "_BaseColorMap" };
        private static readonly string[] ColorNames = { "_BaseColor", "_Color" };

        public static VoxelizerTriangles Collect(Transform part, IReadOnlyList<Renderer> renderers)
        {
            var corners = new List<float3>();
            var uvs = new List<float2>();
            var materialOf = new List<int>();
            var materials = new List<Material>();
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            Matrix4x4 toPart = part.worldToLocalMatrix;

            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = MeshOf(renderer, out Matrix4x4 meshToWorld);
                if (mesh == null)
                    continue;

                Matrix4x4 m = toPart * meshToWorld;
                Vector3[] vertices = mesh.vertices;
                Vector2[] meshUvs = mesh.uv;
                bool hasUv = meshUvs.Length == vertices.Length;
                Material[] shared = renderer.sharedMaterials;

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                        continue;
                    Material material = shared.Length > 0 ? shared[math.min(sub, shared.Length - 1)] : null;
                    int index = materials.IndexOf(material);
                    if (index < 0)
                    {
                        index = materials.Count;
                        materials.Add(material);
                    }

                    int[] indices = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            float3 p = m.MultiplyPoint3x4(vertices[indices[i + k]]);
                            corners.Add(p);
                            uvs.Add(hasUv ? (float2)meshUvs[indices[i + k]] : float2.zero);
                            min = math.min(min, p);
                            max = math.max(max, p);
                        }

                        materialOf.Add(index);
                    }
                }

                if (renderer is SkinnedMeshRenderer)
                    Object.DestroyImmediate(mesh);
            }

            var result = new VoxelizerTriangles
            {
                Corners = new NativeArray<float3>(corners.ToArray(), Allocator.Persistent),
                Uvs = new NativeArray<float2>(uvs.ToArray(), Allocator.Persistent),
                Material = new NativeArray<int>(materialOf.ToArray(), Allocator.Persistent),
                Tints = new NativeArray<Color32>(materials.Count, Allocator.Persistent),
                Textures = new NativeArray<int4>(materials.Count, Allocator.Persistent),
                Min = min,
                Max = max
            };

            var pixels = new List<Color32>();
            for (int i = 0; i < materials.Count; i++)
            {
                result.Tints[i] = TintOf(materials[i]);
                Color32[] texels = ReadTexture(TextureOf(materials[i]), out int width, out int height);
                result.Textures[i] = new int4(pixels.Count, width, height, texels != null ? 1 : 0);
                if (texels != null)
                    pixels.AddRange(texels);
            }

            result.Pixels = new NativeArray<Color32>(math.max(1, pixels.Count), Allocator.Persistent);
            for (int i = 0; i < pixels.Count; i++)
                result.Pixels[i] = pixels[i];
            return result;
        }

        private static Mesh MeshOf(Renderer renderer, out Matrix4x4 meshToWorld)
        {
            meshToWorld = renderer.transform.localToWorldMatrix;
            if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter))
                return filter.sharedMesh;
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                meshToWorld = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                return baked;
            }

            return null;
        }

        private static Color32 TintOf(Material material)
        {
            if (material == null)
                return new Color32(128, 128, 128, 255);
            foreach (string name in ColorNames)
                if (material.HasProperty(name))
                    return material.GetColor(name);
            return new Color32(255, 255, 255, 255);
        }

        private static Texture TextureOf(Material material)
        {
            if (material == null)
                return null;
            foreach (string name in TextureNames)
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                    return material.GetTexture(name);
            return null;
        }

        /// <summary>The texture's pixels, row by row from the bottom, as stored (sRGB).</summary>
        private static Color32[] ReadTexture(Texture texture, out int width, out int height)
        {
            width = height = 0;
            if (texture == null)
                return null;
            width = texture.width;
            height = texture.height;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }
    }
}
```

- [ ] **Step 6: Задачи в `VoxelizerJobs.cs`.** `SampleSurfaceJob` пишет ключ `cell << 36 | rgb24 << 12 | bin12`. `ResolveCellsJob` после сортировки выбирает в каждой ячейке корзину с наибольшим числом точек; цвет — среднее точек этой корзины.

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>
    /// Points every 0.35 voxel over each triangle, each with its colour (tint × texture), keyed so
    /// that a sort brings each cell's points together, grouped by colour bin (4 bits a channel).
    /// Key: cell (27 bits) | rgb (24 bits) | bin (12 bits).
    /// </summary>
    [BurstCompile]
    public struct SampleSurfaceJob : IJob
    {
        [ReadOnly] public NativeArray<float3> Corners;
        [ReadOnly] public NativeArray<float2> Uvs;
        [ReadOnly] public NativeArray<int> Material;
        [ReadOnly] public NativeArray<Color32> Tints;
        [ReadOnly] public NativeArray<Color32> Pixels;
        [ReadOnly] public NativeArray<int4> Textures;
        public float3 Origin;
        public float VoxelSize;
        public int3 Size;
        public NativeList<ulong> Keys;

        public void Execute()
        {
            float step = VoxelSize * 0.35f;
            for (int t = 0; t < Corners.Length / 3; t++)
            {
                float3 a = Corners[t * 3], b = Corners[t * 3 + 1], c = Corners[t * 3 + 2];
                float edge = math.max(math.distance(a, b), math.max(math.distance(b, c), math.distance(c, a)));
                int n = math.clamp((int)math.ceil(edge / step), 1, 4096);
                for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n - i; j++)
                {
                    float u = i / (float)n, v = j / (float)n, w = 1f - u - v;
                    float3 p = a * w + b * u + c * v;
                    int3 cell = (int3)math.floor((p - Origin) / VoxelSize);
                    if (math.any(cell < 0) || math.any(cell >= Size))
                        continue;
                    float2 uv = Uvs[t * 3] * w + Uvs[t * 3 + 1] * u + Uvs[t * 3 + 2] * v;
                    Color32 colour = Shade(Material[t], uv);
                    ulong index = (ulong)(cell.x + Size.x * (cell.y + Size.y * cell.z));
                    ulong rgb = ((ulong)colour.r << 16) | ((ulong)colour.g << 8) | colour.b;
                    ulong bin = ((ulong)(colour.r >> 4) << 8) | ((ulong)(colour.g >> 4) << 4) | (ulong)(colour.b >> 4);
                    Keys.Add((index << 36) | (rgb << 12) | bin);
                }
            }
        }

        private Color32 Shade(int material, float2 uv)
        {
            Color32 tint = Tints[material];
            int4 texture = Textures[material];
            if (texture.w == 0)
                return tint;
            int x = (int)math.floor(math.frac(uv.x) * texture.y);
            int y = (int)math.floor(math.frac(uv.y) * texture.z);
            Color32 texel = Pixels[texture.x + math.clamp(y, 0, texture.z - 1) * texture.y + math.clamp(x, 0, texture.y - 1)];
            return new Color32((byte)(tint.r * texel.r / 255), (byte)(tint.g * texel.g / 255), (byte)(tint.b * texel.b / 255), 255);
        }
    }
```

Сортировка ключей: `Keys.Sort()` после завершения задачи (расширение `NativeSortExtension`). Ключ упорядочен так: ячейка → rgb → корзина. Поэтому внутри ячейки точки одной корзины не обязательно идут подряд, и `ResolveCellsJob` считает корзины ячейки локальной таблицей на 4096 слотов. Слоты сбрасываются по списку тронутых.

```csharp
    /// <summary>Each cell's colour: the colour bin most of its points fell in, its points averaged.</summary>
    [BurstCompile]
    public struct ResolveCellsJob : IJob
    {
        [ReadOnly] public NativeArray<ulong> Keys; // sorted
        public NativeArray<Color32> Cells;

        public void Execute()
        {
            var count = new NativeArray<int>(4096, Allocator.Temp);
            var sum = new NativeArray<int3>(4096, Allocator.Temp);
            var touched = new NativeList<int>(64, Allocator.Temp);
            int i = 0;
            while (i < Keys.Length)
            {
                ulong cell = Keys[i] >> 36;
                int best = -1;
                for (; i < Keys.Length && Keys[i] >> 36 == cell; i++)
                {
                    int bin = (int)(Keys[i] & 0xFFF);
                    int rgb = (int)((Keys[i] >> 12) & 0xFFFFFF);
                    if (count[bin] == 0)
                        touched.Add(bin);
                    count[bin]++;
                    sum[bin] += new int3(rgb >> 16, (rgb >> 8) & 0xFF, rgb & 0xFF);
                    if (best < 0 || count[bin] > count[best])
                        best = bin;
                }

                int3 mean = sum[best] / count[best];
                Cells[(int)cell] = new Color32((byte)mean.x, (byte)mean.y, (byte)mean.z, VoxelizerPartGrid.SURFACE);
                for (int k = 0; k < touched.Length; k++)
                {
                    count[touched[k]] = 0;
                    sum[touched[k]] = int3.zero;
                }

                touched.Clear();
            }
        }
    }
}
```

- [ ] **Step 7: `VoxelizerSurface.cs`.** Сетка по габаритам с запасом в 1 воксель с каждой стороны, предел 512.

```csharp
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>A part's surface on the voxel grid: which cells its triangles pass through, and their colour.</summary>
    public static class VoxelizerSurface
    {
        public const int MAX_SIZE = 512;

        public static VoxelizerPartGrid Sample(VoxelizerTriangles triangles, float voxelSize)
        {
            float3 origin = math.floor(triangles.Min / voxelSize) * voxelSize - voxelSize;
            int3 size = math.clamp((int3)math.ceil((triangles.Max - origin) / voxelSize) + 2, 1, MAX_SIZE);
            var grid = new VoxelizerPartGrid
            {
                Size = size,
                Origin = origin,
                VoxelSize = voxelSize,
                Cells = new NativeArray<Color32>(size.x * size.y * size.z, Allocator.Persistent)
            };

            var keys = new NativeList<ulong>(triangles.Count * 16, Allocator.TempJob);
            new SampleSurfaceJob
            {
                Corners = triangles.Corners, Uvs = triangles.Uvs, Material = triangles.Material,
                Tints = triangles.Tints, Pixels = triangles.Pixels, Textures = triangles.Textures,
                Origin = origin, VoxelSize = voxelSize, Size = size, Keys = keys
            }.Run();
            keys.Sort();
            new ResolveCellsJob { Keys = keys.AsArray(), Cells = grid.Cells }.Run();
            keys.Dispose();
            return grid;
        }
    }
}
```

Тест 1 рассчитан так: куб 0.999 м с вокселем 0.1 занимает ячейки 1..10 по каждой оси (запас 1). Каждая грань лежит внутри слоя ячеек, поэтому оболочка — ровно 10³ − 8³ ячеек.

- [ ] **Step 8: Run, expect PASS** (оба теста).
- [ ] **Step 9: Commit**

```bash
git add Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Editor/Voxelizer.meta Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: surface points with texture colour, each cell takes its most common colour

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Заливка снаружи и внутренность

**Files:**
- Modify: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerJobs.cs` (+ `FloodOutsideJob`, `ShadeInteriorJob`)
- Modify: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerSurface.cs` (+ `public static void Fill(VoxelizerPartGrid grid)`)
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizerFillTests.cs`

**Interfaces:**
- Consumes: `VoxelizerPartGrid` (Task 2).
- Produces: `VoxelizerSurface.Fill(VoxelizerPartGrid)`. Ячейки, до которых заливка от краёв не дошла, получают a = 128 и цвет ближайшей поверхности × 0.85.

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerFillTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        private int Solid(params Mesh[] meshes)
        {
            var renderers = new List<Renderer>();
            foreach (Mesh m in meshes)
                renderers.Add(TestMeshes.Add(_root.transform, "Part", m, Color.gray));
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, renderers);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);
            VoxelizerSurface.Fill(grid);
            int solid = 0;
            foreach (Color32 c in grid.Cells)
                if (c.a != 0) solid++;
            return solid;
        }

        [Test]
        public void AClosedBox_IsSolid() =>
            Assert.AreEqual(1000, Solid(TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f)));

        [Test]
        public void TwoOverlappingBoxes_FillAsTheirUnion_WithoutParityStripes()
        {
            // 10×10×10 and a 10×10×10 shifted by 5 along X: union 15×10×10.
            int solid = Solid(TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f),
                TestMeshes.Box(new Vector3(0.501f, 0.001f, 0.001f), new Vector3(1.499f, 0.999f, 0.999f)));
            Assert.AreEqual(1500, solid);
        }

        [Test]
        public void InsideIsDarkerThanTheSurface()
        {
            MeshRenderer r = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), new Color(0.8f, 0.8f, 0.8f));
            using VoxelizerTriangles triangles = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, 0.1f);
            VoxelizerSurface.Fill(grid);
            Color32 centre = grid.Cells[5 + grid.Size.x * (5 + grid.Size.y * 5)];
            Assert.AreEqual(VoxelizerPartGrid.INSIDE, centre.a);
            Assert.Less(centre.r, 204);
        }
    }
}
```

- [ ] **Step 2: Run, expect FAIL** (`Fill` не найден).
- [ ] **Step 3: Implementation.** В `VoxelizerJobs.cs`:

```csharp
    /// <summary>
    /// Floods the empty space from the grid's border; every empty cell it never reaches is inside
    /// (a = INSIDE). Unlike parity rays it does not care whether the mesh is closed or overlaps itself.
    /// </summary>
    [BurstCompile]
    public struct FloodOutsideJob : IJob
    {
        public NativeArray<Color32> Cells;
        public int3 Size;

        public void Execute()
        {
            var outside = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            var queue = new NativeQueue<int>(Allocator.Temp);
            for (int z = 0; z < Size.z; z++)
            for (int y = 0; y < Size.y; y++)
            for (int x = 0; x < Size.x; x++)
            {
                bool border = x == 0 || y == 0 || z == 0 || x == Size.x - 1 || y == Size.y - 1 || z == Size.z - 1;
                int i = x + Size.x * (y + Size.y * z);
                if (border && Cells[i].a == 0)
                {
                    outside[i] = true;
                    queue.Enqueue(i);
                }
            }

            int sy = Size.x, sz = Size.x * Size.y;
            while (queue.TryDequeue(out int i))
            {
                int x = i % Size.x, y = (i / Size.x) % Size.y, z = i / sz;
                if (x > 0) Visit(i - 1, ref outside, ref queue);
                if (x < Size.x - 1) Visit(i + 1, ref outside, ref queue);
                if (y > 0) Visit(i - sy, ref outside, ref queue);
                if (y < Size.y - 1) Visit(i + sy, ref outside, ref queue);
                if (z > 0) Visit(i - sz, ref outside, ref queue);
                if (z < Size.z - 1) Visit(i + sz, ref outside, ref queue);
            }

            for (int i = 0; i < Cells.Length; i++)
                if (Cells[i].a == 0 && !outside[i])
                    Cells[i] = new Color32(0, 0, 0, VoxelizerPartGrid.INSIDE);
        }

        private void Visit(int j, ref NativeArray<bool> outside, ref NativeQueue<int> queue)
        {
            if (outside[j] || Cells[j].a != 0)
                return;
            outside[j] = true;
            queue.Enqueue(j);
        }
    }

    /// <summary>Inside cells take the colour of the nearest surface (breadth-first from it), × Darken.</summary>
    [BurstCompile]
    public struct ShadeInteriorJob : IJob
    {
        public NativeArray<Color32> Cells;
        public int3 Size;
        public float Darken;

        public void Execute()
        {
            var done = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            var queue = new NativeQueue<int>(Allocator.Temp);
            for (int i = 0; i < Cells.Length; i++)
                if (Cells[i].a == VoxelizerPartGrid.SURFACE)
                {
                    done[i] = true;
                    queue.Enqueue(i);
                }

            int sy = Size.x, sz = Size.x * Size.y;
            while (queue.TryDequeue(out int i))
            {
                Color32 from = Cells[i];
                int x = i % Size.x, y = (i / Size.x) % Size.y, z = i / sz;
                if (x > 0) Spread(i - 1, from, ref done, ref queue);
                if (x < Size.x - 1) Spread(i + 1, from, ref done, ref queue);
                if (y > 0) Spread(i - sy, from, ref done, ref queue);
                if (y < Size.y - 1) Spread(i + sy, from, ref done, ref queue);
                if (z > 0) Spread(i - sz, from, ref done, ref queue);
                if (z < Size.z - 1) Spread(i + sz, from, ref done, ref queue);
            }
        }

        private void Spread(int j, Color32 from, ref NativeArray<bool> done, ref NativeQueue<int> queue)
        {
            if (done[j] || Cells[j].a != VoxelizerPartGrid.INSIDE)
                return;
            done[j] = true;
            bool fromSurface = from.a == VoxelizerPartGrid.SURFACE;
            Cells[j] = fromSurface
                ? new Color32((byte)(from.r * Darken), (byte)(from.g * Darken), (byte)(from.b * Darken), VoxelizerPartGrid.INSIDE)
                : new Color32(from.r, from.g, from.b, VoxelizerPartGrid.INSIDE);
            queue.Enqueue(j);
        }
    }
```

В `VoxelizerSurface.cs`:

```csharp
        public const float INSIDE_SHADE = 0.85f;

        public static void Fill(VoxelizerPartGrid grid)
        {
            new FloodOutsideJob { Cells = grid.Cells, Size = grid.Size }.Run();
            new ShadeInteriorJob { Cells = grid.Cells, Size = grid.Size, Darken = INSIDE_SHADE }.Run();
        }
```

- [ ] **Step 4: Run, expect PASS** (3 теста + тесты Task 2).
- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: fill from outside, inside takes the nearest surface colour darkened

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Выпрямление «авто» — плоскости у осей и срез мелочи

Главный риск плана. После тестов — визуальная проверка на настоящих моделях.

**Files:**
- Modify: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerJobs.cs` (+ `SnapPlanesJob`, `OpenJob`)
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerStraighten.cs`
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizerStraightenTests.cs`

**Interfaces:**
- Produces:
  - `VoxelizerStraighten.SnapPlanes(VoxelizerTriangles t, float voxelSize, float maxAngleDeg = 10f)` — меняет `t.Corners` до `Sample`;
  - `VoxelizerStraighten.Open(VoxelizerPartGrid grid, int minDetail = 2)` — после `Fill`.

Как работает `SnapPlanes`. Треугольник с нормалью не дальше 10° от оси A (±X, ±Y, ±Z) плющится на плоскость `A = k·vs + ε`:
- `k` — ближайшая граница вокселя к средней координате треугольника по A;
- `ε = −0.25·vs·sign(n)`, то есть плоскость сдвигается на четверть вокселя внутрь тела, и точки грани попадают в один слой ячеек.

Соседние треугольники одной почти плоской грани снапятся к той же границе: грань выходит ровной.

`Open` — морфологическое открытие (эрозия, потом наращивание) кубом 3×3×3, то есть по 26 соседям. Части тоньше `minDetail` вокселей исчезают. Наращивание восстанавливает форму и цвета остальных. Куб, а не 6-соседство: при 6-соседстве наращивание не возвращает рёбра и углы, и у каждой коробки срезались бы рёбра.

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerStraightenTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void ATopFaceTiltedFiveDegrees_ComesOutFlat()
        {
            // A 2 m × 2 m slab whose top rises 5° along X: 0.5 m high at x 0, ~0.67 m at x 2.
            Mesh mesh = TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.5f, 1.999f));
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
                if (v[i].y > 0.25f)
                    v[i].y += v[i].x * Mathf.Tan(5f * Mathf.Deg2Rad);
            mesh.vertices = v;
            MeshRenderer r = TestMeshes.Add(_root.transform, "Slab", mesh, Color.gray);

            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            VoxelizerStraighten.SnapPlanes(t, 0.1f);
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);
            // The side faces still rise to the old sloped edge: a one-voxel sliver above the snapped
            // top along the high end. «Auto» always opens after filling, which cuts it.
            VoxelizerStraighten.Open(grid, 2);

            // Every column has the same height: the top is one flat layer.
            int expected = -1;
            for (int z = 1; z < grid.Size.z - 1; z++)
            for (int x = 1; x < grid.Size.x - 1; x++)
            {
                int top = -1;
                for (int y = 0; y < grid.Size.y; y++)
                    if (grid.Cells[x + grid.Size.x * (y + grid.Size.y * z)].a != 0) top = y;
                if (top < 0) continue;
                if (expected < 0) expected = top;
                Assert.AreEqual(expected, top, $"column {x},{z}");
            }
        }

        [Test]
        public void ARodThinnerThanTwoVoxels_IsCutAway_TheBoxStays()
        {
            MeshRenderer box = TestMeshes.Add(_root.transform, "Box", TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f), Color.gray);
            MeshRenderer rod = TestMeshes.Add(_root.transform, "Rod", TestMeshes.Box(new Vector3(0.451f, 1.001f, 0.451f), new Vector3(0.549f, 1.5f, 0.549f)), Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { box, rod });
            using VoxelizerPartGrid grid = VoxelizerSurface.Sample(t, 0.1f);
            VoxelizerSurface.Fill(grid);
            VoxelizerStraighten.Open(grid, 2);

            int solid = 0;
            foreach (Color32 c in grid.Cells)
                if (c.a != 0) solid++;
            Assert.AreEqual(1000, solid);
        }
    }
}
```

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implementation.** `SnapPlanesJob` (в `VoxelizerJobs.cs`):

```csharp
    /// <summary>
    /// Flattens every triangle whose normal lies within MaxAngle of an axis onto the voxel boundary
    /// nearest its mean, a quarter voxel into the body, so a nearly flat face samples as one even layer.
    /// </summary>
    [BurstCompile]
    public struct SnapPlanesJob : IJob
    {
        public NativeArray<float3> Corners;
        public float VoxelSize;
        public float CosMaxAngle;

        public void Execute()
        {
            for (int t = 0; t < Corners.Length / 3; t++)
            {
                float3 a = Corners[t * 3], b = Corners[t * 3 + 1], c = Corners[t * 3 + 2];
                float3 n = math.cross(b - a, c - a);
                float length = math.length(n);
                if (length < 1e-12f)
                    continue;
                n /= length;
                float3 an = math.abs(n);
                int axis = an.x >= an.y && an.x >= an.z ? 0 : an.y >= an.z ? 1 : 2;
                if (an[axis] < CosMaxAngle)
                    continue;

                float mean = (a[axis] + b[axis] + c[axis]) / 3f;
                float plane = math.round(mean / VoxelSize) * VoxelSize - 0.25f * VoxelSize * math.sign(n[axis]);
                a[axis] = plane; b[axis] = plane; c[axis] = plane;
                Corners[t * 3] = a; Corners[t * 3 + 1] = b; Corners[t * 3 + 2] = c;
            }
        }
    }
```

`OpenJob`: эрозия `minDetail − 1` раз по 26 соседям (клетка остаётся, если все 26 соседей полные), потом столько же наращиваний по 26 соседям. Наращивание возвращает только клетки, которые были полными до эрозии, со своим прежним цветом.

```csharp
    /// <summary>Morphological opening: removes every feature thinner than Passes + 1 voxels, keeps the rest as it was.</summary>
    [BurstCompile]
    public struct OpenJob : IJob
    {
        public NativeArray<Color32> Cells;
        public int3 Size;
        public int Passes;

        public void Execute()
        {
            var original = new NativeArray<Color32>(Cells, Allocator.Temp);
            var mask = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            var next = new NativeArray<bool>(Cells.Length, Allocator.Temp);
            for (int i = 0; i < Cells.Length; i++)
                mask[i] = Cells[i].a != 0;

            for (int p = 0; p < Passes; p++)
            {
                for (int i = 0; i < mask.Length; i++)
                    next[i] = mask[i] && AllNeighbours(mask, i);
                (mask, next) = (next, mask);
            }

            for (int p = 0; p < Passes; p++)
            {
                for (int i = 0; i < mask.Length; i++)
                    next[i] = mask[i] || (original[i].a != 0 && AnyNeighbour(mask, i));
                (mask, next) = (next, mask);
            }

            for (int i = 0; i < Cells.Length; i++)
                Cells[i] = mask[i] ? original[i] : default;
        }

        /// <summary>All 26 neighbours solid; a cell on the grid's edge never is.</summary>
        private bool AllNeighbours(NativeArray<bool> m, int i)
        {
            int3 p = new int3(i % Size.x, (i / Size.x) % Size.y, i / (Size.x * Size.y));
            if (math.any(p == 0) || math.any(p == Size - 1))
                return false;
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (!m[(p.x + dx) + Size.x * ((p.y + dy) + Size.y * (p.z + dz))])
                    return false;
            return true;
        }

        /// <summary>Any of the 26 neighbours solid.</summary>
        private bool AnyNeighbour(NativeArray<bool> m, int i)
        {
            int3 p = new int3(i % Size.x, (i / Size.x) % Size.y, i / (Size.x * Size.y));
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int3 q = p + new int3(dx, dy, dz);
                if (math.all(q >= 0) && math.all(q < Size) && m[q.x + Size.x * (q.y + Size.y * q.z)])
                    return true;
            }

            return false;
        }
    }
```

`VoxelizerStraighten.cs`:

```csharp
using Unity.Mathematics;

namespace Game.Voxels.EditorTools
{
    /// <summary>The «auto» straightening: faces near an axis lie on the grid, details under the minimum go.</summary>
    public static class VoxelizerStraighten
    {
        public static void SnapPlanes(VoxelizerTriangles triangles, float voxelSize, float maxAngleDeg = 10f) =>
            new SnapPlanesJob
            {
                Corners = triangles.Corners,
                VoxelSize = voxelSize,
                CosMaxAngle = math.cos(math.radians(maxAngleDeg))
            }.Run();

        public static void Open(VoxelizerPartGrid grid, int minDetail = 2)
        {
            if (minDetail < 2)
                return;
            new OpenJob { Cells = grid.Cells, Size = grid.Size, Passes = minDetail - 1 }.Run();
        }
    }
}
```

`SnapPlanes` меняет `Min`/`Max` не дальше чем на полвокселя. Запаса сетки в 1 воксель хватает, пересчитывать не нужно.

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Визуальная проверка до коммита.**
  - Скопировать `proto_voxelize.cs` из папки заметок (`side-session-notes/<тимлид>/voxelizer/`) в скретчпад.
  - Заменить в нём функцию `Voxelize` вызовами `VoxelizerSource.Collect` → `SnapPlanes` → `Sample` → `Fill` → `Open`.
  - Отрендерить стену Synty, изогнутую стену и Dropship «до и после» в скретчпад.
  - Если наклоны под 10–30° дают «ломаные» края (соседние треугольники одной грани снапятся к разным границам), выписать это в отчёт задачи. Вариант исправления — снап по кластерам копланарных треугольников; решение — по рендерам с пользователем.
- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: auto straightening, faces near an axis snap to the grid, thin details go

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Пометки «коробка» и «цилиндр»

**Files:**
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerPrimitives.cs`
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizerPrimitivesTests.cs`

**Interfaces:**
- Consumes: `VoxelizerPartGrid` после `Sample` (цвета поверхности).
- Produces:
  - `VoxelizerPrimitives.Box(VoxelizerPartGrid grid)` — заполняет габарит занятых ячеек целиком;
  - `VoxelizerPrimitives.Cylinder(VoxelizerPartGrid grid, int axis)` — цилиндр по оси `axis` (0 X, 1 Y, 2 Z), вписанный в габарит.

Цвет примитива — самый частый цвет поверхности части. Внутренность — тот же цвет × 0.85, a = 128.

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerPrimitivesTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("Model");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        private VoxelizerPartGrid Grid(Mesh mesh)
        {
            MeshRenderer r = TestMeshes.Add(_root.transform, "Part", mesh, Color.gray);
            using VoxelizerTriangles t = VoxelizerSource.Collect(_root.transform, new List<Renderer> { r });
            return VoxelizerSurface.Sample(t, 0.1f);
        }

        private static int Solid(VoxelizerPartGrid g)
        {
            int n = 0;
            foreach (Color32 c in g.Cells) if (c.a != 0) n++;
            return n;
        }

        [Test]
        public void Box_FillsTheBoundsOfARaggedPart()
        {
            Mesh rough = TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.999f, 0.499f, 0.799f));
            // Pull one corner of the top face (face 5 in TestMeshes.Box: vertices 20..23) down to
            // 0.2 m: the top is no longer flat, the bounds stay 1 × 0.5 × 0.8 m.
            Vector3[] v = rough.vertices;
            v[22] = new Vector3(v[22].x, 0.2f, v[22].z);
            rough.vertices = v;
            using VoxelizerPartGrid g = Grid(rough);
            VoxelizerPrimitives.Box(g);
            Assert.AreEqual(10 * 5 * 8, Solid(g));
        }

        [Test]
        public void Cylinder_AlongX_IsRoundAndSymmetric()
        {
            // A 1 m cube marked «cylinder along X»: diameter 10 voxels, length 10.
            using VoxelizerPartGrid g = Grid(TestMeshes.Box(Vector3.one * 0.001f, Vector3.one * 0.999f));
            VoxelizerPrimitives.Cylinder(g, 0);
            int solid = Solid(g);
            Assert.AreEqual(10 * 80, solid); // a disc of 10 voxels across holds 80 cells (centres inside r = 5)

            // Mirror symmetry in Y and Z.
            for (int z = 0; z < g.Size.z; z++)
            for (int y = 0; y < g.Size.y; y++)
            {
                bool a = g.Cells[5 + g.Size.x * (y + g.Size.y * z)].a != 0;
                bool b = g.Cells[5 + g.Size.x * ((g.Size.y - 1 - y) + g.Size.y * z)].a != 0;
                Assert.AreEqual(a, b);
            }
        }
    }
}
```

Диск в 10 вокселей: центры ячеек `(i + 0.5 − 5)`, условие `dx² + dy² ≤ 25`. Это 80 ячеек: по рядам 4, 8, 8, 10, 10, 10, 10, 8, 8, 4. Если реализация даёт другое число, сначала пересчитать вручную, потом править тест: число зависит от правила «центр внутри r».

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implementation** `VoxelizerPrimitives.cs`:

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>Parts marked «box» or «cylinder»: replaced by a clean primitive over the part's bounds, as TankBuilder builds them.</summary>
    public static class VoxelizerPrimitives
    {
        public static void Box(VoxelizerPartGrid grid) =>
            Replace(grid, -1);

        public static void Cylinder(VoxelizerPartGrid grid, int axis) =>
            Replace(grid, math.clamp(axis, 0, 2));

        private static void Replace(VoxelizerPartGrid grid, int axis)
        {
            int3 lo = grid.Size, hi = -1;
            var counts = new System.Collections.Generic.Dictionary<int, int>();
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                Color32 c = grid.Cells[i];
                if (c.a == 0) continue;
                int3 p = new int3(i % grid.Size.x, (i / grid.Size.x) % grid.Size.y, i / (grid.Size.x * grid.Size.y));
                lo = math.min(lo, p);
                hi = math.max(hi, p);
                if (c.a == VoxelizerPartGrid.SURFACE)
                {
                    int key = c.r << 16 | c.g << 8 | c.b;
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                }
            }

            if (math.any(hi < lo))
                return;

            int best = 0, bestCount = -1;
            foreach (var pair in counts)
                if (pair.Value > bestCount) { best = pair.Key; bestCount = pair.Value; }
            var surface = new Color32((byte)(best >> 16), (byte)(best >> 8), (byte)best, VoxelizerPartGrid.SURFACE);
            var inside = new Color32((byte)(surface.r * VoxelizerSurface.INSIDE_SHADE), (byte)(surface.g * VoxelizerSurface.INSIDE_SHADE),
                (byte)(surface.b * VoxelizerSurface.INSIDE_SHADE), VoxelizerPartGrid.INSIDE);

            float3 centre = (float3)(lo + hi + 1) * 0.5f;
            float3 half = (float3)(hi - lo + 1) * 0.5f;
            for (int i = 0; i < grid.Cells.Length; i++)
            {
                int3 p = new int3(i % grid.Size.x, (i / grid.Size.x) % grid.Size.y, i / (grid.Size.x * grid.Size.y));
                bool inBox = math.all(p >= lo) && math.all(p <= hi);
                bool inside = inBox && (axis < 0 || InDisc(p, centre, half, axis));
                if (!inside)
                {
                    grid.Cells[i] = default;
                    continue;
                }

                grid.Cells[i] = surface;
            }

            // Cells with all six neighbours solid are inside.
            int sy = grid.Size.x, sz = grid.Size.x * grid.Size.y;
            var copy = grid.Cells.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].a == 0) continue;
                int x = i % grid.Size.x, y = (i / grid.Size.x) % grid.Size.y, z = i / sz;
                if (x == 0 || y == 0 || z == 0 || x == grid.Size.x - 1 || y == grid.Size.y - 1 || z == grid.Size.z - 1) continue;
                if (copy[i - 1].a != 0 && copy[i + 1].a != 0 && copy[i - sy].a != 0 && copy[i + sy].a != 0 && copy[i - sz].a != 0 && copy[i + sz].a != 0)
                    grid.Cells[i] = inside;
            }
        }

        private static bool InDisc(int3 p, float3 centre, float3 half, int axis)
        {
            float3 d = ((float3)p + 0.5f - centre) / half;
            float2 q = axis == 0 ? d.yz : axis == 1 ? d.xz : d.xy;
            return math.lengthsq(q) <= 1f;
        }
    }
}
```

Это редакторский код вне тика: управляемый словарь допустим. Для цилиндра с неравным сечением (`half` по двум осям разный) получается эллипс по габариту. Так и задумано: у колеса габарит круглый.

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: parts marked box or cylinder become clean primitives over their bounds

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Палитра на модель и фильтр большинства

**Files:**
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerPalette.cs`
- Modify: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerJobs.cs` (+ `MajorityJob`)
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizerPaletteTests.cs`

**Interfaces:**
- Consumes: список `VoxelizerPartGrid` модели (после `Fill`, `Open`, примитивов).
- Produces:
  - `public static Color32[] VoxelizerPalette.Build(IReadOnlyList<VoxelizerPartGrid> parts, int k)`:
    - 256 цветов, уже вершинные (`MeshVoxelizer.ToVertexColor`), альфа 255;
    - слоты `1..k` — поверхность, `k+1..2k` — внутренность (× 0.85), остальные пустые (a = 0).
  - `public static NativeArray<byte> VoxelizerPalette.Map(VoxelizerPartGrid part, Color32[] authoredCentres, int k, Allocator a)` — байты сетки.
  - `VoxelizerPalette.Smooth(NativeArray<byte> voxels, int3 size, int k, int passes = 2)` — фильтр большинства только по слотам поверхности.

`Build` возвращает центры k-means в sRGB (`out Color32[] centres`) для `Map`. Полная сигнатура:

```csharp
public static Color32[] Build(IReadOnlyList<VoxelizerPartGrid> parts, int k, out Color32[] centres)
```

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerPaletteTests
    {
        private static VoxelizerPartGrid Noisy(int colours, int3 size)
        {
            var g = new VoxelizerPartGrid { Size = size, VoxelSize = 0.1f, Cells = new NativeArray<Color32>(size.x * size.y * size.z, Allocator.Persistent) };
            var random = new Unity.Mathematics.Random(7);
            for (int i = 0; i < g.Cells.Length; i++)
            {
                int c = random.NextInt(colours);
                g.Cells[i] = new Color32((byte)(c * 255 / colours), (byte)(255 - c * 255 / colours), 90, VoxelizerPartGrid.SURFACE);
            }
            return g;
        }

        [Test]
        public void ThePaletteHoldsAtMostKSurfaceAndKInsideColours()
        {
            using VoxelizerPartGrid g = Noisy(40, new int3(16, 16, 16));
            Color32[] palette = VoxelizerPalette.Build(new List<VoxelizerPartGrid> { g }, 12, out _);
            int used = 0;
            for (int i = 1; i < 256; i++) if (palette[i].a != 0) used++;
            Assert.LessOrEqual(used, 24);
            Assert.AreEqual(0, palette[0].a);
        }

        [Test]
        public void TwoColours_StayTwo()
        {
            var g = new VoxelizerPartGrid { Size = new int3(4, 4, 4), VoxelSize = 0.1f, Cells = new NativeArray<Color32>(64, Allocator.Persistent) };
            for (int i = 0; i < 64; i++)
                g.Cells[i] = i < 32 ? new Color32(200, 30, 30, 255) : new Color32(30, 30, 200, 255);
            Build2(g, out NativeArray<byte> voxels);
            var used = new HashSet<byte>();
            foreach (byte b in voxels) used.Add(b);
            Assert.AreEqual(2, used.Count);
            voxels.Dispose();
            g.Dispose();
        }

        private static void Build2(VoxelizerPartGrid g, out NativeArray<byte> voxels)
        {
            VoxelizerPalette.Build(new List<VoxelizerPartGrid> { g }, 12, out Color32[] centres);
            voxels = VoxelizerPalette.Map(g, centres, 12, Allocator.Persistent);
        }

        [Test]
        public void ALoneSpeck_TakesItsNeighboursColour()
        {
            int3 size = new int3(5, 5, 5);
            var voxels = new NativeArray<byte>(125, Allocator.Persistent);
            for (int i = 0; i < 125; i++) voxels[i] = 1;
            voxels[2 + 5 * (2 + 5 * 2)] = 2;
            VoxelizerPalette.Smooth(voxels, size, 12);
            Assert.AreEqual(1, voxels[2 + 5 * (2 + 5 * 2)]);
            voxels.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implementation** `VoxelizerPalette.cs`. k-means++ в RGB; точки — уникальные цвета с весом (число ячеек), 12 итераций, сид 5, детерминированно.

```csharp
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>The model's own palette: its surface colours brought down to K by k-means; inside colours are the same × 0.85.</summary>
    public static class VoxelizerPalette
    {
        public static Color32[] Build(IReadOnlyList<VoxelizerPartGrid> parts, int k, out Color32[] centres)
        {
            k = math.clamp(k, 1, 32);
            var weight = new Dictionary<int, int>();
            foreach (VoxelizerPartGrid part in parts)
            foreach (Color32 c in part.Cells)
            {
                if (c.a != VoxelizerPartGrid.SURFACE) continue;
                int key = c.r << 16 | c.g << 8 | c.b;
                weight.TryGetValue(key, out int n);
                weight[key] = n + 1;
            }

            var points = new List<float3>();
            var weights = new List<int>();
            foreach (var pair in weight)
            {
                points.Add(new float3(pair.Key >> 16 & 0xFF, pair.Key >> 8 & 0xFF, pair.Key & 0xFF));
                weights.Add(pair.Value);
            }

            List<float3> means = KMeans(points, weights, k);
            centres = new Color32[means.Count];
            var palette = new Color32[256];
            for (int i = 0; i < means.Count; i++)
            {
                float3 m = means[i];
                centres[i] = new Color32((byte)m.x, (byte)m.y, (byte)m.z, 255);
                palette[1 + i] = MeshVoxelizer.ToVertexColor((Color)centres[i]);
                palette[1 + k + i] = MeshVoxelizer.ToVertexColor((Color)centres[i] * VoxelizerSurface.INSIDE_SHADE);
            }

            return palette;
        }

        public static NativeArray<byte> Map(VoxelizerPartGrid part, Color32[] centres, int k, Allocator allocator)
        {
            var voxels = new NativeArray<byte>(part.Cells.Length, allocator);
            for (int i = 0; i < part.Cells.Length; i++)
            {
                Color32 c = part.Cells[i];
                if (c.a == 0) continue;
                // Inside cells are the surface × 0.85: undo it to find the same centre.
                float3 p = c.a == VoxelizerPartGrid.INSIDE
                    ? new float3(c.r, c.g, c.b) / VoxelizerSurface.INSIDE_SHADE
                    : new float3(c.r, c.g, c.b);
                int nearest = Nearest(centres, p);
                voxels[i] = (byte)(1 + nearest + (c.a == VoxelizerPartGrid.INSIDE ? k : 0));
            }

            return voxels;
        }

        public static void Smooth(NativeArray<byte> voxels, int3 size, int k, int passes = 2) =>
            new MajorityJob { Voxels = voxels, Size = size, LastSurface = k, Passes = passes }.Run();

        private static int Nearest(Color32[] centres, float3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < centres.Length; i++)
            {
                float d = math.distancesq(p, new float3(centres[i].r, centres[i].g, centres[i].b));
                if (d < bestD) { bestD = d; best = i; }
            }

            return best;
        }

        private static List<float3> KMeans(List<float3> points, List<int> weights, int k)
        {
            var means = new List<float3>();
            if (points.Count == 0)
                return means;
            var random = new Unity.Mathematics.Random(5);
            int heaviest = 0;
            for (int i = 1; i < points.Count; i++) if (weights[i] > weights[heaviest]) heaviest = i;
            means.Add(points[heaviest]);
            var d2 = new float[points.Count];
            while (means.Count < math.min(k, points.Count))
            {
                float total = 0f;
                for (int i = 0; i < points.Count; i++)
                {
                    float m = float.MaxValue;
                    foreach (float3 c in means) m = math.min(m, math.distancesq(points[i], c));
                    d2[i] = m * weights[i];
                    total += d2[i];
                }

                if (total <= 0f) break;
                float r = random.NextFloat(total);
                int pick = 0;
                for (; pick < points.Count - 1; pick++) { r -= d2[pick]; if (r <= 0f) break; }
                means.Add(points[pick]);
            }

            var sum = new float3[means.Count];
            var count = new float[means.Count];
            for (int iteration = 0; iteration < 12; iteration++)
            {
                System.Array.Clear(sum, 0, sum.Length);
                System.Array.Clear(count, 0, count.Length);
                for (int i = 0; i < points.Count; i++)
                {
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int c = 0; c < means.Count; c++)
                    {
                        float d = math.distancesq(points[i], means[c]);
                        if (d < bestD) { bestD = d; best = c; }
                    }

                    sum[best] += points[i] * weights[i];
                    count[best] += weights[i];
                }

                for (int c = 0; c < means.Count; c++)
                    if (count[c] > 0f) means[c] = sum[c] / count[c];
            }

            return means;
        }
    }
}
```

`MajorityJob` (в `VoxelizerJobs.cs`). Воксель поверхности (`1..LastSurface`), чей цвет не повторяет ни один из 26 соседей (или повторяет только один), берёт самый частый цвет поверхности среди соседей. Внутренность и пустота не трогаются. Проходов `Passes`; в каждом проходе читается копия.

```csharp
    [BurstCompile]
    public struct MajorityJob : IJob
    {
        public NativeArray<byte> Voxels;
        public int3 Size;
        public int LastSurface;
        public int Passes;

        public void Execute()
        {
            var read = new NativeArray<byte>(Voxels.Length, Allocator.Temp);
            var count = new NativeArray<int>(256, Allocator.Temp);
            for (int pass = 0; pass < Passes; pass++)
            {
                read.CopyFrom(Voxels);
                for (int z = 0; z < Size.z; z++)
                for (int y = 0; y < Size.y; y++)
                for (int x = 0; x < Size.x; x++)
                {
                    int i = x + Size.x * (y + Size.y * z);
                    byte v = read[i];
                    if (v == 0 || v > LastSurface) continue;
                    for (int c = 0; c < 256; c++) count[c] = 0;
                    int same = 0;
                    for (int dz = -1; dz <= 1; dz++)
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0 && dz == 0) continue;
                        int3 q = new int3(x + dx, y + dy, z + dz);
                        if (math.any(q < 0) || math.any(q >= Size)) continue;
                        byte w = read[q.x + Size.x * (q.y + Size.y * q.z)];
                        if (w == 0 || w > LastSurface) continue;
                        count[w]++;
                        if (w == v) same++;
                    }

                    if (same > 1) continue;
                    int best = v, bestCount = 0;
                    for (int c = 1; c <= LastSurface; c++)
                        if (count[c] > bestCount) { bestCount = count[c]; best = c; }
                    Voxels[i] = (byte)best;
                }
            }
        }
    }
```

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: the model's own palette of K colours, lone specks take their neighbours' colour

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Конфиг и конвейер по частям (`VoxelizeUseCase`)

**Files:**
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerConfig.cs`
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizeUseCase.cs`
- Modify: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerTypes.cs` (+ `VoxelizerResult`)
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizeUseCaseTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class VoxelizerConfig : ScriptableObject`:
    - `public GameObject model`;
    - `public ModelKind kind` (`Vehicle`, `Building`);
    - `public float length` (м; 0 — масштаб как в модели);
    - `public int colours = 12`;
    - `public PartRule[] parts`;
    - `public float VoxelSize` → 0.03 / 0.1.
  - Вложенные типы `ModelKind`, `PartMark` (`Auto`, `Box`, `Cylinder`, `AsIs`, `Skip`) и `[Serializable] PartRule`:
    - `public string path`;
    - `public PartMark mark`;
    - `public int axis`.
  - `public sealed class VoxelizerResult : IDisposable`:
    - `public List<VoxelizerResultPart> Parts`, где у части `Path`, `Root`, `Size`, `Origin`, `NativeArray<byte> Voxels`;
    - `public Color32[] Palette`;
    - `public float VoxelSize`;
    - `public float Scale`.
  - `public static VoxelizerResult VoxelizeUseCase.Run(VoxelizerConfig config, GameObject instance)`.
    - `instance` — уже созданная копия модели (в превью-сцене или в тесте).
    - Масштаб (`length`) применяется так: сетка строится по пространству части, делённому на `scale`. Воксель в метрах модели — `VoxelSize / scale`. Префаб потом масштабируется на `scale` (Task 8).

Порядок в `Run`:
1. Найти корни частей по путям, с `*` и `?` как в `VoxelizedModel.Resolve`. Пустой список — одна часть, корень модели. Рендерер принадлежит ближайшему корню части выше себя.
2. Для каждой части, кроме `Skip`:
   1. `Collect`;
   2. если `Auto` — `SnapPlanes`;
   3. `Sample`;
   4. если `Box` или `Cylinder` — примитив, иначе `Fill`;
   5. если `Auto` — `Open(2)`.
3. `VoxelizerPalette.Build` по всем частям.
4. `Map` и `Smooth` каждой части.

- [ ] **Step 1: Failing test**

```csharp
using Game.Voxels.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizeUseCaseTests
    {
        [Test]
        public void AHullWithAWheel_GivesTwoParts_TheWheelARoundCylinder_ASkippedPartNothing()
        {
            var root = new GameObject("Car");
            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            TestMeshes.Add(hull.transform, "Body", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(1.999f, 0.6f, 0.999f)), Color.gray);
            var wheel = new GameObject("Wheel_1");
            wheel.transform.SetParent(hull.transform, false);
            wheel.transform.localPosition = new Vector3(2.2f, 0f, 0f);
            TestMeshes.Add(wheel.transform, "Tyre", TestMeshes.Box(new Vector3(0.001f, -0.399f, -0.399f), new Vector3(0.299f, 0.399f, 0.399f)), Color.black);
            var aerial = new GameObject("Aerial");
            aerial.transform.SetParent(hull.transform, false);
            TestMeshes.Add(aerial.transform, "Rod", TestMeshes.Box(Vector3.one * 0.001f, new Vector3(0.05f, 1f, 0.05f)), Color.black);

            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Building; // 0.1 m, keeps the test grid small
            config.parts = new[]
            {
                new VoxelizerConfig.PartRule { path = "Hull", mark = VoxelizerConfig.PartMark.Auto },
                new VoxelizerConfig.PartRule { path = "Hull/Wheel_*", mark = VoxelizerConfig.PartMark.Cylinder, axis = 0 },
                new VoxelizerConfig.PartRule { path = "Hull/Aerial", mark = VoxelizerConfig.PartMark.Skip }
            };

            using VoxelizerResult result = VoxelizeUseCase.Run(config, root);

            Assert.AreEqual(2, result.Parts.Count);
            VoxelizerResultPart wheelPart = result.Parts.Find(p => p.Path == "Hull/Wheel_1");
            Assert.IsNotNull(wheelPart);
            int solid = 0;
            foreach (byte b in wheelPart.Voxels) if (b != 0) solid++;
            Assert.AreEqual(3 * 52, solid); // 8 voxels across: 52 cells a disc, 3 thick

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }
    }
}
```

Диск 8 вокселей: центры `(i + 0.5 − 4)`, `dx² + dy² ≤ 16`, по рядам 4, 6, 8, 8, 8, 8, 6, 4 = 52. Толщина колеса 0.3 м = 3 вокселя. «Антенна» помечена `Skip`, её нет. Без пометки она ушла бы в корпус, и `Open` срезал бы её как деталь тоньше 2 вокселей.

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implementation.** `VoxelizerConfig.cs`:

```csharp
using System;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>How one model is voxelized: kept as an asset next to the baked prefab, so a rebake gives the same result.</summary>
    [CreateAssetMenu(menuName = "Game/Voxels/Voxelizer Config", fileName = "ModelVoxelizerConfig")]
    public sealed class VoxelizerConfig : ScriptableObject
    {
        public enum ModelKind { Vehicle, Building }

        public enum PartMark { Auto, Box, Cylinder, AsIs, Skip }

        [Serializable]
        public sealed class PartRule
        {
            [Tooltip("Path from the model root; '*' and '?' match any name in that step.")]
            public string path = "";
            public PartMark mark;
            [Tooltip("Cylinder axis in the part's space: 0 X, 1 Y, 2 Z.")]
            [Range(0, 2)] public int axis;
        }

        public const float VEHICLE_VOXEL = 0.03f;
        public const float BUILDING_VOXEL = 0.1f;

        public GameObject model;
        public ModelKind kind;
        [Tooltip("Real length of the model's longest side, m; 0 — as the model is.")]
        public float length;
        [Range(4, 32)] public int colours = 12;
        public PartRule[] parts = Array.Empty<PartRule>();

        public float VoxelSize => kind == ModelKind.Vehicle ? VEHICLE_VOXEL : BUILDING_VOXEL;
    }
}
```

`VoxelizeUseCase.cs`. Поиск частей копирует логику `VoxelizedModel.Resolve`/`FindOwner`: те методы приватные, а класс трогать нельзя. Поэтому код повторяется здесь, со ссылкой в комментарии.

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>The whole voxelizer: a model instance and its config in, one byte grid per part and the model's palette out.</summary>
    public static class VoxelizeUseCase
    {
        public static VoxelizerResult Run(VoxelizerConfig config, GameObject instance)
        {
            Transform root = instance.transform;
            float scale = Scale(config, instance);
            float voxel = config.VoxelSize / scale;

            var rules = new List<(Transform part, VoxelizerConfig.PartRule rule)>();
            foreach (VoxelizerConfig.PartRule rule in config.parts)
                foreach (Transform t in Resolve(root, rule.path))
                    rules.Add((t, rule));
            if (rules.Count == 0)
                rules.Add((root, new VoxelizerConfig.PartRule { path = "", mark = VoxelizerConfig.PartMark.Auto }));

            var owned = new Dictionary<Transform, List<Renderer>>();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Transform owner = null;
                for (Transform t = renderer.transform; t != null && owner == null; t = t == root ? null : t.parent)
                    foreach (var (part, _) in rules)
                        if (part == t) { owner = t; break; }
                if (owner == null) continue;
                if (!owned.TryGetValue(owner, out List<Renderer> list)) owned[owner] = list = new List<Renderer>();
                list.Add(renderer);
            }

            var grids = new List<VoxelizerPartGrid>();
            foreach (var (part, rule) in rules)
            {
                if (rule.mark == VoxelizerConfig.PartMark.Skip || !owned.TryGetValue(part, out List<Renderer> renderers))
                    continue;

                using VoxelizerTriangles triangles = VoxelizerSource.Collect(part, renderers);
                if (triangles.Count == 0) continue;
                if (rule.mark == VoxelizerConfig.PartMark.Auto)
                    VoxelizerStraighten.SnapPlanes(triangles, voxel);
                VoxelizerPartGrid grid = VoxelizerSurface.Sample(triangles, voxel);
                grid.Root = part;
                grid.Path = PathOf(root, part);
                switch (rule.mark)
                {
                    case VoxelizerConfig.PartMark.Box: VoxelizerPrimitives.Box(grid); break;
                    case VoxelizerConfig.PartMark.Cylinder: VoxelizerPrimitives.Cylinder(grid, rule.axis); break;
                    default: VoxelizerSurface.Fill(grid); break;
                }

                if (rule.mark == VoxelizerConfig.PartMark.Auto)
                    VoxelizerStraighten.Open(grid, 2);
                grids.Add(grid);
            }

            var result = new VoxelizerResult { VoxelSize = voxel, Scale = scale };
            result.Palette = VoxelizerPalette.Build(grids, config.colours, out Color32[] centres);
            foreach (VoxelizerPartGrid grid in grids)
            {
                NativeArray<byte> voxels = VoxelizerPalette.Map(grid, centres, config.colours, Allocator.Persistent);
                VoxelizerPalette.Smooth(voxels, grid.Size, config.colours);
                result.Parts.Add(new VoxelizerResultPart
                {
                    Path = grid.Path, Root = grid.Root, Size = grid.Size, Origin = grid.Origin, Voxels = voxels
                });
                grid.Dispose();
            }

            return result;
        }

        /// <summary>Model metres to real metres: the config's length over the model's longest side.</summary>
        private static float Scale(VoxelizerConfig config, GameObject instance)
        {
            if (config.length <= 0f) return 1f;
            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            float longest = math.cmax((float3)bounds.size);
            return longest > 1e-4f ? config.length / longest : 1f;
        }

        private static string PathOf(Transform root, Transform t)
        {
            if (t == root) return "";
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>Same matching as VoxelizedModel.Resolve: '*' and '?' within one step.</summary>
        private static List<Transform> Resolve(Transform root, string path)
        {
            var level = new List<Transform> { root };
            if (string.IsNullOrWhiteSpace(path)) return level;
            foreach (string step in path.Split('/'))
            {
                var pattern = new Regex("^" + Regex.Escape(step).Replace("\\*", ".*").Replace("\\?", ".") + "$");
                var next = new List<Transform>();
                foreach (Transform parent in level)
                    foreach (Transform child in parent)
                        if (pattern.IsMatch(child.name)) next.Add(child);
                level = next;
            }

            return level;
        }
    }
}
```

В `VoxelizerTypes.cs` добавить:

```csharp
    public sealed class VoxelizerResultPart
    {
        public string Path;
        public Transform Root;
        public int3 Size;
        /// <summary>Part space, model units (the prefab root scales them to real size).</summary>
        public float3 Origin;
        public NativeArray<byte> Voxels;
    }

    public sealed class VoxelizerResult : IDisposable
    {
        public readonly System.Collections.Generic.List<VoxelizerResultPart> Parts = new();
        public Color32[] Palette;
        public float VoxelSize; // model units: the class voxel / Scale
        public float Scale = 1f;

        public void Dispose()
        {
            foreach (VoxelizerResultPart part in Parts)
                if (part.Voxels.IsCreated) part.Voxels.Dispose();
            Parts.Clear();
        }
    }
```

В `VoxelizerTypes.cs` теперь четыре типа. Файл-набор `…Types.cs` это разрешает.

Колесо внутри корпуса в тесте. Рендерер `Tyre` ближе к корню `Wheel_1`, чем к `Hull`, поэтому идёт в колесо. Цикл владельца идёт вверх и берёт первый корень части.

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: config per model and the pipeline part by part

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Запекание в префаб и загрузка в игре

**Files:**
- Create: `Assets/Game/Voxels/Runtime/VoxelModel.cs`
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelModelBakeUseCase.cs`
- Test (EditMode): `Assets/Game/Voxels/Tests/Voxelizer/VoxelModelBakeUseCaseTests.cs`
- Test (PlayMode): `Assets/Game/Voxels/Tests/VoxelModelTests.cs`

**Interfaces:**
- Consumes: `VoxelizerResult` (Task 7), `VoxelModelAsset` (Task 1).
- Produces:
  - `public sealed class VoxelModel : MonoBehaviour`:
    - `[SerializeField] VoxelModelAsset _data`;
    - `public IReadOnlyList<VoxelBody> Bodies`;
    - `public void Build()` (вызывается из `Start`).
  - `public static GameObject VoxelModelBakeUseCase.Bake(VoxelizerConfig config, string folder)`:
    - создаёт копию модели;
    - запускает `VoxelizeUseCase.Run`;
    - пишет `VX_<Имя>_Data.asset`, сохраняет `VX_<Имя>.prefab` в `folder`;
    - возвращает ассет префаба.
  - `public static VoxelModelAsset VoxelModelBakeUseCase.ToAsset(VoxelizerResult result, VoxelizerConfig config)` — чистая функция для тестов.

`VoxelModel.Build`. Для каждой части:
1. найти `transform.Find(path)` (пустой путь — сам корень);
2. создать дочерний `Voxels` на слое части;
3. `AddComponent<VoxelBody>().Init(size, Decode(runs), palette copy, data.Settings(origin, layer))`.

Префаб — копия модели:
- рендереры вокселизированных частей удалены;
- рендереры `Skip`-частей оставлены;
- масштаб корня × `Scale`;
- на корне `VoxelModel`.

Палитра в `Init` — `new NativeArray<Color32>(data.palette, Allocator.Persistent)`: тело владеет своей копией.

- [ ] **Step 1: Failing EditMode test**

```csharp
using System.Collections.Generic;
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelModelBakeUseCaseTests
    {
        [Test]
        public void ToAsset_KeepsEveryPartAndTheClassSettings()
        {
            var result = new VoxelizerResult { VoxelSize = 0.03f, Palette = new Color32[256] };
            var voxels = new NativeArray<byte>(27, Allocator.Persistent);
            for (int i = 0; i < 27; i++) voxels[i] = 1;
            result.Parts.Add(new VoxelizerResultPart { Path = "Hull", Size = new Unity.Mathematics.int3(3, 3, 3), Voxels = voxels });
            var config = ScriptableObject.CreateInstance<VoxelizerConfig>();
            config.kind = VoxelizerConfig.ModelKind.Vehicle;

            VoxelModelAsset asset = VoxelModelBakeUseCase.ToAsset(result, config);

            Assert.AreEqual(1, asset.parts.Length);
            Assert.AreEqual("Hull", asset.parts[0].path);
            Assert.AreEqual(0.45f, asset.toughness);
            Assert.AreEqual(7800f, asset.density);
            Assert.AreEqual(VoxelAnchor.Largest, asset.anchor);
            Assert.IsFalse(asset.colliders);
            NativeArray<byte> back = VoxelModelAsset.Decode(asset.parts[0].runs, 27, Allocator.Temp);
            Assert.AreEqual(27, System.Linq.Enumerable.Count(back, b => b == 1));
            back.Dispose();
            result.Dispose();
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(config);
        }
    }
}
```

- [ ] **Step 2: Failing PlayMode test** `Assets/Game/Voxels/Tests/VoxelModelTests.cs` (сборка `Game.Voxels.Tests`, по образцу `VoxelPlayModeTests.cs`: там видно, как тест поднимает `VoxelWorld`; повторить ту же подготовку):

```csharp
using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Voxels.Tests
{
    public sealed class VoxelModelTests
    {
        [UnityTest]
        public IEnumerator ABakedModel_BuildsABodyPerPart_WithEveryVoxel()
        {
            var data = ScriptableObject.CreateInstance<VoxelModelAsset>();
            data.voxelSize = 0.1f;
            data.palette = new Color32[256];
            data.palette[1] = new Color32(200, 200, 200, 255);
            var voxels = new NativeArray<byte>(4 * 4 * 4, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++) voxels[i] = 1;
            data.parts = new[] { new VoxelModelAsset.Part { path = "", size = new Vector3Int(4, 4, 4), runs = VoxelModelAsset.Encode(voxels) } };
            voxels.Dispose();

            var go = new GameObject("Model");
            go.SetActive(false);
            VoxelModel model = go.AddComponent<VoxelModel>();
            model.SetData(data);
            go.SetActive(true);
            yield return null;

            Assert.AreEqual(1, model.Bodies.Count);
            Assert.AreEqual(64, model.Bodies[0].SolidCount);
            Object.Destroy(go);
            Object.Destroy(data);
        }
    }
}
```

`SetData(VoxelModelAsset)` — сеттер для тестов и запекания (`[SerializeField]` поле в префаб пишет `SerializedObject`, а тесту нужен прямой путь).

- [ ] **Step 3: Run both, expect FAIL.** PlayMode-тест — только при чистой открытой сцене:

```bash
unity test "C:/!!!Work/CodeCraft_HomeWork_ATOMIC/.claude/worktrees/voxelizer-e3c9e1" --mode PlayMode --filter Game.Voxels.Tests.VoxelModelTests -o Logs/voxelizer-playmode.xml
```

- [ ] **Step 4: Implementation** `VoxelModel.cs`:

```csharp
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// A model baked by the Voxelizer: on start each part gets a VoxelBody from the baked data, under
    /// that part's transform, so a turret still turns and wheels still spin. Nothing is voxelized here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelModel : MonoBehaviour
    {
        [SerializeField] private VoxelModelAsset _data;

        private readonly List<VoxelBody> _bodies = new();

        public IReadOnlyList<VoxelBody> Bodies => _bodies;

        public void SetData(VoxelModelAsset data) => _data = data;

        private void Start() => Build();

        public void Build()
        {
            if (_bodies.Count > 0 || _data == null)
                return;

            foreach (VoxelModelAsset.Part part in _data.parts)
            {
                Transform root = string.IsNullOrEmpty(part.path) ? transform : transform.Find(part.path);
                if (root == null)
                {
                    Debug.LogWarning($"[VoxelModel] {name}: no part '{part.path}'; it is left out.", this);
                    continue;
                }

                int3 size = new int3(part.size.x, part.size.y, part.size.z);
                NativeArray<byte> voxels = VoxelModelAsset.Decode(part.runs, size.x * size.y * size.z, Allocator.Persistent);
                var palette = new NativeArray<Color32>(_data.palette, Allocator.Persistent);
                var go = new GameObject("Voxels") { layer = root.gameObject.layer };
                go.transform.SetParent(root, false);
                VoxelBody body = go.AddComponent<VoxelBody>();
                body.Init(size, voxels, palette, _data.Settings(part.origin, root.gameObject.layer));
                _bodies.Add(body);
            }
        }
    }
}
```

`VoxelModelBakeUseCase.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Voxels.EditorTools
{
    /// <summary>Bakes a config's model into VX_&lt;Name&gt;_Data.asset and VX_&lt;Name&gt;.prefab.</summary>
    public static class VoxelModelBakeUseCase
    {
        public static VoxelModelAsset ToAsset(VoxelizerResult result, VoxelizerConfig config)
        {
            var asset = ScriptableObject.CreateInstance<VoxelModelAsset>();
            bool vehicle = config.kind == VoxelizerConfig.ModelKind.Vehicle;
            asset.voxelSize = result.VoxelSize;
            asset.toughness = vehicle ? 0.45f : 1f;
            asset.density = vehicle ? 7800f : 1800f;
            asset.anchor = vehicle ? VoxelAnchor.Largest : VoxelAnchor.Ground;
            asset.colliders = !vehicle;
            asset.colliderCell = vehicle ? 0 : 2;
            asset.palette = (Color32[])result.Palette.Clone();
            asset.parts = new VoxelModelAsset.Part[result.Parts.Count];
            for (int i = 0; i < result.Parts.Count; i++)
            {
                VoxelizerResultPart part = result.Parts[i];
                asset.parts[i] = new VoxelModelAsset.Part
                {
                    path = part.Path,
                    size = new Vector3Int(part.Size.x, part.Size.y, part.Size.z),
                    origin = part.Origin,
                    runs = VoxelModelAsset.Encode(part.Voxels)
                };
            }

            return asset;
        }

        public static GameObject Bake(VoxelizerConfig config, string folder)
        {
            string name = config.model.name;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(config.model);
            if (instance == null) instance = Object.Instantiate(config.model);
            try
            {
                using VoxelizerResult result = VoxelizeUseCase.Run(config, instance);
                VoxelModelAsset data = ToAsset(result, config);
                AssetDatabase.CreateAsset(data, Path.Combine(folder, $"VX_{name}_Data.asset").Replace('\\', '/'));

                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // The voxels take the place of every renderer a part owns; the mesh filter goes first,
                // while the renderer still exists to find it by.
                foreach (VoxelizerResultPart part in result.Parts)
                    foreach (Renderer r in part.Root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (Owner(r.transform, result) != part.Root) continue;
                        if (r.TryGetComponent(out MeshFilter filter)) Object.DestroyImmediate(filter);
                        Object.DestroyImmediate(r);
                    }

                // The grids are in the model's own units (voxel = real voxel / Scale); the root's
                // scale brings the whole model, skipped parts too, to its real size.
                instance.transform.localScale *= result.Scale;
                instance.AddComponent<VoxelModel>().SetData(data);
                instance.name = $"VX_{name}";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, Path.Combine(folder, $"VX_{name}.prefab").Replace('\\', '/'));
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Transform Owner(Transform from, VoxelizerResult result)
        {
            for (Transform t = from; t != null; t = t.parent)
                foreach (VoxelizerResultPart part in result.Parts)
                    if (part.Root == t) return t;
            return null;
        }
    }
}
```

**Масштаб.**
- Сетки и `voxelSize` в ассете — в единицах модели: `VoxelizeUseCase` кладёт в `VoxelizerResult.VoxelSize` значение `config.VoxelSize / scale`, а `Origin` не масштабирует.
- Корень префаба масштабируется на `Scale`: воксель в мире = настоящий размер класса.

Риск: `VoxelBody` под масштабированным родителем не проверялся (у танка и домов масштаб 1). В Task 9 есть модель с `length` ≠ размеру исходника (Dropship). Если кратеры на ней не того размера, нужно отказаться от масштаба корня. Тогда `length` переносится в Scale Factor импорта FBX, и это решается с пользователем.

- [ ] **Step 5: Run both, expect PASS.**
- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Voxels/Runtime/VoxelModel.cs* Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer Assets/Game/Voxels/Tests/VoxelModelTests.cs*
git commit -m "Voxels: bake a voxelized model into VX_ data and prefab, VoxelModel builds its bodies on start

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Ранний замер и первые модели — контрольная точка с пользователем

Просьба тимлида: проверить объём вокселей техники по новым правилам до того, как вкладываться в окно.

- [ ] **Step 1: Танк по новым правилам.** Скрипт в скретчпаде (`script eval`):
  1. создать `VoxelizerConfig` в памяти:
     - модель `Assets/Game/Prefabs/Vehicles/Tank_01.prefab`, класс `Vehicle`;
     - части как в `VoxelizedModel` по умолчанию: `HullVisual`, `HullVisual/TurretVisual`, `HullVisual/TurretVisual/Gun`, `HullVisual/Wheel_*`, `HullVisual/Track?_Front`, `HullVisual/Track?_Rear` — «авто»; колёса — «цилиндр» по X;
     - `HullVisual/Track?_Link*` — «пропустить»;
  2. сделать копию префаба в превью-сцене и запустить `VoxelizeUseCase.Run`;
  3. записать число вокселей и чанков 16³ по частям рядом с нынешними (`VoxelizedModel.Voxelize()`; сейчас 144 тыс. вокселей, 23 тела).

  Нынешний танк уже 3 см. Разница даст только заливка и выпрямление: ожидаем ±10 %. Если больше — разобраться до замера.
- [ ] **Step 2: Замер в VoxelSandbox.**
  1. Только в своём редакторе. Сцену не сохранять: подмену делать в Play.
  2. Скрипт после входа в Play выключает на танке `VoxelizedModel` (если уже построил — удаляет его тела) и добавляет `VoxelModel` с данными из Step 1.
  3. Запустить агент `perf-check` с `repo` = рабочая копия, `measure: yes, port <PORT>`, `play: free`.
  4. Сравнить с последней строкой `game/develop` в `Docs/Reports/perf-history.jsonl`.

  Если подмена в Play неудобна, замерить два варианта одной сцены тем же стендом `Tools/PerfCheck/Bench/perf_bench.cs`: как есть и с подменой.
- [ ] **Step 3: Модели «до и после».** Через `VoxelizeUseCase` и превью (меш по сетке, как в `render_voxels.cs`) отрендерить:
  - танк;
  - Dropship (`Assets/3rdParty/OmniShade/Samples/Dropship Baked/Models/Dropship.fbx`, класс техника, длина 15 м);
  - стену-контейнер и изогнутую стену Synty (класс здание);
  - одно здание из паков, если найдётся целое (поиск `SM_Bld_*` в `Assets/Game/Prefabs/Environment`).

  Отправить пользователю через `SendUserFile` с цифрами замера.
- [ ] **Step 4: Решение пользователя.** Устраивает ли выпрямление, укладывается ли техника в бюджет. Если нет — правка правил: порог угла, минимальная деталь, размер вокселя техники. Правки вносятся в спецификацию, потом в задачи 4–7. Окно (Task 10) не начинать до ответа.
- [ ] **Step 5:** Отчёт тимлиду: цифры, рендеры, решение пользователя.

---

### Task 9.1: Камуфляж и декали — общий код для `VoxelizedModel` и вокселизатора

Решение пользователя от 2026-11-17 после контрольной точки (Task 9): танк по новым правилам теряет камуфляж и надписи. Покраска переносится в общий код. Тимлид разрешил правку `VoxelizedModel` — отдельным коммитом, без изменения поведения.

**Files:**
- Create: `Assets/Game/Voxels/Runtime/VoxelPaint.cs` — статический класс и Burst-задача покраски.
- Modify: `Assets/Game/Voxels/Runtime/VoxelizedModel.cs` — `PaintCamouflage`/`PaintDecals`/`CamouflageJob` переезжают в `VoxelPaint`. Публичные типы `VoxelizedModel.Camouflage` и `VoxelizedModel.Decal` остаются на месте, их сериализованные поля не меняются, поэтому префаб `Tank_01` не ломается.
- Modify: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerConfig.cs`, `VoxelizeUseCase.cs` — у конфига появляются поля `camouflage` и `decals` тех же типов. Покраска идёт после `Map`/`Smooth`.
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelPaintTests.cs`.

**Interfaces:**
- `public static class VoxelPaint`:
  - `int Camouflage(NativeArray<byte> voxels, int3 size, float3 origin, float voxelSize, float4x4 partToModel, NativeArray<Color32> palette, int paintFirst, int variants, in VoxelizedModel.Camouflage camo, float noise)` — перекрашивает воксели слотов `[paintFirst, paintFirst + variants)`. Новые цвета кладёт в свободные слоты палитры через `MeshVoxelizer.AddShades` при `variants == MeshVoxelizer.VARIANTS`; при `variants == 1` — по одному слоту на цвет. Возвращает число перекрашенных вокселей.
  - `void Decals(NativeArray<byte> voxels, int3 size, float3 origin, float voxelSize, NativeArray<Color32> palette, int variants, string partPath, VoxelizedModel.Decal[] decals)` — декали только для `decal.Part == partPath`.
- В вокселизаторе `variants = 1`, слоты 1..2K. `paintFirst` — слот поверхности, ближайший к цвету `camo.Paint`, а если материала нет — к `camo.Paint.color`. Если ближайший цвет дальше порога, камуфляж пропускается с предупреждением.

**Требования:**
- `VoxelizedModel` даёт **побайтно ту же** сетку и палитру, что до переноса. Тест: `VoxelizedModel.Voxelize()` на копии `Tank_01.prefab`, хеш всех `Voxels` и `Palette` до и после переноса. Хеш «до» снять заранее на текущем коде и записать в тест константой.
- Вокселизатор с камуфляжем и декалями танка (взять из компонента `VoxelizedModel` на `Tank_01.prefab`: пути частей те же) даёт на корпусе и башне больше одного цвета поверхности, а на борту башни — пиксели декали.
- Отдельные коммиты: (1) перенос в `VoxelPaint` без изменения поведения, вместе с тестом хеша; (2) конфиг и вокселизатор.

### Task 9.2: Коробка и цилиндр сохраняют цвета исходника

Решение пользователя: колёса-цилиндры не должны быть сплошными тёмными барабанами.

**Files:** Modify `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerPrimitives.cs`; Test `VoxelizerPrimitivesTests.cs`.

**Требования:**
- Каждая клетка поверхности примитива берёт цвет ближайшей клетки поверхности исходной части. Ближайшая — по BFS от клеток `SURFACE` исходной сетки по 26 соседям. Внутренность — этот цвет × `INSIDE_SHADE`.
- Тест: деталь «диск» из двух цветов (обод и центр разными материалами) после `Cylinder` имеет оба цвета на торце. Старый тест «одного цвета» тоже остаётся зелёным.

### Task 9.3: Срез тонких деталей — выключатель, по умолчанию выключен

Решение пользователя: тонкие оболочки (Dropship) не должны рассыпаться.

**Files:** Modify `VoxelizerConfig.cs` (поле `public bool cutThinDetails;` по умолчанию `false`, с тултипом), `VoxelizeUseCase.cs` (`Open` только если `cutThinDetails`); Test `VoxelizeUseCaseTests.cs`.

**Требования:**
- По умолчанию «авто» = только выпрямление плоскостей.
- Тест: однослойная пластина (незамкнутый quad или плита в 1 воксель) по умолчанию остаётся. С `cutThinDetails = true` она исчезает.

### Task 9.4: Воксель 6 см для техники длиннее 6 м

Решение пользователя: крупная техника — воксель 6 см вместо 3 см.

**Files:** Modify `VoxelizerConfig.cs`, `VoxelizeUseCase.cs`; Test `VoxelizeUseCaseTests.cs`.

**Требования:**
- `VoxelizerConfig`: константы `LARGE_VEHICLE_LENGTH = 6f`, `LARGE_VEHICLE_VOXEL = 0.06f`.
- Метод `public float VoxelSizeFor(float realLength)`:
  - техника — `realLength > LARGE_VEHICLE_LENGTH ? 0.06 : 0.03`;
  - здание — `0.1`.
- Свойство `VoxelSize` убирается. Все вызовы переходят на `VoxelSizeFor`, в том числе `VoxelModelBakeUseCase.ToAsset`, который берёт `result.VoxelSize`.
- Реальная длина = `length`, если он больше 0, иначе длиннейшая сторона габарита модели.
- Тесты:
  - техника длиной 5 м → 0.03, 7 м → 0.06, здание → 0.1;
  - `Run` на модели 8 м даёт `VoxelSize ≈ 0.06 / scale`.

---

### Task 10: Окно «Вокселизатор» и превью без Play

**Files:**
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerPreview.cs`
- Create: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerWindow.cs`
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxelizerPreviewTests.cs`

**Interfaces:**
- Consumes: `VoxelizerConfig`, `VoxelizeUseCase.Run`, `VoxelModelBakeUseCase.Bake`.
- Produces:
  - `public static Mesh VoxelizerPreview.BuildMesh(int3 size, NativeArray<byte> voxels, Color32[] palette, float voxelSize, float3 origin)` — грани только между полным и пустым, 32-битные индексы;
  - `public sealed class VoxelizerPreview : IDisposable` с `Render(Rect, ...)` через `PreviewRenderUtility`;
  - меню `Tools/Voxels/Voxelizer`.

- [ ] **Step 1: Failing test**

```csharp
using Game.Voxels.EditorTools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxelizerPreviewTests
    {
        [Test]
        public void ACubeOfThree_HasOnlyItsOuterFaces()
        {
            var voxels = new NativeArray<byte>(27, Allocator.Temp);
            for (int i = 0; i < 27; i++) voxels[i] = 1;
            var palette = new Color32[256];
            palette[1] = new Color32(255, 0, 0, 255);
            Mesh mesh = VoxelizerPreview.BuildMesh(new int3(3, 3, 3), voxels, palette, 0.1f, float3.zero);
            Assert.AreEqual(6 * 9 * 2, mesh.triangles.Length / 3);
            Object.DestroyImmediate(mesh);
            voxels.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: `VoxelizerPreview.BuildMesh`.** Перенести `BuildMesh` из `proto_voxelize.cs` (папка заметок) с правилом обхода:
  - `a = up` для граней ±X, иначе `right`;
  - `b = Cross(n, a)`;
  - углы `(-a-b, a-b, a+b, -a+b) × vs/2` от центра грани;
  - треугольники `(0, 1, 2)`, `(0, 2, 3)`.

  Так передняя сторона смотрит наружу (проверено в пробах).

  Рендер:
  - `PreviewRenderUtility`;
  - материал `Game/Voxel` (после перехода на URP — его URP-версия, имя то же);
  - камера по орбите: углы `yaw` и `pitch` в полях окна, мышь в области превью;
  - `BeginPreview(rect, GUIStyle.none)` → `Render(true)` → `EndAndDrawPreview(rect)`.
- [ ] **Step 4: `VoxelizerWindow`.** Окно IMGUI, `[MenuItem("Tools/Voxels/Voxelizer")]`.
  - **Поле `VoxelizerConfig`** и кнопка «Новый конфиг» (создаёт `<Имя>VoxelizerConfig.asset` рядом с моделью).
  - **Инспектор конфига:** `Editor.CreateEditor(config)` → `OnInspectorGUI`.
  - **Кнопки:**
    - «Пересчитать» — копия модели через `PreviewRenderUtility.InstantiatePrefabInScene`, `VoxelizeUseCase.Run`, меш на каждую часть с её `localToWorld`;
    - «Запечь» — `VoxelModelBakeUseCase.Bake(config, папка модели)`.
  - **Превью:** две панели — исходник и воксели, общая камера.
  - **Строка статуса:** воксели по частям, всего, чанков 16³, время вокселизации.

  Пересчёт только по кнопке, не на каждое изменение. Перед «Запечь» проверить `EditorApplication.isPlaying`: в Play кнопка выключена.
- [ ] **Step 5: Run test, expect PASS.** Проверить окно руками: открыть, взять конфиг танка из Task 9, пересчитать, повращать, запечь в `Assets/Game/Prefabs/Vehicles/` (не коммитить префабы танка без решения пользователя). Снимок окна отправить пользователю.
- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxelizer: the Voxelizer window, preview beside the model without Play, bake button

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Обмен с MagicaVoxel (`.vox`)

**Files:**
- Create: `Assets/Game/Voxels/Runtime/VoxFile.cs`
- Modify: `Assets/Game/Voxels/Editor/Voxelizer/VoxelizerWindow.cs` (кнопки «Экспорт .vox» и «Импорт .vox» по части)
- Test: `Assets/Game/Voxels/Tests/Voxelizer/VoxFileTests.cs`

**Interfaces:**
- Produces:
  - `public static byte[] VoxFile.Write(int3 size, NativeArray<byte> voxels, Color32[] palette)`;
  - `public static bool VoxFile.Read(byte[] file, out int3 size, out byte[] voxels, out Color32[] palette)`.

Формат: заголовок `VOX `, версия 150; чанк `MAIN` с детьми `SIZE`, `XYZI`, `RGBA`.
- В `.vox` ось Z вертикальная, у нас Y: при записи `(x, y, z) → (x, z, y)`, при чтении обратно.
- Палитра `.vox`: элемент `i` (с 0) — цвет индекса `i + 1`.
- Альфа при чтении — 255.
- Если хоть одна ось больше 256, `Write` бросает `ArgumentException` с понятным текстом. Окно тогда пишет: «часть больше 256 вокселей, экспорт по частям невозможен».

- [ ] **Step 1: Failing test**

```csharp
using Game.Voxels;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels.Tests
{
    public sealed class VoxFileTests
    {
        [Test]
        public void WriteThenRead_GivesTheSameGridAndColours()
        {
            int3 size = new int3(5, 7, 3);
            var voxels = new NativeArray<byte>(size.x * size.y * size.z, Allocator.Temp);
            for (int i = 0; i < voxels.Length; i++) voxels[i] = (byte)(i % 4);
            var palette = new Color32[256];
            for (int i = 1; i < 256; i++) palette[i] = new Color32((byte)i, (byte)(255 - i), 7, 255);

            byte[] file = VoxFile.Write(size, voxels, palette);
            Assert.IsTrue(VoxFile.Read(file, out int3 backSize, out byte[] back, out Color32[] backPalette));

            Assert.AreEqual(size, backSize);
            Assert.AreEqual(voxels.ToArray(), back);
            for (int i = 1; i < 256; i++)
                Assert.AreEqual(palette[i].r, backPalette[i].r);
            voxels.Dispose();
        }

        [Test]
        public void Write_RefusesAPartOverTwoHundredFiftySixVoxels()
        {
            var voxels = new NativeArray<byte>(300, Allocator.Temp);
            Assert.Throws<System.ArgumentException>(() => VoxFile.Write(new int3(300, 1, 1), voxels, new Color32[256]));
            voxels.Dispose();
        }
    }
}
```

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implementation** `VoxFile.cs`:

```csharp
using System;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// MagicaVoxel .vox, one model: SIZE, XYZI and RGBA under MAIN. Its Z is up, ours is Y.
    /// Palette entry i of the file is our colour index i + 1. Alpha (our wetness) is read back as dry.
    /// </summary>
    public static class VoxFile
    {
        public const int MAX_SIZE = 256;

        public static byte[] Write(int3 size, NativeArray<byte> voxels, Color32[] palette)
        {
            if (math.any(size > MAX_SIZE))
                throw new ArgumentException($"A .vox model holds at most {MAX_SIZE} voxels a side; this one is {size}.");

            var xyzi = new MemoryStream();
            var w = new BinaryWriter(xyzi);
            int count = 0;
            w.Write(0);
            for (int z = 0; z < size.z; z++)
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                byte v = voxels[x + size.x * (y + size.y * z)];
                if (v == 0) continue;
                w.Write((byte)x); w.Write((byte)z); w.Write((byte)y); w.Write(v);
                count++;
            }

            byte[] xyziBytes = xyzi.ToArray();
            BitConverter.GetBytes(count).CopyTo(xyziBytes, 0);

            var rgba = new byte[256 * 4];
            for (int i = 0; i < 255; i++)
            {
                Color32 c = palette[i + 1];
                rgba[i * 4] = c.r; rgba[i * 4 + 1] = c.g; rgba[i * 4 + 2] = c.b; rgba[i * 4 + 3] = 255;
            }

            var sizeBytes = new byte[12];
            BitConverter.GetBytes(size.x).CopyTo(sizeBytes, 0);
            BitConverter.GetBytes(size.z).CopyTo(sizeBytes, 4);
            BitConverter.GetBytes(size.y).CopyTo(sizeBytes, 8);

            var children = new MemoryStream();
            WriteChunk(children, "SIZE", sizeBytes);
            WriteChunk(children, "XYZI", xyziBytes);
            WriteChunk(children, "RGBA", rgba);
            byte[] childBytes = children.ToArray();

            var file = new MemoryStream();
            var f = new BinaryWriter(file);
            f.Write(Encoding.ASCII.GetBytes("VOX "));
            f.Write(150);
            f.Write(Encoding.ASCII.GetBytes("MAIN"));
            f.Write(0);
            f.Write(childBytes.Length);
            f.Write(childBytes);
            return file.ToArray();
        }

        public static bool Read(byte[] file, out int3 size, out byte[] voxels, out Color32[] palette)
        {
            size = int3.zero;
            voxels = null;
            palette = new Color32[256];
            if (file == null || file.Length < 20 || Encoding.ASCII.GetString(file, 0, 4) != "VOX ")
                return false;

            int at = 8;
            int mainContent = BitConverter.ToInt32(file, at + 4);
            at += 12 + mainContent; // MAIN header and its own content
            byte[] xyzi = null;
            bool hasPalette = false;
            while (at + 12 <= file.Length)
            {
                string id = Encoding.ASCII.GetString(file, at, 4);
                int content = BitConverter.ToInt32(file, at + 4);
                int children = BitConverter.ToInt32(file, at + 8);
                int body = at + 12;
                if (id == "SIZE" && size.x == 0)
                    size = new int3(BitConverter.ToInt32(file, body), BitConverter.ToInt32(file, body + 8), BitConverter.ToInt32(file, body + 4));
                else if (id == "XYZI" && xyzi == null)
                {
                    xyzi = new byte[content];
                    Array.Copy(file, body, xyzi, 0, content);
                }
                else if (id == "RGBA")
                {
                    hasPalette = true;
                    for (int i = 0; i < 255; i++)
                        palette[i + 1] = new Color32(file[body + i * 4], file[body + i * 4 + 1], file[body + i * 4 + 2], 255);
                }

                at = body + content + children;
            }

            if (xyzi == null || math.any(size <= 0))
                return false;
            if (!hasPalette)
                for (int i = 1; i < 256; i++) palette[i] = new Color32(200, 200, 200, 255);

            voxels = new byte[size.x * size.y * size.z];
            int count = BitConverter.ToInt32(xyzi, 0);
            for (int k = 0; k < count; k++)
            {
                int x = xyzi[4 + k * 4], z = xyzi[5 + k * 4], y = xyzi[6 + k * 4];
                if (x < size.x && y < size.y && z < size.z)
                    voxels[x + size.x * (y + size.y * z)] = xyzi[7 + k * 4];
            }

            return true;
        }

        private static void WriteChunk(Stream stream, string id, byte[] content)
        {
            var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes(id));
            w.Write(content.Length);
            w.Write(0);
            w.Write(content);
        }
    }
}
```

Важно: `MAIN` в файлах MagicaVoxel имеет нулевой `content` и детей в `children`. `Read` пропускает только заголовок `MAIN` и его content (0), потом идёт по детям подряд — верно для обоих случаев.

- [ ] **Step 4: Кнопки в окне.**
  - «Экспорт .vox» — по выбранной части: `EditorUtility.SaveFilePanel`, `File.WriteAllBytes`.
  - «Импорт .vox» — `OpenFilePanel` → `Read` → заменяет сетку части в `VX_<Имя>_Data.asset` (новый `runs`, `size`; `origin` остаётся). Палитра: цвета файла сопоставляются ближайшим цветам палитры модели. Новые цвета добавляются в свободные слоты; если их нет — ближайший существующий.
- [ ] **Step 5: Run, expect PASS.** Руками: экспорт части танка, открыть в MagicaVoxel (если стоит у пользователя — спросить), поправить, импорт.
- [ ] **Step 6: Commit**

```bash
git add Assets/Game/Voxels/Runtime/VoxFile.cs* Assets/Game/Voxels/Editor/Voxelizer Assets/Game/Voxels/Tests/Voxelizer
git commit -m "Voxels: MagicaVoxel .vox read and write, export and import a part from the Voxelizer window

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Проверка ветки и отчёт

- [ ] **Step 1:** Все тесты вокселизатора — EditMode `Game.Voxels.Tests` (фильтр сборки `Game.Voxels.VoxelizerTests`) и PlayMode `VoxelModelTests`. Только при чистой сцене.
- [ ] **Step 2:** Ревью кода субагентом (`superpowers:requesting-code-review`) по диффу `game/develop...HEAD`.
- [ ] **Step 3:** `/perf-check` по рабочей копии, `measure: yes, port <PORT>`.
- [ ] **Step 4:** Обновить спецификацию: раздел «Не проверено» и все изменения правил после Task 9.
- [ ] **Step 5:** Отчёт тимлиду (`SendMessage` на `local_9e515c69-9439-4d76-867e-047fd127494e`):
  - что сделано;
  - ветка и коммиты;
  - как проверить (`Tools/Voxels/Voxelizer`, конфиг танка);
  - цифры замера;
  - что не проверено.

## Вне плана

- **Камуфляж и декали на запечённых моделях.** Их код — приватные `CamouflageJob` и `PaintDecals` внутри `VoxelizedModel`. Чтобы не копировать, их нужно вынести в общий файл, а это правка существующего класса — только через тимлида и ядро. Пока запечённая модель берёт цвета из текстуры. Камуфляж — следующей задачей после согласования.
- **Монстры, пропсы, статичный меш, перевод `VoxelStructure`** — по спецификации, раздел 7.

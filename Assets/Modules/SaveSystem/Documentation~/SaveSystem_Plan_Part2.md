# SaveSystem, часть 2 — объекты: план реализации

> **Для исполнителя:** обязательный под-скилл — superpowers:executing-plans (инлайн, задача за задачей). Тесты идут
> через один Unity-эдитор по MCP, и каждая задача меняет его состояние (компиляция, перезагрузка домена), поэтому
> параллельные исполнители друг другу мешали бы. Шаги — чекбоксы (`- [ ]`).

**Цель:** слой объектов плагина — сборка `SaveSystem.Objects`: компонент `Saveable`, скрытый реестр, каталог
сериализаторов компонентов (свои → встроенные → поля `[Save]`), секции сцен (`objects` / `destroyed` / `spawned`),
`$scenes` и `$persistent`, загрузка перезагрузкой сохранённой сцены фазами, `SaveSystemSettings` с каталогами
префабов и сцен, ID в эдиторе, инспекторы и меню — и подключение к игре.

**Архитектура:** ядро (`SaveLoadService`) получает крючок `ISaveExtension`: слой объектов пишет свои секции при
каждом захвате, проверяет файл до применения (`SceneNotFound`, версии) и заканчивает загрузку позже — когда сцена
восстановлена; до этого `IsLoading` и `Save` → `Blocked`, `Loaded` поднимается в конце. `SaveLoadSystem` подключает
слой через ассет настроек (`SaveExtensionAsset` в ядре, его наследник `SaveSystemSettings` — в слое объектов), так
что ядро по-прежнему ничего не знает об объектах. Реестр `Saveable` статический и скрытый; сцену он сканирует на
`sceneLoaded` (с выключенными объектами), слой объектов раздаёт данные: объекты — сразу, данные компонентов — в
позднем `Start` каждого `Saveable`.

**Стек:** Unity 6000.5.6f1, C# 9, Newtonsoft JSON 3.2.2, NUnit (Unity Test Framework), EditMode + PlayMode, MCP.

Спек — [SaveSystem.md](SaveSystem.md), разделы 1 (часть 2), 5.3, 6, 7, 10, 11. План части 1 —
[SaveSystem_Plan.md](SaveSystem_Plan.md); основа — **фактический код части 1** (ветка `feature/save-system`).

## Решения, принятые перед планом (с пользователем, 2026-10-23)

- **Загрузка при старте игры — на месте.** Если сохранённая сцена — та, что только что загрузилась, и в ней ещё
  ничего не стартовало (тот же кадр, ни один `Saveable` не прошёл `Start`), данные применяются к ней без повторной
  загрузки. F9 в игре по-прежнему перезагружает сцену.
- **Новые компоненты сохраняются по умолчанию.** `Saveable` хранит только снятые флажки (`_excluded`); всё, что умеет
  сохраняться и не снято, сохраняется, в том числе добавленное позже.

## Решения плана (в спеке не описаны)

- **Ключи полей `[Save]`** — имя поля как есть (`_current`) или псевдоним из `[Save("старое")]`: под ним поле и
  пишется, и читается, так что переименование поля не меняет ключ. Значения `[Serializable]`-классов — по правилам
  Unity (публичные поля и `[SerializeField]`, без свойств), имена членов — camelCase, как в ядре.
- **Поля `[Save]` со ссылкой на Unity-объект** (`Sprite`, `GameObject`…) в части 2 пропускаются молча: ссылки —
  часть 3. `readonly`, `const`, `static` — тоже пропускаются.
- **`enabled`** пишется в запись компонента (`"enabled"`) у `Behaviour` и `Renderer`; у остальных его нет.
  `activeSelf` объекта — `"active"`, детей с отмеченными компонентами — `"children": { "Visual": true }`.
- **Родитель заспавненного объекта:** ID ближайшего `Saveable`-предка и путь от него (`parent`, `parentPath`); если
  предка-`Saveable` нет — путь от корня сцены (`parentPath: "Holder"`). Путь — имена через `/`, одинаковые имена
  соседей — `Имя#1`, `Имя#2`.
- **Вложенные `Saveable` заспавненного объекта** — в его записи `nested` по пути от корня, со своими ID.
- **Версии компонентов проверяются до применения:** запись компонента новее его сериализатора (по имени типа из
  ключа) — `NewerVersion` для всей загрузки, как у секций. Секция, которую нельзя прочитать, — `Failed`.
- **Проверка сцены:** путь берётся из каталога по GUID (переименование сцены не ломает сейв), иначе — сохранённый
  путь. В эдиторе сцена «есть», если ассет существует (грузится `EditorSceneManager.LoadSceneInPlayMode`), в сборке —
  если она в Build Settings. Сообщение одно: `the saved scene '…' is not in the build`.
- **`LoadScene(path)` на `SaveLoadSystem`** в эту часть не входит: смена сцены с сохранением изменений — `Capture()`
  перед `SceneManager.LoadScene`, как в спеке; метод добавим, когда понадобится игре.
- **Каталог префабов** — префабы, у которых `Saveable` на корне; ID префаба — GUID его ассета, запекается в ассет
  (у варианта — свой). Каталог сцен — все сцены в `Assets/`.

## Что не проверено заранее (эдитор был закрыт, пока писался план)

Если что-то из этого не так — остановиться на задаче, где это всплыло, и обсудить, а не обходить:

- `AddComponent<Saveable>()` в эдиторе вызывает `Reset`/`OnValidate` (на этом стоит автоматическая выдача ID, тест
  `Ids_AddedInEditor_AssignedOnItsOwn`, задача 7).
- `EditorSceneManager.NewScene(…, NewSceneMode.Additive)` работает в EditMode-тестах при открытой сохранённой сцене
  (помощник `TempScene`, задача 2).
- `Time.frameCount` одинаков в `sceneLoaded` и в `Start` первого кадра сцены (загрузка на месте при старте, задача 6).
- `LoadSceneInPlayMode(…, LoadSceneMode.Single)` в PlayMode-тестах не мешает Test Runner (тесты сцен, задачи 5–6).

## Глобальные ограничения

- Модуль — `Assets/Game/Scripts/Modules/SaveSystem/`. Новая сборка `SaveSystem.Objects` (`Runtime/Objects`, namespace
  `Saving.Objects`, ссылка — `SaveSystem`; Newtonsoft подключается сам). Ядро (`SaveSystem`) не ссылается на
  `SaveSystem.Objects`. Модуль не зависит от `Game`, `ServiceLocator` и Odin.
- Тесты слоя объектов — в тех же сборках `SaveSystem.Tests` (EditMode) и `SaveSystem.Tests.Runtime` (PlayMode);
  тестовые компоненты — каждый в своём файле с именем класса (иначе Unity не сохранит их в сцену и префаб).
- Ключи секций: `$scenes`, `$persistent`, `scene/<GUID сцены>`. `formatVersion` остаётся 1, версия секций слоя — 1.
- Стиль: Allman, 4 пробела, `_camelCase` приватные поля, `[SerializeField] private`, `UPPER_CASE` константы.
  Комментарии, `<summary>`, имена и сообщения — **по-английски**. Без `[Min]`, без Odin, без `GetInstanceID()`.
  Не использовать `Object.FindObjectsByType` и `Scene.handle` (на 6000.5 не проверены) — обход сцен через
  `SceneManager.GetSceneAt` / `GetRootGameObjects`, сцена как ключ словаря — сама структура `Scene`.
- TDD: тест → падает по правильной причине → минимальный код → зелёные **все** тесты. На шаге Red новый API —
  заглушками с `throw new NotImplementedException()`. Имена тестов — `Класс_Ситуация_Ожидание`.
- **Коммиты — только с разрешения пользователя;** перед коммитом — сводка. `git add` — только пути из шага.
- Тестовые сцены и префабы модуля создаются командой `Unity_RunCommand` (код — в шаге) и коммитятся; правка
  ассетов Undo не откатывается — откат через git. `Unity_RunCommand` не удаляет ассеты — временное в командах не
  создавать (тесты свои временные ассеты удаляют сами).
- Если код плана не компилируется на 6000.5, API ведёт себя иначе, тест падает не по той причине — остановиться и
  спросить, не обходить молча.

## Как запускать тесты (через MCP)

Как в части 1: выйти из Play Mode, сохранить сцены; после правки файлов — `AssetDatabase.Refresh()` через
`Unity_RunCommand` и проверка консоли на ошибки компиляции; затем
`EditorApplication.ExecuteMenuItem("Tools/Game/Run EditMode Tests")` (или `Run PlayMode Tests` / `Run All Tests`);
дождаться `DONE` в `Temp/GameTestResults.txt` и прочитать файл. Мост отвалился — повторить через ~10 с.

Эдитор — тот, что открыт на этой рабочей копии (`Application.dataPath` проверить первой командой сессии).

## Файлы

```
Assets/Game/Scripts/Modules/SaveSystem/
├── Runtime/Core/                       (сборка SaveSystem)
│   ├── LoadOutcome.cs                   + SceneNotFound
│   ├── ISaveExtension.cs                (новое) крючок слоя: захват, проверка, отложенная загрузка
│   ├── SaveExtensionAsset.cs            (новое) ассет настроек, подключающий слой к SaveLoadSystem
│   ├── ISaveLoadService.cs              комментарий к Loaded
│   ├── SaveLoadService.cs               Attach, TryGetSection, отложенный Loaded
│   └── SaveLoadSystem.cs                поле настроек, UseSettings, Dispose слоя
├── Runtime/Objects/                    (сборка SaveSystem.Objects, namespace Saving.Objects) — всё новое
│   ├── SaveSystem.Objects.asmdef, AssemblyInfo.cs
│   ├── SaveAttribute.cs, ISaveCallbacks.cs
│   ├── IComponentSerializer.cs, ComponentSerializer.cs, SerializerKind.cs
│   ├── ObjectsJson.cs, FieldsSerializer.cs  — поля [Save] по правилам Unity
│   ├── ComponentSerializerCatalog.cs        — поиск сериализаторов и приоритет
│   ├── BuiltIn/TransformSerializer.cs
│   ├── Saveable.cs, KeyedComponent.cs, ComponentKeys.cs
│   ├── ObjectData.cs, SceneData.cs, ObjectState.cs
│   ├── SaveRegistry.cs                      — скрытый реестр, скан сцены
│   ├── SaveSystemSettings.cs                — каталоги префабов и сцен, подключение слоя
│   └── SceneSaving.cs                       — секции сцен, фазы загрузки
├── Editor/                              (сборка SaveSystem.Editor; + ссылка SaveSystem.Objects)
│   ├── SaveableIdAssigner.cs, SaveCatalogBuilder.cs, SaveValidator.cs
│   ├── SaveCatalogPostprocessor.cs, SaveCatalogBuildStep.cs
│   ├── SaveableEditor.cs, SaveSystemSettingsEditor.cs
│   └── SaveSystemMenu.cs                    + Create Settings, Rebuild Catalogs, Validate Scenes
├── Tests/Editor/                        (+ ссылка SaveSystem.Objects)
│   ├── TestDoubles.cs                       + FakeExtension
│   ├── TempScene.cs, TestSerializers.cs
│   ├── Components/  FieldsProbe.cs, FieldsProbeBase.cs, SerializerProbe.cs, PlainProbe.cs, ProbeSerializer.cs
│   ├── SaveExtensionTests.cs, FieldsSerializerTests.cs, ComponentSerializerTests.cs,
│   │   ComponentSerializerCatalogTests.cs, TransformSerializerTests.cs, ObjectStateTests.cs,
│   │   SaveableIdTests.cs, SaveCatalogTests.cs, SaveableEditorTests.cs
└── Tests/Runtime/                       (+ ссылка SaveSystem.Objects)
    ├── FakeExtensionAsset.cs, SaveTestCounter.cs, SaveTestCallbacks.cs, TestScenes.cs
    ├── Scenes/SaveTestScene.unity, Prefabs/SaveTestSpawn.prefab   (создаются командой)
    ├── SaveLoadSystemTests.cs               + тесты настроек
    └── SaveRegistryTests.cs, SceneSavingTests.cs

Assets/Game/Configs/SaveSystemSettings.asset            (новое, командой)
Assets/Game/Prefabs/Services.prefab                     SaveLoadSystem._settings
Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs        не грузить во время загрузки
Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs       IsLoading задаётся
Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs     + тест
Assets/Game/Tests/EditMode/Game.Tests.EditMode.asmdef   + SaveSystem, SaveSystem.Objects, SaveSystem.Editor
Assets/Game/Tests/EditMode/SaveSetupTests.cs            (новое) настройки, каталоги, ID в сценах сборки
Assets/AI/CLAUDE.md, Assets/AI/Docs/SaveSystem.md, Modules/SaveSystem/README.md   документация
```

Пути в задачах — от `Assets/Game/Scripts/Modules/SaveSystem/`, если не сказано иначе.

---

### Задача 1: крючок слоя в ядре

**Files:**
- Modify: `Runtime/Core/LoadOutcome.cs`, `Runtime/Core/ISaveLoadService.cs`, `Runtime/Core/SaveLoadService.cs`,
  `Runtime/Core/SaveLoadSystem.cs`
- Create: `Runtime/Core/ISaveExtension.cs`, `Runtime/Core/SaveExtensionAsset.cs`
- Modify: `Tests/Editor/TestDoubles.cs` (+ `FakeExtension`)
- Create: `Tests/Runtime/FakeExtensionAsset.cs`
- Test: `Tests/Editor/SaveExtensionTests.cs`, `Tests/Runtime/SaveLoadSystemTests.cs` (+ 3 теста)

**Interfaces:**
- Produces: `LoadOutcome.SceneNotFound`; `ISaveExtension : IDisposable { void CaptureInto(IDictionary<string,
  SaveSection>); LoadOutcome Check(IReadOnlyDictionary<string, SaveSection>, out string problem); void
  BeginLoad(Action finished); }`; `abstract class SaveExtensionAsset : ScriptableObject { abstract ISaveExtension
  CreateExtension(SaveLoadService service); }`; `SaveLoadService.Attach(ISaveExtension)`,
  `SaveLoadService.TryGetSection(string, out SaveSection) → bool`; `SaveLoadSystem.UseSettings(SaveExtensionAsset)`.
- Поведение: `Save` и `Capture` после глобальных сериализаторов зовут `CaptureInto` слоя (исключение — ошибка в
  консоль, прежние секции слоя остаются); `Load` после проверки глобальных секций зовёт `Check` (не `Loaded` —
  отказ этим исходом, слот «только для чтения», ничего не применено), затем применяет глобальные секции и зовёт
  `BeginLoad`; пока слой не вызвал `finished`, `IsLoading == true`, `Save` → `Blocked`, `Loaded` ещё не поднят.
  При `Empty` и `Corrupted` слой не вызывается.

- [x] **Шаг 1: типы ядра** (сразу финальные).

  В `Runtime/Core/LoadOutcome.cs` последним значением после `Failed` (у `Failed` поставить запятую):

  ```csharp
          SceneNotFound     // the saved scene is not in the build: nothing applied, slot read-only
  ```

  `Runtime/Core/ISaveExtension.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving
  {
      /// <summary>
      /// A layer plugged into <see cref="SaveLoadService"/> that keeps its own sections, such as the objects layer with
      /// scene sections. It writes them on every capture, checks a file before a load applies anything and finishes a
      /// load later, once the saved scene is restored.
      /// </summary>
      public interface ISaveExtension : IDisposable
      {
          /// <summary>Writes the layer's sections into the snapshot; runs after the global serializers.</summary>
          void CaptureInto(IDictionary<string, SaveSection> sections);

          /// <summary>Checks the file's sections before anything is applied: Loaded when the layer can restore them.</summary>
          LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem);

          /// <summary>Restores the layer after the global sections; calls <paramref name="finished"/> once, maybe later.</summary>
          void BeginLoad(Action finished);
      }
  }
  ```

  `Runtime/Core/SaveExtensionAsset.cs`:

  ```csharp
  using UnityEngine;

  namespace Saving
  {
      /// <summary>A settings asset that plugs a layer into SaveLoadSystem: SaveSystemSettings of the objects layer.</summary>
      public abstract class SaveExtensionAsset : ScriptableObject
      {
          public abstract ISaveExtension CreateExtension(SaveLoadService service);
      }
  }
  ```

  В `Runtime/Core/ISaveLoadService.cs` строку `event Action<LoadOutcome> Loaded;` заменить на:

  ```csharp
          event Action<LoadOutcome> Loaded;            // after the saved scene is restored too
  ```

- [x] **Шаг 2: заглушки.** В `Runtime/Core/SaveLoadService.cs` после метода `AddPlayTime`:

  ```csharp
          public void Attach(ISaveExtension extension) => throw new NotImplementedException();
          public bool TryGetSection(string key, out SaveSection section) => throw new NotImplementedException();
  ```

  В `Runtime/Core/SaveLoadSystem.cs` после метода `Configure`:

  ```csharp
          public void UseSettings(SaveExtensionAsset settings) => throw new NotImplementedException();
  ```

- [x] **Шаг 3: двойники.** В конец namespace в `Tests/Editor/TestDoubles.cs`:

  ```csharp
      /// <summary>A layer with one int under "$fake"; can refuse a load, hold it open or throw on capture.</summary>
      internal sealed class FakeExtension : ISaveExtension
      {
          public const string KEY = "$fake";

          public SaveLoadService Service;
          public int Value;
          public int Restored = -1;
          public int Captures;
          public int Begins;
          public bool Disposed;
          public bool FailCapture;
          public bool FinishAtOnce = true;
          public LoadOutcome CheckResult = LoadOutcome.Loaded;
          public Action Finish;

          public void CaptureInto(IDictionary<string, SaveSection> sections)
          {
              Captures++;
              if (FailCapture)
                  throw new InvalidOperationException("capture broke");

              sections[KEY] = new SaveSection { V = 1, Data = new JValue(Value) };
          }

          public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
          {
              problem = CheckResult == LoadOutcome.Loaded ? null : "the fake layer refused";
              return CheckResult;
          }

          public void BeginLoad(Action finished)
          {
              Begins++;
              Restored = Service.TryGetSection(KEY, out SaveSection section) ? (int)section.Data : -1;
              if (FinishAtOnce)
                  finished();
              else
                  Finish = finished;
          }

          public void Dispose() => Disposed = true;
      }
  ```

  `Tests/Runtime/FakeExtensionAsset.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving.Tests
  {
      /// <summary>Settings asset whose layer only counts captures and remembers being disposed.</summary>
      public sealed class FakeExtensionAsset : SaveExtensionAsset
      {
          public Layer Created { get; private set; }

          public override ISaveExtension CreateExtension(SaveLoadService service) => Created = new Layer();

          public sealed class Layer : ISaveExtension
          {
              public int Captures;
              public bool Disposed;

              public void CaptureInto(IDictionary<string, SaveSection> sections) => Captures++;

              public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
              {
                  problem = null;
                  return LoadOutcome.Loaded;
              }

              public void BeginLoad(Action finished) => finished();

              public void Dispose() => Disposed = true;
          }
      }
  }
  ```

- [x] **Шаг 4: падающие тесты.**

  `Tests/Editor/SaveExtensionTests.cs`:

  ```csharp
  using System.Text.RegularExpressions;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>The layer hook: its sections go through the snapshot, it can refuse a load and finish it later.</summary>
      public class SaveExtensionTests
      {
          private InMemorySaveStorage _storage;

          [SetUp]
          public void SetUp() => _storage = new InMemorySaveStorage();

          private (SaveLoadService Service, FakeExtension Layer) Create()
          {
              var service = new SaveLoadService(_storage, "1.0");
              var layer = new FakeExtension { Service = service };
              service.Attach(layer);
              return (service, layer);
          }

          private void SaveValue(int value, int other = 0)
          {
              (SaveLoadService writer, FakeExtension layer) = Create();
              layer.Value = value;
              writer.Register(new FlakySerializer("other") { Value = other });
              writer.Save();
          }

          [Test]
          public void Extension_SaveThenLoad_RestoresItsSection()
          {
              SaveValue(5);
              (SaveLoadService reader, FakeExtension layer) = Create();

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
              Assert.AreEqual(5, layer.Restored);
          }

          [Test]
          public void Extension_Capture_WritesSnapshotWithoutDisk()
          {
              (SaveLoadService service, FakeExtension layer) = Create();
              layer.Value = 3;

              service.Capture();

              Assert.IsTrue(service.TryGetSection(FakeExtension.KEY, out SaveSection section));
              Assert.AreEqual(3, (int)section.Data);
              Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE));
          }

          [Test]
          public void Extension_CheckRefuses_NothingAppliedAndReadOnly()
          {
              SaveValue(5, other: 7);
              (SaveLoadService reader, FakeExtension layer) = Create();
              layer.CheckResult = LoadOutcome.SceneNotFound;
              var other = new FlakySerializer("other");
              reader.Register(other);

              LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
              Assert.AreEqual(LoadOutcome.SceneNotFound, reader.Load());

              Assert.AreEqual(0, other.Value);
              Assert.AreEqual(0, layer.Begins);
              Assert.IsTrue(reader.IsReadOnly);
          }

          [Test]
          public void Extension_LoadHeldOpen_LoadingUntilFinished()
          {
              SaveValue(5);
              (SaveLoadService reader, FakeExtension layer) = Create();
              layer.FinishAtOnce = false;
              int loadedEvents = 0;
              reader.Loaded += _ => loadedEvents++;

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
              Assert.IsTrue(reader.IsLoading);
              Assert.AreEqual(SaveOutcome.Blocked, reader.Save());
              Assert.AreEqual(0, loadedEvents);

              layer.Finish();

              Assert.IsFalse(reader.IsLoading);
              Assert.AreEqual(1, loadedEvents);
          }

          [Test]
          public void Extension_EmptySlot_NotAskedToLoad()
          {
              (SaveLoadService reader, FakeExtension layer) = Create();

              Assert.AreEqual(LoadOutcome.Empty, reader.Load());
              Assert.AreEqual(0, layer.Begins);
          }

          [Test]
          public void Extension_CaptureThrows_SaveGoesOnWithPreviousSection()
          {
              (SaveLoadService service, FakeExtension layer) = Create();
              layer.Value = 1;
              service.Save();
              layer.Value = 2;
              layer.FailCapture = true;

              LogAssert.Expect(LogType.Error, new Regex("scene sections failed"));
              Assert.AreEqual(SaveOutcome.Saved, service.Save());

              Assert.IsTrue(service.TryGetSection(FakeExtension.KEY, out SaveSection section));
              Assert.AreEqual(1, (int)section.Data);
          }
      }
  }
  ```

  В класс `SaveLoadSystemTests` (`Tests/Runtime/SaveLoadSystemTests.cs`):

  ```csharp
          [Test]
          public void SaveLoadSystem_Settings_LayerCapturesOnSave()
          {
              var settings = ScriptableObject.CreateInstance<FakeExtensionAsset>();
              SaveLoadSystem system = Create();
              system.UseSettings(settings);

              system.Save();

              Assert.AreEqual(1, settings.Created.Captures);
              Object.DestroyImmediate(settings);
          }

          [Test]
          public void SaveLoadSystem_Destroyed_DisposesLayer()
          {
              var settings = ScriptableObject.CreateInstance<FakeExtensionAsset>();
              SaveLoadSystem system = Create();
              system.UseSettings(settings);
              system.Save();

              Object.DestroyImmediate(_object);

              Assert.IsTrue(settings.Created.Disposed);
              Object.DestroyImmediate(settings);
          }

          [Test]
          public void SaveLoadSystem_UseSettingsAfterStart_Throws()
          {
              SaveLoadSystem system = Create();
              _ = system.CurrentSlot; // starts the service

              Assert.Throws<InvalidOperationException>(() => system.UseSettings(null));
          }
  ```

- [x] **Шаг 5: запуск — красный.** `Refresh`, консоль без ошибок компиляции, All Tests. Ожидание: падают 6
  `SaveExtensionTests` и 3 новых `SaveLoadSystemTests` с `NotImplementedException`; остальные зелёные.

- [x] **Шаг 6: реализация `SaveLoadService`.**

  Описание класса (`<summary>`) дописать последним предложением:
  `An attached layer (the objects layer) adds its own sections and may finish a load later, when the saved scene is
  restored: until then the service is loading.`

  Поле после `private string _lastText;`:

  ```csharp
          private ISaveExtension _extension;
  ```

  Заглушки `Attach` и `TryGetSection` заменить на:

  ```csharp
          /// <summary>Plugs a layer in (the objects layer); null takes it out. Not during a load.</summary>
          public void Attach(ISaveExtension extension)
          {
              if (IsLoading)
                  throw new InvalidOperationException("Attach cannot be called while a load is in progress.");

              _extension = extension;
          }

          /// <summary>A section of the current snapshot: the layer reads its scene sections with it.</summary>
          public bool TryGetSection(string key, out SaveSection section) => _sections.TryGetValue(key, out section);
  ```

  Метод `Capture` целиком (с комментарием над ним):

  ```csharp
          /// <summary>Registered serializers and the layer -> snapshot, without writing: call before unloading what they save.</summary>
          public void Capture()
          {
              if (IsLoading)
                  return;

              SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
              CaptureExtension();
          }
  ```

  Метод `Load` целиком:

  ```csharp
          public LoadOutcome Load()
          {
              if (IsLoading)
                  throw new InvalidOperationException("Load cannot be called while a load is in progress.");

              IsLoading = true;
              LoadOutcome outcome;
              bool continues;
              try
              {
                  outcome = LoadCurrentSlot(out continues);
              }
              catch (Exception exception)
              {
                  outcome = Refuse(LoadOutcome.Failed, $"the storage failed: {exception.Message}");
                  continues = false;
              }

              if (continues)
                  BeginExtensionLoad(outcome);
              else
                  FinishLoad(outcome);

              return outcome;
          }
  ```

  В `SaveCurrentSlot` после строки `SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);` добавить
  `CaptureExtension();`.

  `LoadCurrentSlot`: сигнатура — `private LoadOutcome LoadCurrentSlot(out bool continues)`, первой строкой тела —
  `continues = false;`. После цикла `foreach (IGameSerializer serializer in _serializers) { … }` и до
  `if (outcome == LoadOutcome.Recovered)`:

  ```csharp
              if (_extension != null)
              {
                  LoadOutcome check;
                  string refusal;
                  try
                  {
                      check = _extension.Check(file.Sections, out refusal);
                  }
                  catch (Exception exception)
                  {
                      return Refuse(LoadOutcome.Failed, $"checking the saved scenes failed: {exception.Message}");
                  }

                  if (check != LoadOutcome.Loaded)
                      return Refuse(check, refusal);
              }
  ```

  Последние строки метода (`SerializerGroup.RestoreInOrder(prepared);` и `return outcome;`) заменить на:

  ```csharp
              SerializerGroup.RestoreInOrder(prepared);
              continues = _extension != null;
              return outcome;
  ```

  Новые закрытые методы — после `ApplyOnRegister`:

  ```csharp
          private void CaptureExtension()
          {
              if (_extension == null)
                  return;

              try
              {
                  _extension.CaptureInto(_sections);
              }
              catch (Exception exception)
              {
                  Debug.LogError($"Saving the scene sections failed, their previous data is kept: {exception.Message}");
              }
          }

          private void BeginExtensionLoad(LoadOutcome outcome)
          {
              try
              {
                  _extension.BeginLoad(() => FinishLoad(outcome));
              }
              catch (Exception exception)
              {
                  Debug.LogError($"Restoring the saved scene of slot {CurrentSlot} failed: {exception.Message}");
                  FinishLoad(outcome);
              }
          }

          /// <summary>Ends the load once: a second call (a late callback of the layer) does nothing.</summary>
          private void FinishLoad(LoadOutcome outcome)
          {
              if (!IsLoading)
                  return;

              IsLoading = false;
              Loaded?.Invoke(outcome);
          }
  ```

- [x] **Шаг 7: реализация `SaveLoadSystem`.** Описание класса дописать: `A settings asset (SaveSystemSettings)
  plugs in the objects layer.` Поле после `_autoSavePeriod`:

  ```csharp
          [Tooltip("SaveSystemSettings: plugs in scene objects (Saveable) and their catalogs; empty saves only global systems")]
          [SerializeField] private SaveExtensionAsset _settings;
  ```

  Поле после `private SaveLoadService _service;` — `private ISaveExtension _extension;`. Заглушку `UseSettings`
  заменить на:

  ```csharp
          /// <summary>Replaces the settings asset (tests). Only before the service starts.</summary>
          public void UseSettings(SaveExtensionAsset settings)
          {
              EnsureNotStarted(nameof(UseSettings));
              _settings = settings;
          }
  ```

  В `CreateService` перед `return service;`:

  ```csharp
              if (_settings != null)
              {
                  _extension = _settings.CreateExtension(service);
                  service.Attach(_extension);
              }
  ```

  В блок `// ---------- Unity ----------` после `OnApplicationQuit`:

  ```csharp
          private void OnDestroy()
          {
              _extension?.Dispose();
              _extension = null;
          }
  ```

- [x] **Шаг 8: запуск — зелёный.** All Tests: EditMode и PlayMode `failed 0`.

- [x] **Шаг 9: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core Assets/Game/Scripts/Modules/SaveSystem/Tests
  git commit -m "Let a layer add sections and finish loads later"
  ```

---

### Задача 2: сборка объектов, поля `[Save]` и `ComponentSerializer`

**Files:**
- Create: `Runtime/Objects/SaveSystem.Objects.asmdef`, `Runtime/Objects/AssemblyInfo.cs`,
  `Runtime/Objects/SaveAttribute.cs`, `Runtime/Objects/IComponentSerializer.cs`,
  `Runtime/Objects/ComponentSerializer.cs`, `Runtime/Objects/ObjectsJson.cs`, `Runtime/Objects/FieldsSerializer.cs`
- Modify: `Tests/Editor/SaveSystem.Tests.asmdef`, `Tests/Runtime/SaveSystem.Tests.Runtime.asmdef`,
  `Editor/SaveSystem.Editor.asmdef` (+ ссылка `SaveSystem.Objects`)
- Create: `Tests/Editor/TempScene.cs`, `Tests/Editor/Components/FieldsProbeBase.cs`,
  `Tests/Editor/Components/FieldsProbe.cs`, `Tests/Editor/Components/PlainProbe.cs`,
  `Tests/Editor/Components/SerializerProbe.cs`, `Tests/Editor/Components/ProbeSerializer.cs`
- Test: `Tests/Editor/FieldsSerializerTests.cs`, `Tests/Editor/ComponentSerializerTests.cs`

**Interfaces:**
- Consumes: `SaveJson.ToToken`/`FromToken`, `UnityJsonConverters.Create()`, `SaveMigrationException` (часть 1).
- Produces: `[Save(string key = null)]` (`SaveAttribute.Key`); `IComponentSerializer { Type ComponentType; int
  Version; JToken Capture(Component); JToken Upgrade(JToken, int fromVersion); void Restore(Component, JToken); }`;
  `abstract ComponentSerializer<TComponent, TData> where TComponent : Component` (`virtual int Version => 1`,
  `protected abstract TData Serialize(TComponent)`, `protected abstract void Deserialize(TComponent, TData)`,
  `protected virtual JToken Upgrade(JToken, int)`); `internal FieldsSerializer.For(Type) → FieldsSerializer` (null,
  если полей `[Save]` нет); `internal ObjectsJson.ToToken(object)`, `ObjectsJson.FromToken(JToken, Type)`;
  `TempScene` (EditMode: временная сцена, `IDisposable`).

- [x] **Шаг 1: сборки.**

  `Runtime/Objects/SaveSystem.Objects.asmdef`:

  ```json
  {
      "name": "SaveSystem.Objects",
      "rootNamespace": "Saving.Objects",
      "references": [
          "SaveSystem"
      ],
      "includePlatforms": [],
      "excludePlatforms": [],
      "allowUnsafeCode": false,
      "overrideReferences": false,
      "precompiledReferences": [],
      "autoReferenced": true,
      "defineConstraints": [],
      "versionDefines": [],
      "noEngineReferences": false
  }
  ```

  `Runtime/Objects/AssemblyInfo.cs`:

  ```csharp
  using System.Runtime.CompilerServices;

  // The editor (ids, catalogs, inspectors) and the module's tests work with the hidden registry and object state
  [assembly: InternalsVisibleTo("SaveSystem.Editor")]
  [assembly: InternalsVisibleTo("SaveSystem.Tests")]
  [assembly: InternalsVisibleTo("SaveSystem.Tests.Runtime")]
  ```

  В `references` файлов `Tests/Editor/SaveSystem.Tests.asmdef`, `Tests/Runtime/SaveSystem.Tests.Runtime.asmdef` и
  `Editor/SaveSystem.Editor.asmdef` после `"SaveSystem",` добавить `"SaveSystem.Objects",`.

- [x] **Шаг 2: атрибут и контракт** (сразу финальные).

  `Runtime/Objects/SaveAttribute.cs`:

  ```csharp
  using System;

  namespace Saving.Objects
  {
      /// <summary>
      /// Saves the field with its object; fields without it are settings and stay out of the save. The key is the field
      /// name, or the given one: put the former name here after a rename and old saves still read.
      /// </summary>
      [AttributeUsage(AttributeTargets.Field)]
      public sealed class SaveAttribute : Attribute
      {
          public SaveAttribute(string key = null) => Key = key;

          public string Key { get; }
      }
  }
  ```

  `Runtime/Objects/IComponentSerializer.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>Writes and reads the state of one component type.</summary>
      public interface IComponentSerializer
      {
          Type ComponentType { get; }
          int Version { get; }                          // current data version, starts at 1
          JToken Capture(Component component);
          JToken Upgrade(JToken data, int fromVersion); // one step: fromVersion -> fromVersion + 1
          void Restore(Component component, JToken data); // data is already at Version
      }
  }
  ```

- [x] **Шаг 3: заглушки.**

  `Runtime/Objects/ComponentSerializer.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      public abstract class ComponentSerializer<TComponent, TData> : IComponentSerializer where TComponent : Component
      {
          public Type ComponentType => typeof(TComponent);
          public virtual int Version => 1;

          JToken IComponentSerializer.Capture(Component component) => throw new NotImplementedException();
          void IComponentSerializer.Restore(Component component, JToken data) => throw new NotImplementedException();
          JToken IComponentSerializer.Upgrade(JToken data, int fromVersion) => Upgrade(data, fromVersion);

          protected abstract TData Serialize(TComponent component);
          protected abstract void Deserialize(TComponent component, TData data);
          protected virtual JToken Upgrade(JToken data, int fromVersion) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Objects/ObjectsJson.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;

  namespace Saving.Objects
  {
      internal static class ObjectsJson
      {
          public static JToken ToToken(object value) => throw new NotImplementedException();
          public static object FromToken(JToken token, Type type) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Objects/FieldsSerializer.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      internal sealed class FieldsSerializer : IComponentSerializer
      {
          private FieldsSerializer(Type type) => ComponentType = type;

          public Type ComponentType { get; }
          public int Version => 1;

          public static FieldsSerializer For(Type type) => throw new NotImplementedException();

          public JToken Capture(Component component) => throw new NotImplementedException();
          public JToken Upgrade(JToken data, int fromVersion) => throw new NotImplementedException();
          public void Restore(Component component, JToken data) => throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 4: временная сцена и тестовые компоненты.** Каждый компонент — в своём файле с именем класса.

  `Tests/Editor/TempScene.cs`:

  ```csharp
  using System;
  using UnityEditor.SceneManagement;
  using UnityEngine.SceneManagement;

  namespace Saving.Tests
  {
      /// <summary>
      /// An empty additive scene made active for the test: new objects land in it, and closing it leaves the user's
      /// scenes clean (no "save changes?" before the next PlayMode run).
      /// </summary>
      internal sealed class TempScene : IDisposable
      {
          private readonly Scene _previous;

          public TempScene()
          {
              _previous = SceneManager.GetActiveScene();
              Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
              SceneManager.SetActiveScene(Scene);
          }

          public Scene Scene { get; }

          public void Dispose()
          {
              if (_previous.IsValid())
                  SceneManager.SetActiveScene(_previous);

              EditorSceneManager.CloseScene(Scene, removeScene: true);
          }
      }
  }
  ```

  `Tests/Editor/Components/FieldsProbeBase.cs`:

  ```csharp
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>Base of FieldsProbe: an inherited [Save] field.</summary>
      public abstract class FieldsProbeBase : MonoBehaviour
      {
          [Save] protected int _level;

          public int Level
          {
              get => _level;
              set => _level = value;
          }
      }
  }
  ```

  `Tests/Editor/Components/FieldsProbe.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>A script with [Save] fields of every kind the plugin promises to keep, and a setting that it must not.</summary>
      public class FieldsProbe : FieldsProbeBase
      {
          [Serializable]
          public sealed class Inner
          {
              public int A;
              [SerializeField] private int _b;

              public int C { get; set; }

              public int B
              {
                  get => _b;
                  set => _b = value;
              }
          }

          [SerializeField] private float _setting = 0.5f;
          [Save] private int _hp;
          [Save] public string Title;
          [Save("_start")] private Vector2 _startPosition;
          [Save] private readonly int _fixed = 3;
          [Save] public Inner Data = new();
          [Save] public List<int> Items = new();
          [Save] public Dictionary<string, int> Scores = new();

          public float Setting
          {
              get => _setting;
              set => _setting = value;
          }

          public int Hp
          {
              get => _hp;
              set => _hp = value;
          }

          public Vector2 StartPosition
          {
              get => _startPosition;
              set => _startPosition = value;
          }

          public int Fixed => _fixed;
      }
  }
  ```

  `Tests/Editor/Components/PlainProbe.cs`:

  ```csharp
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>A script with nothing to save: no [Save] fields and no serializer.</summary>
      public sealed class PlainProbe : MonoBehaviour
      {
          public int Setting;
      }
  }
  ```

  `Tests/Editor/Components/SerializerProbe.cs`:

  ```csharp
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>A component saved by its own serializer (ProbeSerializer), not by fields.</summary>
      public sealed class SerializerProbe : MonoBehaviour
      {
          public float Speed;
          public string Mode;
      }
  }
  ```

  `Tests/Editor/Components/ProbeSerializer.cs` (публичный конструктор без параметров — каталог найдёт его сам):

  ```csharp
  using Newtonsoft.Json.Linq;
  using Saving.Objects;

  namespace Saving.Tests
  {
      /// <summary>Version 2 of the probe's data: version 1 stored the speed as "spd".</summary>
      public sealed class ProbeSerializer : ComponentSerializer<SerializerProbe, ProbeSerializer.Data>
      {
          public sealed class Data
          {
              public float Speed;
              public string Mode;
          }

          public override int Version => 2;

          protected override Data Serialize(SerializerProbe probe) => new() { Speed = probe.Speed, Mode = probe.Mode };

          protected override void Deserialize(SerializerProbe probe, Data data)
          {
              probe.Speed = data.Speed;
              probe.Mode = data.Mode;
          }

          protected override JToken Upgrade(JToken data, int fromVersion)
          {
              if (fromVersion != 1)
                  return base.Upgrade(data, fromVersion);

              var json = (JObject)data;
              json["speed"] = json["spd"];
              json.Remove("spd");
              return json;
          }
      }
  }
  ```

- [x] **Шаг 5: падающие тесты.**

  `Tests/Editor/FieldsSerializerTests.cs`:

  ```csharp
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>[Save] fields: what is kept, under which key, and that a bad value changes nothing.</summary>
      public class FieldsSerializerTests
      {
          private TempScene _scene;
          private FieldsProbe _probe;

          [SetUp]
          public void SetUp()
          {
              _scene = new TempScene();
              _probe = new GameObject("Probe").AddComponent<FieldsProbe>();
          }

          [TearDown]
          public void TearDown() => _scene.Dispose();

          private static IComponentSerializer Serializer => FieldsSerializer.For(typeof(FieldsProbe));

          [Test]
          public void Fields_PrivatePublicAndInherited_RoundTrip()
          {
              _probe.Hp = 7;
              _probe.Title = "Knight";
              _probe.Level = 3;
              _probe.Items.Add(5);
              _probe.Scores["Boss"] = 2;
              _probe.Data.A = 1;
              _probe.Data.B = 2;

              JToken data = Serializer.Capture(_probe);
              var copy = new GameObject("Copy").AddComponent<FieldsProbe>();
              Serializer.Restore(copy, data);

              Assert.AreEqual(7, copy.Hp);
              Assert.AreEqual("Knight", copy.Title);
              Assert.AreEqual(3, copy.Level);
              CollectionAssert.AreEqual(new[] { 5 }, copy.Items);
              Assert.AreEqual(2, copy.Scores["Boss"]);
              Assert.AreEqual(1, copy.Data.A);
              Assert.AreEqual(2, copy.Data.B);
          }

          [Test]
          public void Fields_Keys_FieldNamesAsIsAndAlias()
          {
              var json = (JObject)Serializer.Capture(_probe);

              Assert.IsNotNull(json["_hp"]);
              Assert.IsNotNull(json["Title"]);
              Assert.IsNotNull(json["_level"]);
              Assert.IsNotNull(json["_start"]);
              Assert.IsNull(json["_startPosition"]);
          }

          [Test]
          public void Fields_UnmarkedAndReadonly_NotSaved()
          {
              var json = (JObject)Serializer.Capture(_probe);

              Assert.IsNull(json["_setting"]);
              Assert.IsNull(json["_fixed"]);
          }

          [Test]
          public void Fields_Alias_ReadsFormerName()
          {
              Serializer.Restore(_probe, JObject.Parse("{ \"_start\": { \"x\": 1, \"y\": 2 } }"));

              Assert.AreEqual(new Vector2(1f, 2f), _probe.StartPosition);
          }

          [Test]
          public void Fields_MissingKey_KeepsValue()
          {
              _probe.Title = "Old";

              Serializer.Restore(_probe, JObject.Parse("{ \"_hp\": 3 }"));

              Assert.AreEqual(3, _probe.Hp);
              Assert.AreEqual("Old", _probe.Title);
          }

          [Test]
          public void Fields_UnknownKey_Ignored() =>
              Assert.DoesNotThrow(() => Serializer.Restore(_probe, JObject.Parse("{ \"gone\": 1 }")));

          [Test]
          public void Fields_WrongType_NothingAssigned()
          {
              _probe.Title = "Old";

              Assert.Catch(() => Serializer.Restore(_probe, JObject.Parse("{ \"Title\": \"New\", \"_hp\": \"not a number\" }")));

              Assert.AreEqual("Old", _probe.Title);
          }

          [Test]
          public void Fields_SerializableClass_FollowsUnityRules()
          {
              JToken inner = Serializer.Capture(_probe)["Data"];

              Assert.IsNotNull(inner["a"]);
              Assert.IsNotNull(inner["_b"]);
              Assert.IsNull(inner["c"], "properties are not saved");
              Assert.IsNull(inner["b"], "properties are not saved");
          }

          [Test]
          public void Fields_TypeWithoutSaveFields_NoSerializer() =>
              Assert.IsNull(FieldsSerializer.For(typeof(PlainProbe)));
      }
  }
  ```

  `Tests/Editor/ComponentSerializerTests.cs`:

  ```csharp
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>The typed component serializer: round trip, type and version, one-step upgrades.</summary>
      public class ComponentSerializerTests
      {
          private TempScene _scene;

          [SetUp]
          public void SetUp() => _scene = new TempScene();

          [TearDown]
          public void TearDown() => _scene.Dispose();

          [Test]
          public void ComponentSerializer_CaptureThenRestore_RoundTrips()
          {
              var source = new GameObject("Source").AddComponent<SerializerProbe>();
              source.Speed = 2.5f;
              source.Mode = "run";
              IComponentSerializer serializer = new ProbeSerializer();

              var copy = new GameObject("Copy").AddComponent<SerializerProbe>();
              serializer.Restore(copy, serializer.Capture(source));

              Assert.AreEqual(2.5f, copy.Speed);
              Assert.AreEqual("run", copy.Mode);
          }

          [Test]
          public void ComponentSerializer_TypeAndVersion()
          {
              IComponentSerializer serializer = new ProbeSerializer();

              Assert.AreEqual(typeof(SerializerProbe), serializer.ComponentType);
              Assert.AreEqual(2, serializer.Version);
          }

          [Test]
          public void ComponentSerializer_Upgrade_OneStep()
          {
              IComponentSerializer serializer = new ProbeSerializer();

              JToken upgraded = serializer.Upgrade(JObject.Parse("{ \"spd\": 3 }"), 1);

              Assert.AreEqual(3, (int)upgraded["speed"]);
          }

          [Test]
          public void ComponentSerializer_NoStep_ThrowsMigrationException() =>
              Assert.Throws<SaveMigrationException>(() => ((IComponentSerializer)new ProbeSerializer()).Upgrade(new JObject(), 5));
      }
  }
  ```

- [x] **Шаг 6: запуск — красный.** EditMode: падают 9 `FieldsSerializerTests` (`NotImplementedException` из
  `FieldsSerializer.For`) и 2 `ComponentSerializerTests`: `CaptureThenRestore_RoundTrips` (`NotImplementedException`)
  и `NoStep_ThrowsMigrationException` (`NotImplementedException` вместо `SaveMigrationException`). `TypeAndVersion` и
  `Upgrade_OneStep` зелёные сразу: тип, версия и шаг миграции заданы без заглушек.

- [x] **Шаг 7: реализация.**

  `Runtime/Objects/ComponentSerializer.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Typed serializer of one component type, found by the plugin on its own: the subclass turns the component into
      /// a data object and back, and wins over the built-in serializer and over [Save] fields. Bump Version when the
      /// data changes shape and override Upgrade with one step per version. Needs a public parameterless constructor.
      /// </summary>
      public abstract class ComponentSerializer<TComponent, TData> : IComponentSerializer where TComponent : Component
      {
          public Type ComponentType => typeof(TComponent);
          public virtual int Version => 1;

          JToken IComponentSerializer.Capture(Component component) => SaveJson.ToToken(Serialize((TComponent)component));

          void IComponentSerializer.Restore(Component component, JToken data) =>
              Deserialize((TComponent)component, SaveJson.FromToken<TData>(data));

          JToken IComponentSerializer.Upgrade(JToken data, int fromVersion) => Upgrade(data, fromVersion);

          protected abstract TData Serialize(TComponent component);
          protected abstract void Deserialize(TComponent component, TData data);

          /// <summary>One migration step: data written by version <paramref name="fromVersion"/> to the next one.</summary>
          protected virtual JToken Upgrade(JToken data, int fromVersion) =>
              throw new SaveMigrationException(typeof(TComponent).Name, fromVersion);
      }
  }
  ```

  `Runtime/Objects/ObjectsJson.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using Newtonsoft.Json.Serialization;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// JSON for values of [Save] fields: the SaveJson setup, except that classes follow Unity's rules (public and
      /// [SerializeField] fields, no properties), so a [Serializable] class keeps what the Inspector shows.
      /// </summary>
      internal static class ObjectsJson
      {
          public static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
          {
              ContractResolver = new UnityFieldsResolver(),
              ObjectCreationHandling = ObjectCreationHandling.Replace,
              TypeNameHandling = TypeNameHandling.None,
              DateParseHandling = DateParseHandling.None,
              Converters = UnityJsonConverters.Create()
          });

          public static JToken ToToken(object value) =>
              value == null ? JValue.CreateNull() : JToken.FromObject(value, Serializer);

          public static object FromToken(JToken token, Type type)
          {
              if (token == null || token.Type == JTokenType.Null)
                  return type.IsValueType ? Activator.CreateInstance(type) : null;

              return token.ToObject(type, Serializer);
          }

          private sealed class UnityFieldsResolver : DefaultContractResolver
          {
              private const BindingFlags FIELDS =
                  BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

              public UnityFieldsResolver() =>
                  NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: true);

              protected override List<MemberInfo> GetSerializableMembers(Type objectType)
              {
                  var members = new List<MemberInfo>();
                  for (Type current = objectType; current != null && current != typeof(object); current = current.BaseType)
                  {
                      foreach (FieldInfo field in current.GetFields(FIELDS))
                      {
                          if (field.IsInitOnly || field.IsDefined(typeof(NonSerializedAttribute)))
                              continue;

                          if (field.IsPublic || field.IsDefined(typeof(SerializeField)))
                              members.Add(field);
                      }
                  }

                  return members;
              }

              protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
              {
                  JsonProperty property = base.CreateProperty(member, memberSerialization);
                  // Private [SerializeField] fields are read and written too
                  property.Readable = true;
                  property.Writable = true;
                  return property;
              }
          }
      }
  }
  ```

  `Runtime/Objects/FieldsSerializer.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Reflection;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Saves the [Save] fields of one component type, inherited ones too, under their names or keys. Field metadata
      /// comes from reflection once per type. On load every present field is converted first and assigned only when
      /// all of them could be read: a field whose type changed leaves the component as it was.
      /// </summary>
      internal sealed class FieldsSerializer : IComponentSerializer
      {
          private const BindingFlags FIELDS =
              BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

          private static readonly Dictionary<Type, FieldsSerializer> Cache = new();

          private readonly List<(string Key, FieldInfo Field)> _fields;

          private FieldsSerializer(Type type, List<(string Key, FieldInfo Field)> fields)
          {
              ComponentType = type;
              _fields = fields;
          }

          public Type ComponentType { get; }
          public int Version => 1;

          /// <summary>The serializer of a type, or null when it has no [Save] field to save.</summary>
          public static FieldsSerializer For(Type type)
          {
              if (type == null)
                  return null;

              if (!Cache.TryGetValue(type, out FieldsSerializer serializer))
              {
                  serializer = new FieldsSerializer(type, FindFields(type));
                  Cache.Add(type, serializer);
              }

              return serializer._fields.Count > 0 ? serializer : null;
          }

          public JToken Capture(Component component)
          {
              var json = new JObject();
              foreach ((string key, FieldInfo field) in _fields)
                  json[key] = ObjectsJson.ToToken(field.GetValue(component));

              return json;
          }

          public JToken Upgrade(JToken data, int fromVersion) =>
              throw new SaveMigrationException(ComponentType.Name, fromVersion);

          public void Restore(Component component, JToken data)
          {
              if (data is not JObject json)
                  throw new FormatException($"the [Save] fields of {ComponentType.Name} are not a JSON object");

              var values = new List<(FieldInfo Field, object Value)>();
              foreach ((string key, FieldInfo field) in _fields)
              {
                  if (json.TryGetValue(key, out JToken token))
                      values.Add((field, ObjectsJson.FromToken(token, field.FieldType)));
              }

              foreach ((FieldInfo field, object value) in values)
                  field.SetValue(component, value);
          }

          private static List<(string Key, FieldInfo Field)> FindFields(Type type)
          {
              var fields = new List<(string Key, FieldInfo Field)>();
              var keys = new HashSet<string>();
              // The type itself first: a key redeclared in a subclass wins over its base
              for (Type current = type;
                   current != null && current != typeof(MonoBehaviour) && current != typeof(Component);
                   current = current.BaseType)
              {
                  foreach (FieldInfo field in current.GetFields(FIELDS))
                  {
                      var attribute = field.GetCustomAttribute<SaveAttribute>();
                      // readonly and Unity references (part 3) are skipped; static never comes with these flags
                      if (attribute == null || field.IsInitOnly || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                          continue;

                      string key = attribute.Key ?? field.Name;
                      if (keys.Add(key))
                          fields.Add((key, field));
                      else
                          Debug.LogError($"[Save] key '{key}' is used twice in {type.Name}; {current.Name}.{field.Name} is skipped.");
                  }
              }

              return fields;
          }
      }
  }
  ```

- [x] **Шаг 8: запуск — зелёный.** EditMode: `failed 0`.

- [x] **Шаг 9: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add the objects assembly with [Save] fields and component serializers"
  ```

---

### Задача 3: каталог сериализаторов и встроенный `Transform`

**Files:**
- Create: `Runtime/Objects/SerializerKind.cs`, `Runtime/Objects/ComponentSerializerCatalog.cs`,
  `Runtime/Objects/BuiltIn/TransformSerializer.cs`
- Create: `Tests/Editor/TestSerializers.cs`
- Test: `Tests/Editor/ComponentSerializerCatalogTests.cs`, `Tests/Editor/TransformSerializerTests.cs`

**Interfaces:**
- Consumes: `IComponentSerializer`, `ComponentSerializer<,>`, `FieldsSerializer.For` (задача 2).
- Produces: `SerializerKind { None, Own, BuiltIn, Fields }`; `ComponentSerializerCatalog(IEnumerable<IComponentSerializer>
  own, IEnumerable<IComponentSerializer> builtIn)`, `static ComponentSerializerCatalog Default`, `IComponentSerializer
  For(Type)`, `SerializerKind KindOf(Type)`, `int VersionOf(string typeName)`; `TransformSerializer` (публичный,
  встроенный: `localPosition`, `localRotation`, `localScale`).
- Правила: свой сериализатор (из любой сборки, ссылающейся на `SaveSystem.Objects`) → встроенный (из самой
  `SaveSystem.Objects`) → поля `[Save]`; сериализатор базового типа обслуживает наследников, если нет более близкого;
  два своих на один тип — ошибка в консоль, берётся первый по полному имени класса. Находится только класс с
  публичным конструктором без параметров. `VersionOf` — версия своего или встроенного сериализатора типа с таким
  коротким именем, иначе 1.

- [x] **Шаг 1: перечисление** — `Runtime/Objects/SerializerKind.cs` (финальное):

  ```csharp
  namespace Saving.Objects
  {
      /// <summary>Who saves a component type: what the Saveable inspector shows next to it.</summary>
      public enum SerializerKind
      {
          None,     // nothing to save
          Own,      // a ComponentSerializer of the game
          BuiltIn,  // one of the plugin's serializers
          Fields    // [Save] fields
      }
  }
  ```

- [x] **Шаг 2: заглушки.**

  `Runtime/Objects/ComponentSerializerCatalog.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving.Objects
  {
      public sealed class ComponentSerializerCatalog
      {
          public ComponentSerializerCatalog(IEnumerable<IComponentSerializer> own, IEnumerable<IComponentSerializer> builtIn)
          {
          }

          public static ComponentSerializerCatalog Default => throw new NotImplementedException();

          public IComponentSerializer For(Type componentType) => throw new NotImplementedException();
          public SerializerKind KindOf(Type componentType) => throw new NotImplementedException();
          public int VersionOf(string typeName) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Objects/BuiltIn/TransformSerializer.cs`:

  ```csharp
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class TransformSerializer : ComponentSerializer<Transform, TransformSerializer.Data>
      {
          public sealed class Data
          {
              public Vector3 LocalPosition;
              public Quaternion LocalRotation = Quaternion.identity;
              public Vector3 LocalScale = Vector3.one;
          }

          protected override Data Serialize(Transform transform) => throw new NotImplementedException();
          protected override void Deserialize(Transform transform, Data data) => throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 3: двойник** — `Tests/Editor/TestSerializers.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>
      /// A serializer for any type, told apart by its tag; can throw on capture. Its constructor takes arguments, so
      /// the catalog never discovers it on its own: tests hand it in explicitly.
      /// </summary>
      internal sealed class TaggedSerializer : IComponentSerializer
      {
          public TaggedSerializer(Type componentType, string tag = "", bool failCapture = false)
          {
              ComponentType = componentType;
              Tag = tag;
              FailCapture = failCapture;
          }

          public Type ComponentType { get; }
          public int Version => 1;
          public string Tag { get; }
          public bool FailCapture { get; set; }
          public string RestoredWith { get; private set; }

          public JToken Capture(Component component) =>
              FailCapture ? throw new InvalidOperationException("capture broke") : new JValue(Tag);

          public JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(ComponentType.Name, fromVersion);

          public void Restore(Component component, JToken data) => RestoredWith = (string)data;
      }
  }
  ```

- [x] **Шаг 4: падающие тесты.**

  `Tests/Editor/ComponentSerializerCatalogTests.cs`:

  ```csharp
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>Which serializer a component type gets: own, then built-in, then [Save] fields; discovery on its own.</summary>
      public class ComponentSerializerCatalogTests
      {
          private static ComponentSerializerCatalog Catalog(IComponentSerializer[] own, IComponentSerializer[] builtIn) =>
              new(own, builtIn);

          [Test]
          public void Catalog_OwnSerializer_BeatsBuiltIn()
          {
              var own = new TaggedSerializer(typeof(Transform), "own");
              ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[] { own },
                  new IComponentSerializer[] { new TransformSerializer() });

              Assert.AreSame(own, catalog.For(typeof(Transform)));
              Assert.AreEqual(SerializerKind.Own, catalog.KindOf(typeof(Transform)));
          }

          [Test]
          public void Catalog_BuiltIn_BeatsSaveFields()
          {
              var builtIn = new TaggedSerializer(typeof(FieldsProbe), "built-in");
              ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[0], new IComponentSerializer[] { builtIn });

              Assert.AreSame(builtIn, catalog.For(typeof(FieldsProbe)));
              Assert.AreEqual(SerializerKind.BuiltIn, catalog.KindOf(typeof(FieldsProbe)));
          }

          [Test]
          public void Catalog_OnlySaveFields_UsesFields()
          {
              ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[0], new IComponentSerializer[0]);

              Assert.AreEqual(typeof(FieldsProbe), catalog.For(typeof(FieldsProbe)).ComponentType);
              Assert.AreEqual(SerializerKind.Fields, catalog.KindOf(typeof(FieldsProbe)));
          }

          [Test]
          public void Catalog_NothingToSave_None()
          {
              ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[0], new IComponentSerializer[0]);

              Assert.IsNull(catalog.For(typeof(PlainProbe)));
              Assert.AreEqual(SerializerKind.None, catalog.KindOf(typeof(PlainProbe)));
          }

          [Test]
          public void Catalog_BaseTypeSerializer_ServesSubclass()
          {
              var forBase = new TaggedSerializer(typeof(FieldsProbeBase), "base");
              ComponentSerializerCatalog catalog = Catalog(new IComponentSerializer[] { forBase }, new IComponentSerializer[0]);

              Assert.AreSame(forBase, catalog.For(typeof(FieldsProbe)));
          }

          [Test]
          public void Catalog_Default_FindsOwnAndBuiltInSerializers()
          {
              Assert.IsInstanceOf<ProbeSerializer>(ComponentSerializerCatalog.Default.For(typeof(SerializerProbe)));
              Assert.IsInstanceOf<TransformSerializer>(ComponentSerializerCatalog.Default.For(typeof(Transform)));
          }

          [Test]
          public void Catalog_VersionOf_ByTypeNameOrOne()
          {
              Assert.AreEqual(2, ComponentSerializerCatalog.Default.VersionOf(nameof(SerializerProbe)));
              Assert.AreEqual(1, ComponentSerializerCatalog.Default.VersionOf("NoSuchComponent"));
          }
      }
  }
  ```

  `Tests/Editor/TransformSerializerTests.cs`:

  ```csharp
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>The built-in Transform serializer keeps the local pose.</summary>
      public class TransformSerializerTests
      {
          private TempScene _scene;

          [SetUp]
          public void SetUp() => _scene = new TempScene();

          [TearDown]
          public void TearDown() => _scene.Dispose();

          [Test]
          public void TransformSerializer_LocalPose_RoundTrips()
          {
              Transform transform = new GameObject("Moved").transform;
              transform.localPosition = new Vector3(1f, 2f, 3f);
              transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
              transform.localScale = new Vector3(2f, 2f, 1f);
              IComponentSerializer serializer = new TransformSerializer();
              var data = serializer.Capture(transform);

              transform.localPosition = Vector3.zero;
              transform.localRotation = Quaternion.identity;
              transform.localScale = Vector3.one;
              serializer.Restore(transform, data);

              Assert.AreEqual(new Vector3(1f, 2f, 3f), transform.localPosition);
              Assert.AreEqual(Quaternion.Euler(0f, 0f, 45f), transform.localRotation);
              Assert.AreEqual(new Vector3(2f, 2f, 1f), transform.localScale);
              Assert.IsNotNull(data["localPosition"]);
          }
      }
  }
  ```

- [x] **Шаг 5: запуск — красный.** EditMode: падают 7 `ComponentSerializerCatalogTests` и
  `TransformSerializer_LocalPose_RoundTrips` с `NotImplementedException`.

- [x] **Шаг 6: реализация.**

  `Runtime/Objects/BuiltIn/TransformSerializer.cs`:

  ```csharp
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>Built in: local position, rotation and scale. The parent stays as in the scene.</summary>
      public sealed class TransformSerializer : ComponentSerializer<Transform, TransformSerializer.Data>
      {
          public sealed class Data
          {
              public Vector3 LocalPosition;
              public Quaternion LocalRotation = Quaternion.identity;
              public Vector3 LocalScale = Vector3.one;
          }

          protected override Data Serialize(Transform transform) => new()
          {
              LocalPosition = transform.localPosition,
              LocalRotation = transform.localRotation,
              LocalScale = transform.localScale
          };

          protected override void Deserialize(Transform transform, Data data)
          {
              transform.localPosition = data.LocalPosition;
              transform.localRotation = data.LocalRotation;
              transform.localScale = data.LocalScale;
          }
      }
  }
  ```

  `Runtime/Objects/ComponentSerializerCatalog.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Component type -> serializer. <see cref="Default"/> finds every IComponentSerializer with a public
      /// parameterless constructor in the assemblies that reference SaveSystem.Objects. Priority: own serializer,
      /// built-in (this assembly), [Save] fields. A serializer of a base type serves its subclasses unless a closer
      /// one exists.
      /// </summary>
      public sealed class ComponentSerializerCatalog
      {
          private static ComponentSerializerCatalog _default;

          private readonly Dictionary<Type, IComponentSerializer> _own = new();
          private readonly Dictionary<Type, IComponentSerializer> _builtIn = new();
          private readonly Dictionary<Type, IComponentSerializer> _resolved = new();

          public ComponentSerializerCatalog(IEnumerable<IComponentSerializer> own, IEnumerable<IComponentSerializer> builtIn)
          {
              Add(own, _own);
              Add(builtIn, _builtIn);
          }

          public static ComponentSerializerCatalog Default => _default ??= Discover();

          public IComponentSerializer For(Type componentType)
          {
              if (componentType == null)
                  return null;

              if (!_resolved.TryGetValue(componentType, out IComponentSerializer serializer))
              {
                  serializer = Closest(_own, componentType) ?? Closest(_builtIn, componentType) ??
                               FieldsSerializer.For(componentType);
                  _resolved.Add(componentType, serializer); // a null answer is cached too
              }

              return serializer;
          }

          public SerializerKind KindOf(Type componentType)
          {
              IComponentSerializer serializer = For(componentType);
              if (serializer == null)
                  return SerializerKind.None;

              if (serializer is FieldsSerializer)
                  return SerializerKind.Fields;

              return _own.ContainsValue(serializer) ? SerializerKind.Own : SerializerKind.BuiltIn;
          }

          /// <summary>The data version written for a component type name; 1 for [Save] fields and unknown names.</summary>
          public int VersionOf(string typeName)
          {
              foreach (Dictionary<Type, IComponentSerializer> serializers in new[] { _own, _builtIn })
              {
                  foreach (KeyValuePair<Type, IComponentSerializer> pair in serializers)
                  {
                      if (pair.Key.Name == typeName)
                          return pair.Value.Version;
                  }
              }

              return 1;
          }

          private static IComponentSerializer Closest(Dictionary<Type, IComponentSerializer> serializers, Type type)
          {
              for (Type current = type; current != null; current = current.BaseType)
              {
                  if (serializers.TryGetValue(current, out IComponentSerializer serializer))
                      return serializer;
              }

              return null;
          }

          private static void Add(IEnumerable<IComponentSerializer> serializers, Dictionary<Type, IComponentSerializer> into)
          {
              if (serializers == null)
                  return;

              foreach (IComponentSerializer serializer in serializers
                           .Where(serializer => serializer?.ComponentType != null)
                           .OrderBy(serializer => serializer.GetType().FullName, StringComparer.Ordinal))
              {
                  if (!into.TryAdd(serializer.ComponentType, serializer))
                      Debug.LogError($"Two serializers for {serializer.ComponentType.Name}: " +
                                     $"{into[serializer.ComponentType].GetType().Name} is used, {serializer.GetType().Name} is not.");
              }
          }

          private static ComponentSerializerCatalog Discover()
          {
              Assembly own = typeof(ComponentSerializerCatalog).Assembly;
              string ownName = own.GetName().Name;
              var game = new List<IComponentSerializer>();
              var builtIn = new List<IComponentSerializer>();

              foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
              {
                  if (assembly != own && assembly.GetReferencedAssemblies().All(reference => reference.Name != ownName))
                      continue;

                  foreach (Type type in TypesOf(assembly))
                  {
                      if (type.IsAbstract || type.IsGenericTypeDefinition || !typeof(IComponentSerializer).IsAssignableFrom(type)
                          || type.GetConstructor(Type.EmptyTypes) == null)
                          continue;

                      try
                      {
                          (assembly == own ? builtIn : game).Add((IComponentSerializer)Activator.CreateInstance(type));
                      }
                      catch (Exception exception)
                      {
                          Debug.LogError($"The component serializer {type.Name} could not be created: {exception.Message}");
                      }
                  }
              }

              return new ComponentSerializerCatalog(game, builtIn);
          }

          private static IEnumerable<Type> TypesOf(Assembly assembly)
          {
              try
              {
                  return assembly.GetTypes();
              }
              catch (ReflectionTypeLoadException exception)
              {
                  return exception.Types.Where(type => type != null);
              }
          }
      }
  }
  ```

- [x] **Шаг 7: запуск — зелёный.** EditMode: `failed 0`.

- [x] **Шаг 8: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Find component serializers on their own and add the Transform one"
  ```

---

### Задача 4: `Saveable`, ключи компонентов и состояние объекта

**Files:**
- Create: `Runtime/Objects/Saveable.cs`, `Runtime/Objects/KeyedComponent.cs`, `Runtime/Objects/ComponentKeys.cs`,
  `Runtime/Objects/ObjectData.cs`, `Runtime/Objects/ObjectState.cs`
- Test: `Tests/Editor/ObjectStateTests.cs`

**Interfaces:**
- Consumes: `ComponentSerializerCatalog`, `IComponentSerializer`, `TransformSerializer` (задача 3).
- Produces:
  - `Saveable : MonoBehaviour` (`[DisallowMultipleComponent]`, `[DefaultExecutionOrder(32000)]`): `string Id`,
    `string PrefabId`, `bool IsSpawned { get; internal set; }`, `bool IsSaved(string key)`, `void SetSaved(string key,
    bool saved)`; внутреннее — `IsRegistered`, `HasStarted`, `SetId(string)`. Поля `_id`, `_prefabId`, `_excluded`.
  - `KeyedComponent { string Key; string Path; Component Component; }`.
  - `ComponentKeys.Collect(Saveable) → List<KeyedComponent>`, `ComponentKeys.FindChild(Transform root, string path)
    → Transform`, `ComponentKeys.PathOf(Transform root, Transform node) → string`, `ComponentKeys.ScenePathOf(Transform)
    → string`, `ComponentKeys.FindInScene(Scene, string path) → Transform`, `ComponentKeys.TypeNameOf(string key) → string`.
  - `ObjectData { bool Active; Dictionary<string, bool> Children; Dictionary<string, ComponentData> Components;
    CopyFrom(ObjectData) }`, `ComponentData { int V; bool? Enabled; JToken Data; }`.
  - `ObjectState.Capture(Saveable, ComponentSerializerCatalog, ObjectData previous = null) → ObjectData`,
    `ObjectState.RestoreActive(Saveable, ObjectData)`, `ObjectState.RestoreComponents(Saveable, ObjectData,
    ComponentSerializerCatalog)`.
- Ключи: путь от `Saveable` до объекта компонента (имена через `/`, повтор имени среди соседей — `Имя#1`) и имя типа
  (`Transform`, `Visual/SpriteRenderer`, второй одного типа — `BoxCollider2D#1`). Обход детей останавливается на
  вложенном `Saveable`. В список попадают компоненты, для которых у каталога есть сериализатор и которые не сняты.

- [x] **Шаг 1: данные** (финальные).

  `Runtime/Objects/KeyedComponent.cs`:

  ```csharp
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>A component of a Saveable with its key and the path of its object from the Saveable ("" for its own).</summary>
      internal readonly struct KeyedComponent
      {
          public KeyedComponent(string key, string path, Component component)
          {
              Key = key;
              Path = path;
              Component = component;
          }

          public string Key { get; }
          public string Path { get; }
          public Component Component { get; }
      }
  }
  ```

  `Runtime/Objects/ObjectData.cs`:

  ```csharp
  using System.Collections.Generic;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;

  namespace Saving.Objects
  {
      /// <summary>A Saveable in a scene section: its active flag, its children's and the data of its components by key.</summary>
      internal class ObjectData
      {
          public bool Active = true;
          public Dictionary<string, bool> Children = new();
          public Dictionary<string, ComponentData> Components = new();

          public void CopyFrom(ObjectData other)
          {
              Active = other.Active;
              Children = other.Children;
              Components = other.Components;
          }
      }

      /// <summary>One component: its data version, its enabled flag (for components that have one) and its data.</summary>
      internal sealed class ComponentData
      {
          public int V;
          [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
          public bool? Enabled;
          public JToken Data;
      }
  }
  ```

  `Runtime/Objects/Saveable.cs` (жизненный цикл добавит задача 5, хуки эдитора — задача 7):

  ```csharp
  using System.Collections.Generic;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Marks an object to save: its id, its prefab and which components of it and of its children (down to the next
      /// Saveable, which saves itself) are kept. Everything that can be saved is, unless unchecked in the Inspector, so
      /// a component added later is saved too. Scene objects get their id in the editor; objects made in play
      /// (Instantiate, copies of scene objects too) get a new one in Awake and count as spawned.
      /// </summary>
      [DisallowMultipleComponent]
      [DefaultExecutionOrder(32000)]
      public sealed class Saveable : MonoBehaviour
      {
          [SerializeField] private string _id;
          [SerializeField] private string _prefabId;
          [Tooltip("Keys of the components that are not saved; everything else that can be saved is")]
          [SerializeField] private List<string> _excluded = new();

          public string Id => _id;
          public string PrefabId => _prefabId;

          /// <summary>Made in play, not loaded with its scene: saved as spawned, restored from its prefab.</summary>
          public bool IsSpawned { get; internal set; }

          internal bool IsRegistered { get; set; }
          internal bool HasStarted { get; private set; }

          public bool IsSaved(string componentKey) => !_excluded.Contains(componentKey);

          /// <summary>Checks or unchecks a component by its key; everything is checked by default.</summary>
          public void SetSaved(string componentKey, bool saved)
          {
              if (saved)
                  _excluded.Remove(componentKey);
              else if (!_excluded.Contains(componentKey))
                  _excluded.Add(componentKey);
          }

          internal void SetId(string id) => _id = id;
      }
  }
  ```

- [x] **Шаг 2: заглушки.**

  `Runtime/Objects/ComponentKeys.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  namespace Saving.Objects
  {
      internal static class ComponentKeys
      {
          public static List<KeyedComponent> Collect(Saveable root) => throw new NotImplementedException();
          public static Transform FindChild(Transform root, string path) => throw new NotImplementedException();
          public static string PathOf(Transform root, Transform node) => throw new NotImplementedException();
          public static string ScenePathOf(Transform node) => throw new NotImplementedException();
          public static Transform FindInScene(Scene scene, string path) => throw new NotImplementedException();
          public static string TypeNameOf(string key) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Objects/ObjectState.cs`:

  ```csharp
  using System;

  namespace Saving.Objects
  {
      internal static class ObjectState
      {
          public static ObjectData Capture(Saveable saveable, ComponentSerializerCatalog catalog, ObjectData previous = null) =>
              throw new NotImplementedException();

          public static void RestoreActive(Saveable saveable, ObjectData data) => throw new NotImplementedException();

          public static void RestoreComponents(Saveable saveable, ObjectData data, ComponentSerializerCatalog catalog) =>
              throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 3: падающие тесты** — `Tests/Editor/ObjectStateTests.cs`:

  ```csharp
  using System.Linq;
  using System.Text.RegularExpressions;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>Component keys of a Saveable and the round trip of one object's state.</summary>
      public class ObjectStateTests
      {
          private TempScene _scene;

          [SetUp]
          public void SetUp() => _scene = new TempScene();

          [TearDown]
          public void TearDown() => _scene.Dispose();

          private static Saveable MakeSaveable(string name, out FieldsProbe probe)
          {
              var root = new GameObject(name);
              var saveable = root.AddComponent<Saveable>();
              saveable.SetId(System.Guid.NewGuid().ToString("N"));
              probe = root.AddComponent<FieldsProbe>();
              return saveable;
          }

          private static GameObject Child(GameObject parent, string name)
          {
              var child = new GameObject(name);
              child.transform.SetParent(parent.transform, false);
              return child;
          }

          private static ComponentSerializerCatalog Catalog => ComponentSerializerCatalog.Default;

          [Test]
          public void Keys_PathsTypesAndRepeats()
          {
              Saveable saveable = MakeSaveable("Root", out _);
              saveable.gameObject.AddComponent<BoxCollider2D>();
              saveable.gameObject.AddComponent<BoxCollider2D>();
              Child(saveable.gameObject, "Visual").AddComponent<SpriteRenderer>();
              Child(saveable.gameObject, "Visual");
              GameObject nested = Child(saveable.gameObject, "Nested");
              nested.AddComponent<Saveable>();
              Child(nested, "Deep");

              string[] keys = ComponentKeys.Collect(saveable).Select(entry => entry.Key).ToArray();

              CollectionAssert.IsSubsetOf(new[]
              {
                  "Transform", "FieldsProbe", "BoxCollider2D", "BoxCollider2D#1", "Visual/Transform",
                  "Visual/SpriteRenderer", "Visual#1/Transform"
              }, keys);
              Assert.IsFalse(keys.Any(key => key.StartsWith("Nested")), "a nested Saveable saves itself");
              Assert.IsFalse(keys.Contains("Saveable"));
          }

          [Test]
          public void Keys_FindChild_FollowsRepeatedNames()
          {
              Saveable saveable = MakeSaveable("Root", out _);
              Child(saveable.gameObject, "Visual");
              GameObject second = Child(saveable.gameObject, "Visual");

              Assert.AreSame(second.transform, ComponentKeys.FindChild(saveable.transform, "Visual#1"));
              Assert.AreEqual("Visual#1", ComponentKeys.PathOf(saveable.transform, second.transform));
              Assert.IsNull(ComponentKeys.FindChild(saveable.transform, "Missing"));
          }

          [Test]
          public void Keys_TypeNameOf_DropsPathAndNumber()
          {
              Assert.AreEqual("BoxCollider2D", ComponentKeys.TypeNameOf("Visual/BoxCollider2D#1"));
              Assert.AreEqual("Transform", ComponentKeys.TypeNameOf("Transform"));
          }

          [Test]
          public void Capture_OnlyComponentsWithSerializer()
          {
              Saveable saveable = MakeSaveable("Root", out _);
              saveable.gameObject.AddComponent<PlainProbe>();

              ObjectData data = ObjectState.Capture(saveable, Catalog);

              CollectionAssert.AreEquivalent(new[] { "Transform", "FieldsProbe" }, data.Components.Keys);
          }

          [Test]
          public void Capture_Unchecked_NotSaved()
          {
              Saveable saveable = MakeSaveable("Root", out _);
              saveable.SetSaved("FieldsProbe", false);

              ObjectData data = ObjectState.Capture(saveable, Catalog);

              Assert.IsFalse(data.Components.ContainsKey("FieldsProbe"));
              Assert.IsFalse(saveable.IsSaved("FieldsProbe"));
          }

          [Test]
          public void State_RoundTrip_ActiveEnabledAndData()
          {
              Saveable saveable = MakeSaveable("Root", out FieldsProbe probe);
              GameObject visual = Child(saveable.gameObject, "Visual");
              probe.Hp = 7;
              probe.enabled = false;
              visual.SetActive(false);
              saveable.transform.localPosition = new Vector3(4f, 5f, 0f);
              ObjectData data = ObjectState.Capture(saveable, Catalog);

              probe.Hp = 1;
              probe.enabled = true;
              visual.SetActive(true);
              saveable.transform.localPosition = Vector3.zero;
              ObjectState.RestoreActive(saveable, data);
              ObjectState.RestoreComponents(saveable, data, Catalog);

              Assert.AreEqual(7, probe.Hp);
              Assert.IsFalse(probe.enabled);
              Assert.IsFalse(visual.activeSelf);
              Assert.AreEqual(new Vector3(4f, 5f, 0f), saveable.transform.localPosition);
              Assert.AreEqual(false, data.Components["FieldsProbe"].Enabled);
              Assert.IsNull(data.Components["Transform"].Enabled, "Transform has no enabled flag");
          }

          [Test]
          public void Restore_MissingComponent_WarnsOnce()
          {
              Saveable saveable = MakeSaveable("Root", out _);
              ObjectData data = ObjectState.Capture(saveable, Catalog);
              data.Components["Ghost"] = new ComponentData { V = 1, Data = new JObject() };

              LogAssert.Expect(LogType.Warning, new Regex("has no component Ghost"));
              ObjectState.RestoreComponents(saveable, data, Catalog);
              ObjectState.RestoreComponents(saveable, data, Catalog);

              LogAssert.NoUnexpectedReceived();
          }

          [Test]
          public void Restore_OneComponentFails_OthersApplied()
          {
              Saveable saveable = MakeSaveable("Root", out FieldsProbe probe);
              saveable.transform.localPosition = new Vector3(3f, 0f, 0f);
              ObjectData data = ObjectState.Capture(saveable, Catalog);
              data.Components["FieldsProbe"].Data = JObject.Parse("{ \"_hp\": \"not a number\" }");
              saveable.transform.localPosition = Vector3.zero;

              LogAssert.Expect(LogType.Error, new Regex("Loading FieldsProbe of 'Root' failed"));
              ObjectState.RestoreComponents(saveable, data, Catalog);

              Assert.AreEqual(new Vector3(3f, 0f, 0f), saveable.transform.localPosition);
          }

          [Test]
          public void Restore_NewerComponentVersion_Skipped()
          {
              Saveable saveable = MakeSaveable("Root", out FieldsProbe probe);
              probe.Hp = 5;
              ObjectData data = ObjectState.Capture(saveable, Catalog);
              data.Components["FieldsProbe"].V = 9;
              probe.Hp = 1;

              LogAssert.Expect(LogType.Error, new Regex("newer than 1"));
              ObjectState.RestoreComponents(saveable, data, Catalog);

              Assert.AreEqual(1, probe.Hp);
          }

          [Test]
          public void Capture_ComponentThrows_KeepsPreviousEntry()
          {
              Saveable saveable = MakeSaveable("Root", out _);
              var failing = new TaggedSerializer(typeof(FieldsProbe), "kept");
              var catalog = new ComponentSerializerCatalog(new IComponentSerializer[] { failing }, new IComponentSerializer[0]);
              ObjectData previous = ObjectState.Capture(saveable, catalog);
              failing.FailCapture = true;

              LogAssert.Expect(LogType.Error, new Regex("Saving FieldsProbe of 'Root' failed"));
              ObjectData data = ObjectState.Capture(saveable, catalog, previous);

              Assert.AreEqual("kept", (string)data.Components["FieldsProbe"].Data);
          }
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** EditMode: падают все 10 `ObjectStateTests` с `NotImplementedException`.

- [x] **Шаг 5: реализация.**

  `Runtime/Objects/ComponentKeys.cs`:

  ```csharp
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  namespace Saving.Objects
  {
      /// <summary>
      /// Keys of a Saveable's components: the path of the component's object from the Saveable and the type name
      /// ("Transform", "Visual/SpriteRenderer", "BoxCollider2D#1"). A path is object names joined by '/', a repeated
      /// name among siblings gets "#1", "#2". The walk stops at a nested Saveable: it saves itself.
      /// </summary>
      internal static class ComponentKeys
      {
          public static List<KeyedComponent> Collect(Saveable root)
          {
              var result = new List<KeyedComponent>();
              Walk(root.transform, string.Empty, result);
              return result;
          }

          public static Transform FindChild(Transform root, string path)
          {
              if (string.IsNullOrEmpty(path))
                  return root;

              Transform current = root;
              foreach (string segment in path.Split('/'))
              {
                  current = FindAmong(ChildrenOf(current), segment);
                  if (current == null)
                      return null;
              }

              return current;
          }

          public static string PathOf(Transform root, Transform node)
          {
              var segments = new List<string>();
              for (Transform current = node; current != null && current != root; current = current.parent)
                  segments.Add(SegmentOf(current));

              segments.Reverse();
              return string.Join("/", segments);
          }

          /// <summary>The path of an object from its scene root, for parents that are not under a Saveable.</summary>
          public static string ScenePathOf(Transform node) => PathOf(null, node);

          public static Transform FindInScene(Scene scene, string path)
          {
              if (string.IsNullOrEmpty(path) || !scene.IsValid())
                  return null;

              int slash = path.IndexOf('/');
              string first = slash < 0 ? path : path.Substring(0, slash);
              var roots = new List<Transform>();
              foreach (GameObject root in scene.GetRootGameObjects())
                  roots.Add(root.transform);

              Transform top = FindAmong(roots, first);
              return top == null || slash < 0 ? top : FindChild(top, path.Substring(slash + 1));
          }

          /// <summary>The component type name in a key: "Visual/BoxCollider2D#1" -> "BoxCollider2D".</summary>
          public static string TypeNameOf(string key)
          {
              string last = key.Substring(key.LastIndexOf('/') + 1);
              int hash = last.IndexOf('#');
              return hash < 0 ? last : last.Substring(0, hash);
          }

          private static void Walk(Transform node, string path, List<KeyedComponent> result)
          {
              var counts = new Dictionary<string, int>();
              foreach (Component component in node.GetComponents<Component>())
              {
                  // A missing script is null; the Saveable itself is not a saved component
                  if (component == null || component is Saveable)
                      continue;

                  string type = component.GetType().Name;
                  counts.TryGetValue(type, out int index);
                  counts[type] = index + 1;
                  string name = index == 0 ? type : $"{type}#{index}";
                  result.Add(new KeyedComponent(path.Length == 0 ? name : $"{path}/{name}", path, component));
              }

              foreach (Transform child in node)
              {
                  if (child.GetComponent<Saveable>() != null)
                      continue;

                  string segment = SegmentOf(child);
                  Walk(child, path.Length == 0 ? segment : $"{path}/{segment}", result);
              }
          }

          private static string SegmentOf(Transform node)
          {
              int index = 0;
              foreach (Transform sibling in SiblingsOf(node))
              {
                  if (sibling == node)
                      break;

                  if (sibling.name == node.name)
                      index++;
              }

              return index == 0 ? node.name : $"{node.name}#{index}";
          }

          private static Transform FindAmong(IEnumerable<Transform> candidates, string segment)
          {
              int hash = segment.LastIndexOf('#');
              string name = segment;
              int wanted = 0;
              if (hash > 0 && int.TryParse(segment.Substring(hash + 1), out int number))
              {
                  name = segment.Substring(0, hash);
                  wanted = number;
              }

              int seen = 0;
              foreach (Transform candidate in candidates)
              {
                  if (candidate.name != name)
                      continue;

                  if (seen == wanted)
                      return candidate;

                  seen++;
              }

              return null;
          }

          private static IEnumerable<Transform> ChildrenOf(Transform node)
          {
              foreach (Transform child in node)
                  yield return child;
          }

          private static IEnumerable<Transform> SiblingsOf(Transform node)
          {
              if (node.parent != null)
                  return ChildrenOf(node.parent);

              var roots = new List<Transform>();
              foreach (GameObject root in node.gameObject.scene.GetRootGameObjects())
                  roots.Add(root.transform);

              return roots;
          }
      }
  }
  ```

  `Runtime/Objects/ObjectState.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// One Saveable's state: its active flag, the flags of children that hold saved components and the data of
      /// every checked component that has a serializer. A failing component is logged and skipped, the rest go on;
      /// on save it keeps its previous entry.
      /// </summary>
      internal static class ObjectState
      {
          private static readonly HashSet<string> MissingWarned = new();

          [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
          private static void ResetWarnings() => MissingWarned.Clear();

          public static ObjectData Capture(Saveable saveable, ComponentSerializerCatalog catalog, ObjectData previous = null)
          {
              var data = new ObjectData { Active = saveable.gameObject.activeSelf };
              foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
              {
                  IComponentSerializer serializer = catalog.For(entry.Component.GetType());
                  if (serializer == null || !saveable.IsSaved(entry.Key))
                      continue;

                  if (entry.Path.Length > 0)
                      data.Children[entry.Path] = entry.Component.gameObject.activeSelf;

                  try
                  {
                      data.Components[entry.Key] = new ComponentData
                      {
                          V = serializer.Version,
                          Enabled = EnabledOf(entry.Component),
                          Data = serializer.Capture(entry.Component)
                      };
                  }
                  catch (Exception exception)
                  {
                      Debug.LogError($"Saving {entry.Key} of '{saveable.name}' failed, its previous data is kept: " +
                                     exception.Message, saveable);
                      if (previous != null && previous.Components.TryGetValue(entry.Key, out ComponentData old))
                          data.Components[entry.Key] = old;
                  }
              }

              return data;
          }

          public static void RestoreActive(Saveable saveable, ObjectData data)
          {
              foreach (KeyValuePair<string, bool> pair in data.Children)
              {
                  Transform child = ComponentKeys.FindChild(saveable.transform, pair.Key);
                  if (child != null)
                      child.gameObject.SetActive(pair.Value);
              }

              saveable.gameObject.SetActive(data.Active);
          }

          public static void RestoreComponents(Saveable saveable, ObjectData data, ComponentSerializerCatalog catalog)
          {
              var byKey = new Dictionary<string, Component>();
              foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
                  byKey[entry.Key] = entry.Component;

              foreach (KeyValuePair<string, ComponentData> pair in data.Components)
              {
                  if (!byKey.TryGetValue(pair.Key, out Component component))
                  {
                      if (MissingWarned.Add(saveable.Id + "/" + pair.Key))
                          Debug.LogWarning($"'{saveable.name}' has no component {pair.Key} any more; its saved data is skipped.",
                              saveable);
                      continue;
                  }

                  IComponentSerializer serializer = catalog.For(component.GetType());
                  if (serializer == null || !saveable.IsSaved(pair.Key))
                      continue;

                  try
                  {
                      serializer.Restore(component, Upgrade(serializer, pair.Value));
                      if (pair.Value.Enabled.HasValue)
                          SetEnabled(component, pair.Value.Enabled.Value);
                  }
                  catch (Exception exception)
                  {
                      Debug.LogError($"Loading {pair.Key} of '{saveable.name}' failed, it is skipped: {exception.Message}",
                          saveable);
                  }
              }
          }

          private static JToken Upgrade(IComponentSerializer serializer, ComponentData entry)
          {
              if (entry.V > serializer.Version)
                  throw new InvalidOperationException($"it was saved with version {entry.V}, newer than {serializer.Version}");

              if (entry.V < 1)
                  throw new InvalidOperationException($"it has an invalid version {entry.V}");

              JToken data = entry.Data?.DeepClone();
              for (int version = entry.V; version < serializer.Version; version++)
                  data = serializer.Upgrade(data, version);

              return data;
          }

          private static bool? EnabledOf(Component component) => component switch
          {
              Behaviour behaviour => behaviour.enabled,
              Renderer renderer => renderer.enabled,
              _ => null
          };

          private static void SetEnabled(Component component, bool enabled)
          {
              switch (component)
              {
                  case Behaviour behaviour:
                      behaviour.enabled = enabled;
                      break;
                  case Renderer renderer:
                      renderer.enabled = enabled;
                      break;
              }
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — зелёный.** EditMode: `failed 0`.

- [x] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add Saveable with component keys and object state"
  ```

---

### Задача 5: скрытый реестр, жизненный цикл `Saveable` и тестовые ассеты

**Files:**
- Create: `Runtime/Objects/SaveRegistry.cs`, `Runtime/Objects/ISaveCallbacks.cs`
- Modify: `Runtime/Objects/Saveable.cs` (Awake, Start, OnDisable, OnDestroy)
- Create: `Tests/Runtime/SaveTestCounter.cs`, `Tests/Runtime/SaveTestCallbacks.cs`, `Tests/Runtime/TestScenes.cs`
- Create (командой): `Tests/Runtime/Scenes/SaveTestScene.unity`, `Tests/Runtime/Prefabs/SaveTestSpawn.prefab`
- Test: `Tests/Runtime/SaveRegistryTests.cs`

**Interfaces:**
- Consumes: `Saveable` (задача 4).
- Produces:
  - `ISaveCallbacks { void OnBeforeSave(); void OnAfterLoad(); }` (публичный, финальный).
  - `internal static SaveRegistry`: `NewId() → string` (GUID `N`), `Register(Saveable)`, `Unregister(Saveable)`,
    `Rekey(Saveable, string id)`, `TryGet(string id, out Saveable) → bool`, `All() → List<Saveable>`,
    `InScene(Scene) → List<Saveable>`, `Records() → List<SceneRecord>`, `TryGetRecord(Scene, out SceneRecord) → bool`,
    `NotifyReady(Saveable, bool started)`, события `SceneScanned(Scene)` и `Ready(Saveable)`.
  - `SaveRegistry.SceneRecord { Scene Scene; HashSet<string> Originals; int ScanFrame; bool AnyStarted; bool Applied;
    bool IsFresh; }` — `IsFresh`: отсканирована в этом кадре, ни один `Saveable` в ней не стартовал, данные не
    применялись.
- Поведение: `Awake` → `Register`; пустой ID → новый, `IsSpawned`; ID уже занят живым объектом → новый, `IsSpawned`
  (копия в игре — молча; повтор в ещё не отсканированной сцене — ошибка в консоль). На `sceneLoaded` реестр обходит
  корни сцены с выключенными объектами, регистрирует незарегистрированных; не-`IsSpawned` — исходный набор сцены;
  затем `SceneScanned`. `Start` → `HasStarted`, `Ready`; `OnDisable` до `Start` → `Ready` (данные не ждут `Start`,
  которого не будет). Статика сбрасывается в `SubsystemRegistration`.
- Тестовая сцена: `Box` (ID `5a0e0000000000000000000000000001`, `SaveTestCounter`, `SaveTestCallbacks`), `Hidden`
  (выключен, ID `…0002`, `SaveTestCounter`), `Doomed` (ID `…0003`), `Holder` (без `Saveable`). Префаб
  `SaveTestSpawn`: `Saveable` + `SaveTestCounter`, ребёнок `Inner` с `Saveable` + `SaveTestCounter`; ID пустые, ID
  префаба — GUID ассета.

- [x] **Шаг 1: колбэки** — `Runtime/Objects/ISaveCallbacks.cs` (финальный):

  ```csharp
  namespace Saving.Objects
  {
      /// <summary>Hooks for components of a Saveable: before its state is captured, after its saved data is applied.</summary>
      public interface ISaveCallbacks
      {
          void OnBeforeSave();
          void OnAfterLoad();
      }
  }
  ```

- [x] **Шаг 2: заглушка реестра** — `Runtime/Objects/SaveRegistry.cs` (без подписки на `sceneLoaded`: красный
  прогон не должен ронять чужие тесты):

  ```csharp
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  namespace Saving.Objects
  {
      internal static class SaveRegistry
      {
          internal sealed class SceneRecord
          {
              public SceneRecord(Scene scene)
              {
                  Scene = scene;
                  ScanFrame = Time.frameCount;
              }

              public Scene Scene { get; }
              public HashSet<string> Originals { get; } = new();
              public int ScanFrame { get; }
              public bool AnyStarted { get; set; }
              public bool Applied { get; set; }
              public bool IsFresh => ScanFrame == Time.frameCount && !AnyStarted && !Applied;
          }

          public static event Action<Scene> SceneScanned;
          public static event Action<Saveable> Ready;

          public static string NewId() => throw new NotImplementedException();
          public static void Register(Saveable saveable) => throw new NotImplementedException();
          public static void Unregister(Saveable saveable) => throw new NotImplementedException();
          public static void Rekey(Saveable saveable, string id) => throw new NotImplementedException();
          public static bool TryGet(string id, out Saveable saveable) => throw new NotImplementedException();
          public static List<Saveable> All() => throw new NotImplementedException();
          public static List<Saveable> InScene(Scene scene) => throw new NotImplementedException();
          public static List<SceneRecord> Records() => throw new NotImplementedException();
          public static bool TryGetRecord(Scene scene, out SceneRecord record) => throw new NotImplementedException();
          public static void NotifyReady(Saveable saveable, bool started) => throw new NotImplementedException();
      }
  }
  ```

  (События объявлены, но не вызываются — компилятор предупредит `CS0067`; уйдёт на шаге 6.)

- [x] **Шаг 3: жизненный цикл `Saveable`.** В `Runtime/Objects/Saveable.cs` после `SetId`:

  ```csharp
          private void Awake() => SaveRegistry.Register(this);

          private void Start()
          {
              HasStarted = true;
              SaveRegistry.NotifyReady(this, started: true);
          }

          // Switched off before its Start: the Start never comes, so its data must not wait for it
          private void OnDisable()
          {
              if (!HasStarted)
                  SaveRegistry.NotifyReady(this, started: false);
          }

          private void OnDestroy() => SaveRegistry.Unregister(this);
  ```

- [x] **Шаг 4: тестовые компоненты и помощник.** Каждый — в своём файле.

  `Tests/Runtime/SaveTestCounter.cs`:

  ```csharp
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>A [Save] value that the object's own Start resets, as game code often does.</summary>
      public sealed class SaveTestCounter : MonoBehaviour
      {
          [SerializeField] private int _initialValue = 1;
          [Save] public int Value;

          public int Starts { get; private set; }

          private void Start()
          {
              Value = _initialValue;
              Starts++;
          }
      }
  }
  ```

  `Tests/Runtime/SaveTestCallbacks.cs`:

  ```csharp
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>Counts the save callbacks it gets.</summary>
      public sealed class SaveTestCallbacks : MonoBehaviour, ISaveCallbacks
      {
          public int BeforeSave { get; private set; }
          public int AfterLoad { get; private set; }

          public void OnBeforeSave() => BeforeSave++;
          public void OnAfterLoad() => AfterLoad++;
      }
  }
  ```

  `Tests/Runtime/TestScenes.cs`:

  ```csharp
  using System;
  using System.Collections;
  using System.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  #if UNITY_EDITOR
  using UnityEditor;
  using UnityEditor.SceneManagement;
  #endif

  namespace Saving.Tests
  {
      /// <summary>The module's test scene and prefab: paths, fixed ids, loading and waiting.</summary>
      internal static class TestScenes
      {
          public const string SCENE = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/Scenes/SaveTestScene.unity";
          public const string PREFAB = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/Prefabs/SaveTestSpawn.prefab";
          public const string BOX = "5a0e0000000000000000000000000001";
          public const string HIDDEN = "5a0e0000000000000000000000000002";
          public const string DOOMED = "5a0e0000000000000000000000000003";

          /// <summary>Loads the test scene (editor only: it is not in Build Settings) and waits until it has started.</summary>
          public static IEnumerator Load()
          {
  #if UNITY_EDITOR
              Scene scene = EditorSceneManager.LoadSceneInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
              yield return WaitFor(() => scene.isLoaded);
              yield return null; // Start of the scene's objects
  #else
              Assert.Ignore("The save system test scenes load only in the editor.");
              yield break;
  #endif
          }

          public static GameObject Prefab =>
  #if UNITY_EDITOR
              AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
  #else
              null;
  #endif

          public static string GuidOf(string path) =>
  #if UNITY_EDITOR
              AssetDatabase.AssetPathToGUID(path);
  #else
              string.Empty;
  #endif

          public static GameObject Find(string name) =>
              SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(root => root.name == name);

          public static IEnumerator WaitFor(Func<bool> condition, int frames = 300)
          {
              for (int frame = 0; frame < frames; frame++)
              {
                  if (condition())
                      yield break;

                  yield return null;
              }

              Assert.Fail($"The condition did not come true in {frames} frames.");
          }
      }
  }
  ```

- [x] **Шаг 5: тестовые ассеты.** `Refresh`, консоль без ошибок компиляции, затем `Unity_RunCommand` (создаёт сцену и
  префаб в папке тестов модуля; открытые сцены пользователя не трогает):

  ```csharp
  using System;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  internal class CommandScript : IRunCommand
  {
      private const string FOLDER = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime";
      private const string SCENE = FOLDER + "/Scenes/SaveTestScene.unity";
      private const string PREFAB = FOLDER + "/Prefabs/SaveTestSpawn.prefab";

      private static readonly Type Saveable = Type.GetType("Saving.Objects.Saveable, SaveSystem.Objects");
      private static readonly Type Counter = Type.GetType("Saving.Tests.SaveTestCounter, SaveSystem.Tests.Runtime");
      private static readonly Type Callbacks = Type.GetType("Saving.Tests.SaveTestCallbacks, SaveSystem.Tests.Runtime");

      public void Execute(ExecutionResult result)
      {
          if (Saveable == null || Counter == null || Callbacks == null)
          {
              result.LogError("Test types are not compiled yet");
              return;
          }

          if (!AssetDatabase.IsValidFolder(FOLDER + "/Scenes"))
              AssetDatabase.CreateFolder(FOLDER, "Scenes");
          if (!AssetDatabase.IsValidFolder(FOLDER + "/Prefabs"))
              AssetDatabase.CreateFolder(FOLDER, "Prefabs");

          Scene previous = SceneManager.GetActiveScene();
          Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
          SceneManager.SetActiveScene(scene);

          var spawn = new GameObject("SaveTestSpawn");
          spawn.AddComponent(Saveable);
          spawn.AddComponent(Counter);
          var inner = new GameObject("Inner");
          inner.transform.SetParent(spawn.transform, false);
          inner.AddComponent(Saveable);
          inner.AddComponent(Counter);
          PrefabUtility.SaveAsPrefabAsset(spawn, PREFAB);
          UnityEngine.Object.DestroyImmediate(spawn);

          var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
          SetString(prefab.GetComponent(Saveable), "_prefabId", AssetDatabase.AssetPathToGUID(PREFAB));
          PrefabUtility.SavePrefabAsset(prefab);

          Make("Box", "5a0e0000000000000000000000000001", active: true, counter: true, callbacks: true);
          Make("Hidden", "5a0e0000000000000000000000000002", active: false, counter: true, callbacks: false);
          Make("Doomed", "5a0e0000000000000000000000000003", active: true, counter: false, callbacks: false);
          new GameObject("Holder");

          EditorSceneManager.SaveScene(scene, SCENE);
          SceneManager.SetActiveScene(previous);
          EditorSceneManager.CloseScene(scene, true);
          AssetDatabase.Refresh();
          result.Log("Created {0} and {1}", SCENE, PREFAB);
      }

      private static void Make(string name, string id, bool active, bool counter, bool callbacks)
      {
          var go = new GameObject(name);
          SetString(go.AddComponent(Saveable), "_id", id);
          if (counter)
              go.AddComponent(Counter);
          if (callbacks)
              go.AddComponent(Callbacks);
          go.SetActive(active);
      }

      private static void SetString(Component component, string property, string value)
      {
          var serialized = new SerializedObject(component);
          serialized.FindProperty(property).stringValue = value;
          serialized.ApplyModifiedPropertiesWithoutUndo();
      }
  }
  ```

  Проверить: в `Tests/Runtime/Scenes` и `Tests/Runtime/Prefabs` появились ассеты; в YAML сцены у `Hidden`
  `m_IsActive: 0`; у корня префаба `_prefabId` — его GUID, у `Inner` — пусто.

- [x] **Шаг 6: падающие тесты** — `Tests/Runtime/SaveRegistryTests.cs`:

  ```csharp
  using System.Collections;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>The hidden registry: scene scan with disabled objects, copies and prefab instances, destruction.</summary>
      public class SaveRegistryTests
      {
          [UnitySetUp]
          public IEnumerator SetUp()
          {
              yield return TestScenes.Load();
          }

          [UnityTest]
          public IEnumerator Registry_SceneScan_FindsDisabledObjectsAsOriginals()
          {
              Assert.IsTrue(SaveRegistry.TryGet(TestScenes.HIDDEN, out Saveable hidden));
              Assert.IsFalse(hidden.gameObject.activeSelf);
              Assert.IsFalse(hidden.IsSpawned);

              Assert.IsTrue(SaveRegistry.TryGetRecord(SceneManager.GetActiveScene(), out SaveRegistry.SceneRecord record));
              CollectionAssert.IsSupersetOf(record.Originals, new[] { TestScenes.BOX, TestScenes.HIDDEN, TestScenes.DOOMED });
              yield break;
          }

          [UnityTest]
          public IEnumerator Registry_CopyOfSceneObject_NewIdAndSpawned()
          {
              GameObject box = TestScenes.Find("Box");

              var copy = Object.Instantiate(box).GetComponent<Saveable>();

              Assert.AreNotEqual(TestScenes.BOX, copy.Id);
              Assert.IsTrue(copy.IsSpawned);
              Assert.IsTrue(SaveRegistry.TryGet(TestScenes.BOX, out Saveable original));
              Assert.AreSame(box, original.gameObject);
              yield break;
          }

          [UnityTest]
          public IEnumerator Registry_PrefabInstance_SpawnedWithPrefabId()
          {
              GameObject instance = Object.Instantiate(TestScenes.Prefab);
              var root = instance.GetComponent<Saveable>();
              var inner = instance.transform.Find("Inner").GetComponent<Saveable>();

              Assert.AreEqual(32, root.Id.Length);
              Assert.IsTrue(root.IsSpawned);
              Assert.AreEqual(TestScenes.GuidOf(TestScenes.PREFAB), root.PrefabId);
              Assert.IsTrue(inner.IsSpawned);
              Assert.AreNotEqual(root.Id, inner.Id);
              yield break;
          }

          [UnityTest]
          public IEnumerator Registry_Destroyed_Unregistered()
          {
              Object.Destroy(TestScenes.Find("Doomed"));
              yield return null;

              Assert.IsFalse(SaveRegistry.TryGet(TestScenes.DOOMED, out _));
          }

          [UnityTest]
          public IEnumerator Registry_Started_SceneNoLongerFresh()
          {
              Assert.IsTrue(SaveRegistry.TryGet(TestScenes.BOX, out Saveable box));
              Assert.IsTrue(box.HasStarted);
              Assert.IsTrue(SaveRegistry.TryGetRecord(SceneManager.GetActiveScene(), out SaveRegistry.SceneRecord record));
              Assert.IsTrue(record.AnyStarted);
              Assert.IsFalse(record.IsFresh);
              yield break;
          }
      }
  }
  ```

- [x] **Шаг 7: запуск — красный.** PlayMode: падают 5 `SaveRegistryTests` (`NotImplementedException` из
  `Saveable.Awake` при загрузке сцены и из вызовов реестра); остальные тесты зелёные.

- [x] **Шаг 8: реализация** — `Runtime/Objects/SaveRegistry.cs` целиком:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  namespace Saving.Objects
  {
      /// <summary>
      /// The hidden registry: every live Saveable by id and the original set of every loaded scene. Saveables register
      /// in Awake; on sceneLoaded (before Start) the scene is scanned, disabled objects included, which is why no
      /// registry object is needed in a scene. Static state resets when Play Mode starts, with or without a domain
      /// reload.
      /// </summary>
      internal static class SaveRegistry
      {
          internal sealed class SceneRecord
          {
              public SceneRecord(Scene scene)
              {
                  Scene = scene;
                  ScanFrame = Time.frameCount;
              }

              public Scene Scene { get; }
              public HashSet<string> Originals { get; } = new();
              public int ScanFrame { get; }
              public bool AnyStarted { get; set; }
              public bool Applied { get; set; }

              /// <summary>Scanned this frame and untouched: a load may apply to it in place instead of reloading it.</summary>
              public bool IsFresh => ScanFrame == Time.frameCount && !AnyStarted && !Applied;
          }

          private static readonly Dictionary<string, Saveable> ById = new();
          private static readonly Dictionary<Scene, SceneRecord> Scenes = new();

          public static event Action<Scene> SceneScanned;
          public static event Action<Saveable> Ready;

          [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
          private static void Reset()
          {
              ById.Clear();
              Scenes.Clear();
              SceneScanned = null;
              Ready = null;
              SceneManager.sceneLoaded -= OnSceneLoaded;
              SceneManager.sceneLoaded += OnSceneLoaded;
              SceneManager.sceneUnloaded -= OnSceneUnloaded;
              SceneManager.sceneUnloaded += OnSceneUnloaded;
          }

          public static string NewId() => Guid.NewGuid().ToString("N");

          public static void Register(Saveable saveable)
          {
              if (saveable.IsRegistered)
                  return;

              saveable.IsRegistered = true;
              string id = saveable.Id;
              if (string.IsNullOrEmpty(id))
              {
                  Add(saveable, NewId(), spawned: true);
                  return;
              }

              if (ById.TryGetValue(id, out Saveable other) && other != null && !ReferenceEquals(other, saveable))
              {
                  // A copy made in play is expected; the same id twice in a scene being loaded is not
                  if (!Scenes.ContainsKey(saveable.gameObject.scene))
                      Debug.LogError($"'{saveable.name}' has the id {id} of '{other.name}'; it gets a new id and counts as " +
                                     "spawned. Run Tools → Save System → Validate Scenes.", saveable);

                  Add(saveable, NewId(), spawned: true);
                  return;
              }

              Add(saveable, id, spawned: false);
          }

          public static void Unregister(Saveable saveable)
          {
              if (!saveable.IsRegistered)
                  return;

              saveable.IsRegistered = false;
              if (!string.IsNullOrEmpty(saveable.Id) && ById.TryGetValue(saveable.Id, out Saveable current)
                  && ReferenceEquals(current, saveable))
                  ById.Remove(saveable.Id);
          }

          /// <summary>Gives a restored spawned object the id it was saved with.</summary>
          public static void Rekey(Saveable saveable, string id)
          {
              if (!string.IsNullOrEmpty(saveable.Id) && ById.TryGetValue(saveable.Id, out Saveable current)
                  && ReferenceEquals(current, saveable))
                  ById.Remove(saveable.Id);

              if (ById.TryGetValue(id, out Saveable other) && other != null && !ReferenceEquals(other, saveable))
              {
                  Debug.LogError($"The saved id {id} is taken by '{other.name}'; '{saveable.name}' keeps a new one.", saveable);
                  id = NewId();
              }

              saveable.IsRegistered = true;
              Add(saveable, id, spawned: true);
          }

          public static bool TryGet(string id, out Saveable saveable)
          {
              saveable = null;
              return !string.IsNullOrEmpty(id) && ById.TryGetValue(id, out saveable) && saveable != null;
          }

          public static List<Saveable> All() => ById.Values.Where(saveable => saveable != null).ToList();

          public static List<Saveable> InScene(Scene scene) =>
              ById.Values.Where(saveable => saveable != null && saveable.gameObject.scene == scene).ToList();

          public static List<SceneRecord> Records() => Scenes.Values.ToList();

          public static bool TryGetRecord(Scene scene, out SceneRecord record) => Scenes.TryGetValue(scene, out record);

          public static void NotifyReady(Saveable saveable, bool started)
          {
              if (started && Scenes.TryGetValue(saveable.gameObject.scene, out SceneRecord record))
                  record.AnyStarted = true;

              Ready?.Invoke(saveable);
          }

          private static void Add(Saveable saveable, string id, bool spawned)
          {
              saveable.SetId(id);
              saveable.IsSpawned = spawned;
              ById[id] = saveable;
          }

          private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
          {
              var record = new SceneRecord(scene);
              foreach (GameObject root in scene.GetRootGameObjects())
              {
                  foreach (Saveable saveable in root.GetComponentsInChildren<Saveable>(true))
                  {
                      Register(saveable);
                      if (!saveable.IsSpawned)
                          record.Originals.Add(saveable.Id);
                  }
              }

              Scenes[scene] = record;
              SceneScanned?.Invoke(scene);
          }

          private static void OnSceneUnloaded(Scene scene)
          {
              Scenes.Remove(scene);
              // Objects that were never active get no OnDestroy: drop what died with the scene
              foreach (string id in ById.Where(pair => pair.Value == null).Select(pair => pair.Key).ToList())
                  ById.Remove(id);
          }
      }
  }
  ```

- [x] **Шаг 9: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 10: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add the hidden Saveable registry and test scene"
  ```

---

### Задача 6: секции сцен, фазы загрузки и `SaveSystemSettings`

**Files:**
- Create: `Runtime/Objects/SceneData.cs`, `Runtime/Objects/SaveSystemSettings.cs`, `Runtime/Objects/SceneSaving.cs`
- Modify: `Tests/Runtime/TestScenes.cs` (+ `CreateSettings`)
- Test: `Tests/Runtime/SceneSavingTests.cs`

**Interfaces:**
- Consumes: `ISaveExtension`, `SaveExtensionAsset`, `SaveLoadService.TryGetSection`, `LoadOutcome.SceneNotFound`
  (задача 1); `ComponentSerializerCatalog` (задача 3); `Saveable`, `ComponentKeys`, `ObjectData`, `ObjectState`
  (задача 4); `SaveRegistry`, `ISaveCallbacks` (задача 5).
- Produces:
  - `SaveSystemSettings : SaveExtensionAsset` (`[CreateAssetMenu]`): вложенные `PrefabEntry { string Id; GameObject
    Prefab; }`, `SceneEntry { string Guid; string Path; }`; `IReadOnlyList<PrefabEntry> Prefabs`,
    `IReadOnlyList<SceneEntry> Scenes`, `TryGetPrefab(string id, out GameObject)`, `TryGetSceneGuid(string path, out
    string guid)`, `TryGetScenePath(string guid, out string path)`, `SetCatalogs(IEnumerable<PrefabEntry>,
    IEnumerable<SceneEntry>)`; `CreateExtension` → `SceneSaving` с `ComponentSerializerCatalog.Default`.
  - `internal SceneSaving : ISaveExtension` (ключи `SCENES_KEY = "$scenes"`, `PERSISTENT_KEY = "$persistent"`,
    `SCENE_PREFIX = "scene/"`).
  - Данные: `SceneData { Dictionary<string, ObjectData> Objects; List<string> Destroyed; List<SpawnedData> Spawned; }`,
    `SpawnedData : ObjectData { string Id; string Prefab; string Parent; string ParentPath; Dictionary<string,
    NestedData> Nested; }`, `NestedData : ObjectData { string Id; }`, `ScenesData { SceneRef Active; }`,
    `SceneRef { string Path; string Guid; }`.
- Сохранение: `OnBeforeSave` у компонентов всех живых `Saveable`; по каждой отсканированной загруженной сцене из
  каталога — `objects` (живые не-заспавненные), `destroyed` (исходный набор минус живые), `spawned` (активные
  заспавненные верхнего уровня с ID префаба, родителем и вложенными); `$persistent` — объекты `DontDestroyOnLoad`;
  `$scenes` — активная сцена. Объект, чьи данные ещё ждут `Start`, пишется данными из очереди, а не захватом.
- Загрузка: `Check` — версии секций слоя и компонентов (по имени типа), читаемость секций, наличие сохранённой сцены;
  `BeginLoad` — `$persistent` к живым объектам сразу, затем сохранённая сцена: на месте, если она активна и
  `IsFresh`, иначе перезагрузка (`LoadSceneInPlayMode` в эдиторе, `SceneManager.LoadScene` в сборке). Любая сцена при
  загрузке (`SceneScanned`) получает свою секцию из снимка: уничтожить `destroyed`, заспавнить `spawned`, выставить
  `active`, раздать данные — сразу выключенным и уже стартовавшим, остальным — в их позднем `Start`. Когда очередь
  пуста и ждать нечего — `OnAfterLoad` восстановленным, затем `finished`.

- [x] **Шаг 1: данные секций** — `Runtime/Objects/SceneData.cs` (финальные):

  ```csharp
  using System.Collections.Generic;

  namespace Saving.Objects
  {
      /// <summary>A scene section: its Saveables by id, the ids destroyed since the scene loaded, objects made in play.</summary>
      internal sealed class SceneData
      {
          public Dictionary<string, ObjectData> Objects = new();
          public List<string> Destroyed = new();
          public List<SpawnedData> Spawned = new();
      }

      /// <summary>An object made in play: restored from its prefab under its parent, with its nested Saveables.</summary>
      internal sealed class SpawnedData : ObjectData
      {
          public string Id;
          public string Prefab;
          public string Parent;      // id of the nearest Saveable above, if any
          public string ParentPath;  // from that Saveable, or from the scene root when there is none
          public Dictionary<string, NestedData> Nested = new();
      }

      /// <summary>A Saveable inside a spawned object, found by its path from the spawned root.</summary>
      internal sealed class NestedData : ObjectData
      {
          public string Id;
      }

      /// <summary>"$scenes": the scene that was active when the game saved.</summary>
      internal sealed class ScenesData
      {
          public SceneRef Active;
      }

      internal sealed class SceneRef
      {
          public string Path;
          public string Guid;
      }
  }
  ```

- [x] **Шаг 2: настройки** — `Runtime/Objects/SaveSystemSettings.cs` (финальные: логики, кроме поиска по
  спискам, нет):

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// The project's save settings, set on SaveLoadSystem: plugs in the objects layer and holds its catalogs —
      /// prefabs with a Saveable on the root by prefab id, scenes by GUID and path. The editor keeps the catalogs up to
      /// date on its own (on import, move and delete, before a build, Tools → Save System → Rebuild Catalogs).
      /// </summary>
      [CreateAssetMenu(fileName = "SaveSystemSettings", menuName = "Save System/Settings")]
      public sealed class SaveSystemSettings : SaveExtensionAsset
      {
          [Serializable]
          public sealed class PrefabEntry
          {
              public string Id;
              public GameObject Prefab;
          }

          [Serializable]
          public sealed class SceneEntry
          {
              public string Guid;
              public string Path;
          }

          [SerializeField] private List<PrefabEntry> _prefabs = new();
          [SerializeField] private List<SceneEntry> _scenes = new();

          public IReadOnlyList<PrefabEntry> Prefabs => _prefabs;
          public IReadOnlyList<SceneEntry> Scenes => _scenes;

          public override ISaveExtension CreateExtension(SaveLoadService service) =>
              new SceneSaving(service, this, ComponentSerializerCatalog.Default);

          public bool TryGetPrefab(string id, out GameObject prefab)
          {
              prefab = _prefabs.FirstOrDefault(entry => entry.Id == id)?.Prefab;
              return prefab != null;
          }

          public bool TryGetSceneGuid(string path, out string guid)
          {
              guid = _scenes.FirstOrDefault(entry => entry.Path == path)?.Guid;
              return !string.IsNullOrEmpty(guid);
          }

          public bool TryGetScenePath(string guid, out string path)
          {
              path = _scenes.FirstOrDefault(entry => entry.Guid == guid)?.Path;
              return !string.IsNullOrEmpty(path);
          }

          /// <summary>Replaces both catalogs: the editor fills them, tests fill them by hand.</summary>
          public void SetCatalogs(IEnumerable<PrefabEntry> prefabs, IEnumerable<SceneEntry> scenes)
          {
              _prefabs = prefabs.ToList();
              _scenes = scenes.ToList();
          }
      }
  }
  ```

- [x] **Шаг 3: заглушка** — `Runtime/Objects/SceneSaving.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving.Objects
  {
      internal sealed class SceneSaving : ISaveExtension
      {
          public const string SCENES_KEY = "$scenes";
          public const string PERSISTENT_KEY = "$persistent";
          public const string SCENE_PREFIX = "scene/";

          public SceneSaving(SaveLoadService service, SaveSystemSettings settings, ComponentSerializerCatalog catalog)
          {
          }

          public void CaptureInto(IDictionary<string, SaveSection> sections) => throw new NotImplementedException();

          public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem) =>
              throw new NotImplementedException();

          public void BeginLoad(Action finished) => throw new NotImplementedException();

          public void Dispose()
          {
          }
      }
  }
  ```

- [x] **Шаг 4: помощник настроек.** В `Tests/Runtime/TestScenes.cs`: `using Saving.Objects;` и метод:

  ```csharp
          /// <summary>Settings with the test prefab and the test scene in their catalogs.</summary>
          public static SaveSystemSettings CreateSettings()
          {
              var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              settings.SetCatalogs(
                  new[] { new SaveSystemSettings.PrefabEntry { Id = GuidOf(PREFAB), Prefab = Prefab } },
                  new[] { new SaveSystemSettings.SceneEntry { Guid = GuidOf(SCENE), Path = SCENE } });
              return settings;
          }
  ```

- [x] **Шаг 5: падающие тесты** — `Tests/Runtime/SceneSavingTests.cs`:

  ```csharp
  using System.Collections;
  using System.Text.RegularExpressions;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using UnityEngine.TestTools;
  #if UNITY_EDITOR
  using UnityEditor.SceneManagement;
  #endif
  using Object = UnityEngine.Object;

  namespace Saving.Tests
  {
      /// <summary>
      /// Scene objects through a real save system on the test scene: destroyed, spawned, disabled, untouched objects,
      /// the order against Start, the load phases, in-place loads, DontDestroyOnLoad, callbacks, scene changes.
      /// </summary>
      public class SceneSavingTests
      {
          private InMemorySaveStorage _storage;
          private SaveSystemSettings _settings;
          private GameObject _systemObject;
          private SaveLoadSystem _system;

          [UnitySetUp]
          public IEnumerator SetUp()
          {
              yield return TestScenes.Load();
              _storage = new InMemorySaveStorage();
              _settings = TestScenes.CreateSettings();
              CreateSystem();
          }

          private void CreateSystem()
          {
              _systemObject = new GameObject("SaveLoadSystem");
              Object.DontDestroyOnLoad(_systemObject); // like [Services]: it outlives the scene reload
              _system = _systemObject.AddComponent<SaveLoadSystem>();
              _system.UseStorage(_storage);
              _system.UseSettings(_settings);
              _system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
          }

          [TearDown]
          public void TearDown()
          {
              Object.DestroyImmediate(_systemObject);
              Object.DestroyImmediate(_settings);
          }

          private static SaveTestCounter Counter(string name) => TestScenes.Find(name).GetComponent<SaveTestCounter>();

          private static SaveTestCounter CounterOf(string id) =>
              SaveRegistry.TryGet(id, out Saveable saveable) ? saveable.GetComponent<SaveTestCounter>() : null;

          private IEnumerator LoadAndWait(LoadOutcome expected = LoadOutcome.Loaded)
          {
              bool done = false;
              void OnLoaded(LoadOutcome _) => done = true;
              _system.Loaded += OnLoaded;
              Assert.AreEqual(expected, _system.Load());
              yield return TestScenes.WaitFor(() => done);
              _system.Loaded -= OnLoaded;
              yield return null; // Destroy calls of the load take effect
          }

          private string SceneKey => SceneSaving.SCENE_PREFIX + TestScenes.GuidOf(TestScenes.SCENE);

          [UnityTest]
          public IEnumerator Scene_UserStartRunsFirst_LoadedValueWins()
          {
              Counter("Box").Value = 42;
              _system.Save();
              Counter("Box").Value = 3;

              yield return LoadAndWait();

              Assert.AreEqual(42, Counter("Box").Value);
              Assert.AreEqual(1, Counter("Box").Starts, "the reloaded Box ran its own Start once");
          }

          [UnityTest]
          public IEnumerator Scene_DestroyedObject_StaysDestroyed()
          {
              Object.Destroy(TestScenes.Find("Doomed"));
              yield return null;
              _system.Save();

              yield return LoadAndWait();

              Assert.IsNull(TestScenes.Find("Doomed"));
              Assert.IsNotNull(TestScenes.Find("Box"));
          }

          [UnityTest]
          public IEnumerator Scene_SpawnedPrefab_BackWithIdParentNestedAndData()
          {
              GameObject spawned = Object.Instantiate(TestScenes.Prefab, TestScenes.Find("Holder").transform);
              spawned.transform.localPosition = new Vector3(2f, 3f, 0f);
              yield return null; // its Start resets the counters
              spawned.GetComponent<SaveTestCounter>().Value = 7;
              spawned.transform.Find("Inner").GetComponent<SaveTestCounter>().Value = 8;
              string id = spawned.GetComponent<Saveable>().Id;
              string innerId = spawned.transform.Find("Inner").GetComponent<Saveable>().Id;
              _system.Save();

              yield return LoadAndWait();

              Assert.IsTrue(SaveRegistry.TryGet(id, out Saveable restored));
              Assert.AreEqual("Holder", restored.transform.parent.name);
              Assert.AreEqual(new Vector3(2f, 3f, 0f), restored.transform.localPosition);
              Assert.AreEqual(7, CounterOf(id).Value);
              Assert.AreEqual(8, CounterOf(innerId).Value);
              Assert.AreSame(restored.transform, CounterOf(innerId).transform.parent);
          }

          [UnityTest]
          public IEnumerator Scene_DisabledObject_GetsItsData()
          {
              Counter("Hidden").Value = 5;
              _system.Save();

              yield return LoadAndWait();

              Assert.AreEqual(5, Counter("Hidden").Value);
              Assert.IsFalse(TestScenes.Find("Hidden").activeSelf);
          }

          [UnityTest]
          public IEnumerator Scene_ObjectWithoutData_StaysAsInScene()
          {
              Vector3 scenePosition = TestScenes.Find("Box").transform.position;
              TestScenes.Find("Box").transform.position = new Vector3(5f, 5f, 0f);
              _system.Save();
              Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
              Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
              ((JObject)json["sections"][SceneKey]["data"]["objects"]).Remove(TestScenes.BOX);
              _storage.Put(0, SaveFiles.SAVE, json.ToString());

              yield return LoadAndWait();

              Assert.AreEqual(scenePosition, TestScenes.Find("Box").transform.position);
          }

          [UnityTest]
          public IEnumerator Scene_Loaded_RaisedAfterDataApplied()
          {
              Counter("Box").Value = 42;
              _system.Save();
              int valueOnLoaded = -1;
              bool loadingOnLoaded = true;
              _system.Loaded += _ =>
              {
                  valueOnLoaded = Counter("Box").Value;
                  loadingOnLoaded = _system.IsLoading;
              };

              yield return LoadAndWait();

              Assert.AreEqual(42, valueOnLoaded);
              Assert.IsFalse(loadingOnLoaded);
          }

          [UnityTest]
          public IEnumerator Scene_SaveDuringReload_Blocked()
          {
              _system.Save();
              bool done = false;
              _system.Loaded += _ => done = true;

              _system.Load();
              SaveOutcome during = _system.Save();
              yield return TestScenes.WaitFor(() => done);

              Assert.AreEqual(SaveOutcome.Blocked, during);
          }

          [UnityTest]
          public IEnumerator Scene_UnknownScene_SceneNotFoundAndNothingReloaded()
          {
              _storage.Put(0, SaveFiles.SAVE, "{ \"formatVersion\": 1, \"gameVersion\": \"t\", \"sections\": { \"$scenes\": " +
                                              "{ \"v\": 1, \"data\": { \"active\": { \"path\": \"Assets/Missing.unity\", " +
                                              "\"guid\": \"00000000000000000000000000000000\" } } } } }");
              GameObject box = TestScenes.Find("Box");

              LogAssert.Expect(LogType.Error, new Regex("is not in the build"));
              Assert.AreEqual(LoadOutcome.SceneNotFound, _system.Load());
              yield return null;

              Assert.IsTrue(_system.IsReadOnly);
              Assert.AreSame(box, TestScenes.Find("Box"), "the scene was not reloaded");
          }

          [UnityTest]
          public IEnumerator Scene_NewerComponentData_NewerVersionAndNothingReloaded()
          {
              _system.Save();
              Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
              Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
              json["sections"][SceneKey]["data"]["objects"][TestScenes.BOX]["components"]["Transform"]["v"] = 9;
              _storage.Put(0, SaveFiles.SAVE, json.ToString());
              GameObject box = TestScenes.Find("Box");

              LogAssert.Expect(LogType.Error, new Regex("newer than 1"));
              Assert.AreEqual(LoadOutcome.NewerVersion, _system.Load());
              yield return null;

              Assert.AreSame(box, TestScenes.Find("Box"));
          }

  #if UNITY_EDITOR
          [UnityTest]
          public IEnumerator Scene_LoadRightAfterSceneLoad_AppliesInPlace()
          {
              Counter("Box").Value = 42;
              _system.Save();
              // A game that has just started: empty snapshot, the save is only on disk
              Object.DestroyImmediate(_systemObject);
              CreateSystem();
              int loads = 0;
              bool done = false;

              void OnSceneLoaded(Scene scene, LoadSceneMode mode)
              {
                  if (scene.path != TestScenes.SCENE)
                      return;

                  loads++;
                  if (loads > 1)
                      return;

                  _system.Loaded += _ => done = true;
                  _system.Load();
              }

              SceneManager.sceneLoaded += OnSceneLoaded;
              try
              {
                  EditorSceneManager.LoadSceneInPlayMode(TestScenes.SCENE, new LoadSceneParameters(LoadSceneMode.Single));
                  yield return TestScenes.WaitFor(() => done);
                  yield return null;
              }
              finally
              {
                  SceneManager.sceneLoaded -= OnSceneLoaded;
              }

              Assert.AreEqual(1, loads, "the saved scene was applied without loading it again");
              Assert.AreEqual(42, Counter("Box").Value);
          }

          [UnityTest]
          public IEnumerator Scene_SceneChange_RestoredFromSnapshot()
          {
              Counter("Box").Value = 42;
              _system.Capture();

              Scene scene = EditorSceneManager.LoadSceneInPlayMode(TestScenes.SCENE, new LoadSceneParameters(LoadSceneMode.Single));
              yield return TestScenes.WaitFor(() => scene.isLoaded);
              yield return null;

              Assert.AreEqual(42, Counter("Box").Value);
          }
  #endif

          [UnityTest]
          public IEnumerator Scene_DontDestroyOnLoad_AliveObjectAndRespawn()
          {
              GameObject keeper = Object.Instantiate(TestScenes.Prefab);
              Object.DontDestroyOnLoad(keeper);
              yield return null;
              string id = keeper.GetComponent<Saveable>().Id;
              keeper.GetComponent<SaveTestCounter>().Value = 9;
              _system.Save();
              keeper.GetComponent<SaveTestCounter>().Value = 1;

              yield return LoadAndWait();
              Assert.AreEqual(9, keeper.GetComponent<SaveTestCounter>().Value, "the live object got its data");

              Object.Destroy(keeper);
              yield return null;
              yield return LoadAndWait();

              Assert.IsTrue(SaveRegistry.TryGet(id, out Saveable respawned));
              Assert.AreEqual("DontDestroyOnLoad", respawned.gameObject.scene.name);
              Assert.AreEqual(9, respawned.GetComponent<SaveTestCounter>().Value);
              Object.Destroy(respawned.gameObject);
          }

          [UnityTest]
          public IEnumerator Scene_Callbacks_BeforeSaveAndAfterLoad()
          {
              _system.Save();
              Assert.AreEqual(1, TestScenes.Find("Box").GetComponent<SaveTestCallbacks>().BeforeSave);

              yield return LoadAndWait();

              Assert.AreEqual(1, TestScenes.Find("Box").GetComponent<SaveTestCallbacks>().AfterLoad);
          }

          [UnityTest]
          public IEnumerator Scene_Save_WritesScenesAndSceneSection()
          {
              _system.Save();

              Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
              Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
              Assert.AreEqual(TestScenes.SCENE, (string)json["sections"][SceneSaving.SCENES_KEY]["data"]["active"]["path"]);
              Assert.IsNotNull(json["sections"][SceneKey]["data"]["objects"][TestScenes.HIDDEN]);
              yield break;
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — красный.** PlayMode: падают все 14 `SceneSavingTests` (`NotImplementedException` из
  `SceneSaving.CaptureInto` на `Save`/`Capture` — сервис ловит его и пишет ошибку `Saving the scene sections
  failed`, которую тест не ждёт; из `Check` — на `Load`). Остальные тесты зелёные.

- [x] **Шаг 7: реализация** — `Runtime/Objects/SceneSaving.cs` целиком:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using Object = UnityEngine.Object;

  namespace Saving.Objects
  {
      /// <summary>
      /// The objects layer inside the service. Writes "scene/&lt;GUID&gt;" sections (Saveables, destroyed and spawned
      /// objects), "$persistent" for DontDestroyOnLoad and "$scenes" with the active scene. A scene that loads gets its
      /// section from the snapshot in phases: objects on sceneLoaded (destroy, spawn, active flags), component data in
      /// each Saveable's late Start (disabled and already started ones at once), then OnAfterLoad. Load reloads the
      /// saved scene, or applies to it in place when it has just loaded and nothing in it has started.
      /// </summary>
      internal sealed class SceneSaving : ISaveExtension
      {
          public const string SCENES_KEY = "$scenes";
          public const string PERSISTENT_KEY = "$persistent";
          public const string SCENE_PREFIX = "scene/";

          private const int SECTION_VERSION = 1;
          private const string PERSISTENT_SCENE = "DontDestroyOnLoad";

          private readonly SaveLoadService _service;
          private readonly SaveSystemSettings _settings;
          private readonly ComponentSerializerCatalog _catalog;
          private readonly Dictionary<Saveable, ObjectData> _pending = new();
          private readonly List<Saveable> _restored = new();
          private readonly HashSet<string> _warned = new();

          private Action _finishLoad;
          private string _awaitedScenePath;

          public SceneSaving(SaveLoadService service, SaveSystemSettings settings, ComponentSerializerCatalog catalog)
          {
              _service = service ?? throw new ArgumentNullException(nameof(service));
              _settings = settings ? settings : throw new ArgumentNullException(nameof(settings));
              _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
              SaveRegistry.SceneScanned += OnSceneScanned;
              SaveRegistry.Ready += OnReady;
          }

          public void Dispose()
          {
              SaveRegistry.SceneScanned -= OnSceneScanned;
              SaveRegistry.Ready -= OnReady;
          }

          // ---------- Save ----------

          public void CaptureInto(IDictionary<string, SaveSection> sections)
          {
              List<Saveable> live = SaveRegistry.All();
              foreach (Saveable saveable in live)
                  Callbacks(saveable, beforeSave: true);

              foreach (SaveRegistry.SceneRecord record in SaveRegistry.Records())
              {
                  if (!record.Scene.isLoaded)
                      continue;

                  List<Saveable> inScene = SaveRegistry.InScene(record.Scene);
                  if (!TryGetGuid(record.Scene, out string guid))
                  {
                      if (inScene.Count > 0)
                          WarnOnce($"scene/{record.Scene.path}", $"The scene '{record.Scene.path}' is not in SaveSystemSettings, " +
                                                                 "its objects are not saved: Tools → Save System → Rebuild Catalogs.");
                      continue;
                  }

                  string key = SCENE_PREFIX + guid;
                  sections[key] = Section(CaptureObjects(inScene, record.Originals, ReadScene(sections, key)));
              }

              List<Saveable> persistent = live.Where(saveable => saveable.gameObject.scene.name == PERSISTENT_SCENE).ToList();
              if (persistent.Count > 0 || sections.ContainsKey(PERSISTENT_KEY))
                  sections[PERSISTENT_KEY] = Section(CaptureObjects(persistent, null, ReadScene(sections, PERSISTENT_KEY)));

              Scene active = SceneManager.GetActiveScene();
              if (TryGetGuid(active, out string activeGuid))
                  sections[SCENES_KEY] = Section(new ScenesData { Active = new SceneRef { Path = active.path, Guid = activeGuid } });
          }

          private SceneData CaptureObjects(List<Saveable> saveables, HashSet<string> originals, SceneData previous)
          {
              var data = new SceneData();
              var live = new HashSet<string>();
              foreach (Saveable saveable in saveables.OrderBy(saveable => saveable.Id, StringComparer.Ordinal))
              {
                  live.Add(saveable.Id);
                  if (!saveable.IsSpawned)
                  {
                      ObjectData old = previous != null && previous.Objects.TryGetValue(saveable.Id, out ObjectData found) ? found : null;
                      data.Objects[saveable.Id] = CaptureObject(saveable, old);
                  }
                  else if (IsTopSpawned(saveable) && saveable.gameObject.activeInHierarchy)
                  {
                      SpawnedData spawned = CaptureSpawned(saveable, previous);
                      if (spawned != null)
                          data.Spawned.Add(spawned);
                  }
              }

              if (originals != null)
                  data.Destroyed.AddRange(originals.Where(id => !live.Contains(id)).OrderBy(id => id, StringComparer.Ordinal));

              return data;
          }

          private ObjectData CaptureObject(Saveable saveable, ObjectData previous) =>
              _pending.TryGetValue(saveable, out ObjectData waiting) ? waiting : ObjectState.Capture(saveable, _catalog, previous);

          private SpawnedData CaptureSpawned(Saveable saveable, SceneData previous)
          {
              if (string.IsNullOrEmpty(saveable.PrefabId))
              {
                  WarnOnce($"no-prefab/{saveable.Id}", $"'{saveable.name}' was made in play from an object without a prefab id; " +
                                                       "it cannot be restored and is not saved.", saveable);
                  return null;
              }

              SpawnedData old = previous?.Spawned.FirstOrDefault(entry => entry.Id == saveable.Id);
              var spawned = new SpawnedData { Id = saveable.Id, Prefab = saveable.PrefabId };
              SetParent(saveable, spawned);
              spawned.CopyFrom(CaptureObject(saveable, old));

              foreach (Saveable nested in saveable.GetComponentsInChildren<Saveable>(true))
              {
                  if (nested == saveable)
                      continue;

                  string path = ComponentKeys.PathOf(saveable.transform, nested.transform);
                  NestedData oldNested = old != null && old.Nested.TryGetValue(path, out NestedData found) ? found : null;
                  var entry = new NestedData { Id = nested.Id };
                  entry.CopyFrom(CaptureObject(nested, oldNested));
                  spawned.Nested[path] = entry;
              }

              return spawned;
          }

          private static void SetParent(Saveable saveable, SpawnedData spawned)
          {
              Transform parent = saveable.transform.parent;
              if (parent == null)
                  return;

              Saveable owner = parent.GetComponentInParent<Saveable>(true);
              if (owner != null)
              {
                  spawned.Parent = owner.Id;
                  spawned.ParentPath = ComponentKeys.PathOf(owner.transform, parent);
              }
              else
              {
                  spawned.ParentPath = ComponentKeys.ScenePathOf(parent);
              }
          }

          private static bool IsTopSpawned(Saveable saveable)
          {
              Transform parent = saveable.transform.parent;
              Saveable owner = parent != null ? parent.GetComponentInParent<Saveable>(true) : null;
              return owner == null || !owner.IsSpawned;
          }

          // ---------- Load ----------

          public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
          {
              problem = null;
              foreach (KeyValuePair<string, SaveSection> pair in sections)
              {
                  bool isScene = pair.Key.StartsWith(SCENE_PREFIX, StringComparison.Ordinal) || pair.Key == PERSISTENT_KEY;
                  if (!isScene && pair.Key != SCENES_KEY)
                      continue;

                  if (pair.Value.V > SECTION_VERSION)
                  {
                      problem = $"'{pair.Key}' was saved with version {pair.Value.V}, newer than {SECTION_VERSION}";
                      return LoadOutcome.NewerVersion;
                  }

                  if (!isScene)
                      continue;

                  if (!TryRead(pair.Value, out SceneData scene))
                  {
                      problem = $"'{pair.Key}' cannot be read";
                      return LoadOutcome.Failed;
                  }

                  if (FindNewerComponent(scene, out string newer))
                  {
                      problem = $"'{pair.Key}': {newer}";
                      return LoadOutcome.NewerVersion;
                  }
              }

              if (!sections.TryGetValue(SCENES_KEY, out SaveSection scenesSection)
                  || !TryRead(scenesSection, out ScenesData scenes) || scenes.Active == null)
                  return LoadOutcome.Loaded;

              if (!SceneExists(PathOf(scenes.Active)))
              {
                  problem = $"the saved scene '{scenes.Active.Path}' is not in the build";
                  return LoadOutcome.SceneNotFound;
              }

              return LoadOutcome.Loaded;
          }

          public void BeginLoad(Action finished)
          {
              _finishLoad = finished;
              ApplyPersistent();

              if (!_service.TryGetSection(SCENES_KEY, out SaveSection section) || !TryRead(section, out ScenesData scenes)
                  || scenes.Active == null)
              {
                  TryFinish();
                  return;
              }

              string path = PathOf(scenes.Active);
              Scene active = SceneManager.GetActiveScene();
              if (active.path == path && SaveRegistry.TryGetRecord(active, out SaveRegistry.SceneRecord record) && record.IsFresh)
              {
                  ApplyScene(active, record);
                  TryFinish();
                  return;
              }

              _awaitedScenePath = path;
              try
              {
                  LoadScene(path);
              }
              catch
              {
                  _awaitedScenePath = null;
                  _finishLoad = null;
                  throw;
              }
          }

          private void OnSceneScanned(Scene scene)
          {
              if (_awaitedScenePath != null && scene.path == _awaitedScenePath)
                  _awaitedScenePath = null;

              if (SaveRegistry.TryGetRecord(scene, out SaveRegistry.SceneRecord record))
                  ApplyScene(scene, record);

              TryFinish();
          }

          private void ApplyScene(Scene scene, SaveRegistry.SceneRecord record)
          {
              if (!TryGetGuid(scene, out string guid) || !_service.TryGetSection(SCENE_PREFIX + guid, out SaveSection section))
                  return;

              if (!TryRead(section, out SceneData data))
              {
                  Debug.LogError($"The saved data of scene '{scene.path}' cannot be read; the scene stays as it is.");
                  return;
              }

              record.Applied = true;
              foreach (string id in data.Destroyed)
              {
                  if (SaveRegistry.TryGet(id, out Saveable doomed) && doomed.gameObject.scene == scene && !doomed.IsSpawned)
                  {
                      // Switched off first: its Start must not run in the frame before Destroy takes effect
                      doomed.gameObject.SetActive(false);
                      Object.Destroy(doomed.gameObject);
                  }
              }

              foreach (SpawnedData spawned in data.Spawned)
                  Spawn(spawned, scene);

              foreach (KeyValuePair<string, ObjectData> pair in data.Objects)
              {
                  if (SaveRegistry.TryGet(pair.Key, out Saveable saveable) && saveable.gameObject.scene == scene && !saveable.IsSpawned)
                      Assign(saveable, pair.Value);
              }
          }

          private void ApplyPersistent()
          {
              if (!_service.TryGetSection(PERSISTENT_KEY, out SaveSection section) || !TryRead(section, out SceneData data))
                  return;

              foreach (SpawnedData spawned in data.Spawned)
              {
                  if (SaveRegistry.TryGet(spawned.Id, out Saveable alive))
                  {
                      AssignNested(alive, spawned);
                      Assign(alive, spawned);
                  }
                  else
                  {
                      Spawn(spawned, default);
                  }
              }

              foreach (KeyValuePair<string, ObjectData> pair in data.Objects)
              {
                  if (SaveRegistry.TryGet(pair.Key, out Saveable saveable))
                      Assign(saveable, pair.Value);
              }
          }

          /// <summary>Makes a saved spawned object again; an invalid scene means DontDestroyOnLoad.</summary>
          private void Spawn(SpawnedData spawned, Scene scene)
          {
              if (!_settings.TryGetPrefab(spawned.Prefab, out GameObject prefab))
              {
                  WarnOnce($"prefab/{spawned.Prefab}", $"The prefab {spawned.Prefab} of the spawned object {spawned.Id} is not in " +
                                                       "SaveSystemSettings; the object is skipped.");
                  return;
              }

              Transform parent = FindParent(spawned, scene);
              GameObject instance = Object.Instantiate(prefab, parent);
              if (parent == null)
              {
                  if (!scene.IsValid())
                      Object.DontDestroyOnLoad(instance);
                  else if (instance.scene != scene)
                      SceneManager.MoveGameObjectToScene(instance, scene);
              }

              if (!instance.TryGetComponent(out Saveable root))
              {
                  Debug.LogError($"The prefab '{prefab.name}' has no Saveable on its root any more; the spawned object is skipped.");
                  Object.Destroy(instance);
                  return;
              }

              SaveRegistry.Rekey(root, spawned.Id);
              AssignNested(root, spawned);
              Assign(root, spawned);
          }

          private void AssignNested(Saveable root, SpawnedData spawned)
          {
              foreach (KeyValuePair<string, NestedData> pair in spawned.Nested)
              {
                  Transform node = ComponentKeys.FindChild(root.transform, pair.Key);
                  if (node == null || !node.TryGetComponent(out Saveable nested))
                  {
                      WarnOnce($"nested/{spawned.Prefab}/{pair.Key}", $"The prefab of '{root.name}' has no Saveable at {pair.Key} any " +
                                                                     "more; its saved data is skipped.", root);
                      continue;
                  }

                  if (nested.Id != pair.Value.Id)
                      SaveRegistry.Rekey(nested, pair.Value.Id);

                  Assign(nested, pair.Value);
              }
          }

          private static Transform FindParent(SpawnedData spawned, Scene scene)
          {
              if (!string.IsNullOrEmpty(spawned.Parent))
              {
                  if (!SaveRegistry.TryGet(spawned.Parent, out Saveable owner))
                      return null;

                  return string.IsNullOrEmpty(spawned.ParentPath)
                      ? owner.transform
                      : ComponentKeys.FindChild(owner.transform, spawned.ParentPath);
              }

              return scene.IsValid() ? ComponentKeys.FindInScene(scene, spawned.ParentPath) : null;
          }

          private void Assign(Saveable saveable, ObjectData data)
          {
              ObjectState.RestoreActive(saveable, data);
              if (saveable.HasStarted || !saveable.gameObject.activeInHierarchy)
                  Apply(saveable, data);
              else
                  _pending[saveable] = data;
          }

          private void Apply(Saveable saveable, ObjectData data)
          {
              ObjectState.RestoreComponents(saveable, data, _catalog);
              _restored.Add(saveable);
          }

          private void OnReady(Saveable saveable)
          {
              if (!_pending.Remove(saveable, out ObjectData data))
                  return;

              Apply(saveable, data);
              TryFinish();
          }

          /// <summary>When nothing waits any more: OnAfterLoad for what was restored, then the load ends.</summary>
          private void TryFinish()
          {
              foreach (Saveable gone in _pending.Keys.Where(saveable => saveable == null).ToList())
                  _pending.Remove(gone);

              if (_pending.Count > 0 || _awaitedScenePath != null)
                  return;

              foreach (Saveable saveable in _restored)
              {
                  if (saveable != null)
                      Callbacks(saveable, beforeSave: false);
              }

              _restored.Clear();
              Action finish = _finishLoad;
              _finishLoad = null;
              finish?.Invoke();
          }

          // ---------- Helpers ----------

          private static void Callbacks(Saveable saveable, bool beforeSave)
          {
              foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
              {
                  if (entry.Component is not ISaveCallbacks callbacks)
                      continue;

                  try
                  {
                      if (beforeSave)
                          callbacks.OnBeforeSave();
                      else
                          callbacks.OnAfterLoad();
                  }
                  catch (Exception exception)
                  {
                      Debug.LogException(exception, entry.Component);
                  }
              }
          }

          private bool FindNewerComponent(SceneData scene, out string newer)
          {
              IEnumerable<ObjectData> objects = scene.Objects.Values
                  .Concat(scene.Spawned)
                  .Concat(scene.Spawned.SelectMany(spawned => spawned.Nested.Values));

              foreach (ObjectData data in objects)
              {
                  foreach (KeyValuePair<string, ComponentData> pair in data.Components)
                  {
                      int current = _catalog.VersionOf(ComponentKeys.TypeNameOf(pair.Key));
                      if (pair.Value.V > current)
                      {
                          newer = $"{pair.Key} was saved with version {pair.Value.V}, newer than {current}";
                          return true;
                      }
                  }
              }

              newer = null;
              return false;
          }

          private bool TryGetGuid(Scene scene, out string guid)
          {
              guid = null;
              return scene.IsValid() && !string.IsNullOrEmpty(scene.path) && _settings.TryGetSceneGuid(scene.path, out guid);
          }

          private string PathOf(SceneRef scene) => _settings.TryGetScenePath(scene.Guid, out string path) ? path : scene.Path;

          private static bool SceneExists(string path)
          {
              if (string.IsNullOrEmpty(path))
                  return false;

  #if UNITY_EDITOR
              return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null;
  #else
              return SceneUtility.GetBuildIndexByScenePath(path) >= 0;
  #endif
          }

          private static void LoadScene(string path)
          {
  #if UNITY_EDITOR
              UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
  #else
              SceneManager.LoadScene(path, LoadSceneMode.Single);
  #endif
          }

          private static SaveSection Section(object data) => new() { V = SECTION_VERSION, Data = SaveJson.ToToken(data) };

          private static bool TryRead<T>(SaveSection section, out T data) where T : class
          {
              try
              {
                  data = SaveJson.FromToken<T>(section?.Data);
              }
              catch (Exception)
              {
                  data = null;
              }

              return data != null;
          }

          private static SceneData ReadScene(IDictionary<string, SaveSection> sections, string key) =>
              sections.TryGetValue(key, out SaveSection section) && TryRead(section, out SceneData data) ? data : null;

          private void WarnOnce(string key, string message, Object context = null)
          {
              if (_warned.Add(key))
                  Debug.LogWarning(message, context);
          }
      }
  }
  ```

- [x] **Шаг 8: запуск — зелёный.** All Tests: `failed 0`. Если `Scene_LoadRightAfterSceneLoad_AppliesInPlace`
  видит две загрузки — проверить, что реестр подписан на `sceneLoaded` раньше теста (он подписывается в
  `SubsystemRegistration`) и что `Time.frameCount` в `sceneLoaded` и при вызове `Load` один; иначе — остановиться и
  обсудить, как определять «свежую» сцену.

- [x] **Шаг 9: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Save scene objects: destroyed, spawned, persistent, loads by phases"
  ```

---

### Задача 7: эдитор — ID, ID префабов, каталоги, проверка сцен, меню

**Files:**
- Modify: `Runtime/Objects/Saveable.cs` (хук эдитора: `OnValidate`, `Reset`)
- Create: `Editor/SaveableIdAssigner.cs`, `Editor/SaveCatalogBuilder.cs`, `Editor/SaveValidator.cs`,
  `Editor/SaveCatalogPostprocessor.cs`, `Editor/SaveCatalogBuildStep.cs`
- Modify: `Editor/SaveSystemMenu.cs` (+ Create Settings, Rebuild Catalogs, Validate Scenes)
- Test: `Tests/Editor/SaveableIdTests.cs`, `Tests/Editor/SaveCatalogTests.cs`

**Interfaces:**
- Consumes: `Saveable`, `SaveRegistry.NewId` (задачи 4–5), `SaveSystemSettings` (задача 6).
- Produces:
  - `Saveable.EditorValidate` (`internal static Action<Saveable>`, только `UNITY_EDITOR`) — зовётся из `OnValidate` и
    `Reset`.
  - `SaveableIdAssigner.Assign(Saveable) → bool` (изменил ли), `SaveableIdAssigner.IsUsedElsewhere(Saveable, string id)
    → bool`, `SaveableIdAssigner.IsPrefabSide(Saveable) → bool`.
  - `SaveCatalogBuilder.BakePrefabIds(IEnumerable<string> prefabPaths = null) → int`, `Rebuild(SaveSystemSettings)`,
    `BuildPrefabEntries()`, `BuildSceneEntries()`, `AllSettings()`, `PrefabPathsWithSaveable()`.
  - `SaveValidator.ValidateScene(Scene) → List<string>`, `ValidateScenes(IEnumerable<string> paths) → List<string>`,
    `ValidateCatalogs(SaveSystemSettings) → List<string>`.
- Правила: объект сцены получает новый ID, если ID пуст или занят другим `Saveable` в открытых сценах (через
  `SerializedObject` — записывается как override экземпляра префаба, с Undo); в ассете префаба и в режиме
  редактирования префаба ID пустой. Изменение — в `EditorApplication.delayCall` (в самом `OnValidate` менять
  сериализованные данные нельзя). Запекание: у корневого `Saveable` префаба `_prefabId` = GUID ассета (у варианта —
  свой), `_id` пустой; у вложенных `Saveable` префаба оба пустые. Каталог префабов — префабы с `Saveable` на корне,
  каталог сцен — все сцены в `Assets/`; оба сортируются (по ID и пути), чтобы ассет не менялся без нужды.
  Пересборка — после импорта/переноса/удаления префабов и сцен (отложенно, один раз на пачку) и перед сборкой.

- [x] **Шаг 1: хук в `Saveable`.** В `Runtime/Objects/Saveable.cs`: `using System;` и после `SetId`:

  ```csharp
  #if UNITY_EDITOR
          /// <summary>Set by the editor: gives scene objects an id and keeps prefab assets without one.</summary>
          internal static Action<Saveable> EditorValidate;

          private void OnValidate() => EditorValidate?.Invoke(this);
          private void Reset() => EditorValidate?.Invoke(this);
  #endif
  ```

- [x] **Шаг 2: заглушки.**

  `Editor/SaveableIdAssigner.cs`:

  ```csharp
  using System;
  using Saving.Objects;

  namespace Saving.Editor
  {
      public static class SaveableIdAssigner
      {
          public static bool Assign(Saveable saveable) => throw new NotImplementedException();
          public static bool IsUsedElsewhere(Saveable saveable, string id) => throw new NotImplementedException();
          public static bool IsPrefabSide(Saveable saveable) => throw new NotImplementedException();
      }
  }
  ```

  `Editor/SaveCatalogBuilder.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Saving.Objects;

  namespace Saving.Editor
  {
      public static class SaveCatalogBuilder
      {
          public static int BakePrefabIds(IEnumerable<string> prefabPaths = null) => throw new NotImplementedException();
          public static void Rebuild(SaveSystemSettings settings) => throw new NotImplementedException();
          public static List<SaveSystemSettings.PrefabEntry> BuildPrefabEntries() => throw new NotImplementedException();
          public static List<SaveSystemSettings.SceneEntry> BuildSceneEntries() => throw new NotImplementedException();
          public static List<SaveSystemSettings> AllSettings() => new(); // the tests' TearDown calls it
          public static List<string> PrefabPathsWithSaveable() => throw new NotImplementedException();
      }
  }
  ```

  `Editor/SaveValidator.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Saving.Objects;
  using UnityEngine.SceneManagement;

  namespace Saving.Editor
  {
      public static class SaveValidator
      {
          public static List<string> ValidateScene(Scene scene) => throw new NotImplementedException();
          public static List<string> ValidateScenes(IEnumerable<string> paths) => throw new NotImplementedException();
          public static List<string> ValidateCatalogs(SaveSystemSettings settings) => throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 3: падающие тесты.**

  `Tests/Editor/SaveableIdTests.cs`:

  ```csharp
  using System.Collections;
  using NUnit.Framework;
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>Ids in the editor: new objects get one, duplicates get a new one, unique ones stay.</summary>
      public class SaveableIdTests
      {
          private TempScene _scene;

          [SetUp]
          public void SetUp() => _scene = new TempScene();

          [TearDown]
          public void TearDown() => _scene.Dispose();

          private static Saveable Make(string name) => new GameObject(name).AddComponent<Saveable>();

          private static void SetId(Saveable saveable, string id)
          {
              var serialized = new SerializedObject(saveable);
              serialized.FindProperty("_id").stringValue = id;
              serialized.ApplyModifiedPropertiesWithoutUndo();
          }

          [Test]
          public void Ids_EmptyId_GetsNewOne()
          {
              Saveable saveable = Make("New");
              SetId(saveable, string.Empty);

              Assert.IsTrue(SaveableIdAssigner.Assign(saveable));
              Assert.AreEqual(32, saveable.Id.Length);
          }

          [Test]
          public void Ids_Duplicate_GetsNewOneAndFirstKeepsIt()
          {
              Saveable first = Make("First");
              Saveable second = Make("Second");
              SetId(first, "11111111111111111111111111111111");
              SetId(second, "11111111111111111111111111111111");

              Assert.IsTrue(SaveableIdAssigner.Assign(second));

              Assert.AreEqual("11111111111111111111111111111111", first.Id);
              Assert.AreNotEqual(first.Id, second.Id);
          }

          [Test]
          public void Ids_UniqueId_Kept()
          {
              Saveable saveable = Make("Unique");
              SetId(saveable, "22222222222222222222222222222222");

              Assert.IsFalse(SaveableIdAssigner.Assign(saveable));
              Assert.AreEqual("22222222222222222222222222222222", saveable.Id);
          }

          [UnityTest]
          public IEnumerator Ids_AddedInEditor_AssignedOnItsOwn()
          {
              Saveable saveable = Make("Added");

              yield return null; // the assignment runs in EditorApplication.delayCall

              Assert.AreEqual(32, saveable.Id?.Length ?? 0);
          }
      }
  }
  ```

  `Tests/Editor/SaveCatalogTests.cs`:

  ```csharp
  using System.Linq;
  using NUnit.Framework;
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>Prefab ids baked into assets, the catalogs of SaveSystemSettings and the scene check.</summary>
      public class SaveCatalogTests
      {
          private const string PARENT = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor";
          private const string FOLDER = PARENT + "/TempAssets";

          private TempScene _scene;

          [SetUp]
          public void SetUp()
          {
              _scene = new TempScene();
              AssetDatabase.CreateFolder(PARENT, "TempAssets");
          }

          [TearDown]
          public void TearDown()
          {
              _scene.Dispose();
              AssetDatabase.DeleteAsset(FOLDER);
              // The project's settings may have picked up the temporary prefabs: put their catalogs back in order
              foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                  SaveCatalogBuilder.Rebuild(settings);
          }

          private static string MakePrefab(string name, bool withSaveable = true, bool withNested = false)
          {
              var root = new GameObject(name);
              if (withSaveable)
                  root.AddComponent<Saveable>().SetId("33333333333333333333333333333333");

              if (withNested)
              {
                  var child = new GameObject("Child");
                  child.transform.SetParent(root.transform, false);
                  child.AddComponent<Saveable>().SetId("44444444444444444444444444444444");
              }

              string path = $"{FOLDER}/{name}.prefab";
              PrefabUtility.SaveAsPrefabAsset(root, path);
              Object.DestroyImmediate(root);
              return path;
          }

          private static Saveable RootOf(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Saveable>();

          [Test]
          public void Bake_Prefab_PrefabIdIsItsGuidAndNoIds()
          {
              string path = MakePrefab("Thing", withNested: true);

              SaveCatalogBuilder.BakePrefabIds(new[] { path });

              Saveable root = RootOf(path);
              Saveable child = root.transform.Find("Child").GetComponent<Saveable>();
              Assert.AreEqual(AssetDatabase.AssetPathToGUID(path), root.PrefabId);
              Assert.AreEqual(string.Empty, root.Id);
              Assert.AreEqual(string.Empty, child.Id);
              Assert.AreEqual(string.Empty, child.PrefabId);
          }

          [Test]
          public void Bake_Variant_HasItsOwnPrefabId()
          {
              string basePath = MakePrefab("Base");
              var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));
              string variantPath = $"{FOLDER}/Variant.prefab";
              PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
              Object.DestroyImmediate(instance);

              SaveCatalogBuilder.BakePrefabIds(new[] { basePath, variantPath });

              Assert.AreEqual(AssetDatabase.AssetPathToGUID(variantPath), RootOf(variantPath).PrefabId);
              Assert.AreNotEqual(RootOf(basePath).PrefabId, RootOf(variantPath).PrefabId);
          }

          [Test]
          public void Ids_PrefabAsset_KeepsNoId()
          {
              string path = MakePrefab("Asset");

              SaveableIdAssigner.Assign(RootOf(path));

              Assert.AreEqual(string.Empty, RootOf(path).Id);
          }

          [Test]
          public void Rebuild_FindsSaveablePrefabsAndScenes()
          {
              string withSaveable = MakePrefab("Saved");
              string plain = MakePrefab("Plain", withSaveable: false);
              var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();

              SaveCatalogBuilder.Rebuild(settings);

              Assert.IsTrue(settings.TryGetPrefab(AssetDatabase.AssetPathToGUID(withSaveable), out GameObject prefab));
              Assert.AreEqual(withSaveable, AssetDatabase.GetAssetPath(prefab));
              Assert.IsFalse(settings.TryGetPrefab(AssetDatabase.AssetPathToGUID(plain), out _));
              const string testScene = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/Scenes/SaveTestScene.unity";
              Assert.IsTrue(settings.TryGetSceneGuid(testScene, out string guid));
              Assert.AreEqual(AssetDatabase.AssetPathToGUID(testScene), guid);
              Object.DestroyImmediate(settings);
          }

          [Test]
          public void Validate_StaleCatalog_Reported()
          {
              var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              SaveCatalogBuilder.Rebuild(settings);
              Assert.IsEmpty(SaveValidator.ValidateCatalogs(settings));

              MakePrefab("Late");

              Assert.IsTrue(SaveValidator.ValidateCatalogs(settings).Any(problem => problem.Contains("Late")));
              Object.DestroyImmediate(settings);
          }

          [Test]
          public void Validate_EmptyAndRepeatedIds_Reported()
          {
              var empty = new GameObject("Empty").AddComponent<Saveable>();
              empty.SetId(string.Empty);
              new GameObject("A").AddComponent<Saveable>().SetId("55555555555555555555555555555555");
              new GameObject("B").AddComponent<Saveable>().SetId("55555555555555555555555555555555");

              var problems = SaveValidator.ValidateScene(_scene.Scene);

              Assert.AreEqual(2, problems.Count);
              Assert.IsTrue(problems.Any(problem => problem.Contains("'Empty' has no id")));
              Assert.IsTrue(problems.Any(problem => problem.Contains("'B' repeats the id of 'A'")));
          }
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** EditMode: падают 4 `SaveableIdTests` и 6 `SaveCatalogTests`
  (`NotImplementedException`; `Ids_AddedInEditor_AssignedOnItsOwn` — пустой ID: хук ещё никто не выставил).

- [x] **Шаг 5: реализация.**

  `Editor/SaveableIdAssigner.cs`:

  ```csharp
  using Saving.Objects;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  namespace Saving.Editor
  {
      /// <summary>
      /// Ids of scene Saveables in the editor: a new one when empty or used by another Saveable in the open scenes,
      /// written as a prefab instance override with Undo. Prefab assets and objects in prefab mode have no id: their
      /// instances get one in the scene, copies made in play get one in Awake.
      /// </summary>
      [InitializeOnLoad]
      public static class SaveableIdAssigner
      {
          static SaveableIdAssigner() => Saveable.EditorValidate = OnValidate;

          private static void OnValidate(Saveable saveable)
          {
              // OnValidate must not change serialized data: do it right after
              EditorApplication.delayCall += () =>
              {
                  if (saveable != null)
                      Assign(saveable);
              };
          }

          public static bool Assign(Saveable saveable)
          {
              if (saveable == null || EditorApplication.isPlayingOrWillChangePlaymode)
                  return false;

              var serialized = new SerializedObject(saveable);
              SerializedProperty id = serialized.FindProperty("_id");
              if (IsPrefabSide(saveable))
              {
                  if (string.IsNullOrEmpty(id.stringValue))
                      return false;

                  id.stringValue = string.Empty;
              }
              else
              {
                  if (!string.IsNullOrEmpty(id.stringValue) && !IsUsedElsewhere(saveable, id.stringValue))
                      return false;

                  id.stringValue = SaveRegistry.NewId();
              }

              serialized.ApplyModifiedProperties();
              return true;
          }

          public static bool IsPrefabSide(Saveable saveable) =>
              PrefabUtility.IsPartOfPrefabAsset(saveable) || PrefabStageUtility.GetPrefabStage(saveable.gameObject) != null;

          public static bool IsUsedElsewhere(Saveable saveable, string id)
          {
              for (int index = 0; index < SceneManager.sceneCount; index++)
              {
                  Scene scene = SceneManager.GetSceneAt(index);
                  if (!scene.isLoaded)
                      continue;

                  foreach (GameObject root in scene.GetRootGameObjects())
                  {
                      foreach (Saveable other in root.GetComponentsInChildren<Saveable>(true))
                      {
                          if (other != saveable && other.Id == id)
                              return true;
                      }
                  }
              }

              return false;
          }
      }
  }
  ```

  `Editor/SaveCatalogBuilder.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;

  namespace Saving.Editor
  {
      /// <summary>
      /// Keeps prefab ids and the catalogs of SaveSystemSettings in order: a prefab with a Saveable on its root has its
      /// asset GUID as prefab id (a variant has its own), ids inside prefab assets stay empty; the catalogs list those
      /// prefabs and every scene in Assets. Runs after imports, before builds and from the menu.
      /// </summary>
      public static class SaveCatalogBuilder
      {
          private static readonly string[] Root = { "Assets" };

          public static int BakePrefabIds(IEnumerable<string> prefabPaths = null)
          {
              int changed = 0;
              foreach (string path in prefabPaths ?? PrefabPathsWithSaveable())
              {
                  var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                  if (prefab == null)
                      continue;

                  bool dirty = false;
                  string guid = AssetDatabase.AssetPathToGUID(path);
                  foreach (Saveable saveable in prefab.GetComponentsInChildren<Saveable>(true))
                  {
                      var serialized = new SerializedObject(saveable);
                      bool isRoot = saveable.transform == prefab.transform;
                      bool changedHere = SetString(serialized, "_id", string.Empty)
                                         | SetString(serialized, "_prefabId", isRoot ? guid : string.Empty);
                      if (!changedHere)
                          continue;

                      serialized.ApplyModifiedPropertiesWithoutUndo();
                      dirty = true;
                  }

                  if (!dirty)
                      continue;

                  PrefabUtility.SavePrefabAsset(prefab);
                  changed++;
              }

              return changed;
          }

          public static void Rebuild(SaveSystemSettings settings)
          {
              List<SaveSystemSettings.PrefabEntry> prefabs = BuildPrefabEntries();
              List<SaveSystemSettings.SceneEntry> scenes = BuildSceneEntries();
              if (Same(settings, prefabs, scenes))
                  return;

              settings.SetCatalogs(prefabs, scenes);
              if (!EditorUtility.IsPersistent(settings))
                  return;

              EditorUtility.SetDirty(settings);
              AssetDatabase.SaveAssetIfDirty(settings);
          }

          public static List<SaveSystemSettings.PrefabEntry> BuildPrefabEntries()
          {
              var entries = new List<SaveSystemSettings.PrefabEntry>();
              foreach (string path in PrefabPathsWithSaveable())
              {
                  var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                  if (prefab != null && prefab.TryGetComponent(out Saveable _))
                      entries.Add(new SaveSystemSettings.PrefabEntry { Id = AssetDatabase.AssetPathToGUID(path), Prefab = prefab });
              }

              return entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToList();
          }

          public static List<SaveSystemSettings.SceneEntry> BuildSceneEntries() =>
              AssetDatabase.FindAssets("t:Scene", Root)
                  .Select(AssetDatabase.GUIDToAssetPath)
                  .OrderBy(path => path, StringComparer.Ordinal)
                  .Select(path => new SaveSystemSettings.SceneEntry { Guid = AssetDatabase.AssetPathToGUID(path), Path = path })
                  .ToList();

          public static List<SaveSystemSettings> AllSettings() =>
              AssetDatabase.FindAssets($"t:{nameof(SaveSystemSettings)}")
                  .Select(guid => AssetDatabase.LoadAssetAtPath<SaveSystemSettings>(AssetDatabase.GUIDToAssetPath(guid)))
                  .Where(settings => settings != null)
                  .ToList();

          /// <summary>Prefabs that depend on the Saveable script, found without loading every prefab of the project.</summary>
          public static List<string> PrefabPathsWithSaveable()
          {
              string script = SaveableScriptPath();
              return AssetDatabase.FindAssets("t:Prefab", Root)
                  .Select(AssetDatabase.GUIDToAssetPath)
                  .Where(path => AssetDatabase.GetDependencies(path, recursive: true).Contains(script))
                  .ToList();
          }

          private static string SaveableScriptPath()
          {
              var holder = new GameObject { hideFlags = HideFlags.HideAndDontSave };
              try
              {
                  return AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(holder.AddComponent<Saveable>()));
              }
              finally
              {
                  UnityEngine.Object.DestroyImmediate(holder);
              }
          }

          private static bool SetString(SerializedObject serialized, string name, string value)
          {
              SerializedProperty property = serialized.FindProperty(name);
              if (property.stringValue == value)
                  return false;

              property.stringValue = value;
              return true;
          }

          private static bool Same(SaveSystemSettings settings, List<SaveSystemSettings.PrefabEntry> prefabs,
              List<SaveSystemSettings.SceneEntry> scenes) =>
              settings.Prefabs.Count == prefabs.Count && settings.Scenes.Count == scenes.Count
              && settings.Prefabs.Zip(prefabs, (a, b) => a.Id == b.Id && a.Prefab == b.Prefab).All(same => same)
              && settings.Scenes.Zip(scenes, (a, b) => a.Guid == b.Guid && a.Path == b.Path).All(same => same);
      }
  }
  ```

  `Editor/SaveValidator.cs`:

  ```csharp
  using System.Collections.Generic;
  using System.Linq;
  using Saving.Objects;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  namespace Saving.Editor
  {
      /// <summary>Finds what breaks saves before the game does: empty and repeated ids in scenes, stale catalogs.</summary>
      public static class SaveValidator
      {
          public static List<string> ValidateScene(Scene scene)
          {
              var problems = new List<string>();
              var seen = new Dictionary<string, Saveable>();
              foreach (GameObject root in scene.GetRootGameObjects())
              {
                  foreach (Saveable saveable in root.GetComponentsInChildren<Saveable>(true))
                  {
                      string path = ComponentKeys.ScenePathOf(saveable.transform);
                      if (string.IsNullOrEmpty(saveable.Id))
                          problems.Add($"{scene.path}: '{path}' has no id");
                      else if (seen.TryGetValue(saveable.Id, out Saveable first))
                          problems.Add($"{scene.path}: '{path}' repeats the id of '{ComponentKeys.ScenePathOf(first.transform)}'");
                      else
                          seen.Add(saveable.Id, saveable);
                  }
              }

              return problems;
          }

          /// <summary>Checks scenes by path; scenes that are not open are opened additively and closed again.</summary>
          public static List<string> ValidateScenes(IEnumerable<string> paths)
          {
              var problems = new List<string>();
              foreach (string path in paths.Distinct())
              {
                  Scene scene = SceneManager.GetSceneByPath(path);
                  bool opened = !scene.isLoaded;
                  if (opened)
                      scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                  problems.AddRange(ValidateScene(scene));
                  if (opened)
                      EditorSceneManager.CloseScene(scene, removeScene: true);
              }

              return problems;
          }

          public static List<string> ValidateCatalogs(SaveSystemSettings settings)
          {
              var problems = new List<string>();
              List<SaveSystemSettings.PrefabEntry> prefabs = SaveCatalogBuilder.BuildPrefabEntries();
              List<SaveSystemSettings.SceneEntry> scenes = SaveCatalogBuilder.BuildSceneEntries();

              foreach (SaveSystemSettings.PrefabEntry entry in prefabs.Where(entry => !settings.Prefabs.Any(known => known.Id == entry.Id && known.Prefab == entry.Prefab)))
                  problems.Add($"{settings.name}: the prefab '{entry.Prefab.name}' is missing from the catalog");
              foreach (SaveSystemSettings.PrefabEntry entry in settings.Prefabs.Where(known => !prefabs.Any(entry => entry.Id == known.Id)))
                  problems.Add($"{settings.name}: the prefab {entry.Id} is in the catalog but has no Saveable any more");
              foreach (SaveSystemSettings.SceneEntry entry in scenes.Where(entry => !settings.Scenes.Any(known => known.Guid == entry.Guid && known.Path == entry.Path)))
                  problems.Add($"{settings.name}: the scene '{entry.Path}' is missing from the catalog");
              foreach (SaveSystemSettings.SceneEntry entry in settings.Scenes.Where(known => !scenes.Any(entry => entry.Guid == known.Guid)))
                  problems.Add($"{settings.name}: the scene '{entry.Path}' is in the catalog but not in the project");

              return problems;
          }
      }
  }
  ```

  `Editor/SaveCatalogPostprocessor.cs`:

  ```csharp
  using System.Collections.Generic;
  using System.Linq;
  using Saving.Objects;
  using UnityEditor;

  namespace Saving.Editor
  {
      /// <summary>After prefabs or scenes are imported, moved or deleted: bake prefab ids and rebuild the catalogs, once per batch.</summary>
      internal sealed class SaveCatalogPostprocessor : AssetPostprocessor
      {
          private static readonly HashSet<string> TouchedPrefabs = new();
          private static bool _scheduled;

          private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
          {
              IEnumerable<string> all = imported.Concat(deleted).Concat(moved).Concat(movedFrom);
              if (!all.Any(path => path.EndsWith(".prefab") || path.EndsWith(".unity")))
                  return;

              foreach (string path in imported.Concat(moved).Where(path => path.EndsWith(".prefab")))
                  TouchedPrefabs.Add(path);

              if (_scheduled)
                  return;

              _scheduled = true;
              EditorApplication.delayCall += Run;
          }

          private static void Run()
          {
              _scheduled = false;
              if (EditorApplication.isPlayingOrWillChangePlaymode)
                  return;

              string[] prefabs = TouchedPrefabs.ToArray();
              TouchedPrefabs.Clear();
              SaveCatalogBuilder.BakePrefabIds(prefabs);
              foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                  SaveCatalogBuilder.Rebuild(settings);
          }
      }
  }
  ```

  `Editor/SaveCatalogBuildStep.cs`:

  ```csharp
  using Saving.Objects;
  using UnityEditor.Build;
  using UnityEditor.Build.Reporting;

  namespace Saving.Editor
  {
      /// <summary>Before a build: prefab ids and catalogs up to date, so the player finds every prefab and scene.</summary>
      internal sealed class SaveCatalogBuildStep : IPreprocessBuildWithReport
      {
          public int callbackOrder => 0;

          public void OnPreprocessBuild(BuildReport report)
          {
              SaveCatalogBuilder.BakePrefabIds();
              foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                  SaveCatalogBuilder.Rebuild(settings);
          }
      }
  }
  ```

  В `Editor/SaveSystemMenu.cs`: `using System.Linq;`, `using Saving.Objects;`, `using UnityEditor.SceneManagement;`,
  `using UnityEngine.SceneManagement;` и методы:

  ```csharp
          [MenuItem("Tools/Save System/Create Settings")]
          public static void CreateSettings()
          {
              SaveSystemSettings existing = SaveCatalogBuilder.AllSettings().FirstOrDefault();
              if (existing != null)
              {
                  Selection.activeObject = existing;
                  EditorGUIUtility.PingObject(existing);
                  return;
              }

              string path = EditorUtility.SaveFilePanelInProject("Save System Settings", "SaveSystemSettings", "asset",
                  "Where to keep the save settings; set the asset on SaveLoadSystem.");
              if (string.IsNullOrEmpty(path))
                  return;

              var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              AssetDatabase.CreateAsset(settings, path);
              SaveCatalogBuilder.Rebuild(settings);
              Selection.activeObject = settings;
          }

          [MenuItem("Tools/Save System/Rebuild Catalogs")]
          public static void RebuildCatalogs()
          {
              int baked = SaveCatalogBuilder.BakePrefabIds();
              foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                  SaveCatalogBuilder.Rebuild(settings);

              Debug.Log($"Save catalogs rebuilt; prefab ids fixed in {baked} prefab(s).");
          }

          [MenuItem("Tools/Save System/Validate Scenes")]
          public static void ValidateScenes()
          {
              var paths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToList();
              for (int index = 0; index < SceneManager.sceneCount; index++)
                  paths.Add(SceneManager.GetSceneAt(index).path);

              var problems = SaveValidator.ValidateScenes(paths.Where(path => !string.IsNullOrEmpty(path)));
              foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                  problems.AddRange(SaveValidator.ValidateCatalogs(settings));

              foreach (string problem in problems)
                  Debug.LogError(problem);

              if (problems.Count == 0)
                  Debug.Log("Save System: no problems found in the scenes and catalogs.");
          }
  ```

- [x] **Шаг 6: запуск — зелёный.** EditMode: `failed 0`. Затем меню *Tools → Save System → Rebuild Catalogs* через
  `EditorApplication.ExecuteMenuItem` и `git status`: тестовый префаб модуля мог получить `_prefabId` (он уже равен
  GUID — изменений быть не должно); тестовая сцена — без изменений.

- [x] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Assign Saveable ids in the editor and keep catalogs up to date"
  ```

---

### Задача 8: инспекторы `Saveable` и `SaveSystemSettings`

**Files:**
- Create: `Editor/SaveableEditor.cs`, `Editor/SaveSystemSettingsEditor.cs`
- Test: `Tests/Editor/SaveableEditorTests.cs`

**Interfaces:**
- Consumes: `ComponentKeys`, `ComponentSerializerCatalog.KindOf`, `Saveable.IsSaved/SetSaved` (задачи 3–4),
  `SaveableIdAssigner` (задача 7), `SaveCatalogBuilder.Rebuild` (задача 7).
- Produces: `SaveableEditor.BuildRows(Saveable, ComponentSerializerCatalog) → List<SaveableEditor.Row>`;
  `SaveableEditor.Row { string Key; SerializerKind Kind; bool Saved; }`.
- Инспектор `Saveable`: ID (только чтение) и кнопка «New ID» (не на префабе и не в игре), ID префаба, список
  компонентов, которые умеют сохраняться, с флажками и видом сериализатора, предупреждения: нет ID, ID повторяется в
  открытых сценах, нечего сохранять. В игре — «Spawned / Scene object». Инспектор настроек: списки каталогов только
  для чтения и кнопка «Rebuild Catalogs». Оба на IMGUI без Odin (Odin в 6.5 не рисует списки).
  Класс инспектора наследует `UnityEditor.Editor` полным именем: внутри namespace `Saving.Editor` короткое
  `Editor` означает сам namespace.

- [x] **Шаг 1: заглушка** — `Editor/SaveableEditor.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Saving.Objects;
  using UnityEditor;

  namespace Saving.Editor
  {
      [CustomEditor(typeof(Saveable))]
      public sealed class SaveableEditor : UnityEditor.Editor
      {
          public readonly struct Row
          {
              public Row(string key, SerializerKind kind, bool saved)
              {
                  Key = key;
                  Kind = kind;
                  Saved = saved;
              }

              public string Key { get; }
              public SerializerKind Kind { get; }
              public bool Saved { get; }
          }

          public static List<Row> BuildRows(Saveable saveable, ComponentSerializerCatalog catalog) =>
              throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 2: падающий тест** — `Tests/Editor/SaveableEditorTests.cs`:

  ```csharp
  using System.Linq;
  using NUnit.Framework;
  using Saving.Editor;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>What the Saveable inspector lists: savable components with their kind and checkbox.</summary>
      public class SaveableEditorTests
      {
          private TempScene _scene;

          [SetUp]
          public void SetUp() => _scene = new TempScene();

          [TearDown]
          public void TearDown() => _scene.Dispose();

          [Test]
          public void Rows_SavableComponentsWithKindAndCheckbox()
          {
              var root = new GameObject("Root");
              var saveable = root.AddComponent<Saveable>();
              root.AddComponent<FieldsProbe>();
              root.AddComponent<SerializerProbe>();
              root.AddComponent<PlainProbe>();
              saveable.SetSaved("SerializerProbe", false);

              var rows = SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default)
                  .ToDictionary(row => row.Key);

              Assert.AreEqual(SerializerKind.BuiltIn, rows["Transform"].Kind);
              Assert.AreEqual(SerializerKind.Fields, rows["FieldsProbe"].Kind);
              Assert.AreEqual(SerializerKind.Own, rows["SerializerProbe"].Kind);
              Assert.IsFalse(rows["SerializerProbe"].Saved);
              Assert.IsTrue(rows["FieldsProbe"].Saved);
              Assert.IsFalse(rows.ContainsKey("PlainProbe"));
          }
      }
  }
  ```

- [x] **Шаг 3: запуск — красный.** EditMode: `Rows_SavableComponentsWithKindAndCheckbox` падает с
  `NotImplementedException`.

- [x] **Шаг 4: реализация.**

  `Editor/SaveableEditor.cs`:

  ```csharp
  using System.Collections.Generic;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;

  namespace Saving.Editor
  {
      /// <summary>
      /// Saveable in the Inspector: its id with a "New ID" button, its prefab id, the components it saves with a
      /// checkbox and who saves them, and warnings about a missing or repeated id.
      /// </summary>
      [CustomEditor(typeof(Saveable))]
      public sealed class SaveableEditor : UnityEditor.Editor
      {
          public readonly struct Row
          {
              public Row(string key, SerializerKind kind, bool saved)
              {
                  Key = key;
                  Kind = kind;
                  Saved = saved;
              }

              public string Key { get; }
              public SerializerKind Kind { get; }
              public bool Saved { get; }
          }

          /// <summary>Every component the Saveable covers that can be saved, with its checkbox.</summary>
          public static List<Row> BuildRows(Saveable saveable, ComponentSerializerCatalog catalog)
          {
              var rows = new List<Row>();
              foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
              {
                  SerializerKind kind = catalog.KindOf(entry.Component.GetType());
                  if (kind != SerializerKind.None)
                      rows.Add(new Row(entry.Key, kind, saveable.IsSaved(entry.Key)));
              }

              return rows;
          }

          public override void OnInspectorGUI()
          {
              var saveable = (Saveable)target;
              serializedObject.Update();
              SerializedProperty id = serializedObject.FindProperty("_id");
              SerializedProperty prefabId = serializedObject.FindProperty("_prefabId");
              SerializedProperty excluded = serializedObject.FindProperty("_excluded");
              bool prefabSide = SaveableIdAssigner.IsPrefabSide(saveable);

              using (new EditorGUILayout.HorizontalScope())
              {
                  EditorGUILayout.LabelField("ID", prefabSide ? "given to each instance in a scene" : id.stringValue);
                  using (new EditorGUI.DisabledScope(prefabSide || Application.isPlaying))
                  {
                      if (GUILayout.Button("New ID", GUILayout.Width(70f)))
                          id.stringValue = SaveRegistry.NewId();
                  }
              }

              EditorGUILayout.LabelField("Prefab", string.IsNullOrEmpty(prefabId.stringValue) ? "-" : prefabId.stringValue);
              if (Application.isPlaying)
                  EditorGUILayout.LabelField("In play", saveable.IsSpawned ? "Spawned" : "Scene object");

              EditorGUILayout.Space();
              EditorGUILayout.LabelField("Saved components", EditorStyles.boldLabel);
              List<Row> rows = BuildRows(saveable, ComponentSerializerCatalog.Default);
              if (rows.Count == 0)
                  EditorGUILayout.HelpBox("Nothing here can be saved: add [Save] fields or a ComponentSerializer.", MessageType.Info);

              foreach (Row row in rows)
              {
                  bool saved = EditorGUILayout.ToggleLeft($"{row.Key}   ({Describe(row.Kind)})", row.Saved);
                  if (saved != row.Saved)
                      SetExcluded(excluded, row.Key, !saved);
              }

              if (!prefabSide && !Application.isPlaying)
              {
                  if (string.IsNullOrEmpty(id.stringValue))
                      EditorGUILayout.HelpBox("No id yet: it is given in a moment.", MessageType.Warning);
                  else if (SaveableIdAssigner.IsUsedElsewhere(saveable, id.stringValue))
                      EditorGUILayout.HelpBox("Another Saveable in the open scenes has this id: press New ID.", MessageType.Error);
              }

              serializedObject.ApplyModifiedProperties();
          }

          private static void SetExcluded(SerializedProperty excluded, string key, bool exclude)
          {
              for (int index = 0; index < excluded.arraySize; index++)
              {
                  if (excluded.GetArrayElementAtIndex(index).stringValue != key)
                      continue;

                  if (!exclude)
                      excluded.DeleteArrayElementAtIndex(index);

                  return;
              }

              if (!exclude)
                  return;

              excluded.InsertArrayElementAtIndex(excluded.arraySize);
              excluded.GetArrayElementAtIndex(excluded.arraySize - 1).stringValue = key;
          }

          private static string Describe(SerializerKind kind) => kind switch
          {
              SerializerKind.Own => "own serializer",
              SerializerKind.BuiltIn => "built-in",
              SerializerKind.Fields => "[Save] fields",
              _ => string.Empty
          };
      }
  }
  ```

  `Editor/SaveSystemSettingsEditor.cs`:

  ```csharp
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;

  namespace Saving.Editor
  {
      /// <summary>SaveSystemSettings in the Inspector: the catalogs, read only, and a rebuild button.</summary>
      [CustomEditor(typeof(SaveSystemSettings))]
      public sealed class SaveSystemSettingsEditor : UnityEditor.Editor
      {
          public override void OnInspectorGUI()
          {
              var settings = (SaveSystemSettings)target;
              EditorGUILayout.HelpBox("The editor keeps these catalogs up to date: after prefabs and scenes are imported, " +
                                      "moved or deleted, and before a build.", MessageType.Info);

              EditorGUILayout.LabelField($"Prefabs with Saveable: {settings.Prefabs.Count}", EditorStyles.boldLabel);
              using (new EditorGUI.DisabledScope(true))
              {
                  foreach (SaveSystemSettings.PrefabEntry entry in settings.Prefabs)
                      EditorGUILayout.ObjectField(entry.Id, entry.Prefab, typeof(GameObject), false);
              }

              EditorGUILayout.Space();
              EditorGUILayout.LabelField($"Scenes: {settings.Scenes.Count}", EditorStyles.boldLabel);
              foreach (SaveSystemSettings.SceneEntry entry in settings.Scenes)
                  EditorGUILayout.LabelField(entry.Path, entry.Guid);

              EditorGUILayout.Space();
              if (GUILayout.Button("Rebuild Catalogs"))
                  SaveCatalogBuilder.Rebuild(settings);
          }
      }
  }
  ```

- [x] **Шаг 5: запуск — зелёный.** EditMode: `failed 0`. Глазами (вместе с пользователем, по желанию): выбрать
  `Box` в `SaveTestScene` — ID, список `Transform (built-in)`, `SaveTestCounter ([Save] fields)`; снять флажок —
  ключ появляется в `_excluded`, Undo возвращает. Консоль без ошибок Odin от этих инспекторов.

- [x] **Шаг 6: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add Saveable and SaveSystemSettings inspectors"
  ```

---

### Задача 9: подключение к игре

**Files:**
- Modify: `Assets/Game/Tests/EditMode/Game.Tests.EditMode.asmdef` (+ `SaveSystem`, `SaveSystem.Objects`,
  `SaveSystem.Editor`)
- Create: `Assets/Game/Tests/EditMode/SaveSetupTests.cs`
- Create (командой): `Assets/Game/Configs/SaveSystemSettings.asset`; Modify (командой): `Assets/Game/Prefabs/Services.prefab`
- Modify: `Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs`, `Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs`,
  `Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs`

**Interfaces:**
- Consumes: `SaveSystemSettings`, `SaveCatalogBuilder`, `SaveValidator` (задачи 6–7), `SaveLoadSystem` (часть 1).
- Produces: ассет настроек игры в `[Services]`; `FakeSaveLoadService.IsLoading { get; set; }`; F9/F5 ничего не
  делают, пока идёт загрузка (иначе `Load` во время перезагрузки сцены бросил бы `InvalidOperationException`).

- [x] **Шаг 1: ссылки.** В `references` файла `Assets/Game/Tests/EditMode/Game.Tests.EditMode.asmdef` после
  `"SpriteAnimator",` добавить `"SaveSystem",`, `"SaveSystem.Objects",`, `"SaveSystem.Editor",`.

- [x] **Шаг 2: падающие тесты.**

  `Assets/Game/Tests/EditMode/SaveSetupTests.cs`:

  ```csharp
  using System.Linq;
  using NUnit.Framework;
  using Saving;
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;

  namespace Gameplay.Tests
  {
      /// <summary>The game's save setup: settings on [Services], fresh catalogs, valid ids in the build scenes.</summary>
      public class SaveSetupTests
      {
          private const string SETTINGS_PATH = "Assets/Game/Configs/SaveSystemSettings.asset";
          private const string SERVICES_PATH = "Assets/Game/Prefabs/Services.prefab";

          private static SaveSystemSettings Settings => AssetDatabase.LoadAssetAtPath<SaveSystemSettings>(SETTINGS_PATH);

          [Test]
          public void SaveSettings_SetOnServices()
          {
              var services = AssetDatabase.LoadAssetAtPath<GameObject>(SERVICES_PATH);
              var system = services.GetComponentInChildren<SaveLoadSystem>(true);

              Assert.IsNotNull(Settings, $"no settings at {SETTINGS_PATH}");
              Assert.AreSame(Settings, new SerializedObject(system).FindProperty("_settings").objectReferenceValue);
          }

          [Test]
          public void SaveSettings_CatalogsUpToDate()
          {
              Assert.IsNotNull(Settings, $"no settings at {SETTINGS_PATH}");
              CollectionAssert.IsEmpty(SaveValidator.ValidateCatalogs(Settings));
          }

          [Test]
          public void BuildScenes_SaveablesHaveUniqueIds()
          {
              string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();

              CollectionAssert.IsEmpty(SaveValidator.ValidateScenes(scenes));
          }
      }
  }
  ```

  В `Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs` строку `public bool IsLoading => false;` заменить на
  `public bool IsLoading { get; set; }`.

  В `Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs`:

  ```csharp
          [Test]
          public void SaveDebugHotkeys_WhileLoading_DoesNothing()
          {
              ServiceLocator.Register<ISaveLoadService>(_saves);
              _saves.IsLoading = true;

              _input.Press(_keyboard.f9Key);
              _hotkeys.Poll(_keyboard);

              Assert.AreEqual(0, _saves.LoadCalls);
              Assert.AreEqual(0, _saves.SaveCalls);
          }
  ```

- [x] **Шаг 3: запуск — красный.** All Tests: падают `SaveSettings_SetOnServices` и `SaveSettings_CatalogsUpToDate`
  (ассета нет), `SaveDebugHotkeys_WhileLoading_DoesNothing` (`LoadCalls` = 1). `BuildScenes_SaveablesHaveUniqueIds`
  зелёный сразу: в сценах игры `Saveable` пока нет — тест страхует будущие.

- [x] **Шаг 4: клавиши.** В `Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs` в `Poll` после проверки
  `if (keyboard == null || …) return;`:

  ```csharp
              // Load reloads the scene over several frames: keys wait until it is done
              if (saves.IsLoading)
                  return;
  ```

- [x] **Шаг 5: ассет настроек и `[Services]`.** `Refresh`, консоль без ошибок, затем `Unity_RunCommand`:

  ```csharp
  using Saving;
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;

  internal class CommandScript : IRunCommand
  {
      private const string SETTINGS_PATH = "Assets/Game/Configs/SaveSystemSettings.asset";
      private const string SERVICES_PATH = "Assets/Game/Prefabs/Services.prefab";

      public void Execute(ExecutionResult result)
      {
          var settings = AssetDatabase.LoadAssetAtPath<SaveSystemSettings>(SETTINGS_PATH);
          if (settings == null)
          {
              settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
          }

          SaveCatalogBuilder.BakePrefabIds();
          SaveCatalogBuilder.Rebuild(settings);

          GameObject root = PrefabUtility.LoadPrefabContents(SERVICES_PATH);
          try
          {
              var system = root.GetComponentInChildren<SaveLoadSystem>(true);
              var serialized = new SerializedObject(system);
              serialized.FindProperty("_settings").objectReferenceValue = settings;
              serialized.ApplyModifiedPropertiesWithoutUndo();
              PrefabUtility.SaveAsPrefabAsset(root, SERVICES_PATH);
          }
          finally
          {
              PrefabUtility.UnloadPrefabContents(root);
          }

          AssetDatabase.SaveAssets();
          result.Log("{0}: {1} prefab(s), {2} scene(s); set on {3}", SETTINGS_PATH, settings.Prefabs.Count,
              settings.Scenes.Count, SERVICES_PATH);
      }
  }
  ```

  Проверить `git diff Assets/Game/Prefabs/Services.prefab` — одна строка `_settings: {fileID: …, guid: …}`; сцены с
  `[Services]` без изменений.

- [x] **Шаг 6: запуск — зелёный.** All Tests: EditMode и PlayMode `failed 0`.

- [x] **Шаг 7: проверка в игре** (через MCP, без нажатия клавиш — Unity не выдёргивать на передний план): открыть
  `Game.unity`, Play; через `ServiceLocator.Get<ISaveLoadService>()` вызвать `Save()` — в `save.json` слота есть
  `$scenes` с путём `Game.unity` и секция `scene/<GUID Game.unity>` (объектов в ней пока нет — `Saveable` в игре
  появятся в части 3); `Load()` — исход `Loaded`, сцена перезагружена, консоль без ошибок; выход из Play Mode
  сохраняет слот. Затем повторно Play: загрузка при старте применяется на месте (в логе редактора — одна загрузка
  `Game.unity`).

- [x] **Шаг 8: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Configs/SaveSystemSettings.asset Assets/Game/Configs/SaveSystemSettings.asset.meta Assets/Game/Prefabs/Services.prefab Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs Assets/Game/Tests/EditMode/Game.Tests.EditMode.asmdef Assets/Game/Tests/EditMode/SaveSetupTests.cs Assets/Game/Tests/EditMode/SaveSetupTests.cs.meta Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs
  git commit -m "Plug the objects layer into the game services"
  ```

---

### Задача 10: документация и итоговый прогон

**Files:**
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/README.md`, `Assets/AI/CLAUDE.md`, `Assets/AI/Docs/SaveSystem.md`, этот план

- [x] **Шаг 1: README модуля.** В `Assets/Game/Scripts/Modules/SaveSystem/README.md`:
  - первый абзац: «Это ядро (часть 1)…» заменить на «Части 1–2 готовы: ядро и объекты сцены. Unity-типы (физика,
    анимация, частицы, звук) и ссылки — часть 3, см. спек `Assets/AI/Docs/SaveSystem.md`.»;
  - в «Состав» после строки `Runtime/Core/` добавить блок:

    ```
    ├── Runtime/Objects/ (сборка SaveSystem.Objects, namespace Saving.Objects)
    │   ├── Saveable                 — компонент на сохраняемом объекте: ID, ID префаба, флажки компонентов
    │   ├── SaveSystemSettings       — ассет настроек: подключает слой объектов, каталоги префабов и сцен
    │   ├── ComponentSerializer      — свой сериализатор компонента; [Save] — поля без кода
    │   └── SceneSaving, SaveRegistry — секции сцен и скрытый реестр (внутреннее)
    ```

  - новый раздел после «Быстрый старт»:

    ````markdown
    ## Объекты сцены

    1. *Tools → Save System → Create Settings*, ассет — в поле `Settings` у `SaveLoadSystem`.
    2. `Saveable` на объект. ID объект сцены получает сам; в инспекторе — какие компоненты сохраняются (по умолчанию
       всё, что умеет, в том числе добавленное позже).
    3. Поля своих скриптов — `[Save]`; настройки из Inspector не сохраняются:

    ```csharp
    using Saving.Objects;

    public class FallingPlatform : MonoBehaviour
    {
        [SerializeField] private float _fallDelay = 0.5f; // setting: not saved
        [Save] private float _timer;
        [Save("_start")] private Vector2 _startPosition;  // key of the former name
    }
    ```

    Особый случай — `ComponentSerializer<TComponent, TData>` (находится сам, вытесняет встроенный и `[Save]`).
    Сохраняются: `activeSelf` объекта и детей, `enabled`, `Transform` (локальная поза), поля `[Save]` (кроме ссылок
    на Unity-объекты — часть 3). Уничтоженные объекты сцены остаются уничтоженными, заспавненные из префабов с
    `Saveable` на корне создаются снова с прежними ID и родителем, `DontDestroyOnLoad` — секция `$persistent`.

    **Загрузка** перезагружает сохранённую сцену: объекты — на `sceneLoaded`, данные — в позднем `Start` каждого
    `Saveable` (после `Start` остальных компонентов: инициализация не перетирает загруженное), затем
    `ISaveCallbacks.OnAfterLoad` и событие `Loaded`. При старте игры, если сохранённая сцена только что загрузилась,
    данные применяются к ней на месте. Смена сцены без потери изменений — `Capture()` перед `SceneManager.LoadScene`.

    Меню *Tools → Save System*: Rebuild Catalogs, Validate Scenes (пустые и повторные ID, устаревшие каталоги).
    ````

  - в «Ошибки» добавить `SceneNotFound` (сцены нет в сборке: ничего не применено, слот только для чтения) и строку
    «Компонент новее своего сериализатора — `NewerVersion`; сбой одного компонента пишется в консоль, остальные
    применяются»;
  - в «Тесты»: «`SaveSystem.Tests`: … поля `[Save]`, каталог сериализаторов, состояние объекта, ID и каталоги в
    эдиторе, инспектор. `SaveSystem.Tests.Runtime`: компонент `SaveLoadSystem`, реестр и сцена (`Tests/Runtime/Scenes/
    SaveTestScene.unity`, префаб `SaveTestSpawn`)».

- [x] **Шаг 2: `Assets/AI/CLAUDE.md`.** Если в файле есть чужие незакоммиченные правки — показать пользователю и
  договориться. Правки:
  - абзац «**Сохранения — …**»: «Часть 1 (ядро…) готова; часть 2 (`Saveable`, объекты сцены) — следующая.» →
    «Части 1 (ядро: слоты, `.bak`, версии и миграции, `GameSerializer`, `SaveLoadSystem` на `[Services]`, F5/F9) и 2
    (объекты: `Saveable`, `[Save]`, секции сцен, загрузка перезагрузкой сцены) готовы; часть 3 (Unity-типы, ссылки,
    демо-сцена, `Saveable` на игроке) — следующая.»;
  - «Структура Assets/Game»: `Configs/` — добавить `SaveSystemSettings.asset (каталоги сохранений)`;
  - «Сборки»: в пункт `SaveSystem` добавить «`SaveSystem.Objects` (`Runtime/Objects`) — слой объектов сцены,
    ссылается на `SaveSystem`»; у `Game.Tests.EditMode` — ещё `SaveSystem`, `SaveSystem.Objects`, `SaveSystem.Editor`;
  - «Архитектура», абзац «**Сохранения.**» дописать: «Объект сцены сохраняется компонентом `Saveable` (ID — в
    эдиторе, флажки компонентов — в инспекторе), поля своих скриптов — `[Save]`, особые случаи —
    `ComponentSerializer<TComponent, TData>`. Слой подключает ассет `Configs/SaveSystemSettings` на `SaveLoadSystem`;
    каталоги префабов и сцен эдитор обновляет сам. Загрузка перезагружает сохранённую сцену; инициализация в `Start`
    не перетирает загруженное (данные приходят в позднем `Start` `Saveable`). Смена сцены с сохранением изменений —
    `Capture()` перед загрузкой. Тестовые сцены модуля грузятся `EditorSceneManager.LoadSceneInPlayMode` и в Build
    Settings не нужны.»;
  - «Стиль кода» — в список namespace модулей добавить `Saving.Objects`.

- [x] **Шаг 3: спек.** В `Assets/AI/Docs/SaveSystem.md` строку статуса заменить на «**Статус:** части 1 (ядро) и 2
  (объекты) готовы <дата>, часть 3 — впереди.»; в разделе 5.3 у `LoadOutcome` убрать «в части 2 добавится» —
  `SceneNotFound` уже есть. В этом плане отметить выполненные шаги и дописать раздел «Исполнение» с отличиями от
  плана, если они были.

- [x] **Шаг 4: итоговый прогон.** Сохранить сцены, `Refresh`, All Tests: EditMode и PlayMode `failed 0`. Консоль —
  без новых ошибок и предупреждений от `Saving` (известные шумы Odin и `UAC1001` — не наши). *Tools → Save System →
  Validate Scenes* — «no problems found».

- [x] **Шаг 5: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem/README.md Assets/AI/CLAUDE.md Assets/AI/Docs/SaveSystem.md Assets/AI/Docs/SaveSystem_Plan_Part2.md Assets/AI/Docs/SaveSystem_Plan_Part2.md.meta
  git commit -m "Document the save system objects layer"
  ```

  Пуш — только по просьбе.

---

## Исполнение (2026-10-23)

Выполнено в worktree `C:\!!!Work\Platformer-SaveSystem` со своим эдитором, задача за задачей по TDD, с локальным
коммитом после каждой зелёной задачи (пользователь разрешил идти до конца). Отличия от плана:

- **Тестовые компоненты** (`FieldsProbe`, `FieldsProbeBase`, `PlainProbe`, `SerializerProbe`, `ProbeSerializer`)
  лежат в `Tests/Runtime/Components`, а не в `Tests/Editor/Components`: Unity не прикрепляет MonoBehaviour из папки
  `Editor` («Can't add script behaviour … because it is an editor script»). Перенесены `AssetDatabase.MoveAsset`;
  `SaveSystem.Tests` ссылается на `SaveSystem.Tests.Runtime`.
- **`TempScene`**: свежий эдитор открыт с безымянной сценой, а рядом с ней Unity не создаёт аддитивную
  («Cannot create a new scene additively with an untitled scene unsaved»). С согласия пользователя помощник в этом
  случае заменяет нетронутую безымянную сцену пустой и после теста оставляет чистую безымянную; если в ней есть
  несохранённые правки — тест `Inconclusive`. Команда создания тестовых ассетов (задача 5) учитывает то же.
- Задача 2, красный прогон: `Fields_WrongType_NothingAssigned` сразу зелёный — `Assert.Catch` ловит и исключение
  заглушки; после реализации проходит по своей причине.
- Все четыре непроверенных допущения подтвердились: `Reset` на `AddComponent` в эдиторе, аддитивная `TempScene`
  рядом с сохранённой сценой, один `Time.frameCount` в `sceneLoaded` и `Start` (загрузка при старте игры применилась на
  месте: в `Game.unity` запись сцены отсканирована на кадре 2, `Applied = true`), `LoadSceneInPlayMode` в PlayMode-тестах.
- **Найдено итоговой проверкой:** Validate Scenes после PlayMode-прогона нашёл в каталоге сцен `Assets/InitTestScene….unity` —
  временную сцену Test Framework: постпроцессор видел её импорт, а удаление во время Play Mode пропускал, и
  `SaveSettings_CatalogsUpToDate` упал бы на следующем прогоне. Исправлено по TDD (тест
  `Rebuild_SkipsTestRunnerScenes`): каталог не берёт `InitTestScene*`, а постпроцессор, встретив Play Mode, доделывает
  работу после выхода из него.
- Проверка в игре — через MCP без нажатия клавиш: `Save()` в `Game.unity` пишет `$scenes` и секцию
  `scene/e7d1bc14…` (объектов пока нет), `Load()` — `Loaded`, консоль без ошибок плагина, выход из Play Mode
  сохраняет слот.

Итог: EditMode 378/0, PlayMode 374/0 (4 inconclusive — как до начала).

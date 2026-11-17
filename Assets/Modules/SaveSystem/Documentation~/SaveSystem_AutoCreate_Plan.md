# SaveSystem: система создаётся сама — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Saveable`, который не нашёл системы сохранений, создаёт её сам: пользователь вешает компонент на префаб и ни о
чём не думает; система в сцене и `[Services]` игры работают как раньше.

**Architecture:** Новое событие реестра `SaveRegistry.Registered` слушает `SaveSystemAutoCreate` (сборка
`SaveSystem.Objects`): при `SaveLoadSystem.Current == null` он берёт ассет `Resources/SaveSystemSettings` и создаёт
`SaveSystem (auto)`, которая сразу грузит слот новым `LoadInPlace()` — без смены сцены (`SceneSaving` при
`SaveLoadService.KeepsScene`). Эдитор создаёт новый ассет настроек в `Assets/Settings/Resources/` уже при добавлении
`Saveable`. Спек — [SaveSystem_AutoCreate.md](SaveSystem_AutoCreate.md).

**Tech Stack:** Unity 6000.5.6f1, C#, Newtonsoft JSON, Unity Test Framework (NUnit, `[UnityTest]`), MCP `Unity_*`
или Unity в batchmode.

## Global Constraints

- **Два этапа (правило проекта, сильнее TDD скилла):** задачи 1–4 — прототип: код без новых тестов, существующие тесты
  зелёные. После задачи 4 — **СТОП**: финализацию (задачи 5–7: тесты, документация, полный прогон) начинать только
  после явного «устраивает, финализируй» от пользователя.
- Наша система не меняется: система в сцене, копии-пересылки, `[Services]` игры, `ISaveLoadService` (и
  `FakeSaveLoadService`), формат сейвов — без изменений. Модуль не ссылается на игру.
- Ассет игры `Assets/Game/Configs/SaveSystemSettings.asset` руками не править и не переносить в `Resources`.
- Код, комментарии, сообщения — на английском; Allman, 4 пробела, `_camelCase` у приватных полей, `UPPER_CASE` у
  констант; namespace: ядро `Saving`, объекты `Saving.Objects`, эдитор `Saving.Editor`, тесты `Saving.Tests`
  (тесты игры — `Gameplay.Tests`).
- Тесты: поведение, а не внутренности; имена `Класс_Ситуация_Ожидание`; пути ассетов модуля — от папки модуля
  (`TestScenes.ROOT`, `TestScenes.MODULE`); хранилище — `InMemorySaveStorage`, никогда `persistentDataPath`.
- Пока пользователь в Play Mode — не писать `.cs`/`.asmdef` в основной папке и не делать `Refresh` (только `.md`).
- Во время работы — прогоны с фильтром; полный прогон — один раз, в задаче 7.
- Не коммитить и не пушить без явной просьбы. Коммит — `git add` по путям; сообщение на английском; в прототипе —
  префикс `Prototype:`; в конце сообщения строка `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
  Файлы с чужими незакоммиченными правками (`Assets/AI/CLAUDE.md`) — коммитить только свои куски (задача 7).

---

## Файлы

| Файл | Что делает | Задача |
|---|---|---|
| `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveLoadService.cs` | + `KeepsScene`, `LoadInPlace()` | 1 |
| `.../SaveSystem/Runtime/Core/SaveLoadSystem.cs` | + `LoadInPlace()` компонента (с пересылкой у копии) | 1 |
| `.../SaveSystem/Runtime/Objects/SceneSaving.cs` | при `KeepsScene`: `Check` не требует сохранённую сцену, `BeginLoad` её не грузит | 1 |
| `.../SaveSystem/Runtime/Objects/SaveRegistry.cs` | + событие `Registered` | 2 |
| `.../SaveSystem/Runtime/Objects/SaveSystemSettings.cs` | + `RESOURCES_NAME`, галочка `CreateSystemWhenMissing` | 2 |
| `.../SaveSystem/Runtime/Objects/SaveSystemAutoCreate.cs` (новый) | создать систему, когда её нет | 2 |
| `.../SaveSystem/Editor/SaveSystemSetup.cs` | новый путь ассета в `Resources`, ассет при добавлении `Saveable`, `IsRuntimeSettingsPath` | 3 |
| `.../SaveSystem/Editor/SaveableIdAssigner.cs` | `EditorAdded` — общий делегат (`+=`) | 3 |
| `.../SaveSystem/Editor/SaveSystemSettingsEditor.cs` | галочка и подсказка «ассет не в `Resources`» | 3 |
| `.../SaveSystem/Tests/Editor/LoadInPlaceTests.cs` (новый) | `LoadInPlace` сервиса | 5 |
| `.../SaveSystem/Tests/Editor/SaveSystemSetupTests.cs` | путь ассета, галочка, без дублей ассета | 5 |
| `Assets/Game/Tests/EditMode/SaveSetupTests.cs` | защита игры: нет `Resources/SaveSystemSettings` | 5 |
| `.../SaveSystem/Tests/Runtime/SaveSystemAutoCreateTests.cs` (новый) | поведение автосистемы | 6 |
| `.../SaveSystem/README.md`, `Assets/AI/CLAUDE.md`, `Assets/AI/Docs/SaveSystem_AutoCreate.md` | документация | 7 |

`...` = `Assets/Game/Scripts/Modules`. `.meta` новых файлов создаёт Unity при импорте — коммитить вместе с файлом.

## Как собирать и гонять тесты

**A. Основная папка, открытый эдитор (MCP).** Только если папку не занимает другая сессия и пользователь не в Play.

1. Проверка Play — `Unity_RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Debug.Log($"isPlaying={EditorApplication.isPlaying}");
    }
}
```

   `isPlaying=True` — ничего не компилировать, ждать.

2. Сборка — отдельным вызовом `Unity_RunCommand`, затем `Unity_GetConsoleLogs`: ошибок `error CS` нет.

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}
```

3. Прогон с фильтром — `Unity_RunCommand` (`MODE` и фильтр — из задачи; открытые сцены должны быть сохранены, иначе
   Test Runner встанет на вопросе — несохранённые сцены пользователя не сохранять без его согласия, а идти путём B):

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        System.IO.File.WriteAllText("Temp/GameTestResults.txt", "RUNNING filtered\n");
        SessionState.SetString("Gameplay.Tests.CurrentMode", "PlayMode");      // or "EditMode"
        SessionState.SetBool("Gameplay.Tests.PlayModeQueued", false);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.PlayMode,                                       // or TestMode.EditMode
            assemblyNames = new[] { "SaveSystem.Tests.Runtime" }                // or groupNames = new[] { "..." }
        }));
    }
}
```

4. Ждать итог: `until grep -q DONE "/c/!!!Work/Platformer/Temp/GameTestResults.txt"; do sleep 2; done` (Bash,
   `timeout` 600000), затем прочитать файл. Полный прогон — `EditorApplication.ExecuteMenuItem("Tools/Game/Run All
   Tests")`, итог в том же файле. Вызов `Unity_*` упал с «Unity not detected» — повторить через 10–40 с.

**B. Свой worktree + batchmode** (папку занимает другая сессия или пользователь в Play) — задача 0. Команда:

```bash
"/c/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath "C:/!!!Work/Platformer-AutoSave" \
  -runTests -testPlatform PlayMode -assemblyNames "SaveSystem.Tests.Runtime" \
  -testResults "<scratchpad>/PlayMode.xml" -logFile "<scratchpad>/PlayMode.log"
```

`<scratchpad>` — папка scratchpad сессии (её путь — в системном промпте).
`-testPlatform EditMode|PlayMode`, `-assemblyNames` — сборка, `-testFilter "A;B"` — группы, без них — всё. Итог —
атрибуты `<test-run>` в XML (exit 0 — всё прошло, 2 — падения), ошибки компиляции — `error CS` в логе. Результаты и
лог — в scratchpad, не в `Temp` (Unity чистит её на старте). EditMode и PlayMode — по очереди (один Unity на папку).

---

## Прототип

### Task 0: Рабочее место

**Files:** нет.

- [ ] **Step 1: Проверить, кто занят папкой.** Загрузить `mcp__ccd_session_mgmt__list_sessions` (через `ToolSearch`)
  и вызвать; посмотреть `git -C "C:/!!!Work/Platformer" status --short`. Проверить Play (путь A, шаг 1).

- [ ] **Step 2: Выбрать путь.** Другая активная сессия в `C:\!!!Work\Platformer` или пользователь в Play → путь B
  (шаги 3–5). Иначе → путь A, работать в основной папке (шаги 3–5 пропустить).

- [ ] **Step 3 (путь B): worktree и Library.**

```bash
git -C "C:/!!!Work/Platformer" worktree add -b feature/save-autocreate "C:/!!!Work/Platformer-AutoSave" main
```

```powershell
robocopy "C:\!!!Work\Platformer\Library" "C:\!!!Work\Platformer-AutoSave\Library" /E /MT:16 /NFL /NDL /NJH /NJS
```

  robocopy: коды 0–7 — успех. Скопировать незакоммиченные документы спека и плана:

```bash
cp "/c/!!!Work/Platformer/Assets/AI/Docs/SaveSystem_AutoCreate.md"* "/c/!!!Work/Platformer-AutoSave/Assets/AI/Docs/"
cp "/c/!!!Work/Platformer/Assets/AI/Docs/SaveSystem_AutoCreate_Plan.md"* "/c/!!!Work/Platformer-AutoSave/Assets/AI/Docs/"
```

- [ ] **Step 4 (путь B): проверочный прогон до правок.** Команда пути B с `-testPlatform EditMode -assemblyNames
  "SaveSystem.Tests"`. Ожидание: exit 0. Если сотни падений «The referenced script on this Behaviour is missing» — кэш
  Library: временный `-executeMethod`-скрипт, который делает `AssetDatabase.ImportAsset(path,
  ImportAssetOptions.ForceUpdate)` для каждого `MonoScript` из `Assets/Game` с `GetClass() == null`, затем удалить
  скрипт и `.meta`.

- [ ] **Step 5: Записать в отчёт**, каким путём идёт работа. Дальше «папка проекта» — выбранная (`Platformer` или
  `Platformer-AutoSave`).

---

### Task 1: Загрузка без смены сцены (`LoadInPlace`)

**Files:**
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveLoadService.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveLoadSystem.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SceneSaving.cs`

**Interfaces:**
- Produces: `public bool SaveLoadService.KeepsScene { get; private set; }` — `true` только внутри `LoadInPlace`;
  `public LoadOutcome SaveLoadService.LoadInPlace()`; `public LoadOutcome SaveLoadSystem.LoadInPlace()`.

- [ ] **Step 1: `SaveLoadService` — свойство.** После строки `public Func<string> SceneName { get; set; }` вставить:

```csharp

        /// <summary>True only during <see cref="LoadInPlace"/>: the objects layer keeps the scenes in play.</summary>
        public bool KeepsScene { get; private set; }
```

- [ ] **Step 2: `SaveLoadService` — метод.** Сразу после метода `public LoadOutcome Load()` (после его закрывающей
  скобки) вставить:

```csharp

        /// <summary>
        /// A load that keeps the scenes in play: the objects layer neither loads nor requires the saved scene — the active
        /// one gets its data in place if nothing in it has started, other scenes when they load. A save system made by
        /// the first Saveable starts this way; buttons, menus and <see cref="Load"/> go to the saved scene.
        /// </summary>
        public LoadOutcome LoadInPlace()
        {
            KeepsScene = true;
            try
            {
                return Load();
            }
            finally
            {
                KeepsScene = false;
            }
        }
```

- [ ] **Step 3: `SaveLoadSystem`.** После строки `public LoadOutcome Load() => Service.Load();` вставить:

```csharp

        /// <summary>Loads the current slot keeping the scenes in play: see <see cref="SaveLoadService.LoadInPlace"/>.</summary>
        public LoadOutcome LoadInPlace() => Service.LoadInPlace();
```

  `Service` у копии — сервис живой системы, так что копия пересылает вызов сама.

- [ ] **Step 4: `SceneSaving.Check`.** Найти:

```csharp
                if (FindNewerComponent(scene, out string newer))
                {
                    problem = $"'{pair.Key}': {newer}";
                    return LoadOutcome.NewerVersion;
                }
            }

            if (!sections.TryGetValue(SCENES_KEY, out SaveSection scenesSection)
```

  заменить на:

```csharp
                if (FindNewerComponent(scene, out string newer))
                {
                    problem = $"'{pair.Key}': {newer}";
                    return LoadOutcome.NewerVersion;
                }
            }

            // In place the saved scene is not loaded, so it need not exist any more
            if (_service.KeepsScene)
                return LoadOutcome.Loaded;

            if (!sections.TryGetValue(SCENES_KEY, out SaveSection scenesSection)
```

- [ ] **Step 5: `SceneSaving.BeginLoad`.** Найти:

```csharp
        public void BeginLoad(Action finished)
        {
            _finishLoad = finished;
            ApplyPersistent();

            if (!_service.TryGetSection(SCENES_KEY, out SaveSection section) || !TryRead(section, out ScenesData scenes)
```

  заменить на (имена `current`/`fresh` — не `active`/`record`: те объявлены ниже в этом же методе, иначе CS0136):

```csharp
        public void BeginLoad(Action finished)
        {
            _finishLoad = finished;
            ApplyPersistent();

            if (_service.KeepsScene)
            {
                // The scenes in play stay: the active one gets its data now if nothing in it has started; a scene still
                // loading and every later one get theirs on their scan
                Scene current = SceneManager.GetActiveScene();
                if (SaveRegistry.TryGetRecord(current, out SaveRegistry.SceneRecord fresh) && fresh.IsFresh)
                    ApplyScene(current, fresh);

                TryFinish();
                return;
            }

            if (!_service.TryGetSection(SCENES_KEY, out SaveSection section) || !TryRead(section, out ScenesData scenes)
```

- [ ] **Step 6: `SceneSaving` — описание класса.** Найти:

```csharp
    /// each Saveable's late Start (disabled and already started ones at once), then OnAfterLoad. Load reloads the
    /// saved scene, or applies to it in place when it has just loaded and nothing in it has started.
    /// </summary>
```

  заменить на:

```csharp
    /// each Saveable's late Start (disabled and already started ones at once), then OnAfterLoad. Load reloads the
    /// saved scene, or applies to it in place when it has just loaded and nothing in it has started. LoadInPlace keeps
    /// the scenes in play: the saved one is neither loaded nor required, the active one gets its data in place if
    /// nothing in it has started, the others on their scan.
    /// </summary>
```

- [ ] **Step 7: Собрать.** Путь A: шаги 1–2. Путь B: прогон шага 8 сам компилирует; `error CS` в логе — нет.

- [ ] **Step 8: Существующие тесты модуля.** EditMode, `assemblyNames = { "SaveSystem.Tests" }`; затем PlayMode,
  `assemblyNames = { "SaveSystem.Tests.Runtime" }`. Ожидание: всё зелёное — `Load()` не менялся, флаг включается только
  новым методом.

---

### Task 2: Автосоздание системы

**Files:**
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveRegistry.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveSystemSettings.cs`
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveSystemAutoCreate.cs`

**Interfaces:**
- Consumes: `SaveLoadSystem.LoadInPlace()` (задача 1).
- Produces: `public static event Action<Saveable> SaveRegistry.Registered` (класс `SaveRegistry` — internal, как его
  события `SceneScanned` и `Ready`);
  `public const string SaveSystemSettings.RESOURCES_NAME = "SaveSystemSettings"`;
  `public bool SaveSystemSettings.CreateSystemWhenMissing { get; set; }` (по умолчанию `true`);
  `internal static class SaveSystemAutoCreate` с `public const string SYSTEM_NAME = "SaveSystem (auto)"`,
  `internal static Func<SaveSystemSettings> FindSettings`, `internal static Action<SaveLoadSystem> BeforeStart`,
  `internal static void Reset()`.

- [ ] **Step 1: `SaveRegistry` — событие.** После строки `public static event Action<Saveable> Ready;` вставить:

```csharp

        /// <summary>A new Saveable joined: its Awake, or the scan of its scene (disabled ones too); not on Rekey.</summary>
        public static event Action<Saveable> Registered;
```

- [ ] **Step 2: `SaveRegistry.Reset`.** После строки `Ready = null;` вставить строку `Registered = null;`.

- [ ] **Step 3: `SaveRegistry.Register`.** Заменить метод `Register` целиком на (поведение то же, событие — одно, в
  конце):

```csharp
        public static void Register(Saveable saveable)
        {
            if (saveable.IsRegistered)
                return;

            saveable.IsRegistered = true;
            string id = saveable.Id;
            if (string.IsNullOrEmpty(id))
            {
                Add(saveable, NewId(), spawned: true);
            }
            else if (ById.TryGetValue(id, out Saveable other) && other != null && !ReferenceEquals(other, saveable))
            {
                // A copy made in play is expected; the same id twice in a scene being loaded is not
                if (!Scenes.ContainsKey(saveable.gameObject.scene))
                    Debug.LogError($"'{saveable.name}' has the id {id} of '{other.name}'; it gets a new id and counts as " +
                                   "spawned. Run Tools → Save System → Validate Scenes.", saveable);

                Add(saveable, NewId(), spawned: true);
            }
            else
            {
                Add(saveable, id, spawned: false);
            }

            Registered?.Invoke(saveable);
        }
```

- [ ] **Step 4: `SaveSystemSettings` — имя и галочка.** Заменить:

```csharp
    [CreateAssetMenu(fileName = "SaveSystemSettings", menuName = "Save System/Settings")]
    public sealed class SaveSystemSettings : SaveExtensionAsset
    {
```

  на:

```csharp
    [CreateAssetMenu(fileName = RESOURCES_NAME, menuName = "Save System/Settings")]
    public sealed class SaveSystemSettings : SaveExtensionAsset
    {
        /// <summary>The name a scene without a SaveSystem finds the settings by: an asset right in a Resources folder.</summary>
        public const string RESOURCES_NAME = "SaveSystemSettings";

```

  Перед строкой `[SerializeField] private List<PrefabEntry> _prefabs = new();` вставить:

```csharp
        [Tooltip("A Saveable that finds no SaveSystem in play creates one with default settings. Only an asset named " +
                 RESOURCES_NAME + " right in a Resources folder is found")]
        [SerializeField] private bool _createSystemWhenMissing = true;

```

  После строки `public IReadOnlyList<SceneEntry> Scenes => _scenes;` вставить:

```csharp

        /// <summary>A scene without a SaveSystem gets one from this asset (when it lies in a Resources folder).</summary>
        public bool CreateSystemWhenMissing
        {
            get => _createSystemWhenMissing;
            set => _createSystemWhenMissing = value;
        }
```

  В `<summary>` класса найти:

```csharp
    /// date on its own (on import, move and delete, before a build, Tools → Save System → Rebuild Catalogs).
    /// </summary>
```

  заменить на:

```csharp
    /// date on its own (on import, move and delete, before a build, Tools → Save System → Rebuild Catalogs). A scene
    /// without a SaveSystem gets one made from it when the first Saveable wakes, if the asset lies right in a Resources
    /// folder (CreateSystemWhenMissing).
    /// </summary>
```

- [ ] **Step 5: Создать `SaveSystemAutoCreate.cs`:**

```csharp
using System;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// A save system for scenes that have none: the first Saveable that joins the registry while no SaveLoadSystem is in
    /// play makes "SaveSystem (auto)" — the module prefab's defaults, DontDestroyOnLoad, the project's SaveSystemSettings
    /// found in a Resources folder — and loads its slot in place, without switching scenes. A SaveSystem in the scene or
    /// one made by code earlier wins: it wakes before the Saveables. No settings asset there, or Create System When
    /// Missing off — nothing is made, and that answer holds until Play ends.
    /// </summary>
    internal static class SaveSystemAutoCreate
    {
        public const string SYSTEM_NAME = "SaveSystem (auto)";

        /// <summary>Where the settings come from: Resources; tests put their own.</summary>
        internal static Func<SaveSystemSettings> FindSettings = FromResources;

        /// <summary>Runs on the new system before it starts: tests give it a storage in memory.</summary>
        internal static Action<SaveLoadSystem> BeforeStart;

        private static bool _declined;

        /// <summary>The start of Play: settings from Resources, no setup hook, nothing declined yet. Tests call it too.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Reset()
        {
            FindSettings = FromResources;
            BeforeStart = null;
            _declined = false;
            // After SaveRegistry clears its events in SubsystemRegistration
            SaveRegistry.Registered -= OnRegistered;
            SaveRegistry.Registered += OnRegistered;
        }

        private static void OnRegistered(Saveable saveable)
        {
            if (_declined || SaveLoadSystem.Current != null || !Application.isPlaying)
                return;

            SaveSystemSettings settings = FindSettings?.Invoke();
            if (settings == null || !settings.CreateSystemWhenMissing)
            {
                // Remembered: every spawn of a Saveable would look for the asset again
                _declined = true;
                return;
            }

            // Its Awake makes it Current and keeps it across scenes, so the Saveables that follow find it
            var system = new GameObject(SYSTEM_NAME).AddComponent<SaveLoadSystem>();
            system.UseSettings(settings);
            BeforeStart?.Invoke(system);
            system.LoadInPlace();
        }

        private static SaveSystemSettings FromResources() =>
            Resources.Load<SaveSystemSettings>(SaveSystemSettings.RESOURCES_NAME);
    }
}
```

- [ ] **Step 6: Собрать** (путь A: шаги 1–2, `.meta` нового файла создаётся при `Refresh`; путь B: при прогоне).

- [ ] **Step 7: Тесты модуля и игры.** PlayMode `assemblyNames = { "SaveSystem.Tests.Runtime" }`; PlayMode игры
  `groupNames = { "PlayerSaveTests", "SavedGameTests", "SaveDebugHotkeysTests", "SaveDemoBoardTests",
  "SandboxPauseTests", "AppFlowTests", "RespawnTests" }`; EditMode `groupNames = { "SaveSetupTests" }`. Ожидание: всё
  зелёное. В нашем проекте ассета в `Resources` нет — автосистема не появляется ни в одном тесте.

---

### Task 3: Эдитор — ассет в `Resources`, ассет при добавлении `Saveable`, инспектор

**Files:**
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveSystemSetup.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveableIdAssigner.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveSystemSettingsEditor.cs`

**Interfaces:**
- Consumes: `SaveSystemSettings.RESOURCES_NAME`, `SaveSystemSettings.CreateSystemWhenMissing` (задача 2).
- Produces: `public const string SaveSystemSetup.NEW_SETTINGS_PATH = "Assets/Settings/Resources/SaveSystemSettings.asset"`;
  `public static bool SaveSystemSetup.IsRuntimeSettingsPath(string assetPath)`;
  `public static void SaveSystemSetup.EnsureSettingsFor(Saveable saveable)`.

- [ ] **Step 1: `SaveSystemSetup` — using.** Первой строкой файла добавить `using System;` (для `StringComparison`).

- [ ] **Step 2: `SaveSystemSetup` — описание и путь.** Заменить:

```csharp
    /// <summary>
    /// The "drop it in and it works" part of the editor: a SaveLoadSystem that saves scene objects gets the project's
    /// SaveSystemSettings on its own — the one that exists, or a new one in Assets/Settings. Prefab assets and prefab
    /// mode are left alone: their instances in scenes get it.
    /// </summary>
    [InitializeOnLoad]
    public static class SaveSystemSetup
    {
        public const string NEW_SETTINGS_PATH = "Assets/Settings/SaveSystemSettings.asset";
```

  на:

```csharp
    /// <summary>
    /// The "drop it in and it works" part of the editor. The project gets its SaveSystemSettings on its own — the one that
    /// exists, or a new one in Assets/Settings/Resources, where a scene without a SaveSystem finds it in play — as soon as
    /// a Saveable or a SaveLoadSystem is added; a SaveLoadSystem that saves scene objects is given it too. Prefab assets
    /// and prefab mode keep no reference: their instances in scenes get it.
    /// </summary>
    [InitializeOnLoad]
    public static class SaveSystemSetup
    {
        public const string NEW_SETTINGS_PATH = "Assets/Settings/Resources/" + SaveSystemSettings.RESOURCES_NAME + ".asset";
```

- [ ] **Step 3: `SaveSystemSetup` — подписка.** Найти конец статического конструктора:

```csharp
                // OnValidate must not change serialized data: do it right after
                EditorApplication.delayCall += () => AssignSettings(system);
            };
        }
```

  заменить на:

```csharp
                // OnValidate must not change serialized data: do it right after
                EditorApplication.delayCall += () => AssignSettings(system);
            };
            // Shared with the id assigner: both add to it
            Saveable.EditorAdded += EnsureSettingsFor;
        }
```

- [ ] **Step 4: `SaveSystemSetup` — методы.** Перед `/// <summary>The project's SaveSystemSettings; created with fresh
  catalogs when there is none.</summary>` вставить:

```csharp
        /// <summary>A Saveable just added: the project gets its settings now, so Play works without a SaveSystem in the scene.</summary>
        public static void EnsureSettingsFor(Saveable saveable)
        {
            if (saveable != null && !EditorApplication.isPlayingOrWillChangePlaymode)
                FindOrCreateSettings();
        }

        /// <summary>Whether a scene without a SaveSystem finds settings at this path in play: right in a Resources folder, by name.</summary>
        public static bool IsRuntimeSettingsPath(string assetPath) =>
            !string.IsNullOrEmpty(assetPath)
            && assetPath.EndsWith("/Resources/" + SaveSystemSettings.RESOURCES_NAME + ".asset", StringComparison.Ordinal);

```

- [ ] **Step 5: `SaveableIdAssigner`.** Заменить:

```csharp
            // A freshly added component may be changed at once: no wait for an editor update, which a background
            // editor hardly runs
            Saveable.EditorAdded = saveable => Assign(saveable);
```

  на (оба `[InitializeOnLoad]`-класса добавляют в один делегат, порядок их статических конструкторов не задан):

```csharp
            // A freshly added component may be changed at once: no wait for an editor update, which a background
            // editor hardly runs. Shared with SaveSystemSetup: both add to it
            Saveable.EditorAdded += saveable => Assign(saveable);
```

- [ ] **Step 6: `SaveSystemSettingsEditor`.** Заменить:

```csharp
            serializedObject.Update();
            SerializedProperty folders = serializedObject.FindProperty("_assetFolders");
```

  на:

```csharp
            serializedObject.Update();
            SerializedProperty createSystem = serializedObject.FindProperty("_createSystemWhenMissing");
            EditorGUILayout.PropertyField(createSystem);
            if (createSystem.boolValue && !SaveSystemSetup.IsRuntimeSettingsPath(AssetDatabase.GetAssetPath(settings)))
                EditorGUILayout.HelpBox("A scene without a SaveSystem gets none from this asset: in play it is found only as " +
                                        $"{SaveSystemSettings.RESOURCES_NAME} right in a Resources folder.", MessageType.Info);

            EditorGUILayout.Space();
            SerializedProperty folders = serializedObject.FindProperty("_assetFolders");
```

  Описание класса — найти:

```csharp
    /// <summary>SaveSystemSettings in the Inspector: the asset folders, the catalogs (read only) and a rebuild button.</summary>
```

  заменить на:

```csharp
    /// <summary>
    /// SaveSystemSettings in the Inspector: the auto-create flag, the asset folders, the catalogs (read only) and a
    /// rebuild button.
    /// </summary>
```

- [ ] **Step 7: Собрать** и проверить консоль (путь A: шаги 1–2; путь B: при прогоне).

- [ ] **Step 8: Тесты эдитора модуля.** EditMode `assemblyNames = { "SaveSystem.Tests" }`. Ожидание: всё зелёное
  (`SaveSystemSetupTests`, `SaveableIdTests` — ID по-прежнему раздаются при добавлении `Saveable`).

---

### Task 4: Проверка прототипа и стоп

**Files:**
- Create (временно): `Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/AutoCreateProbe.cs`

- [ ] **Step 1: Временная проба.** Создать файл:

```csharp
using System.Collections;
using NUnit.Framework;
using Saving.Objects;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    // Temporary prototype probe: delete it with its .meta after the check
    public class AutoCreateProbe
    {
        [UnityTest]
        public IEnumerator Probe_SaveThenRestart_StateBack()
        {
            var storage = new InMemorySaveStorage();
            SaveSystemSettings settings = TestScenes.CreateSettings();
            SaveSystemAutoCreate.Reset();
            SaveSystemAutoCreate.FindSettings = () => settings;
            SaveSystemAutoCreate.BeforeStart = system =>
            {
                system.UseStorage(storage);
                system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            };

            try
            {
                yield return TestScenes.Load();
                Assert.IsNotNull(SaveLoadSystem.Current, "the scene's Saveables made no system");
                Assert.AreEqual(SaveSystemAutoCreate.SYSTEM_NAME, SaveLoadSystem.Current.name);
                TestScenes.Find("Box").GetComponent<SaveTestCounter>().Value = 42;
                SaveLoadSystem.Current.Save();
                Object.DestroyImmediate(SaveLoadSystem.Current.gameObject);

                yield return TestScenes.Load();

                Assert.AreEqual(42, TestScenes.Find("Box").GetComponent<SaveTestCounter>().Value);
            }
            finally
            {
                SaveSystemAutoCreate.Reset();
                if (SaveLoadSystem.Current != null)
                    Object.DestroyImmediate(SaveLoadSystem.Current.gameObject);

                Object.DestroyImmediate(settings);
            }
        }
    }
}
```

- [ ] **Step 2: Прогнать пробу.** PlayMode `groupNames = { "AutoCreateProbe" }`. Ожидание: PASS. Падение — разобрать
  (superpowers:systematic-debugging), не гадать.

- [ ] **Step 3: Удалить пробу** с диска вместе с `.meta` (`rm` в Bash; `Unity_RunCommand` удалять не умеет), затем
  `Refresh` (путь A, шаг 2).

- [ ] **Step 4: Ручная проверка — предложить пользователю.** Чистый проект — ровно опыт новичка. По его согласию:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -quit -createProject "C:\!!!Work\SaveSystemTry" -logFile "<scratchpad>\create.log"
```

  В `C:\!!!Work\SaveSystemTry\Packages\manifest.json` в `dependencies` добавить
  `"com.unity.nuget.newtonsoft-json": "3.2.2",`. Скопировать модуль с `.meta`:

```powershell
robocopy "<папка проекта>\Assets\Game\Scripts\Modules\SaveSystem" "C:\!!!Work\SaveSystemTry\Assets\SaveSystem" /E /NFL /NDL /NJH /NJS
```

  Пользователю: открыть проект в Unity Hub (*Add project from disk*), новая сцена → куб → *Add Component*
  `DemoSpinner` и `Saveable` → сохранить сцену → Play пару секунд → Stop → Play. Ожидание: куб продолжает с того угла, где
  остановился; в иерархии `DontDestroyOnLoad/SaveSystem (auto)`; появился `Assets/Settings/Resources/SaveSystemSettings.asset`.

- [ ] **Step 5: Отчёт и СТОП.** Сообщить пользователю: что сделано, результаты прогонов (числа), как пощупать. Коммит
  прототипа — только по его просьбе:

```bash
git add Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveLoadService.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveLoadSystem.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SceneSaving.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveRegistry.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveSystemSettings.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveSystemAutoCreate.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveSystemAutoCreate.cs.meta \
  Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveSystemSetup.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveableIdAssigner.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveSystemSettingsEditor.cs \
  Assets/AI/Docs/SaveSystem_AutoCreate.md Assets/AI/Docs/SaveSystem_AutoCreate.md.meta \
  Assets/AI/Docs/SaveSystem_AutoCreate_Plan.md Assets/AI/Docs/SaveSystem_AutoCreate_Plan.md.meta
git commit -m "$(cat <<'EOF'
Prototype: create the save system when a Saveable finds none

A Saveable that wakes with no SaveLoadSystem in play makes "SaveSystem (auto)" from
Resources/SaveSystemSettings and loads its slot in place (LoadInPlace, no scene switch).
A system in the scene or [Services] still wins; the game's settings stay outside Resources.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

  Путь B: перед коммитом `git status` в worktree — вернуть файлы, которые batchmode трогает сам
  (`git checkout -- Assets/Resources/DOTweenSettings.asset ProjectSettings/EditorBuildSettings.asset
  ProjectSettings/ProjectAuditorSettings.asset Assets/Game/Configs/SaveSystemSettings.asset`, если они в списке).

  **Дальше — только после явного «устраивает, финализируй» от пользователя.**

---

## Финализация (после согласия пользователя)

Пользователь подтвердил прототип 2026-10-25 («Проверил в SaveSystemTry — устраивает, финализируй»). По ревью прототипа
план дополнен: задача 8 (чистка кода по ревью) идёт первой, изоляция тестов модуля — в задаче 6, задача 9 — проверка
переносимости в `C:\!!!Work\SaveSystemTry`. Порядок финализации: **8 → 5 → 6 → 7 → 9**.

### Task 8: Чистка кода по ревью прототипа (первая в финализации)

**Files:**
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveLoadService.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveLoadSystem.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/ISaveExtension.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SceneSaving.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/SaveSystemAutoCreate.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Objects/Saveable.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveSystemSetup.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Editor/SaveCatalogBuilder.cs`

**Interfaces:**
- Produces: `SaveSystemAutoCreate.FindSettings` — теперь `internal static Func<SaveSystemSettings> FindSettings { get; set; }`
  (свойство; присваивание забывает запомненный отказ, `null` — обратно к `Resources`); исключение внутри автосоздания
  пишется `Debug.LogException` и запоминается как отказ; `SaveLoadService.KeepsScene` сбрасывается до события `Loaded`.
  Остальные имена и сигнатуры задач 1–3 не меняются.

- [ ] **Step 1: `SaveLoadService.FinishLoad` — флаг до события.** Найти:

```csharp
            IsLoading = false;
            Loaded?.Invoke(outcome);
```

  заменить на:

```csharp
            IsLoading = false;
            KeepsScene = false; // a Loaded subscriber that loads again gets an ordinary load
            Loaded?.Invoke(outcome);
```

- [ ] **Step 2: `SaveLoadService` — описания.** Найти:

```csharp
        /// <summary>True only during <see cref="LoadInPlace"/>: the objects layer keeps the scenes in play.</summary>
```

  заменить на:

```csharp
        /// <summary>
        /// True while a <see cref="LoadInPlace"/> load runs (until it finishes or returns): the objects layer keeps the
        /// scenes in play.
        /// </summary>
```

  Найти:

```csharp
        /// one gets its data in place if nothing in it has started, other scenes when they load. A save system made by
        /// the first Saveable starts this way; buttons, menus and <see cref="Load"/> go to the saved scene.
```

  заменить на:

```csharp
        /// one gets its data in place if it was scanned this frame and nothing in it has started (otherwise it keeps its
        /// state, and the load still reports Loaded), other scenes when they load. A save system made by the first
        /// Saveable starts this way; buttons, menus and <see cref="Load"/> go to the saved scene.
```

  Найти (описание класса):

```csharp
    /// writing another slot's files into the current one makes it read-only until the next Load.
    /// </summary>
```

  заменить на:

```csharp
    /// writing another slot's files into the current one makes it read-only until the next Load. LoadInPlace is a load
    /// that keeps the scenes in play (KeepsScene tells the layer).
    /// </summary>
```

- [ ] **Step 3: `SaveLoadSystem` — пустая строка.** Найти:

```csharp
        public LoadOutcome LoadInPlace() => Service.LoadInPlace();
        public void SelectSlot(int slot, bool keepData = false) => Service.SelectSlot(slot, keepData);
```

  заменить на:

```csharp
        public LoadOutcome LoadInPlace() => Service.LoadInPlace();

        public void SelectSlot(int slot, bool keepData = false) => Service.SelectSlot(slot, keepData);
```

- [ ] **Step 4: `ISaveExtension` — описание.** Найти:

```csharp
    /// scene sections. It writes them on every capture, checks a file before a load applies anything and finishes a
    /// load later, once the saved scene is restored.
```

  заменить на:

```csharp
    /// scene sections. It writes them on every capture, checks a file before a load applies anything and finishes a
    /// load later, once the saved scene is restored. During SaveLoadService.LoadInPlace (SaveLoadService.KeepsScene) it
    /// keeps the scenes in play instead of loading the saved one.
```

- [ ] **Step 5: `SceneSaving` — условие «в этом кадре».** Найти:

```csharp
    /// the scenes in play: the saved one is neither loaded nor required, the active one gets its data in place if
    /// nothing in it has started, the others on their scan.
```

  заменить на:

```csharp
    /// the scenes in play: the saved one is neither loaded nor required, the active one gets its data in place if it
    /// was scanned this frame and nothing in it has started, the others on their scan.
```

  Найти:

```csharp
                // The scenes in play stay: the active one gets its data now if nothing in it has started; a scene still
                // loading and every later one get theirs on their scan
```

  заменить на:

```csharp
                // The scenes in play stay: the active one gets its data now if it was scanned this frame and nothing in
                // it has started; a scene still loading and every later one get theirs on their scan
```

- [ ] **Step 6: `SaveSystemAutoCreate.cs` — переписать файл целиком:**

```csharp
using System;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// A save system for scenes that have none: the first Saveable that joins the registry while no SaveLoadSystem is in
    /// play makes "SaveSystem (auto)" — DontDestroyOnLoad, the project's SaveSystemSettings found in a Resources folder,
    /// SaveLoadSystem's own defaults (slot 0, files in persistentDataPath/Saves, autosave every 30 s of game time, saves
    /// on pause, focus loss and quit, no Load On Start) — and loads its slot in place, without switching scenes. A
    /// SaveSystem in the scene or one made by code earlier wins: it wakes before the Saveables. No settings asset there,
    /// Create System When Missing off or a setup that throws — nothing is made, and that answer holds until Play ends or
    /// the lookup is replaced.
    /// </summary>
    internal static class SaveSystemAutoCreate
    {
        public const string SYSTEM_NAME = "SaveSystem (auto)";

        private static Func<SaveSystemSettings> _findSettings = FromResources;
        private static bool _declined;

        /// <summary>
        /// Where the settings come from: Resources; tests put their own. A new lookup forgets an earlier "no".
        /// </summary>
        internal static Func<SaveSystemSettings> FindSettings
        {
            get => _findSettings;
            set
            {
                _findSettings = value ?? FromResources;
                _declined = false;
            }
        }

        /// <summary>Runs on the new system before it starts: tests give it a storage in memory.</summary>
        internal static Action<SaveLoadSystem> BeforeStart;

        /// <summary>The start of Play: settings from Resources, no setup hook, nothing declined yet. Tests call it too.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Reset()
        {
            FindSettings = FromResources;
            BeforeStart = null;
            // After SaveRegistry clears its events in SubsystemRegistration
            SaveRegistry.Registered -= OnRegistered;
            SaveRegistry.Registered += OnRegistered;
        }

        private static void OnRegistered(Saveable saveable)
        {
            if (_declined || SaveLoadSystem.Current != null || !Application.isPlaying)
                return;

            try
            {
                SaveSystemSettings settings = _findSettings();
                if (settings == null || !settings.CreateSystemWhenMissing)
                {
                    // Remembered: every spawn of a Saveable would look for the asset again
                    _declined = true;
                    return;
                }

                // Its Awake makes it Current and keeps it across scenes, so the Saveables that follow find it
                var system = new GameObject(SYSTEM_NAME).AddComponent<SaveLoadSystem>();
                system.UseSettings(settings);
                BeforeStart?.Invoke(system);
                system.LoadInPlace();
            }
            catch (Exception exception)
            {
                // Raised from the registry, inside the scan of a loading scene: a broken setup must not stop the scan
                _declined = true;
                Debug.LogException(exception);
            }
        }

        private static SaveSystemSettings FromResources() =>
            Resources.Load<SaveSystemSettings>(SaveSystemSettings.RESOURCES_NAME);
    }
}
```

- [ ] **Step 7: `Saveable` — общий делегат.** Найти:

```csharp
        /// <summary>Set by the editor: the same right when the component is added, where changing data is allowed.</summary>
        internal static Action<Saveable> EditorAdded;
```

  заменить на:

```csharp
        /// <summary>
        /// The editor adds to it (ids, the project's settings): runs right when the component is added, where changing
        /// data is allowed. Add with +=: more than one part of the editor listens.
        /// </summary>
        internal static Action<Saveable> EditorAdded;
```

- [ ] **Step 8: `SaveSystemSetup` — формулировки.** Найти описание класса:

```csharp
    /// <summary>
    /// The "drop it in and it works" part of the editor. The project gets its SaveSystemSettings on its own — the one that
    /// exists, or a new one in Assets/Settings/Resources, where a scene without a SaveSystem finds it in play — as soon as
    /// a Saveable or a SaveLoadSystem is added; a SaveLoadSystem that saves scene objects is given it too. Prefab assets
    /// and prefab mode keep no reference: their instances in scenes get it.
    /// </summary>
```

  заменить на:

```csharp
    /// <summary>
    /// The "drop it in and it works" part of the editor. Adding a Saveable makes sure the project has its
    /// SaveSystemSettings — the one that exists, or a new one in Assets/Settings/Resources, where a scene without a
    /// SaveSystem finds it in play (an asset elsewhere is used as it is; the settings Inspector says when play will not
    /// find it). A SaveLoadSystem that saves scene objects gets the asset in its field when added to a scene; prefab
    /// assets and prefab mode keep no reference: their instances in scenes get it.
    /// </summary>
```

  Найти:

```csharp
        /// <summary>A Saveable just added: the project gets its settings now, so Play works without a SaveSystem in the scene.</summary>
```

  заменить на:

```csharp
        /// <summary>A Saveable just added: the project gets its settings now (see the class summary).</summary>
```

  Найти:

```csharp
            // A hidden helper object (SaveCatalogBuilder learns the Saveable script path through one) is not an added
            // Saveable: creating settings from it would re-enter FindOrCreateSettings and touch projects on a rebuild
```

  заменить на:

```csharp
            // A hidden helper object (SaveCatalogBuilder learns the Saveable script path through one) is not an added
            // Saveable: it would re-enter FindOrCreateSettings, and a rebuild would create the asset in a project that
            // has none
```

- [ ] **Step 9: `SaveCatalogBuilder` — связь с проверкой.** Найти:

```csharp
        private static string SaveableScriptPath()
        {
            var holder = new GameObject { hideFlags = HideFlags.HideAndDontSave };
```

  заменить на:

```csharp
        private static string SaveableScriptPath()
        {
            // HideAndDontSave also tells SaveSystemSetup.EnsureSettingsFor that this Saveable is a helper: keep the flag
            var holder = new GameObject { hideFlags = HideFlags.HideAndDontSave };
```

- [ ] **Step 10: Собрать и прогнать.** Путь A: проверка Play, `Refresh`, консоль без `error CS`. EditMode
  `assemblyNames = { "SaveSystem.Tests" }` и PlayMode `assemblyNames = { "SaveSystem.Tests.Runtime" }`. Ожидание: как
  до правок — 361/361 и 106/106 (поведение для существующих тестов не меняется).

- [ ] **Step 11: Коммит** — только с разрешения пользователя.

### Task 5: Тесты EditMode — `LoadInPlace`, эдитор, защита игры

**Files:**
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/LoadInPlaceTests.cs`
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/SaveSystemSetupTests.cs`
- Modify: `Assets/Game/Tests/EditMode/SaveSetupTests.cs`

**Interfaces:**
- Consumes: `SaveLoadService.LoadInPlace()`, `SaveLoadService.KeepsScene` (задача 1);
  `SaveSystemSettings.RESOURCES_NAME`, `SaveSystemSettings.CreateSystemWhenMissing` (задача 2);
  `SaveSystemSetup.NEW_SETTINGS_PATH`, `SaveSystemSetup.IsRuntimeSettingsPath` (задача 3).

- [ ] **Step 1: Создать `LoadInPlaceTests.cs`:**

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Saving.Tests
{
    /// <summary>LoadInPlace: a load during which the objects layer keeps the scenes; outside it, Load as usual.</summary>
    public class LoadInPlaceTests
    {
        /// <summary>Notes what the service told the layer while checking the file and restoring it.</summary>
        private sealed class SceneProbe : ISaveExtension
        {
            public SaveLoadService Service;
            public bool? KeptSceneOnCheck;
            public bool? KeptSceneOnBegin;

            public void CaptureInto(IDictionary<string, SaveSection> sections) =>
                sections["$probe"] = new SaveSection { V = 1, Data = new JValue(1) };

            public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
            {
                KeptSceneOnCheck = Service.KeepsScene;
                problem = null;
                return LoadOutcome.Loaded;
            }

            public void BeginLoad(Action finished)
            {
                KeptSceneOnBegin = Service.KeepsScene;
                finished();
            }

            public void Dispose()
            {
            }
        }

        private static (SaveLoadService Service, SceneProbe Probe) CreateSaved()
        {
            var storage = new InMemorySaveStorage();
            var writer = new SaveLoadService(storage, "1.0");
            writer.Attach(new SceneProbe { Service = writer });
            writer.Save();

            var service = new SaveLoadService(storage, "1.0");
            var probe = new SceneProbe { Service = service };
            service.Attach(probe);
            return (service, probe);
        }

        [Test]
        public void LoadInPlace_LayerKeepsSceneWhileCheckingAndRestoring()
        {
            (SaveLoadService service, SceneProbe probe) = CreateSaved();

            Assert.AreEqual(LoadOutcome.Loaded, service.LoadInPlace());

            Assert.AreEqual(true, probe.KeptSceneOnCheck);
            Assert.AreEqual(true, probe.KeptSceneOnBegin);
            Assert.IsFalse(service.KeepsScene, "only during that load");
        }

        [Test]
        public void Load_LayerDoesNotKeepScene()
        {
            (SaveLoadService service, SceneProbe probe) = CreateSaved();

            Assert.AreEqual(LoadOutcome.Loaded, service.Load());

            Assert.AreEqual(false, probe.KeptSceneOnCheck);
            Assert.AreEqual(false, probe.KeptSceneOnBegin);
        }

        [Test]
        public void LoadInPlace_EmptySlot_EmptyAndFlagOff()
        {
            var service = new SaveLoadService(new InMemorySaveStorage(), "1.0");

            Assert.AreEqual(LoadOutcome.Empty, service.LoadInPlace());
            Assert.IsFalse(service.KeepsScene);
        }

        [Test]
        public void LoadInPlace_LoadedSubscriber_SeesAnOrdinaryLoad()
        {
            (SaveLoadService service, _) = CreateSaved();
            bool? keptSceneInLoaded = null;
            service.Loaded += _ => keptSceneInLoaded = service.KeepsScene;

            Assert.AreEqual(LoadOutcome.Loaded, service.LoadInPlace());

            Assert.AreEqual(false, keptSceneInLoaded, "a subscriber that loads again must get an ordinary load");
        }
    }
}
```

- [ ] **Step 2: `SaveSystemSetupTests.cs` — using.** Добавить `using Saving.Objects;` к остальным `using`.

- [ ] **Step 3: `SaveSystemSetupTests.cs` — тесты.** Перед закрывающей скобкой класса вставить:

```csharp

        [Test]
        public void NewSettingsPath_FoundInPlayByScenesWithoutASystem()
        {
            Assert.IsTrue(SaveSystemSetup.IsRuntimeSettingsPath(SaveSystemSetup.NEW_SETTINGS_PATH));
        }

        [Test]
        public void IsRuntimeSettingsPath_OutsideResourcesNestedOrRenamed_False()
        {
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath("Assets/Settings/SaveSystemSettings.asset"));
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath("Assets/Settings/Resources/Saves/SaveSystemSettings.asset"));
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath("Assets/Settings/Resources/MySaveSettings.asset"));
            Assert.IsFalse(SaveSystemSetup.IsRuntimeSettingsPath(null));
        }

        [Test]
        public void NewSettings_CreateSystemWhenMissingOn()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();

            Assert.IsTrue(settings.CreateSystemWhenMissing);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void AddSaveable_SettingsExist_NoNewAsset()
        {
            int before = AssetDatabase.FindAssets("t:" + nameof(SaveSystemSettings)).Length;

            new GameObject("Box").AddComponent<Saveable>();

            Assert.AreEqual(before, AssetDatabase.FindAssets("t:" + nameof(SaveSystemSettings)).Length);
        }

        [Test]
        public void Inspector_CreateSystemWhenMissingField_Found()
        {
            var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();

            Assert.IsNotNull(new SerializedObject(settings).FindProperty("_createSystemWhenMissing"),
                "SaveSystemSettingsEditor draws the flag by this name");
            Object.DestroyImmediate(settings);
        }
```

  (ветку «ассета нет — создать» в нашем проекте не прогнать: ассет у нас уже есть; её путь держит первый тест.)

- [ ] **Step 4: `SaveSetupTests.cs` (игра).** После теста `SaveSettings_CatalogsUpToDate` вставить:

```csharp

        [Test]
        public void SaveSettings_NotInResources()
        {
            // The game's save system is the one of [Services]. Settings in Resources would make the module create its
            // own wherever a Saveable wakes without one — in every PlayMode test that spawns the player
            Assert.IsNull(Resources.Load<SaveSystemSettings>(SaveSystemSettings.RESOURCES_NAME));
        }
```

- [ ] **Step 5: Прогнать.** EditMode `groupNames = { "LoadInPlaceTests", "SaveSystemSetupTests", "SaveSetupTests" }`.
  Ожидание: PASS (код уже есть). Падение — это баг: тест уже его воспроизводит, исправить код
  (superpowers:systematic-debugging), прогнать снова.

- [ ] **Step 6: Коммит** — только с разрешения пользователя (в автономном режиме — локальный коммит шага):

```bash
git add Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/LoadInPlaceTests.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/LoadInPlaceTests.cs.meta \
  Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/SaveSystemSetupTests.cs \
  Assets/Game/Tests/EditMode/SaveSetupTests.cs
git commit -m "$(cat <<'EOF'
Test LoadInPlace, the runtime settings path and the game's guard

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Тесты PlayMode — поведение автосистемы

**Files:**
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/AutoCreateOff.cs`
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/SaveSystemAutoCreateTests.cs`

**Interfaces:**
- Consumes: `SaveSystemAutoCreate.SYSTEM_NAME`, `.FindSettings`, `.BeforeStart`, `.Reset()`, `SaveRegistry.Registered`,
  `SaveSystemSettings.CreateSystemWhenMissing` (задача 2); помощники модуля `TestScenes` (`Load`, `Find`,
  `CreateSettings`, `GuidOf`, `Prefab`, `SCENE`, `MODULE`, `BOX`, `HIDDEN`, `WaitFor`) и `SaveTestCounter` (`Value`,
  `Starts`; его `Start` ставит `Value = 1`). В тестовой сцене — `Box`, выключенный `Hidden` и другие объекты с
  `Saveable`, системы нет.

- [ ] **Step 0: Изоляция тестов модуля — создать `AutoCreateOff.cs`.** Модуль носит тесты с собой: в проекте, где ассет
  настроек лежит в `Resources` с галочкой (любой проект новичка после первого `Saveable`), сцены тестов создали бы
  автосистему раньше системы теста, и та стала бы копией. Выключить автосоздание на всю сборку (`SetUpFixture` в
  namespace `Saving.Tests` действует на все её тесты), включать его — только в тестах автосистемы:

```csharp
using NUnit.Framework;
using Saving.Objects;

namespace Saving.Tests
{
    /// <summary>
    /// The module's PlayMode tests make their own save systems: in a project whose settings sit in Resources, the test
    /// scenes' Saveables would make an automatic one first. Off for the whole assembly; SaveSystemAutoCreateTests turn it
    /// on per test and back off after.
    /// </summary>
    [SetUpFixture]
    public class AutoCreateOff
    {
        [OneTimeSetUp]
        public void TurnOff() => SaveSystemAutoCreate.FindSettings = () => null;

        [OneTimeTearDown]
        public void Restore() => SaveSystemAutoCreate.Reset();
    }
}
```

- [ ] **Step 1: Создать `SaveSystemAutoCreateTests.cs`:**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
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
    /// A project without a SaveSystem in its scenes: the first Saveable makes one from the settings asset and loads its
    /// slot in place. A system in the scene or made earlier wins; without the asset or with the flag off nothing is made.
    /// The settings come from a hook instead of Resources, the storage is in memory.
    /// </summary>
    public class SaveSystemAutoCreateTests
    {
        private static readonly string MINIMAL_DEMO = TestScenes.MODULE + "/Demo/SaveSystemMinimal.unity";

        private InMemorySaveStorage _storage;
        private SaveSystemSettings _settings;

        [SetUp]
        public void SetUp()
        {
            DestroySystems();
            _storage = new InMemorySaveStorage();
            _settings = TestScenes.CreateSettings();
            Hook();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystemAutoCreate.Reset();
            SaveSystemAutoCreate.FindSettings = () => null; // back off for the rest of the assembly (AutoCreateOff)
            DestroySystems();
            Object.DestroyImmediate(_settings);
        }

        private void Hook()
        {
            SaveSystemAutoCreate.Reset();
            SaveSystemAutoCreate.FindSettings = () => _settings;
            SaveSystemAutoCreate.BeforeStart = system =>
            {
                system.UseStorage(_storage);
                system.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            };
        }

        private static void DestroySystems()
        {
            foreach (SaveLoadSystem system in Object.FindObjectsByType<SaveLoadSystem>())
                Object.DestroyImmediate(system.gameObject);
        }

        private static SaveTestCounter Counter(string name) => TestScenes.Find(name).GetComponent<SaveTestCounter>();

        /// <summary>Points the saved active scene somewhere else, as if the last save was made there.</summary>
        private void MoveSavedActiveScene(string path, string guid)
        {
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
            var active = (JObject)json["sections"][SceneSaving.SCENES_KEY]["data"]["active"];
            active["path"] = path;
            active["guid"] = guid;
            _storage.Put(0, SaveFiles.SAVE, json.ToString());
        }

        /// <summary>A save of an earlier run in the open scene, written by a system that is gone again.</summary>
        private void SaveBoxAs(int value)
        {
            var writer = new GameObject("Writer").AddComponent<SaveLoadSystem>();
            writer.UseStorage(_storage);
            writer.UseSettings(_settings);
            writer.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);
            int current = Counter("Box").Value;
            Counter("Box").Value = value;
            writer.Save();
            Counter("Box").Value = current;
            Object.DestroyImmediate(writer.gameObject);
        }

        /// <summary>What happened to scenes while a step ran: scene loads, and whether a load was still running after it.</summary>
        private sealed class SceneWatch
        {
            public int Loads;
            public bool LoadingAfter;
        }

        /// <summary>
        /// Runs the step and notes right after it whether the live system is still loading (an in-place load is over by
        /// then, a load of the saved scene is not), then waits on the real clock: a scene switch arrives later than a frame.
        /// </summary>
        private static IEnumerator WatchScenes(IEnumerator step, SceneWatch watch)
        {
            void Count(Scene scene, LoadSceneMode mode) => watch.Loads++;
            SceneManager.sceneLoaded += Count;
            try
            {
                yield return step;
                watch.LoadingAfter = SaveLoadSystem.Current != null && SaveLoadSystem.Current.IsLoading;
                yield return new WaitForSecondsRealtime(1f);
            }
            finally
            {
                SceneManager.sceneLoaded -= Count;
            }
        }

        [UnityTest]
        public IEnumerator NoSystem_SceneLoads_OneAutoSystemForAllScenes()
        {
            yield return TestScenes.Load();

            SaveLoadSystem[] systems = Object.FindObjectsByType<SaveLoadSystem>();
            Assert.AreEqual(1, systems.Length, "one system for all the Saveables of the scene");
            Assert.AreSame(systems[0], SaveLoadSystem.Current);
            Assert.AreEqual(SaveSystemAutoCreate.SYSTEM_NAME, systems[0].name);
            Assert.AreEqual("DontDestroyOnLoad", systems[0].gameObject.scene.name);

            yield return TestScenes.Load();

            Assert.AreSame(systems[0], SaveLoadSystem.Current, "the next scene uses the same system");
            Assert.AreEqual(1, Object.FindObjectsByType<SaveLoadSystem>().Length);
        }

        [UnityTest]
        public IEnumerator SavedThenRestarted_StateBackWithoutASecondSceneLoad()
        {
            yield return TestScenes.Load();
            Counter("Box").Value = 42;
            Counter("Hidden").Value = 5;
            TestScenes.Find("Box").transform.position = new Vector3(4f, 1f, 0f);
            SaveLoadSystem.Current.Save();
            DestroySystems(); // the game is over

            var watch = new SceneWatch();
            yield return WatchScenes(TestScenes.Load(), watch); // the next run

            Assert.IsFalse(watch.LoadingAfter, "the slot loaded in place, no scene load was running");
            Assert.AreEqual(1, watch.Loads, "the slot loads into the scene, nothing else is loaded");
            Assert.AreEqual(42, Counter("Box").Value);
            Assert.AreEqual(5, Counter("Hidden").Value, "a disabled object gets its data too");
            Assert.AreEqual(new Vector3(4f, 1f, 0f), TestScenes.Find("Box").transform.position);
        }

        [UnityTest]
        public IEnumerator SavedInAnotherScene_StaysHereWithThisScenesState()
        {
            yield return TestScenes.Load();
            Counter("Box").Value = 42;
            SaveLoadSystem.Current.Save();
            MoveSavedActiveScene(MINIMAL_DEMO, TestScenes.GuidOf(MINIMAL_DEMO));
            DestroySystems();

            var watch = new SceneWatch();
            yield return WatchScenes(TestScenes.Load(), watch);

            Assert.IsFalse(watch.LoadingAfter, "no load of the saved scene was running");
            Assert.AreEqual(1, watch.Loads, "no switch to the saved scene");
            Assert.AreEqual(TestScenes.SCENE, SceneManager.GetActiveScene().path);
            Assert.AreEqual(42, Counter("Box").Value, "this scene got its saved state");
        }

        [UnityTest]
        public IEnumerator SavedSceneGone_StillLoadsHere()
        {
            yield return TestScenes.Load();
            Counter("Box").Value = 42;
            SaveLoadSystem.Current.Save();
            MoveSavedActiveScene("Assets/Gone/Level.unity", "00000000000000000000000000000000");
            DestroySystems();

            yield return TestScenes.Load();

            Assert.AreEqual(42, Counter("Box").Value);
            Assert.IsFalse(SaveLoadSystem.Current.IsReadOnly, "a missing saved scene does not refuse the slot");
        }

        [UnityTest]
        public IEnumerator FirstSaveableSpawnedMidScene_SceneNotReloadedKeepsItsState()
        {
            SaveSystemAutoCreate.FindSettings = () => null; // the scene starts without a system
            yield return TestScenes.Load();
            Assert.IsNull(SaveLoadSystem.Current);
            SaveBoxAs(42);
            SaveTestCounter box = Counter("Box");
            box.Value = 3;
            Hook();

            IEnumerator Spawn()
            {
                Object.Instantiate(TestScenes.Prefab); // the first Saveable since the scene started
                yield return null;
            }

            var watch = new SceneWatch();
            yield return WatchScenes(Spawn(), watch);

            Assert.IsNotNull(SaveLoadSystem.Current, "the spawned Saveable made the system");
            Assert.IsFalse(watch.LoadingAfter, "no scene load was running");
            Assert.AreEqual(0, watch.Loads, "no scene was loaded");
            Assert.AreSame(box, Counter("Box"));
            Assert.AreEqual(3, box.Value, "a started scene keeps its state");
        }

        [UnityTest]
        public IEnumerator AfterAutoSystem_Load_ReloadsTheSavedSceneAsBefore()
        {
            yield return TestScenes.Load();
            SaveTestCounter before = Counter("Box");
            before.Value = 42;
            SaveLoadSystem.Current.Save();
            before.Value = 3;

            bool done = false;
            void OnLoaded(LoadOutcome _) => done = true;
            SaveLoadSystem.Current.Loaded += OnLoaded;
            Assert.AreEqual(LoadOutcome.Loaded, SaveLoadSystem.Current.Load());
            yield return TestScenes.WaitFor(() => done);
            SaveLoadSystem.Current.Loaded -= OnLoaded;
            yield return null;

            Assert.AreNotSame(before, Counter("Box"), "a button's Load reloads the saved scene");
            Assert.AreEqual(42, Counter("Box").Value);
        }

        [UnityTest]
        public IEnumerator SystemInTheScene_Wins()
        {
#if UNITY_EDITOR
            void UseMemory(Scene scene, LoadSceneMode mode) => SaveLoadSystem.Current.UseStorage(_storage);
            SceneManager.sceneLoaded += UseMemory;
            Scene demo = EditorSceneManager.LoadSceneInPlayMode(MINIMAL_DEMO, new LoadSceneParameters(LoadSceneMode.Single));
            yield return TestScenes.WaitFor(() => demo.isLoaded);
            SceneManager.sceneLoaded -= UseMemory;
            yield return null;

            Assert.AreEqual(1, Object.FindObjectsByType<SaveLoadSystem>().Length);
            Assert.AreNotEqual(SaveSystemAutoCreate.SYSTEM_NAME, SaveLoadSystem.Current.name, "the scene's own system is live");
#else
            Assert.Ignore("The demo scene loads only in the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SystemMadeEarlier_Wins()
        {
            var services = new GameObject("[Services]");
            Object.DontDestroyOnLoad(services);
            var own = services.AddComponent<SaveLoadSystem>();
            own.UseStorage(_storage);
            own.Configure(saveOnQuit: false, saveOnPause: false, autoSavePeriod: 0f);

            yield return TestScenes.Load();

            Assert.AreSame(own, SaveLoadSystem.Current);
            Assert.AreEqual(1, Object.FindObjectsByType<SaveLoadSystem>().Length);
        }

        [UnityTest]
        public IEnumerator FlagOff_NoSystem()
        {
            _settings.CreateSystemWhenMissing = false;

            yield return TestScenes.Load();

            Assert.IsNull(SaveLoadSystem.Current);
        }

        [UnityTest]
        public IEnumerator NoSettingsAsset_NoSystemAndOneLookup()
        {
            int lookups = 0;
            SaveSystemAutoCreate.FindSettings = () =>
            {
                lookups++;
                return null;
            };

            yield return TestScenes.Load(); // several Saveables wake
            Object.Instantiate(TestScenes.Prefab);
            yield return null;

            Assert.IsNull(SaveLoadSystem.Current);
            Assert.AreEqual(1, lookups, "the answer is remembered: later Saveables do not look for the asset again");
        }

        [UnityTest]
        public IEnumerator SaveSystemAddedLater_IsACopyThatForwards()
        {
            yield return TestScenes.Load();
            SaveLoadSystem auto = SaveLoadSystem.Current;

            var copy = new GameObject("SaveSystem").AddComponent<SaveLoadSystem>(); // the prefab in a later scene
            copy.UseStorage(new InMemorySaveStorage()); // if forwarding ever broke, the copy must not write real saves
            copy.SaveGame(); // a button wired to the scene's copy

            Assert.IsTrue(copy.IsIdle);
            Assert.AreSame(auto, SaveLoadSystem.Current);
            Assert.IsTrue(_storage.TryRead(0, SaveFiles.SAVE, out _), "the live system wrote the slot");
            Object.DestroyImmediate(copy.gameObject);
        }

        [UnityTest]
        public IEnumerator Registered_ReportsDisabledSaveablesOfALoadingScene()
        {
            var registered = new List<string>();
            void Record(Saveable saveable) => registered.Add(saveable.Id);
            SaveRegistry.Registered += Record;

            yield return TestScenes.Load();

            SaveRegistry.Registered -= Record;
            CollectionAssert.Contains(registered, TestScenes.BOX);
            CollectionAssert.Contains(registered, TestScenes.HIDDEN, "the scan reports a disabled Saveable too");
        }

        [UnityTest]
        public IEnumerator NewLookupAfterAnEarlierNo_CreatesTheSystem()
        {
            SaveSystemAutoCreate.FindSettings = () => null;
            yield return TestScenes.Load(); // no system: the "no" is remembered
            Assert.IsNull(SaveLoadSystem.Current);

            SaveSystemAutoCreate.FindSettings = () => _settings; // no Reset: a new lookup forgets the "no"
            Object.Instantiate(TestScenes.Prefab);
            yield return null;

            Assert.IsNotNull(SaveLoadSystem.Current);
        }

        [UnityTest]
        public IEnumerator BrokenLookup_LoggedAndTheSceneScanCompletes()
        {
            SaveSystemAutoCreate.FindSettings = () => throw new InvalidOperationException("broken settings lookup");
            LogAssert.Expect(LogType.Exception, new Regex("broken settings lookup"));

            yield return TestScenes.Load();

            Assert.IsNull(SaveLoadSystem.Current);
            Assert.IsTrue(SaveRegistry.TryGetRecord(SceneManager.GetActiveScene(), out _),
                "the registry finished the scan of the scene");
        }

        [UnityTest]
        public IEnumerator ThrowingSetup_LoggedAndNoHalfBuiltSystemLeft()
        {
            SaveSystemAutoCreate.BeforeStart = _ => throw new InvalidOperationException("broken setup");
            LogAssert.Expect(LogType.Exception, new Regex("broken setup"));

            yield return TestScenes.Load();
            yield return null; // Destroy takes effect at the end of a frame

            Assert.IsNull(SaveLoadSystem.Current, "the half-built system was removed");
            Assert.AreEqual(0, Object.FindObjectsByType<SaveLoadSystem>(FindObjectsInactive.Include).Length,
                "destroyed, not just switched off");
        }
    }
}
```

  Сверка со спеком (раздел 7): «затем вход в B — у B её состояние» — это обычное применение секции сцены при скане, его
  уже держат тесты смены сцен в `SceneSavingTests`; «выключенный `Saveable` — единственный в сцене» — тот же обработчик
  события: тест `Registered_ReportsDisabledSaveablesOfALoadingScene` показывает, что скан сообщает о выключенных, а
  `SavedThenRestarted_…` — что выключенный `Hidden` получает данные.

- [ ] **Step 2: Прогнать.** PlayMode `groupNames = { "SaveSystemAutoCreateTests" }`. Ожидание: 15 PASS. Падение — баг
  кода или неверное ожидание теста: разобрать (superpowers:systematic-debugging), исправить причину, прогнать снова.

- [ ] **Step 3: Соседи.** PlayMode `assemblyNames = { "SaveSystem.Tests.Runtime" }` — всё зелёное (хуки сбрасываются в
  `TearDown`, другие тесты автосистему не видят).

- [ ] **Step 4: Коммит** — только с разрешения пользователя:

```bash
git add Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/SaveSystemAutoCreateTests.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/SaveSystemAutoCreateTests.cs.meta \
  Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/AutoCreateOff.cs \
  Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/AutoCreateOff.cs.meta
git commit -m "$(cat <<'EOF'
Test the save system a Saveable creates when there is none

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Документация, полный прогон, итог

**Files:**
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/README.md`
- Modify: `Assets/AI/CLAUDE.md`
- Modify: `Assets/AI/Docs/SaveSystem_AutoCreate.md`

- [ ] **Step 1: README — вступление.** Заменить:

```markdown
Сохранения для Unity «из коробки»: кинул префаб в сцену — игра сохраняется; повесил `Saveable` на объект — он
сохраняется со всеми компонентами, которые ты отметил; из кода — полный контроль, слоты, версии и миграции, любые
хранилища и серверы. Модуль самостоятельный: не зависит от игры, нужен только Newtonsoft JSON
(`com.unity.nuget.newtonsoft-json`; для меню слотов — uGUI и TextMesh Pro). Проверено на Unity 6000.5.
```

  на:

```markdown
Сохранения для Unity «из коробки»: повесил `Saveable` на объект или префаб — он сохраняется со всеми компонентами,
которые ты отметил, а систему сохранений модуль создаёт сам; кинул префаб системы в сцену — свои настройки и кнопки;
из кода — полный контроль, слоты, версии и миграции, любые хранилища и серверы. Модуль самостоятельный: не зависит от
игры, нужен только Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`; для меню слотов — uGUI и TextMesh Pro).
Проверено на Unity 6000.5.
```

- [ ] **Step 2: README — спеки.** Заменить:

```markdown
Спеки — `Assets/AI/Docs/SaveSystem.md` (части 1–3) и `Assets/AI/Docs/SaveSystem_Part4.md` (простой путь, режимы
компонентов, слоты, хранилища, меню). Демо
```

  на:

```markdown
Спеки — `Assets/AI/Docs/SaveSystem.md` (части 1–3), `Assets/AI/Docs/SaveSystem_Part4.md` (простой путь, режимы
компонентов, слоты, хранилища, меню), `Assets/AI/Docs/SaveSystem_AutoCreate.md` (система создаётся сама). Демо
```

- [ ] **Step 3: README — быстрый старт, шаг 1.** Заменить:

```markdown
1. **Система.** Перетащи `Prefabs/SaveSystem` в сцену игры (или *GameObject → Save System → Save System*).
   Всё: при старте загружается слот, сохранение — каждые 30 с игрового времени (если что-то поменялось), при паузе
   приложения и при выходе (всё это выключается в инспекторе, группа *When to save*). Первая проснувшаяся система
   переживает смену сцен; префаб можно класть в каждую сцену — копии ничего не делают сами и передают свои вызовы и
   события первой. Ассет настроек `SaveSystemSettings` редактор находит или создаёт сам (`Assets/Settings/`).
2. **Объекты.** Добавь компонент `Saveable` на объект или префаб. В инспекторе — все компоненты объекта и детей:
```

  на:

```markdown
1. **Объекты.** Добавь компонент `Saveable` на объект или префаб — и жми Play: систему в сцену класть не нужно.
   Первый проснувшийся `Saveable` не находит системы и создаёт её сам (`SaveSystem (auto)`, переживает смену сцен):
   при старте загружает слот 0, оставаясь в текущей сцене (каждая сцена получает своё состояние, когда загрузится),
   сохраняет каждые 30 с игрового времени (если что-то поменялось), при паузе приложения и при выходе. Ассет настроек
   `SaveSystemSettings` редактор создаёт сам в `Assets/Settings/Resources/` — там его находит автосистема; галочка
   *Create System When Missing* у ассета её выключает. В инспекторе `Saveable` — все компоненты объекта и детей:
```

- [ ] **Step 4: README — шаги 2 и 3.** Заменить строку:

```markdown
3. **Кнопки.** В `Button.onClick` перетащи объект `SaveSystem` своей сцены и выбери метод `SaveLoadSystem`:
```

  на:

```markdown
2. **Своя система (по желанию).** Нужны свои настройки — хранилище, шифрование, автосейв, слот, загрузка при старте —
   перетащи `Prefabs/SaveSystem` в первую сцену (или *GameObject → Save System → Save System*): система в сцене главнее
   автосистемы. Первая проснувшаяся система переживает смену сцен; префаб можно класть в каждую сцену — копии ничего не
   делают сами и передают свои вызовы и события первой (и автосистеме тоже).
3. **Кнопки.** В `Button.onClick` перетащи объект `SaveSystem` своей сцены (префаб из шага 2) и выбери метод `SaveLoadSystem`:
```

- [ ] **Step 5: README — «Из кода».** Заменить:

```markdown
saves.Load();                                          // "Continue", F9
```

  на:

```markdown
saves.Load();                                          // "Continue", F9
SaveLoadSystem.Current.LoadInPlace();                  // load keeping the scenes: how the auto system starts
```

  и заменить:

```markdown
Регистрация — когда система появилась, снятие — когда исчезла;
```

  на:

```markdown
`SaveLoadSystem.Current` — `null`, пока системы нет: без префаба в сцене она появляется с первым `Saveable`.
Регистрация — когда система появилась, снятие — когда исчезла;
```

- [ ] **Step 6: README — состав и тесты.** После строки

```text
│   ├── SaveSystemSettings       — ассет настроек: подключает слой объектов, каталоги префабов, сцен и ассетов
```

  вставить строку

```text
│   ├── SaveSystemAutoCreate     — система для сцены без неё: её создаёт первый Saveable (по ассету в Resources)
```

  В разделе «Тесты» заменить `сервис, значения, слоты,` на:

```markdown
сервис, загрузка без смены сцены (`LoadInPlace`), значения, слоты,
```

  и `сценах, кнопки, события, хранилища), реестр и сцена,` на:

```markdown
сценах, кнопки, события, хранилища), автосистема (создаёт первый `Saveable`, система в сцене главнее, загрузка без
смены сцены, появление посреди игры), реестр и сцена,
```

- [ ] **Step 7: CLAUDE.md.** Файл несёт чужие незакоммиченные правки: сначала `git diff Assets/AI/CLAUDE.md`; править
  только эти места (найти их `grep`, текст мог сдвинуться).

  «Что сделано», пункт «Сохранения» — заменить `[часть 4](Docs/SaveSystem_Part4.md)):` на:

```markdown
[часть 4](Docs/SaveSystem_Part4.md), [автосоздание](Docs/SaveSystem_AutoCreate.md)):
```

  и `простой путь «кинул и работает» (префабы модуля,` на:

```markdown
простой путь «кинул и работает» (система создаётся сама с первым `Saveable`, префабы модуля,
```

  «Архитектура → Сохранения» — заменить `каталоги префабов и сцен эдитор обновляет сам. Загрузка перезагружает` на:

```markdown
каталоги префабов и сцен эдитор обновляет сам. Сцене без системы модуль создаёт её сам: первый `Saveable` делает
`SaveSystem (auto)` по ассету `Resources/SaveSystemSettings` и грузит слот без смены сцены (`LoadInPlace`); в игре это
не срабатывает — ассет игры вне `Resources` (держит `SaveSetupTests.SaveSettings_NotInResources`). Загрузка перезагружает
```

  Строку «Тесты: EditMode …/…, PlayMode …/…» в «Текущем состоянии» — числами из полного прогона шага 9 и датой.

- [ ] **Step 8: Спек.** В `Assets/AI/Docs/SaveSystem_AutoCreate.md` строку статуса заменить на
  `**Статус:** готово <дата>: прототип и финализация по плану [SaveSystem_AutoCreate_Plan.md](SaveSystem_AutoCreate_Plan.md);
  тесты — EditMode <N>/<N>, PlayMode <M>/<M>.` (числа и дата — из шага 9).

  Перед разделом «## 10. За рамками, можно позже» спека вставить раздел:

```markdown
## Отличия реализации от дизайна

- `SaveSystemSetup.EnsureSettingsFor` пропускает скрытый вспомогательный `Saveable` (`HideAndDontSave`), которым
  `SaveCatalogBuilder` узнаёт путь скрипта: иначе «Rebuild Catalogs» и шаг перед сборкой создавали бы ассет в проекте
  без него, а ветка создания ассета заходила бы сама в себя (решение пользователя по ревью задачи 3).
- `SaveSystemAutoCreate.FindSettings` — свойство: новый способ поиска забывает запомненный отказ. Исключение при
  автосоздании пишется в лог и запоминается как отказ: оно приходит из скана загружаемой сцены и не должно его прервать.
- `SaveLoadService.KeepsScene` сбрасывается до события `Loaded`: подписчик, который грузит снова, получает обычную
  загрузку. `SceneSaving.BeginLoad` читает флаг один раз в начале: восстановление объектов DontDestroyOnLoad может
  закончить загрузку раньше и сбросить его (решение пользователя по ревью задачи 8).
- Недоделанная автосистема (ошибка после её создания) выключается и удаляется: она не загрузила слот, и её сохранения
  затёрли бы его.
- Тестовая сборка модуля `SaveSystem.Tests.Runtime` выключает автосоздание (`AutoCreateOff`, `SetUpFixture`): в проекте
  с ассетом в `Resources` сцены тестов иначе создали бы автосистему раньше системы теста.
```

  В README модуля, раздел «Тесты», в конец абзаца дописать:

```markdown
Тестовая сборка модуля выключает автосоздание (`AutoCreateOff`): в проекте с ассетом настроек в `Resources` её сцены
иначе создали бы автосистему раньше системы теста. Свои PlayMode-тесты, которые делают систему сами, — создавай её до
загрузки сцены: созданная раньше главнее.
```

- [ ] **Step 9: Полный прогон — один раз.** Путь A: `EditorApplication.ExecuteMenuItem("Tools/Game/Run All Tests")`,
  итог в `Temp/GameTestResults.txt`. Путь B: команда пути B с `-testPlatform EditMode`, затем с `-testPlatform PlayMode`,
  без `-assemblyNames`. Ожидание: всё зелёное; консоль чистая (`Unity_GetConsoleLogs` / `error`/`Exception` в логе).
  Первый EditMode-прогон после импорта может уронить `SaveSetupTests.SaveSettings_CatalogsUpToDate` — каталог ещё
  дописывается, повторный прогон зелёный (CLAUDE.md).

- [ ] **Step 10: Коммит** — только с разрешения пользователя. README и спек — своими путями; CLAUDE.md — только свои
  куски: применить свои правки к `git show HEAD:Assets/AI/CLAUDE.md` во временном файле в scratchpad, затем
  `git hash-object -w --path=Assets/AI/CLAUDE.md <tmp>` и
  `git update-index --cacheinfo 100644,<hash>,Assets/AI/CLAUDE.md` (рабочая копия остаётся со всеми правками).

```bash
git add Assets/Game/Scripts/Modules/SaveSystem/README.md Assets/AI/Docs/SaveSystem_AutoCreate.md
git commit -m "$(cat <<'EOF'
Document the save system that a Saveable creates when there is none

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

  Путь B: перед коммитом вернуть файлы, которые трогает batchmode (Task 4, шаг 5); слияние ветки
  `feature/save-autocreate` в `main` и удаление worktree — по просьбе пользователя.

---

### Task 9: Переносимость — тесты модуля в `SaveSystemTry` (после задачи 7)

**Files:** в репозитории — нет. Меняется только копия модуля в `C:\!!!Work\SaveSystemTry\Assets\SaveSystem` (проект вне
репозитория, собран для ручной проверки прототипа; ассет настроек там уже лежит в `Assets/Settings/Resources/` — ровно
тот случай, от которого защищает `AutoCreateOff`).

**Interfaces:**
- Consumes: финальный код модуля и его тесты (задачи 8, 5, 6, 7).

- [ ] **Step 1: Проект не открыт.** Если существует `C:\!!!Work\SaveSystemTry\Temp\UnityLockfile`, проект открыт в
  эдиторе — batchmode его не откроет: остановиться и сообщить контроллеру (BLOCKED), ничего не трогать.

- [ ] **Step 2: Обновить копию модуля** (папка — наша копия, её можно заменить целиком; ассеты пользователя в
  `Assets/Settings` и его сцены не трогать):

```bash
rm -rf "/c/!!!Work/SaveSystemTry/Assets/SaveSystem"
cp -r "/c/!!!Work/Platformer/Assets/Game/Scripts/Modules/SaveSystem" "/c/!!!Work/SaveSystemTry/Assets/SaveSystem"
```

- [ ] **Step 3: Прогнать тесты модуля в batchmode** — сначала EditMode, затем PlayMode:

```bash
"/c/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath "C:/!!!Work/SaveSystemTry" \
  -runTests -testPlatform EditMode -assemblyNames "SaveSystem.Tests" \
  -testResults "<scratchpad>/try-EditMode.xml" -logFile "<scratchpad>/try-EditMode.log"
"/c/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe" -batchmode -projectPath "C:/!!!Work/SaveSystemTry" \
  -runTests -testPlatform PlayMode -assemblyNames "SaveSystem.Tests.Runtime" \
  -testResults "<scratchpad>/try-PlayMode.xml" -logFile "<scratchpad>/try-PlayMode.log"
```

  Ожидание: всё зелёное, `Assets/Settings/Resources/SaveSystemSettings.asset` на месте и не изменён (`git hash-object`
  до и после). Падения разобрать: связанные с автосозданием (система теста стала копией, `SaveSystem (auto)` в
  тестах) — баг финализации; связанные с окружением чистого проекта (например, нет TMP Essential Resources для
  демо-сцен) — записать в отчёт с текстом падения, не чинить.

- [ ] **Step 4: Отчёт** — числа EditMode и PlayMode, список падений с причинами. Коммитов нет (проект вне репозитория).

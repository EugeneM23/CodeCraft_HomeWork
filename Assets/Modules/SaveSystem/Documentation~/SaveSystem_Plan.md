# SaveSystem, часть 1 — ядро: план реализации

> **Для исполнителя:** обязательный под-скилл — superpowers:executing-plans (инлайн, задача за задачей). Тесты идут
> через один Unity-эдитор по MCP, и каждая задача меняет его состояние (компиляция, перезагрузка домена), поэтому
> параллельные исполнители друг другу мешали бы. superpowers:subagent-driven-development возможен только строго
> по очереди. Шаги — чекбоксы (`- [ ]`).

**Цель:** ядро плагина сохранений — модуль `Assets/Game/Scripts/Modules/SaveSystem` со слотами, атомарной записью и `.bak`,
версиями и миграциями секций, глобальными системами через `GameSerializer<TService, TData>`, компонентом
`SaveLoadSystem` (автосейв, пауза, выход) — и подключение к игре: `GameServices`, префаб `[Services]`, F5/F9.

**Архитектура:** чистое C#-ядро `SaveLoadService` держит в памяти снимок секций текущего слота и пишет его через
`ISaveStorage` (`FileSaveStorage` на диске, `InMemorySaveStorage` в тестах). Секция — `{ v, data }` под ключом;
её пишет и читает `IGameSerializer`, общий цикл версий и миграций — `SerializerGroup`. MonoBehaviour
`SaveLoadSystem` на `[Services]` — тонкая обёртка: настройки, триггеры сохранения, реализация `ISaveLoadService`;
в `ServiceLocator` его кладёт `GameServices`. Объектов сцены (`Saveable`) в этой части нет — это часть 2.

**Стек:** Unity 6000.5.6f1, C# 9, Newtonsoft JSON (`com.unity.nuget.newtonsoft-json` 3.2.2), NUnit (Unity Test
Framework), EditMode + PlayMode, MCP `unity-mcp`.

Спек — [SaveSystem.md](SaveSystem.md), разделы 1, 2, 4, 5, 8–11. Исследование — [Research_SaveSystems.md](Research_SaveSystems.md).

## Глобальные ограничения

- Модуль — `Assets/Game/Scripts/Modules/SaveSystem/`; сборки `SaveSystem` (`Runtime/Core`), `SaveSystem.Editor` (`Editor`),
  `SaveSystem.Tests` (`Tests/Editor`, EditMode), `SaveSystem.Tests.Runtime` (`Tests/Runtime`, PlayMode); namespace
  `Saving`, `Saving.Editor`, `Saving.Tests`. Модуль не зависит от `Game`, `ServiceLocator` и Odin.
- Newtonsoft: `ObjectCreationHandling.Replace`, `TypeNameHandling.None`, `DateParseHandling.None`; имена членов —
  camelCase, ключи словарей — как есть; конвертеры `Vector2/3/4`, `Vector2Int/3Int`, `Quaternion`, `Color`,
  `Color32`, `Rect`, `Bounds`.
- Слот на диске: `<persistentDataPath>/Saves/slot_N/` — `save.json`, `meta.json`, `save.json.bak`,
  `save.premigration.json`, `*.corrupt`. `formatVersion` = 1. Ключи с `$` зарезервированы за плагином.
- Игровой код — namespace `Gameplay`; `GameServices` — `Assets/Game/Scripts/GameSystem/Services/GameServices.cs`; префаб —
  `Assets/Game/Prefabs/Services.prefab`.
- Стиль: Allman, 4 пробела, `_camelCase` приватные поля, `[SerializeField] private`, `UPPER_CASE` константы.
  Комментарии, `<summary>`, имена и сообщения — **по-английски**. Без `[Min]` (ограничения в `OnValidate`), без
  Odin, без `GetInstanceID()` (в 6000.5 — ошибка компиляции, есть `GetEntityId()`).
- TDD: тест → падает по правильной причине → минимальный код → зелёные **все** тесты. Чтобы тестовая сборка
  компилировалась, на шаге Red новый API — заглушками с `throw new NotImplementedException()`: тест падает на
  проверке, а не на сборке. Имена тестов — `Класс_Ситуация_Ожидание`.
- **Коммиты — только по просьбе пользователя или при разрешённой автономной работе;** перед коммитом — сводка.
  `git add` — только пути из шага, никогда `git add -A`: в рабочей копии чужие незакоммиченные правки
  (на 2026-10-23: `Assets/AI/CLAUDE.md`, `CompositionGuide.md`, `Stage1_Locomotion.md`, `Stage4_EnemyAI_Plan.md`,
  `PlayerMovementConfig.asset`, `CharacterMovementConfig.cs`, `LedgeComponent.cs`, `Sandbox_Locomotion.unity`,
  `Sandbox_PixelBaker.unity`, `Assets/Plugins/**`; неотслеживаемые `GanzSeTest`, `PixelTest`, `Scenes/1.unity`,
  `ThirdParty/Synty`, `ThirdParty/Art/URP GanzSe…`). Перед стартом — свежий `git status`.
- `.meta` создаёт Unity при `AssetDatabase.Refresh()`. `Unity_RunCommand` не умеет удалять — временное не
  создавать. Правка префаба не откатывается через Undo — откат только через git.

## Как запускать тесты (через MCP)

1. Выйти из Play Mode, сохранить открытые сцены (иначе Test Runner спросит и встанет).
2. После правки файлов с диска — `Unity_RunCommand`:

   ```csharp
   using UnityEditor;

   internal class CommandScript : IRunCommand
   {
       public void Execute(ExecutionResult result)
       {
           AssetDatabase.Refresh();
           result.Log("Refreshed");
       }
   }
   ```

   затем `Unity_GetConsoleLogs`: ошибок компиляции быть не должно.
3. `Unity_RunCommand` с `EditorApplication.ExecuteMenuItem("Tools/Game/Run EditMode Tests")` (или
   `Run PlayMode Tests` / `Run All Tests`).
4. Дождаться строки `DONE` в `Temp/GameTestResults.txt` и прочитать файл: сводка `passed N, failed M` и тексты
   падений (`FAIL <полное имя теста>`). Мост отвалился («Unity not detected») — повторить вызов через ~10 с.

Шаблон `Unity_RunCommand` — `internal class CommandScript : IRunCommand` (см. `Assets/AI/CLAUDE.md`).

## Файлы

```
Packages/manifest.json                                      + com.unity.nuget.newtonsoft-json 3.2.2

Assets/Game/Scripts/Modules/SaveSystem/                             (новое)
├── package.json, README.md
├── Runtime/Core/   SaveSystem.asmdef
│   ├── SaveJson.cs, UnityJsonConverters.cs                  — настройки Newtonsoft и конвертеры Unity-структур
│   ├── SaveSection.cs, SaveFile.cs, SaveKeys.cs, SaveFiles.cs — формат файла, зарезервированные ключи, имена файлов
│   ├── IGameSerializer.cs, GameSerializer.cs, SaveMigrationException.cs
│   ├── SectionStatus.cs, SerializerGroup.cs                 — версии, миграции, порядок, изоляция сбоев
│   ├── ISaveStorage.cs, FileSaveStorage.cs, InMemorySaveStorage.cs
│   ├── SaveOutcome.cs, LoadOutcome.cs, SlotMeta.cs, SlotInfo.cs
│   ├── ISaveLoadService.cs, SaveLoadService.cs              — ядро
│   └── SaveLoadSystem.cs                                    — компонент на [Services]
├── Editor/         SaveSystem.Editor.asmdef, SaveSystemMenu.cs
├── Tests/Editor/   SaveSystem.Tests.asmdef, TestDoubles.cs, SaveJsonTests.cs, GameSerializerTests.cs,
│                   SerializerGroupTests.cs, SaveStorageContractTests.cs, SaveLoadServiceTests.cs,
│                   SaveLoadServiceLoadTests.cs, SaveSlotsTests.cs, SaveSystemMenuTests.cs,
│                   Golden/SaveSystemGolden_v1.json
└── Tests/Runtime/  SaveSystem.Tests.Runtime.asmdef, SaveLoadSystemTests.cs

Assets/Game/Game.asmdef                                     + ссылка SaveSystem
Assets/Game/Scripts/GameSystem/Services/GameServices.cs                + поле SaveLoadSystem, регистрация ISaveLoadService
Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs            (новое) F5/F9
Assets/Game/Prefabs/Services.prefab                         + дочерний SaveLoadSystem, + SaveDebugHotkeys на корне
Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef       + ссылка SaveSystem
Assets/Game/Tests/PlayMode/GameServicesTests.cs             хранилище в памяти, тест регистрации
Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs           (новое)
Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs         (новое)
Assets/AI/CLAUDE.md, Assets/AI/Docs/SaveSystem.md           документация
```

---

### Задача 1: ветка, модуль и JSON

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/Game/Scripts/Modules/SaveSystem/package.json`
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveSystem.asmdef`
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveJson.cs`
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/UnityJsonConverters.cs`
- Create: `Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/SaveSystem.Tests.asmdef`
- Test: `Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/SaveJsonTests.cs`

**Interfaces:**
- Produces: `SaveJson.Settings`, `SaveJson.Serializer`, `SaveJson.ToToken<T>(T)`, `SaveJson.FromToken<T>(JToken)`,
  `SaveJson.Write<T>(T) → string`, `SaveJson.TryRead<T>(string, out T) → bool` (T — класс),
  `SaveJson.TryParseObject(string, out JObject) → bool`; `UnityJsonConverters.Create() → IList<JsonConverter>`.

- [x] **Шаг 1: предусловие и ветка.** Спросить пользователя, закончена ли работа над врагами в этой папке (на
  2026-10-23 в рабочей копии были чужие незакоммиченные правки на `feature/enemy-ai`). Работаем в основной рабочей
  копии: Unity и MCP привязаны к `C:\!!!Work\Platformer`, а отдельный worktree потребовал бы второго эдитора с полным
  импортом и своего MCP. Затем:

  ```bash
  git status --short
  git switch -c feature/save-system
  ```

  Чужие правки переезжают на новую ветку незакоммиченными — не трогать. Если пользователь разрешил, первым
  коммитом — документы:

  ```bash
  git add Assets/AI/Docs/Research_SaveSystems.md Assets/AI/Docs/Research_SaveSystems.md.meta Assets/AI/Docs/SaveSystem.md Assets/AI/Docs/SaveSystem.md.meta Assets/AI/Docs/SaveSystem_Plan.md Assets/AI/Docs/SaveSystem_Plan.md.meta
  git commit -m "Add save system research, spec and plan"
  ```

  (`.meta` для `SaveSystem.md` появится после `Refresh` — если его нет, сделать `Refresh` до коммита.)

- [x] **Шаг 2: Newtonsoft явно.** В `Packages/manifest.json` в `dependencies` после строки
  `"com.unity.multiplayer.center": "1.0.1",` добавить:

  ```json
      "com.unity.nuget.newtonsoft-json": "3.2.2",
  ```

  (3.2.2 — версия, которую уже выбрал резолвер, см. `packages-lock.json`.)

- [x] **Шаг 3: сборки и описание модуля.**

  `Assets/Game/Scripts/Modules/SaveSystem/package.json`:

  ```json
  {
    "name": "com.eugenem23.save-system",
    "version": "0.1.0",
    "displayName": "Save System",
    "description": "Save slots with atomic writes and backups, versioned sections with migrations, global systems through GameSerializer<TService, TData>, a SaveLoadSystem component with autosave. Needs Newtonsoft JSON.",
    "unity": "2021.3",
    "dependencies": {
      "com.unity.nuget.newtonsoft-json": "3.2.2"
    },
    "keywords": [
      "save",
      "load",
      "slots",
      "serialization"
    ],
    "author": {
      "name": "EugeneM23"
    }
  }
  ```

  `Assets/Game/Scripts/Modules/SaveSystem/Runtime/Core/SaveSystem.asmdef` (Newtonsoft подключается сам: его DLL в пакете —
  `isExplicitlyReferenced: 0`):

  ```json
  {
      "name": "SaveSystem",
      "rootNamespace": "Saving",
      "references": [],
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

  `Assets/Game/Scripts/Modules/SaveSystem/Tests/Editor/SaveSystem.Tests.asmdef`:

  ```json
  {
      "name": "SaveSystem.Tests",
      "rootNamespace": "Saving.Tests",
      "references": [
          "SaveSystem",
          "UnityEngine.TestRunner",
          "UnityEditor.TestRunner"
      ],
      "includePlatforms": [
          "Editor"
      ],
      "excludePlatforms": [],
      "allowUnsafeCode": false,
      "overrideReferences": true,
      "precompiledReferences": [
          "nunit.framework.dll",
          "Newtonsoft.Json.dll"
      ],
      "autoReferenced": false,
      "defineConstraints": [
          "UNITY_INCLUDE_TESTS"
      ],
      "versionDefines": [],
      "noEngineReferences": false
  }
  ```

- [x] **Шаг 4: заглушки API.**

  `Runtime/Core/SaveJson.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;

  namespace Saving
  {
      public static class SaveJson
      {
          public static readonly JsonSerializerSettings Settings = new();
          public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

          public static JToken ToToken<T>(T value) => throw new NotImplementedException();
          public static T FromToken<T>(JToken token) => throw new NotImplementedException();
          public static string Write<T>(T value) => throw new NotImplementedException();
          public static bool TryRead<T>(string text, out T value) where T : class => throw new NotImplementedException();
          public static bool TryParseObject(string text, out JObject json) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Core/UnityJsonConverters.cs`:

  ```csharp
  using System.Collections.Generic;
  using Newtonsoft.Json;

  namespace Saving
  {
      public static class UnityJsonConverters
      {
          public static IList<JsonConverter> Create() => new List<JsonConverter>();
      }
  }
  ```

- [x] **Шаг 5: падающие тесты** — `Tests/Editor/SaveJsonTests.cs`:

  ```csharp
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>The shared Newtonsoft setup: Unity structs, naming, fresh collections, strings that look like dates.</summary>
      public class SaveJsonTests
      {
          private sealed class AllStructs
          {
              public Vector2 Flat;
              public Vector3 Point;
              public Vector4 Wide;
              public Vector2Int Cell;
              public Vector3Int Voxel;
              public Quaternion Rotation;
              public Color Tint;
              public Color32 Tint32;
              public Rect Area;
              public Bounds Box;
          }

          private sealed class Naming
          {
              public int SomeValue;
              public Dictionary<string, int> Scores = new();
          }

          private sealed class WithList
          {
              public List<int> Items = new() { 1, 2 };
          }

          private sealed class Note
          {
              public string Text;
          }

          [Test]
          public void SaveJson_UnityStructs_RoundTrip()
          {
              var source = new AllStructs
              {
                  Flat = new Vector2(1.5f, -2f),
                  Point = new Vector3(1f, 2f, 3.25f),
                  Wide = new Vector4(1f, 2f, 3f, 4f),
                  Cell = new Vector2Int(3, -4),
                  Voxel = new Vector3Int(5, 6, -7),
                  Rotation = Quaternion.Euler(10f, 20f, 30f),
                  Tint = new Color(0.1f, 0.2f, 0.3f, 0.4f),
                  Tint32 = new Color32(10, 20, 30, 40),
                  Area = new Rect(1f, 2f, 3f, 4f),
                  Box = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f))
              };

              AllStructs copy = SaveJson.FromToken<AllStructs>(SaveJson.ToToken(source));

              Assert.AreEqual(source.Flat, copy.Flat);
              Assert.AreEqual(source.Point, copy.Point);
              Assert.AreEqual(source.Wide, copy.Wide);
              Assert.AreEqual(source.Cell, copy.Cell);
              Assert.AreEqual(source.Voxel, copy.Voxel);
              Assert.AreEqual(source.Rotation, copy.Rotation);
              Assert.AreEqual(source.Tint, copy.Tint);
              Assert.AreEqual(source.Tint32, copy.Tint32);
              Assert.AreEqual(source.Area, copy.Area);
              Assert.AreEqual(source.Box, copy.Box);
          }

          [Test]
          public void SaveJson_Vector3_WrittenAsThreeNumbers()
          {
              var json = (JObject)SaveJson.ToToken(new Vector3(1f, 2f, 3f));

              Assert.AreEqual(3, json.Count);
              Assert.AreEqual(2f, (float)json["y"]);
          }

          [Test]
          public void SaveJson_Members_CamelCaseAndDictionaryKeysAsIs()
          {
              var naming = new Naming { SomeValue = 1 };
              naming.Scores["Player_One"] = 5;

              var json = (JObject)SaveJson.ToToken(naming);

              Assert.IsNotNull(json["someValue"]);
              Assert.AreEqual(5, (int)json["scores"]["Player_One"]);
          }

          [Test]
          public void SaveJson_ListWithInitializer_IsReplacedNotAppended()
          {
              WithList read = SaveJson.FromToken<WithList>(JObject.Parse("{ \"items\": [ 3 ] }"));

              CollectionAssert.AreEqual(new[] { 3 }, read.Items);
          }

          [Test]
          public void SaveJson_DateLikeString_StaysString()
          {
              Assert.IsTrue(SaveJson.TryParseObject("{ \"text\": \"2026-10-23T14:05:00Z\" }", out JObject json));

              Assert.AreEqual(JTokenType.String, json["text"].Type);
              Assert.AreEqual("2026-10-23T14:05:00Z", SaveJson.FromToken<Note>(json).Text);
          }

          [Test]
          public void SaveJson_WriteThenRead_RoundTrips()
          {
              string text = SaveJson.Write(new Note { Text = "hello" });

              Assert.IsTrue(SaveJson.TryRead(text, out Note note));
              Assert.AreEqual("hello", note.Text);
          }

          [Test]
          public void SaveJson_BrokenOrEmptyText_ReadsNothing()
          {
              Assert.IsFalse(SaveJson.TryRead("{ broken", out Note _));
              Assert.IsFalse(SaveJson.TryRead("", out Note _));
              Assert.IsFalse(SaveJson.TryParseObject("[ 1, 2 ]", out JObject _));
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — красный.** `Refresh`, консоль без ошибок компиляции, EditMode-тесты. Ожидание: 7 падений
  `Saving.Tests.SaveJsonTests.*` с `NotImplementedException`; остальные тесты проекта зелёные.

- [x] **Шаг 7: реализация.**

  `Runtime/Core/SaveJson.cs`:

  ```csharp
  using System;
  using System.IO;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using Newtonsoft.Json.Serialization;

  namespace Saving
  {
      /// <summary>
      /// The one Newtonsoft setup every save file uses: camelCase members with dictionary keys kept as they are, fresh
      /// collections on read, no type names in the data, strings that look like dates left as strings, Unity structs
      /// written as plain numbers.
      /// </summary>
      public static class SaveJson
      {
          public static readonly JsonSerializerSettings Settings = CreateSettings();
          public static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

          public static JToken ToToken<T>(T value) =>
              value == null ? JValue.CreateNull() : JToken.FromObject(value, Serializer);

          public static T FromToken<T>(JToken token) =>
              token == null || token.Type == JTokenType.Null ? default : token.ToObject<T>(Serializer);

          public static string Write<T>(T value) => JsonConvert.SerializeObject(value, Formatting.Indented, Settings);

          /// <summary>Reads a whole document; false for broken JSON, a wrong shape or an empty text.</summary>
          public static bool TryRead<T>(string text, out T value) where T : class
          {
              value = null;
              if (string.IsNullOrEmpty(text))
                  return false;

              try
              {
                  value = JsonConvert.DeserializeObject<T>(text, Settings);
              }
              catch (Exception)
              {
                  value = null;
              }

              return value != null;
          }

          /// <summary>Parses a JSON object without turning date-like strings into dates; false for anything else.</summary>
          public static bool TryParseObject(string text, out JObject json)
          {
              json = null;
              if (string.IsNullOrEmpty(text))
                  return false;

              try
              {
                  using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None };
                  json = JObject.Load(reader);
                  return true;
              }
              catch (Exception)
              {
                  json = null;
                  return false;
              }
          }

          private static JsonSerializerSettings CreateSettings() => new()
          {
              ContractResolver = new DefaultContractResolver
              {
                  NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: true)
              },
              ObjectCreationHandling = ObjectCreationHandling.Replace,
              TypeNameHandling = TypeNameHandling.None,
              DateParseHandling = DateParseHandling.None,
              Converters = UnityJsonConverters.Create()
          };
      }
  }
  ```

  `Runtime/Core/UnityJsonConverters.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving
  {
      /// <summary>
      /// Unity structs as plain objects of numbers. Without them Newtonsoft walks properties like Vector3.normalized
      /// and loops forever.
      /// </summary>
      public static class UnityJsonConverters
      {
          public static IList<JsonConverter> Create() => new List<JsonConverter>
          {
              new Vector2Converter(),
              new Vector3Converter(),
              new Vector4Converter(),
              new Vector2IntConverter(),
              new Vector3IntConverter(),
              new QuaternionConverter(),
              new ColorConverter(),
              new Color32Converter(),
              new RectConverter(),
              new BoundsConverter()
          };

          private abstract class StructConverter<T> : JsonConverter<T> where T : struct
          {
              public sealed override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
              {
                  writer.WriteStartObject();
                  Write(writer, value);
                  writer.WriteEndObject();
              }

              public sealed override T ReadJson(JsonReader reader, Type objectType, T existingValue,
                  bool hasExistingValue, JsonSerializer serializer) => Read(JObject.Load(reader));

              protected abstract void Write(JsonWriter writer, T value);
              protected abstract T Read(JObject json);

              protected static void Number(JsonWriter writer, string name, float value)
              {
                  writer.WritePropertyName(name);
                  writer.WriteValue(value);
              }

              protected static void Number(JsonWriter writer, string name, int value)
              {
                  writer.WritePropertyName(name);
                  writer.WriteValue(value);
              }

              protected static float Float(JObject json, string name) => json.Value<float?>(name) ?? 0f;
              protected static int Int(JObject json, string name) => json.Value<int?>(name) ?? 0;
          }

          private sealed class Vector2Converter : StructConverter<Vector2>
          {
              protected override void Write(JsonWriter writer, Vector2 value)
              {
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
              }

              protected override Vector2 Read(JObject json) => new(Float(json, "x"), Float(json, "y"));
          }

          private sealed class Vector3Converter : StructConverter<Vector3>
          {
              protected override void Write(JsonWriter writer, Vector3 value)
              {
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
                  Number(writer, "z", value.z);
              }

              protected override Vector3 Read(JObject json) => new(Float(json, "x"), Float(json, "y"), Float(json, "z"));
          }

          private sealed class Vector4Converter : StructConverter<Vector4>
          {
              protected override void Write(JsonWriter writer, Vector4 value)
              {
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
                  Number(writer, "z", value.z);
                  Number(writer, "w", value.w);
              }

              protected override Vector4 Read(JObject json) =>
                  new(Float(json, "x"), Float(json, "y"), Float(json, "z"), Float(json, "w"));
          }

          private sealed class Vector2IntConverter : StructConverter<Vector2Int>
          {
              protected override void Write(JsonWriter writer, Vector2Int value)
              {
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
              }

              protected override Vector2Int Read(JObject json) => new(Int(json, "x"), Int(json, "y"));
          }

          private sealed class Vector3IntConverter : StructConverter<Vector3Int>
          {
              protected override void Write(JsonWriter writer, Vector3Int value)
              {
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
                  Number(writer, "z", value.z);
              }

              protected override Vector3Int Read(JObject json) => new(Int(json, "x"), Int(json, "y"), Int(json, "z"));
          }

          private sealed class QuaternionConverter : StructConverter<Quaternion>
          {
              protected override void Write(JsonWriter writer, Quaternion value)
              {
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
                  Number(writer, "z", value.z);
                  Number(writer, "w", value.w);
              }

              protected override Quaternion Read(JObject json) =>
                  new(Float(json, "x"), Float(json, "y"), Float(json, "z"), Float(json, "w"));
          }

          private sealed class ColorConverter : StructConverter<Color>
          {
              protected override void Write(JsonWriter writer, Color value)
              {
                  Number(writer, "r", value.r);
                  Number(writer, "g", value.g);
                  Number(writer, "b", value.b);
                  Number(writer, "a", value.a);
              }

              protected override Color Read(JObject json) =>
                  new(Float(json, "r"), Float(json, "g"), Float(json, "b"), Float(json, "a"));
          }

          private sealed class Color32Converter : StructConverter<Color32>
          {
              protected override void Write(JsonWriter writer, Color32 value)
              {
                  Number(writer, "r", value.r);
                  Number(writer, "g", value.g);
                  Number(writer, "b", value.b);
                  Number(writer, "a", value.a);
              }

              protected override Color32 Read(JObject json) =>
                  new((byte)Int(json, "r"), (byte)Int(json, "g"), (byte)Int(json, "b"), (byte)Int(json, "a"));
          }

          private sealed class RectConverter : StructConverter<Rect>
          {
              protected override void Write(JsonWriter writer, Rect value)
              {
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
                  Number(writer, "width", value.width);
                  Number(writer, "height", value.height);
              }

              protected override Rect Read(JObject json) =>
                  new(Float(json, "x"), Float(json, "y"), Float(json, "width"), Float(json, "height"));
          }

          private sealed class BoundsConverter : StructConverter<Bounds>
          {
              protected override void Write(JsonWriter writer, Bounds value)
              {
                  writer.WritePropertyName("center");
                  WriteVector(writer, value.center);
                  writer.WritePropertyName("size");
                  WriteVector(writer, value.size);
              }

              protected override Bounds Read(JObject json) =>
                  new(ReadVector(json["center"] as JObject), ReadVector(json["size"] as JObject));

              private static void WriteVector(JsonWriter writer, Vector3 value)
              {
                  writer.WriteStartObject();
                  Number(writer, "x", value.x);
                  Number(writer, "y", value.y);
                  Number(writer, "z", value.z);
                  writer.WriteEndObject();
              }

              private static Vector3 ReadVector(JObject json) =>
                  json == null ? Vector3.zero : new Vector3(Float(json, "x"), Float(json, "y"), Float(json, "z"));
          }
      }
  }
  ```

- [x] **Шаг 8: запуск — зелёный.** EditMode: `failed 0`, 7 тестов `SaveJsonTests` прошли.

- [ ] **Шаг 9: коммит** (если разрешено):

  ```bash
  git add Packages/manifest.json Packages/packages-lock.json Assets/Game/Scripts/Modules/SaveSystem.meta Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add SaveSystem module with shared JSON settings"
  ```

---

### Задача 2: секции и сериализаторы

**Files:**
- Create: `Runtime/Core/SaveSection.cs`, `SaveFile.cs`, `SaveKeys.cs`, `IGameSerializer.cs`,
  `SaveMigrationException.cs`, `GameSerializer.cs`, `SectionStatus.cs`, `SerializerGroup.cs`
- Test: `Tests/Editor/TestDoubles.cs`, `Tests/Editor/GameSerializerTests.cs`, `Tests/Editor/SerializerGroupTests.cs`

(Пути — от `Assets/Game/Scripts/Modules/SaveSystem/`.)

**Interfaces:**
- Consumes: `SaveJson.ToToken`, `SaveJson.FromToken` (задача 1).
- Produces:
  - `SaveSection { int V; JToken Data; }`;
  - `SaveFile { const int CURRENT_FORMAT_VERSION = 1; int FormatVersion; string GameVersion; Dictionary<string, SaveSection> Sections; }`;
  - `SaveKeys.RESERVED_PREFIX = "$"`, `SaveKeys.IsReserved(string) → bool`;
  - `IGameSerializer { string Key; int Version; int Order; JToken Capture(); JToken Upgrade(JToken, int fromVersion); void Restore(JToken); }`;
  - `GameSerializer<TService, TData>(TService service, string key, int version = 1, int order = 0)` с
    `protected abstract TData Serialize(TService)`, `protected abstract void Deserialize(TService, TData)`,
    `protected virtual JToken Upgrade(JToken, int)`, `protected TService Service`;
  - `SaveMigrationException(string key, int fromVersion)`;
  - `SectionStatus { Ok, NewerVersion, MigrationFailed }`;
  - `SerializerGroup.CaptureInto(IEnumerable<IGameSerializer>, IDictionary<string, SaveSection>)`,
    `SerializerGroup.Prepare(IGameSerializer, SaveSection, out JToken data, out string problem) → SectionStatus`,
    `SerializerGroup.RestoreInOrder(IEnumerable<(IGameSerializer Serializer, JToken Data)>)`.

- [x] **Шаг 1: типы данных и интерфейс** (логики нет — сразу финальные).

  `Runtime/Core/SaveSection.cs`:

  ```csharp
  using Newtonsoft.Json.Linq;

  namespace Saving
  {
      /// <summary>One system's data in a save file plus the version it was written with.</summary>
      public sealed class SaveSection
      {
          public int V;
          public JToken Data;
      }
  }
  ```

  `Runtime/Core/SaveFile.cs`:

  ```csharp
  using System.Collections.Generic;

  namespace Saving
  {
      /// <summary>The save.json envelope: format version, game version and the sections by key.</summary>
      public sealed class SaveFile
      {
          public const int CURRENT_FORMAT_VERSION = 1;

          // No initializer: a file without the field reads as 0, which no build ever wrote
          public int FormatVersion;
          public string GameVersion;
          public Dictionary<string, SaveSection> Sections = new();
      }
  }
  ```

  `Runtime/Core/SaveKeys.cs`:

  ```csharp
  using System;

  namespace Saving
  {
      public static class SaveKeys
      {
          /// <summary>Section keys starting with this belong to the save system itself.</summary>
          public const string RESERVED_PREFIX = "$";

          public static bool IsReserved(string key) =>
              key != null && key.StartsWith(RESERVED_PREFIX, StringComparison.Ordinal);
      }
  }
  ```

  `Runtime/Core/IGameSerializer.cs`:

  ```csharp
  using Newtonsoft.Json.Linq;

  namespace Saving
  {
      /// <summary>Writes and reads one section of a save: one system's data under one key.</summary>
      public interface IGameSerializer
      {
          string Key { get; }                           // section key, unique among registered serializers
          int Version { get; }                          // current data version, starts at 1
          int Order { get; }                            // restore order, lower first
          JToken Capture();
          JToken Upgrade(JToken data, int fromVersion); // one step: fromVersion -> fromVersion + 1
          void Restore(JToken data);                    // data is already at Version
      }
  }
  ```

  `Runtime/Core/SaveMigrationException.cs`:

  ```csharp
  using System;

  namespace Saving
  {
      /// <summary>A section is older than its serializer and the serializer has no step to upgrade it.</summary>
      public sealed class SaveMigrationException : Exception
      {
          public SaveMigrationException(string key, int fromVersion)
              : base($"'{key}' has no migration from version {fromVersion}.")
          {
              Key = key;
              FromVersion = fromVersion;
          }

          public string Key { get; }
          public int FromVersion { get; }
      }
  }
  ```

  `Runtime/Core/SectionStatus.cs`:

  ```csharp
  namespace Saving
  {
      public enum SectionStatus
      {
          Ok,
          NewerVersion,
          MigrationFailed
      }
  }
  ```

- [x] **Шаг 2: заглушки.**

  `Runtime/Core/GameSerializer.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;

  namespace Saving
  {
      public abstract class GameSerializer<TService, TData> : IGameSerializer
      {
          protected GameSerializer(TService service, string key, int version = 1, int order = 0) =>
              throw new NotImplementedException();

          public string Key { get; }
          public int Version { get; }
          public int Order { get; }
          protected TService Service { get; }

          JToken IGameSerializer.Capture() => throw new NotImplementedException();
          void IGameSerializer.Restore(JToken data) => throw new NotImplementedException();
          JToken IGameSerializer.Upgrade(JToken data, int fromVersion) => Upgrade(data, fromVersion);

          protected abstract TData Serialize(TService service);
          protected abstract void Deserialize(TService service, TData data);
          protected virtual JToken Upgrade(JToken data, int fromVersion) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Core/SerializerGroup.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;

  namespace Saving
  {
      public static class SerializerGroup
      {
          public static void CaptureInto(IEnumerable<IGameSerializer> serializers,
              IDictionary<string, SaveSection> sections) => throw new NotImplementedException();

          public static SectionStatus Prepare(IGameSerializer serializer, SaveSection section, out JToken data,
              out string problem) => throw new NotImplementedException();

          public static void RestoreInOrder(IEnumerable<(IGameSerializer Serializer, JToken Data)> prepared) =>
              throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 3: тестовые двойники** — `Tests/Editor/TestDoubles.cs` (их используют и задачи 4–6):

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>A game system to save: level, name and a position.</summary>
      internal sealed class Progress
      {
          public int Level;
          public string Name;
          public Vector3 Position;
      }

      internal sealed class ProgressData
      {
          public int Level;
          public string Name;
          public Vector3 Position;
      }

      /// <summary>Version 2 of the progress section: version 1 stored the level as "lvl".</summary>
      internal sealed class ProgressSerializer : GameSerializer<Progress, ProgressData>
      {
          public const string KEY = "progress";

          public ProgressSerializer(Progress progress, int order = 0) : base(progress, KEY, version: 2, order: order)
          {
          }

          protected override ProgressData Serialize(Progress progress) =>
              new() { Level = progress.Level, Name = progress.Name, Position = progress.Position };

          protected override void Deserialize(Progress progress, ProgressData data)
          {
              progress.Level = data.Level;
              progress.Name = data.Name;
              progress.Position = data.Position;
          }

          protected override JToken Upgrade(JToken data, int fromVersion)
          {
              if (fromVersion != 1)
                  return base.Upgrade(data, fromVersion);

              var json = (JObject)data;
              json["level"] = json["lvl"];
              json.Remove("lvl");
              return json;
          }
      }

      internal sealed class LegacyProgressData
      {
          public int Lvl;
          public string Name;
          public Vector3 Position;
      }

      /// <summary>The same section the way an older build wrote it: version 1 with "lvl".</summary>
      internal sealed class LegacyProgressSerializer : GameSerializer<Progress, LegacyProgressData>
      {
          public LegacyProgressSerializer(Progress progress) : base(progress, ProgressSerializer.KEY, version: 1)
          {
          }

          protected override LegacyProgressData Serialize(Progress progress) =>
              new() { Lvl = progress.Level, Name = progress.Name, Position = progress.Position };

          protected override void Deserialize(Progress progress, LegacyProgressData data)
          {
              progress.Level = data.Lvl;
              progress.Name = data.Name;
              progress.Position = data.Position;
          }
      }

      /// <summary>One int under a key; can be told to throw on save or on load and reports its restores.</summary>
      internal sealed class FlakySerializer : IGameSerializer
      {
          public int Value;
          public bool FailCapture;
          public bool FailRestore;
          public Action OnRestore;

          public FlakySerializer(string key, int order = 0)
          {
              Key = key;
              Order = order;
          }

          public string Key { get; }
          public int Version => 1;
          public int Order { get; }

          public JToken Capture() => FailCapture ? throw new InvalidOperationException("capture broke") : new JValue(Value);

          public JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(Key, fromVersion);

          public void Restore(JToken data)
          {
              if (FailRestore)
                  throw new InvalidOperationException("restore broke");

              Value = (int)data;
              OnRestore?.Invoke();
          }
      }

      /// <summary>Version 3: records every migration step and marks the data with it.</summary>
      internal sealed class StepSerializer : IGameSerializer
      {
          public readonly List<int> Steps = new();
          public JToken Restored;
          public bool FailUpgrade;

          public StepSerializer(string key) => Key = key;

          public string Key { get; }
          public int Version => 3;
          public int Order => 0;

          public JToken Capture() => new JObject();

          public JToken Upgrade(JToken data, int fromVersion)
          {
              if (FailUpgrade)
                  throw new InvalidOperationException("upgrade broke");

              Steps.Add(fromVersion);
              var json = (JObject)data;
              json[$"step{fromVersion}"] = true;
              return json;
          }

          public void Restore(JToken data) => Restored = data;
      }
  }
  ```

- [x] **Шаг 4: падающие тесты.**

  `Tests/Editor/GameSerializerTests.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;

  namespace Saving.Tests
  {
      /// <summary>The typed base from the homework: service in the constructor, key rules, JSON round trip.</summary>
      public class GameSerializerTests
      {
          private sealed class Box
          {
              public int Value;
          }

          private sealed class BoxData
          {
              public int Value;
          }

          private sealed class BoxSerializer : GameSerializer<Box, BoxData>
          {
              public BoxSerializer(Box box, string key = "box", int version = 1) : base(box, key, version)
              {
              }

              protected override BoxData Serialize(Box box) => new() { Value = box.Value };
              protected override void Deserialize(Box box, BoxData data) => box.Value = data.Value;
          }

          [Test]
          public void GameSerializer_EmptyKey_Throws() =>
              Assert.Throws<ArgumentException>(() => new BoxSerializer(new Box(), key: ""));

          [Test]
          public void GameSerializer_ReservedKey_Throws() =>
              Assert.Throws<ArgumentException>(() => new BoxSerializer(new Box(), key: "$scenes"));

          [Test]
          public void GameSerializer_VersionBelowOne_Throws() =>
              Assert.Throws<ArgumentOutOfRangeException>(() => new BoxSerializer(new Box(), version: 0));

          [Test]
          public void GameSerializer_NullService_Throws() =>
              Assert.Throws<ArgumentNullException>(() => new BoxSerializer(null));

          [Test]
          public void GameSerializer_CaptureThenRestore_RoundTrips()
          {
              IGameSerializer writer = new BoxSerializer(new Box { Value = 5 });
              JToken data = writer.Capture();

              var box = new Box();
              IGameSerializer reader = new BoxSerializer(box);
              reader.Restore(data);

              Assert.AreEqual(5, (int)data["value"]);
              Assert.AreEqual(5, box.Value);
          }

          [Test]
          public void GameSerializer_NoUpgrade_ThrowsMigrationException()
          {
              IGameSerializer serializer = new BoxSerializer(new Box(), version: 2);

              var exception = Assert.Throws<SaveMigrationException>(() => serializer.Upgrade(new JObject(), 1));
              Assert.AreEqual(1, exception.FromVersion);
          }
      }
  }
  ```

  `Tests/Editor/SerializerGroupTests.cs`:

  ```csharp
  using System.Collections.Generic;
  using System.Text.RegularExpressions;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>The shared loop: capture with versions, check and migrate on a copy, restore by order.</summary>
      public class SerializerGroupTests
      {
          [Test]
          public void SerializerGroup_Capture_WritesSectionWithVersion()
          {
              var sections = new Dictionary<string, SaveSection>();

              SerializerGroup.CaptureInto(new IGameSerializer[] { new FlakySerializer("value") { Value = 5 } }, sections);

              Assert.AreEqual(1, sections["value"].V);
              Assert.AreEqual(5, (int)sections["value"].Data);
          }

          [Test]
          public void SerializerGroup_CaptureThrows_KeepsPreviousSection()
          {
              var previous = new SaveSection { V = 1, Data = new JValue(1) };
              var sections = new Dictionary<string, SaveSection> { ["value"] = previous };
              var flaky = new FlakySerializer("value") { Value = 2, FailCapture = true };

              LogAssert.Expect(LogType.Error, new Regex("Saving 'value' failed"));
              SerializerGroup.CaptureInto(new IGameSerializer[] { flaky }, sections);

              Assert.AreSame(previous, sections["value"]);
          }

          [Test]
          public void SerializerGroup_PrepareSameVersion_ReturnsCopy()
          {
              var section = new SaveSection { V = 1, Data = new JObject { ["a"] = 1 } };

              SectionStatus status = SerializerGroup.Prepare(new FlakySerializer("value"), section, out JToken data, out _);

              Assert.AreEqual(SectionStatus.Ok, status);
              Assert.IsTrue(JToken.DeepEquals(section.Data, data));
              Assert.AreNotSame(section.Data, data);
          }

          [Test]
          public void SerializerGroup_PrepareNewerSection_Refused()
          {
              var section = new SaveSection { V = 2, Data = new JValue(1) };

              SectionStatus status = SerializerGroup.Prepare(new FlakySerializer("value"), section, out _, out string problem);

              Assert.AreEqual(SectionStatus.NewerVersion, status);
              StringAssert.Contains("newer", problem);
          }

          [Test]
          public void SerializerGroup_PrepareOlderSection_UpgradesStepByStepOnCopy()
          {
              var steps = new StepSerializer("steps");
              var original = new JObject();

              SectionStatus status = SerializerGroup.Prepare(steps, new SaveSection { V = 1, Data = original },
                  out JToken data, out _);

              Assert.AreEqual(SectionStatus.Ok, status);
              CollectionAssert.AreEqual(new[] { 1, 2 }, steps.Steps);
              Assert.IsTrue((bool)data["step1"] && (bool)data["step2"]);
              Assert.AreEqual(0, original.Count, "the section in the snapshot must stay untouched");
          }

          [Test]
          public void SerializerGroup_UpgradeThrows_MigrationFailed()
          {
              var steps = new StepSerializer("steps") { FailUpgrade = true };

              SectionStatus status = SerializerGroup.Prepare(steps, new SaveSection { V = 1, Data = new JObject() },
                  out _, out string problem);

              Assert.AreEqual(SectionStatus.MigrationFailed, status);
              StringAssert.Contains("Migrating 'steps' from version 1 failed", problem);
          }

          [Test]
          public void SerializerGroup_VersionBelowOne_MigrationFailed()
          {
              SectionStatus status = SerializerGroup.Prepare(new FlakySerializer("value"),
                  new SaveSection { V = 0, Data = new JValue(1) }, out _, out _);

              Assert.AreEqual(SectionStatus.MigrationFailed, status);
          }

          [Test]
          public void SerializerGroup_Restore_ByOrderAndSkipsThrowing()
          {
              var restored = new List<string>();
              var late = new FlakySerializer("late", order: 5) { OnRestore = () => restored.Add("late") };
              var broken = new FlakySerializer("broken") { FailRestore = true };
              var early = new FlakySerializer("early", order: -1) { OnRestore = () => restored.Add("early") };

              LogAssert.Expect(LogType.Error, new Regex("Loading 'broken' failed"));
              SerializerGroup.RestoreInOrder(new (IGameSerializer, JToken)[]
              {
                  (late, new JValue(1)), (broken, new JValue(2)), (early, new JValue(3))
              });

              CollectionAssert.AreEqual(new[] { "early", "late" }, restored);
              Assert.AreEqual(3, early.Value);
          }
      }
  }
  ```

- [x] **Шаг 5: запуск — красный.** Падают все тесты `GameSerializerTests` и `SerializerGroupTests` с
  `NotImplementedException` (у `GameSerializer_*` — из конструктора).

- [x] **Шаг 6: реализация.**

  `Runtime/Core/GameSerializer.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;

  namespace Saving
  {
      /// <summary>
      /// Typed serializer of one global system: the subclass turns the service into a data object and back, the base
      /// writes it as JSON under the key. Bump the version when the data changes shape and override Upgrade with one
      /// step per version.
      /// </summary>
      public abstract class GameSerializer<TService, TData> : IGameSerializer
      {
          protected GameSerializer(TService service, string key, int version = 1, int order = 0)
          {
              if (service == null)
                  throw new ArgumentNullException(nameof(service));

              if (string.IsNullOrEmpty(key))
                  throw new ArgumentException("The key must not be empty.", nameof(key));

              if (SaveKeys.IsReserved(key))
                  throw new ArgumentException(
                      $"Keys starting with '{SaveKeys.RESERVED_PREFIX}' are reserved for the save system: '{key}'.",
                      nameof(key));

              if (version < 1)
                  throw new ArgumentOutOfRangeException(nameof(version), version, "The version starts at 1.");

              Service = service;
              Key = key;
              Version = version;
              Order = order;
          }

          public string Key { get; }
          public int Version { get; }
          public int Order { get; }
          protected TService Service { get; }

          JToken IGameSerializer.Capture() => SaveJson.ToToken(Serialize(Service));
          void IGameSerializer.Restore(JToken data) => Deserialize(Service, SaveJson.FromToken<TData>(data));
          JToken IGameSerializer.Upgrade(JToken data, int fromVersion) => Upgrade(data, fromVersion);

          protected abstract TData Serialize(TService service);
          protected abstract void Deserialize(TService service, TData data);

          /// <summary>One migration step: data written by version <paramref name="fromVersion"/> to the next one.</summary>
          protected virtual JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(Key, fromVersion);
      }
  }
  ```

  `Runtime/Core/SerializerGroup.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving
  {
      /// <summary>
      /// The loop shared by global sections and (later) component sections: capture with versions, check and migrate
      /// on a copy, restore in ascending Order. One failing serializer is logged and does not stop the others.
      /// </summary>
      public static class SerializerGroup
      {
          public static void CaptureInto(IEnumerable<IGameSerializer> serializers, IDictionary<string, SaveSection> sections)
          {
              foreach (IGameSerializer serializer in serializers)
              {
                  try
                  {
                      sections[serializer.Key] = new SaveSection { V = serializer.Version, Data = serializer.Capture() };
                  }
                  catch (Exception exception)
                  {
                      Debug.LogError($"Saving '{serializer.Key}' failed, its previous data is kept: {exception.Message}");
                  }
              }
          }

          /// <summary>Checks the section's version and migrates a copy step by step; applies nothing.</summary>
          public static SectionStatus Prepare(IGameSerializer serializer, SaveSection section, out JToken data,
              out string problem)
          {
              data = null;
              problem = null;

              if (section.V > serializer.Version)
              {
                  problem = $"'{serializer.Key}' was saved with version {section.V}, newer than {serializer.Version}";
                  return SectionStatus.NewerVersion;
              }

              if (section.V < 1)
              {
                  problem = $"'{serializer.Key}' has an invalid version {section.V}";
                  return SectionStatus.MigrationFailed;
              }

              JToken current = section.Data?.DeepClone();
              for (int version = section.V; version < serializer.Version; version++)
              {
                  try
                  {
                      current = serializer.Upgrade(current, version);
                  }
                  catch (Exception exception)
                  {
                      problem = $"Migrating '{serializer.Key}' from version {version} failed: {exception.Message}";
                      return SectionStatus.MigrationFailed;
                  }
              }

              data = current;
              return SectionStatus.Ok;
          }

          public static void RestoreInOrder(IEnumerable<(IGameSerializer Serializer, JToken Data)> prepared)
          {
              // OrderBy is stable: equal orders keep registration order
              foreach ((IGameSerializer serializer, JToken data) in prepared.OrderBy(item => item.Serializer.Order).ToList())
              {
                  try
                  {
                      serializer.Restore(data);
                  }
                  catch (Exception exception)
                  {
                      Debug.LogError($"Loading '{serializer.Key}' failed, it is skipped: {exception.Message}");
                  }
              }
          }
      }
  }
  ```

- [x] **Шаг 7: запуск — зелёный.** EditMode: `failed 0`.

- [ ] **Шаг 8: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add save sections and game serializers"
  ```

---

### Задача 3: хранилище

**Files:**
- Create: `Runtime/Core/SaveFiles.cs`, `ISaveStorage.cs`, `InMemorySaveStorage.cs`, `FileSaveStorage.cs`
- Test: `Tests/Editor/SaveStorageContractTests.cs`

**Interfaces:**
- Produces:
  - `SaveFiles.SAVE = "save.json"`, `META = "meta.json"`, `PREMIGRATION = "save.premigration.json"`,
    `BACKUP_SUFFIX = ".bak"`, `CORRUPT_SUFFIX = ".corrupt"`;
  - `ISaveStorage { IReadOnlyList<int> ListSlots(); bool TryRead(int slot, string file, out string text);
    bool TryReadBackup(int slot, string file, out string text); void Write(int slot, string file, string text);
    void Quarantine(int slot, string file, bool backup); void DeleteSlot(int slot); }`;
  - `InMemorySaveStorage` (+ `Put(int, string, string)`, `Has(int, string) → bool`), `FileSaveStorage(string root)` (+ `Root`).

- [x] **Шаг 1: имена файлов и интерфейс** (сразу финальные).

  `Runtime/Core/SaveFiles.cs`:

  ```csharp
  namespace Saving
  {
      /// <summary>File names inside a slot folder.</summary>
      public static class SaveFiles
      {
          public const string SAVE = "save.json";
          public const string META = "meta.json";
          public const string PREMIGRATION = "save.premigration.json";
          public const string BACKUP_SUFFIX = ".bak";
          public const string CORRUPT_SUFFIX = ".corrupt";
      }
  }
  ```

  `Runtime/Core/ISaveStorage.cs`:

  ```csharp
  using System.Collections.Generic;

  namespace Saving
  {
      /// <summary>
      /// Where slots live. The boundary is a file's text, so the disk, a platform's save API or memory plug in the
      /// same way. Write is atomic and keeps the previous version as &lt;file&gt;.bak.
      /// </summary>
      public interface ISaveStorage
      {
          IReadOnlyList<int> ListSlots();                          // slots that have any file, ascending
          bool TryRead(int slot, string file, out string text);    // false when the file is missing
          bool TryReadBackup(int slot, string file, out string text);
          void Write(int slot, string file, string text);
          void Quarantine(int slot, string file, bool backup);     // <file> or <file>.bak -> *.corrupt
          void DeleteSlot(int slot);
      }
  }
  ```

- [x] **Шаг 2: заглушки.**

  `Runtime/Core/InMemorySaveStorage.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving
  {
      public sealed class InMemorySaveStorage : ISaveStorage
      {
          public IReadOnlyList<int> ListSlots() => throw new NotImplementedException();
          public bool TryRead(int slot, string file, out string text) => throw new NotImplementedException();
          public bool TryReadBackup(int slot, string file, out string text) => throw new NotImplementedException();
          public void Write(int slot, string file, string text) => throw new NotImplementedException();
          public void Quarantine(int slot, string file, bool backup) => throw new NotImplementedException();
          public void DeleteSlot(int slot) => throw new NotImplementedException();
          public void Put(int slot, string file, string text) => throw new NotImplementedException();
          public bool Has(int slot, string file) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Core/FileSaveStorage.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving
  {
      public sealed class FileSaveStorage : ISaveStorage
      {
          public FileSaveStorage(string root) => Root = root;

          public string Root { get; }

          public IReadOnlyList<int> ListSlots() => throw new NotImplementedException();
          public bool TryRead(int slot, string file, out string text) => throw new NotImplementedException();
          public bool TryReadBackup(int slot, string file, out string text) => throw new NotImplementedException();
          public void Write(int slot, string file, string text) => throw new NotImplementedException();
          public void Quarantine(int slot, string file, bool backup) => throw new NotImplementedException();
          public void DeleteSlot(int slot) => throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 3: падающие тесты** — `Tests/Editor/SaveStorageContractTests.cs` (один контракт, две реализации):

  ```csharp
  using System;
  using System.IO;
  using NUnit.Framework;

  namespace Saving.Tests
  {
      /// <summary>What every storage promises: atomic writes with a backup, quarantine, slot listing and deletion.</summary>
      public abstract class SaveStorageContractTests
      {
          protected ISaveStorage Storage { get; private set; }

          protected abstract ISaveStorage CreateStorage();
          protected abstract void Plant(int slot, string file, string text);
          protected abstract bool Exists(int slot, string file);

          [SetUp]
          public void SetUpStorage() => Storage = CreateStorage();

          [Test]
          public void Storage_MissingFile_ReadsNothing() => Assert.IsFalse(Storage.TryRead(0, SaveFiles.SAVE, out _));

          [Test]
          public void Storage_FirstWrite_ReadsBackWithoutBackup()
          {
              Storage.Write(0, SaveFiles.SAVE, "first");

              Assert.IsTrue(Storage.TryRead(0, SaveFiles.SAVE, out string text));
              Assert.AreEqual("first", text);
              Assert.IsFalse(Storage.TryReadBackup(0, SaveFiles.SAVE, out _));
          }

          [Test]
          public void Storage_SecondWrite_PreviousBecomesBackup()
          {
              Storage.Write(0, SaveFiles.SAVE, "first");
              Storage.Write(0, SaveFiles.SAVE, "second");

              Storage.TryRead(0, SaveFiles.SAVE, out string text);
              Assert.IsTrue(Storage.TryReadBackup(0, SaveFiles.SAVE, out string backup));
              Assert.AreEqual("second", text);
              Assert.AreEqual("first", backup);
          }

          [Test]
          public void Storage_QuarantineMain_RenamesToCorrupt()
          {
              Plant(0, SaveFiles.SAVE, "broken");

              Storage.Quarantine(0, SaveFiles.SAVE, backup: false);

              Assert.IsFalse(Storage.TryRead(0, SaveFiles.SAVE, out _));
              Assert.IsTrue(Exists(0, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX));
          }

          [Test]
          public void Storage_QuarantineBackup_RenamesToCorrupt()
          {
              Storage.Write(0, SaveFiles.SAVE, "first");
              Storage.Write(0, SaveFiles.SAVE, "second");

              Storage.Quarantine(0, SaveFiles.SAVE, backup: true);

              Assert.IsFalse(Storage.TryReadBackup(0, SaveFiles.SAVE, out _));
              Assert.IsTrue(Exists(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX + SaveFiles.CORRUPT_SUFFIX));
              Assert.IsTrue(Storage.TryRead(0, SaveFiles.SAVE, out _), "the main file stays");
          }

          [Test]
          public void Storage_ListSlots_SlotsWithFilesAscending()
          {
              Storage.Write(3, SaveFiles.SAVE, "three");
              Storage.Write(1, SaveFiles.SAVE, "one");

              CollectionAssert.AreEqual(new[] { 1, 3 }, Storage.ListSlots());
          }

          [Test]
          public void Storage_DeleteSlot_RemovesItsFilesOnly()
          {
              Storage.Write(1, SaveFiles.SAVE, "one");
              Storage.Write(2, SaveFiles.SAVE, "two");

              Storage.DeleteSlot(1);

              Assert.IsFalse(Storage.TryRead(1, SaveFiles.SAVE, out _));
              CollectionAssert.AreEqual(new[] { 2 }, Storage.ListSlots());
          }
      }

      public class InMemorySaveStorageTests : SaveStorageContractTests
      {
          private InMemorySaveStorage _memory;

          protected override ISaveStorage CreateStorage() => _memory = new InMemorySaveStorage();
          protected override void Plant(int slot, string file, string text) => _memory.Put(slot, file, text);
          protected override bool Exists(int slot, string file) => _memory.Has(slot, file);
      }

      public class FileSaveStorageTests : SaveStorageContractTests
      {
          private string _root;

          protected override ISaveStorage CreateStorage()
          {
              _root = Path.Combine(Path.GetTempPath(), "SaveSystemTests_" + Guid.NewGuid().ToString("N"));
              return new FileSaveStorage(_root);
          }

          protected override void Plant(int slot, string file, string text)
          {
              string path = PathOf(slot, file);
              Directory.CreateDirectory(Path.GetDirectoryName(path));
              File.WriteAllText(path, text);
          }

          protected override bool Exists(int slot, string file) => File.Exists(PathOf(slot, file));

          protected string PathOf(int slot, string file) => Path.Combine(_root, $"slot_{slot}", file);

          [TearDown]
          public void DeleteRoot()
          {
              if (Directory.Exists(_root))
                  Directory.Delete(_root, recursive: true);
          }

          [Test]
          public void FileStorage_WriteFails_PreviousFileIntact()
          {
              Storage.Write(0, SaveFiles.SAVE, "first");

              // A locked temp file makes the next write fail before it touches the save
              using (new FileStream(PathOf(0, SaveFiles.SAVE) + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
              {
                  Assert.Throws<IOException>(() => Storage.Write(0, SaveFiles.SAVE, "second"));
              }

              Storage.TryRead(0, SaveFiles.SAVE, out string text);
              Assert.AreEqual("first", text);
          }

          [Test]
          public void FileStorage_DeleteSlot_RemovesFolder()
          {
              Storage.Write(4, SaveFiles.SAVE, "four");

              Storage.DeleteSlot(4);

              Assert.IsFalse(Directory.Exists(Path.Combine(_root, "slot_4")));
          }
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** Падают 7 тестов контракта в обеих реализациях и 2 теста `FileStorage_*` с
  `NotImplementedException`.

- [x] **Шаг 5: реализация.**

  `Runtime/Core/InMemorySaveStorage.cs`:

  ```csharp
  using System.Collections.Generic;
  using System.Linq;

  namespace Saving
  {
      /// <summary>
      /// Slots kept in memory with the same backup rules as the file storage: for tests and for platforms that hand the
      /// text to their own storage API. <see cref="Put"/> and <see cref="Has"/> let tests plant and inspect files.
      /// </summary>
      public sealed class InMemorySaveStorage : ISaveStorage
      {
          private readonly Dictionary<int, Dictionary<string, string>> _slots = new();

          public IReadOnlyList<int> ListSlots() =>
              _slots.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key).OrderBy(slot => slot).ToList();

          public bool TryRead(int slot, string file, out string text) => TryGet(slot, file, out text);

          public bool TryReadBackup(int slot, string file, out string text) =>
              TryGet(slot, file + SaveFiles.BACKUP_SUFFIX, out text);

          public void Write(int slot, string file, string text)
          {
              Dictionary<string, string> files = FilesOf(slot);
              if (files.TryGetValue(file, out string previous))
                  files[file + SaveFiles.BACKUP_SUFFIX] = previous;

              files[file] = text;
          }

          public void Quarantine(int slot, string file, bool backup)
          {
              string name = backup ? file + SaveFiles.BACKUP_SUFFIX : file;
              if (!_slots.TryGetValue(slot, out Dictionary<string, string> files) || !files.TryGetValue(name, out string text))
                  return;

              files.Remove(name);
              files[name + SaveFiles.CORRUPT_SUFFIX] = text;
          }

          public void DeleteSlot(int slot) => _slots.Remove(slot);

          /// <summary>Writes a file as is, without a backup: for tests that plant broken or foreign files.</summary>
          public void Put(int slot, string file, string text) => FilesOf(slot)[file] = text;

          public bool Has(int slot, string file) =>
              _slots.TryGetValue(slot, out Dictionary<string, string> files) && files.ContainsKey(file);

          private bool TryGet(int slot, string file, out string text)
          {
              text = null;
              return _slots.TryGetValue(slot, out Dictionary<string, string> files) && files.TryGetValue(file, out text);
          }

          private Dictionary<string, string> FilesOf(int slot)
          {
              if (!_slots.TryGetValue(slot, out Dictionary<string, string> files))
              {
                  files = new Dictionary<string, string>();
                  _slots.Add(slot, files);
              }

              return files;
          }
      }
  }
  ```

  `Runtime/Core/FileSaveStorage.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Globalization;
  using System.IO;
  using System.Text;

  namespace Saving
  {
      /// <summary>
      /// Slots as folders root/slot_N. A write goes to &lt;file&gt;.tmp in the same folder, is flushed to disk and then
      /// replaces the file, which becomes &lt;file&gt;.bak: a crash mid-write leaves the old save whole.
      /// </summary>
      public sealed class FileSaveStorage : ISaveStorage
      {
          private const string SLOT_PREFIX = "slot_";
          private const string TEMP_SUFFIX = ".tmp";

          private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

          public FileSaveStorage(string root)
          {
              if (string.IsNullOrEmpty(root))
                  throw new ArgumentException("The root folder must not be empty.", nameof(root));

              Root = root;
          }

          public string Root { get; }

          public IReadOnlyList<int> ListSlots()
          {
              var slots = new List<int>();
              if (!Directory.Exists(Root))
                  return slots;

              foreach (string directory in Directory.GetDirectories(Root, SLOT_PREFIX + "*"))
              {
                  string number = Path.GetFileName(directory).Substring(SLOT_PREFIX.Length);
                  if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int slot)
                      && Directory.GetFiles(directory).Length > 0)
                      slots.Add(slot);
              }

              slots.Sort();
              return slots;
          }

          public bool TryRead(int slot, string file, out string text) => TryReadPath(PathOf(slot, file), out text);

          public bool TryReadBackup(int slot, string file, out string text) =>
              TryReadPath(PathOf(slot, file) + SaveFiles.BACKUP_SUFFIX, out text);

          public void Write(int slot, string file, string text)
          {
              string path = PathOf(slot, file);
              Directory.CreateDirectory(Path.GetDirectoryName(path));

              string temp = path + TEMP_SUFFIX;
              byte[] bytes = Utf8.GetBytes(text);
              using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
              {
                  stream.Write(bytes, 0, bytes.Length);
                  stream.Flush(flushToDisk: true);
              }

              // File.Replace needs an existing target, so the very first write is a move
              if (File.Exists(path))
                  File.Replace(temp, path, path + SaveFiles.BACKUP_SUFFIX);
              else
                  File.Move(temp, path);
          }

          public void Quarantine(int slot, string file, bool backup)
          {
              string path = PathOf(slot, file) + (backup ? SaveFiles.BACKUP_SUFFIX : string.Empty);
              if (!File.Exists(path))
                  return;

              string corrupt = path + SaveFiles.CORRUPT_SUFFIX;
              if (File.Exists(corrupt))
                  File.Delete(corrupt);

              File.Move(path, corrupt);
          }

          public void DeleteSlot(int slot)
          {
              string directory = SlotDirectory(slot);
              if (Directory.Exists(directory))
                  Directory.Delete(directory, recursive: true);
          }

          private string SlotDirectory(int slot)
          {
              if (slot < 0)
                  throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots start at 0.");

              return Path.Combine(Root, SLOT_PREFIX + slot.ToString(CultureInfo.InvariantCulture));
          }

          private string PathOf(int slot, string file)
          {
              if (string.IsNullOrEmpty(file))
                  throw new ArgumentException("The file name must not be empty.", nameof(file));

              return Path.Combine(SlotDirectory(slot), file);
          }

          private static bool TryReadPath(string path, out string text)
          {
              if (!File.Exists(path))
              {
                  text = null;
                  return false;
              }

              text = File.ReadAllText(path, Utf8);
              return true;
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — зелёный.** EditMode: `failed 0`. Если `FileStorage_SecondWrite…` падает на `File.Replace`
  (Mono) — остановиться и сообщить пользователю: это риск из спека (раздел 14), обходить молча нельзя.

- [ ] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add save storages with atomic writes and backups"
  ```

---

### Задача 4: `SaveLoadService` — сохранение, регистрация, снимок

**Files:**
- Create: `Runtime/Core/SaveOutcome.cs`, `LoadOutcome.cs`, `SaveLoadService.cs`
- Test: `Tests/Editor/SaveLoadServiceTests.cs`

**Interfaces:**
- Consumes: задачи 1–3.
- Produces: `SaveOutcome { Saved, Unchanged, Blocked, Failed }`;
  `LoadOutcome { Loaded, Recovered, Empty, Corrupted, NewerVersion, MigrationFailed, Failed }`;
  `SaveLoadService(ISaveStorage storage, string gameVersion)` с `CurrentSlot`, `IsLoading`, событиями
  `Saved`/`Loaded`, `Register`, `Unregister`, `Capture`, `Save(bool onlyIfChanged = false)`, `Load()`,
  `SelectSlot(int slot)`.

- [x] **Шаг 1: перечисления** (финальные).

  `Runtime/Core/SaveOutcome.cs`:

  ```csharp
  namespace Saving
  {
      public enum SaveOutcome
      {
          Saved,
          Unchanged,  // onlyIfChanged and nothing changed: nothing written
          Blocked,    // a load is running or the slot is read-only
          Failed      // the storage threw: the file on disk is the previous one
      }
  }
  ```

  `Runtime/Core/LoadOutcome.cs`:

  ```csharp
  namespace Saving
  {
      public enum LoadOutcome
      {
          Loaded,
          Recovered,        // save.json was broken or missing, its .bak was loaded
          Empty,            // no save: a new game
          Corrupted,        // both files broken: renamed to *.corrupt, a new game
          NewerVersion,     // written by a newer game: nothing applied, slot read-only
          MigrationFailed,  // a migration threw: nothing applied, slot read-only
          Failed            // the storage threw: nothing applied, slot read-only
      }
  }
  ```

- [x] **Шаг 2: заглушка** `Runtime/Core/SaveLoadService.cs`:

  ```csharp
  using System;

  namespace Saving
  {
      public sealed class SaveLoadService
      {
          public SaveLoadService(ISaveStorage storage, string gameVersion)
          {
          }

          public int CurrentSlot { get; private set; }
          public bool IsLoading { get; private set; }

          public event Action<SaveOutcome> Saved;
          public event Action<LoadOutcome> Loaded;

          public void Register(IGameSerializer serializer) => throw new NotImplementedException();
          public void Unregister(IGameSerializer serializer) => throw new NotImplementedException();
          public void Capture() => throw new NotImplementedException();
          public SaveOutcome Save(bool onlyIfChanged = false) => throw new NotImplementedException();
          public LoadOutcome Load() => throw new NotImplementedException();
          public void SelectSlot(int slot) => throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 3: падающие тесты** — `Tests/Editor/SaveLoadServiceTests.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Text.RegularExpressions;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>Saving, registering and capturing: the snapshot keeps what is not registered right now.</summary>
      public class SaveLoadServiceTests
      {
          private InMemorySaveStorage _storage;

          [SetUp]
          public void SetUp() => _storage = new InMemorySaveStorage();

          private SaveLoadService CreateService() => new(_storage, "1.0");

          private static Progress SampleProgress() =>
              new() { Level = 3, Name = "Cave", Position = new Vector3(1f, 2f, 3f) };

          [Test]
          public void SaveLoadService_SaveThenLoad_RestoresRegisteredData()
          {
              SaveLoadService writer = CreateService();
              writer.Register(new ProgressSerializer(SampleProgress()));
              Assert.AreEqual(SaveOutcome.Saved, writer.Save());

              SaveLoadService reader = CreateService();
              var progress = new Progress();
              reader.Register(new ProgressSerializer(progress));

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
              Assert.AreEqual(3, progress.Level);
              Assert.AreEqual("Cave", progress.Name);
              Assert.AreEqual(new Vector3(1f, 2f, 3f), progress.Position);
          }

          [Test]
          public void SaveLoadService_NoFile_Empty() => Assert.AreEqual(LoadOutcome.Empty, CreateService().Load());

          [Test]
          public void SaveLoadService_RegisterAfterLoad_AppliesItsSectionAtOnce()
          {
              SaveLoadService writer = CreateService();
              writer.Register(new ProgressSerializer(SampleProgress()));
              writer.Save();

              SaveLoadService reader = CreateService();
              reader.Load();
              var progress = new Progress();
              reader.Register(new ProgressSerializer(progress));

              Assert.AreEqual(3, progress.Level);
          }

          [Test]
          public void SaveLoadService_SectionWithoutSerializer_IsKept()
          {
              SaveLoadService first = CreateService();
              first.Register(new ProgressSerializer(SampleProgress()));
              first.Register(new FlakySerializer("other") { Value = 42 });
              first.Save();

              SaveLoadService second = CreateService();
              second.Register(new ProgressSerializer(new Progress()));
              second.Load();
              second.Save();

              SaveLoadService third = CreateService();
              var other = new FlakySerializer("other");
              third.Register(other);
              third.Load();

              Assert.AreEqual(42, other.Value);
          }

          [Test]
          public void SaveLoadService_SameKeyTwice_Throws()
          {
              SaveLoadService service = CreateService();
              service.Register(new FlakySerializer("same"));

              Assert.Throws<ArgumentException>(() => service.Register(new FlakySerializer("same")));
          }

          [Test]
          public void SaveLoadService_CaptureThrows_KeepsItsPreviousDataAndSavesOthers()
          {
              SaveLoadService service = CreateService();
              var flaky = new FlakySerializer("flaky") { Value = 1 };
              Progress progress = SampleProgress();
              service.Register(flaky);
              service.Register(new ProgressSerializer(progress));
              service.Save();

              flaky.Value = 2;
              flaky.FailCapture = true;
              progress.Level = 9;
              LogAssert.Expect(LogType.Error, new Regex("Saving 'flaky' failed"));
              Assert.AreEqual(SaveOutcome.Saved, service.Save());

              SaveLoadService reader = CreateService();
              var flakyRead = new FlakySerializer("flaky");
              var progressRead = new Progress();
              reader.Register(flakyRead);
              reader.Register(new ProgressSerializer(progressRead));
              reader.Load();

              Assert.AreEqual(1, flakyRead.Value);
              Assert.AreEqual(9, progressRead.Level);
          }

          [Test]
          public void SaveLoadService_RestoreThrows_OthersStillRestored()
          {
              SaveLoadService writer = CreateService();
              writer.Register(new FlakySerializer("flaky") { Value = 5 });
              writer.Register(new ProgressSerializer(SampleProgress()));
              writer.Save();

              SaveLoadService reader = CreateService();
              var progress = new Progress();
              reader.Register(new FlakySerializer("flaky") { FailRestore = true });
              reader.Register(new ProgressSerializer(progress));
              LogAssert.Expect(LogType.Error, new Regex("Loading 'flaky' failed"));

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
              Assert.AreEqual(3, progress.Level);
          }

          [Test]
          public void SaveLoadService_Load_RestoresInAscendingOrder()
          {
              var orders = new[] { ("late", 5), ("early", -1), ("middle", 0) };
              SaveLoadService writer = CreateService();
              foreach ((string key, int order) in orders)
                  writer.Register(new FlakySerializer(key, order));
              writer.Save();

              var restored = new List<string>();
              SaveLoadService reader = CreateService();
              foreach ((string key, int order) in orders)
                  reader.Register(new FlakySerializer(key, order) { OnRestore = () => restored.Add(key) });
              reader.Load();

              CollectionAssert.AreEqual(new[] { "early", "middle", "late" }, restored);
          }

          [Test]
          public void SaveLoadService_SaveDuringLoad_Blocked()
          {
              SaveLoadService writer = CreateService();
              writer.Register(new FlakySerializer("value") { Value = 1 });
              writer.Save();

              SaveLoadService reader = CreateService();
              SaveOutcome? during = null;
              reader.Register(new FlakySerializer("value") { OnRestore = () => during = reader.Save() });
              reader.Load();

              Assert.AreEqual(SaveOutcome.Blocked, during);
          }

          [Test]
          public void SaveLoadService_OnlyIfChangedNothingChanged_WritesNothing()
          {
              SaveLoadService service = CreateService();
              var value = new FlakySerializer("value") { Value = 1 };
              service.Register(value);
              service.Save();

              Assert.AreEqual(SaveOutcome.Unchanged, service.Save(onlyIfChanged: true));
              Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX), "a second write would make a backup");

              value.Value = 2;
              Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));
          }

          [Test]
          public void SaveLoadService_OnlyIfChangedEmptySlot_WritesNothing()
          {
              Assert.AreEqual(SaveOutcome.Unchanged, CreateService().Save(onlyIfChanged: true));
              Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE));
          }

          [Test]
          public void SaveLoadService_CaptureThenUnregister_KeepsCapturedDataWithoutWriting()
          {
              SaveLoadService service = CreateService();
              var scene = new FlakySerializer("scene") { Value = 7 };
              service.Register(scene);

              service.Capture();
              service.Unregister(scene);
              Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE), "capture must not write");

              scene.Value = 99; // no longer registered: not saved any more
              service.Save();

              SaveLoadService reader = CreateService();
              var read = new FlakySerializer("scene");
              reader.Register(read);
              reader.Load();
              Assert.AreEqual(7, read.Value);
          }

          [Test]
          public void SaveLoadService_DateLikeString_StaysAsWritten()
          {
              SaveLoadService writer = CreateService();
              writer.Register(new ProgressSerializer(new Progress { Name = "2026-10-23T14:05:00Z" }));
              writer.Save();

              SaveLoadService reader = CreateService();
              var progress = new Progress();
              reader.Register(new ProgressSerializer(progress));
              reader.Load();

              Assert.AreEqual("2026-10-23T14:05:00Z", progress.Name);
          }

          [Test]
          public void SaveLoadService_NegativeSlot_Throws() =>
              Assert.Throws<ArgumentOutOfRangeException>(() => CreateService().SelectSlot(-1));
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** Падают все `SaveLoadServiceTests` с `NotImplementedException`.

- [x] **Шаг 5: реализация** — `Runtime/Core/SaveLoadService.cs` целиком (версия задачи 4; задачи 5 и 6 её дополнят):

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;

  namespace Saving
  {
      /// <summary>
      /// Save slots over an <see cref="ISaveStorage"/>. Keeps the current slot's sections in memory: Save refreshes the
      /// sections of registered serializers and leaves the rest alone, so data of systems that are gone right now
      /// survives. A serializer registered after a load gets its section at once.
      /// </summary>
      public sealed class SaveLoadService
      {
          private readonly ISaveStorage _storage;
          private readonly string _gameVersion;
          private readonly List<IGameSerializer> _serializers = new();
          private readonly Dictionary<string, IGameSerializer> _byKey = new();
          private readonly string _emptyText;

          private Dictionary<string, SaveSection> _sections = new();
          private string _lastText;

          public SaveLoadService(ISaveStorage storage, string gameVersion)
          {
              _storage = storage ?? throw new ArgumentNullException(nameof(storage));
              _gameVersion = gameVersion ?? string.Empty;
              _emptyText = WriteSaveFile(new Dictionary<string, SaveSection>());
              _lastText = _emptyText;
          }

          public int CurrentSlot { get; private set; }
          public bool IsLoading { get; private set; }

          public event Action<SaveOutcome> Saved;
          public event Action<LoadOutcome> Loaded;

          public void Register(IGameSerializer serializer)
          {
              if (serializer == null)
                  throw new ArgumentNullException(nameof(serializer));

              if (string.IsNullOrEmpty(serializer.Key))
                  throw new ArgumentException("A serializer needs a key.", nameof(serializer));

              if (_byKey.ContainsKey(serializer.Key))
                  throw new ArgumentException($"A serializer with key '{serializer.Key}' is already registered.",
                      nameof(serializer));

              _byKey.Add(serializer.Key, serializer);
              _serializers.Add(serializer);

              if (_sections.TryGetValue(serializer.Key, out SaveSection section))
                  ApplyOnRegister(serializer, section);
          }

          /// <summary>Stops saving the serializer; its last captured section stays in the snapshot.</summary>
          public void Unregister(IGameSerializer serializer)
          {
              if (serializer == null || !_byKey.TryGetValue(serializer.Key, out IGameSerializer current)
                  || !ReferenceEquals(current, serializer))
                  return;

              _byKey.Remove(serializer.Key);
              _serializers.Remove(serializer);
          }

          /// <summary>Registered serializers -> snapshot, without writing: call before unloading what they save.</summary>
          public void Capture()
          {
              if (!IsLoading)
                  SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
          }

          public SaveOutcome Save(bool onlyIfChanged = false)
          {
              SaveOutcome outcome = SaveCurrentSlot(onlyIfChanged);
              Saved?.Invoke(outcome);
              return outcome;
          }

          public LoadOutcome Load()
          {
              if (IsLoading)
                  throw new InvalidOperationException("Load cannot be called while a load is in progress.");

              IsLoading = true;
              LoadOutcome outcome;
              try
              {
                  outcome = LoadCurrentSlot();
              }
              finally
              {
                  IsLoading = false;
              }

              Loaded?.Invoke(outcome);
              return outcome;
          }

          public void SelectSlot(int slot)
          {
              ValidateSlot(slot);
              if (IsLoading)
                  throw new InvalidOperationException("SelectSlot cannot be called while a load is in progress.");

              CurrentSlot = slot;
              StartEmpty();
          }

          // ---------- Save ----------

          private SaveOutcome SaveCurrentSlot(bool onlyIfChanged)
          {
              if (IsLoading)
                  return SaveOutcome.Blocked;

              SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
              string text = WriteSaveFile(_sections);
              if (onlyIfChanged && text == _lastText)
                  return SaveOutcome.Unchanged;

              _storage.Write(CurrentSlot, SaveFiles.SAVE, text);
              _lastText = text;
              return SaveOutcome.Saved;
          }

          private string WriteSaveFile(Dictionary<string, SaveSection> sections) =>
              SaveJson.Write(new SaveFile
              {
                  FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                  GameVersion = _gameVersion,
                  Sections = sections
              });

          // ---------- Load ----------

          private LoadOutcome LoadCurrentSlot()
          {
              if (!_storage.TryRead(CurrentSlot, SaveFiles.SAVE, out string text) || !TryParse(text, out SaveFile file))
              {
                  StartEmpty();
                  return LoadOutcome.Empty;
              }

              var prepared = new List<(IGameSerializer Serializer, JToken Data)>();
              foreach (IGameSerializer serializer in _serializers)
              {
                  if (file.Sections.TryGetValue(serializer.Key, out SaveSection section)
                      && SerializerGroup.Prepare(serializer, section, out JToken data, out _) == SectionStatus.Ok)
                      prepared.Add((serializer, data));
              }

              _sections = new Dictionary<string, SaveSection>(file.Sections);
              _lastText = text;
              SerializerGroup.RestoreInOrder(prepared);
              return LoadOutcome.Loaded;
          }

          private void ApplyOnRegister(IGameSerializer serializer, SaveSection section)
          {
              bool wasLoading = IsLoading;
              IsLoading = true;
              try
              {
                  if (SerializerGroup.Prepare(serializer, section, out JToken data, out _) == SectionStatus.Ok)
                      SerializerGroup.RestoreInOrder(new[] { (serializer, data) });
              }
              finally
              {
                  IsLoading = wasLoading;
              }
          }

          private static bool TryParse(string text, out SaveFile file)
          {
              file = null;
              if (!SaveJson.TryParseObject(text, out JObject json))
                  return false;

              try
              {
                  file = json.ToObject<SaveFile>(SaveJson.Serializer);
              }
              catch (Exception)
              {
                  return false;
              }

              return file?.Sections != null;
          }

          private void StartEmpty()
          {
              _sections = new Dictionary<string, SaveSection>();
              _lastText = _emptyText;
          }

          private static void ValidateSlot(int slot)
          {
              if (slot < 0)
                  throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots start at 0.");
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — зелёный.** EditMode: `failed 0`.

- [ ] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add SaveLoadService with snapshot and registration"
  ```

---

### Задача 5: надёжная загрузка, версии и миграции

**Files:**
- Modify: `Runtime/Core/SaveLoadService.cs` (целиком, версия задачи 5)
- Modify: `Tests/Editor/TestDoubles.cs` (+ `FailingStorage`), `Tests/Editor/SaveStorageContractTests.cs`
  (+ тест в `FileSaveStorageTests`)
- Create: `Tests/Editor/Golden/SaveSystemGolden_v1.json`
- Test: `Tests/Editor/SaveLoadServiceLoadTests.cs`

**Interfaces:**
- Consumes: задачи 1–4.
- Produces: `SaveLoadService.IsReadOnly`; загрузка возвращает `Recovered`, `Corrupted`, `NewerVersion`,
  `MigrationFailed`, `Failed`; `Save` при сбое хранилища — `Failed`, в слот «только для чтения» — `Blocked`;
  `SelectSlot` снимает «только для чтения».

- [x] **Шаг 1: двойник хранилища со сбоями** — дописать в конец namespace в `Tests/Editor/TestDoubles.cs`
  (и `using System.IO;` в начало файла):

  ```csharp
      /// <summary>In-memory storage whose reads or writes can be made to fail like a broken disk.</summary>
      internal sealed class FailingStorage : ISaveStorage
      {
          public readonly InMemorySaveStorage Inner = new();
          public bool FailWrites;
          public bool FailReads;

          public IReadOnlyList<int> ListSlots() => Inner.ListSlots();

          public bool TryRead(int slot, string file, out string text)
          {
              if (FailReads)
                  throw new IOException("read failed");

              return Inner.TryRead(slot, file, out text);
          }

          public bool TryReadBackup(int slot, string file, out string text)
          {
              if (FailReads)
                  throw new IOException("read failed");

              return Inner.TryReadBackup(slot, file, out text);
          }

          public void Write(int slot, string file, string text)
          {
              if (FailWrites)
                  throw new IOException("disk full");

              Inner.Write(slot, file, text);
          }

          public void Quarantine(int slot, string file, bool backup) => Inner.Quarantine(slot, file, backup);
          public void DeleteSlot(int slot) => Inner.DeleteSlot(slot);
      }
  ```

- [x] **Шаг 2: золотой файл** — `Tests/Editor/Golden/SaveSystemGolden_v1.json` (сохранение «старой сборки»: прогресс
  версии 1 с `lvl` и секция системы, которой в тестах нет). Файл пишется руками один раз; тест его не создаёт:

  ```json
  {
    "formatVersion": 1,
    "gameVersion": "0.9",
    "sections": {
      "progress": {
        "v": 1,
        "data": { "lvl": 4, "name": "Cave", "position": { "x": 1.5, "y": -2.0, "z": 0.0 } }
      },
      "unknown-system": {
        "v": 3,
        "data": { "anything": [ 1, 2, 3 ] }
      }
    }
  }
  ```

- [x] **Шаг 3: падающие тесты** — `Tests/Editor/SaveLoadServiceLoadTests.cs`:

  ```csharp
  using System.Text.RegularExpressions;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>Loading that never loses data: backups, corrupted files, newer saves, migrations, storage failures.</summary>
      public class SaveLoadServiceLoadTests
      {
          private const string NEWER_PROGRESS = @"{
    ""formatVersion"": 1,
    ""gameVersion"": ""9.0"",
    ""sections"": {
      ""progress"": { ""v"": 7, ""data"": { ""level"": 8 } },
      ""other"": { ""v"": 1, ""data"": 5 }
    }
  }";

          private const string NEWER_FORMAT = @"{ ""formatVersion"": 2, ""gameVersion"": ""9.0"", ""chunks"": [] }";

          private const string STEPS_V1 = @"{
    ""formatVersion"": 1,
    ""gameVersion"": ""0.9"",
    ""sections"": {
      ""steps"": { ""v"": 1, ""data"": {} },
      ""other"": { ""v"": 1, ""data"": 5 }
    }
  }";

          private InMemorySaveStorage _storage;

          [SetUp]
          public void SetUp() => _storage = new InMemorySaveStorage();

          private SaveLoadService CreateService() => new(_storage, "1.0");

          private void SaveProgress(int level)
          {
              SaveLoadService writer = CreateService();
              writer.Register(new ProgressSerializer(new Progress { Level = level, Name = "Cave" }));
              writer.Save();
          }

          private (SaveLoadService Service, Progress Progress) CreateReader()
          {
              SaveLoadService reader = CreateService();
              var progress = new Progress();
              reader.Register(new ProgressSerializer(progress));
              return (reader, progress);
          }

          private static string LoadGolden()
          {
              string[] guids = AssetDatabase.FindAssets("SaveSystemGolden_v1");
              Assert.AreEqual(1, guids.Length, "the golden file SaveSystemGolden_v1.json must exist exactly once");
              return AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[0])).text;
          }

          // ---------- Broken files ----------

          [Test]
          public void SaveLoadService_BrokenMain_RecoversFromBackup()
          {
              SaveProgress(1);
              SaveProgress(2);
              _storage.Put(0, SaveFiles.SAVE, "{ broken");
              (SaveLoadService reader, Progress progress) = CreateReader();

              LogAssert.Expect(LogType.Warning, new Regex("restored from its backup"));
              Assert.AreEqual(LoadOutcome.Recovered, reader.Load());

              Assert.AreEqual(1, progress.Level);
              Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX));
          }

          [Test]
          public void SaveLoadService_AfterRecovery_NextSaveWritesMainFile()
          {
              SaveProgress(1);
              SaveProgress(2);
              _storage.Put(0, SaveFiles.SAVE, "{ broken");
              (SaveLoadService reader, _) = CreateReader();
              LogAssert.Expect(LogType.Warning, new Regex("restored from its backup"));
              reader.Load();

              Assert.AreEqual(SaveOutcome.Saved, reader.Save(onlyIfChanged: true));
              Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE));
          }

          [Test]
          public void SaveLoadService_BothBroken_CorruptedAndStartsOver()
          {
              _storage.Put(0, SaveFiles.SAVE, "{ broken");
              _storage.Put(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX, "also broken");
              (SaveLoadService reader, Progress progress) = CreateReader();

              LogAssert.Expect(LogType.Warning, new Regex("is corrupted"));
              Assert.AreEqual(LoadOutcome.Corrupted, reader.Load());

              Assert.AreEqual(0, progress.Level);
              Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE + SaveFiles.CORRUPT_SUFFIX));
              Assert.IsTrue(_storage.Has(0, SaveFiles.SAVE + SaveFiles.BACKUP_SUFFIX + SaveFiles.CORRUPT_SUFFIX));
              Assert.AreEqual(SaveOutcome.Saved, reader.Save());
          }

          // ---------- Newer saves ----------

          [Test]
          public void SaveLoadService_NewerFormat_RefusedAndNeverOverwritten()
          {
              _storage.Put(0, SaveFiles.SAVE, NEWER_FORMAT);
              (SaveLoadService reader, _) = CreateReader();

              LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
              Assert.AreEqual(LoadOutcome.NewerVersion, reader.Load());

              Assert.IsTrue(reader.IsReadOnly);
              Assert.AreEqual(SaveOutcome.Blocked, reader.Save());
              _storage.TryRead(0, SaveFiles.SAVE, out string text);
              Assert.AreEqual(NEWER_FORMAT, text);
          }

          [Test]
          public void SaveLoadService_NewerSection_NothingApplied()
          {
              _storage.Put(0, SaveFiles.SAVE, NEWER_PROGRESS);
              (SaveLoadService reader, Progress progress) = CreateReader();
              var other = new FlakySerializer("other");
              reader.Register(other);

              LogAssert.Expect(LogType.Error, new Regex("newer than 2"));
              Assert.AreEqual(LoadOutcome.NewerVersion, reader.Load());

              Assert.AreEqual(0, progress.Level);
              Assert.AreEqual(0, other.Value, "nothing is applied when one section cannot be read");
          }

          [Test]
          public void SaveLoadService_SelectSlotAfterRefusal_ClearsReadOnly()
          {
              _storage.Put(0, SaveFiles.SAVE, NEWER_FORMAT);
              (SaveLoadService reader, _) = CreateReader();
              LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
              reader.Load();

              reader.SelectSlot(0);

              Assert.IsFalse(reader.IsReadOnly);
              Assert.AreEqual(SaveOutcome.Saved, reader.Save());
          }

          [Test]
          public void SaveLoadService_RegisterNewerSection_SlotBecomesReadOnly()
          {
              _storage.Put(0, SaveFiles.SAVE, NEWER_PROGRESS);
              SaveLoadService reader = CreateService();
              Assert.AreEqual(LoadOutcome.Loaded, reader.Load(), "nothing registered yet: nothing to refuse");

              LogAssert.Expect(LogType.Error, new Regex("becomes read-only"));
              reader.Register(new ProgressSerializer(new Progress()));

              Assert.IsTrue(reader.IsReadOnly);
              Assert.AreEqual(SaveOutcome.Blocked, reader.Save());
          }

          // ---------- Migrations ----------

          [Test]
          public void SaveLoadService_OlderSection_MigratesStepByStep()
          {
              _storage.Put(0, SaveFiles.SAVE, STEPS_V1);
              SaveLoadService reader = CreateService();
              var steps = new StepSerializer("steps");
              reader.Register(steps);

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

              CollectionAssert.AreEqual(new[] { 1, 2 }, steps.Steps);
              Assert.IsTrue((bool)steps.Restored["step1"] && (bool)steps.Restored["step2"]);
          }

          [Test]
          public void SaveLoadService_LegacyProgress_MigratesToCurrent()
          {
              SaveLoadService legacy = CreateService();
              legacy.Register(new LegacyProgressSerializer(new Progress { Level = 6 }));
              legacy.Save();
              (SaveLoadService reader, Progress progress) = CreateReader();

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
              Assert.AreEqual(6, progress.Level);
          }

          [Test]
          public void SaveLoadService_MigrationThrows_NothingAppliedAndReadOnly()
          {
              _storage.Put(0, SaveFiles.SAVE, STEPS_V1);
              SaveLoadService reader = CreateService();
              reader.Register(new StepSerializer("steps") { FailUpgrade = true });
              var other = new FlakySerializer("other");
              reader.Register(other);

              LogAssert.Expect(LogType.Error, new Regex("Migrating 'steps' from version 1 failed"));
              Assert.AreEqual(LoadOutcome.MigrationFailed, reader.Load());

              Assert.AreEqual(0, other.Value);
              Assert.IsTrue(reader.IsReadOnly);
          }

          [Test]
          public void SaveLoadService_Migrated_KeepsOriginalCopyOnce()
          {
              SaveLoadService legacy = CreateService();
              legacy.Register(new LegacyProgressSerializer(new Progress { Level = 1 }));
              legacy.Save();
              _storage.TryRead(0, SaveFiles.SAVE, out string original);

              CreateReader().Service.Load();
              SaveLoadService legacyAgain = CreateService();
              legacyAgain.Register(new LegacyProgressSerializer(new Progress { Level = 2 }));
              legacyAgain.Save();
              CreateReader().Service.Load();

              Assert.IsTrue(_storage.TryRead(0, SaveFiles.PREMIGRATION, out string copy));
              Assert.AreEqual(original, copy, "the copy is made before the first migration only");
          }

          // ---------- Storage failures ----------

          [Test]
          public void SaveLoadService_WriteFails_FailedAndRetriedLater()
          {
              var storage = new FailingStorage();
              var service = new SaveLoadService(storage, "1.0");
              var value = new FlakySerializer("value") { Value = 1 };
              service.Register(value);
              service.Save();

              value.Value = 2;
              storage.FailWrites = true;
              LogAssert.Expect(LogType.Error, new Regex("Saving slot 0 failed"));
              Assert.AreEqual(SaveOutcome.Failed, service.Save());

              var reader = new SaveLoadService(storage, "1.0");
              var read = new FlakySerializer("value");
              reader.Register(read);
              reader.Load();
              Assert.AreEqual(1, read.Value, "the file on disk is the previous one");

              storage.FailWrites = false;
              Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));
          }

          [Test]
          public void SaveLoadService_ReadFails_FailedAndReadOnly()
          {
              var storage = new FailingStorage { FailReads = true };
              var service = new SaveLoadService(storage, "1.0");

              LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));
              Assert.AreEqual(LoadOutcome.Failed, service.Load());

              Assert.IsTrue(service.IsReadOnly);
          }

          // ---------- Golden file ----------

          [Test]
          public void SaveLoadService_GoldenV1_LoadsExpectedValues()
          {
              _storage.Put(0, SaveFiles.SAVE, LoadGolden());
              (SaveLoadService reader, Progress progress) = CreateReader();

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());

              Assert.AreEqual(4, progress.Level);
              Assert.AreEqual("Cave", progress.Name);
              Assert.AreEqual(new Vector3(1.5f, -2f, 0f), progress.Position);
          }

          [Test]
          public void SaveLoadService_GoldenV1_UnknownSectionSurvivesSave()
          {
              _storage.Put(0, SaveFiles.SAVE, LoadGolden());
              (SaveLoadService reader, _) = CreateReader();
              reader.Load();

              reader.Save();

              _storage.TryRead(0, SaveFiles.SAVE, out string text);
              Assert.IsTrue(SaveJson.TryParseObject(text, out JObject json));
              JToken unknown = json["sections"]["unknown-system"];
              Assert.AreEqual(3, (int)unknown["v"]);
              Assert.IsTrue(JToken.DeepEquals(new JArray(1, 2, 3), unknown["data"]["anything"]));
          }
      }
  }
  ```

  И в класс `FileSaveStorageTests` (`SaveStorageContractTests.cs`; добавить `using System.Text.RegularExpressions;`,
  `using UnityEngine;`, `using UnityEngine.TestTools;`) — проверка на настоящем диске:

  ```csharp
          [Test]
          public void FileStorage_ServiceWithBrokenMain_RecoversFromBackup()
          {
              var service = new SaveLoadService(Storage, "1.0");
              var value = new FlakySerializer("value") { Value = 1 };
              service.Register(value);
              service.Save();
              value.Value = 2;
              service.Save();
              File.WriteAllText(PathOf(0, SaveFiles.SAVE), "{ broken");

              var reader = new SaveLoadService(new FileSaveStorage(_root), "1.0");
              var read = new FlakySerializer("value");
              reader.Register(read);
              LogAssert.Expect(LogType.Warning, new Regex("restored from its backup"));

              Assert.AreEqual(LoadOutcome.Recovered, reader.Load());
              Assert.AreEqual(1, read.Value);
              Assert.IsTrue(File.Exists(PathOf(0, SaveFiles.SAVE) + SaveFiles.CORRUPT_SUFFIX));
          }
  ```

- [x] **Шаг 4: запуск — красный.** Тесты не компилируются, пока у сервиса нет `IsReadOnly`: добавить в
  `SaveLoadService` заглушку `public bool IsReadOnly => throw new NotImplementedException();`, `Refresh`,
  запуск. Ожидание: падают `SaveLoadServiceLoadTests` (кроме `LegacyProgress_MigratesToCurrent` и обоих
  `GoldenV1_*` — миграция и неизвестные секции уже работают) и `FileStorage_ServiceWithBrokenMain…` — неверный
  `LoadOutcome`, `NotImplementedException` или исключение `IOException` наружу.

- [x] **Шаг 5: реализация** — `Runtime/Core/SaveLoadService.cs` целиком (версия задачи 5):

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving
  {
      /// <summary>
      /// Save slots over an <see cref="ISaveStorage"/>. Keeps the current slot's sections in memory: Save refreshes the
      /// sections of registered serializers and leaves the rest alone, so data of systems that are gone right now
      /// survives. Load checks every registered section first and applies only when all of them can be read; a
      /// serializer registered later gets its section at once. A slot written by a newer game, or one that failed to
      /// migrate or to read, turns read-only and is never overwritten.
      /// </summary>
      public sealed class SaveLoadService
      {
          private enum ReadStatus
          {
              Missing,
              Corrupt,
              Newer,
              Ok
          }

          private readonly ISaveStorage _storage;
          private readonly string _gameVersion;
          private readonly List<IGameSerializer> _serializers = new();
          private readonly Dictionary<string, IGameSerializer> _byKey = new();
          private readonly string _emptyText;

          private Dictionary<string, SaveSection> _sections = new();
          private string _lastText;

          public SaveLoadService(ISaveStorage storage, string gameVersion)
          {
              _storage = storage ?? throw new ArgumentNullException(nameof(storage));
              _gameVersion = gameVersion ?? string.Empty;
              _emptyText = WriteSaveFile(new Dictionary<string, SaveSection>());
              _lastText = _emptyText;
          }

          public int CurrentSlot { get; private set; }
          public bool IsLoading { get; private set; }
          public bool IsReadOnly { get; private set; }

          public event Action<SaveOutcome> Saved;
          public event Action<LoadOutcome> Loaded;

          public void Register(IGameSerializer serializer)
          {
              if (serializer == null)
                  throw new ArgumentNullException(nameof(serializer));

              if (string.IsNullOrEmpty(serializer.Key))
                  throw new ArgumentException("A serializer needs a key.", nameof(serializer));

              if (_byKey.ContainsKey(serializer.Key))
                  throw new ArgumentException($"A serializer with key '{serializer.Key}' is already registered.",
                      nameof(serializer));

              _byKey.Add(serializer.Key, serializer);
              _serializers.Add(serializer);

              if (_sections.TryGetValue(serializer.Key, out SaveSection section))
                  ApplyOnRegister(serializer, section);
          }

          /// <summary>Stops saving the serializer; its last captured section stays in the snapshot.</summary>
          public void Unregister(IGameSerializer serializer)
          {
              if (serializer == null || !_byKey.TryGetValue(serializer.Key, out IGameSerializer current)
                  || !ReferenceEquals(current, serializer))
                  return;

              _byKey.Remove(serializer.Key);
              _serializers.Remove(serializer);
          }

          /// <summary>Registered serializers -> snapshot, without writing: call before unloading what they save.</summary>
          public void Capture()
          {
              if (!IsLoading)
                  SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
          }

          public SaveOutcome Save(bool onlyIfChanged = false)
          {
              SaveOutcome outcome = SaveCurrentSlot(onlyIfChanged);
              Saved?.Invoke(outcome);
              return outcome;
          }

          public LoadOutcome Load()
          {
              if (IsLoading)
                  throw new InvalidOperationException("Load cannot be called while a load is in progress.");

              IsLoading = true;
              LoadOutcome outcome;
              try
              {
                  outcome = LoadCurrentSlot();
              }
              catch (Exception exception)
              {
                  outcome = Refuse(LoadOutcome.Failed, $"the storage failed: {exception.Message}");
              }
              finally
              {
                  IsLoading = false;
              }

              Loaded?.Invoke(outcome);
              return outcome;
          }

          public void SelectSlot(int slot)
          {
              ValidateSlot(slot);
              if (IsLoading)
                  throw new InvalidOperationException("SelectSlot cannot be called while a load is in progress.");

              CurrentSlot = slot;
              StartEmpty();
          }

          // ---------- Save ----------

          private SaveOutcome SaveCurrentSlot(bool onlyIfChanged)
          {
              if (IsLoading || IsReadOnly)
                  return SaveOutcome.Blocked;

              SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
              string text = WriteSaveFile(_sections);
              if (onlyIfChanged && text == _lastText)
                  return SaveOutcome.Unchanged;

              try
              {
                  _storage.Write(CurrentSlot, SaveFiles.SAVE, text);
              }
              catch (Exception exception)
              {
                  Debug.LogError($"Saving slot {CurrentSlot} failed: {exception.Message}");
                  return SaveOutcome.Failed;
              }

              _lastText = text;
              return SaveOutcome.Saved;
          }

          private string WriteSaveFile(Dictionary<string, SaveSection> sections) =>
              SaveJson.Write(new SaveFile
              {
                  FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                  GameVersion = _gameVersion,
                  Sections = sections
              });

          // ---------- Load ----------

          private LoadOutcome LoadCurrentSlot()
          {
              ReadStatus main = ReadSaveFile(backup: false, out SaveFile file, out string text);
              if (main == ReadStatus.Newer)
                  return Refuse(LoadOutcome.NewerVersion, "it was written by a newer version of the game");

              LoadOutcome outcome = LoadOutcome.Loaded;
              if (main != ReadStatus.Ok)
              {
                  if (main == ReadStatus.Corrupt)
                      _storage.Quarantine(CurrentSlot, SaveFiles.SAVE, backup: false);

                  ReadStatus backup = ReadSaveFile(backup: true, out file, out text);
                  if (backup == ReadStatus.Newer)
                      return Refuse(LoadOutcome.NewerVersion, "its backup was written by a newer version of the game");

                  if (backup != ReadStatus.Ok)
                  {
                      if (backup == ReadStatus.Corrupt)
                          _storage.Quarantine(CurrentSlot, SaveFiles.SAVE, backup: true);

                      StartEmpty();
                      if (main == ReadStatus.Missing && backup == ReadStatus.Missing)
                          return LoadOutcome.Empty;

                      Debug.LogWarning($"Save slot {CurrentSlot} is corrupted, starting over; the broken files were " +
                                       $"renamed to *{SaveFiles.CORRUPT_SUFFIX}.");
                      return LoadOutcome.Corrupted;
                  }

                  outcome = LoadOutcome.Recovered;
              }

              var prepared = new List<(IGameSerializer Serializer, JToken Data)>();
              bool migrated = false;
              foreach (IGameSerializer serializer in _serializers)
              {
                  if (!file.Sections.TryGetValue(serializer.Key, out SaveSection section))
                      continue;

                  SectionStatus status = SerializerGroup.Prepare(serializer, section, out JToken data, out string problem);
                  if (status == SectionStatus.NewerVersion)
                      return Refuse(LoadOutcome.NewerVersion, problem);

                  if (status == SectionStatus.MigrationFailed)
                      return Refuse(LoadOutcome.MigrationFailed, problem);

                  migrated |= section.V < serializer.Version;
                  prepared.Add((serializer, data));
              }

              if (outcome == LoadOutcome.Recovered)
                  Debug.LogWarning($"Save slot {CurrentSlot} was restored from its backup " +
                                   $"({SaveFiles.SAVE}{SaveFiles.BACKUP_SUFFIX}).");

              if (migrated)
                  KeepPremigrationCopy(text);

              _sections = new Dictionary<string, SaveSection>(file.Sections);
              // After a recovery the main file is gone: the next save must write it even if nothing changed
              _lastText = outcome == LoadOutcome.Recovered ? null : text;
              IsReadOnly = false;
              SerializerGroup.RestoreInOrder(prepared);
              return outcome;
          }

          private ReadStatus ReadSaveFile(bool backup, out SaveFile file, out string text)
          {
              file = null;
              bool found = backup
                  ? _storage.TryReadBackup(CurrentSlot, SaveFiles.SAVE, out text)
                  : _storage.TryRead(CurrentSlot, SaveFiles.SAVE, out text);

              if (!found)
                  return ReadStatus.Missing;

              // The version goes first: a newer format may have a shape this build cannot parse
              if (!SaveJson.TryParseObject(text, out JObject json) || json["formatVersion"]?.Type != JTokenType.Integer)
                  return ReadStatus.Corrupt;

              int formatVersion = (int)json["formatVersion"];
              if (formatVersion > SaveFile.CURRENT_FORMAT_VERSION)
                  return ReadStatus.Newer;

              if (formatVersion < 1)
                  return ReadStatus.Corrupt;

              try
              {
                  file = json.ToObject<SaveFile>(SaveJson.Serializer);
              }
              catch (Exception)
              {
                  return ReadStatus.Corrupt;
              }

              if (file?.Sections == null)
                  return ReadStatus.Corrupt;

              foreach (SaveSection section in file.Sections.Values)
              {
                  if (section == null)
                      return ReadStatus.Corrupt;
              }

              return ReadStatus.Ok;
          }

          private void ApplyOnRegister(IGameSerializer serializer, SaveSection section)
          {
              bool wasLoading = IsLoading;
              IsLoading = true;
              try
              {
                  SectionStatus status = SerializerGroup.Prepare(serializer, section, out JToken data, out string problem);
                  if (status != SectionStatus.Ok)
                  {
                      IsReadOnly = true;
                      Debug.LogError($"Save slot {CurrentSlot} becomes read-only: {problem}.");
                      return;
                  }

                  SerializerGroup.RestoreInOrder(new[] { (serializer, data) });
              }
              finally
              {
                  IsLoading = wasLoading;
              }
          }

          private LoadOutcome Refuse(LoadOutcome outcome, string problem)
          {
              IsReadOnly = true;
              Debug.LogError($"Save slot {CurrentSlot} is not loaded and will not be overwritten: {problem}.");
              return outcome;
          }

          private void KeepPremigrationCopy(string text)
          {
              try
              {
                  if (!_storage.TryRead(CurrentSlot, SaveFiles.PREMIGRATION, out _))
                      _storage.Write(CurrentSlot, SaveFiles.PREMIGRATION, text);
              }
              catch (Exception exception)
              {
                  Debug.LogWarning($"Could not keep the pre-migration copy of save slot {CurrentSlot}: {exception.Message}");
              }
          }

          private void StartEmpty()
          {
              _sections = new Dictionary<string, SaveSection>();
              _lastText = _emptyText;
              IsReadOnly = false;
          }

          private static void ValidateSlot(int slot)
          {
              if (slot < 0)
                  throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots start at 0.");
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — зелёный.** EditMode: `failed 0` (вместе с тестами задачи 4).

- [ ] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Recover saves from backups and refuse newer or unreadable ones"
  ```

---

### Задача 6: слоты, метаданные, `ISaveLoadService`

**Files:**
- Create: `Runtime/Core/SlotMeta.cs`, `SlotInfo.cs`, `ISaveLoadService.cs`
- Modify: `Runtime/Core/SaveLoadService.cs` (целиком, финальная версия)
- Test: `Tests/Editor/SaveSlotsTests.cs`

**Interfaces:**
- Consumes: задачи 1–5.
- Produces:
  - `SlotMeta { const string TIME_FORMAT; int FormatVersion; string GameVersion; string SavedAtUtc; double PlayTimeSeconds; Dictionary<string, string> Summary; static string FormatTime(DateTime); static DateTime ParseTime(string); }`;
  - `SlotInfo(int slot, SlotMeta meta)` с `Slot`, `GameVersion`, `SavedAtUtc` (DateTime UTC), `PlayTimeSeconds`,
    `Summary` (`IReadOnlyDictionary<string, string>`);
  - `ISaveLoadService` (ниже); `SaveLoadService : ISaveLoadService` с конструктором
    `(ISaveStorage storage, string gameVersion, Func<DateTime> utcNow = null)` и `AddPlayTime(double)`.

- [x] **Шаг 1: метаданные и интерфейс** (финальные).

  `Runtime/Core/SlotMeta.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Globalization;

  namespace Saving
  {
      /// <summary>meta.json of a slot: what the slot menu shows without reading the save itself.</summary>
      public sealed class SlotMeta
      {
          public const string TIME_FORMAT = "yyyy-MM-dd'T'HH:mm:ss'Z'";

          public int FormatVersion;
          public string GameVersion;
          public string SavedAtUtc;
          public double PlayTimeSeconds;
          public Dictionary<string, string> Summary = new();

          public static string FormatTime(DateTime utc) =>
              utc.ToUniversalTime().ToString(TIME_FORMAT, CultureInfo.InvariantCulture);

          public static DateTime ParseTime(string text) =>
              DateTime.TryParseExact(text, TIME_FORMAT, CultureInfo.InvariantCulture,
                  DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime utc)
                  ? utc
                  : DateTime.MinValue;
      }
  }
  ```

  `Runtime/Core/SlotInfo.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving
  {
      /// <summary>A slot as the slot menu sees it, built from meta.json alone.</summary>
      public sealed class SlotInfo
      {
          public SlotInfo(int slot, SlotMeta meta)
          {
              Slot = slot;
              GameVersion = meta.GameVersion;
              SavedAtUtc = SlotMeta.ParseTime(meta.SavedAtUtc);
              PlayTimeSeconds = meta.PlayTimeSeconds;
              Summary = new Dictionary<string, string>(meta.Summary ?? new Dictionary<string, string>());
          }

          public int Slot { get; }
          public string GameVersion { get; }
          public DateTime SavedAtUtc { get; }
          public double PlayTimeSeconds { get; }
          public IReadOnlyDictionary<string, string> Summary { get; }
      }
  }
  ```

  `Runtime/Core/ISaveLoadService.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving
  {
      /// <summary>The save system as game code sees it: the key for ServiceLocator.</summary>
      public interface ISaveLoadService
      {
          int CurrentSlot { get; }
          bool IsLoading { get; }
          bool IsReadOnly { get; }                     // slot from a newer game or failed migration: never overwritten
          double PlayTime { get; }
          IDictionary<string, string> Summary { get; } // written to meta.json

          event Action<SaveOutcome> Saved;
          event Action<LoadOutcome> Loaded;

          void Register(IGameSerializer serializer);   // applies its section from the snapshot right away, if present
          void Unregister(IGameSerializer serializer); // does not capture
          void Capture();                              // registered serializers -> snapshot, no disk write
          SaveOutcome Save(bool onlyIfChanged = false);
          LoadOutcome Load();                          // current slot: disk -> snapshot -> registered serializers
          void SelectSlot(int slot, bool keepData = false); // keepData: "save as" into another slot
          IReadOnlyList<SlotInfo> GetSlots();          // reads meta.json only
          void DeleteSlot(int slot);
      }
  }
  ```

- [x] **Шаг 2: заглушки в `SaveLoadService`:** добавить `: ISaveLoadService` к объявлению класса; в конструктор —
  третий параметр `Func<DateTime> utcNow = null` (пока не используется); членами-заглушками:

  ```csharp
          public double PlayTime => throw new NotImplementedException();
          public IDictionary<string, string> Summary => throw new NotImplementedException();
          public IReadOnlyList<SlotInfo> GetSlots() => throw new NotImplementedException();
          public void DeleteSlot(int slot) => throw new NotImplementedException();
          public void AddPlayTime(double seconds) => throw new NotImplementedException();
  ```

  и заменить `public void SelectSlot(int slot)` на `public void SelectSlot(int slot, bool keepData = false)` (тело то
  же; `keepData` пока не используется).

- [x] **Шаг 3: падающие тесты** — `Tests/Editor/SaveSlotsTests.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Text.RegularExpressions;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>Slots, meta.json, play time and summary.</summary>
      public class SaveSlotsTests
      {
          private static readonly DateTime SavedAt = new(2026, 10, 23, 14, 5, 0, DateTimeKind.Utc);

          private InMemorySaveStorage _storage;

          [SetUp]
          public void SetUp() => _storage = new InMemorySaveStorage();

          private SaveLoadService CreateService() => new(_storage, "1.0", () => SavedAt);

          [Test]
          public void SaveSlots_DifferentSlots_DoNotShareData()
          {
              SaveLoadService service = CreateService();
              var value = new FlakySerializer("value");
              service.Register(value);

              value.Value = 1;
              service.Save();
              service.SelectSlot(1);
              value.Value = 2;
              service.Save();

              service.SelectSlot(0);
              service.Load();
              Assert.AreEqual(1, value.Value);
              service.SelectSlot(1);
              service.Load();
              Assert.AreEqual(2, value.Value);
          }

          [Test]
          public void SaveSlots_KeepData_CopiesSnapshotIntoOtherSlot()
          {
              SaveLoadService writer = CreateService();
              writer.Register(new FlakySerializer("other") { Value = 5 });
              writer.Register(new FlakySerializer("value") { Value = 1 });
              writer.Save();

              SaveLoadService service = CreateService();
              service.Register(new FlakySerializer("value"));
              service.Load();
              service.SelectSlot(1, keepData: true);
              Assert.AreEqual(SaveOutcome.Saved, service.Save(onlyIfChanged: true));

              SaveLoadService reader = CreateService();
              reader.SelectSlot(1);
              var other = new FlakySerializer("other");
              reader.Register(other);
              reader.Load();
              Assert.AreEqual(5, other.Value);
          }

          [Test]
          public void SaveSlots_GetSlots_ReadsMetaOnly()
          {
              SaveLoadService service = CreateService();
              service.Summary["level"] = "Cave";
              service.AddPlayTime(12.5);
              service.Save();
              service.SelectSlot(2);
              service.Summary["level"] = "Lava";
              service.Save();
              _storage.Put(2, SaveFiles.SAVE, "{ broken");

              IReadOnlyList<SlotInfo> slots = CreateService().GetSlots();

              Assert.AreEqual(2, slots.Count);
              Assert.AreEqual(0, slots[0].Slot);
              Assert.AreEqual("Cave", slots[0].Summary["level"]);
              Assert.AreEqual(12.5, slots[0].PlayTimeSeconds);
              Assert.AreEqual(SavedAt, slots[0].SavedAtUtc);
              Assert.AreEqual("1.0", slots[0].GameVersion);
              Assert.AreEqual(2, slots[1].Slot);
              Assert.AreEqual("Lava", slots[1].Summary["level"]);
              Assert.AreEqual(0, slots[1].PlayTimeSeconds, "selecting a slot starts its play time over");
          }

          [Test]
          public void SaveSlots_SlotWithoutMeta_NotListed()
          {
              _storage.Put(3, SaveFiles.SAVE, "{}");

              CollectionAssert.IsEmpty(CreateService().GetSlots());
          }

          [Test]
          public void SaveSlots_DeleteCurrent_RemovesFilesAndStartsOver()
          {
              SaveLoadService service = CreateService();
              service.Register(new FlakySerializer("value") { Value = 1 });
              service.Summary["level"] = "Cave";
              service.Save();

              service.DeleteSlot(0);

              Assert.IsFalse(_storage.Has(0, SaveFiles.SAVE));
              Assert.IsFalse(_storage.Has(0, SaveFiles.META));
              CollectionAssert.IsEmpty(service.Summary);
              Assert.AreEqual(LoadOutcome.Empty, service.Load());
          }

          [Test]
          public void SaveSlots_PlayTimeAndSummary_SurviveSaveAndLoad()
          {
              SaveLoadService writer = CreateService();
              writer.AddPlayTime(12.5);
              writer.AddPlayTime(-3);
              writer.Summary["checkpoint"] = "Gate";
              writer.Save();

              SaveLoadService reader = CreateService();
              reader.Load();

              Assert.AreEqual(12.5, reader.PlayTime);
              Assert.AreEqual("Gate", reader.Summary["checkpoint"]);
          }

          [Test]
          public void SaveSlots_UnreadableMeta_DataLoadsTimeFromZero()
          {
              SaveLoadService writer = CreateService();
              writer.Register(new FlakySerializer("value") { Value = 4 });
              writer.AddPlayTime(10);
              writer.Save();
              _storage.Put(0, SaveFiles.META, "{ broken");

              SaveLoadService reader = CreateService();
              var value = new FlakySerializer("value");
              reader.Register(value);
              LogAssert.Expect(LogType.Warning, new Regex("unreadable"));

              Assert.AreEqual(LoadOutcome.Loaded, reader.Load());
              Assert.AreEqual(4, value.Value);
              Assert.AreEqual(0, reader.PlayTime);
          }

          [Test]
          public void SaveSlots_SelectSlot_ClearsSummaryAndPlayTime()
          {
              SaveLoadService service = CreateService();
              service.Summary["level"] = "Cave";
              service.AddPlayTime(5);

              service.SelectSlot(1);

              CollectionAssert.IsEmpty(service.Summary);
              Assert.AreEqual(0, service.PlayTime);
          }
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** Падают все `SaveSlotsTests`, кроме `DifferentSlots_DoNotShareData` (слоты как
  таковые работают с задачи 4, тест закрепляет поведение): `NotImplementedException`, у `KeepData_*` — `other`
  не скопирован (снимок очищен).

- [x] **Шаг 5: реализация** — `Runtime/Core/SaveLoadService.cs` целиком (финальная версия части 1):

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Saving
  {
      /// <summary>
      /// Save slots over an <see cref="ISaveStorage"/>. Keeps the current slot's sections in memory: Save refreshes the
      /// sections of registered serializers and leaves the rest alone, so data of systems that are gone right now
      /// survives. Load checks every registered section first and applies only when all of them can be read; a
      /// serializer registered later gets its section at once. A slot written by a newer game, or one that failed to
      /// migrate or to read, turns read-only and is never overwritten. meta.json carries time, play time and summary.
      /// </summary>
      public sealed class SaveLoadService : ISaveLoadService
      {
          private enum ReadStatus
          {
              Missing,
              Corrupt,
              Newer,
              Ok
          }

          private readonly ISaveStorage _storage;
          private readonly string _gameVersion;
          private readonly Func<DateTime> _utcNow;
          private readonly List<IGameSerializer> _serializers = new();
          private readonly Dictionary<string, IGameSerializer> _byKey = new();
          private readonly Dictionary<string, string> _summary = new();
          private readonly string _emptyText;

          private Dictionary<string, SaveSection> _sections = new();
          private string _lastText;

          public SaveLoadService(ISaveStorage storage, string gameVersion, Func<DateTime> utcNow = null)
          {
              _storage = storage ?? throw new ArgumentNullException(nameof(storage));
              _gameVersion = gameVersion ?? string.Empty;
              _utcNow = utcNow ?? (() => DateTime.UtcNow);
              _emptyText = WriteSaveFile(new Dictionary<string, SaveSection>());
              _lastText = _emptyText;
          }

          public int CurrentSlot { get; private set; }
          public bool IsLoading { get; private set; }
          public bool IsReadOnly { get; private set; }
          public double PlayTime { get; private set; }
          public IDictionary<string, string> Summary => _summary;

          public event Action<SaveOutcome> Saved;
          public event Action<LoadOutcome> Loaded;

          public void Register(IGameSerializer serializer)
          {
              if (serializer == null)
                  throw new ArgumentNullException(nameof(serializer));

              if (string.IsNullOrEmpty(serializer.Key))
                  throw new ArgumentException("A serializer needs a key.", nameof(serializer));

              if (_byKey.ContainsKey(serializer.Key))
                  throw new ArgumentException($"A serializer with key '{serializer.Key}' is already registered.",
                      nameof(serializer));

              _byKey.Add(serializer.Key, serializer);
              _serializers.Add(serializer);

              if (_sections.TryGetValue(serializer.Key, out SaveSection section))
                  ApplyOnRegister(serializer, section);
          }

          /// <summary>Stops saving the serializer; its last captured section stays in the snapshot.</summary>
          public void Unregister(IGameSerializer serializer)
          {
              if (serializer == null || !_byKey.TryGetValue(serializer.Key, out IGameSerializer current)
                  || !ReferenceEquals(current, serializer))
                  return;

              _byKey.Remove(serializer.Key);
              _serializers.Remove(serializer);
          }

          /// <summary>Registered serializers -> snapshot, without writing: call before unloading what they save.</summary>
          public void Capture()
          {
              if (!IsLoading)
                  SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
          }

          public SaveOutcome Save(bool onlyIfChanged = false)
          {
              SaveOutcome outcome = SaveCurrentSlot(onlyIfChanged);
              Saved?.Invoke(outcome);
              return outcome;
          }

          public LoadOutcome Load()
          {
              if (IsLoading)
                  throw new InvalidOperationException("Load cannot be called while a load is in progress.");

              IsLoading = true;
              LoadOutcome outcome;
              try
              {
                  outcome = LoadCurrentSlot();
              }
              catch (Exception exception)
              {
                  outcome = Refuse(LoadOutcome.Failed, $"the storage failed: {exception.Message}");
              }
              finally
              {
                  IsLoading = false;
              }

              Loaded?.Invoke(outcome);
              return outcome;
          }

          public void SelectSlot(int slot, bool keepData = false)
          {
              ValidateSlot(slot);
              if (IsLoading)
                  throw new InvalidOperationException("SelectSlot cannot be called while a load is in progress.");

              CurrentSlot = slot;
              IsReadOnly = false;

              if (keepData)
                  _lastText = null; // the new slot's file is unknown: the next save always writes
              else
                  StartEmpty();
          }

          public IReadOnlyList<SlotInfo> GetSlots()
          {
              var slots = new List<SlotInfo>();
              foreach (int slot in _storage.ListSlots())
              {
                  if (_storage.TryRead(slot, SaveFiles.META, out string text) && SaveJson.TryRead(text, out SlotMeta meta))
                      slots.Add(new SlotInfo(slot, meta));
              }

              return slots;
          }

          public void DeleteSlot(int slot)
          {
              ValidateSlot(slot);
              _storage.DeleteSlot(slot);

              if (slot == CurrentSlot)
                  StartEmpty();
          }

          /// <summary>Adds played time; the save system calls it every frame with game time.</summary>
          public void AddPlayTime(double seconds)
          {
              if (seconds > 0)
                  PlayTime += seconds;
          }

          // ---------- Save ----------

          private SaveOutcome SaveCurrentSlot(bool onlyIfChanged)
          {
              if (IsLoading || IsReadOnly)
                  return SaveOutcome.Blocked;

              SerializerGroup.CaptureInto(_serializers.ToArray(), _sections);
              string text = WriteSaveFile(_sections);
              if (onlyIfChanged && text == _lastText)
                  return SaveOutcome.Unchanged;

              try
              {
                  _storage.Write(CurrentSlot, SaveFiles.SAVE, text);
                  _storage.Write(CurrentSlot, SaveFiles.META, WriteMeta());
              }
              catch (Exception exception)
              {
                  Debug.LogError($"Saving slot {CurrentSlot} failed: {exception.Message}");
                  return SaveOutcome.Failed;
              }

              _lastText = text;
              return SaveOutcome.Saved;
          }

          private string WriteSaveFile(Dictionary<string, SaveSection> sections) =>
              SaveJson.Write(new SaveFile
              {
                  FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                  GameVersion = _gameVersion,
                  Sections = sections
              });

          private string WriteMeta() =>
              SaveJson.Write(new SlotMeta
              {
                  FormatVersion = SaveFile.CURRENT_FORMAT_VERSION,
                  GameVersion = _gameVersion,
                  SavedAtUtc = SlotMeta.FormatTime(_utcNow()),
                  PlayTimeSeconds = PlayTime,
                  Summary = new Dictionary<string, string>(_summary)
              });

          // ---------- Load ----------

          private LoadOutcome LoadCurrentSlot()
          {
              ReadStatus main = ReadSaveFile(backup: false, out SaveFile file, out string text);
              if (main == ReadStatus.Newer)
                  return Refuse(LoadOutcome.NewerVersion, "it was written by a newer version of the game");

              LoadOutcome outcome = LoadOutcome.Loaded;
              if (main != ReadStatus.Ok)
              {
                  if (main == ReadStatus.Corrupt)
                      _storage.Quarantine(CurrentSlot, SaveFiles.SAVE, backup: false);

                  ReadStatus backup = ReadSaveFile(backup: true, out file, out text);
                  if (backup == ReadStatus.Newer)
                      return Refuse(LoadOutcome.NewerVersion, "its backup was written by a newer version of the game");

                  if (backup != ReadStatus.Ok)
                  {
                      if (backup == ReadStatus.Corrupt)
                          _storage.Quarantine(CurrentSlot, SaveFiles.SAVE, backup: true);

                      StartEmpty();
                      if (main == ReadStatus.Missing && backup == ReadStatus.Missing)
                          return LoadOutcome.Empty;

                      Debug.LogWarning($"Save slot {CurrentSlot} is corrupted, starting over; the broken files were " +
                                       $"renamed to *{SaveFiles.CORRUPT_SUFFIX}.");
                      return LoadOutcome.Corrupted;
                  }

                  outcome = LoadOutcome.Recovered;
              }

              var prepared = new List<(IGameSerializer Serializer, JToken Data)>();
              bool migrated = false;
              foreach (IGameSerializer serializer in _serializers)
              {
                  if (!file.Sections.TryGetValue(serializer.Key, out SaveSection section))
                      continue;

                  SectionStatus status = SerializerGroup.Prepare(serializer, section, out JToken data, out string problem);
                  if (status == SectionStatus.NewerVersion)
                      return Refuse(LoadOutcome.NewerVersion, problem);

                  if (status == SectionStatus.MigrationFailed)
                      return Refuse(LoadOutcome.MigrationFailed, problem);

                  migrated |= section.V < serializer.Version;
                  prepared.Add((serializer, data));
              }

              if (outcome == LoadOutcome.Recovered)
                  Debug.LogWarning($"Save slot {CurrentSlot} was restored from its backup " +
                                   $"({SaveFiles.SAVE}{SaveFiles.BACKUP_SUFFIX}).");

              if (migrated)
                  KeepPremigrationCopy(text);

              ReadMeta();
              _sections = new Dictionary<string, SaveSection>(file.Sections);
              // After a recovery the main file is gone: the next save must write it even if nothing changed
              _lastText = outcome == LoadOutcome.Recovered ? null : text;
              IsReadOnly = false;
              SerializerGroup.RestoreInOrder(prepared);
              return outcome;
          }

          private ReadStatus ReadSaveFile(bool backup, out SaveFile file, out string text)
          {
              file = null;
              bool found = backup
                  ? _storage.TryReadBackup(CurrentSlot, SaveFiles.SAVE, out text)
                  : _storage.TryRead(CurrentSlot, SaveFiles.SAVE, out text);

              if (!found)
                  return ReadStatus.Missing;

              // The version goes first: a newer format may have a shape this build cannot parse
              if (!SaveJson.TryParseObject(text, out JObject json) || json["formatVersion"]?.Type != JTokenType.Integer)
                  return ReadStatus.Corrupt;

              int formatVersion = (int)json["formatVersion"];
              if (formatVersion > SaveFile.CURRENT_FORMAT_VERSION)
                  return ReadStatus.Newer;

              if (formatVersion < 1)
                  return ReadStatus.Corrupt;

              try
              {
                  file = json.ToObject<SaveFile>(SaveJson.Serializer);
              }
              catch (Exception)
              {
                  return ReadStatus.Corrupt;
              }

              if (file?.Sections == null)
                  return ReadStatus.Corrupt;

              foreach (SaveSection section in file.Sections.Values)
              {
                  if (section == null)
                      return ReadStatus.Corrupt;
              }

              return ReadStatus.Ok;
          }

          /// <summary>Play time and summary from meta.json; a missing or broken meta starts them from zero.</summary>
          private void ReadMeta()
          {
              _summary.Clear();
              PlayTime = 0;

              string text;
              try
              {
                  if (!_storage.TryRead(CurrentSlot, SaveFiles.META, out text))
                      return;
              }
              catch (Exception exception)
              {
                  Debug.LogWarning($"{SaveFiles.META} of save slot {CurrentSlot} is unreadable: {exception.Message}");
                  return;
              }

              if (!SaveJson.TryRead(text, out SlotMeta meta))
              {
                  Debug.LogWarning($"{SaveFiles.META} of save slot {CurrentSlot} is unreadable; play time and summary " +
                                   "start from zero.");
                  return;
              }

              PlayTime = Math.Max(0, meta.PlayTimeSeconds);
              if (meta.Summary == null)
                  return;

              foreach (KeyValuePair<string, string> pair in meta.Summary)
                  _summary[pair.Key] = pair.Value;
          }

          private void ApplyOnRegister(IGameSerializer serializer, SaveSection section)
          {
              bool wasLoading = IsLoading;
              IsLoading = true;
              try
              {
                  SectionStatus status = SerializerGroup.Prepare(serializer, section, out JToken data, out string problem);
                  if (status != SectionStatus.Ok)
                  {
                      IsReadOnly = true;
                      Debug.LogError($"Save slot {CurrentSlot} becomes read-only: {problem}.");
                      return;
                  }

                  SerializerGroup.RestoreInOrder(new[] { (serializer, data) });
              }
              finally
              {
                  IsLoading = wasLoading;
              }
          }

          private LoadOutcome Refuse(LoadOutcome outcome, string problem)
          {
              IsReadOnly = true;
              Debug.LogError($"Save slot {CurrentSlot} is not loaded and will not be overwritten: {problem}.");
              return outcome;
          }

          private void KeepPremigrationCopy(string text)
          {
              try
              {
                  if (!_storage.TryRead(CurrentSlot, SaveFiles.PREMIGRATION, out _))
                      _storage.Write(CurrentSlot, SaveFiles.PREMIGRATION, text);
              }
              catch (Exception exception)
              {
                  Debug.LogWarning($"Could not keep the pre-migration copy of save slot {CurrentSlot}: {exception.Message}");
              }
          }

          private void StartEmpty()
          {
              _sections = new Dictionary<string, SaveSection>();
              _summary.Clear();
              PlayTime = 0;
              _lastText = _emptyText;
              IsReadOnly = false;
          }

          private static void ValidateSlot(int slot)
          {
              if (slot < 0)
                  throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slots start at 0.");
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — зелёный.** EditMode: `failed 0` (все тесты задач 1–6).

- [ ] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add save slots and slot metadata"
  ```

---

### Задача 7: компонент `SaveLoadSystem` и меню

**Files:**
- Create: `Runtime/Core/SaveLoadSystem.cs`
- Create: `Editor/SaveSystem.Editor.asmdef`, `Editor/SaveSystemMenu.cs`
- Create: `Tests/Runtime/SaveSystem.Tests.Runtime.asmdef`
- Modify: `Tests/Editor/SaveSystem.Tests.asmdef` (+ ссылка `SaveSystem.Editor`)
- Test: `Tests/Runtime/SaveLoadSystemTests.cs`, `Tests/Editor/SaveSystemMenuTests.cs`

**Interfaces:**
- Consumes: `SaveLoadService`, `ISaveLoadService`, `FileSaveStorage`, `InMemorySaveStorage` (задачи 3–6).
- Produces: `SaveLoadSystem : MonoBehaviour, ISaveLoadService` с `DEFAULT_FOLDER = "Saves"`,
  `MIN_AUTO_SAVE_PERIOD = 5f`, `UseStorage(ISaveStorage)`, `Configure(int startupSlot = 0, bool loadOnStart = false,
  bool saveOnQuit = true, bool saveOnPause = true, float autoSavePeriod = 30f)`, `Tick(float deltaTime)`;
  `SaveSystemMenu.SavesFolder`.

- [x] **Шаг 1: сборки.**

  `Editor/SaveSystem.Editor.asmdef`:

  ```json
  {
      "name": "SaveSystem.Editor",
      "rootNamespace": "Saving.Editor",
      "references": [
          "SaveSystem"
      ],
      "includePlatforms": [
          "Editor"
      ],
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

  `Tests/Runtime/SaveSystem.Tests.Runtime.asmdef`:

  ```json
  {
      "name": "SaveSystem.Tests.Runtime",
      "rootNamespace": "Saving.Tests",
      "references": [
          "SaveSystem",
          "UnityEngine.TestRunner",
          "UnityEditor.TestRunner"
      ],
      "includePlatforms": [],
      "excludePlatforms": [],
      "allowUnsafeCode": false,
      "overrideReferences": true,
      "precompiledReferences": [
          "nunit.framework.dll",
          "Newtonsoft.Json.dll"
      ],
      "autoReferenced": false,
      "defineConstraints": [
          "UNITY_INCLUDE_TESTS"
      ],
      "versionDefines": [],
      "noEngineReferences": false
  }
  ```

  В `Tests/Editor/SaveSystem.Tests.asmdef` в `references` после `"SaveSystem",` добавить `"SaveSystem.Editor",`.

- [x] **Шаг 2: заглушки.**

  `Runtime/Core/SaveLoadSystem.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  namespace Saving
  {
      [DefaultExecutionOrder(-9000)]
      public sealed class SaveLoadSystem : MonoBehaviour, ISaveLoadService
      {
          public const string DEFAULT_FOLDER = "Saves";
          public const float MIN_AUTO_SAVE_PERIOD = 5f;

          public void UseStorage(ISaveStorage storage) => throw new NotImplementedException();

          public void Configure(int startupSlot = 0, bool loadOnStart = false, bool saveOnQuit = true,
              bool saveOnPause = true, float autoSavePeriod = 30f) => throw new NotImplementedException();

          public void Tick(float deltaTime) => throw new NotImplementedException();

          public int CurrentSlot => throw new NotImplementedException();
          public bool IsLoading => throw new NotImplementedException();
          public bool IsReadOnly => throw new NotImplementedException();
          public double PlayTime => throw new NotImplementedException();
          public IDictionary<string, string> Summary => throw new NotImplementedException();

          public event Action<SaveOutcome> Saved
          {
              add => throw new NotImplementedException();
              remove => throw new NotImplementedException();
          }

          public event Action<LoadOutcome> Loaded
          {
              add => throw new NotImplementedException();
              remove => throw new NotImplementedException();
          }

          public void Register(IGameSerializer serializer) => throw new NotImplementedException();
          public void Unregister(IGameSerializer serializer) => throw new NotImplementedException();
          public void Capture() => throw new NotImplementedException();
          public SaveOutcome Save(bool onlyIfChanged = false) => throw new NotImplementedException();
          public LoadOutcome Load() => throw new NotImplementedException();
          public void SelectSlot(int slot, bool keepData = false) => throw new NotImplementedException();
          public IReadOnlyList<SlotInfo> GetSlots() => throw new NotImplementedException();
          public void DeleteSlot(int slot) => throw new NotImplementedException();
      }
  }
  ```

  `Editor/SaveSystemMenu.cs`:

  ```csharp
  namespace Saving.Editor
  {
      public static class SaveSystemMenu
      {
          public static string SavesFolder => throw new System.NotImplementedException();
      }
  }
  ```

- [x] **Шаг 3: падающие тесты.**

  `Tests/Runtime/SaveLoadSystemTests.cs`:

  ```csharp
  using System;
  using System.Collections;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;
  using Object = UnityEngine.Object;

  namespace Saving.Tests
  {
      /// <summary>The component: autosave on game time, saves on pause, focus loss and quit, startup load.</summary>
      public class SaveLoadSystemTests
      {
          private sealed class Counter
          {
              public int Value;
          }

          private sealed class CounterData
          {
              public int Value;
          }

          private sealed class CounterSerializer : GameSerializer<Counter, CounterData>
          {
              public CounterSerializer(Counter counter) : base(counter, "counter")
              {
              }

              protected override CounterData Serialize(Counter counter) => new() { Value = counter.Value };
              protected override void Deserialize(Counter counter, CounterData data) => counter.Value = data.Value;
          }

          private InMemorySaveStorage _storage;
          private GameObject _object;

          [SetUp]
          public void SetUp() => _storage = new InMemorySaveStorage();

          [TearDown]
          public void TearDown()
          {
              if (_object != null)
                  Object.DestroyImmediate(_object);
          }

          private SaveLoadSystem Create(int startupSlot = 0, bool loadOnStart = false, bool saveOnPause = true,
              float autoSavePeriod = 10f)
          {
              _object = new GameObject("SaveLoadSystem");
              var system = _object.AddComponent<SaveLoadSystem>();
              system.UseStorage(_storage);
              system.Configure(startupSlot, loadOnStart, saveOnQuit: true, saveOnPause, autoSavePeriod);
              return system;
          }

          /// <summary>A system with something to save, so onlyIfChanged has a reason to write.</summary>
          private SaveLoadSystem CreateWithData(bool saveOnPause = true, float autoSavePeriod = 10f)
          {
              SaveLoadSystem system = Create(saveOnPause: saveOnPause, autoSavePeriod: autoSavePeriod);
              system.Register(new CounterSerializer(new Counter { Value = 1 }));
              return system;
          }

          private bool Saved() => _storage.Has(0, SaveFiles.SAVE);

          [Test]
          public void SaveLoadSystem_TickBeforePeriod_DoesNotSave()
          {
              CreateWithData().Tick(9f);

              Assert.IsFalse(Saved());
          }

          [Test]
          public void SaveLoadSystem_TickPastPeriod_Saves()
          {
              SaveLoadSystem system = CreateWithData();

              system.Tick(9f);
              system.Tick(1.5f);

              Assert.IsTrue(Saved());
          }

          [Test]
          public void SaveLoadSystem_ZeroDelta_TimerAndPlayTimeWait()
          {
              SaveLoadSystem system = CreateWithData();

              for (int i = 0; i < 100; i++)
                  system.Tick(0f);

              Assert.IsFalse(Saved());
              Assert.AreEqual(0, system.PlayTime);
          }

          [Test]
          public void SaveLoadSystem_Tick_AddsPlayTime()
          {
              SaveLoadSystem system = CreateWithData();

              system.Tick(2.5f);

              Assert.AreEqual(2.5, system.PlayTime, 1e-6);
          }

          [Test]
          public void SaveLoadSystem_AutoSaveOff_NeverSaves()
          {
              CreateWithData(autoSavePeriod: 0f).Tick(1000f);

              Assert.IsFalse(Saved());
          }

          [Test]
          public void SaveLoadSystem_ShortPeriod_ClampedToMinimum()
          {
              SaveLoadSystem system = CreateWithData(autoSavePeriod: 1f);

              system.Tick(SaveLoadSystem.MIN_AUTO_SAVE_PERIOD - 0.5f);

              Assert.IsFalse(Saved());
          }

          [Test]
          public void SaveLoadSystem_ApplicationPause_Saves()
          {
              CreateWithData();

              _object.SendMessage("OnApplicationPause", true);

              Assert.IsTrue(Saved());
          }

          [Test]
          public void SaveLoadSystem_SaveOnPauseOff_PauseDoesNotSave()
          {
              CreateWithData(saveOnPause: false);

              _object.SendMessage("OnApplicationPause", true);

              Assert.IsFalse(Saved());
          }

          [Test]
          public void SaveLoadSystem_FocusLost_Saves()
          {
              CreateWithData();

              _object.SendMessage("OnApplicationFocus", false);

              Assert.IsTrue(Saved());
          }

          [Test]
          public void SaveLoadSystem_Quit_Saves()
          {
              CreateWithData();

              _object.SendMessage("OnApplicationQuit");

              Assert.IsTrue(Saved());
          }

          [UnityTest]
          public IEnumerator SaveLoadSystem_LoadOnStart_LoadsStartupSlot()
          {
              var writer = new SaveLoadService(_storage, "test");
              writer.SelectSlot(2);
              writer.Register(new CounterSerializer(new Counter { Value = 7 }));
              writer.Save();

              SaveLoadSystem system = Create(startupSlot: 2, loadOnStart: true);
              var counter = new Counter();
              system.Register(new CounterSerializer(counter));

              yield return null; // Start

              Assert.AreEqual(2, system.CurrentSlot);
              Assert.AreEqual(7, counter.Value);
          }

          [Test]
          public void SaveLoadSystem_UseStorageAfterStart_Throws()
          {
              SaveLoadSystem system = Create();
              _ = system.CurrentSlot; // starts the service

              Assert.Throws<InvalidOperationException>(() => system.UseStorage(new InMemorySaveStorage()));
          }
      }
  }
  ```

  `Tests/Editor/SaveSystemMenuTests.cs`:

  ```csharp
  using System.IO;
  using NUnit.Framework;
  using Saving.Editor;
  using UnityEngine;

  namespace Saving.Tests
  {
      public class SaveSystemMenuTests
      {
          [Test]
          public void SaveSystemMenu_SavesFolder_IsDefaultFolderUnderPersistentDataPath() =>
              Assert.AreEqual(Path.Combine(Application.persistentDataPath, SaveLoadSystem.DEFAULT_FOLDER),
                  SaveSystemMenu.SavesFolder);
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** All Tests: падают `SaveSystemMenuTests` (EditMode) и все `SaveLoadSystemTests`
  (PlayMode) с `NotImplementedException`.

- [x] **Шаг 5: реализация.**

  `Runtime/Core/SaveLoadSystem.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.IO;
  using UnityEngine;

  namespace Saving
  {
      /// <summary>
      /// The save system as a component, made for the [Services] object. Owns the file storage under
      /// persistentDataPath, selects the startup slot and loads it if asked, and saves on quit, on app pause or focus
      /// loss and on an autosave timer that runs on game time (only when something changed). The service starts on
      /// first use: <see cref="UseStorage"/> and <see cref="Configure"/> go before it.
      /// </summary>
      [DefaultExecutionOrder(-9000)]
      public sealed class SaveLoadSystem : MonoBehaviour, ISaveLoadService
      {
          public const string DEFAULT_FOLDER = "Saves";
          public const float MIN_AUTO_SAVE_PERIOD = 5f;

          [SerializeField] private string _folder = DEFAULT_FOLDER;
          [SerializeField] private int _startupSlot;
          [SerializeField] private bool _loadOnStart;
          [SerializeField] private bool _saveOnQuit = true;
          [Tooltip("Save on app pause and on focus loss: on mobile and web it can be the last chance")]
          [SerializeField] private bool _saveOnPause = true;
          [Tooltip("Seconds of game time between autosaves, only when something changed; 0 turns autosave off")]
          [SerializeField] private float _autoSavePeriod = 30f;

          private ISaveStorage _storage;
          private SaveLoadService _service;
          private float _untilAutoSave;

          private SaveLoadService Service => _service ??= CreateService();

          // ---------- Setup ----------

          /// <summary>Replaces the file storage (tests, other platforms). Only before the service starts.</summary>
          public void UseStorage(ISaveStorage storage)
          {
              EnsureNotStarted(nameof(UseStorage));
              _storage = storage ?? throw new ArgumentNullException(nameof(storage));
          }

          /// <summary>Sets the component up from code (tests). Only before the service starts.</summary>
          public void Configure(int startupSlot = 0, bool loadOnStart = false, bool saveOnQuit = true,
              bool saveOnPause = true, float autoSavePeriod = 30f)
          {
              EnsureNotStarted(nameof(Configure));
              _startupSlot = startupSlot;
              _loadOnStart = loadOnStart;
              _saveOnQuit = saveOnQuit;
              _saveOnPause = saveOnPause;
              _autoSavePeriod = autoSavePeriod;
              OnValidate();
              _untilAutoSave = _autoSavePeriod;
          }

          /// <summary>Counts play time and runs the autosave timer; Update passes game time, so a pause waits.</summary>
          public void Tick(float deltaTime)
          {
              if (deltaTime <= 0f)
                  return;

              Service.AddPlayTime(deltaTime);

              if (_autoSavePeriod <= 0f)
                  return;

              _untilAutoSave -= deltaTime;
              if (_untilAutoSave > 0f)
                  return;

              _untilAutoSave = _autoSavePeriod;
              Save(onlyIfChanged: true);
          }

          // ---------- ISaveLoadService ----------

          public int CurrentSlot => Service.CurrentSlot;
          public bool IsLoading => Service.IsLoading;
          public bool IsReadOnly => Service.IsReadOnly;
          public double PlayTime => Service.PlayTime;
          public IDictionary<string, string> Summary => Service.Summary;

          public event Action<SaveOutcome> Saved
          {
              add => Service.Saved += value;
              remove => Service.Saved -= value;
          }

          public event Action<LoadOutcome> Loaded
          {
              add => Service.Loaded += value;
              remove => Service.Loaded -= value;
          }

          public void Register(IGameSerializer serializer) => Service.Register(serializer);
          public void Unregister(IGameSerializer serializer) => Service.Unregister(serializer);
          public void Capture() => Service.Capture();
          public SaveOutcome Save(bool onlyIfChanged = false) => Service.Save(onlyIfChanged);
          public LoadOutcome Load() => Service.Load();
          public void SelectSlot(int slot, bool keepData = false) => Service.SelectSlot(slot, keepData);
          public IReadOnlyList<SlotInfo> GetSlots() => Service.GetSlots();
          public void DeleteSlot(int slot) => Service.DeleteSlot(slot);

          // ---------- Unity ----------

          private void Awake() => _untilAutoSave = _autoSavePeriod;

          private void Start()
          {
              if (_loadOnStart)
                  Load();
          }

          private void Update() => Tick(Time.deltaTime);

          private void OnApplicationPause(bool paused)
          {
              if (paused && _saveOnPause)
                  Save(onlyIfChanged: true);
          }

          private void OnApplicationFocus(bool focused)
          {
              if (!focused && _saveOnPause)
                  Save(onlyIfChanged: true);
          }

          private void OnApplicationQuit()
          {
              if (_saveOnQuit)
                  Save();
          }

          // Instead of [Min]: the old Odin in the project breaks drawing [Min] in Unity 6.5
          private void OnValidate()
          {
              if (string.IsNullOrWhiteSpace(_folder))
                  _folder = DEFAULT_FOLDER;

              _startupSlot = Mathf.Max(0, _startupSlot);
              _autoSavePeriod = _autoSavePeriod <= 0f ? 0f : Mathf.Max(MIN_AUTO_SAVE_PERIOD, _autoSavePeriod);
          }

          private SaveLoadService CreateService()
          {
              _storage ??= new FileSaveStorage(Path.Combine(Application.persistentDataPath, _folder));
              var service = new SaveLoadService(_storage, Application.version);
              service.SelectSlot(_startupSlot);
              return service;
          }

          private void EnsureNotStarted(string method)
          {
              if (_service != null)
                  throw new InvalidOperationException($"{method} must be called before the save system starts.");
          }
      }
  }
  ```

  `Editor/SaveSystemMenu.cs`:

  ```csharp
  using System.IO;
  using UnityEditor;
  using UnityEngine;

  namespace Saving.Editor
  {
      /// <summary>Tools → Save System: open the saves folder, delete every save.</summary>
      public static class SaveSystemMenu
      {
          public static string SavesFolder => Path.Combine(Application.persistentDataPath, SaveLoadSystem.DEFAULT_FOLDER);

          [MenuItem("Tools/Save System/Open Saves Folder")]
          public static void OpenSavesFolder()
          {
              Directory.CreateDirectory(SavesFolder);
              EditorUtility.RevealInFinder(SavesFolder);
          }

          [MenuItem("Tools/Save System/Delete All Saves")]
          public static void DeleteAllSaves()
          {
              if (!Directory.Exists(SavesFolder))
              {
                  Debug.Log("There are no saves to delete.");
                  return;
              }

              string warning = EditorApplication.isPlaying
                  ? "\n\nPlay Mode is running: the save system keeps its snapshot in memory and writes the slot again on the next save."
                  : string.Empty;

              if (!EditorUtility.DisplayDialog("Delete All Saves", $"Delete every save in\n{SavesFolder}?{warning}",
                      "Delete", "Cancel"))
                  return;

              Directory.Delete(SavesFolder, recursive: true);
              Debug.Log($"Deleted all saves in {SavesFolder}.");
          }
      }
  }
  ```

- [x] **Шаг 6: запуск — зелёный.** All Tests: EditMode и PlayMode `failed 0`. Вручную: *Tools → Save System → Open
  Saves Folder* открывает `…/LocalLow/DefaultCompany/Lesson/Saves`.

- [ ] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Add SaveLoadSystem component and saves menu"
  ```

---

### Задача 8: подключение к игре

**Files:**
- Modify: `Assets/Game/Game.asmdef`, `Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef`
- Modify: `Assets/Game/Scripts/GameSystem/Services/GameServices.cs`
- Create: `Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs`
- Modify: `Assets/Game/Prefabs/Services.prefab` (через MCP)
- Modify: `Assets/Game/Tests/PlayMode/GameServicesTests.cs`
- Test: `Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs`, `Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs`

**Interfaces:**
- Consumes: `ISaveLoadService`, `SaveLoadSystem`, `InMemorySaveStorage` (задачи 3–7); `ServiceLocator`.
- Produces: `ServiceLocator.Get<ISaveLoadService>()` в игре; `SaveDebugHotkeys.Poll(Keyboard)`.

- [x] **Шаг 1: ссылки сборок.** `Assets/Game/Game.asmdef`:

  ```json
  {
    "name": "Game",
    "references":[ "SpriteAnimator", "AudioEngine", "PrefabPool", "ServiceLocator", "SaveSystem", "Unity.InputSystem", "Unity.Cinemachine" ]
  }
  ```

  В `Game.Tests.PlayMode.asmdef` в `references` после `"ServiceLocator",` добавить `"SaveSystem",`.

- [x] **Шаг 2: падающие тесты регистрации.** В `GameServicesTests.cs`: `using Saving;` в начало; метод `Spawn`
  заменить на версию, где тесты не трогают настоящую папку сейвов:

  ```csharp
          private GameObject Spawn()
          {
              GameObject instance = Object.Instantiate(_prefab);
              _instances.Add(instance);

              // Tests never touch the real saves folder; the copy is inactive, so include inactive objects
              foreach (SaveLoadSystem saves in instance.GetComponentsInChildren<SaveLoadSystem>(includeInactive: true))
                  saves.UseStorage(new InMemorySaveStorage());

              return instance;
          }
  ```

  Новый тест в класс:

  ```csharp
          [Test]
          public void GameServices_Awake_RegistersSaveLoadSystem()
          {
              GameObject services = Spawn();

              Assert.AreSame(services.GetComponentInChildren<SaveLoadSystem>(), ServiceLocator.Get<ISaveLoadService>());
          }
  ```

  В `GameServices_Destroyed_UnregistersServices` перед последней проверкой добавить
  `Assert.IsFalse(ServiceLocator.TryGet(out ISaveLoadService _));`.

- [x] **Шаг 3: запуск — красный.** PlayMode: `GameServices_Awake_RegistersSaveLoadSystem` падает
  (`Assert.AreSame`: `null` у `GetComponentInChildren` и `InvalidOperationException` у `Get` — системы в префабе нет).

- [x] **Шаг 4: регистрация в `GameServices`.** В `GameServices.cs`: `using Saving;`; поле после `_audioSystem`:

  ```csharp
          [SerializeField] private SaveLoadSystem _saveLoadSystem;
  ```

  в `Awake` после регистрации звука:

  ```csharp
              ServiceLocator.Register<ISaveLoadService>(_saveLoadSystem);
  ```

  в `OnDestroy` первой строкой:

  ```csharp
              ServiceLocator.Unregister<ISaveLoadService>(_saveLoadSystem);
  ```

  В `<summary>` класса после «registers game-wide services in the ServiceLocator» дописать: «(audio, saves)».

- [x] **Шаг 5: горячие клавиши** — `Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs` (пишется сейчас, чтобы
  префаб правился один раз; тесты — шаг 8):

  ```csharp
  using Saving;
  using ServiceLocation;
  using UnityEngine;
  using UnityEngine.InputSystem;

  namespace Gameplay
  {
      /// <summary>
      /// Debug keys of the save system on [Services]: F5 saves the current slot, F9 loads it. Only in the editor and
      /// development builds; in a release build the component does nothing.
      /// </summary>
      public sealed class SaveDebugHotkeys : MonoBehaviour
      {
  #if UNITY_EDITOR || DEVELOPMENT_BUILD
          private void Update() => Poll(Keyboard.current);

          /// <summary>Checks the keys pressed this frame; public for tests with a virtual keyboard.</summary>
          public void Poll(Keyboard keyboard)
          {
              if (keyboard == null || !ServiceLocator.TryGet(out ISaveLoadService saves))
                  return;

              if (keyboard.f5Key.wasPressedThisFrame)
                  saves.Save();
              else if (keyboard.f9Key.wasPressedThisFrame)
                  saves.Load();
          }
  #endif
      }
  }
  ```

- [x] **Шаг 6: префаб `[Services]`.** `Refresh`, консоль без ошибок, затем `Unity_RunCommand` (правка ассета
  Undo не откатывается — откат через git):

  ```csharp
  using System;
  using Gameplay;
  using Saving;
  using UnityEditor;
  using UnityEngine;

  internal class CommandScript : IRunCommand
  {
      private const string PREFAB_PATH = "Assets/Game/Prefabs/Services.prefab";

      public void Execute(ExecutionResult result)
      {
          GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
          try
          {
              if (root.GetComponentInChildren<SaveLoadSystem>(true) != null)
                  throw new InvalidOperationException("The prefab already has a SaveLoadSystem");

              var child = new GameObject("SaveLoadSystem");
              child.transform.SetParent(root.transform, false);
              var system = child.AddComponent<SaveLoadSystem>();

              var systemObject = new SerializedObject(system);
              systemObject.FindProperty("_loadOnStart").boolValue = true;
              systemObject.ApplyModifiedPropertiesWithoutUndo();

              var services = new SerializedObject(root.GetComponent<GameServices>());
              services.FindProperty("_saveLoadSystem").objectReferenceValue = system;
              services.ApplyModifiedPropertiesWithoutUndo();

              root.AddComponent<SaveDebugHotkeys>();

              PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
              result.Log("{0}: SaveLoadSystem child and SaveDebugHotkeys added", PREFAB_PATH);
          }
          finally
          {
              PrefabUtility.UnloadPrefabContents(root);
          }
      }
  }
  ```

  Если команда не видит типы — `child.AddComponent(Type.GetType("Saving.SaveLoadSystem, SaveSystem"))` и
  `root.AddComponent(Type.GetType("Gameplay.SaveDebugHotkeys, Game"))`. Проверить: в префабе дочерний
  `SaveLoadSystem` (загрузка при старте — да), в `GameServices` назначен `_saveLoadSystem`, на корне
  `SaveDebugHotkeys`; сцены с экземпляром `[Services]` получили это сами (переопределений в них нет).

- [x] **Шаг 7: запуск — зелёный (регистрация).** PlayMode: все `GameServicesTests` проходят.

- [x] **Шаг 8: тесты клавиш.**

  `Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Saving;

  namespace Gameplay.Tests
  {
      /// <summary>ISaveLoadService that only counts Save and Load calls.</summary>
      public sealed class FakeSaveLoadService : ISaveLoadService
      {
          public int SaveCalls { get; private set; }
          public int LoadCalls { get; private set; }

          public int CurrentSlot => 0;
          public bool IsLoading => false;
          public bool IsReadOnly => false;
          public double PlayTime => 0;
          public IDictionary<string, string> Summary { get; } = new Dictionary<string, string>();

          public event Action<SaveOutcome> Saved;
          public event Action<LoadOutcome> Loaded;

          public void Register(IGameSerializer serializer)
          {
          }

          public void Unregister(IGameSerializer serializer)
          {
          }

          public void Capture()
          {
          }

          public SaveOutcome Save(bool onlyIfChanged = false)
          {
              SaveCalls++;
              Saved?.Invoke(SaveOutcome.Saved);
              return SaveOutcome.Saved;
          }

          public LoadOutcome Load()
          {
              LoadCalls++;
              Loaded?.Invoke(LoadOutcome.Loaded);
              return LoadOutcome.Loaded;
          }

          public void SelectSlot(int slot, bool keepData = false)
          {
          }

          public IReadOnlyList<SlotInfo> GetSlots() => Array.Empty<SlotInfo>();

          public void DeleteSlot(int slot)
          {
          }
      }
  }
  ```

  `Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs`:

  ```csharp
  using NUnit.Framework;
  using Saving;
  using ServiceLocation;
  using UnityEngine;
  using UnityEngine.InputSystem;
  using Object = UnityEngine.Object;

  namespace Gameplay.Tests
  {
      /// <summary>F5 saves, F9 loads through the ServiceLocator; nothing breaks without the service.</summary>
      public class SaveDebugHotkeysTests
      {
          private InputTestFixture _input;
          private Keyboard _keyboard;
          private FakeSaveLoadService _saves;
          private SaveDebugHotkeys _hotkeys;

          [SetUp]
          public void SetUp()
          {
              ServiceLocator.Clear();
              _input = new InputTestFixture();
              _input.Setup();
              _keyboard = InputSystem.AddDevice<Keyboard>();
              _saves = new FakeSaveLoadService();
              _hotkeys = new GameObject("SaveDebugHotkeys").AddComponent<SaveDebugHotkeys>();
          }

          [TearDown]
          public void TearDown()
          {
              Object.DestroyImmediate(_hotkeys.gameObject);
              _input.TearDown();
              ServiceLocator.Clear();
          }

          [Test]
          public void SaveDebugHotkeys_F5_Saves()
          {
              ServiceLocator.Register<ISaveLoadService>(_saves);

              _input.Press(_keyboard.f5Key);
              _hotkeys.Poll(_keyboard);

              Assert.AreEqual(1, _saves.SaveCalls);
              Assert.AreEqual(0, _saves.LoadCalls);
          }

          [Test]
          public void SaveDebugHotkeys_F9_Loads()
          {
              ServiceLocator.Register<ISaveLoadService>(_saves);

              _input.Press(_keyboard.f9Key);
              _hotkeys.Poll(_keyboard);

              Assert.AreEqual(1, _saves.LoadCalls);
              Assert.AreEqual(0, _saves.SaveCalls);
          }

          [Test]
          public void SaveDebugHotkeys_HeldKey_SavesOnce()
          {
              ServiceLocator.Register<ISaveLoadService>(_saves);

              _input.Press(_keyboard.f5Key);
              _hotkeys.Poll(_keyboard);
              InputSystem.Update();
              _hotkeys.Poll(_keyboard);

              Assert.AreEqual(1, _saves.SaveCalls);
          }

          [Test]
          public void SaveDebugHotkeys_NoService_DoesNothing()
          {
              _input.Press(_keyboard.f5Key);

              Assert.DoesNotThrow(() => _hotkeys.Poll(_keyboard));
          }
      }
  }
  ```

  Эти тесты сразу зелёные (клавиши написаны на шаге 5). Проверить, что они ловят ошибку: временно поменять в
  `Poll` `f5Key` на `f6Key` — `F5_Saves` и `HeldKey_SavesOnce` падают; вернуть `f5Key`.

- [x] **Шаг 9: запуск — зелёный.** All Tests: `failed 0`. Вручную в `Game.unity`: Play → F5 → в
  `…/LocalLow/DefaultCompany/Lesson/Saves/slot_0/` появились `save.json` и `meta.json`; F9 — без ошибок в консоли;
  выход из Play Mode сохраняет слот (время в `meta.json` обновилось).

- [ ] **Шаг 10: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Game.asmdef Assets/Game/Scripts/GameSystem/Services/GameServices.cs Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs Assets/Game/Scripts/GameSystem/Services/SaveDebugHotkeys.cs.meta Assets/Game/Prefabs/Services.prefab Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef Assets/Game/Tests/PlayMode/GameServicesTests.cs Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs Assets/Game/Tests/PlayMode/FakeSaveLoadService.cs.meta Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs Assets/Game/Tests/PlayMode/SaveDebugHotkeysTests.cs.meta
  git commit -m "Register the save system in game services"
  ```

  Если в сценах с `[Services]` появились изменения (`git status`) — показать пользователю, прежде чем коммитить:
  сцены `Sandbox_Locomotion.unity` и `Sandbox_PixelBaker.unity` содержат чужие правки.

---

### Задача 9: документация и итоговый прогон

**Files:**
- Create: `Assets/Game/Scripts/Modules/SaveSystem/README.md`
- Modify: `Assets/AI/CLAUDE.md`, `Assets/AI/Docs/SaveSystem.md`, этот план (отметить шаги)

- [x] **Шаг 1: README модуля** — `Assets/Game/Scripts/Modules/SaveSystem/README.md`:

  ````markdown
  # Save System

  Сохранения для Unity: слоты с атомарной записью и резервной копией, версии и миграции данных, глобальные системы
  через `GameSerializer<TService, TData>`, компонент `SaveLoadSystem` с автосейвом. Модуль самостоятельный: не
  зависит от игры, нужен только Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`). Проверено на Unity 6000.5.

  Это ядро (часть 1). Сохранение объектов сцены компонентом `Saveable` — часть 2, см. спек `Assets/AI/Docs/SaveSystem.md`.

  ## Состав

  ```
  SaveSystem/
  ├── Runtime/Core/   (сборка SaveSystem, namespace Saving)
  │   ├── SaveLoadSystem     — компонент: настройки, слот при старте, автосейв, пауза, выход
  │   ├── SaveLoadService    — ядро: снимок слота, Register/Save/Load, слоты, meta.json
  │   ├── GameSerializer     — типизированный сериализатор глобальной системы; IGameSerializer — его контракт
  │   ├── FileSaveStorage    — слоты на диске; InMemorySaveStorage — в памяти (тесты)
  │   └── SaveJson           — общие настройки Newtonsoft и конвертеры Unity-структур
  ├── Editor/         (сборка SaveSystem.Editor) — меню Tools → Save System
  └── Tests/          Editor (SaveSystem.Tests), Runtime (SaveSystem.Tests.Runtime)
  ```

  ## Быстрый старт

  1. Компонент `SaveLoadSystem` на объект, который живёт всю игру (у нас — `[Services]`).
  2. Сериализатор на систему, которую надо сохранять:

  ```csharp
  using Saving;

  public sealed class InventorySerializer : GameSerializer<Inventory, InventoryData>
  {
      public InventorySerializer(Inventory inventory) : base(inventory, key: "inventory") { }

      protected override InventoryData Serialize(Inventory inventory) => new() { Items = inventory.ItemIds };
      protected override void Deserialize(Inventory inventory, InventoryData data) => inventory.Set(data.Items);
  }
  ```

  3. Регистрация — когда система появилась, снятие — когда исчезла:

  ```csharp
  saves.Register(new InventorySerializer(inventory)); // gets its data at once if the slot is already loaded
  saves.Save();                                       // checkpoint, menu, F5
  saves.Load();                                       // "Continue", F9
  ```

  ## Файлы на диске

  `<persistentDataPath>/Saves/slot_N/`: `save.json` (данные), `meta.json` (время, время игры, `Summary` — для меню
  слотов), `save.json.bak` (предыдущая запись), `save.premigration.json` (копия до первой миграции), `*.corrupt`
  (испорченные файлы). Запись — через временный файл и `File.Replace`: обрыв не портит сейв.

  ## Версии и миграции

  Секция хранит версию своих данных. Поменялась форма данных — поднять `version` в конструкторе и добавить шаг:

  ```csharp
  protected override JToken Upgrade(JToken data, int fromVersion) => fromVersion switch
  {
      1 => RenameField(data, "ids", "items"), // version 1 -> 2
      _ => base.Upgrade(data, fromVersion)
  };
  ```

  Файл или секция от более новой игры не загружаются и не перезаписываются: слот становится «только для чтения»
  до выбора слота.

  ## Когда сохраняется

  | Событие | Что делает `SaveLoadSystem` |
  |---|---|
  | Выход из игры | `Save()` |
  | Пауза приложения, потеря фокуса | `Save(onlyIfChanged: true)` |
  | Таймер (игровое время, 30 с; 0 — выкл.) | `Save(onlyIfChanged: true)` |
  | Код игры | `Save()`, `Load()` |

  ## Ошибки

  `Load()` возвращает `Loaded`, `Recovered` (данные из `.bak`), `Empty`, `Corrupted` (новая игра, файлы в
  `.corrupt`), `NewerVersion`, `MigrationFailed`, `Failed` (последние три — ничего не применено, слот только для
  чтения). `Save()` — `Saved`, `Unchanged`, `Blocked`, `Failed` (файл на диске — прежний). Сбой одного сериализатора
  пишется в консоль и не мешает остальным.

  ## Тесты

  `SaveSystem.Tests` (EditMode): формат JSON, сериализаторы, хранилища (один контракт на диск и память), сервис,
  золотой файл версии 1. `SaveSystem.Tests.Runtime` (PlayMode): компонент `SaveLoadSystem`.
  ````

- [x] **Шаг 2: `Assets/AI/CLAUDE.md`.** Если в файле есть чужие незакоммиченные правки (`git diff Assets/AI/CLAUDE.md`)
  — показать пользователю и договориться, прежде чем править и коммитить. Правки:
  - после абзаца «Этап 4» — новый абзац:
    «**Сохранения — [Docs/SaveSystem.md](Docs/SaveSystem.md)** (исследование — [Docs/Research_SaveSystems.md](Docs/Research_SaveSystems.md)):
    модуль `Modules/SaveSystem`. Часть 1 (ядро: слоты, `.bak`, версии и миграции, `GameSerializer`, `SaveLoadSystem`
    на `[Services]`, F5/F9) готова; часть 2 (`Saveable`, объекты сцены) — следующая. Ветка `feature/save-system`.»;
  - «Глобальные сервисы (звук; дальше — сейв, настройки)» → «Глобальные сервисы (звук, сохранения; дальше — настройки)»;
  - в «Структура Assets/Game»: `Modules/` — добавить `SaveSystem` в список модулей; `Prefabs/` — «Services
    ([Services]: GameServices, SaveDebugHotkeys, AudioSystem, SaveLoadSystem)»; `Scripts/GameSystem/Services/` — «GameServices: …;
    SaveDebugHotkeys — F5/F9 в эдиторе и dev-сборках»;
  - «Пакеты сверх базовых» — добавить «Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`, нужен `SaveSystem`)»;
  - «Сборки»: у `Game` в ссылках — `SaveSystem`; новый пункт «`SaveSystem` (`Game/Scripts/Modules/SaveSystem/Runtime/Core`) —
    ядро сохранений, ссылается только на Newtonsoft; `SaveSystem.Editor` — меню; `SaveSystem.Tests` (EditMode) и
    `SaveSystem.Tests.Runtime` (PlayMode) — тесты модуля, внутри модуля»; у `Game.Tests.PlayMode` — ещё `SaveSystem`;
  - «Архитектура» — абзац «**Сохранения.** Модуль `Modules/SaveSystem` (namespace `Saving`, описание — его
    `README.md`, спек — [Docs/SaveSystem.md](Docs/SaveSystem.md)). Сервис — `ISaveLoadService` (компонент
    `SaveLoadSystem` на `[Services]`, регистрирует `GameServices`). Глобальная система сохраняется сериализатором
    `GameSerializer<TService, TData>`: регистрация — когда система появилась, снятие — когда исчезла. Ключи с `$` —
    плагина. Формат данных поменялся — поднять версию и написать `Upgrade`; золотые файлы в тестах модуля не
    переписывать, а добавлять новые. Тесты не трогают `persistentDataPath`: хранилище в памяти через `UseStorage`.»;
  - «Стиль кода» — в список namespace модулей добавить `Saving`;
  - «Где тесты» — добавить `Game/Scripts/Modules/SaveSystem/Tests/{Editor,Runtime}`.

- [x] **Шаг 3: спек.** В `Assets/AI/Docs/SaveSystem.md` строку статуса заменить на «**Статус:** часть 1 (ядро)
  готова <дата>, части 2–3 — впереди.» (остальное без изменений); в этом плане отметить выполненные шаги.

- [x] **Шаг 4: итоговый прогон.** Сохранить сцены, All Tests: EditMode и PlayMode `failed 0`. Консоль после
  `Refresh` — без новых предупреждений от `SaveSystem` (известные шумы Odin и `UAC1001` — не наши).

- [ ] **Шаг 5: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem/README.md Assets/Game/Scripts/Modules/SaveSystem/README.md.meta Assets/AI/Docs/SaveSystem.md Assets/AI/Docs/SaveSystem_Plan.md Assets/AI/CLAUDE.md
  git commit -m "Document the save system core"
  ```

  (`CLAUDE.md` — только если договорились на шаге 2.) Пуш — только по просьбе.

---

## Исполнение (2026-10-23)

Выполнено в отдельном worktree `C:\!!!Work\Platformer-SaveSystem` со своим эдитором (основная папка была занята),
а не в основной копии, как предполагал шаг 1 задачи 1. Отличия от плана:

- `feature/enemy-ai` на момент ответвления не компилировался: закоммиченные `LedgeTests` ссылаются на
  `CharacterMovementConfig.LedgeGrabZoneAbove`, которое было только в незакоммиченной правке основной копии. С согласия
  пользователя правка `CharacterMovementConfig.cs` + `LedgeComponent.cs` перенесена сюда отдельным коммитом.
- Задача 5, шаг 4: кроме `LegacyProgress_*` и `GoldenV1_*` на красном прогоне уже проходил
  `OlderSection_MigratesStepByStep` — по той же причине (миграция работает с задачи 4).
- Ручные проверки задач 7–8 сделаны через MCP без нажатия клавиш: путь `SaveSystemMenu.SavesFolder`,
  `Save()`/`Load()` через `ServiceLocator` в Play Mode `Game.unity` (появились `save.json`/`meta.json`), выход из
  Play Mode пересохранил слот. F5/F9 покрыты `SaveDebugHotkeysTests`.

Итог: EditMode 326/0, PlayMode 351/0 (4 inconclusive — как до начала).

# SaveSystem, часть 3 — Unity-типы и ссылки: план реализации

> **Для исполнителя:** обязательный под-скилл — superpowers:executing-plans (инлайн, задача за задачей). Тесты идут
> через один Unity-эдитор по MCP; параллельные исполнители друг другу мешали бы. Шаги — чекбоксы (`- [ ]`).

**Цель:** встроенные сериализаторы Unity-типов (`Rigidbody2D`, `Rigidbody`, `Collider`/`Collider2D`, `Animator`,
`ParticleSystem` с флажком «живые частицы», `SpriteRenderer`, `AudioSource`), ссылки в полях `[Save]` и в
сериализаторах (на `Saveable` и его компоненты и детей, на префабы, на ассеты из выбранных папок), `SpriteAnimatorSerializer`
в игре, демо-сцена `Sandbox_SaveSystem.unity` и `Saveable` на игроке — это закрывает критерий готовности спека.

**Архитектура:** встроенные сериализаторы — обычные `ComponentSerializer<,>` в `SaveSystem.Objects`, каждый под
`#if` своего модуля движка (`versionDefines`). Ссылки — один конвертер Newtonsoft для всех `UnityEngine.Object`: в
поле `[Save]` и в данных сериализаторов компонентов; разрешает его статический `SaveReferences` по реестру
`Saveable` и каталогам `SaveSystemSettings` (префабы — по ID префаба, ассеты — по ключу из новой таблицы ассетов).
Параметры сериализаторов («живые частицы») — флажки на `Saveable`, которые сериализатор видит во время своей работы.

**Стек:** Unity 6000.5.6f1, C# 9, Newtonsoft JSON 3.2.2, NUnit (Unity Test Framework), EditMode + PlayMode, MCP.

Спек — [SaveSystem.md](SaveSystem.md), разделы 1 (часть 3 и критерий готовности), 6.3 (таблица встроенных), 6.4
(ссылки), 6.6 (таблица ассетов), 8 (игрок, демо), 10–11. Планы частей 1–2 — [SaveSystem_Plan.md](SaveSystem_Plan.md),
[SaveSystem_Plan_Part2.md](SaveSystem_Plan_Part2.md); основа — **фактический код частей 1–2** (ветка
`feature/save-system`, коммит `7295c724`).

## Решения, принятые перед планом (с пользователем, 2026-10-24)

- **Игрок — последней задачей, после слияния.** `feature/ui` (основная папка) сильно меняла `Player.prefab`,
  `Game.unity`, `Sandbox_Enemies.unity`. Предпоследняя задача сначала (с разрешения пользователя в тот момент)
  вливает `feature/ui` в `feature/save-system`, потом ставит `Saveable` на слитый префаб. Остальные задачи игрока
  не трогают.
- **У игрока сохраняются `Transform`, `Rigidbody2D` и `SpriteAnimator`**: остальные компоненты на `Saveable` игрока
  сняты (вспышки, цвета, эффекты не восстанавливаются).
- **Враги сохраняются тоже:** `Enemy.prefab` — вариант игрока и наследует его `Saveable`; после загрузки враг стоит на
  сохранённом месте, а машина состояний ИИ начинает с `Patrol` (её состояние не сохраняется).
- **Папки ассетов для ссылок** в настройках игры — `Assets/Game/Art` и `Assets/Game/Audio`.

## Решения плана (в спеке не описаны)

- **Формат ссылки:** объект сцены — `{ "id": "<ID Saveable>", "path": "Visual", "component": "SpriteRenderer" }`
  (`id` — ближайший `Saveable` над целью, `path` — от него до объекта цели, нет у самого объекта Saveable;
  `component` — имя типа на этом объекте, второй того же типа — `Type#1`, нет у ссылки на `GameObject`); префаб —
  `{ "prefab": "<ID префаба>" }`; ассет — `{ "asset": "<ключ>" }`, ключ — GUID ассета, у вложенного (спрайт в
  текстуре) — `GUID:localId`. Ссылка на объект сцены вне `Saveable`, на ассет вне папок, на созданный в игре ассет —
  `null` и предупреждение (один раз на объект). Не найдено при загрузке — `null` и предупреждение.
- **Ссылки разрешаются там же, где данные:** в позднем `Start` `Saveable` все объекты сцены уже есть. Исключение —
  выключенный объект получает данные сразу, в фазе объектов: ссылка с него на ещё не заспавненный объект станет
  `null` (документируется).
- **`ComponentSerializer` пишет данные тем же Newtonsoft, что и ядро, плюс конвертер ссылок** — поле `Sprite` в
  `TData` пишется как ссылка. Поля `[Save]` с Unity-объектами больше не пропускаются.
- **Параметры сериализаторов:** сериализатор, реализующий `ISerializerOptions`, объявляет имена флажков; `Saveable`
  хранит включённые как `"<ключ компонента>|<флажок>"` (`_options`); во время `Capture`/`Restore` сериализатор
  спрашивает `SerializerOptions.IsOn("liveParticles")`. Инспектор `Saveable` рисует флажки под компонентом.
- **`Collider2D` и `Collider`** — встроенные сериализаторы без данных: компонент появляется в списке `Saveable`,
  а его `enabled` пишет общий механизм (у `Collider` 3D — добавлен в него).
- **`ParticleSystem`:** восстанавливается перемоткой `Simulate` от сохранённого зерна; чтобы зерно можно было
  задать, у системы выключается `useAutoRandomSeed` (при автозерне Unity не отдаёт реальное зерно — рисунок
  выброса после загрузки может отличаться, время — то же). Пауза — `Simulate` оставляет систему на паузе.
- **`AudioSource`:** «пауза» — не играет, но время больше нуля; `time` выставляется после `Play`.
- **`Animator`:** если контроллера нет или аниматор не инициализирован (объект выключен), пишется только `speed`.
- **Публичный снимок одного объекта:** `Saveable.CaptureState() → JToken` и `RestoreState(JToken)` — для тестов
  игры (стенд `CharacterTestRig` без сцены и `[Services]`) и отладки.
- **Демо-скрипт** `SaveDemoBoard` (`Scripts/Gameplay/Demo/`): клавиши B — заспавнить ящик, X — уничтожить последний, C —
  сменить табличку; поля `[Save]` со ссылками на заспавненный объект и на спрайт. Демо не в Build Settings.
- **`_Import` удаляет пользователь** (Claude не удаляет файлы безвозвратно): последний шаг — попросить.

## Что не проверено заранее

Если что-то из этого не так — остановиться на задаче, где это всплыло, и обсудить:

- `versionDefines` с пустым `expression` на встроенный модуль (`com.unity.modules.physics` и др.) задаёт символ
  (задача 1).
- `Rigidbody2D.IsAwake()` сразу после `Sleep()` — `false`; `Rigidbody.IsSleeping()` после `Sleep()` — `true` в том же
  кадре (задача 1).
- `Animator.Play(hash, layer, time)` + `Animator.Update(0f)` сразу даёт новое состояние в
  `GetCurrentAnimatorStateInfo` (задача 2).
- `ParticleSystem.randomSeed` задаётся у остановленной системы с `useAutoRandomSeed = false`; `Simulate(t, true,
  true)` оставляет её на паузе с `time ≈ t` (задача 3).
- `AudioSource.time` меняется в Play Mode эдитора (тест на тихом клипе; задача 5).
- `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` даёт стабильный `localId` спрайта в текстуре (задача 4).

## Глобальные ограничения

- Всё, что не про игру, — в модуле `Assets/Game/Scripts/Modules/SaveSystem/`; namespace `Saving.Objects`, сборка
  `SaveSystem.Objects`. Модуль не зависит от `Game`, `ServiceLocator` и Odin. Код игры — namespace `Gameplay`.
- Встроенный сериализатор модуля движка — под `#if` своего символа: `SAVE_PHYSICS` (`com.unity.modules.physics`),
  `SAVE_PHYSICS_2D` (`physics2d`), `SAVE_ANIMATION` (`animation`), `SAVE_PARTICLES` (`particlesystem`), `SAVE_AUDIO`
  (`audio`). Тесты таких сериализаторов — под теми же символами (те же `versionDefines` в тестовых asmdef).
- Тестовые компоненты — в `Tests/Runtime/Components` (MonoBehaviour из папки `Editor` Unity не прикрепляет), каждый в
  своём файле с именем класса. EditMode-тесты со своими объектами — в `TempScene`.
- Стиль: Allman, 4 пробела, `_camelCase`, `[SerializeField] private`, `UPPER_CASE` константы; комментарии и имена —
  по-английски. Без `[Min]`, Odin, `GetInstanceID()`, `Object.FindObjectsByType`, `Scene.handle`. В командах
  `Unity_RunCommand` нельзя `using System.Reflection` и удалять ассеты.
- TDD: тест → падает по правильной причине → код → зелёные **все** тесты. Новый API на шаге Red — заглушками с
  `throw new NotImplementedException()`. Имена тестов — `Класс_Ситуация_Ожидание`.
- **Коммиты — только с разрешения пользователя** (при разрешённой автономной работе — локальный коммит каждой зелёной
  задачи); `git add` — только пути задачи; пуш — только по просьбе. Слияние `feature/ui` — только с явного
  согласия пользователя в момент задачи 8.

## Как запускать тесты (через MCP)

Как в частях 1–2: выйти из Play Mode, сохранить сцены; после правки файлов — `AssetDatabase.Refresh()` и консоль
без ошибок компиляции; затем `EditorApplication.ExecuteMenuItem("Tools/Game/Run All Tests")` (или EditMode /
PlayMode); дождаться `DONE` в `Temp/GameTestResults.txt`. Первой командой сессии — `Application.dataPath` (эдитор
этой рабочей копии).

## Файлы

```
Assets/Game/Scripts/Modules/SaveSystem/
├── Runtime/Objects/
│   ├── SaveSystem.Objects.asmdef           + versionDefines
│   ├── ObjectState.cs                      enabled у Collider 3D; флажки сериализаторов вокруг Capture/Restore
│   ├── Saveable.cs                         + _options, IsOptionOn/SetOption; CaptureState/RestoreState
│   ├── ISerializerOptions.cs, SerializerOptions.cs           (новое)
│   ├── SaveReferences.cs, UnityReferenceConverter.cs        (новое) ссылки
│   ├── ObjectsJson.cs                      + конвертер ссылок; сериализатор данных компонентов
│   ├── ComponentSerializer.cs              пишет через ObjectsJson.Components
│   ├── FieldsSerializer.cs                 ссылки больше не пропускаются
│   ├── SaveSystemSettings.cs               + папки и таблица ассетов
│   ├── SceneSaving.cs                      отдаёт настройки SaveReferences
│   └── BuiltIn/ Rigidbody2DSerializer, RigidbodySerializer, Collider2DSerializer, ColliderSerializer,
│                AnimatorSerializer, ParticleSystemSerializer, SpriteRendererSerializer, AudioSourceSerializer
├── Editor/  SaveCatalogBuilder.cs (+ таблица ассетов), SaveValidator.cs (+ ассеты), SaveCatalogPostprocessor.cs
│            (+ папки ассетов), SaveableEditor.cs (+ флажки), SaveSystemSettingsEditor.cs (+ папки)
├── Tests/Editor/   SaveReferencesTests.cs (новое), SaveCatalogTests.cs (+ ассеты), SaveableEditorTests.cs (+ флажки)
└── Tests/Runtime/  SaveSystem.Tests.Runtime.asmdef (+ versionDefines), TestScenes.cs (+ ассет, ссылки)
                    Components/SaveTestLinks.cs (новое), BuiltInSerializerTests.cs (новое), SceneSavingTests.cs (+ ссылки)
                    Art/SaveTestIcon.png, Audio/SaveTestTone.wav, Animation/SaveTestAnimator.controller,
                    Animation/SaveTestSlide.anim (создаются командами); SaveTestScene.unity (+ Linker, Box/Handle)

Assets/Game/Game.asmdef                               + SaveSystem.Objects
Assets/Game/Scripts/Gameplay/Character/Components/Visual/SpriteAnimatorSerializer.cs   (новое)
Assets/Game/Scripts/Gameplay/Demo/SaveDemoBoard.cs              (новое)
Assets/Game/Prefabs/Demo/SaveDemoCrate.prefab, Assets/Game/Animations/Demo/SaveDemoSpin.controller,
Assets/Game/Scenes/Sandbox_SaveSystem.unity            (создаются командами)
Assets/Game/Configs/SaveSystemSettings.asset           + папки Art, Audio и таблица ассетов
Assets/Game/Prefabs/Characters/Player.prefab           + Saveable (после слияния feature/ui)
Assets/Game/Tests/EditMode/SpriteAnimatorSerializerTests.cs   (новое)
Assets/Game/Tests/PlayMode/SaveDemoBoardTests.cs, PlayerSaveTests.cs   (новое)
Assets/AI/CLAUDE.md, Assets/AI/Docs/SaveSystem.md, Modules/SaveSystem/README.md   документация
```

Пути в задачах — от `Assets/Game/Scripts/Modules/SaveSystem/`, если не сказано иначе.

---

### Задача 1: физика — `Rigidbody2D`, `Rigidbody`, `Collider2D`, `Collider`

**Files:**
- Modify: `Runtime/Objects/SaveSystem.Objects.asmdef`, `Tests/Runtime/SaveSystem.Tests.Runtime.asmdef` (`versionDefines`)
- Modify: `Runtime/Objects/ObjectState.cs` (`enabled` у `Collider` 3D)
- Create: `Runtime/Objects/BuiltIn/Rigidbody2DSerializer.cs`, `RigidbodySerializer.cs`, `Collider2DSerializer.cs`,
  `ColliderSerializer.cs`
- Test: `Tests/Runtime/BuiltInSerializerTests.cs`

**Interfaces:**
- Consumes: `ComponentSerializer<,>`, `ComponentSerializerCatalog.Default`, `ObjectState`, `Saveable` (часть 2).
- Produces: встроенные `Rigidbody2DSerializer` (тип тела, `simulated`, позиция, поворот, скорость, угловая скорость,
  сон, `gravityScale`, `constraints`), `RigidbodySerializer` (`isKinematic`, `useGravity`, скорость, угловая скорость,
  сон, `constraints`), `Collider2DSerializer` и `ColliderSerializer` (без данных: только попасть в список и сохранить
  `enabled`). Символы `SAVE_PHYSICS`, `SAVE_PHYSICS_2D`, `SAVE_ANIMATION`, `SAVE_PARTICLES`, `SAVE_AUDIO`.

- [x] **Шаг 1: символы модулей.** В `Runtime/Objects/SaveSystem.Objects.asmdef` и
  `Tests/Runtime/SaveSystem.Tests.Runtime.asmdef` заменить `"versionDefines": [],` на:

  ```json
      "versionDefines": [
          { "name": "com.unity.modules.physics", "expression": "", "define": "SAVE_PHYSICS" },
          { "name": "com.unity.modules.physics2d", "expression": "", "define": "SAVE_PHYSICS_2D" },
          { "name": "com.unity.modules.animation", "expression": "", "define": "SAVE_ANIMATION" },
          { "name": "com.unity.modules.particlesystem", "expression": "", "define": "SAVE_PARTICLES" },
          { "name": "com.unity.modules.audio", "expression": "", "define": "SAVE_AUDIO" }
      ],
  ```

- [x] **Шаг 2: заглушки.**

  `Runtime/Objects/BuiltIn/Rigidbody2DSerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS_2D
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class Rigidbody2DSerializer : ComponentSerializer<Rigidbody2D, Rigidbody2DSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(Rigidbody2D body) => throw new NotImplementedException();
          protected override void Deserialize(Rigidbody2D body, Data data) => throw new NotImplementedException();
      }
  }
  #endif
  ```

  `Runtime/Objects/BuiltIn/RigidbodySerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class RigidbodySerializer : ComponentSerializer<Rigidbody, RigidbodySerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(Rigidbody body) => throw new NotImplementedException();
          protected override void Deserialize(Rigidbody body, Data data) => throw new NotImplementedException();
      }
  }
  #endif
  ```

  `Runtime/Objects/BuiltIn/Collider2DSerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS_2D
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class Collider2DSerializer : ComponentSerializer<Collider2D, Collider2DSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(Collider2D collider) => throw new NotImplementedException();
          protected override void Deserialize(Collider2D collider, Data data) => throw new NotImplementedException();
      }
  }
  #endif
  ```

  `Runtime/Objects/BuiltIn/ColliderSerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class ColliderSerializer : ComponentSerializer<Collider, ColliderSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(Collider collider) => throw new NotImplementedException();
          protected override void Deserialize(Collider collider, Data data) => throw new NotImplementedException();
      }
  }
  #endif
  ```

- [x] **Шаг 3: падающие тесты** — `Tests/Runtime/BuiltInSerializerTests.cs` (задачи 2, 3 и 5 допишут сюда свои):

  ```csharp
  using System.Collections;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;
  using UnityEngine.TestTools;
  using Object = UnityEngine.Object;

  namespace Saving.Tests
  {
      /// <summary>Each built-in serializer: a round trip on a live component.</summary>
      public class BuiltInSerializerTests
      {
          private readonly List<GameObject> _objects = new();

          [TearDown]
          public void TearDown()
          {
              foreach (GameObject go in _objects)
              {
                  if (go != null)
                      Object.DestroyImmediate(go);
              }

              _objects.Clear();
          }

          private GameObject Make(string name)
          {
              var go = new GameObject(name);
              _objects.Add(go);
              return go;
          }

          private static IComponentSerializer For<T>() => ComponentSerializerCatalog.Default.For(typeof(T));

  #if SAVE_PHYSICS_2D
          [Test]
          public void Rigidbody2D_BodyAndMotion_RoundTrip()
          {
              var body = Make("Body2D").AddComponent<Rigidbody2D>();
              body.gravityScale = 0.5f;
              body.position = new Vector2(1f, 2f);
              body.rotation = 30f;
              body.linearVelocity = new Vector2(3f, -4f);
              body.angularVelocity = 90f;
              JToken data = For<Rigidbody2D>().Capture(body);

              body.bodyType = RigidbodyType2D.Kinematic;
              body.constraints = RigidbodyConstraints2D.FreezeAll;
              body.gravityScale = 1f;
              body.position = Vector2.zero;
              body.rotation = 0f;
              For<Rigidbody2D>().Restore(body, data);

              Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
              Assert.AreEqual(RigidbodyConstraints2D.None, body.constraints);
              Assert.AreEqual(0.5f, body.gravityScale);
              Assert.AreEqual(new Vector2(1f, 2f), body.position);
              Assert.AreEqual(30f, body.rotation, 1e-3f);
              Assert.AreEqual(new Vector2(3f, -4f), body.linearVelocity);
              Assert.AreEqual(90f, body.angularVelocity, 1e-3f);
          }

          [Test]
          public void Rigidbody2D_Asleep_StaysAsleep()
          {
              var body = Make("Sleeper2D").AddComponent<Rigidbody2D>();
              body.Sleep();
              JToken data = For<Rigidbody2D>().Capture(body);
              body.WakeUp();

              For<Rigidbody2D>().Restore(body, data);

              Assert.IsFalse(body.IsAwake());
          }
  #endif

  #if SAVE_PHYSICS
          [Test]
          public void Rigidbody_BodyAndMotion_RoundTrip()
          {
              var body = Make("Body").AddComponent<Rigidbody>();
              body.useGravity = false;
              body.linearVelocity = new Vector3(1f, 2f, 3f);
              body.angularVelocity = new Vector3(0f, 0f, 2f);
              JToken data = For<Rigidbody>().Capture(body);

              body.useGravity = true;
              body.constraints = RigidbodyConstraints.FreezeAll;
              body.linearVelocity = Vector3.zero;
              body.angularVelocity = Vector3.zero;
              For<Rigidbody>().Restore(body, data);

              Assert.IsFalse(body.useGravity);
              Assert.AreEqual(RigidbodyConstraints.None, body.constraints);
              Assert.AreEqual(new Vector3(1f, 2f, 3f), body.linearVelocity);
              Assert.AreEqual(new Vector3(0f, 0f, 2f), body.angularVelocity);
          }

          [Test]
          public void Rigidbody_Asleep_StaysAsleep()
          {
              var body = Make("Sleeper").AddComponent<Rigidbody>();
              body.Sleep();
              JToken data = For<Rigidbody>().Capture(body);
              body.WakeUp();

              For<Rigidbody>().Restore(body, data);

              Assert.IsTrue(body.IsSleeping());
          }
  #endif

  #if SAVE_PHYSICS && SAVE_PHYSICS_2D
          [Test]
          public void Colliders_ListedAndEnabledKept()
          {
              GameObject go = Make("Colliders");
              var saveable = go.AddComponent<Saveable>();
              var box2D = go.AddComponent<BoxCollider2D>();
              // 2D and 3D physics do not mix on one object: the 3D collider sits on a child
              var solid = new GameObject("Solid");
              solid.transform.SetParent(go.transform, false);
              var box = solid.AddComponent<BoxCollider>();
              box2D.enabled = false;
              box.enabled = false;
              ObjectData data = ObjectState.Capture(saveable, ComponentSerializerCatalog.Default);

              box2D.enabled = true;
              box.enabled = true;
              ObjectState.RestoreComponents(saveable, data, ComponentSerializerCatalog.Default);

              Assert.IsTrue(data.Components.ContainsKey("BoxCollider2D"));
              Assert.IsTrue(data.Components.ContainsKey("Solid/BoxCollider"));
              Assert.IsFalse(box2D.enabled);
              Assert.IsFalse(box.enabled);
          }
  #endif
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** PlayMode: падают 5 новых тестов (`NotImplementedException`). Если какой-то тест
  не скомпилировался или не найден — символ `versionDefines` не задался: остановиться (допущение плана).

- [x] **Шаг 5: реализация.**

  `Runtime/Objects/BuiltIn/Rigidbody2DSerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS_2D
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Built in: body type, simulated, body position and rotation, velocities, sleep, gravity scale, constraints.
      /// Contacts and solver state are not restored: a tiny jolt after a load is possible.
      /// </summary>
      public sealed class Rigidbody2DSerializer : ComponentSerializer<Rigidbody2D, Rigidbody2DSerializer.Data>
      {
          public sealed class Data
          {
              public RigidbodyType2D BodyType = RigidbodyType2D.Dynamic;
              public bool Simulated = true;
              public Vector2 Position;
              public float Rotation;
              public Vector2 LinearVelocity;
              public float AngularVelocity;
              public bool Awake = true;
              public float GravityScale = 1f;
              public RigidbodyConstraints2D Constraints;
          }

          protected override Data Serialize(Rigidbody2D body) => new()
          {
              BodyType = body.bodyType,
              Simulated = body.simulated,
              Position = body.position,
              Rotation = body.rotation,
              LinearVelocity = body.linearVelocity,
              AngularVelocity = body.angularVelocity,
              Awake = body.IsAwake(),
              GravityScale = body.gravityScale,
              Constraints = body.constraints
          };

          protected override void Deserialize(Rigidbody2D body, Data data)
          {
              body.bodyType = data.BodyType;
              body.simulated = data.Simulated;
              body.constraints = data.Constraints;
              body.gravityScale = data.GravityScale;
              body.position = data.Position;
              body.rotation = data.Rotation;
              if (body.bodyType != RigidbodyType2D.Static)
              {
                  body.linearVelocity = data.LinearVelocity;
                  body.angularVelocity = data.AngularVelocity;
              }

              if (data.Awake)
                  body.WakeUp();
              else
                  body.Sleep();
          }
      }
  }
  #endif
  ```

  `Runtime/Objects/BuiltIn/RigidbodySerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>Built in: kinematic, gravity, velocities, sleep, constraints. The pose comes from Transform.</summary>
      public sealed class RigidbodySerializer : ComponentSerializer<Rigidbody, RigidbodySerializer.Data>
      {
          public sealed class Data
          {
              public bool IsKinematic;
              public bool UseGravity = true;
              public Vector3 LinearVelocity;
              public Vector3 AngularVelocity;
              public bool Sleeping;
              public RigidbodyConstraints Constraints;
          }

          protected override Data Serialize(Rigidbody body) => new()
          {
              IsKinematic = body.isKinematic,
              UseGravity = body.useGravity,
              LinearVelocity = body.linearVelocity,
              AngularVelocity = body.angularVelocity,
              Sleeping = body.IsSleeping(),
              Constraints = body.constraints
          };

          protected override void Deserialize(Rigidbody body, Data data)
          {
              body.isKinematic = data.IsKinematic;
              body.useGravity = data.UseGravity;
              body.constraints = data.Constraints;
              // A kinematic body takes no velocity (Unity warns)
              if (!body.isKinematic)
              {
                  body.linearVelocity = data.LinearVelocity;
                  body.angularVelocity = data.AngularVelocity;
              }

              if (data.Sleeping)
                  body.Sleep();
              else
                  body.WakeUp();
          }
      }
  }
  #endif
  ```

  `Runtime/Objects/BuiltIn/Collider2DSerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS_2D
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>Built in: no data of its own; it lists 2D colliders in the Saveable so their enabled flag is kept.</summary>
      public sealed class Collider2DSerializer : ComponentSerializer<Collider2D, Collider2DSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(Collider2D collider) => new();

          protected override void Deserialize(Collider2D collider, Data data)
          {
          }
      }
  }
  #endif
  ```

  `Runtime/Objects/BuiltIn/ColliderSerializer.cs`:

  ```csharp
  #if SAVE_PHYSICS
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>Built in: no data of its own; it lists 3D colliders in the Saveable so their enabled flag is kept.</summary>
      public sealed class ColliderSerializer : ComponentSerializer<Collider, ColliderSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(Collider collider) => new();

          protected override void Deserialize(Collider collider, Data data)
          {
          }
      }
  }
  #endif
  ```

  В `Runtime/Objects/ObjectState.cs` методы `EnabledOf` и `SetEnabled` заменить на (у 3D-коллайдера `enabled` есть, но
  он не `Behaviour`):

  ```csharp
          private static bool? EnabledOf(Component component)
          {
              switch (component)
              {
                  case Behaviour behaviour:
                      return behaviour.enabled;
                  case Renderer renderer:
                      return renderer.enabled;
  #if SAVE_PHYSICS
                  case Collider collider:
                      return collider.enabled;
  #endif
                  default:
                      return null;
              }
          }

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
  #if SAVE_PHYSICS
                  case Collider collider:
                      collider.enabled = enabled;
                      break;
  #endif
              }
          }
  ```

  В `SaveSystem.Objects.asmdef` символы уже заданы (шаг 1), так что `#if SAVE_PHYSICS` в `ObjectState` видит их.

- [x] **Шаг 6: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Save rigidbodies and colliders"
  ```

---

### Задача 2: `Animator`

**Files:**
- Create: `Runtime/Objects/BuiltIn/AnimatorSerializer.cs`
- Create (командой): `Tests/Runtime/Animation/SaveTestSlide.anim`, `Tests/Runtime/Animation/SaveTestAnimator.controller`
- Modify: `Tests/Runtime/TestScenes.cs` (+ `ANIMATOR`), `Tests/Runtime/BuiltInSerializerTests.cs` (+ 2 теста)

**Interfaces:**
- Consumes: `ComponentSerializer<,>` (часть 2), `SAVE_ANIMATION` (задача 1).
- Produces: встроенный `AnimatorSerializer`: `speed`; по слоям — полный хэш состояния, нормализованное время, вес
  (у слоя 0 вес не пишется); параметры `float`/`int`/`bool` по имени (триггеры не читаются). Нет контроллера или
  аниматор не инициализирован — только `speed`. Восстановление: параметры → `Play(hash, layer, time)` и вес по
  слоям → `Update(0)`.

- [x] **Шаг 1: заглушка** — `Runtime/Objects/BuiltIn/AnimatorSerializer.cs`:

  ```csharp
  #if SAVE_ANIMATION
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class AnimatorSerializer : ComponentSerializer<Animator, AnimatorSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(Animator animator) => throw new NotImplementedException();
          protected override void Deserialize(Animator animator, Data data) => throw new NotImplementedException();
      }
  }
  #endif
  ```

- [x] **Шаг 2: тестовый контроллер.** `Refresh`, затем `Unity_RunCommand`:

  ```csharp
  using UnityEditor;
  using UnityEditor.Animations;
  using UnityEngine;

  internal class CommandScript : IRunCommand
  {
      private const string PARENT = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime";
      private const string FOLDER = PARENT + "/Animation";

      public void Execute(ExecutionResult result)
      {
          if (!AssetDatabase.IsValidFolder(FOLDER))
              AssetDatabase.CreateFolder(PARENT, "Animation");

          var clip = new AnimationClip { name = "SaveTestSlide" };
          clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 1f));
          AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
          clipSettings.loopTime = true;
          AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
          AssetDatabase.CreateAsset(clip, FOLDER + "/SaveTestSlide.anim");

          AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(FOLDER + "/SaveTestAnimator.controller");
          controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
          controller.AddParameter("Count", AnimatorControllerParameterType.Int);
          controller.AddParameter("On", AnimatorControllerParameterType.Bool);
          controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
          AnimatorStateMachine machine = controller.layers[0].stateMachine;
          AnimatorState idle = machine.AddState("Idle");
          idle.motion = clip;
          AnimatorState slide = machine.AddState("Slide");
          slide.motion = clip;
          machine.defaultState = idle;

          AssetDatabase.SaveAssets();
          result.Log("Created {0}", FOLDER);
      }
  }
  ```

  В `Tests/Runtime/TestScenes.cs` константа:

  ```csharp
          public const string ANIMATOR = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/Animation/SaveTestAnimator.controller";
  ```

- [x] **Шаг 3: падающие тесты.** В `Tests/Runtime/BuiltInSerializerTests.cs`: в начало
  `#if UNITY_EDITOR` / `using UnityEditor;` / `#endif` и в класс:

  ```csharp
  #if SAVE_ANIMATION && UNITY_EDITOR
          [UnityTest]
          public IEnumerator Animator_StateTimeAndParameters_RoundTrip()
          {
              var animator = Make("Animated").AddComponent<Animator>();
              animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(TestScenes.ANIMATOR);
              yield return null;
              animator.Play("Slide", 0, 0.25f);
              animator.SetFloat("Speed", 2.5f);
              animator.SetInteger("Count", 3);
              animator.SetBool("On", true);
              animator.speed = 0.5f;
              animator.Update(0f);
              float savedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
              JToken data = For<Animator>().Capture(animator);

              animator.Play("Idle", 0, 0.9f);
              animator.SetFloat("Speed", 0f);
              animator.SetInteger("Count", 0);
              animator.SetBool("On", false);
              animator.speed = 1f;
              animator.Update(0f);
              For<Animator>().Restore(animator, data);

              AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
              Assert.AreEqual(Animator.StringToHash("Base Layer.Slide"), state.fullPathHash);
              Assert.AreEqual(savedTime, state.normalizedTime, 0.01f);
              Assert.AreEqual(2.5f, animator.GetFloat("Speed"));
              Assert.AreEqual(3, animator.GetInteger("Count"));
              Assert.IsTrue(animator.GetBool("On"));
              Assert.AreEqual(0.5f, animator.speed);
          }

          [Test]
          public void Animator_WithoutController_OnlySpeed()
          {
              var animator = Make("Bare").AddComponent<Animator>();
              animator.speed = 2f;

              JToken data = For<Animator>().Capture(animator);
              animator.speed = 1f;
              For<Animator>().Restore(animator, data);

              Assert.AreEqual(2f, animator.speed);
              Assert.AreEqual(0, ((JArray)data["layers"]).Count);
          }
  #endif
  ```

- [x] **Шаг 4: запуск — красный.** PlayMode: оба теста падают с `NotImplementedException`.

- [x] **Шаг 5: реализация** — `Runtime/Objects/BuiltIn/AnimatorSerializer.cs`:

  ```csharp
  #if SAVE_ANIMATION
  using System.Collections.Generic;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Built in: speed; per layer the state, the time in it and the weight; float, int and bool parameters by name
      /// (triggers cannot be read). A transition in the middle of a blend is not kept. Without a controller, or on a
      /// switched-off object (not initialized), only the speed is saved.
      /// </summary>
      public sealed class AnimatorSerializer : ComponentSerializer<Animator, AnimatorSerializer.Data>
      {
          public sealed class Layer
          {
              public int State;
              public float Time;
              public float Weight = 1f;
          }

          public sealed class Data
          {
              public float Speed = 1f;
              public List<Layer> Layers = new();
              public Dictionary<string, float> Floats = new();
              public Dictionary<string, int> Ints = new();
              public Dictionary<string, bool> Bools = new();
          }

          protected override Data Serialize(Animator animator)
          {
              var data = new Data { Speed = animator.speed };
              if (animator.runtimeAnimatorController == null || !animator.isInitialized)
                  return data;

              for (int layer = 0; layer < animator.layerCount; layer++)
              {
                  AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                  data.Layers.Add(new Layer
                  {
                      State = state.fullPathHash,
                      Time = state.normalizedTime,
                      Weight = layer == 0 ? 1f : animator.GetLayerWeight(layer)
                  });
              }

              foreach (AnimatorControllerParameter parameter in animator.parameters)
              {
                  switch (parameter.type)
                  {
                      case AnimatorControllerParameterType.Float:
                          data.Floats[parameter.name] = animator.GetFloat(parameter.nameHash);
                          break;
                      case AnimatorControllerParameterType.Int:
                          data.Ints[parameter.name] = animator.GetInteger(parameter.nameHash);
                          break;
                      case AnimatorControllerParameterType.Bool:
                          data.Bools[parameter.name] = animator.GetBool(parameter.nameHash);
                          break;
                  }
              }

              return data;
          }

          protected override void Deserialize(Animator animator, Data data)
          {
              animator.speed = data.Speed;
              if (animator.runtimeAnimatorController == null || !animator.isInitialized)
                  return;

              foreach (AnimatorControllerParameter parameter in animator.parameters)
              {
                  switch (parameter.type)
                  {
                      case AnimatorControllerParameterType.Float when data.Floats.TryGetValue(parameter.name, out float value):
                          animator.SetFloat(parameter.nameHash, value);
                          break;
                      case AnimatorControllerParameterType.Int when data.Ints.TryGetValue(parameter.name, out int value):
                          animator.SetInteger(parameter.nameHash, value);
                          break;
                      case AnimatorControllerParameterType.Bool when data.Bools.TryGetValue(parameter.name, out bool value):
                          animator.SetBool(parameter.nameHash, value);
                          break;
                  }
              }

              for (int layer = 0; layer < Mathf.Min(data.Layers.Count, animator.layerCount); layer++)
              {
                  if (layer > 0)
                      animator.SetLayerWeight(layer, data.Layers[layer].Weight);

                  animator.Play(data.Layers[layer].State, layer, data.Layers[layer].Time);
              }

              // Play takes effect on the next update: apply it now, so the state is right before anything reads it
              animator.Update(0f);
          }
      }
  }
  #endif
  ```

- [x] **Шаг 6: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Save Animator states and parameters"
  ```

---

### Задача 3: параметры сериализаторов и `ParticleSystem`

**Files:**
- Create: `Runtime/Objects/ISerializerOptions.cs`, `Runtime/Objects/SerializerOptions.cs`,
  `Runtime/Objects/BuiltIn/ParticleSystemSerializer.cs`
- Modify: `Runtime/Objects/Saveable.cs` (+ `_options`), `Runtime/Objects/ObjectState.cs` (флажки вокруг
  `Capture`/`Restore`), `Editor/SaveableEditor.cs` (флажки в строках)
- Test: `Tests/Runtime/BuiltInSerializerTests.cs` (+ 3), `Tests/Editor/SaveableEditorTests.cs` (+ 1)

**Interfaces:**
- Consumes: `ObjectState`, `Saveable`, `SaveableEditor.BuildRows` (часть 2), `SAVE_PARTICLES` (задача 1).
- Produces:
  - `ISerializerOptions { IReadOnlyList<string> Options { get; } }` — сериализатор объявляет свои флажки;
  - `SerializerOptions.IsOn(string option) → bool` (публичный: зовёт сериализатор во время работы);
    `internal SerializerOptions.Use(ICollection<string>) → Scope` (`IDisposable`);
  - `Saveable.IsOptionOn(string key, string option)`, `Saveable.SetOption(string key, string option, bool on)`,
    `internal Saveable.OptionsOf(string key) → ICollection<string>`; хранение — `_options`, строки `"key|option"`;
  - `ParticleSystemSerializer` (+ `LIVE_PARTICLES = "liveParticles"`): играет / пауза / стоп, время, зерно; с флажком —
    живые частицы;
  - `SaveableEditor.Row`: + `IReadOnlyList<string> Options`, `bool IsOn(string option)`.

- [x] **Шаг 1: контракт** (финальные).

  `Runtime/Objects/ISerializerOptions.cs`:

  ```csharp
  using System.Collections.Generic;

  namespace Saving.Objects
  {
      /// <summary>A serializer with switches of its own; the Saveable inspector shows them under the component.</summary>
      public interface ISerializerOptions
      {
          IReadOnlyList<string> Options { get; }
      }
  }
  ```

  `Runtime/Objects/SerializerOptions.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Saving.Objects
  {
      /// <summary>The options the Saveable turned on for the component being saved or loaded right now.</summary>
      public static class SerializerOptions
      {
          private static readonly HashSet<string> None = new();
          private static ICollection<string> _current = None;

          public static bool IsOn(string option) => _current.Contains(option);

          internal static Scope Use(ICollection<string> options)
          {
              var scope = new Scope(_current);
              _current = options ?? None;
              return scope;
          }

          internal readonly struct Scope : IDisposable
          {
              private readonly ICollection<string> _previous;

              public Scope(ICollection<string> previous) => _previous = previous;

              public void Dispose() => _current = _previous;
          }
      }
  }
  ```

- [x] **Шаг 2: заглушки.**

  В `Runtime/Objects/Saveable.cs` поле после `_excluded`:

  ```csharp
          [Tooltip("Options of the serializers, as <component key>|<option>")]
          [SerializeField] private List<string> _options = new();
  ```

  и методы после `SetSaved`:

  ```csharp
          public bool IsOptionOn(string componentKey, string option) => throw new NotImplementedException();
          public void SetOption(string componentKey, string option, bool on) => throw new NotImplementedException();
          internal ICollection<string> OptionsOf(string componentKey) => throw new NotImplementedException();
  ```

  `Runtime/Objects/BuiltIn/ParticleSystemSerializer.cs`:

  ```csharp
  #if SAVE_PARTICLES
  using System;
  using System.Collections.Generic;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class ParticleSystemSerializer : ComponentSerializer<ParticleSystem, ParticleSystemSerializer.Data>,
          ISerializerOptions
      {
          public const string LIVE_PARTICLES = "liveParticles";

          public sealed class Data
          {
          }

          public IReadOnlyList<string> Options { get; } = new[] { LIVE_PARTICLES };

          protected override Data Serialize(ParticleSystem particles) => throw new NotImplementedException();
          protected override void Deserialize(ParticleSystem particles, Data data) => throw new NotImplementedException();
      }
  }
  #endif
  ```

  В `Editor/SaveableEditor.cs` структуру `Row` заменить на:

  ```csharp
          public readonly struct Row
          {
              private readonly ICollection<string> _on;

              public Row(string key, SerializerKind kind, bool saved, IReadOnlyList<string> options, ICollection<string> on)
              {
                  Key = key;
                  Kind = kind;
                  Saved = saved;
                  Options = options ?? Array.Empty<string>();
                  _on = on;
              }

              public string Key { get; }
              public SerializerKind Kind { get; }
              public bool Saved { get; }
              public IReadOnlyList<string> Options { get; }

              public bool IsOn(string option) => _on != null && _on.Contains(option);
          }
  ```

  (`using System;` в начало файла) и в `BuildRows` строку `rows.Add(new Row(entry.Key, kind, saveable.IsSaved(entry.Key)));` —
  на `rows.Add(new Row(entry.Key, kind, saveable.IsSaved(entry.Key), null, null));` (флажки заполнит шаг 5).

- [x] **Шаг 3: падающие тесты.**

  В `Tests/Runtime/BuiltInSerializerTests.cs`:

  ```csharp
  #if SAVE_PARTICLES
          [UnityTest]
          public IEnumerator ParticleSystem_PlayingAndTime_RoundTrip()
          {
              var particles = Make("Particles").AddComponent<ParticleSystem>();
              particles.Play();
              yield return new WaitForSeconds(0.3f);
              float time = particles.time;
              JToken data = For<ParticleSystem>().Capture(particles);

              particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
              For<ParticleSystem>().Restore(particles, data);

              Assert.IsTrue(particles.isPlaying);
              Assert.AreEqual(time, particles.time, 0.05f);
          }

          [UnityTest]
          public IEnumerator ParticleSystem_Paused_StaysPaused()
          {
              var particles = Make("Paused").AddComponent<ParticleSystem>();
              particles.Play();
              yield return new WaitForSeconds(0.2f);
              particles.Pause();
              JToken data = For<ParticleSystem>().Capture(particles);

              particles.Play();
              For<ParticleSystem>().Restore(particles, data);

              Assert.IsTrue(particles.isPaused);
          }

          [UnityTest]
          public IEnumerator ParticleSystem_LiveParticles_OnlyWithTheOption()
          {
              GameObject go = Make("Live");
              var saveable = go.AddComponent<Saveable>();
              var particles = go.AddComponent<ParticleSystem>();
              particles.Play();
              yield return new WaitForSeconds(0.5f);
              particles.Pause();
              var live = new ParticleSystem.Particle[particles.particleCount];
              int count = particles.GetParticles(live);
              Assert.Greater(count, 0, "precondition: some particles are alive");

              ObjectData without = ObjectState.Capture(saveable, ComponentSerializerCatalog.Default);
              saveable.SetOption("ParticleSystem", ParticleSystemSerializer.LIVE_PARTICLES, true);
              ObjectData with = ObjectState.Capture(saveable, ComponentSerializerCatalog.Default);
              particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
              ObjectState.RestoreComponents(saveable, with, ComponentSerializerCatalog.Default);

              Assert.AreEqual(JTokenType.Null, without.Components["ParticleSystem"].Data["particles"].Type);
              var restored = new ParticleSystem.Particle[particles.particleCount];
              Assert.AreEqual(count, particles.GetParticles(restored));
              Assert.AreEqual(live[0].position, restored[0].position);
          }
  #endif
  ```

  В `Tests/Editor/SaveableEditorTests.cs`:

  ```csharp
          [Test]
          public void Rows_SerializerOptions_ListedWithState()
          {
              var root = new GameObject("Root");
              var saveable = root.AddComponent<Saveable>();
              root.AddComponent<ParticleSystem>();
              saveable.SetOption("ParticleSystem", ParticleSystemSerializer.LIVE_PARTICLES, true);

              SaveableEditor.Row row = SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default)
                  .Single(candidate => candidate.Key == "ParticleSystem");

              CollectionAssert.AreEqual(new[] { ParticleSystemSerializer.LIVE_PARTICLES }, row.Options);
              Assert.IsTrue(row.IsOn(ParticleSystemSerializer.LIVE_PARTICLES));
              Assert.IsTrue(saveable.IsOptionOn("ParticleSystem", ParticleSystemSerializer.LIVE_PARTICLES));
          }
  ```

  (Тест без `#if`: `ParticleSystemSerializer` есть, пока в проекте включён модуль частиц, — как в этом проекте.)

- [x] **Шаг 4: запуск — красный.** All Tests: падают 3 новых PlayMode-теста и `Rows_SerializerOptions_ListedWithState`
  (`NotImplementedException` из `SetOption`/сериализатора).

- [x] **Шаг 5: реализация.**

  В `Runtime/Objects/Saveable.cs` заглушки заменить на:

  ```csharp
          public bool IsOptionOn(string componentKey, string option) => _options.Contains(OptionEntry(componentKey, option));

          /// <summary>Turns a serializer option of a component on or off, such as live particles.</summary>
          public void SetOption(string componentKey, string option, bool on)
          {
              string entry = OptionEntry(componentKey, option);
              if (!on)
                  _options.Remove(entry);
              else if (!_options.Contains(entry))
                  _options.Add(entry);
          }

          internal ICollection<string> OptionsOf(string componentKey)
          {
              var options = new HashSet<string>();
              string prefix = componentKey + "|";
              foreach (string entry in _options)
              {
                  if (entry.StartsWith(prefix, StringComparison.Ordinal))
                      options.Add(entry.Substring(prefix.Length));
              }

              return options;
          }

          private static string OptionEntry(string componentKey, string option) => componentKey + "|" + option;
  ```

  В `Runtime/Objects/ObjectState.cs`: в `Capture` строку `Data = serializer.Capture(entry.Component)` поменять — данные
  снимать заранее под флажками компонента:

  ```csharp
                  try
                  {
                      JToken captured;
                      using (SerializerOptions.Use(saveable.OptionsOf(entry.Key)))
                          captured = serializer.Capture(entry.Component);

                      data.Components[entry.Key] = new ComponentData
                      {
                          V = serializer.Version,
                          Enabled = EnabledOf(entry.Component),
                          Data = captured
                      };
                  }
  ```

  и в `RestoreComponents` строку `serializer.Restore(component, Upgrade(serializer, pair.Value));` — на:

  ```csharp
                      using (SerializerOptions.Use(saveable.OptionsOf(pair.Key)))
                          serializer.Restore(component, Upgrade(serializer, pair.Value));
  ```

  `Runtime/Objects/BuiltIn/ParticleSystemSerializer.cs`:

  ```csharp
  #if SAVE_PARTICLES
  using System.Collections.Generic;
  using System.Linq;
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Built in: playing, paused or stopped, the time and the random seed; restored by fast-forwarding Simulate from
      /// that seed. Setting the seed turns useAutoRandomSeed off. With the "live particles" option the living
      /// particles are written too (the file grows with them).
      /// </summary>
      public sealed class ParticleSystemSerializer : ComponentSerializer<ParticleSystem, ParticleSystemSerializer.Data>,
          ISerializerOptions
      {
          public const string LIVE_PARTICLES = "liveParticles";

          public enum Playback
          {
              Stopped,
              Playing,
              Paused
          }

          public sealed class Particle
          {
              public Vector3 Position;
              public Vector3 Velocity;
              public float RemainingLifetime;
              public float StartLifetime;
              public Color32 Color;
              public float Size;
              public float Rotation;
              public float AngularVelocity;
              public uint Seed;
          }

          public sealed class Data
          {
              public Playback State;
              public float Time;
              public uint Seed;
              public List<Particle> Particles;
          }

          public IReadOnlyList<string> Options { get; } = new[] { LIVE_PARTICLES };

          protected override Data Serialize(ParticleSystem particles)
          {
              var data = new Data
              {
                  State = particles.isPaused ? Playback.Paused : particles.isPlaying ? Playback.Playing : Playback.Stopped,
                  Time = particles.time,
                  Seed = particles.randomSeed
              };

              if (!SerializerOptions.IsOn(LIVE_PARTICLES) || particles.particleCount == 0)
                  return data;

              var live = new ParticleSystem.Particle[particles.particleCount];
              int count = particles.GetParticles(live);
              data.Particles = live.Take(count).Select(particle => new Particle
              {
                  Position = particle.position,
                  Velocity = particle.velocity,
                  RemainingLifetime = particle.remainingLifetime,
                  StartLifetime = particle.startLifetime,
                  Color = particle.startColor,
                  Size = particle.startSize,
                  Rotation = particle.rotation,
                  AngularVelocity = particle.angularVelocity,
                  Seed = particle.randomSeed
              }).ToList();
              return data;
          }

          protected override void Deserialize(ParticleSystem particles, Data data)
          {
              particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
              if (data.State == Playback.Stopped)
                  return;

              // The seed can be set only on a stopped system that does not pick its own
              particles.useAutoRandomSeed = false;
              particles.randomSeed = data.Seed;
              particles.Simulate(data.Time, withChildren: true, restart: true, fixedTimeStep: true);

              if (data.Particles != null && data.Particles.Count > 0)
              {
                  ParticleSystem.Particle[] live = data.Particles.Select(particle => new ParticleSystem.Particle
                  {
                      position = particle.Position,
                      velocity = particle.Velocity,
                      remainingLifetime = particle.RemainingLifetime,
                      startLifetime = particle.StartLifetime,
                      startColor = particle.Color,
                      startSize = particle.Size,
                      rotation = particle.Rotation,
                      angularVelocity = particle.AngularVelocity,
                      randomSeed = particle.Seed
                  }).ToArray();
                  particles.SetParticles(live, live.Length);
              }

              // Simulate leaves the system paused: a playing one goes on from that time
              if (data.State == Playback.Playing)
                  particles.Play(true);
          }
      }
  }
  #endif
  ```

  В `Editor/SaveableEditor.cs`:
  - в `BuildRows` строку `rows.Add(new Row(entry.Key, kind, saveable.IsSaved(entry.Key), null, null));` — на:

    ```csharp
                    IComponentSerializer serializer = catalog.For(entry.Component.GetType());
                    rows.Add(new Row(entry.Key, kind, saveable.IsSaved(entry.Key), (serializer as ISerializerOptions)?.Options,
                        saveable.OptionsOf(entry.Key)));
    ```

  - в `OnInspectorGUI` после строки `SetExcluded(excluded, row.Key, !saved);` (внутри цикла по строкам) — флажки:

    ```csharp
                  SerializedProperty options = serializedObject.FindProperty("_options");
                  using (new EditorGUI.IndentLevelScope())
                  {
                      foreach (string option in row.Options)
                      {
                          bool on = EditorGUILayout.ToggleLeft(option, row.IsOn(option));
                          if (on != row.IsOn(option))
                              SetExcluded(options, $"{row.Key}|{option}", on);
                      }
                  }
    ```

    (`SetExcluded` — общее «добавить/убрать строку в списке»: для флажков `exclude = on` означает «добавить».)

- [x] **Шаг 6: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Save particle systems, with live particles as a Saveable option"
  ```

---

### Задача 4: ссылки и таблица ассетов

**Files:**
- Modify: `Runtime/Objects/SaveSystemSettings.cs` (+ папки и таблица ассетов), `Runtime/Objects/ObjectsJson.cs`,
  `Runtime/Objects/ComponentSerializer.cs`, `Runtime/Objects/FieldsSerializer.cs`, `Runtime/Objects/SceneSaving.cs`
- Create: `Runtime/Objects/SaveReferences.cs`, `Runtime/Objects/UnityReferenceConverter.cs`
- Modify: `Editor/SaveCatalogBuilder.cs`, `Editor/SaveValidator.cs`, `Editor/SaveCatalogPostprocessor.cs`,
  `Editor/SaveSystemSettingsEditor.cs`
- Create: `Tests/Runtime/Components/SaveTestLinks.cs`; (командой) `Tests/Runtime/Art/SaveTestIcon.png`, правка
  `Tests/Runtime/Scenes/SaveTestScene.unity` (+ `Linker`, `Box/Handle`)
- Modify: `Tests/Runtime/TestScenes.cs` (+ `ICON`, `LINKER`, `Icon`, ассеты в `CreateSettings`)
- Test: `Tests/Editor/SaveReferencesTests.cs`, `Tests/Editor/SaveCatalogTests.cs` (+ 2), `Tests/Runtime/SceneSavingTests.cs` (+ 2)
- Modify (командой): `Assets/Game/Configs/SaveSystemSettings.asset` — папки `Assets/Game/Art`, `Assets/Game/Audio`

**Interfaces:**
- Consumes: `SaveRegistry`, `ComponentKeys`, `SaveSystemSettings`, `SaveCatalogBuilder`, `SaveValidator` (часть 2).
- Produces:
  - `SaveSystemSettings`: `AssetEntry { string Key; Object Asset; }`, `IReadOnlyList<string> AssetFolders`,
    `IReadOnlyList<AssetEntry> Assets`, `static string AssetKey(string guid, long localId, bool isMainAsset)`,
    `TryGetAsset(string key, out Object)`, `TryGetAssetKey(Object, out string key)`, `SetAssetFolders(IEnumerable<string>)`,
    `SetAssetCatalog(IEnumerable<AssetEntry>)`;
  - `internal SaveReferences`: `Settings { get; set; }`, `Write(Object) → JToken`, `Read(JToken, Type) → Object`;
  - `internal UnityReferenceConverter : JsonConverter` (для всех `UnityEngine.Object`);
  - `internal ObjectsJson.Components` (`JsonSerializer`), `ComponentToToken<T>(T)`, `ComponentFromToken<T>(JToken)`;
  - `SaveCatalogBuilder.BuildAssetEntries(SaveSystemSettings) → List<AssetEntry>`,
    `SaveCatalogBuilder.Rebuild(SaveSystemSettings, bool includeAssets = true)`.
- Правила — «Решения плана» (формат ссылки, `null` и предупреждение).

- [x] **Шаг 1: заглушки.**

  В `Runtime/Objects/SaveSystemSettings.cs` вложенный класс после `SceneEntry`:

  ```csharp
          [Serializable]
          public sealed class AssetEntry
          {
              public string Key;
              public UnityEngine.Object Asset;
          }
  ```

  поля после `_scenes`:

  ```csharp
          [Tooltip("Folders whose assets [Save] fields and serializers may reference; the table below follows them")]
          [SerializeField] private List<string> _assetFolders = new();
          [SerializeField] private List<AssetEntry> _assets = new();
  ```

  и члены после `Scenes`:

  ```csharp
          public IReadOnlyList<string> AssetFolders => _assetFolders;
          public IReadOnlyList<AssetEntry> Assets => _assets;

          // Final already: the test helpers fill the table through these, and a stub would break every scene test
          public static string AssetKey(string guid, long localId, bool isMainAsset) =>
              isMainAsset ? guid : $"{guid}:{localId}";

          public void SetAssetFolders(IEnumerable<string> folders) => _assetFolders = folders.ToList();
          public void SetAssetCatalog(IEnumerable<AssetEntry> assets) => _assets = assets.ToList();

          public bool TryGetAsset(string key, out UnityEngine.Object asset) => throw new NotImplementedException();
          public bool TryGetAssetKey(UnityEngine.Object asset, out string key) => throw new NotImplementedException();
  ```

  `Runtime/Objects/SaveReferences.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json.Linq;
  using Object = UnityEngine.Object;

  namespace Saving.Objects
  {
      internal static class SaveReferences
      {
          public static SaveSystemSettings Settings { get; set; }

          public static JToken Write(Object target) => throw new NotImplementedException();
          public static Object Read(JToken token, Type type) => throw new NotImplementedException();
      }
  }
  ```

  В `Editor/SaveCatalogBuilder.cs`:

  ```csharp
          public static List<SaveSystemSettings.AssetEntry> BuildAssetEntries(SaveSystemSettings settings) =>
              throw new NotImplementedException();
  ```

- [x] **Шаг 2: тестовые ассеты и компонент.**

  `Tests/Runtime/Components/SaveTestLinks.cs`:

  ```csharp
  using System.Collections.Generic;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>[Save] references of every kind: a Saveable, a component, a child, a prefab, an asset, a list.</summary>
      public sealed class SaveTestLinks : MonoBehaviour
      {
          [Save] public Saveable Target;
          [Save] public SaveTestCounter Counter;
          [Save] public Transform Handle;
          [Save] public GameObject Prefab;
          [Save] public Sprite Icon;
          [Save] public List<Saveable> Many = new();
      }
  }
  ```

  `Refresh`, консоль без ошибок, затем `Unity_RunCommand` (иконка-спрайт; в тестовой сцене — `Linker` и ребёнок
  `Handle` у `Box`):

  ```csharp
  using System;
  using System.IO;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  internal class CommandScript : IRunCommand
  {
      private const string RUNTIME = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime";
      private const string ICON = RUNTIME + "/Art/SaveTestIcon.png";
      private const string SCENE = RUNTIME + "/Scenes/SaveTestScene.unity";

      private static readonly Type Saveable = Type.GetType("Saving.Objects.Saveable, SaveSystem.Objects");
      private static readonly Type Links = Type.GetType("Saving.Tests.SaveTestLinks, SaveSystem.Tests.Runtime");

      public void Execute(ExecutionResult result)
      {
          if (Saveable == null || Links == null)
          {
              result.LogError("Types are not compiled yet");
              return;
          }

          if (!AssetDatabase.IsValidFolder(RUNTIME + "/Art"))
              AssetDatabase.CreateFolder(RUNTIME, "Art");

          var texture = new Texture2D(4, 4);
          var pixels = new Color[16];
          for (int index = 0; index < pixels.Length; index++)
              pixels[index] = Color.white;
          texture.SetPixels(pixels);
          File.WriteAllBytes(ICON, texture.EncodeToPNG());
          UnityEngine.Object.DestroyImmediate(texture);
          AssetDatabase.ImportAsset(ICON);
          var importer = (TextureImporter)AssetImporter.GetAtPath(ICON);
          importer.textureType = TextureImporterType.Sprite;
          importer.spriteImportMode = SpriteImportMode.Single;
          importer.SaveAndReimport();

          Scene previous = SceneManager.GetActiveScene();
          bool untitled = string.IsNullOrEmpty(previous.path);
          if (untitled && previous.isDirty)
          {
              result.LogError("The untitled scene has changes");
              return;
          }

          Scene scene = EditorSceneManager.OpenScene(SCENE, untitled ? OpenSceneMode.Single : OpenSceneMode.Additive);
          GameObject box = Array.Find(scene.GetRootGameObjects(), root => root.name == "Box");
          var handle = new GameObject("Handle");
          SceneManager.MoveGameObjectToScene(handle, scene);
          handle.transform.SetParent(box.transform, false);

          var linker = new GameObject("Linker");
          SceneManager.MoveGameObjectToScene(linker, scene);
          var serialized = new SerializedObject(linker.AddComponent(Saveable));
          serialized.FindProperty("_id").stringValue = "5a0e0000000000000000000000000004";
          serialized.ApplyModifiedPropertiesWithoutUndo();
          linker.AddComponent(Links);

          EditorSceneManager.SaveScene(scene);
          if (untitled)
              EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
          else
              EditorSceneManager.CloseScene(scene, true);

          result.Log("Icon {0}, scene {1} updated", ICON, SCENE);
      }
  }

  ```

  В `Tests/Runtime/TestScenes.cs`:

  ```csharp
          public const string ICON = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/Art/SaveTestIcon.png";
          public const string LINKER = "5a0e0000000000000000000000000004";

          public static Sprite Icon =>
  #if UNITY_EDITOR
              AssetDatabase.LoadAssetAtPath<Sprite>(ICON);
  #else
              null;
  #endif

          /// <summary>The assets of a file as entries of the asset table, keyed the way the editor keys them.</summary>
          public static SaveSystemSettings.AssetEntry[] EntriesOf(string path)
          {
  #if UNITY_EDITOR
              var entries = new System.Collections.Generic.List<SaveSystemSettings.AssetEntry>();
              foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
              {
                  if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                      entries.Add(new SaveSystemSettings.AssetEntry
                      {
                          Key = SaveSystemSettings.AssetKey(guid, localId, AssetDatabase.IsMainAsset(asset)),
                          Asset = asset
                      });
              }

              return entries.ToArray();
  #else
              return new SaveSystemSettings.AssetEntry[0];
  #endif
          }

          public static SaveSystemSettings.AssetEntry[] IconEntries() => EntriesOf(ICON);
  ```

  и в `CreateSettings` перед `return settings;` — `settings.SetAssetCatalog(IconEntries());`.

- [x] **Шаг 3: падающие тесты.**

  `Tests/Editor/SaveReferencesTests.cs`:

  ```csharp
  using System.Text.RegularExpressions;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Saving.Tests
  {
      /// <summary>References to scene objects, prefabs and assets, and what happens to anything else.</summary>
      public class SaveReferencesTests
      {
          private TempScene _scene;
          private SaveSystemSettings _settings;
          private Saveable _owner;

          [SetUp]
          public void SetUp()
          {
              _scene = new TempScene();
              _settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              SaveReferences.Settings = _settings;
              _owner = new GameObject("Owner").AddComponent<Saveable>();
              _owner.SetId(System.Guid.NewGuid().ToString("N"));
              SaveRegistry.Register(_owner);
          }

          [TearDown]
          public void TearDown()
          {
              SaveRegistry.Unregister(_owner);
              SaveReferences.Settings = null;
              Object.DestroyImmediate(_settings);
              _scene.Dispose();
          }

          private GameObject Child(string name)
          {
              var child = new GameObject(name);
              child.transform.SetParent(_owner.transform, false);
              return child;
          }

          [Test]
          public void Ref_ComponentOnChild_IdPathAndComponent()
          {
              var renderer = Child("Visual").AddComponent<SpriteRenderer>();

              JToken token = SaveReferences.Write(renderer);

              Assert.AreEqual(_owner.Id, (string)token["id"]);
              Assert.AreEqual("Visual", (string)token["path"]);
              Assert.AreEqual("SpriteRenderer", (string)token["component"]);
              Assert.AreSame(renderer, SaveReferences.Read(token, typeof(SpriteRenderer)));
          }

          [Test]
          public void Ref_GameObjectAndTransform_RoundTrip()
          {
              GameObject child = Child("Hand");

              JToken token = SaveReferences.Write(child);

              Assert.IsNull(token["component"]);
              Assert.AreSame(child, SaveReferences.Read(token, typeof(GameObject)));
              Assert.AreSame(child.transform, SaveReferences.Read(token, typeof(Transform)));
          }

          [Test]
          public void Ref_SecondComponentOfType_Numbered()
          {
              _owner.gameObject.AddComponent<BoxCollider2D>();
              var second = _owner.gameObject.AddComponent<BoxCollider2D>();

              JToken token = SaveReferences.Write(second);

              Assert.AreEqual("BoxCollider2D#1", (string)token["component"]);
              Assert.IsNull(token["path"], "a component of the Saveable's own object has no path");
              Assert.AreSame(second, SaveReferences.Read(token, typeof(BoxCollider2D)));
          }

          [Test]
          public void Ref_Prefab_ByPrefabId()
          {
              var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestScenes.PREFAB);
              string id = prefab.GetComponent<Saveable>().PrefabId;
              _settings.SetCatalogs(new[] { new SaveSystemSettings.PrefabEntry { Id = id, Prefab = prefab } },
                  new SaveSystemSettings.SceneEntry[0]);

              JToken token = SaveReferences.Write(prefab);

              Assert.AreEqual(id, (string)token["prefab"]);
              Assert.AreSame(prefab, SaveReferences.Read(token, typeof(GameObject)));
          }

          [Test]
          public void Ref_Asset_ByKey()
          {
              var icon = AssetDatabase.LoadAssetAtPath<Sprite>(TestScenes.ICON);
              _settings.SetAssetCatalog(TestScenes.IconEntries());

              JToken token = SaveReferences.Write(icon);

              StringAssert.Contains(AssetDatabase.AssetPathToGUID(TestScenes.ICON) + ":", (string)token["asset"]);
              Assert.AreSame(icon, SaveReferences.Read(token, typeof(Sprite)));
          }

          [Test]
          public void Ref_SceneObjectOutsideSaveable_NullWithWarning()
          {
              var loose = new GameObject("Loose" + System.Guid.NewGuid().ToString("N"));

              LogAssert.Expect(LogType.Warning, new Regex("outside any Saveable"));
              JToken token = SaveReferences.Write(loose);

              Assert.AreEqual(JTokenType.Null, token.Type);
          }

          [Test]
          public void Ref_Missing_NullWithWarning()
          {
              LogAssert.Expect(LogType.Warning, new Regex("was not found"));

              Assert.IsNull(SaveReferences.Read(JObject.Parse("{ \"id\": \"no-such-id\" }"), typeof(GameObject)));
          }

          [Test]
          public void Fields_References_RoundTrip()
          {
              var links = _owner.gameObject.AddComponent<SaveTestLinks>();
              GameObject handle = Child("Handle");
              var icon = AssetDatabase.LoadAssetAtPath<Sprite>(TestScenes.ICON);
              _settings.SetAssetCatalog(TestScenes.IconEntries());
              links.Target = _owner;
              links.Handle = handle.transform;
              links.Icon = icon;
              links.Many.Add(_owner);
              IComponentSerializer serializer = ComponentSerializerCatalog.Default.For(typeof(SaveTestLinks));
              JToken data = serializer.Capture(links);

              links.Target = null;
              links.Handle = null;
              links.Icon = null;
              links.Many.Clear();
              serializer.Restore(links, data);

              Assert.AreSame(_owner, links.Target);
              Assert.AreSame(handle.transform, links.Handle);
              Assert.AreSame(icon, links.Icon);
              Assert.AreSame(_owner, links.Many[0]);
          }
      }
  }
  ```

  В `Tests/Editor/SaveCatalogTests.cs`:

  ```csharp
          [Test]
          public void Rebuild_AssetTableFromFolders()
          {
              const string art = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/Art";
              var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              settings.SetAssetFolders(new[] { art });

              SaveCatalogBuilder.Rebuild(settings);

              var icon = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/SaveTestIcon.png");
              var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "/SaveTestIcon.png");
              Assert.IsTrue(settings.TryGetAssetKey(icon, out string iconKey));
              Assert.IsTrue(settings.TryGetAssetKey(texture, out string textureKey));
              Assert.AreEqual(AssetDatabase.AssetPathToGUID(art + "/SaveTestIcon.png"), textureKey);
              Assert.IsTrue(settings.TryGetAsset(iconKey, out Object found));
              Assert.AreSame(icon, found);
              Object.DestroyImmediate(settings);
          }

          [Test]
          public void Validate_StaleAssetTable_Reported()
          {
              var settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              settings.SetAssetFolders(new[] { FOLDER });
              SaveCatalogBuilder.Rebuild(settings);
              Assert.IsTrue(AssetDatabase.CopyAsset(TestScenes.ICON, FOLDER + "/LateIcon.png"));

              Assert.IsTrue(SaveValidator.ValidateCatalogs(settings).Exists(problem => problem.Contains("LateIcon")));
              Object.DestroyImmediate(settings);
          }
  ```

  В `Tests/Runtime/SceneSavingTests.cs`:

  ```csharp
          [UnityTest]
          public IEnumerator Scene_References_ResolvedAfterReload()
          {
              var links = TestScenes.Find("Linker").GetComponent<SaveTestLinks>();
              links.Target = TestScenes.Find("Hidden").GetComponent<Saveable>();
              links.Counter = Counter("Box");
              links.Handle = TestScenes.Find("Box").transform.Find("Handle");
              links.Prefab = TestScenes.Prefab;
              links.Icon = TestScenes.Icon;
              links.Many.Add(TestScenes.Find("Doomed").GetComponent<Saveable>());
              _system.Save();

              yield return LoadAndWait();

              links = TestScenes.Find("Linker").GetComponent<SaveTestLinks>();
              Assert.AreSame(TestScenes.Find("Hidden").GetComponent<Saveable>(), links.Target);
              Assert.AreSame(Counter("Box"), links.Counter);
              Assert.AreSame(TestScenes.Find("Box").transform.Find("Handle"), links.Handle);
              Assert.AreSame(TestScenes.Prefab, links.Prefab);
              Assert.AreSame(TestScenes.Icon, links.Icon);
              Assert.AreSame(TestScenes.Find("Doomed").GetComponent<Saveable>(), links.Many[0]);
          }

          [UnityTest]
          public IEnumerator Scene_ReferenceToSpawned_ResolvedAfterReload()
          {
              GameObject spawned = Object.Instantiate(TestScenes.Prefab);
              yield return null;
              string id = spawned.GetComponent<Saveable>().Id;
              TestScenes.Find("Linker").GetComponent<SaveTestLinks>().Target = spawned.GetComponent<Saveable>();
              _system.Save();

              yield return LoadAndWait();

              Saveable target = TestScenes.Find("Linker").GetComponent<SaveTestLinks>().Target;
              Assert.IsNotNull(target);
              Assert.AreEqual(id, target.Id);
          }
  ```

  (В тестовых сценах ссылка `Box` → `Doomed` уцелеет: `Doomed` в этом тесте не уничтожается.)

- [x] **Шаг 4: запуск — красный.** All Tests: падают 8 `SaveReferencesTests` и 2 новых `SaveCatalogTests`
  (`NotImplementedException`), 2 новых `SceneSavingTests` — на проверках: поля `[Save]` со ссылками пока пропускаются
  (часть 2), после загрузки они `null`. Остальные тесты зелёные.

- [x] **Шаг 5: реализация.**

  `Runtime/Objects/SaveSystemSettings.cs` — `AssetKey`, `SetAssetFolders`, `SetAssetCatalog` и заглушки `TryGet…`
  заменить на (`using System.Linq;` уже есть):

  ```csharp
          [NonSerialized] private Dictionary<string, UnityEngine.Object> _assetsByKey;
          [NonSerialized] private Dictionary<UnityEngine.Object, string> _keysByAsset;

          /// <summary>An asset's key in the table: its GUID; for one inside another (a sprite of a texture), GUID:localId.</summary>
          public static string AssetKey(string guid, long localId, bool isMainAsset) =>
              isMainAsset ? guid : $"{guid}:{localId}";

          public bool TryGetAsset(string key, out UnityEngine.Object asset)
          {
              BuildLookups();
              asset = null;
              return !string.IsNullOrEmpty(key) && _assetsByKey.TryGetValue(key, out asset) && asset != null;
          }

          public bool TryGetAssetKey(UnityEngine.Object asset, out string key)
          {
              BuildLookups();
              key = null;
              return asset != null && _keysByAsset.TryGetValue(asset, out key);
          }

          public void SetAssetFolders(IEnumerable<string> folders) => _assetFolders = folders.ToList();

          /// <summary>Replaces the asset table: the editor fills it from the folders, tests by hand.</summary>
          public void SetAssetCatalog(IEnumerable<AssetEntry> assets)
          {
              _assets = assets.ToList();
              ForgetLookups();
          }

          private void OnValidate() => ForgetLookups();

          private void ForgetLookups()
          {
              _assetsByKey = null;
              _keysByAsset = null;
          }

          private void BuildLookups()
          {
              if (_assetsByKey != null)
                  return;

              _assetsByKey = new Dictionary<string, UnityEngine.Object>();
              _keysByAsset = new Dictionary<UnityEngine.Object, string>();
              foreach (AssetEntry entry in _assets)
              {
                  if (entry == null || entry.Asset == null || string.IsNullOrEmpty(entry.Key))
                      continue;

                  _assetsByKey[entry.Key] = entry.Asset;
                  _keysByAsset[entry.Asset] = entry.Key;
              }
          }
  ```

  Описание класса дописать: `…scenes by GUID and path, and assets of the chosen folders by key for references.`

  `Runtime/Objects/SaveReferences.cs`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using UnityEngine;
  using Object = UnityEngine.Object;

  namespace Saving.Objects
  {
      /// <summary>
      /// References to Unity objects inside saved data. A scene object is the nearest Saveable above it, the path from
      /// it and the component on that object; a prefab is its prefab id; an asset is its key in the asset table of the
      /// settings. Anything else is saved as null with a warning (once per object); a reference not found on load is
      /// null with a warning. The objects layer sets <see cref="Settings"/>.
      /// </summary>
      internal static class SaveReferences
      {
          private static readonly HashSet<string> Warned = new();

          public static SaveSystemSettings Settings { get; set; }

          [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
          private static void Reset()
          {
              Warned.Clear();
              Settings = null;
          }

          public static JToken Write(Object target)
          {
              if (target == null)
                  return JValue.CreateNull();

              GameObject go = target as GameObject;
              if (go == null && target is Component component)
                  go = component.gameObject;

              if (go != null && go.scene.IsValid())
              {
                  Saveable owner = go.GetComponentInParent<Saveable>(true);
                  if (owner == null || string.IsNullOrEmpty(owner.Id))
                      return Unresolved(target, "is a scene object outside any Saveable");

                  var json = new JObject { ["id"] = owner.Id };
                  string path = ComponentKeys.PathOf(owner.transform, go.transform);
                  if (path.Length > 0)
                      json["path"] = path;

                  if (target is Component targetComponent)
                      json["component"] = NameOf(targetComponent);

                  return json;
              }

              if (target is GameObject prefab && prefab.TryGetComponent(out Saveable root) && !string.IsNullOrEmpty(root.PrefabId)
                  && Settings != null && Settings.TryGetPrefab(root.PrefabId, out _))
                  return new JObject { ["prefab"] = root.PrefabId };

              if (Settings != null && Settings.TryGetAssetKey(target, out string key))
                  return new JObject { ["asset"] = key };

              return Unresolved(target, "is not a Saveable, a prefab with a Saveable or an asset of the settings' folders");
          }

          public static Object Read(JToken token, Type type)
          {
              if (token == null || token.Type == JTokenType.Null)
                  return null;

              Object found = null;
              if (token is JObject json)
              {
                  if (json.TryGetValue("id", out JToken id))
                  {
                      if (SaveRegistry.TryGet((string)id, out Saveable owner))
                      {
                          Transform node = ComponentKeys.FindChild(owner.transform, (string)json["path"]);
                          string name = (string)json["component"];
                          if (node != null)
                              found = name == null ? node.gameObject : FindComponent(node.gameObject, name);
                      }
                  }
                  else if (json.TryGetValue("prefab", out JToken prefabId))
                  {
                      if (Settings != null && Settings.TryGetPrefab((string)prefabId, out GameObject prefab))
                          found = prefab;
                  }
                  else if (json.TryGetValue("asset", out JToken key))
                  {
                      if (Settings != null && Settings.TryGetAsset((string)key, out Object asset))
                          found = asset;
                  }
              }

              found = Fit(found, type);
              if (found == null)
                  Debug.LogWarning($"The saved reference {token.ToString(Formatting.None)} was not found; it is set to null.");

              return found;
          }

          /// <summary>The component's type name on its object; a second one of that type is "Type#1".</summary>
          private static string NameOf(Component component)
          {
              string type = component.GetType().Name;
              int index = 0;
              foreach (Component other in component.GetComponents<Component>())
              {
                  if (other == component)
                      break;

                  if (other != null && other.GetType().Name == type)
                      index++;
              }

              return index == 0 ? type : $"{type}#{index}";
          }

          private static Component FindComponent(GameObject go, string name)
          {
              int hash = name.LastIndexOf('#');
              string type = name;
              int wanted = 0;
              if (hash > 0 && int.TryParse(name.Substring(hash + 1), out int number))
              {
                  type = name.Substring(0, hash);
                  wanted = number;
              }

              int seen = 0;
              foreach (Component component in go.GetComponents<Component>())
              {
                  if (component == null || component.GetType().Name != type)
                      continue;

                  if (seen == wanted)
                      return component;

                  seen++;
              }

              return null;
          }

          /// <summary>A GameObject where a component is wanted gives its component, a component gives its GameObject.</summary>
          private static Object Fit(Object found, Type type)
          {
              if (found == null || type.IsInstanceOfType(found))
                  return found;

              if (found is GameObject go && typeof(Component).IsAssignableFrom(type))
                  return go.GetComponent(type);

              if (found is Component component && type == typeof(GameObject))
                  return component.gameObject;

              return null;
          }

          private static JToken Unresolved(Object target, string why)
          {
              if (Warned.Add($"{target.GetType().Name}/{target.name}"))
                  Debug.LogWarning($"'{target.name}' ({target.GetType().Name}) {why}; it is saved as null.", target);

              return JValue.CreateNull();
          }
      }
  }
  ```

  `Runtime/Objects/UnityReferenceConverter.cs`:

  ```csharp
  using System;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using Object = UnityEngine.Object;

  namespace Saving.Objects
  {
      /// <summary>Writes every UnityEngine.Object as a reference (<see cref="SaveReferences"/>) instead of walking it.</summary>
      internal sealed class UnityReferenceConverter : JsonConverter
      {
          public override bool CanConvert(Type objectType) => typeof(Object).IsAssignableFrom(objectType);

          public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
              SaveReferences.Write(value as Object).WriteTo(writer);

          public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
              SaveReferences.Read(JToken.Load(reader), objectType);
      }
  }
  ```

  `Runtime/Objects/ObjectsJson.cs`: в `Serializer` строку `Converters = UnityJsonConverters.Create()` заменить на
  `Converters = Converters()`; после `Serializer` добавить:

  ```csharp
          /// <summary>For the data of component serializers: the SaveJson setup plus references.</summary>
          public static readonly JsonSerializer Components = JsonSerializer.Create(new JsonSerializerSettings
          {
              ContractResolver = SaveJson.Settings.ContractResolver,
              ObjectCreationHandling = ObjectCreationHandling.Replace,
              TypeNameHandling = TypeNameHandling.None,
              DateParseHandling = DateParseHandling.None,
              Converters = Converters()
          });

          public static JToken ComponentToToken<T>(T value) =>
              value == null ? JValue.CreateNull() : JToken.FromObject(value, Components);

          public static T ComponentFromToken<T>(JToken token) =>
              token == null || token.Type == JTokenType.Null ? default : token.ToObject<T>(Components);

          private static IList<JsonConverter> Converters()
          {
              IList<JsonConverter> converters = UnityJsonConverters.Create();
              converters.Add(new UnityReferenceConverter());
              return converters;
          }
  ```

  (Порядок полей важен: `Converters()` — метод, его можно звать из инициализаторов в любом месте класса.) В
  описание класса: «…and Unity objects as references».

  `Runtime/Objects/ComponentSerializer.cs`: `SaveJson.ToToken(Serialize(...))` → `ObjectsJson.ComponentToToken(Serialize(...))`,
  `SaveJson.FromToken<TData>(data)` → `ObjectsJson.ComponentFromToken<TData>(data)`; в описание — «Unity objects in
  TData are saved as references».

  `Runtime/Objects/FieldsSerializer.cs`: условие пропуска полей —
  `if (attribute == null || field.IsInitOnly) continue;` (комментарий над ним: `// readonly fields are skipped;
  static never comes with these flags`), из описания класса убрать про пропуск ссылок.

  `Runtime/Objects/SceneSaving.cs`: в конструкторе после подписок — `SaveReferences.Settings = settings;`; в `Dispose`:

  ```csharp
              if (SaveReferences.Settings == _settings)
                  SaveReferences.Settings = null;
  ```

  `Editor/SaveCatalogBuilder.cs`:
  - заглушку `BuildAssetEntries` заменить на:

    ```csharp
          /// <summary>Every asset of the settings' folders, sprites inside textures too; prefabs and scenes have catalogs of their own.</summary>
          public static List<SaveSystemSettings.AssetEntry> BuildAssetEntries(SaveSystemSettings settings)
          {
              string[] folders = settings.AssetFolders.Where(AssetDatabase.IsValidFolder).ToArray();
              var entries = new Dictionary<string, SaveSystemSettings.AssetEntry>();
              if (folders.Length == 0)
                  return new List<SaveSystemSettings.AssetEntry>();

              foreach (string path in AssetDatabase.FindAssets("t:Object", folders).Select(AssetDatabase.GUIDToAssetPath).Distinct())
              {
                  if (AssetDatabase.IsValidFolder(path) || path.EndsWith(".unity") || path.EndsWith(".prefab")
                      || path.EndsWith(".cs") || path.EndsWith(".asmdef"))
                      continue;

                  foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                  {
                      if (asset == null || asset is GameObject || asset is Component
                          || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                          continue;

                      string key = SaveSystemSettings.AssetKey(guid, localId, AssetDatabase.IsMainAsset(asset));
                      entries[key] = new SaveSystemSettings.AssetEntry { Key = key, Asset = asset };
                  }
              }

              return entries.Values.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList();
          }
    ```

  - `Rebuild` целиком:

    ```csharp
          /// <summary>Rebuilds the catalogs; the asset table only when asked (loading every asset of the folders costs time).</summary>
          public static void Rebuild(SaveSystemSettings settings, bool includeAssets = true)
          {
              List<SaveSystemSettings.PrefabEntry> prefabs = BuildPrefabEntries();
              List<SaveSystemSettings.SceneEntry> scenes = BuildSceneEntries();
              List<SaveSystemSettings.AssetEntry> assets = includeAssets ? BuildAssetEntries(settings) : null;
              bool catalogsSame = Same(settings, prefabs, scenes);
              bool assetsSame = assets == null || SameAssets(settings, assets);
              if (catalogsSame && assetsSame)
                  return;

              if (!catalogsSame)
                  settings.SetCatalogs(prefabs, scenes);
              if (!assetsSame)
                  settings.SetAssetCatalog(assets);

              if (!EditorUtility.IsPersistent(settings))
                  return;

              EditorUtility.SetDirty(settings);
              AssetDatabase.SaveAssetIfDirty(settings);
          }
    ```

  - метод сравнения таблиц:

    ```csharp
          private static bool SameAssets(SaveSystemSettings settings, List<SaveSystemSettings.AssetEntry> assets) =>
              settings.Assets.Count == assets.Count
              && settings.Assets.Zip(assets, (a, b) => a.Key == b.Key && a.Asset == b.Asset).All(same => same);
    ```

  `Editor/SaveValidator.cs` — в конец `ValidateCatalogs` перед `return problems;`:

  ```csharp
              List<SaveSystemSettings.AssetEntry> assets = SaveCatalogBuilder.BuildAssetEntries(settings);
              foreach (SaveSystemSettings.AssetEntry entry in assets.Where(entry => !settings.Assets.Any(known => known.Key == entry.Key && known.Asset == entry.Asset)))
                  problems.Add($"{settings.name}: the asset '{entry.Asset.name}' is missing from the asset table");
              foreach (SaveSystemSettings.AssetEntry entry in settings.Assets.Where(known => !assets.Any(entry => entry.Key == known.Key)))
                  problems.Add($"{settings.name}: the asset {entry.Key} is in the asset table but not in its folders any more");
  ```

  `Editor/SaveCatalogPostprocessor.cs` — пересборка и при изменениях в папках ассетов (таблица — только тогда):

  ```csharp
          private static readonly HashSet<string> TouchedPrefabs = new();
          private static bool _scheduled;
          private static bool _assetsTouched;

          private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
          {
              string[] all = imported.Concat(deleted).Concat(moved).Concat(movedFrom).ToArray();
              bool catalogs = all.Any(path => path.EndsWith(".prefab") || path.EndsWith(".unity"));
              bool assets = all.Any(InAssetFolders);
              if (!catalogs && !assets)
                  return;

              foreach (string path in imported.Concat(moved).Where(path => path.EndsWith(".prefab")))
                  TouchedPrefabs.Add(path);

              _assetsTouched |= assets;
              if (_scheduled)
                  return;

              _scheduled = true;
              EditorApplication.delayCall += Run;
          }

          private static bool InAssetFolders(string path) =>
              SaveCatalogBuilder.AllSettings().Any(settings =>
                  settings.AssetFolders.Any(folder => path.StartsWith(folder.TrimEnd('/') + "/", System.StringComparison.Ordinal)));
  ```

  и в `Run` вызов пересборки — `SaveCatalogBuilder.Rebuild(settings, includeAssets: assets);`, где перед циклом
  `bool assets = _assetsTouched; _assetsTouched = false;` (рядом с `TouchedPrefabs.Clear();`). `InAssetFolders`
  зовётся, только если путь не префаб и не сцена, — `AllSettings()` ищет ассеты настроек по базе и быстр.

  `Editor/SaveSystemSettingsEditor.cs` — перед блоком префабов:

  ```csharp
              serializedObject.Update();
              SerializedProperty folders = serializedObject.FindProperty("_assetFolders");
              EditorGUILayout.LabelField("Asset folders for references", EditorStyles.boldLabel);
              for (int index = 0; index < folders.arraySize; index++)
              {
                  using (new EditorGUILayout.HorizontalScope())
                  {
                      SerializedProperty folder = folders.GetArrayElementAtIndex(index);
                      folder.stringValue = EditorGUILayout.TextField(folder.stringValue);
                      if (GUILayout.Button("-", GUILayout.Width(24f)))
                      {
                          folders.DeleteArrayElementAtIndex(index);
                          break;
                      }
                  }
              }

              if (GUILayout.Button("Add folder"))
              {
                  folders.InsertArrayElementAtIndex(folders.arraySize);
                  folders.GetArrayElementAtIndex(folders.arraySize - 1).stringValue = "Assets/";
              }

              serializedObject.ApplyModifiedProperties();
              EditorGUILayout.LabelField($"Assets in the table: {settings.Assets.Count}");
              EditorGUILayout.Space();
  ```

- [x] **Шаг 6: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 7: папки игры.** `Unity_RunCommand`:

  ```csharp
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;

  internal class CommandScript : IRunCommand
  {
      public void Execute(ExecutionResult result)
      {
          var settings = AssetDatabase.LoadAssetAtPath<SaveSystemSettings>("Assets/Game/Configs/SaveSystemSettings.asset");
          settings.SetAssetFolders(new[] { "Assets/Game/Art", "Assets/Game/Audio" });
          EditorUtility.SetDirty(settings);
          SaveCatalogBuilder.Rebuild(settings);
          AssetDatabase.SaveAssets();
          result.Log("{0} assets in the table", settings.Assets.Count);
      }
  }
  ```

  Ожидание — несколько сотен записей (текстуры, их спрайты, звуки). Затем All Tests ещё раз: `SaveSetupTests` (каталоги
  актуальны) — зелёные.

- [x] **Шаг 8: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem Assets/Game/Configs/SaveSystemSettings.asset
  git commit -m "Save references to Saveables, prefabs and assets"
  ```

---

### Задача 5: `SpriteRenderer` и `AudioSource`

**Files:**
- Create: `Runtime/Objects/BuiltIn/SpriteRendererSerializer.cs`, `Runtime/Objects/BuiltIn/AudioSourceSerializer.cs`
- Create (командой): `Tests/Runtime/Audio/SaveTestTone.wav`
- Modify: `Tests/Runtime/TestScenes.cs` (+ `TONE`, `Tone`), `Tests/Runtime/BuiltInSerializerTests.cs` (+ 3 теста)

**Interfaces:**
- Consumes: `ComponentSerializer<,>` с ссылками, `SaveReferences.Settings`, `TestScenes.EntriesOf` (задача 4),
  `SAVE_AUDIO` (задача 1).
- Produces: встроенные `SpriteRendererSerializer` (спрайт — ссылкой, цвет, `flipX`, `flipY`) и `AudioSourceSerializer`
  (клип — ссылкой, время, играет / пауза, громкость, высота, `mute`).

- [x] **Шаг 1: заглушки.**

  `Runtime/Objects/BuiltIn/SpriteRendererSerializer.cs`:

  ```csharp
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class SpriteRendererSerializer : ComponentSerializer<SpriteRenderer, SpriteRendererSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(SpriteRenderer renderer) => throw new NotImplementedException();
          protected override void Deserialize(SpriteRenderer renderer, Data data) => throw new NotImplementedException();
      }
  }
  ```

  `Runtime/Objects/BuiltIn/AudioSourceSerializer.cs`:

  ```csharp
  #if SAVE_AUDIO
  using System;
  using UnityEngine;

  namespace Saving.Objects
  {
      public sealed class AudioSourceSerializer : ComponentSerializer<AudioSource, AudioSourceSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(AudioSource source) => throw new NotImplementedException();
          protected override void Deserialize(AudioSource source, Data data) => throw new NotImplementedException();
      }
  }
  #endif
  ```

- [x] **Шаг 2: тестовый звук.** `Refresh`, затем `Unity_RunCommand` (тишина 2 с, 8 кГц, 16 бит, моно):

  ```csharp
  using System.IO;
  using System.Text;
  using UnityEditor;

  internal class CommandScript : IRunCommand
  {
      private const string PARENT = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime";
      private const string TONE = PARENT + "/Audio/SaveTestTone.wav";

      public void Execute(ExecutionResult result)
      {
          if (!AssetDatabase.IsValidFolder(PARENT + "/Audio"))
              AssetDatabase.CreateFolder(PARENT, "Audio");

          const int rate = 8000;
          const int samples = rate * 2;
          using (var stream = new MemoryStream())
          using (var writer = new BinaryWriter(stream))
          {
              writer.Write(Encoding.ASCII.GetBytes("RIFF"));
              writer.Write(36 + samples * 2);
              writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
              writer.Write(16);
              writer.Write((short)1);      // PCM
              writer.Write((short)1);      // mono
              writer.Write(rate);
              writer.Write(rate * 2);      // bytes per second
              writer.Write((short)2);      // block align
              writer.Write((short)16);     // bits per sample
              writer.Write(Encoding.ASCII.GetBytes("data"));
              writer.Write(samples * 2);
              writer.Write(new byte[samples * 2]);
              File.WriteAllBytes(TONE, stream.ToArray());
          }

          AssetDatabase.ImportAsset(TONE);
          result.Log("Created {0}", TONE);
      }
  }
  ```

  В `Tests/Runtime/TestScenes.cs`:

  ```csharp
          public const string TONE = "Assets/Game/Scripts/Modules/SaveSystem/Tests/Runtime/Audio/SaveTestTone.wav";

          public static AudioClip Tone =>
  #if UNITY_EDITOR
              AssetDatabase.LoadAssetAtPath<AudioClip>(TONE);
  #else
              null;
  #endif
  ```

- [x] **Шаг 3: падающие тесты.** В `Tests/Runtime/BuiltInSerializerTests.cs`: поле `private SaveSystemSettings _settings;`,
  в начало класса:

  ```csharp
          [SetUp]
          public void SetUp()
          {
              _settings = ScriptableObject.CreateInstance<SaveSystemSettings>();
              var entries = new List<SaveSystemSettings.AssetEntry>(TestScenes.EntriesOf(TestScenes.ICON));
              entries.AddRange(TestScenes.EntriesOf(TestScenes.TONE));
              _settings.SetAssetCatalog(entries);
              SaveReferences.Settings = _settings;
          }
  ```

  в `TearDown` в конец — `SaveReferences.Settings = null; Object.DestroyImmediate(_settings);`, и тесты:

  ```csharp
          [Test]
          public void SpriteRenderer_SpriteColorAndFlip_RoundTrip()
          {
              var renderer = Make("Sprite").AddComponent<SpriteRenderer>();
              renderer.sprite = TestScenes.Icon;
              renderer.color = new Color(1f, 0.5f, 0.25f, 0.8f);
              renderer.flipX = true;
              JToken data = For<SpriteRenderer>().Capture(renderer);

              renderer.sprite = null;
              renderer.color = Color.white;
              renderer.flipX = false;
              renderer.flipY = true;
              For<SpriteRenderer>().Restore(renderer, data);

              Assert.AreSame(TestScenes.Icon, renderer.sprite);
              Assert.AreEqual(new Color(1f, 0.5f, 0.25f, 0.8f), renderer.color);
              Assert.IsTrue(renderer.flipX);
              Assert.IsFalse(renderer.flipY);
              Assert.IsNotNull(data["sprite"]["asset"], "the sprite is saved as an asset reference");
          }

  #if SAVE_AUDIO
          [UnityTest]
          public IEnumerator AudioSource_PlayingClipAndTime_RoundTrip()
          {
              var source = Make("Audio").AddComponent<AudioSource>();
              source.clip = TestScenes.Tone;
              source.volume = 0.3f;
              source.pitch = 1.2f;
              source.Play();
              yield return new WaitForSeconds(0.3f);
              float time = source.time;
              Assert.Greater(time, 0f, "precondition: the clip is playing");
              JToken data = For<AudioSource>().Capture(source);

              source.Stop();
              source.clip = null;
              source.volume = 1f;
              source.pitch = 1f;
              For<AudioSource>().Restore(source, data);

              Assert.AreSame(TestScenes.Tone, source.clip);
              Assert.IsTrue(source.isPlaying);
              Assert.AreEqual(time, source.time, 0.1f);
              Assert.AreEqual(0.3f, source.volume, 1e-4f);
              Assert.AreEqual(1.2f, source.pitch, 1e-4f);
          }

          [UnityTest]
          public IEnumerator AudioSource_Paused_StaysPaused()
          {
              var source = Make("Paused").AddComponent<AudioSource>();
              source.clip = TestScenes.Tone;
              source.Play();
              yield return new WaitForSeconds(0.3f);
              source.Pause();
              float time = source.time;
              JToken data = For<AudioSource>().Capture(source);

              source.Stop();
              For<AudioSource>().Restore(source, data);

              Assert.IsFalse(source.isPlaying);
              Assert.AreEqual(time, source.time, 0.1f);
          }
  #endif
  ```

- [x] **Шаг 4: запуск — красный.** PlayMode: 3 новых теста падают с `NotImplementedException`. Если
  `AudioSource_PlayingClipAndTime_RoundTrip` падает уже на предусловии (`time` равно 0) — звук в Play Mode эдитора не
  идёт: остановиться (допущение плана).

- [x] **Шаг 5: реализация.**

  `Runtime/Objects/BuiltIn/SpriteRendererSerializer.cs`:

  ```csharp
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>Built in: the sprite (an asset reference: its folder must be in the settings), color, flipX, flipY. A sprite
      /// outside the folders is saved as null and the current one stays on load.</summary>
      public sealed class SpriteRendererSerializer : ComponentSerializer<SpriteRenderer, SpriteRendererSerializer.Data>
      {
          public sealed class Data
          {
              public Sprite Sprite;
              public bool HasSprite;
              public Color Color = Color.white;
              public bool FlipX;
              public bool FlipY;
          }

          protected override Data Serialize(SpriteRenderer renderer) => new()
          {
              Sprite = renderer.sprite,
              HasSprite = renderer.sprite != null,
              Color = renderer.color,
              FlipX = renderer.flipX,
              FlipY = renderer.flipY
          };

          protected override void Deserialize(SpriteRenderer renderer, Data data)
          {
              // A sprite that could not be referenced comes back null: keep the current one instead of wiping it
              if (data.Sprite != null || !data.HasSprite)
                  renderer.sprite = data.Sprite;

              renderer.color = data.Color;
              renderer.flipX = data.FlipX;
              renderer.flipY = data.FlipY;
          }
      }
  }
  ```

  `Runtime/Objects/BuiltIn/AudioSourceSerializer.cs`:

  ```csharp
  #if SAVE_AUDIO
  using UnityEngine;

  namespace Saving.Objects
  {
      /// <summary>
      /// Built in: the clip (an asset reference), the time, playing or paused, volume, pitch, mute. Paused means not
      /// playing with the time past the start.
      /// </summary>
      public sealed class AudioSourceSerializer : ComponentSerializer<AudioSource, AudioSourceSerializer.Data>
      {
          public sealed class Data
          {
              public AudioClip Clip;
              public bool HasClip;
              public float Time;
              public bool Playing;
              public bool Paused;
              public float Volume = 1f;
              public float Pitch = 1f;
              public bool Mute;
          }

          protected override Data Serialize(AudioSource source) => new()
          {
              Clip = source.clip,
              HasClip = source.clip != null,
              Time = source.time,
              Playing = source.isPlaying,
              Paused = !source.isPlaying && source.clip != null && source.time > 0f,
              Volume = source.volume,
              Pitch = source.pitch,
              Mute = source.mute
          };

          protected override void Deserialize(AudioSource source, Data data)
          {
              source.Stop();
              if (data.Clip != null || !data.HasClip)
                  source.clip = data.Clip;

              source.volume = data.Volume;
              source.pitch = data.Pitch;
              source.mute = data.Mute;
              if (source.clip == null || (!data.Playing && !data.Paused))
                  return;

              // Play starts from the beginning: the time goes after it
              source.Play();
              source.time = Mathf.Clamp(data.Time, 0f, Mathf.Max(0f, source.clip.length - 0.01f));
              if (data.Paused)
                  source.Pause();
          }
      }
  }
  #endif
  ```

- [x] **Шаг 6: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 7: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem
  git commit -m "Save sprite renderers and audio sources"
  ```

---

### Задача 6: снимок одного объекта и `SpriteAnimatorSerializer` в игре

**Files:**
- Modify: `Runtime/Objects/Saveable.cs` (+ `CaptureState`, `RestoreState`)
- Test: `Tests/Runtime/SaveableStateTests.cs`
- Modify: `Assets/Game/Game.asmdef` (+ `SaveSystem.Objects`),
  `Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef` (+ `SaveSystem.Objects`, `Newtonsoft.Json.dll`)
- Create: `Assets/Game/Scripts/Gameplay/Character/Components/Visual/SpriteAnimatorSerializer.cs`
- Test: `Assets/Game/Tests/PlayMode/SpriteAnimatorSerializerTests.cs`

**Interfaces:**
- Consumes: `ObjectState`, `ObjectData`, `ComponentSerializerCatalog.Default` (часть 2); `SpriteAnimator`
  (`CurrentClip`, `NormalizedTime`, `IsPaused`, `Speed`, `TryGetClip`, `Play(clip, normalizedTime)`, `Pause`);
  `CharacterTestBase` (`Rig`), `CharacterTestRig.SpawnPlayer` (игра).
- Produces: `Saveable.CaptureState() → JToken`, `Saveable.RestoreState(JToken)`; `Gameplay.SpriteAnimatorSerializer`
  (`Id` клипа, нормализованное время, пауза, скорость; клипа нет в библиотеке — данные пропускаются).

- [x] **Шаг 1: заглушки.**

  В `Runtime/Objects/Saveable.cs` (`using Newtonsoft.Json.Linq;`) после `SetOption`:

  ```csharp
          public JToken CaptureState() => throw new NotImplementedException();
          public void RestoreState(JToken state) => throw new NotImplementedException();
  ```

  `Assets/Game/Game.asmdef` — в `references` после `"SaveSystem",` добавить `"SaveSystem.Objects",`.
  `Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef` — после `"SaveSystem",` добавить `"SaveSystem.Objects",`, в
  `precompiledReferences` после `"nunit.framework.dll"` — `"Newtonsoft.Json.dll"`.

  `Assets/Game/Scripts/Gameplay/Character/Components/Visual/SpriteAnimatorSerializer.cs`:

  ```csharp
  using System;
  using Saving.Objects;
  using SpriteAnimation;

  namespace Gameplay
  {
      public sealed class SpriteAnimatorSerializer : ComponentSerializer<SpriteAnimator, SpriteAnimatorSerializer.Data>
      {
          public sealed class Data
          {
          }

          protected override Data Serialize(SpriteAnimator animator) => throw new NotImplementedException();
          protected override void Deserialize(SpriteAnimator animator, Data data) => throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 2: падающие тесты.**

  `Tests/Runtime/SaveableStateTests.cs`:

  ```csharp
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;

  namespace Saving.Tests
  {
      /// <summary>The snapshot of one object, without a scene or a save system around it.</summary>
      public class SaveableStateTests
      {
          private GameObject _object;

          [TearDown]
          public void TearDown() => Object.DestroyImmediate(_object);

          [Test]
          public void Saveable_CaptureThenRestore_ActiveAndData()
          {
              _object = new GameObject("Snapshot");
              var saveable = _object.AddComponent<Saveable>();
              var counter = _object.AddComponent<SaveTestCounter>();
              var child = new GameObject("Child");
              child.transform.SetParent(_object.transform, false);
              counter.Value = 5;
              _object.transform.position = new Vector3(2f, 3f, 0f);
              child.SetActive(false);
              var state = saveable.CaptureState();

              counter.Value = 1;
              _object.transform.position = Vector3.zero;
              child.SetActive(true);
              saveable.RestoreState(state);

              Assert.AreEqual(5, counter.Value);
              Assert.AreEqual(new Vector3(2f, 3f, 0f), _object.transform.position);
              Assert.IsFalse(child.activeSelf);
          }
      }
  }
  ```

  `Assets/Game/Tests/PlayMode/SpriteAnimatorSerializerTests.cs`:

  ```csharp
  using NUnit.Framework;
  using Saving.Objects;
  using SpriteAnimation;

  namespace Gameplay.Tests
  {
      /// <summary>The player's frame animation: clip, time, pause and speed survive a round trip.</summary>
      public class SpriteAnimatorSerializerTests : CharacterTestBase
      {
          [Test]
          public void SpriteAnimatorSerializer_ClipTimePauseSpeed_RoundTrip()
          {
              SpriteAnimator animator = Rig.SpawnPlayer(new UnityEngine.Vector2(0f, 1f)).GetComponentInChildren<SpriteAnimator>();
              Assert.IsTrue(animator.TryGetClip(nameof(AnimationID.Run), out SpriteAnimationClip run));
              Assert.IsTrue(animator.TryGetClip(nameof(AnimationID.Idle), out SpriteAnimationClip idle));
              IComponentSerializer serializer = ComponentSerializerCatalog.Default.For(typeof(SpriteAnimator));
              animator.Play(run, 0.4f);
              animator.Pause();
              animator.Speed = 1.5f;
              var data = serializer.Capture(animator);

              animator.Play(idle, 0f);
              animator.Resume();
              animator.Speed = 1f;
              serializer.Restore(animator, data);

              Assert.IsInstanceOf<SpriteAnimatorSerializer>(serializer);
              Assert.AreSame(run, animator.CurrentClip);
              Assert.AreEqual(0.4f, animator.NormalizedTime, 0.05f);
              Assert.IsTrue(animator.IsPaused);
              Assert.AreEqual(1.5f, animator.Speed);
          }

          [Test]
          public void SpriteAnimatorSerializer_UnknownClip_KeepsCurrent()
          {
              SpriteAnimator animator = Rig.SpawnPlayer(new UnityEngine.Vector2(0f, 1f)).GetComponentInChildren<SpriteAnimator>();
              Assert.IsTrue(animator.TryGetClip(nameof(AnimationID.Idle), out SpriteAnimationClip idle));
              animator.Play(idle, 0.2f);
              IComponentSerializer serializer = ComponentSerializerCatalog.Default.For(typeof(SpriteAnimator));

              serializer.Restore(animator, Newtonsoft.Json.Linq.JObject.Parse("{ \"clip\": \"NoSuchClip\", \"time\": 0.9 }"));

              Assert.AreSame(idle, animator.CurrentClip);
          }
      }
  }
  ```

- [x] **Шаг 3: запуск — красный.** All Tests: падают `Saveable_CaptureThenRestore_ActiveAndData` и
  `SpriteAnimatorSerializer_ClipTimePauseSpeed_RoundTrip`, `SpriteAnimatorSerializer_UnknownClip_KeepsCurrent`
  (`NotImplementedException`).

- [x] **Шаг 4: реализация.**

  В `Runtime/Objects/Saveable.cs` заглушки заменить на:

  ```csharp
          /// <summary>This object's own state (active flags and saved components): for tests and debugging; scenes save it on their own.</summary>
          public JToken CaptureState() => SaveJson.ToToken(ObjectState.Capture(this, ComponentSerializerCatalog.Default));

          public void RestoreState(JToken state)
          {
              ObjectData data = SaveJson.FromToken<ObjectData>(state);
              if (data == null)
                  return;

              ObjectState.RestoreActive(this, data);
              ObjectState.RestoreComponents(this, data, ComponentSerializerCatalog.Default);
          }
  ```

  `Assets/Game/Scripts/Gameplay/Character/Components/Visual/SpriteAnimatorSerializer.cs`:

  ```csharp
  using Saving.Objects;
  using SpriteAnimation;

  namespace Gameplay
  {
      /// <summary>
      /// Saves the frame animation of SpriteAnimator: the clip by its Id, the normalized time, pause and speed. Lives
      /// in the game: the SpriteAnimator module knows nothing about saves. A clip missing from the library leaves the
      /// animator as it is.
      /// </summary>
      public sealed class SpriteAnimatorSerializer : ComponentSerializer<SpriteAnimator, SpriteAnimatorSerializer.Data>
      {
          public sealed class Data
          {
              public string Clip;
              public float Time;
              public bool Paused;
              public float Speed = 1f;
          }

          protected override Data Serialize(SpriteAnimator animator) => new()
          {
              Clip = animator.CurrentClip != null ? animator.CurrentClip.Id : null,
              Time = animator.NormalizedTime,
              Paused = animator.IsPaused,
              Speed = animator.Speed
          };

          protected override void Deserialize(SpriteAnimator animator, Data data)
          {
              animator.Speed = data.Speed;
              if (string.IsNullOrEmpty(data.Clip) || !animator.TryGetClip(data.Clip, out SpriteAnimationClip clip))
                  return;

              animator.Play(clip, data.Time);
              if (data.Paused)
                  animator.Pause();
          }
      }
  }
  ```

- [x] **Шаг 5: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 6: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem Assets/Game/Game.asmdef Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef Assets/Game/Scripts/Gameplay/Character/Components/Visual/SpriteAnimatorSerializer.cs Assets/Game/Scripts/Gameplay/Character/Components/Visual/SpriteAnimatorSerializer.cs.meta Assets/Game/Tests/PlayMode/SpriteAnimatorSerializerTests.cs Assets/Game/Tests/PlayMode/SpriteAnimatorSerializerTests.cs.meta
  git commit -m "Add a one-object snapshot and the SpriteAnimator serializer"
  ```

---

### Задача 7: демо-сцена `Sandbox_SaveSystem.unity`

**Files:**
- Create: `Assets/Game/Scripts/Gameplay/Demo/SaveDemoBoard.cs`
- Test: `Assets/Game/Tests/PlayMode/SaveDemoBoardTests.cs`
- Create (командой): `Assets/Game/Prefabs/Demo/SaveDemoCrate.prefab`, `Assets/Game/Animations/Demo/SaveDemoSpin.controller`,
  `Assets/Game/Scenes/Sandbox_SaveSystem.unity`

**Interfaces:**
- Consumes: `Saveable`, `[Save]`, `ISaveCallbacks` (часть 2), ссылки (задача 4), все встроенные (задачи 1–5),
  `SaveableIdAssigner`, `SaveCatalogBuilder` (часть 2), префаб `[Services]`.
- Produces: `Gameplay.SaveDemoBoard` — `Configure(GameObject cratePrefab, Transform spawnPoint, SpriteRenderer sign,
  Sprite[] signSprites)`, `SpawnCrate() → Saveable`, `DestroyLastCrate()`, `NextSign()`, `Poll(Keyboard)`, `Spawned`,
  `LastCrate`; поля `[Save]` `_spawned`, `_lastCrate` (ссылка на `Saveable`), `_signSprite` (ссылка на ассет).
- Сцена (не в Build Settings): камера, пол, 4 ящика-префаба, пила с `Animator` (клип `Saw_Rotation`, параметр
  скорости `Pace`), частицы (флажок «живые частицы»), источник звука (клип `Saw_Drag_01`, тихо, по кругу), 3D-куб
  с `Rigidbody` на 3D-полу, доска `SaveDemoBoard` с табличкой и точкой спавна, подсказка TMP, `[Services]` со
  слотом 9 (у демо свой слот: загрузка при старте не перебрасывает в `Game.unity`).

- [x] **Шаг 1: заглушка** — `Assets/Game/Scripts/Gameplay/Demo/SaveDemoBoard.cs`:

  ```csharp
  using System;
  using Saving.Objects;
  using UnityEngine;
  using UnityEngine.InputSystem;

  namespace Gameplay
  {
      public sealed class SaveDemoBoard : MonoBehaviour, ISaveCallbacks
      {
          public int Spawned => throw new NotImplementedException();
          public Saveable LastCrate => throw new NotImplementedException();

          public void Configure(GameObject cratePrefab, Transform spawnPoint, SpriteRenderer sign, Sprite[] signSprites) =>
              throw new NotImplementedException();

          public void Poll(Keyboard keyboard) => throw new NotImplementedException();
          public Saveable SpawnCrate() => throw new NotImplementedException();
          public void DestroyLastCrate() => throw new NotImplementedException();
          public void NextSign() => throw new NotImplementedException();
          public void OnBeforeSave() => throw new NotImplementedException();
          public void OnAfterLoad() => throw new NotImplementedException();
      }
  }
  ```

- [x] **Шаг 2: падающие тесты** — `Assets/Game/Tests/PlayMode/SaveDemoBoardTests.cs`:

  ```csharp
  using System.Collections;
  using NUnit.Framework;
  using Saving.Objects;
  using UnityEngine;
  using UnityEngine.InputSystem;
  using UnityEngine.TestTools;
  using Object = UnityEngine.Object;

  namespace Gameplay.Tests
  {
      /// <summary>The save demo board: spawns and destroys crates, switches the sign, keys B / X / C.</summary>
      public class SaveDemoBoardTests
      {
          private GameObject _board;
          private GameObject _cratePrefab;
          private SpriteRenderer _sign;
          private Sprite[] _sprites;
          private SaveDemoBoard _demo;

          [SetUp]
          public void SetUp()
          {
              _cratePrefab = new GameObject("CratePrefab");
              _cratePrefab.AddComponent<Saveable>();
              _cratePrefab.SetActive(false); // a stand-in prefab: copies are switched on below
              var texture = new Texture2D(2, 2);
              _sprites = new[]
              {
                  Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f),
                  Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f)
              };
              _board = new GameObject("Board");
              _sign = new GameObject("Sign").AddComponent<SpriteRenderer>();
              _sign.transform.SetParent(_board.transform, false);
              _demo = _board.AddComponent<SaveDemoBoard>();
              _demo.Configure(_cratePrefab, null, _sign, _sprites);
          }

          [TearDown]
          public void TearDown()
          {
              if (_demo.LastCrate != null)
                  Object.DestroyImmediate(_demo.LastCrate.gameObject);

              Object.DestroyImmediate(_board);
              Object.DestroyImmediate(_cratePrefab);
          }

          [Test]
          public void SaveDemoBoard_SpawnCrate_MadeAndRemembered()
          {
              Saveable crate = _demo.SpawnCrate();
              crate.gameObject.SetActive(true);

              Assert.IsNotNull(crate);
              Assert.AreSame(crate, _demo.LastCrate);
              Assert.AreEqual(1, _demo.Spawned);
              Assert.AreEqual(_board.transform.position, crate.transform.position, "no spawn point: at the board");
          }

          [UnityTest]
          public IEnumerator SaveDemoBoard_DestroyLastCrate_Gone()
          {
              GameObject crate = _demo.SpawnCrate().gameObject;

              _demo.DestroyLastCrate();
              yield return null;

              Assert.IsTrue(crate == null);
              Assert.IsNull(_demo.LastCrate);
          }

          [Test]
          public void SaveDemoBoard_NextSign_CyclesSprites()
          {
              _demo.NextSign();
              Assert.AreSame(_sprites[0], _sign.sprite);

              _demo.NextSign();
              Assert.AreSame(_sprites[1], _sign.sprite);

              _demo.NextSign();
              Assert.AreSame(_sprites[0], _sign.sprite);
          }

          [Test]
          public void SaveDemoBoard_AfterLoad_ShowsSavedSign()
          {
              _demo.NextSign();
              _sign.sprite = null;

              _demo.OnAfterLoad();

              Assert.AreSame(_sprites[0], _sign.sprite);
          }

          [Test]
          public void SaveDemoBoard_KeyB_SpawnsCrate()
          {
              var input = new InputTestFixture();
              input.Setup();
              try
              {
                  var keyboard = InputSystem.AddDevice<Keyboard>();
                  input.Press(keyboard.bKey);
                  _demo.Poll(keyboard);
              }
              finally
              {
                  input.TearDown();
              }

              Assert.AreEqual(1, _demo.Spawned);
          }
      }
  }
  ```

- [x] **Шаг 3: запуск — красный.** PlayMode: 5 `SaveDemoBoardTests` падают с `NotImplementedException` (в `SetUp` —
  из `Configure`).

- [x] **Шаг 4: реализация** — `Assets/Game/Scripts/Gameplay/Demo/SaveDemoBoard.cs`:

  ```csharp
  using System;
  using Saving.Objects;
  using UnityEngine;
  using UnityEngine.InputSystem;

  namespace Gameplay
  {
      /// <summary>
      /// The board of the save demo (Sandbox_SaveSystem): B spawns a crate, X destroys the last one, C switches the
      /// sign. Its [Save] fields keep a reference to the last crate and to the sign's sprite, so a load shows that
      /// references come back; F5 / F9 of [Services] save and load.
      /// </summary>
      public sealed class SaveDemoBoard : MonoBehaviour, ISaveCallbacks
      {
          [SerializeField] private GameObject _cratePrefab;
          [SerializeField] private Transform _spawnPoint;
          [SerializeField] private SpriteRenderer _sign;
          [SerializeField] private Sprite[] _signSprites = Array.Empty<Sprite>();

          [Save] private int _spawned;
          [Save] private Saveable _lastCrate;
          [Save] private Sprite _signSprite;

          public int Spawned => _spawned;
          public Saveable LastCrate => _lastCrate;

          /// <summary>Sets the board up from code (tests); the scene sets the same fields in the Inspector.</summary>
          public void Configure(GameObject cratePrefab, Transform spawnPoint, SpriteRenderer sign, Sprite[] signSprites)
          {
              _cratePrefab = cratePrefab;
              _spawnPoint = spawnPoint;
              _sign = sign;
              _signSprites = signSprites ?? Array.Empty<Sprite>();
          }

          private void Update() => Poll(Keyboard.current);

          /// <summary>Checks the keys pressed this frame; public for tests with a virtual keyboard.</summary>
          public void Poll(Keyboard keyboard)
          {
              if (keyboard == null)
                  return;

              if (keyboard.bKey.wasPressedThisFrame)
                  SpawnCrate();
              else if (keyboard.xKey.wasPressedThisFrame)
                  DestroyLastCrate();
              else if (keyboard.cKey.wasPressedThisFrame)
                  NextSign();
          }

          public Saveable SpawnCrate()
          {
              Vector3 position = _spawnPoint != null ? _spawnPoint.position : transform.position;
              GameObject crate = Instantiate(_cratePrefab, position, Quaternion.identity);
              _lastCrate = crate.GetComponent<Saveable>();
              _spawned++;
              return _lastCrate;
          }

          public void DestroyLastCrate()
          {
              if (_lastCrate == null)
                  return;

              Destroy(_lastCrate.gameObject);
              _lastCrate = null;
          }

          public void NextSign()
          {
              if (_signSprites.Length == 0)
                  return;

              int index = (Array.IndexOf(_signSprites, _signSprite) + 1) % _signSprites.Length;
              _signSprite = _signSprites[index];
              ShowSign();
          }

          public void OnBeforeSave()
          {
          }

          public void OnAfterLoad() => ShowSign();

          private void ShowSign()
          {
              if (_sign != null && _signSprite != null)
                  _sign.sprite = _signSprite;
          }
      }
  }
  ```

- [x] **Шаг 5: запуск — зелёный.** All Tests: `failed 0`.

- [x] **Шаг 6: ассеты демо.** `Refresh`, затем `Unity_RunCommand` (создаёт префаб ящика, контроллер и сцену; ID
  объектов сцены выдаёт `SaveableIdAssigner`; открытые сцены пользователя не трогает):

  ```csharp
  using System;
  using Gameplay;
  using Saving;
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;
  using UnityEditor.Animations;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  internal class CommandScript : IRunCommand
  {
      private const string CRATE = "Assets/Game/Prefabs/Demo/SaveDemoCrate.prefab";
      private const string SPIN = "Assets/Game/Animations/Demo/SaveDemoSpin.controller";
      private const string SCENE = "Assets/Game/Scenes/Sandbox_SaveSystem.unity";
      private const string SERVICES = "Assets/Game/Prefabs/Services.prefab";

      public void Execute(ExecutionResult result)
      {
          Folder("Assets/Game/Prefabs", "Demo");
          Folder("Assets/Game/Animations", "Demo");
          var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Level/Square.png");
          var board = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Level/Board.png");
          var chair = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Level/Chair.png");
          var saw = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/Art/Level/Saw.png");
          var spinClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Game/Animations/Level/Saw_Rotation.anim");
          var drag = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Sounds/Level/Saw_Drag_01.aif");

          Scene previous = SceneManager.GetActiveScene();
          bool untitled = string.IsNullOrEmpty(previous.path);
          if (untitled && previous.isDirty)
          {
              result.LogError("The untitled scene has changes");
              return;
          }

          Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitled ? NewSceneMode.Single : NewSceneMode.Additive);
          SceneManager.SetActiveScene(scene);

          // Crate prefab: a falling square that the board spawns
          var crate = new GameObject("SaveDemoCrate");
          crate.AddComponent<SpriteRenderer>().sprite = square;
          crate.AddComponent<BoxCollider2D>();
          crate.AddComponent<Rigidbody2D>();
          crate.AddComponent<Saveable>();
          GameObject cratePrefab = PrefabUtility.SaveAsPrefabAsset(crate, CRATE);
          UnityEngine.Object.DestroyImmediate(crate);
          SaveCatalogBuilder.BakePrefabIds(new[] { CRATE });

          // Spinning saw: Saw_Rotation with its pace as a parameter
          AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(SPIN);
          controller.AddParameter("Pace", AnimatorControllerParameterType.Float);
          AnimatorState spin = controller.layers[0].stateMachine.AddState("Spin");
          spin.motion = spinClip;
          spin.speedParameterActive = true;
          spin.speedParameter = "Pace";
          AnimatorControllerParameter[] parameters = controller.parameters;
          parameters[0].defaultFloat = 1f;
          controller.parameters = parameters;

          var camera = new GameObject("Main Camera") { tag = "MainCamera" };
          Camera view = camera.AddComponent<Camera>();
          view.orthographic = true;
          view.orthographicSize = 7f;
          view.clearFlags = CameraClearFlags.SolidColor;
          view.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
          camera.transform.position = new Vector3(1f, 2f, -10f);

          var ground = new GameObject("Ground");
          ground.transform.position = new Vector3(0f, -3f, 0f);
          ground.transform.localScale = new Vector3(26f, 1f, 1f);
          ground.AddComponent<SpriteRenderer>().sprite = square;
          ground.AddComponent<BoxCollider2D>();

          for (int index = 0; index < 4; index++)
          {
              var instance = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, scene);
              instance.transform.position = new Vector3(-8f + index * 0.6f, -1.5f + index * 1.1f, 0f);
          }

          GameObject sawObject = Saveable("Saw", new Vector3(3f, 1f, 0f));
          sawObject.AddComponent<SpriteRenderer>().sprite = saw;
          sawObject.AddComponent<Animator>().runtimeAnimatorController = controller;

          GameObject sparks = Saveable("Sparks", new Vector3(6f, -2f, 0f));
          sparks.AddComponent<ParticleSystem>();
          sparks.GetComponent<Saveable>().SetOption("ParticleSystem", ParticleSystemSerializer.LIVE_PARTICLES, true);

          GameObject music = Saveable("Saw Sound", new Vector3(3f, 1f, 0f));
          AudioSource source = music.AddComponent<AudioSource>();
          source.clip = drag;
          source.loop = true;
          source.volume = 0.15f;
          source.playOnAwake = true;

          var floor3D = GameObject.CreatePrimitive(PrimitiveType.Cube);
          floor3D.name = "Floor 3D";
          floor3D.transform.position = new Vector3(9.5f, -2.5f, 0f);
          floor3D.transform.localScale = new Vector3(3f, 0.5f, 1f);
          GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
          cube.name = "Cube 3D";
          cube.transform.position = new Vector3(9.5f, 3f, 0f);
          cube.transform.rotation = Quaternion.Euler(0f, 0f, 25f);
          cube.AddComponent<Rigidbody>();
          cube.AddComponent<Saveable>();

          GameObject boardObject = Saveable("Board", new Vector3(-3f, 4.5f, 0f));
          var sign = new GameObject("Sign");
          sign.transform.SetParent(boardObject.transform, false);
          var signRenderer = sign.AddComponent<SpriteRenderer>();
          signRenderer.sprite = board;
          var spawnPoint = new GameObject("Spawn Point");
          spawnPoint.transform.SetParent(boardObject.transform, false);
          spawnPoint.transform.localPosition = new Vector3(-4f, 1f, 0f);
          boardObject.AddComponent<SaveDemoBoard>().Configure(cratePrefab, spawnPoint.transform, signRenderer,
              new[] { board, chair, square });

          Type tmpType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
          if (tmpType != null)
          {
              var label = new GameObject("Hint");
              label.transform.position = new Vector3(1f, 7.6f, 0f);
              var text = new SerializedObject(label.AddComponent(tmpType));
              text.FindProperty("m_text").stringValue = "F5 save   F9 load   B crate   X destroy crate   C sign";
              text.FindProperty("m_fontSize").floatValue = 4f;
              text.ApplyModifiedPropertiesWithoutUndo();
          }

          var services = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SERVICES), scene);
          var system = new SerializedObject(services.GetComponentInChildren<SaveLoadSystem>(true));
          system.FindProperty("_startupSlot").intValue = 9; // the demo's own slot
          system.ApplyModifiedProperties();

          foreach (GameObject root in scene.GetRootGameObjects())
          {
              foreach (Saveable saveable in root.GetComponentsInChildren<Saveable>(true))
                  SaveableIdAssigner.Assign(saveable);
          }

          EditorSceneManager.SaveScene(scene, SCENE);
          if (untitled)
          {
              EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
          }
          else
          {
              SceneManager.SetActiveScene(previous);
              EditorSceneManager.CloseScene(scene, true);
          }

          foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
              SaveCatalogBuilder.Rebuild(settings);

          result.Log("Created {0}, {1}, {2}", CRATE, SPIN, SCENE);
      }

      private static GameObject Saveable(string name, Vector3 position)
      {
          var go = new GameObject(name);
          go.transform.position = position;
          go.AddComponent<Saveable>();
          return go;
      }

      private static void Folder(string parent, string name)
      {
          if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
              AssetDatabase.CreateFolder(parent, name);
      }
  }
  ```

  Проверить: `git status` — новые ассеты в `Prefabs/Demo`, `Animations/Demo`, `Scenes/Sandbox_SaveSystem.unity`;
  у префаба ящика `_prefabId` = его GUID; *Tools → Save System → Validate Scenes* — без проблем (сцена демо — в
  каталоге настроек; Build Settings не меняются).

- [x] **Шаг 7: проверка демо** (через MCP, без нажатия клавиш): открыть `Sandbox_SaveSystem.unity`, Play; через
  2–3 с `Unity_RunCommand` — снять «до» и сохранить:

  ```csharp
  using System.Linq;
  using Gameplay;
  using Saving;
  using ServiceLocation;
  using UnityEditor;
  using UnityEngine;

  internal class CommandScript : IRunCommand
  {
      public void Execute(ExecutionResult result)
      {
          var board = Object.FindAnyObjectByType<SaveDemoBoard>();
          board.SpawnCrate();
          board.NextSign();
          board.NextSign();
          var crates = Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
          string before = string.Join(";", crates.OrderBy(body => body.name + body.position.x).Select(body => $"{body.name}:{body.position}"));
          var animator = GameObject.Find("Saw").GetComponent<Animator>();
          SessionState.SetString("SaveDemo.Before", before);
          result.Log("save -> {0}; spawned {1}; saw time {2:F2}; particles {3}; sign {4}",
              ServiceLocator.Get<ISaveLoadService>().Save(), board.Spawned,
              animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
              GameObject.Find("Sparks").GetComponent<ParticleSystem>().particleCount,
              GameObject.Find("Board/Sign").GetComponent<SpriteRenderer>().sprite.name);
          board.SpawnCrate();
          board.NextSign();
      }
  }
  ```

  (В командах `FindObjectsByType` допустим: это не код плагина, а одноразовая проверка; если на 6.5 он помечен
  устаревшим — заменить обходом `GetRootGameObjects`.) Подождать 2 с и загрузить: `ServiceLocator.Get<ISaveLoadService>().Load()`;
  ещё через секунду тем же способом снять «после» и сравнить: те же ящики (лишний — исчез, уничтоженный — нет),
  позиции рядом с сохранёнными, `Spawned` и табличка — как при сохранении, частицы и пила продолжили с
  сохранённого времени. Расхождения — записать и разобрать (контакты физики дают небольшой сдвиг — это ожидаемо, см.
  спек, раздел 14). Выйти из Play Mode.

- [x] **Шаг 8: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Gameplay/Demo Assets/Game/Scripts/Demo.meta Assets/Game/Tests/PlayMode/SaveDemoBoardTests.cs Assets/Game/Tests/PlayMode/SaveDemoBoardTests.cs.meta Assets/Game/Prefabs/Demo Assets/Game/Prefabs/Demo.meta Assets/Game/Animations/Demo Assets/Game/Animations/Demo.meta Assets/Game/Scenes/Sandbox_SaveSystem.unity Assets/Game/Scenes/Sandbox_SaveSystem.unity.meta Assets/Game/Configs/SaveSystemSettings.asset
  git commit -m "Add the save system demo scene"
  ```

---

### Задача 8: слияние `feature/ui` и `Saveable` на игроке

**Files:**
- Слияние: `feature/ui` → `feature/save-system` (merge-коммит)
- Modify (командой): `Assets/Game/Prefabs/Characters/Player.prefab` (+ `Saveable`), `Enemy.prefab` (свой ID префаба),
  сцены из Build Settings с экземплярами игрока и врагов (ID), `Configs/SaveSystemSettings.asset` (каталог)
- Modify: `Assets/Game/Tests/EditMode/SaveSetupTests.cs` (+ 2)
- Test: `Assets/Game/Tests/PlayMode/PlayerSaveTests.cs`

**Interfaces:**
- Consumes: `Saveable.SetSaved`, `Saveable.CaptureState`/`RestoreState` (задача 6), `SaveableEditor.BuildRows`,
  `SaveCatalogBuilder`, `SaveableIdAssigner`, `SaveValidator` (часть 2), `SpriteAnimatorSerializer` (задача 6),
  `CharacterTestBase`/`CharacterTestRig` (игра).
- Produces: `Saveable` на корне `Player.prefab` с отмеченными `Transform`, `Rigidbody2D`, `Visual/SpriteAnimator`
  (остальное снято); враги наследуют его (у `Enemy.prefab` свой ID префаба); у экземпляров в сценах — ID.

- [x] **Шаг 1: согласие на слияние.** Показать пользователю, что придёт:

  ```bash
  git log --oneline feature/save-system..feature/ui | wc -l
  git diff --stat feature/save-system...feature/ui | tail -3
  git diff --name-only feature/save-system...feature/ui | grep -E "CLAUDE.md|GameServices|Services.prefab|asmdef|Player.prefab|Enemy.prefab|\.unity$"
  ```

  и спросить разрешения влить `feature/ui` в `feature/save-system`. Нет согласия — задачу 8 не начинать (остальное
  плана от неё не зависит), отметить в «Исполнении».

- [x] **Шаг 2: слияние.** Сохранить сцены и выйти из Play Mode, затем:

  ```bash
  git merge --no-ff feature/ui -m "Merge feature/ui into feature/save-system"
  ```

  Ожидаемые конфликты и как решать (оставить обе стороны):
  - `Assets/AI/CLAUDE.md` — взять обе правки: абзацы этапов и сервисов `feature/ui`, абзацы сохранений этой ветки;
  - `Assets/Game/Scripts/GameSystem/Services/GameServices.cs` — поля, регистрации и снятия обоих сервисов (пауза / экраны и
    `ISaveLoadService`), порядок снятия — обратный регистрации;
  - `Assets/Game/Prefabs/Services.prefab` — взять версию `feature/ui` (`git checkout --theirs`), затем, после `Refresh`,
    заново добавить то, что вносила эта ветка: дочерний `SaveLoadSystem` с «загрузкой при старте» и `SaveDebugHotkeys`
    на корне (команда — план части 1, задача 8, шаг 6), ссылку на `Configs/SaveSystemSettings` (план части 2,
    задача 9, шаг 5);
  - `*.asmdef` — объединить списки `references`.
  Конфликт в других файлах или непонятный — остановиться и спросить. После решения: `Refresh`, консоль без ошибок,
  All Tests `failed 0`, затем `git add` решённых файлов и `git commit --no-edit`.

- [x] **Шаг 3: падающие тесты.**

  В `Assets/Game/Tests/EditMode/SaveSetupTests.cs`:

  ```csharp
          private const string PLAYER_PATH = "Assets/Game/Prefabs/Characters/Player.prefab";
          private const string ENEMY_PATH = "Assets/Game/Prefabs/Characters/Enemy.prefab";

          [Test]
          public void Player_SavesBodyTransformAndAnimationOnly()
          {
              var player = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PATH);
              var saveable = player.GetComponent<Saveable>();

              Assert.IsNotNull(saveable, "the player has a Saveable on its root");
              string[] saved = SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default)
                  .Where(row => row.Saved).Select(row => row.Key).ToArray();
              CollectionAssert.AreEquivalent(new[] { "Transform", "Rigidbody2D", "Visual/SpriteAnimator" }, saved);
          }

          [Test]
          public void Enemy_InheritsSaveableWithItsOwnPrefabId()
          {
              var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(ENEMY_PATH).GetComponent<Saveable>();

              Assert.IsNotNull(enemy);
              Assert.AreEqual(AssetDatabase.AssetPathToGUID(ENEMY_PATH), enemy.PrefabId);
          }
  ```

  `Assets/Game/Tests/PlayMode/PlayerSaveTests.cs`:

  ```csharp
  using NUnit.Framework;
  using Saving.Objects;
  using SpriteAnimation;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Gameplay.Tests
  {
      /// <summary>The player through a save round trip on the test rig: position, velocity and clip come back.</summary>
      public class PlayerSaveTests : CharacterTestBase
      {
          [Test]
          public void Player_RoundTrip_PositionVelocityAndClip()
          {
              Character player = Rig.SpawnGrounded(new Vector2(0f, 0f));
              var body = player.GetComponent<Rigidbody2D>();
              var animator = player.GetComponentInChildren<SpriteAnimator>();
              var saveable = player.GetComponent<Saveable>();
              body.linearVelocity = new Vector2(4f, 6f);
              Rig.Step(10);
              Vector2 position = body.position;
              Vector2 velocity = body.linearVelocity;
              SpriteAnimationClip clip = animator.CurrentClip;
              var state = saveable.CaptureState();

              Rig.Step(40);
              saveable.RestoreState(state);

              Assert.AreEqual(position.x, body.position.x, 1e-3f);
              Assert.AreEqual(position.y, body.position.y, 1e-3f);
              Assert.AreEqual(velocity.x, body.linearVelocity.x, 1e-3f);
              Assert.AreEqual(velocity.y, body.linearVelocity.y, 1e-3f);
              Assert.AreSame(clip, animator.CurrentClip);
          }

          [Test]
          public void Player_WithoutServices_RunsAsBefore()
          {
              Character player = Rig.SpawnGrounded(new Vector2(0f, 0f));

              Rig.Step(60);

              Assert.IsTrue(player.GetComponent<Saveable>().IsSpawned, "a spawned player gets its own id");
              LogAssert.NoUnexpectedReceived();
          }
      }
  }
  ```

- [x] **Шаг 4: запуск — красный.** All Tests: падают 2 новых `SaveSetupTests` (у игрока нет `Saveable`) и
  `PlayerSaveTests` (`NullReferenceException` на `Saveable`).

- [x] **Шаг 5: `Saveable` на игроке.** `Unity_RunCommand`:

  ```csharp
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;
  using UnityEngine;

  internal class CommandScript : IRunCommand
  {
      private const string PLAYER = "Assets/Game/Prefabs/Characters/Player.prefab";
      private const string ENEMY = "Assets/Game/Prefabs/Characters/Enemy.prefab";

      public void Execute(ExecutionResult result)
      {
          GameObject root = PrefabUtility.LoadPrefabContents(PLAYER);
          try
          {
              if (!root.TryGetComponent(out Saveable saveable))
                  saveable = root.AddComponent<Saveable>();

              foreach (SaveableEditor.Row row in SaveableEditor.BuildRows(saveable, ComponentSerializerCatalog.Default))
                  saveable.SetSaved(row.Key, row.Key == "Transform" || row.Key == "Rigidbody2D" || row.Key == "Visual/SpriteAnimator");

              PrefabUtility.SaveAsPrefabAsset(root, PLAYER);
          }
          finally
          {
              PrefabUtility.UnloadPrefabContents(root);
          }

          int baked = SaveCatalogBuilder.BakePrefabIds(new[] { PLAYER, ENEMY });
          foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
              SaveCatalogBuilder.Rebuild(settings);

          result.Log("Saveable on {0}; prefab ids baked in {1} prefab(s)", PLAYER, baked);
      }
  }
  ```

  Проверить `git diff --stat`: `Player.prefab` (+ компонент `Saveable` и список `_excluded`), `Enemy.prefab` (override
  `_prefabId`), `SaveSystemSettings.asset` (оба префаба в каталоге).

- [x] **Шаг 6: ID в сценах.** `Unity_RunCommand` — открыть по очереди сцены из Build Settings и `Sandbox_SaveSystem`,
  выдать ID экземплярам без него, сохранить:

  ```csharp
  using System.Linq;
  using Saving.Editor;
  using Saving.Objects;
  using UnityEditor;
  using UnityEditor.SceneManagement;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  internal class CommandScript : IRunCommand
  {
      public void Execute(ExecutionResult result)
      {
          if (SceneManager.GetActiveScene().isDirty)
          {
              result.LogError("Save the open scene first");
              return;
          }

          string[] paths = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path)
              .Append("Assets/Game/Scenes/Sandbox_SaveSystem.unity").Distinct().ToArray();
          foreach (string path in paths)
          {
              Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
              int assigned = 0;
              foreach (GameObject root in scene.GetRootGameObjects())
              {
                  foreach (Saveable saveable in root.GetComponentsInChildren<Saveable>(true))
                  {
                      if (SaveableIdAssigner.Assign(saveable))
                          assigned++;
                  }
              }

              if (assigned > 0)
                  EditorSceneManager.SaveScene(scene);

              result.Log("{0}: {1} id(s) given", path, assigned);
          }

          EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
      }
  }
  ```

  *Tools → Save System → Validate Scenes* — без проблем.

- [x] **Шаг 7: запуск — зелёный.** All Tests: EditMode и PlayMode `failed 0`.

- [x] **Шаг 8: проверка в игре** (MCP, без клавиш): Play в `Sandbox_Locomotion.unity` (там стоит игрок); через
  `Unity_RunCommand` задать игроку скорость (`Rigidbody2D.linearVelocity`), подождать, `Save()`, запомнить позицию,
  подождать ещё, `Load()` — после `Loaded` игрок на сохранённой позиции, клип анимации тот же; консоль без ошибок
  плагина. Выйти из Play Mode.

- [x] **Шаг 9: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Prefabs/Characters Assets/Game/Configs/SaveSystemSettings.asset Assets/Game/Scenes Assets/Game/Tests/EditMode/SaveSetupTests.cs Assets/Game/Tests/PlayMode/PlayerSaveTests.cs Assets/Game/Tests/PlayMode/PlayerSaveTests.cs.meta
  git commit -m "Save the player and enemies with Saveable"
  ```

  (`Assets/Game/Scenes` — только сцены, в которых шаг 6 выдал ID; чужих правок в них быть не должно — проверить
  `git diff --stat` перед `git add`.)

---

### Задача 9: документация, итоговый прогон и `_Import`

**Files:**
- Modify: `Assets/Game/Scripts/Modules/SaveSystem/README.md`, `Assets/AI/CLAUDE.md`, `Assets/AI/Docs/SaveSystem.md`, этот план

- [x] **Шаг 1: README модуля.**
  - первый абзац: «Части 1–2 готовы…» → «Плагин готов: ядро, объекты сцены, Unity-типы и ссылки (части 1–3).»;
  - в «Объекты сцены» строку «Сохраняются: …» заменить на таблицу встроенных:

    ```markdown
    | Компонент | Что сохраняется |
    |---|---|
    | `GameObject` | `activeSelf` объекта и детей с отмеченными компонентами |
    | `Transform` | локальные позиция, поворот, масштаб |
    | `Rigidbody2D` | тип тела, `simulated`, позиция и поворот тела, скорость, угловая скорость, сон, `gravityScale`, `constraints` |
    | `Rigidbody` | `isKinematic`, `useGravity`, скорость, угловая скорость, сон, `constraints` |
    | `Collider2D`, `Collider` | `enabled` |
    | `Animator` | по слоям: состояние, время, вес; параметры `float`/`int`/`bool`; `speed` |
    | `ParticleSystem` | играет / пауза / стоп, время, зерно (перемотка `Simulate`); флажок «живые частицы» |
    | `SpriteRenderer` | спрайт, цвет, `flipX`, `flipY` |
    | `AudioSource` | клип, время, играет / пауза, громкость, высота, `mute` |
    ```

    и абзац «Ссылки»: в полях `[Save]` и данных сериализаторов — на `Saveable`, его компоненты и детей (ID + путь +
    компонент), на префабы с `Saveable` (ID префаба) и на ассеты из папок `SaveSystemSettings` (GUID; спрайт в
    текстуре — `GUID:localId`); прочее — `null` и предупреждение. Свой сериализатор с параметрами —
    `ISerializerOptions` + `SerializerOptions.IsOn`. Сериализатор модуля движка компилируется, только если модуль
    включён (`versionDefines`: `SAVE_PHYSICS`, `SAVE_PHYSICS_2D`, `SAVE_ANIMATION`, `SAVE_PARTICLES`, `SAVE_AUDIO`).
  - «Тесты»: + встроенные сериализаторы, ссылки, снимок объекта.
  - Демо: «`Assets/Game/Scenes/Sandbox_SaveSystem.unity` (не в Build Settings): F5 / F9, B — ящик, X — убрать ящик,
    C — табличка; у демо свой слот 9».

- [x] **Шаг 2: `Assets/AI/CLAUDE.md`.** Абзац «**Сохранения — …**»: «Части 1 … и 2 … готовы; часть 3 (…) — следующая.»
  → «Плагин готов (части 1–3): ядро, объекты сцены, Unity-типы и ссылки; игрок и враги сохраняются (`Transform`,
  `Rigidbody2D`, анимация; ИИ после загрузки начинает с `Patrol`). Демо — `Sandbox_SaveSystem.unity`.». В «Структура
  Assets/Game»: `Scenes/` — `Sandbox_SaveSystem.unity (демо сохранений; не в Build Settings)`, `Scripts/` — `Demo/
  (SaveDemoBoard — доска демо сохранений)`, `Prefabs/` — `Demo/ (SaveDemoCrate)`. «Сборки»: у `Game` — ещё
  `SaveSystem.Objects`; у `Game.Tests.PlayMode` — `SaveSystem.Objects` и `Newtonsoft.Json.dll`. «Архитектура»,
  абзац «**Сохранения.**» — дописать: «Встроенные сериализаторы Unity-типов — в модуле под `versionDefines`; ссылки в
  данных — на `Saveable` и его части, префабы, ассеты папок `Art` и `Audio` (папки — в `SaveSystemSettings`). Свой
  компонент модуля (`SpriteAnimator`) сохраняет сериализатор в игре (`SpriteAnimatorSerializer`). Новые компоненты на
  `Saveable` игрока по умолчанию сохраняются — снять флажок, если это кратковременный визуал.»

- [x] **Шаг 3: спек.** В `Assets/AI/Docs/SaveSystem.md` строку статуса — «**Статус:** плагин готов (части 1–3)
  <дата>.»; в разделе 8 строку про игрока дополнить: «(сделано; враги — вариант игрока — сохраняются тоже, состояние
  ИИ — нет)». В этом плане отметить шаги и дописать раздел «Исполнение».

- [x] **Шаг 4: итоговый прогон.** Сохранить сцены, `Refresh`, All Tests: `failed 0`. Консоль без новых ошибок и
  предупреждений от `Saving` (известные шумы Odin и `UAC1001` — не наши). *Validate Scenes* — «no problems found».
  Сверить критерий готовности спека (раздел 1) по пунктам: демо — задача 7, шаг 7; игрок — задача 8, шаг 8; `.bak`,
  `.corrupt`, более новая версия — тесты частей 1–2; все тесты зелёные.

- [ ] **Шаг 5: `_Import`.** Код домашки перенесён (спек, раздел 12) — спросить пользователя и попросить удалить папку
  `_Import` в корне рабочей копии самому (Claude безвозвратно файлы не удаляет; папка не в git — коммитить нечего).

- [x] **Шаг 6: коммит** (если разрешено):

  ```bash
  git add Assets/Game/Scripts/Modules/SaveSystem/README.md Assets/AI/CLAUDE.md Assets/AI/Docs/SaveSystem.md Assets/AI/Docs/SaveSystem_Plan_Part3.md Assets/AI/Docs/SaveSystem_Plan_Part3.md.meta
  git commit -m "Document the finished save system"
  ```

  Пуш — только по просьбе.

---

## Исполнение (2026-10-24)

Выполнено в worktree `C:\!!!Work\Platformer-SaveSystem` со своим эдитором, задача за задачей по TDD, с локальным
коммитом после каждой зелёной задачи, без пуша (пользователь разрешил идти до конца без вопросов). Отличия от плана:

- **Связь с эдитором** — родные инструменты `unity-mcp-save` (`Unity_RunCommand`, `Unity_GetConsoleLogs`); скрипт
  relay из части 2 спорил с ними за канал и зависал. Команда запуска тестов сохраняет только сцены с путём, без
  `SaveOpenScenes`: тот сам дал бы имя безымянной грязной сцене в `Assets`.
- **`Assets/2.unity`** — сцена пользователя, открытая в эдиторе этой рабочей копии (не в git). Её запись в каталоге
  сцен `SaveSystemSettings` не коммитилась: ассет добавлялся в индекс без этой записи.
- Задача 3: тест частиц сначала видел 0 частиц — `cullingMode = Automatic` ставит на паузу систему, которую не видит
  ни одна камера. Помощник `MakeParticles` в тестах ставит `AlwaysSimulate`.
- Задача 4: `TestScenes` (Runtime-тесты) стал `public` — его константы и ассеты берут EditMode-тесты.
- Задача 4: ID нового `Saveable` выдаётся сразу в `Reset` (крючок `Saveable.EditorAdded`), а не через `delayCall` из
  `OnValidate`: эдитор в фоне `delayCall` почти не крутит. Тест `Ids_AddedInEditor_AssignedAtOnce`, коммит отдельный.
- Задача 5: допущение 5 подтвердилось — время `AudioSource` в PlayMode-тестах эдитора идёт; остальные допущения
  подтвердились раньше.
- Задача 7: `Saw_Rotation` — legacy-клип, `Animator` его не играет. Пила демо крутится своим клипом
  `Animations/Demo/SaveDemoSpin.anim` (тот же поворот, не legacy), клип уровня не тронут. Проверка демо: после `Load`
  те же 5 ящиков (лишний исчез, уничтоженный вернулся, позиции совпали), `Spawned` 1, табличка `Chair`, ссылка на
  последний ящик восстановлена, пила продолжила с сохранённого времени (20.58 + время после загрузки), звук — со
  своего места, 50 живых частиц. *Validate Scenes* меню проверяет только сцены Build Settings и открытые — демо
  проверено вызовом `SaveValidator` напрямую.
- Задача 8: `feature/ui` влита с ожидаемыми конфликтами; в `Services.prefab` конфликт был только в списке детей корня —
  оставлены оба ребёнка (`SaveLoadSystem`, `EventSystem`), пересобирать префаб не понадобилось. С этапом урона спрайт
  игрока переехал под `Deform`: ключ анимации — `Deform/Visual/SpriteAnimator` (тест и команда поправлены). В тестах
  игрока не хватало `Rig.AddFloor()` — стенд сам пол не строит. Проверка в игре — в `Sandbox_Enemies`: в
  `Sandbox_Locomotion` после `feature/ui` нет `[Services]`; загрузка при старте (слот 0) сперва перенесла в
  `Game.unity`, по сохранению прошлой проверки. Сохранение в прыжке: после `Load` игрок там же, в воздухе, клип
  `AirMid`, дальше падает как обычно.
- Наблюдение: если переставить только тело (`Rigidbody2D.position`) и сразу сохранить, в сейве окажется старый
  `Transform`, и после загрузки он сдвинет тело к себе. В игре трансформ догоняет тело на шаге физики; в README —
  примечание.

Итог: EditMode 423/0, PlayMode 540/0 (4 inconclusive — как до начала), *Validate Scenes* — без проблем. Шаг 5
задачи 9 (`_Import`) — за пользователем: папку в корне рабочей копии удаляет он сам.

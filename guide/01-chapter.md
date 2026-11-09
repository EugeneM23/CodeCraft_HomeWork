# Глава 01. Основы мультиплеера: минимальный каркас Fusion 2

> **TL;DR для агента**
> - Поднять сетевую сессию Fusion 2 в режиме Host/Client одним вызовом `NetworkRunner.StartGame` из одной точки входа, без UI выбора роли и без создания раннера кодом.
> - Раздать `INetworkRunnerCallbacks` по мелким независимым MonoBehaviour через штатный компонент `NetworkEvents`, вместо god-класса на 19 методов.
> - Построить server-authoritative жизненный цикл игрока: `Spawn`/`Despawn` только под `runner.IsServer`, связка «игрок ↔ объект» — в реплицируемом реестре Fusion (`SetPlayerObject`/`GetPlayerObject`), а не в своём `Dictionary`.
> - Сделать движение детерминированным: ввод собирается ровно один раз на тик в `OnInput`, применяется в `FixedUpdateNetwork`, шаг масштабируется на `Runner.DeltaTime` (никогда на `Time.deltaTime`).
> - Понять, почему `NetworkTransform` на префабе обязателен: симуляция идёт 64 Гц, пакеты уходят 32 Гц — разрыв прячет интерполяция, и именно сетевое состояние `NetworkTransform` делает безопасной относительную мутацию `transform.position += ...`.

Версии, на которых всё проверено: **Unity 2022.3.42f1** (`ProjectSettings/ProjectVersion.txt`), **Photon Fusion 2.0.5 Stable build 920**, 2025-02-12, ветка `2.0/release` (`Assets/Photon/Fusion/build_info.txt`). Корень проекта главы: `C:/Users/MY PC/Downloads/Модуль V. Мультиплеер/01 - Основы мультиплеера/_src`.

Учебного кода ровно 8 файлов, все в `Assets/Scripts`. Папок `[Teacher]`/`[Archive]` в разделе нет. Зато в раздаче лежит `Assets/Photon/FusionDemos/Photon-Fusion-Intro-Sample-2.0.5-Stable-920.unitypackage` — неимпортированный официальный Intro Sample; это ближайший аналог отсутствующей эталонной реализации, если понадобится сверка.

---

## Какую проблему решает

Любое сетевое приложение на Fusion упирается в четыре вопроса, на которые надо ответить **до** написания игровой логики. Эта глава даёт на них ответы в виде рабочего каркаса.

**1. Как вообще начать сессию и не получить гонку инициализации.** `StartGame` — асинхронная операция, которая поднимает симуляцию, подключается к Photon Cloud, выбирает роль пира и перехватывает сцену. Если её вызывать из нескольких мест, из UI-кнопок или из `Awake`, получаются двойные старты раннера и подписчики, которые пропустили первые колбэки.

**2. Как не утонуть в `INetworkRunnerCallbacks`.** Интерфейс рантайма — это 19 методов (точное число подтверждается по сцене: у `NetworkEvents` ровно 19 UnityEvent-полей). Реализация их одним классом неизбежно смешивает спавн, ввод, матчмейкинг, загрузку сцен и AOI в нерефакторимый god-object.

**3. Кто имеет право создавать и двигать объекты.** Колбэки `PlayerJoined`/`PlayerLeft` приходят на **все** пиры. Без гейта авторитета каждый клиент попытается заспавнить персонажа — в Host-режиме это ошибка отсутствия state authority, а логически дубли.

**4. Почему движение «разъезжается» между хостом и клиентом.** Ответ — детерминизм по тикам: одинаковый ввод, привязанный к номеру тика, плюс одинаковый шаг времени. Любое отступление (`Time.deltaTime`, чтение ввода в `Update`, мутация состояния вне `FixedUpdateNetwork`) даёт разные результаты на разных пирах и постоянные рывки коррекции.

Чего в этой главе **намеренно нет** и чего не надо искать: ни одного `[Networked]`-свойства, ни `Spawned()`/`Despawned()`/`Render()`, ни RPC, ни `ChangeDetector`, ни lag compensation (`LagCompensation.Enabled: false`), ни host migration (`HostMigration.EnableAutoUpdate: false`), ни Rigidbody/физики, ни UI и матчмейкинга. Это нулевой каркас.

---

## Модель мира: кто чем владеет

Режим один — **Host/Client**, задаётся `GameMode.AutoHostOrClient`. Shared-режим в главе не используется (хотя на префабе уже видны его «крючки» — см. раздел настроек).

**Два вида авторитета, которые нельзя путать:**

- **State authority** — право менять сетевое состояние объекта. В Host-режиме она у **хоста**, для всех объектов. Клиенты держат *предсказанные* копии и *прокси*.
- **Input authority** — право поставлять ввод для объекта. Выдаётся конкретному `PlayerRef` четвёртым аргументом `Spawn`.

**Что где исполняется.** Это критично, и в главе это видно буквально через `Debug.Log`:

| Код | Хост | Клиент-владелец | Клиент-прокси (чужой объект) |
|---|---|---|---|
| `OnInput` (колбэк) | да (для своего ввода) | да | — (вызывается на пире с `ProvideInput`, для его собственного ввода) |
| `FixedUpdateNetwork()` | да, для всех объектов | да, для всех объектов | да, для всех объектов |
| `GetInput(out …)` → `true` | да (есть state authority) | да (есть input authority) | **нет** — вернёт `false` |
| `Spawn`/`Despawn` | да | нет (гейт `IsServer`) | нет |

Отсюда правило: **`FixedUpdateNetwork` исполняется везде, а право на действие внутри него определяется `GetInput`/authority-проверками.** В коде главы это зафиксировано комментарием автора `//Only Input Authority & State Authority` над `FixedUpdateNetwork`.

**Тик и время.** Симуляция идёт с частотой 64 Гц (`TickRateSelection.Client: 64`). Но отправка пакетов — **32 Гц**: поля `ClientSendIndex: 1` и `ServerSendIndex: 1` — это *индексы делителя*, а не частоты. Подтверждение в раздаче, `Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs`, `TickRateDrawer` (~стр. 1552–1676): кнопки делителя подписаны `"1:1"` при `currentValue == 0`, подписи вариантов строятся как `ratioOptions[i] = tickRate.GetTickRate(i)`, а поля рисуются как «Client Tick Rate» / «Client Send Rate» / «Server Tick Rate» / «Server Send Rate». Там же комментарий про `ServerIndex`: *«Force it to be 1:1 with the client tick rate - since different tick rates are not supported»* — отсюда `ServerIndex: 0`.

**Практический вывод, который и объясняет весь префаб игрока:** 64 тика симуляции против 32 отправок в секунду = интервал снапшота ≈ 31.25 мс. Прокси-копии получают позицию в два раза реже, чем она считается. Без интерполяции они двигались бы рывками по 31 мс. Именно это, а не абстрактная «гладкость», делает `NetworkTransform` на префабе обязательным.

---

## Архитектура раздела

```
                 ┌───────────────────────── Сцена SampleScene ──────────────────────────┐
                 │                                                                      │
  [System] ──────┤  1. NetworkRunner           ← лежит в сцене, НЕ создаётся кодом      │
  (один GameObject, 2. NetworkSceneManagerDefault                                       │
   все компоненты   3. EntryPoint ─── Start() ──► StartGame(StartGameArgs)              │
   в одном стеке)   4. NetworkEvents  ← ДОЛЖЕН быть на GameObject раннера               │
                 │  5. PlayerJoinController ─┐                                          │
                 │  6. SpawnPointService     │ подписка только из кода,                  │
                 │  7. PlayerLeftController ─┤ OnEnable / OnDisable                      │
                 │  8. InputAccumulator ─────┘                                          │
                 │  дети: SpawnPoint1, SpawnPoint2                                       │
                 │                                                                      │
  [Camera] ──────┤  Camera + EnableOnSingleRunner                                       │
  Directional Light  Light + EnableOnSingleRunner                                       │
  [Environment] ─┤  Plane (материал Green.mat)                                          │
                 └──────────────────────────────────────────────────────────────────────┘

ПОТОК ВВОДА (один тик, 64 Гц):

  Устройство            Пир с ProvideInput                 Все пиры
  ─────────             ──────────────────                 ────────
  Input.GetAxis  ──►  NetworkEvents.OnInput
                        └► InputAccumulator.OnInput
                             └► input.Set(InputData)   ──┐
                                                         │ сериализация,
                                                         │ привязка к номеру тика,
                                                         │ отправка 32 Гц
                                                         ▼
                                           MoveController.FixedUpdateNetwork()
                                             └► GetInput(out InputData)      → true только
                                                  └► direction * Runner.DeltaTime  у владельца
                                                       └► MoveComponent.MoveStep(step)
                                                            └► transform.position += step * _speed
                                                                 ▼
                                                       NetworkTransform (сетевое состояние)
                                                                 ▼
                                           на прокси: интерполяция между снапшотами (≈31.25 мс)
```

**Три слоя, сознательно плоские, без DI-контейнера и без синглтонов.**

**Слой запуска.** `EntryPoint` — обычный `MonoBehaviour`, не `NetworkBehaviour`. Раннер и scene-manager прокинуты через `[SerializeField]` и живут в сцене **до** старта игры. Это ключевое архитектурное решение: все остальные системы могут безопасно ссылаться на раннер и на `NetworkEvents` в инспекторе, не решая задачу «найти раннер в рантайме».

**Слой системных контроллеров.** Один объект `[System]`, на нём весь стек. Каждый «интерес» — отдельный крошечный класс на одно событие. Фактический порядок компонентов в YAML, кстати, не совпадает с логическим: `PlayerJoinController` (fileID 933717206) стоит **раньше** `SpawnPointService` (933717205). Это не ошибка и не баг — и то, что это безразлично, само по себе важный факт (см. шаг 6).

**Слой игрового объекта.** Префаб `Player`: корень несёт только `NetworkObject`, `NetworkTransform`, `MoveComponent`, `MoveController`. Визуал (MeshFilter, MeshRenderer, CapsuleCollider) вынесен в дочерний `Visual` с `m_LocalPosition.y = 1`.

---

## Реализация по шагам

### Шаг 1. Точка входа: один раннер, одна сцена, одна строка конфигурации

Файл: `Assets/Scripts/EntryPoint.cs`

```csharp
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame
{
    public sealed class EntryPoint : MonoBehaviour
    {
        [SerializeField]
        private NetworkRunner _networkRunner;

        [SerializeField]
        private NetworkSceneManagerDefault _sceneManager;
        
        private void Start()
        {
            _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = "SampleSession",
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = _sceneManager
            });
        }
    }
}
```

**Почему именно так.**

`GameMode.AutoHostOrClient` снимает необходимость в UI выбора роли: первый процесс с данным `SessionName` становится хостом, остальные — клиентами. Для учебного запуска через ParrelSync это два клика вместо кнопок Host/Client. **Следствие, которое надо впитать:** роль **не детерминирована**. Никогда не пиши логику, завязанную на «я всегда клиент» или на константу сборки — всё ветвление делается через `runner.IsServer` в рантайме.

`SessionName = "SampleSession"` — жёстко зашитая константа. В учебном проекте это рандеву-точка для ParrelSync-клонов. **В реальном проекте так нельзя**, и здесь это особенно остро: `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` приезжает в раздаче с **уже заполненным чужим** `AppIdFusion: b8976a98-4b4b-405e-8a66-0e4404b7e0bb`. Проект запускается «из коробки», но это значит, что все, кто скачал раздачу, сидят на одном AppId с одинаковым константным именем сессии: общий лимит CCU и общее пространство сессий. Свой AppId надо подставлять осознанно.

`Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)` — раннер берёт на себя **уже загруженную** сцену, а не грузит её повторно. Без этого сцена перезагрузилась бы и уничтожила объекты, на которые ссылается инспектор. Работает это потому, что сцена есть в `EditorBuildSettings` (там она единственная, индекс 0).

**Важная поправка к распространённому заблуждению.** `IsSceneTakeOverEnabled: 1` в сцене — это **дефолт Fusion, а не настройка автора**: в `Assets/Photon/Fusion/Runtime/NetworkSceneManagerDefault.cs` поле объявлено как `public bool IsSceneTakeOverEnabled = true;` (стр. 22), `LogSceneLoadErrors = true` (стр. 30). И смысл опции уже, чем «перехват открытой сцены» — её собственный XML-doc: *«If enabled and there is an already loaded scene that matches what the scene manager has intended to load, that scene will be used instead and load will be avoided»*. То есть: совпала уже загруженная сцена → повторная загрузка пропускается. **Ценность приёма держится на `Scene = SceneRef.FromIndex(активная сцена)`, а не на галочке.** Передача своего scene-manager здесь нужна, чтобы настройки были видны в инспекторе одного объекта, а не потому, что без неё take-over не заработает.

⚠️ **В проекте не реализовано, рекомендация:** `StartGame` возвращает `Task<StartGameResult>`, и результат здесь не ожидается и не проверяется — ошибка подключения молча утонет. В своём коде делай метод `async void`/`async Task`, `await`-ь результат и смотри `StartGameResult.Ok`, иначе при неверном AppId или отсутствии сети ты получишь пустую сцену без единой записи в консоли.

### Шаг 2. Контракт ввода

Файл: `Assets/Scripts/System/Input/InputData.cs`

```csharp
using Fusion;
using UnityEngine;

namespace SampleGame
{
    public struct InputData : INetworkInput
    {
        public Vector3 direction;
    }
}
```

**Почему именно так.** `INetworkInput` — маркерный интерфейс, по которому Fusion ILWeaver генерирует сериализацию. Обязательно `struct` и только блиттируемые поля: никаких ссылок, строк, массивов, `List`. Именно поэтому в конфиге можно оставить `InputDataWordCount: 0` — weaver вычисляет размер сам, но **только** если сборка с кодом перечислена в `AssembliesToWeave` (там есть `Assembly-CSharp`).

Цена контракта: `Vector3` = 12 байт на тик. При 64 тиках это заметный трафик для одного вектора направления. В реальном проекте направление упаковывают в `byte`/короткий угол, а кнопки — в битовую маску `NetworkButtons`.

### Шаг 3. Сбор ввода: один раз на тик, а не на кадр

Файл: `Assets/Scripts/System/Input/InputAccumulator.cs`

```csharp
public sealed class InputAccumulator : MonoBehaviour
{
    [SerializeField]
    private NetworkEvents _networkEvents;

    private void OnEnable()
    {
        _networkEvents.OnInput.AddListener(this.OnInput);
    }

    private void OnDisable()
    {
        _networkEvents.OnInput.RemoveListener(this.OnInput);
    }

    private void OnInput(NetworkRunner runner, NetworkInput input)
    {
        float dx = Input.GetAxis("Horizontal");
        float dz = Input.GetAxis("Vertical");

        var moveDirection = new Vector3(dx, 0, dz);
        var inputData = new InputData
        {
            direction = moveDirection
        };

        input.Set(inputData);
    }
}
```

**Почему именно так.** Чтение устройства происходит **внутри колбэка `OnInput`**, который Fusion вызывает один раз на тик симуляции, и результат немедленно упаковывается через `input.Set`. Ввод становится привязан к номеру тика и может быть переигран при ресимуляции. Читай ввод в `Update` — и он потеряет привязку к тику: сервер и клиент прогонят один тик с разными данными, предсказание разъедется.

`Input.GetAxis`, а не `Input.GetKeyDown` — это **правильно, но по более узкой причине, чем кажется**. Дискретные `GetKeyDown`/`GetKeyUp` живут один кадр Unity, а `OnInput` привязан к тикам. Unity Fixed Timestep здесь 0.02 (50 Гц), Fusion tick rate — 64 Гц; кадр и тик не совпадают, поэтому однокадровое нажатие можно пропустить или продублировать. Для кнопок правильный паттерн — накапливать их в поле между вызовами `OnInput` и сбрасывать после `input.Set`, либо хранить состояние как `NetworkButtons`.

Legacy Input здесь не случайность: `ProjectSettings/ProjectSettings.asset`, стр. 848 — `activeInputHandler: 0`, то есть Input Manager (Old). Это и есть прямое доказательство (пакет Input System можно иметь установленным и при `activeInputHandler: 0`, так что по `manifest.json` судить нельзя).

⚠️ **Две реальные дырки в этом фрагменте — не повторяй их.**

1. `Input.GetAxis` применяет **сглаживание** Unity (gravity/sensitivity из InputManager) по **кадрам**. Одно и то же физическое удержание клавиши даст разные значения оси на пирах с разным FPS, а `OnInput` привязан к тикам. Tick-корректный вариант — `Input.GetAxisRaw` (или Input System) плюс своя нормализация.
2. `new Vector3(dx, 0, dz)` **никогда не нормализуется**. По диагонали персонаж едет до √2 ≈ 1.41 раза быстрее — классический баг учебных проектов. Делай `Vector3.ClampMagnitude(moveDirection, 1f)` или `.normalized` при ненулевой длине.

### Шаг 4. Применение ввода: единственное место, где меняется состояние

Файл: `Assets/Scripts/GameObject/MoveController.cs`

```csharp
using Fusion;
using UnityEngine;

namespace SampleGame
{
    public sealed class MoveController : NetworkBehaviour
    {
        [SerializeField]
        private MoveComponent _moveComponent;

        //Only Input Authority & State Authority
        public override void FixedUpdateNetwork()
        {
            Debug.Log($"FIXED UPDATE {this.Object.Id}");
            
            if (this.GetInput(out InputData inputData))
            {
                Vector3 moveStep = inputData.direction * this.Runner.DeltaTime;
                _moveComponent.MoveStep(moveStep);
                Debug.Log($"GET INPUT {this.Object.Id}");
            }
        }
    }
}
```

**Почему именно так.** Три вещи, каждая обязательна.

*Состояние меняется только в `FixedUpdateNetwork`.* Fusion зовёт его по тикам и повторно при ресимуляции предсказанных тиков. Трогай `transform` в `Update`/`LateUpdate` — и `NetworkTransform` с ресимуляцией начнут бороться с твоим кодом: получишь дрожание.

*`GetInput` — это и есть authority-гейт.* Он возвращает `true` только там, где есть право читать ввод, и `false` на прокси. Два `Debug.Log` по `Object.Id` в учебном коде позволяют увидеть это вживую: `FIXED UPDATE` печатается для **всех** объектов на **всех** пирах, а `GET INPUT` — только для своего.

*Шаг масштабируется на `Runner.DeltaTime`.* Это единственный источник времени в игровой логике главы. Подстановка `Time.deltaTime`/`Time.fixedDeltaTime` сломала бы детерминизм: Unity Fixed Timestep 0.02 (50 Гц) ≠ Fusion tick rate 64 Гц, числа разные, дистанция за тик разъехалась бы между пирами и при ресимуляции.

**А вот про сами `Debug.Log` надо сказать честно.** Они дидактически полезны, но они же — живая демонстрация **неидемпотентности**: при ресимуляции предсказанного тика лог сработает повторно для одного и того же тика, и консоль покажет один тик несколько раз. Это ровно тот побочный эффект, от которого надо избавляться в бою. Правило: **любой побочный эффект в `FixedUpdateNetwork` (звук, VFX, лог, аналитика) оборачивай в проверку `Runner.IsForward`**, иначе он отработает N раз на один тик. Члены `IsForward` и `IsResimulation` присутствуют в метаданных `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.dll`.

⚠️ **Ветка `else` отсутствует.** При потере пакета ввода объект просто не двигается этот тик. Штатное решение Fusion 2 — колбэк `OnInputMissing`, см. шаг 9.

### Шаг 5. Чистая механика без сетевой осведомлённости

Файл: `Assets/Scripts/GameObject/MoveComponent.cs`

```csharp
using UnityEngine;

namespace SampleGame
{
    public sealed class MoveComponent : MonoBehaviour
    {
        [SerializeField]
        private float _speed = 5;
        
        public void MoveStep(Vector3 step) => 
            this.transform.position += step * _speed;
    }
}
```

**Почему именно так.** Нулевая сетевая осведомлённость: обычный `MonoBehaviour`, никаких `[Networked]`, `Runner` или authority. Это делает механику переиспользуемой (AI, катсцена, локальный прототип) и тестируемой обычными unit-тестами, а сетевой слой — тонким настолько, что его можно глазами прочитать на предмет authority-ошибок. Ссылка прокинута `[SerializeField]`, без `GetComponent` в рантайме.

**А теперь самое важное и самое неочевидное место всей главы.** `this.transform.position += step * _speed` — это **относительная мутация**, и она по определению **не идемпотентна**: два прогона одного тика дадут двойной сдвиг. Почему же это работает корректно и в предсказанном, и в авторитетном тике?

Не потому, что код идемпотентен. А потому, что **позиция живёт не в обычном поле `MonoBehaviour`, а в сетевом состоянии `NetworkTransform`**, и Fusion перед ресимуляцией тика восстанавливает это состояние — после чего `transform.position` читается уже откатанным. *(Это вывод о поведении Fusion, а не факт из учебного кода. Подкрепляется тем, что `NetworkTransform` наследует `NetworkTRSP`, то есть transform — часть сетевого снапшота; `NetworkTRSP` есть в метаданных `Fusion.Runtime.dll`, и в `Assets/Photon/Fusion/release_history.txt` для 2.0.5 Build 920 есть запись «`NetworkTRSP` state authority change error correction delta default value set to 0».)*

**Переносимое правило, которое из этого следует:**

> `+=` по `transform` безопасно **только** пока эта позиция является сетевым состоянием (`NetworkTransform` / `NetworkRigidbody` / `NetworkCharacterController`). Сними `NetworkTransform` с префаба — и тот же `MoveComponent` поедет: ресимуляция начнёт накапливать шаги, а прокси-копии вообще перестанут двигаться.
>
> Любое **своё** накопительное значение (счётчик патронов, таймер перезарядки, здоровье) в обычном поле `MonoBehaviour` ломается по той же причине и обязано быть `[Networked]`.

### Шаг 6. Server-authoritative спавн

Файл: `Assets/Scripts/System/Player/PlayerJoinController.cs`

```csharp
private void OnEnable()
{
    _networkEvents.PlayerJoined.AddListener(this.OnPlayerJoined);
}

private void OnDisable()
{
    _networkEvents.PlayerJoined.RemoveListener(this.OnPlayerJoined);
}

private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
{
    if (!runner.IsServer)
        return;

    Transform spawnPoint = _pointService.GetSpawnPoint(player);
    NetworkObject character = runner.Spawn(_characterPrefab, spawnPoint.position, spawnPoint.rotation, player);
    runner.SetPlayerObject(player, character);
}
```

**Почему именно так — четыре приёма в семи строках.**

`if (!runner.IsServer) return;` — гейт авторитета. Колбэк прилетает на **все** пиры; без гейта каждый клиент плодил бы дубль или падал на отсутствии state authority. Обрати внимание: проверяется **роль в рантайме**, а не константа сборки — потому что `AutoHostOrClient` не даёт знать роль заранее. Во всём учебном коде главы `runner.IsServer` — **единственный** authority-гейт, и стоит он ровно в двух контроллерах жизненного цикла. Ни одного `HasStateAuthority`, `Object.HasInputAuthority`, `Runner.IsClient` или `Runner.IsSharedModeMasterClient` в разделе нет.

Правило размещения делегировано в `SpawnPointService` — сетевой код не знает, как выбирается точка.

**Четвёртый аргумент `Spawn` — это input authority.** Полная сигнатура в Fusion 2: `Spawn(GameObject prefab, Vector3? position = null, Quaternion? rotation = null, PlayerRef? inputAuthority = null, OnBeforeSpawned onBeforeSpawned = null, NetworkObjectPredictionKey? predictionKey = null)` — все позиционные параметры опциональные и nullable. Забыть `player` — и объект создастся, но `GetInput` на машине владельца всегда вернёт `false`: игрок неподвижен без единой ошибки в консоли. Классическая «тихая» поломка.

**Про пятый параметр, которого здесь нет, но который понадобится сразу же.** `onBeforeSpawned` — канонический способ проинициализировать `[Networked]`-состояние **до** того, как объект отреплицируется. Здесь без него обходятся именно потому, что `[Networked]`-полей нет вовсе. Как только они появятся, инициализация в `Spawned()` будет уже поздно: клиенты сначала увидят объект с нулевым состоянием.

`SetPlayerObject` вместо собственного словаря: связка игрок↔объект уходит в **реплицируемый** реестр Fusion, доступный и клиентам, и переживающий смену хоста. Свой `Dictionary<PlayerRef, NetworkObject>` живёт только на хосте, клиентам не видим и теряется при host migration.

⚠️ `private GameObject _characterPrefab` типизирован как `GameObject`, а не `NetworkObject`/`NetworkPrefabRef`. Компилятор не гарантирует, что в инспектор положили префаб с `NetworkObject`. Типизируй поле `NetworkObject` — тогда ошибка возникнет на этапе назначения в инспекторе, а не в рантайме.

**И ключевая деталь про порядок инициализации, которая здесь load-bearing.** Подписка стоит в `OnEnable`, а `StartGame` вызывается в `Start()` (`EntryPoint`). Unity гарантирует, что `Awake`/`OnEnable` **всех** компонентов активного объекта выполнятся до `Start` **любого** из них. Поэтому `PlayerJoinController`, `PlayerLeftController` и `InputAccumulator` заведомо подписаны к моменту `StartGame`. `PlayerJoined` для локального игрока хоста прилетает практически сразу после `StartGame` — **перенеси подписку в `Start()`, и всё начнёт зависеть от порядка компонентов на GameObject, а персонаж хоста иногда не будет спавниться вообще.** Перенести `StartGame` в `Awake` поломало бы это с другой стороны. Именно поэтому фактический порядок компонентов в YAML (`PlayerJoinController` раньше `SpawnPointService`) безразличен: зависимости прокинуты ссылками, а подписки — в `OnEnable`.

### Шаг 7. Зеркальный деспавн

Файл: `Assets/Scripts/System/Player/PlayerLeftController.cs`

```csharp
private void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
{
    if (runner.IsServer)
    {
        NetworkObject character = runner.GetPlayerObject(player);
        runner.Despawn(character);
    }
}
```

**Почему именно так.** Тот же гейт `IsServer`, и объект находится через `GetPlayerObject` — реестр Fusion используется и на запись, и на чтение. Контроллер полностью stateless, поэтому его и можно держать отдельным классом от `PlayerJoinController`.

⚠️ **Две дыры, которые надо закрыть в своём коде.**

1. `GetPlayerObject` может вернуть `null` (игрок отвалился до завершения спавна, объект уже снят), а null-проверки перед `Despawn` нет. Готовое решение есть в API — `TryGetPlayerObject` (член присутствует в метаданных `Fusion.Runtime.dll`). Правильный вид обработчика:
   ```csharp
   if (runner.IsServer && runner.TryGetPlayerObject(player, out NetworkObject character))
       runner.Despawn(character);
   ```
2. После `Despawn` **не вызывается** `runner.SetPlayerObject(player, null)`. Запись в реестре остаётся висеть на уничтоженный объект, и следующий `GetPlayerObject` по тому же (переиспользованному) `PlayerId` может вернуть мусор.

### Шаг 8. Правило размещения в stateless-сервисе

Файл: `Assets/Scripts/System/SpawnPointService.cs`

```csharp
using Fusion;
using UnityEngine;

namespace SampleGame
{
    public sealed class SpawnPointService : MonoBehaviour
    {
        [SerializeField]
        private Transform _spawnPoint1;
        
        [SerializeField]
        private Transform _spawnPoint2;

        public Transform GetSpawnPoint(PlayerRef player)
        {
            return player.PlayerId == 1 
                ? _spawnPoint1 
                : _spawnPoint2;
        }
    }
}
```

**Почему именно так.** Логика «куда ставить игрока» меняется часто (команды, рандом, круг, сохранённая позиция). Вшей её в обработчик `PlayerJoined` — и каждое изменение правил будет править сетевой код. Сервис принимает `PlayerRef` и ничего не знает ни о `Spawn`, ни о раннере; точки спавна — обычные `Transform` в сцене, дети `[System]`.

⚠️ **Это одновременно и анти-пример, который полезно разобрать.** `PlayerId` переиспользуется после выхода игрока и не гарантирует «первый зашедший = 1». А при `PlayerCount: 10` из конфига **все игроки с третьего встанут в одну точку**. Реальному проекту нужен пул точек и выдача по кругу/занятости.

### Шаг 9 (⚠️ в проекте не реализовано). `OnInputMissing` — штатный ответ на потерю ввода

В сцене у `NetworkEvents` есть UnityEvent `OnInputMissing`, но ни один учебный класс на него не подписан. Это прямое штатное решение проблемы из шага 4. Сигнатура подтверждается по раздаче — `Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs`:

```csharp
void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
```

**Зачем:** колбэк вызывается на сервере, когда ввод игрока за тик не пришёл, и позволяет подставить предыдущий ввод (`input.Set(lastInput)`) **один раз централизованно**, вместо того чтобы каждый `NetworkBehaviour` кэшировал последний ввод у себя.

**Что сломается иначе:** при обычном джиттере объект стоит один тик, едет следующий — получается дрожание, а на клиенте-владельце ещё и постоянная коррекция предсказания.

---

## Fusion API этой главы

| API | Что делает | Где вызывается | Подводный камень |
|---|---|---|---|
| `NetworkRunner.StartGame(StartGameArgs)` | Поднимает симуляцию, подключается к Photon Cloud, решает роль пира | `EntryPoint.Start()` | Возвращает `Task` — в коде не ожидается, ошибка подключения молча теряется. `await`-ь и проверяй `StartGameResult.Ok` |
| `GameMode.AutoHostOrClient` | Первый процесс с данным `SessionName` → хост, остальные → клиенты | `EntryPoint` | Роль не детерминирована. Всё ветвление только через `runner.IsServer` в рантайме |
| `StartGameArgs.SessionName` | Строковый ключ матчмейкинга | `EntryPoint` | Константа `"SampleSession"` + чужой `AppIdFusion` в раздаче = общее пространство сессий со всеми, у кого та же раздача |
| `SceneRef.FromIndex(...)` + `StartGameArgs.Scene` | Объявляет сетевой сценой уже открытую сцену | `EntryPoint` | Работает только если сцена есть в `EditorBuildSettings`. Это и есть механизм take-over, а не галочка на scene-manager |
| `StartGameArgs.SceneManager` | Передаёт реализацию `INetworkSceneManager` | `EntryPoint` | Не передашь — Fusion создаст `NetworkSceneManagerDefault` сам, с теми же дефолтами (`IsSceneTakeOverEnabled = true`). Передача нужна для видимости настроек, не для работоспособности |
| `NetworkEvents` | Штатный компонент, реализующий `INetworkRunnerCallbacks` и переизлучающий **19** колбэков как UnityEvent | Компонент в сцене на GameObject раннера | Должен лежать **на GameObject раннера**. Имена UnityEvent ≠ имена методов интерфейса (см. ниже) |
| `INetworkRunnerCallbacks.OnInput` → `NetworkInput.Set<T>()` | Сбор локального ввода один раз на тик и упаковка в сетевую структуру | `InputAccumulator.OnInput` | Вызывается по тикам, а не по кадрам, и только на пире с `ProvideInput`. `GetKeyDown` здесь ловить нельзя |
| `INetworkRunnerCallbacks.OnInputMissing` | Сервер сообщает, что ввод игрока за тик не пришёл | ⚠️ не используется | Единственное централизованное место, где можно переиспользовать последний ввод |
| `INetworkInput` | Контракт сериализуемого ввода; маркер для weaver | `InputData` | Обязательно `struct`, только блиттируемые поля. `Vector3` = 12 байт × 64 тика |
| `NetworkBehaviour.FixedUpdateNetwork()` | Тик симуляции; единственное место, где меняется состояние | `MoveController` | Зовётся на **всех** пирах для **всех** объектов и повторно при ресимуляции. Побочные эффекты оборачивай в `Runner.IsForward` |
| `NetworkBehaviour.GetInput(out T)` | Достаёт `InputData` текущего тика; `false` без права на ввод | `MoveController.FixedUpdateNetwork` | Ветка `false` не обработана: потеря пакета = тик без движения |
| `NetworkRunner.DeltaTime` | Длительность одного тика симуляции | `MoveController` | Никогда не заменяй на `Time.deltaTime`/`Time.fixedDeltaTime`: 0.02 (50 Гц) ≠ 1/64 |
| `NetworkRunner.Spawn(prefab, position?, rotation?, inputAuthority?, onBeforeSpawned?, predictionKey?)` | Создаёт сетевой объект и выдаёт input authority | `PlayerJoinController` | 4-й аргумент — **input**, не state authority. Забудешь — `GetInput` всегда `false`, без ошибок. 5-й (`onBeforeSpawned`) — единственный способ задать `[Networked]`-состояние до репликации |
| `NetworkRunner.IsServer` | Гейт на спавн/деспавн | Оба контроллера жизненного цикла | `PlayerJoined`/`PlayerLeft` приходят на все пиры. Без гейта — дубли или ошибка state authority |
| `NetworkRunner.SetPlayerObject` / `GetPlayerObject` | Реплицируемый реестр «игрок → главный NetworkObject» | `PlayerJoinController` / `PlayerLeftController` | `GetPlayerObject` может вернуть `null`. Используй `TryGetPlayerObject`. И обнуляй запись после `Despawn` |
| `NetworkRunner.Despawn(NetworkObject)` | Снимает персонажа | `PlayerLeftController` | Null-проверки в коде нет — реальная дыра |
| `PlayerRef` / `PlayerRef.PlayerId` | Сетевой идентификатор игрока | `SpawnPointService` | `PlayerId` переиспользуется после выхода и не даёт стабильной нумерации |
| `NetworkTransform` | Репликация + интерполяция позиции/поворота | Компонент на префабе | Единственное сетевое состояние главы. Прячет разрыв 64 Гц симуляции / 32 Гц отправки. Снимешь — `+=` по transform начнёт накапливать при ресимуляции |

**Ловушка, которая стоит отдельного абзаца: имена UnityEvent у `NetworkEvents` НЕ совпадают с именами методов интерфейса.** `PlayerJoined` против `INetworkRunnerCallbacks.OnPlayerJoined`, `PlayerLeft` против `OnPlayerLeft`, `OnSimulationMessage` против `OnUserSimulationMessage`, `OnSessionListUpdate` против `OnSessionListUpdated`, `OnReliableData` против `OnReliableDataReceived`. Имена методов видны в `Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/RunnerEnableVisibility.cs` (стр. 65–81). При переходе с ручной реализации `INetworkRunnerCallbacks` на `NetworkEvents` поиск по именам ничего не найдёт.

Полный список 19 UnityEvent из YAML сцены: `OnInput`, `OnInputMissing`, `OnConnectedToServer`, `OnDisconnectedFromServer`, `OnConnectRequest`, `OnConnectFailed`, `PlayerJoined`, `PlayerLeft`, `OnSimulationMessage`, `OnShutdown`, `OnSessionListUpdate`, `OnCustomAuthenticationResponse`, `OnHostMigration`, `OnSceneLoadDone`, `OnSceneLoadStart`, `OnReliableData`, `OnReliableProgress`, `OnObjectEnterAOI`, `OnObjectExitAOI`.

---

## Настройки, без которых не работает

### `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`

```
"PeerMode": 0,
"LagCompensation": { "Enabled": false, "HitboxBufferLengthInMs": 200 },
"Simulation": {
    "InputDataWordCount": 0,
    "ReplicationFeatures": 1,
    "InputTransferMode": 0,
    "SimulationUpdateTimeMode": 0,
    "PlayerCount": 10,
    "TickRateSelection": { "Client": 64, "ServerIndex": 0, "ClientSendIndex": 1, "ServerSendIndex": 1 }
},
"HostMigration": { "EnableAutoUpdate": false, "UpdateDelay": 10 },
"NetworkConditions": { "Enabled": false, "DelayMin": 0.15, "DelayMax": 0.15, "AdditionalJitter": 0.05 },
"AssembliesToWeave": [ "Fusion.Unity", "Assembly-CSharp", "Assembly-CSharp-firstpass", "Fusion.Addons.Physics", "Fusion.Addons.FSM" ]
```

- **`TickRateSelection`: симуляция 64 Гц, отправка 32 Гц.** `Client: 64` — тик. `ServerIndex: 0` — тик сервера 1:1 с клиентом (разные тик-рейты не поддерживаются). `ClientSendIndex: 1` и `ServerSendIndex: 1` — делитель 1/2, то есть **send rate = 32 Гц**, интервал снапшота ≈31.25 мс. Это то число, которое стоит за `Runner.DeltaTime`, и то, из-за чего нужна интерполяция прокси.
- **`PeerMode: 0` (Single)** — один раннер на процесс. Поэтому второго игрока в том же редакторе не получить, и поэтому в раздаче лежит ParrelSync (`Assets/ParrelSync`, `Assets/Plugins/ParrelSync/ScriptableObjects/ParrelSyncProjectSettings.asset`): клон на симлинках `Assets`/`ProjectSettings`/`Packages` гарантирует одинаковый код и конфиг у хоста и клиента.
- **`InputDataWordCount: 0`** работает только потому, что `Assembly-CSharp` входит в `AssembliesToWeave`. Вынесешь учебный код в свой asmdef и забудешь добавить его сюда — `[Networked]`-поля и `INetworkInput` **тихо** перестанут работать.
- **`PlayerCount: 10`** против двух точек спавна в `SpawnPointService` — рассогласование, о котором надо знать.
- `LagCompensation.Enabled: false` (`HitboxBufferLengthInMs 200`, `HitboxDefaultCapacity 512`), `HostMigration.EnableAutoUpdate: false` — явные границы главы.
- `NetworkConditions.Enabled: false`, но заготовлены `DelayMin/Max: 0.15`, `AdditionalJitter: 0.05`, `LossChance: 0.05` — готовый тумблер для демонстрации десинхрона.
- Прочее: `Network.ConnectionTimeout 10.0`, `ConnectionShutdownTime 1.0`, `ReliableDataTransferModes 3`; `EncryptionConfig.EnableEncryption: false`; `Heap { PageShift: 15, PageCount: 256 }`; `NullChecksForNetworkedProperties: true`, `UseSerializableDictionary: true`, `CheckRpcAttributeUsage: false`.
- `ReplicationFeatures: 1` и `InputTransferMode: 0` — дефолты, но `InputTransferMode` стоит держать в голове: enum `InputTransferModes` есть в `Fusion.Runtime.dll`, и он определяет, как ввод передаётся между пирами, в том числе могут ли клиенты видеть ввод друг друга. Это прямо влияет на то, что вообще можно читать в `GetInput` на не-владельце.

### `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`

`AppIdFusion: b8976a98-4b4b-405e-8a66-0e4404b7e0bb` — **заполнен**, `AppIdChat`/`AppIdVoice` пусты, `UseNameServer: 1`, `Protocol: 0`, `AuthMode: 0`, `EnableLobbyStatistics: 0`. Проект стартует «из коробки» на чужом AppId. Для своего проекта это первое, что надо менять.

### `ProjectSettings/`

- `TimeManager.asset`: Fixed Timestep **0.02** (50 Гц), Maximum Allowed Timestep 0.33333334. **Не совпадает** с Fusion tick rate 64 — отсюда правило никогда не использовать `Time.fixedDeltaTime` в `FixedUpdateNetwork`.
- `EditorBuildSettings.asset`: ровно одна сцена `Assets/Scenes/SampleScene.unity`, `enabled: 1`, индекс 0. Без неё `SceneRef.FromIndex(buildIndex)` не сработал бы.
- `ProjectSettings.asset`: `productName PhotonSample`, `companyName DefaultCompany`, `activeInputHandler: 0` (Input Manager Old).
- `DynamicsManager.asset`: `m_Gravity (0, -9.81, 0)`, `m_AutoSimulation: 1`, `m_DefaultSolverIterations: 6`. Автосимуляция Unity-физики здесь безвредна, потому что игрок кинематический и Rigidbody нет; в главе про физику её обычно приходится отключать и гонять из Fusion.

### Сцена `Assets/Scenes/SampleScene.unity`

Корневые объекты: `[System]`, `[Camera]`, `[Environment]` (с Plane), `Directional Light`.

На `[System]` (фактический порядок компонентов из YAML): Transform, `NetworkRunner` (guid `e725a070cec140c4caffb81624c8c787`, fileID `-1199893898`, **без единого сериализованного поля**), `NetworkSceneManagerDefault` (`IsSceneTakeOverEnabled: 1`, `LogSceneLoadErrors: 1` — дефолты), `EntryPoint`, `NetworkEvents` (fileID `-232609262`, все 19 UnityEvent с **пустыми** `m_PersistentCalls.m_Calls`), `PlayerJoinController`, `SpawnPointService`, `PlayerLeftController`, `InputAccumulator`. Дети: `SpawnPoint1`, `SpawnPoint2` — пустые Transform.

**Критично:** `NetworkEvents` лежит на том же GameObject, что `NetworkRunner`, и в коде нигде нет `runner.AddCallbacks` (проверено по всему проекту, включая вендор — ни одного вызова). Это **документированный контракт Fusion**, а не догадка. Подтверждение в раздаче:

1. `Assets/Photon/Fusion/Runtime/FusionBootstrap.cs`, стр. ~61–63: *«Supply a Prefab or a scene object which has the `NetworkRunner` component on it, as well as any runner dependent components which implement `INetworkRunnerCallbacks`, such as `NetworkEvents` or your own custom INetworkInput implementations»*.
2. `Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs`, стр. 12066: пункт меню «Tools/Fusion/Scene/Setup Networking in the Scene» делает `n.nr.gameObject.EnsureComponentExists<NetworkEvents>();`.

Перенесёшь `NetworkEvents` на другой объект — ввод, спавн и деспавн **молча** перестанут работать.

Вывод, который остаётся выводом: `ProvideInput` у `NetworkRunner` в сцене не задан явно (у компонента нет сериализованных полей). Что `OnInput` вообще вызывается, объясняется значением по умолчанию `true`.

**Вендорный компонент, который легко пропустить.** `Fusion.EnableOnSingleRunner` (guid `d73b0a95be81246699cf0d9ecdc01863`) висит в сцене на **двух** объектах: на `[Camera]` (`PreferredRunner: 0`, `Components: [{fileID: 963194226}]` — сама Camera) и на `Directional Light` (`Components: [{fileID: 705507994}]` — сам Light); у каждого свой сериализованный `_guid`. Его собственный XML-doc (`Assets/Photon/Fusion/Runtime/Utilities/RunnerVisibility/EnableOnSingleRunner.cs`): *«Automatically adds a `RunnerVisibilityLink` for each indicated component. These indicated components will be limited to no more than one enabled instance when running in Multi-Peer mode»*. **Зачем:** в Multi-Peer режиме в одном процессе живёт несколько `NetworkRunner`, сцена дублируется, и ты получаешь N камер, N источников света и N AudioListener (лог-спам и «белая» картинка). При `PeerMode: 0` компонент практически no-op — его добавил ассистент Fusion (там же `HandleAudioListeners()`/`HandleLights()`, `Fusion.Unity.Editor.cs` ~стр. 12060–12090). **Переносимое знание:** как только переключишься на Multi-Peer для тестов в одном редакторе, единственность камеры/света/листенера становится обязательной темой.

### Префаб `Assets/Prefabs/Player.prefab`

```yaml
--- !u!114 &2651116395783247469   # NetworkObject
  m_Script: {fileID: -1552182283, guid: e725a070cec140c4caffb81624c8c787, type: 3}
  SortKey: 2591673740
  ObjectInterest: 1
  Flags: 262145
  NestedObjects: []
  NetworkedBehaviours:
  - {fileID: 2109334254631807660}   # NetworkTransform
  - {fileID: 449620599510217154}    # MoveController
  ForceRemoteRenderTimeframe: 0
--- !u!114 &2109334254631807660   # NetworkTransform
  _stateAuthorityChangeErrorCorrectionDelta: 0
  SyncScale: 0
  SyncParent: 0
  _autoAOIOverride: 1
  DisableSharedModeInterpolation: 0
--- !u!114 &6027591379984614441   # MoveComponent
  _speed: 5
--- !u!114 &449620599510217154    # MoveController
  _moveComponent: {fileID: 6027591379984614441}
```

Сетевых поведений ровно два. **Список `NetworkedBehaviours` и их порядок — часть сетевого контракта префаба**: добавление/удаление `NetworkBehaviour` меняет раскладку состояния, поэтому сборки с разными версиями префаба несовместимы.

`SyncScale: 0` и `SyncParent: 0` — то, что не меняется, не реплицируется: экономия байт на тик. Обратная сторона: начнёшь менять scale/parent в рантайме — на прокси этого не будет видно.

**Что такое `Flags: 262145`** — важно не ошибиться. Это `NetworkObjectFlags`, и инспектор рисует из них ровно три бита в секции «Shared Mode Settings» (`Fusion.Unity.Editor.cs`, стр. ~12673–12720): `MasterClientObject`, `AllowStateAuthorityOverride`, `DestroyWhenStateAuthorityLeaves`. То есть `Flags` относится к правилам authority в **Shared-режиме**, а не к репликации transform, и к сетевому контракту раскладки состояния отношения **не имеет**. Числовые значения конкретных битов остались в DLL.

`ObjectInterest` — **не битовая маска**, а enum-поле типа `NetworkObject.ObjectInterestModes` (`Fusion.Unity.Editor.cs`, стр. ~12731–12734, секция «Interest Management Settings», сравнивается с `ObjectInterestModes.AreaOfInterest`). `_autoAOIOverride` соответствует `AutoUpdateAreaOfInterestOverride`/`SetAreaOfInterestOverride` (есть в метаданных `Fusion.Runtime.dll`).

`_stateAuthorityChangeErrorCorrectionDelta: 0` — это **новый дефолт 2.0.5**, а не настройка автора (`release_history.txt`). Вместе с `DisableSharedModeInterpolation` это Shared-специфичные поля, не задействованные в этой главе.

Дочерний `Visual` (`m_LocalPosition.y = 1`): MeshFilter (встроенная капсула, mesh 10208), MeshRenderer (встроенный дефолтный материал 10303), CapsuleCollider (radius 0.5, height 2, direction 1, `isTrigger 0`). Коллайдер есть, но **в логике движения не участвует**: нет ни `Rigidbody`, ни `NetworkRigidbody`, ни `NetworkCharacterController`, ни `Hitbox`/`HitboxRoot`, ни `NetworkMecanimAnimator`.

Материалы: `Assets/Materials/Green.mat` (guid `95e895472c1f44ca8997905091d94530`) — это материал **земли**, он лежит на MeshRenderer объекта Plane внутри `[Environment]` (строка 250 сцены: `m_Materials: - {fileID: 2100000, guid: 95e895472c1f44ca8997905091d94530, type: 2}`). Дефолтный встроенный материал используется только на `Visual` в префабе игрока.

---

## Антипаттерны и как их обходит этот код

| Антипаттерн | Как обойдён | Что сломается иначе |
|---|---|---|
| God-класс на `INetworkRunnerCallbacks` | Компонент `NetworkEvents` + три подписчика по одному событию | Спавн, ввод, матчмейкинг и загрузка сцен склеиваются в нерефакторимый класс на 19 методов |
| Спавн на каждом пире | `if (!runner.IsServer) return;` в обоих контроллерах жизненного цикла | Ошибка отсутствия state authority на клиенте; логически — дубли персонажей |
| Забытая input authority | `PlayerRef` четвёртым аргументом `Spawn` | Объект создан, но `GetInput` у владельца всегда `false`: игрок неподвижен **без ошибок в консоли** |
| Свой `Dictionary<PlayerRef, NetworkObject>` | `SetPlayerObject`/`GetPlayerObject` | Словарь не реплицируется клиентам и не выживает при host migration |
| Чтение ввода в `Update` | Только внутри `OnInput`, сразу `input.Set` | Ввод теряет привязку к номеру тика, предсказание разъезжается |
| `GetKeyDown` в `OnInput` | Только непрерывные оси | Кадр (50 Гц) ≠ тик (64 Гц): нажатие пропускается или дублируется |
| `Time.deltaTime` в сетевой логике | Только `Runner.DeltaTime` | Fixed Timestep 0.02 ≠ 1/64: разная дистанция за тик у разных пиров и при ресимуляции |
| Изменение состояния вне `FixedUpdateNetwork` | Движение только в `FixedUpdateNetwork` | `NetworkTransform` и ресимуляция начнут бороться с кодом → дрожание |
| Побочные эффекты в тике | ⚠️ **не обойдён**: два `Debug.Log` в `FixedUpdateNetwork` | При ресимуляции сработают повторно для одного тика. Оборачивай в `Runner.IsForward` |
| Накопительная мутация без сетевого состояния | ⚠️ `transform.position +=` работает **только** потому, что позиция — состояние `NetworkTransform` | Сними `NetworkTransform` — ресимуляция начнёт накапливать шаги, прокси перестанут двигаться |
| `GetComponent`/`FindObjectOfType`/статические синглтоны | Всё через `[SerializeField]`, все 7 классов `sealed`, раннер берётся из `NetworkBehaviour.Runner` или из аргумента колбэка | В процессе может быть несколько раннеров/сцен, порядок инициализации статиков непредсказуем |
| Утечка подписок | Строго симметричные `OnEnable`/`OnDisable` | Вызовы в уничтоженные объекты и работающая логика у выключенного контроллера |
| Подписка через инспектор | Все `m_PersistentCalls` у `NetworkEvents` пусты | Инспекторные привязки не находятся поиском по коду, молча рвутся при переименовании, не видны в code review |
| Повторная загрузка сцены при старте | `Scene = SceneRef.FromIndex(активная сцена)` | Сцена перезагрузится и уничтожит объекты, на которые ссылается инспектор |
| Ручной выбор роли Host/Client | `AutoHostOrClient` + константный `SessionName` | Нужен UI и ветвление; и всё равно роль нельзя знать заранее |
| Сетевая позиция, завязанная на визуал | Меш и коллайдер в дочернем `Visual` (y=1) | Правка пивота/модели поедет в сеть и сломает интерполяцию |
| Лишний трафик в `NetworkTransform` | `SyncScale: 0`, `SyncParent: 0` | Байты на тик × 32 отправки/с |
| Жирный `INetworkInput` | Ровно одно поле | Неблиттируемые типы weaver не соберёт, лишние байты умножаются на send rate |

**Явные границы главы, которые полезно зафиксировать как границы, а не как недоработки.** В сцене нет камеры за игроком: `[Camera]` статичен, на нём только `Camera` + `EnableOnSingleRunner`, никакого follow-скрипта. И реестр `SetPlayerObject` **ни один учебный файл не читает локально** — он пишется и читается только на сервере. Канонический потребитель, который появится дальше, — `Runner.GetPlayerObject(Runner.LocalPlayer)`, чтобы клиент нашёл своего персонажа для камеры и UI. То есть «клиент сможет узнать, где мой персонаж» — это потенциал API, а не приём этой главы.

---

## Чеклист: как применить в своём проекте

1. **Создай один GameObject `[System]`** и положи на него `NetworkRunner`. Рядом, **на тот же GameObject**, положи `NetworkEvents` — иначе Fusion его не подхватит и все подписки молча умрут. Можно сделать это пунктом «Tools/Fusion/Scene/Setup Networking in the Scene».
2. **Напиши `sealed MonoBehaviour EntryPoint`** с `[SerializeField] NetworkRunner` и `[SerializeField] NetworkSceneManagerDefault`. Вызывай `StartGame` **в `Start()`**, не в `Awake()`.
3. **`await`-ь результат `StartGame`** и проверяй `StartGameResult.Ok` с логом ошибки. Это строчка, которой нет в учебном коде и которая экономит часы отладки «почему пусто».
4. **Добавь сцену в `EditorBuildSettings`** и передавай `Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)`, чтобы раннер взял уже открытую сцену, а не перезагрузил её.
5. **Подставь свой `AppIdFusion`** в `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` и не оставляй `SessionName` константой в проде — иначе ты делишь CCU и пространство сессий со всеми владельцами той же раздачи.
6. **Заведи по одному маленькому `sealed MonoBehaviour` на каждый колбэк**: join, leave, input. Подписывайся в `OnEnable`, отписывайся в `OnDisable`, строго симметрично. **Никогда не подписывайся в `Start()`** — `PlayerJoined` хоста прилетает сразу после `StartGame`, и ты начнёшь зависеть от порядка компонентов.
7. **Не используй persistent calls в инспекторе** для `NetworkEvents`. Только `AddListener`/`RemoveListener` из кода.
8. **Помни, что имена UnityEvent ≠ имена методов интерфейса** (`PlayerJoined` vs `OnPlayerJoined`, `OnSessionListUpdate` vs `OnSessionListUpdated`, `OnReliableData` vs `OnReliableDataReceived`). Ищи по списку из 19 полей, не по именам интерфейса.
9. **Закрой любой `Spawn`/`Despawn` гейтом `if (!runner.IsServer) return;`.** Колбэки приходят на все пиры. Проверяй роль в рантайме, никогда — константой сборки.
10. **Передавай `PlayerRef` четвёртым аргументом `Spawn`** для объектов, которыми игрок управляет. Без этого `GetInput` у владельца всегда `false` и ошибок не будет.
11. **Типизируй поле префаба как `NetworkObject` или `NetworkPrefabRef`**, не `GameObject` — чтобы ошибку поймал инспектор, а не рантайм.
12. **Как только появятся `[Networked]`-поля — инициализируй их через `onBeforeSpawned`** (пятый параметр `Spawn`), а не в `Spawned()`: иначе клиенты сначала увидят объект с нулевым состоянием.
13. **Храни связку «игрок ↔ объект» в `SetPlayerObject`**, не в своём словаре. Читай через **`TryGetPlayerObject`**, а не `GetPlayerObject`. После `Despawn` вызывай `SetPlayerObject(player, null)`, иначе запись повиснет на уничтоженный объект и переиспользованный `PlayerId` вернёт мусор.
14. **Объяви `struct X : INetworkInput`** только с блиттируемыми полями. Упаковывай направление в `byte`/короткий угол и кнопки в битовую маску — всё это умножается на send rate.
15. **Читай устройства ввода только в `OnInput`** и сразу делай `input.Set`. Используй **`GetAxisRaw`**, не `GetAxis` (сглаживание Unity зависит от FPS, а тик — нет), и **нормализуй вектор** (`Vector3.ClampMagnitude(dir, 1f)`), иначе диагональ будет быстрее в √2 раза.
16. **Для дискретных кнопок** накапливай состояние между вызовами `OnInput` и сбрасывай после `input.Set`; одиночный `GetKeyDown` внутри `OnInput` — баг.
17. **Подпишись на `OnInputMissing`** и подставляй там последний известный ввод (`input.Set(lastInput)`) — централизованно, в одном месте, вместо кэша в каждом `NetworkBehaviour`. Иначе джиттер даёт дрожание и постоянную коррекцию предсказания.
18. **Меняй состояние только в `FixedUpdateNetwork`.** Никогда не трогай сетевой `transform` в `Update`/`LateUpdate`.
19. **Масштабируй всё на `Runner.DeltaTime`.** `Time.deltaTime` и `Time.fixedDeltaTime` в сетевой логике запрещены: Fixed Timestep 0.02 ≠ 1/64.
20. **Оборачивай любой побочный эффект в `FixedUpdateNetwork` в `Runner.IsForward`** — логи, звуки, VFX, аналитику. Иначе при ресимуляции они отработают несколько раз на один тик.
21. **Делай накопительное состояние `[Networked]`.** `transform.position += ...` допустимо **только** потому, что позиция принадлежит `NetworkTransform`; своё поле в `MonoBehaviour` при ресимуляции накопит лишнее.
22. **Расщепляй сетевой контроллер и чистую механику**: `NetworkBehaviour` знает про тики и authority и делегирует в обычный `MonoBehaviour`, который умеет только «сдвинуться на шаг». Так механика переиспользуется в AI и тестах, а сетевой слой остаётся читаемым.
23. **Вынеси правила размещения в stateless-сервис** `GetSpawnPoint(PlayerRef)`. Не выбирай точку по `PlayerId == 1 ? a : b` — `PlayerId` переиспользуется, и при `PlayerCount > 2` все встанут в одну точку. Делай пул точек с выдачей по занятости.
24. **Повесь `NetworkTransform` на корень префаба** и выставь `SyncScale: 0`/`SyncParent: 0`, если scale и parent не меняются. Помни: он не просто реплицирует, а прячет разрыв между 64 Гц симуляции и 32 Гц отправки.
25. **Вынеси меш, материалы и коллайдеры в дочерний объект.** Сетевой корень — чистая точка на земле, чтобы художественные правки не ехали в сеть и не ломали интерполяцию.
26. **Прокидывай все зависимости `[SerializeField]`**, делай классы `sealed`, не заводи статиков и синглтонов для раннера. В игровом коде бери раннер из `NetworkBehaviour.Runner` или из аргумента колбэка.
27. **Если выносишь код в свой asmdef — добавь его в `AssembliesToWeave`.** Иначе `[Networked]` и `INetworkInput` тихо перестанут работать, и `InputDataWordCount: 0` перестанет выводиться автоматически.
28. **Для тестов нескольких игроков при `PeerMode: 0`** используй ParrelSync (второй процесс редактора) вместе с `AutoHostOrClient` и фиксированным `SessionName`. Переключишься на Multi-Peer — обязательно навесь `EnableOnSingleRunner` на камеру, свет и AudioListener, иначе получишь N копий каждого.
29. **Для демонстрации десинхрона включай `NetworkConditions`** в `NetworkProjectConfig` — заготовки задержки 150 мс и потерь 5 % уже лежат там выключенными.

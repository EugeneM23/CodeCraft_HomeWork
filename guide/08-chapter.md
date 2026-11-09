# Глава 08. Interest Management: кому из игроков какие объекты вообще доезжают

> **TL;DR для агента**
> - Включи `Simulation.ReplicationFeatures = 3` в `NetworkProjectConfig.fusion` — без этого весь код главы превращается в no-op, а объекты приходят всем и всегда.
> - Построй **три уровня фильтрации** от грубого к точному: `ObjectInterest` на префабе (геометрия) → `NetworkObject.SetPlayerAlwaysInterested` (объект целиком) → `NetworkBehaviour.ReplicateToAll(false)` + `ReplicateTo(player, true)` (отдельный бихевиор).
> - Форму интереса **переобъявляй каждый тик**: `ClearPlayerAreaOfInterest(player)` + `AddPlayerAreaOfInterest(player, pos, radius)` в `FixedUpdateNetwork` под `if (Runner.IsServer)`.
> - Всё, от чего зависит *вычисление* интереса (персонаж, задающий сферу, и player object), выводи из AoI навсегда через `ObjectInterest = Global`.
> - Сервисный слой (`SimulationBehaviour`) физически вешай **на объект раннера или его детей** — иначе он не зарегистрируется и его колбэки просто не вызовутся.
> - Помни: с включённым Interest Management `RpcTargets.All` больше не означает «гарантированно всем».

---

## Какую проблему решает

Трафик сервера растёт как `объекты × клиенты × тикрейт`. На 30 Hz и 200 объектах сервер каждую секунду 30 раз сериализует и рассылает состояние всего мира каждому из клиентов, хотя клиент видит на экране десяток объектов. Interest Management — это механизм, которым сервер решает **не отправлять** клиенту то, что ему сейчас не нужно.

Важно понять, что отсечение здесь не косметическое, не «не рисовать». Объект, не попавший в интерес игрока, физически не получает снапшоты: его `[Networked]`-состояние на этом клиенте не обновляется, а `ISimulationExit` (документированный в поставке, в этом разделе не используется) прямо говорит, что объект *перестаёт исполнять* `FixedUpdateNetwork()` и `Render()`, пока не вернётся в симуляцию. Это одновременно и экономия полосы, и анти-чит: чужой клиент не получает байты, которые нельзя «просто не показать в UI», потому что их у него нет.

Раздел 08 — отдельный самостоятельный проект (не продолжение линии 01-06), намеренно лишённый стрельбы и хитбоксов. Это «собиралка монет»: много дешёвого движущегося контента, который можно безопасно отсекать. Снаряды для такой демонстрации не годятся — их терять нельзя.

---

## Модель мира: кто чем владеет

| Что | Где исполняется | Почему |
|---|---|---|
| `SetAreaOfInterestGrid` / `SetAreaOfInterestCellSize` | **только Server/Host** | XML-док Fusion объявляет исключения `Can't change grid size in shared mode` и `Can't change cell size in shared mode` |
| `AddPlayerAreaOfInterest` / `ClearPlayerAreaOfInterest` | **только Server/Host** | док: *«Should only be called from the Host/Server in Server client mode. Should only be called for the local player in shared mode»*; для Clear — *«This can only be called from the server/host»* |
| `NetworkObject.SetPlayerAlwaysInterested` | **только StateAuthority** | док: *«Only the NetworkObject State Authority can set interest»* |
| `IInterestEnter` / `IInterestExit` | на каждом пире, но приходят **с `PlayerRef`** | на хосте известны события по всем игрокам, поэтому фильтр по `LocalPlayer` обязателен |
| `IsInterestedIn` | везде, но возвращает `bool?` | док: *«Returns null if interest cannot be determined (clients without State Authority are not aware of other client's Object Interest)»* |
| `ReplicateToAll` / `ReplicateTo` | в этом проекте вызываются **без гарда** в `Spawned` | см. раздел антипаттернов — это спорное место |

Ключевая смысловая деталь: **интерес адресуется игроку, а не объекту**. Поэтому форма интереса берётся с персонажа, но объявляется от имени `Object.InputAuthority` — то есть `PlayerRef` того, кто этим персонажем управляет.

Проект запускается в `GameMode.Single` (один пир, сам себе сервер). В этом режиме отсечения трафика не увидеть вообще: для демонстрации переключай на `GameMode.Host` и поднимай второй инстанс через ParrelSync.

---

## Архитектура раздела

```
                      [Runner]  (GameObject сцены Game.unity)
                      ├── NetworkRunner
                      ├── NetworkSceneManagerDefault
                      ├── NetworkEvents            (OnInput; OnObjectEnterAOI/ExitAOI — пустые)
                      ├── FusionStatistics
                      ├── RunnerAOIGizmos          (GizmoOptions = 3)
                      ├── AreaOfInterestIniitalizer (32 x 1 x 32, cell 4)
                      └── EntryPoint
                      │
   ДЕТИ РАННЕРА (это условие работоспособности, а не композиция для красоты):
                      ├── <Input>    InputAccumulator
                      ├── <Players>  PlayerInitializer + PlayerContextService
                      ├── <Camera>   CameraFollower
                      └── <Coins>    CoinSpawner (ОТКЛЮЧЁН) + 10 инстансов Coin

ПОТОК ИНТЕРЕСА (сервер):
  InterestShapeBehaviour (на Character, radius 10)
      │ каждый FixedUpdateNetwork, if IsServer
      ├─ ClearPlayerAreaOfInterest(Object.InputAuthority)
      └─ AddPlayerAreaOfInterest(Object.InputAuthority, transform.position, 10)
                      │
                      ▼
          сетка AoI 32x1x32, cellSize 4  =>  поле 128x128
                      │  сервер объединяет ячейки, накрытые сферой игрока
                      ▼
      объекты с ObjectInterest = AreaOfInterest (Coin.prefab = 0)
          ──► попал в ячейку? снапшот уходит клиенту
          ──► не попал?       снапшот НЕ уходит, на клиенте приходит IInterestExit

  Плюс два обхода геометрии:
      NetworkObject.SetPlayerAlwaysInterested(player, true)   — объект целиком
      NetworkBehaviour.ReplicateToAll(false)/ReplicateTo(p,1) — один бихевиор

ПОТОК ДАННЫХ (клиент -> сервер -> все):
  InputAccumulator (MonoBehaviour) --OnInput--> PlayerInput.[Networked] CurrentInput
      --> MoveController.FixedUpdateNetwork --> MovementComponent/RotationComponent
      --> transform --> NetworkTransform --> снапшот
  CameraFollower.Render() <-- Runner.GetPlayerObject(LocalPlayer).CharacterProvider
```

Слои:
1. **Загрузка.** `EntryPoint` — обычный `MonoBehaviour`, стартует раннер.
2. **Инициализация сетки.** `AreaOfInterestIniitalizer` — `SimulationBehaviour, ISpawned`.
3. **Форма интереса.** `InterestShapeBehaviour` — `NetworkBehaviour` на персонаже.
4. **Контекст игрока.** `PlayerContextService` спавнит `Player.prefab` и регистрирует его в `Runner.SetPlayerObject`.
5. **Контент.** `Coin` + `CoinPatrolComponent` + `CollisionReceiver`.
6. **Фильтрация.** `InterestComponent` (события интереса) и `MoneyStorage` (`ReplicateTo`).

---

## Реализация по шагам

### Шаг 1. Включить Interest Management в конфиге

Файл: `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`

```json
"Simulation": {
    "InputDataWordCount": 0,
    "ReplicationFeatures": 3,
    "InputTransferMode": 0,
    "SimulationUpdateTimeMode": 0,
    "PlayerCount": 2,
    "TickRateSelection": {
        "Client": 30,
        "ServerIndex": 0,
        "ClientSendIndex": 1,
        "ServerSendIndex": 1
    }
},
```

**Почему именно так.** В `Fusion.Runtime.xml` enum `NetworkProjectConfig.ReplicationFeatures` документирован тремя членами: `None`, `Scheduling` (*«сервер повышает приоритет объекта, не доехавшего из-за лимита байт на тик»*) и `SchedulingAndInterestManagement` (*«In addition to scheduling, Interest Management features are also enabled (Area Of Interest and Explicit Interest)»*). Значение `3` даёт и Scheduling, и Interest Management. Чему именно численно равен третий член — из поставки установить нельзя (значения живут в dll), поэтому не домысливай флаговую арифметику: ставь значение через UI (**Simulation → Replication Features**) и проверяй, что в .fusion записалось `3`.

**Что сломается иначе.** При `ReplicationFeatures = 1` вызовы `AddPlayerAreaOfInterest`, `SetPlayerAlwaysInterested` и колбэки `IInterestEnter`/`IInterestExit` — пустышки. Код компилируется, не падает, ничего не фильтрует. Это отладочная проверка №1 при симптоме «интерес не работает».

Полезный побочный эффект от того, что Scheduling тоже включён: когда объекты всё-таки отсекаются лимитом байт на тик, их приоритет на следующий тик растёт — culling не приводит к вечно забытым объектам.

### Шаг 2. Сервисный слой физически живёт под объектом раннера

Факт из `Assets/Game/Scenes/Game.unity`: у объектов `<Coins>`, `<Players>`, `<Camera>`, `<Input>` стоит `m_Father: {fileID: 1271488998}` — это Transform объекта `[Runner]`. А `AreaOfInterestIniitalizer`, `RunnerAOIGizmos`, `NetworkEvents`, `FusionStatistics`, `NetworkSceneManagerDefault` и `EntryPoint` висят прямо на `[Runner]` (8 компонентов).

**Почему именно так.** Это требуется дважды.
1. `Fusion.Runtime.xml`, `T:Fusion.SimulationBehaviour`: *«If a SimulationBehaviour is found on a NetworkRunner game object during the runner initialisation, the SimulationBehaviour is automatically registered»*. Без регистрации `CameraFollower.Render()`, `CoinSpawner.FixedUpdateNetwork()`, `PlayerContextService.PlayerJoined()` просто не вызовутся.
2. `Runner.GetBehaviour<T>()` — это, по доку `M:Fusion.Behaviour.GetBehaviour\`\`1`, буквально *«Wrapper for Unity's GameObject.GetComponentInChildren()»*. То есть поиск идёт по иерархии GameObject раннера.

**Что сломается иначе.** Вынеси `<Players>` из-под `[Runner]` — и раздел молча перестанет работать: игроки не заспавнятся, `Runner.GetBehaviour<PlayerContextService>()` вернёт `null`, а `Coin.ProcessCollision` упадёт с `NullReferenceException`. Альтернатива для объекта, который нельзя парентить, — `NetworkRunner.AddGlobal()` (упомянут в том же доке).

Контрпример в том же проекте: `[Spawn]` с `SpawnPointService` **не** ребёнок раннера — потому что это обычный `MonoBehaviour`, и `PlayerInitializer` достаёт его через `FindObjectOfType<SpawnPointService>()`, а не через `Runner.GetBehaviour`. Разные базовые классы — разные механизмы поиска; не путай их.

### Шаг 3. Пространственная сетка AoI

Файл: `Assets/Game/Scripts/System/AreaOfInterest/AreaOfInterestIniitalizer.cs`

```csharp
public sealed class AreaOfInterestIniitalizer : SimulationBehaviour, ISpawned
{
    [SerializeField]
    private int _sizeX = 32;

    [SerializeField]
    private int _sizeZ = 32;

    [Range(4, 32)]
    [SerializeField]
    private int _cellSize = 4;

    public void Spawned()
    {
        if (this.Runner.IsServer)
        {
            this.Runner.SetAreaOfInterestGrid(_sizeX, 1, _sizeZ);
            this.Runner.SetAreaOfInterestCellSize(_cellSize);
        }
    }
}
```

**Почему именно так.** Это не «радиус видимости», а внутренний пространственный хеш сервера: 32×1×32 ячеек по 4 юнита = поле 128×128. Сервер раскладывает объекты по ячейкам и для каждого игрока объединяет ячейки, перекрытые его AoI-шейпами. Стоимость рассылки становится `O(объекты в ячейках игрока)`, а не `O(все объекты)`.

`y = 1` означает один слой по вертикали — игра плоская. **Для игры с этажами это надо менять**, иначе игрок на первом этаже «интересуется» всем, что над ним.

`[Range(4, 32)]` на `_cellSize` фиксирует компромисс: мелкие ячейки = много ячеек на сферу радиуса 10 = дороже обсчёт; крупные = больше лишних объектов в выборке. Выноси в `SerializeField`, а не хардкодь (в прототипе `Assets/[Teacher]/AoIManager.cs` было захардкожено `SetAreaOfInterestGrid(32, 1, 32)` — рефакторинг именно в этом и состоял).

Гард `IsServer` — не косметика: док обещает **исключение** в Shared-режиме.

### Шаг 4. Форма интереса: Clear + Add каждый тик

Файл: `Assets/Game/Scripts/GameObject/Core/InterestShapeBehaviour.cs`

```csharp
public sealed class InterestShapeBehaviour : NetworkBehaviour
{
    [SerializeField]
    private float _radius = 10;

    public override void FixedUpdateNetwork()
    {
        if (this.Runner.IsServer)
        {
            this.Runner.ClearPlayerAreaOfInterest(this.Object.InputAuthority);
            this.Runner.AddPlayerAreaOfInterest(this.Object.InputAuthority, this.transform.position, _radius);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(this.transform.position, _radius);
    }
}
```

**Почему именно так.** `AddPlayerAreaOfInterest` — не подписка, а **однокадровая декларация**. Док прямо требует: *«Call this every FixedUpdateNetwork to add an area of interest for a player»*. Пара Clear+Add делает обновление формы идемпотентным: состояние нигде не хранится, сфера сама следует за движущимся персонажем, ресимуляция безопасна.

**Что сломается иначе.**
- Вызовешь `Add` один раз в `Spawned` — сфера останется в точке спавна, и клиент будет терять объекты вокруг себя.
- Уберёшь `Clear` — формы (вывод по смыслу API: Add однокадровый, снимать его нечем) начнут накапливаться по траектории игрока, и интерес перестанет что-либо отсекать.
- Уберёшь `if (Runner.IsServer)` — вызовешь серверное API на клиенте. Ровно так выглядит прототип `Assets/[Teacher]/InterestSphere.cs`: та же логика **без гарда**. Добавление гарда и есть разница между прототипом и боевой версией.

Адресация через `this.Object.InputAuthority` — потому что интерес принадлежит игроку, а не объекту.

### Шаг 5. ObjectInterest на префабах: что вообще подлежит отсечению

Файлы: `Assets/Game/Prefabs/Coin.prefab`, `Character.prefab`, `Player.prefab`

```yaml
# --- Coin.prefab, NetworkObject ---
  SortKey: 426224592
  ObjectInterest: 0
  Flags: 262145
  NestedObjects: []
  NetworkedBehaviours:
  - {fileID: 8752311890635330128}   # NetworkTransform (_autoAOIOverride: 1)
  - {fileID: 8964197543693425253}   # CollisionReceiver
  - {fileID: 468443206332540626}    # Coin
  - {fileID: 7133910748650768152}   # CoinPatrolComponent
  - {fileID: 578980440896631799}    # InterestComponent

# --- Character.prefab, NetworkObject ---
  SortKey: 2689712953
  ObjectInterest: 1
  NestedObjects: []
  NetworkedBehaviours:
  - {fileID: 454109258957820244}    # NetworkTransform (_autoAOIOverride: 0)
  - {fileID: 6052631099880656968}   # InterestShapeBehaviour (_radius: 10)
  - {fileID: 9185397998693264020}   # ColorComponent — сидит на ДОЧЕРНЕМ <Visual>

# --- Player.prefab, NetworkObject ---
  SortKey: 477462300
  ObjectInterest: 1
  NestedObjects:
  - {fileID: 1029340443581327872}   # NetworkObject вложенного Character
  NetworkedBehaviours:
  - {fileID: 8032379552497074581}   # PlayerInput
  - {fileID: 6377632269075262674}   # MoveController
  - {fileID: 4034088494716844788}   # MoneyStorage
  - {fileID: 6265595456888814051}   # PlayerNameConstraint
```

**Почему именно так.** `Coin` = 0, `Character`/`Player` = 1. Сопоставление `0 = AreaOfInterest`, `1 = Global` — это **вывод по поведению, а не факт из поставки**: имя `NetworkObject.ObjectInterestModes` встречается в исходниках ровно один раз (`Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs`, сравнение `objectInterest.intValue == (int)NetworkObject.ObjectInterestModes.AreaOfInterest`), а в `Fusion.Runtime.xml` этот тип не задокументирован вообще. Выставляй значение через инспектор, а не правкой YAML.

**Что сломается иначе.** Если сделать `Character` жертвой AoI — получится курица-яйцо: персонаж, который *задаёт* зону интереса, сам может из неё выпасть, клиент потеряет позицию своего героя, зона перестанет обновляться. **Правило: всё, от чего зависит вычисление интереса, плюс сам player object — выводи из AoI навсегда.**

Три неочевидные детали YAML, которые стоит знать.

1. **`NetworkBehaviour` на дочернем GameObject принадлежит ближайшему родительскому `NetworkObject`.** `ColorComponent` (fileID `9185397998693264020`) физически висит на объекте `<Visual>` внутри `Character.prefab` (его `m_GameObject` указывает на объект с `m_Name: <Visual>`), но числится в `NetworkedBehaviours` корневого `NetworkObject`. Это нормальный способ держать визуальный бихевиор рядом с рендерером.

2. **`_autoAOIOverride` — это дефолт, а не настройка.** Док `P:Fusion.NetworkTransform.AutoUpdateAreaOfInterestOverride`: *«Default is true... AOI works in world space, and NetworkTransform operates in local space, so any AOI position values of nested Objects will ALWAYS be invalid, so nested Objects should always have their AOI Override set to a non-nested Object»*. Значит `1` у `Coin` — просто дефолт, и применять его не к чему (`Coin` ни во что не вложен, `NestedObjects: []`). Единственное осмысленное отклонение здесь — `0` у `Character`, который вложен в `Player`; это безвредно только потому, что оба объекта Global. **Для nested-объекта, подлежащего AoI, отключение override было бы грубой ошибкой.**

3. **Порядок компонентов в `NetworkedBehaviours` значим.** `InterestComponent` стоит последним, поэтому `Coin.Spawned` (третий) успевает подписаться на `OnInterestChanged` до того, как `InterestComponent.Spawned` поднимет начальное событие. Переставишь в инспекторе — событие улетит в пустоту. (Здесь это скомпенсировано прямым вызовом `SetActive(IsInterested())`, но закладываться на порядок нельзя.)

Обрати внимание и на то, чего в списках **нет**: `MovementComponent` на `Coin.prefab` и `CharacterProvider` на `Player.prefab` не входят в `NetworkedBehaviours`. Это не ошибка — см. шаг 9.

### Шаг 6. Интерес как событие домена

Файл: `Assets/Game/Scripts/GameObject/Core/InterestComponent.cs`

```csharp
public sealed class InterestComponent : NetworkBehaviour, IInterestEnter, IInterestExit
{
    public event Action<bool> OnInterestChanged;
    
    [Button]
    public void DebugChangeInterest(int playerId, bool interested)
    {
        PlayerRef player = PlayerRef.FromIndex(playerId);
        this.SetInterested(player, interested);
    }

    public bool IsInterested()
    {
        return this.Runner.IsInterestedIn(this.Object, this.Runner.LocalPlayer).GetValueOrDefault();
    }

    public void SetInterested(PlayerRef player, bool interested)
    {
        if (this.Runner.IsServer) 
            this.Object.SetPlayerAlwaysInterested(player, interested);
    }

    public override void Spawned()
    {
        if (this.Runner.IsServer)
            return;

        if (this.IsInterested()) 
            this.OnInterestChanged?.Invoke(true);
    }

    void IInterestEnter.InterestEnter(PlayerRef player)
    {
        if (player == this.Runner.LocalPlayer) 
            this.OnInterestChanged?.Invoke(true);
    }

    void IInterestExit.InterestExit(PlayerRef player)
    {
        if (player == this.Runner.LocalPlayer) 
            this.OnInterestChanged?.Invoke(false);
    }
}
```

**Почему именно так.** Пять отдельных решений в двадцати строках.

1. **`IsInterestedIn` возвращает `bool?`, а не `bool`.** Док: *«Returns null if interest cannot be determined»*. `.Value` дал бы `NullReferenceException` на клиенте; `.GetValueOrDefault()` сознательно читает «не знаю» как «не интересен» — безопасная трактовка для вьюшки.
2. **`SetPlayerAlwaysInterested` — второй, НЕгеометрический канал интереса.** «Отправляй этот объект этому игроку всегда, где бы он ни был». Нужен для квестовых маркеров, босса, командного HUD-объекта. Закрыт гардом по требованию дока *«Only the NetworkObject State Authority can set interest»*.
3. **Фильтр `player == Runner.LocalPlayer` обязателен.** Колбэк приходит с `PlayerRef`, а не как «моё/не моё», и на хосте известны события по всем игрокам. Без фильтра вьюшка мигает от чужого интереса. Этот фильтр стоит в трёх реализациях проекта: боевой `InterestComponent`, `[Teacher]/InterestComponentA`, `[Teacher]/InterestReceiver`. (В `[Teacher]/RpcTest.cs` методы `InterestEnter`/`InterestExit` фильтра не имеют — но там это и не колбэки интерфейса, а обычные методы, которые дёргаются вручную из закомментированного `Spawned`.)
4. **`Spawned` доопределяет случай «объект приехал уже интересным».** `InterestEnter` для него может не прийти вовсе — без этой строки свежеполученные монеты остались бы невидимыми.
5. **Единственный класс, который знает про Fusion-интерес.** Наружу — `event Action<bool>` и `IsInterested()`. Сравни с `[Teacher]/InterestComponentA.cs`, где в одном классе и подписка, и `[SerializeField] _visual`, и `Debug.Log` — рефакторинг разрезал это по шву «событие vs презентация», а `[Teacher]/InterestReceiver.cs` сохранил промежуточный шаг «только события».

Ещё один шаг рефакторинга, видимый только при сравнении: прототип `InterestComponentA` использовал **рунерный** overload `Runner.SetPlayerAlwaysInterested(player, this.Object, interested)` (он задокументирован: `M:Fusion.NetworkRunner.SetPlayerAlwaysInterested(PlayerRef, NetworkObject, Boolean)` — *«Flags this player as always interested in this object»*), а боевой код перешёл на **объектный** `this.Object.SetPlayerAlwaysInterested(player, interested)`. Предпочитай объектный: меньше параметров, меньше шансов перепутать объект.

### Шаг 7. Потребитель события ничего не знает про Fusion

Файл: `Assets/Game/Scripts/GameObject/Content/Coin.cs`

```csharp
public override void Spawned()
{
    _collisionReceiver = this.Object.GetBehaviour<CollisionReceiver>();
    _interestComponent = this.Object.GetBehaviour<InterestComponent>();
    _interestComponent.OnInterestChanged += _visual.SetActive;
    _visual.SetActive(_interestComponent.IsInterested());
}

public override void Despawned(NetworkRunner runner, bool hasState)
{
    _interestComponent.OnInterestChanged -= _visual.SetActive;
}

public override void FixedUpdateNetwork()
{
    int count = _collisionReceiver.GetColliders(out IReadOnlyList<Collider> buffer);
    for (int i = 0; i < count; i++)
    {
        Collider collider = buffer[i];
        if (this.ProcessCollision(collider))
        {
            this.Runner.Despawn(this.Object);
            return;
        }
    }
}

private bool ProcessCollision(Collider collider)
{
    if (!collider.CompareTag(GameObjectTags.Character))
        return false;

    NetworkObject character = collider.GetComponent<NetworkObject>();
    PlayerContextService playerService = this.Runner.GetBehaviour<PlayerContextService>();
    NetworkObject playerContext = playerService.GetContext(character.InputAuthority);
    
    MoneyStorage moneyStorage = playerContext.GetBehaviour<MoneyStorage>();
    moneyStorage.EarnMoney(_moneyIncome);
    return true;
}
```

**Почему именно так.** `Coin` лишь подписывает `_visual.SetActive` на событие — контент-код про Fusion-интерес не знает вообще. Отписка в `Despawned` обязательна: при пулинге/переиспользовании `NetworkObject` на событие окажутся подписаны несколько вьюшек.

Строка `_visual.SetActive(_interestComponent.IsInterested())` — **не дублирование**, как может показаться. На сервере/хосте `InterestComponent` вообще никогда не поднимает `OnInterestChanged`: `Spawned` делает ранний `return` при `IsServer`, а колбэки фильтруются по `LocalPlayer`. Значит на хост-пире эта строка — **единственный** путь, которым вьюшка получает начальное состояние. (Вывод, но следует прямо из кода.)

Второй сюжет — путь «коллайдер → игрок → его деньги»: по тегу находится `NetworkObject` персонажа, у него берётся `InputAuthority`, по `PlayerRef` через `GetContext` достаётся объект-контекст и уже на нём `MoneyStorage`. То есть деньги лежат не на персонаже, а на долгоживущем контексте.

Отдельно отметь разделение «интерес управляет презентацией, но не правилами»: `Coin._visual` — дочерний `<Visual>` (fileID `8774382049572745653`), а `SphereCollider` и `CollisionReceiver._center` — **корень** `Coin` (fileID `5264891449863896200`). Выключение вьюшки по интересу не выключает ни коллайдер, ни `OverlapSphere`: «невидимая» монета остаётся собираемой, и игровая логика не расходится с картинкой. Если бы `_visual` был корнем — интерес начал бы менять правила игры.

### Шаг 8. Пер-бихевиорная фильтрация: объект всем, состояние — одному

Файл: `Assets/Game/Scripts/GameObject/Core/MoneyStorage.cs`

```csharp
public sealed class MoneyStorage : NetworkBehaviour
{
    [Networked]
    public int Money { get; private set; }

    public override void Spawned()
    {
        this.ReplicateToAll(false);
        this.ReplicateTo(this.Object.InputAuthority, true);
    }

    [Button]
    public void DebugReplicateTo(int playerId, bool interested)
    {
        this.ReplicateTo(PlayerRef.FromIndex(playerId), interested);
    }

    public void EarnMoney(int amount)
    {
        this.Money += amount;
    }
}
```

**Почему именно так.** Третий, самый тонкий уровень: `Player.prefab` имеет `ObjectInterest = 1` и доезжает до всех, но состояние `Money` уходит **только владельцу**. Это и экономия трафика, и анти-чит: чужой клиент физически не получает байты с балансом противника, вместо того чтобы «просто не показывать их в UI».

**Порядок вызовов неслучаен.** Док `ReplicateToAll`: *«this by default is true so calling ReplicateToAll(true) does nothing if ReplicateToAll(false) hasn't been called before»*. Значит сначала «выключить всем», потом «включить одному». Обратный порядок молча не даст эффекта.

Альтернативы, которые стоит знать: переопределяемый `virtual bool ReplicateTo(PlayerRef)` (*«default implementation always returns true»*) — для правила, вычисляемого на лету; и рунерные эквиваленты `Runner.SetBehaviourReplicateToAll(behaviour, bool)` / `Runner.SetBehaviourReplicateTo(behaviour, player, bool)` — когда решение принимает внешняя система, а не сам бихевиор.

`[Button]` из Odin — не украшение: AoI и репликацию нельзя отладить юнит-тестами, нужен способ дёрнуть конкретный сценарий в редакторе на конкретном пире. `PlayerRef.FromIndex(int)` — мост от инспектора к `PlayerRef`.

### Шаг 9. Контекст игрока отдельно от персонажа

Файл: `Assets/Game/Scripts/System/Players/PlayerContextManager.cs` (класс называется `PlayerContextService` — имя файла и класса разошлись)

```csharp
public sealed class PlayerContextService : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    public event Action<PlayerRef, NetworkObject> ProcessPlayerJoin;
    public event Action<PlayerRef, NetworkObject> ProcessPlayerLeft;
    
    [SerializeField]
    private NetworkPrefabRef _playerPrefab;
    
    public int PlayerCount { get; private set; }

    public NetworkObject LocalContext
    {
        get { return this.Runner.GetPlayerObject(this.Runner.LocalPlayer); }
    }

    public NetworkObject GetContext(PlayerRef player)
    {
        return this.Runner.GetPlayerObject(player);
    }

    void IPlayerJoined.PlayerJoined(PlayerRef player)
    {
        this.PlayerCount++;
        
        if (!this.Runner.IsServer)
            return;

        NetworkObject context = this.Runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity, player);
        this.ProcessPlayerJoin?.Invoke(player, context);
        this.Runner.SetPlayerObject(player, context);
    }

    void IPlayerLeft.PlayerLeft(PlayerRef player)
    {
        this.PlayerCount--;

        if (!this.Runner.IsServer)
            return;
        
        NetworkObject context = this.Runner.GetPlayerObject(player);
        this.ProcessPlayerLeft?.Invoke(player, context);
        this.Runner.SetPlayerObject(player, null);
        this.Runner.Despawn(context);
    }
}
```

**Почему именно так.** Сетевой «игрок» здесь — **не персонаж**, а отдельный объект-контекст (`Player.prefab`: `PlayerInput` + `MoveController` + `MoneyStorage` + `PlayerNameConstraint` в `NetworkedBehaviours`, плюс `CharacterProvider`, плюс nested `Character` со своим `NetworkObject`). Персонаж становится сменяемой «аватаркой», а деньги/ввод живут на долгоживущем объекте, переживающем смерть и респаун. Для темы главы это ещё и объект, который гарантированно доезжает до всех пиров — на нём и демонстрируется фильтрация уровнем ниже.

`PlayerCount++` стоит **вне** гарда (счётчик нужен всем пирам), спавн — только на сервере.

В `PlayerLeft` порядок правильный: сначала `SetPlayerObject(player, null)`, потом `Despawn` — иначе кто-то успеет получить ссылку на уже убитый объект.

⚠️ **А вот в `PlayerJoined` порядок дефектный.** `Spawn` → событие → `SetPlayerObject`. В зазоре между `Spawn` и `SetPlayerObject` персонаж уже физически в мире (`PlayerInitializer` в обработчике события как раз ставит его на точку спавна), но `Runner.GetPlayerObject(player)` ещё `null`. Если в этом же тике `CollisionReceiver` монеты зацепит коллайдер персонажа, `Coin.ProcessCollision` сделает `playerContext.GetBehaviour<MoneyStorage>()` на `null` → `NullReferenceException`. **Рекомендация: `SetPlayerObject` сразу после `Spawn`, событие — последним.**

Отдельная тонкость — `CharacterProvider`:

Файл: `Assets/Game/Scripts/Player/CharacterProvider.cs`

```csharp
using UnityEngine;
using Behaviour = Fusion.Behaviour;

namespace SampleGame
{
    public sealed class CharacterProvider : Behaviour
    {
        [SerializeField]
        private GameObject _character;

        public GameObject Character => _character;
    }
}
```

Это `Fusion.Behaviour`, а **не** `MonoBehaviour` и не `NetworkBehaviour` — и в `NetworkedBehaviours` префаба его нет. Причина принципиальная: `Behaviour.GetBehaviour<T>` ограничен `T : Fusion.Behaviour`, поэтому провайдер обязан наследовать `Fusion.Behaviour`, чтобы быть доступным через `Object.GetBehaviour<CharacterProvider>()`, и при этом не тащит никакого сетевого состояния. **Используй `Fusion.Behaviour` для «сетевых по доступу, но не по состоянию» компонентов.**

Контраст по null-гардам. `CameraFollower` реализует `ISpawned` явно (`void ISpawned.Spawned()`), а `Render()` переопределяет как виртуальный метод `SimulationBehaviour`, и в нём есть защита:

Файл: `Assets/Game/Scripts/System/Camera/CameraFollower.cs`

```csharp
public override void Render()
{
    NetworkObject playerContext = _playerService.LocalContext;
    if (!playerContext)
        return;

    CharacterProvider characterProvider = playerContext.GetBehaviour<CharacterProvider>();
    GameObject character = characterProvider.Character;
    _camera.transform.position = character.transform.position + _offset;
}
```

Камера — локальная, несетевая вещь, и правильно сидит в `Render()`, а не в `FixedUpdateNetwork()`. А `if (!playerContext) return;` страхует первые кадры, когда контекст ещё не зарегистрирован — ровно то, чего не хватает в `Coin.ProcessCollision`.

### Шаг 10. Один физический запрос на тик

Файл: `Assets/Game/Scripts/GameObject/Core/CollisionReceiver.cs`

```csharp
public sealed class CollisionReceiver : NetworkBehaviour, IBeforeTick
{
    [SerializeField]
    private Transform _center;

    [SerializeField]
    private Vector3 _offset;

    [SerializeField]
    private float _radius = 1;

    [SerializeField]
    private LayerMask _layerMask;

    [SerializeField]
    private QueryTriggerInteraction _triggerInteraction;

    [SerializeField]
    private int _bufferCapacity = 8;

    private Collider[] _buffer = new Collider[8];
    private int _bufferCount;

    private void Awake()
    {
        _buffer = new Collider[_bufferCapacity];
    }

    public int GetColliders(out IReadOnlyList<Collider> buffer)
    {
        buffer = _buffer;
        return _bufferCount;
    }

    void IBeforeTick.BeforeTick()
    {
        PhysicsScene physicsScene = this.Runner.GetPhysicsScene();
        _bufferCount = physicsScene.OverlapSphere(
            _center.position + _offset,
            _radius,
            _buffer,
            _layerMask,
            _triggerInteraction
        );
    }

    private void OnDrawGizmos()
    {
        if (_center)
            Gizmos.DrawWireSphere(_center.position + _offset, _radius);
    }
}
```

**Почему именно так.** Три сетевые проблемы решены разом.
1. `Runner.GetPhysicsScene()` вместо `Physics.OverlapSphere` — при нескольких раннерах в одном процессе глобальный запрос собрал бы коллайдеры чужого пира.
2. `IBeforeTick` — один запрос за тик **до** симуляции. Все потребители видят один снимок физики; разные запросы в разные моменты сломали бы детерминизм ресимуляции. Поэтому в `BeforeTick` нельзя класть побочные эффекты (звук, спавн): он вызывается и при ресимуляции, логика обязана быть чистой функцией от состояния тика.
3. Предвыделенный `Collider[_bufferCapacity]` и возврат пары `(int count, IReadOnlyList<Collider>)` вместо нового массива — ноль аллокаций в тиковом цикле. GC-спайк в `FixedUpdateNetwork` — это пропущенные тики.

---

## Fusion API этой главы

| API | Что делает | Где вызывается | Подводный камень |
|---|---|---|---|
| `NetworkRunner.SetAreaOfInterestGrid(x,y,z)` | задаёт размерность сетки AoI | `AreaOfInterestIniitalizer.Spawned` | док: исключение *«Can't change grid size in shared mode»*; `y=1` игнорирует вертикаль |
| `NetworkRunner.SetAreaOfInterestCellSize(int)` | размер ячейки | там же | отдельное исключение *«Can't change cell size in shared mode»*; мелкие ячейки дороже, крупные менее точны |
| `NetworkRunner.AddPlayerAreaOfInterest(player,pos,r)` | объявляет сферу интереса на **текущий тик** | `InterestShapeBehaviour.FixedUpdateNetwork` | *«Call this every FixedUpdateNetwork»*; Server/Host only, в Shared — только для локального игрока |
| `NetworkRunner.ClearPlayerAreaOfInterest(player)` | сбрасывает все шейпы игрока | там же, перед Add | *«This can only be called from the server/host»*; без Clear формы накапливаются |
| `NetworkObject.SetPlayerAlwaysInterested(player,bool)` | явный, негеометрический интерес | `InterestComponent.SetInterested` | *«Only the NetworkObject State Authority can set interest»* |
| `NetworkRunner.SetPlayerAlwaysInterested(player,obj,bool)` | тот же смысл, рунерный overload | `[Teacher]/InterestComponentA` | предпочитай объектный вариант |
| `NetworkRunner.IsInterestedIn(obj,player)` | опрос состояния интереса | `InterestComponent.IsInterested` | возвращает **`bool?`**; `null` = «определить нельзя» → `.GetValueOrDefault()`, никогда `.Value` |
| `IInterestEnter` / `IInterestExit` | колбэки входа/выхода из AoI игрока | `InterestComponent` | приходят с `PlayerRef` → обязателен фильтр `== Runner.LocalPlayer`; `InterestEnter` может не прийти для уже интересного объекта |
| `ISimulationEnter` / `ISimulationExit` | объект начал/перестал симулироваться у меня | ⚠️ в проекте не реализовано | док: объект *перестаёт исполнять* `FixedUpdateNetwork()`/`Render()`. Для «объект выпал — выключи поведение» это правильный инструмент, а не `IInterestExit` |
| `NetworkBehaviour.ReplicateToAll(bool)` | дефолт репликации бихевиора | `MoneyStorage.Spawned` | *«by default is true... `ReplicateToAll(true)` does nothing if `ReplicateToAll(false)` hasn't been called before»* |
| `NetworkBehaviour.ReplicateTo(player,bool)` | репликация одному игроку | `MoneyStorage.Spawned` | только **после** `ReplicateToAll(false)` |
| `NetworkBehaviour.ReplicateTo(player)` (virtual) | правило репликации на лету | ⚠️ в проекте не реализовано | *«default implementation always returns true»* |
| `NetworkRunner.SetBehaviourReplicateTo(...)` / `...ToAll(...)` | рунерные эквиваленты | ⚠️ в проекте не реализовано | нужны, когда решает внешняя система |
| `NetworkObject.ObjectInterest` (поле префаба) | режим интереса per-prefab | `Coin.prefab = 0`, `Character/Player = 1` | численные значения режимов из поставки **не** устанавливаются — ставь через инспектор |
| `NetworkTransform.AutoUpdateAreaOfInterestOverride` / `SetAreaOfInterestOverride(obj)` | AoI-позиция для nested-объектов | `_autoAOIOverride` в YAML | *«Default is true»*; у nested-объектов AoI-позиция в world space **всегда** невалидна |
| `NetworkRunner.GetObjectsInAreaOfInterestForPlayer(player)` | список NetworkObjectId в AoI игрока | ⚠️ в проекте не реализовано | *«Server only»*. Главный инструмент отладки «почему объект не доехал» кодом, а не глазами |
| `NetworkRunner.GetAreaOfInterestGizmoData(list)` | данные ячеек для гизмо | внутри `RunnerAOIGizmos` | — |
| `RpcSendMessageResult.NotSentTargetObjectNotInPlayerInterest` | диагностика отброшенного RPC | — | *«Target object not in client's interest... needs to be explicitly set as always interested»*. Рядом `NotSentBroadcastNoConfirmedNorInterestedClients` |
| `NetworkRunner.SetPlayerObject` / `GetPlayerObject` | реестр `PlayerRef → объект-контекст` | `PlayerContextService` | `Set` только на сервере; читается везде, но может быть `null` в первых кадрах |
| `Behaviour.GetBehaviour<T>()` | поиск компонента | `Coin`, `MoveController`, `CameraFollower` | док: *«Wrapper for Unity's GameObject.GetComponentInChildren()»* — см. антипаттерны |
| `IBeforeTick.BeforeTick()` | фаза до симуляции тика | `CollisionReceiver`, `PlayerInput` | вызывается и при ресимуляции → никаких побочных эффектов |
| `NetworkRunner.GetPhysicsScene()` | физика сцены раннера | `CollisionReceiver` | `Physics.OverlapSphere` собрал бы коллайдеры чужого пира |
| `NetworkEvents.OnInput` | `UnityEvent` вместо `INetworkRunnerCallbacks` | `InputAccumulator` | позволяет держать сбор ввода в чистом `MonoBehaviour`; минус — `FindObjectOfType<NetworkEvents>()` в `Awake` делает порядок инициализации сцены неявной зависимостью |
| `RunnerAOIGizmos` | визуализация ячеек и зон | компонент на `[Runner]` | в исходнике: гизмо *«NOT applicable to Shared Mode and will only render for the Server/Host peer»*, и требуют включённого Interest Management |

---

## Настройки, без которых не работает

1. **`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`**
   - `Simulation.ReplicationFeatures = 3` — тумблер №1.
   - `PeerMode: 0` (single peer; второй инстанс — через ParrelSync).
   - `AssembliesToWeave` содержит `Assembly-CSharp` — иначе `[Networked]` в `Assets/Game/Scripts` не вплетётся.
   - `Simulation.PlayerCount: 2`, но `EntryPoint` передаёт `PlayerCount = 4` в `StartGameArgs` (вывод: аргумент перекрывает конфиг).
   - `LagCompensation.Enabled: true` — наследие раздела 06, в коде 08 не используется: ни `Hitbox`, ни `HitboxRoot` здесь нет, коллизии делаются обычным `PhysicsScene.OverlapSphere`.
2. **`Assets/Game/Scenes/Game.unity`**
   - На `[Runner]`: `NetworkRunner`, `NetworkSceneManagerDefault` (`IsSceneTakeOverEnabled: 1`), `NetworkEvents`, `FusionStatistics` (`_statsEnabled: 0`), `RunnerAOIGizmos` (`GizmoOptions: 3`), `AreaOfInterestIniitalizer` (`32/32/4`), `EntryPoint`.
   - `<Input>`, `<Players>`, `<Camera>`, `<Coins>` — **дети** `[Runner]` (`m_Father: 1271488998`).
   - `<Players>`: `PlayerInitializer` + `PlayerContextService` (`_playerPrefab → Player.prefab`).
   - `<Coins>`: `CoinSpawner` с `m_Enabled: 0` + 10 заранее расставленных инстансов `Coin`.
   - `[Spawn]` (ребёнок `[Points]`, **не** раннера): `SpawnPointService` с 4 точками `<Point>` при `PlayerCount = 4`. Это мешок без повторов: `if (_freePoints.Count == 0) _freePoints.AddRange(_spawnPoints);` затем `Random.Range` + `RemoveAt` — на каждого игрока по точке, повторов до исчерпания нет.
   - `GizmoOptions: 3` = `ShowActiveServerZones | ShowPlayerInterest` — это **факт из исходника** `RunnerAOIGizmos.cs` (`ShowActiveServerZones = 1`, `ShowPlayerInterest = 2`, инициализатор `GizmoOptions = ShowActiveServerZones | ShowPlayerInterest`). И значение, и цвета совпадают с дефолтами компонента, то есть их никто не настраивал — не перечисляй их как «настройки раздела».
   - `NetworkEvents` имеет хуки `OnObjectEnterAOI` / `OnObjectExitAOI` — доказательство, что AoI-события есть и на уровне раннера. В этой сцене все 19 `UnityEvent` пустые (`m_Calls: []`), включая `OnInput`, на который `InputAccumulator` подписывается кодом.
3. **Префабы**
   - `Coin.prefab`: `ObjectInterest: 0`; `CollisionReceiver` (`_center` = корень, `_offset (0,1,0)`, `_radius 0.5`, `_layerMask` = Default, `_triggerInteraction: 1`, `_bufferCapacity 8`); `Coin` (`_moneyIncome 1`, `_visual → <Visual>`); `MovementComponent` (`_speed 5`, **не** в `NetworkedBehaviours`); `CoinPatrolComponent` (`_stoppingDistance 0.25`); `InterestComponent` **последним** в `NetworkedBehaviours`; `SphereCollider` на корне, `m_IsTrigger: 0`.
   - `Character.prefab`: `ObjectInterest: 1`; тег `Character` (по нему `Coin` ищет персонажа); `InterestShapeBehaviour` (`_radius 10`); `RotationComponent` (`_speedRate 2000`), `MovementComponent` (`_speed 5`) — обычные `MonoBehaviour`; `ColorComponent` — `NetworkBehaviour` на дочернем `<Visual>`.
   - `Player.prefab`: `ObjectInterest: 1`, `NestedObjects` = nested `Character`, `CharacterProvider` вне `NetworkedBehaviours`.
4. **ProjectSettings**
   - `ProjectVersion: 2022.3.42f1`; теги `Character`, `Bullet`, `Coin`; `DynamicsManager.m_SimulationMode: 0` (FixedUpdate) — физика нужна только для `OverlapSphere`, `Rigidbody` в учебных префабах нет.
   - `EditorBuildSettings`: `Game.unity` (индекс 0) и `SceneContext.unity` (тоже enabled). ⚠️ `EntryPoint` берёт `SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)` — запуск из `SceneContext.unity` отдал бы раннеру не ту сцену.
   - Зависимости: Odin Inspector (`[Button]`), ParrelSync (второй инстанс).

---

## Антипаттерны и как их обходит этот код

**1. «Поставил зону интереса один раз».** Обходится через Clear+Add каждый тик (шаг 4). Это идемпотентное переобъявление, а не расточительство.

**2. «Логика интереса размазана по контенту».** Обходится выделением `InterestComponent` как единственного знатока Fusion-интереса. Антипаттерн сохранён в репозитории как `[Teacher]/InterestComponentA.cs`: интерес + вьюшка + логи в одном классе.

**3. «`bool?` привели к `true`».** Обходится `.GetValueOrDefault()`: «не знаю» = «не интересен».

**4. «Колбэк интереса считают своим».** Обходится фильтром `player == Runner.LocalPlayer`.

**5. «Серверное API зовётся на клиенте».** Обходится гардами `if (Runner.IsServer)` в трёх местах. Антипаттерн сохранён как `[Teacher]/InterestSphere.cs` — та же логика без гарда.

**6. «Физика опрашивается из каждого потребителя».** Обходится одним `OverlapSphere` в `IBeforeTick` с переиспользуемым буфером.

**7. «`RpcTargets.All` значит всем».** Включив Interest Management, на это рассчитывать нельзя: в поставке есть документированные `RpcSendMessageResult.NotSentTargetObjectNotInPlayerInterest` (*«Target object not in client's interest. Likely due to being outside of player's AOI region, or needs to be explicitly set as always interested»*) и `NotSentBroadcastNoConfirmedNorInterestedClients` (*«Server attempted to send to all the clients, but the target object is not confirmed/not in Object Interest for all target clients»*). Это факт из поставки, а не догадка. Посылай RPC через объект, который точно доезжает (здесь таким служит `Player`-контекст), либо делай объект Global/AlwaysInterested. Стенд ровно под этот вопрос — `[Teacher]/RpcTest.cs` (целиком закомментирован): `SetReplicateAll`, `SetInterested` и `[Rpc(RpcSources.StateAuthority, RpcTargets.All)] RpcTestAAA()`.

### Дефекты самого раздела — не копируй их

**8. ⚠️ `UnityEngine.Random` внутри `FixedUpdateNetwork`.** Файл `CoinPatrolComponent.cs`:

```csharp
public override void FixedUpdateNetwork()
{
    Vector3 distance = _waypoints[_currentIndex] - this.transform.position;
    if (distance.sqrMagnitude <= _stoppingDistance * _stoppingDistance)
        _currentIndex = Random.Range(0, _waypoints.Length);  //(_currentIndex + 1) % _waypoints.Count;
    else
        _movementComponent.MoveStep(distance.normalized, this.Runner.DeltaTime);
}
```

Недетерминированный источник в тиковой симуляции: при ресимуляции клиент получит другую цель, чем сервер. Плюс `_currentIndex` и `_waypoints` — не `[Networked]`, при ресимуляции и host migration не восстановятся. Закомментированный последовательный вариант `(_currentIndex + 1) % _waypoints.Count` детерминирован — **используй его**. Сама идея («заставь объекты интереса двигаться, иначе вход/выход из AoI будет тестироваться только движением игрока, и половина сценариев не проверится») правильная; реализация — нет. И из-за `Random` демо нельзя называть полностью детерминированным, даже при выключенном `CoinSpawner`.

**9. ⚠️ `GetBehaviour<T>` — это `GetComponentInChildren`.** Док `M:Fusion.Behaviour.GetBehaviour\`\`1`: *«Wrapper for Unity's GameObject.GetComponentInChildren()»*. Он **не** уважает границы вложенных `NetworkObject`. На `Player.prefab` (где `Character` вложен как nested `NetworkObject`) вызов `playerContext.GetBehaviour<InterestShapeBehaviour>()` вернёт компонент **вложенного** `Character`, а не сообщит об ошибке. В разделе это безвредно только потому, что все запрашиваемые типы (`MoneyStorage`, `CharacterProvider`, `PlayerInput`) уникальны в иерархии. Вторая мина: `GetComponentInChildren` по умолчанию **пропускает выключенные объекты** — для `Coin`, у которого `<Visual>` выключается по интересу, компонент на `<Visual>` не нашёлся бы. **Правило: кешируй в `Spawned` и не держи дубликатов типа в nested-иерархии.**

**10. ⚠️ `MoveController`: нет гарда авторитета и `GetComponent` в тиковом цикле.**

```csharp
public override void FixedUpdateNetwork()
{
    Vector3 direction = _input.CurrentInput.moveDirection;
    float deltaTime = this.Runner.DeltaTime;

    GameObject character = _characterProvider.Character;
    character.GetComponent<MovementComponent>().MoveStep(direction, deltaTime);
    character.GetComponent<RotationComponent>().RotateStep(direction, deltaTime);
}
```

Два `GetComponent` каждый тик, хотя в том же `Spawned` `PlayerInput` и `CharacterProvider` аккуратно кешируются. Прямое противоречие приёму «ноль накладных расходов в тике» из `CollisionReceiver`. **Кешируй их в `Spawned`.**

Зато отсутствие гарда авторитета здесь работает по другой причине, и её надо понять. `PlayerInput`:

```csharp
void IBeforeTick.BeforeTick()
{
    this.PreviousInput = this.CurrentInput;
    this.CurrentInput = this.GetInput(out InputData input) ? input : default;
}
```

Ввод лежит в `[Networked] CurrentInput` **намеренно**: `NetworkBehaviour.GetInput` вернёт `false` там, где у пира нет ввода этого игрока, то есть на клиенте для чужих игроков `CurrentInput` обнулился бы. Но сервер пишет настоящий ввод в сетевое состояние, снапшот разносит его всем, и `MoveController` на любом пире (и в ресимуляции) читает один и тот же ввод вместо того, чтобы спрашивать `Runner.GetInputForPlayer`, который доступен не всем. Плюс пара `PreviousInput`/`CurrentInput` даёт готовую основу для детекции «кнопку нажали именно в этом тике». **Это переносимый приём: если ввод нужен не только автору, храни его в `[Networked]`.**

**11. ⚠️ `Coin`: ни `FixedUpdateNetwork`, ни `Runner.Despawn` не закрыты гардом `IsServer`,** и `MoneyStorage.Spawned` вызывает `ReplicateToAll`/`ReplicateTo` тоже без гарда. Это не «расчёт на предсказание»: в том же разделе `CoinSpawner.FixedUpdateNetwork` гард **имеет** (`if (!this.Runner.IsServer) return;`), и `CameraFollower.Render` имеет null-гард, которого нет в `Coin.ProcessCollision`. Скорее пропуск. **Закрывай спавн/despawn/запись сетевого состояния гардом авторитета явно.**

**12. ⚠️ Мёртвый код, унаследованный из линии 01-06.** `MovementComponent.SetCondition` / `RotationComponent.SetCondition` в разделе 08 **не вызываются никем** — `CanMove()`/`CanRotate()` всегда `true`, потому что `_condition == null`. `ParentTransformConstraint` не используется ни в одной сцене и ни в одном префабе (ноль вхождений его guid); реальный парентинг под `[Runner]` сделан вручную в сцене, а не этим компонентом. Не принимай унаследованные API за приёмы этого раздела.

---

## Чеклист: как применить в своём проекте

1. Поставь `Simulation → Replication Features` в режим с Interest Management (в .fusion это `ReplicationFeatures: 3`). Проверь первым делом, если интерес «не работает».
2. Создай объект `[Runner]` с `NetworkRunner`, `NetworkSceneManagerDefault`, `NetworkEvents` и размести **все** `SimulationBehaviour`-сервисы либо на нём, либо на его детях. Иначе их колбэки не вызовутся.
3. Добавь `SimulationBehaviour, ISpawned`-инициализатор, который под `if (Runner.IsServer)` зовёт `SetAreaOfInterestGrid(x, y, z)` и `SetAreaOfInterestCellSize(size)`. Размеры выноси в `SerializeField`. Подбери `cellSize` так, чтобы сфера интереса накрывала единицы, а не десятки ячеек. Для игры с этажами задавай `y > 1`.
4. На объекте, который задаёт зону, повесь `NetworkBehaviour` с `FixedUpdateNetwork`, где под `if (Runner.IsServer)` идут `ClearPlayerAreaOfInterest(Object.InputAuthority)` и `AddPlayerAreaOfInterest(Object.InputAuthority, transform.position, radius)`. Каждый тик, без исключений.
5. Выстави `ObjectInterest = Global` на player object и на всём, от чего зависит вычисление интереса. Всё остальное — `AreaOfInterest`. Делай это в инспекторе, а не правкой YAML.
6. Для nested-объектов, подлежащих AoI, оставь `AutoUpdateAreaOfInterestOverride` включённым или вызови `SetAreaOfInterestOverride(parentObject)` руками: AoI-позиция вложенного `NetworkTransform` всегда невалидна.
7. Для объектов, которые нельзя терять при выходе из радиуса (босс, квестовый маркер, командный HUD), вызывай `Object.SetPlayerAlwaysInterested(player, true)` под гардом StateAuthority.
8. Для состояния, которое должен видеть только владелец, в `Spawned` бихевиора вызывай `ReplicateToAll(false)`, затем `ReplicateTo(Object.InputAuthority, true)` — **строго в этом порядке**.
9. Заведи один компонент-адаптер (`IInterestEnter` + `IInterestExit` + `IsInterestedIn`), который наружу отдаёт `event Action<bool>`. Внутри: фильтр `player == Runner.LocalPlayer`, `.GetValueOrDefault()` вместо `.Value`, и подъём начального события в `Spawned` для случая «объект приехал уже интересным».
10. В потребителе подпишись в `Spawned`, **отпишись в `Despawned`** и выстави начальное состояние вручную — на хост-пире это единственный путь.
11. Если нужно «объект выпал из интереса — выключи поведение», используй `ISimulationEnter`/`ISimulationExit`, а не `IInterestEnter`/`IInterestExit`: первая пара про «я симулируюсь», вторая про «кому меня отправляют».
12. Не выключай по интересу коллайдеры и игровые правила — только презентацию. Держи вьюшку на отдельном дочернем GameObject, а коллайдер и запросы физики — на корне.
13. Физику опрашивай один раз за тик в `IBeforeTick` через `Runner.GetPhysicsScene()`, в предвыделенный `Collider[]`, и раздавай потребителям как `(count, IReadOnlyList<Collider>)`. Никаких побочных эффектов в `BeforeTick`.
14. Спавни отдельный player-context `NetworkObject` вместо персонажа, регистрируй его через `Runner.SetPlayerObject(player, context)` **сразу после `Spawn`**, и только потом поднимай события инициализации. Любые данные игрока достаются как `Runner.GetPlayerObject(player).GetBehaviour<T>()` — с обязательной null-проверкой.
15. Ввод, который нужен не только его автору, держи в `[Networked]`-свойстве и заполняй в `BeforeTick` через `GetInput`.
16. Для «сетевых по доступу, но не по состоянию» компонентов наследуй `Fusion.Behaviour`, а не `MonoBehaviour` — тогда они находятся через `GetBehaviour<T>`, не попадая в `NetworkedBehaviours`.
17. Никогда не вызывай `UnityEngine.Random` (и любой другой нерепликуемый источник) в `FixedUpdateNetwork`.
18. Кешируй `GetBehaviour<T>`/`GetComponent<T>` в `Spawned`. Помни, что `GetBehaviour<T>` — это `GetComponentInChildren`: он перелезет в nested-`NetworkObject` и пропустит выключенные объекты.
19. Включи `RunnerAOIGizmos` на раннере для глазной отладки (только Server/Host, не Shared), а для программной — используй `Runner.GetObjectsInAreaOfInterestForPlayer(player)` (server only).
20. Отлаживай интерес в `GameMode.Host` со вторым инстансом (ParrelSync). В `GameMode.Single` отсечения трафика не видно вообще.
21. Не рассчитывай на `RpcTargets.All` как на «гарантированно всем»: отправляй критичные RPC через Global-объект или через player-context.
22. Для ручных сценариев заведи `[Button]`-методы, переводящие `int` в `PlayerRef` через `PlayerRef.FromIndex` — AoI юнит-тестами не проверить.

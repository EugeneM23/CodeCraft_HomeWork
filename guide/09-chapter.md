# Глава 09. Подключение и миграция хоста

> **TL;DR для агента**
> - Сделай сетевую сессию **самостоятельным объектом жизненного цикла**: отдельная Bootstrap-сцена + фасад под `DontDestroyOnLoad` + `NetworkRunner` как **инстанцируемый префаб**, а не сценовый объект. Только так ранер можно убить и создать заново произвольное число раз.
> - Идентифицируй игрока **не по `PlayerRef`, а по `ConnectionToken`**: `PlayerRef` — это слот подключения, он меняется при реконнекте и при миграции хоста.
> - Раздели **«контекст игрока»** (деньги, токен, инпут — живёт долго) и **«персонаж»** (умирает/респавнится). Связь — через `[Networked] NetworkObject`, не через `[SerializeField]`.
> - Выход игрока — это **grace-period в тиках**, а не удаление. Реконнект = `AssignInputAuthority` на старые объекты, а не новый `Spawn`.
> - Host migration восстанавливает **только `[Networked]`-состояние**. Префабные объекты пересоздай `Spawn + CopyStateFrom` в `onBeforeSpawned`, сценовым залей состояние и **доудали лишние**, а обычные C#-словари пересобери руками через `IHostMigrationHandler`.

---

## Какую проблему решает

До этой главы проект жил в идеальном мире: `GameMode.Single`, ранер лежит в сцене, игрок один, никто не отключается. Глава 09 ломает все три допущения сразу и заставляет ответить на четыре неприятных вопроса.

**1. Чем идентифицировать игрока?** Fusion выдаёт `PlayerRef` — индекс слота в комнате. Он переиспользуется: игрок ушёл, вернулся и получил другой индекс; или другой игрок получил его старый индекс. Если привязать к `PlayerRef` деньги, прогресс и персонажа, то после первого же разрыва связи игрок либо потеряет всё, либо получит чужое. Нужен идентификатор, который живёт дольше соединения, — это `ConnectionToken`: произвольный `byte[]`, который клиент передаёт в `StartGameArgs` и который сервер читает дважды — в `OnConnectRequest` (до того как клиент стал игроком) и потом через `GetPlayerConnectionToken(player)`.

**2. Кого вообще пускать в комнату?** Валидация токена в реальном проекте — это HTTP-запрос к бэкенду, то есть секунда и больше. А `OnConnectRequest` — синхронный колбэк внутри симуляции хоста. Заблокировать его `.Result`-ом значит заморозить всю игру для всех. Fusion решает это трёхзначным ответом `Accept / Refuse / Waiting`.

**3. Что делать с ушедшим игроком?** Разрыв в мультиплеере чаще всего временный: лифт, смена Wi-Fi, сама миграция хоста. Удалять контекст немедленно — значит гарантированно потерять прогресс игрока, который вернётся через пять секунд.

**4. Что происходит, когда уходит хост?** В Host/Client-топологии хост — это сервер. Его уход означает конец комнаты, если не включить host migration. Photon Cloud назначает нового хоста из клиентов, но новый хост — это бывший клиент: у него нет ни `StateAuthority`, ни локальных реестров, а сцену он загружает заново, с нуля. Восстановление мира — целиком ответственность вашего кода.

---

## Модель мира: кто чем владеет

Проект работает в режиме `GameMode.AutoHostOrClient` (`_gameMode: 6` в `Bootstrap.unity`; что 6 — это `AutoHostOrClient`, а не что-то другое, — **вывод** из порядка объявления enum и из того, что поле в C# инициализировано как `= GameMode.AutoHostOrClient`). Первый вошедший становится хостом, остальные — клиентами. Shared-режим здесь не используется, и все приёмы главы рассчитаны именно на единый `StateAuthority` у хоста.

Распределение ответственности:

| Что | Где исполняется | Как это выражено в коде |
|---|---|---|
| `Spawn`/`Despawn` контекста и персонажа | только хост | `if (this.Runner.IsServer)` в `PlayerJoined` / `PlayerLeft` / `FixedUpdateNetwork` |
| Запись `[Networked]`-полей (`Token`, `Character`, `Money`, `despawned`) | только `StateAuthority` = хост | записи сидят внутри `IsServer`-ветвей либо в `FixedUpdateNetwork` |
| Отсчёт grace-period | только хост | `FixedUpdateNetwork` под `if (!this.Runner.IsServer) return;` |
| `OnConnectRequest` | только хост | серверный хук авторизации |
| `OnConnectedToServer` / `OnConnectFailed` / `OnDisconnectedFromServer` | только клиент | комментарии `//Client:` в `GameFacade_Connection.cs` |
| `OnHostMigration` | **каждый пир** | и будущий хост, и клиенты гасят свой ранер и перезапускаются |
| `HostMigrationResume` | **только новый хост** | комментарий `//Server...` в коде; единственное место, где доступен снапшот |
| Сбор инпута | каждый пир у себя | `_runner.ProvideInput = true` + `NetworkEvents.OnInput` |
| Выбор цвета / имя объекта | каждый пир локально | `HasInputAuthority` в `Render()` |

Время считается **в тиках**, не в секундах реального времени. Grace-period: `Runner.Tick + Mathf.RoundToInt(TIMEOUT_SECONDS / Runner.DeltaTime)`. Это принципиально: `FixedUpdateNetwork` может ресимулироваться (прогоняться повторно для прошлых тиков), и `Time.time` в нём врёт. Тик — монотонная координата симуляции, сравнение тиков ресимуляцию переживает.

`InputAuthority` в этой главе становится **динамической величиной**. Раньше он выдавался один раз при спавне и жил до смерти объекта. Теперь он снимается и переназначается: при реконнекте (`AssignInputAuthority(player)`) и после миграции (все префабные объекты пересоздаются с `PlayerRef.None`). Любая логика, вычисленная один раз в `Spawned()` по `InputAuthority`, после этого станет неверной.

Area of Interest в разделе **отключён** (`ReplicationFeatures: 1` в `NetworkProjectConfig.fusion`, против `3` в разделе 08), lag compensation тоже (`LagCompensation.Enabled: false`). Это снимает шум: всё состояние едет всем, кроме того, что явно ограничено `ReplicateTo`.

---

## Архитектура раздела

```
┌──────────────────────── Bootstrap.unity (индекс 1) ─────────────────────────┐
│  <GameFacade>   [DontDestroyOnLoad]                                         │
│    ├── NetworkSceneManagerDefault   (IsSceneTakeOverEnabled: 0)             │
│    ├── GameFacade  (partial × 5, INetworkRunnerCallbacks)                   │
│    └── ConnectionTokenService  (мок бэкенда, Task<byte[]>)                  │
└─────────────────────────────────────────────────────────────────────────────┘
          │ Instantiate(_networkRunnerPrefab)      ▲  Shutdown(destroyGameObject: true)
          ▼                                        │
┌──────────────────── [Runner].prefab (создаётся заново) ─────────────────────┐
│  [Runner]: NetworkRunner + NetworkEvents                                    │
│    ├── <Coins>   CoinSpawner          (m_Enabled: 0 — выключен!)            │
│    ├── <Input>   InputAccumulator     (_networkEvents -> NetworkEvents)     │
│    ├── <Camera>  CameraFollower       (_offset 0,40,0)                      │
│    └── <Players> PlayerContextService + PlayerCharactersController          │
└─────────────────────────────────────────────────────────────────────────────┘
          │ StartGame(Scene = SceneRef.FromIndex(0))
          ▼
┌──────────────────────── Game.unity (индекс 0) ──────────────────────────────┐
│  [World]: Coin ×4 (сценовые NetworkObject), PlayerContext(+nested Character)│
│  [SpawnPoints]: SpawnPointProvider    [Camera]: MainCamera                  │
│  спавнятся в рантайме: Player.prefab (контекст), Character.prefab           │
└─────────────────────────────────────────────────────────────────────────────┘
```

Поток подключения:

```
клиент: StartGame(ConnectionToken=token)
   -> хост: OnConnectRequest(token)  ──TryAdd(Waiting)──> ValidateToken (async, 1 сек)
        |                                                        |
        |<── request.Waiting() ... Fusion переспрашивает ─────────┘
        └── request.Accept() / Refuse()
   -> клиент: OnConnectedToServer | OnConnectFailed(reason)
   -> хост:   IPlayerJoined.PlayerJoined -> JoinPlayer
                 GetPlayerConnectionToken -> _allContexts[token]?
                   есть  -> isNew=false, AssignInputAuthority
                   нет   -> isNew=true,  Spawn(Player) + Token + ReplicateTo
                 SetPlayerObject -> ProcessJoin(player, context, isNew)
                   -> PlayerCharactersController: Spawn(Character) | AssignInputAuthority
                 PushHostMigrationSnapshot()
```

Поток миграции:

```
старый хост: Quit() -> Shutdown(true, ShutdownReason.HostMigration)
   -> Photon Cloud выбирает нового хоста
   -> ВСЕ пиры: OnHostMigration(runner, hostMigrationToken)
        await runner.Shutdown()  ->  новый токен  ->  Instantiate(ранер)
        StartGame(+ HostMigrationToken, + HostMigrationResume)
   -> ТОЛЬКО новый хост: HostMigrationResume(runner)
        1) MigratePrefabObjects  : Spawn + CopyStateFrom(prefabObject)
        2) MigrateSceneObjects   : CopyStateFrom(headerPtr) + Despawn лишних
        3) InvokeAfterMigration  : IHostMigrationHandler.AfterMigration()
   -> PushHostMigrationSnapshot()
```

Слои читаются так: **Bootstrap** — вечный слой, он переживает всё; **[Runner].prefab** — слой сессии, умирает и рождается целиком; **Game.unity** — слой мира, перезагружается при каждом `StartGame`. Системы (`PlayerContextService`, `CameraFollower`, `CoinSpawner`, `InputAccumulator`) живут в слое сессии, потому что обязаны жить ровно столько же, сколько ранер.

---

## Реализация по шагам

### Шаг 1. Фасад под `DontDestroyOnLoad` и ранер-префаб

`Assets/Game/Scripts/App/GameFacade.cs`

```csharp
public sealed partial class GameFacade : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private GameMode _gameMode = GameMode.AutoHostOrClient;
    [SerializeField] private int _playerCount = 4;
    [SerializeField] private string _sessionName = "SampleSession";
    [SerializeField] private NetworkRunner _networkRunnerPrefab;
    [SerializeField] private NetworkSceneManagerDefault _sceneManager;
    private NetworkRunner _runner;
    [SerializeField] private int _gameSceneIndex;
    [SerializeField] private bool _autoRun = true;
    [SerializeField] private ConnectionTokenService _connectionTokenService;

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    private void Start()
    {
        if (_autoRun)
            this.StartGame();
    }
```

**Почему именно так.** `_networkRunnerPrefab` — это `NetworkRunner`, лежащий **в префабе**, а не ссылка на сценовый объект. Весь раздел держится на этом: `Shutdown(destroyGameObject: true)` физически уничтожает объект ранера, а миграция требует создать его заново — возможно, несколько раз за сессию. Сценовый ранер после первого `Shutdown` восстановить нечем. `DontDestroyOnLoad` нужен, потому что `StartGame` грузит `Game.unity` поверх `Bootstrap.unity`, и фасад обязан выжить: он держит и токен-сервис, и `_pendingConnections`, и ссылку на `_sceneManager`. Побочное следствие, важное для реконнекта: `ConnectionTokenService.Awake` выполняется один раз за процесс, поэтому при переподключении предъявляется **тот же самый токен** — именно это и делает реконнект возможным.

### Шаг 2. Старт сессии: сначала токен, потом ранер

`Assets/Game/Scripts/App/GameFacade.cs`

```csharp
        [Button]
        public async void StartGame()
        {
            byte[] token = await _connectionTokenService.GetToken();
            Debug.Log($"Start Game with token {BitConverter.ToInt32(token)}");

            _runner = Instantiate(_networkRunnerPrefab);
            _runner.AddCallbacks(this);
            _runner.name = _networkRunnerPrefab.name;
            _runner.ProvideInput = true;

            StartGameResult result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = _gameMode,
                SessionName = _sessionName,
                SceneManager = _sceneManager,
                Scene = SceneRef.FromIndex(_gameSceneIndex),
                PlayerCount = _playerCount,
                ConnectionToken = token 
            });

            if (!result.Ok)
                _runner = null;
        }
        
        [Button]
        public void Shutdown()
        {
            if (_runner) 
                _runner.Shutdown(destroyGameObject: true);
        }
```

**Почему именно так.** Порядок строк не случаен. Токен получается **до** `Instantiate`: он обязательный аргумент `StartGameArgs`, и без него сервер не сможет опознать игрока ни в `OnConnectRequest`, ни в `GetPlayerConnectionToken`. `AddCallbacks(this)` вызывается руками, потому что ранер теперь инстанцируется в рантайме, и автоматический поиск реализаций `INetworkRunnerCallbacks` в сцене на фасад из другой сцены не сработает. `_runner.name = _networkRunnerPrefab.name` — косметика, но полезная: иначе в иерархии будет `[Runner] (Clone)`, а при отладке миграции вы смотрите в иерархию постоянно. `ProvideInput = true` обязателен на **каждом** новом ранере: забыть — значит оказаться в игре, но без управления. `if (!result.Ok) _runner = null;` не даёт остаться со ссылкой на полуживой ранер, по которой следующий `Shutdown()` попытается работать.

Обратите внимание на `if (_runner)` вместо `if (_runner != null)`. `Shutdown()` не присваивает `_runner = null`, а `OnShutdown` в проекте — пустая заглушка, то есть после `Shutdown()` поле хранит ссылку на **уничтоженный** Unity-объект. Спасает только Unity-овский implicit `bool`, который для уничтоженного объекта возвращает `false`; строгое `!= null` здесь вернуло бы `true` и уронило бы код. Это самостоятельный приём: **для ссылок на Unity-объекты всегда пишите `if (obj)`, а не `if (obj != null)`.**

⚠️ В проекте не реализовано, рекомендация: в `Shutdown()` и в `OnShutdown` обнуляйте `_runner` явно, а `StartGame` оберните в `try/catch`. `async void` не даёт исключению всплыть — упавший `StartGame` вы увидите только по отсутствию игры.

### Шаг 3. Асинхронная авторизация в синхронном колбэке

`Assets/Game/Scripts/App/GameFacade_Connection.cs`

```csharp
        public event Action OnConnectionSucceed;
        public event Action<NetConnectFailedReason> OnConnectionFailed;
        
        private readonly Dictionary<int, OnConnectionRequestReply> _pendingConnections = new();

        void INetworkRunnerCallbacks.OnConnectRequest(
            NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token
        )
        {
            int connectionToken = BitConverter.ToInt32(token);
            Debug.Log($"Connection request with token {connectionToken}");

            if (_pendingConnections.TryAdd(connectionToken, OnConnectionRequestReply.Waiting))
                this.ValidateToken(token, connectionToken);

            switch (_pendingConnections[connectionToken])
            {
                case OnConnectionRequestReply.Ok:
                    request.Accept();
                    break;
                case OnConnectionRequestReply.Refuse:
                    request.Refuse();
                    break;
                case OnConnectionRequestReply.Waiting:
                    request.Waiting();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async void ValidateToken(byte[] token, int connectionToken)
        {
            if (await _connectionTokenService.ValidateToken(token))
                _pendingConnections[connectionToken] = OnConnectionRequestReply.Ok;
            else
                _pendingConnections[connectionToken] = OnConnectionRequestReply.Refuse;
        }
```

**Почему именно так.** Это эталонный паттерн главы, и в нём три отдельные идеи.

Первая: `OnConnectRequest` **вызывается повторно**, пока вы отвечаете `request.Waiting()`. Значит проверку нельзя ждать внутри колбэка (встанет симуляция хоста) и нельзя запускать на каждом вызове (получите N параллельных запросов к бэкенду на один токен). `Dictionary.TryAdd` решает оба вопроса одной строкой: он возвращает `true` только при первом попадании токена, и ровно тогда стартует `ValidateToken`. Вторая: состояние хранится **в enum-е самого Fusion** — `OnConnectionRequestReply`. Свой дублирующий enum не нужен, а `switch` по нему компилятор проверит на полноту. Третья: наружу фасад отдаёт не колбэки Fusion, а нормальные C#-события `OnConnectionSucceed` / `OnConnectionFailed(NetConnectFailedReason)`, то есть UI не знает про `INetworkRunnerCallbacks` вообще. `NetConnectFailedReason` важно донести до UI: он различает «сервер отказал» и «не достучались», а это два разных текста для игрока.

Что сломается иначе: синхронное ожидание — фриз хоста на время запроса к бэкенду у всех игроков; запуск проверки без `TryAdd` — лавина запросов и гонка за запись вердикта; отсутствие ветки `Waiting` — Fusion не получит ответа и подключение повиснет.

⚠️ Латентный дефект этой версии: `_pendingConnections` **никогда не чистится**, а фасад живёт под `DontDestroyOnLoad` и переживает все миграции. Следствия: отказанный токен навсегда останется отказанным и не сможет повторить попытку; принятый токен при реконнекте проходит не валидацию, а кэш. В `[Teacher]`-варианте на обеих терминальных ветках стоит `_pendingRequests.Remove(connectionToken);` — делайте так:

`Assets/[Teacher]/GameFacade_Connection.cs` (в проекте закомментирован целиком)

```csharp
//             switch (_pendingRequests[connectionToken])
//             {
//                 case OnConnectionRequestReply.Ok:
//                     _pendingRequests.Remove(connectionToken);
//                     request.Accept();
//                     break;
//                 case OnConnectionRequestReply.Refuse:
//                     _pendingRequests.Remove(connectionToken);
//                     request.Refuse();
//                     break;
//                 case OnConnectionRequestReply.Waiting:
//                     request.Waiting();
//                     break;
```

### Шаг 4. Разделение «боевых» и «пустых» колбэков

`INetworkRunnerCallbacks` в Fusion 2 — это 19 методов. Проект использует пять: `OnConnectRequest`, `OnConnectFailed`, `OnConnectedToServer`, `OnDisconnectedFromServer`, `OnHostMigration`. Остальные 14 реализованы **публично и пусто** в отдельном файле.

`Assets/Game/Scripts/App/GameFacade_Unused.cs`

```csharp
    public sealed partial class GameFacade
    {
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }
        // ... ещё 13 таких же пустых публичных методов
    }
```

а используемые — **явной реализацией интерфейса**:

```csharp
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(
            NetworkRunner runner,
            NetDisconnectReason reason
        )
```

**Почему именно так.** Конвенция работает как визуальный маркер: `void INetworkRunnerCallbacks.X(...)` — «это вызывает Fusion, из моего кода это недоступно»; `public void X(...)` — «шум, заглушка». Явная реализация ещё и прячет пять боевых методов из публичного API фасада, чтобы никто не позвал `facade.OnHostMigration(...)` руками. Плюс один `sealed partial class` разложен по файлам-ответственностям: `GameFacade.cs` (конфиг + старт + shutdown), `_Connection`, `_Disconnect`, `_HostMigration`, `_Unused`. Это четыре независимые темы, которые правят в разное время и по разным причинам; в одном файле три важных колбэка утонут в семнадцати пустых.

Отмечу расхождение: в `[Teacher]/GameFacade_Unused.cs` конвенция **обратная** — там неиспользуемые объявлены явной реализацией внутри `#region UnusedCallbacks`. То есть «public = шум, explicit = боевое» — это решение финальной версии, и именно его стоит копировать: оно даёт различимость без региона.

### Шаг 5. Реакция на дисконнект и ручной кик

`Assets/Game/Scripts/App/GameFacade_Disconnect.cs`

```csharp
        public event Action<NetDisconnectReason> OnDisconnected;
        
        [Button]
        public void DisconnectPlayer(int playerIndex)
        {
            PlayerRef player = PlayerRef.FromIndex(playerIndex);
            if (_runner)
                _runner.Disconnect(player);
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(
            NetworkRunner runner,
            NetDisconnectReason reason
        )
        {
            runner.Shutdown(destroyGameObject: true, ShutdownReason.Ok);
            Debug.Log($"Disconnected {reason}");
            this.OnDisconnected?.Invoke(reason);
        }
```

**Почему именно так.** `OnDisconnectedFromServer` — это уведомление, а не уборка: Fusion сообщает факт, но ранер остаётся в памяти в нерабочем состоянии. Если не сделать явный `Shutdown`, следующий `StartGame` будет сделан поверх мусора. `[Button]` + `PlayerRef.FromIndex` — рабочий способ тестировать реконнект руками: кикнуть игрока из инспектора хоста и посмотреть, вернётся ли он к своему контексту.

### Шаг 6. Контекст игрока и персонаж — два разных объекта

`Assets/Game/Scripts/Player/CharacterProvider.cs`

```csharp
    public sealed class CharacterProvider : NetworkBehaviour
    {
        [Networked]
        public NetworkObject Character { get; set; }
    }
```

`Assets/Game/Scripts/Player/ConnectionTokenComponent.cs`

```csharp
    public sealed class ConnectionTokenComponent : NetworkBehaviour
    {
        [Networked]
        public int Token { get; set; }
    }
```

**Почему именно так.** `Player.prefab` стал плоским контекстом с тегом `Player` (`PlayerInput`, `MoveController`, `MoneyStorage`, `CharacterProvider`, `PlayerNameConstraint`, `ConnectionTokenComponent`), а `Character.prefab` — самостоятельным `NetworkObject` (на корне: `NetworkTransform`, `MovementComponent`, `RotationComponent`, `PlayerNameConstraint`; `ColorComponent` сидит на дочернем `<Visual>`). В разделе 08 персонаж был **вложен** в `Player.prefab`, а `CharacterProvider` был `Fusion.Behaviour` с `[SerializeField] GameObject`. Такая связь ломается дважды: любое удаление персонажа уносит с собой деньги и токен, а сериализованная ссылка не может указывать на отдельно заспавненный префаб и не переживает host migration. `[Networked] NetworkObject Character` переживает: `CopyStateFrom` копирует именно `[Networked]`-поля.

Цена решения — ссылка теперь **может быть пустой**, и это нормальное состояние, а не ошибка: между `PlayerJoined` и фактическим спавном персонажа, и после его `Despawn`. Поэтому `MoveController` получил защиту.

`Assets/Game/Scripts/Player/MoveController.cs`

```csharp
        public override void FixedUpdateNetwork()
        {
            Vector3 direction = _input.CurrentInput.moveDirection;
            float deltaTime = this.Runner.DeltaTime;

            NetworkObject character = _characterProvider.Character;
            if (!character)
                return;

            character.GetComponent<MovementComponent>().MoveStep(direction, deltaTime);
            character.GetComponent<RotationComponent>().RotateStep(direction, deltaTime);
        }
```

Работает это потому, что в конфиге стоит `NullChecksForNetworkedProperties: true` — обращение к пустому `[Networked] NetworkObject` ведёт себя как обычная проверка на null, а не бросает.

Отдельно стоит отметить, почему управление вообще продолжает работать после переназначения `InputAuthority`: инпут кэшируется в `[Networked]`-поле.

`Assets/Game/Scripts/Player/PlayerInput.cs`

```csharp
    public sealed class PlayerInput : NetworkBehaviour, IBeforeTick
    {
        [Networked]
        public InputData CurrentInput { get; private set; }

        public InputData PreviousInput { get; private set; }

        void IBeforeTick.BeforeTick()
        {
            this.PreviousInput = this.CurrentInput;
            this.CurrentInput = this.GetInput(out InputData input) ? input : default;
        }
    }
```

`GetInput()` возвращает данные только тому, у кого есть `InputAuthority`; при `PlayerRef.None` он вернёт `false`, и `CurrentInput` станет `default` — то есть персонаж просто встанет, а не начнёт двигаться по мусорным данным.

### Шаг 7. Вход игрока: реконнект по токену

`Assets/Game/Scripts/System/Players/PlayerContextService.cs`

```csharp
        private void JoinPlayer(PlayerRef player)
        {
            int token = BitConverter.ToInt32(this.Runner.GetPlayerConnectionToken(player));
            bool isNew;

            if (_allContexts.TryGetValue(token, out NetworkObject context))
            {
                isNew = false;
                context.AssignInputAuthority(player);
            }
            else
            {
                isNew = true;

                context = this.Runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity, player);

                ConnectionTokenComponent tokenComponent = context.GetComponent<ConnectionTokenComponent>();
                tokenComponent.Token = token;
                tokenComponent.ReplicateToAll(false);
                tokenComponent.ReplicateTo(player, true);

                _allContexts.Add(token, context);
            }

            _timeoutContexts.Remove(context);

            this.Runner.SetPlayerObject(player, context);
            this.ProcessJoin?.Invoke(player, context, isNew);

            //Photon Cloud..
            this.Runner.PushHostMigrationSnapshot();
        }
```

**Почему именно так.** Ключ словаря — **токен**, не `PlayerRef`. Нашёлся контекст — это реконнект: ничего не спавним, только переназначаем `InputAuthority` на новый `PlayerRef`, и игрок получает обратно свои деньги. Не нашёлся — спавним и записываем токен в `[Networked] int`, чтобы он выжил и при миграции.

Две строки `ReplicateToAll(false)` + `ReplicateTo(player, true)` ограничивают репликацию **одного** `NetworkBehaviour` до его владельца. Токен — приватные данные: раздать его всем значит выдать каждому возможность представиться другим игроком и забрать его контекст. Это точечная фильтрация на уровне behaviour, а не объекта, — именно поэтому токен и вынесен в отдельный `ConnectionTokenComponent`, а не добавлен полем в `MoneyStorage`.

`_timeoutContexts.Remove(context)` снимает «приговор» на удаление по таймауту — это и есть момент, когда вернувшийся игрок спасает свой контекст. `SetPlayerObject` регистрирует контекст во встроенном реестре Fusion, откуда его читают `LocalContext`, `Coin` и `CameraFollower`. `PushHostMigrationSnapshot()` в конце нужен поверх автопуша (в конфиге `EnableAutoUpdate: true, UpdateDelay: 10`): автопуш может опоздать, а только что созданный контекст терять нельзя.

### Шаг 8. Grace-period вместо удаления

`Assets/Game/Scripts/System/Players/PlayerContextService.cs`

```csharp
        private void LeftPlayer(PlayerRef player)
        {
            NetworkObject context = this.Runner.GetPlayerObject(player);
            this.Runner.SetPlayerObject(player, null);

            Tick timeoutTick = this.Runner.Tick + Mathf.RoundToInt(TIMEOUT_SECONDS / this.Runner.DeltaTime);
            _timeoutContexts.Add(context, timeoutTick);
        }

        public override void FixedUpdateNetwork()
        {
            if (!this.Runner.IsServer)
                return;

            Tick currentTick = this.Runner.Tick;
            foreach ((NetworkObject context, Tick timeoutTick) in _timeoutContexts.ToArray())
            {
                if (timeoutTick > currentTick)
                    continue;

                _timeoutContexts.Remove(context);
                _allContexts.Remove(context.GetComponent<ConnectionTokenComponent>().Token);
                this.ProcessLeft?.Invoke(context);
                this.Runner.Despawn(context);
            }
        }
```

**Почему именно так.** Выход игрока не удаляет ничего — только снимает регистрацию и ставит дедлайн. `SetPlayerObject(player, null)` обязателен: иначе контекст, который ещё ждёт реконнекта, будет считаться принадлежащим ушедшему `PlayerRef`, а этот `PlayerRef` Fusion переиспользует для следующего входящего. `.ToArray()` нужен, чтобы удалять из словаря внутри цикла. Порядок внутри цикла важен: сначала вынуть из обоих реестров, потом поднять `ProcessLeft` (подписчик удалит персонажа), и только потом `Despawn` самого контекста — иначе подписчик получит уже мёртвый объект.

`TIMEOUT_SECONDS = 10f` совпадает с `Network.ConnectionTimeout: 10.0` в конфиге. **Вывод, а не факт из кода:** grace-period подогнан под таймаут соединения Photon, чтобы контекст не умирал раньше, чем Photon вообще признает соединение потерянным.

### Шаг 9. Флаг `isNew`: один хук на два сценария

`Assets/Game/Scripts/System/Players/PlayerCharactersController.cs`

```csharp
        void ISpawned.Spawned()
        {
            _playerService = this.Runner.GetBehaviour<PlayerContextService>();
            _playerService.ProcessJoin += this.ProcessJoin;
            _playerService.ProcessLeft += this.ProcessLeft;
        }

        void IDespawned.Despawned(NetworkRunner runner, bool hasState)
        {
            if (_playerService)
            {
                _playerService.ProcessJoin -= this.ProcessJoin;
                _playerService.ProcessLeft -= this.ProcessLeft;
            }
        }

        private void ProcessJoin(PlayerRef player, NetworkObject context, bool isNew)
        {
            CharacterProvider characterProvider = context.GetBehaviour<CharacterProvider>();
            if (isNew)
            {
                Transform spawnPoint = FindObjectOfType<SpawnPointProvider>().NextPoint();
                NetworkObject character = this.Runner.Spawn(_characterPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation,
                    player
                );
                characterProvider.Character = character;
            }
            else
            {
                characterProvider.Character.AssignInputAuthority(player);
            }
        }

        private void ProcessLeft(NetworkObject context)
        {
            CharacterProvider characterProvider = context.GetBehaviour<CharacterProvider>();
            if (characterProvider)
                // characterProvider.Character.RemoveInputAuthority();
                this.Runner.Despawn(characterProvider.Character);
        }
```

**Почему именно так.** `PlayerContextService` владеет идентичностью и реестром, `PlayerCharactersController` — только персонажем. Разница между «первый вход» и «реконнект» — ровно одна строка, и флаг `isNew` в делегате `JoinHandler(PlayerRef player, NetworkObject context, bool isNew)` разводит её без того, чтобы контроллер что-то знал про токены. При реконнекте персонаж **не пересоздаётся**: сохраняются позиция, скорость и всё `[Networked]`-состояние, игрок возвращается туда, где стоял.

`ISpawned.Spawned()` у `SimulationBehaviour` вызывается, когда стартовал ранер, — это правильный момент для подписок (`Awake` слишком рано: `Runner` ещё не готов). `GetBehaviour<PlayerContextService>()` находит сервис именно потому, что обе системы — дети одного префаба ранера. Проверка `if (_playerService)` в `Despawned` нужна, потому что порядок уничтожения компонентов не гарантирован.

Закомментированный `RemoveInputAuthority()` — не украшение, а след важного решения: в текущей версии персонаж **сохраняет** `InputAuthority` на всё время grace-period. Это и порождает дефект в `Coin` (см. раздел про антипаттерны).

### Шаг 10. Запуск и приём миграции

`Assets/Game/Scripts/App/GameFacade_HostMigration.cs`

```csharp
        [Button]
        public void Quit()
        {
            if (_runner) 
                _runner.Shutdown(destroyGameObject: true, ShutdownReason.HostMigration);
        }
        
        //Client / Server
        async void INetworkRunnerCallbacks.OnHostMigration(
            NetworkRunner runner,
            HostMigrationToken hostMigrationToken
        )
        {
            Debug.Log("Host migration");
            await runner.Shutdown();

            byte[] token = await _connectionTokenService.GetToken();

            _runner = Instantiate(_networkRunnerPrefab);
            _runner.name = _networkRunnerPrefab.name;
            _runner.ProvideInput = true;

            StartGameResult result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = _gameMode,
                SessionName = _sessionName,
                SceneManager = _sceneManager,
                Scene = SceneRef.FromIndex(_gameSceneIndex),
                PlayerCount = _playerCount,
                ConnectionToken = token,
                HostMigrationToken = hostMigrationToken,
                HostMigrationResume = HostMigrationResume
            });

            if (result.Ok)
                await _runner.PushHostMigrationSnapshot();
            else
                _runner = null;
        }
```

**Почему именно так.** `ShutdownReason.HostMigration` — это не просто «я ушёл»: именно эта причина заставляет Photon Cloud назначить нового хоста и разослать всем `OnHostMigration`, вместо того чтобы закрыть комнату (**вывод по коду и смыслу API**). `OnHostMigration` прилетает **каждому** пиру, включая будущего хоста, и каждый делает одно и то же: гасит старый ранер и запускает сессию заново. `await runner.Shutdown()` обязателен **до** `Instantiate` — иначе в сцене окажутся два ранера одновременно, две копии всех систем и два набора подписок. Отличие от обычного `StartGame` — два поля: `HostMigrationToken` (превращает старт в возобновление) и `HostMigrationResume` (колбэк восстановления). `PushHostMigrationSnapshot()` сразу после успешного старта делает новое состояние доступным для **следующей** миграции.

⚠️ Два дефекта этой версии, которые надо исправить у себя. Первый: здесь **нет** `_runner.AddCallbacks(this)` на новом ранере. Следствие — вторая миграция подряд этим фасадом уже не будет обработана. Второй: `GameMode = _gameMode` берётся из инспектора; надёжнее `hostMigrationToken.GameMode` — токен точно знает, в каком режиме шла сессия. В `[Teacher]`-варианте оба места сделаны правильно, там же `await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration)` вместо `await runner.Shutdown()`.

### Шаг 11. Восстановление префабных объектов

`Assets/Game/Scripts/App/GameFacade_HostMigration.cs`

```csharp
        //Server...
        private void HostMigrationResume(NetworkRunner runner)
        {
            MigratePrefabObjects(runner);
            MigrateSceneObjects(runner);
            InvokeAfterMigration(runner);
        }

        private static void MigratePrefabObjects(NetworkRunner runner)
        {
            IEnumerable<NetworkObject> prefabObjects = runner.GetResumeSnapshotNetworkObjects();
            foreach (NetworkObject prefabObject in prefabObjects)
            {
                NetworkObjectTypeId typeId = prefabObject.NetworkTypeId;
                if (!typeId.IsValid)
                    continue;

                if (!typeId.IsPrefab)
                    continue;

                Debug.Log($"Prefab object migration {prefabObject.Name}");
                NetworkTRSP transform = prefabObject.GetComponent<NetworkTRSP>();
                Vector3 position = transform != null ? transform.Data.Position : Vector3.zero;
                Quaternion rotation = transform != null ? transform.Data.Rotation : Quaternion.identity;

                runner.Spawn(prefabObject, position, rotation, PlayerRef.None,
                    onBeforeSpawned: (_, obj) => obj.CopyStateFrom(prefabObject));
            }
        }
```

**Почему именно так.** Новый хост загрузил `Game.unity` заново, и всё, что спавнилось в рантайме (контексты игроков, персонажи), просто отсутствует. Снапшот даёт «временные» объекты-носители прошлого состояния, по которым надо пересоздать настоящие.

Три детали, каждая из которых обязательна. (1) Фильтры `IsValid` и `IsPrefab`: в снапшоте лежат и сценовые, и невалидные записи, `Spawn` по ним упадёт. (2) Позиция берётся из `NetworkTRSP.Data`, а не из `prefabObject.transform.position`: трансформ — это тоже `[Networked]`-состояние, и в объекте-носителе из снапшота достоверно именно оно. Это прямое следствие того, как устроено движение в проекте: `MovementComponent` и `RotationComponent` — обычные `MonoBehaviour`, которые двигают `transform` на хосте, а в сеть это уезжает через `NetworkTransform`/`NetworkTRSP`. Защита `transform != null` нужна потому, что объект мог не иметь `NetworkTransform` — например, контекст игрока: `Player.prefab` его не имеет, восстановится в нулевую позицию, и это правильно, его позиция никому не нужна. (3) `CopyStateFrom` вызывается **в `onBeforeSpawned`**, то есть до того, как объект начнёт симулироваться и реплицироваться; иначе один тик уйдёт в сеть с дефолтным состоянием, и клиенты увидят мигание.

`PlayerRef.None` в качестве `inputAuthority` — осознанное решение: кто из игроков реально ещё в комнате, новый хост в этот момент не знает. Авторитет восстановится позже, при реконнекте, в `JoinPlayer` / `ProcessJoin`. Именно поэтому в `Coin.ProcessCollision` стоит `if (player.IsNone) return false;` — страховка на промежуток между возобновлением сессии и повторным `AssignInputAuthority`.

`[Teacher]`-вариант того же места идиоматичнее для Fusion — `TryGetBehaviour` вместо `GetComponent` + сравнения с null:

`Assets/[Teacher]/GameFacade_HostMigration.cs` (в проекте закомментирован целиком)

```csharp
//                 bool hasTRSP  = previousObject.TryGetBehaviour(out NetworkTRSP trsp);
//                 Vector3 position = hasTRSP ? trsp.Data.Position : Vector3.zero;
//                 Quaternion rotation = hasTRSP ? trsp.Data.Rotation : Quaternion.identity;
//
//                 runner.Spawn(previousObject, position, rotation, 
//                     onBeforeSpawned: (_, newObject) => newObject.CopyStateFrom(previousObject));
```

### Шаг 12. Восстановление сценовых объектов и уборка лишних

`Assets/Game/Scripts/App/GameFacade_HostMigration.cs`

```csharp
        private static void MigrateSceneObjects(NetworkRunner runner)
        {
            HashSet<NetworkObject> migatedSceneObjects = new HashSet<NetworkObject>();
            IEnumerable<(NetworkObject, NetworkObjectHeaderPtr)> sceneObjects = runner.GetResumeSnapshotNetworkSceneObjects();
            foreach ((NetworkObject current, NetworkObjectHeaderPtr previous) in sceneObjects)
            {
                Debug.Log($"Scene object migration {current.name} {previous.Id}");
                current.CopyStateFrom(previous);
                migatedSceneObjects.Add(current);
            }

            foreach (NetworkObject networkObject in runner.GetAllNetworkObjects())
                if (networkObject.NetworkTypeId.IsSceneObject && !migatedSceneObjects.Contains(networkObject))
                    runner.Despawn(networkObject);
        }
        
        private static void InvokeAfterMigration(NetworkRunner runner)
        {
            IHostMigrationHandler[] migrationHandlers = runner.GetComponentsInChildren<IHostMigrationHandler>();
            foreach (IHostMigrationHandler handler in migrationHandlers) 
                handler.AfterMigration();
        }
```

**Почему именно так.** Со сценовыми объектами логика обратная: они уже существуют после загрузки сцены, их не надо спавнить — им надо залить состояние. Сопоставление «новый экземпляр ↔ старый хедер» делает Fusion, коду остаётся `CopyStateFrom(previous)`.

Второй цикл — то, о чём забывают. Загрузка сцены воскресила **все** сценовые `NetworkObject`, включая те, которые по ходу игры были уничтожены. В снапшоте их нет, значит мир частично откатится к начальному состоянию сцены, если их не отстрелить. В этом разделе это не теоретический риск, а **основной демонстрационный сценарий**: `CoinSpawner` на `<Coins>` **выключен** (`m_Enabled: 0`), а в `Game.unity` под `[World]` положены четыре экземпляра префаба `Coin` (`Coin`, `Coin (1)`, `Coin (2)`, `Coin (3)`) — то есть монеты здесь **сценовые**, а не префабные. Уже собранная монета была `Despawn`-нута, в снапшоте её нет, после перезагрузки сцены на новом хосте она появляется снова — и гасит её именно этот цикл. Там же лежат тестовые сценовые `PlayerContext` (с `NestedObjects: [Character]`) и вложенный `Character` — материал для проверки случая вложенного `NetworkObject`.

`InvokeAfterMigration` через `GetComponentsInChildren` работает **только потому**, что все системы живут детьми префаба ранера. Это прямая связь между архитектурным решением шага 1 и возможностью хука.

### Шаг 13. `IHostMigrationHandler` — восстановление нереплицируемого

`Assets/Game/Scripts/System/HostMigration/IHostMigrationHandler.cs`

```csharp
    public interface IHostMigrationHandler
    {
        void AfterMigration();
    }
```

`Assets/Game/Scripts/System/Players/PlayerContextService.cs`

```csharp
        void IHostMigrationHandler.AfterMigration()
        {
            Debug.Log("AFTER HOST MIGRATION");
            
            _allContexts.Clear();
            _timeoutContexts.Clear();
            
            Tick timeoutTick = this.Runner.Tick + Mathf.RoundToInt(TIMEOUT_SECONDS / this.Runner.DeltaTime);
            
            foreach (NetworkObject networkObject in this.Runner.GetAllNetworkObjects())
            {
                if (!networkObject.CompareTag(GameObjectTags.Player))
                    continue;

                int token = networkObject.GetComponent<ConnectionTokenComponent>().Token;
                Debug.Log($"TOKEN IS {token} {networkObject.name}", networkObject);
                _allContexts.Add(token, networkObject);
                _timeoutContexts.Add(networkObject, timeoutTick);
            }
        }
```

**Почему именно так.** Главный урок по миграции: `CopyStateFrom` восстанавливает **только `[Networked]`-поля**. Все обычные C#-структуры — словари, индексы, кэши, подписки — на новом хосте пусты, потому что он раньше был клиентом и ничего этого не вёл. Без этого хука `PlayerContextService` после миграции не знает ни одного контекста, и первый же реконнект пойдёт по ветке `isNew == true`: игрок получит **второго** персонажа и пустой кошелёк, а старый контекст останется в мире навсегда.

Реестр восстанавливается не из реплицированного словаря (его дорого и ненадёжно делать `[Networked]`), а **из самих объектов**: обход `GetAllNetworkObjects()` по тегу `Player` + чтение `[Networked] Token`. Это «состояние, размазанное по объектам» вместо «состояние в центральном сервисе» — и оно восстанавливается точно, потому что объекты сами несут нужные данные.

И принципиальное решение: каждый найденный контекст кладётся **и** в `_allContexts`, **и** в `_timeoutContexts`. После миграции никто не считается подключённым: новый хост не знает, кто реально в комнате, а `PlayerJoined` для уже находящихся придёт заново. Кто вернётся — будет вынут из таймаута в `JoinPlayer`; кто нет — удалится сам. Отказоустойчивость по умолчанию в сторону уборки.

Альтернатива из `[Teacher]/PCS.cs` — реестр на `List<NetworkObject>` с линейным поиском `GetContextByToken` вместо `Dictionary<int, NetworkObject>`: O(n) против O(1), зато токен не обязан быть ключом и `AfterMigration` проще (`_allContexts.Add(no)` без чтения токена). Для десятка игроков разница неощутима; для словаря аргумент — очевидность кода.

### Шаг 14. `Render` вместо `Spawned` для всего, что зависит от `InputAuthority`

`Assets/Game/Scripts/GameObject/View/ColorComponent.cs`

```csharp
        public override void Spawned()
        {
            this.UpdateColor();
        }

        public override void Render()
        {
            this.UpdateColor();
        }

        private void UpdateColor()
        {
            _renderer.material = this.HasInputAuthority ? _blue : _red;
        }
```

`Assets/Game/Scripts/GameObject/Utils/PlayerNameConstraint.cs`

```csharp
        private void OnValidate()
        {
            _name = this.name;
        }

        public override void Render()
        {
            this.name = $"{_name} [{this.Object.InputAuthority.AsIndex}]";
        }
```

**Почему именно так.** `InputAuthority` стал динамическим: он снимается, переназначается при реконнекте и обнуляется при миграции. Любое значение, вычисленное один раз в `Spawned()`, после переназначения станет неверным — чужой персонаж останется «моего» цвета. `Render()` вызывается каждый кадр, поэтому цвет и имя всегда соответствуют текущему владельцу. `PlayerNameConstraint` — ещё и дешёвый диагностический инструмент: в иерархии видно `Character [2]`, то есть кто чем владеет **прямо сейчас**; незаменимо при отладке миграции, и именно поэтому он стоит и на `Player.prefab`, и на `Character.prefab`.

### Шаг 15. `Despawn` как реплицируемое состояние

`Assets/Game/Scripts/GameObject/Content/Coin.cs`

```csharp
        [Networked, UnitySerializeField]
        private bool despawned { get; set; }

        public override void FixedUpdateNetwork()
        {
            if (!this.despawned)
                this.ProcessCollision();

            if (this.despawned)
                this.Runner.Despawn(this.Object);
        }

        private void ProcessCollision()
        {
            int count = _collisionReceiver.GetColliders(out IReadOnlyList<Collider> buffer);
            for (int i = 0; i < count; i++)
            {
                Collider collider = buffer[i];
                if (this.ProcessCollision(collider))
                {
                    this.despawned = true;
                    return;
                }
            }
        }

        public override void Render()
        {
            this.gameObject.SetActive(!this.despawned);
        }
```

**Почему именно так.** Факт удаления вынесен в `[Networked]`-состояние, а не оставлен локальным решением. Сбор монеты не делает `Despawn` немедленно посреди обхода коллайдеров — он ставит флаг и выходит из цикла; `Despawn` происходит отдельной проверкой в начале следующей фазы. Это даёт три вещи: корректную ресимуляцию (флаг откатится вместе с состоянием, если тик пересчитается), синхронное гашение визуала у всех пиров по тику (через `Render`, не дожидаясь despawn-сообщения) и перенос через миграцию — `CopyStateFrom` утащит `despawned`, поэтому ещё живая монета не «соберётся» заново. `UnitySerializeField` рядом с `[Networked]` даёт возможность видеть и править значение в инспекторе при отладке.

Детерминированность самого сбора обеспечивает `CollisionReceiver`, который делает запрос к физике **в `BeforeTick`**, а не в Unity-овском `OnTriggerEnter`.

`Assets/Game/Scripts/GameObject/Core/CollisionReceiver.cs`

```csharp
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
```

Коллизия становится функцией состояния на тике, а не событием физического движка, — значит она воспроизводима при ресимуляции.

---

## Fusion API этой главы

| API | Что делает | Где вызывается | Подводный камень |
|---|---|---|---|
| `StartGameArgs { GameMode, SessionName, SceneManager, Scene, PlayerCount, ConnectionToken, HostMigrationToken, HostMigrationResume }` | Единая структура параметров сессии | `GameFacade.StartGame`, `OnHostMigration` | `ConnectionToken` — произвольные байты; при миграции берите `GameMode` из `hostMigrationToken.GameMode`, а не из инспектора |
| `NetworkRunner.StartGame(args) -> Task<StartGameResult>` | Асинхронный старт/возобновление | там же | `async void` не даёт исключению всплыть; при `!result.Ok` обнуляйте `_runner` |
| `NetworkRunner.AddCallbacks(params INetworkRunnerCallbacks[])` | Ручная подписка на колбэки | `GameFacade.StartGame` | Ранер — префаб, авто-поиск не сработает. В `OnHostMigration` вызов **забыт** — вторая миграция не обработается |
| `NetworkRunner.ProvideInput` | Локальный пир собирает и отправляет инпут | `StartGame`, `OnHostMigration` | Выставлять на **каждом** новом ранере; забыть = игрок в игре, но без управления |
| `OnConnectRequest(runner, request, byte[] token)` | Серверный хук авторизации | `GameFacade_Connection.cs` | Синхронный и **повторный**, пока отвечаете `Waiting()`. Вердикт кэшируйте по токену |
| `ConnectRequest.Accept()/Refuse()/Waiting()`, `OnConnectionRequestReply` | Трёхзначный ответ | там же | Enum Fusion годится как собственный тип состояния — свой не нужен |
| `OnConnectedToServer` / `OnConnectFailed(..., NetConnectFailedReason)` | Клиентский результат подключения | `GameFacade_Connection.cs` | `NetConnectFailedReason` различает «отказал» и «не достучались» — это разный текст в UI |
| `OnDisconnectedFromServer(runner, NetDisconnectReason)` | Клиента отключили | `GameFacade_Disconnect.cs` | Это уведомление, не уборка: нужен явный `Shutdown`, иначе следующий `StartGame` поверх мусора |
| `NetworkRunner.Disconnect(PlayerRef)` | Серверный кик | `DisconnectPlayer(int)` | `PlayerRef.FromIndex` — удобно дёргать из инспектора для теста реконнекта |
| `NetworkRunner.Shutdown(bool destroyGameObject, ShutdownReason)` | Гашение ранера | три разных вызова | `ShutdownReason.HostMigration` запускает миграцию в облаке вместо закрытия комнаты (**вывод**) |
| `OnHostMigration(runner, HostMigrationToken)` | Прилетает **каждому** пиру | `GameFacade_HostMigration.cs` | `await runner.Shutdown()` строго **до** `Instantiate`, иначе два ранера |
| `StartGameArgs.HostMigrationResume = Action<NetworkRunner>` | Колбэк возобновления на новом хосте | там же | Единственное место, где доступен снапшот (**вывод по структуре API**) |
| `NetworkRunner.PushHostMigrationSnapshot()` | Отправить облаку снапшот | после `StartGame`; в `JoinPlayer` | Автопуш (`EnableAutoUpdate: true, UpdateDelay: 10`) может опоздать — ручной вызов нужен поверх него |
| `GetResumeSnapshotNetworkObjects()` | Объекты из снапшота | `MigratePrefabObjects` | Фильтры `IsValid` и `IsPrefab` обязательны, иначе `Spawn` упадёт |
| `GetResumeSnapshotNetworkSceneObjects()` | Пары «новый объект сцены ↔ старый хедер» | `MigrateSceneObjects` | Сопоставление делает Fusion; деконструкция кортежа в `foreach` читаемее `pair.Item1` |
| `NetworkObjectTypeId { IsValid, IsPrefab, IsSceneObject }` | Разделяет две стратегии восстановления | `GameFacade_HostMigration.cs` | `IsSceneObject` нужен третий раз — для отстрела «воскресших» объектов |
| `NetworkObject.CopyStateFrom(NetworkObject / NetworkObjectHeaderPtr)` | Копирует `[Networked]`-состояние | обе ветки миграции | Копирует **только** `[Networked]`; обычные поля и словари теряются |
| `NetworkRunner.Spawn(proto, pos, rot, PlayerRef, onBeforeSpawned)` | Пересоздание по прототипу | `MigratePrefabObjects` | `CopyStateFrom` — строго в `onBeforeSpawned`, иначе тик уйдёт в сеть с дефолтом |
| `NetworkTRSP.Data.Position / .Rotation` | Реплицированный трансформ | `MigratePrefabObjects` | Берите отсюда, не из `transform.position`; объект может не иметь компонента |
| `NetworkRunner.GetAllNetworkObjects()` | Перебор всех объектов | `MigrateSceneObjects`, `AfterMigration` | Дешёвый способ восстановить индекс без реплицируемого реестра |
| `NetworkRunner.GetPlayerConnectionToken(PlayerRef)` | Прочитать токен игрока | `JoinPlayer` | Мост между нестабильным `PlayerRef` и стабильной личностью |
| `SetPlayerObject / GetPlayerObject` | Встроенный реестр «игрок → контекст» | `PlayerContextService` | `SetPlayerObject(player, null)` при выходе обязателен; но тогда `GetPlayerObject` вернёт null на время grace-period |
| `NetworkObject.AssignInputAuthority / RemoveInputAuthority` | Переназначение владельца | реконнект | Главный инструмент реконнекта: объекты не пересоздаются |
| `NetworkBehaviour.ReplicateToAll(bool) / ReplicateTo(PlayerRef, bool)` | Фильтрация репликации одного behaviour | `JoinPlayer` | Приватные данные держите в **отдельном** behaviour, иначе фильтр заденет лишнее |
| `SimulationBehaviour` + `ISpawned` / `IDespawned`, `Runner.GetBehaviour<T>()` | Системы в иерархии ранера | `PlayerCharactersController`, `CameraFollower` | `Spawned()` — правильный момент для подписок; в `Despawned` проверяйте ссылку |
| `IPlayerJoined` / `IPlayerLeft` | Интерфейсные колбэки входа/выхода | `PlayerContextService` | Счётчик обновляется у всех, логика спавна — только под `IsServer` |
| `IBeforeTick.BeforeTick()`, `Runner.GetPhysicsScene()` | Детерминированный опрос физики | `CollisionReceiver`, `PlayerInput` | Замена `OnTrigger*`: воспроизводится при ресимуляции |
| `Tick`, `Runner.Tick`, `Runner.DeltaTime` | Время симуляции | grace-period | Считайте в тиках, не в `Time.time`: симуляция ресимулируется |
| `[Networked, UnitySerializeField]` | Реплицируемое поле, видимое в инспекторе | `Coin.despawned` | Удобно для отладки; значение всё равно приходит из сети |
| `NetworkEvents` | UnityEvent-обёртка над колбэками | `InputAccumulator` | Ссылку держите `[SerializeField]` внутри префаба, не `FindObjectOfType` |

---

## Настройки, без которых не работает

**`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`** — ключевые значения и их роль:

- `HostMigration.EnableAutoUpdate: true`, `UpdateDelay: 10` — без автопуша облако не будет держать актуальный снапшот, и миграция восстановит мир в состоянии последнего ручного `Push`. В разделе 08 флаг был `false`; его включение — изменение именно этой главы.
- `NullChecksForNetworkedProperties: true` — делает `if (!character)` на `[Networked] NetworkObject` корректной проверкой.
- `Network.ConnectionTimeout: 10.0` — совпадает с `TIMEOUT_SECONDS = 10f`. Держите grace-period не меньше таймаута соединения, иначе контекст умрёт раньше, чем Photon признает разрыв.
- `Simulation.ReplicationFeatures: 1` — interest management выключен (в 08 было `3`). Парный шаг к удалению AoI-кода.
- `LagCompensation.Enabled: false`, `Simulation.PlayerCount: 10`, `TickRateSelection.Client: 64`, `PeerMode: 0` (single peer — значит два инстанса гоняются через ParrelSync, а не через multi-peer), `InvokeRenderInBatchMode: true`, `UseSerializableDictionary: true`, `AssembliesToWeave` включает `Assembly-CSharp`.
- Внимание: `Simulation.PlayerCount: 10` — это потолок конфига, а `StartGameArgs.PlayerCount = 4` из инспектора — лимит конкретной комнаты.

**`ProjectSettings/EditorBuildSettings.asset`**: индекс 0 — `Assets/Game/Scenes/Game.unity`, индекс 1 — `Bootstrap.unity`. Поэтому `_gameSceneIndex: 0` и `SceneRef.FromIndex(0)` грузят игровую сцену. Запускать нужно `Bootstrap`.

**`Bootstrap.unity`**: ровно один объект `<GameFacade>` с тремя компонентами — `NetworkSceneManagerDefault` (`IsSceneTakeOverEnabled: 0`), `GameFacade` (`_gameMode: 6`, `_playerCount: 4`, `_sessionName: SampleSession`, `_gameSceneIndex: 0`, `_autoRun: 1`), `ConnectionTokenService`. `IsSceneTakeOverEnabled: 0` правильно: в 08 на `[Runner]` стояло `1`, потому что ранер жил в уже открытой игровой сцене; теперь сцена грузится по `SceneRef`, перехватывать нечего.

**`Assets/Game/Prefabs/[Runner].prefab`**: корень `[Runner]` несёт `NetworkRunner` + `NetworkEvents`; дети — `<Coins>` (`CoinSpawner`, **`m_Enabled: 0`**, `_areaMin: -25,-25`, `_areaMax: 25,25`, `_spawnInterval: 2`), `<Input>` (`InputAccumulator`, `_networkEvents` → `NetworkEvents` корня), `<Camera>` (`CameraFollower`, `_offset: 0,40,0`), `<Players>` (`PlayerContextService` + `PlayerCharactersController`). Порядок компонентов не важен, важна **иерархия**: `GetBehaviour<T>()` и `GetComponentsInChildren<IHostMigrationHandler>()` работают только из-за неё.

**`Player.prefab`**: `m_TagString: Player`, компоненты `NetworkObject`, `PlayerInput`, `MoveController`, `MoneyStorage`, `CharacterProvider`, `PlayerNameConstraint`, `ConnectionTokenComponent`. **`NetworkTransform` отсутствует** — контексту он не нужен. **`Character.prefab`**: `m_TagString: Character`, на корне `NetworkObject`, `NetworkTransform`, `RotationComponent`, `MovementComponent`, `PlayerNameConstraint`; `ColorComponent` — на дочернем `<Visual>`.

**`Game.unity`**: под `[World]` лежат четыре сценовых экземпляра `Coin` и пара тестовых сценовых `NetworkObject` без скриптов — `PlayerContext` с `NestedObjects: [Character]` и вложенный `Character`. Это материал для ветки `MigrateSceneObjects`, включая случай вложенного `NetworkObject`. Ни одной ссылки на `NetworkRunner` в сцене больше нет.

**`ProjectSettings/TagManager.asset`** объявляет только `Character`, `Bullet`, `Coin`. Тега `Player` там нет — он работает потому, что входит во встроенные теги Unity. Заводить руками не нужно, но знать об этом надо, иначе приём «тег как способ восстановить индекс» выглядит так, будто тег добавляли в проект. Заодно: `GameObjectTags.Coin` объявлен, а `Coin.prefab` имеет `m_TagString: Untagged` — константа не используется.

**Мусор, который введёт в заблуждение**: в `[Runner].prefab` у `PlayerContextService` сериализовано `AFK_TIME: 20`. В финальном коде такого поля нет (там `const float TIMEOUT_SECONDS = 10f`) — это остаток от `[Teacher]`-версии с `[SerializeField] float AFK_TIME`. Реальный grace-period — **10 секунд**, а не 20, как показывает инспектор.

---

## Антипаттерны и как их обходит этот код

1. **`NetworkRunner` в сцене.** Нельзя пересоздать, а `Shutdown(destroyGameObject: true)` уничтожает навсегда. → Ранер в префабе + `Instantiate`.
2. **Системы в сцене, а не в иерархии ранера.** Их жизненный цикл расходится с сессией, а поиск через `FindObjectOfType` даёт гонки: в 08 `InputAccumulator.Awake` искал `NetworkEvents` через `FindObjectOfType` и ломался бы, как только ранер стал создаваться позже и многократно. → Системы — дети ранера, ссылки — `[SerializeField]` внутри префаба.
3. **Идентификация игрока по `PlayerRef`.** Ломает реконнект и миграцию. → `ConnectionToken` в `[Networked] int` на контексте.
4. **Блокирующая валидация в `OnConnectRequest`.** Фриз симуляции хоста. → `Waiting()` + кэш вердиктов по токену.
5. **Запуск проверки на каждом вызове колбэка.** Лавина запросов к бэкенду. → `Dictionary.TryAdd` как «я тут впервые?».
6. **Синхронная заглушка бэкенда.** Если `ValidateToken` возвращает `bool` сразу, вся архитектура подключения получится синхронной и не переживёт реальный HTTP. → `ConnectionTokenService` с `Task.Delay(1000)`; именно эта задержка делает паттерн `Waiting` необходимым, а не декоративным.
7. **Немедленное удаление при выходе.** Гарантированная потеря прогресса при любом моргании сети. → Grace-period в тиках.
8. **Удаление из словаря внутри `foreach` по нему же.** → `.ToArray()`.
9. **`Time.time` для таймаутов в симуляции.** Ломается при ресимуляции. → Арифметика по `Tick`.
10. **Персонаж вложен в объект игрока.** Смерть персонажа уносит прогресс, реконнект невозможен, ссылка не переживает миграцию. → Два объекта + `[Networked] NetworkObject`.
11. **Вычисление по `InputAuthority` один раз в `Spawned`.** После переназначения значение неверно. → `Render()`.
12. **`Despawn` как локальное решение посреди обхода коллизий.** → `[Networked] bool despawned` + `Despawn` отдельной фазой + гашение визуала в `Render`.
13. **`OnTriggerEnter` для сетевой логики.** Не воспроизводится при ресимуляции. → `OverlapSphere` в `IBeforeTick.BeforeTick`.
14. **Забыть второй цикл в `MigrateSceneObjects`.** Мир частично откатится к начальному состоянию сцены: собранные монеты воскреснут. → `Despawn` всех `IsSceneObject`, которых нет в снапшоте.
15. **Считать, что миграция восстанавливает всё.** Она восстанавливает только `[Networked]`. → `IHostMigrationHandler.AfterMigration`.
16. **Считать после миграции всех подключёнными.** Мёртвые контексты останутся навсегда. → Все контексты в таймаут, спасаются только вернувшиеся.
17. **`if (obj != null)` для Unity-объектов.** После `Shutdown(destroyGameObject: true)` ссылка не null, но объект мёртв. → `if (obj)`.

**Дефекты, которые есть в самом этом коде — не копируйте:**

- Нет `AddCallbacks(this)` на новом ранере в `OnHostMigration` → вторая миграция подряд не обработается.
- `GameMode = _gameMode` при миграции вместо `hostMigrationToken.GameMode`.
- `_pendingConnections` никогда не чистится (см. шаг 3).
- `Shutdown()` не обнуляет `_runner`; `OnShutdown` — пустая заглушка.
- **NRE в `Coin.ProcessCollision`:** защита стоит только на `player.IsNone`, а `playerService.GetContext(player)` — это `Runner.GetPlayerObject(player)`, который `LeftPlayer` обнуляет. При этом персонаж на время grace-period `InputAuthority` **сохраняет** (`RemoveInputAuthority` в `ProcessLeft` закомментирован, а `LeftPlayer` персонажа не трогает). Брошенный персонаж, стоящий на монете, даёт `IsNone == false`, `playerContext == null` и `NullReferenceException` в `FixedUpdateNetwork` хоста. `[Teacher]`-версия зовёт `context.RemoveInputAuthority()`, но на контексте, а не на персонаже, — дыру это не закрывает. Проверяйте `playerContext` на null, а не только `player.IsNone`.
- `Coin.ProcessCollision` зовёт `Runner.GetBehaviour<PlayerContextService>()` **каждый тик** — кэшируйте в `Spawned`, как это делают `CameraFollower` и `PlayerCharactersController`.
- `CoinPatrolComponent.FixedUpdateNetwork` использует `Random.Range` — недетерминированно при ресимуляции.
- `ConnectionTokenService.GetToken()` возвращает **один и тот же массив по ссылке**; любой вызывающий может испортить токен сервиса. Возвращайте копию. Обёртка `Task.Run(async () => await Task.Delay(...))` избыточна: `Task.Delay` и так не блокирует, `Task.Run` лишь уводит продолжение в пул потоков.
- `MoneyStorage` в 09 **потерял** `ReplicateToAll(false)`/`ReplicateTo` (в 08 они там были): деньги теперь реплицируются всем. Приватность денег разменяна на демонстрацию приватности токена — в своём проекте оставьте фильтр на обоих.
- `CameraFollower` держит мёртвое поле `_camera` и зовёт `Camera.main!` каждый кадр из `Render`. Кеширование убрано осознанно (на момент `Spawned()` игровая сцена ещё не загружена и `Camera.main` вернул бы null), но поле стоило удалить, а камеру кешировать лениво.
- `ParentTransformConstraint.cs` — мёртвый код, ни один префаб и ни одна сцена на него не ссылаются.

---

## Чеклист: как применить в своём проекте

1. Создай сцену `Bootstrap` с единственным объектом: фасад сессии + `NetworkSceneManagerDefault` (`IsSceneTakeOverEnabled: 0`) + сервис токенов. В `Awake` фасада — `DontDestroyOnLoad`. Поставь `Bootstrap` сценой запуска, игровую сцену адресуй через `SceneRef.FromIndex`.
2. Вынеси `NetworkRunner` в префаб, а все игровые системы сделай его **детьми**. Никаких `FindObjectOfType` — ссылки внутри префаба через `[SerializeField]`, поиск систем через `Runner.GetBehaviour<T>()` в `ISpawned.Spawned()`.
3. Реализуй `INetworkRunnerCallbacks` одним `sealed partial class`, разложенным по файлам-ответственностям. Пустые колбэки — `public`, в отдельном `_Unused.cs`; боевые — явной реализацией интерфейса. Наружу публикуй C#-события, а не колбэки Fusion.
4. Сделай сервис токенов с **асинхронным** API (`Task<byte[]> GetToken()`, `Task<bool> ValidateToken(byte[])`) с самого начала, даже если это заглушка. Возвращай копию массива.
5. В `StartGame`: получи токен → `Instantiate(prefab)` → `AddCallbacks(this)` → `_runner.name = prefab.name` → `ProvideInput = true` → `StartGame(args)` с `ConnectionToken`. При `!result.Ok` обнули ссылку. Повтори **весь** этот порядок в обработчике миграции.
6. В `OnConnectRequest` держи `Dictionary<int, OnConnectionRequestReply>`; `TryAdd(token, Waiting)` запускает проверку, `switch` по текущему значению отвечает `Accept/Refuse/Waiting`; на терминальных ветках **удаляй запись из словаря**.
7. Раздели контекст игрока и персонажа на два `NetworkObject`. Связь — `[Networked] NetworkObject`. Токен — в отдельном `NetworkBehaviour` с `ReplicateToAll(false)` + `ReplicateTo(owner, true)`. То же сделай для любых приватных данных (деньги, инвентарь).
8. Поставь тег на префаб контекста и добавь константу в свой `GameObjectTags` — после миграции реестр восстанавливается обходом `GetAllNetworkObjects()` по тегу.
9. В `PlayerJoined` (только под `IsServer`) ищи контекст по `GetPlayerConnectionToken`: нашёлся → `AssignInputAuthority`, не нашёлся → `Spawn` + запись токена. Затем `_timeoutContexts.Remove`, `SetPlayerObject`, событие с флагом `isNew`, `PushHostMigrationSnapshot()`.
10. В `PlayerLeft` — только `SetPlayerObject(player, null)` и дедлайн в тиках. Уборку делай в `FixedUpdateNetwork` под `IsServer`, по копии словаря, в порядке: убрать из реестров → событие → `Despawn`.
11. Для миграции: `Shutdown(true, ShutdownReason.HostMigration)` у уходящего хоста; в `OnHostMigration` — `await runner.Shutdown()` **до** создания нового ранера, затем `StartGame` с `HostMigrationToken = token`, `GameMode = token.GameMode`, `HostMigrationResume = <метод>`, `AddCallbacks(this)`, и `PushHostMigrationSnapshot()` при успехе.
12. В `HostMigrationResume` сделай ровно три шага: префабные объекты (`Spawn` + `CopyStateFrom` в `onBeforeSpawned`, позиция из `NetworkTRSP.Data`, фильтры `IsValid`/`IsPrefab`), сценовые (`CopyStateFrom(headerPtr)` + `Despawn` всех `IsSceneObject` не из снапшота), хуки (`GetComponentsInChildren<IHostMigrationHandler>()`).
13. Введи `IHostMigrationHandler` и реализуй его в каждой системе, у которой есть **ненетворкнутое** состояние: словари, индексы, кэши. Восстанавливай его из самих объектов, а не из реплицированного реестра.
14. После миграции считай всех игроков отключёнными: ставь каждый восстановленный контекст в таймаут. Вернувшиеся спасутся сами.
15. Перенеси в `Render()` всё, что зависит от `InputAuthority` (цвет, имя, UI владельца). Добавь защитные `if (!obj) return;` везде, где `[Networked]`-ссылка может быть пустой, и проверяй на null результат `GetPlayerObject` — на время grace-period он null.
16. Удаление сетевых объектов делай двухфазным: `[Networked] bool despawned` → гашение визуала в `Render` → `Despawn` отдельной проверкой. Физику опрашивай в `IBeforeTick`, не через `OnTrigger*`.
17. Поставь `HostMigration.EnableAutoUpdate: true`, сверь grace-period с `Network.ConnectionTimeout`, выстави `[Button]` на `StartGame`, `Shutdown`, `DisconnectPlayer(int)`, `Quit()`, `[ShowInInspector]` на реестрах и флаг `_autoRun` — миграцию и реконнект иначе не оттестировать.

---

## Что изменилось относительно главы 08

**Появилось:**

- `Assets/Game/Scenes/Bootstrap.unity` — новая точка входа.
- `Assets/Game/Prefabs/[Runner].prefab` — иерархия «ранер + системы-дети» переехала из сцены в префаб. Важно: сама иерархия существовала и в 08 (`<Coins>`, `<Input>`, `<Camera>`, `<Players>` уже были детьми объекта `[Runner]` в `Game.unity`) — раздел 09 (а) вынес её в префаб и (б) снял с объекта ранера точку входа и `NetworkSceneManagerDefault`, переселив их в `Bootstrap`.
- `Assets/Game/Prefabs/Character.prefab` — персонаж стал самостоятельным префабом.
- `Assets/Game/Scripts/App/ConnectionTokenService.cs`, `GameFacade_Connection.cs`, `GameFacade_Disconnect.cs`, `GameFacade_HostMigration.cs`, `GameFacade_Unused.cs`.
- `Assets/Game/Scripts/Player/ConnectionTokenComponent.cs`, `System/HostMigration/IHostMigrationHandler.cs`.
- В `Game.unity` — четыре сценовых `Coin` под `[World]` и тестовые сценовые `PlayerContext` (+ вложенный `Character`); в 08 там не было ни одной монеты.
- `Assets/[Teacher]/` — альтернативные (закомментированные) реализации: `GameFacade_Connection.cs`, `GameFacade_HostMigration.cs`, `GameFacade_Unused.cs`, `IHostMigrationHandler.cs` (там интерфейсный метод называется `PostMigration()`), `PCS.cs`.

**Переименовано с сохранением meta-guid** — поэтому ссылки в сценах и префабах выжили; это отдельный приём: переименовывайте файл и класс, а не создавайте новый файл:

- `EntryPoint.cs` → `App/GameFacade.cs` (guid `24d57f79d65548159368759595d42745`);
- `System/Players/PlayerContextManager.cs` → `PlayerContextService.cs` (guid `d485fdb272f1451884e78e560dd8737b`);
- `System/Players/PlayerInitializer.cs` → `PlayerCharactersController.cs` (guid `fb6cffb3e11c4926a1811d2fec655595`);
- `SpawnPointService.cs` → `SpawnPointProvider.cs` (guid `5fc31548240b4e2d9666c2b848a0c664`; код не идентичен — переименован класс, добавлены `using System;` и `using Random = UnityEngine.Random;`).

Содержательно `EntryPoint` был на порядок проще: `GameMode.Single`, уже лежащий в сцене ранер, `Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)`, никаких токенов и колбэков.

**Удалено:** `System/AreaOfInterest/AreaOfInterestIniitalizer.cs` вместе с выключением interest management в конфиге (`ReplicationFeatures: 3 → 1`) — парный шаг, чтобы освободить главу от шума AoI.

**Изменено:**

- `CharacterProvider`: `Fusion.Behaviour` + `[SerializeField] GameObject _character` → `NetworkBehaviour` + `[Networked] NetworkObject Character`.
- `Player.prefab`: потерял вложенный `Character`, получил тег `Player` и `ConnectionTokenComponent`.
- `InputAccumulator`: `FindObjectOfType<NetworkEvents>()` в `Awake` → `[SerializeField] NetworkEvents`.
- `CameraFollower`: снято кеширование `_camera = Camera.main` в `Spawned()` (поле осталось мёртвым), в `Render` теперь `Camera.main!.transform.position`, плюс защита `if (!character) return;`. Причина: ранер инстанцируется в `Bootstrap` **до** загрузки `Game.unity`, поэтому на момент `Spawned()` камеры игровой сцены ещё нет и `Camera.main` вернул бы null.
- `MoveController`: добавлена защита `if (!character) return;` (в 08 работал без неё, потому что ссылка была сериализованной и пустой быть не могла).
- `ColorComponent`: выбор материала вынесен в `UpdateColor()` и вызывается из `Spawned` **и** из `Render` (в 08 — только `Spawned`).
- `PlayerNameConstraint`: переехал со `Spawned` на `Render`, формат `$"{_name} [{Object.InputAuthority.AsIndex}]"`.
- `Coin`: немедленный `Runner.Despawn` в цикле коллизий → `[Networked, UnitySerializeField] bool despawned` + `Render`-гашение + защита `player.IsNone`.
- `MoneyStorage`: **убраны** `ReplicateToAll(false)`/`ReplicateTo(InputAuthority, true)` и метод `DebugReplicateTo` — приём перенесён на токен.
- `GameObjectTags`: добавлена константа `Player`.
- `NetworkProjectConfig.fusion`: `LagCompensation.Enabled true → false`; `Simulation.ReplicationFeatures 3 → 1`; `Simulation.PlayerCount 2 → 10`; `TickRateSelection.Client 30 → 64`; `HostMigration.EnableAutoUpdate false → true`.
- `NetworkSceneManagerDefault`: `IsSceneTakeOverEnabled 1 → 0`, компонент переехал с `[Runner]` на `<GameFacade>` в `Bootstrap`.

# 90. Справочник Fusion 2 API, встреченного в курсе

Сгруппировано по задаче. Колонка «гл.» — где этот API разбирается подробно.
Всё ниже реально вызывается в коде учебных проектов (Fusion 2.0.5), а не взято из документации.

---

## 90.1. Запуск и жизненный цикл сессии

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `NetworkRunner.StartGame(StartGameArgs) : Task<StartGameResult>` | поднимает сессию | 01, 09 | **всегда проверяй `result.Ok`** — иначе падение уходит в тишину |
| `StartGameArgs.GameMode = GameMode.AutoHostOrClient` | первый становится хостом, остальные клиентами | 01, 09 | основной режим курса; `Shared` не используется |
| `StartGameArgs.SessionName` | имя комнаты | 01, 09 | один конфиг сессии на всё приложение (гл. 07) |
| `StartGameArgs.Scene = SceneRef.FromIndex(buildIndex)` | какая сцена сетевая | 01 | сцена обязана быть в Build Settings |
| `StartGameArgs.SceneManager = NetworkSceneManagerDefault` | загрузка сцен | 01, 09 | `IsSceneTakeOverEnabled` влияет на владение сценовыми объектами |
| `StartGameArgs.HostMigrationResume : Action<NetworkRunner>` | хук восстановления после миграции | 09 | вызывается **вместо** обычного старта |
| `NetworkRunner.Shutdown(bool destroyGameObject, ShutdownReason)` | корректное закрытие | 09 | без него ранер и его дети утекают между сессиями |
| `NetworkRunner.Disconnect(PlayerRef)` | хост выкидывает игрока | 09 | только на хосте |
| `NetworkRunner.ProvideInput = true` | этот пир отправляет ввод | 01 | забыть = `GetInput()` всегда `false` |
| `NetworkRunner.AddCallbacks(INetworkRunnerCallbacks)` | подписка на колбэки | 09 | альтернатива — `NetworkEvents` |
| `NetworkEvents` (компонент) | UnityEvent-обёртка над `INetworkRunnerCallbacks` | 01, 02, 08 | **предпочитается в курсе**: системная логика живёт на мелких `MonoBehaviour`, а не в god-классе |

## 90.2. Игроки и идентичность

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `PlayerRef`, `.PlayerId`, `PlayerRef.FromIndex(id)`, `PlayerRef.None` | идентификатор игрока в сессии | 01+ | **не стабилен между сессиями** — для стабильной личности нужен `ConnectionToken` |
| `Runner.SetPlayerObject(PlayerRef, NetworkObject)` / `GetPlayerObject(PlayerRef)` | штатный реестр «игрок → его объект» | 01, 02, 07, 08 | используй **вместо** своего `Dictionary<PlayerRef, …>` |
| `IPlayerJoined.PlayerJoined(PlayerRef)` / `IPlayerLeft.PlayerLeft(PlayerRef)` | колбэки на `SimulationBehaviour` | 02, 03 | срабатывают на всех пирах — спавнить только под `Runner.IsServer` |
| `NetworkObject.AssignInputAuthority(PlayerRef)` / `RemoveInputAuthority()` | переназначение владельца ввода | 09 | нужно при реконнекте и миграции хоста |
| `NetworkRunner.GetPlayerConnectionToken(PlayerRef) : byte[]` | токен, который клиент послал при коннекте | 09 | единственный способ узнать «это тот же игрок» |
| `ConnectRequest.Accept()` / `.Refuse()` / `.Waiting()` | решение о допуске | 09 | `Waiting()` даёт асинхронную авторизацию (запрос к бэкенду) |
| `Runner.SessionInfo.MaxPlayers` | лимит комнаты | 07 | — |

## 90.3. Ввод (input)

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `INetworkInput` (struct) | контракт пакета ввода | 01+ | только blittable-поля; держи его маленьким |
| `OnInput(runner, NetworkInput)` → `NetworkInput.Set<T>()` | отдать собранный ввод в тик | 01+ | вызывается **раз на тик**, не на кадр → ввод надо **накапливать** (`InputAccumulator`) |
| `NetworkBehaviour.GetInput(out T input) : bool` | прочитать ввод в FUN | 01+ | `true` только у `InputAuthority` и у хоста; читать **в одном месте** на объект |
| `NetworkButtons.Set(key, bool)` / `WasPressed(previous, key)` | фронт кнопки через тики | 03, 04, 06 | требует `[Networked] NetworkButtons previousButtons`, иначе двойные срабатывания при ресимуляции |

## 90.4. Сетевое состояние

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `[Networked]` property `{ get; set; }` | реплицируемое поле | 01+ | писать **только** под `HasStateAuthority`; это свойство, не поле |
| `[OnChangedRender(nameof(Method))]` | вызвать метод при изменении, в рендер-фазе | 02, 03, 08, 09 | **замена** `[Networked(OnChanged=)]` из Fusion 1 |
| `[Networked, Capacity(N)] NetworkArray<T>` | реплицируемый массив фиксированной ёмкости | 04, 05, 06 | ёмкость — константа компиляции; элементы — `INetworkStruct` |
| `NetworkArray<T>.Set(int, T)` / `.GetRef(int) : ref T` | запись / доступ по ссылке | 04, 05 | `GetRef` пишет прямо в сетевую память — быстрее, но только под авторитетом |
| `NetworkArrayReadOnly<T>` | read-only проекция | 05 | то, что отдают снапшот-ридеры |
| `INetworkStruct` | элемент сетевого контейнера | 04, 05, 06 | — |
| `INetworkStruct` + `unsafe fixed byte customData[32]` | union-payload внутри одной структуры | 05 | полиморфизм без наследования; требует Allow unsafe code |
| `NetworkString<_16>` | реплицируемая строка фикс. длины | 08 | дороже, чем кажется; не для частых изменений |
| `Vector3Compressed` | сжатый вектор | 06 | точность ниже — не для точных позиций |
| `NetworkBehaviour.StateBufferIsValid` | можно ли уже читать стейт | 07, 09 | чтение до валидности — типовой краш |
| `[Networked, UnityNonSerialized]` / `[UnitySerializeField]` | показывать/скрывать в инспекторе | 06, 08 | на репликацию не влияет |

## 90.5. Спавн, префабы, пулинг

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `Runner.Spawn(prefab, pos, rot, inputAuthority, onBeforeSpawned)` | создать сетевой объект | 01+ | только под `Runner.IsServer`; инициализировать `[Networked]` **в `onBeforeSpawned`**, не после |
| `Runner.Despawn(NetworkObject)` | удалить | 01+ | только под авторитетом; из FUN — через реплицируемый флаг, не напрямую |
| `NetworkPrefabRef` | сериализуемая ссылка на префаб | 02+ | — |
| `NetworkObjectProviderDefault` → `InstantiatePrefab` / `DestroyPrefabInstance` (override) | подменить создание на пул | 02, 03, 05, 06 | **пулинг прозрачен для игровой логики**: она по-прежнему зовёт `Spawn`/`Despawn` |
| `NetworkObject.NestedObjects` | вложенные сетевые объекты | 04, 09 | `Player` как корень, `Character` внутри — состояние игрока живёт дольше персонажа |
| `NetworkObject.IsProxy` | у этого пира нет ни одного авторитета | 03+ | «я только наблюдатель» |
| `Runner.TryFindObject(NetworkId, out NetworkObject)` | найти по id | 09 | id реплицируем, ссылка — нет |
| `NetworkObject.CopyStateFrom(…)` | перенести состояние | 09 | ключ к восстановлению мира при миграции хоста |
| `NetworkObject.NetworkTypeId { IsValid, IsPrefab, IsSceneObject }` | откуда объект | 09 | при миграции префабы и сценовые объекты восстанавливаются по-разному |

## 90.6. Тики и время

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `FixedUpdateNetwork()` | тик симуляции | 01+ | может вызваться **повторно** для того же тика → код обязан быть идемпотентным |
| `Render()` | кадр | 02+ | здесь **нельзя** писать `[Networked]` |
| `Runner.DeltaTime` | шаг тика | 01+ | в FUN используй **только его**, не `Time.deltaTime` |
| `Runner.Tick` (`Fusion.Tick`) | номер тика | 04, 05, 06 | основа «времени» для детерминированных траекторий |
| `Runner.IsForward` | это первый (не ре-) прогон тика | 06 | ворота для всех неидемпотентных действий |
| `Runner.LocalRenderTime` / `Runner.RemoteRenderTime` | два таймфрейма рендера | 04, 05 | свои объекты рисуй по local, чужие — по remote; перепутать = рассинхрон визуала |
| `TickTimer.CreateFromSeconds(Runner, sec)` / `.Expired(Runner)` / `.ExpiredOrNotRunning(Runner)` | сетевой таймер | 02, 03, 06 | «не запущен» и «истёк» — разные состояния; `ExpiredOrNotRunning` объединяет их |
| `IBeforeTick.BeforeTick()` | хук перед тиком | 05 | место для подготовки буферов |

## 90.7. Снапшоты и интерполяция

| API | Что делает | гл. |
|---|---|---|
| `TryGetSnapshotsBuffers(out from, out to, out alpha)` | два соседних снапшота + коэффициент | 05 |
| `GetArrayReader<T>(string property) : ArrayReader<T>` | читать `NetworkArray` из конкретного снапшота | 05 |
| `NetworkTRSP` (`.Data.Position`, `.Data.Rotation`) | базовый класс транспортировки трансформа | 03, 05 |
| `NetworkTransform` (`SyncParent`, `SyncScale`, `DisableSharedModeInterpolation`, `_autoAOIOverride`) | готовая репликация трансформа | 03, 05, 08 |
| `InterpolationTarget` (поле `NetworkTRSP`-компонентов) | дочерний визуал, интерполируемый отдельно от сетевого корня | 02, 03 |
| `NetworkRigidbody3D` (`Fusion.Addons.Physics`) + `_interpolationTarget`, `RenderThresholds` | физика под управлением Fusion | 03 |

## 90.8. Физика и запросы к миру

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `Runner.GetPhysicsScene() : PhysicsScene` | сетевая физическая сцена | 02, 03, 08 | **никогда** не используй `Physics.*` в FUN — это другая сцена |
| `PhysicsScene.OverlapSphere(center, radius, buffer, layerMask, QueryTriggerInteraction)` | запрос перекрытий | 02, 03, 08 | буфер переиспользуй (zero-alloc); один запрос на тик |
| `Runner.SetIsSimulated(NetworkObject, bool)` | включить симуляцию объекта на клиенте | 03 | работает **в паре** с `ClientPhysicsSimulation = SimulateAlways` |
| `NetworkProjectConfig`: Physics Authority = Fusion, Timing = `FixedUpdateNetwork` | Fusion сам зовёт `Physics.Simulate` | 03 | без этого Rigidbody не детерминирован |

## 90.9. Лаг-компенсация

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `Runner.LagCompensation` (это `Fusion.HitboxManager`) | перемотка истории хитбоксов | 06 | работает **только** по `Hitbox`/`HitboxRoot`, не по обычным коллайдерам |
| `LagCompensation.Raycast(…)` / `.OverlapSphere(…)` | запрос в прошлом тайм-фрейме стрелка | 06 | — |
| `HitOptions.IgnoreInputAuthority` | не попадать в себя | 06 | — |
| `HitOptions.SubtickAccuracy` | точность между тиками | 06 | дороже, но честнее для быстрых цели/снаряда |
| `LagCompensatedHit` (`hit.Hitbox`, `hit.Point`), `Hitbox.Root → HitboxRoot` | результат | 06 | от `Hitbox` к игровому компоненту идёшь через `Root` |
| `preProcessRoots` (параметр запроса) | отсечь кандидатов до расчёта | 06 | фильтруй **здесь**, а не постфильтрацией результатов |

## 90.10. RPC

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `[Rpc(RpcSources, RpcTargets)]` | объявление RPC | 07 | имя метода должно начинаться с `Rpc` (требование weaver) |
| `RpcHostMode.SourceIsServer` / `SourceIsHostPlayer` | кем считается хост как отправитель | 07 | **главная ловушка host mode**: хост одновременно и сервер, и игрок |
| `RpcChannel.Reliable` / `Unreliable` | канал доставки | 07 | косметика → Unreliable, команды → Reliable |
| `RpcInfo info` (последний параметр) | кто вызвал | 07 | единственный способ узнать источник на принимающей стороне |
| `RpcInvokeInfo` (возвращаемое значение) | результат отправки | 07 | **логируй его** — иначе молча проглоченные RPC не отладить |
| `[RpcTarget] PlayerRef player` | адресовать конкретному игроку | 07 | — |
| статический `[Rpc]` на `SimulationBehaviour` | канал без привязки к объекту | 07 | чат, системные события |

## 90.11. Interest Management (Area of Interest)

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `Runner.AddPlayerAreaOfInterest(PlayerRef, Vector3, float radius)` | добавить зону интереса | 08 | это **не подписка**: зоны живут один тик → `Clear` + `Add` каждый тик |
| `Runner.ClearPlayerAreaOfInterest(PlayerRef)` | очистить зоны | 08 | — |
| `Runner.SetAreaOfInterestCellSize(int)` / `SetAreaOfInterestGrid(x,y,z)` | сетка AoI | 08 | крупная ячейка = меньше CPU, больше трафика |
| `NetworkObject.SetPlayerAlwaysInterested(PlayerRef, bool)` | объект всегда реплицируется этому игроку | 08 | для своего персонажа, UI-объектов игрока |
| `ReplicateTo(PlayerRef, bool)` / `ReplicateToAll(bool)` | фильтрация **на уровне компонента** | 08 | самый точный уровень: «инвентарь виден только владельцу» |
| `IInterestEnter.InterestEnter(PlayerRef)` / `IInterestExit.InterestExit(PlayerRef)` | колбэки входа/выхода | 08 | фильтруй `player == Runner.LocalPlayer`, иначе сработает на чужих |
| `Runner.IsInterestedIn(NetworkObject, PlayerRef) : bool?` | проверка | 08 | `null` = «не знаю» (AoI выключен) — трактуй явно |
| `NetworkObject.ObjectInterest` (`ObjectInterestModes`) | режим интереса на префабе | 08 | настраивается в префабе, а не кодом |
| `NetworkProjectConfig.Simulation.ReplicationFeatures` | включает AoI вообще | 08 | **без этого весь код AoI молча ничего не делает** |
| `RunnerAOIGizmos` | визуализация зон в редакторе | 08 | обязательный инструмент отладки AoI |

## 90.12. Host Migration

| API | Что делает | гл. |
|---|---|---|
| `OnHostMigration(runner, HostMigrationToken)` | хост упал, вот токен для перезапуска | 09 |
| `Runner.PushHostMigrationSnapshot()` | хост периодически отдаёт снапшот мира | 09 |
| `StartGameArgs.HostMigrationResume` | вход в восстановление | 09 |
| `Runner.GetResumeSnapshotNetworkObjects()` | префабные объекты из снапшота | 09 |
| `Runner.GetResumeSnapshotNetworkSceneObjects()` | сценовые объекты из снапшота | 09 |
| `NetworkObject.CopyStateFrom(…)` | перелить состояние в новый объект | 09 |
| `OnDisconnectedFromServer(runner, NetDisconnectReason)` | причина отключения | 09 |

## 90.13. Служебное

| API | Что делает | гл. | Подводный камень |
|---|---|---|---|
| `SimulationBehaviour` (+ `ISpawned`, `IDespawned`, `IPlayerJoined`, `IPlayerLeft`) | сетевой код **без** сетевого состояния | 02, 03, 05, 07 | — |
| `Runner.GetSingleton<T>() where T : SimulationBehaviour` | доступ к сервису без ссылок | 05, 08 | — |
| `Runner.GetBehaviour<T>()` / `NetworkObject.GetBehaviour<T>()` | найти компонент | 07, 09 | — |
| `Runner.GetAllNetworkObjects()` | перебор всех объектов | 08, 09 | — |
| `AssembliesToWeave` (NetworkProjectConfig) | какие сборки обрабатывает weaver | 01 | **свою сборку забыть = `[Networked]` молча не работает** |
| `ConnectionHandler` (`KeepAliveInBackground`) | удержание соединения в фоне | 15 | — |

## 90.14. Photon Realtime (глава 15, без Fusion)

| API | Что делает |
|---|---|
| `RealtimeClient.ConnectUsingSettings(AppSettings) : bool` | подключение; **проверяй возвращаемое значение, не жди колбэк** |
| `RealtimeClient.Service()` / `DispatchIncomingCommands()` / `SendOutgoingCommands()` | ручной цикл сети — здесь его крутишь ты, а не движок |
| `RealtimeClient.AddCallbackTarget(object)` / `RemoveCallbackTarget(object)` | подписка на события |
| `IOnEventCallback.OnEvent(EventData)` | приём события |
| `RaiseEventArgs { CachingOption, InterestGroup, TargetActors, Receivers }` | адресация события |
| `SendOptions { Reliability, Encrypt, Channel }` | транспорт |
| `EventCaching.AddToRoomCache` + `RoomOptions.CleanupCacheOnLeave` | «состояние» через кэш событий |
| `RealtimeClient.OpChangeGroups(byte[] remove, byte[] add) : bool` | Interest Groups — аналог AoI, но вручную |
| `Room.SetCustomProperties(PhotonHashtable set, PhotonHashtable expected) : bool` | состояние комнаты (CAS через `expected`) |
| `OpJoinRandomOrCreateRoom(JoinRandomRoomArgs, EnterRoomArgs) : bool` | матчмейкинг одним round-trip |
| `RealtimePeer.UseByteArraySlicePoolForEvents` + `ByteArraySlice.Release()` | zero-allocation путь для горячих событий |
| `Player` (`ActorNumber`, `IsLocal`, `NickName`, `CustomProperties`), `Room.Players` | модель участников |
| `ClientState`, `RealtimeClient.InRoom` / `.InLobby` / `.State` | состояние клиента |

---

## 90.15. Чего в курсе НЕТ (и почему это важно знать)

- **`Shared` mode** — курс целиком про Host mode. Правило «только хост пишет состояние» в Shared не действует.
- **Dedicated server** — не рассматривается.
- **Multi-Peer** — мультизапуск решается ParrelSync-ом (отдельные процессы), а не Multi-Peer.
- **Ручной rollback предсказания** — Fusion ресимулирует сам; курс учит *не мешать* ему (идемпотентность), а не писать свой откат.
- **Fusion Chat/Voice** — глава 15 показывает только Realtime-слой, чат там свой на `RaiseEvent`.

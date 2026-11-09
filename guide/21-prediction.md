# Глава 21. Предсказание, ресимуляция и тайминг

> **TL;DR для агента**
>
> 1. В Host/Server Mode клиент **симулирует будущее**: он отправляет ввод на тик, который сервер ещё не считал, а при получении снапшота откатывается и **переигрывает** все тики между снапшотом и своим текущим тиком ([network-simulation-loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop)). Поэтому `FixedUpdateNetwork()` для одного и того же тика вызывается **многократно**.
> 2. Fusion восстанавливает перед переигрыванием только `[Networked]`-состояние; обычные поля класса **не сбрасываются** ([host-mode-basics/3-prediction](https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/3-prediction)). Любой накапливающий счётчик в FUN — баг.
> 3. Всё одноразовое (звук, VFX, спавн визуала, изменение `Animator`) — только под `Runner.IsForward`. **RPC в ресимуляции вызвать нельзя**: «Calling one in a resimulation will fail», результат — `NotInvokableDuringResim` ([rpcs](https://doc.photonengine.com/fusion/current/manual/data-transfer/rpcs)).
> 4. **Predicted spawning в Fusion 2 удалён** и замены нет: «Predicted spawning has been removed and there is no new APi to replaced it» ([migration](https://doc.photonengine.com/fusion/current/getting-started/migration/coming-from-fusion-v1)). Предсказывается движение — но не создание объектов.
> 5. Все явные ручки тайминга (раздел `Time`, input delay, лимит ресимуляций, свободный tick rate 8–256 Гц, Send Priority, Forecast-физика) — это **Fusion 2.1+**. Курс стоит на 2.0.5, там их нет. Первым делом определяй версию SDK по `Assets/Photon/Fusion/release_history.txt`.

---

## Зачем это знать

Курс пользуется предсказанием и ресимуляцией бесплатно: пишешь `FixedUpdateNetwork()` — и «просто работает». Это верно, пока в проекте нет стрельбы, анимации, звука, VFX и физики. Как только они появляются, незнание внутреннего устройства симуляции даёт класс багов, который невозможно отладить «снаружи»: выстрел, засчитанный дважды; звук, сыгравший три раза; дёрганый прокси; скачки CPU ровно у тех игроков, у кого плохая связь.

Главы части I трогают тему точечно: [гл. 01](01-chapter.md) — tick-based `INetworkInput`, [гл. 05](05-chapter.md) — интерполяция снапшотов, [гл. 06](06-chapter.md) — лаг-компенсация. Эта глава собирает **часы, которые крутятся внутри `NetworkRunner`**, в одну модель и даёт правила, которые из этой модели следуют.

---

## 21.1. Конвейер: один кадр Unity изнутри

Fusion строго разделяет **симуляцию** (фиксированный шаг) и **рендер** (частота кадров Unity). Шаг задаётся в `NetworkProjectConfig` → `Simulation` → `Tick Rate` в герцах: «a timestep of 60 is equal to a delta of 1/60th of a second», а сам шаг читается из `NetworkRunner.DeltaTime`. Документация подчёркивает: «The tick rate used for the simulation loop is _DIFFERENT_ from the rendering rate» ([network-simulation-loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop)).

Порядок одного кадра фиксирован ([fusion-update-loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/fusion-update-loop)):

| № | Фаза | Замечание из источника |
|---|---|---|
| 0 | Unity `FixedUpdate()` | «There can be 0, 1 or multiple FixedUpdate steps due to how the Unity player loop works» |
| 1 | колбэки, зарегистрированные на `NetworkRunner` | |
| 2 | **Resimulation Loop** | «only runs in `ServerMode` and `HostMode`» |
| 3 | **Forward Loop** | «runs in **all** modes - including `SharedMode`»; в Shared Mode это «the only loop running the simulation» |
| 4 | Interest Management | |
| 5 | Shutdown Handling | если `Shutdown()` вызван внутри FUN |
| 6 | Unity `Update()` → `Render()` → `LateUpdate()` | «Before `Render()`, all of Unity's `Update()` methods are executed» |

Главное следствие: **«один кадр = один тик» — неверная модель**. «Every Unity Update, the `NetworkRunner` determines how many ticks need to be simulated» исходя из прошедшего времени ([network-runner](https://doc.photonengine.com/fusion/current/manual/network-runner)). За кадр может быть ноль, один или много вызовов FUN — и то же относится к `OnInput`: «One Update frame might contain zero, one, or several OnInput calls» ([player-input](https://doc.photonengine.com/fusion/current/manual/input/player-input)).

Порядок FUN между компонентами по умолчанию **произвольный**: «NetworkBehaviours and SimulationBehaviours are arbitrarily ordered», управляется атрибутами `[OrderedBefore()]`/`[OrderedAfter()]`, в том числе относительно Network Physics ([Fusion 1 fixed-update-network](https://doc.photonengine.com/fusion/v1/manual/network-object/fixed-update-network) — отдельной страницы для Fusion 2 не нашлось).

---

## 21.2. Предсказание: насколько далеко клиент уходит в будущее

В Host/Server Mode истина у сервера, но клиент не ждёт подтверждения — он симулирует вперёд. Дословный пример из документации: серверный тик 100; «Client 1's latency is equal to 4 ticks (2 ticks from server to client + 2 ticks from client to server)», и клиент отправляет ввод на **Tick 102**, то есть «2 ticks into the server's future». Клиент 2 с задержкой 6 тиков уходит на 3 тика ([network-simulation-loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop)).

⚠️ Формула «предсказание = половина RTT в тиках + запас на джиттер» **в документации не сформулирована** — это вывод из арифметики примера. Страница тайминга описывает механизм иначе: клиент «analyzes the spacing of outgoing packets to figure out how early to send», собирая распределение packet delay variation ([time-synchronization](https://doc.photonengine.com/fusion/current/manual/advanced/time-synchronization)). Пользуйся примером как иллюстрацией, не как формулой.

Запас на джиттер — не константа, и в Fusion 2.1 он настраивается перцентилем: «If this value is 3%, the client will use the 97th percentile jitter value». Темп тиков клиента тоже плавающий: «Clients speed up and slow their tick rate as needed over time», чтобы его апдейты приходили до того, как сервер в них упрётся ([network-runner](https://doc.photonengine.com/fusion/current/manual/network-runner)). Именно поэтому `Runner.DeltaTime` стабилен, а число тиков за Unity-кадр — нет.

Важная асимметрия: предсказываются **не все** объекты. В Fusion 2 «`FixedUpdateNetwork` no longer executes on proxies by default»; включить обратно можно `networkRunner.SetIsSimulated(networkObject, true);` — и это **только Client-Server режимы** ([whats-new-2-0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-0)). И ещё одна граница: «the one thing it cannot predict is the input from other players» ([3-prediction](https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/3-prediction)).

---

## 21.3. Ресимуляция: что именно ломается

Когда приходит снапшот, клиент «immediately re-simulates all ticks between the received snapshot and the client's current tick», то есть «simulates the same tick several times as it receives updated snapshots» ([3-prediction](https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/3-prediction)). Дословный пример реконсиляции: при получении подтверждённого тика 100 клиент «will re-apply its input sequentially from 100, 101 and 102 to arrive at a new Tick 103»; дальше источник говорит только «it will be in Tick 104 when it receives the corrected Tick 101» — что именно переигрывается на этом шаге, в тексте не сказано.

Разделение фаз — через `Runner.Simulation`. Семантика (формулировки со страницы Fusion 1; для Fusion 2 построчно не подтверждены, но сама механика подтверждена туториалом Fusion 2):

| Флаг | Значение |
|---|---|
| `IsResimulation` | «the tick currently being simulated has previously been simulated locally» |
| `IsForward` | тик **не** симулировался локально ранее |
| `IsFirstTick` | «the first tick of the resimulation or forward phase of the simulation loop» |
| `IsLastTick` | «the last tick of the resimulation or forward phase» |

Источник: [Fusion 1 fixed-update-network](https://doc.photonengine.com/fusion/v1/manual/network-object/fixed-update-network).

### Что Fusion восстанавливает, а что нет

«Fusion resets it to the proper tick before calling `FixedUpdateNetwork()`» — но «this is not true for non-networked state» ([3-prediction](https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/3-prediction)). Страница Fusion 1 формулирует жёстче: «Regular local non-networked state variables (e.g. class members) will not be reset».

| Операция в FUN | Безопасна при ресимуляции? | Что делать |
|---|---|---|
| запись `[Networked]`-поля | да | штатный путь |
| `_counter++` по локальному полю | **нет** | вынести в `[Networked]` или использовать `TickTimer` |
| `Instantiate` VFX, `PlayOneShot` | **нет** | `if (Runner.IsForward)` |
| `[Rpc]` | **нет**, падает | см. ниже |
| `Runner.Spawn()` | только на `StateAuthority` | спавн не предсказывается вовсе |
| изменение `Animator` | **нет** | `if (Runner.IsForward)` |
| `Random`, `Time.deltaTime`, `DateTime.Now` | **нет** | детерминизм — [гл. 03](03-chapter.md) |

### RPC и ресимуляция — самый частый пропуск

Документация прямая: «RPCs may not be sent in a Resimulation tick», «They represent a definitive action, which may not be altered by resimulation», «Calling one in a resimulation will fail»; при использовании `RpcInvokeInfo` результат содержит `NotInvokableDuringResim` ([rpcs](https://doc.photonengine.com/fusion/current/manual/data-transfer/rpcs)).

Разница версий критична для курса:

| Версия | Доставка RPC |
|---|---|
| **Fusion 2.1+** | «RPCs are delivered in Forward ticks and are never re-delivered in Resimulation ticks» |
| **Fusion 2.0** (версия курса) | «RPCs may be first delivered in Resimulation or Forward ticks»; «They will never be re-delivered in Resimulation ticks». Если на такой RPC надо ответить своим RPC, «then the response RPC must be deferred until a Forward tick» |

То есть на 2.0.5 шаблон «получил RPC → ответил RPC» требует явной отложки до Forward-тика. Про RPC как механизм — [гл. 07](07-chapter.md).

### Animator — документированный пример «что нельзя откатить»

«Unity's `Animator` component cannot be rewound and re-simulated (it is designed to run forward only)», поэтому он «cannot be accurately set to a tick's state» и «cannot be relied on for tick accurate animations». Следствие: «`NetworkMecanimAnimator` does not attempt to resimulate», и компонент «will only sync the `Animator` component from the State Authority to Proxies»; Input Authority «should also apply changes to the Animator on Forward ticks» ([network-mecanim-animator](https://doc.photonengine.com/fusion/current/manual/sync-components/network-mecanim-animator)).

### TickTimer — штатное решение проблемы счётчиков

```csharp
// из документации Photon (host-mode-basics/3-prediction)
_timer = TickTimer.CreateFromSeconds(Runner, lifetime);
```

«Instead of storing the current remaining time, it stores the end-time in ticks», поэтому «the timer does not need to be sync'ed on every tick but just once, when it is created». Создавать его надо в `Init()`, а не в `Spawned()`, потому что «`Spawned()` is called only after a local instance has been created» ([3-prediction](https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/3-prediction)). В Fusion 2 `TickTimer` доступен и в Shared Mode — Shared Mode «now also relies on accurate tick-aligned data between clients» ([whats-new-2-0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-0)).

### Predicted spawning: в Fusion 2 его нет

«Predicted spawning has been removed and there is no new APi to replaced it», а «`NetworkObjectPredictionKey` is no longer needed, remove it from the `Spawn` function» ([migration](https://doc.photonengine.com/fusion/current/getting-started/migration/coming-from-fusion-v1)). Туториал подтверждает практику: «only the `StateAuthority` (host) can spawn `NetworkObjects`», и «unlike the movement the spawn of these objects will only be executed on the host and not predicted on the clients». Любые найденные в сети `IPredictedSpawnBehaviour` / `PredictedSpawnSpawned` — это Fusion 1, не переносить.

Следствие для геймдизайна: мгновенный отклик на выстрел делается **локальным косметическим** объектом (не `NetworkObject`), либо экстраполяцией из `[Networked]`-массива — приём из [гл. 04](04-chapter.md) и [гл. 05](05-chapter.md).

---

## 21.4. Удалённое время: снапшотная интерполяция

Прокси живут в прошлом. Документация: интерполяция «produces smooth rendered results, by always lerping between two previously simulated snapshots» ([network-behaviour](https://doc.photonengine.com/fusion/current/manual/network-behaviour)); цель — «ideally the interpolation is based on the two most recent snapshot available», а смещение адаптивно: «the interpolation algorithm which adapts the offset and buffering required based on current network conditions» ([network-simulation-loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop)). ⚠️ Явного «never extrapolates» в документации нет — это инференция из «always lerping between two previously simulated snapshots».

Что где хранится ([network-buffers](https://doc.photonengine.com/fusion/current/manual/advanced/network-buffers)):

* «Fusion keeps a history of received state snapshots for each `NetworkObject` that it does not have `StateAuthority` over» — то есть история есть у прокси.
* «For each locally simulated object (Whether authoritative or predicted) there's an **additional** current and previous snapshot» — это **дополнительные** буферы, а не замена истории.

API доступа:

```csharp
// из документации Photon (manual/advanced/network-buffers)
if (TryGetSnapshotsBuffers(out var fromBuffer, out var toBuffer, out var alpha))
{
    // fromBuffer.Tick / toBuffer.Tick
}

var interpolator = new NetworkBehaviourBufferInterpolator(this);
Vector3 v = interpolator.Vector3(nameof(SomeVector3Property));
```

Для `ChangeDetector` выбор шкалы обязателен и осознан: `SimulationState` — локальный таймфрейм (и он «only exist for network objects that are included in the simulation»), `SnapshotFrom` / `SnapshotTo` — удалённые from/to тики. Детектор, настроенный не на ту шкалу, — классический источник «изменение не замечено» или «замечено дважды». Про `ChangeDetector` и `[OnChangedRender]` в проекте курса — [гл. 02](02-chapter.md), [гл. 05](05-chapter.md).

**Чего в Fusion 2 больше нет:** `Interpolator<T>` и `NetworkBehaviour.InterpolationDataSource` — это Fusion 1 ([v1/manual/interpolator](https://doc.photonengine.com/fusion/v1/manual/interpolator)); migration-страница называет замену: «We add a new interpolation API to access data points». Там же: Interpolation Targets удалены, трансформ с `NetworkTransform` интерполируется напрямую. ⚠️ Свойств `Runner.LocalRenderTime` / `RemoteRenderTime` / `LocalAlpha` / `RemoteAlpha` / `GetInterpolationData` **в документации Fusion 2 нет** — целевой поиск по этим именам не даёт попаданий. Не использовать их в коде и не советовать.

Мгновенный перенос без лерпа — `NetworkTransform.Teleport()` ([network-transform](https://doc.photonengine.com/fusion/current/manual/network-transform)). Сглаживание ошибки при смене владельца — `NetworkTRSP.StateAuthorityChangeErrorCorrectionDelta`, по умолчанию 0 (выключено), документация предлагает 0.15 как стартовое значение ([network-trsp](https://doc.photonengine.com/fusion/current/manual/sync-components/network-trsp)).

---

## 21.5. Lag compensation — третье время

`Runner.LagCompensation` откатывает хитбоксы в прошлое: Fusion «keeps a history of where hitboxes were previously located» и «knows how far behind each client's view is compared to the current server state», после чего «is able to compensate for lag by raycasting in the past» ([lag-compensation](https://doc.photonengine.com/fusion/current/manual/advanced/lag-compensation)).

⚠️ Утверждение «откатываемый лаг = предсказание клиента + его задержка интерполяции» **первоисточником не подтверждено** — страница не называет компоненты лага и не объясняет, как Fusion измеряет отставание клиента. Правдоподобно, но подавать как факт нельзя.

Подтверждённые настройки и лимиты (все — с той же страницы; **значений по умолчанию страница не даёт ни для одной настройки**):

| Настройка / лимит | Значение |
|---|---|
| `Hitbox Buffer Length In Ms` | «the length buffer in which hitbox data will be stored in milliseconds» |
| `Hitbox Default Capacity` | «the default number of hitbox values stored per snapshot. Has a minimum of 16» |
| `Cached Static Colliders Size` | размер кэша статических коллайдеров (PhysX / Box2D) для запросов по умолчанию |
| лимит узлов | «There is a limit of 31 `Hitbox` nodes per `HitboxRoot`» |
| доступность | «Lag Compensation is only available in `Server Mode` and `Host Mode`» |

Флаги `HitOptions`: `SubtickAccuracy` — запрос интерполированного состояния между двумя тиками «using the exact interpolation factor seen by the player when the input was polled» (потому что «what the player actually sees on screen is usually not at a discrete tick»); `IncludePhysX` — подключает обычные PhysX-коллайдеры, которые **не** лаг-компенсируются и видны в текущем состоянии; `IgnoreInputAuthority` — игнорирует хитбоксы объекта самого стреляющего. Практика курса — [гл. 06](06-chapter.md).

---

## 21.6. Ручки тайминга: что появилось в 2.1

**Весь раздел `Time` — только 2.1:** «Time Settings are available starting with Fusion 2.1» ([time-synchronization](https://doc.photonengine.com/fusion/current/manual/advanced/time-synchronization)). Все три группы документированы **только с опцией `Custom`**; что Fusion делает до переключения на `Custom`, не описано даже качественно. **Значений по умолчанию страница не даёт ни для одной настройки раздела `Time`** — 3% приводится только как пример.

| Группа | Настройка | Что делает |
|---|---|---|
| `Input Receive Buffer Settings = Custom` | `Max Late Inputs` | перцентиль джиттера: «If this value is 3%, the client will use the 97th percentile jitter value» |
| | `Redundant Inputs` | множитель: ввод придёт вовремя даже при потере нескольких подряд идущих пакетов |
| | `Input Receive Buffer Delay Added` | фиксированная добавка к опережению (мс или тики) |
| `Input Delay Settings = Custom` | `Minimum Input Delay` | минимум задержки ввода игрока (мс или тики) |
| | `Clients Can Add Input Delay To Limit Resims` | клиент сам добавляет задержку, чтобы удержаться в лимите |
| | `Client Max Resims Per Frame` | «Caps the number of predicted ticks per frame» — верхняя граница глубины предсказания |
| `State Receive Buffer Settings = Custom` | `Max Late Snapshots` | перцентиль джиттера для задержки интерполяции (3% → 97-й) |
| | `Redundant Snapshots` | множитель: плавность сохраняется при потере N подряд снапшотов |
| | `State Receive Buffer Delay Added` | дополнительная задержка интерполяции |

Зачем это вообще: «Clients perform more resimulations when there is higher latency leading to more CPU usage», и «Input delay can help reduce the amount of resimulations clients have to perform». Input delay «is only supported in `Server Mode` and `Host Mode`» и «is disabled by default» ([whats-new 2.1](https://doc.photonengine.com/fusion/current/getting-started/whats-new), [time-synchronization](https://doc.photonengine.com/fusion/current/manual/advanced/time-synchronization)).

Компромисс прямой: платишь отзывчивостью управления, получаешь стабильный кадровый бюджет. Для соревновательного шутера это обычно неприемлемо; для физической песочницы или мобильного проекта с бюджетным CPU — обычно выгодно.

**Fusion 2.0-эквиваленты** грубее и называются иначе: `TimeSyncConfiguration.MaxLateInputs` и `TimeSyncConfiguration.ExtraSimulationOffset` (в примере документации оба равны 1 — около 1% опозданий и один дополнительный шанс доставки) ([whats-new-2-0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-0)). Без префикса `TimeSyncConfiguration` эти имена не найти.

### Tick rate, send rate, приоритеты

| Параметр | Значение | Источник |
|---|---|---|
| `Tick Rate` | 2.1+: «it can be set to any value between 8 and 256»; раньше — ограниченный набор опций. **Дефолт не документирован** | [network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) |
| `Client/Server Send Rate` | интервал «in the 1-8 range», должен «divide evenly into the chosen tick rate». Дефолт не документирован | то же |
| Shared Mode | «in Shared Mode the following tick & send rate settings cannot exceed `32`!»; Fusion 2.0 жёстко фиксировал 32 и 16, с 2.1 можно менять ниже лимита; переопределяется через dashboard и Webhooks | то же |
| `Input Transfer Mode` | `Redundancy` — «send delta compressed and redundant input, used for most games»; `Latest State` — «only the latest input state». ⚠️ **Документация не называет значение по умолчанию** | то же |
| `Replication Features` | `None` / `Scheduling` / `Scheduling and Interest Management`; `Scheduling` помечен «the default» | то же |
| `Player Count` | «ranging from 1 to 255» | то же |
| `ObjectDataConsistency` | «By default Fusion uses a version of Eventual Consistency called Full Consistency» — все `[Networked]` одного объекта приезжают в одном тике; переключается кодом: `SimulationConfig.ObjectDataConsistency` = `Eventual` | [eventual-consistency](https://doc.photonengine.com/fusion/current/manual/data-transfer/eventual-consistency) |

**Send Priority (2.1):** `NetworkObject.SetPriority(PlayerRef player, int priority)` / `NetworkObject.ClearPriority(PlayerRef player)`. «A priority of `1` means the object is sent each send tick», `2` → `SendRate/2`, `3` → `SendRate/3`. Доступно в Host, Server и Shared Mode, но в Shared «it is only valid to call `SetPriority` with the local `PlayerRef` (`NetworkRunner.LocalPlayer`)» ([send-priority](https://doc.photonengine.com/fusion/current/manual/advanced/send-priority)). Interest Management ([гл. 08](08-chapter.md)) решает «получает ли объект апдейты вообще», Send Priority — «как часто».

**Ловушка `ObjectInactivityThreshold`:** `NetworkProjectConfig.Simulation.ObjectInactivityThreshold`, «in seconds, default `0.251`», в инспекторе отсутствует — задаётся кодом до старта раннера (`NetworkProjectConfig.Global.Simulation.ObjectInactivityThreshold = 0.5f;`). Превышение интервала между апдейтами останавливает интерполяцию и делает движение дёрганым; формула — `ObjectInactivityThreshold = 2 * priority / send rate`, и если результат меньше дефолта, дефолт оставляют. **Но:** страница пишет «available starting with Fusion 2.1.4», а и SDK-страница, и Release Notes 2.1 заканчиваются на **2.1.3 Stable, Build 2390 (Sep 17, 2026)** — никакого 2.1.4 в релизах нет. На 2026-11-01 эта настройка **недоступна ни в одном выпущенном стабильном SDK**: документация опережает релиз. До 2.1.4 документация советует другое: приоритеты, опускающие частоту обновления до ~4 Гц и ниже, рекомендованы только для объектов вне frustum ([send-priority](https://doc.photonengine.com/fusion/current/manual/advanced/send-priority), [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1), [sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)).

`NetworkProjectConfig.TimeSynchronizationOverride` упомянут как лечение неровного движения при высоком приоритете («Reducing the send rate, by increasing the send priority can cause object updates to be so infrequent» → подстроить этот override), но **отдельной документации с диапазоном и семантикой не существует** ([whats-new](https://doc.photonengine.com/fusion/current/getting-started/whats-new)).

---

## 21.7. Физика в этом тайминге

Три стратегии ([physics-overview](https://doc.photonengine.com/fusion/current/manual/physics/physics-overview)):

| Подход | Топология | Прокси взаимодействуют | Цена | Для чего |
|---|---|---|---|---|
| **Physics Addon** (предсказание + ресимуляция) | только Client/Server | да, предсказанно и ресимулированно | **максимальная**: «Unity's physics engine is not optimized for resimulation, so this approach is the most expensive» | максимальная точность, когда цена приемлема |
| **Forecast** (2.1) | Shared и Client/Server; в [whats-new](https://doc.photonengine.com/fusion/current/getting-started/whats-new) заявлено «available in Host, Server and Shared Mode» | да, сглаженно, через аппроксимацию | локальная физика на каждом клиенте, «significantly lower CPU cost» | объекты, с которыми игрок взаимодействует; для Shared Mode — рекомендованный путь |
| **Forecast Disabled** | Shared и Client/Server | нет: прокси кинематические, в удалённом времени | «the cheapest option» | физика, которую смотрят, но не трогают |

Практические правила по Forecast ([physics-forecast](https://doc.photonengine.com/fusion/current/manual/physics/physics-forecast)):

* Физику писать в `FixedUpdate()`: «for enabled objects `FixedUpdate()` should be used, not `Update()` or `FixedUpdateNetwork()`».
* «Toggling `ForecastEnabled` at runtime is not supported». Обходной путь из документации — переключать `isKinematic` на `StateAuthority`.
* Зоны коррекции: Deadzone (ошибка ниже `Min Linear Detected Error` / `Min Angular Detected Error`) → плавная коррекция («between the two, the body is smoothly corrected», если не сработала стоп-детекция) → snap (выше `Max Linear Error` / `Max Angular Error`).
* Дефолты: `Error Correction Type` = `Velocity` («default, recommended»); `Gravity Forecast` = `Auto`; `Max Linear Error` / `Max Angular Error` = `0`, и «`0` (the default) disables the limit, so no hard snap is triggered on magnitude alone». Для `Max Extrapolation Time`, `Min Linear/Angular Detected Error`, `Max Error Total Time` **дефолты не документированы** (для минимальной линейной ошибки есть правило «около 2% диаметра объекта», для `Max Error Total Time` — кнопка `Auto`, выводящая значение из bounds коллайдера).

Версионные различия: в 2.1 «By default, on proxies, their rigidbodies are set to kinematic», в 2.0 — нет. Поведение курса (Fusion 2.0 + `Fusion.Addons.Physics`) в 2.1 воспроизводится явными настройками: `RunnerPhysicsSimulate2D/3D = SimulateAlways`, `Physics Authority = Fusion`, `Physics Timing = FixedUpdateNetwork`. Разбор физики курса — [гл. 03](03-chapter.md).

---

## 21.8. Диагностика: мерить, а не угадывать

**Fusion Statistics 2.1** ([fusion-statistics-2.1](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1)). Статистика собирается только с DEBUG DLL — в release-сборках её не будет.

| Страница | Что смотреть |
|---|---|
| **Simulation** | `Forward Ticks` — «How many forward ticks were simulated»; `Re-simulation Ticks` — «How many re-simulation ticks were executed»; Objects Update In/Out |
| **Network** | RTT, In/Out Bandwidth, In/Out Packets, Input In/Out Bandwidth |
| **Time** | «delta time between state receives, interpolation offset and more» |
| **Lag Compensation** | «percentage of hitboxes in use and the time required for internal updates» |
| **Behaviour** | «execution count and execution time of any NetworkBehaviour or SimulationBehaviour», отдельно для FUN и Render |
| **Object** | in/out bandwidth конкретного `NetworkObject` |
| **Memory** | использование двух внутренних аллокаторов; на 100% Fusion больше не аллоцирует |
| **Forecast Objects** (debug, выключена) | включается добавлением префаба `ForecastObjectView` в Statistics Pages; Current/Desired/Interpolated Speed, Lerp Alpha, Error Similarity, Correction Progress, Impact Alignment/Speed |

**Воспроизведение условий** — `NetworkProjectConfig` → `Network Conditions`, «available when using the debug version of the `fusion.dll`» (меню `Fusion > Toggle Debug Dlls`) ([simulating-network-conditions](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/simulating-network-conditions)). Настройки: `Enabled`, `Delay Shape`, `Delay Min`, `Delay Max`, `Delay Period` (в секундах), `Delay Threshold`, `Additional Jitter`, `Loss Chance Shape`, `Loss Chance Min`, `Loss Chance Max` (оба «Range 0 to 1, where 0 means 0% and 1 means 100%»), `Loss Chance Threshold`, `Loss Chance Period`, `Additional Loss`. Для задержки числовой диапазон на странице **не указан** — встречающееся в обзорах «0–0.5 с» дословно не подтверждается, **данные расходятся**.

Три вещи, которые ломают интерпретацию тестов, если их не знать:

1. Host/Server Mode: «The built-in Network Conditions produce 1/2 of the indicated values on each peer» — 200 мс дают ~100 мс на сервере + ~100 мс на клиенте.
2. Shared Mode: «only half (50%) of the indicated values in the settings will actually be applied» — значения надо удваивать.
3. «these values are being ADDED to already naturally occuring latency from being online».

---

## Чего по этой теме нет в учебном курсе

Курс: Unity 2022.3.42f1, Fusion **2.0.5 Stable, build 920** (Feb 2025), целиком Host Mode. Актуальное состояние на 2026-11-01: **2.1.3 Stable, Build 2390 (Sep 17, 2026)**, параллельно живёт ветка **2.0.13 Stable, Build 2379 (Sep 16, 2026)** ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)).

1. **Весь раздел `Time`** (§21.6) — появился в 2.1. На 2.0.5 есть только `TimeSyncConfiguration.MaxLateInputs` / `ExtraSimulationOffset`.
2. **Свободный tick rate 8–256 Гц** — 2.1. Совет «подними tick rate до 128» на курсовой версии физически невыполним; Shared Mode там вообще залочен на 32/16.
3. **Send Priority и `ObjectInactivityThreshold`** — 2.1 / 2.1.4. На 2.0.5 «реже обновлять далёкие объекты» делается только через Interest Management ([гл. 08](08-chapter.md)).
4. **Forecast-физика** — 2.1. Курс использует `Fusion.Addons.Physics` с `NetworkRigidbody3D`, то есть самую дорогую ветку «предсказание + ресимуляция» ([гл. 03](03-chapter.md)).
5. **Дисциплина `IsForward` / `IsResimulation` / `IsFirstTick` / `IsLastTick`** как обязательное правило, запрет RPC в ресимуляции, `[Networked] NetworkButtons _previousButtons` для `WasPressed`, фолбэк на `GetInput() == false` у `StateAuthority` — в курсе не выделены в правило. Про ввод — [гл. 01](01-chapter.md), про RPC — [гл. 07](07-chapter.md).
6. **Инструменты измерения** (§21.8) — в курсе отсутствуют полностью. Без них решения про tick rate и input delay принимаются на ощупь.
7. **Версия Unity.** Текущий SDK заявляет 2021.3.45, 2022.3.45, 6.0.x, 6.3.x; курсовая 2022.3.42f1 **ниже** минимальной 2022.3.45, значит при переносе апгрейд редактора обязателен. Плюс требование «Asset Serialization MUST be set to text (`Project Settings > Editor > Asset Serialization`, `Mode = Force Text`)».
8. **Документация с 2026 года разделена на три набора:** `FUSION 2`, `FUSION 2 SHARED` (своё дерево `/fusion/2-shared/` со своими release notes) и `FUSION 1`. Все утверждения этой главы про Shared Mode взяты из набора FUSION 2; для Shared-проекта читать надо [другой набор страниц](https://doc.photonengine.com/fusion/2-shared/manual/network-runner).
9. **Цены на 2026** (курс их не касается, но агент обязан проверять): Development only — 20 CCU бесплатно; Free 100 — 100 CCU бесплатно, одно приложение; разовый 100 CCU — $95 за 12 месяцев; 500 CCU — $125/мес; 1 000 CCU — $250/мес; 2 000 CCU — $500/мес; Premium Cloud — $0.50 за CCU при минимуме $1 000/мес ([fusion/pricing](https://www.photonengine.com/fusion/pricing)). Цены брать только со страницы Fusion: страницы Realtime и Voice показывают другие цифры.

---

## Решение: что брать и когда

```
Проблема «клиентов колбасит / CPU скачет при лагах»
├─ сначала измерь: Fusion Statistics → Simulation → Re-simulation Ticks
├─ много ресимуляций?
│   ├─ SDK 2.1+ → Time → Input Delay Settings = Custom
│   │              ├─ соревновательный шутер → НЕ включать; снижать tick rate
│   │              └─ физпесочница / мобилка → Minimum Input Delay +
│   │                 Clients Can Add Input Delay To Limit Resims +
│   │                 Client Max Resims Per Frame
│   └─ SDK 2.0.x  → только снижение tick rate + смена стратегии физики
└─ мало ресимуляций, но дёргается визуал?
    ├─ низкий эффективный send rate / высокий Send Priority
    │   → ObjectInactivityThreshold (2.1.4+, в релизах пока нет)
    │     и TimeSynchronizationOverride
    └─ не та временная шкала у ChangeDetector / своего интерполятора
        → ChangeDetector.Source, TryGetSnapshotsBuffers
```

| Задача | Host/Server Mode | Shared Mode |
|---|---|---|
| Авторитетное попадание | `Runner.LagCompensation` (+ `SubtickAccuracy`) | **недоступно** |
| Откат и реконсиляция | Resimulation Loop | **нет**, симуляцию ведёт только Forward Loop |
| Симуляция на прокси | `SetIsSimulated(obj, true)` | **нет** такого пути |
| Физика с взаимодействием | Physics Addon (дорого) или Forecast | только Forecast |
| Лимит tick/send rate | 8–256 Гц (2.1) | ≤ 32 Гц |
| Input delay | да (2.1) | **не поддерживается** |

| Если нужно… | Используй | Не используй |
|---|---|---|
| одноразовый эффект | `if (Runner.IsForward)` | голый вызов в FUN |
| таймер | `TickTimer.CreateFromSeconds` в `Init()` | локальный `float _t -= dt` |
| «событие» клиенту | RPC из Forward-тика | RPC из FUN без проверки фазы |
| мгновенный отклик на выстрел | локальный косметический объект / экстраполяция из `[Networked]`-массива | predicted spawn (удалён в Fusion 2) |
| плавный прокси | снапшотная интерполяция (`NetworkBehaviourBufferInterpolator`) | `Interpolator<T>` / `InterpolationDataSource` (Fusion 1) |
| мгновенный перенос | `NetworkTransform.Teleport()` | запись позиции в `Render()` |
| анимация | изменения `Animator` только на Forward-тиках | расчёт на tick-accurate анимацию |

---

## Чеклист

1. Определи версию SDK по `Assets/Photon/Fusion/release_history.txt` **до** любого совета про тайминг: раздел `Time`, свободный tick rate, Send Priority и Forecast — только 2.1+.
2. Всю логику, меняющую `[Networked]`, держи в `FixedUpdateNetwork()` и считай время через `Runner.DeltaTime`, никогда через `Time.deltaTime`.
3. Любой одноразовый побочный эффект (звук, VFX, спавн визуала, изменение `Animator`) оберни в `if (Runner.IsForward)`; если он должен случиться строго раз за фазу — добавь `IsLastTick`.
4. Не держи в FUN накапливающих не-сетевых полей: переведи их в `[Networked]` или в `TickTimer`, создавая таймер в `Init()`, а не в `Spawned()`.
5. Никогда не вызывай `[Rpc]` в ресимуляции. На Fusion 2.0.x дополнительно отложи ответный RPC до Forward-тика; на 2.1+ проверяй результат через `RpcInvokeInfo` (`NotInvokableDuringResim`).
6. Не рассчитывай на предсказанный спавн: в Fusion 2 его нет. Мгновенный отклик делай локальным не-сетевым визуалом.
7. Аккумулируй кратковременный ввод (клики, дельты мыши) в `Update()` и сбрасывай только после того, как `OnInput` его забрал: за кадр `OnInput` может не вызваться ни разу или вызваться несколько раз.
8. Для `WasPressed` храни `[Networked] NetworkButtons _previousButtons`, иначе ресимуляция даст ложные срабатывания.
9. На `StateAuthority` всегда предусматривай ветку «ввод не пришёл» (`GetInput()` вернул `false`) с кэшем последнего валидного ввода, плюс санитизацию — клиенту доверять нельзя.
10. Для `ChangeDetector` указывай `Source` явно: `SimulationState` для локального таймфрейма (и только для объектов в симуляции), `SnapshotFrom`/`SnapshotTo` — для удалённого.
11. Не используй `Interpolator<T>`, `InterpolationDataSource`, Interpolation Targets и `Runner.LocalRenderTime`/`RemoteRenderTime`: первых трёх в Fusion 2 нет, последних нет и в документации. Бери `TryGetSnapshotsBuffers` / `NetworkBehaviourBufferInterpolator`.
12. Решение «поднять tick rate» принимай только после замера `Re-simulation Ticks`: предсказание измеряется в тиках, поэтому при том же RTT удвоение tick rate удваивает объём ресимуляции.
13. Тестируй под `Network Conditions` с debug-DLL и помни про деление значений: Host/Server — половина на каждый пир, Shared — 50% от заданного, и всё это поверх реальной задержки.
14. При включении Forecast переведи физический код в `FixedUpdate()` и не пытайся переключать `ForecastEnabled` в рантайме — меняй `isKinematic` на `StateAuthority`.
15. Снижая частоту отправки через `SetPriority`, проверяй порог неактивности: на 2.1.4+ — `ObjectInactivityThreshold = 2 * priority / send rate` (дефолт `0.251` с), до 2.1.4 — не опускай эффективную частоту обновления движущихся объектов до ~4 Гц и ниже, кроме объектов вне frustum.
16. Не копируй числа из этой главы как дефолты Fusion. Подтверждённые дефолты — только `Replication Features = Scheduling`, `ObjectInactivityThreshold = 0.251`, `Error Correction Type = Velocity`, `Gravity Forecast = Auto`, `Max Linear/Angular Error = 0`, `StateAuthorityChangeErrorCorrectionDelta = 0`, `ObjectDataConsistency = Full Consistency`. Остальное смотри в инспекторе своего проекта.

---

## Источники

**Fusion 2, концепции и конвейер**
- [network-simulation-loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/network-simulation-loop) — tick rate в Гц, `NetworkRunner.DeltaTime`, расцепление симуляции и рендера, пример предсказания (тики 100/102/103), адаптивное смещение интерполяции.
- [fusion-update-loop](https://doc.photonengine.com/fusion/current/concepts-and-patterns/fusion-update-loop) — порядок фаз кадра, Resimulation Loop только в Server/Host, Forward Loop во всех режимах.
- [network-runner](https://doc.photonengine.com/fusion/current/manual/network-runner) — сколько тиков симулировать за Unity Update, телеметрия и подстройка темпа тиков клиента.

**Настройки**
- [network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) — tick rate 8–256 (2.1), send rate 1–8 с делением без остатка, Shared ≤ 32, Input Transfer Mode, Replication Features (дефолт `Scheduling`), Player Count 1–255, отсутствие остальных дефолтов.
- [time-synchronization](https://doc.photonengine.com/fusion/current/manual/advanced/time-synchronization) — весь раздел `Time` (2.1+), перцентиль 97 при 3%, `Client Max Resims Per Frame`, только опция `Custom`, отсутствие дефолтов, механика анализа spacing пакетов.
- [send-priority](https://doc.photonengine.com/fusion/current/manual/advanced/send-priority) — `SetPriority`/`ClearPriority`, смысл приоритетов 1/2/3, Shared-ограничение, `ObjectInactivityThreshold` (0.251 с, 2.1.4+, формула, только код).
- [eventual-consistency](https://doc.photonengine.com/fusion/current/manual/data-transfer/eventual-consistency) — Full Consistency как дефолт, `SimulationConfig.ObjectDataConsistency`.

**Ресимуляция и её последствия**
- [host-mode-basics/3-prediction](https://doc.photonengine.com/fusion/current/tutorials/host-mode-basics/3-prediction) — переигрывание тиков, сброс сетевого и отсутствие сброса несетевого состояния, `TickTimer`, спавн только на хосте и без предсказания.
- [rpcs](https://doc.photonengine.com/fusion/current/manual/data-transfer/rpcs) — запрет RPC в ресимуляции, `NotInvokableDuringResim`, разница доставки 2.0 vs 2.1, отложка ответного RPC.
- [network-mecanim-animator](https://doc.photonengine.com/fusion/current/manual/sync-components/network-mecanim-animator) — `Animator` forward-only, синхронизация только State Authority → прокси, `Runner.IsForward` в примере.
- [v1/manual/network-object/fixed-update-network](https://doc.photonengine.com/fusion/v1/manual/network-object/fixed-update-network) — формулировки `IsForward`/`IsResimulation`/`IsFirstTick`/`IsLastTick`, `[OrderedBefore]`/`[OrderedAfter]` (**страница Fusion 1**, для Fusion 2 построчно не подтверждена).

**Интерполяция и буферы**
- [network-buffers](https://doc.photonengine.com/fusion/current/manual/advanced/network-buffers) — история снапшотов для объектов без StateAuthority, **дополнительные** current+previous для локально симулируемых, `TryGetSnapshotsBuffers`, `NetworkBehaviourBufferInterpolator`, `ChangeDetector.Source`.
- [network-behaviour](https://doc.photonengine.com/fusion/current/manual/network-behaviour) — «always lerping between two previously simulated snapshots».
- [network-transform](https://doc.photonengine.com/fusion/current/manual/network-transform) — `Teleport()`.
- [network-trsp](https://doc.photonengine.com/fusion/current/manual/sync-components/network-trsp) — `StateAuthorityChangeErrorCorrectionDelta`, дефолт 0, рекомендация 0.15.
- [v1/manual/interpolator](https://doc.photonengine.com/fusion/v1/manual/interpolator) — `InterpolationDataSource` и `force = true` (**только Fusion 1**).

**Ввод и лаг-компенсация**
- [player-input](https://doc.photonengine.com/fusion/current/manual/input/player-input) — `GetInput` true только у Input/State Authority, редкий `false` и кэш последнего ввода, 0..N вызовов `OnInput` за кадр, `[Networked] _previousButtons`.
- [lag-compensation](https://doc.photonengine.com/fusion/current/manual/advanced/lag-compensation) — только Server/Host, 31 хитбокс на `HitboxRoot`, `Hitbox Default Capacity` минимум 16, `HitOptions`, отсутствие описания состава компенсируемого лага.

**Физика**
- [physics-overview](https://doc.photonengine.com/fusion/current/manual/physics/physics-overview) — сравнительная таблица трёх подходов, «physics engine is not optimized for resimulation», Fusion 2.0-эквивалент аддона, кинематические прокси в 2.1.
- [physics-forecast](https://doc.photonengine.com/fusion/current/manual/physics/physics-forecast) — `FixedUpdate()` вместо FUN, запрет переключения в рантайме и обход через `isKinematic`, зоны коррекции, дефолты `Velocity`/`Auto`/`0`.

**Инструменты и версии**
- [fusion-statistics-2.1](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1) — все страницы статистики, `Forward Ticks` / `Re-simulation Ticks`, debug-страница Forecast Objects, требование DEBUG DLL.
- [simulating-network-conditions](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/simulating-network-conditions) — debug-DLL, полный список настроек, диапазон loss 0–1, деление значений по пирам, сложение с реальной задержкой.
- [sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — 2.1.3 Stable build 2390 (Sep 17, 2026), ветка 2.0.13 build 2379, Unity 2021.3.45 / 2022.3.45 / 6.0.x / 6.3.x, Force Text.
- [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) — отсутствие 2.1.4 в релизах; метка «Last updated on 18/06/2026» устарела относительно таблицы билдов.
- [whats-new (2.1)](https://doc.photonengine.com/fusion/current/getting-started/whats-new) — причина появления input delay, tick rate 8–256, Shared-лимиты, Send Priority, `TimeSynchronizationOverride`, Forecast в Host/Server/Shared.
- [whats-new-2-0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-0) — FUN не на прокси, `SetIsSimulated` только Client-Server, `TimeSyncConfiguration.MaxLateInputs`/`ExtraSimulationOffset`, Shared Mode tick-accurate и `TickTimer`.
- [coming-from-fusion-v1](https://doc.photonengine.com/fusion/current/getting-started/migration/coming-from-fusion-v1) — predicted spawning удалён, `NetworkObjectPredictionKey` не нужен, новый interpolation API, Interpolation Targets удалены.
- [2-shared/manual/network-runner](https://doc.photonengine.com/fusion/2-shared/manual/network-runner) — подтверждение отдельного набора документации FUSION 2 SHARED.
- [fusion/pricing](https://www.photonengine.com/fusion/pricing) — тарифы и CCU на 2026.

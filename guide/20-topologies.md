# Глава 20. Топологии и режимы Fusion 2

> **TL;DR для агента**
>
> 1. **Топология — не настройка, а архитектура.** В `NetworkProjectConfig` переключателя топологии нет (проверено по перечню полей страницы [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config) — такой настройки там нет; прямой цитаты «его нет» документация не даёт). Топология задаётся per-session значением `StartGameArgs.GameMode`.
> 2. **Топологий три, моделей программирования две.** Dedicated Server и Client Host — одна и та же модель: «Both Dedicated Server and Client Host have the same programming approach, so are covered by a single tutorial» ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)). Shared Authority — вторая, принципиально другая.
> 3. **Host → Dedicated Server — лёгкая правка; Host → Shared — переписывание.** «Switching topology within Client-Server (between Dedicated Server and Client-Host) is light», «Switching mode (between Shared-Authority and Client-Server) is heavy» ([choose-topology, Fusion/Godot](https://doc.photonengine.com/fusion-godot/current/getting-started/choose-topology)).
> 4. **В Shared Mode главное правило части I неверно.** `INetworkInput` / `OnInput` / `GetInput()` не используются, предсказания и ресимуляции нет, клиент пишет `[Networked]` напрямую. Проверка `Runner.IsServer` заменяется на `Object.HasStateAuthority`.
> 5. **Shared Mode имеет жёсткий потолок 32 Hz** на тик и send: «in Shared Mode the following tick & send rate settings cannot exceed `32`!» ([2-shared/network-project-config](https://doc.photonengine.com/fusion/2-shared/manual/network-project-config)). Шутер/файтинг на 60 Hz в Shared невозможен по определению.

---

## Зачем это знать

Часть I этого гайда выведена из учебного курса, который целиком написан под **Host Mode** на Fusion 2.0.5. Это значит, что главы [01](01-chapter.md)–[09](09-chapter.md) нужно читать как «руководство по топологии Client-Server», а не как «руководство по Fusion». Если агент перенесёт правило из главы [01](01-chapter.md) («клиент отправляет намерение, хост превращает намерение в состояние») в Shared-проект, он получит код, который не компилируется концептуально: в Shared Mode нет ни хоста, ни пайплайна ввода.

Вторая причина: топология определяется требованиями платформы, а не вкусом. Для WebGL документация прямо пишет «We strongly recommend Shared Mode on WebGL», а Server и Host там «are not recommended» и отключены по умолчанию ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)). То есть курс, целиком стоящий на Host Mode, по умолчанию **непригоден для веб-проекта**, и «Host Mode is not recommended for mobile except for special use-cases» (там же) — для мобильного тоже.

---

## 1. Топология против режима: три топологии, два режима

Photon разделяет два понятия, и путаница между ними — главный источник ошибок.

- **Topology** — физическая расстановка узлов: кто где исполняется, нужен ли отдельный хостинг.
- **Mode** — модель программирования: кто владеет состоянием и пишешь ли ты код предсказания.

Связь между ними документация Fusion/Godot формулирует так: «They are linked: pick a topology, and the mode follows from it» ([choose-topology](https://doc.photonengine.com/fusion-godot/current/getting-started/choose-topology)). *(В исходном исследовании эта мысль приводилась как цитата «Your topology choice determines your mode» — такого предложения на странице нет; смысл верен, формулировка другая.)*

Страница [network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies) даёт **три топологии** (там же называемые «Modes») плюс служебный Single Player:

| Топология | `GameMode` | Модель программирования | Кто State Authority |
|---|---|---|---|
| **Dedicated Server** | `GameMode.Server`; клиенты — `GameMode.Client` | Client-Server | выделенный headless-билд, полный и неотчуждаемый авторитет |
| **Client Host** | `GameMode.Host`; клиенты — `GameMode.Client`; авто — `GameMode.AutoHostOrClient` | Client-Server | машина одного из игроков |
| **Shared Authority** | `GameMode.Shared` | Shared | распределён по клиентам, отдельно по каждому объекту |
| *Single Player* (подвид Client Host) | `GameMode.Single` | Client-Server | локальная машина, без сетевого соединения |

**Про написание имени.** Правильно — `GameMode.AutoHostOrClient`. Именно так его пишут страницы [matchmaking](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/matchmaking) («Defaults to `false` in `Client`, and `true` in `Shared` and `AutoHostOrClient`») и [encryption](https://doc.photonengine.com/fusion/v2/manual/advanced/encryption) («This only affects `ClientServer` Modes (`Client`, `Host`, `Server`, `AutoHostOrClient`)»). Вариант `AutoClientOrHost` встречается только на одной странице network-topologies и является опечаткой документации. Курс использует верное имя.

**Dedicated Server и Client Host — это один и тот же код.** Для нашего гайда это ключевой факт: перенос курса на выделенный сервер меняет `GameMode`, деплой и то, что у сервера нет локального игрока, но не меняет архитектуру. Деталь из [matchmaking](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/matchmaking): «In `Server` mode, one extra slot is added for the dedicated `Server`». Хостинг Photon под это не даёт: «Photon does not provide servers for hosting a dedicated Fusion server application» ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)); Dedicated Servers указаны как фича **Enterprise Cloud** ([pricing](https://www.photonengine.com/fusion/pricing)).

**Про соединение.** В Client Host есть «built-in UDP NAT punch-through to enable direct connection between each client and the Player Host»; если не сработало — «that player will connect through a relay», и это «only happens to approximately 1 in 10 players» ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)). В Shared Mode выбора нет вообще: «in `SharedMode`, the clients always connect via relay» ([matchmaking](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/matchmaking)) — отсюда и то, что трафик для Shared критичнее.

**Следующая линия продукта.** В Fusion Core v3 (нативный SDK) разделение названо прямо: «The three topologies are **Shared-Authority**, **Dedicated Server** and **Client-Host**», «The two modes are **Shared-Authority** and **Client-Server**»; в коде это `SimulationMode::Shared` и `SimulationMode::Authority`, задаётся полем `SimulationMode` room-property `fusion_config` (дефолт `"Shared"`), читается через `Client::GetSimulationMode()`. Про Shared там сказано предельно чётко: «No prediction or reconciliation code runs in this mode» ([modes-and-topologies, Fusion Core v3](https://doc.photonengine.com/fusion-core/v3/getting-started/modes-and-topologies)). На Fusion 2 для Unity это не переносится — отдельный продукт, совместимость API не документирована.

---

## 2. Модель авторитета по режимам

### 2.1 Два вида авторитета и судьба второго в Shared

Страница [network-object](https://doc.photonengine.com/fusion/v2/manual/network-object):

- В Server-режимах: «`Runner.StateAuthority` will always be `PlayerRef.None` - as the Server is ALWAYS the State Authority». *(Обратите внимание: документация в этом месте пишет `Runner.StateAuthority`, а не `NetworkObject.StateAuthority` — исследование процитировало второе имя. Смысл один: сервер — implied authority, `None` означает «сервер».)*
- В Shared: «`NetworkObject.StateAuthority` will always be a valid `PlayerRef`».
- Input Authority: «Input Authority is specific to Server Modes (Host, Dedicated, Single) and is not applicable in Shared Server Mode». Параметр `inputAuthority` у `Runner.Spawn(...)` помечен «(Only relevant for Host/Server Mode)» ([spawning](https://doc.photonengine.com/fusion/v2/manual/spawning)).
- В Shared владение и управление — одно и то же: «In Shared Mode, the client who has `InputAuthority` is also the `StateAuthority`» ([shared-mode-input](https://doc.photonengine.com/fusion/v2/manual/input/shared-mode-input)).

### 2.2 Передача авторитета (только Shared)

```csharp
// Код из документации Photon (manual/network-object)
bool ok = Object.RequestStateAuthority();   // забрать
Object.ReleaseStateAuthority();             // отпустить
```

| Механизм | Поведение | Источник |
|---|---|---|
| Назначить авторитет другому игроку | **нельзя**; можно только забрать себе | [network-object](https://doc.photonengine.com/fusion/v2/manual/network-object) |
| `Object.RequestStateAuthority()` | удаётся, если включён `NetworkObject.AllowStateAuthorityOverride` **или** владелец вызвал `ReleaseStateAuthority()` | [network-object](https://doc.photonengine.com/fusion/v2/manual/network-object) |
| Возврат `bool` | с build 1390 (14.10.2025): «`Release/RequestStateAuthority` returns a bool; false means the call has been unsuccessful» | [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) |
| `MasterClientObject` нельзя забрать | build 2390 (17.09.2026): «RequestStateAuthority now returns false locally on a MasterClientObject with AllowStateAuthorityOverride» | [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) |
| Батч-операции | build 2390: «NetworkRunner now exposes BatchRequestStateAuthority and BatchReleaseStateAuthority» — до **16** объектов за один all-or-nothing вызов (парный бэкпорт: есть и в 2.0.13 build 2379, и в 2.1.3 build 2390) | [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) |
| Колбэк | `IStateAuthorityChanged` → «`OnStateAuthorityChanged` is called on every client whenever the `StateAuthority` of an object changes» | [network-object](https://doc.photonengine.com/fusion/v2/manual/network-object) |

**Встроенная защита Shared-состояния, которую легко пропустить:** `NetworkedAttribute.AllowPrediction` (build 1460, 12.11.2025) — «if set to `false`, only state authority can write to the property» ([release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)). Это главный декларативный инструмент против записи `[Networked]`-полей кем попало:

```csharp
[Networked(AllowPrediction = false)] public int Score { get; set; }
```

### 2.3 Master Client — «полусервер» Shared-режима

Shared Mode не бесструктурен: один клиент держит роль `SharedModeMasterClient` ([shared-mode-master-client](https://doc.photonengine.com/fusion/v2/manual/shared-mode-master-client)).

- Роль по умолчанию у создателя Shared-сессии; локальная проверка — `NetworkRunner.IsSharedModeMasterClient` (валидна только в Shared).
- **Передаёт роль сам мастер:** `NetworkRunner.SetMasterClient(PlayerRef player)` вызывает текущий мастер. Это не захват со стороны.
- **Автопереход только при полном дисконнекте:** «Master clients switching does not happen automatically when the master client becomes unresponsive» ([whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/whats-new-2-1)). Именно поэтому в 2.1 появился `NetworkRunner.RequestMasterClientReassign()` (добавлен build 1240, 08.08.2025) — требует `AllowMasterClientReassignRequest` в конфиге, порог `MasterClientReassignRequestThreshold` по умолчанию **2 секунды**; значение `0` «lets any client take over at any time, even if the master is responsive» — документация сама предупреждает о злоупотреблении.
- **Узнать, кто мастер:** `NetworkRunner.GetMasterClient()` — только Fusion 2.1. До 2.1 остаётся косвенный путь — читать `StateAuthority` объекта с флагом `IsMasterClientObject` (⚠️ «единственный способ» — это вывод, документация так не формулирует).
- **Сцены:** «In Shared Mode, only the `SharedModeMasterClient` has the authority to manage scenes»; при загрузке сцены мастер получает State Authority над всеми запечёнными в неё `NetworkObject`.

**Это не Host Migration.** Host Migration — фича топологии Client Host: «Host Migration also does not occur in Shared Mode», и вместо неё «the `SharedModeMasterClient` is transferred to another player if they disconnect» ([disconnect-reconnect](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect)). Для контраста — что нужно, чтобы Host Migration вообще работала в Client Host ([host-migration](https://doc.photonengine.com/fusion/current/manual/host-migration)): это «an _optional_ feature», «not enabled by default»; включается чекбоксом `Enable Host Migration` в `NetworkProjectConfig` (там же `Host Migration Snapshot Interval`); точка входа — «`INetworkRunnerCallbacks.OnHostMigration` callback»; `HostMigrationToken` «contains all the necessary information from the old `NetworkRunner`»; восстановление — через `StartGameArgs.HostMigrationResume` и `NetworkRunner.GetResumeSnapshotNetworkObjects`.

---

## 3. Матчмейкинг, сцены, соединение: что ещё зависит от топологии

Этого слоя в исходном исследовании не было, а он — часть «различий API». Всё из [matchmaking](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/matchmaking):

| Аспект | Client-Server (`Host`/`Server`/`Client`/`AutoHostOrClient`) | Shared |
|---|---|---|
| `EnableClientSessionCreation` | «Defaults to `false` in `Client`», `true` в `AutoHostOrClient` | «`true` in `Shared`» |
| Лобби по умолчанию | «the `ClientServer` Lobby in `Host`, `Server`, `Client` and `AutoHostOrClient`» | «the `Shared` Lobby in `Shared`» |
| Второй хост в сессии | «A `Server` or `Host` cannot join a Session that already has a `Server` or `Host`» | не применимо |
| Кто меняет свойства сессии | «Only the Server or Host, or the Shared Mode Master Client, can change the Session» | мастер-клиент |
| Тип соединения | NAT punch-through, relay как fallback | «in `SharedMode`, the clients always connect via relay» |
| Несовпадение режима | плагин отключает пира с `IncompatibleConfiguration` | то же |

`GameMode.Single`: «In `GameMode.Single` there is no Room, and `NetworkRunner.SessionInfo` is a local record»; `Name` и `Region` — `null`, `PlayerCount`/`MaxPlayers` — 0. Соединения с Photon тоже нет ([network-runner](https://doc.photonengine.com/fusion/v2/manual/network-runner)).

**Шифрование — тоже топологично:** «This only affects `ClientServer` Modes (`Client`, `Host`, `Server`, `AutoHostOrClient`)»; для Shared актуальна только настройка шифрования соединения с Photon Cloud — «This covers encryption for the `Shared` mode» ([encryption](https://doc.photonengine.com/fusion/v2/manual/advanced/encryption)).

**Сцены.** «Both `LoadScene()` and `UnloadScene()` are only allowed to be called on the Server/Host or Master Client», и это «enforced by the default scene manager implementation. Custom implementations must also enforce this» ([scene-loading](https://doc.photonengine.com/fusion/v2/manual/scene-loading)). То есть дефолтный менеджер использовать никто не обязывает — можно свой `INetworkSceneManager`, но проверку придётся повторить. Режимо-нейтральный guard в примерах документации — `if (Runner.IsSceneAuthority)` (⚠️ само свойство в документации формально не описано, встречается только в коде примеров).

Частая ловушка Shared-проектов: если `StartGameArgs.SceneManager` не задан, «Fusion will create an instance of the `NetworkSceneManagerDummy` class and log an error», и dummy не умеет линковать объекты сцены — запечённые `NetworkObject` молча не работают ([scene-loading](https://doc.photonengine.com/fusion/v2/manual/scene-loading)).

---

## 4. Что ломается при переходе Host → Shared

### Недоступно в Shared Mode

| Фича курса | Статус в Shared | Источник |
|---|---|---|
| Lag Compensation (гл. [06](06-chapter.md)) | «Lag Compensation is only available in `Server Mode` and `Host Mode`» | [lag-compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation) |
| Input Delay (Fusion 2.1) | «Input delay is only supported in Server Mode and Host Mode»; «Input delay is disabled by default» | [time-synchronization](https://doc.photonengine.com/fusion/v2/manual/advanced/time-synchronization), [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/whats-new-2-1) |
| Behaviour Interest / `ReplicateTo()` (гл. [08](08-chapter.md)) | «This is only applicable to Server Mode and Host Mode (NOT Shared Mode)» | [interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management) |
| Несколько AoI-регионов (гл. [08](08-chapter.md)) | «Shared mode supports only a single Area of Interest per player»; повторный `AddPlayerAreaOfInterest()` удаляет предыдущий | [interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management) |
| Physics Addon (гл. [03](03-chapter.md)) | в таблице помечен «Client/Server only»; для Shared — «For Shared Mode this is the recommended approach for interactable networked physics» про Forecast | [physics-overview](https://doc.photonengine.com/fusion/v2/manual/physics/physics-overview) |
| Host Migration (гл. [09](09-chapter.md)) | «Host Migration also does not occur in Shared Mode» | [disconnect-reconnect](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect) |
| `ConnectionToken` (гл. [09](09-chapter.md)) | «The three features previously mentioned only apply to Client Host mode» — про ConnectionTokens, Host Migration и CloudConnectionLost | [disconnect-reconnect](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect) |
| `INetworkInput` / `OnInput` / `GetInput()` (гл. [01](01-chapter.md)) | на странице Shared-ввода не упоминаются вообще; в Fusion Core v3 прямо: «No prediction or reconciliation code runs in this mode» | [shared-mode-input](https://doc.photonengine.com/fusion/v2/manual/input/shared-mode-input), [Fusion Core v3](https://doc.photonengine.com/fusion-core/v3/getting-started/modes-and-topologies) |

Отдельно: имена `Fusion.Addons.Physics` и `NetworkRigidbody3D` страница physics-overview не называет — она говорит «Physics Addon» и «Client/Server only». Сами имена живут в документации аддона, не по этому URL.

### Меняет смысл

**`Runner.IsServer` → `Object.HasStateAuthority`.** Это главная механическая правка. Рекомендация Photon для кода, работающего в нескольких топологиях: «best practice for provide support for both is to always check for `Object.HasStateAuthority` instead of `Runner.IsServer`»; топологию раннера в рантайме даёт `Runner.Topology` ([fusion-social-hub](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-social-hub)). *(Сэмпл демонстрирует управление несколькими `NetworkRunner` для перехода между сессиями — Shared для хаба, Host для подземелий; одновременность их работы страница не утверждает.)* Что именно возвращает `Runner.IsServer` в Shared Mode — **официально не документировано**; не опирайтесь на него в Shared.

**Спавн.** «In Host and Server Mode only the Host/Server can spawn `NetworkObjects`»; «In Shared Mode any client can spawn a `NetworkObject`», и «Spawning a `NetworkObject` automatically assigns StateAuthority over it to the client». Despawn в обоих режимах — только у пира со State Authority ([spawning](https://doc.photonengine.com/fusion/v2/manual/spawning)).

**Player Object.** `SetPlayerObject` в Host/Server вызывает только хост; в Shared каждый игрок ставит только свой объект и обязан иметь над ним State Authority ([playerref](https://doc.photonengine.com/fusion/v2/manual/playerref)).

**Ввод.** Из [shared-mode-input](https://doc.photonengine.com/fusion/v2/manual/input/shared-mode-input):

- «Inputs that change networked state should be handled in `FixedUpdateNetwork`»;
- транзиентный ввод — «transient inputs must be captured in `Update` and applied in `FixedUpdate`», потому что «One `Update` frame might contain zero, one, or several `FixedUpdateNetwork` calls»;
- **но для Forecast-rigidbody иначе:** «For these types of objects, we want to read Unity input in `FixedUpdate`», так как «These use Unity's built-in physics interpolation, which occurs between `FixedUpdate` calls».

```csharp
// Код из документации Photon (manual/input/shared-mode-input)
void Update()
{
    if (Object == null || !Object.HasStateAuthority) return;
    _accumulatedMouseDelta += Mouse.current.delta.ReadValue();
}

public override void FixedUpdateNetwork()
{
    AdjustAim(_accumulatedMouseDelta);
    _accumulatedMouseDelta = Vector2.zero;
}
```

**RPC.** Канонический Shared-паттерн — «изменить чужое состояние»: `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]`. Обоснование из туториала: «By default, only the `InputAuthority` (same as `StateAuthority` in Shared mode) can call an RPC» — поэтому `RpcSources.All` обязателен ([shared-mode-basics/5](https://doc.photonengine.com/fusion/v2/tutorials/shared-mode-basics/5-remote-procedure-calls)). Остальное из главы [07](07-chapter.md): ограничение «RPC нельзя отправлять во время ресимуляции» в Shared неактуально, потому что ресимуляции нет (имя константы вроде `NotInvokableDuringResim` в документации не встречается — страница говорит только, что такие вызовы fail). Про `RpcHostMode` в Shared ⚠️ **не подтверждено первоисточником**: Shared-срез документации его вообще не содержит, вывод «смысла не имеет» правдоподобен, но прямой цитаты нет. Дефолт `RpcHostMode` — `SourceIsServer` ([rpcs](https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs)).

`[OnChangedRender]` и `ChangeDetector` (гл. [02](02-chapter.md), [05](05-chapter.md)) работают одинаково и в Shared остаются главным инструментом реакции на чужое состояние.

---

## 5. Цифры и лимиты — все со ссылкой

| Параметр | Значение | Версия | Источник |
|---|---|---|---|
| Тик-рейт Client-Server | «can be set to any value between 8 and 256» | 2.1+ | [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config) |
| Send rate | интервал 1–8, должен делить тик-рейт нацело | 2.1+ | там же |
| Тик/send в Shared | «cannot exceed `32`!» | 2.x | [2-shared/network-project-config](https://doc.photonengine.com/fusion/2-shared/manual/network-project-config) |
| Shared по умолчанию | 32 tick / 16 send; в Fusion 2.0 были залочены на этих значениях | 2.0 / 2.1 | [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/whats-new-2-1) |
| Тик-рейт Client-Server по умолчанию | **официально не документировано** — брать из `NetworkProjectConfig` своего проекта | — | — |
| Player Count | «ranging from 1 to 255» в конфиге; в Shared с 2.0.7 клампится 1–2048 | — | [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config), [release-notes](https://doc.photonengine.com/fusion/current/getting-started/release-notes) |
| Shared AoI cell/grid size | настраиваются с 2.1; «In Fusion 2.0 and earlier it was fixed to 32 units» | 2.1+ | [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/whats-new-2-1) |
| Макс. радиус player AoI в Shared | 300 — **данные расходятся**: цифра есть только в release notes 2.0.0, страница interest-management лимита не упоминает | 2.0.0 | [release-notes](https://doc.photonengine.com/fusion/current/getting-started/release-notes) |
| `Replication Features` по умолчанию | «`Scheduling`: the default»; для AoI «must be set to `Scheduling and Interest Management`» | — | [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config), [interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management) |
| Лимит RPC | 512 байт; снимается `RpcChannel.ReliableLargeData`, но такие RPC не tick-aligned и порядок не сохраняется | 2.1+ | [rpcs](https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs) |
| Батч-авторитет | до 16 объектов, all-or-nothing | 2.0.13 / 2.1.3 | [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) |
| `MasterClientReassignRequestThreshold` | по умолчанию 2 секунды; `0` = любой клиент в любой момент | 2.1+ | [shared-mode-master-client](https://doc.photonengine.com/fusion/v2/manual/shared-mode-master-client) |
| Object Send Priority | priority 1 = каждый send-тик, 2 = SendRate/2, 3 = SendRate/3; API — `NetworkObject.SetPriority(PlayerRef, int)` / `ClearPriority(PlayerRef)`. `SendPriority` и `SendPriorityCallback` **obsolete** с build 2174 (30.06.2026) | 2.1+ | [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/whats-new-2-1), [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) |
| `TimeSynchronizationOverride` | build 1513 (27.11.2025): настройки синхронизации времени переопределяются в Shared Mode | 2.1+ | [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) |
| Предупреждение 2.0.2 | «Warning when Shared Mode is started with a different tickrate than the Shared Mode tickrate» — это предупреждение о **расхождении**, а не о превышении 32 | 2.0.2 | [release-notes](https://doc.photonengine.com/fusion/current/getting-started/release-notes) |

**Актуальные версии на 2026-11-01** ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)): Fusion **2.1.3 Stable, build 2390 (17.09.2026)** и Fusion **2.0.13 Stable, build 2379 (16.09.2026)**. Курс стоит на 2.0.5 build 920 (12.02.2025). Апгрейд: сначала до последнего 2.0.x, потом до 2.1.x; 2.1 требует Realtime 5 и несовместим с Voice SDK для Fusion 2.0. Поддерживаемые Unity: 2021.3.45, 2022.3.45, 6.0.x, 6.3.x. **Почти всё, что делает Shared Mode пригодным для серьёзного проекта, появилось в 2.1**: настраиваемый Shared-тикрейт, Forecast Physics, `GetMasterClient()`, reassign-request, Large Data RPC, `PlayerUniqueId`, Shared Mode Custom Plugins.

**Цены** (подтверждены на 2026-11-01, [pricing](https://www.photonengine.com/fusion/pricing)): Development — бесплатно, 20 CCU / 60.0 GB в месяц; Launch Free — 100 CCU / 0.3 TB; one-time 100 CCU — $95 за 12 месяцев без автопродления / 0.3 TB; 500 CCU — $125/мес / 1.5 TB; 1000 CCU — $250/мес / 3.0 TB; 2000 CCU — $500/мес / 6.0 TB. Premium Cloud — $0.50 за CCU, минимум $1000/мес (включает 2000 CCU и 6.0 TB), 3.0 GB трафика на peak CCU. Доп. трафик — $0.05/GB (Europe, Canada, USA East/West и др.) или $0.10/GB (Asia, Australia, China, India, Japan, South Africa, South America, South Korea, Turkey, UAE). Enterprise Cloud — цены не публикуются. **«CCU for all three Fusion solutions are the same»** ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)) — топология на цену CCU не влияет, влияет на трафик.

---

## 6. Защита от читов и выбор топологии

Прямые цитаты из [fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose) — это то, что агент должен предъявлять заказчику, выбирая топологию:

| | Dedicated Server | Client Host | Shared Authority |
|---|---|---|---|
| Античит | «Cheat protection is strong because the dedicated server has state authority over the game» | «the same cheat protection applies for clients except the host»; «The host client, can cheat and modify any value and has access to full information» | «Because players have State Authority over objects they control, cheating can occur without extra precautions being taken» |
| Размер сессии | «competitive games with a lot of users» | «games with few players within a session (2-4)» | «those with a lot of players» |
| Mobile | «Only recommended for high ARPU mobile titles» | «Host Mode is not recommended for mobile except for special use-cases» | «The recommended mode for mobile» |
| WebGL | не рекомендуется | не рекомендуется, отключён по умолчанию | «We strongly recommend Shared Mode on WebGL» |

**Про имя WebGL-настройки данные расходятся:** fusion-choose пишет «Enable ClientServer Modes in WebGL builds», а [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config) — «Allow Client Server Modes in WebGL». Инспекторное имя, скорее всего, второе. Shared-срез добавляет полезное: «This setting is not relevant for Shared Mode» — Shared не страдает от WebGL-ограничений Client-Server ([2-shared/network-project-config](https://doc.photonengine.com/fusion/2-shared/manual/network-project-config)).

**Чем закрывать дыру античита в Shared:** `[Networked(AllowPrediction = false)]` для полей, которые пишет только авторитет, плюс Shared Mode Custom Plugins — «Custom Plugins are available for Enterprise Cloud Only» ([whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/whats-new-2-1)); их сила в том, что «[SharedMode] Plugin can invoke and send any Rpc and is not limited by `Sources`» ([release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)).

---

## Чего по этой теме нет в учебном курсе

Курс — Fusion 2.0.5, Unity 2022.3.42f1, целиком Host Mode. Shared Mode упомянут одной строкой в [00-README.md](00-README.md) и не используется нигде.

1. **Концепция topology vs mode** и то, что топология задаётся через `StartGameArgs.GameMode`. В таблице README есть три режима, но нет `GameMode.Server`, нет `GameMode.Single` как подвида Client Host и нет `Runner.Topology`.
2. **Передача авторитета**: `RequestStateAuthority` / `ReleaseStateAuthority`, батч-варианты, флаги `AllowStateAuthorityOverride` и `DestroyWhenStateAuthorityLeaves`, колбэк `IStateAuthorityChanged`. Курс исходит из «StateAuthority в Host Mode — всегда хост», и потому единственный его сценарий смены владельца — Host Migration из главы [09](09-chapter.md).
3. **Master Client целиком**: `IsSharedModeMasterClient`, `SetMasterClient`, `GetMasterClient`, `IsMasterClientObject`, `IsSceneAuthority`, reassign-request. Это другой механизм для другой топологии, не Host Migration.
4. **Главное правило курса в Shared неверно.** «Клиент никогда не пишет сетевое состояние» — в Shared клиент пишет его напрямую, без `INetworkInput`. Это надо оговаривать прямо, иначе агент перенесёт правило не туда.
5. **Что физически не переносится:** глава [06](06-chapter.md) (Lag Compensation) — недоступна; уровень `ReplicateTo` и multi-region AoI из главы [08](08-chapter.md) — недоступны; Physics Addon из глав [02](02-chapter.md)–[03](03-chapter.md) — Client/Server only, в Shared нужен Forecast (а в 2.0.5 его не существует); Host Migration **и `ConnectionToken`** из главы [09](09-chapter.md) — Client Host only.
6. **Привычка проверять `Runner.IsServer`.** Замена на `Object.HasStateAuthority` — единственная правка, которая делает весь остальной код режимо-нейтральным.
7. **Потолок 32 Hz** и то, что в 2.0.x Shared-рейты вообще не настраиваются. Курс обсуждает тик-рейт как свободный параметр.
8. **Матчмейкинг и шифрование зависят от топологии** — лобби, `EnableClientSessionCreation`, «always connect via relay», `ClientServer`-only шифрование прямого соединения.
9. **Паттерн «подобрать осиротевшие объекты в `OnPlayerLeft` на мастер-клиенте»** — обязательный шаг любого Shared-проекта (сэмпл [pirate-adventure](https://doc.photonengine.com/fusion/v2/game-samples/pirate-adventure/tech-design) на `GameMode.Shared`).
10. **Версионный разрыв**: 2.0.5 (12.02.2025) против 2.1.3 / 2.0.13 (сентябрь 2026). Плюс Photon ведёт отдельный срез документации `doc.photonengine.com/fusion/2-shared/...` — тот же Fusion 2, но страницы переписаны под Shared; при работе в Shared читать надо именно его.

**Что из курса в Shared переносится:** декомпозиция на мелкие `NetworkBehaviour` и выбор базового класса (гл. [02](02-chapter.md)), Model/View и пулинг (гл. [02](02-chapter.md)), `[OnChangedRender]` и `ChangeDetector` (гл. [02](02-chapter.md), [05](05-chapter.md)), идемпотентность и детерминизм FUN (гл. [03](03-chapter.md)), снапшот-интерполяция через `NetworkArray` (гл. [04](04-chapter.md)–[05](05-chapter.md)), `Runner.GetPhysicsScene()` вместо `Physics.*`, Object Interest/AoI частично (гл. [08](08-chapter.md)).

**Не переносится:** `INetworkInput`-пайплайн (гл. [01](01-chapter.md)), Lag Compensation (гл. [06](06-chapter.md)), `ReplicateTo` и multi-AoI (гл. [08](08-chapter.md)), Host Migration и `ConnectionToken` (гл. [09](09-chapter.md)), Physics Addon (гл. [03](03-chapter.md)).

---

## Решение: что брать и когда

```
Платформа — WebGL?
  └─ да → Shared Authority. Точка. («We strongly recommend Shared Mode on WebGL»)
Платформа — mobile и не high-ARPU?
  └─ да → Shared Authority («The recommended mode for mobile»)
Нужна симуляция быстрее 32 Hz (шутер, файтинг)?
  └─ да → Shared отпадает. Client Host или Dedicated Server.
Соревновательная игра / персистентный мир / много игроков в сессии?
  └─ да → Dedicated Server (античит «strong», но хостинг свой, Enterprise-фича)
Лобби 2–4 игрока, физика, нет бюджета на серверы?
  └─ да → Client Host (но ~1 из 10 через relay, нужна Host Migration)
Много игроков, частые join/leave, социальный хаб, казуальщина?
  └─ да → Shared Authority (+ AllowPrediction=false, + Custom Plugins на Enterprise)
```

| Переход | Цена | Что делать |
|---|---|---|
| Client Host ↔ Dedicated Server | **light** ([choose-topology](https://doc.photonengine.com/fusion-godot/current/getting-started/choose-topology)) | сменить `GameMode`, headless-билд, учесть отсутствие локального игрока у сервера и лишний слот в `MaxPlayers` |
| Client-Server ↔ Shared | **heavy** (там же) | см. чеклист ниже — это переписывание слоя ввода, авторитета и физики |
| Fusion 2.0.x → 2.1.x | средняя | сначала последний 2.0.x, потом 2.1.x; Realtime 5; Voice несовместим |

---

## Чеклист

1. **Определи топологию до первой строки кода.** Платформа (WebGL/mobile), требуемая частота симуляции и требования к античиту решают за тебя. Не начинай с `GameMode.AutoHostOrClient` «потому что так в туториале».
2. **Пиши `GameMode` только в `StartGameArgs`.** В `NetworkProjectConfig` переключателя топологии нет; не ищи его.
3. **Везде, где в части I стоит `Runner.IsServer`, пиши `Object.HasStateAuthority`.** Для сцен — `Runner.IsSceneAuthority`. Это делает код режимо-нейтральным и это официальная рекомендация Photon.
4. **Для Shared-проекта перенеси спавн игрока** из `OnPlayerJoined` хоста в `OnPlayerJoined` локального клиента: joining client спавнит себя и автоматически получает State Authority.
5. **Удали `INetworkInput`, `OnInput`, `GetInput()`.** Читай Input System прямо в `FixedUpdateNetwork` под `HasStateAuthority`; транзиенты (`wasPressedThisFrame`, mouse delta) копи в `Update`; для Forecast-rigidbody читай в `FixedUpdate`.
6. **Каждое «клиент меняет чужое состояние»** реализуй либо `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]`, либо `RequestStateAuthority()` + `AllowStateAuthorityOverride`. Первое — когда важна единая истина, второе — когда важна отзывчивость.
7. **Проверяй возврат `RequestStateAuthority()`** (с build 1390 он `bool`). На `MasterClientObject` он вернёт `false` даже при включённом override (build 2390) — наивный паттерн «заберу авторитет над чем угодно» там не работает.
8. **Ставь `[Networked(AllowPrediction = false)]`** на поля, которые в Shared обязан писать только авторитет. Это дешёвый встроенный античит.
9. **Напиши обработку осиротевших объектов.** В `OnPlayerLeft` под `Runner.IsSharedModeMasterClient` пройди `runner.GetAllNetworkObjects()`, пропусти объекты без `AllowStateAuthorityOverride`, с `IsMasterClientObject` и с `DestroyWhenStateAuthorityLeaves`, остальным со `StateAuthority` ушедшего вызови `RequestStateAuthority()`.
10. **Всегда передавай `StartGameArgs.SceneManager`.** Иначе Fusion подставит `NetworkSceneManagerDummy`, залогирует ошибку, и запечённые в сцену `NetworkObject` не слинкуются.
11. **Для Shared выставь тик и send ≤ 32** и держи в голове, что в 2.0.x они залочены на 32/16 и настройке не поддаются.
12. **Включи `Replication Features = Scheduling and Interest Management`**, если нужен AoI (по умолчанию `Scheduling`), и помни: в Shared регион у игрока ровно один.
13. **Не планируй Host Migration для Shared** — её там нет. Планируй master-client reassign: `AllowMasterClientReassignRequest`, порог, и один «вице-мастер», который следит за heartbeat. Порог `0` не ставь — это дыра для захвата сессии.
14. **Для Client Host включи Host Migration явно** — чекбокс `Enable Host Migration` в `NetworkProjectConfig`, реализуй `OnHostMigration`, прокинь `HostMigrationToken` и `StartGameArgs.HostMigrationResume`.
15. **Не тащи `ConnectionToken` в Shared-проект** — он, как Host Migration и `CloudConnectionLost`, работает только в Client Host.
16. **Если нужна интерактивная сетевая физика в Shared — это Fusion 2.1 и Forecast.** Physics Addon в Shared не работает, в 2.0 Forecast не существует вовсе.
17. **Апгрейдься ступенчато:** 2.0.5 → последний 2.0.x → 2.1.x. Проверь Realtime 5 и Voice SDK перед прыжком на 2.1.
18. **При работе в Shared читай Shared-срез документации** `doc.photonengine.com/fusion/2-shared/...`, а не общий — страницы там переписаны под топологию.

---

## Источники

- [manual/network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies) — три топологии, `GameMode` на каждую, «same programming approach», NAT punch-through и relay 1/10, отсутствие хостинга dedicated у Photon, `GameMode.Single`.
- [manual/network-object](https://doc.photonengine.com/fusion/v2/manual/network-object) — `Runner.StateAuthority == PlayerRef.None` в Server-режимах, валидный `PlayerRef` в Shared, Input Authority только в Server-режимах, `RequestStateAuthority` / `ReleaseStateAuthority` / `AllowStateAuthorityOverride`, `IStateAuthorityChanged`.
- [manual/shared-mode-master-client](https://doc.photonengine.com/fusion/v2/manual/shared-mode-master-client) — `IsSharedModeMasterClient`, `SetMasterClient` вызывает сам мастер, автопереход только при полном дисконнекте, `GetMasterClient()` (2.1), `RequestMasterClientReassign()`, порог 2 с, `IsMasterClientObject`, авторитет над объектами сцены.
- [manual/spawning](https://doc.photonengine.com/fusion/v2/manual/spawning) — кто спавнит в каком режиме, автоназначение State Authority в Shared, `inputAuthority` только Host/Server.
- [manual/playerref](https://doc.photonengine.com/fusion/v2/manual/playerref) — `SetPlayerObject` по режимам, `PlayerRef` с 1 и без переиспользования, dedicated server без `PlayerRef`.
- [manual/network-runner](https://doc.photonengine.com/fusion/v2/manual/network-runner) — `GameMode.Single` без соединения и Room, `LocalPlayer == PlayerRef.None` там, где игрока нет.
- [manual/scene-loading](https://doc.photonengine.com/fusion/v2/manual/scene-loading) — кто может `LoadScene`/`UnloadScene`, дефолтный менеджер enforce-ит, кастомный обязан повторить, `NetworkSceneManagerDummy`, `if (Runner.IsSceneAuthority)` в примерах.
- [manual/connection-and-matchmaking/matchmaking](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/matchmaking) — верное написание `AutoHostOrClient`, `EnableClientSessionCreation` по режимам, лобби `ClientServer` / `Shared`, запрет второго Host/Server, лишний слот в Server, Shared всегда через relay, `GameMode.Single` без Room.
- [manual/network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config) — «Allow Client Server Modes in WebGL», `Replication Features` default `Scheduling`, тик 8–256, send 1–8, потолок 32 в Shared, Player Count 1–255, `Allow Master Client Reassign Request`.
- [2-shared/manual/network-project-config](https://doc.photonengine.com/fusion/2-shared/manual/network-project-config) — «cannot exceed `32`», «This setting is not relevant for Shared Mode» про WebGL.
- [manual/input/shared-mode-input](https://doc.photonengine.com/fusion/v2/manual/input/shared-mode-input) — ввод в `FixedUpdateNetwork`, транзиенты в `Update`, `FixedUpdate` для Forecast-rigidbody, «zero, one, or several `FixedUpdateNetwork` calls», InputAuthority == StateAuthority.
- [manual/data-transfer/rpcs](https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs) — лимит 512 байт, `RpcChannel.ReliableLargeData` (2.1), запрет отправки в ресимуляции, дефолт `RpcHostMode = SourceIsServer`.
- [manual/advanced/lag-compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation) — «only available in `Server Mode` and `Host Mode`».
- [manual/advanced/interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management) — Behaviour Interest «NOT Shared Mode», один AoI-регион в Shared, требование `Scheduling and Interest Management`, отсутствие упоминания макс. радиуса.
- [manual/advanced/time-synchronization](https://doc.photonengine.com/fusion/v2/manual/advanced/time-synchronization) — Input Delay только Server/Host.
- [manual/advanced/encryption](https://doc.photonengine.com/fusion/v2/manual/advanced/encryption) — «only affects `ClientServer` Modes (`Client`, `Host`, `Server`, `AutoHostOrClient`)», Shared покрыт Cloud-шифрованием.
- [manual/physics/physics-overview](https://doc.photonengine.com/fusion/v2/manual/physics/physics-overview) — Physics Addon «Client/Server only», Forecast как рекомендованный путь для Shared, отсутствие Forecast в 2.0.
- [manual/host-migration](https://doc.photonengine.com/fusion/current/manual/host-migration) — Host Migration опциональна и выключена по умолчанию, `Enable Host Migration`, `Host Migration Snapshot Interval`, `OnHostMigration`, `HostMigrationToken`, `StartGameArgs.HostMigrationResume`, `GetResumeSnapshotNetworkObjects`.
- [fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose) — цитаты про античит по топологиям, mobile/WebGL рекомендации, размеры сессий, «CCU for all three Fusion solutions are the same», «Enable ClientServer Modes in WebGL builds».
- [tutorials/shared-mode-basics/5-remote-procedure-calls](https://doc.photonengine.com/fusion/v2/tutorials/shared-mode-basics/5-remote-procedure-calls) — `[Rpc(RpcSources.All, RpcTargets.StateAuthority)]` и обоснование `RpcSources.All`.
- [technical-samples/fusion-social-hub](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-social-hub) — «always check for `Object.HasStateAuthority` instead of `Runner.IsServer`», `Runner.Topology`, несколько `NetworkRunner` для перехода между сессиями.
- [technical-samples/fusion-disconnect-reconnect](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-disconnect-reconnect) — «The three features previously mentioned only apply to Client Host mode», «Host Migration also does not occur in Shared Mode», передача `SharedModeMasterClient`.
- [game-samples/pirate-adventure/tech-design](https://doc.photonengine.com/fusion/v2/game-samples/pirate-adventure/tech-design) — практический Shared-паттерн подбора осиротевших объектов; в коде страницы вызывается `obj.RequestStateAuthority()` (прозаический текст той же страницы пишет `RequestAuthority` — это ошибка текста, альтернативного API нет).
- [getting-started/whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/whats-new-2-1) — Shared AoI cell/grid настраиваемы (было 32 units), `SetPriority`/`ClearPriority`, «Input delay is disabled by default», Forecast в Host/Server/Shared, `PlayerUniqueId`, Custom Plugins только Enterprise, «switching does not happen automatically when the master client becomes unresponsive», тик 8–256 и потолок 32 в Shared, `CloudConnectionLostMode.MoveSessionToNewRoom`.
- [getting-started/release-notes/release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) — `BatchRequestStateAuthority`/`BatchReleaseStateAuthority` (build 2390), `bool` у `Request/ReleaseStateAuthority` (build 1390), `false` на `MasterClientObject` (build 2390), `NetworkedAttribute.AllowPrediction` (build 1460), `TimeSynchronizationOverride` (build 1513), obsolete `SendPriority` (build 2174), «Plugin … is not limited by `Sources`» (build 1460), `RequestMasterClientReassign` (build 1240), Input delay off by default (build 1717).
- [getting-started/release-notes](https://doc.photonengine.com/fusion/current/getting-started/release-notes) — 2.0.2 предупреждение о расхождении Shared-тикрейта, 2.0.4 `StateAuthorityChanged` на прокси, 2.0.6 осиротевшие объекты и независимость `DestroyWhenStateAuthorityLeaves`, 2.0.7 клампы `PlayerCount` и `PageCount`, 2.0.0 радиус AoI 300 и выравнивание Shared-тиков.
- [getting-started/sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — версии 2.1.3 build 2390 (17.09.2026) и 2.0.13 build 2379 (16.09.2026), порядок апгрейда, Realtime 5, Voice, поддерживаемые Unity.
- [fusion-godot/choose-topology](https://doc.photonengine.com/fusion-godot/current/getting-started/choose-topology) — «pick a topology, and the mode follows from it», «light» для смены топологии внутри Client-Server и «heavy» для смены режима.
- [fusion-core/v3/modes-and-topologies](https://doc.photonengine.com/fusion-core/v3/getting-started/modes-and-topologies) — три топологии и **два** режима, `SimulationMode::Shared` / `SimulationMode::Authority`, `fusion_config.SimulationMode` (дефолт `"Shared"`), `Client::GetSimulationMode()`, «No prediction or reconciliation code runs in this mode».
- [photonengine.com/fusion/pricing](https://www.photonengine.com/fusion/pricing) — все тарифы, CCU, трафик, доп. трафик по регионам, Dedicated Servers как Enterprise-фича (подтверждено 2026-11-01).

> **Замечание о полноте поиска.** Бюджет веб-поиска в этом проходе был исчерпан; все факты выше получены прямым чтением страниц документации через WebFetch. Остались непроверенными: точный состав enum `Topologies` (`doc-api.photonengine.com` недоступен), значение `Runner.IsServer` в Shared Mode, работа Multi-Peer Mode / ParrelSync в Shared, актуальность лимита AoI 300 в Fusion 2.1, и связка «смена авторитета → interest-колбэки» (build 1683), указанная в release notes, но не описанная на странице interest-management.

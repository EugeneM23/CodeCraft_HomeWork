# Глава 30. Альтернативы Fusion и критерии выбора

> **TL;DR для агента**
>
> 1. **Fusion нельзя отвязать от Photon Cloud.** Photon прямо пишет: «Photon does not provide servers for hosting a dedicated Fusion server application» ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)) — и при этом «a Fusion Server must be connected to a Photon Cloud Region», а выбор региона «is mandatory» ([dedicated-server-multiplay](https://doc.photonengine.com/fusion/current/technical-samples/dedicated-server/fusion-dedicated-server-multiplay)). Свой сервер вы хостите сами, но от облака Photon не уходите.
> 2. **Главный технический потолок Shared Mode — не Area of Interest, а тик.** «In Shared Mode the following tick & send rate settings cannot exceed 32!» ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)). 32 Hz — это граница для твич-геймплея, и обойти её в Shared Mode нельзя.
> 3. **Client-side prediction — не универсальная фича, а разделительная линия.** Fusion и Netcode for Entities дают её из коробки; NGO официально «doesn't support full client-side prediction and reconciliation» ([client-anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/advanced-topics/client-anticipation.html)); у Mirror в README статус Prediction — «Researching» ([Mirror](https://github.com/MirrorNetworking/Mirror)).
> 4. **Выбор делается по пяти независимым осям** (авторитет, предсказание, кто платит за трафик, исходники, платформы), а не по «сколько игроков держит». Жанр — следствие первых двух осей, а не критерий.
> 5. **Курс учит на Fusion 2.0.5 — там tick/send rate были залочены.** «In Fusion 2 both values were locked (to 32 & 16, respectively)»; менять их «starting in Fusion 2.1» можно в диапазоне 8–256 ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)). Актуальный SDK — 2.1.3 Build 2390 (17.09.2026) ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)).

---

## Зачем это знать

Первая часть этого руководства (главы [01](01-chapter.md)–[09](09-chapter.md), [15](15-chapter.md)) выведена из курса, который целиком построен на Fusion 2.0.5 в Host Mode. Это один угол очень большого пространства решений. Агент, который знает только его, систематически делает две ошибки: берёт Fusion там, где он архитектурно не подходит (детерминированный файтинг, ECS-масштаб, полностью свой сервер), и считает, что client-side prediction есть везде — а потом обнаруживает, что на NGO или Mirror его придётся писать руками.

Вторая ошибка — выбирать стек «по количеству игроков». Почти всё из описанного ниже выдерживает 8–32 игрока. Разница начинается в том, **чей симуляционный цикл считается истиной** и **сколько это стоит в месяц**.

---

## 1. Пять осей, по которым различаются стеки

| Ось | Варианты | Почему это решающее |
|---|---|---|
| **Модель авторитета** | dedicated server · listen-server (client-host) · распределённый по объектам · детерминированный лок-степ · relay (авторитета нет) | определяет античит и то, можно ли вообще доверять клиенту |
| **Предсказание** | полный CSP + reconciliation · только «anticipation»/интерполяция · ничего | определяет, годится ли стек для твича |
| **Кто платит** | managed по CCU (Photon) · managed по часам/кредитам (Unity, Normcore, coherence) · свой хостинг (Mirror/FishNet/Colyseus/Nakama) · платформенный relay (Steam/EOS) | определяет стоимость на масштабе |
| **Исходники** | MIT/Apache/BSD · закрытые DLL · Pro-лицензия | определяет, сможете ли вы сами пропатчить баг или написать транспорт |
| **Платформы и билды** | WebGL/WebSocket · консоли и сертификация · headless Linux | регулярно решающий фактор, который замечают последним |

Жанровая пригодность — производное от осей 1 и 2. Проверять её нужно **после** того, как определены авторитет и предсказание, а не вместо этого.

---

## 2. Fusion 2 как базовая линия: что внутри и где потолки

### Что Fusion заявляет сам

Fusion описан как библиотека синхронизации состояния с «advanced features like data compression, client-side prediction and lag compensation out of the box» и «fully configurable area-of-interest system»; поддерживаются три топологии плюс одиночный режим без сетевого соединения ([fusion-intro](https://doc.photonengine.com/fusion/current/getting-started/fusion-intro)). Важно для сравнения: **lag compensation у Fusion заявлена в базе** — у FishNet это платная Pro-фича (см. §4).

### Версии (состояние на 2026-11-01)

| Ветка | Последняя | Дата |
|---|---|---|
| 2.1.x (актуальная) | **2.1.3 Stable, Build 2390** | 17.09.2026 |
| 2.0.x (поддерживается) | **2.0.13, Build 2379** | 16.09.2026 |
| курс | 2.0.5, Build 920 | Feb 2025 |

Порядок обновления задан дословно: «Projects should be upgraded to the latest 2.0.x SDK before being upgraded to 2.1.x» ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)).

### Настройки и лимиты, которых в курсе не было

Всё из [network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config):

| Настройка | Значение / диапазон | Замечание |
|---|---|---|
| Client & Server Tick Rate | в Fusion 2 **залочены: 32 (tick) и 16 (send)**; с Fusion 2.1 — любое значение **8–256** | это прямо объясняет, почему в курсе эти поля не трогают |
| Client / Server Send Rate | интервал **1–8** (1 = каждый тик, 2 = каждый второй) | интервал должен делить tick rate нацело |
| **Shared Mode tick & send rate** | **не могут превышать 32** | жёсткий потолок режима |
| Player Count | **1–255**, дефолт для сессии; в рантайме `config.Simulation.PlayerCount` | официальное число игроков на сессию |
| Replication Features | `None` · `Scheduling` (**по умолчанию**) · `Scheduling and Interest Management` | AoI работает только на третьем значении |

Отдельно: tick/send rate можно **ограничить и переопределить для всех сессий через дашборд или Webhooks**, чтобы злонамеренный клиент в Shared Mode не выставил нежелательное значение ([там же](https://doc.photonengine.com/fusion/current/manual/network-project-config)). В курсе ([гл. 08](08-chapter.md)) про `Replication Features` сказано только то, что AoI требует значения `Scheduling and Interest Management` — остальные два значения и механика приоритизации при превышении лимита данных на тик не разбираются.

### Где Fusion объективно слабее — сводка с источниками

| Ограничение | Дословно / источник |
|---|---|
| Нет хостинга dedicated-сервера | «Photon does not provide servers for hosting a dedicated Fusion server application» ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)) |
| Нельзя уйти от облака | «a Fusion Server must be connected to a Photon Cloud Region»; «selecting the best Region for your Server is mandatory» ([multiplay](https://doc.photonengine.com/fusion/current/technical-samples/dedicated-server/fusion-dedicated-server-multiplay)) |
| Интеграция с хостингом — ваша | «Fusion does not support any kind of integration with *Multiplay* out of the box» — слой связи сервера с хостинг-сервисом вы пишете сами ([там же](https://doc.photonengine.com/fusion/current/technical-samples/dedicated-server/fusion-dedicated-server-multiplay)) |
| Relay-хвост Host Mode | UDP NAT punch-through с откатом на relay; «only happens to approximately 1 in 10 players» ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)) |
| Хост может читерить | «A rogue host can cheat» ([там же](https://doc.photonengine.com/fusion/current/manual/network-topologies)) |
| Shared Mode: тик ≤ 32 | [network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) |
| Shared Mode: одна AoI-зона | «Shared mode supports only a single Area of Interest per player»; Behaviour Interest (`ReplicateTo`) — «only applicable to Server Mode and Host Mode (NOT Shared Mode)» ([interest-management](https://doc.photonengine.com/fusion/current/manual/interest-management)) |
| Нет детерминизма и реплеев | это отдельный продукт — Quantum (§5) |
| Нет публичных исходников | публичного репозитория нет. Наличие коммерческой source-code-лицензии **официально не документировано** — проверять запросом в Photon |

Технический сетап dedicated-сервера, который Photon документирует официально: `GameMode.Server`, бинд на `0.0.0.0` через `Address = NetAddress.Any(port)` с портом из аргументов командной строки, и таблица маппинга регионов Multiplay → Photon (US/EU/JP/ASIA/SA/CN) ([multiplay](https://doc.photonengine.com/fusion/current/technical-samples/dedicated-server/fusion-dedicated-server-multiplay)). Второй официально документированный провайдер — Gameye, через Docker.

```csharp
// Из документации Photon (Fusion Dedicated Server / Multiplay), не из учебного проекта.
var args = new StartGameArgs {
    GameMode = GameMode.Server,          // всегда Server на выделенном хосте
    Address  = NetAddress.Any(port),     // EndPoint to bind: 0.0.0.0:port
    SessionName = sessionName,
};
```

### «Fusion» в 2026 — это семейство

Отдельная ветка **Fusion Core 3.0.0 (Preview, Build 3364, 22.10.2026)** — это переименованный бывший SharedClient SDK: «The former `SharedClient` SDK is renamed to **Fusion Core**» (Build 1923, 05.05.2026), namespace `SharedMode::*` → `FusionCore::*`, C++20 «required for `Task<T>` coroutines», опциональная `FusionCAPI` с flat C ABI (235+ функций) и CMake-пресеты `UnrealPresets.json` / `GodotPresets.json`. **Unity в release notes не упоминается вообще** ([fusion-core release notes](https://doc.photonengine.com/fusion-core/v3/getting-started/release-notes)). Там же нестыковка: футер страницы говорит «Last updated on 25/03/2026» при билдах до октября — страница обновляется неаккуратно. Вывод для агента: не считайте, что фичи Core v3 придут в Unity-SDK Fusion 2; отношение продуктов официально не документировано.

И контекст: PUN 2 «is in maintenance / LTS mode», «no further feature updates are planned», «New projects should consider Fusion or Quantum» ([PUN migration notes](https://doc.photonengine.com/pun/current/getting-started/migration-notes)). То же в FAQ Fusion: «we suggest to use either Fusion or Quantum, which are our most advanced client solutions» ([FAQ](https://doc.photonengine.com/fusion/current/troubleshooting/faq)). Photon Realtime из [гл. 15](15-chapter.md) остаётся осознанным выбором для простой синхронизации, но не для твича.

---

## 3. Unity-стек: NGO, distributed authority, Netcode for Entities

### NGO: anticipation вместо prediction

Ключевая цитата: NGO «doesn't support full client-side prediction and reconciliation, but it does support client anticipation» ([client-anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/advanced-topics/client-anticipation.html)). Что есть вместо:

| API | Что делает |
|---|---|
| `AnticipatedNetworkVariable<T>` · `.Anticipate()` · `.Value` · `.AuthoritativeValue` · `.Smooth()` | клиент показывает предполагаемое значение до подтверждения сервером; `Smooth()` интерполирует коррекцию вместо скачка |
| `AnticipatedNetworkTransform.AnticipateMove/Rotate/Scale/State` | то же для трансформа |
| `StaleDataHandling.Ignore` (дефолт для переменных) / `.Reanticipate` | что делать с устаревшим серверным апдейтом |
| `ShouldReanticipate`, `OnReanticipate` | точка, где полную петлю rollback+replay вы пишете сами |

Документация честно предупреждает: «Implementing a full, production-ready prediction loop is a complex topic and recommended for advanced users only» ([там же](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/advanced-topics/client-anticipation.html)). Сравните с [гл. 04](04-chapter.md)–[гл. 06](06-chapter.md), где ресимуляция и lag compensation в Fusion — встроенное поведение, а не ваш код.

### Distributed authority: прямой конкурент Host Mode

Авторитет распределён по клиентам согласно ownership permissions; объекты с `Distributable` «are automatically distributed between clients as clients connect and disconnect». Есть **session owner** — «a single dedicated client that's responsible for managing and synchronizing global game state-related tasks»; первым владельцем становится первый подключившийся, а при его отключении «a new session owner is automatically selected and promoted from within the clients that are currently connected»; проверка — `NetworkManager.Singleton.IsSessionOwner`, а `OwnershipStatus.SessionOwner` держит объект за текущим владельцем ([distributed-authority](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/terms-concepts/distributed-authority.html)).

Это **прямой аналог миграции хоста**, которой в Fusion Host Mode нет — в [гл. 09](09-chapter.md) миграция реализуется вручную. Цена — три ограничения, перечисленные Unity дословно: «there's typically no single physics simulation governing the interaction of all objects», «it can be easier for bad actors to cheat», «Using a distributed authority topology is typically not suitable for high-performance competitive games» с точной предиктивной моделью движения ([там же](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/terms-concepts/distributed-authority.html)). Пометки beta/experimental на странице нет.

### NGO 3.0: Unity сводит два своих стека в один

- «Netcode for Entities (`com.unity.netcode` 7.0.0) is now a hard dependency» — вместе с NGO ставятся Entities, Burst, Collections ([release 3.0.0](https://github.com/needle-mirror/com.unity.netcode.gameobjects/releases/tag/3.0.0)).
- Зачем: «takes a hard dependency on Netcode for Entities, laying the groundwork for the unified GameObject layer»; при этом NGO 3.0.0 **ещё не использует** NFE активно ([анонс Unity](https://discussions.unity.com/t/netcode-for-gameobjects-3-0-0-is-now-available/1736792)).
- Ломающие изменения: editor-сборки переименованы (`Unity.Netcode.Editor` → `Unity.Netcode.GameObjects.Editor`), `NetworkTime`, `NetworkTimeSystem` и `NetworkTickSystem` переехали в `Unity.Netcode.GameObjects.Timing`, `[Obsolete]`-API с warning теперь даёт **ошибку компиляции**; апгрейдер «only rewrites references that fail to resolve», ссылки через namespace alias не переписываются ([release 3.0.0](https://github.com/needle-mirror/com.unity.netcode.gameobjects/releases/tag/3.0.0)). Дефолтная ветка репозитория меняется с `develop-2.0.0` на `develop-3.x.x` ([анонс](https://discussions.unity.com/t/netcode-for-gameobjects-3-0-0-is-now-available/1736792)).

**Данные расходятся по минимальной версии редактора.** Анонс Unity: «Unity 6000.7.0b1 is the minimum supported Editor version for Netcode for GameObjects 3.0.0» — это **бета** 6.7, не LTS. Документация пакета 3.1 при этом говорит «Unity 6.0+ and later» ([NGO 3.1 index](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@3.1/manual/index.html)). Формулировку «требует Unity 6.7 LTS» использовать нельзя; проверяйте `package.json` конкретной версии. Параллельно живёт ветка 2.11 для Unity 6.0+.

### Netcode for Entities

«Netcode for Entities requires Unity version 6.7»; зависимости — Unity Transport и Unity Entities. Есть `GhostPredictionSmoothingSystem`, который «provides a way of reconciling and reducing prediction errors over time», prediction switching «on a per-client, per-ghost basis» (например, предсказывать только ghosts в радиусе от character controller клиента) и отдельный раздел «Prediction edge cases and known issues» ([NFE 7.1 prediction](https://docs.unity3d.com/Packages/com.unity.netcode@7.1/manual/prediction.html)).

Позиционирование — дословно от Unity, без упоминания опыта разработчика или требования знать ECS: NFE — «to create high-performance multiplayer games with large numbers of players and complex game logic»; NGO — «for multiplayer games with a smaller number of players and less complex game logic» ([netcode overview](https://docs.unity.com/en-us/multiplayer/netcode/netcode)). Требования «вы должны уметь пользоваться ECS» в актуальной документации 7.1 **нет** — это практический вывод, а не цитата: NFE — это ECS-пакет, и перевод GameObject-проекта на DOTS ради нетcode почти всегда дороже, чем взять Fusion или FishNet.

### Жанровые рекомендации самой Unity

Из [Multiplayer Center](https://docs.unity.com/multiplayer/multiplayer-center) — всего три строки:

| Game type | Networking solution | Connect players | Develop and test |
|---|---|---|---|
| Casual co-op (puzzle, group exploration, co-op platformers) | Netcode for GameObjects + Unity Transport | Multiplayer Services SDK | Multiplayer Play Mode, Multiplayer Tools |
| Competitive (FPS, fighting, battle royales) | **Netcode for Entities** + Unity Transport | Multiplayer Services SDK + «a server hosting service of your choice» | Multiplayer Play Mode, thin clients, Network Profiler |
| Turn-based (strategy, board, auto-battlers, card) | **Cloud Code** | Multiplayer Services SDK | Multiplayer Play Mode |

Distributed Authority на этой странице не упоминается вообще — то есть сама Unity не предлагает её ни для одного из трёх жанров.

---

## 4. Open-source стек: Mirror, FishNet, Netick

| | **Mirror** | **FishNet** | **Netick** |
|---|---|---|---|
| Лицензия | **MIT** ([README](https://github.com/MirrorNetworking/Mirror)) | Unity Asset Store EULA («Extension Asset») для Pro; лицензия бесплатной версии **официально не документирована** | не указана на сайте |
| Версия | **v96.11.3, 17.09.2026** ([releases](https://github.com/MirrorNetworking/Mirror/releases)) | **4.7.4R Pro, 16.10.2026** ([Asset Store](https://assetstore.unity.com/packages/tools/network/fishnet-pro-networking-evolved-287711)) | «Netick 2», номер не указан ([netick.net](https://netick.net/)) |
| Prediction | **«Researching»** ([README](https://github.com/MirrorNetworking/Mirror)) | есть, бесплатно: «the only free solution to provide built-in client-side prediction with only a few lines of code», плюс компоненты desync smoothing и rigidbody prediction ([features](https://fish-networking.gitbook.io/docs/overview/readme/features)) | есть: «CSP ensures that the client enjoys a buttery smooth lag-free gameplay experience» ([netick.net](https://netick.net/)) |
| Lag compensation | **«Beta»**; физический мир не откатывается | **только Pro** | **только Pro** |
| Interest management | **2 варианта**: Spatial Hashing, Distance Checker; одновременно активен один | несколько условий одновременно | Pro-фича |
| Транспорты | **10**: KCP, Telepathy, Websockets + 7 community (Ignorance, LiteNetLib, FizzySteam, FizzyFacepunch, Epic Relay, Bubble, Light Reflective Mirror) | своя транспортная система | — |
| Unity | 2019 / 2020 / 2021 / 2022 LTS и 6000.1 | не указано на странице фич | не указано |
| Цена | $0 | $0 / Pro **$50** (Extension Asset) | $0 / Pro, цена не опубликована |

Детали, которые решают выбор:

- **Mirror `LagCompensator`** записывает «a history of Collider snapshots in 3D» и при приходе `[Command]` откатывает её на латентность стрелка (`compensator.RaycastCheck(...)`). Но: «You also need to worry about rolling back the physics world» и «we don't have a demo for this *yet*». Назначение сформулировано узко: «Lag Compensation (aka Rollback) is needed for fast pace / first person shooter games»; «For MMORPGs, card games, round based games, strategy games, etc. this is not interesting» ([Mirror lag compensation](https://mirror-networking.gitbook.io/docs/manual/general/lag-compensation)). Сравните с [гл. 06](06-chapter.md): в Fusion `Runner.LagCompensation` — это встроенный `HitboxManager`, а не история коллайдеров, которую вы ведёте сами.
- **Mirror: ломающее изменение 2026 года** — v96.11.0 (12.08.2026), «NetworkTime. scaled + unscaled time» ([releases](https://github.com/MirrorNetworking/Mirror/releases)). При обновлении проверяйте весь код, который читает сетевое время.
- **Счётчик игр у Mirror противоречит сам себе**: на одной странице и «over 1000 Steam Games», и «thousands of steam games» ([README](https://github.com/MirrorNetworking/Mirror)). Цитируйте первую формулировку. WebGL в README упомянут только применительно к игре Castaways («Castaway runs in the Browser, thanks to Mirror's WebGL support»); отдельного WebGL-транспорта в списке нет, есть built-in Websockets.
- **FishNet** — ближайший конкурент Fusion по профилю: server-authoritative by design, tick-based ядро, CSP, AoI, «No-Break Promise» по API, Addressables, zero hotpath allocations. Pro добавляет три вещи: lag compensation («rolling back colliders in time to where a client had seen them»), automatic code stripping серверной логики и Network Level of Detail ([features](https://fish-networking.gitbook.io/docs/overview/readme/features)). ⚠️ Имя компонента `ColliderRollback` **не подтверждено первоисточником** — на странице фич он описан словами, без имени класса; проверяйте по SDK. ⚠️ «Пожизненные обновления» и «Priority Support» на листинге Asset Store **не заявлены** — там только цена, «Seats» и тип лицензии.
- Сравнения FishNet с Mirror («Mirror allows only one AOI system at a time», «Mirror is not tick based») — это **заявления вендора** на его собственной странице фич, а не независимая проверка. Количество AoI-вариантов (2) из README Mirror их не опровергает: одновременно действительно активен один.
- **Netick** позиционируется как free, server-authoritative и state-synced: Tick-Aligned Simulation («makes it possible to predict things such as waves, platforms, and elevators»), Snapshot Interpolation, CSP, built-in replay system; Interest Management и Lag Compensation перечислены как Pro Features, цены на странице нет, лицензия и доступ к исходникам не заявлены ([netick.net](https://netick.net/)).

### Платформенный relay ломает тезис «open-source = свой хостинг»

Mirror и FishNet не обязаны означать свои серверы и свой счёт за трафик: в списке транспортов Mirror есть `FizzySteam`, `FizzyFacepunch` и `Epic Relay` ([README](https://github.com/MirrorNetworking/Mirror)) — то есть relay и лобби платформы вместо вашей инфраструктуры. Технический фундамент Steam-пути — `GameNetworkingSockets` (BSD-3-Clause, NAT traversal через WebRTC ICE), и на Steam через него доступны «the additional services provided by the» Steam Datagram Relay network; оговорка Valve дословна: «some features are only available on Steam», и Valve «not able to offer access to SDR on other platforms to all partners» ([GameNetworkingSockets](https://github.com/ValveSoftware/GameNetworkingSockets)). ⚠️ Конкретные гарантии SDR (DDoS-защита, сокрытие IP) и условия доступа лежат в партнёрской документации Steamworks, которая закрыта от публичного чтения — **проверять в Steamworks под своим аккаунтом**. Так же не подтверждены публично условия бесплатности relay/лобби Epic Online Services.

---

## 5. Детерминизм: Photon Quantum 3

Другая парадигма внутри самого Photon: детерминированный ECS, «for up to 128 players», «game clients only exchange player input with the simulation running locally on all clients»; при неверном предсказании состояние восстанавливается и тики пересимулируются; серверный компонент на стороне Photon — game-agnostic authoritative server, синхронизирует часы и **manages input latency**, расширяем до authoritative referee simulation. Целевые жанры названы прямо: «sports games, fighting games, & FPS», с устойчивостью к высоким пингам ([quantum-intro](https://doc.photonengine.com/quantum/current/quantum-intro)).

Прайс Quantum **идентичен** Fusion по всем тирам ([Quantum pricing](https://www.photonengine.com/en-US/Quantum/pricing)). То есть цена перехода — не денежная, а архитектурная: всю симуляцию придётся переписать в их ECS, отделённом от Unity-сцены. Это главный случай, где Fusion объективно слабее — и он находится внутри Photon.

---

## 6. Другие парадигмы: coherence, Normcore, Nakama, Colyseus, свой .NET

**coherence** — самая выразительная модель авторитета. Авторитет разделён на два типа: *state authority* («authorized to change its state, that is, the values of the entity's networked properties») и *input authority* («authorized to send inputs to the State authority»); оба вместе — «full authority». Авторитет — «a property of each `CoherenceSync`», поэтому **разные сущности в одной игре могут использовать разные модели**. Три модели описаны прямо: client authority (проще, но «Higher latency», так как «the minimum latency for data replication and commands is the combined ping»), server-side authority через Simulator, и server-side with client input («The entity is simulated on the Server, and the Client only sends inputs») с локальным предсказанием и реконсиляцией. «Сироты»: `AbandonAuthority()`, `Adopt()`, Auto-Adopt Orphan; сущность обязана быть **Persistent**, иначе Replication Server её удаляет; и важное следствие — «Orphaned entities are not simulated» ([authority](https://docs.coherence.io/manual/networking-state-changes/authority)). Simulators — отдельные билды с флагом `--coherence-simulation-server` ([simulation-server](https://docs.coherence.io/manual/simulation-server.md)); данные ходят через Replication Server, который можно держать в облаке, локально, on-premise или у клиента ([overview](https://docs.coherence.io/overview)); SDK пока только Unity. Цена: Starter — FREE для «solo developers and small teams with less than $200k in revenue or funding»; Pro — «$1,000 / month or $8,000 / year + tax»; «Unlimited CCU» на обоих; хостинг отдельно — предоплаченные кредиты (Cloud) либо revenue share (client/self-hosting), **проценты и структура кредитов не опубликованы** ([pricing](https://coherence.io/pricing)).

Этот язык (state authority / input authority) полезен даже для описания решений на Fusion — в [гл. 01](01-chapter.md) и [92-architecture.md](92-architecture.md) разделение `StateAuthority` / `InputAuthority` есть, но без понятий передачи авторитета и сирот.

**Normcore** — биллинг по room-hours, а не по CCU. Public бесплатно: 100 room hours и 120 GB/мес; Pro $49/мес: 10 000 room hours и 3 TB; Unlimited: $49/мес плюс $0.03/HR и $0.10/GB сверху. На всех тарифах «Unlimited Applications», «Unlimited concurrent users», «Unlimited rooms». Содержательное отличие от Photon сформулировано дословно: «Normcore only bills for the room servers and bandwidth you actually use» и «We don't measure your peak CCUs to determine your bill for the whole month» ([pricing](https://normcore.io/pricing)). Для соц-VR с долгими малонаселёнными комнатами это радикально дешевле; для коротких сессий с высоким CCU — наоборот.

**Nakama** — не нетcode-слой, а бэкенд: Apache-2.0, сервер на Go, нужна Postgres-wire-совместимая БД (CockroachDB), серверная логика на Lua / TypeScript / Go; из коробки аккаунты, storage, друзья/группы, чат с историей, лидерборды, турниры, пати, матчмейкер, валидация IAP, REST/gRPC/WebSocket/rUDP ([GitHub](https://github.com/heroiclabs/nakama)). Режимы мультиплеера документированы как relayed (сервер работает «as a relay for gameplay data between clients»), authoritative (ваш runtime-код валидирует и рассылает) и turn-based в двух формах, active и passive; **client-side prediction на этой странице не упоминается вообще** ([multiplayer concepts](https://heroiclabs.com/docs/nakama/concepts/multiplayer/)). Heroic Cloud тарифицируется по CPU без лимитов DAU/MAU/CCU, **цена не публикуется** (contact sales); Satori — от $600/мес; support-пакеты Studio Basic $2000 и Standard $6000 ([pricing](https://heroiclabs.com/pricing/)).

**Colyseus** — authoritative-сервер на Node.js: состояние описывается Schema, клиенты получают бинарные дельты и коллбеки `onAdd`/`onRemove`, комнаты с `onCreate`/`onJoin`/`onLeave`/`onDispose`, лобби и ranked-очереди; с версии **0.18** появились встроенные client prediction, rollback, interpolation и lag compensation, Unity SDK официальный ([docs](https://docs.colyseus.io/)). Цена: план **Open Source — Free**, self-host на своих серверах, community support, «No restrictions»; Colyseus Cloud — «Starting at $15/mo» с managed SSL, unlimited bandwidth и «No CCU/DAU/MAU limits» ([pricing](https://www.colyseus.io/pricing)). ⚠️ Числа «32 локации» и детали биллингового цикла, которые ходят по сторонним сводкам, на странице прайса **отсутствуют**; бесплатного *хостингового* тира нет — бесплатен только self-host.

**Свой сервер на .NET** оправдан в двух случаях. (а) Игра не realtime-твич — карточная, турновая, async PvP: тогда ASP.NET Core + WebSocket и БД закрывают всё, а нетcode-фреймворк становится лишним весом; это, кстати, ровно то, что Unity рекомендует для turn-based (Cloud Code, [Multiplayer Center](https://docs.unity.com/multiplayer/multiplayer-center)). (б) Нужен полный контроль над симуляцией, античитом, шардингом и экономикой, и серверные инженеры в команде уже есть. Цена — вы пишете сериализацию дельт, интерполяцию, CSP, AoI, матчмейкинг, оркестрацию и наблюдаемость сами. Промежуточный вариант — взять Colyseus или Nakama как каркас.

---

## 7. Экономика: кто за что платит

| Стек | Бесплатно | Дальше | Источник |
|---|---|---|---|
| **Photon Fusion / Quantum** | 20 CCU / 60 GB (dev); 100 CCU / 0.3 TB (launch, ~40k MAU, одно приложение, хард-кап без burst) | **$95 единым платежом** за 100 CCU на 12 месяцев без автопродления (складывается с бесплатными 100 → 200 CCU); далее $125 / 500 CCU (1.5 TB), $250 / 1000 (3 TB), $500 / 2000 (6 TB); Premium $0.50/CCU при минимуме $1000/мес (вкл. 2000 CCU и 6 TB), до 50 000 CCU; трафик сверх — $0.05/GB (EU/Canada/USA) или $0.10/GB (Asia/AU и др.) | [Fusion pricing](https://www.photonengine.com/en-US/Fusion/pricing), [Quantum pricing](https://www.photonengine.com/en-US/Quantum/pricing) |
| **Unity Relay** | 50 средних CCU (≈2 160 000 connectivity minutes) + 3 GiB на CCU, потолок 150 GiB/мес | $0.16 за средний CCU; трафик $0.09/GiB (US+EU), $0.16/GiB (Asia+AU) | [Unity pricing](https://unity.com/products/gaming-services/pricing) |
| **Unity Lobby** | 10 GiB/мес на региональную группу | те же ставки за GiB | там же |
| **Unity Distributed Authority** | 6000 connectivity hours/мес + 100 GiB egress | $0.001/час; те же ставки за GiB | там же |
| **Unity Cloud Code** | 1 000 000 вызовов, 20 compute hours, 1 GiB egress | $1.50 за млн вызовов, $0.00002 за compute-секунду, $0.25/GiB | там же |
| **Unity Game Server Hosting (Multiplay)** | — | **на странице прайса не указано вообще** — считать стоимость «NGO/NFE + dedicated» по открытым данным нельзя | там же |
| **Mirror / FishNet / Netick** | $0 за фреймворк | ваш хостинг — или платформенный relay (Steam/EOS); FishNet Pro $50, Netick Pro — цена не опубликована | [Mirror](https://github.com/MirrorNetworking/Mirror), [FishNet Pro](https://assetstore.unity.com/packages/tools/network/fishnet-pro-networking-evolved-287711) |
| **Normcore** | 100 room hours + 120 GB | Pro $49/мес (10 000 room hours + 3 TB); сверх — $0.03/HR и $0.10/GB. Peak CCU для биллинга не измеряется | [Normcore pricing](https://normcore.io/pricing) |
| **coherence** | Starter FREE до $200k выручки/финансирования, Unlimited CCU | Pro $1000/мес или $8000/год + налог; хостинг — кредиты или revenue share (цифр нет) | [coherence pricing](https://coherence.io/pricing) |
| **Colyseus** | Open Source self-host — Free, «No restrictions» | Cloud «Starting at $15/mo», без лимитов CCU/DAU/MAU | [Colyseus pricing](https://www.colyseus.io/pricing) |
| **Nakama** | Apache-2.0 self-host | Heroic Cloud по CPU, **цена не публикуется**; Satori от $600/мес; support $2000 / $6000 | [Heroic pricing](https://heroiclabs.com/pricing/) |

Главный вывод: **модель биллинга важнее тарифа**. Photon берёт за peak CCU независимо от того, сколько трафика игра реально создаёт; Normcore — за время жизни комнат и прямо отказывается измерять peak CCU; Unity — за средний CCU и гигабайты; open-source — ноль, но вы платите инфраструктурой либо зависимостью от платформенного relay.

---

## Чего по этой теме нет в учебном курсе

Курс = Fusion 2.0.5, Unity 2022.3.42f1, Host Mode. Отсюда девять пробелов:

1. **Альтернатив нет вообще.** В курсе Fusion тождественен «сетевому Unity». Карта из §1–§6 и дерево решений ниже — полностью новый материал.
2. **Shared Mode и Dedicated Server не рассматриваются как реальные опции** — в [гл. 01](01-chapter.md) и [00-README.md](00-README.md) они только перечислены в таблице режимов. Поэтому их ограничения неочевидны: tick ≤ 32 и одна AoI-зона в Shared, отсутствие хостинга у Photon для dedicated.
3. **Tick rate в курсе принципиально не настраивается** — и это не упущение автора: в Fusion 2 значения были залочены на 32/16, менять их можно только с 2.1. Это ключевой пункт раздела «что изменилось с 2.0.5».
4. **Player Count 1–255** как официальное число игроков на сессию в курсе не упоминается; в [гл. 08](08-chapter.md) и [гл. 09](09-chapter.md) масштаб обсуждается через AoI и подключения, а не через конфиг.
5. **Unity-стек ушёл дальше, чем версия курса.** NFE 7.1 требует Unity 6.7; минимальная версия для NGO 3.0 — предмет расхождения источников (6000.7.0b1 в анонсе против «Unity 6.0+» в документации). Значительная часть современного Unity-нетcode просто не поставится в проект на 2022.3.
6. **CSP подан как данность.** [Гл. 04](04-chapter.md)–[гл. 06](06-chapter.md) показывают предсказание, ресимуляцию и lag compensation как встроенное поведение. Что это редкая фича, которой нет в NGO и нет в Mirror, — главная ловушка при смене стека.
7. **Экономики в курсе нет ни строчки.** §7 — первые реальные цифры.
8. **Понятие авторитета бинарное** (хост или не хост). Модель coherence со state/input authority, передачей авторитета и сиротами даёт более богатый язык.
9. **Бэкенд как отдельный слой не обозначен.** Аккаунты, лидерборды, инвентарь, IAP, чат Fusion не закрывает; [гл. 15](15-chapter.md) показывает Photon Chat, но не платформу. Nakama и Unity Gaming Services сочетаются с любым нетcode-выбором.

Таблицы выбора в [91-decision-tables.md](91-decision-tables.md) описывают выбор **внутри** Fusion; дерево ниже — уровнем выше.

---

## Решение: что брать и когда

| № | Если | Берите | Почему |
|---|---|---|---|
| 1 | Турновое / async / не нужно 20+ Hz | свой .NET (ASP.NET Core + WebSocket), Nakama, Colyseus, Unity Cloud Code | Unity сама рекомендует Cloud Code для turn-based; нетcode-фреймворк — лишний вес |
| 2 | Realtime, авторитет не критичен: соц-VR, хангаут, co-op песочница | Normcore (room-hours, peak CCU не измеряется), NGO + distributed authority, Fusion Shared Mode | но помните: Shared Mode — tick ≤ 32 |
| 3 | PvE-кооп / выживание / MMO-лайт, сервер-авторитет, твич не нужен | Mirror (MIT, 10 транспортов, 1000+ игр) или FishNet | у Mirror Prediction «Researching» — это допустимо здесь |
| 4 | Соревновательный твич на GameObjects, нужны CSP + lag compensation | FishNet Pro ($50, свой хостинг или Steam/EOS relay) или Fusion 2 (managed-облако, Host/Server) | выбор = «контроль и цена» против «скорость и инфраструктура»; у Fusion lag compensation в базе, у FishNet — в Pro |
| 5 | Нужен детерминизм: файтинг, спорт, RTS, реплеи, лок-степ-античит | **Quantum 3** (до 128 игроков) или свой лок-степ | Fusion здесь не инструмент; прайс тот же, цена — переписать симуляцию |
| 6 | Десятки тысяч сущностей, геймплей уже в ECS, long-term на Unity | Netcode for Entities 7.x (Unity 6.7) | единственный официальный Unity-путь с настоящим server-authoritative prediction |
| 7 | Сложная передача авторитета, persistent world, симуляторы как процессы | coherence | Pro $1000/мес выше порога $200k; SDK только Unity |
| 8 | Нужен бэкенд независимо от нетcode | Nakama (Apache-2.0, self-host) или Unity Gaming Services | сочетается с любым пунктом 2–7 |

Короткий дискриминатор: **нужен ли вам полный CSP?** Нет → §2/§3 списка. Да, и вы на GameObjects → FishNet или Fusion. Да, и нужен детерминизм → Quantum. Да, и вы в ECS → Netcode for Entities.

---

## Чеклист

1. Разложи требования по пяти осям из §1 **прежде** чем назвать стек. Жанр назови последним — он следствие, а не критерий.
2. Ответь «нужен ли полный client-side prediction?» отдельным пунктом. Если да — вычеркни NGO и Mirror из списка немедленно.
3. Если рассматриваешь Shared Mode — проверь, укладывается ли геймплей в **tick ≤ 32**. Не укладывается → Host или Server Mode.
4. Если планируешь dedicated — заложи в план: свой headless-билд, свой провайдер, **свой слой связи с хостингом** («Fusion does not support any kind of integration with Multiplay out of the box») и обязательную привязку к региону Photon Cloud.
5. Никогда не обещай «уйти от вендора» на Fusion. Нужна независимость → Mirror / FishNet / Netick / Colyseus / Nakama.
6. Перед выбором Unity-стека проверь версию редактора по `package.json` конкретного пакета, а не по статьям: NFE 7.1 требует Unity 6.7, а по минимальной версии NGO 3.0 источники расходятся.
7. Беря NGO 3.x, заложи миграцию: жёсткая зависимость на NFE 7.0, переезд `NetworkTime`/`NetworkTimeSystem`/`NetworkTickSystem` в `Unity.Netcode.GameObjects.Timing`, `[Obsolete]` → ошибка компиляции.
8. Считай стоимость в **модели биллинга**, а не в тарифе: peak CCU (Photon) vs room-hours (Normcore) vs средний CCU + GiB (Unity) vs ноль + своя инфраструктура.
9. Для «бесплатных» Mirror/FishNet сразу выбери, откуда берётся relay и лобби: свои серверы или платформа (Steam/EOS). Условия платформенного relay проверь в её партнёрской документации — публично они не опубликованы.
10. Если нужен бэкенд (аккаунты, лидерборды, IAP, чат) — выбери его **отдельным решением**. Не жди этого от нетcode-фреймворка.
11. Беря Quantum, заранее признай: вся симуляция переезжает в их ECS, оторванный от Unity-сцены. Прайс тот же — цена архитектурная.
12. Любое число из этой главы, которое попадёт в коммерческое решение, перепроверь по ссылке на дату решения. Цены и версии меняются; данные здесь — на 2026-11-01.

---

## Источники

**Photon Fusion**
- [doc.photonengine.com/fusion/current/getting-started/fusion-intro](https://doc.photonengine.com/fusion/current/getting-started/fusion-intro) — compression, CSP и lag compensation «out of the box», три топологии + single player.
- [.../getting-started/sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — версии 2.1.3 (Build 2390, 17.09.2026), 2.0.13 (Build 2379, 16.09.2026), порядок обновления 2.0.x → 2.1.x.
- [.../manual/network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies) — отсутствие хостинга dedicated, NAT punch-through и relay (1 из 10), rogue host, Photon Room держит копию состояния в Shared.
- [.../manual/network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) — tick/send rate (32/16 locked в Fusion 2, 8–256 с 2.1), send rate интервал 1–8, Shared Mode ≤ 32, Player Count 1–255, Replication Features и их дефолт, override через дашборд/Webhooks.
- [.../manual/interest-management](https://doc.photonengine.com/fusion/current/manual/interest-management) — одна AoI-зона в Shared, Behaviour Interest только Server/Host, Send Priority (2.1).
- [.../technical-samples/dedicated-server/fusion-dedicated-server-multiplay](https://doc.photonengine.com/fusion/current/technical-samples/dedicated-server/fusion-dedicated-server-multiplay) — обязательная привязка к региону Photon Cloud, отсутствие интеграции с Multiplay, `GameMode.Server`, `NetAddress.Any(port)`, таблица регионов.
- [.../troubleshooting/faq](https://doc.photonengine.com/fusion/current/troubleshooting/faq) — рекомендация Fusion или Quantum.
- [photonengine.com/en-US/Fusion/pricing](https://www.photonengine.com/en-US/Fusion/pricing) — все тиры, включая $95 one-time за 100 CCU; отсутствие лимита игроков на комнату на странице.
- [doc.photonengine.com/fusion-core/v3/getting-started/release-notes](https://doc.photonengine.com/fusion-core/v3/getting-started/release-notes) — Fusion Core 3.0.0 Preview, переименование в Build 1923, `FusionCore::*`, C++20, FusionCAPI, пресеты Unreal/Godot, отсутствие Unity.

**Photon Quantum и PUN**
- [doc.photonengine.com/quantum/current/quantum-intro](https://doc.photonengine.com/quantum/current/quantum-intro) — до 128 игроков, обмен только инпутом, rollback, жанры, «manages input latency».
- [photonengine.com/en-US/Quantum/pricing](https://www.photonengine.com/en-US/Quantum/pricing) — прайс, идентичный Fusion.
- [doc.photonengine.com/pun/current/getting-started/migration-notes](https://doc.photonengine.com/pun/current/getting-started/migration-notes) — PUN 2 «in maintenance / LTS mode», «New projects should consider Fusion or Quantum».

**Unity**
- [NGO 2.11 client-anticipation](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/advanced-topics/client-anticipation.html) — отсутствие полного CSP, все имена API anticipation, предупреждение про advanced users.
- [NGO 2.11 distributed-authority](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.11/manual/terms-concepts/distributed-authority.html) — session owner и его промоушен, `IsSessionOwner`, `OwnershipStatus.SessionOwner`, `Distributable`, три ограничения.
- [NGO 3.0.0 release](https://github.com/needle-mirror/com.unity.netcode.gameobjects/releases/tag/3.0.0) — hard dependency на NFE 7.0.0, переименование сборок, namespace `...GameObjects.Timing`, `[Obsolete]` → ошибка компиляции, поведение апгрейдера.
- [Анонс NGO 3.0.0 на discussions.unity.com](https://discussions.unity.com/t/netcode-for-gameobjects-3-0-0-is-now-available/1736792) — «Unity 6000.7.0b1 is the minimum supported Editor version», «laying the groundwork for the unified GameObject layer», смена дефолтной ветки.
- [NGO 3.1 index](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@3.1/manual/index.html) — «Unity 6.0+ and later» (расхождение с анонсом), платформы, требования WebGL.
- [NFE 7.1 prediction](https://docs.unity3d.com/Packages/com.unity.netcode@7.1/manual/prediction.html) — Unity 6.7, зависимости, `GhostPredictionSmoothingSystem`, prediction switching, edge cases.
- [docs.unity.com/en-us/multiplayer/netcode/netcode](https://docs.unity.com/en-us/multiplayer/netcode/netcode) — позиционирование NGO vs NFE дословно.
- [docs.unity.com/multiplayer/multiplayer-center](https://docs.unity.com/multiplayer/multiplayer-center) — жанровая таблица из трёх строк; Distributed Authority не упоминается.
- [unity.com/products/gaming-services/pricing](https://unity.com/products/gaming-services/pricing) — Relay, Lobby, Distributed Authority, Cloud Code, Vivox; отсутствие цены Game Server Hosting.

**Open source и альтернативы**
- [github.com/MirrorNetworking/Mirror](https://github.com/MirrorNetworking/Mirror) — MIT, 6.4k звёзд, версии Unity, 10 транспортов, 2 AoI-варианта, статусы Prediction/Lag Compensation, счётчики игр.
- [github.com/MirrorNetworking/Mirror/releases](https://github.com/MirrorNetworking/Mirror/releases) — v96.11.3 (17.09.2026), ломающее `NetworkTime` в v96.11.0 (12.08.2026).
- [mirror-networking.gitbook.io/docs/manual/general/lag-compensation](https://mirror-networking.gitbook.io/docs/manual/general/lag-compensation) — `LagCompensator`, `RaycastCheck`, нерешённый откат физического мира, жанровая применимость.
- [fish-networking.gitbook.io/docs/overview/readme/features](https://fish-networking.gitbook.io/docs/overview/readme/features) — CSP бесплатно, desync smoothing и rigidbody prediction, Pro-фичи, AoI и tick-based, сравнения с Mirror (заявления вендора).
- [assetstore.unity.com/.../fishnet-pro-287711](https://assetstore.unity.com/packages/tools/network/fishnet-pro-networking-evolved-287711) — 4.7.4R Pro (16.10.2026), FirstGearGames, $50, Extension Asset.
- [netick.net](https://netick.net/) — free server-authoritative, Tick-Aligned Simulation, CSP, Snapshot Interpolation, replay; Pro = Interest Management + Lag Compensation.
- [github.com/ValveSoftware/GameNetworkingSockets](https://github.com/ValveSoftware/GameNetworkingSockets) — BSD-3-Clause, NAT traversal через ICE, доступ к SDR только на Steam и не для всех партнёров.

**Бэкенды и платформы**
- [github.com/heroiclabs/nakama](https://github.com/heroiclabs/nakama) — Apache-2.0, Go, Postgres-wire БД, рантаймы, список фич.
- [heroiclabs.com/docs/nakama/concepts/multiplayer/](https://heroiclabs.com/docs/nakama/concepts/multiplayer/) — relayed / authoritative / turn-based (active, passive); prediction не упоминается.
- [heroiclabs.com/pricing](https://heroiclabs.com/pricing/) — Heroic Cloud по CPU, цена не публикуется; Satori от $600/мес; support $2000 / $6000.
- [docs.colyseus.io](https://docs.colyseus.io/) — Schema, дельты, комнаты, prediction/rollback/interpolation/lag compensation с 0.18, Unity SDK.
- [colyseus.io/pricing](https://www.colyseus.io/pricing) — Open Source Free self-host, Cloud от $15/мес, без лимитов CCU/DAU/MAU.
- [normcore.io/pricing](https://normcore.io/pricing) — room-hours, все тиры, «only bills for ... you actually use», peak CCU не измеряется.
- [docs.coherence.io/manual/networking-state-changes/authority](https://docs.coherence.io/manual/networking-state-changes/authority) — state/input/full authority, три модели, combined ping, per-`CoherenceSync`, сироты и их несимулируемость.
- [docs.coherence.io/manual/simulation-server.md](https://docs.coherence.io/manual/simulation-server.md) — Simulators и `--coherence-simulation-server`.
- [docs.coherence.io/overview](https://docs.coherence.io/overview) — Replication Server и варианты его размещения, SDK только Unity.
- [coherence.io/pricing](https://coherence.io/pricing) — Starter FREE до $200k, Pro $1000/мес или $8000/год + налог, Unlimited CCU, модели хостинга.

**Открытые вопросы (сознательно не закрыты)**
- Коммерческая source-code-лицензия на Fusion — официально не документирована.
- Отношение Fusion Core v3 к Unity-SDK Fusion 2 — не документировано.
- Минимальная версия редактора для NGO 3.x — данные расходятся (6000.7.0b1 vs «Unity 6.0+»).
- Цена Unity Game Server Hosting / Multiplay — на странице прайса отсутствует.
- Имя компонента `ColliderRollback` в FishNet Pro — в документации не названо.
- Лицензия бесплатной версии FishNet и цена Netick Pro — не опубликованы.
- Условия Steam Datagram Relay и бесплатность relay/лобби EOS — в закрытой партнёрской документации.
- Структура кредитов coherence Cloud и проценты revenue share — не опубликованы.

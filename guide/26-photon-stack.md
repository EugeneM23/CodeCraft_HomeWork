# Глава 26. Экосистема Photon: что выбрать

> **TL;DR для агента**
>
> 1. **Realtime — фундамент, остальное — надстройки.** Fusion, Quantum, PUN 2, Voice и Chat «are all using Realtime as their foundation» ([photon-products](https://doc.photonengine.com/photon/current/photon-products)). Любой симптом «не коннектится / не вижу комнату / не тот регион / `MaxCcuReached`» — это уровень Realtime, а не Fusion.
> 2. **Продукт выбирается по топологии авторитета, а не по фичам.** Photon формализовал это как Photon Quadrant: Fusion Shared / Quantum / Fusion Server / Fusion Host. Курс покрывает ровно один квадрант — Client Host.
> 3. **Платишь за Peak CCU = СУММУ месячных пиков по регионам.** EU 420 + US 230 = 650 CCU ([pricing](https://doc.photonengine.com/photon/current/pricing)). Поэтому Region Allowlist — инструмент управления счётом, а не только пингом.
> 4. **Free-план 20 CCU запрещает публикацию игры**; бесплатные 100 CCU разрешают коммерческий запуск, но только для Fusion/Quantum и только одно приложение на клиента ([pricing](https://doc.photonengine.com/photon/current/pricing)).
> 5. **Курс отстал на две минорные линии.** Fusion 2.1.3 build 2390 (17.09.2026) и 2.0.13 build 2379 (16.09.2026) против курсовых 2.0.5 build 920 ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)). В 2.1 есть breaking change, который ломает курсовой код: `PlayerRef.MasterClient` переименован.

---

## Зачем это знать

Учебный курс даёт один инструмент и одну топологию. Этого достаточно, чтобы написать кооперативный шутер на 4 игроков, и недостаточно, чтобы ответить на вопросы, которые возникают до первой строки кода: на чём вообще писать эту игру, сколько это будет стоить в лайве, в каких регионах её можно запускать и что делать, если нужен голос, чат или детерминированные реплеи.

Ошибка выбора здесь не лечится рефакторингом. Переход с Fusion Host на Quantum — это переписывание симуляции с нуля. Переход с PUN на Fusion Host — переписывание модели владения объектами. Поэтому глава стоит перед кодом, а не после.

Второй мотив: часть «багов» в сетевом проекте вообще не баги. Дисконнект `MaxCcuReached` — исчерпанный тариф. «Два игрока не видят друг друга» — разный `AppVersion` или разные регионы при недетерминированном Best Region. Агент, обученный только по курсу, будет искать это в коде и не найдёт.

---

## 26.1. Карта стека: один транспорт, четыре топологии

**Realtime** — низкоуровневый слой: комнаты, лобби, матчмейкинг, выбор региона, события, кастомные свойства. Что он умеет по [realtime-intro](https://doc.photonengine.com/realtime/current/getting-started/realtime-intro):

| Возможность | Что это |
|---|---|
| Rooms | игровые сессии со свойствами: open, visible, max players, имя как ID |
| Lobbies | листинг видимых комнат на клиенте |
| Random room matchmaking | «ask the server to join a random room fitting a given filter» |
| **SQL Lobby** | SQL-подобные фильтры, например диапазон уровней |
| **Matchmaking Tickets** | внешний сервер определяет матчмейкинг за клиента и ограничивает его конкретной комнатой |
| Events | произвольный код и контент между игроками в комнате; подходит для частых апдейтов (ввод, позиции) |
| Custom Room / Player Properties | key-value на комнату и на игрока |

Чего Realtime не делает — цитата оттуда же: «On its own, it does not provide object synchronization or tight engine integration». За этим идут в Fusion или Quantum, у которых «rich and deep integration into game engines like Unity, Unreal and Godot».

**Архитектура серверов** (там же, дословно): Name Servers «provide region lists and addresses and authenticate the users», Master Servers «handle matchmaking per region», Game Servers «host the actual gameplay in rooms». И два следствия, которые надо держать в голове: «A Realtime client is only connected to one server type at any time» и «matchmaking is only available while the client is not active in a room (and vice versa)». То есть **находясь в комнате, нельзя получить список комнат** — это архитектурное ограничение, а не недоработка API. Глава [15](15-chapter.md) работает именно на этом слое.

> ⚠️ Research-черновик приписывал странице realtime-intro «релей сообщений» и «webhooks» — в тексте страницы этого нет (webhooks присутствуют только как пункт навигации). Утверждение убрано.

**Актуальная версия Realtime — 5.1.20** (Unity SDK и .NET SDK, [realtime/sdk-download](https://doc.photonengine.com/realtime/current/getting-started/sdk-download); номер билда страница не публикует). Глава [15](15-chapter.md) стоит на 5.1.5 — разрыв 15 патч-версий внутри линии 5.1.

---

## 26.2. Photon Quadrant: таблица выбора топологии

Это основная таблица главы. Данные — со страницы [photon-products](https://doc.photonengine.com/photon/current/photon-products), формулировки сильных и слабых сторон приведены близко к оригиналу.

| Топология | SDK | Сильные стороны | Слабые стороны | Игры (полный список со страницы) |
|---|---|---|---|---|
| **Shared Authority** | Fusion Shared mode | любая платформа, идеально для mobile и web, cost effective; плагины для анти-чита и кастомного серверного кода (Enterprise Cloud) | «not suited for highly competitive/eSports games»; читерство | Peak, R.E.P.O., Polygun Arena, Ratchet & Clank: Ranger Rumble |
| **Deterministic Predict/Rollback** | **Quantum** | любая платформа, mobile/web, simple to operate, cost effective; «frame-perfect esports, built-in bot SDK and cheat-proof»; плагины (Enterprise Cloud) | «each client has ‘perfect information’» | Stumble Guys, No Rest for the Wicked, Lucky Defense, Windblown, World War Armies |
| **Dedicated Server** | Fusion Server mode | «most performant headless Unity build with full customization and self-hosted, **Photon Cloud as relay**. Cheat-proof» | «Complex to operate with high costs for external orchestration service which is problematic for mobile f2p and web» | Prime Rush, Breachers |
| **Client Host** | Fusion Host mode | cost effective, easy to operate, **high gameplay quality possible** | «not suited for mobile due to unstable connections; unstable game quality due to host and relayed connections»; читерство | Among Us 3D, Davigo |

Два уточнения, которые research-черновик искажал:

- **Релей используется не только в Host.** Описание Dedicated Server тоже содержит «Photon Cloud as relay». Релей — это не отличительная черта Host-режима.
- **Серверные плагины — не привилегия Fusion Shared.** Строка Quantum на той же странице тоже перечисляет плагины для анти-чита и кастомного серверного кода на Enterprise Cloud, а для Quantum это отдельный документированный продукт (см. 26.4).

Приоритет выбора Photon формулирует прямо: «Selecting the right topology is foremost based on your gameplay», платформы — во вторую очередь, стоимость лайв-операций — в третью. И отдельно: выход в лайв на dedicated server «requires 3rd party server orchestration at additional costs» — Photon managed-хостинг игровых серверов в этих тарифах не даёт.

Курс целиком живёт в правом нижнем квадранте (см. таблицу режимов в [00-README](00-README.md#02-модель-мира-fusion-2--то-что-надо-держать-в-голове-всегда)) — то есть покрывает одну топологию из четырёх, причём ту, которую Photon сам помечает как непригодную для мобильных платформ.

---

## 26.3. Quantum: другая модель вычислений, а не другой SDK

**Fusion (state sync):** авторитет считает мир и рассылает **состояние** — `[Networked]`-поля, дельты, снапшоты. Трафик растёт с размером состояния и числом объектов.

**Quantum (deterministic predict/rollback):** клиенты обмениваются **только инпутом**, симуляция идёт на каждом клиенте и обязана дать побитово одинаковый результат. Серверный компонент Photon — «game-agnostic authoritative server component», его задача «synchronize clocks and manage input latency»; при этом клиенты «never need to wait for a slower client or a client with a worse network connection» — классического lockstep-залипания нет ([quantum-intro](https://doc.photonengine.com/quantum/current/quantum-intro)).

Технические опоры Quantum 3 (оттуда же): «high performance sparse-set Entity Component System (ECS)»; **Qtn** — «a simple domain specific language (DSL)… to automatically generate C# code for game data»; логика в stateless-системах, получающих `Frame` каждый тик; «Complete set of deterministic libraries (math, 2D and 3D physics, navigation, animation, bots, etc.)»; симуляция «decouples simulation logic (Quantum ECS) from the view/presentation (Unity)»; поддержка **«up to 128 players»**.

> ⚠️ Фикспойнт-математика в тексте quantum-intro не описана — термин «Fixed Point» есть только как пункт навигации. Что математика детерминированная — задокументировано; что она именно fixed-point — на этой странице нет.

**Актуальные версии.** Quantum **3.0.14, build 2241, 05.10.2026**, внутри Photon Realtime **5.1.20**; минимальная Unity для линии 3.0 — **Unity 2021 LTS** ([release-notes](https://doc.photonengine.com/quantum/current/getting-started/release-notes)). В свежих релиз-нотах есть фиксы под Unity 6000.7 beta (3.0.14), Unity 6.5+ (3.0.12), 6000.4 (3.0.11), 6.3 (3.0.10), 6000.3 (3.0.9).

Есть **Quantum SDK 3.1.0 Preview, build 2279, 30.10.2026** — и к нему явное предупреждение: «The Quantum SDK 3.1.0 is provided as development snapshots and is **not** intended to be used for live or released games» ([preview-3-1/sdk-download](https://doc.photonengine.com/quantum/current/getting-started/preview-3-1/sdk-download)). Что в 3.1: table components «as an alternative to the sparse set ECS components that scale better», новый физический солвер `ProjectedGaussSeidel` («greatly improves the stability when stacking rigid bodies»), `QuantumEntityViewTransformHistory` для интерполяции вида, экспериментальный режим `ExponentialDecay`. Breaking changes 3.1: команды больше не требуют `CompoundCommand` (`QuantumGame.AddCommand(DeterministicCommand)` + `frame.GetPlayerCommands<MyCommand>(player)`), обязательный явный `TaskRunner` в `SessionRunner.Arguments`, `Quantum.Profiling.HostProfiler` → `Quantum.HostProfiler`.

### Конфигурация Quantum: что документировано, а что нет

Со страницы [config-files](https://doc.photonengine.com/quantum/current/manual/config-files).

**SessionConfig — дефолтов страница не публикует вообще.** Перечислены настройки: Simulation Rate (Update FPS, Гц), Lockstep, Rollback Window (кадры в локальном ring buffer — именно он ограничивает глубину предсказания; «не используется в lockstep»), Checksum Interval, Checksum Cross Platform Determinism, Input Delta Compression, Input Delay Min/Max/Ping Start (мс), Input Redundancy, Input Repeat Max Distance, Hard Tolerance, Min Time Correction Frames, Time Correction Rate, Session Start Timeout, Time Scale Min / Ping Min / Ping Max, Player Count, Input Fixed Size.

Два правила оттуда, которые надо соблюдать:

- **Checksum Interval в релизе ставить 0** — страница описывает его как «useful during development, set to zero for release».
- **В lockstep-режиме `InputDelayMin` — не меньше 10.**
- **Player Count можно оставить 0**: он перезаписывается `SessionRunner.Arguments.PlayerCount`, когда тот больше нуля.

**SimulationConfig — единственные явно опубликованные дефолты:**

| Параметр | Default |
|---|---|
| Thread Count | 2 |
| Checksum Snapshot History Length | 3 |
| Heap Page Shift | 15 (2^15 = 32 768 байт) |
| Heap Page Count | 256 |
| Heap Extra Count | 0 |
| | `TotalHeapSizeInBytes = (1 << HeapPageShift) * HeapPageCount` |

> **Данные не подтверждены.** Числа `InputDelayMax = 60`, `InputDelayPingStart = 100`, `InputRedundancy = 3` фигурировали в черновике со ссылкой на `doc-api.photonengine.com`, но этот домен отдаёт страницу bot-валидации вместо контента и **проверить его невозможно**. Любые дефолты SessionConfig сверяй по ассетам установленного SDK, а не по памяти.

### Реплеи и save-game

Со страницы [replay](https://doc.photonengine.com/quantum/current/manual/replay). `QuantumReplayFile` — класс, который «holds all relevant data to run a Quantum replay and can be serialized in JSON»; поля: `InputHistoryDeltaCompressed` / `InputHistoryLegacy`, `RuntimeConfigData`, `DeterministicConfig`, `LastTick`, `InitialTick`, `InitialFrameData`, `Checksums`, `AssetDatabaseData`.

Как это работает на практике:

- режим старта — `DeterministicGameMode.Replay`; `PlayerIsLocal` «won't work for replays», поэтому view-код должен сначала проверить `game.Session.GameMode`;
- запись — `QuantumGame.StartRecordingInput()` и `GetRecordedReplay()`;
- standalone-раннер: `--replay-path` (JSON реплея), `--lut-path` (папка LUT), опционально `--db-path` и `--checksum-path`;
- `InitialTick` + `InitialFrameData` нужны только если реплей начинается не с начала; `InitialTick` требует заданного `InitialFrameData`, а сам `InitialFrameData` «is used for save games for example»;
- `Checksums` — «Optional checksums. **Omit this for replays in production environments**»;
- чек-суммы из Unity не совпадают с чек-суммами Dotnet-раннера; для кросс-платформенной сверки включи `ChecksumCrossPlatformDeterminism` в SessionConfig.

> ⚠️ Две правки к черновику. Первая: **`ReplayFile` не объявлен deprecated** — страница просто читает `ReplayFile.text` (поле TextAsset в инспекторе раннера), а `QuantumReplayFile` — это класс десериализации. Это разные объекты, а не замена одного другим. Вторая: **`QuantumGame.CreateSavegame` на этой странице отсутствует** — ссылка была на недоступный `doc-api`; в документации save-game описан только через `InitialFrameData`.

---

## 26.4. Серверный авторитет: Quantum Plugin SDK и Photon Server

Здесь черновик давал неверный ответ («поднять Quantum у себя нельзя»), и это важно исправить.

**Quantum Plugin SDK** ([plugin-sdk/overview](https://doc.photonengine.com/quantum/current/addons/plugin-sdk/overview)) позволяет «create a custom Quantum server, test it locally and upload to an Photon Enterprise Cloud». Что он даёт: запуск симуляции на серверной стороне для доверенных результатов матча и серверные снапшоты для клиентов, которые подключаются поздно или переподключаются. Условия: доступен участникам **Gaming Circle и Industries Circle**, «Creating a Photon Enterprise Cloud involves additional costs», на скачивание нужно разрешение Photon. Локальный тест: «Starting a local Photon Server is only supported on Windows». Версии: **3.1.0 Preview, build 92, 30.10.2026** и **3.0.13, build 86, 05.08.2026**.

**Photon Server 5** (self-hosted, [photon-server-intro](https://doc.photonengine.com/server/current/getting-started/photon-server-intro)): «The Photon Core is written in native C++ for performance reasons», бизнес-логика — в .NET-рантайме внутри ядра, приложения на «C# or any other .NET language». Протоколы: reliable UDP (на базе eNET), Binary TCP, Web Sockets / Secure Web Sockets (в разделе архитектуры отдельно упомянут HTTP). «All applications are provided in C# source code», деплой — xcopy, «Photon Server does not provide a persistence layer» (своя БД), и главное: «**The Photon Server requires a valid license to run**».

Что принёс v5 ([whats-in-photon-5](https://doc.photonengine.com/server/v5/version-history/whats-in-photon-5)): NameServer, который до этого был только в облаке; готовые провайдеры аутентификации **Facebook, Steam, Oculus, Vive (Viveport)** (позже добавлены Facebook Gaming, Nintendo, Epic); per-peer лимиты `MessageRate` (default **5000**), `MessageDataRate` (default `int.MaxValue`), `MaxMessageSize` (default **512000**), `AuthTimeout` (default **20000 мс**); serialization protocol 1.8; переход со Schannel на OpenSSL с поддержкой TLS 1.2 и 1.3; «PhotonSocketServer is now available in 64bit only»; scheduled disconnect с дефолтной задержкой **5000 мс**; сняты CounterPublisher, проект MMO и StarDust.

> **Линия Photon Server — открытый вопрос.** Та же страница говорит: «Photon Server 5.x runs on .NET Framework (target `net471`)», а «Photon Server 6.x, built from the same code base, runs on .NET (formerly .NET Core)». То есть 5 — не обязательно текущая линия. Статус и актуальный билд 6.x подтвердить первоисточником не удалось; конкретной версии страница intro не называет (её метка — «SERVER 5»).

Практический вывод: **Photon Server — это бэкенд Realtime-класса** (свои комнаты, своя серверная логика), документация не описывает self-hosting Fusion или Quantum. Dedicated Server в Fusion — это твой headless-билд Unity в `GameMode.Server`, который всё равно нужно оркестрировать сторонними средствами за отдельные деньги. А для Quantum путь к серверному авторитету есть — через Plugin SDK на Enterprise Cloud.

---

## 26.5. PUN 2, PUN Classic, Bolt: статус и миграция

**PUN 2 — maintenance / LTS.** Дословно ([migration-notes](https://doc.photonengine.com/pun/current/getting-started/migration-notes)): «PUN 2 is in maintenance / LTS mode», «Aside from some fixes no further feature updates are planned», «Of course, existing PUN 2 projects will continue to run», «New projects should consider Fusion or Quantum». **Публичной даты end-of-life нет.**

> **Данные расходятся по версиям PUN 2.** Официальная [история версий](https://doc.photonengine.com/pun/current/reference/version-history) показывает максимум **v2.50 (11.03.2025)**. Форумный листинг Photon в Unity Discussions идёт дальше: **v2.56 (10.09.2026)** — «Support for Video SDK with new type of AppId», библиотеки до 4.1.8.23; v2.55 (03.07.2026), v2.54 (04.06.2026), v2.53 (02.06.2026) ([тред](https://discussions.unity.com/t/photon-unity-networking-pun-2/453757)). То есть официальный changelog отстаёт от анонсов, и формулировка «только фиксы» уже не вполне точна: v2.56 добавила поддержку нового продукта.

**PUN Classic (v1.x) и Bolt — outdated.** Дословно ([pun/v1 intro](https://doc.photonengine.com/pun/v1/getting-started/pun-intro)): «PUN Classic (v1.x) and Bolt are outdated and will not receive further updates», существующие проекты продолжат работать, новым рекомендуются Fusion или Quantum. **Никогда не предлагай Bolt для нового проекта.**

### Карта миграции PUN 2 → Fusion

Таблица дословно со страницы [coming-from-pun2](https://doc.photonengine.com/fusion/current/getting-started/migration/coming-from-pun2) — черновик передавал её вольно, здесь оригинальные формулировки:

| PUN | Fusion |
|---|---|
| `PhotonNetwork` | `SimulationBehaviour.Runner` и `SimulationBehaviour.Object` |
| `MonoBehaviourPunCallbacks` | `SimulationBehaviour` и `NetworkBehaviour` |
| `PhotonNetwork.AddCallbackTarget(this)` | N/A (automatic) |
| `PhotonNetwork.Instantiate()` | `Runner.Spawn()` |
| `PhotonView` | `NetworkObject` |
| `IPunObservable.OnPhotonSerializeView()` | C# auto-implemented properties and Input Sync |
| `PhotonTransformView` | `NetworkTransform` |
| `PhotonRigidbodyView` | `NetworkRigidbody` |
| `PhotonAnimatorView` | `NetworkMecanimAnimator` |
| N/A | `NetworkCharacterController` |
| `[PunRPC]` | `[Rpc]` |
| `PhotonNetwork.LoadLevel()` | `Runner.SetActiveScene()` |

Главный тезис страницы, дословно: «Most concepts that exist in PUN are transferable to Fusion Shared mode but **NOT** to Fusion Server / Host mode». То есть **PUN-проект мигрирует в Shared, а не в Host mode, которому посвящён весь курс.** Если целиться в Host — это не миграция, а переписывание модели авторитета.

Ещё оттуда: смена свойств комнаты/игрока вместо PUN-события `PropertyChanged` ловится через «`OnChangeRender` or a `ChangeDetector`» (см. гл. [02](02-chapter.md), [05](05-chapter.md)); «бесхозные» объекты мастера настраиваются на каждом `NetworkObject`: «When `IsMasterClientObject` is checked on the `NetworkObject`» — StateAuthority переходит к новому мастеру, «When `Destroy When State Authority Leaves` is checked the object is destroyed instead».

> **Данные расходятся по нумерации `PlayerRef` — не считай ни одну версию универсальным правилом.**
> Страница миграции (контекст — Shared mode): «In Fusion, the `PlayerRef` starts at 0 and goes up to MaxPlayers - 1» и «If there is a host for a game, that player will always get MaxPlayers - 1 as `PlayerRef` value».
> Основная страница [playerref](https://doc.photonengine.com/fusion/current/manual/playerref): «Each client (including the Host) is given a unique `PlayerRef` **starting at 1**», значения «are never re-used», переподключившийся клиент получает **новый** `PlayerRef`, верхняя граница не заявлена.
> Практическое правило: **не пиши логику, зависящую от конкретного числового значения `PlayerRef`.** Для устойчивой идентичности между переподключениями используй `StartGameArgs.PlayerUniqueId` (Fusion 2.1; первое подключение с данным ID создаёт `PlayerRef`, последующие его переиспользуют). В релиз-нотах `PlayerUniqueId` впервые появляется в nightly build 1240 (08.08.2025): «`StartGameArgs.PlayerUniqueId` enables reconnects to maintain the same `PlayerRef`» ([release-notes-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1)).

---

## 26.6. CCU-модель: как реально считают деньги

Всё в этом разделе — со страницы [pricing](https://doc.photonengine.com/photon/current/pricing), если не указано иное.

**CCU = Concurrently Connected Users**, считается по всему приложению: все комнаты и все регионы вместе. Это **не** лимит на комнату.

**Биллинг считает Peak CCU — сумму месячных пиков по каждому региону.** Пример из документации: «EU peak 420 CCU + US peak 230 CCU + other regions 0 → CCU total = 650, matching a 1,000 CCU plan». Пики в разное время суток всё равно складываются. Отсюда прямое следствие: **чем больше регионов разрешено, тем больше счёт при том же реальном онлайне.**

**Трафик:** «3 GB per peak CCU per month» в каждом плане (1000 CCU → 3 ТБ). Ставки перерасхода (с продуктовых страниц): **$0.05/ГБ** — Europe, Canada, East/RU/RUE, USA East, USA West; **$0.10/ГБ** — Asia, Australia, China, India, Japan, South Africa, South America, South Korea, Turkey, UAE ([Fusion pricing](https://www.photonengine.com/en-US/Fusion/pricing)).

**CCU Burst:** на планах от 500 CCU и на Premium «no player is ever cut off», превышение биллится как overage; после первого burst'а есть «48 hours to upgrade your subscription», иначе Photon вправе зарезать CCU «at its sole discretion». Планы 20 и 100 CCU — жёсткий cap, лишние клиенты получают дисконнект с причиной **`MaxCcuReached`**.

**Два бесплатных плана — не путать:**

- **20 CCU Development** — «licensed for development and non-commercial use only», публиковать игру нельзя.
- **Free 100 CCU («Free for Launch»)** — коммерческий запуск разрешён, но: одно приложение на клиента, только игры на Fusion/Quantum, **нельзя совместить с подпиской** («only one subscription can be active per AppId»), стекается с одноразовым 100-CCU add-on до 200 CCU суммарно, burst'а нет. **Монетизация только рекламой тоже считается коммерческим использованием.**

**Per-AppId:** одна активная подписка на AppId; один одноразовый 100-CCU add-on на AppId за раз, срок 12 месяцев, без автопродления.

**MAU** — только планировочный ориентир, ничем не ограничивается: «1 CCU ≈ 20 DAU ≈ 400 MAU». Для приложений с концентрированной во времени активностью (пример документации — классные комнаты) CCU на MAU нужно больше.

### Цены, зафиксированные на 2026-11-01

| Тариф | Fusion / Quantum | Realtime / Voice | Chat |
|---|---|---|---|
| 20 CCU dev | free, 60 ГБ | free, 60 ГБ | free, **10 ГБ** |
| Free 100 «for Launch» | **есть**, 0.3 ТБ | **нет** | **нет** |
| 100 CCU one-time (12 мес) | $95, 0.3 ТБ | $95, 0.3 ТБ | $45, **50 ГБ** |
| 500 CCU /мес | $125, 1.5 ТБ | $95, 1.5 ТБ | $45, **0.3 ТБ** |
| 1000 CCU /мес | $250, 3.0 ТБ | $185, 3.0 ТБ | $85, **0.5 ТБ** |
| 2000 CCU /мес | $500, 6.0 ТБ | $370, 6.0 ТБ | $170, **1.0 ТБ** |
| Premium, за CCU | $0.50, минимум $1000/мес | $0.29, минимум $580/мес | $0.14, минимум $280/мес |

Источники по столбцам: [Fusion](https://www.photonengine.com/en-US/Fusion/pricing), [Quantum](https://www.photonengine.com/en-US/Quantum/pricing), [Realtime](https://www.photonengine.com/en-US/pricing), [Voice](https://www.photonengine.com/en-US/Voice/pricing), [Chat](https://www.photonengine.com/en-US/Chat/pricing).

Три вещи, которые надо прочитать в этой таблице:

1. **Fusion и Quantum тарифицируются идентично** по всем тирам. Проверено по двум разным страницам.
2. **Realtime вдвое дешевле за CCU** ($0.29 против $0.50). Если репликация объектов не нужна — чистый лобби-сервис, пошаговая игра, обмен событиями — Fusion стоит двойную цену за абстракцию, которой проект не пользуется.
3. **У Chat трафика кардинально меньше** — 0.3/0.5/1.0 ТБ против 1.5/3.0/6.0 ТБ. Все платные тиры Chat помечены «For Games Only».

**Premium Cloud:** «minimum commitment of 2,000 CCU», минимальный платёж уже включает 2000 CCU и 6.0 ТБ, автоскейл до 50 000 CCU, burst — выше 50k, 3.0 ГБ трафика на peak CCU (Chat — 0.5 ГБ на peak CCU).

**Enterprise Cloud:** минимум 2000 CCU без лимита CCU, «Dedicated bare-metal clusters», SLA с «guaranteed uptime & response time, 24/7 NOC», приоритетная поддержка. Публичных цен нет. Это же единственный путь к кастомным серверным плагинам для Fusion Shared и к Quantum Plugin SDK.

> ⚠️ Пулится ли CCU между разными приложениями одного аккаунта — **официально не документировано**: страница pricing формулирует все правила per AppId, но прямо этого не утверждает.

---

## 26.7. Регионы, Best Region, AppId / AppVersion

Всё — со страницы [regions](https://doc.photonengine.com/realtime/current/connection-and-authentication/regions).

**Коды регионов (Photon Cloud for Gaming — Quantum, Fusion, Voice, Realtime, PUN)**, регистр не важен: `asia` Singapore, `au` Sydney, `cae` Montreal, `cn` Shanghai, `eu` Amsterdam, `hk` Hong Kong, `in` Chennai, `jp` Tokyo, `kr` Seoul, `sa` Sao Paulo, `tr` Istanbul, `uae` Dubai, `us` Washington D.C., `usw` San José, `ussc` Dallas, `za` Johannesburg.

**Photon Industries Premium Cloud — свой список:** `asia`, `au`, `eu`, `in`, `jp`, `za`, `sa`, `kr`, `us`, `usw`.

**Регионы не вечны:** страница прямо предупреждает, что за время жизни проекта «new regions may be added or old ones may be deprecated and removed». В сочетании с кэшем Best Region и 10-минутной пропагацией Allowlist это операционный риск, а не теория.

**Best Region (ping-based):**

```csharp
// Из документации Photon (Realtime), НЕ из учебного проекта.
// Клиент получает список регионов от Name Server, затем:
RegionHandler.PingMinimumOfRegions();
// Результат сворачивается в строку, которую надо сохранить на устройстве:
string summary = regionHandler.SummaryToCache;
```

Правила кэша: с сохранённым summary клиент перепингует **все** регионы, если список регионов изменился или если прежний лучший регион отвечает «at least 1.5x slower» своего референсного пинга. Без кэша пингуются все регионы, и первый коннект дольше. И отдельно задокументировано: **результат не детерминирован** — при близких или одинаковых замерах выбор выглядит случайным, одно и то же устройство в разных попытках может уйти в разные регионы. Это значит, что два игрока с похожим пингом могут просто не увидеть друг друга.

**Region Allowlist:** настраивается в дашборде на приложение («Edit Allowlist»), формат — коды через точку с запятой, например `eu;us`, регистр не важен, нераспознанные коды игнорируются. **Пустая или некорректная строка (например `;;;`) разрешает ВСЕ регионы** — типовая ошибка, которая выглядит как ограничение, а работает как «открыто всё», и прилетает в счёт лишними региональными пиками. Изменения доезжают до клиентов в пределах 10 минут.

**FixedRegion:**

```csharp
// Из документации Photon (Realtime), НЕ из учебного проекта.
appSettings.FixedRegion = "eu";
client.ConnectUsingSettings(appSettings);
// Клиент не запрашивает список регионов и не пингует — коннект быстрее.
```

Минус: вкомпилированный регион не меняется без обновления билда.

**Китай (`cn`):** доступ разблокируется для AppId через обращение к Photon по email; платные планы в дашборде не купить — нужен тикет и индивидуальный расчёт; Name Server — **`ns.photonengine.cn`**; нужен **отдельный билд**, отдельный AppId рекомендован; **Photon Voice в Китае недоступен** (Quantum, Fusion, Realtime, PUN, Chat — доступны); бесплатно до 20 CCU для некоммерческой разработки, единственная предлагаемая подписка — 500 CCU; изменения дашборда не попадают в китайские кэши автоматически (надо писать Photon); подключения извне материкового Китая могут работать плохо.

**AppId и AppVersion** ([glossary](https://doc.photonengine.com/realtime/current/reference/glossary)): AppId — «the main identifier for a Title and its Virtual Application»; AppVersion «is used to separate builds and players in matchmaking into compatible multiplayer builds»; «Internally, games are separated per AppId and GameVersion». В PUN формат — `{GameVersion}_{PunVersion}`, и менять GameVersion в `PhotonServerSettings` надо «when there are breaking changes in the game's network- and gameplay-logic to separate players»; если смешиваешь Realtime- и PUN-клиентов, AppVersion у них должен совпадать ([pun/feature-overview](https://doc.photonengine.com/pun/current/getting-started/feature-overview)).

**Директива:** меняй AppVersion при каждом несовместимом изменении сетевого протокола или схемы `[Networked]`-состояния. Иначе старые билды попадут в комнаты новых и десинхронизируются — а в курсе (гл. [09](09-chapter.md)) настраивается только AppId.

---

## 26.8. Chat и Voice

**Chat** ([chat-intro](https://doc.photonengine.com/chat/current/getting-started/chat-intro)). Что подтверждено страницей: **отдельное приложение и отдельный AppId** — «Photon Chat applications need a distinct application and AppId to connect to»; регионы, названные на этой странице — EU, US, ASIA (страница regions добавляет `cn`); подключение по AppId + произвольной строке версии + имени пользователя (желательно уникальному); нужно непрерывно вызывать `Service()`, чтобы держать соединение; подписки требуют только публичные каналы, каналы создаются при первой подписке, подписка обновляет `ChatClient.PublicChannels`, публиковать можно только после подписки; приватные сообщения отправляются по имени пользователя без подписки и кэшируются в `ChatClient.PrivateChannels`; «complex messages» (не только строки — например, инвайты); статус через `UserStatus.Online` плюс свои целочисленные статусы и отслеживание друзей через `OnStatusUpdate`; все публичные и приватные сообщения кэшируются на клиенте автоматически, и на длинных активных каналах историю «might want to trim» самому.

> ⚠️ Чего на этой странице **нет** (черновик приписывал): «неограниченные каналы», «надёжная доставка», «шифрование», «буфер истории на канал» как заявленная фича, а также утверждение, что Chat работает без других SDK Photon. Лимиты Chat — максимум каналов, максимальный размер сообщения, глубина и срок хранения истории — **официально на этой странице не документированы.**

**Voice** ([photon-products](https://doc.photonengine.com/photon/current/photon-products)): кодек — «state-of-the-art Opus codec», серверные интеграции модерации — GGWP, Voice Patrol, ToxMod by Modulate.

**Интеграция с Fusion** ([voice-for-fusion](https://doc.photonengine.com/voice/current/getting-started/voice-for-fusion)):

- **Fusion 2.1:** «Starting with Fusion 2.1 the Voice integration is installed directly from the Fusion Hub» — `Tools/Fusion/Fusion Hub` → вкладка Addons → Install на записи Voice.
- **Ручной путь:** импорт Photon Voice 2 через UPM или Asset Store, при этом снять галки с `Photon\PhotonChat` и `Photon\PhotonUnityNetworking`; затем в Network Project Config в `Assemblies To Weave` добавить **`PhotonVoice.Fusion`** и нажать Apply.
- **Префабы:** «Add a `VoiceNetworkObject` to the Fusion prefab object that has a `NetworkBehaviour` attached to it», затем обязательно Fusion → **Rebuild Object Table**. `VoiceNetworkObject` сам стартует Recorder из иерархии префаба (иначе — `FusionVoiceClient.PrimaryRecorder`; если используешь префабы с `VoiceNetworkObject`, снимай `UsePrimaryRecorder`) и Speaker из иерархии или из `FusionVoiceClient.SpeakerPrefab` (тот обязан содержать компонент Speaker).

> ⚠️ **Исправление черновика про аутентификацию.** Неверно, что значения аутентификации между Fusion и Voice «не шарятся». Правильно: **шарятся опционально** — `FusionVoiceClient.UseFusionAuthValues`, «if true, the Voice client will use the same `AuthenticationValues` as Fusion's`PhotonNetwork.AuthValues`», и эта опция «requires the Voice application to have the same custom authentication dashboard configuration as the Fusion application». **Вручную** через `FusionVoiceClient.Client.AuthValues` их надо задавать только тогда, когда провайдер выдаёт одноразовый nonce на запрос — в этом случае «the authentication values cannot be shared between Fusion and Photon Voice».

> **Данные расходятся по версии Voice.** Страница [Fusion SDK & Download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) требует **Voice 2.64**: «Unity Voice SDK Realtime5 2.64» для Fusion 2.1 («Fusion 2.1 requires Realtime 5. (not compatible with Fusion 2.0)») и «Unity Voice SDK 2.64» для Fusion 2.0 («not compatible with Fusion 2.1») — то есть сборки взаимно несовместимы. При этом официальная [история версий Photon Voice 2](https://doc.photonengine.com/voice/current/reference/version-history) версии 2.64 **не содержит вообще**: максимум там — **2.58 (03.03.2025)**, далее 2.57 (13.12.2024), 2.56 (19.08.2024), 2.55 (26.02.2024). Расхождение — внутри самой документации Photon. Версию, которую требует твоя линия Fusion, бери со страницы Fusion SDK & Download.
>
> Breaking changes в Voice 2.50 (18.10.2022) подтверждены: переименования `VoiceConnection` → `UnityVoiceClient`, «BREAKING: renamed PhotonVoiceNetwork -> PunVoiceClient», удаление `SpeakerFactory`, изменения API Recorder и Speaker, плюс migration guide. Привязка миграции 2.50 к файлу `changes-voice.txt` — **ошибка**: этот файл упомянут в записи v2.7 (17.05.2019): «changelog.txt renamed to changes-voice.txt».

**Video SDK** — пятый продукт экосистемы, которого в курсе нет: «enables the transmission of video and screen share in multiuser situations», доступен **только участникам Circle** ([Industries](https://www.photonengine.com/Industries)). Косвенное подтверждение живости: PUN 2.56 добавил «Support for Video SDK with new type of AppId».

**Circle-членство — вторая ось доступа помимо CCU-тарифов.** Есть Gaming Circle (для игр) и Industries Circle (для неигровых применений). Industries Circle даёт доступ ко всем продуктам, сервисам и поддержке Photon, гибкие лицензионные опции, ранний доступ к сэмплам, эксклюзивный Voice/Video SDK для HoloLens 2 (UWP ARM64), приватный канал к инженерам, enterprise-хостинг и библиотеку аддонов. Публичных цен на членство страница не даёт. Quantum Plugin SDK доступен именно участникам Circle.

---

## Чего по этой теме нет в учебном курсе

Курс покрывает **один квадрант из четырёх** — Client Host на Fusion 2.0.5 build 920 / Unity 2022.3.42f1 ([00-README](00-README.md)).

1. **Quantum отсутствует как класс.** В [00-README](00-README.md) есть таблица трёх режимов Fusion, но четвёртой топологии Photon — детерминированного predict/rollback — в курсе нет. Для файтинга, RTS, автобатлера, esports-проекта или любой игры, которой нужны реплеи и античит без выделенных серверов, знания курса ведут к заведомо неправильному выбору.
2. **Денег нет вообще.** Ни CCU-модели, ни Peak CCU как суммы региональных пиков, ни того, что план 20 CCU (на котором почти наверняка велась вся курсовая разработка) **запрещает публикацию**, ни того, что бесплатные 100 CCU доступны только проектам на Fusion/Quantum.
3. **Симптом `MaxCcuReached`.** Выглядит как сетевой баг, является исчерпанным тарифом. В таблице «симптом → глава» ([91-decision-tables.md](91-decision-tables.md)) его нет.
4. **Регионы.** Курс не касается недетерминированности Best Region, кэша `SummaryToCache`, правила репинга при деградации в 1.5x, `FixedRegion`, Region Allowlist как инструмента управления счётом, возможности удаления регионов и особого режима Китая.
5. **AppVersion.** Курс учит ставить AppId (гл. [09](09-chapter.md)), но не изолировать несовместимые билды.
6. **Версии.** Курс на 2.0.5 (build 920, февраль 2025). На 2026-11-01: 2.1.3 (build 2390, 17.09.2026) и 2.0.13 (build 2379, 16.09.2026), поддерживаемые Unity включают 6.0.x и 6.3.x. Курсовой код, трогающий `PlayerRef.MasterClient`, на 2.1 **не скомпилируется**.
7. **Voice и Chat.** В курсе нет. Chat требует отдельного AppId и отдельного CCU-счёта; Voice с Fusion 2.1 ставится из Fusion Hub и требует совпадающей конфигурации custom authentication в двух приложениях дашборда, если шарить AuthValues.
8. **Миграция с PUN 2.** Курс начинается с Fusion. Критично: PUN переносится в **Shared**, а не в Host mode.
9. **Self-hosting и серверный авторитет.** Курс целиком на Photon Cloud. Для Fusion self-hosting Photon Server не предусмотрен; для Quantum есть Plugin SDK на Enterprise Cloud.
10. **Глава [15](15-chapter.md)** использует Realtime 5.1.5 напрямую — но без контекста того, что Realtime вдвое дешевле за CCU и что клиент архитектурно не может делать матчмейкинг, находясь в комнате.

---

## Решение: что брать и когда

| Вопрос | Ответ | Почему |
|---|---|---|
| Репликация объектов вообще нужна? | **Нет** → Realtime 5 (или Chat для текста) | $0.29 против $0.50 за CCU, проще |
| Важен кадр, нужен честный кроссплей, реплеи, античит без серверов? | **Quantum 3** | детерминизм; трафик не зависит от числа сущностей; реплей почти бесплатен |
| Mobile/web, кооп, социальные и сессионные игры? | **Fusion Shared** | «any platform, ideal for mobile and web, cost effective» |
| Миграция с PUN 2? | **Fusion Shared** (не Host) | «NOT to Fusion Server / Host mode» |
| Нужен авторитет и предсказание, серверов не хочется? | **Fusion Host** (путь курса) | дёшево; но релей и нестабильность хоста на мобилках |
| Нужен cheat-proof и максимум производительности, бюджет есть? | **Fusion Server** + сторонняя оркестрация | Photon оркестрацию не даёт, это «additional costs» |
| Нужны кастомные серверные плагины? | **Enterprise Cloud** (Fusion Shared или Quantum) | минимум 2000 CCU |
| Нужен серверный авторитет для Quantum? | **Quantum Plugin SDK** на Enterprise Cloud | Circle-членство, локальный тест только на Windows |
| Нужна своя серверная логика Realtime-класса? | **Photon Server** (лицензия, своя БД) | Fusion/Quantum так не поднять |
| Голос? | **Voice 2** | версию брать со страницы Fusion SDK; в Китае недоступен |
| Текстовый чат с историей и приватными сообщениями? | **Chat** | отдельный AppId, 4 региона, меньше трафика в тарифе |
| Видео / шеринг экрана? | **Video SDK** | только участникам Circle |
| Новый проект на PUN 2 / Bolt? | **Никогда** | PUN 2 — maintenance/LTS; Bolt и PUN Classic — «outdated» |

---

## Чеклист

1. Определи топологию до первой строки кода: открой [photon-products](https://doc.photonengine.com/photon/current/photon-products) и выбери квадрант по геймплею, затем по платформам, затем по стоимости лайва.
2. Если проекту не нужна репликация объектов — возьми Realtime 5 и сэкономь половину стоимости CCU.
3. Если жанр frame-sensitive, нужны реплеи или античит без выделенных серверов — возьми Quantum 3 и заложи время на Qtn DSL, ECS и детерминированную физику.
4. Проверь версии перед установкой: Fusion по [sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download), Quantum по [release-notes](https://doc.photonengine.com/quantum/current/getting-started/release-notes), Realtime по [sdk-download](https://doc.photonengine.com/realtime/current/getting-started/sdk-download). Не доверяй номерам по памяти.
5. Обновляясь с 2.0.x на 2.1.x — сначала поднимись до последней 2.0.x, затем правь `PlayerRef.MasterClient` → `NetworkRunner.GetMasterClient()`.
6. Поставь `Project Settings > Editor > Asset Serialization > Mode = Force Text` — это требование Fusion.
7. Заполни Region Allowlist явной строкой (`eu;us`) и проверь, что она не пустая и без опечаток; подожди 10 минут до вступления в силу.
8. Сохраняй `RegionHandler.SummaryToCache` на устройстве; для стабильного пула игроков рассмотри `AppSettings.FixedRegion`.
9. Заведи процедуру смены `AppVersion` / GameVersion при каждом несовместимом изменении сетевого протокола или схемы `[Networked]`.
10. Проверь тарифный план перед релизом: на 20 CCU публиковаться нельзя; бесплатные 100 CCU — только Fusion/Quantum, одно приложение, без подписки.
11. Добавь `MaxCcuReached` в список «не баг кода, а тариф» в обработчике дисконнектов (гл. [09](09-chapter.md)).
12. Если план ниже 500 CCU — спланируй апгрейд заранее: burst'а там нет, живым игрокам прилетит дисконнект.
13. Не пиши логику, зависящую от числового значения `PlayerRef`; для идентичности при переподключении используй `StartGameArgs.PlayerUniqueId` (Fusion 2.1).
14. Для Quantum-релиза поставь Checksum Interval = 0 и убери `Checksums` и `AssetDatabaseData` из реплей-файлов.
15. Подключая Voice: возьми версию со страницы Fusion SDK, поставь `VoiceNetworkObject` на префаб с `NetworkBehaviour`, сделай Rebuild Object Table, и реши вопрос с AuthValues (`UseFusionAuthValues` плюс одинаковая конфигурация custom auth в двух приложениях дашборда — либо ручная установка при nonce-провайдере).
16. Заводя Chat — создай для него отдельное приложение и отдельный AppId и учти, что трафика в его тарифах в 3–6 раз меньше.
17. Планируя Китай — заложи отдельный билд, отдельный AppId, `ns.photonengine.cn`, обращение к Photon для разблокировки и отсутствие Voice.
18. Любые дефолты Quantum SessionConfig сверяй по ассетам установленного SDK: документация их не публикует, а `doc-api.photonengine.com` недоступен для проверки.

---

## Источники

**Выбор продукта и топологии**
- [doc.photonengine.com/photon/current/photon-products](https://doc.photonengine.com/photon/current/photon-products) — Photon Quadrant, четыре топологии, полные списки игр, приоритет выбора, релей в Dedicated Server, плагины на Enterprise Cloud для Shared и Quantum, Opus и партнёры модерации Voice, «all using Realtime as their foundation».

**Тарифы и CCU**
- [doc.photonengine.com/photon/current/pricing](https://doc.photonengine.com/photon/current/pricing) — определение CCU, Peak CCU как сумма региональных пиков (пример 420+230=650), 3 ГБ на peak CCU, CCU Burst и 48 часов, `MaxCcuReached`, ограничения free-планов, запрет совмещения Free 100 с подпиской, ad-only как коммерческое использование, per-AppId правила, Premium минимум 2000 CCU, Enterprise (bare-metal, SLA, 24/7 NOC), 1 CCU ≈ 20 DAU ≈ 400 MAU.
- [photonengine.com/en-US/Fusion/pricing](https://www.photonengine.com/en-US/Fusion/pricing) — тиры Fusion, Free 100 «for Launch», ставки перерасхода по регионам.
- [photonengine.com/en-US/Quantum/pricing](https://www.photonengine.com/en-US/Quantum/pricing) — тиры Quantum (идентичны Fusion).
- [photonengine.com/en-US/pricing](https://www.photonengine.com/en-US/pricing) — тиры Realtime, Premium $0.29 / минимум $580.
- [photonengine.com/en-US/Voice/pricing](https://www.photonengine.com/en-US/Voice/pricing) — тиры Voice, отсутствие Free 100.
- [photonengine.com/en-US/Chat/pricing](https://www.photonengine.com/en-US/Chat/pricing) — тиры Chat, малый объём трафика, пометки «For Games Only».

**Realtime, регионы, AppId**
- [realtime/getting-started/realtime-intro](https://doc.photonengine.com/realtime/current/getting-started/realtime-intro) — rooms/lobbies, random matchmaking, SQL Lobby, Matchmaking Tickets, Events, Custom Properties, три типа серверов, «one server type at any time», матчмейкинг только вне комнаты.
- [realtime/connection-and-authentication/regions](https://doc.photonengine.com/realtime/current/connection-and-authentication/regions) — коды регионов, список Industries Premium Cloud, возможное удаление регионов, Best Region и `SummaryToCache`, правило 1.5x, недетерминизм, Allowlist и 10 минут, `FixedRegion`, китайская специфика.
- [realtime/getting-started/sdk-download](https://doc.photonengine.com/realtime/current/getting-started/sdk-download) — Realtime 5.1.20.
- [realtime/reference/glossary](https://doc.photonengine.com/realtime/current/reference/glossary) — определения AppId, AppVersion, GameVersion, Virtual Application, Region, CCU.

**Fusion: версии и миграция**
- [fusion/getting-started/sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — 2.1.3 build 2390 (17.09.2026), 2.0.13 build 2379 (16.09.2026), порядок апгрейда, поддерживаемые Unity, Force Text, требуемые сборки Voice 2.64 и их несовместимость.
- [fusion/v2/release-notes-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1) — 2.1.0 build 1762 (16.02.2026): переименование `PlayerRef.MasterClient` → `InternalMasterClientIdentifier`, `NetworkRunner.GetMasterClient`, `StartGameArgs.PlayerUniqueId`.
- [fusion/manual/playerref](https://doc.photonengine.com/fusion/current/manual/playerref) — нумерация с 1, значения не переиспользуются, `PlayerUniqueId` (Fusion 2.1).
- [fusion/manual/shared-mode-master-client](https://doc.photonengine.com/fusion/current/manual/shared-mode-master-client) — `IsSharedModeMasterClient`, `SetMasterClient`, `GetMasterClient` «available starting with Fusion 2.1», `IsMasterClientObject`.
- [fusion/getting-started/migration/coming-from-pun2](https://doc.photonengine.com/fusion/current/getting-started/migration/coming-from-pun2) — таблица миграции, «NOT to Fusion Server / Host mode», нумерация 0..MaxPlayers-1 и хост = MaxPlayers-1, `IsMasterClientObject`, `Destroy When State Authority Leaves`, `OnChangeRender`/`ChangeDetector`.

**PUN и legacy**
- [pun/getting-started/migration-notes](https://doc.photonengine.com/pun/current/getting-started/migration-notes) — «PUN 2 is in maintenance / LTS mode», отсутствие новых фич, рекомендация Fusion/Quantum.
- [pun/reference/version-history](https://doc.photonengine.com/pun/current/reference/version-history) — официальный максимум v2.50 (11.03.2025).
- [discussions.unity.com/t/photon-unity-networking-pun-2](https://discussions.unity.com/t/photon-unity-networking-pun-2/453757) — анонс v2.56 (10.09.2026) с поддержкой Video SDK, v2.55/2.54/2.53.
- [pun/v1/getting-started/pun-intro](https://doc.photonengine.com/pun/v1/getting-started/pun-intro) — «PUN Classic (v1.x) and Bolt are outdated and will not receive further updates».
- [pun/getting-started/feature-overview](https://doc.photonengine.com/pun/current/getting-started/feature-overview) — когда менять GameVersion, формат `{GameVersion}_{PunVersion}`.

**Quantum**
- [quantum/quantum-intro](https://doc.photonengine.com/quantum/current/quantum-intro) — up to 128 players, game-agnostic сервер, клиенты не ждут медленных, sparse-set ECS, Qtn DSL, системы и Frame, развязка с Unity.
- [quantum/getting-started/release-notes](https://doc.photonengine.com/quantum/current/getting-started/release-notes) — 3.0.14 build 2241 (05.10.2026), Realtime 5.1.20, минимум Unity 2021 LTS, фиксы под Unity 6.x.
- [quantum/getting-started/preview-3-1/sdk-download](https://doc.photonengine.com/quantum/current/getting-started/preview-3-1/sdk-download) — 3.1.0 Preview build 2279 (30.10.2026), предупреждение «not intended to be used for live or released games», table components, `ProjectedGaussSeidel`, новое API команд.
- [quantum/manual/config-files](https://doc.photonengine.com/quantum/current/manual/config-files) — список SessionConfig без дефолтов, дефолты SimulationConfig, Checksum Interval = 0 для релиза, Rollback Window, `SessionRunner.Arguments.PlayerCount`, lockstep `InputDelayMin >= 10`.
- [quantum/manual/replay](https://doc.photonengine.com/quantum/current/manual/replay) — поля `QuantumReplayFile`, `DeterministicGameMode.Replay`, `StartRecordingInput`/`GetRecordedReplay`, аргументы раннера, `InitialTick`/`InitialFrameData`, чек-суммы вне продакшена, `ChecksumCrossPlatformDeterminism`.
- [quantum/addons/plugin-sdk/overview](https://doc.photonengine.com/quantum/current/addons/plugin-sdk/overview) — свой Quantum-сервер, trusted results, серверные снапшоты, Circle-членство и Enterprise Cloud, локальный сервер только на Windows, 3.1.0 Preview build 92 (30.10.2026) и 3.0.13 build 86 (05.08.2026).

**Voice, Chat, Video**
- [voice/getting-started/voice-for-fusion](https://doc.photonengine.com/voice/current/getting-started/voice-for-fusion) — `UseFusionAuthValues` и требование одинаковой конфигурации custom auth, ручные AuthValues при nonce-провайдерах, установка из Fusion Hub в 2.1, ручной импорт и `PhotonVoice.Fusion` в Assemblies To Weave, `VoiceNetworkObject` + `NetworkBehaviour` + Rebuild Object Table, Recorder/Speaker.
- [voice/reference/version-history](https://doc.photonengine.com/voice/current/reference/version-history) — максимум 2.58 (03.03.2025), отсутствие 2.64, breaking changes 2.50, `changes-voice.txt` в записи v2.7.
- [chat/getting-started/chat-intro](https://doc.photonengine.com/chat/current/getting-started/chat-intro) — отдельный AppId, `Service()`, подписки и создание каналов, `PublicChannels`/`PrivateChannels`, complex messages, статусы и друзья, совет подрезать историю.
- [photonengine.com/Industries](https://www.photonengine.com/Industries) — Video SDK и его доступность только участникам Circle, состав Industries Circle, наличие Gaming Circle.

**Photon Server**
- [server/getting-started/photon-server-intro](https://doc.photonengine.com/server/current/getting-started/photon-server-intro) — C++ ядро и .NET-рантайм, протоколы, исходники приложений на C#, лицензия, отсутствие персистентности, xcopy-деплой.
- [server/v5/version-history/whats-in-photon-5](https://doc.photonengine.com/server/v5/version-history/whats-in-photon-5) — NameServer на self-hosted, провайдеры аутентификации по именам, per-peer лимиты с дефолтами, protocol 1.8, OpenSSL и TLS 1.2/1.3, только 64 бита, scheduled disconnect, снятые приложения, 5.x на .NET Framework net471 против 6.x на .NET.

**Не удалось проверить**
- `doc-api.photonengine.com` (API-референс Quantum: `DeterministicSessionConfig`, `QuantumReplayFile`, `QuantumGame`) — домен отдаёт страницу bot-валидации вместо контента. Все числа, опиравшиеся только на него, из главы удалены.
- Актуальный билд и статус линии Photon Server 6.x — официального подтверждения на 2026 год не найдено.
- WebSearch в этой сессии был исчерпан, поэтому статус Photon Bolt подтверждён не его собственной страницей (она отдаёт 404), а страницей PUN Classic, где оба продукта названы outdated.

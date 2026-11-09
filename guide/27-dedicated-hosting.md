# Глава 27. Dedicated server и хостинг

> **TL;DR для агента**
>
> 1. **Dedicated server в Fusion — это `GameMode.Server`**, peer без локального игрока с «full and exclusive State Authority over all objects» ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)). Клиенты только присылают input и просят изменения через RPC.
> 2. **Переход на свой сервер НЕ убирает Photon Cloud.** Матчмейкинг, реестр сессий и лобби остаются у Photon: «For a Fusion server, it is imperative to connect it to a Photon Cloud Region» ([Gameye](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server-gameye)). Архитектура всегда гибридная: матчмейкинг у Photon, игровой трафик — напрямую на ваш сервер.
> 3. **Две строчки, без которых ничего не работает:** `Address = NetAddress.Any(port)` (бинд ровно `0.0.0.0`) и `CustomPublicAddress = NetAddress.CreateFromIpPort(publicIp, publicPort)` из переменных окружения провайдера ([matchmaking-api](https://doc.photonengine.com/fusion/current/manual/matchmaking/matchmaking-api)).
> 4. **Третий путь, про который почти никто не знает:** официальный **Fusion Plugin SDK** (только Fusion 2.1.x, топология Shared, деплой на Photon Enterprise Cloud) — серверная авторитетность без собственного хостинга ([plugin-sdk](https://doc.photonengine.com/fusion/current/addons/plugin-sdk/plugin-sdk-overview-2.1)).
> 5. **Рынок хостинга перетряхнуло в 2026.** Multiplay by Unity депрекейтнут с 1 апреля 2026 ([status.unity.com](https://status.unity.com/info_notices/362941)), Hathora выключена 5 мая 2026 ([hathora.ghost.io](https://hathora.ghost.io/hathora-is-joining-fireworks-ai/)). Любой туториал 2024–2025 по провайдерам — мёртвый код.

---

## Зачем это знать

Весь учебный курс — это Host Mode: один из игроков поднимает `GameMode.AutoHostOrClient`, он же State Authority, он же клиент (см. [гл. 01](01-chapter.md), [гл. 09](09-chapter.md)). Это нормальный продакшн-режим, и для кооператива на 4–8 человек он правильный. Но у него два встроенных ограничения, которые Photon фиксирует открытым текстом: «A rogue host can cheat», и NAT punch-through срабатывает не всегда — «This is a sporadic occurrence and only happens to approximately 1 in 10 players», остальные уходят на relay с дополнительной задержкой ([network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies)).

Как только игра становится соревновательной PvP, оба ограничения становятся блокерами. Тогда нужен третий топологический режим — Server Mode. Там же Photon честно пишет цену: «Photon does not provide servers for hosting a dedicated Fusion server application», а стоимость — это «the cost of spinning up complete Unity instances for every game session». Host mode «is much cheaper to run as no server hosting costs are incurred».

Эта глава — про то, что именно надо сделать в коде, в билде, в контейнере и в чужой панели управления, чтобы `GameMode.Server` заработал, и про то, где в ноябре 2026 этот контейнер вообще можно запустить.

---

## 27.1. Три архитектуры авторитетного сервера, а не две

Курс знает одну топологию. В Fusion 2 их фактически три.

| | Host Mode (курс) | Server Mode | Fusion Plugin SDK |
|---|---|---|---|
| Как включается | `GameMode.AutoHostOrClient` / `GameMode.Host` | `GameMode.Server` | топология **Shared** + серверный плагин |
| Где исполняется авторитетный код | у игрока-хоста | ваш headless Unity | **на стороне Photon** (Enterprise Cloud) |
| Кто платит за compute | никто | вы | Photon (в составе Enterprise Cloud) |
| Читер-хост | возможен | исключён | исключён |
| Версия Fusion | 2.0+ | 2.0+ | **только 2.1.x**, «It does not support Fusion 2.0.x» |
| Доступность | все | все | Gaming Circle / Industries Circle, SDK по запросу на email |

Источники: [network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies), [plugin-sdk-overview-2.1](https://doc.photonengine.com/fusion/current/addons/plugin-sdk/plugin-sdk-overview-2.1).

**Plugin SDK** — это то, что в исследованиях по теме обычно пропускают, а оно закрывает самый частый запрос «нужна серверная авторитетность, но не хочется DevOps». Плагин может читать каждое сетевое свойство («can read every `Networked` property»), перехватывать RPC, держать authority над выбранной частью стейта, спавнить серверные объекты и вызывать внешние сервисы. В комплекте — шаблон проекта плагина, генерируемый types bridge, прекомпилированные бинарники Fusion (Debug/Release) и **локальный Photon Server для разработки, который запускается только под Windows**. Деплой — на Photon Enterprise Cloud, и страница прямо предупреждает, что его создание «involves additional costs». Страница обновлена 21/07/2026.

> ⚠️ Распространённое утверждение «своего Photon-сервера под Fusion не бывает, поле `Server` в `PhotonAppSettings` не поддерживается» **официально не документировано** — в `doc.photonengine.com/fusion/current` такой формулировки нет, а первоисточником был ответ на форуме Photon, который сейчас недоступен. Более того, Plugin SDK поставляется с локальным Photon Server, то есть безусловная формулировка неверна. Не опирайтесь на неё в архитектурных решениях.

---

## 27.2. Как технически поднимается Fusion-сервер

Канонический скелет задаёт официальный сэмпл **Fusion Dedicated Server, версия 2.0.6 от Jul 10, 2025** (страница обновлена 25/06/2026) — он заметно отстаёт от актуального SDK, см. §27.6 ([источник](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server)).

### Аргументы запуска

| Аргумент | Обязательность | Значение по умолчанию |
|---|---|---|
| `-batchmode -nographics` | **обязательны** — именно они триггерят старт Game Server | — |
| `-logFile output.log` | опционально | — |
| `-session <name>` | опционально | «a random *GUID*» |
| `-region <ID>` | опционально | *Best Region* |
| `-lobby <name>` | опционально | *ClientServer* Lobby |
| `-port <number>` | опционально | **`27015`** |
| `-P<property name> <value>` | опционально | «an empty list» |

Сервер ждёт клиентов и автоматически выключается, когда отключились все, — это и есть основа биллинга «платим только за живые матчи».

### Настройки проекта

- **`FUSION_WEAVER`** в Scripting Define Symbols для **каждого** build target: «make sure the *FUSION_WEAVER* symbols is defined», иначе «Fusion will not be able to *weave* your project».
- **`NetworkProjectConfig` → Peer Mode = `Single`**, не `Multiple`. В документации Fusion `Single` описан как «the normal setting for a single server, host, or client peer», а `Multiple` «allows multiple peers in the editor» — только для тестов в редакторе ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)). Гайд Edgegap формулирует это как «Peer Mode = Single (Dedicated Server)» ([Edgegap](https://docs.edgegap.com/docs/sample-projects/unity-netcodes/photon-fusion-2-on-edgegap)).
- Сцены в сборке строго: `0.Launch` (решает «сервер или меню») → `1.Menu` (только клиент) → `2.Game`. Префабы разделены: `Server_NetworkRunner`, `Server_Manager`, `Client_NetworkRunner`, `Client_Manager`, `Player`.

### Адресация — самое тонкое место

```csharp
// Код из документации Photon + гайда Edgegap, не из учебного проекта.
await runner.StartGame(new StartGameArgs {
    GameMode = GameMode.Server,
    SessionName = sessionName,            // то, по чему клиент найдёт матч
    Address = NetAddress.Any(listenPort),  // = 0.0.0.0:listenPort
    CustomPublicAddress = NetAddress.CreateFromIpPort(publicIp, publicPort),
    CustomLobbyName = lobbyName,
    SceneManager = sceneManager,
});
```

Что говорит документация ([matchmaking-api](https://doc.photonengine.com/fusion/current/manual/matchmaking/matchmaking-api)):

- `Address` фиксирует локальный endpoint; «The peer will fail to start if any other service is bound to that same port». Для бинда только по порту — «use `NetAddress.Any(27015)` (meaning `0.0.0.0:27015`) instead».
- `CustomPublicAddress` «is intended for scenarios where the hosting service already provides the mapping between Local and Public EndPoints» — Fusion пропускает STUN и публикует переданный адрес.
- При `DisableNATPunchthrough` «the peer only uses relayed connections, and `Address` and `CustomPublicAddress` are ignored». То есть вся конструкция живёт только при включённом NAT punchthrough.

Таблица последствий неправильного бинда (проверенная Edgegap, [источник](https://docs.edgegap.com/docs/sample-projects/unity-netcodes/photon-fusion-2-on-edgegap)):

| Бинд | Результат |
|---|---|
| `0.0.0.0` | Direct-соединение. **Единственно правильный вариант.** |
| `127.0.0.1` | Сервер стартует, но клиенты идут через Photon relay с лишней задержкой (в оверлее видно «CT Relayed») |
| публичный IP деплоймента | `Failed to bind socket` на сервере, `GameNotFound` у клиентов |

### Лобби, свойства сессии, слоты

- `CustomLobbyName` «Defaults to the ClientServer Lobby in Host, Server, Client and AutoHostOrClient, and the Shared Lobby in Shared». Сессии видны только внутри своего лобби; клиент входит через `JoinSessionLobby(SessionLobby.Custom, name)`.
- `SessionProperties` — **10 свойств и 500 байт** сериализованного размера. При превышении «`StartGame` fails with `ShutdownReason.InvalidArguments`» во всех режимах, кроме `GameMode.Single`. `UpdateCustomProperties` меняет только существующие ключи.
- `PlayerCount` — максимум peer'ов, **включая хост**; «In Server mode, one extra slot is added for the dedicated Server». То есть на матч 8 игроков ставьте 8, а не 9 — Fusion добавит слот сам.
- `EnableClientSessionCreation` «does not apply» в Server/Host mode (дефолт `false` в Client, `true` в Shared и AutoHostOrClient).
- `MatchmakingMode` «Defaults to `FillRoom`», варианты `FillRoom` / `SerialMatching` / `RandomMatching`.

### Регион

Регион сервера задаётся жёстко, не Best Region: `appSettings.FixedRegion = region.ToLower()`. Менять можно только когда `NetworkRunner` не запущен — «can only be done when the NetworkRunner is not running». Коды регионов: `asia`, `au`, `cae`, `cn`, `eu`, `hk`, `in`, `jp`, `za`, `sa`, `kr`, `tr`, `uae`, `us`, `usw`, `ussc`; **они case insensitive**, так что `ToLower()` в сэмпле — стиль, а не требование API. В дашборде есть region allowlist вида `eu;us`: «Within 10 minutes of a change ... the Name Servers will send the filtered list to connecting clients». Для Chinese Mainland — отдельный Name Server `ns.photonengine.cn` и разблокировка доступа по заявке ([regions](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/regions)).

> **Жёсткое требование:** регион хостинга и `FixedRegion` должны совпадать. Расхождение App Version или региона Photon у клиента и сервера даёт клиенту `GameNotFound` ([Edgegap, troubleshooting](https://docs.edgegap.com/docs/sample-projects/unity-netcodes/photon-fusion-2-on-edgegap)).

### Валидация игрока

`SessionName` — это просто строка; знающий её может войти в любой матч. Боевой dedicated server обязан проверять игрока на входе, и слот для этого — `StartGameArgs.ConnectionToken` + `runner.GetPlayerConnectionToken(player)`. В курсе это разобрано в [гл. 09](09-chapter.md) как способ узнать переподключившегося игрока; в dedicated-конфигурации через тот же канал передаётся токен оркестратора или матчмейкера. Обратите внимание: на странице Matchmaking API `ConnectionToken` не упомянут вообще — это API уровня подключения, не матчмейкинга.

---

## 27.3. Что меняется в самой симуляции (а не только в bootstrap)

Распространённая ошибка — считать, что Server Mode это «тот же код, другой bootstrap». Две подсистемы ведут себя иначе ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)):

- **Lag Compensation** «is only available in `Server Mode` and `Host Mode`» — в Shared не нужна, там каждый клиент делает hitbox-детекцию локально. Всё из [гл. 06](06-chapter.md) переносится на Server Mode дословно.
- **Input Delay** (только Fusion 2.1) «is only supported in `Server Mode` and `Host Mode`». `Minimum Input Delay` задаёт пол в миллисекундах или тиках; `Clients Can Add Input Delay To Limit Resims` позволяет клиенту добавлять задержку, чтобы удержать число предсказанных тиков под `Client Max Resims Per Frame`.

И отдельный сюжет, которого нет в курсе вообще, — **tick rate**. В Fusion 2.0 tick и send rate были зафиксированы на **32 и 16**. Начиная с **Fusion 2.1** «Client & Server Tick Rate» можно поставить в любое значение **от 8 до 256**; `Client Send Rate` и `Server Send Rate` задаются интервалом **1–8** (1 = каждый тик, 2 = каждый второй) и должны делить tick rate без остатка. В Shared Mode tick и send rate «cannot exceed `32`!». Значения можно ограничить для всех сессий через дашборд или вебхуки, «so a malicious client can't set an undesired value».

Для dedicated это прямая экономика: 60 Hz сервер стоит примерно вдвое больше CPU, чем 32 Hz. Повышайте tick rate только после профилирования, и помните, что это фича 2.1 — на 2.0.x (и тем более на 2.0.5 из курса) выбора нет.

---

## 27.4. Билд и контейнер

### Headless ≠ Dedicated Server platform

| | Desktop headless mode | Dedicated Server build target |
|---|---|---|
| Как получить | обычный desktop-билд + `-batchmode -nographics` | отдельный sub-target в Build Settings |
| Выбирается в UI | нет: «You can't select headless mode from the Unity Editor build settings» | да |
| Оптимизации | «doesn't perform any optimizations for running a build as a dedicated server» | стриппинг кода и ассетов: «audio files, textures, meshes, and shaders» |
| Цель | полезен «for other purposes, such as automated testing on CI/CD platforms» | «reduce the resource demand of server builds, including the disk size, the size in memory, and the CPU usage» |

Источники: [desktop-headless-mode](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/dedicated-server/desktop-headless-mode), [dedicated-server/introduction](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/dedicated-server/introduction). Dedicated Server — «a sub-target of the three Desktop Platforms (Linux, macOS, and Windows)». Минимальную версию Unity страница Unity **не называет**; требование «Unity 2021.2+» для этого таргета есть только у [Photon](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server).

### Модули и тулчейн

Для Linux-сервера нужны модули Unity Hub: `Linux Build Support (IL2CPP)`, `Linux Build Support (Mono)`, `Linux Dedicated Server Build Support`. **OpenXR надо отключить** — «not compatible with Linux server builds» ([unity-server-in-docker](https://docs.edgegap.com/docs/tools-and-integrations/unity-server-in-docker)).

**Собирайте IL2CPP, не Mono.** `MethodAccessException ... CheckInvokeRpc` в серверном логе означает ровно одно: сервер собран на Mono, надо пересобрать на IL2CPP. При ошибках `No Toolchain found for host platform` / `No Linux sysroot found` добавьте пакеты: для Unity 6000.3+ — `com.unity.toolchain.win-x86_64-linux` и `com.unity.sdk.linux-x86_64`, для более ранних — `com.unity.toolchain.win-x86_64-linux-x86_64` ([Edgegap](https://docs.edgegap.com/docs/sample-projects/unity-netcodes/photon-fusion-2-on-edgegap)).

### Dockerfile: что можно и что нельзя

Официальный сэмпл **Fusion Dedicated Server Docker Image — версия 1.0.2 от Nov 20, 2024 (build 733)**, то есть отстаёт сильнее, чем основной сэмпл. В нём: Linux-контейнер, игровой процесс работает под пользователем `fusion`, **порт по умолчанию 27015/UDP** и он объявлен в Dockerfile. Билд: Linux + x86_64, флаг Server Build, имя исполняемого файла `server.x86_64`, файлы в папку `bin`, затем `docker build -t <name> .`. Запуск: `docker run -d -p 27015:27015/udp fusion-dedicatedserver -s my_session -r eu -l my_lobby`; аргументы `-s` (сессия, дефолт случайный GUID), `-r` (регион, дефолт best region), `-l` (лобби), `-i` (публичный IP), `-p` (публичный порт) ([источник](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server-docker-image)).

Правила из гайда Edgegap ([unity-server-in-docker](https://docs.edgegap.com/docs/tools-and-integrations/unity-server-in-docker)) — обратите внимание, там порт по умолчанию уже 7777/UDP, то есть у Photon и у Edgegap разные дефолты:

- `FROM` — Linux-образ; `COPY` — билд; `USER` — непривилегированный пользователь (`useradd`); `CMD` — **последней строкой**, обычно запускает shell-скрипт.
- **`VOLUME` использовать нельзя** — локальное хранилище так не монтируется. Замена для файлов — **Endpoint Storage** (например, S3-бакет). Базы данных на деплойментах не держать: они «aren't meant for long-running processes and may be terminated without notice».
- `EXPOSE` не требуется — «It only documents the port». Локально публикуйте через `-p 7777/udp` (плюс переменную `ARBITRIUM_PORTS_MAPPING`), в облаке — через Port Mapping. На Apple Silicon нужен `--platform=linux/amd64`.
- **Конфигурация — слоями, один образ:** deployment parameters (env перед каждым деплоем), version parameters (общие для версии приложения — stage, тег артефакта, секреты, эндпоинты), и образ, читающий всё это при старте. Не плодите образ на каждую конфигурацию.
- Размер: и >1 ГБ, и <100 МБ нормальны. Сокращать через `.dockerignore`, отключение static batching, mesh compression = High, снятие CPU read/write с текстур и мешей, Addressables, multi-stage build. У Container Registry есть лимит хранилища, при превышении загрузка падает.

---

## 27.5. Склейка с оркестратором: три рабочих примера

Fusion не содержит интеграции с хостинг-провайдерами: «Fusion does not support any kind of integration with Multiplay out of the box», и для серверных метаданных (player count, описание режима) нужен «communication layer between the Server and the Multiplay Service» ([multiplay](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server-multiplay)). Делается это руками, и шаблонов всего три.

**Edgegap — через переменные окружения.** `EdgegapServerManager.cs` читает `ARBITRIUM_REQUEST_ID` (→ `SessionName = $"{requestId}.pr.edgegap.net"`), `ARBITRIUM_PORT_GAMEPORT_INTERNAL` (порт бинда), `ARBITRIUM_PUBLIC_IP` и `ARBITRIUM_PORT_GAMEPORT_EXTERNAL` (→ `CustomPublicAddress`). Клиент получает тот же Host URL из дашборда (для теста) или от матчмейкинга и ставит `ProvideInput = true` ([источник](https://docs.edgegap.com/docs/sample-projects/unity-netcodes/photon-fusion-2-on-edgegap)).

**Gameye — через REST + аргументы.** Спавн: `https://api.gameye.io/session` с полями `id` (match ID), `location`, `image`, `env`, `args`, `restart`, `enableMetrics`, `labels`. Серверу передаются `-s <game session name>` и `-r <photon region>`. Нужны Gameye auth token и Docker Hub repository. Своя таблица соответствия регионов: `eu-west`/`eu-south` → `eu`, `na-east`/`na-central` → `us`, `na-west` → `usw`, `sa-east` → `sa`, `asia-east` → `asia`, `asia-northeast`/`oce-east` → `jp` ([источник](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server-gameye)).

**Amazon GameLift Servers — через колбэки.** Разбор AWS China Blog от 8 мая 2025 (Fusion 2, Unity 2021.3.18+/2022.3.x/2023.x) даёт полный маппинг ([источник](https://aws.amazon.com/cn/blogs/china/photon-fusion-integration-guide-for-gamelift-servers/)):

| GameLift SDK | Где вызывается |
|---|---|
| `InitSDK()` | старт процесса (на Anywhere — с `ServerParameters`, на managed EC2 — без аргументов) |
| `ActivateGameSession` | внутри колбэка `OnStartGameSession`, затем `StartFusion` постится в main thread |
| `AcceptPlayerSession` | `INetworkRunnerCallbacks.OnPlayerJoined` при `runner.IsServer` |
| `RemovePlayerSession` | `OnPlayerLeft` |
| `ProcessEnding` | `OnShutdown` (после сохранения данных и выгрузки логов) |

Адреса: `Address = NetAddress.CreateFromIpPort("0.0.0.0", gameport)`, `CustomPublicAddress = NetAddress.CreateFromIpPort(gamesession.IpAddress, gamesession.Port)`, `SessionName` — из `GameProperties["SessionName"]`. Клиент вызывает `CreatePlayerSession`, затем `StartGame(GameMode.Client, ...)` с `ConnectionToken = Encoding.UTF8.GetBytes(playerSessionId)`; сервер в `OnPlayerJoined` делает `runner.GetPlayerConnectionToken(player)` и отдаёт строку в `AcceptPlayerSession`. **Это и есть канонический скелет валидации игрока на dedicated server.**

> **Операционный слой** — graceful shutdown по SIGTERM, health checks, A2S/query для server browser, агрегация логов и метрик, zero-downtime патчинг — в документации Fusion не описан нигде, кроме «сервер выключается, когда ушли все клиенты». Для Agones он обязателен: там игровой сервер обязан использовать SDK для health checking и смены состояния ([agones](https://agones.dev/site/docs/overview/)). Планируйте этот слой сами.

---

## 27.6. Рынок хостинга на ноябрь 2026

Это часть, где устаревшие знания дороже всего.

**Multiplay Game Server Hosting больше не сервис Unity.** Статус-страница Unity (нотис от 31 марта 2026, 16:46 UTC): «As of April 1st 2026 Multiplay Game Server Hosting by Unity has been deprecated». Жёсткого выключения 31.03 не было: клиенты, заявившие о намерении миграции на «Multiplay by Rocket Science» до этой даты, «can continue using the service as normal until migration is complete» ([status.unity.com](https://status.unity.com/info_notices/362941)). В **Unity 6.3** вырезано всё: «The default version of the Multiplayer Services package is now 2.0.0. This version removes all the functionalities related to Multiplay Hosting», и «Multiplay Hosting is no longer supported in the Unity Editor and runtime» ([upgrade-guide-unity63](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity63)). Конкретика ломки: при `UNITY_SERVER`/`ENABLE_UCS_SERVER` или Dedicated Server platform вызовы Multiplay-функций **не компилируются**; `.mmq`-файлы из 1.2.0 ещё деплоятся, но при изменении теряют Multiplay type; `Matchmake async` с Multiplay Queue отдаёт **Not supported**; единственный эскейп — откат на Unity 6.0 + Multiplayer Services 1.2.0. Photon уже вырезал эту интеграцию из своих сэмплов: в BR200 она «removed from the BR200 sample in version 2.1.0», при этом «Running BR200 as a self-hosted headless dedicated server is unaffected by this change» ([br200](https://doc.photonengine.com/fusion/v2/game-samples/br200/game-server-hosting)).

> Документация Photon по этой теме **не обновлена**: страница Fusion 2 про Multiplay всё ещё называет его «the Unity Hosting Service solution» и приводит таблицу соответствия регионов Multiplay → Photon (US West/East/Central → `US`, Europe/Russia → `EU`, North East Asia → `JP`, South East Asia/Australia/India → `ASIA`, South America → `SA`, China → `CN`). Таблица полезна как образец логики, но применимость к Rocket Science не проверена.

**Hathora закрылась.** Первоисточник — пост самой Hathora от 4 марта 2026: «Today, we'd like to share that Hathora has been acquired by Fireworks AI», «support will continue through May 5, 2026», и партнёр миграции — «we've partnered with Nitrado's GameFabric to provide a clear migration path» ([hathora.ghost.io](https://hathora.ghost.io/hathora-is-joining-fireworks-ai/)). Все репозитории и туториалы «Fusion + Hathora» — источник паттернов, не рабочее решение. **GameFabric by Nitrado** — де-факто то, куда ушли её клиенты; собственной проверки его прайсинга и интеграции с Fusion у меня нет.

**Цены провайдеров (на ноябрь 2026).**

| Провайдер | Модель | Цена из первоисточника |
|---|---|---|
| **Edgegap Edge Cloud** | on-demand | **$0.00115 / vCPU / мин**, 0.25–4 vCPU на деплоймент, egress **$0.10/ГБ**, 615+ локаций ([pricing](https://edgegap.com/pricing)) |
| **Edgegap Private Fleet** | bare metal | **$0.0261 / vCPU-час**, или **$280/хост/мес** (12 мес) / **$350** month-to-month; 16 vCPU (14.7 полезных), 6 ТБ egress включено, 58 локаций |
| **Edgegap Free trial** | — | **1 приложение, 2 версии**, до 1.5 vCPU и 3 ГБ RAM, **1** одновременный деплоймент, **лимит 60 минут аптайма** на деплоймент |
| **Edgegap Matchmaker** | подписка | Hobbyist $0.0312/час (1 vCPU / 2 ГБ), Studio $0.146/час (6 vCPU / 12 ГБ), Enterprise $0.548/час (18 vCPU / 48 ГБ) |
| **Edgegap relays** | CCU | 100 CCU — $8, 500 — $115, 1000 — $225, 2000 — $450/мес; overage $0.14/CCU и $0.10/ГБ |
| **GameLift Servers** | instance-hours | bandwidth: «Network bandwidth is free for all instance types generation 6 and later»; Spot — 50–85% экономии; Windows-лицензия «can increase your instance costs by almost 2x over comparable Linux servers»; 68 instance families, 26 регионов, 9 Local Zones; volume-дискаунты при расходе свыше $80K/мес ([pricing](https://aws.amazon.com/gamelift/pricing/)) |
| **Agones** | open source | софт бесплатен; версия **1.61.0**, страница обновлена 11 октября 2026 ([agones](https://agones.dev/site/docs/overview/)) |
| **Rocket Science** | — | **публичного прайс-листа нет** |

Отдельной ставки за RAM у Edge Cloud на странице цен нет — RAM фигурирует только в бандлах матчмейкинга и лимитах trial; как считается память деплоймента 0.25–4 vCPU, страница не раскрывает.

Про **GameLift** важно не упрощать: биллинг идёт за compute-инстансы, а peak CCU — это входной параметр калькулятора, из которого через average/peak ratio, players per session, sessions per instance и 10% instance buffer выводятся instance-hours. Формулировки «биллинг по пиковому CCU» на странице нет. Модели хостинга: managed EC2 fleets, managed container fleets (только Linux), Anywhere fleets (своё железо под управлением GameLift placement) и гибрид.

**Agones** — open source оркестрация «for deploying, hosting, scaling, and orchestrating dedicated game servers» на Kubernetes, с SDK для lifecycle (health checking, state management, конфигурация) и клиентскими SDK для Unreal, Unity, C++, Go, C#, Node.js, Rust, Python и REST. Обратите внимание: сама overview-страница CRD не перечисляет; `GameServer`, `Fleet`, **`GameServerAllocation`** и `Fleet Autoscaler` описаны в разделе Reference. `GameServerAllocation` — ключевой для интеграции с матчмейкингом ресурс: именно через него сервер выделяется под матч.

**Rocket Science** заявляет 40+ локаций, sub-30ms, zero-downtime patching и «50%+ saved compared to pure cloud» — это **вендорский маркетинг без методики и базлайна**, страница не называет ни одного облачного провайдера, а счётчики статистики на ней не рендерятся. Для бюджета непригодно ([rocketscience.gg](https://www.rocketscience.gg/services/hosting)).

**PlayFab Multiplayer Servers** фигурирует в списке провайдеров документации Fusion 1 наравне с Multiplay, Gameye и GameLift ([v1 overview](https://doc.photonengine.com/fusion/v1/manual/dedicated-server-overview)). Его текущий статус, тарифы и наличие Fusion 2-интеграции я первоисточником **не проверял** — бюджет веб-поиска в этой сессии был исчерпан. Перед выбором проверьте сами.

---

## 27.7. Деньги: Photon никуда не уходит

| | Host Mode (курс) | Server Mode |
|---|---|---|
| Photon AppId | нужен | нужен |
| Photon CCU | считаются | считаются |
| Compute | $0 | ваш счёт |
| Egress | в трафике Photon | отдельный счёт провайдера |

Photon Public Cloud (gaming): Development — бесплатно, 20 CCU, 60 ГБ, без burst; Free 100 CCU — бесплатно, 0.3 ТБ, одно приложение, стекается до 200 CCU; 100 CCU one-time — $95 единоразово, истекает через 12 месяцев; 500 CCU — $125/мес, 1.5 ТБ; 1000 CCU — $250/мес, 3.0 ТБ; 2000 CCU — $500/мес, 6.0 ТБ. Premium — $0.50/CCU при минимуме $1000/мес (включает 2000 CCU и 6.0 ТБ), автоскейл до 50 000 CCU, DDoS по умолчанию. Enterprise — по запросу: dedicated servers, SLA, server-authoritative logic через плагины. Доп. трафик $0.05/ГБ (Europe, Canada, East/RU/RUE, USA East, USA West) или $0.10/ГБ (Asia, Australia, China, India, Japan, South Africa, South America, South Korea, Turkey, UAE) ([fusion/pricing](https://www.photonengine.com/en-US/fusion/pricing)).

Правила CCU ([photon/pricing](https://doc.photonengine.com/photon/current/pricing)): CCU считается по всему приложению, «all rooms and all regions combined», биллинг — по сумме региональных пиков. Не потребляют CCU только сессии, которые вообще не открывают соединение к Photon Cloud, — примеры: Fusion `GameMode.Single` и PUN 2 Offline Mode. Трафик — 3 ГБ на пиковый CCU в месяц в каждом плане. Fair-use ориентир — «staying below 500 messages per second per room», и это «a fair-use guideline rather than a hard limit». CCU burst доступен с 500 CCU и выше: «no player is ever cut off», перебор доначисляется в следующий инвойс, 48 часов на апгрейд после первого burst. **На планах 20 и 100 CCU — жёсткий кап, и в клиентском коде это выглядит как дисконнект с причиной `MaxCcuReached`** — этот код ошибки важнее для чеклиста, чем сама цена.

> ⚠️ **«Headless-сервер тоже съедает один CCU» — официально не документировано.** Страница прайсинга Photon не упоминает dedicated server в контексте CCU вообще; логика «серверный peer открывает соединение к Photon Cloud, значит считается» выводится из правила про сессии без соединения, но прямой формулировки нет, а форум Photon сейчас недоступен для проверки. **Перед бюджетированием уточните у Photon напрямую.** Консервативно планируйте матч «1 сервер + N клиентов» как N+1 CCU.

> ⚠️ Старые тарифы Unity Multiplay ($0.038/CPU-core-час и т.п.) **удалены вместе с документацией**: `docs.unity.com/en-us/multiplay-hosting` отдаёт 308-редирект на страницу Rocket Science. Не используйте эти цифры ни в каких расчётах.

Ориентир стоимости матча: **$0.00115/vCPU/мин = $0.069/vCPU-час**, значит матч на 0.5–1 vCPU — порядка **$0.035–0.069 за час** плюс egress $0.10/ГБ. Это **моя арифметика из опубликованной ставки Edgegap**, а не опубликованная Edgegap цифра: типичное потребление vCPU на матч зависит от вашего tick rate и числа игроков и нигде не опубликовано. Сравнивать нужно не «Photon vs Edgegap», а **«Host mode (Photon CCU) vs Server mode (Photon CCU + compute + egress)»**.

---

## Чего по этой теме нет в учебном курсе

Курс — это 100% Host Mode на **Fusion 2.0.5 / Unity 2022.3.42f1**. Из этой главы в нём отсутствует **всё**, кроме одного: `GameMode.Server`, `Peer Mode = Single`, `StartGameArgs.Address` и `CustomPublicAddress`, серверный `FixedRegion`, `CustomLobbyName`, лимиты `SessionProperties`, headless-запуск, Dedicated Server build target, Docker, IL2CPP-специфика, оркестрация, autoscaling, настраиваемый tick rate и любой разговор о деньгах за compute.

Что переносится без переписывания:

- Вся модель State / Input Authority из [гл. 01](01-chapter.md) и [00-README](00-README.md) — в Server Mode она та же, просто у сервера нет локального игрока.
- Лаг-компенсация из [гл. 06](06-chapter.md) — доступна именно в Server и Host mode, то есть переносится дословно.
- `ConnectionToken` из [гл. 09](09-chapter.md) — в dedicated это канал для токена оркестратора.
- Interest Management из [гл. 08](08-chapter.md) — на сервере важнее, чем на хосте: AoI напрямую режет egress, за который вы теперь платите.

Что **не** переносится: Host Migration из [гл. 09](09-chapter.md) теряет смысл (сервер не «уходит» — он перезапускается оркестратором), а Input Delay из §27.3 — фича только Fusion 2.1, которой в курсовой версии нет.

Версии: курс на 2.0.5, актуальные стабильные — **2.1.3 (Sep 17, 2026, build 2390)** и **2.0.13 (Sep 16, 2026, build 2379)**; поддерживаемые Unity — 2021.3.45, 2022.3.45, 6.0.x, 6.3.x; «Projects should be upgraded to the latest `2.0.x` SDK before being upgraded to `2.1.x`», и Fusion 2.1 требует Realtime 5 и несовместим с 2.0 ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)). При этом сэмплы Photon по dedicated server отстают: основной — 2.0.6 (10.07.2025), Docker Image — 1.0.2 (20.11.2024). На Unity 6 ориентируется гайд Edgegap, а не экосистема сэмплов Photon.

---

## Решение: что брать и когда

```
Нужна защита от читер-хоста или стабильный Direct-коннект?
├─ НЕТ → Host Mode. Это курс. Ничего не меняйте. (гл. 01, 09)
└─ ДА
   ├─ Топология Shared подходит и есть бюджет Enterprise Cloud?
   │     → Fusion Plugin SDK (только 2.1.x). Авторитетность без своего DevOps.
   └─ Нужен GameMode.Server → выбираем оркестратор:
         ├─ инди, минимум работы, готовая Fusion-интеграция  → Edgegap
         ├─ уже в AWS, нужен FlexMatch и Spot                → GameLift Servers
         ├─ есть инженер по Kubernetes, важна переносимость   → Agones
         ├─ пришли из Hathora                                → GameFabric by Nitrado
         └─ нужен REST-спавн и есть Docker Hub               → Gameye (есть офиц. страница Fusion)
```

| Критерий | Edgegap | GameLift Servers | Agones | Plugin SDK |
|---|---|---|---|---|
| Официальный гайд под Fusion 2 | да | нет (есть разбор AWS) | нет | да (это аддон Photon) |
| Своё железо возможно | Private Fleet | Anywhere fleets | да | нет |
| Вы пишете autoscaling | нет | нет | **да** | нет |
| Вендор-лок | средний | высокий | нулевой | полный (Photon) |
| Нужен Kubernetes | нет | нет | **да** | нет |

**Не планируйте** на Hathora (выключена) и на Multiplay как на сервис Unity (депрекейтнут, вырезан из Unity 6.3). Главный вывод 2026 года: абстрагируйте провайдера за своим слоем — один `ServerManager`, читающий конфиг из переменных окружения, — тогда миграция стоит один файл, а не один квартал.

---

## Чеклист

1. Решить, нужен ли Server Mode вообще. Короткие кооп-матчи → Host Mode из курса, дешевле и проще.
2. Если нужна только авторитетность и подходит Shared — оценить Fusion Plugin SDK (требует 2.1.x + Enterprise Cloud) до того, как браться за контейнеры.
3. Поставить `GameMode.Server`, `NetworkProjectConfig → Peer Mode = Single`, добавить `FUSION_WEAVER` в Scripting Define Symbols для серверного таргета.
4. Собрать Dedicated Server target под Linux x86_64, **IL2CPP** (не Mono), отключить OpenXR, поставить три Linux-модуля в Unity Hub.
5. Запускать с `-batchmode -nographics`; `-logFile` — обязательно в проде, иначе логи некуда собирать.
6. Выставить `Address = NetAddress.Any(port)` — бинд ровно `0.0.0.0`. Никогда `127.0.0.1` (уйдёте на relay) и никогда публичный IP (`Failed to bind socket`).
7. Выставить `CustomPublicAddress` из переменных окружения провайдера. Убедиться, что `DisableNATPunchthrough` **не** включён, иначе оба поля игнорируются.
8. Задать `FixedRegion` строго под регион хостинга и проверить region allowlist в дашборде (применяется до 10 минут). Расхождение региона или App Version → `GameNotFound` у клиента.
9. Держать `SessionProperties` в пределах 10 ключей и 500 байт; геймплейное состояние в них не возить. `PlayerCount` ставить без запаса на сервер — Fusion добавит слот сам.
10. Реализовать валидацию игрока: `ConnectionToken` на клиенте → `GetPlayerConnectionToken` в `OnPlayerJoined` → проверка у матчмейкера/оркестратора. Без этого в матч войдёт любой, кто знает `SessionName`.
11. Dockerfile: `FROM`/`COPY`/`USER`/`CMD` (последней строкой), **без `VOLUME`**; файлы — через Endpoint Storage; БД на деплойментах не держать; конфиг — слоями через env, один образ на все конфигурации.
12. Привести к одному значению порт: у сэмпла Photon дефолт 27015, у Edgegap — 7777/UDP. Локально публиковать через `-p <port>/udp`.
13. Написать операционный слой самостоятельно: SIGTERM-shutdown, health check, метрики, агрегация логов. Для Agones health checking и смена состояния через SDK обязательны, аллокация — через `GameServerAllocation`.
14. Повышать tick rate (8–256, только Fusion 2.1) только после профилирования и ограничить его в дашборде/вебхуках, чтобы клиент не выставил своё значение.
15. Посчитать бюджет как **Photon CCU + compute + egress**, а не «вместо Photon». Обработать дисконнект `MaxCcuReached` в клиенте. Планировать матч консервативно как N+1 CCU и уточнить этот вопрос у Photon напрямую.
16. Не использовать материалы 2024–2025 по провайдерам без проверки: Hathora выключена, Multiplay сменил владельца и вырезан из Unity 6.3, старые тарифы Multiplay удалены вместе с документацией.

---

## Источники

**Photon Fusion — ядро**
- [network-topologies](https://doc.photonengine.com/fusion/current/manual/network-topologies) — определение Server/Host/Shared, «full and exclusive State Authority», «Photon does not provide servers», «1 in 10 players» на relay, «A rogue host can cheat».
- [matchmaking-api](https://doc.photonengine.com/fusion/current/manual/matchmaking/matchmaking-api) — `Address`, `NetAddress.Any`, `CustomPublicAddress`, `DisableNATPunchthrough`, `CustomLobbyName`, лимиты `SessionProperties` (10 / 500 байт), `PlayerCount`, `EnableClientSessionCreation`, `MatchmakingMode`.
- [network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) — tick rate 8–256 с Fusion 2.1 (ранее 32/16), send rate 1–8, лимит 32 в Shared, Peer Mode Single/Multiple, Lag Compensation и Input Delay только в Server/Host mode.
- [regions](https://doc.photonengine.com/fusion/current/manual/connection-and-matchmaking/regions) — 16 кодов регионов, `FixedRegion` только при остановленном ранере, region allowlist и 10 минут, `ns.photonengine.cn`.
- [sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — 2.1.3 (17.09.2026, build 2390), 2.0.13 (16.09.2026, build 2379), поддерживаемые Unity, порядок апгрейда 2.0.x → 2.1.x.
- [plugin-sdk-overview-2.1](https://doc.photonengine.com/fusion/current/addons/plugin-sdk/plugin-sdk-overview-2.1) — Plugin SDK: только 2.1.x, Shared authority, Enterprise Cloud, возможности плагина, локальный Photon Server только под Windows, обновлено 21/07/2026.

**Photon Fusion — сэмплы dedicated server**
- [fusion-dedicated-server](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server) — 2.0.6 (10.07.2025), аргументы и дефолты, `FUSION_WEAVER`, сцены и префабы, Standalone vs Dedicated Server Build, «Unity 2021.2+».
- [fusion-dedicated-server-docker-image](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server-docker-image) — 1.0.2 (20.11.2024, build 733), порт 27015/UDP, пользователь `fusion`, аргументы `-s -r -l -i -p`, шаги сборки.
- [fusion-dedicated-server-gameye](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server-gameye) — «imperative to connect it to a Photon Cloud Region», `api.gameye.io/session`, поля спавна, аргументы `-s`/`-r`, таблица регионов.
- [fusion-dedicated-server-multiplay](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server-multiplay) — бинд `0.0.0.0`, «does not support any kind of integration with Multiplay out of the box», таблица регионов Multiplay → Photon (документация не обновлена под Rocket Science).
- [br200/game-server-hosting](https://doc.photonengine.com/fusion/v2/game-samples/br200/game-server-hosting) — интеграция удалена в 2.1.0, self-hosted headless не затронут, дисклеймер о несвязанности с Unity.
- [v1/dedicated-server-overview](https://doc.photonengine.com/fusion/v1/manual/dedicated-server-overview) — «a dedicated game server is needed as well as a Photon Fusion Cloud subscription» (формулировка Fusion 1), список провайдеров: Multiplay, Gameye, PlayFab, GameLift.

**Цены и CCU**
- [photon/current/pricing](https://doc.photonengine.com/photon/current/pricing) — правила CCU, `MaxCcuReached`, CCU burst и 48 часов, 3 ГБ на пиковый CCU, 500 msg/s per room как fair use.
- [photonengine.com/fusion/pricing](https://www.photonengine.com/en-US/fusion/pricing) — тарифы Public Cloud, Premium ($0.50/CCU, минимум $1000), Enterprise (dedicated servers, SLA, server-authoritative logic через плагины), ставки доп. трафика.
- [edgegap.com/pricing](https://edgegap.com/pricing) — Edge Cloud, Private Fleet, free trial (1 app / 2 версии / 60 мин), матчмейкинг, relays.
- [aws.amazon.com/gamelift/pricing](https://aws.amazon.com/gamelift/pricing/) — instance-based биллинг, бесплатный bandwidth с 6-го поколения, Spot 50–85%, Windows ~2x, 68/26/9, порог $80K.

**Unity**
- [dedicated-server/introduction](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/dedicated-server/introduction) — что такое Dedicated Server build target, sub-target трёх десктопных платформ, что стрипается; минимальная версия Unity **не указана**.
- [desktop-headless-mode](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/dedicated-server/desktop-headless-mode) — headless нельзя выбрать в Build Settings, никаких серверных оптимизаций, полезен для CI/CD.
- [upgrade-guide-unity63](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity63) — Multiplayer Services 2.0.0 убирает всю Multiplay Hosting функциональность, ошибки компиляции, `.mmq`, «Not supported», откат на Unity 6.0 + 1.2.0.
- [status.unity.com/info_notices/362941](https://status.unity.com/info_notices/362941) — «As of April 1st 2026 ... has been deprecated», исключение для миграции на Rocket Science, нотис от 31.03.2026 16:46 UTC.

**Провайдеры и инструменты**
- [docs.edgegap.com/.../photon-fusion-2-on-edgegap](https://docs.edgegap.com/docs/sample-projects/unity-netcodes/photon-fusion-2-on-edgegap) — Peer Mode Single, `ARBITRIUM_*`, `SessionName`, таблица бинда, `MethodAccessException`/IL2CPP, `GameNotFound`, `InvalidAuthentication`, пакеты тулчейна.
- [docs.edgegap.com/.../unity-server-in-docker](https://docs.edgegap.com/docs/tools-and-integrations/unity-server-in-docker) — модули Unity, OpenXR, правила Dockerfile, запрет `VOLUME` и Endpoint Storage, размер образа, слои конфигурации.
- [aws.amazon.com/cn/blogs/china/photon-fusion-integration-guide-for-gamelift-servers](https://aws.amazon.com/cn/blogs/china/photon-fusion-integration-guide-for-gamelift-servers/) — маппинг GameLift SDK на колбэки Fusion, `ConnectionToken` = `playerSessionId` (08.05.2025).
- [agones.dev/site/docs/overview](https://agones.dev/site/docs/overview/) — что такое Agones, SDK lifecycle, клиентские SDK, 1.61.0, обновлено 11.10.2026; CRD (`GameServer`, `Fleet`, `GameServerAllocation`, `Fleet Autoscaler`) — в разделе Reference, не в overview.
- [hathora.ghost.io/hathora-is-joining-fireworks-ai](https://hathora.ghost.io/hathora-is-joining-fireworks-ai/) — покупка Fireworks AI, «support will continue through May 5, 2026», партнёр миграции GameFabric by Nitrado (04.03.2026).
- [rocketscience.gg/services/hosting](https://www.rocketscience.gg/services/hosting) — маркетинговые заявления, публичного прайс-листа нет.

**Не проверено первоисточником в этой главе:** текущий статус и тарифы PlayFab Multiplayer Servers; прайсинг и Fusion-интеграция GameFabric by Nitrado; возможности Nakama сверх самоопределения «open source game backend» (на странице документации они представлены пунктами навигации, без описаний); требование `.license`-файла для Plugin SDK (страница аддона о нём не пишет).

# Глава 31. Production: от прототипа до релиза

> **TL;DR для агента**
>
> 1. **Экономика проекта — это одно число: ~1,2 КБ/с суммарного трафика на игрока.** Оно выводится из биллинга (3 ГБ на peak-CCU в месяц, [pricing](https://www.photonengine.com/en-US/Fusion/pricing)), а не из документации по API. Технический потолок другой — снапшот должен быть меньше MTU, ~1280 байт ([optimizations](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations)). Разрыв между ними в десятки раз закрывается Interest Management и send rate, и это не «оптимизация потом», а часть архитектуры.
> 2. **Fusion 2.1 несовместим с Fusion 2.0** ([sdk-download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download)). Любой апгрейд SDK — это форсированное обновление всех клиентов плюс смена `AppVersion`, а не постепенный rollout. Unity 2022.3.42f1 из курса уже вне списка поддерживаемых версий.
> 3. **Профилирование сети работает только в DEBUG DLL.** Fusion Statistics и симулятор сетевых условий в release-сборке не существуют ([fusion-statistics-2.1](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/fusion-statistics-2.1), [simulating-network-conditions](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/simulating-network-conditions)). Планируй отдельный QA-билд.
> 4. **Логируй `NetworkRunner.DisconnectInfo`, а не аргумент `OnShutdown`.** Есть три разных перечисления причин разрыва, и `ShutdownReason` — самое бедное из них ([disconnect-info](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/disconnect-info)).
> 5. **Мобила и WebGL меняют топологию, а не настройки.** Для обоих Photon рекомендует Shared Mode; Host Mode «не рекомендуется для мобильных, кроме особых случаев», на WebGL Server/Host отключены по умолчанию и всегда идут через релей ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)). То есть весь Host-mode код курса на этих платформах придётся пересобирать.

---

## Зачем это знать

Учебный курс отвечает на вопрос «как заставить механику работать по сети». Прод отвечает на другой: «что сломается, когда игроков станет тысяча, канал — LTE, а билд уйдёт в стор». Это разные инженерные задачи, и у второй есть жёсткие числовые границы, которые нельзя обойти кодом — их можно только учесть в архитектуре заранее.

Главное отличие: в курсе сеть бесплатна и идеальна (два редактора на localhost), а в проде она стоит денег и регулярно рвётся. Четыре числа задают весь контур: **~1,2 КБ/с** на игрока (деньги), **1280 байт** на снапшот (MTU), **10 секунд** таймаута при уходе приложения в фон (мобила), **10 попыток × 500 мс** на коннект (UX первого экрана). Всё остальное в этой главе — инженерия вокруг этих четырёх ограничений.

---

## 31.1. Точка отсчёта: версии и стратегия апгрейда

Курс зафиксирован на **Fusion 2.0.5** и **Unity 2022.3.42f1** (см. [00-README](00-README.md)). Состояние на 2026-11-01 по [sdk-download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download):

| Ветка | Актуальная версия | Build | Дата |
|---|---|---|---|
| 2.1 (current) | 2.1.3 Stable | 2390 | 17 сен 2026 |
| 2.0 (поддерживается) | 2.0.13 Stable | 2379 | 16 сен 2026 |

Поддерживаемые версии Unity: **2021.3.45, 2022.3.45, 6.0.x, 6.3.x**. Платформы: Windows 10/11, macOS, iOS, visionOS, PS4/PS5, Xbox One / Series X|S, Switch / Switch 2, Android, WebGL.

Два следствия, которых в курсе нет:

- **Unity 2022.3.42f1 вне списка.** Минимум в ветке 2022.3 теперь `.45`. Патч-апдейт Unity — обязательная позиция в релизном плане, а не «как-нибудь потом».
- **Апгрейд SDK — ломающий.** Дословно: «Fusion 2.1 requires Realtime 5. (not compatible with Fusion 2.0)» и «Projects should be upgraded to the latest `2.0.x` SDK before being upgraded to `2.1.x`». Voice тянется за этим же: Voice SDK 2.64 существует в двух вариантах, и вариант «для Fusion 2.0» несовместим с 2.1.

Что появилось между 2.0.5 и 2.1 и прямо меняет production-решения ([whats-new-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/whats-new-2-1)):

| Фича | Почему важна для релиза |
|---|---|
| Tick Rate 8…256 Гц | в Fusion 2.0 Shared был **залочен на 32 tick / 16 send** ([network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)); в Shared и в 2.1 потолок 32 |
| Object Send Priority | способ приглушить дальние объекты, не выключая их из интереса |
| Large Data RPCs (`RpcChannel.ReliableLargeData`) | «Fusion 2.1 no longer has a limit of 512 bytes for RPC size» — до 2.1 **512 байт** был жёстким лимитом; не tick-accurate, порядок не сохраняется |
| Input Delay | только Server/Host, **выключен по умолчанию**; снижает число ресимуляций ценой задержки |
| Improved Master Client Switching | `NetworkRunner.RequestMasterClientReassign()`; включается `AllowMasterClientReassignRequest`, порог по умолчанию **2 с** (`MasterClientReassignRequestThreshold`). Порог 0 даёт любому клиенту захватить мастера — вектор атаки |
| Shared Mode Custom Plugins | серверный код в Shared (перехват RPC) — практически единственный путь к античиту в Shared; «Custom Plugins are available for Enterprise Cloud Only» |
| `StartGameArgs.PlayerUniqueId` | стабильный `PlayerRef` между реконнектами; «The PlayerUniqueId is not used as the PlayerRef» |
| Debug Breakpoint Support | «In Host / Server Mode debugging using breakpoints is possible without triggering a timeout» — меняет весь процесс отладки сетевого кода |
| Photon Realtime API | создание комнаты до бутстрапа Fusion-сессии (SQL-лобби, слот-резервация); доступ по запросу в Photon |
| Shared Mode Configurable AOI | cell/grid size AoI настраиваются; раньше были зафиксированы на 32 юнита |

Также в 2.1 сменилась подпись коллбэка Data Streaming: `OnReliableDataReceived` отдаёт `ReadOnlySpan<byte>` вместо `ArraySegment<byte>` — ломающее изменение при апгрейде. А в 2.1.3 пакет `com.unity.ugui` стал необязательным: его удаление отключает меню и Statistics (документированное поведение, не баг).

---

## 31.2. Бюджет трафика: откуда берутся числа

Ориентир даёт биллинг, а не документация. Все тарифы включают **3 ГБ трафика на один peak-CCU в месяц**, сверх — **$0,05/ГБ** (Europe / Canada East / RU / RUE / USA East / USA West) или **$0,10/ГБ** (Asia / Australia / China / India / Japan / South Africa / South America / South Korea / Turkey / UAE). Считается **весь входящий и исходящий** трафик ([pricing](https://www.photonengine.com/en-US/Fusion/pricing), [faq](https://doc.photonengine.com/fusion/v2/troubleshooting/faq)).

```
3 ГБ / (30 сут × 86400 с) ≈ 1157…1243 байт/с на одного подключённого игрока
                            ≈ 9…10 кбит/с, суммарно in + out
```

Тарифы на 2026-11-01 ([pricing](https://www.photonengine.com/en-US/Fusion/pricing)):

| План | CCU | Трафик/мес | Цена | Burst |
|---|---|---|---|---|
| Development | 20 | 60 ГБ | бесплатно, «Development only» | нет |
| **Free 100 CCU (Launch)** | 100 | 0,3 ТБ | бесплатно, «One app only», складывается до 200 CCU | нет |
| 100 CCU one-time | 100 | 0,3 ТБ | $95 единоразово, 12 мес, без автопродления | нет |
| 500 CCU | 500 | 1,5 ТБ | $125/мес | есть |
| 1000 CCU | 1000 | 3,0 ТБ | $250/мес | есть |
| 2000 CCU | 2000 | 6,0 ТБ | $500/мес | есть |
| Premium Cloud | до 50 000 | 3 ГБ/CCU | $0,50/CCU, минимум $1000/мес (включает 2000 CCU и 6 ТБ) | > 50k |
| Enterprise Cloud | без лимита | — | по запросу | Dedicated Servers, серверные плагины, SLA |

После первого бурста есть **48 часов** на апгрейд, иначе возможен cap; овередж выставляется в следующем месяце ([pricing](https://www.photonengine.com/en-US/Fusion/pricing), [faq](https://doc.photonengine.com/fusion/v2/troubleshooting/faq)).

**Технический потолок иной.** Снапшот должен укладываться в MTU (**~1280 байт**), и Interest Management назван «the best way to drastically reduce bandwidth» ([optimizations](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations)). При 60 Гц и send rate 1 полный снапшот каждый тик — это ~76 КБ/с, то есть примерно в 60 раз дороже биллингового бюджета. Отсюда директива: **как только игроков больше четырёх, Interest Management и send rate — обязательная часть архитектуры.**

> **Жанровых ориентиров по трафику в этой главе нет сознательно.** Публичные «нормы байт/с на игрока по жанрам» первоисточником не подтверждаются; единственное корректное действие — измерить свой худший сценарий страницей Object в Fusion Statistics (§31.3) и сравнить с ~1,2 КБ/с.

### Инструменты снижения трафика, по силе эффекта

1. **Interest Management / Area of Interest** — самое сильное средство. Режимы Object Interest: `Area Of Interest` (требует `Replication Features = Scheduling and Interest Management`, дефолт — `Scheduling`, и наследника `NetworkTRSP` на том же GameObject), `Global`, `Explicit`. Регионы — `Runner.AddPlayerAreaOfInterest()`, точечный интерес — `SetPlayerAlwaysInterested()`. Фильтр на уровне поведения — `ReplicateTo(PlayerRef)`: только Server/Host и **не влияет на RPC**. `IInterestEnter` не вызывается при Spawn, `IInterestExit` — при Despawn. В Shared у игрока поддерживается только один AoI ([interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management), [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)). Подробно — [гл. 08](08-chapter.md).
2. **Send Priority (2.1+)** — `NetworkObject.SetPriority(player, N)`: объект уходит каждый N-й send-тик, дефолт 1, сброс `ClearPriority(player)`. **Приоритеты нужно переустанавливать после host migration** — «Priorities must be reapplied after host migration» ([whats-new-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/whats-new-2-1)). Побочный эффект редких апдейтов — рваное движение; документация даёт два рычага: `NetworkProjectConfig.TimeSynchronizationOverride` ([whats-new-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/whats-new-2-1)) и `ObjectInactivityThreshold` (дефолт **0,251 с**, правило `2 × priority / send rate`, задаётся только из кода до старта раннера) — но сам `ObjectInactivityThreshold` **появился лишь в 2.1.4** ([send-priority](https://doc.photonengine.com/fusion/v2/manual/advanced/send-priority)), то есть на 2.1.0–2.1.3 этот рецепт неприменим. Диагностика — trace-канал `SendPriority`.
3. **Send rate** — интервал 1…8, обязан делить tick rate без остатка ([network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)).
4. **Eventual Consistency** — `SimulationConfig.ObjectDataConsistency = Eventual`, только из кода: отправляются лишь изменения изменившихся объектов, но атомарность объекта теряется — свойства могут приехать разными тиками ([eventual-consistency](https://doc.photonengine.com/fusion/v2/manual/data-transfer/eventual-consistency)).
5. **Никаких строк** в `[Networked]` и RPC — «Avoid the usage of strings in RPCs and Networked Properties» ([optimizations](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations)). Большие данные — Data Streaming: `SendReliableDataToPlayer/Server`, ключ `ReliableKey.FromInts(...)` или из двух `ulong` (16 байт), коллбэки `OnReliableDataReceived` / `OnReliableDataProgress` ([data-streaming](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming)).

---

## 31.3. Профилирование: Statistics, дашборд, своя телеметрия

**Fusion Statistics собирает данные только в DEBUG DLL** — в release-сборке счётчиков нет по дизайну. Включение: компонент `FusionStatistics` на объекте раннера до старта, кнопка в Network Runner Controls, либо `NetworkRunner.SetupStatistics()` / `RemoveStatistics()`; для VR — `SetStatisticsWorldAnchor(Anchor)` ([fusion-statistics-2.1](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/fusion-statistics-2.1)).

| Страница | Что даёт | Для чего в проде |
|---|---|---|
| Simulation | Forward Ticks, Re-simulation Ticks, Objects Update In/Out | рост ресимуляций → смотреть Input Delay |
| Network | RTT, In/Out Bandwidth (байты), In/Out Packets, Input Bandwidth | сверка с бюджетом ~1,2 КБ/с |
| **Object** | in/out bandwidth конкретного `NetworkObject` | **единственный способ узнать, кто съедает канал** |
| Behaviour | время и число вызовов каждого `NetworkBehaviour`, отдельно FUN и Render | поиск дорогого кода в тике |
| Memory | две внутренние арены; 100% = память кончилась | диагностика `OutOfMemoryException` |
| Lag Compensation | процент занятых хитбоксов | ёмкость хитбокс-буфера |
| Time | интерполяционный offset | качество интерполяции |

Чтение из кода (API Fusion 2.1), *код из документации*:

```csharp
if (Runner.TryGetFusionStatistics(out var m)) {
    var rtt   = m.SimulationSnapshot.Stats[FusionStatType.RoundTripTime];
    var ticks = m.SimulationSnapshot.Stats[FusionStatType.ForwardTicks];
    var free  = m.MemorySnapshot.GeneralAllocatorMemorySnapshot.TotalBytesFree;
}
```

**Порядок работы с трафиком:** общий Out Bandwidth → добавить подозрительные объекты на страницу Object → найти непропорциональную долю → снизить приоритет / вынести из Global interest / урезать состояние.

**Шаблон продовой телеметрии** даёт сэмпл BR200: запуск с флагом `-recordSession` пишет engine delta time и число игроков каждый кадр в CSV (путь — `Application.persistentDataPath` или `-dataPath XXX`), python-скрипты из `Assets/Extras.zip` строят HTML-графики, расширяется своим `StatsRecorder` ([br200 performance](https://doc.photonengine.com/fusion/v2/game-samples/br200/performance-optimizations)). Идея переносимая: в прод идёт не Unity Profiler, а дешёвый поток ключевых чисел.

**Второй источник правды — дашборд.** Counter Analytics даёт CCU, CCU Peaks, Rooms, CCU per Room, **Msg/s per Room**, Bandwidth/s (и per Peer для Master/Gameserver/WebHooks), Traffic, **Disconnects/s per Peer**. Биллинг считается по сумме peak-CCU по регионам за месяц, поэтому для сверки берут «All Regions» и месячный интервал. Ограничения: «Free apps are restricted to the 24 hours timespan and the All Regions graphs», а «Our Counter API is available with Premium Cloud, Circle and Enterprise plans» ([counter-analytics](https://doc.photonengine.com/fusion/v2/reference/counter-analytics)).

**Продовый минимум логирования:**

1. Один агрегированный лог на разрыв: `DisconnectInfo.ToString()` + `CloudDisconnectCause` + регион + `AppVersion` + платформа.
2. Признак нестабильного канала — рост `ResentReliableCommands`: «If this value goes through the roof, the connection is unstable». Рычаги — `QuickResendAttempts` (имеет смысл понижать) и `SentCountAllowance` (эффект «subtle») ([analyzing-disconnects](https://doc.photonengine.com/fusion/v2/troubleshooting/analyzing-disconnects)).
3. Диагностический режим по флагу: `Fusion/Toggle Debug DLLs` + `Network Logging = ALL` в `PhotonAppSettings` — штатный рецепт перед отправкой логов в саппорт.

---

## 31.4. CPU, память, масштабирование сервера

- **Пулинг обязателен.** По умолчанию каждый Spawn/Despawn — Instantiate/Destroy. BR200 реализует `NetworkObjectPool : INetworkObjectProvider` и передаёт его в `StartGame`, плюс отдельный `ObjectCache` для несетевых объектов с возвратом по таймеру ([br200 performance](https://doc.photonengine.com/fusion/v2/game-samples/br200/performance-optimizations)). Пулинг на уровне механик — [гл. 02](02-chapter.md).
- **`targetFrameRate` на сервере обязателен:** «On the server always set a target frame rate or Unity will run as many FPS as possible and use 100% CPU» ([optimizations](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations)). *Код из документации:*

```csharp
Application.targetFrameRate =
    TickRate.Resolve(NetworkProjectConfig.Global.Simulation.TickRateSelection).Server;
```

- **Heap Fusion преаллоцируется:** «The total size of the heap is PageCount * PageShift». `Page Shift` — от 1 КБ до 256 КБ степенями двойки (держать ≥ 16 КБ), `Page Count` — 16…4096; нехватка даёт `OutOfMemoryException` ([optimizations](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations), [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)).
- **Tick rate режет CPU сервера напрямую; send rate — почти нет.**

Референсные числа масштабирования BR200 — максимум игроков при стабильных 60 Гц на headless dedicated ([br200 performance](https://doc.photonengine.com/fusion/v2/game-samples/br200/performance-optimizations)):

| Железо | Игроков |
|---|---|
| Mac Mini M1 2020 (Silicon) | 200 |
| Core i9-10900K, Windows server | 200 |
| Bare metal Xeon E-2288G @ 3.70 ГГц, Linux | 200 |
| **Виртуалка GCP N2 @ 2.80 ГГц, Linux** | **60–80** |

Переход с bare metal на VM стоит 2,5–3× по capacity. **Важно:** интеграция Unity Game Server Hosting **удалена из сэмпла в версии 2.1.0** («The Unity Game Server Hosting integration was removed from the sample in version 2.1.0»), и числа оставлены только «as a hardware reference» — это не действующий бенчмарк сервиса.

Dedicated server как цель релиза: Server Mode — peer без локального игрока, headless определяется наличием `-batchmode -nographics` (документация называет их «mandatory»); аргументы `-session` (дефолт — случайный GUID), `-region` (дефолт Best Region), `-lobby` (дефолт ClientServer-лобби), `-port` (**дефолт 27015**), `-P<name> <value>`, `-logFile`. При смене build target надо убедиться, что для него определён символ **`FUSION_WEAVER`**, иначе weaving не произойдёт ([dedicated-server](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server)). Хостинг этих серверов в Public/Premium Cloud **не входит**: «Dedicated Servers» перечислены только в составе Enterprise Cloud ([pricing](https://www.photonengine.com/en-US/Fusion/pricing)); сама страница сэмпла вопрос «кто хостит» не обсуждает. Выбор топологии: Dedicated Server «Only recommended for high ARPU mobile titles» — дорог из-за сторонного хостинга ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)).

---

## 31.5. Регионы, матчмейкинг и лимиты

Сессия живёт **только внутри одного региона**: «Game Sessions only exist within a single Region» ([matchmaking](https://doc.photonengine.com/fusion/current/manual/matchmaking)). Без явной настройки Fusion идёт в Best Region, и **этот выбор недетерминирован** — при близких или равных пингах разные устройства в одной сети могут попасть в разные регионы ([regions](https://doc.photonengine.com/fusion/v2/connection-and-authentication/regions)).

Коды регионов: `asia` (Singapore), `au` (Sydney), `cae` (Montreal), `cn` (Shanghai, спецдоступ), `eu` (Amsterdam), `hk` (Hong Kong), `in` (Chennai), `jp` (Tokyo), `za` (Johannesburg), `sa` (Sao Paulo), `kr` (Seoul), `tr` (Istanbul), `uae` (Dubai), `us` (Washington D.C.), `usw` (San José), `ussc` (Dallas). Регистр не важен.

| Способ | Как | Когда брать |
|---|---|---|
| Best Region (дефолт) | ничего не делать | обычный прод |
| **Region Allowlist** | дашборд → Manage → «Edit Allowlist», коды через `;` (`"eu;us"`); пустая или битая строка = все регионы; нераспознанные коды игнорируются; Name Server раздаёт список **в течение 10 минут** | штатный способ развести `us`/`usw` и сузить географию без релиза билда |
| `GetAvailableRegions()` | `NetworkRunner.GetAvailableRegions()` → `Fusion.RegionInfo` (имя + ping); нужен `NetworkRunner` в сцене | ручной выбор региона игроком |
| `FixedRegion` | `PhotonAppSettings.Global.AppSettings.GetCopy()` → `FixedRegion = region.ToLower()` → `StartGameArgs.CustomPhotonAppSettings` | фиксация под один рынок; **вкомпилировано в билд** |

Регион меняется только пока раннер не запущен. Для материкового Китая нужен отдельный name server `ns.photonengine.cn`, отдельный билд и, желательно, отдельный AppId; подписки для этого региона через дашборд не покупаются.

> **Предупреждение про биллинг:** «Changing the available regions for a popular app will affect the Peak CCUs in multiple regions» ([regions](https://doc.photonengine.com/fusion/v2/connection-and-authentication/regions)). Allowlist — это не только UX, это немедленное изменение счёта.

**Лимиты матчмейкинга, о которые бьются в прод** ([matchmaking](https://doc.photonengine.com/fusion/current/manual/matchmaking)): `SessionProperties` — максимум **10 свойств и 500 байт** сериализованно; превышение роняет `StartGame` с `ShutdownReason.InvalidArguments` во всех режимах, кроме `GameMode.Single`. Проверить до падения можно `UpdateCustomProperties` — он возвращает **false с предупреждением**, если результат выйдет за лимит. Браузеры сессий «strongly discouraged» (безопасность, нагрузка на сервер, список привязан к региону); рекомендованные альтернативы — random join, random join с фильтрами по свойствам, обмен `SessionName` как инвайт-кодом. `MatchmakingMode`: `FillRoom` (дефолт, набивает старые комнаты первыми), `SerialMatching` (раскладывает игроков по комнатам последовательно), `RandomMatching` (случайная комната, нужны matching properties).

---

## 31.6. Мобильные платформы и WebGL

### Мобила

Официальные Known Issues ([known-issues](https://doc.photonengine.com/fusion/v2/troubleshooting/known-issues)):

- Уход в фон «pauses the main message loop which is responsible for keeping the client connected». Триггеры: кнопка home, звонок, видеореклама, оверлейные плагины. Если пауза длиннее **дефолтного 10-секундного таймаута клиента**, соединение рвётся, а коллбэк таймаута приходит **только после возврата в foreground**.
- На iOS «applications can not keep a connection, while in background» — держать соединение в фоне нельзя в принципе.
- `Application.runInBackground` «is not supported on mobile platforms» — использовать `OnApplicationPause`.
- Для возврата в ту же комнату с тем же actor number нужны достаточные `PlayerTTL` и `EmptyRoomTTL`; конкретных значений эта страница не даёт, рекомендация ≥ **12000 мс** приходит с [analyzing-disconnects](https://doc.photonengine.com/fusion/v2/troubleshooting/analyzing-disconnects).
- **iOS App Store:** реджекты «due to connection issues» бывают из-за заблокированного UDP; «Newer Photon clients can automatically fallback to using TCP or WSS, if UDP is not connecting». Решается апелляцией.
- **iOS Local Network Privacy popup:** в Shared «The popup will never trigger»; в ClientServer — только если хост и клиент в одной локальной сети, и только при первом запуске; текст — `NSLocalNetworkUsageDescription` в `info.plist`; «there is no way to bypass this». В разработке попап всплывает постоянно из-за переустановок — это объясняет «ложные» баг-репорты от QA.
- **UWP:** нужна capability `InternetClient` (ошибка «A network capability is required to access the network!»); и UWP-приложения изолированы AppContainer'ом, поэтому **не могут подключиться к серверу на той же машине Windows** — ловушка локального тестирования.

Топологический вывод, которого в курсе нет: Shared Mode — «The recommended mode for mobile» и единственный режим без ресимуляций, то есть дешевле по CPU, а «Host Mode is not recommended for mobile except for special use-cases» — плохой канал хоста ломает всех, и успех NAT punchthrough на мобиле ниже ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)). Client Host позиционируется на «games with few players within a session (2-4)». При Client Host релей — не экзотика: «approximately 1 in 10 players» идёт через релей, когда UDP NAT punch-through не прошёл ([network-topologies](https://doc.photonengine.com/fusion/v2/manual/network-topologies)). ⚠️ Числовых данных отдельно по мобильным сетям (насколько именно хуже проходит punchthrough) в документации нет — есть только качественная оценка.

### WebGL

- «We strongly recommend Shared Mode on WebGL»; Server и Host mode «are not recommended» и отключены по умолчанию — включаются опцией **«Enable ClientServer Modes in WebGL builds»** в `NetworkProjectConfig`. WebGL-сервер «will always utilize a relay connection since direct connections are not supported for WebGL builds» ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose), [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)).
- Следствие: **Input Delay доступен только в Server и Host Mode** ([whats-new-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/whats-new-2-1)), то есть на WebGL-Shared его нет. Lag Compensation ([гл. 06](06-chapter.md)) — тоже серверная механика Host/Server-режимов; ⚠️ страницу с дословной формулировкой «only available in Server Mode and Host Mode» в рамках исчерпанного бюджета поиска переоткрыть не удалось — проверь перед проектированием WebGL-шутера.
- Порты: WS **9093** (name server) / **9090** (master) / **9091** (game), WSS **19093 / 19090 / 19091**; UDP — 5058/27000, 5055/27001, 5056/27002; TCP — 4533/4530/4531 ([ports](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/tcp-and-udp-port-numbers)). ⚠️ Прямой связки «WebGL ⇒ WebSocket/WSS» ни страница портов, ни страница сэмпла не формулируют, хотя порты WS/WSS существуют именно для этого.
- Сборка: `Run In Background` обязателен; многопоточный режим поддерживается не везде (на CrazyGames — нет); размер первой загрузки резать Addressables / Streaming Assets и sprite atlas'ами. Сэмпл собран на Unity 6000.0.78f1 и на Shared Mode. В 2.1.3 исправлена «TaskManager.Delay precision loss on long-running WebGL builds» — у долгих WebGL-сессий своя категория баг-риска ([fusion-webgl](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-webgl)).
- Браузеры «will not run JS and WebAssembly in the background»; иногда помогает неслышимый звук, но «only works in some cases» ([known-issues](https://doc.photonengine.com/fusion/v2/troubleshooting/known-issues)).

---

## 31.7. Реконнект и матрица причин разрыва

### Автоматический реконнект

Начиная с **Fusion 2.0.5** работает Photon Connection Lost & Quick Reconnect: при потере связи с облаком peer не выключается, а пытается перезайти **до пяти раз**; «Shared Mode is not supported». Триггеры — `ShutdownReason.PhotonCloudTimeout` и `ShutdownReason.GameNotFound` ([lost-connection-handling](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/lost-connection-handling)). *Код из документации:*

```csharp
// назначать ДО StartGame(); то же относится к CloudConnectionLostCurrentMode
NetworkRunner.CloudConnectionLost += (NetworkRunner runner, ShutdownReason reason, bool reconnecting) => {
    // reconnecting == true  -> идёт перезаход в ту же сессию, показать экран "переподключение"
    // reconnecting == false -> это настоящий разрыв
};
```

Режимы (2.1+) — `CloudConnectionLostMode.Disabled` / `QuickRejoin` / `MoveSessionToNewRoom`; для сервера дефолт — `MoveSessionToNewRoom`.

- **QuickRejoin:** если сервер не смог перезайти, плагин ставит сессию **Invisible и Closed**; новые клиенты не входят, а «Existing **directly connected** clients will stay connected and continue playing normally». То есть **релеенные игроки — те самые ~1 из 10 — при quick rejoin теряются.** Это дыра, которую закрывают в UX, а не надеются на автоматику.
- **MoveSessionToNewRoom:** сервер создаёт новую сессию, Session ID получает суффикс `[bpk-<6 случайных цифр>]` (вся строка максимум **200 символов**, лишнее обрезается автоматически), велит напрямую подключённым клиентам перейти и даёт им **до 10 секунд**.

На уровне облака для quick rejoin нужен **`PlayerTTL != 0`, рекомендуется минимум 12000 мс** — и это «does not guarantee the rejoin will work» ([analyzing-disconnects](https://doc.photonengine.com/fusion/v2/troubleshooting/analyzing-disconnects)).

Восстановление личности игрока — три паттерна (подробнее в [гл. 09](09-chapter.md), где `ConnectionToken` разобран на учебном проекте): `StartGameArgs.ConnectionToken` + `Runner.GetPlayerConnectionToken(PlayerRef)` и переназначение `InputAuthority` существующему объекту вместо спавна нового; Host Migration; в Shared — `SessionID` плюс `Allow State Authority Override = on` и `Destroy When State Authority Leaves = off`. В 2.1 добавился `StartGameArgs.PlayerUniqueId` — стабильный `PlayerRef` между реконнектами, поддержаны Shared и Client-Server. UX-деталь: окно «disconnected» не показывают, если `ShutdownReason == HostMigration`.

### Три перечисления причин — не путать

Всё из [disconnect-info](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/disconnect-info):

| Перечисление | Значения | Где читать |
|---|---|---|
| `NetDisconnectReason` | `Timeout`, `SendWindowFull`, `SequenceOutOfBounds`, `Requested`, `ByRemote`, `Unknown` | `OnDisconnectedFromServer`; первые три детектируются любой стороной и **не говорят, кто инициатор** |
| `Fusion.Protocol.DisconnectReason` | `ConnectionTimeout`, `SendWindowFull`, `SequenceOutOfBounds`, `RequestedByClient`, `ServerHasDisconnected`, `ServerLogic` (+ `IncompatibleConfiguration`, `ServerAlreadyInRoom`, `Error` для провалов join) | все приходят в `OnShutdown` как **`ShutdownReason.DisconnectedByPluginLogic`**; детализация доступна только если **все** peer'ы на 2.0.14+ / 2.1.4+, иначе плагин пришлёт `ServerLogic` |
| `DisconnectInfo` | `ShutdownReason`, `NetDisconnectReason?`, `Token` (**отправляются только первые 128 байт**), `PluginDisconnectReason?`, `CloudDisconnectCause` (строка Realtime, напр. `"ClientTimeout"`), `Player`, `Message` | `NetworkRunner.DisconnectInfo` — своя запись, живёт после shutdown до следующего `StartGame`; `Runner.GetPlayerDisconnectInfo(PlayerRef)` — запись удалённого игрока, читать в `OnPlayerLeft`, **только на Server/Host** (на клиентах null) |

Правило: зафиксированная причина **не перезатирается** последующим обычным `Shutdown()`, поэтому логировать нужно `DisconnectInfo.ToString()`, а не аргумент `OnShutdown`.

Отдельный класс — отказ аутентификации при коннекте ([subscription-error-cases](https://doc.photonengine.com/fusion/v2/troubleshooting/subscription-error-cases)): **Unknown or Archived AppId** («This happens if the provided AppId is not known to the server at all» — опечатка или не тот тип приложения), **CCU Limit Reached** («This is a temporary measure. Once the CCU is below the limit, players will be able to connect an play again») и **Plugin Mismatch** (AppId не того типа, что SDK — например Realtime/Voice с Fusion). ⚠️ Описания падения кастомной аутентификации на этой странице нет; `ShutdownReason.CustomAuthenticationFailed` и `StartGameResult.ErrorMessage` надо сверять по странице Custom Authentication отдельно — в рамках этой главы первоисточник не открыт.

---

## 31.8. Версионирование протокола и серверные настройки

- **`AppVersion`** в `PhotonAppSettings` сегментирует игроков по билду: несовместимые версии не попадают в одни комнаты. Политика «менять `AppVersion` при любом изменении сетевого контракта (состав `[Networked]`, подписи RPC, формат `INetworkStruct`, tick/send rate, набор префабов) и не менять для совместимых патчей» — **разумная практика, но официально как политика не документирована**; документирован только сам механизм разделения билдов в матчмейкинге.
- `AppVersion` годится и как feature-флаг окружения: BR200 считает билд публичным, если поле App Version содержит подстроку `-public` (пример `1.0-public`), и отключает создание игр и триггер дропа; обход для модератора — аргумент `-moderator`. Сами авторы пишут: «this solution uses security by obscurity and must be removed from the code at some point» ([br200 testing](https://doc.photonengine.com/fusion/v2/game-samples/br200/testing)).
- **Часть «протокола» меняется без релиза билда** — в дашборде ([dashboard-config](https://doc.photonengine.com/fusion/v2/manual/advanced/dashboard-config)):

| Переменная | Дефолт | Смысл |
|---|---|---|
| `FusionSessionCreationControlEnable` | `false` | ограничить, какой UserID вправе создавать сессии (простое сравнение на равенство) |
| `FusionSessionCreationControlServerId` | `""` | этот UserID |
| `FusionDummyTrafficEnable` | `false` | периодические пустые пакеты между плагином и peer'ами для удержания соединений |
| `FusionDummyTrafficInterval` | **100 мс** | как часто |
| `FusionDummyTrafficSize` | **2 байта** | размер |
| `FusionSharedTickEnable` (2.1+) | не указан на странице | управление tick rate в Shared с сервера |
| `FusionSharedTickRate` (2.1+) | не указан | 8…32; **переопределяет то, что просит клиент** |
| `FusionSharedTickSendInterval` (2.1+) | не указан | 1…8, должен делить tick rate; иначе берётся следующий подходящий (32 и 3 → фактически 2) |

Для серьёзной проверки прав на создание сессии документация сама отсылает к **webhooks**: «webhooks should be used instead» — сравнение UserID на равенство это не авторизация.

- **Шифрование** ([encryption](https://doc.photonengine.com/fusion/v2/manual/advanced/encryption)): нужен native-плагин `DatagramEncryption`, которого **нет в пакете** («This plugin is not included in the default Fusion package due to its size and rare use») — запрашивается в Photon Support. Облачное соединение: `PhotonAppSettings` → Port **443**, Protocol **UDP**, AuthMode **Auth Once Wss**, Encryption Mode **Datagram Encryption GCM**; это покрывает и Shared, потому что «in `Shared` mode, there is only 1 type of connection». Прямое соединение — Fusion Encryption System в `NetworkProjectConfig`, только для `Client`, `Host`, `Server`, `AutoHostOrClient`. Шифр — AES с 256-битным ключом в CBC, целостность HMACSHA256, пакет с непройденной проверкой просто отбрасывается.

---

## 31.9. Тестирование: лаги, боты, нагрузка

**Встроенный симулятор** — `NetworkProjectConfig` → foldout **Network Conditions**, только в debug DLL (`Fusion > Toggle Debug Dlls`, возможен перезапуск редактора). Поля: `Delay Shape`, `Delay Min`/`Max` (**0…0,5 с**), `Delay Period`, `Delay Threshold`, `Additional Jitter`, `Loss Chance Shape`, `Loss Chance Min`/`Max` (**0…1**), `Loss Chance Threshold`, `Loss Chance Period`, `Additional Loss`; фиксированное значение = равные Min и Max ([simulating-network-conditions](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/simulating-network-conditions); численные диапазоны — [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)).

> **Семантика, на которой ошибаются:** в Host/Server каждый peer применяет **половину** заданного значения — 200 мс дают ~100 мс на сервере плюс ~100 мс на клиенте. В Shared условия добавляют только клиенты, поэтому для Shared значения надо **удваивать**. Симуляция складывается с реальной латентностью.

**Для release-билдов штатного симулятора нет.** Photon отсылает к **Clumsy**; точный фильтр только по игровому трафику — `udp.DstPort == 5056 or udp.SrcPort == 5056 or udp.DstPort == 27002 or udp.SrcPort == 27002` (грубый `udp` режет весь UDP на машине). Калибровка: «Additional lag of 50ms to each will increase the rountrip times by 100ms»; Drop = 100% имитирует полную потерю связи. Для разбора трафика — Wireshark: `portrange 5055-5058 or portrange 27000-27003`, для WebSocket — `portrange 9090-9093 or portrange 19090-19093` ([analyzing-disconnects](https://doc.photonengine.com/fusion/v2/troubleshooting/analyzing-disconnects)). ⚠️ Network Link Conditioner на страницах Photon не упоминается — инструмент существует, но как рекомендация Photon не подтверждён.

**Матрица перед релизом:** 0 / 50 / 150 / 300 мс RTT × 0 / 1 / 5 % потерь × {хост на Wi-Fi, клиент на LTE} × {уход в фон на 5 и 15 с} × {убийство хоста} × {релей вместо direct}.

**Процесс коннекта** — знать, чтобы читать таймауты: «The connection attempts interval is fixed at 500ms, and the connection attempts limit is set to 10»; после 2/3 лимита клиент начинает дублировать запросы через облачный релей, по исчерпании лимита выключает сетевые операции ([connection-process](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/connection-process)). «Висит ~5 секунд и падает» — это ровно 10 × 500 мс, а не баг.

**Три штатных уровня нагрузочного тестирования:**

1. **Multi-Peer Mode** — `Peer Mode = Multiple` в `NetworkProjectConfig`: у каждого `NetworkRunner` своя Fusion-симуляция, **своя физическая сцена**, своё соединение и свои сцены. Отсюда жёсткое требование: «avoid static variables and singletons for anything that affects simulation». Видимость — `RunnerEnableVisibility` + `SetVisible(bool)`, `EnableOnSingleRunner` оставляет один AudioListener/EventSystem; управление — `Tools > Fusion > Windows > Network Runner Controls` ([multipeer](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/multipeer)). В BR200 то же проще: `StandaloneManager.ExtraPeers = 2`, numpad 1/2/3 — переключение peer'ов, 4/5/6 — показать всех.
2. **Боты-клиенты процессами** — паттерн Casual MMO: компонент `PlayerAI` на префабе `Player` (переключается клавишей `I`), файлы `BatchStart-10Clients-Log1..4.bat`, каждый поднимает «10 bot clients in two processes with 5 clients each» в сессию `BatchTest`; масштаб ограничен ресурсами ПК, в редакторе — `Simulate Batch Mode` на `BatchModeManager` ([casual-mmo testing](https://doc.photonengine.com/fusion/v2/game-samples/casual-mmo/debugging-testing)).
3. **Headless dedicated + запись метрик** — паттерн BR200 (`-recordSession`, `-dataPath`).

**Отладка в 2.1 стала принципиально другой:** «In Host / Server Mode debugging using breakpoints is possible without triggering a timeout», поддержаны Multi-Peer и Unity Multiplayer Play Mode ([whats-new-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/whats-new-2-1)). На 2.0.5 из курса брейкпоинт в хосте означал таймаут — именно поэтому там всё отлаживается логами.

---

## 31.10. Типовые ошибки и как их диагностируют

| Симптом | Наиболее вероятная причина | Проверка |
|---|---|---|
| Коннект висит ~5 с и падает | исчерпаны 10 × 500 мс попыток, релей тоже не помог | firewall / VPN / UDP; порты 5055-5058 и 27000-27002 |
| «Unknown or Archived AppId» | опечатка или не тот тип AppId | тип приложения в дашборде |
| Внезапные отказы у части игроков под нагрузкой | CCU Limit Reached (временная) | дашборд CCU vs план; CCU Burst |
| `StartGame` падает с `InvalidArguments` | `SessionProperties` > 10 свойств или > 500 байт | `UpdateCustomProperties` вернёт false заранее |
| Клиенты отваливаются при сворачивании | пауза main loop > 10 с | `OnApplicationPause` + `PlayerTTL` / `EmptyRoomTTL` |
| Реджект в App Store «connection issues» | у ревьюера заблокирован UDP | апелляция; проверить fallback на TCP/WSS |
| Рваное движение после включения приоритетов | слишком редкие апдейты | trace-канал `SendPriority`; `TimeSynchronizationOverride`, а `ObjectInactivityThreshold` только с 2.1.4 |
| `OutOfMemoryException` в Fusion | мал heap (`PageCount × PageShift`) | Memory-страница Statistics; `PageShift` ≥ 16 КБ |
| Часть игроков теряется при quick rejoin | сохраняются только directly connected клиенты | логи `CloudConnectionLost` |
| UWP не видит сервер на той же машине | изоляция AppContainer, так по дизайну | тестировать с двух машин |
| RPC «иногда не доходит» | один из трёх reliability exceptions | см. ниже |
| Bool-переключатель «не сработал» | быстрые изменения `[Networked]` склеились | заменить bool на инкрементируемый int (чётное = false) |
| Пропали меню и статистика | удалён `com.unity.ugui` (с 2.1.3 он необязателен) | вернуть пакет |
| После апгрейда на 2.1 не компилируется Data Streaming | `OnReliableDataReceived` отдаёт `ReadOnlySpan<byte>` | обновить подпись коллбэка |

**Reliability exceptions — это контракт, а не баги** ([reliability-exceptions](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/reliability-exceptions)). `[Networked]`: промежуточные значения теряются (5→6→7 может приехать как единственное изменение 5→7; `true→false→true` может не дать изменения вовсе — поэтому рекомендуется инкрементируемый int вместо bool). В Shared запись без StateAuthority создаёт локальный Shared Mode Prediction, который «is not automatically reset after a certain period of time» — вплоть до постоянной десинхронизации; в 2.1+ есть `NetworkObject.ResetToLatestState()`. RPC теряется, когда объекта нет на принимающем peer'е, когда сменилась State/Input Authority в полёте и когда клиент отключился; «RPCs sent in `Spawned()` or shortly after the object is spawned are not guaranteed to arrive», а перед despawn нужен подтверждающий обратный RPC. Подробный разбор RPC — [гл. 07](07-chapter.md).

---

## Чего по этой теме нет в учебном курсе

Курс — это Host Mode, Fusion 2.0.5, Unity 2022.3.42f1, два редактора через ParrelSync. Поэтому в нём физически отсутствует:

| Тема | Что есть в курсе | Чего нет |
|---|---|---|
| Трафик | Interest Management как механика ([гл. 08](08-chapter.md)) | бюджет в байт/с, биллинг, MTU, Send Priority (фича 2.1) |
| Профилирование | ручные тесты через `[Button]` | Fusion Statistics, debug DLL, дашборд, CSV-телеметрия |
| Масштаб | 2–4 peer'а в редакторе | headless dedicated, `targetFrameRate`, heap, числа по железу |
| Разрывы | `ConnectionToken` и Host Migration ([гл. 09](09-chapter.md)) | `CloudConnectionLost`, три перечисления причин, `PlayerTTL`, `PlayerUniqueId` |
| Платформы | редактор на Windows | мобильный 10-секундный таймаут, iOS popup, App Store, WebGL, UWP |
| Топология | только Host ([00-README](00-README.md), §0.2) | что для мобилы и WebGL рекомендован Shared, а значит [гл. 06](06-chapter.md) (Lag Compensation) там неприменима |
| Лаги | нет | симулятор Network Conditions, Clumsy, Wireshark, матрица тестов |
| Боты | нет | Multi-Peer, batch-боты, нагрузочные прогоны |
| Версии | 2.0.5 как данность | несовместимость 2.0 ↔ 2.1, `AppVersion`, dashboard-config |
| Безопасность | нет | шифрование, webhooks, античит в Shared (Enterprise) |

---

## Решение: что брать и когда

| Ситуация | Решение | Почему |
|---|---|---|
| Мобильный проект, 2–8 игроков | **Shared Mode** | «The recommended mode for mobile», нет ресимуляций; Host «not recommended for mobile» |
| Мобильный шутер с попаданиями, высокий ARPU | Dedicated Server | Input Delay и Lag Compensation — Server/Host; «Only recommended for high ARPU mobile titles» |
| Браузерная игра | **Shared Mode** на WebGL | Server/Host отключены по умолчанию и всегда через релей |
| Кооп на 2–4 игрока, экономия на хостинге | Client Host | штатная ниша Client Host; принять ~1 из 10 через релей |
| Нужны Send Priority / tick > 32 в Shared / RPC > 512 Б | апгрейд 2.0.x(последний) → 2.1.x + смена `AppVersion` + окно downtime | 2.1 несовместим с 2.0, нужен Realtime 5 |
| Трафик выше ~1,2 КБ/с на игрока | сначала Interest Management, потом Send Priority, потом send rate, в последнюю очередь Eventual Consistency | порядок по силе эффекта и по риску для геймплея |
| Нужно сузить географию / развести `us` и `usw` | **Region Allowlist** в дашборде | меняется без релиза, доезжает ≤ 10 мин; `FixedRegion` вкомпилирован в билд |
| Нужны метрики в проде | свой CSV/телеметрия + дашборд | Fusion Statistics нет в release DLL |
| Нужна проверка прав на создание сессии | **webhooks**, не `FusionSessionCreationControl` | равенство UserID — не авторизация, так говорит документация |
| Передать > 512 байт на 2.0.x | Data Streaming | лимит RPC 512 Б снят только в 2.1 |
| Нужен античит в Shared | Shared Mode Custom Plugins | только Enterprise Cloud, доступ по запросу |

---

## Чеклист

1. Зафиксировать и записать версии: весь проект на одной ветке (2.0.x **или** 2.1.x), Unity — из поддерживаемого списка (2021.3.45 / 2022.3.45 / 6.0.x / 6.3.x), вариант Voice SDK подобран под ветку Fusion.
2. Измерить Out Bandwidth на игрока в худшем сценарии (страница Network в Statistics) и сравнить с **~1,2 КБ/с**; убедиться, что снапшот меньше **1280 байт**.
3. Пройти страницу **Object** в Statistics и убить топ-3 потребителя канала; только после этого крутить send rate.
4. Включить Interest Management (`Replication Features = Scheduling and Interest Management`) и проверить, что `ReplicateTo` не используется там, где нужны RPC.
5. Завести пулинг через `INetworkObjectProvider` для всего, что спавнится чаще раза в секунду, плюс отдельный кэш несетевых эффектов.
6. На сервере выставить `Application.targetFrameRate` по серверному tick rate; настроить heap (`PageShift` ≥ 16 КБ) по факту из Memory-страницы.
7. Обработать **все** `ShutdownReason` и `NetDisconnectReason` явным switch; логировать `DisconnectInfo.ToString()`, а не аргумент `OnShutdown`; на Server/Host читать `GetPlayerDisconnectInfo` в `OnPlayerLeft`.
8. Реализовать реконнект: подписка на `CloudConnectionLost` **до** `StartGame`, `PlayerTTL ≥ 12000 мс`, `ConnectionToken` или `PlayerUniqueId`, экран «переподключение» с явной кнопкой выхода; отдельно продумать судьбу релеенных игроков при quick rejoin.
9. Мобила: `OnApplicationPause` → осознанный disconnect при уходе в фон; `NSLocalNetworkUsageDescription` в `info.plist`; для UWP — capability `InternetClient`; не тестировать UWP-клиент и сервер на одной машине.
10. WebGL: Shared Mode, `Run In Background`, не рассчитывать на Lag Compensation и Input Delay, измерить размер первой загрузки.
11. Прогнать матрицу лагов в debug DLL (помня про «половину значения» в Host/Server и удвоение в Shared) и Clumsy на release-билде с точным портовым фильтром.
12. Прогнать ботов до целевого CCU: Multi-Peer в редакторе, затем batch-процессы, затем headless с `-recordSession -dataPath`.
13. Проверить лимиты матчмейкинга: `SessionProperties` ≤ 10 и ≤ 500 байт, проверять через `UpdateCustomProperties`; отказаться от браузера сессий в пользу random join с фильтрами.
14. Настроить дашборд: Region Allowlist, `FusionSessionCreationControl` или webhooks, при необходимости `FusionDummyTraffic`; помнить, что смена набора регионов сразу меняет Peak CCU и счёт.
15. Сменить `AppVersion` вместе с любым изменением сетевого контракта и задокументировать политику версий; не оставлять `-public`-подобные флаги в релизном коде.
16. Если есть что защищать — запросить плагин `DatagramEncryption` в Photon Support и настроить Port 443 / UDP / Auth Once Wss / Datagram Encryption GCM, плюс Fusion Encryption System для ClientServer-режимов.
17. Сверить фактический трафик и peak CCU по регионам в дашборде («All Regions», месячный интервал) за первый месяц и скорректировать план до прихода овереджа.

---

## Источники

Все URL проверены 2026-11-01. ⚠️ Бюджет веб-поиска в этой сессии был исчерпан, поэтому форум Photon и Discord не просматривались — всё ниже основано на `doc.photonengine.com` и странице тарифов; «народные» практики (поведение релея в дикой природе, политики версионирования студий, Network Link Conditioner) остаются неподтверждёнными.

- [getting-started/sdk-download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download) — версии 2.1.3 build 2390 и 2.0.13 build 2379, поддерживаемые Unity и платформы, несовместимость 2.1 с 2.0, требование Realtime 5, два варианта Voice SDK 2.64.
- [whats-new-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/whats-new-2-1) — tick rate 8…256, снятие лимита RPC 512 Б, Input Delay, `RequestMasterClientReassign` + порог 2 с, Custom Plugins, `PlayerUniqueId`, Debug Breakpoints, приоритеты после host migration, `TimeSynchronizationOverride`.
- [network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config) — лок 32/16 в Fusion 2, диапазоны heap, `Replication Features`, WebGL ClientServer, диапазоны Delay 0…0,5 с и Loss 0…1.
- [pricing](https://www.photonengine.com/en-US/Fusion/pricing) — планы и цены, 3 ГБ на CCU, овередж $0,05 / $0,10, Dedicated Servers только в Enterprise, burst и 48 часов.
- [faq](https://doc.photonengine.com/fusion/v2/troubleshooting/faq) — учёт трафика в обе стороны, правила burst и овереджа.
- [optimizations](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/optimizations) — MTU ~1280 Б, Interest Management, запрет строк, `targetFrameRate`, формула heap.
- [interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management) — режимы Object Interest, требования AoI, `ReplicateTo`, `IInterestEnter`/`IInterestExit`.
- [send-priority](https://doc.photonengine.com/fusion/v2/manual/advanced/send-priority) — `SetPriority`/`ClearPriority` с 2.1, `ObjectInactivityThreshold` только с **2.1.4**, дефолт 0,251 с, формула, trace-канал.
- [eventual-consistency](https://doc.photonengine.com/fusion/v2/manual/data-transfer/eventual-consistency) — потеря атомарности объекта.
- [data-streaming](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming) — `SendReliableDataTo*`, `ReliableKey`, коллбэки прогресса.
- [fusion-statistics-2.1](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/fusion-statistics-2.1) — только debug DLL, состав страниц, `TryGetFusionStatistics`, `FusionStatType`.
- [simulating-network-conditions](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/simulating-network-conditions) — поля, «половина значения» на peer, удвоение для Shared, Clumsy для release.
- [multipeer](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/multipeer) — `Peer Mode = Multiple`, своя физическая сцена, запрет статиков, `RunnerEnableVisibility`.
- [disconnect-info](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/disconnect-info) — три перечисления, 128 Б токена, `GetPlayerDisconnectInfo` только на Server/Host, 2.0.14+/2.1.4+, неперезатираемость причины.
- [lost-connection-handling](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/lost-connection-handling) — 2.0.5, пять попыток, Shared не поддержан, три режима, Invisible+Closed, **только directly connected**, `[bpk-…]`, 200 символов, 10 с.
- [connection-process](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/connection-process) — 500 мс, лимит 10 попыток, релей после 2/3.
- [ports](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/tcp-and-udp-port-numbers) — номера портов UDP / TCP / WS / WSS.
- [matchmaking](https://doc.photonengine.com/fusion/current/manual/matchmaking) — лимиты `SessionProperties`, `UpdateCustomProperties`, отказ от браузеров сессий, `MatchmakingMode`, один регион.
- [regions](https://doc.photonengine.com/fusion/v2/connection-and-authentication/regions) — коды регионов, Region Allowlist, предупреждение про Peak CCU, Best Region, `GetAvailableRegions`, `FixedRegion`, `ns.photonengine.cn`.
- [network-topologies](https://doc.photonengine.com/fusion/v2/manual/network-topologies) — «approximately 1 in 10 players» через релей.
- [fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose) — Shared для мобилы и WebGL, Client Host на 2–4, Dedicated только high-ARPU, релей для WebGL.
- [known-issues](https://doc.photonengine.com/fusion/v2/troubleshooting/known-issues) — пауза main loop и 10 с, iOS в фоне, `runInBackground`, WebGL в фоне, App Store и TCP/WSS, iOS Local Network popup, UWP.
- [fusion-webgl](https://doc.photonengine.com/fusion/v2/technical-samples/fusion-webgl) — Shared Mode, Unity 6000.0.78f1, `Run In Background`, CrazyGames, фикс `TaskManager.Delay` в 2.1.3.
- [analyzing-disconnects](https://doc.photonengine.com/fusion/v2/troubleshooting/analyzing-disconnects) — фильтры Clumsy и Wireshark, `PlayerTTL ≥ 12000`, 50 мс → +100 мс RTT, `ResentReliableCommands`, `Network Logging = ALL`.
- [subscription-error-cases](https://doc.photonengine.com/fusion/v2/troubleshooting/subscription-error-cases) — Archived AppId, CCU Limit Reached, Plugin Mismatch.
- [counter-analytics](https://doc.photonengine.com/fusion/v2/reference/counter-analytics) — счётчики дашборда, 24 ч для free, Counter API только Premium/Circle/Enterprise.
- [dashboard-config](https://doc.photonengine.com/fusion/v2/manual/advanced/dashboard-config) — Fusion-переменные с дефолтами, делимость send interval, отсылка к webhooks.
- [encryption](https://doc.photonengine.com/fusion/v2/manual/advanced/encryption) — плагин по запросу, настройки `PhotonAppSettings`, покрытие Shared, AES-256 CBC + HMACSHA256.
- [reliability-exceptions](https://doc.photonengine.com/fusion/v2/concepts-and-patterns/reliability-exceptions) — потеря промежуточных значений, Shared Prediction и `ResetToLatestState`, три случая потери RPC.
- [dedicated-server](https://doc.photonengine.com/fusion/v2/technical-samples/dedicated-server/fusion-dedicated-server) — аргументы и дефолты, `-port 27015`, `-batchmode -nographics`, `FUSION_WEAVER`.
- [br200 performance](https://doc.photonengine.com/fusion/v2/game-samples/br200/performance-optimizations) — таблица железа, **удаление Unity GSH в 2.1.0**, `-recordSession` и `-dataPath`, пулы.
- [br200 testing](https://doc.photonengine.com/fusion/v2/game-samples/br200/testing) — `-public` в App Version, `-moderator`, «security by obscurity», `ExtraPeers`.
- [casual-mmo testing](https://doc.photonengine.com/fusion/v2/game-samples/casual-mmo/debugging-testing) — `PlayerAI`, bat-файлы, 2 процесса × 5 ботов, `Simulate Batch Mode`.

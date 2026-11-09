# Глава 28. Безопасность и борьба с читами

> **TL;DR для агента**
> - Безопасность в Fusion выбирается **топологией**, а не кодом. По документации Photon: Dedicated Server — «Cheat-proof»; Client Host — «The host client, can cheat and modify any value and has access to full information»; Shared Authority — «cheating can occur without extra precautions being taken» ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose), [photon-products](https://doc.photonengine.com/photon/v1/photon-products)).
> - Курс целиком написан в Host Mode. Это значит: архитектура курса защищает от всех игроков, **кроме хоста**. Для ранкеда, экономики и турниров Host Mode непригоден.
> - Любое поле `INetworkInput` и любой аргумент RPC — это **запрос, а не факт**. Fusion валидирует право вызова RPC, но никогда не валидирует значения. Официальная формулировка: «we can't trust data from clients» ([network-input](https://doc.photonengine.com/en-us/fusion/current/manual/network-input)).
> - `RpcSources.All` **полностью снимает** встроенную проверку авторитета: документация требует «a manual authority check is needed to replace the InputAuthority check» ([reliability-exceptions](https://doc.photonengine.com/fusion/current/concepts-and-patterns/reliability-exceptions)).
> - Rate limiting в Fusion из коробки нет. Внешний анти-чит (EAC) закрывает ровно то, что НЕ закрывает серверный авторитет (aimbot, ESP, инжект) — и наоборот. Порядок внедрения: архитектура → валидация → аутентификация → Interest Management → IL2CPP → EAC → телеметрия.

Актуальные версии на 2026-11-01: **Fusion 2.1.3 Stable, Build 2390 (17 сентября 2026)**, ветка 2.0 — **2.0.13 Stable, Build 2379 (16 сентября 2026)**. Документация предписывает: «Projects should be upgraded to the **latest `2.0.x` SDK** before being upgraded to `2.1.x`» ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)). Курс построен на 2.0.5 — то есть всё, что описано ниже как «2.1», в курсе отсутствует физически.

---

## Зачем это знать

Потому что единственное решение, которое реально что-то гарантирует, принимается в первой строке кода — в `GameMode` — и почти не откатывается потом. Всё остальное (валидация ввода, Custom Authentication, Interest Management, Plugin SDK, EAC) — это слои **поверх** этого выбора, а не его замена.

Photon формулирует защиту как свойство топологии, а не как набор инструментов. Для Server Mode: «Cheat protection is strong because the dedicated server has state authority over the game», speed hacks и правка переменных через память заблокированы при использовании `NetworkInput` и `NetworkedProperties`, а «The only possible cheats in a well-designed Server Mode game are aimbots»; wall-hacks там же помечены как ограничиваемые через object interest ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)).

Для Client Host ровно та же страница говорит: «Because the host player also acts as the server, the same cheat protection applies for clients except the host» и «The host client, can cheat and modify any value and has access to full information». Сводная таблица продуктов Photon перечисляет минусы Client Host как «not suited for mobile due to unstable connections; unstable game quality due to host and relayed connections; cheating» ([photon-products](https://doc.photonengine.com/photon/v1/photon-products)).

Для Shared Authority: «Because players have State Authority over objects they control, cheating can occur without extra precautions being taken. This includes modifying any `NetworkedProperty` to any value or sending `RPCs` at any time and more» ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)); минусы в таблице — «not suited for highly competitive/eSports games; cheating» ([photon-products](https://doc.photonengine.com/photon/v1/photon-products)).

Практический вывод: правило курса «клиент никогда не пишет сетевое состояние» защищает от девяти игроков из десяти и не защищает от десятого — от того, кто запустил хост.

---

## 28.1. Что именно может подделать клиент: разбор по каналам

| Канал | Что Fusion проверяет | Что клиент может подделать | Что делать на авторитете |
|---|---|---|---|
| `INetworkInput` | только сериализацию | любое поле: вектор длиной 50, все кнопки сразу, мгновенный разворот прицела (= aimbot), несовместимые флаги | нормализовать, клампить, отбрасывать несовместимые комбинации, считать кулдауны и дистанции |
| Аргументы RPC | только **право вызова** (`RpcSources`/`RpcTargets`) | любое значение любого аргумента | клампить каждый аргумент, проверять `info.Source` и право на действие |
| `[Networked]` в Host/Server | запись только у StateAuthority | ничего (у не-хоста) | — |
| `[Networked]` в Shared | ничего: клиент — StateAuthority над своими объектами | своё состояние (`Health`, `Score`, инвентарь) — легитимно синхронизируется всем | выносить ценное состояние в свой бэкенд или в Plugin SDK |
| Транспорт | AES-256 клиент↔сервер | ничего в канале; но хост видит весь трафик открытым у себя в процессе | не рассчитывать на шифрование как на анти-чит |

**Ввод.** Документация прямо просит санировать: комментарий в официальном коде `PlayerMovement` — «to effectively sanitize inputs on the StateAuthority, we can't trust data from clients», и рядом «Its important that we apply normalization here» с `var direction = input.MoveDirection.normalized;` ([network-input](https://doc.photonengine.com/en-us/fusion/current/manual/network-input)). `GetInput` возвращает `true` «only for the InputAuthority and StateAuthority», прокси получают `false`; StateAuthority тоже может получить `false`, если ввод опоздал (потери пакетов, скачок задержки) — документация советует «cache the last good input and re-use it in this case» (там же). Нормализация — минимум, не достаточное условие: aimbot подаёт абсолютно валидный, нормализованный вектор.

**RPC.** `RpcSources` и `RpcTargets` — это фильтры; страница описывает один набор из четырёх значений, применимый к обоим: `All`, `Proxies`, `InputAuthority`, `StateAuthority`. `RpcInfo` даёт `Tick`, `Source`, `Channel`, `IsInvokeLocal`. `RpcChannel.Reliable` — дефолт, `Unreliable` может потеряться, `ReliableLargeData` помечен «Fusion 2.1 Only» и описан как «Use to send RPCs that exceed the 512 byte limit of the other channels» ([rpcs](https://doc.photonengine.com/fusion/current/manual/data-transfer/rpcs)). Важно по версиям: лимит 512 байт существовал и раньше — в 2.1 добавлен способ его обойти, а не сам лимит («Fusion 2.1 no longer has a limit of 512 bytes for RPC size, use `RpcChannel.ReliableLargeData`», [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/preview-2-1/whats-new-2-1)). Большие RPC «are not tick accurate» и порядок не сохраняется.

Главная ловушка безопасности здесь: **`RpcSources.All` снимает встроенную проверку авторитета полностью.** Документация по reliability exceptions говорит это дважды — в Shared Mode «a manual authority check is needed to replace the StateAuthority check», в Host/Server Mode «a manual authority check is needed to replace the InputAuthority check» ([reliability-exceptions](https://doc.photonengine.com/fusion/current/concepts-and-patterns/reliability-exceptions)). То есть переход на `All` ради надёжности доставки (reliable RPC не гарантирован, если в момент отправки меняется авторитет объекта) — это **обмен надёжности на безопасность**, и компенсировать его надо руками.

**Shared Mode Prediction.** Запись `[Networked]`-свойства без StateAuthority в Shared Mode даёт локальное предсказание: изменение не синхронизируется и, в отличие от Host/Server Mode, не сбрасывается автоматически. Вывод документации: «It is possible to end up with a permanent desynchronization of Networked Property values between peers». Лекарство задокументировано: «In Fusion 2.1 and newer» доступен `NetworkObject.ResetToLatestState()`, который «can be called to reset all Shared Mode predicted Networked Properties» объекта локально ([reliability-exceptions](https://doc.photonengine.com/fusion/current/concepts-and-patterns/reliability-exceptions)). Это делает «permanent desync» устранимым — но только если ты знаешь про метод и вызываешь его.

**Транспорт.** Photon шифрует Payload Encryption 256-битным AES для UDP и TCP; ключ согласуется по Diffie-Hellman при коннекте к Name Server, либо при `AuthMode.AuthOnceWss` «securely exchanged on a TLS connection». «A lean header per message (length, etc.) remains unencrypted». На WSS «all communication gets encrypted, including headers», и per-operation настройка игнорируется; WSS — дефолт для WebGL-экспорта Unity независимо от `PhotonServerSettings`. И жёстко: «Encryption is always between a client and the server and Photon does not offer end-to-end encryption between clients» ([encryption](https://doc.photonengine.com/realtime/current/reference/encryption)). Значит MITM между игроками — не твоя проблема, а вот хост в Host Mode видит всё. Отдельную операцию можно зашифровать через `PhotonPeer.SendOperation` с `sendOptions.Encrypt = true`. ⚠️ Утверждение «шифрование стоит производительности, применяйте избирательно» первоисточником **не подтверждено** — на странице encryption такой рекомендации нет.

---

## 28.2. Валидация на авторитете: эталон из Casual MMO Server Plugin

Готового фреймворка валидации Fusion не даёт, но даёт один официально разобранный образец. Паттерны переносятся в Host Mode напрямую, даже без плагина ([casual-mmo/server-plugin](https://doc.photonengine.com/fusion/v2/game-samples/casual-mmo/server-plugin)).

```csharp
// Код из документации Photon (Casual MMO Server Plugin), не из учебного проекта.
private const int MAX_DAMAGE = 50; // clamp cumulative damage from a single source per second
```

```csharp
// Код из документации Photon: детект speed hack через tolerance-счётчик.
if (speed > MaxSpeed)
  ++_fasterTicks;
else
  _fasterTicks = default;

if (_fasterTicks > 8)
  Log.Warn($"Speed of {Object.Id} ({speed:F1}m/s) is over threshold ({MaxSpeed:F1}m/s) for {_fasterTicks} ticks in a row!");
```

Страница прямо подчёркивает, что валидатор «intentionally does not clamp speed - sustained violations are logged» — скорость **детектируется, но не корректируется**, чтобы не сигналить читеру. Сравни с уроном, который именно клампится. Это и есть осознанный выбор между двумя стратегиями.

```csharp
// Код из документации Photon: перехват клиентского RPC плагином.
[Rpc(RpcSources.All, RpcTargets.StateAuthority, InvokeLocalMode = RpcInvokeLocalMode.ForwardToPlugin)]
// ... внутри плагина:
info.Cancel();   // «cancels the incoming RPC so it is not forwarded to clients as-is»

// Состояние, писать которое может ТОЛЬКО плагин — даже StateAuthority идёт через RPC:
[Networked(PluginAuthority = true, AllowPrediction = false)]

// Отказ клиенту в деспавне серверного объекта:
public override bool CanPlayerDestroyObject(NetworkRunner runner, PlayerRef player, NetworkObjectMeta meta)

// Один и тот же код работает с плагином и без него:
if (Runner.HasCustomPlugin == true)
```

Примечание по версиям: `InvokeLocalMode` / `RpcInvokeLocalMode` **не документированы** на странице RPC — там описан только булев параметр `InvokeLocal` (default true). Имя `ForwardToPlugin` подтверждается только страницей Casual MMO.

Политика, прописанная Photon там же: **«Don't chase 100% cheat-proof gameplay»** и «Flag cheaters on repeated patterns and segregate them into their own matchmaking pool rather than punishing single events». Старая страница PUN про hacking protection выстраивает четыре уровня по возрастанию стоимости (чисто клиентский; клиентский плюс репорты и баны через бэкенд; серверный анализ идущей игры; полный серверный авторитет) и честно говорит, что даже при полной серверной логике «it's not a guarantee cheating is impossible»; рекомендует банить и тех, кто слишком часто репортит, и требовать аутентификацию. ⚠️ Оговорка: это страница из ветки PUN, а PUN 2 «is in maintenance / LTS mode» — по сути актуально, но ссылаться на неё надо с этой пометкой ([hackingprotection](https://doc.photonengine.com/pun/current/gameplay/hackingprotection)).

---

## 28.3. Fusion 2.1 Plugin SDK — третий путь между Shared и Dedicated

Custom Plugin для Shared Mode «can read every `Networked` property, intercept `RPC`s, hold authority over selected pieces of state», спавнить серверные объекты и звать внешние сервисы. Заявленные use-cases прямо анти-читовые: «Running a subset of game logic server-side so outcomes can't be tampered with by clients», детект читеров без их уведомления, «Validating client authoritative state against achievable bounds» ([plugin-sdk-overview-2.1](https://doc.photonengine.com/fusion/v2/addons/plugin-sdk/plugin-sdk-overview-2.1)).

Ограничения, которые надо знать до проектирования:

| Ограничение | Формулировка/источник |
|---|---|
| Только 2.1.x | «This Addon is for Fusion 2.1.x. It does not support Fusion 2.0.x» ([plugin-sdk](https://doc.photonengine.com/fusion/v2/addons/plugin-sdk/plugin-sdk-overview-2.1)) |
| Доступность | «Available in the Gaming Circle and Industries Circle»; SDK получают по email-запросу (там же) |
| Стоимость | «Creating a Photon Enterprise Cloud involves additional costs» (там же). Цена Enterprise Cloud не публикуется — «Contact order@photonengine.com» ([pricing](https://www.photonengine.com/en/fusion/pricing)) |
| Разработка | «Starting a local Photon Server is only supported on Windows» ([plugin-sdk](https://doc.photonengine.com/fusion/v2/addons/plugin-sdk/plugin-sdk-overview-2.1)) |
| Снапшот | «the plugin works against a snapshot taken at export time» — после изменения кода или объектов нужен повторный Export ([casual-mmo/server-plugin](https://doc.photonengine.com/fusion/v2/game-samples/casual-mmo/server-plugin)) |
| Многопоточность | «The plugin runs in a multi-threaded environment - avoid mutable `static` state» (там же) |
| Топология | Overview указывает только Shared Authority. ⚠️ Поддержка Host/Server **явно не отрицается** — документация об этом молчит |

Альтернатива при недоступном Enterprise — `GameMode.Server`: headless Unity-билд, хостинг свой («Photon does not provide servers for hosting a dedicated Fusion server application», [network-topologies](https://doc.photonengine.com/fusion/v2/manual/network-topologies)), минусы в таблице Photon — «Complex to operate with high costs for external orchestration service which is problematic for mobile f2p and web» ([photon-products](https://doc.photonengine.com/photon/v1/photon-products)).

И третий вариант, который нельзя пропускать в разговоре про ранкед: **Photon Quantum**. В сводной таблице его плюсы — «frame-perfect esports», «built-in bot SDK and cheat-proof; plugins for anti-cheat and custom server-side code (Enterprise Cloud)», минус — «each client has "perfect information"» (то есть wallhack/ESP остаются) ([photon-products](https://doc.photonengine.com/photon/v1/photon-products)).

---

## 28.4. Идентичность: AppId, аутентификация, бан-листы

**AppId не секрет.** Официально: «While an App ID typically can not be kept entirely secret, you should not post it publicly» ([obtain-your-app-id](https://doc.photonengine.com/realtime/v5/getting-started/obtain-your-app-id)). Прятать его в обфусцированной строке — не защита.

**Без аутентификации** документация перечисляет: «no security against impersonation, anyone can claim any userId», «any inventory or stats are provided client side and easy to manipulate», «malicious users can not be banned», «users can not identify others or find friends», «everyone has unconditional access to the online component of your app». Вывод Photon: «we strongly recommend to add authentication to all apps before a general release». По умолчанию клиент идёт как `CustomAuthenticationType.None`, сервер выдаёт новый GUID как `userId` на время сессии, а присланный клиентом `userId` «is not checked in any way» ([overview-auth](https://doc.photonengine.com/realtime/v5/connection-and-authentication/authentication/overview-auth)).

**Custom Authentication** — единственный способ получить доверенную идентичность. Ключевые факты ([custom-authentication](https://doc.photonengine.com/realtime/current/connection-and-authentication/authentication/custom-authentication)):

| Элемент | Значение / поведение |
|---|---|
| `ResultCode` | `0` — не завершено (OAuth 2.0, двухфакторка), `1` — успех, `2` — неверные credentials, `3` — некорректные параметры; свои коды разрешены |
| Что возвращается | `UserId`, `Nickname`, `AuthCookie` — **только** при `ResultCode 1`; `Data` — при `0` или `1`; `Message` рекомендован особенно при ошибке |
| HTTP-метод | GET по умолчанию; POST включает непустая строка, `byte[]` (в т.ч. пустой) или `Dictionary<string, object>` (уходит JSON, `application/json`) |
| `Data` | «Keep in mind that nested arrays or objects are not supported» |
| Коллизия ключей | «In case, the same key is used in both places, only the dashboard value will be sent» → секреты и версии API задавай в Dashboard |
| `UserId` от сервиса | «will override the value of `UserId` in client and cannot be changed after that» |
| `AuthCookie` | внутри зашифрованного токена, клиент прочитать не может, передаётся в Webhook/WebRPC |
| Пароли | «For security reasons it is not recommended to send plain text passwords» — только хэш/ключ/токен |
| Анонимы | галка «Allow anonymous clients to connect»; «The option is shown only when you add at least one authentication provider», без провайдера настройка скрыта и игнорируется |
| Сервис недоступен | клиентов пускать или отвергать — настройка провайдера; после HTTP-ошибки Photon приостанавливает вызовы |
| Версионный гейт | документированный кейс: `AddAuthParameter("version", version)` → сервис отвечает `{ "ResultCode": 5, "Message": "Version not allowed." }` |

**Версионный гейт — недооценённый анти-чит-инструмент.** Он отсекает старые сборки с известными дырками на входе в облако, до любой игровой логики.

**Photon Token** живёт 1 час по умолчанию, читается только серверами Photon, рефрешится при переходе Master→Game Server и пока клиент поднимает события; клиент, просидевший в комнате больше часа без событий, остаётся подключённым к Game Server, но при реконнекте к Master должен переаутентифицироваться ([encryption](https://doc.photonengine.com/realtime/current/reference/encryption)).

**WebRPC — не для новых проектов.** Документация: «We are phasing out "Persistent Games" support», «New titles will not be able to rely on the `IsPersistent` setting, WebFlags and related features», «At this point, using WebRPCs is discouraged» — клиенты могут звать бэкенд напрямую по HTTPS. И важная поправка: `AuthCookie` **не** попадает в WebRPC автоматически — нужен webflag `SendAuthCookie = 0x02` при вызове операции; `UserId` же Photon подставляет во все WebRPC-запросы сам ([webrpc](https://doc.photonengine.com/server/current/applications/loadbalancing/webrpc)).

---

## 28.5. Rate limiting и утечка данных

**Встроенного per-RPC rate limiting в Fusion нет.** Есть две цифры со стороны Photon Cloud ([server FAQ](https://doc.photonengine.com/server/v5/reference/faq)):

- «we recommend keeping your msg/s per room count below 500» — и сразу же «Photon does not enforce this limit, but relies on a fair use policy». **Важная тонкость, которую легко перепутать:** сервер считает суммарные входящие и исходящие сообщения в секунду и делит на число комнат на том же Master Server — то есть это усреднённая по серверу метрика, а не счётчик твоей комнаты. «Any operation request or operation response or event is considered a message», «Cached events are also counted as messages»; broadcast `RaiseEvent` всем = 1 ответ + n событий.
- «Photon Cloud has a server side limit for client buffers which is 500KB»; «If a client hits this limit in a short period of time, it will be disconnected by the server». Это единственный лимит с автоматическим наказанием.

⚠️ Паттерн «счётчик на `(PlayerRef, RpcId)` + `TickTimer`, молча отбрасывать лишнее, копить счётчик нарушений» — это **вывод**, а не рекомендация Photon: официального сэмпла rate limiting для RPC в Fusion нет. Для `RpcChannel.ReliableLargeData` собственный лимитер особенно обязателен — канал снимает ограничение 512 байт и потому удобен для флуда.

**Interest Management как анти-чит.** Fusion-документация прямо связывает его с wall-hacks: в Server Mode остаются aimbots и wall-hacks, причём последние «can be limited with object interest» ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)). Логика проста: нельзя нарисовать врага, данных о котором у клиента нет. Ограничения: AoI режет по расстоянию/зонам, а не по линии видимости, поэтому враг в соседней комнате внутри радиуса всё равно утекает; always-interested объекты видны всем по определению. ⚠️ Утверждение «аудио/VoIP и UI-слои часто обходят фильтр» — вывод, первоисточника нет.

Рядом стоит **Object Send Priority** (Fusion 2.1): `NetworkObject.SetPriority(PlayerRef player, int priority)` и `ClearPriority(PlayerRef player)`; «An object with send priority of `1` means the object is updated each send tick», `ClearPriority` эквивалентен `SetPriority(player, 1)`. Критично для безопасности и для корректности: «priorities must be re-applied on the new Host after a migration occurs» — через колбэк `IAfterHostMigration` ([whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/preview-2-1/whats-new-2-1)).

**Lag Compensation** — это механизм доверия. Документация: «For Shared Mode each client has authority and decides what they hit. This is precise, but vulnerable to cheating»; если принимать заявленные клиентом попадания, «the system will be extremely vulnerable to simple game-destroying exploits»; задача механизма — дать WYSIWYG «despite not being able to trust them». И техническое ограничение: «Lag Compensation is only available in `Server Mode` and `Host Mode`» — значит **в Shared Mode честного хит-детекта нет в принципе**. Окно откатa задаётся настройкой `Hitbox Buffer Length In Ms` на `NetworkProjectConfig`; **ни значения по умолчанию, ни максимума документация не публикует** ([lag-compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation), [network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)). Соображение «большое окно даёт больше пространства читу с подделанной задержкой» — вывод, не факт из документации.

**Tick rate** (важно для версий): в Fusion 2.1 «can be set to any value between 8 and 256»; «Previously this was restricted to a limited set of options». В Shared Mode «tick & send rate settings cannot exceed `32`!», причём «In Fusion 2 both values were locked (to `32` & `16`, respectively)», а с 2.1 их можно менять внутри этого лимита. Client Send Rate — интервал 1–8, и доступны только значения, делящие tick rate без остатка. **Численного значения по умолчанию для Host/Server документация не публикует** ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)).

---

## 28.6. Внешний анти-чит и защита клиента

**EAC через EOS-плагин для Unity.** В Epic Developer Portal включаются EAC-модули под платформы и client protection; скачиваются integrity keys с предупреждением, что публикация приватного ключа — «making it public will allow players to bypass client protection». В Unity: «Enable EAC build steps by checking the `Use EAC` toggle», пути `Path to EAC private key` и `Path to EAC Certificate`, опциональный splash «a 800x450 PNG that will display in the EAC bootstrapper», опциональный свой `anticheat_integritytool.cfg`. Запуск — «the `EACBootstrapper.exe` file generated at the root of your build directory»; при сообщении «not installed» — `.\EasyAntiCheat_EOS_Setup.exe install prod-fn` из elevated-терминала в каталоге `EasyAntiCheat`. В коде — `AntiCheatManager.IsAntiCheatAvailable()` и колбэк `OnClientIntegrityViolated` («Called when the integrity of the client has been violated according to EAC») ([easy_anticheat_configuration.md](https://github.com/EOS-Contrib/eos_plugin_for_unity/blob/stable/com.playeveryware.eos/Documentation~/easy_anticheat_configuration.md)).

Что это честно означает: описанная конфигурация — **контроль целостности файлов и процесса клиента**, то есть защита от модифицированной сборки, инжекта и отладчика. Серверная часть (Anti-Cheat Server interface, регистрация клиентов, санкции) в документации Unity-плагина не раскрыта. И главное: EAC **не делает Host Mode доверенным** — хост запускает неизменённый билд и всё равно имеет StateAuthority.

⚠️ **Не подтверждено первоисточником:** утверждения «базовый anti-cheat сервис EOS бесплатный и self-service», а также поддержка protected-режима для P2P/listen-server. Официальная страница Epic (`dev.epicgames.com/docs/game-services/anti-cheat`) в этой сессии не открылась, а бюджет веб-поиска был исчерпан — проверьте перед проектированием. Константа `EOS_EAntiCheatClientMode_EOS_ACCM_PeerToPeer` («full mesh peer-to-peer mode») известна только из автогенерированных сторонних биндингов ([docs.rs](https://docs.rs/eos-sys/latest/eos_sys/constant.EOS_EAntiCheatClientMode_EOS_ACCM_PeerToPeer.html)), а не из документации Epic.

**BattlEye** — закрытая лицензируемая интеграция: по собственному сайту «Integration happens on the server- and client-side part of the game», после чего система «does its entire job independently without requiring any input from the developer's side»; получить — только через `license[at]battleye.com`, с бесплатной демонстрацией ([battleye.com/about](https://www.battleye.com/about/)). Unity на странице не упомянут, цены не публикуются. Для Photon Cloud (Host/Shared) серверной стороны, в которую BattlEye мог бы встроиться, попросту нет — он осмыслен только при Server Mode со своим хостингом.

**Обфускация.** Позиция Photon: «Unity builds are quite hackable unless you use IL2CPP», нужны дополнительные клиентские меры, и как пример инструмента назван Anti Cheat Toolkit с Asset Store ([hackingprotection](https://doc.photonengine.com/pun/current/gameplay/hackingprotection)). ⚠️ Утверждение «метаданные IL2CPP и имена типов в значительной мере восстановимы» встречается только в маркетинговых материалах вендоров обфускаторов — независимым первоисточником **не подтверждено**. Трактуй IL2CPP как налог на атакующего, а не как барьер.

---

## Чего по этой теме нет в учебном курсе

Курс — Fusion 2.0.5, Unity 2022.3.42f1, целиком Host Mode (`GameMode.AutoHostOrClient`). Отдельной главы про безопасность нет; тема затронута фрагментарно.

**Что в курсе уже есть — не дублируй, ссылайся:**
- `RpcHostMode.SourceIsServer` / `SourceIsHostPlayer` и ловушка `RpcInfo.Source == PlayerRef.None` разобраны подробно в [гл. 07](07-chapter.md) (включая формулировку «главная ловушка host mode» в [90-api-reference.md](90-api-reference.md)). Здесь только напоминание: если строишь проверки на `info.Source`, хост в них не попадёт без `SourceIsHostPlayer`.
- Lag Compensation, `HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority` и то, что `IgnoreInputAuthority` — «единственная защита от самопопадания», покрыты в [гл. 06](06-chapter.md) и [99-checklist](99-checklist.md).
- Ненадёжность `RpcTargets.All` при включённом Interest Management — [гл. 08](08-chapter.md).
- «Realtime по умолчанию читерабелен» и «не бери Realtime как транспорт геймплея для соревновательного шутера» — [гл. 15](15-chapter.md).
- `ConnectionToken` как идентификатор, живущий дольше соединения — [гл. 09](09-chapter.md). Но это внутренний механизм Fusion, **не** доверенная идентичность: клиент формирует `byte[]` сам.

**Чего в курсе нет содержательно:**
1. **Модели угроз и соответствия «жанр → топология».** Курс не говорит, что выбор `GameMode` — это в первую очередь выбор уровня защиты.
2. **Разбора «что подделывается по каждому каналу»** (таблица в 28.1).
3. **`RpcSources.All` без встроенной проверки авторитета** — курс использует RPC свободно, не отмечая этого.
4. **Валидации как дисциплины:** клампинг per-source, tolerance-счётчики, кулдауны, проверки дистанции. Нормализация вектора ввода в курсе — косметика; официально это обязательная мера.
5. **Rate limiting** и цифр Photon Cloud (<500 msg/s как fair-use, 500 КБ буфер с отключением).
6. **Аутентификации как контроля доступа:** `AuthenticationValues`, Custom Authentication, `AuthCookie`, `ResultCode`, бан-листы по UserId, версионный гейт, «Allow anonymous clients to connect».
7. **AppId** — в курсе просто настройка; не сказано, что он принципиально не секрет.
8. **Шифрования** — не упомянуто вовсе.
9. **Второго назначения Interest Management** ([гл. 08](08-chapter.md) — про трафик).
10. **Внешнего анти-чита** (EAC, BattlEye, IL2CPP) и понимания, какой класс читов он закрывает, а какой нет.
11. **Политики реакции:** телеметрия, флагинг, отдельный матчмейкинг-пул вместо бана.
12. **Shared Mode Prediction и permanent desync** плюс `NetworkObject.ResetToLatestState()`.
13. **Quantum** как альтернативы для ранкеда.

**Версионный пробел.** Всё самое значимое для безопасности появилось после курса: Plugin SDK для серверной логики в Shared Mode; `RpcChannel.ReliableLargeData`; tick rate 8–256 Hz вместо фиксированного набора; Object Send Priority и `IAfterHostMigration`; `CloudConnectionLostMode.MoveSessionToNewRoom` (дефолт в 2.1; при неудаче Quick Re-join сервер уведомляет напрямую подключённых клиентов о переходе в новую комнату в течение 10 секунд) и `StartGameArgs.PlayerUniqueId` (`long`; «The `PlayerUniqueId` is not used as the `PlayerRef`», но вернувшийся игрок с тем же ID получает тот же `PlayerRef`) — [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/preview-2-1/whats-new-2-1). И при апгрейде на 2.1 помни про Voice: для Fusion 2.1 это Voice Realtime5 2.64, несовместимый с Fusion 2.0 ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)).

---

## Решение: что брать и когда

| Жанр / контекст | Топология | Обязательные слои | Почему |
|---|---|---|---|
| Кооп 2–4, друзья, ПК | `Host` | валидация ввода, кламп аргументов RPC | Хост читит «сам себе» в компании знакомых. ⚠️ Для мобильного коопа Photon предупреждает: «Host Mode is not recommended for mobile except for special use-cases» ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)) |
| Казуальный мобильный | `Shared` («The recommended mode for mobile») | прогрессия/валюта/инвентарь — **в своём бэкенде** за Custom Authentication | В Shared клиент легитимно пишет своё `[Networked]` |
| WebGL | `Shared` («We strongly recommend Shared Mode on WebGL»; Server/Host «not recommended and by default disabled») | всё ценное на бэкенде; WSS даёт полное шифрование канала | JS-клиент — самая открытая среда |
| Соревновательный PvP, ранкед, шутер | `Server`, либо `Shared` + Plugin SDK (Enterprise), либо Quantum («cheat-proof») | серверная валидация, AoI против wallhack, EAC против aimbot/ESP, телеметрия | В Host Mode хост получает преимущество, которое нельзя ни обнаружить, ни оспорить |
| Персистентный мир / MMO | `Server` или Plugin SDK | весь прогресс в БД; клиентские RPC — только заявки | Casual MMO sample — эталон содержания проверок |
| Асинхронное / пошаговое | логика на своём HTTPS-бэкенде | Photon только транспорт | WebRPC для нового кода «discouraged» |

**Иерархия слоёв (внедрять строго в этом порядке):**

1. **Топология** — единственный уровень, который что-то гарантирует.
2. **Валидация и rate limiting на авторитете** — закрывает то, что топология пропустила.
3. **Аутентификация + бан-лист по UserId + версионный гейт** — делает наказание возможным.
4. **Interest Management + Object Send Priority** — лишают клиент данных для wallhack/ESP.
5. **IL2CPP + обфускация + шифрование локальных сейвов** — повышают цену взлома.
6. **Внешний анти-чит (EAC/BattlEye)** — целостность процесса; имеет смысл только когда 1–5 сделаны.
7. **Телеметрия, флагинг, отдельный пул для помеченных** — то, что Photon рекомендует вместо погони за 100%.

Обратный порядок («сначала обфускатор, с авторитетом разберёмся потом») — самая частая и самая дорогая ошибка.

---

## Чеклист

1. **Выпиши модель угроз до первой строки кода** и выбери `GameMode` из неё, а не из удобства хостинга. Решение почти не откатывается.
2. **Никогда не применяй Host Mode** к ранкеду, турнирам, внутриигровой экономике и персистентному прогрессу: «The host client, can cheat and modify any value and has access to full information».
3. **Трактуй каждое поле `INetworkInput` как запрос:** нормализуй векторы, клампи скаляры, отбрасывай несовместимые комбинации флагов, проверяй кулдауны и дистанции на StateAuthority.
4. **Кэшируй последний валидный input** на StateAuthority — `GetInput` может вернуть `false` при опоздавшем вводе, и без кэша это открывает дыру в логике.
5. **Клампи каждый аргумент каждого RPC.** `RpcSources`/`RpcTargets` проверяют право вызова, а не значения.
6. **При `RpcSources.All` всегда пиши ручную проверку авторитета** по `info.Source` — встроенной там нет.
7. **Для `RpcChannel.ReliableLargeData` пиши собственный rate limiter.** Отбрасывай молча, копи счётчик нарушений.
8. **Никогда не храни прогрессию, валюту и инвентарь в `[Networked]` в Shared Mode** — выноси в свой бэкенд за Custom Authentication или под `[Networked(PluginAuthority = true)]`.
9. **В Shared Mode зови `NetworkObject.ResetToLatestState()`** (2.1+) там, где возможен permanent desync от локального предсказания.
10. **Включи Custom Authentication до релиза** и сними галку «Allow anonymous clients to connect». Без аутентификации «anyone can claim any userId».
11. **Задавай все секреты и версии API в Dashboard, а не на клиенте** — при коллизии ключей уходит значение из Dashboard.
12. **Добавь версионный гейт** через auth-параметр и кастомный `ResultCode`, чтобы отсекать сборки с известными дырками.
13. **Никогда не считай AppId секретом** и не строй на его сокрытии никакой защиты.
14. **Настрой `Hitbox Buffer Length In Ms` осознанно** и помни, что Lag Compensation есть только в Server и Host Mode.
15. **Переустанавливай Object Send Priority в `IAfterHostMigration`** — иначе после миграции хоста приоритеты и связанное с ними ограничение утечки данных теряются.
16. **Настраивай AoI с оглядкой на безопасность, а не только на байты**, и держи список always-interested объектов минимальным.
17. **Клампи то, что дорого (урон), и детектируй то, что шумно (скорость)** — не корректируй скорость, чтобы не уведомлять читера.
18. **Флагь повторяющиеся паттерны, а не единичные события,** и уводи помеченных в отдельный матчмейкинг-пул: «Don't chase 100% cheat-proof gameplay».
19. **Никогда не ставь внешний анти-чит раньше серверного авторитета.** EAC закрывает модифицированный клиент, не легитимную власть хоста.
20. **Не опирайся на WebRPC в новом коде** (`discouraged`), а если опираешься — не забудь webflag `SendAuthCookie = 0x02`, иначе `AuthCookie` до бэкенда не доедет.
21. **Апгрейдь версиями по порядку:** сначала последняя 2.0.x, потом 2.1.x; синхронно обновляй Voice (для 2.1 — Realtime5 2.64).

---

## Источники

- [doc.photonengine.com/fusion/v2/fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose) — цитаты о cheat protection по каждому режиму, aimbots/wall-hacks, платформенные рекомендации (mobile, WebGL), «Host Mode is not recommended for mobile».
- [doc.photonengine.com/photon/v1/photon-products](https://doc.photonengine.com/photon/v1/photon-products) — сводная таблица «топология → плюсы/минусы», «Cheat-proof» для Dedicated Server, «cheating» в минусах Client Host и Shared Authority, Quantum «cheat-proof», статус PUN 2 (maintenance/LTS) и Bolt (DISCONTINUED), плагины как «Key component to protect scaling games against hacking and fraud».
- [doc.photonengine.com/fusion/v2/manual/network-topologies](https://doc.photonengine.com/fusion/v2/manual/network-topologies) — «A rogue host can cheat», отсутствие хостинга для Dedicated Fusion-сервера у Photon.
- [doc.photonengine.com/en-us/fusion/current/manual/network-input](https://doc.photonengine.com/en-us/fusion/current/manual/network-input) — «we can't trust data from clients», нормализация, поведение `GetInput`, совет кэшировать последний валидный input.
- [doc.photonengine.com/fusion/current/manual/data-transfer/rpcs](https://doc.photonengine.com/fusion/current/manual/data-transfer/rpcs) — `RpcSources`/`RpcTargets` как один набор из четырёх значений, `RpcChannel` и лимит 512 байт, поля `RpcInfo`, `RpcHostMode`, отсутствие документации на `InvokeLocalMode`.
- [doc.photonengine.com/fusion/current/concepts-and-patterns/reliability-exceptions](https://doc.photonengine.com/fusion/current/concepts-and-patterns/reliability-exceptions) — `RpcSources.All` и ручная проверка авторитета, Shared Mode Prediction, permanent desync, `NetworkObject.ResetToLatestState()` (2.1+).
- [doc.photonengine.com/fusion/v2/addons/plugin-sdk/plugin-sdk-overview-2.1](https://doc.photonengine.com/fusion/v2/addons/plugin-sdk/plugin-sdk-overview-2.1) — возможности плагина, анти-читовые use-cases, требования 2.1.x / Gaming & Industries Circle / Enterprise Cloud / Windows-only локальный сервер, топология Shared Authority.
- [doc.photonengine.com/fusion/v2/game-samples/casual-mmo/server-plugin](https://doc.photonengine.com/fusion/v2/game-samples/casual-mmo/server-plugin) — `MAX_DAMAGE = 50`, speed-hack детект `_fasterTicks > 8` и отказ от клампа скорости, `CanPlayerDestroyObject`, `[Networked(PluginAuthority = true, AllowPrediction = false)]`, `ForwardToPlugin` + `info.Cancel()`, `Runner.HasCustomPlugin`, снапшот на Export, запрет mutable `static`, «Don't chase 100% cheat-proof gameplay» и флагинг с отдельным пулом.
- [doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation](https://doc.photonengine.com/fusion/v2/manual/advanced/lag-compensation) — доверие и exploits, доступность только в Server/Host Mode, `Hitbox Buffer Length In Ms` без документированных default и максимума, `HitOptions`.
- [doc.photonengine.com/fusion/current/manual/network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) — tick rate 8–256 в 2.1, прежний фиксированный набор, лимит 32 в Shared Mode и прежняя фиксация 32/16, правила send rate 1–8 с делимостью.
- [doc.photonengine.com/fusion/current/getting-started/preview-2-1/whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/preview-2-1/whats-new-2-1) — Object Send Priority и `IAfterHostMigration`, снятие лимита 512 байт, Custom Plugins только на Enterprise Cloud, `CloudConnectionLostMode.MoveSessionToNewRoom` и 10 секунд, `StartGameArgs.PlayerUniqueId`.
- [doc.photonengine.com/fusion/current/getting-started/sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — версии 2.1.3/2390/17.09.2026 и 2.0.13/2379/16.09.2026, путь апгрейда через последнюю 2.0.x, совместимость Voice.
- [www.photonengine.com/en/fusion/pricing](https://www.photonengine.com/en/fusion/pricing) — актуальные тарифы (Development 20 CCU и Free 100 CCU бесплатно, 500/1000/2000 CCU — $125/$250/$500 в месяц, Premium $0.50/CCU с минимумом $1 000/мес), Enterprise Cloud без публичной цены.
- [doc.photonengine.com/realtime/current/reference/encryption](https://doc.photonengine.com/realtime/current/reference/encryption) — AES-256, Diffie-Hellman / AuthOnceWss, незашифрованный заголовок, WSS и WebGL, отсутствие end-to-end шифрования, TTL токена 1 час, `sendOptions.Encrypt`.
- [doc.photonengine.com/realtime/v5/connection-and-authentication/authentication/overview-auth](https://doc.photonengine.com/realtime/v5/connection-and-authentication/authentication/overview-auth) — полный список рисков без аутентификации, дефолтный `CustomAuthenticationType.None`, непроверяемый клиентский `userId`.
- [doc.photonengine.com/realtime/current/connection-and-authentication/authentication/custom-authentication](https://doc.photonengine.com/realtime/current/connection-and-authentication/authentication/custom-authentication) — `ResultCode` и поля ответа по кодам, GET/POST, ограничение `Data`, приоритет Dashboard при коллизии ключей, перезапись `UserId`, `AuthCookie`, запрет plain-text паролей, условие появления галки анонимов, версионный гейт, поведение при недоступном сервисе.
- [doc.photonengine.com/server/v5/reference/faq](https://doc.photonengine.com/server/v5/reference/faq) — «below 500» msg/s как fair use, механика подсчёта (inbound+outbound, деление на число комнат, cached events, 1+n для broadcast), буфер 500 КБ с отключением.
- [doc.photonengine.com/server/current/applications/loadbalancing/webrpc](https://doc.photonengine.com/server/current/applications/loadbalancing/webrpc) — `SendAuthCookie = 0x02`, автоматическая подстановка `UserId`, сворачивание Persistent Games и «using WebRPCs is discouraged».
- [doc.photonengine.com/realtime/v5/getting-started/obtain-your-app-id](https://doc.photonengine.com/realtime/v5/getting-started/obtain-your-app-id) — App ID «typically can not be kept entirely secret».
- [doc.photonengine.com/pun/current/gameplay/hackingprotection](https://doc.photonengine.com/pun/current/gameplay/hackingprotection) — четыре уровня защиты, «it's not a guarantee cheating is impossible», «Unity builds are quite hackable unless you use IL2CPP», Anti Cheat Toolkit, репорты и баны. ⚠️ Страница ветки PUN; PUN 2 в maintenance/LTS mode.
- [github.com/EOS-Contrib/eos_plugin_for_unity — easy_anticheat_configuration.md](https://github.com/EOS-Contrib/eos_plugin_for_unity/blob/stable/com.playeveryware.eos/Documentation~/easy_anticheat_configuration.md) — настройка EAC в Unity: `Use EAC`, пути к ключу и сертификату, splash 800x450, `anticheat_integritytool.cfg`, `EACBootstrapper.exe`, `EasyAntiCheat_EOS_Setup.exe install prod-fn`, `IsAntiCheatAvailable()`, `OnClientIntegrityViolated`, предупреждение о приватном ключе.
- [www.battleye.com/about](https://www.battleye.com/about/) — интеграция «on the server- and client-side part of the game», автономная работа, лицензирование через email. Unity и цены не упомянуты.
- [docs.rs — EOS_EAntiCheatClientMode_EOS_ACCM_PeerToPeer](https://docs.rs/eos-sys/latest/eos_sys/constant.EOS_EAntiCheatClientMode_EOS_ACCM_PeerToPeer.html) — ⚠️ сторонние автогенерированные биндинги, не документация Epic. Поддержка P2P в protected-режиме EAC первоисточником не подтверждена.

**Не удалось проверить в этой сессии:** официальная страница Epic по EOS Anti-Cheat (`dev.epicgames.com/docs/game-services/anti-cheat`) не отдала содержимое, а бюджет веб-поиска был исчерпан — поэтому утверждения о бесплатности/self-service EAC и о его серверном интерфейсе остались непроверенными. Численное значение tick rate по умолчанию для Host/Server Mode официально не документировано. Значение по умолчанию и максимум `Hitbox Buffer Length In Ms` официально не документированы.

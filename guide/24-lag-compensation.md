# Глава 24. Лаг-компенсация и попадания на практике

> **TL;DR для агента**
> - Лаг-компенсация в Fusion существует **только** в Server Mode и Host Mode: *«Lag Compensation is only available in `Server Mode` and `Host Mode`»* ([manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation)). В Shared Mode её нет и она не предусмотрена — там каждый клиент сам детектит попадания локально ([network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)). Выбор топологии = выбор модели попаданий, и это решение принимается **до** первой строки кода оружия.
> - Глубину буфера считай по формуле из доки времени, а не на глаз: окно перемотки = **prediction клиента + его interpolation delay** ([time-synchronization](https://doc.photonengine.com/fusion/v2/manual/advanced/time-synchronization)). В поставке Fusion 2.0.5 буфер — `HitboxBufferLengthInMs: 200`, ёмкость — `HitboxDefaultCapacity: 512` (дамп `NetworkProjectConfig` учебного проекта, [гл. 06](06-chapter.md)); на странице `NetworkProjectConfig` дефолты **не опубликованы**, документирован только минимум `Hitbox Default Capacity` — 16.
> - Пуля — **не** `NetworkObject`. Все официальные сэмплы (BR200, Simple FPS, Tanknarok, Projectiles Essentials) держат попадания в сетевом кольцевом буфере, потому что *«NetworkObject spawn is not predicted in Fusion 2»* → на каждую пулю приходит spawn delay ([projectiles-essentials](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-essentials)).
> - «Убили за углом» — не баг, а задокументированный выбор Photon в пользу стреляющего, и обменивать его обратно нельзя: без перемотки *«no one will ever intentionally hit anything»* ([manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation)).
> - Твоя версия устарела, если проект на 2.0.x: три фикса лаг-компенсации (layer mask при `SubtickAccuracy`, box overlap при Y-повороте и при offset хитбокса) легли в nightly Build 1240 (08.08.2025) и Preview Build 1460 (12.11.2025) — то есть **до** любого выпущенного 2.1.x, а в 2.0.x их нет ([release-notes-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1)).

---

## Зачем это знать

Глава [06](06-chapter.md) части I уже научила механике: `Runner.LagCompensation.Raycast` с `player: Object.InputAuthority`, пара `HitboxRoot` + `Hitbox` на отдельном слое, кольцевой буфер трасс вместо RPC. Эта глава — о том, что начинается **после** работающего хитскана: как выбрать окно перемотки под реальную аудиторию, что делать с хитбоксами, привязанными к анимации, какие приёмы Photon применяет в продакшн-сэмплах на 200 игроков, и где официальной документации просто нет.

Главный тезис: лаг-компенсация — это не функция, а **контракт между четырьмя подсистемами**: топологией, временем (prediction/interpolation), анимацией и представлением попадания. Сломай любую из четырёх — и правильный вызов `Raycast` перестаёт помогать.

---

## 1. Граница применимости: две модели попаданий, а не одна

| | Server / Host Mode | Shared Mode |
|---|---|---|
| Лаг-компенсация | есть | **нет** ([manual](https://doc.photonengine.com/fusion/current/manual/lag-compensation)) |
| Кто решает попадание | сервер, перематывая историю хитбоксов в тайм-фрейм стрелка | клиент локально: *«each client performs their own hitbox detection locally»* ([network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)) |
| API попадания | `Runner.LagCompensation.*` | обычный `Runner.GetPhysicsScene().Raycast` |
| Передача урона | прямая запись `[Networked]` на State Authority | RPC к владельцу цели |
| Защита от читов | серверная | *«extremely vulnerable to simple game-destroying exploits»* ([manual](https://doc.photonengine.com/fusion/current/manual/lag-compensation)) |
| Цена инфраструктуры | нужен выделенный сервер или игрок-хост | нет серверной логики |

Этот A/B показан на одном и том же сэмпле. **Tanknarok в Host Mode**: хитскан-оружие *«fire[s] a lag-compensated raycast immediately using `Runner.LagCompensation.Raycast`»*, снарядные стволы каждый тик проверяют коллизии лаг-компенсированными лучами вдоль экстраполированного пути ([tanknarok host](https://doc.photonengine.com/fusion/v2/game-samples/tanknarok/project-architecture-host)). **Тот же Tanknarok в Shared Mode**: лаг-компенсация не упоминается на странице вообще, луч обычный, урон идёт через `RPC_RequestAreaDamage()` + локальный `ApplyAreaDamage()`, а прокси предсказывают здоровье локальной переменной `_predictedHealth`, чтобы вспышка и партиклы появились сразу ([tanknarok shared](https://doc.photonengine.com/fusion/v2/game-samples/tanknarok/project-architecture-shared)).

**Директива.** Если проект в Shared Mode — не пиши `Runner.LagCompensation.*` вообще. Пиши локальный райкаст + RPC + предсказание реакции на прокси, и закладывай в дизайн, что урон читается. Если нужен серверный авторитет — это Host или Server Mode, и дальше вся глава про них.

Экономическая сторона выбора не абстрактна. Публичное облако Photon: Development — бесплатно, 20 CCU, 60 GB трафика; Launch Free — бесплатно, 100 CCU, 0.3 TB (стыкуется до 200 CCU); платные — 500 CCU за $125/мес, 1000 CCU за $250/мес, 2000 CCU за $500/мес; Premium — $0.50 за CCU при минимуме $1000/мес; перерасход трафика $0.05–0.10 за GB по регионам ([pricing](https://www.photonengine.com/fusion/pricing)). Дедикейтед-серверы и серверная логика через плагины — только в Enterprise, по запросу, цена не публикуется. Host Mode (игрок-хост) — единственный способ получить лаг-компенсацию, не платя за сервер.

---

## 2. Окно перемотки: откуда 200 мс и когда их мало

Документация даёт ровно одну формулу, и она лежит не на странице лаг-компенсации, а на странице синхронизации времени: объём лага, который серверу надо компенсировать (*«how far back it needs to test against»*), равен сумме **prediction клиента** и его **interpolation delay** ([time-synchronization](https://doc.photonengine.com/fusion/v2/manual/advanced/time-synchronization)). Оба слагаемых Fusion подбирает адаптивно: prediction — по разбросу исходящих пакетов, interpolation delay — по разбросу входящих. Числовых дефолтов для них страница **не даёт**.

Настройки буфера в `NetworkProjectConfig` → Lag Compensation ([network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)):

| Настройка | Что значит | Дефолт |
|---|---|---|
| `Enabled` | добавлять ли `HitboxManager` при инициализации `NetworkRunner` (описан на [manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation)) | `false` в поставке 2.0.5 ([гл. 06](06-chapter.md), дамп конфига) |
| `Hitbox Buffer Length In Ms` | *«the length buffer in which hitbox data will be stored in milliseconds»* | на странице доки **не опубликован**; в поставке 2.0.5 — `200` ([гл. 06](06-chapter.md)) |
| `Hitbox Default Capacity` | число хитбокс-значений на снапшот; *«Has a minimum of 16»* | **не опубликован**; в поставке 2.0.5 — `512` ([гл. 06](06-chapter.md)) |
| `Cached Static Colliders Size` | размер кэша статических коллайдеров (PhysX/Box2D) для стандартных запросов | **не опубликован**; в поставке 2.0.5 — `64` ([гл. 06](06-chapter.md)) |

⚠️ Отдельно: минимум `16` у `Hitbox Default Capacity` — это **минимум, а не дефолт**. Реальное значение в поставке на порядок больше. Источником дефолтов служит API-референс класса `LagCompensationSettings`, но `doc-api.photonengine.com` на 01.11.2026 закрыт bot-защитой Gcore и недоступен — поэтому единственный проверяемый первоисточник здесь это дамп `NetworkProjectConfig.fusion` из поставки, приведённый в [гл. 06](06-chapter.md). Перед сдачей проекта **посмотри инспектор своей версии SDK**, а не эту таблицу.

**Как выбирать длину буфера.** Короткий буфер экономит память и тихо ломает попадания у игроков, чьё окно (prediction + interpolation delay) выходит за его границы: такой игрок будет стрелять «правильно» и промахиваться. Длинный буфер честен к лагающим клиентам, но ровно настолько же увеличивает окно «убийства за углом». Правило: длина ≥ окна **худшего поддерживаемого** клиента, а не среднего. Косвенная калибровка — `NetworkConditions` в конфиге (в учебном проекте заготовлены 150 мс задержки, 50 мс джиттера и 5% потерь, см. [гл. 06](06-chapter.md)): включи и смотри, попадают ли выстрелы.

**Что Photon прямо признаёт платой.** Цель может быть поражена, хотя уже считала себя за укрытием, *«however, this is much less likely to be noticeable»*, чем две альтернативы: серверная правда без перемотки, где *«no one will ever intentionally hit anything»*, и клиентский авторитет, уязвимый к тривиальным эксплойтам ([manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation)).

---

## 3. Хитбоксы в продакшене: активность, анимация, broad-phase

Правила сборки из манула ([manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation)) — те же в Fusion 1 и Fusion 2, проверено по [v1-странице](https://doc.photonengine.com/fusion/v1/manual/lag-compensation):

1. `HitboxRoot` — *«must first be attached to the topmost node of the GameObject»*. Он группирует все `Hitbox` объекта и его детей.
2. Bounding sphere broad-phase задаётся `BroadRadius` + `Offset` и *«must encompass all grouped hitboxes»*, включая объёмы, которых хитбоксы достигают **из-за анимации**. Это самая частая причина «луч прошёл сквозь вытянутую руку». Инструмент подбора — `HitboxRoot.SetMinBoundingRadius()` (XML-док Fusion, цитируется в [гл. 06](06-chapter.md)); на веб-странице манула он не описан.
3. **Лимит 31** `Hitbox`-нода на один `HitboxRoot`. Больше — дроби иерархию на несколько рутов.
4. Слой берётся с GameObject хитбокса, разные хитбоксы под одним рутом могут быть на разных слоях. Но слой **кэшируется при регистрации** — менять в рантайме только через `Hitbox.SetLayer(int layer)`.
5. Дисциплина слоёв: динамические Unity-`Collider` держать на слое, который лаг-компенсированный луч игнорирует; динамику бить **только** хитбоксами, статику — обычными PhysX-коллайдерами. Удалять физические коллайдеры с динамики нельзя — они нужны физике.

**Правило активности, из-за которого хитбоксы молча не работают.** Манул описывает его косвенно: коллбэк `preProcessRoots` может протолкнуть коллайдер в narrow-phase, *«bypass[ing] the layer mask and Hitbox Active-state checks»* — то есть **проверка active-state у хитбокса в запросе есть**. Прямой формулировки «и хитбокс, и его рут должны быть активны» на открытых страницах нет (она в API-референсе, который закрыт Gcore) — ⚠️ формально не подтверждено первоисточником. Практический вывод от этого не меняется: гаси хитбоксы мёртвых, неуязвимых, зрителей и рагдоллов через `HitboxActive` / `HitboxRootActive` / `SetHitboxActive`, а не фильтром на каждом выстреле (см. [гл. 06](06-chapter.md), п. 12 чеклиста).

**Форма хитбокса — данные расходятся.** Манул утверждает: *«Currently, a `Hitbox` can only be described as a 3D shape»*, sphere или box. Но дамп префаба учебного проекта на Fusion 2.0.5 показывает `Hitbox` с `Type: 3`, `CapsuleRadius: 0.5`, `CapsuleExtents: 2`, то есть **Capsule** (порядок в `HitboxTypes`: None, Box, Sphere, Capsule — [гл. 06](06-chapter.md)). Формулировка манула неполна; капсулы есть. Не строй проект на выводе «только sphere/box».

**Анимация и хитбоксы — отдельная подсистема, которую легко недооценить.** Сэмпл Fusion Animations прямо связывает одно с другим: если хитбоксы ведёт анимация, попадание, зарегистрированное на клиенте, должно регистрироваться и на сервере. Сэмпл рисует хитбоксы ног и рук скриптом `HitboxDraw` — серверные зелёным, прокси синим, с перерисовкой каждые 50 тиков (примерно раз в секунду) — и формулирует вывод: *«The higher the animation precision, the more closely the boxes align»* ([fusion-animations](https://doc.photonengine.com/fusion/current/technical-samples/fusion-animations)).

| Подход к анимации | Точность | Цена |
|---|---|---|
| Примеры 1–5 (`[Networked]`-параметры, интерполированные данные, периодическая синхронизация state+time, `NetworkMecanimAnimator`, FSM + Animancer) | **render accurate** | NMA: *«more bandwidth intensive»*, чем периодическая синхронизация; при State Root sync время состояния меняется каждый тик |
| Пример 6 — Playables API, Fusion Animation Controller | **tick accurate**, максимальная | *«in the experimental state and lacking web documentation»*, самый сложный в поддержке |

Все данные — [fusion-animations](https://doc.photonengine.com/fusion/current/technical-samples/fusion-animations). BR200 в своём списке фич заявляет именно *«Tick-accurate animation system»* ([br200/overview](https://doc.photonengine.com/fusion/v2/game-samples/br200/overview)). Противоположный полюс — Simple FPS: там хитбоксы вообще не следуют анимации и стоят в *«static pose that represents the most common player pose»* ([simple-fps/architecture](https://doc.photonengine.com/fusion/current/game-samples/simple-fps/architecture)). Это осознанный учебный компромисс: дёшево и предсказуемо, но стрельба в вытянутую руку или присевшего не работает.

---

## 4. Запрос: что в курсе не вызывалось

Методы, названные на странице манула: `Raycast`, `RaycastAll`, `OverlapSphere`, `ResolveQuery` (+ `SphereOverlapQuery.CreateQuery`). ⚠️ `OverlapBox` на странице **по имени не упомянут**, хотя box-overlap как возможность описан; в API 2.1.1 и в сводной таблице [гл. 06](06-chapter.md) метод есть. То же про query-перегрузки (`RaycastQuery`, `RaycastAllQuery`, `SphereOverlapQuery`, `BoxOverlapQuery`), `GetClosestHit(List<LagCompensatedHit>)`, `PositionRotation(Hitbox, PlayerRef, out Vector3, out Quaternion, bool)` и `GetPlayerTickAndAlpha(PlayerRef, …)` — всё это перечислено в XML-доке поставки ([гл. 06](06-chapter.md)), но не на веб-страницах.

Канонический вызов из манула (**код из документации, не из учебного проекта**):

```csharp
Runner.LagCompensation.RaycastAll(transform.position, transform.forward, rayLength,
    player: Object.InputAuthority, _hits, layerMask, clearHits: true);
```

`player` — смысловое ядро системы: он задаёт, **из чьей перспективы** резолвить запрос, и обычно равен `Object.InputAuthority` объекта, который стреляет.

| `HitOptions` | Что делает | Подтверждение |
|---|---|---|
| `IgnoreInputAuthority` | игнорирует хитбоксы объектов, которыми управляет стреляющий | [manual](https://doc.photonengine.com/fusion/current/manual/lag-compensation) |
| `SubtickAccuracy` | резолвит против интерполированного состояния с тем самым alpha, который игрок видел при опросе ввода (без флага — ближайший из тиков From/To) | [manual](https://doc.photonengine.com/fusion/current/manual/lag-compensation) |
| `IncludePhysX` | добавляет обычные PhysX-коллайдеры; они **не** лаг-компенсируются и читаются в текущем состоянии; всегда опрашивается 3D PhysX | [manual](https://doc.photonengine.com/fusion/current/manual/lag-compensation) |
| `IncludeBox2D` | то же для Box2D-статики | ⚠️ на странице манула **отсутствует**; есть только в XML-доке `Raycast` в поставке: *«If the `HitOptions.IncludePhysX` or `HitOptions.IncludeBox2D` flag is indicated, query will also include static colliders»* ([гл. 06](06-chapter.md)) |

⚠️ **Полный список значений `HitOptions` официально проверить не удалось**: единственный источник — API-референс, закрытый bot-защитой. Считай список выше неполным.

**Фильтрация кандидатов.** Делегат `PreProcessingDelegate` между broad- и narrow-phase. Манул Fusion 2 даёт сигнатуру `(ref Query query, List<HitboxRoot> candidates, HashSet<int> preProcessedColliders)` с удалением кандидатов в обратном порядке. ⚠️ **Данные расходятся:** XML-док поставки 2.0.5 описывает второй параметр как `HashSet<HitboxRoot>`, и учебный код вызывает `candidates.RemoveWhere(...)` — метод `HashSet`, а не `List` ([гл. 06](06-chapter.md)). Похоже на смену типа между версиями; пиши под тот тип, который видит компилятор, а не под пример из доки. Два правила общие для обеих версий: делегат **кэшируй** (иначе аллокация на каждый выстрел), а данные передавай через `Query.UserArgs`, собирая запрос через `CreateQuery` + `ResolveQuery`, вместо замыканий.

**Результат.** Манул показывает у `LagCompensatedHit` только `Hitbox` и `Point`; полного списка полей открытые страницы не дают. ⚠️ Поля `Normal`, `Distance`, `Collider`, `Collider2D`, `GameObject`, `HitboxColliderPosition`/`HitboxColliderRotation` — **не подтверждены первоисточником** (API-референс закрыт). Что подтверждено надёжно: PhysX/Box2D overlap-запросы *«do not provide detailed information about the computed collision»* — точку и нормаль для этой части результата считай сам; у хитбоксов Fusion информация есть ([manual](https://doc.photonengine.com/fusion/current/manual/lag-compensation)).

**2D.** `Hitbox` — только 3D-форма; 2D эмулируется фиксацией плоскости (например XY). `IncludePhysX` всегда идёт в 3D PhysX, поэтому для 2D-коллайдеров нужен отдельный `Runner.GetPhysicsScene2D().Raycast(...)` ([manual](https://doc.photonengine.com/fusion/current/manual/lag-compensation)).

**Профилирование — штатное, а не на глаз.** Fusion Statistics имеет отдельную страницу **Lag Compensation** с процентом используемых хитбоксов и временем внутренних обновлений. Важная оговорка: статистика *«will only collect statistics data when using the DEBUG DLL»* ([fusion-statistics](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/fusion-statistics)). С 2.1.2 добавлен трейс-канал `LagCompensation` ([release-notes-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1)). Нормативов «сколько хитбоксов выдержит сервер» документация не даёт — измеряй сам.

---

## 5. Как попадания сделаны в официальных сэмплах

| Сэмпл | Топология | Структура попадания | Ёмкость буфера | Хитбоксы |
|---|---|---|---|---|
| [BR200](https://doc.photonengine.com/fusion/v2/game-samples/br200/weapons) | Dedicated Server & Client Host, 200 игроков, 60 Hz | `ProjectileData { Destination, ImpactNormal, ImpactTagHash }` | `[Networked, Capacity(10)]` | `BodyPart : Hitbox` + множитель урона (голова >1, конечности <1; точных чисел страница не даёт) |
| [Simple FPS](https://doc.photonengine.com/fusion/current/game-samples/simple-fps/architecture) | Host | `ProjectileData { HitPosition, HitNormal, ShowHitEffect }`, `INetworkStruct` | circular buffer (размер не указан) | `BodyHitbox : Hitbox`; голова — двойной урон, ноги — пониженный; **статичная поза**, анимацию не следуют |
| [Tanknarok (Host)](https://doc.photonengine.com/fusion/v2/game-samples/tanknarok/project-architecture-host) | Host | `ShotState { StartTick, EndTick, Position, Direction }` | `NetworkArray<ShotState>` как circular buffer | — |
| [Projectiles Essentials](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-essentials) | Host | `ProjectileData` (подход D) + `_fireCount`/`_visibleFireCount` | `NetworkArray`, размер по сценарию | — |

Ни один не спавнит `NetworkObject` на выстрел. BR200: *«no network object is spawned per shot»*, вместо этого маленькая структура в кольцевом буфере, плюс опциональный визуальный `DummyProjectile`. Исключение в BR200 — **гранаты**: `ProjectileWeapon`/`ThrowableWeapon` спавнят самостоятельные объекты и каждый тик симуляции кастуют короткий луч между предыдущей и новой позицией ([br200/weapons](https://doc.photonengine.com/fusion/v2/game-samples/br200/weapons)).

**Четыре приёма BR200, которых нет ни в одном мануале** (все — [br200/weapons](https://doc.photonengine.com/fusion/v2/game-samples/br200/weapons)):

1. **Двухэтапный райкаст.** Каждый `FixedUpdateNetwork()` луч из камеры ищет target point. Когда обрабатывается ввод «огонь», второй луч идёт от фиксированной точки стрельбы персонажа к этому target point, и уже он определяет попадания.
2. **Точка стрельбы — не срез ствола, а плечо.** Причина: позиция ствола зависит от анимации, и *«firing experience was less stable during rapid movement like strafing»*.
3. **The Corner Issue.** Второй луч **игнорирует** попадания в окружение, если они не близки ни к точке выстрела, ни к target point. Это резко улучшает стрельбу вокруг углов и деревьев ценой небольшого допуска подстрелить игрока вплотную за углом. Логика живёт в `ProjectileUtility`, через который идут все расчёты снарядов.
4. **Относительная позиция хита.** `BodyHitData { Action, Damage, RelativePosition, Direction, Instigator }` в `[Networked, Capacity(4)]`. Позиция относительная, потому что прокси интерполируются между двумя последними полученными тиками — с абсолютной позицией кровь уедет с тела.

Плюс решение по честности представления: импакты и кровь показываются стреляющему **сразу**, а хит-маркер (красный крест) и цифры урона — только когда попадание пришло в новом серверном состоянии. Это добавляет задержку по пингу, зато *«ensures only valid hits are shown»*.

**Производительность BR200** — единственные публичные цифры по серверу на 200 игроков ([br200/performance-optimizations](https://doc.photonengine.com/fusion/v2/game-samples/br200/performance-optimizations)):

| Железо | Игроков при 60 Hz |
|---|---|
| Mac Mini M1 2020 (Mac server) | 200 |
| Core i9-10900K (Windows server) | 200 |
| UGS Bare Metal, Intel Xeon E-2288G @3.70GHz (Linux) | 200 |
| UGS Virtual Machine, GCP N2, Intel Xeon @2.80GHz (Linux) | 60–80 |

Две оговорки, без которых цифры врут: интеграция Unity Game Server Hosting **удалена из сэмпла в версии 2.1.0**, UGS-измерения сохранены лишь как референс сопоставимого железа; и *«Numbers above represent the worst case scenario»* — первая минута после спавна, когда все бегут, крутят камеру и стреляют. Разбивки «сколько из этого стоит лаг-компенсация» нет — для этого и нужна страница Lag Compensation в Fusion Statistics.

---

## 6. Снаряды: четыре подхода Photon и перенос на свой проект

Projectiles Essentials — единственное место, где Photon сам ранжирует варианты ([projectiles-essentials](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-essentials), версии сэмпла: 2.1.1 от 10.07.2026 и 2.0.4 от 20.12.2024):

| Подход | Плюсы (цитаты) | Минусы (цитаты) |
|---|---|---|
| **A.** `NetworkObject` + `NetworkTransform`/`NetworkRigidbody` | *«Interest management works out of the box»*, *«Can use proper physics (PhysX)»* | *«NetworkObject instantiation overhead»*, трафик на каждый апдейт трансформа, *«Spawn delay»* |
| **B.** `NetworkObject` + fire data | *«Updating position costs no bandwidth»* | overhead инстанса, *«Manual interest management handling»*, spawn delay |
| **C.** Projectile Count Property | *«Least amount of data transferred»*, *«No spawn delay»*, interest management наследуется от владельца | *«Suitable only for hitscan projectiles»*, *«Imprecise on proxies»* (прокси в интерполированной позе), точка попадания неизвестна |
| **D.** Projectile Data Buffer | *«Good bandwidth consumption»*, *«Covers most projectile use cases»*, *«No spawn delay»* | *«cannot outlive»* владельца, *«Projectile physics like bouncing is only manually calculated»* |

**Рекомендация Photon — D.** Именно его учит [гл. 06](06-chapter.md) части I (`FireCount` + `NetworkArray<HitScanState>` + догон в `Render()`), а [гл. 04](04-chapter.md) и [гл. 05](05-chapter.md) — снарядный вариант того же (`ProjectileWorld`, экстраполяция и интерполяция снапшотов).

Корень всех минусов `NetworkObject`-подходов один: *«Since NetworkObject spawn is not predicted in Fusion 2, all projectiles are spawned by the State Authority»*. Обход — пре-спавн. В Projectiles Advanced для этого есть `NetworkObjectBuffer`, который *«pre-spawns selected number of objects in advance»* именно потому, что spawn prediction в Fusion 2 больше не поддерживается ([transition-from-fusion1](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/transition-from-fusion1)).

**Критичная строка для прокси** (код из документации):

```csharp
float renderTime = Object.IsProxy ? Runner.RemoteRenderTime : Runner.LocalRenderTime;
```

Чужой снаряд, отрисованный в локальном тайм-фрейме, появится «из ниоткуда» в нескольких метрах от стрелявшего. Кинематический снаряд при этом каждый тик кастует короткий луч между предыдущей и новой позицией; в Example 2 — с `HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority` ([projectiles-essentials](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-essentials)).

**Projectiles Advanced** (2.1.1 от 06.08.2026, требует **Unity 6.3+** — [download](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/download)) доводит D до продакшн-вида. Ключевой контракт: *«Projectile is not a `NetworkBehaviour` and is not associated with a `NetworkObject`»* — значит все сетевые данные обязаны лежать в структуре данных снаряда. `GetFireData(Vector3 firePosition, Vector3 fireDirection)` и `OnFixedUpdate(ref KinematicData data)` вызываются **прямо на префабах** из `FixedUpdateNetwork` на State и Input authority; `Activate(ref KinematicData)`, `Deactivate()`, `Render(ref KinematicData data, ref KinematicData fromData, float alpha)` — на инстансах и чисто визуальны ([projectiles-advanced/projectiles](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/projectiles)). ⚠️ Имя метода — **`GetFireData`**; встречающееся на той же странице `GetFiredData` — опечатка документации, не альтернативное API.

Типы: `InstantHitscanProjectile`, `FlyingHitscanProjectile`, `SimpleKinematicProjectile`, `AdvancedKinematicProjectile`, `HomingKinematicProjectile`, `SprayProjectile`, луч через `WeaponBeam`, `StandaloneProjectile`. Расхождение «стреляем из камеры, рисуем из ствола» решено честно: *«Fusion Projectiles uses the more standard approach of firing from the camera center»*, а визуал интерполируется от ствола к реальной траектории (время и ease настраиваются). Для хитскана *«no interpolation is needed»* — точка импакта уже известна.

Архитектурно `ProjectileManager` из Fusion 1 **удалён**: *«Removed `ProjectileManager`. Instead, hitscan projectiles and kinematic projectiles are now managed separately»* — через генерик `NetworkDataBuffer` с реализациями `HitscanProjectileBuffer` и `KinematicProjectileBuffer`. Приятное следствие: *«Projectile visuals can persist longer than the data in network buffers»*, поэтому буферы можно делать меньше ([transition-from-fusion1](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/transition-from-fusion1)).

⚠️ **Устаревшая фраза в документации.** Страница Projectiles Essentials до сих пор утверждает: *«Projectiles Advanced is not yet available for Fusion 2 but the principles remain the same»*. Это неверно на 01.11.2026 — Advanced 2.1.1 выпущен 06.08.2026 и имеет собственную страницу перехода с Fusion 1. Не принимай эту фразу за факт.

---

## 7. Чего документация не говорит

Четыре стыка, где официального ответа нет, и это надо знать до того, как упрёшься:

1. **Лаг-компенсация × Interest Management.** Страница Interest Management не упоминает ни лаг-компенсацию, ни хитбоксы, и **не говорит**, попадают ли в broad-phase руты объектов, отсечённых Area of Interest ([interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management)). Что известно точно: игрок без интереса *«will not receive any updates»* по объекту, а колбэки `IInterestEnter`/`IInterestExit` предлагается использовать на сервере, чтобы гасить части объекта. Для проектов масштаба BR200 (в списке фич — *«Advanced interest management»*) это центральный вопрос, и он **официально не документирован**. Механика AoI — [гл. 08](08-chapter.md) части I.
2. **Античит вокруг самой перемотки.** Ограничение глубины отката, отбраковка подделанного тайминга ввода, лимит частоты выстрелов — ничего из этого документация Photon не описывает, и FAQ тоже молчит (проверено: [faq](https://doc.photonengine.com/fusion/v2/troubleshooting/faq) не упоминает ни лаг-компенсацию, ни читы). Серверная перемотка — стандартный вектор абьюза; закладывай собственные проверки.
3. **Physics Forecast × перемотка.** Forecast-объекты существуют (в Fusion Statistics есть отладочная страница **Forecast Objects** с Forecast Speed, Stall Heuristic и Collision — [fusion-statistics](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/fusion-statistics)), но стык «объект экстраполирован в локальное время» × «хитбоксы перематываются в прошлое» документация **не описывает**. Концептуально это конфликт; проверяй на своём проекте.
4. **Детерминизм разброса.** Лаг-компенсация бессмысленна, если сервер и клиент разыграли разный разброс: случайность в симуляции обязана быть чистой функцией тика. Приём из [гл. 06](06-chapter.md): `Random.InitState(Runner.Tick.Raw * unchecked((int) Object.Id.Raw))`. В официальных мануалах по лаг-компенсации эта связь не проговорена вообще.

---

## Чего по этой теме нет в учебном курсе

Курс — Host Mode, Fusion **2.0.5** build 920, Unity 2022.3.42f1. Тема лаг-компенсации в нём разобрана **полноценно**: [гл. 06](06-chapter.md) целиком про `Runner.LagCompensation.Raycast` с `player: Object.InputAuthority`, пару `HitboxRoot`+`Hitbox` на выделенном слое, `BroadRadius`/`SetMinBoundingRadius()`, `Hitbox.SetLayer`, флаги `SubtickAccuracy | IgnoreInputAuthority`, фильтрацию в `preProcessRoots` и Projectile Data Buffer (`FireCount` + `NetworkArray<HitScanState>`, проигрывание только в `Render()`). Снарядная альтернатива — [гл. 04](04-chapter.md) и [гл. 05](05-chapter.md). Дельта этой главы — не «тема отсутствует», а шесть конкретных пробелов:

1. **Контраст топологий.** Курс целиком в Host Mode и потому не показывает, что в Shared Mode лаг-компенсации нет вовсе и попадания делаются обычным райкастом + RPC с предсказанием здоровья на прокси. Это развилка, которая решается до кода.
2. **Количественный выбор буфера.** Курс фиксирует `HitboxBufferLengthInMs: 200` как данность. Формулы «окно = prediction + interpolation delay» и связи с interpolation delay прокси в нём нет.
3. **Анимация как часть системы попаданий.** В курсе хитбокс — один статичный `Hitbox` на корне. Темы «хитбоксы по костям», «tick-accurate vs render-accurate анимация», `HitboxDraw` зелёные/синие — нет.
4. **Продакшн-приёмы.** Двухэтапный райкаст, точка стрельбы у плеча, The Corner Issue, относительная позиция хита, разделение мгновенных эффектов и подтверждённых хит-маркеров — ничего из этого в курсе нет, и в мануалах тоже: только в BR200.
5. **Профилирование и пределы.** Страница Lag Compensation в Fusion Statistics, DEBUG DLL, трейс-канал `LagCompensation` (с 2.1.2), публичные бенчмарки BR200 — за рамками курса.
6. **Версионный сдвиг.** Курс на 2.0.5 (12.02.2025); актуален **2.1.3 Stable Build 2390 от 17.09.2026**, параллельно поддерживается 2.0.13 Stable Build 2379 от 16.09.2026 ([sdk-download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download)). Разбор дельты — в таблице ниже.

| Изменение | Версия и дата | Последствие для курсового проекта |
|---|---|---|
| Три фикса лаг-компенсации: layer mask игнорируется при `SubtickAccuracy`; box overlap ломается при Y-повороте хитбокса и при его `Offset` | nightly Build 1240, **08.08.2025** | это **до** любого выпущенного 2.1.x → баги актуальны именно для 2.0.x, включая курсовой 2.0.5 |
| Фикс порядка присваиваний во внутреннем AABB-конструкторе | Preview 2.1.0 Build 1460, **12.11.2025** | то же |
| `HitboxManager` самоуничтожается, если лаг-компенсация выключена в `NetworkProjectConfig`; кэширование ссылки; трейс-канал `LagCompensation` | 2.1.2 Build 2279, **13.08.2026** | ⚠️ **ломает приём курса**: там `HitboxManager` поставлен в сцену вручную при `"Enabled": false`. На 2.1.2+ такая схема перестанет работать — включай `Enabled` |
| Shared Mode tick/send rate стали настраиваемыми (до 32); дефолты остались 32 и 16 | nightly Build 1390, **14.10.2025** | в 2.0.x Shared Mode заблокирован на 32/16 |
| `Client & Server Tick Rate` — любое значение **8–256**; `Send Rate` — интервал **1–8** (1 = каждый тик), только делители тикрейта | 2.1 ([network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config)) | у курса `TickRateSelection.Client: 30` из фиксированного набора; на 2.1 выбор свободный. Дефолт тикрейта страница **не публикует** |
| `NetworkProjectConfig.ClientsOnlyRollbackUpdatedObjects` — клиенты пропускают rollback объектов без нового состояния | 2.1.3 Build 2390, **17.09.2026** | ⚠️ формулировку механики открытые источники не дают; влияние на стоимость предсказания — интерпретация, не факт |
| Unity для Fusion 2.1: 2021.3.45, 2022.3.45, 6.0.x, 6.3.x ([sdk-download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download)) | 2.1.x | Unity 2022.3 **поддерживается**, но нужен патч .45 (у курса .42f1) |
| Projectiles Advanced требует **Unity 6.3+** ([download](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/download)) | 2.1.1, 06.08.2026 | на Unity 2022.3 этот сэмпл не открыть; BR200 и Simple FPS требований Unity не публикуют |

Все даты и билды — [release-notes-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1), кроме помеченных отдельно. ⚠️ Переименование настроек между Fusion 1 и Fusion 2 (`HitboxBufferSize` в тиках → `HitboxBufferLengthInMs` в миллисекундах) **подтвердить первоисточником не удалось**: v1-страница манула настроек не перечисляет вообще, API-референс закрыт.

---

## Решение: что брать и когда

```
Нужен серверный авторитет над попаданиями?
├─ НЕТ (кооп, казуал, доверенные игроки)
│   └─ Shared Mode: Runner.GetPhysicsScene().Raycast локально
│      + RPC к владельцу цели + предсказание реакции на прокси (_predictedHealth)
│      → лаг-компенсации нет и не будет; закладывай, что урон читается
└─ ДА
    ├─ Host Mode (игрок-хост, нет затрат на сервер) ─┐
    └─ Server Mode (dedicated, Enterprise/self-host) ┘
        │
        ├─ Включи LagCompensation.Enabled (на 2.1.2+ обязательно: HitboxManager
        │  без флага самоуничтожается)
        ├─ Буфер: длина ≥ prediction + interpolation delay худшего клиента
        │
        ├─ Оружие мгновенное (hitscan)?
        │   ├─ ДА → Projectile Data Buffer: [Networked] Count
        │   │        + NetworkArray<HitData> ring, догон в Render()
        │   │        Raycast(player: Object.InputAuthority,
        │   │                SubtickAccuracy | IgnoreInputAuthority [| IncludePhysX])
        │   │        Пробивающее / AoE → RaycastAll / OverlapSphere + GetClosestHit
        │   └─ НЕТ (время полёта, баллистика, отскоки)
        │       ├─ снаряд не должен пережить стрелка → тот же Data Buffer,
        │       │   каждый тик короткий луч previous→next позиция
        │       │   (IncludePhysX | IgnoreInputAuthority)
        │       └─ должен пережить / нужна настоящая физика PhysX
        │           → NetworkObject + fire data, ОБЯЗАТЕЛЬНО пре-спавн
        │             (spawn prediction в Fusion 2 нет), ручной interest management
        │
        └─ Хитбоксы: по костям или статичная поза?
            ├─ соревновательный шутер → по костям + tick-accurate анимация
            │   (Playables / Fusion Animation Controller — experimental)
            │   + BroadRadius через SetMinBoundingRadius с запасом на анимацию
            └─ казуал / прототип → статичная поза (как Simple FPS): дёшево,
                но стрельба в вытянутую руку не работает
```

---

## Чеклист

1. Определи топологию **до** кода оружия. Shared Mode → `Runner.LagCompensation.*` не использовать вообще.
2. Включи `LagCompensation.Enabled` в `NetworkProjectConfig`. Не полагайся на ручной `HitboxManager` в сцене: с 2.1.2 он самоуничтожается при выключенном флаге.
3. Посмотри `Hitbox Buffer Length In Ms`, `Hitbox Default Capacity` и `Cached Static Colliders Size` **в инспекторе своей версии SDK** и запиши фактические значения в документацию проекта. Дефолты на веб-страницах не публикуются.
4. Подбери длину буфера под окно худшего поддерживаемого клиента (prediction + interpolation delay), а не среднего. Проверь включением `NetworkConditions`.
5. `HitboxRoot` — только на самый верхний узел объекта. Больше 31 хитбокса — дроби на несколько рутов.
6. `BroadRadius` подбирай через `SetMinBoundingRadius()` и добавь запас на анимационные крайние положения конечностей.
7. Динамические Unity-`Collider` — на отдельный слой, который лаг-компенсированный луч игнорирует. Не удаляй их: они нужны физике.
8. Смену слоя хитбокса в рантайме делай только через `Hitbox.SetLayer(int)` — слой кэшируется при регистрации.
9. Мёртвых, неуязвимых, зрителей и рагдоллы гаси через `HitboxActive`/`HitboxRootActive`/`SetHitboxActive`, а не фильтром в `preProcessRoots` на каждом выстреле.
10. В запросе всегда передавай `player: Object.InputAuthority` и `HitOptions.IgnoreInputAuthority`. `SubtickAccuracy` — когда FPS заметно выше тикрейта.
11. Нужна геометрия уровня в запросе — добавь `IncludePhysX` и помни: эти коллайдеры **не** перематываются. Для 2D нужен отдельный `Runner.GetPhysicsScene2D().Raycast`.
12. Делегат `preProcessRoots` кэшируй, данные передавай через `Query.UserArgs` + `CreateQuery`/`ResolveQuery`. Тип второго параметра (`List<>` или `HashSet<>`) сверь с компилятором — документация и поставка расходятся.
13. Попадания и выстрелы храни в сетевом кольцевом буфере (`[Networked] Count` + `NetworkArray<T>`), а не как `NetworkObject` на пулю. Capacity — минимум на 2 тика выстрелов.
14. Позицию хита в структуре храни **относительной** к цели — прокси интерполируются между двумя серверными тиками, и абсолютная позиция уедет с тела.
15. Прокси рендери в удалённом тайм-фрейме: `Object.IsProxy ? Runner.RemoteRenderTime : Runner.LocalRenderTime`.
16. Разведи обратную связь: импакты и кровь — мгновенно, хит-маркеры и цифры урона — только после подтверждения новым серверным состоянием.
17. Точку стрельбы вынеси из анимируемого ствола (BR200 ставит её у плеча) и считай попадание двухэтапно: камера → target point, затем точка стрельбы → target point.
18. Любую случайность в симуляции сей от тика и id объекта. Без этого лаг-компенсация бессмысленна: сервер и клиент разыграют разный разброс.
19. Если хитбоксы ведёт анимация — поднимай точность анимации до tick-accurate и проверяй совпадение серверных и прокси-хитбоксов визуально (приём `HitboxDraw` из Fusion Animations).
20. Профилируй через страницу Lag Compensation в Fusion Statistics на **DEBUG DLL** и трейс-канал `LagCompensation` (2.1.2+). Нормативов по числу хитбоксов нет — измеряй.
21. Напиши свои ограничения поверх перемотки: лимит глубины отката, отбраковка устаревшего тайминга ввода, лимит частоты выстрелов. Photon этого не документирует.
22. Если проект на 2.0.x — учитывай, что три фикса лаг-компенсации (layer mask при `SubtickAccuracy`, box overlap при Y-повороте и при offset) в твою ветку не попали.

---

## Источники

Все ссылки проверены 01.11.2026 прямым чтением страниц.

| URL | Что взято |
|---|---|
| [doc.photonengine.com/fusion/current/manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation) | ограничение Server/Host Mode; `HitboxRoot` на верхнем узле; лимит 31 хитбокс; `BroadRadius`/`Offset` с запасом на анимацию; кэширование слоя и `SetLayer`; дисциплина слоёв; `HitOptions` (3 флага); сигнатура `PreProcessingDelegate`; обход layer mask и Active-state; отсутствие point/normal у PhysX-overlap; 2D и `GetPhysicsScene2D`; признание платы за WYSIWYG |
| [.../fusion/v2/manual/network-project-config](https://doc.photonengine.com/fusion/v2/manual/network-project-config) | состав секции Lag Compensation; минимум 16 у `Hitbox Default Capacity`; отсутствие публичных дефолтов; tick rate 8–256 в 2.1; send rate как интервал 1–8; лимит 32 в Shared Mode |
| [.../fusion/v2/manual/advanced/time-synchronization](https://doc.photonengine.com/fusion/v2/manual/advanced/time-synchronization) | формула окна перемотки = prediction + interpolation delay; отсутствие числовых дефолтов |
| [.../fusion/v2/manual/advanced/interest-management](https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management) | отсутствие упоминаний лаг-компенсации и хитбоксов; «will not receive any updates»; `IInterestEnter`/`IInterestExit` |
| [.../fusion/v2/manual/testing-and-tooling/fusion-statistics](https://doc.photonengine.com/fusion/v2/manual/testing-and-tooling/fusion-statistics) | страница Lag Compensation (процент используемых хитбоксов, время внутренних обновлений); только DEBUG DLL; страница Forecast Objects |
| [.../fusion/v2/getting-started/sdk-download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download) | 2.1.3 Stable Build 2390 (17.09.2026); 2.0.13 Stable Build 2379 (16.09.2026); Realtime 5 и несовместимость 2.1 с 2.0; Unity 2021.3.45/2022.3.45/6.0.x/6.3.x |
| [.../fusion/v2/getting-started/release-notes/release-notes-2-1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1) | даты четырёх фиксов лаг-компенсации (Build 1240 от 08.08.2025, Build 1460 от 12.11.2025); 2.1.2 Build 2279 (13.08.2026): auto-destroy `HitboxManager`, кэширование, трейс-канал; 2.1.3 Build 2390 (17.09.2026): `ClientsOnlyRollbackUpdatedObjects`; Build 1390 (14.10.2025): Shared Mode tick/send настраиваемы, дефолты 32/16 |
| [.../fusion/v2/troubleshooting/faq](https://doc.photonengine.com/fusion/v2/troubleshooting/faq) | отсутствие тем лаг-компенсации, читов и серверной валидации |
| [.../fusion/v2/game-samples/br200/overview](https://doc.photonengine.com/fusion/v2/game-samples/br200/overview) | 200 игроков, 60 Hz, Dedicated Server & Client Host; «Render-accurate lag compensated shooting», «Tick-accurate animation system», «Projectile piercing (penetration)», «Advanced interest management» |
| [.../fusion/v2/game-samples/br200/weapons](https://doc.photonengine.com/fusion/v2/game-samples/br200/weapons) | «no network object is spawned per shot»; `ProjectileData` + `Capacity(10)`; `DummyProjectile`; гранаты как standalone с лучом previous→next; `BodyPart : Hitbox` + множитель; `BodyHitData` + `Capacity(4)` и относительная позиция; двухэтапный райкаст; точка стрельбы у плеча вместо ствола; The Corner Issue и `ProjectileUtility`; мгновенные импакты vs подтверждённые хит-маркеры |
| [.../fusion/v2/game-samples/br200/performance-optimizations](https://doc.photonengine.com/fusion/v2/game-samples/br200/performance-optimizations) | все четыре бенчмарка с железом; удаление интеграции UGS в 2.1.0; оговорка «worst case scenario» |
| [.../fusion/current/game-samples/simple-fps/architecture](https://doc.photonengine.com/fusion/current/game-samples/simple-fps/architecture) | `ProjectileData { HitPosition, HitNormal, ShowHitEffect }`; circular buffer; `BodyHitbox : Hitbox`, голова — двойной урон; статичная поза хитбоксов |
| [.../fusion/v2/game-samples/simple-fps/overview](https://doc.photonengine.com/fusion/v2/game-samples/simple-fps/overview) | «The Simple FPS also uses Client Side Prediction and Lag Compensation»; три хитскан-оружия; Simple KCC |
| [.../fusion/v2/game-samples/tanknarok/project-architecture-host](https://doc.photonengine.com/fusion/v2/game-samples/tanknarok/project-architecture-host) | «lag-compensated raycast … using `Runner.LagCompensation.Raycast`»; лучи вдоль экстраполированного пути; «Shots are not NetworkObjects»; `ShotState { StartTick, EndTick, Position, Direction }` |
| [.../fusion/v2/game-samples/tanknarok/project-architecture-shared](https://doc.photonengine.com/fusion/v2/game-samples/tanknarok/project-architecture-shared) | отсутствие упоминаний лаг-компенсации; `RPC_RequestAreaDamage()` + `ApplyAreaDamage()`; `_predictedHealth` на прокси |
| [.../fusion/current/technical-samples/projectiles-essentials](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-essentials) | полные списки плюсов/минусов подходов A–D; «NetworkObject spawn is not predicted in Fusion 2»; пре-спавн; строка `renderTime`; HitOptions в Example 2; устаревшая фраза про Advanced; версии 2.1.1 (10.07.2026) и 2.0.4 (20.12.2024) |
| [.../fusion/v2/technical-samples/projectiles-advanced/projectiles](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/projectiles) | «Projectile is not a `NetworkBehaviour`»; сигнатуры `GetFireData`/`OnFixedUpdate`/`Activate`/`Deactivate`/`Render`; префабы vs инстансы; полный список типов снарядов; выстрел из центра камеры + интерполяция; «For hitscan projectiles no interpolation is needed»; опечатка `GetFiredData` |
| [.../fusion/v2/technical-samples/projectiles-advanced/transition-from-fusion1](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/transition-from-fusion1) | «Removed `ProjectileManager`»; `NetworkDataBuffer` + `HitscanProjectileBuffer`/`KinematicProjectileBuffer`; `NetworkObjectBuffer` и пре-спавн вместо spawn prediction; визуалы живут дольше данных буфера |
| [.../fusion/v2/technical-samples/projectiles-advanced/download](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-advanced/download) | версия 2.1.1 от 06.08.2026; требование Unity 6.3+ |
| [.../fusion/current/technical-samples/fusion-animations](https://doc.photonengine.com/fusion/current/technical-samples/fusion-animations) | связь точности анимации и лаг-компенсации; `HitboxDraw`, зелёные серверные и синие прокси-хитбоксы, интервал 50 тиков; примеры 1–5 render accurate, 6 tick accurate на Playables; «Fusion Animation Controller is in the experimental state»; NMA «more bandwidth intensive» |
| [.../fusion/v1/manual/lag-compensation](https://doc.photonengine.com/fusion/v1/manual/lag-compensation) | идентичность правил Fusion 1 и 2 (лимит 31, `BroadRadius`/`Offset`, те же три `HitOptions`, те же имена методов); отсутствие перечня настроек буфера на v1-странице |
| [photonengine.com/fusion/pricing](https://www.photonengine.com/fusion/pricing) | тарифы и CCU: Development 20 CCU / 60 GB; Launch Free 100 CCU / 0.3 TB; 500 CCU $125/мес; 1000 CCU $250/мес; 2000 CCU $500/мес; Premium $0.50/CCU при минимуме $1000/мес; перерасход $0.05–0.10/GB; dedicated-серверы только в Enterprise |
| `doc-api.photonengine.com` (`LagCompensationSettings`, `HitboxManager`, `LagCompensatedHit`, `HitboxRoot`) | **недоступен**: на 01.11.2026 закрыт bot-защитой Gcore. Поэтому не подтверждены первоисточником: полный список `HitOptions`, полный список полей `LagCompensatedHit`, дефолты `LagCompensationSettings`, свойство `HitboxRoot.InInterest`, правило «hitbox и его root должны быть активны», переименование настроек между Fusion 1 и 2 |
| [гл. 06](06-chapter.md) части I — дамп `NetworkProjectConfig.fusion` и `Character.prefab` поставки Fusion 2.0.5 | фактические значения `Enabled: false`, `HitboxBufferLengthInMs: 200`, `HitboxDefaultCapacity: 512`, `CachedStaticCollidersSize: 64`; `Hitbox.Type: 3` = Capsule; `SetMinBoundingRadius()`; `IncludeBox2D` в XML-доке `Raycast`; `HashSet<HitboxRoot>` в `PreProcessingDelegate` 2.0.5; `GetClosestHit`/`PositionRotation`/`GetPlayerTickAndAlpha`; детерминированный сид разброса; `NetworkConditions` 150 мс / 50 мс джиттера / 5% потерь |

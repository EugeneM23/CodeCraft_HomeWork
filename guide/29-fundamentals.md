# Глава 29. Фундамент: теория сетевых игр

> **TL;DR для агента**
> 1. **Транспорт — UDP, и игровой протокол не расщепляют.** TCP непригоден не из-за накладных расходов, а из-за head-of-line blocking; TCP-трафик к тому же «индуцирует» потери UDP в том же канале ([Fiedler, UDP vs TCP](https://gafferongames.com/post/udp_vs_tcp/)). Отдельный HTTP к REST-сервисам — можно, второй игровой канал — нет.
> 2. **Схем синхронизации всего четыре:** deterministic lockstep, rollback, snapshot interpolation, state synchronization. Fusion 2 в Host/Server mode — это state sync + prediction. Выбор схемы делается **до** выбора SDK, а не после.
> 3. **Три обязательных трюка** — client-side prediction, entity interpolation, lag compensation. Каждый покупает отзывчивость ценой строго определённой несправедливости.
> 4. **Буфер интерполяции ≈ 3× периода отправки** при 2–5 % потерь: 300 мс при 10 пакетах/с, ~150 мс при 30, ~85 мс при 60 ([Fiedler, Snapshot Interpolation](https://gafferongames.com/post/snapshot_interpolation/)). Трафик и задержка — один и тот же ресурс.
> 5. **Идеальной честности не существует.** Выбирается только то, кто платит задержку. Это решение геймдизайна, а не настройка в конфиге.

---

## Зачем это знать

Часть I этого руководства выведена из учебного курса: там есть готовый `NetworkRunner`, галочка `Hitbox Buffer Length In Ms` и работающий код. Чего там нет — объяснения, **почему** всё устроено именно так и какие альтернативы существуют. Агент, знающий только API Fusion, совершает два системных класса ошибок:

* применяет Host Mode к задаче, для которой в экосистеме того же вендора есть другой SDK (RTS на тысячу юнитов, P2P-файтинг → Photon Quantum, а не Fusion);
* ищет причину «лагов» в коде игры там, где проблема принципиально нерешаема на уровне кода (peeker's advantage).

Эта глава — вендоро-независимый фундамент. Названия API и настроек даны в оригинале; где источник не читался напрямую — стоит явная пометка.

---

## 1. Транспорт: почему UDP и что приходится дописывать руками

Единственный жёстко установленный факт транспортного уровня: для экшен-игр TCP непригоден. Glenn Fiedler формулирует это без полутонов: «Using TCP is the worst possible mistake you can make when developing a multiplayer game!» ([UDP vs TCP](https://gafferongames.com/post/udp_vs_tcp/)).

Механизм — head-of-line blocking: при потере пакета более новые уже пришедшие данные ставятся в очередь и недоступны приложению, пока потерянный пакет не будет переслан. Цена восстановления: минимум 1 RTT на обнаружение, «commonly it takes 2*RTT, and another one way trip from the sender to the receiver for the resent packet to get there». На пинге 125 мс это ~1/5 секунды в лучшем случае и до полусекунды и более в плохих условиях — после чего данные приезжают пачкой и обрабатываются в одном кадре. Игре не нужны все состояния, ей нужно **самое свежее**.

Второй механизм — алгоритм Нейгла: TCP копит мелкие записи до «разумного» размера пакета. Для игровых пакетов по 10–80 байт это чистая задержка. `TCP_NODELAY` его отключает, но не лечит обработку потерь и переупорядочивания. Третий — конкуренция: «TCP tends to induce packet loss in UDP packets», отсюда правило «Don't mix TCP and UDP!» с единственным допустимым исключением — отдельные RESTful-сервисы по HTTP (матчмейкинг, профили, лидерборды).

Цена UDP: всё, что даёт TCP, нужно реализовать выборочно — виртуальные соединения, обнаружение потерь и переотправка, порядок, flow control, congestion avoidance и **собственная контрольная сумма**: «You can't even rely on the UDP checksum so you must add your own» (16 бит слишком слабо для длительной отправки на высокой частоте).

### Канонический дизайн надёжности поверх UDP

Fusion и Realtime внутри делают то же самое, так что это полезно знать как эталон ([Reliability, Ordering and Congestion Avoidance over UDP](https://gafferongames.com/post/reliability_ordering_and_congestion_avoidance_over_udp/)):

| Механизм | Значение из источника |
|---|---|
| sequence number | локальный, **никогда не переиспользуется** — потерянные данные уходят в новом пакете с новым номером |
| ack + ack bitfield | 32 бита; бит n = «пакет ack − n получен». Каждый пакет подтверждает **всегда 33** номера; каждый ack дополнительно пересылается до 32 раз |
| критерий потери | нет ack в течение **одной секунды** — это эвристика, а не факт |
| оценка RTT | экспоненциальное сглаживание, «10% seems to work well for me in practice» |
| congestion avoidance | бинарное: good mode **30 пакетов/с**, bad mode **10 пакетов/с**, порог «say 250ms» RTT. Вниз — мгновенно, вверх — через период *t*; *t* удваивается (потолок 60 с), если срыв случился в первые 10 с хорошего режима, и делится вдвое за каждые 10 с хорошего режима (пол 1 с) |

> ⚠️ Тезис «чтобы ack не дошёл, нужно потерять все пакеты более чем за секунду» — **производный**. Статья говорит про 33 подтверждаемых номера; переход «33 пакета подряд → больше секунды» верен только при ~30 пакетах/с.

### MTU и фрагментация

Проектное ограничение, которое курс не упоминает вовсе ([Packet Fragmentation and Reassembly](https://gafferongames.com/post/packet_fragmentation_and_reassembly/)): реальный MTU IPv4 — 1500 байт, после заголовков UDP/IP остаётся ~1472 байта полезной нагрузки; консервативная нижняя граница для интернета — **1200 байт**, и игры обычно исходят из 1000–1200 байт payload. IP-фрагментация опасна: «if any fragment of that packet is lost then the entire packet is lost». Прикладная фрагментация в статье — до 1024 байт на фрагмент, максимум 256 фрагментов (потолок 256 КБ), но рекомендация жёсткая: фрагментировать только в диапазоне 2–4 фрагментов и только для time-critical данных, терпящих потерю; большие надёжные данные (начальное состояние мира) — отдельным resend-механизмом.

**Директива.** Держите сетевое состояние одного тика в пределах ~1200 байт. В Fusion 2.0 это косвенно обеспечивалось лимитом RPC 512 байт; в 2.1 лимит снят через `RpcChannel.ReliableLargeData` — и вместе с лимитом снята защита «от дурака».

---

## 2. Четыре схемы синхронизации

| Схема | Что передаётся | Нужен детерминизм | Трафик | Где применимо |
|---|---|---|---|---|
| **Deterministic lockstep** | только ввод | бит-в-бит | ∝ размеру ввода, **не** числу объектов | RTS, пошаговые, P2P-кооп |
| **Rollback** | только ввод + ресимуляция | бит-в-бит + save/load/advance | как lockstep | файтинги 1v1 |
| **Snapshot interpolation** | полное состояние каждый тик | не нужен | очень высокий | демо, реплеи, простые случаи |
| **State synchronization** | ввод **и** подмножество состояния | не нужен | управляемый бюджетом | шутеры, физические песочницы, **Fusion Host/Server** |

### Deterministic lockstep

В демо Fiedler'а ввод — 6 бит (left, right, up, down, space, z) ([Deterministic Lockstep](https://gafferongames.com/post/deterministic_lockstep/)). В пакет кладутся все неподтверждённые входы: худший случай 120 входов (~2 с при 60 fps) = 720 бит = 90 байт; с дельта-кодированием (1 бит если ввод не изменился, 7 бит если изменился) накладные расходы худшего случая — 120 бит = **15 байт**.

Ограничения жёсткие. Расхождение возможно между компиляторами, ОС и наборами инструкций (PowerPC vs Intel) и даже между debug и release из-за оптимизаций с плавающей точкой: «Floating point determinism is a complicated subject and there's no silver bullet». Симуляция не может шагнуть без следующего ввода, поэтому jitter и потери видны как пауза; playout delay buffer в демо — **100 мс**, догон ограничен **4 шагами** симуляции за кадр рендера против spiral of death.

**Исторический пример — Doom (1993), а не dead reckoning.** Doom — peer-to-peer deterministic lockstep: ввод упаковывается в `ticcmd_t` («The data sampled per tick… and transmitted to other peers», «plus a checksum for internal state consistency») с полями `forwardmove`, `sidemove`, `angleturn`, `chatchar`, `buttons` и `short consistancy; // checks for net game` ([d_ticcmd.h, id Software DOOM source](https://github.com/id-Software/DOOM/blob/master/linuxdoom-1.10/d_ticcmd.h)). Кадр идёт только когда получены `ticcmd` всех игроков. Относить Doom к линии dead reckoning — ошибка.

### Rollback (GGPO)

Тот же детерминизм, но игра не ждёт сеть: «Rollback networking uses input prediction and speculative execution to send player inputs to the game immediately»; при опоздании удалённых входов «the networking code will predict what it expects the remote players to do based on previously seen inputs», при промахе — ресимуляция от точки расхождения ([ggpo.net](https://www.ggpo.net/)). От движка требуются три операции: «save your game state, load it back up, and execute a frame of game state without rendering its outcome». Альтернатива — input delay, и её минус сформулирован точно: «The longer it takes to get inputs over the network, the slower the game becomes». SDK — на GitHub под MIT (возможны иные условия по запросу).

### Snapshot interpolation

900 кубов × 225 бит (28.1 байта) = снапшот ~25 КБ; при 60 пакетах/с — «1520550 bytes per-second or 11.6 megabit/sec» ([Snapshot Interpolation](https://gafferongames.com/post/snapshot_interpolation/)). Снижение до 10 пакетов/с экономит примерно шестикратно, но требует буфера: «the amount of delay that works best at 2-5% packet loss is 3X the packet send rate» — **300 мс** при 10 пакетах/с, плюс «about 50ms for jitter» = **350 мс**, использованные в видео (50 мс — это ~3 кадра при 60 fps, а не 1–2). Обратное и практически важное: **~150 мс** при 30 пакетах/с и **~85 мс** при 60.

Линейная интерполяция на 10 Гц даёт артефакты (дрожание висящего куба, «пульсация» плотного кома, интерполирующего сквозь игрока); Hermite-сплайны по линейной скорости убирают их на той же частоте, но скорость обязательно передавать. Для ориентации достаточно замены nlerp на slerp — угловую скорость передавать не надо. Экстраполяция на 200 мс сократила бы задержку с 350 до 150 мс, но для твёрдых тел плоха: «extrapolation doesn't know anything about the physics simulation» — проваливание через пол, игнор пружин, промахи по столкновениям.

### Порядок величин при сжатии

Baseline — 321 бит на куб (ориентация 128 + линейная скорость 96 + позиция 96 + флаг 1 = 40.125 байта), 901 куб при 60 Гц = «17.37 megabits per-second!» ([Snapshot Compression](https://gafferongames.com/post/snapshot_compression/)).

| Шаг | Результат |
|---|---|
| ориентация «smallest three» (2 бита индекс + 3×9 бит, границы ±0.707107) | 128 → **29 бит** |
| линейная скорость | выброшена целиком (на 60 Гц достаточно линейной интерполяции) |
| позиция, 512 значений на метр (~2 мм) | 18+18+14 = **50 бит** |
| итого | «This reduces our cube state to 80 bits, or just 10 bytes per-cube» |
| delta compression против подтверждённого baseline | неизменённый куб — **1 бит** (крупнейший единичный выигрыш); относительный индекс ~5.5 бита; дельта позиции ~26.1 бита; дельта ориентации 23.3 бита (80.3 % от абсолютной) |
| в статике | «around 15 kilobits per-second», почти целиком заголовки IP/UDP |
| контекстное арифметическое кодирование | **+25 %** к лучшему bit-packed результату |

> Финальную полосу после всех оптимизаций статья числом **не называет**. Цифра 256 кбит/с из демо state sync — это **целевой бюджет**, а не подтверждённый итог сжатия.

Главный тезис автора: лучшие оптимизации трафика не про то, как ты сжимаешь, а про то, **что ты не отправляешь**.

### State synchronization — то, что делает Fusion

Гибрид: симуляция крутится на обеих сторонах, передаются и входы, и состояние, идеальный детерминизм не нужен, между апдейтами объекты движутся сами ([State Synchronization](https://gafferongames.com/post/state_synchronization/)). Числа демо: `MaxInputsPerPacket = 32`, `MaxStateUpdatesPerPacket = 64` из 901 куба, бюджет **256 кбит/с** (изменяемый на ходу как congestion control), jitter buffer 4–5 кадров при 60 Гц.

Кто попадёт в пакет — решает **priority accumulator**: приоритеты (1 000 000 для своего куба, 100 для взаимодействующих, 1 для покоящихся) каждый кадр добавляются в аккумулятор, объекты сортируются по аккумулятору, отправленные сбрасываются в ноль, не влезшие сохраняют значение и идут в начало следующего пакета.

> Индустриальный первоисточник приоритизации/скоупинга — «The TRIBES Engine Networking Model» (Frohnmayer & Gift). ⚠️ В этом прогоне текст PDF ([gamedevs.org](https://www.gamedevs.org/uploads/tribes-networking-model.pdf)) извлечь не удалось — ссылка даётся как указание на первоисточник, числа оттуда не приводятся.

Критичная деталь, которую почти всегда забывают: квантовать нужно **всё** состояние симуляции на **обеих** сторонах перед каждым шагом, иначе симуляции расходятся и «попают» при каждом апдейте. Точность приходится поднимать: 512 → **4096** значений позиции на метр, 9 → **15 бит** на компоненту кватерниона, иначе грубое квантование загоняет тела во взаимное проникновение и солвер начинает с этим бороться.

Сглаживание — **только в рендере**, не в симуляции: держатся `position_error` и `orientation_error`, рендер идёт как `position + position_error` и `orientation * orientation_error`; затухание ~0.9 за кадр, адаптивно ~0.95 при ошибке ≤25 см и ~0.85 при ≥1 м, ориентация slerp'ится к единице по 0.1 за кадр.

---

## 3. Триада: prediction / interpolation / lag compensation

Это та часть, которую Fusion даёт из коробки (см. [гл. 01](01-chapter.md), [гл. 05](05-chapter.md), [гл. 06](06-chapter.md)), но понимать её надо вендоро-независимо.

**Client-side prediction + server reconciliation** ([Gambetta](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html)). Клиент нумерует каждый ввод («the client adds a sequence number to each request»), сервер в каждом апдейте «includes the sequence number of the last input it processed». Клиент: (1) берёт состояние сервера как базу, (2) «discards its copies of sent input up to #1», (3) «applies all the input still not seen by the server». Апдейты описывают прошлое, но клиент знает, каких входов сервер ещё не видел, — переигрывание восстанавливает настоящее. Без prediction при 100 мс задержки и 100 мс анимации «the whole action would take 200 ms».

Отдельный совет Gambetta: необратимое не предсказывать — «you shouldn't actually update the health of the character until the server says so», «you may want to avoid killing a character until the server says so».

> ⚠️ Фразу «don't trust the player» **нельзя** давать как цитату Gambetta. Дословная формулировка принципа на той же странице: «if a hacked client would send invalid inputs, it could render whatever it wanted on the screen, but it wouldn't affect the state of the server, which is what the other players see». Отсюда же общий вывод, которого нет в курсе: серверный авторитет — это не про отзывчивость, а про **модель доверия**; валидация ввода на сервере обязательна независимо от prediction.

**Entity interpolation** ([Gambetta](https://www.gabrielgambetta.com/entity-interpolation.html)). Чужие объекты рисуются в прошлом: имея позиции для t=900 и t=1000, клиент показывает движение между ними, когда его время между 1000 и 1100. Это не предсказание вообще — только серверные позиции, задержанные на ~100 мс. Следствие: каждый игрок видит **свой** мир — себя в настоящем, остальных в прошлом. Dead reckoning (экстраполяция по скорости и курсу) работает для техники, не способной мгновенно сменить направление, и ломается на шутерных игроках, которые резко останавливаются и разворачиваются.

> ⚠️ Привязка dead reckoning к стандарту IEEE DIS / наследию DARPA SIMNET и тезис «классические реализации используют один фиксированный error threshold, адаптивные — академические» первоисточником **не подтверждены**: единственный источник — обзорная статья 2010 г. ([arXiv:1008.3758](https://arxiv.org/abs/1008.3758)), сам стандарт не читался. Doom к этой линии **не относится** (см. раздел 2).

**Lag compensation** ([Gambetta](https://www.gabrielgambetta.com/lag-compensation.html)). Из «я вижу чужих в прошлом» следует, что идеальный выстрел в голову промахивается. Решение: выстрел несёт точный таймстемп, сервер «can reconstruct the world exactly as it looked like to any client at any point in time», оценивает выстрел в восстановленном мире и рассылает результат. Плата — несправедливость к цели: она может быть убита уже за укрытием. Gambetta называет это «somewhat unfair», но «the most agreeable solution for everyone involved».

> ⚠️ Жаргонное «favor the shooter» в проверенных первоисточниках (Riot, страница доклада Overwatch на GDC Vault) **не встречается** — не приписывайте его конкретному вендору.

---

## 4. Числа индустрии: чем мерить свои настройки

| Продукт | Тик / частота | Окно откатa | Задержка интерполяции | Статус источника |
|---|---|---|---|---|
| **Source 1** (CS:S, DoD:S, TF2) | tickrate **66**; L4D/L4D2 — **30**. `-tickrate` на этих играх **недоступен** (менять нельзя) | «keeps a history of all recent player positions for **one second**» | `cl_interp 0.1` = **100 мс** по умолчанию; `cl_updaterate` по умолчанию **20**; формула «interpolation period = max( cl_interp, cl_interp_ratio / cl_updaterate )» | дословная копия вики Valve ([зеркало](https://gist.github.com/CoolOppo/fe0586836de3fb2f90f9); оригинал [developer.valvesoftware.com](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) не открывался) |
| **VALORANT** | сервер **128 Гц**; клиент и сервер обновляют движение и физику ровно 128 раз/с независимо от fps | есть, «tuned per region»; числовое значение Riot **не публикует** | буферизация ~2 кадра на сервере (0.5 + 0.5 + 1), ~3 кадра на клиенте (0.5 + 1 + ~0.5 GPU/swap chain) | официальный пост Riot ([peeking-valorants-netcode](https://www.riotgames.com/en/news/peeking-valorants-netcode)) |
| **Overwatch 1 (2016–17)** | командный кадр **16 мс** (~60 Гц), турнирный **7 мс** (~128 Гц); при 30 fps — два тика на кадр | bounding volume на ~последние **0.5 с** движения для широкой фазы перед rewind | лид клиента = RTT/2 + 1 буферизованный кадр (~96 мс при RTT 160 мс); отключение предсказания импакта выше ~**220 мс** RTT | ⚠️ **вторичный источник**, разбор [Edgegap](https://edgegap.com/blog/game-backend-deep-dive-overwatch-2016-netcode-architecture-rollback); сам доклад ([GDC Vault 1024001](https://gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and), Timothy Ford, Blizzard, GDC 2017, трек Programming) недоступен |

Дополнительно по Source 1: `cl_updaterate` по умолчанию 20 и command rate «usually 30» — именно отсюда во вторичных источниках берётся «~21 Гц клиентский рейт» эпохи Overwatch 2016. Единственный совет вики про потери касается `cl_updaterate`: «If you increase the snapshot rate and encounter packet loss or choke, you have to turn it down again» — **значений `cl_interp_ratio` 3/4 в вики Valve нет**, это community lore. К Counter-Strike 2 (sub-tick input) значения Source 1 не применимы.

> ⚠️ **Не подтверждено первоисточником:** Overwatch high bandwidth mode (повышение клиентского update rate, по прессе августа 2016 — с ~20.8–21 до 60–63 апдейтов/с; тезис Jeff Kaplan'а «сервер тикал на 60 Гц и раньше, низким был клиентский update rate»; оценка «задержка интерполяции с ~60 мс до ~20 мс»). Официального поста Blizzard найти не удалось, цифры расходятся между изданиями, разбор Edgegap высокобандвидный режим вообще не описывает. Используйте только как порядок величины.
>
> ⚠️ **Не подтверждено:** Rocket League — доклад «It IS Rocket Science! The Physics of Rocket League Detailed» (Jared Cone, Psyonix, GDC 2018) существует ([gamedeveloper.com](https://www.gamedeveloper.com/design/video-the-rocket-science-behind-i-rocket-league-s-i-physics)), но числа (120 Гц, ~8 мс на кадр, детерминизм, идентичная физика на клиенте и сервере) на доступных страницах отсутствуют.

### VALORANT: peeker's advantage в миллисекундах

Самая ценная для теории честности величина ([Riot](https://www.riotgames.com/en/news/peeking-valorants-netcode)): базово выглядывающий получает **~141 мс** лишнего времени на реакцию при человеческой реакции ~300 мс. Riot Direct и региональные серверы (цель — 35 мс пинга для 70 % игроков) срезают ~**40 мс** (28 %), а 144 fps снижают преимущество до ~**71 мс** (−49 %). В плейтестах топовые перестрелки решались на 20–50 мс, а в одном сценарии разница **10 мс** сдвигала винрейт с 90 % в пользу держащего угол до 90 % в пользу выглядывающего. Если ход не пришёл, сервер предсказывает его, обычно продолжая последние полученные входы.

Вывод, которого нет в курсе: lag compensation **обязана** иметь предел откатa — иначе игрок с 500 мс задержки убивает того, кто уже ушёл за укрытие. Индустриальный ориентир для этого предела есть ровно один проверенный: **1 секунда** в Source 1.

---

## 5. Фиксированный шаг и где взять детерминизм в Unity

Любая из четырёх схем предполагает фиксированный timestep ([Fix Your Timestep!](https://gafferongames.com/post/fix_your_timestep/)). Переменный dt меняет не «ощущение», а устойчивость: пружины взрываются, быстрые тела туннелируют сквозь стены. Semi-fixed timestep ограничивает dt, но открывает spiral of death — «It's what happens when your physics simulation can't keep up with the steps it's asked to take»; лечится запасом CPU или ограничением числа шагов на кадр.

```c
// Канонический accumulator (код из статьи Fix Your Timestep!, не из учебного проекта)
if (frameTime > 0.25) frameTime = 0.25;   // кламп догона
accumulator += frameTime;
while (accumulator >= dt) {
    integrate(state, t, dt);
    accumulator -= dt;
    t += dt;
}
const double alpha = accumulator / dt;    // рендер по двум состояниям; для ориентации — slerp
render(lerp(previousState, currentState, alpha));
```

Это ровно та конструкция, что в Fusion даёт разделение `FixedUpdateNetwork()` / `Render()` ([гл. 01](01-chapter.md)). Fusion 2 задаёт шаг через `Simulation > Tick Rate` в `NetworkProjectConfig` в герцах и отдаёт его как `NetworkRunner.DeltaTime`; **значение по умолчанию документация не называет** — 60 используется только как иллюстрация ([Network Simulation Loop](https://doc.photonengine.com/fusion/current/manual/network-simulation-loop)). Смотрите сам ассет `NetworkProjectConfig` в проекте.

**Детерминизм в Unity — отдельная проблема.** PhysX недетерминирован: «the physics engine inside Unity (PhysX) is not deterministic» ([Networked Physics in VR](https://gafferongames.com/post/networked_physics_in_virtual_reality/)). Варианты:

| Путь | Что даёт | Источник |
|---|---|---|
| **Fixed-point математика** (Photon Quantum) | `FP` типа **Q48.16** «completely replaces all usages of `floats` and `doubles` to ensure cross-platform determinism»; точность «decent when the numbers are kept within a certain range (0.01..1000)»; LUT для тригонометрии и sqrt; `FP.RawValue` — один `long` | [Quantum Fixed Point](https://doc.photonengine.com/quantum/v3/manual/quantum-ecs/fixed-point) |
| **Unity Physics (DOTS)** | «provides a deterministic rigid body dynamics system and spatial query system» (пакет 1.3.15) | [docs.unity3d.com](https://docs.unity3d.com/Packages/com.unity.physics@1.3/manual/index.html) |
| **PhysX / встроенная физика** | детерминизма нет | см. выше |

Контрпример к «всегда клиент-сервер»: в VR-сэмпле Fiedler lockstep отвергнут, потому что при 90 Гц и до 250 мс задержки «this meant 25 physics simulation steps for each visual render frame» — «25X cost is simply not realistic». Выбрана распределённая схема: авторитет над объектом переходит к тому, кто последним с ним взаимодействовал, ownership сильнее authority (захваченный куб нельзя отобрать до отпускания), «all packets flow through the host player, making the host the arbiter». Числа: симуляция 90 Гц, состояние «up to 60 times per-second», квантование позиции «a resolution of 1/1000th of a centimeter», депенетрация «one meter per-second works very well», покой — отсутствие значимого движения 16 кадров, jitter buffer аватара 100 мс, цель «under 256kbps» на игрока. Автор прямо ограничивает применимость кооперативом: серверной защиты от читов модель не даёт.

---

## 6. Честность: три обмена, которых не обойти

1. **Чьё время авторитетно при попадании.** Либо стрелок бьёт по тому, что видит (lag compensation → цель умирает за укрытием), либо попадание считается по текущему состоянию сервера (цель в безопасности → очевидные выстрелы не регистрируются). Третьего нет; смягчается только лимитом откатa.
2. **Буферизация против задержки.** Любой playout/jitter buffer покупает плавность задержкой один к одному. Отсюда «буфер ≈ 3× периода отправки» и прямая выгода от повышения send rate.
3. **Асимметрия позиции.** Peeker's advantage — следствие того, что каждый видит себя в настоящем, а других в прошлом. Его нельзя обнулить ни тикрейтом, ни инфраструктурой. Riot добирает честность **дизайном**: карты (защитник держит несколько углов, атакующий входит в одну точку), визуальные подсказки (плечи и POV появляются раньше головы), штраф к точности при стрельбе в движении, hit tagging (нелетальное попадание замедляет жертву).

---

## Чего по этой теме нет в учебном курсе

Курс — Host Mode, Fusion 2.0.5 build 920 (Feb 2025), Unity 2022.3.42f1.

1. **Транспорта как темы нет.** Курс начинается с готового `NetworkRunner` ([гл. 01](01-chapter.md)). Нет head-of-line blocking, ack bitfield, оценки RTT, congestion avoidance, MTU и фрагментации. Риск: агент не поймёт, почему «просто добавить TCP-канал для чата» — плохая идея, и будет искать причину лагов в коде игры.
2. **Из четырёх схем в курсе одна.** Deterministic lockstep, rollback и distributed authority не упоминаются. Агент применит Host Mode к RTS на тысячу юнитов или к P2P-файтингу и удивится цене.
3. **Photon Quantum в курсе отсутствует — это крупнейший смысловой пробел.** У того же вендора есть SDK именно под lockstep/rollback: «a high-performance deterministic ECS framework… for up to 128 players», predict/rollback, «game clients only exchange player input», рекомендуется для «latency-sensitive online games like sports games, fighting games, & FPS» ([quantum-intro](https://doc.photonengine.com/quantum/current/quantum-intro)). На 2026-11-01 актуален **Quantum SDK 3.0.14, Oct 05 2026, build 2241, Stable** ([download](https://doc.photonengine.com/quantum/v3/getting-started/initial-setup)).
4. **Числа курса вендорские, без ориентиров.** Сравнивать не с чем: таблица раздела 4 закрывает это.
5. **[Гл. 04](04-chapter.md) и [гл. 05](05-chapter.md) — это ровно snapshot interpolation vs extrapolation**, но без обоснования выбора. Теория объясняет, почему для снарядов экстраполяция работает (баллистика меняет направление не мгновенно), а для игрока в шутере — нет.
6. **[Гл. 06](06-chapter.md) даёт API, но не обмен.** `Runner.LagCompensation.Raycast` подан как единственный источник истины о попадании. Нет того, что это **выбор** в пользу стрелка, что цель умирает за укрытием, и что `Hitbox Buffer Length In Ms` — это и есть rewind cap. По доке: lag compensation «is only available in `Server Mode` and `Host Mode`», лимит **31 Hitbox** на `HitboxRoot`, `Hitbox Default Capacity` «has a minimum of 16», **значения по умолчанию ни для буфера, ни для capacity официально не документированы** ([lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation)). Флаг `HitOptions.SubtickAccuracy` запрашивает интерполированное состояние между тиками «using the exact interpolation factor the player saw when input was polled» — прямая реализация тезиса Gambetta о реконструкции мира клиента.
7. **[Гл. 08](08-chapter.md) — это priority accumulator без его общей формы.** В Fusion 2.0 приоритизации объектов не было вовсе; в **2.1** появился Object Send Priority.
8. **Версионный разрыв стал большим.** Таблица ниже.
9. **Честности как темы нет.** Нет peeker's advantage, нет мысли, что каждый видит свою версию мира, нет вывода, что идеальной честности не существует.
10. **Нет методики измерения.** Курс не говорит, чем проверять netcode (симуляция потерь/jitter/latency, воспроизводимые сценарии).

### Состояние на 2026-11-01 vs курс

| | Курс | Сейчас |
|---|---|---|
| Fusion | 2.0.5 build 920 (Feb 2025) | **2.1.3 / Sep 17 2026 / build 2390 / Stable**; ветка 2.0 — **2.0.13 / Sep 16 2026 / build 2379 / Stable** (2.1.2 build 2279 Aug 13 2026, 2.1.1 build 2177 Jun 30 2026 — все Stable) ([download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)) |
| Unity | 2022.3.42f1 | поддержаны 2021.3.45, 2022.3.45, 6.0.x, 6.3.x |
| Quantum | не упоминается | 3.0.14 / Oct 05 2026 / build 2241 / Stable |
| PUN 2 | не упоминается | «PUN 2 is in maintenance / LTS mode. Aside from some fixes no further feature updates are planned»; «New projects should consider Fusion or Quantum» ([pun-intro](https://doc.photonengine.com/pun/current/getting-started/pun-intro)) |
| Photon Bolt | не упоминается | статус официально не заявлен ни на [sdks](https://www.photonengine.com/sdks), ни на доступных страницах — **данных нет**, не выбирать для нового проекта |

**Что из теории этой главы вендор закрыл в Fusion 2.1** ([whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1)):

| Фича 2.1 | Теория, которую она реализует | Детали из доки |
|---|---|---|
| Tick rate «any tick rate between 8hz and 256hz» | выбор тикрейта как бизнес-решение | send rate до 1/8 от тика, если делится ровно; Shared Mode в 2.0 был «locked… to 32hz and the send rate to 16hz», в 2.1 это потолок |
| `NetworkObject.SetPriority(PlayerRef, int)` / `ClearPriority` | priority accumulator | 1 — каждый send-тик, 2 — каждый второй, 3 — каждый третий. В Host/Server ставит хост на пару игрок-объект, в Shared — только `LocalPlayer`. После host migration переустановить через `IAfterHostMigration` |
| `RpcChannel.ReliableLargeData` | снят лимит 512 байт | «Large RPCs are not tick accurate», порядок не сохраняется |
| **Forecast Physics** | экстраполяция физики в локальное время игрока | Host, Server, Shared. Это ровно техника, которую Fiedler для твёрдых тел считал проблемной — тестируйте |
| **Input Delay** (Server/Host, **выключен по умолчанию**) | «rollback vs input delay» из мира файтингов, вынесенный в настройку | `Input Delay Settings` в секции `Time`; `ClientsCanAddInputDelayToLimitResims` |
| `Input/State Receive Buffer Settings` | playout/jitter buffer | `Max Late Inputs` / `Redundant Inputs` / `Input Receive Buffer Delay Added`; `Max Late Snapshots` / `Redundant Snapshots` / `State Receive Buffer Delay Added`. **Значения по умолчанию не документированы** ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)) |
| `Client Max Resims Per Frame`, `Minimum Input Delay` | «ограничить шаги за кадр против spiral of death» | 2.1 only; defaults не документированы |
| AOI cell/grid size настраиваемы | Area of Interest | в Shared Mode «In Fusion 2.0 and earlier it was fixed to 32 units» |
| `NetworkProjectConfig.TimeSynchronizationOverride` | синхронизация часов | лекарство от неровного движения при низком send rate |
| `RequestMasterClientReassign()` + `AllowMasterClientReassignRequest` | host migration | порог по умолчанию **2 с** (`MasterClientReassignRequestThreshold`); 0 помечен как эксплуатируемый |
| `StartGameArgs.PlayerUniqueId` (long) | реконнект | тот же id → тот же `PlayerRef`; сам id не используется как `PlayerRef` |
| `CloudConnectionLostMode.MoveSessionToNewRoom` | устойчивость сессии | новое поведение по умолчанию, окно **10 секунд**, `NetworkRunner.CloudConnectionLostCurrentMode` |

**Цены Photon Public Cloud на 2026-11-01** ([pricing](https://www.photonengine.com/en-US/fusion/pricing); валюта на странице не названа): Development — 20 CCU, бесплатно, 60 ГБ/мес, только разработка; Free launch — 100 CCU, бесплатно, 0.3 ТБ/мес, одно приложение, стекается до 200 CCU; One-time 100 CCU — $95 единовременно, 0.3 ТБ/мес, 12 месяцев без автопродления; Monthly 500 — $125/мес, 1.5 ТБ; Monthly 1000 — $250/мес, 3.0 ТБ; Monthly 2000 — $500/мес, 6.0 ТБ. Premium Cloud — $0.50/CCU, минимум $1000/мес, до 50 000 CCU, 3.0 ГБ на пиковый CCU/мес. Переплата за трафик: **$0.05/ГБ** (Europe, Canada, East/RU/RUE, USA East/West) и **$0.10/ГБ** (Asia, Australia, China, India, Japan, South Africa, South America, South Korea, Turkey, UAE). Enterprise Cloud — по запросу.

---

## Решение: что брать и когда

```
Нужен бит-в-бит детерминизм (реплеи, античит на вводе, трафик ∝ вводу)?
├─ ДА
│   ├─ 1v1, отзывчивость важнее всего (файтинг) ────────► rollback: Quantum / GGPO
│   ├─ тысячи юнитов, input delay терпим (RTS) ────────► deterministic lockstep: Quantum
│   └─ в Unity нужна детерминированная физика ─────────► Quantum (FP Q48.16) или Unity Physics (DOTS).
│                                                        PhysX — НЕ детерминирован
└─ НЕТ
    ├─ нужен серверный авторитет / античит ─────────────► state sync + prediction + interpolation
    │                                                     + lag compensation с лимитом откатa
    │                                                     = Fusion Host / Server Mode (часть I)
    ├─ кооп/VR, физическая песочница, античит не нужен ─► distributed authority (Fusion Shared Mode)
    ├─ много объектов, невысокая точность (MMO) ────────► snapshot + priority accumulator + AoI,
    │                                                     предсказывать только своего персонажа
    └─ техника с инерцией (гонки, танки) ──────────────► dead reckoning / экстраполяция работает
```

| Жанр | Схема | Цена | Чем платит игрок |
|---|---|---|---|
| Файтинг 1v1 P2P | rollback | детерминизм + save/load/advance | визуальные артефакты при промахе предсказания |
| RTS, 1000+ юнитов | lockstep | бит-в-бит детерминизм | input delay (всегда) |
| Шутер с авторитетным сервером | state sync + триада | трафик, ресимуляции | peeker's advantage, смерть за укрытием |
| Гонки, техника | dead reckoning | порог расхождения | мягкие коррекции курса |
| Физический кооп / VR | distributed authority | нет защиты от читов | только доверенная компания |
| MMO | snapshot + AoI + priority | сложность приоритизации | низкая точность чужих сущностей |

---

## Чеклист

1. **Зафиксируй схему до выбора SDK.** Ответь письменно: какая из четырёх схем, кто платит задержку за достоверность попадания, какой лимит откатa и размер буфера интерполяции. Всё остальное — API.
2. **Никогда не расщепляй игровой протокол между UDP и TCP.** Отдельный HTTP к матчмейкингу/лидербордам — можно; второй игровой канал — нет.
3. **Держи состояние одного тика в пределах ~1200 байт** payload. Проверь, не упираешься ли в фрагментацию после снятия лимита 512 байт в `RpcChannel.ReliableLargeData`.
4. **Выпиши значения по умолчанию из самого ассета `NetworkProjectConfig`**, а не из документации: `Tick Rate`, `Hitbox Buffer Length In Ms`, `Hitbox Default Capacity`, `Input/State Receive Buffer Settings`, `Client Max Resims Per Frame` — ни одно из них официально не документировано.
5. **Задай лимит откатa явно.** `Hitbox Buffer Length In Ms` — это rewind cap. Единственный проверенный индустриальный ориентир — 1 секунда (Source 1). Без лимита игрок с 500 мс задержки убивает ушедшего за укрытие.
6. **Считай буфер интерполяции как 3× периода отправки** при 2–5 % потерь, плюс ~50 мс на jitter. Хочешь меньше задержку — поднимай send rate, а не обрезай буфер.
7. **Никогда не предсказывай необратимое.** Смерть и итоговое здоровье — только по подтверждению сервера; урон можно отрисовать сразу.
8. **Квантуй состояние на обеих сторонах** и перед каждым шагом симуляции, если симуляция идёт у обоих. Для физики поднимай точность (4096 шагов/м, 15 бит на компоненту кватерниона).
9. **Сглаживай только рендер, никогда симуляцию.** Ошибка затухает ~0.9 за кадр, адаптивно 0.95/0.85 по величине.
10. **Ограничь число шагов симуляции за кадр** (`Client Max Resims Per Frame` в Fusion 2.1) — иначе spiral of death.
11. **Валидируй весь ввод на сервере** независимо от prediction: prediction — про отзывчивость, авторитет — про доверие.
12. **Измеряй, а не угадывай.** Заведи воспроизводимый сценарий с искусственными latency/jitter/loss и мерь в миллисекундах: 10 мс в VALORANT сдвигали винрейт с 90/10 до 10/90.
13. **Если задача — RTS или файтинг, не бери Fusion Host Mode.** В экосистеме Photon правильный ответ — Quantum (3.0.14 на 2026-11-01).
14. **Не пытайся «починить» peeker's advantage кодом.** Его добирают дизайном карт, визуальными подсказками, штрафом к точности в движении и hit tagging.

---

## Источники

**Транспорт и надёжность**
- [gafferongames.com/post/udp_vs_tcp](https://gafferongames.com/post/udp_vs_tcp/) — head-of-line blocking, 2×RTT, 125 мс → ~1/5 с, Нейгл/`TCP_NODELAY`, «TCP induces packet loss in UDP», «Don't mix TCP and UDP!», своя контрольная сумма.
- [.../reliability_ordering_and_congestion_avoidance_over_udp](https://gafferongames.com/post/reliability_ordering_and_congestion_avoidance_over_udp/) — ack + 32-битный bitfield (всегда 33 номера), потеря = 1 с без ack, RTT EMA 10 %, good/bad mode 30/10 п/с, порог 250 мс, правила t.
- [.../packet_fragmentation_and_reassembly](https://gafferongames.com/post/packet_fragmentation_and_reassembly/) — MTU 1500 / payload ~1472, консервативные 1200 байт, 1024 байта на фрагмент, 256 фрагментов, рекомендация 2–4 фрагмента.

**Схемы синхронизации**
- [.../deterministic_lockstep](https://gafferongames.com/post/deterministic_lockstep/) — 6 бит ввода, 90 → 15 байт, «no silver bullet», playout buffer 100 мс, 4 шага за кадр.
- [ggpo.net](https://www.ggpo.net/) — rollback, input prediction, «the slower the game becomes», три требования к движку, MIT на GitHub.
- [.../snapshot_interpolation](https://gafferongames.com/post/snapshot_interpolation/) — 25 КБ/снапшот, 11.6 Мбит/с, «3X the packet send rate», 300 + ~50 мс jitter = 350 мс, 150/85 мс, Hermite vs nlerp/slerp, экстраполяция 200 мс.
- [.../snapshot_compression](https://gafferongames.com/post/snapshot_compression/) — 321 бит/куб, 17.37 Мбит/с, smallest three 29 бит, позиция 50 бит, итог 80 бит, delta 1 бит, ~15 кбит/с в статике, +25 % арифметического кодирования.
- [.../state_synchronization](https://gafferongames.com/post/state_synchronization/) — `MaxInputsPerPacket = 32`, `MaxStateUpdatesPerPacket = 64`, приоритеты 1 000 000/100/1, бюджет 256 кбит/с, квантование 4096 и 15 бит, сглаживание 0.9/0.95/0.85.
- [.../networked_physics_in_virtual_reality](https://gafferongames.com/post/networked_physics_in_virtual_reality/) — «PhysX is not deterministic», 25 шагов на кадр, distributed authority, 90 Гц / 60 апдейтов, 1/1000 см, 1 м/с депенетрации, 16 кадров покоя, <256 кбит/с.
- [.../fix_your_timestep](https://gafferongames.com/post/fix_your_timestep/) — spiral of death, accumulator, кламп 0.25 с, `alpha = accumulator / dt`, slerp.
- [github.com/id-Software/DOOM — d_ticcmd.h](https://github.com/id-Software/DOOM/blob/master/linuxdoom-1.10/d_ticcmd.h) — `ticcmd_t`, поле `consistancy`, Doom как lockstep.
- [gamedevs.org/uploads/tribes-networking-model.pdf](https://www.gamedevs.org/uploads/tribes-networking-model.pdf) — первоисточник приоритизации/ghost-репликации. ⚠️ Текст в этом прогоне извлечь не удалось, числа не приводятся.

**Триада**
- [gabrielgambetta.com/client-side-prediction-server-reconciliation.html](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html) — нумерация входов, три шага reconciliation, 200 мс без prediction, «until the server says so».
- [.../entity-interpolation.html](https://www.gabrielgambetta.com/entity-interpolation.html) — чужие в прошлом (~100 мс), dead reckoning и его границы.
- [.../lag-compensation.html](https://www.gabrielgambetta.com/lag-compensation.html) — «reconstruct the world exactly as it looked like to any client», «somewhat unfair», «the most agreeable solution».
- [gabrielgambetta.com/client-server-game-architecture.html](https://gabrielgambetta.com/client-server-game-architecture.html) — обзорная страница серии.
- [arxiv.org/abs/1008.3758](https://arxiv.org/abs/1008.3758) — обзор dead reckoning в DIS. ⚠️ Вторичный источник; стандарт IEEE 1278 не читался.

**Прод-числа**
- [riotgames.com/en/news/peeking-valorants-netcode](https://www.riotgames.com/en/news/peeking-valorants-netcode) — 128 Гц, буферизация 2/3 кадра, peeker's advantage 141 / −40 / 71 мс, 10 мс → 90/10, лимит откатa «tuned per region» без числа, дизайн-меры.
- [gist.github.com/CoolOppo/fe0586836de3fb2f90f9](https://gist.github.com/CoolOppo/fe0586836de3fb2f90f9) — дословная копия Source Multiplayer Networking: tickrate 66/30, `-tickrate` недоступен на CSS/DoD:S/TF2/L4D, `cl_interp 0.1`, `cl_updaterate 20`, формула interpolation period, «history of all recent player positions for one second», «Command Execution Time = Current Server Time - Packet Latency - Client View Interpolation». Оригинал: [developer.valvesoftware.com](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) (не открывался).
- [gdcvault.com/play/1024001](https://gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and) — метаданные доклада Timothy Ford (Blizzard, GDC 2017). Содержимое недоступно.
- [edgegap.com/blog/…overwatch-2016-netcode-architecture-rollback](https://edgegap.com/blog/game-backend-deep-dive-overwatch-2016-netcode-architecture-rollback) — ⚠️ **вторичный** источник всех чисел Overwatch (16/7 мс, RTT/2 + 1 кадр, time dilation 15.2 мс, ~46 систем / 103 компонента, bounding volume 0.5 с, 220 мс).
- [gamedeveloper.com/…rocket-league-s-physics](https://www.gamedeveloper.com/design/video-the-rocket-science-behind-i-rocket-league-s-i-physics) и [gdcvault.com/play/1024972](https://www.gdcvault.com/play/1024972/) — ⚠️ подтверждают только существование доклада Rocket League; числа не верифицированы.

**Photon на 2026-11-01**
- [doc.photonengine.com/fusion/current/getting-started/sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — 2.1.3/2390/Sep 17 2026, 2.1.2/2279/Aug 13 2026, 2.1.1/2177/Jun 30 2026, 2.0.13/2379/Sep 16 2026 — все Stable; Unity 2021.3.45, 2022.3.45, 6.0.x, 6.3.x.
- [.../release-notes/whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1) — 8–256 Гц, send rate 1/8, Shared 32/16 → потолок 32, `SetPriority`, `ReliableLargeData`, Forecast Physics, Input Delay, AOI 32 units, master client reassign (2 с), `PlayerUniqueId`, `MoveSessionToNewRoom` (10 с).
- [.../manual/network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) — Time Settings, `Client Max Resims Per Frame`, `Minimum Input Delay`, `Hitbox Default Capacity` минимум 16; **defaults не документированы**.
- [.../manual/network-simulation-loop](https://doc.photonengine.com/fusion/current/manual/network-simulation-loop) — `Simulation > Tick Rate`, `NetworkRunner.DeltaTime`, предсказание на N тиков по RTT в тиках, интерполяция прокси между снапшотами, re-simulation. Дефолт тикрейта не назван.
- [.../manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation) — «only available in Server Mode and Host Mode», 31 Hitbox на `HitboxRoot`, `HitOptions.IncludePhysX / SubtickAccuracy / IgnoreInputAuthority`.
- [doc.photonengine.com/quantum/current/quantum-intro](https://doc.photonengine.com/quantum/current/quantum-intro) — детерминированный ECS до 128 игроков, predict/rollback, input-only, целевые жанры.
- [.../quantum/v3/getting-started/initial-setup](https://doc.photonengine.com/quantum/v3/getting-started/initial-setup) — Quantum SDK 3.0.14 / Oct 05 2026 / build 2241 / Stable; Unity от 2021.3 LTS.
- [.../quantum/v3/manual/quantum-ecs/fixed-point](https://doc.photonengine.com/quantum/v3/manual/quantum-ecs/fixed-point) — `FP` Q48.16, диапазон 0.01..1000, LUT, `FP.RawValue`.
- [doc.photonengine.com/pun/current/getting-started/pun-intro](https://doc.photonengine.com/pun/current/getting-started/pun-intro) — «PUN 2 is in maintenance / LTS mode», «New projects should consider Fusion or Quantum».
- [photonengine.com/sdks](https://www.photonengine.com/sdks) — перечень продуктов; Photon Bolt на странице отсутствует, статус официально не заявлен.
- [photonengine.com/en-US/fusion/pricing](https://www.photonengine.com/en-US/fusion/pricing) — все планы и переплата за трафик $0.05 / $0.10 за ГБ.

**Детерминизм в Unity**
- [docs.unity3d.com/Packages/com.unity.physics@1.3](https://docs.unity3d.com/Packages/com.unity.physics@1.3/manual/index.html) — «provides a deterministic rigid body dynamics system and spatial query system», пакет 1.3.15.

> **Риск долговечности ссылок.** Fiedler перенёс блог на mas-bandwidth.com; на 2026-11-01 все статьи по старым URL `gafferongames.com` открываются, но фиксируйте зеркала.
>
> **Ограничение этого прогона.** Бюджет веб-поиска был исчерпан; добор делался прямыми запросами к известным URL. Непрочитанными остались: текст PDF Tribes, доклады GDC (Overwatch, Rocket League), официальный пост Blizzard про high bandwidth mode, оригинал вики Valve и статья Yahn Bernier «Latency Compensating Methods in Client/Server In-game Protocol Design and Optimization» (канонический первоисточник lag compensation), а также официальный статус Photon Bolt.

# Глава 22. Сетевое состояние, сжатие и бюджет трафика

> **TL;DR для агента**
> - **Битовых атрибутов сжатия в Fusion 2 нет.** `[Accuracy]`/`[AccuracyRange]` — это Fusion 1 ([v1 manual](https://doc.photonengine.com/fusion/v1/manual/state-transfer): «Accuracy is used by Fusion to convert floating point numbers into integers»); на [странице `[Networked]` Fusion 2](https://doc.photonengine.com/fusion/current/manual/data-transfer/networked-properties) их нет вообще. Квантуй вручную — храни `short` в сантиметрах, а не `float` в метрах.
> - Трафиком управляют **четыре рычага**, в порядке убывания эффекта: Interest Management → Send Priority (2.1+) → tick/send rate → размер состояния. Квантование типов — последнее, оно даёт проценты.
> - Fusion **уже сам выбрасывает объекты** из переполненного пакета: `Replication Features = Scheduling` (дефолт) существует ради «culling (as the data per tick limit was reached)» ([Network Project Config](https://doc.photonengine.com/fusion/current/manual/network-project-config)). Численный per-tick лимит официально не документирован.
> - Клиентская панель `FusionStatistics` собирает данные **только в DEBUG DLL** ([doc](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1)). Но продакшен-трафик мерять есть чем: Photon Dashboard → Analyze, счётчики `Traffic` и `Bandwidth/s` ([counter analytics](https://doc.photonengine.com/fusion/current/reference/counter-analytics)).
> - Бюджет считай от константы Photon: **3.0 ГБ трафика на один peak CCU в месяц** ([pricing](https://www.photonengine.com/en-US/fusion/pricing)). Overage — $0.05/ГБ или $0.10/ГБ по регионам.

---

## Зачем это знать

Курс части I учит написать работающую игру. Он не учит уложить её в бюджет — потому что в Host Mode на четырёх игроках бюджета не существует. Как только игроков становится 8–10, исходящий канал хоста (домашний интернет игрока) становится жёстким пределом, а при переходе на Photon Cloud появляется счёт за гигабайты.

Эта глава про три вопроса, на которые агент обязан уметь отвечать до того, как напишет первое `[Networked]`-свойство: **сколько весит состояние**, **чем это измерить** и **в каком порядке оптимизировать**.

### Версии: что считать «текущим» на 2026-11-01

| Ветка | Последний стабильный | Build | Дата |
|---|---|---|---|
| 2.1 | **2.1.3 Stable** | 2390 | 17.09.2026 |
| 2.0 | **2.0.13 Stable** | 2379 | 16.09.2026 |

Источник — [SDK Download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download). Самый ранний стабильный релиз ветки 2.1 в списке — **2.1.1, Build 2177, 30.06.2026**; версии «2.1.0 Stable» в списке нет. ⚠️ Распространённая в блогах дата «06.07.2026» как дата стабилизации 2.1 первоисточником не подтверждается — не повторяй её. Утверждение «ветка 2.0 ещё поддерживается» — тоже вывод, а не заявление Photon: страница просто перечисляет релизы.

Курс написан на **2.0.5** и отстаёт на всю ветку 2.1. Для этой главы 2.1 принесла три вещи, прямо относящиеся к трафику ([What's new in 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1)):

1. Tick rate — «any tick rate between 8hz and 256hz» (в Fusion 2.0 — ограниченный набор опций);
2. **Object Send Priority** — «In Fusion 2.0 and earlier every object synchronized Networked properties each send tick if possible»;
3. Снятие лимита 512 байт на RPC: «Fusion 2.1 no longer has a limit of 512 bytes for RPC size» через `RpcChannel.ReliableLargeData` (не tick-accurate, порядок доставки не сохраняется).

---

## 22.1 Модель состояния: плоский буфер слов

`[Networked]` ставится на **автосвойство**; ILWeaver вплетает в него чтение/запись в State buffer объекта. Писать геттер/сеттер руками нельзя — документация велит «leave them as auto-properties», своя логика только через обёртку другим свойством ([Networked Properties](https://doc.photonengine.com/fusion/current/manual/data-transfer/networked-properties)). Читать `[Networked]` до `Spawned()` запрещено: «Networked Properties CANNOT be accessed until the NetworkBehaviour has had `Spawned()` called» ([Network Behaviour](https://doc.photonengine.com/fusion/current/manual/network-behaviour)).

Два следствия, которые надо держать в голове всегда:

1. **Размер состояния фиксирован на момент компиляции.** `[Capacity(256)]` на `NetworkArray<int>` — это 256 элементов памяти всегда: «Memory will be allocated for this many elements, regardless of usage» ([Network Collections](https://doc.photonengine.com/fusion/current/manual/network-collections)). Без `[Capacity]` атрибут «defaults to 1» ([Networked Properties](https://doc.photonengine.com/fusion/current/manual/data-transfer/networked-properties)) — то есть `NetworkArray<T>` без Capacity это массив на один элемент.
2. **Раскладка — это де-факто версия протокола.** Добавили свойство или поменяли Capacity — билды клиента и хоста больше не совместимы.

### Разрешённые типы (полный список подтверждён мануалом)

- **Blittable-примитивы:** `byte, sbyte, short, int, long, ushort, uint, ulong, float, double`.
- **Unity-структуры:** `Vector2/3/4, Quaternion, Matrix4x4, Vector2Int, Vector3Int, BoundingSphere, Bounds, Rect, BoundsInt, RectInt, Color, Color32`.
- **Свои:** enum'ы, `System.Guid`, любые blittable-структуры, любой `INetworkStruct`.
- **Fusion-типы:** `NetworkString<IFixedStorage>, NetworkBool, Ptr, Angle, TickTimer, PlayerRef, PlayerRefSet, SceneRef, NetworkId, NetworkObjectGuid, NetworkObjectHeader, NetworkPrefabRef, NetworkPrefabId, NetworkRNG, NetworkButtons, BitSet64/128/192/256`.
- **Коллекции:** `NetworkArray<T>, NetworkDictionary<K,V>, NetworkLinkedList<T>, NetworkString<_size>`; `IFixedStorage` даёт `_2 … _512`.
- **Unsafe:** `fixed int MyArray[32]`.
- **Ссылки** на `NetworkObject`/`NetworkBehaviour` — только как свойства behaviour'а, «not valid inside INetworkStruct».
- Generic-класс `NetworkBehaviour` может иметь `[Networked]`-свойства, но свойство обобщённого типа `T` помечено invalid.

### ⚠️ Цена типов: официальной таблицы нет

**Официально не документировано.** На странице Networked Properties нет ни байтовой цены типов, ни слова «word», ни упоминаний сжатия. Рабочая модель «слово = 4 байта, свойство занимает `ceil(sizeof/4)` слов» и цифра служебного tail в 18 слов берутся из [документации Fusion Core v3](https://doc.photonengine.com/fusion-core/current/manual/objects) — это **другой продукт** (C++ SDK), и переносить числа на Unity-Fusion 2 напрямую нельзя. Точный размер состояния смотри на странице **Object** в Fusion Statistics.

Что из модели всё-таки безопасно использовать как эвристику: `Matrix4x4` в состоянии почти всегда ошибка (16 слов там, где хватит позиции + поворота), а `NetworkString<_512>` для ника игрока — расточительство, `_16`/`_32` достаточно. ⚠️ Чему именно соответствует `_X` в `NetworkString<_X>` — словам, байтам или символам — в мануале не сказано; проверяй замером.

---

## 22.2 Сжатие: что есть и чего нет

**Чего нет:** атрибутов квантования. В Fusion 1 точность задавалась «on a global level in the `NetworkProjectConfig > Accuracy Defaults`» либо «for each individual variable using the `[Accuracy]` attribute» ([v1 state transfer](https://doc.photonengine.com/fusion/v1/manual/state-transfer)). В Fusion 2 ни атрибута, ни секции конфига нет.

⚠️ **Не подтверждено первоисточником:** прямой английской формулировки «Removed [Accuracy] and [AccuracyRange]» в стабильном changelog найти не удалось — [What's new in Fusion 2](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-0) про Accuracy молчит. Подтверждение косвенное: атрибутов нет в текущем мануале. Практический вывод от этого не меняется — **любой сгенерированный код с `[Accuracy]` не скомпилируется под Fusion 2**, и это самая частая ошибка агента, обученного на текстах 2022–2023.

**Чего не стало вместе с Accuracy.** Та же страница what's-new фиксирует: «Fusion 1 provided two data transfer models: Delta Snapshots and Eventual Consistency». В Fusion 2 модель одна — Delta Snapshots как отдельная фича ушла.

**Что есть:**

1. **Delta-сжатие на уровне транспорта.** Механика описана ещё в v1: отправляется разница между последним подтверждённым клиентом состоянием и текущим; дельта растёт до подтверждения; опоздавшая дельта «simply dropped without consequences to the current state», отдельной логики восстановления после потери пакета нет. Fusion сознательно тратит часть трафика, чтобы убрать worst-case latency ([v1 state transfer](https://doc.photonengine.com/fusion/v1/manual/state-transfer)).
2. **Сжатие внутри встроенных типов — оно есть, просто вы им не управляете.** `NetworkTRSPData` хранит `Position: Vector3` и `Rotation: Quaternion` без сжатия, а `Scale` — как **`Vector3Compressed`** ([API: NetworkTRSPData](https://doc-api-test.photonengine.com/en/fusion/current/struct_fusion_1_1_network_t_r_s_p_data.html)). Там же лежат `Parent: NetworkBehaviourId`, `AreaOfInterestOverride: NetworkId` и `TeleportKey: int` — то есть NetworkTransform платит не только за трансформ. Поэтому формулировка «в Fusion 2 сжатия нет» неточна: нет **управляемого атрибутами** сжатия.
3. **Ручное квантование.** Единственный доступный способ «задать accuracy».
4. ⚠️ **Конкретный алгоритм сжатия Photon не документирует.** Предсказать, сколько байт улетит за изменение конкретного свойства, нельзя — только измерить.

---

## 22.3 Коллекции и change detection

Коллекции платят **поэлементно**: «Elements do not generate network traffic unless they change value». У `NetworkLinkedList<T>` «when items are removed, the backing array data does not shift other elements in memory» — грязной помечается только изменившаяся ячейка, поэтому для списков с частыми вставками он дешевле массива по трафику. У `NetworkDictionary` «setting values with the indexer are not valid» — только `Set(K,V)`; чтение индексатором работает. Начальные значения — через `MakeInitializer(...)`; ref-свойство через `MakeRef` снимает копирование при правке коллекции внутри `INetworkStruct` ([Network Collections](https://doc.photonengine.com/fusion/current/manual/network-collections)).

### Три механизма детекции изменений

| Механизм | Что про него говорит документация | Когда брать |
|---|---|---|
| `[OnChangedRender(nameof(M))]` | «the easiest way for handling `Render` based Change Detection» | чистый визуал |
| `NetworkBehaviourBuffer previous` + `GetPropertyReader<T>` | то же, но даёт предыдущее значение | визуал, которому нужен diff |
| `ChangeDetector` | работает в `FixedUpdateNetwork` / `Render` / `Update` | **любая геймплейная логика в Host/Server** |

Главная ловушка: «`OnChangedRender` is not called when the object is first spawned on a client» — инициализацию обязательно дублировать в `Spawned()` ([Change Detection](https://doc.photonengine.com/fusion/current/manual/data-transfer/change-detection)). ⚠️ Утверждение «OnChangedRender вызывается *только* на Render» в документации отсутствует — страница подаёт его как Render-основанный подход, но запрета не формулирует; не строй на этом логику.

Правило выбора — дословно из той же страницы: в Host/Server Mode детекция в `FixedUpdateNetwork` «is needed when the Change Detection is used to affect gameplay» (иначе client-side prediction разойдётся с сервером и rollback будет некорректен), а «in **Shared Mode** using Render based Change Detection is sufficient for most use cases». Ручной интервал («Custom») — «rarely needed except in advanced use cases», выигрыш по CPU «slightly».

```csharp
// Код из документации Fusion (Network Buffers), сокращённо.
private ChangeDetector _cd;

public override void Spawned() {
    _cd = GetChangeDetector(ChangeDetector.Source.SimulationState);
}

public override void FixedUpdateNetwork() {
    foreach (var name in _cd.DetectChanges(this, out var prev, out var curr)) {
        switch (name) {
            case nameof(Stamina):
                var reader = GetPropertyReader<float>(name);
                (var from, var to) = reader.Read(prev, curr);
                break;
        }
    }
}
```

`ChangeDetector.Source` имеет три значения: `SimulationState` («the SimulationState only exist for network objects that are included in the simulation»), `SnapshotFrom` и `SnapshotTo`. Параметр `copy` в `GetChangeDetector(source, copy)` управляет копированием baseline: при `copy=false` первый `DetectChanges` только задаёт базу и возвращает пустой энумератор. Для ручной интерполяции — `TryGetSnapshotsBuffers(out from, out to, out alpha)`; `ReinterpretState<T>(offset)` документация называет «generally not recommended» вне performance-critical кода ([Network Buffers](https://doc.photonengine.com/fusion/current/manual/advanced/network-buffers)).

### Consistency-режимы

По умолчанию — Full Consistency: «ensures all `[Networked]` properties on a single NetworkObject arrive on clients in the same tick». Eventual включается только кодом: «set the `SimulationConfig.ObjectDataConsistency` to `Eventual` in code» ([Eventual Consistency](https://doc.photonengine.com/fusion/current/manual/data-transfer/eventual-consistency)).

Точные формулировки разницы есть в API-референсе: при `Full` «the server will send all properties whose changes have not been acknowledged» (дороже по трафику, но состояние behaviour'а согласовано), при `Eventual` «the server will only send the newly changed properties ... consumes less bandwidth, but a NetworkBehaviour may have inconsistent state at times» ([API: SimulationConfig](https://doc-api-test.photonengine.com/en/fusion/current/class_fusion_1_1_simulation_config.html)). ⚠️ Популярный вывод «в один апдейт влезает больше объектов» — это чужая интерпретация, Photon так не пишет; опирайся на «less bandwidth / inconsistent state at times».

---

## 22.4 Рычаги бюджета в порядке эффективности

### 0. Scheduling — то, что Fusion делает за вас

Прежде чем что-то оптимизировать, пойми базовый механизм. `Replication Features` имеет три значения, и `Scheduling` стоит по умолчанию именно потому, что объекты **уже выбрасываются** из переполненного пакета: Scheduling описан как обработка объектов, «not replicated by the server to a client due to culling (as the data per tick limit was reached)» — сервер поднимает приоритет такого объекта для следующего исходящего тика. `None` подходит, «if the project never exceeds per tick data limits during gameplay» ([Network Project Config](https://doc.photonengine.com/fusion/current/manual/network-project-config)).

⚠️ **Численный per-tick data limit в документации не определён.** Это значит: при перерасходе вы не получите ошибку — вы получите объекты, которые приезжают через тик-два. Симптом «дальние объекты дёргаются под нагрузкой» — это чаще culling, а не потеря пакетов.

### 1. Interest Management

Требует `Replication Features = Scheduling and Interest Management` — «must be set to `Scheduling and Interest Management`». Объекту нужен компонент, производный от `NetworkTRSP`, на том же GameObject: `NetworkTransform`, `NetworkRigidbody3D`, `NetworkRigidbody2D`, `NetworkCharacterControllerPrototype`, Advanced KCC ([Interest Management](https://doc.photonengine.com/fusion/current/manual/advanced/interest-management)).

Что обязательно знать:

- **Область интереса — куб, а не сфера.** API-референс прямо: «The conversion does not use a sphere but all cells that are inside cube centered at the position», и ребро куба = `radius * 2` ([API: AreaOfInterestState](https://doc-api-test.photonengine.com/en/Fusion/current/struct_fusion_1_1_simulation_1_1_area_of_interest_state.html)).
- **Предел радиуса в Shared Mode не константа.** Это вычисляемое свойство `AreaOfInterestState.MaxSharedRadius` — «the maximum radius supported for the player interest area in shared mode based on the current cell size», причём «the radius is half the size of the cube's edges». ⚠️ Цифру 300 из сэмплов **нельзя хардкодить**; читай `MaxSharedRadius` в рантайме.
- **Shared Mode: одна область на игрока** — добавление новой «will delete the previously active one». В 2.1 cell size и grid size в Shared Mode настраиваются в `NetworkProjectConfig`; «in Fusion 2.0 and earlier it was fixed to 32 units» ([What's new in 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1)).
- **`AutoAOIOverride` для вложенных объектов.** `NetworkTransform` синхронизируется в локальном пространстве, что «can cause problems for interest management»: если родитель стоит не в (0,0,0) под обычным GameObject, локальная позиция ребёнка будет принята за глобальную. Прямая ловушка — AoI посчитается по неверной точке.
- `IInterestEnter` не вызывается на клиентах при спавне, `IInterestExit` — при деспавне; эти случаи закрывай в `Spawned()`/`Despawned()`. Не-`NetworkBehaviour` компоненты регистрируются в раннере вручную.
- `ReplicateTo(PlayerRef)` — только Server/Host («only valid on the Server peer»), **на RPC не влияет**, Object Interest проверяется раньше.

⚠️ Числа по cell size (917 504 м³ при 32 м против 327 680 м³ при 16 м) — из [документации addon'а Interest Management](https://doc.photonengine.com/fusion/v2/addons/interest-management/architecture), а не основного мануала, и привязаны к конкретной конфигурации карты. Не выдавай их за общие константы Fusion. Запомни только принцип: сетка всегда больше заданной формы, мельче ячейка — точнее форма и дороже CPU. Держи в голове и то, что Interest Management существует в двух видах: встроенный (Replication Features + `AddPlayerAreaOfInterest`) и отдельный addon со своей документацией.

### 2. Send Priority (только 2.1+)

`NetworkObject.SetPriority(PlayerRef player, int priority)` и `ClearPriority(PlayerRef player)`. Default — `1`, объект уходит каждый send-тик; `2` = каждый второй, `3` = каждый третий. Доступно в Host, Server и Shared Mode начиная с Fusion 2.1; в Shared Mode валиден только `NetworkRunner.LocalPlayer`. Пример из документации: `NearDistance = 32f`, `FarDistance = 128f` → приоритеты 1 / 2 / 4. После Host Migration «priorities must be re-applied on the new Host after a migration occurs» — через `IAfterHostMigration` ([Send Priority](https://doc.photonengine.com/fusion/current/manual/advanced/send-priority), [What's new in 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1)).

> **🚩 Исправление распространённой ошибки: `ObjectInactivityThreshold` в релизе ещё нет.**
> Страница Send Priority прямо говорит: «`ObjectInactivityThreshold` is available starting with Fusion 2.1.4». В списке стабильных SDK на 2026-11-01 последний — **2.1.3, Build 2390**; версии 2.1.4 там нет. То есть параметра **нет ни в одном выпущенном стабильном SDK**, и код, который его трогает, у агента не соберётся.
> Для 2.1.0–2.1.3 действует старая рекомендация той же страницы: снижать частоту апдейтов до ~4 Гц и ниже **только для объектов вне view frustum**; для видимых объектов так делать не рекомендуется.
> Когда 2.1.4 выйдет: default `0.251` с, путь `NetworkProjectConfig.Simulation.ObjectInactivityThreshold`, формула `2 * priority / send rate` (priority здесь — максимальный среди движущихся объектов, порог глобальный), если расчёт ниже дефолта — оставить дефолт. В инспекторе не выставлен, задаётся кодом до старта раннера: `NetworkProjectConfig.Global.Simulation.ObjectInactivityThreshold = 0.5f;`. Проверка — trace-канал `SendPriority` (поле Trace Channels в `NetworkProjectConfig`), лог вида «ObjectInactivityThreshold exceeded: 0.3125 >= 0.251».

Альтернативный штатный способ погасить рывок при разрыве апдейтов — `Teleport()`, «the default Teleport implementation for NetworkTRSP derived classes» ([API: NetworkTRSP](https://doc-api-test.photonengine.com/en/fusion/current/class_fusion_1_1_network_t_r_s_p.html)); в состоянии ему соответствует поле `TeleportKey: int`. ⚠️ Формулировку «suspends interpolation on this peer and all remote peers» подтвердить первоисточником не удалось.

### 3. Tick rate и send rate

| Параметр | Fusion 2.0 | Fusion 2.1 |
|---|---|---|
| Client & Server Tick Rate | ограниченный набор опций | любое значение **8–256** |
| Send Rate | фиксированный | интервал **1–8**, должен делить tick rate нацело (1 = каждый тик) |
| Shared Mode | tick 32 / send 16, locked | оба ≤ 32, ниже — свободно |

Источники: [Network Project Config](https://doc.photonengine.com/fusion/current/manual/network-project-config), [What's new in 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1). ⚠️ **Дефолты tick/send rate в мануале не названы.** Для ориентира — реальные значения из проекта курса (гл. [02](02-chapter.md)): `TickRateSelection: { Client: 30, ClientSendIndex: 1, ServerSendIndex: 1 }`.

Практика: поднять tick rate симуляции и увеличить send-интервал дешевле, чем гнать каждый тик по сети. Удвоение send rate без изменений состояния примерно удваивает исходящий трафик.

### 4. Размер состояния и инпут

Меньше свойств, меньше Capacity, узкие типы, ручное квантование. Отдельно про **входящий** трафик: `SimulationConfig.InputDataWordCount` («Input Data Word Count») задаёт размер структуры ввода в словах, и этот объём улетает от каждого клиента каждый тик — при 32–256 Гц это не мелочь. В проекте курса он равен `0` (гл. [02](02-chapter.md)) — то есть инпут не раздут. `Input Transfer Mode`: мануал перечисляет `Redundancy` («send delta compressed and redundant input, used for most games») и `Latest State` («only send the latest input state», для крупных структур вроде VR); API-референс показывает ещё третий — `RedundancyUncompressed`. ⚠️ Дефолт не документирован.

`PlayerCount` в мануале дан диапазоном 1..255 без дефолта; API-референс называет **10** — «the default number of players allowed to join a game instance» ([API: SimulationConfig](https://doc-api-test.photonengine.com/en/fusion/current/class_fusion_1_1_simulation_config.html)). Для Host Mode это прямой множитель исходящего трафика хоста.

### 5. Data Streaming вместо состояния

Большие блобы не должны ехать ни состоянием, ни обычным RPC: `runner.SendReliableDataToPlayer(playerRef, key, largeData)` / `runner.SendReliableDataToServer(key, largeData)`, ключ — 16-байтный `ReliableKey.FromInts(42, 0, 0, 0)` (страница упоминает и вариант «from two ulongs», ⚠️ но метода не называет). Приём — `OnReliableDataReceived`, и подпись **версионно-зависима**: в 2.0.x параметр `ArraySegment<byte> data`, с 2.1 — `ReadOnlySpan<byte>`. Прогресс — `OnReliableDataProgress(runner, player, key, float progress)`. Коллбэк приёма срабатывает после полной сборки ([Data Streaming](https://doc.photonengine.com/fusion/current/manual/data-transfer/data-streaming)). ⚠️ Размеры фрагментов, лимиты и пропускная способность не документированы; MTU и дробление состояния по пакетам в мануале Fusion 2 не описаны вообще.

---

## 22.5 Измерение: два инструмента, не один

### Клиент: Fusion Statistics (только DEBUG)

«Fusion Statistics will only collect statistics data when using the DEBUG DLL» — причина названа прямо: не трогать производительность RELEASE DLL ([Fusion Statistics 2.1](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1)).

Включение: компонент `FusionStatistics` «on a `NetworkRunner` object before it was started», кнопка в Network Runner Controls, либо `NetworkRunner.SetupStatistics()`. ⚠️ Расхождение документации: страница 2.0 требует жёстче — «the `FusionStatistics` component must be on the same game object as the `NetworkRunner`»; страница 2.1 этого не повторяет. Безопаснее следовать строгому варианту.

Страницы, релевантные бюджету:

| Страница | Метрики |
|---|---|
| **Network** | RTT, In/Out Bandwidth (байты), In/Out Packets, Input In/Out Bandwidth |
| **Simulation** | Forward Ticks, Re-simulation Ticks, Objects Update In/Out |
| **Object** | in/out bandwidth конкретного объекта; на 2.1 объекты добавляются кнопкой **Add Object**, отображаются первые 10 + поиск |
| **Memory** | общая память и два внутренних аллокатора; «if either of these radial charts reaches 100%, Fusion will not have any more memory available to work with» — это hard fail, а не предупреждение |
| **Behaviour** | count и время FixedUpdateNetwork/Render, первые 10 |
| **Lag Compensation** | процент занятых хитбоксов и время внутренних апдейтов |

В названии графика `(U)` = за апдейт, `(S)` = за секунду, клик переключает; RTT залочен на per-update. ⚠️ Компонент `NetworkObjectStatistics` и раскраска по трём порогам (зелёный → жёлтый → оранжевый → красный, настройка в редакторе или `FusionStatistics.SetStatsCustomConfig()`) описаны **только на странице [Fusion Statistics 2.0](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.0)** — на странице 2.1 их нет; лимит в 10 объектов, наоборот, назван только на 2.1.

Конфиг: `Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsDefaultConfig.asset`.

```csharp
// Код из документации Fusion Statistics 2.1.
public void StatisticsCollectData() {
    if (Runner.TryGetFusionStatistics(out var statisticsManager)) {
        var rtt          = statisticsManager.SimulationSnapshot.Stats[FusionStatType.RoundTripTime];
        var forwardTicks = statisticsManager.SimulationSnapshot.Stats[FusionStatType.ForwardTicks];
        var memFree      = statisticsManager.MemorySnapshot
                              .GeneralAllocatorMemorySnapshot.TotalBytesFree;
    }
}
```

⚠️ Полный список членов `FusionStatType` ни в мануале, ни в доступных страницах API-референса не опубликован — смотри в установленном SDK.

### Продакшен: Photon Dashboard → Analyze

Вывод «в release-билде трафик измерить нечем» **неверен** — ограничение DEBUG касается только клиентской панели. Серверная сторона считает трафик в продакшене, и именно по нему выставляется счёт ([Counter Analytics](https://doc.photonengine.com/fusion/current/reference/counter-analytics)):

- **Traffic** — «accumulated data volume for the loaded timespan», включая Master Servers, Game Servers и WebHooks;
- **Bandwidth/s** — суммарные байты/с, отданные game-серверами;
- **CCU**, **CCU Peaks** («peak CCU for the given timespan»), **Rooms**, **CCU per Room**, **Msg/s per Room**;
- **Master / Gameserver / WebHooks Bandwidth/s per Peer**, **Disconnects/s per Peer**.

Два ограничения для аудита бюджета: «free apps are restricted to the 24 hours timespan and the "All Regions" graphs» — на Development-плане месячный разрез не снять; а **Counter API** «is available with Premium Cloud, Circle and Enterprise plans» и требует support-запроса из дашборда.

---

## 22.6 Бюджет и деньги: как считать

Прайс Fusion Public Cloud на 2026-11-01 ([pricing](https://www.photonengine.com/en-US/fusion/pricing)):

| План | Цена | CCU | Трафик/мес |
|---|---|---|---|
| Development | бесплатно | 20 (без burst) | 60.0 ГБ |
| Free 100 | бесплатно (одно приложение, стекается до 200 CCU) | 100 (без burst) | 0.3 ТБ |
| 100 one-time | $95 единожды (~$8/мес за 12 мес) | 100 (без burst) | 0.3 ТБ |
| 500 | $125/мес | 500 (burst) | 1.5 ТБ |
| 1000 | $250/мес | 1 000 (burst) | 3.0 ТБ |
| 2000 | $500/мес | 2 000 (burst) | 6.0 ТБ |

Premium Cloud — **$0.50 за CCU** по месячному потреблению, автоскейл «for up to 50,000 CCU», минимум **$1 000/мес** (= 2 000 CCU + 6.0 ТБ). Ключевая проектная константа: «**Includes 3.0 GB Traffic per Peak CCU per Month**». Overage: **$0.05/ГБ** (Europe, Canada, RU/RUE, USA East, USA West) и **$0.10/ГБ** (Asia, Australia, China, India, Japan, South Africa, South America, South Korea, Turkey, UAE). «All incoming and outgoing messages to the Photon Cloud servers count towards the traffic limit» — отдельного лимита по числу сообщений на странице нет.

**Ориентир (моя арифметика, не цифра Photon):** 3 ГБ / 2 592 000 с ≈ **1.16 КБ/с на CCU** в среднем по месяцу. Лимит месячный, поэтому активный игрок может потреблять больше, пока онлайн.

**Процедура расчёта:**

1. Оцени состояние одного реплицируемого объекта — не по догадке, а по странице **Object** в Fusion Statistics (официальной таблицы байт на тип нет, см. 22.1).
2. Оцени **долю изменяющихся** данных за тик: платишь за изменения, не за размер.
3. `out_bytes/s ≈ Σ(изменившиеся_байты × send_rate / priority) по объектам в interest игрока` + накладные пакета.
4. Умножь на число клиентов. В Host Mode исходящий трафик хоста растёт линейно — это и есть предел Host Mode.
5. Сверь с замером: Network → Out Bandwidth в режиме `(S)`, плюс Object по самым дорогим префабам.
6. Переведи в месяц и сравни с `3 ГБ × peak CCU`. Задним числом сверь по Dashboard → Traffic, выбрав «All Regions» и месячный диапазон (нужен платный план).

---

## Чего по этой теме нет в учебном курсе

Курс — Fusion **2.0.5**, Unity **2022.3.42f1**, целиком Host Mode. Что покрыто частично и что не покрыто вовсе:

| Тема | Состояние в курсе |
|---|---|
| Interest Management | **покрыто хорошо** — гл. [08](08-chapter.md) целиком: три уровня фильтрации, `ReplicationFeatures = 3`, переобъявление формы каждый тик. Нет: куб vs сфера, `MaxSharedRadius`, `AutoAOIOverride`, настройка cell/grid size в 2.1 |
| `[OnChangedRender]` | есть в гл. [02](02-chapter.md), [03](03-chapter.md), [04](04-chapter.md), [07](07-chapter.md). Но курс учит **только** этому — то есть по построению учит детектировать на Render, что для геймплейной логики в Host Mode некорректно |
| `ChangeDetector` | упомянут по имени в гл. [01](01-chapter.md), [04](04-chapter.md), [05](05-chapter.md), [15](15-chapter.md) — **без API**. `GetChangeDetector`, три `Source`, `DetectChanges` с буферами отсутствуют |
| `NetworkBehaviourBuffer`, `GetPropertyReader`, `TryGetSnapshotsBuffers`, `NetworkBehaviourWithState<T>` | **нет совсем** |
| Fusion Statistics | компонент виден в сцене гл. [08](08-chapter.md) с `_statsEnabled: 0` — то есть **выключен и не разобран**. Ни страниц панели, ни ограничения DEBUG DLL, ни `TryGetFusionStatistics` |
| Photon Dashboard / Analyze | **нет совсем** — продакшен-измерения трафика в курсе не существует |
| Экономика (прайс, 3 ГБ на peak CCU, overage) | **нет совсем** — непонятно, что считать «много трафика» |
| Send Priority | физически не существовал в 2.0.5 — фича 2.1 |
| Tick rate 8–256, send-интервал 1–8 | в 2.0.5 набор опций был ограничен; курс зафиксировал `Client: 30` (гл. [02](02-chapter.md)) и рычаг не описывает |
| `ObjectDataConsistency = Eventual`, `InputTransferMode`, Heap Page Shift/Count | значения из конфига перечислены в гл. [02](02-chapter.md) (`Heap: { PageShift: 15, PageCount: 256 }`), но как рычаги трафика не разобраны |
| Data Streaming, `RpcChannel.ReliableLargeData` | **нет совсем** |
| Scheduling / per-tick culling | гл. [02](02-chapter.md) фиксирует `ReplicationFeatures: 1` = `Scheduling`, но **зачем** — не объяснено |
| `[Accuracy]`/`[AccuracyRange]` | в курсе отсутствуют корректно (их нет в Fusion 2), но и предупреждения о старой интуиции нет |

Итог: главы части I дают написать работающую игру, но не дают уложить её в бюджет. Эту главу читай как «измерь → посчитай → оптимизируй в правильном порядке», и первым делом включи `FusionStatistics` в debug-билде.

---

## Решение: что брать и когда

| Симптом / задача | Решение | Проверка |
|---|---|---|
| Не знаю, сколько трачу | `FusionStatistics` в **debug-билде**, страница Network, `(S)` | Out Bandwidth в байтах/с |
| Нужен продакшен-трафик | Dashboard → Analyze → `Traffic` / `Bandwidth/s` | для месячного разреза нужен платный план |
| Дорогой префаб неизвестен | страница **Object** (первые 10) | per-object in/out |
| Перерасход, мир большой | **Interest Management** (`ReplicationFeatures = 3` + `NetworkTRSP`) | режет объекты целиком |
| Перерасход, объекты нужны все | **Send Priority** по дистанции (2.1+) | priority 1/2/4 как в доке |
| Перерасход после Priority | **send rate**: интервал 2–8 | должен делить tick rate нацело |
| Много объектов, допустима рассинхронизация свойств | `ObjectDataConsistency = Eventual` | нельзя читать «позиция+скорость» как согласованную пару |
| Состояние раздуто | узкие типы, меньше Capacity, ручное квантование | даёт проценты, не кратность |
| Нужно передать блоб (карта, аватар) | Data Streaming; с 2.1 — `RpcChannel.ReliableLargeData` | не tick-accurate, порядок не гарантирован |
| Дальние объекты дёргаются | до 2.1.4 — снижать частоту **только вне view frustum**; `Teleport()` при разрывах | trace-канал `SendPriority` |
| Memory-диаграмма у 100 % | поднять `Heap: Page Shift` / `Page Count` | это hard fail, не warning |
| Флаг теряется между сериализациями | `int` вместо `bool` — «often recommended to use an int instead of bool for use cases where a change of the value might go undetected» | — |
| Нужна ссылка на объект | синхронизируй `NetworkId`, а не `[Networked] NetworkObject` | геттер ссылки не различает явный null и нерезолвящийся id |

---

## Чеклист

1. Поставь `FusionStatistics` на GameObject раннера (тот же, что и `NetworkRunner`) **до старта** и убедись, что собираешь данные в **debug-билде** — в release их нет.
2. Зафиксируй базовую линию: Network → Out Bandwidth в режиме `(S)`, Input Out Bandwidth отдельно, и Object по 3–5 самым массовым префабам.
3. Посчитай допустимый бюджет: `3 ГБ × peak CCU` в месяц, ориентир ~1.16 КБ/с на CCU в среднем.
4. Проверь `Replication Features`. Если в проекте бывают пиковые тики — оставь `Scheduling` (дефолт); для AoI поставь `Scheduling and Interest Management`.
5. Перед любой микрооптимизацией включи Interest Management: `NetworkTRSP`-компонент на объекте, `AddPlayerAreaOfInterest`, `AutoAOIOverride` для вложенных объектов. Помни, что область — куб с ребром `radius * 2`.
6. В Shared Mode читай `AreaOfInterestState.MaxSharedRadius` в рантайме — не хардкодь радиус.
7. На 2.1+ навесь Send Priority по дистанции (1 / 2 / 4) и прогони trace-канал `SendPriority`. Не используй `ObjectInactivityThreshold` — его нет в стабильных SDK до 2.1.4; вместо него до 2.1.4 понижай частоту только для объектов вне view frustum.
8. После Host Migration переустанови приоритеты из `IAfterHostMigration`.
9. Только после этого крути send rate (интервал 1–8, должен делить tick rate нацело) и `ObjectDataConsistency = Eventual`.
10. Пройди по всем `[Networked]`: убери `Matrix4x4`, уменьши `NetworkString<_X>` до `_16`/`_32`, выставь `[Capacity]` по факту, а не «с запасом» — память выделяется на всю Capacity всегда.
11. Квантуй вручную: `short` в сантиметрах вместо `float` в метрах. Никогда не пиши `[Accuracy]` — в Fusion 2 его нет.
12. Любой блоб больше нескольких сотен байт отправляй через Data Streaming, а не состоянием и не обычным RPC. Учти версионную разницу подписи `OnReliableDataReceived` (`ArraySegment<byte>` до 2.1, `ReadOnlySpan<byte>` с 2.1).
13. Детекцию изменений, влияющую на геймплей, делай в `FixedUpdateNetwork` через `ChangeDetector`; `[OnChangedRender]` оставь визуалу и обязательно дублируй инициализацию в `Spawned()`.
14. Перед релизом сверь расчёт с Dashboard → Analyze за месяц; на бесплатном плане это недоступно (24 часа + All Regions).
15. Следи за Memory-страницей: 100 % на радиальной диаграмме — память кончилась, а не «почти».

---

## Источники

**Основной мануал Fusion 2**
- [Networked Properties](https://doc.photonengine.com/fusion/current/manual/data-transfer/networked-properties) — полный список разрешённых типов, `IFixedStorage` `_2…_512`, `[Capacity]` «defaults to 1», запрет ручных геттеров, рекомендация `int` вместо `bool`, отсутствие Accuracy и байтовых таблиц.
- [Network Collections](https://doc.photonengine.com/fusion/current/manual/network-collections) — «memory will be allocated for this many elements, regardless of usage», поэлементная дельта, поведение `NetworkLinkedList`, запрет индексатора у `NetworkDictionary`, `MakeInitializer`/`MakeRef`.
- [Change Detection](https://doc.photonengine.com/fusion/current/manual/data-transfer/change-detection) — `OnChangedRender` не вызывается при первом спавне, правило Host/Server → `FixedUpdateNetwork` vs Shared → Render, «Custom» интервал.
- [Network Buffers](https://doc.photonengine.com/fusion/current/manual/advanced/network-buffers) — `GetChangeDetector(source, copy)`, три `ChangeDetector.Source`, `DetectChanges`, `GetPropertyReader<T>`, `TryGetSnapshotsBuffers`, `ReinterpretState<T>`, `NetworkBehaviourBufferInterpolator`, `NetworkBehaviourWithState<T>`.
- [Eventual Consistency](https://doc.photonengine.com/fusion/current/manual/data-transfer/eventual-consistency) — Full по умолчанию, включение Eventual только кодом.
- [Data Transfer (обзор)](https://doc.photonengine.com/fusion/current/manual/data-transfer/data-transfer) — четыре способа передачи; подтверждение, что компрессия, per-tick лимиты и MTU там не описаны.
- [Data Streaming](https://doc.photonengine.com/fusion/current/manual/data-transfer/data-streaming) — `SendReliableDataToPlayer`/`ToServer`, `ReliableKey.FromInts`, версионная разница `ArraySegment<byte>` → `ReadOnlySpan<byte>`, `OnReliableDataProgress`.
- [Network Project Config](https://doc.photonengine.com/fusion/current/manual/network-project-config) — Replication Features и culling по per-tick data limit, tick rate 8–256, send-интервал 1–8, Shared Mode ≤ 32, Player Count 1..255, Heap Page Shift 1 КБ–256 КБ / Page Count 16–4096, Input Transfer Mode, Trace Channels.
- [Network Behaviour](https://doc.photonengine.com/fusion/current/manual/network-behaviour) — «Networked Properties CANNOT be accessed until ... `Spawned()`», `FixedUpdateNetwork` вместо `FixedUpdate`, `Render` вместо `Update`.
- [Interest Management](https://doc.photonengine.com/fusion/current/manual/advanced/interest-management) — требование `Scheduling and Interest Management`, список `NetworkTRSP`-компонентов, `AddPlayerAreaOfInterest`, одна AoI на игрока в Shared Mode, `AutoAOIOverride`, `SetPlayerAlwaysInterested`, `ReplicateTo` и его ограничения, поведение `IInterestEnter`/`IInterestExit`.
- [Send Priority](https://doc.photonengine.com/fusion/current/manual/advanced/send-priority) — `SetPriority`/`ClearPriority`, default 1, пример 32/128 юнитов, **`ObjectInactivityThreshold` доступен начиная с 2.1.4**, его default 0.251 с, формула и trace-канал, рекомендация до 2.1.4 (≤4 Гц только вне view frustum).
- [Fusion Statistics 2.1](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1) — «only collect statistics data when using the DEBUG DLL», способы включения, метрики страниц Network/Simulation/Object/Memory/Behaviour/Lag Compensation, `(U)`/`(S)`, «Add Object» и первые 10, 100 % = память кончилась, путь конфига, пример `TryGetFusionStatistics`.
- [Fusion Statistics 2.0](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.0) — требование «must be on the same game object as the `NetworkRunner`», компонент `NetworkObjectStatistics`, три порога раскраски и `SetStatsCustomConfig()`, `StatisticsWorldAnchor`.
- [Counter Analytics](https://doc.photonengine.com/fusion/current/reference/counter-analytics) — счётчики Dashboard (`Traffic`, `Bandwidth/s`, CCU, CCU Peaks, Rooms, CCU per Room, Msg/s per Room, Bandwidth/s per Peer, Disconnects/s per Peer), ограничение бесплатных приложений (24 ч + All Regions), Counter API только на Premium/Circle/Enterprise.

**Релизы и версии**
- [SDK Download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download) — 2.1.3 Build 2390 (17.09.2026), 2.1.2 Build 2279 (13.08.2026), 2.1.1 Build 2177 (30.06.2026); 2.0.13 Build 2379 (16.09.2026); 2.1.4 в списке отсутствует.
- [What's new in Fusion 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1) — Object Send Priority, снятие лимита 512 байт через `RpcChannel.ReliableLargeData`, tick rate 8–256, Shared Mode 32/16 → настраиваемые ≤ 32, cell/grid size в Shared Mode (было фиксировано 32 юнита), переустановка приоритетов через `IAfterHostMigration`.
- [What's new in Fusion 2](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-0) — «Fusion 1 provided two data transfer models: Delta Snapshots and Eventual Consistency»; подтверждение, что Accuracy там не упоминается.

**Fusion 1 (для контраста)**
- [State Transfer (v1)](https://doc.photonengine.com/fusion/v1/manual/state-transfer) — `NetworkProjectConfig > Accuracy Defaults` и атрибут `[Accuracy]`, «Accuracy is used by Fusion to convert floating point numbers into integers»; механика delta-сжатия и отбрасывания опоздавших дельт.

**API-референс (зеркало doc-api-test; основной хост doc-api не читается через WebFetch)**
- [SimulationConfig](https://doc-api-test.photonengine.com/en/fusion/current/class_fusion_1_1_simulation_config.html) — точные описания `Full`/`Eventual`, `PlayerCount` default 10, `InputDataWordCount`, три значения Input Transfer Mode.
- [AreaOfInterestState](https://doc-api-test.photonengine.com/en/Fusion/current/struct_fusion_1_1_simulation_1_1_area_of_interest_state.html) — `MaxSharedRadius` как вычисляемое свойство, «the area is a cube not a sphere», «the radius is half the size of the cube's edges», `GetCellSize()`.
- [NetworkTRSPData](https://doc-api-test.photonengine.com/en/fusion/current/struct_fusion_1_1_network_t_r_s_p_data.html) — `Position: Vector3`, `Rotation: Quaternion`, **`Scale: Vector3Compressed`**, `Parent: NetworkBehaviourId`, `AreaOfInterestOverride: NetworkId`, `TeleportKey: int`.
- [NetworkTRSP](https://doc-api-test.photonengine.com/en/fusion/current/class_fusion_1_1_network_t_r_s_p.html) — `Teleport()` как «the default Teleport implementation for NetworkTRSP derived classes».

**Прочее**
- [Fusion Pricing](https://www.photonengine.com/en-US/fusion/pricing) — все планы, «Includes 3.0 GB Traffic per Peak CCU per Month», минимум $1 000/мес, overage $0.05 / $0.10 за ГБ по регионам, «all incoming and outgoing messages ... count towards the traffic limit».
- [Interest Management addon: Architecture](https://doc.photonengine.com/fusion/v2/addons/interest-management/architecture) — числа по cell size (917 504 м³ / 327 680 м³); ⚠️ это документация **addon'а**, а не основного мануала, и числа привязаны к конкретной конфигурации карты.
- [Fusion Core v3: Objects](https://doc.photonengine.com/fusion-core/current/manual/objects) — модель `Word = int32_t`, `float` = 1 слово, ObjectTail 18 слов; ⚠️ **другой продукт** (C++ SDK), на Unity-Fusion 2 числа не переносятся.

**Не удалось подтвердить / данные расходятся**
- Прямая английская формулировка об удалении `[Accuracy]`/`[AccuracyRange]` в стабильном changelog Fusion 2 — не найдена; подтверждение только косвенное (атрибутов нет в текущем мануале).
- Формулировка «`Teleport()` suspends interpolation on this peer and all remote peers» — первоисточником не подтверждена.
- `MaxObjectDestroysSentPerPacket` — в доступных страницах API-референса не найден; существование не подтверждено.
- Антипаттерн «менять `[Networked]` в `Update()` ломает prediction, а в Shared Mode это допустимо» — страница Network Behaviour такого не утверждает; опирайся на сформулированное правило Change Detection (геймплей → `FixedUpdateNetwork`).
- Численный per-tick data limit, размеры фрагментов Data Streaming, MTU, дефолты tick/send rate, Heap и Input Transfer Mode, полный список `FusionStatType`, байтовая цена `NetworkString<_X>` — **официально не документированы**.
- Веб-поиск в этой сессии был исчерпан после первых запросов; проверка велась прямым чтением страниц документации через WebFetch, блог-пост с бенчмарками Photon (`blog.photonengine.com/photon-fusion-benchmark/`) недоступен.

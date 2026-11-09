# Глава 23. Физика и детерминизм

> **TL;DR для агента**
>
> 1. **Fusion — это state sync, а не детерминированный lockstep.** Клиент предсказывает вперёд, а при получении снапшота ресимулирует от тика снапшота до текущего, прогоняя цикл симуляции [«once for every tick between the the received state»](https://doc.photonengine.com/fusion/current/manual/network-simulation-loop). Битово одинакового результата на разных машинах Fusion не обещает. Нужен настоящий кросс-платформенный детерминизм — это **Quantum**, с фиксированной точкой `Q48.16` вместо float ([источник](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/fixed-point)).
> 2. **Версия, с которой надо начинать в 2026 году, — Fusion 2.1.3 Stable, build 2390, 17.09.2026**; линия 2.0.x жива как legacy (последняя — 2.0.13, build 2379, 16.09.2026) ([release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1), [release notes 2.0](https://doc.photonengine.com/fusion/current/getting-started/release-notes)). Курс части I написан на 2.0.5 — это восемь минорных версий назад.
> 3. **В Fusion 2.1 три стратегии сетевой физики: Forecast, Forecast Disabled, Physics Addon.** Physics Addon — «Only for Client Host or Dedicated Server modes» ([addon overview](https://doc.photonengine.com/fusion/current/addons/physics/overview)); в Shared Mode единственный способ дать интерактивную физику — Forecast ([manual/physics](https://doc.photonengine.com/fusion/current/manual/physics)).
> 4. **Код физики Forecast-объектов пишется в `FixedUpdate()`, а не в `FixedUpdateNetwork()`**, и Unity-режим симуляции физики обязан быть `FixedUpdate` ([physics-forecast](https://doc.photonengine.com/fusion/current/manual/physics/physics-forecast)).
> 5. **Для персонажа — никогда `NetworkRigidbody`.** Берите Simple KCC (CCD из коробки, step detection, ground snapping) или Advanced KCC, если нужны движущиеся платформы и render prediction без ~одного тика input lag ([simple-kcc](https://doc.photonengine.com/fusion/current/addons/simple-kcc), [render-behavior](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/render-behavior)).

---

## Зачем это знать

Курс части I ([гл. 03](03-chapter.md) «Кинематика и физика объектов», [гл. 05](05-chapter.md) «Интерполяция и снапшоты») показывает ровно одну конфигурацию сетевой физики: `Fusion.Addons.Physics` с `NetworkRigidbody3D` в Host Mode на Fusion 2.0.5. Это рабочая, но очень узкая точка в пространстве решений. На практике агент, пишущий сетевой код, упирается в три вопроса, на которые курс не отвечает:

- «Почему объект, который на хосте лежит ровно, у клиента дрожит / проваливается / отскакивает?»
- «Почему при 150 мс пинга клиент теряет кадры, хотя профайлер показывает, что виновата физика?»
- «Можно ли вообще сделать детерминированный реплей или rollback-файтинг на Fusion?»

Все три — про одну развилку: **Fusion не воспроизводит физику одинаково, он её пересчитывает и подтягивает к авторитету.** Из этого следуют и три стратегии физики, и ограничения тик-рейта, и необходимость KCC вместо Rigidbody для персонажа.

---

## 1. Что Fusion гарантирует, а что нет

Страница [`manual/network-simulation-loop`](https://doc.photonengine.com/fusion/current/manual/network-simulation-loop) описывает цикл так: клиент предсказывает будущее состояние сервера («Prediction involves the client predicting the future server state based on its current local information»), сервер требует от клиента быть минимум на 2 тика впереди («the server tells it to always be at least 2 ticks ahead of the server state»), а при расхождении клиент ресимулирует от тика серверного снапшота до текущего тика, прогоняя цикл один раз на каждый тик в разрыве. Шаг времени фиксирован и берётся из `NetworkRunner.DeltaTime`, «regardless of how much actual time actually passed».

Что из этого следует:

| Свойство | Гарантирует ли Fusion |
|---|---|
| Один и тот же тик, пересчитанный на той же машине, даёт тот же результат | Да — на этом держится ресимуляция |
| Авторитет и клиент сойдутся в итоговом состоянии | Да — через перезапись снапшотом и ресимуляцию |
| Два клиента на разных CPU/платформах дадут битово одинаковый результат | **Нет** |
| Детерминированный реплей по одному только логу ввода | **Нет** |

Правило «не используй `Random` и `Time.deltaTime` в `FixedUpdateNetwork()`» (курс даёт его в [гл. 02](02-chapter.md) и [гл. 03](03-chapter.md)) — это требование **повторяемости одного тика на одной машине**, а не требование кросс-платформенного детерминизма. ⚠️ Отмечу честно: страница `network-simulation-loop` про `Random` и `Time.deltaTime` не говорит вообще — этот вывод сделан из механики ресимуляции, а не процитирован.

Если нужен настоящий детерминизм — это другой продукт. Quantum использует фиксированную точку: «The fixed-point type implemented in Quantum is `Q48.16`», поверх одного `long`; конвертация из float внутри симуляции «should **never** be done inside the simulation» и «will cause desyncs 100% of the time»; `FP.UseableMax` = 32767.9999847412, `FP.UseableMin` = −32768, а приемлемая точность — в диапазоне `(0.01..1000)` ([fixed-point](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/fixed-point)).

⚠️ **Не подтверждено первоисточником:** распространённое утверждение «Unity PhysX детерминирован только в пределах одной сессии и одного порядка создания тел, без гарантий между платформами». Официальной страницы Unity с такой формулировкой найти не удалось; тезис живёт в форумах и неофициальной вики. Для главы важно не это, а то, что документация Photon сама не обещает детерминизма физики в Fusion и отправляет за ним в Quantum.

---

## 2. Версионная рамка на 2026-11-01

Это первое, что агент обязан проверить, прежде чем копировать код из курса.

| Что | Версия | Build | Дата | Источник |
|---|---|---|---|---|
| Fusion, актуальная линия | **2.1.3 Stable** | 2390 | 17.09.2026 | [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) |
| Fusion, предыдущие 2.1 Stable | 2.1.2 / 2.1.1 | 2279 / 2177 | 13.08.2026 / 30.06.2026 | там же |
| Fusion, legacy линия | 2.0.13 Stable | 2379 | 16.09.2026 | [release-notes](https://doc.photonengine.com/fusion/current/getting-started/release-notes) |
| Fusion в курсе части I | 2.0.5 | 920 | 12.02.2025 | [гл. 01](01-chapter.md) |
| Physics Addon для 2.1.x | 2.1.3 / 2.1.2 / 2.1.1 | — | 22.10.2026 / 22.07.2026 / 03.07.2026 | [physics-addon-2.1](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.1) |
| Physics Addon для 2.0.x | **только 2.0.6** | — | 03.07.2026 | [physics-addon-2.0](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.0) |
| Simple KCC addon | 2.1.1 / 2.1.0 / 2.0.15 | — | 02.09.2026 / 17.07.2026 / 20.05.2026 | [simple-kcc](https://doc.photonengine.com/fusion/current/addons/simple-kcc) |
| Simple KCC sample | 2.1.1 / 2.1.0 / 2.0.18 | — | 02.09.2026 / 17.07.2026 / 03.11.2025 | там же |

Аддоны **не взаимозаменяемы**: страница 2.1 прямо пишет «This Addon is for Fusion 2.1.x. It does not support Fusion 2.0.x», а страница 2.0 — что она для 2.0.x и не поддерживает 2.1.x. Порядок апгрейда задан документацией: «Projects should be upgraded to the latest 2.0.x SDK before being upgraded to 2.1.x», и после апгрейда «All prefabs and scenes need a rebake» ([download-2-1](https://doc.photonengine.com/fusion/current/getting-started/preview-2-1/download-2-1)). Там же — ломающие изменения, задевающие физику и приоритеты: build 1762 переименовал `PlayerRef.MasterClient` → `PlayerRef.InternalMasterClientIdentifier`, build 2174 сделал `NetworkObject.SendPriority` obsolete в пользу `SetPriority`, build 1965 требует обязательного обновления протокола. ⚠️ Упоминания конфликта Fusion 2.1 с Meta Core XR SDK на этой странице обнаружить не удалось — считайте это непроверенным.

Переименования компонентов при переходе на аддон 2.1 ([physics-addon-2.1](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.1)):

| Fusion 2.0 addon | Fusion 2.1 addon |
|---|---|
| `NetworkRigidbody3D`, `NetworkRigidbody2D` | `NetworkRigidbody` (единый) |
| `RunnerSimulatePhysics3D`, `RunnerSimulatePhysics2D` | `RunnerSimulatePhysics` (добавляется автоматически при спавне `NetworkRigidbody`) |

⚠️ **Ловушка документации:** на странице аддона 2.0 имя раннер-компонента написано двояко — в списке Core Components стоят `RunnerPhysicsSimulate3D` / `RunnerPhysicsSimulate2D`, а в разделах Usage и Callbacks — `RunnerSimulatePhysics3D`. Не ищите несуществующий класс: проверяйте по установленному SDK.

---

## 3. Три стратегии сетевой физики

Главный delta относительно курса. [`manual/physics`](https://doc.photonengine.com/fusion/current/manual/physics) описывает для Fusion 2.1 три подхода, а для Fusion 2.0 — только два, с прямой оговоркой «Forecast does not exist in this version».

| Подход | Топология | Прокси интерактивны | Относительная цена CPU | Для чего |
|---|---|---|---|---|
| **Forecast** | Shared + Client/Server | Да, плавно, приближённо | Локальная физика на каждом клиенте | Интерактивная физика; для Shared Mode — «the recommended approach for interactable networked physics» |
| **Forecast Disabled** | Shared + Client/Server | Нет (прокси кинематические, remote time) | Самая низкая | Декоративная, неинтерактивная физика |
| **Physics Addon** | **Только Client/Server (Host или Dedicated)** | Да, предсказанно и ресимулированно | Самая высокая | Максимальная точность, когда цена приемлема |

Про цену Physics Addon документация не стесняется: «Unity's physics engine is not optimized for resimulation», поэтому подход самый дорогой; и «Only Clients have resimulations», значит **профилировать надо на клиенте при максимальном лаге, который игра собирается поддерживать** ([manual/physics](https://doc.photonengine.com/fusion/current/manual/physics), [addon overview](https://doc.photonengine.com/fusion/current/addons/physics/overview)).

Отдельно про поведение, которое поменялось: в Fusion 2.0 режим «Out of the Box» **не** делал прокси-Rigidbody кинематическими, а в 2.1 Forecast Disabled делает это по умолчанию — «on proxies, their rigidbodies are set to kinematic, so all interactions are handled on the authority». Вернуть поведение 2.0 можно только кодом:

```csharp
// Из документации Photon (manual/physics), не из учебного проекта.
// Возвращает поведение Fusion 2.0: прокси-Rigidbody не делаются кинематическими.
NetworkTransform.PhysicsSettings.SetForecastDisabledProxyRigidbodiesToKinematic = false;
```

Миграционная заметка, которую важно держать в голове: **аддон 2.1 эквивалентен аддону 2.0 при трёх настройках** — `RunnerPhysicsSimulate2D/3D` = `SimulateAlways`, `Physics Authority` = `Fusion`, `Physics Timing` = `FixedUpdateNetwork` ([manual/physics](https://doc.photonengine.com/fusion/current/manual/physics), [physics-addon-2.1](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.1)). То есть конфигурация, которую описывает курс, — ровно одна точка из четырёх возможных режимов аддона 2.0. ⚠️ Документация 2.1 при этом **не утверждает**, что эти три ручки исчезли из инспектора; она лишь даёт отображение настроек 2.0 → поведение 2.1. Проверяйте по установленному аддону.

---

## 4. Forecast Physics: требования, настройки, ограничения

У Forecast есть отдельная страница документации — [`manual/physics/physics-forecast`](https://doc.photonengine.com/fusion/current/manual/physics/physics-forecast). Это самый ценный материал, которого в курсе нет вообще.

**Чеклист включения (все пункты обязательны):**

1. `Physics Forecast` включён в `NetworkProjectConfig` (секция Physics). Формулировка документации: «Enabling `Physics Forecast` enables extrapolation of `NetworkTransforms` with `Rigidbodies`» ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)). По умолчанию включено.
2. `Forecast Physics Enabled` включён на `NetworkTransform` объекта — в блоке **Forecast Physics Settings** инспектора ([network-transform](https://doc.photonengine.com/fusion/current/manual/network-transform)). Без глобального флага пообъектный не работает.
3. Unity-режим симуляции физики = **FixedUpdate**, не Update и не Script. Это критично для Unity 6, где физику можно гонять в `Update`.
4. `Rigidbody.interpolation` = **Interpolate**.
5. Объект имеет Rigidbody (2D или 3D), `NetworkObject` и `NetworkTransform`.
6. Код физики — в **`FixedUpdate()`**, не в `FixedUpdateNetwork()`.

**Настройки Forecast на `NetworkTransform`** (значения по умолчанию там, где документация их называет):

| Группа | Настройка | Значение / смысл |
|---|---|---|
| Extrapolation | `Max Extrapolation Time` | Предел экстраполяции вперёд; дефолт не указан |
| Extrapolation | `Gravity Forecast` | `Apply` / `None` / `Auto`; **по умолчанию `Auto`** — гравитация учитывается только при падении |
| Error Detection | `Min Linear Detected Error`, `Min Angular Detected Error` | Мёртвая зона; для линейной ошибки рекомендация — **около 2 % диаметра объекта** |
| Error Detection | `Max Linear Error`, `Max Angular Error` | Выше — снап тела; **дефолт `0` = ограничение выключено** |
| Error Detection | `Deadzone Velocity Mode` | `Local Physics` / `Match Target` |
| Error Detection | `High Error Similarity Threshold`, `Low Correction Progress Threshold`, `Max Error Total Time` | Детект «залипания»; у `Max Error Total Time` в инспекторе есть кнопка **Auto**, выводящая значение из bounds коллайдера |
| Error Detection | `Max Remote Sleep Ignore Time` | Сколько локально активное тело может укладываться спать, если авторитет уже сообщил sleep |
| Error Correction | `Error Correction Type` | `Velocity` (**по умолчанию и рекомендуется**) / `Position Rotation` / `Spring Damping` |
| Error Correction | `Linear Vel Correction Mul`, `Angular Vel Correction Mul` | Тюнинг коррекции скоростью |
| Error Correction | `Position Correction Lerp`, `Rotation Correction Lerp` | Используются режимом `Position Rotation` |
| Error Correction | `Min Impactful Collision Alignment` | `0` — любое столкновение считается значимым, `1` — только лобовое |
| Error Correction | `Impact Start Correction Time`, `Impact Correction Time Complete` | Пауза и разгон коррекции после удара |
| Debugging | `Draw Debug Gizmos` | Remote state — зелёный, extrapolated target — красный, velocity — синий |

**Ограничения Forecast (из той же страницы):**

- Экстраполяция **не знает про препятствия мира**. Локальная физика не даст пройти сквозь стену, но позиция объекта у разных клиентов разойдётся — особенно после резкой смены направления (отскок мяча от пола).
- Качество падает с ростом пинга клиента.
- **Переключать `ForecastEnabled` в рантайме не поддерживается**; вместо этого переключайте `isKinematic` на State Authority.
- Смена Forecast-состояния живого объекта не поддерживается.

**Готовые сэмплы Photon по Forecast** (референс, которого в курсе нет): *Essential Forecast Vehicle Physics*, *Forecast Physics 2D*, *Forecast Boat and Water Physics*, *Advanced Forecast Vehicle Physics*. Последний показан прямо в [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1).

---

## 5. Physics Addon: ручки, которые решают играбельность

Это то, что стоит за `NetworkRigidbody3D` из [гл. 03](03-chapter.md). Компоненты: `NetworkRigidbody*` (рассылка состояния Rigidbody от State Authority и примирение при ресимуляции) и `RunnerPhysicsSimulate*` на объекте `NetworkRunner` (отдаёт шаг физики под контроль Fusion). По сети синкаются **linear velocity, angular velocity, kinematic, constraints** — всё остальное (например `drag`) надо синкать своим `NetworkBehaviour` ([physics-addon-2.1](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.1)).

### Client Physics Simulation — четыре режима

Главная таблица решений аддона 2.0 ([physics-addon-2.0](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.0)). В Client Host и Dedicated Server сервер симулирует физику всегда.

| Режим | Что делает | Последствия |
|---|---|---|
| `Disabled` | «Physics simulation doesn't run on clients and transforms are not synced to Unity's Physics engine» | Дешевле всего; по объектам **нельзя сделать raycast** на клиенте |
| `SyncTransforms` | «`UnityEngine.Physics.SyncTransform()` is called in all ticks» | Объекты видны raycast'ом, но только интерполируются |
| `SimulateForward` | `SyncTransform()` на ресим-тиках, `Simulate()` на forward-тиках | Локальный предсказанный игрок может растолкать объекты; взаимодействие менее плавное |
| `SimulateAlways` | «`UnityEngine.Physics.Simulate()` is called in all tick» | Лучшее взаимодействие, максимальная цена CPU |

Документация прямо фиксирует: «In Shared mode, `SimulateForward` and `SimulateAlways` behaves the same». ⚠️ Объяснение «потому что в Shared Mode нет ресимуляций» — вывод автора главы, в источнике его нет.

Для `SimulateAlways` прокси нужно явно помечать симулируемыми:

```csharp
// Из документации Photon (physics-addon-2.0), не из учебного проекта.
public override void Spawned()
{
    Runner.SetIsSimulated(Object, true); // «ideally in Spawned()»
}
```

В аддоне 2.1 этого вызова не требуется: `Spawned()` сам обеспечивает симуляцию объекта в предсказанном времени.

### Ручки на Runner-компоненте

| Настройка | Смысл (по документации) |
|---|---|
| `Physics Authority` | `Auto` — Fusion берёт на себя вызовы `Simulate()` вне Shared Mode либо при включённом Multi-Peer; `Fusion` — берёт всегда. ⚠️ Часто повторяемый совет «в Shared Mode выставь `Fusion`, если контроллеры в FUN» на странице не написан — это инференция |
| `Physics Timing` | «Indicates which timing segment is to be used if Fusion is the Physics Authority». ⚠️ **Полный список значений enum официально не документирован**: миграционная заметка называет только `FixedUpdateNetwork`; API-reference (`doc-api.photonengine.com`) закрыт cookie-интерстишиалом и автоматически не читается |
| `DeltaTime Multiplier` (2.0) / `Time Scale` (2.1) | «Multiplier for value passed to Physics `Simulate()`»; >1 ускоряет время, 0..1 замедляет. Отладочный инструмент, не геймплейный |
| `Set FixedTimestep` | Подгоняет Unity fixed timestep под дельту Fusion — **с прямым предупреждением, что `FixedUpdate()` и `FixedUpdateNetwork()` никогда не будут полностью выровнены** |

В аддоне 2.1 auto-simulate Unity (3D и 2D) выключается при инициализации первого раннера и восстанавливается при шатдауне последнего.

### Ручки на `NetworkRigidbody`

- **`Sync Scale`** — синк `transform.localScale`. **`Sync Parent`** — синк `transform.parent`; припаренченные Rigidbody должны быть кинематическими, AOI-override автоматически уходит на родительский Network Object, родительские трансформы обязаны иметь `NetworkBehaviour`, а сам `NetworkRigidbody` обязан быть на корне Network Object. Оба `NetworkRigidbody*` наследуют `NetworkTRSP` и его обработку Area of Interest — ту же иерархию, что курс разбирает в [гл. 08](08-chapter.md).
- **`Interpolation Target`** — дочерний трансформ **без коллайдеров**, который двигается для интерполяции. Документация советует **оставлять null**, пока не появится нежелательное поведение Rigidbody (трение, штабелирование). Если null — интерполируется корень и сбрасывается в tick-correct позицию в начале цикла симуляции. **Камера должна следить за Interpolation Target, а не за корнем.**
- **`Sleep Thresholds` / `Render Thresholds`** — порог, ниже которого интерполяция корня пропускается: это сохраняет sleep и физические кэши и ощутимо снижает сетевой трафик, особенно на Server Authority. Работает **только когда Interpolation Target не используется**. Внутри — `Use Energy` (сравнение энергии линейной/угловой скорости со sleep threshold), `Position`, `Rotation`, `Scale`; **«A value of 0 excludes Position from the test»** (аналогично для Rotation и Scale).
- **`Teleport()`** — ссылается на `NetworkTransform.Teleport`: событие телепорта рассылается по сети, интерполяция между предыдущим и текущим тиком в `Render()` приостанавливается. **`MovingTeleport()`** вызывается в `FixedUpdateNetwork()` **до** шага физики; телепорт «deferred until after physics has simulated», что снимает однотиковое замирание интерполяции — это и есть правильный способ сделать портал или телепорт на ходу.

### Колбэки вокруг шага физики

`IBeforePhysicsStep` / `IAfterPhysicsStep` из Fusion 1 **удалены**. Замена — события `OnBeforeSimulate` / `OnAfterSimulate` на раннер-компоненте (подписка в `Spawned()`, отписка в `Despawned()`) и одноразовые `QueueBeforeSimulationCallback` / `QueueAfterSimulationCallback`.

### Known issues (одинаковы в 2.0 и 2.1)

1. Дочерние Rigidbody ведут себя непредсказуемо **даже будучи кинематическими** и могут столкнуться «как будто из прошлой позиции». Решение: отключать коллайдеры на вложенных и переносимых объектах.
2. Масштабирование вместе с вложенностью не работает, если любой из родителей использует Interpolation Target. Документация 2.0 отдельно отмечает, что комбинация Scale + Parenting «NOT give correct results».
3. `MovePosition()` и `MoveRotation()` **требуют** Interpolation Target; прямая запись `transform.position` / `transform.rotation` — нет.

---

## 6. Тик-рейт, ресимуляции и CCD

Это самая скрытая ловушка курса. **В Fusion 2.0 tick rate и send rate залочены на 32 и 16** — то есть шаг симуляции ≈ 31.25 мс, и любая интуиция «про 60 Hz» неверна. Начиная с Fusion 2.1 tick rate можно поставить «any value between 8 and 256» ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config), [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1)).

| Параметр | Fusion 2.0 | Fusion 2.1 |
|---|---|---|
| Client & Server Tick Rate | залочен 32 | 8–256 Hz |
| Client / Server Send Rate | залочен 16 | **интервал в диапазоне 1–8** (1 = каждый тик, 2 = каждый второй), обязан делить tick rate без остатка |
| Shared Mode | — | «both rates remain subject to a cap of 32hz, but can be adjusted freely below this limit» |
| Переопределение | — | per-session на дашборде или через Webhooks — чтобы клиент не выставил нежелательное значение |

Обратите внимание: **send rate — это интервал, а не герцы**; ограничение «32» в Shared Mode применяется к паре настроек и не превращает интервал в частоту.

**Рычаги против перегрузки клиента ресимуляцией** (секция помечена «Fusion 2.1 Only»):

| Настройка | Документация |
|---|---|
| `Client Max Resims Per Frame` | «The maximum number of predicted ticks that should happen per frame» |
| `Clients Can Add Input Delay To Limit Resims` | клиенты добавляют столько input delay, чтобы удержать число предсказанных тиков ниже лимита |
| `Minimum Input Delay` | «The player's input delay will be at least this many milliseconds or ticks»; **input delay поддерживается только в Server Mode и Host Mode** и по умолчанию выключен |

⚠️ Аналог из Fusion 1 (`MaxPrediction` с дефолтом 60) часто упоминают в обсуждениях, но первоисточника для Fusion 1 подтвердить не удалось — не опирайтесь на это число.

Плюс дешёвый способ разгрузить далёкую физику: `NetworkObject.SetPriority(PlayerRef player, int priority)` — приоритет 1 отправляет каждый тик, 2 — через один, и так далее; `ClearPriority(PlayerRef)` эквивалентен `SetPriority(player, 1)`. В Host/Server «the host / server is in control of priority and has to set it for every player-object pair»; в Shared Mode валиден только локальный `PlayerRef`. После Host Migration приоритеты **надо переназначить** на новом хосте через `IAfterHostMigration` (см. [гл. 09](09-chapter.md)). Низкий send rate может давать неровное движение — лечится через `NetworkProjectConfig.TimeSynchronizationOverride`.

### CCD и туннелирование

Отдельной «сетевой» CCD в аддоне физики нет. ⚠️ Это вывод по отсутствию: ни страница аддона 2.0, ни 2.1 слово CCD не упоминают. На практике работает Unity-ный CCD: enum `Rigidbody.collisionDetectionMode` имеет значения `Discrete`, `Continuous` (static mesh geometry), `ContinuousDynamic` (static + dynamic), `ContinuousSpeculative` (speculative, static + dynamic) ([script-reference](https://docs.unity.com/en-us/engine/current/script-reference/unityengine/collisiondetectionmode)); в инспекторе они подписаны Continuous, Continuous Dynamic, Continuous Speculative, причём первые два — sweep-based, третий — speculative. «CCD is supported for Box, Sphere and Capsule colliders», и Unity прямо называет его страховочной сеткой: «It is intended as a safety net to catch collisions in cases where colliders would otherwise pass through each other. However, it does not always deliver physically accurate collision results, so you might still consider decreasing the physics timestep frequency to make the simulation more precise» ([manual](https://docs.unity.com/en-us/engine/current/manual/physics-section/physics-overview/collision-section/collision-detection/continuous)).

⚠️ Эта страница — мануал Unity 6; для проекта курса на Unity 2022.3.42f1 выводы применимы, но структура раздела Physics в Unity 6 переработана, и прямой аналог страницы в 2022.3 выглядит иначе.

Связка, которую курс не формулирует: уменьшить physics timestep в Fusion = **повысить tick rate**, а в Fusion 2.0 это невозможно (залочено 32). Поэтому для быстрых снарядов в Fusion 2.0 правильный ответ — не CCD, а **hitscan через `Runner.LagCompensation`**, что курс и делает в [гл. 06](06-chapter.md), либо extrapolation-снаряды из [гл. 04](04-chapter.md).

### Lag Compensation и физика — это документировано

Вопреки распространённому мнению, взаимодействие описано ([lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation)):

- «Lag Compensation is only available in `Server Mode` and `Host Mode`».
- «regular Unity colliders are not lag-compensated, but seen in their current state when the query is resolved» — то есть `NetworkRigidbody` **не** откатывается во времени при lag-compensated запросе.
- «The solution is to keep dynamic Colliders on a layer that can be ignored by lag-compensated raycasts» — динамическую физику выносите на отдельный слой.
- «There is a limit of 31 `Hitbox` nodes per `HitboxRoot`» — больше требует разбивки иерархии на несколько рутов.
- Пример применения силы из попадания: `nrb.Rigidbody.AddForceAtPosition(Vector2.up, hit.Point);`.

---

## 7. Персонажи: Simple KCC и Advanced KCC

Для персонажа `NetworkRigidbody` — неправильный инструмент. `NetworkCharacterController` в документации Fusion 2 всё ещё встречается (в reference-таблице страницы [coming-from-pun2](https://doc.photonengine.com/fusion/current/getting-started/migration/coming-from-pun2): колонка PUN — «N/A», колонка Fusion — `NetworkCharacterController`), но там же про альтернативу сказано: «Simple KCC addon available which is a superior option with more features». ⚠️ Явного объявления «deprecated/удалён в Fusion 2» в документации нет; вывод «не строить новое на `NetworkCharacterController`» — практическая рекомендация, а не цитата.

**Simple KCC** ([simple-kcc](https://doc.photonengine.com/fusion/current/addons/simple-kcc)) — 3D-контроллер для Client/Server и Shared. Из коробки: velocity-based движение, гравитация и прыжок, pitch+yaw, capsule-запросы и депенетрация, ground detection, step detection и ground snapping, **«Continuous collision detection (CCD) out of the box»**, автоматическое создание коллайдера дочерним объектом, pooling compatibility, «platform independent, mobile friendly». Требования: Unity 2021.3+ для аддона, Unity 6000.3+ для сэмпла.

Численные ориентиры из документации: `Extent` — «Recommended range is 10-20% of radius»; `CCD Radius Multiplier` — допустимо 10–90 % радиуса, максимальная дистанция CCD-шага = Radius × CCD Radius Multiplier, уменьшать, если персонаж проходит сквозь геометрию. `Proxy Interpolation Mode` — `Full` или `Transform`. `Anti Jitter Distance` задаётся по осям (X — горизонталь, Y — вертикаль); ⚠️ численного диапазона документация не даёт, только предупреждение, что большие значения дают заметную задержку при смене направления.

API (подтверждённые сигнатуры):

```csharp
// Из документации Photon (simple-kcc), не из учебного проекта.
void Move(Vector3 kinematicVelocity = default, float jumpImpulse = default);
void SetPosition(Vector3 position, bool teleport = true, bool allowAntiJitter = false);
void SetGravity(float gravity);
void AddLookRotation(float pitchDelta, float yawDelta, float minPitch, float maxPitch);
void SetLookRotation(Quaternion lookRotation, bool preservePitch = false, bool preserveYaw = false);
bool ProjectOnGround(Vector3 vector, out Vector3 projectedVector);

// Внимание: это свойство-колбэк фильтрации коллизий, а не метод.
Func<KCC, Collider, bool> ResolveCollision { get; set; }
```

**Отдельного метода `Jump()` в Simple KCC нет.** Прыжок — положительный `jumpImpulse` в `Move()` при `IsGrounded`; факт прыжка читается из `HasJumped`. Сэмпл использует `jumpImpulse = 10.0f`. Поток: `SetGravity` в `Spawned()`, `AddLookRotation` + `Move` в `FixedUpdateNetwork()`, камера — в `LateUpdate()` и только для input authority.

**Advanced KCC** ([overview](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/overview)) — Client/Server и Shared, уровень Advanced, «expert coding skills are expected». Сверх Simple KCC: «Support for moving platforms and latency independent character snapping», «Optional render prediction for maximum responsivity and movement precision», dynamic (physics-like) и kinematic (unconstrained) скорости, внешние воздействия (acceleration, velocity, force, impulse), «Pipeline driven by `Stages` and `Processors`», взаимодействия через регистрируемые `Modifier` и `Collision`, опциональный сетевой синк свойств KCC, ignore-список коллайдеров, enter/exit колбэки для предсказанных персонажей, partial-расширение структур данных, покадровая отладка и запись производительности, полный исходный код.

Архитектура ([architecture](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/architecture)): четыре слоя — Gameplay Scripts («what to do»), Interaction Scripts (`IKCCProcessor`, «how to move»), Public API (`KCC.SetInputDirection()`, `KCC.SetLookRotation()`, `KCC.Jump()`, `KCC.SetRadius()`, `KCC.SetPosition()`), Core Loop (физзапросы, фильтрация коллизий, депенетрация, нетворкинг, трекинг коллайдеров, step detection, ground snapping). Rigidbody обязателен и принудительно кинематичен при спавне; коллайдер создаётся в рантайме дочерним GameObject, его слой задаётся `KCCSettings.ColliderLayer` и синкается по сети; Transform используется write-only — позиция и поворот читаются один раз, при спавне. Названные по имени поля данных: `KCCData.InputDirection`, `KCCData.DynamicVelocity`, `KCCData.KinematicVelocity`, `KCCData.HasTeleported`. Названная по имени стадия — `PrepareData`. Названные процессоры: Environment Processor, Teleport Processor (вызывает `KCC.SetPosition()` из `OnEnter()`), `DashProcessor` как пример. ⚠️ Деления данных на `KCC.FixedData` / `RenderData` страница architecture не упоминает — не опирайтесь на эти имена без проверки по SDK.

### Render behavior — ответ на «как убрать input lag»

Самая практичная страница KCC и тема, которой в курсе нет совсем ([render-behavior](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/render-behavior)). Режим задаётся тремя свойствами KCC Settings: `Input Authority Behavior`, `State Authority Behavior`, `Proxy Interpolation Mode`. ⚠️ Точные имена значений enum страница не перечисляет.

| Режим | Как считается | Плюсы | Минусы |
|---|---|---|---|
| **Render Interpolation** (по умолчанию) | линейная интерполяция двух последних fixed-тиков | просто, производительно, меньше ошибок | **добавляет один fixed-шаг input lag — «about 16.67 ms at a 60 Hz simulation»**; визуальные глитчи в быстром геймплее; на отражённом движении (jump pad) «stuck effect for several render frames» |
| **Render Extrapolation** | линейная экстраполяция двух последних тиков | просто, производительно | джиттер при смене направления; персонажа может вдавить в геометрию на несколько кадров. KCC применяет экстраполяцию **только при render-дельте менее 50 µs**: меньшие дельты дают распространение `NaN` в полностью предсказанном движении |
| **Render Prediction** | полный прогон пайплайна KCC вперёд от последнего fixed-тика, включая физзапросы | мгновенный отзыв без лишнего input lag, точное движение без экстраполяции | дороже по CPU; требует аккуратного интегрирования — накопленная ошибка частичного шага даёт заметный джиттер; на jump pad «stuck effect» максимум один кадр |

**Важная поправка к цифре.** 16.67 мс — это для 60 Hz. В **Fusion 2.0 tick rate жёстко 32 Hz**, значит один fixed-шаг интерполяции ≈ **31.25 мс**, то есть почти вдвое больше. Для шутера это решающий параметр, и он меняется от версии Fusion.

Рекомендации документации: интерполяция — для чужих персонажей (производительнее и проще в поддержке); render prediction — когда нужен полный контроль над движением (быстрый платформер) или минимальный input lag (esports, шутеры).

### Known problems Advanced KCC

Оба помечены Resolved, но важны как симптомы ([known-problems](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/known-problems)):

1. **Ghost collisions** — KCC застревает на `MeshCollider` с включённым Convex даже на плоскости из-за неверного вектора пенетрации. Исправлено в Unity 2023.2. Обходы: снять Convex (при необходимости сделать отдельный меш-коллайдер) либо включить **Suppress Convex Mesh Colliders** в KCC Settings — тогда рантайм использует невыпуклую форму.
2. **Terrain trees** — `UnityEngine.Physics.ComputePenetration()` возвращает неверный вектор для `TerrainCollider` при перекрытии с деревьями, фактически их игнорируя. Решено в **KCC 2.0.11**: `TerrainTreeProcessor` делает дополнительный capsule cast после шага move.

---

## 8. Цена выбора: не только CPU

Стратегия физики тянет за собой топологию, а топология — счёт. Physics Addon требует Client Host или Dedicated Server: либо хост-игрока (и тогда у него и авторитет, и своя физика — см. [гл. 01](01-chapter.md)), либо выделенный сервер, который Photon Cloud не предоставляет и который надо хостить самостоятельно. Forecast в Shared Mode такого не требует вовсе.

Тарифы Fusion Public Cloud на момент написания ([pricing](https://www.photonengine.com/en-US/fusion/pricing)): Free 20 CCU (60 GB трафика, только разработка), Free 100 CCU (0.3 TB, одно приложение, стыкуется до 200 CCU), one-time 100 CCU за $95 с истечением через 12 месяцев, 500 CCU — $125/мес (1.5 TB), 1000 CCU — $250/мес (3.0 TB), 2000 CCU — $500/мес (6.0 TB); Premium Cloud — $0.50 за CCU при минимуме $1000/мес (включает 2000 CCU и 6.0 TB), до 50 000 CCU. Ни один тариф не включает выделенные игровые серверы — только реле и матчмейкинг Photon.

⚠️ Отдельно: Fusion существует и для Unreal, со своей физической репликацией и своими именами настроек. Это не проверялось по первоисточнику — просто не переносите Unity-названия настроек в Unreal-проект и проверяйте, что открытая страница документации относится к Unity-версии.

---

## Чего по этой теме нет в учебном курсе

Курс (Fusion 2.0.5 build 920, Unity 2022.3.42f1, Host Mode) трогает из всего направления одну точку: `Fusion.Addons.Physics` с `NetworkRigidbody3D` в [гл. 03](03-chapter.md) и [гл. 05](05-chapter.md). Отсутствует:

1. **Fusion 2.1 и Forecast.** Ни трёх стратегий, ни того, что аддон 2.0.x не работает на 2.1.x, ни переименования `NetworkRigidbody3D` → `NetworkRigidbody`. Код курса на свежем Fusion **не скомпилируется** без миграции.
2. **Все ручки аддона.** `Client Physics Simulation` (четыре режима), `Physics Authority`, `Physics Timing`, `Set FixedTimestep`, `DeltaTime Multiplier`/`Time Scale`, `Render Thresholds`, `Sleep Thresholds`, `MovingTeleport()`, `OnBeforeSimulate`/`OnAfterSimulate`, `Runner.SetIsSimulated()`. Именно они определяют, будет ли физика играбельной на 150 мс.
3. **Interpolation Target как решение, а не как галка.** Курс оставляет его null (что совпадает с рекомендацией Photon), но не говорит, что без него `MovePosition()` не работает и что камеру надо вешать на него.
4. **Тик-рейт.** Нигде не сказано, что в 2.0 tick rate жёстко 32, send rate 16, и что настраиваемый 8–256 Hz появился только в 2.1 — вместе с `Client Max Resims Per Frame` и input delay.
5. **Детерминизм как понятие.** Правило «не используй `Random` в FUN» дано, но без объяснения, что это про повторяемость тика, и без развилки «нужен настоящий детерминизм → Quantum».
6. **Персонажи.** `NetworkCharacterController`, Simple KCC, Advanced KCC отсутствуют целиком. То есть курс не даёт ни одного готового способа сделать сетевого персонажа с CCD, step detection, ground snapping и движущимися платформами.
7. **Render prediction и input lag.** Тема «один fixed-шаг интерполяции = 31.25 мс на 2.0 или 16.67 мс на 60 Hz и как это убрать» не поднимается.
8. **CCD.** Связка «туннелирование ↔ physics timestep ↔ tick rate ↔ почему hitscan» не проговорена: [гл. 06](06-chapter.md) делает hitscan, но без объяснения, что это следствие залоченного тик-рейта.
9. **Lag Compensation × физика.** Что `NetworkRigidbody` не откатывается во времени и что динамику надо выносить на отдельный слой — в [гл. 06](06-chapter.md) нет.

---

## Решение: что брать и когда

```
Нужна битовая одинаковость на всех платформах
(файтинг, RTS, детерминированный реплей)?
  └─ ДА  → не Fusion. Quantum (FP Q48.16, свой детерминированный физдвижок).
  └─ НЕТ ↓

Физика декоративная, в неё не толкаются?
  └─ ДА  → Forecast Disabled (2.1) / «out of the box» (2.0). Дешевле всего.
  └─ НЕТ ↓

Топология — Shared Mode?
  └─ ДА  → Forecast. Единственный вариант: Physics Addon в Shared не работает.
            Код физики в FixedUpdate(). Unity simulation mode = FixedUpdate.
  └─ НЕТ (Host / Dedicated Server) ↓

Нужна максимальная точность предсказанной физики
(пазлы с телами, физические снаряды, транспорт под игроком)?
  └─ НЕТ → Forecast. Дешевле, взаимодействие плавное со всех клиентов.
  └─ ДА  → Physics Addon. SimulateAlways. Профилировать НА КЛИЕНТЕ
            при максимальном поддерживаемом пинге.

Объект — персонаж?
  └─ Всегда KCC, никогда NetworkRigidbody.
     Simple KCC по умолчанию; Advanced KCC — если нужны
     движущиеся платформы, процессоры или Render Prediction.

Клиент тонет в ресимуляциях?
  └─ Fusion 2.1: Client Max Resims Per Frame
     + ClientsCanAddInputDelayToLimitResims (только Server/Host Mode).
     Покупаем стабильность CPU ценой input lag.
```

Главные трейдоффы в одной таблице:

| Выбор | Платим | Получаем |
|---|---|---|
| Forecast вместо Physics Addon | расхождением картинки между клиентами на резких отскоках | CPU, плавность, поддержку Shared Mode |
| Forecast Disabled | невозможностью толкнуться в объект с клиента | минимальную цену |
| `SimulateAlways` вместо `Disabled` | максимальным CPU на клиенте | предсказанную физику и raycast по объектам |
| Interpolation Target = null | возможными проблемами трения и штабелирования | работающие Render Thresholds (трафик, sleep) |
| Повышение Render Thresholds | ступенчатостью на медленном дрейфе | трафик и сохранение физических кэшей |
| Повышение tick rate (2.1) | линейной ценой CPU и более тяжёлой ресимуляцией | устойчивые стеки, меньше туннелирования |
| `ClientsCanAddInputDelayToLimitResims` | input lag | кадры на плохой сети |
| KCC Render Prediction | CPU и требованием к аккуратности интегрирования | отсутствие ~31 мс (2.0) или ~17 мс (60 Hz) задержки |
| `Compress Network Position` (KCC) | точностью позиции | трафик |

---

## Чеклист

1. **Определи линию Fusion до первой строки кода.** Открой Release Notes и зафиксируй версию: 2.1.3 build 2390 (актуальная) или 2.0.13 build 2379 (legacy). Аддон физики должен быть той же линии: 2.1.x ↔ Physics Addon 2.1.3, 2.0.x ↔ Physics Addon 2.0.6.
2. **Если переносишь код курса** — сначала обнови проект до последней 2.0.x, потом до 2.1.x, затем сделай rebake всех префабов и сцен. Переименуй `NetworkRigidbody3D` → `NetworkRigidbody`, `RunnerSimulatePhysics3D` → `RunnerSimulatePhysics` и убери ручное добавление раннер-компонента.
3. **Выбери одну из трёх стратегий физики осознанно** по дереву выше и запиши выбор в документацию проекта. Не смешивай Forecast и Physics Addon в одном проекте без явной причины.
4. **Для Forecast** пройди все шесть пунктов чеклиста включения: глобальный `Physics Forecast`, пообъектный `Forecast Physics Enabled`, Unity simulation mode = FixedUpdate, Rigidbody interpolation = Interpolate, наличие `NetworkObject` и `NetworkTransform`, код физики в `FixedUpdate()`.
5. **Для Forecast не переключай `ForecastEnabled` в рантайме** — переключай `isKinematic` на State Authority.
6. **Для Physics Addon 2.0** выставь `Client Physics Simulation` явно. Если клиент должен толкаться — `SimulateAlways` плюс `Runner.SetIsSimulated(Object, true)` в `Spawned()`. Если нужен только raycast — `SyncTransforms`.
7. **Interpolation Target оставляй null** до появления проблем. Если включил — привяжи камеру к нему, не ставь на него коллайдеры, не комбинируй с Sync Scale при вложенности и помни, что Render Thresholds перестают работать.
8. **Отключи коллайдеры на вложенных и переносимых Rigidbody** — это документированный known issue, дающий «столкновение из прошлой позиции».
9. **Телепортируй движущиеся объекты через `MovingTeleport()`** из `FixedUpdateNetwork()` до шага физики, а не через `Teleport()`.
10. **Проверь тик-рейт.** На 2.0 он 32 Hz — пересчитай все допущения про шаг физики (31.25 мс) и про input lag интерполяции. На 2.1 поднимай его только после профилирования и убедись, что send rate делит tick rate без остатка.
11. **Профилируй физику на клиенте, а не на хосте**, при максимальном пинге, который игра собирается поддерживать: ресимулируют только клиенты.
12. **Если клиент теряет кадры** — включи `Client Max Resims Per Frame` и `ClientsCanAddInputDelayToLimitResims` (только Server/Host Mode), а далёкие объекты переведи на `SetPriority(player, N)`; после Host Migration переназначь приоритеты в `IAfterHostMigration`.
13. **Персонажа делай на Simple KCC**: `SetGravity` в `Spawned()`, `AddLookRotation` плюс `Move(velocity, jumpImpulse)` в `FixedUpdateNetwork()`, камера в `LateUpdate()` только для input authority. `Extent` — 10–20 % радиуса, `CCD Radius Multiplier` — 10–90 %.
14. **Если нужен минимальный input lag** — Advanced KCC с Render Prediction, и проверь, что один шаг 16 мс даёт тот же результат, что четыре по 4 мс.
15. **Для быстрых снарядов не надейся на CCD.** Unity сам называет его safety net; правильный ответ — hitscan через `Runner.LagCompensation` ([гл. 06](06-chapter.md)) или extrapolation-снаряд ([гл. 04](04-chapter.md)). Динамические коллайдеры вынеси на слой, игнорируемый lag-compensated raycast'ами.
16. **Не пытайся добиться детерминизма PhysX.** Если ТЗ требует кросс-платформенного детерминизма или rollback-реплеев — это смена движка на Quantum, а не настройка Fusion.

---

## Источники

**Photon Fusion — ядро**
- [manual/physics](https://doc.photonengine.com/fusion/current/manual/physics) — три стратегии физики в 2.1, таблица сравнения, раздел Fusion 2.0 («Forecast does not exist in this version»), тройка настроек эквивалентности аддонов 2.1 ↔ 2.0.
- [manual/physics/physics-forecast](https://doc.photonengine.com/fusion/current/manual/physics/physics-forecast) — чеклист требований Forecast, полный список настроек error detection и error correction с дефолтами, ограничения, имена сэмплов.
- [manual/network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config) — `Physics Forecast`, Tick Rate 8–256, Send Rate как интервал 1–8, cap 32 в Shared Mode, `Client Max Resims Per Frame`, `Clients Can Add Input Delay To Limit Resims`, `Minimum Input Delay`.
- [manual/network-transform](https://doc.photonengine.com/fusion/current/manual/network-transform) — блок Forecast Physics Settings и `Forecast Physics Enabled`, поведение `Teleport()`.
- [manual/network-simulation-loop](https://doc.photonengine.com/fusion/current/manual/network-simulation-loop) — prediction, «at least 2 ticks ahead», ресимуляция «once for every tick», `NetworkRunner.DeltaTime`.
- [manual/lag-compensation](https://doc.photonengine.com/fusion/current/manual/lag-compensation) — только Server/Host Mode, обычные коллайдеры не lag-compensated, слой для динамики, лимит 31 Hitbox на HitboxRoot, `AddForceAtPosition`.

**Photon Fusion — версии и релизы**
- [release-notes-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1) — 2.1.3 Stable build 2390 (17.09.2026), 2.1.2 build 2279, 2.1.1 build 2177.
- [release-notes](https://doc.photonengine.com/fusion/current/getting-started/release-notes) — последняя 2.0.x: 2.0.13 Stable build 2379 (16.09.2026).
- [whats-new-2-1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/whats-new-2-1) — Forecast Physics, «any tick rate between 8hz and 256hz», «cap of 32hz» для Shared Mode, input delay, `SetPriority`/`ClearPriority`, `TimeSynchronizationOverride`, сэмпл Advanced Forecast Vehicle Physics.
- [preview-2-1/download-2-1](https://doc.photonengine.com/fusion/current/getting-started/preview-2-1/download-2-1) — порядок апгрейда через последнюю 2.0.x, rebake префабов и сцен, ломающие изменения (`PlayerRef.MasterClient`, `SendPriority` → `SetPriority`, протокол в build 1965).
- [migration/coming-from-pun2](https://doc.photonengine.com/fusion/current/getting-started/migration/coming-from-pun2) — `NetworkCharacterController` в reference-таблице, «Simple KCC addon available which is a superior option with more features».

**Photon Fusion — Physics Addon**
- [addons/physics/overview](https://doc.photonengine.com/fusion/current/addons/physics/overview) — «Only for Client Host or Dedicated Server modes», «This Addon is for Fusion 2.1.x. It does not support Fusion 2.0.x», `NetworkRigidbody` и `RunnerSimulatePhysics`, рекомендация профилировать на клиенте.
- [addons/physics-addon-2.1](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.1) — версии 2.1.1–2.1.3, переименования, синк linear/angular velocity плюс kinematic и constraints, `Time Scale`, автодобавление `RunnerSimulatePhysics`, миграция, known issues.
- [addons/physics-addon-2.0](https://doc.photonengine.com/fusion/current/addons/physics-addon-2.0) — единственная версия 2.0.6 (03.07.2026), четыре режима Client Physics Simulation с дословными описаниями, `Physics Authority`, `Physics Timing`, `DeltaTime Multiplier`, `Set FixedTimestep`, Sync Scale/Parent, Interpolation Target, Sleep и Render Thresholds («A value of 0 excludes Position from the test»), `Teleport`/`MovingTeleport`, замена `IBeforePhysicsStep`/`IAfterPhysicsStep`, расхождение в имени раннер-компонента.

**Photon Fusion — KCC**
- [addons/simple-kcc](https://doc.photonengine.com/fusion/current/addons/simple-kcc) — версии addon 2.1.1/2.1.0/2.0.15 и sample 2.1.1/2.1.0/2.0.18, Unity 2021.3+ и 6000.3+, CCD out of the box, Extent 10–20 %, CCD Radius Multiplier 10–90 %, сигнатуры `Move`/`SetPosition`/`SetGravity`/`AddLookRotation`/`SetLookRotation`/`ProjectOnGround`/`ResolveCollision`, отсутствие `Jump()`, `jumpImpulse = 10.0f` в сэмпле.
- [addons/advanced-kcc/overview](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/overview) — полный список фич, Client/Server и Shared, «expert coding skills are expected».
- [addons/advanced-kcc/architecture](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/architecture) — четыре слоя, Public API, обязательный кинематический Rigidbody, рантайм-коллайдер и `KCCSettings.ColliderLayer`, write-only Transform, `KCCData.InputDirection/DynamicVelocity/KinematicVelocity/HasTeleported`, стадия `PrepareData`, Environment/Teleport/`DashProcessor`.
- [addons/advanced-kcc/render-behavior](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/render-behavior) — три режима, «about 16.67 ms at a 60 Hz simulation», порог 50 µs и риск `NaN`, `Input Authority Behavior`/`State Authority Behavior`/`Proxy Interpolation Mode`, рекомендации.
- [addons/advanced-kcc/known-problems](https://doc.photonengine.com/fusion/current/addons/advanced-kcc/known-problems) — ghost collisions на Convex `MeshCollider` (фикс в Unity 2023.2, опция Suppress Convex Mesh Colliders), terrain trees и `TerrainTreeProcessor` (решено в KCC 2.0.11).

**Photon Quantum — альтернатива для детерминизма**
- [quantum/manual/quantum-ecs/fixed-point](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/fixed-point) — `Q48.16` поверх `long`, запрет float в симуляции («will cause desyncs 100% of the time»), `FP.UseableMax` = 32767.9999847412, `FP.UseableMin` = −32768, диапазон точности `(0.01..1000)`.

**Unity — CCD**
- [manual: continuous collision detection](https://docs.unity.com/en-us/engine/current/manual/physics-section/physics-overview/collision-section/collision-detection/continuous) — «safety net», «does not always deliver physically accurate collision results, so you might still consider decreasing the physics timestep frequency», «CCD is supported for Box, Sphere and Capsule colliders», инспекторные подписи режимов.
- [script reference: CollisionDetectionMode](https://docs.unity.com/en-us/engine/current/script-reference/unityengine/collisiondetectionmode) — имена enum `Discrete`, `Continuous`, `ContinuousDynamic`, `ContinuousSpeculative` и их описания.

**Цены**
- [photonengine.com/fusion/pricing](https://www.photonengine.com/en-US/fusion/pricing) — Free 20/100 CCU, one-time 100 CCU $95, 500/1000/2000 CCU за $125/$250/$500 в месяц, Premium Cloud $0.50/CCU при минимуме $1000/мес, до 50 000 CCU.

**Не подтверждено первоисточником / данные расходятся**
- Полный список значений enum `Physics Timing`: официально не документирован, в миграционной заметке назван только `FixedUpdateNetwork`. `doc-api.photonengine.com` закрыт cookie-интерстишиалом и автоматически не читается.
- CCD в контексте Physics Addon: ни страница 2.0, ни 2.1 его не упоминают. Вывод «это CCD Unity» сделан по отсутствию альтернативы.
- Кросс-платформенный детерминизм PhysX: официальной страницы Unity с утверждением «limited determinism» найти не удалось; тезис бытует на форумах и в неофициальной вики.
- Утверждение «на форуме Photon отвечают, что Fusion не использует lockstep»: страница форума отдаёт защитный интерстишиал и не проверяется. По сути вывод косвенно подтверждается страницами Quantum.
- `MaxPrediction` (default 60) как аналог из Fusion 1: источника не найдено.
- Конфликт Fusion 2.1 с Meta Core XR SDK: на странице download-2-1 не обнаружен.
- Деление данных Advanced KCC на `FixedData` / `RenderData`: на странице architecture не упомянуто.
- `Max Penetration Steps` в Simple KCC и численный диапазон `Anti Jitter Distance`: поимённо на странице не подтверждены.
- Fusion для Unreal (своя физическая репликация, свои имена настроек): не проверялось; просто не переносите Unity-названия в Unreal-проект.

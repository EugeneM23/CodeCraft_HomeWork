# Глава 25. Сцены, Multi-Peer и инструменты тестирования

> **TL;DR для агента**
>
> 1. **Fusion не умеет работать со сценами Unity.** Он умеет только дёргать `INetworkSceneManager`, который ты передаёшь в `StartGameArgs.SceneManager`. Передашь `null` — получишь `NetworkSceneManagerDummy`, ошибку в лог и необъяснимо «несетевые» объекты сцены ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)).
> 2. **`LoadScene`/`UnloadScene` вызывает только scene authority.** Гейт — `if (Runner.IsSceneAuthority)`. Штатный менеджер это навязывает, кастомный обязан навязать сам ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)).
> 3. **Лимит одновременно загруженных сцен: 8 в Fusion 2.0, 31 начиная с 2.1.3** (build 2390, 17.09.2026). Страница manual под `/current` до сих пор пишет «8» — ей верить нельзя ([release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)).
> 4. **Multi-Peer = N независимых `NetworkRunner` в одном процессе.** Отсюда жёсткое следствие: любой статик, синглтон, `Camera.main` и `FindObjectOfType` становится багом. Документация формулирует это как «strongly advised to avoid static variables and singletons for anything that affects simulation» ([multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer)).
> 5. **Вся отладочная оснастка главы живёт только на debug DLL**: Fusion Statistics, встроенная симуляция задержек/потерь и поддержка брейкпоинтов. На release-сборке их нет вообще — симулировать сеть придётся внешним Clumsy.

---

## Зачем это знать

Учебный курс (Fusion 2.0.5, Unity 2022.3.42f1, Host Mode) живёт в **одной сцене** и запускает второго игрока **вторым процессом редактора** через ParrelSync. Обе эти опоры ломаются одновременно, как только проект перестаёт быть учебным:

- появляется меню → уровень → возврат в меню, и выясняется, что сцены в Fusion — это **сетевое состояние**, а не вызов `SceneManager.LoadScene`;
- появляется желание запускать 4 клиента за полсекунды, и выясняется, что для этого есть бесплатный официальный Multi-Peer, но написанный «как обычно» код в нём не работает.

Эта глава — про стык двух тем, которые в документации Photon стоят рядом не случайно: **четыре члена интерфейса `INetworkSceneManager` существуют исключительно ради multi-peer** (`MainRunnerScene` — «mainly used for multi-peer support», `MakeDontDestroyOnLoad` — «Used for multi-peer support», `MoveToRunnerScene` — «This is only used for multi-peer support», `IsRunnerScene` — «Used for multi-peer support», [scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)).

---

## 1. Сцены: Fusion знает только `INetworkSceneManager`

### 1.1. Базовая модель

Менеджер сцен задаётся «in the `NetworkRunner.StartGame()` method via the `StartGameArgs.SceneManager` field». Если реализация не передана, «Fusion will create an instance of the `NetworkSceneManagerDummy` class» — то есть объекты, лежащие в сцене, не получат сетевых ID ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)).

Штатная реализация `NetworkSceneManagerDefault` умеет (дословный список со страницы):

| Возможность | Цитата |
|---|---|
| Загрузка/выгрузка | «Load and Unload scenes.» |
| Addressables | «Supports regular scene loading and scene loading via addressables.» |
| Перезагрузка | «Can reload currently active scenes.» |
| Аддитивность | «Loads up to 8 scenes simultaneously using additive scene loading.» |
| Multi-peer | «Supports multi-peer scene loading with correct `PhysicsScene` associations for each Runner.» |

Источник: [scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading).

### 1.2. Авторитет и API загрузки

```csharp
// рецепт, не код из документации
if (Runner.IsSceneAuthority) {
    Runner.LoadScene(SceneRef.FromIndex(2), LoadSceneMode.Single);
}
```

- «Both `LoadScene()` and `UnloadScene()` are only allowed to be called on the Server/Host or» Master Client; «This is enforced by the default scene manager implementation. Custom implementations must also enforce this» ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)).
- `Runner.IsSceneAuthority` — ⚠️ **подтверждается только примерами кода** на этой странице (`if (Runner.IsSceneAuthority) {`); описания свойства в manual нет. Проверяй по IntelliSense своей версии.
- `LoadSceneMode.Single` — «loading with single load mode will unload all previous scenes»; `Additive` — «previous scenes remain loaded». Перезагрузка — «by unloading and loading them again in sequence» ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)).
- `SceneRef.FromIndex(int index)` — «The index used to create the `SceneRef` is the index of the scene in the Unity build». `SceneRef.FromPath(string path)` — «mostly used to create references to addressables scenes» ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)). **В ветке 2.1** (nightly build 1240, 08.08.2025): «Non-addressable scenes can now also be loaded with `NetworkRunner.LoadScene(SceneRef.FromPath(...))`» ([release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)).

### 1.3. Начальный набор сцен при `StartGame`

Это то, чего в курсе нет совсем: курс присваивает `StartGameArgs.Scene` напрямую один `SceneRef` ([гл. 01](01-chapter.md), [гл. 07](07-chapter.md)). Туториал Fusion 102 показывает полную форму — через `NetworkSceneInfo`:

```csharp
// структура кода из документации (Fusion 102, BasicSpawner.StartGame)
var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
var sceneInfo = new NetworkSceneInfo();
if (scene.IsValid) {
    sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
}
await _runner.StartGame(new StartGameArgs() {
    GameMode = mode,
    SessionName = "TestRoom",
    Scene = scene,
    SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
});
```

Туториал отдельно оговаривает: индекс сцены «is passed in, but this is only relevant for the host as clients will be forced to use the scene specified by the host», а дефолтный scene manager «isn't strictly needed in this example because there are no such objects» (т.е. нет объектов сцены) ([fusion-102](https://doc.photonengine.com/fusion/current/fusion-100/fusion-102)).

**Директива:** если нужен более одной стартовой сцены или аддитивный набор — строй `NetworkSceneInfo` и наполняй его `AddSceneRef(sceneRef, LoadSceneMode)`. Одиночный `SceneRef` в поле `Scene` работает (так сделано в курсе), но набор им не задать.

### 1.4. `NetworkSceneInfo`, лимит и `OnSceneInfoChanged`

`NetworkSceneInfo` — **сетевое** состояние «какие сцены активны»: «it can store up to 8 active scenes and allows for duplicates». «Whenever the scene authority (Host/Server or Master Client) changes the scene info», на зарегистрированном `INetworkSceneManager` вызывается `OnSceneInfoChanged()` — «Use this method to react and trigger scene loading on clients» ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)).

**Числа по версиям — тут данные расходятся между manual и release notes:**

| Версия | Лимит сцен | Источник |
|---|---|---|
| 2.0.x | 8 | [scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading) («up to 8 active scenes») |
| 2.1.3, build 2390, 17.09.2026 | **31** | [release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1): «Increased the max loaded scene count from 8 to 31» |

Страница manual под `/current` **не обновлена** и всё ещё печатает 8. Для 2.1.3+ верное число — 31.

В 2.1 же `NetworkSceneManagerDefault.OnSceneInfoChanged` сделан `virtual` (nightly build 1240, 08.08.2025) — переопределять реакцию на смену набора сцен без форка менеджера можно только начиная с этой ветки ([release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)).

### 1.5. Что обязан делать кастомный scene manager

Три вызова на ранере, без которых Fusion не узнает о загрузке ([scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)):

| Вызов | Зачем |
|---|---|
| `Runner.InvokeSceneLoadStart(sceneRef)` | уведомление о старте |
| `Runner.InvokeSceneLoadDone(SceneLoadDoneArgs)` | уведомление о завершении |
| `NetworkRunner.RegisterSceneObjects(SceneRef scene, NetworkObject[] objects, byte loadId = 0)` | регистрация объектов сцены; «The additional `loadId` parameter is passed to ensure each scene load yields unique type ids for scene objects» |

`Version` — поле **дефолтной** реализации: «maintain a `Version` field that works as a counter, increasing every time a scene is loaded or unloaded», и это значение «is used to register the load ID of the scene object». Кастомному менеджеру страница вести `Version` не предписывает — но без уникального `loadId` повторная загрузка той же сцены даст конфликт type ID.

Остальные члены интерфейса и их назначение:

| Член | Назначение (цитата/пересказ) | Multi-peer? |
|---|---|---|
| `Initialize(NetworkRunner runner)` | «Called when the runner initializes itself» | — |
| `Shutdown()` | «Called when the runner shutdown» | — |
| `bool IsBusy` | занят ли менеджер загрузкой | — |
| `Scene MainRunnerScene` | какая сцена главная для ранера; «mainly used for multi-peer support» | **да** |
| `TryGetPhysicsScene2D/3D(out ...)` | отдать физическую сцену ранера | — |
| `MakeDontDestroyOnLoad(GameObject)` | «Used for multi-peer support» | **да** |
| `MoveToRunnerScene(GameObject)` | «This is only used for multi-peer support» | **да** |
| `IsRunnerScene(Scene)` | «Return if this scene is related with the `NetworkRunner`. Used for multi-peer support» | **да** |
| `LoadScene(SceneRef, LoadSceneParameters)` | загрузка с параметрами Unity | — |
| `UnloadScene(SceneRef)` | выгрузка | — |
| `OnSceneInfoChanged()` | реакция на смену набора сцен | — |
| `GetSceneRef(string sceneName)` | `SceneRef` по имени сцены | — |

Источник таблицы: [scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading).

**Про `LoadSceneParameters.localPhysicsMode`:** интерфейс принимает `LoadSceneParameters`, но manual **никак не описывает** `localPhysicsMode` — это официально не документировано. Что параметр реально участвует в работе, видно только по чейнджлогу: Build 719 (11.07.2023), «Fixed: `NetworkRunner.LoadScene` ignoring `localPhysicsMode` parameter» ([release notes 2.0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-0)). Это ровно стык «аддитивные сцены + своя физическая сцена на ранер», и опираться на него без собственного теста нельзя.

**Addressables:** define переименован в 2.0.0 — «Renamed addressable define from FUSION_USE_ADDRESSABLES -> FUSION_ENABLE_ADDRESSABLES». Исторический фикс по надёжности — Build 733 (05.08.2023): «Fixed: Addressable scenes discovery blocking Unity, if something goes wrong» (это **не** таймаут и **не** 2.0.2, как иногда пересказывают). Там же, Build 733: «Calls to Load/Unload scene are correctly deferred until the initial info is processed» ([release notes 2.0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-0)).

---

## 2. Объекты сцены против `Spawn`: откуда берётся `NetworkId`

«Network Objects can be created either by Spawning or by loading as Scene Objects» ([network-object](https://doc.photonengine.com/fusion/current/manual/network-object)).

**Scene objects.** При загрузке сцены «any enabled Network Objects in the scene are attached on the Master Client or Server in Server Mode», затем «the assigned `NetworkId` of each Scene Object is communicated to all other clients», и клиенты прикрепляют свои локальные копии ([network-object](https://doc.photonengine.com/fusion/current/manual/network-object)). Отсюда:

- Слово **enabled** в цитате — единственное, что документация говорит про выключенные объекты. Вывод «выключенный в сцене `NetworkObject` в сеть не попадёт» логически следует из формулировки, но ⚠️ **прямо не задокументирован**.
- Авторитет: «When the scene is loaded for the first time the master client automatically takes StateAuthority over all Scene Objects» ([coming-from-pun2](https://doc.photonengine.com/fusion/v2/getting-started/migration/coming-from-pun2)). ⚠️ Страница в целом устарела — на ней же написано «replace `PhotonNetwork.LoadLevel()` with `Runner.SetActiveScene()`», а `SetActiveScene` удалён в 2.0.0 («`Runner.SetActiveScene` -> `Runner.LoadScene`», [release notes 2.0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-0)). Цитату про StateAuthority брать можно, остальное с этой страницы — нет.
- Термина **«scene take-over»** в документации Fusion 2 нет. Зато в SDK есть реальное поле `NetworkSceneManagerDefault.IsSceneTakeOverEnabled` (дефолт `true`), и его смысл уже, чем кажется: совпала уже загруженная сцена с той, что менеджер собирался грузить → загрузка пропускается. Разбор по исходникам SDK — [гл. 01](01-chapter.md).

**Spawned objects.** «`Runner.Spawn()` can only be called on the Server in Host Mode» (в Shared — на клиенте, который станет State Authority). Как именно создаётся инстанс, решает `INetworkObjectProvider`, переданный в `Runner.StartGame()`; префаб «must be registered with Fusion», вручную — кнопкой «Rebuild Object Table» в инспекторе `NetworkProjectConfig` ([network-object](https://doc.photonengine.com/fusion/current/manual/network-object)). Пулинг через свой провайдер в курсе есть — [гл. 02](02-chapter.md).

**Порядок колбэков.** `NetworkObject` сперва вызывает `Spawned()` на всех дочерних `NetworkBehaviour`, и только после того как «all those `Spawned()` methods have been called», начинают вызываться `FixedUpdateNetwork()` и `Render()` ([network-object](https://doc.photonengine.com/fusion/current/manual/network-object)). Это ровно та модель, на которой построены главы [01](01-chapter.md)–[05](05-chapter.md).

**Запекание (bake).** Тут документация буквально не дописана: на странице стоит «The `NetworkObject` must be baked (needs instructions here)» ([network-object](https://doc.photonengine.com/fusion/current/manual/network-object)). Бытовая формулировка есть только в туториале: «Save your project to have Fusion bake the new Network object» ([fusion-102](https://doc.photonengine.com/fusion/current/fusion-100/fusion-102)). Что это не формальность, видно по чейнджлогу: 2.1.2 build 2279 (13.08.2026) — «Fixed: `NetworkSceneManagerDefault` not ignoring scene `NetworkObjects` that have not been baked, e.g. on-scene pools»; 2.1.3 build 2390 — «Fixed: Guard scene baking against unloaded scenes» ([release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)). ⚠️ **Имя `Baker.Bake(GameObject)` для рантайм-запекания первоисточником не подтверждено** — оно встречается только в сторонних руководствах; официальной страницы с инструкцией нет.

**Правило выбора (авторская методика, не цитата):** единственный в мире объект, которым управляет хост (дверь, зона захвата, точка спавна с логикой, флаг) — **scene object**; всё, что появляется и исчезает по ходу матча — **префаб + `Spawn`**.

---

## 3. Multi-Peer: N симуляций в одном процессе

### 3.1. Включение и назначение

`Fusion > NetworkProjectConfig` → `Peer Mode` → `Multiple`. В сэмплах путь к ассету — `Photon/Resources/NetworkProjectConfig` ([multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer), [projectiles-advanced quickstart](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-advanced/quickstart)).

Формулировки опций в конфиге: `Single` — «the normal use case in which the build or editor runs a single server, host, or client peer»; `Multiple` — «allows multiple peers **in the editor**» ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)). **Данные расходятся:** страница Multi-Peer Mode называет ещё два применения — «dedicated servers hosting several Game Sessions from one Unity instance» и «clients connected to several Game Sessions at once» ([multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer)). Опирать продакшен-архитектуру на эти два сценария без собственной проверки не стоит.

Каждый ранер получает свою симуляцию, своё соединение, свои сцены и свою физическую сцену (`PhysicsScene` на ранер обеспечивает `NetworkSceneManagerDefault`, [scene-loading](https://doc.photonengine.com/fusion/current/manual/scene-loading)). Переопределить выбор корневой сцены можно через `NetworkSceneManagerDefault.GetMultiplePeerTargetScene` — «ability to override the logic of choosing a root scene object in MP mode», добавлено в 2.0.0 ([release notes 2.0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-0)).

### 3.2. Видимость

По умолчанию видны все пиры одновременно. Управление:

- `RunnerEnableVisibility` — «must be added to the NetworkRunner GameObject/prefab in order to have visibility control». Компонент сканирует прикреплённые `NetworkObject`, сценовые `NetworkObject` и **не-инстанцированные сценовые GameObject** на косметические компоненты (рендереры, аудиосорсы) и держит их в списке, привязанном к ранеру.
- Переключение прячет рендер и аудио, но «Code execution for that runner is unaffected» — симуляция скрытого пира продолжает идти.
- `EnableOnSingleRunner` добавляет `RunnerVisibilityLink` на каждый компонент из поля `Components` и оставляет **ровно один включённый экземпляр** на все ранеры. Каноничные примеры документации — `AudioListener` и `EventSystem`. Поле *Preferred Runner* задаёт, какой тип ранера приоритетно видим.

Источник: [multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer).

**Имя метода: данные расходятся, в официальных документах встречаются три написания.**

| Написание | Где |
|---|---|
| `NetworkRunner.SetVisibility(bool)` | [multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer), раздел про `RunnerEnableVisibility` |
| `NetworkRunner.SetVisible(bool)` / `GetVisible()` | [multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer), заголовок «GetVisible() and SetVisible(bool)» |
| `NetworkRunner.SetVisiblity` (с опечаткой) | [AoI Multi-Peer sample](https://doc.photonengine.com/fusion/current/technical-samples/fusion-multi-peer-area-of-interest) |

**Директива:** не копируй имя из документации — сверяй по IntelliSense своей версии SDK.

### 3.3. Главное ограничение — твой собственный код

Единственное, что документация говорит прямо: ранеры «are meant to be fully independent», и потому «strongly advised to avoid static variables and singletons for anything that affects simulation», потому что при нескольких `NetworkRunner` они конфликтуют ([multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer)).

⚠️ **Дальше — авторская методика, а не документация Photon.** На странице Multi-Peer Mode нет ни слова про `Camera.main`, `FindObjectOfType` или `Physics.Raycast`. Механика поломки выводится из модели «статик один на процесс, а симуляций в процессе теперь N»:

| Конструкция | Что сломается | Замена |
|---|---|---|
| `GameManager.Instance` | указывает на инстанс произвольного ранера | состояние матча в `NetworkBehaviour` на scene object, доступ через `Runner` |
| `Camera.main` | вернёт камеру чужого пира — в том числе скрытую `SetVisible(false)` (компонент при этом может быть включён) | камера пер-ранер, под `RunnerEnableVisibility` |
| `FindObjectOfType<T>()` | пересечёт границу ранеров | наследовать `SimulationBehaviour` (у него есть `Runner`) |
| `Physics.Raycast` | «в чей мир я стреляю» | `Runner.GetPhysicsScene()` — требование уже введено в курсе, [гл. 02](02-chapter.md), [гл. 03](03-chapter.md), [гл. 08](08-chapter.md) |
| `Instantiate` визуала | объект уедет в чужую/общую сцену | `MoveToRunnerScene(go)`; для «вечных» — `MakeDontDestroyOnLoad` |

Как получить ранер без синглтона — это задокументировано: `SimulationBehaviour` даёт `FixedUpdateNetwork`/`Render` без `NetworkObject` и «can not have any networked properties». Если он **не** на GameObject ранера — найди ранер через `NetworkRunner.GetRunnerForGameObject()`, убедись, что он запущен, и зарегистрируйся:

```csharp
// из документации (SimulationBehaviour)
runner.AddGlobal(this);    // register
runner.RemoveGlobal(this); // unregister
```

«Registering is not needed for `SimulationBehaviours` that are on the same `GameObject` as the `NetworkRunner`» ([simulation-behaviour](https://doc.photonengine.com/fusion/current/manual/advanced/simulation-behaviour)).

Исторически физика в multi-peer чинилась дважды: 2.0.2 build 851 (16.08.2024) — «Fixed: Default Unity physics not being autosimulated in multi-peer» ([release notes 2.0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-0)); nightly build 1240 (08.08.2025) — «[Multipeer] Ensure we execute physics for non-default scenes last in FixedUpdate» ([release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)). То есть порядок исполнения физики для non-default сцен между версиями менялся — тест на одной версии не переносится на другую механически.

### 3.4. Переключение пиров: окно редактора и сэмплы

Окно: `Tools > Fusion > Windows > Network Runner Controls` — показать/скрыть ранеры, выбрать, кто собирает ввод, кнопки создания `FusionStats`-оверлея для активного ранера, клик по имени пингует/выделяет ранер в сцене, показывается `NetworkRunner.PlayerRef.PlayerId`, а при назначенном PlayerObject клик по ID выделяет его GameObject ([multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer)). **Данные расходятся:** quickstart сэмпла Projectiles Advanced называет то же окно `Fusion/Windows/Runner Visibility Controls` ([quickstart](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-advanced/quickstart)) — вероятно, разные версии SDK; какой путь в какой версии, официально не сказано.

Горячие клавиши **не универсальны** — у каждого сэмпла свои:

| Сэмпл | Сколько пиров | Переключение | Источник |
|---|---|---|---|
| Projectiles Advanced | `Start Host` стартует «the specified number of clients, which is 2 by default» | numpad `0/1/2/3` | [quickstart](https://doc.photonengine.com/fusion/current/technical-samples/projectiles-advanced/quickstart) |
| BR200 | `StandaloneManager.ExtraPeers = 2` → 3 пира | numpad `1/2/3` — переключение пира; numpad `4/5/6` — показать все инстансы игрока сразу | [br200/testing](https://doc.photonengine.com/fusion/current/game-samples/br200/testing) |
| AoI Multi-Peer (2.0.5, 04.03.2025) | поле **Number of Clients** в главном меню; `BeginMultipeerSession()` крутит `for (int i = 0; i < PlayersToSpawn; i++)`, каждый ранер стартует с `PlayerCount = 100` | — | [fusion-multi-peer-area-of-interest](https://doc.photonengine.com/fusion/current/technical-samples/fusion-multi-peer-area-of-interest) |

AoI-сэмпл — самый полный документированный стенд: требует `Peer Mode = Multiple` **и** `Replication Features = Scheduling and Interest Management`, на префабе ранера висят `Runner Enable Visibility`, `Runner AoI Gizmos`, `Runner AoI Display`, `Fusion Stats`, а `EnableOnSingleRunner` настроен с `Preferred Runner = Client` ([там же](https://doc.photonengine.com/fusion/current/technical-samples/fusion-multi-peer-area-of-interest)). Interest Management разобран в [гл. 08](08-chapter.md).

**Максимальное число пиров официально не документировано** — ни на странице Multi-Peer Mode, ни в конфиге. Единственный документированный ограничитель — не технический, а тарифный: «Note, you should not exceed your project's number of CCU» ([AoI sample](https://doc.photonengine.com/fusion/current/technical-samples/fusion-multi-peer-area-of-interest)). Цен и CCU-чисел вводная страница Fusion не печатает: там только «Fusion is free for development» и оплата «only when your game becomes popular beyond our free tier» ([fusion-intro](https://doc.photonengine.com/fusion/current/getting-started/fusion-intro)).

---

## 4. Четыре способа поднять N клиентов локально

| | Fusion Multi-Peer | Unity MPPM 3.0 | ParrelSync | Полные билды |
|---|---|---|---|---|
| Что это | N `NetworkRunner` в одном процессе | главный Editor Player + до 3 доп. инстансов редактора | копия папки проекта на симлинках | отдельные приложения |
| Требования | `Peer Mode = Multiple` | **Unity 6.3 LTS или новее**, только Windows/Mac | тестирован на 2022.3.56f1 / 2021.3.29f1 / 2020.3.1f1 LTS | — |
| Лимит | официально не документирован; ориентир — CCU проекта | «up to four total editor Players» (+ до 4 локально собранных инстансов) | не ограничен, ограничен ресурсами машины | — |
| Цена | нужен код без статиков/синглтонов | нельзя создавать/менять GameObject в доп. инстансе | второй полный редактор: свои `Library`, `Temp`, `obj` | медленный цикл |
| Доступен для стека курса (Unity 2022.3) | **да** | **нет** | да | да |
| Поддержан Photon | да, есть страница manual | упоминается в release notes и на странице breakpoint support | в документации Photon не упоминается | — |

Источники: [multipeer](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer); [MPPM 3.0](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@3.0/manual/index.html); [ParrelSync](https://github.com/VeriorPies/ParrelSync); [breakpoint-support](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/breakpoint-support).

**MPPM: что изменилось к 2026.** Актуальна версия пакета **3.0.0**, требование — «Unity 6.3 LTS or later» (старое требование «6000.0.22f1+» относилось к ветке 1.4.x и неактуально), платформы — «Windows, Mac platforms». Терминология сменилась: вместо «Virtual Player» — «additional Editor instance», и ограничение формулируется как «You can't create or change the properties of GameObjects in an additional Editor instance»; правки в главном Editor Player сбрасываются при выходе из Play Mode, и из дополнительных инстансов нельзя обращаться к функциональности главного ([MPPM 3.0](https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@3.0/manual/index.html)). Поскольку Fusion официально поддерживает Unity 6.3.x ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)), пара **Fusion 2.1.x + MPPM 3.0 на Unity 6.3 LTS** сходится; на Unity 2022.3 из курса MPPM недоступен в принципе.

**Fusion ↔ MPPM.** Отдельной страницы в manual **нет** (раздел Testing and Tooling содержит ровно пять страниц: Fusion Statistics 2.0 и 2.1, Multi-Peer Mode, Simulating Network Conditions, Breakpoint Support). Интеграция прослеживается только по чейнджлогу: 2.0.1 build 835 (22.05.2024) — поддержка MPPM и поле «`FusionBoostrap.AutoConnectVirtualInstances` (true by default) - if MPPM is installed, virtual instances will join the main instance once it starts a session» (имя напечатано **с опечаткой** — `Boostrap`); 2.0.2 build 851 — «MPPM now supports starting FusionBootstrap in shared mode»; nightly build 1240 — «Added `FusionMppmCommand.ExecuteAsync`» (свои команды в виртуальные инстансы); 2.1.3 build 2390 — фикс `NetworkProjectConfig.Global` под MPPM ([release notes 2.0](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-0), [2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)).

**Про сам Fusion Bootstrap:** страницы в manual Fusion 2 нет — `.../manual/fusion-bootstrap` отдаёт HTTP 404 (проверено). Компонент существует (его чинят: «FusionBootstrap can now correctly start in Single Peer with the Auto setting (previously created 0 clients)», nightly build 744, 22.08.2023), но набор полей и кнопок официально не описан. Документированный путь прототипирования — другой: правый клик в Hierarchy → `Fusion > Scene > Setup Networking in Scene`, что «adds a `Prototype Runner` and a `Prototype Network Start` to the scene», где `Prototype Network Start` — «a prototyping component that contains a bootstrap GUI for quickly joining into a Fusion room»; в Play Mode туториал Shared Mode Basics называет **только** кнопку `Start Shared Client` ([2-scene-and-player](https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/2-scene-and-player)). Кнопку `Start Host` в multipeer документирует quickstart сэмпла, а не этот туториал.

**Тестовые билды (из BR200).** «A public build is any build which contains `-public` in `PhotonAppSettings` => `App Version`» (пример — `1.0-public`); часть функций в таком билде отключена, а обход — «Moderators can bypass this limitation by launching the game with the `-moderator` command line argument». Документация сама предупреждает: «Be aware that this solution uses security by obscurity and must be removed from the code at some point» ([br200/testing](https://doc.photonengine.com/fusion/current/game-samples/br200/testing)).

---

## 5. Отладка: статистика, брейкпоинты, плохая сеть

### 5.1. Fusion Statistics — и почему версия важна

**Общее для 2.0 и 2.1:** «Fusion Statistics will only collect statistics data when using the DEBUG DLL». Компонент `FusionStatistics` должен быть **на том же GameObject, что `NetworkRunner`** ([statistics 2.0](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.0), [statistics 2.1](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1)).

| | Fusion Statistics **2.0** (релевантно курсу) | Fusion Statistics **2.1** |
|---|---|---|
| Включение | компонент до старта, либо кнопка «Setup Statistics Panel» в инспекторе/из кода | компонент; кнопка в Network Runner Controls; `NetworkRunner.SetupStatistics()` / `RemoveStatistics()` |
| Структура UI | одна панель с дропдауном добавления графиков | страницы: Simulation, Network, Memory, Behaviour, Object, Lag Compensation, Time + debug-страница Forecast Objects |
| Пороги | три кастомных порога на стат; ниже 1-го — зелёный, 1–2 жёлтый, 2–3 оранжевый, выше 3-го — красный; `FusionStatistics.SetStatsCustomConfig()` | на странице 2.1 не описаны |
| Мировое пространство | чекбокс **Use World Anchor** + компонент `StatisticsWorldAnchor` на любом GameObject | метод `NetworkRunner.SetStatisticsWorldAnchor(Anchor)`; `null` возвращает канвас в screen space |
| Пер-объектная статистика | компонент `NetworkObjectStatistics` на `NetworkObject` | страница **Object**, кнопка «Add Object», показываются только первые десять |
| Чтение из кода | **не задокументировано** | `Runner.TryGetFusionStatistics(...)` (это и есть breaking change при переходе с 2.0) |
| Свёрнутая панель | «no statistics are collected and the charts are cleaned up» | сворачивание есть, про остановку сбора не сказано |
| Multi-peer | на панели метка локального `PlayerRef` — «useful with multi-peer» | «When using Multi-Peer, a button will be available to switch between statistics panels» |

Чтение значений в 2.1 (код из документации):

```csharp
if (Runner.TryGetFusionStatistics(out var statisticsManager)) {
    var rtt = statisticsManager.SimulationSnapshot.Stats[FusionStatType.RoundTripTime];
    var generalMemoryFree = statisticsManager.MemorySnapshot.GeneralAllocatorMemorySnapshot.TotalBytesFree;
}
```

Что показывают страницы 2.1: **Simulation** — Forward Ticks, Re-simulation Ticks, Objects Update In/Out; **Network** — RTT, In/Out Bandwidth и Packets, Input Bandwidth; **Memory** — общая память и два аллокатора, «at 100% Fusion has no memory left», правится в `NetworkProjectConfig`; **Behaviour** — число вызовов и время на `NetworkBehaviour`/`SimulationBehaviour` отдельно для FUN или `Render`, первые десять; **Lag Compensation** — процент использования хитбоксов (см. [гл. 06](06-chapter.md)); **Time** — дельта между приёмами стейта и interpolation offset (см. [гл. 05](05-chapter.md)). Графики помечены `(U)` (на апдейт) или `(S)` (в секунду), переключение — клик по графику, RTT всегда per update. Debug-страница **Forecast Objects** включается добавлением префаба `ForecastObjectView` в список Statistics Pages в Fusion Hub. Конфиг — `Photon/Fusion/Runtime/Statistics/Resources/FusionStatsResources/FusionStatisticsDefaultConfig.asset` либо страница Fusion Hub ([statistics 2.1](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1)).

> Компонент `FusionStatistics` в курсе **есть** — на `[Runner]` в разделе Interest Management, но с `_statsEnabled: 0`, то есть выключенным ([гл. 08](08-chapter.md)). Как инструмент он не используется.

### 5.2. Поддержка брейкпоинтов (появилась ровно в версии курса)

Введена в **Fusion SDK 2.0.5** — то есть доступна в стеке курса, но курсом не упоминается. Ограничения: только Client-Server режимы (Host, Server, Client) — в Shared не работает; все пиры на одной машине, «using either Fusion Multi-Peer Mode or Unity Multiplayer Play Mode (MPPM)»; только Debug-сборки, в Release отключено.

Рецепт из документации:

1. «Increase the `Network / Connection Timeout` to the max time for debugging purposes on the `NetworkProjectConfig`.»
2. Настроить окружение: Multi-Peer, MPPM, либо ничего, если ни то ни другое.
3. Поставить брейкпоинт и приаттачиться к процессу Unity.
4. Запустить игру и дождаться срабатывания.

Механика — расширение фичи Photon Cloud Connection Lost, которая «allows Client-Server peers to stay connected even if the connection with the Photon Cloud ceases». Ожидаемое поведение: «You should be able to see the peers still connected and the game freezing while the breakpoint is active»; после снятия брейкпоинта игра продолжается — пока не истёк таймаут ([breakpoint-support](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/breakpoint-support)). Дефолтное значение `Connection Timeout` страница `network-project-config` в извлекаемом тексте **не печатает** — ставь максимум и возвращай обратно перед релизом.

### 5.3. Симуляция плохой сети

Требует debug-сборки: встроенная симуляция «available when using the debug version of the `fusion.dll`», переключение — `Fusion > Toggle Debug Dlls`, может потребоваться рестарт редактора. Работает ли она только в редакторе или и в debug-билдах — **страница не говорит** ([simulating-network-conditions](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/simulating-network-conditions)).

Поля фолдаута `Network Conditions` в `NetworkProjectConfig`:

| Поле | Смысл | Диапазон |
|---|---|---|
| `Enabled` | включить симуляцию | — |
| `Delay Shape` | форма осциллятора между Min и Max | — |
| `Delay Min` / `Delay Max` | границы задержки | «in seconds, ranging from 0 to 0.5» |
| `Delay Period` | период осцилляции, сек | — |
| `Delay Threshold` | значения формы ниже порога обнуляются → результат равен `Delay Min` | форма даёт 0..1 |
| `Additional Jitter` | случайная добавка от 0 до этого значения | — |
| `Loss Chance Shape` | форма осциллятора потерь | — |
| `Loss Chance Min` / `Max` | «0 = 0% chance of being lost. 1 = 100% chance of being lost» | 0..1 |
| `Loss Chance Threshold` | ниже порога → результат равен `Loss Chance Min` | — |
| `Loss Chance Period` | период осцилляции, сек (в описании поля опечатка «delay») | — |
| `Additional Loss` | случайная добавка к нормализованной вероятности потери | — |

Диапазоны `Delay Min/Max` 0–0.5 с и `Loss Chance` 0–1 — со страницы [network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config); описания полей — со страницы [simulating-network-conditions](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/simulating-network-conditions), которая числовых диапазонов для задержки не даёт. Фиксированное значение задаётся как `Min = Max`.

**Арифметика, которую нельзя угадать:**

- Host/Server: «The built-in Network Conditions produce 1/2 of the indicated values on each peer» — 200 ms это ~100 ms на сервере и ~100 ms на клиенте, суммарно близко к настройке.
- Shared: симуляцию применяют только клиенты, поэтому «the values should be doubled for Shared Mode testing».
- В любом режиме «these values are being ADDED to already naturally occuring latency» — симулированное складывается с реальным.

На release DLL встроенного механизма нет: для Windows документация рекомендует **Clumsy** с фильтром `udp`, предупреждая, что он режет весь UDP-трафик, а не только Fusion ([simulating-network-conditions](https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/simulating-network-conditions)).

### 5.4. Числа из `NetworkProjectConfig`, которые влияют на тест

Слаг страницы — `network-project-config` (не `-configuration`). Что на ней действительно напечатано ([network-project-config](https://doc.photonengine.com/fusion/current/manual/network-project-config)):

| Настройка | Что сказано |
|---|---|
| Client & Server Tick Rate | «the tick rate at which servers & clients are updated»; «starting in Fusion 2.1 it can be set to any value between 8 and 256» (до 2.1 — закрытый набор значений) |
| **Shared Mode** tick & send rate | «in Shared Mode the following tick & send rate settings cannot exceed `32`!»; в Fusion 2 значения были залочены (32 и 16) и менять их было нельзя, с 2.1 — настраиваемы в пределах этого лимита. Жёсткая фиксация 32/16 — это **Shared Mode в 2.0**, не Host/Server |
| Client/Server Send Rate | интервал 1–8; «A value of 1 means data is sent every tick»; допустимы только интервалы, делящие выбранный tick rate без остатка |
| Переопределение извне | Shared-режимные tick/send rate «can be overridden for all sessions via the dashboard» либо через Webhooks — чтобы «ensure a malicious client cannot set an undesired value» |
| Player Count | «ranging from 1 to 255» |
| Heap → Page Shift | «ranges from 1 KB to 256 KB at powers of 2» |
| Heap → Page Count | «Ranging from 16 to 4096» |
| Replication Features | None / Scheduling / Scheduling and Interest Management; **Scheduling — дефолт** |

**Чего на странице нет:** дефолтных значений tick rate, send rate, Player Count и `Connection Timeout` — они в извлекаемом тексте не печатаются. Числа вида «TickRate 60 / DefaultPlayers 10» относятся к API-референсу **Fusion 1** и к Fusion 2 не применимы — не подставляй их как дефолты.

---

## 6. Версии: состояние на 01.11.2026

| | Версия | Build | Дата |
|---|---|---|---|
| Ветка 2.1 (stable) | **2.1.3** | 2390 | 17.09.2026 |
| | 2.1.2 | 2279 | 13.08.2026 |
| | 2.1.1 | 2177 | 30.06.2026 |
| Ветка 2.0 (поддерживается) | **2.0.13** | 2379 | 16.09.2026 |
| | 2.0.12 | 1861 | 19.03.2026 |
| Курс | 2.0.5 | 920 | 12.02.2025 ([00-README](00-README.md)) |

Поддерживаемые Unity: **2021.3.45, 2022.3.45, 6.0.x, 6.3.x**. Asset Serialization обязательно `Force Text` (Project Settings > Editor). Fusion 2.1 требует Realtime 5 и **несовместим с Fusion 2.0**; Voice — для 2.1 нужен Unity Voice SDK **Realtime5 2.64**, для 2.0 — Unity Voice SDK 2.64, помеченный как несовместимый с 2.1. Путь обновления: «Projects should be upgraded to the latest `2.0.x` SDK before being upgraded to `2.1.x`» ([sdk-download](https://doc.photonengine.com/fusion/current/getting-started/sdk-download)).

Релевантные для этой главы отличия 2.1 от 2.0: лимит сцен 8 → 31; `SceneRef.FromPath` для не-addressable сцен; `virtual OnSceneInfoChanged`; `FusionMppmCommand.ExecuteAsync`; переработанная статистика с чтением через `TryGetFusionStatistics`. Плюс: `NetworkObject.SendPriority` и `SendPriorityCallback` «are now obsolete, please use `NetworkObject.SetPriority(player, priority)` instead» (Release-Candidate, build 2174, 30.06.2026) — то есть часть нового API в 2.1 устарела внутри самой 2.1 ([release notes 2.1](https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1)).

---

## Чего по этой теме нет в учебном курсе

Курс: Host Mode, Fusion 2.0.5, Unity 2022.3.42f1, мультизапуск через ParrelSync. Провалы здесь системные.

1. **Сцены.** Курс присваивает `StartGameArgs.Scene = SceneRef.FromIndex(активная сцена)` и передаёт `NetworkSceneManagerDefault` ради видимости настроек в инспекторе ([гл. 01](01-chapter.md), [гл. 07](07-chapter.md)) — и на этом тема закрыта. `Runner.LoadScene`/`UnloadScene`, `Runner.IsSceneAuthority`, `NetworkSceneInfo.AddSceneRef`, аддитивная загрузка, лимит сцен, `Version`, `RegisterSceneObjects`, `OnSceneInfoChanged` не встречаются нигде. Первая же попытка сделать «меню → уровень → меню» даст либо вызов `LoadScene` с клиента, либо `SceneManager = null` и `NetworkSceneManagerDummy`.
2. **Scene objects.** Мир курса построен целиком на `Runner.Spawn` и префабах ([гл. 02](02-chapter.md)–[гл. 05](05-chapter.md)), поэтому модели «объект сцены получает `NetworkId` при attach», «master client забирает StateAuthority над scene objects при первой загрузке» и «scene object должен быть запечён» в гайде части I нет. Это прямо ломает типовой паттерн «дверь/зона/точка захвата лежит в сцене».
3. **Multi-Peer.** В курсе он выключен (`PeerMode: 0`), и это зафиксировано как причина использовать ParrelSync ([гл. 01](01-chapter.md)). Курс даже предупреждает: переключишься на Multi-Peer — навесь `EnableOnSingleRunner` на камеру, свет и `AudioListener`. Но весь остальной код написан под «один ранер на процесс», где синглтоны и `Camera.main` работают «правильно». Практический вывод: требование курса «используй `Runner.GetPhysicsScene()` вместо `Physics.*`» ([гл. 02](02-chapter.md), [гл. 03](03-chapter.md), [гл. 08](08-chapter.md)) — это не только про детерминизм, это первый шаг к multi-peer-совместимости, и его надо распространить на камеры, аудио, UI и любое глобальное состояние.
4. **Инструменты.** MPPM в стеке курса невозможен: нужен Unity 6.3 LTS+. Остаются ParrelSync (в документации Photon не упоминается) и Multi-Peer (бесплатный и официальный). Поддержка брейкпоинтов появилась ровно в 2.0.5 — версии курса — но курс о ней молчит, хотя это ответ на самую болезненную проблему сетевой отладки. `Network Conditions` и Fusion Statistics тоже вне курса, хотя статистика в курсе физически присутствует выключенной ([гл. 08](08-chapter.md)), а требуют они лишь переключения на debug DLL. Арифметику «в Host mode каждый пир применяет половину заданной задержки, в Shared — только клиенты» без источника угадать нельзя.
5. **Версии.** Курс на 2.0.5; на 01.11.2026 stable — 2.1.3 и 2.0.13. Для этой темы главное: лимит сцен 8 → 31 и смена API статистики. Читателю на 2.0.x нужна страница Fusion Statistics **2.0**, а не 2.1.

---

## Решение: что брать и когда

```
Нужно загрузить другую сцену по сети?
├─ только одна сцена на весь матч → StartGameArgs.Scene + NetworkSceneManagerDefault, ничего не грузить (как в курсе)
├─ меню → уровень → меню       → Runner.LoadScene(..., Single) под if (Runner.IsSceneAuthority)
├─ persistent-сцена + уровни   → LoadSceneMode.Additive; следи за лимитом (8 в 2.0 / 31 в 2.1.3)
└─ своя логика загрузки (экран загрузки, стриминг, addressables)
                               → свой INetworkSceneManager + InvokeSceneLoadStart / InvokeSceneLoadDone /
                                 RegisterSceneObjects(..., loadId) и САМ навяжи гейт по scene authority

Объект должен быть в сцене или заспавнен?
├─ единственный, управляется хостом, редактируется дизайнером → scene object (включённый, запечённый)
└─ появляется/исчезает по ходу матча                          → префаб + Runner.Spawn (пул через INetworkObjectProvider)

Нужно N клиентов локально?
├─ Unity 2022.3 (стек курса) → Fusion Multi-Peer; ParrelSync — если код не multi-peer-чистый
├─ Unity 6.3 LTS+           → MPPM 3.0 (до 4 инстансов) или Multi-Peer
├─ отладка брейкпоинтами    → Multi-Peer или MPPM + Connection Timeout на максимум, только Host/Server/Client
└─ проверка release/IL2CPP  → полные билды (ни статистики, ни Network Conditions, ни брейкпоинтов)

Нужно воспроизвести плохую сеть?
├─ debug DLL  → NetworkProjectConfig → Network Conditions (Host/Server: ставь двойное от желаемого на пир;
│               Shared: удваивай настройки)
└─ release DLL → Clumsy, фильтр udp (режет весь UDP)
```

---

## Чеклист

1. Передавай `StartGameArgs.SceneManager` всегда, даже если объектов сцены пока нет; `null` → `NetworkSceneManagerDummy` + ошибка в лог.
2. Строй начальный набор сцен через `new NetworkSceneInfo()` + `AddSceneRef(sceneRef, LoadSceneMode)` и передавай его в `StartGameArgs.Scene`; одиночный `SceneRef` оставь для случая «одна сцена на матч».
3. Любой `Runner.LoadScene` / `Runner.UnloadScene` оборачивай в `if (Runner.IsSceneAuthority)`; в кастомном менеджере воспроизведи это ограничение руками.
4. Перед аддитивной загрузкой проверь лимит своей версии: 8 для 2.0.x, 31 для 2.1.3+. Не доверяй числу «8» на странице manual, если ты на 2.1.3.
5. Для перезагрузки сцены делай `UnloadScene` → `LoadScene` и передавай разный `loadId` в `RegisterSceneObjects`, иначе type ID объектов столкнутся.
6. Scene object держи **включённым** в сцене и запечённым; выключенный объект к сети не прикрепится (вывод из формулировки «any enabled Network Objects»).
7. Включи `Peer Mode = Multiple` на раннем этапе проекта и держи код multi-peer-чистым, даже если тестируешь в Single: переделывать потом дороже.
8. Запрети себе статики и синглтоны, влияющие на симуляцию. Состояние матча — `NetworkBehaviour` на scene object, доступ через `Runner`.
9. Вместо `FindObjectOfType` наследуй `SimulationBehaviour`; если он не на GameObject ранера — `NetworkRunner.GetRunnerForGameObject()` + `AddGlobal(this)` / `RemoveGlobal(this)` и `RemoveGlobal` в `OnDestroy`.
10. Навесь `RunnerEnableVisibility` на GameObject/префаб `NetworkRunner`, а `AudioListener` и `EventSystem` вынеси в `EnableOnSingleRunner`.
11. Локальные визуальные объекты, созданные в рантайме, прогоняй через `MoveToRunnerScene(go)`; «вечные» — через `MakeDontDestroyOnLoad`.
12. Имя метода видимости сверяй по IntelliSense: в документации три написания (`SetVisibility`, `SetVisible`, `SetVisiblity`).
13. Планируя число пиров, считай не «лимит Multi-Peer» (его нет в документации), а CCU своего тарифа.
14. Для отладки брейкпоинтами: только Host/Server/Client, все пиры на одной машине, debug-сборка, `Network / Connection Timeout` на максимум — и верни значение обратно перед релизом.
15. Перед тестом «плохой сети» переключись на debug DLL (`Fusion > Toggle Debug Dlls`), а в Host/Server помни: каждый пир применяет половину заданного; в Shared — удваивай значения.
16. Читая статистику, выбери правильную страницу документации: на 2.0.x — Fusion Statistics 2.0 (`Use World Anchor`, `NetworkObjectStatistics`, `SetStatsCustomConfig`, свёрнутая панель не собирает данные), на 2.1.x — Fusion Statistics 2.1 (`TryGetFusionStatistics`, страницы, `SetStatisticsWorldAnchor`).
17. Не измеряй производительность на release-сборке через Fusion Statistics — на release DLL данные не собираются вообще.
18. Не переносись с 2.0.x на 2.1.x через одну ступень: сперва последняя 2.0.x, потом 2.1.x; проверь Realtime 5 и подходящую версию Voice SDK.
19. Не используй `NetworkObject.SendPriority` / `SendPriorityCallback` в 2.1 — они obsolete, замена `NetworkObject.SetPriority(player, priority)`.
20. Любой механизм вида «тестовый билд по подстроке в `App Version` + аргумент `-moderator`» считай временным: документация Photon сама называет это security by obscurity и требует удалить.

---

## Источники

| URL | Что взято |
|---|---|
| https://doc.photonengine.com/fusion/current/manual/scene-loading | весь раздел 1: интерфейс, авторитет Load/Unload, `SceneRef`, лимит 8, `Version`, члены интерфейса |
| https://doc.photonengine.com/fusion/current/manual/network-object | раздел 2: attach, «must be baked (needs instructions here)», `Spawn`, Object Table |
| https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/multipeer | раздел 3: `Peer Mode`, видимость, `EnableOnSingleRunner`, запрет статиков, Network Runner Controls |
| https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.0 | колонка «2.0» таблицы 5.1 |
| https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/fusion-statistics-2.1 | колонка «2.1», состав страниц, `TryGetFusionStatistics`, конфиг-ассет |
| https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/simulating-network-conditions | раздел 5.3 целиком |
| https://doc.photonengine.com/fusion/current/manual/testing-and-tooling/breakpoint-support | раздел 5.2 целиком |
| https://doc.photonengine.com/fusion/current/manual/network-project-config | раздел 5.4: все диапазоны, формулировки Peer Mode |
| https://doc.photonengine.com/fusion/current/manual/advanced/simulation-behaviour | `GetRunnerForGameObject()`, `AddGlobal`/`RemoveGlobal`, исключение для GameObject ранера |
| https://doc.photonengine.com/fusion/current/fusion-100/fusion-102 | раздел 1.3: `AddSceneRef`, `StartGameArgs`, запекание сохранением проекта |
| https://doc.photonengine.com/fusion/current/tutorials/shared-mode-basics/2-scene-and-player | `Setup Networking in Scene`, Prototype-компоненты, `Start Shared Client` |
| https://doc.photonengine.com/fusion/current/technical-samples/projectiles-advanced/quickstart | 2 клиента по умолчанию, numpad 0/1/2/3, Runner Visibility Controls |
| https://doc.photonengine.com/fusion/current/technical-samples/fusion-multi-peer-area-of-interest | стенд AoI, Number of Clients, CCU-предупреждение, `SetVisiblity` |
| https://doc.photonengine.com/fusion/current/game-samples/br200/testing | `ExtraPeers`, numpad 1–6, `-public`, `-moderator` |
| https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-0 | 2.0.0 breaking, `GetMultiplePeerTargetScene`, builds 835/851/744/771/733/719/2379 |
| https://doc.photonengine.com/fusion/current/getting-started/release-notes/release-notes-2-1 | лимит 8 → 31 (build 2390), бейкинг, `FromPath`, `ExecuteAsync` (1240), `SetPriority` (2174) |
| https://doc.photonengine.com/fusion/current/getting-started/sdk-download | таблица версий раздела 6, Unity, Force Text, Realtime 5, Voice SDK |
| https://doc.photonengine.com/fusion/current/getting-started/fusion-intro | «free for development», отсутствие CCU-чисел |
| https://doc.photonengine.com/fusion/v2/getting-started/migration/coming-from-pun2 | StateAuthority над Scene Objects + устаревшесть страницы |
| https://docs.unity3d.com/Packages/com.unity.multiplayer.playmode@3.0/manual/index.html | MPPM 3.0.0, Unity 6.3 LTS+, лимит 4, Windows/Mac |
| https://github.com/VeriorPies/ParrelSync | симлинки, `_clone_x`, протестированные версии Unity |
| https://doc.photonengine.com/fusion/current/manual/fusion-bootstrap | HTTP 404 — страницы Fusion Bootstrap в manual Fusion 2 нет (проверено 01.11.2026) |

**Открытые вопросы (официально не документировано / данные расходятся):**

- Не описаны в прозе, только в примерах кода или чейнджлоге: `Runner.IsSceneAuthority`, `LoadSceneParameters.localPhysicsMode`. API-референс (`doc-api.photonengine.com`) из автоматики не читается.
- Не подтверждено первоисточником: имя `Baker.Bake(GameObject)`; поведение `Camera.main` / `FindObjectOfType` / `Physics.*` в Multi-Peer (раздел 3.3 — авторская методика); поддержан ли ParrelSync со стороны Photon; «scene take-over» как термин.
- Не заявлено числом: максимум пиров в Multi-Peer (ограничитель — CCU тарифа); дефолты `NetworkProjectConfig` (tick rate, send rate, Player Count, `Connection Timeout`); горячие клавиши панели статистики.
- Данные расходятся: путь к окну переключения пиров (Network Runner Controls против Runner Visibility Controls); имя метода видимости (три написания); позиционирование `Peer Mode = Multiple` как «editor only» против dedicated-server use case; лимит сцен 8 в manual против 31 в release notes.
- Не сказано: работает ли встроенная симуляция сетевых условий в debug-билдах или только в редакторе.

# AIKit на Atomic: разбор курса, карта модуля и план перехода

Дата: 2026-11-20. Ветка `claude/aikit-atomic-7209e1` от `game/develop` (`c390412f6`). Статус: **фаза 1 — предложение, кода не меняли.** Код пишется только после «да» пользователя.

Задача пользователя: «взять всю документацию по ИИ на атомарном фреймворке и переработать наш плагин, чтобы он был строго на атомарном фреймворке (там, где это необходимо)».

Источники:
- курсовые проекты `C:\Users\MY PC\Downloads\Extracted 2026-10-31` (10 проектов; прочитаны полностью по ИИ-части);
- документы Platformer-AIKit (копии в [Platformer/](Platformer/README.md)): `Research_AI_Code.md` (разбор того же курса), `AI_Handbook.md`, `AIKit.md`, `AIKit_Flow.md`;
- гайд по Atomic этого репозитория `Docs/AtomicGuide/*`;
- код: `Assets/Modules/AIKit` (≈30 тыс. строк рантайма, 49 тыс. строк тестов, 1672 теста), адаптеры игры `Assets/Game/Scripts/Features/AI`.

---

## Коротко

1. **В курсе алгоритмы ИИ не атомарные.** Utility, GOAP, HTN, BT, FSM, Team AI, навигация, карты влияния — обычные C#-библиотеки (`Plugins/AI/*`, `Modules.*`), а Burst — для сеток и карт влияния. **Атомарен только «клей»:** агент — это сама сущность персонажа, его данные (цель, дистанции, план, роль) — значения сущности по кодогенерированным ключам, решатель — поведение сущности или значение сущности, исполнение — запросы (`MoveRequest`, `FireRequest`, `LookRequest`), которые потребляют те же поведения, что и у игрока, отряды — слой на контексте.
2. **Значит, «строго на Atomic, где нужно» для AIKit = атомарный слой интеграции, а не переписывание ядра.** Ядро (граф, blackboard графа, utility, восприятие, ноды, редактор) остаётся чистым C#: на нём 1672 теста, ассеты графов и редактор, и оно должно переноситься между проектами — ровно как в курсе.
3. Сейчас этот клей у нас **размазан по игре** (`Features/AI/EliteRegax`, `Common`) и сделан под одного Regax. Предлагаю вынести его в модуль отдельной сборкой **`AIKit.Atomic`** (`Assets/Modules/AIKit/Adapters/Atomic`) с ключами `[EntityExtensionsAPI]`, инсталлерами агента и мира, поведениями, атомарным актуатором, мостом «значение сущности → blackboard», сканером целей по миру сущностей. Игра оставляет у себя только своё: тело Regax (мотор, прыжок), укус, урон через `DamageUseCase`, взрывы.
4. После этого любой ИИ-персонаж игры (элитный Regax, а позже — зомби-префаб, если команда `zombie-ai` захочет мозг AIKit) собирается одинаково: `MonoEntity` + `AIAgentInstaller` + свой инсталлер тела.
5. План — 7 шагов, каждый с зелёными тестами и отдельным коммитом. Объём: около 1–1,5 дня работы сессии, ~1 000–1 500 строк нового и перенесённого кода плюс ~400 строк тестов. Орду не трогаем.

---

## 1. Как курс строит ИИ на Atomic

### 1.1. Агент — это сущность персонажа

В `UnityProject (1)`, `UnityProject.v.2`, `UnityProject (2)` бот — тот же `Character.prefab` (`SceneEntity`), что и у игрока. Отдельной «сущности мозга» нет. ИИ подключается дочерним объектом `[AI]` с шестью `SceneEntityInstaller`: игрок и бот отличаются только списком инсталлеров (переопределение `installers.Array.data[2..7]` в сцене).

Инсталлеры бота (версия (2)):

| Инсталлер | Что ставит |
|---|---|
| `BotBlackboardInstaller` | дистанции (`AtDistance`, `NearDistance`, `FarDistance`), точки патруля, слоты союзников |
| `BotSensorInstaller` | 4 поведения-сенсора `EntitySensor` и переменные `TargetEnemy`, `TargetWeapon`… |
| `BotHTNInstaller` | агент HTN + поведение `ProducePlanBehaviour` |
| `BotActionInstaller` | дерево поведения (BT) и таблица «имя оператора → узел» |
| `BotExtensionsInstaller` | когда перепланировать (смена цели, остановка дерева, период) |
| `TeamAIInstaller` | намерение «атаковать» для отрядов, `RoleType` |

### 1.2. Данные ИИ — значения сущности по кодогенерации

Отдельный файл кодогенерации на слой: `EntityAIAPI.yml` рядом с `EntityAPI.yml` (ядро) и `EntityViewAPI.yaml` (вид). У ИИ:

```yaml
values:
  - Waypoints: Transform[]
  - StoppingDistance / AtDistance / NearDistance / FarDistance: IValue<float>
  - TargetEnemy / TargetWeapon / TargetAmmo / TargetHealing: IReactiveVariable<IEntity>
  - HTNAgent: HTNAgentDecorator          # сам решатель — значение сущности
  - PlanRequest: IRequest                # «перепланируй»
  - PlanEvent: IEvent<string[], int>     # «вот новый план»
  - BehaviourTree: BehaviourNodeSequence
  - RoleType: IReactiveVariable<string>  # роль в отряде (её показывает UI)
```

Цели — `IReactiveVariable<IEntity>`, настройки — `IValue<float>`/`Const<float>`, команды — `IRequest`/`IEvent`. **Отдельного blackboard-объекта в атомарных проектах нет: blackboard — это значения сущности.** Отдельный `Blackboard` с int-ключами и своей кодогенерацией есть только в старом плагине `Конец урока/Plugns/AI`, и он вообще без Atomic (MonoBehaviour `SceneBlackboard`).

### 1.3. Решатель — поведение сущности над чистым C#-агентом

- `UtilityAgent`, `GoapAgent`, `HTNAgent` — чистые классы из `Plugins/AI`; инсталлер строит их кодом и отдаёт в поведение (а с версии (2) ещё кладёт значением сущности, чтобы роль отряда могла подменить домен).
- Поведения тикают в `FixedUpdate`: `DecisionBehaviour` (utility раз в случайный период), `ProducePlanBehaviour` (план только по `PlanRequest.Consume()`, результат — `PlanEvent`), `ConsumePlanBehaviour` (собирает BT из плана по таблице имён).
- Перепланирование — подписки в инсталлере: смена `TargetEnemy` → `PlanRequest`; дерево остановилось или истёк период → `PlanRequest`.
- FSM-уроки: `StateMachine<StateName>` — значение сущности, тикается подписками `entity.WhenEnable/WhenDisable/WhenFixedUpdate`; состояния ловят `IEntity` в конструкторе, условия — замыкания по значениям сущности (`() => entity.GetTarget().Value != null`).

### 1.4. Исполнение — запросы, общие с игроком

Лист BT/состояние FSM не двигает тело, а пишет запрос:

```csharp
_entity.GetLookRequest().Invoke(direction);
_entity.GetFireRequest().Invoke();
return BehaviourResult.RUNNING;
```

Запросы потребляют обычные поведения ядра (`MoveBehaviour`, `FireBehaviour`: `Consume` → условие → действие → событие). **Игрок и бот идут через один конвейер действий.** Подбор, лечение, перезарядка в курсе вызывают use-case напрямую — это непоследовательность, а не правило.

### 1.5. Сенсоры

- Поведение-сенсор на сущности: `OverlapSphere` раз в тик, предикат по тегам кодогенерации (`other.HasCharacterTag() && TeamUseCase.IsEnemies(other, entity)`), результат — в `IReactiveVariable<IEntity>`.
- Добытчик (`Конец Урока (2)`) ищет ближайшее по тегу в мире сущностей: `context.GetEntityWorld().GetAllWithTag(tag)`.
- `UnityProject`: пространственное дерево — значение контекста, поведение `SpatialBehaviour` регистрирует сущность, система контекста `SpatialUnitSystem` его пересобирает.
- Второй слой — сенсоры мира планировщика: читают значения сущности и пишут символьные факты (`DistanceType { At, Near, Far }`).

### 1.6. Отряды и мир — слой на контексте

`PendingTeamManager` и `ActiveTeamManager` — значения `GameContext`, тикаются `context.WhenFixedUpdate`. Агент заявляет намерение поведением `AttackIntentBehaviour`. Роль подменяет домен HTN (`member.GetHTNAgent().OverrideDomain(...)`) и пишет `RoleType`.

### 1.7. Что в курсе не Atomic вовсе

| Часть | Как сделано |
|---|---|
| навигация по сетке (A*, JPS, Dijkstra, волна), GOAP-модуль, GroundRTS (`Конец урока (3)`) | чистый C#, свои asmdef и тесты; в игру — статическим полем и MonoBehaviour |
| карты влияния (`InfluenceMap (JobSystem)`) | `partial struct` над `NativeArray<float>`, `[BurstCompile] IJobParallelFor`, `…AsJob(depends)` |
| пространственные деревья (`UnityProject`) | чистый C#; в Atomic — значением контекста + поведение/система |
| плагин Blackboard/BT/FSM в `Конец урока` | MonoBehaviour + `[SerializeReference]`, без Atomic (BT там закомментирован целиком) |

### 1.8. Чего у курса не повторять

- Связь план → действие только строками (`nameof`), `_nodes[name]` бросает исключение на опечатке.
- `unsafeAccess: true` в кодогенерации: забыли инсталлер — падение без внятной ошибки.
- Анонимные поведения-лямбды в инсталлерах (`WhenFixedUpdate(dt => ...)`): их не видно и не снять.
- Состояние (кулдауны) в полях инсталлера.
- Синглтон `GameContext.Instance` из поведений.
- 4 `OverlapSphere` на бота каждый `FixedUpdate`, «первый», а не «ближайший».
- Удаление из списка в прямом цикле (Team AI пропускает участника).
- `Debug.Log` и LINQ в тике, `new` при каждой смене роли.
- Значения, прочитанные в конструкторе (`_attackDistance = entity.GetRangeDistance().Value`), — реактивность теряется.

---

## 2. Где AIKit сейчас

```
СЕНСОРЫ ──► BLACKBOARD ──► ГРАФ (мозг → решения → цепочки) ──► УМЕНИЯ/ЗАДАЧИ ──► НАВИГАЦИЯ ──► АКТУАТОР ──► ТЕЛО
```

- Ядро `AIKit` (asmdef без ссылок): `AICharacter` (MonoBehaviour-оркестратор, 13 partial-файлов), `AgentContext`, `Blackboard` (типизированные слоты по int-id, ревизии, события), граф (`BehaviorGraph` + `BehaviorGraphInstance` + `NodeMemory`), ассет `BehaviorGraphAsset` + сериализатор + валидатор, 55 файлов нод, utility, восприятие, умения, отряды, объекты мира, `AIWorld` (общий blackboard, расписание решений, ростер, жетоны).
- Адаптеры модуля: `AIKit.NavMesh` (прототип), `AIKit.Animation` (Mecanim).
- Редактор графа (34 файла, 13 тыс. строк) и тесты (203 файла).

Клей игры (`Game.AIKit.Adapters`):

| Где | Что | Уже Atomic? |
|---|---|---|
| `Common/LevelAIInstaller` | сущность уровня: `LevelNav`, `AIWorld` + `AIAgentList`, `PlayerTank`, режиссёр арены | да: `MonoEntityInstaller`, ключи `AIAPI` |
| `Common/AIWorldBehaviour` | шаг мира и всех агентов (`IEntityTick` сущности уровня) | да |
| `Common/AIAgentList`, `IAIAgent` | порядок шага агентов, бюджет решений | чистый C# (общий, не про Regax) |
| `EliteRegax/EliteRegaxInstaller` | тег + `EliteRegaxBehaviour` | да |
| `EliteRegax/EliteRegaxBehaviour` | связывает `AICharacter` с сущностью: тело, мир, укус, урон → `TakeHit`, взрыв → **прямо в blackboard** | наполовину: смесь общего (мир, урон, смерть) и своего (мотор, взрыв) |
| `EliteRegax/AtomicActuator` | «геймпад» → `MoveDirection`/`RotateDirection` сущности | да, но лежит в игре, хотя почти общий |
| `EliteRegax/AtomicHitReceiver` | удар AIKit → `DamageUseCase.TakeDamage` | да, это игра |
| `EliteRegax/EliteRegaxBody`, `EliteMotor`, `VoxelGridNavigator`, `PlayerTankScanner`, `RegaxBiteRunner`, ноды `Nodes/` | тело, навигация по воксельной сетке, укус, ноды игры | своё для игры |

Что мешает «атомарности» сейчас:
1. Общий клей (мир, список агентов, актуатор, «агент ↔ сущность») не в модуле, а в папке Regax: второй ИИ-персонаж пришлось бы собирать копированием.
2. Игра пишет в blackboard агента напрямую (`board.Set(_blastCount, ...)` в `EliteRegaxBehaviour`) — данные мимо сущности.
3. Состояние агента (решение, цель, «думает») снаружи не видно как значения сущности: виду, звуку, HUD приходится лезть в `AICharacter`.
4. Ключ мира не объявить (`AIAPI`: «generated extensions name its type `AIKit.IAIWorld`, which inside `Game.AIKit.Adapters` means the wrong namespace») — в сборке модуля этой проблемы нет.

---

## 3. Карта: что становится Atomic, а что нет

| Часть AIKit | Решение | Почему |
|---|---|---|
| **Агент** (`AICharacter`) | остаётся компонентом ядра; к сущности его привязывает `AIAgentInstaller` + `AIAgentBehaviour` | как в курсе: решатель — чистый агент, Atomic владеет жизненным циклом. Превращать `AICharacter` в не-MonoBehaviour — 34 файла с `AddComponent/GetComponent<AICharacter>`, инспектор, визард; выгоды для игры нет (см. шаг 7, необязательный) |
| **Жизненный цикл агента** (вход в мир, выход при смерти, шаг) | **Atomic**: `AIAgentBehaviour : IEntityInit, IEntityDispose` | сейчас это делает `EliteRegaxBehaviour` руками; общее для любого ИИ-персонажа |
| **Мир и расписание** (`AIWorld`, `AIAgentList`, шаг) | **Atomic**: `AIWorldInstaller` (часть сущности уровня) + `AIWorldBehaviour` (`IEntityTick`) — перенос из игры в модуль | курс держит общий ИИ-слой на контексте; у нас — на сущности уровня `[LevelAI]`, это уже работает |
| **Данные агента наружу** (цель, текущее решение, «думает», смена решения) | **Atomic**: значения `AITarget : IReactiveVariable<Transform>`, `AIDecision : IReactiveVariable<string>`, событие `AIDecisionChanged` | как `TargetEnemy`, `RoleType` курса: вид/звук/HUD подписываются на сущность, не на `AICharacter` |
| **Команды агенту** (подумать сейчас, удар получен) | **Atomic**: `AIThinkRequest : IRequest`, `AIHit : IEvent<HitInfo>`; источники урона игры пишут в сущность | как `PlanRequest` курса; сейчас `RequestThink()`/`TakeHit()` зовут напрямую |
| **Факты игры для графа** (взрыв рядом и т. п.) | **Atomic → blackboard**: значение сущности + мост `AIBlackboardBinding` (переменная сущности → переменная графа по имени, раз за шаг или по изменению) | игра пишет в сущность (как в курсе), граф читает свой blackboard (как сейчас). Регистры графа не превращаются в значения сущности |
| **Актуатор** | **Atomic**: общий `EntityActuator` в модуле: пишет в переданные `IReactiveVariable<Vector3>` движения и поворота, только при изменении; прыжок/рывок — через расширения игры | `AtomicActuator` уже это делает, но лежит в игре. Ключи `MoveDirection`/`RotateDirection` — игры, поэтому инсталлер игры передаёт сами переменные (модуль не знает ключей игры) |
| **Сенсор целей по сущностям** | **Atomic**: `EntityTargetScanner : ITargetScanner` по `IEntityWorld`/`EntityFilter` с предикатом по тегу | курс: предикат по тегам кодогенерации; у нас бюджет — без `OverlapSphere` на агента |
| **Удар AIKit → урон игры** | остаётся в игре (`AtomicHitReceiver`) | это `DamageUseCase` игры, модуль его не знает |
| **Граф, ассет, сериализатор, валидатор, редактор** | **не Atomic** | данные и инструмент; один граф на всех агентов; 1672 теста |
| **Blackboard графа** | **не Atomic** | переменные объявляет ассет графа по именам во время работы — кодогенерация их не знает; значения сущности хранятся как `object` (боксинг на каждую запись `float`/`Vector3` — против `Performance.md`); слоты с ревизиями нужны окну графа (живая доска, точки останова) |
| **Ноды, решения, utility, умения, восприятие (математика), отряды** | **не Atomic** | чистая логика, как `Plugins/AI` курса; тесты без сцены |
| **Навигация** (`INavigator`, `VoxelGridNavigator`, `LevelNav`) | **не Atomic**; сетка — значение сущности уровня (уже так) | как навигация курса; сетка уровня общая с ордой |
| **Орда** (`Features/AI/Horde`, Burst/VAT) | **не трогаем** | отдельная система толпы (справочник §26), её ведёт `zombie-ai` |
| **Карты влияния** (если появятся) | Burst `struct` над `NativeArray`, значение сущности уровня | как `InfluenceMap (JobSystem)` |
| **Анимация** (`AIKit.Animation`) | не трогаем | Regax анимирует игра по `MoveDirection`; Mecanim-адаптер нужен только демо |
| **Отладка** | **Atomic**: `AIAgentGizmos : IEntityGizmos` (цель, путь, решение) | идиома гайда «гизмо как поведение сущности» |

Главный принцип: **сущность владеет агентом и его входами/выходами; агент владеет своей памятью и решениями.** Две стороны соединяют ровно три шва: актуатор (выход движения), значения/события сущности (вход фактов и команд) и значения состояния (выход для вида).

---

## 4. Целевая архитектура

```
Assets/Modules/AIKit/
├── Runtime/Core/                 AIKit — без изменений по смыслу (чистый C#)
└── Adapters/Atomic/              AIKit.Atomic (новая сборка: AIKit + Atomic.Entities + Atomic.Elements)
    ├── AIKitAPI.cs               [EntityExtensionsAPI]: ключи агента и мира
    ├── Agent/
    │   ├── AIAgentInstaller.cs   MonoEntityInstaller: AICharacter → значения + AIAgentBehaviour
    │   ├── AIAgentBehaviour.cs   IEntityInit/IEntityDispose/IAIAgent: мир, шаг, команды, состояние наружу
    │   ├── EntityActuator.cs     IAgentActuator над IReactiveVariable<Vector3> движения и поворота
    │   ├── AIBlackboardBinding.cs  значения сущности → переменные графа
    │   └── AIAgentGizmos.cs      IEntityGizmos
    ├── World/
    │   ├── AIWorldInstaller.cs   часть сущности уровня: AIWorld + AIAgentList + AIWorldBehaviour
    │   ├── AIWorldBehaviour.cs   IEntityTick: BeginStep + шаг агентов (перенос из игры)
    │   ├── AIAgentList.cs, IAIAgent.cs   (перенос из игры)
    └── Perception/
        └── EntityTargetScanner.cs  ITargetScanner по миру сущностей и фильтру
```

Ключи (`AIKitAPI`, по правилу `Project.md` «Ключи сущностей»):

```csharp
[EntityExtensionsAPI]
public static partial class AIKitAPI
{
    // агент
    public static readonly TagKey<IEntity> AIAgent = new(nameof(AIAgent));
    public static readonly ValueKey<IEntity, AICharacter> AICharacter = new(nameof(AICharacter));
    public static readonly ValueKey<IEntity, IReactiveVariable<Transform>> AITarget = new(nameof(AITarget));
    public static readonly ValueKey<IEntity, IReactiveVariable<string>> AIDecision = new(nameof(AIDecision));
    public static readonly ValueKey<IEntity, IRequest> AIThinkRequest = new(nameof(AIThinkRequest));
    public static readonly ValueKey<IEntity, IEvent<HitInfo>> AIHit = new(nameof(AIHit));
    // мир (на сущности уровня)
    public static readonly ValueKey<IEntity, IAIWorld> AIWorld = new(nameof(AIWorld));
    public static readonly ValueKey<IEntity, AIAgentList> AIAgents = new(nameof(AIAgents));
}
```

Как собирается элитный Regax после перехода:

```
MonoEntity Regax
├── RegaxInstaller (ядро: ходьба, здоровье, оружие)          — без изменений
├── AIAgentInstaller (модуль)                                  — агент, мир, команды, состояние, гизмо
│     актуатор: EntityActuator(entity.GetMoveDirection(), entity.GetRotateDirection())  ← передаёт игра
│     факты: BlastCount/BlastPoint/BlastTime ← значения сущности
└── EliteRegaxInstaller (игра)                                 — тело Regax (мотор, прыжок), укус, взрывы → значения
```

Шаг по-прежнему один на кадр и в одном порядке: `[LevelAI]` → `AIWorldBehaviour.Tick` → `world.BeginStep(dt)` → `agent.Step(dt)` по списку. `AICharacter.Simulation = Manual` ставит `AIAgentBehaviour`. Так сохраняются бюджет решений (`MAX_DECISIONS_PER_STEP = 8`), детерминизм (сид мира) и пауза `GameLoop`.

Правила для нового кода (из разбора курса и `Performance.md`):
- значения сущности берутся в `Init`, а не в тике; подписки снимаются в `Dispose` (пул трупов);
- никаких лямбд-поведений в инсталлерах — только именованные поведения;
- никаких синглтонов из поведений: мир берётся у сущности уровня (`LevelAI.Ensure()` — только в `Init`, как сейчас);
- запись в реактивные значения — только при изменении (их слушает аниматор);
- без мусора в кадре: проверка `Is.Not.AllocatingGCMemory()` для шага агента через сущность.

---

## 5. План перехода

Каждый шаг — отдельный коммит; после каждого: компиляция в batchmode, `AIKit.Tests` (1672), `Game.AIKit.Adapters.Tests`, сценовый `EliteRegaxSandboxTests` в живом свёрнутом редакторе (только он ловит поведение в песочнице).

| Шаг | Что | Что проверяет |
|---|---|---|
| **1. Сборка и ключи** | `Adapters/Atomic/AIKit.Atomic.asmdef`, `AIKitAPI` с ключами; проверить, что генератор Atomic видит сборку модуля | компиляция; новый тест: ключи создаются, расширения `GetAICharacter()` есть |
| **2. Мир в модуль** | перенести `AIAgentList`, `IAIAgent`, `AIWorldBehaviour` в `AIKit.Atomic` + `AIWorldInstaller`; `LevelAIInstaller` игры зовёт его; ключ `WorldAgents` игры → `AIAgents`/`AIWorld` модуля | `AIAgentListTests` (перенесены в тесты модуля), `ArenaDirectorTests`, сценовый тест Regax |
| **3. Актуатор** | общий `EntityActuator` в модуле; `AtomicActuator` игры = `EntityActuator` + прыжок (`ILeapActuator`) | `AtomicActuatorTests` без изменений смысла |
| **4. Агент на сущности** | `AIAgentInstaller` + `AIAgentBehaviour`: вход/выход из мира, `Manual`, `AIThinkRequest`, `AIHit` → `TakeHit` на следующем шаге, `AIDecision`/`AITarget` наружу; `EliteRegaxBehaviour` теряет общий код (≈ −80 строк) | новые тесты на чистой `Entity` без сцены; сценовый тест Regax; тест «без мусора» на шаг |
| **5. Факты через сущность** | `AIBlackboardBinding`; взрыв пишет `BlastCount/Point/Time` в значения сущности, граф видит их как раньше | `BlastReaction` и сценовые тесты взрыва; тест моста |
| **6. Сенсор по сущностям и гизмо** | `EntityTargetScanner` (по `EntityFilter`), `AIAgentGizmos`. Regax оставляет `PlayerTankScanner` (цель — танк игрока, без перебора) | тесты сканера на мире из 3 сущностей |
| **7. Документы** | `README.md` модуля: раздел «Atomic»; `PORTING.md`: новая сборка; этот файл — статус | — |
| *8. (необязательно, не предлагаю сейчас)* | ядро агента без MonoBehaviour (`AIAgentCore`), `AICharacter` — его обёртка; тогда агент живёт на любой `Entity` без GameObject | 34 файла тестов/редактора; нужен только для агентов без объекта сцены |

Порядок выбран так, чтобы игра работала после каждого шага: сначала добавляем модульный слой рядом, потом переводим на него игру и только потом убираем дубли из игры.

---

## 6. Риски

| Риск | Что делаем |
|---|---|
| Генератор Atomic (`EntityAPIGenerator`, Roslyn) может не обрабатывать сборку в `Assets/Modules` | шаг 1 проверяет это первым; если не видит — ключи и расширения пишем руками в том же виде, что генерирует Atomic (как запасной путь) |
| Переименование ключа мира ломает сохранённые сцены | ключи живут только в рантайме (кодом в инсталлерах), в сценах их нет; проверка — сценовый тест и `Chapter1_Arena` в batchmode-загрузке |
| Порядок тика: агент должен шагать после ходьбы сущности, а мотор — после агента | порядок не меняется: шаг мира — поведение сущности уровня, как сейчас; сценовый тест Regax это ловит |
| `AICharacter` — MonoBehaviour, `AIAgentBehaviour` держит на него ссылку при пуле трупов | `Dispose` выходит из мира и отписывается (как `EliteRegaxBehaviour` сейчас); тест на повторный `Init` после `Dispose` |
| `zombie-ai` параллельно может поставить мозг AIKit на `Zombie.prefab` | не трогаем `Features/AI/Horde` и префаб зомби; после шага 4 сообщаем тимлиду, что новый способ — `AIAgentInstaller`; их адаптер (если появится) переводим последним, по договорённости |
| Известные падения тестов не от нас (AbilityAsset, CanvasUpgrade из `HANDOVER.md`) | снимаем базовую линию на чистом `game/develop` до шага 1 и сравниваем с ней |
| Сценовые PlayMode-тесты виснут в batchmode | их — только в живом свёрнутом редакторе, с внешним таймаутом, один редактор на машину |
| Боксинг `HitInfo` в `IEvent<HitInfo>` | `HitInfo` — struct; `Event<T>` Atomic generic, без боксинга; проверим тестом «без мусора» |

---

## 7. Чего не делаем и почему

- **Не переписываем ядро AIKit на значения сущности.** В курсе этого тоже нет: планировщики, BT, FSM, Utility — чистые библиотеки. Переписывание сломало бы 1672 теста, редактор графа и перенос модуля в другие проекты, а игре ничего не дало бы.
- **Не делаем blackboard графа значениями сущности.** Значения Atomic — `object`, запись `float` боксится; переменные графа объявляются ассетом во время работы. Мост из шага 5 даёт атомарный вход без этой цены.
- **Не трогаем орду.** Толпа — отдельная Burst-система, её ведёт `zombie-ai`.
- **Не берём строковую связку «план → узел»** из курса: у нас граф ссылается на ноды по стабильным id `[AIKitNode]`.

---

## 8. Источники в репозитории

Копии документов Platformer-AIKit лежат в [Platformer/](Platformer/README.md) (откуда и какой коммит — там же). Гайд по Atomic — `Docs/AtomicGuide/` (инсталлеры — `11`, поведения — `10`, миры и фильтры — `13`, системы и гизмо — `14`, кодогенерация — `15`).

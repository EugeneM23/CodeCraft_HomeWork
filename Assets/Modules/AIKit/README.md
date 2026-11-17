# AI Kit

ИИ неигровых персонажей «из коробки» — в пару к `CharacterKit`. Поведение собирается **графом**, и граф — это вся
программа персонажа (шаг 89, [AIKit_BrainOnly](../../../AI/Docs/AIKit_BrainOnly.md)): сенсоры пишут переменные, **мозг**
выбирает решение, решение ведёт цепочку готовых блоков, цепочка кончилась — снова мозг; выбор умений — utility-оценкой; тело подключается адаптером, поэтому один и тот же
граф водит 2D-платформерного персонажа, `NavMeshAgent` или просто `transform`.

> **Ветка `feature/aikit-lab` — только ядро.** С 11.11.2026 в этой ветке нет адаптера `Adapters/Platformer2D`
> (`CharacterBodyActuator`, `ReactiveNavigator`, `PhysicsProbes2D`, раннеры дэша и смэша), тестов `Tests/Runtime`
> (стенд `AgentRig`) и тестов визарда и навигации платформера: они зависят от `CharacterKit`, которого здесь нет.
> Всё это живёт в ветках игры (`feature/aikit`, `main`); упоминания этих частей ниже относятся к ним.

> **Полотно — это программа (шаг 89).** Персонаж делает только то, что нарисовано: граф без мозга не работает вовсе,
> нода, до которой не доходит ни одна цепочка решения, не запускается никогда (полотно рисует её бледной). Старой
> модели — старта с первой ноды, рефлексов сенсоров `Detected` / `Lost`, вложенных состояний, условий на стрелках,
> флага «начать заново», композитов BT внутри ноды, нод-кода — больше нет. Один компонент `AI Character` на персонаже
> (с шага 90 — разделы Behavior, Body, Combat, Advanced; аниматор — компонент `AI Animator`): граф-ассет
> и кнопка **Open Behavior Graph**. Ход работы — [Docs/AIKit_Flow.md](../../../AI/Docs/AIKit_Flow.md).

> **Шаг 90 (15.11.2026, [AIKit_Refactor](../../../AI/Docs/AIKit_Refactor.md)).** Одно меню нод с поиском (правый клик,
> пробел, пин в пустоту), группы — путь `[AIKitNode]`; подпись ноды руками; выпадающие списки вместо строк; ярлыки
> связей у выхода; провода мозга по рамке. Место атаки — видимое условие `Attack Slot` и блок `Take Attack Slot`;
> `Get` / `Is` / `Set Parameter`; реакции на действия игрока — решения мозга вне очереди; умения — ассеты из частей,
> выбираются в графе (`Use Ability`, `Use Any Ability`); камень и турель — временное оружие из мира; поиск — `Search
> For` → `What` → `Find` → `Move To` → `Interact` по объектам мира (`AI Object`, `Pickable Item`, `Turret`, `Cover`,
> `Perch`). У `AI Character` разделы Behavior, Body, Combat, Advanced; **анимация — отдельный компонент `AI Animator`**
> (модель не знает отрисовку: действия — `AgentContext.Actions`, аниматор их читает).

**Состояние: модуль закрыт (этапы 1–10 плана).** Спек — [Docs/AIKit.md](../../../AI/Docs/AIKit.md),
план — [Docs/AIKit_Plan.md](../../../AI/Docs/AIKit_Plan.md), исследование — [Docs/Research_AI.md](../../../AI/Docs/Research_AI.md).
Граф собирается кодом (`BrainGraphBuilder`), пресетом или ассетом в окне *Window → AI Kit → Behavior Graph*;
один граф ведёт сколько угодно агентов. Пример в игре — враг платформера (`Enemy.prefab`, ассет `Configs/AI/EnemyBrain`).

## Пять минут: враг без кода

1. Выделить объект врага → *Tools → AI Kit → Setup Agent* (или ПКМ в иерархии → *AI Kit → Setup Agent*). Визард
   ставит на корень **один** компонент `AI Character` (а если тела нет — `Simple Body`, который двигает `Transform`);
   повторный запуск только добавляет недостающее. Шаги визарда собираются по типу `IAgentSetupStep`: адаптер
   добавляет свои части, игра — свой водитель цикла.
2. Разделы компонента: **Behavior** — граф-ассет (`BehaviorGraphAsset`, окно *Window → AI Kit → Behavior Graph*);
   **Body** — тело списком (`Simple Body`, `NavMesh Body`, а в играх — своё; числа лазания и прыжка — замер клипов,
   строкой «measured from …»); **Combat** — оружие; **Advanced** — фракция, частота решений, сид, ручной шаг, запись
   решений. Аниматор — отдельный компонент **`AI Animator`** на объекте `Animator` (визард ставит его сам).
3. Оружие — ассет `WeaponAsset` с приёмами; умения — ассеты `AbilityAsset` из частей, их называет граф (решения `Use
   Ability` / `Use Any Ability`), списка умений у персонажа нет.
4. Цель находят сенсоры графа (`Detection` и его формы, `Senses` с профилем-ассетом восприятия), или её даёт игра:
   `agent.SetTarget(transform)`. Места мира (дом, работа, зона патруля) — объекты `AI Object` с владельцами, их находит
   граф (`Find`).
5. Проверка: *Tools → AI Kit → Validate All* (персонажи), вкладка *Issues* окна графа (ассеты графов).

## Главный принцип: пять слоёв

```
 СЕНСОРЫ ──► BLACKBOARD ──► ГРАФ ──► ЗАДАЧИ/УМЕНИЯ ──► НАВИГАЦИЯ ──► АКТУАТОР ──► ТЕЛО
```

Граф не трогает физику: он пишет намерения, а **актуатор** (`IAgentActuator`) — «виртуальный геймпад» тела
(`Move`, `SetVertical`, `Face`, `Jump` + `HoldTimeForHeight`, `Crouch`, `DropThrough`, `Stop`). Всё, что знает про конкретного персонажа, живёт только
в адаптере актуатора. Поэтому модуль переносится в другой проект целиком и одинаково работает в 2D и 3D.

## Состав

```
AIKit/
├── Runtime/Core/        сборка AIKit — без зависимостей
│   ├── Agent/           AICharacter (оркестратор), AgentContext, IAgentActuator + AgentBodyInfo,
│   │                    TransformActuator, AIWorld (общие факты + расписание), AgentScheduler (лимит
│   │                    решений на шаг), DecisionTrace (трасса данными), AgentDebugComponent (отладка
│   │                    в сцене), AxisWriteDetector (два писателя в ось тела), MovementArbiter,
│   │                    AICheats (ai.trace / ai.freeze / ai.spawn)
│   ├── Blackboard/      Blackboard (типизированные слоты, теги, события), BlackboardKey, TagKey, BlackboardVariable, BlackboardValue
│   ├── Graph/           AgentNode / AgentTask / AgentCondition / AgentState / Transition,
│   │                    BehaviorGraph (описание) + BehaviorGraphInstance (исполнение) + NodeMemory
│   │                    (память агента по слотам узлов), BrainGraphBuilder (мозг, решения, цепочки);
│   │                    Asset/ — BehaviorGraphAsset, GraphSerializer, GraphFields, GraphValidator;
│   │                    Registry/ — AIKitNodeAttribute, AIKitNodeRegistry
│   ├── Nodes/           блоки: Wait, MoveTo, Patrol, UseAbility, Chase, Kite, Strike (Attack), Perform, Chat, Go Inside…;
│   │                    сенсоры Detection (+ Radius, Cone, Zone, Calls For Help), Time Of Day, Senses; решения
│   │                    (Reasoners/): Start (шаг 92: линия один раз), Fight, Chase, Flank, Circle, Notice, Watch, Patrol, Daily Routine, Sleep…; тактика (шаг 87): Kite с путём назад, Take Cover, Pick Up, Drop,
│   │                    Throw, Use Turret, Load Turret, Charge, High Ground; решения Fetch, Throw, Use Turret, Reload Turret,
│   │                    Charge, Take Cover, High Ground
│   ├── Objects/         объекты мира для нод: IHands (руки AICharacter), IPickable — PickableItem (вещь) и
│   │                    ItemSource (склад), Turret, Perch (возвышенность), IThrowLauncher, Bookings (бронь объекта токеном ростера)
│   ├── Conditions/      Compare, All / Any — вопрос ноды Branch (других условий нет: стрелки без условий)
│   ├── Combat/          AbilityDefinition (+ RangeBand), IAbilityRunner, AgentAbilities (кулдауны + выбор стратегией)
│   ├── Utility/         ResponseCurve, Consideration (ось с вето), UtilityMath (свёртка, компенсация), UtilityScore,
│   │                    ISelectionStrategy (Max, Random, RandomWeighted, TopRandom)
│   ├── Perception/      AgentPerception (треки, внимание, лестница тревоги, память, точки поиска), PerceptionProfile,
│   │                    TargetTrack + TargetSubject, пробы ISightProbe / IGroundProbe / IAllyProbe / ITargetScanner /
│   │                    IAimProbe, IPerceivable, ITargetFilter, ListTargetScanner; Signal, SignalBus, ISignalListener
│   ├── Navigation/      INavigator (+ NavVerdict, NavStatus, NavEdgeKind), MoveKind, DirectNavigator (запасной:
│   │                    прямо к цели, поворот не быстрее 300°/с, не шагает в яму при IGroundProbe), IWalkProbe
│   │                    («сколько пройти по прямой» — путь назад у Kite, рывок у Charge)
│   └── Presets/         BehaviorPresets (Melee Chaser, Ranged Kiter, Patrol Guard, Sentry),
│                        графы-пресеты для демо и тестов
├── Adapters/NavMesh/       сборка AIKit.NavMesh — 3D на Unity NavMesh (**прототип**: тестов нет, см. «Чего нет»)
│   ├── NavMeshBody        — тело `AgentBody` на NavMeshAgent: «геймпад», путь и дистанция, глаза (`PhysicsEyes3D`), прыжки по ссылкам (`NavMeshLinkJumper`)
├── Editor/              сборка AIKit.Editor — визард Setup Agent (шаги IAgentSetupStep), Validate All,
│                        инспекторы BlackboardValue (константа / переменная) и ResponseCurve (график);
│                        Graph/ — окно графа (GraphDocument, IGraphCanvas + GraphViewCanvas, NodeLibrary,
│                        ElementInspector, отладка в Play), окно читов
├── Demo/                AIKitDemo.unity — демо-сцена 2D; Scripts/ (сборка AIKit.Demo): DemoTargetMover,
│                        DemoAbility, DemoAgentTarget, DemoAgentState; Editor/ (AIKit.Demo.Editor) —
│                        сборщик сцены (*Tools → AI Kit → Build Demo Scene*)
├── Demo3D/              AIKitDemo3D.unity — арена 3D на NavMesh (прототип, вне правил проекта);
│                        Scripts/ (сборка AIKit.Demo3D):
│                        ArenaNavMesh (рантайм-выпечка), ArenaSpawner, ArenaFighter, ArenaSight,
│                        ArenaAbility, ArenaHud; Editor/ — сборщик (*Tools → AI Kit → Build 3D Demo Scene*)
└── Tests/Editor/        сборка AIKit.Tests (ярус 1) — стенд GraphRig (водит настоящий AICharacter.Step), семантика
│                        графа, blackboard, utility, умения, пресеты, компоненты, расписание, «правды»
│                        (аллокации, детерминизм, чистые условия), дымовой перф-тест
```

## Демо-сцена

`Demo/AIKitDemo.unity` — четыре агента на четырёх пресетах и цель, которая ходит туда-сюда. Ни физики, ни
сервисов, ни игровых ассетов: модуль тикает сам, цель выдаётся напрямую (восприятие — следующий этап).

Сцена собрана до перехода на `AI Character`: после обновления модуля пересоберите её (*Tools → AI Kit → Build Demo Scene*).

| Агент | Пресет | Что показывает |
|---|---|---|
| Melee (синий) | `Melee Chaser` | патруль → сближение → удар → восстановление → снова сближение |
| Ranged (зелёный) | `Ranged Kiter` | держит полосу дистанций, стреляет из неё, отходит при сближении |
| Guard (охра) | `Patrol Guard` | ходит по маршруту, останавливается и смотрит на цель, бьёт только вплотную |
| Sentry (фиолетовый) | `Sentry` | не двигается, стреляет по цели в своей полосе |

Цвет спрайта = текущее состояние, вспышка и «раздувание» = идёт умение, подпись над агентом = имя состояния и
счётчик применений умения (`DemoAgentState`). Это временная замена окну графа из этапа 2.

Сцена собирается кодом: *Tools → AI Kit → Build Demo Scene* (`AIKitDemoSceneBuilder`). Правки демо делаются в
билдере и пересборкой — так сцену нельзя «случайно затюнить» и потерять. Сборка идёт в аддитивно открытой
сцене, поэтому та сцена, что открыта у вас, не трогается.

## Демо-арена 3D (NavMesh)

`Demo3D/AIKitDemo3D.unity` — арена с укрытиями, где живут 15 бойцов со **случайными типами бойцов**; собирается
*Tools → AI Kit → Build 3D Demo Scene*.

- **Навигация — Unity NavMesh.** Навмеш **печётся в рантайме** (`ArenaNavMesh`: `NavMeshBuilder.CollectSources`
  + `BuildNavMeshData`), поэтому в сцене нет запечённого ассета и пакет `com.unity.ai.navigation` не нужен.
  Укрытия не входят в геометрию навмеша, а **вырезают** его через `NavMeshObstacle` с carving — иначе на их
  крышах появляются «островки», и бойцы встают на ящики.
- **Типы бойцов** (`ArenaSpawner`; тело — `NavMesh Body`): Brawler (быстрый ближний), Brute (медленный и толстый), Archer (кайтер),
  Guard (патрульный, не преследует), Sentry (неподвижный стрелок). Каждому достаются случайные числа:
  скорость, здоровье, дальность, кулдаун, маршрут патруля, сид и интервал решений.
- **Команды или все против всех**: `Mode = Teams` (цвет = сторона) либо `FreeForAll` (каждый сам за себя,
  свой цвет). Переключается на ходу клавишей **T**.
- **Бой**: `ArenaFighter` — здоровье, урон, смерть и респаун; `ArenaAbility` — замах (телеграф вспышкой) →
  удар → восстановление, у стрелков ещё трассер; `ArenaSight` — «восприятие-лайт»: ближайший живой враг чужой
  стороны в радиусе и с линией взгляда (укрытия её рвут), раз в ~0.3 с.
- **Хоткеи**: `T` — команды/каждый сам за себя, `R` — перезапуск, `Space` — добавить бойца, `L` — подписи.
- Подписи над бойцами показывают состояние графа и здоровье, панель слева — живых и убийства по сторонам.

## Ассеты умений и оружия

Тип врага задаётся не ассетом-архетипом, а **компонентом `AI Character` и вариантами префаба** (учебник §32.1 —
осознанное отличие, см. [AIKit_Flow](../../../AI/Docs/AIKit_Flow.md), шаг 80). Что переиспользуется между
персонажами — ассеты, и в них нет состояния (учебник C.5):

- **`WeaponAsset`** (*Create → AI Kit → Weapon*) — оружие: список приёмов `WeaponMove` (`Name`, `Action`, `Reach`,
  `Damage`, `Wind Up`, `Recovery`, `Needs Sight`). Удар — `virtual Strike(in StrikeContext)`; по умолчанию попадает
  в `IHitReceiver` цели. Нода `Attack` выбирает приём из списка (`Move`).
- **`AbilityAsset`** (*Create → AI Kit → Ability*) — умение: имя (= действие аниматора), дальность, кулдаун, вес,
  `At Once`, `Needs My Place`… Эффект — `virtual OnStrike(in StrikeContext)`. Паузу, фазу и цель держит
  `AbilityRunner`, который `AICharacter` заводит на каждого персонажа, поэтому один ассет у двух персонажей ничего
  не делит.

*Tools → AI Kit → Validate All* проверяет каждого `AICharacter` на сцене и в префабах (те же проблемы показывает
его инспектор): нет тела; не хватает компонента, который нужен телу (например, `NavMeshAgent`); нет графа
поведения (персонаж ничего не делает); нода `Attack` без оружия или с приёмом, которого у оружия нет (нода с пустым
приёмом при оружии без приёмов — «у оружия нет приёмов»); нода `Use Ability` или решение `Use Ability` называет
умение, которого нет у персонажа. Из графа проверки персонажа смотрят только ноды `Attack` и умений; проверки самого
ассета графа (оборванные связи и прочее) — во вкладке *Issues* окна графа. Сообщение называет объект, клик выделяет
его.

Настройки узлов — `BlackboardValue<T>`: в инспекторе переключатель `$` — число или переменная доски.

## Свой граф кодом

Код строит ту же картину, что полотно, — и ничего сверх неё (`BrainGraphBuilder`):

```csharp
var g = new BrainGraphBuilder();                                       // корень и мозг
g.Sensor(new DetectionTask(new RadiusSensorTask { Radius = 8f }) { Tag = "Player", StoreIn = "Target" });
g.BrainTask.Target = "Target";                                         // провод: что видит Detection — в мозг
g.Variable("Target", VariableType.Transform);

AgentState fight = g.Decision(new FightReasoner { Closer = 1.6f }, 1);
g.Chain(fight, new UseAbilityTask(), new WaitTask(0.4f));             // Do → Use Ability → (Done) → Wait
AgentState chase = g.Decision(new ChaseReasoner { NeedsAttackPlace = false }, 2);
g.Chain(chase, new ChaseTask { StopDistance = 1.3f });
AgentState patrol = g.Decision(new PatrolReasoner(), 3);
g.Chain(patrol, new PatrolTask(points));

agent.SetGraph(g.Build());
```

## API коротко

| Что | Где |
|---|---|
| Агент: шаг, цель, граф, сид, трасса | `AICharacter.Step(dt)` (или сам в `Update`), `SetTarget`, `SetGraph`, `SetSeed`, `IsTracing`, `IsThinking`, `Graph` (экземпляр), `Context` |
| Контекст задачи | `AgentContext`: `Blackboard` (+ `BlackboardOf(scope)`), `Actuator`, `Navigation`, `Abilities`, `Perception`, `Track`/`Target`, `Range`/`Chance` (ПСЧ агента), `SendSignal`/`HasSignal`, жетоны `RequestToken`/`HoldsToken`/`CouldWinToken`/`ReleaseToken`, `Arbiter`, `Memory`, `Record` |
| Граф кодом | `new BrainGraphBuilder()` → `Sensor`, `BrainTask.Target`, `Variable`, `Decision(reasoner, priority)`, `Always`, `Block`, `Do`, `Then` / `OnFail` / `OnEnd` / `Out` / `OnTick`, `Chain`, `Build()` |
| Граф ассетом | `BehaviorGraphAsset.Compile()`, `GraphSerializer.Write/Read`, `GraphValidator.Validate`, `AICharacter.GraphAsset` |
| Экземпляр графа | `BehaviorGraphInstance`: `Current`, `Brain`, `Decision`, `ActivePath`, `EnterState(id/имя)` (мозг, решение или блок цепочки), `StateChanged`; время в ноде — `state.TimeIn(context)` |
| Мир | `IAIWorld` (`AIWorld`): `Roster` (ростер, стычки, жетоны), `BeginStep(dt)`, общие факты; `ISignalBus` (`SignalBus`) |
| Тело и навигация | `IAgentActuator` (+ `AgentBodyInfo`), `INavigator` (`SetDestination` → `NavVerdict`, `Step`, `Status`), адаптеры `CharacterBodyActuator`/`ReactiveNavigator` (2D), `NavMeshActuator`/`NavMeshNavigator` (3D, прототип), `TransformActuator`/`DirectNavigator` |
| Читы | `AICheats.Execute("ai.trace on")`, `Register(name, prefab)` |

## Как расширять

- **Своя задача** — наследник `AgentTask` с `[Serializable]` и `[AIKitNode("Путь/В/Библиотеке", "Task", "мой.id")]`
  (id — навсегда: по нему ассет находит класс), публичные поля — данные (`BlackboardValue<T>` для чисел, которые
  может дать переменная), конструктор без параметров. **Всё, что меняется на ходу, — в `Memory<T>(context)`**
  (вложенный класс полей), не в полях задачи: граф общий. `OnEnter` / `OnTick` → `TaskStatus` / `OnExit(reason)`;
  ось тела — `Claim(context, BodyAxis.Move, MoveAuthority.Path)` перед записью. Переименовали поле —
  `[FormerlySerializedAs]`.
- **Своё решение** — наследник `ReasonerTask` (`Holds(query, out whyNot)` — когда держится, простыми словами;
  `NeedsTarget`), свой блок — наследник `AgentTask`. Условие — только вопрос `Branch` (`Compare`, `AND` / `OR`).
- **Своё умение** — `IAbilityRunner` (или `TimedAbilityRunner`): `CanStart`, `Begin`, `IsRunning`, `Phase`, `Cancel`;
  определение — `AbilityDefinition` с тем же `Id`.
- **Своё тело** — `IAgentActuator` (+ `ICapabilityGate`, если умеет не всё), своя навигация — `INavigator`; пробы
  восприятия — `ISightProbe`, `IGroundProbe`, `ITargetScanner`, `IAimProbe`; фильтр целей — `ITargetFilter`.
- **Свой шаг визарда** — `IAgentSetupStep` (адаптер или игра докладывают свои части).
- **Своя игра на цикле** — выключить `AICharacter.Simulation` (ручной режим) и звать `Step(dt)` из своего цикла; мир —
  `AIWorld.BeginStep(dt)` раз за шаг до агентов.

## Как работает граф

Полотно — это программа (шаг 89): персонаж делает только то, что нарисовано.

- **Состав.** Невидимый корень держит сенсоры (фоновые задачи: пишут переменные) и плоский список нод: **один мозг**
  (`Brain`, у отряда — `Squad Brain`), **решения**, подключённые к нему проводами, и **блоки** — по одной задаче в ноде.
- **Старт — мозг.** Граф без мозга не работает вовсе: ни сенсоры, ни ноды (проблема `NoBrain`, ошибка в консоли).
- **Стрелки — только четыре вида:** провод мозга к решению; `Do` решения — к первому блоку цепочки; выход блока по
  тому, как кончилась его работа (`Done` / `Failed` / «закончил», пронумерованные выходы `Random`); `On Tick` —
  цепочка мгновенных блоков раз в размышление, пока блок идёт. Условий на стрелках нет: развилка внутри цепочки —
  `Branch` с `Compare`. Стрелка в ноду запускает её заново.
- **Размышление** (раз в `ThinkInterval`): мозг раз считает факты о своей цели (`Target`), идёт `On Tick` текущего
  блока, затем стрелки текущей ноды (мозг берёт решение, `Do` ведёт к блоку, выход блока — к следующему), затем мозг
  смотрит снова: важнее держится другое решение — переход (не посреди удара или умения; только что взятое держится
  0,3 с); цепочка кончилась — мозг выбирает заново (то же решение тоже; не сделавшее ничего — отдыхает 0,5 с); не
  держится ничего — персонаж стоит в мозге. Повторяющийся блок (`Strike`) без стрелки из выхода бьёт снова. Мгновенные
  блоки (`Set`, `Add`, `Branch`, `Random`, `Debug Log`) проходятся в то же размышление. Правила мозга написаны на его
  ноде.
- **Мозг без провода `Target`** не слепнет: решения о цели (`Fight`, `Chase`…) не держатся, остальные (`Patrol`,
  `Start`, `Daily Routine`, `Sleep`, `Follow Squad`, решения отряда) — держатся.
- **Нода вне цепочки не работает никогда:** до неё не доходит ни одно `Do` — проблема `NotInChain`, на полотне она
  бледная. Две задачи в одной ноде (граф из кода) — `TwoBlocks`.
- **Граф — описание, агент — память.** Один граф ведёт сколько угодно агентов: всё, что меняется на ходу, живёт в
  `NodeMemory` контекста агента по слоту узла. Своя задача хранит своё в `Memory<T>(context)`. Имена переменных — общий
  на процесс id (`Blackboard.NameToId`).
- **Сенсор только видит.** Что делать с увиденным, решает мозг приоритетами (`Notice`, `Fight`…), а не стрелка сенсора
  (учебник §38, грабля №1). Зов союзника слышит только сенсор-форма `Calls For Help` в `Detection` — видно, кто слышит.

## Граф ассетом и редактор

- **Ассет** — `BehaviorGraphAsset` (*Create → AI Kit → Behavior Graph*; новый граф начинается с мозга): плоские записи
  нод и стрелок (тип — стабильный id из `[AIKitNode]`, данные — JSON полей, связи — id), корень, **объявления
  переменных**. `asset.Compile()` один раз собирает `BehaviorGraph`: один граф на всех агентов, у каждого свой
  `BehaviorGraphInstance`. Граф из кода сохраняется `GraphSerializer.Write(graph, asset)`.
- **Валидация** (`GraphValidator.Validate`, кнопка проблем окна): нет корня, нет мозга, неизвестный тип или поле,
  висячая связь, нода вне цепочки, две задачи в ноде, необъявленная переменная, решение без цепочки, равные
  приоритеты, решение отряда у персонажа и наоборот. **Старый ассет** (рефлексы, вложенные ноды, условия на стрелках
  или на входе, стрелки по сигналу или изменению переменной) читается только для просмотра — проблема `OldModel`.
- **Переименования:** класс — держит id `[AIKitNode]`; поле данных или связи — `[FormerlySerializedAs]`.
- **Окно** — *Window → AI Kit → Behavior Graph*: холст на `GraphView` за `IGraphCanvas`, правки через `GraphDocument`;
  плоский вид, мозг — начало (маркера Start нет), у сенсоров один выход — `Object`; список нод с поиском (Space / ПКМ),
  панель Blackboard; в Play — подсветка, причины решений, живая доска, точки останова.
- **Читы** (§33.3): `AICheats.Execute("ai.trace on")`, `"ai.freeze on|off"`, `"ai.spawn имя x y z"`; панель —
  *Window → AI Kit → Cheats*.

## Решения и исполнение разделены

Шаг агента `AICharacter.Step(dt)`: кулдауны умений → решение (`Think`), если подошёл его срок по `ThinkInterval` и
мир разрешил → исполнение активных состояний (`Tick`) → нажатия уходят в тело (`Apply`). Решения прореживаются,
исполнение идёт каждый шаг — движение не дёргается.

**Один водитель шага.** Агента шагает ровно один владелец: сам себя в `FixedUpdate` (`Simulation = FixedUpdate`,
модуль работает в любом проекте) или владелец в режиме `Manual` — компонент-водитель игрового цикла (в игре —
`AgentLoopComponent`, как `CharacterLoopComponent` у CharacterKit: тогда ИИ честно стоит на паузе) или тестовый
стенд. Двойной шаг невозможен по устройству: расписание агентов не шагает.

**Мир и расписание.** `AIWorld` (интерфейс `IAIWorld`) — то, что агенты делят: глобальный blackboard и
`AgentScheduler`. Агент входит в мир `SetWorld(world)`; без мира он живёт один (глобальная область — его
собственный blackboard) и думает, когда пришёл срок. Расписание только отвечает «можно ли решать сейчас»:
`MaxDecisionsPerStep` — не больше N решений на шаг (остальные решают на следующем), счётом, а не миллисекундами,
поэтому «кто думал на каком шаге» одинаково на любой машине; при регистрации первые решения агентов
раскладываются по интервалу (фазовый сдвиг). `Stopwatch` стоит только вокруг решения и только меряет
(`ThinkMillisecondsThisStep`). Шаг мира открывает его владелец: `world.BeginStep()` раз за фиксированный шаг до
агентов (в игре — `AIWorldLoop` на цикле, порядок −200; он же стирает общие факты при смене сцены). Мир в игре
создаёт и регистрирует `GameServices` (`ServiceLocator.TryGet<IAIWorld>` в `AgentLoopComponent`).

Время — только из `dt`, случайность — свой `AgentRandom` (SplitMix64, всё состояние — `Context.RandomState`, его
можно сохранить): один сид — одна трасса решений, другой сид — другая (`TruthTests`). Сид 0 — от мира
(`IAIWorld.Seed`, `NextAgentSeed`); без мира выбирается и хранится в `Context.Seed`.

**Производительность.** Маркеры профайлера `AIKit.Think` / `AIKit.Tick` / `AIKit.Apply` на шаг агента и образец на
каждый класс задачи (под `ENABLE_PROFILER`) — дорогая задача видна по имени. Дымовой тест
`PerfSmokeTests` (50 агентов × 2000 шагов) печатает мкс на агента за шаг, решения отдельно от исполнения; порог —
относительный, от базовой линии ниже.

## Навигация

`INavigator` — «отведи туда»: `SetDestination` отвечает вердиктом (`NavVerdict`), `Step(dt)` пишет нажатия в
«геймпад», `Status` — где следование сейчас: `Following`, `Waiting` (впереди союзник — стоит за ним), `Blocked`
(впереди непроходимое: стена выше прыжка, яма шире прыжка — стоит, ещё не сдался), `Unreachable` (сдался: нет
прогресса `GiveUpTime`). `Waiting` и `Blocked` — «ещё в пути» (`IsMoving`), задача сама решает, сколько ждать.

`ReactiveNavigator` (2D, `CharacterBody`) идёт в рамке тела (любая гравитация) в порядке: у цели — стоит или
прыгает / спрыгивает / сходит с уступа к ней; союзник впереди — ждёт; цель ниже и под ногами one-way — проваливается;
стена — прыгает, если прыжок достаёт, иначе `Blocked`; край — к нижней цели шагает вниз, через яму прыгает, если
за ней есть площадка в досягаемости прыжка, иначе `Blocked`; цель выше и рядом — прыгает; иначе бежит. В воздухе
рулит и добирает воздушным прыжком при недолёте. Лестница: стоит у лестницы и цель выше прыжка — лезет (сверху
вниз — спускается), наверху выход ставит тело само. Мир спрашивается только через пробы сущности
(`IGroundProbe`, `IAllyProbe` — у 2D это `PhysicsProbes2D`); **без пробы земли края и ямы не видны, а любая стена
считается выше прыжка** — вслепую навигатор не прыгает. Числа — `ReactiveNavigatorSettings` (в игре их копирует
из своего конфига владелец — `EnemyBrainGraph` из `EnemyAIConfig`); «на сколько прыжка рассчитывать» —
`CharacterBodyActuator.JumpHeightMargin` / `JumpReachMargin`, высота и дальность прыжка — `AgentBodyInfo` от
настроек тела (правка прыжка в песочнице меняет проходимость сразу, `NavigationParityTests`). Те же проверки —
запросами для задач: `IsEdgeAhead`, `IsWallAhead`, `IsBlocked`, `ObstacleHeight`, `GroundAheadLength`,
`TryFindLanding`, `IsAllyAhead`.

Задачи движения: `MoveToTargetTask` решает **один раз при старте** (§15), подход это (цель видна — идёт за живой
целью) или боковое (не видна — к месту, где видел, точка не едет); `KeepDistanceTask` и подходит, и отходит через
навигатор (видит яму и стену сзади); `MoveToTask` — к точке. Граф площадок и поиск пути — «по требованию»: тот же
`INavigator`, поведение не меняется.

## Восприятие и сигналы

У агента свои органы чувств (`AgentPerception`), когда на сущности есть `ITargetScanner` (у 2D — `PhysicsProbes2D`, у
3D — `PhysicsProbes3D`, без физики — `ListTargetScanner`); без сканера цель задаёт игра (`SetTarget` — для тестов и
сценария). Каждый взгляд — со случайным интервалом профиля (5–10 Гц, фаза своя у каждого агента): кто вокруг
(сканер), кто из них цель (`IPerceivable` цели — жива ли, чья фракция, куда смотреть; `ITargetFilter` агента —
правила игры), попадает ли в конусы **в рамке тела** (фокус, периферия, за спиной; известная цель держится до
`LoseRange`, куда бы агент ни смотрел) и видна ли (`ISightProbe` — единственный путь к физике). Каждый шаг внимание
растёт по скорости зоны (фокус — полностью, периферия — вдвое медленнее, спина — вчетверо), вне видимости гаснет.
Лучший трек — единственный интерес: агент знает его субъект, пока видит, и только последнее место, когда нет
(восприятие — единственный писатель `TargetTrack`). Лестница тревоги `Idle → Suspicious (0.3) → Aware (0.8) →
Searching` с гистерезисом (Aware держится до 0.3) и «прощением» (память `MemoryTime`); уровень мира — `IAIWorld.Alert`.
`SearchPoints` даёт 2–5 точек вокруг последнего места (рандом агента), `SearchAroundTask` обходит их.
Нечестные входы: `Alert(source)` — удар будит всегда (внимание 1, решение на следующем шаге), `Hear(signal)` — крик
союзника поднимает до Suspicious, не выше. Узлы: `KnowsTargetCondition` (пресеты: «знает» — Aware), `AwarenessCondition`,
`AlertLevelCondition`, `HeardSignalCondition`; входы осей `Awareness`, `TimeSinceSeen`. Шина `SignalBus`
(`ISignalBus`): `Broadcast` с областью (радиус, фракция), рассылка по копии слушателей; агент слушает её через
`AICharacter.SetSignals`, кричит `SendSignalTask`; `HasSignalSince(id, s)` — память о сигнале. Без шины агент работает.
Мозг выключен — органы чувств работают (`AICharacter.Sense`). Отладка: `AgentDebugComponent.ShowPerception` — конусы,
лучи к трекам с вниманием, последнее место.

## Бой: ростер, жетоны, фазы, арбитр

**Ростер** (`AgentRoster`, у мира — `IAIWorld.Roster`): агенты мира в постоянном порядке; агент входит в него с
`SetWorld`. «Союзник впереди» — запрос к ростеру по позициям тел, а не к физике (инвариант 1: агент не читает
другого агента). У каждой цели — **`Engagement`** с **жетонами атаки** (`AttackTokens`): пулы по виду атаки, ёмкость
говорит цель (`ITokenCapacity`, иначе треть ростера — `RoleAssignment`). Заявка подаётся и подтверждается каждый
шаг, пока агент хочет бить (`context.RequestToken(pool)` → `Granted` / `Pending` / `Refused`); заявки одного окна
(`MaturationWindow`, по умолчанию шаг) ранжируются вместе — приоритет, подошедший в упор (`CloseRange`) вперёд,
ближний, порядок ростера — а не «кто первым позвал». Дальше `WinRadius` — отказ сразу; лучшая заявка забирает жетон у
худшего держателя, **но не у начавшего замах**; опустевший пул отдыхает `RestAfterEmpty`; жетоны мёртвого держателя
и мёртвой цели возвращаются сами (цель «мертва», когда её отвергает фильтр агентов). `UseAbilityTask` с
`AbilityDefinition.TokenPool` получил третий исход — «заявка подана, жду» (Running). Без мира ничто не ограничивает.

**Фазы умения** (`AbilityPhase`: `WindUp`, `Active`, `Recovery`): раннер докладывает свою фазу; **выводится из
контента** — у врага игры из окна удара его хода (`HitStart..HitEnd` в `AttackMove`), у `TimedAbilityRunner` — из
чисел `WindUp`/`Recovery` определения (только у умений без клипа: одно число — один владелец); раннер, который не
знает, называет весь бег `Active`. `AgentAbilities.RunningPhase` / `PhaseOf(id)`.

**Арбитр движения** (`context.Arbiter`, `MoveAuthority`: Steering < Path < Strafe < Traversal < Frozen): писатель
заявляет ось (`Move`, `Face`) перед записью и пишет, только пока заявка жива; сильнее — забирает, равный — нет,
проигравший просто молчит. Задачи движения модуля заявляют сами (`AgentTask.Claim`), отпускается на выходе
автоматически; `UseAbilityTask` замораживает обе оси на время умения.

**Дистанция боя** (`CombatSpacingTask`): без жетона агент держит кольцо роли (`RingMin`/`RingMax` на доске — их
пишет в них граф (кольцо роли)), после своего умения отходит `RecoveryTime`, в колонне за союзником не
лезет вперёд. Пресет `Melee Chaser` с `Tuning.TokenPool` переходит в неё, когда жетон не выиграть. Приоритет роли
роли (`Role.Priority`) добавляется к весу заявки. Кривые: ещё `Hyperbolic`, `Const`, `Custom` (AnimationCurve);
декораторы осей `InvertOutput`, `Multiply`. Отладка: `AgentDebugComponent.ShowCombat` — фаза, кто держит оси, жетоны.

## Умения

`AbilityDefinition` — данные: `Id`, вес, кулдаун (считается **от конца** умения), `StartDelay`, полоса дистанций
`Range`, `RequiresLineOfSight`, `MinAwareness`, `RepeatPenalty` (анти-повтор), оси оценки `Consideration` с кривыми
отклика и свёрткой `Reduction`; `WindUp`, `Recovery`, `TokenCost`, `CancellableByHurt` — форма данных под этап 8.
Ось без данных (дистанция без цели, нет переменной) — **вето**, а не ноль: обязательная ось снимает умение с выбора
(в трассе — `AbilityVetoed` с именем оси), необязательная просто молчит. `AgentAbilities.Select` отбирает
подходящие, считает оценку (произведение осей, компенсация — среднее геометрическое) и отдаёт выбор стратегии
`AgentAbilities.Strategy` (по умолчанию `TopRandomSelection(2)`) — всегда брать максимум значит сделать бой
предсказуемым. Играет умение `IAbilityRunner` — адаптер к тому, что уже
есть в игре.

## Отладка

- **Трасса решений — данными.** `AICharacter.IsTracing` (или `Context.TraceEnabled`): каждое решение пишется в
  кольцевой буфер `Context.Trace` как `DecisionRecord` — старт, переход (откуда, куда, каким ребром), повторный
  выбор после завершения листа, **все** кандидаты умения со счётом и выбранное умение. При записи строки не
  строятся (`ToString` форматирует запись для человека), с выключенной трассой шаг не аллоцирует вовсе
  (`TruthTests.Step_TraceOff_AllocatesNothing`); две трассы сравниваются поле в поле.
- **`AgentDebugComponent`** (рядом с агентом, только в эдиторе) — над агентом в окне Scene: путь активных
  состояний, задачи листа и их статус, blackboard агента и последние решения. Пока он включён, трасса включена.
- **Два писателя в одну ось тела** (`AxisWriteDetector`, только в эдиторе): если две задачи за один шаг пишут
  `Move` (или `Face`), в консоль один раз на пару уходит предупреждение с их именами — побеждает последняя. Это
  способ узнать, каким графам нужен арбитр движения (этап 8).
- **Навигация в отладке.** Флажок `AgentDebugComponent.ShowNavigation` (выключен по умолчанию): линия к точке
  рулёжки цветом статуса, в подписи — статус, ребро и остаток пути, и последние отказы (`Blocked` / `Unreachable`)
  с временем, ребром и местом — по одному на отказ, а не на шаг.

### Вопрос → куда смотреть

| Вопрос | Куда смотреть |
|---|---|
| Почему он в этом состоянии? | окно графа, вкладка *Debug*: путь подсвечен, последнее ребро выделено; лог решений (`DecisionRecord`: откуда, куда, каким ребром) с перемоткой |
| Почему не перешёл? | инспектор ребра (условия, `MinStateTime`, триггер); порядок проверки — рефлексы, потом от листа к корню до первого сработавшего |
| Почему выбрал это умение? | трасса: `AbilityCandidate` со счётом по каждому умению, `AbilityVetoed` — кто отбросил, `AbilityUsed` |
| Почему стоит и не бьёт? | `ShowCombat`: жетоны цели (кто держит, кто в очереди), фаза умения; погоня ждёт жетон (`Pending`) |
| Видит ли он цель? | `ShowPerception`: внимание, лестница тревоги, память, последняя позиция |
| Почему не идёт туда? | `ShowNavigation`: статус, ребро, отказы `Blocked`/`Unreachable` с местом |
| Кто дёргает тело? | предупреждение `AxisWriteDetector` (две задачи пишут одну ось), `MovementArbiter` — кто держит ось |
| Что у него на доске? | вкладка *Debug* окна (живые значения с правкой), `AgentDebugComponent` |
| Граф не грузится / ведёт себя не так после правки | вкладка *Issues* окна, консоль при `Compile` (каждая проблема называет узел) |
| Все сразу | читы: `ai.trace on`, `ai.freeze on` (окно *Window → AI Kit → Cheats*) |

## Базовая линия производительности

Замер этапа 2 (эдитор, Mono, Debug-код): 0,3 мкс на агента за шаг (09.11.2026: 0,28 мкс, решения ≈ 20 % времени) в `PerfSmokeTests`
(50 агентов-бойцов `Melee Chaser`, 2000 шагов, трасса выключена). Тест падает, если шаг стал в 5 раз дороже.
Шаг 89 (14.11.2026): пресет собран из блоков полотна — его `Chase` берёт место на кольце, и у каждой цели появилась
стычка, которую мир шагает. Замер на одной машине, прогоны вперемежку: прежний пресет 0,73 мкс, новый 1,34 мкс (×1,84),
база стала 0,55 мкс. Вопросы мозга о цели («есть ли у меня место», «кто бьёт первым») стычку не создают
(`AgentRoster.FindEngagement`): с ними граф без работы в блоках стоит 0,4 мкс, а не 1,0.

## Покрытие тестами

Замер (этап 10, 10.11.2026): `AIKit` + `AIKit.Platformer2D` — 99,7 % строк (5271 из 5285); `AIKit.NavMesh` — прототип, не меряется. Не покрыты: пять мёртвых по устройству строк (защиты, которые не срабатывают), защита от округления в рулетке, редкий случай `ReactiveNavigator` (стена вне слоёв земли).
Исключены `[ExcludeFromCodeCoverage]`: отрисовка гизмо и `OnGUI` (`AgentDebugComponent`; текст оверлея проверяется через `Describe()`), недостижимые `catch` реестра узлов. Как повторить —
рантайм-сборки модуля, EditMode и PlayMode в одну папку результатов, отчёт со второго прогона (Unity стирает
`Temp` при выходе, поэтому папка — не в нём):

```bash
Unity -batchmode -projectPath <проект> -runTests -testPlatform EditMode -assemblyNames "AIKit.Tests" \
  -debugCodeOptimization -enableCodeCoverage -coverageResultsPath <проект>/Logs/AIKitCoverage \
  -coverageOptions "generateAdditionalMetrics;assemblyFilters:+AIKit,+AIKit.Platformer2D"
Unity -batchmode -projectPath <проект> -runTests -testPlatform PlayMode -assemblyNames "AIKit.Tests.Runtime" \
  -debugCodeOptimization -enableCodeCoverage -coverageResultsPath <проект>/Logs/AIKitCoverage \
  -coverageOptions "generateAdditionalMetrics;assemblyFilters:+AIKit,+AIKit.Platformer2D;generateHtmlReport"
```

Сводка — `Logs/AIKitCoverage/Report/Summary.md`. Под инструментированием `PerfSmokeTests` падает (шаг дороже
в разы) — это ожидаемо, перф меряется обычным прогоном. Сознательно не покрыта отрисовка гизмо
(`[ExcludeFromCodeCoverage]`: в batchmode окна Scene нет).

## Лист регрессии (§34)

| Фича | Автотесты | Руками (в эдиторе) |
|---|---|---|
| Граф: выбор, рёбра, рефлексы, повторный выбор | `GraphSemanticsTests`, `GraphSelectionTests`, `EdgeCaseTests` | `AgentDebugComponent`: путь состояний меняется как ждёшь |
| Blackboard, переменные, `BlackboardValue` | `BlackboardTests`, `BlackboardContractTests`, `VariableGraphTests` | переключатель `$` в инспекторе узла |
| Умения и выбор | `AbilityTests`, `AbilityChoiceTests`, `UtilityCoreTests` | в трассе видны кандидаты со счётом |
| Навигация 2D | `NavigationParityTests`, `NavigationOnBodyTests`, `AdapterEdgeTests`, игра: `NavigationTests`, `PathQueryTests` | `ShowNavigation`: линия и отказы в `Sandbox_Enemies` |
| Рулёжка и задачи движения | `SteeringTests` | агент не прыгает в яму, разворот плавный |
| Ассеты умений и оружия, компонент | `AbilityAssetTests`, `WeaponTests`, `CharacterComponentTests`, `NavMeshBodyTests` | *Validate All* — пусто на чистом проекте |
| Визард | тестов в этой ветке нет (`SetupAgentTests` остался в ветках игры) | *Setup Agent* на новом враге, Play — он ходит |
| Пауза и цикл (в игре) | `AgentLoopTests`, `HonestPauseTests` | Esc в уровне — враги стоят |
| Граф ассетом, узлы ассета, общий граф | `GraphAssetTests`, `DecisionNodeTests`, игра: `EnemyGraphAssetTests` | окно графа: открыть `EnemyBrain`, Play, выделить врага — путь светится |
| Редактор и читы | `GraphEditorTests` | правка в окне → Ctrl+Z возвращает; *Cheats*: `ai.freeze on` — все стоят |
| Восприятие и сигналы | `PerceptionTests`, `PerceptionOnBodyTests`, игра: `EnemyReflexTests`, `SearchTests` | `ShowPerception`: тревога растёт и гаснет |
| Бой: жетоны, фазы, арбитр | `AttackTokensTests`, `CombatCoreTests`, игра: `CombatHonestyTests` | трое на игроке — бьёт один |
| Переносимость | `PortabilityTests` (ни одна сборка модуля не ссылается на игру) | — |
| Детерминизм и аллокации | `TruthTests`, `PerfSmokeTests` | — |

## Чего нет и почему

Модуль закрыт по плану [AIKit_Plan](../../../../AI/Docs/AIKit_Plan.md) (этапы 1–10). Остальное не делаем, пока не
пришёл повод: у каждой строки — условие возврата и что уже готово под него.

| Чего нет | Вернём, когда | Что уже готово |
|---|---|---|
| Граф площадок 2D и поиск пути (A*, JPS, исполнение рёбер) | врагу нужно туда, куда больше одного прыжка «в сторону цели» | `NavEdgeKind`, вердикты `INavigator` — возврат аддитивен |
| Инструменты дизайнеру уровней (достижимость, тепловая карта) | вместе с графом площадок | — |
| Полный слой steering (context map, separation) | четверо и больше в ближнем бою выпихивают друг друга | `AvoidPit`, ограничение поворота, кольцо `CombatSpacingTask` |
| Координатор (выбор цели, отряды, формации, карты влияния) | больше одной возможной цели или отряд с приказами | ростер, стычки, жетоны; отряд `AI Squad` с командным графом (строй, кольца, сбор) |
| Умные объекты и реестр объектов уровня | на уровне появились укрытия, турели, рычаги | зонды и ростер |
| Директор, волны, аггро с домом | контентный уровень с волнами | жетоны, роли; жить на цикле игры |
| Укрытия, подавление, уклонение от снаряда | в игре появились снаряды (сейчас урон — overlap-запрос) | — |
| Реактивная защита и стойки | у персонажа появился блок или уклон | партнёр «идеально блокирует» в тестах игры |
| Боссы | дизайн босса | фазы умения, подграфы, `Capability` |
| Толпа (flow field, boids) | больше ~50 одновременных агентов | 50 агентов на одном графе без аллокаций |
| GOAP / HTN | сущности мало графа | узлы — данные со стабильным id, `Subgraph` |
| Сон и LOD (`Asleep`) | дымовой перф-тест показал нехватку `ThinkInterval` | расписание с лимитом решений |
| Пространственный индекс агентов | больше ~30 агентов и широкая фаза в профайлере | в 2D её делает `Physics2D` |
| Слух по длине пути, карта группового поиска | стелс или групповой поиск | минимальная шина сигналов |
| Сохранение состояния ИИ | сохранение посреди боя | `EnterState(id)`, стабильные id, ПСЧ сериализуем |
| Полная шина сигналов (отряд, режимы, барки) | пришёл координатор или барки | радиус, фракция, `HasSignalSince` |
| Пресеты в 2D/3D-парах, летающие, транспорт | модуль уходит наружу / летающий враг / ИИ мотоцикла | актуатор — единственный шов |
| **3D-адаптер `AIKit.NavMesh` и `Demo3D` — прототип, тестов нет** | модуль переносят в 3D-проект или нужны off-mesh-рёбра | частичный путь — уже `Unreachable` |

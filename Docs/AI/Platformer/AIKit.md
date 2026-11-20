# AIKit — модуль ИИ персонажей (спек)

Переносимый модуль: «ИИ из коробки» так же, как `CharacterKit` — «персонаж из коробки». 2D и 3D,
визуальный граф поведения, восприятие, навигация, бой и умения, взаимодействие между ИИ, отряды, толпа.

> **Актуально с 13.11.2026:** персонаж собирается одним компонентом `AICharacter` ([AIKit_Character](AIKit_Character.md)).
> Слова «архетип», `AIAgent`, `Controllers` / `Components`, провайдеры графа и карточки умений ниже — история замысла;
> действующая картина — §3.1, §10 и справка про умение в §7.

> **Актуально с 15.11.2026 (шаг 90, [AIKit_Refactor](AIKit_Refactor.md), итог — шаг 90 в [AIKit_Flow](AIKit_Flow.md)):**
> у `AI Character` разделы Behavior, Body, Combat, Advanced; аниматор — отдельный компонент `AI Animator` (сборка
> `AIKit.Animation`), модель не знает отрисовку (действия — `AgentContext.Actions`); умения — ассеты из частей, их
> называет граф; объекты сцены у персонажа не хранятся (места — `AI Object`, поиск — `Search For` → `What` → `Find` →
> `Move To` → `Interact`, реестр `WorldObjects`); копий чисел тела на доске нет (`Get` / `Is` / `Set Parameter`).

Справочник «как делать» — **[AI_Handbook](AI_Handbook.md)**; сводный вход в тему — **[AI](AI.md)** (принципы, архитектура, трассируемость, состояние).

Исследование, на котором стоит спек — [Research_AI.md](Research_AI.md) и серия к нему:
[разборы игр](Research_AI_Games.md), [жанры и роли](Research_AI_Genres.md),
[инструменты](Research_AI_Tools.md), [практика Unity](Research_AI_Unity.md),
[библиография](Research_AI_Sources.md). План работ — [AIKit_Plan.md](AIKit_Plan.md).
Правила композиции — [CompositionGuide.md](CompositionGuide.md).

---

## 0. Цели и не-цели

**Цели**

1. **Просто:** поставить врага в сцену, добавить компонент `AI Character` (граф, тело, оружие, умения),
   поправить 5–10 чисел в инспекторе — враг работает. Граф при этом открывать не обязательно.
2. **Гибко:** всё поведение собирается в визуальном графе; свой узел — один класс с атрибутом; подграфы и
   «способности» переиспользуются; числа перекрываются на экземпляре.
3. **Полный охват поведения:** не только ходьба — бой, выбор умений, позиционирование, реакции, разговоры
   сигналами, отряды и приказы, использование объектов уровня.
4. **Переносимо:** модуль ничего не знает об игре и о конкретном контроллере персонажа; тело подключается
   адаптером (CharacterKit 2D, `NavMeshAgent` 3D, свой).
5. **Честно по производительности:** бюджет на кадр, разная частота «думания», ноль аллокаций в тике,
   детерминизм по сиду (тесты).

**Не-цели (сознательно вне первых этапов)**

- ECS/DOTS-версия рантайма (отдельная большая тема, разбираем позже; API проектируем «jobs-ready», но не на джобах).
- Машинное обучение, нейросети, обучение с подкреплением.
- Диалоги, квесты, нарратив (сигналы и барки — да, диалоговые деревья — нет).
- Полноценная замена A\* Pathfinding Project для огромных 3D-уровней: наша навигация закрывает 2D-платформер,
  плоский 2D и 3D-NavMesh; внешние решения подключаются адаптером.

---

## 1. Принципы

### 1.1 Пять слоёв и жёсткие границы

```
 СЕНСОРЫ ──► BLACKBOARD ──► ГРАФ ПОВЕДЕНИЯ ──► ЗАДАЧИ/УМЕНИЯ ──► НАВИГАЦИЯ+STEERING ──► АКТУАТОР ──► ТЕЛО
 (Perception)  (факты)       (решение «что»)    (исполнение      (как дойти)            («геймпад»)  (CharacterBody,
                                                 «как»)                                               NavMeshAgent…)
```

- **Граф не трогает физику.** Узел не знает слова `Rigidbody`. Он пишет намерение: «двигайся к точке»,
  «повернись туда», «используй умение `SwordCombo`».
- **Актуатор — единственная точка связи с телом** (`IAgentActuator`): `Move(direction)`, `Face(direction)`,
  `Jump(hold)`, `Crouch(bool)`, `UseAbility(id)`, `Stop()`. Это обобщение `EnemyCommands` из игры, которое уже
  доказало себя: состояния пишут «нажатия», контроллер применяет их теми же вызовами, что `PlayerInputController`.
- **Blackboard — единственный канал данных** между слоями и узлами.

### 1.2 Правила проекта, которые соблюдаем с первой строки

- Модуль в `Assets/Game/Scripts/Modules/AIKit`, свои asmdef, `README.md`, `Tests/`; ссылок на игру нет.
- Код и комментарии — английские; документация — русская.
- **Тикает сам** (как CharacterKit: `FixedUpdate`/`Update`), но имеет ручной режим: в игре его ведёт
  `AgentLoopComponent` от `IGameLoop` — на паузе ИИ стоит.
- Время — только параметром `dt`; случайность — свой `System.Random` с сидом; `Time.time` не читаем.
- Сущность агента по гайду композиции: на корне — один компонент `AICharacter`: тело, оружие и умения
  заданы его полями, отдельных `Controllers` и `Components` нет (с 13.11.2026).
- Odin не тащим; инспекторы — свои (UI Toolkit/IMGUI) в `AIKit.Editor`.

### 1.3 Один формат графа вместо трёх систем

Граф поведения AIKit = **состояния + задачи + переходы** (как Unreal StateTree) **+ BT-композиты внутри**
(`Sequence`, `Selector`, `Parallel`) **+ utility-селекторы** **+ слоты планировщиков** (GOAP/HTN как узел).
Обоснование — §2.7 исследования: FSM рассыпается на N² переходов, чистое BT плохо держит состояние и
непрозрачно прерывается, utility нужен для «выбери лучшее из многого», планировщик — точечно.

### 1.4 Инварианты модуля

Список того, что **запрещено** — его держат код, валидация ассетов и тесты. Взят с поправками из
архитектуры живого проекта ([Research_AI_Games §12.2](Research_AI_Games.md)): там этот список оказался
полезнее любого описания фич.

1. **Агент не обращается к агенту.** Всё «кто из нас» — через координатор и его общие ресурсы (жетоны,
   слоты, брони). Прямых ссылок «агент → соседний агент» в модуле нет.
2. **Случайность — только из `AgentContext.Random`** (свой `System.Random` с сидом агента, сид — от сида
   мира). `UnityEngine.Random` в модуле запрещён: баг должен воспроизводиться по сиду.
3. **Время — только параметром `dt`.** `Time.time`, `Time.deltaTime`, корутины по времени, `Invoke`, твины —
   нет (правило проекта и условие паузы).
4. **Одна ось тела — один владелец.** Передвижение и поворот — два отдельных клейма у `MovementArbiter`
   (§6.1); писать в тело в обход арбитра нельзя.
5. **Условия двухуровневые:** сначала дешёвый гейт (флаг, дистанция²), потом дорогой тест (линия взгляда,
   запрос пути). Дорогой тест первым — ошибка, которую ловит ревью и перф-тест.
6. **Условие только читает.** Никаких побочных эффектов в `AgentCondition.Evaluate`.
7. **Задача не сканирует мир.** `FindObjectsOfType`/`OverlapAll` в тике узла запрещены: объекты уровня —
   через реестр с бронью (§8), цели — через восприятие (§5).
8. **Игра не знает о модуле, модуль не знает об игре.** Ссылка только одна: игра → `AIKit`. Адаптеры
   (`AIKit.Platformer2D`, `AIKit.NavMesh`) знают о `CharacterKit`/`NavMesh`, но не об игре.
9. **Ассеты-данные — листья.** Архетип ссылается на граф, набор ролей и умения; обратных ссылок нет,
   циклов нет. Валидация ассетов проверяет это и ругается внятно.
10. **У каждого значения ровно один владелец.** Граф сущности задаётся в одном месте (граф-ассет персонажа), числа
    механики — в одном ассете; «второй слой, который тоже назначает» — гарантированное расхождение
    (их же пункт долга №3: часть префабов годами ссылалась на устаревший граф).
11. **Ноль аллокаций в тике** и никаких строк в горячем пути (строки — только в трассе отладки).
12. **Поле ассета без потребителя в рантайме — баг.** Валидация ловит «настройка есть, код её не читает»
    (у них такие знобы дожили до прода и вводили дизайнеров в заблуждение).

---

## 2. Карта модуля: сборки и папки

```
Assets/Game/Scripts/Modules/AIKit/
├── README.md
├── Runtime/
│   ├── Core/            AIKit                 — агент, blackboard, граф, узлы, расписание, сигналы, utility
│   ├── Perception/      AIKit.Perception      → AIKit
│   ├── Navigation/      AIKit.Navigation      → AIKit
│   ├── Combat/          AIKit.Combat          → AIKit
│   ├── Coordination/    AIKit.Coordination    → AIKit, AIKit.Perception
│   └── Crowd/           AIKit.Crowd           → AIKit, AIKit.Navigation
├── Adapters/
│   ├── Platformer2D/    AIKit.Platformer2D    → AIKit, AIKit.Navigation, CharacterKit, CharacterKit.Skills
│   └── NavMesh/         AIKit.NavMesh         → AIKit, AIKit.Navigation (+ модуль Unity AI)
├── Editor/              AIKit.Editor          — окно графа, инспекторы, визард, валидаторы
├── Demo/                AIKit.Demo            (не auto-referenced) + сцена AIKitDemo.unity
└── Tests/
    ├── Editor/          AIKit.Tests           — граф, blackboard, utility, планировщик, валидаторы
    └── Runtime/         AIKit.Tests.Runtime   — стенд агента, навигация, бой, отряды, толпа
```

Namespace по сборкам: `AIKit`, `AIKit.Perception`, `AIKit.Navigation`, `AIKit.Combat`, `AIKit.Coordination`,
`AIKit.Crowd`, `AIKit.Platformer`, `AIKit.NavMeshNav`.

В игре (сборка `Game`): `Gameplay/Character/Components/AgentLoopComponent.cs` (ведёт агента от игрового цикла),
адаптеры игровых умений (`AttackSkill`, `DashSkill`, `SmashSkill` → `IAbilityRunner`), префабы врагов с `AICharacter` и ассеты оружия и умений в
`Configs/AI/`.

---

## 3. Ядро

### 3.1 Агент

```csharp
// Корень префаба врага: оркестратор сущности
[DefaultExecutionOrder(-150)]                     // думает до шага тела (CharacterBody — -100)
public sealed class AICharacter : MonoBehaviour
{
    [SerializeField] private BehaviorGraphAsset _graph;   // граф; тело, оружие и умения — поля разделов
    [SerializeField] private BlackboardOverrides _overrides;
    [SerializeField] private int _seed;                   // 0 — случайный

    public Blackboard Blackboard { get; }
    public BehaviorGraphInstance Graph { get; }
    public IAgentActuator Actuator { get; }
    public AgentPerception Perception { get; }
    public AgentNavigation Navigation { get; }
    public AgentAbilities Abilities { get; }
    public SquadMembership Squad { get; }

    public bool IsThinking { get; set; } = true;          // пауза мозга (катсцены, тесты)
    public IAIWorld World { get; }                        // общие факты и расписание; null — один

    public void Step(float dt);                           // шаг: решение (если срок и мир разрешил) + исполнение
    public void SetWorld(IAIWorld world);
    public void SetSeed(int seed);
}
```

Разделение `Think()` / `Tick(dt)` — принципиальное (взято у crashkonijn GOAP: «решение» и «исполнение» —
разные компоненты): решения можно прореживать и резать бюджетом, исполнение идёт каждый шаг, иначе движение
дёргается.

### 3.2 Blackboard

- Типизированные слоты: `int/float/bool/string/Vector2/Vector3/enum/Object/Transform/AgentRef/List<T>` и свои
  `[Serializable]`-структуры (это то, чего не умеет Unity Behavior — см. исследование §3).
- **Области:** `Agent` (своя), `Squad` (общая на отряд), `Global` (мировая). Узел ссылается на переменную
  указанием области и имени; имя компилируется в индекс при компиляции графа.
- **Перекрытия на экземпляре:** `BlackboardOverrides` на компоненте — `AttackRange = 1.8` у одного врага и
  `2.6` у другого на том же графе.
- **События:** `Changed(index)` и «переходы по изменению переменной» без опроса.
- Отладка: просмотр значений в окне графа в рантайме, лог изменений по флажку.
- **Тег — слот без значения** (просто «факт есть»): дешёвая замена `bool`-переменной для «оглушён», «в
  укрытии», «заметил» ([Research_AI_Code §8](Research_AI_Code.md)).
- **Ключ в инспекторе выбирается из списка, а не вводится строкой** (атрибут + драйвер свойства). Типизацию
  поверхности **не писать руками**: либо дженерик `BlackboardKey<T>`, либо генерация — в разобранном
  фреймворке ручная типизированная поверхность дала два copy-paste-бага подряд (`HasFloat` смотрит в чужой
  словарь, `DelQuaternion` удаляет из чужого).
- Для структур — доступ по ссылке (`ref T Get<T>()` через обёртку-ref), чтобы менять по месту и не
  перезаписывать слот целиком.
- **В коде (этап 3а):** слот создаёт первая запись имени (это изменение: `Changed`, версия растёт) и живёт до
  `Clear`; `Remove` его опустошает, ключ остаётся годным. Чтение никогда не объявляет. Теги — `TagKey` и
  `SetTag/HasTag/RemoveTag`; структура по месту — `Ref<T>(key)`, запись тихая, `NotifyChanged` — явно. Узел знает
  переменную по имени и читает через `BlackboardVariable<T>` (поле узла): имя хешируется один раз, отсутствующее
  ищется заново, только когда blackboard вырос (`Layout`), `Clear` обнуляет ключи (`Generation`); область,
  указавшая на другой blackboard (агент вошёл в мир), разрешается заново. Отдельного прохода связывания нет.
  Переход по изменению — `TransitionTrigger.VariableChanged` (`GoToOnChange`): изменения считаются с входа в
  состояние, каждое видно ребру один раз.

### 3.3 Сигналы (общение между ИИ)

```csharp
public readonly struct Signal
{
    public readonly SignalId Id;          // "EnemySpotted", "TakingFire", "AllyDied", "Help", "Order"
    public readonly Object Sender;
    public readonly Vector3 Position;
    public readonly float Value;
    public readonly Object Subject;       // о кого речь (цель, объект)
}

public interface ISignalBus
{
    void Broadcast(in Signal signal, SignalScope scope);   // радиус / фракция / отряд / все
    void Subscribe(ISignalListener listener, SignalFilter filter);
}
```

В графе — узлы `OnSignal` (вход в состояние по сигналу, режимы `Default/Restart/Once/Queue` — семантика
Unity Behavior), `SendSignal`, условие `HasSignalSince(id, seconds)`. Шина — сервис модуля, по умолчанию
синглтон-пустышка (без неё агент работает, просто никто никого не слышит; как `ISplatter` в игре).

### 3.4 Расписание и LOD

- `AgentScheduler` (один на мир, в `AIWorld`): **не шагает агентов** — только разрешает решать.
  Лимит `MaxDecisionsPerStep` — счётом, не миллисекундами (бюджет на настенных часах делает поведение
  машинозависимым и ломает «один сид — одна трасса»); фазовый сдвиг первых решений при регистрации;
  `Stopwatch` только вокруг `Think` и только как замер. Сделано в этапе 2.
- Ярусы LOD (`AgentLod`) удалены в этапе 2 как поле без потребителя (инвариант 12) и возвращаются «по
  требованию» сразу правильно — сон как ноль, а не «реже» (план, таблица «по требованию»). Ниже — целевые числа.
- Частоты по умолчанию: решения `Near 0.05с / Medium 0.15 / Far 0.5 / Offscreen 2`; восприятие — вдвое реже
  решений; исполнение — каждый шаг (кроме `Offscreen`: упрощённое).
- Профайлер: образец на класс узла и на агента (под `ENABLE_PROFILER`), как в `GameLoopRunner`.
- **Период сканирования сенсора — диапазон, а не число** (`0.2…0.3 с`, значение из `Random` агента): агенты
  расходятся по кадрам сами, без общего распределения фаз ([Research_AI_Code §9](Research_AI_Code.md)).
  Обёртка `Throttle(behaviour, min, max)` применима к любому тику — сенсору, дорогому условию, запросу позиции.
- **Рассылка по списку слушателей идёт по копии** (или с обратным проходом): узел и скилл имеют право
  подписаться/отписаться прямо в тике.

---

## 4. Граф поведения

> **С шага 89 (14.11.2026) граф — только модель мозга, и полотно — это вся программа персонажа**
> ([AIKit_BrainOnly](AIKit_BrainOnly.md)): невидимый корень с сенсорами и плоский список нод; один `Brain`, к нему
> проводами — решения (приоритет, «нельзя из», `Do`), за `Do` — цепочка блоков по выходам (`Done` / `Failed` /
> пронумерованные выходы `Random` / `On Tick`). Граф стартует в мозге; без мозга не работает вовсе; нода, до которой
> не доходит ни одна цепочка, не запускается никогда. Удалены: старт с первой ноды, рефлексы (`Global`, стрелки
> сенсоров `Detected` / `Lost`), вложенные состояния и условия входа, условия и области прерывания на стрелках,
> триггеры «сигнал» и «изменилась переменная», «начать заново» (`ReselectWhenComplete`), подмена подграфа ролью,
> композиты BT (`Sequence`, `Selector`, `Parallel`…, `PrioritySelect`, `UtilitySelect`, `Subgraph`, `Capability`),
> ноды-код (`FuncTask` / `FuncCondition`), условия, кроме `Compare` / `AND` / `OR` (вопрос ноды `Branch`), задачи,
> читавшие цель восприятия неявно (`MoveToTarget`, `FaceTarget`, `KeepDistance`, `CombatSpacing`, `Stop`, `Send
> Signal`, `Search Around`). Подразделы 4.1–4.3 ниже — история модели до шага 89.

### 4.1 Узлы: пять семейств

| Семейство | Что это | Примеры |
|---|---|---|
| **State** | состояние с набором параллельных задач и условиями входа | `Patrol`, `Chase`, `Attack`, `Flee` |
| **Task** | исполняемое действие внутри состояния, возвращает `Running/Success/Failure` | `MoveTo`, `FaceTarget`, `UseAbility`, `Wait`, `PlayAnim`, `Claim(slot)` |
| **Flow** | композиты и модификаторы | `Sequence`, `Selector`, `Parallel`, `Random`, `Cooldown`, `Repeat`, `TimeOut`, `Inverter`, `Guard` |
| **Decision** | выбор ветки/варианта по оценке | `UtilitySelect`, `PrioritySelect`, `SwitchOn(variable)`, `GoapGoal`, `HtnTask` |
| **Transition** | ребро с условием, приоритетом и областью прерывания | `OnCondition`, `OnSignal`, `OnTaskFinished`, `OnVariableChanged`, `Next` |

Плюс служебное: `Subgraph` (вызов другого графа; статический и динамический через переменную),
`Capability` (включаемый блок поведения: набор состояний+переходов, который можно выключить на архетипе),
`Note` (стикер).

**Планировщик — не узел-исполнитель, а источник плана.** Решение после разбора рабочего кода
([Research_AI_Code §7](Research_AI_Code.md)): `GoapGoal`/`HtnTask` не «делают», а выдают **план из имён
операторов**, который граф исполняет последовательностью уже существующих задач по таблице
«имя оператора → задача/поддерево»:

```
Sensors → WorldState → Planner.Decide(plan[], out size)   // чистая логика, тестируется без сцены
                          │
                          └─> PlanSequence: plan[i] → таблица имён → AgentTask / Subgraph
```

Что это даёт: планировщик остаётся чистым C# (сид, `dt`, никакой сцены), набор «что умеет тело» описан один
раз узлами, а **архетип превращается в данные** — снайпер, боец ближнего боя и фланкёр отличаются только
доменом/целями над одним и тем же набором операторов. Декоратор планировщика с `OverrideDomain` /
`RollbackDomain` — это механизм, которым отрядный слой подменяет мозг на время роли (§8).

### 4.2 Семантика (решения, которые делают граф предсказуемым)

1. **Выбор состояния — по требованию, не каждый кадр.** Переходы проверяются от активного листа к корню;
   первый сработавший запускает выбор с его цели вниз (условия входа отбрасывают непригодные ветки).
   Это StateTree-модель; она же решает проблему «BT пересчитывает всё дерево каждый тик».
2. **Один think-тик = одна перепроверка.** Все условия и все переходы агента считаются ровно один раз
   за think-тик, в фиксированном порядке (рефлексы → глобальные → от листа к корню). Никаких «наблюдателей,
   которые срабатывают вне активного пути» — это главная болячка Unreal BT (см. исследование §2.2).
3. **Область прерывания у ребра, а не у декоратора:** `Self` (перезапустить состояние), `Branch` (выйти из
   поддерева), `Global` (прервать что угодно — рефлексы: получил урон, умер, командир отозвал).
   Область видна на графе цветом/иконкой. В коде (этап 3а): `Branch` оставляет общих предков старого и
   нового пути, `Global` выходит из всего пути и входит заново от корня (общие предки тоже перезапускаются);
   `Global`-ребро, чья цель уже активна, не срабатывает — рефлекс с держащимся условием не рвёт граф каждый
   think-тик (перезапустить активное — ребро `Self`).
4. **Задачи состояния работают параллельно, пока состояние активно** (как Global/State Tasks в StateTree),
   и получают `OnEnter/OnTick/OnExit(reason)` — `reason` отличает «сам завершился» от «прервали».
   **Фоновая задача** (`AgentTask.IsBackground`; `FaceTargetTask` — фоновая по умолчанию) тикается, пока
   состояние активно, и после `Success` входит заново; в «задачи состояния завершены» она не считается.
   **Лист без основных задач** (без задач или только с фоновыми) — «режим-метка»: держится, пока не сработает
   его ребро, и не перевыбирается каждый think-тик. Лист, чьи задачи завершились без ребра, перевыбирается от
   корня; если выбор вернул его же, задачи запускаются заново, а `TimeInState` продолжает расти — состояние
   не покидали (так `MinStateTime` срабатывает и у листа с мгновенно падающими задачами).
   **Один граф — один агент:** второй `BehaviorGraphInstance` занятого графа бросает исключение
   (`AICharacter.SetGraph`/`OnDestroy` освобождают граф через `Release`); общий граф на многих — этап 9.
5. **Статусы задач:** `Running/Success/Failure`; состояние может объявить «успех = выход по переходу
   `OnTaskFinished`».
6. **Детерминизм:** порядок обхода стабилен, рандом — из агентского `System.Random`, время — `dt`.
7. **Условие — двухуровневое и чистое** (§1.4, пункты 5–6): `AgentCondition` объявляет дешёвый гейт и
   дорогой тест отдельно, движок графа не зовёт дорогой, если гейт не прошёл. Это и перф, и предсказуемость:
   дорогие тесты считаются только там, где они могут изменить решение. В коде (этап 3а): `Gate` и `Evaluate`;
   граф открывает think-тик на контексте (`ThinkStamp`), и внутри него условие отвечает один раз — второе ребро
   в то же поддерево, поиск листа и перевыбор получают тот же ответ; вне think-тика (задачи, умения) — каждый
   вызов заново. `Squad` у агента без отряда — отдельный пустой blackboard и одно предупреждение, а не его свой.
8. **Запросы к миру кешируются на один think-тик.** Линия взгляда, достижимость, расстояние до цели, список
   союзников — посчитались один раз и переиспользуются всеми условиями этого тика (у них это «кеши с
   отметкой кадра» в координаторе). Иначе пять условий в одном переходе дают пять одинаковых рейкастов.
9. **Переход «сам завершился» и «прервали» различимы** всегда: `OnExit(reason)` получает причину, и граф
   не может «тихо» перезапустить состояние — перезапуск только по ребру с областью `Self`.
10. **Жизненный цикл узла — в базовом классе, а не в узлах** ([Research_AI_Code §5](Research_AI_Code.md)):
    `Tick` сам вызывает `OnEnter` на первом тике и `OnExit` на любом выходе (успех, провал, прерывание), узел
    пишет только `OnUpdate`. Тогда «забыл сбросить состояние на входе» становится невозможным, а составные
    узлы могут безопасно продолжаться между кадрами (индекс текущего ребёнка сбрасывается ровно во входе).
11. **Узел не хранит состояние механики.** Счётчики, индексы маршрута, выбранная цель — в blackboard или на
    агенте (видно в инспекторе, сохраняется, доступно другим узлам); в узле — только кеш ссылок и локальный
    таймер текущего исполнения. Так же устроены use-case функции в разобранном уроке: состояние — на сущности,
    логика — чистая функция.

### 4.3 Пример графа (ближний боец 2D)

```
ROOT (Selector по условиям входа, проверка от листа к корню)
│
├── [Global] Dead          (условие: Health.IsDead)        → Task: Stop, DisableBrain
├── [Global] Hurt          (условие: Hurt.IsHurt)          → Task: PlayFlinch; переход: !IsHurt → Combat
│
├── Combat                 (условие: Perception.Awareness > 0.8)
│   ├── Approach           (условие: Distance > Ability.MaxRange)
│   │   └── Tasks: MoveTo(Target, stop = Ability.MaxRange*0.9), FaceTarget
│   │       переходы: Distance <= MaxRange && HasToken → Strike
│   │                 Distance <= MinRange            → Spacing
│   ├── Strike            (условие: HasToken && !AnyAbilityOnCooldown)
│   │   └── Tasks: UtilitySelect(abilities) → UseAbility(selected), FaceTarget
│   │       переходы: OnTaskFinished → Recover
│   ├── Recover           → Tasks: Wait(Recovery), KeepDistance(Prefer)
│   │       переходы: Next → Approach
│   └── Spacing           → Tasks: KeepDistance(MinRange..MaxRange), FaceTarget
│
├── Search                (условие: Perception.HasMemory)
│   └── Tasks: MoveTo(LastKnownPosition), LookAround(SearchTime)
│       переходы: Awareness > 0.8 → Combat;  OnTaskFinished → Patrol
│
└── Patrol                (всегда пригодно — последняя ветка)
    └── Tasks: PatrolRoute(points | pingPong), Wait(PauseAtPoint)
        переходы: OnSignal("EnemySpotted") → Search
```

Это ровно то поведение, что сейчас зашито кодом в `EnemyAIController` + 8 состояний — но графом, с теми же
числами и без попарных переходов.

### 4.4 Свой узел — один класс

```csharp
[Serializable, AIKitNode("Movement/Move To", Category = NodeCategory.Task)]
[NodeDescription("Moves the agent to a point until it is closer than Stop Distance.")]
public sealed class MoveToTask : AgentTask
{
    [Tooltip("Where to go")]
    public BlackboardValue<Vector3> Destination;

    [Min(0f)] public float StopDistance = 0.5f;
    public bool FaceMoveDirection = true;

    protected override void OnEnter(AgentContext context) =>
        context.Navigation.SetDestination(Destination.Get(context));

    protected override TaskStatus OnTick(AgentContext context, float dt)
    {
        if (context.Navigation.RemainingDistance <= StopDistance)
            return TaskStatus.Success;

        if (!context.Navigation.HasPath)
            return TaskStatus.Failure;

        context.Navigation.Step(dt);          // путь + steering → актуатор
        return TaskStatus.Running;
    }

    protected override void OnExit(AgentContext context, ExitReason reason) =>
        context.Navigation.Clear();
}
```

Требования, которые это обеспечивает: узел появляется в меню редактора без правки редактора (атрибут +
рефлексия один раз при компиляции домена), сериализуется через `[SerializeReference]`, тестируется как обычный
класс (`new MoveToTask().OnTick(fakeContext, 0.02f)`).

`BlackboardValue<T>` — «константа или переменная»: в инспекторе переключатель. Так дизайнер пишет `2.5` там,
где программист подставит переменную.

**Альтернатива для мелких условий и действий: ассет — фабрика делегата.** Приём из разобранного урока
([Research_AI_Code §6](Research_AI_Code.md)): сериализуемый класс не *является* условием, а **создаёт** его
один раз на агента, после чего в рантайме работает замыкание и ни одного обращения к ассету нет:

```csharp
public interface IAgentConditionAsset { Func<bool> Create(AgentContext context); }
public interface IAgentActionAsset    { Action   Create(AgentContext context); }

[Serializable]
public sealed class TargetFartherThanAsset : IAgentConditionAsset
{
    [SerializeField] private BlackboardKey<float> _distanceKey;

    public Func<bool> Create(AgentContext context)
    {
        var body = context.Body;
        var target = context.Blackboard.Slot<Transform>(Keys.Target);
        return () => target.Value != null &&
                     (target.Value.position - body.Position).sqrMagnitude >
                     context.Blackboard.Get(_distanceKey) * context.Blackboard.Get(_distanceKey);
    }
}
```

Когда что: **полноценный узел** (`AgentTask`/`AgentCondition`) — когда нужны `OnEnter/OnExit`, состояние
исполнения и статусы; **ассет-фабрика** — когда нужно «одно выражение в инспекторе» (условие перехода,
мелкое действие на входе в состояние), и тогда это дешевле и быстрее.

### 4.5 Хранение и исполнение

> **В коде (этап 3в).** Узлы — данные: `[Serializable]`, публичные поля, конструктор без параметров,
> `[AIKitNode(Path, Category, Id)]` (стабильный id типа), реестр `AIKitNodeRegistry`; `AgentNode.Id` нумерует
> `GraphBuilder.Build`, `Transition.TargetId`, `EnterState(id)`; `OverrideSubgraph`/`RollbackSubgraph` — роль
> подменяет дерево выбора. До этапа 9 один граф — один агент (второй экземпляр бросает).

- **Авторский ассет** `BehaviorGraphAsset : ScriptableObject`: список узлов (`[SerializeReference]`),
  рёбра, объявления blackboard, позиции узлов для редактора, стабильные `Id` узлов (переименование типа
  безопасно).
- **Компиляция**: ассет → `CompiledGraph` (плоские массивы узлов/переходов, индексы вместо ссылок,
  предрасчитанные порядки проверки). Один раз на ассет, кешируется.
- **Экземпляр**: `BehaviorGraphInstance` на агента — только изменяемое состояние (активный путь, таймеры,
  состояния задач), берётся из пула. Сто агентов на одном графе = один `CompiledGraph` + сто лёгких
  экземпляров.
- **Валидация** при компиляции: висячие рёбра, недостижимые состояния, нет ветки по умолчанию, пустое
  состояние, ссылка на несуществующую переменную → понятная ошибка в консоли с кликом на ассет.

---

## 5. Восприятие (`AIKit.Perception`)

> **В коде (этап 3б).** Тип цели уже окончательный: `TargetTrack` в `AgentContext.Track` (`Subject` —
> дескриптор `TargetSubject`: `Id` + лениво разрешаемый `Transform`, `ITargetResolver`; `Awareness`,
> `LastKnownPosition`, `TimeSinceSeen`); уничтоженный или ушедший в пул субъект сбрасывается сам. Пробы —
> `ISightProbe`, `IGroundProbe`, `IAllyProbe` в ядре, реализации `PhysicsProbes2D/3D` в адаптерах;
> `Context.CanSeeTarget()` спрашивает пробу раз за think-тик. Сенсоры и `AgentPerception` ниже — этап 7, тип
> трека при этом не меняется.

```csharp
public sealed class AgentPerception                   // компонент на Components
{
    public TargetTrack Best { get; }                  // самая «уверенная» цель
    public IReadOnlyList<TargetTrack> Tracks { get; }
    public float Awareness => Best?.Awareness ?? 0f;
    public bool HasMemory => Best != null && Best.TimeSinceSeen < Profile.MemoryTime;

    public void Alert(Transform source, float awareness = 1f);   // нечестный вход: удар в спину, крик союзника
    public void Tick(float dt);                                  // реже решений, свой фазовый сдвиг
}
```

- `PerceptionProfile` (ассет или сериализуемый класс в разделе Senses персонажа): конусы зрения (дальность, угол, вес —
  периферия/фокус), скорость набора и спада внимания, время памяти, слух (радиус, затухание за стенами),
  маски стен и цели, фракции.
- **Треки с уверенностью** вместо бинарного «вижу» (§4 исследования): пороги дают бесплатные состояния
  Notice/Combat/Search, и игрок читает реакцию.
- Сенсоры-плагины: `VisionSensor`, `HearingSensor`, `DamageSensor` (пришёл урон → `Alert`),
  `SignalSensor` (крик союзника), `ProximitySensor`, свой — наследник `AgentSensor`.
- Гизмо: конусы, лучи, треки с цифрой уверенности, последняя известная позиция.
- **Поиск кандидатов — через пространственный индекс**, а не перебором всех
  ([Research_AI_Code §9а](Research_AI_Code.md)): `AgentIndex` (хеш-сетка на мир, сервис модуля) с запросом
  «все в радиусе с предикатом» в буфер из пула; агент регистрируется сам в `OnEnable`, снимается в
  `OnDisable`, позицию в индексе обновляет **с гистерезисом** (сместился больше порога), плюс периодическая
  полная пересборка как страховка. Без индекса (мало агентов) — честный перебор по реестру, интерфейс тот же.
- **Период сенсора — диапазон из рандома агента** (§3.4), фильтр — предикатом в запросе, а не отсевом после.

---

## 6. Движение: навигация, steering, актуатор

### 6.1 Актуатор — граница с телом

```csharp
public interface IAgentActuator
{
    void Move(Vector3 direction, float speedScale = 1f);   // 2D-платформер читает только X
    void Face(Vector3 direction);
    void Jump(float holdTime);                             // «подержать кнопку» — высота прыжка
    void Crouch(bool crouch);
    void DropThrough();                                    // вниз сквозь one-way платформу
    void Dash(Vector3 direction);
    void Stop();
    void Apply(float dt);                                  // сбросить накопленные «нажатия» в тело

    AgentBodyInfo Body { get; }   // IsGrounded, Velocity, Position, Facing, Radius, Height, JumpHeight, JumpReach
}
```

Умения через актуатор **не** идут: их играет `IAbilityRunner` (§7.1) — так тело остаётся «геймпадом», а бой
подключается к тому, что в игре уже есть, не расширяя контракт тела на каждую новую абилку.

Адаптеры:

- `CharacterBodyActuator` (`AIKit.Platformer2D`) — обобщение `EnemyCommands` + `Apply(dt)`:
  одноразовые нажатия, удержание прыжка, `TryDash`, `AttackSkill.Press()`.
- `NavMeshAgentActuator` (`AIKit.NavMesh`) — `NavMeshAgent` как движок перемещения (или только как
  навигатор: `CalculatePath` + свой `Move`).
- `TransformActuator` — для прототипов, толпы и тестов (двигает `transform` без физики).
- Свой — один класс, 7 методов.

**Арбитр движения: один токен на ось.** Как только в графе появляется больше одного источника движения
(ведение по пути, поле потока, страйф вокруг цели, прохождение ребра, монтаж умения, реакция на удар), они
начинают писать в тело в одном кадре — и агент дёргается или едет не туда. Решение из шипнутой практики
([Research_AI_Games §12.6](Research_AI_Games.md)) — чистая логика без `MonoBehaviour`, **по экземпляру на
ось**: один арбитр на передвижение, один на поворот.

```csharp
public enum MoveAuthority { None, Steering, Path, Strafe, Traversal, Frozen }   // строгий порядок приоритета

public struct MoveClaim { internal int Id; internal MoveAuthority Authority; }

public sealed class MovementArbiter
{
    public bool TryClaim(MoveAuthority authority, object owner, out MoveClaim claim);  // свободно / тот же владелец / приоритет СТРОГО выше
    public bool IsValid(in MoveClaim claim);     // производитель проверяет каждый тик и молчит, если токен отобрали
    public void Release(in MoveClaim claim);     // по токену; устаревший Release — no-op
}
```

Правила: клейм выдаётся, если слот свободен, это тот же владелец или приоритет **строго выше** (тогда
прежний токен инвалидируется); освобождение — только по токену; **производитель обязан каждый тик проверять
`IsValid` и перестать писать в тело, если токен отобрали**. `Frozen` (монтаж умения, стан, посадка в
транспорт) берёт **обе** оси — тело принадлежит тому, что его играет. `Steering` — фоновый уровень, любой
явный движитель его вытесняет.

### 6.2 Навигация

> **В коде (этап 3б).** `SetDestination` возвращает `NavVerdict` (`Reachable`, `Detour`, `Unreachable`,
> `OffGraph`, `NoGraph`), состояние следования — `NavStatus` (`Idle`, `Planning`, `Following`, `Unreachable`):
> навигатор сам говорит «пути нет», и `MoveToTask`/`MoveToTargetTask` падают без `TimeOutTask`. Актуатор —
> `SetVertical(axis)` и `HoldTimeForHeight(rise)`, `AgentBodyInfo` += `CanClimb`, `CanDropThrough`, `HasDash`,
> `DashReach`, `StepHeight`. Ниже — исходный эскиз контракта.

```csharp
public interface INavigator
{
    bool SetDestination(Vector3 point);
    bool HasPath { get; }
    float RemainingDistance { get; }
    NavEdgeKind CurrentEdge { get; }        // Walk / Jump / Fall / Ladder / Dash / Drop
    Vector3 SteeringTarget { get; }         // следующая точка пути
    void Step(float dt);                    // путь + steering → актуатор
    void Clear();
}
```

Поиск пути под навигатором (`IPathFinder`) — **буферное API с предикатом проходимости**, по образцу
разобранного модуля ([Research_AI_Code §10](Research_AI_Code.md)):

```csharp
public interface IPathFinder
{
    // основное: без аллокаций, путь пишется в буфер владельца
    bool FindPath(in NavPoint start, in NavPoint end, NavPoint[] path, out int pathSize,
                  Predicate<NavNode> passable = null);

    // удобное: построено на первом, буфер берётся из ArrayPool<T>.Shared
    NavPoint[] FindPath(in NavPoint start, in NavPoint end, Predicate<NavNode> passable = null);
}
```

- **Проходимость — предикат запроса**, а не свойство графа: прыгучий враг, нелетающий и тот, кто не умеет
  лестницы, ходят по одному графу (`node => body.CanTraverse(node)`).
- Эвристика — стратегия (`Euclidean/Manhattan/Octile`), алгоритм — фабрика по типу; **новый вид графа
  добавляется своей функцией соседей**, а цикл A* остаётся тем же (так в разборе сделан JPS — подкласс A* с
  другой `GetNeighbours`).
- Тривиальные случаи (невалидные концы, `start == end`, «концы — соседи») отсекаются **до** запуска поиска.

Реализации:

1. **`ReactiveNavigator`** — без графа: идти в сторону цели, прыгать перед стеной/обрывом (обобщение
   `PathSensorComponent`). Для 80% врагов 2D-платформера этого достаточно; работает сразу, ничего не печёт.
2. **`PlatformerGraphNavigator`** (`AIKit.Navigation` + построитель в `AIKit.Editor`) — граф площадок с
   рёбрами-способами (`Walk/Jump/Fall/Ladder/Drop`), построение из коллайдеров слоя земли и tilemap,
   проходимость рёбер считается **из возможностей тела** (`JumpHeight`, `JumpReach` из `AgentBodyInfo`,
   у нас это `MaxJumpHeight/MaxJumpDistance` из `EnemyAIContext`), стоимость — оценка времени.
   Алгоритм построения — §5.3 исследования. Пересчёт куска графа в рантайме (двери, разрушенные платформы).
3. **`GridNavigator`** — плоский 2D (топ-даун): сетка по коллайдерам/tilemap, A*, string-pulling.
4. **`NavMeshNavigator`** — 3D (и 2D через NavMeshPlus), `NavMeshLink` как `Jump`-ребро.
5. **`FlowFieldNavigator`** (в `AIKit.Crowd`) — одно поле на цель для массы агентов.

**Прохождение ребра** — ключевая часть 2D: навигатор не «едет к точке», а исполняет ребро:
`Walk` → `Move(dir)`; `Jump` → `Face` + `Jump(hold)` с расчётом удержания по высоте; `Fall` → подойти к краю
и шагнуть; `Drop` → `DropThrough()`; `Ladder` → `Move` по вертикали. Это то, чего нет в готовых ассетах и что
делает 2D-ИИ живым.

Правила ребра, которые берём из шипнутого проекта ([Research_AI_Games §12.5](Research_AI_Games.md)):

- **Проходимость ребра считается от тела** (`AgentBodyInfo`: высота и дальность прыжка, габарит), а не от
  чисел в графе: один граф уровня обслуживает и прыгучего, и неуклюжего — ребро просто не берётся.
- **Лимит пользователей на ребро** (по умолчанию 1): две капсулы на одном узком переходе выглядят как брак.
- **Агент у занятого ребра ждёт у горловины и перепроверяет**, а не объявляет отказ и не ищет новый путь.
- **Журнал отказов**: 3 неудачи подряд выключают ребро на N секунд (у них 30) — ловит «ребро построено, но
  пройти нельзя» без правки данных; попадает в отладочный оверлей, чтобы это увидел дизайнер.
- **Признак успеха зависит от типа ребра**: падение проверяется приземлением ниже старта, перелаз
  заканчивается на той же высоте (общее правило «упал» помечало бы каждый перелаз отказом).

**Контракт эскалации** — обязательная часть `INavigator`, когда навигаторов больше одного (реактивный →
граф/NavMesh). Навигатор отвечает не «да/нет», а вердиктом, и владелец решает, кому передать агента
([Research_AI §5.1](Research_AI.md)):

```csharp
public enum NavVerdict { Reachable, Detour, Unreachable, OffGraph, NoGraph }
```

Переключение на дорогой навигатор — только когда вердикт плохой **или** обход велик **и** прямая линия
геометрически закрыта (большой обход сам по себе может быть просто толпой). Обратно — с гистерезисом
(разные пороги входа и выхода, подтверждение 0.5–0.75 с, бэкофф на повторную эскалацию). И пока работает
дорогой навигатор, дешёвый **не имеет права подменять направление** — иначе его градиент каждый тик тащит
агента назад на «короткую» дорогу (их отдельный баг и отдельный флаг-заглушка).

**Классификация «подход / боковое» — один раз при выдаче запроса**, а не каждый тик: подход к цели ведётся
по живой цели (цель движется), боковое перемещение (точка обхода, отход, страйф) — по пути к точке.
Проверка каждый тик неверна в обе стороны: кайтящий игрок превращает подход в «боковое» (агент бежит туда,
где игрок был), а игрок, вставший на точку обхода, превращает обход в «подход».

### 6.3 Steering

- Контекстные карты (`ContextMap`, §5.5 исследования): поведения пишут interest/danger, арбитраж один.
  В 2D-платформере карта вырождается до «влево/вправо + прыжок», в 3D/топ-дауне — 8–16 слотов.
- Готовые поведения: `SeekTarget`, `KeepDistance(min..max)`, `AvoidPit`, `AvoidAlly`, `StrafeAround`,
  `Flee`, `FollowPath`, `Wander`, `Separation`.
- Локальный обход: `Separation` по умолчанию; RVO — опциональный адаптер (A\*PP), не зависимость.

---

## 7. Бой и умения (`AIKit.Combat`)

### 7.1 Умение описывается данными

> **В коде (этап 3в).** `AbilityDefinition` в окончательной форме (`Range` — `RangeBand`, `RequiresLineOfSight`,
> `MinAwareness`, `WindUp`, `Recovery`, `TokenCost`, `CancellableByHurt`, `Reduction`); ось с вето
> (`Consideration.Evaluate(ctx, out score)`, `Required`), `UtilityScore`, компенсация — среднее геометрическое;
> стратегия выбора — `ISelectionStrategy` у владельца набора (`AgentAbilities.Strategy`), не у узла.

```csharp
[Serializable]
public sealed class AbilityDefinition
{
    public AbilityId Id;                  // связка с игровым скиллом и анимацией
    public float Weight = 1f;             // базовый вес в случайном выборе
    public float Cooldown = 1f;           // считается от КОНЦА умения
    public float StartDelay;              // чтобы в начале боя не телеграфировали все сразу
    public RangeBand Range;               // Min/Max, нужна ли земля, нужна ли линия взгляда
    public float WindUp = 0.25f;          // телеграф
    public float Recovery = 0.4f;         // окно уязвимости после
    public int TokenCost = 1;             // жетоны атаки на цели
    public float RepeatPenalty = 0.5f;    // анти-повтор
    public ConsiderationSet Utility;      // оси оценки + кривые отклика
    public bool CancellableByHurt = true;
}
```

- **Выбор:** `AbilitySelector` = фильтр по условиям (дистанция, линия, кулдаун, жетон, земля/воздух) →
  utility-оценка (произведение осей с компенсацией) → взвешенный случайный выбор из топ-N. Прямое обобщение
  `AttackChooser` (веса, кулдауны, условия, `RepeatPenalty`, сид) из игры.
- **Устройство оценки — по разобранному фреймворку** ([Research_AI_Code §2](Research_AI_Code.md)):
  - ось возвращает `bool Evaluate(out Score)`, и `false` — это **вето** («нет цели», «нет патронов»), а не
    ноль: вариант не рассматривается и это видно в трассе;
  - у варианта **две группы осей**: обязательные (отказ убивает вариант) и необязательные (только меняют
    счёт), функция свёртки — параметр (`Mul` по умолчанию, `Sum` для «накопительных» оценок);
  - **выбор — сменная стратегия** `IAbilityStrategy`: `Max(порог)`, `Random(порог)`, `RandomWeighted`,
    `TopRandom(N, порог)`; порог отсечения живёт у стратегии, рандом — из `AgentContext.Random`;
  - `Score` — свой тип с клэмпом ≥ 0 и неявными приведениями; оценка всех вариантов — в `stackalloc`-буфере,
    ноль аллокаций на think-тик; отдельный метод «все счёты» — только для окна отладки;
  - кривые отклика — полный набор IAUS с единой параметризацией (slope, exponent, offset.x, offset.y + клэмп):
    `Linear`, `Polynomial`, `Logistic`, `Logit`, `Normal`, `Sine`, `Hyperbolic`, `Const`, `Custom`
    (`AnimationCurve`) + декораторы `Invert` и `Multiply`. **Каждая кривая — со своим тестом** (в разобранном
    коде покрыта только линейная, и в гауссиане из-за этого σ не влияет на ширину).
- **Исполнение:** `IAbilityRunner` — адаптер к игровому умению:

```csharp
public interface IAbilityRunner
{
    AbilityId Id { get; }
    bool CanStart(AgentContext context);
    void Begin(AgentContext context);          // не Start: у MonoBehaviour это сообщение Unity
    AbilityPhase Phase { get; }          // WindUp / Active / Recovery / Done
    void Cancel();
}
```

В игре: `AttackSkillRunner` (жмёт `AttackSkill.Press()`), `DashSkillRunner`, `SmashSkillRunner`. В модуле:
`TimedAbilityRunner` (телеграф → событие `Released` → восстановление) — для прототипов и демо.

> **Умение ассетом (шаг 26, с 13.11.2026 — шаг 80 [AIKit_Flow](AIKit_Flow.md)).** Карточка-компонент `AgentAbility` заменена ассетом `AbilityAsset` в списке `Abilities` раздела Combat: данные те же, раннер (`AbilityRunner`) заводит `AICharacter` на каждого персонажа (`IAbilitySource` отдаёт агенту `AbilityDefinition`).
> имя умения = `Id` = имя действия аниматора; эффект — `OnStrike` (подкласс: граната стенда). `At Once` — пул
> жетонов умения и его ёмкость (`TokenPool`, новое поле `TokenCapacity`). Цель из переменной сенсора — «прицел»
> `AgentContext.AimAt(Transform)`: пока он задан, `Target`, `HasTarget`, `DistanceToTarget`, `CanSeeTarget` и
> `Engagement` отвечают про этот объект. `Use Ability` и `An Ability Is Ready` выведены на полотно (поле `Target`,
> у ноды один пин `Done` — `[AIKitNode(Finished = …)]`); с прицелом «готово» учитывает и свободное место в пуле.
>
> **Решатель — `Combat Brain` (шаг 28 [AIKit_Flow](AIKit_Flow.md)).** Состояние с меткой `CombatBrainTask`: его
> переходы по условию — правила по порядку (`AgentState.MoveTransitionUp`). `BehaviorGraphInstance` помнит, чьё действие
> идёт (`Brain`), и раз в think-тик после собственных переходов действия спрашивает правила (`Supervise`,
> `Transition.ConditionsHold`): тот же победитель — законченное действие входит заново, другой — переключение, никого —
> возврат в состояние мозга; пока задача «занята» (`AgentTask.CommitsFor`: удар, умение) — ждёт. Рефлекс снимает
> владение. `CounterTask` / `CounterCondition` — число на доске агента по имени; `NodeLastsCondition` — время в
> состоянии с данным именем. От шага 27 остались `AbilityDefinition.RequiresPlace`, `AttackTokens.HoldsPlace`,
> `AbilityBlock`, `AbilityPlaces`.

- **Жетоны и слоты на цели** (`AIKit.Coordination`): `AttackTokens` (сколько могут бить одновременно),
  `SurroundSlots` (позиции вокруг цели), `LaneSlots` (линии стрельбы). Обобщение `AttackSlotsComponent`:
  `CanTake/TryTake/Release` + автоосвобождение при смерти/выключении.
- **Рефлексы** (`Global`-переходы): урон → `Hurt`, смерть → `Dead`, «цель за спиной» → `TurnAround`.

### 7.2 Пространство боя

`CombatSpacing` компонент: желаемая дистанция по выбранному умению, `Strafe`, `Reposition` при потере линии,
отход после серии (`Retreat`), «не стой там, где стоит союзник» (слоты).

Числа дистанций принадлежат **роли**, а не узлу графа: у давящего кольцо отхода маленькое, у заднего ряда —
большое, у стрелка — свой диапазон. Три приёма оттуда же ([Research_AI_Games §12.5–12.6](Research_AI_Games.md)):

- **Держащий позицию не уступает в сторону цели.** Когда давящего толкают союзники, он скользит *вокруг*
  цели, а не внутрь неё: иначе толпа вдавливает передний ряд в игрока.
- **Окно восстановления после атаки**: короткое время (≈1 с) и радиус, внутри которых агент не ломится
  обратно вплотную — читается как «отшагнул после удара», стоит ноль.
- **Колонна подхода**: когда несколько агентов идут одним маршрутом, скорость внутри колонны масштабируется
  (идущий первым не ускоряется, задние не телескопируют) — дешёвая замена «очередям на узких местах».

---

## 8. Координация (`AIKit.Coordination`)

- **`SquadBrain`** — тот же граф, но «тело» у него отряд: ставит приказы в `Squad`-blackboard
  (`Order = Attack/Hold/Flank/Fallback`, `FocusTarget`, `RallyPoint`), назначает роли по utility-матрице
  (роли переназначаются при потерях — §7.1 исследования).
  **Сделано иначе (шаг 88 в [AIKit_Flow](AIKit_Flow.md), 14.11.2026):** компонент `AI Squad` (объект-знамя, список
  бойцов) крутит **командный граф** отряда: `Squad Sight` → `Squad Brain` (`SquadBrainTask`) → решения «когда»
  (`SquadDecisionTask`: `Surround` / `Hold The Line` / `Advance` / `Form Up`) → блоки «что делает строй»
  (`SquadBlockTask`: `Form Ring` / `Hold Line` / `March To Target` / `Return To Banner`). Граф двигает только строй
  (`Squad`, `Runtime/Core/Squads`: слоты фаланги или колец вокруг виртуального якоря, замена павших сзади, скорость по
  сплочённости). Боец остаётся со своим графом: решение `Follow Squad` → `Keep Formation` → `Strike`, а свои решения
  поважнее (кайт, лазание, камни) выводят его из строя и возвращают. Приказов строкой в доске нет (§22.1 учебника).
- **`SquadMembership`** на агенте: ссылка на отряд, своя роль, чтение приказа как обычного факта
  («держи позицию X», но *как* дойти и когда ударить решает сам).
- **Запросы позиций** (`PositionQuery`): кандидаты (узлы графа навигации / слоты вокруг цели / сетка) →
  фильтры (видимость, дистанции, занятость, опасность) → utility-оценка → лучший. Аналог Unreal EQS в
  миниатюре; для платформера — запрос по узлам графа.
- **Карты влияния** (`InfluenceMap`, опционально): слои «мои/враги/опасность/недавние смерти», штампы,
  затухание. Даёт информацию, а не команды (принцип Dave Mark).
- **Умные объекты** (`SmartObject`): объявленные взаимодействия со слотами (`Claim/Occupy/Release`),
  точка входа, условия, «реклама» (сила предложения) — Sims + Unreal Smart Objects в одном.
  Узлы графа: `FindSmartObject(type, filter)`, `UseSmartObject`. Так дизайнер наполняет уровень поведением
  (укрытия, турели, рычаги, точки засады) **без кода**.
- **Шина сигналов** (§3.3): «вижу врага», «меня бьют», «помогите», «граната», «союзник умер» + барки.

### 8.1 Координатор: один арбитр на мир

Главное правило (§1.4, пункт 1): **арбитраж централизован, исполнение децентрализовано — агент не
договаривается с агентом**. `AgentWorld` вырастает в координатор: реестр агентов + список «стычек»
(`Engagement` — цель и её текущий ростер). Агент только спрашивает и исполняет.

Чтобы координатор не стал God Object'ом (их же пункт долга №1: 8335 строк в одном файле), делим его сразу
по осям — каждая часть пользуется только реестром и списком стычек:

| Часть | Решает | Ключевые правила |
|---|---|---|
| `RoleAssignment` | кто давит, кто обходит, кто кружит | ёмкость роли — **формула от размера ростера** (`clamp(ceil(N·0.2),1,3)` давящих и т. п.), назначение **событийное и детерминированное** (ростер изменился → один проход), а не «кто первый попросил» |
| `TargetSelection` | кого бьёт каждый агент | счёт: дистанция + загрузка цели + мало HP + «память угрозы» (кто меня бил, полураспад ~6 с); бонус текущей цели, выдержка между сменами, **порог переключения** (новая цель лучше в 1.5 раза), внеплановый пересчёт только по заметному событию |
| `AttackTokens` | кому можно бить сейчас | **пулы по типу атаки** (ближний/дальний/мелочь) со своими лимитами; одномоментные запросы **созревают окно и ранжируются вместе**; вытеснение по приоритету, но **никогда не отмена начатого замаха**; гейт по дистанции и «радиус, на котором жетон ещё можно выиграть»; приоритет тому, к кому игрок подошёл в упор (радиус — на длине оружия); пауза после опустошения пула |
| `ApproachGroups` | как толпа идёт к цели | общий маршрут + масштаб скорости в колонне (§7.2) |
| `StationRegistry` | кто занимает объект уровня | объект регистрируется сам, агент **бронирует**; бронь — слой эффективности с TTL, право проверяет сам объект; **журнал недостижимости** на агента; цепочка контекста «авторский список → зона → без ограничений», и непустой авторский список не проваливается в «поищу рядом» |

Эскалация против глухой защиты живёт здесь же: N заблокированных ударов за T секунд → одному агенту
выдаётся «непробиваемая» атака, с минимальным интервалом и кулдауном на кластер, чтобы такие атаки не
выходили пачкой ([Research_AI §6.3](Research_AI.md)).

### 8.1а Второй режим: отряд собирается сам вокруг намерения

Координатор (§8.1) — директивный режим. Рядом живёт недирективный, и он дешевле для мелких стычек
([Research_AI §7.1а](Research_AI.md), код — [Research_AI_Code §12](Research_AI_Code.md)):

```csharp
public abstract class Intent                      // намерение, под которое собирают отряд
{
    public int MinMembers { get; }                // ≥ 2
    public int MaxMembers { get; }
    public float PendingTime { get; }             // таймаут сборки

    public abstract bool SameAs(Intent other);                 // «то же намерение» (обычно по цели)
    public abstract void CreateRoles(IReadOnlyList<AICharacter> members, List<ISquadRole> roles);
    public virtual bool StillValid(Squad squad) => true;       // цель жива и не ушла — отряд жив
}
```

- `SquadMatchmaker.Join(agent, intent)`: подсадить в активный отряд с таким же намерением → иначе в ожидающий
  → иначе открыть новый ожидающий. Набрался максимум — активируем сразу; истёк таймаут с составом ≥ минимума —
  активируем; иначе распускаем.
- Активный отряд тикает роли, выкидывает участника, чья роль перестала быть актуальной, и умирает, когда
  состав упал ниже минимума или намерение перестало быть валидным. **Состав изменился — роли пересобираются.**
- **Роль = подграф (`Capability`) + свои сенсоры**, а не строка-приказ: роль на входе включает агенту свою
  ветку поведения (`OverrideSubgraph`) и свои сенсоры, на выходе — снимает. Данные, которые знает только
  отряд («самый раненый союзник», «кто без патронов»), роль пишет участнику в blackboard.
- Роль хранится **рядом с участником** (в записи отряда), а не параллельным списком по индексу; удаление из
  состава — обратным проходом (в разобранном коде именно здесь баг: прямой цикл с удалением пропускает
  участника).

Какой режим когда: координатор — когда дизайнер должен гарантировать картину боя (кто давит, кто обходит,
сколько бьют одновременно); матчмейкинг намерений — когда нужна дешёвая кооперация без центра («двое идут на
одну цель — пусть разойдутся по ролям»). Выбор — флажок архетипа, оба пользуются одними ролями.

### 8.2 Директор и цепочка приоритетов настроек

Слой выше координатора (`EncounterDirector`, «по требованию» — см. [план](AIKit_Plan.md)) **не управляет знанием агентов** — он крутит числа
слоёв под собой: множители ёмкости атакующих, кулдауна жетонов, задержки респауна, лимита населения,
состав волны. Интенсивность считается от участия со **усреднением по отряду**, чтобы один герой не разгонял
темп.

Цепочка приоритетов пишется в документации **одной лесенкой** и соблюдается кодом — тогда «почему слишком
легко/сложно» отлаживается проходом по ней ([Research_AI_Games §12.8](Research_AI_Games.md)):

```
архетип (ролсет, профиль роли)        — авторская база
 → профиль сложности                  — глобальный
  → оверрайд сущности/спавнера        — на этого врага и на эту стычку
   → оверрайд сцены/стычки            — временный, по месту
    → множитель директора             — поверх полученного
     → отладочный форс (инспектор)    — всегда побеждает
```

Общий **бюджет создания** — тоже здесь: одна очередь спавна на мир, N попыток на кадр, round-robin между
спавнерами, случайная задержка выхода (волна «струйкой», а не пачкой).

---

## 9. Толпа (`AIKit.Crowd`)

Отдельная подсистема, не «много агентов»:

- `CrowdSystem`: общее `FlowField` на цель (Dijkstra по сетке), `SpatialHash` для соседей,
  boids (`Separation/Alignment/Cohesion`), LOD-уровни (полный агент → упрощённый → только позиция),
  бюджет и time slicing.
- `CrowdUnit` — лёгкая запись (struct) в массиве, без `MonoBehaviour` на агента; визуал — пул спрайтов/мешей.
- **Повышение/понижение**: юнит толпы рядом с игроком становится полноценным `AIAgent` (и обратно).
- API «jobs-ready»: данные в плоских массивах, функции без состояния — перевод на джобы/ECS возможен позже,
  но в первой версии их нет (по просьбе — ECS отдельной темой).

---

## 10. Авторинг: как пользователь настраивает ИИ

### 10.1 Путь «5 минут» (главный сценарий)

С 13.11.2026 персонаж — **один компонент `AI Character`** ([AIKit_Character](AIKit_Character.md)), без `Controllers` /
`Components` и без провайдеров графа.

1. *Tools → AI Kit → Setup Agent* на префабе: визард ставит `AICharacter` на корень (и `Simple Body`, если тела не
   дали шаги адаптеров: `NavMesh Body` для `NavMeshAgent`).
2. В разделе **Behavior** — граф-ассет (`BehaviorGraphAsset`); в **Body** — тело; в **Senses** — профиль
   восприятия; в **Animation** — имена параметров Mecanim; в **Combat** — оружие и умения.
3. Поправить числа в инспекторе: дистанции, время реакции, веса умений, маршрут патруля.
4. Запустить и смотреть отладку.

Граф открывать нужно только когда нужно **новое** поведение.

### 10.2 Тип врага — префаб-вариант и ассеты, не архетип

Ассета-архетипа (`AgentArchetype`) больше нет: тип врага — компонент на префабе, варианты префаба («этот враг как
тот, но без прыжков») перекрывают его поля. Это осознанное отличие от учебника §32.1: общий ассет с состоянием
запрещён C.5, а всё, что переиспользуется, — ассеты без состояния:

```csharp
[CreateAssetMenu(menuName = "AI Kit/Weapon")]
public class WeaponAsset : ScriptableObject    // приёмы WeaponMove: Name, Action, Reach, Damage, WindUp, Recovery, NeedsSight
{
    public virtual void Strike(in StrikeContext strike);
}

[CreateAssetMenu(menuName = "AI Kit/Ability")]
public class AbilityAsset : ScriptableObject   // имя = действие аниматора, дальность, кулдаун, вес, At Once…
{
    public virtual void OnStrike(in StrikeContext strike);
}
```

Состояние умения (пауза, фаза, цель) держит `AbilityRunner` персонажа, замаха оружия — память ноды `Attack`.

### 10.3 Окно графа

- `AIKit.Editor` на **GraphView** (есть в 6000.5, проверен; Graph Toolkit — экспериментальный и без рантайма,
  переезд позже за собственным слоем абстракции — см. §10 исследования).
- Панели: граф, инспектор узла, blackboard, библиотека узлов (поиск), ошибки валидации.
- **Рантайм-отладка (обязательна):** подсветка активного состояния и пути, цвет узла по последнему статусу,
  значения blackboard живьём и правка на ходу, лог решений («выбрал `AirSlash`: dist 0.82 × hp 0.9 × token 1 =
  0.74; `Combo` = 0.61»), трасса последних N решений с перемоткой, выбор агента кликом в сцене.
- Удобства: подграфы, копирование ветки, «извлечь в подграф», стикеры, выравнивание, поиск по графу.

### 10.4 Отладка в сцене

`AgentDebugComponent`: гизмо восприятия, путь и текущее ребро (цветом по способу), желаемая дистанция,
занятые жетоны, подпись состояния над головой (как сейчас `Handles.Label` у `EnemyAIController`),
экранный оверлей по выбранному агенту.

---

## 11. Встраивание в проект

- **Игровой цикл:** `AgentLoopComponent` (в игре, `Gameplay/Character/Components/AI`) переводит агента в ручной
  режим и раз за фиксированный шаг зовёт `Step(dt)` (порядок −150, до тела −100): решение по сроку и разрешению
  мира, затем исполнение; на паузе не шагает (как `CharacterLoopComponent` для CharacterKit). Сделано в этапе 2.
- **Сервисы:** мир `IAIWorld` (общий blackboard и `AgentScheduler`) создаёт и регистрирует `GameServices`;
  `AIWorldLoop` открывает шаг мира (порядок −200) и стирает общие факты при смене сцены. Агент берёт мир через
  `ServiceLocator.TryGet` в `AgentLoopComponent` и без него живёт автономно. Шина сигналов и ростер войдут в
  тот же мир (этапы 7–8).
- **Умения игры** подключаются рантайм-адаптерами на `Components` врага.
- **Миграция существующего врага:** граф `EnemyMelee2D` воспроизводит текущий FSM (Patrol→Notice→Chase→
  Attack→Retreat→Search→Flee→Hurt) с теми же числами из `EnemyAIConfig`; старый `EnemyAIController` остаётся,
  пока новый не пройдёт те же тесты (`EnemyTestBase`), потом удаляется. Это лучший тест плагина: он обязан
  воспроизвести поведение, уже покрытое 100+ тестами.

---

## 12. Тестирование

- `BrainRig` (PlayMode, в тестах модуля): фейковое тело (`TestActuator` с `AgentBodyInfo`), фейковое
  восприятие, ручные шаги, фиксированный сид — проверяем поведение графа без физики.
- Реальный стенд: тесты игры на префабе врага через `CharacterTestRig` (как сейчас) — проверяем адаптер и
  навигацию на реальной геометрии.
- EditMode: семантика узлов (`Sequence`, `Selector`, `Abort`, `Cooldown`), blackboard, utility-кривые и
  компенсация, компиляция и валидация графа, построение графа платформера на синтетических коллайдерах,
  планировщики.
- Детерминизм: один сид → одна трасса решений (сравнение трасс).
- Дымовой перф-тест: N агентов × M шагов укладываются в бюджет (относительно, без жёстких мс).
- **«Постановочные» агенты** (пресеты в `AIKit.Demo`, по образцу `ST_Test_*` из
  [Research_AI_Games §12.11](Research_AI_Games.md)): агент, который вечно делает ровно одно — только бьёт,
  только блокирует, только кружит, только бежит по ребру. Нужны, чтобы **мерить боевую фичу против
  известного партнёра**, а не против полноценного ИИ (иначе тест боя проверяет два ИИ сразу). С
  координатором такие агенты не регистрируются.
- **Что тестируется без сцены** — больше, чем кажется: координатор и ростер, ёмкости и назначение ролей,
  выбор цели со счётом, пулы жетонов, **арбитр движения**, LOD и сон, вердикты навигатора, валидация
  ассетов. Сцена нужна только там, где нужна геометрия и тело.
- **Лист регрессии** в README модуля: таблица «фича → автотесты + ручные проверки». Видно, что покрыто, а
  что каждый раз проверяется руками.
- **Геометрию и графы в тестах описывать матрицей**, как они выглядят глазами (строка 0 — верх), а не
  собирать руками из коллайдеров; к ней — именованные заготовки (`Flat`, `Step`, `Gap`, `LShape`).
  Приём из разобранных тестов навигации ([Research_AI_Code §13](Research_AI_Code.md)).
- **`[assembly: InternalsVisibleTo("AIKit.Tests")]` для шагов алгоритмов**: эвристика, проверка соседства,
  склейка подпланов, выбор перехода тестируются по отдельности, а не только «на входе цель — на выходе план».
- Обязательный минимум, который в разобранном коде пропущен (и каждый пропуск = живой баг): тест на **каждую**
  кривую отклика, тест на **конструктор**, который присваивает поля, тест на `Set/Has/Del` каждого типа
  blackboard, тест на **удаление из коллекции во время обхода**.

---

## 13. Бюджеты производительности (целевые)

| Что | Цель |
|---|---|
| Решение (think) одного агента | < 0.05 мс средне, < 0.2 мс пик |
| Бюджет решений на кадр | 2–3 мс (настройка) |
| Исполнение (tick) агента | < 0.02 мс |
| Аллокации в тике | 0 байт |
| Агентов «рядом» на кадр | 50+ без просадки |
| Юнитов толпы | 2000+ (упрощённый LOD) |

**Ярусы LOD** (`AgentLod`, числа — настройка; устройство — из [Research_AI §9](Research_AI.md), где приведён
замер «из 77 агентов в бою реально 6.7»):

| Ярус | Когда | Думает |
|---|---|---|
| `Active` | в бою или близко к камере | каждый think-интервал |
| `Near` | есть активный путь или средняя дистанция | ×2–3 реже |
| `Idle` | ни боя, ни пути, далеко | ×10 реже |
| `Asleep` | дальше порога сна и не в бою | **не думает вовсе**: сенсоры выключены, подписка на цикл снята, навигация очищена; состояние и объект живы |

Правила: критерий «есть активный путь» важнее дистанции (далёкий идущий агент должен идти ровно);
пробуждение — с **гистерезисом** (порог пробуждения ближе порога сна) и по событию (урон, сигнал, приказ);
«глушить анимацию и физику» — отдельный флажок, **выключен по умолчанию** (меняет качество картинки);
фазовый сдвиг think-тиков, чтобы агенты не думали в один кадр.

---

## 14. Принятые по умолчанию решения (пользователю — подтвердить)

Пока пользователя нет, фиксирую выбор с обоснованием; поменять любое — дешево, пока не начат код.

1. **Имя модуля** — `AIKit` (в пару к `CharacterKit`). Альтернатива: `BrainKit`, `MindKit`.
2. **Формат решений** — один граф StateTree-типа (состояния+задачи+переходы) с BT-композитами и
   utility-селекторами; GOAP/HTN — **«по требованию»**, шов «планировщик → план → граф» заложен предпосылками
   (узлы как данные и стабильные `Id` — этап 3, `Subgraph` — этап 9). Альтернатива: чистое BT (проще
   редактор, хуже держит состояние).
3. **Редактор** — GraphView сейчас, слой абстракции для переезда на Graph Toolkit позже; сам редактор —
   последний содержательный этап плана, путь «пять минут» закрывается архетипом без окна.
4. **Сериализация** — `[SerializeReference]`-узлы в одном ассете + компиляция в рантайм-представление.
5. **Навигация** — `ReactiveNavigator` доводится до паритета с нынешним врагом (этап 5); граф площадок
   (`PlatformerGraphNavigator`), grid и JPS — **«по требованию»**, триггер и цена возврата в плане.
6. **Бой** — декларативные `AbilityDefinition` + адаптеры к существующим скиллам игры (не переписываем бой).
7. **Толпа** — **«по требованию»** (триггер: больше ~50 одновременных агентов), без ECS.
8. **Миграция врага игры на модуль** — обязательный этап приёмки, а не «когда-нибудь»; по плану это этап 4,
   через расходные мосты к компонентам игры.
9. **Координация — централизованная** (один координатор на мир, агент не обращается к агенту), §8.1.
   Альтернатива — чисто эмерджентная: дешевле на старте, но «кто сейчас бьёт» становится невоспроизводимым.
   **План срезает координатор в «по требованию»**, оставляя из него ростер, стычки, жетоны и `CombatSpacing`
   (этап 8) — решение ждёт подтверждения (открытый вопрос 2 плана).
10. **Арбитр движения с токенами на ось**: детектор «два писателя в ось» — этап 2, настоящий
    `MovementArbiter` — этап 8 (спор за ось создают параллельные задачи и `CombatSpacing`, а не второй
    навигатор), §6.1.
11. **Жетоны атаки — пулами по типу атаки** (ближний/дальний/мелочь), а не один счётчик, §8.1.
12. **Инварианты (§1.4) — часть приёмки:** каждый пункт закрыт либо тестом, либо валидацией ассета, либо
    строкой в ревью-чеклисте README.
13. **Планировщик выдаёт план, граф исполняет** (§4.1): GOAP/HTN не «делают», а пишут план из имён
    операторов; связка — таблица «имя → задача/поддерево». Альтернатива («планировщик сам ведёт агента»)
    делает его непроверяемым без сцены.
14. **Utility: вето вместо нуля, обязательные и необязательные оси, стратегия выбора** (§7.1).
15. **Ассет-фабрика делегата** для мелких условий и действий, полноценный узел — для всего, что имеет
    `OnEnter/OnExit` и статусы (§4.4).
16. **Буферные API по умолчанию** (путь, план, счёты — в буфер с `out size`), удобные перегрузки — поверх,
    с памятью из пула (§6.2, §13).
17. **Две модели координации рядом** (§8.1 и §8.1а), выбор — флажок архетипа; роли общие.

## 15. Что сделано (этапы 1–10, модуль закрыт)

Код — `Assets/Game/Scripts/Modules/AIKit`, описание, «пять минут», API и «как расширять» — его
[README](../../Game/Scripts/Modules/AIKit/README.md); история этапов с отступлениями — [план](AIKit_Plan.md).

- **Этапы 1–3:** ядро (агент, blackboard с областями, граф состояний с семантикой §4.2, BT-композиты, utility с вето),
  шов с игрой (`AgentLoopComponent`, мир `AIWorld`, трасса решений данными), контракты ядра (цель треком, вердикты
  навигации, узлы как данные со стабильным id, сид от мира).
- **Этапы 4–5:** враг игры на модуле, паритет навигации (`ReactiveNavigator`), старый мозг удалён.
- **Этап 6:** архетип ассетом, визард *Setup Agent*, *Validate All*; второй тип врага только ассетами.
- **Этап 7:** восприятие (`AgentPerception`: внимание, лестница тревоги, память, поиск) и шина сигналов.
- **Этап 8:** ростер, стычки, жетоны атаки, фазы умения, арбитр движения, дистанция боя.
- **Этап 9:** граф — общее описание, состояние — в памяти агента (`NodeMemory`); ассет графа с валидатором,
  узлы ассета (Guard, Priority/Utility Select, SwitchOn, Random, Subgraph, Capability), окно графа с отладкой в Play,
  читы; враг игры на ассете (трасса равна графу из кода), третий тип врага только ассетами.
- **Этап 10:** покрытие рантайм-сборок, README финальным, тесты инвариантов по исходникам и по asmdef, мёртвое
  (`[ComingWithStage]`) удалено. 3D-адаптер `AIKit.NavMesh` и `Demo3D` — **прототип** (тестов нет; возврат — в
  таблице «по требованию»).

**Инварианты §1.4 — поимённо:**

| № | Инвариант | Чем закрыт |
|---|---|---|
| 1 | Агент не обращается к агенту | союзники и жетоны — запросы к ростеру: `ProbesOnBodyTests.Agent_FindsTheProbesOnItsEntity`, `AttackTokensTests.Roster_AllyAhead_ByPositions_NotByPhysics` |
| 2 | Случайность только из контекста | `InvariantTests.Invariant2_NoUnityRandom_InTheModule`, `TruthTests.SameSeed_SameTrace` / `OtherSeed_OtherTrace`, `BrainContractTests.SeedZero_InAWorld_ComesFromTheWorld_AndReplays` |
| 3 | Время только `dt` | `InvariantTests.Invariant3_NoEngineTime_ButTheAgentsOwnStep`, игра: `AgentLoopTests.Pause_Agent_NeitherMovesNorThinks`, `SchedulerTests.Step_RegisteredAgent_AdvancesOncePerStep` |
| 4 | Одна ось — один владелец | `CombatCoreTests.Arbiter_StrongerClaimWins_EqualOneDoesNot`, `Arbiter_FrozenTakesBothAxes`, `Arbiter_TwoMoversInParallel_OnlyTheStrongerMovesTheBody`; нарушение видно `AgentComponentTests.TwoTasksMovingInOneStep_AreReported` |
| 5 | Двухуровневые условия | `GraphSemanticsTests.ClosedGate_TheExpensiveTestIsNotCalled`, `BrainContractTests.CanSeeTarget_OutOfRange_TheProbeIsNotAsked` |
| 6 | Условие только читает | `TruthTests.Conditions_OnlyRead_TheBlackboard`, `UtilityCoreTests.AbilityAvailableCondition_DoesNotConsumeTheRandom` |
| 7 | Задача не сканирует мир | `InvariantTests.Invariant7_NodesDoNotScanTheWorld`; реестр объектов уровня с бронью — **не реализован**, приходит с умными объектами («по требованию») |
| 8 | Игра ↔ модуль | `PortabilityTests.NoModuleAssembly_RefersToTheGame`, `TheCore_RefersToNothing` |
| 9 | Ассеты-листья | `InvariantTests.Invariant9_ASubgraphLeadingBack_IsACycle` (валидатор графа: `SubgraphCycle`); архетип → граф без обратных ссылок по устройству |
| 10 | Один владелец значения | `ArchetypeTests.Validator_SeedAndAbilitySetTwice` (`SecondOwner`); числа врага — только `EnemyAIConfig`, граф читает их с доски |
| 11 | Ноль аллокаций в тике | `TruthTests.Step_TraceOff_AllocatesNothing`, `GraphAssetTests.SharedGraph_HotPath_DoesNotAllocate`, `PerceptionTests.Perception_StepsWithoutAllocating` |
| 12 | Поле без потребителя — баг | `ArchetypeTests.EveryField_IsReadAtRuntime`; переменная без объявления в ассете графа — `GraphAssetTests.Validator_UnknownVariable_ATypoIsAnError_NotASecondVariable` |

**Решения по умолчанию, принятые в ходе работы** (пересмотр — с пользователем): сначала 2D, 3D — прототипом;
граф в коде остаётся первым классом (билдер и ассет пишут одни данные, равенство трасс — тест); толпа, планировщики,
координатор — «по требованию»; тюнинг врагов в `Sandbox_Enemies` — вместе с пользователем.

---

## Приложение — вопрос → куда смотреть

Приём из [Research_AI_Games §12](Research_AI_Games.md): отладка ИИ начинается не с кода, а с порядка
проверок. Таблица — часть спека и переезжает в README модуля по мере готовности подсистем (сейчас
реализованы только строки про граф и умения).

| Вопрос | Порядок проверок |
|---|---|
| «Почему он не атакует?» | условия умения (дистанция, линия, кулдаун, ресурс) → жетон атаки (§8.1: пул, лимит, гейт по дистанции, «радиус, на котором жетон можно выиграть») → кольцо отхода роли (§7.2) → трасса решений агента |
| «Почему он стоит на месте?» | клеймы арбитра движения (§6.1: кто держит ось и почему) → вердикт навигатора (§6.2) → активная задача состояния (не `Running` ли `Wait`) → проходимость ребра от тела |
| «Почему он идёт в стену / в угол?» | вердикт и эскалация навигатора (§6.2) → рёбра графа навигации и их журнал отказов → steering: не перекрыт ли интерес опасностью |
| «Почему он выбрал это умение?» | трасса utility (оценки осей по умению) → анти-повтор и кулдауны → пороги стойки/дистанции (§7.1) |
| «Почему они все набежали одновременно?» | ёмкости ролей (§8.1) → пулы жетонов и пауза волны → колонна подхода (§7.2) → бюджет создания и задержка выхода (§8.2) |
| «Почему бой слишком лёгкий/сложный?» | строго по цепочке приоритетов (§8.2), сверху вниз |
| «Почему ИИ тормозит?» | ярусы LOD и сон (§13) → бюджет расписания и фазовый сдвиг → маркеры профайлера по фазам (восприятие / решение / навигация / исполнение) → аллокации в тике |
| «Почему поведение невоспроизводимо?» | сид агента и сид мира (§1.4, пункт 2) → чтение времени не из `dt` → порядок коллекций → `UnityEngine.Random` в модуле |

<!-- NEXT -->

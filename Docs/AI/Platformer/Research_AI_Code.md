# Исследование: разбор учебного кода ИИ (готовый фреймворк на Unity)

Разбор **кода**, а не публикаций: папка `C:\Users\MY PC\Downloads\Extracted 2026-10-31` — десять проектов
из учебных материалов высокого качества (Unity, C#, Odin, Unity.Mathematics, Job System). Внутри — почти
полный производственный AI-фреймворк: Utility AI, GOAP, HTN, Behaviour Tree, FSM, Blackboard, Team AI,
навигация по сетке (A*/JPS/Dijkstra/волна) и карты влияния на джобах, с тестами.

Для нас это второй по ценности источник после шипнутой архитектуры
([Research_AI_Games §12](Research_AI_Games.md)): там — *что* шипнуто и почему, здесь — **как это написано на
C# в Unity**, с теми же задачами, что у нашего `AIKit`.

Справочник «как делать» — **[AI_Handbook](AI_Handbook.md)**; сводный вход в тему — **[AI](AI.md)** (принципы, архитектура, трассируемость, состояние).

Часть серии: [Research_AI](Research_AI.md) (техники и теория) · [Research_AI_Games](Research_AI_Games.md)
(разборы игр) · [Research_AI_Genres](Research_AI_Genres.md) (жанры и роли) ·
[Research_AI_Tools](Research_AI_Tools.md) (инструменты) · [Research_AI_Unity](Research_AI_Unity.md)
(практика Unity) · **Research_AI_Code** (этот файл) · [Research_AI_Sources](Research_AI_Sources.md)
(библиография). Спек модуля — [AIKit](AIKit.md), план — [AIKit_Plan](AIKit_Plan.md).

> **Статус материала.** Чужой учебный код, лежит вне проекта (в `Downloads`), в репозиторий не копируется.
> Берём **устройство, приёмы и выводы**, а не файлы: прямое копирование притащило бы Odin, `Unity.Mathematics`,
> Atomic и их соглашения в модуль, который по нашим правилам должен быть без зависимостей.

---

## 0. Что в папке

| Папка | Что внутри по ИИ | Ценность |
|---|---|---|
| `UnityProject (2)` | **`Plugins/AI` — весь фреймворк** (180 файлов): BehaviourTree, FSM, Goap, HTN, UtilityAI, TeamAI + тесты; игровая часть: HTN-домены ботов, 20 BT-узлов, роли отряда, инсталлеры | ★★★★★ |
| `UnityProject (1)`, `UnityProject.v.2` | тот же проект на другой итерации: GOAP-ветка (`Reasoners/Goap`), utility-considerations, `DecisionBehaviour`/`PlanningBehaviour` | ★★★★ |
| `Конец урока (3)` | модули `Navigation` (сетка, A*, JPS, Dijkstra, волна, эвристики + ~1000 строк тестов) и `Goap` (свой планировщик + 1200 строк тестов) | ★★★★★ |
| `Конец урока` | `Plugns/AI`: Blackboard (int-ключи, кодогенерация API, инсталлеры), BehaviourTree, StateMachine, Behaviours (композит, throttle), Gizmos, Elements (And/Or/Not) | ★★★★★ |
| `InfluenceMap (JobSystem)` | модуль карт влияния: `NativeArray` + `IJobParallelFor` + Burst, шаблоны-штампы, запросы (max в радиусе, min в секторе) + тесты | ★★★★ |
| `Lesson FSM.v.2`, `Конец урока (1)` | урок про FSM: состояния и условия **ассетами**, `StateFactory`, use-case функции, сенсор цели | ★★★★ |
| `Конец Урока (2)` | BT-узлы для бота-добытчика (найти ресурс → подойти → собрать → отнести), use-cases | ★★★ |
| `UnityProject` | `DetectTargetBehaviour`, `SpatialBehaviour`, `SpatialUnitSystem` (пространственный индекс) | ★★ |

Проекты построены на фреймворке **Atomic** (`Plugins/Atomic`): сущность — `IEntity` как набор значений и
поведений, сборка — инсталлерами (`SceneEntityInstaller`), доступ к значениям — через **сгенерированное API**
(`entity.GetHealth()`, `entity.GetTargetEnemy()`). Это не наша архитектура (у нас компоненты по
[CompositionGuide](CompositionGuide.md)), но ровно две идеи оттуда переносимы и полезны: **инсталлер как место
сборки ИИ** и **сгенерированные типизированные аксессоры вместо строковых ключей**.

---

## 1. Общая картина: пять способов решать, один контракт на исполнение

Во фреймворке сосуществуют пять моделей принятия решений, и это не каша: у каждой свой контракт, и
**исполнение всегда отделено от решения**.

```
Сенсоры  ──> факты (WorldState / Blackboard)
                 │
                 ├── UtilityAI   : решения со счётом → лучший ключ
                 ├── GOAP        : цель + действия   → план (список действий)
                 ├── HTN         : домен задач       → план (массив имён операторов)
                 ├── FSM         : состояния+переходы→ текущее состояние
                 └── BT          : дерево узлов      → Running/Success/Failure
                 │
исполнение  ──> BehaviourTree из реальных узлов ──> запросы телу (move/look/fire)
```

Главный архитектурный вывод всего разбора (§7): **планировщик выдаёт план из имён операторов, а исполняет
план дерево поведения, собранное по таблице «имя → узел»**. Планирование символьное и дешёвое, исполнение
поведенческое и честное (Running/Success/Failure, вход/выход, прерывание).

---

## 2. Utility AI: самая аккуратная часть фреймворка

### 2.1 Счёт — свой тип, а не `float`

```csharp
[Serializable]
public struct Score : IEquatable<Score>, IComparable<Score>
{
    private double _value;

    public double value { get => _value; set => _value = math.clamp(value, 0.0, double.MaxValue); }
    public Score normalized => new(math.clamp(_value, 0.0, 1.0));

    public static implicit operator Score(double score) => new(score);
    public static implicit operator double(Score score) => score._value;
    // операторы сравнения и арифметики + равенство по эпсилону
}
```

Приём: **домен-тип вместо `double`** — счёт нельзя сделать отрицательным (клэмп в конструкторе и сеттере),
сравнение по относительному эпсилону, неявные приведения, так что в арифметике он читается как число. Нам
это прямо применимо к `Score`/`Weight` в `AIKit.Combat`.

### 2.2 Consideration с правом вето

```csharp
public interface IConsideration
{
    bool Evaluate(out Score score);      // false = «я не могу дать значение» = вето на всё решение
}

public abstract class AbstractConsideration : IConsideration
{
    private readonly ICurve _curve;

    public bool Evaluate(out Score score)
    {
        if (!this.TryGetValue(out double value)) { score = 0; return false; }
        score = _curve.Evaluate(value);
        return true;
    }

    protected internal abstract bool TryGetValue(out double value);
}
```

Это аккуратнее, чем «ось вернула 0 → решение умерло»: **ноль и «нет данных» — разные вещи**. Нет цели →
`TryGetValue` вернул false → решение «атаковать» даже не считается (и это видно в отладке), а не получает
счёт 0 и тонет среди других нулей.

Декораторы на осях — `Invert` (1 − x) и `Multiply(coef)` + extension-методы `consideration.Invert()`,
`consideration.Multiply(0.5f)`.

### 2.3 Decision: обязательные и необязательные оси

```csharp
public bool Evaluate(out Score resultScore)
{
    resultScore = _weight;

    for (int i = 0; i < _considerations.Count; i++)                  // обязательные
    {
        if (_considerations[i].Evaluate(out Score score))
            resultScore = _reduceFunction.Invoke(resultScore, score); // Mul или Sum
        else
        {
            resultScore = 0;
            return false;                                            // вето
        }
    }

    for (int i = 0; i < _optionalConsiderations.Count; i++)          // необязательные
        if (_optionalConsiderations[i].Evaluate(out Score score))
            resultScore = _reduceFunction.Invoke(resultScore, score);

    return true;
}
```

Два списка осей — **обязательные (их отказ убивает решение) и необязательные (только меняют счёт)** — закрывают
80% ручной работы, которую в наивном utility приходится делать условиями снаружи. Функция свёртки (`Mul`/`Sum`)
— параметр решения, а не зашитое умножение.

### 2.4 Выбор — отдельная стратегия, а не «максимум»

```csharp
public interface IStrategy
{
    bool SelectBest(in Span<Score> buffer, in int count, out int bestIndex);
}
```

Реализации: `MaxStrategy(minThreshold)`, `RandomStrategy(minThreshold)` (равновероятно среди прошедших
порог), `RandomWeightedStrategy` (рулетка по весам), `TopRandomStrategy(topCount, minThreshold)` (случайный
из топ-N). Порог отсечения — у стратегии: решение со счётом ниже порога не берётся **никогда**, даже если оно
лучшее.

Агент считает все решения в `stackalloc`-буфере и отдаёт стратегии:

```csharp
Span<Score> scores = stackalloc Score[decisionCount];
Span<int> decisions = stackalloc int[decisionCount];
// … заполнили только те решения, которые прошли вето …
if (!_strategy.SelectBest(scores, scoreCount, out int bestIndex)) return false;
```

**Ноль аллокаций на think-тик** и `EvaluateScores()` отдельным методом для отладки (все счёты как
`Dictionary<TKey,double>` — только для инспектора).

### 2.5 Библиотека кривых = канонический IAUS

Все кривые — `ICurve { double Evaluate(double value); }` поверх `ClampedCurve(min,max)`, параметризация
одинаковая: **наклон (slope), показатель (exponent), смещение (offset.x, offset.y)** — то есть `m`, `k`,
`b`, `c` из IAUS Дэйва Марка.

| Кривая | Формула (внутри clamp) | Когда |
|---|---|---|
| `LinearCurve` | `slope * (x − offX) + offY` | «чем ближе, тем лучше» |
| `PolynomialCurve` | `slope * (x − offX)^exp + offY` | квадратичные/кубические акценты |
| `LogisticCurve` | `exp * 1/(1 + e^(−slope·(x+offX))) + offY` | плавный порог «включилось» |
| `QuantileCurve` (logit) | `(slope * ln((x+offX)/(1−(x+offX))) + offY) / exp` | обратная логистике |
| `NormalCurve` | гауссиана | «оптимум в середине» (желаемая дистанция) |
| `SineCurve` | `exp * sin(slope·(x − offX)) + offY` | периодика |
| `HyperbolicCurve` | `slope/(x + offX) + offY` | «очень важно вблизи нуля» |
| `ConstCurve`, `CustomCurve` | константа, `AnimationCurve` | заглушка и ручная кривая |
| `InvertCurve`, `MultipliedCurve` | `1 − f(x)`, `w·f(x)` | декораторы |

У нас в [Research_AI §2.4](Research_AI.md) была упрощённая `ResponseCurve` — вот полный набор с готовыми
формулами; `CustomCurve` поверх `AnimationCurve` — дешёвый способ дать дизайнеру «нарисовать» ось.

### 2.6 Чему учат их баги

- `NormalCurve`: `-(1.0 / _exponent * _exponent * 2.0)` вычисляется как `−2.0` — **σ не влияет на ширину
  гауссианы**. Тесты кривых покрывают только `LinearCurve`, поэтому баг жив.
- `TopRandomStrategy.ComparePairs` сортирует пары `(Score, index)` **по индексу**, а не по счёту: «топ-N»
  получается произвольным.
- `RandomWeightedStrategy.Default` объявлен как `MaxStrategy` (copy-paste).
- `RandomStrategy`/`RandomWeighted`/`TopRandom` берут `UnityEngine.Random` — поведение невоспроизводимо,
  трассу решений не сравнить.

Выводы для нас: **формулу кривой покрывать тестом на каждую кривую** (одна строка `[TestCase]` на точку —
дешевле, чем искать потом); **стратегия выбора получает рандом снаружи** (наш `AgentContext.Random`), а не
берёт статический.

---

## 3. GOAP: обратный A* по действиям

### 3.1 Данные

```csharp
public interface IAction
{
    string Name { get; }
    bool IsValid { get; }          // можно ли вообще брать это действие сейчас
    float Cost { get; }            // динамическая цена (делегат)
    LocalState Effects { get; }    // что станет правдой
    LocalState Conditions { get; } // что должно быть правдой
}
```

Факты — `Dictionary<string, bool>` (`WorldState`), условия и эффекты — `LocalState` (компактный список пар).
Действие можно объявить **без нового класса**:

```csharp
new BaseAction(
    name: "PickUpWeapon",
    conditions: new LocalState(("SeeWeapon", true), ("WeaponAtHand", true)),
    effects:    new LocalState(("HasWeapon", true)),
    isValid: () => !inventory.IsFull,
    cost:    () => 1f);
```

Цели — `IGoal { Name, IsValid, Priority, Effect }`; сенсоры — `ISensor.WriteState(WorldState)`, и **только
они** пишут факты. Агент — `partial class`, разрезанный по смыслу: `_Decide`, `_Goals`, `_Actions`,
`_Sensors`, `_WorldState`, `_Builder` (тот же приём, что у нашего `CharacterBody`).

### 3.2 Планировщик: от цели к миру

```csharp
// Вес узла = накопленная цена + эвристика (число несовпавших условий × шаг)
while (TryPeekAction(openList.Values, out Node node))
{
    if (worldState.Overlaps(node.action.Conditions))   // условия уже выполнены в мире — план готов
    {
        CreatePlan(node, plan);                        // идём по parent: получается порядок исполнения
        break;
    }

    VisitAction(node, worldState, actions, openList, closedList, depth);
    closedList.Add(node.action);
}
```

- Поиск **обратный**: стартуем от действий, чьи эффекты закрывают цель, и ищем, чем закрыть их условия, пока
  не упрёмся в факты мира. Поэтому `CreatePlan`, идя по `parent`, сразу даёт **порядок исполнения**.
- Эвристика — число несовпавших условий; **неизвестный ключ** (нет в мире) даёт `float.MaxValue / 2` и ветка
  отсекается (а не считается достижимой).
- Сосед: действие годится, если **каждое** условие закрыто либо его эффектами, либо уже миром.
- **Композитное действие** (`depth`, по умолчанию 3): если ни одно действие не закрывает набор из ≥2 условий,
  планировщик планирует каждое условие **отдельной подзадачей** и склеивает подпланы в один узел
  `CompositeAction` (цена — сумма, `Decompose()` раскрывает его в финальном плане). Это то, что обычные
  учебные GOAP не умеют — конъюнктивные цели.
- Пулы на всех структурах поиска: `DictionaryPool`, `HashSetPool`, `ListPool`.
- Ограничение: открытый список — словарь с **линейным поиском минимума** (`TryPeekAction`). Для 10–30
  действий это быстрее очереди с приоритетами, для сотен — нужна куча.

### 3.3 Как это протестировано

```csharp
[assembly: InternalsVisibleTo("Goap.Tests")]
```

Внутренние шаги планировщика (`VisitGoal`, `VisitAction`, `GetHeuristic`, `IsNeighbour`, `ComposeNeighbour`)
`internal` и покрыты отдельно: `AStarPlannerTests_PlanSucceed` (419 строк), `_PlanFailed` (207),
`_Heuristic`, `_PlanThrows`, `_Constructor`; плюс агент — `GoapAgentTests_Decide` (266), `_Goals`, `_Actions`,
`_WorldState`. **Приём, который стоит повторить:** планировщик — чистая логика, его шаги тестируются
по отдельности, а не только «на входе цель — на выходе план».

---

## 4. HTN: план как массив имён

```csharp
public interface ITask
{
    bool Decide(in WorldState simulation, in string[] plan, in int index, out int size);
}
```

Три узла домена:

| Узел | Семантика |
|---|---|
| `PrimitiveTask(operator, condition, effect)` | лист: если условие на симуляции выполнено — пишет **имя оператора** в план и применяет эффект к симуляции |
| `Method(condition, …sequence)` | последовательность: все подзадачи должны получиться, иначе метод не годится |
| `CompoundTask(condition, …methods)` | выбор: первый метод, который получился **на копии** состояния (`worldState.Clone()`), и побеждает |

Два приёма, которые делают это дешёвым:

1. **План — заранее выделенный буфер** `string[32]`, задачи пишут в него по индексу и возвращают `size`.
   Никаких списков и аллокаций на планирование.
2. **Эффекты применяются к симуляции**, а не к миру: домен «проигрывается в голове», мир не трогается.

Мир HTN — `Dictionary<string,int>` с типизированным доступом `Get<T>/TryGet<T> where T : unmanaged`, то есть
факты могут быть перечислениями. Отсюда фокус с дискретизацией геометрии:

```csharp
public enum DistanceType { Far = 0, Near = 1, At = 2 }

worldState.Set(nameof(EnemyDistance), GetDistance(entity, target, atDistance, nearDistance));
```

Непрерывная дистанция превращается в три символа — и символьный планировщик начинает работать с геометрией.
`MoveNearEnemy` имеет эффект `EnemyDistance = Near`, а `RangeCombat` — условие `EnemyDistance == Near`.

**Декоратор агента** — крючок для внешнего управления:

```csharp
public sealed class HTNAgentDecorator : IHTNAgent
{
    public void OverrideDomain(ITask domain) => _overridenDomain = domain;
    public void RollbackDomain() => _overridenDomain = null;
    // Decide идёт в переопределённый домен, если он есть
}
```

Отряд не командует движением — он **подменяет мозг** (см. §12).

---

## 5. Behaviour Tree: жизненный цикл узла

```csharp
public abstract class BehaviourNode : IBehaviourNode
{
    private bool _isRunning;

    public BehaviourResult Run(float deltaTime)
    {
        if (!_isRunning) { _isRunning = true; this.OnEnable(); }     // первый тик = вход

        BehaviourResult result = this.OnUpdate(deltaTime);

        if (result != BehaviourResult.RUNNING) { _isRunning = false; this.OnDisable(); }
        return result;
    }

    public void Abort()
    {
        if (!_isRunning) return;
        _isRunning = false;
        this.OnAbort();
        this.OnDisable();
    }
}
```

Это ответ на главную болячку самописных BT — «где сбрасывать состояние узла»: **`OnEnable` вызывается ровно
при входе, `OnDisable` — при любом выходе, включая прерывание**, а `OnAbort` отличает прерывание от
нормального завершения. Композиты этим пользуются: `Sequence`/`Selector` держат `_nodeIndex` между кадрами
(узел продолжается, а не начинается заново), сбрасывают его в `OnEnable` и в `OnAbort` прерывают **только
текущего ребёнка**.

```csharp
protected override BehaviourResult OnUpdate(float deltaTime)      // Sequence
{
    Start:
    if (_nodeIndex == _nodes.Count) return BehaviourResult.SUCCESS;

    BehaviourResult result = _nodes[_nodeIndex].Run(deltaTime);
    if (result != BehaviourResult.SUCCESS) return result;          // RUNNING или FAILURE наружу

    _nodeIndex++;
    goto Start;                                                    // за один тик проходит все мгновенные узлы
}
```

**Декоратор — один класс на все случаи**: делегаты `onEnable/onUpdate/onDisable/onAbort` плюс
`OverrideResult { None, Success, Failure, Running }`. Инвертор, «всегда успех», «всегда провал» и
логирование — это конфигурация, а не пять классов.

**Реактивное прерывание** (то, что в Behavior Designer зовут conditional abort) — 25 строк:

```csharp
private void TryAbort()
{
    bool currentState = _condition.Invoke();
    if (currentState == _lastState) return;     // реагируем на ИЗМЕНЕНИЕ условия, не на «истину»

    _lastState = currentState;
    _origin.Abort();
    _onAbort?.Invoke();
}
```

Тонкость, которую видно только в коде: аборт происходит на **любом фронте** (и `false→true`, и `true→false`),
а `_lastState` инициализирован `false`, поэтому условие, истинное на первом тике, сразу даст одно лишнее
прерывание. В нашей семантике (§4.2 спека) прерывание должно быть **направленным** — это как раз то решение,
которое граф AIKit описывает явно (`TransitionTrigger`), а не «по изменению любого бита».

---

## 6. FSM: машина, которая сама — состояние

```csharp
public interface IState
{
    void OnEnter();
    void OnUpdate(float deltaTime);
    void OnExit();
}

public class StateMachine<TKey> : IStateMachine<TKey>   // ← IStateMachine : IState
```

**Машина реализует `IState`** — и иерархическая FSM получается бесплатно: вложенная машина кладётся в
родительскую как обычное состояние, её `OnEnter/OnUpdate/OnExit` прокидываются в активное под-состояние.
Ключ — любой тип; вариант `IStateMachine : IStateMachine<Type>` + extension-методы даёт `ChangeState<PatrolState>()`
вообще без перечислений.

Вокруг этого:
- `BaseState(onEnter, onUpdate, onExit)` — состояние из делегатов, без класса;
- `CompositeState(params IState[])` — несколько состояний как одно (параллельные задачи внутри состояния —
  ровно наш «набор задач состояния» из [AIKit §4.2](AIKit.md));
- `DecoratorState` — обёртка с дополнительными хуками;
- `ChangeState(key, transition)` — между `OnExit` и `OnEnter` вызывается **действие перехода** (там удобно
  играть анимацию/звук перехода);
- события `OnStateChanged/OnStateAdded/OnStateRemoved` — на них вешается отладка и анимация.

`AutoStateMachine` добавляет переходы с приоритетом:

```csharp
public readonly struct StateTransition<TKey> : IComparable<StateTransition<TKey>>
{
    internal readonly TKey from, to;
    private readonly int priority;
    private readonly Func<bool> condition;
    private readonly Action action;

    public int CompareTo(StateTransition<TKey> other) => other.priority.CompareTo(this.priority);
    public bool Equals(StateTransition<TKey> other) => /* по from+to: дубль перехода невозможен */;
}

public override void OnUpdate(float deltaTime)
{
    this.UpdateTransitions();      // сначала переходы (список отсортирован по приоритету, первый годный)
    base.OnUpdate(deltaTime);      // потом тик активного состояния
}
```

Порядок «сначала переходы, потом тик» — то же решение, что у нас в `BehaviorGraphInstance` (рефлексы и
переходы до задач). Переход — `readonly struct` с равенством по `from+to`, поэтому добавить одно и то же
ребро дважды нельзя.

Баг оттуда, полезный как напоминание: в конструкторе `StateTransition(from, to, priority)` стоит
`this.priority = priority;` и сразу `this.priority = 0;` — приоритет молча теряется. Конструкторы тоже
нужно покрывать тестами.

### Состояния и условия — ассетами, а логика — в use-case

Из урока про FSM (`Lesson FSM.v.2`): ассет не *является* условием, а **создаёт** его:

```csharp
[Serializable]
public sealed class TargetDistanceGreatestConditionAsset : IStateConditionAsset
{
    [SerializeField] private string _distanceKey;

    public Func<bool> Create(object context)
    {
        var entity = (IEntity) context;
        return () => VectorUseCase.IsDistanceGreatest(entity, entity.GetTarget().Value,
                                                      entity.GetValue<IValue<float>>(_distanceKey).Value);
    }
}
```

**Ассет = фабрика делегата** (`Create(context) → Func<bool>` / `Action`), рантайм — замыкание. Инспектор
показывает список таких фабрик через `[SerializeReference]`, а в рантайме нет ни одного обращения к ассету.
Это прямой ответ на наш вопрос «как авторить узлы графа в инспекторе» ([AIKit §4.4](AIKit.md)).

И вторая половина того же урока — **логика в статических use-case функциях**, состояние — в сущности:

```csharp
public static class PatrolUseCase
{
    public static void PatrolWaypoints(in IEntity entity, in float stoppingDistance)
    {
        Transform[] waypoints = entity.GetWaypoints();
        IReactiveVariable<int> waypointIndex = entity.GetWaypointIndex();
        // … дошли до точки → index = (index + 1) % length; иначе → entity.GetMoveRequest().Invoke(dir)
    }
}
```

В материалах лежит и «до» (`PatrolStateOOP`, закомментирован): состояние само хранило массив точек и индекс.
После рефактора **узел/состояние — без данных**, данные — на сущности (их видно в инспекторе, они сохраняются,
их можно поменять снаружи), а логика — чистая функция. Для AIKit это правило «узел не хранит состояние
механики; состояние — в blackboard/агенте».

---

## 7. Главная идея: планировщик пишет план, дерево его исполняет

Самое ценное в разборе — то, как склеены планирование и исполнение. Планировщик (HTN) ничего не умеет
делать: его `PrimitiveTask` — **символ** с условием и эффектом.

```csharp
public static readonly ITask MoveNearEnemy = new PrimitiveTask(
    @operator: nameof(MoveNearEnemy),
    effect: ws => ws.Set(EnemyDistance, DistanceType.Near));

public static readonly ITask RangeCombat = new PrimitiveTask(
    @operator: nameof(RangeCombat),
    condition: ws => ws.Equals(EnemyDistance, DistanceType.Near) && ws.Equals(HasWeaponAmmo, true),
    effect: ws => ws.Set(EnemyAlive, false));
```

Планирование и исполнение — два поведения на сущности:

```csharp
// производитель: по запросу построить план и разослать его
public void OnFixedUpdate(in IEntity entity, in float deltaTime)
{
    if (!_planRequest.Consume()) return;
    if (!_agent.Decide(_currentPlan, out int size)) return;
    _planEvent.Invoke(_currentPlan, size);
}

// потребитель: собрать из плана дерево поведения
private void OnPlanChanged(string[] plan, int size)
{
    _behaviourTree.Clear();
    for (int i = 0; i < size; i++)
        _behaviourTree.Add(_nodes[plan[i]]);     // таблица «имя оператора → готовый BT-узел»
}
```

Таблица собирается в инсталлере один раз на сущность:

```csharp
var nodes = new Dictionary<string, IBehaviourNode>
{
    {nameof(MoveAtEnemy),   new MoveAtEnemyNode(entity)},
    {nameof(MoveNearEnemy), new MoveNearEnemyNode(entity)},
    {nameof(RangeCombat),   new RangeCombatNode(entity)},
    {nameof(Patrol),        new PatrolWaypointsNode(entity)},
    // … 20 узлов
};
```

А лист дерева переводит результат use-case в статус:

```csharp
protected override BehaviourResult OnUpdate(float deltaTime)
{
    IEntity target = _target.Value;
    if (target == null) return BehaviourResult.FAILURE;                     // нет цели — провал
    if (!target.Enabled || target.GetHealth().GetCurrent() <= 0)
        return BehaviourResult.SUCCESS;                                     // цель мертва — успех

    _entity.GetLookRequest().Invoke(direction);
    _entity.GetFireRequest().Invoke();
    return BehaviourResult.RUNNING;                                         // делаю
}
```

**Почему это важно для нас.** В спеке AIKit планировщики были «узлами графа» (`GoapGoal`, `HtnTask`). Этот
разбор показывает более чистый шов: планировщик — **источник плана**, граф — **исполнитель**, связка —
таблица «имя оператора → поддерево/задача». Тогда планировщик остаётся чистой логикой (тестируется без сцены),
а всё, что умеет тело, описано один раз в узлах.

Ещё один вывод: **архетип — это домен, а не код**. В материалах лежат рядом (частью закомментированы) домены
«снайпер», «ближний бой», «фланкёр» — над одним и тем же набором операторов и узлов.

---

## 8. Blackboard: int-ключи, типизированные словари, кодогенерация

```csharp
public interface IBlackboard
{
    bool HasTag(int key);                       // «тег» = ключ без значения (просто присутствие)
    bool TryGetFloat(int key, out float value);
    bool TryGetObject<T>(int key, out T value) where T : class;
    bool TryGetStruct<T>(int key, out Ref<T> value) where T : struct;
    ref T GetStruct<T>(int key) where T : struct;   // ← изменять структуру на месте
    void SetFloat(int key, float value);
    // … по словарю на тип: bool/int/float/float2/float3/quaternion/object/struct + множество тегов
}
```

Три приёма:

1. **Ключ — `int`, а не строка.** Константы генерируются из ассета-конфига вместе с типизированными
   расширениями, с шапкой «Code generation. Don't modify!» — ровно как наш `MasterBankAPI`. Плюс атрибут
   `[BlackboardKey]` с драйвером инспектора: поле ключа выбирается из списка, а не вводится руками.
2. **Словарь на каждый тип** — нет боксинга для `float`/`int`/`float2`, и в инспекторе видно каждую группу.
3. **`Ref<T>` для структур** — обёртка-ссылка, чтобы `GetStruct<T>` возвращал `ref` и структуру можно было
   менять по месту, не перезаписывая запись словаря.

Цена подхода видна в их же коде: `HasFloat(int key)` проверяет `float2Values`, а `DelQuaternion` удаляет из
`float3Values` — **два copy-paste-бага в руками написанной типизированной поверхности**. Вывод для AIKit:
такую поверхность либо генерировать, либо делать дженериком с типизированным ключом (`BlackboardKey<T>`), что
у нас и выбрано.

---

## 9. Поведения и расписание

```csharp
public interface IAIBehaviour { }                                     // маркер
public interface IAIEnable  : IAIBehaviour { void Enable(IBlackboard b); }
public interface IAIDisable : IAIBehaviour { void Disable(IBlackboard b); }
public interface IAIUpdate  : IAIBehaviour { void OnUpdate(IBlackboard b, float deltaTime); }
```

- **Сегрегация интерфейсов**: группа вызывает только то, что поведение реализует, и держит отдельный список
  `IAIUpdate` — проверки типа не попадают в горячий цикл. В `CompositeAIBehaviour` этот подсписок строится в
  `ISerializationCallbackReceiver.OnAfterDeserialize()` — то есть при загрузке, а не в тике.
- **Безопасная итерация**: `BehaviourGroup.OnUpdate` копирует список в кеш перед обходом, поэтому поведение
  может добавить/снять другое поведение прямо в тике.
- **Throttle — 20 строк и большой эффект:**

```csharp
public void OnUpdate(IBlackboard blackboard, float deltaTime)
{
    if (this.currentTime > 0) { this.currentTime -= deltaTime; return; }

    this.behaviour?.OnUpdate(blackboard, deltaTime);
    this.currentTime = Random.Range(this.minScanPeriod, this.maxScanPeriod);   // 0.2…0.3 с
}
```

Обёртка над любым поведением, период **случайный в диапазоне** — сенсоры всех агентов расходятся по кадрам
сами, без общего планировщика фаз. (Рандом здесь — `UnityEngine.Random`; у нас он должен приходить из сида
агента.)

- **Режимы тика** (`UpdateMode { UPDATE, FIXED_UPDATE, MANUAL }`) + MonoBehaviour-обёртка
  `SceneBehaviourGroup`, у которой `OnStart/OnUpdate(dt)/OnStop` выведены кнопками Odin в инспектор:
  систему можно шагать руками. Это независимое подтверждение нашего правила «модуль тикает сам, но имеет
  ручной режим» (CharacterKit, SpriteAnimator, AIKit).
- `[MovedFrom(true, "Modules.AI", …)]` на переименованных типах — переименование namespace без потери
  сериализованных ссылок. Практичная мелочь для модуля, который переносится между проектами.

---

## 9а. Поиск соседей: пространственный индекс вместо перебора

В материалах лежит рабочий пример того, как сенсор цели перестаёт быть O(N) — и обе версии стоят рядом в
одном файле, одна закомментирована:

```csharp
// O(N): перебрать всех врагов
// private IEntity FindClosestEnemy() => EntitiesUseCase.FindClosest(_enemyFilter, _position.Value);

// O(log N): спросить у пространственного дерева только тех, кто в радиусе
private IEntity FindClosestEnemy()
{
    SpatialTree<IEntity> spatialTree = GameContext.Instance.GetSpatialTree();
    IEntity[] buffer = s_arrayPool.Rent(BUFFER_SIZE);                       // ArrayPool, не new

    int count = spatialTree.FindAllInRadius(_position.Value, DETECTION_RADIUS, in buffer, this.IsEnemy);
    IEntity enemy = EntitiesUseCase.FindClosest(in buffer, in count, _position.Value);

    s_arrayPool.Return(buffer);
    return enemy;
}
```

Три детали, которые делают это дешёвым:

- **Агент сам себя регистрирует** в индексе (`Add` в `Enable`, `Remove` в `Disable`) и обновляет позицию
  **с гистерезисом**: `Update` вызывается только когда он сместился больше порога (у них 2 м), а не каждый
  кадр. Это ключевое — наивное «обновлять индекс на каждое движение» дороже перебора.
- **Периодическая полная пересборка** индекса отдельной системой (раз в секунду) — страховка от накопленного
  расхождения, если кто-то не снялся корректно.
- **Опрос — с периодом и фильтром**: `RandomCooldown` (случайный период, §9) + предикат «это враг» прямо в
  запросе радиуса, буфер маленький (4) и из пула.

Для нас: это ровно то, что нужно `AIKit.Perception` при десятках агентов и `AIKit.Crowd` для соседей
(`SpatialHash`): **индекс + гистерезис обновления + буферный запрос с предикатом**.

---

## 10. Навигация по сетке: один интерфейс, четыре алгоритма

```csharp
public interface IPathFinder
{
    NavVector[] FindPath(in NavVector start, in NavVector end, Predicate<NavCell> predicate = null);

    bool FindPath(in NavVector start, in NavVector end,
                  in NavVector[] path, out int pathSize, Predicate<NavCell> predicate = null);
}
```

Что здесь сделано правильно:

- **Два вида API**: заполнение буфера (`path`, `out pathSize`) — основной, без аллокаций; удобный
  (`NavVector[]`) — построен на нём и внутри берёт массив из `ArrayPool<NavVector>.Shared`.
- **Проходимость — `Predicate<NavCell>` в запросе, а не свойство сетки.** Одна сетка обслуживает летающих,
  плавающих и ходячих: `cell => cell.type != NavCellType.None` по умолчанию. Это ровно наш принцип
  «проходимость ребра считается от возможностей тела».
- **Дешёвые короткие замыкания в базовом классе** до запуска поиска: невалидный старт/финиш, `start == end`,
  «старт и финиш — соседи».
- **Эвристика — стратегия**: `Euclidean`, `Manhattan`, `Octile` (`max + 0.41·min`), `Base(func)`, со
  статическими `Instance`.
- `stackalloc NavVector[8]` под соседей, пулы `DictionaryPool`/`HashSetPool` под открытый и закрытый списки.
- `PathFinderFactory.Create(type, grid)` — переключение алгоритма из конфига: волна (BFS), Dijkstra, A*, JPS.

**JPS сделан подклассом A\*, который переопределяет только `GetNeighbours`:**

```csharp
public class JPSPathFinder : AStarPathFinder
{
    protected sealed override int GetNeighbours(in NavVector point, in NavVector end,
        in Predicate<NavCell> predicate, ref Span<NavVector> neighbours)
    {
        // вместо 8 соседних клеток — «точки прыжка»: сканируем по каждому из 8 направлений,
        // пока не встретим вынужденного соседа (препятствие рядом), конец или стену
    }
}
```

Тот же цикл A*, другая функция соседей → на порядок меньше раскрытых узлов на открытых картах. Отличная
демонстрация того, что **поиск пути — это цикл + функция соседей**: наш граф платформера с рёбрами
`Walk/Jump/Fall/Ladder` вставляется в этот же цикл как своя `GetNeighbours`.

(Их JPS — упрощённый: правила вынужденных соседей неполные, диагональ не раскрывает прямые сканы как
отдельные узлы, поэтому оптимальность пути не гарантирована. Как учебный пример — хорошо, как
продакшен-код — требует доработки.)

---

## 11. Карты влияния на Job System

```csharp
public partial struct InfluenceMap2D : IDisposable
{
    private NativeArray<float> _matrix;                 // row-major: [x * sizeY + y]
    public float this[int x, int y] { get => _matrix[x * _sizeY + y]; set => _matrix[x * _sizeY + y] = value; }
}
```

- **Структура над `NativeArray<float>`**, разрезанная на `partial`-файлы по операциям: `AddInfluence`,
  `Clear`, `Normalize`, `Inverse`, `MaxPoint`, `MaxRadiusPoint`, `MinSectorPoint`, `SectorPointsLower`,
  `MinMaxValue`, `CopyFrom/CopyTo`, `SetAll`, `Enumerator`, `Factory`, `ToString`. Тот же приём
  «partial-файл на операцию», что у нашего `CharacterBody`.
- **У каждой операции две версии**: обычная и `…AsJob(…, JobHandle depends)` на `IJobParallelFor` с
  `[BurstCompile]` и `[ReadOnly]`/`[WriteOnly]`. Кадр собирается в цепочку зависимостей
  (`Clear → AddInfluence×N → Normalize`) и синхронизируется один раз.
- **Затухание — делегат** `Propagation(maxValue, distance, maxDistance)`: `Linear`, `Polynomial(exponent)`.
  Но в Burst-джоб делегат не положить, поэтому формула дублируется статиком (`MathUtils.LinearPropagation`), и
  джоб называется `AddLinearCircleInfluenceAsJob` — **ограничение Burst прямо в API**. Полезный урок: либо
  параметризовать джоб числами, либо генерировать джоб на каждую кривую.
- **Шаблоны-штампы** (`AddTemplateInfluence(template, center, multiplier)`): маленькая заранее посчитанная
  карта «след угрозы» добавляется со смещением и множителем — дешевле, чем считать затухание для каждого
  агента (приём Дэйва Марка из «Modular Tactical Influence Maps»).
- **Запросы, из которых получается тактика**: `MaxPoint` (лучшая клетка), `MaxRadiusPoint(center, radius)`
  («куда идти рядом»), `MinSectorPoint(center, direction, angle, maxDistance)` («куда отходить в этом
  направлении»), `SectorPointsLower(threshold)`, `Inverse` (опасность = 1 − безопасность), `Normalize`.

Образцовый `InfluenceSystem` в материалах закомментирован — и правильно: он `FindObjectsOfType` каждые N
секунд и читает `Time.time`. У нас это: реестр источников влияния + тик от игрового цикла + `dt`.

---

## 12. Team AI: отряд как «матчмейкинг намерений»

Это третья модель координации — не централизованный координатор (как в
[Research_AI_Games §12.1](Research_AI_Games.md)) и не чистая эмерджентность, а **самосборка отряда вокруг
намерения**.

```csharp
public abstract class Intent<T>
{
    public int MinTeamSize { get; }             // ≥ 2
    public int MaxTeamSize { get; }
    public float PendingTime { get; }           // сколько ждём, пока наберётся команда

    public virtual Dictionary<string, object> CreateBlackboard() => new();      // общие данные отряда
    public virtual Predicate<ActiveTeam<T>> CreateCondition() => _ => true;     // когда отряд ещё жив
    public abstract List<IRole<T>> CreateRoles(IReadOnlyList<T> members);       // роли под текущий состав
    public abstract bool Equals(Intent<T> other);                               // «то же самое намерение»
}
```

Как собирается отряд:

1. Агент периодически (кулдаун 2 с) **заявляет намерение**, если у него сейчас нет роли:
   `teamManager.Join(entity, new AttackIntent(target))`.
2. `PendingTeamManager.Join` сначала пытается подсадить его в **активный** отряд с тем же намерением, потом в
   **ожидающий**, иначе открывает новый ожидающий отряд.
3. Ожидающий отряд набрался до `MaxTeamSize` → активируется сразу; истёк `PendingTime` с `≥ MinTeamSize`
   участниками → активируется; иначе — распускается.
4. `ActiveTeam` создаёт роли под состав, запускает их, тикает каждую, пока `role.IsActive(member, team)`;
   отряд живёт, пока участников ≥ минимума и выполняется условие намерения (цель жива и не ушла из радиуса).
5. Состав изменился (кто-то присоединился) → **роли пересоздаются под новый состав**.

Равенство намерений — по цели (`other is AttackIntent o && o._target.Equals(_target)`), поэтому все, кто
выбрал одну жертву, собираются в один отряд.

**Роль — это подмена мозга, а не приказ:**

```csharp
public abstract class AbstractRole : IRole<IEntity>
{
    public virtual void OnStart(IEntity member, ActiveTeam<IEntity> team)
    {
        member.GetRoleType().Value = this.GetRoleName();
        member.GetHTNAgent().OverrideDomain(this.GetDomain(member, team));   // ← роль = домен HTN
    }

    public virtual void OnStop(IEntity member, ActiveTeam<IEntity> team)
    {
        member.GetRoleType().Value = string.Empty;
        member.GetHTNAgent().RollbackDomain();
    }

    protected abstract ITask GetDomain(IEntity member, ActiveTeam<IEntity> team);
}
```

Три шипнутые роли:

| Роль | Домен (приоритет сверху вниз) |
|---|---|
| `AssaultRole` | подобрать оружие → (дальний бой на средней дистанции / ближний в упор) |
| `FlankerRole` | оружие → патроны → ближний бой, иначе **подойти с фланга** и стрелять → патруль |
| `SupportRole` | лечить раненого товарища → донести патроны → собрать аптечку/патроны → **отойти от врага** → патруль |

`SupportRole` добавляет и **свои сенсоры** на время роли (`InjuredBuddySensor`, `EmptyBuddySensor`), снимая их
в `OnStop`, и в `OnUpdate` пишет в участника выводы, которые видны только отряду («самый раненый товарищ»,
«у кого кончились патроны»). То есть роль = **домен + сенсоры + данные от отряда**.

Что здесь хрупко (и чего не повторять): роли и участники хранятся **двумя параллельными списками с
выравниванием по индексу** (`_roles[i]` относится к `_members[i]`), а `ActiveTeam.Update` удаляет элементы
прямо в прямом цикле `for (i++)` — следующий участник пропускается. Роль должна лежать **рядом с участником**
(словарь или структура), а удаление — обратным проходом или через буфер.

---

## 13. Тесты: что у них есть и чему учит то, чего нет

Три независимых набора: `Goap.Tests` (~1200 строк), `Navigation.Tests` (~1000), `UtilityAI.Tests` (~400),
`FSM.Tests` (~300), плюс симуляция HTN (`ShooterSimulation`).

Приёмы, которые стоит повторить:

- **Карта в тесте — литералом, как она выглядит глазами:**

```csharp
public static NavGrid CreateInverted(byte[,] invertedMatrix)   // строка 0 — ВЕРХ карты
{
    // 0 → NavCellType.None, иначе Ground; инверсия по Z, чтобы матрица в коде читалась как карта
}
// плюс именованные заготовки: Grid1x1, Grid3x3, GridГ (Г-образная), …
```

  Для нашего графа платформера это значит: геометрию тестов строить из ASCII/байтовой матрицы, а не руками
  из коллайдеров.
- **Имена тестов** `Класс_Ситуация_Ожидание`: `FindPath_StartEqualsEnd_ReturnsTrueOneZeroPathSize`,
  `FindPath_NoValidPath_ReturnsFalse` — то же соглашение, что у нас.
- **Конструкторы и исключения тоже тесты**: `Constructor_GridIsNull_ArgumentNullException`.
- **`InternalsVisibleTo` для шагов алгоритма** — эвристика, сосед, композиция подпланов проверяются
  по отдельности (§3.3).
- `[TestCase]` на математику с клэмпом: значение внутри диапазона, ниже минимума, выше максимума.

Чему учат **пробелы** в их тестах (каждый из найденных багов жив именно из-за пробела):

| Баг | Пробел в тестах | Правило для нас |
|---|---|---|
| `NormalCurve`: σ не влияет на ширину | покрыта только `LinearCurve` | тест на каждую кривую, по 3 точки |
| `TopRandomStrategy` сортирует по индексу | тест проверяет «вернул что-то из топа», но не порядок | в тесте стратегии задавать разные счёты и проверять **кого именно** выбрали |
| `StateTransition`: приоритет затёрт нулём | конструкторы перехода не покрыты | конструктор, который присваивает поля, — тест |
| `Blackboard.HasFloat` смотрит в `float2Values` | нет теста «записал float → `HasFloat` истинно» | на каждую пару Set/Has/Del — один тест |
| `ActiveTeam.Update` пропускает участника при удалении | нет теста «участник выпал из роли посреди обхода» | тест на удаление из коллекции **во время** обхода |

---

## 14. Что берём в AIKit

| Берём | Куда |
|---|---|
| `Score` как домен-тип с клэмпом и неявными приведениями | [AIKit §7.1](AIKit.md) |
| Consideration с правом **вето** (`bool Evaluate(out Score)`), а не «ноль как вето» | AIKit §7.1, [Research_AI §2.4](Research_AI.md) |
| Решение из **обязательных и необязательных** осей + сменная функция свёртки (Mul/Sum) | AIKit §7.1 |
| **Стратегия выбора** отдельным интерфейсом: Max / Random / RandomWeighted / TopRandom + порог отсечения | AIKit §7.1, Research_AI §2.4 |
| Полная библиотека кривых IAUS (linear, polynomial, logistic, logit, normal, sine, hyperbolic, const, custom) + декораторы invert/multiply | AIKit §7.1 |
| `stackalloc`-буфер счётов и `EvaluateScores()` только для отладки | AIKit §13 |
| GOAP: действие как **данные с делегатами** (`isValid`, `cost`), обратный A*, эвристика «число несовпавших условий», неизвестный ключ = отсечение | Research_AI §2.5 |
| GOAP: **композитное действие** для конъюнктивных целей с ограничением глубины | Research_AI §2.5 |
| HTN: `Primitive/Method/Compound`, план в **заранее выделенном буфере**, эффекты на копии состояния | Research_AI §2.6 |
| **Дискретизация геометрии в символы** (`DistanceType { Far, Near, At }`) для символьных планировщиков | Research_AI §2.6 |
| **Планировщик → план имён → таблица «имя → узел» → BT исполняет** | AIKit §4.1, §7, Research_AI §2.5–2.6 |
| BT: жизненный цикл узла `OnEnable/OnUpdate/OnDisable/OnAbort` вокруг `Run(dt)` | AIKit §4.4 |
| BT: композиты, продолжающиеся между кадрами (`_nodeIndex`), сброс в `OnEnable`, прерывание только текущего ребёнка | AIKit §4.2 |
| BT: **один декоратор** с делегатами и `OverrideResult` вместо пяти классов | AIKit §4.1 |
| FSM: машина сама — состояние (бесплатная иерархия), ключ-тип вместо enum, действие перехода между `OnExit` и `OnEnter` | AIKit §4.2 |
| FSM: `CompositeState` как «набор задач состояния», переходы `readonly struct` с приоритетом и равенством по `from+to` | AIKit §4.2 |
| **Ассет = фабрика делегата** (`Create(context) → Func<bool>/Action`) для авторинга узлов и условий | AIKit §4.4, §10 |
| **Узел без данных**: состояние механики — в blackboard/агенте, логика — в чистой функции | AIKit §1.4, §4.4 |
| Blackboard: типизированные ключи вместо строк, словарь на тип, `Ref<T>` для структур, кодогенерация API, «тег» как ключ без значения | AIKit §3.2 |
| Сегрегация интерфейсов поведений (`Enable/Disable/Update`) + кеш подсписка на десериализации | AIKit §3.1 |
| **Безопасная итерация**: копия списка перед обходом (поведение может снять поведение в тике) | AIKit §3.4 |
| **Throttle-обёртка** со случайным периодом сканирования — расфазировка сенсоров без общего планировщика | AIKit §3.4, §5 |
| Режимы тика (`Update/FixedUpdate/Manual`) + кнопки пошагового тика в инспекторе | AIKit §11 |
| Навигация: **буферное API** (`out pathSize`) как основное + `ArrayPool` в удобной обёртке | AIKit §6.2 |
| Навигация: **проходимость предикатом в запросе**, а не свойством сетки | AIKit §6.2 |
| Навигация: эвристика-стратегия (Euclidean/Manhattan/Octile), короткие замыкания до поиска, фабрика алгоритма | AIKit §6.2 |
| **JPS как подкласс A\* с другой функцией соседей** — и наш граф платформера туда же | Research_AI §5.2–5.3 |
| Карты влияния: `NativeArray` + partial-операции + `…AsJob(depends)`, шаблоны-штампы, запросы max-в-радиусе и min-в-секторе | AIKit §8, Research_AI §7.3 |
| Team AI: **намерение → ожидающий отряд → активный отряд**, равенство намерений по цели, таймаут сборки | Research_AI §7.1, AIKit §8 |
| Team AI: **роль = поддомен/подграф + свои сенсоры**, а не приказ в blackboard | AIKit §8 |
| Тесты: карта литералом «как видно глазами», `InternalsVisibleTo` для шагов алгоритма, тесты конструкторов | AIKit §12 |

| Пространственный индекс для поиска соседей: самостоятельная регистрация, **гистерезис обновления позиции**, периодическая пересборка, буферный запрос радиуса с предикатом | AIKit §5, §9 |
| И-ИЛИ-НЕ условия как `[SerializeReference]`-массивы с компактным инспектором (`InlineProperty`) | AIKit §4.1 |

**Не берём:** Odin в модуле (`[ShowInInspector]`, `[Button]`, `[FoldoutGroup]` — у нас только в AudioEngine),
`Unity.Mathematics`/`double` в API счёта (нам хватает `float`), фреймворк Atomic и его `IEntity`-сущности
(у нас компоненты по гайду), `goto` как управляющую конструкцию, `UnityEngine.Random` в логике,
`FindObjectsOfType` и `Time.time` в сенсорах, строковые ключи фактов в горячем пути, параллельные списки
с выравниванием по индексу, линейный поиск минимума в открытом списке на больших графах.

---

## Приложение: что прочитано, а что просмотрено

Прочитано построчно (~4700 строк): весь `Plugins/AI` проекта `UnityProject (2)` (UtilityAI, GOAP, HTN,
BehaviourTree, FSM, TeamAI), игровая обвязка ботов (HTN-домены, сенсоры, операторы, таблица узлов,
инсталлеры, роли отряда), модули `Navigation` и `Goap` из `Конец урока (3)`, `Plugns/AI` из `Конец урока`
(Blackboard, поведения, кодогенератор), модуль карт влияния целиком, урок по FSM
(состояния-ассеты, use-cases, сенсор цели), пространственный индекс из `UnityProject`, примеры тестов из всех
трёх наборов.

Просмотрено по списку файлов и выборочно, без построчного разбора (всё — повторы разобранных приёмов на другом
контенте): 20 BT-узлов шутера и 8 узлов бота-добытчика (`Конец Урока (2)`), GOAP-ветка `UnityProject (1)`
(старее разобранной версии планировщика), `Gizmos` и `SceneBlackboard` из `Конец урока`, тесты карт влияния
и FSM, `UnityProject.v.2` (совпадает с `UnityProject (2)` по AI-части), сторонние паки контента и сам
фреймворк Atomic (`Plugins/Atomic`) — он нам не нужен, из него взяты только две идеи (инсталлер как место
сборки ИИ и сгенерированные типизированные аксессоры вместо строк).

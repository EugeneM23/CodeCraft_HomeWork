# Справочник: как делать ИИ неигровых персонажей (Unity, C#)

Практический справочник, собранный из всего, что мы изучили: публикаций [Game AI Pro](http://www.gameaipro.com/)
(158 глав в свободном доступе), разборов 13 шипнутых проектов, архитектурного руководства живого проекта на
Unreal и кода учебного AI-фреймворка на Unity. Здесь **всё в одном месте и по задачам**: теория ровно
настолько, насколько нужна, потом код, потом правила «делай так / не делай так» с числами.

Объём: 52 раздела и 6 приложений, 91 пример кода, 43 таблицы.

**Как пользоваться.** Читать целиком не нужно: §0.3 — указатель «мне нужно сделать X → раздел Y». Каждый
раздел самодостаточен: теория → код → правила → грабли. Код — рабочие заготовки на C#, не псевдокод; имена и
комментарии английские (правило проекта), пояснения русские.

**Документ самодостаточен.** Разборы шипнутых проектов (приложение A), каталог ролей и жанры (B), практика
Unity (C), обзор рынка и открытого кода (D), первоисточники со ссылками (E) и глоссарий RU→EN (F) — внутри.
Наружу ведут только три ссылки, и все три — про **наш модуль**: [AI](AI.md) (свод принципов и состояние дел),
[AIKit](AIKit.md) (спек: сборки, API, решения), [AIKit_Plan](AIKit_Plan.md) (план работ). Исследовательская
серия (`Research_AI*`) остаётся как архив с полными ссылками и историей разборов; если она расходится со
справочником — прав справочник, а расхождение надо выправить.

---

## Содержание

**Часть 0. С чего начать**
0.1 Пять слоёв ИИ · 0.2 Как выбрать модель решений · 0.3 Указатель «мне нужно…» ·
0.4 Чего здесь нет и почему

**Часть I. Модели решений**
1. Конечный автомат (FSM/HFSM) · 2. Дерево поведения (BT) · 3. Граф состояний и задач (StateTree) ·
4. Utility AI и IAUS · 5. GOAP · 6. HTN · 7. Шов «планировщик → план → исполнитель»

**Часть II. Данные и восприятие**
8. Blackboard · 9. Восприятие · **9б. Стелс: детекция и тревога** · 10. Сигналы и барки

**Часть III. Движение**
11. Три уровня навигации · 12. Графы и алгоритмы поиска пути · 13. 2D-платформер ·
**13а. Дизайн уровня под ИИ** · 14. Steering · 15. Арбитр движения

**Часть IV. Бой**
16. Умение как данные · 17. Выбор умения · 18. Честность боя: жетоны и слоты ·
**18а. Укрытия и подавление** · 19. Реакции и защита · **20. Анимация, эффекты, звук** ·
**20а. Формации и строй** · **20б. Боссы: фазы, паттерны, арены**

**Часть V. Группы и мир**
21. Координатор · 22. Намерения и самосборка отряда · 23. Карты влияния и выбор позиции ·
24. Объекты уровня · 25. Директор и сложность · **25а. Энкаунтеры: спавнеры, волны, триггеры** ·
**25б. Правила аггро и привязка к дому**

**Часть VI. Толпа**
26. Толпа как отдельная подсистема · **26а. Транспорт, летающие, водные**

**Часть VII. Производительность**
27. Расписание и LOD · 28. Аллокации и API · 29. Детерминизм ·
**29а. Сохранение состояния ИИ** · 30. Профилирование · **30а. Джобы и многопоточность**

**Часть VIII. Инструменты, авторинг, отладка**
31. Редакторы графов в Unity · 32. Авторинг для дизайнера · 33. Отладка

**Часть IX. Тестирование**
34. Три яруса тестов · 35. Стенды и приёмы · 36. Что покрывать обязательно

**Часть X. Чеклисты и грабли**
37. Чеклисты · 38. Сорок грабель

**Приложения**
A. Разборы шипнутых проектов (13 кейсов) · B. Роли врагов и жанры · C. Практика Unity ·
D. Готовые решения и build vs buy · E. Источники · F. Глоссарий RU → EN

---

# Часть 0. С чего начать

## 0.1 Пять слоёв ИИ

Любой вменяемый игровой ИИ разложен на одни и те же пять слоёв. Смешение слоёв — причина 90% неподдерживаемого
кода ИИ.

```
СЕНСОРЫ ──► ФАКТЫ ──► РЕШЕНИЕ ──► ИСПОЛНЕНИЕ ──► НАВИГАЦИЯ/STEERING ──► АКТУАТОР ──► ТЕЛО
(что я вижу) (blackboard) (что делать) (как делать)  (как дойти)          («геймпад»)  (физика, анимация)
```

| Слой | Отвечает за | НЕ отвечает за | Типичная ошибка |
|---|---|---|---|
| Сенсоры | «вижу врага в 8 м, слышал шум слева» | что с этим делать | решение прямо в сенсоре («увидел → побежал») |
| Факты | единое хранилище состояния агента | логику | узлы читают компоненты игры напрямую, минуя факты |
| Решение | «сейчас догонять» | как именно двигаться | в состоянии `Chase` зашита формула прыжка |
| Исполнение | «идти к точке, ударить» | выбор цели и приоритетов | задача сама решает, что делать дальше |
| Навигация | путь и локальное ведение | нажатия и физику | навигатор дёргает `transform.position` |
| Актуатор | перевод намерений в вызовы тела | принятие решений | ИИ пишет скорость в `Rigidbody` из десяти мест |

**Правило границы.** Узел решения не знает слова `Rigidbody`. Он говорит «двигайся туда», «повернись»,
«примени умение `SwordCombo`». Единственная точка связи с телом — актуатор, он же «виртуальный геймпад»:

```csharp
public interface IAgentActuator
{
    void Move(Vector3 direction, float speedScale = 1f);
    void Face(Vector3 direction);
    void Jump(float holdTime);           // «подержать кнопку» = высота прыжка
    void Crouch(bool crouch);
    void Dash(Vector3 direction);
    void Stop();
    void Apply(float dt);                // сбросить накопленные «нажатия» в тело

    AgentBodyInfo Body { get; }           // IsGrounded, Velocity, Position, Facing, JumpHeight, JumpReach
}
```

Почему именно так: тело меняется (2D-контроллер, `NavMeshAgent`, транспорт, сетевой прокси), ИИ не меняется.
И обратная выгода: ИИ подаёт те же «нажатия», что игрок, — значит всё, что умеет игрок, автоматически умеет
и ИИ, и тестировать ИИ можно, подавая нажатия вручную.

## 0.2 Как выбрать модель решений

| Модель | Решает вопрос | Сильна | Слаба | Брать, когда |
|---|---|---|---|---|
| FSM | «в каком я режиме» | предсказуемость, скорость, тривиальная отладка | N² переходов, не масштабируется | ≤ 6–8 режимов, подрежимы внутри состояния |
| HFSM | то же + иерархия | знакомо, дёшево | всё равно руками | 2D-враги, боссы по фазам |
| BT | «что попробовать по приоритету» | модульность, переиспользование поддеревьев | реактивность, хранение состояния | исполнение планов, последовательности действий |
| Граф состояний и задач (StateTree) | «состояние + что в нём делаю + когда ухожу» | нет спагетти, выбор по требованию | нужен редактор | каркас современного ИИ |
| Utility/IAUS | «что лучше из многого» | ранжирование, плавная деградация | «почему так?», тюнинг чисел | выбор умения, цели, позиции |
| GOAP | «как достичь цели из текущего мира» | эмерджентность, не надо писать сценарии | взрыв вариантов, отладка, инерция | сим-агенты, выживание, крафт |
| HTN | «разложи задачу на подзадачи» | авторская структура + реальный поиск | нет готовых инструментов | многошаговые миссии, роли-«сценарии» |

**Дерево выбора.**

1. Поведение описывается 4–8 режимами, переходы очевидны? → **FSM/HFSM**, и не усложняй.
2. Нужны длинные последовательности («подойти → открыть → войти → зачистить») с переиспользованием? → **BT**
   как исполнитель.
3. Есть и режимы, и последовательности, и бой, и реакции? → **граф состояний и задач** как каркас (состояния
   дают режимы, задачи внутри — последовательности).
4. Внутри режима надо выбрать одно из многого (какое умение, какую цель, какую позицию)? → **utility-селектор**
   внутри каркаса, а не вместо него.
5. Агент должен сам придумывать цепочки, которых не писал дизайнер? → **планировщик** (GOAP/HTN) **как
   источник плана**, исполняет план каркас (§7). Если такой потребности нет — планировщик не нужен: он дорог
   в отладке.

**Практический консенсус индустрии:** каркас — граф состояний и задач; utility — для «выбери лучшее»;
планировщик — точечно. Так устроены Unreal StateTree, Unity Behavior и почти все шипнутые проекты, которые
прошли через чистый BT и отказались от него.

## 0.3 Указатель «мне нужно…»

| Задача | Раздел |
|---|---|
| враг патрулирует, замечает, догоняет, бьёт | §1, §3, §9, §16 |
| враг не должен застревать в геометрии | §11, §13 |
| враг в 2D-платформере должен прыгать по платформам | §13 |
| выбрать умение из десяти | §4, §17 |
| «они все набрасываются одновременно» | §18, §21 |
| «он бьёт как робот, читает мои нажатия» | §19 |
| враги должны разойтись по ролям | §21, §22 |
| враг должен занимать укрытие / турель / станцию | §24 |
| выбрать точку: видно цель, не видно второго врага | §23 |
| 500 юнитов идут к одной точке | §26 |
| ИИ съедает 8 мс на кадр | §27, §28 |
| «иногда он ведёт себя странно, воспроизвести не могу» | §29, §33 |
| дизайнер должен настраивать врагов без программиста | §32 |
| как это всё тестировать | §34–§36 |
| сделать новый тип врага | §37.1 |
| добавить новую механику в ИИ | §37.2 |
| стелс: тревога, индикатор, «почему меня заметили» | §9б |
| враг должен прятаться за укрытиями и подавлять | §18а |
| группа должна идти строем / напарники за игроком | §20а |
| босс с фазами и паттернами | §20б |
| враг как в соулс-лайке: выбор приёма, комбо, реакции | §20б.4 |
| уровень, на котором ИИ не выглядит тупым | §13а |
| собрать аниматор под ИИ, связать удар с кадром клипа | §20.1–§20.3 |
| эффекты, звуки, шаги на действия персонажа | §20.4 |
| спавнеры, волны, двери, триггеры энкаунтера | §25а |
| «вся карта побежала на выстрел» | §25б |
| враг в транспорте, летающий, на корабле | §26а |
| загрузился из сейва — враг стоит столбом | §29а |
| вынести ИИ в джобы | §30а |
| как это делали в шипнутых играх | приложение A |
| что требуется от ИИ в моём жанре | приложение B |
| грабли именно Unity | приложение C |
| взять готовое или писать своё | приложение D |
| первоисточник по теме | приложение E |
| как это называется по-английски | приложение F |

---

## 0.4 Чего в справочнике нет и почему

Чтобы не искать зря:

| Тема | Почему не здесь |
|---|---|
| **Машинное обучение** (ML-Agents, RL, имитация) | другой инструмент и другой процесс: нет гарантии поведения, нет авторинга дизайнером, отладка через переобучение. В играх применяется точечно (боты для тестирования, анимация) — если понадобится, это отдельное исследование |
| **Диалоги, квесты, нарратив** | смежная область со своими системами (деревья диалогов, квест-графы). Пересечение одно — **барки** (§10), они здесь |
| **Сетевой ИИ** | ИИ считается на сервере/хосте, клиент видит результат. Правило простое: ИИ живёт **только на авторитете**, клиенту репликация состояния и анимаций; всё остальное — тема сетевого слоя, не ИИ |
| **ECS/DOTS-рантайм** | ускорение, а не архитектура. Сначала правильная модель (§26), потом джобы (§30а) и только потом ECS, если упёрлись |
| **Поиск по ходам** (минимакс, MCTS) | для пошаговых игр; с реального времени не переносится. Полезное оттуда — utility-оценка и запросы позиций, они здесь |
| **Гонки как жанр** | специфическая система координат (трасса, а не мир) и PID-регуляторы. Главный переносимый вывод — в §13а: **выбор системы координат важнее алгоритма** |
| **Анимация как система** | ИК, motion matching, тонкости блендинга — дело аниматора. Здесь шов и минимальная сборка аниматора под ИИ, эффекты и звук: §20 |

---

# Часть I. Модели решений

## 1. Конечный автомат (FSM/HFSM)

### 1.1 Минимальная рабочая реализация

```csharp
public interface IState
{
    void OnEnter();
    void OnUpdate(float deltaTime);
    void OnExit();
}

public class StateMachine<TKey> : IState          // ← машина сама является состоянием
{
    private readonly Dictionary<TKey, IState> _states = new();
    private KeyValuePair<TKey, IState> _current;

    public event Action<TKey> StateChanged;
    public TKey CurrentState => _current.Key;

    public void AddState(TKey key, IState state) => _states.Add(key, state);

    public void ChangeState(TKey key, Action transition = null)
    {
        _current.Value?.OnExit();
        transition?.Invoke();                      // звук/анимация перехода — здесь
        _current = new KeyValuePair<TKey, IState>(key, _states[key]);
        _current.Value?.OnEnter();
        StateChanged?.Invoke(key);
    }

    public bool TryChangeState(TKey key, Action transition = null)
    {
        if (EqualityComparer<TKey>.Default.Equals(_current.Key, key))
            return false;                          // переход в себя — не переход
        ChangeState(key, transition);
        return true;
    }

    public virtual void OnEnter() => _current.Value?.OnEnter();
    public virtual void OnUpdate(float dt) => _current.Value?.OnUpdate(dt);
    public virtual void OnExit() => _current.Value?.OnExit();
}
```

**Главный приём: машина реализует `IState`.** Тогда иерархия (HFSM) получается бесплатно — вложенная машина
кладётся в родительскую как обычное состояние, и `OnEnter/OnUpdate/OnExit` прокидываются вниз по активной
ветке. Никакого отдельного «HFSM-движка» писать не нужно.

### 1.2 Переходы данными, а не кодом

Если переходы раскиданы по `OnUpdate` состояний, автомат превращается в спагетти. Переход — **объект** с
приоритетом, условием и действием:

```csharp
public readonly struct StateTransition<TKey> : IComparable<StateTransition<TKey>>
{
    public readonly TKey From, To;
    private readonly int _priority;
    private readonly Func<bool> _condition;
    private readonly Action _action;

    public bool CanPerform() => _condition == null || _condition.Invoke();
    public void Perform() => _action?.Invoke();

    // сортировка по убыванию приоритета
    public int CompareTo(StateTransition<TKey> other) => other._priority.CompareTo(_priority);

    // равенство по (From, To): одно и то же ребро нельзя добавить дважды
    public bool Equals(StateTransition<TKey> other) =>
        EqualityComparer<TKey>.Default.Equals(From, other.From) &&
        EqualityComparer<TKey>.Default.Equals(To, other.To);
}

public class AutoStateMachine<TKey> : StateMachine<TKey>
{
    private readonly List<StateTransition<TKey>> _transitions = new();   // отсортирован по приоритету

    public override void OnUpdate(float deltaTime)
    {
        UpdateTransitions();          // 1) сначала переходы
        base.OnUpdate(deltaTime);     // 2) потом тик активного состояния
    }

    private void UpdateTransitions()
    {
        for (int i = 0; i < _transitions.Count; i++)
        {
            StateTransition<TKey> t = _transitions[i];
            if (!EqualityComparer<TKey>.Default.Equals(t.From, CurrentState)) continue;
            if (!t.CanPerform()) continue;

            ChangeState(t.To, t.Perform);
            break;                    // первый подходящий по приоритету — и выходим
        }
    }
}
```

**Правила:**

- **Переходы проверяются до тика состояния**, иначе состояние успевает сделать шаг в мире, из которого его
  уже выгоняют.
- **Сортировка по приоритету один раз при добавлении**, а не каждый кадр.
- **Равенство ребра по (from, to)** — защита от дублей, которые дают «мигающие» переходы.
- **Переходы «из любого состояния»** (получил урон, умер, командир отозвал) — отдельный список, который
  проверяется раньше обычных. Без него в FSM на 8 состояний появляется 8 копий одного ребра.
- Минимальное время в состоянии (`MinStateTime`) — лекарство от дребезга на границе условия; гистерезис
  порогов лучше, но дороже в авторинге.

### 1.3 Состояние без собственных данных

Классическая ошибка — состояние хранит данные механики:

```csharp
// ПЛОХО: индекс маршрута живёт в состоянии
public sealed class PatrolState : IState
{
    private readonly Transform[] _waypoints;
    private int _index;                      // ← потерялось при выходе из состояния,
                                             //    не видно в инспекторе, не сохраняется
    public void OnUpdate(float dt) { /* … */ }
}
```

```csharp
// ХОРОШО: данные — на агенте/в blackboard, логика — чистая функция
public static class PatrolUseCase
{
    public static void PatrolWaypoints(Agent agent, float stoppingDistance)
    {
        Transform[] waypoints = agent.Waypoints;
        int index = agent.Blackboard.Get(Keys.WaypointIndex);

        Vector3 to = waypoints[index].position - agent.Position;
        to.y = 0f;

        if (to.sqrMagnitude <= stoppingDistance * stoppingDistance)
            agent.Blackboard.Set(Keys.WaypointIndex, (index + 1) % waypoints.Length);
        else
            agent.Actuator.Move(to.normalized);
    }
}

public sealed class PatrolState : IState
{
    private readonly Agent _agent;
    public void OnUpdate(float dt) => PatrolUseCase.PatrolWaypoints(_agent, _agent.StopDistance);
}
```

Что это даёт: состояние переиспользуется и тестируется как функция, прогресс маршрута виден в инспекторе,
попадает в сейв и доступен другим узлам (например, «вернись на маршрут после боя»).

### 1.4 Когда FSM перестаёт справляться

Признаки, по которым пора переходить на граф состояний и задач (§3):

- переходов больше, чем состояний, в 3+ раза;
- появились «состояния-флаги» (`ChaseWithLowHp`, `ChaseWithoutAmmo`);
- одно и то же действие скопировано в три состояния;
- в состоянии больше одного параллельного дела («идти и стрелять и следить за кулдауном»).

---

## 2. Дерево поведения (BT)

### 2.1 Контракт и жизненный цикл узла

```csharp
public enum BehaviourResult { Running = 0, Success = 1, Failure = 2 }

public interface IBehaviourNode
{
    bool IsRunning { get; }
    BehaviourResult Run(float deltaTime);
    void Abort();
}

public abstract class BehaviourNode : IBehaviourNode
{
    private bool _isRunning;
    public bool IsRunning => _isRunning;

    public BehaviourResult Run(float deltaTime)
    {
        if (!_isRunning) { _isRunning = true; OnEnable(); }      // первый тик = вход

        BehaviourResult result = OnUpdate(deltaTime);

        if (result != BehaviourResult.Running) { _isRunning = false; OnDisable(); }
        return result;
    }

    public void Abort()
    {
        if (!_isRunning) return;
        _isRunning = false;
        OnAbort();          // отличает прерывание от нормального завершения
        OnDisable();        // освобождение ресурсов — в одном месте
    }

    protected abstract BehaviourResult OnUpdate(float deltaTime);
    protected virtual void OnEnable() { }
    protected virtual void OnDisable() { }
    protected virtual void OnAbort() { }
}
```

**Это самая важная часть BT.** Жизненный цикл зашит в базовый класс, а не в каждый узел, поэтому:

- сброс состояния узла живёт **ровно в одном месте** — в `OnEnable`;
- освобождение (отменить путь, снять бронь, опустить оружие) — в `OnDisable`, и оно вызывается при любом
  исходе, включая прерывание;
- `OnAbort` даёт узлу шанс отреагировать именно на прерывание.

Без этого контракта самописные BT обрастают багами «узел не сбросился при повторном входе» и «после аборта
агент остался с поднятым оружием».

### 2.2 Композиты, которые продолжаются между кадрами

```csharp
public sealed class SequenceNode : CompositeNode        // «все подряд, до первого провала»
{
    private int _index;

    protected override void OnEnable() => _index = 0;                   // вход = сброс
    protected override void OnAbort() => _nodes[_index].Abort();        // прерываем только текущего

    protected override BehaviourResult OnUpdate(float deltaTime)
    {
        while (_index < _nodes.Count)
        {
            BehaviourResult result = _nodes[_index].Run(deltaTime);
            if (result != BehaviourResult.Success)
                return result;                                          // Running или Failure — наружу

            _index++;                                                   // мгновенные узлы проходим
        }                                                               // за один тик
        return BehaviourResult.Success;
    }
}

public sealed class SelectorNode : CompositeNode         // «первый, кто смог»
{
    private int _index;

    protected override void OnEnable() => _index = 0;
    protected override void OnAbort() => _nodes[_index].Abort();

    protected override BehaviourResult OnUpdate(float deltaTime)
    {
        while (_index < _nodes.Count)
        {
            BehaviourResult result = _nodes[_index].Run(deltaTime);
            if (result != BehaviourResult.Failure)
                return result;

            _index++;
        }
        return BehaviourResult.Failure;
    }
}
```

Индекс хранится между кадрами — поведение **продолжается**, а не начинается заново каждый тик. Это
«память» композита; без неё `Sequence` из трёх длинных действий никогда не дойдёт до второго.

### 2.3 Один декоратор вместо пяти классов

```csharp
public sealed class DecoratorNode : BehaviourNode
{
    public enum Override { None = 0, Success = 1, Failure = 2, Running = 3 }

    private readonly IBehaviourNode _origin;
    private readonly Override _override;
    private readonly Action _onEnable, _onDisable, _onAbort;
    private readonly Action<float> _onUpdate;

    protected override BehaviourResult OnUpdate(float deltaTime)
    {
        _onUpdate?.Invoke(deltaTime);
        BehaviourResult result = _origin.Run(deltaTime);

        return _override switch
        {
            Override.None => result,
            Override.Success => BehaviourResult.Success,     // «всегда успех»
            Override.Failure => BehaviourResult.Failure,
            Override.Running => BehaviourResult.Running,     // «не отпускай управление»
            _ => result
        };
    }

    protected override void OnEnable() => _onEnable?.Invoke();
    protected override void OnDisable() => _onDisable?.Invoke();
    protected override void OnAbort() { _onAbort?.Invoke(); _origin.Abort(); }
}
```

Инвертор, «всегда успех», «всегда провал», логирование, звук на входе — это **конфигурация одного класса**, а
не пять классов в библиотеке узлов.

### 2.4 Реактивность: главная болячка BT

Наивное дерево доигрывает начатую ветку и не замечает, что мир изменился. Два решения, и нужны оба:

**1. Прерыватель по условию** (в Unreal — `observer aborts`, в Behavior Designer — `conditional abort`):

```csharp
public sealed class AborterNode : BehaviourNode
{
    private readonly IBehaviourNode _origin;
    private readonly Func<bool> _condition;
    private bool _last;

    protected override BehaviourResult OnUpdate(float deltaTime)
    {
        bool current = _condition.Invoke();
        if (current != _last)              // реагируем на ИЗМЕНЕНИЕ, а не на истинность
        {
            _last = current;
            _origin.Abort();
        }
        return _origin.Run(deltaTime);
    }
}
```

**Осторожно:** в таком виде прерывание срабатывает на **любом** фронте (и `false→true`, и обратно), а
`_last == false` на старте означает, что уже истинное условие даст лишний аборт на первом тике. Правильнее
задавать направление явно: `AbortWhen.BecameTrue` / `BecameFalse` / `Changed`.

**2. Реактивные композиты** (`ReactiveSequence`): перепроверяют условия ранних детей каждый тик и сбрасывают
младшие ветки.

**Правило, которое снимает весь класс проблем:** перепроверка условий и переходов — **один раз за think-тик, в
фиксированном порядке** (рефлексы → глобальные → от активного листа к корню), а не «когда сработает
наблюдатель где-то в дереве». Жалобы на Unreal BT («декоратор считается вне активного пути», «ключ изменился,
а аборт не произошёл», «задача не прерывается без явного `FinishAbort`») — ровно про отсутствие этого правила.

### 2.5 Правила BT

- Статусы ровно три: `Running/Success/Failure`. Четвёртого не бывает; «не смог начать» — это `Failure`.
- `Running` означает «я держу управление» — родитель не имеет права идти дальше.
- Лист переводит результат механики в статус по одной схеме:
  `нет цели → Failure`, `цель достигнута → Success`, `иду → Running`, `цель мертва → Success`.
- `deltaTime` течёт сквозь дерево параметром — никаких `Time.deltaTime` внутри узлов.
- Дерево не хранит игровое состояние: индексы и таймеры — в узле (локальные, сбрасываются во входе), данные
  механики — в blackboard.
- Глубина дерева в реальных проектах ≤ 6–7; глубже — выносить в поддерево с осмысленным именем.

---

## 3. Граф состояний и задач (StateTree)

Гибрид, к которому пришла индустрия: состояния дают режимы (как FSM), задачи внутри состояния дают
последовательности (как BT), переходы с областями прерывания дают реактивность.

### 3.1 Структура

```
ROOT  (выбор по условиям входа, проверка от активного листа к корню)
├── Patrol        [enter: !HasTarget]            tasks: Patrol(points), FaceMoveDirection
│    └── goto Combat  when Sees(target)          scope: Branch
├── Combat        [enter: HasTarget]
│    ├── Approach [enter: Distance > AttackRange] tasks: MoveToTarget(stopAt: AttackRange)
│    ├── Strike   [enter: Distance <= AttackRange, CanAttack]
│    │              tasks: FaceTarget, UseAbility(best)
│    │    └── goto Recover  when TaskFinished
│    └── Recover  tasks: Wait(recovery), KeepDistance(min, max)
│         └── goto Approach  when Cooldown.Ready
└── Hurt          [reflex]  tasks: PlayHurt, LockControl(hurtTime)   scope: Global
```

Четыре понятия и всё:

| Понятие | Что это |
|---|---|
| **Состояние** | режим + набор задач, которые работают **параллельно**, пока состояние активно |
| **Задача** | исполняемое действие со статусом `Running/Success/Failure` и хуками `OnEnter/OnTick/OnExit(reason)` |
| **Переход** | ребро с условием, приоритетом и **областью прерывания** |
| **Поток** | композиты внутри задач: `Sequence`, `Selector`, `Parallel`, `Random`, `Cooldown`, `Repeat`, `TimeOut` |

### 3.2 Семантика, которая делает граф предсказуемым

1. **Выбор состояния — по требованию, а не каждый кадр.** Переходы проверяются от активного листа к корню;
   первый сработавший запускает выбор с его цели вниз, условия входа отбрасывают непригодные ветки. Это
   решает проблему «BT пересчитывает всё дерево каждый тик».
2. **Один think-тик = одна перепроверка**, в фиксированном порядке: рефлексы → глобальные переходы → от листа
   к корню. Никаких наблюдателей, срабатывающих вне активного пути.
3. **Область прерывания — у ребра, а не у декоратора:**
   `Self` (перезапустить состояние), `Branch` (выйти из поддерева), `Global` (прервать что угодно — урон,
   смерть, приказ). Область видна на графе цветом.
4. **Задачи состояния работают параллельно** и получают `OnExit(reason)`, где `reason` отличает «сам
   завершился» от «прервали».
5. **Детерминизм:** порядок обхода стабилен, случайность — из сида агента, время — параметром `dt`.
6. **Условия двухуровневые:** дешёвый гейт (флаг, квадрат дистанции) → дорогой тест (линия взгляда, запрос
   пути). Движок не зовёт дорогой тест, если гейт не прошёл. Запросы к миру кешируются на один think-тик.

### 3.3 Узел как класс

```csharp
[Serializable, AgentNode("Movement/Move To", Category = NodeCategory.Task)]
public sealed class MoveToTask : AgentTask
{
    public BlackboardValue<Vector3> Destination;      // «константа или переменная» переключателем
    [Min(0f)] public float StopDistance = 0.5f;

    protected override void OnEnter(AgentContext context) =>
        context.Navigation.SetDestination(Destination.Get(context));

    protected override TaskStatus OnTick(AgentContext context, float dt)
    {
        if (context.Navigation.RemainingDistance <= StopDistance) return TaskStatus.Success;
        if (!context.Navigation.HasPath) return TaskStatus.Failure;

        context.Navigation.Step(dt);
        return TaskStatus.Running;
    }

    protected override void OnExit(AgentContext context, ExitReason reason) =>
        context.Navigation.Clear();
}
```

Требования, которые этим закрываются: узел появляется в меню редактора сам (атрибут + рефлексия один раз),
сериализуется через `[SerializeReference]`, тестируется как обычный класс
(`new MoveToTask().OnTick(fakeContext, 0.02f)`).

Для мелочи (условие перехода, действие на входе) полноценный узел — перебор. Там дешевле **ассет-фабрика
делегата**: сериализуемый класс не *является* условием, а **создаёт** его один раз на агента, и в рантайме
работает замыкание:

```csharp
public interface IAgentConditionAsset { Func<bool> Create(AgentContext context); }

[Serializable]
public sealed class TargetFartherThanAsset : IAgentConditionAsset
{
    [SerializeField] private float _distance;

    public Func<bool> Create(AgentContext context)
    {
        var body = context.Body;
        var target = context.Blackboard.Slot<Transform>(Keys.Target);
        float sqr = _distance * _distance;

        return () => target.Value != null &&
                     (target.Value.position - body.Position).sqrMagnitude > sqr;
    }
}
```

### 3.4 Правила графа

- Состояние без задач — нормально (это «режим-метка», на нём висят переходы).
- Задача, которая никогда не возвращает `Success`, — тоже нормально (`Patrol`, `KeepDistance`): состояние
  покидают переходом.
- Рефлексы (`Global`) — максимум 3–5 на граф: урон, смерть, приказ, «цель исчезла». Больше — признак, что
  условия надо перенести в условия входа состояний.
- Одно состояние = один режим поведения. «Состояние на каждое действие» — это BT, вывернутое наизнанку.
- Подграфы (`Subgraph`) — для переиспользования («бой мечом» у трёх врагов); «способности» (`Capability`) —
  включаемые блоки, которые архетип может выключить.
---

## 4. Utility AI и IAUS

Модель «оценить все варианты и взять лучший». Нужна там, где вариантов много и они не выстраиваются в
приоритет: какое умение применить, какую цель выбрать, какую позицию занять, какую реплику сказать.

### 4.1 Устройство: ось → кривая → решение → стратегия

```
значение из мира ──► кривая отклика ──► счёт оси ──► свёртка осей × вес ──► счёт решения ──► стратегия выбора
 (дистанция 7 м)      (чем ближе,         (0.6)       (× остальные оси)       (0.18)          (max / random /
                       тем лучше)                                                              weighted / top-N)
```

### 4.2 Счёт как тип, а не `float`

```csharp
public readonly struct Score : IComparable<Score>
{
    private readonly double _value;

    public Score(double value) => _value = Math.Max(0.0, value);     // счёт не бывает отрицательным

    public static implicit operator Score(double v) => new(v);
    public static implicit operator double(Score s) => s._value;

    public static Score operator *(Score a, Score b) => new(a._value * b._value);
    public static Score operator +(Score a, Score b) => new(a._value + b._value);

    public int CompareTo(Score other) => _value.CompareTo(other._value);
    public override string ToString() => _value.ToString("0.000");
}
```

Зачем: клэмп в одном месте, невозможно случайно получить отрицательный счёт, в арифметике читается как число,
а в отладочной трассе печатается одинаково.

### 4.3 Ось с правом вето

```csharp
public interface IConsideration
{
    bool Evaluate(out Score score);      // false = «нет данных» = ВЕТО на всё решение
}

public abstract class ConsiderationBase : IConsideration
{
    private readonly ICurve _curve;

    public bool Evaluate(out Score score)
    {
        if (!TryGetValue(out double value)) { score = 0; return false; }
        score = _curve.Evaluate(value);
        return true;
    }

    protected abstract bool TryGetValue(out double value);   // нормализованное значение из мира
}

public sealed class TargetDistanceConsideration : ConsiderationBase
{
    private readonly Agent _agent;
    private readonly float _maxDistance;

    protected override bool TryGetValue(out double value)
    {
        Transform target = _agent.Target;
        if (target == null) { value = 0; return false; }      // нет цели → вето, а не ноль

        float distance = Vector3.Distance(_agent.Position, target.position);
        value = Mathf.Clamp01(distance / _maxDistance);
        return true;
    }
}
```

**Почему вето — `bool`, а не нулевой счёт.** «Ноль» и «нет данных» — разные вещи. Умение «ударить» при
отсутствии цели должно **не рассматриваться**, а не набрать 0 и утонуть среди других нулей: в трассе отладки
видно «вариант отброшен осью Target», а не «все варианты по нулям, почему-то выбран случайный».

### 4.4 Решение: обязательные и необязательные оси

```csharp
public sealed class Decision
{
    private readonly List<IConsideration> _required = new();    // отказ любой убивает вариант
    private readonly List<IConsideration> _optional = new();    // только меняют счёт
    private readonly Func<Score, Score, Score> _reduce;         // Mul (по умолчанию) или Sum
    private double _weight = 1.0;

    public bool Evaluate(out Score result)
    {
        result = _weight;

        for (int i = 0; i < _required.Count; i++)
        {
            if (!_required[i].Evaluate(out Score score)) { result = 0; return false; }
            result = _reduce(result, score);
        }

        for (int i = 0; i < _optional.Count; i++)
            if (_optional[i].Evaluate(out Score score))
                result = _reduce(result, score);

        return true;
    }
}
```

Две группы осей закрывают 80% ручной работы, которую в наивном utility приходится делать условиями снаружи:
«бить можно только если есть цель, кулдаун прошёл и дистанция подходит» — это обязательные; «а ещё лучше, если
у цели мало HP и рядом союзник» — необязательные.

**Умножение против суммы.** Умножение (по умолчанию) означает «все оси важны»: провал одной тянет счёт к нулю.
Сумма означает «накопление аргументов». У умножения есть известная беда: чем больше осей, тем ниже итог, и
варианты с 5 осями проигрывают вариантам с 2. Лечится **компенсацией** — возведением результата в степень
`1/n`:

```csharp
// Dave Mark, компенсация за число осей
double compensated = Math.Pow(product, 1.0 / considerationCount);
```

### 4.5 Библиотека кривых отклика (IAUS)

Все кривые — одна параметризация: **наклон `m`, показатель `k`, смещение по X `b`, смещение по Y `c`**, и
результат клэмпится в `[min, max]` (обычно `[0, 1]`).

```csharp
public interface ICurve { double Evaluate(double x); }

public abstract class ClampedCurve : ICurve
{
    protected double Min = 0, Max = 1;
    public double Evaluate(double x) => Math.Clamp(EvaluateInternal(x), Min, Max);
    protected abstract double EvaluateInternal(double x);
}
```

| Кривая | Формула | Форма | Когда |
|---|---|---|---|
| Linear | `m·(x − b) + c` | прямая | «чем ближе, тем лучше» |
| Polynomial | `m·(x − b)^k + c` | парабола/куб | резкий рост у края диапазона |
| Logistic | `k · 1/(1 + e^(−m·(x + b))) + c` | S-кривая | плавный порог «включилось» |
| Logit (quantile) | `(m · ln((x + b)/(1 − (x + b))) + c)/k` | обратная S | «почти всегда 0, у края взрывается» |
| Normal (Gauss) | `m · 1/(k√2π) · e^(−(x−b)²/(2k²)) + c` | колокол | «оптимум в середине» (желаемая дистанция) |
| Sine | `k · sin(m·(x − b)) + c` | волна | периодика, патрульная активность |
| Hyperbolic | `m/(x + b) + c` | гипербола | «критично вблизи нуля» (мало HP) |
| Const | `c` | полка | заглушка, базовый вес |
| Custom | `AnimationCurve.Evaluate(x)` | любая | «дизайнер нарисует сам» |

Декораторы: `Invert` → `1 − f(x)`, `Multiply(w)` → `w · f(x)`.

```csharp
public sealed class LogisticCurve : ClampedCurve
{
    private readonly double _m, _k, _b, _c;

    protected override double EvaluateInternal(double x) =>
        _k * (1.0 / (1.0 + Math.Exp(-_m * (x + _b)))) + _c;
}

public sealed class NormalCurve : ClampedCurve
{
    private readonly double _m, _k, _b, _c;      // k = сигма

    protected override double EvaluateInternal(double x)
    {
        double exponent = -((x - _b) * (x - _b)) / (2.0 * _k * _k);   // ← скобки обязательны!
        return _m * (1.0 / (_k * 2.5066282746)) * Math.Exp(exponent) + _c;
    }
}
```

> **Грабли из живого кода.** В разобранном фреймворке гауссиана написана как
> `-(1.0 / k * k * 2.0)`, что вычисляется в `−2.0`: сигма **не влияет на ширину колокола**. Баг жил годами,
> потому что из девяти кривых тестом покрыта была только линейная. **Тест на каждую кривую, три точки на
> кривую** — минимум.

### 4.6 Стратегия выбора — отдельный интерфейс

```csharp
public interface ISelectionStrategy
{
    bool SelectBest(in Span<Score> scores, int count, out int bestIndex);
}
```

| Стратегия | Поведение | Когда |
|---|---|---|
| `Max(threshold)` | лучший выше порога | «всегда оптимально» — боссы, шахматы |
| `Random(threshold)` | равновероятно среди прошедших порог | массовка, чтобы не синхронизировались |
| `RandomWeighted` | рулетка по счёту | естественная вариативность |
| `TopRandom(n, threshold)` | случайный из топ-N | **рабочий дефолт для боя**: разумно и непредсказуемо |

```csharp
public sealed class TopRandomStrategy : ISelectionStrategy
{
    private readonly int _topCount;
    private readonly double _threshold;
    private readonly System.Random _random;          // сид агента, НЕ UnityEngine.Random

    public bool SelectBest(in Span<Score> scores, int count, out int bestIndex)
    {
        Span<int> candidates = stackalloc int[count];
        int candidateCount = 0;

        for (int i = 0; i < count; i++)
            if (scores[i] >= _threshold)
                candidates[candidateCount++] = i;

        if (candidateCount == 0) { bestIndex = -1; return false; }

        // частичная сортировка по счёту — ТОЛЬКО по счёту (см. грабли ниже)
        for (int i = 0; i < Math.Min(_topCount, candidateCount); i++)
            for (int j = i + 1; j < candidateCount; j++)
                if (scores[candidates[j]] > scores[candidates[i]])
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);

        bestIndex = candidates[_random.Next(Math.Min(_topCount, candidateCount))];
        return true;
    }
}
```

> **Грабли из живого кода.** Там `TopRandom` сортировал пары `(счёт, индекс)` **по индексу**, а не по счёту —
> «топ-3» оказывался произвольной тройкой. Тест проверял только «вернулось что-то из списка». Тест стратегии
> должен задавать **разные** счёты и проверять, **кого именно** выбрали.

### 4.7 Оценка без аллокаций и с отладкой

```csharp
public bool Evaluate(out TKey bestDecision)
{
    bestDecision = default;
    int count = _decisions.Count;
    if (count == 0) return false;

    Span<Score> scores = stackalloc Score[count];
    Span<int> indices = stackalloc int[count];
    int scored = 0;

    for (int i = 0; i < count; i++)
        if (_decisions[i].Value.Evaluate(out Score score))
        {
            scores[scored] = score;
            indices[scored] = i;
            scored++;
        }

    if (scored == 0) return false;
    if (!_strategy.SelectBest(scores, scored, out int best)) return false;

    bestDecision = _decisions[indices[best]].Key;
    return true;
}

// отдельный метод ТОЛЬКО для окна отладки: все счёты с именами
public Dictionary<TKey, double> EvaluateScores() { /* … */ }
```

**Правило:** utility без отладочного вывода оценок не отлаживается в принципе. «Почему он выбрал отход, а не
удар» — вопрос, на который должен отвечать инструмент, а не чтение кода.

### 4.8 Правила utility

- Значение оси **нормализовать в [0..1]** перед кривой: кривые параметризованы под этот диапазон.
- Ось = один смысл («дистанция», «мало HP», «кулдаун»), а не «общая оценка атаки».
- Кулдаун и «умение ещё идёт» — тоже оси (0 = нельзя), а не отдельные `if` снаружи.
- Анти-повтор — ось или множитель, а не память в селекторе: «только что делал это» снижает счёт.
- Не делать utility всем мозгом. Он — селектор внутри каркаса: «utility решает *что* из многого, граф решает
  *когда вообще об этом думать*».
- Числа тюнит дизайнер, значит кривые — в инспекторе, а наборы осей — переиспользуемыми ассетами.

---

## 5. GOAP

Планировщик: из текущих фактов мира и набора действий строит **последовательность действий**, приводящую к
цели. Агент не знает сценария — он знает, что чем достигается.

### 5.1 Данные

```csharp
public interface IGoapAction
{
    string Name { get; }
    bool IsValid { get; }           // можно ли вообще брать сейчас (есть оружие, есть ресурс)
    float Cost { get; }             // динамическая цена (расстояние, риск)
    LocalState Conditions { get; }  // что должно быть правдой ДО
    LocalState Effects { get; }     // что станет правдой ПОСЛЕ
}

public interface IGoapGoal
{
    string Name { get; }
    bool IsValid { get; }
    int Priority { get; }
    LocalState Effect { get; }      // чего хотим
}

public interface IGoapSensor { void WriteState(WorldState state); }   // только сенсоры пишут факты
```

Факты — пары «ключ → bool» (`WorldState` — словарь, `LocalState` — компактный список пар для условий и
эффектов). Действие объявляется **данными, без нового класса**:

```csharp
new GoapAction(
    name: "PickUpWeapon",
    conditions: new LocalState(("SeeWeapon", true), ("WeaponInReach", true)),
    effects:    new LocalState(("HasWeapon", true)),
    isValid: () => !inventory.IsFull,
    cost:    () => Vector3.Distance(agent.Position, weapon.Position));
```

### 5.2 Планировщик: обратный A*

Ищем **от цели к миру**: какие действия дают нужный эффект, чем закрыть их условия, и так до фактов, которые
уже истинны.

```csharp
public bool Plan(WorldState world, LocalState goal, IReadOnlyList<IGoapAction> actions, List<IGoapAction> plan)
{
    if (world.Overlaps(goal)) return true;                 // уже выполнено

    var open = DictionaryPool<IGoapAction, Node>.Get();
    var closed = HashSetPool<IGoapAction>.Get();

    // 1) стартуем от действий, чьи эффекты закрывают цель
    foreach (IGoapAction action in actions)
        if (IsNeighbour(action, goal, world))
            open.Add(action, new Node(action, parent: null, cost: action.Cost,
                                      heuristic: Heuristic(world, action)));

    bool complete = false;

    while (TryPeekCheapest(open, out Node node))           // вес = cost + heuristic
    {
        open.Remove(node.Action);

        if (world.Overlaps(node.Action.Conditions))        // условия выполнены в мире — план готов
        {
            BuildPlan(node, plan);                         // идём по parent → порядок исполнения
            complete = true;
            break;
        }

        VisitNeighbours(node, world, actions, open, closed);
        closed.Add(node.Action);
    }

    DictionaryPool<IGoapAction, Node>.Release(open);
    HashSetPool<IGoapAction>.Release(closed);
    return complete;
}

// действие годится, если КАЖДОЕ условие закрыто либо его эффектами, либо уже миром
private static bool IsNeighbour(IGoapAction action, LocalState conditions, WorldState world)
{
    for (int i = 0; i < conditions.Count; i++)
        if (!action.Effects.Overlaps(conditions[i]) && !world.Overlaps(conditions[i]))
            return false;
    return true;
}

// эвристика: сколько условий действия НЕ совпадает с миром; неизвестный факт = отсечение
private float Heuristic(WorldState world, IGoapAction action)
{
    float result = 0;
    LocalState conditions = action.Conditions;

    for (int i = 0; i < conditions.Count; i++)
    {
        if (!world.TryGetValue(conditions[i].Key, out bool value))
            return float.MaxValue / 2;                      // о таком факте мир ничего не знает
        if (value != conditions[i].Value)
            result += 1f;
    }
    return result;
}
```

Поскольку поиск обратный, обход `parent` от найденного узла даёт **сразу порядок исполнения** — ничего
разворачивать не надо.

### 5.3 Конъюнктивные цели: композитное действие

Цель «иметь оружие И патроны» часто не закрывается одним действием, и наивный GOAP сдаётся. Приём: если ни
одно действие не закрывает набор из ≥2 условий, планируем **каждое условие отдельной подзадачей** и склеиваем
подпланы в один узел:

```csharp
private bool ComposeNeighbour(LocalState goal, WorldState world, IReadOnlyList<IGoapAction> actions,
                              int depth, out CompositeAction composite)
{
    composite = null;
    if (goal.Count < 2 || depth <= 0) return false;

    var result = new CompositeAction(goal);                 // цена = сумма, Decompose() раскрывает
    var localPlan = ListPool<IGoapAction>.Get();

    for (int i = 0; i < goal.Count; i++)
    {
        if (world.Overlaps(goal[i])) continue;

        localPlan.Clear();
        if (!Plan(world, new LocalState(goal[i]), actions, localPlan)) { result = null; break; }

        result.AddRange(localPlan);
    }

    ListPool<IGoapAction>.Release(localPlan);
    composite = result;
    return composite != null;
}
```

Глубина рекурсии — параметр (3 достаточно). Без ограничения планировщик уходит в экспоненту.

### 5.4 Правила и когда НЕ брать GOAP

- Открытый список словарём с линейным поиском минимума — нормально до ~30 действий; дальше нужна куча.
- Все структуры поиска — **из пулов**: планирование не должно аллоцировать.
- **Планировать не каждый кадр**, а по событию: план провалился, цель изменилась, прошёл интервал.
- Инерция плана: не перепланировать, пока текущий план выполняется успешно, иначе агент «передумывает»
  на каждом шаге.
- **Не брать GOAP, если** поведение описывается 5–10 режимами (возьмите граф), если дизайнер должен точно
  знать, что сделает враг (планировщик даёт эмерджентность, а не контроль), или если некому отлаживать
  «почему план такой» — это самая дорогая отладка из всех моделей.

---

## 6. HTN

Планировщик, который **декомпозирует** задачу по заранее написанным методам: структура авторская (как BT), но
есть реальный поиск с откатами и проверкой предусловий (как GOAP). Killzone, Transformers.

### 6.1 Три узла домена

```csharp
public interface ITask
{
    // пишет имена операторов в plan[index…], возвращает, сколько записал
    bool Decide(WorldState simulation, string[] plan, int index, out int size);
}

// лист: символ + условие + эффект на СИМУЛЯЦИИ (мир не трогается)
public sealed class PrimitiveTask : ITask
{
    private readonly string _operator;
    private readonly Predicate<WorldState> _condition;
    private readonly Action<WorldState> _effect;

    public bool Decide(WorldState simulation, string[] plan, int index, out int size)
    {
        size = 0;
        if (!_condition(simulation)) return false;

        plan[index] = _operator;
        size = 1;
        _effect(simulation);
        return true;
    }
}

// последовательность: должны получиться ВСЕ подзадачи
public sealed class Method : ITask
{
    private readonly Predicate<WorldState> _condition;
    private readonly ITask[] _sequence;

    public bool Decide(WorldState simulation, string[] plan, int index, out int size)
    {
        size = 0;
        if (!_condition(simulation)) return false;

        for (int i = 0; i < _sequence.Length; i++)
        {
            if (!_sequence[i].Decide(simulation, plan, index + size, out int length))
                return false;
            size += length;
        }
        return true;
    }
}

// выбор: первый метод, который получился НА КОПИИ состояния
public sealed class CompoundTask : ITask
{
    private readonly Predicate<WorldState> _condition;
    private readonly ITask[] _methods;

    public bool Decide(WorldState simulation, string[] plan, int index, out int size)
    {
        size = 0;
        if (!_condition(simulation)) return false;

        for (int i = 0; i < _methods.Length; i++)
            if (_methods[i].Decide(simulation.Clone(), plan, index, out size))
                return true;

        size = 0;
        return false;
    }
}
```

### 6.2 Два приёма, которые делают HTN дешёвым

**План — заранее выделенный буфер.** Никаких списков и аллокаций:

```csharp
private readonly string[] _plan = new string[32];

if (agent.Decide(_plan, out int size))
    planChanged.Invoke(_plan, size);
```

**Дискретизация геометрии в символы.** Символьный планировщик не умеет «7.3 метра», зато умеет `Near`:

```csharp
public enum DistanceType { Far = 0, Near = 1, At = 2 }

// сенсор переводит непрерывное в символ
worldState.Set(Keys.EnemyDistance, Classify(distance, atRange, nearRange));

// а действия описываются в символах
MoveNearEnemy: effect   EnemyDistance = Near
RangeCombat:   condition EnemyDistance == Near && HasAmmo == true
```

### 6.3 Домен как архетип

Домен — это и есть «характер» врага. Один и тот же набор операторов даёт разных бойцов:

```csharp
// Снайпер: держать дистанцию и стрелять
ITask sniper = new CompoundTask(
    new Method(ws => ws.Equals(SeeWeapon, true) && ws.Equals(HasWeapon, false), MoveAtWeapon, PickUpWeapon),
    new Method(ws => ws.Equals(SeeEnemy, true) && ws.Equals(EnemyAlive, true), MoveFarEnemy, RangeCombat),
    Patrol);

// Боец ближнего боя: сблизиться и рубить
ITask brawler = new Method(ws => ws.Equals(SeeEnemy, true) && ws.Equals(EnemyAlive, true),
    MoveAtEnemy, MeleeCombat);

// Поддержка: лечить, снабжать, держаться позади
ITask support = new CompoundTask(
    new Method(ws => ws.Equals(SeeInjuredBuddy, true) && ws.Equals(HasMedkit, true),
               MoveAtInjuredBuddy, HealBuddy),
    new Method(ws => ws.Equals(SeeEmptyBuddy, true) && ws.Equals(HasAmmo, true),
               MoveAtEmptyBuddy, GiveAmmo),
    new Method(ws => ws.Equals(SeeEnemy, true), MoveFarEnemy),
    Patrol);
```

**Подмена домена снаружи** — механизм, которым отряд меняет поведение бойца, не отбирая управление:

```csharp
public sealed class PlannerDecorator : IPlannerAgent
{
    private readonly IPlannerAgent _origin;
    private ITask _overridden;

    public void OverrideDomain(ITask domain) => _overridden = domain;
    public void RollbackDomain() => _overridden = null;

    public bool Decide(string[] plan, out int size) =>
        _overridden == null ? _origin.Decide(plan, out size)
                            : _origin.Decide(_overridden, plan, out size);
}
```

---

## 7. Шов «планировщик → план → исполнитель»

**Главный архитектурный вывод всего справочника.** Планировщик не исполняет. Он выдаёт план из **имён
операторов**; исполняет план дерево/граф, собранный по таблице «имя → узел».

```
Сенсоры → факты → Планировщик.Decide(plan[], out size)        ← чистая логика, тестируется без сцены
                        │
                        ▼
          plan = ["MoveAtWeapon", "PickUpWeapon", "MoveNearEnemy", "RangeCombat"]
                        │
                        ▼
          таблица { "MoveAtWeapon" → MoveAtWeaponNode, … } → Sequence из реальных узлов
                        │
                        ▼
          узлы исполняются со статусами Running/Success/Failure, прерыванием, входом и выходом
```

```csharp
// производитель плана: по запросу (не каждый кадр!)
public void OnFixedUpdate(float deltaTime)
{
    if (!_planRequest.Consume()) return;
    if (!_planner.Decide(_plan, out int size)) return;
    _planChanged.Invoke(_plan, size);
}

// потребитель: собрать последовательность из готовых узлов
private void OnPlanChanged(string[] plan, int size)
{
    _sequence.Clear();
    for (int i = 0; i < size; i++)
        _sequence.Add(_nodes[plan[i]]);       // _nodes собрана один раз на агента
}
```

```csharp
// таблица собирается один раз при сборке агента
var nodes = new Dictionary<string, IBehaviourNode>
{
    { nameof(MoveAtEnemy),   new MoveAtEnemyNode(agent) },
    { nameof(MoveNearEnemy), new MoveNearEnemyNode(agent) },
    { nameof(MoveFarEnemy),  new MoveFarEnemyNode(agent) },
    { nameof(MeleeCombat),   new MeleeCombatNode(agent) },
    { nameof(RangeCombat),   new RangeCombatNode(agent) },
    { nameof(PickUpWeapon),  new PickUpWeaponNode(agent) },
    { nameof(Patrol),        new PatrolNode(agent) },
};
```

А лист переводит результат механики в статус по единой схеме:

```csharp
protected override BehaviourResult OnUpdate(float deltaTime)
{
    Agent target = _target.Value;
    if (target == null) return BehaviourResult.Failure;              // нет цели
    if (!target.IsAlive) return BehaviourResult.Success;             // цель мертва — задача выполнена

    _agent.Actuator.Face(DirectionTo(target));
    _agent.Abilities.Press(AbilityId.Fire);
    return BehaviourResult.Running;                                 // делаю
}
```

**Что это даёт:**

| | Без шва (планировщик «сам ведёт») | Со швом |
|---|---|---|
| Тестируемость | только в сцене, с телом и физикой | планировщик — чистый C#, тест в миллисекунды |
| Повторное использование | действие знает и логику, и исполнение | узел один, планов много |
| Отладка | «почему он так пошёл» — в двух местах | план виден строкой, исполнение — в графе |
| Архетипы | новый тип врага = новый код | новый тип врага = другой домен/цели |

Правило: **оператор — символ, узел — исполнитель, таблица — единственная связка.** Имена операторов брать
через `nameof(...)`, а не строковыми литералами в двух местах.
---

# Часть II. Данные и восприятие

## 8. Blackboard

Типизированное хранилище фактов агента — единственный канал данных между слоями и между узлами. Так устроены
все системы: Unreal (Blackboard Asset + ключи), Unity Behavior (переменные графа, перекрываемые на агенте),
NodeCanvas, GOAP (`WorldKey`).

### 8.1 Что должно быть в контракте

```csharp
public interface IBlackboard
{
    // типизированный ключ: тип известен на этапе компиляции, боксинга нет
    bool TryGet<T>(BlackboardKey<T> key, out T value);
    T Get<T>(BlackboardKey<T> key);
    void Set<T>(BlackboardKey<T> key, T value);
    bool Remove<T>(BlackboardKey<T> key);

    // «тег» — ключ без значения: сам факт присутствия
    bool HasTag(TagKey key);
    void SetTag(TagKey key);
    bool RemoveTag(TagKey key);

    event Action<int> Changed;           // индекс слота; на этом строятся переходы по изменению
}
```

| Приём | Зачем |
|---|---|
| **Типизированный ключ** (`BlackboardKey<T>`) или сгенерированная `int`-константа | имя компилируется в индекс один раз; в тике нет хеширования строк |
| **Ключ выбирается из списка в инспекторе** (атрибут + драйвер свойства) | дизайнер не вводит строку с опечаткой |
| **Словарь на тип** (или слоты с типом) | нет боксинга для `float`/`int`/`Vector2` |
| **Тег вместо `bool`** | «оглушён», «в укрытии», «заметил» — дешевле и читается в отладке как список |
| **Доступ к структуре по ссылке** (`ref T`) | менять поле структуры по месту, не перезаписывая слот |
| **Событие изменения** | переходы по событию вместо опроса; лог «кто и когда поменял» |
| **Области видимости** | `Agent` (своё), `Squad` (общее отряду — канал координации), `Global` (уровень тревоги, фаза боя) |
| **Перекрытия на экземпляре** | один граф на всех, а `AttackRange` у этого врага 1.8, у того 2.6 |

### 8.2 Что хранить, а что нет

**Хранить:** цель и последняя известная позиция, индекс маршрута, роль, приказ, уровень внимания, кулдауны,
выбранное умение, точка отхода, «кто меня ударил последним».

**Не хранить:** ссылки на компоненты игры (их кеширует узел при входе), результаты дорогих запросов дольше
одного think-тика (кеш с отметкой тика), визуальное состояние (это дело анимации), копии настроек (настройки
живут в архетипе).

> **Грабли из живого кода.** Руками написанная типизированная поверхность на девять типов дала два
> copy-paste-бага: `HasFloat` проверял словарь `float2`, `DelQuaternion` удалял из словаря `float3`. Такую
> поверхность надо **генерировать** (как мы генерируем API банка звуков) или делать дженериком с типизированным
> ключом. И тест на `Set/Has/Remove` **каждого** типа — три строки, которые это ловят.

---

## 9. Восприятие

### 9.1 Трек с уверенностью вместо «вижу / не вижу»

Бинарное `CanSee` даёт роботов: враг либо не видит, либо мгновенно всё знает. Рабочая модель — **трек на
цель** с накоплением и спадом:

```csharp
public sealed class TargetTrack
{
    public Transform Target;
    public float Awareness;              // 0..1
    public Vector3 LastKnownPosition;
    public float TimeSinceSeen;

    public void Tick(bool visible, Vector3 position, float gain, float decay, float dt)
    {
        if (visible)
        {
            LastKnownPosition = position;
            TimeSinceSeen = 0f;
            Awareness = Mathf.Min(1f, Awareness + gain * dt);
        }
        else
        {
            TimeSinceSeen += dt;
            Awareness = Mathf.Max(0f, Awareness - decay * dt);
        }
    }
}
```

Пороги дают состояния бесплатно: `< 0.3` — не заметил; `0.3..0.8` — «что-то там» (поворот головы, бурчание,
состояние `Notice`); `> 0.8` — цель захвачена (`Combat`). Игрок читает эти пороги как «он меня почти заметил» —
то самое, что делает стелс честным.

**Числа из шипнутых игр:** время реакции человека-игрока ≈ 0.16–0.22 с, поэтому реакция ИИ в районе
0.2–0.4 с читается как «живая». Скорость набора внимания — так, чтобы с полного обзора цель захватывалась за
0.3–1.2 с в зависимости от роли (разведчик быстрее, тяжёлый медленнее).

### 9.2 Зрение: конусы и маски

```csharp
public bool CanSee(Transform target)
{
    Vector3 to = target.position - _eye.position;

    // 1) дешёвый гейт: дистанция в квадрате
    float sqr = to.sqrMagnitude;
    if (sqr > _radius * _radius) return false;

    // 2) дешёвый гейт: угол (без нормализации и acos)
    if (Vector3.Dot(_eye.forward, to / Mathf.Sqrt(sqr)) < _cosHalfAngle) return false;

    // 3) дорогой тест: луч по маске стен — только если прошли гейты
    return !Physics.Linecast(_eye.position, target.position, _obstacleMask, QueryTriggerInteraction.Ignore);
}
```

Приёмы:

- **Два-три конуса вместо одного**: узкий далёкий «фокус» + широкий близкий «периферия» + маленький
  «спина» — так враг не видит сквозь затылок, но замечает подкрадывающегося вплотную.
- **Угол зрения от дистанции** (The Last of Us): вблизи обзор шире, далеко — уже. Один график вместо трёх
  конусов.
- **Маска стен отдельно от маски цели** — иначе луч упирается в самого себя или в декорации.
- Физические запросы — **`NonAlloc`-версии**, маски вместо фильтрации по тегам после.

### 9.3 Слух, урон и «нечестные» входы

Не всё должно идти через зрение, и это нормально:

```csharp
public void Alert(Transform source, float awareness = 1f);   // крик союзника, выстрел, удар в спину
```

- **Слух** — событие с позицией и громкостью, затухание по дистанции (и опционально через стены ×0.5):
  шаги игрока, выстрел, падение ящика. Реализуется шиной сигналов (§10), а не опросом.
- **Урон будит всегда**: ударили в спину — трек создаётся с `Awareness = 1` и `LastKnownPosition` = позиция
  источника. Иначе бой за спиной выглядит сломанным.
- **Зона присутствия**: вход игрока в комнату-арену поднимает тревогу всем в комнате. Не «честно», зато
  предсказуемо для дизайнера энкаунтера.

### 9.4 Память и поиск

- Трек живёт после потери видимости `MemoryTime` (3–8 с) — всё это время враг идёт к `LastKnownPosition`.
- После прихода — **поиск**: 2–5 точек вокруг последней позиции (в шипнутых играх — по «карте поиска», чтобы
  двое не искали в одном месте), потом возврат на маршрут.
- **Разные стамп-времена на каждую цель**, а не один флаг «вижу кого-то»: в кооперативе один bool на N игроков
  даёт баг «потерял одного — считает, что потерял всех».
- Привязка к дому (`HomeAnchor` + радиус) — чтобы патрульный не ушёл за игроком через полкарты. Числа:
  потеря из виду 6–10 с, радиус возврата 20–40 м.

### 9.5 Поиск цели дешёвым способом

Перебор всех врагов на каждого агента — O(N²) на кадр. Решение — пространственный индекс:

```csharp
private static readonly ArrayPool<Agent> s_pool = ArrayPool<Agent>.Shared;

private Agent FindClosestEnemy()
{
    Agent[] buffer = s_pool.Rent(BufferSize);              // 4–8 достаточно
    int count = _index.FindAllInRadius(_position, DetectionRadius, buffer, IsEnemy);

    Agent closest = Closest(buffer, count, _position);
    s_pool.Return(buffer);
    return closest;
}
```

Три детали, без которых индекс не окупается:

1. **Агент регистрируется сам** (`Add` при включении, `Remove` при выключении) — никаких
   `FindObjectsOfType`.
2. **Позиция в индексе обновляется с гистерезисом** — только когда агент сместился больше порога (1–2 м):

```csharp
private void OnPositionChanged(Vector3 position)
{
    if ((position - _indexedPosition).sqrMagnitude <= SyncDistance * SyncDistance) return;

    _index.Update(_indexedPosition, position, _agent);
    _indexedPosition = position;
}
```

3. **Периодическая полная пересборка** (раз в 1–2 с) — страховка от накопленных расхождений.

### 9.6 Расфазировка и частоты

```csharp
// Throttle: обёртка над любым дорогим тиком; период СЛУЧАЙНЫЙ в диапазоне
public void Tick(float deltaTime)
{
    if (_timeLeft > 0f) { _timeLeft -= deltaTime; return; }

    _inner.Tick(deltaTime);
    _timeLeft = _random.Range(_minPeriod, _maxPeriod);     // 0.2…0.3 с
}
```

Случайный период решает задачу расфазировки без общего планировщика: 50 агентов не думают в один кадр сами
собой. Рекомендуемые частоты: зрение 5–10 Гц, слух — событиями, выбор цели 2–5 Гц, решение 10–20 Гц рядом с
игроком и 1–2 Гц далеко (§27).

---

## 9б. Стелс: детекция, тревога, честность

Стелс — это не «враг видит или не видит», а **понятная игроку кривая внимания**. Если игрок не может
объяснить, почему его заметили, стелс воспринимается как лотерея.

### 9б.1 Уровни тревоги как состояния

```
Idle ──(первый стимул)──► Suspicious ──(подтверждение N сек)──► Aware ──(потерял)──► Searching ──► Idle
      ◄──(стимул угас)──             ◄──(не подтвердилось)──            ◄──(нашёл)──
```

| Уровень | Что делает агент | Что видит игрок |
|---|---|---|
| `Idle` | маршрут, работа | обычное поведение |
| `Suspicious` | повернулся, идёт проверить, барк «что это?» | **индикатор заполняется** |
| `Aware` | бой, зовёт союзников | индикатор полный, музыка |
| `Searching` | идёт по памяти, проверяет укрытия | индикатор медленно падает |

Числа-ориентиры: подозрение набирается 0.8–1.5 с, подтверждение в полную тревогу ещё 0.3–1 с, поиск длится
4–10 с, возврат на маршрут через 3–8 с после потери.

### 9б.2 Правила честного стелса

1. **Разные пороги в стелсе и в бою.** Пока игрока ни разу не видели, порог обнаружения значительно выше: это
   даёт «первый контакт» по воле игрока, а не случайно (так сделано в The Last of Us).
2. **Точка проверки перекрытия — в пользу игрока.** В стелсе луч идёт в грудь (торчащая из-за ящика макушка не
   выдаёт), в бою — в макушку (прятаться в бою сложнее).
3. **Поиск по памяти, а не по актуальной позиции.** Агент идёт туда, где видел, и проверяет 2–5 точек вокруг.
   Карта поиска (с растеканием вероятности и очисткой просмотренного) не даёт двоим искать в одном месте.
4. **Отвлечения — источники интереса с приоритетами**, и на один стимул реагируют **роли**, а не все сразу
   (Mark of the Ninja: иначе игрок не может растащить охрану).
5. **«Один интерес за раз»**: агент не копит стимулы; новый важнее — перебивает, равный — игнорируется.
6. **Прощение игрока как механика:** задержка перед тревогой, «показалось», возврат к патрулю. Бесплатно
   повышает читаемость.
7. **Тревога передаётся сигналом, а не телепатией** (§10): крик поднимает соседей в радиусе — игрок видит
   источник и может его убрать.

### 9б.3 Звук как проходимость, а не радиус

В 2D (и в закрытых уровнях) честный слух — это **путь**, а не окружность: шаги за стеной не слышны, шаги за
углом — слышны.

```csharp
// дешёвое приближение: громкость падает по ДЛИНЕ ПУТИ, а не по прямой
float PerceivedLoudness(Vector2 source, Vector2 ear, float loudness)
{
    if (!_nav.TryFindPath(source, ear, _buffer, out int size)) return 0f;

    float pathLength = PathLength(_buffer, size);          // сумма длин рёбер
    return loudness * Mathf.Max(0f, 1f - pathLength / MaxHearingPath);
}
```

Если путь считать дорого — последовательность 2–3 лучей по узлам графа; полный радиус «сквозь стены» оставить
только для очень громких событий (взрыв, сирена).

---

## 10. Сигналы и барки

Шина сигналов — то, что превращает группу одиночек в «они общаются».

```csharp
public readonly struct Signal
{
    public readonly SignalId Id;      // EnemySpotted, TakingFire, AllyDied, Help, Grenade, Order
    public readonly Object Sender;
    public readonly Vector3 Position;
    public readonly float Value;
    public readonly Object Subject;   // о ком речь
}

public interface ISignalBus
{
    void Broadcast(in Signal signal, SignalScope scope);   // радиус / фракция / отряд / все
    void Subscribe(ISignalListener listener, SignalFilter filter);
}
```

**Семантика реакции** (скопирована у Unity Behavior, проверена практикой): `Default` — реагировать только
если свободен; `Restart` — перебить текущее; `Once` — один раз; `Queue` — встать в очередь.

**Минимальный набор сигналов, который стоит поддержать сразу:**

| Сигнал | Кто шлёт | Кто реагирует |
|---|---|---|
| `EnemySpotted(position)` | тот, кто захватил цель | все в радиусе/фракции: поднять внимание, повернуться |
| `TakingFire(from)` | получивший урон | укрыться, ответить, позвать |
| `AllyDied(position)` | тело союзника | осторожность, бегство при низком HP, барк |
| `Help(me)` | низкое HP / схвачен | поддержка идёт на помощь |
| `Grenade(at)` | граната | разбежаться из радиуса |
| `Order(role, point)` | командир/координатор | сменить роль, занять точку |

**Барки.** Самый дешёвый способ сделать ИИ «понятным»: реплика на событие, которое игрок и так видит.
Вешать **на события графа** (вход в состояние, получение сигнала, выбор умения), а не внутрь узлов:
«Вижу его!» на входе в `Notice`, «Обходим!» при получении роли фланкёра, «Он за ящиком!» при потере
видимости. Правила: не чаще одного барка на N секунд на агента, не дублировать в один момент (кулдаун на
группу), не говорить то, чего игрок не может проверить.

---

# Часть III. Движение

## 11. Три уровня навигации

```
1. ГЛОБАЛЬНЫЙ ПУТЬ   где идти           граф + A*/Dijkstra/поле потока
2. ЛОКАЛЬНОЕ ВЕДЕНИЕ как идти по пути    steering, сглаживание, обход друг друга
3. ИСПОЛНЕНИЕ ШАГА   чем идти            ходьба/прыжок/падение/лестница/дэш → актуатор
```

Ошибка новичка — делать только (1) и двигать `transform.position`: получается «едет по рельсам».
Ошибка вторая — делать только (2): агент застревает в вогнутой геометрии.

### 11.1 Как связать уровни: вердикт и эскалация

Когда есть дешёвый локальный слой (поле потока, реактивное ведение) и дорогой глобальный (A* по графу),
нужен контракт переключения. Три правила:

**1. Вердикт, а не `bool`.**

```csharp
public enum NavVerdict
{
    Reachable,      // веди меня, всё в порядке
    Detour,         // путь есть, но он кривой (возможно, из-за толпы)
    Unreachable,    // в этой сетке/графе до цели не добраться
    OffGraph,       // я сам вне структуры данных
    NoGraph         // структуры нет вообще
}
```

«Нет направления» и «я вне сетки» — разные причины с разной реакцией. В многоэтажной геометрии проверка
обязана учитывать высоту, иначе на втором этаже выбирается клетка первого.

**2. Отличать геометрию от толпы.** «Путь длиннее прямой в N раз» сам по себе ничего не значит — длинный
обход бывает и от скопления агентов. Эскалировать только если **обход велик И прямая линия геометрически
закрыта**.

**3. Гистерезис и подтверждение.** Разные пороги на вход и выход (например, эскалация при коэффициенте обхода
> 2.5, возврат при < 1.5), подтверждение 0.5–0.75 с, бэкофф на повторную эскалацию ~1 с. И пока работает
дорогой путь, дешёвый слой **не имеет права подменять направление** — иначе его градиент каждый тик тащит
агента назад на «короткую» дорогу.

---

## 12. Графы и алгоритмы поиска пути

### 12.1 Выбор структуры

| Граф | Как строится | Плюсы | Минусы | Где |
|---|---|---|---|---|
| Сетка (grid) | клетки по коллайдерам/tilemap | просто, терпит динамику, годится толпе | много узлов, «ступеньки» | 2D топ-даун, RTS |
| NavMesh | триангуляция геометрии | мало узлов, плавно | 3D-ориентирован, запекание | 3D |
| Точечный граф | точки + связи | полный контроль, прыжки | ручная работа, угловатые пути | 2D-платформер |
| HPA*/порталы | кластеры над графом | дальние пути дёшево | сложность | большие уровни |
| Поле потока | одна волна Dijkstra | тысячи агентов к одной цели | одна цель на поле | толпа, tower defense |

Для NavMesh-путей **обязателен funnel modifier** (string-pulling), иначе путь идёт по центрам треугольников и
агент «качается».

### 12.2 Форма API: буфер и предикат

```csharp
public interface IPathFinder
{
    // основное: без аллокаций, путь пишется в буфер владельца
    bool FindPath(in NavPoint start, in NavPoint end, NavPoint[] path, out int pathSize,
                  Predicate<NavNode> passable = null);

    // удобное: построено на первом, буфер берётся из пула
    NavPoint[] FindPath(in NavPoint start, in NavPoint end, Predicate<NavNode> passable = null);
}
```

- **Проходимость — предикат запроса, а не свойство графа.** Одна структура обслуживает всех:
  `node => body.CanTraverse(node)`. Летающий игнорирует обрывы, тяжёлый не пролезает в узкое, не умеющий
  лестницы их не берёт.
- **Короткие замыкания до поиска** в базовом классе: невалидные концы, `start == end`, «концы — соседи».
  Это снимает заметную долю вызовов вообще без поиска.
- Буфер пути фиксированного размера (64 точки) — «не нашёл за 64 шага» честнее, чем бесконечный путь.

### 12.3 A* одной функцией

```csharp
protected override bool FindPathInternal(in NavPoint start, in NavPoint end, NavPoint[] path,
                                         Predicate<NavNode> passable, out int pathSize)
{
    pathSize = 0;
    var open = DictionaryPool<NavPoint, Node>.Get();
    var closed = HashSetPool<NavPoint>.Get();

    open.Add(start, new Node(start, parent: null, cost: 0, heuristic: _heuristic.Evaluate(start, end)));

    bool complete = false;

    while (TryPeekCheapest(open, out Node node))          // вес = cost + heuristic
    {
        open.Remove(node.Point);

        if (node.Point.Equals(end)) { pathSize = BuildPath(node, path); complete = true; break; }

        closed.Add(node.Point);

        Span<NavPoint> neighbours = stackalloc NavPoint[8];
        int count = GetNeighbours(node.Point, end, passable, ref neighbours);   // ← точка расширения

        for (int i = 0; i < count; i++)
            AddNeighbour(neighbours[i], node, open, closed, end);
    }

    DictionaryPool<NavPoint, Node>.Release(open);
    HashSetPool<NavPoint>.Release(closed);
    return complete;
}
```

**Поиск пути = цикл + функция соседей.** Это главное, что надо понять про навигацию: алгоритм один, а
структуры графа разные — меняется только `GetNeighbours`.

### 12.4 Эвристики

```csharp
public interface IHeuristicStrategy { float Evaluate(in NavPoint a, in NavPoint b); }
```

| Эвристика | Формула | Когда |
|---|---|---|
| Манхэттен | `|dx| + |dz|` | только 4 направления |
| Евклид | `√(dx² + dz²)` | любое направление, «честная» |
| Октиль | `max + 0.41·min` | 8 направлений (0.41 ≈ √2 − 1) — **дефолт для сетки** |

Эвристика **не должна переоценивать** реальную стоимость, иначе путь перестаёт быть оптимальным. Умышленное
умножение эвристики на 1.1–1.3 («weighted A*») даёт быстрее и чуть хуже — допустимый обмен для массовки.

### 12.5 JPS: то же A*, другая функция соседей

```csharp
public class JpsPathFinder : AStarPathFinder
{
    protected override int GetNeighbours(in NavPoint point, in NavPoint end,
                                        Predicate<NavNode> passable, ref Span<NavPoint> neighbours)
    {
        // вместо 8 соседних клеток — «точки прыжка»: сканируем по каждому направлению,
        // пока не встретим вынужденного соседа (препятствие рядом), цель или стену
    }
}
```

На открытых картах раскрытых узлов на порядок меньше. Цена — сложность правил вынужденных соседей: наивная
реализация даёт неоптимальные пути. Брать, когда сетка большая и открытая; на тесной геометрии выгода мала.

### 12.6 Волна и Dijkstra

- **Волна (BFS)** — все шаги одинаковой цены: самый простой и предсказуемый поиск, годится для сеток без
  весов и для «найди ближайшее из многих» (несколько источников волны).
- **Dijkstra** — цены разные, цели нет: строит стоимость до всех клеток. Основа **поля потока**: один проход
  даёт направление для тысяч агентов (§26).

---
## 13. 2D-платформер: особый случай

В платформере «проходимость» — это не «клетка свободна», а **«я умею туда попасть»**: прыжком, падением, по
лестнице, сквозь платформу, дэшем. Поэтому граф строится из **площадок и рёбер-способов**.

### 13.1 Граф площадок

```csharp
public enum NavEdgeKind { Walk, Jump, Fall, Drop, Ladder, Dash }

public readonly struct NavEdge
{
    public readonly int From, To;
    public readonly NavEdgeKind Kind;
    public readonly float Rise;      // перепад высоты (для прыжка)
    public readonly float Run;       // горизонтальная дальность
    public readonly float Cost;      // оценка времени прохождения
}
```

Построение (один раз при загрузке уровня, инструментом в редакторе):

1. Собрать **поверхности**: верхние грани коллайдеров слоя земли + one-way платформы + tilemap.
2. Разрезать поверхности на **площадки** по разрывам и по углу наклона.
3. Между площадками проложить рёбра:
   - `Walk` — края соседних площадок перекрываются по X и по высоте в пределах шага;
   - `Jump` — перепад `Rise` и пропуск `Run` проходят под возможности тела;
   - `Fall` — край, с которого можно спрыгнуть и попасть на площадку ниже (баллистика от нулевой скорости);
   - `Drop` — one-way платформа вниз;
   - `Ladder` — пересечение площадки с лестницей.
4. Для каждого ребра посчитать стоимость как **время прохождения**, а не длину: прыжок дороже ходьбы,
   лестница медленная.

### 13.2 Проходимость считается от тела

```csharp
private bool CanTraverse(in NavEdge edge) => edge.Kind switch
{
    NavEdgeKind.Jump   => _body.JumpHeight >= edge.Rise && _body.JumpReach >= edge.Run,
    NavEdgeKind.Ladder => _body.CanClimb,
    NavEdgeKind.Drop   => _body.CanDropThrough,
    NavEdgeKind.Dash   => _body.HasDash && _body.DashReach >= edge.Run,
    _ => true
};
```

Один граф уровня обслуживает прыгучего, неуклюжего и ползающего — просто разные рёбра им доступны. Числа
берутся **из настроек тела**, а не дублируются в графе: тюнинг прыжка автоматически меняет навигацию.

### 13.3 Прохождение ребра — это исполнение, а не «ехать к точке»

```csharp
public void Step(float dt)
{
    switch (CurrentEdge)
    {
        case NavEdgeKind.Walk:
            _actuator.Move(DirectionTo(SteeringTarget));
            break;

        case NavEdgeKind.Jump:
            _actuator.Face(DirectionTo(SteeringTarget));
            _actuator.Move(DirectionTo(SteeringTarget));
            if (_body.IsGrounded && ShouldJumpNow())
                _actuator.Jump(HoldTimeForHeight(CurrentEdgeRise));   // высота = время удержания
            break;

        case NavEdgeKind.Fall:
            _actuator.Move(DirectionTo(EdgeExitPoint));               // дойти до края и шагнуть
            break;

        case NavEdgeKind.Drop:
            _actuator.Crouch(true);
            _actuator.DropThrough();
            break;

        case NavEdgeKind.Ladder:
            _actuator.Move(VerticalDirectionTo(SteeringTarget));
            break;
    }
}
```

**Это то, чего нет в готовых ассетах** и что делает 2D-ИИ живым: навигатор не «едет к точке», а исполняет
ребро через те же нажатия, что игрок.

### 13.4 Правила ребра

- **Лимит пользователей на ребро** (по умолчанию 1): две капсулы на одном узком проёме читаются как брак.
- **Агент у занятого ребра ждёт у горловины и перепроверяет**, а не объявляет отказ и не ищет новый путь
  (иначе очередь из двух превращается в вечный перепоиск).
- **Журнал отказов:** 3 неудачи подряд выключают ребро на 30 с. Ловит «ребро построено, но пройти нельзя»
  без правки данных; отказы показывать в отладочном оверлее — это баг-репорт для дизайнера уровня.
- **Признак успеха зависит от типа ребра:** падение проверяется приземлением ниже старта, перелаз
  заканчивается на той же высоте. Общее правило «упал» помечало бы каждый успешный перелаз отказом.
- **Валидаторы вместо ручной расстановки:** переходы ищет инструмент по числам («спуск 1–3.5 м», «проём
  1.5–4.5 м», «подъём 1–1.5 м», «бортик 0.5–1.3 м»), а не дизайнер мышью. Ручная расстановка не переживает
  правку геометрии.
- **Реактивный режим обязателен** как запасной: «идти в сторону цели, прыгать перед стеной или обрывом, если
  цель выше». 80% врагов 2D-платформера большего не требуют, и это работает без запекания.

---

## 13а. Дизайн уровня под ИИ

Половина «тупого ИИ» — это геометрия, в которой любой ИИ выглядит тупо. Чеклист, который стоит отдать
дизайнеру уровней.

### 13а.1 Геометрия

- **Проходы шире габарита агента ×1.5.** Проход ровно в габарит — это место, где двое навсегда застрянут, а
  навмеш «схлопнется» в нить.
- **Выпуклые укрытия вместо вогнутых щелей.** Агент в щели не видит ничего и не может выйти, не подставившись.
- **Чистые края платформ** (2D): край без «зубцов» и мелких коллайдеров, иначе ребро `Fall` строится не там,
  где виден край.
- **Без «почти проходимых» мест:** щель на 1–2 пикселя шире габарита даёт путь, который агент физически не
  проедет.
- **Уступы кратны шагу/прыжку.** Если перепад чуть выше высоты прыжка, агент будет упираться в него вечно —
  либо понизить, либо явно сделать непроходимым.
- **Тупики видны как тупики.** Агент, которого путь привёл в тупик, выглядит глупо; дизайнер должен видеть это
  на этапе блокаута.

### 13а.2 Бой и пространство

- **Арена под число атакующих.** Если ёмкость цели 3, а в арену ведёт один коридор шириной в одного — жетоны
  не спасут, трое не встанут.
- **Кольца вокруг цели должны существовать физически:** если вокруг игрока нет места на кольцо отхода (2–4 м),
  агенты будут давить друг друга в него.
- **Пути обхода** (чтобы фланкёр имел что обходить) и **пути отхода** (чтобы отступление читалось как
  решение, а не как застревание).
- **Укрытия парами**: одно укрытие = агент приклеен к нему; два-три рядом = перебежки, которые игрок читает
  как тактику.
- **Высоты.** Стрелок сверху читается опасным; но путь наверх должен быть и для игрока, иначе это
  несправедливость.

### 13а.3 Инструменты, которые стоит сделать дизайнеру

| Инструмент | Что показывает |
|---|---|
| оверлей навмеша/графа площадок | где агент может ходить **на самом деле** |
| подсветка рёбер по типу (`Jump`/`Fall`/`Ladder`) | как агент будет попадать на платформы |
| журнал отказов рёбер (§13.4) | места, где агент пробовал и не смог |
| тепловая карта «куда агенты не ходят» | мёртвые зоны уровня |
| проверка достижимости от спавна до цели **навигацией нужного архетипа** | «врага поставили, а он не дойдёт» |
| число мест в кольце вокруг точки боя | хватит ли места на ёмкость цели |

### 13а.4 Выбор системы координат

Главный переносимый вывод из гоночного ИИ: **выбор системы координат важнее алгоритма.** В гонке решения
принимаются в координатах трассы («смещение от идеальной траектории», «насколько тормозить»), а не в мировых —
и сложный ИИ становится одномерным.

Для платформера такая «трасса» — **граф площадок**: решения («отступить», «обойти», «занять высоту») удобно
принимать в его координатах (узлы, рёбра, высоты), а не в метрах мира. Для арены — **кольца и сектора вокруг
цели**. Прежде чем писать алгоритм, стоит спросить: в каких координатах эта задача тривиальна?

---

## 14. Steering

### 14.1 Контекстные карты вместо суммы векторов

Классика Рейнольдса (`Seek`, `Flee`, `Arrive`, `Pursue`, `Wander`, `ObstacleAvoidance`, `Separation`,
`Alignment`, `Cohesion`) складывается взвешенной суммой — и в этом беда: сумма противоположных векторов даёт
ноль, агент встаёт или дрожит.

**Контекстные карты** (Andrew Fray, F1 2011): каждое поведение пишет не вектор, а два массива по направлениям:

```csharp
public struct ContextMap
{
    public readonly float[] Interest;   // «хочу туда»
    public readonly float[] Danger;     // «туда нельзя»
    public readonly int SlotCount;      // 8–16 слотов в 3D/топ-дауне; в платформере — 2 + прыжок

    public void AddInterest(Vector2 direction, float weight) { /* по косинусу в соседние слоты */ }
    public void AddDanger(Vector2 direction, float weight) { }
}

public Vector2 Resolve()
{
    // 1) минимальная опасность: берём слоты с наименьшим danger
    // 2) среди них — максимальный interest
    // 3) интерполяция между соседними слотами, чтобы направление было плавным
}
```

Правила арбитража, проверенные практикой:

- **Слияние по максимуму, не по сумме**: два источника опасности с одной стороны не удваивают запрет.
- **Интерполяция внутри слота** — иначе агент «щёлкает» между 8 направлениями.
- **Размытие + смешивание с картой предыдущего кадра** (гистерезис) — против дребезга на границе.
- Переход на контекстные карты в F1 2011 убрал ~4000 строк спец-случаев обгона — это аргумент.

### 14.2 Локальный обход друг друга

| Приём | Когда | Цена |
|---|---|---|
| `Separation` (отталкивание) | 5–20 агентов | ничего |
| **Плотность в стоимость поля** | толпа у одной цели | бесплатно, если поле уже есть |
| Антиципирующее уклонение | средняя дистанция (3–8 м) | дёшево |
| RVO/ORCA | честное взаимное уклонение | дорого, но масштабируется |
| **Слоты и очереди** | бой вокруг цели | дёшево и выглядит умнее всего |

**Плотность в стоимость:** агенты сами добавляют штраф в стоимость своих клеток поля (пик ~60 единиц, радиус
2–3 клетки, затухание у цели) — градиенты обтекают скопление **до** того, как кто-то в него упёрся. Это
заменяет постфактум-расталкивание.

**Антиципирующее уклонение** (та полоса, где separation уже не держит, а RVO ещё не нужен):

```csharp
Vector2 CrossingAvoidance(Agent other, float lookahead = 0.6f, float avoidRadius = 2.2f)
{
    Vector2 relativePosition = other.Position - _position;
    Vector2 relativeVelocity = other.Velocity - _velocity;

    float closingTime = -Vector2.Dot(relativePosition, relativeVelocity) /
                         Mathf.Max(0.0001f, relativeVelocity.sqrMagnitude);
    if (closingTime <= 0f || closingTime > lookahead) return Vector2.zero;   // расходятся или далеко

    Vector2 futureOffset = relativePosition + relativeVelocity * closingTime;
    if (futureOffset.magnitude > avoidRadius) return Vector2.zero;

    return -futureOffset.normalized;      // отталкиваемся от БУДУЩЕЙ позиции
}
```

**Правило «держащий позицию не уступает в сторону цели»:** когда толкаются «давящий» и «кружащий», давящего
надо сдвигать *вокруг* цели, а не внутрь неё — иначе толпа вдавливает передний ряд в игрока. И у края обрыва
клетки должны иметь штраф (кайма в 1 клетку), чтобы толчок не выпихивал агента в пропасть.

**Сглаживание направления:** поворот не быстрее ~300°/с — без этого любое арбитражное решение выглядит как
подёргивание.

---

## 15. Арбитр движения

Как только источников движения больше одного (ведение по пути, поле потока, страйф вокруг цели, прохождение
ребра, монтаж умения, реакция на удар), они начинают писать в тело в одном кадре — и агент дёргается или едет
не туда. Лечится арбитром с токенами, **по экземпляру на ось**.

```csharp
public enum MoveAuthority { None, Steering, Path, Strafe, Traversal, Frozen }   // строгий приоритет

public struct MoveClaim { internal int Id; internal MoveAuthority Authority; }

public sealed class MovementArbiter
{
    private MoveAuthority _authority;
    private object _owner;
    private int _id;

    public bool TryClaim(MoveAuthority authority, object owner, out MoveClaim claim)
    {
        bool free = _authority == MoveAuthority.None;
        bool same = ReferenceEquals(_owner, owner);
        bool higher = authority > _authority;

        if (!free && !same && !higher) { claim = default; return false; }

        _authority = authority;
        _owner = owner;
        _id++;                                   // старый токен становится недействительным
        claim = new MoveClaim { Id = _id, Authority = authority };
        return true;
    }

    public bool IsValid(in MoveClaim claim) => claim.Id == _id && claim.Authority == _authority;

    public void Release(in MoveClaim claim)
    {
        if (!IsValid(claim)) return;             // устаревший Release — no-op
        _authority = MoveAuthority.None;
        _owner = null;
        _id++;
    }
}
```

**Правила:**

- Два арбитра: **на передвижение и на поворот отдельно**. Прицеливание не должно отбирать движение.
- Клейм выдаётся, если слот свободен, это тот же владелец, или приоритет **строго выше**.
- **Производитель обязан каждый тик проверять `IsValid` и замолчать**, если токен отобрали.
- `Frozen` (монтаж умения, стан, посадка в транспорт) берёт **обе** оси: тело принадлежит тому, что его играет.
- `Steering` — фоновый уровень, любой явный движитель его вытесняет.
- **Классификация «подход к цели / боковое перемещение» делается один раз при выдаче запроса**, а не каждый
  тик: подход рулится живой целью, боковое — путём к точке. Проверка каждый тик неверна в обе стороны:
  кайтящий игрок превращает подход в «боковое» (агент бежит туда, где игрок был), а игрок, вставший на точку
  обхода, превращает обход в «подход».

---

# Часть IV. Бой

## 16. Умение как данные

```csharp
[Serializable]
public sealed class AbilityDefinition
{
    public AbilityId Id;                  // связка с анимацией и игровым скиллом
    public float Weight = 1f;             // базовый вес в выборе
    public float Cooldown = 1f;           // считается от КОНЦА умения
    public float StartDelay;              // чтобы в начале боя не телеграфировали все сразу
    public float MinRange, MaxRange;
    public bool RequiresGround = true;
    public bool RequiresLineOfSight = true;
    public float WindUp = 0.25f;          // телеграф
    public float Recovery = 0.4f;         // окно уязвимости после
    public int TokenCost = 1;             // жетоны атаки на цели (§18)
    public float RepeatPenalty = 0.5f;    // анти-повтор
    public ConsiderationSet Utility;      // оси оценки (§4)
    public bool CancellableByHurt = true;
}
```

**Правила:**

- **`Id` умения = `Id` клипа анимации.** Тогда новая атака — ассет клипа + строка в наборе анимаций + строка
  в списке умений, **без правки кода и графа**.
- **Длительность берётся из контента**, а не из числа в ИИ: `IAbilityRunner.IsRunning` отвечает «идёт ли
  умение», и знает это тот, кто играет анимацию. Иначе ИИ и анимация расходятся при каждом ретайминге клипа.
- **Кулдаун от конца умения**, не от начала — иначе длинные умения спамятся.
- Исполнение — адаптер к тому, что в игре уже есть:

```csharp
public interface IAbilityRunner
{
    AbilityId Id { get; }
    bool CanStart(AgentContext context);
    void Begin(AgentContext context);      // не Start: у MonoBehaviour это сообщение Unity
    AbilityPhase Phase { get; }            // WindUp / Active / Recovery / Done
    void Cancel();
}
```

## 17. Выбор умения

```
фильтр по жёстким условиям → utility-оценка → стратегия выбора → жетон на цели → исполнение
(дистанция, линия,            (§4: оси,        (TopRandom)       (§18)
 кулдаун, земля/воздух,        кривые, вето)
 хватает ли жетонов)
```

Два правила, экономящих недели тюнинга:

1. **Что должно зависеть от дистанции — решает дистанция, а не бросок.** Переключение «стойки» (ближний бой ↔
   дальний) делается порогами с гистерезисом (вход 6.5 м, выход 4.5 м) и выдержками (1.5 с на вход, 3.5 с
   минимум в стойке), чтобы мигание линии взгляда не плясало стойкой; пока стойка активна, из пула берётся
   строго её запись. Иначе случайный выбор раз в N секунд рано или поздно выдаёт удар мечом с десяти метров.
2. **Исключительные приёмы — условие, а не вес.** Захват: «цель блокирует И ближе 2.2 м» → принудительное
   умение, а вес захвата в пуле **обнулён**, чтобы бросок его не выдал никогда. Любой приём, уместный только в
   одной ситуации, выносится из броска в условие.

## 18. Честность боя: жетоны и слоты

Канонический приём экшенов (Halo, Arkham, Assassin's Creed): **атаковать может столько агентов, сколько
разрешает цель**. Остальные «танцуют» вокруг, подходят, имитируют агрессию.

### 18.1 Веса и ёмкости вместо счётчика

Шипнутая версия (Kingdoms of Amalur): не счётчик атакующих, а **веса и ёмкости**:

- у цели — `GridCapacity` (сколько врагов вообще пустить в ближний круг) и `AttackCapacity` (сколько и каких
  атак одновременно);
- у врага — `GridWeight` (солдат 4, тролль 8), у атаки — `AttackWeight`;
- ёмкость 12 → третий тролль ждёт снаружи.

Слоты раздаёт **центральный распределитель**; враг каждый кадр спрашивает свой слот и не помнит его;
атакующий лочит слот и **освобождает сразу после начала атаки** (ротация). Без слота агент встаёт напротив
свободного слота — и получается бесплатное фланкирование. **Сложность крутится ёмкостями цели**, а не весами
врагов.

### 18.2 Девять правил жетонов из шипнутой реализации

1. **Не один счётчик, а несколько параллельных пулов** по типу атаки (ближний / дальний / мелочь) со своими
   лимитами: один пул на всех либо душит стрелков, либо пускает в упор всех сразу.
2. **Одномоментные заявки не исполняются по порядку обхода.** Заявки в одном кадре «созревают» короткое окно
   и ранжируются вместе — иначе кто ударит, решает порядок в коллекции, и это невоспроизводимо.
3. **Приоритетное вытеснение, но никогда не отмена начатого замаха.**
4. **Жетон не выдаётся тому, кто не успеет дойти**: гейт по дистанции плюс «радиус, на котором жетон ещё можно
   выиграть» (считается от скорости и кольца отхода роли).
5. **Защита по близости**: тот, к кому игрок подошёл в упор, получает приоритет на следующий жетон. Радиус
   держать **на длине оружия** — подъём 2 → 4 м сломал бой в шипнутом проекте (кружащие стали выигрывать
   жетоны и бросаться в лицо).
6. **Пауза после опустошения пула** (~0.1 с) — бой «дышит» волнами.
7. **Мелочь ранжируется по сектору обзора цели** (±50°): удар чаффы должен приходить оттуда, где игрок его
   видит; у неё свой множитель давления и нет реактивной защиты.
8. **Эскалация против глухой защиты:** 2 заблокированных удара за 4 с → одному агенту выдаётся непробиваемая
   атака, с задержкой готовности 1.5 с, минимальным интервалом 6 с и кулдауном на кластер (радиус 6 м), чтобы
   непробиваемые не выходили пачкой.
9. **Память атак:** кто-то помнит, какое направление и класс удара уже заходили в цель и сколько раз подряд
   (сквозной удар по нескольким жертвам считается один раз). Это крючок под «игрок спамит один приём — ИИ
   находит ответ».

### 18.3 Кольца и пространство

- **Range bands** (ближний / средний / дальний) + желаемая дистанция роли → кайтинг, отход, сближение.
- Числа дистанций принадлежат **роли**, а не узлу: у давящего кольцо 2 м, у заднего ряда 3.8 м, у стрелка свой
  диапазон.
- **Окно восстановления после атаки** (≈1 с, радиус 3 м): агент не ломится обратно вплотную — читается как
  «отшагнул после удара», стоит ноль.
- **Колонна подхода:** когда несколько идут одним маршрутом, скорость внутри колонны масштабируется, чтобы
  строй не телескопировал.

## 18а. Укрытия и подавление

Для стрелков **позиция важнее стрельбы**: все разборы шутеров (Crysis 2, The Last of Us, Days Gone) сводятся к
выбору точки — кандидаты → фильтры → веса → лучший, с запасным запросом.

### 18а.1 Укрытие — это «спрятался И может стрелять»

```csharp
public readonly struct CoverPoint
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;      // от укрытия в сторону угрозы
    public readonly CoverHeight Height;  // Low (присев) / High (стоя)
    public readonly bool LeftLean, RightLean;   // откуда можно выглянуть
}
```

Генерация: по краям коллайдеров/навмеша с шагом 0.5–1 м, нормаль — наружу, высота — по габариту препятствия.
Печётся в редакторе, динамические укрытия (машина, ящик) добавляются в рантайме как обычные записи.

**Фильтр, который отличает укрытие от стены:** точка годится, только если из неё (или из её «выглядывания»)
**есть линия к цели**. В The Last of Us на каждую точку проверяется до четырёх лучей — иначе агент прячется
там, откуда не может стрелять, и бой превращается в прятки.

```csharp
bool IsUsefulCover(in CoverPoint cover, Vector3 threat, float threatRadius)
{
    if (Vector3.Dot(cover.Normal, (threat - cover.Position).normalized) < 0.2f) return false;  // не от угрозы

    bool hiddenInside = Physics.Linecast(cover.Position + HeadOffset(cover.Height), threat, _obstacles);
    if (!hiddenInside) return false;                      // не прячет — это не укрытие

    // но должна быть позиция, откуда можно стрелять
    return CanShootFrom(cover.Position + LeanOffset(cover, +1), threat)
        || CanShootFrom(cover.Position + LeanOffset(cover, -1), threat)
        || cover.Height == CoverHeight.Low;               // из низкого можно встать
}
```

### 18а.2 Каскад инвалидации — главная боль

Один агент сдвинулся → чужое укрытие стало невалидным → тот побежал → сломал третьему. Лечение:

1. **Агент в укрытии считается стоящим в точке укрытия**, а не там, где фактически шагнул (Days Gone).
2. **Не пересчитывать валидность чаще порога** (0.5–1 с) и только для тех, у кого изменилась обстановка.
3. **Бронь на точку** (§24): укрытие занято — другие его не рассматривают.
4. **Гистерезис на смену укрытия**: новое должно быть лучше текущего в N раз, иначе агент бегает кругами.

### 18а.3 Правила боя за укрытиями

- **Один стреляет — остальные работают.** В TLoU есть роль «стрелок по возможности»: если стреляют все, бой
  превращается в тир. Это те же жетоны (§18), но по линии огня, а не по удару.
- **Подавление** (suppression) — дешёвый способ показать опасность: стрельба «в сторону» + реакция «залечь» у
  того, по кому стреляют. Подавленный агент не стреляет, но и не умирает — игрок получает окно на маневр.
- **Перебежка — отдельное состояние**: выбрал новое укрытие → «я бегу» (уязвим, не стреляет, читается игроком)
  → добежал. Перебегать не чаще раза в 2–4 с и не больше одного агента из группы одновременно.
- **Сложность — точностью, а не хитпоинтами.** Первый выстрел мимо, разброс от дистанции и от того, двигается
  ли цель, «пристрелка» (каждый следующий выстрел точнее). Хитпоинты делают бой долгим, точность — опасным.
- **Карта простреливаемости** (exposure map): предпосчитанный слой «откуда меня видно» вокруг известных
  позиций угрозы; позиция выбирается по нему, а не лучами в рантайме.

---

## 19. Реакции и защита

### 19.1 Ритм атаки: телеграф → окно → наказание

У каждой значимой атаки три фазы: **телеграф** (анимация, звук, подсветка), **окно реакции** игрока,
**наказание**. Длительность телеграфа масштабируется опасностью: лёгкие 0.4–0.6 с, тяжёлые 0.8–1.2 с.
Сильные умения гейтить, чтобы не выпадали подряд.

### 19.2 Реактивная защита (блок и уклон)

```csharp
// опрос каждые 0.15 с, пока цель в телеграфе
if (!target.IsAttacking) return;
if (distanceToTarget > MaxReactionDistance) return;            // 3 м
if (Vector3.Dot(_facing, DirectionTo(target)) < MinFacingDot) return;   // 0.5 — только спереди

float probability = BaseProbability + EscalationPerHit * RecentImpactPressure;   // 0.7 + 0.15 × давление
if (_random.NextDouble() > probability) return;

bool dodge = _random.NextDouble() < DodgeBias;                 // 0.4
float delay = _random.Range(DelayMin, DelayMax);               // 0.1…0.35 с — «человеческая» задержка

Schedule(delay, () =>
{
    if (!target.IsAttacking) return;                           // ← ПЕРЕПРОВЕРКА на срабатывании
    if (dodge && TryDodge(ComputeDodgeDirection(target))) return;
    Block(HoldSeconds);                                        // не вышел уклон → блок, а не «ничего»
});
```

Правила:

- **Задержка реакции случайная** (0.1–0.35 с): кадрово-точный блок читается как читерство.
- **Перепроверка в момент срабатывания задержки**: отменённый замах не должен вызывать блок в пустоту — самый
  заметный артефакт «телепатии наоборот».
- **Вероятность растёт от недавних попаданий по мне** (затухающий счётчик, полураспад ~2 с): спам атак
  встречает блоки, а не бесконечный флинч.
- **Направление уклона — от угрозы и никогда в сторону атакующего**: в сторону (вес 0.7) или назад;
  вырожденный случай «стоим в одной точке» разрешается в стабильную боковую.
- **Вектор уклона идёт тем же каналом, что у игрока** (один payload, один код приёма) — иначе у ИИ и игрока
  расходятся физика и ощущения.
- **Ритм контратак:** 2 контратаки за 3 с, потом 4 с кулдауна — иначе «идеальный блок + мгновенный ответ»
  превращает бой в стену.

### 19.3 Уклонение от снаряда: предсказание, а не реакция

```csharp
// в момент выстрела: прогоняем траекторию ТЕМ ЖЕ шагом и порядком интеграции, что у снаряда
const float SimDt = 1f / 60f;

Vector3 position = muzzle, velocity = initialVelocity;
for (float t = 0; t < MaxFlightSeconds; t += SimDt)
{
    velocity += Physics.gravity * SimDt;
    velocity += ExtraDescentGravity(velocity) * SimDt;
    velocity -= velocity * (Drag * velocity.magnitude * SimDt);
    position += velocity * SimDt;

    if ((position - agentPosition).sqrMagnitude < TriggerRadius * TriggerRadius)
    {
        // планируем перекат так, чтобы СМЕЩЕНИЕ совпало с предсказанным прилётом
        QueueDodge(delay: Mathf.Max(MinReact, t - LeadSeconds), direction: SidewaysFrom(velocity));
        break;
    }
}
```

- Шаг и порядок интеграции **обязаны совпадать** с шагом снаряда: в шипнутом проекте снаряд на 30-герцовом
  сервере считался с шагом 1/30, а предсказание 1/60 — уклоны промахивались, пока шаг снаряда не пришпилили.
- Очередь запланированных уклонов — сортированный массив **с номером вставки**: у одномоментного залпа
  дедлайны совпадают, и нестабильная сортировка перемешала бы их случайно.
- Заблокированный уклон (кулдаун, стан) **повторяет попытку**, пока снаряд не прилетел.

### 19.4 Приёмы «иллюзии интеллекта»

Их стоит вынести в настройки и применять осознанно:

| Приём | Эффект |
|---|---|
| Время реакции 0.2–0.4 с | ИИ «живой», а не телепат |
| Намеренный промах первой атакой | игрок успевает понять правила боя |
| Пауза после смены состояния (`MinStateTime`) | нет дребезга, видно решение |
| «Не стреляй, пока игрок в прицеле меньше X» | снайпер не убивает из ниоткуда |
| Барки на решения (§10) | игрок **понимает**, что ИИ думает |
| Враг иногда делает заведомо неоптимальное | «хорошо выглядящий бой важнее эффективного» (Days Gone) |

## 20. Анимация, эффекты, звук

**ИИ не проигрывает анимацию.** Он выбирает действие; анимация идёт от состояния тела и от умения.

- **Умение ссылается на клип по `Id`**, а не наоборот.
- **Длительность — из контента** (§16): ретайминг клипа не ломает ИИ.
- **Телеграф — это кадры:** окно `WindUp` должно совпадать с замахом в клипе, а момент удара приходить
  **событием кадра** — ИИ только слушает.
- **Барки и звуки реакций** вешаются на события графа (вход в состояние, сигнал, выбор умения), а не внутрь
  узлов.
- Прерывания: получил урон → реакция поверх всего; смерть → мозг выключается; оба случая обязаны корректно
  отменять умение (`Cancel()`), иначе агент застревает в фазе `Active`.

Тезис Bobby Anguelov (Game AI Pro 2, гл. 12): ИИ живёт или умирает вместе с анимационной системой — игрок видит
не решение, а движение. Поэтому шов между ними надо проектировать так же тщательно, как сам мозг.

### 20.1 Три слоя и кто что пишет

```
МОЗГ / ГРАФ ──► ТЕЛО (актуатор, раннер умения, оружие) ──► АНИМАТОР
 «что делать»     состояние тела + события                  читает состояние, пишет только картинку
                        │
                        └──► ОТКЛИКИ (VFX, звук, тряска, барки) — слушают события
```

| Слой | Пишет | Читает | Не делает |
|---|---|---|---|
| Мозг / граф | решение, запросы телу («иди», «примени `Slash`») | доску ИИ, ответ тела | не трогает `Animator`, не играет звук |
| Тело | своё состояние: скорость, направление, действие, фаза, флаги | запросы ИИ | не решает |
| Аниматор | только позу | состояние тела, раз в кадр | не пишет обратно в геймплей |
| Отклики | частицы, звук | события тела и графа | не влияют на геймплей |

- **Анимация читает, но не пишет.** В Unreal значения для графа анимации собираются раз в кадр в отдельную
  копию (Property Access, thread-safe update), граф берёт их без вычислений (Fast Path). Значение, которое
  граф анимации вычисляет сам из геймплея, — первый кандидат в баг рассинхрона.
- **Доска ИИ ≠ «доска» аниматора.** Аниматор не читает blackboard ИИ: переименовали переменную в графе — сломалась
  картинка; сменили модель решений — переделывать аниматор. Если картинке нужен факт («насторожен», «ранен»),
  тело выставляет флаг или тег, аниматор читает его (в Lyra теги персонажа отображаются в переменные графа
  анимации таблицей — Gameplay Tag Property Map).
- **Локомоция измеряется, а не спрашивается:** скорость и направление берутся из того, как тело реально
  сдвинулось, — тогда ноги следуют за любым источником движения (путь, толчок, отбрасывание).
- **Действие — запрос по имени, исход — ответ тела.** Задача графа заканчивается, когда тело ответило «закончено»
  или «прервано», а не по таймеру. Нода «сыграй анимацию» прямо из дерева поведения — известная беда Unreal
  (ломает граф анимации, задача не знает, чем кончилось): задача просит тело, тело играет.

```csharp
// то, что тело показывает аниматору; заполняется один раз за кадр
public struct BodyAnimState
{
    public float Speed;            // м/с, измерено по перемещению
    public Vector2 LocalDirection; // куда идёт относительно взгляда: −1…1
    public bool Grounded;
    public int Action;             // хэш имени действия (0 — свободен)
    public AbilityPhase Phase;     // WindUp / Active / Recovery
    public bool Alert, Hurt;       // флаги для картинки, ставит тело
}
```

### 20.2 Как собирать граф аниматора (Mecanim)

- **Слой — одна функция.** Base — локомоция (blend tree по скорости и направлению); верхний слой с маской от
  позвоночника — удары и действия на ходу (аналог Slot + Layered blend per bone в Unreal: ноги бегут, руки
  бьют); аддитивный — вздрагивание от урона. Слой с весом 0 не считается.
- **Скольжение ног:** параметр «реальная скорость / скорость клипа» в множитель скорости состояния ходьбы.
- **Действие = состояние с именем действия** (= `Id` умения, §16) и общим тегом (`Action`). Вход — `CrossFade`
  из кода по имени, а не паутина стрелок; стрелки — только внутри действия (начало → петля → конец).
  «Действие закончилось» = аниматор вышел из состояний с этим тегом.
- **`Any State` — только для «откуда угодно»:** смерть, оглушение. Классика: «приземление» из `Any State`
  срабатывает и на земле.
- **`StateMachineBehaviour` — лёгкий:** сообщает о входе и выходе, логики геймплея в нём нет.
- **Параметры — по хэшам** (`Animator.StringToHash` один раз); отсутствующий параметр пропускать, а не падать.
- **Связка с `NavMeshAgent`:** `updatePosition = false`; локальная скорость агента — в blend tree; позиция — в
  `OnAnimatorMove` (от агента или из root motion с высотой агента); ушёл от агента дальше радиуса — подтянуть
  одного к другому. Root motion — настройка тела, не ИИ: качество движения против точности обхода.
- **За кадром** — `Cull Completely`; на паузе `Animator` морозить (C.4).
- **Адаптер аниматора — заменяемый:** Mecanim сейчас, Animancer / Playables (переходы из кода, без ассета
  контроллера) — если понадобится. Новую систему анимации Unity анонсировали на GDC 2025 и, по сообщениям прессы,
  поставили на паузу на Unite 2025 — строиться на Mecanim, ничего от неё не ждать.

### 20.3 Время удара — из клипа

The Last of Us Part II описывает удар **данными**: анимации атакующего и цели, условия старта (дистанция,
направление, свободный путь) и **разметка таймлайна клипа** — кадр попадания, окно доводки на цель, неуязвимость.
Длинный чёткий замах — время игроку на реакцию (§19.1).

- **Мгновенное — событием клипа:** кадр удара, выпуск стрелы, шаг. **Длительное — кривой клипа:** «можно
  отменить», «неуязвим», «доводка». Кривая с именем параметра аниматора задаёт его значение, кривые разных клипов
  смешиваются при переходе.
- **Событие может не прийти** (клип прерван переходом раньше кадра). Тело подстраховывает: фаза `Active`
  кончилась без события — решить явно, доставлять удар или нет, и записать в трассу.
- **Число в данных умения — запасной путь** для тела без клипа. Editor-утилита может вынуть числа из клипа
  (`AnimationMode.SampleAnimationClip`, как FFXV вынимала дистанции атак, A.2), чтобы ретайминг не требовал
  правки умения.

### 20.4 Эффекты и звук: событие → отклик

Схема Gameplay Cue из Unreal: **источник называет событие, а что играть — решает ассет отклика.** Умение не
знает, какие частицы у огненного удара; оно поднимает `Cue.Hit.Fire`, а реестр находит отклик.

```csharp
public readonly struct Cue
{
    public readonly CueId Id;           // Windup, Strike, Hit, Hurt, Died, Footstep, Land, Bark.Spotted…
    public readonly Transform Source;   // кто
    public readonly Vector3 Position, Normal;
    public readonly Transform Target;   // в кого (для попадания)
    public readonly float Magnitude;    // сила удара, скорость падения
}

public interface ICueSink               // кто играет: таблица персонажа, общий реестр, тестовый счётчик
{
    void Play(in Cue cue);
    void Begin(in Cue cue);             // длительный: горит, оглушён, натягивает тетиву
    void End(CueId id, Transform source);
}
```

- **Кто поднимает событие:** тело, раннер умения, оружие (замах, удар, попадание, шаг, смерть). Граф — только
  смену решения (барки, §10). **Нода графа звук не играет.**
- **Кто владеет содержимым:** ассет отклика (частицы, звук, кость, на которой они появляются). Своя таблица
  персонажа перекрывает общую; оружие-ассет несёт свои отклики. Новый звук удара — правка ассета, не кода и не
  графа.
- **Длительный отклик — пара `Begin` / `End`,** и `End` обязателен при `Cancel()` и смерти — иначе огонь горит
  на трупе (та же беда, что застрявшая фаза `Active`).
- **Шаги:** событие клипа → луч вниз → поверхность → таблица «поверхность → звук и пыль», одна на проект.
  Переключать поверхность только при её смене.
- **Пул**, частицам в пуле не самоуничтожаться. **Лимиты:** кулдаун одного отклика на агента и на группу (как у
  барков, §10), потолок одновременных звуков одного вида, дальние агенты — тише или без откликов (LOD, §27).
- **Отклик не влияет на геймплей:** урон не зависит от того, сыграл ли эффект. Поэтому в тестах вместо откликов
  подписывается счётчик событий.

### 20.5 Вариант клипа — таблицей

Chooser Table из Unreal — таблица «условия на данных → ассет». Так выбираются варианты, **различающиеся только
картинкой**: какой из трёх замахов, реакция на урон спереди или сзади, приземление с малой или большой высоты.
Это не ветки графа ИИ и не стрелки аниматора. Если варианты различаются **геймплеем** (дальность, урон, окно) —
это разные умения (§16–§17), и выбирает их ИИ.

**Двойняшки:** соседи, начавшие одно действие в один кадр, выглядят как копии. Разброс старта и выбор варианта с
учётом того, что уже играют соседи (общая доска; Game AI Pro 2, «Preventing Animation Twinning Using a Simple
Blackboard»).

**Источники раздела:** Epic — Animation Optimization, Animation in Lyra, Animation Slots, Motion Matching,
Dynamic Asset Selection (Chooser); tranek, GASDocumentation (§4.2 теги, §4.8 Gameplay Cues); Unity — «Build
Animator Controllers», Mecanim Performance, Animation Curves on Imported Clips, Coupling Animation and Navigation,
`StateMachineBehaviour`; Animancer docs; Naughty Dog — melee в TLoU2 (Game Developer); Game AI Pro 2, гл. 12
(Anguelov); Audiokinetic — footsteps material management; Feel (More Mountains) — `MMF_Player`. Ссылки — E.3.

---
## 20а. Формации и строй

Формация нужна, когда группа должна **читаться как группа**: отряд на марше, щитовая стена, конвой, напарники
за игроком.

### 20а.1 Слоты вместо «следуй за лидером»

«Иди за лидером» даёт гусеницу и толкучку. Рабочая модель — **слоты в системе координат формации**:

```csharp
public readonly struct FormationSlot
{
    public readonly Vector2 LocalOffset;   // в координатах формации: X — поперёк, Y — вдоль
    public readonly int Priority;          // кого ставим первым, если участников меньше слотов
    public readonly SlotRole Role;         // Front / Flank / Rear / Support
}

public Vector3 WorldSlot(in FormationSlot slot) =>
    _anchor.position + _anchor.rotation * new Vector3(slot.LocalOffset.x, 0f, slot.LocalOffset.y);
```

| Строй | Слоты | Когда |
|---|---|---|
| Колонна | `(0,0) (0,-1.5) (0,-3)` | узкие коридоры, марш |
| Клин | `(0,0) (-1.5,-1.5) (1.5,-1.5)` | наступление на открытом |
| Шеренга | `(-1.5,0) (0,0) (1.5,0)` | щитовая стена, оборона |
| Кольцо | по окружности радиуса R | охрана важного объекта |
| Свободный | слоты вокруг цели (§18.1) | бой |

### 20а.2 Правила, без которых формация ломается

- **Якорь — не лидер, а точка формации.** Если якорь — живой агент, его смерть разваливает строй; если это
  виртуальная точка, которую ведёт отряд, строй переживает потери.
- **Слот — цель навигации, а не позиция.** Агент идёт к слоту своим путём (обходя препятствия), а не
  телепортируется в офсет.
- **Переназначение слотов при потерях** (по приоритету): дыра в середине строя читается как баг.
- **Скорость формации = скорость самого медленного**, иначе строй растягивается; плюс масштаб скорости в
  колонне (§18.3).
- **Строй — не приоритет над выживанием:** попал под обстрел/в огонь — вышел из строя (роль «в укрытие»
  важнее роли «в строю»), потом вернулся.
- **Исключения из строя:** застрельщики, снайперы и турели в строй не входят вовсе (Days Gone явно исключает
  rusher'ов из линии фронта) — иначе один тип ломает всю картину.
- **Строй в 2D** вырождается в «кто впереди, кто сзади, кто на другой платформе» — это те же слоты, только
  офсеты одномерные плюс выбор платформы.

---

## 20б. Боссы: фазы, паттерны, арены

Босс — это не «враг с большим HP», а **сценарий**, который читается игроком и который он учится проходить.

### 20б.1 Фазы — состояния графа

```
Phase1  [enter: Health > 0.66]   умения: {SwipeA 1.0, Charge 0.5, Roar 0.2}
Phase2  [enter: Health <= 0.66]  умения: {SwipeA 0.8, Charge 1.0, Summon 0.6, Roar 0.2}
Phase3  [enter: Health <= 0.33]  умения: {Charge 1.2, Slam 1.0, Enrage 1.0}
                                 модификаторы: скорость ×1.15, телеграфы ×0.9
Transition (on phase change): неуязвимость, ролик/рёв, смена арены
```

- Фаза задаёт **свой набор умений и свои веса**, а не новый код.
- Переход между фазами — **явное состояние** с неуязвимостью и телеграфом: игрок должен понять, что правила
  изменились.
- Условие входа — здоровье, время, событие (разрушили элемент арены, умерли адды).

### 20б.2 Паттерны и анти-повтор

- **Выбор атаки — utility с анти-повтором** (§17): «не максимум, а взвешенный случайный из топ-N» + штраф за
  повтор. Чистый максимум делает босса предсказуемым за три попытки.
- **Служебные оси** `runtime` (сколько уже идёт бой) и `cooldown` — стандартные оси, а не отдельные условия
  (приём Guild Wars 2): так «сильная атака не в начале боя» описывается данными.
- **Гейт на сильные умения:** нельзя дважды подряд, нельзя раньше N секунд, нельзя одновременно с другой
  сильной.
- **Цепочки (комбо)** — последовательность с окнами: `SwipeA → SwipeB → Slam`, где каждый шаг имеет своё окно
  реакции. Описывается последовательностью задач, а не одним длинным умением.
- **Числа атак — из анимации, а не из головы:** FFXV прогоняла клипы по сферам вокруг монстра и извлекала
  дистанции и углы автоматически, иначе тюнинг не сходится с контентом.

### 20б.3 Арена и честность

- **Арена — часть босса:** безопасные зоны, предметы, разрушаемые элементы — они задают ритм не хуже умений.
- **Телеграф масштабируется опасностью** (§19.1): чем больнее, тем длиннее замах.
- **Не два телеграфа одновременно** — игрок не может читать два окна сразу.
- **Пауза после серии** (recovery) — окно, в которое игрок наносит урон; без неё бой превращается в
  уворачивание без награды.
- **Босс не должен «терять» игрока**: привязка к арене, возврат в центр, запрет на бесконечное преследование
  по кругу.
- **Адды и фазы:** вызванные враги — обычные агенты с ролями (§21), а не часть босса; иначе координация боя
  сломается.

### 20б.4 Соулс-лайк: как монстр выбирает приём

Добавлено 13.11.2026 по сбору из интернета (источники — [Research_AI_Sources §5.2](Research_AI_Sources.md)).
Официальных документов FromSoftware нет; устройство их ИИ известно по разборам и по Lua-скриптам, которые
открыли моддеры, — поэтому это **схема жанра**, а не точная копия чьего-то кода. Относится не только к боссам:
так же устроен любой рядовой враг соулс-лайка.

```
решить ──► выбрать приём ──► доиграть до конца ──► (комбо? шанс продолжить) ──► решить снова
              ▲                                                    
   реакции на игрока (лечится, зашёл за спину, бросил снаряд) — вне очереди
```

1. **Решение — только между действиями.** Начатый удар не прерывается и не подстраивается: его доигрывают,
   это и даёт игроку окно уклониться (§19.1). Новое решение — когда кончилось прошлое действие.
2. **Выбор — рулетка с весами по зонам дистанции.** У врага список приёмов; у каждого — где он уместен
   (дистанция, угол «спереди / сбоку / за спиной»), перезарядка и вес. Подходящие сейчас приёмы разыгрываются
   случайно пропорционально весу. Дистанция — главный признак; угол и здоровье — уточнения. Это тот же
   «взвешенный случайный» §20б.2, только отбор по дистанции стоит первым и делается жёстко.
3. **Анти-повтор — снижением веса.** Только что сделанный приём временно получает меньший вес; некоторые —
   «не чаще раза в N секунд».
4. **Комбо — цепочка с шансом продолжения.** Комбо — заранее заданная последовательность ударов, но после
   каждого шага враг с заданным шансом продолжает или обрывает её (а иногда выбирает одно из двух продолжений).
   Поэтому одно комбо бывает в два, три или четыре удара — игрок не может «заучить счёт» и обязан смотреть.
   Обрывать по внешним причинам (игрок вне досягаемости) — тоже нормально; посреди замаха — нет.
5. **Реакции поверх выбора (прерывания).** Отдельный слой: на событие от игрока (лечение, выстрел, атака в
   спину, промах) враг может ответить вне очереди — наказать лечение, развернуться, отпрыгнуть. Реакции тоже
   с шансом, иначе враг читерски идеален (§19.2, §19.4).
6. **Паузы — часть паттерна.** Между сериями — стрейф, хождение по кругу, отход: окно для ответа игрока
   (§20б.3). Без пауз бой — уворачивание без награды.
7. **Фазы — смена таблицы, а не кода** (§20б.1): на новой фазе другие веса и приёмы, рёв-телеграф перехода.

**Настройка — данными.** Всё, что дизайнер правит, — таблица: приём → зона дистанции, угол, вес, перезарядка;
комбо → шаги и шанс продолжения после каждого; реакции → событие, шанс, ответ. Числа дистанций — из анимаций
(§20б.2). Новое поведение врага — новая строка таблицы, а не новый код.

**Как это ложится на AIKit:** «что делать сейчас» (атаковать, кружить, отойти) — решения мозга с приоритетами;
**какой** удар и сколько ударов в серии — цепочка решения (счётчик на доске, `Branch` + `Compare`), как
договорено в шаге 69 (`AIKit_Flow`). Удар не прерывается (`AgentTask.CommitsFor`). Выбора удара случайно по
весам с зонами дистанции и комбо с шансом продолжения как готовых блоков в модуле пока **нет** — если понадобятся,
это новые блоки в C# (§37.2), а не логика на полотне.

---

# Часть V. Группы и мир

## 21. Координатор: директивная координация

> **Арбитраж централизован, исполнение децентрализовано. Агент никогда не договаривается с агентом.**

Любое решение «кто из нас» (кто сейчас бьёт, кто давит, кто обходит, кто идёт к турели) принимает **один
сервис**. Агент только спрашивает и исполняет. Прямых связей «агент ↔ агент» нет вообще.

Что это даёт: нет гонок и взаимных блокировок, поведение воспроизводимо по сиду, а «почему он не атакует»
отлаживается в одном окне, а не трассировкой N агентов.

### 21.1 Роли как слоты у цели

Роль — не строка на агенте, а **слот у цели** с ёмкостью от размера ростера:

```csharp
// N — сколько агентов в стычке с этой целью
int pressurers = Mathf.Clamp(Mathf.CeilToInt(N * 0.2f), 1, 3);    // давят в лицо
int flankers   = Mathf.Clamp(Mathf.RoundToInt(N * 0.4f), 0, 4);   // обходят
int circlers   = N - pressurers - flankers;                        // кружат
// чаффа — отдельный безлимитный пул
```

**Назначение событийное и детерминированное:** изменился ростер (пришёл, умер, сменил цель) — координатор
один раз пересобирает очередь по счёту и раздаёт слоты. Вариант «кто первый попросил, тот и занял» в шипнутом
проекте оброс семью слоями заплаток и живёт за флагом — тот же вывод, что в The Last of Us: **выбирать
идеального кандидата, а не первого попросившего**.

**Роли переназначаются при потерях** — иначе отряд развалится после первой смерти.

### 21.2 Выбор цели со счётом и анти-дребезгом

```csharp
float ScoreTarget(Agent agent, Target target) =>
      WeightDistance  * DistanceTerm(agent, target)       // нормировка ~30 м
    + WeightLoad      * LoadTerm(target)                  // сколько уже её бьют → размазывает по игрокам
    + WeightLowHp     * (1f - target.HealthFraction)
    + WeightRevenge   * ThreatMemory(agent, target)       // кто меня бил, полураспад ~6 с
    + (target == agent.CurrentTarget ? StickyBonus : 0f); // ×1.25 инкумбенту
```

Обязательный анти-дребезг:

| Приём | Число |
|---|---|
| минимальная выдержка между сменами цели | 2 с |
| порог переключения (новая должна быть лучше) | ×1.5, а не на эпсилон |
| внеплановый пересчёт только по заметному событию | цель сместилась на 6 м или повернулась на 75° |
| мягкий опрос по таймеру | раз в 3.5 с |

Без этого агенты «переобуваются» каждый тик и выглядят слабоумными.

### 21.3 Группы подхода

Бежать к цели всей толпой нельзя: координатор ведёт группу по общему маршруту и **масштабирует скорость
внутри колонны**, чтобы строй не телескопировал. Это дешёвая замена «очередям на узких местах».

### 21.4 Как не превратить координатор в God Object

В шипнутом проекте координатор разросся до 8335 строк в одном файле, и внутрь просочилась доменная логика
оружия (итерация по турелям, чтение состояния седла). Делить **сразу**, по осям:

| Часть | Решает | Общее у всех |
|---|---|---|
| `RoleAssignment` | кто давит, обходит, кружит | реестр агентов |
| `TargetSelection` | кого бьёт каждый | и список стычек |
| `AttackTokens` | кому можно бить сейчас (§18) | |
| `ApproachGroups` | как толпа идёт к цели | |
| `StationAssignment` | кто занимает турель/станцию (§24) | |

Доменная логика (оружие, станции) — **за провайдером** (`IGunnerStation`: позиция, дуга, доступность, оценка
выстрела), координатору остаются счёт и лимиты.

---

## 22. Намерения: недирективная координация

Между эмерджентностью и командиром есть рабочая середина: **матчмейкинг намерений**. Никто никем не
командует — агент, которому для дела нужны товарищи, заявляет намерение, а система собирает из заявок отряд.

```csharp
public abstract class Intent
{
    public int MinMembers { get; }            // ≥ 2: меньше — нет смысла в отряде
    public int MaxMembers { get; }
    public float PendingTime { get; }         // таймаут сборки, 1.5–2 с

    public abstract bool SameAs(Intent other);                        // обычно «та же цель»
    public abstract void CreateRoles(IReadOnlyList<Agent> members, List<ISquadRole> roles);
    public virtual bool StillValid(Squad squad) => true;              // цель жива и не ушла
    public virtual void CreateSharedData(Blackboard squadBlackboard) { }
}
```

Порядок работы:

1. Агент **без роли** раз в ~2 с заявляет намерение: `matchmaker.Join(agent, new AttackIntent(target))`.
2. Матчмейкер подсаживает его в **активный** отряд с таким же намерением; если некуда — в **ожидающий**;
   если и такого нет — открывает новый ожидающий.
3. Ожидающий отряд **набрался до максимума** → активируется сразу; **истёк таймаут** с составом ≥ минимума →
   активируется; иначе распускается без последствий.
4. Активный отряд создаёт роли под состав, тикает их, выкидывает участника, чья роль перестала быть
   актуальной, и умирает, когда состав упал ниже минимума или намерение стало недействительным.
5. **Состав изменился — роли пересобираются** под новый состав.

Равенство намерений по цели → все, кто выбрал одну жертву, собираются в один отряд сами.

### 22.1 Роль — это подмена мозга, а не приказ

```csharp
public abstract class SquadRole : ISquadRole
{
    public virtual void OnStart(Agent member, Squad squad)
    {
        member.Blackboard.Set(Keys.Role, RoleName);
        member.Brain.OverrideSubgraph(GetSubgraph(member, squad));   // ← роль = поддерево/домен
        foreach (ISensor sensor in GetRoleSensors(member, squad))     // ← и свои сенсоры
            member.Perception.AddSensor(sensor);
    }

    public virtual void OnStop(Agent member, Squad squad)
    {
        member.Blackboard.Set(Keys.Role, string.Empty);
        member.Brain.RollbackSubgraph();
        member.Perception.RemoveRoleSensors();
    }

    public virtual void OnUpdate(Agent member, Squad squad, float dt) { }

    protected abstract IBehaviourSubgraph GetSubgraph(Agent member, Squad squad);
}
```

Роль поддержки в `OnUpdate` пишет участнику то, что знает только отряд: «самый раненый союзник», «у кого
кончились патроны». То есть роль = **поддерево + сенсоры + данные от отряда**, а не строка `Order = Flank`,
которую агент должен как-то интерпретировать.

### 22.2 Какой режим когда

| | Координатор (§21) | Намерения (§22) |
|---|---|---|
| Кто решает | один сервис на мир | заявки агентов |
| Даёт | гарантированную дизайнером картину боя | дешёвую кооперацию без центра |
| Платим | единой точкой отказа, риском God Object | поведение отряда не гарантировано |
| Когда | бой с игроком, где важны честность и ритм | ситуативные стычки, «двое пошли на одну цель» |

Оба режима пользуются одними ролями и одними ресурсами на цели. Выбор — свойство архетипа.

> **Грабли из живого кода.** Роли и участники там лежат двумя параллельными списками с выравниванием по
> индексу, а удаление идёт прямо в прямом цикле `for (i++)` — следующий участник пропускается. Роль хранить
> **рядом с участником**, удаление — обратным проходом или через буфер.

---

## 23. Карты влияния и выбор позиции

### 23.1 Принцип

> **Карта влияния не даёт команд. Она даёт информацию, которой пользуются структуры принятия решений.**
> (Dave Mark, «Modular Tactical Influence Maps»)

Несколько слоёв: своё влияние, влияние врага, недавние смерти, опасные зоны, видимость. Каждый слой
«штампуется» агентами и событиями, затухает со временем и распространяется по сетке.

### 23.2 Реализация: сетка + штампы + джобы

```csharp
public partial struct InfluenceMap2D : IDisposable
{
    private NativeArray<float> _matrix;            // row-major: [x * sizeY + y]
    public float this[int x, int y] { /* … */ }
}

// затухание — делегат для управляемого пути
public delegate float Propagation(float maxValue, float distance, float maxDistance);

public static readonly Propagation Linear = (max, d, maxD) =>
    maxD == 0 ? max : max * (1f - d / maxD);

public static Propagation Polynomial(float exponent = 2) => (max, d, maxD) =>
    maxD == 0 ? max : max * Mathf.Pow(1f - d / maxD, exponent);
```

Приёмы:

- **Шаблоны-штампы** вместо пересчёта: маленькая заранее посчитанная карта «след угрозы» добавляется со
  смещением и множителем. В разы дешевле, чем считать затухание от каждого агента.
- **У каждой операции две версии**: обычная и `…AsJob(JobHandle depends)` на `IJobParallelFor` с
  `[BurstCompile]`. Кадр собирается в цепочку (очистить → наштамповать N источников → нормализовать) и
  синхронизируется один раз.
- Burst-джоб **не умеет держать делегат**, поэтому формула затухания в джобе дублируется статиком — это
  видно в имени метода (`AddLinearCircleInfluenceAsJob`). Либо параметризовать числами, либо генерировать
  джоб на кривую.
- **Нормализация перед смешиванием** слоёв, `Inverse` для «опасность = 1 − безопасность».

### 23.3 Запросы, из которых получается тактика

Не «карта», а ответ на вопрос:

| Запрос | Вопрос |
|---|---|
| `MaxPoint()` | где вообще лучше всего |
| `MaxRadiusPoint(center, radius)` | **куда идти рядом со мной** |
| `MinSectorPoint(center, direction, angle, maxDistance)` | **куда отходить в эту сторону** |
| `SectorPointsLower(threshold)` | все безопасные точки в секторе |

### 23.4 Запрос позиции (аналог Unreal EQS)

```csharp
public bool TryFindPosition(PositionQuery query, out Vector3 position)
{
    position = default;
    float best = float.MinValue;

    foreach (Vector3 candidate in query.Candidates)       // узлы графа / слоты вокруг цели / клетки
    {
        if (!query.PassesFilters(candidate)) continue;     // видимость, дистанции, занятость, опасность

        float score = query.Score(candidate);              // utility: ближе к цели, дальше от союзников…
        if (score > best) { best = score; position = candidate; }
    }

    return best > float.MinValue;
}
```

- Кандидаты в 2D-платформере — **узлы графа навигации**: «узлы в радиусе, с линией к цели, не занятые
  союзником». Дёшево и очень заметно улучшает картинку боя.
- У запроса должны быть **fallback-опции** (Crysis 2): «идеально → приемлемо → хоть что-то». Запрос без
  fallback даёт агента, который стоит на месте, потому что идеальной точки нет.
- Запрос — не каждый кадр: по событию (потерял линию, получил роль, сменил цель) или раз в 0.5–2 с.

---

## 24. Объекты уровня: один шлюз вместо сканирования

**Задача никогда не сканирует мир.** Нужен объект (турель, укрытие, рычаг, станция починки, аптечка) —
через реестр:

```csharp
public interface IInteractableRegistry
{
    bool TryFindNearestAvailable(PurposeTag purpose, Vector3 from, Agent requester, out IInteractable result);
    bool TryBook(IInteractable target, Agent holder, float seconds);
    void Release(IInteractable target, Agent holder);

    void ReportUnreachable(IInteractable target, Agent agent);       // журнал недостижимости
    bool IsUnreachableFor(IInteractable target, Agent agent);
}
```

Правила:

- **Объект регистрируется сам** на старте; `FindObjectsOfType` в тике узла запрещён.
- **Бронь — слой эффективности, а не истина.** Бронировать нужно, чтобы трое не пошли к одной турели, но
  право на взаимодействие проверяет сам объект в момент использования. У брони TTL, её чистит сервис, смерть
  агента снимает её принудительно.
- **Журнал недостижимости:** не смог дойти — объект помечен недостижимым **для этого агента** на время.
  Иначе агент ходит к нему по кругу всю жизнь.
- **Цепочка контекста** с фиксированным порядком: `авторский список на записи спавна → зона → без
  ограничений`. Непустой авторский список **никогда не проваливается** в «поищу что-нибудь рядом»: если
  дизайнер назначил конкретные точки, а они заняты, агент ждёт, а не уходит фрилансить.

### 24.1 Умные объекты (Sims-модель)

Логика переносится из агента в **объекты мира**: холодильник «рекламирует» еду, кровать — сон, турель —
«стрелять дальше». Агент берёт ближайшую сильнейшую рекламу, взвешенную его нуждами и расстоянием.

```csharp
public readonly struct Advertisement
{
    public readonly PurposeTag Purpose;
    public readonly float Strength;       // сила предложения
    public readonly Vector3 EntryPoint;   // где встать, чтобы воспользоваться
}
```

Unreal Smart Objects добавляют к этому **слоты** со состояниями `Claimed/Occupied`, валидацию точки входа по
навигации и поведение слота. Для нас это способ дать дизайнеру наполнять уровень поведением **без кода**:
поставил объект с слотами — агенты начали им пользоваться, потому что в графе есть узлы
`FindSmartObject(type)` / `UseSmartObject`.

---

## 25. Директор и сложность

### 25.1 Директор управляет темпом, а не знанием

Главный принцип (Left 4 Dead, Alien: Isolation): **директор не подсказывает агентам, где игрок.** Он крутит
числа слоёв под собой.

| Что крутит директор | Пример |
|---|---|
| сколько врагов живо одновременно | лимит населения |
| как часто спавнить | задержка волны, респаун |
| ёмкости атаки | множитель на `AttackCapacity` цели |
| состав волны | таблица по интенсивности |
| паузы | «период покоя» после пика |

В Alien: Isolation у чужого **два мозга**: один знает, где игрок, и подсказывает «ищи в этом районе», второй
— сам чужой, который честно ищет. Менеджер напряжения не даёт первому давать точную позицию. Это и есть
«директор управляет темпом, а не знанием».

**Интенсивность считать от участия с усреднением по отряду** — иначе один герой разгоняет директора на всю
группу. Известный пробел шипнутой реализации: у поддержки участие не считается вовсе (хил без прямого
контакта «не участвует»).

### 25.2 Прибытие и бюджет создания

- **Режимы прибытия:** пешком из двери, «дымом» на месте, как выйдет. Первое честнее, второе быстрее.
- **Формирование:** агент существует и держит слот в кольце с момента спавна, но ещё скрыт (разброс ~1.5 с) —
  волна «проявляется», а не «телепортируется».
- **Передислокация вместо спавна:** живых перебрасывают, если они далеко и их не видно.
- **Общий бюджет создания на мир:** одна очередь, N попыток на кадр (4), round-robin между спавнерами, плюс
  случайная задержка выхода — волна **выходит струйкой**, а не пачкой. Это одновременно и производительность,
  и картинка.

### 25.3 Цепочка приоритетов сложности

Четыре независимых слоя правят одно и то же число. Порядок фиксируется **намеренно и записывается**:

```
архетип (ролсет, профиль роли)   — авторская база
 → профиль сложности             — глобальный пресет
  → оверрайд спавнера/сущности   — на этого врага и эту стычку
   → оверрайд стычки             — временный, по месту
    → множитель директора        — поверх полученного
     → отладочный форс           — всегда побеждает
```

Ценность не в самой цепочке, а в том, что она **записана одной лесенкой**: на вопрос «почему бой слишком
лёгкий» отвечают проходом по ней в этом порядке, а не чтением кода.

Известное ограничение, которое честнее назвать заранее: сложность обычно **глобальная**. «Элита среди чаффы»
или «отряд на кошмаре внутри среднего боя» требуют per-NPC множителей — это проектируется отдельно.

---

## 25а. Энкаунтеры: спавнеры, волны, триггеры

Это поверхность, которую дизайнер трогает каждый день. Чем она беднее, тем больше «скриптов на уровне».

### 25а.1 Спавнер и режимы

| Режим | Поведение |
|---|---|
| `OnBeginPlay` | выпустить состав один раз при старте уровня |
| `OnPlayerTrigger` | выпустить при входе игрока в зону |
| `MaintainAlive` | держать N живых, добавляя замену через задержку |
| `OnKillThreshold` | новая волна каждые K смертей |
| `OnActivation` | старт по событию (консоль, рычаг, квест) |
| `Manual` | только по вызову из скрипта/квеста |

Плюс флаг «отложить первую волну до входа игрока» — иначе враги «проявляются» на глазах.

### 25а.2 Волны и состав

```csharp
[Serializable]
public sealed class Wave
{
    public SpawnEntry[] Entries;        // кого и сколько
    public int ReserveCount;            // подкрепления ПО МЕРЕ СМЕРТЕЙ
    public float StartDelaySeconds;
    public ArrivalMode Arrival;         // пешком / «дымом» / как выйдет
}

[Serializable]
public sealed class SpawnEntry
{
    public AgentArchetype Archetype;
    public int Count;
    public bool UseForRefill;           // кем пополнять потери
    public SpawnDoor Door;              // откуда выходят
    public Transform[] PatrolRoute;     // чем заняты до боя
}
```

- **Резерв важнее размера волны:** `ReserveCount` подаёт замену **по мере смертей**, поэтому волна может быть
  большой в сумме и никогда не быть большой на экране.
- **Пополнение по кольцу** (deterministic round-robin по записям, курсор сохраняется между запросами) — иначе
  «подкрепления» всегда одного типа.
- **Разрешение прибытия — цепочкой:** запись → волна → спавнер → архетип (каждый уровень может сказать
  «наследовать»).
- **Двери (spawn closets)** — размещённые точки выхода с тегами («только мелочь», «только тяжёлые»):
  распределение `Split` / `Random` / `NearestToTarget`, минимальная дистанция до игрока, выбор по
  «дистанция + конус обзора + фланг», и **запрет спавна в поле зрения** (ждать скрытую точку).
- **Группы триггеров:** несколько спавнеров с одним именем = один энкаунтер с несколькими входами; сработал
  один — остальные разоружаются. Плюс перезарядка триггера, чтобы повторный визит давал другую засаду.
- **Бюджет на мир, а не на спавнер** (§25.2): общая очередь, N попыток на кадр, случайная задержка выхода.

### 25а.3 Цели и завершение

- **Объектив** = группа спавнеров + зона присутствия + счётчик живых: «зачистить лагерь» показывается игроку
  одним числом.
- **Награда на всю группу** — отдельный интерфейс, вызываемый **на авторитете** один раз.
- **Перезапуск энкаунтера** после полной зачистки (через N секунд) — отдельная настройка, не путать с
  «держать N живых».
- **Лимит на энкаунтер** (`MaxTotalSpawned`) — предохранитель от бесконечного спавна при баге условия.

---

## 25б. Правила аггро и привязка к дому

Самый частый баг энкаунтера — «вся карта побежала на выстрел». Правила входа в бой должны быть **явными
режимами**, а не следствием настроек восприятия.

| Режим | Когда агент входит в бой |
|---|---|
| `OnSpawn` | сразу (арена, волны) |
| `OnPerception` | только когда сам увидел/услышал (патрули, стелс) |
| `OnZonePresence` | игрок вошёл в зону — вся зона в бою (комнатные арены) |
| `OnReinforcement` | подкрепление входит в бой **против уже известной цели**, не ожидая своего восприятия |
| `OptOut` | никогда (постановочные дурачки для тестов, мирные NPC) |

Тонкость из шипнутой практики: при `OnReinforcement` надо **проштамповать «цель видели сейчас»**, иначе
подкрепление, которое никого не увидело, через несколько секунд решит, что потеряло цель, и уйдёт домой.

### 25б.1 Привязка к дому (leash)

```csharp
[Serializable]
public sealed class LeashConfig
{
    public float LostSightSeconds = 8f;    // сколько гонимся после потери
    public float ReturnDistance = 30f;     // дальше этого от дома — возвращаемся
    public bool LeashToZone = true;        // зона вместо радиуса
    public float ZoneMargin = 3f;
}
```

- Привязка **опциональна**: у агента без дома нет ветки возврата вовсе (арена, босс).
- **Достижимость дома проверяется и кешируется**, но с повторной проверкой: агент, заспавненный до готовности
  навигации, не должен навсегда остаться «без дома».
- Если зона недостижима — падать на точку дома, а не пытаться дойти бесконечно.
- **Запас на преследование по виду:** пока агент **видит** цель, он гонится за неё дальше радиуса возврата
  (иначе разрыв поводка выглядит как «врагу стало скучно»).
- Возврат — отдельное состояние с собственным поведением (идти, а не бежать; не атаковать по пути).

---

# Часть VI. Толпа

## 26. Толпа — отдельная подсистема, а не «много агентов»

Признак, что нужна толпа: больше ~100 сущностей, у которых поведение проще, чем у врага, и которые идут
примерно туда же.

```
CrowdSystem
├── FlowField       одна волна Dijkstra на цель → направление в каждой клетке
├── SpatialHash     соседи для расталкивания
├── Boids           Separation / Alignment / Cohesion по соседям
├── LOD             полный агент → упрощённый → только позиция
└── Budget          time slicing: N юнитов за кадр
```

### 26.1 Поле потока

```csharp
public struct FlowCell
{
    public ushort Cost;    // в единицах BFS; 0 = цель, 0xFFFF = недостижимо
    public byte Dir;       // НЕ 8 сторон: 240 единиц угла (1.5°) — хватает на плавность
    public byte Flags;     // Walkable | Goal | Blocked | Portal
}
```

- **Многоисточниковый BFS/Dijkstra**: каждая цель сеет стоимость 0, и клетка указывает на **ближайшую** цель.
- Шаги: ортогональный 10, диагональный 14 (целые числа — быстро и без накопления ошибки).
- **Выборка — билинейная по 4 соседям**, а не «взять направление клетки»: иначе юниты движутся ступеньками.
  У целевых клеток поток нулевой, чтобы на подходе не наматывать круги.
- **Правила, вшитые в BFS:** гейт по уступу (нет рёбер через перепад выше порога), **штраф у края** (кайма в
  1 клетку), чтобы толпа не выдавливала юнитов с обрыва, штраф на зону чужой стычки, чтобы потоки обтекали
  друг друга.
- **Плотность в стоимость** (§14.2) — вместо расталкивания постфактум.
- Перестройка 5–10 Гц, двойная буферизация, перестройка только при смещении цели больше порога.

### 26.2 Лёгкий юнит и повышение

```csharp
public struct CrowdUnit                  // НЕ MonoBehaviour: плоский массив
{
    public Vector2 Position, Velocity;
    public int CellIndex;
    public byte Lod;
    public byte Flags;
}
```

- Визуал — пул спрайтов/мешей или `Graphics.DrawMeshInstanced`, а не объект на юнита.
- **Повышение/понижение:** юнит, оказавшийся рядом с игроком, становится полноценным агентом (с графом,
  восприятием, боем) и обратно при удалении. Игрок взаимодействует только с «повышенными».
- API **«jobs-ready»**: данные в плоских массивах, функции без состояния — перевод на джобы/ECS возможен
  позже, но не обязателен сразу.

### 26.3 Числа

| Масштаб | Что хватает |
|---|---|
| до 50 | обычные агенты с расписанием (§27) |
| 50–300 | упрощённые агенты + spatial hash + separation |
| 300–2000 | поле потока + плоские данные + LOD + time slicing |
| 2000+ | джобы/ECS — отдельная тема |

---

## 26а. Транспорт, летающие, водные

Все три случая — про то, что **актуатор другой, а мозг тот же**. Если граф, восприятие и бой не знают о типе
тела (§0.1), новый носитель — это новый адаптер, а не новый ИИ.

### 26а.1 Транспорт (машины, мотоциклы)

```csharp
public interface IVehicleActuator : IAgentActuator
{
    void Throttle(float value01);     // газ
    void Brake(float value01);
    void Steer(float value);          // −1…1
    void Handbrake(bool on);
}
```

- **Два уровня, как в гонках:** низкий — регуляторы (руль и газ ведут машину по целевой точке, обычно PID),
  высокий — поведение (держать траекторию, обгонять, таранить, отходить). Смешивать нельзя: «ИИ, который
  рулит из графа», не едет.
- **Координаты — трасса/маршрут, а не мир** (§13а.4): «смещение от линии» и «насколько тормозить» — две
  величины, в которых задача становится одномерной.
- **Предсказание вперёд обязательно:** решение принимается по точке в 1–3 с впереди по текущей скорости, иначе
  машина всегда реагирует поздно.
- **Границы тела:** радиус поворота, скорость торможения, занос — часть `AgentBodyInfo`; граф не должен знать
  этих чисел, но навигатор обязан (ребро «разворот на месте» для машины непроходимо).
- Для нашего проекта (мотоцикл `BikeBody`) это означает: `BikeActuator` поверх существующего ввода мотоцикла,
  и тот же граф «патруль → догнать → атаковать».

### 26а.2 Летающие

- **Поиск пути обычно не нужен.** Рабочая схема: непрерывный steering + **модель высоты** (держать коридор над
  землёй) + кешированные зонды (вперёд-вверх «перелететь», вниз «земля», вверх «потолок») с частотой ~10 Гц.
- **Цели вместо путей:** `FlyTo(point)`, `Orbit(target, radius, height)`, `Dive(target)`, `Launch(altitude)`.
- **Летать на полной частоте**, даже когда мозг думает реже: пульсирующее движение летуна заметно сразу
  (поэтому у летающих steering не подчиняется LOD-троттлингу).
- **Сторож застревания** обязателен: концавная геометрия ловит летуна насмерть — таймер «толкаюсь в стену
  N секунд» → подняться выше / сменить цель.
- **Высота — ресурс:** набор высоты на спавне (до 20–30 м), пикирование как атака, возврат на высоту после.

### 26а.3 Водные и многоуровневые пространства

- **Отдельная грубая сетка** по воде (проходимость = глубина и клиренс), A* по ней, детур-точки — иначе
  реактивные «усы» зацикливаются на вогнутых берегах.
- **Многоуровневое пространство** (палубы, этажи) — это не 3D-навмеш, а **набор уровней с портелами**: на
  каждую XY-клетку не больше одной опоры, уровни связаны переходами (`Walk` / `Drop` / `Jump`) с направлением.
  Разрешение уровня по позиции агента должно быть **без состояния** (пересчитывается от его высоты), иначе
  агент, сбитый с лестницы, остаётся «на том уровне» навсегда.
- **Движущаяся платформа (корабль, лифт)** — локальные координаты: путь считается в системе платформы и
  пересчитывается в мир каждый тик, иначе агент идёт туда, где палуба была.
- **Падение за борт** — явное правило, а не физика: агент, оказавшийся в воде (а он туда «законно» попасть не
  может), либо гибнет, либо телепортируется на палубу; иначе он будет вечно плыть к недостижимой цели.

---

# Часть VII. Производительность

## 27. Расписание и LOD

Бюджет ИИ на кадр при 60 fps — **2–4 мс**. Увеличение интервала «думания» почти не заметно игроку, но даёт
кратную экономию.

### 27.1 Round-robin с бюджетом

```csharp
public sealed class ThinkScheduler
{
    private readonly List<AgentBrain> _agents = new();
    private int _cursor;
    public float BudgetMilliseconds = 3f;

    public void Tick(float dt)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int count = _agents.Count;

        for (int i = 0; i < count; i++)
        {
            AgentBrain brain = _agents[(_cursor + i) % count];
            brain.Accumulate(dt);                      // таймеры идут у ВСЕХ каждый шаг
            if (!brain.WantsToThink) continue;

            brain.Think();                             // решение — только у тех, чья очередь
            if (watch.Elapsed.TotalMilliseconds > BudgetMilliseconds)
            {
                _cursor = (_cursor + i + 1) % count;   // остальные додумают на следующем шаге
                return;
            }
        }

        _cursor = 0;
    }
}
```

Ключевое: **таймеры тикают у всех, думают по очереди**. Иначе агент, до которого не дошла очередь, «замирает»
в своих кулдаунах.

### 27.2 Ярусы LOD и сон

Трасса шипнутого сервера на 77 ботах: в боевом движении реально **6.7 бота на кадр**, ещё 36.9 просто держат
путь — а полную цену платят все 77. Отсюда:

| Ярус | Когда | Частота решений |
|---|---|---|
| `Active` | в бою или близко к камере | полная (10–20 Гц) |
| `Near` | **есть активный путь** или средняя дистанция | 0.1 с |
| `Idle` | ни боя, ни пути, далеко | 0.25 с |
| `Asleep` | дальше порога сна, не в бою | **не думает вовсе** |

- Критерий «есть активный путь» важнее дистанции: далёкий идущий агент должен идти ровно.
- **Сон — это ноль, а не «реже»:** мозг на паузе, сенсоры выключены, подписка на тик снята, активный запрос
  движения отменён — но сущность и состояние живы.
- **Пробуждение с гистерезисом** (порог пробуждения ближе порога сна) и по событию: урон, сигнал, приказ.
- «Глушить анимацию и физику» — отдельный флажок, **выключенный по умолчанию**: это меняет качество картинки.
- **Порог дистанции не держит бюджет.** Когда NPC сбиваются в кучу вокруг игрока, LOD по дистанции вылетает
  за бюджет. Решение из «LOD Trader» (Game AI Pro): считать LOD **задачей о рюкзаке** — у агента есть
  «вероятность, что игрок заметит глупость», у уровня детализации — наглость и стоимость; жадно берём самые
  ценные апгрейды, пока не перерасходовали, затем самые выгодные даунгрейды. Работает за десятки микросекунд.

### 27.3 Расфазировка

Случайный период вместо константы (§9.6) + фазовый сдвиг при регистрации агента. Без этого 50 агентов с
периодом 0.2 с думают в одном кадре каждые 12 кадров — и это видно как пила на графике.

## 28. Аллокации и форма API

| Правило | Как |
|---|---|
| Ноль аллокаций в тике | узлы — заранее созданные объекты, статусы — `enum`, никаких LINQ и лямбд в горячем пути |
| Строки — только в отладке | имена операторов через `nameof`, ключи — числа/типизированные ключи |
| Буферные API по умолчанию | `bool FindPath(…, T[] buffer, out int size)`, `bool Decide(string[] plan, out int size)` |
| Удобные перегрузки — поверх | внутри берут буфер из `ArrayPool<T>.Shared` и копируют результат |
| Коллекции поиска — из пулов | `DictionaryPool`, `HashSetPool`, `ListPool` (Unity Collections Pool) |
| Небольшие буферы — на стеке | `Span<T> x = stackalloc T[8]` для соседей, счётов, кандидатов |
| Физические запросы | `NonAlloc`-версии, маски вместо фильтрации после |
| Безопасная итерация | перед обходом списка слушателей — копия (поведение может отписаться в тике) |

```csharp
// типовая пара: основной метод без аллокаций + удобная обёртка
public NavPoint[] FindPath(in NavPoint start, in NavPoint end, Predicate<NavNode> passable = null)
{
    NavPoint[] buffer = ArrayPool<NavPoint>.Shared.Rent(MaxPathSize);
    FindPath(start, end, buffer, out int size, passable);

    var result = new NavPoint[size];
    Array.Copy(buffer, result, size);
    ArrayPool<NavPoint>.Shared.Return(buffer);
    return result;
}
```

## 29. Детерминизм

Это не академическая чистота, а **условие отладки и тестируемости**.

- **Время — только параметром `dt`.** Никаких `Time.time`, `Time.deltaTime` внутри ИИ, никаких корутин по
  времени, `Invoke`, твинов (они идут и в паузе).
- **Случайность — только из сида агента** (`System.Random` на агента, сид от сида мира). `UnityEngine.Random`
  в логике ИИ запрещён: он глобальный, его дёргает весь проект, воспроизвести нельзя.
- **Стабильный порядок обхода:** не зависеть от порядка `GetComponent`, от порядка словаря, от порядка
  регистрации. Одномоментные заявки — ранжировать, а не брать «кто первый в списке» (§18.2).
- **Одна сущность = один сид = одна трасса.** Тест «один сид → одна трасса решений» ловит нарушения этого
  раздела лучше, чем ревью.

## 29а. Сохранение и восстановление состояния ИИ

Тема, которую обычно забывают до первого бага «загрузился — враг стоит столбом».

### 29а.1 Что сохранять, а что пересчитывать

| Сохранять | Пересчитывать при загрузке |
|---|---|
| жив / мёртв, здоровье, инвентарь | путь и текущее ребро |
| позиция, поворот, скорость | факты восприятия (сенсоры отработают сами) |
| ключ активного состояния графа (и ветки, если иерархия) | занятые жетоны, слоты, брони |
| прогресс маршрута (индекс точки) | роль в отряде и состав отряда |
| кулдауны умений, таймеры состояний | трек внимания (или сохранить «уровень тревоги» одним числом) |
| последняя известная позиция цели, уровень тревоги | ссылки на цель — **по id, не по ссылке** |
| сид случайности агента | кеши, буферы, временные списки |

Правило: **сохранять намерения и прогресс, а не вычислимое.** Путь, слоты, роли и соседи восстанавливаются за
один-два тика; если их сохранить, они почти наверняка окажутся несогласованными с загруженным миром.

### 29а.2 Правила восстановления

```csharp
// порядок, который не ломается
// 1) загрузить агентов (позиции, здоровье, кулдауны, сид, ключ состояния)
// 2) загрузить мир (двери, объекты, укрытия) и реестры
// 3) отпустить все ресурсы: жетоны, слоты, брони — они не сохранялись
// 4) войти в сохранённое состояние графа через OnEnter (а не «продолжить с середины тика»)
// 5) первый think-тик отработает сенсорами и доберёт всё остальное
```

- **Вход в состояние через `OnEnter`**, а не восстановление внутренностей задач: задача получает корректный
  вход, навигация строит путь заново, анимация стартует с начала клипа.
- **Ссылки только по идентификаторам** (`SaveableId`, не `Transform`): цель могла не сохраниться или
  перегенерироваться.
- **Сид сохранять обязательно** — иначе после загрузки поведение разойдётся с тем, что было, и баг-репорт «по
  сейву» перестанет воспроизводиться.
- **Координатор/отряды не сохранять:** они пересобираются из живых агентов. Сохранённая стычка с мёртвыми
  участниками — источник неубиваемых баг-репортов.
- **Проверить загрузку в бою**, а не только «сохранился у костра»: половина багов именно там (агент в фазе
  замаха, в прыжке, на ребре, с бронью на станции).

### 29а.3 Версии и миграции

Формат данных агента меняется так же часто, как сам ИИ: номер версии в блоке данных + шаг апгрейда
(«не было поля `AlertLevel` — считаем 0»). Старые сейвы ломать нельзя, а молчаливое игнорирование
неизвестных полей — ещё хуже: лучше явная миграция и запись в лог.

### 29а.4 Пауза — не сохранение, но рядом

ИИ не должен жить на `Update`/`Time.time`: тогда пауза — это «цикл не раздаёт тики», и ничего специального для
ИИ делать не надо. Если же в проекте есть `Time.timeScale = 0`, ИИ на корутинах и `Invoke` сломается
непредсказуемо — см. §29.

---

## 30. Профилирование

- **Маркеры по фазам, а не «ИИ дорогой»:** отдельно восприятие, отдельно решение, отдельно навигация,
  отдельно исполнение; в толпе — отдельно построение поля (игровой поток) и BFS (воркер).
- **Образец на класс узла** (`ProfilerMarker` по имени типа, под `ENABLE_PROFILER`) — видно, какой узел
  дорогой, а не «граф дорогой».
- Счётчики, которые стоит выводить на экран в отладке: агентов всего / думает за кадр / в бою / спит,
  мс на решения, число перестроений поля, аллокации за кадр.
- **Дымовой перф-тест в CI:** N агентов × M шагов укладываются в бюджет (относительно, без жёстких мс —
  железо разное).

---
## 30а. Джобы и многопоточность

Правило простое: **в джобы уходит массовое и однородное, а не граф решений.**

| Годится в джобы | Почему |
|---|---|
| построение поля потока (BFS/Dijkstra по сетке) | чистые данные, нет Unity-объектов |
| карты влияния: штампы, нормализация, поиск максимума | `NativeArray<float>` + `IJobParallelFor` |
| boids/расталкивание толпы | плоские массивы позиций и скоростей |
| пакетная проверка видимости | `RaycastCommand` / `BoxcastCommand` — пакет лучей за один `Schedule` |
| пакетный поиск пути по навмешу | `NavMeshQuery` **можно** внутри джоба (в отличие от `NavMesh.*`) |
| предподсчёт дистанций/сортировок кандидатов | математика без побочных эффектов |

| Не годится | Почему |
|---|---|
| граф решений, узлы, blackboard | ветвления, ссылки на объекты, побочные эффекты |
| вызовы Unity API (компоненты, `Physics.Raycast`, анимация) | только главный поток |
| планировщик с делегатами (`Func<bool> isValid`, `cost`) | управляемые делегаты в Burst-джоб не положить |

### 30а.1 Как это выглядит на практике

```csharp
// 1) сбор данных на главном потоке (дёшево, без аллокаций — буферы переиспользуются)
for (int i = 0; i < _agents.Count; i++)
    _origins[i] = _agents[i].EyePosition;

// 2) пакет лучей одним Schedule
var commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
var results  = new NativeArray<RaycastHit>(count, Allocator.TempJob);
// … заполнить commands …
JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, minCommandsPerJob: 32);

// 3) дождаться и применить на главном потоке
handle.Complete();
for (int i = 0; i < count; i++)
    _agents[i].Perception.ApplyVisibility(results[i].collider == null);

commands.Dispose();
results.Dispose();
```

### 30а.2 Правила

- **Цепочка зависимостей вместо синхронизаций:** `Clear → Stamp×N → Normalize` планируются с `depends` и
  синхронизируются **один раз** за кадр.
- **`[BurstCompile]` + `in`/`out`-параметры** у статических функций, `[ReadOnly]`/`[WriteOnly]` на массивах:
  без этого Burst не даёт выигрыша и ругается на алиасинг.
- **Делегат в джоб не передать** — либо параметризовать числами, либо генерировать джоб на каждую формулу
  (отсюда имена вида `AddLinearCircleInfluenceAsJob`).
- **Планировать в начале кадра, забирать в конце** — иначе `Complete()` сразу после `Schedule()` превращает
  джоб в синхронный вызов с накладными расходами.
- **Результат применяет главный поток.** Джоб считает, решения принимает мозг — так остаётся детерминизм и
  отладка.
- **Сначала измерить.** На 50 агентах джобы почти наверняка не нужны: выигрыш съедят сбор данных и
  синхронизация. Джобы — ответ на профайлер, а не на предчувствие.

---

# Часть VIII. Инструменты, авторинг, отладка

## 31. Редакторы графов в Unity

| Технология | Статус | Когда брать |
|---|---|---|
| **GraphView** (`UnityEditor.Experimental.GraphView`) | «experimental» с 2019, но на нём сделаны все известные редакторы (Shader Graph, VFX Graph, Unity Behavior, NodeCanvas) | сейчас — рабочий выбор |
| **Graph Toolkit** (`com.unity.graphtoolkit`) | официальная замена, с 6.2, API ещё меняется | закладывать слой абстракции и переезжать позже |
| IMGUI руками | полный контроль, много работы | мелкие инспекторы, не граф |

**Слой абстракции обязателен:** редактор работает не с `GraphView`-узлами напрямую, а с моделью графа
(узлы, рёбра, позиции, выделение). Тогда переезд на Graph Toolkit — это переписать слой отрисовки, а не
редактор.

### 31.1 Сериализация графа

```csharp
[CreateAssetMenu(menuName = "AI Kit/Behavior Graph")]
public sealed class BehaviorGraphAsset : ScriptableObject
{
    [SerializeReference] private List<StateData> _states;        // плоский список, parent по индексу
    [SerializeField] private List<VariableData> _variables;      // объявления blackboard
    [SerializeField] private List<TransitionData> _transitions;
    [SerializeField] private List<Vector2> _positions;           // раскладка для редактора
}
```

- **`[SerializeReference]` для полиморфных узлов** — единственный рабочий способ хранить узлы разных типов в
  одном списке. Платим скоростью десериализации и хрупкостью при переименовании типов.
- **Стабильные `Id` узлов** (GUID или инкремент), а не индексы в списке: иначе удаление узла рвёт рёбра.
- **`[MovedFrom]`** (`UnityEngine.Scripting.APIUpdating`) при переименовании типа или namespace — иначе
  ассеты теряют узлы молча.
- **Компиляция ассета в рантайм-представление** при загрузке: имена переменных → индексы, рёбра → массивы,
  условия → делегаты. Рантайм не должен лазить в ассет.
- Тест «граф из билдера == граф из ассета» (одинаковые трассы решений) — лучший страж сериализации.

### 31.2 Чему учит UX готовых решений

- **Библиотека узлов с поиском** и категориями; узел добавляется в меню **атрибутом**, а не правкой редактора.
- **Подсветка активного узла/пути в рантайме** + цвет по статусу (`Running` жёлтый, `Success` зелёный,
  `Failure` красный) — это главная фича, за которую покупают такие ассеты.
- **Панель blackboard живьём**, с правкой значений на ходу.
- **«Извлечь в подграф»** на выделении — иначе большие графы не рефакторят.
- **Горячая перезагрузка**: правка графа без выхода из Play Mode (FFXV считали это критичным для итераций).
- Стикеры/заметки на графе — дешёвая документация, которую реально пишут.

## 32. Авторинг для дизайнера

### 32.1 Архетип — один ассет на тип врага

```csharp
[CreateAssetMenu(menuName = "AI Kit/Agent Archetype")]
public sealed class AgentArchetype : ScriptableObject
{
    public BehaviorGraphAsset Graph;             // или пресет из коробки
    public PerceptionProfile Perception;         // конусы, память, слух
    public MovementProfile Movement;             // скорости, кольца дистанций
    public AbilityDefinition[] Abilities;        // что умеет
    public RoleProfile Role;                     // ёмкости, приоритеты в отряде
    public DifficultyScales Difficulty;          // множители
    public BlackboardOverrides Overrides;        // числа поверх графа
}
```

**Путь «пять минут»** (главный сценарий, который надо защищать на приёмке):

1. Поставить префаб врага в сцену.
2. Выбрать архетип в одном поле.
3. Поправить 5–10 чисел в инспекторе.
4. Нажать Play — враг патрулирует, замечает, догоняет, бьёт.

Граф при этом **открывать не обязательно**. Если для нового врага нужен программист — авторинг не сделан.

### 32.2 Что должно быть в инспекторе

- **«Константа или переменная»** переключателем (`BlackboardValue<T>`): дизайнер пишет `2.5`, программист
  подставляет переменную.
- **Ключи blackboard — выпадающим списком**, не строкой.
- **Кривые — графиком** (`AnimationCurve` или свой драйвер для IAUS-кривых).
- **Валидация с внятными сообщениями**: висячее ребро, недостижимое состояние, ссылка на несуществующую
  переменную, умение без раннера, **поле без потребителя в рантайме** (ассет не должен обещать того, чего код
  не читает).
- **Визард настройки префаба** (*Setup Agent*): добавить компоненты по правилам, подключить адаптер тела,
  создать дочерние пустышки, проверить слои и маски.

### 32.3 Пресеты из коробки

Минимальный набор, который закрывает большинство запросов: `Melee Chaser`, `Ranged Kiter`, `Patrol Guard`,
`Sentry` (стационарный стрелок), `Brute` (медленный тяжёлый), `Flyer`, `Crowd Unit`. Каждый — 2D и 3D вариант.
Пресет должен работать **сразу после перетаскивания**, без настройки.

## 33. Отладка

### 33.1 Трасса решений — важнее всего

```csharp
public readonly struct DecisionRecord
{
    public readonly int Tick;
    public readonly string State;        // активное состояние/лист
    public readonly string Reason;       // что сработало: условие, сигнал, завершение задачи
    public readonly string Chosen;       // выбранное умение/цель
    public readonly float Score;         // счёт, если выбор был utility
}
```

- Кольцевой буфер на N последних решений **на агента**, вывод в окно и в файл.
- **Диффы трасс** — лучший инструмент отладки «почему он так себя ведёт»: прогнать дважды с одним сидом,
  сравнить.
- Для utility — **все счёты вариантов** в момент выбора, а не только победитель.

### 33.2 Гизмо и оверлеи

| Что рисовать | Зачем |
|---|---|
| конусы зрения, лучи, треки с цифрой внимания | «почему не видит / почему видит» |
| последняя известная позиция и точки поиска | «почему идёт не туда» |
| путь: узлы, рёбра с типом (`Jump`/`Fall`), текущее ребро | «почему застрял» |
| жетоны/слоты вокруг цели, кто чем владеет | «почему не атакует» |
| клеймы арбитра движения (кто держит ось) | «почему стоит на месте» |
| карта влияния heat-map'ой, выбранная точка | «почему выбрал эту позицию» |
| отказы рёбер (журнал) | баг-репорт дизайнеру уровня |

Все оверлеи — **выключены по умолчанию** и включаются флажками; рисование не должно жить в рантайм-коде
(только под `UNITY_EDITOR`/`DEVELOPMENT_BUILD`).

### 33.3 Экранная панель и читы

Минимум, который экономит часы: панель «агентов всего / думает за кадр / в бою / спит, мс на решения»,
и читы: `ai.spawn <archetype> [count]`, `ai.freeze`, `ai.invisible` (игрок неперцептируем),
`ai.kill`, `ai.difficulty <level>`, `ai.trace <agent>`.

---

# Часть IX. Тестирование

## 34. Три яруса

| Ярус | Что проверяет | Чем |
|---|---|---|
| **1. Чистая логика без сцены** | граф (семантика композитов, переходов, прерываний), blackboard, utility (кривые, свёртка, стратегии), планировщик (эвристика, соседство, склейка подпланов), арбитр движения, поиск пути на синтетической сетке, LOD и сон, валидаторы ассетов | обычные unit-тесты, EditMode |
| **2. Поведение на стенде** | «враг замечает за 1.2 с», «подходит на дистанцию удара», «не падает в яму», «один бьёт — остальные ждут», «уклоняется от снаряда», «далёкий спит» | PlayMode, реальный префаб, ручные шаги физики |
| **3. Постановочные партнёры** | боевые фичи против **известного** поведения: «только левый удар», «идеальный блок», «статичный блок», «захват», «парирование» | агенты-пресеты, которые вечно делают одно; с координатором не регистрируются |

Плюс **дымовой перф-тест** (§30) и **лист регрессии**: таблица «фича → автотесты + ручные проверки», чтобы
видеть, что проверяется руками.

## 35. Стенды и приёмы

### 35.1 Детерминированный стенд

```csharp
// геометрия описывается матрицей, КАК ОНА ВЫГЛЯДИТ ГЛАЗАМИ (строка 0 — верх)
NavGrid grid = GridHelper.FromRows(
    "..........",
    "...####...",
    "..........",
    "#####..###",
    "##########");

// или для платформера — именованные заготовки
Rig.Build(Level.Flat, Level.Step, Level.Gap(width: 3), Level.Slope(35f), Level.Wall);
```

Требования к стенду:

- **фиксированный сид** и ручные шаги: `Physics2D.simulationMode = Script`, свой цикл, шаг = фиксированный
  `dt`. Секунды игрового времени за миллисекунды реального;
- **реальный префаб** врага (не синтетический объект): тест ловит и ошибки настройки префаба;
- **контроллеры выключены**, ввод подаётся прямо в модель и скиллы — тест управляет агентом как игрок;
- **ожидания считаются из настроек** (`Settings.Jump.Height`, `AttackRange`), а не константами в тесте:
  тюнинг чисел не должен ломать тесты;
- **геометрия строится от значения настройки** («склон на 5° круче порога»), а не от магического числа.

### 35.2 Приёмы

- **Проверять «стало ли это правдой за N секунд»**, а не «сразу после вызова»: `StepUntil(() => …, seconds)`.
  ИИ по природе асинхронен.
- **Большинство багов ИИ воспроизводится двумя персонажами** (Skarupke, Game AI Pro Online 2021) — по
  аналогии с 96% багов многопоточности на двух потоках. Не нужны сцены на 20 врагов, чтобы найти баг
  координации.
- **`[assembly: InternalsVisibleTo("…Tests")]`** для шагов алгоритмов: эвристика, соседство, композиция
  подпланов, выбор перехода тестируются по отдельности.
- **Эргономика важнее количества:** pause / cancel / restart / repeat-until-fail ловят баги «один раз из
  десяти» лучше, чем новые тесты. Если что-то трудно тестировать — **не тестировать, а делать тестируемым**.
- Запись трассы в файл и диффы трасс (§33.1) как форма теста: «поведение не изменилось после рефактора».

## 36. Что покрывать обязательно

Каждый пункт ниже — живой баг из разобранного кода, который существовал именно потому, что теста не было:

| Что покрыть | Какой баг ловит |
|---|---|
| **Каждую** кривую отклика, по 3 точки | в гауссиане σ не влияла на ширину (покрыта была только линейная) |
| Стратегию выбора с **разными** счётами, проверяя *кого именно* выбрали | «топ-N» сортировался по индексу, а не по счёту |
| Конструкторы, которые присваивают поля | приоритет перехода затирался нулём в конструкторе |
| `Set/Has/Remove` **каждого** типа blackboard | `HasFloat` проверял словарь `float2` |
| Удаление из коллекции **во время** обхода | участник отряда пропускался при выпадении роли |
| Семантику композитов (`Sequence`/`Selector`/`Abort`) | «узел не сбросился при повторном входе» |
| Вето оси (нет цели → вариант не рассматривается) | «все по нулям, выбран случайный» |
| «Один сид → одна трасса» | скрытый `UnityEngine.Random` в логике |
| Отказ пути (нет маршрута → `false`, а не бесконечный поиск) | «застрял под платформой» |

---

# Часть X. Чеклисты и грабли

## 37. Чеклисты

### 37.1 Новый тип врага

1. Создать **архетип** (ассет), выбрать граф или пресет.
2. Заполнить профиль восприятия: радиусы, конусы, память. Проверить **маски** (стены отдельно от целей).
3. Заполнить профиль движения: скорости, кольца дистанций, возможности тела (прыжок, дэш, лестницы).
4. Перечислить **умения**: дистанции, кулдауны, телеграфы, веса, стоимость жетона; на каждое — раннер и клип
   с совпадающим `Id`.
5. Задать роль в отряде: ёмкости, приоритет, кольцо отхода.
6. Проверить на стенде: замечает → догоняет → бьёт → отходит; не падает в ямы; не застревает.
7. Проверить **втроём**: не толпятся в одной точке, один бьёт — остальные ждут.
8. Барки и звуки на входы в состояния (минимум: заметил, атакует, умер).
9. Гизмо включить и посмотреть глазами: конусы, путь, слоты.
10. Тест в PlayMode на главную механику врага + строка в листе регрессии.

### 37.2 Новая механика ИИ

1. Решить, **в каком слое** она живёт (§0.1). Если «во всех» — механика разложена неверно.
2. Данные — в blackboard/настройки, не в узел (§1.3).
3. Узел или ассет-фабрика? (`OnEnter/OnExit` и статусы → узел; одно выражение → фабрика.)
4. Время — параметром `dt`, случайность — из сида агента (§29).
5. Дорогие запросы — за дешёвым гейтом и с кешем на think-тик (§3.2).
6. Ресурс, за который конкурируют агенты, — через координатор, не через ссылки (§21).
7. Аллокации: буфер + `out size`, пулы (§28).
8. Тест: ярус 1, если это логика; ярус 2, если нужна геометрия (§34).
9. Гизмо/трасса: видно ли в отладке, что механика сработала (§33).
10. Документация: раздел здесь + строка в спеке модуля.

### 37.3 Ревью кода ИИ

- [ ] Узел не знает о `Rigidbody`/`Transform` тела напрямую (только через актуатор).
- [ ] Нет `Time.time` / `Time.deltaTime` / корутин по времени / `Invoke` / твинов.
- [ ] Нет `UnityEngine.Random` — только сид агента.
- [ ] Нет `FindObjectsOfType`/`GameObject.Find` в тике.
- [ ] Нет аллокаций в тике (список, строка, LINQ, лямбда с замыканием).
- [ ] Дорогой тест не стоит первым в условии.
- [ ] Узел не хранит данные механики.
- [ ] Прерывание обработано: `OnExit(reason)`/`OnAbort` освобождает ресурсы и брони.
- [ ] Новое значение имеет **ровно одного** владельца.
- [ ] Ассет не обещает поля, которое код не читает.
- [ ] На поведение есть тест нужного яруса.

## 38. Сорок грабель

**Архитектура**

1. Решение в сенсоре («увидел → побежал»).
2. Узел пишет скорость в физику напрямую.
3. Данные механики в состоянии, а не в blackboard.
4. Два слоя назначают одно значение (граф сущности, числа) и расходятся.
5. Координатор обрастает доменной логикой (оружие, станции) и превращается в God Object.
6. Планировщик сам ведёт агента — его невозможно протестировать без сцены.
7. Строковые ключи фактов в горячем пути.
8. Ручная типизированная поверхность blackboard (copy-paste-баги гарантированы).
9. Параллельные списки с выравниванием по индексу (роли и участники).
10. Модуль ИИ знает об игре — переносимость потеряна.

**Решения и граф**

11. FSM с переходами внутри `OnUpdate` состояний → спагетти.
12. Состояния-флаги (`ChaseWithLowHp`) вместо условий.
13. BT без жизненного цикла узла → «не сбросился при повторном входе».
14. Композит без памяти индекса → `Sequence` никогда не доходит до второго шага.
15. Прерывание по истинности условия, а не по фронту/направлению → лишние аборты.
16. Наблюдатели, срабатывающие вне активного пути (болезнь Unreal BT).
17. Перепроверка условий в случайные моменты вместо одного think-тика.
18. Utility как весь мозг — «почему он это выбрал» становится неотвечаемым.
19. Ноль как вето вместо явного «нет данных».
20. Умножение осей без компенсации → варианты с пятью осями всегда проигрывают.

**Восприятие и движение**

21. Бинарное «вижу/не вижу» → телепат или слепой.
22. Один флаг «вижу кого-то» на N игроков → «потерял одного — потерял всех».
23. Луч зрения по маске «всё» → упирается в самого себя.
24. Перебор всех целей каждым агентом каждый кадр → O(N²).
25. Обновление пространственного индекса на каждое движение (дороже перебора) — нужен гистерезис.
26. Только глобальный путь → «едет по рельсам»; только steering → застревает в вогнутой геометрии.
27. Сумма векторов в steering → ноль и дрожание.
28. Проходимость как свойство графа, а не предикат запроса → граф на каждый тип тела.
29. Ребро прыжка построено от числа в графе, а не от возможностей тела → прыжок, который нельзя сделать.
30. Эскалация к дорогому поиску без гистерезиса → мигание между навигаторами.

**Бой и группы**

31. Все атакуют одновременно (нет жетонов) → игрока съедают за секунду.
32. Один счётчик атакующих вместо пулов по типу → стрелки душатся или все лезут в упор.
33. Жетон выдаётся по порядку обхода коллекции → невоспроизводимо.
34. Отмена начатого замаха при вытеснении → дёргающаяся анимация.
35. Кадрово-точный блок/уклон → «читает мои нажатия».
36. Реакция без перепроверки на срабатывании задержки → блок в пустоту.
37. Уклон в сторону атакующего (вырожденный случай не обработан).
38. Предсказание снаряда с другим шагом интеграции, чем у снаряда → промахи.
39. Директор подсказывает агентам позицию игрока → «он всегда знает, где я».
40. Сложность только глобальная, а дизайнер просит «элиту среди чаффы» → переделка слоя множителей.

---

# Приложение A. Разборы шипнутых проектов

Тринадцать разборов по первоисточникам (главы Game AI Pro написаны теми, кто эти игры делал) и по
архитектурному руководству живого проекта. Для каждого: задача → решение → что брать.

## A.1 Mark of the Ninja — восприятие в 2D-стелсе

**Задача.** Игрок должен понимать, **почему** его заметили, и уметь растаскивать охрану.

**Решение.** Источники интереса — **данные**, а не код: у стимула приоритет, флаги («требует проверки»,
«тревожный») и время жизни. На один стимул реагируют **роли** («проверяющий», «прикрывающий»), а не все сразу.
Правило «один интерес за раз»: агент не копит стимулы — новый важнее перебивает, равный игнорируется. Звук
распространяется **по проходимости**, а не радиусом.

**Что брать:** data-driven стимулы с приоритетами; роли от стимула; «один интерес за раз»; слух как путь
(§9б.3).

## A.2 FINAL FANTASY XV — AI Graph и мета-ИИ

**Задача.** Один инструмент на все виды ИИ (монстры, напарники, мета-слой), чтобы контент делали дизайнеры.

**Решение.** Узел графа имеет `Start/Update/Finalize` и **условие завершения**; локальные blackboard'ы на
узел; оверрайды графа для подвидов; подсветка активного узла и **горячая перезагрузка** без выхода из Play.
Параметры атак извлекались **из анимации** (прогон клипа по сферам вокруг монстра → дистанции и углы).
Над агентами — мета-ИИ, который выдаёт задания («спаси», «дай уйти»).

**Что брать:** контракт узла; локальные данные; оверрайды; горячая перезагрузка; числа атак — из контента
(§16, §20б.2).

## A.3 The Last of Us — «чтобы было неприятно убивать»

**Задача.** Человеческие враги, бой с которыми читается как честный.

**Решение.** Координатор ролей с **запросом и подтверждением**: роль достаётся «идеальному кандидату», а не
первому попросившему. Укрытия (`posts`) фильтруются по «могу ли отсюда стрелять» — до четырёх лучей на точку.
Угол зрения **зависит от дистанции**. Поиск по **карте вероятности** (растекание + очистка просмотренного).
Роль «стрелок по возможности», чтобы не стреляли все. Читы — **ради темпа**: если игрок ускользнул, цикл
проверки ужимается с 2 мин до 30 с.

**Что брать:** идеальный кандидат вместо первого; «укрытие = спрятался и может стрелять»; угол от дистанции;
search map; читы на уровне темпа, не знания (§18а, §21.1, §9б).

## A.4 Days Gone — «уверенность» и линия фронта

**Задача.** Орда и группы должны наступать и отступать осмысленно, без пересчёта всего каждый кадр.

**Решение.** Одно число — **уверенность** группы — управляет наступлением, отходом и привязкой. Пространство
описывается **линией фронта** и личными **полосами** агентов. Агент в укрытии считается стоящим **в точке
укрытия** (это снимает каскад инвалидации). Застрельщики (rusher) **исключены** из фронта. Явный принцип:
«хорошо выглядящий бой важнее эффективного».

**Что брать:** одно число на тактику группы; фронт и полосы; «агент = точка укрытия»; исключения из строя
(§18а.2, §20а.2).

## A.5 Kingdoms of Amalur — сколько врагов бьют одновременно

**Задача.** Чтобы толпа не съедала игрока, но и не стояла без дела.

**Решение.** Не счётчик атакующих, а **веса и ёмкости**: у цели `grid capacity` (кого пустить в круг) и
`attack capacity` (сколько и каких атак), у врага `grid weight` (солдат 4, тролль 8), у атаки `attack weight`.
Ёмкость 12 → третий тролль ждёт снаружи. Слоты раздаёт **центральный распределитель**; атакующий лочит слот и
**отпускает сразу после начала атаки** (ротация). Без слота агент встаёт напротив свободного — получается
бесплатное фланкирование. Сложность крутится **ёмкостями цели**.

**Что брать:** всю модель целиком (§18.1).

## A.6 Guild Wars 2: Heart of Thorns — utility-бой

**Задача.** Сотни умений у десятков видов врагов, настраиваемые дизайнерами.

**Решение.** Оценка = **произведение осей** с ранним выходом (ноль — вето), общие наборы осей на несколько
умений, служебные оси `runtime` (сколько идёт бой) и `cooldown` как обычные оси.

**Что брать:** умножение с вето и early-out; переиспользуемые наборы осей; служебные оси вместо спец-условий
(§4, §20б.2).

## A.7 Project Highrise — 1000 NPC при 60 FPS

**Задача.** Тысяча жильцов небоскрёба на слабом железе.

**Решение.** **Упрощать модель, а не алгоритм:** 15 000 клеток свелись к ~100 узлам «площадки +
соединители» (этажи и лифты). Решения дешёвые, исполнение **open-loop** (решил — иди, не переспрашивая).
Расписания вместо планировщиков. **Меньше скрытых состояний**: внутренние «голод/усталость» на сотнях NPC
делают поведение необъяснимым и для игрока, и для дизайнера.

**Что брать:** модель вместо алгоритма (это же — граф площадок в платформере); расписания; меньше скрытого
состояния (§13, §26).

## A.8 F1 2011 — контекстные карты

**Задача.** Обгоны и защита траектории без тысяч спец-случаев.

**Решение.** **Контекстные карты**: поведения пишут interest/danger по направлениям, арбитраж один — минимум
опасности, затем максимум интереса, со сглаживанием и гистерезисом. Переход убрал ~4000 строк спец-кода
обгона. LOD — **разрешением карты**, а не отключением поведений.

**Что брать:** контекстные карты и их арбитраж; LOD разрешением (§14.1).

## A.9 Crysis 2 — выбор позиции как язык запросов

**Задача.** Дизайнер должен описывать тактику без программиста.

**Решение.** **Tactical Point System**: генераторы кандидатов + тесты-фильтры + веса → лучшая точка, с
**fallback-опциями** («идеально → приемлемо → хоть что-то») и профилированием запросов. Это прямой предок
Unreal EQS.

**Что брать:** язык запросов позиций с обязательным fallback (§23.4).

## A.10 Alien: Isolation — два мозга и менеджер напряжения

**Задача.** Чужой должен быть страшным, но не несправедливым.

**Решение.** **Два мозга:** первый знает, где игрок, и подсказывает район («ищи здесь»); второй — сам чужой,
который честно ищет и ничего не знает. Между ними — менеджер напряжения, который не даёт давать точную
позицию и разводит пики и передышки.

**Что брать:** принцип «директор знает, агент — нет» (§25.1).

## A.11 Left 4 Dead — AI Director как процедурный темп

**Задача.** Каждый прохождение должно иметь ритм «нагнетание → пик → передышка».

**Решение.** Директор следит за «интенсивностью» игроков (урон, близость врагов, продвижение) и управляет
**подачей**: когда и где спавнить, когда дать тишину, когда выпустить особого врага. Сила врагов не меняется.

**Что брать:** интенсивность как число; управление подачей, а не силой (§25).

## A.12 Atone (внутренний источник) — координатор, арбитр, LOD

**Задача.** 40–77 ботов на выделенном сервере, бой в ближнем бою, корабли, летающие; поведение должно быть
воспроизводимым.

**Решение.** Главный принцип: **«бот никогда не договаривается с ботом»** — весь арбитраж в координаторе,
исполнение в агенте. Роли — слоты у цели с ёмкостями формулой от размера ростера; выбор цели — счёт с памятью
угрозы и порогом переключения; **жетоны тремя пулами** с окном созревания заявок и приоритетом «в упор»;
локальное движение — **одно поле потока на стычку** с плотностью в стоимости, с эскалацией к честному поиску
по **вердикту** достижимости; **арбитр движения с токеном на ось**; реакции с задержкой и перепроверкой;
**ярусы LOD и сон** (трасса: из 77 ботов в боевом движении реально 6.7); три яруса тестов и постановочные
партнёры; цепочка приоритетов сложности, записанная лесенкой.

Отдельно ценен их **названный долг**: координатор в 8335 строк с доменной логикой оружия внутри; контроллер
как хаб; два слоя, назначающих мозг, которые расходятся; ассеты с полями, которые код не читает.

**Что брать:** практически всё перечисленное — §15, §18.2, §21, §25.3, §27.2, §34; и их долг как список
граблей (§38).

## A.13 Учебный AI-фреймворк (разбор кода) — как это пишется на C#

**Задача.** Увидеть не «как бывает», а как это выглядит в рабочем Unity-коде.

**Решение (что оказалось в коде).** Жизненный цикл узла в базовом классе; композиты с памятью индекса; один
декоратор с подменой результата; аборт по фронту условия; FSM, где машина сама — состояние; переходы
`readonly struct` с приоритетом; utility со `Score`-типом, **вето как `bool`**, обязательными и
необязательными осями, стратегиями выбора и полной библиотекой кривых; GOAP обратным A* с композитными
действиями; HTN с планом в буфере и подменой домена; **шов «планировщик → план из имён → таблица → BT»**;
blackboard с int-ключами и кодогенерацией; throttle со случайным периодом; поиск пути буферным API с
предикатом проходимости и JPS как подкласс A*; карты влияния на `NativeArray` + Burst со штампами; TeamAI как
матчмейкинг намерений, где роль = подмена домена.

**И их баги как уроки:** σ в гауссиане не влияла на ширину; «топ-N» сортировался по индексу; приоритет
перехода затирался нулём в конструкторе; `HasFloat` смотрел в чужой словарь; участник отряда пропускался при
удалении в прямом цикле. Каждый — из-за отсутствия одного теста (§36).

---

# Приложение B. Роли врагов и жанры

## B.1 Каталог ролей

Роль — это не «тип монстра», а набор решений, которые ИИ обязан уметь принимать.

| Роль | Что решает | Что нужно от системы |
|---|---|---|
| **Патрульный** | маршрут, пауза, «заметил» | маршруты, восприятие с нарастанием, состояние `Notice` |
| **Преследователь (ближний бой)** | сблизиться, ударить, отойти | навигация с прыжками, кольца дистанций, жетоны, восстановление |
| **Стрелок / кайтер** | держать дистанцию, линия огня, перезарядка | полосы дистанций, проверка линии, запросы позиции |
| **Фланкёр** | обойти, не попасть под огонь | стоимость пути с «опасностью», роль от координатора |
| **Щитоносец / танк** | держать фронт, прикрывать | роль с «не отступать», приоритет над личным страхом |
| **Застрельщик (rusher)** | лезть вплотную, ломать строй | отдельный тип, **исключаемый** из фронта и строя |
| **Снайпер / турель** | не двигаться, стрелять далеко | «неподвижный» режим, тоже вне строя |
| **Летающий** | полёт, зависание, пикирование | навигатор без земли, steering + модель высоты (§26а.2) |
| **Саппорт** | лечение, бафф, подкрепления | utility по состоянию союзников, кулдауны, свои сенсоры |
| **Босс** | фазы, паттерны, телеграфы | фазы как состояния, анти-повтор, арена (§20б) |
| **Юнит толпы** | идти, обтекать, не толпиться | общее поле, boids, LOD (§26) |
| **Напарник (buddy)** | не мешать, подыгрывать, не умирать глупо | приказы от директора, «не лезть в кадр», особые читы |

Главный вывод: ролей много, но **все собираются из одного набора кусков** — восприятие, граф, запросы позиции,
умения с весами, координатор. Отдельный код на роль не нужен; нужны **данные** (архетип).

## B.2 2D-платформер

1. **Рёбра неоднородны:** ходьба, прыжок, падение, лестница, сквозь платформу, дэш; проходимость — от
   возможностей тела (§13).
2. **Пространство решений маленькое:** «влево / вправо / прыжок / присесть» — контекстные карты вырождаются в
   2–4 слота, и большинство врагов обходится реактивной навигацией.
3. **Читаемость критична:** игрок видит весь экран, любая тупость заметна сразу → слоты вокруг цели,
   «кто не бьёт — отойди», телеграфы.
4. **Восприятие секторами, а не конусами:** «за спиной» в 2D — буквально «с другой стороны».

Типовой скелет 2D-врага:

```
Patrol → (заметил, нарастание) → Notice → Chase
Chase  → (в радиусе удара и есть жетон) → Strike → Recover → Chase
Chase  → (потерял) → Search (по последней позиции) → Patrol
любое  → (урон) → Hurt → предыдущее;   (смерть) → Dead
```

Что ломается в 2D чаще всего: враг идёт в обрыв, потому что цель ниже; прыгает в стену в упор; двое стоят в
одной точке; «читает» игрока через платформу; мгновенно разворачивается. Лечится проверкой «упаду ли и
вернусь», зазором перед прыжком, слотами, честной проверкой перекрытия и временем реакции 0.2–0.4 с.

## B.3 Топ-даун и твинстик

Навигация — сетка или навмеш (в Unity 2D — `NavMeshPlus` либо своя сетка по tilemap). Главная проблема —
**кучи**: все идут к игроку одним путём; лечится полем потока + расталкиванием + слотами вокруг цели.
Уклонение от снарядов — **контекстная карта опасности** (danger вдоль траектории каждого снаряда). Хорошо
работают «веерные» волны: директор выпускает группы, а не всех сразу.

## B.4 Шутеры

Позиция важнее стрельбы (§18а). Ключевое: укрытие = «спрятался и может стрелять»; каскад инвалидации лечится
правилом «агент = точка укрытия»; один стреляет — остальные работают; подавление как способ показать
опасность; сложность — точностью, а не хитпоинтами.

## B.5 Стелс

Нарастание и спад внимания, разные пороги в стелсе и бою, точка проверки перекрытия в пользу игрока, поиск по
памяти и карте вероятности, отвлечения как источники интереса с приоритетами, прощение игрока как механика —
всё это §9б.

## B.6 Турн-бейсд, гонки, RTS, «живой мир»

- **Пошаговые:** ход длинный, можно позволить поиск (минимакс/MCTS), но в шипнутых играх чаще utility по
  действиям + портфель эвристик. Переносимое: utility-оценка и запросы позиций.
- **Гонки:** координата — трасса, а не мир; низкий уровень — PID, высокий — поведения; «резинка» — отдельная
  подсистема управления гонкой, а не чит в физике. Переносимое: **выбор системы координат важнее алгоритма**
  (§13а.4).
- **RTS:** карты влияния слоями (§23), предсказание исхода боя как функция (аналог «уверенности» для групп),
  поле потока для массы.
- **«Живой мир»:** расписания вместо планировщиков; умные зоны и объекты (§24.1); **алиби вместо симуляции**
  (если игрок не смотрел — не симулируем, а придумываем правдоподобное «что было»); меньше скрытых состояний.

## B.7 Чеклист «ИИ читается игроком»

- [ ] Видно, **кто сейчас угрожает** (кто в круге атаки, кто ждёт).
- [ ] Видно **направление боя** (фронт, полосы, кто откуда идёт).
- [ ] У каждой опасной атаки есть **телеграф** и окно реакции.
- [ ] Враг **реагирует заметно** на каждое важное событие: вздрогнул, крикнул, повернулся.
- [ ] Решения **объяснимы**: игрок понимает, почему его заметили и почему враг отступил.
- [ ] Нет «мерцания»: минимальное время в состоянии, гистерезис, кулдауны на смену решения.
- [ ] Нет мгновенных реакций и мгновенных разворотов.
- [ ] Враги **не толпятся** и не стоят в одной точке.
- [ ] Поведение **детерминировано** при одном сиде.

---

# Приложение C. Практика Unity

## C.1 Где в Unity живёт ИИ

- **Решение — до шага тела.** Если ИИ пишет ввод телу, которое шагает в `FixedUpdate`, у мозга должен быть
  более ранний `[DefaultExecutionOrder]` (например −150 против −100 у тела). Иначе ввод применяется через кадр
  и движение «запаздывает».
- **Исполнение каждый шаг, решение по расписанию:** разделение `Think()` / `Tick()` ложится на Unity
  естественно.
- **Своя пауза.** `Time.timeScale = 0` ломает всё, что живёт на `unscaledDeltaTime`, и не останавливает
  корутины. В проекте с игровым циклом ИИ обязан тикаться от цикла и вставать на паузе вместе с ним.
- **`GetComponent<IInterface>()` работает** (и `InChildren`/`InParent`); но перегрузку с `List<T>` для
  интерфейсов лучше не использовать — брать массивную.
- **Порядок `Awake` не определён:** связка «найди соседа» должна быть идемпотентной (`EnsureParts()`), а не
  «один раз в `Awake` и надеемся».

## C.2 Навигация в Unity

| Инструмент | Что важно знать |
|---|---|
| `NavMeshSurface` (`com.unity.ai.navigation` 2.x) | печёт в редакторе **и в рантайме**; данные живут, пока компонент включён; для рантайма — мелкие плитки и воксель от радиуса агента |
| `NavMeshModifier` / `ModifierVolume` | «непроходимо», своя область, своя стоимость |
| `NavMeshLink` | связь между поверхностями (прыжок, лестница, телепорт) — гибче и дешевле старого `OffMeshLink` |
| `NavMeshAgent` | использовать **как навигатор, а не как двигатель**: `CalculatePath`/`path.corners`, а двигать тело самому; `updatePosition/updateRotation = false` |
| Прохождение ссылок | вручную: ловим `agent.isOnOffMeshLink`, играем прыжок, затем `CompleteOffMeshLink()` |
| `NavMeshQuery` (`UnityEngine.Experimental.AI`) | **можно внутри джоба**; строгий порядок `BeginFindPath → UpdateFindPath → EndFindPath → GetPathResult`; возвращает полигоны, превращение в точки (funnel) писать самому; статус «experimental» |
| 2D | встроенной 2D-навигации **нет**: `NavMeshPlus` (коллекторы tilemap/спрайтов/2D-коллайдеров), своя сетка, или свой граф площадок (§13) |

## C.3 Физические запросы

- **Только `NonAlloc`** в горячем пути (`OverlapCircleNonAlloc`, `RaycastNonAlloc`, `Raycast(..., ContactFilter2D, results)`);
  буферы — поля, не локальные массивы.
- **Маски слоёв вместо тегов и `GetComponent`**: фильтрация в физике, не в C#.
- Проверить явно `Physics2D.queriesHitTriggers` и `queriesStartInColliders` — дефолты часто не те, что нужны
  для «вижу ли я цель».
- **Шаг физики под контролем в тестах:** `Physics2D.simulationMode = Script` + `Physics2D.Simulate(dt)` —
  только так получается детерминированный прогон.
- **Частота сенсоров ≠ частота решений**: 0.1–0.3 с на сенсор с фазовым сдвигом (§9.6).

## C.4 Анимация

- **Граф ИИ не трогает анимацию.** Трёхслойная схема: ИИ → слой тела (состояние, запреты) → анимация.
- **Длительность действия — из контента**; моменты удара — **события кадров**; ИИ слушает.
- Параметры атак можно извлекать из клипов editor-утилитой (`AnimationMode.SampleAnimationClip`).
- `Animator` на паузе надо морозить; `Animator.StringToHash` кешировать.
- Действия — состояния по имени через `CrossFade` из кода; `Any State` — только для смерти и оглушения;
  окна («можно отменить») — кривыми клипа; связка с `NavMeshAgent` — `updatePosition = false` +
  `OnAnimatorMove` (§20.2).

## C.5 Данные и сериализация

- **Узлы графа:** `[SerializeReference]` + `[Serializable]`; альтернатива — узлы как подассеты
  `ScriptableObject` (удобнее инспекторы, но замусоривает ассет).
- **Стабильные идентификаторы узлов** важнее имён типов; `[MovedFrom]` при переименовании.
- **Перекрытия на экземпляре** (значения blackboard, числа архетипа) — иначе на каждый подвид врага плодится
  копия графа. Варианты префабов — штатный способ «такой же, но другой».
- **Миграции:** номер версии формата + шаг апгрейда, иначе чужие ассеты рассыплются.
- Generic-поля (`BlackboardValue<float>`) сериализуются, но только конкретные инстанцирования.
- **`ScriptableObject` — общий для всех экземпляров:** любое «состояние» в ассете станет общим на всех агентов
  (классическая ошибка: таймер в ассете настроек).

## C.6 Редакторы и отладка

- **GraphView** (`UnityEditor.Experimental.GraphView`) — «experimental» с 2019, но на нём сделаны Shader Graph,
  VFX Graph и почти все ассеты; рабочий выбор сейчас.
- **Graph Toolkit** (`com.unity.graphtoolkit`, с 6.2) — будущее (UI Toolkit), но 0.x, «не для продакшена» и
  **без рантайм-исполнения**: закладываться за своим слоем абстракции.
- **Domain reload стирает статику:** реестры узлов и кеши перестраивать по
  `[InitializeOnLoad]`/`[RuntimeInitializeOnLoadMethod]`.
- Компиляция скриптов внутри Play Mode роняет сессию и ломает ввод — не компилировать, пока идёт Play.
- Обязательное в отладчике: подсветка активного состояния/пути, цвет узла по статусу, blackboard живьём **с
  правкой**, лог «почему это решение», горячая перезагрузка, выбор агента кликом в сцене.

## C.7 Десять грабель, на которые наступают именно в ИИ

1. **Уничтоженные объекты:** цель может исчезнуть в любой момент — проверка `Target != null` обязательна.
2. **`OnEnable`/`OnDisable` и подписки:** мозг обязан отпускать жетоны, роли, слоты и брони в `OnDisable`,
   иначе цель останется «занятой» навсегда.
3. **Пул объектов:** агент из пула сбрасывает состояние в `OnEnable`, а не надеется на конструктор.
4. **Смена сцены:** статические реестры (мир, координаторы) чистить, иначе второй заход получает «мёртвых»
   агентов.
5. **`Time.timeScale` и корутины** несовместимы с честной паузой; `Invoke`/твины в логике ИИ не использовать.
6. **Строки в blackboard:** имена компилировать в индексы один раз.
7. **Порядок `FixedUpdate`** между агентом и телом — `DefaultExecutionOrder` обязателен.
8. **Физика 2D:** писать позицию в обход физики — рассогласование с коллизиями; телепорты только методами тела.
9. **Интерфейсы не сериализуются:** ссылка «что-то с `IAbilityRunner`» — через `MonoBehaviour`-поле + проверку
   или автопоиск.
10. **Состояние в `ScriptableObject`** становится общим на всех агентов.

---

# Приложение D. Готовые решения и build vs buy

## D.1 Что встроено в движки

**Unreal** — самый богатый набор: Behavior Tree + Blackboard (с декораторами-наблюдателями), **StateTree**
(состояния + параллельные задачи + переходы, выбор по требованию), **EQS** (запросы окружения: генераторы +
тесты → лучшая точка), **Smart Objects** (слоты со состояниями Claimed/Occupied и аннотациями входа),
**Mass AI / ZoneGraph** (ECS-толпа), AI Perception, Gameplay Ability System. Главный урок: **разделение «что
делать» (StateTree) и «где это делать» (EQS)** — две разные системы, и это правильно.

**Unity** даёт существенно меньше:

| Пакет | Состояние | Суть |
|---|---|---|
| `com.unity.behavior` | бесплатный, 1.0.x | BT-граф: ветки могут сходиться, `Abort`/`Restart`, подграфы (в т.ч. динамические), blackboard перекрывается на агенте, узлы — `[SerializeReference]` |
| `com.unity.ai.navigation` | 2.x, поддерживается | NavMeshSurface, Modifier, **NavMeshLink** |
| `UnityEngine.Experimental.AI` | experimental | `NavMeshQuery` в джобах |
| `com.unity.graphtoolkit` | 0.x, с 6.2 | фреймворк для своих графовых редакторов; **без рантайма** |
| `UnityEditor.Experimental.GraphView` | experimental, живёт | на нём сделано всё остальное |
| ML-Agents | Apache 2.0 | обучение агентов; отдельная тема (§0.4) |

**Вывод по Unity:** восприятия, координации, толпы и пространственных запросов в движке **нет вообще** — есть
навигация и (отдельно) BT-граф.

## D.2 Asset Store

| Ассет | Что внутри | Что брать как идею |
|---|---|---|
| **Behavior Designer** (Opsive) | BT, большая библиотека задач, рантайм-отладка, **conditional aborts**; дальше только поддержка, фичи — в Pro | семантику conditional abort, формат задач |
| **Behavior Designer Pro** | рантайм на DOTS, новый UI | «ядро на данных, рантайм отдельно» |
| **NodeCanvas** | BT + FSM + visual scripting в одном, много готового | BT и FSM одним инструментом |
| **Panda BT** | дерево **описывается скриптом** + лёгкий визуал | скриптовое описание как альтернатива графу |
| **A\* Pathfinding Project** | grid/point/navmesh-графы, funnel modifier, RVO, пересчёт в рантайме | интерфейс навигации, funnel, RVO как **опциональный адаптер** |
| **Emerald AI / Invector** и подобные | «готовый враг» целиком | ничего: это контент-киты, а не каркас |
| Sensing/Senses-паки | конусы зрения, слух, периодическое обновление | «восприятие — отдельный пакет» |

Что говорят практики: у Unity Behavior хвалят UI, интеграцию и **лёгкость добавления своих узлов**, ругают
скудную библиотеку и неясность поддержки; у NodeCanvas хвалят производительность и объём готового, ругают UI и
платные паки узлов. Независимый тест на ~19.7k объектов: **пока у вас нет десятков тысяч агентов, вы упрётесь
во что-то другое раньше, чем в накладные расходы дерева** — то есть «производительность BT» почти никогда не
является настоящей проблемой.

## D.3 Открытый код, который стоит читать

| Проект | Чем ценен |
|---|---|
| [BehaviorTree.CPP](https://github.com/BehaviorTree/BehaviorTree.CPP) | строгая документированная семантика (`ReactiveSequence`, `RetryUntilSuccessful`), логирование, редактор Groot |
| [py_trees](https://github.com/splintered-reality/py_trees) | лучшая документация семантики: что считается «висящим» состоянием и как его прерывать |
| [thekiwicoder0/UnityBehaviourTreeEditor](https://github.com/thekiwicoder0/UnityBehaviourTreeEditor) | компактный GraphView-редактор: узлы-подассеты, подсветка статусов |
| [LimboAI](https://github.com/limbonaut/limboai) (Godot) | эталон отладчика: активные деревья, blackboard по областям, hot-reload |
| [crashkonijn/GOAP](https://github.com/crashkonijn/GOAP) | многопоточный планировщик, **Capabilities** (наборы goals/actions/sensors), раздельные «решение» и «исполнение» |
| [luxkun/ReGoap](https://github.com/luxkun/ReGoap) | A* по действиям на чистом C# |
| [fluid-hierarchical-task-network](https://github.com/ptrefall/fluid-hierarchical-task-network) | HTN с Domain Builder и слотами поддоменов |
| [sinbad/SUSS](https://github.com/sinbad/SUSS) | utility для Unity с акцентом на отладку оценок |
| [apoch/curvature](https://github.com/apoch/curvature) | редактор кривых отклика (авторинг IAUS) |
| [ProjectBorealis/IAUS](https://github.com/ProjectBorealis/IAUS) | IAUS внутри Unreal BT, с честными предупреждениями «что не стыкуется» |
| [recastnavigation](https://github.com/recastnavigation/recastnavigation) | индустриальный стандарт navmesh (Recast) + рантайм (Detour) |
| [snape/RVO2](https://github.com/snape/RVO2) | референс ORCA |
| [h8man/NavMeshPlus](https://github.com/h8man/NavMeshPlus) | NavMesh для 2D |
| [BrunoSilvaFreire/ShiroiUnity2DPathfinding](https://github.com/BrunoSilvaFreire/ShiroiUnity2DPathfinding) | 2D-платформер: граф с прыжковыми рёбрами |
| [gameai.com: Response Curve](https://www.gameai.com/manuals/index.php/Response_Curve) | формальное описание кривых и параметров m, k, b, c |

## D.4 Брать или писать

| Берите готовое, если | Пишите своё, если |
|---|---|
| нужен только BT и только для 3D | нужны восприятие + координация + бой + 2D-навигация в одном |
| нет требований к переносимости между проектами | модуль должен переезжать папкой в другой проект |
| команда готова жить с чужой семантикой прерываний | семантика решений — часть вашего геймдизайна |
| времени нет, а поведение простое | поведение — ядро игры, и его будут тюнить годами |

Разумная середина, которую мы и выбрали: **своё ядро + адаптеры к чужому** (навигация A\*PP/NavMesh как
опция, тело как адаптер), и никаких жёстких зависимостей.

---

# Приложение E. Источники

## E.1 Game AI Pro — главный бесплатный источник

Четыре книги + онлайн-издания, **158 глав, все в свободном PDF**: [gameaipro.com](http://www.gameaipro.com/).
Написаны практиками о шипнутых играх. Ключевые главы, на которые опирается этот справочник:

| Глава | О чём |
|---|---|
| [GAIP ch.28 — Beyond the Kung-Fu Circle](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter28_Beyond_the_Kung-Fu_Circle_A_Flexible_System_for_Managing_NPC_Attacks.pdf) | жетоны атаки: веса и ёмкости (§18.1) |
| [GAIP ch.32 — How to Catch a Ninja](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter32_How_to_Catch_a_Ninja_NPC_Awareness_in_a_2D_Stealth_Platformer.pdf) | восприятие в 2D-стелсе (§9б) |
| [GAIP ch.14 — LOD Trader](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter14_Phenomenal_AI_Level-of-Detail_Control_with_the_LOD_Trader.pdf) | LOD как задача о рюкзаке (§27.2) |
| [GAIP ch.23 — Crowd Pathfinding with Flow Field Tiles](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter23_Crowd_Pathfinding_and_Steering_Using_Flow_Field_Tiles.pdf) | поля потока плитками (§26.1) |
| [GAIP ch.24 — Efficient Crowd Simulation for Mobile](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter24_Efficient_Crowd_Simulation_for_Mobile_Games.pdf) | дешёвая толпа |
| [GAIP ch.37 — Alibi Generation](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter37_Alibi_Generation_Fooling_All_the_Players_All_the_Time.pdf) | «алиби вместо симуляции» (B.6) |
| [GAIP ch.38–42 — Racing AI](http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter38_An_Architecture_Overview_for_AI_in_Racing_Games.pdf) | архитектура, трасса, PID, heat vision, rubber-banding (§26а.1) |
| [GAIP2 ch.11 — Smart Zones](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter11_Smart_Zones_to_Create_the_Ambience_of_Life.pdf) | поведение в объектах мира (§24.1) |
| [GAIP2 ch.17 — Robust Efficient Crowds](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter17_Advanced_Techniques_for_Robust_Efficient_Crowds.pdf) | толпа в продакшене (§26) |
| [GAIP2 ch.25 — Monte Carlo Tree Search](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter25_Monte_Carlo_Tree_Search_and_Related_Algorithms_for_Games.pdf) | MCTS для пошаговых (B.6) |
| [GAIP2 ch.30 — Modular Tactical Influence Maps](http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter30_Modular_Tactical_Influence_Maps.pdf) | карты влияния слоями (§23) |
| [GAIP3 ch.25 — Combat Outcome Prediction](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter25_Combat_Outcome_Prediction_for_Real-Time_Strategy_Games.pdf) | предсказание исхода боя |
| [GAIP3 ch.28 — Pitfalls of MCTS](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter28_Pitfalls_and_Solutions_When_Using_Monte_Carlo_Tree_Search_for_Strategy_and_Tactical_Games.pdf) | подводные камни MCTS |
| [GAIP3 ch.33 — Combat AI Accuracy to Balance Difficulty](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter33_Using_Your_Combat_AI_Accuracy_to_Balance_Difficulty.pdf) | сложность точностью (§18а.3) |
| [GAIP3 ch.34 — 1000 NPCs at 60 FPS](http://www.gameaipro.com/GameAIPro3/GameAIPro3_Chapter34_1000_NPCs_at_60_FPS.pdf) | «площадки + соединители», дешёвые решения (A.7) |
| [OE21 ch.1 — Automated AI Testing](http://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter01_Automated_AI_Testing_Simple_tests_will_save_you_time.pdf) | тесты ИИ, «двух персонажей достаточно» (§35.2) |
| [OE21 ch.3 — Gears Tactics](http://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter03_Gearing_the_Tactics_Genre_Simultaneous_AI_Actions_in_Gears_Tactics.pdf) | одновременные действия отряда |
| [OE21 ch.7 — Pacing in Warframe](http://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter07_Managing_Pacing_in_Procedural_Levels_in_Warframe.pdf) | темп в процедурных уровнях (§25) |
| [OE21 ch.11 — F.E.A.R. Combat Dialogue](http://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter11_Hitting_the_Mark_Combat_Dialogue.pdf) | барки как объяснение решений (§10) |

Остальные главы (включая разборы FFXV, The Last of Us, Days Gone, Guild Wars 2, Alien, Left 4 Dead,
Kingdoms of Amalur, Crysis 2, F1) ищутся по индексу на [gameaipro.com](http://www.gameaipro.com/) — структура
ссылок однотипная: `GameAIPro<N>/GameAIPro<N>_Chapter<NN>_<Title>.pdf`.

## E.2 Книги

- **Ian Millington, «AI for Games»** (3-е изд.) — энциклопедия алгоритмов: движение, поиск пути, принятие
  решений, тактика, обучение. Эталонный справочник по алгоритмам.
- **Steve Rabin (ред.), «Game AI Pro»** — те же 158 глав в бумаге.
- **Mat Buckland, «Programming Game AI by Example»** — steering, FSM, простая тактика с кодом; устаревает, но
  steering-глава до сих пор лучшая вводная.

## E.3 Доклады и люди

- **Dave Mark** — [IntrinsicAlgorithm](http://intrinsicalgorithm.com/): utility AI и IAUS из первых рук;
  доклады GDC «Building a Better Centaur», «Improving AI Decision Modeling Through Utility Theory».
- **Andrew Fray** — context steering (GDC 2013/2015), F1 2011.
- **Bobby Anguelov** — [takinginitiative.wordpress.com](https://takinginitiative.wordpress.com/): анимация и
  ИИ, критика «BT как всё».
- **Tommy Thompson, «AI and Games»** — [aiandgames.com](https://www.aiandgames.com/): видеоразборы
  Alien: Isolation, F.E.A.R., Halo, Shadow of Mordor.
- **GDC AI Summit** — ежегодный срез практики; **AI Game Programmers Guild** — сообщество авторов Game AI Pro.

**Анимация, эффекты, звук (§20), проверено 13.11.2026:**

- Epic: [Animation Optimization](https://dev.epicgames.com/documentation/en-us/unreal-engine/animation-optimization-in-unreal-engine),
  [Animation in Lyra](https://dev.epicgames.com/documentation/en-us/unreal-engine/animation-in-lyra-sample-game-in-unreal-engine),
  [Animation Slots](https://dev.epicgames.com/documentation/en-us/unreal-engine/animation-slots-in-unreal-engine),
  [Motion Matching](https://dev.epicgames.com/documentation/unreal-engine/motion-matching-in-unreal-engine),
  [Dynamic Asset Selection (Chooser)](https://dev.epicgames.com/documentation/unreal-engine/dynamic-asset-selection-in-unreal-engine).
- [GASDocumentation (tranek)](https://github.com/tranek/GASDocumentation) — теги (§4.2), Gameplay Cues (§4.8).
- Unity: [Build Animator Controllers](https://unity.com/how-to/build-animator-controllers),
  [Mecanim Performance](https://docs.unity3d.com/Manual/MecanimPeformanceandOptimization.html),
  [Animation Curves on Imported Clips](https://docs.unity.com/en-us/engine/current/manual/animation-section/animation-mecanim/animation-clips/animation-curves-on-imported-clips),
  [Coupling Animation and Navigation](https://docs.unity3d.com/ru/2021.1/Manual/nav-CouplingAnimationAndNavigation.html),
  [StateMachineBehaviour](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/statemachinebehaviour);
  [Animancer — переход с контроллеров](https://kybernetik.com.au/animancer/docs/manual/animator-controllers/conversion).
- [Naughty Dog: melee в The Last of Us Part II](https://www.gamedeveloper.com/design/how-naughty-dog-defined-melee-attacks-and-behaviors-in-i-the-last-of-us-part-ii-i-);
  [DOOM 2016: AI и анимация](https://gamedeveloper.com/programming/video-bringing-hell-to-life----ai-and-animation-in-i-doom-i-);
  [Hitman: Animation-Driven Locomotion](https://www.gdcvault.com/play/1015614/Animation-Driven-Locomotion-for-Smoother).
- [Audiokinetic: шаги и материалы](https://blog.audiokinetic.com/footsteps-material-management-using-wwise-unreal-engine-4-unity-3d/);
  [Feel — MMF Player](https://feel-docs.moremountains.com/mmf-player.html).
- Не прочитаны полностью: Game AI Pro 2, гл. 12 (Anguelov) и глава про «двойняшек» анимации (закрыты подпиской).

## E.4 Внутренние источники проекта

| Источник | Что внутри |
|---|---|
| Архитектурное руководство ИИ проекта **Atone** (Unreal 5, 1993 строки) | координатор боя, директор, поле потока, арбитр движения, LOD и сон, три яруса тестов, названный техдолг (A.12) |
| **Учебный AI-фреймворк** (10 проектов Unity, ~4700 прочитанных строк кода) | UtilityAI, GOAP, HTN, BT, FSM, Blackboard, TeamAI, навигация по сетке с JPS, карты влияния на джобах, тесты (A.13) |

Разборы обоих — в [Research_AI_Games §12](Research_AI_Games.md) и [Research_AI_Code](Research_AI_Code.md);
полная библиография с индексом всех 158 глав и проверенными ссылками —
[Research_AI_Sources](Research_AI_Sources.md).

---

# Приложение F. Глоссарий RU → EN

| Русский | English | Пояснение |
|---|---|---|
| агент | agent | сущность с восприятием, решением и действием |
| актуатор, «виртуальный геймпад» | actuator | единственный канал от ИИ к телу |
| арбитраж | arbitration | выбор одного действия из нескольких желающих |
| ассет-фабрика | asset factory | сериализуемый класс, создающий делегат на агента |
| барк | bark | короткая реплика, объясняющая решение игроку |
| бронь | booking / claim | «этот объект мой» с TTL |
| вето (оси) | veto | ось не может дать значение → вариант не рассматривается |
| восприятие | perception / senses | зрение, слух, память, стимулы |
| вердикт достижимости | reachability verdict | `Reachable/Detour/Unreachable/OffGraph` вместо bool |
| выбор действия | action selection | «что делать сейчас» |
| дерево поведения | behavior tree (BT) | композиты, декораторы, листья; `Running/Success/Failure` |
| декоратор | decorator | узел с одним ребёнком, меняющий его результат |
| директор, мета-ИИ | director / meta-AI | слой над агентами, управляющий темпом |
| дискретизация | discretization | «7.3 м» → `Near`: превращение непрерывного в символ |
| жетон атаки | attack token / lease | право бить цель (ёмкость и веса) |
| зона обзора, конус | vision cone | форма области видимости |
| интерес (зацепка) | interest / point of interest | то, что агент идёт проверить |
| карта влияния | influence map | слой «кто где силён / где опасно» |
| карта простреливаемости | exposure map | откуда агента видно |
| карта контекста | context map | interest/danger по направлениям |
| классная доска | blackboard | типизированное хранилище фактов агента/группы/мира |
| колонна подхода | approach queue | масштаб скорости внутри строя на одном маршруте |
| кривая отклика | response curve | преобразование нормализованного входа в оценку |
| линия фронта | frontline | пространственное отношение группы и врага |
| машина состояний | finite-state machine (FSM) | состояния и переходы |
| намерение | intent | «что хочу» без того, как это сделать телом |
| ось оценки | consideration | одна составляющая utility-оценки |
| планировщик | planner | GOAP (действия с пред/постусловиями), HTN (декомпозиция задач) |
| поиск пути | pathfinding | A\*, Dijkstra, JPS, поле потока, HPA\* |
| поле потока | flow field | направление в каждой клетке к ближайшей цели |
| полоса (сектор фронта) | lane | личный сектор агента на фронте |
| постановочный агент | choreography bot | агент, вечно делающий одно, для измерения боевых фич |
| прерывание | abort / interrupt | насильственное прекращение текущего поведения |
| пространственный запрос | spatial query (EQS / TPS) | «найди точку, откуда…» |
| пространственный индекс | spatial index / hash | структура для поиска соседей |
| расфазировка | phase offset / jitter | разведение периодических тиков по кадрам |
| реакция (время реакции) | reaction time | 0.2 с простая, 0.4 с с распознаванием |
| ролсет, профиль роли | roleset / role profile | ёмкости и числа роли в отряде |
| роль | role | под-поведение, выданное координатором |
| рулевое поведение | steering behavior | seek/flee/arrive/separation… |
| сенсор | sensor | источник фактов для blackboard |
| сигнал | signal | сообщение между агентами |
| слой тела | body layer | состояние тела, ограничивающее действия |
| сон (LOD) | sleep | агент не думает вовсе: сенсоры и тики сняты |
| стадия, распределитель позиций | stage manager | центральный распределитель слотов вокруг цели |
| стычка | engagement | цель + ростер агентов, которые ею заняты |
| телеграф (замах) | telegraph / wind-up | предупреждающая фаза атаки |
| толпа | crowd | масса агентов с общим полем и LOD |
| точка прыжка | jump point | узел, который раскрывает JPS вместо всех соседей |
| уровень детализации ИИ | AI LOD | частота решений и упрощение по важности |
| умение | ability / skill | действие с кулдауном, дистанцией и весом |
| умный объект | smart object | объект мира, предлагающий агенту взаимодействие |
| утилитарный ИИ | utility AI | оценка вариантов числом и выбор лучшего |
| фланкирование | flanking | обход цели сбоку или с тыла |
| шина сигналов | signal / event bus | рассылка стимулов по фракции, радиусу, отряду |
| штамп (влияния) | stamp / template | предпосчитанный «след» источника на карте влияния |
| эскалация | escalation | переход с дешёвого слоя навигации на дорогой |
| ярус внимания | attention tier | Active / Near / Idle / Asleep |

---

## Куда дальше по нашему модулю

Справочник отвечает на вопрос «как делать ИИ вообще». Про **наш** модуль:

- [AI](AI.md) — свод принципов, архитектура `AIKit` и состояние дел.
- [AIKit](AIKit.md) — спек: сборки, API, инварианты, принятые решения.
- [AIKit_Plan](AIKit_Plan.md) — план работ (переделывается по этому справочнику).

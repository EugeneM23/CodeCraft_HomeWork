# 04. Elements: действия и команды

`Atomic.Elements` выносит «сделать что-то» в отдельный контракт — `IAction`, пять перегрузок по арности (0–4 аргумента). Поверх него живут готовые реализации: обёртка над делегатом (`InlineAction`), группа обработчиков (`CompositeAction`), логирующая заглушка (`LogAction`) и четыре Unity-класса, через которые действие попадает в сцену и в инспектор. `ICommand` добавляет к действию две вещи: набор условий, который решает, выполнять ли вызов, и сигнал `OnEvent`, который срабатывает после успешного выполнения. Вместе это «исполнить сейчас» — половина потока Request-Condition-Action-Event; отложенную половину закрывают `IRequest`/`Request`.

Основной источник ошибок в этой области не в логике, а в том, что здесь сосуществуют два мира: делегаты (`Action<...>`, `Func<...>`) и объекты (`IAction`, `IFunction`). Конструкторы, расширения и поля подписаны то на один, то на другой, и почти все примеры из upstream-документации, которые не компилируются, спотыкаются именно об это. Ниже каждая подпись сверена с исходниками в `Assets/Plugins/Atomic/Elements/Scripts`; расхождения с документацией помечены явно.

## Модель

**Действие — это объект, а не метод.** `IAction` не наследует `IDisposable`, не имеет состояния по контракту и не знает, кто его вызывает. Один эффект игры — один класс. Это даёт две вещи: эффект можно положить в поле, передать, сериализовать (если класс помечен `[Serializable]`) и подменить в тесте; и его можно скомпоновать с другими без наследования.

**Арности 0–4, параметры контравариантны.** Все пять интерфейсов объявляют свои параметры как `in`, поэтому `IAction<object>` присваивается в `IAction<string>`. Обработчик стоит писать на самом широком типе, который имеет смысл. Пятой арности нет: дальше четырёх аргументов упаковывайте их в структуру.

**Два мира: делегаты и объекты.** Держите в голове, что чем подписан каждый приёмник:

| Приёмник | Что принимает |
|---|---|
| `CompositeAction(params …)` | делегаты `Action<...>` |
| `ICompositeAction` (список внутри) | делегаты `Action<...>` |
| `ICommand.AddCondition` / `AddAction` | делегаты `Func<...,bool>` / `Action<...>` |
| `Extensions.AddCondition` / `AddAction` (Commands) | объекты `IFunction<...,bool>` / `IAction<...>` |
| `MonoActionConfigurable.actions` | объекты `IAction<...>` через `[SerializeReference]` |
| `MonoActionComposite.actions` | компоненты `MonoAction<...>` через `[SerializeField]` |
| `Extensions.InvokeRange` | объекты `IAction<...>` |

Переход объект → делегат делается группой методов: `myAction.Invoke`. Обратный переход — `new InlineAction(myDelegate)` или неявный оператор.

**Команда — это действие плюс затвор плюс сигнал.** `ICommand : IAction, ISignal`. Условия — массив `Func<bool>`, проверяются по порядку с коротким замыканием на первом `false`. `Invoke()` при неуспехе молча ничего не делает, `TryInvoke()` возвращает `bool`. После успешного выполнения действия поднимается `OnEvent`. Поскольку `ICommand : IAction`, команду можно положить в любой слот, который ждёт `IAction` — затвор от этого не исчезает, просто вызывающий не узнаёт результата.

**Асимметрия арностей между Actions и Functions.** `IAction` доходит до 4 аргументов, `IFunction` — только до 2. Поэтому условия-объекты (`IFunction<...,bool>`, `IPredicate`) существуют только для арностей 0, 1 и 2, и расширения в `Commands/Extensions.cs` обрываются на `ICommand<T1,T2>`. Для `ICommand<T1,T2,T3>` условие можно задать только лямбдой или группой методов.

**Unity-слой — четыре разных роли, не взаимозаменяемые.**

- `MonoAction` — абстрактный компонент. Действие, которое *живёт в сцене* и которое дизайнер тянет мышью в `[SerializeField] MonoAction`.
- `MonoActionConfigurable` — компонент с массивом `[SerializeReference] IAction[]`. Дизайнер собирает поведение из обычных `[Serializable]`-классов, не создавая новых скриптов. Документация прямо называет `[SerializeReference]` последним средством.
- `MonoActionComposite` — компонент с массивом `[SerializeField] MonoAction[]`, то есть обычными ссылками на компоненты. Устойчив к рефакторингу, потому что данные — ссылки на объекты, а не сериализованные типы.
- `MonoActionReference` — `[Serializable]`-объект с одним полем `MonoAction`. Мост: в массив `[SerializeReference] IAction[]` нельзя положить `MonoBehaviour`, поэтому туда кладут `MonoActionReference` и целят им в компонент сцены.

**Action против Request.** `IRequest : IAction`. Действие исполняется в момент вызова. Запрос запоминает «меня просили» и исполняется тем, кто вызовет `Consume()` — обычно в другой фазе кадра (`Tick` производит, `FixedTick` потребляет). Если эффект зависит от физики или от порядка систем, нужен запрос, а не действие.

## API

### Контракты

```csharp
// Actions/IAction.cs
public interface IAction                                { void Invoke(); }
public interface IAction<in T>                          { void Invoke(T arg); }
public interface IAction<in T1, in T2>                  { void Invoke(T1 arg1, T2 arg2); }
public interface IAction<in T1, in T2, in T3>           { void Invoke(T1 args1, T2 args2, T3 args3); }
public interface IAction<in T1, in T2, in T3, in T4>    { void Invoke(T1 args1, T2 args2, T3 args3, T4 args4); }
```

Базовый контракт всей области. Обратите внимание на имена параметров: `arg` / `arg1, arg2` для арностей 1–2, но `args1, args2, args3[, args4]` для арностей 3–4. Документация показывает `arg1..argN` везде; именованные аргументы, написанные по документации, для арностей 3–4 не скомпилируются.

```csharp
// Actions/ICompositeAction.cs
public interface ICompositeAction                             : IAction, IReactiveList<Action>;
public interface ICompositeAction<T>                          : IAction<T>, IReactiveList<Action<T>>;
public interface ICompositeAction<T1, T2>                     : IAction<T1, T2>, IReactiveList<Action<T1, T2>>;
public interface ICompositeAction<T1, T2, T3>                 : IAction<T1, T2, T3>, IReactiveList<Action<T1, T2, T3>>;
public interface ICompositeAction<T1, T2, T3, T4>             : IAction<T1, T2, T3, T4>, IReactiveList<Action<T1, T2, T3, T4>>;
```

Композит — одновременно действие и наблюдаемый изменяемый список **делегатов**. Объявляйте поле как `ICompositeAction<T>`, когда вызывающий должен и вызывать, и менять состав; как `IAction<T>` — когда только вызывать.

```csharp
// Commands/ICommand.cs
public interface ICommand : IAction, ISignal
{
    bool CanInvoke();
    bool TryInvoke();
    ICommand AddCondition(Func<bool> condition);
    ICommand RemoveCondition(Func<bool> condition);
    ICommand AddAction(Action action);
    ICommand RemoveAction(Action action);
}

public interface ICommand<T> : IAction<T>, ISignal<T>
{
    bool CanInvoke(T arg);
    bool TryInvoke(T arg);
    ICommand<T> AddCondition(Func<T, bool> condition);
    ICommand<T> RemoveCondition(Func<T, bool> condition);
    ICommand<T> AddAction(Action<T> action);
    ICommand<T> RemoveAction(Action<T> action);
}
```

И так же для `ICommand<T1,T2>`, `ICommand<T1,T2,T3>`, `ICommand<T1,T2,T3,T4>` — `CanInvoke`/`TryInvoke` по арности, `AddCondition(Func<...,bool>)`, `AddAction(Action<...>)`. Все мутаторы возвращают интерфейс, поэтому цепочка работает и через переменную типа `ICommand`.

`ICommand<T>` не несёт ни `in`, ни `out`, и не может: базы имеют противоположную вариантность — `IAction<in T>` и `ISignal<out T>`.

```csharp
// Events/ISignal.cs
public interface ISignal                                     { event Action OnEvent; }
public interface ISignal<out T>                              { event Action<T> OnEvent; }
public interface ISignal<out T1, out T2>                     { event Action<T1, T2> OnEvent; }
public interface ISignal<out T1, out T2, out T3>             { event Action<T1, T2, T3> OnEvent; }
public interface ISignal<out T1, out T2, out T3, out T4>     { event Action<T1, T2, T3, T4> OnEvent; }
```

Весь публичный контракт `ISignal` — одно событие. Метода `Subscribe` в интерфейсе **нет**: это extension-метод (см. ниже).

```csharp
// Requests/IRequest.cs
public interface IRequest : IAction
{
    bool Required { get; }
    bool Consume();
}

public interface IRequest<T> : IAction<T>
{
    bool Required { get; }
    T Arg { get; }
    bool TryGet(out T arg);
    bool Consume(out T arg);
}
```

Отложенная половина потока. Арности до 4 включительно (`Arg1..Arg4`, `TryGet(out …)`, `Consume(out …)`). Конкретные классы — `Request`, `Request<T>` … `Request<T1,T2,T3,T4>` (`public class Request : IRequest` в `Requests/Request.cs`). Типов с именами `BaseRequest` / `BaseEvent` в кодовой базе нет.

```csharp
// Functions/IFunction.cs
public interface IFunction<out R>                { R Invoke(); }
public interface IFunction<in T, out R>          { R Invoke(T args); }
public interface IFunction<in T1, in T2, out R>  { R Invoke(T1 args1, T2 args2); }

// Functions/IPredicate.cs
public interface IPredicate            : IFunction<bool>;
public interface IPredicate<in T>      : IFunction<T, bool>;
public interface IPredicate<in T1, in T2> : IFunction<T1, T2, bool>;
```

Идиоматический тип условия-объекта — `IPredicate`. Потолок — два аргумента, и это жёстко ограничивает набор расширений для команд. У `IFunction<out R>` **нет** члена `Value` — только `Invoke()`. `Value` живёт на `IValue<out T> : IFunction<T>, IValue` (`Values/IValue.cs`).

### Реализации действий

```csharp
// Actions/Core/InlineAction.cs
public class InlineAction : IAction
{
    private readonly Action action;
    public InlineAction(Action action) => this.action = action ?? throw new ArgumentNullException(nameof(action));
    public static implicit operator InlineAction(Action value) => new(value);
    public void Invoke() => this.action.Invoke();
    public override string ToString() => this.action.Method.Name;
}
```

Та же форма для всех пяти арностей (`InlineAction<T>`, … `InlineAction<T1,T2,T3,T4>`), у каждой — свой неявный оператор из соответствующего `Action<...>`. Все пять — `public class`, не `sealed`, и ни одна **не** помечена `[Serializable]`: в слот `[SerializeReference]` их положить нельзя. Берите, когда логику проще держать рядом с местом установки, чем выделять в класс.

```csharp
// Actions/Core/CompositeAction.cs
[Serializable]
public class CompositeAction : ReactiveLinkedList<Action>, ICompositeAction
{
    public CompositeAction() { }                                  // только для инспектора
    public CompositeAction(params Action[] actions) : base(actions) { }
    public CompositeAction(IEnumerable<Action> actions) : base(actions) { }

    public void Invoke()
    {
        foreach (Action action in this)
            action.Invoke();
    }
}
```

Конструкторы принимают **делегаты**, не `IAction`. Это главное расхождение с документацией в области. Наследуется от `ReactiveLinkedList<Action>`, то есть каждый `CompositeAction` — ещё и `IDisposable`, и (в Unity-сборке: часть `ReactiveLinkedList<T>` с `ISerializationCallbackReceiver` объявлена под `#if UNITY_5_3_OR_NEWER`) `ISerializationCallbackReceiver`, и полноценный изменяемый наблюдаемый список (`Add`/`Insert`/`Remove`/`Clear`/индексатор/`Count` + события `OnItemAdded`, `OnItemRemoved`, `OnItemChanged`, `OnStateChanged`). Остальные четыре арности идентичны по форме.

```csharp
// Actions/Core/LogAction.cs
[Serializable]
public sealed class LogAction : IAction
{
    [SerializeField] private LogType _logType;   // под UNITY_5_3_OR_NEWER
    [SerializeField] private string _message;    // под UNITY_5_3_OR_NEWER

    public LogAction() { }                                        // только для инспектора
    public LogAction(string message, LogType logType = LogType.Log);   // вне Unity: LogAction(string message)

    public void Invoke();      // Debug.unityLogger.Log(_logType, _message) / Console.WriteLine(_message)
    public override string ToString();   // "{_logType}: {_message}" / _message
}
```

Единственное готовое действие с данными. `[Serializable]` и `sealed` — ставится в любой `[SerializeReference] IAction`-слот. Класс называется `LogAction`, файл документации — `PrintAction.md`.

```csharp
// Actions/Extensions.cs
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InvokeRange(this IEnumerable<IAction> actions)
    {
        if (actions != null)
            foreach (IAction action in actions)
                action?.Invoke();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InvokeRange(this IAction[] actions)
    {
        if (actions != null)
            for (int i = 0, count = actions.Length; i < count; i++)
                actions[i]?.Invoke();
    }
    // и так же <T>, <T1,T2>, <T1,T2,T3>, <T1,T2,T3,T4> — в обеих формах, всего 10 перегрузок
}
```

Групповой вызов с проверкой и коллекции, и каждого элемента на `null`. Класс объявлен `static partial`, и Commands расширяет тот же `Extensions`, поэтому одного `using Atomic.Elements;` достаточно.

### Unity

```csharp
// Actions/Unity/MonoAction.cs  (весь файл под #if UNITY_5_3_OR_NEWER)
[HelpURL("…/Docs/Elements/Actions/MonoAction.md")]
public abstract class MonoAction : MonoBehaviour, IAction
{
    public abstract void Invoke();
}

public abstract class MonoAction<T>                 : MonoBehaviour, IAction<T>                 { public abstract void Invoke(T arg); }
public abstract class MonoAction<T1, T2>            : MonoBehaviour, IAction<T1, T2>            { public abstract void Invoke(T1 arg1, T2 arg2); }
public abstract class MonoAction<T1, T2, T3>        : MonoBehaviour, IAction<T1, T2, T3>        { public abstract void Invoke(T1 arg1, T2 arg2, T3 arg3); }
public abstract class MonoAction<T1, T2, T3, T4>    : MonoBehaviour, IAction<T1, T2, T3, T4>    { public abstract void Invoke(T1 arg1, T2 arg2, T3 arg3, T4 arg4); }
```

Чистые абстрактные компоненты: ни полей, ни `Awake`, ни `Update`. Наследуйте, когда действие должно быть видимым объектом сцены.

```csharp
// Actions/Unity/MonoActionConfigurable.cs
[AddComponentMenu("Atomic/Elements/Action")]
public class MonoActionConfigurable : MonoAction
{
    [SerializeReference] public IAction[] actions;

    public override void Invoke()
    {
        if (this.actions != null)
            for (int i = 0, count = this.actions.Length; i < count; i++)
                this.actions[i]?.Invoke();
    }
}

public abstract class MonoActionConfigurable<T> : MonoAction<T>
{
    [SerializeReference] public IAction<T>[] actions;
    public override void Invoke(T arg1) { /* тот же цикл */ }
}
```

Неродовой вариант — `public class` с `[AddComponentMenu]`, его можно добавить на объект напрямую. Все четыре родовых — `public abstract class`, под каждый нужен закрытый подкласс.

```csharp
// Actions/Unity/MonoActionComposite.cs
[AddComponentMenu("Atomic/Elements/Action Composite")]
public class MonoActionComposite : MonoAction
{
    [Space, SerializeField] public MonoAction[] actions;

    public override void Invoke()
    {
        if (this.actions == null)
            return;

        for (int i = 0, count = this.actions.Length; i < count; i++)
        {
            MonoAction action = this.actions[i];
            if (action) action.Invoke();
        }
    }
}

public class MonoActionComposite<T> : MonoAction<T>
{
    [Space, SerializeField] public MonoAction<T>[] actions;
    public override void Invoke(T arg) { /* тот же цикл с if (action) */ }
}
```

Проверка `if (action)` — неявный `bool` от `UnityEngine.Object`: отфильтровывает и `null`, и уничтоженные объекты, чего `!= null` и `?.` не делают. Все родовые варианты здесь конкретные `public class` (в отличие от `MonoActionConfigurable`), но для добавления на объект всё равно нужен закрытый подкласс. Арности 1 и 2 несут `[Space, SerializeField]`, арности 3 и 4 — просто `[SerializeField]`; `[AddComponentMenu]` есть только у неродового.

```csharp
// Actions/Unity/MonoActionReference.cs
[Serializable]
public sealed class MonoActionReference : IAction
{
    [SerializeField] public MonoAction action;

    public MonoActionReference() { }                              // только для инспектора
    public MonoActionReference(MonoAction action) => this.action = action;

    public void Invoke()
    {
        if (this.action)
            this.action.Invoke();
    }
}
```

Все пять арностей `sealed` и `[Serializable]`. Уничтоженная цель — тихий no-op, а не `MissingReferenceException`.

### Команды

```csharp
// Commands/Command.cs
public sealed class Command : ICommand
{
    public event Action OnEvent;

    private Func<bool>[] _conditions = new Func<bool>[4];
    private int _count;
    private Action _action;

    public bool CanInvoke()
    {
        for (int i = 0; i < _count; i++)
            if (!_conditions[i]())
                return false;

        return true;
    }

    public bool TryInvoke()
    {
        if (!CanInvoke())
            return false;

        _action?.Invoke();
        OnEvent?.Invoke();
        return true;
    }

    public void Invoke()
    {
        if (!CanInvoke())
            return;

        _action?.Invoke();
        OnEvent?.Invoke();
    }

    public ICommand AddCondition(Func<bool> condition)
    {
        if (_count == _conditions.Length)
            Array.Resize(ref _conditions, _conditions.Length * 2);

        _conditions[_count++] = condition;
        return this;
    }

    public ICommand RemoveCondition(Func<bool> condition)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_conditions[i] == condition)
            {
                for (int j = i; j < _count - 1; j++)
                    _conditions[j] = _conditions[j + 1];

                _conditions[--_count] = null;
                break;
            }
        }

        return this;
    }

    public ICommand AddAction(Action action)    { _action += action; return this; }
    public ICommand RemoveAction(Action action) { _action -= action; return this; }
}
```

Класс по умолчанию. Все пять арностей (`Command`, `Command<T>` … `Command<T1,T2,T3,T4>`) — `sealed` и имеют идентичную раскладку: массив условий на 4 элемента с удвоением, действия в одном multicast-делегате. `RemoveCondition` сдвигает хвост, то есть порядок условий сохраняется. `Command` **не** реализует `IDisposable` — метода `Dispose` нет, чистить состав приходится вручную.

```csharp
// Commands/ThreadSafeCommand.cs
public sealed class ThreadSafeCommand : ICommand, MainThreadDispatcher.IFlushable
{
    public event Action OnEvent;

    private readonly object _lock = new();
    private Func<bool>[] _conditions = new Func<bool>[4];
    private int _conditionCount;
    private Action _action;

    public bool CanInvoke()
    {
        Func<bool>[] conditions;
        int count;

        lock (_lock)
        {
            conditions = _conditions;
            count = _conditionCount;
        }

        for (int i = 0; i < count; i++)
            if (!conditions[i]())
                return false;

        return true;
    }

    public void Invoke()
    {
        Action action;
        lock (_lock)
            action = _action;

        if (!CanInvoke())        // ещё один захват замка
            return;

        action?.Invoke();
        MainThreadDispatcher.MarkDirty(this);
    }

    public bool TryInvoke()      // то же плюс return true/false
    { /* … */ }

    void MainThreadDispatcher.IFlushable.Flush()
    {
        Action handler = OnEvent;
        handler?.Invoke();
    }

    public ICommand RemoveCondition(Func<bool> condition)
    {
        lock (_lock)
        {
            for (int i = 0; i < _conditionCount; i++)
            {
                if (_conditions[i] == condition)
                {
                    _conditions[i] = _conditions[_conditionCount - 1];   // swap-with-last: порядок НЕ сохраняется
                    _conditions[--_conditionCount] = null;
                    break;
                }
            }
        }

        return this;
    }
    // AddCondition / AddAction / RemoveAction меняют состояние под тем же замком
}
```

Родовые варианты **не** являются копиями неродового. Их форма другая:

```csharp
public sealed class ThreadSafeCommand<T> : ICommand<T>, MainThreadDispatcher.IFlushable
{
    public event Action<T> OnEvent;

    private readonly object _lock = new();
    private Func<T, bool>[] _conditions = new Func<T, bool>[4];
    private int _conditionCount;
    private Action<T> _action;

    private T _arg;
    private bool _hasValue;

    public void Invoke(T arg)
    {
        Action<T> action;
        Func<T, bool>[] conditions;
        int count;

        lock (_lock)                 // ОДИН снимок: действие + условия + счётчик
        {
            action = _action;
            conditions = _conditions;
            count = _conditionCount;
        }

        for (int i = 0; i < count; i++)
        {
            Func<T, bool> cond = conditions[i];
            if (!cond(arg))
                return;              // CanInvoke() не вызывается
        }

        action?.Invoke(arg);

        lock (_lock)                 // второй захват — только на успешном пути
        {
            _arg = arg;
            _hasValue = true;
        }

        MainThreadDispatcher.MarkDirty(this);
    }

    void MainThreadDispatcher.IFlushable.Flush()
    {
        T arg;
        Action<T> handler;

        lock (_lock)
        {
            if (!_hasValue)          // гейт, которого нет у неродового
                return;

            arg = _arg;
            _hasValue = false;
            handler = OnEvent;
        }

        handler?.Invoke(arg);
    }
}
```

Разница существенна на практике: у родовых `Flush()` под замком снимает флаг, поэтому одна публикация доставляет ровно один (последний) аргумент, а повторный flush без нового вызова — no-op. Неродовой `Flush()` читает `OnEvent` без замка и без флага, поэтому может поднять `OnEvent` просто потому, что диспетчер решил его сбросить.

```csharp
// Commands/Extensions.cs — ровно 12 перегрузок, арности 0, 1 и 2
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddCondition(this ICommand command, IFunction<bool> condition) =>
        command.AddCondition(condition.Invoke);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveCondition(this ICommand command, IFunction<bool> condition) =>
        command.RemoveCondition(condition.Invoke);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddAction(this ICommand command, IAction action) =>
        command.AddAction(action.Invoke);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveAction(this ICommand command, IAction action) =>
        command.RemoveAction(action.Invoke);

    // + те же четыре для ICommand<T> (IFunction<T,bool> / IAction<T>)
    // + те же четыре для ICommand<T1,T2> (IFunction<T1,T2,bool> / IAction<T1,T2>)
}
```

Мост между объектным и делегатным миром. Перегрузок для арностей 3 и 4 нет и быть не может: `IFunction` объявлен только до двух аргументов. Все эти методы возвращают `void`, а не команду. В документации Commands они отсутствуют полностью.

`MainThreadDispatcher` — **внутренний** тип (`internal sealed class MainThreadDispatcher : MonoBehaviour` в `Utils/MainThreadDispatcher.cs`). Ни сам класс, ни вложенный `IFlushable`, ни `MarkDirty`/`IsMainThread` не доступны из кода вне сборки `Atomic.Elements` (`AssemblyInfo.cs` открывает `internal` только для `Atomic.Elements.Tests.EditMode` и `.PlayMode`). Это не API, которым пользуются — это механизм, который достаётся вам вместе с `ThreadSafeCommand`.

### Подписка на OnEvent

```csharp
// Events/Extensions.cs
public static Subscription Subscribe(this ISignal it, Action action) => new(it, action);
public static Subscription Subscribe(this ISignal it, IAction action) => new(it, action.Invoke);
public static Subscription<T> Subscribe<T>(this ISignal<T> it, Action<T> action) => new(it, action);
public static Subscription<T> Subscribe<T>(this ISignal<T> it, IAction<T> action) => new(it, action.Invoke);
// … до <T1,T2,T3,T4>, в обеих формах (делегат и объект)

public static void SubscribeRange(this ISignal it, IEnumerable<IAction> actions);
public static void UnsubscribeRange(this ISignal it, IEnumerable<IAction> actions);
// … до <T1,T2,T3,T4>
```

```csharp
// Events/Subscription.cs
public readonly struct Subscription : IDisposable
{
    private readonly ISignal signal;
    private readonly Action action;

    public Subscription(ISignal signal, Action action)
    {
        this.signal = signal ?? throw new ArgumentNullException(nameof(signal));
        this.action = action ?? throw new ArgumentNullException(nameof(action));
        this.signal.OnEvent += this.action;
    }

    public void Dispose()
    {
        if (this.signal != null)
            this.signal.OnEvent -= this.action;
    }
}
```

`Subscribe` возвращает `readonly struct`, а не класс. Конструктор подписывается как побочный эффект, `Dispose()` терпим к `default` (поле `signal` равно `null` — проверка есть). `SubscribeRange`/`UnsubscribeRange` — парная групповая проводка; `SubscribeRange` ничего не возвращает, так что отписываться придётся через `UnsubscribeRange` с тем же набором.

### Тестирование

```csharp
// Tests/Substitutions/ActionSpy.cs — сборка Atomic.Elements.Tests.Substitutions
public sealed class ActionSpy : IAction
{
    public int InvokeCount => _invokeCount;
    public bool WasInvoked => _invokeCount > 0;

    private int _invokeCount;

    public void Invoke() => _invokeCount++;
}
```

Готовый test double для `IAction`. Сборка помечена `includePlatforms: ["Editor"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `autoReferenced: true` — то есть из вашей EditMode-тестовой сборки он виден без дополнительной настройки. Используйте его вместо самодельного `bool invoked` в каждом тесте.

## Идиомы

### Один эффект — один `sealed` класс нужной арности

```csharp
public sealed class DealDamageAction : IAction<Character, int>
{
    public void Invoke(Character character, int damage)
    {
        character.TakeDamage(damage);
    }
}
```

`sealed`, потому что расширять эффект наследованием здесь не предполагается — его компонуют. Арность выбирается по тому, что эффект действительно должен получить извне; всё остальное — поля, заданные в конструкторе.

### Поле — интерфейс, конструктор — реализация

```csharp
Character enemy = ...;

IAction<Character, int> action = new DealDamageAction();
action.Invoke(enemy, 5);
```

Тип переменной и тип поля — всегда интерфейс. Это то, что позволяет подменить реализацию на `ActionSpy` в тесте, на `LogAction` на время отладки и на `CompositeAction` при расширении — не меняя вызывающий код.

### Валидация — внутри действия, с конкретным исключением

```csharp
public sealed class MoveResourcesAction : IAction<Storage, Storage, int>
{
    public void Invoke(Storage source, Storage destination, int amount)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (amount <= 0)
            throw new ArgumentException("Amount of resources must be greater than zero.", nameof(amount));
        if (source.AvailableResources < amount)
            throw new InvalidOperationException("Source does not have enough resources.");
        if (destination.FreeCapacity < amount)
            throw new InvalidOperationException("Destination does not have enough free capacity.");

        source.SpendResources(amount);
        destination.EarnResources(amount);
    }
}
```

Контракт действия проверяется там, где он объявлен, а не у каждого вызывающего. `ArgumentException` для неверных входов, `InvalidOperationException` для неподходящего состояния — разделение, по которому вызывающий может решить, это баг или просто «нельзя сейчас». Если «нельзя сейчас» — нормальная ветка, вам нужен не `IAction`, а `ICommand` с условием.

### `InlineAction` — для одноразовой логики, группа методов — когда метод уже есть

```csharp
IAction action = new InlineAction(() => Console.WriteLine("Hello World!"));
action.Invoke();

// метод уже существует — класс не нужен вообще
IAction destroy = new InlineAction<GameObject>(GameObject.Destroy);
```

`InlineAction` оправдан, когда логика короткая и живёт ровно в одном месте — обычно в установщике сущности. Как только её хочется переиспользовать или протестировать отдельно — выделяйте класс.

### `CompositeAction`: конструктор принимает делегаты

```csharp
// ПРАВИЛЬНО
IAction startup = new CompositeAction(
    new ActivatePlayerAction().Invoke,       // группа методов от IAction
    new ActivateEnemiesAction().Invoke,
    new ActivateGameTimerAction().Invoke
);

startup.Invoke();
```

```csharp
// НЕ КОМПИЛИРУЕТСЯ — именно так написано в Docs/Elements/Actions/Manual.md
IAction composite = new CompositeAction(
    new InlineAction(() => Console.WriteLine("Action 1")),
    new InlineAction(() => Console.WriteLine("Action 2"))
);
```

Конструктор — `CompositeAction(params Action[] actions)`. `InlineAction` имеет неявный оператор *из* `Action`, но не *в* `Action`, так что преобразования не будет. Собственный тест фреймворка (`Tests/EditMode/Actions/CompositeActionTests.cs`) подтверждает рабочую форму: `new CompositeAction(a1, a2, a3)`, где каждый `a` — `new Action(() => …)`. Чистая форма из лямбд:

```csharp
var composite = new CompositeAction(
    () => Console.WriteLine("Action 1"),
    () => Console.WriteLine("Action 2")
);
```

### Композит живёт — значит, его надо освобождать

```csharp
public sealed class WeaponPresenter : IDisposable
{
    private readonly CompositeAction _onFire = new(
        () => Debug.Log("muzzle flash"),
        () => Debug.Log("shell eject")
    );

    public IAction OnFire => _onFire;

    public void Dispose() => _onFire.Dispose();   // Clear() + сброс всех реактивных событий
}
```

`CompositeAction` наследует `ReactiveLinkedList<Action>`, то есть реализует `IDisposable`, и его `Dispose()` делает `Clear()` и обнуляет `OnItemAdded`/`OnItemRemoved`/`OnItemChanged`/`OnStateChanged`. Композит, который держат в поле, но никогда не освобождают, держит делегаты, а те — замкнутые сущности. Для набора таких освобождений в фреймворке есть `DisposableComposite` (`Utils/DisposableComposite.cs`: `public class DisposableComposite : DisposableComposite<IDisposable>`).

### `InvokeRange`: массив вместо `IEnumerable`

```csharp
// ПРАВИЛЬНО — индексный цикл, без энумератора
IAction[] actions = { action1, null, action2 };
actions.InvokeRange();      // вызовет action1 и action2, null пропустит

IAction<GameObject>[] onSpawn = { logSpawn, registerInWorld };
onSpawn.InvokeRange(go);
```

```csharp
// ХУЖЕ — энумератор HashSet'а структурный, но ходим мы через интерфейс
IEnumerable<IAction> actions = new HashSet<IAction> { action1, null, action2 };
actions.InvokeRange();
```

Обе перегрузки корректны и обе безопасны по `null`, но массивная идёт по индексу с вынесенной границей и вообще не создаёт энумератор. Где тип хранения выбираете вы — выбирайте `IAction[]`.

### `MonoAction` + `[SerializeField]` для сценарной логики

```csharp
public sealed class HelloWorldAction : MonoAction
{
    public override void Invoke() => Debug.Log("Hello World!");
}

public sealed class GameStartup : MonoBehaviour
{
    [SerializeField] private MonoAction _action;

    private void Start() => _action.Invoke();
}
```

```csharp
public sealed class DestroyGameObjectAction : MonoAction<GameObject>
{
    public override void Invoke(GameObject go) => GameObject.Destroy(go);
}

public sealed class ActionTrigger : MonoBehaviour
{
    [SerializeField] private MonoAction<GameObject> _action;

    private void OnTriggerEnter(Collider collider) => _action.Invoke(collider.gameObject);
}
```

Поле объявлено абстрактным базовым типом, а не конкретным, — дизайнер подставляет любую реализацию. Ни `MonoAction`, ни его родовые варианты не имеют Unity-колбэков, так что компонент не стоит ничего, пока его не вызовут.

### Пустой закрытый подкласс для родового Unity-типа

```csharp
using Atomic.Elements;

public sealed class GameObjectMonoActionComposite : MonoActionComposite<GameObject>
{
}
```

Unity не умеет сериализовать открытый родовой компонент. Пустой закрытый подкласс — это не обходной путь, а штатный шаг: один файл на каждый аргумент, который реально нужен в инспекторе. Для `MonoActionConfigurable<T>` он обязателен ещё и потому, что все родовые варианты объявлены `abstract`.

### `MonoActionComposite` предпочтительнее `MonoActionConfigurable`

```csharp
// ПРЕДПОЧТИТЕЛЬНО — [SerializeField] MonoAction[]: данные это ссылки на компоненты,
// переименование классов их не ломает
public class MonoActionComposite : MonoAction
{
    [Space, SerializeField] public MonoAction[] actions;
}
```

```csharp
// ТОЛЬКО ЕСЛИ ИНАЧЕ НЕЛЬЗЯ — [SerializeReference] IAction[]:
// данные хранят имя типа, переименование или перенос класса теряет их
public class MonoActionConfigurable : MonoAction
{
    [SerializeReference] public IAction[] actions;
}
```

Это прямая рекомендация документации (`MonoActionsConfigurable.md`): «Using `[SerializeReference]` should be considered a last resort». `MonoActionConfigurable` берут там, где детьми должны быть обычные `[Serializable]`-объекты (`LogAction`, ваши собственные), а не компоненты сцены.

### `MonoActionReference` как мост между двумя мирами сериализации

```csharp
public sealed class HelloWorldMonoAction : MonoAction
{
    public override void Invoke() => Debug.Log("Hello World!");
}

public sealed class GameStartup : MonoBehaviour
{
    [SerializeField] private MonoActionReference _reference;

    private void Start() => _reference.Invoke();
}
```

В `[SerializeReference] IAction[]` нельзя положить `MonoBehaviour`. Кладёте туда `MonoActionReference` и целите его полем `action` в компонент сцены — получается `[SerializeReference]`-граф, у которого один лист находится в сцене.

### Fluent-команда: сначала условия, потом действие, потом `TryInvoke`

```csharp
IEntity player = ...;

ICommand jumpCommand = new Command()
    .AddCondition(() => player.GetIsGrounded().Value)       // дешёвое условие первым
    .AddCondition(() => player.GetStamina().Value >= 5)
    .AddAction(() =>
    {
        player.GetStamina().Value -= 5;
        player.GetRigidbody().AddForce(Vector3.up * 5f, ForceMode.Impulse);
    });

bool jumped = jumpCommand.TryInvoke();
```

Цепочка работает потому, что все `Add*` в `ICommand` возвращают интерфейс. Порядок условий значим: `CanInvoke` возвращает `false` на первом же неуспехе, так что самое дешёвое условие ставят первым.

### Параметризованная команда: аргумент идёт и в условие, и в действие

```csharp
ICommand<int> healCommand = new Command<int>();

healCommand
    .AddCondition(amount => player.IsAlive)
    .AddAction(amount => player.Health = Mathf.Min(player.MaxHealth, player.Health + amount));

bool healed = healCommand.TryInvoke(25);
```

Условие решает, можно ли вообще, действие приводит значение в допустимый диапазон. Зажим (`Mathf.Min`) — внутри действия, а не в условии: условие обязано быть без побочных эффектов и может вызываться много раз за кадр.

### `CanInvoke()` для состояния UI, `TryInvoke()` для исполнения

```csharp
// каждый кадр — только спрашиваем
_jumpButton.interactable = jumpCommand.CanInvoke();

// по нажатию — исполняем и узнаём результат
if (jumpCommand.TryInvoke())
    _jumpSfx.Play();
```

Это ровно то, что перечислено в Best Practices `Docs/Elements/Commands/Manual.md`: `TryInvoke()` — когда нужен результат, `CanInvoke()` — чтобы гасить кнопку, условия — без побочных эффектов. Отсюда прямо следует, что `CanInvoke` вызывается часто (и `TryInvoke` зовёт его ещё раз внутри), поэтому условие не должно ничего менять и не должно быть дорогим.

### Подписка: тип локальной переменной — `Subscription`, не `IDisposable`

```csharp
// ПРАВИЛЬНО — структура не упаковывается
Subscription sub = attackCommand.Subscribe(() => Debug.Log("Attack executed!"));
...
sub.Dispose();

// или, если область жизни совпадает с блоком
using var scoped = attackCommand.Subscribe(OnAttack);
```

```csharp
// УПАКОВЫВАЕТ — именно так написано в Docs/Elements/Commands/Manual.md
IDisposable subscription = attackCommand.Subscribe(() => Debug.Log("Attack executed!"));
subscription.Dispose();
```

Код компилируется в обоих вариантах; разница в том, что присваивание `readonly struct Subscription` в переменную типа `IDisposable` боксит её. Для одной подписки это неважно, для подписки на каждый спавн сущности — уже нет.

Если подписки нужно собрать в пачку, обратите внимание, что `DisposableComposite` — это `DisposableComposite<IDisposable>`, то есть `Add` тоже боксит. `DisposableComposite<Subscription>` боксить не будет, потому что вызов `Dispose()` по ограничению `where T : IDisposable` идёт как constrained call (это вывод из формы кода, а не измерение).

### Условия-объекты ломают цепочку

```csharp
// ПРАВИЛЬНО — объектные условия ставим отдельными вызовами
ICommand fire = new Command();
fire.AddCondition(hasAmmoPredicate);      // extension-перегрузка, возвращает void
fire.AddCondition(cooldownPredicate);
fire.AddAction(fireAction);               // extension-перегрузка, возвращает void
```

```csharp
// НЕ КОМПИЛИРУЕТСЯ — extension-перегрузки возвращают void, цепочку продолжить нечем
ICommand fire = new Command()
    .AddCondition(hasAmmoPredicate)       // void
    .AddAction(fireAction);
```

Ловушка срабатывает незаметно: в `ICommand` есть `AddCondition(Func<bool>)`, возвращающий `ICommand`, и extension `AddCondition(this ICommand, IFunction<bool>)`, возвращающий `void`. Выбор зависит от типа аргумента. Это задевает больше типов, чем кажется: `IPredicate : IFunction<bool>`, `IValue<T> : IFunction<T>`, значит `IVariable<bool>` и `ReactiveVariable<bool>` — тоже `IFunction<bool>`, и `command.AddCondition(entity.GetIsGrounded())` молча уйдёт в void-перегрузку. Если цепочка нужна — передавайте делегат: `command.AddCondition(entity.GetIsGrounded().Invoke)`.

Второе: `condition.Invoke` создаёт новый экземпляр делегата на каждый вызов. `RemoveCondition` всё равно находит его, потому что равенство делегатов сравнивает цель и метод, но аллокация происходит каждый раз. Если регистрация идёт не один раз на установку — кэшируйте делегат у себя.

### Поведение кэширует элементы в `Init`, в тике только оркестровка

```csharp
public sealed class JumpBehaviour : IEntityInit, IEntityFixedTick
{
    private IRequest _jumpRequest;
    private IValue<bool> _jumpCondition;      // НЕ IFunction<bool> — у него нет .Value
    private IAction _jumpAction;
    private IEvent _jumpEvent;

    public void Init(IEntity entity)
    {
        _jumpRequest = entity.GetJumpRequest();
        _jumpCondition = entity.GetJumpCondition();
        _jumpAction = entity.GetJumpAction();
        _jumpEvent = entity.GetJumpEvent();
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        if (_jumpRequest.Consume() && _jumpCondition.Value)
        {
            _jumpAction.Invoke();
            _jumpEvent.Invoke();
        }
    }
}
```

Все поиски элементов делаются один раз в `Init`, в тике нет ни разрешения ключей, ни аллокаций. Тип поля условия — `IValue<bool>` (или `IExpression<bool>`): в `Docs/BestPractices/RequestConditionActionEvent.md` поле объявлено `IFunction<bool>`, а читается как `_jumpCondition.Value`, и это не компилируется — `IFunction<out R>` объявляет только `Invoke()`. Альтернатива — оставить `IFunction<bool>` и писать `_jumpCondition.Invoke()`.

### Поток Request-Condition-Action-Event, собранный на реальных типах

```csharp
var entity = ...;

entity.AddJumpRequest(new Request());

var jumpCondition = new AndExpression();
jumpCondition.Add(() => entity.GetHealth().Value > 0);     // extension Add(this IExpression<R>, Func<R>)
jumpCondition.Add(() => entity.GetEnergy().Value >= 10);
entity.AddJumpCondition(jumpCondition);

entity.AddJumpAction(new InlineAction(
    () => entity.GetRigidbody().AddForce(Vector3.up, ForceMode.Impulse)
));
entity.AddJumpEvent(new Event());

entity.AddBehaviour<JumpBehaviour>();
```

Четыре элемента, каждый подменяется независимо: запрос — кто просит, условие — можно ли, действие — что происходит, событие — кто узнаёт. Конкретные классы — `Request` и `Event`, а не `BaseRequest`/`BaseEvent` (таких типов в кодовой базе нет). Конструкторы `AndExpression` принимают только `int capacity`, `params ExpressionMember<bool>[]` или `IEnumerable<ExpressionMember<bool>>`, а у `ExpressionMember<R>` нет неявного преобразования из делегата, поэтому лямбды добавляются через extension `Add` (`Expressions/Extensions.cs`) либо оборачиваются вручную: `new AndExpression(new ExpressionMember<bool>(() => …), …)`. Имена `AddJumpRequest`/`GetJumpCondition` — сгенерированные extension-методы, они зависят от ваших объявленных ключей (см. главу про кодогенерацию).

### Action для немедленного эффекта, Request для отложенного

```csharp
// немедленно: выстрел происходит в момент вызова
IEntity character = ...;
IAction fireAction = character.GetFireAction();
fireAction.Invoke();

// отложенно: ввод собирается в Tick, физика применяется в FixedTick
public sealed class MoveController : IEntityInit, IEntityTick
{
    private IRequest<Vector3> _moveRequest;

    public void Init(IEntity entity) => _moveRequest = entity.GetMoveRequest();

    public void Tick(IEntity entity, float deltaTime)
    {
        float dx = Input.GetAxis("Horizontal");
        float dz = Input.GetAxis("Vertical");
        _moveRequest.Invoke(new Vector3(dx, 0f, dz));
    }
}

public sealed class MoveBehaviour : IEntityInit, IEntityFixedTick
{
    private Transform _transform;
    private IValue<float> _moveSpeed;
    private IRequest<Vector3> _moveRequest;

    public void Init(IEntity entity)
    {
        _transform = entity.GetTransform();
        _moveSpeed = entity.GetMoveSpeed();
        _moveRequest = entity.GetMoveRequest();
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        if (_moveRequest.Consume(out Vector3 moveDirection))
            _transform.position += moveDirection * (deltaTime * _moveSpeed.Value);
    }
}
```

Признак, по которому выбирают: если эффект обязан произойти в конкретной фазе кадра (физика) или должен быть переигран/отправлен по сети, это запрос. Если эффект — просто вызов, это действие.

### Проверка в тесте через `ActionSpy`

```csharp
[Test]
public void Composite_InvokesEveryChild()
{
    var first = new ActionSpy();
    var second = new ActionSpy();

    IAction composite = new CompositeAction(first.Invoke, second.Invoke);

    composite.Invoke();

    Assert.AreEqual(1, first.InvokeCount);
    Assert.IsTrue(second.WasInvoked);
}
```

`ActionSpy` считает вызовы, а не только фиксирует факт, поэтому им проверяется и «вызвали ровно один раз», и «не вызвали дважды» — вещи, которые самодельным `bool invoked` не проверяются.

## Производительность

**Измерений для Actions и Commands в репозитории нет.** `Assets/Plugins/Atomic/Elements/Tests/Performance` содержит только бенчмарки коллекций (`ArrayPerformance.cs`, `ListPerformance.cs`, `ReactiveArrayPerformance.cs`, `ReactiveDictionaryPerformance.cs`, `ReactiveHashSetPerformance.cs`, `ReactiveLinkedListPerformance.cs`, `ReactiveListPerformance.cs`, `DictionaryPerformance.cs`, `HashSetPerformance.cs`), а в `Docs/Elements/Performance` лежат только пять страниц по реактивным коллекциям (`ReactiveArrayPerformance.md`, `ReactiveDictionaryPerformance.md`, `ReactiveHashSetPerformance.md`, `ReactiveLinkedListPerformance.md`, `ReactiveListPerformance.md`) — страниц по `Array`/`List`/`Dictionary`/`HashSet` нет даже там. Всё ниже — чтение исходников, а не замеры; где это вывод, а не прямое свойство кода, сказано отдельно.

**`AggressiveInlining` стоит на всех групповых операциях.** Все десять перегрузок `InvokeRange` в `Actions/Extensions.cs` и все двенадцать перегрузок в `Commands/Extensions.cs` помечены `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.

**Массивные перегрузки `InvokeRange` не создают энумератор.** `Actions/Extensions.cs`: `for (int i = 0, count = actions.Length; i < count; i++) actions[i]?.Invoke(…)` — граница вынесена из цикла. `IEnumerable`-перегрузки идут `foreach` по интерфейсу. Вывод (не замер): для коллекции со структурным энумератором проход через `IEnumerable<IAction>` боксит этот энумератор, так что массивная форма строго дешевле.

**`MonoActionConfigurable` и `MonoActionComposite` идут по индексу с вынесенной границей.** `MonoActionConfigurable`: `if (this.actions != null)` + `this.actions[i]?.Invoke(…)` — управляемая проверка на `null`. `MonoActionComposite` и `MonoActionReference`: `if (action)` — неявный `bool` от `UnityEngine.Object`, нативный вызов проверки валидности, который заодно отсекает уничтоженные объекты. Вывод: нативная проверка немного дороже управляемой — на один вызов это неважно, во внутреннем цикле стоит учитывать.

**`Command` хранит условия в массиве, действия — в одном делегате.** `Commands/Command.cs`: `private Func<bool>[] _conditions = new Func<bool>[4]; private int _count;`, рост через `Array.Resize(ref _conditions, _conditions.Length * 2)`, обход по индексу. Аллокаций на вызов нет, память непрерывна. Действия — один multicast `Action _action`: вызов — один `_action?.Invoke()`, но `AddAction`/`RemoveAction` копируют цепочку делегатов (O(n)). Отсюда правило: команду собирают один раз, обработчики не перевешивают каждый кадр. Раскладка идентична во всех пяти арностях `Command` и всех пяти арностях `ThreadSafeCommand`.

**`CanInvoke` короткозамкнут во всех реализациях.** И `Command.CanInvoke`, и `ThreadSafeCommand.CanInvoke` возвращают `false` на первом неуспешном условии, поэтому порядок условий — реальная оптимизация, а не косметика.

**`RemoveCondition` устроен по-разному.** `Command`: сдвиг хвоста (`for (int j = i; j < _count - 1; j++) _conditions[j] = _conditions[j + 1];`) — O(n), порядок сохраняется. `ThreadSafeCommand`: `_conditions[i] = _conditions[_conditionCount - 1]` — O(1), порядок не сохраняется. Если условия были упорядочены «от дешёвого к дорогому», на thread-safe варианте этот порядок после любого удаления теряется.

**`ThreadSafeCommand`: неродовой вариант берёт замок дважды.** `Invoke()`/`TryInvoke()` сначала `lock (_lock) action = _action;`, затем `CanInvoke()`, который берёт замок ещё раз. Причём снимок `_action` делается до проверки условий, так что отказ по условию всё равно оплачен одним захватом.

**Родовые `ThreadSafeCommand<T>` дешевле на пути отказа.** `Invoke`/`TryInvoke` снимают `_action`, `_conditions` и `_conditionCount` одним замком, прогоняют условия и само действие полностью вне замка, и берут второй замок только на успешном пути — чтобы записать `_arg`/`_hasValue`. Неуспешное условие стоит один захват против двух у неродового.

**Отложенная публикация ничего не аллоцирует, но склеивает вызовы.** `Utils/MainThreadDispatcher.cs`: `static readonly HashSet<IFlushable> _dirty = new(256)` и `static readonly List<IFlushable> _processing = new(256)`. `MarkDirty` — вставка в множество под замком, то есть повторные вызовы с рабочего потока склеиваются и не создают ни замыкания, ни элемента очереди. `Update()` копирует множество в список под замком, чистит множество, затем вызывает `Flush()` **вне** замка по индексу и чистит список. Порядок обхода `HashSet` не определён, значит порядок flush между разными экземплярами недетерминирован.

**`CompositeAction.Invoke` не аллоцирует.** `Collections/List/ReactiveLinkedList.cs` — не список узлов в куче: `private struct Node`, `private Node[] _nodes`, свободный список, `INITIAL_CAPACITY = 4`, рост `Array.Resize(ref _nodes, _nodes.Length * 2)`, `ref Node node = ref _nodes[_index]` в энумераторе, чтобы не копировать структуру, и `[MethodImpl(AggressiveInlining)]` на внутренних `AllocateNode`/поиске/отвязке (строки 463, 479, 492). `public Enumerator GetEnumerator() => new(this);` при `public struct Enumerator : IEnumerator<T>`, а интерфейсные формы (`IEnumerator<T> IEnumerable<T>.GetEnumerator()`, `IEnumerator IEnumerable.GetEnumerator()`) реализованы явно. Поэтому `foreach (Action action in this)` внутри `Invoke()` связывается со структурным энумератором. Обратная сторона (вывод): обход того же композита через переменную типа `IEnumerable<Action>` боксит энумератор — композит надо обходить через конкретный тип или брать энумератор по значению: `var e = composite.GetEnumerator();`. Обход идёт по индексам `next`, то есть по массиву, но не строго последовательно.

**`InlineAction` — один лишний прыжок через делегат.** Поле `private readonly Action action` проверено на `null` в конструкторе, поэтому `Invoke()` — просто `this.action.Invoke()` без проверок. Цена: одна аллокация объекта при создании (две, если лямбда захватывает контекст) и один дополнительный переход делегата против прямого вызова. `ToString()` читает `this.action.Method.Name` — это рефлексия по `MethodInfo`, на горячем пути её вызывать не надо.

**`MonoAction` ничего не стоит, пока не вызван.** Все пять вариантов — абстрактные `MonoBehaviour` с единственным абстрактным `Invoke`, без полей и без Unity-колбэков, так что добавление компонента не добавляет движку покадровой работы.

**Боксинг аргументов отсутствует.** Все параметры `IAction` объявлены `in`, и каждый родовой вариант — отдельный закрытый тип, поэтому вызов через `IAction<T>` со структурным `T` не упаковывает аргумент. Риск боксинга в этой области остаётся в двух местах: структурный энумератор через интерфейс (см. выше) и `Subscription` в переменной типа `IDisposable`.

**Чтение условия через `IValue` — это вызов default interface method.** `Values/IValue.cs` использует реализации по умолчанию: `object IValue.Value => Value;` и `T IFunction<T>.Invoke() => this.Value;`. То есть условие, прочитанное как `IFunction<bool>.Invoke()` на элементе, который на самом деле `IValue<bool>`, — это не чтение поля, а диспетчеризация через DIM.

**Пулинг.** В области Actions/Commands собственных пулов нет; ни один тип здесь не имеет `Reset`/`Return`. То, что переиспользуется между спавнами, — это сами экземпляры действий (они без состояния, если вы их такими написали) и `Request`, который переиспользуется по своей природе: `Invoke` ставит флаг, `Consume` снимает. Пулинг сущностей — отдельная область (`Docs/Entities`), в этой главе его нет.

## Грабли

- **Конструктор `CompositeAction` принимает `params Action[]`, а не `params IAction[]`.** Все пять страниц документации `CompositeAction*.md` и пример в `Actions/Manual.md` говорят об `IAction` и передают объекты. Код, написанный по документации, не компилируется. Передавайте `myAction.Invoke` или лямбду.
- **`CompositeAction.Invoke()` не пропускает `null`.** `foreach (Action action in this) action.Invoke();` — `null`-делегат в списке бросает `NullReferenceException`. Это противоположно `MonoActionConfigurable`, `MonoActionComposite` и `InvokeRange`, которые `null` пропускают. Не считайте, что «null безопасен» одинаково во всей области.
- **`new CompositeAction(null)` падает с `NullReferenceException`, а не `ArgumentNullException`.** XML-док на `params`-конструкторе обещает `ArgumentNullException`, но базовый `ReactiveLinkedList(params T[] items) : this(items.Length)` читает `Length` до любой проверки (`ReactiveLinkedList.cs:106`). `ArgumentNullException` бросает только `IEnumerable`-конструктор, и то через LINQ-`Count()`.
- **`CompositeAction` — это `IDisposable`, и об этом легко не узнать.** Он виден как `ICompositeAction`/`IAction`, но наследует `ReactiveLinkedList<Action>`, чей `Dispose()` делает `Clear()` и обнуляет реактивные события. Долгоживущий композит, который никто не освобождает, — настоящая утечка.
- **`InlineAction` не `[Serializable]`.** В слот `[SerializeReference] IAction` его положить нельзя. Из готовых реализаций `[Serializable]` несут только `CompositeAction`, `LogAction` и `MonoActionReference`.
- **Имена параметров `IAction` различаются по арности.** `arg` / `arg1, arg2` для арностей 1–2, `args1, args2, args3[, args4]` для 3–4. Документация показывает `arg1..argN` всюду. Именованные аргументы и явные реализации интерфейса, написанные по документации, не сойдутся. В `MonoActionConfigurable<T>` параметр и вовсе `arg1`, хотя в базовом `MonoAction<T>` он `arg`.
- **Родовые Unity-классы нельзя повесить на объект.** У `MonoActionConfigurable` все родовые варианты `abstract`; у `MonoActionComposite` они конкретные, но Unity всё равно не сериализует открытый родовой компонент. Нужен пустой закрытый подкласс на каждый тип аргумента.
- **`[SerializeReference]` хрупок.** Прямое предупреждение в `MonoActionsConfigurable.md`: это последнее средство, переименование или перенос сериализованного класса теряет данные. Где дети могут быть компонентами — берите `MonoActionComposite`.
- **Extension-перегрузки команд возвращают `void` и ломают цепочку.** `AddCondition(IFunction<bool>)`, `AddAction(IAction)` и их пары не возвращают команду. Хуже то, что выбор перегрузки зависит от типа аргумента: `IPredicate`, `IValue<bool>`, `IVariable<bool>`, `ReactiveVariable<bool>` — все `IFunction<bool>`, так что `AddCondition` на них уйдёт в void-вариант молча.
- **Перегрузок команд для арностей 3 и 4 нет.** `Commands/Extensions.cs` — ровно 12 методов, до `ICommand<T1,T2>`. `ICommand<T1,T2,T3>.AddCondition(IFunction<…>)` не скомпилируется, потому что `IFunction` объявлен только до двух аргументов.
- **`condition.Invoke` создаёт новый делегат на каждый вызов extension-метода.** `RemoveCondition` его находит (равенство делегатов сравнивает цель и метод), но аллокация есть всегда. Для частой перерегистрации кэшируйте делегат сами.
- **`Command` не проверяет условия на `null` и не дедуплицирует их.** `_conditions[i]()` вызывается без охраны — `null`-условие упадёт на следующем `CanInvoke()`. Дважды добавленное одинаковое условие будет вычислено дважды.
- **`Command` не реализует `IDisposable`.** Best Practices говорит «clean up conditions and actions when disposing to avoid leaks», но метода для этого нет: условия и multicast-делегат держат сильные ссылки на всё, что они захватили. Снимайте их `RemoveCondition`/`RemoveAction` или не переживайте командой её сущности.
- **Условие с побочным эффектом вызывается многократно.** `CanInvoke()` документирован как способ гасить кнопки, то есть зовётся каждый кадр, и `TryInvoke()` зовёт его ещё раз внутри. Любая мутация в условии произойдёт много раз за кадр.
- **`ThreadSafeCommand.RemoveCondition` теряет порядок условий.** Swap-with-last. Порядок «дешёвое первым» после любого удаления перестаёт действовать, молча.
- **`MarkDirty` — это отметка, а не очередь.** Несколько вызовов с рабочего потока до следующего сброса склеиваются в одно `OnEvent`, а у родовых `ThreadSafeCommand<T>` доставляется только **последний** аргумент. `OnEvent` нельзя использовать как «уведомление на каждый вызов» из фонового потока.
- **Неродовой `ThreadSafeCommand.Flush()` может поднять `OnEvent` без вызова.** Он читает `OnEvent` без замка и без флага «есть что публиковать»: `Action handler = OnEvent; handler?.Invoke();`. У родовых вариантов есть гейт `if (!_hasValue) return;` внутри замка, у неродового — нет.
- **`Flush()` — явная реализация интерфейса, и вызывать её нельзя.** Документация: «Called automatically by `MainThreadDispatcher`; do not invoke directly». Из пользовательского кода вы её и не увидите — `MainThreadDispatcher` объявлен `internal`.
- **В EditMode-тестах `ThreadSafeCommand.OnEvent` не сработает никогда.** Диспетчер создаётся из `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` на `DontDestroyOnLoad`-объекте `"[MainThreadDispatcher]"`. В EditMode этот хук не выполняется, `Update()` не идёт, `Flush()` не вызывается. Проверяйте побочный эффект действия, а не сигнал.
- **Исключение в одном `Flush()` оставляет диспетчер в грязном состоянии.** В `MainThreadDispatcher.Update()` цикл `_processing[i].Flush()` и последующий `_processing.Clear()` не обёрнуты в `try/finally`. Бросившее исключение прерывает сброс остальных, список не очищается, и следующий `Update()` допишет в него новые элементы — старые будут сброшены повторно. (Чтение исходника, не замер.)
- **`MainThreadDispatcher` недоступен из вашего кода.** `internal sealed class`; `AssemblyInfo.cs` открывает `internal` только для `Atomic.Elements.Tests.EditMode` и `Atomic.Elements.Tests.PlayMode`. Своя тестовая сборка его не увидит.
- **`Subscribe` возвращает структуру.** Присваивание в `IDisposable` боксит. Именно так написано в примере `Commands/Manual.md`.
- **`ISignal` не имеет `Subscribe`.** Это extension в `Events/Extensions.cs`. Если вы объявили собственный интерфейс, который «как ISignal», но не наследует его, `Subscribe` на нём не появится.
- **`IFunction<out R>` не имеет `Value`.** Ни `IFunction`, ни `IPredicate`. `Value` — на `IValue<out T>`. Пример `JumpBehaviour` в `RequestConditionActionEvent.md` объявляет поле `IFunction<bool>` и читает `.Value` — не компилируется.
- **Типов `BaseRequest` и `BaseEvent` не существует.** Поиск по всему `Assets/Plugins/Atomic` даёт ноль совпадений. Реальные классы — `Request`/`Request<T>`… и `Event`/`Event<T>`…
- **Конструкторы `AndExpression` не принимают делегаты.** Только `int capacity`, `params ExpressionMember<bool>[]` и `IEnumerable<ExpressionMember<bool>>`; у `ExpressionMember<R>` нет неявного преобразования. Лямбды добавляются через extension `Add` или через явный `new ExpressionMember<bool>(…)`. Члена `Exists` на `IValue`/`IReactiveVariable` нет вообще.
- **Класс называется `LogAction`, файл документации — `PrintAction.md`.** В Manual он подписан как «LogAction», страница Best Practices — `UsingSerializeReferenceForPrintActions.md`, а в XML-доке исходника остался `<see cref="ConsoleAction"/>`. Поиск по документации найдёт `PrintAction`, поиск по коду — нет.
- **Поле в ``MonoActionComposite`3.md`` и ``MonoActionComposite`4.md`` задокументировано неверно.** Документация пишет `MonoActionComposite<T1, T2, T3>[] actions`; в исходнике — `MonoAction<T1, T2, T3>[] actions`.
- **Мелкие примеры в документации не копипастятся.** ``CompositeAction`2.md`` содержит лишнюю `z` после вызова конструктора; ``CompositeAction`3.md`` собирает `CompositeAction<int,int,int>` из `new InlineAction<int, int>((a, b, c) => …)` — не та арность; `Commands/Manual.md` пишет `player.GetStamina().Vaue >= 10`; `Command.md` — `player.GetTranform()` и `new Vector(5, 5)`.
- **Примеры `InvokeRange` спрятаны в HTML-комментарии.** В `ExtensionsInvokeRange.md` блок примеров обёрнут в `<!-- … -->` (открывается на строке 48), так что на отрендеренной странице видны только подписи. Примеры есть в сыром markdown.
- **XML-документация `InlineAction` не попадает в сборку.** В `Actions/Core/InlineAction.cs` блок `#if ODIN_INSPECTOR [InlineProperty] #endif` стоит **перед** `/// <summary>` (и `[Button]` — перед доком `Invoke`), во всех пяти арностях. Такие комментарии отвязаны от члена и отбрасываются компилятором: в сгенерированной документации у `InlineAction` и его `Invoke` описаний не будет.

## Где читать

Все пути относительно `C:/!!!Work/_AtomicUpstream`.

### Документация: Actions

- `Docs/Elements/Actions/Manual.md` — входная точка области: оглавление всех страниц Actions, четыре коротких примера (`IAction`, `InlineAction`, `CompositeAction`, `MonoAction<T>`) и ссылки на четыре страницы Best Practices (`UsingInlineActions`, `UsingSerializeReferenceForCompositeActions`, `UsingSerializeReferenceForPrintActions`, `RequestsVsActions`). Пример `CompositeAction` здесь как раз тот, который не компилируется.
- `Docs/Elements/Actions/IActions.md` — обзорная страница по всем пяти арностям `IAction`: три примера (без параметров, с одним, с двумя) и список ссылок на поарностные страницы.
- `Docs/Elements/Actions/IAction.md`, ``IAction`1.md`` … ``IAction`4.md`` — по странице на арность, одинаковой структуры: пример реализации, пример использования, блок «Type» с точной подписью интерфейса и блок «Methods» с подписью `Invoke` и описанием параметров. ``IAction`1.md`` — эталон формата (`DestroyGameObjectAction`).
- `Docs/Elements/Actions/InlineActions.md` — обзор `InlineAction` по арностям, три примера.
- `Docs/Elements/Actions/InlineAction.md`, ``InlineAction`1.md`` … ``InlineAction`4.md`` — поарностный API: конструктор, неявный оператор, `Invoke`, `ToString`.
- `Docs/Elements/Actions/CompositeActions.md` — обзор композитов: один пример и ссылки на поарностные страницы.
- `Docs/Elements/Actions/CompositeAction.md`, ``CompositeAction`1.md`` … ``CompositeAction`4.md`` — поарностный API. **Читать с поправкой:** конструкторы здесь описаны как `params IAction[]` / `IEnumerable<IAction>`, в исходнике они `params Action[]` / `IEnumerable<Action>`. В ``CompositeAction`2.md`` опечатка в примере (лишняя `z`), в ``CompositeAction`3.md`` — пример с неверной арностью.
- `Docs/Elements/Actions/MonoActions.md` — обзор абстрактных `MonoAction`: зачем компонент-действие, три примера (`HelloWorldAction`, `DestroyGameObjectAction` и двухаргументный), ссылки на поарностные страницы.
- `Docs/Elements/Actions/MonoAction.md`, ``MonoAction`1.md`` … ``MonoAction`4.md`` — поарностный API абстрактного базового класса (одна абстрактная `Invoke`).
- `Docs/Elements/Actions/MonoActionsConfigurable.md` — обзор `MonoActionConfigurable`: два примера (неродовой и родовой с пустым закрытым подклассом), API-ссылки и раздел «Notes» с тремя врезками. Здесь же то самое `WARNING` про `[SerializeReference]` как последнее средство и `NOTE` о том, что `null` пропускаются, а порядок исполнения — порядок массива.
- `Docs/Elements/Actions/MonoActionConfigurable.md`, ``MonoActionConfigurable`1.md`` … ``MonoActionConfigurable`4.md`` — поарностный API: поле `actions`, `Invoke`.
- `Docs/Elements/Actions/MonoActionsComposite.md` — обзор `MonoActionComposite`: неродовой и родовой примеры, объяснение, что дети — ссылки на компоненты.
- `Docs/Elements/Actions/MonoActionComposite.md`, ``MonoActionComposite`1.md`` … ``MonoActionComposite`4.md`` — поарностный API. **Читать с поправкой:** в ``MonoActionComposite`3.md`` и ``MonoActionComposite`4.md`` поле задокументировано как `MonoActionComposite<T1,T2,T3>[]` / `MonoActionComposite<T1,T2,T3,T4>[]`, в исходнике — `MonoAction<…>[]`.
- `Docs/Elements/Actions/MonoActionsReference.md` — самая подробная страница области (443 строки): объясняет роль моста между `[SerializeReference]`-графом и компонентами сцены, даёт пример, и содержит полный API-reference всех пяти арностей прямо внутри себя (поле, оба конструктора, `Invoke`), а не ссылками.
- `Docs/Elements/Actions/MonoActionReference.md`, ``MonoActionReference`1.md`` … ``MonoActionReference`4.md`` — те же пять API-блоков отдельными страницами.
- `Docs/Elements/Actions/PrintAction.md` — страница `LogAction` (имя файла осталось старым). Содержит раздел «Inspector Settings» (как выглядят `_logType`/`_message` в инспекторе), три блока конструкторов (`LogAction()`, `LogAction(string)`, `LogAction(string, LogType)`) и `Invoke`/`ToString`.
- `Docs/Elements/Actions/ExtensionsInvokeRange.md` — все десять перегрузок `InvokeRange` с подписями и описаниями. Блок «Examples of Usage» (десять примеров, по одному на перегрузку) обёрнут в HTML-комментарий начиная со строки 48 — на отрендеренной странице его не видно, в сыром файле он есть.

### Документация: Commands

- `Docs/Elements/Commands/Manual.md` — короткая (91 строка) входная точка: три примера (команда без параметров, параметризованная, подписка на `OnEvent`), список из трёх API-страниц и раздел Best Practices из пяти пунктов (`TryInvoke` когда нужен результат, `CanInvoke` для UI, условия без побочных эффектов, чистить при освобождении, `ThreadSafeCommand` для многопоточности). В примерах — опечатка `Vaue` и присваивание подписки в `IDisposable`.
- `Docs/Elements/Commands/ICommand.md` — 530 строк: по блоку на каждую из пяти арностей (`ICommand`, `ICommand<T>`, `ICommand<T1,T2>`, `ICommand<T1,T2,T3>`, `ICommand<T1,T2,T3,T4>`) с подписями всех членов. Основной справочник по контракту.
- `Docs/Elements/Commands/Command.md` — 586 строк, та же структура для конкретного класса: два примера вверху и пять блоков API. Примеры содержат `GetTranform()` и `new Vector(5, 5)`.
- `Docs/Elements/Commands/ThreadSafeCommand.md` — 611 строк, **только** API-reference по пяти арностям, без раздела с примерами. Разницу между неродовым и родовыми вариантами (гейт `_hasValue`, отсутствие вызова `CanInvoke` внутри `Invoke`) страница не описывает — её видно только в исходнике.
- Отдельной страницы про `Commands/Extensions.cs` нет: мост `IFunction`/`IAction` → команда в документации отсутствует целиком.

### Документация: Best Practices

- `Docs/BestPractices/UsingInlineActions.md` — как держать логику механики рядом с установкой сущности: `WeaponInstaller : MonoEntityInstaller<IWeapon>` со всей логикой выстрела внутри `new InlineAction(…)`. Содержит `new BaseEvent()`, которого не существует.
- `Docs/BestPractices/UsingSerializeReferenceForCompositeActions.md` — пошагово: `PlayerActionTrigger` с полем `[SerializeReference] private IAction _action`, добавление на объект, назначение композита в инспекторе.
- `Docs/BestPractices/UsingSerializeReferenceForPrintActions.md` — то же самое, но лист графа — `LogAction`; самый короткий способ увидеть, как `[SerializeReference]` работает в инспекторе.
- `Docs/BestPractices/RequestsVsActions.md` — когда брать действие, а когда запрос. Два примера на действия (выстрел, взаимодействие с предметом), два на запросы (ввод движения через `IRequest<Vector3>`, слежение за целью) и раздел «Multiplayer Considerations». Это та страница, которую стоит прочитать до того, как решать архитектуру механики.
- `Docs/BestPractices/RequestConditionActionEvent.md` — поток RCAE: описание четырёх ролей со ссылками на соответствующие элементы и сквозной пример прыжка. **Оба блока кода здесь не компилируются:** `new BaseRequest()`/`new BaseEvent()`, `AndExpression` с делегатами и `.Exists`, поле `IFunction<bool>`, читаемое как `.Value`. Идея верна, типы — нет; исправленный вариант — в разделе «Идиомы» выше.
- `Docs/BestPractices/UsingSubscriptionsWithDisposeComposite.md` — управление пачкой подписок через `DisposableComposite` (`private readonly DisposableComposite _disposables = new();`) на примере `WeaponViewInstaller`. Обратите внимание: класс называется `DisposableComposite`, несмотря на имя файла.
- `Docs/BestPractices/UsingExpressions.md`, `UsingConstantsWithAndExpressions.md`, `UsingInlineFunctions.md` — условия как объекты: откуда брать корректную форму `AndExpression` и `IPredicate`, если условия команды нужны переиспользуемыми.

### Исходники

- `Assets/Plugins/Atomic/Elements/Scripts/Actions/IAction.cs` — пять интерфейсов, 77 строк. Единственный достоверный источник по именам параметров.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/ICompositeAction.cs` — пять интерфейсов композита, пустые тела, вся суть — в списке баз.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Extensions.cs` — десять `InvokeRange`, 154 строки. Короткий файл, читается целиком за минуту; стоит прочитать, чтобы увидеть разницу массив/`IEnumerable` своими глазами.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Core/InlineAction.cs` — пять классов. Обратите внимание на порядок атрибутов и XML-доков.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Core/CompositeAction.cs` — пять классов. Файл, из-за которого расходятся документация и код: смотреть на `params Action[]`.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Core/LogAction.cs` — один класс, целиком под `#if`-ветками Unity / не-Unity.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Unity/MonoAction.cs` — пять абстрактных компонентов, весь файл под `#if UNITY_5_3_OR_NEWER`; у каждого класса `[HelpURL]` на свою страницу документации.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Unity/MonoActionConfigurable.cs` — неродовой `public class` + четыре `abstract`. Здесь видно, почему родовые требуют подкласса.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Unity/MonoActionComposite.cs` — пять конкретных классов; видно `if (action)` и разницу в атрибутах `[Space]` между арностями 1–2 и 3–4.
- `Assets/Plugins/Atomic/Elements/Scripts/Actions/Unity/MonoActionReference.cs` — пять `sealed [Serializable]` классов-мостов.
- `Assets/Plugins/Atomic/Elements/Scripts/Commands/ICommand.cs` — пять интерфейсов команды, 337 строк.
- `Assets/Plugins/Atomic/Elements/Scripts/Commands/Command.cs` — пять `sealed` классов (строки 18, 122, 227, 333, 440). Читать для раскладки условий и для `RemoveCondition` со сдвигом.
- `Assets/Plugins/Atomic/Elements/Scripts/Commands/ThreadSafeCommand.cs` — пять `sealed` классов (строки 19, 157, 344, 540, 742). **Обязательно к прочтению**, если собираетесь вызывать команды из потоков: неродовой и родовые варианты устроены по-разному, и документация про это не говорит.
- `Assets/Plugins/Atomic/Elements/Scripts/Commands/Extensions.cs` — 12 перегрузок, 94 строки. Единственный источник по мосту `IFunction`/`IAction` → команда.
- `Assets/Plugins/Atomic/Elements/Scripts/Utils/MainThreadDispatcher.cs` — 67 строк, `internal sealed class`. Читать, чтобы понять механику склеивания `OnEvent` и почему его нет в EditMode.
- `Assets/Plugins/Atomic/Elements/Scripts/Collections/List/ReactiveLinkedList.cs` — база `CompositeAction`. Ключевые места: `private struct Node` (76), `Node[] _nodes` (82), конструкторы (93/106/116), `Dispose` (124), `GetEnumerator`/`struct Enumerator` (412/421), inlined-хелперы (463/479/492), `ISerializationCallbackReceiver` (521).
- `Assets/Plugins/Atomic/Elements/Scripts/Events/ISignal.cs`, `Events/Subscription.cs`, `Events/Extensions.cs` — контракт сигнала, структура подписки и все `Subscribe`/`SubscribeRange`/`UnsubscribeRange`.
- `Assets/Plugins/Atomic/Elements/Scripts/Requests/IRequest.cs`, `Requests/Request.cs` — отложенная половина RCAE и настоящие имена классов.
- `Assets/Plugins/Atomic/Elements/Scripts/Functions/IFunction.cs`, `Functions/IPredicate.cs` — потолок арности для условий-объектов.
- `Assets/Plugins/Atomic/Elements/Scripts/Values/IValue.cs` — где на самом деле живёт `Value` и как он связан с `IFunction` через default interface methods.
- `Assets/Plugins/Atomic/Elements/Scripts/Expressions/AndExpression.cs`, `Expressions/ExpressionMember.cs`, `Expressions/ExpressionBase.cs`, `Expressions/Extensions.cs` — настоящие конструкторы выражений и extension `Add(this IExpression<R>, Func<R>)`, через который в `AndExpression` попадают лямбды.
- `Assets/Plugins/Atomic/Elements/Scripts/AssemblyInfo.cs` — две строки `InternalsVisibleTo`; объясняет, почему `internal`-типы фреймворка не видны вашим тестам.

### Тесты как справочник по идиомам

- `Assets/Plugins/Atomic/Elements/Tests/EditMode/Actions/CompositeActionTests.cs` — короткий тест, который снимает все вопросы о конструкторе: `new CompositeAction(a1, a2, a3)`, где каждый `a` — `new Action(() => …)`.
- `Assets/Plugins/Atomic/Elements/Tests/EditMode/Actions/ActionExtensionsTests.cs` — покрытие `InvokeRange`, включая `Extensions.InvokeRange(null)` как no-op.
- `Assets/Plugins/Atomic/Elements/Tests/EditMode/Actions/BasicActionTests.cs` — базовые проверки `IAction`-реализаций.
- `Assets/Plugins/Atomic/Elements/Tests/EditMode/Commands/CommandTests.cs` — единственный тест-файл по командам: конструктор, `CanInvoke` с проходящими и непроходящими условиями, далее по членам. `ThreadSafeCommand` не покрыт.
- `Assets/Plugins/Atomic/Elements/Tests/PlayMode/Actions/SceneActionTests.cs` — несмотря на имя файла, содержит `MonoActionConfigurableTests`: компонент навешивается на пустой `GameObject`. Имя файла осталось от эпохи `SceneAction*`.
- `Assets/Plugins/Atomic/Elements/Tests/PlayMode/Actions/MonoActionReferenceTests.cs` — PlayMode-проверки моста.
- `Assets/Plugins/Atomic/Elements/Tests/Substitutions/ActionSpy.cs` — готовый test double; сборка `Atomic.Elements.Tests.Substitutions`, Editor-only, `autoReferenced: true`.
- `Assets/Plugins/Atomic/Elements/Tests/Performance/` — бенчмарков по действиям и командам здесь **нет**, только коллекции. Любое утверждение «команда быстрее/медленнее» в этом проекте придётся измерять самим.

# 06. Elements: события и реквесты

Этот раздел `Atomic.Elements` — два примитива, на которых держится вся реактивная часть фреймворка. События (`ISignal` / `IAction` / `IEvent` / `Event` / `Subscription` / `InlineSignal` / `ThreadSafeEvent`) дают оповещение «сейчас»: кто-то вызвал, все подписчики отработали синхронно в том же стеке. Реквесты (`IRequest` / `Request`) дают оповещение «потом»: производитель помечает намерение в одной фазе игрового цикла, потребитель забирает его в другой, и повторные вызовы до разбора не дублируются, а перезатирают друг друга. Ни один из этих типов не является контейнером сообщений: событие — это обёртка над `System.Action`, реквест — это `bool` плюс до четырёх типизированных полей. Всё остальное в главе — следствия этих двух фактов.

Все сигнатуры и все сниппеты ниже сверены с исходниками в `C:/!!!Work/_AtomicUpstream/Assets/Plugins/Atomic`. Там, где документация фреймворка расходится с кодом, в тексте указано и то, и другое: расхождений много, и они систематические.

---

## Модель

**Три интерфейса — это одно разделение по направлению, а не три разных сущности.**

- `ISignal` — только наблюдать. Есть `event Action OnEvent`, вызвать нечем.
- `IAction` — только вызывать. Есть `void Invoke()`, подписаться нечем.
- `IEvent : ISignal, IAction` — и то, и другое. Своих членов у `IEvent` нет вообще: это буквально пересечение двух предыдущих (`Elements/Scripts/Events/IEvent.cs` — все пять арностей объявлены с пустым телом).

Из этого вытекает основное правило проектирования, которое фреймворк предлагает как конвенцию: экземпляр хранится как конкретный `Event`, но раздаётся наружу через узкий интерфейс. Слушатель держит поле `ISignal`, триггер — поле `IAction`, и только владелец видит `IEvent`. Компонент, который должен лишь реагировать, физически не может выстрелить событием, и наоборот.

**Вариантность разная у `ISignal` и у `IEvent`, и это не опечатка.**

`ISignal<out T>` …`ISignal<out T1, out T2, out T3, out T4>` объявлены ковариантно (`Elements/Scripts/Events/ISignal.cs`). Это законно именно потому, что `T` встречается только внутри `Action<T>`, который сам контравариантен: двойная инверсия даёт ковариантную позицию. Практический эффект: `ISignal<Enemy>` присваивается в `ISignal<IEntity>` без обёртки и без аллокации.

`IAction<in T>` …`IAction<in T1, in T2, in T3, in T4>` объявлены контравариантно (`Elements/Scripts/Actions/IAction.cs`, строки 6, 18, 32, 48, 67). Зеркальный эффект: один `IAction<IEntity>` можно отдать потребителю, который ждёт `IAction<Enemy>`.

`IEvent<T>` и `IRequest<T>` инвариантны — `T` стоит и на входе, и на выходе. Не рассчитывайте на присваивание между ними.

Отдельно: файлы документации `Docs/Elements/Events/ISignal`1.md` (строка 34) печатают тип как `public interface ISignal<T>`, без `out`. Это ошибка документации; в исходнике `out` есть.

**`Event` не владеет подписками по отдельности.**

`Event.Dispose()` — это `this.OnEvent = null`. Один вызов сбрасывает весь список обработчиков, включая чужие. Это операция сноса объекта, а не отписки. Отписка — либо `Subscription.Dispose()`, либо `Unsubscribe(именованныйМетод)`.

**`Subscription` подписывает в конструкторе.**

`new Subscription(signal, action)` в теле конструктора делает `this.signal.OnEvent += this.action`. Побочный эффект есть всегда, даже если результат выбросить. Возвращаемое значение — это только токен для отписки, не «объект подписки», который можно не создавать.

**Реквест — не очередь.**

`Request<T>` — это `bool _required` плюс `T _arg`. `Invoke(arg)` пишет оба поля, `Consume(out arg)` читает поле, проверяет флаг и сбрасывает его. Десять `Invoke` за кадр — это одно последнее значение и один `Consume`. Это и есть обещанная «защита от дублей»: она работает не потому, что реквест что-то фильтрует, а потому, что хранить больше одного значения ему нечем.

**`ThreadSafeEvent` — тоже не очередь, и это важнее, чем кажется.**

`ThreadSafeEvent.Invoke()` не вызывает подписчиков. Он делает `MainThreadDispatcher.MarkDirty(this)`, а `MarkDirty` кладёт объект в `HashSet<IFlushable>`. Генерики дополнительно складывают аргументы в поля под `lock` (у `<T>` это `_value`, у остальных арностей — `_v1.._v4`). Диспетчер раз в `Update` сливает сет и вызывает `Flush()` у каждого. Итог: N вызовов за кадр = один колбэк с последними аргументами. Это канал «последнее значение побеждает», а не поток событий.

---

## API

### ISignal

```csharp
public interface ISignal
{
    event Action OnEvent;
}

public interface ISignal<out T>
{
    event Action<T> OnEvent;
}

public interface ISignal<out T1, out T2>
{
    event Action<T1, T2> OnEvent;
}

public interface ISignal<out T1, out T2, out T3>
{
    event Action<T1, T2, T3> OnEvent;
}

public interface ISignal<out T1, out T2, out T3, out T4>
{
    event Action<T1, T2, T3, T4> OnEvent;
}
```

Тип поля у всякого компонента, который только слушает. Ковариантен — `ISignal<Enemy>` присваивается в `ISignal<IEntity>` без адаптера.

### IEvent

```csharp
public interface IEvent : ISignal, IAction { }
public interface IEvent<T> : ISignal<T>, IAction<T> { }
public interface IEvent<T1, T2> : ISignal<T1, T2>, IAction<T1, T2> { }
public interface IEvent<T1, T2, T3> : ISignal<T1, T2, T3>, IAction<T1, T2, T3> { }
public interface IEvent<T1, T2, T3, T4> : ISignal<T1, T2, T3, T4>, IAction<T1, T2, T3, T4> { }
```

Тип поля только для владельца события. Собственных членов нет.

### Event

```csharp
#if ODIN_INSPECTOR
[InlineProperty]
#endif
[Serializable]
public class Event : IEvent, IDisposable
{
    public event Action OnEvent;

#if ODIN_INSPECTOR
    [Button]
#endif
    public void Invoke() => this.OnEvent?.Invoke();

    public void Dispose() => this.OnEvent = null;
}
```

Аналогично объявлены `Event<T>`, `Event<T1, T2>`, `Event<T1, T2, T3>`, `Event<T1, T2, T3, T4>` — все пять классов в `Elements/Scripts/Events/Event.cs`, все пять с `[Serializable]`, с `[InlineProperty]` и с `[Button]` на `Invoke` под `ODIN_INSPECTOR`. Классы не `sealed`. Это дефолтная реализация события: кладите её в поле, раздавайте через интерфейсы.

### ThreadSafeEvent

```csharp
public sealed class ThreadSafeEvent : IEvent, IDisposable, MainThreadDispatcher.IFlushable
{
    public event Action OnEvent;

    public void Invoke() => MainThreadDispatcher.MarkDirty(this);

    public void Dispose() => Interlocked.Exchange(ref OnEvent, null);

    void MainThreadDispatcher.IFlushable.Flush()
    {
        Action handler = OnEvent;
        handler?.Invoke();
    }
}
```

```csharp
public sealed class ThreadSafeEvent<T> : IEvent<T>, IDisposable, MainThreadDispatcher.IFlushable
{
    public event Action<T> OnEvent;

    private readonly object _lock = new();
    private bool _hasValue;
    private T _value;

    public void Invoke(T value)
    {
        lock (_lock)
        {
            _value = value;
            _hasValue = true;
        }

        MainThreadDispatcher.MarkDirty(this);
    }

    public void Dispose() => Interlocked.Exchange(ref OnEvent, null);

    void MainThreadDispatcher.IFlushable.Flush()
    {
        T value;

        lock (_lock)
        {
            if (!_hasValue)
                return;

            value = _value;
            _hasValue = false;
        }

        Action<T> handler = OnEvent;
        handler?.Invoke(value);
    }
}
```

Поставляется пять арностей (0–4). У `<T1,T2>`, `<T1,T2,T3>`, `<T1,T2,T3,T4>` — поля `_v1.._v4` и один общий флаг `_hasValue` под тем же `_lock`, семантика «последнее значение» одинаковая на всех арностях. Все пять `sealed`. Берите, когда событие поднимается с фонового потока и подписчики обязаны отработать на главном. Не берите, когда важен каждый вызов.

### Subscription

```csharp
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

Арности 1–4 устроены ровно так же, с `ISignal<...>` и `Action<...>`:

```csharp
public readonly struct Subscription<T> : IDisposable
{
    public Subscription(ISignal<T> signal, Action<T> action);
    public void Dispose();
}

public readonly struct Subscription<T1, T2> : IDisposable
{
    public Subscription(ISignal<T1, T2> signal, Action<T1, T2> action);
    public void Dispose();
}

public readonly struct Subscription<T1, T2, T3> : IDisposable
{
    public Subscription(ISignal<T1, T2, T3> signal, Action<T1, T2, T3> action);
    public void Dispose();
}

public readonly struct Subscription<T1, T2, T3, T4> : IDisposable
{
    public Subscription(ISignal<T1, T2, T3, T4> signal, Action<T1, T2, T3, T4> action);
    public void Dispose();
}
```

RAII-токен отписки. Держите в локальной переменной или в поле конкретного типа — тогда он не попадает в кучу. `default(Subscription).Dispose()` безопасен: `Dispose` проверяет `signal != null`.

### InlineSignal

```csharp
[Serializable]
public class InlineSignal : ISignal
{
    public event Action OnEvent
    {
        add => this.subscribe.Invoke(value);
        remove => this.unsubscribe.Invoke(value);
    }

    private readonly Action<Action> subscribe;
    private readonly Action<Action> unsubscribe;

    public InlineSignal(Action<Action> subscribe, Action<Action> unsubscribe)
    {
        this.subscribe = subscribe ?? throw new ArgumentNullException(nameof(subscribe));
        this.unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
    }
}
```

```csharp
public class InlineSignal<T> : ISignal<T>
{
    public InlineSignal(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe);
}

public class InlineSignal<T1, T2> : ISignal<T1, T2>
{
    public InlineSignal(Action<Action<T1, T2>> subscribe, Action<Action<T1, T2>> unsubscribe);
}

public class InlineSignal<T1, T2, T3> : ISignal<T1, T2, T3>
{
    public InlineSignal(Action<Action<T1, T2, T3>> subscribe, Action<Action<T1, T2, T3>> unsubscribe);
}

public class InlineSignal<T1, T2, T3, T4> : ISignal<T1, T2, T3, T4>
{
    public InlineSignal(Action<Action<T1, T2, T3, T4>> subscribe,
                        Action<Action<T1, T2, T3, T4>> unsubscribe);
}
```

Адаптер чужого C#-события в `ISignal` двумя лямбдами. Своего хранилища подписчиков нет — `add`/`remove` пробрасывают напрямую. `Invoke` отсутствует намеренно: `InlineSignal` только для чтения, парного `IEvent`-варианта нет. Все пять классов помечены `[Serializable]`, хотя оба поля — `readonly` делегаты, так что сериализовать там нечего.

### Extensions: Subscribe / Unsubscribe

В `Elements/Scripts/Events/Extensions.cs` лежат 30 методов: по пять арностей (0–4) на каждую из шести групп.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription Subscribe(this ISignal it, Action action) => new(it, action);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T> Subscribe<T>(this ISignal<T> it, Action<T> action) => new(it, action);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T1, T2> Subscribe<T1, T2>(
    this ISignal<T1, T2> it, Action<T1, T2> action) => new(it, action);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T1, T2, T3> Subscribe<T1, T2, T3>(
    this ISignal<T1, T2, T3> it, Action<T1, T2, T3> action) => new(it, action);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T1, T2, T3, T4> Subscribe<T1, T2, T3, T4>(
    this ISignal<T1, T2, T3, T4> it, Action<T1, T2, T3, T4> action) => new(it, action);
```

Те же пять арностей продублированы для `IAction`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription Subscribe(this ISignal it, IAction action) => new(it, action.Invoke);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T> Subscribe<T>(this ISignal<T> it, IAction<T> action) => new(it, action.Invoke);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T1, T2> Subscribe<T1, T2>(this ISignal<T1, T2> it, IAction<T1, T2> action) =>
    new(it, action.Invoke);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T1, T2, T3> Subscribe<T1, T2, T3>(
    this ISignal<T1, T2, T3> it, IAction<T1, T2, T3> action) => new(it, action.Invoke);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T1, T2, T3, T4> Subscribe<T1, T2, T3, T4>(
    this ISignal<T1, T2, T3, T4> it, IAction<T1, T2, T3, T4> action) => new(it, action.Invoke);
```

Это важно для миграции: мультиаргументные `IAction`, собранные дизайнером через `SerializeReference`, подключаются к мультиаргументным сигналам без рукописных адаптеров и без лямбд.

`Unsubscribe` симметричен — пять перегрузок под `Action<...>` и пять под `IAction<...>`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe(this ISignal it, Action action) => it.OnEvent -= action;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe<T>(this ISignal<T> it, Action<T> action) => it.OnEvent -= action;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe<T1, T2>(this ISignal<T1, T2> it, Action<T1, T2> action) => it.OnEvent -= action;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe<T1, T2, T3>(this ISignal<T1, T2, T3> it, Action<T1, T2, T3> action) =>
    it.OnEvent -= action;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe<T1, T2, T3, T4>(
    this ISignal<T1, T2, T3, T4> it, Action<T1, T2, T3, T4> action) => it.OnEvent -= action;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe(this ISignal it, IAction action) => it.OnEvent -= action.Invoke;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe<T>(this ISignal<T> it, IAction<T> action) => it.OnEvent -= action.Invoke;
// + <T1,T2>, <T1,T2,T3>, <T1,T2,T3,T4>
```

### Extensions: SubscribeRange / UnsubscribeRange

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void SubscribeRange(this ISignal it, IEnumerable<IAction> actions)
{
    if (actions != null)
        foreach (IAction action in actions)
            if (action != null)
                it.OnEvent += action.Invoke;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void SubscribeRange<T>(this ISignal<T> it, IEnumerable<IAction<T>> actions);
// + <T1,T2>, <T1,T2,T3>, <T1,T2,T3,T4>
```

```csharp
public static void UnsubscribeRange(this ISignal it, IEnumerable<IAction> actions)
{
    if (actions != null)
        foreach (IAction action in actions)
            if (action != null)
                it.OnEvent -= action.Invoke;
}

public static void UnsubscribeRange<T>(this ISignal<T> it, IEnumerable<IAction<T>> actions);
// + <T1,T2>, <T1,T2,T3>, <T1,T2,T3,T4>
```

Обе группы существуют во всех пяти арностях, обе проверяют и коллекцию, и каждый элемент на `null`. Это install-time API для списков из `[SerializeReference]`-полей. Пять `SubscribeRange` помечены `AggressiveInlining`, пять `UnsubscribeRange` — нет.

### IRequest

```csharp
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

public interface IRequest<T1, T2> : IAction<T1, T2>
{
    bool Required { get; }
    T1 Arg1 { get; }
    T2 Arg2 { get; }
    bool TryGet(out T1 arg1, out T2 arg2);
    bool Consume(out T1 args1, out T2 args2);
}

public interface IRequest<T1, T2, T3> : IAction<T1, T2, T3>
{
    bool Required { get; }
    T1 Arg1 { get; }
    T2 Arg2 { get; }
    T3 Arg3 { get; }
    bool TryGet(out T1 arg1, out T2 arg2, out T3 arg3);
    bool Consume(out T1 args1, out T2 args2, out T3 args3);
}

public interface IRequest<T1, T2, T3, T4> : IAction<T1, T2, T3, T4>
{
    bool Required { get; }
    T1 Arg1 { get; }
    T2 Arg2 { get; }
    T3 Arg3 { get; }
    T4 Arg4 { get; }
    bool TryGet(out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4);
    bool Consume(out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4);
}
```

`Invoke` здесь не переобъявлен — он приходит из `IAction<...>`. Отсюда главное свойство реквеста: он подставляется везде, где ждут `IAction`. Контроллер ввода пишет в `IAction<Vector3>` и не знает, немедленное это действие или отложенное.

Косметика, которая всё равно в поставленном интерфейсе: у двух- и трёхаргументного `Consume` параметры названы `args1`, `args2`, `args3` (в остальных арностях — `arg1`…). Если вы вызываете `Consume` с именованными аргументами, имена разные по арностям.

### Request

```csharp
#if ODIN_INSPECTOR
[InlineProperty]
#endif
[Serializable]
public class Request : IRequest
{
    public bool Required => _required;

#if UNITY_5_3_OR_NEWER
    [SerializeField]
#endif
    private bool _required;

#if ODIN_INSPECTOR
    [Button, HideInEditorMode]
#endif
    public void Invoke() => _required = true;

#if ODIN_INSPECTOR
    [Button, HideInEditorMode]
#endif
    public bool Consume()
    {
        if (!_required)
            return false;

        _required = false;
        return true;
    }
}
```

```csharp
[Serializable]
public class Request<T> : IRequest<T>
{
    public bool Required => _required;
    public T Arg => _arg;

#if UNITY_5_3_OR_NEWER
    [SerializeField]
#endif
    private T _arg;

#if UNITY_5_3_OR_NEWER
    [SerializeField]
#endif
    private bool _required;

#if ODIN_INSPECTOR
    [Button, HideInEditorMode]
#endif
    public void Invoke(T arg)
    {
        _required = true;
        _arg = arg;
    }

    public bool TryGet(out T arg)
    {
        arg = _arg;
        return _required;
    }

#if ODIN_INSPECTOR
    [Button, HideInEditorMode]
#endif
    public bool Consume(out T arg)
    {
        arg = _arg;

        if (!_required)
            return false;

        _required = false;
        return true;
    }
}
```

Арности 2–4 устроены так же: по одному `[SerializeField] private Tn _argN` на аргумент плюс `_required`. Обратите внимание на асимметрию с `Event`: `[InlineProperty]` навешен **только** на неродовой `Request`; `Request<T>`…`Request<T1,T2,T3,T4>` — просто `[Serializable]` (кнопки на `Invoke`/`Consume` при этом есть у всех). Классы не `sealed`.

### Extensions: IsRequired

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsRequired(this IRequest request) => request.Required;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsRequired<T>(this IRequest<T> request) => request.Required;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsRequired<T1, T2>(this IRequest<T1, T2> request) => request.Required;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsRequired<T1, T2, T3>(this IRequest<T1, T2, T3> request) => request.Required;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsRequired<T1, T2, T3, T4>(this IRequest<T1, T2, T3, T4> request) => request.Required;
```

Существует ровно для того, чтобы отдать `Required` как группу методов туда, где нужен `Func<bool>` — в `ExpressionMember<bool>`, `InlineFunction<bool>`, `AndExpression`. Проверки на `null` нет: `null`-реквест даёт `NullReferenceException`. Ни один `.md`-файл в `Docs/Elements/Requests` этот набор не описывает.

### DisposableComposite и AddTo

Не в папке Events, но без них раздел про подписки неполон (`Elements/Scripts/Utils/`).

```csharp
public static void AddTo(this IDisposable it, DisposableComposite composite) =>
    composite.Add(it);

public static void AddTo<T>(this T it, DisposableComposite<T> composite) where T : IDisposable =>
    composite.Add(it);
```

```csharp
public class DisposableComposite<T> : IDisposable where T : IDisposable
{
    public int Count => _items.Count;

    private readonly List<T> _items;

    public DisposableComposite(IEnumerable<T> disposables) => _items = new List<T>(disposables);
    public DisposableComposite(params T[] disposables) => _items = new List<T>(disposables);

    public DisposableComposite<T> Add(T disposable)
    {
        if (disposable == null)
            throw new ArgumentNullException(nameof(disposable));

        _items.Add(disposable);
        return this;
    }

    public void Dispose()
    {
        for (int i = 0, count = _items.Count; i < count; i++)
            _items[i].Dispose();

        _items.Clear();
    }
}

public class DisposableComposite : DisposableComposite<IDisposable> { }
```

Две перегрузки `AddTo` — это две разные стратегии, и выбор между ними не косметический. Негенерик принимает `IDisposable`, то есть упаковывает `Subscription` в бокс. Генерик выводит `T = Subscription` и складывает структуру в `List<Subscription>` как значение. `Dispose()` вызывает `_items[i].Dispose()` без приведения к интерфейсу, то есть на структуре — ограниченным вызовом, без упаковки. `Add` возвращает сам композит, так что вызовы можно цепочкой; `Dispose()` не только разбирает подписки, но и чистит список, поэтому композит переиспользуем между install/uninstall.

### MainThreadDispatcher

```csharp
internal sealed class MainThreadDispatcher : MonoBehaviour
{
    public interface IFlushable
    {
        void Flush();
    }

    private static MainThreadDispatcher _instance;

    private static readonly HashSet<IFlushable> _dirty = new(256);
    private static readonly List<IFlushable> _processing = new(256);

    private static readonly object _lock = new();

    private static int _mainThreadId;

    public static bool IsMainThread =>
        Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    public static void MarkDirty(IFlushable flushable)
    {
        if (flushable == null)
            return;

        lock (_lock)
            _dirty.Add(flushable);
    }

    private void Update()
    {
        lock (_lock)
        {
            if (_dirty.Count == 0)
                return;

            _processing.AddRange(_dirty);
            _dirty.Clear();
        }

        for (int i = 0, count = _processing.Count; i < count; i++)
            _processing[i].Flush();

        _processing.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var go = new GameObject("[MainThreadDispatcher]");
        DontDestroyOnLoad(go);

        _instance = go.AddComponent<MainThreadDispatcher>();
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;
    }
}
```

Тип `internal`, поэтому из игровых сборок он недоступен целиком — ни `Flush`, ни даже публичный `IsMainThread`. Поднимает себя сам: `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` создаёт `DontDestroyOnLoad`-объект `[MainThreadDispatcher]`. Доставка идёт в `Update`, значит `ThreadSafeEvent` никогда не срабатывает в фазе `FixedUpdate` и не срабатывает вообще в EditMode-тесте без игрового цикла Unity.

---

## Идиомы

### 1. Разделить событие по направлению

```csharp
var entity = new Entity("Character");
entity.AddValue("FireEvent", new Event());
```

```csharp
[Serializable]
public class FireSFXBehaviour : IEntityInit, IEntityDispose
{
    [SerializeField] private AudioClip _fireSFX;
    [SerializeField] private AudioSource _audioSource;

    private ISignal _fireSignal;

    public void Init(IEntity entity)
    {
        _fireSignal = entity.GetValue<ISignal>("FireEvent");
        _fireSignal.Subscribe(OnFire);
    }

    public void Dispose(IEntity entity)
    {
        _fireSignal.Unsubscribe(OnFire);
    }

    private void OnFire()
    {
        _audioSource.PlayOneShot(_fireSFX);
    }
}
```

```csharp
IAction fireEvent = entity.GetValue<IAction>("FireEvent");
fireEvent.Invoke();
```

Один экземпляр `Event` лежит в значениях сущности и достаётся через три разных интерфейса по роли вызывающего: `ISignal` слушателю, `IAction` триггеру, `IEvent` владельцу. Никто не может выстрелить событием, которое собирался только слушать, и ни один компонент не держит ссылку на конкретный `Event`.

Второе, менее заметное: обработчик — именованный приватный метод, а не лямбда. Именно поэтому симметричный `Unsubscribe(OnFire)` в `Dispose` реально снимает подписку: `-=` сравнивает делегаты по значению (один и тот же объект-цель плюс один и тот же метод), и две группы методов `OnFire` дают равные делегаты. Тот же приём проверяется тестом `EventTests.Unsubscribe` в `Elements/Tests/EditMode/Events/EventTests.cs`, где `OnEvent -= OnEventRaisen1` и `Unsubscribe(OnEventRaisen2)` работают одинаково.

Антипаттерн:

```csharp
// ПЛОХО
private Event _fireEvent;                 // конкретный тип: любой может и Invoke, и Dispose
_fireEvent.OnEvent += () => _audioSource.PlayOneShot(_fireSFX);   // лямбда: отписаться нечем
```

Лямбда без сохранённого делегата или токена — это утечка по построению: у неё нет имени, под которым её можно снять.

### 2. Подписка как RAII-токен в композите

Вариант с боксом (как в документации, но с правильной сигнатурой `Uninstall`):

```csharp
public sealed class WeaponViewInstaller : MonoEntityInstaller
{
    [SerializeField] private ParticleSystem _fireVFX;
    [SerializeField] private AudioSource _fireSFX;
    [SerializeField] private Animator _animator;

    private readonly DisposableComposite _disposables = new();

    public override void Install(IEntity entity)
    {
        ISignal fireEvent = entity.GetValue<ISignal>("FireEvent");

        fireEvent.Subscribe(_fireVFX.Play).AddTo(_disposables);
        fireEvent.Subscribe(_fireSFX.Play).AddTo(_disposables);
        fireEvent.Subscribe(() => _animator.SetTrigger("Fire")).AddTo(_disposables);
    }

    public override void Uninstall(IEntity entity)
    {
        _disposables.Dispose();
    }
}
```

Вариант без бокса — предпочтительный, если подписок много:

```csharp
public sealed class WeaponViewInstaller : MonoEntityInstaller
{
    [SerializeField] private ParticleSystem _fireVFX;
    [SerializeField] private AudioSource _fireSFX;
    [SerializeField] private Animator _animator;

    private readonly DisposableComposite<Subscription> _disposables = new();

    public override void Install(IEntity entity)
    {
        ISignal fireEvent = entity.GetValue<ISignal>("FireEvent");

        fireEvent.Subscribe(_fireVFX.Play).AddTo(_disposables);
        fireEvent.Subscribe(_fireSFX.Play).AddTo(_disposables);
        fireEvent.Subscribe(() => _animator.SetTrigger("Fire")).AddTo(_disposables);
    }

    public override void Uninstall(IEntity entity)
    {
        _disposables.Dispose();
    }
}
```

Текст в `Install` не изменился ни на символ — изменился только тип поля. `AddTo<T>(this T it, DisposableComposite<T> composite)` выводит `T = Subscription`, подписки лежат в `List<Subscription>` значениями, `Dispose()` вызывает структурный `Dispose` напрямую. Боксов нет вообще; в куче остаётся только сам список.

Почему форма правильная в обоих вариантах: подписка и отписка написаны один раз и рядом, а строка с лямбдой (`SetTrigger("Fire")`) вообще не может быть снята иначе — у неё нет имени. Цена — ровно одна, в момент установки, а не каждый кадр.

Сигнатура, которую нужно писать, — `public override void Uninstall(IEntity entity)`. В `Entities/Scripts/Installers/MonoEntityInstaller.cs` объявлены `public abstract void Install(IEntity entity)` (строка 33) и `public virtual void Uninstall(IEntity entity)` (строка 44). Вариант `Uninstall()` без параметра, который печатает `Docs/BestPractices/UsingSubscriptionsWithDisposeComposite.md`, не компилируется.

Антипаттерн:

```csharp
// ПЛОХО
public override void Install(IEntity entity)
{
    ISignal fireEvent = entity.GetValue<ISignal>("FireEvent");
    fireEvent.OnEvent += _fireVFX.Play;
    fireEvent.OnEvent += _fireSFX.Play;
    fireEvent.OnEvent += () => _animator.SetTrigger("Fire");   // снять невозможно
}

public override void Uninstall(IEntity entity)
{
    // ...и здесь кто-то руками повторяет три строки, забывая одну
}
```

### 3. Завернуть чужое событие в `ISignal` через `InlineSignal`

```csharp
// Предполагаем, что на gameObject есть компонент "TriggerEvents"
TriggerEvents triggerEvents = ...;

ISignal<Collider> onTriggerEnter = new InlineSignal<Collider>(
    subscribe: action => triggerEvents.OnEntered += action,
    unsubscribe: action => triggerEvents.OnEntered -= action
);

Subscription<Collider> subscription = onTriggerEnter.Subscribe(
    collider => Debug.Log($"On Trigger Entered: {collider.name}")
);

// Позже
subscription.Dispose();
```

Две лямбды вместо класса-адаптера. `InlineSignal` не хранит подписчиков (`add => this.subscribe.Invoke(value)`), поэтому источник истины один — настоящее событие, и терять в адаптере нечего.

Именованные аргументы `subscribe:` / `unsubscribe:` — часть идиомы, а не стиль. Оба параметра конструктора имеют один и тот же тип делегата (`Action<Action<T>>` дважды), так что позиционный вызов с перепутанным порядком компилируется молча и даёт сигнал, который отписывается при подписке.

Антипаттерн — свой `MonoBehaviour`-адаптер, который слушает `OnEntered` и перебрасывает в собственный `Event<Collider>`: двойное хранилище подписчиков, двойная бухгалтерия отписок и второе место, где обработчики могут протечь.

### 4. Реквест: произвести в Tick, разобрать в FixedTick

```csharp
entity.AddValue("MoveRequest", new Request<Vector3>());
entity.AddBehaviour<MoveController>();
entity.AddBehaviour<MoveBehaviour>();
```

```csharp
public sealed class MoveController : IEntityInit, IEntityTick
{
    private IRequest<Vector3> _moveRequest;

    public void Init(IEntity entity)
    {
        _moveRequest = entity.GetValue<IRequest<Vector3>>("MoveRequest");
    }

    public void Tick(IEntity entity, float deltaTime)
    {
        float dx = Input.GetAxis("Horizontal");
        float dz = Input.GetAxis("Vertical");
        Vector3 moveDirection = new Vector3(dx, 0, dz);
        _moveRequest.Invoke(moveDirection);
    }
}
```

```csharp
public sealed class MoveBehaviour : IEntityInit, IEntityFixedTick
{
    private Transform _transform;
    private IValue<float> _moveSpeed;
    private IRequest<Vector3> _moveRequest;

    public void Init(IEntity entity)
    {
        _transform = entity.GetValue<Transform>("Transform");
        _moveSpeed = entity.GetValue<IValue<float>>("MoveSpeed");
        _moveRequest = entity.GetValue<IRequest<Vector3>>("MoveRequest");
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        if (_moveRequest.Consume(out Vector3 moveDirection))
            _transform.position += moveDirection * Time.fixedDeltaTime * _moveSpeed.Value;
    }
}
```

Производитель и потребитель не знают друг о друге — у них общий только слот на сущности. Поменять `MoveController` на `AIFollowBehaviour` можно без правок в `MoveBehaviour`; в документации это показано как раз такой подменой.

Все зависимости разрешены один раз в `Init` в типизированные поля. В пофреймовом пути нет ни поиска, ни приведения типов, ни аллокаций. `Consume(out ...)` прямо в условии `if` — это и есть вся идиома: один вызов проверяет, извлекает и сбрасывает, поэтому сколько бы `Invoke` ни случилось в `Update`, физический шаг будет ровно один.

Поля — интерфейсы (`IRequest<Vector3>`, `IValue<float>`), классы — `sealed`.

Антипаттерн:

```csharp
// ПЛОХО
public void FixedTick(IEntity entity, float deltaTime)
{
    if (_moveRequest.Required)
    {
        Vector3 dir = _moveRequest.Arg;
        Move(dir);
        // флаг никто не сбросил — применится снова на следующем FixedTick
    }
}
```

`Required` + `Arg` без `Consume` возвращают ту самую двойную отработку, ради предотвращения которой `Consume` и существует. Второй вариант того же антипаттерна — двигать персонажа прямо из `Update`, ради чего отсрочка и вводилась.

### 5. Реквест как локальный буфер Update → FixedUpdate внутри MonoBehaviour

```csharp
public class AttackComponent : MonoBehaviour
{
    private readonly IRequest<GameObject> _request = new Request<GameObject>();

    public bool IsAttack => _request.Required;

    // Вызывается из Update — Input System или UI
    public void Attack(GameObject target)
    {
        _request.Invoke(target);
    }

    private void FixedUpdate()
    {
        if (_request.Consume(out GameObject target))
        {
            // нанести урон / выпустить снаряд
        }
    }
}
```

Шаблон работает вообще без `Atomic.Entities`. Поле `readonly`, типизировано **интерфейсом**, но инициализировано конкретным классом — компонент сам может пользоваться только контрактом. `IsAttack => _request.Required` отдаёт наружу намерение только для чтения, не отдавая сам реквест. Это та форма, в которую стоит переводить легаси-`MonoBehaviour` до того, как они станут entity-поведениями.

Антипаттерн — пара `bool _attackQueued;` + `GameObject _queuedTarget;`, которую руками сбрасывают в трёх разных местах.

### 6. Request-Condition-Action-Event

Четыре независимо заменяемых слота на сущности: намерение, проверка, исполнение, оповещение.

```csharp
IFunction<bool> isGrounded = ...;   // например, InlineFunction<bool>
IValue<int> energy = ...;
Rigidbody rigidbody = ...;

entity.AddValue("JumpRequest", new Request());

entity.AddValue("JumpCondition", new AndExpression(
    new ExpressionMember<bool>(isGrounded),
    new ExpressionMember<bool>(() => energy.Value > 0)
));

entity.AddValue("JumpAction", new InlineAction(
    () => rigidbody.AddForce(Vector3.up, ForceMode.Impulse)
));

entity.AddValue("JumpEvent", new Event());

entity.AddBehaviour<JumpBehaviour>();
```

```csharp
public sealed class JumpBehaviour : IEntityInit, IEntityFixedTick
{
    private IRequest _jumpRequest;
    private IFunction<bool> _jumpCondition;
    private IAction _jumpAction;
    private IEvent _jumpEvent;

    public void Init(IEntity entity)
    {
        _jumpRequest = entity.GetValue<IRequest>("JumpRequest");
        _jumpCondition = entity.GetValue<IFunction<bool>>("JumpCondition");
        _jumpAction = entity.GetValue<IAction>("JumpAction");
        _jumpEvent = entity.GetValue<IEvent>("JumpEvent");
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        if (_jumpCondition.Invoke() && _jumpRequest.Consume())
        {
            _jumpAction.Invoke();
            _jumpEvent.Invoke();
        }
    }
}
```

Поведение — чистая проводка, в нём нет ни строчки логики самого прыжка, поэтому дизайнер может перенастроить условие или подменить действие без правок кода. Все поля — интерфейсы. Пофреймовая цена — одно условие с коротким замыканием.

Два места, где этот сниппет расходится с `Docs/BestPractices/RequestConditionActionEvent.md`, и оба — не придирки.

**Первое: `.Value` на `IFunction<bool>` не существует.** `IFunction<out R>` (`Elements/Scripts/Functions/IFunction.cs`) объявляет ровно один член — `R Invoke()`. `Value` есть у `ExpressionBase<R>` (`Elements/Scripts/Expressions/ExpressionBase.cs:30`, `public R Value => this.Invoke();`) и у `IValue<out T>`. Код из документации с полем типа `IFunction<bool>` и обращением `_jumpCondition.Value` не компилируется. Либо вызывайте `Invoke()`, как выше, либо типизируйте поле как `IValue<bool>`.

**Второе: `AndExpression` не принимает делегаты напрямую.** Конструктор — `public AndExpression(params ExpressionMember<bool>[] members)` (`Expressions/AndExpression.cs:24`), а `ExpressionMember<R>` — структура, у которой нет ни одного оператора неявного преобразования; собрать её можно только конструктором — их четыре: `ExpressionMember(Func<R>)`, `ExpressionMember(IFunction<R>)`, `ExpressionMember(object source, Func<R>)`, `ExpressionMember(object source, IFunction<R>)`. Каждый член приходится оборачивать явно. Форма из документации (`new AndExpression(entity.GetHealth().Exists, entity.GetEnergy().Exists)`) не компилируется.

**Третье, и это уже про геймплей, а не про компиляцию: порядок в условии определяет игровое ощущение.**

```csharp
// «Тратим попытку»: реквест съедается, даже если условие не прошло
if (_jumpRequest.Consume() && _jumpCondition.Invoke())

// «Держим намерение»: реквест остаётся в силе, пока условие не станет истинным
if (_jumpCondition.Invoke() && _jumpRequest.Consume())
```

Документация показывает первую форму и не отмечает выбор. Это два разных ощущения ввода (нажал в воздухе — прыжок проглочен, против нажал в воздухе — прыгнул при касании земли). Выбирайте осознанно и одинаково по всей игре.

Антипаттерн — один метод `Jump()`, который читает ввод, проверяет землю и энергию, прикладывает силу и играет звук: четыре причины для изменения в одном месте и невозможность протестировать без `Rigidbody` и `AudioSource`.

### 7. Массовая подписка списка `IAction` на один сигнал

```csharp
ISignal fireSignal = ...;

var actions = new List<IAction>
{
    new InlineAction(() => Debug.Log("Fire VFX")),
    new InlineAction(() => Debug.Log("Fire SFX"))
};

fireSignal.SubscribeRange(actions);
```

`SubscribeRange` проверяет на `null` и саму коллекцию, и каждый элемент — в этом весь смысл перегрузки, потому что такие списки обычно приходят из `[SerializeReference]`-полей, и полузаполненный массив в инспекторе не должен ронять установку.

Снятие парным вызовом работает:

```csharp
fireSignal.UnsubscribeRange(actions);
```

Делегаты в `SubscribeRange` и `UnsubscribeRange` строятся одним и тем же выражением (`action.Invoke` по переменной того же интерфейсного типа), поэтому `-=` сравнивает равные делегаты и обработчик действительно снимается. Цена — по одному выброшенному `Action` на каждый элемент при подписке и при отписке. Если список переживает несколько циклов install/uninstall, дешевле держать `Subscription`-токены в `DisposableComposite<Subscription>`; `UnsubscribeRange` уместен там, где он вызывается один раз на снос объекта.

### 8. ThreadSafeEvent для «готово», а не для «ещё одно попадание»

```csharp
public sealed class AssetLoader
{
    private readonly ThreadSafeEvent<float> _progress = new();

    public ISignal<float> Progress => _progress;

    // вызывается с пула потоков
    private void OnChunkLoaded(float value) => _progress.Invoke(value);
}
```

Форма правильная, потому что наружу отдан `ISignal<float>` — подписчик не может вызвать событие с чужого потока, а внутренний `Invoke` остаётся у владельца. И потому, что `float progress` — это именно «последнее значение»: схлопывание десяти вызовов за кадр в один колбэк с последним числом здесь не баг, а то, что нужно.

Антипаттерн — то же самое для событий, где важен каждый случай:

```csharp
// ПЛОХО
private readonly ThreadSafeEvent<int> _damageTaken = new();
// ... из четырёх джобов за один кадр
_damageTaken.Invoke(10);
_damageTaken.Invoke(15);
_damageTaken.Invoke(7);
// подписчик получит ОДИН вызов со значением 7; 10 и 15 потеряны
```

---

## Производительность

Сразу про статус утверждений: **измеренного здесь нет ничего**. В `Elements/Tests/Performance` лежат только бенчмарки коллекций (`ArrayPerformance.cs`, `ListPerformance.cs`, `ReactiveListPerformance.cs` и т. д.) — ни одного по событиям или реквестам. Всё ниже — либо прочитано в исходнике (помечено «источник»), либо следует из семантики C#/.NET (помечено «вывод»).

**`AggressiveInlining` расставлен асимметрично.** Источник: `Elements/Scripts/Events/Extensions.cs`. Атрибут `[MethodImpl(MethodImplOptions.AggressiveInlining)]` стоит на всех пяти `Subscribe(Action...)`, всех пяти `Subscribe(IAction...)`, всех пяти `Unsubscribe(Action...)`, всех пяти `Unsubscribe(IAction...)` и всех пяти `SubscribeRange` — 25 методов. На пяти `UnsubscribeRange` его нет. Похоже на недосмотр в поставке, а не на решение.

**`IsRequired` инлайнится весь.** Источник: `Elements/Scripts/Requests/Extensions.cs` — все пять перегрузок с `AggressiveInlining` и без проверки на `null`. Вывод: `request.IsRequired` по стоимости совпадает с `request.Required`, так что использовать его как группу методов в выражениях ничего не стоит.

**`Subscription` не ходит в куче, пока вы её туда не положите.** Источник: `Elements/Scripts/Events/Subscription.cs` — `readonly struct` с ровно двумя ссылочными полями (`signal`, `action`), `ArgumentNullException` на оба аргумента конструктора, `Dispose()` = `if (signal != null) signal.OnEvent -= action`. Вывод: в локальной переменной или в поле конкретного типа — нулевой трафик GC; `default(Subscription).Dispose()` безопасен. Точный размер структуры зависит от платформы (два указателя), поэтому конкретных байтов не называю.

**Бокс при композиции подписок — выбираемый, а не обязательный.** Источник: `Elements/Scripts/Utils/Extensions.cs` содержит две перегрузки `AddTo`; `Elements/Scripts/Utils/DisposableComposite`1.cs` — `List<T>` с `_items[i].Dispose()` без приведения. Вывод: `AddTo(IDisposable, DisposableComposite)` упаковывает структуру (один маленький объект на подписку), `AddTo<T>(T, DisposableComposite<T>)` с `DisposableComposite<Subscription>` — нет. Для десятка подписок разница несущественна, для инсталлера, который ставится на сотни сущностей, — нет.

Одна деталь, которую стоит знать: `DisposableComposite<T>.Add` делает `if (disposable == null) throw`. Для `T = Subscription` это сравнение структуры с `null`, которое компилируется (ограничение `T : IDisposable` — не `struct`) и всегда ложно. Вывод, не измерение: JIT в инстанциации по значимому типу обычно сворачивает `box` + проверку на `null` в константу, так что лишней аллокации там нет; если это критично, проверьте на своей платформе.

**Реквест аллокационно-свободен в установившемся режиме.** Источник: `Elements/Scripts/Requests/Request.cs`. `Invoke` — N записей в поля плюс `_required = true`; `TryGet`/`Consume` — N чтений плюс проверка `bool`. Аргументы лежат в типизированных полях (`private T _arg;`), значит `Request<Vector3>` держит `Vector3` по значению: ни объекта, ни упаковки. Классы не `sealed`, так что девиртуализация вызовов остаётся на усмотрение JIT; впрочем, тип поля всё равно должен быть интерфейсом.

**У `Consume` out-аргументы пишутся до проверки флага.** Источник: во всех пяти телах `Consume` сначала идёт присваивание `arg = _arg`, и только потом `if (!_required) return false`. Вывод: при `false` вы получаете предыдущее (или `default`) значение, а не гарантированный `default`. Читать out-параметр можно только внутри ветки `if`.

**У `Event` дорогая сторона — подписка, а не вызов.** Источник: `Event.Invoke()` — это `this.OnEvent?.Invoke(...)`, `Dispose()` — `OnEvent = null`. Ни массива подписчиков, ни пула, ни struct-энумератора: это обёртка над `System.Action`. Вывод (стандартное поведение .NET, не утверждение исходника): `+=`/`-=` на field-like событии создают новый делегат, а для мультикаста — ещё и новый внутренний массив. Значит, подписываться и отписываться каждый кадр — это то, чего надо избегать; вызывать — нет.

**`InlineSignal` ничего не учитывает.** Источник: `add`/`remove` — по одному вызову делегата, своих полей кроме двух `readonly`-делегатов нет. Вывод: один лишний прыжок через делегат на подписку/отписку и ноль пофреймовой бухгалтерии. Цена конструирования — сам объект плюс два замыкания, поэтому строить его надо в `Install`/`Init`, а не в `Tick`.

**`ThreadSafeEvent`: неродовой вариант дешевле на вызове.** Источник: `ThreadSafeEvent.Invoke()` — голый `MarkDirty(this)` без `lock`; каждая генерик-арность берёт `lock (_lock)`, чтобы сложить аргументы. `Dispose` везде — `Interlocked.Exchange(ref OnEvent, null)` (законно только внутри объявляющего класса). Вывод: если событие поднимается из горячего джоба и аргументы не нужны, неродовой `ThreadSafeEvent` не создаёт конкуренции за блокировку.

**Схлопывание — это `HashSet`.** Источник: `MarkDirty` вставляет в `HashSet<IFlushable>`, генерики перезатирают `_v*` под блокировкой. Вывод: повторные `Invoke` в пределах кадра ничего не стоят, но и ничего не доставляют, кроме последнего значения.

**`MainThreadDispatcher` написан аккуратно.** Источник: обе статические коллекции предразмерены на 256; блокировка держится только на `AddRange` + `Clear` в `_processing`; слив идёт вне блокировки индексным `for` с вынесенным `count`; после — `_processing.Clear()`. Вывод, важный для порядка кадров: `_processing` не защищён от реентерабельности, то есть обработчик, который во время `Flush()` поднимает другой `ThreadSafeEvent`, помечает его грязным для **следующего** `Update`, а не рекурсирует. Это безопасный исход, но каждое звено цепочки событий добавляет кадр задержки.

**`SubscribeRange`/`UnsubscribeRange` принимают `IEnumerable`.** Источник: сигнатуры и `foreach` по интерфейсу. Вывод: передача `List<T>` упаковывает его энумератор (одна аллокация). Это install-time API; в `Tick` их звать не надо. Struct-энумераторного быстрого пути здесь нет.

**Того, что обычно ищут в «производительном» модуле, тут нет вообще.** Источник: по `Elements/Scripts/Events` и `Elements/Scripts/Requests` нет ни struct-энумераторов, ни `Span`, ни `in`/`ref`-параметров, ни пулинга. Массивное хранилище с открытой адресацией в этой предметной области живёт в другом модуле — `Atomic.Events.EventBus` (`EventSlot[] _slots` + `int[] _buckets`, свободный список, рост по простым числам, ключи — `int`, а не строки).

**Тестовое покрытие тонкое, и для миграции это фактор риска.** Источник: единственный тестовый файл на всю область — `Elements/Tests/EditMode/Events/EventTests.cs` (398 строк, по три теста `Subscribe`/`Unsubscribe`/`Dispose` на `Event`, `Event<T>`, `Event<T1,T2>`, `Event<T1,T2,T3>`). Папки тестов для `Requests` под `Elements/Tests` нет. Нет ни одного теста на `Request`/`IRequest`, `Subscription`, `InlineSignal`, `ThreadSafeEvent`, `MainThreadDispatcher`, `SubscribeRange`/`UnsubscribeRange` и `IAction`-перегрузки `Subscribe`. Если вы меняете что-то в этих типах — писать тесты придётся самим.

---

## Грабли

- **Типов `BaseEvent` и `BaseRequest` не существует.** Реальные — `Atomic.Elements.Event`/`Event<T>` и `Atomic.Elements.Request`/`Request<T>`. При этом имена `BaseEvent(...)`/`BaseRequest(...)` стоят в примерах кода у `Docs/BestPractices/UsingEvents.md:29`, `UsingRequests.md:29,87`, `RequestConditionActionEvent.md:52,60`, `UsingCooldownInGameMechanics.md:82,97,152,327,342,393`, `UsingInlineActions.md:37,115`, `UsingOptionalWithInstallers.md:41,42`, `OverrideEntityFactoriesWithBakers.md:178` и даже у `Docs/Elements/Events/IEvents.md:25,40`. Файлы справочника тоже называются `BaseEvent.md`/`BaseRequest.md`, хотя блок `## Type` внутри печатает правильное `public class Event<T>`. Это самый вероятный источник ошибок компиляции при копировании примеров: мысленно убирайте `Base`.

- **`Event` конфликтует по имени.** `UnityEngine.Event` существует (IMGUI). В файле с `using UnityEngine; using Atomic.Elements;` простое `new Event()` станет неоднозначным. Квалифицируйте (`new Atomic.Elements.Event()`) или заводите алиас. То же касается собственного типа `Event`, если он у вас есть в проекте.

- **Два разных типа с именем `Subscription`.** `Atomic.Elements.Subscription` (сигнал + делегат) и `Atomic.Events.Subscription` (`IEventBus` + `int` ключ + `Action`, конструктор `internal`). Файл с `using` обоих пространств имён даёт ошибку неоднозначности или, что хуже, для какой-то арности молча берёт не тот. Один из них алиасьте.

- **`Dispose()` у события снимает всех.** `Event.Dispose()` и `ThreadSafeEvent.Dispose()` обнуляют весь список обработчиков. Вызов его «чтобы снять свой» молча снимает и чужие. Снять свой — это `Subscription.Dispose()` или `Unsubscribe(именованныйМетод)`.

- **`Subscription` подписывает в конструкторе.** `_ = signal.Subscribe(x);` или проигнорированный результат `new Subscription(...)` — это всё равно состоявшаяся подписка, просто без возможности её снять. Никогда не конструируйте `Subscription` ради значения.

- **Лямбда в `OnEvent +=` без токена — гарантированная утечка.** У лямбды нет имени, под которым её можно передать в `-=`. Либо храните делегат в поле, либо держите `Subscription`.

- **`Consume` пишет out-аргументы до проверки флага.** На `false` в out-параметре лежит прошлое или `default` значение. Читайте его только внутри `if`.

- **`Required` живёт вечно, пока его не разберут.** Реквест, произведённый в `Update`, при отключённом потребителе остаётся в силе и отработает на кадре включения. API «сбросить, не потребляя» нет — только `Consume()` с выбросом результата.

- **Порядок `Consume()` в составном условии — это геймплей.** `Consume() && condition` тратит намерение на неудачной проверке; `condition && Consume()` сохраняет его. Разные ощущения ввода, одинаково компилируются.

- **`MainThreadDispatcher.Flush()` вызвать нельзя.** `Docs/Elements/Events/ThreadSafeEvent.md` на строках 33 и 42 показывает такой вызов, а строка 297 прямо советует «вызывать `MainThreadDispatcher.Flush()` на главном потоке каждый кадр». Тип `internal sealed class ... : MonoBehaviour` с приватным `Update()` и без публичного `Flush` — из игровой сборки он недостижим. Доставка и так автоматическая. Следствия: `ThreadSafeEvent` никогда не срабатывает в фазе `FixedUpdate`, и в EditMode-тесте без игрового цикла не срабатывает вовсе.

- **`MainThreadDispatcher.IsMainThread` тоже недостижим.** Он `public`, но на `internal`-типе. Проверку «я на главном потоке?» придётся писать свою.

- **`ThreadSafeEvent` схлопывает вызовы.** Повторю, потому что это тихая потеря данных: N вызовов за кадр = один колбэк с последними аргументами. Для урона, подтверждений попаданий и спавна снарядов не годится; для прогресса загрузки, «ассет готов», снимка счёта — годится.

- **`[SerializeField] private T _arg` в `Request<T>` — декорация для открытых генериков.** Unity не сериализует поля открытых генериков, и `T` может быть в принципе несериализуемым (`IEntity`, интерфейс, `GameObject` внутри генерика). Фраза «Supports Unity serialization» на страницах `BaseRequest*.md` для генерик-арностей — пожелание.

- **`[InlineProperty]` расставлен несимметрично.** У `Event` он на всех пяти арностях, у `Request` — только на неродовом. Если вы ждёте одинакового вида в инспекторе Odin — не ждите.

- **У `InlineSignal` оба параметра конструктора одного типа.** Перепутанный порядок компилируется и даёт сигнал, который отписывается при подписке. Всегда именованные аргументы.

- **`IFunction<bool>` не имеет `Value`.** Только `Invoke()`. `Value` есть у `IValue<out T>` и у `ExpressionBase<R>`. Пример RCAE в документации с полем `IFunction<bool>` и обращением `.Value` не компилируется.

- **`AndExpression` требует явного `ExpressionMember<bool>`.** Неявных преобразований у `ExpressionMember<R>` нет; оборачивайте каждый член конструктором.

- **Ни у `ISignal`, ни у `Event` нет счётчика подписчиков и способа их перечислить.** Если это нужно — такой член (`bool IsSubscribed(int key)`) есть у `Atomic.Events.IEventBus`, а не здесь.

- **Примеры в документации местами не компилируются сами по себе.** `Docs/Elements/Requests/Manual.md` объявляет `shootRequest` (строки 30, 33), а затем на строках 36 и 42 обращается к необъявленному `shoot` (`if (shoot.Required)`, `if (shoot.Consume())`). `Docs/BestPractices/UsingRequests.md` на строках 70 и 140 печатает `public void FixedTick((IEntity entity, float deltaTime))` с удвоенными скобками — правильную сигнатуру смотрите в `RequestsVsActions.md`. `Docs/Elements/Events/Manual.md` содержит вложенное открытие ```` ```csharp ```` на строке 51 внутри третьего примера, из-за чего блок ломается при рендере.

- **Ссылки в `Docs/Elements/Requests/Manual.md` битые.** Он ссылается на `Requests.md`, `Request.md`, `Request`1.md`…`Request`4.md`, которых в папке нет (файлы называются `BaseRequests.md`, `BaseRequest.md`, `BaseRequest`1.md`…). В `Docs/Elements/Events/Manual.md` все ссылки, наоборот, валидны.

- **`Atomic.Contexts` в этом апстриме нет.** Контекстные общие события заменены отдельным модулем `Atomic.Events` (`IEventBus`/`EventBus`/`ThreadSafeEventBus`, обёртки `MonoEventBus`/`MonoEventBusSingleton`). У него другое пространство имён, другой `Subscription` и максимальная арность 3 против 4 в `Elements`.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Исходники (приоритет над документацией во всех спорных случаях)

| Файл | Что внутри |
|---|---|
| `Assets/Plugins/Atomic/Elements/Scripts/Events/ISignal.cs` | 5 интерфейсов `ISignal`…`ISignal<out T1..T4>`; единственное место, где видно `out` |
| `Assets/Plugins/Atomic/Elements/Scripts/Events/IEvent.cs` | 5 интерфейсов `IEvent`, все с пустым телом |
| `Assets/Plugins/Atomic/Elements/Scripts/Events/Event.cs` | 5 классов `Event`; атрибуты Odin, `Invoke`, `Dispose` |
| `Assets/Plugins/Atomic/Elements/Scripts/Events/Subscription.cs` | 5 `readonly struct Subscription`; подписка в конструкторе, `null`-гарды |
| `Assets/Plugins/Atomic/Elements/Scripts/Events/InlineSignal.cs` | 5 классов `InlineSignal`; `add`/`remove` через два делегата |
| `Assets/Plugins/Atomic/Elements/Scripts/Events/ThreadSafeEvent.cs` | 5 `sealed` классов; `_value` (у `<T>`) / `_v1.._v4` + `_hasValue` под `_lock`, `Interlocked.Exchange` в `Dispose` |
| `Assets/Plugins/Atomic/Elements/Scripts/Events/Extensions.cs` | 30 методов: `Subscribe`/`Unsubscribe` × (`Action`/`IAction`) × 5 арностей, `SubscribeRange`/`UnsubscribeRange` × 5 |
| `Assets/Plugins/Atomic/Elements/Scripts/Requests/IRequest.cs` | 5 интерфейсов `IRequest`; `Required`, `Arg*`, `TryGet`, `Consume` |
| `Assets/Plugins/Atomic/Elements/Scripts/Requests/Request.cs` | 5 классов `Request`; типизированные поля аргументов, `[SerializeField]`, кнопки Odin |
| `Assets/Plugins/Atomic/Elements/Scripts/Requests/Extensions.cs` | 5 перегрузок `IsRequired`, все инлайнятся; в документации не описаны |
| `Assets/Plugins/Atomic/Elements/Scripts/Utils/MainThreadDispatcher.cs` | `internal sealed` MonoBehaviour: `IFlushable`, `MarkDirty`, `_dirty`/`_processing`, самобутстрап |
| `Assets/Plugins/Atomic/Elements/Scripts/Utils/Extensions.cs` | две перегрузки `AddTo` — боксящая и нет |
| `Assets/Plugins/Atomic/Elements/Scripts/Utils/DisposableComposite.cs` | `DisposableComposite : DisposableComposite<IDisposable>`, пустое тело |
| `Assets/Plugins/Atomic/Elements/Scripts/Utils/DisposableComposite`1.cs` | `List<T>`-композит: `Count`, `Add` с возвратом себя, `Dispose` с очисткой |
| `Assets/Plugins/Atomic/Elements/Scripts/Actions/IAction.cs` | 5 интерфейсов `IAction`, все контравариантны (`in`) |
| `Assets/Plugins/Atomic/Elements/Scripts/Actions/Core/InlineAction.cs` | `InlineAction`×5: обёртка `Action` в `IAction` |
| `Assets/Plugins/Atomic/Elements/Scripts/Functions/IFunction.cs` | `IFunction<out R>` с единственным `R Invoke()` — здесь видно, что `Value` нет |
| `Assets/Plugins/Atomic/Elements/Scripts/Expressions/AndExpression.cs` | конструкторы `AndExpression`, в т. ч. `params ExpressionMember<bool>[]` |
| `Assets/Plugins/Atomic/Elements/Scripts/Expressions/ExpressionMember.cs` | структура-член выражения; только конструкторы, преобразований нет |
| `Assets/Plugins/Atomic/Elements/Tests/EditMode/Events/EventTests.cs` | единственные тесты области: `Subscribe`/`Unsubscribe`/`Dispose` для 4 арностей `Event` |
| `Assets/Plugins/Atomic/Entities/Scripts/Installers/MonoEntityInstaller.cs` | настоящие сигнатуры `Install(IEntity)` / `Uninstall(IEntity)` |
| `Assets/Plugins/Atomic/Events/Scripts/Subscription/Subscription.cs` | одноимённая структура из другого модуля — причина конфликта `using` |

### Документация: `Docs/Elements/Events`

- `Manual.md` — входная точка области: 4 примера (неродовое событие, событие с аргументом, две разновидности подписки) и полный индекс ссылок на страницы типов. Ссылки валидны; в третьем примере сломан блок кода (вложенный открывающий фенс на строке 51).
- `ISignals.md` — обзор семейства `ISignal` с тремя примерами (без аргументов, один аргумент, три аргумента).
- `ISignal.md`, `ISignal`1.md`, `ISignal`2.md`, `ISignal`3.md`, `ISignal`4.md` — по странице на арность: блок `## Type`, описание `OnEvent`, короткий пример. **Внимание:** `## Type` печатает `ISignal<T>` без `out` — в исходнике ковариантность есть.
- `IEvents.md` — обзор `IEvent` с тремя примерами; в примерах используется несуществующий `BaseEvent` (строки 25 и 40).
- `IEvent.md`, `IEvent`1.md`, `IEvent`2.md`, `IEvent`3.md`, `IEvent`4.md` — по арности: `## Type`, `OnEvent`, `Invoke`. Полезны тем, что явно показывают: своих членов у `IEvent` нет.
- `BaseEvents.md` — обзор конкретных классов `Event` (заголовок страницы — «Events»), три примера, индекс ссылок. Имя файла с `Base`, содержимое — про `Event`.
- `BaseEvent.md`, `BaseEvent`1.md`, `BaseEvent`2.md`, `BaseEvent`3.md`, `BaseEvent`4.md` — справочник по арностям `Event`: `## Type` (печатает корректно `[Serializable] public class Event<T> : IEvent<T>, IDisposable`), `OnEvent`, `Invoke(...)`, `Dispose()`, пример.
- `ThreadSafeEvent.md` — самая объёмная страница области (297 строк): пример, затем `## Type` на все пять арностей и раздел про потокобезопасность. Здесь же и дезинформация про `MainThreadDispatcher.Flush()` (строки 32–33, 42, 57, 297).
- `InlineSignals.md` — обзор с одним примером: обёртка `TriggerEvents.OnEntered` в `InlineSignal<Collider>` с именованными аргументами. Это канонический сниппет, который стоит скопировать.
- `InlineSignal.md`, `InlineSignal`1.md`, `InlineSignal`2.md`, `InlineSignal`3.md`, `InlineSignal`4.md` — только справочник (`## Type`, конструктор, `OnEvent`), примеров внутри нет.
- `Subscriptions.md` — обзор с двумя примерами: неродовая и генерик-подписка через конструктор `new Subscription(...)`.
- `Subscription.md`, `Subscription`1.md`…`Subscription`4.md` — по арности: `## Type` (`public readonly struct Subscription<T> : IDisposable`), конструктор, `Dispose()`, пример.
- `ExtensionsSubscribe.md` — 282 строки: четыре примера (`Action`, `Action<T>`, `IAction`, `IAction<T>`) плюс полный перечень перегрузок. Нужна, чтобы увидеть, что `IAction`-вариант существует на всех арностях.
- `ExtensionsUnsubscribe.md` — 321 строка, зеркальная структура. Показывает `signal.Unsubscribe(action)` для `IAction` как рекомендованный способ снятия; это действительно работает, но стоит одного выброшенного делегата на вызов.
- `ExtensionsSubscribeRange.md` — 213 строк: массовая подписка, два примера (неродовой и генерик), упоминание пропуска `null`-элементов.
- `ExtensionsUnsubscribeRange.md` — 221 строка, зеркальная. Парное снятие, показанное здесь, корректно.

### Документация: `Docs/Elements/Requests`

- `Manual.md` — входная точка: три примера (без аргумента, с аргументом, использование внутри `MonoBehaviour`), раздел `## Notes` с семантикой `Required`/`TryGet`/`Consume` и ссылки на best practices. Третий пример (`AttackComponent`) — лучший в области, его можно брать как есть. Первый пример сломан (необъявленный `shoot` на строках 36 и 42), а индекс ссылок указывает на несуществующие `Requests.md`/`Request*.md`.
- `IRequests.md` — обзор семейства `IRequest` с двумя примерами; объясняет, что `IRequest` расширяет `IAction`.
- `IRequest.md`, `IRequest`1.md`, `IRequest`2.md`, `IRequest`3.md`, `IRequest`4.md` — по арности: `## Type`, `Required`, `Arg*`, `Invoke`, `Consume(out ...)`, `TryGet(out ...)`, пример.
- `BaseRequests.md` — обзор конкретных классов `Request` с выделенным `IMPORTANT` про отложенное исполнение и отсутствие дублей при повторных `Invoke`. Короткая и по делу.
- `BaseRequest.md`, `BaseRequest`1.md`…`BaseRequest`4.md` — справочник по арностям `Request`; `## Type` печатает корректное `[Serializable] public class Request<T> : IRequest<T>`. Фразу про поддержку сериализации Unity для генерик-арностей читайте критически.

### Документация: `Docs/BestPractices` (относится к этой области)

- `UsingEvents.md` — пошаговый разбор разделения `ISignal`/`IAction` на примере `FireSFXBehaviour`. Содержательно — главный текст по конвенции «событие раздаётся узкими интерфейсами»; в коде надо заменить `BaseEvent` на `Event`.
- `UsingRequests.md` — два сценария произвести/разобрать: ввод игрока (`MoveController`) и ИИ (`AIFollowBehaviour`), оба с общим `MoveBehaviour`. Показывает, что подмена производителя не трогает потребителя. В коде — `BaseRequest<Vector3>` и удвоенные скобки в `FixedTick` (строки 70 и 140).
- `RequestsVsActions.md` — когда брать немедленное действие, а когда отложенный реквест; отдельный раздел про мультиплеер (в сетевых играх рекомендуются действия, потому что флаги реквестов требуют синхронизации). Здесь же корректная сигнатура `FixedTick(IEntity entity, float deltaTime)` — предпочитайте версию из этого файла.
- `RequestConditionActionEvent.md` — описание RCAE: четыре роли, пример сборки механики прыжка и поведение-проводка. Самый полезный текст по архитектуре и одновременно самый проблемный по коду: `BaseRequest`/`BaseEvent`, `AndExpression` без обёртки в `ExpressionMember<bool>`, `.Value` на `IFunction<bool>`, и порядок `Consume() && condition` без оговорки.
- `UsingSubscriptionsWithDisposeComposite.md` — `DisposableComposite` в инсталлере (`WeaponViewInstaller`), включая подписку лямбды, которую иначе не снять. Сигнатуру `Uninstall()` в примере надо читать как `Uninstall(IEntity entity)`.
- `PreferAbstractInterfaces.md`, `UsingSerializeReferenceForCompositeActions.md`, `UsingInlineActions.md`, `UsingInlineFunctions.md`, `UsingExpressions.md` — соседние тексты, на которые опираются идиомы 6 и 7 этой главы (композитные действия из инспектора, `InlineAction`/`InlineFunction`, выражения-условия).
- `Docs/Elements/Utils/DisposableComposite.md`, `Docs/Elements/Utils/DisposableComposite`1.md`, `Docs/Elements/Utils/Extensions.md` — справочник по композиту и `AddTo`; смотреть обязательно, если выбираете между боксящей и небоксящей перегрузкой.

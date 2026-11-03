# 03. Elements: значения, переменные, сеттеры

`Atomic.Elements` — нижний слой фреймворка: мелкие контейнеры состояния, из которых собирается игровой объект. Весь слой построен на трёх ортогональных интерфейсах — читать (`IValue<out T>`), писать (`ISetter<in T>`), наблюдать (`ISignal<out T>`) — а каждый конкретный тип есть просто их комбинация. Почти всё помечено `[Serializable]` и видно в инспекторе, поэтому состояние, которое настраивает дизайнер, и состояние, которым управляет код, — это одни и те же объекты. Сюда же относится обвязка: `Optional<T>` для включаемых фич, `Reference<T>` для разделяемой ячейки, `Subscription`/`DisposableComposite` для времени жизни подписок и MonoBehaviour-фасады над Unity-колбэками. Жёсткое требование: `Docs/Elements/Manual.md` заявляет **Unity 6** или **.NET 7+**, и исходники действительно используют default interface implementations из C# 8 — на Unity 2022.3 этот код не соберётся.

---

## Модель

**1. Три способности, а не один «реактивный объект».** Разделение сделано по способности, а не по типу хранения:

| интерфейс | способность | базируется на |
|---|---|---|
| `IValue<out T>` | чтение | `IFunction<T>` |
| `ISetter<in T>` | запись | `IAction<T>` |
| `ISignal<out T>` | наблюдение | — |
| `IVariable<T>` | чтение + запись | `IValue<T>` + `ISetter<T>` |
| `IReactiveValue<out T>` | чтение + наблюдение | `IValue<T>` + `ISignal<T>` |
| `IReactiveVariable<T>` | всё три | `IVariable<T>` + `IReactiveValue<T>` |

Объявленный тип поля — это контракт о том, что потребителю разрешено делать. Контроллер ввода просит `ISetter<Vector3>` и физически не может прочитать состояние; презентер просит `IReactiveValue<int>` и не может его испортить.

**2. `IValue<T>` — это ещё и функция, `ISetter<T>` — это ещё и действие.** `IValue<out T> : IFunction<T>` и `ISetter<in T> : IAction<T>`, причём `Invoke()` реализован как default interface implementation. Практическое следствие: любой сеттер можно передать туда, где ждут `IAction<T>` — например `signal.Subscribe(someSetter)` работает без адаптера.

**3. Вариантность.** `IValue<out T>`, `IReactiveValue<out T>`, `ISignal<out T>` ковариантны; `ISetter<in T>`, `IAction<in T>` контравариантны. `IVariable<T>` и `IReactiveVariable<T>` вынужденно инвариантны (комбинируют `in` и `out`). Это значит, что `IValue<Derived>` напрямую используется как `IValue<Base>` — без обёрток и аллокаций. Обратите внимание: документация (`Docs/Elements/Values/IReactiveValue.md`) показывает `IReactiveValue<T>` без `out` — документация неверна, в исходнике `out T`.

**4. У каждого обобщённого интерфейса есть необобщённый базовый.** `IValue { object Value { get; } }` и `ISetter { object Value { set; } }`. Они существуют для редактора и рефлексии. Любое обращение через них боксирует value-типы, а сеттер делает жёсткий каст `(T) value`. В игровом коде их не использовать никогда.

**5. Реактивность — это не «подписка на свойство», а событие `OnEvent`.** Все реактивные типы несут `public event Action<T> OnEvent` и поднимают его **только при фактическом изменении** — сравнение значения встроено в сеттер. Отсюда главный вывод для миграции: семантика «изменилось» зависит от конкретного типа (см. раздел «Грабли»), и выбор между `ReactiveVariable<Vector3>` и `ReactiveVector3` — это выбор семантики, а не только производительности.

**6. Время жизни подписки — ваша забота, и инструмент для этого — структура.** `Subscribe`/`Observe` возвращают `readonly struct Subscription<T>`, который **подписывается в конструкторе**. Проигнорировали возврат — потеряли единственный способ отписаться.

**7. Odin Inspector опционален, но одна вещь поведенческая.** `Docs/Elements/Manual.md`: «The framework **works without Odin**». Почти всё за `#if ODIN_INSPECTOR` — косметика инспектора, кроме `[HideLabel, OnValueChanged(nameof(InvokeEvent))]` на каждом `Reactive*`. Именно `OnValueChanged` заставляет правку в инспекторе поднять `OnEvent`. Без Odin правка реактивной переменной в инспекторе меняет значение и **не оповещает никого**.

---

## API

### Интерфейсы

`Assets/Plugins/Atomic/Elements/Scripts/Values/IValue.cs`:

```csharp
public interface IValue
{
    object Value { get; }
}

public interface IValue<out T> : IFunction<T>, IValue
{
    new T Value { get; }

    object IValue.Value => Value;

    T IFunction<T>.Invoke() => this.Value;
}
```

Единственная абстракция, против которой должен быть типизирован любой геттер в проекте. Реализатору достаточно написать свойство `Value` — `Invoke()` приходит из default-реализации.

`Scripts/Setters/ISetter.cs`:

```csharp
public interface ISetter
{
    object Value { set; }
}

public interface ISetter<in T> : IAction<T>, ISetter
{
    new T Value { set; }

    object ISetter.Value
    {
        set => Value = (T) value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IAction<T>.Invoke(T arg) => this.Value = arg;
}
```

Контракт только на запись. Берите его для продюсеров (ввод, AI, сеть).

`Scripts/Variables/Base/IVariable.cs`:

```csharp
public interface IVariable<T> : IValue<T>, ISetter<T>
{
    new T Value { get; set; }

    T IValue<T>.Value
    {
        get { return this.Value; }
    }

    T ISetter<T>.Value
    {
        set => this.Value = value;
    }
}
```

Тип по умолчанию для изменяемого состояния сущности. Реализатор пишет одно `public T Value { get; set; }` — два мостика к базовым интерфейсам идут default-реализациями. Эти мостики **не** помечены `AggressiveInlining`, в отличие от `ISetter<T>.Invoke`.

`Scripts/Values/IReactiveValue.cs` и `Scripts/Variables/Reactive/IReactiveVariable.cs`:

```csharp
public interface IReactiveValue<out T> : IValue<T>, ISignal<T>
{
}

public interface IReactiveVariable<T> : IVariable<T>, IReactiveValue<T>
{
}
```

Пустые тела — чистая композиция. `IReactiveValue<T>` — тип, от которого должен зависеть презентер/вью; `IReactiveVariable<T>` — рабочая лошадка для состояния сущности, на которое реагируют системы и UI.

`Scripts/Events/ISignal.cs`:

```csharp
public interface ISignal
{
    event Action OnEvent;
}

public interface ISignal<out T>
{
    event Action<T> OnEvent;
}
// плюс ISignal<out T1, out T2>, <out T1, out T2, out T3>, <out T1, out T2, out T3, out T4>
```

Необобщённый `ISignal` важен: именно он — параметр конструктора `Subscription`.

### Values: константы

`Scripts/Values/Const.cs`:

```csharp
[Serializable]
public class Const<T> : IValue<T>
{
    public T Invoke() => this.value;

    public T Value => this.value;

    [SerializeField]            // #if UNITY_5_3_OR_NEWER
    private T value;

    public Const() { }
    public Const(T value) => this.value = value;

    public static implicit operator Const<T>(T value) => new(value);
    public static implicit operator T(Const<T> value) => value.value;

    public override string ToString() => this.value?.ToString();
}
```

Сериализуемая неизменяемая константа. Это **ссылочный** тип: одна коробка, разделяемая N объектами, без копии payload у каждого. Оба неявных преобразования есть, поэтому `Const<float> speed = 5f;` и `float f = speed;` компилируются. Не `sealed`.

`Scripts/Values/ScriptableConst.cs` (весь файл внутри `#if UNITY_5_3_OR_NEWER`):

```csharp
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Elements/Values/ScriptableConst.md")]
public abstract class ScriptableConst<T> : ScriptableObject, IValue<T>
{
    public T Value => this.value;

    [SerializeField]
    private T value;

    public T Invoke() => this.value;

    public override string ToString() => this.value?.ToString();
}
```

Константа уровня ассета: настроили один раз — видят все, кто ссылается. `abstract`, потому что Unity не сериализует открытый дженерик-ScriptableObject; закрытый подкласс на каждый `T` обязателен. Неявных операторов **нет**, в отличие от `Const<T>`.

`Scripts/Values/InlineValue.cs`:

```csharp
[Serializable]
public class InlineValue<T> : IValue<T>
{
    private readonly Func<T> func;

    public InlineValue(Func<T> func) => this.func = func ?? throw new ArgumentNullException(nameof(func));

    public static implicit operator InlineValue<T>(Func<T> value) => new(value);

    public T Value => this.func.Invoke();

    public T Invoke() => this.func.Invoke();

    public override string ToString() => this.func.Method.Name;
}
```

Лямбда как `IValue<T>` без написания класса. **В документации отсутствует** — нет файла `Docs/Elements/Values/InlineValue.md`, хотя это прямой аналог `Const<T>` для вычисляемых значений. `[Serializable]` здесь вводит в заблуждение: единственное поле — `readonly Func<T>`, который Unity не сериализует. (В XML-доке этого класса остался `<typeparam name="R">` и ссылка на `InlineFunction{T}` — следы копипасты, IntelliSense не покажет документацию по `T`.)

`Scripts/Values/Constants.cs` — предаллоцированные разделяемые `Const<T>`:

```csharp
public static class Constants
{
    public static readonly Const<bool> True = true;
    public static readonly Const<bool> False = false;

    public static readonly Const<float> PI = 3.1415927f;
    public static readonly Const<float> TwoPI = 2 * PI;
    public static readonly Const<float> HalfPI = PI / 2f;
#if UNITY_5_3_OR_NEWER
    public static readonly Const<float> Deg2Rad = Mathf.Deg2Rad;
    public static readonly Const<float> Rad2Deg = Mathf.Rad2Deg;
#endif
    public static readonly Const<float> E = 2.7182818f;
    public static readonly Const<float> GoldenRatio = 1.6180339f;

    public static readonly Const<float> Second = 1f;
    public static readonly Const<float> Minute = 60f;
    public static readonly Const<float> Hour = 3600f;
    public static readonly Const<float> FrameTime60FPS = 1f / 60f;

    public static readonly Const<int>   ZeroInt = 0;
    public static readonly Const<int>   OneInt = 1;
    public static readonly Const<float> Zero = 0f;
    public static readonly Const<float> One = 1f;
    public static readonly Const<float> NegativeOne = -1f;
    public static readonly Const<float> Half = 0.5f;

    public static readonly Const<float> Gravity = 9.81f;

#if UNITY_5_3_OR_NEWER
    public static readonly Const<Vector3> Up = Vector3.up;      // Down, Left, Right, Forward, Back,
                                                                // ZeroVector, OneVector
    public static readonly Const<Color> White = Color.white;    // Black, Red, Green, Blue, Transparent
#endif
}
```

Используйте их вместо `new Const<T>(...)` для фиксированных значений: ноль аллокаций, а совпадение по ссылочной идентичности делает удаление из графа выражений надёжным. Важно для сборки под сервер: `Deg2Rad`/`Rad2Deg`/векторы/цвета пропадают вне Unity, а `PI`/`E`/`Zero`/`One`/`Gravity` доступны всегда.

### Variables: изменяемое состояние

`Scripts/Variables/Base/Variable.cs`:

```csharp
[Serializable]
public class Variable<T> : IVariable<T>
{
    public T Value
    {
        get => this.value;
        set => this.value = value;
    }

    [SerializeField]            // #if UNITY_5_3_OR_NEWER
    private T value;

    public T Invoke() => this.value;

    public Variable() => this.value = default;
    public Variable(T value) => this.value = value;

    public static implicit operator Variable<T>(T value) => new(value);
    public static implicit operator T(Variable<T> variable) => variable.value;

    public override string ToString() => this.value?.ToString();
}
```

Выбор по умолчанию, когда никто не наблюдает за изменением. Никакой проверки на равенство в сеттере — это самый дешёвый изменяемый контейнер. **Имя типа — `Variable<T>`**; `BaseVariable<T>` не существует, хотя файл документации до сих пор называется `BaseVariable.md`.

Монотипные `sealed`-варианты — `Scripts/Variables/Base/Common/` и `Base/Unity/`:

```csharp
[Serializable]
public sealed class BoolVariable : IVariable<bool>
{
    public bool Value { get => this.value; set => this.value = value; }

    [SerializeField]            // #if UNITY_5_3_OR_NEWER
    private bool value;

    public bool Invoke() => this.value;

    public BoolVariable() => this.value = false;
    public BoolVariable(bool value) => this.value = value;

    public static implicit operator BoolVariable(bool value) => new(value);

    public override string ToString() => this.value.ToString();
}
```

Набор: `BoolVariable`, `IntVariable`, `FloatVariable`; `Vector2Variable`, `Vector3Variable`, `Vector4Variable`, `Vector2IntVariable`, `Vector3IntVariable`, `QuaternionVariable` (Unity-файлы целиком в `#if UNITY_5_3_OR_NEWER`). Все `sealed`, все переопределяют `Invoke()`. Неявное преобразование только одностороннее (`bool -> BoolVariable`), обратного нет — в отличие от `Variable<T>`. `FloatVariable` отличается `ToString() => this.value.ToString(CultureInfo.InvariantCulture)`. `QuaternionVariable`, `Vector2IntVariable` и `Vector3IntVariable` отличаются безпараметрическим конструктором (`Quaternion.identity`, `Vector2Int.zero`, `Vector3Int.zero` вместо `default`) и несут `[ShowInInspector]` на `Value`.

`Scripts/Variables/Reactive/ReactiveVariable.cs`:

```csharp
[Serializable]
public class ReactiveVariable<T> : IReactiveVariable<T>, IDisposable
{
    private static readonly IEqualityComparer<T> s_equalityComparer = EqualityComparer.GetDefault<T>();

    public event Action<T> OnEvent;

    [HideLabel, OnValueChanged(nameof(InvokeEvent))]    // #if ODIN_INSPECTOR
    [SerializeField]                                    // #if UNITY_5_3_OR_NEWER
    private T value;

    public T Value
    {
        get => this.value;
        set
        {
            if (!s_equalityComparer.Equals(this.value, value))
            {
                this.value = value;
                this.OnEvent?.Invoke(value);
            }
        }
    }

    public ReactiveVariable() => this.value = default;
    public ReactiveVariable(T value) => this.value = value;

    public static implicit operator ReactiveVariable<T>(T value) => new(value);

    public T Invoke() => this.value;

    private void InvokeEvent(T value) => this.OnEvent?.Invoke(value);

    public void Dispose() => this.OnEvent = null;

    public override string ToString() => this.value?.ToString();
}
```

Обобщённый fallback для реактивного состояния. Компарер резолвится **один раз на закрытый дженерик-тип** в `static readonly` поле. `Dispose()` обнуляет список подписчиков и **не** сбрасывает значение.

Монотипные реактивные варианты — `Reactive/Common/` и `Reactive/Unity/`:

```csharp
[Serializable]
public class ReactiveBool : IReactiveVariable<bool>, IDisposable
{
    public event Action<bool> OnEvent;

    [HideLabel, OnValueChanged(nameof(InvokeEvent))]    // #if ODIN_INSPECTOR
    [SerializeField]                                    // #if UNITY_5_3_OR_NEWER
    private bool value;

    public bool Value
    {
        get => this.value;
        set
        {
            if (this.value != value)
            {
                this.value = value;
                this.OnEvent?.Invoke(value);
            }
        }
    }

    public ReactiveBool() => this.value = false;
    public ReactiveBool(bool value) => this.value = value;

    public static implicit operator ReactiveBool(bool value) => new(value);

    private void InvokeEvent(bool value) => this.OnEvent?.Invoke(value);
    public void Dispose() => this.OnEvent = null;
    public override string ToString() => this.Value.ToString();
}
```

Набор: `ReactiveBool`, `ReactiveInt`, `ReactiveFloat`; `ReactiveVector2/3/4`, `ReactiveVector2Int`, `ReactiveVector3Int`, `ReactiveQuaternion`. Сравнивают нативным `!=` вместо вызова через `IEqualityComparer<T>`. Отличия, которые нужно держать в голове:

- `ReactiveFloat` сравнивает `if (Math.Abs(this.value - value) > float.Epsilon)` и делает `ToString(CultureInfo.InvariantCulture)`;
- `ReactiveVector3` добавляет конструктор `(float x, float y, float z)`, а `ReactiveQuaternion` — `(float x, float y, float z, float w)`;
- ни один из них не `sealed` и ни один **не** переопределяет `Invoke()` — в отличие от `ReactiveVariable<T>`, который переопределяет.

### Variables: прокси-адаптеры

`Scripts/Variables/Proxy/Transform/TransformPositionVariable.cs` — эталонная форма адаптера, написанного руками:

```csharp
[Serializable]
public sealed class TransformPositionVariable : IVariable<Vector3>
{
    private readonly Transform _transform;

    public TransformPositionVariable(Transform transform)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));

        _transform = transform;
    }

    [ShowInInspector]           // #if ODIN_INSPECTOR
    public Vector3 Value
    {
        get => _transform.position;
        set => _transform.position = value;
    }

    public Vector3 Invoke() => _transform.position;
}
```

`TransformRotationVariable` имеет ту же форму и биндится на `_transform.rotation` (мировой, не `localRotation`), `TransformScaleVariable` — на `_transform.localScale`. А вот `TransformParentVariable` из этой четвёрки выпадает и **не является эталоном**:

```csharp
public class TransformParentVariable : IVariable<Transform>
{
    public Transform Value
    {
        get => _transform.parent;
        set => _transform.parent = value;
    }

    private readonly Transform _transform;

    public TransformParentVariable(Transform transform)
    {
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));

        _transform = transform;
    }
}
```

Не `sealed`, нет `[Serializable]`, нет `[ShowInInspector]`, нет переопределения `Invoke()` — падает в default-реализацию `IValue<T>.Invoke()`. Совпадает только проверка в конструкторе. И по семантике: присваивает `_transform.parent`, а это сохраняет мировой трансформ.

`Scripts/Variables/Proxy/PlayerPrefs/IntPrefsVariable.cs`:

```csharp
[Serializable]
public sealed class IntPrefsVariable : IVariable<int>
{
    [ShowInInspector]           // #if ODIN_INSPECTOR
    public int Value
    {
        get => PlayerPrefs.GetInt(this.key, this.defaultValue);
        set => PlayerPrefs.SetInt(this.key, value);
    }

    private readonly string key;
    private readonly int defaultValue;

    public IntPrefsVariable(string key, int defaultValue = default)
    {
        this.key = key;
        this.defaultValue = defaultValue;
    }

    public int Invoke() => this.Value;
}
```

Также `BoolPrefsVariable` (`PlayerPrefs.GetInt(key, default ? 1 : 0) != 0`), `FloatPrefsVariable`, `StringPrefsVariable`. Настройки, выставленные как обычный `IVariable<T>`, — UI настроек и игровая логика не нуждаются в особом коде. Ключ не валидируется, `PlayerPrefs.Save()` не вызывается, и `Invoke()` здесь идёт через `this.Value`, то есть нативного вызова не избегает.

`Scripts/Variables/Proxy/GameObject/GameObjectLayerVariable.cs` — **не копируйте его форму**:

```csharp
public sealed class GameObjectLayerVariable : IVariable<int>
{
    private readonly GameObject _gameObject;

    public GameObjectLayerVariable(GameObject gameObject)
    {
        _gameObject = gameObject;
    }

    public int Value
    {
        get => _gameObject.layer;
        set => _gameObject.layer = value;
    }
}
```

В документации отсутствует, нет `[Serializable]`, нет проверки в конструкторе, нет `Invoke()`, и файл **не** обёрнут в `#if UNITY_5_3_OR_NEWER` при наличии `using UnityEngine` — сломает не-Unity сборку `Atomic.Elements`.

### Variables: inline-адаптеры

`Scripts/Variables/Base/InlineVariable.cs`:

```csharp
[Serializable]
public class InlineVariable<T> : IVariable<T>
{
    [ShowInInspector]           // #if ODIN_INSPECTOR
    public T Value
    {
        get => this.getter.Invoke();
        set => this.setter.Invoke(value);
    }

    private readonly Func<T> getter;
    private readonly Action<T> setter;

    public InlineVariable(Func<T> getter, Action<T> setter)
    {
        this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
        this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
    }

    public override string ToString() => this.Value?.ToString();

    public static Builder StartBuild() => new();

    public struct Builder
    {
        private Func<T> _getter;
        private Action<T> _setter;

        public Builder WithGetter(Func<T> getter)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            return this;
        }

        public Builder WithSetter(Action<T> setter)
        {
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            return this;
        }

        public InlineVariable<T> Build()
        {
            if (_getter == null) throw new InvalidOperationException("Getter must be provided.");
            if (_setter == null) throw new InvalidOperationException("Setter must be provided.");
            return new InlineVariable<T>(_getter, _setter);
        }
    }
}
```

Адаптация существующего поля/свойства/чужого API в `IVariable<T>` **без дублирования состояния**. Переименован из `ProxyVariable<T>`.

`Scripts/Variables/Reactive/InlineReactiveVariable.cs`:

```csharp
public class InlineReactiveVariable<T> : IReactiveVariable<T>
{
    public event Action<T> OnEvent
    {
        add => this.subscribe.Invoke(value);
        remove => this.unsubscribe.Invoke(value);
    }

    [ShowInInspector]           // #if ODIN_INSPECTOR
    public T Value
    {
        get => this.getter.Invoke();
        set => this.setter?.Invoke(value);
    }

    private readonly Func<T> getter;
    private readonly Action<T> setter;
    private readonly Action<Action<T>> subscribe;
    private readonly Action<Action<T>> unsubscribe;

    public InlineReactiveVariable(
        Func<T> getter,
        Action<T> setter,
        Action<Action<T>> subscribe,
        Action<Action<T>> unsubscribe
    )
    {
        this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
        this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
        this.subscribe = subscribe ?? throw new ArgumentNullException(nameof(subscribe));
        this.unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
    }

    public static Builder StartBuild() => new();
    // Builder: WithGetter / WithSetter / WithSubscribe / WithUnsubscribe, каждый `return this;`,
    //          Build() бросает InvalidOperationException на любой отсутствующий делегат
}
```

Мост от чужого источника событий (сетевое состояние Photon/Mirror, legacy C#-event) к `IReactiveVariable<T>`. Конструктор **только четырёхаргументный** — документация показывает двухаргументный, он не скомпилируется. Не `[Serializable]`, в отличие от `InlineVariable<T>`.

Для «только наблюдать», без геттера и сеттера, есть `Scripts/Events/InlineSignal.cs`:

```csharp
[Serializable]
public class InlineSignal<T> : ISignal<T>
{
    public event Action<T> OnEvent
    {
        add => this.subscribe.Invoke(value);
        remove => this.unsubscribe.Invoke(value);
    }

    private readonly Action<Action<T>> subscribe;
    private readonly Action<Action<T>> unsubscribe;

    public InlineSignal(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe)
    {
        this.subscribe = subscribe ?? throw new ArgumentNullException(nameof(subscribe));
        this.unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
    }
}
```

Есть необобщённый `InlineSignal` и варианты на 2–4 аргумента. Два делегата вместо четырёх — берите его, когда нужно выставить чужое событие как `ISignal<T>` и не нужно подделывать геттер/сеттер.

### Variables: потокобезопасные

`Scripts/Variables/Base/ThreadSafeVariable.cs`:

```csharp
public class ThreadSafeVariable<T> : IVariable<T>
{
    private static readonly IEqualityComparer<T> s_comparer = EqualityComparer<T>.Default;
    private readonly object _lock = new();

    private T _value;

    public T Value
    {
        get
        {
            lock (_lock)
                return _value;
        }
        set
        {
            lock (_lock)
            {
                if (s_comparer.Equals(_value, value))
                    return;

                _value = value;
            }
        }
    }

    public ThreadSafeVariable() => _value = default;
    public ThreadSafeVariable(T value) => _value = value;

    public override string ToString()
    {
        lock (_lock)
            return _value?.ToString();
    }
}
```

Состояние, которое трогают фоновые потоки (jobs, приём по сети, асинхронная загрузка). Не `[Serializable]`, без неявных операторов, без `Invoke()`, не реализует `IDisposable`, и использует `EqualityComparer<T>.Default`, а не `EqualityComparer.GetDefault<T>()` Atomic'а.

`Scripts/Variables/Reactive/ThreadSafeReactiveVariable.cs`:

```csharp
public class ThreadSafeReactiveVariable<T> :
    IReactiveVariable<T>,
    IDisposable,
    MainThreadDispatcher.IFlushable
{
    private static readonly IEqualityComparer<T> s_comparer = EqualityComparer<T>.Default;
    private readonly object _lock = new();

    public event Action<T> OnEvent;

    private T _value;

    public T Value
    {
        get
        {
            lock (_lock)
            {
                return _value;
            }
        }
        set
        {
            bool changed = false;

            lock (_lock)
            {
                if (s_comparer.Equals(_value, value))
                    return;

                _value = value;
                changed = true;
            }

            if (changed)
                MainThreadDispatcher.MarkDirty(this);
        }
    }

    void MainThreadDispatcher.IFlushable.Flush()
    {
        T value;
        lock (_lock)
        {
            value = _value;
        }

        Action<T> handler = this.OnEvent;
        handler?.Invoke(value);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref OnEvent, null);
    }
}
```

Правильный примитив для «фоновый поток меняет состояние, UI реагирует». `Flush()` — явная реализация интерфейса, снаружи недостижима, и это хорошо. Нюанс для архитектуры: `MainThreadDispatcher.IFlushable` — вложенный тип `internal sealed class`, поэтому реализовать `IFlushable` своим типом вне сборки невозможно.

### Setters

`Scripts/Setters/InlineSetter.cs`:

```csharp
[Serializable]
public class InlineSetter<T> : ISetter<T>
{
    [ShowInInspector]           // #if ODIN_INSPECTOR
    public T Value
    {
        set => this.action?.Invoke(value);
    }

    private readonly Action<T> action;

    public InlineSetter(Action<T> action) => this.action = action ?? throw new ArgumentNullException(nameof(action));

    public static implicit operator InlineSetter<T>(Action<T> value) => new(value);

    public override string ToString() => this.action.Method.Name;

#if UNITY_EDITOR
    [Button("Set Value")]       // #if ODIN_INSPECTOR
    private void Editor_SetValue(T value) => this.action?.Invoke(value);
#endif
}
```

Обернуть `Action<T>` как `ISetter<T>`. `ToString()` возвращает имя метода делегата — для лямбды это `<Install>b__0_1`, полезно в инспекторе, бесполезно в логах. Паттерн `#if UNITY_EDITOR` + `[Button]` — идиоматичный способ Atomic выставить редакторную «кнопку-дёрни» и не тащить её в билд.

### Utils

`Scripts/Utils/Reference.cs`:

```csharp
[Serializable]
public class Reference<T>
{
    public ref T Value => ref this.value;

    [HideLabel]                 // #if ODIN_INSPECTOR
    [SerializeField]            // #if UNITY_5_3_OR_NEWER
    private T value;

    public Reference(T value = default) => this.value = value;

    public static implicit operator Reference<T>(T value) => new Reference<T>(value);
}
```

Единственный член во всём `Elements`, возвращающий `ref`. Инструмент для (а) мутации большой структуры на месте без копирования, (б) разделяемой изменяемой ячейки, (в) замены `out`-параметра в корутинах и `async`-методах, где `out`/`ref` запрещены. Сознательно **не** реализует `IValue<T>`/`IVariable<T>` — те возвращают по значению.

`Scripts/Utils/Optional.cs`:

```csharp
[Serializable]
public struct Optional<T>
{
    [SerializeField] private bool active;                   // [FoldoutGroup(""), HideLabel]
    [SerializeField] private T value;                       // [ShowIf(nameof(active)), HideLabel]

    public T Value
    {
        get => value;
        set
        {
            this.value = value;
            this.active = value != null;
        }
    }

    public bool Active
    {
        get => this.active;
        set => this.active = value;
    }

    public bool TryGetValue(out T value)
    {
        value = this.value;
        return this.active;
    }

    public static implicit operator Optional<T>(T it) => new() {value = it, active = it != null};
    public static implicit operator T(Optional<T> it) => it.value;

    public static bool operator true(Optional<T> it) => it.active;
    public static bool operator false(Optional<T> it) => !it.active;

    public T GetValueOrDefault(T defaultValue) => this.active ? this.value : defaultValue;
}
```

Сериализуемая пара «фича включена + её payload». Пара `operator true`/`operator false` — то, из-за чего легален голый `if (_vfx)`.

`Scripts/Utils/DisposableAction.cs` и `Scripts/Events/Subscription.cs`:

```csharp
public readonly struct DisposableAction : IDisposable
{
    private readonly Action _action;

    public DisposableAction(Action action) => _action = action ?? throw new ArgumentNullException(nameof(action));

    public void Dispose() => _action.Invoke();
}

public readonly struct Subscription<T> : IDisposable
{
    private readonly ISignal<T> signal;
    private readonly Action<T> action;

    public Subscription(ISignal<T> signal, Action<T> action)
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
// то же для Subscription (над ISignal), Subscription<T1,T2>, <T1,T2,T3>, <T1,T2,T3,T4>
```

`Subscription*` — токен отписки. Подписка происходит **в конструкторе**; `Dispose()` идемпотентен и безопасен на `default`. `DisposableAction` — наоборот: `Dispose()` вызывает действие **каждый раз**, а `default(DisposableAction).Dispose()` бросит `NullReferenceException`, обойдя проверку конструктора.

``Scripts/Utils/DisposableComposite`1.cs``:

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
```

Ограничение `where T : IDisposable` — это то, что покупает отсутствие боксинга. Необобщённая форма — пустое наследование:

```csharp
public class DisposableComposite : DisposableComposite<IDisposable>
{
}
```

Пустое тело значит, что конструкторы базы **не наследуются**: у `DisposableComposite` есть только неявный безпараметрический. `new DisposableComposite(d1, d2)` не скомпилируется, хотя документация утверждает обратное. И `Add` унаследован, то есть возвращает `DisposableComposite<IDisposable>`, а не `DisposableComposite`.

`Scripts/Utils/AtomicUtils.cs`:

```csharp
public static class AtomicUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPlayMode()
    {
#if UNITY_EDITOR
        return EditorApplication.isPlaying;
#else
        return true;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEditMode()
    {
#if UNITY_EDITOR
        return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
#else
        return false;
#endif
    }
}
```

Один инсталлер, корректно работающий и в edit-mode-превью, и в рантайме. `IsEditMode()` дополнительно исключает окно компиляции — поэтому из него безопасно трогать ассеты. Это **не** взаимодополняющие предикаты: во время domain reload оба вернут `false`.

`Scripts/Utils/IRandomizer.cs`:

```csharp
public interface IRandomizer
{
    int Range(int from, int to);

    float Range(float from, float to);
}

public sealed class UnityRandomizer : IRandomizer
{
    public static readonly UnityRandomizer Instance = new();

    public int Range(int from, int to) => Random.Range(from, to);       // using Random = UnityEngine.Random;

    public float Range(float from, float to) => Random.Range(from, to);
}
```

Инъектируемая случайность. В документации отсутствует. Зависьте от `IRandomizer`, передавайте `UnityRandomizer.Instance` (предаллоцированный синглтон, не `new`). Реализации вне Unity нет: `DefaultRandomizer` на `ThreadLocal<System.Random>` в файле закомментирован — детерминированный/seeded рандомайзер придётся писать самим.

### Extensions

`Extensions` — один `partial static class Atomic.Elements.Extensions`, физически разложенный по `Values/`, `Variables/`, `Utils/`, `Events/`.

`Scripts/Values/Extensions.cs`:

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Const<T> AsСonst<T>(this T it) => new(it);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Subscription<T> Observe<T>(this IReactiveValue<T> it, Action<T> action)
    {
        action.Invoke(it.Value);
        return new Subscription<T>(it, action);
    }
}
```

`Observe` — канонический «подписаться и сразу проинициализировать». Имя первого метода содержит кириллическую С (см. «Грабли»).

`Scripts/Variables/Extensions.cs`:

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Variable<T> AsVariable<T>(this T it) => new(it);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReactiveVariable<T> AsReactiveVariable<T>(this T it) => new(it);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InlineVariable<R> AsInlineVariable<T, R>(this T it, Func<T, R> getter, Action<T, R> setter) =>
        new(() => getter.Invoke(it), value => setter.Invoke(it, value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyTo(this IValue it, ISetter target) => target.Value = it.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyTo<T>(this IValue<T> it, ISetter<T> target) => target.Value = it.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyFrom(this ISetter it, IValue target) => it.Value = target.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyFrom<T>(this ISetter<T> it, IValue<T> target) => it.Value = target.Value;
}
```

`CopyTo`/`CopyFrom` в документации отсутствуют, но полезны: перекачка значения из любого геттера в любой сеттер без промежуточного типа. Берите **только обобщённые** перегрузки.

`Scripts/Utils/Extensions.cs`:

```csharp
public static partial class Extensions
{
    public static void AddTo(this IDisposable it, DisposableComposite composite) =>
        composite.Add(it);

    public static void AddTo<T>(this T it, DisposableComposite<T> composite) where T : IDisposable =>
        composite.Add(it);
}
```

Обобщённый `AddTo<T>` сохраняет структуру неупакованной. Оба возвращают `void` — `AddTo` завершает цепочку. Ни один из них не помечен `AggressiveInlining`, в отличие от всего остального в `Extensions`.

`Scripts/Events/Extensions.cs` — полная матрица арностей 0..4:

```csharp
public static Subscription    Subscribe(this ISignal it, Action action) => new(it, action);
public static Subscription<T> Subscribe<T>(this ISignal<T> it, Action<T> action) => new(it, action);
public static Subscription<T1, T2> Subscribe<T1, T2>(this ISignal<T1, T2> it, Action<T1, T2> action) => new(it, action);
// ... <T1,T2,T3>, <T1,T2,T3,T4>

public static Subscription    Subscribe(this ISignal it, IAction action) => new(it, action.Invoke);
public static Subscription<T> Subscribe<T>(this ISignal<T> it, IAction<T> action) => new(it, action.Invoke);
// ... IAction-формы для арностей 2..4

public static void Unsubscribe(this ISignal it, Action action) => it.OnEvent -= action;
public static void Unsubscribe<T>(this ISignal<T> it, Action<T> action) => it.OnEvent -= action;
public static void Unsubscribe(this ISignal it, IAction action) => it.OnEvent -= action.Invoke;
// ... всего десять Unsubscribe

public static void SubscribeRange(this ISignal it, IEnumerable<IAction> actions)
{
    if (actions != null)
        foreach (IAction action in actions)
            if (action != null)
                it.OnEvent += action.Invoke;
}
// SubscribeRange<T>, <T1,T2>, <T1,T2,T3>, <T1,T2,T3,T4> и пять UnsubscribeRange той же формы
```

`SubscribeRange` — штатный способ подключить сериализованный список `IAction`-хендлеров к одному сигналу одним вызовом. Он **не возвращает** `Subscription`, а `UnsubscribeRange` строит новый делегат `action.Invoke` на каждый элемент и поэтому **не удаляет ничего**. Применяйте `*Range` только к сигналам, которые вы уничтожаете целиком. `Subscribe`/`Unsubscribe` и все `SubscribeRange` помечены `AggressiveInlining`; `UnsubscribeRange` — нет.

### Внутреннее (знать, но не вызывать)

- `Scripts/Utils/EqualityComparer.cs` — `internal static class EqualityComparer` с комментарием `//Steal from UniRx! ;)`. Фабрика компареров за `ReactiveVariable<T>`. `public static IEqualityComparer<T> GetDefault<T>() => Cache<T>.Comparer;` — **internal**, из игрового кода недоступен.
- `Scripts/Utils/MainThreadDispatcher.cs` — `internal sealed class MainThreadDispatcher : MonoBehaviour`. Насос, маршалящий оповещения `ThreadSafeReactiveVariable<T>` на главный поток. Самобутстрапится в `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` в `DontDestroyOnLoad`-объект с именем `[MainThreadDispatcher]` — ожидайте его в каждой сцене, это не баг.
- `Scripts/Utils/InternalUtils.cs` — `CeilToPrime(int, out int)` по статической таблице из 31 простого и `Array.BinarySearch`; политика роста бакетов реактивных коллекций. Плюс `Contains<T>(IEnumerable<T>, T, IEqualityComparer<T>)` с `AggressiveInlining` и явным компарером.

---

## Идиомы

### 1. Зависеть от атомарного интерфейса, а не от конкретного класса

```csharp
// ❌ Неправильно
ReactiveVariable<Vector3> position = ...;
ReactiveVariable<Vector3> moveDirection = ...;
Const<float> speed = ...;

// ✅ Правильно
IVariable<Vector3> position = ...;
IValue<Vector3> moveDirection = ...;
IValue<float> speed = ...;
```

Правило №1 фреймворка (`Docs/BestPractices/PreferAbstractInterfaces.md`) и главный водораздел между идиоматичным Atomic и «ООП с реактивным привкусом». Форма правильная, потому что объявленный тип сообщает ровно то, что потребителю разрешено: (а) реализацию можно подменить — та же система движения работает на `ReactiveVector3` в синглплеере и на `InlineReactiveVariable<Vector3>` поверх Photon Fusion/Mirror в мультиплеере, без правок игровой логики; (б) тест-дубль пишется в одну строку; (в) логика не знает про Unity. Объявив конкретный тип, вы протаскиваете решения о хранении и реактивности в каждого потребителя.

### 2. Сужать интерфейс до фактически используемой способности

```csharp
public sealed class MoveController
{
    private readonly ISetter<Vector3> _moveDirection;

    public MoveController(ISetter<Vector3> moveDirection)
    {
        _moveDirection = moveDirection;
    }

    public void Update()
    {
        float dx = Input.GetAxis("Horizontal");
        float dz = Input.GetAxis("Vertical");
        _moveDirection.Value = new Vector3(dx, 0, dz);
    }
}
```

Контроллер — чистый продюсер, поэтому просит `ISetter<Vector3>` и ничего больше. Форма правильная, потому что подпись теперь документирует направление потока данных, а чтение становится физически невозможным — обработка ввода не сможет втихую начать зависеть от текущего состояния. `readonly`-поле, инъекция через конструктор, никакого MonoBehaviour, ни одного Unity-типа в собственном состоянии класса.

### 3. Хранить структуру `Subscription`, возвращённую `Observe`

```csharp
public sealed class ScorePresenter : IDisposable
{
    private readonly IReactiveValue<int> _score;
    private readonly Text _scoreText;
    private readonly Subscription<int> _subscription;   // struct

    public ScorePresenter(IReactiveValue<int> score, Text scoreText)
    {
        _score = score;
        _scoreText = scoreText;

        // подписка + немедленная инициализация UI текущим значением
        _subscription = _score.Observe(this.OnScoreChanged);
    }

    private void OnScoreChanged(int score)
    {
        _scoreText.text = "Score: " + score;
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
```

Рекомендованная форма из `Docs/BestPractices/UsingObserveWithReactiveValues.md`. Правильная она потому, что `Observe` склеивает два шага — прайминг текущим значением и подписку — и тем убивает классический баг «вью пустой до первого изменения». Хендл — поле типа `readonly struct`: ноль аллокаций, и `Dispose()` не может «забыть», какой делегат снимать.

Антипаттерн (вариант из `Values/IReactiveValue.md` — то же поведение, больше способов ошибиться):

```csharp
// ❌
_score.Subscribe(this.OnScoreChanged);
this.OnScoreChanged(_score.Value);          // прайминг вручную, легко забыть
// ...
_score.Unsubscribe(this.OnScoreChanged);    // method group создаёт новый делегат —
                                            // должен совпасть в точности, иначе не снимется ничего
```

### 4. `AddTo` в `DisposableComposite` для групповой разборки

```csharp
public sealed class WeaponViewInstaller : MonoEntityInstaller
{
    [SerializeField] private ParticleSystem _fireVFX;
    [SerializeField] private AudioSource _fireSFX;
    [SerializeField] private Animator _animator;

    // generic-форма: Subscription — структура, боксинга нет
    private readonly DisposableComposite<Subscription> _disposables = new();

    public override void Install(IEntity entity)
    {
        ISignal fireEvent = entity.GetFireEvent();

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

Каждая подписка объявляется и регистрируется на уборку **в одной строке**: нет второго списка, который надо держать в синхроне, и нет способа добавить подписку, забыв её снятие. Класс утечек исчезает структурно, а не за счёт дисциплины. `_fireVFX.Play` передан как method group, а не лямбда — замыкания нет. Один `Dispose()` в `Uninstall` отпускает всё, и композит снова пригоден к использованию (`Dispose` чистит список), поэтому цикл install/uninstall работает.

Две детали, в которых документация врёт и которые здесь исправлены: `Uninstall` в `MonoEntityInstaller` объявлен как `public virtual void Uninstall(IEntity entity)` — **с параметром**; и composite взят обобщённый (`DisposableComposite<Subscription>`), потому что необобщённый `DisposableComposite` — это `DisposableComposite<IDisposable>` и боксирует каждую структурную подписку.

Антипаттерн — параллельный учёт:

```csharp
// ❌ два места для правки, гарантированно разъедутся
private Action _vfxHandler, _sfxHandler, _animHandler;

public override void Install(IEntity entity)
{
    ISignal e = entity.GetFireEvent();
    e.OnEvent += _vfxHandler = _fireVFX.Play;
    // ...
}

public override void Uninstall(IEntity entity)
{
    // вспомнить все три, в нужном порядке, с нужными делегатами
}
```

### 5. `Optional<T>` вместо nullable-полей

```csharp
public sealed class WeaponView : MonoBehaviour
{
    [SerializeField] private Weapon _weapon;

    [SerializeField] private Optional<ParticleSystem> _vfx;
    [SerializeField] private Optional<AudioSource> _sfx;
    [SerializeField] private Optional<Animation> _animation;

    private void OnEnable()  => _weapon.OnFire += this.OnFire;
    private void OnDisable() => _weapon.OnFire -= this.OnFire;

    private void OnFire()
    {
        if (_vfx) _vfx.Value.Play();
        if (_sfx) _sfx.Value.Play();
        if (_animation) _animation.Value.Play();
    }
}
```

Форма правильная потому, что `Optional<T>` — структура `(bool active, T value)` с `[ShowIf(nameof(active))]`, то есть в инспекторе это чекбокс, раскрывающий поле только при включении. Дизайнер включает и выключает фичу, и пустой слот больше не значит двух разных вещей («не подключено» против «сознательно выключено»). Пара `operator true`/`operator false` делает `if (_vfx)` законным, а структура не аллоцирует. Симметричные `OnEnable`/`OnDisable` — вторая половина паттерна.

Замечание к коду: документация в этом месте пишет `if (_vfx) _vfx.Play();`, опираясь на неявный `operator T`. Это компилируется, но здесь явный `.Value` честнее: `operator T` **не проверяет** `Active`, и если привыкнуть к краткой форме, то в другом месте, без `if`, получите `default(T)` без всякого предупреждения.

Антипаттерн:

```csharp
// ❌ null — это «выключено» или «забыли назначить»?
[SerializeField] private ParticleSystem _vfx;
// ...
if (_vfx != null) _vfx.Play();
```

### 6. `Optional<T>` для переопределения общего ScriptableObject-инсталлера

```csharp
// Общий инсталлер-архетип для многих врагов
public sealed class EnemyInstaller : ScriptableEntityInstaller<IEnemyEntity>
{
    [SerializeField] private ReactiveInt _health;
    [SerializeField] private ReactiveInt _damage;
    [SerializeField] private ReactiveFloat _speed;

    public override void Install(IEnemyEntity entity)
    {
        entity.AddHealth(_health);
        entity.AddDamage(_damage);
        entity.AddSpeed(_speed);
    }
}

// MonoBehaviour-инсталлер для конкретного экземпляра на сцене
public sealed class OverrideEnemyInstaller : MonoEntityInstaller<IEnemyEntity>
{
    [SerializeField] private Optional<ReactiveInt> _health;
    [SerializeField] private Optional<ReactiveInt> _damage;
    [SerializeField] private Optional<ReactiveFloat> _speed;

    public override void Install(IEnemyEntity entity)
    {
        if (_health) entity.SetHealth(_health.Value);
        if (_damage) entity.SetDamage(_damage.Value);
        if (_speed)  entity.SetSpeed(_speed.Value);
    }
}
```

Двухслойный конфиг: один общий ScriptableObject даёт дефолты архетипа обычными полями, а сценовый `MonoEntityInstaller` с `Optional<T>`-полями переопределяет только те статы, которые дизайнер реально отметил. `Add*` в базовом слое, `Set*` в слое переопределения — это ещё и кодирует требование к порядку. Уходит обычный хак с сентинелами («-1 значит наследовать»), и у каждого стата остаётся один однозначный источник. Подписи `Install` в примере приведены к реальным: `MonoEntityInstaller<E>`/`ScriptableEntityInstaller<E>` объявляют `public abstract void Install(E entity)`, то есть в подклассе нужен `override`.

### 7. Flyweight: разделять один `Const<T>`, а не копировать значение

```csharp
// Общий конфиг
public sealed class CharacterConfig : ScriptableObject
{
    [SerializeField]
    public Const<float> moveSpeed = 5.0f;
}

// Много экземпляров
public sealed class Character : MonoBehaviour
{
    [SerializeField]
    private CharacterConfig _config;

    public void MoveStep(Vector3 direction, float deltaTime)
    {
        this.transform.position += direction * (_config.moveSpeed.Value * deltaTime);
    }
}
```

`Const<T>` — ссылочный тип с неизменяемым payload, поэтому тысяча персонажей держит указатель на одну коробку, а не свою копию, и значение задаётся только через сериализацию — в рантайме его ничто не мутирует. Поскольку он реализует `IValue<float>`, то же поле удовлетворяет системам, типизированным против `IValue<T>`: общие константы и персональные реактивные статы взаимозаменяемы на стороне потребления. `= 5.0f` работает через неявный `T -> Const<T>`.

### 8. `ScriptableConst<T>`: закрытый подкласс на каждый `T`

```csharp
[CreateAssetMenu(
    fileName = "FloatConst",
    menuName = "Game/Elements/FloatConst"
)]
public sealed class FloatScriptableConst : ScriptableConst<float>
{
}

public sealed class Character : MonoBehaviour
{
    [SerializeField]
    private FloatScriptableConst _moveSpeed;

    public void MoveStep(Vector3 direction, float deltaTime)
    {
        this.transform.position += direction * (_moveSpeed.Value * deltaTime);
    }
}
```

Unity не сериализует открытый дженерик-ScriptableObject, поэтому однострочный `sealed`-подкласс с `[CreateAssetMenu]` — обязательный бойлерплейт, а не выбор: `ScriptableConst<T>` объявлен `abstract` именно чтобы это навязать. Выигрыш — константа уровня ассета: правите один раз, и все сцены и префабы подхватывают без рекомпиляции и переавторинга. `sealed` оставляет возможность девиртуализации. В документации в этом месте вызывают `.Invoke()`; это то же самое (`Invoke()` просто возвращает поле), но `.Value` читается лучше в выражениях.

### 9. Рукописный `sealed`-адаптер вместо `InlineVariable` в горячем пути

```csharp
public sealed class RigidbodyVelocityVariable : IVariable<Vector3>
{
    private readonly Rigidbody _rigidbody;

    public RigidbodyVelocityVariable(Rigidbody rigidbody)
    {
        if (rigidbody == null)
            throw new ArgumentNullException(nameof(rigidbody));

        _rigidbody = rigidbody;
    }

    public Vector3 Value
    {
        get => _rigidbody.velocity;
        set => _rigidbody.velocity = value;
    }

    public Vector3 Invoke() => _rigidbody.velocity;
}
```

Это форма `TransformPositionVariable`, перенесённая на свой тип: одно `readonly`-поле, проверка в конструкторе с `nameof`, свойство форвардит прямо в источник **без хранения копии** — адаптер физически не может устареть, и переопределённый `Invoke()` минует хоп через default-реализацию интерфейса. Сравните с маршрутом через `InlineVariable<Vector3>`: та же работа, но замыкание плюс два делегата плюс два косвенных вызова на каждое обращение. Пишите класс для всего, что трогается каждый кадр. Для позиции, поворота, масштаба и родителя трансформа писать ничего не нужно — готовые адаптеры уже есть в `Variables/Proxy/Transform/`.

### 10. `InlineVariable<T>` для разовой проводки и тест-дублей

```csharp
// Прокси над Transform.position
IVariable<Vector3> position = new InlineVariable<Vector3>(
    getter: () => transform.position,
    setter: value => transform.position = value
);

position.Value += Vector3.forward;

// либо через fluent-билдер
IVariable<Vector3> position2 = InlineVariable<Vector3>
    .StartBuild()
    .WithGetter(() => transform.position)
    .WithSetter(value => transform.position = value)
    .Build();
```

Именованные аргументы (`getter:`, `setter:`) — то, что делает двухлямбдовый конструктор читаемым: без них место вызова выглядит как два неразличимых делегата. Результат объявлен как `IVariable<Vector3>`, поэтому ниже по стеку никто не знает, что это прокси. Состояние не дублируется: лямбды читают и пишут реальный `Transform`, так что `position.Value += Vector3.forward` действительно двигает объект. Вариант с билдером — zero-alloc цепочка по структуре, которая меняет краткость на гарантированный `InvalidOperationException` при отсутствующем хендлере; берите его, когда хендлеры собираются условно, и обычный конструктор, когда оба известны.

### 11. `InlineReactiveVariable<T>` как мост к чужому источнику событий

```csharp
// Реальное имя типа — InlineReactiveVariable<T>; документация зовёт его ReactiveInlineVariable<T>
IReactiveVariable<int> networkedHealth = InlineReactiveVariable<int>
    .StartBuild()
    .WithGetter(() => _netObj.Health)
    .WithSetter(value => _netObj.Health = value)
    .WithSubscribe(handler => _netObj.HealthChanged += handler)
    .WithUnsubscribe(handler => _netObj.HealthChanged -= handler)
    .Build();
```

Это шов, который держит игровую логику транспорт-независимой: сетевое свойство (Photon Fusion 2, Mirror) или любой legacy C#-event оборачивается в `IReactiveVariable<T>`, и каждая система, уже умеющая работать с `IReactiveVariable<T>`, работает без правок и в синглплеере, и в мультиплеере. Все четыре хендлера обязательны и `Build()` бросает исключение при отсутствии любого — полусобранный реактивный прокси не доедет до рантайма.

Антипаттерн — транспорт протекает в игровую логику:

```csharp
// ❌
void Tick()
{
    if (_netObj.Health != _cachedHealth)
    {
        _cachedHealth = _netObj.Health;
        _healthBar.Set(_cachedHealth);
    }
}
```

### 12. Предпочитать монотипные варианты обобщённым

```csharp
// Вместо Variable<int> / ReactiveVariable<float> / ReactiveVariable<Vector3>:
IVariable<bool>            flag  = new BoolVariable(true);
IVariable<int>             ammo  = new IntVariable(30);
IReactiveVariable<float>   speed = new ReactiveFloat(5f);
IReactiveVariable<Vector3> dir   = new ReactiveVector3(Vector3.forward);
```

Документация говорит это прямо для обоих семейств (`Docs/Elements/Variables/Manual.md`): «It is recommended to use them, as they compare values without relying on `EqualityComparer`, which makes them slightly faster than the generic version». Специализированные реактивные сравнивают нативным `!=`, тогда как `ReactiveVariable<T>` идёт через интерфейсный вызов `IEqualityComparer<T>` (кешированный статически, но всё равно виртуальный). Нереактивные специализации вдобавок `sealed`. Обобщённые формы оставьте для типов без специализации.

Антипаттерн и ловушка семантики:

```csharp
// ❌ интерфейсный компарер на каждой записи, при наличии ReactiveFloat
IReactiveVariable<float> speed = new ReactiveVariable<float>(5f);

// ⚠️ это НЕ взаимозаменяемые типы: разная детекция изменения
IReactiveVariable<Vector3> a = new ReactiveVector3(Vector3.zero);          // Unity !=, допуск ~1e-5
IReactiveVariable<Vector3> b = new ReactiveVariable<Vector3>(Vector3.zero); // покомпонентный float.Equals
```

### 13. `Reference<T>` для мутации структуры на месте

```csharp
var health = new Reference<int>(100);

ref int healthRef = ref health.Value;
healthRef += 50;

Debug.Log(health.Value);   // 150
```

`public ref T Value => ref this.value` возвращает настоящую managed-ссылку, поэтому `healthRef += 50` пишет **сквозь** в коробку, без чтения-модификации-записи копии. Это единственное место в `Elements`, где большую структуру можно обновлять поле за полем без копирования. Это же — штатный обход запрета C# на `out`/`ref` в итераторах и `async`: передайте `Reference<T>` в корутину, и вызывающий увидит результат после завершения.

Антипаттерн — ловушка value-семантики:

```csharp
// ❌ мутирует копию, оригинал не меняется
int healthCopy = health.Value;
healthCopy += 50;
```

### 14. `Reference<T>` как разделяемая изменяемая ячейка

```csharp
public class Player
{
    private readonly Reference<int> _scoreRef;

    public Player(Reference<int> sharedScore)
    {
        _scoreRef = sharedScore;
    }

    public void AddScore(int amount)
    {
        ref int score = ref _scoreRef.Value;
        score += amount;
    }
}

public class Example : MonoBehaviour
{
    private readonly Reference<int> sharedScore = 0;

    private void Awake()
    {
        var player1 = new Player(sharedScore);
        var player2 = new Player(sharedScore);

        player1.AddScore(5);
        player2.AddScore(10);

        Debug.Log($"Shared score: {sharedScore.Value}");   // 15
    }
}
```

Одна коробка, много держателей, без статики и без синглтона: счёт команды живёт в `Reference<int>`, инъектированном в каждого `Player`, поэтому владение видно в подписи конструктора, а время жизни — это время жизни владельца. `private readonly Reference<int> sharedScore = 0;` показывает неявный `T -> Reference<T>`, который держит объявление коротким. Честный компромисс: это разделяемая мутация **без** оповещения об изменении — как только что-то должно наблюдать за значением, берите `ReactiveInt`.

### 15. Прикрывать рантайм-часть инсталлера `AtomicUtils.IsPlayMode()`

```csharp
[Serializable]
public sealed class CharacterSystemInstaller : IEntityInstaller<IPlayerContext>
{
    [SerializeField]
    private GameEntity _characterPrefab;

    public void Install(IPlayerContext context)
    {
        if (AtomicUtils.IsPlayMode())
        {
            GameContext gameContext = GameContext.Instance;
            GameEntity character = CharacterUseCase.Spawn(context, gameContext, _characterPrefab);
            context.AddCharacter(character);
            gameContext.WhenDisable(character.Disable);
        }

        context.AddBehaviour<CharacterMoveController>();
        context.AddBehaviour<CharacterFireController>();
        context.AddBehaviour<CharacterRespawnController>();
    }
}
```

Один инсталлер обслуживает и edit-mode-превью, и рантайм: часть с побочными эффектами (инстанцирование префабов, регистрация disable-колбэков) сидит внутри гарда, а декларативная проводка выполняется безусловно, поэтому инспектор по-прежнему показывает полностью сконфигурированную сущность. Гард компилируется в константу `true` в плеерной сборке и помечен `AggressiveInlining`, то есть в шипнутом билде он бесплатен. (В оригинале примера из `Docs/BestPractices/UsingUtilsForEntityInstallers.md` `CharacterMoveController` добавлен дважды — копипаста; здесь исправлено.)

### 16. Разделяемые allocation-free булевы в графах выражений

```csharp
IExpression<bool> condition = new AndExpression(cond1, cond2, cond3);

// Полностью выключить AND-условие
condition.Add(Constants.False);

// Снять блокировку
condition.Remove(Constants.False);
```

`Constants.False` — один предаллоцированный `Const<bool>` в `static readonly` поле, поэтому постановка и снятие жёсткой блокировки на граф условий стоят ноль аллокаций и ноль давления на GC — важно, потому что такие переключатели в геймплее происходят постоянно. Удаление по тому же разделяемому экземпляру работает благодаря ссылочной идентичности; свежесозданный `new Const<bool>(false)` так удалить не получилось бы. (`IExpression<bool>`/`AndExpression` — из области `Docs/Elements/Expressions`, за пределами этой главы; здесь показано только как употребляется `Constants`.)

### 17. Фасад-компонент вместо переопределения Unity-колбэка

```csharp
public class Example : MonoBehaviour
{
    [SerializeField]
    private TriggerEvents _triggerEvents;

    private void OnEnable()
    {
        _triggerEvents.OnEntered += HandleEnter;
        _triggerEvents.OnExited  += HandleExit;
        _triggerEvents.OnStay    += HandleStay;
    }

    private void OnDisable()
    {
        _triggerEvents.OnEntered -= HandleEnter;
        _triggerEvents.OnExited  -= HandleExit;
        _triggerEvents.OnStay    -= HandleStay;
    }

    private void HandleEnter(Collider other) => Debug.Log($"Trigger entered by {other.gameObject.name}");
    private void HandleExit(Collider other)  => Debug.Log($"Trigger exited by {other.gameObject.name}");
    private void HandleStay(Collider other)  => Debug.Log($"Still inside trigger: {other.gameObject.name}");
}
```

`OnTriggerEnter` у Unity — магический метод: им владеет ровно один скрипт на объекте, на него нельзя подписаться из обычного C#-класса, и он заставляет каждого реагирующего быть MonoBehaviour именно на этом объекте. Фасад превращает его в `event Action<Collider>`, поэтому слушателей может быть сколько угодно, включая не-MonoBehaviour-системы, а физическая проводка становится сериализованным полем, а не обязательством наследования. Строгое зеркало `OnEnable`/`OnDisable` — тот контракт жизненного цикла, который держит это без утечек.

Вторая половина пользы — компонент сам ведёт учёт контактов, и переподписываться ради этого не нужно:

```csharp
// Что мы прямо сейчас перекрываем — спрашивать у компонента, а не вести свой HashSet
foreach (Collider c in _triggerEvents.CurrentColliders)
    ApplyDamage(c);
```

`CurrentColliders` (`CurrentCollisions` у `CollisionEvents`) — это `IReadOnlyCollection<>` над внутренним `HashSet<>`. Enter/exit **дедуплицированы** через этот же набор (`if (_currentColliders.Add(other)) OnEntered?.Invoke(other);`), а `OnDisable()` проигрывает `OnExited` для всех ещё отслеживаемых контактов и чистит набор — пулящийся или выключенный объект не может оставить фантомное перекрытие. Рефакторинг, который подписывается только на `OnEntered`/`OnExited` и ведёт свой набор, переписывает состояние, которым компонент уже владеет. `OnStay` — единственный, не прикрытый набором.

### 18. Строковые ключи анимационных событий вместо магических методов

```csharp
public class Example : MonoBehaviour
{
    [SerializeField]
    private AnimationEvents _animEvents;

    private void OnEnable()
    {
        _animEvents.Subscribe("Hello", OnHello);
        _animEvents.OnEvent += OnAnimationEvent;
    }

    private void OnDisable()
    {
        _animEvents.Unsubscribe("Hello", OnHello);
        _animEvents.OnEvent -= OnAnimationEvent;
    }

    private void OnHello() => Debug.Log("Hello!");

    private void OnAnimationEvent(string evt) => Debug.Log($"Event triggered: {evt}");
}
```

Обычно AnimationEvent обязан называть реальный метод на компоненте анимируемого объекта, что приваривает данные клипа к раскладке классов и ломается на каждом переименовании. Здесь клип вызывает единственную точку входа `ReceiveEvent(string)`, а ключ — просто данные: хендлеры могут жить где угодно, добавляться и убираться в рантайме, и несколько хендлеров могут делить один ключ. Показаны оба канала: ключевой `Subscribe("Hello", ...)` для конкретного момента и catch-all `OnEvent` для логирования, и оба симметрично отписаны в `OnDisable`. В клипе Function должна указывать на `ReceiveEvent`.

### 19. Перекачка из любого геттера в любой сеттер обобщённым `CopyTo`

```csharp
// только обобщённые перегрузки — боксинга нет
health.CopyTo(healthBarSetter);          // IValue<int> -> ISetter<int>
moveDirection.CopyFrom(inputProvider);   // ISetter<Vector3> <- IValue<Vector3>
```

В документации отсутствует, но идиоматично: поскольку чтение и запись — разные интерфейсы, одноразовый перенос не требует конкретного типа ни с одной стороны и не нуждается в промежуточной локальной переменной. Обе обобщённые формы помечены `AggressiveInlining`, то есть вырождаются в прямое присваивание свойства свойству.

Антипаттерн:

```csharp
// ❌ необобщённая форма: int боксируется дважды за вызов
IValue value = health;
ISetter target = healthBarSetter;
value.CopyTo(target);
```

---

## Производительность

Разделяю то, что **заявлено** (в документации), **видно в исходнике** и является **выводом** (мой анализ формы кода, не измерение).

**1. Компарер реактивной переменной резолвится один раз на закрытый дженерик-тип.** *Исходник:* `Variables/Reactive/ReactiveVariable.cs` — `private static readonly IEqualityComparer<T> s_equalityComparer = EqualityComparer.GetDefault<T>();`. `GetDefault<T>()` читает `Cache<T>.Comparer`, где `static class Cache<T>` в статическом конструкторе один раз выполняет `GetDefaultHelper(typeof(T))` — цепочку сравнений `RuntimeTypeHandle.Equals` (`Utils/EqualityComparer.cs`). *Вывод:* установившаяся стоимость — одно чтение статического поля плюс один интерфейсный вызов; аллокаций и боксинга нет даже для `Vector3`, поскольку компареры — рукописные `IEqualityComparer<Vector3>`, работающие покомпонентно через `float.Equals`.

**2. Монотипные нереактивные варианты — самые дешёвые изменяемые контейнеры.** *Исходник:* `BoolVariable`, `IntVariable`, `FloatVariable`, `Vector2/3/4Variable`, `Vector2IntVariable`, `Vector3IntVariable`, `QuaternionVariable` объявлены `sealed`, не имеют компарера вообще, обращаются к полю напрямую и каждый переопределяет `Invoke()`. *Заявлено:* `Docs/Elements/Variables/Manual.md` рекомендует их как «slightly faster than the generic version». *Вывод:* `sealed` даёт JIT/IL2CPP возможность девиртуализовать, а собственный `Invoke()` убирает хоп через default interface implementation.

**3. Реактивные монотипные варианты эту вторую экономию не дают.** *Исходник:* `ReactiveBool`, `ReactiveInt`, `ReactiveFloat`, `ReactiveVector2/3/4`, `ReactiveVector2Int`, `ReactiveVector3Int`, `ReactiveQuaternion` — ни один не `sealed` и ни один не объявляет `Invoke()`; `ReactiveVariable<T>` при этом `Invoke()` объявляет. *Вывод:* на `Reactive*` обращение через `IFunction<T>.Invoke()` идёт в default-реализацию интерфейса; выигрыш относительно `ReactiveVariable<T>` сводится к нативному `!=` вместо интерфейсного компарера.

**4. Чтение через `IValue<T>`-ссылку дороже, чем кажется.** *Исходник:* в `Variables/Base/IVariable.cs` оба мостика (`T IValue<T>.Value => this.Value;` и `T ISetter<T>.Value { set => this.Value = value; }`) не несут `[MethodImpl(AggressiveInlining)]`, тогда как `ISetter<T>.Invoke` в `Setters/ISetter.cs` — несёт. *Вывод:* чтение переменной через ссылку типа `IValue<T>` — это непрохинтованный интерфейсный вызов в DIM, который делает второй виртуальный вызов свойства. Объявляйте поле как интерфейс способности (это правило №1 дизайна), но выносите `.Value` за пределы горячего цикла.

**5. Подписка аллокаций не стоит, если не боксировать.** *Исходник:* `Events/Subscription.cs` — `Subscription`, `Subscription<T>`, `<T1,T2>`, `<T1,T2,T3>`, `<T1,T2,T3,T4>` все `readonly struct : IDisposable`, `+=` выполняется в конструкторе, `Dispose()` прикрыт `if (this.signal != null)`. `Utils/DisposableAction.cs` — тоже `readonly struct`. *Вывод:* хранение токена в поле или в `DisposableComposite<Subscription<T>>` аллокаций не делает; присваивание его в поле типа `IDisposable` — боксирует.

**6. `DisposableComposite<T>` — контейнер без боксинга, необобщённый — с боксингом.** *Заявлено:* `Docs/Elements/Utils/DisposableComposite`1.md` («use this generic version when all disposables share the same type, allowing type safety and boxing avoidance to `IDisposable`»), `Utils/Manual.md`, и `Utils/Extensions.md` про `AddTo<T>` («Avoid boxing to `IDisposable` reference type»). *Исходник:* ограничение `where T : IDisposable` + `List<T>` + вызов через constrained callvirt; `public class DisposableComposite : DisposableComposite<IDisposable> { }`. *Вывод:* `DisposableComposite<Subscription>` хранит структуры инлайн в `List<Subscription>`; необобщённый боксирует каждую. То же для `AddTo`: обобщённая перегрузка сохраняет структуру неупакованной, необобщённая — нет. Ни одна из двух `AddTo` не помечена `AggressiveInlining`.

**7. Итерация в `Dispose()` без аллокаций.** *Исходник:* `for (int i = 0, count = _items.Count; i < count; i++)` по кешированному `count`, без `foreach`, без энумератора и без проверки версии; затем `_items.Clear()`. *Вывод:* композит переиспользуем после `Dispose()`, поэтому цикл install/uninstall не требует нового объекта.

**8. Но у `DisposableComposite<T>` нет безпараметрического конструктора.** *Исходник:* объявлены только `(IEnumerable<T>)` и `(params T[])`. *Вывод:* `new DisposableComposite<Subscription>()` разрешается в `params`-перегрузку в развёрнутой форме и аллоцирует пустой массив плюс `List<T>` ёмкостью 0, который переаллоцируется на первом `Add`. Передавайте начальный набор в конструктор, когда он известен.

**9. Делегатные адаптеры — главная точка аллокаций этого слоя.** *Исходник:* `InlineVariable<T>` держит два делегата, `InlineReactiveVariable<T>` — четыре, а `Extensions.AsInlineVariable<T,R>` (`Variables/Extensions.cs`) создаёт display class, захватывающий `it`/`getter`/`setter`, два делегата и сам `InlineVariable<R>` — четыре аллокации на вызов. *Вывод:* нормально на install/bind, недопустимо на кадр. Рукописные `sealed`-адаптеры (`TransformPositionVariable` и аналоги) — альтернатива с нулём делегатов: одно `readonly`-поле и прямой форвард.

**10. Необобщённые пути боксируют.** *Исходник:* `IValue { object Value { get; } }` и `ISetter { object Value { set; } }`; необобщённые `CopyTo(this IValue, ISetter)` / `CopyFrom(this ISetter, IValue)` в `Variables/Extensions.cs`. *Вывод:* необобщённая перекачка боксирует дважды за вызов — чтение упаковывает в `object`, запись распаковывает жёстким кастом в `ISetter.Value`. Обобщённые перегрузки помечены `AggressiveInlining` и не боксируют ничего.

**11. `AggressiveInlining` расставлен точечно — полезно знать, где его нет.** *Исходник:* помечены `ISetter<T>.Invoke`, `AtomicUtils.IsPlayMode()`/`IsEditMode()`, `AsСonst`, `Observe`, `AsVariable`/`AsReactiveVariable`/`AsInlineVariable`, все четыре `CopyTo`/`CopyFrom`, все `Subscribe`/`Unsubscribe` и все `SubscribeRange` в `Events/Extensions.cs` (25 атрибутов в файле), `InternalUtils.Contains`. **Не** помечены: мостики `IVariable<T>`, обе `AddTo`, все `UnsubscribeRange`.

**12. Гарды режима редактора бесплатны в билде.** *Исходник:* тело `IsPlayMode()`/`IsEditMode()` целиком за `#if UNITY_EDITOR`, вне редактора возвращает литерал, и оба метода `AggressiveInlining`. *Вывод:* в плеерной сборке это константа, и JIT выкидывает мёртвую ветку.

**13. `MainThreadDispatcher` коалесцирует и не аллоцирует в установившемся режиме.** *Исходник:* `private static readonly HashSet<IFlushable> _dirty = new(256);` и `private static readonly List<IFlushable> _processing = new(256);` — оба переиспользуются; lock держится только на `_processing.AddRange(_dirty); _dirty.Clear();`, а `Flush()` вызывается вне lock. *Вывод:* N записей за кадр схлопываются в **один** колбэк с **последним** значением; промежуточные значения теряются. Повторный `MarkDirty` из колбэка безопасен — он попадёт в следующий кадр.

**14. PlayerPrefs-адаптеры нельзя опрашивать каждый кадр.** *Исходник:* каждое чтение `Value` — нативный вызов `PlayerPrefs.Get*` (у `BoolPrefsVariable` плюс `!= 0`), и `Invoke()` реализован как `=> this.Value`, то есть свойства не минует. *Вывод:* читать один раз в `ReactiveVariable<T>`.

**15. Потокобезопасные варианты берут lock на каждом чтении.** *Исходник:* `ThreadSafeVariable<T>` и `ThreadSafeReactiveVariable<T>` оборачивают геттер, сеттер и `ToString()` в `lock (_lock)`, и оба используют `EqualityComparer<T>.Default`, а не `EqualityComparer.GetDefault<T>()`. *Вывод:* снимайте снапшот раз в кадр; в горячем цикле не читать. И «`ReactiveVariable<Vector3>` сравнивает точно, а `ReactiveVector3` — с допуском» на них не распространяется: у них вообще другой компарер.

**16. Внутренняя плюмбинга.** *Исходник:* `InternalUtils.CeilToPrime` — статическая таблица из 31 простого + `Array.BinarySearch`, без пробного деления; `Contains<T>` — `AggressiveInlining` и явный компарер, чтобы не боксировать. `AnimationEvents` использует `new Dictionary<string, Action>(StringComparer.Ordinal)` — без культурно-зависимого сравнения — и удаляет ключ, когда снят последний хендлер, поэтому таблица не накапливает мусор.

**17. Коробки для физики — по одной на компонент, сравнение по ссылочной идентичности.** *Исходник:* `HashSet<Collision>` / `HashSet<Collider>` с дефолтным компарером в каждом из четырёх фасадов. *Вывод:* для `TriggerEvents`/`TriggerEvents2D` ключи — стабильные объекты `Collider`, и это корректно; для `CollisionEvents`/`CollisionEvents2D` ключи — экземпляры `Collision`, которые Unity может переиспользовать или создавать заново на каждый колбэк, поэтому парность `Add`/`Remove` не гарантирована. Относитесь к `CurrentCollisions` как к best-effort, и там, где нужна точная парность, предпочитайте trigger-варианты либо ведите ключи по `Collider` сами.

**18. `Reference<T>` — единственная мутация на месте.** *Исходник:* `public ref T Value => ref this.value;` — единственный ref-возвращающий член в `Elements`. *Вывод:* обновление большой структуры через `ref T x = ref r.Value` не копирует `T` вообще, в отличие от любого `IValue<T>`/`IVariable<T>`, которые возвращают по значению.

**19. `Optional<T>` бесплатен.** *Исходник:* `public struct Optional<T>` (не `readonly struct`), а `operator true`/`operator false` читают только поле `active`. *Вывод:* флаги фич в инсталлерах и вью не стоят ни байта в куче, а `if (_vfx)` вырождается в чтение bool-поля. Обратная сторона отсутствия `readonly`: `TryGetValue(out T)` копирует payload.

**20. Что измерено, а не выведено.** В этой области фреймворка нет ни одного бенчмарка. `Docs/Elements/Manual.md` упоминает замеры «on a **MacBook with Apple M1** for collections containing **1000 elements**» и ведёт в `Docs/Elements/Performance/` — там пять файлов, и все они про реактивные **коллекции** (`ReactiveArray`, `ReactiveList`, `ReactiveLinkedList`, `ReactiveDictionary`, `ReactiveHashSet`), не про значения и переменные. Для `Values`/`Variables`/`Setters`/`Utils` всё, что есть, — рекомендация «slightly faster» в `Variables/Manual.md` и TIP'ы про боксинг в `Utils`. Все числа по этому слою придётся получать своим профилированием.

**21. Условная компиляция влияет на производительность и на сборку.** *Исходник:* в `Utils/EqualityComparer.cs` компареры `Vector2/3/4`, `Color`, `Color32`, `Rect`, `Bounds`, `Quaternion` и их type handles лежат внутри `#if UNITY_5_3_OR_NEWER`, а `Vector2Int`, `Vector3Int`, `RangeInt`, `RectInt`, `BoundsInt` — внутри отдельного `#if UNITY_2017_2_OR_NEWER`. *Вывод:* вне Unity (чистая .NET-сборка) `GetDefault<T>()` всегда падает в `EqualityComparer<T>.Default`, то есть расщепление семантики «точный `ReactiveVariable<Vector3>` против допускающего `ReactiveVector3`» существует только в Unity. Сборка: `Scripts/Atomic.Elements.asmdef` ставит `"allowUnsafeCode": true` и ссылается на `Unity.Mathematics`; `package.json` — `"unity": "6.0"`, `"version": "3.0.0"`.

---

## Грабли

1. **`AsConst` недостижим так, как он задокументирован.** `Values/Extensions.cs` объявляет `public static Const<T> AsСonst<T>(this T it)`, где третий символ — **кириллическая** заглавная С (U+0421, байты `d0 a1`). Проверено hexdump'ом: в файле стоит `A s d0 a1 o n s t`, и латинская строка `AsConst` в файле не встречается ни разу. `Docs/Elements/Values/Extensions.md` документирует метод как `AsConst<T>(T)`. Набранный латиницей `value.AsConst()` не скомпилируется. В проектном коде пишите `new Const<T>(value)` или пользуйтесь неявным преобразованием; дефект стоит отправить в upstream.

2. **`new DisposableComposite(d1, d2)` не компилируется.** `DisposableComposite : DisposableComposite<IDisposable>` с пустым телом конструкторов не объявляет, а конструкторы базы не наследуются. `Docs/Elements/Utils/DisposableComposite.md` перечисляет `DisposableComposite(IEnumerable<IDisposable>)` и `DisposableComposite(params IDisposable[])` как члены этого типа — их там нет. Унаследованный `Add` возвращает `DisposableComposite<IDisposable>`, поэтому fluent-цепочка, типизированная как `DisposableComposite`, ломается.

3. **Переименования оставили документацию врущей.** `Variable<T>` документирован в файле `BaseVariable.md`, а `Docs/BestPractices/UsingSetters.md:25` до сих пор пишет `new BaseVariable<Vector3>()` — типа с таким именем не существует. `InlineVariable<T>`/`InlineReactiveVariable<T>` документированы в `ProxyVariable.md`/`ReactiveProxyVariable.md`; `ProxyVariable.md` вдобавок ссылается на несуществующий `InlineVariableBuilder.md` (реальный файл билдера — `ProxyVariableBuilder.md`), а `ReactiveProxyVariableBuilder.md` во всех примерах зовёт класс `ReactiveInlineVariable<T>` (перевёрнутый порядок слов). Всегда сверяйте имя типа с исходником, прежде чем писать его.

4. **У `InlineReactiveVariable<T>` задокументирован несуществующий конструктор.** Документация показывает двухаргументный `InlineReactiveVariable(Func<T>, Action<T>)`, перечисляя при этом четыре параметра. Реальный конструктор один и четырёхаргументный.

5. **Имена в `Constants` расходятся с документацией.** В исходнике `Constants.Gravity`, в `Docs/Elements/Values/Manual.md:220` — `GravityEarth`. Там же, строкой ниже, документация обещает `DefaultMass` со значением `1f` — такого поля в `Constants.cs` нет вообще.

6. **`ReactiveVector3` и `ReactiveVariable<Vector3>` детектируют изменение по-разному.** `ReactiveVector3` сравнивает через Unity-овский `Vector3 !=`, у которого есть допуск по величине разности (порядка 1e-5), поэтому мелкие дельты проглатываются. `ReactiveVariable<Vector3>` идёт через `Vector3EqualityComparer` из Atomic: `self.x.Equals(vector.x) && self.y.Equals(vector.y) && self.z.Equals(vector.z)` — точное покомпонентное сравнение, поэтому float-шум поднимает событие каждый кадр. То же для `ReactiveVector2`/`ReactiveVector4`. Выбирайте осознанно.

7. **Но это расщепление НЕ распространяется на целочисленные векторы и кватернион.** `ReactiveVector2Int.cs` и `ReactiveVector3Int.cs` используют `if (this.value != value)` на `Vector2Int`/`Vector3Int`, где `!=` — точное целочисленное сравнение, то есть ровно то же, что и компарер Atomic. `ReactiveQuaternion.cs` использует `Quaternion !=`, то есть сравнение Unity через скалярное произведение (равны при `Dot(a,b) > 1f - 1e-6f`) — это **угловой** допуск, а не покомпонентный 1e-5, и на практике он существенно грубее.

8. **`float.Epsilon` в `ReactiveFloat` — не допуск.** Сеттер сравнивает `Math.Abs(this.value - value) > float.Epsilon`, а `float.Epsilon` — наименьший положительный денормал (≈1.4E-45). Это фактически точное неравенство. Нужен настоящий допуск — пишите свой тип.

9. **Неявный `operator T` у `Optional<T>` не смотрит на `Active`.** `T x = inactiveOptional;` тихо вернёт `default(T)` без всякого предупреждения. Всегда проверяйте `if (opt)` или `TryGetValue(out var v)`.

10. **Активация `Optional<T>` использует `value != null` на несвязанном `T`.** Для non-nullable value-типов это всегда истина: `Optional<int> o = 0;` получается **активным**. А для payload типа `UnityEngine.Object` это поднятое ссылочное сравнение, а не Unity-проверка на разрушенный объект, поэтому уничтоженный объект может отрапортовать `Active == true`. Присваивание `Value = null` молча деактивирует optional.

11. **`DisposableAction.Dispose()` не идемпотентен.** Он вызывает действие **каждый раз**. А поскольку это структура, `default(DisposableAction).Dispose()` бросает `NullReferenceException`, обойдя `ArgumentNullException` конструктора. Для отписки от событий берите `Subscription`/`Subscription<T>`, чей `Dispose` прикрыт null-проверкой и идемпотентен.

12. **Конструирование `Subscription` — это побочный эффект.** `+=` происходит в конструкторе. Создали и выбросили результат — утекла подписка без единого хендла для снятия. Никогда не игнорируйте возврат `Subscribe`/`Observe`.

13. **`Observe` вызывает колбэк ДО подписки.** `action.Invoke(it.Value); return new Subscription<T>(it, action);`. Хендлер, бросивший исключение на прайминге, оставит вас **не подписанным**, и никакой exception safety здесь нет.

14. **`IAction`-перегрузки `Subscribe` строят новый делегат.** `Subscribe(this ISignal it, IAction action) => new(it, action.Invoke)` создаёт свежий делегат `action.Invoke` на каждый вызов. Возвращённый `Subscription` — единственное, что способно его снять: `Unsubscribe(signal, тотЖеIAction)` построит **другой** делегат и молча не удалит ничего. Те же грабли у `UnsubscribeRange` (все пять арностей) — он тоже делает `it.OnEvent -= action.Invoke` поэлементно. `SubscribeRange`/`UnsubscribeRange` вообще не возвращают токен, поэтому применимы только к сигналам, которые вы уничтожаете целиком.

15. **`ReactiveVector3Int` отличается от всего семейства и содержит баг двойной подписки.** Только у этого типа есть инстанс-методы:

    ```csharp
    public Subscription<Vector3Int> Subscribe(Action<Vector3Int> listener)
    {
        this.OnEvent += listener;                               // первое +=
        return new Subscription<Vector3Int>(this, listener);    // конструктор делает второе +=
    }
    ```

    Конструктор `Subscription<T>` сам выполняет `this.signal.OnEvent += this.action;`, поэтому listener оказывается в списке **дважды** и будет вызван дважды на каждое изменение, а `Dispose()` снимет только одну копию. Проверено grep'ом: `public Subscription` встречается в `Scripts/Variables/` ровно один раз, в `ReactiveVector3Int.cs`. Не вызывайте этот метод — используйте расширение `Subscribe` из `Events/Extensions.cs` или `Observe`.

16. **Необобщённые `IValue`/`ISetter` боксируют и могут бросить.** `object Value` боксирует value-типы на каждом обращении, а сеттер делает жёсткий каст `(T) value` и бросит `InvalidCastException` при несовпадении. Они существуют для редактора и рефлексии. Никогда не типизируйте против них игровой код и не пользуйтесь необобщёнными `CopyTo`/`CopyFrom`.

17. **`ThreadSafeReactiveVariable<T>` коалесцирует оповещения.** N записей за кадр дают **один** колбэк с последним значением, выпущенный из `MainThreadDispatcher.Update()`. Промежуточные значения теряются навсегда. Никогда не используйте его для передачи последовательности дискретных событий.

18. **`ThreadSafeVariable<T>` и `ThreadSafeReactiveVariable<T>` — не инспекторные типы.** Не `[Serializable]`, без неявных операторов, без `Invoke()`, lock на каждом чтении включая `ToString()`, и компарер `EqualityComparer<T>.Default` вместо оптимизированного `EqualityComparer.GetDefault<T>()`. `ThreadSafeVariable<T>` вообще не реализует `IDisposable`.

19. **`MainThreadDispatcher` молча создаёт GameObject.** На `BeforeSceneLoad` в каждом запуске появляется `DontDestroyOnLoad`-объект с именем `[MainThreadDispatcher]`. Он `internal sealed`, поэтому `IsMainThread` игровому коду недоступен, а `IFlushable` — хотя и фигурирует в публичном списке базовых типов `ThreadSafeReactiveVariable<T>` — реализовать вне сборки нельзя. В исходнике, кстати, остались рабочие пометки `// Thread 3`, `// Thread 2`, `// Thread 1` и маркер `//O`.

20. **`Dispose()` у реактивных переменных только обнуляет подписчиков.** `public void Dispose() => this.OnEvent = null;` — значение не сбрасывается, объект остаётся полностью рабочим. Диспознутая реактивная переменная тихо перестаёт оповещать, и это выглядит как логическая ошибка, а не как ошибка времени жизни.

21. **Билдеры — структуры, и `WithX` возвращают `this` по значению.** `InlineVariable<T>.Builder` и `InlineReactiveVariable<T>.Builder` — `public struct`. Цепочку нельзя разрывать: присвоили билдер в локальную переменную и вызвали `WithGetter` отдельным оператором — мутация потеряна.

22. **Неявные преобразования несимметричны.** `Const<T>` и `Variable<T>` конвертируются в обе стороны. `BoolVariable`/`IntVariable`/`FloatVariable`/`Vector*Variable`/`QuaternionVariable`/`ReactiveBool`/`ReactiveInt`/`ReactiveFloat`/`ReactiveVector*`/`ReactiveQuaternion` — только **из** примитива. `ScriptableConst<T>` не конвертируется вообще. Не предполагайте симметрию.

23. **`[Serializable]` на делегатных типах вводит в заблуждение.** У `InlineValue<T>`, `InlineVariable<T>`, `InlineSetter<T>`, `InlineSignal*` и у Transform/PlayerPrefs-адаптеров единственные поля — `readonly`-делегаты или рантайм-ссылки, которые Unity не сериализует. Не ждите, что они переживут domain reload или осмысленно покажутся в инспекторе.

24. **PlayerPrefs-переменные не валидируют ключ и не сохраняют.** `IntPrefsVariable(string key, int defaultValue = default)` просто присваивает `this.key = key` — null дойдёт до PlayerPrefs. `PlayerPrefs.Save()` не вызывается нигде. Каждое чтение — нативный вызов плюс хеш строки.

25. **`TransformParentVariable` — не та форма, которую надо копировать.** Из четырёх Transform-адаптеров три (`TransformPositionVariable`, `TransformRotationVariable`, `TransformScaleVariable`) `sealed`, `[Serializable]`, с `[ShowInInspector]` и переопределённым `Invoke()`. `TransformParentVariable` — ни `sealed`, ни `[Serializable]`, без `[ShowInInspector]` и без `Invoke()`. Плюс семантика: `TransformScaleVariable` биндится на `localScale` (не на мировой масштаб), `TransformRotationVariable` — на мировой `rotation`, а `TransformParentVariable` присваивает `_transform.parent`, что сохраняет мировой трансформ.

26. **`GameObjectLayerVariable`, `IRandomizer`/`UnityRandomizer` и `RendererEvents` не обёрнуты в `#if UNITY_5_3_OR_NEWER`** при наличии зависимости от UnityEngine — они сломают не-Unity сборку `Atomic.Elements`. `GameObjectLayerVariable` вдобавок пропускает проверку в конструкторе и `Invoke()`, которые есть у всех соседних адаптеров.

27. **`AnimationEvents` асимметричен по обработке ошибок.** `Subscribe` бросает `ArgumentNullException` на пустой ключ или null-действие, `Unsubscribe` молча возвращается. Не рассчитывайте, что `Unsubscribe` сообщит об опечатке в ключе.

28. **`RendererEvents` — самый недоделанный фасад.** Нигде не задокументирован, без `[AddComponentMenu]`, без `[DisallowMultipleComponent]`, без `[HelpURL]`, без `#if`-гарда, и сворачивает два колбэка в один `Action<bool> OnVisible` вместо пары `OnEntered`/`OnExited`, принятой у остальных. Пользоваться можно, но за образец берите `CollisionEvents`/`TriggerEvents`.

29. **Без Odin правка реактивной переменной в инспекторе никого не оповещает.** Это единственный поведенческий (а не косметический) пункт за `#if ODIN_INSPECTOR`: `[HideLabel, OnValueChanged(nameof(InvokeEvent))]` — ровно то, что заставляет инспекторную правку поднять `OnEvent`, потому что `InvokeEvent` приватный и иначе недостижим. Заодно `InlineValue<T>`/`InlineVariable<T>`/`InlineSetter<T>` без Odin вообще не появляются в инспекторе.

30. **`EqualityComparer.GetDefault<T>()` и `InternalUtils` — `internal`.** Лучший компарер фреймворка переиспользовать в игровом коде нельзя. Если вам нужен не-боксящий компарер для `Vector3` в своём словаре — пишите свой.

31. **Детерминированного рандомайзера нет.** У `IRandomizer` единственная реализация — `UnityRandomizer` поверх `UnityEngine.Random`; `DefaultRandomizer` на `ThreadLocal<System.Random>` в файле закомментирован. Seeded/потокобезопасный рандом — ваша работа.

32. **Сообщение об ошибке в `CeilToPrime` врёт.** `throw new InvalidOperationException($"Prime can't get larger than {PrimeTable.Length - 1}")` напечатает `30`, подразумевая `1610612741`. Если увидите это в логе, не ищите ограничение в 30 элементов.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Корень области

| файл | что внутри |
|---|---|
| `Docs/Elements/Manual.md` | Точка входа во весь слой Elements. Содержит требование «requires **Unity 6** or **.NET 7+**», раздел «Using Odin Inspector» с формулировкой «works without Odin», и оглавление по всем подобластям. Здесь же — указание, что замеры производительности делались «on a **MacBook with Apple M1** for collections containing **1000 elements**» со ссылкой в `Docs/Elements/Performance/`. |

### Values

| файл | что внутри |
|---|---|
| `Docs/Elements/Values/Manual.md` | Обзор + четыре примера использования (`IValue<T>`, `IReactiveValue<T>`, `Const<T>`, `ScriptableConst<T>`), API Reference и — главное — **полная таблица `Constants`** по разделам (Boolean / Mathematical / Time / Common Values / Physics / Unity-Specific Vectors / Unity-Specific Colors). Именно в этой таблице живут две ошибки: `GravityEarth` вместо `Gravity` (строка 220) и несуществующий `DefaultMass` (строка 221). |
| `Docs/Elements/Values/IValue.md` | Контракт `IValue<T>` и примеры реализации. Сигнатуру показывает как `public interface IValue<out T> : IFunction<T>` — ковариантность на месте, а необобщённый базовый `IValue` в списке базовых типов пропущен. |
| `Docs/Elements/Values/IReactiveValue.md` | Контракт «читать + наблюдать» и ручная пара `Subscribe`/`Unsubscribe` (именно отсюда взят антипаттерн к идиоме 3). Показывает `IReactiveValue<T>` без `out` — в исходнике `out T`. |
| `Docs/Elements/Values/Const.md` | `Const<T>` целиком, с TIP'ом про Flyweight: «Unlike regular value types, `Const<T>` is a **reference type**... to **share a constant value** across **multiple instances** without copying the value». |
| `Docs/Elements/Values/ScriptableConst.md` | `ScriptableConst<T>` и обязательный закрытый подкласс с `[CreateAssetMenu]`. На этот файл ссылается `[HelpURL]` в самом исходнике. |
| `Docs/Elements/Values/Extensions.md` | Документирует `AsConst<T>(T)` (латиницей — в исходнике кириллическая С) и `Observe`. На раздел `#observe` этого файла ссылается best-practice по `Observe`. |
| — | **Файла `Values/InlineValue.md` не существует.** `InlineValue<T>` не задокументирован нигде. |

### Variables

| файл | что внутри |
|---|---|
| `Docs/Elements/Variables/Manual.md` | Самый полезный файл области. Три примера (`Variable<T>`, `ReactiveVariable<T>`, `InlineVariable<T>`), API Reference и раздел **«Specialized Types»** — полные списки по четырём группам: Base Variables, Reactive Variables, Proxy Variables (PlayerPrefs и Transform), Thread-Safe Variables. Здесь же дважды, для базовых и для реактивных, стоит рекомендация «It is recommended to use them, as they compare values without relying on `EqualityComparer`, which makes them slightly faster than the generic version». Это единственное утверждение о производительности переменных во всей документации. |
| `Docs/Elements/Variables/IVariable.md` | Контракт `IVariable<T>` и пример рукописного адаптера `TransformPositionVariable` — почти совпадающий с шипнутым классом (в документации он без `sealed` и без `Invoke()`). |
| `Docs/Elements/Variables/IReactiveVariable.md` | Контракт «читать + писать + наблюдать». |
| `Docs/Elements/Variables/BaseVariable.md` | **Документация на `Variable<T>`**, несмотря на имя файла. Устаревшее имя `BaseVariable<T>` в исходнике не существует. |
| `Docs/Elements/Variables/ReactiveVariable.md` | `ReactiveVariable<T>`: сравнение в сеттере, `OnEvent`, `Dispose`. |
| `Docs/Elements/Variables/ProxyVariable.md` | **Документация на `InlineVariable<T>`.** Ссылается на несуществующий `InlineVariableBuilder.md`; реальный файл билдера — `ProxyVariableBuilder.md`. |
| `Docs/Elements/Variables/ProxyVariableBuilder.md` | Fluent-билдер `InlineVariable<T>.Builder`. |
| `Docs/Elements/Variables/ReactiveProxyVariable.md` | **Документация на `InlineReactiveVariable<T>`.** Здесь же тот самый несуществующий двухаргументный конструктор. |
| `Docs/Elements/Variables/ReactiveProxyVariableBuilder.md` | Билдер на четыре `WithX`. Во всех примерах класс назван `ReactiveInlineVariable<T>` — перевёрнутый порядок слов, реального типа с таким именем нет. |
| `Docs/Elements/Variables/ThreadSafeVariable.md` | `ThreadSafeVariable<T>`: lock-семантика, отсутствие оповещений. |
| `Docs/Elements/Variables/ThreadSafeReactiveVariable.md` | `ThreadSafeReactiveVariable<T>` и диспатч на главный поток. Читать обязательно перед любой работой с состоянием из фоновых потоков — здесь описана коалесценция. |
| `Docs/Elements/Variables/Extensions.md` | `AsVariable`/`AsReactiveVariable`/`AsInlineVariable`. `CopyTo`/`CopyFrom` в этом файле **не задокументированы**, хотя объявлены рядом в `Variables/Extensions.cs`. |

### Setters

| файл | что внутри |
|---|---|
| `Docs/Elements/Setters/Manual.md` | `ISetter<T>` целиком: базовое использование и пример контроллера (идиома 2 — оттуда). Короткий файл, читается за пять минут. |
| `Docs/Elements/Setters/ISetter.md` | Контракт записи, связь с `IAction<T>`. |
| `Docs/Elements/Setters/InlineSetter.md` | `InlineSetter<T>`. Про редакторную кнопку `Editor_SetValue` здесь нет ни слова — о ней узнаёшь только из исходника. |

### Utils

| файл | что внутри |
|---|---|
| `Docs/Elements/Utils/Manual.md` | Восемь примеров использования: `Reference<T>`, `Optional<T>`, Atomic Utils, Disposable Action, Disposable Composite, Animation Events, Collision Events, Trigger Events. Содержит замечание про обобщённую форму композита: «you can use generic version to avoid boxing to `IDisposable` when add struct disposable to the composite». |
| `Docs/Elements/Utils/Reference.md` | `Reference<T>`: ref-возвращающее свойство, разделяемая ячейка, обход запрета `out` в корутинах. Обе идиомы 13 и 14 — отсюда. |
| `Docs/Elements/Utils/Optional.md` | `Optional<T>`: `operator true`/`false`, `TryGetValue`, `GetValueOrDefault`. Про `operator T` предупреждает прямо: «Does not check for `Active`; ensure optional is active before using». |
| `Docs/Elements/Utils/AtomicUtils.md` | `IsPlayMode()`/`IsEditMode()`. На якорь `#isplaymode` ссылается best-practice по инсталлерам. |
| `Docs/Elements/Utils/DisposableAction.md` | `DisposableAction`. Про отсутствие идемпотентности не предупреждает. |
| `Docs/Elements/Utils/DisposableComposite.md` | Необобщённый `DisposableComposite`. **Содержит неверный API Reference**: перечисляет конструкторы `(IEnumerable<IDisposable>)` и `(params IDisposable[])`, которых у этого типа нет. |
| ``Docs/Elements/Utils/DisposableComposite`1.md`` | Обобщённый `DisposableComposite<T>`. TIP: «use this generic version when all disposables share the same type, allowing type safety and boxing avoidance to `IDisposable`». |
| `Docs/Elements/Utils/Extensions.md` | `AddTo`/`AddTo<T>`. Про обобщённую форму: «Avoid boxing to `IDisposable` reference type». |
| `Docs/Elements/Utils/AnimationEvents.md` | Фасад анимационных событий: как настроить AnimationEvent на `ReceiveEvent(string)`, оба канала подписки. На этот файл ссылается `[HelpURL]` компонента. |
| `Docs/Elements/Utils/CollisionEvents.md`, `CollisionEvents2D.md`, `TriggerEvents.md`, `TriggerEvents2D.md` | Четыре физических фасада. Файлы лежат в `Utils/`, но **исходники — в `Assets/Plugins/Atomic/Elements/Scripts/UnityComponents/`**, а не в `Scripts/Utils/`; на эти .md ссылаются `[HelpURL]` компонентов, и именно отсюда путаница в классификации. |
| — | **`RendererEvents` не задокументирован нигде**, равно как `IRandomizer`/`UnityRandomizer` и `GameObjectLayerVariable`. |

### Best Practices, относящиеся к этой главе

| файл | что внутри |
|---|---|
| `Docs/BestPractices/PreferAbstractInterfaces.md` | «Prefer Atomic Interfaces to Concrete Classes» — правило №1, источник идиомы 1 с парой ❌/✅. |
| `Docs/BestPractices/SharedConstants.md` | «Flyweight Pattern for Constants» — почему `Const<T>` ссылочный и когда это важно (идиома 7). |
| `Docs/BestPractices/UsingObserveWithReactiveValues.md` | Эталонная форма презентера с полем `Subscription<T>` и комментарием `//struct` в исходнике примера (идиома 3). |
| `Docs/BestPractices/UsingSetters.md` | `ISetter<T>` на сущностях: `MoveController : IEntityInit, IEntityTick`, `entity.AddValue<ISetter<Vector3>>("MoveDirection", ...)`. **Содержит `new BaseVariable<Vector3>()`** (строка 25) — не скомпилируется, нужен `Variable<Vector3>`. |
| `Docs/BestPractices/UsingSubscriptionsWithDisposeComposite.md` | `DisposableComposite` в инсталлерах — источник идиомы 4. Учтите две поправки из этой главы: `Uninstall` принимает `IEntity`, и composite лучше брать обобщённый. |
| `Docs/BestPractices/UsingUtilsForEntityInstallers.md` | «PlayMode & EditMode for EntityInstallers» — идиома 15. В примере дублируется `CharacterMoveController`. |
| `Docs/BestPractices/UsingOptionalWithInstallers.md` | Двухслойная конфигурация `ScriptableEntityInstaller` + `Optional<T>`-override (идиома 6). Подписи `Install` в примере не соответствуют реальным абстрактным методам — сверяйтесь с `Assets/Plugins/Atomic/Entities/Scripts/Installers/`. |
| `Docs/BestPractices/UsingConstantsWithAndExpressions.md` | `Constants.True`/`Constants.False` в графах выражений: «**shared**, **allocation-free**», «Avoids unnecessary object creation for simple `true`/`false` checks. Keeps expression logic **lightweight** and **garbage-free**» (идиома 16). |

### Исходники — куда идти за правдой

Все пути от `Assets/Plugins/Atomic/Elements/Scripts/`.

| путь | что там |
|---|---|
| `Values/` | `IValue.cs`, `IReactiveValue.cs`, `Const.cs`, `InlineValue.cs`, `ScriptableConst.cs`, `Constants.cs`, `Extensions.cs` (здесь кириллическая С в `AsСonst`). |
| `Setters/` | `ISetter.cs`, `InlineSetter.cs`. |
| `Variables/Base/` | `IVariable.cs`, `Variable.cs`, `InlineVariable.cs`, `ThreadSafeVariable.cs`; `Base/Common/` — `BoolVariable.cs`, `IntVariable.cs`, `FloatVariable.cs`; `Base/Unity/` — `Vector2/3/4Variable.cs`, `Vector2IntVariable.cs`, `Vector3IntVariable.cs`, `QuaternionVariable.cs`. |
| `Variables/Reactive/` | `IReactiveVariable.cs`, `ReactiveVariable.cs`, `InlineReactiveVariable.cs`, `ThreadSafeReactiveVariable.cs`; `Reactive/Common/` — `ReactiveBool.cs`, `ReactiveInt.cs`, `ReactiveFloat.cs`; `Reactive/Unity/` — `ReactiveVector2/3/4.cs`, `ReactiveVector2Int.cs`, `ReactiveVector3Int.cs` (баг двойной подписки), `ReactiveQuaternion.cs`. |
| `Variables/Proxy/` | `Transform/` — четыре Transform-адаптера; `PlayerPrefs/` — `BoolPrefsVariable.cs`, `IntPrefsVariable.cs`, `FloatPrefsVariable.cs`, `StringPrefsVariable.cs`; `GameObject/GameObjectLayerVariable.cs`. |
| `Variables/Extensions.cs` | `AsVariable`, `AsReactiveVariable`, `AsInlineVariable`, четыре `CopyTo`/`CopyFrom`. |
| `Utils/` | `Reference.cs`, `Optional.cs`, `DisposableAction.cs`, `DisposableComposite.cs`, ``DisposableComposite`1.cs``, `Extensions.cs` (`AddTo`), `AtomicUtils.cs`, `IRandomizer.cs`, и три internal-файла: `EqualityComparer.cs`, `MainThreadDispatcher.cs`, `InternalUtils.cs`. |
| `UnityComponents/` | `AnimationEvents.cs`, `CollisionEvents.cs`, `CollisionEvents2D.cs`, `TriggerEvents.cs`, `TriggerEvents2D.cs`, `RendererEvents.cs`. **Не** в `Utils/`, хотя документация на них лежит в `Docs/Elements/Utils/`. |
| `Events/` | `ISignal.cs` (арности 0..4), `Subscription.cs` (пять структур), `Extensions.cs` (полная матрица `Subscribe`/`Unsubscribe`/`SubscribeRange`/`UnsubscribeRange`), `InlineSignal.cs`, плюс `IEvent.cs`, `Event.cs`, `ThreadSafeEvent.cs` — последние три за пределами этой главы. |
| `Actions/IAction.cs`, `Functions/IFunction.cs` | База для `ISetter<T>` и `IValue<T>` соответственно. Читать вместе с интерфейсами. |
| `Atomic.Elements.asmdef` | `"allowUnsafeCode": true`, ссылка на `Unity.Mathematics`, `versionDefines` → `UNITY_MATHEMATICS`. |
| `Assets/Plugins/Atomic/package.json` | `"unity": "6.0"`, `"version": "3.0.0"` — формальное подтверждение требования к версии движка. |

### Куда НЕ ходить

- `Docs/Elements/Reference/` и `Assets/.../Elements/Scripts/Reference/` **не существуют**. `Reference<T>` — это один файл `Scripts/Utils/Reference.cs` и один `Docs/Elements/Utils/Reference.md`.
- `Assets/.../Elements/Scripts/Reactive/` как папки верхнего уровня **не существует**: реактивные переменные лежат в `Scripts/Variables/Reactive/`.
- `Docs/Elements/Performance/` содержит пять файлов, и все они про реактивные **коллекции**, а не про значения и переменные: `ReactiveArrayPerformance.md`, `ReactiveListPerformance.md`, `ReactiveLinkedListPerformance.md`, `ReactiveDictionaryPerformance.md`, `ReactiveHashSetPerformance.md`.

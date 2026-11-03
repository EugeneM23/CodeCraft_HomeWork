# 05. Elements: функции и выражения

`Atomic.Elements` делит мир на «значения», «действия», «события» и — эта глава — «функции». Функции
(`Assets/Plugins/Atomic/Elements/Scripts/Functions`) — это три крошечных интерфейса `IFunction<...>` с
вариантностью, их булева специализация `IPredicate`, обёртки над лямбдами `InlineFunction`/`InlinePredicate` и
три сериализуемых композита для инспектора: `AndPredicate`, `OrPredicate`, `NotPredicate`. Выражения
(`Scripts/Expressions`) — составная форма: выражение одновременно *является* реактивным списком функций-членов и
само *является* функцией, поэтому набор условий или множитель урона становится объектом, в который другие
системы добавляют и из которого убирают слагаемые в рантайме. Конкретные подклассы задают только правило
свёртки (`&&`, `||`, `+`, `*`) над struct-энумератором; хранение, реактивность и события они наследуют от
`ReactiveLinkedList<T>`. Практический вывод для миграции: эта часть фреймворка заменяет рукописные `bool`-флаги и
захардкоженную математику модификаторов, но документация по ней устарела сильнее, чем по любой другой области, и
почти все примеры из `Docs/Elements/Expressions` не компилируются.

---

## Модель

**1. Функция — это интерфейс, а не делегат.** Потребитель объявляет зависимость как `IFunction<Vector3>`, а не
как `Func<Vector3>` и не как `Rigidbody`. Кто и откуда берёт значение — дело установщика. Вариантность
(`out R`, `in T`) объявлена в самих интерфейсах, так что `IFunction<Derived>` присваивается в
`IFunction<Base>` без адаптеров. Арности выше двух нет: третий аргумент — повод завести struct.

**2. `IPredicate` — чистый маркер.** У него нет ни одного собственного члена, он лишь фиксирует в типе, что `bool`
здесь — условие, а не вычисленный результат. `IPredicate` и `IFunction<bool>` взаимозаменяемы в точке вызова.

**3. Выражение — это список И функция одновременно.** `IExpression<R>` наследует
`IReactiveList<ExpressionMember<R>>` и `IValue<R>`. Отсюда вся идеология: эффект стана добавляет свой член в
`AndExpression` условия выстрела и убирает его при снятии, а стрелковая система об эффекте ничего не знает.
Разделяйте намерение через тип: читающая сторона держит `IFunction<bool>`, мутирующая — `IExpression<bool>`.

**4. Элемент хранения — `ExpressionMember<R>`, а не `Func<R>`.** Это самое важное расхождение с документацией.
Все страницы `Docs/Elements/Expressions` говорят `IList<Func<R>>` и `params Func<bool>[]`; в исходниках это
`IReactiveList<ExpressionMember<R>>` и `params ExpressionMember<bool>[]`. Неявного преобразования
`Func<R>` → `ExpressionMember<R>` нет, поэтому ни один конструкторный пример из доков не собирается. Члены
добавляются extension-методами `Add`, которые строят `ExpressionMember` за вас.

**5. `ExpressionMember` несёт тег владельца.** Помимо делегата в члене лежит `object Source`. Это единственный
надёжный способ убрать член позже: удаление по делегату работает только если вы сохранили *тот самый* экземпляр
`Func<R>`, потому что сравнение идёт через `==` делегатов.

**6. Подкласс выражения пишет только свёртку.** `ExpressionBase<R>.Invoke()` создаёт struct-энумератор и передаёт
его в `protected abstract R Invoke(Enumerator)`. Свёртка никогда не трогает список напрямую — поэтому своё
выражение занимает шесть строк и не аллоцирует.

**7. Два разных мира композиции условий.** `AndExpression`/`OrExpression` — рантайм-мутируемые, реактивные,
на связном списке. `AndPredicate`/`OrPredicate`/`NotPredicate` — сериализуемые, на `[SerializeReference]`
(`IPredicate[]` у `And`/`Or`, один `IPredicate` у `Not`), неизменяемые в рантайме, с массивным циклом внутри. Первые — когда состав условий меняется по ходу игры; вторые —
когда дизайнер собирает дерево условий в префабе. Путать их дорого в обе стороны.

**8. Ничего не кэшируется.** `Value` и `Invoke()` каждый раз сворачивают весь список. Делегаты-члены обязаны быть
чистыми: под Odin `Value` помечен `[ShowInInspector]`, то есть редактор перевычисляет ваше выражение на каждой
перерисовке инспектора.

---

## API

### Функции

```csharp
public interface IFunction<out R>                      { R Invoke(); }
public interface IFunction<in T, out R>                { R Invoke(T args); }
public interface IFunction<in T1, in T2, out R>        { R Invoke(T1 args1, T2 args2); }
```

`Scripts/Functions/IFunction.cs`. Базовый контракт на любое вычисляемое значение. Имена параметров в исходниках —
`args` / `args1`, `args2` (в доках `arg`/`arg1`/`arg2`); важно только для именованных аргументов.

```csharp
public interface IPredicate : IFunction<bool> { }
public interface IPredicate<in T> : IFunction<T, bool> { }
public interface IPredicate<in T1, in T2> : IFunction<T1, T2, bool> { }
```

`Scripts/Functions/IPredicate.cs`. Берите в поле/параметр, когда `bool` — это условие. Членов своих нет.

```csharp
#if ODIN_INSPECTOR
    [InlineProperty]
#endif
[Serializable]
public class InlineFunction<R> : IFunction<R>
{
    private readonly Func<R> func;

    public InlineFunction(Func<R> func) => this.func = func ?? throw new ArgumentNullException(nameof(func));
    public static implicit operator InlineFunction<R>(Func<R> value) => new(value);

    public R Value => this.func.Invoke();
    public R Invoke() => this.func.Invoke();
    public override string ToString() => this.func.Method.Name;
}
```

`Scripts/Functions/InlineFunction.cs`. Главный рабочий инструмент установщика: завернуть лямбду в `IFunction<R>`.
`Value` — вычисляемое свойство, оно вызывает делегат на каждом чтении. **Класс НЕ реализует `IValue<R>`**, хотя
`InlineFunction.md:46` утверждает обратное.

```csharp
public class InlineFunction<T, R> : IFunction<T, R>
{
    public InlineFunction(Func<T, R> func);
    public static implicit operator InlineFunction<T, R>(Func<T, R> value);
    public R Invoke(T args);
    public override string ToString();
}

public class InlineFunction<T1, T2, R> : IFunction<T1, T2, R>
{
    public InlineFunction(Func<T1, T2, R> func);
    public static implicit operator InlineFunction<T1, T2, R>(Func<T1, T2, R> value);
    public R Invoke(T1 arg1, T2 arg2);
    public override string ToString();
}
```

Арности 1 и 2. Свойства `Value` у них нет — им нужны аргументы. `[Serializable]` и `[InlineProperty]` стоят на всех
трёх арностях, `[Button]` под Odin — на каждом `Invoke`.

```csharp
public class InlinePredicate : InlineFunction<bool>, IPredicate
{
    public InlinePredicate(Func<bool> func) : base(func) { }
    public static implicit operator InlinePredicate(Func<bool> value) => new(value);
}

public class InlinePredicate<T> : InlineFunction<T, bool>, IPredicate<T> { /* то же */ }
public class InlinePredicate<T1, T2> : InlineFunction<T1, T2, bool>, IPredicate<T1, T2> { /* то же */ }
```

`Scripts/Functions/InlinePredicate.cs`. Нулевое дублирование состояния: добавлены только маркер-интерфейс и неявное
преобразование. Это образец, по которому стоит писать свои специализации. Своего `[Serializable]` у них нет.

```csharp
[Serializable]
public sealed class AndPredicate : IPredicate
{
#if ODIN_INSPECTOR
    [HideLabel]
#endif
    [SerializeReference]
    private IPredicate[] conditions;

    public AndPredicate() { }
    public AndPredicate(IPredicate[] conditions) { this.conditions = conditions; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Invoke()
    {
        if (this.conditions == null)
            return true;

        for (int i = 0, count = this.conditions.Length; i < count; i++)
            if (!this.conditions[i].Invoke())
                return false;

        return true;
    }
}
```

`Scripts/Functions/AndPredicate.cs`. Берите, когда условия собирает дизайнер в префабе. Пустой/`null` массив → `true`.

```csharp
[Serializable]
public sealed class OrPredicate : IPredicate
{
    [SerializeReference] private IPredicate[] conditions;
    public OrPredicate() { }
    public OrPredicate(IPredicate[] conditions);
    public bool Invoke();   // null → false, короткое замыкание на первом true
}

[Serializable]
public sealed class NotPredicate : IPredicate
{
    [SerializeReference] private IPredicate condition;
    public bool Invoke() => this.condition != null && !this.condition.Invoke();
}
```

`OrPredicate.cs`, `NotPredicate.cs`. `OrPredicate.Invoke` и `NotPredicate.Invoke` **не** несут
`AggressiveInlining`, в отличие от `AndPredicate.Invoke`. У `NotPredicate` вообще нет конструкторов — он только
для инспектора; незаполненный слот даёт `false`, а не `true`.

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InvokeNot(this IFunction<bool> it) => !it.Invoke();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InvokeNot<T>(this IFunction<T, bool> it, T arg) => !it.Invoke(arg);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InvokeNot<T1, T2>(this IFunction<T1, T2, bool> it, T1 arg1, T2 arg2) => !it.Invoke(arg1, arg2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InlineFunction<bool> Negate(this IFunction<bool> it) => new(() => !it.Invoke());
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InlineFunction<T, bool> Negate<T>(this IFunction<T, bool> it) => new(arg => !it.Invoke(arg));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InlineFunction<T1, T2, bool> Negate<T1, T2>(this IFunction<T1, T2, bool> it) =>
        new((arg1, arg2) => !it.Invoke(arg1, arg2));
}
```

`Scripts/Functions/Extensions.cs`. `InvokeNot` — бесплатное отрицание по месту, `Negate` — переиспользуемое
отрицание за цену объекта и замыкания. **Метод называется `Negate`**, хотя `ExtensionsInvert.md` документирует
`Invert` и даже сам исходник помечает блок `#region Invert` — `it.Invert()` не компилируется.

```csharp
public static void Add<R>(this ICollection<Func<R>> it, IFunction<R> member);          // throw на null
public static void Add<T, R>(this ICollection<Func<T, R>> it, IFunction<T, R> member);
public static void Add<T1, T2, R>(this ICollection<Func<T1, T2, R>> it, IFunction<T1, T2, R> member);

public static void Remove<R>(this ICollection<Func<R>> it, IFunction<R> member);       // молча no-op на null
public static void Remove<T1, T2, R>(this ICollection<Func<T1, T2, R>> it, IFunction<T1, T2, R> member);
```

Адаптеры `IFunction` → `ICollection<Func<...>>`. Асимметрия двойная: `Add` кидает `ArgumentNullException`, `Remove`
молчит, и **`Remove` для арности 1 отсутствует** — только арности 0 и 2.

### Значения, нужные выражениям

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

`Scripts/Values/IValue.cs`. Важно для `IExpression<R>`: через `IValue<R>` каждое безаргументное выражение получает
не только `R Value` и `R Invoke()`, но и **негенерик `object Value`**, а реализация `Invoke()` по умолчанию
(default interface implementation) — это то, что `ExpressionBase<R>` перекрывает своим собственным `R Invoke()`.

```csharp
#if ODIN_INSPECTOR
    [InlineProperty]
#endif
[Serializable]
public class Const<T> : IValue<T>
{
    public T Invoke() => this.value;
    public T Value => this.value;

#if ODIN_INSPECTOR
    [HideLabel]
#endif
#if UNITY_5_3_OR_NEWER
    [SerializeField]
#endif
    private T value;

    public Const() { }
    public Const(T value) => this.value = value;

    public static implicit operator Const<T>(T value) => new(value);
    public static implicit operator T(Const<T> value) => value.value;

    public override string ToString() => this.value?.ToString();
}
```

`Scripts/Values/Const.cs`. Это **класс**, не struct — поэтому разделяемый экземпляр из `Constants` остаётся одной и
той же ссылкой, и удаление по `IFunction` его находит. Плюс `[SerializeField]` — это способ задать литерал в
инспекторе и скормить его выражению через перегрузку `Add<R>(IExpression<R>, IFunction<R>)`.

```csharp
public static class Constants
{
    public static readonly Const<bool>  True, False;
    public static readonly Const<float> PI, TwoPI, HalfPI, E, GoldenRatio;
    public static readonly Const<float> Second, Minute, Hour, FrameTime60FPS;
    public static readonly Const<int>   ZeroInt, OneInt;
    public static readonly Const<float> Zero, One, NegativeOne, Half;
    public static readonly Const<float> Gravity;      // 9.81f
#if UNITY_5_3_OR_NEWER
    public static readonly Const<float>   Deg2Rad, Rad2Deg;
    public static readonly Const<Vector3> Up, Down, Left, Right, Forward, Back, ZeroVector, OneVector;
    public static readonly Const<Color>   White, Black, Red, Green, Blue, Transparent;
#endif
}
```

`Scripts/Values/Constants.cs` (список полей приведён сгруппированно; в файле каждое — отдельная строка
`public static readonly`). Нейтральные элементы для свёрток: `Constants.Zero` для `FloatSumExpression`,
`Constants.One` для `FloatMulExpression`, `Constants.True` для `OrExpression`, `Constants.False` для
`AndExpression`.

### Выражения

```csharp
[Serializable]
public struct ExpressionMember<R>
{
#if ODIN_INSPECTOR
    [ShowInInspector]
#endif
    public object Source => _source;

    private object _source;
    private Func<R> _func;

    public ExpressionMember(Func<R> func);
    public ExpressionMember(IFunction<R> func);                // _func = func.Invoke
    public ExpressionMember(object source, Func<R> func);
    public ExpressionMember(object source, IFunction<R> func); // _func = func.Invoke

    public readonly bool EqualsFunction(Func<R> func) => _func == func;
    public readonly R Invoke() => _func.Invoke();
}
```

`Scripts/Expressions/ExpressionMember.cs`. Реальный элемент хранения любого безаргументного выражения.
`readonly` на `Invoke`/`EqualsFunction` означает отсутствие защитной копии при вызове через `ref`/`in`.

```csharp
[Serializable]
public struct ExpressionMember<T, R>
{
    public object Source => _source;
    public ExpressionMember(Func<T, R> func);
    public ExpressionMember(object source, Func<T, R> func);
    public readonly bool EqualsFunction(Func<T, R> func);
    public readonly R Invoke(T arg);
}

[Serializable]
public struct ExpressionMember<T1, T2, R>
{
    public object Source => _source;
    public ExpressionMember(Func<T1, T2, R> func);
    public ExpressionMember(object source, Func<T1, T2, R> func);
    public readonly bool EqualsFunction(Func<T1, T2, R> func);
    public readonly R Invoke(T1 arg1, T2 arg2);
}
```

У параметризованных членов **нет** конструкторов, принимающих `IFunction<...>` — только `Func<...>`. Если
добавляете `IFunction` в выражение арности 1 или 2, передавайте `func.Invoke` сами либо пользуйтесь
`Add`-расширениями, которые это делают.

```csharp
public interface IExpression<R>          : IReactiveList<ExpressionMember<R>>,          IValue<R> { }
public interface IExpression<T, R>       : IReactiveList<ExpressionMember<T, R>>,       IFunction<T, R> { }
public interface IExpression<T1, T2, R>  : IReactiveList<ExpressionMember<T1, T2, R>>,  IFunction<T1, T2, R> { }
```

`Scripts/Expressions/IExpression.cs` (в файле стоит `// ReSharper disable  PossibleInterfaceMemberAmbiguity`).
Асимметрия существенна: у арности 0 есть `Value`, у арностей 1 и 2 его **нет** — там только `Invoke(arg…)`.

Реактивные события, которые выражение наследует через `IReactiveList<T>`, — точные имена:

```csharp
// Collections/List/IReadOnlyReactiveList.cs
new event Action OnStateChanged;
new event Action<int, T> OnItemAdded;
new event Action<int, T> OnItemRemoved;

// Collections/Array/IReadOnlyReactiveArray.cs
event Action<int, T> OnItemChanged;

// Collections/IReadOnlyReactiveCollection.cs
event Action OnStateChanged;
event Action<T> OnItemAdded;
event Action<T> OnItemRemoved;
```

Никаких `OnItemInserted` и `OnItemDeleted` в фреймворке нет — `expr.OnItemInserted += …` не компилируется.

```csharp
[Serializable]
#if ODIN_INSPECTOR
[InlineProperty]
#endif
public abstract class ExpressionBase<R> : ReactiveLinkedList<ExpressionMember<R>>, IExpression<R>
{
#if ODIN_INSPECTOR
    [ShowInInspector]
#endif
    public R Value => this.Invoke();

    public R Invoke() => this.Invoke(new Enumerator(this));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract R Invoke(Enumerator enumerator);

    protected ExpressionBase(int capacity = INITIAL_CAPACITY) : base(capacity) { }
    protected ExpressionBase(params ExpressionMember<R>[] members) : base(members) { }
    protected ExpressionBase(IEnumerable<ExpressionMember<R>> members) : base(members) { }
}
```

`Scripts/Expressions/ExpressionBase.cs`. Шаблонный метод: подкласс отдаёт только свёртку. `Enumerator` —
унаследованный `ReactiveLinkedList<T>.Enumerator` (struct), переопределять его не нужно.

```csharp
[Serializable]
#if ODIN_INSPECTOR
[InlineProperty]
#endif
public abstract class ExpressionBase<T, R> : ReactiveLinkedList<ExpressionMember<T, R>>, IExpression<T, R>
{
#if ODIN_INSPECTOR
    [Button]
#endif
    public R Invoke(T arg) => this.Invoke(new Enumerator(this), arg);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract R Invoke(Enumerator enumerator, T arg);

    protected ExpressionBase(int capacity = INITIAL_CAPACITY) : base(capacity) { }
    protected ExpressionBase(params ExpressionMember<T, R>[] members) : base(members) { }
    protected ExpressionBase(IEnumerable<ExpressionMember<T, R>> members) : base(members) { }
}
```

Арность 2 (`ExpressionBase<T1, T2, R>`) устроена идентично: `Invoke(T1 arg1, T2 arg2)` под `[Button]` и
`protected abstract R Invoke(Enumerator, T1, T2)`.

```csharp
[Serializable]
public class AndExpression : ExpressionBase<bool>, IPredicate
{
    public AndExpression(int capacity = INITIAL_CAPACITY) : base(capacity) { }
    public AndExpression(params ExpressionMember<bool>[] members) : base(members) { }
    public AndExpression(IEnumerable<ExpressionMember<bool>> members) : base(members) { }

    protected override bool Invoke(Enumerator enumerator)
    {
        while (enumerator.MoveNext())
            if (!enumerator.Current!.Invoke())
                return false;

        return true;
    }
}
```

`Scripts/Expressions/AndExpression.cs`. Пустое → `true`, короткое замыкание на первом `false`. Реализует
`IPredicate`, то есть кладётся туда, где ждут предикат, оставаясь мутируемым через `IExpression`.
`AndExpression<T> : ExpressionBase<T, bool>, IPredicate<T>` и
`AndExpression<T1, T2> : ExpressionBase<T1, T2, bool>, IPredicate<T1, T2>` — та же форма со `Invoke(arg)` /
`Invoke(arg1, arg2)`.

```csharp
[Serializable]
public class OrExpression : ExpressionBase<bool>, IPredicate
{
    public OrExpression(int capacity = INITIAL_CAPACITY) : base(capacity) { }
    public OrExpression(IEnumerable<ExpressionMember<bool>> members) : base(members) { }
    public OrExpression(params ExpressionMember<bool>[] members) : base(members) { }

    protected override bool Invoke(Enumerator enumerator)
    {
        while (enumerator.MoveNext())
            if (enumerator.Current!.Invoke())
                return true;

        return false;
    }
}
```

Пустое → `false`. Обратите внимание на порядок объявления конструкторов (`IEnumerable` раньше `params`) — у
негенерического `OrExpression` он отличается от `AndExpression`; на вызовы это не влияет, но выдаёт, что файлы
писались руками по арностям. Та же инверсия порядка — в `IntSumExpression<T>` и `IntSumExpression<T1, T2>`;
`IntMulExpression`, `FloatSumExpression`, `FloatMulExpression` и `AndExpression` держат `params` первым везде.

```csharp
[Serializable]
public class IntSumExpression : ExpressionBase<int>
{
    public IntSumExpression(int capacity = INITIAL_CAPACITY) : base(capacity) { }
    public IntSumExpression(params ExpressionMember<int>[] members) : base(members) { }
    public IntSumExpression(IEnumerable<ExpressionMember<int>> members) : base(members) { }

    protected override int Invoke(Enumerator enumerator)
    {
        int result = 0;
        while (enumerator.MoveNext())
            result += enumerator.Current!.Invoke();

        return result;
    }
}
```

Затравка `0`, пустое → `0`. Плоские бонусы: `+armour`, `+damage`, `+maxHealth`.

```csharp
[Serializable]
public class IntMulExpression : ExpressionBase<int>
{
    protected override int Invoke(Enumerator enumerator)
    {
        int result = 1;
        while (enumerator.MoveNext())
            result *= enumerator.Current!.Invoke();

        return result;
    }
}
```

Затравка `1`, пустое → `1`. Нейтральность единицы и есть причина, по которой мультипликативные выражения —
правильный дефолт для стеков множителей: пустой стек ничего не меняет. `FloatSumExpression` (затравка `0`) и
`FloatMulExpression` (затравка `1`) идентичны по форме для `float`; у каждого есть генерические варианты `<T>` и
`<T1, T2>` с теми же тремя конструкторами.

```csharp
[Serializable]
public class InlineExpression<R> : ExpressionBase<R>
{
    private readonly Func<Enumerator, R> function;

    public InlineExpression(Func<Enumerator, R> function, int capacity = INITIAL_CAPACITY) : base(capacity) =>
        this.function = function;

    public InlineExpression(Func<Enumerator, R> function, params ExpressionMember<R>[] array) : base(array) =>
        this.function = function;

    public InlineExpression(Func<Enumerator, R> function, IEnumerable<ExpressionMember<R>> enumerable) :
        base(enumerable) =>
        this.function = function;

    protected override R Invoke(Enumerator enumerator) => this.function.Invoke(enumerator);
}
```

`Scripts/Expressions/InlineExpression.cs`. Разовая свёртка без объявления типа: min, max, среднее, первое
ненулевое. Сигнатура свёртки совпадает с `override` в подклассе, поэтому «повышение» до именованного типа —
копипаст. `null` в конструкторе **не** проверяется (в отличие от `InlineFunction`).

```csharp
// арность 2 — ошибка в исходнике: нет ": base(capacity)"
public InlineExpression(Func<Enumerator, T1, T2, R> function, int capacity = INITIAL_CAPACITY) =>
    this.function = function;
```

Перегрузка арности 2 с `capacity` биндится к неявному `base()`, и переданная ёмкость отбрасывается — массив
остаётся на 4. У арностей 0 и 1 `: base(capacity)` на месте.

```csharp
// Scripts/Expressions/Extensions.cs — fluent Add, возвращают то же выражение
public static IExpression<R> Add<R>(this IExpression<R> it, object source, Func<R> func);
public static IExpression<R> Add<R>(this IExpression<R> it, object source, IFunction<R> func);
public static IExpression<R> Add<R>(this IExpression<R> it, IFunction<R> func);
public static IExpression<R> Add<R>(this IExpression<R> it, KeyValuePair<object, IFunction<R>> function);
public static IExpression<R> Add<R>(this IExpression<R> it, Func<R> func);

public static IExpression<T1, R> Add<T1, R>(this IExpression<T1, R> it, object source, Func<T1, R> func);
public static IExpression<T1, R> Add<T1, R>(this IExpression<T1, R> it, object source, IFunction<T1, R> func);
public static IExpression<T1, R> Add<T1, R>(this IExpression<T1, R> it, KeyValuePair<object, IFunction<T1, R>> function);
public static IExpression<T1, R> Add<T1, R>(this IExpression<T1, R> it, Func<T1, R> func);

public static IExpression<T1, T2, R> Add<T1, T2, R>(this IExpression<T1, T2, R> it, object source, Func<T1, T2, R> func);
public static IExpression<T1, T2, R> Add<T1, T2, R>(this IExpression<T1, T2, R> it, object source, IFunction<T1, T2, R> func);
public static IExpression<T1, T2, R> Add<T1, T2, R>(this IExpression<T1, T2, R> it, KeyValuePair<object, IFunction<T1, T2, R>> function);
public static IExpression<T1, T2, R> Add<T1, T2, R>(this IExpression<T1, T2, R> it, Func<T1, T2, R> func);
```

Это и есть реальный способ наполнить выражение из кода. Перегрузки без `source` кладут `null` в тег.
Перегрузки `IFunction` сохраняют `func.Invoke` — новый делегат, но с тем же target+method, поэтому удаление по
`IFunction` потом срабатывает. Заметьте: у арностей 1 и 2 **нет** перегрузки `Add(IFunction<…>)` без `source` —
передавайте `Add(null, func)`.

```csharp
public static bool Remove<R>(this IExpression<R> it, object source);
public static bool Remove<R>(this IExpression<R> it, Func<R> value);
public static bool Remove<R>(this IExpression<R> it, IFunction<R> value) => it.Remove(value.Invoke);

public static bool Remove<T, R>(this IExpression<T, R> it, object source);
public static bool Remove<R, T>(this IExpression<T, R> it, Func<T, R> value);                 // порядок R, T
public static bool Remove<T, R>(this IExpression<T, R> it, IFunction<T, R> value);

public static bool Remove<T1, T2, R>(this IExpression<T1, T2, R> it, object source);
public static bool Remove<R, T1, T2>(this IExpression<T1, T2, R> it, Func<T1, T2, R> value);  // порядок R, T1, T2
public static bool Remove<T1, T2, R>(this IExpression<T1, T2, R> it, IFunction<T1, T2, R> value);
```

Обе формы удаления идут линейным `foreach` по членам и выходят после первого совпадения. У двух перегрузок
порядок типовых параметров перевёрнут (`R` первым) — важно, если указываете их явно.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static IReadOnlyReactiveList<ExpressionMember<R>>.StateChangedSubscription SubscribeState<R>(
    this IExpression<R> it, Action<R> action
) => new(it, () => action.Invoke(it.Invoke()));
```

Подписка на изменение *состава* выражения: обработчик — это `() => action.Invoke(it.Invoke())`, то есть на каждый
`OnStateChanged` колбэк получает заново свёрнутое значение. Немедленного вызова при подписке нет: конструктор
`StateChangedSubscription` делает только `collection.OnStateChanged += handler`. Возвращаемый
`StateChangedSubscription` — это `readonly struct : IDisposable`, объявленный внутри `IReadOnlyReactiveList<T>`;
его надо диспозить. Это одно из семейства: в `Collections/Extensions.cs` есть ещё `SubscribeState` для
`IReadOnlyReactiveCollection<T>`, `IReadOnlyReactiveArray<T>` и `IReadOnlyReactiveDictionary<K, V>`.

```csharp
// Scripts/Expressions/Extensions.cs — свёртки над последовательностями, НЕ над выражениями
public static float Sum(this IEnumerable<float> list);
public static float Sum(this IEnumerable<int> list);       // возвращает float, итерирует как float
public static float Multiply(this IEnumerable<float> list);
public static float Multiply(this IEnumerable<int> list);  // то же
public static bool  And(this IEnumerable<bool> list);      // корректен
public static bool  Or(this IEnumerable<bool> list);       // СЛОМАН, см. «Грабли»
```

Все шесть кидают `ArgumentNullException` на `null`. Это утилиты над `IEnumerable`, они не имеют отношения к
`IExpression` и к выражению применяются только через его `IEnumerable`-грань (с боксингом энумератора).

### Хранилище, которое наследуют все выражения

```csharp
public partial class ReactiveLinkedList<T> : IReactiveList<T>, IDisposable
{
    private static readonly IEqualityComparer<T> s_comparer = EqualityComparer<T>.Default;
    private const int UNDEFINED_INDEX = -1;
    protected const int INITIAL_CAPACITY = 4;

    public event Action OnStateChanged;
    public event Action<int, T> OnItemChanged;
    public event Action<int, T> OnItemAdded;
    public event Action<int, T> OnItemRemoved;

    private struct Node { public T item; public int next; }

    private Node[] _nodes;
    private int _head, _tail, _count, _freeList;

    public int Count => _count;
    public bool IsReadOnly => false;

    public ReactiveLinkedList(int capacity = INITIAL_CAPACITY)
    {
        _nodes = new Node[Math.Max(capacity, INITIAL_CAPACITY)];
        _head = _tail = UNDEFINED_INDEX;
        _count = 0;
        _freeList = UNDEFINED_INDEX;
    }

    public Enumerator GetEnumerator() => new(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<T>
    {
        private readonly ReactiveLinkedList<T> _list;
        private int _index;
        private T _current;

        public T Current => _current;
        object IEnumerator.Current => _current;

        public bool MoveNext()
        {
            if (_index == UNDEFINED_INDEX) return false;
            ref Node node = ref _list._nodes[_index];
            _current = node.item;
            _index = node.next;
            return true;
        }

        public void Reset();
        public void Dispose() { }
    }
}
```

`Scripts/Collections/List/ReactiveLinkedList.cs` — формально чужая область (глава про коллекции), но без неё
выражения не объяснить. Узлы живут в одном `Node[]`, связаны `int`-индексами, освобождённые индексы
переиспользуются через `_freeList`. Публичные члены, доступные на любом выражении: `Add`, `AddRange`, `Insert`,
`Remove`, `RemoveAt`, `IndexOf`, `Contains`, `Clear`, `CopyTo` (две перегрузки), индексатор `this[int]`, `Count`,
`IsReadOnly`, `Dispose`.

```csharp
public void Add(T item)
{
    if (item == null) return;        // для struct-члена условие константно-ложно
    // … вставка в хвост …
    _count++;
    OnItemAdded?.Invoke(_count - 1, item);
    onItemAdded?.Invoke(item);
    OnStateChanged?.Invoke();
}

public void AddRange(IEnumerable<T> items)
{
    if (items == null) throw new ArgumentNullException(nameof(items));
    foreach (var item in items) Add(item);
}
```

`AddRange` — просто `foreach` по `Add`, то есть N уведомлений, а не одно. Если на выражение подписан UI через
`SubscribeState`, массовая загрузка модификаторов установщиком вызовет его N раз.

---

## Идиомы

### 1. Спрятать источник данных за `InlineFunction` на этапе установки

```csharp
public class PhysicsTransformInstaller : MonoEntityInstaller
{
    [SerializeField]
    private Rigidbody _rigidbody;

    public override void Install(IEntity entity)
    {
        entity.AddPosition(new InlineFunction<Vector3>(() => _rigidbody.position));
        entity.AddRotation(new InlineFunction<Quaternion>(() => _rigidbody.rotation));
    }
}

public class KinematicTransformInstaller : MonoEntityInstaller
{
    [SerializeField]
    private Transform _transform;

    public override void Install(IEntity entity)
    {
        entity.AddPosition(new InlineFunction<Vector3>(() => _transform.position));
        entity.AddRotation(new InlineFunction<Quaternion>(() => _transform.rotation));
    }
}

// Читающая сторона не знает, откуда взялись координаты
IFunction<Vector3> position = entity.GetPosition();
IFunction<Quaternion> rotation = entity.GetRotation();
```

Почему это правильная форма: потребитель зависит от `IFunction<Vector3>`, а не от `Rigidbody` или `Transform`.
Два установщика, различающиеся только лямбдой, дают физического и кинематического персонажа без единого `if` в
геймплейном коде, а тест ставит `new InlineFunction<Vector3>(() => Vector3.zero)`. Замыкание захватывает поле
`this`, поэтому переназначение сериализованной ссылки делегат не ломает.

Обратите внимание на `override`. Базовый класс объявляет `public abstract void Install(IEntity entity)`
(`Entities/Scripts/Installers/MonoEntityInstaller.cs:33`), так что `override` обязателен. Пример в
`UsingInlineFunctions.md` написан как `public void Install(IEntity entity)` — такой класс не соберётся вообще
(абстрактный член не реализован). `AddPosition`/`GetPosition` — генерируемые расширения (глава про
`SourceGenerators`); сама Elements-часть от них не зависит.

**Анти-паттерн из доков — ООП вместо абстракции:**

```csharp
// Потребитель теперь знает про физику
public class PositionProvider : MonoBehaviour
{
    [SerializeField] private Rigidbody _rigidbody;
    public Vector3 GetPosition() => _rigidbody.position;
}
// каждый читатель делает GetComponent<PositionProvider>(), юнит-тест невозможен,
// а переезд на Transform требует второго интерфейса и второго класса
```

### 2. Наполнять выражение через fluent `Add` (единственная форма, которая компилируется)

```csharp
var expression = new FloatSumExpression();
var source = new object();

expression
    .Add(source, (Func<float>)(() => 1f))
    .Add(new FunctionStub(2f))      // FunctionStub : IFunction<float>
    .Add(() => 3f);

Assert.AreEqual(3, expression.Count);
Assert.AreEqual(6f, expression.Value);
```

Это дословно `ExpressionExtensionsTests.Add_MultipleOverloads_CanBeChainedFluently` —
единственный живой фикстур в модуле, то есть достоверная форма API. `Add` сам строит `ExpressionMember<R>` (ту
часть, которую доки не показывают нигде) и возвращает то же выражение, поэтому установка читается одной
декларативной цепочкой. Тег `source` — то, что потом сделает удаление возможным.

Тот же фикстур в другом тесте пишет двухаргументную форму **без** приведения:
`expression.Add(source1, () => 5f);`. Приведение `(Func<float>)` — страховка от неоднозначности перегрузок,
а не обязательное требование; ставьте его, когда компилятор жалуется.

**Анти-паттерн:**

```csharp
// Не компилируется: нет неявного Func<float> → ExpressionMember<float>
var expression = new FloatSumExpression(() => 1f, () => 2f, () => 3f);

// Компилируется, но это ровно то, что расширение и прячет:
expression.Add(new ExpressionMember<float>(source, () => 1f));
```

### 3. Удалять модификатор по тегу источника, а не по делегату

```csharp
var source1 = new object();
var source2 = new object();
var expression = new FloatSumExpression();
expression.Add(source1, () => 5f);
expression.Add(source2, () => 10f);

bool removed = expression.Remove(source1);

Assert.IsTrue(removed);
Assert.AreEqual(1, expression.Count);
Assert.AreEqual(10f, expression.Value);
```

Бафф добавляет свой вклад с тегом `this` и снимает его через `expression.Remove(this)` — лямбду нигде хранить не
надо. Ровно для этого `ExpressionMember.Source` и существует. Удаление по делегату тоже работает, но только если
вы сохранили тот же экземпляр:

```csharp
Func<float> func1 = () => 5f;
var expression = new FloatSumExpression();
expression.Add(func1);
// …
expression.Remove(func1);   // true
```

**Анти-паттерн:**

```csharp
expression.Add(() => 5f);
expression.Remove(() => 5f);   // false: это другой экземпляр делегата, ничего не удалено
```

### 4. Своё выражение — только переопределённая свёртка

```csharp
[Serializable]
public sealed class MinFloatExpression : ExpressionBase<float>
{
    public MinFloatExpression(int capacity = INITIAL_CAPACITY) : base(capacity) { }
    public MinFloatExpression(params ExpressionMember<float>[] members) : base(members) { }
    public MinFloatExpression(IEnumerable<ExpressionMember<float>> members) : base(members) { }

    protected override float Invoke(Enumerator enumerator)
    {
        float result = float.PositiveInfinity;
        while (enumerator.MoveNext())
        {
            float value = enumerator.Current.Invoke();
            if (value < result)
                result = value;
        }

        return result;
    }
}
```

Весь контракт здесь: проброс трёх конструкторов и один метод — свёртка над struct-энумератором. Ни хранения, ни
событий, ни реактивности переписывать не нужно, `ExpressionBase` уже *является* `ReactiveLinkedList`. Свёртка может
выходить обычным `return` (короткое замыкание), и поскольку `Enumerator` — struct, передаваемый по значению, всё
вычисление не аллоцирует. Выбирайте затравку так, чтобы пустое выражение было нейтральным для вашей операции, —
`PositiveInfinity` для минимума, `0` для суммы, `1` для произведения.

Рецепт из доков (`ExpressionsBase.md`) показывает `public AndExpression(params Func<bool>[] members)` — это не
компилируется; параметр реального конструктора `params ExpressionMember<bool>[]`.

**Анти-паттерн — переписать хранилище:**

```csharp
// Заново реализует то, чем базовый класс уже владеет,
// и полностью теряет реактивность (OnItemAdded / OnStateChanged):
public sealed class MyAndCondition
{
    private readonly List<Func<bool>> _conditions = new();

    public bool Invoke()
    {
        foreach (var c in _conditions)
            if (!c()) return false;
        return true;
    }
}
```

### 5. Разовая свёртка через `InlineExpression`

```csharp
var expression = new InlineExpression<int>(enumerator =>
{
    int sum = 0;
    while (enumerator.MoveNext())
        sum += enumerator.Current.Invoke();
    return sum;
});

expression.Add(() => 1);
expression.Add(() => 2);
expression.Add(() => 3);

int sum = expression.Invoke();   // 6
```

Новое правило агрегации без объявления типа — удобно в тестах и в единственном месте использования.
Параметризованные формы принимают аргумент рядом с энумератором:
`new InlineExpression<int, int>((enumerator, x) => { … enumerator.Current.Invoke(x) … })`.

**Анти-паттерн:**

```csharp
// Это буквально IntSumExpression, только с лишним полем-делегатом и без имени:
var sum = new InlineExpression<int>(e => { int s = 0; while (e.MoveNext()) s += e.Current.Invoke(); return s; });
// надо: var sum = new IntSumExpression();
```

### 6. Запирать выражение разделяемой константой

```csharp
IExpression<bool> condition = new AndExpression()
    .Add(this, () => _health.Value > 0)
    .Add(this, () => _ammo.Value > 0);

// Полностью заблокировать условие
condition.Add(Constants.False);

// Снять блокировку
condition.Remove(Constants.False);
```

`Constants.False` — `static readonly Const<bool>`, один экземпляр на процесс. Блокировка и разблокировка не
аллоцируют и не создают замыканий. `Const<T>` реализует `IValue<T>`, который расширяет `IFunction<T>`, поэтому
вызов биндится к перегрузке `Add<R>(this IExpression<R>, IFunction<R>)`; а поскольку это **класс** и каждый раз
та же самая ссылка, `Remove` его действительно находит (`Remove(IFunction<R>)` сводится к
`Remove(value.Invoke)`, а равенство делегатов сравнивает target и method). Симметрично: `Constants.True` —
нейтральная заглушка для `OrExpression`.

`UsingConstantsWithAndExpressions.md` даёт этот же пример, но с нерабочей первой строкой
(`new AndExpression(cond1, cond2, cond3)`) — конструктор так не принимает. Берите fluent-форму выше.

**Анти-паттерн:**

```csharp
condition.Add(() => false);
// …позже…
condition.Remove(() => false);   // no-op: другое замыкание, другой экземпляр делегата
```

### 7. `Negate` — один раз на установке, `InvokeNot` — на каждый вызов

```csharp
// без аллокаций, по месту
bool dead  = isAlive.InvokeNot();
bool ally  = isEnemy.InvokeNot(otherCharacter);

// аллоцирует InlineFunction + замыкание: делать на установке, результат сохранить
InlineFunction<bool> isDead = isAlive.Negate();
InlineFunction<Character, bool> isAlly = isEnemy.Negate();
```

`InvokeNot` — это `=> !it.Invoke()` с `AggressiveInlining`, он стоит ровно ничего и читается лучше, чем
`!cond.Invoke()` посреди длинного булева выражения. `Negate` отдаёт переиспользуемый объект, который можно
передать другой системе, ценой одного объекта плюс одного замыкания — его место в `Install`, а не в `Update`.
Обратите внимание на тип переменной: `Negate` возвращает `InlineFunction<…>`, а не `IFunction<…>`;
присвоить в `IFunction<Character, bool>` можно, но `IValue<bool>` — нельзя, `InlineFunction` его не реализует.

**Анти-паттерн:**

```csharp
void Update()
{
    if (_isAlive.Negate().Invoke()) { … }   // новый InlineFunction и замыкание каждый кадр
}
```

### 8. Именованный предикат как `sealed` класс с `readonly` состоянием

```csharp
public sealed class IsEnemyFunction : IFunction<Character, bool>
{
    private readonly Character _source;

    public IsEnemyFunction(Character source) => _source = source;

    public bool Invoke(Character other) => _source.Team != other.Team;
}

public sealed class AreAlliesPredicate : IPredicate<Character, Character>
{
    public bool Invoke(Character a, Character b) => a.Team == b.Team;
}
```

Форма, которую доки повторяют во всех примерах: `sealed`, одно `readonly` поле через конструктор,
`Invoke` в одну строку, без наследования и без зависимости от Unity. `sealed` даёт JIT шанс девиртуализовать
вызов, `readonly`-состояние делает функцию безопасной для шаринга между системами и тривиально тестируемой.
`AreAlliesPredicate` — stateless-случай: полей нет, значит один экземпляр можно закэшировать навсегда.
Именованный класс — когда у условия есть имя в дизайн-документе; `InlinePredicate` — когда нет.

**Анти-паттерн:**

```csharp
public class EnemyChecker : MonoBehaviour
{
    public Character source;              // публичное мутируемое поле, никакой инъекции
    public bool IsEnemy(Character other)  // не IFunction, значит ничем не композируется
        => source.Team != other.Team;
}
```

### 9. Дизайнерское дерево условий — на `AndPredicate`/`OrPredicate`/`NotPredicate`

```csharp
public sealed class DoorBehaviour : MonoBehaviour
{
    [SerializeReference]
    private IPredicate _canOpen = new AndPredicate();

    private void TryOpen()
    {
        if (_canOpen.Invoke())
            Open();
    }
}
```

`[SerializeReference]` на поле типа `IPredicate` даёт полиморфную правку в инспекторе: дизайнер складывает
`AndPredicate`, внутри него — свой массив `IPredicate[]`, в нём `NotPredicate` и ваши собственные предикаты.
Берите это семейство, когда условия живут в префабе; `And`/`OrExpression` — когда состав меняется в рантайме.
Форма цикла внутри `AndPredicate` (индексный `for` с вынесенной длиной, без энумератора, с коротким замыканием и
null-гардом, возвращающим нейтральный элемент операции) — образец для собственных массивных композитов.

**Анти-паттерн:**

```csharp
// LINQ по массиву интерфейсов: энумератор и замыкание на каждое вычисление
public bool Invoke() => conditions.All(c => c.Invoke());
```

### 10. Подписка UI на изменение состава

```csharp
var expression = new FloatSumExpression();
var subscription = expression.SubscribeState(v => _label.text = v.ToString("0.##"));

try
{
    expression.Add(() => 10f);   // колбэк уже вызван, v == 10f
}
finally
{
    subscription.Dispose();
}
```

Так это делает `ExpressionExtensionsTests.SubscribeState_FiresOnStateChangeWithCurrentValue`. Держите подписку в
`var` или в поле точного типа: `StateChangedSubscription` — `readonly struct`, и присваивание в поле типа
`IDisposable` его забоксит. Событие стреляет на изменение **состава** (`Add`, `Remove`, `Clear`), а не на изменение
значения внутри члена, — это не универсальный value-changed хук.

---

## Производительность

В репозитории есть **один** набор реальных замеров, релевантный этой главе:
`Docs/Elements/Performance/ReactiveLinkedListPerformance.md`, с фикстурой
`Assets/Plugins/Atomic/Elements/Tests/Performance/ReactiveLinkedListPerformance.cs`. Замеры сделаны на MacBook
Apple M1 для коллекций из 1000 элементов типа `object`; медианы в микросекундах. Поскольку каждое выражение
*является* `ReactiveLinkedList<ExpressionMember<R>>`, эти числа — его числа.

| Операция   | List  | ReactiveList | ReactiveLinkedList |
|------------|-------|--------------|--------------------|
| Add        | 30.15 | 31.20        | 60.00              |
| Clear      | 0.40  | 1.20         | 2.65               |
| Contains   | 1821.35 | 33455.75   | 33605.80           |
| CopyTo     | 0.40  | 0.40         | 30.65              |
| Enumerator | 29.35 | 28.80        | 28.50              |
| For        | 1.70  | 1.70         | 1273.55            |
| Get        | 1.50  | 1.75         | 1277.70            |
| Set        | 30.40 | 42.00        | 1304.00            |
| Remove     | 307.40 | 254.25      | 43.50              |
| Remove At  | 29.55 | 3.00         | 2546.10            |
| Insert At  | 242.85 | 245.85      | 60.80              |

Читать это так (**измерено**): `foreach` по связному списку не медленнее, чем по `List` (28.5 против 29.35);
индексный доступ медленнее в ~750 раз (`Get` 1277.7 против 1.5); `RemoveAt(int)` — худшая операция в таблице
(2546.1); `Remove(item)` наоборот лучшая (43.5 против 307.4 у `List`); `Contains` стоит 33.6 мс. Сам вывод в доке
сформулирован коротко: «Always use `foreach` for iteration; avoid `for` loops».

Оговорка к методике (**прочитано в фикстуре**): тест `Contains` делает 1000 обращений `list.Contains(absent)` по
отсутствующему элементу, то есть 10⁶ полных сравнений; тест `Add` вставляет 1000 элементов за одно измерение.
Числа — это стоимость цикла из 1000 операций, не одной. И вся фикстура обёрнута в `#if UNITY_6000`: на Unity
2022.3 она не собирается, воспроизвести замеры на проектной версии редактора без правки директивы не получится.

Остальные факты — прямо из исходников (**прочитано**) с пометкой, где начинается вывод (**предположение**).

**Struct-энумератор, свёртка без аллокаций.** `public struct Enumerator : IEnumerator<T>` возвращается из
конкретного `public Enumerator GetEnumerator() => new(this);` (`ReactiveLinkedList.cs:412, 421`).
`ExpressionBase.Invoke()` — это `this.Invoke(new Enumerator(this))`, и свёртка получает его **по значению**,
поэтому вычисление выражения не аллоцирует. `MoveNext` берёт `ref Node node = ref _list._nodes[_index];`, то есть
не копирует узел.

**Но `foreach` через интерфейс боксит.** `IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();` —
явная реализация, которая упаковывает struct-энумератор. `foreach` по переменной типа `IExpression<R>` или
`IEnumerable<T>` боксит, по переменной конкретного типа — нет. То же касается `object IEnumerator.Current =>
_current;` — через негенерический интерфейс боксится и сам элемент.

**Массив узлов и free-list, без объектов на узел.** `private struct Node { public T item; public int next; }` плюс
`private Node[] _nodes; private int _head, _tail, _count, _freeList;`. Члены лежат в одном массиве и связаны
`int`-индексами: локальность как у массива, O(1) вставка/удаление, переиспользование индексов. На узел не
аллоцируется объект, поэтому добавление и снятие модификаторов каждые несколько секунд не создаёт давления на GC
(**предположение** в части GC — замеров аллокаций в репозитории нет).

**Рост — удвоение, сжатия нет.** `AllocateNode` делает `Array.Resize(ref _nodes, _nodes.Length * 2)` только когда
`_count >= _nodes.Length` и free-list пуст; освобождённые индексы берутся первыми. Ёмкость никогда не уменьшается,
а `new Node[Math.Max(capacity, INITIAL_CAPACITY)]` задаёт пол в 4 элемента. `INITIAL_CAPACITY` — `protected const
int = 4`. Передавать реальную ёмкость (`new FloatMulExpression(16)`) имеет смысл для длинных стеков модификаторов;
значение меньше 4 игнорируется.

**Индексный доступ обходит цепочку.** `GetNodeIndex(int index)` идёт от `_head` шагами `_nodes[current].next`
(`ReactiveLinkedList.cs:480`). На нём держатся `this[int]`, `Insert(int, T)` и `RemoveAt(int)`. Хуже того,
`RemoveAt(index)` вызывает `GetNodeIndex` **дважды** — для `index` и (при `index != 0`) для `index - 1`, — поэтому
это два обхода;
отсюда 2546 мкс в таблице. `for (int i = 0; i < expr.Count; i++) expr[i]` — это O(n²).

**Удаление члена боксит, и сканирует дважды.** `s_comparer` — это
`private static readonly IEqualityComparer<T> s_comparer = EqualityComparer<T>.Default;`, и на нём работают
`Remove(T)`, `IndexOf(T)`, `Contains(T)` и сеттер индексатора. `ExpressionMember<R>` — struct **без** переопределённого
`Equals` и **без** `IEquatable<ExpressionMember<R>>`, поэтому `EqualityComparer<T>.Default` разрешается в
`ObjectEqualityComparer`, то есть в `ValueType.Equals` с упаковкой обоих операндов на каждое сравнение
(**предположение** по правилам BCL, выведенное из отсутствия `IEquatable` в `ExpressionMember.cs`). Сверху —
двойной проход: `Extensions.Remove<R>(IExpression<R>, object source)` и `Remove<R>(IExpression<R>, Func<R>)` сами
идут `foreach` по списку, находят член и вызывают `it.Remove(pair)`, который проходит список **второй раз** с
теми же боксящими сравнениями. Удаление члена — это два O(n)-прохода плюс O(n) боксов, а не один дешёвый поиск.
Практический вывод: `Remove` на установке и на событиях — нормально, `Remove` в `Update` — нет.

**`AggressiveInlining` расставлен неравномерно.** Все методы в `Functions/Extensions.cs` и
`Expressions/Extensions.cs` атрибутированы; `AllocateNode`, `GetNodeIndex`, `RemoveNode` — тоже;
`AndPredicate.Invoke` — да, `OrPredicate.Invoke` и `NotPredicate.Invoke` — нет. Отдельно:
`[MethodImpl(AggressiveInlining)]` на `protected abstract R Invoke(Enumerator)` в `ExpressionBase` **бесполезен** —
атрибут стоит на абстрактном объявлении, а не на конкретных `override` в `And`/`Or`/`IntSum`/`IntMul`/`FloatSum`/
`FloatMul`/`InlineExpression`, ни один из которых его не несёт. Виртуальный вызов свёртки остаётся.

**Короткое замыкание реально.** `AndExpression` выходит на первом `false`, `OrExpression` — на первом `true`,
`AndPredicate`/`OrPredicate` — так же. Порядок членов *и есть* порядок вычисления (порядок вставки в список),
поэтому дешёвые проверки добавляйте первыми.

**`ExpressionMember<R>` — struct из двух ссылочных полей.** `object _source` и `Func<R> _func`: копия по значению
внутри `MoveNext` (`_current = node.item`) стоит 16 байт на 64-битной платформе и не трогает куче.
`readonly` на `Invoke`/`EqualsFunction` исключает защитную копию при вызове через `ref`/`in`.

**Боксинг тега.** `ExpressionMember.Source` объявлен как `object`. Тегирование значимым типом (int-id, enum,
маленький struct-handle) боксит на каждом `Add`, а `Remove(object source)` потом сравнивает `pair.Source == source`
— это сравнение ссылок, и для двух разных боксов оно никогда не истинно. Тегируйте ссылочным типом: установщиком,
компонентом, экземпляром эффекта (`this`).

**Негация: аллоцирующая и нет.** `InvokeNot(…)` — `=> !it.Invoke()`, ноль аллокаций, инлайнится.
`Negate(…)` — `=> new(() => !it.Invoke())`, то есть один `InlineFunction` плюс одно замыкание, захватывающее `it`,
на каждый вызов.

**`Add`/`Remove` над `ICollection<Func<…>>` аллоцируют делегат.** `it.Add(member.Invoke)` — это преобразование
группы методов, то есть новый объект делегата на каждый вызов. На установке нормально, в `Update` — аллокация
каждый кадр.

**`Value` и `Invoke()` не кэшируются.** `ExpressionBase<R>.Value => this.Invoke()` сворачивает весь список на
каждом чтении, и под `ODIN_INSPECTOR` `Value` помечен `[ShowInInspector]` — редактор перевычисляет выражение на
каждой перерисовке. Для покадрового чтения дорогого выражения вызовите один раз в локальную переменную.

**Выражения диспозабельны.** `ReactiveLinkedList<T> : IReactiveList<T>, IDisposable`; `Dispose()` вызывает
`Clear()` и обнуляет все обработчики. Выражение, которое переживает свои источники или на которое подписались
через `SubscribeState`, надо диспозить — иначе замыкания членов держат захваченные объекты живыми.

**Сериализация выражения стоит аллокаций и ничего не сохраняет.** Партиал
`#if UNITY_5_3_OR_NEWER [Serializable] public partial class ReactiveLinkedList<T> : ISerializationCallbackReceiver`
держит `[SerializeField] internal T[] serializedItems` и в `OnBeforeSerialize` делает `this.serializedItems =
this.ToArray();`. `ToArray` здесь — LINQ (своего в классе нет), то есть боксинг struct-энумератора плюс аллокация
массива на каждую сериализацию. При этом `T` для выражения — `ExpressionMember<R>`, внутри которого `Func<R>`;
Unity делегаты не сериализует, поэтому после `OnAfterDeserialize` члены вернутся с `_func == null`
(**предположение**: прямого теста на это в репозитории нет, но `Add(T)` для struct-члена не отфильтрует такой
элемент, и `Invoke()` на нём упадёт `NullReferenceException`). Вывод: не держите выражение как
сериализуемое поле ради сохранения его состава — наполняйте его в `Install`.

**Пол цены — одно обращение к делегату на член.** Выражение из N членов стоит один виртуальный вызов свёртки плюс
N вызовов `Func`. Рукописное `a && b && c` инлайнится и строго быстрее. Выражения покупают рантайм-композируемость,
а не скорость: применяйте их там, где состав меняется, и обычный код (или массивный цикл `AndPredicate`) там, где
не меняется.

---

## Грабли

- **Доки врут про тип элемента.** Все страницы `Docs/Elements/Expressions` говорят `IList<Func<R>>`,
  `ReactiveLinkedList<Func<R>>`, `params Func<bool>[] members`, `foreach (Func<bool> func in fireCondition)`.
  Сверено: `IExpression.md:107` — `public interface IExpression<R> : IList<Func<R>>, IValue<R>, IFunction<R>`;
  `ExpressionBase.md:130` — `public abstract class ExpressionBase<R> : ReactiveLinkedList<Func<R>>, IExpression<R>`.
  В исходниках это `ExpressionMember<R>`. Читайте каждый `Func<R>` в доках как `ExpressionMember<R>`.

- **Конструкторные примеры из доков не компилируются.** `new AndExpression(() => true, () => false)`,
  `new IntSumExpression(() => 2, () => 3)`, `new FloatMulExpression(() => a, () => b)` требуют неявного
  `Func<R>` → `ExpressionMember<R>`, которого нет. Подтверждение из самого репозитория:
  `AndExpressionTests.cs` (342 строки), `OrExpressionTests.cs` (340), `IntSumExpressionTests.cs` (323),
  `IntMulExpressionTests.cs` (308), `FloatSumExpressionTests.cs` (322), `FloatMulExpressionTests.cs` (308)
  **закомментированы целиком** — ноль некомментарных непустых строк в каждом. Компилируется только
  `ExpressionExtensionsTests.cs`, который работает через fluent `Add`.

- **`Extensions.Or(this IEnumerable<bool>)` инвертирован.** Тело: `foreach (bool item in list) if (!item) return
  true; return false;` — возвращает `true`, когда нашёл `false`. И это **зафиксировано в живом фикстуре**:
  `ExpressionExtensionsTests` содержит `Or_AllTrue_ReturnsFalse`, `Or_AllFalse_ReturnsTrue`,
  `Or_SingleTrue_ReturnsFalse`, `Or_SingleFalse_ReturnsTrue`. Если будете править баг в форке — придётся править и
  четыре зелёных теста. `And(…)` корректен и корректно покрыт.

- **`InlineExpression<T1, T2, R>(…, int capacity)` теряет ёмкость.** Нет `: base(capacity)`, биндится неявный
  `base()`, массив остаётся на 4. Арности 0 и 1 — в порядке.

- **`Extensions.Sum(this IEnumerable<int>)` возвращает `float`** и внутри итерирует `foreach (float item in
  list)`. То же у `Multiply(IEnumerable<int>)`. И возвращаемый тип, и тип итерации неверны для суммы `int`;
  не пользуйтесь ими.

- **`Remove` для `ICollection<Func<T, R>>` отсутствует.** Все три перегрузки `Add` есть, `Remove` — только для
  арностей 0 и 2. Симметричное удаление функции арности 1 из коллекции делегатов даст ошибку компиляции.

- **`Invert()` не существует.** `ExtensionsInvert.md` документирует `Invert`/`Invert<T>`/`Invert<T1,T2>`;
  в исходнике метод называется `Negate` (плюс `InvokeNot` для бесплатной формы). Коварство в том, что сам
  исходник помечает блок `#region Invert`, а XML-доки говорят «inverse», — поиск по `Invert` находит регион и
  вводит в заблуждение. Вызываемы только `Negate` и `InvokeNot`.

- **`InlineFunction<R>` не реализует `IValue<R>`.** `InlineFunction.md:46` утверждает
  `: IFunction<R>, IValue<R>`; исходник объявляет только `IFunction<R>` и просто имеет свойство `Value`.
  `IValue<R> v = new InlineFunction<Vector3>(…)` не скомпилируется.

- **Семантика пустого набора разная у каждого оператора.** `AndExpression`/`AndPredicate` → `true`;
  `OrExpression`/`OrPredicate` → `false`; `NotPredicate` с незаполненным слотом → **`false`**, а не `true`;
  `IntSum`/`FloatSum` → `0`; `IntMul`/`FloatMul` → `1`. Дизайнер, оставивший пустой `[SerializeReference]`,
  получит разный результат в зависимости от типа узла.

- **`OrExpression.md:205-213` описывает `Value` словами AND**: «returns `true` if all functions return `true`.
  If no functions are present, returns `true` by default». Исходник — настоящий OR, пустое даёт `false`. У
  параметризованных страниц `Value` нет, но AND-семантика продублирована в описании `Invoke`:
  ``OrExpression`1.md:211-214`` и ``OrExpression`2.md:214-219`` обещают «Returns `false` immediately if any function
  evaluates to `false`» и «The aggregated logical AND result».

- **`Contains`/`Remove`/`IndexOf` держатся на тождестве делегата.** `ExpressionMember.EqualsFunction` — это
  `_func == func`. Свежезаписанная лямбда никогда не равна ранее добавленной. Доки сами это помечают в
  `IExpression.md:86` и `IExpressions.md:44`:
  `bool contains = expression.Contains(testFunc); // might be false if reference differs`.
  Храните `Func<R>` в поле либо тегируйте `Source` и удаляйте через `Remove(object source)`.

- **`RemoveAt(isCooldown)` в доках.** Встречается в `Elements/Expressions/Manual.md:56`,
  `AndExpression.md:39` и `AndExpressions.md:49`. `RemoveAt` принимает `int`; подразумевался
  `Remove(isCooldown)`. Другие дефекты сниппетов в той же области: `var attackExpression = new
  AndExpression<GameObject>()` без точки с запятой (`AndExpressions.md:67`, ``AndExpression`1.md:81``);
  `int result = attackExpression.Invoke();` на булевом выражении (`AndExpressions.md:77`,
  ``AndExpression`1.md:91``, `IExpressions.md:72`); `int result = sumExpression.Invoke(3, 5);` с необъявленной
  переменной (`IExpressions.md:87`, ``IExpression`2.md:78``). Не копируйте сниппеты из доков, не скомпилировав.

- **`IPredicates.md:67` называет типы `IPredicate<R>`/`IPredicate<T, R>`** — опечатка документации, никакого `R`
  у предикатов нет.

- **Индексный доступ к выражению — O(n)**, и `RemoveAt` делает два обхода. См. таблицу замеров выше.

- **Тег значимого типа ломает удаление.** `Source` типизирован как `object`: struct-тег боксится на каждом `Add`,
  а `Remove(object source)` сравнивает ссылки и никогда не совпадёт.

- **`InlineExpression` не проверяет свёртку на `null`** (в отличие от `InlineFunction`, который кидает
  `ArgumentNullException` в конструкторе). `null`-свёртка проявится как `NullReferenceException` при первом
  `Invoke()`, далеко от места создания.

- **`AndPredicate`/`OrPredicate` не проверяют элементы массива на `null`** — незаполненный слот
  `[SerializeReference]` даст `NullReferenceException` внутри `Invoke`. `NotPredicate` свой единственный слот
  проверяет. И у `NotPredicate` вообще нет конструкторов — построить его из кода нельзя.

- **У `InlinePredicate`/`InlinePredicate<T>`/`InlinePredicate<T1,T2>` нет своего `[Serializable]`.** Атрибуты в C#
  не наследуются, хотя базовый `InlineFunction` их несёт.

- **`ToString()` у любого `InlineFunction` возвращает `func.Method.Name`** — для лямбды это
  сгенерированное компилятором имя вида `<Install>b__0_1`. Не показывайте его пользователю и не логируйте как
  имя условия.

- **Реальная ошибка в `ReactiveLinkedList.cs:48-52`.** Явный аксессор
  `event Action<T> IReadOnlyReactiveCollection<T>.OnItemAdded { add => this.onItemAdded += value; remove =>
  this.onItemRemoved -= value; }` отписывает **не от того поля**. Отписка обработчика «элемент добавлен» через
  интерфейс `IReadOnlyReactiveCollection<T>` молча портит список обработчиков «элемент удалён». Выражения это
  наследуют. Обходной путь: подписывайтесь на публичные `OnItemAdded`/`OnItemRemoved` (`Action<int, T>`) класса,
  а не на одноимённые члены `IReadOnlyReactiveCollection<T>`.

- **`Add`/`Insert` молча отбрасывают `null`.** `if (item == null) return;` — без исключения. Для
  `ExpressionMember<R>` (struct) проверка константно-ложна и безвредна, но `ReactiveLinkedList` ссылочных типов
  молча игнорирует `null`, и это стоит знать всем, кто копирует эту модель хранения под class-типизированный член.

- **`AddRange` шлёт N уведомлений, а не одно**: это `foreach` по `Add`, каждый вызов поднимает `OnItemAdded`,
  `onItemAdded` и `OnStateChanged`. `Clear` поднимает `OnItemRemoved` на каждый узел плюс один `OnStateChanged`
  в конце (на этом держится тест `SubscribeState_FiresOnClear`). Отдельная тонкость: в `Clear` первым аргументом
  в `OnItemRemoved?.Invoke(current, removed)` уходит **внутренний индекс узла в массиве**, а не позиция в списке.

- **`SubscribeState` надо диспозить**, и он реагирует только на изменение состава, не на изменение значения
  внутри члена. Возвращаемый тип — `IReadOnlyReactiveList<ExpressionMember<R>>.StateChangedSubscription`,
  `readonly struct`; хранение его в поле типа `IDisposable` боксит.

- **Перевёрнутый порядок типовых параметров** у `Remove<R, T>(this IExpression<T, R>, Func<T, R>)` и
  `Remove<R, T1, T2>(this IExpression<T1, T2, R>, Func<T1, T2, R>)`. Важно только при явном указании — но тогда
  критично.

- **У арностей 1 и 2 нет `Value`.** `IExpression<T, R>` и `IExpression<T1, T2, R>` наследуют только
  `IFunction<…>`. Старый код, читавший `.Value` у параметризованного выражения, переходит на `.Invoke(arg)`.

- **`AndPredicate`, `OrPredicate` и `NotPredicate` не задокументированы вообще.** Поиск по всему `Docs/` не
  находит ни одного упоминания. Единственный источник правды по ним — исходники.

---

## Где читать

Пути — относительно `C:/!!!Work/_AtomicUpstream`.

### Исходники (приоритетный источник; доки по этой области устарели)

| Файл | Что в нём |
|---|---|
| `Assets/Plugins/Atomic/Elements/Scripts/Functions/IFunction.cs` | Три интерфейса `IFunction` с вариантностью и XML-доками. Всё. |
| `…/Scripts/Functions/IPredicate.cs` | Три пустых маркера `IPredicate`. |
| `…/Scripts/Functions/InlineFunction.cs` | Три арности `InlineFunction`: `readonly` поле, null-проверка в конструкторе, неявное преобразование из `Func`, `ToString`, атрибуты Odin. `Value` только у арности 0. |
| `…/Scripts/Functions/InlinePredicate.cs` | Три арности `InlinePredicate`: наследование от `InlineFunction` плюс маркер и неявное преобразование, без собственного состояния и без `[Serializable]`. |
| `…/Scripts/Functions/AndPredicate.cs` | `sealed`, `[SerializeReference] IPredicate[]`, `[HideLabel]` под Odin, `AggressiveInlining`, индексный цикл, пустое → `true`. |
| `…/Scripts/Functions/OrPredicate.cs` | То же, пустое → `false`, **без** `AggressiveInlining`. |
| `…/Scripts/Functions/NotPredicate.cs` | Один слот `[SerializeReference] IPredicate`, единственный метод `Invoke`, ни одного конструктора. 40 строк. |
| `…/Scripts/Functions/Extensions.cs` | `public static partial class Extensions`, часть «функции»: `InvokeNot` ×3, `Negate` ×3 внутри `#region Invert`, `#region Collections` с `Add` ×3 и `Remove` ×2 (арность 1 пропущена). |
| `…/Scripts/Expressions/IExpression.cs` | 44 строки: три объявления `IExpression` и подавление `PossibleInterfaceMemberAmbiguity`. Здесь видно асимметрию `IValue<R>` против `IFunction<…>`. |
| `…/Scripts/Expressions/ExpressionMember.cs` | Три struct-а члена: поля `_source`/`_func`, конструкторы (у арности 0 их четыре, у остальных два), `readonly Invoke`, `readonly EqualsFunction` через `==`. |
| `…/Scripts/Expressions/ExpressionBase.cs` | Три абстрактных базы: `Value`/`Invoke`, `protected abstract` свёртка, по три конструктора на арность, атрибуты `[Serializable]`/`[InlineProperty]`/`[ShowInInspector]`/`[Button]`. |
| `…/Scripts/Expressions/AndExpression.cs` | Три арности AND; каждая реализует `IPredicate…`; `params` объявлен раньше `IEnumerable`. |
| `…/Scripts/Expressions/OrExpression.cs` | Три арности OR; у негенерической `IEnumerable`-конструктор объявлен раньше `params`. |
| `…/Scripts/Expressions/IntSumExpression.cs` | Три арности суммы `int`, затравка `0`; у `<T>` и `<T1,T2>` порядок конструкторов инвертирован. |
| `…/Scripts/Expressions/IntMulExpression.cs` | Три арности произведения `int`, затравка `1`. |
| `…/Scripts/Expressions/FloatSumExpression.cs` | Три арности суммы `float`, затравка `0`. Тип, который покрыт живыми тестами. |
| `…/Scripts/Expressions/FloatMulExpression.cs` | Три арности произведения `float`, затравка `1` — стек множителей. |
| `…/Scripts/Expressions/InlineExpression.cs` | Три арности свёртки-лямбды; здесь же баг с потерянным `capacity` у арности 2 и отсутствие null-проверки. |
| `…/Scripts/Expressions/Extensions.cs` | **Главный файл этой главы.** Объявлен как `public partial class Extensions` — без `static`, в отличие от `Functions/Extensions.cs`; `static` приезжает из другого партиала. `Sum`/`Multiply`/`And`/`Or` над `IEnumerable` (здесь же инвертированный `Or`), 13 перегрузок fluent `Add`, 9 перегрузок `Remove`, `SubscribeState`. Именно эти методы, а не конструкторы, — реальный публичный API выражений. |
| `…/Scripts/Collections/List/ReactiveLinkedList.cs` | 542 строки хранилища, которое наследует каждое выражение: `Node[]` + free-list, события, `Add`/`Insert`/`Remove`/`RemoveAt`/`IndexOf`/`Contains`/`Clear`/`CopyTo`, struct `Enumerator`, `AllocateNode`/`GetNodeIndex`/`RemoveNode`, партиал `ISerializationCallbackReceiver`. Здесь же ошибочный аксессор `IReadOnlyReactiveCollection<T>.OnItemAdded` (строки 48-52). |
| `…/Scripts/Collections/List/IReadOnlyReactiveList.cs` | Точные имена событий списка (`OnStateChanged`, `OnItemAdded`/`OnItemRemoved` как `Action<int, T>`) и три `readonly struct`-подписки, включая `StateChangedSubscription`. |
| `…/Scripts/Collections/List/IReactiveList.cs` | 17 строк: `IList<T>` + `IReadOnlyReactiveList<T>` + `IReactiveCollection<T>`, плюс `new bool Contains(T)`. |
| `…/Scripts/Collections/IReadOnlyReactiveCollection.cs` | `OnStateChanged`, `OnItemAdded`/`OnItemRemoved` как `Action<T>` — другая форма тех же событий. |
| `…/Scripts/Collections/Array/IReadOnlyReactiveArray.cs` | Откуда у выражения берётся `OnItemChanged` (`Action<int, T>`). |
| `…/Scripts/Collections/Extensions.cs` | Семейство `SubscribeState` для collection/array/list/dictionary — контекст для экспрешновой перегрузки. |
| `…/Scripts/Values/IValue.cs` | 31 строка: негенерический `IValue` с `object Value`, `IValue<out T> : IFunction<T>, IValue` и две default-реализации. Объясняет, что именно даёт `IExpression<R>` его `Value`. |
| `…/Scripts/Values/Const.cs` | `Const<T>` — класс с `[SerializeField] T value`, `Value`, `Invoke()`, неявными преобразованиями в обе стороны. Основание идиомы с `Constants`. |
| `…/Scripts/Values/Constants.cs` | Весь каталог разделяемых констант: `True`/`False`, математика, время, `Zero`/`One`/`NegativeOne`/`Half`, `Gravity`, Unity-векторы и цвета под `#if UNITY_5_3_OR_NEWER`. |
| `…/Elements/Tests/EditMode/Expressions/ExpressionExtensionsTests.cs` | Единственный компилирующийся фикстур модуля (~1300 строк). Эталон API: fluent-цепочки `Add`, удаление по source/`Func`/`IFunction` для всех трёх арностей, `SubscribeState` с `try/finally`, и четыре теста, закрепляющих баг `Or`. |
| `…/Elements/Tests/EditMode/Expressions/{And,Or,IntSum,IntMul,FloatSum,FloatMul}ExpressionTests.cs` | Шесть файлов, закомментированных целиком. Полезны только как список того, что когда-то проверялось, и как оценка объёма работы при починке конструкторов. |
| `…/Elements/Tests/Performance/ReactiveLinkedListPerformance.cs` | Методика замеров из таблицы выше: `N = 1000`, `Measure.Method` с 5 прогревами и 20 измерениями, `SampleUnit.Microsecond`. Весь файл под `#if UNITY_6000`. |
| `Assets/Plugins/Atomic/Entities/Scripts/Installers/MonoEntityInstaller.cs` | Нужен только за одной строкой: `public abstract void Install(IEntity entity)` — поэтому в установщике обязателен `override`. |

### Документация

| Файл | Что в нём |
|---|---|
| `Docs/Elements/Functions/Manual.md` | 151 строка обзора: базовое использование, `InlineFunction`, предикат с аргументом, API-таблица, Best Practices. Лучшая точка входа в половину «функции»; про `AndPredicate` и компанию не упоминает. |
| `Docs/Elements/Functions/IFunctions.md` | Сводная страница по трём арностям `IFunction` с примерами по каждой. |
| `Docs/Elements/Functions/IFunction.md`, ``IFunction`1.md``, ``IFunction`2.md`` | По странице на арность: пример использования + API-референс. Корректны, расхождение только в именах параметров (`arg` против `args` в исходнике). |
| `Docs/Elements/Functions/IPredicates.md` | Сводная по предикатам. Содержит опечатку `IPredicate<R>`/`IPredicate<T, R>` (строка 67) — такого `R` нет. |
| `Docs/Elements/Functions/IPredicate.md`, ``IPredicate`1.md``, ``IPredicate`2.md`` | По странице на арность предиката; примеры `IsEnemyPredicate`/`AreAlliesPredicate`, откуда взята идиома 8. |
| `Docs/Elements/Functions/InlineFunctions.md` | Сводная по трём арностям `InlineFunction` с примерами. |
| `Docs/Elements/Functions/InlineFunction.md` | Самая подробная страница по арности 0 — и единственная с явной ошибкой типа: строка 46 объявляет `: IFunction<R>, IValue<R>`. |
| ``Docs/Elements/Functions/InlineFunction`1.md``, ``InlineFunction`2.md`` | Арности 1 и 2; корректны. |
| `Docs/Elements/Functions/InlinePredicates.md`, `InlinePredicate.md`, ``InlinePredicate`1.md``, ``InlinePredicate`2.md`` | Сводная плюс три страницы по арностям `InlinePredicate`. Про отсутствие `[Serializable]` не предупреждают. |
| `Docs/Elements/Functions/ExtensionsInvert.md` | 107 строк про негацию — **и везде называет метод `Invert`** (строки 17-19, 31, 44, 57, 76). В коде это `Negate`. Читайте только за примерами сценариев, не за именами. |
| `Docs/Elements/Functions/ExtensionsCollections.md` | 89 строк про `Add`/`Remove` для `ICollection<Func<…>>`. Не упоминает, что `Remove` арности 1 нет. |
| `Docs/Elements/Expressions/Manual.md` | 276 строк — главный обзор выражений: AND, OR, IntSum, FloatMul, API-таблица. Концептуально полезен, но все конструкторы в нём `Func<R>`-овые и не компилируются, плюс `RemoveAt(isCooldown)` на строке 56. |
| `Docs/Elements/Expressions/IExpressions.md` | Сводная по трём арностям `IExpression` с примерами. Два сниппета дефектны (`int result` от булева выражения, необъявленная переменная). |
| `Docs/Elements/Expressions/IExpression.md`, ``IExpression`1.md``, ``IExpression`2.md`` | По странице на арность: строка типа (устаревшая, `IList<Func<R>>`), затем довольно полный разбор событий `OnStateChanged`/`OnItemChanged`/`OnItemAdded`/`OnItemRemoved` и членов списка. Самое ценное в этих файлах — описание реактивной грани. |
| `Docs/Elements/Expressions/ExpressionsBase.md` | 70 строк: рецепт «как объявить своё выражение», из которого взята идиома 4. Конструктор в примере `params Func<bool>[]` — замените на `params ExpressionMember<bool>[]`. |
| `Docs/Elements/Expressions/ExpressionBase.md`, ``ExpressionBase`1.md``, ``ExpressionBase`2.md`` | По 344-404 строки на арность: тип, конструкторы, свойства, события, методы. Самая подробная документация базы — с устаревшим элементом (`ReactiveLinkedList<Func<R>>`, строка 130). |
| `Docs/Elements/Expressions/ExpressionMember.md` | 264 строки: единственная страница, которая вообще знает про `ExpressionMember`, с разбором `Source`, конструкторов, `EqualsFunction` и честным предупреждением про `Contains` и тождество делегатов. Читать обязательно. |
| `Docs/Elements/Expressions/AndExpressions.md` | Короткая сводная (87 строк) по трём арностям AND. Содержит `RemoveAt(isCooldown)` (49) и два дефектных сниппета (67, 77). |
| `Docs/Elements/Expressions/AndExpression.md`, ``AndExpression`1.md``, ``AndExpression`2.md`` | По 318-353 строки: тип, конструкторы (capacity / params / IEnumerable), `Value`, `Count`, события, методы списка. Структура правильная, типы элементов — нет. |
| `Docs/Elements/Expressions/OrExpressions.md` | Сводная по OR (66 строк). |
| `Docs/Elements/Expressions/OrExpression.md`, ``OrExpression`1.md``, ``OrExpression`2.md`` | По 322-367 строк. **Описание `Value` скопировано у AND** (в `OrExpression.md` — строки 205-213), что переворачивает семантику. Верьте исходнику. |
| `Docs/Elements/Expressions/IntSumExpressions.md`, `IntSumExpression.md`, ``IntSumExpression`1.md``, ``IntSumExpression`2.md`` | Сводная плюс три страницы по сумме `int`; конструкторы задокументированы как `IntSumExpression(params Func<int>[])`. |
| `Docs/Elements/Expressions/IntMulExpressions.md`, `IntMulExpression.md`, ``IntMulExpression`1.md``, ``IntMulExpression`2.md`` | То же для произведения `int`. |
| `Docs/Elements/Expressions/FloatSumExpressions.md`, `FloatSumExpression.md`, ``FloatSumExpression`1.md``, ``FloatSumExpression`2.md`` | То же для суммы `float` — тип, по которому есть живые тесты, так что сверять доки с `ExpressionExtensionsTests.cs` удобнее всего именно здесь. |
| `Docs/Elements/Expressions/FloatMulExpressions.md`, `FloatMulExpression.md`, ``FloatMulExpression`1.md``, ``FloatMulExpression`2.md`` | То же для произведения `float`; в примерах — стек множителей урона/скорости/прибыли. |
| `Docs/Elements/Expressions/InlineExpressions.md`, `InlineExpression.md`, ``InlineExpression`1.md``, ``InlineExpression`2.md`` | Сводная плюс три страницы по свёртке-лямбде; сигнатура `Func<Enumerator, R>` здесь документирована верно. Про потерянный `capacity` у арности 2 не предупреждают. |
| `Docs/Elements/Performance/ReactiveLinkedListPerformance.md` | 24 строки — единственные реальные замеры, релевантные выражениям: таблица `List`/`ReactiveList`/`ReactiveLinkedList` на 1000 элементов (M1) плюс четыре вывода. Таблица воспроизведена выше. |
| `Docs/BestPractices/UsingInlineFunctions.md` | 142 строки: два установщика (Rigidbody/Transform) и чтение через `IFunction<…>` — источник идиомы 1. Оба примера пишут `public void Install`, что не скомпилируется: нужен `override`. Вторая половина файла — та же версия примеров, закомментированная в HTML-комментарии. |
| `Docs/BestPractices/UsingExpressions.md` | 197 строк: `OrExpression` для условия лечения и `AndExpression` для условия выстрела внутри `CharacterInstaller`, плюс динамическое отключение через `condition.Add(() => false)`. Здесь `Install` правильно помечен `override`. Конструкторы с лямбдами не компилируются; саму схему «условие как объект сущности» брать стоит. |
| `Docs/BestPractices/UsingConstantsWithAndExpressions.md` | 43 строки про `Constants.True`/`Constants.False` как бесплатный способ запереть и отпереть условие — источник идиомы 6. Первая строка примера (`new AndExpression(cond1, cond2, cond3)`) не компилируется, `Add`/`Remove` с `Constants.False` — корректны. |

Чего в документации нет вовсе: `AndPredicate`, `OrPredicate`, `NotPredicate` (ни одного упоминания во всём
`Docs/`), и нигде не описан реальный публичный путь наполнения выражения — fluent-расширения `Add` из
`Scripts/Expressions/Extensions.cs`. По этим двум темам читайте исходники и
`ExpressionExtensionsTests.cs`.

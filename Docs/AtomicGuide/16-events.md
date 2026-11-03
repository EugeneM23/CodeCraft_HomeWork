# 16. Events: шина событий

`Atomic.Events` — это слой publish/subscribe, в котором издатель и подписчик не знают друг о друге: они встречаются на целочисленном `int`-идентификаторе события. Идентификатор получается из строкового имени через `EventKeyStore` и оборачивается в `readonly struct EventKey<TBus, ...>`, несущий в generic-параметрах и тип шины, и типы аргументов, — поэтому ключ физически нельзя применить к несовместимой шине или с неправильной полезной нагрузкой. Хранилище внутри `EventBus` — не `Dictionary`, а собственная хеш-таблица с раздельными цепочками на двух массивах (`EventSlot[]` + `int[]`), со свободным списком и ростом по таблице простых чисел; подписка и публикация не выделяют память. Поверх этого Roslyn-генератор `[EventExtensionsAPI]` превращает декларативный класс ключей в методы `Subscribe{Name}` / `Unsubscribe{Name}` / `Invoke{Name}` / `IsSubscribed{Name}` / `Dispose{Name}` — и это единственная форма API, которую команде стоит писать в игровом коде. В модуле нет никакого обновления по кадрам: единственный хук в главный цикл — `ThreadSafeEventBus.Flush()`, который вы вызываете сами.

---

## Модель

Пять понятий, которые надо держать в голове до первого вызова.

**1. Событие — это `int`.** Вся шина работает на `int key`. `IEventBus` не знает ни строк, ни `EventKey` — это целиком `int`-API (`Assets/Plugins/Atomic/Events/Scripts/Bus/Runtime/IEventBus.cs`). Строки и ключи — надстройки в `Extensions`.

**2. Один ключ — ровно один `Delegate`.** В слоте хранится один `Delegate`. Множественная подписка на безопасном пути достигается композицией: `Subscribe` читает уже лежащий делегат, складывает с новым (`(Action<T>) del + action`) и пишет обратно. Из этого вытекает самое важное практическое следствие всего модуля — см. «Грабли», пункт 1.

**3. Типизация живёт в ключе, а не в шине.** `EventKey<TBus>`, `EventKey<TBus, TArg>`, `EventKey<TBus, TArg1, TArg2>`, `EventKey<TBus, TArg1, TArg2, TArg3>` — четыре отдельных структуры. Первый generic-аргумент — тип шины, остальные — полезная нагрузка. Генератор берёт `TypeArguments[0]` и объявляет расширение именно на этом типе (`CodeEmitter.EmitEventRegion`, строка `busTypeName = evt.BusTypeName`). Поэтому выбор `TBus` — это и есть решение «какие шины принимают это событие».

**4. Есть два пути: safe и unsafe, и это не «медленно/быстро».** Safe-путь — многоподписчиковый, прощающий: `Invoke` без подписчиков ничего не делает, `Unsubscribe` неизвестного ключа ничего не делает. Unsafe-путь — односубскрайберный и бросающий: `SubscribeUnsafe` кидает `ArgumentException` на втором подписчике, `InvokeUnsafe` и `UnsubscribeUnsafe` кидают `KeyNotFoundException`, если ключа нет. Это разная семантика, а не разная скорость.

**5. `EventKeyStore` — глобальное изменяемое статическое состояние.** Два несинхронизированных `Dictionary` и один подменяемый `IEventKeyAlgorithm`. Имя разрешается в id один раз на процесс; обратное отображение существует только для диагностики (`IdToName`, иначе `#Unknown:{id}`). По умолчанию алгоритм — `SequentialEventKeyAlgorithm`, который **игнорирует имя** и просто возвращает `_nextId++`. Это значит: id зависят от порядка инициализации типов и не стабильны между запусками.

Жизненный цикл в одном абзаце: вы объявляете ключи как `static` поля в `[EventExtensionsAPI] partial` классе → их статический инициализатор один раз разрешает имена в id → игровой код вызывает сгенерированные расширения, которые читают `ClassName.FieldName.Id` как прямую загрузку поля → шина кладёт/достаёт делегат из массива → `Subscription` (struct) возвращается как хендл отписки.

---

## API

### Ядро шины

```csharp
public interface IEventBus : IDisposable
{
    Subscription Subscribe(int key, Action action);
    Subscription<T> Subscribe<T>(int key, Action<T> action);
    Subscription<T1, T2> Subscribe<T1, T2>(int key, Action<T1, T2> action);
    Subscription<T1, T2, T3> Subscribe<T1, T2, T3>(int key, Action<T1, T2, T3> action);

    Subscription SubscribeUnsafe(int key, Action action);
    Subscription<T> SubscribeUnsafe<T>(int key, Action<T> action);
    Subscription<T1, T2> SubscribeUnsafe<T1, T2>(int key, Action<T1, T2> action);
    Subscription<T1, T2, T3> SubscribeUnsafe<T1, T2, T3>(int key, Action<T1, T2, T3> action);

    void Unsubscribe(int key, Action action);
    void Unsubscribe<T>(int key, Action<T> action);
    void Unsubscribe<T1, T2>(int key, Action<T1, T2> action);
    void Unsubscribe<T1, T2, T3>(int key, Action<T1, T2, T3> action);

    void UnsubscribeUnsafe(int key, Action action);
    void UnsubscribeUnsafe<T>(int key, Action<T> action);
    void UnsubscribeUnsafe<T1, T2>(int key, Action<T1, T2> action);
    void UnsubscribeUnsafe<T1, T2, T3>(int key, Action<T1, T2, T3> action);

    void Invoke(int key);
    void Invoke<T>(int key, T arg);
    void Invoke<T1, T2>(int key, T1 arg1, T2 arg2);
    void Invoke<T1, T2, T3>(int key, T1 arg1, T2 arg2, T3 arg3);

    void InvokeUnsafe(int key);
    void InvokeUnsafe<T>(int key, T arg);
    void InvokeUnsafe<T1, T2>(int key, T1 arg1, T2 arg2);
    void InvokeUnsafe<T1, T2, T3>(int key, T1 arg1, T2 arg2, T3 arg3);

    bool IsSubscribed(int key);
    bool Dispose(int key);
}
```

Это весь контракт. Арность полезной нагрузки — максимум 3. `Dispose(int key)` сбрасывает всех подписчиков одного события и возвращает, было ли что сбрасывать; `Dispose()` (из `IDisposable`) очищает шину целиком. Обратите внимание: `Flush()` в интерфейсе **нет**.

```csharp
public class EventBus : IEventBus
{
    public EventBus(int capacity = 0)
}
```

Рабочая реализация для чистого C# и однопоточной логики. Параметр `capacity` округляется вверх по `InternalUtils.PrimeTable` — используйте его, когда число различных событий известно (см. «Производительность»). В документации этого параметра нет: `grep -rni "capacity" Docs/Events` не даёт ни одного совпадения.

```csharp
public class ThreadSafeEventBus : IEventBus
{
    public ThreadSafeEventBus()                  // внутри: new EventBus() → capacity 0
    public ThreadSafeEventBus(IEventBus inner)   // оборачивает любую готовую шину
    public void Flush()
}
```

Декоратор, который откладывает **только публикацию**: все восемь перегрузок `Invoke`/`InvokeUnsafe` кладут замыкание в `ConcurrentQueue<Action>`, а `Flush()` выполняет их на вызывающем потоке. Перегрузка с `inner` — единственный способ получить потокобезопасную публикацию в предразмеренную шину (`new ThreadSafeEventBus(new EventBus(64))`) или в `MonoEventBus`; покрыта тестом `ThreadSafeEventBusTests.Constructor_WithInner_DoesNotThrow` (строка 28).

```csharp
[AddComponentMenu("Atomic/Events/Event Bus")]
[DisallowMultipleComponent, DefaultExecutionOrder(-1000)]
public partial class MonoEventBus : MonoBehaviour, IEventBus
{
    protected virtual void OnDestroy() => _eventBus.Dispose();
}
```

Шина как компонент сцены: вся поверхность `IEventBus` проброшена в приватный `readonly EventBus _eventBus = new()`. `DefaultExecutionOrder(-1000)` гарантирует, что её `Awake`/`OnDestroy` идут раньше обычных компонентов. Берите её, когда проводку шины должен видеть и перенастраивать дизайнер в инспекторе.

```csharp
public abstract class MonoEventBusSingleton<T> : MonoEventBus where T : MonoEventBus
{
    public static T Instance { get; }                     // бросает NullReferenceException, если не найдено
    public static T Resolve(in Component component);
    public static T Resolve(in GameObject gameObject);
    public static T Resolve(Scene scene);                 // бросает Exception, если не найдено
}
```

`Instance` сначала проверяет статический кеш, затем ищет через `FindFirstObjectByType<T>()` (под `UNITY_2023_1_OR_NEWER`) или `FindObjectOfType<T>()`. Статик пишется в `Awake`. `Resolve(Scene)` мемоизирует результат в `static Dictionary<Scene, T>` — это правильный выбор для аддитивных мультисценовых сборок, где глобальный `Instance` склеил бы две игровые сцены в одну шину.

### Ключи

```csharp
public readonly struct EventKey<TBus> : IEquatable<EventKey<TBus>> where TBus : IEventBus
{
    public readonly int Id;

    public EventKey(string name) => Id = EventKeyStore.NameToId(name);
    public EventKey(int id) => Id = id;

    public override string ToString() => EventKeyStore.IdToName(Id);
    public bool Equals(EventKey<TBus> other) => Id == other.Id;
    public override bool Equals(object obj) => obj is EventKey<TBus> other && Equals(other);
    public override int GetHashCode() => Id;
    public static bool operator ==(EventKey<TBus> left, EventKey<TBus> right) => left.Equals(right);
    public static bool operator !=(EventKey<TBus> left, EventKey<TBus> right) => !left.Equals(right);
}
```

`EventKey<TBus, TArg>`, `EventKey<TBus, TArg1, TArg2>`, `EventKey<TBus, TArg1, TArg2, TArg3>` имеют ровно ту же форму (файлы ``EventKey`1.cs``, ``EventKey`2.cs``, ``EventKey`3.cs``). `Id` — **публичное readonly поле**, не свойство (XML-комментарий в `CodeEmitter.cs` называет его property — это ошибка комментария, не кода).

Конструктор `EventKey(int id)` заслуживает отдельного абзаца, потому что его легко не заметить. Это единственный способ восстановить ключ из сохранённого или присланного по сети id, минуя `EventKeyStore`. Цена: обратного отображения у такого id нет, поэтому сообщения исключений шины деградируют до `#Unknown:{id}` (`EventKeyStore.IdToName` возвращает именно эту строку для незарегистрированного id). Если вы сериализуете id — сначала переключитесь на детерминированный алгоритм, см. «Грабли».

```csharp
public static class EventKeyStore
{
    public static void SetAlgorithm(IEventKeyAlgorithm algorithm);   // + Reset() внутри
    public static int NameToId(string name);                         // AggressiveInlining
    public static string IdToName(int id);                           // AggressiveInlining
    public static void Reset();
}
```

Двунаправленный кеш имя↔id. `NameToId` кидает `ArgumentNullException` на `null`, иначе один раз спрашивает алгоритм и запоминает оба направления. Вызывайте `SetAlgorithm` только на старте приложения, `Reset` — в тестах.

```csharp
public interface IEventKeyAlgorithm
{
    int NameToId(string name);
    void Reset();
}

public sealed class SequentialEventKeyAlgorithm : IEventKeyAlgorithm
{
    public SequentialEventKeyAlgorithm(int nextId = 1);
    public int NameToId(string name) => _nextId++;   // имя игнорируется
    public void Reset() => _nextId = INITIAL_ID;     // INITIAL_ID == 1, не аргумент конструктора
}

public sealed class Fnv1AEventKeyAlgorithm : IEventKeyAlgorithm   // 32-бит FNV-1a, без null-проверки
public sealed class SHA256EventKeyAlgorithm : IEventKeyAlgorithm  // один общий static readonly SHA256
```

Выбор прост: `Sequential` (по умолчанию) — пока id нужны только внутри процесса; `Fnv1A` — как только id попадают в сейв, реплей или сеть; `SHA256` брать практически незачем (см. «Грабли»).

### Подписки

```csharp
public readonly struct Subscription : IDisposable
{
    internal Subscription(IEventBus eventBus, int key, Action action);
    public void Dispose() => _eventBus.Unsubscribe(_key, _action);
}
```

`Subscription<T>`, `Subscription<T1, T2>`, `Subscription<T1, T2, T3>` — то же с соответствующим `Action<...>`. Все четыре — `readonly struct` с `internal` конструктором: экземпляр можно получить только из `Subscribe*`.

### Extensions

`Atomic.Events.Extensions` даёт два семейства перегрузок поверх `int`-API.

Строковое семейство — полное зеркало, включая unsafe-путь, 26 методов:

```csharp
public static Subscription Subscribe(this IEventBus it, string key, Action action);
public static Subscription<T> Subscribe<T>(this IEventBus it, string key, Action<T> action);
// + <T1,T2>, <T1,T2,T3>
public static Subscription SubscribeUnsafe(this IEventBus it, string key, Action action);
// + <T>, <T1,T2>, <T1,T2,T3>
public static void Invoke(this IEventBus it, string key);
public static void Invoke<T>(this IEventBus it, string key, T arg);
// + <T1,T2>, <T1,T2,T3>
public static void InvokeUnsafe(this IEventBus it, string key);
public static void InvokeUnsafe<T>(this IEventBus it, string key, T arg);
// + <T1,T2>, <T1,T2,T3>
public static void Unsubscribe(this IEventBus it, string key, Action action);          // + <T>, <T1,T2>, <T1,T2,T3>
public static void UnsubscribeUnsafe(this IEventBus it, string key, Action action);    // + <T>, <T1,T2>, <T1,T2,T3>
public static bool IsSubscribed(this IEventBus it, string key);
public static bool Dispose(this IEventBus it, string key);
```

Каждый из них разворачивается в `it.<Method>(NameToId(key), ...)`, то есть платит проход по словарю `EventKeyStore` **на каждом вызове**. Это API для тестов, тулинга и прототипов, не для геймплея.

Семейство `EventKey` — то же самое, но с обобщённым получателем, привязанным к шине ключа:

```csharp
public static Subscription Subscribe<TBus>(this TBus it, EventKey<TBus> key, Action action)
    where TBus : IEventBus => it.Subscribe(key.Id, action);

public static Subscription<T> Subscribe<TBus, T>(this TBus it, EventKey<TBus, T> key, Action<T> action)
    where TBus : IEventBus => it.Subscribe(key.Id, action);
// + <TBus,T1,T2>, <TBus,T1,T2,T3>; такие же наборы для SubscribeUnsafe, Invoke, InvokeUnsafe,
//   Unsubscribe, UnsubscribeUnsafe
```

Отдельно стоит знать про `IsSubscribed` и `Dispose`: у них **по четыре перегрузки каждая**, по одной на арность ключа, хотя ни одна из них не использует типы полезной нагрузки:

```csharp
public static bool IsSubscribed<TBus>(this TBus it, EventKey<TBus> key) where TBus : IEventBus;
public static bool IsSubscribed<TBus, T>(this TBus it, EventKey<TBus, T> key) where TBus : IEventBus;
public static bool IsSubscribed<TBus, T1, T2>(this TBus it, EventKey<TBus, T1, T2> key) where TBus : IEventBus;
public static bool IsSubscribed<TBus, T1, T2, T3>(this TBus it, EventKey<TBus, T1, T2, T3> key) where TBus : IEventBus;
// идентичный набор из четырёх перегрузок для Dispose
```

Причина — типы ключей разной арности не связаны наследованием, так что общей перегрузки быть не может. Практический вывод: generic-арность вызова обязана совпадать с арностью ключа, иначе перегрузка просто не найдётся.

### Кодогенерация

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventExtensionsAPIAttribute : Attribute
{
    public bool Unsafe { get; set; }
    public EventExtensionsAPIAttribute() { }
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class UnsafeAttribute : Attribute
{
}
```

У `UnsafeAttribute` нет ни одного параметра конструктора — запомните это, из этого следует пункт 5 в «Граблях».

```csharp
[Generator]
public sealed class EventAPIGenerator : IIncrementalGenerator
{
    internal static readonly string CodegenAssemblyName = "Atomic.Events";
    internal static readonly bool IsBuildTime = Assembly.GetEntryAssembly() != null;
    internal static bool ShouldRun(Compilation compilation);
}
```

`ShouldRun` отсеивает три случая: фоновый анализ в IDE (`!IsBuildTime`), саму сборку `Atomic.Events` и любую сборку, которая не ссылается на `Atomic.Events`. Диагностика `EAPG0001` — информационная, печатает `{Namespace}.{ClassName} → [шины] (N events)`; это первое, что надо смотреть, когда «методы не появились». `EAPG0002` — внутренняя ошибка генератора (ловится `catch` вокруг `Emit`/`AddSource`).

Требования парсера к полю (`EventAPIParser.Transform`) — четыре, и каждое отсеивает поле **молча**, без диагностики:

- модификатор `static` (строка 62). `readonly` **не требуется**;
- ровно одна переменная в объявлении: `fieldDecl.Declaration.Variables.Count != 1` → `continue` (строка 65);
- тип — именно `EventKey` из пространства имён `Atomic.Events` (строки 75-80);
- арность в диапазоне `1..4`: `if (arity < 1 || arity > 4) continue` (строка 83).

Условие unsafe — `bool fieldUnsafe = classUnsafe || HasUnsafeAttribute(fieldDecl);` (строка 91): только эскалация, понижения нет.

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventAPIAnalyzer : DiagnosticAnalyzer
```

Полный исходник анализатора лежит в репозитории: `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIAnalyzer/` — `EventAPIAnalyzer.cs`, `EventAPIDiagnostics.cs` и `EventAPICodeFixProvider.cs` (то есть у диагностик есть автофикс). Готовый DLL — `Assets/Plugins/Atomic/SourceGenerators/EventAPIAnalyzer.dll`. Две диагностики, обе `DiagnosticSeverity.Error`:

| Id | Когда | Текст |
|---|---|---|
| `EAPI0001` | у `EventKey`-поля нет инициализатора | «Event API key field '{0}' must be initialized with a non-default value» |
| `EAPI0002` | инициализатор — `new()`, `new EventKey<...>()` или `default` | «cannot be initialized with `new()` or `default`; provide a name or id» |

`IsParameterlessOrDefault` ловит `default`, `default(T)`, `new T()` и `new()` с пустым списком аргументов.

---

## Идиомы

### Канонический класс ключей

```csharp
using Atomic.Events;
using UnityEngine;

[EventExtensionsAPI]
public static partial class GameEventAPI
{
    public static readonly EventKey<IGameEventBus> PlayerTurnStarted = new(nameof(PlayerTurnStarted));
    public static readonly EventKey<IGameEventBus, int> DamageDealt = new(nameof(DamageDealt));
    public static readonly EventKey<IGameEventBus, int, Vector3> Hit = new(nameof(Hit));
}
```

Почему именно такая форма, по частям:

- `[EventExtensionsAPI]` — обязателен. `EventAPIParser.IsCandidate` ищет атрибут с именем `EventExtensionsAPI` или `EventExtensionsAPIAttribute` на объявлении класса; без него класс вообще не попадает в конвейер, и генератор молча ничего не делает.
- `partial` — обязателен. `CodeEmitter.Emit` печатает `public static partial class {ClassName}`, и без `partial` в вашем объявлении это не скомпилируется.
- `static readonly` + `new(nameof(X))` — строка хешируется ровно один раз на процесс, в статическом инициализаторе типа, а в точке вызова остаётся чтение `int`-поля. `nameof` привязывает имя id к имени поля, так что переименование не рассинхронизирует их. Формально `readonly` генератору не нужен (он проверяет только `static`), но мутабельное статическое поле ключа — готовая диверсия: id уже разошёлся по подписчикам, а поле уже другое.
- `IGameEventBus` вместо `IEventBus` в первом generic-параметре — главная дисциплина этого модуля. Генератор берёт `TypeArguments[0]` и объявляет расширения именно на этом типе, а перегрузки `Extensions` ограничены `where TBus : IEventBus`. В итоге попытка опубликовать игровое событие в UI-шину становится **ошибкой компиляции**. `IGameEventBus` — ваш собственный пустой интерфейс-маркер, унаследованный от `IEventBus`.
- `Vector3` как второй аргумент — легально; арность полезной нагрузки до трёх. Если тип приходит из другого модуля (например, `IEntity` из `Atomic.Entities`), нужен и `using`, и ссылка в asmdef — одного `Atomic.Events` не хватит.

Три антипаттерна, которые это заменяет:

```csharp
// 1. магические int: ничто не мешает коллизии, в стектрейсе событие безымянно
bus.Subscribe(1, () => Debug.Log("Hello!"));
bus.Invoke(1);

// 2. строковые ключи в точке вызова: проход по словарю на КАЖДОМ вызове, а опечатка молчит
bus.Invoke("PlayerTurnStarted");
bus.Subscribe("PlayerTrunStarted", OnTurn);   // не выстрелит никогда и не предупредит

// 3. поля, которые отвергнет анализатор: id останется 0, и все такие события сольются в одно
[EventExtensionsAPI]
public static partial class GameEventAPI
{
    public static readonly EventKey<IGameEventBus> PlayerTurnStarted;        // EAPI0001
    public static readonly EventKey<IGameEventBus> PlayerTurnEnded = new();  // EAPI0002
}

// 4. и ещё одна ловушка, про которую не предупредит никто: два ключа в одном объявлении
[EventExtensionsAPI]
public static partial class GameEventAPI
{
    // Variables.Count == 2 → EventAPIParser.Transform делает continue.
    // Методы для A и B НЕ будут сгенерированы, диагностики не будет.
    public static readonly EventKey<IGameEventBus> A = new(nameof(A)), B = new(nameof(B));
}
```

### Объявление шины домена

```csharp
public interface IGameEventBus : IEventBus
{
}

public sealed class GameEventBus : MonoEventBusSingleton<GameEventBus>, IGameEventBus
{
}
```

`sealed`, пустое тело, self-referencing generic-параметр. Вся логика унаследована; тип существует только для того, чтобы быть отдельной идентичностью для `Instance`/`Resolve` и носителем маркера `IGameEventBus`, против которого типизированы ключи. Одна шина на домен (game / UI / audio) — именно это делает типизацию ключей по шине небесполезной.

### Скоуп подписки через `using var`

```csharp
IGameEventBus bus = GameEventBus.Instance;

using var subscription = bus.SubscribeDamageDealt(amount => Debug.Log($"Damage: {amount}"));

bus.InvokeDamageDealt(10);   // в лог попадёт
// subscription.Dispose() вызывается на выходе из скоупа
```

`Subscription<int>` — `readonly struct`, и `using var` на структуре с `IDisposable` компилируется в прямой вызов `Dispose()` **без боксинга**: хендл целиком живёт на стеке. Декларация `using` вдобавок делает точку отписки структурной — нет пути исполнения, на котором её можно забыть.

Плохо:

```csharp
// боксит структуру в IDisposable и теряет типизацию
IDisposable sub = bus.SubscribeDamageDealt(OnDamage);
var all = new List<IDisposable> { bus.SubscribeDamageDealt(OnDamage) };   // один бокс на подписку

// или, хуже, вообще без хендла: отписки нет, колбэк переживёт слушателя
bus.SubscribeDamageDealt(OnDamage);
```

### Блочный `using`, когда окно прослушивания уже метода

```csharp
IEventBus bus = new EventBus();

using (bus.Subscribe(1, () => Debug.Log("Hello!")))
{
    bus.Invoke(1);   // Output: Hello!
}

bus.Invoke(1);       // ничего: подписка уничтожена
```

Это пример из `Docs/Events/Subscriptions/Manual.md`, и он показывает семантику, которую команде надо усвоить: после отписки `Invoke` на безопасном пути — тихий no-op, а не ошибка, потому что `TryGetEvent` просто не находит ключ. Блочная форма правильна, когда окно прослушивания короче метода.

### Ручная отписка для нелексических времён жизни

```csharp
public sealed class DamageHud : MonoBehaviour
{
    // именно GameEventBus, а не MonoEventBus: сгенерированный SubscribeDamageDealt
    // объявлен расширением IGameEventBus, и MonoEventBus этот маркер не реализует
    [SerializeField] private GameEventBus _eventBus;

    private Subscription<int> _damageSub;   // типизированное поле: без боксинга

    private void OnEnable() => _damageSub = _eventBus.SubscribeDamageDealt(OnDamage);

    private void OnDisable() => _damageSub.Dispose();

    private void OnDamage(int amount) { /* ... */ }
}
```

Это честная форма для времён жизни в стиле `MonoBehaviour`: хранить **типизированный** `Subscription<int>` в поле и уничтожать его в `OnDisable`. Инъекция через `[SerializeField]` вместо статического поиска: зависимость видна в инспекторе, дизайнер может её перевесить. Тип поля обязан быть тем, на котором объявлено расширение: ключ типизирован `EventKey<IGameEventBus, int>`, значит `SubscribeDamageDealt` — это `this IGameEventBus bus`, и поле `MonoEventBus` здесь просто не скомпилируется. Повторный `Dispose` на безопасном пути безвреден — `Unsubscribe` неизвестного ключа ничего не делает.

Антипаттерн, который на это похож и который берут прямо из документации (`Docs/Events/Bus/Manual.md`, строки 46-60):

```csharp
public class SampleUsage : MonoBehaviour
{
    [SerializeField] private MonoEventBus eventBus;

    private void Start() => eventBus.SubscribePlayerTurnStarted(() => Debug.Log("Player turn started"));

    private void OnDestroy() => eventBus.Dispose();   // чистим ЧУЖУЮ шину целиком
}
```

Два дефекта. Во-первых, подписка сделана без хендла. Во-первых-же следствие: единственный способ «убрать за собой» — снести шину полностью, а это `ClearEvents()`, то есть вынос всех подписчиков всех событий. Во-вторых, `MonoEventBus.OnDestroy` уже делает `_eventBus.Dispose()` сам. Правило: кто владеет шиной — тот её и чистит; потребитель уничтожает только свои `Subscription`.

### Публикация из рабочих потоков

```csharp
// ThreadSafeEventBus не sealed, а его конструктор с inner — публичный,
// поэтому маркер домена навешивается тонким наследником. Без этого
// сгенерированный InvokeDamageDealt (расширение IGameEventBus) не найдётся.
public sealed class WorldEventBus : ThreadSafeEventBus, IGameEventBus
{
    public WorldEventBus(int capacity) : base(new EventBus(capacity))
    {
    }
}

public sealed class WorldEvents : MonoBehaviour
{
    // держим КОНКРЕТНЫЙ тип: Flush() нет в IEventBus
    private readonly WorldEventBus _bus = new(64);

    public WorldEventBus Bus => _bus;

    private void Update() => _bus.Flush();   // один раз за кадр, на главном потоке
    private void OnDestroy() => _bus.Dispose();
}

// в джобе или рабочем потоке:
worldEvents.Bus.InvokeDamageDealt(5);        // кладётся в очередь, не вызывается здесь
```

Все колбэки подписчиков остаются на главном потоке — вызовы Unity API внутри обработчиков легальны, — а джобы публикуют свободно через безлоковый `ConcurrentQueue`. `Flush` изолирует исключения (`try`/`catch` → `Debug.LogException` на каждую задачу), так что один сломанный подписчик не останавливает остальные события кадра. Конструктор с `inner` здесь не роскошь: беспараметрический создаёт `new EventBus()` с capacity 0.

### Выбор алгоритма ключей — один раз, на входе в приложение

```csharp
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // ДО того, как будет затронут любой [EventExtensionsAPI] класс
        EventKeyStore.SetAlgorithm(new Fnv1AEventKeyAlgorithm());
    }
}
```

`SetAlgorithm` внутри вызывает `Reset()`, который очищает оба направления кеша. Любой уже сконструированный `EventKey` сохранит свой теперь осиротевший id, а хранилище забудет отображение. Поэтому «ровно один раз, в точке входа» — единственное корректное применение. Берите `Fnv1A`, когда id должны быть воспроизводимы между запусками (сейвы, реплеи, неткод); оставляйте `Sequential` по умолчанию, когда id чисто внутрипроцессные.

### Сброс хранилища между тестами

```csharp
[SetUp]
public void SetUp() => EventKeyStore.Reset();
```

`EventKeyStore` — глобальное изменяемое состояние; без этого порядок тестов меняет id, и набор тестов на алгоритме по умолчанию становится зависимым от порядка. В редакторе `[InitializeOnEnterPlayMode]` делает это само — но только когда domain reload **не** отключён.

### Свой алгоритм ключей

```csharp
public sealed class Crc32EventKeyAlgorithm : IEventKeyAlgorithm
{
    public int NameToId(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        uint crc = 0xFFFFFFFFu;
        foreach (byte b in Encoding.UTF8.GetBytes(name))
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320u & (uint) -(int) (crc & 1));
        }

        return unchecked((int) ~crc);
    }

    public void Reset()
    {
        // алгоритм без состояния — сбрасывать нечего
    }
}
```

Нужная форма: `sealed`, без состояния, без кеширования (кешем владеет `EventKeyStore`), `Reset` — пустой для stateless-алгоритма, детерминированный результат на одном и том же имени. Обязательно настоящий хеш: алгоритм вида `name => name.Length` коллизирует на каждом имени одинаковой длины, а коллизия id — это два разных события в одном слоте.

### Разрешение шины по сцене

```csharp
private void Awake()
{
    IGameEventBus bus = GameEventBus.Resolve(this.gameObject);   // → Resolve(gameObject.scene)
}
```

Правильно для аддитивных мультисценовых сборок, где статический `Instance` выдал бы двум игровым сценам одну шину и склеил бы их события. `Resolve` мемоизирует результат на `Scene` в статическом словаре, так что обход `GetRootGameObjects` происходит один раз на сцену. Апстримная оговорка: на успешном пути пулованный список не возвращается в пул — см. «Грабли».

### Точечный вывод горячего события на unsafe-путь

```csharp
[EventExtensionsAPI]
public static partial class GameEventAPI
{
    public static readonly EventKey<IGameEventBus> GameStarted = new(nameof(GameStarted));

    [Unsafe]
    public static readonly EventKey<IGameEventBus, int> DamageDealt = new(nameof(DamageDealt));
}
```

Правильная гранулярность: по умолчанию всё остаётся безопасным, а проверки теряет только измеренное горячее событие — и unsafe-поверхность в кодовой базе остаётся маленькой и грепаемой по `[Unsafe]`. `InvokeDamageDealt` тогда эмитит `InvokeUnsafe`, который пропускает одну проверенную приведение-операцию и одну ветвь, реинтерпретируя делегат через `Unsafe.As`. Делайте это **только** для события с ровно одним постоянным подписчиком: `SubscribeUnsafe` кидает на втором, `InvokeUnsafe` — когда подписчиков нет, `UnsubscribeUnsafe` — когда ключа уже нет.

Антипаттерн — ковровый unsafe:

```csharp
// каждое событие в классе теряет проверки, включая холодные,
// и обратно по одному полю не вернуть: [Unsafe(false)] НЕ КОМПИЛИРУЕТСЯ
[EventExtensionsAPI(Unsafe = true)]
public static partial class GameEventAPI
{
    public static readonly EventKey<IGameEventBus> GameStarted = new(nameof(GameStarted));
    public static readonly EventKey<IGameEventBus, int> DamageDealt = new(nameof(DamageDealt));
}
```

Нужна смесь — делайте два `[EventExtensionsAPI]` класса.

### Предразмеривание шины

```csharp
// ~40 различных событий в этой шине
IEventBus bus = new EventBus(capacity: 64);   // CeilToPrime(64) → 97
```

Значение по умолчанию `capacity: 0` разрешается в первое простое из таблицы — **2**. Шина, которая в итоге держит ~40 различных событий, проходит через несколько `IncreaseCapacity`, каждый из которых — `Array.Resize` двух массивов плюс полная перелинковка всех живых слотов. Один аргумент конструктора убирает это целиком. `Array.BinarySearch(PrimeTable, 64)` не находит точного совпадения между 53 и 97, поэтому фактическая ёмкость — 97.

### Групповая зачистка одного события

```csharp
// снять ВСЕХ подписчиков одного события, не трогая остальные
bool had = bus.DisposeDamageDealt();

// проверить, слушает ли кто-то
if (!bus.IsSubscribedDamageDealt())
    return;
```

Этого нет у обычного C#-`event`, и именно это (плюс `Dispose()` всей шины и инспекторный листинг событий) — практическая причина переходить с рукописных `Action`-полей на шину. `Dispose{Name}` и `IsSubscribed{Name}` генератор **всегда** эмитит в безопасном варианте, независимо от `[Unsafe]` (`CodeEmitter.EmitEventRegion`, строки 99-106), так что они не кинут на отсутствующем ключе.

### Как выглядит сгенерированный код

```csharp
/**
 * Code generation. Don't modify!
 **/

using Atomic.Events;
using System;

public static partial class GameEventAPI
{

    #region DamageDealt

    public static Subscription<int> SubscribeDamageDealt(this MyGame.IGameEventBus bus, Action<int> action) =>
        bus.Subscribe<int>(GameEventAPI.DamageDealt.Id, action);

    public static void UnsubscribeDamageDealt(this MyGame.IGameEventBus bus, Action<int> action) =>
        bus.Unsubscribe<int>(GameEventAPI.DamageDealt.Id, action);

    public static void InvokeDamageDealt(this MyGame.IGameEventBus bus, int arg1) =>
        bus.Invoke<int>(GameEventAPI.DamageDealt.Id, arg1);

    public static bool IsSubscribedDamageDealt(this MyGame.IGameEventBus bus) =>
        bus.IsSubscribed(GameEventAPI.DamageDealt.Id);

    public static bool DisposeDamageDealt(this MyGame.IGameEventBus bus) =>
        bus.Dispose(GameEventAPI.DamageDealt.Id);

    #endregion
}
```

Пять методов на ключ, все expression-bodied, все — прямые форвардеры в `int`-API. Тип шины печатается через `TypeArguments[0].ToDisplayString()`, то есть **полностью квалифицированно** (`Atomic.Events.IEventBus`, `MyGame.IGameEventBus`) — не удивляйтесь, когда в `Temp/GeneratedCode/` увидите длинные имена там, где в своём коде пишете короткие. Чтобы включить этот дамп: `ATOMIC_OUTPUT_SOURCEGEN_FILES` в **Edit → Project Settings → Player → Scripting Define Symbols**.

---

## Производительность

Ниже отмечено, что подтверждено чтением исходника, а что — вывод. Замеров (бенчмарков) в апстриме нет вообще, поэтому ни одна цифра здесь не «измерена».

**Хранилище — два массива, не `Dictionary`.** Подтверждено: `EventSlot[] _slots` + `int[] _buckets`, раздельные цепочки, `_freeList`, `_lastIndex`, `_primeIndex`; `Dictionary` в `EventBus.cs` не встречается. `EventSlot` — приватная структура `(int key, Delegate value, bool exists, int next)`. Источник: `Assets/Plugins/Atomic/Events/Scripts/Bus/Runtime/EventBus.cs`, строки 16-33. Вывод (не из исходника): раскладка 4+8+1+4 с выравниванием, то есть слот не упакован, и обход цепочки читает хоть и непрерывную память, но с «дырами». Отдельно: `Docs/Events/Bus/EventBus.md`, строка 3, описывает хранилище прозой как «a dictionary keyed by integer event IDs» — это описание поведения, а не типа; не стоит ожидать там `Dictionary<int, Delegate>`.

**`AggressiveInlining` на всех примитивах хранилища.** Подтверждено: `[MethodImpl(MethodImplOptions.AggressiveInlining)]` стоит на `AddEvent`, `SetEvent`, `RemoveEvent`, `ContainsEvent`, `ClearEvents`, `GetEvent`, `TryGetEvent`, `GetEventUnsafe`, `AllocateSlot`, `FindIndex`, `IncreaseCapacity` и на обеих фабриках исключений (`EventNotFoundException`, `EventAlreadyAddedException`, строки 500-507). Вывод: на двух последних это контрпродуктивно — инлайнинг `new KeyNotFoundException($"...{EventKeyStore.IdToName(key)}...")` с интерполяцией строки в каждую точку вызова `GetEventUnsafe` раздувает горячий метод и может заблокировать его собственный инлайнинг.

**Стоимость безопасного `Invoke`.** Подтверждено по коду `FindIndex` (строки 452-474): проверка `_count == 0`, `int hash = key & 0x7FFFFFFF`, `int bucket = hash % _capacity`, загрузка бакета, короткий обход цепочки через `ref readonly EventSlot slot = ref _slots[index]` (без копирования структуры на каждый шаг), затем приведение `(T)` и вызов делегата. `RemoveEvent`, `IncreaseCapacity` и `ClearEvents` используют мутабельный `ref` — тоже без копий. Вывод: на маленькой шине доминирует не обход памяти, а целочисленное деление в `% _capacity` — одна операция деления на каждый lookup.

**Маска `& 0x7FFFFFFF` — не косметика.** Подтверждено: и `Fnv1AEventKeyAlgorithm` (`unchecked((int) hash)`), и `SHA256EventKeyAlgorithm` (`BitConverter.ToInt32`) производят отрицательные id. Маска гарантирует, что `%` не даст отрицательный индекс бакета. Вывод: плотные последовательные id (алгоритм по умолчанию) раскладываются по простой ёмкости идеально; хеш-id — просто хорошо.

**Рост ёмкости: нет политики загрузки.** Подтверждено: `IncreaseCapacity` вызывается только из `AllocateSlot`, и только когда `_lastIndex == _capacity` (строка 441). То есть таблица работает при коэффициенте заполнения 1.0 до самого роста, и цепочки бакетов максимально длинны ровно перед resize. Сам рост — `_capacity = PrimeTable[++_primeIndex]`, `Array.Resize` обоих массивов, `Array.Fill(_buckets, -1)` и перелинковка всех живых слотов. Выбор простого числа — один `Array.BinarySearch` в конструкторе (`InternalUtils.CeilToPrime`). Практический вывод: предразмеривайте шину.

**Переиспользование слотов.** Подтверждено: `AllocateSlot` сначала снимает голову `_freeList` и только потом расширяет `_lastIndex`; `RemoveEvent` при обнулении `_count` сбрасывает `_lastIndex = 0; _freeList = UNDEFINED_INDEX`, то же делает `ClearEvents`. То есть цикл «подписался/отписался» на разных ключах переиспользует слоты вместо роста массивов, а полностью опустошённая шина возвращается в начальное состояние без переаллокации. Это проверено тестом `EventBusTests.FreeListReuse_AfterUnsubscribe_NewKeyUsesSlot` (строка 158), который через рефлексию читает приватные `_capacity`/`_count` и утверждает отсутствие роста.

**Подписка не выделяет память.** Подтверждено: все четыре `Subscription*` — `readonly struct` с `internal` конструкторами, все четыре `EventKey*` — `readonly struct` с публичным readonly полем `Id`. Передача ключа стоит 4 байта, сгенерированный код читает `GameEventAPI.X.Id` как прямую загрузку статического поля — без индирекции и без поиска по имени. Боксинг возникает ровно в двух местах: когда вы кладёте `Subscription` в `IDisposable`/коллекцию `IDisposable`, и когда вызывается `EventKey.Equals(object)`.

**`Invoke` без подписчиков бесплатен.** Подтверждено: безопасный `Invoke` — это один `FindIndex` и выход, без аллокаций и без исключения. Это делает дёшево публикацию событий, которые сейчас никто не слушает, — полезно для телеметрии и отладочных хуков.

**Разрешение имён — один раз на имя.** Подтверждено: `EventKeyStore.NameToId` кладёт результат в оба словаря и при повторе отдаёт его из кеша; оба метода (`NameToId`, `IdToName`) помечены `AggressiveInlining`. Вывод: игровой код, идущий через `[EventExtensionsAPI]`, строк не касается вообще — имя хешируется в статическом инициализаторе класса ключей. Строковые перегрузки `Extensions` платят `NameToId` на **каждом** вызове: хеш строки плюс проход по словарю.

**Экономия unsafe-пути мала и конкретна.** Подтверждено: `InvokeUnsafe` заменяет `TryGetEvent<Action<T>>` (проверенное приведение `(T)` + ветвь по out-параметру) на `GetEventUnsafe` → `Unsafe.As<Delegate, T>(ref _slots[index].value)`, то есть чистый реинтерпрет. Под Unity `Unsafe` алиасится на `Unity.Collections.LowLevel.Unsafe.UnsafeUtility`, вне Unity — на `System.Runtime.CompilerServices.Unsafe` (`EventBus.cs`, строки 6-10); `Atomic.Events.asmdef` — это буквально `{ "name": "Atomic.Events", "allowUnsafeCode": true }` без ограничений по платформам.

**`UnsubscribeUnsafe` — не быстрый путь.** Подтверждено: все четыре перегрузки начинаются с `GetEventUnsafe<Delegate>(key)` (то есть кидают на отсутствующем ключе), а дальше идут через **безопасное** приведение `(Action<T>) del - action` и `RemoveEvent`/`SetEvent` (`EventBus.cs`, строки 177-215). Выигрыша по сравнению с `Unsubscribe` фактически нет, а риск есть.

**`ThreadSafeEventBus` — единственный аллоцирующий путь.** Подтверждено: все восемь перегрузок `Invoke`/`InvokeUnsafe` — это `_queue.Enqueue(() => _inner.Invoke(key, arg...))`, то есть один display-class с захватом ключа и аргументов плюс один `Action` на каждую публикацию. Не ставьте его на покадровые или на-каждое-попадание события; это механизм для редкой передачи между потоками. `Flush` делает ранний выход на `_queue.Count == 0` (чтение счётчика `ConcurrentQueue` — дешёвое, но не бесплатное) и оборачивает каждую задачу в `try`/`catch` → `Debug.LogException`.

**Аргументы передаются по значению.** Подтверждено: в публичных сигнатурах модуля `in`/`ref` нет нигде, кроме `MonoEventBusSingleton.Resolve(in Component)` / `Resolve(in GameObject)`, где это косметика (ссылочные типы). Внутри `EventBus` `ref`/`ref readonly` используется активно — но только для доступа к слотам таблицы, не для полезной нагрузки. Полезная нагрузка идёт через `Action<T>` по значению, так что «толстая» структура копируется на входе в вызов делегата. Держите структуры-нагрузки маленькими или передавайте ссылочный тип.

**Обобщённый получатель расширений.** Подтверждено: перегрузки с `EventKey` объявлены как `this TBus it ... where TBus : IEventBus`, тогда как строковые — как `this IEventBus it`. Практический смысл этой разницы — типовая проверка «ключ подходит к этой шине» на этапе компиляции; выигрыша по скорости здесь никто не заявляет и никто не измерял.

**Генератор не добавляет рантайм-слоя.** Подтверждено: все сгенерированные методы — однострочные expression-bodied форвардеры (`CodeEmitter.EmitEventRegion`). Вывод: `bus.InvokeDamageDealt(10)` после инлайнинга — это `bus.Invoke<int>(GameEventAPI.DamageDealt.Id, 10)`.

**Редакторная отладка не стоит ничего в билде, и почти ничего — без Odin.** Подтверждено: `EventBus.GetEvents()` (аллоцирует `KeyValuePair<int, Delegate>[_count]`) целиком внутри `#if UNITY_EDITOR`; весь `MonoEventBus_Debug.cs` — тоже под `#if UNITY_EDITOR`. Важное уточнение: свойство `DebugEvents`, которое вызывает `del.GetInvocationList()` на каждое событие (аллокация массива) и сортирует кеш, попадает в инспектор только через Odin — все `[ShowInInspector]`/`[ListDrawerSettings]`/`[Searchable]` закрыты `#if ODIN_INSPECTOR`. Без установленного Odin геттер никто не вызывает, и стоимости перерисовки нет вообще.

**`MonoEventBusSingleton.Resolve(Scene)` — одноразовая стоимость на сцену.** Подтверждено: мемоизация в `static Dictionary<Scene, T>`, поэтому обход `GetRootGameObjects` + `GetComponentInChildren` делается один раз на сцену. Но пулованный `List<GameObject>` на этом самом пути утекает (см. «Грабли»), так что пул растёт на один список на каждую впервые разрешённую сцену.

**Что покрыто тестами.** `EventBusTests.HashCollision_TwoKeysSameBucket_BothInvoked` (строка 72) намеренно берёт ключи 1 и 3, которые коллизируют в таблице ёмкости 2 по умолчанию; `CapacityGrowth_ManyEvents_AllInvokedAfterResize` (строка 117) прогоняет 100 ключей через несколько resize; `SubscribeUnsafe_0Arg_DuplicateKey_ThrowsArgumentException` (строка 410) фиксирует ограничение «один подписчик»; `UnsubscribeUnsafe_0Arg_MissingKey_ThrowsKeyNotFoundException` (строка 503) фиксирует бросок на отписке. Тестов на `EventKeyStore`, на алгоритмы ключей, на `EventKey`, на `Subscription`, на `MonoEventBus` и на `MonoEventBusSingleton` в апстриме **нет** — в частности, утечка пула в `Resolve` и инвалидация в `SetAlgorithm`/`Reset` не покрыты.

---

## Грабли

1. **Уничтожение подписки может отписать всех, кто был до неё.** `EventBus.Subscribe` делает `action = (Action<T>) del + action;` и затем передаёт **этот сложенный** делегат в `new Subscription<T>(this, key, action)` (`EventBus.cs`, строки 61-68). Значит, хендл, который получил N-й подписчик, держит всю цепочку 1..N. Его `Dispose()` вызывает `Unsubscribe(key, вся_цепочка)`, а `Delegate.Remove` вырезает совпадающую подпоследовательность целиком — подписчики 1..N−1 молча исчезают, а если цепочка с тех пор не менялась, слот удаляется совсем. Практическое правило: `using var` безопасен, когда на ключе один подписчик или когда подписки уничтожаются в обратном порядке; при нескольких независимых подписчиках на одном ключе храните свой `Action` и вызывайте сгенерированный `Unsubscribe{Name}(handler)` вместо уничтожения хендла.

2. **`SubscribeUnsafe` — не «быстрый `Subscribe`», а «только один подписчик».** Он вызывает `AddEvent`, который кидает `ArgumentException` («An event with the same key … already has been added in the event bus!»), когда у ключа уже есть делегат. Он никогда не складывает делегаты. `[Unsafe]`-событие с двумя слушателями падает на втором `Subscribe`.

3. **`InvokeUnsafe` кидает, когда никто не подписан**, тогда как безопасный `Invoke` тихо ничего не делает: `GetEventUnsafe` кидает `KeyNotFoundException` на отсутствующем ключе. Перевод события в `[Unsafe]` меняет «нет подписчиков» из no-op в исключение — ровно тот случай, который легко не воспроизвести в тестах.

4. **`UnsubscribeUnsafe` тоже кидает `KeyNotFoundException`** — и это опаснее предыдущего пункта. Все четыре перегрузки начинаются с `GetEventUnsafe<Delegate>(key)`. То есть на `[Unsafe]`-событии повторная или запоздавшая отписка — исключение, а повторные/поздние отписки берутся из порядка разрушения объектов, то есть из самого обычного кода. Зафиксировано тестом `EventBusTests.UnsubscribeUnsafe_0Arg_MissingKey_ThrowsKeyNotFoundException` (строка 503).

5. **`[Unsafe(false)]` не существует.** У `UnsafeAttribute` объявлен только неявный беспараметрический конструктор, поэтому все пять упоминаний `[Unsafe(false)]` в документации — ошибка компиляции CS1729: `Docs/Events/CodeGen/UnsafeAttribute.md` строки 55, 61 и 91, `Docs/Events/CodeGen/EventExtensionsAPIAttribute.md` строки 81 и 135. Обоснование в `UnsafeAttribute.md`, строка 91 («relies on the implicit `bool` conversion supported by the source generator parser»), тоже неверно: никакого преобразования в парсере нет, `HasUnsafeAttribute` вообще не смотрит на аргументы атрибута — только на его имя (`EventAPIParser.cs`, строки 148-163). Генератор считает `fieldUnsafe = classUnsafe || HasUnsafeAttribute(fieldDecl)` (строка 91): поле можно только поднять до unsafe, понизить нельзя. Нужна смесь — два `[EventExtensionsAPI]` класса.

6. **`GetEventUnsafe<T>` не проверяет тип вообще.** `Unsafe.As<Delegate, T>(ref _slots[index].value)` — чистый реинтерпрет. На unsafe-пути несовпадение типа или арности между подпиской и публикацией — это неопределённое поведение, повреждённый вызов, а не `InvalidCastException`. Приведение `(Action<T>) del` на безопасном пути — именно то, что превращает этот класс ошибок в честное исключение; это и есть основной аргумент оставаться на safe-пути вне измеренных горячих мест.

7. **`ThreadSafeEventBus` делает потокобезопасной только публикацию.** `Subscribe`, `Unsubscribe`, `IsSubscribed` и `Dispose` — несинхронизированные проброски в непотокобезопасный `EventBus` (`ThreadSafeEventBus.cs`, строки 22-72). Подписка из рабочего потока, или `Flush` одновременно с подпиской из другого потока, по-прежнему портит массивы слотов и бакетов.

8. **`Flush()` нет в `IEventBus`.** Поле, объявленное как `IEventBus`, нельзя флашить, и никто об этом не предупредит — события просто никогда не выстрелят. Держите конкретный тип `ThreadSafeEventBus` там, где флашите.

9. **Unsafe-событие, опубликованное через `ThreadSafeEventBus`, падает молча.** Поставленный в очередь `InvokeUnsafe` для ключа без подписчиков кидает внутри `Flush`, а `Flush` глотает исключение в `Debug.LogException`. То есть кросс-потоковые unsafe-публикации превращаются в строчку в логе вместо падения. Тест `ThreadSafeEventBusTests.InvokeUnsafe_MissingKey_DoesNotThrowBeforeFlush` (строка 546) проверяет только сторону постановки в очередь.

10. **`EventKeyStore` — глобальное изменяемое состояние без блокировок** вокруг `_nameToId`/`_idToName`. Разрешайте все ключи на главном потоке. Статические инициализаторы `[EventExtensionsAPI]`-класса делают это естественно; `bus.Invoke("SomeName")` из джобы — нет.

11. **`EventKeyStore.SetAlgorithm` аннулирует все уже созданные ключи.** Внутри он вызывает `Reset()`, который чистит оба словаря; существующие `EventKey` сохраняют теперь осиротевшие id. Документация (`Docs/Events/Keys/Manual.md`) говорит об этом как «Switching the algorithm clears the current cache», не раскрывая последствия. Вызов — только в точке входа в приложение, до первого ключа.

12. **Алгоритм по умолчанию игнорирует имя.** `SequentialEventKeyAlgorithm.NameToId(string name) => _nextId++;`. Id зависят от порядка инициализации типов, то есть не стабильны между запусками, билдами, сборками и машинами. Никогда не сериализуйте, не сохраняйте и не отправляйте такой id по сети.

13. **`SequentialEventKeyAlgorithm.Reset()` сбрасывает к константе, а не к аргументу конструктора.** `Reset() => _nextId = INITIAL_ID;`, где `INITIAL_ID == 1`. `new SequentialEventKeyAlgorithm(100)` после сброса начнёт с 1 — вопреки формулировке документации «resets the internal counter back to the initial value».

14. **`SHA256EventKeyAlgorithm` разделяет один `static readonly SHA256`.** `ComputeHash` на общем экземпляре не потокобезопасен. Плюс усечение до `BitConverter.ToInt32(hash, 0)` выбрасывает всю стойкость SHA-256 к коллизиям — то есть по сравнению с FNV-1a не покупается почти ничего при заметно большей цене.

15. **`Fnv1AEventKeyAlgorithm` не проверяет `null`** (в отличие от `SHA256EventKeyAlgorithm`), хотя его XML-документация обещает `ArgumentNullException`. На практике это прикрыто тем, что `EventKeyStore.NameToId` проверяет `null` раньше; но при прямом вызове алгоритма вы получите `NullReferenceException` из `Encoding.UTF8.GetBytes`.

16. **`MonoEventBusSingleton<T>.Resolve(Scene)` утекает пулованный список на успешном пути.** Он делает `return singleton;` изнутри цикла, а `ListPool<GameObject>.Release(gameObjects)` стоит после цикла и достигается только на бросающем пути (`MonoEventBusSingleton.cs`, строки 58-72). Апстримный баг: не копируйте этот паттерн и ожидайте роста пула при разрешении многих сцен.

17. **`Instance` не возвращает уничтоженный компонент, но может вернуть шину не той сцены.** Проверка `if (_instance != null)` идёт по `T : MonoEventBus`, то есть по `UnityEngine.Object`, у которого перегружен `!=`: уничтоженный компонент сообщает о себе как `null`, и геттер провалится в `FindFirstObjectByType`/`FindObjectOfType`. А вот «не та сцена» — реальная проблема: статик пишется в `Awake` тем экземпляром, который проснулся последним. В аддитивных сборках используйте `Resolve`, не `Instance`. Диагностика при неудаче разная: `Instance` кидает `NullReferenceException`, `Resolve` — голый `Exception`.

18. **`default(Subscription).Dispose()` кидает `NullReferenceException`** — `_eventBus` в такой структуре `null`. Не оставляйте поле `Subscription` неинициализированным и не уничтожайте структуру, которую не получили из `Subscribe*`. В `MonoBehaviour` с `OnEnable`/`OnDisable` это значит: присваивайте поле в `OnEnable`, а не «когда-нибудь».

19. **Присваивание `Subscription` переменной, полю или списку типа `IDisposable` боксит структуру** — и выбрасывает весь смысл `readonly struct`. Держите их в типизированных полях (`Subscription<int> _damageSub;`).

20. **`EventKey.Equals(object)` боксит.** Ключи нормально работают как ключи обобщённого словаря (они реализуют `IEquatable<>`), но никогда не сравнивайте и не храните их как `object`.

21. **Арность полезной нагрузки ограничена тремя.** Четвёртый аргумент приходится заворачивать в структуру — и помните, что `in`/`ref` нигде нет, так что «толстая» структура-нагрузка копируется на входе в делегат.

22. **Два ключа в одном объявлении поля молча не генерируются.** `EventAPIParser.Transform` требует `fieldDecl.Declaration.Variables.Count == 1` (строка 65). `public static readonly EventKey<IEventBus> A = new(nameof(A)), B = new(nameof(B));` пропускается без единой диагностики — методов для `A` и `B` просто не будет.

23. **`readonly` генератор не требует.** Проверяется только `static` (строка 62), так что мутабельное статическое поле ключа спокойно сгенерирует методы. Пишите `readonly` сами — это не проверяет никто.

24. **Генератор пропускает фоновый анализ IDE и не-ссылающиеся сборки.** `IsBuildTime = Assembly.GetEntryAssembly() != null`, плюс требование `compilation.ReferencedAssemblyNames.Any(n => n.Name == "Atomic.Events")`. Симптомы: IDE подчёркивает `bus.InvokeDamageDealt(...)` красным до настоящего билда; а при отсутствии ссылки в asmdef методы не появляются никогда. Каждый геймплейный asmdef, объявляющий ключи, обязан ссылаться на `Atomic.Events`.

25. **Hint-name генерируется только из имени класса.** `string hintName = $"{def.ClassName}.g.cs";` — `def.Namespace` игнорируется (`EventAPIGenerator.cs`, строка 89). Два `[EventExtensionsAPI]`-класса с одинаковым именем в разных пространствах имён внутри одной сборки столкнутся на `AddSource`, и вместо генерации вы получите `EAPG0002` (внутренняя ошибка генератора). Имена классов ключей должны быть уникальны в пределах сборки.

26. **Без установленного анализатора `EAPI0001`/`EAPI0002` становятся рантайм-загадками.** Неинициализированный или `new()`-инициализированный ключ получает `Id == 0` и **алиасит все остальные такие события**. Ставьте `EventAPIAnalyzer.dll` в `Assets/Plugins/Atomic/SourceGenerators/` с лейблом `RoslynAnalyzer` и снятыми платформами — иначе эти две ошибки сборки превратятся в «событие приходит не туда».

27. **В `EventKeyStore.cs` незакрытый `using UnityEditor;` на уровне файла** (строка 4) — сам `using` не завёрнут ни в какую директиву, хотя единственный его потребитель, метод `ResetOnEnterPlayMode` вместе с атрибутом, закрыт `#if UNITY_EDITOR` (строки 55-64). В репозитории как есть это компилируется, но сломает плеерный билд в любой конфигурации, где это пространство имён недоступно. Учтите до того, как перенесёте asmdef или добавите ограничения по платформам.

28. **Авто-сброс `EventKeyStore` в редакторе отключается быстрым входом в Play Mode.** `if ((options & EnterPlayModeOptions.DisableDomainReload) != 0) return;` — с включёнными Enter Play Mode Options id и кешированные отображения переживают между сессиями Play. Вызывайте `Reset()` сами, если это важно.

29. **Строковые перегрузки `Extensions` бьют по словарю на каждом вызове.** `it.Invoke(NameToId(key), arg)` — хеш строки плюс проход по словарю на каждую публикацию. Это API для тулинга, тестов и прототипирования.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Документация модуля

| Файл | Что внутри |
|---|---|
| `Docs/Events/Manual.md` | Входная точка модуля (197 строк). Три примера (ключи через генератор, подписка/публикация, кросс-потоковая отправка), затем самый полезный раздел — **Event API Source Generation** со unsafe-режимом, анализатором и, главное, пошаговой инструкцией установки четырёх DLL генераторов (`RoslynAnalyzer`-лейбл, снятые платформы, reimport) и символом `ATOMIC_OUTPUT_SOURCEGEN_FILES` → `Temp/GeneratedCode/`. Заканчивается навигационным индексом по всем остальным файлам и шестью строчками Best Practices. |
| `Docs/Events/Extensions.md` | Самый полный справочник (273 строки) по перегрузкам `Extensions`: сначала строковые и `EventKey`-варианты `Subscribe`/`Invoke`/`Unsubscribe`/`IsSubscribed`/`Dispose`, затем отдельный раздел **Unsafe Methods** с шестью подразделами по unsafe-зеркалу. Сюда идти, когда надо понять, какая именно перегрузка выберется. |

### Шины

| Файл | Что внутри |
|---|---|
| `Docs/Events/Bus/Manual.md` | Обзор шин (96 строк) с тремя примерами: `EventBus` в чистом C#, `MonoEventBus` как компонент (`GameObject > Atomic > Events > Event Bus`) и `ThreadSafeEventBus` с `Flush()` в `Update`. Пример `SampleUsage` отсюда (строки 46-60) — именно тот, который мы разобрали как антипаттерн в «Идиомах». Внизу, в Best Practices (строки 89-96), — список-шпаргалка по выбору шины: `EventBus` для чистого C#, `MonoEventBus` для сценовой проводки в инспекторе, `MonoEventBusSingleton<T>` для глобальных и посценовых шин, `ThreadSafeEventBus` для публикации из фоновых потоков. |
| `Docs/Events/Bus/IEventBus.md` | Контракт интерфейса (158 строк): два примера (беспараметрическое и параметризованное событие) плюс постатейный разбор всех методов. Сам интерфейс целиком — в этой главе выше. |
| `Docs/Events/Bus/EventBus.md` | Справка по реализации по умолчанию (150 строк). Важно: строка 3 описывает хранилище прозой как «a dictionary keyed by integer event IDs» — в коде это массивная хеш-таблица, а не `Dictionary`. Параметра `capacity` в этом файле нет; его вообще нет во всей `Docs/Events`. |
| `Docs/Events/Bus/ThreadSafeEventBus.md` | 159 строк. Единственное место в документации, где отдельным разделом расписаны **оба конструктора** — беспараметрический и с `inner`, — и подчёркнуто, что подписка/отписка идут напрямую в inner, а отложена только публикация. |
| `Docs/Events/Bus/MonoEventBus.md` | 167 строк. Две инструкции — как добавить компонент в сцену и как им пользоваться в коде — и полная справка по проброшенной поверхности, включая `protected virtual void OnDestroy()` (строки 160-163): именно отсюда видно, что компонент сам чистит свою шину. |
| `Docs/Events/Bus/MonoEventBusSingleton.md` | 111 строк, и это самый «идиоматичный» файл в разделе: объявление `public sealed class GameEventBus : MonoEventBusSingleton<GameEventBus>, IGameEventBus { }`, доступ через `Instance` и разрешение через `Resolve(scene)` — три примера, которые стоит скопировать как есть. Про утечку пула в `Resolve` здесь не сказано. |

### Ключи

| Файл | Что внутри |
|---|---|
| `Docs/Events/Keys/Manual.md` | 92 строки, три примера: создание ключей напрямую (`EventKey<IEventBus, int, Vector3> hit = new(nameof(hit));` — показывает, что арность 3 поддерживается), рекомендуемый путь через `[EventExtensionsAPI]`, и подмена алгоритма через `EventKeyStore.SetAlgorithm`. Содержит ту самую сдержанную формулировку «Switching the algorithm clears the current cache» без разбора последствий. В Best Practices — требование звать `EventKeyStore.Reset()` в тестах. |
| `Docs/Events/Keys/EventKey.md` | 208 строк — самая подробная справка по ключам: тип, **оба конструктора** (`string` и `int`), методы и раздел операторов `==`/`!=`. Сюда идти за точной формой структуры. |
| `Docs/Events/Keys/EventKeyStore.md` | 102 строки. Описывает двунаправленное отображение и четыре метода; фиксирует формат `#Unknown:{id}` для незарегистрированного id. Про отсутствие блокировок не говорит. Про авто-сброс говорит, но неточно: строка 101 утверждает, что `[InitializeOnEnterPlayMode]` стоит на самом `Reset()` (в коде атрибут висит на отдельном приватном `ResetOnEnterPlayMode`), и условие `DisableDomainReload` не упоминает вовсе. |
| `Docs/Events/Keys/IEventKeyAlgorithm.md` | 74 строки: контракт из двух методов плюс требование детерминированности и явное указание, что кеширование и обратный поиск — ответственность внешнего `EventKeyStore`, а не алгоритма. |
| `Docs/Events/Keys/SequentialEventKeyAlgorithm.md` | 86 строк. Единственный файл с разделом **Constructors** для алгоритмов (параметр `nextId`). Говорит «Resets the internal counter back to the initial value» — а в коде это константа 1, а не аргумент конструктора. |
| `Docs/Events/Keys/Fnv1AEventKeyAlgorithm.md` | 63 строки. Детерминированный 32-битный FNV-1a; то, что надо включать, если id уходят в сейв или в сеть. |
| `Docs/Events/Keys/SHA256EventKeyAlgorithm.md` | 65 строк. Та же форма справки. Про общий статический `SHA256` и усечение до 4 байт не сказано ни слова. |

### Подписки

| Файл | Что внутри |
|---|---|
| `Docs/Events/Subscriptions/Manual.md` | 69 строк, и два примера из него стоит знать наизусть: блочный `using (bus.Subscribe(1, ...))` с комментариями «Output: Hello!» / «No output», показывающий семантику тихого no-op после отписки, и ручной `subscription.Dispose()`. Best Practices требуют уничтожать подписки в `OnDisable`/`Dispose`/обработчиках разгрузки сцены и не захватывать тяжёлое состояние в анонимные колбэки. |
| `Docs/Events/Subscriptions/Subscription.md` | 63 строки: тип, конструктор, `Dispose`. Примечание о том, что generic-варианты (`Subscription<T>` и далее) существуют, вынесено в Manual, а не сюда. |

### Кодогенерация

| Файл | Что внутри |
|---|---|
| `Docs/Events/CodeGen/EventExtensionsAPIAttribute.md` | 138 строк. Канонический пример класса ключей плюс раздел **Unsafe Mode**: `[EventExtensionsAPI(Unsafe = true)]` на весь класс, `[Unsafe]` на поле — и несуществующий `[Unsafe(false)]` как «safe override» (строки 81 и 135). Читайте с поправкой на пункт 5 «Граблей». |
| `Docs/Events/CodeGen/UnsafeAttribute.md` | 115 строк. Раздел **Opt-in to Unsafe** корректен; раздел **Force Safe Mode** (строки 53-67) целиком не компилируется, а примечание на строке 91 объясняет его несуществующим «implicit `bool` conversion» в парсере. В разделе Type видно, почему это невозможно: у атрибута пустое тело. |
| `Docs/Events/CodeGen/EventAPIAnalyzer.md` | 63 строки — самый короткий файл раздела. Описывает анализатор, называет оба идентификатора (`EAPI0001` — нет инициализатора, `EAPI0002` — `new()`/`default`, строки 60-61) и упоминает автофикс, вставляющий `= new(nameof(FieldName))` (строка 62). Неточность одна: блок Type объявляет тип как `public class EventAPIAnalyzer : DiagnosticAnalyzer` с комментарием «external implementation» (строки 52-53), тогда как в коде это `[DiagnosticAnalyzer(LanguageNames.CSharp)] public sealed class`, и исходник лежит в самом репозитории. |

### Исходный код (когда документации не хватает — а по этому модулю её не хватает регулярно)

| Файл | Зачем открывать |
|---|---|
| `Assets/Plugins/Atomic/Events/Scripts/Bus/Runtime/EventBus.cs` | Единственный источник правды про хранилище, `AggressiveInlining`, `capacity`, рост по `PrimeTable`, free-list и всю разницу между safe и unsafe путями. 532 строки; раздел `#region Storage` начинается на 267-й. |
| `Assets/Plugins/Atomic/Events/Scripts/Bus/Runtime/Extensions.cs` | 226 строк, 58 методов: 26 строковых (включая полное unsafe-зеркало) и 32 на `EventKey`. Открывать, чтобы убедиться, что у `IsSubscribed` и `Dispose` по четыре перегрузки на арность ключа. |
| `Assets/Plugins/Atomic/Events/Scripts/Bus/Runtime/IEventBus.cs` | Короткий файл, весь контракт целиком. |
| `Assets/Plugins/Atomic/Events/Scripts/Bus/Runtime/ThreadSafeEventBus.cs` | Строки 74-102: восемь аллоцирующих `Enqueue` и `Flush` с проглатыванием исключений. |
| `Assets/Plugins/Atomic/Events/Scripts/Bus/Unity/MonoEventBusSingleton.cs` | 77 строк. Строки 14-33 — реальная логика `Instance`; строки 53-73 — `Resolve` с утечкой пула. |
| `Assets/Plugins/Atomic/Events/Scripts/Bus/Unity/MonoEventBus_Debug.cs` | Весь файл под `#if UNITY_EDITOR`, а инспекторная часть — ещё и под `#if ODIN_INSPECTOR`. Открывать, чтобы понять, что инспектор показывает (имя, id, число подписчиков) и что без Odin не показывает ничего. |
| `Assets/Plugins/Atomic/Events/Scripts/Keys/EventKeyStore.cs` | 76 строк. Строка 4 — незакрытый `using UnityEditor;`. Строки 55-64 — условие авто-сброса при входе в Play Mode. |
| `Assets/Plugins/Atomic/Events/Scripts/Keys/Keys/EventKey.cs` и ``EventKey`1.cs`` / ``EventKey`2.cs`` / ``EventKey`3.cs`` | Четыре почти идентичных файла; подтверждают `readonly struct`, публичное readonly поле `Id` и наличие `EventKey(int id)` во всех четырёх. |
| `Assets/Plugins/Atomic/Events/Scripts/Keys/Algorithms/` | Четыре файла: интерфейс и три реализации. Все дефекты из «Граблей» (игнорирование имени, `INITIAL_ID`, общий `SHA256`, отсутствие null-проверки в FNV-1a) видны здесь сразу. |
| `Assets/Plugins/Atomic/Events/Scripts/Subscription/Subscription.cs` + ``Subscription`1..3.cs`` | По 19 строк каждый. Подтверждают `readonly struct`, `internal` конструктор и то, что `Dispose` зовёт именно безопасный `Unsubscribe`. |
| `Assets/Plugins/Atomic/Events/Scripts/Utils/InternalUtils.cs` | `PrimeTable` (31 значение, от 2 до 1 610 612 741) и `CeilToPrime`. Нужен, чтобы посчитать, во что развернётся ваш `capacity`. |
| `Assets/Plugins/Atomic/Events/Scripts/Atomic.Events.asmdef` | Три строки: `{ "name": "Atomic.Events", "allowUnsafeCode": true }`. Без ограничений по платформам — поэтому `using UnityEditor;` в `EventKeyStore.cs` сегодня и компилируется. |
| `Assets/Plugins/Atomic/Events/Tests/EventBusTests.cs` | Основной набор тестов. Полезные ориентиры: `HashCollision_TwoKeysSameBucket_BothInvoked` (72), `CapacityGrowth_ManyEvents_AllInvokedAfterResize` (117), `FreeListReuse_AfterUnsubscribe_NewKeyUsesSlot` (158), `SubscribeUnsafe_0Arg_DuplicateKey_ThrowsArgumentException` (410), `UnsubscribeUnsafe_0Arg_MissingKey_ThrowsKeyNotFoundException` (503). Читать как спецификацию намеренного поведения. |
| `Assets/Plugins/Atomic/Events/Tests/EventBusTests_Extended.cs`, `ThreadSafeEventBusTests.cs` | Второй набор и тесты декоратора; в последнем — `Constructor_WithInner_DoesNotThrow` (28) и `InvokeUnsafe_MissingKey_DoesNotThrowBeforeFlush` (546). |

### Генератор и анализатор

| Файл | Зачем открывать |
|---|---|
| `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIGenerator/EventAPIParser.cs` | 176 строк — здесь живут все правила «почему мои методы не сгенерировались»: `IsCandidate` (24-32), требования к полю (62-84), вычисление unsafe (91). |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIGenerator/CodeEmitter.cs` | 153 строки. `Emit` печатает `public static partial class {ClassName}` (41) — отсюда требование `partial`. `EmitEventRegion` (53-109) задаёт форму всех пяти методов, выбор safe/unsafe (66-68) и то, что `IsSubscribed`/`Dispose` всегда безопасны. |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIGenerator/EventAPIGenerator.cs` | 109 строк. `ShouldRun` (36-48) — три условия пропуска. Строка 89 — hint-name из одного имени класса (коллизия между пространствами имён). Строки 97-103 — `EAPG0001`/`EAPG0002`. |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIGenerator/Models/EventField.cs`, `Models/EventAPIDefinition.cs` | Модели, которыми парсер и эмиттер обмениваются. Короткие; нужны, если будете править генератор. |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIAnalyzer/EventAPIAnalyzer.cs` | 126 строк: `[DiagnosticAnalyzer(LanguageNames.CSharp)] public sealed class EventAPIAnalyzer : DiagnosticAnalyzer`. `IsParameterlessOrDefault` (строка 112) — точный список того, что считается недопустимым инициализатором: `default`, `default(T)`, `new T()` и `new()` с пустым списком аргументов. |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIAnalyzer/EventAPIDiagnostics.cs` | Единственное место, где написаны `EAPI0001` и `EAPI0002` с их текстами и `DiagnosticSeverity.Error`. |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/EventAPIAnalyzer/EventAPICodeFixProvider.cs` | Автофикс к этим диагностикам — то есть в IDE обе ошибки можно починить одним Alt+Enter. Сам факт автофикса документация упоминает (`Docs/Events/CodeGen/EventAPIAnalyzer.md`, строка 62), но этот файл — единственный источник правды про то, что именно он вставляет. |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/Docs/EventAPIGenerator.md`, `Project~/Docs/EventAPIAnalyzer.md` | Документация генераторов, лежащая **рядом с их исходниками**, а не в `Docs/Events/CodeGen/`. Легко пропустить. |
| `Assets/Plugins/Atomic/SourceGenerators/EventAPIGenerator.dll`, `EventAPIAnalyzer.dll` | Собранные артефакты, которые надо разложить по инструкции из `Docs/Events/Manual.md` (раздел Setup). Рядом лежат три DLL для Entities — `EntityAPIGenerator.dll`, `EntityAPIAnalyzer.dll`, `EntityDomainGenerator.dll`. |
| `Assets/Plugins/Atomic/SourceGenerators/Project~/Atomic.SourceGenerators.Shared/` | `CodeWriter.cs`, `DiagnosticLogger.cs`, `SourceOutputHelpers.cs` — общая инфраструктура. `SourceOutputHelpers` читает `ATOMIC_OUTPUT_SOURCEGEN_FILES`; открывать, если дамп в `Temp/GeneratedCode/` не появляется. |

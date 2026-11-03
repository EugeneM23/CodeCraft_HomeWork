# 15. Кодогенерация: source generators

Вся типизированная обвязка вокруг состояния сущностей и вокруг шины событий в новом Atomic пишется не руками и не редакторным мастером, а тремя Roslyn-генераторами (`EntityAPIGenerator`, `EventAPIGenerator`, `EntityDomainGenerator`) и двумя анализаторами (`EntityAPIAnalyzer`, `EventAPIAnalyzer`). Декларацией является обычное статическое поле C#: объявили `ValueKey<IEntity, int> Health` — получили `entity.GetHealth()`, `entity.AddHealth(...)`, `entity.SetHealth(...)`; объявили `EventKey<IEventBus, int> DamageDealt` — получили `bus.InvokeDamageDealt(10)` и `bus.SubscribeDamageDealt(...)`. Старый пайплайн с `.atomic`/yaml-файлами и окном редактора в репозитории отсутствует целиком: папки `Assets/Plugins/Atomic/Entities/Editor/EntityAPI` нет, в `Editor/` остался только `Inspector/`, а генерация делается пятью предсобранными DLL в `Assets/Plugins/Atomic/SourceGenerators/`.

Для миграции здесь два практических следствия. Первое — жёсткий барьер по версии: пайплайн рассчитан на Unity 6 (6000.0 LTS+) с её Roslyn 4.3.0, под Unity 2022.3 он не работает. Второе — сгенерированный код живёт в памяти компилятора, поэтому «посмотреть, что получилось» можно только через дефайн `ATOMIC_OUTPUT_SOURCEGEN_FILES`, и именно это стоит включить на время переноса, чтобы дифать новый API против старого yaml-генерированного метод в метод.

---

## Модель

Пять фактов, без которых API читается неправильно.

**1. Тип сущности живёт в ключе, а не в атрибуте.** `[EntityExtensionsAPI]` больше не принимает `typeof(...)`. Генератор берёт тип-получатель расширения из **первого** generic-аргумента каждого ключа (`EntityAPIParser.Transform`: `namedType.TypeArguments[0].ToDisplayString()`). Поэтому один API-класс может обслуживать несколько интерфейсов сущностей одновременно, а «контекст» из старого `Atomic.Contexts` — это просто ещё один интерфейс сущности (`public interface IPlayerContext : IEntity {}`) со своими ключами.

**2. Имя превращается в `int` один раз, и этот `int` нестабилен.** Конструктор ключа зовёт `EntityKeyStore.NameToId(name)` / `EventKeyStore.NameToId(name)`. Алгоритм по умолчанию — `SequentialEntityKeyAlgorithm`, и его `NameToId(string name) => _nextId++` **игнорирует имя**. Id зависит от порядка инициализации статических полей, а не от строки. Id нельзя сериализовать, кладти в сейв или отправлять по сети. Имя сохраняется только для обратного маппинга `IdToName` (он же используется в текстах исключений) и возвращает `#Unknown:{id}` для незарегистрированного id.

**3. Сгенерированный метод — это однострочный форвард на `int`-перегрузку.** В рантайме нет ни строк, ни словарей по имени: `entity.GetValue<int>(PlayerAPI.Health.Id)`. `Id` — это `public readonly int` **поле** структуры ключа (не свойство), сами ключи — `readonly struct`.

**4. Генераторы не работают в IDE.** `EntityAPIGenerator.IsBuildTime = Assembly.GetEntryAssembly() != null`, и `ShouldRun` возвращает `false`, когда `!IsBuildTime`. Новые методы появятся в IntelliSense только после реального билда/перезагрузки домена. Плюс два других ранних выхода: компиляция не должна быть самой `Atomic.Entities`/`Atomic.Events` и обязана на неё ссылаться — asmdef без ссылки получит ноль сгенерированного кода молча.

**5. Генератор молчалив, анализатор — нет.** Поле, которое не подошло под правила парсера (не `static`, два объявления в одной строке, не тот namespace, не та арность), **пропускается без диагностики**: симптом — отсутствующий метод расширения, а не ошибка сборки. Единственное, что ловится в явном виде, — незаинициализированный ключ: `EAPI0001`/`EAPI0002`, оба с `DiagnosticSeverity.Error`.

Схема конвейера одинакова у всех трёх генераторов: `CreateSyntaxProvider(IsCandidate, Transform)` → `Where(def.HasValue)` → `Select` → `Collect().Combine(CompilationProvider).Combine(ParseOptionsProvider)` → `RegisterSourceOutput`. `ForAttributeWithMetadataName` не используется сознательно: он требует Roslyn ≥ 4.6, а Unity 6000.3.x несёт 4.3.0 (`SourceGenerators/Project~/Docs/Implementation.md`, раздел «Roslyn 4.3.0 Compatibility», строка 121).

---

## API

### Маркерные атрибуты (лежат в рантайм-сборках, не в генераторах)

```csharp
// Assets/Plugins/Atomic/Entities/Scripts/Codegen/EntityExtensionsAPIAttribute.cs
namespace Atomic.Entities
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EntityExtensionsAPIAttribute : Attribute
    {
        public bool Unsafe { get; set; }
        public bool AggressiveInlining { get; set; } = true;

        public EntityExtensionsAPIAttribute() { }
    }
}
```
Ставится на класс, который держит ключи сущностей. `Unsafe` переводит **все** value-поля класса на `GetValueUnsafe<T>` и добавляет `Ref{Name}`; `AggressiveInlining` (по умолчанию `true`) управляет выдачей `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.

```csharp
// Assets/Plugins/Atomic/Entities/Scripts/Codegen/UnsafeAttribute.cs
namespace Atomic.Entities
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class UnsafeAttribute : Attribute
    {
    }
}
```
Пофайловый opt-in в unsafe-режим для одного value-поля. **Только безаргументный конструктор** — `[Unsafe(false)]` из документации не компилируется (см. «Грабли»).

```csharp
// Assets/Plugins/Atomic/Entities/Scripts/Codegen/EntityDomainBehavioursAttribute.cs
namespace Atomic.Entities
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EntityDomainBehavioursAttribute : Attribute
    {
        public Type EntityType { get; }

        public EntityDomainBehavioursAttribute(Type entityType)
        {
            this.EntityType = entityType;
        }
    }
}
```
Ставится на любой класс-маркер (статический или нет). Используются только его namespace и аргумент-тип. `EntityDomainParser.ImplementsIEntity` требует, чтобы аргумент был интерфейсом, классом или структурой и был `Atomic.Entities.IEntity` либо имел его в `AllInterfaces`; иначе `Transform` возвращает `null` и не генерируется ничего — без диагностики.

```csharp
// Assets/Plugins/Atomic/Events/Scripts/CodeGen/EventExtensionsAPIAttribute.cs
namespace Atomic.Events
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EventExtensionsAPIAttribute : Attribute
    {
        public bool Unsafe { get; set; }

        public EventExtensionsAPIAttribute() { }
    }
}
```
У событийного атрибута `AggressiveInlining` **нет**: в `EventAPIGenerator/CodeEmitter.cs` нет ни константы с `[MethodImpl]`, ни её выдачи. `Atomic.Events.UnsafeAttribute` — точная копия entity-версии, тоже без bool-конструктора.

### Ключи

```csharp
// Entities/Scripts/KeyStore/Keys/TagKey'1.cs
public readonly struct TagKey<E> : IEquatable<TagKey<E>> where E : IEntity
{
    public readonly int Id;

    public TagKey(string name) => Id = EntityKeyStore.NameToId(name);
    public TagKey(int id) => this.Id = id;

    public override string ToString() => EntityKeyStore.IdToName(Id);
    public bool Equals(TagKey<E> other) => Id == other.Id;
    public override bool Equals(object obj) => obj is TagKey<E> other && Equals(other);
    public override int GetHashCode() => Id;
}
```
Рабочая форма тега. Негенерик `TagKey` (файл `TagKey.cs`) имеет ту же структуру без ограничения `where E : IEntity`; парсер трактует арность 0 как `Atomic.Entities.IEntity`.

```csharp
// Entities/Scripts/KeyStore/Keys/ValueKey'1.cs
public readonly struct ValueKey<E, T> : IEquatable<ValueKey<E, T>> where E : IEntity
{
    public readonly int Id;

    public ValueKey(string name) => Id = EntityKeyStore.NameToId(name);
    public ValueKey(int id) => this.Id = id;

    public override string ToString() => EntityKeyStore.IdToName(Id);
    public bool Equals(ValueKey<E, T> other) => Id == other.Id;
    public override bool Equals(object obj) => obj is ValueKey<E, T> other && Equals(other);
    public override int GetHashCode() => Id;
}
```
Двухаргументная форма — та, которую стоит использовать всегда. Односоставная `ValueKey<T>` (`ValueKey.cs`) существует, и парсер читает её арность 1 как **тип значения** на `Atomic.Entities.IEntity`. Поэтому `ValueKey<IEntity>` означает «значение типа `IEntity` на `IEntity`», а не «ключ для `IEntity`».

```csharp
// Events/Scripts/Keys/Keys/EventKey`2.cs
public readonly struct EventKey<TBus, TArg1, TArg2> : IEquatable<EventKey<TBus, TArg1, TArg2>>
    where TBus : IEventBus
{
    public readonly int Id;

    public EventKey(string name) => Id = EventKeyStore.NameToId(name);
    public EventKey(int id) => Id = id;

    public static bool operator ==(EventKey<TBus, TArg1, TArg2> left, EventKey<TBus, TArg1, TArg2> right) => left.Equals(right);
    public static bool operator !=(EventKey<TBus, TArg1, TArg2> left, EventKey<TBus, TArg1, TArg2> right) => !left.Equals(right);
}
```
Четыре арности: `EventKey<TBus>`, `EventKey<TBus, TArg>`, `EventKey<TBus, TArg1, TArg2>`, `EventKey<TBus, TArg1, TArg2, TArg3>` — то есть максимум три аргумента полезной нагрузки. Событийные ключи, в отличие от entity-ключей, определяют `==`/`!=`.

### Хранилища имён и алгоритмы

```csharp
// Entities/Scripts/KeyStore/EntityKeyStore.cs
public static class EntityKeyStore
{
    public static void SetAlgorithm(IEntityKeyAlgorithm algorithm); // ArgumentNullException на null; чистит оба кэша
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NameToId(string name);                        // ArgumentNullException на null
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string IdToName(int id);                          // "#Unknown:{id}", если не зарегистрирован
    public static void Reset();                                     // чистит кэши + _algorithm.Reset()
}

public interface IEntityKeyAlgorithm
{
    int NameToId(string name);
    void Reset();
}

public sealed class SequentialEntityKeyAlgorithm : IEntityKeyAlgorithm
{
    public SequentialEntityKeyAlgorithm(int nextId = 1);
    public int NameToId(string name) => _nextId++;   // name игнорируется
    public void Reset() => _nextId = 1;
}
```
Внутри — два `Dictionary` без блокировок, так что создание ключей не потокобезопасно. В редакторе `[InitializeOnEnterPlayMode]` зовёт `Reset()`, если domain reload **не** отключён.

Асимметрия, важная для миграции: на стороне событий алгоритмов три, на стороне сущностей — один.

| Сторона | Файлы алгоритмов |
|---|---|
| `Atomic.Entities` | `IEntityKeyAlgorithm`, `SequentialEntityKeyAlgorithm` |
| `Atomic.Events` | `IEventKeyAlgorithm`, `SequentialEventKeyAlgorithm`, `Fnv1AEventKeyAlgorithm`, `SHA256EventKeyAlgorithm` |

Если команде нужны детерминированные по имени id на стороне сущностей — свой `IEntityKeyAlgorithm` придётся написать самим.

### Рантайм-поверхность, в которую разворачивается сгенерированный код

```csharp
// Entities/Scripts/Entities/Interface/IEntity_Values.cs
public partial interface IEntity
{
    event Action<IEntity, int, object> OnValueAdded;
    event Action<IEntity, int, object> OnValueDeleted;
    event Action<IEntity, int, object> OnValueChanged;
    int ValueCount { get; }

    T GetValue<T>(int key);
    ref T GetValueUnsafe<T>(int key) where T : class;
    object GetValue(int key);
    bool TryGetValue<T>(int key, out T value);
    bool TryGetValueUnsafe<T>(int key, out T value) where T : class;
    bool TryGetValue(int key, out object value);
    void SetValue(int key, object value);
    bool HasValue(int key);
    void AddValue(int key, object value);
    bool DelValue(int key);
    void ClearValues();
    KeyValuePair<int, object>[] GetValues();
    int CopyValues(KeyValuePair<int, object>[] results);
    IEnumerator<KeyValuePair<int, object>> GetValueEnumerator();
}
```
Два ограничения здесь определяют почти всё остальное. `GetValueUnsafe<T>`/`TryGetValueUnsafe<T>` ограничены `where T : class` — **unsafe-режим недоступен для значений-структур**. `AddValue`/`SetValue` принимают `object` — значения-структуры боксируются на каждой записи, `GetValue<T>` распаковывает их через `(T) slot.value`.

```csharp
// Entities/Scripts/Entities/Interface/IEntity_Tags.cs
public partial interface IEntity
{
    event Action<IEntity, int> OnTagAdded;
    event Action<IEntity, int> OnTagDeleted;
    int TagCount { get; }

    bool HasTag(int key);
    bool AddTag(int key);
    bool DelTag(int key);
    void ClearTags();
    int[] GetTags();
    int CopyTags(int[] results);
    IEnumerator<int> GetTagEnumerator();
}
```
`ClearTags`, `GetTags`, `CopyTags`, `GetTagEnumerator` (и их value-аналоги) генератором **не покрываются** — обход состояния сущности пишется руками.

```csharp
// Events/Scripts/Bus/Runtime/IEventBus.cs
public interface IEventBus : IDisposable
{
    Subscription Subscribe(int key, Action action);
    Subscription<T> Subscribe<T>(int key, Action<T> action);
    Subscription<T1, T2> Subscribe<T1, T2>(int key, Action<T1, T2> action);
    Subscription<T1, T2, T3> Subscribe<T1, T2, T3>(int key, Action<T1, T2, T3> action);

    Subscription SubscribeUnsafe(int key, Action action);
    // … те же четыре арности для SubscribeUnsafe / Unsubscribe / UnsubscribeUnsafe / Invoke / InvokeUnsafe

    bool IsSubscribed(int key);
    bool Dispose(int key);
}
```
`IsSubscribed` и `Dispose(int)` существуют только в безопасном варианте — поэтому сгенерированные `IsSubscribed{Name}`/`Dispose{Name}` всегда безопасны, даже в unsafe-классе.

Семантика safe/unsafe в `EventBus` (`Events/Scripts/Bus/Runtime/EventBus.cs`):

```csharp
public Subscription Subscribe(int key, Action action)
{
    if (TryGetEvent<Delegate>(key, out Delegate del))
        action = (Action) del + action;      // КОМБИНИРУЕТ с существующим делегатом

    SetEvent(key, action);
    return new Subscription(this, key, action);
}

public Subscription SubscribeUnsafe(int key, Action action)
{
    AddEvent(key, action);                   // AddEvent БРОСАЕТ ArgumentException, если ключ уже есть
    return new Subscription(this, key, action);
}

public void Invoke(int key)
{
    if (TryGetEvent<Action>(key, out Action del))
        del.Invoke();
}

public void InvokeUnsafe(int key) =>
    GetEventUnsafe<Action>(key).Invoke();    // KeyNotFoundException, если подписчиков нет
```

```csharp
// Events/Scripts/Subscription/Subscription.cs
public readonly struct Subscription : IDisposable
{
    internal Subscription(IEventBus eventBus, int key, Action action);
    public void Dispose() => _eventBus.Unsubscribe(_key, _action);
}
```
`Subscription<T>`, `Subscription<T1,T2>`, `Subscription<T1,T2,T3>` — такие же `readonly struct`. Заметьте: `Dispose()` зовёт **безопасный** `Unsubscribe`, то есть дескриптор подписки не бросает исключение даже у unsafe-события.

### Типизированный путь без генератора

Это не альтернатива на будущее, а работающий сегодня API — и аварийный выход, если DLL генераторов не подхватились.

```csharp
// Entities/Scripts/Entities/Extensions_Tags.cs
public static bool AddTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static bool HasTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static bool DelTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static void AddTags<E>(this E entity, IEnumerable<TagKey<E>> tags) where E : IEntity;
public static bool HasAllTags<E>(this E entity, params TagKey<E>[] tags) where E : IEntity;
public static bool HasAnyTag<E>(this E entity, params TagKey<E>[] tags) where E : IEntity;

// Entities/Scripts/Entities/Extensions_Values.cs
public static T GetValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static bool TryGetValue<E, T>(this E entity, ValueKey<E, T> key, out T value) where E : IEntity;
public static void AddValue<E, T>(this E entity, ValueKey<E, T> key, T value) where E : IEntity;
public static void SetValue<E, T>(this E entity, ValueKey<E, T> key, T value) where E : IEntity;
public static bool HasValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static bool DelValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static T GetValueUnsafe<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity where T : class;
```
`HasAllTags`/`HasAnyTag` сгенерированного аналога не имеют вообще — только этот путь. И обратите внимание на последнюю строку: двухгенериковый `GetValueUnsafe<E, T>` возвращает `T`, а не `ref T` (одногенериковые перегрузки на строках 138 и 142 того же файла — `ref T`). `ref` через типизированный ключ даёт только сгенерированный `Ref{Name}`.

```csharp
// Events/Scripts/Bus/Runtime/Extensions.cs
public static Subscription Subscribe<TBus>(this TBus it, EventKey<TBus> key, Action action) where TBus : IEventBus;
public static Subscription<T> Subscribe<TBus, T>(this TBus it, EventKey<TBus, T> key, Action<T> action) where TBus : IEventBus;
public static void Invoke<TBus, T1, T2, T3>(this TBus it, EventKey<TBus, T1, T2, T3> key, T1 arg1, T2 arg2, T3 arg3) where TBus : IEventBus;
// + Unsubscribe / SubscribeUnsafe / InvokeUnsafe / UnsubscribeUnsafe / IsSubscribed / Dispose,
//   и строковые варианты всех перечисленных
```
Единственный способ работать с ключом, лежащим в переменной или прокинутым через generic-параметр: сгенерированное имя метода этого выразить не может.

```csharp
// Events/Scripts/Bus/Runtime/ThreadSafeEventBus.cs
public class ThreadSafeEventBus : IEventBus
{
    public ThreadSafeEventBus();
    public ThreadSafeEventBus(IEventBus inner);
    public void Flush();
}
```
Обёртка над `IEventBus` с `ConcurrentQueue<Action>`. Важно для выбора типа ключа: расширения генерируются на тип из первого generic-аргумента, поэтому `EventKey<IEventBus, …>` остаётся применимым и к `EventBus`, и к `ThreadSafeEventBus`, а ключ, привязанный к конкретному `EventBus`, — нет.

### Что именно генерируется

Для каждого `TagKey`/`TagKey<E>` поля (`EntityAPIGenerator/CodeEmitter.cs`, `EmitTagRegion`):

| Метод | Тело |
|---|---|
| `Has{Name}Tag(this E entity)` → `bool` | `entity.HasTag({Class}.{Name}.Id)` |
| `Add{Name}Tag(this E entity)` → `bool` | `entity.AddTag({Class}.{Name}.Id)` |
| `Del{Name}Tag(this E entity)` → `bool` | `entity.DelTag({Class}.{Name}.Id)` |

Для каждого `ValueKey<T>`/`ValueKey<E,T>` поля (`EmitValueRegion`), в этом порядке:

| Метод | Safe | Unsafe |
|---|---|---|
| `Get{Name}(this E)` → `T` | `entity.GetValue<T>(key)` | `entity.GetValueUnsafe<T>(key)` |
| `Ref{Name}(this E)` → `ref T` | — | `ref entity.GetValueUnsafe<T>(key)` |
| `TryGet{Name}(this E, out T)` → `bool` | `entity.TryGetValue(key, out value)` | `entity.TryGetValueUnsafe(key, out value)` |
| `Add{Name}(this E, T)` → `void` | `entity.AddValue(key, value)` | то же |
| `Has{Name}(this E)` → `bool` | `entity.HasValue(key)` | то же |
| `Del{Name}(this E)` → `bool` | `entity.DelValue(key)` | то же |
| `Set{Name}(this E, T)` → `void` | `entity.SetValue(key, value)` | то же |

Значения выдаются **до** тегов, каждый ключ получает свой `#region {Name}`, заголовок файла — `/**\n * Code generation. Don't modify!\n **/`, всегда пишется `using Atomic.Entities;`, а `using System.Runtime.CompilerServices;` — только когда `AggressiveInlining` включён.

Для каждого `EventKey<…>` поля (`EventAPIGenerator/CodeEmitter.cs`):

| Метод | Safe | Unsafe |
|---|---|---|
| `Subscribe{Name}(this TBus, Action<…>)` → `Subscription<…>` | `bus.Subscribe<…>(key, action)` | `bus.SubscribeUnsafe<…>(key, action)` |
| `Unsubscribe{Name}(this TBus, Action<…>)` → `void` | `bus.Unsubscribe<…>(key, action)` | `bus.UnsubscribeUnsafe<…>(key, action)` |
| `Invoke{Name}(this TBus, T1 arg1, …)` → `void` | `bus.Invoke<…>(key, arg1, …)` | `bus.InvokeUnsafe<…>(key, arg1, …)` |
| `IsSubscribed{Name}(this TBus)` → `bool` | `bus.IsSubscribed(key)` | то же (unsafe-варианта нет) |
| `Dispose{Name}(this TBus)` → `bool` | `bus.Dispose(key)` | то же (unsafe-варианта нет) |

Usings событийного файла — `using Atomic.Events;` и `using System;`.

Для каждого `[EntityDomainBehaviours(typeof(X))]` — девять интерфейсов (список `BehaviourVariants` лежит в `EntityDomainParser.cs`, не в эмиттере). Имя = `{entityType.Name}{Suffix}`:

```csharp
public interface IPlayerBehaviour : IEntityBehaviour { }
public interface IPlayerTick      : IEntityTick<IPlayer>,      IPlayerBehaviour { }
public interface IPlayerFixedTick : IEntityFixedTick<IPlayer>, IPlayerBehaviour { }
public interface IPlayerLateTick  : IEntityLateTick<IPlayer>,  IPlayerBehaviour { }
public interface IPlayerInit      : IEntityInit<IPlayer>,      IPlayerBehaviour { }
public interface IPlayerEnable    : IEntityEnable<IPlayer>,    IPlayerBehaviour { }
public interface IPlayerDisable   : IEntityDisable<IPlayer>,   IPlayerBehaviour { }
public interface IPlayerDispose   : IEntityDispose<IPlayer>,   IPlayerBehaviour { }
public interface IPlayerGizmos    : IEntityGizmos<IPlayer>,    IPlayerBehaviour { }
```
Методы, которые надо реализовать, берутся из generic-интерфейсов `Atomic.Entities` (`Entities/Scripts/Behaviours/`): `Init(E)`, `Enable(E)`, `Tick(E, float)`, `FixedTick(E, float)`, `LateTick(E, float)`, `Disable(E)`, `Dispose(E)`, `DrawGizmos(E)`. Обратите внимание: у `Gizmos`-варианта метод называется `DrawGizmos`, а не `Gizmos`.

### Диагностики

| Id | Кто выдаёт | Severity | Смысл |
|---|---|---|---|
| `EAPI0001` | `EntityAPIAnalyzer` и `EventAPIAnalyzer` | `Error` | У поля-ключа нет инициализатора |
| `EAPI0002` | `EntityAPIAnalyzer` и `EventAPIAnalyzer` | `Error` | Ключ инициализирован через `new()` или `default` |
| `EAG0001` / `EAG0002` | `EntityAPIGenerator` | `Info` / `Error` | Трассировка генерации / внутренняя ошибка |
| `EAPG0001` / `EAPG0002` | `EventAPIGenerator` | `Info` / `Error` | То же для событий |
| `EDG0001` / `EDG0002` | `EntityDomainGenerator` | `Info` / `Error` | То же для доменных интерфейсов |

`EAPI0001`/`EAPI0002` **общие** для обоих анализаторов — различаются только `category` (`"Atomic.Entities"` vs `"Atomic.Events"`). У обоих есть code fix, который вставляет `= new(nameof(FieldName))`, и `GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer` — то есть Fix All по документу/проекту/решению.

### Инфраструктура генераторов (нужна, если пишете свой)

```csharp
// Project~/Atomic.SourceGenerators.Shared/CodeWriter.cs
internal ref struct CodeWriter
{
    public CodeWriter();                               // new StringBuilder(1024)
    public CodeWriter Indent();
    public CodeWriter Dedent();
    public CodeWriter Line(string text);
    public CodeWriter Line();
    public CodeWriter Open(string text = "{");
    public CodeWriter Close(string text = "}");
    public CodeWriter Raw(string text);
    public CodeWriter RawLine(string text);
    public readonly string Result { get; }
}
```
`ref struct`, отступы — табами. Именно из-за `ref struct` все хелперы эмиттеров принимают `ref CodeWriter w`.

```csharp
// Project~/Atomic.SourceGenerators.Shared/SourceOutputHelpers.cs
internal static class SourceOutputHelpers
{
    public static void Setup(ParseOptions parseOptions);   // читает ATOMIC_OUTPUT_SOURCEGEN_FILES
    public static void OutputSourceToFile(string assemblyName, string fileName, Func<string> sourceTextProvider);
    public static void LogInfo(string message);            // Temp/GeneratedCode/SourceGen.log
}
```
Путь считается как **относительный** `Path.Combine("Temp", "GeneratedCode")` от рабочего каталога компилятора; `IOException`/`UnauthorizedAccessException` глотаются молча.

```csharp
// Project~/Atomic.SourceGenerators.Shared/DiagnosticLogger.cs
internal sealed class DiagnosticLogger : IDisposable
{
    public DiagnosticLogger(SourceProductionContext context, string className, string category);
    public void LogError(string code, string title, string message, Location? location = null);
    public void LogWarning(string code, string title, string message, Location? location = null);
    public void LogInfo(string code, string title, string message, Location? location = null);
    public void Dispose();   // репортит накопленное, затем чистит
}
```
Каждое сообщение префиксуется `[{ClassName}]`.

Модели конвейера — `readonly struct` с ручным `IEquatable<T>`:

```csharp
// Project~/EntityAPIGenerator/Models/EntityAPIDefinition.cs
public readonly struct EntityAPIDefinition : IEquatable<EntityAPIDefinition>
{
    public string Namespace { get; }
    public string ClassName { get; }
    public bool Unsafe { get; }
    public bool AggressiveInlining { get; }
    public IReadOnlyList<ValueField> Values { get; }
    public IReadOnlyList<TagField> Tags { get; }

    public EntityAPIDefinition(string ns, string className, bool unsafeFlag, bool aggressiveInlining,
        IReadOnlyList<ValueField> values, IReadOnlyList<TagField> tags);
}

// Project~/EntityAPIGenerator/Models/ValueField.cs
public readonly struct ValueField : IEquatable<ValueField>
{
    public string Name { get; }
    public string EntityTypeName { get; }
    public string ValueTypeName { get; }
    public bool IsUnsafe { get; }

    public ValueField(string name, string entityTypeName, string valueTypeName, bool isUnsafe);
}
```
В моделях нет ни одного `ISymbol`/`SyntaxNode` — только строки и bool.

---

## Идиомы

### 1. Объявление API-класса: `public static partial` + `static readonly` + `nameof`

```csharp
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    [EntityExtensionsAPI]
    public static partial class PlayerAPI
    {
        public static readonly TagKey<IEntity> Alive = new(nameof(Alive));
        public static readonly TagKey<IEntity> Dead = new(nameof(Dead));

        public static readonly ValueKey<IEntity, int> Health = new(nameof(Health));
        public static readonly ValueKey<IEntity, float> Speed = new(nameof(Speed));
        public static readonly ValueKey<IPlayerContext, Camera> Camera = new(nameof(Camera));
    }
}
```
Почему именно так. `public static partial` — потому что эмиттер безусловно пишет `public static partial class {def.ClassName}`, и никакой диагностики на несоответствие нет (`internal` даст `CS0262`, отсутствие `partial` — `CS0260`, причём на сгенерированном файле). `static readonly` — потому что парсер требует только `static`, но ключ, который кто-то переприсвоит, сломает все уже скомпилированные вызовы. `nameof` — единственная форма, которую вставляет code fix, и единственная, которая не разъедется при переименовании поля. Последняя строка показывает главное свойство новой схемы: ключ `Camera` объявлен в том же классе, но привязан к `IPlayerContext`, поэтому `GetCamera()` появится только на `IPlayerContext`.

### 2. Вызовы — только через сгенерированные расширения

```csharp
IEntity entity = new Entity(name: "Player", tagCapacity: 4, valueCapacity: 8);

entity.AddAliveTag();
if (entity.HasAliveTag())
    entity.DelAliveTag();

entity.AddHealth(100);          // первая запись
entity.SetHealth(80);           // обновление
int health = entity.GetHealth();

if (entity.TryGetHealth(out int current))
    Debug.Log(current);

if (entity.HasHealth())
    entity.DelHealth();
```
`valueCapacity`/`tagCapacity` переданы не для красоты: `ConstructValues` считает размер массивов один раз через `CeilToPrime(capacity, out _valuePrimeIndex)`. `CeilToPrime` берёт наименьшее простое из `PrimeTable` (`Entities/Scripts/Utils/InternalUtils.cs:22`), не меньшее аргумента, а первый элемент таблицы — `2`. Значит `new Entity()` без аргументов стартует не с нулевой, а с ёмкостью `2`: третий `AddX` уже уйдёт через `IncreaseValueCapacity()`. Запрошенные `4` и `8` округляются до `7` и `17`.

### 3. `Add{Name}` и `Set{Name}` — не синонимы

```csharp
// ХОРОШО: Add один раз при сборке сущности, Set — для всех последующих изменений
public sealed class HealthInstaller : IEntityInit<IPlayer>
{
    public void Init(IPlayer player) => player.AddHealth(100);
}

public sealed class DamageBehaviour : IPlayerTick
{
    public void Tick(IPlayer player, float deltaTime) => player.SetHealth(player.GetHealth() - 1);
}
```

```csharp
// ПЛОХО: второй AddHealth бросит ArgumentException
player.AddHealth(100);
player.AddHealth(80);   // ArgumentException: "A value with the same key Health already has been added…"
```
`Entity.AddValue` обходит бакет и бросает `ValueAlreadyAddedException(key)` (это `ArgumentException`) при совпадении ключа; `SetValue` — upsert. Асимметрия с тегами: `AddTag` на уже существующем теге просто возвращает `false`.

### 4. Unsafe — только для ссылочных типов значений

```csharp
// ХОРОШО: значение — ссылочный тип, поэтому where T : class выполнен
using Atomic.Elements;
using Atomic.Entities;

[EntityExtensionsAPI]
public static partial class PlayerAPI
{
    public static readonly ValueKey<IEntity, int> Score = new(nameof(Score));                       // safe

    [Unsafe]
    public static readonly ValueKey<IEntity, IVariable<int>> Health = new(nameof(Health));          // unsafe
}

// вызов
IVariable<int> hp = entity.GetHealth();      // GetValueUnsafe<IVariable<int>> — без каста
hp.Value -= 10;                              // мутация на месте, без записи в сущность
```

```csharp
// ПЛОХО: не компилируется — int не удовлетворяет where T : class
[EntityExtensionsAPI(Unsafe = true)]
public static partial class PlayerAPI
{
    public static readonly ValueKey<IEntity, int> Health = new(nameof(Health));
    // сгенерированный Get/Ref/TryGet развернутся в GetValueUnsafe<int> / TryGetValueUnsafe<int>
}
```
Этот «плохой» вариант — ровно тот, что приведён в `Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md` в разделе Unsafe Mode. Он не собирается.

### 5. `Ref{Name}` — когда нужно заменить саму ссылку в слоте

```csharp
[EntityExtensionsAPI]
public static partial class PlayerAPI
{
    [Unsafe]
    public static readonly ValueKey<IEntity, IVariable<int>> Health = new(nameof(Health));
}

// подмена хранимого объекта без DelValue/AddValue и без повторного обхода бакетов
ref IVariable<int> slot = ref entity.RefHealth();
slot = new ReactiveVariable<int>(100);
```
`Ref{Name}` разворачивается в `ref entity.GetValueUnsafe<T>(key)`, то есть в `ref Unsafe.As<object, T>(ref slot.value)` — ссылку прямо в поле `ValueSlot.value`. Это единственный способ получить `ref` через типизированный двухгенериковый ключ: ручная перегрузка `GetValueUnsafe<E, T>(this E, ValueKey<E, T>)` возвращает `T` по значению. Цена: запись через этот `ref` **не поднимает** `OnValueChanged`, потому что идёт мимо `SetValue`.

### 6. Одно поле на объявление

```csharp
// ХОРОШО
public static readonly TagKey<IEntity> Alive = new(nameof(Alive));
public static readonly TagKey<IEntity> Dead = new(nameof(Dead));
```

```csharp
// ПЛОХО: fieldDecl.Declaration.Variables.Count != 1 — генератор молча пропустит ОБА поля,
// при этом анализатор проверит их оба (он итерирует все Variables)
public static readonly TagKey<IEntity> Alive = new(nameof(Alive)), Dead = new(nameof(Dead));
```
Результат «плохого» варианта — отсутствующие `HasAliveTag`/`HasDeadTag` без единого предупреждения.

### 7. Явная двухаргументная форма `ValueKey`

```csharp
// ХОРОШО: читается однозначно — значение int на IEntity
public static readonly ValueKey<IEntity, int> Health = new(nameof(Health));
```

```csharp
// ПЛОХО: парсер трактует арность 1 как ТИП ЗНАЧЕНИЯ на Atomic.Entities.IEntity,
// то есть это «значение типа IEntity», а не «ключ для IEntity»
public static readonly ValueKey<IEntity> Target = new(nameof(Target));
```

### 8. Событийный API: ключ на интерфейсе шины, подписка через `using`

```csharp
using System;
using Atomic.Entities;   // нужен для IEntity в полезной нагрузке EntityHealed
using Atomic.Events;

namespace Game.Gameplay
{
    [EventExtensionsAPI]
    public static partial class GameEventAPI
    {
        public static readonly EventKey<IEventBus> PlayerTurnStarted = new(nameof(PlayerTurnStarted));
        public static readonly EventKey<IEventBus, int> DamageDealt = new(nameof(DamageDealt));
        public static readonly EventKey<IEventBus, IEntity, int> EntityHealed = new(nameof(EntityHealed));
    }
}
```

```csharp
public sealed class DamagePresenter : IDisposable
{
    private readonly IEventBus _bus;
    private readonly Subscription<int> _damage;

    public DamagePresenter(IEventBus bus)
    {
        _bus = bus;
        _damage = bus.SubscribeDamageDealt(this.OnDamage);
    }

    public void Dispose() => _damage.Dispose();

    private void OnDamage(int amount) { /* … */ }
}

// для короткоживущей подписки
using var sub = bus.SubscribeDamageDealt(amount => Debug.Log(amount));
```
`EventKey<IEventBus, …>` вместо `EventKey<EventBus, …>` — потому что расширение генерируется на тип первого аргумента; на `IEventBus` оно будет работать и с `EventBus`, и с `ThreadSafeEventBus`. Поле `Subscription<int>` объявлено конкретным типом, а не `IDisposable`: `Subscription<T>` — `readonly struct`, присваивание в поле типа `IDisposable` его забоксирует.

### 9. Unsafe на событиях — только для одного подписчика

```csharp
// ХОРОШО: один владелец, подписка заводится один раз на старте
[EventExtensionsAPI]
public static partial class TickEventAPI
{
    [Unsafe]
    public static readonly EventKey<IEventBus, float> FrameTick = new(nameof(FrameTick));
}

bus.SubscribeFrameTick(_systems.Tick);   // SubscribeUnsafe — единственный подписчик
bus.InvokeFrameTick(Time.deltaTime);     // InvokeUnsafe — без проверки наличия
```

```csharp
// ПЛОХО: второй SubscribeFrameTick бросит ArgumentException (AddEvent, EventBus.cs:270-276)
bus.SubscribeFrameTick(_ui.Tick);
bus.SubscribeFrameTick(_audio.Tick);     // ArgumentException

// ПЛОХО: InvokeUnsafe без подписчиков — KeyNotFoundException
var empty = new EventBus();
empty.InvokeFrameTick(0.016f);           // KeyNotFoundException
```
Это не «риск», а разница в семантике: safe `Subscribe` делает `Delegate.Combine`, `SubscribeUnsafe` — `AddEvent`, который бросает на существующем ключе. Подтверждено тестами `EventBusTests.SubscribeUnsafe_0Arg_DuplicateKey_ThrowsArgumentException` и `InvokeUnsafe_0Arg_MissingKey_ThrowsKeyNotFoundException`.

### 10. Доменные поведения: один маркер, девять интерфейсов

```csharp
using Atomic.Entities;

namespace Game.Domain
{
    public interface IPlayer : IEntity
    {
    }

    [EntityDomainBehaviours(typeof(IPlayer))]
    public static class PlayerDomain
    {
    }
}
```

```csharp
namespace Game.Domain
{
    public sealed class PlayerInitBehaviour : IPlayerInit
    {
        public void Init(IPlayer player) => player.AddHealth(100);
    }

    public sealed class PlayerMoveBehaviour : IPlayerTick
    {
        public void Tick(IPlayer player, float deltaTime)
        {
            // сущность приходит параметром; поле с кэшированным player не нужно
        }
    }
}
```
Почему эта форма правильная: generic-интерфейсы `Atomic.Entities` реализуют негенериковый метод через default interface implementation и бросают `InvalidCastException`, если сущность не того типа. То есть типовой контракт проверяется на вызове поведения, а не при его установке, и `IPlayerTick`, навешенный на не-`IPlayer`, упадёт в первом же `Tick`. Передача сущности параметром (а не хранение в поле) — то, под что спроектированы эти интерфейсы.

### 11. Ключ в переменной — через ручные key-перегрузки

```csharp
// Сгенерированное имя метода не параметризуется, поэтому обобщённый код пишется на ключах
public static void Heal<E>(this E entity, ValueKey<E, int> healthKey, int amount)
    where E : IEntity
{
    entity.SetValue(healthKey, entity.GetValue(healthKey) + amount);
}

// вызов: E выводится в IEntity из типа ключа
IEntity player = new Entity("Player", valueCapacity: 8);
player.AddHealth(100);
player.Heal(PlayerAPI.Health, 25);
```
Это же единственный путь для `HasAllTags`/`HasAnyTag` — сгенерированных аналогов у них нет:

```csharp
[EntityExtensionsAPI]
public static partial class PlayerAPI
{
    public static readonly TagKey<IEntity> Alive = new(nameof(Alive));
    public static readonly TagKey<IEntity> Grounded = new(nameof(Grounded));
    public static readonly ValueKey<IEntity, float> Speed = new(nameof(Speed));
}

// только через ручную перегрузку: params TagKey<E>[]
if (entity.HasAllTags(PlayerAPI.Alive, PlayerAPI.Grounded))
    entity.SetSpeed(0f);

// сгенерированный эквивалент пришлось бы писать так
if (entity.HasAliveTag() && entity.HasGroundedTag())
    entity.SetSpeed(0f);
```
`params TagKey<E>[]` означает аллокацию массива на каждый вызов, поэтому в горячем коде конъюнкция сгенерированных `Has…Tag()` предпочтительнее.

### 12. Свой генератор: модели — `readonly struct` с ручным равенством

```csharp
public readonly struct TagField : IEquatable<TagField>
{
    public string Name { get; }
    public string EntityTypeName { get; }

    public TagField(string name, string entityTypeName)
    {
        Name = name;
        EntityTypeName = entityTypeName;
    }

    public bool Equals(TagField other) =>
        Name == other.Name &&
        EntityTypeName == other.EntityTypeName;

    public override bool Equals(object obj) => obj is TagField other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + (Name?.GetHashCode() ?? 0);
        hash = hash * 31 + (EntityTypeName?.GetHashCode() ?? 0);
        return hash;
    }
}
```
Ручное равенство — не стиль, а требование инкрементального кэша Roslyn: он сравнивает модели между проходами и пропускает неизменившуюся работу. Хранить в модели `ISymbol` или `SyntaxNode` нельзя — это удерживает в памяти компиляцию целиком.

### 13. Свой генератор: гейт запуска и изоляция падений

```csharp
internal static readonly bool IsBuildTime = Assembly.GetEntryAssembly() != null;

internal static bool ShouldRun(Compilation compilation)
{
    if (!IsBuildTime)                                          // не работаем в фоновом анализе IDE
        return false;

    if (compilation.Assembly.Name == CodegenAssemblyName)      // не работаем в самой Atomic.Entities
        return false;

    return compilation.ReferencedAssemblyNames.Any(n => n.Name == CodegenAssemblyName);
}

public void Initialize(IncrementalGeneratorInitializationContext context)
{
    var pipeline = context.SyntaxProvider.CreateSyntaxProvider(
        predicate: (node, _) => EntityAPIParser.IsCandidate(node),
        transform: (ctx, _) => EntityAPIParser.Transform(ctx)
    );

    var definitions = pipeline.Where(static def => def.HasValue)
                              .Select(static (def, _) => def!.Value);

    var combined = definitions.Collect()
        .Combine(context.CompilationProvider)
        .Combine(context.ParseOptionsProvider);

    context.RegisterSourceOutput(combined, (sourceProductionContext, tuple) =>
    {
        var ((defs, compilation), parseOptions) = tuple;

        SourceOutputHelpers.Setup(parseOptions);

        if (!ShouldRun(compilation))
            return;

        foreach (var def in defs)
        {
            using var logger = new DiagnosticLogger(sourceProductionContext, def.ClassName, Id);

            try
            {
                string source = CodeEmitter.Emit(def);
                sourceProductionContext.AddSource($"{def.ClassName}.g.cs", source);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError("EAG0002", "EntityAPIGenerator Internal Error", $"Internal error: {exception}");
            }
        }
    });
}
```
Три детали, которые стоит скопировать: `static`-лямбды (нет замыканий, значит нет аллокаций на каждый проход), дешёвый синтаксический предикат до любой семантики, и `try/catch` вокруг каждого определения с исключением `OperationCanceledException` — одно битое определение не убивает генерацию остальных.

### 14. На время миграции — дамп сгенерированного кода на диск

Добавьте `ATOMIC_OUTPUT_SOURCEGEN_FILES` в `Edit → Project Settings → Player → Scripting Define Symbols`. Файлы лягут в `Temp/GeneratedCode/{assembly}/{ClassName}.g.cs`, лог — в `Temp/GeneratedCode/SourceGen.log`. Это ровно тот артефакт, который можно построчно сравнить со старыми yaml-генерированными файлами и убедиться, что ни один метод не потерялся.

---

## Производительность

Что подтверждено исходником или заявлено в документации:

- **Нет разрешения имён в рантайме.** Сгенерированный вызов передаёт `int` из `static readonly` поля: `entity.GetValue<int>(PlayerAPI.Health.Id)`. Имя превращается в id ровно один раз, в статическом инициализаторе API-класса (`EntityAPIGenerator/CodeEmitter.cs`; конструкторы ключей в `Entities/Scripts/KeyStore/Keys/`).
- **`[MethodImpl(AggressiveInlining)]` — только на entity-расширениях, и только при включённом флаге.** Эмиттер пишет атрибут перед каждым методом, когда `def.AggressiveInlining`. Значение по умолчанию `true` задано **инициализатором свойства атрибута**, а парсер воспроизводит его отдельно: `HasNamedArg(attr, "AggressiveInlining") ? TryGetNamedArgBool(…) : true` (`EntityAPIParser.Transform`). Событийный эмиттер `[MethodImpl]` не пишет вообще — там всё на усмотрение JIT/IL2CPP.
- **Инлайнинг заканчивается на границе интерфейса.** В `Entity_Values.cs` `AggressiveInlining` стоит на 7 методах, в `Entity_Tags.cs` — на 2, и `GetValue<T>`, `GetValueUnsafe<T>`, `TryGetValue<T>` среди них **нет**. То есть сгенерированный однострочник инлайнится, а реализация, в которую он форвардит, — нет; реальная стоимость чтения = виртуальный вызов по `IEntity` + обход бакета.
- **Хранилище — массивы со связными цепочками, не open addressing.** `ValueSlot { int key; object value; bool exists; int next; }` плюс `int[] _valueBuckets`, плюс free-list (`_valueFreeList`, `_valueLastIndex`). Ёмкость — простое число: `_valueCapacity = CeilToPrime(capacity, out _valuePrimeIndex)`, `Array.Fill(_valueBuckets, UNDEFINED_INDEX)`. Проба: `hash = key & 0x7FFFFFFF; bucket = hash % _valueCapacity; index = _valueBuckets[bucket]; … index = slot.next`. На каждый поиск приходится одно целочисленное деление (`%`) — на короткой цепочке именно оно и доминирует. Теги устроены идентично (`TagSlot`, `_tagBuckets`, `CeilToPrime`). `EventBus` — та же схема (`EventSlot`, `_buckets`, `CeilToPrime`).
- **Чтение не копирует слот.** Безопасные пути берут `ref readonly ValueSlot slot = ref _valueSlots[index]`; unsafe-пути берут мутабельный `ref ValueSlot`, чтобы вернуть `ref Unsafe.As<object, T>(ref slot.value)`. Аллокаций на чтение нет ни в одном из них.
- **Боксинг на границе интерфейса — неустраним для структурных значений.** `void AddValue(int key, object value)`, `void SetValue(int key, object value)`, поле слота — `object value`, а `GetValue<T>` возвращает `(T) slot.value`. Значит `entity.AddHealth(100)` боксирует, `entity.GetHealth()` распаковывает, и обходного пути в хранилище нет. Для горячего числового состояния единственный вариант — ссылочный держатель (например `IVariable<int>`), положенный один раз и мутируемый на месте; он же единственная форма, совместимая с `Unsafe`/`Ref{Name}`.
- **Что реально даёт unsafe на сущностях — это снятый каст, а не снятая проверка наличия.** `ref T GetValueUnsafe<T>(int key) where T : class` обходит бакет точно так же и бросает `ValueNotFoundException(key)` (это `KeyNotFoundException`) при промахе. Экономится проверка типа: вместо `(T) slot.value` — `Unsafe.As<object, T>`. В Unity этот `Unsafe` — `Unity.Collections.LowLevel.Unsafe.UnsafeUtility`, а не `System.Runtime.CompilerServices.Unsafe` (условный `using` в `Entity_Values.cs:8-12`). Утверждение документации, что unsafe-аксессоры «skip runtime existence checks» и могут вернуть мусор, для `Atomic.Entities` неверно.
- **События: цена safe-пути — проба + `Delegate.Combine` на подписчика.** `Subscribe` делает `TryGetEvent`, затем `(Action) del + action` (аллокация нового делегата) и `SetEvent`. `SubscribeUnsafe` пропускает пробу и комбинирование, но бросает `ArgumentException` на уже существующем ключе. `InvokeUnsafe` — `GetEventUnsafe<Action>(key).Invoke()`: одна проба, без ветки на наличие, `KeyNotFoundException` при отсутствии. `UnsubscribeUnsafe` дешёвым **не** является: `GetEventUnsafe` (бросает при отсутствии), затем вычитание делегата и `SetEvent`/`RemoveEvent`.
- **Полезная нагрузка событий не аллоцирует.** `Invoke<T1,T2,T3>(int, T1, T2, T3)` передаёт аргументы generic-параметрами в `Action<…>` — нет ни `object[]`, ни класса event-args. Структурные аргументы боксируются только если целевой делегат не generic.
- **Дескрипторы подписок бесплатны.** `Subscription`, `Subscription<T>`, `Subscription<T1,T2>`, `Subscription<T1,T2,T3>` — `readonly struct : IDisposable`; `using var sub = bus.SubscribeX(…)` не аллоцирует под сам дескриптор (аллоцируется только делегат). Присваивание в поле типа `IDisposable` боксирует.
- **Ключи бесплатны в передаче.** Все типы ключей — `readonly struct` с единственным полем `public readonly int Id`, ручным `IEquatable<T>` и `GetHashCode() => Id`. `Equals(object)` боксирует — сравнивайте типизированной перегрузкой или по `.Id`. `NameToId`/`IdToName` в обоих хранилищах помечены `AggressiveInlining` и представляют собой обычный поиск по `Dictionary`, вызываемый один раз на ключ.
- **Каждая сгенерированная запись поднимает события сущности.** `AddValue` → `OnValueAdded` + `OnStateChanged`, `SetValue` → `OnValueChanged` или `OnValueAdded` + `OnStateChanged`, `DelValue` → `OnValueDeleted` + `OnStateChanged`, `AddTag`/`DelTag` → `OnTagAdded`/`OnTagDeleted` + `OnStateChanged`. Это механизм, которым инспекторы, фильтры и реактивные мосты видят сгенерированные записи — и постоянная накладная стоимость на каждую запись, даже когда подписчиков нет (null-проверка делегата).
- **`SetValue` сравнивает старое значение с новым.** `if (!slot.value.Equals(value))` — виртуальный `Equals` на уже боксированном `object`. Если значение не изменилось, запись и `OnValueChanged` не происходят; для структур это сравнение по значению, для ссылочных типов без переопределённого `Equals` — по ссылке.
- **Обход состояния без аллокаций — только через `CopyValues`/`CopyTags`.** `GetValues()` внутри делает `new KeyValuePair<int, object>[_valueCount]` и зовёт `CopyValues`; `GetTags()` аналогично. `Entity` даёт публичный `ValueEnumerator GetValueEnumerator()` (структурный энумератор, без боксинга) и отдельную явную реализацию `IEnumerator<KeyValuePair<int, object>> IEntity.GetValueEnumerator()` — то есть через `IEntity` вы получаете боксированный интерфейс, а через конкретный `Entity` — нет. То же для `TagEnumerator`.
- **Генераторы ничего не стоят в рантайме.** Прямая цитата из `SourceGenerators/Project~/Docs/Implementation.md:171`: «Generators run only at build time; they do not add runtime overhead». DLL не должны попадать в плеерный билд.
- **Пропускная способность самих генераторов.** `ref struct CodeWriter` с одним `StringBuilder(1024)` на файл (не боксируется, не захватывается лямбдой); `readonly struct` модели с ручным равенством (хеш 17/31) для инкрементального кэша; `static`-лямбды в конвейере; три ранних выхода в `ShouldRun`. `Directory.Build.props` фиксирует `Microsoft.CodeAnalysis.CSharp` 4.3.0, `Microsoft.CodeAnalysis.Analyzers` 3.3.4, `netstandard2.0`, `LangVersion 12`, `Nullable enable`, `IsRoslynComponent true`, `EnforceExtendedAnalyzerRules true` и `NoWarn RS1035` (чтобы `SourceOutputHelpers` могла делать файловый IO).
- **Дамп стоит немного даже выключенным.** `SourceOutputHelpers.Setup()` на каждом проходе сбрасывает флаг и сканирует `parseOptions.PreprocessorSymbolNames` в поисках `ATOMIC_OUTPUT_SOURCEGEN_FILES` — то есть перебор символов происходит всегда.

Чего в исходниках и документации **нет**: ни одного числа. Бенчмарков, замеров аллокаций, сравнений safe/unsafe в репозитории не обнаружено. Все утверждения выше — чтение кода и явные формулировки документации; «быстрее на X%» ниоткуда не следует, и делать такие выводы в проектных решениях не стоит без собственного профайла. Отдельно помечу как **вывод, а не факт**: оценка «`%` доминирует на короткой цепочке» — это рассуждение о стоимости целочисленного деления относительно пары сравнений, а не измерение.

---

## Грабли

- **`[Unsafe(false)]` из документации не компилируется.** Оба `UnsafeAttribute` (в `Atomic.Entities` и в `Atomic.Events`) объявлены с единственным неявным конструктором без параметров. При этом `[Unsafe(false)]` показан как способ вернуть поле в safe-режим в `Docs/Entities/CodeGen/UnsafeAttribute.md:54,60,90`, `Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md:85,141`, `Docs/Events/CodeGen/UnsafeAttribute.md:55,61,91`, `Docs/Events/CodeGen/EventExtensionsAPIAttribute.md:81,135` и `SourceGenerators/Project~/Docs/EntityAPIGenerator.md:237,245`. Сама документация признаётся, что это опирается на «implicit `bool` conversion supported by the source generator parser» — такого механизма не существует. Хуже: оба парсера считают `fieldUnsafe = classUnsafe || HasUnsafeAttribute(fieldDecl)`, а `HasUnsafeAttribute` смотрит только на имя атрибута и игнорирует аргументы. Следствие: внутри класса с `Unsafe = true` **нет** способа вернуть одно поле в safe-режим. Командное правило: держать класс safe и включать отдельные поля голым `[Unsafe]`.
- **Unsafe несовместим со структурными значениями.** `where T : class` на `GetValueUnsafe<T>`/`TryGetValueUnsafe<T>`. Все unsafe-примеры в документации используют `int`/`float` и не собираются. XML-документация на эти методы вдобавок говорит «struct type» — это неверно.
- **`Add{Name}` бросает на дубликате, а `Add{Name}Tag` — нет.** `AddValue` → `ArgumentException`; `AddTag` → `false`. Про это легко забыть при переносе кода, который раньше вызывал `AddXxx` идемпотентно.
- **`Set{Name}(null)` и `Add{Name}(null)` бросают `ArgumentNullException`, а не удаляют значение.** Чтобы убрать ссылочное значение, нужен `Del{Name}()`.
- **Запись через `Ref{Name}` не поднимает `OnValueChanged`.** Она идёт прямо в `ValueSlot.value`, минуя `SetValue`. Всё, что подписано на изменения состояния (инспектор, фильтры коллекций, реактивные мосты), эту запись не увидит.
- **`SubscribeUnsafe` на событии с двумя подписчиками — не риск, а баг.** Safe `Subscribe` комбинирует делегаты, `SubscribeUnsafe` зовёт `AddEvent`, который бросает `ArgumentException` (`EventBus.cs:270-276`). `InvokeUnsafe` без подписчиков бросает `KeyNotFoundException` (тесты `EventBusTests.cs:184,192-199,221-228`). `UnsubscribeUnsafe` без подписки — тоже `KeyNotFoundException` (`EventBus.cs:177-215`, `EventBusTests.cs:503-509`).
- **Молчаливые пропуски полей.** Генератор не выдаёт ничего, когда игнорирует поле: не `static`; `Variables.Count != 1`; тип не из ровно `Atomic.Entities` / `Atomic.Events`; `TagKey` арности не 0 и не 1; `ValueKey` арности не 1 и не 2; `EventKey` арности вне 1..4. Симптом — отсутствующий метод расширения. Анализатор при этом проверяет инициализаторы и у многообъявленных полей, и у ключей «неправильной» арности — то есть EAPI-ошибки вы увидите, а пропуск генерации — нет.
- **Класс API обязан быть ровно `public static partial`, и это нигде не проверяется.** Ни `EntityAPIParser.Transform`, ни анализатор не смотрят на модификаторы класса, а эмиттер безусловно пишет `public static partial class {ClassName}` (`EntityAPIGenerator/CodeEmitter.cs:42`, `EventAPIGenerator/CodeEmitter.cs:41`). `internal` даёт `CS0262`, отсутствие `partial` — `CS0260`, причём на сгенерированном файле и без объясняющей диагностики.
- **Вложенный API-класс.** `EntityAPIParser.GetNamespace` поднимается по дереву только до `BaseNamespaceDeclarationSyntax` и цепочку вмещающих типов не учитывает (это **вывод из кода парсера**, не протестированный сценарий). Значит для `class Outer { [EntityExtensionsAPI] public static partial class PlayerAPI { … } }` сгенерируется класс `PlayerAPI` на уровне namespace, у которого полей-ключей нет, и ссылки `PlayerAPI.Health.Id` не разрешатся. Держите API-классы на верхнем уровне namespace.
- **`hintName` для entity и event генераторов — `{ClassName}.g.cs` без namespace.** Два `[EntityExtensionsAPI]` класса с одинаковым именем в разных namespace одной сборки столкнутся. Имена hint-файлов при этом **пер-генераторные**, поэтому entity-класс и event-класс с одним именем не конфликтуют. Доменный генератор использует другой шаблон: `{ClassName}.{EntityShortName}.DomainBehaviours.g.cs` (`EntityDomainGenerator.cs:89`).
- **Коллизии имён методов.** Тег `Alive` даёт `HasAliveTag`, а значение `AliveTag` даёт `HasAliveTag` — то же имя. Плюс одинаково названные ключи в двух API-классах на одном `IEntity` дают неоднозначные методы расширения в файле, где импортированы оба namespace.
- **`[EntityDomainBehaviours(typeof(X))]` с неподходящим `X` не генерирует ничего и молчит.** `ImplementsIEntity` требует `Atomic.Entities.IEntity` напрямую или в `AllInterfaces`; иначе `Transform` возвращает `null`. То же при неконстантном/не-типовом аргументе.
- **Имя доменного интерфейса — это `entityType.Name` как есть.** `typeof(IPlayer)` → `IPlayerTick`, но `typeof(Player)` → `PlayerTick` без ведущей `I`. Коллизии между двумя доменами с одинаковым коротким именем в одном namespace не детектируются.
- **`IEntityGizmos` объявлен внутри `#if UNITY_5_3_OR_NEWER`**, а доменный генератор выдаёт `IPlayerGizmos : IEntityGizmos<IPlayer>` всегда. В Unity-компиляции это не проблема; в сборке тех же исходников вне Unity сгенерированный код не соберётся.
- **`IPlayerTick` на не-`IPlayer` падает в рантайме, не на сборке.** Generic lifecycle-интерфейсы реализуют негенериковый метод через default implementation и бросают `InvalidCastException` с развёрнутым сообщением. Ошибка всплывает в первом тике, а не при установке поведения.
- **Генераторы не работают в IDE — это норма.** `IsBuildTime = Assembly.GetEntryAssembly() != null`. Новые ключи не появятся в IntelliSense до реального билда/перезагрузки домена. Это сделано ради отзывчивости, но стабильно читается как «генератор сломался».
- **Нет ссылки на `Atomic.Entities`/`Atomic.Events` в asmdef — нет сгенерированного кода, без сообщений.** Третий ранний выход `ShouldRun`.
- **Настройки импорта DLL — несущая конструкция.** Каждой DLL в `Assets/Plugins/Atomic/SourceGenerators/` нужен asset label `RoslynAnalyzer`, **снятый Any Platform и снятые все отдельные платформы**, включая Editor и Standalone. После изменения — Apply, затем `Assets → Reimport All` или перезапуск. Отдельно: `SourceGenerators/Project~/Docs/Setup.md:81` и `Docs/Entities/Manual.md:107` говорят про «четыре DLL», а в папке их пять — `EntityDomainGenerator.dll` в этих списках забыт, но настроить его надо точно так же.
- **Версия Roslyn зафиксирована.** Unity 6000.3.x несёт Roslyn 4.3.0; `ForAttributeWithMetadataName` (≥ 4.6) использовать нельзя. Если генератор молча не сработал — смотрите `Editor.log` на `CS9057` (version mismatch) и на отсутствующие DLL.
- **Id ключей нельзя сохранять.** `SequentialEntityKeyAlgorithm.NameToId(string name) => _nextId++` игнорирует имя; первый запрошенный id равен 1 (тест `EntityKeyStoreTests.NameToId_FirstName_ReturnsIdOne`). Id зависит от порядка статической инициализации и не стабилен между запусками и сборками.
- **`EntityKeyStore.SetAlgorithm`/`Reset` после создания ключей ломает соответствие.** `Reset()` чистит оба кэша и зовёт `_algorithm.Reset()` (у `SequentialEntityKeyAlgorithm` это `_nextId = 1`); `SetAlgorithm()` чистит оба кэша и подменяет сам алгоритм — счётчика он не трогает, но свежий экземпляр начинает нумерацию заново. В обоих случаях значения, уже скопированные в `static readonly` поля ключей, остаются старыми — новые имена начнут получать те же id. Менять алгоритм можно только до того, как инициализируется первый API-класс.
- **Оба хранилища ключей не потокобезопасны** (обычные `Dictionary` без блокировок), хотя `ThreadSafeEventBus` в фреймворке есть. Создавайте ключи на главном потоке.
- **`default` на ключе компилируется.** `TagKey`/`ValueKey`/`EventKey` — структуры, поэтому `= default` валидный C# и молча означает `Id = 0`. Именно для этого существует `EAPI0002`. Оба правила имеют `DiagnosticSeverity.Error`, то есть ломают сборку, а не инспекцию; и оба анализатора используют **одни и те же** id `EAPI0001`/`EAPI0002`, так что правило в `.editorconfig` или `#pragma warning disable EAPI0001` действует сразу на entity и на events.
- **Простые типы полей и legacy-структура `Tag` больше не поддерживаются вовсе** (`SourceGenerators/Project~/Docs/EntityAPIGenerator.md:75`: «Plain types and the legacy `Tag` struct are no longer supported»). Каждое поле должно быть `TagKey`, `TagKey<E>`, `ValueKey<T>` или `ValueKey<E, T>`, инициализированным не-default конструктором: `new(nameof(X))`, `new("Alive")` и `new(123)` допустимы, `new()` и `default` — ошибки сборки.
- **Не правьте сгенерированные файлы и не добавляйте в проект генератора вторую class-library DLL** — загрузчик анализаторов Unity может её не разрешить. Общий код подключается как `<Compile Include="..\Atomic.SourceGenerators.Shared\*.cs" />`.
- **Дамп сгенерированного кода пишется по относительному пути** `Temp/GeneratedCode` от рабочего каталога компилятора, а ошибки IO глотаются молча. Если файлы не появились — дело может быть не в дефайне.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Документация по типам (вне плагина)

- `Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md` — 156 строк. Пример объявления API-класса, пример вызовов, раздел «Unsafe Mode» (включая **не собирающиеся** примеры с `int` и `[Unsafe(false)]` на строках 85 и 141), затем API Reference с таблицей свойств `Unsafe`/`AggressiveInlining`. Главная точка входа, если нужно понять форму декларации.
- `Docs/Entities/CodeGen/UnsafeAttribute.md` — 114 строк. Два раздела примеров: «Opt-in to Unsafe» (рабочий) и «Force Safe Mode» (нерабочий, `[Unsafe(false)]`). В заметках к типу прямо сказано, что форсирование safe «relies on the implicit `bool` conversion supported by the source generator parser», а также повторено неверное утверждение про обход проверки наличия значения. Читать с поправками из главы.
- `Docs/Entities/CodeGen/EntityDomainBehavioursAttribute.md` — 189 строк. Самый полезный файл по доменным поведениям: раздел «All Generated Interfaces» (строки 64+) перечисляет все девять сгенерированных интерфейсов для `IPlayer` и даёт реализацию-скелет каждого — оттуда берутся точные имена методов, включая `DrawGizmos`. Дальше — конструктор и свойство `EntityType`.
- `Docs/Entities/CodeGen/EntityAPIAnalyzer.md` — 63 строки. Короткий и точный: пример кода с `EAPI0001` и `EAPI0002` в комментариях, тот же код после code fix, список правил. Расхождений с исходником не найдено.
- `Docs/Events/CodeGen/EventExtensionsAPIAttribute.md` — 138 строк. Событийный аналог: объявление `EventKey` всех арностей, вызовы `Invoke*`/`Subscribe*` с `using var`, раздел «Unsafe Mode» (строки 81 и 135 — снова `[Unsafe(false)]`), таблица свойств. Учтите, что `AggressiveInlining` у этого атрибута нет.
- `Docs/Events/CodeGen/UnsafeAttribute.md` — 115 строк. Зеркало entity-версии, с теми же двумя дефектами.
- `Docs/Events/CodeGen/EventAPIAnalyzer.md` — 63 строки. Зеркало `EntityAPIAnalyzer.md`; те же id `EAPI0001`/`EAPI0002`.

### Обзорные главы

- `Docs/Entities/Manual.md` — «🧬 Entity API Source Generation» (строка 40), внутри: «### Analyzer» (92) с таблицей правил, «### Setup» (103) с настройкой импорта DLL (упоминает четыре DLL вместо пяти) и «#### Inspecting generated source» (120) про `ATOMIC_OUTPUT_SOURCEGEN_FILES`; дальше «🎭 Entity Domain Behaviours» (138). Единственное место, где всё это собрано рядом.
- `Docs/Events/Manual.md` — «### Define Events with Source Generation» (32), «### Thread-Safe Dispatch» (61), «🧬 Event API Source Generation» (75) с «### Unsafe Mode» (81), «### Analyzer» (117), «### Setup» (122) и «📌 Best Practices» (189), где сказано использовать `ThreadSafeEventBus` для фоновых потоков и звать его `Flush()` раз в кадр на главном потоке.

### Документация внутри проекта генераторов

- `Assets/Plugins/Atomic/SourceGenerators/Project~/README.md` — 158 строк. Quick Start, Requirements, «What It Generates» (Entity API / Event API), «Important Notes» (компайл-тайм-онли, `RoslynAnalyzer`, Roslyn 4.3.0) и команды сборки. Самый быстрый обзор.
- `.../Project~/Docs/Setup.md` — 186 строк. Единственный файл с полной процедурой развёртывания: prerequisites (Unity 6 / .NET SDK 7-8+), `dotnet build Atomic.SourceGenerators.sln -c Release`, автоматический деплой через `-p:AtomicDeployToUnity=true -p:AtomicUnityPluginDir="…"`, целевая раскладка файлов, **пошаговая настройка импорта с полным списком галочек** (строки 79-110), раздел Verification и троубльшутинг. При миграции читать первым.
- `.../Project~/Docs/Implementation.md` — 173 строки. Для тех, кто будет дописывать свои генераторы: структура решения, модель «shared sources компилируются в каждую DLL» с предупреждением не делать вторую class-library, перечень настроек `Directory.Build.props`, описание конвейеров генератора и анализатора, раздел «Roslyn 4.3.0 Compatibility» (строка 121) и пошаговый рецепт «Adding a New Generator» с готовым `.csproj` и целью `CopyToUnity`.
- `.../Project~/Docs/EntityAPIGenerator.md` — 284 строки. Самый подробный файл по entity-генерации: раздел «Generated Code» (строка 108+) с парой вход/выход, затем таблицы генерируемых методов для тегов и значений, таблица свойств атрибута с явной фразой «The entity type is no longer passed to `[EntityExtensionsAPI]`», троубльшутинг и Implementation Notes. Строки 237 и 245 содержат неработающий `[Unsafe(false)]`.
- `.../Project~/Docs/EventAPIGenerator.md` — 175 строк. Таблица «Supported Key Shapes» (какая арность `EventKey` какие методы даёт) и полный пример сгенерированного кода для `GameEventAPI`, включая generic-формы `Subscribe<T>`/`Invoke<T>`.
- `.../Project~/Docs/EntityDomainGenerator.md` — 192 строки. Объявление домена, реализация сгенерированных поведений, пример сгенерированного файла и раздел конфигурации по `[EntityDomainBehaviours]`.
- `.../Project~/Docs/EntityAPIAnalyzer.md` и `.../Project~/Docs/EventAPIAnalyzer.md` — по ~125 строк каждый. Purpose, Rules, Code Fixes (before/after), «Valid and Invalid Examples», Setup, Troubleshooting («Diagnostics do not appear», «False negatives») и Implementation Notes. Полнее, чем одноимённые файлы в `Docs/Entities/CodeGen` / `Docs/Events/CodeGen`.
- `.../Project~/Directory.Build.props` — 25 строк, читается целиком за минуту: `netstandard2.0`, `LangVersion 12`, `Nullable enable`, `IsRoslynComponent`, `EnforceExtendedAnalyzerRules`, пины `Microsoft.CodeAnalysis.CSharp` 4.3.0 / `Microsoft.CodeAnalysis.Analyzers` 3.3.4 и `NoWarn RS1035`.

### Исходники генераторов

- `.../Project~/EntityAPIGenerator/EntityAPIGenerator.cs` — конвейер, `IsBuildTime`, `ShouldRun`, `hintName`, `EAG0001`/`EAG0002`, `using var logger`. Эталон, с которого скопированы два других генератора.
- `.../Project~/EntityAPIGenerator/EntityAPIParser.cs` — **все правила приёма полей**, которые нигде больше не задокументированы: `static`, ровно один `Variables`, namespace ровно `Atomic.Entities`, допустимые арности `TagKey`/`ValueKey`, вычисление `classAggressiveInlining`, `HasUnsafeAttribute` без разбора аргументов, `GetNamespace`. Если метод расширения не появился — ответ здесь.
- `.../Project~/EntityAPIGenerator/CodeEmitter.cs` — точные шаблоны тел, порядок выдачи (значения перед тегами; внутри значения — Get → Ref → TryGet → Add → Has → Del → Set), заголовок файла, условный `using System.Runtime.CompilerServices;`.
- `.../Project~/EntityAPIGenerator/Models/{EntityAPIDefinition,ValueField,TagField}.cs` — образцы инкрементальных моделей с ручным `IEquatable`/`GetHashCode`.
- `.../Project~/EventAPIGenerator/EventAPIParser.cs` и `.../EventAPIGenerator/CodeEmitter.cs` — гейт арности `1..4`, извлечение `busTypeName` из `TypeArguments[0]`, хелперы `GetActionType`/`GetSubscriptionType`/`GetInvokeParams`/`GetInvokeArgs`, и подтверждение, что `[MethodImpl]` здесь не выдаётся.
- `.../Project~/EntityDomainGenerator/EntityDomainParser.cs` — список `BehaviourVariants` из девяти вариантов (он лежит именно в парсере) и `ImplementsIEntity`, то есть условия, при которых генерация молча не происходит.
- `.../Project~/EntityDomainGenerator/EntityDomainEmitter.cs` — формирование имени `{EntityShortName}{Suffix}` и наследование `IEntityX<EntityType>, {Short}Behaviour`.
- `.../Project~/EntityAPIAnalyzer/EntityAPIAnalyzer.cs` — `IsParameterlessOrDefault` (что именно считается «пустой» инициализацией: `default`, `default(T)`, `new()`, `new T()`), `IsKeyField` (арность не проверяется) и факт итерации по всем `Variables`.
- `.../Project~/EntityAPIAnalyzer/EntityAPIDiagnostics.cs`, `.../EventAPIAnalyzer/EventAPIDiagnostics.cs` — дескрипторы `EAPI0001`/`EAPI0002` с полными текстами и `category`; видно, что id совпадают.
- `.../Project~/EntityAPIAnalyzer/EntityAPICodeFixProvider.cs` — построение `= new(nameof(Field))` через `SyntaxFactory` и `BatchFixer` для Fix All.
- `.../Project~/Atomic.SourceGenerators.Shared/{CodeWriter,SourceOutputHelpers,DiagnosticLogger}.cs` — три файла, которые имеет смысл переиспользовать целиком, если команда будет писать свой генератор.

### Рантайм, в который всё это разворачивается

- `Assets/Plugins/Atomic/Entities/Scripts/Codegen/{EntityExtensionsAPIAttribute,UnsafeAttribute,EntityDomainBehavioursAttribute}.cs` — настоящие объявления атрибутов (три файла по 15-30 строк). Открыть обязательно: именно здесь видно отсутствие bool-конструктора у `UnsafeAttribute`.
- `Assets/Plugins/Atomic/Events/Scripts/CodeGen/{EventExtensionsAPIAttribute,UnsafeAttribute}.cs` — то же для событий.
- `Entities/Scripts/KeyStore/Keys/{TagKey,TagKey'1,ValueKey,ValueKey'1}.cs` — четыре структуры ключей; видно, что `Id` — поле, и что негенериковые/односоставные формы существуют.
- `Entities/Scripts/KeyStore/EntityKeyStore.cs` + `Algorithms/{IEntityKeyAlgorithm,SequentialEntityKeyAlgorithm}.cs` — маппинг имён, `[InitializeOnEnterPlayMode]`, и тот самый `NameToId(string name) => _nextId++`.
- `Events/Scripts/Keys/EventKeyStore.cs` + `Events/Scripts/Keys/Algorithms/` — событийный аналог с тремя алгоритмами (`Sequential`, `Fnv1A`, `SHA256`).
- `Events/Scripts/Keys/Keys/EventKey.cs` и ``EventKey`1.cs``…``EventKey`3.cs`` — четыре арности событийных ключей, с `==`/`!=`.
- `Entities/Scripts/Entities/Interface/IEntity_Values.cs` и `IEntity_Tags.cs` — контракт, в который форвардит каждый сгенерированный метод; здесь ограничения `where T : class` и `object`-параметры записи.
- `Entities/Scripts/Entities/Core/Entity_Values.cs` — реализация: `ValueSlot`, бакеты, `CeilToPrime`, free-list, условный `using Unsafe = …` (строки 8-12), `AddValue` с броском на дубликате, `SetValue` со сравнением старого значения, `GetValueUnsafe`, `CopyValues`, `ValueEnumerator`, фабрики исключений (строки 596-606). Самый важный файл для понимания цены операций.
- `Entities/Scripts/Entities/Core/Entity_Tags.cs` — то же для тегов; `AddTag` возвращает `false` на дубликате.
- `Entities/Scripts/Entities/Core/Entity.cs` — конструкторы (включая `Entity(string name = null, int tagCapacity = 0, int valueCapacity = 0, int behaviourCapacity = 0, Settings? settings = null)`) и структура `Settings { bool disposeValues; }`.
- `Entities/Scripts/Entities/Extensions_Tags.cs` и `Extensions_Values.cs` — типизированный путь без генератора, включая `HasAllTags`/`HasAnyTag` и расхождение в возвращаемом типе `GetValueUnsafe<E, T>` (строки 138, 142, 146).
- `Entities/Scripts/Behaviours/IEntity{Behaviour,Init,Enable,Tick,FixedTick,LateTick,Disable,Dispose,Gizmos}.cs` — базовые lifecycle-интерфейсы; здесь точные сигнатуры методов и default implementation с `InvalidCastException`.
- `Events/Scripts/Bus/Runtime/IEventBus.cs` — полный контракт шины.
- `Events/Scripts/Bus/Runtime/EventBus.cs` — реализация safe/unsafe: `Subscribe` с `Delegate.Combine`, `AddEvent` с броском (строки 270-276), `UnsubscribeUnsafe` (строки 177-215), `GetEventUnsafe`, фабрики исключений (строки 500-507).
- `Events/Scripts/Bus/Runtime/ThreadSafeEventBus.cs` — обёртка с `ConcurrentQueue<Action>` и `Flush()`.
- `Events/Scripts/Bus/Runtime/Extensions.cs` — key-перегрузки и строковые перегрузки всех операций шины.
- `Events/Scripts/Subscription/Subscription.cs` и ``Subscription`1.cs``…``Subscription`3.cs`` — четыре `readonly struct` дескриптора; `Dispose()` всегда зовёт безопасный `Unsubscribe`.

### Тесты как исполняемая спецификация

- `Entities/Tests/EditMode/KeyStore/EntityKeyStoreTests.cs` — `NameToId_NullName_ThrowsArgumentNullException`, `NameToId_FirstName_ReturnsIdOne`, `NameToId_SequentialNames_ReturnsSequentialIds`. Короткий файл, который закрепляет нестабильность id.
- `Events/Tests/EventBusTests.cs` — зафиксированные исключения unsafe-режима: `InvokeUnsafe_*_MissingKey_ThrowsKeyNotFoundException`, `SubscribeUnsafe_0Arg_DuplicateKey_ThrowsArgumentException`, `UnsubscribeUnsafe_0Arg_MissingKey_ThrowsKeyNotFoundException`. Лучший источник правды по поведению `Unsafe = true` на событиях.
- `Entities/Tests/EditMode/Entity/EntityTests_Extensions_Tags.cs` и `EntityTests_Extensions_Values.cs` — поведение ручных key-перегрузок, на которые стоит опереться, если генератор по какой-то причине не работает.
